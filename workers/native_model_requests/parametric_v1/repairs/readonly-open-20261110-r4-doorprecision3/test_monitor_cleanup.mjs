import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptPath = fileURLToPath(import.meta.url), folder = dirname(scriptPath)
const sourcePath = join(folder, 'source/NativeParametricWidth.cs')
const baselineFolder = resolve(folder, '../readonly-open-20261110-r4-doorprecision2')
const baselinePath = join(baselineFolder, 'source/NativeParametricWidth.cs')
const templatePath = join(folder, 'tests/WidthMonitorCleanupHarness.cs')
const root = resolve(folder, '../../../../..')
const hash = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const source = readFileSync(sourcePath, 'utf8'), baseline = readFileSync(baselinePath, 'utf8')
const inputPaths = [...readdirSync(join(folder, 'source')).map(name => join(folder, 'source', name)),
  scriptPath, templatePath, baselinePath, join(folder, 'build.mjs'),
  join(root, 'tools/lib/locker_16029_parametric_build.mjs'), join(root, 'tools/verify_16029_parametric_build.test.mjs')]
const inputArtifacts = inputPaths.map(path => ({ path, bytes: readFileSync(path) }))
const extracted = []

function unique(text, marker) {
  const start = text.indexOf(marker)
  assert.ok(start >= 0 && text.indexOf(marker, start + marker.length) < 0, 'missing or repeated production marker: ' + marker)
  return start
}

function capture(text, path, start, end, name) {
  const value = text.slice(start, end)
  extracted.push({ path, name, startLine: text.slice(0, start).split('\n').length,
    endLine: text.slice(0, end).split('\n').length, sha256: hash(value) })
  return value
}

function block(text, path, signature) {
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
  throw new Error('unclosed production block: ' + signature)
}

const changedMethods = [
  'private static bool OwnedSessionIsExclusive(',
  'private static void CaptureSessionSldProcMon(',
  'private static void CleanupSessionSldProcMon(',
  'private static void ForceStopOwnedSessionProcess(',
  'private static bool WaitForExactProcessExit('
]
const addedBlocks = [
  'private static bool MonitorIdentityMatches(',
  'private static bool ProcessIdentityMatches(',
  'private static bool ConfirmedProcessHasExited(',
  'public sealed class MonitorProcessIdentity'
]
let restored = source
for (const signature of changedMethods) restored = restored.replace(block(source, sourcePath, signature), () => block(baseline, baselinePath, signature))
for (const signature of addedBlocks) restored = restored.replace(block(source, sourcePath, signature), '')
const sessionIdentityField = '            public readonly List<MonitorProcessIdentity> sldprocmon_processes = new List<MonitorProcessIdentity>();'
assert.equal(restored.split(sessionIdentityField).length, 2)
restored = restored.replace(sessionIdentityField, '')
const baselineWithoutOldIdentityProbe = baseline.replace(block(baseline, baselinePath, 'private static bool ExactProcessIdentityExists('), '')
const normalized = value => value.replace(/^\uFEFF/, '').split(/\r?\n/).map(line => line.trimEnd()).filter(line => line.trim()).join('\n')
assert.equal(normalized(restored), normalized(baselineWithoutOldIdentityProbe), 'Width changes extend beyond the allowed process identity/capture/cleanup blocks')

const sourceComparisons = readdirSync(join(folder, 'source')).map(name => {
  const candidateSha256 = hash(readFileSync(join(folder, 'source', name)))
  const baselineSha256 = hash(readFileSync(join(baselineFolder, 'source', name)))
  return { name, candidateSha256, baselineSha256, identical: candidateSha256 === baselineSha256 }
})
assert.equal(sourceComparisons.length, 10)
assert.deepEqual(sourceComparisons.filter(row => !row.identical).map(row => row.name), ['NativeParametricWidth.cs'])
const gateSignature = 'if (result.success && (result.final_sldworks_process_ids.Count != 0 ||'
const finalGate = block(source, sourcePath, gateSignature)
assert.equal(finalGate, block(baseline, baselinePath, gateSignature), 'final global cleanup gate changed')
const enumerationFirst = 'result.final_sldworks_process_ids.AddRange(ProcessIds("SLDWORKS"));'
const enumerationLast = 'result.final_sldprocmon_process_ids.AddRange(ProcessIds("sldProcMon"));'
const enumeration = capture(source, sourcePath, unique(source, enumerationFirst), unique(source, enumerationLast) + enumerationLast.length, 'final_global_process_enumeration')
const methodSignatures = [
  ...changedMethods,
  'private static void CleanupOnlyCreatedProcesses(',
  ...addedBlocks.filter(signature => signature.startsWith('private ')),
  'private static List<int> ProcessIds(',
  'private static void AddUnique('
]
const replacements = {
  SESSION: block(source, sourcePath, 'public sealed class SessionRecord'),
  IDENTITY: block(source, sourcePath, 'public sealed class MonitorProcessIdentity'),
  METHODS: methodSignatures.map(signature => block(source, sourcePath, signature)).join('\n\n        '),
  ENUMERATE: enumeration,
  FINAL_GATE: finalGate
}
let generated = readFileSync(templatePath, 'utf8')
for (const [name, value] of Object.entries(replacements)) {
  const marker = '/*__' + name + '__*/'
  assert.equal(generated.split(marker).length, 2)
  generated = generated.replace(marker, () => value)
}
const runs = join(folder, 'tests/runs')
mkdirSync(runs, { recursive: true })
const output = mkdtempSync(join(runs, 'process-identity-'))
const generatedPath = join(output, 'process-identity.generated.cs'), executablePath = join(output, 'process-identity.exe')
writeFileSync(generatedPath, generated, { flag: 'wx' })
const compilerPath = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const args = ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/reference:System.Web.Extensions.dll', '/out:' + executablePath, generatedPath]
const compiled = spawnSync(compilerPath, args, { cwd: output, encoding: 'utf8', windowsHide: true, timeout: 30000 })
const report = { schema: 'winnsen.width_process_identity_tests.v1', pass: false,
  scope: 'Production Width process capture, identity, monitor and SLDWORKS cleanup/wait, and unchanged final global cleanup gate. Process and ProcessModule are pure in-memory substitutes; no OS processes or CAD are started or terminated.',
  inputArtifacts: inputArtifacts.map(file => ({ path: file.path, sha256: hash(file.bytes) })), sourceComparisons, extracted,
  staticChecks: { onlyWidthChangedFromPrecision2: true, onlyProcessBlocksChanged: true, finalGlobalGateUnchanged: true },
  compiler: { path: compilerPath, sha256: hash(readFileSync(compilerPath)), args },
  generated: { path: generatedPath, sha256: hash(generated) },
  compile: { exitCode: compiled.status, stdout: compiled.stdout || '', stderr: compiled.stderr || '' },
  limitations: ['In-memory process substitutes verify control flow and identity guards; actual Windows Process.Handle semantics and native CAD cleanup require separate runtime acceptance.'] }
if (compiled.status === 0) {
  const execution = spawnSync(executablePath, [], { cwd: output, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  report.execution = { executablePath, executableSha256: hash(readFileSync(executablePath)), exitCode: execution.status,
    stdout: execution.stdout || '', stderr: execution.stderr || '' }
  const line = execution.stdout?.split(/\r?\n/).find(value => value.startsWith('WIDTH_MONITOR_TEST_RESULT '))
  if (line) report.behavior = JSON.parse(line.slice('WIDTH_MONITOR_TEST_RESULT '.length))
}
report.inputArtifactsUnchanged = inputArtifacts.every(file => hash(readFileSync(file.path)) === hash(file.bytes))
report.pass = report.compile.exitCode === 0 && report.execution?.exitCode === 0 &&
  report.behavior?.failed === 0 && report.behavior.passed >= 60 && report.inputArtifactsUnchanged
const reportPath = join(output, 'report.json')
writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' })
process.stdout.write(JSON.stringify({ pass: report.pass, passed: report.behavior?.passed, failed: report.behavior?.failed, reportPath,
  compileErrors: report.compile.exitCode === 0 ? undefined : report.compile.stdout,
  failedCases: report.behavior?.cases.filter(row => row.status !== 'PASS') }) + '\n')
if (!report.pass) process.exitCode = 1
