import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '../../../../../../../')
const sourcePath = resolve(here, 'TopCoverRight760Native.cs')
const executablePath = resolve(here, 'TopCoverRight760Native.exe')
const manifestPath = resolve(here, 'toolchain_manifest.json')
const buildPath = resolve(here, 'Build-TopCoverRight760Native.ps1')
const wrapperPath = resolve(here, 'Verify-TopCoverRight760Native.ps1')
const configPath = resolve(here, 'TopCoverRight760Native.exe.config')
const readmePath = resolve(here, 'README.md')
const outputPath = resolve(here, 'static_verification.json')
const sldworksInteropPath = resolve(here, 'SolidWorks.Interop.sldworks.dll')
const swconstInteropPath = resolve(here, 'SolidWorks.Interop.swconst.dll')
const rulePath = resolve(repoRoot,
  'data/native_model_requests/attempts/' +
  'NATIVE-20260824T132708Z-888W14D-RIGHT-RULE-R1/attempt-0001/' +
  'evidence/private/topcover_right_native_rule_v2.json')
const independentRuleVerifierPath = resolve(repoRoot,
  'tools/verify_16029_topcover_right_native_rule_v2.mjs')
const correctionPath = resolve(repoRoot,
  'data/native_model_requests/attempts/' +
  'NATIVE-20260826T023534Z-888W14D-RIGHT-SIGNATURE-R1/attempt-0001/' +
  'evidence/private/topcover_right_native_signature_correction_v1.json')
const independentCorrectionVerifierPath = resolve(repoRoot,
  'tools/verify_16029_topcover_right_native_signature_correction_v1.mjs')

const source = readFileSync(sourcePath, 'utf8')
const sourceNormalized = source.replace(/\s+/g, ' ')
const build = readFileSync(buildPath, 'utf8')
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
const checks = []

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function add(name, pass, details = undefined) {
  checks.push({ name, pass: Boolean(pass),
    ...(details === undefined ? {} : { details }) })
}

function cadSnapshot() {
  const raw = execFileSync('powershell.exe', ['-NoProfile', '-Command',
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | " +
    "Sort-Object ProcessName,Id | ForEach-Object { \"$($_.ProcessName):$($_.Id)\" }) -join ','"],
  { encoding: 'utf8', windowsHide: true }).trim()
  return raw === '' ? [] : raw.split(',')
}

function normalizedSourceSha256(text) {
  const normalizedNewlines = text.replace(/\r\n?/g, '\n')
  const pattern = /(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/g
  assert.equal([...normalizedNewlines.matchAll(pattern)].length, 1)
  return sha256(Buffer.from(
    normalizedNewlines.replace(pattern, '$1__SOURCE_SHA256__$2'), 'utf8'))
}

function parseJson(text) {
  try { return JSON.parse(text) } catch { return null }
}

const cadBefore = cadSnapshot()
add('cad_zero_before_static_verification', cadBefore.length === 0, cadBefore)

const requiredFiles = [sourcePath, executablePath, manifestPath, buildPath,
  wrapperPath, configPath, readmePath, rulePath, independentRuleVerifierPath,
  correctionPath, independentCorrectionVerifierPath, sldworksInteropPath,
  swconstInteropPath]
add('required_files_are_regular_single_link_files', requiredFiles.every((path) => {
  if (!existsSync(path)) return false
  const info = statSync(path)
  return info.isFile() && Number(info.nlink) === 1
}))

const sourceNormalizedSha256 = normalizedSourceSha256(source)
const embeddedSourceSha256 = source.match(
  /ExpectedSourceSha256\s*=\s*"([A-F0-9]{64})";/)?.[1]
add('source_normalized_identity_is_exact',
  embeddedSourceSha256 === sourceNormalizedSha256)
add('manifest_binds_live_source_executable_and_verifier',
  manifest?.schema === 'winnsen.16029.native_toolchain_manifest.v1' &&
  manifest?.generatedBy === 'Build-TopCoverRight760Native.ps1' &&
  manifest?.tool?.id === 'topcover_right_760_native_v1' &&
  manifest?.tool?.phase === 'right_760_native_pilot_phase1' &&
  manifest?.tool?.cadImplemented === true &&
  manifest?.tool?.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  manifest?.tool?.executableSha256 === sha256(readFileSync(executablePath)) &&
  manifest?.tool?.verifierSha256 ===
    sha256(readFileSync(fileURLToPath(import.meta.url))) &&
  manifest?.tool?.runtimeDependencies?.length === 2 &&
  manifest.tool.runtimeDependencies[0]?.sha256 ===
    'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC' &&
  manifest.tool.runtimeDependencies[1]?.sha256 ===
    '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359')

add('fixed_rule_hash_is_live',
  sha256(readFileSync(rulePath)) ===
  'EFAB0AE94FC50C0ECC05CA2384D24A93DE3E276918BAAB68CA96EFADBCAEF1F0')
add('fixed_signature_correction_hash_is_live',
  sha256(readFileSync(correctionPath)) ===
  '39C2E5BB2A44EDEF64BF40C4C5DB5023B0FD0BC27F7A8304662B6CEDA0CC7507')

const ruleVerification = spawnSync(process.execPath,
  [independentRuleVerifierPath, rulePath],
  { cwd: repoRoot, encoding: 'utf8', windowsHide: true })
const ruleReport = parseJson(ruleVerification.stdout ?? '')
add('independent_rule_verifier_passes_21_of_21',
  ruleVerification.status === 0 && ruleReport?.status === 'PASS' &&
  ruleReport?.checkCount === 21 && ruleReport?.failed === 0 &&
  ruleReport?.cadStarted === false)

const correctionVerification = spawnSync(process.execPath,
  [independentCorrectionVerifierPath, correctionPath],
  { cwd: repoRoot, encoding: 'utf8', windowsHide: true })
const correctionReport = parseJson(correctionVerification.stdout ?? '')
add('independent_signature_correction_verifier_passes_16_of_16',
  correctionVerification.status === 0 && correctionReport?.status === 'PASS' &&
  correctionReport?.checkCount === 16 && correctionReport?.failed === 0 &&
  correctionReport?.cadStarted === false)

const forbidden = /(SeedPack|FreeCAD|FCStd|PackAndGo|BreakAll|ConvertToSheetMetal|InsertPart\s*\(|DerivedPart|MoveBody|CreateFeatureFromBody|ImportDoc|LoadFile|BuildNativeFront|MirrorPart2|BuildNativeRightMirror|InsertSheetMetalBaseFlange2|InsertSheetMetal3dBend|SM3dBend)/i
add('legacy_import_and_old_generator_routes_are_absent', !forbidden.test(source))
add('one_native_save_callsite_only',
  (source.match(/\.SaveAs3\(/g) ?? []).length === 1)
add('saved_part_hash_is_read_only_after_owned_session_exit',
  source.indexOf('CloseOwnedSolidWorks(ref application, processAudit, session);') <
  source.indexOf('string partSha256 = Sha256File(partPath);'))
add('runtime_never_uses_recursive_delete_or_writealltext',
  !/Directory\.Delete\([^\n]*,\s*true\)/.test(source) &&
  !/File\.WriteAllText\(/.test(source))

add('authorization_is_fixed_short_lived_and_hash_bound', [
  'AuthorizationSchema', 'AUTHORIZATION_LIVE_HASH_INVALID',
  'AUTHORIZATION_NOT_LIVE_OR_TOO_LONG', 'toolSourceNormalizedSha256',
  'toolExecutableSha256', 'execution_authorizations', 'AuthorizationFileName',
  'topcover_right_760_native_v1.json',
].every((token) => source.includes(token)))

add('evidence_and_receipt_use_createnew_reread_pair', [
  'FileMode.CreateNew', 'stream.Flush(true)', 'WriteCreateNewAndReread',
  'PilotEvidenceReceiptPairValid', 'ReceiptSchema', 'evidenceSha256',
  'topcover_right_760_native_pilot',
].every((token) => source.includes(token)))

add('rollback_is_attempt_local_and_file_whitelisted', [
  'RollbackOwnedPartDirectory', 'SearchOption.TopDirectoryOnly',
  'FileLinkCount(entry) != 1', 'new FileInfo(entry).Length <= 4096',
  'Directory.Delete(partDirectory, false)',
].every((token) => source.includes(token)))

add('owned_cad_process_identity_is_proven', [
  'GetProcessID()', 'startUtcTicks', 'MainModule.FileName',
  'ExpectedSolidWorksExeSha256', 'ExpectedMonitorExeSha256',
  '--ppid=', 'refused to kill unproven', 'KillExactIdentity',
  'WaitForGlobalCadQuiescence',
].every((token) => source.includes(token)))

add('left_sharp_reference_and_native_insert_bends_route_are_fixed', [
  'NativeRouteId', 'right_760_sharp_stock_insert_bends_v1',
  'SharpReferenceRelativePath',
  'TOPCOVER-REFERENCE-20260828T001317Z-LEFT-SHARP-R9',
  'topcover_left_reference_brep_capture_v7.json',
  'SharpReferenceSha256',
  '9B841451011285DE50EFBC35F979519B7B1082FF411C86852B67BBA45A8952B8',
  'fixed_left_sharp_reference_is_live',
].every((token) => source.includes(token)))

add('run_path_uses_only_new_sharp_stock_insert_bends_core', [
  'model = application.NewDocument(template, 0, 0, 0) as ModelDoc2',
  'BuildNativeRightSharpInsertBendsCore(application, model, evidence)',
  'FeatureExtrusionThin2(', 'swThinWallMidPlane',
  'part.InsertBends2(0.0005, "", 0.5, 0.0, true, 0.5, true)',
].every((token) => sourceNormalized.includes(token)) &&
  !sourceNormalized.includes('model = BuildNativeRightMirror(') &&
  !sourceNormalized.includes('BuildNativeRight(application, model'))

add('sharp_stock_exact_geometry_and_eight_base_holes_are_gated', [
  'SharpStockMatchesReference', '72104.76859853993',
  '181466.01564657912', '0.5660224334985384',
  'geometry.faceCount == 29', 'geometry.edgeCount == 73',
  'geometry.loopCount == 53', 'geometry.loopEdgeReferenceCount == 146',
  'geometry.cylinderFaceCount == 8',
  'geometry.circleEdgeReferenceCount == 32',
  'new HolePoint(380.0, 1864.2, -448.0, 1.0)',
  'new HolePoint(365.0, 1847.0, -515.0, 2.75)',
  'new HolePoint(330.0, 1911.1, -489.0, 3.25)',
  'new HolePoint(330.0, 1906.0, -85.0, 5.0)',
].every((token) => sourceNormalized.includes(token)))

add('authorization_binds_sharp_reference_and_blocks_legacy_fallback', [
  'routeId', 'sharpReferenceRelativePath', 'sharpReferenceSha256',
  'sharpStockInsertBendsProbeOnly', 'noLegacyGeneratorFallback',
  'Text(root, "routeId") == NativeRouteId',
  'SharpReferenceRelativePath', 'SharpReferenceSha256',
].every((token) => source.includes(token)))

add('sharp_stock_is_built_natively_before_insert_bends', [
  'BuildNativeRightSharpInsertBendsCore',
  'CreateFrontOffsetPlane(model, -549.0)',
  'CreateFrontOffsetPlane(model, -47.0)',
  'FeatureExtrusionThin2(', 'swThinWallMidPlane',
  'CreateBlindRectangleCut', 'CreateThroughHolesOnSelectedFace',
  'evidence["sharpStockGeometryPass"] = sharpPass',
  'NATIVE_SHARP_STOCK_GEOMETRY_REGRESSION_FAILED',
  'AddBuildStage(evidence, "native_sharp_stock", sharp)',
].every((token) => sourceNormalized.includes(token)))

add('insert_bends2_core_contract_is_exact', [
  'part.InsertBends2(0.0005, "", 0.5, 0.0, true, 0.5, true)',
  'InsertBendsCoreMatches', 'value.sheetMetalCount == 1',
  'value.oneBendCount == 5', 'value.flatPatternCount == 1',
  'value.flattenBendsCount == 1', 'value.processBendsCount == 1',
  'value.sketchedBendGroupCount == 0', 'value.bends.Count == 5',
  'swBendAllowanceTypes_e.swBendAllowanceKFactor',
  'swBendType_e.swSharpBend',
  'bend.canonicalDirection == 2', 'bend.canonicalDown) == 4',
  'bend.canonicalDirection == 1', '!bend.canonicalDown) == 1',
  'NATIVE_INSERT_BENDS_CORE_REGRESSION_FAILED',
].every((token) => sourceNormalized.includes(token)))

add('insert_bends_parameter_boundary_is_explicit_and_unmodified', [
  'insertBendsParameterBoundary', 'preserveNativeKFactor',
  'preserveGlobalRadiusMm', 'deductionOverrideApplied',
  'SW2020 nested sharp bends return error 51',
].every((token) => source.includes(token)) &&
  !source.includes('TuneInsertBendsParameters') &&
  !source.includes('CreateCustomBendAllowance') &&
  !source.includes('ModifyDefinition('))

add('post_cuts_and_pem_are_native_and_geometry_gated', [
  'CreateNativeRightPostFeatures', 'POST_TOP_CUTS', 'POST_OUTER_CUTS',
  'new HolePoint(355, 1917, -100, 4.4)',
  'CreateCutProfileOnSelectedFace',
  'FeatureCut3(true, false, reverseDirection',
  'POST_PEM_UNDERSIDE_FACE_SELECT_FAILED',
  'new Point3(355, 1916.2, -420)',
  'CreatePemBossesOnSelectedFace',
  'CreateCircleByRadius(center.x, center.y, 0, 0.0055)',
  'CreateCircleByRadius(center.x, center.y, 0, 0.0030)',
  'FeatureExtrusion2(true, false, false',
  'postFeatureGeometryDiagnostic', 'postFeatureFeatureDiagnostic',
  'AddBuildStage(evidence, "native_post_cuts_and_pem", geometry)',
].every((token) => sourceNormalized.includes(token)))

add('feature_tree_rejects_legacy_routes_and_requires_five_sharp_bends', [
  'CaptureFeatureSnapshot', 'CaptureFeatureRecursive',
  'type.EndsWith("3dBend"', 'output.sketchedBendGroupCount++',
  'string.Equals(type, "MirrorStock"',
  'string.Equals(type, "MirrorPart"',
  'output.forbiddenImportFeatureCount++',
  'IOneBendFeatureData', 'CaptureOneBendDefinition',
  'return InsertBendsCoreMatches(value) && value.mirrorPartCount == 0',
  'value.forbiddenImportFeatureCount == 0',
  'value.sketchedBendGroupCount == 0',
].every((token) => sourceNormalized.includes(token)) &&
  !source.includes('ISketchedBendFeatureData') &&
  !source.includes('InsertSheetMetal3dBend'))

add('sheet_metal_and_one_bend_definitions_are_read_back', [
  'ISheetMetalFeatureData', 'data.Thickness * 1000.0',
  'output.sheetMetalRadiusMm = data.BendRadius * 1000.0',
  'output.sheetMetalKFactor = data.KFactor',
  'output.sheetMetalAllowanceType = data.BendAllowanceType',
  'IOneBendFeatureData', 'output.angleRadians = data.BendAngle',
  'output.direction = data.BendDirection', 'output.down = data.BendDown',
  'output.order = data.BendOrder', 'output.radiusMm = data.BendRadius * 1000.0',
  'output.kFactor = data.KFactor',
  'output.allowanceType = data.BendAllowanceType',
  'output.useDefaultRelief = data.UseDefaultBendRelief',
  'output.useAutoRelief = data.UseAutoRelief',
  'data.IAccessSelections2(model, null)', 'data.ReleaseSelectionAccess()',
  'GetErrorCode2(out warning)', 'value.issues.Count == 0',
].every((token) => sourceNormalized.includes(token)))

add('fresh_reopen_checks_full_geometry_features_and_no_external_refs', [
  'swOpenDocOptions_ReadOnly', 'CaptureGeometry(model)',
  'CaptureFeatureSnapshot(model)', 'GeometryMatchesContract',
  'FeatureMatchesContract', 'ListExternalFileReferencesCount()',
  'fileUnchanged',
].every((token) => source.includes(token)))

add('flat_and_folded_brep_signatures_are_exactly_gated', [
  'GetBodyBox()', 'GetMassProperties(7850.0)', 'GetFaceCount()',
  'GetEdgeCount()', 'GetLoops()', 'GetCurve()', 'IsCylinder()', 'IsCircle()',
  'DetailedGeometrySignature', 'expectedFlatDetailedSignature',
  'expectedFoldedDetailedSignature', 'expectedLoopEdgeReferenceCount',
  'expectedCircleEdgeReferenceCount', 'SignatureCorrectionSchema',
  'SignatureCorrectionSha256', 'LoadAndApplySignatureCorrection',
  'SameDetailedPoint', 'BoolKey', 'points = "FULL"',
].every((token) => source.includes(token)))

add('build_references_pinned_interop_and_system_management',
  build.includes('/reference:System.Management.dll') &&
  build.includes('SolidWorks.Interop.sldworks.dll') &&
  build.includes('SolidWorks.Interop.swconst.dll') &&
  build.includes('Pinned interop source hash mismatch') &&
  build.includes('Existing local interop hash mismatch') &&
  build.includes('Build requires zero CAD processes'))

const escapedInteropPath = sldworksInteropPath.replaceAll("'", "''")
const reflection = execFileSync('powershell.exe', ['-NoProfile', '-Command',
  `$a=[Reflection.Assembly]::LoadFrom('${escapedInteropPath}');` +
  "$f=$a.GetType('SolidWorks.Interop.sldworks.IFeatureManager');" +
  "$p=$a.GetType('SolidWorks.Interop.sldworks.IPartDoc');" +
  "$o=$a.GetType('SolidWorks.Interop.sldworks.IOneBendFeatureData');" +
  "$s=$a.GetType('SolidWorks.Interop.sldworks.ISheetMetalFeatureData');" +
  "[bool]($p.GetMethod('InsertBends2') -and $f.GetMethod('FeatureExtrusionThin2') -and " +
  "$f.GetMethod('InsertRefPlane') -and " +
  "$f.GetMethod('FeatureCut3') -and " +
  "$f.GetMethod('FeatureExtrusion2') -and $o.GetProperty('BendAngle') -and " +
  "$o.GetProperty('BendDirection') -and $o.GetProperty('BendOrder') -and " +
  "$o.GetProperty('BendDown').CanWrite -and " +
  "$o.GetProperty('UseDefaultBendRelief') -and $o.GetProperty('UseAutoRelief') -and " +
  "$o.GetProperty('AutoReliefType') -and $o.GetProperty('ReliefDepth') -and " +
  "$o.GetProperty('ReliefWidth') -and $o.GetProperty('ReliefRatio') -and " +
  "$o.GetProperty('BendRadius') -and $o.GetProperty('UseDefaultBendRadius') -and " +
  "$o.GetProperty('KFactor') -and $o.GetProperty('BendAllowanceType') -and " +
  "$s.GetProperty('Thickness') -and $s.GetProperty('UseAutoRelief') -and " +
  "$s.GetProperty('AutoReliefType') -and $s.GetProperty('ReliefRatio'))"],
{ encoding: 'utf8', windowsHide: true }).trim()
add('solidworks_2020_interop_signatures_are_present', reflection === 'True')

const selfTestRun = spawnSync(executablePath, ['--self-test'],
  { cwd: here, encoding: 'utf8', windowsHide: true })
const selfTest = parseJson(selfTestRun.stdout ?? '')
add('self_test_passes_18_of_18_without_cad',
  selfTestRun.status === 0 && selfTest?.status === 'PASS' &&
  selfTest?.cadStarted === false && selfTest?.checksTotal === 18 &&
  selfTest?.checksFailed === 0 && selfTest?.failure === null)

const identityRun = spawnSync(executablePath, ['--identity'],
  { cwd: here, encoding: 'utf8', windowsHide: true })
const identity = parseJson(identityRun.stdout ?? '')
add('identity_matches_live_source', identityRun.status === 0 &&
  identity?.toolId === 'topcover_right_760_native_v1' &&
  identity?.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  identity?.cadImplemented === true && identity?.ordinaryExitCode === 0)

const invalidInvocation = spawnSync(executablePath, [],
  { cwd: here, encoding: 'utf8', windowsHide: true })
add('invalid_invocation_fails_before_cad', invalidInvocation.status === 64 &&
  /ARGUMENT_GRAMMAR_INVALID/.test(invalidInvocation.stderr ?? ''))

const cadAfter = cadSnapshot()
add('cad_zero_after_static_verification', cadAfter.length === 0, cadAfter)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_right_760_native_static.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  purpose: 'structure_engineering_assistance',
  cadStarted: false,
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  sourceNormalizedSha256,
  sourceSha256: sha256(Buffer.from(source, 'utf8')),
  executableSha256: sha256(readFileSync(executablePath)),
  checks,
}

writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`)
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
