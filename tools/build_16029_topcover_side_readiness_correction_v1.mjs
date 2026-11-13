import { createHash } from 'node:crypto'
import { closeSync, existsSync, fsyncSync, openSync, readFileSync, writeFileSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const rulePath = resolve(repoRoot,
  'data/native_model_requests/attempts/NATIVE-20260824T094619-888W14D-1CDD/' +
  'attempt-0001/evidence/private/topcover_native_rebuild_rules_v1.json')
const capturePath = resolve(repoRoot,
  'data/native_model_requests/attempts/NATIVE-20260824T092510-888W14D-F43C/' +
  'attempt-0001/evidence/private/topcover_native_rebuild_capture_probe_only.json')
const dxfPath = resolve(repoRoot,
  'data/native_model_requests/attempts/NATIVE-20260824T092510-888W14D-F43C/' +
  'attempt-0001/evidence/private/topcover_native_rebuild_gold_dxf_capture.json')
const expectedHashes = {
  ruleV1: '4F2BCA054CE4E91C38443D6604D2A7E23E7230D64769CBC903612980EB5E6012',
  physicalCapture: '68CF3723B540762C4991556A3571DB7B1127D1A7AE4B46A89CF38721BB0E58B5',
  goldDxfCapture: 'A018A121BF0CB31DB05CD3AE5D9FDE775FF151590F228EFC260A9DF327D6C319',
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}
function sha256File(path) {
  return sha256(readFileSync(path))
}
function fail(message) {
  throw new Error(message)
}
function key(point) {
  return point.slice(0, 2).map((value) => Number(value).toFixed(6)).join(',')
}
function polygonArea(points) {
  let twice = 0
  for (let index = 0; index < points.length; index += 1) {
    const current = points[index]
    const next = points[(index + 1) % points.length]
    twice += current[0] * next[1] - next[0] * current[1]
  }
  return Math.abs(twice) / 2
}
function lineComponents(entities) {
  const lines = entities.filter((value) => value.type === 'LINE')
  const nodes = new Map()
  const byIndex = new Map(lines.map((value) => [value.index, value]))
  for (const line of lines) {
    for (const point of [line.geometry.startMm, line.geometry.endMm]) {
      const id = key(point)
      if (!nodes.has(id)) nodes.set(id, [])
      nodes.get(id).push(line.index)
    }
  }
  const seen = new Set()
  const output = []
  for (const seed of lines) {
    if (seen.has(seed.index)) continue
    const pending = [seed.index]
    const indexes = []
    while (pending.length > 0) {
      const index = pending.pop()
      if (seen.has(index)) continue
      seen.add(index)
      indexes.push(index)
      const line = byIndex.get(index)
      for (const point of [line.geometry.startMm, line.geometry.endMm]) {
        for (const next of nodes.get(key(point))) if (!seen.has(next)) pending.push(next)
      }
    }
    const componentLines = indexes.map((index) => byIndex.get(index))
    if (!componentLines.every((line) =>
      nodes.get(key(line.geometry.startMm)).length === 2 &&
      nodes.get(key(line.geometry.endMm)).length === 2)) {
      fail('DXF line component is not a closed degree-two cycle')
    }
    const points = [componentLines[0].geometry.startMm.slice(0, 2)]
    let current = componentLines[0].geometry.endMm.slice(0, 2)
    const used = new Set([componentLines[0].index])
    while (used.size < componentLines.length) {
      points.push(current)
      const next = componentLines.find((line) => !used.has(line.index) &&
        (key(line.geometry.startMm) === key(current) ||
         key(line.geometry.endMm) === key(current)))
      if (!next) fail('DXF cycle ordering failed')
      used.add(next.index)
      current = key(next.geometry.startMm) === key(current)
        ? next.geometry.endMm.slice(0, 2)
        : next.geometry.startMm.slice(0, 2)
    }
    output.push({
      lineCount: indexes.length,
      entityIndexes: indexes.sort((left, right) => left - right),
      areaMm2: polygonArea(points),
    })
  }
  return output.sort((left, right) => right.areaMm2 - left.areaMm2)
}
function circleSummary(entities) {
  const circles = entities.filter((value) => value.type === 'CIRCLE')
  const centers = new Map()
  for (const circle of circles) {
    const id = key(circle.geometry.centerMm)
    if (!centers.has(id)) centers.set(id, [])
    centers.get(id).push(circle.geometry.radiusMm)
  }
  return {
    circleCount: circles.length,
    uniqueCenterCount: centers.size,
    concentricCenters: [...centers.entries()]
      .filter(([, radii]) => radii.length > 1)
      .map(([center, radii]) => ({
        centerDxfMm: center.split(',').map(Number),
        radiiMm: [...radii].sort((left, right) => left - right),
      })),
  }
}
function sideRecord({ name, capture, dxf, rule }) {
  const measurement = capture.measurement
  const body = measurement.bodies[0]
  const components = lineComponents(dxf.entities)
  const circles = circleSummary(dxf.entities)
  const pureOuterProfileVolumeMm3 = components[0].areaMm2 * 0.8
  return {
    name,
    v1WidthRule: rule.widthRule,
    v1TargetBuildReady: rule.targetBuildReady,
    correctedTargetBuildReady: false,
    correctionReasons: [
      'reference_brep_loop_and_cylinder_contract_missing',
      'formed_feature_contract_missing',
      'dxf_closed_loop_reconstruction_required',
      'dxf_contains_concentric_circle_pairs_that_cannot_be_blindly_merged_or_cut',
      'pure_outer_profile_volume_does_not_equal_reference_body_volume',
    ],
    dxf: {
      fileName: dxf.fileName,
      sha256: dxf.sha256Before,
      lineComponentCount: components.length,
      lineComponents: components,
      ...circles,
      manufacturingBboxMm: dxf.manufacturingBboxMm,
      needsLoopReconstruction: dxf.needsLoopReconstruction,
    },
    formedFeatureEvidence: {
      splitBodyCount: measurement.typeCounts.SplitBody ?? 0,
      mirrorStockCount: measurement.typeCounts.MirrorStock ?? 0,
      cutCount: measurement.typeCounts.Cut ?? 0,
      iceCount: measurement.typeCounts.ICE ?? 0,
      sketchBlockInstanceCount: measurement.typeCounts.SketchBlockInst ?? 0,
      sketchBlockDefinitionCount: measurement.typeCounts.SketchBlockDef ?? 0,
    },
    reference760: {
      bodyCount: measurement.bodyCount,
      foldedBboxMm: measurement.partBoxMm,
      volumeMm3: body.volumeMm3,
      surfaceAreaMm2: body.surfaceAreaMm2,
      massKg: body.massKg,
      faceCount: body.faceCount,
      edgeCount: body.edgeCount,
    },
    pureOuterProfileVolumeMm3,
    referenceMinusPureOuterProfileVolumeMm3: body.volumeMm3 - pureOuterProfileVolumeMm3,
    nextRequiredEvidence: [
      'attempt_local_v37_and_gold_full_brep_capture',
      'formed_feature_geometry_and_operation_classification',
      'ordered_outer_and_inner_flat_loop_contract',
      'native_760_runtime_regression_before_888_generation',
    ],
  }
}

const args = process.argv.slice(2)
if (args.length !== 2 || args[0] !== '--out') fail('Usage: --out <private-json>')
const outputPath = resolve(args[1])
if (existsSync(outputPath) || basename(outputPath) !==
    'topcover_side_native_readiness_correction_v1.json' ||
    basename(dirname(outputPath)) !== 'private' ||
    basename(dirname(dirname(outputPath))) !== 'evidence' ||
    basename(dirname(dirname(dirname(outputPath)))) !== 'attempt-0001' ||
    !/^NATIVE-[A-Z0-9-]+-SIDE-RULE-R\d+$/.test(
      basename(dirname(dirname(dirname(dirname(outputPath))))))) {
  fail('Output path is not a new fixed side-rule attempt artifact')
}
const rel = relative(repoRoot, outputPath)
if (rel.startsWith('..') || isAbsolute(rel)) fail('Output escaped repository')
for (const [name, path] of Object.entries({ ruleV1: rulePath,
  physicalCapture: capturePath, goldDxfCapture: dxfPath })) {
  if (sha256File(path) !== expectedHashes[name]) fail(`${name} hash mismatch`)
}

const ruleV1 = JSON.parse(readFileSync(rulePath, 'utf8'))
const physical = JSON.parse(readFileSync(capturePath, 'utf8'))
const goldDxf = JSON.parse(readFileSync(dxfPath, 'utf8'))
const sides = [
  ['上盖壳体左侧板.sldprt', 0],
  ['上盖壳体右侧板.SLDPRT', 1],
].map(([name, index]) => sideRecord({
  name,
  capture: physical.v37Parts.find((value) => value.name === name),
  dxf: goldDxf.files.find((value) => value.partName === name),
  rule: ruleV1.parts[index],
}))

const output = {
  schema: 'winnsen.16029.topcover_side_native_readiness_correction.v1',
  generatedAtUtc: new Date().toISOString(),
  purpose: 'structure_engineering_assistance',
  supersedesReadinessOnly: {
    path: relative(repoRoot, rulePath).replaceAll('\\', '/'),
    sha256: expectedHashes.ruleV1,
    fields: ['parts[0].targetBuildReady', 'parts[1].targetBuildReady'],
  },
  inputs: {
    physicalCapture: {
      path: relative(repoRoot, capturePath).replaceAll('\\', '/'),
      sha256: expectedHashes.physicalCapture,
    },
    goldDxfCapture: {
      path: relative(repoRoot, dxfPath).replaceAll('\\', '/'),
      sha256: expectedHashes.goldDxfCapture,
    },
  },
  provenWidthRelationRetained: true,
  sidePartsNativeBuildReady: false,
  sides,
  unresolved: [
    'side_reference_full_brep',
    'formed_feature_native_operation_contract',
    'ordered_flat_profile_and_nested_circle_semantics',
  ],
  qualityBoundary: 'Readiness correction only; no CAD was opened, generated, or accepted.',
}
const bytes = Buffer.from(`${JSON.stringify(output, null, 2)}\n`, 'utf8')
const descriptor = openSync(outputPath, 'wx')
try {
  writeFileSync(descriptor, bytes)
  fsyncSync(descriptor)
} finally {
  closeSync(descriptor)
}
if (!readFileSync(outputPath).equals(bytes)) fail('Output reread mismatch')
process.stdout.write(`${JSON.stringify({
  status: 'PASS',
  outputPath,
  outputSha256: sha256(bytes),
  sidePartsNativeBuildReady: false,
  cadStarted: false,
}, null, 2)}\n`)
