import { createHash } from 'node:crypto'
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, writeFileSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { execFileSync, spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { validate16029ParametricPublication } from './lib/locker_16029_parametric_publication.mjs'
import * as productionEvidence from './lib/locker_16029_production_evidence.mjs'
import { acquireNativeWorkerInstanceLock } from './run_16029_native_worker.mjs'
import { PRODUCTION_DRAWINGS_BUILD_SCHEMA } from './build_16029_production_drawings.mjs'

export const PRODUCTION_DRAWINGS_PLAN_SCHEMA = 'winnsen.locker16029.production_drawings_plan.v1'
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const DATA = resolve(ROOT, 'data')
const SOURCE = resolve(ROOT, 'workers/native_model_requests/parametric_v1/ProductionOrderDrawings.cs')
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const sha = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const hash = path => sha(readFileSync(path))
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const key = path => resolve(path).toLowerCase()
const inside = (path, root) => { const rel = relative(resolve(root), resolve(path)); return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel)) }
const sameRows = (a, b) => JSON.stringify(a) === JSON.stringify(b)

function plainPath(path, type = 'file') {
  requireValue(typeof path === 'string' && isAbsolute(path), 'drawing plan requires absolute paths: ' + path)
  const full = resolve(path)
  let current = full
  while (true) {
    const stat = lstatSync(current)
    requireValue(!stat.isSymbolicLink() && (!stat.isFile() || stat.nlink === 1), 'drawing plan refuses linked paths: ' + current)
    if (current === full) requireValue(type === 'file' ? stat.isFile() : stat.isDirectory(), 'invalid drawing plan path type: ' + current)
    const parent = dirname(current)
    if (current === parent) break
    current = parent
  }
  return realpathSync.native(full)
}

export function read16029ProductionDrawingsPlan(planPath) {
  const path = plainPath(planPath), bytes = readFileSync(path), plan = JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, ''))
  const planSha256 = sha(bytes)
  requireValue(plan?.schema === PRODUCTION_DRAWINGS_PLAN_SCHEMA, 'invalid production drawing plan schema')
  const attempt = plainPath(plan.attempt, 'directory')
  requireValue(dirname(attempt) === resolve(DATA, 'parametric_attempts'), 'drawing plan requires a direct parametric attempt')
  requireValue(typeof plan.output === 'string' && isAbsolute(plan.output), 'fresh absolute drawing output directory required')
  const requestedOutput = resolve(plan.output)
  requireValue(!existsSync(requestedOutput), 'preserve prior drawing output: ' + requestedOutput)
  const output = resolve(plainPath(dirname(requestedOutput), 'directory'), basename(requestedOutput))
  requireValue(!inside(output, attempt) && !inside(attempt, output), 'drawing output must be separate from the accepted model attempt')
  requireValue(!inside(path, output), 'drawing plan cannot be inside its fresh output')
  const packageDirectory = plainPath(resolve(attempt, 'package'), 'directory')
  requireValue(Array.isArray(plan.entries) && plan.entries.length > 0, 'explicit drawing entries are required; templates are never guessed')
  const outNames = new Set()
  const entries = plan.entries.map((entry, index) => {
    const label = 'drawing entry ' + (index + 1)
    requireValue(entry && typeof entry === 'object' && !Array.isArray(entry), label + ' must be an object')
    for (const field of ['templatePath', 'sourceModelPath', 'candidateModelPath', 'configuration', 'outName']) requireValue(typeof entry[field] === 'string' && entry[field].trim() === entry[field] && entry[field], label + ' requires explicit ' + field)
    const templatePath = plainPath(entry.templatePath), sourceModelPath = plainPath(entry.sourceModelPath), candidateModelPath = plainPath(entry.candidateModelPath)
    requireValue(/\.SLDDRW$/i.test(templatePath), label + ' template must be an existing native SLDDRW')
    requireValue(/\.SLD(PRT|ASM)$/i.test(sourceModelPath) && /\.SLD(PRT|ASM)$/i.test(candidateModelPath), label + ' requires native source and candidate models')
    requireValue(/\.SLDASM$/i.test(sourceModelPath) === /\.SLDASM$/i.test(candidateModelPath), label + ' source/candidate model types differ')
    requireValue(inside(candidateModelPath, packageDirectory) && key(sourceModelPath) !== key(candidateModelPath), label + ' candidate must be in the accepted package and separate from source')
    requireValue(!inside(sourceModelPath, attempt) && !inside(templatePath, attempt), label + ' source model/template must be separate from the candidate attempt')
    requireValue(!inside(output, dirname(templatePath)) && !inside(output, dirname(sourceModelPath)), label + ' output cannot be inside protected source folders')
    requireValue(!/[\x00-\x1f]/.test(entry.configuration), label + ' configuration contains control characters')
    requireValue(/^[^<>:"/\\|?*\x00-\x1f]+\.SLDDRW$/i.test(entry.outName) && !/[. ]\.SLDDRW$/i.test(entry.outName) && !/^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)/i.test(entry.outName), label + ' outName must be a native drawing filename without a directory')
    requireValue(!outNames.has(entry.outName.toLowerCase()), 'duplicate drawing outName: ' + entry.outName)
    outNames.add(entry.outName.toLowerCase())
    return { templatePath, sourceModelPath, candidateModelPath, configuration: entry.configuration, outName: entry.outName }
  })
  requireValue(hash(path) === planSha256, 'drawing plan changed while it was being read')
  return { path, planSha256, attempt, output, packageDirectory, entries }
}

function requireOrder(publication) {
  const request = publication.request
  requireValue(request?.cabinet?.widthMm === 1200 && request.cabinet.heightMm === 1917 && request.cabinet.depthMm === 485, 'drawing authorization is limited to the 1200W1917H485D order')
  requireValue(Array.isArray(request.columns) && request.columns.length === 2, 'drawing order must contain L12/R13')
  for (const [index, side, heights] of [[0, 'L', [316, ...Array(11).fill(130)]], [1, 'R', [179, ...Array(12).fill(130)]]]) {
    const column = request.columns[index]
    requireValue(column?.side === side && sameRows(column.doors?.map(door => door.heightMm), heights), 'drawing order door mapping differs from authorized ' + side + ' column')
  }
}

function gates(plan) {
  const publication = validate16029ParametricPublication(plan.attempt)
  requireValue(publication.ok === true && publication.modelReady === true, 'drawing publication gate failed: ' + publication.reason)
  requireOrder(publication)
  requireValue(typeof productionEvidence.verify16029ProductionEvidence === 'function', 'production evidence verifier is unavailable; drawing runtime remains blocked')
  const production = productionEvidence.verify16029ProductionEvidence(plan.attempt, { throughPhase: 'package' })
  requireValue(production?.ok === true, 'drawing production evidence gate failed')
  return { publication, production }
}

function inventory(folder) {
  return productionEvidence.inventory16029ProductionFiles(folder)
}

function snapshot(plan, checked) {
  const files = new Map()
  for (const path of [plan.path, resolve(plan.attempt, 'parametric_execution.json'), checked.publication.archivePath, ...plan.entries.flatMap(entry => [entry.templatePath, entry.sourceModelPath]), ...inventory(plan.packageDirectory).map(row => row.path)]) files.set(key(path), { path, sha256: hash(plainPath(path)) })
  requireValue(files.get(key(plan.path)).sha256 === plan.planSha256, 'drawing plan changed after mapping validation')
  return [...files.values()].sort((a, b) => key(a.path).localeCompare(key(b.path)))
}

function verifyBuild(buildDirectory) {
  const folder = plainPath(buildDirectory, 'directory'), path = plainPath(resolve(folder, 'build.json')), build = read(path)
  requireValue(build.schema === PRODUCTION_DRAWINGS_BUILD_SCHEMA && key(build.executable) === key(resolve(folder, 'ProductionOrderDrawings.exe')), 'invalid production drawing build manifest')
  requireValue(key(build.source?.path) === key(SOURCE) && hash(plainPath(SOURCE)) === build.source.sha256, 'production drawing build source is stale')
  requireValue(build.copiedSource?.path && key(build.copiedSource.path) === key(resolve(folder, 'ProductionOrderDrawings.cs')) && build.copiedSource.sha256 === build.source.sha256, 'production drawing copied source binding missing')
  requireValue(Array.isArray(build.interop) && build.interop.length === 2 && ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll'].every(name => build.interop.filter(row => row.name === name && key(row.copiedPath) === key(resolve(folder, name)) && row.copiedSha256 === row.sha256).length === 1), 'production drawing interop binding missing')
  for (const row of [{ path: build.executable, sha256: build.executableSha256 }, build.copiedSource, ...(build.interop || []).flatMap(row => [{ path: row.path, sha256: row.sha256 }, { path: row.copiedPath, sha256: row.copiedSha256 }])]) requireValue(hash(plainPath(row.path)) === row.sha256, 'production drawing build artifact hash mismatch: ' + row.path)
  return { path, sha256: hash(path), build }
}

function requireIdle() {
  const count = execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', '@(Get-Process SLDWORKS -ErrorAction SilentlyContinue).Count'], { encoding: 'utf8', windowsHide: true }).trim()
  requireValue(count === '0', 'native CAD busy; drawing export requires an exclusive new session')
  const queue = resolve(DATA, 'native_model_requests')
  if (existsSync(queue)) for (const name of readdirSync(queue).filter(name => /^(NATIVE|PARAMETRIC)-.*\.json$/.test(name))) {
    const request = read(resolve(queue, name))
    requireValue(!['created', 'queued', 'claimed', 'planning', 'building', 'running', 'validating'].includes(request.nativeBuild?.state), 'native queue occupied: ' + name)
  }
}

export async function run16029ProductionDrawings(planPath, { execute = false, buildDirectory } = {}) {
  const plan = read16029ProductionDrawingsPlan(planPath), checked = gates(plan), before = snapshot(plan, checked)
  const packageBefore = inventory(plan.packageDirectory)
  if (!execute) return { status: 'preflight_pass', classification: 'engineering-review-only', productionReleased: false, cadStarted: false, ...plan, gates: checked, protectedFiles: before, boundary: 'Native drawing export, retained dimensions, reopened references, PDF pages and engineer review remain unverified.' }
  requireValue(process.platform === 'win32', 'production drawing runtime requires Windows and SOLIDWORKS 2020')
  const pinnedBuild = verifyBuild(buildDirectory)
  requireValue(!inside(plan.output, dirname(pinnedBuild.path)) && !inside(dirname(pinnedBuild.path), plan.output), 'drawing output and build must be separate')
  requireIdle()
  const lock = acquireNativeWorkerInstanceLock({ dataDir: DATA, workerId: 'production-drawings-' + process.pid })
  let heartbeatError = null
  const heartbeat = setInterval(() => { try { lock.heartbeat() } catch (error) { heartbeatError = error } }, 10000)
  let runResult = null, runtimeError = null, code = null, signal = null, outputCreated = false
  const evidencePath = resolve(plan.output, 'native-drawings.evidence.json')
  try {
    requireIdle()
    requireValue(sameRows(before, snapshot(plan, gates(plan))) && sameRows(packageBefore, inventory(plan.packageDirectory)), 'accepted drawing inputs changed after preflight')
    requireValue(hash(pinnedBuild.path) === pinnedBuild.sha256, 'drawing build manifest changed after preflight')
    verifyBuild(buildDirectory)
    mkdirSync(plan.output)
    outputCreated = true
    for (const name of ['native', 'pdf', 'pages']) mkdirSync(resolve(plan.output, name))
    const invocation = {
      schema: 'winnsen.locker16029.production_drawings_run.v1', classification: 'engineering-review-only', productionReleased: false,
      planPath: plan.path, planSha256: plan.planSha256, attempt: plan.attempt, output: plan.output, packageDirectory: plan.packageDirectory,
      executionPlanPath: resolve(plan.attempt, 'parametric_execution.json'), executionPlanSha256: checked.production.planSha256,
      archivePath: checked.publication.archivePath, archiveSha256: checked.publication.archiveSha256,
      entries: plan.entries, protectedFiles: before, packageInventory: packageBefore, productionEvidence: checked.production,
      buildManifestPath: pinnedBuild.path, buildManifestSha256: pinnedBuild.sha256,
      executablePath: pinnedBuild.build.executable, executableSha256: pinnedBuild.build.executableSha256,
    }
    const invocationPath = resolve(plan.output, 'run.json')
    writeFileSync(invocationPath, JSON.stringify(invocation, null, 2) + '\n', { flag: 'wx' })
    const child = spawn(pinnedBuild.build.executable, [invocationPath], { windowsHide: true, stdio: 'inherit' })
    const exited = await new Promise((done, fail) => { child.once('error', fail); child.once('exit', (exitCode, exitSignal) => done({ code: exitCode, signal: exitSignal })) })
    code = exited.code; signal = exited.signal
    runResult = existsSync(evidencePath) ? read(evidencePath) : null
    requireValue(code === 0 && runResult?.success === true && runResult?.status === 'exported_for_engineering_review', 'native drawing export failed: ' + (runResult?.error || code || signal || 'evidence missing'))
    requireValue(runResult.planSha256 === plan.planSha256 && runResult.modelsUnchanged === true && runResult.packageUnchanged === true && runResult.documentsClosed === true && runResult.sessionExited === true && runResult.classification === 'engineering-review-only' && runResult.productionReleased === false, 'native drawing acceptance evidence missing or mismatched')
    requireValue(runResult.jobs?.length === plan.entries.length && runResult.jobs.every((row, index) => {
      const entry = plan.entries[index]
      return row.outName === entry.outName && row.status === 'exported_for_engineering_review' && row.nativeDimensionsPreserved === true && row.templateNativeDimensionCount > 0 && row.candidateNativeDimensionCount === row.templateNativeDimensionCount && row.danglingDimensionCount === 0 && row.candidateLocalReferences === true && row.reopenedReadOnly === true && row.pages?.length > 0
    }), 'native drawing page/dimension/reference evidence incomplete')
    for (const row of runResult.jobs) for (const artifact of [row.drawing, row.pdf, ...row.pages.map(page => page.image), ...row.pages.map(page => page.pdf)]) requireValue(artifact && inside(artifact.path, plan.output) && hash(plainPath(artifact.path)) === artifact.sha256, 'drawing output artifact hash mismatch')
    if (heartbeatError) throw heartbeatError
  } catch (error) { runtimeError = error }
  finally {
    try {
      requireValue(before.every(row => hash(plainPath(row.path)) === row.sha256), 'source, template, archive, plan or accepted package changed during drawing export')
      requireValue(sameRows(packageBefore, inventory(plan.packageDirectory)), 'accepted package inventory changed during drawing export')
      const afterGates = gates(plan)
      requireValue(sameRows(afterGates.production, checked.production) && afterGates.publication.archiveSha256 === checked.publication.archiveSha256, 'accepted model gate binding changed during drawing export')
    } catch (error) { runtimeError = runtimeError ? new Error(runtimeError.message + '; ' + error.message) : error }
    clearInterval(heartbeat)
    try { lock.release() } catch (error) { runtimeError = runtimeError || error }
    if (outputCreated) writeFileSync(resolve(plan.output, 'runtime.json'), JSON.stringify({ schema: 'winnsen.locker16029.production_drawings_runtime.v1', success: !runtimeError, classification: 'engineering-review-only', productionReleased: false, planSha256: plan.planSha256, processExit: code, processSignal: signal, evidencePath, evidenceSha256: existsSync(evidencePath) ? hash(evidencePath) : null, error: runtimeError?.message || null, finishedAt: new Date().toISOString() }, null, 2) + '\n', { flag: 'wx' })
  }
  if (runtimeError) throw runtimeError
  return { status: runResult.status, classification: 'engineering-review-only', productionReleased: false, evidencePath, evidenceSha256: hash(evidencePath), planSha256: plan.planSha256, drawings: runResult.jobs.length, pages: runResult.jobs.reduce((sum, row) => sum + row.pages.length, 0) }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2)
    if (args.length === 1 && args[0] === '--help') console.log('node tools/draw_16029_production_order.mjs <absolute-plan.json> [--execute --build-dir <absolute-build-directory>]\nDefault: read-only preflight. Explicit SLDDRW/source/candidate/configuration/outName mappings are required. Output stays engineering-review-only.')
    else {
      const planPath = args.shift(); let execute = false, buildDirectory
      while (args.length) {
        const arg = args.shift()
        if (arg === '--execute' && !execute) execute = true
        else if (arg === '--build-dir' && !buildDirectory && args.length) buildDirectory = args.shift()
        else throw new Error('invalid production drawing argument: ' + arg)
      }
      requireValue(planPath && (execute ? Boolean(buildDirectory) : !buildDirectory), 'usage: node tools/draw_16029_production_order.mjs <absolute-plan.json> [--execute --build-dir <absolute-build-directory>]')
      console.log(JSON.stringify(await run16029ProductionDrawings(planPath, { execute, buildDirectory }), null, 2))
    }
  } catch (error) { console.error(error.message); process.exitCode = 1 }
}
