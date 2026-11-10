// Throwaway isolated single-reference experiment. Only explicit --run-native executes CAD.
import { createHash, randomBytes } from 'node:crypto'
import { readFileSync, writeFileSync, lstatSync, readdirSync, mkdirSync, existsSync, copyFileSync, constants } from 'node:fs'
import { dirname, relative, isAbsolute, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { spawn, spawnSync } from 'node:child_process'
const ROOT = 'D:\\Winnsen_Structure_Agent_Studio'
const HERE = dirname(fileURLToPath(import.meta.url)), PROBE = resolve(ROOT, 'data/indoor_workbench_repair_20261110/obsolete_door_probe')
const ATTEMPT = resolve(ROOT, 'data/parametric_attempts/PARAM-20261110111643911-CE439B'), CANDIDATE = resolve(HERE, 'candidate_attempt'), PACK = resolve(CANDIDATE, 'native_cad/working_pack')
const PLAN = resolve(HERE, 'plan.json')
const RUNNING = ['created', 'claimed', 'planning', 'building', 'validating', 'cancel_requested']
export const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const need = (value, message) => { if (!value) throw Error(message) }
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b)
const put = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2) + '\n', { flag: 'wx' })
const under = (root, path) => { const rel = relative(root, path); return rel && !rel.startsWith('..') && !isAbsolute(rel) }
const stamp = () => new Date().toISOString().replace(/[^0-9]/g, '')
export const activePortal = rows => rows.filter(row => RUNNING.includes(row.nativeBuild?.state) || row.nativeBuild?.lease != null)
function regular(path) {
  const info = lstatSync(path); need(info.isFile() && !info.isSymbolicLink() && info.nlink === 1, 'REGULAR_SINGLE_LINK_FILE_REQUIRED ' + path)
  for (let current = resolve(path); ; current = dirname(current)) { need(!lstatSync(current).isSymbolicLink(), 'LINKED_PATH_REFUSED ' + current); if (dirname(current) === current) break }
  return { path: resolve(path), sha256: hash(path), size: info.size }
}
function walk(folder, skipRuns = false) {
  need(lstatSync(folder).isDirectory() && !lstatSync(folder).isSymbolicLink(), 'LINKED_FOLDER_REFUSED ' + folder)
  return readdirSync(folder, { withFileTypes: true }).flatMap(entry => { const path = resolve(folder, entry.name); need(!entry.isSymbolicLink(), 'LINKED_ENTRY_REFUSED ' + path); if (skipRuns && (path === resolve(HERE, 'runs') || path === CANDIDATE)) return []; return entry.isDirectory() ? walk(path, skipRuns) : [regular(path)] })
}
function unchanged(rows) { for (const row of rows) need(existsSync(row.path) && hash(row.path) === row.sha256, 'PROTECTED_SOURCE_CHANGED ' + row.path) }
function processSnapshot() {
  const script = "$ErrorActionPreference='Stop'; $rows=@(Get-Process -Name SLDWORKS,sldProcMon,sldworks_fs -ErrorAction SilentlyContinue | ForEach-Object { @{pid=$_.Id;name=$_.ProcessName;startTicks=$_.StartTime.ToUniversalTime().Ticks.ToString();executable=$_.Path} }); @{processes=$rows} | ConvertTo-Json -Depth 4 -Compress"
  const result = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 10000 })
  need(result.status === 0, result.stderr || 'PROCESS_SNAPSHOT_FAILED'); const state = JSON.parse(result.stdout)
  const protectedFs = state.processes.find(row => row.pid === 13080)
  if (protectedFs) need(protectedFs.name.toLowerCase() === 'sldworks_fs' && protectedFs.startTicks === '639298656825633090', 'PROTECTED_FS_IDENTITY_CHANGED')
  return { ...state, protectedFs, cadAndMonitorEmpty: state.processes.every(row => !['sldworks', 'sldprocmon'].includes(row.name.toLowerCase())) }
}
function portalFiles() { return readdirSync(resolve(ROOT, 'data/native_model_requests'), { withFileTypes: true }).filter(row => row.isFile() && row.name.endsWith('.json')).map(row => regular(resolve(ROOT, 'data/native_model_requests', row.name))).filter(row => { const value = read(row.path); return value.id && value.nativeBuild }) }
async function portalGate() {
  const before = portalFiles(), { createNativeTaskStore } = await import(pathToFileURL(resolve(ROOT, 'tools/lib/locker_16029_native_task_store.mjs')).href)
  const store = createNativeTaskStore({ dataDir: resolve(ROOT, 'data/native_model_requests') }), rows = await store.listSnapshot(); unchanged(before)
  need(!activePortal(rows).length, 'PORTAL_ACTIVE_TASK_OR_NONNULL_LEASE ' + activePortal(rows).map(row => row.id).join(','))
  return { files: before, rows: rows.map(row => ({ id: row.id, state: row.nativeBuild.state, lease: row.nativeBuild.lease, updatedAt: row.updatedAt })) }
}
function inventory(folder) { return walk(folder).filter(row => /\.SLD(?:PRT|ASM)$/i.test(row.path)).map(row => ({ path: row.path, sha256: row.sha256, size: row.size })).sort((a, b) => a.path.localeCompare(b.path)) }

async function preflight(stage, priorArgument) {
  need(resolve(process.cwd()).toLowerCase() === ROOT.toLowerCase() && HERE === resolve(PROBE, 'right_owner_rebind_v1'), 'ACTUAL_ROOT_AND_OWNED_DIRECTORY_REQUIRED')
  const frozenPath = resolve(HERE, 'freeze-manifest.json'), frozen = read(frozenPath); unchanged(frozen.artifacts)
  const plan = read(PLAN); need(plan.schema === 'right-owner-rebind-plan-v1' && plan.candidateRoot === CANDIDATE && plan.files.length === 120, 'PLAN_SCOPE_CHANGED')
  unchanged([plan.sourceBinding, plan.mirrorBinding, plan.fresh]); const binding = read(plan.sourceBinding.path)
  need(binding.inventory.length === 119 && binding.workingCount === 76 && binding.doorsCount === 30 && binding.sourcesCount === 13, 'FIXED_119_INVENTORY_CHANGED')
  unchanged([...binding.allAttemptFiles, ...binding.templatePins, binding.observer])
  for (const file of plan.files) need(hash(file.sourcePath) === file.sha256 && lstatSync(file.sourcePath).size === file.size, 'SOURCE_PIN_CHANGED ' + file.sourcePath)
  const build = read(resolve(HERE, 'build.json')); need(build.buildPassed === true, 'BUILD_REQUIRED'); unchanged(build.sourcePins)
  for (const artifact of build.artifacts) need(artifact.buildPassed === true && hash(artifact.executable) === artifact.executableSha256, 'EXE_CHANGED')
  const old = read(resolve(ATTEMPT, 'research/protected-after-failure.json'))
  need(old.unchanged === true && old.rows.length === 19313 && old.rows.every(row => row.beforeSha256 === row.afterSha256 && !row.error), 'FAILED_ATTEMPT_PROTECTION_LIST_CHANGED')
  const protectedRows = [...new Map([...old.rows.map(row => regular(row.path)), ...walk(ATTEMPT), ...frozen.artifacts.map(row => regular(row.path)), regular(frozenPath), regular(resolve(PROBE, '../PROGRESS.md')), ...binding.templatePins.map(row => regular(row.path)), regular(binding.observer.path), regular(plan.sourceBinding.path), regular(plan.mirrorBinding.path), regular(plan.fresh.path)].map(row => [row.path.toLowerCase(), row])).values()]
  let prior = null, candidatePins = null
  if (stage === 'relink') need(!priorArgument && !existsSync(CANDIDATE), 'PRESERVE_EXISTING_CANDIDATE_OR_INVALID_PRIOR')
  else {
    need(priorArgument, 'PRIOR_STAGE_RESULT_REQUIRED'); const priorPath = resolve(priorArgument)
    need(under(resolve(HERE, 'runs'), priorPath), 'PRIOR_RESULT_MUST_BE_OWNED'); prior = read(priorPath)
    need(prior.schema === 'right-owner-stage-result-v1' && prior.stage === (stage === 'repair' ? 'relink' : 'repair') && prior.observationCompleted === true && prior.candidateRoot === CANDIDATE && prior.planSha256 === hash(PLAN), 'PRIOR_STAGE_NOT_VALID_FOR_CANDIDATE')
    unchanged(prior.candidatePins); unchanged(prior.evidencePins); candidatePins = prior.candidatePins
    const actual = inventory(CANDIDATE); need(same(actual, [...candidatePins].sort((a, b) => a.path.localeCompare(b.path))), 'CANDIDATE_INVENTORY_CHANGED')
    protectedRows.push(regular(priorPath), ...prior.evidencePins)
  }
  const portal = await portalGate(), processState = processSnapshot(); need(processState.cadAndMonitorEmpty, 'CAD_OR_MONITOR_NOT_EMPTY')
  return { plan, binding, build, protectedRows, portal, process: processState, candidatePins, activeRight: stage === 'cold' ? prior.activeRight : plan.right }
}
async function runNative(stage, priorArgument) {
  const context = await preflight(stage, priorArgument), { acquireNativeWorkerInstanceLock } = await import(pathToFileURL(resolve(ROOT, 'tools/run_16029_native_worker.mjs')).href)
  // The finally block below uses this exact executable when checking native-child identity.
  context.build.executable = context.build.artifacts.find(row => row.kind === (stage === 'relink' ? 'relink' : 'owner')).executable
  const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(ROOT, 'data'), workerId: 'right-owner-rebind-' + stage + '-' + process.pid + '-' + randomBytes(3).toString('hex') })
  let timer, chain = Promise.resolve(), heartbeatError = null, observerRunning = false, nativeChildIdentity = null, nativeChildPid = null, nativeChildUnconfirmed = false, currentArmOut = null
  const pulse = () => { const next = chain.then(() => lock.heartbeat()); chain = next.catch(error => { heartbeatError ||= error }); return next }
  const out = resolve(HERE, 'runs', stamp() + '-' + stage + '-' + process.pid), results = []
  try {
    await portalGate(); need(processSnapshot().cadAndMonitorEmpty, 'CAD_OCCUPIED_BEFORE_STAGE'); unchanged(context.protectedRows)
    mkdirSync(out, { recursive: true }); put(resolve(out, 'protected-baseline.json'), { rows: context.protectedRows, process: context.process, portal: context.portal.rows, currentProtectionHashesCapturedNow: true, modelAccepted: false })
    timer = setInterval(() => { void pulse().catch(error => { heartbeatError ||= error }) }, 10000)
    if (stage === 'relink') {
      mkdirSync(CANDIDATE)
      for (const file of context.plan.files) { need(hash(file.sourcePath) === file.sha256, 'SOURCE_CHANGED_BEFORE_COPY'); mkdirSync(dirname(file.path), { recursive: true }); copyFileSync(file.sourcePath, file.path, constants.COPYFILE_EXCL); need(hash(file.path) === file.sha256, 'CLONE_BYTES_DIFFER') }
      context.candidatePins = inventory(CANDIDATE); need(context.candidatePins.length === 120, 'EXACT_120_CANDIDATE_REQUIRED')
    }
    const arms = stage === 'cold' ? ['cold-owner', 'cold-total', 'cold-right'] : [stage]
    for (const arm of arms) {
      await pulse(); if (heartbeatError) throw heartbeatError
      await portalGate(); need(processSnapshot().cadAndMonitorEmpty, 'CAD_OR_MONITOR_OCCUPIED_BEFORE_' + arm); unchanged(context.protectedRows)
      const armOut = resolve(out, arm); mkdirSync(armOut); const owner = lock.heartbeat(), configPath = resolve(armOut, 'runner.json'), manifestPath = resolve(armOut, 'manifest.json')
      const manifest = arm === 'relink' ? { sourcePack: ATTEMPT, candidatePack: CANDIDATE, files: context.plan.files } : { schema: 'right-owner-native-manifest-v1', ...Object.fromEntries(['candidateRoot','owner','right','rebound','total','left','parameterSource','rightRows'].map(key => [key, context.plan[key]])), activeRight: context.activeRight, candidatePins: context.candidatePins, sourcePins: context.plan.files.map(file => ({ path: file.sourcePath, sha256: file.sha256, size: file.size })) }
      put(manifestPath, manifest)
      const immutableCandidates = arm === 'relink' ? [] : context.candidatePins.filter(row => arm !== 'repair' || row.path.toLowerCase() !== context.plan.owner.toLowerCase())
      const config = { schema: 'right-owner-arm-v1', arm, root: ROOT, pack: PACK, executable: context.build.executable, out: armOut, output: resolve(armOut, 'probe.json'), immutable: [...context.protectedRows, ...immutableCandidates, regular(manifestPath)], lockOwner: resolve(lock.lockDir, 'owner.json'), lockToken: owner.token, lockWorker: owner.workerId, lockPid: owner.pid, maximumDiagnosticSeconds: 600, naturalExitSeconds: 45 }
      config.argv = arm === 'relink' ? [config.executable, 'relink', CANDIDATE, config.output, manifestPath] : [config.executable, arm, config.output, manifestPath, hash(manifestPath)]
      put(configPath, config); observerRunning = true; nativeChildUnconfirmed = true; nativeChildIdentity = null; nativeChildPid = null; currentArmOut = armOut
      const child = spawn('python', [resolve(HERE, 'observe.py'), configPath, hash(configPath)], { cwd: PACK, windowsHide: true, stdio: 'inherit' })
      const exitCode = await new Promise((done, reject) => { child.once('error', reject); child.once('close', done) }).finally(() => { observerRunning = false })
      await chain; if (heartbeatError) throw heartbeatError
      const sessionPath = resolve(armOut, 'session.json'); need(existsSync(sessionPath), 'OBSERVER_SESSION_EVIDENCE_MISSING'); const session = read(sessionPath); need(typeof session.nativeChildAlive === 'boolean', 'OBSERVER_CHILD_STATE_MISSING'); nativeChildIdentity = session.nativeChildAlive ? session.nativeChildIdentity : null; nativeChildPid = session.nativeChildAlive ? session.nativeChildPid : null; nativeChildUnconfirmed = session.nativeChildAlive && !nativeChildIdentity
      results.push({ arm, path: sessionPath, sha256: hash(sessionPath), pass: session.pass, timedOut: session.timedOut, modelAccepted: false })
      need(exitCode === 0 && session.pass === true && session.nextArmAllowed === true, 'OBSERVATION_FAILED_OR_TIMEOUT ' + arm)
      unchanged(context.protectedRows)
      const now = inventory(CANDIDATE); need(now.length === 120 && same(now.map(r => r.path), context.candidatePins.map(r => r.path)), 'CANDIDATE_FILE_SET_CHANGED')
      if (stage !== 'relink') unchanged(context.candidatePins.filter(row => stage !== 'repair' || row.path.toLowerCase() !== context.plan.owner.toLowerCase()))
      context.candidatePins = now
      if (stage === 'repair') {
        const events = read(config.output).events, saved = events.filter(row => row.inspection === 'owner_saved_sha256'), skipped = events.filter(row => row.inspection === 'no_rebind_needed')
        need(saved.length + skipped.length === 1 && saved.length <= 1 && skipped.length <= 1, 'REPAIR_DISPOSITION_MUST_BE_EXPLICIT')
        context.activeRight = saved.length === 1 ? context.plan.rebound : context.plan.right
      }
    }
    const evidencePins = walk(out).filter(row => !row.path.endsWith('result.json')), coldObservations = stage === 'cold' ? results.map(row => {
      const events = read(resolve(dirname(row.path), 'probe.json')).events, opens = events.filter(e => e.inspection === 'open_result' && e.arm === row.arm && e.requestedPath === (row.arm === 'cold-owner' ? context.plan.owner : row.arm === 'cold-total' ? context.plan.total : context.activeRight))
      need(opens.length === 1 && Number.isInteger(opens[0].warnings) && opens[0].errors === 0, 'EXACT_COLD_OPEN_RESULT_REQUIRED')
      return { arm: row.arm, errors: opens[0].errors, warnings: opens[0].warnings, bit1Cleared: (opens[0].warnings & 1) === 0, warningsZero: opens[0].warnings === 0 }
    }) : []
    const resultPath = resolve(out, 'result.json')
    put(resultPath, { schema: 'right-owner-stage-result-v1', stage, observationCompleted: true, activeRight: context.activeRight, rebindPerformed: context.activeRight === context.plan.rebound, candidateRoot: CANDIDATE, planSha256: hash(PLAN), candidatePins: context.candidatePins, evidencePins, sessions: results, coldObservations, allThreeBit1Cleared: stage === 'cold' ? coldObservations.length === 3 && coldObservations.every(row => row.bit1Cleared) : null, allThreeWarningsZero: stage === 'cold' ? coldObservations.length === 3 && coldObservations.every(row => row.warningsZero) : null, finiteMateBoundaryWitnessComplete: false, modelAccepted: false, engineeringAccepted: false, operationComplete: false, strictColdAcceptancePassed: false, productionAcceptanceEvaluated: false })
    console.log(JSON.stringify({ resultPath, sha256: hash(resultPath), stage, observationCompleted: true, coldObservations, modelAccepted: false, engineeringAccepted: false }, null, 2))
  } catch (error) {
    if (existsSync(out)) put(resolve(out, 'failure.json'), { schema: 'right-owner-stage-failure-v1', error: error.message, sessions: results, preservedArtifacts: true, modelAccepted: false, engineeringAccepted: false, operationComplete: false })
    throw error
  } finally {
    // A timed-out API is never killed. Keep the shared lock alive until the native child and CAD have exited.
    const safeSnapshot = () => { try { return processSnapshot() } catch (error) { console.error('manual_disposition_required: process observation failed; global lock retained. ' + error.message); return null } }
    let state = safeSnapshot()
    const childLive = () => {
      try {
        if (nativeChildUnconfirmed && currentArmOut) {
          const sessionPath = resolve(currentArmOut, 'session.json'), identityPath = resolve(currentArmOut, 'native-child.json'), startedPath = resolve(currentArmOut, 'native-child-started.json')
          if (existsSync(sessionPath)) { const session = read(sessionPath); if (session.nativeChildAlive === false) { nativeChildUnconfirmed = false; nativeChildIdentity = null; nativeChildPid = null; return false } }
          if (existsSync(identityPath)) { nativeChildIdentity = read(identityPath); nativeChildPid = nativeChildIdentity.pid; nativeChildUnconfirmed = false }
          else if (existsSync(startedPath)) nativeChildPid = read(startedPath).pid
        }
        if (!nativeChildIdentity && !nativeChildUnconfirmed) return false
        const pid = nativeChildIdentity?.pid ?? nativeChildPid
        if (!Number.isInteger(pid) || pid <= 0) return true
        const script = "$ErrorActionPreference='Stop'; $p=Get-Process -Id " + pid + " -ErrorAction SilentlyContinue; if($null -eq $p){'null'}else{@{pid=$p.Id;startTicks=$p.StartTime.ToUniversalTime().Ticks.ToString();executable=$p.Path}|ConvertTo-Json -Compress}"
        const result = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 10000 })
        need(result.status === 0, 'NATIVE_CHILD_IDENTITY_SNAPSHOT_FAILED'); const current = JSON.parse(result.stdout)
        const exactIdentity = nativeChildIdentity && /^\d+$/.test(String(nativeChildIdentity.startTicks ?? '')) && typeof nativeChildIdentity.executable === 'string' && nativeChildIdentity.executable.toLowerCase() === context.build.executable.toLowerCase()
        if (!exactIdentity) return current != null
        return current != null && current.pid === nativeChildIdentity.pid && current.startTicks === nativeChildIdentity.startTicks && String(current.executable).toLowerCase() === String(nativeChildIdentity.executable).toLowerCase()
      } catch (error) { console.error('manual_disposition_required: native child identity unresolved; global lock retained. ' + error.message); return true }
    }
    while (observerRunning || childLive() || state?.cadAndMonitorEmpty !== true) {
      console.error('manual_disposition_required: diagnostic stopped; global lock retained; no CAD or unconfirmed process terminated.')
      await pulse().catch(error => { console.error('GLOBAL_LOCK_HEARTBEAT_FAILED ' + error.message) }); await new Promise(done => setTimeout(done, 10000)); state = safeSnapshot()
    }
    clearInterval(timer); await chain
    if (context.process.protectedFs) need(same(context.process.protectedFs, state.protectedFs), 'PROTECTED_SLDWORKS_FS_CHANGED')
    unchanged(context.protectedRows); lock.release()
  }
}
async function main() {
  const [stage, operation, prior, ...extra] = process.argv.slice(2)
  need(['relink','repair','cold'].includes(stage) && operation === '--run-native' && extra.length === 0 && (stage === 'relink' ? !prior : !!prior), 'Usage: node run.mjs <relink|repair|cold> --run-native [prior-stage-result.json]')
  await runNative(stage, prior)
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => { console.error(error.stack || error.message); process.exitCode = 1 })
