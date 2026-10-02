import assert from 'node:assert/strict'
import { randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  utimesSync,
  writeFileSync,
} from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_BUILD_SCHEMA,
  NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
  NATIVE_TASK_INDEX_SCHEMA,
  NATIVE_TASK_STORE_SCHEMA,
  createNativeTaskStore,
  nativeCheckpointDescriptorDigest,
} from './lib/locker_16029_native_task_store.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const SELF = fileURLToPath(import.meta.url)
const TEMP_ROOT = resolve(ROOT, `tmp/verify_16029_native_task_store-${process.pid}-${randomBytes(4).toString('hex')}`)

function pendingTask(id, overrides = {}) {
  return {
    schema: 'winnsen.native_16029_request.v1',
    id,
    username: 'engineer-a',
    createdAt: '2026-08-12T01:00:00.000Z',
    requestFingerprint: `fingerprint-${id}`,
    status: 'native_task_created',
    taskType: 'native_solidworks_build_task',
    deliveryMode: 'native_task_pending',
    storageMode: 'task_file_source',
    modelReady: false,
    legacyFallbackUsed: false,
    cabinetWidth: 888,
    cabinetHeight: 1917,
    cabinetDepth: 550,
    columns: 2,
    doorCount: 14,
    doorWidth: 381,
    columnDoorCounts: [7, 7],
    rowSequence: 'L1111111-R1111111',
    validationRequired: ['one_door_one_lock_opening'],
    nativeBuild: {
      schema: 'winnsen.native_solidworks_build_task.v1',
      state: 'created',
      attempt: 0,
      executionStarted: false,
      modelReady: false,
      legacyFallbackUsed: false,
      requiredCad: 'SolidWorks 2020 native',
      requiredChecks: ['one_door_one_lock_opening'],
      statusHistory: [{ state: 'created', at: '2026-08-12T01:00:00.000Z' }],
    },
    ...overrides,
  }
}

function waitForFile(path, timeoutMs = 5_000) {
  const startedAt = Date.now()
  return new Promise((resolveWait, reject) => {
    const poll = () => {
      if (existsSync(path)) return resolveWait()
      if (Date.now() - startedAt >= timeoutMs) return reject(new Error(`barrier timeout: ${path}`))
      setTimeout(poll, 10)
    }
    poll()
  })
}

async function childMode() {
  const [mode, dataDir, barrierPath, ...args] = process.argv.slice(2)
  if (!String(mode || '').startsWith('--child-')) return false
  try {
    await waitForFile(barrierPath)
    const store = createNativeTaskStore({ dataDir, lockTimeoutMs: 5_000, lockRetryMs: 5 })
    if (mode === '--child-claim') {
      const task = await store.claimNext({ workerId: args[0], leaseMs: 20_000 })
      process.stdout.write(`${JSON.stringify({ ok: true, task })}\n`)
    } else if (mode === '--child-cancel') {
      const task = await store.cancel({
        taskId: args[0],
        expectedRevision: Number(args[1]),
        transitionId: args[2],
        username: args[3],
      })
      process.stdout.write(`${JSON.stringify({ ok: true, task })}\n`)
    } else if (mode === '--child-dedup') {
      const input = JSON.parse(readFileSync(args[0], 'utf8'))
      const result = await store.createOrReuseByFingerprint(input, { username: args[1] })
      process.stdout.write(`${JSON.stringify({ ok: true, result })}\n`)
    } else {
      throw new Error(`unknown child mode: ${mode}`)
    }
  } catch (error) {
    process.stdout.write(`${JSON.stringify({
      ok: false,
      code: error?.code || 'ERROR',
      message: error instanceof Error ? error.message : String(error),
    })}\n`)
  }
  return true
}

if (await childMode()) process.exit(0)

function spawnJson(args) {
  return new Promise((resolveChild, reject) => {
    const child = spawn(process.execPath, [SELF, ...args], {
      cwd: ROOT,
      windowsHide: true,
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    let stdout = ''
    let stderr = ''
    child.stdout.on('data', (chunk) => { stdout += chunk })
    child.stderr.on('data', (chunk) => { stderr += chunk })
    child.once('error', reject)
    child.once('close', (code) => {
      if (code !== 0) return reject(new Error(`child exited ${code}: ${stderr || stdout}`))
      try {
        const lines = stdout.trim().split(/\r?\n/).filter(Boolean)
        resolveChild(JSON.parse(lines.at(-1)))
      } catch (error) {
        reject(new Error(`invalid child output: ${stdout || stderr}; ${error.message}`))
      }
    })
  })
}

async function releaseBarrier(path) {
  await new Promise((resolveDelay) => setTimeout(resolveDelay, 75))
  writeFileSync(path, 'go\n', 'utf8')
}

async function rejectsCode(action, code) {
  await assert.rejects(action, (error) => {
    assert.equal(error?.code, code)
    return true
  })
}

const checks = []
async function check(name, action) {
  try {
    await action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error instanceof Error ? error.stack || error.message : String(error) })
  }
}

function caseDir(name) {
  const path = resolve(TEMP_ROOT, name)
  mkdirSync(path, { recursive: true })
  return path
}

await check('create_imports_existing_v1_detail_and_detail_is_source_of_truth', async () => {
  const dataDir = caseDir('create-import')
  const store = createNativeTaskStore({ dataDir })
  const created = await store.createOrImport(pendingTask('NATIVE-CREATE-1'))
  assert.equal(created.revision, 0)
  assert.equal(created.taskStoreSchema, NATIVE_TASK_STORE_SCHEMA)
  assert.equal(created.nativeBuild.schema, NATIVE_BUILD_SCHEMA)
  assert.equal(created.nativeBuild.state, 'created')
  const repeated = await store.createOrImport(pendingTask('NATIVE-CREATE-1'))
  assert.equal(repeated.revision, 0)
  await rejectsCode(() => store.createOrImport(pendingTask('NATIVE-CREATE-1', {
    cabinetWidth: 999,
    doorCount: 12,
    columnDoorCounts: [6, 6],
    rowSequence: 'L111111-R111111',
  })), 'TASK_ID_COLLISION')

  const indexPath = resolve(dataDir, 'native_model_request_index.json')
  const poisoned = JSON.parse(readFileSync(indexPath, 'utf8'))
  poisoned.requests.push(pendingTask('NATIVE-INDEX-ONLY'))
  writeFileSync(indexPath, JSON.stringify(poisoned), 'utf8')
  const listed = await store.list()
  assert.deepEqual(listed.map((item) => item.id), ['NATIVE-CREATE-1'])
  const repaired = JSON.parse(readFileSync(indexPath, 'utf8'))
  assert.equal(repaired.schema, NATIVE_TASK_INDEX_SCHEMA)
  assert.deepEqual(repaired.requests.map((item) => item.id), ['NATIVE-CREATE-1'])
})

await check('parametric_door_sequence_is_part_of_immutable_task_identity', async () => {
  const store = createNativeTaskStore({ dataDir: caseDir('parametric-identity') })
  const parametricRequest = { cabinet: { widthMm: 839, heightMm: 1917, depthMm: 550 }, columns: [
    { side: 'L', doors: [{ heightUnits: 6 }, { heightUnits: 4 }, { heightUnits: 2 }] },
    { side: 'R', doors: [{ heightUnits: 2 }, { heightUnits: 4 }, { heightUnits: 6 }] },
  ] }
  await store.createOrImport(pendingTask('NATIVE-PARAMETRIC-IDENTITY', { parametricRequest }))
  const changed = structuredClone(parametricRequest)
  changed.columns[0].doors.reverse()
  await rejectsCode(() => store.createOrImport(pendingTask('NATIVE-PARAMETRIC-IDENTITY', { parametricRequest: changed })), 'TASK_ID_COLLISION')
})

await check('parametric_exact_claim_does_not_take_legacy_or_foreign_tasks', async () => {
  const store = createNativeTaskStore({ dataDir: caseDir('parametric-exact-claim') })
  await store.createOrImport(pendingTask('NATIVE-LEGACY'))
  await store.createOrImport(pendingTask('NATIVE-PARAMETRIC', { parametricRequest: { cabinet: { widthMm: 839 }, columns: [] } }))
  await rejectsCode(() => store.claimTask({ taskId: 'NATIVE-LEGACY', workerId: 'parametric-worker' }), 'PARAMETRIC_REQUEST_REQUIRED')
  const exact = await store.claimTask({ taskId: 'NATIVE-PARAMETRIC', workerId: 'parametric-worker' })
  assert.equal(exact.id, 'NATIVE-PARAMETRIC')
  const revision = exact.revision
  assert.equal(await store.claimTask({ taskId: 'NATIVE-PARAMETRIC', workerId: 'other-worker' }), null)
  assert.equal((await store.read('NATIVE-PARAMETRIC')).revision, revision)
  assert.equal((await store.claimNext({ workerId: 'legacy-worker' })).id, 'NATIVE-LEGACY')
  assert.equal(await store.claimNext({ workerId: 'legacy-worker' }), null)
})

await check('reserved_index_and_windows_device_task_ids_are_rejected', async () => {
  const dataDir = caseDir('reserved-task-ids')
  const store = createNativeTaskStore({ dataDir })
  for (const taskId of ['native_model_request_index', 'NATIVE_MODEL_REQUEST_INDEX', 'CON', 'nul.anything']) {
    await rejectsCode(() => store.createOrImport(pendingTask(taskId)), 'INVALID_TASK_ID')
  }
  assert.deepEqual(await store.listSnapshot(), [])
  const indexPath = resolve(dataDir, 'native_model_request_index.json')
  assert.equal(existsSync(indexPath), false)
})

await check('fingerprint_dedup_is_atomic_across_processes', async () => {
  const dataDir = caseDir('fingerprint-dedup')
  const inputDir = caseDir('fingerprint-inputs')
  const firstPath = resolve(inputDir, 'first.json')
  const secondPath = resolve(inputDir, 'second.json')
  writeFileSync(firstPath, JSON.stringify(pendingTask('NATIVE-DEDUP-A')), 'utf8')
  writeFileSync(secondPath, JSON.stringify(pendingTask('NATIVE-DEDUP-B', {
    requestFingerprint: 'fingerprint-NATIVE-DEDUP-A',
  })), 'utf8')
  const barrier = resolve(dataDir, 'dedup.barrier')
  const first = spawnJson(['--child-dedup', dataDir, barrier, firstPath, 'engineer-a'])
  const second = spawnJson(['--child-dedup', dataDir, barrier, secondPath, 'engineer-a'])
  await releaseBarrier(barrier)
  const results = await Promise.all([first, second])
  assert.equal(results.every((result) => result.ok), true)
  assert.equal(results.filter((result) => result.result.created).length, 1)
  assert.equal(results.filter((result) => !result.result.created).length, 1)
  assert.equal(results[0].result.task.id, results[1].result.task.id)
  const store = createNativeTaskStore({ dataDir })
  assert.equal((await store.listSnapshot()).length, 1)

  await rejectsCode(() => store.createOrReuseByFingerprint(pendingTask('NATIVE-DEDUP-C', {
    requestFingerprint: 'fingerprint-NATIVE-DEDUP-A',
    doorCount: 12,
    columnDoorCounts: [6, 6],
    rowSequence: 'L111111-R111111',
  }), { username: 'engineer-a' }), 'REQUEST_FINGERPRINT_COLLISION')
  await rejectsCode(() => store.createOrReuseByFingerprint(pendingTask('NATIVE-DEDUP-SPEC-CHANGE', {
    requestFingerprint: 'fingerprint-NATIVE-DEDUP-A',
    material: 'SPCC-B',
    thickness: '1.2',
    lockType: 'mechanical-lock-b',
  }), { username: 'engineer-a' }), 'REQUEST_FINGERPRINT_COLLISION')
  const otherUser = await store.createOrReuseByFingerprint(pendingTask('NATIVE-DEDUP-D', {
    username: 'engineer-b',
    requestFingerprint: 'fingerprint-NATIVE-DEDUP-A',
  }), { username: 'engineer-b' })
  assert.equal(otherUser.created, true)

  const inactive = await store.createOrImport(pendingTask('NATIVE-DEDUP-INACTIVE', {
    requestFingerprint: 'fingerprint-inactive',
    status: 'native_task_failed',
    nativeBuild: {
      ...pendingTask('unused').nativeBuild,
      state: 'failed',
    },
  }))
  assert.equal(inactive.nativeBuild.state, 'failed')
  await rejectsCode(() => store.createOrReuseByFingerprint(pendingTask('NATIVE-DEDUP-INACTIVE', {
    requestFingerprint: 'fingerprint-inactive',
    cabinetWidth: 999,
    doorCount: 12,
    columnDoorCounts: [6, 6],
    rowSequence: 'L111111-R111111',
  }), { username: 'engineer-a' }), 'TASK_ID_COLLISION')
})

await check('list_snapshot_never_writes_or_repairs_index', async () => {
  const dataDir = caseDir('readonly-snapshot')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-SNAPSHOT-1'))
  const indexBefore = readFileSync(store.indexPath, 'utf8')
  const fixedTime = new Date('2026-08-12T07:00:00.000Z')
  utimesSync(store.indexPath, fixedTime, fixedTime)
  const mtimeBefore = statSync(store.indexPath).mtimeMs
  const snapshot = await store.listSnapshot()
  assert.deepEqual(snapshot.map((item) => item.id), ['NATIVE-SNAPSHOT-1'])
  assert.equal(readFileSync(store.indexPath, 'utf8'), indexBefore)
  assert.equal(statSync(store.indexPath).mtimeMs, mtimeBefore)
  writeFileSync(store.indexPath, '{corrupt-but-read-only', 'utf8')
  const corruptMtimeBefore = statSync(store.indexPath).mtimeMs
  assert.deepEqual((await store.listSnapshot()).map((item) => item.id), ['NATIVE-SNAPSHOT-1'])
  assert.equal(readFileSync(store.indexPath, 'utf8'), '{corrupt-but-read-only')
  assert.equal(statSync(store.indexPath).mtimeMs, corruptMtimeBefore)
})

await check('two_processes_claim_one_task_exactly_once', async () => {
  const dataDir = caseDir('concurrent-claim')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-RACE-CLAIM'))
  const barrier = resolve(dataDir, 'claim.barrier')
  const first = spawnJson(['--child-claim', dataDir, barrier, 'worker-a'])
  const second = spawnJson(['--child-claim', dataDir, barrier, 'worker-b'])
  await releaseBarrier(barrier)
  const results = await Promise.all([first, second])
  assert.equal(results.filter((result) => result.ok && result.task).length, 1)
  assert.equal(results.filter((result) => result.ok && result.task === null).length, 1)
  const claimed = await store.read('NATIVE-RACE-CLAIM')
  assert.equal(claimed.nativeBuild.state, 'claimed')
  assert.equal(claimed.nativeBuild.attempt, 1)
  assert.equal(claimed.revision, 1)
})

await check('expired_lease_is_recovered_and_cross_process_reclaimed_once', async () => {
  const dataDir = caseDir('expired-reclaim')
  let nowMs = Date.parse('2026-08-12T03:00:00.000Z')
  const store = createNativeTaskStore({ dataDir, leaseMs: 100, clock: () => nowMs })
  await store.createOrImport(pendingTask('NATIVE-EXPIRED-RECLAIM', { createdAt: new Date(nowMs).toISOString() }))
  const firstClaim = await store.claimNext({
    workerId: 'crashed-worker', workerPid: 99_999_999, leaseMs: 100, now: nowMs,
  })
  assert.equal(firstClaim.nativeBuild.attempt, 1)
  nowMs += 101

  const barrier = resolve(dataDir, 'expired.barrier')
  const first = spawnJson(['--child-claim', dataDir, barrier, 'recovery-worker-a'])
  const second = spawnJson(['--child-claim', dataDir, barrier, 'recovery-worker-b'])
  await releaseBarrier(barrier)
  const results = await Promise.all([first, second])
  assert.equal(results.filter((result) => result.ok && result.task).length, 1)
  assert.equal(results.filter((result) => result.ok && result.task === null).length, 1)
  const recovered = await store.read('NATIVE-EXPIRED-RECLAIM')
  assert.equal(recovered.nativeBuild.state, 'claimed')
  assert.equal(recovered.nativeBuild.attempt, 2)
  assert.equal(recovered.nativeBuild.statusHistory.some((entry) =>
    entry.operation === 'lease_recovery' && entry.from === 'claimed' && entry.to === 'created'
  ), true)
})

await check('expired_cancel_requested_finishes_cancelled_and_is_not_reclaimed', async () => {
  const dataDir = caseDir('expired-cancel')
  let nowMs = Date.parse('2026-08-12T04:00:00.000Z')
  const store = createNativeTaskStore({ dataDir, leaseMs: 100, clock: () => nowMs })
  await store.createOrImport(pendingTask('NATIVE-EXPIRED-CANCEL', { createdAt: new Date(nowMs).toISOString() }))
  const claimed = await store.claimNext({
    workerId: 'worker-cancel-expiry', workerPid: 99_999_999, leaseMs: 100, now: nowMs,
  })
  const requested = await store.cancel({
    taskId: claimed.id,
    username: 'engineer-a',
    expectedRevision: claimed.revision,
    transitionId: 'cancel-expiry-request',
    now: nowMs,
  })
  assert.equal(requested.nativeBuild.state, 'cancel_requested')
  nowMs += 101
  assert.equal(await store.claimNext({ workerId: 'worker-must-not-reclaim', now: nowMs }), null)
  const cancelled = await store.read(claimed.id)
  assert.equal(cancelled.nativeBuild.state, 'cancelled')
  assert.equal(cancelled.nativeBuild.lease, null)
  assert.equal(cancelled.nativeBuild.attempt, 1)
  assert.equal(cancelled.nativeBuild.statusHistory.some((entry) =>
    entry.operation === 'lease_recovery' && entry.from === 'cancel_requested' && entry.to === 'cancelled'
  ), true)
})

await check('expired_lease_stops_after_max_attempts', async () => {
  const dataDir = caseDir('expired-max-attempts')
  let nowMs = Date.parse('2026-08-12T05:00:00.000Z')
  const store = createNativeTaskStore({ dataDir, leaseMs: 100, maxAttempts: 1, clock: () => nowMs })
  await store.createOrImport(pendingTask('NATIVE-EXPIRED-MAX', { createdAt: new Date(nowMs).toISOString() }))
  const claimed = await store.claimNext({
    workerId: 'only-attempt', workerPid: 99_999_999, leaseMs: 100, now: nowMs,
  })
  assert.equal(claimed.nativeBuild.attempt, 1)
  nowMs += 101
  assert.equal(await store.claimNext({ workerId: 'must-not-run', now: nowMs }), null)
  const failed = await store.read(claimed.id)
  assert.equal(failed.nativeBuild.state, 'failed')
  assert.equal(failed.nativeBuild.error.code, 'LEASE_EXPIRED_MAX_ATTEMPTS')
})

await check('expired_lease_with_live_or_unverifiable_owner_fails_closed', async () => {
  const dataDir = caseDir('expired-live-owner')
  let nowMs = Date.parse('2026-08-12T06:00:00.000Z')
  const liveStore = createNativeTaskStore({
    dataDir,
    leaseMs: 100,
    clock: () => nowMs,
    processProbe: () => true,
  })
  await liveStore.createOrImport(pendingTask('NATIVE-EXPIRED-LIVE', { createdAt: new Date(nowMs).toISOString() }))
  const claimed = await liveStore.claimNext({ workerId: 'live-worker', workerPid: process.pid, leaseMs: 100, now: nowMs })
  nowMs += 101
  assert.equal(await liveStore.claimNext({ workerId: 'second-worker', now: nowMs }), null)
  const stillClaimed = await liveStore.read(claimed.id)
  assert.equal(stillClaimed.nativeBuild.state, 'claimed')
  assert.equal(stillClaimed.nativeBuild.attempt, 1)

  const unknownStore = createNativeTaskStore({
    dataDir,
    leaseMs: 100,
    clock: () => nowMs,
    processProbe: () => null,
  })
  assert.equal(await unknownStore.claimNext({ workerId: 'third-worker', now: nowMs }), null)
  assert.equal((await unknownStore.read(claimed.id)).nativeBuild.state, 'claimed')
})

await check('revision_lease_transition_and_idempotency_cas', async () => {
  const dataDir = caseDir('cas')
  const store = createNativeTaskStore({ dataDir, leaseMs: 60_000 })
  await store.createOrImport(pendingTask('NATIVE-CAS-1'))
  const claimed = await store.claimNext({ workerId: 'worker-cas' })
  const leaseId = claimed.nativeBuild.lease.id

  await rejectsCode(() => store.transition({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: 0,
    transitionId: 'cas-stale-revision',
    toState: 'planning',
  }), 'REVISION_CONFLICT')
  await rejectsCode(() => store.heartbeat({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId: 'wrong-lease',
    expectedRevision: claimed.revision,
  }), 'LEASE_MISMATCH')
  await rejectsCode(() => store.transition({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: claimed.revision,
    transitionId: 'cas-illegal-jump',
    toState: 'validating',
  }), 'INVALID_TRANSITION')

  const heartbeat = await store.heartbeat({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: claimed.revision,
  })
  const planningArgs = {
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: heartbeat.revision,
    transitionId: 'cas-planning-1',
    toState: 'planning',
    patch: { planArtifact: { recipeId: 'new-native-test-recipe' }, progress: 10 },
  }
  const planning = await store.transition(planningArgs)
  assert.equal(planning.nativeBuild.planArtifact.recipeId, 'new-native-test-recipe')
  const repeated = await store.transition(planningArgs)
  assert.equal(repeated.revision, planning.revision)
  assert.equal(repeated.nativeBuild.state, 'planning')
  await rejectsCode(() => store.transition({
    ...planningArgs,
    patch: { progress: 11 },
  }), 'TRANSITION_ID_REUSED')
  await rejectsCode(() => store.transition({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: planning.revision,
    transitionId: 'cas-protected-field',
    toState: 'building',
    patch: { lease: null },
  }), 'PROTECTED_TRANSITION_FIELD')

  const building = await store.transition({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: planning.revision,
    transitionId: 'cas-building-1',
    toState: 'building',
    patch: { execution: { phase: 'solidworks_native' } },
  })
  const validating = await store.transition({
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: building.revision,
    transitionId: 'cas-validating-1',
    toState: 'validating',
    patch: { validation: { phase: 'reopen' } },
  })
  const completeArgs = {
    taskId: claimed.id,
    workerId: 'worker-cas',
    leaseId,
    expectedRevision: validating.revision,
    transitionId: 'cas-complete-1',
    result: { manifestPath: 'attempt-0001/result-manifest.json', sha256: 'A'.repeat(64) },
  }
  const completed = await store.complete(completeArgs)
  assert.equal(completed.nativeBuild.state, 'ready')
  assert.equal(completed.nativeBuild.lease, null)
  assert.equal(completed.nativeBuild.result.sha256, 'A'.repeat(64))
  const repeatedComplete = await store.complete(completeArgs)
  assert.equal(repeatedComplete.revision, completed.revision)
})

await check('failure_and_cancel_are_cas_and_idempotent', async () => {
  const dataDir = caseDir('failure-cancel')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-FAIL-1'))
  const claimed = await store.claimNext({ workerId: 'worker-fail' })
  const failedArgs = {
    taskId: claimed.id,
    workerId: 'worker-fail',
    leaseId: claimed.nativeBuild.lease.id,
    expectedRevision: claimed.revision,
    transitionId: 'failure-1',
    code: 'RECIPE_NOT_AVAILABLE',
    message: 'No trusted native recipe is registered.',
    retryable: false,
  }
  const failed = await store.fail(failedArgs)
  assert.equal(failed.nativeBuild.state, 'failed')
  assert.equal(failed.nativeBuild.error.code, 'RECIPE_NOT_AVAILABLE')
  assert.equal((await store.fail(failedArgs)).revision, failed.revision)

  await store.createOrImport(pendingTask('NATIVE-CANCEL-1', { createdAt: '2026-08-12T02:00:00.000Z' }))
  const cancelledArgs = {
    taskId: 'NATIVE-CANCEL-1',
    username: 'engineer-a',
    expectedRevision: 0,
    transitionId: 'cancel-1',
    reason: 'customer changed requirement',
  }
  const cancelled = await store.cancel(cancelledArgs)
  assert.equal(cancelled.nativeBuild.state, 'cancelled')
  assert.equal((await store.cancel(cancelledArgs)).revision, cancelled.revision)
})

await check('checkpoint_resume_is_strict_atomic_and_preserves_plan_attempt_and_receipts', async () => {
  const fixedNow = '2026-08-12T09:00:00.000Z'
  const planSha256 = 'A'.repeat(64)
  const workerId = 'checkpoint-worker'
  const checkpointToolchain = {
    native_seed_pack_888x14_v1: '1'.repeat(64),
    native_width_888_v1: '2'.repeat(64),
    native_door_module_888x14_v1: '3'.repeat(64),
    native_lock_topology_888x14_v1: '4'.repeat(64),
    native_root_assembly_888x14_v1: '5'.repeat(64),
    native_final_pack_888x14_v1: '6'.repeat(64),
  }
  const checkpointFor = (taskId, overrides = {}) => ({
    schema: NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
    taskId,
    attempt: 2,
    planSha256,
    planWorkerId: workerId,
    completedPhase: 'assemblies',
    completedReceiptPath: 'evidence/width-assemblies.receipt.json',
    completedReceiptSha256: 'B'.repeat(64),
    completedEvidencePath: 'evidence/width-assemblies.json',
    completedEvidenceSha256: 'C'.repeat(64),
    currentInventoryDigest: 'D'.repeat(64),
    workingPackRelativePath: 'native_cad/working_pack',
    prefixReceiptChainSha256: 'E'.repeat(64),
    checkpointAuditPath: 'diagnostics/assemblies-runtime-audit.json',
    checkpointAuditSha256: 'F'.repeat(64),
    toolchainManifests: checkpointToolchain,
    ...overrides,
  })
  const cancelledTask = (id, overrides = {}, taskOverrides = {}) => pendingTask(id, {
    status: 'native_task_cancelled',
    nativeBuild: {
      ...pendingTask(`${id}-template`).nativeBuild,
      state: 'cancelled',
      attempt: 2,
      lease: null,
      planArtifact: {
        recipeId: 'continuation-native-plan',
        fileName: 'native_build_plan.json',
        fileSha256: planSha256,
      },
      receipts: {
        dimensions: { sha256: 'D'.repeat(64) },
        assemblies: { sha256: 'E'.repeat(64) },
      },
      modelReady: false,
      result: null,
      ...overrides,
    },
    ...taskOverrides,
  })
  const resumeArgsFor = (id, checkpoint, extra = {}) => ({
    taskId: id,
    expectedRevision: 0,
    transitionId: `checkpoint-resume-${id}`,
    workerId,
    leaseMs: 60_000,
    checkpoint,
    checkpointDigest: nativeCheckpointDescriptorDigest(checkpoint),
    now: fixedNow,
    ...extra,
  })

  const dataDir = caseDir('checkpoint-resume-success')
  const store = createNativeTaskStore({ dataDir })
  const taskId = 'NATIVE-CHECKPOINT-RESUME'
  const checkpoint = checkpointFor(taskId)
  const original = await store.createOrImport(cancelledTask(taskId))
  const originalPlan = original.nativeBuild.planArtifact
  const originalReceipts = original.nativeBuild.receipts
  const resumed = await store.resumeCancelledFromCheckpoint(resumeArgsFor(taskId, checkpoint))
  assert.equal(resumed.revision, 1)
  assert.equal(resumed.nativeBuild.state, 'building')
  assert.equal(resumed.status, 'native_build_in_progress')
  assert.equal(resumed.nativeBuild.attempt, 2)
  assert.deepEqual(resumed.nativeBuild.planArtifact, originalPlan)
  assert.deepEqual(resumed.nativeBuild.receipts, originalReceipts)
  assert.deepEqual(resumed.nativeBuild.checkpoint, checkpoint)
  assert.equal(resumed.nativeBuild.checkpointDigest, nativeCheckpointDescriptorDigest(checkpoint))
  assert.equal(resumed.nativeBuild.modelReady, false)
  assert.equal(resumed.modelReady, false)
  assert.equal(resumed.nativeBuild.lease.workerId, workerId)
  assert.equal(resumed.nativeBuild.lease.workerPid, process.pid)
  assert.equal(resumed.nativeBuild.lease.durationMs, 60_000)
  assert.equal(resumed.nativeBuild.lease.expiresAt, '2026-08-12T09:01:00.000Z')
  const history = resumed.nativeBuild.statusHistory.at(-1)
  assert.equal(history.operation, 'checkpoint_resume')
  assert.equal(history.from, 'cancelled')
  assert.equal(history.to, 'building')
  assert.equal(history.leaseId, resumed.nativeBuild.lease.id)
  assert.equal(resumed.nativeBuild.lastTransitionId, resumeArgsFor(taskId, checkpoint).transitionId)

  const repeated = await store.resumeCancelledFromCheckpoint(resumeArgsFor(taskId, checkpoint))
  assert.equal(repeated.revision, resumed.revision)
  assert.equal(repeated.nativeBuild.lease.id, resumed.nativeBuild.lease.id)
  await rejectsCode(() => store.resumeCancelledFromCheckpoint(resumeArgsFor(taskId, checkpoint, {
    leaseMs: 120_000,
  })), 'TRANSITION_ID_REUSED')

  const invalid = async (name, task, mutateArgs, code) => {
    const invalidStore = createNativeTaskStore({ dataDir: caseDir(`checkpoint-resume-${name}`) })
    await invalidStore.createOrImport(task)
    const baseCheckpoint = checkpointFor(task.id)
    const baseArgs = resumeArgsFor(task.id, baseCheckpoint, mutateArgs?.args || {})
    if (mutateArgs?.checkpoint) {
      baseArgs.checkpoint = mutateArgs.checkpoint
      baseArgs.checkpointDigest = nativeCheckpointDescriptorDigest(mutateArgs.checkpoint)
    }
    await rejectsCode(() => invalidStore.resumeCancelledFromCheckpoint(baseArgs), code)
  }

  await invalid('not-cancelled', pendingTask('NATIVE-CHECKPOINT-NOT-CANCELLED'), null, 'CHECKPOINT_RESUME_STATE_INVALID')
  await invalid('lease-present', cancelledTask('NATIVE-CHECKPOINT-LEASE', {
    lease: { id: 'already-owned', workerId, expiresAt: '2030-01-01T00:00:00.000Z' },
  }), null, 'CHECKPOINT_RESUME_LEASE_PRESENT')
  await invalid('archived', cancelledTask('NATIVE-CHECKPOINT-ARCHIVED', { archived: true }), null, 'CHECKPOINT_RESUME_ARCHIVED')
  await invalid('result-present', cancelledTask('NATIVE-CHECKPOINT-RESULT', { result: { receipt: 'result' } }), null, 'CHECKPOINT_RESUME_RESULT_PRESENT')
  await invalid('native-model-ready', cancelledTask('NATIVE-CHECKPOINT-NATIVE-READY', { modelReady: true }), null, 'CHECKPOINT_RESUME_MODEL_READY')
  await invalid('task-model-ready', cancelledTask('NATIVE-CHECKPOINT-TASK-READY', {}, { modelReady: true }), null, 'CHECKPOINT_RESUME_MODEL_READY')
  await invalid('wrong-revision', cancelledTask('NATIVE-CHECKPOINT-REVISION'), { args: { expectedRevision: 7 } }, 'REVISION_CONFLICT')
  await invalid('worker-mismatch', cancelledTask('NATIVE-CHECKPOINT-WORKER'), {
    checkpoint: checkpointFor('NATIVE-CHECKPOINT-WORKER', { planWorkerId: 'other-worker' }),
  }, 'CHECKPOINT_WORKER_MISMATCH')
  await invalid('attempt-mismatch', cancelledTask('NATIVE-CHECKPOINT-ATTEMPT'), {
    checkpoint: checkpointFor('NATIVE-CHECKPOINT-ATTEMPT', { attempt: 3 }),
  }, 'CHECKPOINT_ATTEMPT_MISMATCH')
  await invalid('plan-mismatch', cancelledTask('NATIVE-CHECKPOINT-PLAN'), {
    checkpoint: checkpointFor('NATIVE-CHECKPOINT-PLAN', { planSha256: 'B'.repeat(64) }),
  }, 'CHECKPOINT_PLAN_MISMATCH')

  const digestStore = createNativeTaskStore({ dataDir: caseDir('checkpoint-resume-digest') })
  const digestTaskId = 'NATIVE-CHECKPOINT-DIGEST'
  await digestStore.createOrImport(cancelledTask(digestTaskId))
  const digestCheckpoint = checkpointFor(digestTaskId)
  await rejectsCode(() => digestStore.resumeCancelledFromCheckpoint(resumeArgsFor(digestTaskId, digestCheckpoint, {
    checkpointDigest: 'F'.repeat(64),
  })), 'CHECKPOINT_DIGEST_MISMATCH')

  const shapeStore = createNativeTaskStore({ dataDir: caseDir('checkpoint-resume-shape') })
  const shapeTaskId = 'NATIVE-CHECKPOINT-SHAPE'
  await shapeStore.createOrImport(cancelledTask(shapeTaskId))
  const pathCheckpoint = checkpointFor(shapeTaskId, { path: 'attempt-0002/native_cad' })
  const pathArgs = resumeArgsFor(shapeTaskId, checkpointFor(shapeTaskId))
  pathArgs.checkpoint = pathCheckpoint
  await rejectsCode(() => shapeStore.resumeCancelledFromCheckpoint(pathArgs), 'CHECKPOINT_DESCRIPTOR_FIELDS_INVALID')
  const wrongSchema = checkpointFor(shapeTaskId, { schema: 'winnsen.forged.checkpoint.v1' })
  const wrongSchemaArgs = resumeArgsFor(shapeTaskId, checkpointFor(shapeTaskId))
  wrongSchemaArgs.checkpoint = wrongSchema
  await rejectsCode(() => shapeStore.resumeCancelledFromCheckpoint(wrongSchemaArgs), 'CHECKPOINT_DESCRIPTOR_SCHEMA_INVALID')

  await invalid('existing-checkpoint', cancelledTask('NATIVE-CHECKPOINT-EXISTING', {
    checkpoint: checkpointFor('NATIVE-CHECKPOINT-EXISTING', { attempt: 1 }),
    checkpointDigest: nativeCheckpointDescriptorDigest(checkpointFor('NATIVE-CHECKPOINT-EXISTING', { attempt: 1 })),
  }), null, 'CHECKPOINT_EXISTING_MISMATCH')
  await invalid('orphan-checkpoint-digest', cancelledTask('NATIVE-CHECKPOINT-ORPHAN-DIGEST', {
    checkpointDigest: 'C'.repeat(64),
  }), null, 'CHECKPOINT_EXISTING_MISMATCH')

  const ordinaryCancelStore = createNativeTaskStore({ dataDir: caseDir('checkpoint-resume-ordinary-cancel') })
  const ordinary = await ordinaryCancelStore.createOrImport(pendingTask('NATIVE-CHECKPOINT-ORDINARY-CANCEL'))
  const ordinaryCancelled = await ordinaryCancelStore.cancel({
    taskId: ordinary.id,
    username: ordinary.username,
    expectedRevision: ordinary.revision,
    transitionId: 'ordinary-cancel-stays-ordinary',
    reason: 'ordinary cancellation must not become a checkpoint resume',
    now: fixedNow,
  })
  assert.equal(ordinaryCancelled.nativeBuild.state, 'cancelled')
  assert.equal(ordinaryCancelled.nativeBuild.checkpoint, undefined)
  assert.equal(ordinaryCancelled.nativeBuild.statusHistory.at(-1).operation, 'cancel')
})

await check('trusted_executor_revision_can_requeue_only_its_exact_blocked_task', async () => {
  const dataDir = caseDir('executor-requeue')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-REQUEUE-1'))
  const claimed = await store.claimNext({ workerId: 'planner-requeue' })
  const planning = await store.transition({
    taskId: claimed.id,
    workerId: 'planner-requeue',
    leaseId: claimed.nativeBuild.lease.id,
    expectedRevision: claimed.revision,
    transitionId: 'requeue-planning',
    toState: 'planning',
  })
  const blocked = await store.transition({
    taskId: planning.id,
    workerId: 'planner-requeue',
    leaseId: planning.nativeBuild.lease.id,
    expectedRevision: planning.revision,
    transitionId: 'requeue-blocked',
    toState: 'blocked',
    patch: {
      blocker: { code: 'native_recipe_executor_not_implemented', retryable: false },
      planArtifact: { fileName: 'native_build_plan.json', attempt: 1 },
    },
  })
  const args = {
    taskId: blocked.id,
    expectedRevision: blocked.revision,
    transitionId: 'executor-revision-requeue-1',
    expectedBlockerCode: 'native_recipe_executor_not_implemented',
    executorRevision: 'A'.repeat(64),
  }
  const requeued = await store.requeueBlockedForExecutor(args)
  assert.equal(requeued.nativeBuild.state, 'created')
  assert.equal(requeued.status, 'native_task_created')
  assert.equal(requeued.nativeBuild.lease, null)
  assert.equal(requeued.nativeBuild.blocker, null)
  assert.equal(requeued.nativeBuild.planArtifact, null)
  assert.equal(requeued.nativeBuild.executorRevision, 'A'.repeat(64))
  assert.equal((await store.requeueBlockedForExecutor(args)).revision, requeued.revision)
  const reclaimed = await store.claimNext({ workerId: 'native-executor-v1' })
  assert.equal(reclaimed.id, blocked.id)
  assert.equal(reclaimed.nativeBuild.attempt, 2)

  const badDir = caseDir('executor-requeue-wrong-code')
  const badStore = createNativeTaskStore({ dataDir: badDir })
  await badStore.createOrImport(pendingTask('NATIVE-REQUEUE-BAD', {
    status: 'native_task_blocked',
    nativeBuild: {
      ...pendingTask('unused').nativeBuild,
      state: 'blocked',
      blocker: { code: 'geometry_validation_failed', retryable: false },
    },
  }))
  await rejectsCode(() => badStore.requeueBlockedForExecutor({
    taskId: 'NATIVE-REQUEUE-BAD',
    expectedRevision: 0,
    transitionId: 'must-not-requeue',
    expectedBlockerCode: 'native_recipe_executor_not_implemented',
    executorRevision: 'B'.repeat(64),
  }), 'BLOCKER_CODE_MISMATCH')
})

await check('archive_preserves_ready_state_and_task_file_with_owner_cas', async () => {
  const dataDir = caseDir('archive')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-ARCHIVE-READY', {
    status: 'native_assistance_model_ready',
    taskType: 'generated_native_structure_assistance_model',
    deliveryMode: 'verified_native_task_model',
    modelReady: true,
    nativeBuild: {
      schema: NATIVE_BUILD_SCHEMA,
      state: 'ready',
      attempt: 1,
      lease: null,
      modelReady: true,
      statusHistory: [{ state: 'ready', at: '2026-08-12T01:30:00.000Z' }],
    },
  }))
  const archiveArgs = {
    taskId: 'NATIVE-ARCHIVE-READY',
    username: 'engineer-a',
    expectedRevision: 0,
    transitionId: 'archive-ready-1',
    reason: 'hide from active portal list',
  }
  const archived = await store.archive(archiveArgs)
  assert.equal(archived.nativeBuild.state, 'ready')
  assert.equal(archived.status, 'native_assistance_model_ready')
  assert.equal(archived.modelReady, true)
  assert.equal(archived.nativeBuild.archived, true)
  assert.equal(archived.revision, 1)
  assert.equal(existsSync(resolve(dataDir, 'NATIVE-ARCHIVE-READY.json')), true)
  assert.equal((await store.archive(archiveArgs)).revision, 1)
  await rejectsCode(() => store.archive({ ...archiveArgs, transitionId: 'archive-wrong-owner', username: 'engineer-b', expectedRevision: 1 }), 'TASK_NOT_FOUND')
  await rejectsCode(() => store.archive({ ...archiveArgs, transitionId: 'archive-stale', expectedRevision: 0 }), 'REVISION_CONFLICT')

  await store.createOrImport(pendingTask('NATIVE-ARCHIVE-ACTIVE'))
  await rejectsCode(() => store.archive({
    taskId: 'NATIVE-ARCHIVE-ACTIVE',
    username: 'engineer-a',
    expectedRevision: 0,
    transitionId: 'archive-active-1',
  }), 'TASK_NOT_ARCHIVABLE')
})

await check('claim_and_cancel_race_has_one_serialized_winner', async () => {
  const dataDir = caseDir('cancel-race')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-CANCEL-RACE'))
  const barrier = resolve(dataDir, 'cancel.barrier')
  const claim = spawnJson(['--child-claim', dataDir, barrier, 'worker-race'])
  const cancel = spawnJson([
    '--child-cancel', dataDir, barrier,
    'NATIVE-CANCEL-RACE', '0', 'cancel-race-1', 'engineer-a',
  ])
  await releaseBarrier(barrier)
  const [claimResult, cancelResult] = await Promise.all([claim, cancel])
  const finalTask = await store.read('NATIVE-CANCEL-RACE')
  if (finalTask.nativeBuild.state === 'cancelled') {
    assert.equal(cancelResult.ok, true)
    assert.equal(claimResult.ok, true)
    assert.equal(claimResult.task, null)
  } else {
    assert.equal(finalTask.nativeBuild.state, 'claimed')
    assert.equal(claimResult.ok, true)
    assert.ok(claimResult.task)
    assert.equal(cancelResult.ok, false)
    assert.equal(cancelResult.code, 'REVISION_CONFLICT')
  }
  assert.equal(finalTask.revision, 1)
})

await check('corrupt_index_is_archived_and_rebuilt_from_task_files', async () => {
  const dataDir = caseDir('corrupt-index')
  const store = createNativeTaskStore({ dataDir })
  await store.createOrImport(pendingTask('NATIVE-RECOVER-1'))
  writeFileSync(store.indexPath, '{not-json', 'utf8')
  const listed = await store.list()
  assert.deepEqual(listed.map((item) => item.id), ['NATIVE-RECOVER-1'])
  const recovered = JSON.parse(readFileSync(store.indexPath, 'utf8'))
  assert.deepEqual(recovered.requests.map((item) => item.id), ['NATIVE-RECOVER-1'])
  assert.equal(readdirSync(dataDir).some((name) => name.startsWith('native_model_request_index.json.corrupt-')), true)
})

await check('index_only_legacy_row_is_never_claimed', async () => {
  const dataDir = caseDir('index-only')
  writeFileSync(resolve(dataDir, 'native_model_request_index.json'), JSON.stringify({
    schema: NATIVE_TASK_INDEX_SCHEMA,
    requests: [pendingTask('NATIVE-LEGACY-INDEX-ONLY', { storageMode: 'legacy_index' })],
  }), 'utf8')
  const store = createNativeTaskStore({ dataDir })
  assert.deepEqual(await store.list(), [])
  assert.equal(await store.claimNext({ workerId: 'worker-index-only' }), null)
  assert.deepEqual(JSON.parse(readFileSync(store.indexPath, 'utf8')).requests, [])
})

await check('stale_lock_requires_verified_dead_owner_and_live_owner_fails_closed', async () => {
  const deadDir = caseDir('stale-dead-lock')
  const deadStore = createNativeTaskStore({
    dataDir: deadDir,
    lockTimeoutMs: 200,
    lockRetryMs: 5,
    lockStaleMs: 10,
    processProbe: () => false,
  })
  writeFileSync(deadStore.lockPath, JSON.stringify({ token: 'dead-owner', pid: 99999999 }), 'utf8')
  const oldTime = new Date(Date.now() - 60_000)
  utimesSync(deadStore.lockPath, oldTime, oldTime)
  assert.deepEqual(await deadStore.list(), [])
  assert.equal(existsSync(deadStore.lockPath), false)

  const liveDir = caseDir('stale-live-lock')
  const liveStore = createNativeTaskStore({
    dataDir: liveDir,
    lockTimeoutMs: 60,
    lockRetryMs: 5,
    lockStaleMs: 10,
    processProbe: () => true,
  })
  writeFileSync(liveStore.lockPath, JSON.stringify({ token: 'live-owner', pid: process.pid }), 'utf8')
  utimesSync(liveStore.lockPath, oldTime, oldTime)
  await rejectsCode(() => liveStore.list(), 'STORE_LOCK_TIMEOUT')
  assert.equal(existsSync(liveStore.lockPath), true)
  rmSync(liveStore.lockPath, { force: true })
})

let exitCode = 0
try {
  mkdirSync(TEMP_ROOT, { recursive: true })
} catch {}

for (const result of checks) {
  process.stdout.write(`${result.ok ? 'PASS' : 'FAIL'} ${result.name}${result.ok ? '' : `\n${result.error}`}\n`)
  if (!result.ok) exitCode = 1
}
process.stdout.write(`${checks.filter((item) => item.ok).length}/${checks.length} native task store checks passed\n`)

if (TEMP_ROOT.startsWith(resolve(ROOT, 'tmp') + '\\') || TEMP_ROOT.startsWith(resolve(ROOT, 'tmp') + '/')) {
  rmSync(TEMP_ROOT, { recursive: true, force: true })
}
process.exit(exitCode)
