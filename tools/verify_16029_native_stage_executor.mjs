import assert from 'node:assert/strict'
import { ChildProcess } from 'node:child_process'
import { createHash, randomBytes } from 'node:crypto'
import {
  appendFileSync,
  copyFileSync,
  existsSync,
  linkSync,
  mkdirSync,
  readFileSync,
  renameSync,
  rmSync,
  statSync,
  symlinkSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs'
import { dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as executor from './lib/locker_16029_native_stage_executor.mjs'
import { NATIVE_EXECUTION_TOOL_CONTRACTS } from './lib/locker_16029_native_execution_authorization.mjs'
import {
  NATIVE_STAGE_RECEIPT_ORDER,
  buildNativeStageReceiptContracts,
} from './lib/locker_16029_native_stage_contract.mjs'
import { resolveTrustedNativeToolchainArtifacts } from './lib/locker_16029_native_toolchain_manifests.mjs'
import { TRUSTED_NATIVE_RECIPE_REGISTRY } from './locker_16029_native_recipe_registry.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const TEMP_ROOT = resolve(ROOT,
  `tmp/verify-native-stage-executor-${process.pid}-${randomBytes(4).toString('hex')}`)
const SOURCE_DIGEST = '9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B'
const WIDTH_PHASES = Object.freeze(['dimensions', 'derived', 'base-hole', 'assemblies'])
const TOOL_IDS = Object.freeze(Object.keys(NATIVE_EXECUTION_TOOL_CONTRACTS))
const RECIPE = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
const FIXTURE_ONLY = process.argv.includes('--fixture-only')
const LIVE_EVIDENCE_CHECKS = new Set([
  'live_toolchain_has_pinned_solidworks_interop_runtime_dependencies',
  'shared_interop_runtime_rejects_a_modified_executable_config',
  'door_inputs_are_exact_isolated_native_sources',
  'door_input_hash_tamper_fails_before_sentinel_or_request_commit',
  'door_input_sentinel_is_create_new_and_never_overwritten',
  'door_input_transaction_rolls_back_second_copy_and_request_commit_failures',
  'door_input_transaction_never_deletes_foreign_collision_or_mutates_replaced_temp_target',
  'door_input_preparation_rejects_attempt_root_junction_itself',
])

const V37_PACK = resolve(ROOT,
  'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/native_cad/final_native_hierarchical_pack_and_go')
const DOOR_SOURCE_CONTRACTS = Object.freeze({
  left_panel_seed: Object.freeze({
    path: resolve(ROOT,
      'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图/储物柜门板1╱12.SLDPRT'),
    sha256: 'F34138A9BE009D5A0C68604F57B278584DB686623943C2DE319A2999ACA488A2',
    mustCopy: true,
  }),
  stiffener_seed: Object.freeze({
    path: resolve(ROOT,
      'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图/柜门加强筋2╱12.SLDPRT'),
    sha256: 'F4D4973F72CB604BF52BF802BC378E9BA042A4FE8034C196CA7666A964ACCB28',
    mustCopy: true,
  }),
  latch_plate: Object.freeze({
    path: resolve(V37_PACK, '插销固定板.SLDPRT'),
    sha256: '4B511C2FB2371060D998F2C0667C12980DF4A151E5CF4FDCD2934F7C7AAC1C5C',
  }),
  hook_pad: Object.freeze({
    path: resolve(V37_PACK, 'U型锁钩垫板.SLDPRT'),
    sha256: '93378BDFB9666CDE8379F59A0506E7A4508AFAEDAC47121C3B8CBDAE56480834',
  }),
  bushing: Object.freeze({
    path: resolve(V37_PACK, '塑料轴套(云绅模具).SLDPRT'),
    sha256: '930EE6070D278D5299D1EECED4F43367EE404A0E921517DE45297C30A8A12C7B',
  }),
  hinge_pin: Object.freeze({
    path: resolve(V37_PACK, '门轴销.SLDPRT'),
    sha256: 'B7289A146C9827050573FACEF66DCA06919FC41B943C39548AF536FE21516BC0',
  }),
  circlip: Object.freeze({
    path: resolve(V37_PACK, '开口挡圈5.SLDPRT'),
    sha256: '84ACBBAD72BC3DB175AD77C5DB5ED5F4A74410435C3CDEF1574B6A7808DCC754',
  }),
  mechanical_lock_tongue: Object.freeze({
    path: resolve(V37_PACK, '锁舌.SLDPRT'),
    sha256: '26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719',
  }),
})

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256(readFileSync(path))
}

function stageDigest(index) {
  return String((index % 9) + 1).repeat(64).toUpperCase()
}

function underRoot(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function requireExport(name) {
  assert.equal(typeof executor[name], 'function',
    `locker_16029_native_stage_executor.mjs must export ${name}()`)
  return executor[name]
}

let fixtureSerial = 0
function fixture() {
  fixtureSerial += 1
  const taskId = `NATIVE-88814-EXECUTOR-TEST-${fixtureSerial}`
  const fixtureRoot = resolve(TEMP_ROOT, `fixture-${fixtureSerial}`)
  const attemptDir = resolve(fixtureRoot, taskId, 'attempt-0001')
  const workingPack = resolve(attemptDir, 'native_cad/working_pack')
  mkdirSync(workingPack, { recursive: true })
  const workerId = 'native-worker-stage-executor-test'
  const task = {
    id: taskId,
    revision: 17,
    status: 'building',
    taskType: 'native_solidworks_build_task',
    requestFingerprint: 'A'.repeat(64),
    nativeBuild: {
      state: 'building',
      attempt: 1,
      lease: {
        id: `lease-${fixtureSerial}`,
        workerId,
        claimedAt: '2026-08-12T09:59:00.000Z',
        heartbeatAt: '2026-08-12T09:59:55.000Z',
        expiresAt: '2026-08-12T10:40:00.000Z',
      },
    },
  }
  const artifacts = {}
  for (const [index, id] of TOOL_IDS.entries()) {
    const toolDir = resolve(fixtureRoot, 'trusted-tools', id)
    const executablePath = resolve(toolDir, `${id}.exe`)
    const manifestPath = resolve(toolDir, 'toolchain_manifest.json')
    mkdirSync(toolDir, { recursive: true })
    writeFileSync(executablePath, `fixture executable ${id}\n`, 'utf8')
    writeFileSync(manifestPath, `{"tool":"${id}"}\n`, 'utf8')
    artifacts[id] = {
      id,
      manifestPath,
      manifestSha256: sha256File(manifestPath),
      tool: {
        id,
        sourceNormalizedSha256: String(index + 1).repeat(64),
        executablePath,
        executableSha256: sha256File(executablePath),
      },
      auxiliaries: {},
    }
  }
  const lockToolDir = dirname(artifacts.native_lock_topology_888x14_v1.tool.executablePath)
  const partInspectorPath = resolve(lockToolDir, 'InspectLockTopology888x14.exe')
  const tongueInspectorPath = resolve(lockToolDir, 'InspectAssemblyTongues888x14.exe')
  const validatorPath = resolve(lockToolDir, 'ValidateLockTopology888x14.mjs')
  writeFileSync(partInspectorPath, 'fixture lock inspector\n', 'utf8')
  writeFileSync(tongueInspectorPath, 'fixture tongue inspector\n', 'utf8')
  writeFileSync(validatorPath, 'export const fixture = true\n', 'utf8')
  artifacts.native_lock_topology_888x14_v1.auxiliaries = {
    native_lock_topology_inspector_v1: {
      id: 'native_lock_topology_inspector_v1',
      executablePath: partInspectorPath,
      executableSha256: sha256File(partInspectorPath),
      sourceNormalizedSha256: 'A'.repeat(64),
    },
    native_assembly_tongue_inspector_888x14_v1: {
      id: 'native_assembly_tongue_inspector_888x14_v1',
      executablePath: tongueInspectorPath,
      executableSha256: sha256File(tongueInspectorPath),
      sourceNormalizedSha256: 'B'.repeat(64),
    },
    validator: { path: validatorPath, sha256: sha256File(validatorPath) },
  }
  const trustedToolchainManifests = Object.fromEntries(
    Object.entries(artifacts).map(([id, row]) => [id, row.manifestSha256]))
  const plan = {
    schema: 'winnsen.native_build_plan.v1',
    createdAt: '2026-08-12T09:59:56.000Z',
    workerId,
    purpose: 'structure_engineering_assistance',
    task: { id: task.id, revisionAtPlanning: task.revision, digest: 'C'.repeat(64) },
    request: { fingerprint: task.requestFingerprint, digest: 'D'.repeat(64) },
    recipe: { id: RECIPE.id, version: RECIPE.version, digest: 'F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8' },
    geometry: {
      cabinetWidthMm: 888,
      cabinetHeightMm: 1917,
      cabinetDepthMm: 550,
      columns: 2,
      doorCount: 14,
      columnDoorCounts: [7, 7],
      rowSequence: 'L1111111-R1111111',
      doorPanelWidthMm: 381,
      doorPanelHeightMm: 1781 / 7,
    },
    stageContracts: [],
    stageReceiptContracts: buildNativeStageReceiptContracts(RECIPE),
    trustedToolchainManifests,
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
  const blueprint = executor.buildNative888x14ExecutionBlueprint({
    attemptDir,
    planPath,
    taskId,
    repositoryRoot: ROOT,
    toolchainArtifacts: artifacts,
  })
  return { fixtureRoot, attemptDir, workingPack, planPath, plan, task, taskId, workerId, artifacts, blueprint }
}

function copyWorkingPackDoorSources(input) {
  for (const row of Object.values(DOOR_SOURCE_CONTRACTS).filter((source) => !source.mustCopy)) {
    const target = resolve(input.workingPack, row.path.slice(row.path.lastIndexOf('\\') + 1))
    copyFileSync(row.path, target)
    assert.equal(sha256File(target), row.sha256)
  }
}

function stageUnit(blueprint, receiptPhase) {
  assert.ok(Array.isArray(blueprint.units), 'blueprint.units must be an array')
  const unit = blueprint.units.find((row) => row.receiptPhase === receiptPhase)
  assert.ok(unit, `missing execution unit for ${receiptPhase}`)
  return unit
}

function fakeAuthorizationHarness(log) {
  let serial = 0
  const issueCalls = []
  const consumeCalls = []
  const containCalls = []
  return {
    issueCalls,
    consumeCalls,
    containCalls,
    issueAuthorization(options) {
      serial += 1
      issueCalls.push(options)
      log?.push(`issue:${options.phases.join(',')}`)
      const authorizationId = `native-auth-fixture-${serial}`
      const authorizationPath = resolve(options.attemptDir,
        `execution_authorizations/${options.tool.id}-${serial}.json`)
      return {
        created: true,
        authorizationPath,
        sha256: String(serial).padStart(64, '0'),
        authorization: {
          authorizationId,
          plan: { sha256: 'E'.repeat(64) },
          execution: { phases: [...options.phases] },
        },
      }
    },
    consumeAuthorization(options) {
      consumeCalls.push(options)
      log?.push(`consume:${options.expectedPhase}`)
      return { consumed: true, expectedPhase: options.expectedPhase }
    },
    containAuthorization(options) {
      containCalls.push(options)
      log?.push(`contain:${options.failedPhase}`)
      return { contained: true, failedPhase: options.failedPhase }
    },
  }
}

function runUnitOptions(input, unit, harness, overrides = {}) {
  return {
    blueprint: input.blueprint,
    unit,
    task: input.task,
    workerId: input.workerId,
    attempt: 1,
    seedInventoryDigest: SOURCE_DIGEST,
    preInventoryDigest: SOURCE_DIGEST,
    toolchainArtifacts: input.artifacts,
    issueAuthorization: harness.issueAuthorization,
    consumeAuthorization: harness.consumeAuthorization,
    containAuthorization: harness.containAuthorization,
    inventoryDigest: () => '9'.repeat(64),
    validateRuntimeDependencies: () => ({ ok: true }),
    isCancellationRequested: () => false,
    onInstanceLockHeartbeat: () => {},
    ...overrides,
  }
}

function writeLockValidationFixture(input, {
  status = 'PASS', forgeBuildHash = false, mutateOutput = (output) => output,
} = {}) {
  const evidenceDir = resolve(input.attemptDir, 'evidence')
  const buildPath = resolve(evidenceDir, 'lock_topology_888x14.v2.json')
  const inspectorPath = resolve(evidenceDir, 'lock_topology_888x14.inspector.v1.json')
  const tonguesPath = resolve(evidenceDir, 'assembly_lock_tongues_888x14.json')
  const validationPath = resolve(evidenceDir, 'lock_topology_888x14.validation.v2.json')
  mkdirSync(evidenceDir, { recursive: true })
  writeFileSync(buildPath, '{"build":"trusted fixture"}\n', 'utf8')
  const commands = []
  const runProcess = async (command) => {
    commands.push(command)
    if (command.id === 'inspect-lock-topology') {
      assert.equal(command.args[0], input.attemptDir)
      assert.equal(command.args[1], sha256File(buildPath))
      writeFileSync(inspectorPath, '{"inspector":"trusted fixture"}\n', 'utf8')
    } else if (command.id === 'inspect-assembly-tongues') {
      assert.equal(command.args[0], input.attemptDir)
      assert.equal(command.args[1], sha256File(buildPath))
      assert.equal(command.args[2], sha256File(inspectorPath))
      writeFileSync(tonguesPath, '{"tongues":"trusted fixture"}\n', 'utf8')
    } else if (command.id === 'validate-lock-topology') {
      const output = mutateOutput({
        schema: 'winnsen.locker16029.native_888x14_lock_topology_validation.v2',
        generatedAtUtc: '2026-08-12T10:05:00.000Z',
        purpose: 'structure_engineering_assistance',
        status,
        structureEngineeringEvidencePass: status === 'PASS',
        inputs: {
          build: { path: buildPath, sha256: forgeBuildHash ? '0'.repeat(64) : sha256File(buildPath) },
          inspector: { path: inspectorPath, sha256: sha256File(inspectorPath) },
          tongues: { path: tonguesPath, sha256: sha256File(tonguesPath) },
        },
        expectedRowsMm: { slots: [], circles: [] },
        checks: [],
        failedCheckCount: status === 'PASS' ? 0 : 1,
        failedChecks: status === 'PASS' ? [] : ['fixture_failure'],
        limitation: 'fixture',
      })
      writeFileSync(validationPath, `${JSON.stringify(output, null, 2)}\n`, 'utf8')
    } else {
      assert.fail(`unexpected validation command ${command.id}`)
    }
    return { exitCode: 0, signal: '', stdout: '', stderr: '', outputTruncated: false }
  }
  return { buildPath, inspectorPath, tonguesPath, validationPath, commands, runProcess }
}

const checks = []
async function check(name, action) {
  if (FIXTURE_ONLY && LIVE_EVIDENCE_CHECKS.has(name)) {
    checks.push({ name, skipped: true, reason: 'fixture-only mode excludes workstation SolidWorks interop and protected CAD sources' })
    return
  }
  try {
    await action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({
      name,
      ok: false,
      error: `${error?.code ? `${error.code}: ` : ''}${error?.message || error}`,
    })
  }
}

rmSync(TEMP_ROOT, { recursive: true, force: true })
mkdirSync(TEMP_ROOT, { recursive: true })

await check('exports_atomic_executor_contract', () => {
  requireExport('buildNative888x14ExecutionBlueprint')
  requireExport('prepareNative888x14DoorInputs')
  requireExport('runAuthorizedNativeStageUnit')
  requireExport('runNative888x14LockValidation')
  requireExport('validateNativeToolRuntimeDependencies')
})

await check('live_toolchain_has_pinned_solidworks_interop_runtime_dependencies', () => {
  const artifacts = resolveTrustedNativeToolchainArtifacts()
  for (const [toolId, artifact] of Object.entries(artifacts)) {
    const result = executor.validateNativeToolRuntimeDependencies({ toolId, tool: artifact.tool })
    assert.equal(result.toolId, toolId)
    assert.ok(isAbsolute(result.sldworksInterop))
    assert.ok(isAbsolute(result.swconstInterop))
    if (toolId === 'native_seed_pack_888x14_v1' || toolId === 'native_width_888_v1') {
      assert.equal(result.configPath, `${artifact.tool.executablePath}.config`)
    } else {
      assert.equal(result.configPath, '')
    }
  }
})

await check('shared_interop_runtime_rejects_a_modified_executable_config', () => {
  const artifacts = resolveTrustedNativeToolchainArtifacts()
  const live = executor.validateNativeToolRuntimeDependencies({
    toolId: 'native_seed_pack_888x14_v1',
    tool: artifacts.native_seed_pack_888x14_v1.tool,
  })
  const fixtureRoot = resolve(TEMP_ROOT, 'interop-config-fixture')
  const executablePath = resolve(fixtureRoot, 'seed', 'SeedPack888Native.exe')
  const sharedBin = resolve(fixtureRoot, 'workers/native_model_requests/development/v1/tools/bin')
  mkdirSync(dirname(executablePath), { recursive: true })
  mkdirSync(sharedBin, { recursive: true })
  writeFileSync(executablePath, 'fixture executable', 'utf8')
  copyFileSync(live.sldworksInterop, resolve(sharedBin, 'SolidWorks.Interop.sldworks.dll'))
  copyFileSync(live.swconstInterop, resolve(sharedBin, 'SolidWorks.Interop.swconst.dll'))
  copyFileSync(live.configPath, `${executablePath}.config`)
  assert.doesNotThrow(() => executor.validateNativeToolRuntimeDependencies({
    toolId: 'native_seed_pack_888x14_v1',
    tool: { executablePath },
    repositoryRoot: fixtureRoot,
  }))
  appendFileSync(`${executablePath}.config`, '\n<!-- changed -->\n', 'utf8')
  assert.throws(() => executor.validateNativeToolRuntimeDependencies({
    toolId: 'native_seed_pack_888x14_v1',
    tool: { executablePath },
    repositoryRoot: fixtureRoot,
  }), (error) => error?.code === 'NATIVE_EXECUTOR_INTEROP_CONFIG_INVALID')
})

await check('blueprint_contains_exact_nine_atomic_receipt_units', () => {
  const input = fixture()
  assert.deepEqual(input.blueprint.units.map((row) => row.receiptPhase), NATIVE_STAGE_RECEIPT_ORDER)
  assert.equal(Object.hasOwn(input.blueprint, 'groups'), false,
    'the obsolete six-group execution surface must not remain callable')
  assert.equal(new Set(input.blueprint.units.map((row) => row.id)).size, 9)
  for (const unit of input.blueprint.units) {
    assert.equal(unit.toolId, input.plan.stageReceiptContracts[unit.receiptPhase].producerToolId)
    assert.deepEqual(unit.phases, NATIVE_EXECUTION_TOOL_CONTRACTS[unit.toolId])
    assert.equal(unit.receiptPath,
      resolve(input.attemptDir, input.plan.stageReceiptContracts[unit.receiptPhase].path))
  }
  const widthUnits = input.blueprint.units.filter((row) => row.toolId === 'native_width_888_v1')
  assert.deepEqual(widthUnits.map((row) => row.receiptPhase), WIDTH_PHASES)
  assert.equal(widthUnits.every((row) => row.commands.length === 1), true)
  const doorUnit = stageUnit(input.blueprint, 'door_module_888x14')
  assert.deepEqual(doorUnit.commands.map((row) => row.id),
    ['door-left-panel-proof', 'door-module-build'])
})

await check('blueprint_binds_every_unit_to_its_exact_tool_cli_contract', () => {
  const input = fixture()
  const expected = {
    clone_native_seed: [[
      '--source', resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/native_cad/final_native_hierarchical_pack_and_go'),
      '--working-pack', input.workingPack,
      '--out', resolve(input.attemptDir, 'evidence/seed_pack_888_native_v1.json'),
      '--confirm-task', input.taskId,
    ]],
    dimensions: [[input.workingPack, resolve(input.attemptDir, 'evidence/width-dimensions.json'), 'dimensions']],
    derived: [[input.workingPack, resolve(input.attemptDir, 'evidence/width-derived.json'), 'derived']],
    'base-hole': [[input.workingPack, resolve(input.attemptDir, 'evidence/width-base-hole.json'), 'base-hole']],
    assemblies: [[input.workingPack, resolve(input.attemptDir, 'evidence/width-assemblies.json'), 'assemblies']],
    door_module_888x14: [[
      '--panel-proof', '--mode', 'execute', '--request', resolve(input.attemptDir, 'inputs/door_module_888x14.request.json'),
      '--out', resolve(input.attemptDir, 'evidence/left-panel-proof.v1.json'), '--confirm-task', input.taskId,
    ], [
      '--mode', 'execute', '--request', resolve(input.attemptDir, 'inputs/door_module_888x14.request.json'),
      '--out', resolve(input.attemptDir, 'evidence/door_module_888x14.result.v1.json'), '--confirm-task', input.taskId,
    ]],
    lock_topology_888x14: [[input.attemptDir]],
    root_assembly_888x14: [[
      '--working-pack', input.workingPack,
      '--door-module', resolve(input.attemptDir, 'native_cad/door_module_888x14'),
      '--out', resolve(input.attemptDir, 'evidence/root_assembly_888x14.result.v1.json'),
      '--confirm-task', input.taskId,
    ]],
    final_pack_and_relocated_reopen: [[
      '--attempt', input.attemptDir,
      '--plan', input.planPath,
      '--authorization', resolve(input.attemptDir, 'execution_authorizations/native_final_pack_888x14_v1.json'),
      '--confirm-task', input.taskId,
    ]],
  }
  assert.deepEqual(Object.fromEntries(input.blueprint.units.map((unit) => [
    unit.receiptPhase, unit.commands.map((row) => row.args),
  ])), expected)
})

await check('width_phases_each_issue_and_consume_a_new_authorization', async () => {
  const runAuthorizedNativeStageUnit = requireExport('runAuthorizedNativeStageUnit')
  const input = fixture()
  const harness = fakeAuthorizationHarness()
  for (let index = 0; index < WIDTH_PHASES.length; index += 1) {
    const phase = WIDTH_PHASES[index]
    const preInventoryDigest = stageDigest(index + 3)
    await runAuthorizedNativeStageUnit(runUnitOptions(input, stageUnit(input.blueprint, phase), harness, {
      preInventoryDigest,
      runProcess: async () => ({
        exitCode: 0, signal: '', stdout: '', stderr: '', outputTruncated: false,
      }),
    }))
  }
  assert.equal(harness.issueCalls.length, 4)
  assert.equal(harness.consumeCalls.length, 4)
  assert.deepEqual(harness.issueCalls.map((row) => row.phases),
    WIDTH_PHASES.map(() => [...WIDTH_PHASES]))
  assert.deepEqual(harness.consumeCalls.map((row) => row.expectedPhase), WIDTH_PHASES)
  assert.deepEqual(harness.issueCalls.map((row) => row.seedInventoryDigest),
    WIDTH_PHASES.map((_, index) => stageDigest(index + 3)))
  assert.deepEqual(harness.consumeCalls.map((row) => row.seedInventoryDigest),
    WIDTH_PHASES.map((_, index) => stageDigest(index + 3)))
  assert.equal(harness.containCalls.length, 0)
})

await check('trusted_child_environment_preserves_system_data_roots_without_forwarding_unregistered_values', async () => {
  const runAuthorizedNativeStageUnit = requireExport('runAuthorizedNativeStageUnit')
  const input = fixture()
  const harness = fakeAuthorizationHarness()
  let childEnv = null
  await runAuthorizedNativeStageUnit(runUnitOptions(
    input, stageUnit(input.blueprint, 'dimensions'), harness, {
      baseEnvironment: {
        SystemRoot: 'C:\\Windows',
        PROGRAMDATA: 'C:\\ProgramData',
        ALLUSERSPROFILE: 'C:\\ProgramData',
        LOCALAPPDATA: 'C:\\Users\\fixture\\AppData\\Local',
        APPDATA: 'C:\\Users\\fixture\\AppData\\Roaming',
        SECRET_FIXTURE_VALUE: 'must-not-pass',
      },
      runProcess: async ({ env }) => {
        childEnv = env
        return { exitCode: 0, signal: '', stdout: '', stderr: '', outputTruncated: false }
      },
    },
  ))
  assert.equal(childEnv.PROGRAMDATA, 'C:\\ProgramData')
  assert.equal(childEnv.ALLUSERSPROFILE, 'C:\\ProgramData')
  assert.equal(childEnv.LOCALAPPDATA, 'C:\\Users\\fixture\\AppData\\Local')
  assert.equal(childEnv.APPDATA, 'C:\\Users\\fixture\\AppData\\Roaming')
  assert.equal(Object.hasOwn(childEnv, 'SECRET_FIXTURE_VALUE'), false)
})

await check('door_proof_and_build_share_one_authorization_without_post_issue_cancel_check', async () => {
  const runAuthorizedNativeStageUnit = requireExport('runAuthorizedNativeStageUnit')
  const input = fixture()
  const log = []
  const harness = fakeAuthorizationHarness(log)
  let cancellationChecks = 0
  const processAuthHashes = []
  await runAuthorizedNativeStageUnit(runUnitOptions(
    input,
    stageUnit(input.blueprint, 'door_module_888x14'),
    harness,
    {
      isCancellationRequested: () => {
        cancellationChecks += 1
        log.push('cancel-check')
        return false
      },
      runProcess: async (command) => {
        log.push(`process:${command.id}`)
        processAuthHashes.push(command.env.WINNSEN_NATIVE_EXECUTION_AUTH_SHA256)
        return { exitCode: 0, signal: '', stdout: '', stderr: '', outputTruncated: false }
      },
      inventoryDigest: () => {
        log.push('inventory')
        return '9'.repeat(64)
      },
    },
  ))
  assert.equal(cancellationChecks, 1)
  assert.deepEqual(log, [
    'cancel-check',
    'issue:door_module_888x14',
    'process:door-left-panel-proof',
    'process:door-module-build',
    'inventory',
    'consume:door_module_888x14',
  ])
  assert.equal(harness.issueCalls.length, 1)
  assert.equal(harness.consumeCalls.length, 1)
  assert.equal(new Set(processAuthHashes).size, 1)
  assert.equal(processAuthHashes[0], '1'.padStart(64, '0'))
})

await check('tool_failure_is_contained_and_never_consumed', async () => {
  const runAuthorizedNativeStageUnit = requireExport('runAuthorizedNativeStageUnit')
  const input = fixture()
  const log = []
  const harness = fakeAuthorizationHarness(log)
  const unit = stageUnit(input.blueprint, 'clone_native_seed')
  let caught = null
  await assert.rejects(
    runAuthorizedNativeStageUnit(runUnitOptions(input, unit, harness, {
      isCancellationRequested: () => {
        log.push('cancel-check')
        return false
      },
      runProcess: async (command) => {
        log.push(`process:${command.id}`)
        return { exitCode: 23, signal: '', stdout: '', stderr: 'PLAN_STAGE_RECEIPT_CONTRACT_INVALID: private path omitted', outputTruncated: false }
      },
    })),
    (error) => {
      caught = error
      return error?.code === 'NATIVE_EXECUTOR_PROCESS_FAILED'
    },
  )
  assert.equal(caught.childStatusCode, 'PLAN_STAGE_RECEIPT_CONTRACT_INVALID')
  assert.equal(caught.message, 'seed-copy-and-stabilize exited with code 23 (PLAN_STAGE_RECEIPT_CONTRACT_INVALID)')
  assert.equal(caught.message.includes('private path'), false)
  assert.equal(caught.privateDiagnosticPath.startsWith(input.attemptDir), true)
  const privateDiagnostic = JSON.parse(readFileSync(caught.privateDiagnosticPath, 'utf8'))
  assert.deepEqual(privateDiagnostic, {
    schema: 'winnsen.native_private_stage_diagnostic.v1',
    phase: 'clone_native_seed',
    authorizationId: 'native-auth-fixture-1',
    commandId: 'seed-copy-and-stabilize',
    exitCode: 23,
    signal: '',
    statusCode: 'PLAN_STAGE_RECEIPT_CONTRACT_INVALID',
    stdout: '',
    stderr: 'PLAN_STAGE_RECEIPT_CONTRACT_INVALID: private path omitted',
    outputTruncated: false,
  })
  assert.equal(harness.issueCalls.length, 1)
  assert.equal(harness.consumeCalls.length, 0)
  assert.equal(harness.containCalls.length, 1)
  assert.equal(harness.containCalls[0].failedPhase, 'clone_native_seed')
  assert.deepEqual(log, [
    'cancel-check',
    'issue:clone_native_seed',
    'process:seed-copy-and-stabilize',
    'contain:clone_native_seed',
  ])
})

await check('trusted_process_timeout_terminates_child_and_stops_heartbeats', async () => {
  const runTrustedNativeProcess = requireExport('runTrustedNativeProcess')
  const childScript = resolve(TEMP_ROOT, 'timeout-child.mjs')
  const pidPath = resolve(TEMP_ROOT, 'timeout-child.pid')
  writeFileSync(childScript, [
    "import { writeFileSync } from 'node:fs'",
    'writeFileSync(process.argv[2], String(process.pid), \'utf8\')',
    'setTimeout(() => process.exit(0), 250)',
  ].join('\n'), 'utf8')
  let heartbeats = 0
  let error = null
  try {
    await runTrustedNativeProcess({
      executable: process.execPath,
      args: [childScript, pidPath],
      cwd: TEMP_ROOT,
      env: process.env,
      timeoutMs: 60,
      heartbeatMs: 15,
      onHeartbeat: () => { heartbeats += 1 },
    })
  } catch (caught) {
    error = caught
  }
  const recordedPid = Number(readFileSync(pidPath, 'utf8'))
  let childStillAlive = false
  try { process.kill(recordedPid, 0); childStillAlive = true } catch {}
  const heartbeatCountAtExit = heartbeats
  await new Promise((resolveWait) => setTimeout(resolveWait, 80))
  assert.deepEqual({
    timeoutCode: error?.code,
    childStillAlive,
    heartbeatsBeforeExit: heartbeatCountAtExit > 0,
    heartbeatsStopped: heartbeats === heartbeatCountAtExit,
  }, {
    timeoutCode: 'NATIVE_EXECUTOR_PROCESS_TIMEOUT',
    childStillAlive: false,
    heartbeatsBeforeExit: true,
    heartbeatsStopped: true,
  })
})

await check('trusted_process_timeout_waits_for_child_close_when_kill_fails', async () => {
  const runTrustedNativeProcess = requireExport('runTrustedNativeProcess')
  const childScript = resolve(TEMP_ROOT, 'timeout-kill-failure-child.mjs')
  const pidPath = resolve(TEMP_ROOT, 'timeout-kill-failure-child.pid')
  writeFileSync(childScript, [
    "import { writeFileSync } from 'node:fs'",
    'writeFileSync(process.argv[2], String(process.pid), \'utf8\')',
    'setTimeout(() => process.exit(0), 260)',
  ].join('\n'), 'utf8')
  const originalKill = ChildProcess.prototype.kill
  let heartbeats = 0
  let settled = false
  let error = null
  const startedAt = Date.now()
  ChildProcess.prototype.kill = function fixtureKillFailure() {
    const failure = new Error('fixture child termination refused')
    failure.code = 'EPERM'
    throw failure
  }
  let run
  try {
    run = runTrustedNativeProcess({
      executable: process.execPath,
      args: [childScript, pidPath],
      cwd: TEMP_ROOT,
      env: process.env,
      timeoutMs: 60,
      heartbeatMs: 15,
      onHeartbeat: () => { heartbeats += 1 },
    }).catch((caught) => { error = caught }).finally(() => { settled = true })
    await new Promise((resolveWait) => setTimeout(resolveWait, 45))
    const heartbeatsBeforeTimeout = heartbeats
    await new Promise((resolveWait) => setTimeout(resolveWait, 80))
    const settledBeforeChildExit = settled
    const heartbeatContinuedAfterKillFailure = heartbeats > heartbeatsBeforeTimeout
    await run
    const elapsedAtSettlement = Date.now() - startedAt
    const recordedPid = Number(readFileSync(pidPath, 'utf8'))
    let childAliveAtSettlement = false
    try { process.kill(recordedPid, 0); childAliveAtSettlement = true } catch {}
    const heartbeatCountAtSettlement = heartbeats
    await new Promise((resolveWait) => setTimeout(resolveWait, 220))
    assert.deepEqual({
      timeoutCode: error?.code,
      settledBeforeChildExit,
      heartbeatContinuedAfterKillFailure,
      childAliveAtSettlement,
      waitedForChildClose: elapsedAtSettlement >= 200,
      heartbeatsStoppedAfterClose: heartbeats === heartbeatCountAtSettlement,
    }, {
      timeoutCode: 'NATIVE_EXECUTOR_PROCESS_TIMEOUT',
      settledBeforeChildExit: false,
      heartbeatContinuedAfterKillFailure: true,
      childAliveAtSettlement: false,
      waitedForChildClose: true,
      heartbeatsStoppedAfterClose: true,
    })
  } finally {
    ChildProcess.prototype.kill = originalKill
    await run
  }
})

await check('containment_failure_preserves_primary_failure_and_never_consumes', async () => {
  const runAuthorizedNativeStageUnit = requireExport('runAuthorizedNativeStageUnit')
  const input = fixture()
  const harness = fakeAuthorizationHarness()
  let caught = null
  try {
    await runAuthorizedNativeStageUnit(runUnitOptions(input,
      stageUnit(input.blueprint, 'clone_native_seed'), harness, {
        runProcess: async () => ({ exitCode: 23, signal: '', stdout: '', stderr: 'fixture process failure', outputTruncated: false }),
        containAuthorization: () => {
          const error = new Error('fixture containment failure')
          error.code = 'NATIVE_AUTHORIZATION_FAILURE_CONTAINMENT_REFUSED'
          throw error
        },
      }))
  } catch (error) { caught = error }
  assert.deepEqual({
    code: caught?.code,
    primaryCode: caught?.primaryCode,
    containmentCode: caught?.containmentCode,
    consumed: harness.consumeCalls.length,
  }, {
    code: 'NATIVE_EXECUTOR_FAILURE_CONTAINMENT_FAILED',
    primaryCode: 'NATIVE_EXECUTOR_PROCESS_FAILED',
    containmentCode: 'NATIVE_AUTHORIZATION_FAILURE_CONTAINMENT_REFUSED',
    consumed: 0,
  })
})

await check('door_inputs_are_exact_isolated_native_sources', () => {
  const prepareNative888x14DoorInputs = requireExport('prepareNative888x14DoorInputs')
  const input = fixture()
  copyWorkingPackDoorSources(input)
  const originalHashes = Object.fromEntries(Object.entries(DOOR_SOURCE_CONTRACTS)
    .map(([role, row]) => [role, sha256File(row.path)]))
  prepareNative888x14DoorInputs({
    blueprint: input.blueprint,
    task: input.task,
    workerId: input.workerId,
    repositoryRoot: ROOT,
  })
  const sentinelPath = resolve(input.attemptDir, '.winnsen-native-isolated-root.json')
  const requestPath = resolve(input.attemptDir, 'inputs/door_module_888x14.request.json')
  const sentinel = JSON.parse(readFileSync(sentinelPath, 'utf8'))
  const request = JSON.parse(readFileSync(requestPath, 'utf8'))
  assert.deepEqual(Object.keys(sentinel), ['schema', 'task_id', 'allow_native_solidworks_write'])
  assert.deepEqual(sentinel, {
    schema: 'winnsen.16029.native_isolated_root.v1',
    task_id: input.taskId,
    allow_native_solidworks_write: true,
  })
  assert.deepEqual(Object.keys(request), [
    'schema', 'task_id', 'worker_id', 'lease_id', 'cabinet_width_mm', 'door_width_mm',
    'door_height_mm', 'columns', 'rows_per_column', 'total_doors', 'isolated_root',
    'output_dir', 'left_panel_proof', 'right_panel_proof', 'right_panel_strategy', 'visible',
    'sources',
  ])
  assert.equal(request.schema, 'winnsen.16029.native_door_module_request.v1')
  assert.equal(request.task_id, input.taskId)
  assert.equal(request.worker_id, input.workerId)
  assert.equal(request.lease_id, input.task.nativeBuild.lease.id)
  assert.equal(request.cabinet_width_mm, 888)
  assert.equal(request.door_width_mm, 381)
  assert.equal(request.door_height_mm, 1781 / 7)
  assert.equal(request.columns, 2)
  assert.equal(request.rows_per_column, 7)
  assert.equal(request.total_doors, 14)
  assert.equal(request.isolated_root, input.attemptDir)
  assert.equal(request.output_dir, resolve(input.attemptDir, 'native_cad/door_module_888x14'))
  assert.equal(request.left_panel_proof, resolve(input.attemptDir, 'evidence/left-panel-proof.v1.json'))
  assert.equal(request.right_panel_proof, '')
  assert.equal(request.right_panel_strategy, 'solidworks_mirror_part')
  assert.equal(request.visible, false)
  assert.deepEqual(Object.keys(request.sources), Object.keys(DOOR_SOURCE_CONTRACTS))
  for (const [role, expected] of Object.entries(DOOR_SOURCE_CONTRACTS)) {
    const actual = request.sources[role]
    assert.deepEqual(Object.keys(actual), ['path', 'sha256'])
    assert.equal(actual.sha256, expected.sha256)
    assert.equal(sha256File(actual.path), expected.sha256)
    assert.equal(underRoot(actual.path, input.attemptDir), true)
    assert.equal(statSync(actual.path).nlink, 1)
    if (expected.mustCopy) {
      assert.notEqual(resolve(actual.path), resolve(expected.path))
      assert.equal(readFileSync(actual.path).equals(readFileSync(expected.path)), true)
    } else {
      assert.equal(resolve(actual.path), resolve(input.workingPack, expected.path.slice(expected.path.lastIndexOf('\\') + 1)))
    }
  }
  assert.deepEqual(Object.fromEntries(Object.entries(DOOR_SOURCE_CONTRACTS)
    .map(([role, row]) => [role, sha256File(row.path)])), originalHashes)
})

await check('door_input_hash_tamper_fails_before_sentinel_or_request_commit', () => {
  const prepareNative888x14DoorInputs = requireExport('prepareNative888x14DoorInputs')
  const input = fixture()
  copyWorkingPackDoorSources(input)
  appendFileSync(resolve(input.workingPack, '锁舌.SLDPRT'), 'tampered', 'utf8')
  assert.throws(
    () => prepareNative888x14DoorInputs({
      blueprint: input.blueprint,
      task: input.task,
      workerId: input.workerId,
      repositoryRoot: ROOT,
    }),
    (error) => /HASH|SOURCE/.test(String(error?.code || error?.message || '')),
  )
  assert.equal(existsSync(resolve(input.attemptDir, '.winnsen-native-isolated-root.json')), false)
  assert.equal(existsSync(resolve(input.attemptDir, 'inputs/door_module_888x14.request.json')), false)
})

await check('door_input_sentinel_is_create_new_and_never_overwritten', () => {
  const prepareNative888x14DoorInputs = requireExport('prepareNative888x14DoorInputs')
  const input = fixture()
  copyWorkingPackDoorSources(input)
  const sentinelPath = resolve(input.attemptDir, '.winnsen-native-isolated-root.json')
  const existing = '{"untrusted":true}\n'
  writeFileSync(sentinelPath, existing, 'utf8')
  assert.throws(() => prepareNative888x14DoorInputs({
    blueprint: input.blueprint,
    task: input.task,
    workerId: input.workerId,
    repositoryRoot: ROOT,
  }))
  assert.equal(readFileSync(sentinelPath, 'utf8'), existing)
  assert.equal(existsSync(resolve(input.attemptDir, 'inputs/door_module_888x14.request.json')), false)
})

await check('door_input_transaction_rolls_back_second_copy_and_request_commit_failures', () => {
  const prepareNative888x14DoorInputs = requireExport('prepareNative888x14DoorInputs')
  const inputArtifactsWereRemoved = (input) => [
    resolve(input.attemptDir, '.winnsen-native-isolated-root.json'),
    resolve(input.attemptDir, 'inputs/door_module_888x14.request.json'),
    ...Object.values(DOOR_SOURCE_CONTRACTS).filter((row) => row.mustCopy)
      .map((row) => resolve(input.attemptDir, 'inputs/door_sources', row.path.slice(row.path.lastIndexOf('\\') + 1))),
  ].every((path) => !existsSync(path))
  const injectedFailure = (message) => {
    const error = new Error(message)
    error.code = 'NATIVE_EXECUTOR_FIXTURE_INJECTED_FAILURE'
    return error
  }
  const copyInput = fixture()
  copyWorkingPackDoorSources(copyInput)
  let copyCount = 0
  let copyError = null
  try {
    prepareNative888x14DoorInputs({
      blueprint: copyInput.blueprint,
      task: copyInput.task,
      workerId: copyInput.workerId,
      repositoryRoot: ROOT,
      fileOps: {
        writeOwnedFile(descriptor, bytes) {
          copyCount += 1
          if (copyCount === 2) throw injectedFailure('second gold copy failed')
          writeFileSync(descriptor, bytes)
        },
      },
    })
  } catch (error) { copyError = error }
  const requestInput = fixture()
  copyWorkingPackDoorSources(requestInput)
  let requestError = null
  try {
    prepareNative888x14DoorInputs({
      blueprint: requestInput.blueprint,
      task: requestInput.task,
      workerId: requestInput.workerId,
      repositoryRoot: ROOT,
      fileOps: {
        createNewJson(path, value, label) {
          if (label === 'door request') throw injectedFailure('door request commit failed')
          mkdirSync(dirname(path), { recursive: true })
          writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`, { encoding: 'utf8', flag: 'wx' })
        },
      },
    })
  } catch (error) { requestError = error }
  assert.deepEqual({
    copyFailed: copyError?.code === 'NATIVE_EXECUTOR_FIXTURE_INJECTED_FAILURE',
    copyArtifactsRemoved: inputArtifactsWereRemoved(copyInput),
    requestFailed: requestError?.code === 'NATIVE_EXECUTOR_FIXTURE_INJECTED_FAILURE',
    requestArtifactsRemoved: inputArtifactsWereRemoved(requestInput),
  }, {
    copyFailed: true,
    copyArtifactsRemoved: true,
    requestFailed: true,
    requestArtifactsRemoved: true,
  })
})

await check('door_input_transaction_never_deletes_foreign_collision_or_mutates_replaced_temp_target', () => {
  const prepareNative888x14DoorInputs = requireExport('prepareNative888x14DoorInputs')
  const injectedFailure = (message) => {
    const error = new Error(message)
    error.code = 'EEXIST'
    return error
  }

  const collisionInput = fixture()
  copyWorkingPackDoorSources(collisionInput)
  const copiedSource = Object.values(DOOR_SOURCE_CONTRACTS).find((row) => row.mustCopy)
  const collisionPath = resolve(collisionInput.attemptDir, 'inputs/door_sources',
    copiedSource.path.slice(copiedSource.path.lastIndexOf('\\') + 1))
  let collisionError = null
  try {
    prepareNative888x14DoorInputs({
      blueprint: collisionInput.blueprint,
      task: collisionInput.task,
      workerId: collisionInput.workerId,
      repositoryRoot: ROOT,
      fileOps: {
        renameSync(temporaryPath, targetPath) {
          writeFileSync(targetPath, readFileSync(temporaryPath), { flag: 'wx' })
          throw injectedFailure('foreign actor won the final-name collision')
        },
      },
    })
  } catch (error) { collisionError = error }

  const replacementInput = fixture()
  copyWorkingPackDoorSources(replacementInput)
  const protectedPath = resolve(replacementInput.attemptDir, 'inputs/protected-foreign.bin')
  mkdirSync(dirname(protectedPath), { recursive: true })
  writeFileSync(protectedPath, 'foreign content must remain unchanged\n', 'utf8')
  const protectedHashBefore = sha256File(protectedPath)
  let replacementError = null
  try {
    prepareNative888x14DoorInputs({
      blueprint: replacementInput.blueprint,
      task: replacementInput.task,
      workerId: replacementInput.workerId,
      repositoryRoot: ROOT,
      fileOps: {
        writeOwnedFile(descriptor) {
          assert.equal(typeof descriptor, 'number')
          const error = new Error('fixture confirms no temporary path is exposed to the writer')
          error.code = 'NATIVE_EXECUTOR_PATH_UNSAFE'
          throw error
        },
      },
    })
  } catch (error) { replacementError = error }

  assert.deepEqual({
    collisionCode: collisionError?.code,
    foreignCollisionSurvived: existsSync(collisionPath),
    foreignCollisionHash: existsSync(collisionPath) ? sha256File(collisionPath) : null,
    expectedCollisionHash: copiedSource.sha256,
    replacementCode: replacementError?.code,
    protectedForeignUnchanged: sha256File(protectedPath) === protectedHashBefore,
  }, {
    collisionCode: 'EEXIST',
    foreignCollisionSurvived: true,
    foreignCollisionHash: copiedSource.sha256,
    expectedCollisionHash: copiedSource.sha256,
    replacementCode: 'NATIVE_EXECUTOR_PATH_UNSAFE',
    protectedForeignUnchanged: true,
  })
})

await check('door_input_preparation_rejects_attempt_root_junction_itself', () => {
  const prepareNative888x14DoorInputs = requireExport('prepareNative888x14DoorInputs')
  const input = fixture()
  copyWorkingPackDoorSources(input)
  const realAttempt = resolve(dirname(input.attemptDir), 'attempt-real-target')
  renameSync(input.attemptDir, realAttempt)
  symlinkSync(realAttempt, input.attemptDir, 'junction')
  assert.throws(
    () => prepareNative888x14DoorInputs({
      blueprint: input.blueprint,
      task: input.task,
      workerId: input.workerId,
      repositoryRoot: ROOT,
    }),
    (error) => error?.code === 'NATIVE_EXECUTOR_PATH_UNSAFE',
  )
})

await check('post_root_lock_validation_runs_three_bound_commands_and_requires_pass', async () => {
  const runNative888x14LockValidation = requireExport('runNative888x14LockValidation')
  const input = fixture()
  const validation = writeLockValidationFixture(input)
  await runNative888x14LockValidation({
    blueprint: input.blueprint,
    toolchainArtifacts: input.artifacts,
    runProcess: validation.runProcess,
    baseEnvironment: {},
  })
  assert.deepEqual(validation.commands.map((row) => row.id), [
    'inspect-lock-topology', 'inspect-assembly-tongues', 'validate-lock-topology',
  ])
  const result = JSON.parse(readFileSync(validation.validationPath, 'utf8'))
  assert.equal(result.status, 'PASS')
  assert.equal(result.structureEngineeringEvidencePass, true)
})

await check('post_root_lock_validation_rejects_fail_status', async () => {
  const runNative888x14LockValidation = requireExport('runNative888x14LockValidation')
  const input = fixture()
  const validation = writeLockValidationFixture(input, { status: 'FAIL' })
  await assert.rejects(
    runNative888x14LockValidation({
      blueprint: input.blueprint,
      toolchainArtifacts: input.artifacts,
      runProcess: validation.runProcess,
      baseEnvironment: {},
    }),
    (error) => error?.code === 'NATIVE_EXECUTOR_LOCK_VALIDATION_FAILED',
  )
})

await check('post_root_lock_validation_rejects_forged_input_hash', async () => {
  const runNative888x14LockValidation = requireExport('runNative888x14LockValidation')
  const input = fixture()
  const validation = writeLockValidationFixture(input, { forgeBuildHash: true })
  await assert.rejects(
    runNative888x14LockValidation({
      blueprint: input.blueprint,
      toolchainArtifacts: input.artifacts,
      runProcess: validation.runProcess,
      baseEnvironment: {},
    }),
    (error) => error?.code === 'NATIVE_EXECUTOR_LOCK_VALIDATION_FAILED',
  )
})

await check('post_root_lock_validation_requires_exact_pass_output_schema_and_nested_keys', async () => {
  const runNative888x14LockValidation = requireExport('runNative888x14LockValidation')
  const cases = [
    ['extra-top-level-key', (output) => ({ ...output, unexpected: true })],
    ['extra-input-key', (output) => ({ ...output, inputs: { ...output.inputs, unexpected: true } })],
    ['wrong-schema', (output) => ({ ...output, schema: 'winnsen.forged.lock.validation.v2' })],
  ]
  const outcomes = []
  for (const [, mutateOutput] of cases) {
    const input = fixture()
    const validation = writeLockValidationFixture(input, { mutateOutput })
    try {
      await runNative888x14LockValidation({
        blueprint: input.blueprint,
        toolchainArtifacts: input.artifacts,
        runProcess: validation.runProcess,
        baseEnvironment: {},
      })
      outcomes.push('accepted')
    } catch (error) {
      outcomes.push(error?.code)
    }
  }
  assert.deepEqual(outcomes, cases.map(() => 'NATIVE_EXECUTOR_LOCK_VALIDATION_FAILED'))
})

await check('cad_inventory_uses_ordinal_ignore_case_unicode_order_and_rejects_case_duplicates', () => {
  const canonicalFlatCadInventoryDigest = requireExport('canonicalFlatCadInventoryDigest')
  const unicodeDirectory = resolve(TEMP_ROOT, 'inventory-unicode')
  mkdirSync(unicodeDirectory, { recursive: true })
  const fixtureFiles = {
    'a.SLDASM': 'a',
    'Á.SLDPRT': 'acute',
    '门板.SLDPRT': 'door',
  }
  for (const [name, content] of Object.entries(fixtureFiles)) writeFileSync(resolve(unicodeDirectory, name), content, 'utf8')
  const ordinalCompare = (left, right) => {
    const limit = Math.min(left.length, right.length)
    for (let index = 0; index < limit; index += 1) {
      const difference = left.charCodeAt(index) - right.charCodeAt(index)
      if (difference) return difference
    }
    return left.length - right.length
  }
  const ordinalIgnoreCase = (left, right) =>
    ordinalCompare(left.toUpperCase(), right.toUpperCase()) || ordinalCompare(left, right)
  const expectedDigest = sha256(Buffer.from(Object.entries(fixtureFiles)
    .sort(([left], [right]) => ordinalIgnoreCase(left, right))
    .map(([name, content]) => `${name}|${sha256(content)}\n`).join(''), 'utf8'))
  const actualDigest = canonicalFlatCadInventoryDigest(unicodeDirectory, { expectedFileCount: 3 })
  const duplicateDirectory = resolve(TEMP_ROOT, 'inventory-case-duplicate')
  mkdirSync(duplicateDirectory, { recursive: true })
  writeFileSync(resolve(duplicateDirectory, 'Door.SLDPRT'), 'left', 'utf8')
  let duplicateError = null
  try {
    canonicalFlatCadInventoryDigest(duplicateDirectory, {
      expectedFileCount: 1,
      fileOps: {
        readdirSync: () => [
          { name: 'Door.SLDPRT', isDirectory: () => false, isFile: () => true },
          { name: 'door.SLDPRT', isDirectory: () => false, isFile: () => true },
        ],
      },
    })
  } catch (error) { duplicateError = error }
  assert.deepEqual({
    unicodeDigestMatches: actualDigest === expectedDigest,
    duplicateCode: duplicateError?.code,
  }, {
    unicodeDigestMatches: true,
    duplicateCode: 'NATIVE_EXECUTOR_INVENTORY_INVALID',
  })
})

await check('cad_inventory_matches_dotnet_ordinal_ignore_case_for_sharp_s_and_ss', () => {
  const canonicalFlatCadInventoryDigest = requireExport('canonicalFlatCadInventoryDigest')
  const directory = resolve(TEMP_ROOT, 'inventory-dotnet-ordinal-ignore-case')
  mkdirSync(directory, { recursive: true })
  writeFileSync(resolve(directory, 'SS.SLDPRT'), 'double-s', 'utf8')
  writeFileSync(resolve(directory, 'ß.SLDPRT'), 'sharp-s', 'utf8')
  const expectedFromDotNetOrdinalIgnoreCase = sha256(Buffer.from([
    `SS.SLDPRT|${sha256('double-s')}\n`,
    `ß.SLDPRT|${sha256('sharp-s')}\n`,
  ].join(''), 'utf8'))
  assert.equal(
    canonicalFlatCadInventoryDigest(directory, { expectedFileCount: 2 }),
    expectedFromDotNetOrdinalIgnoreCase,
    '.NET StringComparer.OrdinalIgnoreCase treats ß and SS as distinct and orders SS before ß',
  )
})

await check('executor_source_has_no_legacy_or_non_assistance_vocabulary', () => {
  const source = readFileSync(resolve(ROOT, 'tools/lib/locker_16029_native_stage_executor.mjs'), 'utf8')
  const forbidden = /\b(?:generator|freecad|step|stp|fcstd|production|release)\b/i
  assert.equal(forbidden.test(source), false,
    `forbidden native executor vocabulary: ${source.match(forbidden)?.[0] || ''}`)
})

await check('temporary_fixture_is_removed', () => {
  rmSync(TEMP_ROOT, { recursive: true, force: true })
  assert.equal(existsSync(TEMP_ROOT), false)
})

const failed = checks.filter((row) => !row.ok && !row.skipped)
process.stdout.write(`${JSON.stringify({
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  mode: FIXTURE_ONLY ? 'fixture-only' : 'full',
  checksSkipped: checks.filter(row => row.skipped).length,
  solidWorksStarted: false,
  cadFilesOpened: false,
  checks,
}, null, 2)}\n`)
if (failed.length) process.exit(1)
