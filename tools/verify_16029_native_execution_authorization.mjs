import assert from 'node:assert/strict'
import { createHash, randomBytes } from 'node:crypto'
import { existsSync, linkSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  issueNativeExecutionAuthorization,
  validateNativeExecutionAuthorization,
} from './lib/locker_16029_native_execution_authorization.mjs'
import * as authorizationModule from './lib/locker_16029_native_execution_authorization.mjs'
import {
  NATIVE_RUNTIME_RECEIPT_KEYS,
  nativeEvidenceCommitmentSha256,
} from './lib/locker_16029_native_stage_contract.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const TEMP_ROOT = resolve(ROOT, `tmp/verify-native-authorization-${process.pid}-${randomBytes(4).toString('hex')}`)
const NOW = new Date('2026-08-12T10:00:00.000Z')
const WIDTH_PHASES = ['dimensions', 'derived', 'base-hole', 'assemblies']

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

function taskDigest(task) {
  return sha256(JSON.stringify(stableValue({
    id: task.id,
    revision: task.revision,
    status: task.status,
    taskType: task.taskType,
    requestFingerprint: task.requestFingerprint,
    nativeBuildAttempt: task.nativeBuild.attempt,
  })))
}

let fixtureSerial = 0
function fixture() {
  fixtureSerial += 1
  const attemptDir = resolve(TEMP_ROOT, `NATIVE-88814-TEST-${fixtureSerial}/attempt-0001`)
  mkdirSync(attemptDir, { recursive: true })
  const workerId = 'native-worker-authorization-test'
  const task = {
    id: `NATIVE-88814-TEST-${fixtureSerial}`,
    revision: 7,
    status: 'native_source_planning',
    taskType: 'native_solidworks_build_task',
    requestFingerprint: 'A'.repeat(64),
    nativeBuild: {
      state: 'planning',
      attempt: 1,
      lease: {
        id: 'lease-native-88814-test',
        workerId,
        claimedAt: '2026-08-12T09:59:00.000Z',
        heartbeatAt: '2026-08-12T09:59:55.000Z',
        expiresAt: '2026-08-12T10:20:00.000Z',
      },
    },
  }
  const plan = {
    schema: 'winnsen.native_build_plan.v1',
    createdAt: '2026-08-12T09:59:56.000Z',
    workerId,
    purpose: 'structure_engineering_assistance',
    task: { id: task.id, revisionAtPlanning: task.revision, digest: taskDigest(task) },
    request: { fingerprint: task.requestFingerprint, digest: 'B'.repeat(64) },
    recipe: {
      id: 'winnsen-16029-888w-14door-native-v1',
      version: 1,
      digest: 'C'.repeat(64),
    },
    geometry: {
      cabinetWidthMm: 888,
      cabinetHeightMm: 1917,
      cabinetDepthMm: 550,
      columns: 2,
      doorCount: 14,
      columnDoorCounts: [7, 7],
      rowSequence: 'L1111111-R1111111',
      doorPanelWidthMm: 381,
    },
    stageContracts: [{ id: 'build_native_geometry', toolId: 'native_16029_geometry_builder_v1', outputs: ['cabinet_parts'] }],
    stageReceiptContracts: {
      clone_native_seed: {
        schema: 'winnsen.16029.native_seed_pack_receipt.v1',
        path: 'receipts/clone_native_seed.json',
        producerToolId: 'native_seed_pack_888x14_v1',
        predecessor: '',
      },
      dimensions: {
        schema: 'winnsen.native_width_888.phase_receipt.v1',
        path: 'evidence/width-dimensions.receipt.json',
        producerToolId: 'native_width_888_v1',
        predecessor: 'clone_native_seed',
      },
    },
    requiredChecks: ['one_door_one_lock', 'rebuild_save_reopen', 'relocated_reopen'],
    qualityBoundary: { planningOnly: true, engineeringAssistanceReady: false },
    executionBoundary: {
      executorImplemented: false,
      cadStarted: false,
      modelGenerated: false,
      modelReady: false,
      legacyFallbackUsed: false,
    },
  }
  const planPath = resolve(attemptDir, 'native_build_plan.json')
  writeFileSync(planPath, `${JSON.stringify(plan, null, 2)}\n`, 'utf8')
  const tool = {
    id: 'native_width_888_v1',
    sourceNormalizedSha256: 'D'.repeat(64),
    executableSha256: 'E'.repeat(64),
  }
  return { attemptDir, planPath, task, workerId, tool }
}

function writeConsumedWidthReceipt(input, issued, { postInventoryDigest = '9'.repeat(64) } = {}) {
  const predecessorPath = resolve(input.attemptDir, 'receipts/clone_native_seed.json')
  mkdirSync(dirname(predecessorPath), { recursive: true })
  if (!existsSync(predecessorPath)) {
    writeFileSync(predecessorPath, '{"seed":"trusted-predecessor"}\n', 'utf8')
  }
  const evidenceRelativePath = 'evidence/width-dimensions.json'
  const evidencePath = resolve(input.attemptDir, evidenceRelativePath)
  mkdirSync(dirname(evidencePath), { recursive: true })
  const evidence = {
    schema: 'winnsen.locker16029.native_width_888.v1',
    success: true,
    functional_success: true,
    phase: 'dimensions',
    task_id: input.task.id,
    plan_sha256: issued.authorization.plan.sha256,
    authorization_id: issued.authorization.authorizationId,
    authorization_sha256: issued.sha256,
    initial_inventory_digest: issued.authorization.seed.inventoryDigest,
    post_inventory_digest: postInventoryDigest,
    completed_at_utc: '2026-08-12T10:00:30.000Z',
    evidence_commitment_sha256: '',
  }
  evidence.evidence_commitment_sha256 = nativeEvidenceCommitmentSha256(evidence)
  writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8')
  const authorizationBytes = readFileSync(issued.authorizationPath)
  const receipt = {
    schema: 'winnsen.native_width_888.phase_receipt.v1',
    phase: 'dimensions',
    success: true,
    completedAt: evidence.completed_at_utc,
    taskId: input.task.id,
    taskRevision: issued.authorization.task.revision,
    taskDigest: issued.authorization.task.digest,
    requestDigest: issued.authorization.request.digest,
    leaseId: issued.authorization.task.leaseId,
    leaseExpiresAt: issued.authorization.task.leaseExpiresAt,
    attempt: issued.authorization.execution.attempt,
    planSha256: issued.authorization.plan.sha256,
    authorizationId: issued.authorization.authorizationId,
    authorizationSha256: issued.sha256,
    authorizationJsonBase64: authorizationBytes.toString('base64'),
    authorizationIssuedAt: issued.authorization.issuedAt,
    authorizationExpiresAt: issued.authorization.expiresAt,
    recipeId: issued.authorization.recipe.id,
    recipeDigest: issued.authorization.recipe.digest,
    toolId: issued.authorization.tool.id,
    toolSourceNormalizedSha256: issued.authorization.tool.sourceNormalizedSha256,
    toolExecutableSha256: issued.authorization.tool.executableSha256,
    evidencePath: evidenceRelativePath,
    evidenceSha256: sha256(readFileSync(evidencePath)),
    evidenceCommitmentSha256: evidence.evidence_commitment_sha256,
    preInventoryDigest: issued.authorization.seed.inventoryDigest,
    postInventoryDigest,
    predecessorReceiptSha256: sha256(readFileSync(predecessorPath)),
  }
  assert.deepEqual(Object.keys(receipt), NATIVE_RUNTIME_RECEIPT_KEYS)
  const receiptPath = resolve(input.attemptDir, 'evidence/width-dimensions.receipt.json')
  writeFileSync(receiptPath, `${JSON.stringify(receipt, null, 2)}\n`, 'utf8')
  return { evidencePath, receiptPath, receipt }
}

const checks = []
async function check(name, action) {
  try {
    await action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: `${error?.code ? `${error.code}: ` : ''}${error?.message || error}` })
  }
}

rmSync(TEMP_ROOT, { recursive: true, force: true })

await check('issues_one_immutable_tool_scoped_authorization', () => {
  const input = fixture()
  const result = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
    ttlMs: 10 * 60 * 1000,
  })
  assert.equal(result.created, true)
  assert.equal(
    result.authorizationPath,
    resolve(input.attemptDir, 'execution_authorizations/native_width_888_v1.json'),
  )
  assert.equal(existsSync(result.authorizationPath), true)
  assert.match(result.sha256, /^[A-F0-9]{64}$/)
  const stored = JSON.parse(readFileSync(result.authorizationPath, 'utf8'))
  assert.equal(stored.schema, 'winnsen.native_execution_authorization.v1')
  assert.equal(stored.workerId, input.workerId)
  assert.equal(stored.task.id, input.task.id)
  assert.equal(stored.task.revision, input.task.revision)
  assert.equal(stored.task.leaseId, input.task.nativeBuild.lease.id)
  assert.equal(stored.task.leaseExpiresAt, input.task.nativeBuild.lease.expiresAt)
  assert.equal(stored.plan.sha256, sha256(readFileSync(input.planPath)))
  assert.equal(stored.tool.id, input.tool.id)
  assert.equal(stored.execution.authorized, true)
  assert.deepEqual(stored.execution.phases, ['dimensions', 'derived', 'base-hole', 'assemblies'])
  assert.equal(stored.qualityBoundary.engineeringAssistanceReady, false)
  assert.equal(Object.hasOwn(stored, 'command'), false)
  assert.equal(Object.hasOwn(stored, 'path'), false)
  const verified = validateNativeExecutionAuthorization({
    authorization: stored,
    task: input.task,
    planPath: input.planPath,
    workerId: input.workerId,
    tool: input.tool,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  assert.equal(verified.ok, true)
})

await check('same_authorization_is_idempotent_but_conflicting_content_is_rejected', () => {
  const input = fixture()
  const options = {
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
    ttlMs: 60_000,
  }
  const first = issueNativeExecutionAuthorization(options)
  const second = issueNativeExecutionAuthorization({
    ...options,
    clock: () => new Date(NOW.getTime() + 30_000),
  })
  assert.equal(second.created, false)
  assert.equal(second.sha256, first.sha256)
  assert.throws(
    () => issueNativeExecutionAuthorization({ ...options, seedInventoryDigest: '9'.repeat(64) }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT',
  )
})

await check('expired_authorization_can_be_atomically_reissued_for_the_same_tool_and_binding', () => {
  const input = fixture()
  const options = {
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
    ttlMs: 60_000,
  }
  const first = issueNativeExecutionAuthorization(options)
  input.task.nativeBuild.lease.expiresAt = '2026-08-12T10:21:30.000Z'
  const second = issueNativeExecutionAuthorization({
    ...options,
    task: input.task,
    clock: () => new Date('2026-08-12T10:01:30.000Z'),
  })
  assert.equal(second.created, true)
  assert.notEqual(second.sha256, first.sha256)
  assert.equal(second.authorizationPath, first.authorizationPath)
  assert.equal(second.authorization.task.leaseExpiresAt, '2026-08-12T10:21:30.000Z')
  assert.notEqual(second.authorization.authorizationId, first.authorization.authorizationId)
})

await check('rejects_expired_wrong_owner_or_nonplanning_inputs', () => {
  const input = fixture()
  const base = {
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  }
  assert.throws(
    () => issueNativeExecutionAuthorization({ ...base, workerId: 'different-worker' }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_LEASE_MISMATCH',
  )
  input.task.nativeBuild.lease.expiresAt = '2026-08-12T09:59:59.000Z'
  assert.throws(
    () => issueNativeExecutionAuthorization(base),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_LEASE_EXPIRED',
  )
  const nonplanning = fixture()
  nonplanning.task.nativeBuild.state = 'blocked'
  assert.throws(
    () => issueNativeExecutionAuthorization({ ...base, ...nonplanning }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_TASK_STATE_INVALID',
  )
})

await check('rejects_plan_tampering_and_authorization_tampering', () => {
  const input = fixture()
  const result = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const stored = JSON.parse(readFileSync(result.authorizationPath, 'utf8'))
  stored.execution.phases = ['assemblies']
  assert.throws(
    () => validateNativeExecutionAuthorization({
      authorization: stored,
      task: input.task,
      planPath: input.planPath,
      workerId: input.workerId,
      tool: input.tool,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      clock: () => NOW,
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_BINDING_MISMATCH',
  )
  const changedPlan = JSON.parse(readFileSync(input.planPath, 'utf8'))
  changedPlan.untrustedMutation = true
  writeFileSync(input.planPath, `${JSON.stringify(changedPlan, null, 2)}\n`, 'utf8')
  assert.throws(
    () => validateNativeExecutionAuthorization({
      authorization: JSON.parse(readFileSync(result.authorizationPath, 'utf8')),
      task: input.task,
      planPath: input.planPath,
      workerId: input.workerId,
      tool: input.tool,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      clock: () => NOW,
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_PLAN_MISMATCH',
  )
})

await check('rejects_lease_expiry_binding_tampering', () => {
  const input = fixture()
  const result = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const stored = JSON.parse(readFileSync(result.authorizationPath, 'utf8'))
  stored.task.leaseExpiresAt = '2026-08-12T10:29:00.000Z'
  assert.throws(
    () => validateNativeExecutionAuthorization({
      authorization: stored,
      task: input.task,
      planPath: input.planPath,
      workerId: input.workerId,
      tool: input.tool,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      clock: () => NOW,
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_BINDING_MISMATCH',
  )
})

await check('rejects_quality_boundary_or_nested_key_tampering', () => {
  const input = fixture()
  const result = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  for (const mutate of [
    (value) => { value.qualityBoundary.readyOnlyAfterEveryRequiredCheckPasses = false },
    (value) => { value.qualityBoundary.command = 'forbidden' },
    (value) => { value.task.command = 'forbidden' },
  ]) {
    const stored = JSON.parse(readFileSync(result.authorizationPath, 'utf8'))
    mutate(stored)
    assert.throws(
      () => validateNativeExecutionAuthorization({
        authorization: stored,
        task: input.task,
        planPath: input.planPath,
        workerId: input.workerId,
        tool: input.tool,
        seedInventoryDigest: 'F'.repeat(64),
        attempt: 1,
        phases: WIDTH_PHASES,
        clock: () => NOW,
      }),
      (error) => error?.code === 'NATIVE_AUTHORIZATION_BINDING_MISMATCH',
    )
  }
})

await check('rejects_hardlinked_native_plan', () => {
  const input = fixture()
  const secondName = resolve(dirname(input.attemptDir), `hardlink-${basename(input.attemptDir)}.json`)
  linkSync(input.planPath, secondName)
  assert.throws(
    () => issueNativeExecutionAuthorization({
      ...input,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      clock: () => NOW,
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_HARDLINK_PATH',
  )
})

await check('different_tools_receive_different_non_overwriting_files', () => {
  const input = fixture()
  const common = {
    attemptDir: input.attemptDir,
    planPath: input.planPath,
    task: input.task,
    workerId: input.workerId,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    clock: () => NOW,
  }
  const width = issueNativeExecutionAuthorization({
    ...common,
    tool: input.tool,
    phases: WIDTH_PHASES,
  })
  const door = issueNativeExecutionAuthorization({
    ...common,
    tool: {
      id: 'native_door_module_888x14_v1',
      sourceNormalizedSha256: '1'.repeat(64),
      executableSha256: '2'.repeat(64),
    },
    phases: ['door_module_888x14'],
  })
  assert.notEqual(width.authorizationPath, door.authorizationPath)
  assert.equal(existsSync(width.authorizationPath), true)
  assert.equal(existsSync(door.authorizationPath), true)
})

await check('consumed_stage_receipt_archives_authorization_and_allows_next_inventory_binding', () => {
  assert.equal(typeof authorizationModule.consumeNativeExecutionAuthorization, 'function')
  const input = fixture()
  const first = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const committed = writeConsumedWidthReceipt(input, first)
  const consumed = authorizationModule.consumeNativeExecutionAuthorization({
    attemptDir: input.attemptDir,
    planPath: input.planPath,
    task: input.task,
    workerId: input.workerId,
    tool: input.tool,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    authorizationPath: first.authorizationPath,
    receiptPath: committed.receiptPath,
    expectedPhase: 'dimensions',
    expectedPostInventoryDigest: '9'.repeat(64),
    clock: () => new Date('2026-08-12T10:00:40.000Z'),
  })
  assert.equal(existsSync(first.authorizationPath), false)
  assert.equal(existsSync(consumed.archivedAuthorizationPath), true)
  assert.equal(consumed.authorizationSha256, first.sha256)

  input.task.revision += 2
  input.task.status = 'native_task_building'
  input.task.nativeBuild.state = 'building'
  input.task.nativeBuild.planArtifact = {
    attempt: 1,
    fileSha256: sha256(readFileSync(input.planPath)),
  }
  const second = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: '9'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => new Date('2026-08-12T10:00:45.000Z'),
  })
  assert.equal(second.created, true)
  assert.equal(second.authorization.task.revision, 7)
  assert.equal(second.authorization.seed.inventoryDigest, '9'.repeat(64))
  assert.notEqual(second.authorization.authorizationId, first.authorization.authorizationId)
})

await check('tampered_stage_receipt_cannot_consume_a_live_authorization', () => {
  assert.equal(typeof authorizationModule.consumeNativeExecutionAuthorization, 'function')
  const input = fixture()
  const issued = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const committed = writeConsumedWidthReceipt(input, issued)
  const tampered = JSON.parse(readFileSync(committed.receiptPath, 'utf8'))
  tampered.postInventoryDigest = '7'.repeat(64)
  writeFileSync(committed.receiptPath, `${JSON.stringify(tampered, null, 2)}\n`, 'utf8')
  assert.throws(
    () => authorizationModule.consumeNativeExecutionAuthorization({
      attemptDir: input.attemptDir,
      planPath: input.planPath,
      task: input.task,
      workerId: input.workerId,
      tool: input.tool,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      authorizationPath: issued.authorizationPath,
      receiptPath: committed.receiptPath,
      expectedPhase: 'dimensions',
      expectedPostInventoryDigest: '9'.repeat(64),
      clock: () => new Date('2026-08-12T10:00:40.000Z'),
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_RECEIPT_INVALID',
  )
  assert.equal(existsSync(issued.authorizationPath), true)
})

await check('forged_predecessor_receipt_hash_cannot_consume_a_live_authorization', () => {
  const input = fixture()
  const issued = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const committed = writeConsumedWidthReceipt(input, issued)
  const receipt = JSON.parse(readFileSync(committed.receiptPath, 'utf8'))
  receipt.predecessorReceiptSha256 = '8'.repeat(64)
  writeFileSync(committed.receiptPath, `${JSON.stringify(receipt, null, 2)}\n`, 'utf8')
  assert.throws(
    () => authorizationModule.consumeNativeExecutionAuthorization({
      attemptDir: input.attemptDir,
      planPath: input.planPath,
      task: input.task,
      workerId: input.workerId,
      tool: input.tool,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      authorizationPath: issued.authorizationPath,
      receiptPath: committed.receiptPath,
      expectedPhase: 'dimensions',
      expectedPostInventoryDigest: '9'.repeat(64),
      clock: () => new Date('2026-08-12T10:00:40.000Z'),
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_RECEIPT_INVALID',
  )
  assert.equal(existsSync(issued.authorizationPath), true)
})

await check('failed_stage_authorization_is_contained_without_a_success_receipt', () => {
  assert.equal(typeof authorizationModule.containFailedNativeExecutionAuthorization, 'function')
  const input = fixture()
  const issued = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const contained = authorizationModule.containFailedNativeExecutionAuthorization({
    attemptDir: input.attemptDir,
    planPath: input.planPath,
    task: input.task,
    workerId: input.workerId,
    tool: input.tool,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    authorizationPath: issued.authorizationPath,
    failedPhase: 'dimensions',
  })
  assert.equal(contained.contained, true)
  assert.equal(existsSync(issued.authorizationPath), false)
  assert.equal(existsSync(contained.archivedAuthorizationPath), true)
  assert.equal(contained.authorizationSha256, issued.sha256)
})

await check('failure_containment_refuses_an_existing_stage_receipt', () => {
  const input = fixture()
  const issued = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const receiptPath = resolve(input.attemptDir, 'evidence/width-dimensions.receipt.json')
  mkdirSync(dirname(receiptPath), { recursive: true })
  writeFileSync(receiptPath, '{}\n', 'utf8')
  assert.throws(
    () => authorizationModule.containFailedNativeExecutionAuthorization({
      attemptDir: input.attemptDir,
      planPath: input.planPath,
      task: input.task,
      workerId: input.workerId,
      tool: input.tool,
      seedInventoryDigest: 'F'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      authorizationPath: issued.authorizationPath,
      failedPhase: 'dimensions',
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_FAILURE_CONTAINMENT_REFUSED',
  )
  assert.equal(existsSync(issued.authorizationPath), true)
})

await check('continued_stage_authorization_rejects_a_rebound_plan_digest', () => {
  const input = fixture()
  const issued = issueNativeExecutionAuthorization({
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    clock: () => NOW,
  })
  const committed = writeConsumedWidthReceipt(input, issued)
  authorizationModule.consumeNativeExecutionAuthorization({
    attemptDir: input.attemptDir,
    planPath: input.planPath,
    task: input.task,
    workerId: input.workerId,
    tool: input.tool,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    phases: WIDTH_PHASES,
    authorizationPath: issued.authorizationPath,
    receiptPath: committed.receiptPath,
    expectedPhase: 'dimensions',
    expectedPostInventoryDigest: '9'.repeat(64),
    clock: () => new Date('2026-08-12T10:00:40.000Z'),
  })
  const plan = JSON.parse(readFileSync(input.planPath, 'utf8'))
  plan.task.digest = '0'.repeat(64)
  writeFileSync(input.planPath, `${JSON.stringify(plan, null, 2)}\n`, 'utf8')
  input.task.revision += 2
  input.task.status = 'native_task_building'
  input.task.nativeBuild.state = 'building'
  input.task.nativeBuild.planArtifact = {
    attempt: 1,
    fileSha256: sha256(readFileSync(input.planPath)),
  }
  assert.throws(
    () => issueNativeExecutionAuthorization({
      ...input,
      seedInventoryDigest: '9'.repeat(64),
      attempt: 1,
      phases: WIDTH_PHASES,
      clock: () => new Date('2026-08-12T10:00:45.000Z'),
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_PLAN_BINDING_INVALID',
  )
})

await check('issues_exact_phase_contracts_for_all_native_stage_tools', () => {
  const input = fixture()
  const common = {
    attemptDir: input.attemptDir,
    planPath: input.planPath,
    task: input.task,
    workerId: input.workerId,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    clock: () => NOW,
  }
  const contracts = [
    ['native_seed_pack_888x14_v1', ['clone_native_seed']],
    ['native_width_888_v1', ['dimensions', 'derived', 'base-hole', 'assemblies']],
    ['native_door_module_888x14_v1', ['door_module_888x14']],
    ['native_lock_topology_888x14_v1', ['lock_topology_888x14']],
    ['native_root_assembly_888x14_v1', ['root_assembly_888x14']],
    ['native_final_pack_888x14_v1', ['final_pack_and_relocated_reopen']],
  ]
  for (let index = 0; index < contracts.length; index += 1) {
    const [id, phases] = contracts[index]
    const result = issueNativeExecutionAuthorization({
      ...common,
      tool: {
        id,
        sourceNormalizedSha256: String(index + 1).repeat(64),
        executableSha256: String(index + 2).repeat(64),
      },
      phases,
    })
    assert.deepEqual(result.authorization.execution.phases, phases)
    assert.equal(result.authorization.task.leaseExpiresAt, input.task.nativeBuild.lease.expiresAt)
  }
})

await check('rejects_unknown_tools_and_wrong_phase_contracts', () => {
  const input = fixture()
  const common = {
    ...input,
    seedInventoryDigest: 'F'.repeat(64),
    attempt: 1,
    clock: () => NOW,
  }
  assert.throws(
    () => issueNativeExecutionAuthorization({
      ...common,
      tool: { ...input.tool, id: 'unregistered_native_tool' },
      phases: ['dimensions'],
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_TOOL_NOT_REGISTERED',
  )
  assert.throws(
    () => issueNativeExecutionAuthorization({
      ...common,
      phases: ['assemblies'],
    }),
    (error) => error?.code === 'NATIVE_AUTHORIZATION_PHASE_CONTRACT_MISMATCH',
  )
})

await check('temporary_authorization_data_is_removed', () => {
  rmSync(TEMP_ROOT, { recursive: true, force: true })
  assert.equal(existsSync(TEMP_ROOT), false)
})

const failed = checks.filter((item) => !item.ok)
process.stdout.write(`${JSON.stringify({
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  checks,
}, null, 2)}\n`)
if (failed.length) process.exit(1)
