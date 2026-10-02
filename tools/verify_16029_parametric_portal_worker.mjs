import assert from 'node:assert/strict'
import { mkdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { run16029ParametricPortalTask, runNext16029ParametricPortalTask, watch16029ParametricPortalTasks } from './run_16029_parametric_portal_worker.mjs'

const dataDir = resolve('tmp', 'parametric-worker-' + Date.now())
mkdirSync(dataDir, { recursive: true })
const store = createNativeTaskStore({ dataDir })
const parametricRequest = JSON.parse(readFileSync('data/parametric_913_four_request.json', 'utf8'))
const candidate = resolve('data/parametric_attempts/PARAM-20260906115210111-B86FBD')
const make = async (id, parametric = true) => store.createOrImport({
  id, username: 'worker-test', createdAt: new Date().toISOString(), status: 'native_task_created',
  taskType: 'native_solidworks_build_task', requestFingerprint: id,
  ...(parametric ? { parametricRequest } : {}), nativeBuild: { state: 'created', attempt: 0, modelReady: false },
})
let calls = 0
const runModel = async request => { calls++; assert.deepEqual(request, parametricRequest); return { attempt: candidate, state: 'candidate_requires_validation' } }
await make('PARAMETRIC-READY')
const ready = await run16029ParametricPortalTask('PARAMETRIC-READY', { dataDir, runModel })
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
await make('LEGACY', false)
assert.equal((await run16029ParametricPortalTask('LEGACY', { dataDir, runModel })).state, 'refused')
await make('PARAMETRIC-FOREIGN')
const foreign = await store.claimTask({ taskId: 'PARAMETRIC-FOREIGN', workerId: 'another-worker' })
assert.equal((await run16029ParametricPortalTask('PARAMETRIC-FOREIGN', { dataDir, runModel })).state, 'not_claimed')
assert.equal((await store.read('PARAMETRIC-FOREIGN')).revision, foreign.revision)
assert.equal(calls, 2)
assert.equal((await runNext16029ParametricPortalTask({ dataDir, runModel })).state, 'idle')
await make('PARAMETRIC-BUSY')
const beforeBusy = await store.read('PARAMETRIC-BUSY')
assert.equal((await runNext16029ParametricPortalTask({ dataDir, runModel, cadAvailable: () => false })).state, 'waiting_for_cad')
assert.equal((await store.read('PARAMETRIC-BUSY')).revision, beforeBusy.revision)
const stop = new AbortController(), watched = []
await watch16029ParametricPortalTasks({ dataDir, runModel: async input => {
  await assert.rejects(watch16029ParametricPortalTasks({ dataDir, runModel, signal: AbortSignal.abort() }), /native worker instance is already active/)
  return runModel(input)
}, cadAvailable: () => true, signal: stop.signal,
  onProgress: message => { watched.push(JSON.parse(message)); stop.abort() } })
assert.equal(watched[0].state, 'ready')
assert.equal((await store.read('PARAMETRIC-BUSY')).nativeBuild.modelReady, true)
console.log('parametric portal worker: verified candidate becomes ready; rejected evidence, cancellation, legacy and foreign leases rejected; busy CAD preserves queue; exclusive daemon processes and stops; CAD was not started')
