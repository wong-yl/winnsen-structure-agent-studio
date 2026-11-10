// Throwaway diagnostic only. No task/attempt creation, model mutation, or production acceptance.
import { createHash, randomBytes } from 'node:crypto'
import { readFileSync, writeFileSync, lstatSync, readdirSync, mkdirSync, existsSync } from 'node:fs'
import { dirname, relative, isAbsolute, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { spawn, spawnSync } from 'node:child_process'

export const ROOT = 'D:\\Winnsen_Structure_Agent_Studio'
const HERE = dirname(fileURLToPath(import.meta.url)), PROBE = resolve(ROOT, 'data/indoor_workbench_repair_20261110/obsolete_door_probe')
const ORIGINAL = resolve(PROBE, 'right_identity_v1')
const BINDING = resolve(HERE, 'source-binding.json'), BINDING_SHA = 'A98EE16168513A58DFC199C9997BB1B21194CC50DDDF411D4B4228DD9FD95F04'
const ATTEMPT = resolve(ROOT, 'data/parametric_attempts/PARAM-20261110111643911-CE439B'), PACK = resolve(ATTEMPT, 'native_cad/working_pack')
export const ARMS = ['root-only', 'left-weld-only', 'right-weld-only']
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
  return readdirSync(folder, { withFileTypes: true }).flatMap(entry => { const path = resolve(folder, entry.name); need(!entry.isSymbolicLink(), 'LINKED_ENTRY_REFUSED ' + path); if (skipRuns && path === resolve(HERE, 'runs')) return []; return entry.isDirectory() ? walk(path, skipRuns) : [regular(path)] })
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
async function preflight() {
  need(resolve(process.cwd()).toLowerCase() === ROOT.toLowerCase() && HERE === resolve(PROBE, 'right_context_identity_v1'), 'ACTUAL_ROOT_AND_OWNED_DIRECTORY_REQUIRED')
  need(hash(BINDING) === BINDING_SHA, 'SOURCE_BINDING_CHANGED'); const binding = read(BINDING)
  need(binding.sourceAttempt === ATTEMPT && binding.inventory.length === 119 && binding.workingCount === 76 && binding.doorsCount === 30 && binding.sourcesCount === 13, 'FIXED_INVENTORY_SCOPE_CHANGED')
  unchanged([...binding.allAttemptFiles, ...binding.templatePins, binding.observer]);
  const actual = [...inventory(PACK), ...inventory(resolve(ATTEMPT, 'doors')), ...walk(resolve(ATTEMPT, 'door_sources')).map(row => ({ path: row.path, sha256: row.sha256, size: row.size }))].sort((a, b) => a.path.localeCompare(b.path))
  need(same(actual, [...binding.inventory].sort((a, b) => a.path.localeCompare(b.path))), 'CURRENT_76_30_13_INVENTORY_DIFFERS')
  const build = read(resolve(HERE, 'build.json')); need(build.buildPassed === true && hash(build.source) === build.sourceSha256 && hash(build.executable) === build.executableSha256, 'DIAGNOSTIC_BUILD_CHANGED')
  const originalBuild = read(resolve(ORIGINAL, 'build.json')), validation = read(resolve(ORIGINAL, 'static-validation.json')); need(validation.failed === 0 && validation.sourceSha256 === originalBuild.sourceSha256 && validation.executableSha256 === originalBuild.executableSha256 && validation.nativeCadExecuted === false, 'ORIGINAL_OFFLINE_STATIC_VALIDATION_REQUIRED')
  unchanged(validation.artifactVersions); for (const arm of ARMS) { need(binding.targets[arm] && binding.inventory.some(row => row.path === binding.targets[arm].path && row.sha256 === binding.targets[arm].sha256), 'TARGET_NOT_IN_FIXED_INVENTORY'); unchanged([binding.targets[arm]]) }
  const frozen = read(resolve(HERE, 'freeze-manifest.json')); need(frozen.status === 'ready_for_primary_review' && frozen.nativeCadExecuted === false, 'FROZEN_CONTEXT_DIAGNOSTIC_REQUIRED'); unchanged(frozen.artifacts)
  // The historical list supplies protected paths; their current bytes, including PROGRESS, form this new baseline.
  const old = read(resolve(ATTEMPT, 'research/protected-after-failure.json'))
  need(old.unchanged === true && old.rows.length === 19313 && old.rows.every(row => row.beforeSha256 === row.afterSha256 && !row.error), 'CE439B_FAILED_PROTECTION_EVIDENCE_DIFFERS')
  const rows = [...old.rows.map(row => regular(row.path)), ...walk(ATTEMPT), ...walk(PROBE, true), regular(resolve(PROBE, '../PROGRESS.md')), ...binding.templatePins.map(row => regular(row.path)), regular(binding.observer.path)]
  const dedup = new Map(); for (const row of rows) { const prior = dedup.get(row.path.toLowerCase()); need(!prior || prior.sha256 === row.sha256, 'CONFLICTING_CURRENT_PROTECTION_SNAPSHOT ' + row.path); dedup.set(row.path.toLowerCase(), row) }
  const protectedRows = [...dedup.values()]; unchanged(protectedRows)
  const portal = await portalGate(), processState = processSnapshot(); need(processState.cadAndMonitorEmpty, 'CAD_OR_MONITOR_NOT_EMPTY')
  return { binding, build, validation, protectedRows, portal, process: processState }
}
async function runNative() {
  const context = await preflight(), { acquireNativeWorkerInstanceLock } = await import(pathToFileURL(resolve(ROOT, 'tools/run_16029_native_worker.mjs')).href)
  const workerId = 'readonly-right-context-identity-' + process.pid + '-' + randomBytes(3).toString('hex'), lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(ROOT, 'data'), workerId })
  let timer, chain = Promise.resolve(), heartbeatError = null, observerRunning = false, nativeChildIdentity = null, nativeChildPid = null, nativeChildUnconfirmed = false, currentArmOut = null
  const pulse = () => { const next = chain.then(() => lock.heartbeat()); chain = next.catch(error => { heartbeatError ||= error }); return next }
  const out = resolve(HERE, 'runs', stamp() + '-' + process.pid), results = []
  try {
    await portalGate(); need(processSnapshot().cadAndMonitorEmpty, 'CAD_OR_MONITOR_OCCUPIED_BEFORE_RUN'); unchanged(context.protectedRows)
    mkdirSync(out, { recursive: true }); put(resolve(out, 'protected-baseline.json'), { rows: context.protectedRows, process: context.process, portal: context.portal.rows, currentProtectionHashesCapturedNow: true, modelAccepted: false })
    timer = setInterval(() => { void pulse().catch(error => { heartbeatError ||= error }) }, 10000)
    for (const arm of ARMS) {
      await pulse(); if (heartbeatError) throw heartbeatError
      await portalGate(); need(processSnapshot().cadAndMonitorEmpty, 'CAD_OR_MONITOR_OCCUPIED_BEFORE_' + arm); unchanged(context.protectedRows)
      const armOut = resolve(out, arm); mkdirSync(armOut); const owner = lock.heartbeat(), configPath = resolve(armOut, 'runner.json')
      const config = { schema: 'right-context-identity-arm-v1', arm, root: ROOT, pack: PACK, executable: context.build.executable, out: armOut, output: resolve(armOut, 'probe.json'), immutable: context.protectedRows, lockOwner: resolve(lock.lockDir, 'owner.json'), lockToken: owner.token, lockWorker: owner.workerId, lockPid: owner.pid, maximumDiagnosticSeconds: 180, naturalExitSeconds: 45, argv: [context.build.executable, arm, resolve(armOut, 'probe.json'), BINDING, BINDING_SHA] }
      put(configPath, config); observerRunning = true; nativeChildUnconfirmed = true; nativeChildIdentity = null; nativeChildPid = null; currentArmOut = armOut
      const child = spawn('python', [resolve(HERE, 'observe-right-identity.py'), configPath, hash(configPath)], { cwd: PACK, windowsHide: true, stdio: 'inherit' })
      const exitCode = await new Promise((done, reject) => { child.once('error', reject); child.once('close', done) }).finally(() => { observerRunning = false })
      await chain; if (heartbeatError) throw heartbeatError
      const sessionPath = resolve(armOut, 'session.json'); need(existsSync(sessionPath), 'OBSERVER_SESSION_EVIDENCE_MISSING'); const session = read(sessionPath); need(typeof session.nativeChildAlive === 'boolean', 'OBSERVER_CHILD_STATE_MISSING'); nativeChildIdentity = session.nativeChildAlive ? session.nativeChildIdentity : null; nativeChildPid = session.nativeChildAlive ? session.nativeChildPid : null; nativeChildUnconfirmed = session.nativeChildAlive && !nativeChildIdentity
      results.push({ arm, path: sessionPath, sha256: hash(sessionPath), pass: session.pass, timedOut: session.timedOut, modelAccepted: false })
      need(exitCode === 0 && session.pass === true && session.nextArmAllowed === true, 'DIAGNOSTIC_ARM_FAILED_OR_TIMEOUT ' + arm)
      unchanged(context.protectedRows)
    }
    put(resolve(out, 'result.json'), { schema: 'right-context-identity-result-v1', state: 'diagnostic_observations_only', arms: results, currentInventoryUnchanged: true, modelAccepted: false, strictColdAcceptancePassed: false, warningCauseProven: false, productionAcceptanceEvaluated: false })
    console.log(JSON.stringify({ state: 'diagnostic_observations_only', output: out, arms: results, modelAccepted: false }, null, 2))
  } catch (error) {
    if (existsSync(out)) put(resolve(out, 'failure.json'), { schema: 'right-context-identity-failure-v1', error: error.message, arms: results, preservedArtifacts: true, nextArmAllowed: false, modelAccepted: false })
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
async function main() { need(process.argv.length === 3 && process.argv[2] === '--run-native', 'Usage: node run-right-context-identity.mjs --run-native'); await runNative() }
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => { console.error(error.stack || error.message); process.exitCode = 1 })
