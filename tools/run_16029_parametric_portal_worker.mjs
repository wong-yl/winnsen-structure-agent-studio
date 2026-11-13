import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'
import { existsSync, readFileSync } from 'node:fs'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { run16029ParametricRuntime } from './lib/locker_16029_parametric_runtime.mjs'
import { validate16029ParametricPublication, validate16029ProcessReviewPublication } from './lib/locker_16029_parametric_publication.mjs'
import { is16029ProcessReview } from './lib/locker_16029_production_profile.mjs'
import { acquireNativeWorkerInstanceLock } from './run_16029_native_worker.mjs'
import { inspect16029CadProcesses } from './lib/locker_16029_parametric_process.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const hasResumeOptions = options => ['resumeSource', 'resumeStage', 'resumeBackupPhase'].some(key => Boolean(options[key]))
const refuseQueuedResume = options => {
  if (hasResumeOptions(options)) throw new Error('parametric checkpoint resume requires an explicit taskId and cannot run through the queue or watcher')
}
export async function run16029ParametricPortalTask(taskId, {
  dataDir = resolve(ROOT, 'data/native_model_requests'),
  runModel = run16029ParametricRuntime, validate = (attempt, options) => is16029ProcessReview(options.expectedRequest) ? validate16029ProcessReviewPublication(attempt, options) : validate16029ParametricPublication(attempt, options),
  heartbeatMs = 10000, phaseTimeoutMs, buildTag, repairProfile = '', signal, onProgress = console.log,
  resumeSource = '', resumeStage = '', resumeBackupPhase = '',
  reuseCandidate,
} = {}) {
  if (hasResumeOptions({ resumeSource, resumeStage, resumeBackupPhase }) &&
      (typeof resumeSource !== 'string' || !resumeSource || typeof resumeStage !== 'string' || !resumeStage ||
       typeof resumeBackupPhase !== 'string' || reuseCandidate !== undefined)) {
    throw new Error('parametric portal resume requires an explicit source and completed stage, without candidate reuse')
  }
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
    if (signal?.aborted) {
      cancelled = true
      if (['claimed', 'planning', 'building', 'validating'].includes(task.nativeBuild.state)) {
        task = await store.cancel({ taskId, expectedRevision: task.revision,
          transitionId: taskId + '-parametric-cancel-signal-' + leaseId,
          reason: 'Portal execution signal cancelled' })
      }
    }
    if (task.nativeBuild.state === 'cancel_requested') {
      cancelled = true
      if (inner) {
        const childStore = createNativeTaskStore({ dataDir: resolve(inner.attempt, 'task_store') })
        const child = await childStore.read(inner.taskId)
        if (!['cancelled', 'cancel_requested', 'ready', 'failed', 'blocked'].includes(child.nativeBuild.state)) await childStore.cancel({ taskId: inner.taskId, expectedRevision: child.revision, transitionId: taskId + '-cancel-child', reason: 'Portal task cancelled' })
      }
    }
    if (!['cancelled', 'failed', 'blocked', 'ready'].includes(task.nativeBuild.state)) task = await store.heartbeat(args())
  }
  const queueControl = () => { serial = serial.then(control).catch(error => { controlError = error }); return serial }
  const onAbort = () => { cancelled = true; queueControl() }
  let timer
  try {
    signal?.addEventListener('abort', onAbort, { once: true })
    if (signal?.aborted) throw new Error('Portal execution signal cancelled before generation')
    task = await store.transition({ ...args(), transitionId: taskId + '-parametric-planning-' + leaseId, toState: 'planning' })
    task = await store.transition({ ...args(), transitionId: taskId + '-parametric-building-' + leaseId, toState: 'building' })
    timer = setInterval(queueControl, heartbeatMs)
    const generated = reuseCandidate !== undefined ? { attempt: reuseCandidate?.attempt } : await runModel(task.parametricRequest, { buildTag, repairProfile, resumeSource, resumeStage, resumeBackupPhase, phaseTimeoutMs, signal, beforeResume: async () => {
      await queueControl()
      if (controlError) throw controlError
      if (signal?.aborted || cancelled || task.nativeBuild.state === 'cancel_requested') throw new Error('Portal task cancelled before checkpoint resume')
      if (task.nativeBuild.state !== 'building') throw new Error('Portal task is no longer building at checkpoint resume')
    }, onProgress: message => {
      try { const row = JSON.parse(message); if (row.taskId && row.attempt) inner = { taskId: row.taskId, attempt: row.attempt } } catch {}
      onProgress(message)
    } })
    clearInterval(timer); timer = null; await queueControl()
    if (signal?.aborted) cancelled = true
    if (controlError) throw controlError
    if (cancelled || generated.state === 'cancelled_after_atomic_stage') {
      task = await store.transition({ ...args(), transitionId: taskId + '-parametric-cancelled-' + leaseId, toState: 'cancelled' })
      return { state: 'cancelled', modelReady: false }
    }
    task = await store.transition({ ...args(), transitionId: taskId + '-parametric-validating-' + leaseId, toState: 'validating' })
    if (reuseCandidate !== undefined) {
      task = await store.completeParametricCandidate({ ...args(), signal, transitionId: taskId + '-parametric-ready-' + leaseId,
        candidateAttempt: reuseCandidate?.attempt, expectedArchiveSha256: reuseCandidate?.archiveSha256 })
      return { state: task.nativeBuild.state, taskId, attempt: task.nativeBuild.result.attempt, modelReady: true,
        executionMode: task.nativeBuild.result.executionMode, cadStarted: false }
    }
    const checked = validate(generated.attempt, { expectedRequest: task.parametricRequest })
    if (!checked.ok) throw new Error('Parametric publication rejected: ' + checked.reason)
    if (signal?.aborted || cancelled) {
      cancelled = true
      await queueControl()
      if (controlError) throw controlError
      task = await store.transition({ ...args(), transitionId: taskId + '-parametric-cancelled-after-validation-' + leaseId, toState: 'cancelled' })
      return { state: 'cancelled', modelReady: false }
    }
    task = await store.complete({ ...args(), signal, transitionId: taskId + '-parametric-ready-' + leaseId,
      result: { schema: 'winnsen.locker16029.parametric_publication.v1', ...checked } })
    return { state: task.nativeBuild.state, taskId, attempt: checked.attempt, modelReady: true }
  } catch (error) {
    if (timer) clearInterval(timer)
    await queueControl()
    const latest = await store.read(taskId)
    if (latest.nativeBuild.lease?.id === leaseId && latest.nativeBuild.lease?.workerId === workerId) {
      task = latest
      if (error.cleanupVerified === false) {
        task = await store.transition({ ...args(), transitionId: taskId + '-parametric-blocked-cleanup-' + leaseId, toState: 'blocked',
          patch: { blocker: { code: 'OWNED_PROCESS_CLEANUP_UNVERIFIED', message: error.message } } })
      } else if (task.nativeBuild.state === 'cancel_requested' || cancelled || signal?.aborted) {
        if (task.nativeBuild.state !== 'cancel_requested' && !['cancelled', 'failed', 'blocked', 'ready'].includes(task.nativeBuild.state)) {
          task = await store.cancel({ taskId, expectedRevision: task.revision,
            transitionId: taskId + '-parametric-cancel-signal-' + leaseId,
            reason: 'Portal execution signal cancelled' })
        }
        if (task.nativeBuild.state === 'cancel_requested') task = await store.transition({ ...args(), transitionId: taskId + '-parametric-cancelled-' + leaseId, toState: 'cancelled' })
      }
      else task = await store.fail({ ...args(), transitionId: taskId + '-parametric-failed-' + leaseId, code: 'parametric_generation_failed', message: error.message, retryable: false })
    }
    return { state: task.nativeBuild.state, taskId, modelReady: false, error: error.message }
  } finally {
    if (timer) clearInterval(timer)
    signal?.removeEventListener('abort', onAbort)
  }
}

export async function runNext16029ParametricPortalTask(options = {}) {
  refuseQueuedResume(options)
  const dataDir = options.dataDir || resolve(ROOT, 'data/native_model_requests')
  const store = createNativeTaskStore({ dataDir })
  // Recover dead/expired leases before looking at queue candidates; an orphaned task must not make the queue appear idle.
  await store.recoverExpiredLeases({ now: options.now })
  const tasks = await store.listSnapshot()
  const candidate = tasks.filter(task => task.parametricRequest && task.nativeBuild?.state === 'created')
    .sort((a,b) => String(a.createdAt).localeCompare(String(b.createdAt)) || a.id.localeCompare(b.id))[0]
  if (!candidate) return { state: 'idle' }
  if (options.reuseCandidate === undefined && !(options.cadAvailable || nativeCadAvailable)()) return { state: 'waiting_for_cad', taskId: candidate.id }
  return run16029ParametricPortalTask(candidate.id, { ...options, dataDir })
}

function nativeCadAvailable() {
  const ownerPath = resolve(ROOT, 'data/native_model_requests/.native-worker-instance.lock/owner.json')
  if (existsSync(ownerPath)) {
    try { const owner = JSON.parse(readFileSync(ownerPath, 'utf8')); process.kill(owner.pid, 0); return false } catch (error) { if (error.code !== 'ESRCH') return false }
  }
  return inspect16029CadProcesses().verified
}

export async function watch16029ParametricPortalTasks({ signal, pollMs = 5000, onProgress = console.log, ...options } = {}) {
  refuseQueuedResume(options)
  const dataDir = options.dataDir || resolve(ROOT, 'data/native_model_requests')
  const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(dataDir, 'parametric_portal_daemon'), workerId: `parametric-watch-${process.pid}` })
  const heartbeat = setInterval(() => lock.heartbeat(), 20000)
  let lastState = ''
  try {
    while (!signal?.aborted) {
      const result = await runNext16029ParametricPortalTask({ ...options, dataDir, signal, onProgress })
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
  if (!process.argv[2]) throw new Error('Usage: node tools/run_16029_parametric_portal_worker.mjs <taskId>|--once|--watch [--build-tag tag] [--repair-profile profile] [--resume-source path --resume-stage phase] [--resume-backup-phase phase]')
  const options = {}
  const optionNames = { '--build-tag': 'buildTag', '--repair-profile': 'repairProfile', '--resume-source': 'resumeSource',
    '--resume-stage': 'resumeStage', '--resume-backup-phase': 'resumeBackupPhase', '--phase-timeout-ms': 'phaseTimeoutMs' }
  for (let index = 3; index < process.argv.length; index += 2) {
    const key = optionNames[process.argv[index]], value = process.argv[index + 1]
    if (!key || !value || value.startsWith('--') || options[key] !== undefined) throw new Error('invalid parametric portal worker option: ' + process.argv[index])
    options[key] = key === 'phaseTimeoutMs' ? Number(value) : value
  }
  refuseQueuedResume(['--once', '--watch'].includes(process.argv[2]) ? options : {})
  if (process.argv[2] === '--watch') {
    const stop = new AbortController()
    process.once('SIGINT', () => stop.abort()); process.once('SIGTERM', () => stop.abort())
    await watch16029ParametricPortalTasks({ ...options, signal: stop.signal })
  } else {
    const result = process.argv[2] === '--once' ? await runNext16029ParametricPortalTask(options) : await run16029ParametricPortalTask(process.argv[2], options)
    console.log(JSON.stringify(result))
    if (result.error) process.exitCode = 1
  }
}
