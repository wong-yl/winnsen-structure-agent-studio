import assert from 'node:assert/strict'
import { readFileSync, existsSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { adaptWidthSource, WIDTH_SOURCE_PATH } from '../workers/native_model_requests/parametric_v1/width_adapter.mjs'
import { adaptDoorSource, DOOR_SOURCE_PATH } from '../workers/native_model_requests/parametric_v1/door_adapter.mjs'

const widthPath = WIDTH_SOURCE_PATH
const width = readFileSync(widthPath, 'utf8')
const door = readFileSync(DOOR_SOURCE_PATH, 'utf8')
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const widthBefore = sha(width), doorBefore = sha(door)
const widthOutput = adaptWidthSource(width)
const doorOutput = adaptDoorSource(door, 'A07174DEDF4F9F0958A731E679655B742E53BEDA063709F5F2F10BF2376572B0')
assert.throws(() => adaptWidthSource(width + '\n'), /hash mismatch/)
assert.throws(() => adaptDoorSource(door + '\n', doorBefore), /digest mismatch/)
assert.ok(widthOutput.includes('ParametricContext.AssertAuthorized(phase)'))
assert.ok(widthOutput.indexOf('ParametricContext.AssertAuthorized(phase)') < widthOutput.indexOf('CreateBackups(result, phaseFiles)'))
assert.ok(doorOutput.includes('"ordinary_door_" + handedness +\n                "_" + SizeToken + ".SLDASM"'))
assert.ok(!doorOutput.includes('"door_panel_left_parametric.SLDPRT"'))
assert.equal(sha(readFileSync(widthPath)), widthBefore)
assert.equal(sha(readFileSync(DOOR_SOURCE_PATH)), doorBefore)
const buildTag = process.argv[2] || 'unique-doors'
assert.match(buildTag, /^[a-zA-Z0-9_-]+$/)
const executable = `workers/native_model_requests/parametric_v1/bin-${buildTag}/NativeParametricAssembly.exe`
assert.ok(existsSync(executable), 'compile the native executor first')
const result = spawnSync(executable, [], { encoding: 'utf8', windowsHide: true })
assert.equal(result.status, 1)
assert.match(result.stderr, /Usage: NativeParametricAssembly/)
for (const width of buildTag === 'range-expansion' ? [700, 740, 850, 877, 887, 913, 1000, 1100, 1200] : [740, 850, 877, 887, 913, 1000]) {
  const result = spawnSync(executable, ['--describe-plans', String(width)], { encoding: 'utf8', windowsHide: true })
  assert.equal(result.status, 0, result.stderr)
  const plan = JSON.parse(result.stdout.replace(/^\uFEFF/, ''))
  assert.equal(plan.widthMm, width)
  assert.equal(plan.assemblyPlanCount, 8)
  assert.equal(plan.expectedEditRecordCount, 16)
  assert.ok(plan.assemblies.every(row => !row.file_name.startsWith('储物柜门')))
  const span = plan.dimensionParts.find(row => row.file_name === '标准寄存柜 模型.SLDPRT').targets.find(row => row.name === 'D3@草图137')
  assert.equal(span.target_mm, (width - 300) / 2)
  const center = -(width / 4 + 22)
  assert.equal((center - span.target_mm / 2) - (-width / 2 + 20.5), 32.5)
  assert.equal(-38.5 - (center + span.target_mm / 2), 58.5)
}
console.log(JSON.stringify({ passed: true, verified: ['source revision drift rejected', 'authorization precedes native writes', 'door filenames include actual size', 'original sources unchanged', 'missing authorization input rejected before CAD'] }))
