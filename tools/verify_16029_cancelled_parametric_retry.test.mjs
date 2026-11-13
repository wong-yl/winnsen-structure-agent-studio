import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, join, resolve } from 'node:path'
import {
  NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
  createNativeTaskStore,
  nativeCheckpointDescriptorDigest,
} from './lib/locker_16029_native_task_store.mjs'

const NOW = '2026-11-12T02:00:00.000Z'
const RETRY_NOW = '2026-11-12T02:00:01.000Z'
const EXECUTOR_REVISION = 'A'.repeat(64)
const TEMP_PREFIX = 'verify_16029_cancelled_parametric_retry-'
const TEMP_ROOT = mkdtempSync(join(tmpdir(), TEMP_PREFIX))
const checks = []

function storeFor(name) {
  return createNativeTaskStore({ dataDir: join(TEMP_ROOT, name), clock: () => NOW })
}

function pendingTask(id, overrides = {}) {
  return {
    id,
    username: 'retry-owner',
    createdAt: '2026-11-11T09:00:00.000Z',
    requestFingerprint: `fingerprint-${id}`,
    taskType: 'native_solidworks_build_task',
    status: 'native_task_created',
    storageMode: 'task_file_source',
    modelReady: false,
    engineeringAssistanceReady: false,
    downloadUrl: '',
    deliveryMode: 'native_task_pending',
    cabinetWidth: 1200,
    cabinetHeight: 1917,
    cabinetDepth: 485,
    columnDoorCounts: [12, 13],
    rowSequence: 'L12-R13',
    parametricRequest: {
      cabinet: { widthMm: 1200, heightMm: 1917, depthMm: 485 },
      columns: ['L', 'R'].map((side, column) => ({
        side, doors: Array.from({ length: 12 + column }, () => ({ heightMm: 140.25 + column })),
      })),
    },
    nativeBuild: {
      state: 'created', attempt: 0, lease: null, modelReady: false, executionStarted: false,
      requiredCad: 'SolidWorks 2020 native',
      requiredChecks: ['request_dimensions_exact'],
      statusHistory: [{ state: 'created', at: '2026-11-11T09:00:00.000Z' }],
    },
    ...overrides,
  }
}

function checkpointFor(task) {
  return {
    schema: NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
    taskId: task.id,
    attempt: task.nativeBuild.attempt,
    planSha256: '1'.repeat(64),
    planWorkerId: task.nativeBuild.lease.workerId,
    completedPhase: 'assemblies',
    completedReceiptPath: 'evidence/assemblies.receipt.json',
    completedReceiptSha256: '2'.repeat(64),
    completedEvidencePath: 'evidence/assemblies.json',
    completedEvidenceSha256: '3'.repeat(64),
    currentInventoryDigest: '4'.repeat(64),
    workingPackRelativePath: 'native_cad/working_pack',
    prefixReceiptChainSha256: '5'.repeat(64),
    checkpointAuditPath: '',
    checkpointAuditSha256: '',
    toolchainManifests: Object.fromEntries([
      'native_seed_pack_888x14_v1', 'native_width_888_v1', 'native_door_module_888x14_v1',
      'native_lock_topology_888x14_v1', 'native_root_assembly_888x14_v1', 'native_final_pack_888x14_v1',
    ].map(toolId => [toolId, '6'.repeat(64)])),
  }
}

function leaseArgs(task) {
  return {
    taskId: task.id,
    expectedRevision: task.revision,
    workerId: task.nativeBuild.lease.workerId,
    leaseId: task.nativeBuild.lease.id,
  }
}

async function cancelActiveTask(store, active, suffix = 'first') {
  const requested = await store.cancel({ taskId: active.id, username: active.username,
    expectedRevision: active.revision, transitionId: `${active.id}-${suffix}-cancel-request`, reason: 'User paused execution' })
  assert.equal(requested.nativeBuild.state, 'cancel_requested')
  assert.deepEqual(requested.nativeBuild.lease, active.nativeBuild.lease)
  const cancelled = await store.transition({ ...leaseArgs(requested),
    transitionId: `${active.id}-${suffix}-cancelled`, toState: 'cancelled' })
  assert.equal(cancelled.nativeBuild.state, 'cancelled')
  assert.equal(cancelled.nativeBuild.lease, null)
  return cancelled
}

async function cancelledTask(store, id) {
  await store.createOrImport(pendingTask(id, { engineeringAssistanceReady: true, downloadUrl: '/obsolete-download' }))
  let task = await store.claimTask({ taskId: id, workerId: 'cancel-retry-test-worker', leaseMs: 120000 })
  task = await store.transition({ ...leaseArgs(task), transitionId: `${id}-planning`, toState: 'planning' })
  const checkpoint = checkpointFor(task)
  task = await store.transition({ ...leaseArgs(task), transitionId: `${id}-building`, toState: 'building', patch: {
    planArtifact: { fileSha256: checkpoint.planSha256, relativePath: 'plan.json' },
    validation: { ok: false, stage: 'unfinished' },
    blocker: { code: 'previous_attempt_summary' },
    checkpoint,
    checkpointDigest: nativeCheckpointDescriptorDigest(checkpoint),
    execution: { started: true, cadStarted: true, modelGenerated: false, modelReady: false },
    progress: { stage: 'assemblies', percent: 55 },
    message: 'Previous attempt stopped at an atomic stage',
  } })
  return cancelActiveTask(store, task)
}

function retryArgs(task, overrides = {}) {
  return {
    taskId: task.id,
    username: task.username,
    expectedRevision: task.revision,
    transitionId: `${task.id}-retry`,
    expectedCancellationTransitionId: task.nativeBuild.statusHistory.filter(entry => entry.to === 'cancelled').at(-1)?.transitionId || 'missing-cancellation',
    executorRevision: EXECUTOR_REVISION,
    reason: 'User authorized continuing the same parameter request today',
    now: RETRY_NOW,
    ...overrides,
  }
}

async function rejectsUnchanged(store, before, args, code) {
  await assert.rejects(() => store.retryCancelledParametricTask(args), error => error?.code === code)
  assert.deepEqual(await store.read(before.id), before)
}

async function check(name, action) {
  try {
    await action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error?.stack || String(error) })
  }
}

try {
  await check('active_cancel_retry_preserves_identity_request_and_cancellation_evidence', async () => {
    const store = storeFor('happy-path')
    const cancelled = await cancelledTask(store, 'RETRY-HAPPY')
    const args = retryArgs(cancelled, { executorRevision: EXECUTOR_REVISION.toLowerCase() })
    const queued = await store.retryCancelledParametricTask(args)
    for (const key of ['id', 'username', 'createdAt', 'requestFingerprint', 'parametricRequest',
      'cabinetWidth', 'cabinetHeight', 'cabinetDepth', 'columnDoorCounts', 'rowSequence', 'taskType']) {
      assert.deepEqual(queued[key], cancelled[key], key)
    }
    assert.equal(queued.nativeBuild.state, 'created')
    assert.equal(queued.status, 'native_task_created')
    assert.equal(queued.revision, cancelled.revision + 1)
    assert.equal(queued.updatedAt, RETRY_NOW)
    assert.equal(queued.nativeBuild.attempt, cancelled.nativeBuild.attempt)
    assert.deepEqual(queued.nativeBuild.statusHistory.slice(0, -1), cancelled.nativeBuild.statusHistory)
    assert.deepEqual(queued.nativeBuild.cancelledRetryHistory, [{
      at: RETRY_NOW,
      cancelledAttempt: cancelled.nativeBuild.attempt,
      cancel: cancelled.nativeBuild.cancel,
      cancellationTransition: cancelled.nativeBuild.statusHistory.at(-1),
      executorRevision: EXECUTOR_REVISION,
      reason: args.reason,
    }])
    assert.equal(queued.nativeBuild.lastTransitionId, args.transitionId)
    assert.equal(queued.nativeBuild.statusHistory.at(-1).operation, 'cancelled_parametric_retry')
    assert.equal(queued.nativeBuild.statusHistory.at(-1).from, 'cancelled')
    assert.equal(queued.nativeBuild.statusHistory.at(-1).to, 'created')
    assert.equal(queued.nativeBuild.statusHistory.at(-1).leaseId, null)
    for (const key of ['cancel', 'error', 'blocker', 'planArtifact', 'validation', 'result', 'checkpoint', 'checkpointDigest', 'lease']) {
      assert.equal(queued.nativeBuild[key], null, key)
    }
    assert.equal(queued.nativeBuild.message, '')
    assert.equal(queued.nativeBuild.executionStarted, false)
    assert.equal(queued.nativeBuild.modelReady, false)
    assert.equal(queued.nativeBuild.executorRevision, EXECUTOR_REVISION)
    assert.deepEqual(queued.nativeBuild.execution, {
      started: false, cadStarted: false, modelGenerated: false, modelReady: false, legacyFallbackUsed: false,
    })
    assert.deepEqual(queued.nativeBuild.progress, { stage: 'created', percent: 0 })
    assert.deepEqual(queued.nativeBuild.requiredChecks, cancelled.nativeBuild.requiredChecks)
    assert.equal(queued.modelReady, false)
    assert.equal(queued.engineeringAssistanceReady, false)
    assert.equal(queued.downloadUrl, '')
    assert.equal(queued.deliveryMode, 'native_task_pending')
    assert.deepEqual((await store.listSnapshot()).find(task => task.id === queued.id), queued)
    const claimed = await store.claimTask({ taskId: queued.id, workerId: 'new-attempt-worker' })
    assert.equal(claimed.nativeBuild.state, 'claimed')
    assert.equal(claimed.nativeBuild.attempt, cancelled.nativeBuild.attempt + 1)
    assert.notEqual(claimed.nativeBuild.lease.id, cancelled.nativeBuild.statusHistory.at(-1).leaseId)
    assert.equal(await store.claimTask({ taskId: queued.id, workerId: 'second-worker' }), null)
    assert.deepEqual(claimed.nativeBuild.cancelledRetryHistory, queued.nativeBuild.cancelledRetryHistory)
  })

  await check('same_transition_is_idempotent_before_and_after_claim_and_changed_payload_is_rejected', async () => {
    const store = storeFor('idempotency')
    const cancelled = await cancelledTask(store, 'RETRY-IDEMPOTENT')
    const args = retryArgs(cancelled)
    const [queued, duplicate] = await Promise.all([store.retryCancelledParametricTask(args), store.retryCancelledParametricTask(args)])
    assert.deepEqual(duplicate, queued)
    assert.deepEqual(await store.retryCancelledParametricTask(args), queued)
    const claimed = await store.claimTask({ taskId: queued.id, workerId: 'next-worker' })
    assert.deepEqual(await store.retryCancelledParametricTask(args), claimed)
    for (const change of [{ reason: 'Different authorization' }, { executorRevision: 'B'.repeat(64) },
      { expectedCancellationTransitionId: 'another-cancellation' }]) {
      await rejectsUnchanged(store, claimed, { ...args, ...change }, 'TRANSITION_ID_REUSED')
    }
    await rejectsUnchanged(store, claimed, { ...args, username: 'other-owner' }, 'TASK_NOT_FOUND')
  })

  await check('multiple_cancellations_bind_latest_terminal_transition_and_preserve_all_history', async () => {
    const store = storeFor('multiple-cancellations')
    const first = await cancelledTask(store, 'RETRY-MULTIPLE')
    const firstArgs = retryArgs(first)
    const queued = await store.retryCancelledParametricTask(firstArgs)
    const claimed = await store.claimTask({ taskId: queued.id, workerId: 'second-attempt' })
    const second = await cancelActiveTask(store, claimed, 'second')
    const secondArgs = retryArgs(second, { transitionId: 'second-retry' })
    await rejectsUnchanged(store, second, { ...secondArgs,
      expectedCancellationTransitionId: firstArgs.expectedCancellationTransitionId }, 'CANCELLATION_TRANSITION_MISMATCH')
    const retried = await store.retryCancelledParametricTask(secondArgs)
    assert.equal(retried.nativeBuild.cancelledRetryHistory.length, 2)
    assert.deepEqual(retried.nativeBuild.cancelledRetryHistory[0], queued.nativeBuild.cancelledRetryHistory[0])
    assert.deepEqual(retried.nativeBuild.cancelledRetryHistory[1].cancel, second.nativeBuild.cancel)
    assert.deepEqual(retried.nativeBuild.cancelledRetryHistory[1].cancellationTransition, second.nativeBuild.statusHistory.at(-1))
    assert.equal(retried.nativeBuild.cancelledRetryHistory[1].cancelledAttempt, 2)
    assert.deepEqual(retried.nativeBuild.statusHistory.slice(0, -1), second.nativeBuild.statusHistory)
    assert.deepEqual(await store.retryCancelledParametricTask(firstArgs), retried)
  })

  await check('owner_revision_and_terminal_cancellation_guards_leave_task_unchanged', async () => {
    const store = storeFor('cas-guards')
    const cancelled = await cancelledTask(store, 'RETRY-GUARDS')
    const args = retryArgs(cancelled)
    await rejectsUnchanged(store, cancelled, { ...args, username: 'wrong-owner' }, 'TASK_NOT_FOUND')
    await rejectsUnchanged(store, cancelled, { ...args, expectedRevision: cancelled.revision - 1 }, 'REVISION_CONFLICT')
    await rejectsUnchanged(store, cancelled, { ...args, expectedRevision: undefined }, 'EXPECTED_REVISION_REQUIRED')
    await rejectsUnchanged(store, cancelled, { ...args, expectedCancellationTransitionId: 'wrong-cancellation' }, 'CANCELLATION_TRANSITION_MISMATCH')
    await rejectsUnchanged(store, cancelled, { ...args,
      expectedCancellationTransitionId: cancelled.nativeBuild.statusHistory.at(-2).transitionId }, 'CANCELLATION_TRANSITION_MISMATCH')
  })

  await check('missing_or_invalid_retry_inputs_are_rejected_without_mutation', async () => {
    const store = storeFor('input-guards')
    const cancelled = await cancelledTask(store, 'RETRY-INPUTS')
    const args = retryArgs(cancelled)
    for (const [change, code] of [
      [{ transitionId: '' }, 'TRANSITION_ID_REQUIRED'],
      [{ username: ' ' }, 'USERNAME_REQUIRED'],
      [{ expectedCancellationTransitionId: undefined }, 'CANCELLATION_TRANSITION_ID_REQUIRED'],
      [{ expectedCancellationTransitionId: ' ' }, 'CANCELLATION_TRANSITION_ID_REQUIRED'],
      [{ expectedCancellationTransitionId: 'x'.repeat(201) }, 'CANCELLATION_TRANSITION_ID_REQUIRED'],
      [{ reason: undefined }, 'RETRY_REASON_REQUIRED'],
      [{ reason: ' ' }, 'RETRY_REASON_REQUIRED'],
      [{ executorRevision: undefined }, 'EXECUTOR_REVISION_REQUIRED'],
      [{ executorRevision: 'a'.repeat(63) }, 'EXECUTOR_REVISION_REQUIRED'],
      [{ executorRevision: 'g'.repeat(64) }, 'EXECUTOR_REVISION_REQUIRED'],
    ]) await rejectsUnchanged(store, cancelled, { ...args, ...change }, code)
  })

  await check('reason_payload_is_not_truncated_for_idempotency', async () => {
    const store = storeFor('full-reason')
    const cancelled = await cancelledTask(store, 'RETRY-LONG-REASON')
    const args = retryArgs(cancelled, { reason: 'a'.repeat(1000) + ' original' })
    const queued = await store.retryCancelledParametricTask(args)
    assert.equal(queued.nativeBuild.cancelledRetryHistory[0].reason, args.reason)
    await rejectsUnchanged(store, queued, { ...args, reason: 'a'.repeat(1000) + ' changed' }, 'TRANSITION_ID_REUSED')
  })

  await check('non_cancelled_states_cannot_use_cancelled_retry', async () => {
    for (const state of ['created', 'claimed', 'planning', 'building', 'validating', 'cancel_requested', 'needs_input', 'blocked', 'failed', 'ready']) {
      const store = storeFor(`state-${state}`)
      let task = await store.createOrImport(pendingTask(`RETRY-STATE-${state}`))
      if (state !== 'created') task = await store.claimTask({ taskId: task.id, workerId: 'state-worker' })
      for (const toState of ['planning', 'building', 'validating']) {
        if (['planning', 'building', 'validating', 'ready'].includes(state)) {
          task = await store.transition({ ...leaseArgs(task), transitionId: `${state}-${toState}`, toState })
          if (state === toState) break
        }
      }
      if (state === 'cancel_requested') task = await store.cancel({ taskId: task.id, username: task.username,
        expectedRevision: task.revision, transitionId: 'state-cancel', reason: 'Pause' })
      if (['needs_input', 'blocked'].includes(state)) task = await store.transition({ ...leaseArgs(task), transitionId: `state-${state}`, toState: state })
      if (state === 'failed') task = await store.fail({ ...leaseArgs(task), transitionId: 'state-failed', code: 'fixture_failure', message: 'Test failure' })
      if (state === 'ready') task = await store.complete({ ...leaseArgs(task), transitionId: 'state-ready', result: { fixture: true } })
      assert.equal(task.nativeBuild.state, state)
      await rejectsUnchanged(store, task, retryArgs(task), 'TASK_NOT_RETRYABLE')
    }
  })

  await check('legacy_and_archived_tasks_cannot_use_cancelled_retry', async () => {
    const store = storeFor('legacy-archive')
    const legacy = await store.createOrImport(pendingTask('RETRY-LEGACY', { parametricRequest: undefined }))
    const cancelledLegacy = await store.cancel({ taskId: legacy.id, username: legacy.username,
      expectedRevision: legacy.revision, transitionId: 'legacy-cancel', reason: 'Pause' })
    await rejectsUnchanged(store, cancelledLegacy, retryArgs(cancelledLegacy), 'TASK_NOT_RETRYABLE')
    const cancelled = await cancelledTask(store, 'RETRY-ARCHIVED')
    const archived = await store.archive({ taskId: cancelled.id, username: cancelled.username,
      expectedRevision: cancelled.revision, transitionId: 'archive-cancelled', reason: 'History only' })
    await rejectsUnchanged(store, archived, retryArgs(archived), 'TASK_NOT_RETRYABLE')
  })

  await check('imported_ready_result_lease_or_archive_conflicts_fail_closed', async () => {
    const store = storeFor('import-conflicts')
    const cancelled = await cancelledTask(store, 'RETRY-CONFLICT-SOURCE')
    const conflicts = [
      { archived: true },
      { modelReady: true },
      { nativeBuild: { modelReady: true } },
      { nativeBuild: { lease: { id: 'orphan-lease', workerId: 'orphan-worker' } } },
      ...[{}, false, 0, ''].flatMap(result => [{ result }, { nativeBuild: { result } }]),
    ]
    for (let index = 0; index < conflicts.length; index += 1) {
      const change = conflicts[index]
      const imported = await store.createOrImport({ ...cancelled, ...change, id: `RETRY-CONFLICT-${index}`,
        nativeBuild: { ...cancelled.nativeBuild, ...change.nativeBuild } })
      await rejectsUnchanged(store, imported, retryArgs(imported), 'TASK_NOT_RETRYABLE')
    }
  })

  await check('imported_cancellation_requires_a_terminal_transition_identity', async () => {
    const store = storeFor('missing-cancellation-history')
    const cancelled = await cancelledTask(store, 'RETRY-HISTORY-SOURCE')
    for (const [index, statusHistory] of [[], [{ state: 'cancelled', at: NOW }],
      [{ to: 'cancelled', state: 'cancelled', at: NOW }]].entries()) {
      const imported = await store.createOrImport({ ...cancelled, id: `RETRY-NO-HISTORY-${index}`,
        nativeBuild: { ...cancelled.nativeBuild, statusHistory } })
      await rejectsUnchanged(store, imported, retryArgs(imported), 'CANCELLATION_TRANSITION_MISMATCH')
    }
  })

  await check('retry_preserves_previous_retry_audit_and_clears_current_error', async () => {
    const store = storeFor('failure-audit')
    const cancelled = await cancelledTask(store, 'RETRY-AUDIT-SOURCE')
    const retryHistory = [{ at: NOW, failedAttempt: 0, error: { code: 'old_failure' } }]
    const imported = await store.createOrImport({ ...cancelled, id: 'RETRY-AUDIT', nativeBuild: {
      ...cancelled.nativeBuild, retryHistory, error: { code: 'old_summary', message: 'Obsolete' },
    } })
    const queued = await store.retryCancelledParametricTask(retryArgs(imported))
    assert.equal(queued.nativeBuild.error, null)
    assert.deepEqual(queued.nativeBuild.retryHistory, retryHistory)
  })

  await check('competing_retry_transitions_have_one_revision_winner', async () => {
    const store = storeFor('competing-retries')
    const cancelled = await cancelledTask(store, 'RETRY-RACE')
    const args = retryArgs(cancelled)
    const results = await Promise.allSettled([
      store.retryCancelledParametricTask(args),
      store.retryCancelledParametricTask({ ...args, transitionId: 'competing-retry' }),
    ])
    assert.equal(results.filter(result => result.status === 'fulfilled').length, 1)
    assert.equal(results.find(result => result.status === 'rejected').reason.code, 'REVISION_CONFLICT')
    const queued = await store.read(cancelled.id)
    assert.equal(queued.revision, cancelled.revision + 1)
    assert.equal(queued.nativeBuild.cancelledRetryHistory.length, 1)
    assert.equal(queued.nativeBuild.statusHistory.length, cancelled.nativeBuild.statusHistory.length + 1)
  })

  await check('failed_retry_remains_unavailable_for_cancelled_tasks', async () => {
    const store = storeFor('failed-retry-boundary')
    const cancelled = await cancelledTask(store, 'RETRY-FAILED-BOUNDARY')
    await assert.rejects(() => store.retryFailedTask({ ...retryArgs(cancelled),
      expectedErrorCode: 'parametric_generation_failed' }), error => error?.code === 'TASK_NOT_RETRYABLE')
    assert.deepEqual(await store.read(cancelled.id), cancelled)
  })
} finally {
  const cleanupPath = resolve(TEMP_ROOT)
  if (dirname(cleanupPath) !== resolve(tmpdir()) || !basename(cleanupPath).startsWith(TEMP_PREFIX)) {
    throw new Error(`Refusing temporary store cleanup outside expected root: ${cleanupPath}`)
  }
  rmSync(cleanupPath, { recursive: true, force: true })
}

for (const result of checks) {
  process.stdout.write(`${result.ok ? 'PASS' : 'FAIL'} ${result.name}${result.ok ? '' : `\n${result.error}`}\n`)
}
process.stdout.write(`${checks.filter(result => result.ok).length}/${checks.length} cancelled parametric retry checks passed\n`)
process.exitCode = checks.every(result => result.ok) ? 0 : 1
