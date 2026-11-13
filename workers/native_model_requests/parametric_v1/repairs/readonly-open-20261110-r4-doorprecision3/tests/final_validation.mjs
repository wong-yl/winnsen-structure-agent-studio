import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const scriptPath = fileURLToPath(import.meta.url), folder = resolve(dirname(scriptPath), '..')
const root = resolve(folder, '../../../../..'), tag = 'readonly-open-20261110-r4-doorprecision3'
const nativeFolder = resolve(folder, '../..'), bin = join(nativeFolder, 'bin-' + tag), build = join(nativeFolder, 'build-' + tag)
const hash = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const behaviorPath = resolve(process.argv[2] || join(folder, 'tests/runs/process-identity-ZZdIIG/report.json'))
const numericPath = resolve(folder, '../readonly-open-20261110-r4-doorprecision2/tests/runs/numeric-roundtrip-vzgHJg/report.json')
const sourcePaths = readdirSync(join(folder, 'source')).map(name => join(folder, 'source', name))
const syntaxPaths = [join(folder, 'build.mjs'), join(folder, 'test_monitor_cleanup.mjs'), scriptPath,
  join(root, 'tools/lib/locker_16029_parametric_build.mjs'), join(root, 'tools/verify_16029_parametric_build.test.mjs')]
const artifactPaths = [...sourcePaths, ...syntaxPaths, join(folder, 'tests/WidthMonitorCleanupHarness.cs'), behaviorPath, numericPath,
  join(bin, 'NativeParametricAssembly.exe'), join(bin, 'build.json'), ...readdirSync(build).map(name => join(build, name))]
const inputs = artifactPaths.map(path => ({ path, sha256: hash(readFileSync(path)) }))
const behavior = JSON.parse(readFileSync(behaviorPath, 'utf8')), numeric = JSON.parse(readFileSync(numericPath, 'utf8'))
assert.equal(behavior.pass, true)
for (const sourcePath of sourcePaths) assert.equal(hash(readFileSync(sourcePath)), behavior.inputArtifacts.find(row => row.path === sourcePath)?.sha256)
const contextPath = join(folder, 'source/ParametricContext.cs'), contextSha256 = hash(readFileSync(contextPath))
assert.equal(numeric.pass, true)
assert.ok(numeric.inputArtifacts.some(row => row.path.endsWith('source\\ParametricContext.cs') && row.sha256 === contextSha256))
const { verify16029ParametricBuild } = await import(pathToFileURL(join(root, 'tools/lib/locker_16029_parametric_build.mjs')))
const verified = verify16029ParametricBuild(tag, { root, repairProfile: tag })
const outputParent = join(folder, 'tests/runs')
mkdirSync(outputParent, { recursive: true })
const output = mkdtempSync(join(outputParent, 'final-validation-'))
const checks = syntaxPaths.map(path => {
  const result = spawnSync(process.execPath, ['--check', path], { cwd: root, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  return { command: [process.execPath, '--check', path], exitCode: result.status, stdout: result.stdout || '', stderr: result.stderr || '' }
})
const fixtureArgs = ['tools/verify_16029_parametric_build.test.mjs']
const fixture = spawnSync(process.execPath, fixtureArgs, { cwd: root, encoding: 'utf8', windowsHide: true, timeout: 300000 })
const fixtureLog = join(output, 'build-selection-tests.log')
writeFileSync(fixtureLog, (fixture.stdout || '') + (fixture.stderr || ''), { flag: 'wx' })
const report = { schema: 'winnsen.precision3_final_validation.v1', pass: false, completedAtUtc: new Date().toISOString(),
  tag, executable: verified.executable, executableSha256: verified.manifest.executableSha256,
  manifestSha256: hash(readFileSync(join(bin, 'build.json'))), inputs,
  checks, buildIdentity: { command: 'verify16029ParametricBuild(tag, { root, repairProfile: tag })', pass: true, sourceCount: verified.manifest.sources.length },
  fixture: { command: [process.execPath, ...fixtureArgs], exitCode: fixture.status, log: fixtureLog, logSha256: hash(readFileSync(fixtureLog)) },
  behavior: { reportPath: behaviorPath, reportSha256: hash(readFileSync(behaviorPath)), passed: behavior.behavior.passed,
    failed: behavior.behavior.failed, staticChecks: behavior.staticChecks, sourceComparisons: behavior.sourceComparisons },
  reusedNumericEvidence: { reportPath: numericPath, reportSha256: hash(readFileSync(numericPath)), unchangedContextSha256: contextSha256,
    testedNumbers: numeric.matrix.testedNumbers, candidateMismatches: numeric.arms.candidate.mismatches,
    baselineControlMismatches: numeric.arms['baseline-negative-control'].mismatches },
  limitations: ['No CAD or OS process cleanup was exercised by this validation. Actual native model stages and physical acceptance remain with the primary agent.'] }
report.inputsUnchanged = inputs.every(row => hash(readFileSync(row.path)) === row.sha256)
report.pass = checks.every(row => row.exitCode === 0) && fixture.status === 0 && report.inputsUnchanged
const reportPath = join(output, 'report.json')
writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' })
process.stdout.write(JSON.stringify({ pass: report.pass, reportPath, executableSha256: report.executableSha256,
  behaviorPassed: report.behavior.passed, fixtureExitCode: fixture.status, inputsUnchanged: report.inputsUnchanged }) + '\n')
if (!report.pass) process.exitCode = 1
