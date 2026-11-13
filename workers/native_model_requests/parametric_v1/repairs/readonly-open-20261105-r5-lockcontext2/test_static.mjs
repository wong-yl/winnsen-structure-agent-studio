import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, mkdirSync, readdirSync, existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'

const repair = dirname(fileURLToPath(import.meta.url))
const baseline = resolve(repair, '../readonly-open-20261105-r5-locksession1')
const root = resolve(repair, '../../../../..')
const evidence = resolve(process.env.WINNSEN_LOCKCONTEXT2_EVIDENCE || resolve(root, 'data/review_portal_runtime/readonly_open_repair_20261105_1920/ac_recovery_20261106212518/continue4_20261107105542/lockcontext2_impl_20261107134304'))
const source = (folder, name) => readFileSync(resolve(folder, 'source', name), 'utf8').replaceAll('\r\n', '\n')
const sha = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const checks = {}
function check(name, condition) { assert.ok(condition, name); checks[name] = true }
function method(text, marker) {
  const start = text.indexOf(marker); assert.ok(start >= 0, marker)
  let i = text.indexOf('{', start), depth = 0
  for (; i < text.length; i++) { if (text[i] === '{') depth++; else if (text[i] === '}' && --depth === 0) return text.slice(start, i + 1) }
  throw Error('unbalanced method: ' + marker)
}
const locks = source(repair, 'NativeParametricLocks.cs'), old = source(baseline, 'NativeParametricLocks.cs')
const names = readdirSync(resolve(repair, 'source')).sort()
check('exactlyTenSources', names.length === 10 && names.every(name => name.endsWith('.cs')))
check('nineFrozenSourcesByteIdentical', names.filter(name => name !== 'NativeParametricLocks.cs').every(name => sha(resolve(repair, 'source', name)) === sha(resolve(baseline, 'source', name))))
check('buildOnlyTagChanged', readFileSync(resolve(repair, 'build.mjs'), 'utf8') === readFileSync(resolve(baseline, 'build.mjs'), 'utf8').replace("const tag = 'readonly-open-20261105-r5-locksession1'", "const tag = 'readonly-open-20261105-r5-lockcontext2'"))
for (const marker of ['internal static void RunIsolated(', 'private static void BeginIsolated(', 'private static void InspectOwnerOnly(', 'private static void CaptureLoaded(']) check('unchanged ' + marker, method(locks, marker) === method(old, marker))
const originalSide = method(old, 'private static RightPreparation RunSide('), currentSide = method(locks, 'private static RightPreparation RunSide(')
const generation = s => s.slice(s.indexOf('foreach (string name in LegacyPatternNames)'), s.indexOf('NativeParametricAssembly.Save(model);') + 'NativeParametricAssembly.Save(model);'.length)
const restored = generation(currentSide).split('\n').filter(line => !line.includes('RightSourceContextProof detachedContext =') && !line.includes('RightSourceContextProof sourceProof =') && !line.includes('if (sourceProof != null)')).join('\n')
check('originalDimensionSlotsAndPatternsUnchanged', restored === generation(originalSide))
check('detachBeforeD2ActualChange', currentSide.indexOf('DetachAuditedRightSourceContext(') < currentSide.indexOf('verticalDatum.SystemValue='))
const mirrorBody = s => s.slice(s.indexOf('Feature mirrorFeature=((PartDoc)model).MirrorPart2'), s.indexOf('int errors=0,warnings=0;', s.indexOf('Feature mirrorFeature=((PartDoc)model).MirrorPart2')))
check('linkedMirrorNativeBendsAndReflectedVerticesUnchanged', mirrorBody(locks) === mirrorBody(old) && locks.includes('.MirrorPart2(false,') && locks.includes('NativeParametricDoor.VerifyReflectedVertices(model,mirrored)'))
const inspect = method(locks, 'private static void InspectRightOnly(')
check('rightOnlyNeverPreloadsSourceOrMutates', (inspect.match(/NativeParametricAssembly\.Open\(/g) || []).length === 1 && inspect.includes('Open(sw, path, true)') && inspect.includes('sourcePreloaded = false') && !/\.(Save|GuardedSaveAs|ForceRebuild3|SetReadOnlyState)\(/.test(inspect))
check('openCountBeforeOpenAndGateInsideTry', inspect.indexOf('ReadLockOpenRecords().Length') < inspect.indexOf('NativeParametricAssembly.Open(') && inspect.indexOf('try') < inspect.indexOf('RequireNewCleanLockOpen('))
check('sourceReopenUsesCarriedCurrentProof', locks.includes('RequireDetachedRightSourceContext(model, records, right.ContextProof, "source-disk-reopen-before-mirror")'))
check('noGlobalBreakLockOwnerOrPermissionBypass', !/LockAllExternal|BreakAllExternal|SetReadOnlyState|SetFileAttributes|SetAttributes|SetOwner/.test(locks))
check('onlyAuditedManagerDeletes', (locks.match(/\.DeleteRelation\(/g) || []).length === 1 && locks.includes('foreach (object value in values) Require(manager.DeleteRelation((SketchRelation)value)'))
check('noNewInternalRelationEntityFramework', !locks.includes('swSketchRelationFilterType_e.swAll') && !locks.includes('GetPersistReference3'))
const auditPath = resolve(evidence, '../lock_source_ref_audit_202611071325/lock_source_references_20261107133008.json')
check('auditShaBound', sha(auditPath) === '086F86C7586B4BF0ECF01C8E1B11B03A409261FB2B14D8565C7BACD0E79A1BB8')
const frozen = JSON.parse(readFileSync(resolve(evidence, 'frozen_before.json'), 'utf8').replace(/^\uFEFF/, ''))
check('frozenOldRepairUnchanged', frozen.length === 12 && frozen.every(row => sha(row.path) === row.sha256))
const output = resolve(evidence, 'tests_' + Date.now()); mkdirSync(output, { recursive: false })
const start = locks.indexOf('        private sealed class RightSourceContextProof'), end = locks.indexOf('        private static void CaptureLoaded(', start)
const helpers = locks.slice(start, end) + '\n' + method(locks, 'private static void CaptureLoaded(')
check('newHelpersUseSingleComRelease', !helpers.includes('FinalReleaseComObject') && !/\bRelease\(/.test(helpers) && method(locks, 'private static void ContextRelease(').includes('Marshal.ReleaseComObject(value)'))
const naturalSource = method(locks, 'private static void RequireNaturallyLoadedRightSourceClean(')
check('naturalSourceChecksOnlyLoadedObject', naturalSource.includes('GetOpenDocumentByName(sourcePath)') && !/NativeParametricAssembly\.Open|OpenDoc[67]|ForceRebuild3|Save3/.test(naturalSource) && inspect.includes('RequireNaturallyLoadedRightSourceClean(sw, records, right.Source, stage)'))
const stubPath = resolve(repair, 'tests/LockContextStub.cs'), stub = readFileSync(stubPath, 'utf8')
assert.equal(stub.split('/* ACTUAL_HELPERS */').length, 2)
const cs = resolve(output, 'ActualLockContext.cs'), dll = resolve(output, 'ActualLockContext.dll')
writeFileSync(cs, stub.replace('/* ACTUAL_HELPERS */', helpers))
const compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const args = ['/nologo', '/target:library', '/platform:x64', '/codepage:65001', '/reference:System.Web.Extensions.dll', '/out:' + dll, cs]
const compiled = spawnSync(compiler, args, { encoding: 'utf8', windowsHide: true })
writeFileSync(resolve(output, 'compile.log'), compiled.stdout + compiled.stderr)
assert.equal(compiled.status, 0, compiled.stdout + compiled.stderr)
const ps = resolve(output, 'invoke_stub_tests.ps1')
writeFileSync(ps, `param([string]$Library,[string]$Root)\n$ErrorActionPreference='Stop'\n$assembly=[Reflection.Assembly]::LoadFrom($Library)\n$result=$assembly.GetType('NativeParametricLocks').GetMethod('RunTests').Invoke($null,@($Root))\n[Console]::WriteLine($result)\n`)
const run = spawnSync('C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe', ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', ps, '-Library', dll, '-Root', resolve(output, 'fixtures')], { encoding: 'utf8', windowsHide: true })
writeFileSync(resolve(output, 'run.stdout.txt'), run.stdout); writeFileSync(resolve(output, 'run.stderr.txt'), run.stderr)
assert.equal(run.status, 0, run.stdout + run.stderr)
const behavior = JSON.parse(run.stdout.trim()); assert.equal(behavior.pass, true); assert.ok(behavior.cases >= 45)
check('actualHelpersCoverDeleteAndRefusalBehavior', behavior.pass)
check('noFormalBuildDirectories', !existsSync(resolve(repair, '../../bin-readonly-open-20261105-r5-lockcontext2')) && !existsSync(resolve(repair, '../../build-readonly-open-20261105-r5-lockcontext2')))
const result = { at: new Date().toISOString(), pass: true, sourceHashes: names.map(name => ({ name, sha256: sha(resolve(repair, 'source', name)) })), checks,
  helperSha256: createHash('sha256').update(helpers).digest('hex').toUpperCase(), fixtureSha256: sha(stubPath),
  compiled: { compiler, args, exitCode: compiled.status, source: cs, sourceSha256: sha(cs), library: dll, librarySha256: sha(dll) },
  behavior, runExitCode: run.status, output, cadStarted: false, productionMainInvoked: false, newExeInvoked: false }
writeFileSync(resolve(output, 'result.json'), JSON.stringify(result, null, 2) + '\n')
writeFileSync(resolve(evidence, 'latest-static-result.json'), JSON.stringify({ path: resolve(output, 'result.json'), sha256: sha(resolve(output, 'result.json')) }, null, 2) + '\n')
console.log(JSON.stringify({ pass: true, checks: Object.keys(checks).length, behaviorCases: behavior.cases, output, cadStarted: false, productionMainInvoked: false, newExeInvoked: false }))
