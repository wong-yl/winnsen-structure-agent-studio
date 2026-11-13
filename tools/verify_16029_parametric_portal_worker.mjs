import assert from 'node:assert/strict'
import { mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { run16029ParametricPortalTask, runNext16029ParametricPortalTask, watch16029ParametricPortalTasks } from './run_16029_parametric_portal_worker.mjs'

const dataDir = resolve('tmp', 'parametric-worker-' + Date.now())
mkdirSync(dataDir, { recursive: true })
const store = createNativeTaskStore({ dataDir })
const parametricRequest = { cabinet: { widthMm: 850, heightMm: 1917, depthMm: 550 }, columns: ['L', 'R'].map(side => ({ side, doors: [{ heightUnits: 1 }, { heightUnits: 2 }] })) }
const candidate = resolve(dataDir, 'isolated-candidate')
const validate = attempt => ({ ok: true, modelReady: true, attempt, archiveSha256: 'A'.repeat(64), rootPath: attempt + '/root.SLDASM', archivePath: attempt + '/model.zip', cadFileCount: 1, coverageVerified: true, quality: { pass: true } })
const make = async (id, parametric = true) => store.createOrImport({
  id, username: 'worker-test', createdAt: new Date().toISOString(), status: 'native_task_created',
  taskType: 'native_solidworks_build_task', requestFingerprint: id,
  ...(parametric ? { parametricRequest } : {}), nativeBuild: { state: 'created', attempt: 0, modelReady: false },
})
let calls = 0
const runModel = async request => { calls++; assert.deepEqual(request, parametricRequest); return { attempt: candidate, state: 'candidate_requires_validation' } }
await make('PARAMETRIC-READY')
const ready = await run16029ParametricPortalTask('PARAMETRIC-READY', { dataDir, runModel, validate })
assert.equal(ready.state, 'ready', ready.error)
assert.equal((await store.read('PARAMETRIC-READY')).nativeBuild.result.schema, 'winnsen.locker16029.parametric_publication.v1')
await make('PARAMETRIC-FAILED')
const failed = await run16029ParametricPortalTask('PARAMETRIC-FAILED', { dataDir, runModel, validate: () => ({ ok: false, reason: 'physical acceptance failed' }) })
assert.equal(failed.state, 'failed')
assert.equal(failed.modelReady, false)
await make('PARAMETRIC-CANCELLED')
const cancelled = await run16029ParametricPortalTask('PARAMETRIC-CANCELLED', { dataDir, runModel: async () => {
  const task = await store.read('PARAMETRIC-CANCELLED')
  await store.cancel({ taskId: task.id, expectedRevision: task.revision, transitionId: 'cancel-from-user', reason: 'test' })
  return { attempt: candidate }
} })
assert.equal(cancelled.state, 'cancelled', cancelled.error)
await make('PARAMETRIC-SIGNAL-CANCELLED')
const beforeReturnAbort = new AbortController()
const signalCancelled = await run16029ParametricPortalTask('PARAMETRIC-SIGNAL-CANCELLED', {
  dataDir, signal: beforeReturnAbort.signal,
  runModel: async () => { beforeReturnAbort.abort(); return { attempt: candidate, state: 'candidate_requires_validation' } },
  validate,
})
assert.equal(signalCancelled.state, 'cancelled', signalCancelled.error)
assert.equal((await store.read('PARAMETRIC-SIGNAL-CANCELLED')).nativeBuild.state, 'cancelled')
await make('PARAMETRIC-SIGNAL-AFTER-RETURN')
const afterReturnAbort = new AbortController()
const afterReturnCancelled = await run16029ParametricPortalTask('PARAMETRIC-SIGNAL-AFTER-RETURN', {
  dataDir, signal: afterReturnAbort.signal,
  runModel: async () => ({ attempt: candidate, state: 'candidate_requires_validation' }),
  validate: attempt => { afterReturnAbort.abort(); return validate(attempt) },
})
assert.equal(afterReturnCancelled.state, 'cancelled', afterReturnCancelled.error)
assert.equal((await store.read('PARAMETRIC-SIGNAL-AFTER-RETURN')).nativeBuild.state, 'cancelled')
await make('PARAMETRIC-SIGNAL-COMMIT-WAIT')
const commitWaitAbort = new AbortController()
const commitWaitCancelled = await run16029ParametricPortalTask('PARAMETRIC-SIGNAL-COMMIT-WAIT', {
  dataDir, signal: commitWaitAbort.signal,
  runModel: async () => ({ attempt: candidate, state: 'candidate_requires_validation' }),
  validate: attempt => {
    writeFileSync(store.lockPath, JSON.stringify({ token: 'test-portal-completion-blocker', pid: process.pid }), 'utf8')
    setTimeout(() => { commitWaitAbort.abort(); rmSync(store.lockPath, { force: true }) }, 30)
    return validate(attempt)
  },
})
assert.equal(commitWaitCancelled.state, 'cancelled', commitWaitCancelled.error)
const commitWaitTask = await store.read('PARAMETRIC-SIGNAL-COMMIT-WAIT')
assert.equal(commitWaitTask.nativeBuild.state, 'cancelled')
assert.equal(commitWaitTask.nativeBuild.result, undefined)
assert.equal(commitWaitTask.nativeBuild.statusHistory.some(entry => entry.to === 'ready'), false)
await make('PARAMETRIC-SIGNAL-BEFORE-EXECUTION')
const beforeExecutionCancelled = await run16029ParametricPortalTask('PARAMETRIC-SIGNAL-BEFORE-EXECUTION', {
  dataDir, signal: AbortSignal.abort(),
  runModel: async () => { assert.fail('an already cancelled task must not start model generation') },
  validate,
})
assert.equal(beforeExecutionCancelled.state, 'cancelled', beforeExecutionCancelled.error)
await make('PARAMETRIC-SIGNAL-REJECTED')
const rejectedAbort = new AbortController()
const rejectedCancelled = await run16029ParametricPortalTask('PARAMETRIC-SIGNAL-REJECTED', {
  dataDir, signal: rejectedAbort.signal,
  runModel: async () => { rejectedAbort.abort(); throw Object.assign(new Error('phase cancelled'), { code: 'PARAMETRIC_PHASE_CANCELLED', cleanupVerified: true }) },
  validate,
})
assert.equal(rejectedCancelled.state, 'cancelled', rejectedCancelled.error)
await make('PARAMETRIC-TIMEOUT')
const timeout = await run16029ParametricPortalTask('PARAMETRIC-TIMEOUT', {
  dataDir,
  runModel: async () => { throw Object.assign(new Error('phase timeout'), { code: 'PARAMETRIC_PHASE_TIMEOUT', cleanupVerified: true }) },
  validate,
})
assert.equal(timeout.state, 'failed', timeout.error)
await make('PARAMETRIC-CLEANUP-UNVERIFIED')
const cleanupAbort = new AbortController()
const cleanupBlocked = await run16029ParametricPortalTask('PARAMETRIC-CLEANUP-UNVERIFIED', {
  dataDir, signal: cleanupAbort.signal,
  runModel: async () => { cleanupAbort.abort(); throw Object.assign(new Error('owned process cleanup could not be verified'), { cleanupVerified: false }) },
  validate,
})
assert.equal(cleanupBlocked.state, 'blocked', cleanupBlocked.error)
assert.equal((await store.read('PARAMETRIC-CLEANUP-UNVERIFIED')).nativeBuild.blocker.code, 'OWNED_PROCESS_CLEANUP_UNVERIFIED')
await make('LEGACY', false)
assert.equal((await run16029ParametricPortalTask('LEGACY', { dataDir, runModel })).state, 'refused')
await make('PARAMETRIC-FOREIGN')
const foreign = await store.claimTask({ taskId: 'PARAMETRIC-FOREIGN', workerId: 'another-worker' })
assert.equal((await run16029ParametricPortalTask('PARAMETRIC-FOREIGN', { dataDir, runModel })).state, 'not_claimed')
assert.equal((await store.read('PARAMETRIC-FOREIGN')).revision, foreign.revision)
assert.equal(calls, 2)
assert.equal((await runNext16029ParametricPortalTask({ dataDir, runModel, validate })).state, 'idle')
await make('PARAMETRIC-EXPLICIT-RESUME')
const resumeOptions = { buildTag: 'readonly-open-20261105-r1', repairProfile: 'readonly-open-20261105-r1',
  resumeSource: resolve(dataDir, 'checkpoint-attempt'), resumeStage: 'repair-frame-slots', resumeBackupPhase: 'repair-height-braces',
  phaseTimeoutMs: 120000 }
let resumeCalls = 0
const resumed = await run16029ParametricPortalTask('PARAMETRIC-EXPLICIT-RESUME', { dataDir, ...resumeOptions, validate,
  runModel: async (request, options) => {
    resumeCalls++
    assert.deepEqual(request, parametricRequest)
    for (const [key, value] of Object.entries(resumeOptions)) assert.equal(options[key], value)
    return { attempt: candidate, state: 'candidate_requires_validation' }
  },
})
assert.equal(resumed.state, 'ready', resumed.error)
assert.equal(resumeCalls, 1)
await make('PARAMETRIC-RESUME-GUARDS')
const beforeResumeGuards = await store.read('PARAMETRIC-RESUME-GUARDS')
for (const invalidOptions of [{ resumeSource: resumeOptions.resumeSource }, { resumeStage: resumeOptions.resumeStage },
  { resumeBackupPhase: resumeOptions.resumeBackupPhase }, { ...resumeOptions, reuseCandidate: { attempt: candidate } }]) {
  await assert.rejects(run16029ParametricPortalTask('PARAMETRIC-RESUME-GUARDS', { dataDir, ...invalidOptions, runModel,
    validate }), /requires an explicit source and completed stage/)
  assert.deepEqual(await store.read('PARAMETRIC-RESUME-GUARDS'), beforeResumeGuards)
}
for (const queuedOptions of [{ resumeSource: resumeOptions.resumeSource }, { resumeStage: resumeOptions.resumeStage },
  { resumeBackupPhase: resumeOptions.resumeBackupPhase }, resumeOptions]) {
  await assert.rejects(runNext16029ParametricPortalTask({ dataDir, ...queuedOptions, runModel, validate,
    cadAvailable: () => assert.fail('queue resume must be refused before checking CAD') }), /requires an explicit taskId/)
  await assert.rejects(watch16029ParametricPortalTasks({ dataDir, ...queuedOptions, runModel, validate,
    signal: AbortSignal.abort() }), /requires an explicit taskId/)
  assert.deepEqual(await store.read('PARAMETRIC-RESUME-GUARDS'), beforeResumeGuards)
}
const guardedResume = await store.claimTask({ taskId: 'PARAMETRIC-RESUME-GUARDS', workerId: 'resume-guard-test' })
await store.fail({ taskId: guardedResume.id, workerId: 'resume-guard-test', leaseId: guardedResume.nativeBuild.lease.id,
  expectedRevision: guardedResume.revision, transitionId: 'resume-guards-finished', code: 'test_finished',
  message: 'isolated resume guard checks finished', retryable: false })
let reuseModelCalls = 0, reuseValidationCalls = 0, reuseCadChecks = 0
const reuseOptions = { dataDir, reuseCandidate: { attempt: candidate, archiveSha256: 'A'.repeat(64) },
  runModel: async () => { reuseModelCalls++; assert.fail('reuse failure cannot fall back to generation') },
  validate: () => { reuseValidationCalls++; return validate(candidate) },
  cadAvailable: () => { reuseCadChecks++; return false } }
await make('PARAMETRIC-REUSE-BUSY')
const refusedReuse = await runNext16029ParametricPortalTask(reuseOptions)
assert.equal(refusedReuse.state, 'failed', refusedReuse.error)
assert.match(refusedReuse.error, /publication path outside its bound directory/)
assert.equal((await store.read('PARAMETRIC-REUSE-BUSY')).nativeBuild.result, undefined)
await make('PARAMETRIC-REUSE-MISSING-HASH')
assert.equal((await run16029ParametricPortalTask('PARAMETRIC-REUSE-MISSING-HASH', { ...reuseOptions, reuseCandidate: { attempt: candidate } })).state, 'failed')
await make('PARAMETRIC-REUSE-ABORTED')
assert.equal((await run16029ParametricPortalTask('PARAMETRIC-REUSE-ABORTED', { ...reuseOptions, signal: AbortSignal.abort() })).state, 'cancelled')
assert.equal(reuseModelCalls, 0); assert.equal(reuseValidationCalls, 0); assert.equal(reuseCadChecks, 0)
await make('PARAMETRIC-BUSY')
const beforeBusy = await store.read('PARAMETRIC-BUSY')
assert.equal((await runNext16029ParametricPortalTask({ dataDir, runModel, cadAvailable: () => false })).state, 'waiting_for_cad')
assert.equal((await store.read('PARAMETRIC-BUSY')).revision, beforeBusy.revision)
const stop = new AbortController(), watched = []
await watch16029ParametricPortalTasks({ dataDir, runModel: async (input, options) => {
  assert.equal(options.signal, stop.signal)
  await assert.rejects(watch16029ParametricPortalTasks({ dataDir, runModel, validate, signal: AbortSignal.abort() }), /native worker instance is already active/)
  return runModel(input)
}, validate, cadAvailable: () => true, signal: stop.signal,
  onProgress: message => { watched.push(JSON.parse(message)); stop.abort() } })
assert.equal(watched[0].state, 'ready')
assert.equal((await store.read('PARAMETRIC-BUSY')).nativeBuild.modelReady, true)
console.log('parametric portal worker: ordinary publication and cancellation/timeout/cleanup guards verified; explicit task resume forwards build/profile/checkpoint options, rejects partial or reused checkpoints before claiming, and refuses all queue/watcher resumes before queue/CAD actions; candidate reuse, legacy/foreign leases and exclusive daemon behavior retained; CAD was not started')
