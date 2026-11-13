import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'

const folder = dirname(fileURLToPath(import.meta.url))
const repository = resolve(folder, '../../../../..')
const outputRoot = resolve(repository, 'data/review_portal_runtime/readonly_open_repair_20261105_1920/ac_recovery_20261106212518/continue4_20261107105542')
const output = join(outputRoot, 'monitor1_offline_' + new Date().toISOString().replace(/[-:.TZ]/g, ''))
mkdirSync(output)
const sourcePath = join(folder, 'source/NativeParametricDoor.cs')
const oldSourcePath = resolve(folder, '../readonly-open-20261105-r5/source/NativeParametricDoor.cs')
const assemblyPath = join(folder, 'source/NativeParametricAssembly.cs')
const oldAssemblyPath = resolve(folder, '../readonly-open-20261105-r5/source/NativeParametricAssembly.cs')
const compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const hash = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const sourceBytes = readFileSync(sourcePath)
const source = sourceBytes.toString('utf8')
const oldSource = readFileSync(oldSourcePath, 'utf8')
const extracted = []

function capture(signature, record = true, text = source) {
  const start = text.indexOf(signature)
  if (start < 0 || text.indexOf(signature, start + signature.length) >= 0) throw new Error('missing/ambiguous declaration: ' + signature)
  const open = text.indexOf('{', start + signature.length)
  let depth = 0, state = 'code'
  for (let i = open; i < text.length; i++) {
    const ch = text[i], next = text[i + 1]
    if (state === 'line') { if (ch === '\n') state = 'code'; continue }
    if (state === 'block') { if (ch === '*' && next === '/') { state = 'code'; i++ }; continue }
    if (state === 'string' || state === 'char') {
      if (ch === '\\') { i++; continue }
      if (ch === (state === 'string' ? '"' : "'")) state = 'code'
      continue
    }
    if (state === 'verbatim') { if (ch === '"' && next === '"') i++; else if (ch === '"') state = 'code'; continue }
    if (ch === '/' && next === '/') { state = 'line'; i++; continue }
    if (ch === '/' && next === '*') { state = 'block'; i++; continue }
    if (ch === '@' && next === '"') { state = 'verbatim'; i++; continue }
    if (ch === '"') { state = 'string'; continue }
    if (ch === "'") { state = 'char'; continue }
    if (ch === '{') depth++
    if (ch === '}' && --depth === 0) {
      const value = text.slice(start, i + 1)
      if (record) extracted.push({ signature, sourcePath, firstLine: text.slice(0, start).split('\n').length, sha256: hash(value) })
      return value
    }
  }
  throw new Error('unclosed declaration: ' + signature)
}

const replacements = {
  OWNED_SESSION: capture('internal sealed class OwnedSession'),
  OWNED_MONITOR_IDENTITY: capture('public sealed class OwnedMonitorIdentity'),
  SESSION_EVIDENCE: capture('public sealed class SessionEvidence'),
  PROCESS_IDS: capture('private static List<int> ProcessIds('),
  STOP_OWNED_SESSION: capture('private static void StopOwnedSession('),
  OWNED_CLEANUP_VALUE: capture('private static T OwnedCleanupValue<T>('),
  TRY_OWNED_CLEANUP_ACTION: capture('private static bool TryOwnedCleanupAction('),
  COLLECT_OWNED_SESSION_REFERENCES: capture('private static void CollectOwnedSessionReferences('),
}
const template = readFileSync(join(folder, 'tests/MonitorCleanupHarness.cs'), 'utf8')
let harness = template
for (const [name, value] of Object.entries(replacements)) {
  const token = '/* ' + name + ' */'
  if (!harness.includes(token)) throw new Error('missing harness token: ' + token)
  harness = harness.replace(token, value)
}
if (/\/\* [A-Z_]+ \*\//.test(harness)) throw new Error('unfilled harness token')
const staticChecks = {
  monitor_identity_helper_source_unchanged: hash(readFileSync(assemblyPath)) === hash(readFileSync(oldAssemblyPath)),
  natural_exit_120000ms_unchanged: capture('private static bool WaitForExit(', false) === capture('private static bool WaitForExit(', false, oldSource)
    && replacements.STOP_OWNED_SESSION.includes('WaitForExit(session.process_id, 120000)'),
  no_new_kill_calls_in_door_source: (source.match(/\.Kill\(/g) || []).length === (oldSource.match(/\.Kill\(/g) || []).length,
  build_guard_unchanged_except_tag: readFileSync(join(folder, 'build.mjs'), 'utf8').replace("const tag = 'readonly-open-20261105-r5-monitor1'", "const tag = 'readonly-open-20261105-r5'")
    === readFileSync(resolve(folder, '../readonly-open-20261105-r5/build.mjs'), 'utf8'),
}
function execute(name, code) {
  const cs = join(output, name + '.cs'), exe = join(output, name + '.exe')
  writeFileSync(cs, code, { flag: 'wx' })
  const compiled = spawnSync(compiler, ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', '/reference:System.Web.Extensions.dll', '/out:' + exe, cs], { encoding: 'utf8', windowsHide: true })
  writeFileSync(join(output, name + '.compile.log'), compiled.stdout + compiled.stderr, { flag: 'wx' })
  if (compiled.status !== 0) throw new Error('offline compiler failed: ' + name + ': ' + compiled.stdout + compiled.stderr)
  const run = spawnSync(exe, [], { encoding: 'utf8', windowsHide: true })
  writeFileSync(join(output, name + '.run.log'), run.stdout + run.stderr, { flag: 'wx' })
  const result = JSON.parse(run.stdout.trim())
  return { name, compilerExit: compiled.status, exitCode: run.status, harnessSha256: hash(code), executableSha256: hash(readFileSync(exe)), ...result }
}
const current = execute('actual_source', harness)
const processMutant = execute('regression_unfiltered_process_ids', harness.replace(replacements.PROCESS_IDS,
  replacements.PROCESS_IDS.replace('if (!process.HasExited) ids.Add(process.Id);', 'ids.Add(process.Id);')))
const lifecycleMutant = execute('regression_stale_empty', harness.replace(replacements.STOP_OWNED_SESSION,
  replacements.STOP_OWNED_SESSION.replace(/\r?\n            empty = remainingCad.Count == 0;/, '')))
const mutationChecks = {
  unfiltered_process_ids_rejected: processMutant.exitCode !== 0 && !processMutant.pass,
  stale_empty_rejected: lifecycleMutant.exitCode !== 0 && !lifecycleMutant.pass,
}
const result = {
  schema: 'winnsen.monitor_cleanup_offline_tests.v1', generatedAt: new Date().toISOString(),
  pass: current.pass && Object.values(staticChecks).every(Boolean) && Object.values(mutationChecks).every(Boolean),
  sourcePath, sourceSha256: hash(sourceBytes), output, extracted, staticChecks, mutationChecks,
  current, regressionMutants: [processMutant, lifecycleMutant],
  limitations: ['Process enumeration, COM, natural-exit wait and monitor helper are offline stand-ins.',
    'The actual monitor identity helper is unchanged and is not executed by this harness.',
    'No CAD start, OS process enumeration, OS process cleanup, or official bin/build output was performed.'],
}
writeFileSync(join(output, 'evidence.json'), JSON.stringify(result, null, 2) + '\n', { flag: 'wx' })
writeFileSync(join(outputRoot, 'monitor1_offline_tests.json'), JSON.stringify(result, null, 2) + '\n', { flag: 'wx' })
process.stdout.write(JSON.stringify({ pass: result.pass, cases: current.case_count, staticChecks, mutationChecks, output, evidence: join(outputRoot, 'monitor1_offline_tests.json') }) + '\n')
if (!result.pass) process.exitCode = 1
