[CmdletBinding()]
param(
    [string]$OutPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $PSScriptRoot 'static_verification.json'
}
$sourcePath = Join-Path $PSScriptRoot 'NativeDoorModule888x14.cs'
$exePath = Join-Path $PSScriptRoot 'NativeDoorModule888x14.exe'
$verifierPath = Join-Path $PSScriptRoot 'Verify-NativeDoorModule888x14.ps1'
$receiptVerifierPath = Join-Path $PSScriptRoot 'verify_receipt_contract.mjs'
$toolchainVerifierPath = Join-Path $PSScriptRoot 'verify_toolchain_contract.mjs'
$panelProofVerifierPath = Join-Path $PSScriptRoot 'verify_panel_proof_contract.mjs'
$toolchainManifestPath = Join-Path $PSScriptRoot 'toolchain_manifest.json'
$staleSourceGatePath = Join-Path $PSScriptRoot 'current_source_gate.json'
$interopRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin'))
$sldworksInterop = Join-Path $interopRoot 'SolidWorks.Interop.sldworks.dll'
$swconstInterop = Join-Path $interopRoot 'SolidWorks.Interop.swconst.dll'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..\..'))
$authorizationModulePath = Join-Path $repoRoot `
    'tools\lib\locker_16029_native_execution_authorization.mjs'
$v37WorkingPack = Join-Path $repoRoot `
    'workers\generated_models\review_generation_requests\v43-int-v37-760w-six-door-l642-r246-r1\native_cad\final_native_hierarchical_pack_and_go'

function Assert-Gate {
    param([bool]$Condition, [string]$Code, [string]$Message)
    if (-not $Condition) { throw "$Code`: $Message" }
}

function Get-CadProcessRows {
    @(
        Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
            Sort-Object ProcessName, Id |
            ForEach-Object { "$($_.ProcessName):$($_.Id)" }
    )
}

$cadBefore = @(Get-CadProcessRows)
Assert-Gate ($cadBefore.Count -eq 0) 'CAD_BASELINE_NOT_EMPTY' `
    'Static verification requires no running SolidWorks or sldProcMon process.'
Assert-Gate (-not (Test-Path -LiteralPath $staleSourceGatePath)) 'STALE_SOURCE_GATE_PRESENT' `
    'current_source_gate.json is not runtime evidence and must not be shipped.'
Assert-Gate (Test-Path -LiteralPath $authorizationModulePath -PathType Leaf) `
    'UNIFIED_AUTHORIZATION_MODULE_MISSING' 'The shared authorization module is missing.'
$authorizationModule = [IO.File]::ReadAllText($authorizationModulePath, [Text.Encoding]::UTF8)
foreach ($fragment in @(
    "native_seed_pack_888x14_v1: Object.freeze(['clone_native_seed'])",
    "native_width_888_v1: Object.freeze(['dimensions', 'derived', 'base-hole', 'assemblies'])",
    "native_door_module_888x14_v1: Object.freeze(['door_module_888x14'])",
    "native_lock_topology_888x14_v1: Object.freeze(['lock_topology_888x14'])",
    "native_root_assembly_888x14_v1: Object.freeze(['root_assembly_888x14'])",
    "native_final_pack_888x14_v1: Object.freeze(['final_pack_and_relocated_reopen'])",
    "leaseExpiresAt: String(task.nativeBuild.lease.expiresAt)",
    "assertSingleLinkRegularFile(planPath, 'native build plan')",
    "assertSingleLinkRegularFile(authorizationPath, 'native execution authorization')",
    'engineeringAssistanceReady: false',
    'readyOnlyAfterEveryRequiredCheckPasses: true',
    "'task', 'request', 'plan', 'recipe', 'tool', 'seed', 'execution', 'qualityBoundary'"
)) {
    Assert-Gate $authorizationModule.Contains($fragment) `
        'UNIFIED_AUTHORIZATION_CONTRACT_DRIFT' $fragment
}
$v37CadFiles = @(Get-ChildItem -LiteralPath $v37WorkingPack -File | Where-Object {
    $_.Extension -in @('.SLDPRT', '.SLDASM')
})
Assert-Gate ($v37CadFiles.Count -eq 75 -and
    @($v37CadFiles | Where-Object Extension -eq '.SLDPRT').Count -eq 53 -and
    @($v37CadFiles | Where-Object Extension -eq '.SLDASM').Count -eq 22) `
    'V37_WORKING_PACK_INVENTORY_DRIFT' 'Expected flat 75 = 53 SLDPRT + 22 SLDASM.'
$doorFlatTargetNames = @(
    'door_panel_left_W381_H254p428571.SLDPRT',
    'door_panel_right_W381_H254p428571.SLDPRT',
    'door_stiffener_H243p928571.SLDPRT', 'hinge_latch_plate.SLDPRT',
    'u_hook_pad.SLDPRT', 'plastic_bushing.SLDPRT', 'door_hinge_pin.SLDPRT',
    'circlip.SLDPRT', 'mechanical_lock_tongue.SLDPRT',
    'door_weld_left_W381_H254p428571.SLDASM',
    'door_weld_right_W381_H254p428571.SLDASM',
    'ordinary_door_left_W381_H254p428571.SLDASM',
    'ordinary_door_right_W381_H254p428571.SLDASM'
)
Assert-Gate (@($doorFlatTargetNames | Sort-Object -Unique).Count -eq 13) `
    'FLAT_IMPORT_TARGET_COLLISION' 'Door flat target names are not unique.'
$v37Names = @($v37CadFiles.Name)
Assert-Gate (@($doorFlatTargetNames | Where-Object { $v37Names -contains $_ }).Count -eq 0) `
    'V37_WORKING_PACK_NAME_COLLISION' 'Door flat targets collide with the current V37 pack.'

$build = & (Join-Path $PSScriptRoot 'Build-NativeDoorModule888x14.ps1') -OutputPath $exePath
$source = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::UTF8)

Assert-Gate (Test-Path -LiteralPath $toolchainManifestPath -PathType Leaf) `
    'TOOLCHAIN_MANIFEST_MISSING' 'Generic door toolchain manifest was not generated.'
$toolchainManifest = Get-Content -LiteralPath $toolchainManifestPath -Raw | ConvertFrom-Json
$toolchainKeys = @($toolchainManifest.psobject.Properties.Name | Sort-Object)
$toolKeys = @($toolchainManifest.tool.psobject.Properties.Name | Sort-Object)
Assert-Gate (($toolchainKeys -join ',') -ceq 'generatedBy,schema,tool' -and
    ($toolKeys -join ',') -ceq
        'executablePath,executableSha256,id,sourceNormalizedSha256,sourcePath,verifierPath,verifierSha256' -and
    $toolchainManifest.schema -ceq 'winnsen.16029.native_toolchain_manifest.v1' -and
    $toolchainManifest.tool.id -ceq 'native_door_module_888x14_v1' -and
    $toolchainManifest.tool.sourceNormalizedSha256 -ceq $build.source_normalized_sha256 -and
    $toolchainManifest.tool.executableSha256 -ceq $build.output_sha256 -and
    $toolchainManifest.tool.verifierSha256 -ceq
        (Get-FileHash -LiteralPath $verifierPath -Algorithm SHA256).Hash) `
    'TOOLCHAIN_MANIFEST_CONTRACT_INVALID' `
    'Door manifest must be the exact generic live source/executable/verifier binding.'

$node = (Get-Command node -ErrorAction Stop).Source
$crossCheckRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ('winnsen-door-auth-crosscheck-' + [Guid]::NewGuid().ToString('N'))
$crossCheckRoot = [IO.Path]::GetFullPath($crossCheckRoot)
Assert-Gate $crossCheckRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), `
    [StringComparison]::OrdinalIgnoreCase) 'AUTH_CROSSCHECK_TEMP_SCOPE_INVALID' `
    'Authorization cross-check escaped the system temp directory.'
New-Item -ItemType Directory -Path $crossCheckRoot | Out-Null
try {
    $nodeScript = Join-Path $crossCheckRoot 'issue-auth.mjs'
    $moduleUrl = ([Uri]$authorizationModulePath).AbsoluteUri
    $attemptPathJson = ($crossCheckRoot | ConvertTo-Json -Compress)
    $nodeText = @"
import { createHash } from 'node:crypto'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { issueNativeExecutionAuthorization } from '$moduleUrl'
const stable = (v) => Array.isArray(v) ? v.map(stable) : (!v || typeof v !== 'object') ? v : Object.fromEntries(Object.keys(v).sort().map((k) => [k, stable(v[k])]))
const sha = (v) => createHash('sha256').update(v).digest('hex').toUpperCase()
const root = $attemptPathJson
const fingerprint = 'A'.repeat(64)
const task = { id: 'DOOR-AUTH-CROSSCHECK', revision: 7, status: 'queued', taskType: 'native_model_request', requestFingerprint: fingerprint, nativeBuild: { attempt: 3, state: 'planning', lease: { id: 'lease-door-crosscheck', workerId: 'door-crosscheck-worker', expiresAt: '2030-01-01T00:20:00.000Z' } } }
const digest = sha(JSON.stringify(stable({ id: task.id, revision: task.revision, status: task.status, taskType: task.taskType, requestFingerprint: task.requestFingerprint, nativeBuildAttempt: task.nativeBuild.attempt })))
const plan = { schema: 'winnsen.native_build_plan.v1', purpose: 'structure_engineering_assistance', workerId: 'door-crosscheck-worker', task: { id: task.id, revisionAtPlanning: 7, digest }, request: { fingerprint, digest: 'B'.repeat(64) }, recipe: { id: 'winnsen-16029-888w-14door-native-v1', version: 1, digest: 'C'.repeat(64) }, requiredChecks: ['one_door_one_lock','rebuild_save_reopen','relocated_reopen'], qualityBoundary: { planningOnly: true }, executionBoundary: { executorImplemented: false, cadStarted: false, modelGenerated: false, modelReady: false, legacyFallbackUsed: false } }
const planPath = join(root, 'native_build_plan.json')
writeFileSync(planPath, JSON.stringify(plan, null, 2) + '\n')
const issued = issueNativeExecutionAuthorization({ attemptDir: root, planPath, task, workerId: 'door-crosscheck-worker', tool: { id: 'native_door_module_888x14_v1', sourceNormalizedSha256: 'D'.repeat(64), executableSha256: 'E'.repeat(64) }, seedInventoryDigest: 'F'.repeat(64), attempt: 3, phases: ['door_module_888x14'], clock: () => new Date('2030-01-01T00:00:00.000Z'), ttlMs: 600000 })
process.stdout.write(JSON.stringify({ authorizationPath: issued.authorizationPath, authorizationId: issued.authorization.authorizationId, issuedAt: issued.authorization.issuedAt, expiresAt: issued.authorization.expiresAt }))
"@
    [IO.File]::WriteAllText($nodeScript, $nodeText, (New-Object Text.UTF8Encoding($false)))
    $issuedJson = @(& $node $nodeScript 2>&1 | ForEach-Object { $_.ToString() })
    Assert-Gate ($LASTEXITCODE -eq 0) 'AUTH_CROSSCHECK_ISSUE_FAILED' ($issuedJson -join "`n")
    $issued = ($issuedJson -join "`n") | ConvertFrom-Json
    $assembly = [Reflection.Assembly]::LoadFrom($exePath)
    $programType = $assembly.GetType(
        'Winnsen.StructureAgent.NativeDoorModule888x14.NativeDoorModule888x14', $true)
    $identityMethods = @($programType.GetMethods([Reflection.BindingFlags]'NonPublic,Static') |
        Where-Object {
            $_.Name -eq 'ExpectedAuthorizationId' -and
            $_.GetParameters().Count -eq 1 -and
            $_.GetParameters()[0].ParameterType -eq [string]
        })
    Assert-Gate ($identityMethods.Count -eq 1) 'AUTH_CROSSCHECK_METHOD_AMBIGUOUS' `
        "Expected exactly one ExpectedAuthorizationId(string) overload, found $($identityMethods.Count)."
    $identityMethod = $identityMethods[0]
    Assert-Gate ($null -ne $identityMethod) 'AUTH_CROSSCHECK_METHOD_MISSING' `
        'ExpectedAuthorizationId was not found in the compiled executable.'
    $csharpAuthorizationId = [string]$identityMethod.Invoke($null, @([string]$issued.authorizationPath))
    Assert-Gate ($csharpAuthorizationId -ceq [string]$issued.authorizationId) `
        'AUTHORIZATION_ID_CROSS_LANGUAGE_MISMATCH' `
        "shared=$($issued.authorizationId), csharp=$csharpAuthorizationId"
    $authorizationCrossCheck = [ordered]@{
        shared_authorization_id = [string]$issued.authorizationId
        csharp_authorization_id = $csharpAuthorizationId
        exact_match = $true
        issued_at = [string]$issued.issuedAt
        expires_at = [string]$issued.expiresAt
        shared_stable_json_contract = $true
    }
}
finally {
    if (Test-Path -LiteralPath $crossCheckRoot) {
        Remove-Item -LiteralPath $crossCheckRoot -Recurse -Force
    }
}

$requiredFragments = [ordered]@{
    fixed_cabinet_width = 'TargetCabinetWidthMm = 888.0'
    fixed_door_width = 'TargetDoorWidthMm = 381.0'
    exact_door_height = 'TargetDoorHeightMm = 1781.0 / 7.0'
    fixed_layout = 'request.columns == 2 && request.rows_per_column == 7 && request.total_doors == 14'
    mirror_strategy = 'RightStrategyMirrorPart = "solidworks_mirror_part"'
    mirror_api = 'leftPart.MirrorPart2(true, options, out rightModel)'
    mirror_import_solids = 'swMirrorPartOptions_ImportSolids'
    mirror_import_sheet_metal = 'swMirrorPartOptions_ImportSMInfo'
    mirror_import_properties = 'swMirrorPartOptions_ImportIndProps'
    mirror_import_cut_list = 'swMirrorPartOptions_ImportCutListProperties'
    mirror_feature_data = 'MirrorPartFeatureData'
    mirror_sheet_metal_gate = 'NativeMirrorSheetMetalGate'
    external_reference_gate = 'ExternalReferenceCount(rightModel) == 0'
    one_body_gate = 'health.body_count == 1'
    sheet_metal_gate = 'health.has_sheet_metal'
    flat_pattern_gate = 'health.has_flat_pattern'
    bbox_gate = 'PartBboxGate(health, TargetDoorWidthMm, TargetDoorHeightMm)'
    exact_bbox_api = 'body.GetExtremePoint'
    exact_bbox_evidence = 'health.bbox_from_body_extreme_points = true'
    hash_gate = 'string.Equals(reopenedHash, expectedHash'
    relocation_reopen = '"relocation_reopen"'
    committed_reopen = '"committed_reopen"'
    exclusive_solidworks_2020 = 'Type.GetTypeFromProgID("SldWorks.Application.28", true)'
    process_identity = 'sw.GetProcessID()'
    process_exit = 'sw.ExitApp()'
    exact_sw_revision = 'revision.StartsWith("28.", StringComparison.Ordinal)'
    exact_sw_exe_major = 'fileMajor == 28'
    immutable_plan_auth = 'result.plan_execution_authorized'
    planning_only_execute_denied = '"IMMUTABLE_PLAN_NOT_EXECUTION_AUTHORIZED"'
    planning_plan_is_permanent = '!plan.executionBoundary.executorImplemented'
    planning_only_is_permanent = 'plan.qualityBoundary.planningOnly'
    execution_authorization_path_env = 'WINNSEN_NATIVE_EXECUTION_AUTH_PATH'
    execution_authorization_sha_env = 'WINNSEN_NATIVE_EXECUTION_AUTH_SHA256'
    execution_authorization_schema = 'winnsen.native_execution_authorization.v1'
    exact_execution_authorization_relative_path = 'execution_authorizations\\native_door_module_888x14_v1.json'
    authorization_tool_binding = 'sourceNormalizedSha256'
    authorization_executable_binding = 'executableSha256'
    authorization_seed_inventory_binding = 'inventoryDigest'
    authorization_phase_binding = 'native_door_module_888x14_v1'
    authorization_nested_execution = 'authorization.execution != null && authorization.execution.authorized'
    authorization_exact_phase = 'authorization.execution.phases.SequenceEqual(new[] { DoorModuleExecutionPhase })'
    authorization_camel_worker = 'authorization.workerId'
    authorization_nested_plan = 'authorization.plan.sha256'
    authorization_nested_seed = 'authorization.seed.inventoryDigest'
    authorization_quality_boundary = 'authorization.qualityBoundary.engineeringAssistanceReady'
    authorization_exact_json_keys = 'ValidateAuthorizationJsonShape'
    authorization_identity_digest = 'ExpectedAuthorizationId'
    authorization_lease_expiry_binding = 'authorization.task.leaseExpiresAt'
    authorization_not_beyond_lease = 'expires <= leaseExpires'
    final_commit_time_window_gate = 'RevalidateAuthorizationTimeWindow'
    safe_reparse_realpath_gate = 'GetFinalPathNameByHandle'
    safe_hardlink_gate = 'GetFileInformationByHandle'
    safe_recursive_delete_tree = 'RequireSafeRecursiveDeleteTree'
    safe_path_before_mutation = 'RequireSafeMutationPath'
    exact_component_relative_path = 'RelativePath(allowedRoot, actualPath)'
    exact_component_hash = 'expected.sha256'
    exact_13_cad_inventory = 'VerifyExactOutputInventory'
    flat_import_manifest = 'door_module_flat_import_manifest.json'
    flat_import_unique_names = 'FLAT_IMPORT_TARGET_COLLISION'
    flat_import_v37_collision_gate = 'V37_WORKING_PACK_NAME_COLLISION'
    root_assembler_boundary = 'ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK'
    evidence_commit_guard = 'CommitExecutionEvidence'
    fixed_success_evidence = 'evidence/door_module_888x14.result.v1.json'
    fixed_success_receipt = 'receipts/door_module_888x14.json'
    current_width_post_inventory = 'current_pre_inventory_digest'
    predecessor_chain = 'ValidateLivePredecessorReceiptChain'
    shared_ecmascript_number = 'EcmaJsonNumber'
    lock_composite_manifest_schema = 'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1'
    lock_composite_exact_tools = 'RequireToolchainExactKeys(tools, "lock toolchain manifest.tools"'
    lock_composite_live_artifacts = 'LiveLockManifestArtifact'
    panel_proof_cli = '"--panel-proof"'
    panel_proof_fixed_path = 'evidence/left-panel-proof.v1.json'
    panel_proof_fixed_audit = 'evidence/left-panel-readonly-audit.v1.json'
    panel_proof_same_authorization = 'BindExecutionAuthorization(request, result)'
    panel_proof_authorization_recheck = 'RevalidateAuthorizationTimeWindow(request, result)'
    panel_proof_owned_session = 'StartOwnedSession(request, "left_panel_proof_read_only"'
    panel_proof_readonly_open = '(int)swDocumentTypes_e.swDocPART, true'
    panel_proof_exact_shape = 'PanelProofDocumentValid'
    panel_proof_self_commitment = 'PanelArtifactCommitment'
    panel_proof_no_stage_receipt = '"stageReceiptCreated", false'
    receipt_create_new = 'BuildDoorReceipt'
    evidence_write_failure_state = 'EVIDENCE_ATOMIC_REREAD_FAILED'
    exact_column_layout = 'plan.geometry.columnDoorCounts.SequenceEqual(new[] { 7, 7 })'
    exact_row_sequence = '"L1111111-R1111111"'
    activation_process_delta = 'activationDelta'
    activation_fail_closed_cleanup = 'TryCleanupActivationDelta'
    recursive_feature_gate = 'health.traversal_complete'
    stiffener_body_length = 'StiffenerBboxGate'
    assembly_transform_readback = 'VerifyAssemblyPlacements'
    transaction_lock_commit_gate = '"TRANSACTION_LOCK_RELEASE_FAILED"'
    source_backup = 'source_backups'
    source_mutation_gate = '"SOURCE_MUTATED"'
    failure_evidence_scope_gate = 'IsUnderRoot(path, request.isolated_root)'
    weld_left_right = 'BuildWeldAssembly'
    ordinary_left_right = 'BuildOrdinaryDoorAssembly'
    hinge_formula = 'double hingeX = -(TargetDoorWidthMm / 2.0 - 10.0) * side;'
    lock_formula = 'double lockX = (TargetDoorWidthMm / 2.0 - 15.0) * side;'
    one_mechanical_tongue = 'new Placement("mechanical_lock_tongue"'
    lock_topology_boundary = 'EXTERNAL_PREREQUISITE_FOR_ROOT_ASSEMBLY_TOPOLOGY_STAGE'
    proper_rotation_gate = 'Near(MatrixDeterminant(row.rotation), 1.0, 1e-9)'
    denied_v37_hash_1 = '735F6DABE58153F7023F0327AF4C23C98DECEB62BAE55F13361D3BAF7FA23139'
    denied_v37_hash_2 = '9912C4FC006DCEEB84758A332ACF36EFC5A78EF7978922A6AE303121736902F2'
    denied_v37_hash_3 = 'C2C2456362719C57B1FCBBA599DA0CE5B41ADAF7B1BD1C2CBDA01A15B5C48783'
}

$requiredResults = @()
foreach ($entry in $requiredFragments.GetEnumerator()) {
    $passed = $source.Contains([string]$entry.Value)
    Assert-Gate $passed ('REQUIRED_FRAGMENT_MISSING_' + $entry.Key.ToUpperInvariant()) `
        ([string]$entry.Value)
    $requiredResults += [pscustomobject]@{
        gate = $entry.Key
        fragment = [string]$entry.Value
        passed = $true
    }
}

$prohibitedPatterns = [ordered]@{
    freecad = '(?i)FreeCAD'
    neutral_cad_exchange = '(?i)\.(step|stp)\b'
    active_object_attach = '(?i)(Marshal\.)?GetActiveObject\s*\('
    moniker_attach = '(?i)BindToMoniker\s*\('
    legacy_open = '(?<![A-Za-z0-9_])OpenDoc\s*\('
    electric_lock = '(?i)electric[_ -]?lock|electronic[_ -]?lock|\u7535\u63a7\u9501|ZJA-S'
    unlocked_mirror = 'MirrorPart2\s*\(\s*false\s*,'
    non_silent_solidworks_version = 'SldWorks\.Application\.(?!28\b)\d+'
    alternate_right_panel_strategy = '(?i)provided_native'
    legacy_authorization_execution = '\bexecution_authorized\b'
    legacy_authorization_issued = '\bissued_at_utc\b'
    legacy_authorization_phases = 'authorization\.phases'
}

$prohibitedResults = @()
foreach ($entry in $prohibitedPatterns.GetEnumerator()) {
    $matched = [regex]::IsMatch($source, [string]$entry.Value)
    Assert-Gate (-not $matched) ('PROHIBITED_PATTERN_' + $entry.Key.ToUpperInvariant()) `
        ([string]$entry.Value)
    $prohibitedResults += [pscustomobject]@{
        gate = $entry.Key
        pattern = [string]$entry.Value
        passed = $true
    }
}

$authorizationClass = [regex]::Match(
    $source,
    'public sealed class ExecutionAuthorization\s*\{.*?\r?\n\s*}',
    [Text.RegularExpressions.RegexOptions]::Singleline
).Value
Assert-Gate (-not [string]::IsNullOrWhiteSpace($authorizationClass)) `
    'AUTHORIZATION_CLASS_NOT_FOUND' 'Could not inspect ExecutionAuthorization.'
foreach ($legacyField in @('worker_id', 'plan_sha256', 'seed_inventory_digest', 'phases')) {
    Assert-Gate (-not $authorizationClass.Contains($legacyField)) `
        ('LEGACY_AUTHORIZATION_FIELD_' + $legacyField.ToUpperInvariant()) `
        "ExecutionAuthorization still contains legacy field $legacyField."
}

$foreignMethod = [regex]::Match(
    $source,
    'private static bool IsForeignFeatureType\(string type\).*?\r?\n\s*}',
    [Text.RegularExpressions.RegexOptions]::Singleline
).Value
Assert-Gate (-not [string]::IsNullOrWhiteSpace($foreignMethod)) `
    'FOREIGN_FEATURE_METHOD_NOT_FOUND' 'Could not inspect IsForeignFeatureType.'
Assert-Gate (-not $foreignMethod.Contains('MirrorStock')) `
    'MIRRORSTOCK_WRONGLY_CLASSIFIED_FOREIGN' `
    'MirrorStock is permitted only for the link-free MirrorPart2-derived right panel.'
Assert-Gate ($foreignMethod.Contains('BaseBody') -and $foreignMethod.Contains('ImportedBody')) `
    'FOREIGN_FEATURE_DENIAL_MISSING' 'BaseBody and ImportedBody must remain denied.'

$sldworksAssembly = [Reflection.Assembly]::LoadFrom($sldworksInterop)
$swconstAssembly = [Reflection.Assembly]::LoadFrom($swconstInterop)
$partDocType = $sldworksAssembly.GetType('SolidWorks.Interop.sldworks.IPartDoc', $true)
$extensionType = $sldworksAssembly.GetType('SolidWorks.Interop.sldworks.IModelDocExtension', $true)
$mirrorDataType = $sldworksAssembly.GetType(
    'SolidWorks.Interop.sldworks.IMirrorPartFeatureData', $true)
$mirrorMethod = @($partDocType.GetMethods() | Where-Object { $_.Name -eq 'MirrorPart2' })
Assert-Gate ($mirrorMethod.Count -eq 1) 'INTEROP_MIRRORPART2_MISSING' `
    'SolidWorks 2020 IPartDoc.MirrorPart2 was not found.'
Assert-Gate ($null -ne $extensionType.GetMethod('BreakAllExternalFileReferences2')) `
    'INTEROP_BREAK_EXTERNAL_REFERENCE_API_MISSING' `
    'SolidWorks 2020 BreakAllExternalFileReferences2 was not found.'
Assert-Gate ($null -ne $extensionType.GetMethod('ListExternalFileReferencesCount')) `
    'INTEROP_EXTERNAL_REFERENCE_COUNT_API_MISSING' `
    'SolidWorks 2020 ListExternalFileReferencesCount was not found.'
Assert-Gate ($null -ne $mirrorDataType.GetProperty('SheetMetalInformation')) `
    'INTEROP_SHEET_METAL_INFORMATION_MISSING' `
    'MirrorPartFeatureData.SheetMetalInformation was not found.'

$mirrorOptionsType = $swconstAssembly.GetType(
    'SolidWorks.Interop.swconst.swMirrorPartOptions_e', $true)
$expectedEnumValues = [ordered]@{
    swMirrorPartOptions_ImportSolids = 1
    swMirrorPartOptions_ImportCutListProperties = 2048
    swMirrorPartOptions_ImportSMInfo = 4096
    swMirrorPartOptions_ImportIndProps = 8192
}
$enumResults = @()
foreach ($entry in $expectedEnumValues.GetEnumerator()) {
    $actual = [int][Enum]::Parse($mirrorOptionsType, [string]$entry.Key)
    Assert-Gate ($actual -eq [int]$entry.Value) `
        ('INTEROP_ENUM_VALUE_MISMATCH_' + $entry.Key.ToUpperInvariant()) `
        "Expected $($entry.Value), got $actual."
    $enumResults += [pscustomobject]@{
        name = [string]$entry.Key
        expected = [int]$entry.Value
        actual = $actual
        passed = $true
    }
}

$helpCadBefore = @(Get-CadProcessRows)
$helpOutput = @(& $exePath --help 2>&1 | ForEach-Object { $_.ToString() })
$helpExitCode = $LASTEXITCODE
$helpCadAfter = @(Get-CadProcessRows)
Assert-Gate ($helpExitCode -eq 0) 'HELP_CLI_FAILED' 'The compiled CLI --help contract failed.'
Assert-Gate ($helpCadBefore.Count -eq 0 -and $helpCadAfter.Count -eq 0) `
    'HELP_STARTED_CAD' 'The --help smoke test must not start SolidWorks.'

$noArgsCadBefore = @(Get-CadProcessRows)
$noArgsStartInfo = [Diagnostics.ProcessStartInfo]::new()
$noArgsStartInfo.FileName = $exePath
$noArgsStartInfo.UseShellExecute = $false
$noArgsStartInfo.CreateNoWindow = $true
$noArgsStartInfo.RedirectStandardOutput = $true
$noArgsStartInfo.RedirectStandardError = $true
$noArgsProcess = [Diagnostics.Process]::Start($noArgsStartInfo)
$noArgsStandardOutput = $noArgsProcess.StandardOutput.ReadToEnd()
$noArgsStandardError = $noArgsProcess.StandardError.ReadToEnd()
$noArgsProcess.WaitForExit()
$noArgsExitCode = $noArgsProcess.ExitCode
$noArgsProcess.Dispose()
$noArgsOutput = @($noArgsStandardOutput, $noArgsStandardError | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
$noArgsCadAfter = @(Get-CadProcessRows)
Assert-Gate ($noArgsExitCode -eq 2 -and
    ($noArgsOutput -join "`n").Contains('INVALID_MODE')) 'NOARGS_CLI_CONTRACT_FAILED' `
    'No-args must fail closed before any CAD activation.'
Assert-Gate ($noArgsCadBefore.Count -eq 0 -and $noArgsCadAfter.Count -eq 0) `
    'NOARGS_STARTED_CAD' 'The no-args negative control must not start SolidWorks.'

$receiptCadBefore = @(Get-CadProcessRows)
$receiptOutput = @(& $node $receiptVerifierPath 2>&1 | ForEach-Object { $_.ToString() })
$receiptExitCode = $LASTEXITCODE
$receiptCadAfter = @(Get-CadProcessRows)
Assert-Gate ($receiptExitCode -eq 0) 'RECEIPT_CONTRACT_VERIFIER_FAILED' `
    ($receiptOutput -join "`n")
Assert-Gate ($receiptCadBefore.Count -eq 0 -and $receiptCadAfter.Count -eq 0) `
    'RECEIPT_VERIFIER_STARTED_CAD' 'Receipt self-tests must not start SolidWorks.'

Assert-Gate (Test-Path -LiteralPath $toolchainVerifierPath -PathType Leaf) `
    'TOOLCHAIN_CONTRACT_VERIFIER_MISSING' 'Toolchain contract verifier is missing.'
$toolchainCadBefore = @(Get-CadProcessRows)
$toolchainOutput = @(& $node $toolchainVerifierPath 2>&1 | ForEach-Object { $_.ToString() })
$toolchainExitCode = $LASTEXITCODE
$toolchainCadAfter = @(Get-CadProcessRows)
Assert-Gate ($toolchainExitCode -eq 0) 'TOOLCHAIN_CONTRACT_VERIFIER_FAILED' `
    ($toolchainOutput -join "`n")
Assert-Gate ($toolchainCadBefore.Count -eq 0 -and $toolchainCadAfter.Count -eq 0) `
    'TOOLCHAIN_VERIFIER_STARTED_CAD' 'Toolchain self-tests must not start SolidWorks.'

Assert-Gate (Test-Path -LiteralPath $panelProofVerifierPath -PathType Leaf) `
    'PANEL_PROOF_CONTRACT_VERIFIER_MISSING' 'Panel proof contract verifier is missing.'
$panelProofCadBefore = @(Get-CadProcessRows)
$panelProofOutput = @(& $node $panelProofVerifierPath 2>&1 | ForEach-Object { $_.ToString() })
$panelProofExitCode = $LASTEXITCODE
$panelProofCadAfter = @(Get-CadProcessRows)
Assert-Gate ($panelProofExitCode -eq 0) 'PANEL_PROOF_CONTRACT_VERIFIER_FAILED' `
    ($panelProofOutput -join "`n")
Assert-Gate ($panelProofCadBefore.Count -eq 0 -and $panelProofCadAfter.Count -eq 0) `
    'PANEL_PROOF_VERIFIER_STARTED_CAD' 'Panel proof contract self-tests must not start SolidWorks.'

$exeBytes = [IO.File]::ReadAllBytes($exePath)
$exeUnicodeEven = [Text.Encoding]::Unicode.GetString($exeBytes)
$exeUnicodeOdd = [Text.Encoding]::Unicode.GetString($exeBytes, 1, $exeBytes.Length - 1)
$sketchOne = ([string][char]0x8349) + ([string][char]0x56FE) + '1'
$rightPlane = ([string][char]0x53F3) + ([string][char]0x89C6)
Assert-Gate ($exeUnicodeEven.Contains($sketchOne) -or $exeUnicodeOdd.Contains($sketchOne)) `
    'COMPILED_UTF8_SKETCH_NAME_MISSING' `
    'The compiled executable lost the localized Sketch1 dimension name.'
Assert-Gate ($exeUnicodeEven.Contains($rightPlane) -or $exeUnicodeOdd.Contains($rightPlane)) `
    'COMPILED_UTF8_RIGHT_PLANE_MISSING' `
    'The compiled executable lost the localized Right plane fallback.'

$cadAfter = @(Get-CadProcessRows)
Assert-Gate ($cadAfter.Count -eq 0) 'CAD_PROCESS_RESIDUAL' `
    'Static verification left a SolidWorks or sldProcMon process.'

$report = [ordered]@{
    schema = 'winnsen.16029.native_door_module_static_verification.v1'
    generated_at_utc = [DateTime]::UtcNow.ToString('o')
    status = 'STATIC_PASS_RUNTIME_NOT_RUN'
    scope = '888 cabinet width, 14 doors, 2 columns x 7, W381 x H(1781/7) door module'
    solidworks_started = $false
    freecad_used = $false
    neutral_exchange_used = $false
    runtime_cad_validation = 'NOT_RUN_BY_DESIGN'
    source = [ordered]@{
        path = [IO.Path]::GetFullPath($sourcePath)
        size_bytes = (Get-Item -LiteralPath $sourcePath).Length
        sha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    }
    executable = [ordered]@{
        path = [IO.Path]::GetFullPath($exePath)
        size_bytes = (Get-Item -LiteralPath $exePath).Length
        sha256 = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
        target = 'x64 .NET Framework 4'
    }
    stale_runtime_source_gate_present = $false
    build = $build
    toolchain_manifest = [ordered]@{
        path = [IO.Path]::GetFullPath($toolchainManifestPath)
        sha256 = (Get-FileHash -LiteralPath $toolchainManifestPath -Algorithm SHA256).Hash
        schema = [string]$toolchainManifest.schema
        live_source_executable_verifier_hashes = $true
    }
    required_source_gates = $requiredResults
    prohibited_source_gates = $prohibitedResults
    interop_contract = [ordered]@{
        mirror_part_2_signature = $mirrorMethod[0].ToString()
        break_all_external_file_references_2 = $true
        list_external_file_references_count = $true
        mirror_sheet_metal_information = $true
        option_values = $enumResults
    }
    unified_authorization_module = [ordered]@{
        path = $authorizationModulePath
        sha256 = (Get-FileHash -LiteralPath $authorizationModulePath -Algorithm SHA256).Hash
        door_tool_id = 'native_door_module_888x14_v1'
        exact_phases = @('door_module_888x14')
        lease_expiry_bound = $true
        exact_quality_boundary = $true
    }
    authorization_id_cross_language = $authorizationCrossCheck
    flat_import_contract = [ordered]@{
        v37_working_pack = [IO.Path]::GetFullPath($v37WorkingPack)
        v37_cad_count = $v37CadFiles.Count
        v37_part_count = @($v37CadFiles | Where-Object Extension -eq '.SLDPRT').Count
        v37_assembly_count = @($v37CadFiles | Where-Object Extension -eq '.SLDASM').Count
        door_flat_target_count = $doorFlatTargetNames.Count
        unique_target_names = $true
        v37_name_collisions = @()
        imported_to_working_pack = $false
        root_assembler_boundary = 'ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK'
    }
    cli_help = [ordered]@{
        exit_code = $helpExitCode
        output = $helpOutput
        solidworks_started = $false
    }
    cli_noargs = [ordered]@{
        exit_code = $noArgsExitCode
        output = $noArgsOutput
        solidworks_started = $false
    }
    receipt_contract = [ordered]@{
        verifier_path = [IO.Path]::GetFullPath($receiptVerifierPath)
        verifier_sha256 = (Get-FileHash -LiteralPath $receiptVerifierPath -Algorithm SHA256).Hash
        exit_code = $receiptExitCode
        output = $receiptOutput
        solidworks_started = $false
    }
    toolchain_consumer_contract = [ordered]@{
        verifier_path = [IO.Path]::GetFullPath($toolchainVerifierPath)
        verifier_sha256 = (Get-FileHash -LiteralPath $toolchainVerifierPath -Algorithm SHA256).Hash
        exit_code = $toolchainExitCode
        output = $toolchainOutput
        generic_manifest_count = 5
        lock_composite_manifest_count = 1
        solidworks_started = $false
    }
    panel_proof_contract = [ordered]@{
        verifier_path = [IO.Path]::GetFullPath($panelProofVerifierPath)
        verifier_sha256 = (Get-FileHash -LiteralPath $panelProofVerifierPath -Algorithm SHA256).Hash
        exit_code = $panelProofExitCode
        output = $panelProofOutput
        fixed_proof_path = 'evidence/left-panel-proof.v1.json'
        fixed_audit_path = 'evidence/left-panel-readonly-audit.v1.json'
        same_door_authorization_required = $true
        authorization_consumed = $false
        stage_receipt_created = $false
        runtime_cad_validation = 'NOT_RUN_BY_DESIGN'
        solidworks_started = $false
    }
    compiled_unicode_contract = [ordered]@{
        localized_sketch1_present = $true
        localized_right_plane_present = $true
        compiler_codepage = 65001
    }
    cad_processes_before = $cadBefore
    cad_processes_after = $cadAfter
    decision = 'CONDITIONAL_GO_FOR_ISOLATED_SOLIDWORKS_RUNTIME_VALIDATION_ONLY'
}

$outFullPath = [IO.Path]::GetFullPath($OutPath)
$parent = Split-Path -Parent $outFullPath
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$json = $report | ConvertTo-Json -Depth 12
[IO.File]::WriteAllText($outFullPath, $json, (New-Object Text.UTF8Encoding($false)))
$report
