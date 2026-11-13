import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { lstatSync, readFileSync, statSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve('.')
const rulePath = resolve(root,
  'data/native_model_requests/attempts/NATIVE-20260824T101117-888W14D-3E50/' +
  'attempt-0001/evidence/private/topcover_front_native_rule_v2.json')
const expectedRuleSha = '15CDAA0B19189F3D6E41D7A277F15AA1A0CEF58CA304B6BE5E6B207F43B14553'
const sha256 = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const sha256File = (path) => sha256(readFileSync(path))
const snapshot = () => {
  const value = execFileSync('powershell.exe', ['-NoProfile', '-Command',
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Sort-Object ProcessName,Id | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ','"],
  { cwd: root, encoding: 'utf8', windowsHide: true }).trim()
  return value ? value.split(',') : []
}
const near = (left, right, tolerance = 0.001) => Math.abs(Number(left) - Number(right)) <= tolerance
const pointKey = (point) => point.map((value) => Number(value).toFixed(6)).join(',')

assert.deepEqual(snapshot(), [])
assert.equal(lstatSync(rulePath).isSymbolicLink(), false)
assert.equal(Number(statSync(rulePath).nlink), 1)
assert.equal(sha256File(rulePath), expectedRuleSha)
const rule = JSON.parse(readFileSync(rulePath, 'utf8'))
assert.equal(rule.schema, 'winnsen.16029.topcover_front_native_rule.v2')
assert.equal(rule.purpose, 'structure_engineering_assistance')
assert.equal(rule.frontNativePilotReady, true)
assert.equal(rule.nativeCadCreated, false)
assert.deepEqual(rule.sheetMetal, { thicknessMm: 0.8, defaultBendRadiusMm: 0.2,
  kFactor: 0.5, allowanceMm: 1.4 })
assert.deepEqual(rule.holePattern, {
  anchor: 'right', rightMarginMm: 80, pitchMm: 140, minimumLeftMarginMm: 80,
  countFormula: 'floor((width - 160) / 140) + 1',
  v37Xmm: [-260, -120, 20, 160, 300],
  goldXmm: [-420, -280, -140, 0, 140, 280, 420],
  target888Xmm: [-336, -196, -56, 84, 224, 364],
})

for (const input of Object.values(rule.inputs)) {
  assert.equal(sha256File(resolve(input.path)), input.sha256)
}

function validateSpec(spec, width, expectedXs, topology) {
  assert.equal(spec.cabinetWidthMm, width)
  assert.equal(spec.outline.length, 20)
  const degree = new Map()
  for (const line of spec.outline) {
    assert.equal(line.startMm.length, 3)
    assert.equal(line.endMm.length, 3)
    for (const point of [line.startMm, line.endMm]) {
      const key = pointKey(point)
      degree.set(key, (degree.get(key) ?? 0) + 1)
    }
  }
  assert.equal([...degree.values()].every((value) => value === 2), true)
  const outlineXs = spec.outline.flatMap((value) => [value.startMm[0], value.endMm[0]])
  assert.equal(near(Math.max(...outlineXs) - Math.min(...outlineXs), width), true)
  assert.equal(spec.holes.length, expectedXs.length * 2)
  assert.deepEqual([...new Set(spec.holes.map((value) => value.xMm))].sort((a, b) => a - b),
    expectedXs)
  assert.deepEqual([...new Set(spec.holes.map((value) => value.diameterMm))].sort((a, b) => a - b),
    [3.5, 10])
  assert.equal(spec.bendLines.length, 2)
  assert.equal(spec.bends.length, 2)
  assert.deepEqual(spec.bends.map((value) => value.order), [1, 2])
  assert.equal(near(spec.bends[0].angleRadians, Math.PI / 2, 1e-12), true)
  assert.equal(near(spec.bends[1].angleRadians, Math.PI, 1e-12), true)
  assert.equal(near(spec.bends[0].radiusMm, 0.2), true)
  assert.equal(near(spec.bends[1].radiusMm, 0.05), true)
  assert.deepEqual(spec.expectedTopology, topology)
  assert.equal(spec.expectedMass.volumeMm3 > 0, true)
  assert.equal(spec.expectedMass.massKg > 0, true)
  assert.equal(spec.expectedMass.surfaceAreaMm2 > 0, true)
}

validateSpec(rule.buildSpecs.width760Pilot, 760, [-260, -120, 20, 160, 300], {
  holeCount: 10, cylinderFaceCount: 14, faceCount: 48, edgeCount: 112,
  loopCount: 78, loopEdgeReferenceCount: 224, circleEdgeReferenceCount: 56,
})
validateSpec(rule.buildSpecs.width888Target, 888, [-336, -196, -56, 84, 224, 364], {
  holeCount: 12, cylinderFaceCount: 16, faceCount: 50, edgeCount: 116,
  loopCount: 86, loopEdgeReferenceCount: 232, circleEdgeReferenceCount: 64,
})
validateSpec(rule.buildSpecs.width1000Regression, 1000,
  [-420, -280, -140, 0, 140, 280, 420], {
    holeCount: 14, cylinderFaceCount: 18, faceCount: 52, edgeCount: 120,
    loopCount: 94, loopEdgeReferenceCount: 240, circleEdgeReferenceCount: 72,
  })
assert.equal(near(rule.buildSpecs.width760Pilot.expectedMass.volumeMm3, 27485.460242, 0.001), true)
assert.equal(near(rule.buildSpecs.width888Target.expectedMass.volumeMm3, 32274.446273, 0.001), true)
assert.deepEqual(snapshot(), [])
process.stdout.write(`${JSON.stringify({ status: 'PASS', checksTotal: 30,
  checksFailed: 0, ruleSha256: expectedRuleSha, cadStarted: false }, null, 2)}\n`)
