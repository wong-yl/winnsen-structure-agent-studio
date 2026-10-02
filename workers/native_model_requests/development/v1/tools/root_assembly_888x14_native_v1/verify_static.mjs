import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import {
  existsSync,
  lstatSync,
  readFileSync,
  realpathSync,
  renameSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { basename, dirname, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from '../../../../../../tools/locker_16029_native_recipe_registry.mjs'
import { buildNativeAssemblyContract } from '../../../../../../tools/lib/locker_16029_native_assembly_contract.mjs'
import {
  NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS,
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_SEED_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  NATIVE_TOOLCHAIN_MANIFEST_PATHS,
  buildNativeStageReceiptContracts,
  nativeEvidenceCommitmentSha256,
} from '../../../../../../tools/lib/locker_16029_native_stage_contract.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '..', '..', '..', '..', '..', '..')
const sourcePath = join(here, 'NativeRootAssembly888x14.cs')
const executablePath = join(here, 'NativeRootAssembly888x14.exe')
const verifierPath = fileURLToPath(import.meta.url)
const snapshotPath = join(here, 'assembly_contract.snapshot.json')
const manifestPath = join(here, 'toolchain_manifest.json')
const sharedToolsDir = join(repoRoot, 'workers', 'native_model_requests', 'development', 'v1', 'tools')
const lockManifestPath = join(sharedToolsDir, 'lock_toolchain_manifest.json')
const lockValidatorPath = join(sharedToolsDir, 'ValidateLockTopology888x14.mjs')
const lockToolSpecs = Object.freeze({
  native_lock_topology_888x14_v1: Object.freeze({
    source: join(sharedToolsDir, 'BuildLockTopology888x14.cs'),
    executable: join(sharedToolsDir, 'bin', 'BuildLockTopology888x14.exe'),
  }),
  native_lock_topology_inspector_v1: Object.freeze({
    source: join(sharedToolsDir, 'InspectLockTopology888x14.cs'),
    executable: join(sharedToolsDir, 'bin', 'InspectLockTopology888x14.exe'),
  }),
  native_assembly_tongue_inspector_888x14_v1: Object.freeze({
    source: join(sharedToolsDir, 'InspectAssemblyTongues888x14.cs'),
    executable: join(sharedToolsDir, 'bin', 'InspectAssemblyTongues888x14.exe'),
  }),
})
const reportPath = join(here, 'static_verification.json')
const recipeId = 'winnsen-16029-888w-14door-native-v1'
const recipeDigest = 'f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8'
const toolId = 'native_root_assembly_888x14_v1'

function sha256Bytes(bytes) {
  return createHash('sha256').update(bytes).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256Bytes(readFileSync(path))
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

function stableJson(value) {
  return JSON.stringify(stableValue(value))
}

function exactKeys(value, expected) {
  return value && typeof value === 'object' && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
}

function normalizeIdentitySource(source) {
  return source.replace(/\r\n?/g, '\n')
    .replace(/private const string ExpectedSourceSha256\s*=\s*"(?:__SOURCE_SHA256__|[0-9A-F]{64})";/,
      (match) => match.replace(/(?:__SOURCE_SHA256__|[0-9A-F]{64})/, '__SOURCE_SHA256__'))
    .replace(/private const string ExpectedContractSnapshotSha256\s*=\s*"(?:__CONTRACT_SHA256__|[0-9A-F]{64})";/,
      (match) => match.replace(/(?:__CONTRACT_SHA256__|[0-9A-F]{64})/, '__CONTRACT_SHA256__'))
}

function normalizedSourceSha256(path) {
  return sha256Bytes(Buffer.from(normalizeIdentitySource(readFileSync(path, 'utf8')), 'utf8'))
}

function normalizedLockSourceSha256(path) {
  const source = readFileSync(path, 'utf8')
    .replace(/private const string ExpectedSourceSha256\s*=\s*"(?:__SOURCE_SHA256__|[0-9A-F]{64})";/,
      (match) => match.replace(/(?:__SOURCE_SHA256__|[0-9A-F]{64})/, '__SOURCE_SHA256__'))
  return sha256Bytes(Buffer.from(source, 'utf8'))
}

function under(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith(`..${sep}`) && rel !== '..')
}

function safeSingleLinkFile(path) {
  if (!existsSync(path) || !under(path, repoRoot)) return false
  const info = lstatSync(path)
  if (!info.isFile() || info.isSymbolicLink() || Number(statSync(path).nlink) !== 1) return false
  return under(realpathSync.native(path), realpathSync.native(repoRoot))
}

function sameResolvedPath(left, right) {
  return resolve(String(left ?? '')).toLowerCase() === resolve(String(right ?? '')).toLowerCase()
}

function pathChainHasNoReparse(path) {
  let current = resolve(path)
  const root = resolve(repoRoot)
  for (let guard = 0; guard < 64 && under(current, root); guard += 1) {
    const info = lstatSync(current)
    if (info.isSymbolicLink() || !sameResolvedPath(realpathSync.native(current), current)) return false
    if (sameResolvedPath(current, root)) return true
    const parent = dirname(current)
    if (sameResolvedPath(parent, current)) return false
    current = parent
  }
  return false
}

function safeExactRepositoryFile(actualPath, expectedPath) {
  return typeof actualPath === 'string' && sameResolvedPath(actualPath, expectedPath) &&
    safeSingleLinkFile(expectedPath) && pathChainHasNoReparse(expectedPath)
}

function lockCompositeManifestIsLive() {
  try {
    if (!safeExactRepositoryFile(lockManifestPath, lockManifestPath)) return false
    const document = JSON.parse(readFileSync(lockManifestPath, 'utf8'))
    if (!exactKeys(document, ['schema', 'generatedBy', 'tools', 'validator']) ||
        document.schema !== 'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1' ||
        document.generatedBy !== 'VerifyLockTopology888x14Static.mjs' ||
        !exactKeys(document.tools, Object.keys(lockToolSpecs)) ||
        !exactKeys(document.validator, ['path', 'sha256']) ||
        !safeExactRepositoryFile(document.validator.path, lockValidatorPath) ||
        document.validator.sha256 !== sha256File(lockValidatorPath)) return false
    return Object.entries(lockToolSpecs).every(([id, spec]) => {
      const tool = document.tools[id]
      return exactKeys(tool, ['sourcePath', 'sourceNormalizedSha256', 'executablePath',
        'executableSha256']) && safeExactRepositoryFile(tool.sourcePath, spec.source) &&
        safeExactRepositoryFile(tool.executablePath, spec.executable) &&
        tool.sourceNormalizedSha256 === normalizedLockSourceSha256(spec.source) &&
        tool.executableSha256 === sha256File(spec.executable)
    })
  } catch {
    return false
  }
}

function repoRelative(path) {
  return relative(repoRoot, path).split(sep).join('/')
}

function processSnapshot() {
  const command = "$ErrorActionPreference='SilentlyContinue'; " +
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue) | " +
    "Sort-Object ProcessName,Id | ForEach-Object { " +
    "'{0}|{1}|{2}' -f $_.ProcessName,$_.Id,$_.StartTime.ToUniversalTime().Ticks }; exit 0"
  const result = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], {
    encoding: 'utf8', windowsHide: true, timeout: 10_000,
  })
  if (result.status !== 0) return [`SNAPSHOT_ERROR:${result.stderr}`]
  return result.stdout.split(/\r?\n/).map((value) => value.trim()).filter(Boolean)
}

function runExecutable(args) {
  return spawnSync(executablePath, args, {
    encoding: 'utf8', windowsHide: true, timeout: 15_000,
  })
}

const checks = []
function add(name, ok, actual, expected) {
  checks.push({ name, ok: Boolean(ok), actual, expected })
}

let source = ''
let snapshot = null
let manifest = null
let liveContract = null
let stageContracts = null
let beforeProcesses = []
let afterProcesses = []

try {
  add('static_artifacts_are_single_link_repository_files',
    [sourcePath, executablePath, verifierPath, snapshotPath, manifestPath].every(safeSingleLinkFile),
    [sourcePath, executablePath, verifierPath, snapshotPath, manifestPath],
    { regular: true, singleLink: true, repositoryLocal: true })

  source = readFileSync(sourcePath, 'utf8')
  snapshot = JSON.parse(readFileSync(snapshotPath, 'utf8'))
  manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY[recipeId]
  liveContract = buildNativeAssemblyContract(recipe)
  stageContracts = buildNativeStageReceiptContracts(recipe)

  add('trusted_recipe_digest_is_current', nativeRecipeDigest(recipe) === recipeDigest,
    nativeRecipeDigest(recipe), recipeDigest)
  add('assembly_snapshot_exactly_matches_live_contract', stableJson(snapshot) === stableJson(liveContract),
    sha256File(snapshotPath), { exactLiveContract: true })
  add('assembly_snapshot_exact_counts', snapshot?.doors?.length === 14 &&
    snapshot?.shelves?.length === 12 && snapshot?.frameCrossbars?.length === 12 &&
    snapshot?.expectedWorkingAssembly?.topLevelComponentCount === 41 &&
    snapshot?.expectedWorkingAssembly?.recursiveComponentCount === 294 &&
    snapshot?.expectedFinalClosure?.cadFileCount === 55 &&
    snapshot?.expectedFinalClosure?.assemblyFileCount === 14 &&
    snapshot?.expectedFinalClosure?.partFileCount === 41,
  {
    doors: snapshot?.doors?.length, shelves: snapshot?.shelves?.length,
    crossbars: snapshot?.frameCrossbars?.length,
    working: snapshot?.expectedWorkingAssembly, closure: snapshot?.expectedFinalClosure,
  }, { doors: 14, shelves: 12, crossbars: 12, top: 41, recursive: 294, closure: '55=14+41' })
  add('assembly_snapshot_feature_health_policy_is_exact_unique_tuple_only',
    stableJson(snapshot?.featureHealthPolicy) === stableJson({
      mode: 'exact_unique_known_issue_only',
      maximumIssueCount: 1,
      allowedIssue: {
        name: '箱体右侧板焊接-1', type: 'Reference', errorCode: 51,
        errorCode2: 51, warning: true,
      },
    }),
  snapshot?.featureHealthPolicy, { exactPolicyKeys: 3, exactTupleKeys: 5, maximumIssueCount: 1 })

  add('shared_nine_stage_contract_exact',
    exactKeys(stageContracts, NATIVE_STAGE_RECEIPT_ORDER) &&
    NATIVE_STAGE_RECEIPT_ORDER.every((id) => stableJson(stageContracts[id]) ===
      stableJson(snapshot?.stageReceiptContracts?.[id] ?? stageContracts[id])) &&
    stageContracts.root_assembly_888x14.path === 'receipts/root_assembly_888x14.json' &&
    NATIVE_RUNTIME_RECEIPT_KEYS.length === 28 && NATIVE_SEED_RECEIPT_KEYS.length === 42,
  { order: Object.keys(stageContracts), runtimeKeys: NATIVE_RUNTIME_RECEIPT_KEYS.length,
    seedKeys: NATIVE_SEED_RECEIPT_KEYS.length },
  { order: NATIVE_STAGE_RECEIPT_ORDER, runtimeKeys: 28, seedKeys: 42 })
  add('shared_toolchain_paths_exact',
    exactKeys(NATIVE_TOOLCHAIN_MANIFEST_PATHS, [
      'native_seed_pack_888x14_v1', 'native_width_888_v1',
      'native_door_module_888x14_v1', 'native_lock_topology_888x14_v1',
      'native_root_assembly_888x14_v1', 'native_final_pack_888x14_v1',
    ]) && NATIVE_TOOLCHAIN_MANIFEST_PATHS[toolId] === repoRelative(manifestPath) &&
    NATIVE_TOOLCHAIN_MANIFEST_PATHS.native_lock_topology_888x14_v1 ===
      'workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json',
  NATIVE_TOOLCHAIN_MANIFEST_PATHS, { sharedExactSix: true })

  const sourceSha = normalizedSourceSha256(sourcePath)
  const embeddedSource = source.match(/ExpectedSourceSha256 = "([A-F0-9]{64})";/)?.[1]
  const embeddedSnapshot = source.match(/ExpectedContractSnapshotSha256 = "([A-F0-9]{64})";/)?.[1]
  add('embedded_source_and_contract_hashes_are_live', embeddedSource === sourceSha &&
    embeddedSnapshot === sha256File(snapshotPath),
  { embeddedSource, sourceSha, embeddedSnapshot, snapshotSha: sha256File(snapshotPath) },
  { exact: true })

  add('toolchain_manifest_exact_and_live',
    exactKeys(manifest, ['schema', 'generatedBy', 'tool']) &&
    manifest.schema === 'winnsen.16029.native_toolchain_manifest.v1' &&
    manifest.generatedBy === 'Build-NativeRootAssembly888x14.ps1' &&
    exactKeys(manifest.tool, ['id', 'sourcePath', 'sourceNormalizedSha256', 'executablePath',
      'executableSha256', 'verifierPath', 'verifierSha256']) &&
    manifest.tool.id === toolId && manifest.tool.sourcePath === repoRelative(sourcePath) &&
    manifest.tool.executablePath === repoRelative(executablePath) &&
    manifest.tool.verifierPath === repoRelative(verifierPath) &&
    manifest.tool.sourceNormalizedSha256 === sourceSha &&
    manifest.tool.executableSha256 === sha256File(executablePath) &&
    manifest.tool.verifierSha256 === sha256File(verifierPath),
  manifest, { exactLiveToolchain: true })

  const identityMaskFixture =
    'private const string ExpectedSourceSha256="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";\r\n' +
    'private const string ExpectedContractSnapshotSha256 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";\r\n'
  add('source_normalization_masks_source_and_contract_constants',
    normalizeIdentitySource(identityMaskFixture) ===
      'private const string ExpectedSourceSha256="__SOURCE_SHA256__";\n' +
      'private const string ExpectedContractSnapshotSha256 = "__CONTRACT_SHA256__";\n',
  normalizeIdentitySource(identityMaskFixture), { source: '__SOURCE_SHA256__', contract: '__CONTRACT_SHA256__' })

  add('lock_composite_manifest_contract_is_live_and_runtime_supported',
    lockCompositeManifestIsLive() &&
      source.includes('ValidateLockCompositeToolchainManifest') &&
      source.includes('winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1') &&
      source.includes('native_lock_topology_inspector_v1') &&
      source.includes('native_assembly_tongue_inspector_888x14_v1'),
  { live: lockCompositeManifestIsLive(), runtimeSpecialCase: source.includes('ValidateLockCompositeToolchainManifest') },
  { exactCompositeLockOnly: true, fixedRepositoryPaths: true, liveHashes: true,
    noReparseOrHardlink: true })

  add('native_root_executor_identity_is_exact',
    source.includes('private const string ToolId = "native_root_assembly_888x14_v1";') &&
    source.includes('private const string ExecutionPhase = "root_assembly_888x14";') &&
    source.includes(`"${recipeDigest}"`) &&
    source.includes('private const string Purpose = "structure_engineering_assistance";') &&
    !source.includes('ConfigureFourDoorAssemblyV36') && !source.includes('ConfigureWidthV37'),
  { toolId, phase: 'root_assembly_888x14', recipeDigest },
  { noOldGeneratorReuse: true })

  add('solidworks_2020_and_process_identity_are_pinned',
    source.includes('Type.GetTypeFromProgID("SldWorks.Application.28", true)') &&
    source.includes('D:\\soildworks2020\\SOLIDWORKS\\SLDWORKS.exe') &&
    source.includes('1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978') &&
    source.includes('D:\\soildworks2020\\SOLIDWORKS\\sldProcMon.exe') &&
    source.includes('A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70') &&
    source.includes('ProcessStartUtcTicks') && source.includes('ExactOwnedMonitor'),
  { progId: 'SldWorks.Application.28', executableAndMonitorPinned: true },
  { SW2020: true, pidStartExeHashParentMonitor: true })

  add('exact_direct_name_mutation_only',
    source.includes('DeleteExactDirectComponents') && source.includes('DirectComponents(model)') &&
    source.includes('exactNames.Count == expectedCount') &&
    source.includes('recursivePatternDeleteForbidden') &&
    !source.includes('SelectByID2(') && !source.includes('SelectByRay('),
  { directExactDelete: true, forbiddenSelectionApisAbsent: true },
  { noPatternOrRecursiveDelete: true })

  add('transaction_and_rollback_order_is_static',
    source.indexOf('result.transaction.mutationStarted = true;') <
      source.indexOf('ImportDoorModule(result);') &&
    source.includes('CreateCompleteBackup(result);') &&
    source.includes('FileLinkCount(target) == 1') &&
    source.includes('RollBack(result);') && source.includes('CaptureFlatCadTree(result.workingPack, 75') &&
    source.includes('RemoveOrQuarantineUnpairedArtifact(result, result.evidence.path'),
  { backupBeforeMutation: true, partialImportRollback: true, unpairedEvidenceHandled: true },
  { exact75RollbackAndExact13Removal: true })

  const evidenceCommit = source.indexOf('WriteJsonAtomicNew(result.evidence.path, result);')
  const receiptCommit = source.indexOf('WriteRootReceiptAtomic(result);')
  const manifestCommit = source.indexOf('WriteRootManifestAtomic(result);')
  add('evidence_receipt_manifest_commit_order_is_exact', evidenceCommit >= 0 &&
    receiptCommit > evidenceCommit && manifestCommit > receiptCommit &&
    source.includes('evidenceSha256') && source.includes('evidenceCommitmentSha256') &&
    source.includes('predecessorReceiptSha256') && source.includes('authorizationJsonBase64'),
  { evidenceCommit, receiptCommit, manifestCommit },
  { evidenceThenReceiptThenManifest: true })

  add('root_quality_gates_are_exact',
    (source.match(/warnings == 32/g) ?? []).length >= 2 &&
    source.includes('snapshot.topLevelComponentCount') && source.includes('snapshot.recursiveComponentCount') &&
    source.includes('snapshot.activeRecursiveComponentCount') && source.includes('snapshot.suppressedComponentCount') &&
    source.includes('snapshot.doors.Count == 14') && source.includes('snapshot.shelves.Count == 12') &&
    source.includes('snapshot.tongues.Count == 14') && source.includes('snapshot.activeFrameCrossbars == 12') &&
    source.includes('RootFeatureHealthAllowed(snapshot.featureHealth, result.contractSnapshot)') &&
    source.includes('maximumIssueCount') && source.includes('allowedIssue') &&
    source.includes('issue.errorCode == Number(allowed, "errorCode")') &&
    source.includes('issue.errorCode2 == Number(allowed, "errorCode2")') &&
    source.includes('issue.warning == Bool(allowed, "warning")'),
  { rootOpenWarnings: 32, topology: '41/294/294/0,14/12/12/14', issue: 'Reference/51/51/warning' },
  { exact: true })

  add('door_and_lock_receipt_inputs_are_fixed',
    source.includes('evidence/door_module_888x14.result.v1.json') &&
    source.includes('evidence/lock_topology_888x14.v2.json') &&
    source.includes('door_module_flat_import_manifest.json') &&
    source.includes('workingPackImportState') && source.includes('NOT_IMPORTED') &&
    source.includes('ValidateDoorReceiptOutputBinding'),
  { doorEvidence: 'evidence/door_module_888x14.result.v1.json',
    lockEvidence: 'evidence/lock_topology_888x14.v2.json' },
  { receiptEvidenceAndDoorOutputBound: true })

  add('shared_commitment_projection_matches',
    nativeEvidenceCommitmentSha256({
      z: 7, nested: { beta: false, alpha: ['L', 381, { y: 2, x: 1 }] },
      phase: 'clone_native_seed', completed_at_utc: 'ignored',
      phase_receipt_sha256: 'ignored',
    }) === '684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1' &&
    nativeEvidenceCommitmentSha256({
      a: 0.1592142857142857, b: -1.4150714285714288, c: 0.0000001,
      d: 0.000001, e: 1e20, f: 1e21, g: 254.4285714285714,
      ticks: 638906112000000000, html: '<structure>&engineering',
    }) === '3BC67213D48D35E839E000955BD2B60A862C13A1EA0DBC0BE4ACBEAD12035C65' &&
    NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS.length === 14,
  NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS, { shared14KeyProjection: true })

  beforeProcesses = processSnapshot()
  add('solidworks_process_baseline_is_zero', beforeProcesses.length === 0,
    beforeProcesses, [])

  const noArgs = runExecutable([])
  add('compiled_noargs_fails_before_execution', noArgs.status === 2 &&
    /Usage: NativeRootAssembly888x14\.exe/.test(noArgs.stderr) &&
    !/NATIVE_ROOT_ASSEMBLY_888X14_PASS/.test(noArgs.stdout),
  { status: noArgs.status, stdout: noArgs.stdout, stderr: noArgs.stderr },
  { status: 2, usageOnly: true })

  const help = runExecutable(['--help'])
  add('compiled_help_is_nonexecuting', help.status === 0 &&
    /Usage: NativeRootAssembly888x14\.exe/.test(help.stderr),
  { status: help.status, stderr: help.stderr }, { status: 0 })

  const identity = runExecutable(['--identity'])
  add('compiled_identity_matches_manifest', identity.status === 0 &&
    identity.stdout.includes(`toolId=${toolId}`) &&
    identity.stdout.includes('phase=root_assembly_888x14') &&
    identity.stdout.includes(`sourceNormalizedSha256=${manifest.tool.sourceNormalizedSha256}`) &&
    identity.stdout.includes(`executableSha256=${manifest.tool.executableSha256}`) &&
    identity.stdout.includes(`contractSnapshotSha256=${sha256File(snapshotPath)}`),
  { status: identity.status, stdout: identity.stdout }, { manifestBound: true })

  const selfTest = runExecutable(['--static-self-test'])
  let selfTestDoc = null
  try { selfTestDoc = JSON.parse(selfTest.stdout) } catch {}
  add('compiled_behavior_negative_controls_pass', selfTest.status === 0 &&
    selfTestDoc?.status === 'PASS' && Number(selfTestDoc?.checksFailed) === 0 &&
    Number(selfTestDoc?.checksTotal) >= 16 &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_forged_root_receipt_path' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'shared_snapshot_feature_policy_exact' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_policy_extra_key' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_other_reference51_tuple' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_additional_root_feature_issue' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'lock_composite_manifest_exact_live_contract' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_lock_composite_manifest_extra_key' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_lock_composite_manifest_wrong_fixed_path' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'rejects_lock_composite_manifest_hash_drift' && row.passed) &&
    selfTestDoc?.checks?.some((row) => row.name === 'source_normalization_masks_source_and_contract_constants' && row.passed),
  selfTestDoc ?? { status: selfTest.status, stdout: selfTest.stdout, stderr: selfTest.stderr },
  { status: 'PASS', negativeControls: true })

  afterProcesses = processSnapshot()
  add('static_verification_started_no_cad_process', afterProcesses.length === 0 &&
    stableJson(afterProcesses) === stableJson(beforeProcesses),
  { before: beforeProcesses, after: afterProcesses }, { before: [], after: [] })
} catch (error) {
  add('verifier_completed_without_exception', false,
    { name: error?.name, message: error?.message, stack: error?.stack }, { exception: null })
  afterProcesses = processSnapshot()
}

const checksFailed = checks.filter((check) => !check.ok).length
const report = {
  schema: 'winnsen.16029.native_root_assembly_static_verification.v1',
  generatedAtUtc: new Date().toISOString(),
  status: checksFailed === 0 ? 'PASS' : 'FAIL',
  toolId,
  recipeDigest,
  cadStarted: false,
  checksTotal: checks.length,
  checksFailed,
  hashes: {
    sourceNormalizedSha256: existsSync(sourcePath) ? normalizedSourceSha256(sourcePath) : '',
    executableSha256: existsSync(executablePath) ? sha256File(executablePath) : '',
    contractSnapshotSha256: existsSync(snapshotPath) ? sha256File(snapshotPath) : '',
    verifierSha256: sha256File(verifierPath),
    toolchainManifestSha256: existsSync(manifestPath) ? sha256File(manifestPath) : '',
  },
  processBaseline: beforeProcesses,
  processAfter: afterProcesses,
  checks,
}

const temporary = `${reportPath}.tmp-${process.pid}`
writeFileSync(temporary, `${JSON.stringify(report, null, 2)}\n`, 'utf8')
rmSync(reportPath, { force: true })
renameSync(temporary, reportPath)
process.stdout.write(`${JSON.stringify({
  status: report.status,
  checksTotal: report.checksTotal,
  checksFailed: report.checksFailed,
  cadStarted: false,
  reportPath,
  hashes: report.hashes,
}, null, 2)}\n`)
process.exitCode = checksFailed === 0 ? 0 : 1
