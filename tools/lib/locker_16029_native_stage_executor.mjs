import { createHash, randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import {
  closeSync,
  existsSync,
  fstatSync,
  fsyncSync,
  lstatSync,
  mkdirSync,
  openSync,
  readFileSync,
  readdirSync,
  realpathSync,
  renameSync,
  statSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  containFailedNativeExecutionAuthorization,
  consumeNativeExecutionAuthorization,
  issueNativeExecutionAuthorization,
  NATIVE_EXECUTION_TOOL_CONTRACTS,
} from './locker_16029_native_execution_authorization.mjs'
import { NATIVE_STAGE_RECEIPT_ORDER } from './locker_16029_native_stage_contract.mjs'
import { resolveTrustedNativeToolchainArtifacts } from './locker_16029_native_toolchain_manifests.mjs'

const REPOSITORY_ROOT = resolve(fileURLToPath(new URL('../..', import.meta.url)))
const V37_SOURCE = 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/native_cad/final_native_hierarchical_pack_and_go'
const WIDTH_PHASES = Object.freeze(['dimensions', 'derived', 'base-hole', 'assemblies'])
const LOCK_VALIDATION_SCHEMA = 'winnsen.locker16029.native_888x14_lock_topology_validation.v2'
const LOCK_VALIDATION_KEYS = Object.freeze([
  'schema', 'generatedAtUtc', 'purpose', 'status', 'structureEngineeringEvidencePass',
  'inputs', 'expectedRowsMm', 'checks', 'failedCheckCount', 'failedChecks', 'limitation',
])
const LOCK_VALIDATION_INPUT_KEYS = Object.freeze(['build', 'inspector', 'tongues'])
const LOCK_VALIDATION_INPUT_VALUE_KEYS = Object.freeze(['path', 'sha256'])
const SLDWORKS_INTEROP_SHA256 = 'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC'
const SWCONST_INTEROP_SHA256 = '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359'
const SHARED_INTEROP_CONFIG = `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="SolidWorks.Interop.sldworks" publicKeyToken="7c4797c3e4eeac03" culture="neutral" />
        <codeBase version="28.5.0.78" href="../bin/SolidWorks.Interop.sldworks.dll" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="SolidWorks.Interop.swconst" publicKeyToken="19f43e188e4269d8" culture="neutral" />
        <codeBase version="28.5.0.78" href="../bin/SolidWorks.Interop.swconst.dll" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>`
export const NATIVE_888X14_SOURCE_INVENTORY_DIGEST =
  '9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B'

const DOOR_SOURCES = Object.freeze({
  left_panel_seed: Object.freeze({
    path: 'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图/储物柜门板1╱12.SLDPRT',
    sha256: 'F34138A9BE009D5A0C68604F57B278584DB686623943C2DE319A2999ACA488A2', copy: true,
  }),
  stiffener_seed: Object.freeze({
    path: 'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图/柜门加强筋2╱12.SLDPRT',
    sha256: 'F4D4973F72CB604BF52BF802BC378E9BA042A4FE8034C196CA7666A964ACCB28', copy: true,
  }),
  latch_plate: Object.freeze({ path: '插销固定板.SLDPRT', sha256: '4B511C2FB2371060D998F2C0667C12980DF4A151E5CF4FDCD2934F7C7AAC1C5C' }),
  hook_pad: Object.freeze({ path: 'U型锁钩垫板.SLDPRT', sha256: '93378BDFB9666CDE8379F59A0506E7A4508AFAEDAC47121C3B8CBDAE56480834' }),
  bushing: Object.freeze({ path: '塑料轴套(云绅模具).SLDPRT', sha256: '930EE6070D278D5299D1EECED4F43367EE404A0E921517DE45297C30A8A12C7B' }),
  hinge_pin: Object.freeze({ path: '门轴销.SLDPRT', sha256: 'B7289A146C9827050573FACEF66DCA06919FC41B943C39548AF536FE21516BC0' }),
  circlip: Object.freeze({ path: '开口挡圈5.SLDPRT', sha256: '84ACBBAD72BC3DB175AD77C5DB5ED5F4A74410435C3CDEF1574B6A7808DCC754' }),
  mechanical_lock_tongue: Object.freeze({ path: '锁舌.SLDPRT', sha256: '26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719' }),
})

function fail(code, message) {
  const error = new Error(message)
  error.code = code
  throw error
}

function childStatusCode(stderr) {
  const match = String(stderr || '').match(/(?:^|\r?\n)([A-Z][A-Z0-9_]{2,95})(?=:)/)
  return match ? match[1] : ''
}

function writePrivateStageDiagnostic({ attemptDir, phase, authorizationId, commandId, result }) {
  const attempt = assertSafeDirectoryRoot(attemptDir, 'native attempt root')
  const diagnosticDirectory = safeAttemptPath(attempt, 'diagnostics', 'private diagnostic directory')
  if (!existsSync(diagnosticDirectory)) mkdirSync(diagnosticDirectory)
  assertSafeDirectoryRoot(diagnosticDirectory, 'private diagnostic directory')
  if (!underRoot(realpathSync.native(diagnosticDirectory), realpathSync.native(attempt))) {
    fail('NATIVE_EXECUTOR_DIAGNOSTIC_PATH_UNSAFE', 'private diagnostic directory escaped the native attempt')
  }
  const safePhase = safeIdentifier(phase, 'diagnostic phase')
  const safeAuthorizationId = safeIdentifier(authorizationId, 'diagnostic authorization')
  const diagnosticPath = safeAttemptPath(attempt,
    `diagnostics/${safePhase}-${safeAuthorizationId}.json`, 'private stage diagnostic')
  const payload = {
    schema: 'winnsen.native_private_stage_diagnostic.v1',
    phase: safePhase,
    authorizationId: safeAuthorizationId,
    commandId: safeIdentifier(commandId, 'diagnostic command'),
    exitCode: Number(result?.exitCode),
    signal: String(result?.signal || ''),
    statusCode: childStatusCode(result?.stderr),
    stdout: String(result?.stdout || ''),
    stderr: String(result?.stderr || ''),
    outputTruncated: result?.outputTruncated === true,
  }
  writeFileSync(diagnosticPath, `${JSON.stringify(payload, null, 2)}\n`, { encoding: 'utf8', flag: 'wx' })
  assertSafeExistingFile(diagnosticPath, attempt, 'private stage diagnostic')
  return diagnosticPath
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256(readFileSync(path))
}

function underRoot(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function safeIdentifier(value, label) {
  const normalized = String(value || '')
  if (!/^[A-Za-z0-9_.-]{1,160}$/.test(normalized)) {
    fail('NATIVE_EXECUTOR_IDENTIFIER_INVALID', `${label} is not a safe identifier`)
  }
  return normalized
}

function assertSafeExistingFile(path, root, label) {
  const absolute = resolve(path)
  const safeRoot = resolve(root)
  if (!underRoot(absolute, safeRoot) || absolute === safeRoot || !existsSync(absolute)) {
    fail('NATIVE_EXECUTOR_PATH_INVALID', `${label} is missing or outside its trusted root`)
  }
  const rel = relative(safeRoot, absolute)
  let current = safeRoot
  for (const segment of rel.split(sep)) {
    current = resolve(current, segment)
    if (lstatSync(current).isSymbolicLink()) fail('NATIVE_EXECUTOR_PATH_UNSAFE', `${label} path contains a link`)
  }
  const info = statSync(absolute)
  if (!info.isFile() || Number(info.nlink) !== 1 || !underRoot(realpathSync.native(absolute), realpathSync.native(safeRoot))) {
    fail('NATIVE_EXECUTOR_PATH_UNSAFE', `${label} must be a single-link regular file`)
  }
  return absolute
}

function samePath(left, right) {
  const a = resolve(left)
  const b = resolve(right)
  return process.platform === 'win32' ? a.toUpperCase() === b.toUpperCase() : a === b
}

function assertSafeDirectoryRoot(path, label) {
  const absolute = resolve(path)
  if (!existsSync(absolute)) fail('NATIVE_EXECUTOR_PATH_INVALID', `${label} is missing`)
  const linkInfo = lstatSync(absolute)
  if (linkInfo.isSymbolicLink()) fail('NATIVE_EXECUTOR_PATH_UNSAFE', `${label} must not be a link`)
  const info = statSync(absolute)
  if (!info.isDirectory() || !samePath(realpathSync.native(absolute), absolute)) {
    fail('NATIVE_EXECUTOR_PATH_UNSAFE', `${label} must be a real directory`)
  }
  return absolute
}

function safeAttemptPath(attemptDir, relativePath, label) {
  const root = resolve(attemptDir)
  const absolute = resolve(root, relativePath)
  if (!underRoot(absolute, root) || absolute === root) fail('NATIVE_EXECUTOR_PATH_INVALID', `${label} escaped the attempt`)
  return absolute
}

function command(id, executable, args, cwd, environmentKind = 'primary') {
  return Object.freeze({ id, executable, args: Object.freeze([...args]), cwd, shell: false, environmentKind })
}

function exactKeys(value, expected) {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
}

function exactArtifactMap(artifacts) {
  const expected = Object.keys(NATIVE_EXECUTION_TOOL_CONTRACTS)
  const actual = artifacts && typeof artifacts === 'object' && !Array.isArray(artifacts) ? Object.keys(artifacts) : []
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    fail('NATIVE_EXECUTOR_TOOLCHAIN_INVALID', 'toolchain artifact map must contain the exact six registered tools')
  }
  for (const id of expected) {
    const row = artifacts[id]
    if (row?.id !== id || row?.tool?.id !== id || !/^[A-F0-9]{64}$/i.test(String(row?.manifestSha256 || '')) ||
        !/^[A-F0-9]{64}$/i.test(String(row?.tool?.sourceNormalizedSha256 || '')) ||
        !/^[A-F0-9]{64}$/i.test(String(row?.tool?.executableSha256 || '')) || !isAbsolute(String(row?.tool?.executablePath || ''))) {
      fail('NATIVE_EXECUTOR_TOOLCHAIN_INVALID', `toolchain artifact identity is incomplete for ${id}`)
    }
  }
  return artifacts
}

export function validateNativeToolRuntimeDependencies({ toolId, tool, repositoryRoot = REPOSITORY_ROOT } = {}) {
  const safeToolId = safeIdentifier(toolId, 'runtime dependency tool')
  const executablePath = assertSafeExistingFile(tool?.executablePath, repositoryRoot, `${safeToolId} executable`)
  const executableDirectory = dirname(executablePath)
  const sharedInteropDirectory = resolve(repositoryRoot,
    'workers/native_model_requests/development/v1/tools/bin')
  const usesSharedInterop = safeToolId === 'native_seed_pack_888x14_v1' || safeToolId === 'native_width_888_v1'
  const interopDirectory = usesSharedInterop ? sharedInteropDirectory : executableDirectory
  const sldworksInterop = assertSafeExistingFile(resolve(interopDirectory, 'SolidWorks.Interop.sldworks.dll'),
    repositoryRoot, `${safeToolId} SolidWorks interop`)
  const swconstInterop = assertSafeExistingFile(resolve(interopDirectory, 'SolidWorks.Interop.swconst.dll'),
    repositoryRoot, `${safeToolId} SolidWorks constants interop`)
  if (sha256File(sldworksInterop) !== SLDWORKS_INTEROP_SHA256 ||
      sha256File(swconstInterop) !== SWCONST_INTEROP_SHA256) {
    fail('NATIVE_EXECUTOR_INTEROP_IDENTITY_INVALID', `${safeToolId} SolidWorks interop identity changed`)
  }
  let configPath = ''
  if (usesSharedInterop) {
    configPath = assertSafeExistingFile(`${executablePath}.config`, repositoryRoot, `${safeToolId} runtime config`)
    const config = readFileSync(configPath, 'utf8').replace(/\r\n?/g, '\n').trim()
    if (config !== SHARED_INTEROP_CONFIG) {
      fail('NATIVE_EXECUTOR_INTEROP_CONFIG_INVALID', `${safeToolId} runtime config changed`)
    }
  }
  return Object.freeze({ toolId: safeToolId, configPath, sldworksInterop, swconstInterop })
}

function receiptPath(plan, phase, attempt) {
  const row = plan?.stageReceiptContracts?.[phase]
  if (!row || row.producerToolId === undefined || typeof row.path !== 'string') {
    fail('NATIVE_EXECUTOR_STAGE_CONTRACT_INVALID', `missing receipt contract for ${phase}`)
  }
  return safeAttemptPath(attempt, row.path, `${phase} receipt`)
}

function ordinalCompare(left, right) {
  const limit = Math.min(left.length, right.length)
  for (let index = 0; index < limit; index += 1) {
    const difference = left.charCodeAt(index) - right.charCodeAt(index)
    if (difference) return difference
  }
  return left.length - right.length
}

function ordinalIgnoreCaseKey(value) {
  let result = ''
  for (let index = 0; index < value.length; index += 1) {
    const original = value[index]
    const upper = original.toUpperCase()
    // .NET OrdinalIgnoreCase uses one-to-one UTF-16 case mappings. JavaScript's
    // full Unicode mapping expands characters such as sharp-s to "SS"; keeping
    // the original code unit for expansions preserves the ordinal distinction.
    result += upper.length === 1 ? upper : original
  }
  return result
}

function ordinalIgnoreCase(left, right) {
  return ordinalCompare(ordinalIgnoreCaseKey(left), ordinalIgnoreCaseKey(right)) || ordinalCompare(left, right)
}

export function canonicalFlatCadInventoryDigest(directory, { expectedFileCount = null, fileOps = {} } = {}) {
  const root = resolve(directory)
  if (!existsSync(root) || !statSync(root).isDirectory() || lstatSync(root).isSymbolicLink()) {
    fail('NATIVE_EXECUTOR_INVENTORY_INVALID', 'CAD inventory root is not a safe directory')
  }
  const rows = []
  const namesIgnoreCase = new Set()
  const readDirectory = fileOps.readdirSync || readdirSync
  for (const entry of readDirectory(root, { withFileTypes: true })) {
    const nameIgnoreCase = ordinalIgnoreCaseKey(String(entry.name))
    if (namesIgnoreCase.has(nameIgnoreCase)) {
      fail('NATIVE_EXECUTOR_INVENTORY_INVALID', 'CAD inventory contains names that differ only by case')
    }
    namesIgnoreCase.add(nameIgnoreCase)
    const path = resolve(root, entry.name)
    if (entry.isDirectory()) fail('NATIVE_EXECUTOR_INVENTORY_INVALID', 'CAD inventory must remain flat')
    if (!entry.isFile() || lstatSync(path).isSymbolicLink()) fail('NATIVE_EXECUTOR_INVENTORY_INVALID', 'CAD inventory contains a non-regular entry')
    const extension = entry.name.slice(entry.name.lastIndexOf('.')).toUpperCase()
    if (extension !== '.SLDASM' && extension !== '.SLDPRT') fail('NATIVE_EXECUTOR_INVENTORY_INVALID', 'CAD inventory contains an unexpected file type')
    const info = statSync(path)
    if (Number(info.nlink) !== 1 || !underRoot(realpathSync.native(path), realpathSync.native(root))) {
      fail('NATIVE_EXECUTOR_INVENTORY_INVALID', 'CAD inventory contains a linked file')
    }
    rows.push({ name: entry.name, sha256: sha256File(path) })
  }
  rows.sort((left, right) => ordinalIgnoreCase(left.name, right.name))
  if (expectedFileCount !== null && rows.length !== Number(expectedFileCount)) {
    fail('NATIVE_EXECUTOR_INVENTORY_INVALID', `expected ${expectedFileCount} CAD files, found ${rows.length}`)
  }
  return sha256(Buffer.from(rows.map((row) => `${row.name}|${row.sha256}\n`).join(''), 'utf8'))
}

export function buildNative888x14ExecutionBlueprint({
  attemptDir,
  planPath,
  taskId,
  repositoryRoot = REPOSITORY_ROOT,
  toolchainArtifacts = resolveTrustedNativeToolchainArtifacts({ repositoryRoot }),
} = {}) {
  const root = resolve(repositoryRoot)
  const attempt = resolve(attemptDir)
  const safeTaskId = safeIdentifier(taskId, 'taskId')
  const expectedPlan = safeAttemptPath(attempt, 'native_build_plan.json', 'native build plan')
  if (resolve(planPath) !== expectedPlan) fail('NATIVE_EXECUTOR_PLAN_INVALID', 'native build plan must be the direct attempt artifact')
  assertSafeExistingFile(expectedPlan, attempt, 'native build plan')
  const plan = JSON.parse(readFileSync(expectedPlan, 'utf8'))
  const artifacts = exactArtifactMap(toolchainArtifacts)
  for (const row of Object.values(artifacts)) assertSafeExistingFile(row.tool.executablePath, root, `${row.id} executable`)
  const workingPack = safeAttemptPath(attempt, 'native_cad/working_pack', 'working pack')
  const doorRequest = safeAttemptPath(attempt, 'inputs/door_module_888x14.request.json', 'door request')
  const doorOutput = safeAttemptPath(attempt, 'native_cad/door_module_888x14', 'door output')
  const leftPanelProof = safeAttemptPath(attempt, 'evidence/left-panel-proof.v1.json', 'left panel proof')
  const unitDefinitions = [
    ['clone_native_seed', 'native_seed_pack_888x14_v1', 'seed-copy-and-stabilize'],
    ['dimensions', 'native_width_888_v1', 'width-dimensions'],
    ['derived', 'native_width_888_v1', 'width-derived'],
    ['base-hole', 'native_width_888_v1', 'width-base-hole'],
    ['assemblies', 'native_width_888_v1', 'width-assemblies'],
    ['door_module_888x14', 'native_door_module_888x14_v1', 'door-module-build'],
    ['lock_topology_888x14', 'native_lock_topology_888x14_v1', 'lock-topology-build'],
    ['root_assembly_888x14', 'native_root_assembly_888x14_v1', 'root-assembly-build'],
    ['final_pack_and_relocated_reopen', 'native_final_pack_888x14_v1', 'final-pack-and-relocated-reopen'],
  ]
  const units = unitDefinitions.map(([phase, toolId, commandId]) => {
    const tool = artifacts[toolId].tool
    const standard = command(commandId, tool.executablePath, [attempt, phase], dirname(tool.executablePath))
    let commands = [standard]
    if (WIDTH_PHASES.includes(phase)) {
      commands = [command(commandId, tool.executablePath, [workingPack, safeAttemptPath(attempt, `evidence/width-${phase}.json`, `${phase} evidence`), phase], dirname(tool.executablePath))]
    } else if (phase === 'clone_native_seed') {
      commands = [command(commandId, tool.executablePath, ['--source', resolve(root, V37_SOURCE), '--working-pack', workingPack, '--out', safeAttemptPath(attempt, 'evidence/seed_pack_888_native_v1.json', 'seed evidence'), '--confirm-task', safeTaskId], dirname(tool.executablePath))]
    } else if (phase === 'door_module_888x14') {
      commands = [
        command('door-left-panel-proof', tool.executablePath, ['--panel-proof', '--mode', 'execute', '--request', doorRequest, '--out', leftPanelProof, '--confirm-task', safeTaskId], dirname(tool.executablePath)),
        command(commandId, tool.executablePath, ['--mode', 'execute', '--request', doorRequest, '--out', safeAttemptPath(attempt, 'evidence/door_module_888x14.result.v1.json', 'door evidence'), '--confirm-task', safeTaskId], dirname(tool.executablePath)),
      ]
    } else if (phase === 'lock_topology_888x14') {
      commands = [command(commandId, tool.executablePath, [attempt], dirname(tool.executablePath))]
    } else if (phase === 'root_assembly_888x14') {
      commands = [command(commandId, tool.executablePath, [
        '--working-pack', workingPack,
        '--door-module', doorOutput,
        '--out', safeAttemptPath(attempt, 'evidence/root_assembly_888x14.result.v1.json', 'root evidence'),
        '--confirm-task', safeTaskId,
      ], dirname(tool.executablePath))]
    } else if (phase === 'final_pack_and_relocated_reopen') {
      commands = [command(commandId, tool.executablePath, [
        '--attempt', attempt,
        '--plan', expectedPlan,
        '--authorization', safeAttemptPath(attempt, 'execution_authorizations/native_final_pack_888x14_v1.json', 'final authorization'),
        '--confirm-task', safeTaskId,
      ], dirname(tool.executablePath))]
    }
    const outputDirectory = phase === 'final_pack_and_relocated_reopen' ? safeAttemptPath(attempt, 'final_native_package', 'final package') : workingPack
    return Object.freeze({
      id: `unit-${phase}`, toolId, phases: NATIVE_EXECUTION_TOOL_CONTRACTS[toolId], receiptPhase: phase,
      receiptPath: receiptPath(plan, phase, attempt), postInventoryDirectory: outputDirectory,
      expectedPostFileCount: phase === 'root_assembly_888x14' ? 88 : phase === 'final_pack_and_relocated_reopen' ? 55 : 75,
      ...(phase === 'door_module_888x14' ? { requestPath: doorRequest, outputDirectory: doorOutput, panelProofPath: leftPanelProof } : {}),
      commands: Object.freeze(commands),
    })
  })
  if (JSON.stringify(units.map((unit) => unit.receiptPhase)) !== JSON.stringify(NATIVE_STAGE_RECEIPT_ORDER)) {
    fail('NATIVE_EXECUTOR_STAGE_CONTRACT_INVALID', 'native receipt order drifted')
  }
  const lockAux = artifacts.native_lock_topology_888x14_v1.auxiliaries || {}
  const validation = Object.freeze({
    id: 'lock-validation', afterUnit: 'root_assembly_888x14', beforeUnit: 'final_pack_and_relocated_reopen',
    commands: Object.freeze([
      command('inspect-lock-topology', lockAux.native_lock_topology_inspector_v1?.executablePath, [attempt, '{lockBuildEvidenceSha256}'], dirname(lockAux.native_lock_topology_inspector_v1?.executablePath || root)),
      command('inspect-assembly-tongues', lockAux.native_assembly_tongue_inspector_888x14_v1?.executablePath, [attempt, '{lockBuildEvidenceSha256}', '{lockInspectorEvidenceSha256}'], dirname(lockAux.native_assembly_tongue_inspector_888x14_v1?.executablePath || root)),
      command('validate-lock-topology', process.execPath, [lockAux.validator?.path, '--build', safeAttemptPath(attempt, 'evidence/lock_topology_888x14.v2.json', 'lock build evidence'), '--inspector', safeAttemptPath(attempt, 'evidence/lock_topology_888x14.inspector.v1.json', 'lock inspector evidence'), '--tongues', safeAttemptPath(attempt, 'evidence/assembly_lock_tongues_888x14.json', 'tongue evidence'), '--output', safeAttemptPath(attempt, 'evidence/lock_topology_888x14.validation.v2.json', 'lock validation'), '--toolchain-manifest', artifacts.native_lock_topology_888x14_v1.manifestPath, '--toolchain-manifest-sha256', artifacts.native_lock_topology_888x14_v1.manifestSha256], dirname(lockAux.validator?.path || root), 'validation'),
    ]),
  })
  return Object.freeze({
    schema: 'winnsen.native_888x14_execution_blueprint.v1', purpose: 'structure_engineering_assistance', taskId: safeTaskId,
    attemptDir: attempt, attemptsRoot: dirname(dirname(attempt)), planPath: expectedPlan, source: resolve(root, V37_SOURCE),
    workingPack, evidenceDir: safeAttemptPath(attempt, 'evidence', 'evidence directory'), receiptDir: safeAttemptPath(attempt, 'receipts', 'receipt directory'),
    units: Object.freeze(units), validation,
    qualityBoundary: Object.freeze({ engineeringAssistanceReady: false, readyOnlyAfterEveryRequiredCheckPasses: true }),
  })
}

function createNewJson(path, value, label) {
  mkdirSync(dirname(path), { recursive: true })
  let descriptor = null
  try {
    descriptor = openSync(path, 'wx')
    writeFileSync(descriptor, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
  } catch (error) {
    if (error?.code === 'EEXIST') fail('NATIVE_EXECUTOR_CREATE_NEW_CONFLICT', `${label} already exists`)
    throw error
  } finally {
    if (descriptor !== null) closeSync(descriptor)
  }
}

function removeOwnedFiles(paths, removeFile) {
  let cleanupError = null
  for (const path of [...paths].reverse()) {
    if (!existsSync(path)) continue
    try { removeFile(path) } catch (error) { cleanupError ||= error }
  }
  return cleanupError
}

export function prepareNative888x14DoorInputs({
  blueprint,
  task,
  workerId,
  repositoryRoot = REPOSITORY_ROOT,
  fileOps = {},
} = {}) {
  if (!blueprint || blueprint.schema !== 'winnsen.native_888x14_execution_blueprint.v1') fail('NATIVE_EXECUTOR_STAGE_CONTRACT_INVALID', 'native blueprint is required')
  const root = resolve(repositoryRoot)
  const attempt = assertSafeDirectoryRoot(blueprint.attemptDir, 'native attempt root')
  const pack = resolve(blueprint.workingPack)
  const resolved = {}
  for (const [role, source] of Object.entries(DOOR_SOURCES)) {
    const path = source.copy ? resolve(root, source.path) : resolve(pack, basename(source.path))
    assertSafeExistingFile(path, source.copy ? root : attempt, `${role} source`)
    if (sha256File(path) !== source.sha256) fail('NATIVE_EXECUTOR_SOURCE_HASH_INVALID', `${role} source hash does not match`)
    resolved[role] = { source, path }
  }
  const sentinelPath = safeAttemptPath(attempt, '.winnsen-native-isolated-root.json', 'isolated root sentinel')
  const requestPath = safeAttemptPath(attempt, 'inputs/door_module_888x14.request.json', 'door request')
  if (existsSync(sentinelPath) || existsSync(requestPath)) fail('NATIVE_EXECUTOR_CREATE_NEW_CONFLICT', 'isolated root artifacts already exist')

  const finalCopies = Object.fromEntries(Object.entries(resolved)
    .filter(([, row]) => row.source.copy)
    .map(([role, row]) => [role, safeAttemptPath(attempt,
      `inputs/door_sources/${basename(row.path)}`, `${role} copy`)]))
  for (const [role, path] of Object.entries(finalCopies)) {
    if (existsSync(path)) fail('NATIVE_EXECUTOR_CREATE_NEW_CONFLICT', `${role} copy already exists`)
  }

  const writeOwnedFile = fileOps.writeOwnedFile || ((descriptor, bytes) => writeFileSync(descriptor, bytes))
  const moveFile = fileOps.renameSync || renameSync
  const removeFile = fileOps.unlinkSync || unlinkSync
  const commitJson = fileOps.createNewJson || createNewJson
  const ownedPaths = []
  const commitOwnedJson = (path, value, label) => {
    const expected = `${JSON.stringify(value, null, 2)}\n`
    try {
      commitJson(path, value, label)
    } catch (error) {
      if (error?.code !== 'NATIVE_EXECUTOR_CREATE_NEW_CONFLICT' && error?.code !== 'EEXIST') {
        try {
          if (existsSync(path)) ownedPaths.push(path)
        } catch {}
      }
      throw error
    }
    ownedPaths.push(path)
    if (readFileSync(path, 'utf8') !== expected) {
      fail('NATIVE_EXECUTOR_JSON_COMMIT_INVALID', `${label} content does not match`)
    }
  }
  const sources = {}
  const sentinel = {
    schema: 'winnsen.16029.native_isolated_root.v1',
    task_id: blueprint.taskId,
    allow_native_solidworks_write: true,
  }
  try {
    for (const [role, row] of Object.entries(resolved)) {
      let path = row.path
      if (row.source.copy) {
        path = finalCopies[role]
        mkdirSync(dirname(path), { recursive: true })
        const temporaryPath = resolve(dirname(path),
          `.${basename(path)}.tmp-${process.pid}-${randomBytes(8).toString('hex')}`)
        let descriptor = null
        let temporaryIdentity = null
        try {
          descriptor = openSync(temporaryPath, 'wx+')
          temporaryIdentity = fstatSync(descriptor)
          writeOwnedFile(descriptor, readFileSync(row.path), role)
          fsyncSync(descriptor)
          const liveIdentity = statSync(temporaryPath)
          if (Number(liveIdentity.dev) !== Number(temporaryIdentity.dev) ||
              Number(liveIdentity.ino) !== Number(temporaryIdentity.ino) ||
              Number(liveIdentity.nlink) !== 1 || lstatSync(temporaryPath).isSymbolicLink()) {
            fail('NATIVE_EXECUTOR_PATH_UNSAFE', `${role} temporary source identity changed during copy`)
          }
        } finally {
          if (descriptor !== null) closeSync(descriptor)
        }
        ownedPaths.push(temporaryPath)
        assertSafeExistingFile(temporaryPath, attempt, `${role} temporary source`)
        if (sha256File(temporaryPath) !== row.source.sha256) {
          fail('NATIVE_EXECUTOR_SOURCE_HASH_INVALID', `${role} temporary source hash does not match`)
        }
        try {
          moveFile(temporaryPath, path)
        } catch (error) {
          // A colliding final path is never ours merely because its bytes match.
          // Only claim it when the temporary name disappeared and the final name
          // retains the exact file identity created by this invocation.
          if (!existsSync(temporaryPath) && existsSync(path) && temporaryIdentity) {
            const finalIdentity = statSync(path)
            if (Number(finalIdentity.dev) === Number(temporaryIdentity.dev) &&
                Number(finalIdentity.ino) === Number(temporaryIdentity.ino)) ownedPaths.push(path)
          }
          throw error
        }
        ownedPaths.splice(ownedPaths.indexOf(temporaryPath), 1)
        ownedPaths.push(path)
        assertSafeExistingFile(path, attempt, `${role} copied source`)
      }
      if (sha256File(path) !== row.source.sha256) fail('NATIVE_EXECUTOR_SOURCE_HASH_INVALID', `${role} input hash does not match`)
      sources[role] = { path, sha256: row.source.sha256 }
    }

    commitOwnedJson(sentinelPath, sentinel, 'isolated root sentinel')
    assertSafeExistingFile(sentinelPath, attempt, 'isolated root sentinel')
    const request = {
      schema: 'winnsen.16029.native_door_module_request.v1', task_id: blueprint.taskId, worker_id: workerId,
      lease_id: task?.nativeBuild?.lease?.id, cabinet_width_mm: 888, door_width_mm: 381, door_height_mm: 1781 / 7,
      columns: 2, rows_per_column: 7, total_doors: 14, isolated_root: attempt,
      output_dir: safeAttemptPath(attempt, 'native_cad/door_module_888x14', 'door output'),
      left_panel_proof: safeAttemptPath(attempt, 'evidence/left-panel-proof.v1.json', 'left panel proof'),
      right_panel_proof: '', right_panel_strategy: 'solidworks_mirror_part', visible: false, sources,
    }
    commitOwnedJson(requestPath, request, 'door request')
    assertSafeExistingFile(requestPath, attempt, 'door request')
    return Object.freeze({ sentinelPath, requestPath, sources: Object.freeze(sources) })
  } catch (error) {
    const cleanupError = removeOwnedFiles(ownedPaths, removeFile)
    if (cleanupError) {
      const rollbackError = new Error('native door input rollback failed', { cause: error })
      rollbackError.code = 'NATIVE_EXECUTOR_INPUT_ROLLBACK_FAILED'
      rollbackError.primaryCode = String(error?.code || error?.name || 'Error')
      rollbackError.cleanupCode = String(cleanupError?.code || cleanupError?.name || 'Error')
      throw rollbackError
    }
    throw error
  }
}

function childEnvironment(baseEnvironment, overrides) {
  const allowed = [
    'SystemRoot', 'WINDIR', 'TEMP', 'TMP', 'PATH', 'COMSPEC', 'PATHEXT',
    'PROCESSOR_ARCHITECTURE', 'ProgramFiles', 'ProgramFiles(x86)', 'ProgramW6432',
    'PROGRAMDATA', 'ALLUSERSPROFILE', 'LOCALAPPDATA', 'APPDATA',
    'HOMEDRIVE', 'HOMEPATH', 'USERNAME', 'USERPROFILE',
  ]
  const output = {}
  for (const key of allowed) if (baseEnvironment?.[key] !== undefined) output[key] = String(baseEnvironment[key])
  return { ...output, ...overrides }
}

export async function runTrustedNativeProcess({
  executable,
  args,
  cwd,
  env,
  onHeartbeat = null,
  heartbeatMs = 30_000,
  timeoutMs = 30 * 60 * 1000,
  maxOutputBytes = 1024 * 1024,
} = {}) {
  const timeoutDuration = Number(timeoutMs)
  if (!Number.isInteger(timeoutDuration) || timeoutDuration < 1) {
    fail('NATIVE_EXECUTOR_PROCESS_TIMEOUT_INVALID', 'native child process timeout must be a positive integer')
  }
  return await new Promise((resolveRun, rejectRun) => {
    let stdout = ''
    let stderr = ''
    let outputBytes = 0
    let heartbeat = null
    let timeout = null
    let timeoutError = null
    const child = spawn(executable, args, { cwd, env, shell: false, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] })
    const append = (kind, chunk) => {
      outputBytes += chunk.length
      if (outputBytes > maxOutputBytes) return
      if (kind === 'stdout') stdout += chunk.toString('utf8')
      else stderr += chunk.toString('utf8')
    }
    const onStdout = (chunk) => append('stdout', chunk)
    const onStderr = (chunk) => append('stderr', chunk)
    const cleanup = () => {
      if (heartbeat) clearInterval(heartbeat)
      if (timeout) clearTimeout(timeout)
      heartbeat = null
      timeout = null
      child.stdout?.removeListener('data', onStdout)
      child.stderr?.removeListener('data', onStderr)
      child.removeListener('error', onError)
      child.removeListener('close', onClose)
    }
    const onError = (error) => {
      if (timeoutError) {
        timeoutError.cause ||= error
        return
      }
      cleanup()
      rejectRun(error)
    }
    const onClose = (exitCode, signal) => {
      cleanup()
      if (timeoutError) {
        rejectRun(timeoutError)
        return
      }
      resolveRun({ exitCode: Number(exitCode), signal: signal || '', stdout, stderr, outputTruncated: outputBytes > maxOutputBytes })
    }
    child.stdout.on('data', onStdout)
    child.stderr.on('data', onStderr)
    child.once('error', onError)
    child.once('close', onClose)
    if (typeof onHeartbeat === 'function') {
      heartbeat = setInterval(() => {
        try { onHeartbeat() } catch {}
      }, heartbeatMs)
      heartbeat.unref?.()
    }
    timeout = setTimeout(() => {
      timeoutError = new Error(`native child process exceeded ${timeoutDuration} ms`)
      timeoutError.code = 'NATIVE_EXECUTOR_PROCESS_TIMEOUT'
      timeoutError.timeoutMs = timeoutDuration
      try {
        child.kill()
      } catch (error) {
        timeoutError.cause = error
        // Fail closed: the authorization remains owned by this call until the
        // exact child handle reports close. Returning while it may still be
        // running would let the caller contain the authorization too early.
      }
    }, timeoutDuration)
  })
}

export async function runAuthorizedNativeStageUnit({
  blueprint, unit, task, workerId, attempt, seedInventoryDigest, preInventoryDigest,
  toolchainArtifacts, baseEnvironment = process.env,
  clock = () => new Date(), ttlMs = 30 * 60 * 1000, issueAuthorization = issueNativeExecutionAuthorization,
  consumeAuthorization = consumeNativeExecutionAuthorization, containAuthorization = containFailedNativeExecutionAuthorization,
  runProcess = runTrustedNativeProcess, inventoryDigest = canonicalFlatCadInventoryDigest,
  validateRuntimeDependencies = validateNativeToolRuntimeDependencies,
  isCancellationRequested = () => false, onInstanceLockHeartbeat = null,
} = {}) {
  if (!blueprint || blueprint.schema !== 'winnsen.native_888x14_execution_blueprint.v1' || !unit || !Array.isArray(blueprint.units) || !blueprint.units.includes(unit)) {
    fail('NATIVE_EXECUTOR_STAGE_CONTRACT_INVALID', 'a registered native stage unit is required')
  }
  if (isCancellationRequested()) fail('NATIVE_EXECUTOR_CANCEL_REQUESTED', 'native task cancellation was requested')
  const artifacts = exactArtifactMap(toolchainArtifacts)
  const artifact = artifacts[unit.toolId]
  const authorizationInventoryDigest = String(
    preInventoryDigest || seedInventoryDigest || '',
  ).toUpperCase()
  if (!/^[A-F0-9]{64}$/.test(authorizationInventoryDigest)) {
    fail('NATIVE_EXECUTOR_INVENTORY_INVALID',
      'the current pre-stage inventory digest is required for authorization')
  }
  validateRuntimeDependencies({ toolId: unit.toolId, tool: artifact.tool })
  const issued = issueAuthorization({ attemptDir: blueprint.attemptDir, planPath: blueprint.planPath, task, workerId, tool: artifact.tool, seedInventoryDigest: authorizationInventoryDigest, attempt, phases: unit.phases, clock, ttlMs })
  const env = childEnvironment(baseEnvironment, {
    WINNSEN_NATIVE_16029_ATTEMPTS_ROOT: blueprint.attemptsRoot, WINNSEN_NATIVE_ATTEMPT_ROOT: blueprint.attemptsRoot,
    WINNSEN_NATIVE_16029_PLAN_SHA256: issued.authorization.plan.sha256, WINNSEN_NATIVE_PLAN_SHA256: issued.authorization.plan.sha256,
    WINNSEN_NATIVE_EXECUTION_AUTH_PATH: issued.authorizationPath, WINNSEN_NATIVE_EXECUTION_AUTH_SHA256: issued.sha256,
  })
  const results = []
  try {
    for (const row of unit.commands) {
      const result = await runProcess({ ...row, env, onHeartbeat: onInstanceLockHeartbeat })
      results.push(Object.freeze({ commandId: row.id, ...result }))
      if (Number(result?.exitCode) !== 0 || result?.signal) {
        const statusCode = childStatusCode(result?.stderr)
        const error = new Error(`${row.id} exited with code ${result?.exitCode}${statusCode ? ` (${statusCode})` : ''}`)
        error.code = 'NATIVE_EXECUTOR_PROCESS_FAILED'
        error.childStatusCode = statusCode
        error.childExitCode = Number(result?.exitCode)
        try {
          error.privateDiagnosticPath = writePrivateStageDiagnostic({
            attemptDir: blueprint.attemptDir,
            phase: unit.receiptPhase,
            authorizationId: issued.authorization.authorizationId,
            commandId: row.id,
            result,
          })
        } catch (diagnosticError) {
          error.privateDiagnosticCode = String(diagnosticError?.code || diagnosticError?.name || 'Error')
        }
        throw error
      }
    }
    const postInventoryDigest = inventoryDigest(unit.postInventoryDirectory, { expectedFileCount: unit.expectedPostFileCount })
    const consumed = consumeAuthorization({ attemptDir: blueprint.attemptDir, planPath: blueprint.planPath, task, workerId, tool: artifact.tool, seedInventoryDigest: authorizationInventoryDigest, attempt, phases: unit.phases, authorizationPath: issued.authorizationPath, receiptPath: unit.receiptPath, expectedPhase: unit.receiptPhase, expectedPostInventoryDigest: postInventoryDigest, clock })
    return Object.freeze({ unitId: unit.id, authorizationId: issued.authorization.authorizationId, authorizationSha256: issued.sha256, postInventoryDigest, commands: Object.freeze(results), consumed })
  } catch (error) {
    try {
      await containAuthorization({ attemptDir: blueprint.attemptDir, planPath: blueprint.planPath, task, workerId, tool: artifact.tool, seedInventoryDigest: authorizationInventoryDigest, attempt, phases: unit.phases, authorizationPath: issued.authorizationPath, failedPhase: unit.receiptPhase })
    } catch (containmentError) {
      const combined = new Error('native stage failed and its authorization could not be contained', { cause: error })
      combined.code = 'NATIVE_EXECUTOR_FAILURE_CONTAINMENT_FAILED'
      combined.primaryCode = String(error?.code || error?.name || 'Error')
      combined.containmentCode = String(containmentError?.code || containmentError?.name || 'Error')
      combined.containmentCause = containmentError
      throw combined
    }
    throw error
  }
}

function checkedResult(result, id) {
  if (Number(result?.exitCode) !== 0 || result?.signal) fail('NATIVE_EXECUTOR_LOCK_VALIDATION_FAILED', `${id} exited with code ${result?.exitCode}`)
}

export async function runNative888x14LockValidation({ blueprint, toolchainArtifacts, runProcess = runTrustedNativeProcess, baseEnvironment = process.env, onInstanceLockHeartbeat = null } = {}) {
  if (!blueprint || blueprint.schema !== 'winnsen.native_888x14_execution_blueprint.v1') fail('NATIVE_EXECUTOR_STAGE_CONTRACT_INVALID', 'native blueprint is required')
  exactArtifactMap(toolchainArtifacts)
  const attempt = blueprint.attemptDir
  const buildPath = safeAttemptPath(attempt, 'evidence/lock_topology_888x14.v2.json', 'lock build evidence')
  const inspectorPath = safeAttemptPath(attempt, 'evidence/lock_topology_888x14.inspector.v1.json', 'lock inspector evidence')
  const tonguesPath = safeAttemptPath(attempt, 'evidence/assembly_lock_tongues_888x14.json', 'tongue evidence')
  const validationPath = safeAttemptPath(attempt, 'evidence/lock_topology_888x14.validation.v2.json', 'lock validation')
  assertSafeExistingFile(buildPath, attempt, 'lock build evidence')
  const [inspect, tongues, validate] = blueprint.validation.commands
  const env = childEnvironment(baseEnvironment, { WINNSEN_NATIVE_ATTEMPT_ROOT: attempt })
  checkedResult(await runProcess({ ...inspect, args: [attempt, sha256File(buildPath)], env, onHeartbeat: onInstanceLockHeartbeat }), inspect.id)
  assertSafeExistingFile(inspectorPath, attempt, 'lock inspector evidence')
  checkedResult(await runProcess({ ...tongues, args: [attempt, sha256File(buildPath), sha256File(inspectorPath)], env, onHeartbeat: onInstanceLockHeartbeat }), tongues.id)
  assertSafeExistingFile(tonguesPath, attempt, 'tongue evidence')
  checkedResult(await runProcess({ ...validate, env, onHeartbeat: onInstanceLockHeartbeat }), validate.id)
  assertSafeExistingFile(validationPath, attempt, 'lock validation')
  let output
  try { output = JSON.parse(readFileSync(validationPath, 'utf8')) } catch { fail('NATIVE_EXECUTOR_LOCK_VALIDATION_FAILED', 'lock validation output is invalid') }
  const inputs = output?.inputs
  const inputShapeMatches = exactKeys(inputs, LOCK_VALIDATION_INPUT_KEYS) &&
    LOCK_VALIDATION_INPUT_KEYS.every((key) => exactKeys(inputs[key], LOCK_VALIDATION_INPUT_VALUE_KEYS))
  const matches = exactKeys(output, LOCK_VALIDATION_KEYS) &&
    output.schema === LOCK_VALIDATION_SCHEMA && output.purpose === 'structure_engineering_assistance' &&
    output.status === 'PASS' && output.structureEngineeringEvidencePass === true &&
    output.failedCheckCount === 0 && Array.isArray(output.failedChecks) && output.failedChecks.length === 0 &&
    inputShapeMatches &&
    inputs?.build?.path === buildPath && String(inputs.build.sha256 || '').toUpperCase() === sha256File(buildPath) &&
    inputs?.inspector?.path === inspectorPath && String(inputs.inspector.sha256 || '').toUpperCase() === sha256File(inspectorPath) &&
    inputs?.tongues?.path === tonguesPath && String(inputs.tongues.sha256 || '').toUpperCase() === sha256File(tonguesPath)
  if (!matches) fail('NATIVE_EXECUTOR_LOCK_VALIDATION_FAILED', 'lock validation did not prove the live evidence set')
  return Object.freeze(output)
}
