import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, realpathSync, statSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const attempt = resolve(process.argv[2] ?? '')
const attemptsRoot = resolve(repoRoot, 'data/native_model_requests/attempts')
assert.equal(basename(attempt).toLowerCase(), 'attempt-0001')
assert.equal(dirname(dirname(attempt)), attemptsRoot)
assert.match(basename(dirname(attempt)), /^TOPCOVER-REFERENCE-[A-Z0-9-]{8,120}$/)

const evidencePath = resolve(attempt,
  'evidence/private/topcover_right_reference_brep_capture.json')
const v37Path = resolve(attempt, 'capture_inputs/v37_right_side.sldprt')
const goldPath = resolve(attempt, 'capture_inputs/gold_right_side.sldprt')
const snapshotDir = resolve(attempt,
  'toolchain_snapshot/topcover_reference_brep_v1_505B819B')
const snapshotManifestPath = resolve(snapshotDir, 'snapshot_manifest.json')
const checks = []
const add = (name, pass, actual = undefined) => checks.push({
  name, pass: Boolean(pass), ...(actual === undefined ? {} : { actual }),
})
const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const near = (left, right, tolerance) =>
  Number.isFinite(left) && Number.isFinite(right) && Math.abs(left - right) <= tolerance
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
const round = (value) => Math.round(value * 1e6) / 1e6
function normalizedGeometryRows(rows, xShift, ignoreCylinderAxisCoordinate = false) {
  return rows.map((row) => ({
    centerMm: row.centerMm.map((value, index) => {
      if (ignoreCylinderAxisCoordinate && Math.abs(row.axis[index]) > 0.999) return 0
      return round(value - (index === 0 ? xShift : 0))
    }),
    axis: (() => {
      const values = row.axis.map(round)
      const first = values.find((value) => Math.abs(value) > 0.000001) ?? 1
      return first < 0 ? values.map((value) => -value) : values
    })(),
    radiusMm: round(row.radiusMm),
  })).sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)))
}
function sideGeometryGate(part, expectedBox) {
  return part.faceCount === 135 && part.edgeCount === 324 &&
    part.loopCount === 188 && part.loopEdgeReferenceCount === 648 &&
    part.cylinderFaceCount === 27 && part.circleEdgeReferences?.length === 108 &&
    part.cylinders?.length === 27 && part.externalReferences?.length === 6 &&
    part.featureTypeCounts?.SheetMetal === 1 && part.featureTypeCounts?.OneBend === 5 &&
    part.featureTypeCounts?.ProcessBends === 1 && part.featureIssues?.length === 0 &&
    part.bboxMm?.length === 6 && part.bboxMm.every((value, index) =>
      near(value, expectedBox[index], 0.00001)) &&
    near(part.volumeMm3, 71396.31748296, 0.00001) &&
    near(part.surfaceAreaMm2, 179242.42901206, 0.00001) &&
    near(part.massKg, 0.560461092241, 1e-9) &&
    /^[A-F0-9]{64}$/.test(part.geometrySignature)
}
function featureGate(part) {
  const sheet = part.sheetMetalFeatures.find((value) => value.type === 'SheetMetal')
  const process = part.sheetMetalFeatures.find((value) => value.type === 'ProcessBends')
  const bends = part.sheetMetalFeatures.filter((value) => value.type === 'OneBend')
    .sort((left, right) => left.order - right.order)
  return part.sheetMetalFeatures.length === 7 && sheet && process && bends.length === 5 &&
    near(sheet.thicknessMm, 0.8, 1e-6) && near(sheet.radiusMm, 0.2, 1e-6) &&
    near(sheet.kFactor, 0.5, 1e-6) && sheet.customAllowanceType === 4 &&
    near(sheet.customDeductionMm, 1.4, 1e-6) &&
    near(process.radiusMm, 0.2, 1e-6) && process.customAllowanceType === 4 &&
    bends.every((bend, index) => bend.order === index + 1 && bend.bendType === 1 &&
      near(bend.angleRadians, Math.PI / 2, 1e-9) &&
      near(bend.radiusMm, 0.5, 1e-6) && near(bend.kFactor, 0.5, 1e-6) &&
      bend.customAllowanceType === 4 && near(bend.customDeductionMm, 1.4, 1e-6)) &&
    bends[4].direction === 1 && bends[4].down === false &&
    bends.slice(0, 4).every((bend) => bend.direction === 2 && bend.down === true)
}

for (const path of [evidencePath, v37Path, goldPath, snapshotManifestPath])
  add(`single_link_no_reparse:${basename(path)}`, fileGate(path))

const evidenceBytes = readFileSync(evidencePath)
const evidence = JSON.parse(evidenceBytes.toString('utf8'))
const snapshotManifest = JSON.parse(readFileSync(snapshotManifestPath, 'utf8'))
const v37 = evidence.parts?.v37_760
const gold = evidence.parts?.gold_1000
add('capture_boundary',
  evidence.schema === 'winnsen.16029.topcover_right_reference_brep_capture.v1' &&
  evidence.status === 'TOPCOVER_RIGHT_REFERENCE_BREP_CAPTURE_COMPLETE' &&
  evidence.purpose === 'structure_engineering_assistance' &&
  evidence.probeOnly === true && evidence.consumable === false)
add('input_hashes_and_bytes_unchanged',
  fileHash(v37Path) === 'F01F8FDA28C4028C629BA3AE428B277CEE88B61D0D73145C46C17845A8CC2641' &&
  fileHash(goldPath) === '380F2D8DC419F8BDEC809319FC1AB14F0AD279038E50362AA8E1C7E7BA70A679' &&
  evidence.inputs?.v37_760?.beforeSha256 === fileHash(v37Path) &&
  evidence.inputs?.v37_760?.afterSha256 === fileHash(v37Path) &&
  evidence.inputs?.gold_1000?.beforeSha256 === fileHash(goldPath) &&
  evidence.inputs?.gold_1000?.afterSha256 === fileHash(goldPath))
add('protected_sources_still_unchanged',
  fileHash(resolve(repoRoot,
    'workers/generated_models/review_generation_requests/' +
    'v43-int-v37-760w-six-door-l642-r246-r1/native_cad/' +
    'final_native_hierarchical_pack_and_go/上盖壳体右侧板.SLDPRT')) ===
      'F01F8FDA28C4028C629BA3AE428B277CEE88B61D0D73145C46C17845A8CC2641' &&
  fileHash(resolve(repoRoot,
    'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/' +
    '1.工程图/上盖壳体右侧板.SLDPRT')) ===
      '380F2D8DC419F8BDEC809319FC1AB14F0AD279038E50362AA8E1C7E7BA70A679')

add('v37_full_geometry_contract', sideGeometryGate(v37,
  [320.8, 1839.2, -549, 380, 1917, -47]))
add('gold_full_geometry_contract', sideGeometryGate(gold,
  [440.8, 1839.2, -549, 500, 1917, -47]))
add('v37_sheet_metal_feature_contract', featureGate(v37))
add('gold_sheet_metal_feature_contract', featureGate(gold))
add('geometry_is_exact_x_translation_only',
  JSON.stringify(normalizedGeometryRows(v37.cylinders, 0, true)) ===
    JSON.stringify(normalizedGeometryRows(gold.cylinders, 120, true)) &&
  JSON.stringify(normalizedGeometryRows(v37.circleEdgeReferences, 0)) ===
    JSON.stringify(normalizedGeometryRows(gold.circleEdgeReferences, 120)) &&
  near(v37.volumeMm3, gold.volumeMm3, 0.0001) &&
  near(v37.surfaceAreaMm2, gold.surfaceAreaMm2, 0.0001))

const sessions = evidence.processAudit?.sessions ?? []
add('owned_read_only_sessions_closed', sessions.length === 2 &&
  sessions.every((session) => session.allExited === true &&
    session.sldworksExited === true && session.inputUnchanged === true &&
    session.sldworks?.executableSha256 ===
      '1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978' &&
    session.monitors.every((monitor) => monitor.exited === true &&
      monitor.parentPid === session.sldworks.pid &&
      monitor.executableSha256 ===
        'A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70')) &&
  evidence.processAudit?.errors?.length === 0)

const snapshotSource = resolve(snapshotDir, 'TopCoverRightReferenceBrep.cs')
const snapshotExe = resolve(snapshotDir, 'TopCoverRightReferenceBrep.exe')
const snapshotStatic = JSON.parse(readFileSync(resolve(snapshotDir,
  'static_verification.json'), 'utf8'))
add('frozen_toolchain_binding',
  snapshotManifest.schema ===
    'winnsen.16029.topcover_reference_brep_toolchain_snapshot.v1' &&
  snapshotManifest.evidenceSha256 === hash(evidenceBytes) &&
  snapshotManifest.sourceNormalizedSha256 === normalizedSourceHash(
    readFileSync(snapshotSource, 'utf8')) &&
  evidence.tool?.sourceNormalizedSha256 === snapshotManifest.sourceNormalizedSha256 &&
  evidence.tool?.executableSha256 === fileHash(snapshotExe) &&
  snapshotStatic.status === 'PASS' && snapshotStatic.cadStarted === false &&
  snapshotManifest.files?.length === 12 && snapshotManifest.files.every((entry) =>
    fileHash(resolve(snapshotDir, entry.name)) === entry.sha256))

const cad = execFileSync('powershell.exe', ['-NoProfile', '-Command',
  '@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue).Count'],
{ encoding: 'utf8', windowsHide: true }).trim()
add('cad_processes_zero_now', cad === '0', cad)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_right_reference_brep_capture_verification.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  evidenceSha256: hash(evidenceBytes),
  checks,
}
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
