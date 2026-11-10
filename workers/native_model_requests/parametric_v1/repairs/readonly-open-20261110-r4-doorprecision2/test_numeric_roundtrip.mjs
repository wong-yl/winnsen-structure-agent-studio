import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { build16029ParametricContract } from '../../../../../tools/lib/locker_16029_parametric_contract.mjs'

const scriptPath = fileURLToPath(import.meta.url), folder = dirname(scriptPath)
const sourcePath = join(folder, 'source/ParametricContext.cs')
const baselinePath = resolve(folder, '../readonly-open-20261110-r4-doorprecision1/source/ParametricContext.cs')
const templatePath = join(folder, 'tests/NumericRoundtripHarness.cs')
const contractPath = resolve(folder, '../../../../../tools/lib/locker_16029_parametric_contract.mjs')
const hash = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const inputArtifacts = [...readdirSync(join(folder, 'source')).map(name => join(folder, 'source', name)), scriptPath, baselinePath, templatePath, contractPath]
  .map(path => ({ path, bytes: readFileSync(path) }))
const source = readFileSync(sourcePath, 'utf8'), baseline = readFileSync(baselinePath, 'utf8')
const extracted = []

function unique(text, marker) {
  const start = text.indexOf(marker)
  assert.ok(start >= 0 && text.indexOf(marker, start + marker.length) < 0, 'missing or repeated production marker: ' + marker)
  return start
}

function capture(text, path, start, end, name) {
  const value = text.slice(start, end)
  extracted.push({ path, name, startLine: text.slice(0, start).split('\n').length, endLine: text.slice(0, end).split('\n').length, sha256: hash(value) })
  return value
}

function method(text, path, signature) {
  const start = unique(text, signature), open = text.indexOf('{', start + signature.length)
  let depth = 0, state = 'code'
  for (let index = open; index < text.length; index++) {
    const char = text[index], next = text[index + 1]
    if (state === 'line') { if (char === '\n') state = 'code'; continue }
    if (state === 'block') { if (char === '*' && next === '/') { state = 'code'; index++ }; continue }
    if (state === 'string' || state === 'char') {
      if (char === '\\') { index++; continue }
      if (char === (state === 'string' ? '"' : "'")) state = 'code'
      continue
    }
    if (char === '/' && next === '/') { state = 'line'; index++; continue }
    if (char === '/' && next === '*') { state = 'block'; index++; continue }
    if (char === '"') { state = 'string'; continue }
    if (char === "'") { state = 'char'; continue }
    if (char === '{') depth++
    if (char === '}' && --depth === 0) return capture(text, path, start, index + 1, signature)
  }
  throw new Error('unclosed production method: ' + signature)
}

const expected = new Set()
let validContracts = 0, rejectedContracts = 0, numericOccurrences = 0
function collect(value) {
  if (typeof value === 'number') {
    assert.ok(Number.isFinite(value), 'contract contains a non-finite number')
    expected.add(value)
    numericOccurrences++
  } else if (value && typeof value === 'object') {
    for (const child of Object.values(value)) collect(child)
  }
}
for (let heightMm = 1700; heightMm <= 2200; heightMm++) for (let count = 1; count <= 24; count++) {
  const request = { cabinet: { widthMm: 900, heightMm, depthMm: 500 }, columns: ['L', 'R'].map(side => ({ side,
    doors: Array.from({ length: count }, () => ({ heightUnits: 1 })) })) }
  let contract
  try { contract = build16029ParametricContract(request) } catch (error) {
    assert.match(error.message, /门高不足 100 mm/)
    rejectedContracts++
    continue
  }
  collect(contract)
  validContracts++
}
assert.equal(validContracts, 8481)
assert.equal(rejectedContracts, 3543)
assert.equal(numericOccurrences, 2455195)
assert.equal(expected.size, 213937)
const matrixUniqueNumbers = expected.size
const focused = [205.55555555555554, 100.0000005, 205.55555555, 603.0000001, 603.0000002, 1715.9999995]
for (const value of focused) expected.add(value)
const numbers = [...expected]
const payload = { values: numbers.map(value => ({ value })) }
const runs = join(folder, 'tests/runs')
mkdirSync(runs, { recursive: true })
const output = mkdtempSync(join(runs, 'numeric-roundtrip-'))
const inputPath = join(output, 'input-node-original.json')
writeFileSync(inputPath, JSON.stringify(payload) + '\n', { flag: 'wx' })
const jsonStart = unique(source, 'internal static readonly JavaScriptSerializer Json = ')
const common = {
  JSON: capture(source, sourcePath, jsonStart, source.indexOf(';', jsonStart) + 1, 'production_serializer_configuration'),
  READ: method(source, sourcePath, 'internal static Dictionary<string, object> Read('),
}
const compilerPath = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const report = { schema: 'winnsen.numeric_roundtrip_tests.v1', pass: false,
  matrix: { cabinetWidthMm: 900, cabinetDepthMm: 500, heightRangeMm: [1700, 2200], doorCounts: [1, 24], columns: 'both columns use equal positive heightUnits',
    validContracts, rejectedContracts, numericOccurrences, matrixUniqueNumbers, focusedNumbers: focused, testedNumbers: numbers.length },
  expectedOrigin: 'Original JavaScript numbers returned by build16029ParametricContract; every numeric field is traversed recursively before JSON.stringify. Expected values never pass through CLR conversion.',
  comparison: 'Node JSON.parse(CLR JavaScriptSerializer.Serialize(Number(JavaScriptSerializer.Deserialize(Node JSON.stringify(value))))) === original Node number',
  input: { path: inputPath, sha256: hash(readFileSync(inputPath)) }, inputArtifacts: inputArtifacts.map(file => ({ path: file.path, sha256: hash(file.bytes) })),
  compiler: { path: compilerPath, sha256: hash(readFileSync(compilerPath)) }, extracted, arms: {},
  limitations: ['No CAD, COM, assembly mutation or physical acceptance is exercised. The matrix covers all numeric contract fields for equal door counts and equal weights across the supported integer height interval, with six focused boundary values.'] }
for (const [arm, text, path] of [['candidate', source, sourcePath], ['baseline-negative-control', baseline, baselinePath]]) {
  const replacements = { ...common, NUMBER: method(text, path, 'internal static double Number(') }
  let generated = readFileSync(templatePath, 'utf8')
  for (const [name, value] of Object.entries(replacements)) {
    const marker = '/*__' + name + '__*/'
    assert.equal(generated.split(marker).length, 2)
    generated = generated.replace(marker, () => value)
  }
  const generatedPath = join(output, arm + '.generated.cs'), executablePath = join(output, arm + '.exe'), outputPath = join(output, arm + '.json')
  writeFileSync(generatedPath, generated, { flag: 'wx' })
  const args = ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/reference:System.Web.Extensions.dll', '/out:' + executablePath, generatedPath]
  const compiled = spawnSync(compilerPath, args, { cwd: output, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  const armResult = report.arms[arm] = { generated: { path: generatedPath, sha256: hash(generated) }, compile: { args, exitCode: compiled.status, stdout: compiled.stdout || '', stderr: compiled.stderr || '' } }
  if (compiled.status !== 0) continue
  const execution = spawnSync(executablePath, [inputPath, outputPath], { cwd: output, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  armResult.execution = { exitCode: execution.status, stdout: execution.stdout || '', stderr: execution.stderr || '', executableSha256: hash(readFileSync(executablePath)) }
  if (execution.status !== 0) continue
  const actual = JSON.parse(readFileSync(outputPath, 'utf8'))
  armResult.output = { path: outputPath, sha256: hash(readFileSync(outputPath)) }
  armResult.boxedTypes = actual.boxedTypes
  armResult.controls = actual.controls
  armResult.comparedNumbers = actual.values.length
  armResult.mismatches = 0
  armResult.examples = []
  for (let index = 0; index < numbers.length; index++) if (actual.values[index] !== numbers[index]) {
    armResult.mismatches++
    if (armResult.examples.length < 12) armResult.examples.push({ index, expected: numbers[index], actual: actual.values[index] })
  }
  armResult.focusedResults = focused.map(value => ({ expected: value, actual: actual.values[numbers.indexOf(value)], identical: actual.values[numbers.indexOf(value)] === value }))
}
const candidate = report.arms.candidate, control = report.arms['baseline-negative-control']
report.inputArtifactsUnchanged = inputArtifacts.every(file => hash(readFileSync(file.path)) === hash(file.bytes))
report.pass = candidate.compile.exitCode === 0 && candidate.execution?.exitCode === 0 && candidate.comparedNumbers === numbers.length &&
  candidate.mismatches === 0 && candidate.boxedTypes['System.Decimal'] > 0 &&
  JSON.stringify(candidate.controls) === JSON.stringify({ int: 123, long: 2147483648, double: 123.75 }) &&
  control.compile.exitCode === 0 && control.execution?.exitCode === 0 && control.comparedNumbers === numbers.length &&
  control.mismatches > 0 && control.focusedResults.find(value => value.expected === 205.55555555555554)?.identical === false && report.inputArtifactsUnchanged
const reportPath = join(output, 'report.json')
writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' })
process.stdout.write(JSON.stringify({ pass: report.pass, testedNumbers: numbers.length, candidateMismatches: candidate.mismatches, baselineControlMismatches: control.mismatches, reportPath }) + '\n')
if (!report.pass) process.exitCode = 1
