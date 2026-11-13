import { readFileSync, writeFileSync, mkdirSync, mkdtempSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'

const folder = dirname(fileURLToPath(import.meta.url))
const repository = resolve(folder, '../../../../..')
const sourcePath = join(folder, 'source/NativeParametricDoorPlacement.cs')
const contextPath = join(folder, 'source/ParametricContext.cs')
const templatePath = join(folder, 'tests/RearHoleHarness.cs')
const evidenceDirectory = resolve(repository, 'data/review_portal_runtime/readonly_open_repair_20261105_1920')
const evidencePath = join(evidenceDirectory, 'r3_rear_hole_tests.json')
const compilerPath = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const startedAt = new Date().toISOString()
const hash = value => createHash('sha256').update(value).digest('hex').toUpperCase()
mkdirSync(evidenceDirectory, { recursive: true })
writeFileSync(evidencePath, JSON.stringify({ schema: 'winnsen.rear_hole_behavior_tests.v1', pass: false, status: 'NOT_COMPLETED', startedAt }, null, 2) + '\n')
const sourceBytes = readFileSync(sourcePath)
const contextBytes = readFileSync(contextPath)
const source = sourceBytes.toString('utf8')
const context = contextBytes.toString('utf8')
const extracted = []

function capture(text, path, start, end) {
  const value = text.slice(start, end)
  extracted.push({ path, startLine: text.slice(0, start).split('\n').length, endLine: text.slice(0, end).split('\n').length, sha256: hash(value) })
  return value
}

function uniqueIndex(text, token) {
  const index = text.indexOf(token)
  if (index < 0 || text.indexOf(token, index + token.length) >= 0) throw new Error('source marker missing or ambiguous: ' + token)
  return index
}

function method(text, path, signature) {
  const start = uniqueIndex(text, signature)
  const open = text.indexOf('{', start + signature.length)
  let depth = 0
  let state = 'code'
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
    if (char === '}' && --depth === 0) return capture(text, path, start, index + 1)
  }
  throw new Error('unclosed source method: ' + signature)
}

const gateStart = uniqueIndex(source, 'ParametricContext.Require(holes.Count==(alreadyRepaired?0:2),"FEEDBACK_REAR_HOLE_COUNT_DRIFT");')
const gateEnd = source.indexOf('if(!alreadyRepaired) {', gateStart)
if (gateEnd < gateStart) throw new Error('pre-deletion pair gate end missing')
const replacements = {
  REAR_HOLES: method(source, sourcePath, 'private static List<Face2> FeedbackRearHoles('),
  PAIR_GATE: capture(source, sourcePath, gateStart, gateEnd),
  REQUIRE: method(context, contextPath, 'internal static void Require(')
}
let generated = readFileSync(templatePath, 'utf8')
for (const [name, value] of Object.entries(replacements)) {
  const token = '/*__' + name + '__*/'
  if (generated.split(token).length !== 2) throw new Error('template token missing or ambiguous: ' + token)
  generated = generated.replace(token, () => value)
}
if (/\/\*__[A-Z_]+__\*\//.test(generated)) throw new Error('unfilled template token')
const runtimeDirectory = mkdtempSync(join(evidenceDirectory, 'r3-rear-hole-behavior-'))
const generatedPath = join(runtimeDirectory, 'RearHoleHarness.generated.cs')
const executablePath = join(runtimeDirectory, 'RearHoleHarness.exe')
writeFileSync(generatedPath, generated, 'utf8')
const args = ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/reference:System.Web.Extensions.dll', '/out:' + executablePath, generatedPath]
const report = {
  schema: 'winnsen.rear_hole_behavior_tests.v1', pass: false, startedAt, sourceSha256: hash(sourceBytes),
  scope: 'Unmodified extracted FeedbackRearHoles plus extracted production pre-deletion count/pair gates compiled against geometric C# doubles; no CAD startup or deletion.',
  sourceArtifacts: [{ path: sourcePath, sha256: hash(sourceBytes) }, { path: contextPath, sha256: hash(contextBytes) }, { path: templatePath, sha256: hash(readFileSync(templatePath)) }, { path: fileURLToPath(import.meta.url), sha256: hash(readFileSync(fileURLToPath(import.meta.url))) }],
  extracted,
  generated: { path: generatedPath, sha256: hash(generated) },
  compiler: { path: compilerPath, sha256: hash(readFileSync(compilerPath)), args },
  limitations: [
    'Bodies, faces and surfaces are in-process doubles with synthetic geometry; native COM and real part topology are not exercised.',
    'Only FeedbackRearHoles and the production pre-deletion count/pair assertion block are extracted; actual face selection, patch/delete, save, envelope checks and physical reopen remain unverified.',
    'Retained front-skin faces are proven excluded by the target selector; separate retained-geometry comparison methods and the actual eight-face CAD preservation require independent evidence.',
    'The selector intentionally has no width-derived X restriction; exactly-two count and signed symmetric-X requirements are covered by the extracted pre-deletion gates.'
  ]
}
const compiled = spawnSync(compilerPath, args, { cwd: runtimeDirectory, encoding: 'utf8', windowsHide: true, timeout: 30000 })
report.compile = { exitCode: compiled.status, stdout: compiled.stdout || '', stderr: compiled.stderr || '', error: compiled.error ? String(compiled.error) : null }
if (compiled.status === 0) {
  report.executable = { path: executablePath, sha256: hash(readFileSync(executablePath)) }
  const result = spawnSync(executablePath, [], { cwd: runtimeDirectory, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  report.execution = { command: [executablePath], exitCode: result.status, stdout: result.stdout || '', stderr: result.stderr || '', error: result.error ? String(result.error) : null }
  const marker = 'REAR_HOLE_TEST_RESULT '
  const line = (result.stdout || '').split(/\r?\n/).find(line => line.startsWith(marker))
  if (line) report.behavior = JSON.parse(line.slice(marker.length))
}
report.sourcesUnchangedDuringRun = hash(readFileSync(sourcePath)) === hash(sourceBytes) && hash(readFileSync(contextPath)) === hash(contextBytes)
report.finishedAt = new Date().toISOString()
report.pass = report.compile.exitCode === 0 && report.execution?.exitCode === 0 && report.behavior?.failed === 0 && report.behavior?.passed > 0 && report.sourcesUnchangedDuringRun
report.status = report.pass ? 'PASS' : 'FAIL'
writeFileSync(evidencePath, JSON.stringify(report, null, 2) + '\n', 'utf8')
process.stdout.write(JSON.stringify({ pass: report.pass, status: report.status, passed: report.behavior?.passed || 0, failed: report.behavior?.failed || 0, sourcesUnchangedDuringRun: report.sourcesUnchangedDuringRun, sourceSha256: report.sourceSha256, evidencePath }) + '\n')
if (!report.pass) {
  process.stderr.write(report.compile.stdout + report.compile.stderr + (report.execution?.stdout || '') + (report.execution?.stderr || ''))
  process.exitCode = 1
}
