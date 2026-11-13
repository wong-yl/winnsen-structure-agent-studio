import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import {
  closeSync,
  existsSync,
  fsyncSync,
  lstatSync,
  mkdirSync,
  openSync,
  readFileSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const attemptsRoot = resolve(root, 'data/native_model_requests/attempts')
const WIDTH_V37 = 760
const WIDTH_GOLD = 1000
const WIDTH_TARGET = 888
const FACTOR = (WIDTH_TARGET - WIDTH_V37) / (WIDTH_GOLD - WIDTH_V37)
const PART_NAMES = Object.freeze([
  '上盖壳体左侧板.sldprt',
  '上盖壳体右侧板.SLDPRT',
  '上盖壳体前侧板.sldprt',
  '上盖壳体后侧板.sldprt',
  '上盖壳体底板.sldprt',
])

const sha256 = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const sha256File = (path) => sha256(readFileSync(path))
const round = (value, digits = 9) => Number(Number(value).toFixed(digits))
const mm = (value) => round(Number(value) * 1000, 6)
const interpolate = (left, right) => round(Number(left) + FACTOR *
  (Number(right) - Number(left)), 12)
const same = (left, right, tolerance = 1e-9) =>
  Math.abs(Number(left) - Number(right)) <= tolerance
const point = (value) => Array.isArray(value) && value.length === 3 &&
  value.every((item) => Number.isFinite(Number(item)))
const transform = (value) => Array.isArray(value) && value.length === 16 &&
  value.every((item) => Number.isFinite(Number(item)))

function parseArgs(argv) {
  const output = {}
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index]
    const value = argv[index + 1]
    assert.ok(['--physical-attempt', '--master-attempt', '--out-dir'].includes(key),
      `unsupported argument: ${key}`)
    assert.ok(value, `missing value for ${key}`)
    output[key.slice(2)] = value
  }
  assert.deepEqual(Object.keys(output).sort(), ['master-attempt', 'out-dir', 'physical-attempt'])
  return output
}

function assertAttempt(path) {
  const absolute = resolve(path)
  const rel = relative(attemptsRoot, absolute)
  assert.equal(rel !== '' && !rel.startsWith('..') && !isAbsolute(rel), true,
    `attempt escaped fixed root: ${absolute}`)
  assert.equal(existsSync(absolute), true)
  assert.equal(lstatSync(absolute).isSymbolicLink(), false)
  return absolute
}

function readJson(path) {
  assert.equal(existsSync(path), true, `missing evidence: ${path}`)
  assert.equal(lstatSync(path).isSymbolicLink(), false)
  assert.equal(Number(statSync(path).nlink), 1)
  return JSON.parse(readFileSync(path, 'utf8'))
}

function mapByName(rows) {
  return new Map(rows.map((value) => [String(value.name).toUpperCase(), value]))
}

function segmentKey(value) {
  return `${value.kind}|${value.name}|${(value.ids ?? []).join(',')}`
}

function mapUnique(rows, keyFn, label) {
  const output = new Map()
  for (const row of rows) {
    const key = keyFn(row)
    assert.equal(output.has(key), false, `${label} duplicate key: ${key}`)
    output.set(key, row)
  }
  return output
}

function classifyCoordinate(v37, gold, scope, counters, samples, strictWidthRule) {
  assert.equal(point(v37), true, `${scope} V37 point invalid`)
  assert.equal(point(gold), true, `${scope} gold point invalid`)
  const deltaMm = gold.map((value, index) => mm(Number(value) - Number(v37[index])))
  const xDelta = deltaMm[0]
  let category = ''
  if (Math.abs(deltaMm[1]) <= 0.001 && Math.abs(deltaMm[2]) <= 0.001 &&
      Math.abs(xDelta + 120) <= 0.001) category = 'left_width_anchor'
  else if (deltaMm.every((value) => Math.abs(value) <= 0.001))
    category = 'center_invariant'
  else if (Math.abs(deltaMm[1]) <= 0.001 && Math.abs(deltaMm[2]) <= 0.001 &&
      Math.abs(xDelta - 120) <= 0.001) category = 'right_width_anchor'
  else category = 'two_point_compound_linear'
  if (strictWidthRule) assert.notEqual(category, 'two_point_compound_linear',
    `${scope} violates the master half-width rule: ${deltaMm}`)
  counters[category] = (counters[category] ?? 0) + 1
  if (samples.length < 60 || (category === 'two_point_compound_linear' &&
      samples.filter((value) => value.category === 'two_point_compound_linear').length < 80)) {
    samples.push({ scope, category, deltaMm, v37Mm: v37.map(mm), goldMm: gold.map(mm),
      target888Mm: v37.map((value, index) => mm(interpolate(value, gold[index]))) })
  }
  return category
}

function compareTransform(v37, gold, scope, counters, strictWidthRule) {
  assert.equal(transform(v37), true, `${scope} V37 transform invalid`)
  assert.equal(transform(gold), true, `${scope} gold transform invalid`)
  const target = []
  for (let index = 0; index < 16; index += 1) {
    const delta = mm(Number(gold[index]) - Number(v37[index]))
    if (Math.abs(delta) > 0.001) {
      assert.equal(index >= 9 && index <= 11, true,
        `${scope} non-translation transform changed at ${index}: ${delta} mm`)
      if (strictWidthRule) assert.equal(Math.abs(Math.abs(delta) - 120) <= 0.001, true,
        `${scope} transform delta is not 120 mm: ${delta}`)
      const key = Math.abs(Math.abs(delta) - 120) <= 0.001
        ? 'transform_width_anchor' : 'transform_two_point_compound_linear'
      counters[key] = (counters[key] ?? 0) + 1
    }
    target.push(interpolate(v37[index], gold[index]))
  }
  return target
}

function compareSketches(v37Sketches, goldSketches, scope, counters, samples,
  strictWidthRule) {
  const vMap = mapUnique(v37Sketches, (value) => value.path, `${scope} V37 sketch`)
  const gMap = mapUnique(goldSketches, (value) => value.path, `${scope} gold sketch`)
  assert.deepEqual([...vMap.keys()].sort(), [...gMap.keys()].sort(),
    `${scope} sketch path set drift`)
  const output = []
  for (const [path, vSketch] of vMap) {
    const gSketch = gMap.get(path)
    assert.equal(vSketch.complete && gSketch.complete, true, `${scope}/${path} incomplete`)
    assert.equal(vSketch.featureType, gSketch.featureType)
    assert.equal(vSketch.segmentCount, gSketch.segmentCount)
    assert.equal(vSketch.relationCount, gSketch.relationCount)
    assert.equal(vSketch.blocks.length, gSketch.blocks.length)
    const targetSketchToModel = compareTransform(vSketch.sketchToModelTransform16,
      gSketch.sketchToModelTransform16, `${scope}/${path}/sketchToModel`, counters,
      strictWidthRule)
    compareTransform(vSketch.modelToSketchTransform16, gSketch.modelToSketchTransform16,
      `${scope}/${path}/modelToSketch`, counters, strictWidthRule)
    const vSegments = mapUnique(vSketch.segments, segmentKey,
      `${scope}/${path} V37 segment`)
    const gSegments = mapUnique(gSketch.segments, segmentKey,
      `${scope}/${path} gold segment`)
    assert.deepEqual([...vSegments.keys()].sort(), [...gSegments.keys()].sort(),
      `${scope}/${path} segment set drift`)
    for (const [key, vSegment] of vSegments) {
      const gSegment = gSegments.get(key)
      for (const field of ['kind', 'type', 'construction', 'status', 'constraintCount',
        'relationCount', 'isBendLine']) assert.equal(vSegment[field], gSegment[field],
      `${scope}/${path}/${key}/${field}`)
      for (const field of ['start', 'end', 'center']) {
        const vPoint = vSegment[field]?.modelSi
        const gPoint = gSegment[field]?.modelSi
        if (point(vPoint) || point(gPoint)) classifyCoordinate(vPoint, gPoint,
          `${scope}/${path}/${key}/${field}`, counters, samples, strictWidthRule)
      }
      if (vSegment.kind === 'arc') {
        assert.equal(same(vSegment.radiusSi, gSegment.radiusSi), true)
        assert.equal(vSegment.rotationDirection, gSegment.rotationDirection)
      }
    }
    const vBlocks = mapUnique(vSketch.blocks, (value) => value.name,
      `${scope}/${path} V37 block`)
    const gBlocks = mapUnique(gSketch.blocks, (value) => value.name,
      `${scope}/${path} gold block`)
    assert.deepEqual([...vBlocks.keys()].sort(), [...gBlocks.keys()].sort(),
      `${scope}/${path} block set drift`)
    for (const [name, vBlock] of vBlocks) {
      const gBlock = gBlocks.get(name)
      assert.equal(same(vBlock.angle, gBlock.angle), true)
      assert.equal(same(vBlock.scale, gBlock.scale), true)
      assert.equal(vBlock.linkToFile, false)
      assert.equal(gBlock.linkToFile, false)
      classifyCoordinate(vBlock.insertion.modelSi, gBlock.insertion.modelSi,
        `${scope}/${path}/block:${name}/insertion`, counters, samples, strictWidthRule)
      compareTransform(vBlock.blockToSketchTransform16, gBlock.blockToSketchTransform16,
        `${scope}/${path}/block:${name}/transform`, counters, strictWidthRule)
    }
    output.push({ path, segments: vSketch.segmentCount, blocks: vSketch.blocks.length,
      targetSketchToModelTransform16: targetSketchToModel })
  }
  return output
}

function compareBends(v37, gold, scope) {
  assert.equal(v37.length, gold.length, `${scope} bend count drift`)
  const vMap = mapUnique(v37, (value) => value.path, `${scope} V37 bend`)
  const gMap = mapUnique(gold, (value) => value.path, `${scope} gold bend`)
  assert.deepEqual([...vMap.keys()].sort(), [...gMap.keys()].sort())
  return [...vMap].map(([path, vBend]) => {
    const gBend = gMap.get(path)
    for (const field of ['type', 'bendType', 'direction', 'down', 'order',
      'useDefaultRadius', 'useDefaultAllowance', 'customAllowanceType']) {
      assert.equal(vBend[field], gBend[field], `${scope}/${path}/${field}`)
    }
    for (const field of ['angle', 'radius', 'kFactor', 'allowance', 'customKFactor',
      'customAllowance', 'customDeduction']) {
      assert.equal(same(vBend[field], gBend[field]), true, `${scope}/${path}/${field}`)
    }
    return { path, angleRadians: vBend.angle, direction: vBend.direction,
      down: vBend.down, order: vBend.order, radiusMm: mm(vBend.radius),
      kFactor: vBend.kFactor, allowanceMm: mm(vBend.allowance),
      supplement: 'gold_dxf_and_fold_sketch' }
  })
}

function predictedMeasurement(v37, gold) {
  const bodyV = v37.measurement.bodies[0]
  const bodyG = gold.measurement.bodies[0]
  return {
    bboxMm: v37.measurement.partBoxMm.map((value, index) =>
      round(interpolate(value, gold.measurement.partBoxMm[index]), 6)),
    volumeMm3: round(interpolate(bodyV.volumeMm3, bodyG.volumeMm3), 6),
    massKg: round(interpolate(bodyV.massKg, bodyG.massKg), 9),
    surfaceAreaMm2: round(interpolate(bodyV.surfaceAreaMm2, bodyG.surfaceAreaMm2), 6),
    sourceTopology: {
      v37: { faces: bodyV.faceCount, edges: bodyV.edgeCount },
      gold: { faces: bodyG.faceCount, edges: bodyG.edgeCount },
      target: bodyV.faceCount === bodyG.faceCount && bodyV.edgeCount === bodyG.edgeCount
        ? { faces: bodyV.faceCount, edges: bodyV.edgeCount }
        : null,
    },
  }
}

const args = parseArgs(process.argv.slice(2))
const physicalAttempt = assertAttempt(args['physical-attempt'])
const masterAttempt = assertAttempt(args['master-attempt'])
const outDir = resolve(args['out-dir'])
const outRel = relative(attemptsRoot, outDir)
assert.equal(!outRel.startsWith('..') && !isAbsolute(outRel), true,
  'output must remain under native attempts root')
mkdirSync(outDir, { recursive: true })
assert.equal(lstatSync(outDir).isSymbolicLink(), false)

const physicalPath = resolve(physicalAttempt,
  'evidence/private/topcover_native_rebuild_capture_probe_only.json')
const dxfPath = resolve(physicalAttempt,
  'evidence/private/topcover_native_rebuild_gold_dxf_capture.json')
const bundlePath = resolve(physicalAttempt,
  'evidence/private/topcover_native_rebuild_capture_bundle.json')
const masterPath = resolve(masterAttempt,
  'evidence/private/topcover_native_master_reference_capture_probe_only.json')
const physical = readJson(physicalPath)
const dxf = readJson(dxfPath)
const bundle = readJson(bundlePath)
const master = readJson(masterPath)
assert.equal(physical.datasetComplete, true)
assert.equal(dxf.capturePass, true)
assert.equal(bundle.gates?.cadDatasetComplete, true)
assert.equal(master.datasetComplete, true)

const counters = {}
const samples = []
const masterSketchRules = compareSketches(master.v37Master.sketches,
  master.goldMaster.sketches, 'master', counters, samples, true)
const v37Parts = mapByName(physical.v37Parts)
const goldParts = mapByName(physical.goldParts)
const partRules = []
for (const name of PART_NAMES) {
  const v37 = v37Parts.get(name.toUpperCase())
  const gold = goldParts.get(name.toUpperCase())
  assert.ok(v37 && gold, `missing physical pair: ${name}`)
  const sketches = compareSketches(v37.sketches, gold.sketches,
    `part:${name}`, counters, samples, false)
  const bends = compareBends(v37.oneBends, gold.oneBends, `part:${name}`)
  const sideInvariant = name.includes('左侧板') || name.includes('右侧板')
  partRules.push({
    name,
    widthRule: sideInvariant
      ? 'flat_geometry_width_invariant_with_half_width_global_anchor'
      : 'width_anchored_master_geometry_with_flat_brep_correlation_required',
    targetBuildReady: sideInvariant,
    unresolved: sideInvariant ? [] : ['target_flat_brep_correspondence'],
    predicted888: predictedMeasurement(v37, gold),
    sketches,
    bends,
    processBends: compareBends(v37.processBends, gold.processBends,
      `part:${name}:process`),
  })
}

assert.equal(Object.keys(counters).some((key) => key.includes('unknown')), false)
assert.equal(counters.left_width_anchor > 0, true)
assert.equal(counters.center_invariant > 0, true)
assert.equal(counters.right_width_anchor > 0, true)
const unresolved = partRules.flatMap((part) => part.unresolved.map((value) =>
  `${part.name}:${value}`))
const output = {
  schema: 'winnsen.16029.topcover_native_rebuild_rules.v1',
  generatedAtUtc: new Date().toISOString(),
  purpose: 'structure_engineering_assistance',
  inputs: {
    physicalCapture: { path: relative(root, physicalPath).replaceAll('\\', '/'),
      sha256: sha256File(physicalPath) },
    goldDxfCapture: { path: relative(root, dxfPath).replaceAll('\\', '/'),
      sha256: sha256File(dxfPath) },
    physicalBundle: { path: relative(root, bundlePath).replaceAll('\\', '/'),
      sha256: sha256File(bundlePath) },
    masterCapture: { path: relative(root, masterPath).replaceAll('\\', '/'),
      sha256: sha256File(masterPath) },
  },
  widthsMm: { v37: WIDTH_V37, gold: WIDTH_GOLD, target: WIDTH_TARGET,
    interpolationFactor: FACTOR },
  masterProvenCoordinateRule: {
    formula: 'target = v37 + (gold - v37) * 8/15',
    allowedGoldMinusV37Xmm: [-120, 0, 120],
    requiredGoldMinusV37Ymm: 0,
    requiredGoldMinusV37Zmm: 0,
    targetMinusV37Xmm: [-64, 0, 64],
    masterScope: 'all master sketch points and transforms',
  },
  downstreamTwoPointRuleCandidates: {
    formula: 'target = v37 + (gold - v37) * 8/15',
    status: counters.two_point_compound_linear > 0
      ? 'requires_brep_and_engineering_correlation'
      : 'half_width_rule_only',
    counters,
    samples,
  },
  master: { bodies: 5, sketches: masterSketchRules,
    outputAssemblyMember: false },
  parts: partRules,
  goldDxf: dxf.files.map((value) => ({ partName: value.partName,
    entityCount: value.entityCount, signature: value.canonicalGeometrySha256 })),
  ruleExtractionPass: true,
  nativeBuildReady: unresolved.length === 0,
  unresolved,
  qualityBoundary: 'Two-point native geometry rule evidence; unresolved B-rep correspondence blocks native part creation.',
}
const jsonPath = resolve(outDir, 'topcover_native_rebuild_rules_v1.json')
const mdPath = resolve(outDir, 'topcover_native_rebuild_rules_v1.md')
assert.equal(existsSync(jsonPath) || existsSync(mdPath), false,
  'immutable rule output already exists')
const jsonBytes = Buffer.from(`${JSON.stringify(output, null, 2)}\n`, 'utf8')
const md = [
  '# 16029 上盖原生重建规则 v1',
  '',
  `- 两点宽度：${WIDTH_V37} / ${WIDTH_GOLD} mm；目标：${WIDTH_TARGET} mm。`,
  '- 母体已证明坐标变化仅为左锚 -120、中心 0、右锚 +120 mm；目标对应 -64 / 0 / +64 mm。',
  `- 下游存在复合两点线性样本：${counters.two_point_compound_linear ?? 0}；必须由 B-rep 对应关系复核。`,
  `- 母体草图：${masterSketchRules.length}；坐标样本计数：${JSON.stringify(counters)}。`,
  `- 当前 nativeBuildReady：${output.nativeBuildReady}。`,
  '',
  '## 分件状态',
  '',
  ...partRules.map((part) => `- ${part.name}: ${part.targetBuildReady ? '可进入单件实现' : `待 ${part.unresolved.join(', ')}`}`),
  '',
  '本文件只证明参数规则，不表示任何新原生零件已经生成。',
  '',
].join('\n')
function writeNew(path, bytes) {
  const descriptor = openSync(path, 'wx', 0o600)
  try { writeFileSync(descriptor, bytes); fsyncSync(descriptor) } finally { closeSync(descriptor) }
  assert.deepEqual(readFileSync(path), bytes)
  assert.equal(Number(statSync(path).nlink), 1)
}
writeNew(jsonPath, jsonBytes)
writeNew(mdPath, Buffer.from(md, 'utf8'))
process.stdout.write(`${JSON.stringify({ status: 'PASS', ruleExtractionPass: true,
  nativeBuildReady: output.nativeBuildReady, unresolved, jsonPath,
  jsonSha256: sha256File(jsonPath), mdPath, mdSha256: sha256File(mdPath), counters }, null, 2)}\n`)
