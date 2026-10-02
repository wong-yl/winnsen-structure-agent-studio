import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { lstatSync, readdirSync, readFileSync, realpathSync, statSync } from 'node:fs'
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS,
  NATIVE_SEED_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  NATIVE_TOOLCHAIN_MANIFEST_PATHS,
  nativeEvidenceCommitmentSha256,
} from '../../../../../../tools/lib/locker_16029_native_stage_contract.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '../../../../../..')
const sourcePath = resolve(here, 'SeedPack888Native.cs')
const executablePath = resolve(here, 'SeedPack888Native.exe')
const verifierPath = resolve(here, 'Verify-SeedPack888Native.ps1')
const manifestPath = resolve(here, 'toolchain_manifest.json')
const lockManifestPath = resolve(repoRoot,
  'workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json')
const source = readFileSync(sourcePath, 'utf8')
const v37SourcePath = resolve(repoRoot,
  'workers/generated_models/review_generation_requests/' +
  'v43-int-v37-760w-six-door-l642-r246-r1/native_cad/' +
  'final_native_hierarchical_pack_and_go')

const sha256 = (bytes) => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const sha256File = (path) => sha256(readFileSync(path))
const sourceStampPattern = /(private\s+const\s+string\s+ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})("\s*;)/
const normalizedSourceSha256 = (text) => {
  const normalizedLines = text.replace(/\r\n?/g, '\n')
  assert.equal(normalizedLines.match(new RegExp(sourceStampPattern.source, 'g'))?.length, 1,
    'source must contain exactly one recognized ExpectedSourceSha256 stamp')
  return sha256(Buffer.from(normalizedLines.replace(sourceStampPattern,
    '$1__SOURCE_SHA256__$2'), 'utf8'))
}
const pathChainIsSafe = (path) => {
  if (!isAbsolute(path)) return false
  const rel = relative(repoRoot, path)
  if (!rel || rel.startsWith('..') || isAbsolute(rel)) return false
  let current = repoRoot
  for (const segment of rel.split(sep)) {
    current = resolve(current, segment)
    if (lstatSync(current).isSymbolicLink()) return false
  }
  return statSync(path).isFile() && Number(statSync(path).nlink) === 1 &&
    !relative(realpathSync.native(repoRoot), realpathSync.native(path)).startsWith('..')
}
const stableValue = (value) => {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}
const processSnapshot = () => {
  const script = "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Sort-Object ProcessName,Id | ForEach-Object { $_.ProcessName + ':' + $_.Id + ':' + $_.StartTime.ToUniversalTime().Ticks }) -join ','"
  const output = execFileSync('powershell.exe', ['-NoProfile', '-Command', script], {
    encoding: 'utf8', windowsHide: true,
  }).trim()
  return output ? output.split(',') : []
}

const beforeProcesses = processSnapshot()
assert.deepEqual(beforeProcesses, [], 'static verification requires zero CAD processes')

const ordinalIgnoreCaseCompare = (left, right) => {
  const upperLeft = left.toUpperCase()
  const upperRight = right.toUpperCase()
  return upperLeft < upperRight ? -1 : upperLeft > upperRight ? 1 :
    left < right ? -1 : left > right ? 1 : 0
}
const v37Entries = readdirSync(v37SourcePath, { withFileTypes: true })
assert.equal(v37Entries.every((entry) => entry.isFile() && /\.sld(?:asm|prt)$/i.test(entry.name)),
  true, 'trusted V37 seed must remain a flat CAD-only directory')
const v37Names = v37Entries.map((entry) => entry.name).sort(ordinalIgnoreCaseCompare)
assert.equal(v37Names.length, 75)
assert.equal(v37Names.filter((name) => /\.sldasm$/i.test(name)).length, 22)
assert.equal(v37Names.filter((name) => /\.sldprt$/i.test(name)).length, 53)
const v37InventoryText = v37Names.map((name) =>
  `${name}|${sha256File(resolve(v37SourcePath, name))}`).join('\n') + '\n'
assert.equal(sha256(Buffer.from(v37InventoryText, 'utf8')),
  '9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B')
assert.equal(sha256File(resolve(v37SourcePath,
  '标准寄存柜1917×760×550(总装配).SLDASM')),
  '5AED314E89A65A3875C517B2F4DC179635E9C684E0CD1877AFEBD33CAA77CA5B')

const embeddedSourceSha256 = /private const string ExpectedSourceSha256 = "([A-F0-9]{64})";/
  .exec(source)?.[1]
assert.equal(embeddedSourceSha256, normalizedSourceSha256(source),
  'runtime source identity must embed the normalized source SHA-256, not a placeholder')
for (const fragment of ['NormalizeNonFiniteJsonNumbers(serializer.Serialize(value))',
  'JsonNonFiniteTokenAt(', 'JsonTokenBoundaryBefore(', 'JsonTokenBoundaryAfter(',
  'output.Append("null")']) {
  assert.equal(source.includes(fragment), true,
    `standard JSON nonfinite-number normalization is missing: ${fragment}`)
}

for (const pattern of [
  /PackAndGoAssembly/,
  /SldWorks\.Application(?!\.28)/,
  /ExpectedSourceInventoryDigest\s*=\s*"000D1C0B/,
  /release_ready|production_release|生产可用/i,
]) {
  assert.equal(pattern.test(source), false, `legacy seed behavior remains: ${pattern}`)
}
for (const pattern of [
  /TwoHopRelocationProbeOnly/,
  /disposable_staging/,
  /GetPackAndGo\(\)/,
  /SavePackAndGo\(/,
  /private_probe_only/,
  /topcover_pack/,
]) {
  assert.equal(pattern.test(source), false, `temporary probe behavior remains: ${pattern}`)
}
for (const pattern of [
  /GetPackAndGo\(/,
  /SavePackAndGo\(/,
  /ReplaceReferencedDocument/,
  /ReplaceComponents/,
]) {
  assert.equal(pattern.test(source), false,
    `native top-cover probe contains a forbidden reuse API: ${pattern}`)
}
assert.equal(source.includes(
  'private static readonly bool NativeTopCoverPartMeasurementProbeOnly = false'), true,
  'read-only native top-cover part measurement must remain retained but dormant')
assert.equal(source.includes(
  'private static readonly bool NativeTopCoverPartDetachProbeOnly = false'), true,
  'physical-part detach must remain dormant after its bounded failure')
assert.equal(source.includes(
  'private static readonly bool NativeTopCoverRebuildProbeOnly = false'), true,
  'assembly rebuild probe must remain dormant while part evidence is incomplete')
assert.equal(source.includes(
  'private static readonly bool NativeTopCoverRebuildCaptureProbeOnly = false'), true,
  'full physical-part capture must remain dormant after its complete evidence bundle')
assert.equal(source.includes(
  'private static readonly bool NativeTopCoverMasterReferenceCaptureProbeOnly = false'), true,
  'master-reference capture must remain retained but dormant')
assert.equal(source.includes(
  'private static readonly bool NativeTopCoverFrontBrepCaptureProbeOnly = false'), true,
  'front B-rep capture must remain dormant in the registered seed stage')
for (const fragment of [
  'System.Web.Script.Serialization.ScriptIgnore',
  'public long start_utc_ticks_value',
  'public string start_utc_ticks',
  'start_utc_ticks_value.ToString(CultureInfo.InvariantCulture)',
  'checks["process_identity_ticks_are_exact_json_strings"]',
]) assert.equal(source.includes(fragment), true,
  `exact process tick string contract is missing: ${fragment}`)
assert.equal(source.includes('public long start_utc_ticks;'), false,
  'unsafe numeric process ticks must not be serialized into evidence')
assert.equal(source.includes('TOPCOVER_NATIVE_REBUILD_CAPTURE_PROBE_COMPLETE", 67'), true,
  'capture probe must stop before shared evidence/receipt with exit 67')
assert.equal(source.includes('TOPCOVER_NATIVE_MASTER_REFERENCE_CAPTURE_COMPLETE", 66'), true,
  'master-reference capture must stop before shared evidence/receipt with exit 66')
for (const fragment of [
  'evidence/private/topcover_native_rebuild_capture_probe_only.json',
  'evidence/private/topcover_native_rebuild_capture_probe_cleanup.json',
  'ExpectedGoldTopCoverSha256', 'ExpectedGoldTopCoverDxfSha256',
  'CaptureNativeTopCoverRebuildPart', 'CaptureNativeTopCoverOneBend',
  'CaptureNativeTopCoverProcessBends', 'CaptureNativeTopCoverFlatPattern',
  'CaptureNativeTopCoverRebuildAssembly', 'masterExcludedFromNewAssembly',
]) assert.equal(source.includes(fragment), true, `capture contract is missing: ${fragment}`)
for (const fragment of [
  'evidence/private/topcover_native_master_reference_capture_probe_only.json',
  'evidence/private/topcover_native_master_reference_capture_probe_cleanup.json',
  'RunNativeTopCoverMasterReferenceCaptureProbeOnly(result)',
  'NativeTopCoverMasterReferenceCaptureComplete(',
  'NativeTopCoverMasterReferenceDatasetComplete(',
  'WriteNativeTopCoverMasterReferenceCapturePrivateEvidence(',
  'WriteNativeTopCoverMasterReferenceCaptureProbeCleanupEvidence(',
]) assert.equal(source.includes(fragment), true,
  `master-reference capture contract is missing: ${fragment}`)
const masterCaptureStart = source.indexOf(
  'private static void RunNativeTopCoverMasterReferenceCaptureProbeOnly(Result result)')
const masterCaptureEnd = source.indexOf(
  '// First reconstruction phase:', masterCaptureStart)
assert.equal(masterCaptureStart >= 0 && masterCaptureEnd > masterCaptureStart, true,
  'master-reference capture must remain a separate bounded branch')
const masterCaptureSource = source.slice(masterCaptureStart, masterCaptureEnd)
for (const forbidden of ['.Save3(', '.SaveAs(', 'BreakAllExternal',
  'UpdateExternalFileReferences', '.NewPart(', '.NewAssembly(', '.AddComponent']) {
  assert.equal(masterCaptureSource.includes(forbidden), false,
    `master-reference capture contains a forbidden CAD mutation: ${forbidden}`)
}
const frontBrepStart = source.indexOf(
  'private static void RunNativeTopCoverFrontBrepCaptureProbeOnly(Result result)')
const frontBrepEnd = source.indexOf(
  '// First reconstruction phase:', frontBrepStart)
assert.equal(frontBrepStart >= 0 && frontBrepEnd > frontBrepStart, true,
  'front B-rep capture must remain a separate bounded branch')
const frontBrepSource = source.slice(frontBrepStart, frontBrepEnd)
for (const fragment of [
  'TOPCOVER_NATIVE_FRONT_BREP_CAPTURE_COMPLETE", 65',
  'evidence/private/topcover_native_front_brep_capture_probe_only.json',
  'evidence/private/topcover_native_front_brep_capture_probe_cleanup.json',
  'BindGoldTopCoverInputs(result, probe)',
  'CreateGoldTopCoverCapturePack(result, probe)',
  'CaptureNativeTopCoverFrontBrepPart(result,',
  'application.SetCurrentWorkingDirectory(expectedDirectory)',
  'output.preopenDependencies = CaptureDependencies(',
  'output.preopenDependencies.all_target_local',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly',
  'body.GetFaces()', 'body.GetEdgeCount()', 'face.GetArea()', 'face.GetBox()',
  'face.Normal', 'face.GetLoopCount()',
  'GetSurface', 'Identity', 'IsPlane', 'IsCylinder', 'PlaneParams', 'CylinderParams',
  'GetLoops', 'loop.IsOuter()', 'loop.GetEdgeCount()', 'GetEdges',
  'GetCurveParams3', 'CurveType',
  'CurveTag', 'Sense', 'UMinValue', 'UMaxValue', 'StartPoint', 'EndPoint',
  'GetLength3', 'IsCircle', 'CircleParams', 'IsLine', 'LineParams',
  'GetStartVertex', 'GetEndVertex', 'GetPoint', 'NativeTopCoverFrontBrepGeometrySignature',
  'ToString("R", CultureInfo.InvariantCulture)', 'expectedFaceCount = expectedFaces',
  'expectedEdgeCount = expectedEdges', 'row.faceCount == row.expectedFaceCount',
  'row.edgeCount == row.expectedEdgeCount', 'row.loopEdgeReferenceCount',
  'row.faces.Any(value => value.isCylinder)',
  'value => value.isCircle', 'WriteCreateNewAndReread', 'StableRollbackCadGate(result)',
]) assert.equal(frontBrepSource.includes(fragment) || source.includes(fragment), true,
  `front B-rep capture contract is missing: ${fragment}`)
for (const forbidden of ['.Save3(', '.SaveAs(', 'BreakAllExternal',
  'UpdateExternalFileReferences', '.NewPart(', '.NewAssembly(', '.AddComponent',
  'ReleaseComArrayItems']) {
  assert.equal(frontBrepSource.includes(forbidden), false,
    `front B-rep capture contains a forbidden mutation or bulk COM release: ${forbidden}`)
}
const captureStart = source.indexOf(
  'private static void RunNativeTopCoverRebuildCaptureProbeOnly(Result result)')
const captureEnd = source.indexOf(
  'private static void RunNativeTopCoverRebuildProbeOnly(Result result)', captureStart)
assert.equal(captureStart >= 0 && captureEnd > captureStart, true,
  'capture probe must remain a separate bounded branch before the old rebuild probe')
const captureSource = source.slice(captureStart, captureEnd)
for (const forbidden of ['.Save3(', '.SaveAs(', 'BreakAllExternal',
  'UpdateExternalFileReferences', '.NewPart(', '.NewAssembly(', '.AddComponent']) {
  assert.equal(captureSource.includes(forbidden), false,
    `capture branch contains a forbidden CAD mutation: ${forbidden}`)
}
for (const fragment of [
  'CreateGoldTopCoverCapturePack(result, probe)',
  'goldCapturePack = Path.Combine(result.attempt_directory',
  'GoldTopCoverCaptureCadFileNames',
  'File.Copy(source, target, false)',
  'WorkingPackExact75WithPairedLocksUnchanged(result)',
  'MathUtility math', 'modelToSketch.Inverse()', 'MultiplyTransform(sketchToModel)',
  'application.SetCurrentWorkingDirectory(expectedDirectory)',
  'output.preopenDependencies = CaptureDependencies(',
  'output.preopenDependencies.all_target_local',
  'goldSourceLockFilesBefore', 'goldSourceLockFilesAfter',
  'goldSourceLockStateUnchanged',
  'KnownGoldBottomHistoricalAssemblyReference',
  'KnownGoldBottomDependencyOnly(output, expectedDirectory)',
  'knownGoldBottomHistoricalDependencyAccepted',
  'row.supplementRequired = !row.accessSelections',
  'row.emptyInternal = row.segments.Count == 0',
  'segment.GetRelationsCount()', 'arc.GetRotationDir()', 'arc.GetNormalVector()',
  'instance.BlockToSketchTransform', 'data.GetFlatPatternSketchSegmentCount2()',
  'data.GetFixedFace() as Face2', 'data.FixedFace2 as Face2',
  'NativeTopCoverPartRowMeasurementComplete(row.measurement)',
  'row.oneBends.Count == expectedBends', 'row.flatPatterns.Count == 1',
  'row.master.hidden &&', 'row.master.envelope &&',
  'row.master.excludeFromBom &&', 'SameTransform16(value.transform16, identity)',
  'RollbackNativeTopCoverCapturePack(result)',
  'goldCapturePackRollbackSucceeded',
]) assert.equal(captureSource.includes(fragment) || source.includes(fragment), true,
  `capture fail-closed contract is missing: ${fragment}`)
for (const forbidden of ['GetRelationCount"', 'GetRotation"',
  'ComRead(arc, "Normal")', 'Release(line)', 'Release(arc)',
  'CaptureNativeTopCoverRebuildPart(result,\n                        Path.Combine(probe.goldEngineeringDirectory']) {
  assert.equal(captureSource.includes(forbidden), false,
    `capture branch retains a stale or unsafe API pattern: ${forbidden}`)
}
for (const [name, hash] of [
  ['标准寄存柜 模型.SLDPRT', '2D00EBDDA5CE93A83358ADB9E910AA3E023A228AA5E5233894D461EC42223628'],
  ['上盖 模型.sldprt', '4E9AAF6BD6422D7330B5CE33BDCBFF8651E1AA5CD68D84EE90DE9B20FD770625'],
  ['上盖焊接.SLDASM', 'FAA607FB615F186EA7B772771B6ACA38BFD22D947CAC4DE6F56D5AFC7FDCC615'],
  ['上盖焊接.SLDDRW', '069047FB00749382669304832402F74A6E24B769448E454FAC98EF3BEB8C8C45'],
  ['上盖壳体左侧板.sldprt', '2DD83A0A1C9FE880B460177F5CD8191B2324738A1E141C652F024DC077A3CBCE'],
  ['上盖壳体左侧板.SLDDRW', '05F6204627D6CEFEABCACD66EDAA6FE7EA6F666F43BCA4096CB5AA1009D1B941'],
  ['上盖壳体右侧板.SLDPRT', '380F2D8DC419F8BDEC809319FC1AB14F0AD279038E50362AA8E1C7E7BA70A679'],
  ['上盖壳体右侧板.SLDDRW', '9FBE33BCE7813DF6FCB14D569075416ABD48C61C12579C2D66091AA7F60EF0BA'],
  ['上盖壳体前侧板.sldprt', '3B7290BE78F0BE495E063C0392F7260339FFEC75CB890ED12E64CF07602FF354'],
  ['上盖壳体前侧板.SLDDRW', 'AEC21D458472D74B6AC0B6F86F12CA07593CA70552283919E004487FDA3A011B'],
  ['上盖壳体后侧板.sldprt', '834C35D04C265CB3C26E4469F93A346B37089ED42D4EA534E109CE6B768FB5F5'],
  ['上盖壳体后侧板.SLDDRW', '995649F7BC0268DB0B8787DF6D86D50E3C9D156FDE6622C16A995B96DFF74177'],
  ['上盖壳体底板.sldprt', '8972838A6F0B09DDEE0344C143A1B8941C9C9E6846DADD2470F2F3443718249C'],
  ['上盖壳体底板.SLDDRW', '593527C99FFF60515B9CA8399AE11F8C89C13507741BBE5F1A3C5DD01E20B1FB'],
  ['上盖壳体左侧板展开图.DXF', '8160ECC4E9051BD541929C1F4C73A4DCF5B747AB666A05111CA9A8705806287A'],
  ['上盖壳体右侧板展开图.DXF', 'DE8FF1A6A1C99E537310769BB04A684BAFAAA7E15C225B331E563F130EF41D26'],
  ['上盖壳体前侧板展开图.DXF', '1AC67CA04FD2F95AC32CB809E199220C9D269F3B72C705AE57542C1CA5041DD1'],
  ['上盖壳体后侧板展开图.DXF', '7D7415A4E157D96DCC265430D7C803354C7657C00554D1134890E958FE937459'],
  ['上盖壳体底板展开图.DXF', 'BAD01389D1EA57CCC75F555DA330F5C09DFCDB3F39C2E78E4B84F66111BD131D'],
]) assert.equal(source.includes(`{ "${name}", "${hash}" }`), true,
  `capture source must pin gold input ${name}`)
assert.equal(source.includes('TOPCOVER_NATIVE_PART_DETACH_COMPLETE", 68'), true,
  'bounded detach probe must force exit 68 before shared evidence/receipt')
assert.equal(source.includes(
  'evidence/private/topcover_native_part_measurement_probe_only.json'), true,
  'measurement probe must write only private evidence')
assert.equal(source.includes(
  'evidence/private/topcover_native_part_measurement_probe_cleanup.json'), true,
  'measurement probe must persist post-rollback cleanup proof')
assert.equal(source.includes(
  'evidence/private/topcover_native_part_detach_probe_only.json'), true,
  'detach probe must write only private evidence')
assert.equal(source.includes(
  'evidence/private/topcover_native_part_detach_probe_cleanup.json'), true,
  'detach probe must persist post-rollback cleanup proof')
assert.equal(source.includes('TOPCOVER_NATIVE_REBUILD_PROBE_COMPLETE", 70'), true,
  'dormant assembly rebuild probe must retain its nonconsumable stop')
assert.equal(source.includes('evidence/private/topcover_native_rebuild_probe_only.json'), true,
  'native top-cover probe must write private evidence only')
assert.equal(source.includes('evidence/private/topcover_native_rebuild_probe_cleanup.json'), true,
  'native top-cover probe must persist a separate post-rollback cleanup proof')
assert.equal(source.includes('new candidate assembly') ||
  source.includes('new empty assembly'), true,
  'native top-cover probe must explain that it creates a new assembly')
const save3Matches = source.match(/model\.Save3\(/g) ?? []
assert.equal(save3Matches.length, 3,
  'Save3 may appear only in formal stabilization, exact-33 ID acceptance, and detach helper')
for (const methodName of ['private static void StabilizeWritableRoot',
  'private static void AcceptExact33RootIdMismatch',
  'private static void DetachNativeTopCoverPhysicalPart']) {
  const start = source.indexOf(methodName)
  assert.ok(start >= 0, `missing permitted Save3 method: ${methodName}`)
  const end = source.indexOf('\n        private static ', start + methodName.length)
  assert.equal(source.slice(start, end < 0 ? source.length : end).includes('model.Save3('), true,
    `permitted Save3 method is missing its explicit bounded save: ${methodName}`)
}
const methodSlice = (startMarker, endMarker) => {
  const start = source.indexOf(startMarker)
  const end = source.indexOf(endMarker, start + startMarker.length)
  assert.equal(start >= 0 && end > start, true,
    `method slice markers are missing or reversed: ${startMarker}`)
  return source.slice(start, end)
}
for (const [startMarker, endMarker, workingDirectoryCall] of [
  ['private static void RunRelocationPrewriteDiagnostic(',
    'private static void StabilizeWritableRoot(',
    'SetCurrentWorkingDirectory(result.target_directory)'],
  ['private static void StabilizeWritableRoot(',
    'private static void VerifyFreshReadonly(',
    'SetCurrentWorkingDirectory(result.target_directory)'],
  ['private static void VerifyFreshReadonly(',
    'private static ISldWorks StartOwnedSolidWorks(',
    'SetCurrentWorkingDirectory(result.target_directory)'],
  ['CaptureTopCoverComponentFreshReadOnlyDiagnostics(Result result,',
    'private static bool FreshComponentDiagnosticComplete(',
    'SetCurrentWorkingDirectory(cadDirectory)'],
  ['CaptureTopCoverAssemblyLoadOrderDiagnostic(Result result,',
    'private static bool TopCoverLoadOrderDiagnosticComplete(',
    'SetCurrentWorkingDirectory(cadDirectory)'],
]) {
  assert.equal(methodSlice(startMarker, endMarker).includes(workingDirectoryCall), true,
    `formal relocated session is missing its local search-directory binding: ${startMarker}`)
}
const frontBrepSignatureSource = methodSlice(
  'private static string NativeTopCoverFrontBrepGeometrySignature(',
  'private static bool WriteNativeTopCoverFrontBrepCapturePrivateEvidence(')
for (const forbidden of ['face.index', 'loop.index', 'edge.order', 'edge.curveTag']) {
  assert.equal(frontBrepSignatureSource.includes(forbidden), false,
    `front B-rep geometry signature contains unstable enumeration data: ${forbidden}`)
}
for (const fragment of ['rows.OrderBy(item => item, StringComparer.Ordinal)',
  '"F|" + R(face.areaSi)', '"L|" + loop.isOuter',
  '"E|" + edge.curveType']) {
  assert.equal(frontBrepSignatureSource.includes(fragment), true,
    `front B-rep stable geometry signature is missing: ${fragment}`)
}
const measurementRunSource = methodSlice(
  'private static void RunNativeTopCoverPartMeasurementProbeOnly(Result result)',
  'private static NativeTopCoverPartMeasurement CaptureNativeTopCoverPartMeasurement(')
for (const fragment of ['mode = "measure_topcover_parts"',
  'exactSixSourceBindings', 'StartOwnedSolidWorks(result, session)',
  'CaptureNativeTopCoverPartMeasurement(application, path,',
  'CaptureNativeTopCoverTemplateComponentStates(',
  'NativeTopCoverPartMeasurementComplete(probe)',
  'WriteNativeTopCoverPartMeasurementPrivateEvidence(',
  'SourceExact75Unchanged(result)', 'WorkingPackExact75Unchanged(result)']) {
  assert.equal(measurementRunSource.includes(fragment), true,
    `top-cover part measurement gate is missing: ${fragment}`)
}
const measurementRegion = methodSlice(
  'private static void RunNativeTopCoverPartMeasurementProbeOnly(Result result)',
  'private static void RunNativeTopCoverPartDetachProbeOnly(Result result)')
for (const forbidden of ['.Save3(', '.SaveAs(', 'BreakAllExternal',
  'UpdateExternalFileReferences', 'ReplaceReferencedDocument', 'ReplaceComponents',
  '.NewPart(', '.NewAssembly(', '.AddComponent', 'repair_staging']) {
  assert.equal(measurementRegion.includes(forbidden), false,
    `read-only part measurement contains CAD mutation behavior: ${forbidden}`)
}
const partMeasurementSource = methodSlice(
  'private static NativeTopCoverPartMeasurement CaptureNativeTopCoverPartMeasurement(',
  'private static void CaptureNativeTopCoverPartGeometry(')
for (const fragment of ['DocumentCacheEmpty(application, out documentsBefore)',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly', 'model.IsOpenedReadOnly()',
  'saveFlagBeforeCaptured', 'model.ForceRebuild3(false)',
  'saveFlagAfterCaptured', 'CaptureNativeTopCoverPartGeometry(model, row)',
  'CaptureNativeTopCoverPartFeatures(model, row, cadDirectory)',
  'model.ListExternalFileReferencesCount2()',
  'extension.ListExternalFileReferencesCount()',
  'CloseDocument(application, ref model)', 'application.CloseAllDocuments(true)',
  'DocumentCacheEmpty(application, out documentsAfter)', 'row.fileUnchanged']) {
  assert.equal(partMeasurementSource.includes(fragment), true,
    `part measurement lifecycle is missing: ${fragment}`)
}
const geometryMeasurementSource = methodSlice(
  'private static void CaptureNativeTopCoverPartGeometry(',
  'private static bool MeasurementValuesFinite(')
for (const fragment of ['partBoxApiRaw', 'part.GetPartBox(false)', 'part.GetBodies2(',
  'body.GetBodyBox()', 'body.GetFaceCount()', 'body.GetEdges()',
  'body.GetMassProperties(7850.0)', 'massPropertiesSi[3]',
  'massPropertiesSi[4]', 'massPropertiesSi[5]', 'bodyRow.complete',
  'row.bodies.Min(value => value.boxMm[0])',
  'row.bodies.Max(value => value.boxMm[5])',
  'partBoxDerivedFromBodies', 'MeasurementValuesFinite(']) {
  assert.equal(geometryMeasurementSource.includes(fragment), true,
    `body geometry/mass measurement is missing: ${fragment}`)
}
assert.equal(geometryMeasurementSource.includes('ReleaseComArrayItems(bodies)'), false,
  'body RCWs must not be released twice')
const sheetMetalMeasurementSource = methodSlice(
  'private static NativeTopCoverSheetMetalMeasurement CaptureNativeTopCoverSheetMetalFeature(',
  'private static List<NativeTopCoverTemplateComponentState>')
for (const fragment of ['data.AccessSelections(null, null)', 'data.Thickness',
  'data.BendRadius', 'data.KFactor', 'data.BendAllowanceType',
  'data.IAccessSelections2(model, null)', 'data.GetCustomBendAllowance()',
  'allowance.Type', 'allowance.KFactor', 'effectiveBendAllowanceType',
  'data.ReleaseSelectionAccess()', 'row.readable']) {
  assert.equal(sheetMetalMeasurementSource.includes(fragment), true,
    `sheet-metal measurement is missing: ${fragment}`)
}
const templateMeasurementSource = methodSlice(
  'CaptureNativeTopCoverTemplateComponentStates(ISldWorks application,',
  'private static bool NativeTopCoverTemplateComponentStatesComplete(')
for (const fragment of ['templateCacheBeforeEmpty', 'templateOpenWarnings',
  'templateReadOnly', 'templateRebuilt', 'templateModelExternalReferenceCount',
  'component.IsHidden(true)', 'component.Visible', 'component.IsEnvelope()',
  'component.ExcludeFromBOM', 'component.IsSuppressed()', 'component.IsFixed()',
  'component.GetSuppression()', 'TransformValues(transform)',
  'templateCacheAfterEmpty', 'templateFileUnchanged']) {
  assert.equal(templateMeasurementSource.includes(fragment), true,
    `template component-state measurement is missing: ${fragment}`)
}
const measurementCompleteSource = methodSlice(
  'private static bool NativeTopCoverPartRowMeasurementComplete(',
  'private static bool RunNativeTopCoverPartMeasurementCompletenessSelfTest(')
for (const fragment of ['saveFlagBeforeCaptured', 'saveFlagAfterCaptured',
  'partBoxApiRaw.Count == 6', 'partBoxDerivedFromBodies',
  'row.bodies.All(value => value.complete', 'sheetMetalProfileComplete',
  'row.modelExternalReferenceCount >= 0', 'row.geometrySignatureSha256']) {
  assert.equal(measurementCompleteSource.includes(fragment), true,
    `measurement completeness gate is missing: ${fragment}`)
}
const measurementPrivateSource = methodSlice(
  'private static bool WriteNativeTopCoverPartMeasurementPrivateEvidence(',
  'private static void WriteNativeTopCoverPartMeasurementProbeCleanupEvidence(')
for (const fragment of ['probe.privateEvidenceWritten = true',
  'WriteCreateNewAndReread(', 'Bool(ReadJsonObject(evidencePath),',
  '"privateEvidenceWritten"', '"probeOnly"', '"consumable"']) {
  assert.equal(measurementPrivateSource.includes(fragment), true,
    `measurement private-evidence reread gate is missing: ${fragment}`)
}
const detachRunSource = methodSlice(
  'private static void RunNativeTopCoverPartDetachProbeOnly(Result result)',
  'private static IEnumerable<string> TopCoverPhysicalPartNames()')
for (const fragment of ['mode = "detach_topcover_physical_parts"',
  'TopCoverPhysicalPartNames()', 'CaptureNativeTopCoverPhysicalParts(result, "baseline_readonly")',
  'probe.operations.Add(operation)', 'DetachNativeTopCoverPhysicalPart(result, operation)',
  'CaptureNativeTopCoverPhysicalParts(result, "fresh_readonly_after_detach")',
  'ChangedSourceNames(result)', 'TargetExact75NameSet(result)', 'changedNamesExact',
  'nonPhysicalTargetHashesUnchanged',
  'SourceExact75Unchanged(result)', 'WriteNativeTopCoverPartDetachPrivateEvidence(']) {
  assert.equal(detachRunSource.includes(fragment), true,
    `physical-part detach probe is missing: ${fragment}`)
}
assert.equal(detachRunSource.indexOf('probe.operations.Add(operation)') <
  detachRunSource.indexOf('DetachNativeTopCoverPhysicalPart(result, operation)'), true,
'detach failure evidence must be registered before the writable helper can throw')
for (const forbidden of ['.Save3(', '.SaveAs(', 'BreakAllExternal',
  'UpdateExternalFileReferences', 'ReplaceReferencedDocument', 'ReplaceComponents',
  '.NewPart(', '.NewAssembly(', '.AddComponent', 'repair_staging']) {
  assert.equal(detachRunSource.includes(forbidden), false,
    `detach orchestrator contains unexpected mutation behavior: ${forbidden}`)
}
const detachHelperSource = methodSlice(
  'private static void DetachNativeTopCoverPhysicalPart(',
  'private static int ExpectedTopCoverPhysicalExternalCount(')
for (const fragment of ['WaitForGlobalCadQuiescence(', 'StartOwnedSolidWorks(result, session)',
  'swOpenDocOptions_e.swOpenDocOptions_Silent', '!model.IsOpenedReadOnly()',
  'ExpectedTopCoverPhysicalExternalCount(name)', 'extension.BreakAllExternalFileReferences2(true)',
  'model.ForceRebuild3(false)', 'model.ListExternalFileReferencesCount2()',
  'extension.ListExternalFileReferencesCount()', 'DetachFeatureHealthClean(', 'model.Save3(',
  'saveErrors == 0', 'saveWarnings == 0', '!operation.dirtyAfterSave',
  'DocumentCacheEmpty(application, out afterDocuments)', 'session.exit_state == "exited"']) {
  assert.equal(detachHelperSource.includes(fragment), true,
    `detach helper safety gate is missing: ${fragment}`)
}
for (const forbidden of ['BreakAllExternalReferences', 'Feature.BreakLink',
  'UpdateExternalFileReferences', 'ReplaceReferencedDocument', 'ReplaceComponents',
  '.NewPart(', '.NewAssembly(', '.AddComponent']) {
  assert.equal(detachHelperSource.includes(forbidden), false,
    `detach helper contains forbidden link mutation API: ${forbidden}`)
}
const detachAfterSource = methodSlice(
  'private static bool PhysicalPartAfterDetachComplete(',
  'private static bool HasDetachForbiddenFeature(')
for (const fragment of ['modelExternalReferenceCount == 0',
  'extensionExternalReferenceCount == 0', 'physicalGeometrySignatureSha256',
  'SheetMetalProfileSignature(first) ==', 'row.featureIssues.Count == 0',
  '!HasDetachForbiddenFeature(row)']) {
  assert.equal(detachAfterSource.includes(fragment), true,
    `detach fresh-readonly comparison gate is missing: ${fragment}`)
}
const detachPrivateSource = methodSlice(
  'private static bool WriteNativeTopCoverPartDetachPrivateEvidence(',
  'private static void WriteNativeTopCoverPartDetachProbeCleanupEvidence(')
for (const fragment of ['NativeTopCoverPartDetachPrivateRelativePath',
  'SamePath(parent, expectedParent)', 'AssertPathChainNoReparse(',
  'WriteCreateNewAndReread(', '"privateEvidenceWritten"']) {
  assert.equal(detachPrivateSource.includes(fragment), true,
    `detach private evidence boundary is missing: ${fragment}`)
}
const detachCleanupSource = methodSlice(
  'private static void WriteNativeTopCoverPartDetachProbeCleanupEvidence(',
  'private static void RunNativeTopCoverRebuildProbeOnly(Result result)')
for (const fragment of ['NativeTopCoverPartDetachCleanupPrivateRelativePath',
  'SamePath(parent, expectedParent)', 'AssertPathChainNoReparse(',
  'WriteCreateNewAndReread(', 'targetRollbackAttempted', 'targetRollbackSucceeded',
  'transientLockFilesRemoved', 'TOPCOVER_NATIVE_PART_DETACH_CLEANUP_REREAD_FAILED']) {
  assert.equal(detachCleanupSource.includes(fragment), true,
    `detach cleanup boundary is missing: ${fragment}`)
}
const topCoverRunSource = methodSlice(
  'private static void RunNativeTopCoverRebuildProbeOnly(Result result)',
  'private static void CreateSafeProbeDirectory(')
for (const fragment of ['before_topcover_template_capture',
  'before_topcover_candidate_create', 'before_topcover_candidate_verify',
  'before_topcover_candidate_swap', 'TOPCOVER_NATIVE_SWAP_NOT_QUIESCENT',
  'at_topcover_candidate_swap_boundary', 'preSwapFiles',
  'TOPCOVER_NATIVE_SWAP_BOUNDARY_DRIFT',
  'TOPCOVER_NATIVE_SWAP_TARGET_PATH_INVALID',
  'TOPCOVER_NATIVE_SWAP_CANDIDATE_PATH_INVALID',
  'TOPCOVER_NATIVE_SWAP_ORIGINAL_PATH_INVALID',
  'CanonicalExistingPath(candidateDirectory)',
  'CanonicalExistingPath(originalDirectory)',
  'File.Delete(oldTopCover)', 'File.Move(candidatePath, oldTopCover)',
  'TOPCOVER_NATIVE_SWAP_NAME_SET_INVALID',
  'SetEquals(allowed)', 'VerifySourceUnchanged(result)',
  'probe.privateEvidenceWritten = WriteNativeTopCoverProbePrivateEvidence']) {
  assert.equal(topCoverRunSource.includes(fragment), true,
    `native top-cover transaction gate is missing: ${fragment}`)
}
assert.equal(topCoverRunSource.indexOf('TOPCOVER_NATIVE_SWAP_NOT_QUIESCENT') <
  topCoverRunSource.indexOf('at_topcover_candidate_swap_boundary') &&
  topCoverRunSource.indexOf('at_topcover_candidate_swap_boundary') <
  topCoverRunSource.indexOf('File.Copy(oldTopCover, originalBackup'), true,
'stable CAD quiescence must precede reauthorization, revalidation and swap')
assert.equal(topCoverRunSource.includes('RollbackNativeTopCoverRepairStaging'), false,
  'repair staging must not be deleted before the outer final CAD gate')

const createProbeDirectorySource = methodSlice(
  'private static void CreateSafeProbeDirectory(',
  'private static NativeTopCoverTemplate CaptureNativeTopCoverTemplate(')
assert.equal(createProbeDirectorySource.indexOf('AssertPathChainNoReparse(path, parent,') <
  createProbeDirectorySource.indexOf('Directory.CreateDirectory(path)'), true,
'probe directories must validate every existing ancestor before creation')

const templateSource = methodSlice(
  'private static NativeTopCoverTemplate CaptureNativeTopCoverTemplate(',
  'private static bool NativeTopCoverTemplateGate(')
for (const fragment of ['warnings == 96', '(warnings & 1) == 0',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly', 'TransformValues(transform)',
  'component.GetSuppression()', 'component.IsSuppressed()',
  'model.IsOpenedReadOnly()', 'output.fileUnchanged', 'output.sessionExitProven']) {
  assert.equal(templateSource.includes(fragment), true,
    `old top-cover read-only template gate is missing: ${fragment}`)
}
for (const forbidden of ['Save3(', 'SaveAs', 'SetTransformAndSolve2', 'FixComponent']) {
  assert.equal(templateSource.includes(forbidden), false,
    `old top-cover template method contains a mutation API: ${forbidden}`)
}
assert.equal(templateSource.indexOf('model.ForceRebuild3(false)') <
  templateSource.indexOf('TransformValues(transform)'), true,
'old top-cover template must rebuild read-only before Transform2 capture')

const candidateCreateSource = methodSlice(
  'private static void CreateNativeTopCoverCandidate(',
  'private static void VerifyNativeTopCoverCandidate(')
for (const fragment of ['application.NewAssembly()', 'application.OpenDoc6(row.path,',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly',
  'template.components.OrderBy(', 'TopCoverCandidateUpstreamFirstFileNames[1]',
  'candidateComponentOpenProfiles.Add(',
  'swAddComponentConfigOptions_CurrentSelectedConfig',
  'component.SetTransformAndSolve2(transform)', 'assembly.FixComponent()',
  'TOPCOVER_NATIVE_COMPONENT_READBACK_INVALID',
  'swSaveAsVersion_e.swSaveAsCurrentVersion', 'candidate.GetSaveFlag()',
  'candidateSaveReturned', 'candidateSaveErrors', 'candidateSaveWarnings',
  'candidateFileExistsAfterSave', 'candidateDirtyAfterSave',
  'CandidateSaveProfileAllowed(', 'swFileSaveError_e.swReadOnlySaveError',
  'Release(extension)', 'Release(math)']) {
  assert.equal(candidateCreateSource.includes(fragment), true,
    `native top-cover candidate creation gate is missing: ${fragment}`)
}
for (const forbidden of ['model.Save3(', 'GetPackAndGo', 'ReplaceReferencedDocument',
  'ReplaceComponents', 'dynamic ']) {
  assert.equal(candidateCreateSource.includes(forbidden), false,
    `candidate creation contains a forbidden API or late binding: ${forbidden}`)
}

const candidateVerifySource = methodSlice(
  'private static void VerifyNativeTopCoverCandidate(',
  'private static void ProbeNativeRootAfterSwap(')
for (const fragment of ['candidateClosurePaths', 'actualPaths.SetEquals(expected.Keys)',
  'actualClosure.SetEquals(expectedClosure)', 'SameTransform16(',
  'row.configuration', 'row.suppression', 'row.suppressed',
  'candidateFreshReadOnly', 'candidateFreshRebuilt',
  'CaptureRelocationFeatureDiagnostics(',
  'CaptureRelocationAssemblyComponentDiagnostics(',
  'candidateExternalReferenceDiagnostic',
  'probe.candidateFileUnchanged', 'probe.candidateSessionExitProven']) {
  assert.equal(candidateVerifySource.includes(fragment), true,
    `candidate exact-set fresh verification is missing: ${fragment}`)
}

const rootInitialSource = methodSlice(
  'private static void ProbeNativeRootAfterSwap(',
  'private static List<string> CaptureInternalIdMismatchPaths(')
for (const fragment of ['warnings == 32 || warnings == 33',
  'model.IsOpenedReadOnly()', 'ExactDependencyGate(',
  'SamePath(', 'HierarchyGate(probe.initialRootHierarchy)',
  'before_topcover_root_id_acceptance', 'before_topcover_final_verification']) {
  assert.equal(rootInitialSource.includes(fragment), true,
    `root 32/33 probe gate is missing: ${fragment}`)
}

const idAcceptSource = methodSlice(
  'private static void AcceptExact33RootIdMismatch(',
  'private static void VerifyNativeRootFinal(')
assert.equal((idAcceptSource.match(/model\.Save3\(/g) ?? []).length, 1,
  'root ID acceptance must contain exactly one Save3 call')
for (const fragment of ['warnings == 33', 'mismatchPaths.Count == 1',
  'SamePath(mismatchPaths[0]', 'resolveStatus == 0 || resolveStatus == 2',
  'KnownRootIssueGate(', 'rootShaBeforeIdAcceptance',
  'rootShaAfterIdAcceptance', 'model.GetSaveFlag()']) {
  assert.equal(idAcceptSource.includes(fragment), true,
    `exact-33 root ID acceptance gate is missing: ${fragment}`)
}

const finalProbeSource = methodSlice(
  'private static void VerifyNativeRootFinal(',
  'private static bool FinalProbeRelocationDiagnosticGate(')
for (const fragment of ['warnings == 32', 'model.IsOpenedReadOnly()',
  'model.ForceRebuild3(false)', 'ExactDependencyGate(',
  'HierarchyGate(', 'KnownRootIssueGate(',
  'FinalProbeRelocationDiagnosticGate(']) {
  assert.equal(finalProbeSource.includes(fragment), true,
    `final native root verification gate is missing: ${fragment}`)
}

const repairRollbackSource = methodSlice(
  'private static void RollbackNativeTopCoverRepairStaging(Result result)',
  'private static void RunRelocationPrewriteDiagnostic(')
for (const fragment of ['topcover_native_rebuild', 'candidate', 'original',
  'TreeHasReparsePoint(repairRoot)', 'FileLinkCount(path) != 1',
  'TopCoverAssemblyFileName', 'topcover_probe_repair_rollback_succeeded']) {
  assert.equal(repairRollbackSource.includes(fragment), true,
    `repair-staging rollback whitelist is missing: ${fragment}`)
}
const cleanupEvidenceSource = methodSlice(
  'private static void WriteNativeTopCoverProbeCleanupEvidence(Result result)',
  'private static void RollbackNativeTopCoverRepairStaging(Result result)')
for (const fragment of ['result.preflight_passed', 'expectedParent',
  'TOPCOVER_NATIVE_CLEANUP_EVIDENCE_PRECREATE_PATH_INVALID',
  'Directory.CreateDirectory(parent)', 'cleanupPass']) {
  assert.equal(cleanupEvidenceSource.includes(fragment), true,
    `post-rollback cleanup evidence scope gate is missing: ${fragment}`)
}
assert.equal(cleanupEvidenceSource.indexOf(
  'TOPCOVER_NATIVE_CLEANUP_EVIDENCE_PRECREATE_PATH_INVALID') <
  cleanupEvidenceSource.indexOf('Directory.CreateDirectory(parent)'), true,
'cleanup evidence must validate the existing path chain before creating directories')
const privateEvidenceSource = methodSlice(
  'private static bool WriteNativeTopCoverProbePrivateEvidence(Result result,',
  'private static void WriteNativeTopCoverProbeCleanupEvidence(Result result)')
for (const fragment of ['result.preflight_passed', 'expectedParent',
  'TOPCOVER_NATIVE_PRIVATE_EVIDENCE_PRECREATE_PATH_INVALID',
  'probe.privateEvidenceWritten = true',
  'Bool(ReadJsonObject(evidencePath),']) {
  assert.equal(privateEvidenceSource.includes(fragment), true,
    `private probe evidence gate is missing: ${fragment}`)
}
assert.equal(privateEvidenceSource.indexOf(
  'TOPCOVER_NATIVE_PRIVATE_EVIDENCE_PRECREATE_PATH_INVALID') <
  privateEvidenceSource.indexOf('Directory.CreateDirectory(parent)'), true,
'private probe evidence must validate ancestors before creating directories')
for (const fragment of ['StableRollbackCadGate(result)',
  'RollbackTarget(result)', 'RollbackNativeTopCoverRepairStaging(result)',
  'WriteNativeTopCoverProbeCleanupEvidence(result)',
  'result.preflight_passed', 'result.target_created_by_this_run']) {
  assert.equal(source.includes(fragment), true,
    `outer fail-closed cleanup gate is missing: ${fragment}`)
}
const stableRollbackSource = methodSlice(
  'private static bool StableRollbackCadGate(Result result)',
  'private static void RollbackTarget(Result result)')
assert.equal(stableRollbackSource.includes('!result.processes.final_gate'), false,
  'rollback must wait for transient CAD exit even when the first final snapshot was nonzero')
assert.equal(stableRollbackSource.indexOf('WaitForGlobalCadQuiescence(') <
  stableRollbackSource.indexOf('CaptureFinalProcessGate(result)'), true,
'rollback must reprove stable global quiescence before its final process gate')
const rollbackTargetSource = methodSlice(
  'private static void RollbackTarget(Result result)',
  'private static bool RollbackPathSafe(')
for (const fragment of ['RollbackPathSafe(result.target_directory',
  'rollback_transient_lock_files_removed', 'File.Delete(lockFile)',
  'RollbackCadBasenameSubsetSafe(cadFiles.Select(Path.GetFileName)']) {
  assert.equal(rollbackTargetSource.includes(fragment), true,
    `rollback transient-lock handling is missing: ${fragment}`)
}
const rollbackPathSource = methodSlice(
  'private static bool RollbackPathSafe(',
  'private static bool RollbackCadBasenameSubsetSafe(')
for (const fragment of ['sourceHashes', 'PairedSolidWorksLockFileSafe(',
  'name.StartsWith("~$", StringComparison.Ordinal)', 'FileLinkCount(path) != 1',
  'new FileInfo(path).Length <= 4096']) {
  assert.equal(rollbackPathSource.includes(fragment), true,
    `rollback paired-lock safety gate is missing: ${fragment}`)
}
assert.equal(source.includes('RunRelocationPrewriteDiagnostic(result)'), true,
  'formal direct-copy seed must retain the prewrite diagnostic')
for (const fragment of [
  'File.Copy(row.path, destination, false)',
  '9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B',
  'WINNSEN_NATIVE_EXECUTION_AUTH_PATH',
  'WINNSEN_NATIVE_EXECUTION_AUTH_SHA256',
  'Type.GetTypeFromProgID("SldWorks.Application.28", true)',
  'ExpectedSolidWorksVersionPrefix = "28."',
  'ResolveAllLightWeightComponents(false)',
  'model.Save3(',
  'model.GetSaveFlag()',
  'warnings == 32 || warnings == 96',
  'warnings == 32',
  'RunRelocationPrewriteDiagnostic(result)',
  'CaptureRelocationDocumentDiagnostics(',
  'ListExternalFileReferences2(',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly',
  'ExpectedDependencyRawCount = 148',
  'ExpectedRecursiveComponents = 158',
  'KnownIssueName = "箱体右侧板焊接-1"',
  'FileMode.CreateNew',
  'CreateHardLink(',
  'GetFinalPathNameByHandle(',
  'refused to kill unproven',
  'KillExactMonitorIdentity(monitor, session.sldworks.pid, processes)',
  'TrustedToolchainStateStillValid(result)',
  'LockToolchainManifestSchema',
  'LockToolchainManifestGeneratedBy',
  'ValidateSpecializedLockToolchainManifest',
  'LockToolchainValidatorRelativePath',
]) {
  assert.equal(source.includes(fragment), true, `missing static seed gate: ${fragment}`)
}

const mainStart = source.indexOf('private static int Main')
const prewriteDiagnostic = source.indexOf('RunRelocationPrewriteDiagnostic(result)', mainStart)
const mainEnd = source.indexOf('private static void PrintUsage', mainStart)
const mainSource = source.slice(mainStart, mainEnd)
const writableCall = source.indexOf('StabilizeWritableRoot(result)', mainStart)
const writableStart = source.indexOf('private static void StabilizeWritableRoot')
const writableSave = source.indexOf('model.Save3(', writableStart)
assert.equal(mainStart >= 0 && prewriteDiagnostic > mainStart &&
  writableCall > prewriteDiagnostic && writableStart > writableCall &&
  writableSave > writableStart &&
  mainSource.match(/StabilizeWritableRoot\(result\)/g)?.length === 1, true,
'complete 75-document warning 64 diagnostic must run before writable stabilization')
const prewriteStart = source.indexOf(
  'private static void RunRelocationPrewriteDiagnostic')
const prewriteEnd = source.indexOf(
  'private static void StabilizeWritableRoot', prewriteStart)
const prewriteSource = source.slice(prewriteStart, prewriteEnd)
for (const fragment of ['scanComplete=true; scanned=75;',
  'KnownRelocatedPrewriteWarningGate(diagnostic)',
  'result.known_relocated_warning64_gate', 'CloseOwnedSolidWorks']) {
  assert.equal(prewriteSource.includes(fragment), true,
    `prewrite diagnostic is missing a complete fail-closed gate: ${fragment}`)
}
const knownRelocatedWarningSource = methodSlice(
  'private static bool KnownRelocatedPrewriteWarningGate(',
  'private static bool RelocationBasePartDiagnosticRequired(')
for (const fragment of ['warning64Docs=none', 'TopCoverAssemblyFileName',
  'RootFileName', ':0/96|', 'diagnostic.Contains(exactKnown)']) {
  assert.equal(knownRelocatedWarningSource.includes(fragment), true,
    `known relocated warning gate is missing: ${fragment}`)
}
const readonlyStart = source.indexOf('private static void VerifyFreshReadonly(Result result)')
const readonlyDiagnostic = source.indexOf(
  'if ((warnings & 64) != 0 && !FreshReopenWarningAllowed(errors, warnings))',
  readonlyStart)
const readonlyExactGate = source.indexOf(
  'Require(model != null && FreshReopenWarningAllowed(errors, warnings)', readonlyStart)
assert.equal(readonlyStart >= 0 && readonlyDiagnostic > readonlyStart &&
  readonlyExactGate > readonlyDiagnostic, true,
'post-save warning 64 diagnostic must run before the exact read-only reopen gate')
const writableSource = source.slice(writableStart, readonlyStart)
for (const fragment of ['WRITABLE_ROOT_SESSION_NOT_QUIESCENT',
  'WaitForGlobalCadQuiescence(', 'InitialRelocatedWarningAllowed(warnings)',
  'WRITABLE_PRE_SAVE_IDENTITY_GATE_FAILED', 'CaptureDependencies(application,',
  'result.writable_dependency_gate', 'result.writable_known_root_issue_gate',
  'model.Save3(']) {
  assert.equal(writableSource.includes(fragment), true,
    `root-only stabilization is missing a pre-save gate: ${fragment}`)
}
assert.equal(writableSource.indexOf('WaitForGlobalCadQuiescence(') <
  writableSource.indexOf('StartOwnedSolidWorks(') &&
  writableSource.indexOf('InitialRelocatedWarningAllowed(warnings)') <
    writableSource.indexOf('model.Save3(') &&
  writableSource.indexOf('WRITABLE_PRE_SAVE_IDENTITY_GATE_FAILED') <
  writableSource.indexOf('model.Save3('), true,
'quiescence, exact known 32/96, dependency and feature gates must all precede root Save3')
assert.equal(source.slice(readonlyStart).indexOf('WaitForGlobalCadQuiescence(') <
  source.slice(readonlyStart).indexOf('StartOwnedSolidWorks('), true,
'root read-only reopen must wait for stable global CAD quiescence')
const dependencyGateStart = source.indexOf(
  'private static bool DependencyInventorySetGate(')
const dependencyGateEnd = source.indexOf(
  'private static string ExternalReferenceDiagnostic', dependencyGateStart)
assert.equal(dependencyGateStart > 0 && dependencyGateEnd > dependencyGateStart, true,
  'dependency inventory lock-file gate must be present')
const dependencyGateSource = source.slice(dependencyGateStart, dependencyGateEnd)
for (const fragment of ['name.StartsWith("~$", StringComparison.Ordinal)',
  'name.Substring(2)', 'inventory.Contains(targetPath)',
  'normalizedClosure.Contains(targetPath)',
  'transientLockFilesValid && normalizedClosure.SetEquals(inventory)']) {
  assert.equal(dependencyGateSource.includes(fragment), true,
    `dependency inventory gate is missing an exact lock-file condition: ${fragment}`)
}
const topCoverPathGateStart = source.indexOf(
  'private static bool TopCoverComponentPathSetGate(')
const topCoverPathGateEnd = source.indexOf(
  'private static FreshComponentDiagnostic', topCoverPathGateStart)
assert.equal(topCoverPathGateStart > 0 && topCoverPathGateEnd > topCoverPathGateStart, true,
  'exact top-cover component path-set gate must be present')
const topCoverPathGateSource = source.slice(topCoverPathGateStart, topCoverPathGateEnd)
for (const fragment of ['TopCoverCandidateUpstreamFirstFileNames.Skip(1)',
  'expected.Count == 6', 'actual.Count == 6', 'actual.SetEquals(expected)']) {
  assert.equal(topCoverPathGateSource.includes(fragment), true,
    `top-cover component path-set gate is missing: ${fragment}`)
}
const diagnosticStart = source.indexOf(
  'private static string CaptureRelocationDocumentDiagnostics')
const diagnosticEnd = source.indexOf(
  'private static FeatureHealthEvidence CaptureFeatureHealth', diagnosticStart)
const diagnosticSource = source.slice(diagnosticStart, diagnosticEnd)
for (const fragment of [
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly',
  'CloseDocument(application, ref model)',
  'application.CloseAllDocuments(true)',
  'DocumentCacheEmpty(application, out documentsBefore)',
  'DocumentCacheEmpty(application, out documentsAfter)',
  'paths.Count == ExpectedCadCount',
  'scanned == ExpectedCadCount',
]) {
  assert.equal(diagnosticSource.includes(fragment), true,
    `read-only relocation diagnostic is missing a lifecycle gate: ${fragment}`)
}
for (const forbidden of ['Save3(', 'SaveAs', 'UpdateExternalFileReferences',
  'BreakAll', 'BreakLink']) {
  assert.equal(diagnosticSource.includes(forbidden), false,
    `read-only relocation diagnostic contains a mutation API: ${forbidden}`)
}
assert.equal(diagnosticSource.includes(
  'string.Equals(type, "Reference", StringComparison.OrdinalIgnoreCase)'), true,
'root Reference features must be included in read-only external-link diagnostics')

const componentDiagnosticStart = source.indexOf(
  'private static void CaptureRelocationAssemblyComponentDiagnostics')
const componentFreshStart = source.indexOf(
  'CaptureTopCoverComponentFreshReadOnlyDiagnostics(Result result,',
  componentDiagnosticStart)
const componentDiagnosticEnd = source.indexOf(
  'private static FeatureHealthEvidence CaptureFeatureHealth', componentFreshStart)
assert.equal(componentDiagnosticStart > diagnosticStart &&
  componentFreshStart > componentDiagnosticStart &&
  componentDiagnosticEnd > componentFreshStart, true,
'warning 64 assembly component diagnostics must be part of the prewrite read-only slice')
const componentDiagnosticSource = source.slice(componentDiagnosticStart, componentDiagnosticEnd)
for (const fragment of [
  'GetRootComponent3(true)',
  'component.GetChildren()',
  'component.GetPathName()',
  'component.GetSuppression()',
  'component.GetModelDoc2()',
  'ReleaseOneComReference(cachedModel)',
  'component.GetSelectByIDString()',
  'component.ReferencedConfiguration',
  'CloseOwnedSolidWorks',
  'application.OpenDoc6(',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly',
  'CaptureRelocationFeatureDiagnostics(',
  'CloseDocument(application, ref model)',
  'application.CloseAllDocuments(true)',
  'DocumentCacheEmpty(application, out documentsBefore)',
  'DocumentCacheEmpty(application, out documentsAfter)',
  'TopCoverComponentDiagnosticComplete(',
]) {
  assert.equal(componentDiagnosticSource.includes(fragment), true,
    `component-level relocation diagnostic is missing a fail-closed gate: ${fragment}`)
}
for (const forbidden of ['Save3(', 'SaveAs', 'SaveAs3',
  'UpdateExternalFileReferences', 'BreakAll', 'BreakLink', 'ReplaceComponents',
  'SetSuppression2', 'ResolveAllLightWeightComponents', 'EditRebuild3',
  'Release(cachedModel)']) {
  assert.equal(componentDiagnosticSource.includes(forbidden), false,
    `component-level relocation diagnostic contains a mutation API: ${forbidden}`)
}
assert.equal(source.includes(
  'checks["warning64_assembly_component_diagnostic_fail_closed"]'), true,
'self-test must lock incomplete traversal/read-only/cache component diagnostics closed')
assert.equal(source.includes(
  'checks["native_topcover_part_measurement_probe_is_nonconsumable"]'), true,
'self-test must lock retained measurement evidence to private nonconsumable scope')
assert.equal(source.includes(
  'checks["native_topcover_part_detach_probe_is_nonconsumable"]'), true,
'self-test must lock the active detach probe to private nonconsumable scope')
assert.equal(source.includes(
  'checks["native_topcover_part_detach_contract_fails_closed"]'), true,
'self-test must reject physical geometry, sheet-metal, and forbidden-feature drift')
assert.equal(source.includes(
  'checks["native_topcover_part_measurement_completeness_fails_closed"]'), true,
'self-test must reject incomplete body, sheet-metal and save-flag measurements')
assert.equal(source.includes(
  'checks["native_topcover_transform_and_final_diagnostic_fail_closed"]'), true,
'self-test must lock transform tolerance and final relocation diagnostics fail closed')
assert.equal(source.includes(
  'checks["native_topcover_part_measurement_private_evidence_serializes_written_true"]'), true,
'self-test must reread measurement evidence with privateEvidenceWritten=true')
assert.equal(source.includes(
  'checks["native_topcover_candidate_save_profile_is_exact"]'), true,
'self-test must lock the only two allowed candidate persistence profiles')

const componentFreshEnd = source.indexOf(
  'private static bool FreshComponentDiagnosticComplete', componentFreshStart)
const componentFreshSource = source.slice(componentFreshStart, componentFreshEnd)
assert.equal(componentFreshSource.indexOf('WaitForGlobalCadQuiescence(') >= 0 &&
  componentFreshSource.indexOf('WaitForGlobalCadQuiescence(') <
  componentFreshSource.indexOf('StartOwnedSolidWorks('), true,
'fresh component sessions must wait for stable global CAD quiescence before activation')
for (const fragment of ['() => ProcessIds("SLDWORKS")',
  '() => ProcessIds("sldProcMon")', 'TOP_COVER_FRESH_SESSION_NOT_QUIESCENT',
  'GlobalCadProcessSnapshot()']) {
  assert.equal(componentFreshSource.includes(fragment), true,
    `fresh component quiescence gate is missing: ${fragment}`)
}
const quiescenceStart = source.indexOf(
  'private static bool WaitForGlobalCadQuiescence(')
const quiescenceEnd = source.indexOf(
  'private static string GlobalCadProcessSnapshot()', quiescenceStart)
assert.equal(quiescenceStart > 0 && quiescenceEnd > quiescenceStart, true,
  'global CAD quiescence helper must be present')
const quiescenceSource = source.slice(quiescenceStart, quiescenceEnd)
for (const fragment of ['sldworksProbe', 'monitorProbe', 'quietWindowMs',
  'emptySince = null']) {
  assert.equal(quiescenceSource.includes(fragment), true,
    `global CAD quiescence helper is missing: ${fragment}`)
}
for (const forbidden of ['KillExactIdentity', 'KillExactMonitorIdentity',
  'CleanupOwnedMonitors', '.Kill(']) {
  assert.equal(quiescenceSource.includes(forbidden), false,
    `global CAD quiescence helper must never terminate a process: ${forbidden}`)
}

for (const fragment of ['"candidate-upstream-first"',
  'TopCoverCandidateUpstreamFirstFileNames', '"candidate-downstream-first"',
  'TopCoverCandidateDownstreamFirstFileNames',
  '(candidateUpstreamFirst.assembly_open_warnings & 64) != 0',
  '"root-plus-door-frame-bottom"', 'RootDoorFrameCandidateFileNames',
  '"root-plus-right-partition-chain"', 'RootRightPartitionCandidateFileNames',
  '"root-plus-door-frame-and-right-partition"',
  '"root-plus-all-known-basebody"', 'RootBaseBodyCandidateFileNames',
  'RootContextNeedsAnotherCandidate(rootContext)',
  '; topCoverLoadOrderScanComplete=', '; topCoverLoadOrderExperiments=']) {
  assert.equal(prewriteSource.includes(fragment), true,
    `prewrite diagnostic is missing a top-cover load-order branch: ${fragment}`)
}
const arrayValues = (name) => new RegExp(
  `private static readonly string\\[\\] ${name}\\s*=\\s*\\{([\\s\\S]*?)\\};`,
).exec(source)?.[1]?.match(/"([^"]+)"/g)?.map((value) => value.slice(1, -1)) ?? []
assert.deepEqual(arrayValues('TopCoverCandidateUpstreamFirstFileNames'), [
  '标准寄存柜 模型.SLDPRT', '上盖 模型.sldprt', '上盖壳体左侧板.sldprt',
  '上盖壳体右侧板.SLDPRT', '上盖壳体前侧板.sldprt',
  '上盖壳体后侧板.sldprt', '上盖壳体底板.sldprt',
])
assert.deepEqual(arrayValues('TopCoverCandidateDownstreamFirstFileNames'), [
  '上盖壳体前侧板.sldprt', '上盖壳体后侧板.sldprt',
  '上盖壳体底板.sldprt', '上盖壳体右侧板.SLDPRT',
  '上盖壳体左侧板.sldprt', '上盖 模型.sldprt', '标准寄存柜 模型.SLDPRT',
])
assert.deepEqual(arrayValues('RootDoorFrameCandidateFileNames'), ['门框 下.sldprt'])
assert.deepEqual(arrayValues('RootRightPartitionCandidateFileNames'), [
  '箱体竖隔板L.sldprt', '箱体竖隔板R.SLDPRT',
  '箱体竖隔板L焊接.SLDASM', '箱体竖隔板R焊接.SLDASM',
])
assert.deepEqual(arrayValues('RootBaseBodyCandidateFileNames'), [
  '储物柜门板2╱12_右.SLDPRT', '储物柜门板4╱12_右.SLDPRT',
  '储物柜门板6╱12_右.SLDPRT', '插销固定板2╱12_左.SLDPRT',
  '插销固定板2╱12_右.SLDPRT', '插销固定板4╱12_左.SLDPRT',
  '插销固定板4╱12_右.SLDPRT', '插销固定板6╱12_左.SLDPRT',
  '插销固定板6╱12_右.SLDPRT', '开口挡圈5.SLDPRT', '锁舌.SLDPRT',
])
const loadOrderStart = source.indexOf(
  'CaptureTopCoverAssemblyLoadOrderDiagnostic(Result result,')
const loadOrderEnd = source.indexOf(
  'private static bool TopCoverLoadOrderDiagnosticComplete', loadOrderStart)
assert.equal(loadOrderStart > componentFreshStart && loadOrderEnd > loadOrderStart, true,
  'top-cover load-order diagnostic helper must follow fresh component diagnostics')
const loadOrderSource = source.slice(loadOrderStart, loadOrderEnd)
for (const fragment of ['expectedNames.Count == 7', 'WaitForGlobalCadQuiescence(',
  'StartOwnedSolidWorks(', 'application.OpenDoc6(',
  'swOpenDocOptions_e.swOpenDocOptions_ReadOnly', 'IsOpenedReadOnly()',
  'assemblyModel.ForceRebuild3(false)',
  'CaptureRelocationFeatureDiagnostics(',
  'CaptureRelocationAssemblyComponentDiagnostics(',
  'IsAssembly(rootPath)', 'rootModel = application.OpenDoc6(',
  'additionalNames.Distinct(StringComparer.OrdinalIgnoreCase)',
  'documentType = IsAssembly(path)', 'output.additional_preload_complete',
  'rootModel.ForceRebuild3(false)', 'HierarchyGate(',
  'KnownRootIssueGate(', 'Release(rootModel)',
  'hashesBefore.Count == expectedNames.Count +', 'additionalNames.Count + 2',
  'application.CloseAllDocuments(true)',
  'DocumentCacheEmpty(application, out documentsBefore)',
  'DocumentCacheEmpty(application, out documentsAfter)',
  'Sha256File(row.Key)', 'output.files_unchanged']) {
  assert.equal(loadOrderSource.includes(fragment), true,
    `top-cover load-order diagnostic is missing a read-only gate: ${fragment}`)
}
for (const forbidden of ['Save3(', 'SaveAs', 'SaveAs3',
  'UpdateExternalFileReferences', 'BreakAll', 'BreakLink', 'ReplaceComponents',
  'SetSuppression2', 'ResolveAllLightWeightComponents', 'EditRebuild3',
  'KillExactIdentity', 'KillExactMonitorIdentity']) {
  assert.equal(loadOrderSource.includes(forbidden), false,
    `top-cover load-order diagnostic contains a mutation API: ${forbidden}`)
}

const sourceReceiptKeys = /private static readonly string\[\] SeedReceiptKeys\s*=\s*\{([\s\S]*?)\};/
  .exec(source)?.[1]?.match(/"([^"]+)"/g)?.map((value) => value.slice(1, -1)) ?? []
assert.deepEqual(sourceReceiptKeys, [...NATIVE_SEED_RECEIPT_KEYS],
  'C# seed receipt keys drifted from shared contract')
const sourceExcludedKeys = /private static readonly string\[\] EvidenceCommitmentExcludedKeys\s*=\s*\{([\s\S]*?)\};/
  .exec(source)?.[1]?.match(/"([^"]+)"/g)?.map((value) => value.slice(1, -1)) ?? []
assert.deepEqual(sourceExcludedKeys, [...NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS],
  'C# commitment exclusion keys drifted from shared contract')
const sourceStageOrder = /private static readonly string\[\] StageReceiptOrder\s*=\s*\{([\s\S]*?)\};/
  .exec(source)?.[1]?.match(/"([^"]+)"/g)?.map((value) => value.slice(1, -1)) ?? []
assert.deepEqual(sourceStageOrder, [...NATIVE_STAGE_RECEIPT_ORDER],
  'C# stage receipt order drifted from shared contract')

const output = execFileSync(executablePath, ['--self-test'], {
  cwd: here,
  encoding: 'utf8',
  windowsHide: true,
  timeout: 30_000,
})
const result = JSON.parse(output)
assert.equal(result.status, 'PASS')
assert.equal(result.cadStarted, false)
assert.equal(result.checksFailed, 0)
assert.equal(result.checksTotal >= 13, true)
for (const name of [
  'canonical_name_sha_inventory_digest',
  'exact_shared_stage_receipt_contract',
  'exact_shared_authorization_contract',
  'authorization_id_and_expiry_mutations_rejected',
  'planning_only_plan_requires_short_authorization',
  'direct_copy_preserves_75_names_and_74_nonroot_hashes',
  'relocation_initial_warning_allows_only_32_or_96_without_id_mismatch',
  'relocated_warning_64_requires_readonly_diagnostic_before_save',
  'warning64_assembly_component_diagnostic_fail_closed',
  'cad_session_quiescence_rejects_late_process_appearance',
  'top_cover_load_order_diagnostic_is_read_only_and_fail_closed',
  'top_cover_load_order_diagnostic_captures_root_context_read_only',
  'root_context_candidate_groups_are_read_only_and_conditional',
  'reference_feature_external_links_are_read_only_diagnostic_only',
  'dependency_inventory_ignores_only_paired_solidworks_lock_files',
  'top_cover_component_paths_match_exact_readonly_preload_set',
  'native_topcover_part_measurement_probe_is_nonconsumable',
  'native_topcover_part_detach_probe_is_nonconsumable',
  'native_topcover_rebuild_capture_probe_is_nonconsumable',
  'native_topcover_master_reference_capture_probe_is_retained_dormant',
  'native_topcover_front_brep_capture_probe_is_nonconsumable',
  'native_topcover_front_brep_contract_fails_closed',
  'native_topcover_rebuild_capture_contract_fails_closed',
  'native_topcover_capture_pack_rollback_is_exact_and_lock_aware',
  'native_topcover_part_measurement_completeness_fails_closed',
  'native_topcover_part_detach_contract_fails_closed',
  'native_topcover_transform_and_final_diagnostic_fail_closed',
  'native_topcover_part_measurement_private_evidence_serializes_written_true',
  'native_topcover_candidate_save_profile_is_exact',
  'fresh_reopen_requires_exact_known_warning',
  'process_identity_ticks_are_exact_json_strings',
  'known_root_issue_requires_unique_reference_51',
  'semantic_commitment_matches_shared_vector',
  'json_bytes_replace_nonfinite_numbers_without_touching_strings',
  'ecmascript_json_cross_language_vectors',
  'evidence_and_receipt_are_paired_without_hash_cycle',
  'rollback_refuses_reparse_or_hardlink_escape',
  'rollback_requires_known_exact75_basename_subset',
  'specialized_lock_manifest_exact_and_mutations_rejected',
]) {
  assert.equal(result.checks?.[name], true, `missing self-test gate: ${name}`)
}

const sharedFixture = {
  z: 7,
  nested: { beta: false, alpha: ['L', 381, { y: 2, x: 1 }] },
  phase: 'clone_native_seed',
  completed_at_utc: 'ignored',
  phase_receipt_sha256: 'ignored',
}
assert.equal(nativeEvidenceCommitmentSha256(sharedFixture),
  '684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1')
const crossFixture = {
  ticks: 639220258081076150,
  float: 1.25,
  oneE20: 1e20,
  oneE21: 1e21,
  micro: 1e-6,
  subMicro: 1e-7,
  html: '<tag>&/x',
  control: 'line\nquote"\\',
  chinese: '结构',
}
const nodeCanonical = JSON.stringify(stableValue(crossFixture))
assert.equal(result.crossLanguageCanonicalJson, nodeCanonical,
  'C# canonical JSON differs from JSON.stringify(stableValue)')
assert.equal(result.crossLanguageCanonicalJsonSha256, sha256(Buffer.from(nodeCanonical, 'utf8')))

const lockManifest = JSON.parse(readFileSync(lockManifestPath, 'utf8'))
const lockToolSpecs = {
  native_lock_topology_888x14_v1: [
    'workers/native_model_requests/development/v1/tools/BuildLockTopology888x14.cs',
    'workers/native_model_requests/development/v1/tools/bin/BuildLockTopology888x14.exe',
  ],
  native_lock_topology_inspector_v1: [
    'workers/native_model_requests/development/v1/tools/InspectLockTopology888x14.cs',
    'workers/native_model_requests/development/v1/tools/bin/InspectLockTopology888x14.exe',
  ],
  native_assembly_tongue_inspector_888x14_v1: [
    'workers/native_model_requests/development/v1/tools/InspectAssemblyTongues888x14.cs',
    'workers/native_model_requests/development/v1/tools/bin/InspectAssemblyTongues888x14.exe',
  ],
}
assert.deepEqual(Object.keys(lockManifest).sort(), ['generatedBy', 'schema', 'tools', 'validator'])
assert.equal(lockManifest.schema,
  'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1')
assert.equal(lockManifest.generatedBy, 'VerifyLockTopology888x14Static.mjs')
assert.deepEqual(Object.keys(lockManifest.tools).sort(), Object.keys(lockToolSpecs).sort())
for (const [id, [sourceRelative, executableRelative]] of Object.entries(lockToolSpecs)) {
  const pinned = lockManifest.tools[id]
  assert.deepEqual(Object.keys(pinned).sort(), [
    'executablePath', 'executableSha256', 'sourceNormalizedSha256', 'sourcePath',
  ])
  const liveSource = resolve(repoRoot, sourceRelative)
  const liveExecutable = resolve(repoRoot, executableRelative)
  assert.equal(isAbsolute(pinned.sourcePath), true)
  assert.equal(isAbsolute(pinned.executablePath), true)
  assert.equal(resolve(pinned.sourcePath), liveSource)
  assert.equal(resolve(pinned.executablePath), liveExecutable)
  assert.equal(pathChainIsSafe(liveSource), true)
  assert.equal(pathChainIsSafe(liveExecutable), true)
  assert.equal(pinned.sourceNormalizedSha256,
    normalizedSourceSha256(readFileSync(liveSource, 'utf8')))
  assert.equal(pinned.executableSha256, sha256File(liveExecutable))
}
assert.deepEqual(Object.keys(lockManifest.validator).sort(), ['path', 'sha256'])
const liveLockValidator = resolve(repoRoot,
  'workers/native_model_requests/development/v1/tools/ValidateLockTopology888x14.mjs')
assert.equal(isAbsolute(lockManifest.validator.path), true)
assert.equal(resolve(lockManifest.validator.path), liveLockValidator)
assert.equal(pathChainIsSafe(liveLockValidator), true)
assert.equal(lockManifest.validator.sha256, sha256File(liveLockValidator))

const noArgs = spawnSync(executablePath, [], {
  cwd: here, encoding: 'utf8', windowsHide: true, timeout: 10_000,
})
assert.equal(noArgs.status, 2, 'no-argument invocation must fail closed with exit 2')
assert.match(noArgs.stderr, /^Usage:/)

const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
assert.deepEqual(Object.keys(manifest).sort(), ['generatedBy', 'schema', 'tool'])
assert.deepEqual(Object.keys(manifest.tool).sort(), [
  'executablePath', 'executableSha256', 'id', 'sourceNormalizedSha256',
  'sourcePath', 'verifierPath', 'verifierSha256',
])
assert.equal(manifest.schema, 'winnsen.16029.native_toolchain_manifest.v1')
assert.equal(manifest.generatedBy,
  'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/Verify-SeedPack888Native.ps1')
assert.equal(manifest.tool.id, 'native_seed_pack_888x14_v1')
assert.equal(manifest.tool.sourcePath,
  'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.cs')
assert.equal(manifest.tool.executablePath,
  'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.exe')
assert.equal(manifest.tool.verifierPath,
  'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/Verify-SeedPack888Native.ps1')
assert.equal(manifest.tool.sourceNormalizedSha256, normalizedSourceSha256(source))
assert.equal(manifest.tool.executableSha256, sha256File(executablePath))
assert.equal(manifest.tool.verifierSha256, sha256File(verifierPath))
assert.equal(NATIVE_TOOLCHAIN_MANIFEST_PATHS.native_seed_pack_888x14_v1,
  'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json')

const executable = readFileSync(executablePath)
const peOffset = executable.readUInt32LE(0x3c)
assert.equal(executable.readUInt16LE(peOffset + 4), 0x8664, 'executable must be PE32+ AMD64')
const afterProcesses = processSnapshot()
assert.deepEqual(afterProcesses, beforeProcesses,
  'static/self/noargs verification changed CAD process set')

console.log(JSON.stringify({
  status: 'STATIC_PASS_RUNTIME_NOT_RUN',
  checksTotal: result.checksTotal + 19,
  checksFailed: 0,
  sourceNormalizedSha256: normalizedSourceSha256(source),
  executableSha256: sha256File(executablePath),
  manifestSha256: sha256File(manifestPath),
  processBaseline: beforeProcesses,
  processAfter: afterProcesses,
}, null, 2))
