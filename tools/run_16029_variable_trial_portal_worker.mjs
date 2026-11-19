import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  DEFAULT_VARIABLE_TRIAL_TASKS_DIR,
  createVariableTrialTaskStore,
  isPathInsideRoot,
} from './lib/locker_16029_variable_trial_tasks.mjs'
import { runVariableTrialPackage } from '../workers/master_v2/variable_trial/portal_pipeline.mjs'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')
const NATIVE_TASKS_DIR = resolve(ROOT, 'data/native_model_requests')
const NATIVE_LOCK_OWNER_PATH = resolve(NATIVE_TASKS_DIR, '.native-worker-instance.lock/owner.json')
const OUTDOOR_TASKS_DIR = resolve(ROOT, 'data/outdoor_22057_tasks')
const ACTIVE_NATIVE_STATES = new Set(['created', 'claimed', 'planning', 'building', 'validating', 'cancel_requested'])

const delay = (ms) => new Promise((done) => setTimeout(done, ms))

export function isProcessRunning(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    return error?.code === 'EPERM'
  }
}

export function hasBlockingCadOrPortalActivity({
  nativeDir = NATIVE_TASKS_DIR,
  lockOwnerPath = NATIVE_LOCK_OWNER_PATH,
  outdoorDir = OUTDOOR_TASKS_DIR,
} = {}) {
  if (existsSync(lockOwnerPath)) {
    try {
      const owner = JSON.parse(readFileSync(lockOwnerPath, 'utf8'))
      if (isProcessRunning(Number(owner?.pid))) return 'shared_cad_lock_active'
    } catch {
      return 'shared_cad_lock_unreadable'
    }
  }

  if (existsSync(nativeDir)) {
    for (const name of readdirSync(nativeDir)) {
      if (!name.endsWith('.json') || name.startsWith('native_model_request_index')) continue
      try {
        const item = JSON.parse(readFileSync(join(nativeDir, name), 'utf8'))
        if (ACTIVE_NATIVE_STATES.has(item?.nativeBuild?.state)) return `native_task_active:${item.id}`
      } catch {}
    }
  }

  if (existsSync(outdoorDir)) {
    for (const name of readdirSync(outdoorDir)) {
      if (!name.endsWith('.json')) continue
      try {
        const item = JSON.parse(readFileSync(join(outdoorDir, name), 'utf8'))
        if (item?.status === 'queued' || item?.status === 'running' || item?.status === 'cancelling') {
          return `outdoor_task_active:${name}`
        }
      } catch {}
    }
  }

  const probe = spawnSync('powershell.exe', [
    '-NoProfile',
    '-Command',
    '@(Get-CimInstance Win32_Process | Where-Object { $_.Name -match "^(SLDWORKS|sldProcMon|VariableTrial)\\.exe$" }).Count',
  ], { windowsHide: true, encoding: 'utf8' })
  if (probe.status !== 0) return 'process_probe_failed'
  const cadCount = Number(String(probe.stdout || '').trim())
  if (!Number.isFinite(cadCount) || cadCount > 0) return `cad_process_count:${cadCount}`
  return ''
}

export function defaultCadAvailable(options = {}) {
  return hasBlockingCadOrPortalActivity(options) === ''
}

export function verifyVariableTrialPublication(publication, archiveDir) {
  if (!publication || typeof publication !== 'object') return false
  if (!publication.zipPath || !existsSync(publication.zipPath)) return false
  if (!isPathInsideRoot(publication.zipPath, archiveDir)) return false
  const actualSha = createHash('sha256').update(readFileSync(publication.zipPath)).digest('hex').toUpperCase()
  if (actualSha !== String(publication.sha256 || '').toUpperCase()) return false
  if (Number(publication.dxfAuditPassCount) <= 0 || Number(publication.dxfAuditPassCount) !== Number(publication.dxfAuditTotalCount)) return false
  if (Number(publication.drawingPdfCount) <= 0 || Number(publication.drawingSlddrwCount) !== Number(publication.drawingPdfCount)) return false
  if (Number(publication.structuralCollisionCount) !== 0 || Number(publication.unapprovedInterferenceCount) !== 0) return false
  return true
}

export function recoverStaleRunningTasks(store) {
  const recovered = []
  for (const task of store.list()) {
    if (task.status !== 'running' && task.status !== 'cancel_requested') continue
    if (isProcessRunning(Number(task.workerPid))) continue
    const next = store.update(task.id, (draft) => {
      if (draft.status === 'cancel_requested') {
        draft.status = 'cancelled'
        draft.stage = 'cancelled'
        draft.progressDetail = '工作进程重启时发现先前待取消任务，已标记为已取消。'
      } else {
        draft.status = 'queued'
        draft.stage = 'queued'
        draft.progressDetail = '工作进程重启恢复，任务重新排队等待构建。'
      }
      draft.workerPid = null
      return draft
    })
    recovered.push(next.id)
  }
  return recovered
}

function isWorkerProcessAlive(pid) {
  if (!isProcessRunning(pid)) return false
  const probe = spawnSync('powershell.exe', [
    '-NoProfile',
    '-Command',
    `$p = Get-CimInstance Win32_Process -Filter "ProcessId = ${pid}" -ErrorAction SilentlyContinue; if ($p -and $p.CommandLine -like '*run_16029_variable_trial_portal_worker*') { '1' } else { '0' }`,
  ], { windowsHide: true, encoding: 'utf8' })
  return probe.status === 0 && String(probe.stdout || '').trim() === '1'
}

function acquireWorkerLock(lockPath) {
  mkdirSync(dirname(lockPath), { recursive: true })
  if (existsSync(lockPath)) {
    try {
      const current = JSON.parse(readFileSync(lockPath, 'utf8'))
      if (isWorkerProcessAlive(Number(current?.pid)) && Number(current.pid) !== process.pid) {
        throw new Error(`VARIABLE_TRIAL_WORKER_ALREADY_RUNNING pid=${current.pid}`)
      }
    } catch (error) {
      if (String(error?.message || '').includes('VARIABLE_TRIAL_WORKER_ALREADY_RUNNING')) throw error
    }
  }
  writeFileSync(lockPath, JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString() }, null, 2) + '\n', 'utf8')
  return () => {
    try {
      const current = JSON.parse(readFileSync(lockPath, 'utf8'))
      if (Number(current?.pid) === process.pid) rmSync(lockPath, { force: true })
    } catch {}
  }
}

export async function runNextVariableTrialPortalTask({
  dataDir = DEFAULT_VARIABLE_TRIAL_TASKS_DIR,
  cadAvailable = defaultCadAvailable,
  runPackage = runVariableTrialPackage,
  signal = null,
} = {}) {
  const store = createVariableTrialTaskStore({ dataDir })
  const queued = store.list().filter((item) => item.status === 'queued')
  if (!queued.length) return { state: 'idle' }
  if (!cadAvailable()) return { state: 'waiting_for_cad' }

  const claimed = store.claimNextQueued(process.pid)
  if (!claimed || claimed.status !== 'running') return { state: 'idle' }

  const abortController = new AbortController()
  const onParentAbort = () => abortController.abort()
  if (signal) {
    if (signal.aborted) abortController.abort()
    else signal.addEventListener('abort', onParentAbort, { once: true })
  }

  let pollTimer = null
  const startCancelPoll = () => {
    pollTimer = setInterval(() => {
      const current = store.read(claimed.id)
      if (!current || current.status === 'cancel_requested' || current.status === 'cancelled') {
        abortController.abort()
      }
    }, 1000)
  }
  startCancelPoll()

  const archiveDir = store.taskDir(claimed.id)
  try {
    const result = await runPackage(
      {
        normalizedRequest: claimed.request,
        trialAssemblyCase: claimed.trialAssemblyCase,
        layoutSummary: claimed.layoutSummary,
      },
      {
        archiveDir,
        taskId: claimed.id,
        signal: abortController.signal,
        onStage(stage, progressDetail) {
          const current = store.read(claimed.id)
          if (!current || current.status === 'cancel_requested' || current.status === 'cancelled') return
          store.update(claimed.id, (draft) => {
            if (draft.status === 'running') {
              draft.stage = stage
              draft.progressDetail = progressDetail
            }
            return draft
          })
        },
      },
    )

    const latest = store.read(claimed.id)
    if (!latest || latest.status === 'cancel_requested' || latest.status === 'cancelled' || abortController.signal.aborted) {
      store.update(claimed.id, (draft) => {
        draft.status = 'cancelled'
        draft.stage = 'cancelled'
        draft.progressDetail = '任务已安全取消，未发布交付包。'
        draft.workerPid = null
        return draft
      })
      return { state: 'cancelled', taskId: claimed.id }
    }

    if (!verifyVariableTrialPublication(result.publication, archiveDir)) {
      throw new Error('PUBLICATION_VERIFICATION_FAILED')
    }

    store.update(claimed.id, (draft) => {
      draft.status = 'ready'
      draft.stage = 'completed'
      draft.progressDetail = `已完成：${result.publication.sheetMetalCount} 件钣金 DXF 与 ${result.publication.drawingPdfCount} 张 2D 工程图（SLDDRW+PDF）全部通过并封装就绪。`
      draft.productionRelease = false
      draft.publication = result.publication
      draft.error = null
      draft.workerPid = null
      return draft
    })
    return { state: 'ready', taskId: claimed.id, publication: result.publication }
  } catch (error) {
    const latest = store.read(claimed.id)
    const message = error instanceof Error ? error.message : String(error)
    const isCancelled = latest?.status === 'cancel_requested'
      || latest?.status === 'cancelled'
      || abortController.signal.aborted
      || message.includes('TRIAL_INTERRUPTED')
      || message.includes('TRIAL_CANCELLATION_REQUESTED')

    if (isCancelled) {
      store.update(claimed.id, (draft) => {
        draft.status = 'cancelled'
        draft.stage = 'cancelled'
        draft.progressDetail = '任务已安全中止。'
        draft.workerPid = null
        return draft
      })
      return { state: 'cancelled', taskId: claimed.id }
    }

    store.update(claimed.id, (draft) => {
      draft.status = 'failed'
      draft.stage = 'failed'
      draft.progressDetail = `构建未通过：${message}`
      draft.error = { code: 'variable_trial_build_failed', message }
      draft.workerPid = null
      return draft
    })
    return { state: 'failed', taskId: claimed.id, error: message }
  } finally {
    if (pollTimer) clearInterval(pollTimer)
    if (signal) signal.removeEventListener('abort', onParentAbort)
  }
}

export async function watchVariableTrialPortalTasks({
  dataDir = DEFAULT_VARIABLE_TRIAL_TASKS_DIR,
  pollMs = 2500,
  cadAvailable = defaultCadAvailable,
  runPackage = runVariableTrialPackage,
  signal = null,
  onProgress = null,
} = {}) {
  const store = createVariableTrialTaskStore({ dataDir })
  const releaseLock = acquireWorkerLock(join(store.rootDir, '.worker.lock'))
  recoverStaleRunningTasks(store)
  try {
    while (!signal?.aborted) {
      const outcome = await runNextVariableTrialPortalTask({ dataDir: store.rootDir, cadAvailable, runPackage, signal })
      if (typeof onProgress === 'function' && outcome.state !== 'idle') {
        onProgress(JSON.stringify(outcome))
      }
      if (signal?.aborted) break
      await delay(outcome.state === 'ready' || outcome.state === 'cancelled' || outcome.state === 'failed' ? 500 : pollMs)
    }
  } finally {
    releaseLock()
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const watch = process.argv.includes('--watch')
  const controller = new AbortController()
  process.on('SIGINT', () => controller.abort())
  process.on('SIGTERM', () => controller.abort())
  const runner = watch
    ? watchVariableTrialPortalTasks({ signal: controller.signal, onProgress: (line) => console.log(line) })
    : runNextVariableTrialPortalTask({ signal: controller.signal }).then((res) => console.log(JSON.stringify(res)))
  runner.catch((error) => {
    console.error(error)
    process.exitCode = 1
  })
}
