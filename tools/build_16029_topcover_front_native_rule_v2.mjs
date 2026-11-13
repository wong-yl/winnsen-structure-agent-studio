import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import {
  closeSync,
  existsSync,
  fsyncSync,
  openSync,
  readFileSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { resolve } from 'node:path'

const root = resolve('.')
const physicalAttempt = resolve(
  'data/native_model_requests/attempts/NATIVE-20260824T092510-888W14D-F43C/attempt-0001')
const masterAttempt = resolve(
  'data/native_model_requests/attempts/NATIVE-20260824T094619-888W14D-1CDD/attempt-0001')
const brepAttempt = resolve(
  'data/native_model_requests/attempts/NATIVE-20260824T101117-888W14D-3E50/attempt-0001')
const outDir = resolve(brepAttempt, 'evidence/private')
const FRONT = '上盖壳体前侧板.sldprt'
const GOLD_WIDTH = 1000
const V37_WIDTH = 760
const TARGET_WIDTH = 888
const PITCH_MM = 140
const RIGHT_MARGIN_MM = 80
const MIN_LEFT_MARGIN_MM = 80
const THICKNESS_MM = 0.8

const sha256 = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const sha256File = (path) => sha256(readFileSync(path))
const json = (path) => JSON.parse(readFileSync(path, 'utf8'))
const round = (value, digits = 6) => Number(Number(value).toFixed(digits))
const near = (left, right, tolerance = 0.001) => Math.abs(Number(left) - Number(right)) <= tolerance
const mm = (value) => round(Number(value) * 1000, 6)
const sortNumber = (left, right) => left - right
const pointKey = (point) => point.map((value) => round(value, 6)).join(',')

const physicalPath = resolve(physicalAttempt,
  'evidence/private/topcover_native_rebuild_capture_probe_only.json')
const dxfPath = resolve(physicalAttempt,
  'evidence/private/topcover_native_rebuild_gold_dxf_capture.json')
const bundlePath = resolve(physicalAttempt,
  'evidence/private/topcover_native_rebuild_capture_bundle.json')
const masterPath = resolve(masterAttempt,
  'evidence/private/topcover_native_master_reference_capture_probe_only.json')
const ruleV1Path = resolve(masterAttempt,
  'evidence/private/topcover_native_rebuild_rules_v1.json')
const brepPath = resolve(brepAttempt,
  'evidence/private/topcover_native_front_brep_capture_probe_only.json')
const cleanupPath = resolve(brepAttempt,
  'evidence/private/topcover_native_front_brep_capture_probe_cleanup.json')
for (const path of [physicalPath, dxfPath, bundlePath, masterPath, ruleV1Path, brepPath,
  cleanupPath]) assert.equal(existsSync(path), true, `missing input: ${path}`)

const physical = json(physicalPath)
const dxf = json(dxfPath)
const bundle = json(bundlePath)
const master = json(masterPath)
const ruleV1 = json(ruleV1Path)
const brep = json(brepPath)
const cleanup = json(cleanupPath)
assert.equal(physical.datasetComplete, true)
assert.equal(dxf.capturePass, true)
assert.equal(bundle.gates?.cadDatasetComplete, true)
assert.equal(master.datasetComplete, true)
assert.equal(ruleV1.ruleExtractionPass, true)
assert.equal(brep.datasetComplete, true)
assert.equal(cleanup.cleanupPass, true)

const v37Part = physical.v37Parts.find((value) => value.name === FRONT)
const goldPart = physical.goldParts.find((value) => value.name === FRONT)
const dxfPart = dxf.files.find((value) => value.partName === FRONT)
assert.ok(v37Part && goldPart && dxfPart)
assert.equal(brep.v37.faceCount, 48)
assert.equal(brep.v37.edgeCount, 112)
assert.equal(brep.v37.loopEdgeReferenceCount, 224)
assert.equal(brep.gold.faceCount, 52)
assert.equal(brep.gold.edgeCount, 120)
assert.equal(brep.gold.loopEdgeReferenceCount, 240)

function cylinderRows(part, radiusMm) {
  return part.faces.filter((face) => face.isCylinder && face.cylinderParamsSi?.length === 7)
    .map((face) => ({
      xMm: mm(face.cylinderParamsSi[0]),
      yMm: mm(face.cylinderParamsSi[1]),
      zMm: mm(face.cylinderParamsSi[2]),
      axis: face.cylinderParamsSi.slice(3, 6).map((value) => round(value, 6)),
      radiusMm: mm(face.cylinderParamsSi[6]),
    }))
    .filter((value) => near(value.radiusMm, radiusMm) && Math.abs(value.axis[2]) > 0.999)
}

function uniqueSorted(values) {
  return [...new Set(values.map((value) => round(value, 3)))].sort(sortNumber)
}

const v37Small = cylinderRows(brep.v37, 1.75)
const v37Large = cylinderRows(brep.v37, 5)
const goldSmall = cylinderRows(brep.gold, 1.75)
const goldLarge = cylinderRows(brep.gold, 5)
assert.equal(v37Small.length, 5)
assert.equal(v37Large.length, 5)
assert.equal(goldSmall.length, 7)
assert.equal(goldLarge.length, 7)
const v37X = uniqueSorted(v37Small.map((value) => value.xMm))
const goldX = uniqueSorted(goldSmall.map((value) => value.xMm))
assert.deepEqual(v37X, [-260, -120, 20, 160, 300])
assert.deepEqual(goldX, [-420, -280, -140, 0, 140, 280, 420])
assert.deepEqual(uniqueSorted(v37Large.map((value) => value.xMm)), v37X)
assert.deepEqual(uniqueSorted(goldLarge.map((value) => value.xMm)), goldX)
assert.equal(v37Small.concat(v37Large, goldSmall, goldLarge).every((value) =>
  near(value.yMm, 1909)), true)

function holeCount(widthMm) {
  return Math.floor((widthMm - 2 * RIGHT_MARGIN_MM + 1e-9) / PITCH_MM) + 1
}

function holeXs(widthMm) {
  const count = holeCount(widthMm)
  const right = widthMm / 2 - RIGHT_MARGIN_MM
  const output = Array.from({ length: count }, (_, index) => right - index * PITCH_MM)
    .sort(sortNumber).map((value) => round(value, 3))
  const leftMargin = output[0] + widthMm / 2
  assert.equal(leftMargin >= MIN_LEFT_MARGIN_MM - 0.001, true)
  assert.equal(leftMargin < MIN_LEFT_MARGIN_MM + PITCH_MM + 0.001, true)
  return output
}
assert.deepEqual(holeXs(V37_WIDTH), v37X)
assert.deepEqual(holeXs(GOLD_WIDTH), goldX)
const targetX = holeXs(TARGET_WIDTH)
assert.deepEqual(targetX, [-336, -196, -56, 84, 224, 364])

const dxfLines = dxfPart.entities.filter((value) => value.type === 'LINE')
const dxfCircles = dxfPart.entities.filter((value) => value.type === 'CIRCLE')
assert.equal(dxfLines.length, 20)
assert.equal(dxfCircles.length, 14)
const bbox = dxfPart.manufacturingBboxMm
const dxfCenterX = (bbox.xmin_mm + bbox.xmax_mm) / 2
const flatBoundary = goldPart.sketches.find((value) => value.path === '平板型式3/边界框3')
const flatBends = goldPart.sketches.find((value) => value.path === '平板型式3/折弯-线3')
assert.ok(flatBoundary?.complete && flatBends?.complete)
const boundaryModelPoints = flatBoundary.segments.flatMap((value) =>
  [value.start.modelSi, value.end.modelSi])
const flatMinY = Math.min(...boundaryModelPoints.map((value) => mm(value[1])))
const flatMaxY = Math.max(...boundaryModelPoints.map((value) => mm(value[1])))
const flatZ = round(boundaryModelPoints[0][2] * 1000, 6)
assert.equal(near(flatMaxY - flatMinY, bbox.height_mm), true)
const yOffset = flatMinY - bbox.ymin_mm

function goldDxfPointToModel(point) {
  return [round(dxfCenterX - point[0], 6), round(point[1] + yOffset, 6), flatZ]
}

const goldOutline = dxfLines.map((value) => ({
  sourceIndex: value.index,
  startMm: goldDxfPointToModel(value.geometry.startMm),
  endMm: goldDxfPointToModel(value.geometry.endMm),
}))
const degrees = new Map()
for (const line of goldOutline) {
  for (const point of [line.startMm, line.endMm]) {
    const key = pointKey(point)
    degrees.set(key, (degrees.get(key) ?? 0) + 1)
  }
}
assert.equal([...degrees.values()].every((value) => value === 2), true,
  'front DXF line graph must be one or more closed loops')
assert.equal(degrees.size, 20)

function widthAnchorX(goldXmm, widthMm) {
  const inward = (GOLD_WIDTH - widthMm) / 2
  if (Math.abs(goldXmm) <= 0.001) return 0
  return round(goldXmm - Math.sign(goldXmm) * inward, 6)
}

function outlineFor(widthMm) {
  const rows = goldOutline.map((value) => ({ sourceIndex: value.sourceIndex,
    startMm: [widthAnchorX(value.startMm[0], widthMm), value.startMm[1], value.startMm[2]],
    endMm: [widthAnchorX(value.endMm[0], widthMm), value.endMm[1], value.endMm[2]] }))
  const xs = rows.flatMap((value) => [value.startMm[0], value.endMm[0]])
  assert.equal(near(Math.max(...xs) - Math.min(...xs), widthMm), true)
  return rows
}

const goldSmallDxf = dxfCircles.filter((value) => near(value.geometry.radiusMm * 2, 3.5))
const goldLargeDxf = dxfCircles.filter((value) => near(value.geometry.radiusMm * 2, 10))
assert.equal(goldSmallDxf.length, 7)
assert.equal(goldLargeDxf.length, 7)
const smallFlatY = round(goldSmallDxf[0].geometry.centerMm[1] + yOffset, 6)
const largeFlatY = round(goldLargeDxf[0].geometry.centerMm[1] + yOffset, 6)
assert.equal(goldSmallDxf.every((value) => near(value.geometry.centerMm[1] + yOffset,
  smallFlatY)), true)
assert.equal(goldLargeDxf.every((value) => near(value.geometry.centerMm[1] + yOffset,
  largeFlatY)), true)

function bendLinesFor(widthMm) {
  return flatBends.segments.map((value, index) => ({
    sourcePath: flatBends.path,
    sourceIndex: index,
    startMm: [widthAnchorX(mm(value.start.modelSi[0]), widthMm),
      mm(value.start.modelSi[1]), mm(value.start.modelSi[2])],
    endMm: [widthAnchorX(mm(value.end.modelSi[0]), widthMm),
      mm(value.end.modelSi[1]), mm(value.end.modelSi[2])],
  }))
}

const sourceBends = v37Part.oneBends.map((value) => ({
  path: value.path,
  bendType: value.bendType,
  angleRadians: value.angle,
  direction: value.direction,
  down: value.down,
  order: value.order,
  radiusMm: mm(value.radius),
  kFactor: value.kFactor,
  allowanceMm: mm(value.allowance),
}))
assert.deepEqual(sourceBends.map((value) => value.order), [1, 2])

const holePairRemovedVolumeMm3 = Math.PI * (1.75 ** 2 + 5 ** 2) * THICKNESS_MM
const holePairSurfaceDeltaMm2 = -2 * Math.PI * (1.75 ** 2 + 5 ** 2) +
  2 * Math.PI * (1.75 + 5) * THICKNESS_MM
const v37Body = v37Part.measurement.bodies[0]
const goldBody = goldPart.measurement.bodies[0]
const baseVolumeSlope = (goldBody.volumeMm3 - v37Body.volumeMm3 +
  (holeCount(GOLD_WIDTH) - holeCount(V37_WIDTH)) * holePairRemovedVolumeMm3) /
  (GOLD_WIDTH - V37_WIDTH)
const baseAreaSlope = (goldBody.surfaceAreaMm2 - v37Body.surfaceAreaMm2 -
  (holeCount(GOLD_WIDTH) - holeCount(V37_WIDTH)) * holePairSurfaceDeltaMm2) /
  (GOLD_WIDTH - V37_WIDTH)
const densityKgPerMm3 = v37Body.massKg / v37Body.volumeMm3

function expectedTopology(widthMm) {
  const holes = holeCount(widthMm) * 2
  return {
    holeCount: holes,
    cylinderFaceCount: 4 + holes,
    faceCount: 38 + holes,
    edgeCount: 92 + holes * 2,
    loopCount: 38 + holes * 4,
    loopEdgeReferenceCount: (92 + holes * 2) * 2,
    circleEdgeReferenceCount: 16 + holes * 4,
  }
}

function expectedMass(widthMm) {
  const pairsAdded = holeCount(widthMm) - holeCount(V37_WIDTH)
  const volume = v37Body.volumeMm3 + baseVolumeSlope * (widthMm - V37_WIDTH) -
    pairsAdded * holePairRemovedVolumeMm3
  const area = v37Body.surfaceAreaMm2 + baseAreaSlope * (widthMm - V37_WIDTH) +
    pairsAdded * holePairSurfaceDeltaMm2
  return { volumeMm3: round(volume, 6), massKg: round(volume * densityKgPerMm3, 9),
    surfaceAreaMm2: round(area, 6) }
}
assert.equal(near(expectedMass(V37_WIDTH).volumeMm3, v37Body.volumeMm3, 0.001), true)
assert.equal(near(expectedMass(GOLD_WIDTH).volumeMm3, goldBody.volumeMm3, 0.001), true)
assert.deepEqual(expectedTopology(V37_WIDTH), { holeCount: 10, cylinderFaceCount: 14,
  faceCount: 48, edgeCount: 112, loopCount: 78, loopEdgeReferenceCount: 224,
  circleEdgeReferenceCount: 56 })
assert.deepEqual(expectedTopology(GOLD_WIDTH), { holeCount: 14, cylinderFaceCount: 18,
  faceCount: 52, edgeCount: 120, loopCount: 94, loopEdgeReferenceCount: 240,
  circleEdgeReferenceCount: 72 })

function buildSpec(widthMm) {
  const xs = holeXs(widthMm)
  const foldedBbox = widthMm === V37_WIDTH
    ? v37Part.measurement.partBoxMm.map((value) => round(value, 6))
    : widthMm === GOLD_WIDTH
      ? goldPart.measurement.partBoxMm.map((value) => round(value, 6))
      : [-widthMm / 2, 1839.19998, -56.9, widthMm / 2, 1917.000005, -45.99995]
  return {
    cabinetWidthMm: widthMm,
    outline: outlineFor(widthMm),
    holes: xs.flatMap((x) => [
      { xMm: x, yMm: smallFlatY, diameterMm: 3.5 },
      { xMm: x, yMm: largeFlatY, diameterMm: 10 },
    ]),
    bendLines: bendLinesFor(widthMm),
    bends: sourceBends,
    flatPlaneZmm: flatZ,
    flatBboxMm: [-widthMm / 2, flatMinY, flatZ, widthMm / 2, flatMaxY, flatZ],
    foldedBboxMm: foldedBbox,
    expectedTopology: expectedTopology(widthMm),
    expectedMass: expectedMass(widthMm),
  }
}

const output = {
  schema: 'winnsen.16029.topcover_front_native_rule.v2',
  generatedAtUtc: new Date().toISOString(),
  purpose: 'structure_engineering_assistance',
  inputs: {
    physicalCapture: { path: physicalPath, sha256: sha256File(physicalPath) },
    goldDxfCapture: { path: dxfPath, sha256: sha256File(dxfPath) },
    physicalBundle: { path: bundlePath, sha256: sha256File(bundlePath) },
    masterCapture: { path: masterPath, sha256: sha256File(masterPath) },
    ruleV1: { path: ruleV1Path, sha256: sha256File(ruleV1Path) },
    frontBrep: { path: brepPath, sha256: sha256File(brepPath) },
    frontBrepCleanup: { path: cleanupPath, sha256: sha256File(cleanupPath) },
  },
  sheetMetal: { thicknessMm: THICKNESS_MM, defaultBendRadiusMm: 0.2,
    kFactor: 0.5, allowanceMm: 1.4 },
  flatMapping: { x: 'gold model x = DXF bbox center x - DXF x',
    yOffsetMm: round(yOffset, 6), zMm: flatZ,
    targetWidthAnchor: 'nonzero x moves inward by (1000-targetWidth)/2' },
  holePattern: { anchor: 'right', rightMarginMm: RIGHT_MARGIN_MM,
    pitchMm: PITCH_MM, minimumLeftMarginMm: MIN_LEFT_MARGIN_MM,
    countFormula: 'floor((width - 160) / 140) + 1',
    v37Xmm: v37X, goldXmm: goldX, target888Xmm: targetX },
  buildSpecs: {
    width760Pilot: buildSpec(V37_WIDTH),
    width888Target: buildSpec(TARGET_WIDTH),
    width1000Regression: buildSpec(GOLD_WIDTH),
  },
  frontNativePilotReady: true,
  nativeCadCreated: false,
  qualityBoundary: 'Rule and topology evidence only; the 760 pilot part must be built and regressed before 888 generation.',
}
const jsonPath = resolve(outDir, 'topcover_front_native_rule_v2.json')
const mdPath = resolve(outDir, 'topcover_front_native_rule_v2.md')
assert.equal(existsSync(jsonPath) || existsSync(mdPath), false,
  'immutable front rule output already exists')
const md = [
  '# 16029 上盖前侧板原生规则 v2', '',
  '- 孔阵列：从右边 80 mm 起，140 mm 节距向左；左余量不少于 80 mm。',
  `- 760：${v37X.join(', ')} mm；1000：${goldX.join(', ')} mm；888：${targetX.join(', ')} mm。`,
  '- 每个 X 位置包含 Ø3.5 与 Ø10 两孔。',
  `- 888 预期拓扑：${JSON.stringify(expectedTopology(TARGET_WIDTH))}。`,
  `- 888 预期质量：${JSON.stringify(expectedMass(TARGET_WIDTH))}。`,
  '- 760 原生试点完成并通过 B-rep/质量/FlatPattern 回归前，不允许进入 888 正式生成。',
  '',
].join('\n')
function writeNew(path, bytes) {
  const descriptor = openSync(path, 'wx', 0o600)
  try { writeFileSync(descriptor, bytes); fsyncSync(descriptor) } finally { closeSync(descriptor) }
  assert.deepEqual(readFileSync(path), bytes)
  assert.equal(Number(statSync(path).nlink), 1)
}
writeNew(jsonPath, Buffer.from(`${JSON.stringify(output, null, 2)}\n`, 'utf8'))
writeNew(mdPath, Buffer.from(md, 'utf8'))
process.stdout.write(`${JSON.stringify({ status: 'PASS', frontNativePilotReady: true,
  target888Xmm: targetX, target888Topology: expectedTopology(TARGET_WIDTH),
  target888Mass: expectedMass(TARGET_WIDTH), jsonPath, jsonSha256: sha256File(jsonPath),
  mdPath, mdSha256: sha256File(mdPath) }, null, 2)}\n`)
