import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const sourcePath = resolve(here, 'TopCoverPartsNative.cs')
const executablePath = resolve(here, 'TopCoverPartsNative.exe')
const manifestPath = resolve(here, 'toolchain_manifest.json')
const buildPath = resolve(here, 'Build-TopCoverPartsNative.ps1')
const configPath = resolve(here, 'TopCoverPartsNative.exe.config')
const readmePath = resolve(here, 'README.md')
const outputPath = resolve(here, 'static_verification.json')
const sldworksInteropPath = resolve(here, 'SolidWorks.Interop.sldworks.dll')
const swconstInteropPath = resolve(here, 'SolidWorks.Interop.swconst.dll')
const rulePath = resolve(here,
  '../../../../../../data/native_model_requests/attempts/' +
  'NATIVE-20260824T101117-888W14D-3E50/attempt-0001/evidence/private/' +
  'topcover_front_native_rule_v2.json')

const source = readFileSync(sourcePath, 'utf8')
const build = readFileSync(buildPath, 'utf8')
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
const checks = []

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function add(name, pass, details = undefined) {
  checks.push({ name, pass: Boolean(pass), ...(details === undefined ? {} : { details }) })
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
  return sha256(Buffer.from(normalizedNewlines.replace(pattern, '$1__SOURCE_SHA256__$2'), 'utf8'))
}

const cadBefore = cadSnapshot()
add('cad_zero_before_static_verification', cadBefore.length === 0, cadBefore)

const requiredFiles = [sourcePath, executablePath, manifestPath, buildPath, configPath,
  readmePath, rulePath, sldworksInteropPath, swconstInteropPath]
add('required_files_are_regular_single_link_files', requiredFiles.every((path) => {
  if (!existsSync(path)) return false
  const info = statSync(path)
  return info.isFile() && Number(info.nlink) === 1
}))

const sourceNormalizedSha256 = normalizedSourceSha256(source)
const embeddedSourceSha256 = source.match(
  /ExpectedSourceSha256\s*=\s*"([A-F0-9]{64})";/)?.[1]
add('source_normalized_identity_is_exact', embeddedSourceSha256 === sourceNormalizedSha256)
add('manifest_binds_live_source_and_executable',
  manifest?.schema === 'winnsen.16029.native_toolchain_manifest.v1' &&
  manifest?.tool?.id === 'topcover_parts_native_v1' &&
  manifest?.tool?.phase === 'front_760_native_pilot_phase2' &&
  manifest?.tool?.cadImplemented === true &&
  manifest?.tool?.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  manifest?.tool?.executableSha256 === sha256(readFileSync(executablePath)) &&
  manifest?.tool?.verifierSha256 === sha256(readFileSync(fileURLToPath(import.meta.url))) &&
  manifest?.tool?.runtimeDependencies?.length === 2 &&
  manifest.tool.runtimeDependencies[0]?.sha256 ===
    'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC' &&
  manifest.tool.runtimeDependencies[1]?.sha256 ===
    '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359')

add('fixed_rule_hash_is_live',
  sha256(readFileSync(rulePath)) ===
  '15CDAA0B19189F3D6E41D7A277F15AA1A0CEF58CA304B6BE5E6B207F43B14553')

const forbidden = /(SeedPack|FreeCAD|FCStd|PackAndGo|BreakAll|ConvertToSheetMetal|InsertPart|DerivedPart|BaseBody|MoveBody|CreateFeatureFromBody|ImportDoc|LoadFile)/i
add('legacy_and_import_routes_are_absent', !forbidden.test(source))
add('one_native_save_callsite_only', (source.match(/\.SaveAs3\(/g) ?? []).length === 1)
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
].every((token) => source.includes(token)))

add('evidence_and_receipt_use_createnew_reread_pair', [
  'FileMode.CreateNew', 'stream.Flush(true)', 'WriteCreateNewAndReread',
  'PilotEvidenceReceiptPairValid', 'ReceiptSchema', 'evidenceSha256',
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

add('native_sheet_metal_build_is_rule_driven', [
  'SelectDefaultFrontPlane', 'plane.Transform', 'normalZ',
  'RefPlaneTransform', 'swRefPlaneReferenceConstraint_OptionFlip',
  'OFFSET_PLANE_TRANSFORM_INVALID',
  'baseSketchPlaneZMm = contract.flatPlaneZMm + 0.8',
  'SetAddToDB(true)', 'SetDisplayWhenAdded(false)',
  'ToActiveSketchPoint', 'InsertSheetMetalBaseFlange2',
  'swBendAllowanceKFactor', 'swSheetMetalReliefTear',
  'FeatureCut3(true, false, false', 'swEndCondThroughAll',
  'swStartConditions_e.swStartSketchPlane',
  'LastRootFeatureOfType(model, "ProfileFeature")',
  'CreateNativeBend', 'InsertSheetMetal3dBend',
  '(short)0, null',
  'BendLineSourceIndexForOrder', 'if (order == 1) return 1',
  'if (order == 2) return 0',
].every((token) => source.includes(token)))
add('bend_position_never_uses_invalid_flange_enum',
  !source.includes('swFlangePositionTypeBendTangent') &&
  !source.includes('swFlangePositionTypeBendCenterLine'))

add('fresh_reopen_checks_full_geometry_and_features', [
  'swOpenDocOptions_ReadOnly', 'CaptureGeometry(model)',
  'CaptureFeatureSnapshot(model)', 'GeometryMatchesContract',
  'FeatureMatchesContract', 'ListExternalFileReferencesCount() == 0',
  'fileUnchanged',
].every((token) => source.includes(token)))

add('brep_mass_bbox_and_bend_cylinders_are_exactly_gated', [
  'GetBodyBox()', 'GetMassProperties(7850.0)', 'GetFaceCount()',
  'GetEdgeCount()', 'GetLoops()', 'GetCurve()', 'IsCylinder()', 'IsCircle()',
  '0.05, 0.2, 0.85, 1.0', 'expectedLoopEdgeReferenceCount',
  'expectedCircleEdgeReferenceCount',
].every((token) => source.includes(token)))

add('feature_health_and_two_bend_definitions_are_gated', [
  'GetErrorCode2(out warning)', 'IOneBendFeatureData', 'oneBendCount != 2',
  'first.direction == 1', 'second.direction == 2',
  'first.bendType == 2', 'second.bendType == 2',
  'first.useDefaultRadius', 'second.useDefaultRadius',
].every((token) => source.includes(token)))

add('build_references_exact_interop_and_system_management',
  build.includes('/reference:System.Management.dll') &&
  build.includes('SolidWorks.Interop.sldworks.dll') &&
  build.includes('SolidWorks.Interop.swconst.dll') &&
  build.includes('Pinned interop source hash mismatch') &&
  build.includes('Existing local interop hash mismatch') &&
  build.includes('Build requires zero CAD processes'))

const reflection = execFileSync('powershell.exe', ['-NoProfile', '-Command',
  "$a=[Reflection.Assembly]::LoadFrom('D:\\soildworks2020\\SOLIDWORKS\\api\\redist\\SolidWorks.Interop.sldworks.dll');" +
  "$f=$a.GetType('SolidWorks.Interop.sldworks.IFeatureManager');" +
  "$o=$a.GetType('SolidWorks.Interop.sldworks.IOneBendFeatureData');" +
  "[bool]($f.GetMethod('InsertSheetMetalBaseFlange2') -and " +
  "$f.GetMethod('InsertSheetMetal3dBend') -and $f.GetMethod('FeatureCut3') -and " +
  "$o.GetProperty('BendAngle') -and $o.GetProperty('BendDirection') -and " +
  "$o.GetProperty('BendOrder') -and $o.GetProperty('BendRadius'))"],
{ encoding: 'utf8', windowsHide: true }).trim()
add('solidworks_2020_interop_signatures_are_present', reflection === 'True')

const selfTest = JSON.parse(execFileSync(executablePath, ['--self-test'],
  { cwd: here, encoding: 'utf8', windowsHide: true }))
add('self_test_passes_without_cad', selfTest.status === 'PASS' &&
  selfTest.cadStarted === false && selfTest.checksTotal >= 16 &&
  selfTest.checksFailed === 0)

const identity = JSON.parse(execFileSync(executablePath, ['--identity'],
  { cwd: here, encoding: 'utf8', windowsHide: true }))
add('identity_matches_live_source', identity.toolId === 'topcover_parts_native_v1' &&
  identity.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  identity.cadImplemented === true && identity.ordinaryExitCode === 0)

const invalidInvocation = spawnSync(executablePath, [],
  { cwd: here, encoding: 'utf8', windowsHide: true })
add('invalid_invocation_fails_before_cad', invalidInvocation.status === 64 &&
  /ARGUMENT_GRAMMAR_INVALID/.test(invalidInvocation.stderr ?? ''))

const cadAfter = cadSnapshot()
add('cad_zero_after_static_verification', cadAfter.length === 0, cadAfter)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_front_native_static.v3',
  status: passed === checks.length ? 'PASS' : 'FAIL',
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
