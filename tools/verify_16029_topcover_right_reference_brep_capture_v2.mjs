import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, realpathSync, statSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const attempt = resolve(process.argv[2] ?? '')
assert.equal(basename(attempt).toLowerCase(), 'attempt-0001')
assert.match(basename(dirname(attempt)), /^TOPCOVER-REFERENCE-[A-Z0-9-]{8,120}$/)
const evidencePath = resolve(attempt,
  'evidence/private/topcover_right_reference_brep_capture_v2.json')
const v37Path = resolve(attempt, 'capture_inputs/v37_right_side.sldprt')
const goldPath = resolve(attempt, 'capture_inputs/gold_right_side.sldprt')
const snapshotDir = resolve(attempt,
  'toolchain_snapshot/topcover_reference_brep_v2_D13C2934')
const snapshotManifestPath = resolve(snapshotDir, 'snapshot_manifest.json')
const checks = []
const add = (name, pass, actual = undefined) => checks.push({
  name, pass: Boolean(pass), ...(actual === undefined ? {} : { actual }),
})
const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const near = (left, right, tolerance) =>
  Number.isFinite(left) && Number.isFinite(right) && Math.abs(left - right) <= tolerance
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
  const pattern = /(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/g
  assert.equal([...normalized.matchAll(pattern)].length, 1)
  return hash(Buffer.from(normalized.replace(pattern, '$1__SOURCE_SHA256__$2'), 'utf8'))
}
function axisCanonical(axis) {
  const length = Math.hypot(...axis)
  let values = axis.map((value) => round(value / length))
  const first = values.find((value) => Math.abs(value) > 0.000001) ?? 1
  if (first < 0) values = values.map((value) => -value)
  return values
}
function pointKey(point, xShift) {
  return !Array.isArray(point) || point.length !== 3 ? [] :
    [round(point[0] - xShift), round(point[1]), round(point[2])]
}
function cylinderKey(parameters, xShift) {
  if (!Array.isArray(parameters) || parameters.length < 7) return []
  const axis = axisCanonical(parameters.slice(3, 6))
  const center = [parameters[0] * 1000 - xShift, parameters[1] * 1000,
    parameters[2] * 1000]
  const along = center.reduce((sum, value, index) => sum + value * axis[index], 0)
  return {
    perpendicular: center.map((value, index) => round(value - along * axis[index])),
    axis,
    radiusMm: round(parameters[6] * 1000),
  }
}
function edgeKey(edge, xShift) {
  const points = [pointKey(edge.startPointMm, xShift),
    pointKey(edge.endPointMm, xShift)].sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b)))
  const vertices = [pointKey(edge.startVertexPointMm, xShift),
    pointKey(edge.endVertexPointMm, xShift)].sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b)))
  return JSON.stringify({
    type: edge.curveType,
    line: edge.isLine,
    circle: edge.isCircle,
    lengthMm: round(edge.lengthMm),
    points,
    vertices,
    circleParams: cylinderKey(edge.circleParams, xShift),
  })
}
function faceKey(face, xShift) {
  const loops = face.loops.map((loop) => ({
    outer: loop.isOuter,
    edges: loop.edges.map((edge) => edgeKey(edge, xShift)).sort(),
  })).sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)))
  return JSON.stringify({
    areaMm2: round(face.areaMm2),
    normal: axisCanonical(face.normal.some((value) => Math.abs(value) > 1e-9)
      ? face.normal : [1, 0, 0]),
    surfaceIdentity: face.surfaceIdentity,
    plane: face.isPlane,
    cylinder: cylinderKey(face.cylinderParams, xShift),
    loops,
  })
}
function detailedCanonical(part, xShift) {
  return part.faces.map((face) => faceKey(face, xShift)).sort()
}
function finiteArray(value) {
  return Array.isArray(value) && value.every(Number.isFinite)
}
function detailedComplete(part) {
  if (part.faces?.length !== 135) return false
  let loops = 0
  let edges = 0
  for (const face of part.faces) {
    if (!Number.isFinite(face.areaMm2) || !finiteArray(face.boxMm) ||
        face.boxMm.length !== 6 || !finiteArray(face.normal) || face.normal.length !== 3 ||
        !finiteArray(face.planeParams) || !finiteArray(face.cylinderParams)) return false
    loops += face.loops.length
    for (const loop of face.loops) {
      if (loop.edgeCount !== loop.edges.length) return false
      edges += loop.edges.length
      for (const edge of loop.edges) {
        if (!Number.isFinite(edge.uMin) || !Number.isFinite(edge.uMax) ||
            !Number.isFinite(edge.lengthMm) || edge.lengthMm <= 0 ||
            !finiteArray(edge.startPointMm) || edge.startPointMm.length !== 3 ||
            !finiteArray(edge.endPointMm) || edge.endPointMm.length !== 3 ||
            !finiteArray(edge.startVertexPointMm) || !finiteArray(edge.endVertexPointMm) ||
            !finiteArray(edge.lineParams) || !finiteArray(edge.circleParams)) return false
      }
    }
  }
  return loops === 188 && edges === 648 &&
    part.loopCount === loops && part.loopEdgeReferenceCount === edges
}

for (const path of [evidencePath, v37Path, goldPath, snapshotManifestPath])
  add(`single_link_no_reparse:${basename(path)}`, fileGate(path))
const evidenceBytes = readFileSync(evidencePath)
const evidence = JSON.parse(evidenceBytes.toString('utf8'))
const v37 = evidence.parts?.v37_760
const gold = evidence.parts?.gold_1000
const snapshotManifest = JSON.parse(readFileSync(snapshotManifestPath, 'utf8'))
add('capture_boundary',
  evidence.schema === 'winnsen.16029.topcover_right_reference_brep_capture.v2' &&
  evidence.status === 'TOPCOVER_RIGHT_REFERENCE_BREP_CAPTURE_V2_COMPLETE' &&
  evidence.probeOnly === true && evidence.consumable === false &&
  evidence.purpose === 'structure_engineering_assistance')
add('inputs_unchanged',
  fileHash(v37Path) === 'F01F8FDA28C4028C629BA3AE428B277CEE88B61D0D73145C46C17845A8CC2641' &&
  fileHash(goldPath) === '380F2D8DC419F8BDEC809319FC1AB14F0AD279038E50362AA8E1C7E7BA70A679' &&
  evidence.inputs.v37_760.beforeSha256 === evidence.inputs.v37_760.afterSha256 &&
  evidence.inputs.gold_1000.beforeSha256 === evidence.inputs.gold_1000.afterSha256)
add('detailed_v37_complete', detailedComplete(v37))
add('detailed_gold_complete', detailedComplete(gold))
const v37Canonical = detailedCanonical(v37, 0)
const goldCanonical = detailedCanonical(gold, 120)
add('independent_translation_normalized_geometry_equal',
  JSON.stringify(v37Canonical) === JSON.stringify(goldCanonical))
add('captured_translation_signatures_equal',
  v37.translationNormalizedDetailedGeometrySignature ===
    gold.translationNormalizedDetailedGeometrySignature &&
  v37.translationNormalizedDetailedGeometrySignature ===
    '05461DA22A685F5995093CB2A8CC5D9C87E118284C5F7234F71EDFE2714FE2FB')
add('topology_and_sheet_metal_counts',
  [v37, gold].every((part) => part.faceCount === 135 && part.edgeCount === 324 &&
    part.loopCount === 188 && part.loopEdgeReferenceCount === 648 &&
    part.cylinderFaceCount === 27 && part.circleEdgeReferences.length === 108 &&
    part.featureTypeCounts.OneBend === 5 && part.externalReferences.length === 6 &&
    part.featureIssues.length === 0))
add('owned_sessions_closed', evidence.processAudit.sessions?.length === 2 &&
  evidence.processAudit.sessions.every((session) => session.allExited === true &&
    session.inputUnchanged === true) && evidence.processAudit.errors.length === 0)

const snapshotSource = resolve(snapshotDir, 'TopCoverRightReferenceBrep.cs')
const snapshotExe = resolve(snapshotDir, 'TopCoverRightReferenceBrep.exe')
const snapshotStatic = JSON.parse(readFileSync(resolve(snapshotDir,
  'static_verification.json'), 'utf8'))
add('frozen_toolchain_binding',
  snapshotManifest.schema ===
    'winnsen.16029.topcover_reference_brep_toolchain_snapshot.v2' &&
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
{ encoding: 'utf8', windowsHide: true }).trim()
add('cad_processes_zero_now', cad === '0', cad)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_right_reference_brep_capture_v2_verification.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  evidenceSha256: hash(evidenceBytes),
  independentCanonicalSha256: hash(Buffer.from(JSON.stringify(v37Canonical))),
  checks,
}
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
