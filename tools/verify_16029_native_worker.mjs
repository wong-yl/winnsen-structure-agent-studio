import assert from 'node:assert/strict'
import { randomBytes } from 'node:crypto'
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  createNativeTaskStore,
  nativeCheckpointDescriptorDigest,
  NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
} from './lib/locker_16029_native_task_store.mjs'
import { NATIVE_EXECUTION_TOOL_CONTRACTS } from './lib/locker_16029_native_execution_authorization.mjs'
import {
  acquireNativeWorkerInstanceLock,
  runNativeWorkerFromCheckpoint,
  runNativeWorkerOnce,
} from './run_16029_native_worker.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const TEMP_ROOT = resolve(ROOT, 'tmp/verify_16029_native_worker')
const WORKER_PATH = resolve(ROOT, 'tools/run_16029_native_worker.mjs')

function freshDataDir(label) {
  const path = resolve(TEMP_ROOT, `${label}-${randomBytes(5).toString('hex')}`)
  mkdirSync(path, { recursive: true })
  return path
}

function portalNativeTask(reference, width, doorCount) {
  const createdAt = '2026-08-12T08:00:00.000Z'
  return {
    id: `NATIVE-${randomBytes(8).toString('hex').toUpperCase()}`,
    schema: 'winnsen.native_16029_request.v1',
    createdAt,
    updatedAt: createdAt,
    username: 'native-worker-verifier',
    requestFingerprint: randomBytes(32).toString('hex').toUpperCase(),
    customerRequirementReference: reference,
    candidateRecipeId: '',
    cabinetWidth: width,
    cabinetHeight: 1917,
    cabinetDepth: 550,
    columns: 2,
    doorCount,
    columnDoorCounts: [doorCount / 2, doorCount / 2],
    rowSequence: doorCount === 14 ? 'L1111111-R1111111' : '',
    doorWidth: (width - 126) / 2,
    previewDimensionsValidated: false,
    storageMode: 'task_file_source',
    status: 'native_task_created',
    taskType: 'native_solidworks_build_task',
    deliveryMode: 'native_task_pending',
    modelReady: false,
    engineeringAssistanceReady: false,
    legacyFallbackUsed: false,
    nativeBuild: {
      schema: 'winnsen.native_solidworks_build_task.v1',
      state: 'created',
      attempt: 0,
      createdAt,
      executionStarted: false,
      modelReady: false,
      legacyFallbackUsed: false,
      statusHistory: [{ state: 'created', at: createdAt }],
    },
  }
}

function nativeRequestDir(dataDir) {
  return resolve(dataDir, 'native_model_requests')
}

function planFiles(dataDir) {
  const attemptRoot = resolve(dataDir, 'native_model_requests/attempts')
  if (!existsSync(attemptRoot)) return []
  const files = []
  const visit = (folder) => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      const path = resolve(folder, entry.name)
      if (entry.isDirectory()) visit(path)
      else if (entry.name === 'native_build_plan.json') files.push(path)
    }
  }
  visit(attemptRoot)
  return files
}

function trustedToolchainHashes() {
  return Object.freeze(Object.fromEntries(Object.keys(NATIVE_EXECUTION_TOOL_CONTRACTS)
    .map((id, index) => [id, String(index + 1).repeat(64)])))
}

const STAGE_ORDER = Object.freeze([
  'clone_native_seed',
  'dimensions',
  'derived',
  'base-hole',
  'assemblies',
  'door_module_888x14',
  'lock_topology_888x14',
  'root_assembly_888x14',
  'final_pack_and_relocated_reopen',
])

function testDigest(index) {
  return String((index % 9) + 1).repeat(64).toUpperCase()
}

function checkpointDescriptor(taskId, planWorkerId, completedPhase = 'assemblies') {
  const door = completedPhase === 'door_module_888x14'
  return Object.freeze({
    schema: NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
    taskId,
    attempt: 1,
    planSha256: testDigest(8),
    planWorkerId,
    completedPhase,
    completedReceiptPath: door
      ? 'receipts/door_module_888x14.json'
      : 'evidence/width-assemblies.receipt.json',
    completedReceiptSha256: door ? testDigest(7) : testDigest(6),
    completedEvidencePath: door
      ? 'evidence/door_module_888x14.result.v1.json'
      : 'evidence/width-assemblies.json',
    completedEvidenceSha256: door ? testDigest(6) : testDigest(5),
    currentInventoryDigest: door ? testDigest(5) : testDigest(4),
    workingPackRelativePath: 'native_cad/working_pack',
    prefixReceiptChainSha256: door ? testDigest(4) : testDigest(3),
    checkpointAuditPath: 'diagnostics/assemblies-r22-runtime-audit.json',
    checkpointAuditSha256: testDigest(2),
    toolchainManifests: trustedToolchainHashes(),
  })
}

function checkpointPrefix(completedPhase = 'assemblies') {
  const last = STAGE_ORDER.indexOf(completedPhase)
  return Object.freeze(STAGE_ORDER.slice(0, last + 1).map((phase, index) => Object.freeze({
    phase,
    receipt: Object.freeze({ phase, path: `receipt-${phase}.json`, sha256: testDigest(index + 1) }),
    evidence: Object.freeze({ path: `evidence-${phase}.json`, sha256: testDigest(index + 2) }),
    postInventoryDigest: phase === completedPhase
      ? (completedPhase === 'door_module_888x14' ? testDigest(5) : testDigest(4))
      : testDigest(index + 3),
  })))
}

function leaseSpy(store) {
  let unitActive = false
  const calls = []
  const wrappedStore = Object.fromEntries(Object.entries(store).map(([key, value]) => [
    key,
    key === 'heartbeat'
      ? async (...args) => {
        assert.equal(unitActive, false, 'worker must not task-heartbeat inside a running unit')
        calls.push({ operation: 'heartbeat', args: args[0] })
        return value.apply(store, args)
      }
      : typeof value === 'function' ? value.bind(store) : value,
  ]))
  return {
    store: wrappedStore,
    calls,
    enterUnit() { unitActive = true },
    leaveUnit() { unitActive = false },
  }
}

const checks = []
async function check(name, action) {
  try {
    await action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({
      name,
      ok: false,
      error: error instanceof Error ? `${error.code ? `${error.code}: ` : ''}${error.message}` : String(error),
    })
  }
}

rmSync(TEMP_ROOT, { recursive: true, force: true })
mkdirSync(TEMP_ROOT, { recursive: true })

await check('checkpoint_validation_failure_never_borrows_another_workers_lease', async () => {
  for (const state of ['building', 'cancel_requested']) {
    const task = { id: 'NATIVE-FOREIGN-LEASE', revision: 8, nativeBuild: { state, attempt: 1, lease: { id: 'foreign-lease', workerId: 'foreign-worker' } } }
    const before = structuredClone(task)
    const mutations = []
    const originalError = Object.assign(new Error('invalid checkpoint before acquiring a lease'), { code: 'native_checkpoint_invalid' })
    await assert.rejects(runNativeWorkerFromCheckpoint({
      taskId: task.id, workerId: 'resume-worker', instanceLock: false,
      store: {
        async read() { return structuredClone(task) },
        async fail(args) { mutations.push(['fail', args]); return task },
        async cancel(args) { mutations.push(['cancel', args]); return task },
        async transition(args) { mutations.push(['transition', args]); return task },
      },
      async checkpointLoader() { throw originalError },
    }), error => error === originalError)
    assert.deepEqual(mutations, [])
    assert.deepEqual(task, before)
  }
})

await check('empty_store_ignores_data_root_json_and_returns_idle', async () => {
  const dataDir = freshDataDir('empty')
  writeFileSync(
    resolve(dataDir, 'unrelated-root-task.json'),
    `${JSON.stringify(portalNativeTask('data根旁置任务不得被worker扫描', 888, 14), null, 2)}\n`,
    'utf8',
  )
  const result = await runNativeWorkerOnce({ dataDir, workerId: 'verify-empty-worker' })
  assert.equal(result.status, 'idle')
  assert.equal(result.claimed, false)
  assert.deepEqual(planFiles(dataDir), [])
  const store = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  assert.equal((await store.list()).length, 0)
})

await check('checkpoint_catch_requires_the_exact_lease_returned_by_atomic_resume', async () => {
  for (const replacement of [null, { id: 'replacement-lease', workerId: 'resume-worker' }, { id: 'replacement-lease', workerId: 'another-worker' }]) {
    let task = { id: 'NATIVE-LEASE-AFTER-RESUME', revision: 1, nativeBuild: { state: 'cancelled', attempt: 1, lease: null } }
    const descriptor = checkpointDescriptor(task.id, 'resume-worker')
    const ownLease = { id: 'new-owned-lease', workerId: 'resume-worker' }
    const failure = new Error('checkpoint changed after atomic resume')
    const mutations = []
    let loaderCount = 0
    const operation = runNativeWorkerFromCheckpoint({
      taskId: task.id, workerId: 'resume-worker', instanceLock: false,
      store: {
        async read() { return structuredClone(task) },
        async resumeCancelledFromCheckpoint() { task = { ...task, revision: 2, nativeBuild: { ...task.nativeBuild, state: 'building', lease: ownLease } }; return structuredClone(task) },
        async fail(args) { mutations.push(args); task.nativeBuild.state = 'failed'; return structuredClone(task) },
      },
      async checkpointLoader() {
        if (loaderCount++ === 0) return { descriptor, checkpointDigest: nativeCheckpointDescriptorDigest(descriptor), prefixStageResults: checkpointPrefix(), currentInventoryDigest: descriptor.currentInventoryDigest }
        if (replacement) task.nativeBuild.lease = replacement
        throw failure
      },
    })
    if (replacement) {
      await assert.rejects(operation, error => error === failure)
      assert.deepEqual(mutations, [])
      assert.equal(task.nativeBuild.state, 'building')
    } else {
      assert.equal((await operation).status, 'failed')
      assert.equal(mutations.length, 1)
      assert.equal(mutations[0].leaseId, ownLease.id)
      assert.equal(mutations[0].workerId, ownLease.workerId)
    }
  }
})

await check('worker_instance_lock_prevents_a_second_owner', async () => {
  const dataDir = freshDataDir('instance-lock')
  const first = acquireNativeWorkerInstanceLock({ dataDir, workerId: 'verify-lock-owner' })
  try {
    assert.throws(
      () => acquireNativeWorkerInstanceLock({ dataDir, workerId: 'verify-lock-contender' }),
      (error) => error?.code === 'native_worker_already_active',
    )
    const heartbeat = first.heartbeat()
    assert.equal(heartbeat.workerId, 'verify-lock-owner')
  } finally {
    assert.equal(first.release(), true)
  }
  const second = acquireNativeWorkerInstanceLock({ dataDir, workerId: 'verify-lock-contender' })
  assert.equal(second.release(), true)
})

await check('checkpoint_worker_runs_only_door_then_commits_a_new_cancelled_checkpoint', async () => {
  const dataDir = freshDataDir('checkpoint-door-stop')
  const taskStore = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir), leaseMs: 30 * 60 * 1000 })
  const planWorkerId = 'checkpoint-plan-worker'
  const input = portalNativeTask('checkpoint continuation runs door only', 888, 14)
  input.status = 'native_task_cancelled'
  input.nativeBuild = {
    ...input.nativeBuild,
    state: 'cancelled',
    attempt: 1,
    lease: null,
    planArtifact: {
      attempt: 1,
      fileName: 'native_build_plan.json',
      fileSha256: testDigest(8),
      immutable: true,
      recipeId: 'winnsen-16029-888w-14door-native-v1',
      recipeVersion: 1,
      recipeDigest: 'f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8',
    },
    modelReady: false,
    result: null,
  }
  const created = await taskStore.createOrImport(input)
  const assembliesDescriptor = checkpointDescriptor(created.id, planWorkerId, 'assemblies')
  const doorDescriptor = checkpointDescriptor(created.id, planWorkerId, 'door_module_888x14')
  const loaderCalls = []
  const phasesRun = []
  const units = STAGE_ORDER.map((phase) => Object.freeze({ receiptPhase: phase, id: `unit-${phase}` }))
  const result = await runNativeWorkerFromCheckpoint({
    dataDir,
    taskId: created.id,
    completedPhase: 'assemblies',
    stopAfterPhase: 'door_module_888x14',
    workerId: planWorkerId,
    store: taskStore,
    instanceLock: false,
    clock: () => new Date('2026-08-12T09:00:00.000Z'),
    checkpointLoader(args) {
      loaderCalls.push({ completedPhase: args.completedPhase, advance: args.allowCheckpointAdvance === true })
      const door = args.completedPhase === 'door_module_888x14'
      const descriptor = door ? doorDescriptor : assembliesDescriptor
      return Object.freeze({
        descriptor,
        checkpointDigest: nativeCheckpointDescriptorDigest(descriptor),
        prefixStageResults: checkpointPrefix(args.completedPhase),
        currentInventoryDigest: descriptor.currentInventoryDigest,
        attemptDir: resolve(dataDir, 'native_model_requests', 'attempts', created.id, 'attempt-0001'),
        plan: Object.freeze({ schema: 'checkpoint-plan-fixture' }),
      })
    },
    toolchainArtifactResolver() { return trustedToolchainHashes() },
    executionBlueprintBuilder() { return Object.freeze({ units }) },
    async prepareDoorInputs(args) {
      assert.equal(args.task.nativeBuild.state, 'building')
      assert.equal(args.workerId, planWorkerId)
      return Object.freeze({ prepared: true })
    },
    async runStageUnit(args) {
      phasesRun.push(args.phase)
      assert.equal(args.phase, 'door_module_888x14')
      assert.equal(args.preInventoryDigest, assembliesDescriptor.currentInventoryDigest)
      return Object.freeze({
        receipt: Object.freeze({ phase: args.phase, path: 'receipts/door_module_888x14.json', sha256: testDigest(7) }),
        evidence: Object.freeze({ path: 'evidence/door_module_888x14.result.v1.json', sha256: testDigest(6) }),
        postInventoryDigest: doorDescriptor.currentInventoryDigest,
      })
    },
    async runLockValidation() { assert.fail('lock validation must not run after the door stop checkpoint') },
    async publishNativeModel() { assert.fail('publication must not run after the door stop checkpoint') },
  })
  assert.equal(result.status, 'cancelled', JSON.stringify({ code: result.code, error: result.task?.nativeBuild?.error }))
  assert.deepEqual(phasesRun, ['door_module_888x14'])
  assert.deepEqual(loaderCalls, [
    { completedPhase: 'assemblies', advance: false },
    { completedPhase: 'assemblies', advance: false },
    { completedPhase: 'door_module_888x14', advance: true },
  ])
  const persisted = await taskStore.read(created.id)
  assert.equal(persisted.nativeBuild.state, 'cancelled')
  assert.equal(persisted.nativeBuild.lease, null)
  assert.equal(persisted.modelReady, false)
  assert.equal(persisted.nativeBuild.modelReady, false)
  assert.equal(persisted.legacyFallbackUsed, false)
  assert.deepEqual(persisted.nativeBuild.checkpoint, doorDescriptor)
  assert.equal(persisted.nativeBuild.checkpointDigest, nativeCheckpointDescriptorDigest(doorDescriptor))
  assert.equal(persisted.nativeBuild.statusHistory.some((row) => row.operation === 'checkpoint_resume'), true)
  assert.equal(persisted.nativeBuild.statusHistory.at(-1).to, 'cancelled')
})

await check('checkpoint_worker_rejects_wrong_worker_before_resuming_or_running_a_stage', async () => {
  const dataDir = freshDataDir('checkpoint-wrong-worker')
  const taskStore = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  const input = portalNativeTask('wrong checkpoint worker must fail closed', 888, 14)
  input.status = 'native_task_cancelled'
  input.nativeBuild = {
    ...input.nativeBuild,
    state: 'cancelled', attempt: 1, lease: null, modelReady: false, result: null,
    planArtifact: { attempt: 1, fileName: 'native_build_plan.json', fileSha256: testDigest(8) },
  }
  const created = await taskStore.createOrImport(input)
  const descriptor = checkpointDescriptor(created.id, 'immutable-plan-worker', 'assemblies')
  await assert.rejects(() => runNativeWorkerFromCheckpoint({
    dataDir,
    taskId: created.id,
    workerId: 'different-worker',
    store: taskStore,
    instanceLock: false,
    checkpointLoader() {
      return {
        descriptor,
        checkpointDigest: nativeCheckpointDescriptorDigest(descriptor),
        prefixStageResults: checkpointPrefix('assemblies'),
        currentInventoryDigest: descriptor.currentInventoryDigest,
      }
    },
    async runStageUnit() { assert.fail('stage must not run with a mismatched worker') },
  }), (error) => error?.code === 'native_checkpoint_worker_mismatch')
  assert.equal((await taskStore.read(created.id)).nativeBuild.state, 'cancelled')
})

await check('trusted_888x14_task_runs_nine_receipt_units_and_completes', async () => {
  const dataDir = freshDataDir('trusted-888')
  const taskStore = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir), leaseMs: 30 * 60 * 1000 })
  const created = await taskStore.createOrImport(portalNativeTask('888W 14-door native worker execution contract', 888, 14))
  const lease = leaseSpy(taskStore)
  const events = []
  let currentDigest = testDigest(0)
  const blueprint = Object.freeze({ contract: 'verify-native-worker-blueprint-v1' })
  const result = await runNativeWorkerOnce({
    dataDir,
    store: lease.store,
    workerId: 'verify-trusted-worker',
    toolchainArtifactResolver() {
      events.push('toolchain')
      return trustedToolchainHashes()
    },
    executionBlueprintBuilder(args) {
      assert.equal(args.task.id, created.id)
      assert.equal(args.recipe.id, 'winnsen-16029-888w-14door-native-v1')
      assert.deepEqual(args.planArtifact.qualityBoundary, {
        planningOnly: true,
        previewDimensionsValidated: false,
        engineeringAssistanceReady: false,
        readyOnlyAfterEveryRequiredCheckPasses: true,
      })
      assert.deepEqual(args.planArtifact.executionBoundary, {
        executorImplemented: false,
        cadStarted: false,
        modelGenerated: false,
        modelReady: false,
        legacyFallbackUsed: false,
      })
      events.push('blueprint')
      return blueprint
    },
    inventoryDigest() {
      events.push(`digest:${currentDigest}`)
      return currentDigest
    },
    async prepareDoorInputs(args) {
      assert.equal(args.task.id, created.id)
      assert.equal(args.lease.id.length > 0, true)
      assert.equal(args.blueprint, blueprint)
      events.push('prepare-door-inputs')
      return Object.freeze({ prepared: true, inputDigest: testDigest(4) })
    },
    async runStageUnit(args) {
      const index = STAGE_ORDER.indexOf(args.phase)
      assert.notEqual(index, -1)
      assert.equal(args.task.id, created.id)
      assert.equal(args.blueprint, blueprint)
      assert.equal(args.preInventoryDigest, currentDigest)
      assert.equal(args.lease.id.length > 0, true)
      if (args.phase === 'door_module_888x14') {
        assert.equal(events.includes('prepare-door-inputs'), true)
        assert.deepEqual(args.doorInputs, { prepared: true, inputDigest: testDigest(4) })
      }
      lease.enterUnit()
      await Promise.resolve()
      lease.leaveUnit()
      const postInventoryDigest = testDigest(index + 1)
      events.push(`unit:${args.phase}`)
      currentDigest = postInventoryDigest
      return {
        receipt: { phase: args.phase, sha256: testDigest(index + 2), path: `receipts/${args.phase}.json` },
        evidence: { sha256: testDigest(index + 3), path: `evidence/${args.phase}.json` },
        postInventoryDigest,
      }
    },
    async runLockValidation(args) {
      assert.equal(args.task.id, created.id)
      assert.equal(args.blueprint, blueprint)
      assert.equal(args.preInventoryDigest, testDigest(8))
      assert.equal(args.receipts.length, 8)
      assert.equal(args.receipts.at(-1).phase, 'root_assembly_888x14')
      events.push('lock-validation')
      return { finalEvidence: { path: 'evidence/lock-validation.json', sha256: testDigest(7) } }
    },
    async publishNativeModel(args) {
      assert.equal((await taskStore.read(created.id)).nativeBuild.state, 'validating')
      assert.equal(args.task.nativeBuild.state, 'validating')
      assert.equal(args.blueprint, blueprint)
      assert.equal(args.recipe.id, 'winnsen-16029-888w-14door-native-v1')
      assert.equal(args.stageResults.length, STAGE_ORDER.length)
      assert.deepEqual(args.finalValidationEvidence,
        { path: 'evidence/lock-validation.json', sha256: testDigest(7) })
      events.push('publish')
      return {
        manifest: { path: 'publications/package_manifest.json', sha256: testDigest(20) },
        archive: {
          path: 'publications/16029_888W_14door_structure_assistance.zip',
          fileName: '16029_888W_14door_structure_assistance.zip',
          sha256: testDigest(21),
          sizeBytes: 24535798,
          entryCount: 55,
          rootDirectory: '16029_888W_14door_NATIVE-888-14',
        },
      }
    },
  })

  assert.equal(result.status, 'completed')
  assert.equal(result.task.id, created.id)
  assert.equal(result.task.nativeBuild.state, 'ready')
  assert.equal(result.task.nativeBuild.lease, null)
  assert.equal(result.task.nativeBuild.modelReady, true)
  assert.equal(result.task.nativeBuild.statusHistory.some((item) => item.to === 'claimed'), true)
  assert.equal(result.task.nativeBuild.statusHistory.some((item) => item.to === 'planning'), true)
  assert.equal(result.task.nativeBuild.statusHistory.some((item) => item.to === 'building'), true)
  assert.equal(result.task.nativeBuild.statusHistory.some((item) => item.to === 'validating'), true)
  assert.equal(result.task.nativeBuild.statusHistory.at(-1).to, 'ready')
  assert.deepEqual(events.filter((event) => event.startsWith('unit:')).map((event) => event.slice(5)), STAGE_ORDER)
  assert.equal(events.indexOf('prepare-door-inputs') < events.indexOf('unit:door_module_888x14'), true)
  assert.equal(events.indexOf('lock-validation') > events.indexOf('unit:root_assembly_888x14'), true)
  assert.equal(events.indexOf('lock-validation') < events.indexOf('unit:final_pack_and_relocated_reopen'), true)
  assert.equal(events.indexOf('publish') > events.indexOf('unit:final_pack_and_relocated_reopen'), true)
  assert.equal(lease.calls.length >= STAGE_ORDER.length, true)

  const persisted = await taskStore.read(created.id)
  assert.equal(persisted.nativeBuild.state, 'ready')
  assert.deepEqual(Object.keys(persisted.nativeBuild.result).sort(), [
    'archive', 'finalReceipt', 'packageManifest', 'schema', 'validationEvidence',
  ])
  assert.deepEqual(persisted.nativeBuild.result.validationEvidence,
    { path: 'evidence/lock-validation.json', sha256: testDigest(7) })
  assert.equal(persisted.nativeBuild.result.finalReceipt.path,
    'receipts/final_pack_and_relocated_reopen.json')
  assert.deepEqual(persisted.nativeBuild.result.packageManifest,
    { path: 'publications/package_manifest.json', sha256: testDigest(20) })
  assert.deepEqual(persisted.nativeBuild.result.archive, {
    path: 'publications/16029_888W_14door_structure_assistance.zip',
    fileName: '16029_888W_14door_structure_assistance.zip',
    sha256: testDigest(21),
    sizeBytes: 24535798,
  })
})

await check('unit_failure_calls_store_fail_and_never_marks_task_ready', async () => {
  const dataDir = freshDataDir('unit-failure')
  const store = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  const created = await store.createOrImport(portalNativeTask('native worker stage failure contract', 888, 14))
  const result = await runNativeWorkerOnce({
    dataDir,
    store,
    workerId: 'verify-failing-unit-worker',
    toolchainArtifactResolver: trustedToolchainHashes,
    executionBlueprintBuilder: () => ({ contract: 'failing-blueprint' }),
    inventoryDigest: () => testDigest(0),
    prepareDoorInputs: async () => ({ prepared: true }),
    runStageUnit: async ({ phase }) => {
      const error = new Error(`fake unit failed: ${phase}`)
      error.code = 'FAKE_UNIT_FAILURE'
      throw error
    },
    runLockValidation: async () => assert.fail('lock validation must not run after a unit failure'),
  })
  assert.equal(result.status, 'failed')
  assert.equal(result.code, 'FAKE_UNIT_FAILURE')
  assert.equal(result.task.id, created.id)
  assert.equal(result.task.nativeBuild.state, 'failed')
  assert.equal(result.task.nativeBuild.lease, null)
  assert.equal(result.task.modelReady, false)
  assert.equal(result.task.nativeBuild.modelReady, false)
  assert.equal(result.task.nativeBuild.error.code, 'FAKE_UNIT_FAILURE')
  const persisted = await store.read(created.id)
  assert.equal(persisted.nativeBuild.state, 'failed')
  assert.equal(persisted.modelReady, false)
})

await check('publication_failure_never_marks_the_generated_cad_task_ready', async () => {
  const dataDir = freshDataDir('publication-failure')
  const store = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  const created = await store.createOrImport(portalNativeTask('native publication failure contract', 888, 14))
  let currentDigest = testDigest(0)
  const result = await runNativeWorkerOnce({
    dataDir,
    store,
    workerId: 'verify-publication-failure-worker',
    toolchainArtifactResolver: trustedToolchainHashes,
    executionBlueprintBuilder: () => ({ contract: 'publication-failure-blueprint' }),
    inventoryDigest: () => currentDigest,
    prepareDoorInputs: async () => ({ prepared: true }),
    runStageUnit: async ({ phase }) => {
      const index = STAGE_ORDER.indexOf(phase)
      currentDigest = testDigest(index + 1)
      return {
        receipt: { phase, path: `receipts/${phase}.json`, sha256: testDigest(index + 2) },
        evidence: { path: `evidence/${phase}.json`, sha256: testDigest(index + 3) },
        postInventoryDigest: currentDigest,
      }
    },
    runLockValidation: async () => ({
      finalEvidence: { path: 'evidence/lock-validation.json', sha256: testDigest(7) },
    }),
    publishNativeModel: async () => {
      const error = new Error('fixture publication gate failed')
      error.code = 'NATIVE_PUBLICATION_FIXTURE_FAILED'
      throw error
    },
  })
  assert.equal(result.status, 'failed')
  assert.equal(result.code, 'NATIVE_PUBLICATION_FIXTURE_FAILED')
  assert.equal(result.task.id, created.id)
  assert.equal(result.task.nativeBuild.state, 'failed')
  assert.equal(result.task.nativeBuild.modelReady, false)
  assert.equal(Boolean(result.task.nativeBuild.result), false)
})


await check('cancel_requested_during_an_issued_unit_waits_for_that_unit_then_stops', async () => {
  const dataDir = freshDataDir('cancel-after-consume')
  const store = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  const created = await store.createOrImport(portalNativeTask('native worker cancellation boundary', 888, 14))
  const calls = []
  const result = await runNativeWorkerOnce({
    dataDir,
    store,
    workerId: 'verify-cancel-worker',
    toolchainArtifactResolver: trustedToolchainHashes,
    executionBlueprintBuilder: () => ({ contract: 'cancel-blueprint' }),
    inventoryDigest: () => testDigest(0),
    prepareDoorInputs: async () => assert.fail('door preparation must not run after the first-unit cancellation'),
    runStageUnit: async (args) => {
      calls.push(`issued:${args.phase}`)
      assert.equal(args.phase, 'clone_native_seed')
      await store.cancel({
        taskId: args.task.id,
        username: args.task.username,
        expectedRevision: args.task.revision,
        transitionId: `cancel-during-${args.phase}`,
        reason: 'test cancellation after authorization issue',
      })
      calls.push(`consumed:${args.phase}`)
      return {
        receipt: { phase: args.phase, sha256: testDigest(1), path: `receipts/${args.phase}.json` },
        evidence: { sha256: testDigest(2), path: `evidence/${args.phase}.json` },
        postInventoryDigest: testDigest(1),
      }
    },
    runLockValidation: async () => assert.fail('lock validation must not run after cancellation'),
  })
  assert.deepEqual(calls, ['issued:clone_native_seed', 'consumed:clone_native_seed'])
  assert.equal(result.status, 'cancelled')
  assert.equal(result.task.nativeBuild.state, 'cancelled')
  assert.equal(result.task.nativeBuild.lease, null)
  assert.equal(result.task.modelReady, false)
  const persisted = await store.read(created.id)
  assert.equal(persisted.nativeBuild.state, 'cancelled')
})

await check('missing_toolchain_blocks_before_plan_is_frozen', async () => {
  const dataDir = freshDataDir('missing-toolchain')
  const store = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  const created = await store.createOrImport(portalNativeTask('验证工具清单缺失必须安全停止', 888, 14))
  const result = await runNativeWorkerOnce({
    dataDir,
    workerId: 'verify-missing-toolchain-worker',
    toolchainArtifactResolver() {
      const error = new Error('manifest missing')
      error.code = 'NATIVE_TOOLCHAIN_MANIFEST_MISSING'
      throw error
    },
  })
  assert.equal(result.status, 'blocked')
  assert.equal(result.code, 'native_toolchain_not_ready')
  assert.equal(result.task.id, created.id)
  assert.deepEqual(planFiles(dataDir), [])
})

await check('unknown_recipe_blocks_without_plan_or_model', async () => {
  const dataDir = freshDataDir('unknown')
  const store = createNativeTaskStore({ dataDir: nativeRequestDir(dataDir) })
  const created = await store.createOrImport(portalNativeTask('验证未知可信配方', 889, 14))
  const result = await runNativeWorkerOnce({ dataDir, workerId: 'verify-unknown-worker' })
  assert.equal(result.status, 'blocked')
  assert.equal(result.code, 'native_recipe_not_available')
  assert.equal(result.task.id, created.id)
  assert.equal(result.task.status, 'native_task_blocked')
  assert.equal(result.task.nativeBuild.state, 'blocked')
  assert.equal(result.task.nativeBuild.blocker.code, 'native_recipe_not_available')
  assert.equal(result.task.nativeBuild.execution.cadStarted, false)
  assert.equal(result.task.nativeBuild.execution.modelGenerated, false)
  assert.equal(result.task.nativeBuild.execution.modelReady, false)
  assert.equal(result.task.nativeBuild.execution.legacyFallbackUsed, false)
  assert.equal(result.task.nativeBuild.lease, null)
  assert.deepEqual(planFiles(dataDir), [])
  assert.equal(Boolean(result.task.downloadUrl), false)
})

await check('worker_source_has_no_legacy_or_process_execution_surface', async () => {
  const source = readFileSync(WORKER_PATH, 'utf8')
  for (const forbidden of [
    "from 'node:child_process'",
    'spawn(',
    'exec(',
    'execFile(',
    'process_16029_review_generation_queue',
    'generate_review_solidworks_full_assembly',
    'generate_review_solidworks_single_door',
    'locker_16029_native_generator',
    'locker_16029_controlled_generation_policy',
    'generation-download',
    'SLDWORKS.exe',
    'command:',
    'script:',
    'productionReady',
    'releaseReady',
  ]) {
    assert.equal(source.includes(forbidden), false, forbidden)
  }
  assert.equal(source.includes('createServer'), false)
  assert.equal(source.includes('.listen('), false)
  assert.equal(source.includes('resolveTrustedNativeRecipe(task)'), true)
  assert.equal(source.includes('buildTrustedNativePlan(task)'), true)
  for (const required of [
    'toolchainArtifactResolver',
    'executionBlueprintBuilder',
    'prepareDoorInputs',
    'runStageUnit',
    'runLockValidation',
    'inventoryDigest',
    "toState: 'building'",
    "toState: 'validating'",
    'taskStore.complete(',
    'taskStore.fail(',
  ]) assert.equal(source.includes(required), true, required)
  assert.equal(source.includes('native_recipe_executor_not_implemented'), false)
})

await check('temporary_worker_data_is_removed', async () => {
  rmSync(TEMP_ROOT, { recursive: true, force: true })
  assert.equal(existsSync(TEMP_ROOT), false)
})

const failed = checks.filter((item) => !item.ok)
const result = {
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  checks,
}
process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (failed.length) process.exit(1)
