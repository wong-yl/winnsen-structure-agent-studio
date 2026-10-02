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
import { dirname, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from '../../../../../../tools/locker_16029_native_recipe_registry.mjs'
import { buildNativeAssemblyContract } from '../../../../../../tools/lib/locker_16029_native_assembly_contract.mjs'
import {
  NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS,
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  NATIVE_TOOLCHAIN_MANIFEST_PATHS,
  buildNativeStageReceiptContracts,
  nativeEvidenceCommitmentSha256,
} from '../../../../../../tools/lib/locker_16029_native_stage_contract.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '..', '..', '..', '..', '..', '..')
const sourcePath = join(here, 'FinalPack888x14Native.cs')
const executablePath = join(here, 'FinalPack888x14Native.exe')
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
const buildPath = join(here, 'Build-FinalPack888x14Native.ps1')
const reportPath = join(here, 'static_verification.json')
const recipeId = 'winnsen-16029-888w-14door-native-v1'
const recipeDigest = 'f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8'
const toolId = 'native_final_pack_888x14_v1'
const phase = 'final_pack_and_relocated_reopen'
const finalRoot = '标准寄存柜1917×888×550(总装配).SLDASM'

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
  return info.isFile() && !info.isSymbolicLink() && Number(statSync(path).nlink) === 1 &&
    under(realpathSync.native(path), realpathSync.native(repoRoot))
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
    encoding: 'utf8', windowsHide: true, timeout: 20_000,
  })
}

const checks = []
function add(name, ok, actual, expected) {
  checks.push({ name, ok: Boolean(ok), actual, expected })
}

let source = ''
let snapshot = null
let manifest = null
let beforeProcesses = []
let afterProcesses = []

try {
  add('static_artifacts_are_single_link_repository_files',
    [sourcePath, executablePath, verifierPath, snapshotPath, manifestPath, buildPath]
      .every(safeSingleLinkFile),
    [sourcePath, executablePath, verifierPath, snapshotPath, manifestPath, buildPath],
    { regular: true, singleLink: true, repositoryLocal: true })

  source = readFileSync(sourcePath, 'utf8')
  snapshot = JSON.parse(readFileSync(snapshotPath, 'utf8'))
  manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY[recipeId]
  const liveContract = buildNativeAssemblyContract(recipe)
  const stageContracts = buildNativeStageReceiptContracts(recipe)

  add('trusted_recipe_and_assembly_contract_are_current',
    nativeRecipeDigest(recipe) === recipeDigest && stableJson(snapshot) === stableJson(liveContract),
    { recipeDigest: nativeRecipeDigest(recipe), snapshotSha256: sha256File(snapshotPath) },
    { recipeDigest, exactLiveAssemblyContract: true })
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

  add('shared_final_stage_contract_is_exact',
    NATIVE_STAGE_RECEIPT_ORDER.length === 9 && NATIVE_STAGE_RECEIPT_ORDER[8] === phase &&
    exactKeys(stageContracts, NATIVE_STAGE_RECEIPT_ORDER) &&
    stableJson(stageContracts[phase]) === stableJson({
      schema: 'winnsen.16029.native_final_pack_receipt.v1',
      path: 'receipts/final_pack_and_relocated_reopen.json',
      producerToolId: toolId,
      predecessor: 'root_assembly_888x14',
    }) && NATIVE_RUNTIME_RECEIPT_KEYS.length === 28,
    { order: NATIVE_STAGE_RECEIPT_ORDER, final: stageContracts[phase],
      runtimeKeys: NATIVE_RUNTIME_RECEIPT_KEYS.length },
    { finalIndex: 8, runtimeKeys: 28 })

  add('shared_six_toolchain_manifest_paths_are_exact',
    exactKeys(NATIVE_TOOLCHAIN_MANIFEST_PATHS, [
      'native_seed_pack_888x14_v1', 'native_width_888_v1',
      'native_door_module_888x14_v1', 'native_lock_topology_888x14_v1',
      'native_root_assembly_888x14_v1', toolId,
    ]) && NATIVE_TOOLCHAIN_MANIFEST_PATHS[toolId] === repoRelative(manifestPath),
    NATIVE_TOOLCHAIN_MANIFEST_PATHS, { exactSix: true, finalPath: repoRelative(manifestPath) })

  add('shared_commitment_fixtures_are_exact',
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
    NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS, { fixtures: ['684D', '3BC6'], excludedKeys: 14 })

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
    manifest.generatedBy === 'Build-FinalPack888x14Native.ps1' &&
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

  add('final_pack_identity_and_fixed_paths_are_exact',
    source.includes('private const string ToolId = "native_final_pack_888x14_v1";') &&
    source.includes('private const string ExecutionPhase = "final_pack_and_relocated_reopen";') &&
    source.includes('private const string Purpose = "structure_engineering_assistance";') &&
    source.includes(`private const string FinalRootFileName = "${finalRoot}";`) &&
    source.includes('private const string FinalPackageLeaf = "final_native_package";') &&
    source.includes('private const string EvidenceRelativePath = "evidence/final_pack_and_relocated_reopen.result.v1.json";') &&
    source.includes('private const string ReceiptRelativePath = "receipts/final_pack_and_relocated_reopen.json";'),
  { toolId, phase, finalRoot }, { fixedAttemptDerivedPaths: true })

  add('dedicated_packaging_api_has_no_old_generator_dependency',
    source.includes('GetPackAndGo()') && source.includes('SavePackAndGo(packAndGo)') &&
    source.includes('SetDocumentSaveToNames') && source.includes('FlattenToSingleFolder = true') &&
    !source.includes('PackAndGoAssembly.exe') && !source.includes('ConfigureFourDoorAssemblyV36') &&
    !source.includes('ConfigureWidthV37') && !source.includes('native_pack_and_go_v1'),
  { dedicatedPackAndGo: true }, { oldGeneratorDependency: false })

  add('solidworks_2020_and_owned_process_identity_are_pinned',
    source.includes('Type.GetTypeFromProgID("SldWorks.Application.28", true)') &&
    source.includes('D:\\soildworks2020\\SOLIDWORKS\\SLDWORKS.exe') &&
    source.includes('1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978') &&
    source.includes('D:\\soildworks2020\\SOLIDWORKS\\sldProcMon.exe') &&
    source.includes('A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70') &&
    source.includes('ProcessStartUtcTicks') && source.includes('ExactOwnedMonitor') &&
    source.includes('ExactParentPattern'),
  { progId: 'SldWorks.Application.28' }, { pidStartExeHashAndMonitorParent: true })

  add('preflight_binds_plan_auth_six_manifests_and_root_receipt',
    source.includes('ValidateTrustedToolchainManifests') &&
    source.includes('ValidateStageReceiptContracts') &&
    source.includes('ValidateRootReceipt') &&
    source.includes('LoadAndValidateAuthorization') &&
    source.includes('native_root_assembly_888x14_v1') &&
    source.includes('receipts/root_assembly_888x14.json') &&
    source.includes('evidence/root_assembly_888x14.result.v1.json'),
  { plan: true, auth: true, manifests: 6, predecessor: 'root receipt' }, { exact: true })

  add('input_backup_output_transaction_and_pair_commit_are_ordered',
    source.indexOf('CreateCompleteInputBackup(result);') >= 0 &&
    source.indexOf('CreateCompleteInputBackup(result);') < source.indexOf('RunDedicatedPackAndGo(result);') &&
    source.indexOf('AuditInputUnchanged(result);') > source.indexOf('VerifyRelocatedCopy(result);') &&
    source.indexOf('WriteJsonAtomicNew(result.evidencePath, result);') >= 0 &&
    source.indexOf('WriteFinalReceiptAtomic(result);') >
      source.indexOf('WriteJsonAtomicNew(result.evidencePath, result);') &&
    source.includes('RollbackAllWrites(result);') &&
    source.includes('RemoveOrQuarantineUnpairedArtifact'),
  { backupBeforePack: true, auditAfterRelocation: true, evidenceThenReceipt: true },
  { rollbackAllWrites: true })

  add('strict_final_quality_gates_are_present',
    source.includes('openWarnings == 32') && !source.includes('openWarnings == 96') &&
    source.includes('topLevelComponentCount == 41') &&
    source.includes('recursiveComponentCount == 294') &&
    source.includes('activeRecursiveComponentCount == 294') &&
    source.includes('suppressedComponentCount == 0') &&
    source.includes('doorModuleCount == 14') && source.includes('shelfModuleCount == 12') &&
    source.includes('activeFrameCrossbarCount == 12') && source.includes('mechanicalTongueCount == 14') &&
    source.includes('cadFileCount == 55') && source.includes('assemblyFileCount == 14') &&
    source.includes('partFileCount == 41') && source.includes('statuses.All(value => value == 0)'),
  { warnings: 32, topology: '41/294/294/0,14/12/12/14', closure: '55=14+41', statuses: 0 },
  { exact: true })

  add('feature_issue_and_reference_policy_fail_closed',
    source.includes('ExactKnownFeatureHealthPolicy') &&
    source.includes('featureHealthPolicy') &&
    source.includes('maximumIssueCount') && source.includes('allowedIssue') &&
    source.includes('StringComparison.Ordinal') &&
    source.includes('issue.errorCode == Number(allowed, "errorCode")') &&
    source.includes('issue.errorCode2 == Number(allowed, "errorCode2")') &&
    source.includes('issue.warning == Bool(allowed, "warning")') &&
    source.includes('forbiddenFinalReferenceLeafNames') && source.includes('dependenciesAllLocal'),
  { knownReferenceRequiresContractPermission: true, forbiddenReferences: true }, { failClosed: true })

  add('evidence_receipt_contract_has_no_cycle',
    source.includes('EvidenceCommitmentDigest(result)') && source.includes('evidenceSha256') &&
    source.includes('evidenceCommitmentSha256') && source.includes('predecessorReceiptSha256') &&
    source.includes('authorizationJsonBase64') &&
    source.includes('!evidence.ContainsKey("phase_receipt_path")') &&
    source.includes('!evidence.ContainsKey("phase_receipt_sha256")') &&
    source.includes('!evidence.ContainsKey("phase_receipt_committed")'),
  { actualEvidenceSha: true, semanticCommitment: true, receiptIdentityInEvidence: false },
  { shared28KeyReceipt: true })

  add('user_facing_state_boundary_has_no_forbidden_claims',
    !/\b(?:production|release)\b/i.test(source) &&
    source.includes('structure_engineering_assistance') &&
    source.includes('engineerReviewRequired = true'),
  { forbiddenTermsFound: source.match(/\b(?:production|release)\b/gi) ?? [] },
  { purpose: 'structure_engineering_assistance', engineerReviewRequired: true })

  beforeProcesses = processSnapshot()
  add('solidworks_process_baseline_is_zero', beforeProcesses.length === 0, beforeProcesses, [])

  const noArgs = runExecutable([])
  add('compiled_noargs_fails_before_execution', noArgs.status === 2 &&
    /Usage: FinalPack888x14Native\.exe/.test(noArgs.stderr) &&
    !/PASS/.test(noArgs.stdout),
  { status: noArgs.status, stdout: noArgs.stdout, stderr: noArgs.stderr },
  { status: 2, usageOnly: true })

  const help = runExecutable(['--help'])
  add('compiled_help_is_nonexecuting', help.status === 0 &&
    /Usage: FinalPack888x14Native\.exe/.test(help.stderr) &&
    help.stderr.includes('structure_engineering_assistance'),
  { status: help.status, stderr: help.stderr }, { status: 0, purposeOnly: true })

  const identity = runExecutable(['--identity'])
  add('compiled_identity_matches_manifest', identity.status === 0 &&
    identity.stdout.includes(`toolId=${toolId}`) && identity.stdout.includes(`phase=${phase}`) &&
    identity.stdout.includes('purpose=structure_engineering_assistance') &&
    identity.stdout.includes(`sourceNormalizedSha256=${manifest.tool.sourceNormalizedSha256}`) &&
    identity.stdout.includes(`executableSha256=${manifest.tool.executableSha256}`) &&
    identity.stdout.includes(`contractSnapshotSha256=${sha256File(snapshotPath)}`),
  { status: identity.status, stdout: identity.stdout }, { manifestBound: true })

  const selfTest = runExecutable(['--static-self-test'])
  let selfTestDoc = null
  try { selfTestDoc = JSON.parse(selfTest.stdout) } catch {}
  const requiredSelfTests = [
    'commitment_fixture_684d', 'commitment_fixture_3bc6',
    'exact_runtime_receipt_keys', 'exact_nine_stage_contract',
    'rejects_forged_final_receipt_path', 'rejects_runtime_receipt_extra_key',
    'fixed_output_path_is_attempt_derived', 'rejects_nonempty_final_output',
    'strict_root_warning_32_rejects_96', 'exact_working_inventory_88_26_62',
    'exact_final_closure_55_14_41', 'exact_root_topology_41_294_294_0',
    'exact_module_counts_14_12_12_14', 'shared_snapshot_feature_policy_exact',
    'requires_valid_feature_health_policy_for_zero_issues',
    'accepts_only_trusted_reference51_tuple', 'rejects_policy_extra_top_level_key',
    'rejects_policy_extra_nested_key', 'rejects_other_reference51_tuple',
    'rejects_second_feature_issue',
    'rejects_forbidden_old_reference', 'all_pack_statuses_must_be_zero',
    'authorization_phase_is_exact', 'six_manifest_keys_are_exact',
    'monitor_parent_argument_is_exact', 'rollback_scope_must_stay_under_attempt',
    'success_evidence_omits_receipt_identity',
    'partial_backup_cleanup_skips_full_inventory_restore',
    'quarantine_artifact_blocks_rollback_completion',
    'preexisting_artifact_is_never_rollback_owned',
    'atomic_create_marks_ownership_before_later_failure',
    'lock_composite_manifest_exact_live_contract',
    'rejects_lock_composite_manifest_extra_key',
    'rejects_lock_composite_manifest_wrong_fixed_path',
    'rejects_lock_composite_manifest_hash_drift',
    'source_normalization_masks_source_and_contract_constants',
  ]
  const passedSelfTests = new Set((selfTestDoc?.checks ?? [])
    .filter((row) => row?.passed).map((row) => row.name))
  add('compiled_behavior_negative_controls_pass', selfTest.status === 0 &&
    selfTestDoc?.status === 'PASS' && Number(selfTestDoc?.checksFailed) === 0 &&
    requiredSelfTests.every((name) => passedSelfTests.has(name)),
  selfTestDoc ?? { status: selfTest.status, stdout: selfTest.stdout, stderr: selfTest.stderr },
  { status: 'PASS', requiredSelfTests })

  const unknown = runExecutable(['--unknown'])
  add('compiled_unknown_cli_fails_before_execution', unknown.status === 2 &&
    /unknown argument/i.test(unknown.stderr),
  { status: unknown.status, stderr: unknown.stderr }, { status: 2 })

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
  schema: 'winnsen.16029.native_final_pack_static_verification.v1',
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
