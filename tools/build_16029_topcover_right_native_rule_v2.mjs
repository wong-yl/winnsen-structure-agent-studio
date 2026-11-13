import { createHash } from 'node:crypto'
import { closeSync, existsSync, fsyncSync, openSync, readFileSync, writeFileSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const paths = {
  ruleV1: resolve(repoRoot,
    'data/native_model_requests/attempts/NATIVE-20260824T094619-888W14D-1CDD/' +
    'attempt-0001/evidence/private/topcover_native_rebuild_rules_v1.json'),
  physical: resolve(repoRoot,
    'data/native_model_requests/attempts/NATIVE-20260824T092510-888W14D-F43C/' +
    'attempt-0001/evidence/private/topcover_native_rebuild_capture_probe_only.json'),
  dxf: resolve(repoRoot,
    'data/native_model_requests/attempts/NATIVE-20260824T092510-888W14D-F43C/' +
    'attempt-0001/evidence/private/topcover_native_rebuild_gold_dxf_capture.json'),
  readiness: resolve(repoRoot,
    'data/native_model_requests/attempts/NATIVE-20260824T122534Z-888W14D-SIDE-RULE-R1/' +
    'attempt-0001/evidence/private/topcover_side_native_readiness_correction_v1.json'),
  referenceV5: resolve(repoRoot,
    'data/native_model_requests/attempts/TOPCOVER-REFERENCE-20260824T131829Z-RIGHT-SIDE-R7/' +
    'attempt-0001/evidence/private/topcover_right_reference_brep_capture_v5.json'),
}
const expectedHashes = {
  ruleV1: '4F2BCA054CE4E91C38443D6604D2A7E23E7230D64769CBC903612980EB5E6012',
  physical: '68CF3723B540762C4991556A3571DB7B1127D1A7AE4B46A89CF38721BB0E58B5',
  dxf: 'A018A121BF0CB31DB05CD3AE5D9FDE775FF151590F228EFC260A9DF327D6C319',
  readiness: 'F5D12C9FF2EA07EEF6D852984407DB2DD4ED929198BFF6EF47D6FE1E4A4F7A5C',
  referenceV5: 'BAEEF8AB4790F4650785605FC3C4A452753755558A931B9275977A1E88C32A98',
}
const sha256 = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileSha = (path) => sha256(readFileSync(path))
const fail = (message) => { throw new Error(message) }
const near = (left, right, tolerance = 1e-6) => Math.abs(left - right) <= tolerance
const pointKey = (point) => point.slice(0, 2).map((value) => value.toFixed(6)).join(',')

for (const [name, path] of Object.entries(paths))
  if (fileSha(path) !== expectedHashes[name]) fail(`${name} input hash mismatch`)
const ruleV1 = JSON.parse(readFileSync(paths.ruleV1, 'utf8'))
const physical = JSON.parse(readFileSync(paths.physical, 'utf8'))
const dxfCapture = JSON.parse(readFileSync(paths.dxf, 'utf8'))
const readiness = JSON.parse(readFileSync(paths.readiness, 'utf8'))
const reference = JSON.parse(readFileSync(paths.referenceV5, 'utf8'))
const rightName = '上盖壳体右侧板.SLDPRT'
const rightPhysical = physical.v37Parts.find((value) => value.name === rightName)
const rightDxf = dxfCapture.files.find((value) => value.partName === rightName)
const rightRuleV1 = ruleV1.parts.find((value) => value.name === rightName)
if (!rightPhysical || !rightDxf || !rightRuleV1 ||
    readiness.sidePartsNativeBuildReady !== false) fail('right-side input records missing')

function orderedLineComponents(entities) {
  const lines = entities.filter((value) => value.type === 'LINE')
  const byIndex = new Map(lines.map((value) => [value.index, value]))
  const nodes = new Map()
  for (const line of lines) for (const point of [line.geometry.startMm, line.geometry.endMm]) {
    const key = pointKey(point)
    if (!nodes.has(key)) nodes.set(key, [])
    nodes.get(key).push(line.index)
  }
  const seen = new Set()
  const components = []
  for (const seed of lines) {
    if (seen.has(seed.index)) continue
    const pending = [seed.index]
    const indexes = []
    while (pending.length) {
      const index = pending.pop()
      if (seen.has(index)) continue
      seen.add(index)
      indexes.push(index)
      const line = byIndex.get(index)
      for (const point of [line.geometry.startMm, line.geometry.endMm])
        for (const next of nodes.get(pointKey(point))) if (!seen.has(next)) pending.push(next)
    }
    const rows = indexes.map((index) => byIndex.get(index))
    if (!rows.every((line) => nodes.get(pointKey(line.geometry.startMm)).length === 2 &&
      nodes.get(pointKey(line.geometry.endMm)).length === 2)) fail('line loop not degree two')
    const points = [rows[0].geometry.startMm.slice(0, 2)]
    let current = rows[0].geometry.endMm.slice(0, 2)
    const used = new Set([rows[0].index])
    while (used.size < rows.length) {
      points.push(current)
      const next = rows.find((line) => !used.has(line.index) &&
        (pointKey(line.geometry.startMm) === pointKey(current) ||
         pointKey(line.geometry.endMm) === pointKey(current)))
      if (!next) fail('line loop ordering failed')
      used.add(next.index)
      current = pointKey(next.geometry.startMm) === pointKey(current)
        ? next.geometry.endMm.slice(0, 2) : next.geometry.startMm.slice(0, 2)
    }
    let twiceArea = 0
    for (let index = 0; index < points.length; index += 1) {
      const a = points[index]
      const b = points[(index + 1) % points.length]
      twiceArea += a[0] * b[1] - b[0] * a[1]
    }
    components.push({ sourceEntityIndexes: indexes.sort((a, b) => a - b),
      pointsDxfMm: points, areaMm2: Math.abs(twiceArea) / 2 })
  }
  return components.sort((left, right) => right.areaMm2 - left.areaMm2 ||
    left.sourceEntityIndexes[0] - right.sourceEntityIndexes[0])
}

const components = orderedLineComponents(rightDxf.entities)
if (components.length !== 5 || components[0].pointsDxfMm.length !== 22 ||
    !components.slice(1).every((value) => value.pointsDxfMm.length === 12))
  fail('right DXF line component contract mismatch')
const dxfXMin = Math.min(...components[0].pointsDxfMm.map((point) => point[0]))
const dxfYMin = Math.min(...components[0].pointsDxfMm.map((point) => point[1]))
const dxfYMax = Math.max(...components[0].pointsDxfMm.map((point) => point[1]))
const xOffset760 = dxfXMin - 299.8
const zConstant = dxfYMin - 47.0
if (!near(xOffset760, 539.8561797753) || !near(zConstant, 320.3146067416) ||
    !near(dxfYMax, 869.3146067416)) fail('DXF-to-model transform mismatch')
const mapPoint = (point, widthShift = 0) => [point[0] - xOffset760 + widthShift,
  1917, zConstant - point[1]]
const mappedComponents = components.map((component, index) => ({
  role: index === 0 ? 'outer_profile' : 'knockout_cut_loop',
  sourceEntityIndexes: component.sourceEntityIndexes,
  points760ModelMm: component.pointsDxfMm.map((point) => mapPoint(point)),
  areaMm2: component.areaMm2,
}))

const circleEntities = rightDxf.entities.filter((value) => value.type === 'CIRCLE')
const circleGroups = new Map()
for (const circle of circleEntities) {
  const key = pointKey(circle.geometry.centerMm)
  if (!circleGroups.has(key)) circleGroups.set(key, [])
  circleGroups.get(key).push(circle)
}
if (circleEntities.length !== 14 || circleGroups.size !== 11) fail('DXF circle contract mismatch')
const holes = []
const pemBosses = []
for (const rows of circleGroups.values()) {
  const radii = rows.map((value) => value.geometry.radiusMm).sort((a, b) => a - b)
  const center = mapPoint(rows[0].geometry.centerMm)
  holes.push({ center760ModelMm: center, radiusMm: Math.max(...radii),
    diameterMm: 2 * Math.max(...radii),
    sourceEntityIndexes: rows.map((value) => value.index).sort((a, b) => a - b) })
  if (radii.length === 2) {
    if (!near(radii[0], 3) || !near(radii[1], 4.4)) fail('unexpected concentric circle pair')
    pemBosses.push({ center760ModelMm: center, sheetHoleRadiusMm: 4.4,
      outerRadiusMm: 5.5, innerRadiusMm: 3, heightMm: 4,
      fromYmm: 1916.2, toYmm: 1912.2, mergeResult: true })
  }
}
holes.sort((left, right) => left.center760ModelMm[2] - right.center760ModelMm[2] ||
  left.center760ModelMm[0] - right.center760ModelMm[0])
pemBosses.sort((left, right) => left.center760ModelMm[2] - right.center760ModelMm[2])
if (holes.length !== 11 || pemBosses.length !== 3) fail('hole or PEM classification mismatch')

const outerArea = mappedComponents[0].areaMm2
const knockoutArea = mappedComponents.slice(1).reduce((sum, value) => sum + value.areaMm2, 0)
const holeArea = holes.reduce((sum, value) => sum + Math.PI * value.radiusMm ** 2, 0)
const baseSheetVolume = (outerArea - knockoutArea - holeArea) * 0.8
const pemVolume = pemBosses.reduce((sum, value) => sum + Math.PI *
  (value.outerRadiusMm ** 2 - value.innerRadiusMm ** 2) * value.heightMm, 0)
const calculatedFlatVolume = baseSheetVolume + pemVolume
const referenceFlat = reference.parts.v37_760.flat
if (!near(calculatedFlatVolume, referenceFlat.volumeMm3, 0.0001))
  fail(`manufacturing flat volume mismatch: ${calculatedFlatVolume}`)

const flatBendSketch = rightPhysical.sketches.find((value) =>
  value.path === '平板型式3/折弯-线2')
const lineByName = new Map(flatBendSketch.segments.map((value) => [value.name, value]))
const bendLineForOrder = new Map([[1, '直线7'], [2, '直线8'], [3, '直线9'],
  [4, '直线6'], [5, '直线10']])
const bends = rightPhysical.oneBends.map((value) => ({
  sourcePath: value.path,
  order: value.order,
  lineName: bendLineForOrder.get(value.order),
  line760ModelMm: {
    start: lineByName.get(bendLineForOrder.get(value.order)).start.modelSi
      .map((item) => item * 1000),
    end: lineByName.get(bendLineForOrder.get(value.order)).end.modelSi.map((item) => item * 1000),
  },
  angleRadians: value.angle,
  direction: value.direction,
  down: value.down,
  radiusMm: value.radius * 1000,
  useDefaultRadius: value.useDefaultRadius,
  bendPosition: 0,
  customAllowanceType: value.customAllowanceType,
  bendDeductionMm: value.customDeduction * 1000,
  kFactorReadback: value.kFactor,
})).sort((left, right) => left.order - right.order)
if (bends.some((value, index) => value.order !== index + 1 ||
  !value.lineName || !near(value.angleRadians, Math.PI / 2, 1e-12) ||
  !near(value.radiusMm, 0.5) || value.customAllowanceType !== 4 ||
  !near(value.bendDeductionMm, 1.4))) fail('bend contract mismatch')

const inputRows = Object.fromEntries(Object.entries(paths).map(([name, path]) => [name, {
  path: relative(repoRoot, path).replaceAll('\\', '/'), sha256: expectedHashes[name],
}]))
const output = {
  schema: 'winnsen.16029.topcover_right_native_rule.v2',
  generatedAtUtc: new Date().toISOString(),
  purpose: 'structure_engineering_assistance',
  inputs: inputRows,
  partName: rightName,
  widthRelation: {
    localGeometryInvariant: true,
    xShiftFrom760Mm: { width760: 0, width888: 64, width1000: 120 },
    targetFormula: 'targetX = width760X + (targetWidth - 760) / 2',
    foldedAndFlatTranslationSignaturesProven: true,
  },
  dxfToWidth760Model: {
    xFormula: `modelX = dxfX - ${xOffset760.toFixed(10)}`,
    yMm: 1917,
    zFormula: `modelZ = ${zConstant.toFixed(10)} - dxfY`,
  },
  sheetMetal: {
    thicknessMm: 0.8,
    defaultBendRadiusMm: 0.2,
    kFactorReadback: 0.5,
    customAllowanceType: 4,
    bendDeductionMm: 1.4,
  },
  manufacturingFlat760: {
    baseSheet: {
      outerLoop: mappedComponents[0],
      knockoutCutLoops: mappedComponents.slice(1),
      throughHoles: holes,
      thicknessMm: 0.8,
      topYmm: 1917,
      bottomYmm: 1916.2,
    },
    pemBosses,
    volumeBreakdownMm3: { baseSheetVolume, pemVolume, calculatedFlatVolume },
    expected: {
      bboxMm: referenceFlat.bboxMm,
      volumeMm3: referenceFlat.volumeMm3,
      massKg: referenceFlat.massKg,
      surfaceAreaMm2: referenceFlat.surfaceAreaMm2,
      topology: {
        faceCount: referenceFlat.faceCount,
        edgeCount: referenceFlat.edgeCount,
        loopCount: referenceFlat.loopCount,
        loopEdgeReferenceCount: referenceFlat.loopEdgeReferenceCount,
        cylinderFaceCount: referenceFlat.cylinderFaceCount,
        circleEdgeReferenceCount: referenceFlat.circleEdgeReferences.length,
      },
      translationNormalizedDetailedGeometrySignature:
        referenceFlat.translationNormalizedDetailedGeometrySignature,
    },
  },
  bends760: bends,
  expectedFolded760: {
    bboxMm: reference.parts.v37_760.folded.bboxMm,
    volumeMm3: reference.parts.v37_760.folded.volumeMm3,
    massKg: reference.parts.v37_760.folded.massKg,
    surfaceAreaMm2: reference.parts.v37_760.folded.surfaceAreaMm2,
    topology: {
      faceCount: reference.parts.v37_760.folded.faceCount,
      edgeCount: reference.parts.v37_760.folded.edgeCount,
      loopCount: reference.parts.v37_760.folded.loopCount,
      loopEdgeReferenceCount: reference.parts.v37_760.folded.loopEdgeReferenceCount,
      cylinderFaceCount: reference.parts.v37_760.folded.cylinderFaceCount,
      circleEdgeReferenceCount: reference.parts.v37_760.folded.circleEdgeReferences.length,
    },
    foldedMinusManufacturingFlatVolumeMm3:
      reference.parts.v37_760.foldedMinusFlatVolumeMm3,
    translationNormalizedDetailedGeometrySignature:
      reference.parts.v37_760.folded.translationNormalizedDetailedGeometrySignature,
  },
  nativeBuildSequence: [
    'native_base_flange_from_outer_loop',
    'single_through_all_cut_for_four_knockout_loops_and_eleven_holes',
    'three_native_merged_annular_pem_boss_extrusions',
    'five_native_sketched_bends_in_order_1_to_5',
    'single_save_then_fresh_read_only_full_brep_regression',
  ],
  right760NativePilotContractReady: true,
  right888NativeBuildReady: false,
  nativeCadCreated: false,
  unresolved: ['760_native_runtime_regression_before_888'],
  qualityBoundary: 'Rule and read-only reference evidence only; no right-side native CAD has been created.',
}

const args = process.argv.slice(2)
if (args.length !== 2 || args[0] !== '--out') fail('Usage: --out <private-json>')
const outputPath = resolve(args[1])
if (existsSync(outputPath) || basename(outputPath) !==
    'topcover_right_native_rule_v2.json' || basename(dirname(outputPath)) !== 'private' ||
    !/^NATIVE-[A-Z0-9-]+-RIGHT-RULE-R\d+$/.test(
      basename(dirname(dirname(dirname(dirname(outputPath)))))))
  fail('output path is not a fixed new right-rule attempt')
const rel = relative(repoRoot, outputPath)
if (rel.startsWith('..') || isAbsolute(rel)) fail('output escaped repository')
const bytes = Buffer.from(`${JSON.stringify(output, null, 2)}\n`, 'utf8')
const descriptor = openSync(outputPath, 'wx')
try { writeFileSync(descriptor, bytes); fsyncSync(descriptor) } finally { closeSync(descriptor) }
if (!readFileSync(outputPath).equals(bytes)) fail('output reread mismatch')
process.stdout.write(`${JSON.stringify({ status:'PASS', outputPath,
  outputSha256:sha256(bytes), cadStarted:false,
  right760NativePilotContractReady:true, nativeCadCreated:false }, null, 2)}\n`)
