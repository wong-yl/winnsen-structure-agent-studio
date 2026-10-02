import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const file = (name) => resolve(here, name)
const sourcePath = file('TopCoverRightReferenceBrep.cs')
const executablePath = file('TopCoverRightReferenceBrep.exe')
const manifestPath = file('toolchain_manifest.json')
const outputPath = file('static_verification.json')
const source = readFileSync(sourcePath, 'utf8')
const build = readFileSync(file('Build-TopCoverRightReferenceBrep.ps1'), 'utf8')
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
const checks = []

const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const add = (name, pass, actual = undefined) => checks.push({
  name,
  pass: Boolean(pass),
  ...(actual === undefined ? {} : { actual }),
})
function cad() {
  const value = execFileSync('powershell.exe', ['-NoProfile', '-Command',
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | " +
    "Sort-Object ProcessName,Id | ForEach-Object { \"$($_.ProcessName):$($_.Id)\" }) -join ','"],
  { encoding: 'utf8', windowsHide: true }).trim()
  return value === '' ? [] : value.split(',')
}
function normalizedSourceHash(value) {
  const normalized = value.replace(/\r\n?/g, '\n')
  const pattern = /(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/g
  assert.equal([...normalized.matchAll(pattern)].length, 1)
  return hash(Buffer.from(normalized.replace(pattern, '$1__SOURCE_SHA256__$2'), 'utf8'))
}

const before = cad()
add('cad_zero_before_static_verification', before.length === 0, before)
const required = [
  'TopCoverRightReferenceBrep.cs', 'TopCoverRightReferenceBrep.exe',
  'TopCoverRightReferenceBrep.exe.config', 'Build-TopCoverRightReferenceBrep.ps1',
  'Verify-TopCoverRightReferenceBrep.ps1', 'README.md', 'toolchain_manifest.json',
  'SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll',
]
add('required_files_are_regular_single_link', required.every((name) => {
  if (!existsSync(file(name))) return false
  const info = statSync(file(name))
  return info.isFile() && Number(info.nlink) === 1
}))

const sourceNormalizedSha256 = normalizedSourceHash(source)
const embedded = source.match(/ExpectedSourceSha256\s*=\s*"([A-F0-9]{64})";/)?.[1]
add('source_normalized_identity', embedded === sourceNormalizedSha256)
add('manifest_binds_live_tool_and_runtime_dependencies',
  manifest?.schema === 'winnsen.16029.native_toolchain_manifest.v1' &&
  manifest?.tool?.id === 'topcover_reference_brep_v1' &&
  manifest?.tool?.mode === 'left-side-760-and-1000-sharp-flat-detailed-v7' &&
  manifest?.tool?.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  manifest?.tool?.executableSha256 === fileHash(executablePath) &&
  manifest?.tool?.runtimeDependencies?.length === 2 &&
  manifest.tool.runtimeDependencies[0]?.sha256 ===
    'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC' &&
  manifest.tool.runtimeDependencies[1]?.sha256 ===
    '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359')

add('fixed_mode_attempt_shape_and_exact_inputs', [
  'left-side-760-and-1000-sharp-flat-detailed-v7',
  '^TOPCOVER-REFERENCE-[A-Z0-9-]{8,120}$',
  'CAPTURE_INPUTS_NOT_EXACT_TWO_FLAT_FILES',
  'v37_left_side.sldprt', 'gold_left_side.sldprt',
].every((token) => source.includes(token)))
add('left_sharp_flat_detailed_v7_evidence_name_is_fixed', source.includes('topcover_left_reference_brep_capture_v7.json') &&
  source.includes('TOPCOVER_LEFT_REFERENCE_BREP_CAPTURE_V7_COMPLETE'))
add('input_and_output_file_identity_gates', [
  'INPUT_SINGLE_HARDLINK_OR_REPARSE_INVALID', 'INPUT_SHA256_MISMATCH',
  'AssertNoReparseChain', 'FileMode.CreateNew', 'Flush(true)',
  'LinkCount(path) == 1', '!HasReparse(path)',
].every((token) => source.includes(token)))

add('read_only_only_and_no_cad_mutation_route', [
  'OpenDoc6', 'swOpenDocOptions_ReadOnly', 'IsOpenedReadOnly()',
  'ForceRebuild3(false)',
].every((token) => source.includes(token)) &&
  !/(?:\.Save\w*\s*\(|PackAndGo|BreakAll|FreeCAD|FCStd|ImportDoc|LoadFile)/i.test(source))

add('full_brep_mass_and_circle_capture', [
  'GetBodies2', 'GetBodyBox', 'GetMassProperties(7850.0)', 'GetFaceCount',
  'GetEdgeCount', 'GetFaces', 'GetLoops', 'GetEdges', 'IsCylinder',
  'CylinderParams', 'IsCircle', 'CircleParams', 'loopEdgeReferenceCount',
  'geometrySignature',
].every((token) => source.includes(token)))
add('detailed_faces_loops_edges_and_finite_contract', [
  'List<DetailedFace> faces', 'DetailedFace', 'DetailedLoop', 'DetailedEdge',
  'face.GetArea()', 'face.GetBox()', 'face.Normal', 'surface.Identity()',
  'surface.PlaneParams', 'loop.IsOuter()', 'curve.GetEndParams',
  'curve.GetLength3', 'EdgePoint(curve, min)', 'VertexPoint(edge.GetStartVertex()',
  'REFERENCE_DETAILED_FACE_COUNT_INVALID', 'REFERENCE_DETAILED_LOOP_EDGE_COUNT_MISMATCH',
  'DETAILED_EDGE_NONFINITE', 'ToListOrEmpty',
].every((token) => source.includes(token)))
add('translation_normalized_detailed_signature', [
  'translationNormalizedDetailedGeometrySignature', 'DetailedGeometrySignature',
  'DetailedCylinderKey', 'CanonicalAxis', 'PointKey', 'perpendicular',
  'OrderBy(value => value, StringComparer.Ordinal)',
].every((token) => source.includes(token)))
add('flatpattern_unsuppress_restore_and_conservation', [
  'FindUniqueFlatPattern', 'CollectFlatPatterns', '"FlatPattern"', 'IsSuppressed()',
  'SetSuppression2(', 'swUnSuppressFeature', 'swSuppressFeature',
  'swThisConfiguration', 'flatPatternOriginallySuppressed', 'unsuppressReturn',
  'flatPatternUnsuppressedAfterRebuild', 'restoreSuppressReturn',
  'flatPatternSuppressedAfterRestore', 'restoreLightweightSignature',
  'restoreDetailedSignature', 'BboxSignificantlyDifferent', 'SameMassAndVolume',
  'FLAT_PATTERN_RESTORE_SIGNATURE_MISMATCH',
].every((token) => source.includes(token)) && !source.includes('SetBendState'))
add('sheetmetal_selection_rollback_captures_sharp_body_and_restores_folded', [
  'FindUniqueRootFeature(doc, "SheetMetal")', 'ISheetMetalFeatureData',
  'IAccessSelections2(doc, null)', 'InspectGeometryOnly(doc)',
  'sharpRollback', 'sharpRollbackCaptured', 'ReleaseSelectionAccess()',
  'sheetMetalSelectionReleased', 'sharpRollbackRestoreRebuild',
  'sharpRollbackRestoredDetailedSignature',
  'SHARP_ROLLBACK_RESTORE_SIGNATURE_MISMATCH',
  'REFERENCE_760_1000_SHARP_FLAT_SIGNATURE_OR_DELTA_MISMATCH',
].every((token) => source.includes(token)))
add('manufacturing_flat_deltas_and_boundary', [
  'massConserved', 'volumeConserved', 'foldedMinusFlatVolumeMm3',
  'foldedMinusFlatMassKg', 'foldedMinusFlatSurfaceAreaMm2', 'DeltasMatch',
  'REFERENCE_760_1000_SHARP_FLAT_SIGNATURE_OR_DELTA_MISMATCH',
  'manufacturing flat body',
].every((token) => source.includes(token)))
add('no_save_or_import_in_v7_source', !/(?:\.Save\w*\s*\(|PackAndGo|BreakAll|FreeCAD|FCStd|ImportDoc|LoadFile)/i.test(source))
add('recursive_sheet_metal_feature_capture', [
  'CaptureFeatureRecursive', 'GetFirstSubFeature', 'GetNextSubFeature',
  'IOneBendFeatureData', 'IBendsFeatureData', 'ISheetMetalFeatureData',
  'GetCustomBendAllowance', 'featureIssues', 'OneBend"] == 5',
].every((token) => source.includes(token)))
add('left_reference_external_count_is_two_not_zero',
  source.includes('REFERENCE_LEFT_EXTERNAL_REFERENCE_COUNT_NOT_TWO') &&
  source.includes('s.externalReferences.Count == 2') &&
  !source.includes('s.externalReferences.Count == 0'))

add('owned_process_identity_and_exact_stop_only', [
  'Activator.CreateInstance', 'GetProcessID()', 'ExpectedRevisionPrefix',
  'startTicks', 'executableSha256', '--ppid=', 'parentPid',
  'KillExact', 'identity.StillSame()', 'forceStoppedProcessIds',
].every((token) => source.includes(token)) &&
  !/Process\.Start\s*\(/.test(source) && !/Marshal\.GetActiveObject/.test(source))
add('stable_zero_uses_milliseconds_not_observation_count',
  source.includes('TotalMilliseconds >= quietWindowMs') &&
  !source.includes('++n >= observations'))
add('pinned_solidworks_and_monitor_hashes', [
  '1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978',
  'A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70',
].every((token) => source.includes(token)))
add('build_packages_pinned_local_interops',
  build.includes('Copy-Item') && build.includes('Local interop hash mismatch') &&
  build.includes('runtimeDependencies') &&
  fileHash(file('SolidWorks.Interop.sldworks.dll')) ===
    'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC' &&
  fileHash(file('SolidWorks.Interop.swconst.dll')) ===
    '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359')

const reflection = execFileSync('powershell.exe', ['-NoProfile', '-Command',
  "$a=[Reflection.Assembly]::LoadFrom('D:\\soildworks2020\\SOLIDWORKS\\api\\redist\\SolidWorks.Interop.sldworks.dll');" +
  "$s=$a.GetType('SolidWorks.Interop.sldworks.ISldWorks');" +
  "$o=$a.GetType('SolidWorks.Interop.sldworks.IOneBendFeatureData');" +
  "$m=$a.GetType('SolidWorks.Interop.sldworks.ISheetMetalFeatureData');" +
  "[bool]($s.GetMethod('OpenDoc6') -and $o.GetProperty('BendAngle') -and " +
  "$o.GetProperty('BendDirection') -and $o.GetProperty('BendOrder') -and " +
  "$m.GetMethod('IAccessSelections2') -and $m.GetMethod('ReleaseSelectionAccess'))"],
{ encoding: 'utf8', windowsHide: true }).trim()
add('interop_signatures_present', reflection === 'True')

const self = JSON.parse(execFileSync(executablePath, ['--self-test'],
  { cwd: here, encoding: 'utf8', windowsHide: true }))
add('self_test_passes_without_cad', self.status === 'PASS' &&
  self.cadStarted === false && self.checksFailed === 0)
const invalid = spawnSync(executablePath, [],
  { cwd: here, encoding: 'utf8', windowsHide: true })
add('invalid_invocation_fails_before_cad', invalid.status === 64 &&
  /ARGUMENT_GRAMMAR_INVALID/.test(invalid.stderr ?? ''))
const after = cad()
add('cad_zero_after_static_verification', after.length === 0, after)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_reference_brep_static.v7',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  cadStarted: false,
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  sourceNormalizedSha256,
  sourceSha256: fileHash(sourcePath),
  executableSha256: fileHash(executablePath),
  checks,
}
writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`)
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
