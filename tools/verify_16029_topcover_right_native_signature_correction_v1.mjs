import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync } from 'node:fs'
import { isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(fileURLToPath(new URL('..', import.meta.url)))
const correctionPath = resolve(process.argv[2] ?? '')
const correction = JSON.parse(readFileSync(correctionPath, 'utf8'))
const checks = []
const add = (name, pass, actual = undefined) => checks.push({
  name, pass: Boolean(pass), ...(actual === undefined ? {} : { actual }),
})
const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const near = (left, right, tolerance = 1e-6) =>
  Number.isFinite(left) && Number.isFinite(right) && Math.abs(left - right) <= tolerance
const exactKeys = (value, expected) => value && typeof value === 'object' &&
  !Array.isArray(value) && JSON.stringify(Object.keys(value).sort()) ===
    JSON.stringify([...expected].sort())

function cadSnapshot() {
  const raw = execFileSync('powershell.exe', ['-NoProfile', '-Command',
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | " +
    "Sort-Object ProcessName,Id | ForEach-Object { \"$($_.ProcessName):$($_.Id)\" }) -join ','"],
  { encoding: 'utf8', windowsHide: true }).trim()
  return raw === '' ? [] : raw.split(',')
}

function roundDetailed(value) {
  let rounded = Math.round(value * 1e6) / 1e6
  if (Object.is(rounded, -0) || Math.abs(rounded) < 0.0000005) rounded = 0
  return rounded.toFixed(6).replace(/\.?0+$/, '')
}

function canonicalAxis(raw) {
  if (!Array.isArray(raw) || raw.length !== 3 || raw.some((value) =>
    !Number.isFinite(value))) return []
  const length = Math.sqrt(raw.reduce((sum, value) => sum + value * value, 0))
  if (!Number.isFinite(length) || length < 1e-12) return []
  let axis = raw.map((value) => value / length)
  if (axis.find((value) => Math.abs(value) > 1e-12) < 0)
    axis = axis.map((value) => -value)
  return axis
}

function pointKey(point, anchor) {
  if (!Array.isArray(point) || point.length !== 3) return '[]'
  return point.map((value, index) => roundDetailed(value - anchor[index])).join(',')
}

function cylinderKey(parameters, anchor) {
  if (!Array.isArray(parameters) || parameters.length < 7) return '[]'
  const axis = canonicalAxis(parameters.slice(3, 6))
  const center = parameters.slice(0, 3).map((value, index) =>
    value * 1000 - anchor[index])
  const parallel = center.reduce((sum, value, index) =>
    sum + value * axis[index], 0)
  const perpendicular = center.map((value, index) => value - parallel * axis[index])
  return `${perpendicular.map(roundDetailed).join(',')};a=${axis.map(roundDetailed).join(',')};r=${roundDetailed(parameters[6] * 1000)}`
}

function samePoint(left, right) {
  return Array.isArray(left) && Array.isArray(right) && left.length === 3 &&
    right.length === 3 && left.every((value, index) =>
      near(value, right[index], 1e-6))
}

function edgeKey(edge, anchor) {
  const fullCircle = edge.isCircle === true &&
    (samePoint(edge.startPointMm, edge.endPointMm) ||
      edge.startVertexPointMm?.length !== 3 || edge.endVertexPointMm?.length !== 3)
  let points = 'FULL'
  let vertices = 'FULL'
  if (!fullCircle) {
    points = [pointKey(edge.startPointMm, anchor),
      pointKey(edge.endPointMm, anchor)].sort().join('>')
    vertices = [pointKey(edge.startVertexPointMm, anchor),
      pointKey(edge.endVertexPointMm, anchor)].sort().join('>')
  }
  return `t=${edge.curveType},l=${edge.isLine ? '1' : '0'},c=${edge.isCircle ? '1' : '0'},len=${roundDetailed(edge.lengthMm)},p=${points},v=${vertices},cp=${cylinderKey(edge.circleParams, anchor)}`
}

function faceKeys(geometry) {
  const box = geometry.bodyBoxMm ?? geometry.bboxMm
  const anchor = box.slice(0, 3)
  return geometry.faces.map((face) => {
    const loops = face.loops.map((loop) =>
      `${loop.isOuter ? 'O' : 'I'}:${loop.edges.map((edge) =>
        edgeKey(edge, anchor)).sort().join(',')}`).sort()
    return `n=${canonicalAxis(face.normal).map(roundDetailed).join(',')};surface=${face.surfaceIdentity},plane=${face.isPlane ? '1' : '0'},cylinder=${cylinderKey(face.cylinderParams, anchor)};loops=${loops.join('/')}`
  }).sort()
}

function invariantSignature(geometry) {
  return hash(Buffer.from(`faces=${faceKeys(geometry).join('|')}`, 'utf8'))
}

function resolveBoundInput(row, expectedHash) {
  if (!exactKeys(row, ['path', 'sha256']) || row.sha256 !== expectedHash) return null
  const path = resolve(repoRoot, row.path)
  const rel = relative(repoRoot, path)
  if (!existsSync(path) || !statSync(path).isFile() || Number(statSync(path).nlink) !== 1 ||
      rel.startsWith('..') || isAbsolute(rel) || fileHash(path) !== expectedHash) return null
  return path
}

const cadBefore = cadSnapshot()
add('cad_zero_before_verification', cadBefore.length === 0, cadBefore)

const correctionRelative = relative(repoRoot, correctionPath)
add('correction_is_regular_single_link_repository_file', existsSync(correctionPath) &&
  statSync(correctionPath).isFile() && Number(statSync(correctionPath).nlink) === 1 &&
  !correctionRelative.startsWith('..') && !isAbsolute(correctionRelative))

add('schema_purpose_state_and_exact_keys', exactKeys(correction, [
  'schema', 'generatedAtUtc', 'purpose', 'inputs', 'finding', 'algorithm',
  'expectedSignatures', 'proof', 'cadStarted', 'nativeCadCreated', 'qualityBoundary',
]) && correction.schema ===
  'winnsen.16029.topcover_right_native_signature_correction.v1' &&
  correction.purpose === 'structure_engineering_assistance' &&
  correction.cadStarted === false && correction.nativeCadCreated === false &&
  typeof correction.qualityBoundary === 'string' && correction.qualityBoundary.length > 40)

const expectedInputHashes = {
  ruleV2: 'EFAB0AE94FC50C0ECC05CA2384D24A93DE3E276918BAAB68CA96EFADBCAEF1F0',
  referenceV5: 'BAEEF8AB4790F4650785605FC3C4A452753755558A931B9275977A1E88C32A98',
  failedNativeR2: 'CFF233764801E14CD35FDB050C346CF7A1C58E2B2E3448A8C7D6B977E0FDC6AC',
}
add('input_keys_are_exact', exactKeys(correction.inputs,
  Object.keys(expectedInputHashes)))
const inputPaths = {}
for (const [name, expectedHash] of Object.entries(expectedInputHashes)) {
  inputPaths[name] = resolveBoundInput(correction.inputs?.[name], expectedHash)
  add(`live_input_hash:${name}`, inputPaths[name] !== null)
}

add('finding_identifies_only_parameterization_noise', exactKeys(correction.finding, [
  'legacyFlatReferenceSignature', 'legacyFlatNativeR2Signature',
  'physicalGeometryExact', 'mismatchCause',
]) && correction.finding.legacyFlatReferenceSignature ===
  'D4D4F1E70FC1D94A16BCA7B41AB7CD0E353CA9039E1F826CA0AC640040CBD5FB' &&
  correction.finding.legacyFlatNativeR2Signature ===
  'DB8F1F935092F08F7E1515175DC5B29FE4B0BFB9CF1E315F22538780735E9C98' &&
  correction.finding.physicalGeometryExact === true &&
  JSON.stringify(correction.finding.mismatchCause) ===
    JSON.stringify(['edge_endpoint_orientation', 'full_circle_parameter_seam']))

add('algorithm_contract_is_exact', exactKeys(correction.algorithm, [
  'id', 'translationAnchor', 'roundingDecimals',
  'roundingMode',
  'edgeEndpointOrientationInvariant', 'fullCircleSeamInvariant',
  'faceNormalSignInvariant', 'cylinderAxisSignInvariant', 'booleanEncoding',
  'payloadPrefix',
]) && correction.algorithm.id ===
  'translation_normalized_brep_orientation_and_full_circle_seam_invariant_v1' &&
  correction.algorithm.translationAnchor === 'body_bbox_min_xyz' &&
  correction.algorithm.roundingDecimals === 6 &&
  correction.algorithm.roundingMode === 'nearest_ties_toward_positive_infinity' &&
  correction.algorithm.edgeEndpointOrientationInvariant === true &&
  correction.algorithm.fullCircleSeamInvariant === true &&
  correction.algorithm.faceNormalSignInvariant === true &&
  correction.algorithm.cylinderAxisSignInvariant === true &&
  correction.algorithm.booleanEncoding === '1_or_0' &&
  correction.algorithm.payloadPrefix === 'faces=')

const reference = JSON.parse(readFileSync(inputPaths.referenceV5, 'utf8'))
const nativeR2 = JSON.parse(readFileSync(inputPaths.failedNativeR2, 'utf8'))
const v37Flat = reference.parts.v37_760.flat
const v37Folded = reference.parts.v37_760.folded
const goldFlat = reference.parts.gold_1000.flat
const goldFolded = reference.parts.gold_1000.folded
const nativeFlat = nativeR2.flatGeometryDiagnostic
const flatSignature = invariantSignature(v37Flat)
const foldedSignature = invariantSignature(v37Folded)

add('failed_r2_is_rolled_back_with_zero_cad',
  nativeR2.status === 'TOPCOVER_RIGHT_760_NATIVE_PILOT_FAILED' &&
  nativeR2.failure === 'ContractException:NATIVE_MANUFACTURING_FLAT_REGRESSION_FAILED' &&
  nativeR2.cadZeroAfterFailure === true && nativeR2.outputRolledBack === true &&
  nativeR2.receiptRolledBack === true && nativeR2.evidenceRolledBack === true)

add('native_r2_flat_physical_metrics_and_topology_are_exact',
  nativeFlat.bodyCount === 1 &&
  nativeFlat.bodyBoxMm?.length === 6 &&
  nativeFlat.bodyBoxMm.every((value, index) => near(value,
    [299.8, 1912.2, -549, 480.6, 1917, -47][index], 1e-6)) &&
  near(nativeFlat.volumeMm3, 70990.25576414, 0.0001) &&
  near(nativeFlat.surfaceAreaMm2, 178225.56498146, 0.0001) &&
  near(nativeFlat.massKg, 0.5572735077485, 1e-10) &&
  nativeFlat.faceCount === 95 && nativeFlat.edgeCount === 244 &&
  nativeFlat.loopCount === 148 && nativeFlat.loopEdgeReferenceCount === 488 &&
  nativeFlat.cylinderFaceCount === 17 && nativeFlat.circleEdgeReferenceCount === 68)

add('flat_reference_translation_and_native_equivalence_is_exact',
  flatSignature === invariantSignature(goldFlat) &&
  flatSignature === invariantSignature(nativeFlat) &&
  JSON.stringify(faceKeys(v37Flat)) === JSON.stringify(faceKeys(goldFlat)) &&
  JSON.stringify(faceKeys(v37Flat)) === JSON.stringify(faceKeys(nativeFlat)) &&
  faceKeys(nativeFlat).length === 95)

add('folded_reference_translation_equivalence_is_exact',
  foldedSignature === invariantSignature(goldFolded) &&
  JSON.stringify(faceKeys(v37Folded)) === JSON.stringify(faceKeys(goldFolded)) &&
  faceKeys(v37Folded).length === 135)

add('expected_signatures_are_independently_recomputed',
  exactKeys(correction.expectedSignatures,
    ['manufacturingFlat760', 'folded760']) &&
  correction.expectedSignatures.manufacturingFlat760 === flatSignature &&
  correction.expectedSignatures.folded760 === foldedSignature &&
  flatSignature === 'DECF4787EFF44B05A148527A7A512ED27D2539182D6A8909D9C28E8E6B2CC0B6' &&
  foldedSignature === '4698418C907999F50B193BDC3C6F5E5DC2C6DED8E8D270C2E05E4D6C8CBBFECF')

add('proof_block_is_exact', exactKeys(correction.proof, [
  'v37FlatEqualsGoldFlat', 'v37FoldedEqualsGoldFolded',
  'nativeR2FlatEqualsReference', 'flatFaceKeysMatched', 'flatFaceKeysTotal',
  'nativeR2PhysicalMetricsExact',
]) && Object.values(correction.proof).every((value) => value === true || value === 95) &&
  correction.proof.flatFaceKeysMatched === 95 &&
  correction.proof.flatFaceKeysTotal === 95)

const cadAfter = cadSnapshot()
add('cad_zero_after_verification', cadAfter.length === 0, cadAfter)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_right_native_signature_correction_verification.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  correctionSha256: fileHash(correctionPath),
  expectedFlatSignature: flatSignature,
  expectedFoldedSignature: foldedSignature,
  cadStarted: false,
  checks,
}
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
