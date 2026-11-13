import { readFileSync, writeFileSync, mkdirSync, mkdtempSync, existsSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'

const folder = dirname(fileURLToPath(import.meta.url))
const repository = resolve(folder, '../../../../..')
const testDirectory = join(folder, 'tests')
const assemblyPath = join(folder, 'source/NativeParametricAssembly.cs')
const contextPath = join(folder, 'source/ParametricContext.cs')
const templatePath = join(testDirectory, 'DocumentStateHarness.cs')
const evidencePath = resolve(repository, 'data/review_portal_runtime/readonly_open_repair_20261105_1920/document_gate_tests.json')
const compilerPath = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const startedAt = new Date().toISOString()
mkdirSync(dirname(evidencePath), { recursive: true })
writeFileSync(evidencePath, JSON.stringify({ schema: 'winnsen.document_state_behavior_tests.v1', pass: false, status: 'NOT_COMPLETED', startedAt }, null, 2) + '\n', 'utf8')
const hash = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const assemblyBytes = readFileSync(assemblyPath)
const contextBytes = readFileSync(contextPath)
const assembly = assemblyBytes.toString('utf8')
const context = contextBytes.toString('utf8')
const extracted = []

function capture(source, file, start, end) {
  const text = source.slice(start, end)
  extracted.push({ file, startLine: source.slice(0, start).split('\n').length, endLine: source.slice(0, end).split('\n').length, sha256: hash(text) })
  return text
}

function method(source, file, signature) {
  const start = source.indexOf(signature)
  if (start < 0 || source.indexOf(signature, start + signature.length) >= 0) throw new Error('method signature is missing or ambiguous: ' + signature)
  const open = source.indexOf('{', start + signature.length)
  let depth = 0
  let state = 'code'
  for (let index = open; index < source.length; index++) {
    const char = source[index]
    const next = source[index + 1]
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
    if (char === '}' && --depth === 0) return capture(source, file, start, index + 1)
  }
  throw new Error('unclosed method: ' + signature)
}

function declaration(source, file, text) {
  const start = source.indexOf(text)
  if (start < 0 || source.indexOf(text, start + text.length) >= 0) throw new Error('declaration is missing or ambiguous: ' + text)
  return capture(source, file, start, start + text.length)
}

const pathStart = context.indexOf('internal static void CheckPath(')
const pathEnd = context.indexOf('internal static Dictionary<string, object> Read(', pathStart)
if (pathStart < 0 || pathEnd < pathStart) throw new Error('context path guard and native declarations not found')
const replacements = {
  STATE_FIELDS: [
    declaration(assembly, assemblyPath, 'private static bool ReadOnlyInspection;'),
    declaration(assembly, assemblyPath, 'private static readonly List<object> DocumentStates = new List<object>();')
  ].join('\n'),
  OPEN: method(assembly, assemblyPath, 'internal static ModelDoc2 Open('),
  SAVE: method(assembly, assemblyPath, 'internal static void Save('),
  CONTEXT_PATH: capture(context, contextPath, pathStart, pathEnd),
  REQUIRE: method(context, contextPath, 'internal static void Require(')
}
let generated = readFileSync(templatePath, 'utf8')
for (const [name, value] of Object.entries(replacements)) {
  const token = '/*__' + name + '__*/'
  if (generated.split(token).length !== 2) throw new Error('template token is missing or duplicated: ' + name)
  generated = generated.replace(token, () => value)
}
if (/\/\*__[A-Z_]+__\*\//.test(generated)) throw new Error('unfilled template token')

const runtime = mkdtempSync(join(testDirectory, '.runtime-'))
const generatedPath = join(runtime, 'DocumentStateHarness.generated.cs')
const executablePath = join(runtime, 'DocumentStateHarness.exe')
writeFileSync(generatedPath, generated, 'utf8')
const args = ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/reference:System.Web.Extensions.dll', '/out:' + executablePath, generatedPath]
const report = {
  schema: 'winnsen.document_state_behavior_tests.v1',
  pass: false,
  sourceSha256: hash(assemblyBytes),
  startedAt,
  scope: 'Extracted production Open/Save and path-guard methods compiled against in-process C# doubles; no SOLIDWORKS or CAD writes.',
  sourceArtifacts: [
    { path: assemblyPath, sha256: hash(assemblyBytes) },
    { path: contextPath, sha256: hash(contextBytes) },
    { path: templatePath, sha256: hash(readFileSync(templatePath)) },
    { path: fileURLToPath(import.meta.url), sha256: hash(readFileSync(fileURLToPath(import.meta.url))) }
  ],
  extracted,
  generated: { path: generatedPath, sha256: hash(generated) },
  compiler: { path: compilerPath, sha256: existsSync(compilerPath) ? hash(readFileSync(compilerPath)) : null, args },
  limitations: [
    'ISldWorks, ModelDoc2, Feature and assembly/component objects are test doubles; native COM and real SOLIDWORKS behavior are not exercised.',
    'ReadOnlyInspection is controlled directly in the harness; RunHeightBraceSessions process isolation, lifecycle and finally reset require separate evidence.',
    'Production Require and CheckPath are extracted unchanged; authorization and evidence writes are in-memory doubles, so leases and disk evidence persistence are not covered.',
    'Mixed-session readOnly=true with no warning may reuse an actually writable document by the existing contract; strict actual-readonly enforcement is tested only for isolated inspection and warning 128.',
    'Stub Save3 performs no file write; physical save/reopen, CAD validity and engineering acceptance remain unverified.'
  ]
}

const compiled = spawnSync(compilerPath, args, { cwd: runtime, encoding: 'utf8', windowsHide: true, timeout: 30000 })
report.compile = { exitCode: compiled.status, stdout: compiled.stdout || '', stderr: compiled.stderr || '', error: compiled.error ? String(compiled.error) : null }
if (compiled.status === 0) {
  report.executable = { path: executablePath, sha256: hash(readFileSync(executablePath)) }
  const executed = spawnSync(executablePath, [], { cwd: runtime, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  report.execution = { command: [executablePath], exitCode: executed.status, stdout: executed.stdout || '', stderr: executed.stderr || '', error: executed.error ? String(executed.error) : null }
  const marker = 'DOCUMENT_GATE_TEST_RESULT '
  const result = (executed.stdout || '').split(/\r?\n/).find(line => line.startsWith(marker))
  if (result) report.behavior = JSON.parse(result.slice(marker.length))
}
report.sourcesUnchangedDuringRun = hash(readFileSync(assemblyPath)) === hash(assemblyBytes) && hash(readFileSync(contextPath)) === hash(contextBytes)
report.finishedAt = new Date().toISOString()
report.status = report.compile.exitCode === 0 && report.execution?.exitCode === 0 && report.behavior?.failed === 0 && report.behavior?.passed > 0 && report.sourcesUnchangedDuringRun ? 'PASS' : 'FAIL'
report.pass = report.status === 'PASS'
mkdirSync(dirname(evidencePath), { recursive: true })
writeFileSync(evidencePath, JSON.stringify(report, null, 2) + '\n', 'utf8')
process.stdout.write(JSON.stringify({ status: report.status, passed: report.behavior?.passed || 0, failed: report.behavior?.failed || 0, sourcesUnchangedDuringRun: report.sourcesUnchangedDuringRun, evidencePath, sourceSha256: report.sourceArtifacts[0].sha256 }) + '\n')
if (report.status !== 'PASS') {
  process.stderr.write((report.compile.stdout || '') + (report.compile.stderr || '') + (report.execution?.stderr || '') + (report.execution?.stdout || ''))
  process.exitCode = 1
}
