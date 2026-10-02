#!/usr/bin/env node

import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const toolDirectory = dirname(fileURLToPath(import.meta.url))
const sourcePath = join(toolDirectory, 'BuildLockTopology888x14.cs')
const executablePath = join(toolDirectory, 'bin', 'BuildLockTopology888x14.exe')
const validatorPath = join(toolDirectory, 'ValidateLockTopology888x14.mjs')
const documentationPath = join(toolDirectory, 'LOCK_TOPOLOGY_888X14_STAGE.md')
const partInspectorSourcePath = join(toolDirectory, 'InspectLockTopology888x14.cs')
const partInspectorExecutablePath = join(toolDirectory, 'bin', 'InspectLockTopology888x14.exe')
const tongueInspectorSourcePath = join(toolDirectory, 'InspectAssemblyTongues888x14.cs')
const tongueInspectorExecutablePath = join(toolDirectory, 'bin', 'InspectAssemblyTongues888x14.exe')
const interopPath = join(toolDirectory, 'bin', 'SolidWorks.Interop.sldworks.dll')
const constantsPath = join(toolDirectory, 'bin', 'SolidWorks.Interop.swconst.dll')
const compilerPath = 'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe'
const toolchainManifestPath = join(toolDirectory, 'lock_toolchain_manifest.json')

function hashFile(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
}

function normalizedSource(source) {
  return source.replace(
    /private const string ExpectedSourceSha256 = "(?:__SOURCE_SHA256__|[0-9A-F]{64})";/,
    'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";',
    'TOOL_SOURCE_LIVE_HASH_MISMATCH',
    'tool_source_path',
  )
}

function run(command, args) {
  const result = spawnSync(command, args, {
    cwd: toolDirectory,
    encoding: 'utf8',
    windowsHide: true,
    timeout: 30_000,
  })
  return {
    command,
    args,
    status: result.status,
    signal: result.signal,
    error: result.error?.message ?? '',
    stdout: String(result.stdout ?? '').trim(),
    stderr: String(result.stderr ?? '').trim(),
  }
}

function processRows(imageName) {
  const result = run('tasklist.exe', ['/FI', 'IMAGENAME eq ' + imageName, '/FO', 'CSV', '/NH'])
  if (result.status !== 0) return [{ queryError: result.stderr || result.stdout || 'tasklist exit ' + result.status }]
  return result.stdout
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.toLowerCase().startsWith('"' + imageName.toLowerCase() + '"'))
}

function relatedCadProcesses() {
  return {
    SLDWORKS: processRows('SLDWORKS.exe'),
    sldProcMon: processRows('sldProcMon.exe'),
  }
}

function parseJson(stdout) {
  try {
    return JSON.parse(stdout)
  } catch {
    return null
  }
}

function check(name, ok, actual, expected) {
  return { name, ok: Boolean(ok), actual, expected }
}

function main() {
  const checks = []
  const requiredPaths = [sourcePath, executablePath, validatorPath, documentationPath, partInspectorSourcePath,
    partInspectorExecutablePath, tongueInspectorSourcePath, tongueInspectorExecutablePath,
    toolchainManifestPath, interopPath, constantsPath, compilerPath]
  checks.push(check('required_static_inputs_exist', requiredPaths.every(existsSync), requiredPaths.filter((path) => !existsSync(path)), []))

  const before = relatedCadProcesses()
  const cleanBefore = before.SLDWORKS.length === 0 && before.sldProcMon.length === 0
  checks.push(check('zero_cad_process_baseline', cleanBefore, before, { SLDWORKS: [], sldProcMon: [] }))

  if (!requiredPaths.every(existsSync) || !cleanBefore) {
    console.log(JSON.stringify({
      schema: 'winnsen.locker16029.native_888x14_lock_topology_static_verification.v1',
      status: 'STATIC_REFUSED',
      conditionalGo: false,
      solidWorksStarted: false,
      runtimeCadExecutionPerformed: false,
      checks,
    }, null, 2))
    process.exitCode = 1
    return
  }

  const source = readFileSync(sourcePath, 'utf8')
  const requiredSourceTokens = [
    'winnsen.locker16029.native_888x14_lock_topology.v2',
    'winnsen-16029-888w-14door-native-v1',
    'winnsen.native_execution_authorization.v1',
    'WINNSEN_NATIVE_16029_ATTEMPTS_ROOT',
    'WINNSEN_NATIVE_16029_PLAN_SHA256',
    'WINNSEN_NATIVE_EXECUTION_AUTH_PATH',
    'WINNSEN_NATIVE_EXECUTION_AUTH_SHA256',
    '@"native_cad\\working_pack"',
    'native_build_plan.json',
    '@"execution_authorizations\\native_lock_topology_888x14_v1.json"',
    'MirrorPart2(true, options, out temporaryRight)',
    'swMirrorPartOptions_ImportSolids',
    'swMirrorPartOptions_ImportSMInfo',
    'swMirrorPartOptions_ImportIndProps',
    'swMirrorPartOptions_ImportCutListProperties',
    'MIRROR_TEMP_RIGHT_HEALTH_GATE_FAILED',
    'MIRROR_TEMP_RIGHT_EXTERNAL_REFERENCE_GATE_FAILED',
    'MirrorStock',
    'MirrorPart',
    'SheetMetal',
    'FlatPattern',
    'BaseBody',
    'Imported',
    'temporary_right_reopen',
    'committed_right_reopen',
    'original_inventory.Count == 75',
    'part_count == 53',
    'assembly_count == 22',
    'post_inventory',
    'added_files',
    'single_evidence_commit',
    'RevisionNumber()',
    'StartsWith("28."',
    'SldWorks.Application.28',
    'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";',
    '--identity',
    '16029-1000w-14door-rule-evidence-20260519',
    'A1C15076110AF9954039B66599C1558F0B6F4C2F0FA04F3048656B06F46E5D21',
    '切除-拉伸3',
    '切除-拉伸5',
    'InstancePosition',
    'GetSketchSegments',
    'NATIVE_888_14_LOCK_ROWS',
    'MOVED_SEED_EDGE_READBACK_GATE_FAILED',
    'FRESH_SESSION_EDGE_GATE_FAILED',
    'LOCK_TOPOLOGY_888X14_COMPLETE',
    '锁舌.SLDPRT',
    '26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719',
    'expected_z_mm = -11.3',
    'StringArrayExactly(ArrayValue(authExecution, "phases")',
    'leaseExpiresAt',
    'qualityBoundary',
    'authorizationId',
    'AUTHORIZATION_TOP_LEVEL_KEYS_MISMATCH',
    'EVIDENCE_COMMIT_FAILED_ROLLBACK_COMPLETE',
    'CanPrepareSuccessEvidence',
    'ValidateLivePredecessorReceiptChain',
    'PREDECESSOR_RECEIPT_CHANGED_DURING_VALIDATION',
    'FROZEN_LOCK_TOOLCHAIN_MANIFEST_HASH_MISMATCH',
    'LOCK_RECEIPT_COMMIT_REREAD_FAILED',
    'UNPAIRED_SUCCESS_ARTIFACT_CONTAINMENT_FAILED',
    'stage_receipt_contract_gate',
    'predecessor_receipt_chain_gate',
    'evidence_commitment_sha256',
    'canonicalInventoryDigestContract',
  ]
  checks.push(check('required_contract_tokens_present', requiredSourceTokens.every((token) => source.includes(token)), requiredSourceTokens.filter((token) => !source.includes(token)), []))
  checks.push(check('success_commit_guard_is_fail_closed',
    /if\s*\(CanPrepareSuccessEvidence\(stagesComplete,\s*result\)\)/.test(source) &&
    !/if\s*\(result\.path_allowlist_match\)/.test(source) &&
    /!result\.rollback_attempted/.test(source) && /string\.IsNullOrWhiteSpace\(result\.error\)/.test(source),
    { helperGuard: /if\s*\(CanPrepareSuccessEvidence\(stagesComplete,\s*result\)\)/.test(source),
      legacyPathOnlyGuard: /if\s*\(result\.path_allowlist_match\)/.test(source) },
    { helperGuard: true, legacyPathOnlyGuard: false, rollbackAndErrorFailClosed: true }))
  const forbiddenExecutablePatterns = [
    /FreeCADCmd(?:\.exe)?/i,
    /Process\.Start\s*\([^)]*(?:\.step|\.stp|\.fcstd|freecad)/is,
    /\b(?:InsertScale|FeatureScale|ScaleFeatureData)\b/i,
    /UpdateExternalFileReferences/i,
    /BreakAllExternalFileReferences/i,
    /BreakAllExternalReferences/i,
    /ExactLocalInContext3/i,
    /ExactLocalBroken0/i,
    /isolated_native_cad/i,
    /Type\.GetTypeFromProgID\("SldWorks\.Application"/i,
  ]
  checks.push(check('no_legacy_or_scale_execution_path', forbiddenExecutablePatterns.every((pattern) => !pattern.test(source)), forbiddenExecutablePatterns.filter((pattern) => pattern.test(source)).map(String), []))

  const validatorSource = readFileSync(validatorPath, 'utf8')
  const requiredValidatorTokens = [
    'winnsen.locker16029.native_888x14_lock_topology_validation.v2',
    'winnsen.locker16029.native_888x14_lock_topology_inspector.v1',
    'winnsen.locker16029.native_888x14_assembly_tongues.v1',
    '--inspector',
    'build_evidence_sha256',
    'inspector_evidence_sha256',
    'root_assembly',
    'liveHashGate',
    'liveHashAnywhere',
    'validRotation',
    'translation_mm',
    'completeHandWrittenEvidenceCanPass',
    'matchingPlanManifestBindingPasses',
    'missingPlanManifestCanPass',
    'wrongPlanManifestCanPass',
    'matchingStageReceiptContractPasses',
    'missingStageReceiptContractCanPass',
    'wrongStageReceiptContractCanPass',
    'completeHandWrittenRootStageCanPass',
    'trustedToolchainManifests',
    'lock_receipt_atomic_evidence_pair_binding',
    'root_stage_exact_plan_authorization_tool_binding',
    'root_stage_evidence_fixed_live_hash_and_status',
    'templates alone cannot pass',
    'native_lock_topology_inspector_v1',
    'native_assembly_tongue_inspector_888x14_v1',
    'EXPECTED_PART_INSPECTOR_SOURCE_SHA256',
    'EXPECTED_TONGUE_INSPECTOR_SOURCE_SHA256',
    'exactKeys',
    'evidence_commit_verified',
  ]
  const forbiddenValidatorPatterns = [
    /--left-edges/i,
    /--right-edges/i,
    /syntheticEdgeEvidence/i,
    /engineeringAssistanceEligible/i,
  ]
  checks.push(check('validator_requires_live_inspector_root_and_transforms', requiredValidatorTokens.every((token) => validatorSource.includes(token)), requiredValidatorTokens.filter((token) => !validatorSource.includes(token)), []))
  checks.push(check('validator_has_no_template_evidence_pass_route', forbiddenValidatorPatterns.every((pattern) => !pattern.test(validatorSource)), forbiddenValidatorPatterns.filter((pattern) => pattern.test(validatorSource)).map(String), []))

  const inspectorContracts = [
    [partInspectorSourcePath, 'native_lock_topology_inspector_v1', 'lock_topology_888x14.inspector.v1.json', 'GetEdges()', 'GetTypeName2()', 'ListExternalFileReferencesCount', 'swOpenDocOptions_ReadOnly'],
    [tongueInspectorSourcePath, 'native_assembly_tongue_inspector_888x14_v1', 'assembly_lock_tongues_888x14.json', 'Transform2', 'rotation', 'translation_mm', 'swOpenDocOptions_ReadOnly'],
  ]
  checks.push(check('real_readonly_inspector_sources_present', inspectorContracts.every(([path, ...tokens]) => existsSync(path) && tokens.every((token) => readFileSync(path, 'utf8').includes(token))), inspectorContracts.map(([path]) => path), 'two real CAD inspectors'))

  const documentation = readFileSync(documentationPath, 'utf8')
  const requiredDocumentationTokens = [
    'MirrorPart2 临时 R',
    'native_cad\\working_pack',
    'execution_authorizations\\native_lock_topology_888x14_v1.json',
    'planningOnly = true',
    'executorImplemented = false',
    '完整读回 3×3 rotation 与 XYZ',
    '没有启动 SolidWorks',
  ]
  checks.push(check('documentation_matches_v2_boundary', requiredDocumentationTokens.every((token) => documentation.includes(token)), requiredDocumentationTokens.filter((token) => !documentation.includes(token)), []))

  const normalizedSourceText = normalizedSource(source)
  const normalizedSourceSha256 = createHash('sha256').update(normalizedSourceText, 'utf8').digest('hex').toUpperCase()
  const compiledSourceText = normalizedSourceText.replace(
    'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";',
    `private const string ExpectedSourceSha256 = "${normalizedSourceSha256}";`,
  )
  const compileSourcePath = join(toolDirectory, `.BuildLockTopology888x14.compile-${process.pid}.cs`)
  const compileExecutablePath = join(toolDirectory, 'bin', `.BuildLockTopology888x14.compile-${process.pid}.exe`)
  writeFileSync(compileSourcePath, compiledSourceText, 'utf8')
  let compile
  try {
    compile = run(compilerPath, [
      '/nologo',
      '/platform:x64',
      '/target:exe',
      '/optimize+',
      '/out:' + compileExecutablePath,
      '/reference:' + interopPath,
      '/reference:' + constantsPath,
      '/reference:System.Management.dll',
      '/reference:System.Web.Extensions.dll',
      compileSourcePath,
    ])
  } finally {
    if (existsSync(compileSourcePath)) unlinkSync(compileSourcePath)
  }
  checks.push(check('csharp_static_compile', compile.status === 0 && existsSync(compileExecutablePath), compile, { status: 0, temporaryExecutableExists: true }))

  let noArgs = { status: null, stdout: '', stderr: '', error: 'compile failed' }
  let csharpSelfTest = { status: null, stdout: '', stderr: '', error: 'compile failed' }
  let identity = { status: null, stdout: '', stderr: '', error: 'compile failed' }
  if (compile.status === 0 && existsSync(compileExecutablePath)) {
    noArgs = run(compileExecutablePath, [])
    csharpSelfTest = run(compileExecutablePath, ['--self-test'])
    identity = run(compileExecutablePath, ['--identity'])
  }
  const csharpSelfDocument = parseJson(csharpSelfTest.stdout)
  checks.push(check('csharp_no_argument_fail_closed', noArgs.status === 2 && /Usage: BuildLockTopology888x14\.exe/.test(noArgs.stderr + noArgs.stdout), noArgs, { status: 2, usage: true }))
  checks.push(check('csharp_self_test_no_sw', csharpSelfTest.status === 0 && csharpSelfDocument?.selfTest === 'PASS' && csharpSelfDocument?.solidWorksStarted === false && csharpSelfDocument?.successCommitPredicate === true && csharpSelfDocument?.exceptionCannotCommit === true && csharpSelfDocument?.rollbackCannotCommit === true && csharpSelfDocument?.sharedCommitmentContract === true && csharpSelfDocument?.sharedNumericCommitmentContract === true && csharpSelfDocument?.canonicalInventoryDigestContract === true, { run: csharpSelfTest, document: csharpSelfDocument }, { status: 0, selfTest: 'PASS', solidWorksStarted: false, successCommitPredicate: true, exceptionCannotCommit: true, rollbackCannotCommit: true, sharedCommitmentContract: true, sharedNumericCommitmentContract: true, canonicalInventoryDigestContract: true }))
  const identityRows = Object.fromEntries(identity.stdout.split(/\r?\n/).map((line) => line.split('=')).filter((row) => row.length === 2))
  checks.push(check('temporary_compile_source_identity_binding', identity.status === 0 && identityRows.sourceNormalizedSha256 === normalizedSourceSha256 && identityRows.executableSha256 === hashFile(compileExecutablePath) && hashFile(sourcePath) === createHash('sha256').update(source, 'utf8').digest('hex').toUpperCase(), { run: identity, identity: identityRows, normalizedSourceSha256, executableSha256: existsSync(compileExecutablePath) ? hashFile(compileExecutablePath) : '' }, { status: 0, sourceNormalizedSha256: normalizedSourceSha256, executableSha256MatchesTemporary: true, sourceUnchanged: true }))
  if (existsSync(compileExecutablePath)) unlinkSync(compileExecutablePath)

  for (const [inspectorSource, inspectorExe, toolId] of [
    [partInspectorSourcePath, partInspectorExecutablePath, 'native_lock_topology_inspector_v1'],
    [tongueInspectorSourcePath, tongueInspectorExecutablePath, 'native_assembly_tongue_inspector_888x14_v1'],
  ]) {
    const raw = readFileSync(inspectorSource, 'utf8')
    const normalized = raw.replace(/private const string ExpectedSourceSha256\s*=\s*"(?:__SOURCE_SHA256__|[0-9A-F]{64})";/, (match) => match.replace(/[0-9A-F]{64}/, '__SOURCE_SHA256__'))
    const normalizedHash = createHash('sha256').update(normalized, 'utf8').digest('hex').toUpperCase()
    const compiled = normalized.replace('__SOURCE_SHA256__', normalizedHash)
    const temporary = join(toolDirectory, `.${toolId}-${process.pid}.cs`)
    const temporaryExecutable = join(toolDirectory, 'bin', `.${toolId}-${process.pid}.exe`)
    writeFileSync(temporary, compiled, 'utf8')
    let build
    try {
      build = run(compilerPath, ['/nologo', '/platform:x64', '/target:exe', '/optimize+', '/out:' + temporaryExecutable,
        '/reference:' + interopPath, '/reference:' + constantsPath, '/reference:System.Management.dll', '/reference:System.Web.Extensions.dll', temporary])
    } finally { if (existsSync(temporary)) unlinkSync(temporary) }
    const self = build.status === 0 ? run(temporaryExecutable, ['--self-test']) : { status: null, stdout: '', stderr: build.stderr }
    const identityRun = build.status === 0 ? run(temporaryExecutable, ['--identity']) : { status: null, stdout: '', stderr: build.stderr }
    const identityMap = Object.fromEntries(identityRun.stdout.split(/\r?\n/).map((line) => line.split('=')).filter((row) => row.length === 2))
    checks.push(check(`${toolId}_x64_compile_identity_selftest`, build.status === 0 && parseJson(self.stdout)?.selfTest === 'PASS' && parseJson(self.stdout)?.solidWorksStarted === false && identityMap.sourceNormalizedSha256 === normalizedHash && identityMap.executableSha256 === hashFile(temporaryExecutable), { build, self, identity: identityMap }, { x64Compile: true, selfTest: 'PASS', solidWorksStarted: false, temporaryIdentityBound: true }))
    if (existsSync(temporaryExecutable)) unlinkSync(temporaryExecutable)
  }

  const liveIdentities = {}
  for (const [id, source, exe] of [
    ['native_lock_topology_888x14_v1', sourcePath, executablePath],
    ['native_lock_topology_inspector_v1', partInspectorSourcePath, partInspectorExecutablePath],
    ['native_assembly_tongue_inspector_888x14_v1', tongueInspectorSourcePath, tongueInspectorExecutablePath],
  ]) {
    const runIdentity = run(exe, ['--identity'])
    const rows = Object.fromEntries(runIdentity.stdout.split(/\r?\n/).map((line) => line.split('=')).filter((row) => row.length === 2))
    liveIdentities[id] = { sourcePath: resolve(source), sourceNormalizedSha256: rows.sourceNormalizedSha256,
      executablePath: resolve(exe), executableSha256: rows.executableSha256,
      identityExitCode: runIdentity.status, liveExecutableSha256: hashFile(exe) }
  }
  const manifest = JSON.parse(readFileSync(toolchainManifestPath, 'utf8'))
  const manifestTools = manifest?.tools ?? {}
  const manifestExact = manifest?.schema === 'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1' &&
    manifest?.generatedBy === 'VerifyLockTopology888x14Static.mjs' &&
    JSON.stringify(Object.keys(manifestTools).sort()) === JSON.stringify(Object.keys(liveIdentities).sort()) &&
    Object.entries(liveIdentities).every(([id, live]) => {
      const pinned = manifestTools[id] ?? {}
      return JSON.stringify(Object.keys(pinned).sort()) === JSON.stringify(['sourcePath','sourceNormalizedSha256','executablePath','executableSha256'].sort()) &&
        resolve(pinned.sourcePath) === live.sourcePath && resolve(pinned.executablePath) === live.executablePath &&
        String(pinned.sourceNormalizedSha256).toUpperCase() === String(live.sourceNormalizedSha256).toUpperCase() &&
        String(pinned.executableSha256).toUpperCase() === live.liveExecutableSha256 &&
        String(live.executableSha256).toUpperCase() === live.liveExecutableSha256 && live.identityExitCode === 0
    }) && manifest?.validator && JSON.stringify(Object.keys(manifest.validator).sort()) === JSON.stringify(['path','sha256'].sort()) &&
    resolve(manifest.validator.path) === resolve(validatorPath) && String(manifest.validator.sha256).toUpperCase() === hashFile(validatorPath)
  checks.push(check('frozen_toolchain_manifest_matches_live_identities', manifestExact,
    { manifest, liveIdentities, validatorSha256: hashFile(validatorPath), manifestSha256: hashFile(toolchainManifestPath) },
    { frozenManifestUnchangedByVerifier: true, exactThreeToolsAndValidator: true }))

  const validatorSyntax = run(process.execPath, ['--check', validatorPath])
  const validatorSelfTest = run(process.execPath, [validatorPath, '--self-test', '--toolchain-manifest', toolchainManifestPath, '--toolchain-manifest-sha256', hashFile(toolchainManifestPath)])
  const validatorSelfDocument = parseJson(validatorSelfTest.stdout)
  checks.push(check('validator_syntax', validatorSyntax.status === 0, validatorSyntax, { status: 0 }))
  checks.push(check('validator_self_test_no_sw', validatorSelfTest.status === 0 && validatorSelfDocument?.selfTest === 'PASS' && validatorSelfDocument?.solidWorksStarted === false && validatorSelfDocument?.completeHandWrittenEvidenceCanPass === false && validatorSelfDocument?.wrongInspectorIdentityCanPass === false && validatorSelfDocument?.matchingPlanManifestBindingPasses === true && validatorSelfDocument?.missingPlanManifestCanPass === false && validatorSelfDocument?.wrongPlanManifestCanPass === false && validatorSelfDocument?.matchingStageReceiptContractPasses === true && validatorSelfDocument?.missingStageReceiptContractCanPass === false && validatorSelfDocument?.wrongStageReceiptContractCanPass === false && validatorSelfDocument?.completeHandWrittenRootStageCanPass === false, { run: validatorSelfTest, document: validatorSelfDocument }, { status: 0, selfTest: 'PASS', solidWorksStarted: false, completeHandWrittenEvidenceCanPass: false, wrongInspectorIdentityCanPass: false, matchingPlanManifestBindingPasses: true, missingPlanManifestCanPass: false, wrongPlanManifestCanPass: false, matchingStageReceiptContractPasses: true, missingStageReceiptContractCanPass: false, wrongStageReceiptContractCanPass: false, completeHandWrittenRootStageCanPass: false }))

  const after = relatedCadProcesses()
  checks.push(check('zero_cad_process_after', after.SLDWORKS.length === 0 && after.sldProcMon.length === 0, after, { SLDWORKS: [], sldProcMon: [] }))

  const failedChecks = checks.filter((item) => !item.ok)
  const files = [sourcePath, executablePath, partInspectorSourcePath, partInspectorExecutablePath,
    tongueInspectorSourcePath, tongueInspectorExecutablePath, validatorPath, documentationPath,
    fileURLToPath(import.meta.url), toolchainManifestPath]
    .filter(existsSync)
    .map((path) => ({ path: resolve(path), sha256: hashFile(path) }))
  const result = {
    schema: 'winnsen.locker16029.native_888x14_lock_topology_static_verification.v1',
    status: failedChecks.length === 0 ? 'STATIC_PASS' : 'STATIC_FAIL',
    conditionalGo: failedChecks.length === 0,
    solidWorksStarted: false,
    runtimeCadExecutionPerformed: false,
    limitation: 'No SolidWorks runtime, CAD mutation, save, reopen, or final edge capture was performed by this static verification.',
    before,
    after,
    files,
    checks,
    failedChecks: failedChecks.map((item) => item.name),
  }
  console.log(JSON.stringify(result, null, 2))
  process.exitCode = failedChecks.length === 0 ? 0 : 1
}

main()
