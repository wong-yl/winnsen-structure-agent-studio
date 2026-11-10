import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { build16029ParametricContract } from '../../../../../tools/lib/locker_16029_parametric_contract.mjs'

const folder = dirname(fileURLToPath(import.meta.url))
const sourcePath = join(folder, 'source/NativeParametricAssembly.cs')
const placementPath = join(folder, 'source/NativeParametricDoorPlacement.cs')
const contextPath = join(folder, 'source/ParametricContext.cs')
const templatePath = join(folder, 'tests/DoorPrecisionHarness.cs')
const sha = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const sourceFiles = [...readdirSync(join(folder, 'source')).map(name => join(folder, 'source', name)), templatePath, fileURLToPath(import.meta.url)]
  .map(path => ({ path, bytes: readFileSync(path) }))
const assembly = readFileSync(sourcePath, 'utf8'), placement = readFileSync(placementPath, 'utf8'), context = readFileSync(contextPath, 'utf8')
const extracted = []

function unique(text, marker) {
  const start = text.indexOf(marker)
  assert.ok(start >= 0 && text.indexOf(marker, start + marker.length) < 0, 'missing or repeated production marker: ' + marker)
  return start
}

function capture(text, path, start, end, name) {
  const value = text.slice(start, end)
  extracted.push({ name, path, startLine: text.slice(0, start).split('\n').length, endLine: text.slice(0, end).split('\n').length, sha256: sha(value) })
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
    if (state === 'verbatim') {
      if (char === '"' && next === '"') { index++; continue }
      if (char === '"') state = 'code'
      continue
    }
    if (char === '/' && next === '/') { state = 'line'; index++; continue }
    if (char === '/' && next === '*') { state = 'block'; index++; continue }
    if (char === '@' && next === '"') { state = 'verbatim'; index++; continue }
    if (char === '"') { state = 'string'; continue }
    if (char === "'") { state = 'char'; continue }
    if (char === '{') depth++
    if (char === '}' && --depth === 0) return capture(text, path, start, index + 1, signature)
  }
  throw new Error('unclosed production method: ' + signature)
}

method(assembly, sourcePath, 'private static int Main(')
const dispatchStart = unique(assembly, 'if (doorPhase)')
const dispatchEnd = assembly.indexOf('var rows = new List<object>();', dispatchStart)
const dispatch = capture(assembly, sourcePath, dispatchStart, dispatchEnd, 'production_door_phase_dispatch')
const directoryStart = unique(placement, 'string directoryName = Path.GetFileName(directory);')
const directoryEnd = placement.indexOf(';', unique(placement, 'double height = ResolveContractDoorHeight(directoryName.Substring(1))')) + 1
const directory = capture(placement, placementPath, directoryStart, directoryEnd, 'production_placement_directory_resolution')
assert.ok(dispatch.includes('ResolveContractDoorHeight(phase.Substring(5))') && dispatch.includes('NativeParametricDoor.Generate(height, evidence)'))
assert.ok(!/double\.(?:Parse|TryParse)\([^;]*(?:phase|directory)/.test(assembly + placement))
assert.equal((assembly.match(/NativeParametricAssembly\.DoorHeightToken\(/g) || []).length, 2, 'SizeToken and Generate must use the protected plan identifiers')
assert.ok(assembly.includes('string height = DoorHeightToken(ParametricContext.Number(row, "doorHeightMm"))'))
assert.ok(placement.includes('"_H" + DoorHeightToken(height)'))

const replacements = {
  METHODS: ['internal static string DoorHeightToken(', 'internal static Dictionary<string, double> ContractDoorHeights(', 'internal static double ResolveContractDoorHeight(']
    .map(signature => method(assembly, sourcePath, signature)).join('\n\n'),
  OBJECT: method(context, contextPath, 'internal static Dictionary<string, object> Object('),
  NUMBER: method(context, contextPath, 'internal static double Number('),
  REQUIRE: method(context, contextPath, 'internal static void Require('),
  DISPATCH: dispatch,
  DIRECTORY: directory,
}
let generated = readFileSync(templatePath, 'utf8')
for (const [name, value] of Object.entries(replacements)) {
  const marker = '/*__' + name + '__*/'
  assert.equal(generated.split(marker).length, 2)
  generated = generated.replace(marker, () => value)
}
assert.ok(!/\/\*__[A-Z_]+__\*\//.test(generated))

const tokenFor = height => Number(height.toFixed(6)).toString().replace('.', 'p')
const planFor = request => {
  const contract = build16029ParametricContract(request)
  return { contract, doorModules: [...new Set([...contract.rowsByColumn.L, ...contract.rowsByColumn.R].map(row => row.doorHeightMm))]
    .map(heightMm => ({ token: tokenFor(heightMm), heightMm })) }
}
const requestFor = (left, right = left, heightMm = 1917) => ({ cabinet: { widthMm: 900, heightMm, depthMm: 500 },
  columns: [{ side: 'L', doors: left }, { side: 'R', doors: right }] })
const explicit = heights => heights.map(heightMm => ({ heightMm }))
const units = count => Array.from({ length: count }, () => ({ heightUnits: 1 }))
const valid = [
  { name: 'automatic-900x2000x500-L4-R9', plan: planFor(requestFor(units(4), units(9), 2000)) },
  { name: 'literal-205p55555555', plan: planFor(requestFor(explicit([205.55555555, 1610.44444445]))) },
  { name: 'historical-explicit-heightMm', plan: planFor(requestFor(explicit(Array(4).fill(471.25)), explicit([...Array(8).fill(205.555), 205.56]), 2000)) },
  { name: 'js-dotnet-midpoint-difference', plan: planFor(requestFor(explicit([100.0000005, 1715.9999995]))) },
]
assert.equal(valid[3].plan.doorModules[0].token, '100')
const invalid = []
const invalidCase = (name, failure, change) => {
  const plan = structuredClone(valid[1].plan)
  change(plan)
  invalid.push({ name, failure, plan })
}
for (const [name, value] of [['null', null], ['string', '205p555556'], ['number', 205], ['object', {}]]) {
  invalidCase('invalid-module-map-' + name, 'PARAMETRIC_DOOR_MODULE_MAP_INVALID', plan => { plan.doorModules = value })
}
invalidCase('missing-module-map', 'PARAMETRIC_DOOR_MODULE_MAP_INVALID', plan => { delete plan.doorModules })
invalidCase('empty-module-map', 'PARAMETRIC_DOOR_MODULE_COVERAGE_INVALID', plan => { plan.doorModules = [] })
invalidCase('missing-height', 'PARAMETRIC_DOOR_MODULE_MAP_INVALID', plan => { delete plan.doorModules[0].heightMm })
invalidCase('extra-module-field', 'PARAMETRIC_DOOR_MODULE_MAP_INVALID', plan => { plan.doorModules[0].extra = true })
invalidCase('string-height', 'PARAMETRIC_DOOR_MODULE_MAP_INVALID', plan => { plan.doorModules[0].heightMm = String(plan.doorModules[0].heightMm) })
invalidCase('rounded-height-rejected', 'PARAMETRIC_DOOR_MODULE_HEIGHT_NOT_IN_CONTRACT', plan => { plan.doorModules[0].heightMm = Number(plan.doorModules[0].heightMm.toFixed(6)) })
invalidCase('missing-contract-height', 'PARAMETRIC_DOOR_MODULE_COVERAGE_INVALID', plan => { plan.doorModules.pop() })
invalidCase('token-collision-distinct-heights', 'PARAMETRIC_DOOR_HEIGHT_TOKEN_COLLISION', plan => { plan.doorModules[1].token = plan.doorModules[0].token })
invalidCase('duplicate-height-different-tokens', 'PARAMETRIC_DOOR_HEIGHT_DUPLICATE_MODULE', plan => { plan.doorModules.push({ token: '999', heightMm: plan.doorModules[0].heightMm }) })
invalidCase('duplicate-module', 'PARAMETRIC_DOOR_HEIGHT_TOKEN_COLLISION', plan => { plan.doorModules.push({ ...plan.doorModules[0] }) })
invalidCase('foreign-module-height', 'PARAMETRIC_DOOR_MODULE_HEIGHT_NOT_IN_CONTRACT', plan => { plan.doorModules.push({ token: '999', heightMm: 999 }) })
invalidCase('invalid-contract-height', 'PARAMETRIC_DOOR_HEIGHT_INVALID', plan => { plan.contract.rowsByColumn.R[0].doorHeightMm = 0 })
for (const token of ['', '../205', 'H205', '0205', '205p1234567', '205\n']) {
  invalidCase('invalid-token-' + JSON.stringify(token), 'PARAMETRIC_DOOR_MODULE_TOKEN_INVALID', plan => { plan.doorModules[0].token = token })
}
const fixtures = { valid, invalid }
const expectedCaseCount = valid.reduce((sum, value) => sum + 4 + value.plan.doorModules.length * 4, 0) + invalid.length
const runs = join(folder, 'tests/runs')
mkdirSync(runs, { recursive: true })
const output = mkdtempSync(join(runs, 'door-precision-'))
const generatedPath = join(output, 'DoorPrecisionHarness.generated.cs'), fixturePath = join(output, 'fixtures.json')
const executablePath = join(output, 'DoorPrecisionHarness.exe'), reportPath = join(output, 'report.json')
writeFileSync(generatedPath, generated, { flag: 'wx' })
writeFileSync(fixturePath, JSON.stringify(fixtures, null, 2) + '\n', { flag: 'wx' })
const compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const args = ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/reference:System.Web.Extensions.dll', '/out:' + executablePath, generatedPath]
const compiled = spawnSync(compiler, args, { cwd: output, encoding: 'utf8', windowsHide: true, timeout: 30000 })
const report = { schema: 'winnsen.door_precision_behavior_tests.v1', pass: false, expectedCaseCount,
  sourceArtifacts: sourceFiles.map(file => ({ path: file.path, sha256: sha(file.bytes) })), extracted,
  generated: { path: generatedPath, sha256: sha(generated) }, fixture: { path: fixturePath, sha256: sha(readFileSync(fixturePath)) },
  compiler: { path: compiler, args, exitCode: compiled.status, stdout: compiled.stdout || '', stderr: compiled.stderr || '' },
  limitations: ['Extracted production value-resolution methods and dispatch statements execute against synthetic contract plans and an in-process Generate recorder. No CAD, COM, document geometry, assembly placement or native package acceptance is exercised.'] }
if (compiled.status === 0) {
  const result = spawnSync(executablePath, [fixturePath], { cwd: output, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  report.execution = { exitCode: result.status, stdout: result.stdout || '', stderr: result.stderr || '', executableSha256: sha(readFileSync(executablePath)) }
  const marker = 'DOOR_PRECISION_TEST_RESULT '
  const line = (result.stdout || '').split(/\r?\n/).find(line => line.startsWith(marker))
  if (line) report.behavior = JSON.parse(line.slice(marker.length))
}
report.sourcesUnchangedDuringRun = sourceFiles.every(file => sha(readFileSync(file.path)) === sha(file.bytes))
report.pass = compiled.status === 0 && report.execution?.exitCode === 0 && report.behavior?.failed === 0 &&
  report.behavior?.passed === expectedCaseCount && new Set(report.behavior?.cases?.map(value => value.name)).size === expectedCaseCount && report.sourcesUnchangedDuringRun
writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' })
process.stdout.write(JSON.stringify({ pass: report.pass, passed: report.behavior?.passed || 0, failed: report.behavior?.failed || 0, expectedCaseCount, reportPath }) + '\n')
if (!report.pass) {
  process.stderr.write(compiled.stdout + compiled.stderr + (report.execution?.stderr || ''))
  for (const value of report.behavior?.cases || []) if (value.status !== 'PASS') process.stderr.write(JSON.stringify(value) + '\n')
  process.exitCode = 1
}
