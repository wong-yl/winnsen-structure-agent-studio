import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { run16029ParametricModel } from './run_16029_parametric_model.mjs'
import { validate16029ParametricPublication } from './lib/locker_16029_parametric_publication.mjs'
import { acquireNativeWorkerInstanceLock } from './run_16029_native_worker.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
export async function run16029ParametricPortalTask(taskId, {
  dataDir = resolve(ROOT, 'data/native_model_requests'),
  runModel = run16029ParametricModel, validate = validate16029ParametricPublication,
  heartbeatMs = 10000, onProgress = console.log,
} = {}) {
  const store = createNativeTaskStore({ dataDir })
  const initial = await store.read(taskId)
  if (!initial.parametricRequest) return { state: 'refused', reason: 'parametricRequest required' }
  const workerId = `parametric-portal-${process.pid}-${randomBytes(4).toString('hex')}`
  let task = await store.claimTask({ taskId, workerId, leaseMs: 120000 })
  if (!task) return { state: 'not_claimed' }
  const leaseId = task.nativeBuild.lease.id
  const args = () => ({ taskId, workerId, leaseId, expectedRevision: task.revision })
  let inner = null, cancelled = false, controlError = null, serial = Promise.resolve()
  async function control() {
    task = await store.read(taskId)
    if (task.nativeBuild.lease?.id !== leaseId || task.nativeBuild.lease?.workerId !== workerId) throw new Error('portal worker lease changed')
    if (task.nativeBuild.state === 'cancel_requested') {
      cancelled = true
      if (inner) {
        const childStore = createNativeTaskStore({ dataDir: resolve(inner.attempt, 'task_store') })
        const child = await childStore.read(inner.taskId)
        if (!['cancelled', 'cancel_requested', 'ready', 'failed', 'blocked'].includes(child.nativeBuild.state)) await childStore.cancel({ taskId: inner.taskId, expectedRevision: child.revision, transitionId: taskId + '-cancel-child', reason: 'Portal task cancelled' })
      }
    }
    task = await store.heartbeat(args())
  }
  const queueControl = () => { serial = serial.then(control).catch(error => { controlError = error }); return serial }
  let timer
  try {
    task = await store.transition({ ...args(), transitionId: taskId + '-parametric-planning-' + leaseId, toState: 'planning' })
    task = await store.transition({ ...args(), transitionId: taskId + '-parametric-building-' + leaseId, toState: 'building' })
    timer = setInterval(queueControl, heartbeatMs)
    const generated = await runModel(task.parametricRequest, { onProgress: message => {
      try { const row = JSON.parse(message); if (row.taskId && row.attempt) inner = { taskId: row.taskId, attempt: row.attempt } } catch {}
      onProgress(message)
    } })
    clearInterval(timer); timer = null; await serial; await control()
    if (controlError) throw controlError
    if (cancelled || generated.state === 'cancelled_after_atomic_stage') {
      task = await store.transition({ ...args(), transitionId: taskId + '-parametric-cancelled-' + leaseId, toState: 'cancelled' })
      return { state: 'cancelled', modelReady: false }
    }
    task = await store.transition({ ...args(), transitionId: taskId + '-parametric-validating-' + leaseId, toState: 'validating' })
    const checked = validate(generated.attempt, { expectedRequest: task.parametricRequest })
    if (!checked.ok) throw new Error('Parametric publication rejected: ' + checked.reason)
    task = await store.complete({ ...args(), transitionId: taskId + '-parametric-ready-' + leaseId,
      result: { schema: 'winnsen.locker16029.parametric_publication.v1', ...checked } })
    return { state: task.nativeBuild.state, taskId, attempt: checked.attempt, modelReady: true }
  } catch (error) {
    if (timer) clearInterval(timer)
    await serial
    const latest = await store.read(taskId)
    if (latest.nativeBuild.lease?.id === leaseId && latest.nativeBuild.lease?.workerId === workerId) {
      task = latest
      if (task.nativeBuild.state === 'cancel_requested') task = await store.transition({ ...args(), transitionId: taskId + '-parametric-cancelled-' + leaseId, toState: 'cancelled' })
      else task = await store.fail({ ...args(), transitionId: taskId + '-parametric-failed-' + leaseId, code: 'parametric_generation_failed', message: error.message, retryable: false })
    }
    return { state: task.nativeBuild.state, taskId, modelReady: false, error: error.message }
  } finally { if (timer) clearInterval(timer) }
}

export async function runNext16029ParametricPortalTask(options = {}) {
  const dataDir = options.dataDir || resolve(ROOT, 'data/native_model_requests')
  const store = createNativeTaskStore({ dataDir })
  const tasks = await store.listSnapshot()
  const candidate = tasks.filter(task => task.parametricRequest && task.nativeBuild?.state === 'created')
    .sort((a,b) => String(a.createdAt).localeCompare(String(b.createdAt)) || a.id.localeCompare(b.id))[0]
  if (!candidate) return { state: 'idle' }
  if (!(options.cadAvailable || nativeCadAvailable)()) return { state: 'waiting_for_cad', taskId: candidate.id }
  return run16029ParametricPortalTask(candidate.id, { ...options, dataDir })
}

function nativeCadAvailable() {
  const ownerPath = resolve(ROOT, 'data/native_model_requests/.native-worker-instance.lock/owner.json')
  if (existsSync(ownerPath)) {
    try { const owner = JSON.parse(readFileSync(ownerPath, 'utf8')); process.kill(owner.pid, 0); return false } catch (error) { if (error.code !== 'ESRCH') return false }
  }
  const probe = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', '@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count'], { windowsHide: true, encoding: 'utf8', timeout: 15000 })
  return probe.status === 0 && probe.stdout.trim() === '0'
}

export async function watch16029ParametricPortalTasks({ signal, pollMs = 5000, onProgress = console.log, ...options } = {}) {
  const dataDir = options.dataDir || resolve(ROOT, 'data/native_model_requests')
  const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(dataDir, 'parametric_portal_daemon'), workerId: `parametric-watch-${process.pid}` })
  const heartbeat = setInterval(() => lock.heartbeat(), 20000)
  let lastState = ''
  try {
    while (!signal?.aborted) {
      const result = await runNext16029ParametricPortalTask({ ...options, dataDir, onProgress })
      const state = JSON.stringify(result)
      if (state !== lastState) { onProgress(state); lastState = state }
      if (!signal?.aborted) await new Promise(resolveWait => {
        const finish = () => { clearTimeout(timer); signal?.removeEventListener('abort', finish); resolveWait() }
        const timer = setTimeout(finish, pollMs); signal?.addEventListener('abort', finish, { once: true })
      })
    }
  } finally { clearInterval(heartbeat); lock.release() }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (!process.argv[2]) throw new Error('Usage: node tools/run_16029_parametric_portal_worker.mjs <taskId>|--once|--watch')
  if (process.argv[2] === '--watch') {
    const stop = new AbortController()
    process.once('SIGINT', () => stop.abort()); process.once('SIGTERM', () => stop.abort())
    await watch16029ParametricPortalTasks({ signal: stop.signal })
  } else {
    const result = process.argv[2] === '--once' ? await runNext16029ParametricPortalTask() : await run16029ParametricPortalTask(process.argv[2])
    console.log(JSON.stringify(result))
    if (result.error) process.exitCode = 1
  }
}
