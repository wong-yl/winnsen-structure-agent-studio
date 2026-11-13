import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, realpathSync, statSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const attempt = resolve(process.argv[2] ?? '')
const evidencePath = resolve(attempt,
  'evidence/private/topcover_right_reference_brep_capture_v5.json')
const v37Path = resolve(attempt, 'capture_inputs/v37_right_side.sldprt')
const goldPath = resolve(attempt, 'capture_inputs/gold_right_side.sldprt')
const snapshotDir = resolve(attempt,
  'toolchain_snapshot/topcover_reference_brep_v5_AAAB1806')
const snapshotManifestPath = resolve(snapshotDir, 'snapshot_manifest.json')
const checks = []
const add = (name, pass, actual = undefined) => checks.push({
  name, pass: Boolean(pass), ...(actual === undefined ? {} : { actual }),
})
const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const near = (left, right, tolerance) => Number.isFinite(left) &&
  Number.isFinite(right) && Math.abs(left - right) <= tolerance
const round = (value) => Math.round(value * 1e6) / 1e6
const under = (candidate, root) => {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}
function fileGate(path) {
  if (!existsSync(path) || !under(path, attempt)) return false
  const info = statSync(path)
  return info.isFile() && Number(info.nlink) === 1 &&
    !lstatSync(path).isSymbolicLink() &&
    under(realpathSync.native(path), realpathSync.native(attempt))
}
function normalizedSourceHash(text) {
  const normalized = text.replace(/\r\n?/g, '\n')
  return hash(Buffer.from(normalized.replace(
    /(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/,
    '$1__SOURCE_SHA256__$2'), 'utf8'))
}
function detailedCountGate(part, expected) {
  if (part.faces?.length !== expected.faces) return false
  let loops = 0
  let edgeRefs = 0
  for (const face of part.faces) {
    if (!Number.isFinite(face.areaMm2) || face.boxMm?.length !== 6 ||
        !face.boxMm.every(Number.isFinite) || face.normal?.length !== 3 ||
        !face.normal.every(Number.isFinite)) return false
    loops += face.loops.length
    for (const loop of face.loops) {
      if (loop.edgeCount !== loop.edges.length) return false
      edgeRefs += loop.edges.length
      if (!loop.edges.every((edge) => Number.isFinite(edge.lengthMm) &&
        edge.lengthMm > 0 && edge.startPointMm?.length === 3 &&
        edge.endPointMm?.length === 3)) return false
    }
  }
  return part.faceCount === expected.faces && part.edgeCount === expected.edges &&
    loops === expected.loops && edgeRefs === expected.edgeRefs &&
    part.loopCount === loops && part.loopEdgeReferenceCount === edgeRefs &&
    part.cylinderFaceCount === expected.cylinders &&
    part.circleEdgeReferences.length === expected.circleRefs
}
function normalizedEdgeCloud(part, xShift) {
  const rows = []
  for (const face of part.faces) for (const loop of face.loops)
    for (const edge of loop.edges) {
      const points = [edge.startPointMm, edge.endPointMm].map((point) =>
        [round(point[0] - xShift), round(point[1]), round(point[2])])
        .sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)))
      rows.push(JSON.stringify({ type: edge.curveType, line: edge.isLine,
        circle: edge.isCircle, length: round(edge.lengthMm), points }))
    }
  return rows.sort()
}

for (const path of [evidencePath, v37Path, goldPath, snapshotManifestPath])
  add(`single_link_no_reparse:${basename(path)}`, fileGate(path))
const evidenceBytes = readFileSync(evidencePath)
const evidence = JSON.parse(evidenceBytes.toString('utf8'))
const v37 = evidence.parts.v37_760
const gold = evidence.parts.gold_1000
const snapshotManifest = JSON.parse(readFileSync(snapshotManifestPath, 'utf8'))
add('capture_boundary',
  evidence.schema === 'winnsen.16029.topcover_right_reference_brep_capture.v5' &&
  evidence.status === 'TOPCOVER_RIGHT_REFERENCE_BREP_CAPTURE_V5_COMPLETE' &&
  evidence.purpose === 'structure_engineering_assistance' &&
  evidence.probeOnly === true && evidence.consumable === false &&
  /manufacturing flat body/.test(evidence.qualityBoundary) &&
  /不能当作完整侧板/.test(evidence.qualityBoundary))
add('inputs_unchanged',
  fileHash(v37Path) === 'F01F8FDA28C4028C629BA3AE428B277CEE88B61D0D73145C46C17845A8CC2641' &&
  fileHash(goldPath) === '380F2D8DC419F8BDEC809319FC1AB14F0AD279038E50362AA8E1C7E7BA70A679' &&
  evidence.inputs.v37_760.beforeSha256 === evidence.inputs.v37_760.afterSha256 &&
  evidence.inputs.gold_1000.beforeSha256 === evidence.inputs.gold_1000.afterSha256)
add('folded_detailed_contracts',
  detailedCountGate(v37.folded, { faces:135, edges:324, loops:188,
    edgeRefs:648, cylinders:27, circleRefs:108 }) &&
  detailedCountGate(gold.folded, { faces:135, edges:324, loops:188,
    edgeRefs:648, cylinders:27, circleRefs:108 }))
add('manufacturing_flat_detailed_contracts',
  detailedCountGate(v37.flat, { faces:95, edges:244, loops:148,
    edgeRefs:488, cylinders:17, circleRefs:68 }) &&
  detailedCountGate(gold.flat, { faces:95, edges:244, loops:148,
    edgeRefs:488, cylinders:17, circleRefs:68 }))
add('flat_geometry_and_delta_exact',
  v37.flat.bboxMm.every((value, index) => near(value,
    [299.8,1912.2,-549,480.6,1917,-47][index], 0.00001)) &&
  gold.flat.bboxMm.every((value, index) => near(value,
    [419.8,1912.2,-549,600.6,1917,-47][index], 0.00001)) &&
  near(v37.flat.volumeMm3, 70990.25576414, 0.0001) &&
  near(gold.flat.volumeMm3, 70990.25576414, 0.0001) &&
  near(v37.foldedMinusFlatVolumeMm3, 406.06171882, 0.0001) &&
  near(gold.foldedMinusFlatVolumeMm3, 406.06171882, 0.0001) &&
  v37.massConserved === false && v37.volumeConserved === false)
add('flat_and_folded_translation_signatures',
  v37.folded.translationNormalizedDetailedGeometrySignature ===
    gold.folded.translationNormalizedDetailedGeometrySignature &&
  v37.flat.translationNormalizedDetailedGeometrySignature ===
    gold.flat.translationNormalizedDetailedGeometrySignature &&
  v37.folded.translationNormalizedDetailedGeometrySignature ===
    '05461DA22A685F5995093CB2A8CC5D9C87E118284C5F7234F71EDFE2714FE2FB' &&
  v37.flat.translationNormalizedDetailedGeometrySignature ===
    'D4D4F1E70FC1D94A16BCA7B41AB7CD0E353CA9039E1F826CA0AC640040CBD5FB')
add('independent_flat_edge_translation_equal',
  JSON.stringify(normalizedEdgeCloud(v37.flat, 0)) ===
    JSON.stringify(normalizedEdgeCloud(gold.flat, 120)))
add('flatpattern_state_restored', [v37, gold].every((pair) =>
  pair.flatPatternOriginallySuppressed === true && pair.unsuppressReturn === true &&
  pair.flatPatternUnsuppressedAfterRebuild === true &&
  pair.restoreSuppressReturn === true && pair.flatPatternSuppressedAfterRestore === true &&
  pair.foldedRestoreRebuild === true &&
  pair.restoreDetailedSignature ===
    pair.folded.translationNormalizedDetailedGeometrySignature &&
  pair.restoreLightweightSignature === pair.folded.geometrySignature))
add('owned_sessions_closed', evidence.processAudit.sessions.length === 2 &&
  evidence.processAudit.sessions.every((session) => session.allExited === true &&
    session.inputUnchanged === true) && evidence.processAudit.errors.length === 0)

const snapshotSource = resolve(snapshotDir, 'TopCoverRightReferenceBrep.cs')
const snapshotExe = resolve(snapshotDir, 'TopCoverRightReferenceBrep.exe')
const snapshotStatic = JSON.parse(readFileSync(resolve(snapshotDir,
  'static_verification.json'), 'utf8'))
add('frozen_toolchain_binding',
  snapshotManifest.schema ===
    'winnsen.16029.topcover_reference_brep_toolchain_snapshot.v5' &&
  snapshotManifest.evidenceSha256 === hash(evidenceBytes) &&
  snapshotManifest.sourceNormalizedSha256 === normalizedSourceHash(
    readFileSync(snapshotSource, 'utf8')) &&
  evidence.tool.sourceNormalizedSha256 === snapshotManifest.sourceNormalizedSha256 &&
  evidence.tool.executableSha256 === fileHash(snapshotExe) &&
  snapshotStatic.status === 'PASS' && snapshotStatic.failed === 0 &&
  snapshotManifest.files.length === 12 && snapshotManifest.files.every((entry) =>
    fileHash(resolve(snapshotDir, entry.name)) === entry.sha256))
const cad = execFileSync('powershell.exe', ['-NoProfile', '-Command',
  '@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue).Count'],
{ encoding:'utf8', windowsHide:true }).trim()
add('cad_zero_now', cad === '0', cad)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_right_reference_brep_capture_v5_verification.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  evidenceSha256: hash(evidenceBytes),
  checks,
}
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
