import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync } from 'node:fs'
import { isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(fileURLToPath(new URL('..', import.meta.url)))
const path = resolve(process.argv[2] ?? '')
const doc = JSON.parse(readFileSync(path, 'utf8'))
const checks = []
const add = (name, pass) => checks.push({ name, pass: Boolean(pass) })
const sha = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileSha = (value) => sha(readFileSync(value))
const near = (left, right, tolerance) => Math.abs(left - right) <= tolerance

add('output_is_regular_repository_file', existsSync(path) && statSync(path).isFile() &&
  Number(statSync(path).nlink) === 1 && !relative(repoRoot, path).startsWith('..') &&
  !isAbsolute(relative(repoRoot, path)))
add('schema_purpose_boundary',
  doc.schema === 'winnsen.16029.topcover_side_native_readiness_correction.v1' &&
  doc.purpose === 'structure_engineering_assistance' &&
  doc.sidePartsNativeBuildReady === false &&
  /no CAD was opened/.test(doc.qualityBoundary))
add('supersedes_only_two_readiness_fields',
  JSON.stringify(doc.supersedesReadinessOnly?.fields) ===
    JSON.stringify(['parts[0].targetBuildReady', 'parts[1].targetBuildReady']))

for (const [name, row] of Object.entries({
  ruleV1: doc.supersedesReadinessOnly,
  physicalCapture: doc.inputs?.physicalCapture,
  goldDxfCapture: doc.inputs?.goldDxfCapture,
})) {
  const inputPath = resolve(repoRoot, row?.path ?? '')
  add(`live_input_hash:${name}`, existsSync(inputPath) && fileSha(inputPath) === row.sha256)
}

add('exact_two_side_records', doc.sides?.length === 2 &&
  doc.sides[0].name === '上盖壳体左侧板.sldprt' &&
  doc.sides[1].name === '上盖壳体右侧板.SLDPRT')
for (const side of doc.sides ?? []) {
  add(`readiness_false:${side.name}`, side.v1TargetBuildReady === true &&
    side.correctedTargetBuildReady === false &&
    side.correctionReasons?.length === 5 && side.nextRequiredEvidence?.length === 4)
  add(`dxf_structure:${side.name}`, side.dxf?.lineComponentCount === 5 &&
    side.dxf.lineComponents?.[0]?.lineCount === 22 &&
    side.dxf.lineComponents.slice(1).every((value) => value.lineCount === 12) &&
    side.dxf.circleCount === 14 && side.dxf.uniqueCenterCount === 11 &&
    side.dxf.concentricCenters?.length === 3 &&
    side.dxf.needsLoopReconstruction === true)
  add(`formed_volume_gap:${side.name}`,
    near(side.dxf.lineComponents[0].areaMm2, 88915.94, 0.000001) &&
    near(side.pureOuterProfileVolumeMm3, 71132.752, 0.000001) &&
    near(side.referenceMinusPureOuterProfileVolumeMm3, 263.56548296, 0.00001) &&
    side.reference760.bodyCount === 1 && side.reference760.faceCount === 135 &&
    side.reference760.edgeCount === 324)
}
add('left_formed_features_and_right_mirror_are_distinguished',
  doc.sides?.[0]?.formedFeatureEvidence?.splitBodyCount === 1 &&
  doc.sides[0].formedFeatureEvidence.cutCount === 1 &&
  doc.sides[0].formedFeatureEvidence.iceCount === 2 &&
  doc.sides[0].formedFeatureEvidence.sketchBlockInstanceCount === 10 &&
  doc.sides?.[1]?.formedFeatureEvidence?.mirrorStockCount === 1)
add('width_relation_preserved_but_unresolved_remains',
  doc.provenWidthRelationRetained === true && doc.unresolved?.length === 3)

const passed = checks.filter((value) => value.pass).length
const result = {
  schema: 'winnsen.16029.topcover_side_native_readiness_correction_verification.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  documentSha256: fileSha(path),
  checks,
}
process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (result.status !== 'PASS') process.exit(1)
