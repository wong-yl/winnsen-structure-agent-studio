import { existsSync, mkdirSync, openSync, closeSync, readFileSync, readdirSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn, spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { acquireNativeWorkerInstanceLock } from './run_16029_native_worker.mjs'
import { listOutdoorTasks, readOutdoorJson, updateOutdoorTask, outdoorSpecification } from './lib/outdoor_22057_tasks.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const dataDir = resolve(process.env.STUDIO_REVIEW_DATA_DIR || resolve(root, 'data'))
const release = resolve(dataDir, 'outdoor_22057_release')
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex')
const live = pid => { try { process.kill(pid, 0); return true } catch { return false } }
function cadAvailable() {
  const queue = resolve(root, 'data/native_model_requests')
  if (existsSync(resolve(queue, '.native-worker-instance.lock/owner.json'))) return false
  for (const name of readdirSync(queue).filter(name => /^(NATIVE|PARAMETRIC)-.*\.json$/.test(name))) {
    const state = readOutdoorJson(resolve(queue, name))?.nativeBuild?.state
    if (['created', 'queued', 'claimed', 'planning', 'building', 'running', 'validating'].includes(state)) return false
  }
  const active = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', '@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object {-not $_.HasExited}).Count'], { windowsHide: true, encoding: 'utf8', timeout: 15000 })
  return active.status === 0 && active.stdout.trim() === '0'
}
export async function runNextOutdoorTask({ available = cadAvailable, execute } = {}) {
  const tasks = listOutdoorTasks(dataDir)
  for (const task of tasks.filter(t => t.state === 'running')) {
    if (task.workerPid && live(task.workerPid)) return { state: 'busy', taskId: task.id }
    if (!available()) return { state: 'waiting_for_cad', taskId: task.id }
    await updateOutdoorTask(dataDir, task.id, current => ({ ...current, state: 'failed', error: 'Worker interrupted; evidence preserved', finishedAt: new Date().toISOString() }))
  }
  const task = tasks.filter(t => t.state === 'queued').sort((a, b) => a.createdAt.localeCompare(b.createdAt))[0]
  if (!task) return { state: 'idle' }
  if (!available()) return { state: 'waiting_for_cad', taskId: task.id }
  await updateOutdoorTask(dataDir, task.id, current => ({ ...current, state: 'running', workerPid: process.pid, startedAt: new Date().toISOString() }))
  try {
    const config = task.generator
    const parameters = { ...task.specification }
    if (parameters.columns) parameters.columns = JSON.stringify(parameters.columns)
    outdoorSpecification(new URLSearchParams(parameters), { generation: config })
    if (hash(config.script) !== config.script_sha256 || hash(resolve(config.source_stage, 'qualification.json')) !== config.qualification_sha256) throw new Error('Qualified generator identity changed')
    const logs = resolve(release, 'worker_logs'); mkdirSync(logs, { recursive: true })
    const stdout = openSync(resolve(logs, task.id + '.stdout.log'), 'a')
    const stderr = openSync(resolve(logs, task.id + '.stderr.log'), 'a')
    let code
    try {
      code = execute ? await execute(task) : await new Promise((yes, no) => {
        const child = spawn(config.python, ['-X', 'utf8', config.script, '--task-file', resolve(dataDir, 'outdoor_22057_tasks', task.id + '.json'), '--release-dir', release], { windowsHide: true, stdio: ['ignore', stdout, stderr] })
        child.on('error', no); child.on('exit', value => yes(value ?? 1))
      })
    } finally { closeSync(stdout); closeSync(stderr) }
    if (code !== 0) throw new Error('Native generation or acceptance failed')
    const result = readOutdoorJson(resolve(release, 'generated', task.id, 'result.json'))
    const artifactPath = resolve(release, result?.archive || '')
    if (result?.status !== 'PASS' || result.taskId !== task.id || JSON.stringify(result.specification) !== JSON.stringify(task.specification) || !artifactPath.startsWith(release + '\\') || hash(artifactPath) !== result.sha256 || readFileSync(artifactPath).length !== result.bytes) throw new Error('Task publication identity mismatch')
    await updateOutdoorTask(dataDir, task.id, current => ({ ...current, state: 'ready', finishedAt: new Date().toISOString(), artifact: { archive: result.archive, bytes: result.bytes, sha256: result.sha256 } }))
    return { state: 'ready', taskId: task.id }
  } catch (error) {
    await updateOutdoorTask(dataDir, task.id, current => ({ ...current, state: 'failed', artifact: null, error: error.message, finishedAt: new Date().toISOString() }))
    return { state: 'failed', taskId: task.id, error: error.message }
  }
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(dataDir, 'outdoor_portal_daemon'), workerId: `outdoor-watch-${process.pid}` })
  const timer = setInterval(() => lock.heartbeat(), 20000)
  let stopping = false, lastState = ''
  process.once('SIGINT', () => { stopping = true }); process.once('SIGTERM', () => { stopping = true })
  try {
    do {
      const result = await runNextOutdoorTask(), state = JSON.stringify(result)
      if (state !== lastState) { console.log(state); lastState = state }
      if (process.argv.includes('--watch') && !stopping) await new Promise(yes => setTimeout(yes, 5000))
    } while (process.argv.includes('--watch') && !stopping)
  } finally { clearInterval(timer); lock.release() }
}
