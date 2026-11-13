import { existsSync, realpathSync } from 'node:fs'
import { resolve, join, basename, dirname, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import { ROOT, SOURCE, DATA, SEED, read, write, writeText, files, directory, inside, protectionSnapshot, compareProtection, copyTree, assertIdle, acquireLock, processState, validateToolchain } from './common.mjs'

export function protectedScopes(sourceDirectory, { root = ROOT, seed = SEED } = {}) {
  return [
    { path: seed, required: true },
    { path: sourceDirectory, required: true },
    { path: resolve(root, 'tools'), required: true, extensions: ['.mjs', '.ps1', '.py', '.json', '.cs'] },
    { path: resolve(root, 'configs') },
    { path: resolve(root, 'workers/native_model_requests/parametric_v1'), excludeDirectoryPattern: '^(bin[^\\/]*|build[^\\/]*|repairs|sources)$' },
    { path: resolve(root, 'workers/native_model_requests/parametric_v1/repairs'), extensions: ['.cs', '.mjs', '.ps1', '.json'], excludeDirectoryPattern: '^(bin[^\\/]*|build[^\\/]*)$' },
    { path: resolve(root, 'workers/native_model_requests/parametric_v1/sources'), extensions: ['.cs', '.mjs', '.ps1', '.json'] },
    { path: resolve(root, 'data/master_v2/reports'), required: true },
    { path: resolve(root, 'docs/PLAN_方案A+_工艺内建参数化母版_20261112.md'), required: true },
    { path: resolve(root, 'data/native_model_requests'), required: true, extensions: ['.json', '.zip'], excludePrefixes: ['.native-worker-instance.lock'] },
    ...['data/review_portal.local.json', 'data/review_download_users.json', '.gitignore'].map(path => ({ path: resolve(root, path) })),
  ]
}

const delay = milliseconds => new Promise(resolveWait => setTimeout(resolveWait, milliseconds))
const isCad = item => /^(SLDWORKS|sldProcMon)\.exe$/i.test(item.name)
const canonicalExecutable = path => realpathSync.native(path).toLowerCase()
function confirmedCad(claim, processes) {
  if (!Number.isSafeInteger(claim?.pid) || claim.pid < 1 || typeof claim.startedAtUtc !== 'string' || !Number.isFinite(Date.parse(claim.startedAtUtc)) || typeof claim.executable !== 'string') throw new Error('CAD_OWNERSHIP_EVENT_INVALID')
  const process = processes.find(item => item.pid === claim.pid)
  if (!process) return null
  if (!/^SLDWORKS\.exe$/i.test(process.name) || Date.parse(process.startedAtUtc) !== Date.parse(claim.startedAtUtc) || canonicalExecutable(process.executable) !== canonicalExecutable(claim.executable)) throw new Error('CAD_PROCESS_IDENTITY_MISMATCH ' + JSON.stringify({ claim, observed: process }))
  return { ...process, kind: 'cad' }
}

function reusedCadAfterNativeExit(state, processes, previousConfirmation) {
  const claim = state.cadClaim, previous = previousConfirmation?.claim, observed = processes.find(item => item.pid === claim?.pid)
  if (!previous || !claim || !['pid', 'startedAtUtc', 'executable'].every(key => previous[key] === claim[key]) ||
      state.ended !== true || state.code !== 0 || state.error != null || state.signal != null || !observed ||
      typeof observed.name !== 'string' || !observed.name || isCad(observed) || typeof observed.startedAtUtc !== 'string' ||
      !Number.isFinite(Date.parse(observed.startedAtUtc)) || Date.parse(observed.startedAtUtc) <= Date.parse(claim.startedAtUtc)) return null
  try {
    const claimedExecutable = canonicalExecutable(claim.executable), observedExecutable = canonicalExecutable(observed.executable)
    if (claimedExecutable === observedExecutable || isCad({ name: basename(observedExecutable) })) return null
    return { code: 'CAD_PID_REUSED_AFTER_NATIVE_EXIT', observedAt: new Date().toISOString(), claim: { ...claim }, observed: { ...observed },
      previousConfirmation, native: { ended: state.ended, code: state.code, error: state.error ?? null, signal: state.signal ?? null },
      canonicalExecutables: { claimed: claimedExecutable, observed: observedExecutable } }
  } catch { return null }
}

export async function superviseCadSession({ lock, observeNative, queryProcesses = processState, wait = delay, pollMs = 1000, onObservation = () => {}, requestStop = () => {}, signal }) {
  let originalError = null, protectedIdentity = null, queryWarning = null, previousConfirmation = null
  const secondaryErrors = [], warnings = []
  const remember = error => { if (!originalError) originalError = error; else if (error !== originalError && !secondaryErrors.includes(error)) secondaryErrors.push(error) }
  const observe = observation => { try { onObservation({ ...observation, warnings: warnings.map(warning => ({ ...warning })) }) } catch (error) { remember(error); requestStop(error) } }
  const interruption = new Error('TRIAL_INTERRUPTED')
  for (;;) {
    const state = observeNative()
    if (state.error) remember(state.error)
    if (signal?.aborted) { remember(interruption); requestStop(interruption) }
    let owned = true
    try { lock.assert(); lock.heartbeat() } catch (error) { owned = false; remember(error); requestStop(error) }
    let processes
    try { processes = queryProcesses({ pids: [state.pid, state.cadClaim?.pid].filter(pid => Number.isSafeInteger(pid) && pid > 0) }) } catch (error) {
      const observedAt = new Date().toISOString()
      if (!queryWarning) {
        queryWarning = { code: 'PROCESS_QUERY_UNAVAILABLE', error: error.message, lastError: error.message, firstObservedAt: observedAt, lastObservedAt: observedAt, attempts: 0, recoveredAt: null }
        warnings.push(queryWarning)
      }
      queryWarning.attempts++; queryWarning.lastObservedAt = observedAt; queryWarning.lastError = error.message
      if (owned && state.ended && protectedIdentity?.kind !== 'cad') {
        try { protectedIdentity = { kind: 'runner', pid: process.pid, reason: 'process_query_unavailable' }; lock.bindProcess(protectedIdentity) } catch (bindingError) { remember(bindingError) }
      }
      observe({ state: 'process_query_unavailable', error: error.message, nativeEnded: state.ended })
      await wait(pollMs)
      continue
    }
    const cad = processes.filter(isCad)
    if (queryWarning) {
      queryWarning.recoveredAt = new Date().toISOString()
      observe({ state: 'process_query_recovered', cad, nativeEnded: state.ended })
      queryWarning = null
    }
    let identity
    if (state.cadClaim) {
      try {
        identity = confirmedCad(state.cadClaim, processes)
        if (identity) previousConfirmation = { claim: { ...state.cadClaim }, observed: { ...identity }, confirmedAt: new Date().toISOString() }
      } catch (error) {
        const reused = owned && !originalError ? reusedCadAfterNativeExit(state, processes, previousConfirmation) : null
        if (reused) {
          identity = null
          if (!warnings.some(warning => warning.code === reused.code && warning.observed.pid === reused.observed.pid && warning.observed.startedAtUtc === reused.observed.startedAtUtc && warning.canonicalExecutables.observed === reused.canonicalExecutables.observed)) warnings.push(reused)
        } else { remember(error); requestStop(error) }
      }
    }
    if (owned) {
      try {
        protectedIdentity = identity || (!state.ended && state.pid ? { kind: 'native', pid: state.pid, executable: state.executable } : { kind: 'runner', pid: process.pid })
        lock.bindProcess(protectedIdentity)
      } catch (error) { remember(error); requestStop(error) }
    }
    observe({ state: cad.length ? 'waiting_for_cad_exit' : state.ended ? 'cad_exited' : 'native_running', cad, nativeEnded: state.ended })
    if (state.ended && !cad.length) return { error: originalError, secondaryErrors, warnings, cadExited: true }
    await wait(pollMs)
  }
}

export async function runNativeJob(job, dir, { lock, dataRoot = DATA, sourceRoot = SOURCE, toolchainManifest = null, spawnChild = spawn, queryProcesses = processState, wait = delay, pollMs = 1000, signal } = {}) {
  if (!lock) throw new Error('SHARED_LOCK_REQUIRED')
  lock.assert()
  if (signal?.aborted) throw new Error('TRIAL_INTERRUPTED')
  const runDirectory = inside(dir, join(dataRoot, 'runs'))
  const cadDirectory = inside(job.cadDirectory, runDirectory)
  if (cadDirectory !== join(runDirectory, 'cad')) throw new Error('JOB_CAD_DIRECTORY_INVALID')
  const tool = validateToolchain(toolchainManifest ?? read(inside(join(dataRoot, 'toolchain-current.json'), dataRoot)), { dataRoot, sourceRoot })
  const resultPath = inside(join(runDirectory, 'native-result.json'), dataRoot)
  const jobPath = inside(join(runDirectory, 'job.json'), dataRoot)
  const cancellationPath = inside(join(runDirectory, 'cancel.json'), dataRoot)
  const toolchainPath = inside(join(runDirectory, 'toolchain.json'), dataRoot)
  if (existsSync(toolchainPath)) throw new Error('PRESERVE_RUN_TOOLCHAIN_EVIDENCE')
  write(toolchainPath, tool, { root: dataRoot, beforeCommit: () => lock.assert() })
  write(jobPath, { ...job, resultPath, cancellationPath, toolchainPath, sharedLockDirectory: lock.lockDir, sharedWorkerId: lock.owner.workerId, sharedToken: lock.owner.token }, { root: dataRoot, beforeCommit: () => lock.assert() })
  const state = { ended: false, pid: null, cadClaim: null, executable: tool.exe, code: null, signal: null, error: null }
  let child, out = '', err = '', lineBuffer = '', stopped = false, lastObservation = ''
  const remember = error => { if (!state.error) state.error = error }
  const stop = error => {
    remember(error)
    if (!stopped) {
      stopped = true
      try { write(cancellationPath, { reason: error.message, requestedAt: new Date().toISOString() }, { root: dataRoot }) } catch (cancellationError) { remember(cancellationError) }
    }
  }
  const abort = () => stop(new Error('TRIAL_INTERRUPTED'))
  const log = (name, content) => { try { writeText(join(runDirectory, name), content, { root: dataRoot }) } catch (error) { stop(error) } }
  try {
    child = spawnChild(tool.exe, [jobPath], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'], cwd: dirname(tool.exe) })
    state.pid = child.pid
    child.once('error', error => { remember(error); state.ended = true })
    child.once('close', (code, terminationSignal) => {
      state.ended = true; state.code = code; state.signal = terminationSignal
      if (code !== 0) remember(new Error('NATIVE_PROCESS_FAILED ' + code + ' ' + (terminationSignal || '') + ' ' + err.slice(-2000)))
    })
    child.stdout.setEncoding('utf8'); child.stderr.setEncoding('utf8')
    child.stdout.on('data', bytes => {
      out += bytes; lineBuffer += bytes; log('native.stdout.log', out)
      const lines = lineBuffer.split(/\r?\n/); lineBuffer = lines.pop()
      for (const line of lines) {
        let event
        try { event = JSON.parse(line) } catch { continue }
        if (event.inspection === 'cad_owned') {
          if (state.cadClaim) stop(new Error('DUPLICATE_CAD_OWNERSHIP_EVENT'))
          else state.cadClaim = event
        }
        if (event.inspection === 'failure') remember(new Error(String(event.error || 'NATIVE_FAILURE_EVENT')))
      }
    })
    child.stderr.on('data', bytes => { err += bytes; log('native.stderr.log', err) })
    if (Number.isSafeInteger(state.pid) && state.pid > 0) lock.bindProcess({ kind: 'native', pid: state.pid, executable: tool.exe })
  } catch (error) { remember(error); if (child && state.pid) stop(error); else state.ended = true }
  signal?.addEventListener('abort', abort, { once: true })
  process.on('SIGINT', abort); process.on('SIGTERM', abort)
  if (signal?.aborted) abort()
  let supervision
  try {
    supervision = await superviseCadSession({
      lock, observeNative: () => state, queryProcesses, wait, pollMs, requestStop: stop, signal,
      onObservation(observation) {
        const value = JSON.stringify(observation)
        if (value !== lastObservation) { writeText(join(runDirectory, 'supervision.json'), value + '\n', { root: dataRoot }); lastObservation = value }
      },
    })
  } finally {
    signal?.removeEventListener('abort', abort)
    process.removeListener('SIGINT', abort); process.removeListener('SIGTERM', abort)
  }
  if (supervision.error) {
    supervision.error.secondaryErrors = supervision.secondaryErrors
    supervision.error.warnings = supervision.warnings
    supervision.error.cadSupervised = true
    throw supervision.error
  }
  lock.assert()
  const nativeResult = read(resultPath), finished = nativeResult.events?.at(-1)
  if (state.code !== 0 || !state.cadClaim || nativeResult.production_release !== false || finished?.inspection !== 'finished' || finished.success !== true || finished.cadExited !== true) {
    const error = new Error('NATIVE_COMPLETION_EVIDENCE_INVALID'); error.cadSupervised = true; throw error
  }
  return { code: state.code, stderr: err.slice(-2000), warnings: supervision.warnings, cadExited: true }
}

export function trialSource(pointer, { dataRoot = DATA } = {}) {
  const dir = inside(pointer.dir, join(dataRoot, 'runs')), tail = relative(join(dataRoot, 'runs'), dir)
  if (!tail || /[\\/]/.test(tail) || pointer.cadDirectory !== join(dir, 'cad') || pointer.nativeResult !== join(dir, 'native-result.json')) throw new Error('PRIOR_TRIAL_POINTER_INVALID')
  return inside(pointer.cadDirectory, dir)
}

export async function runProtectedPhase({ tag, sourceDirectory, job, currentFile = null, excludeRelativeDirectories = [] }, { root = ROOT, dataRoot = DATA, seed = SEED, checkIdle = assertIdle, lockFactory = acquireLock, executeNative = runNativeJob, queryProcesses = processState, wait = delay } = {}) {
  if (typeof tag !== 'string' || !/^[A-Za-z0-9-]{1,48}$/.test(tag)) throw new Error('INVALID_TRIAL_TAG')
  if (currentFile !== null && !/^[A-Za-z0-9-]+-current\.json$/.test(currentFile)) throw new Error('INVALID_CURRENT_POINTER')
  const source = inside(sourceDirectory, root)
  if (!Array.isArray(excludeRelativeDirectories) || excludeRelativeDirectories.length &&
      (excludeRelativeDirectories.length !== 1 || excludeRelativeDirectories[0] !== 'assemblies/case3' || job?.action !== 'assembly' ||
       job?.assemblyResume?.schema !== '16029-variable-assembly-resume/v1' ||
       JSON.stringify(job.assemblyResume.generateCaseIndices) !== '[3]' || JSON.stringify(job.assemblyResume.reuseCaseIndices) !== '[1,2]' ||
       source !== join(job.assemblyResume.sourceRun, 'cad'))) throw new Error('EXACT_CASE3_RESUME_EXCLUSION_REQUIRED')
  if (source !== seed) {
    const sourceRun = dirname(source), tail = relative(join(dataRoot, 'runs'), sourceRun)
    inside(source, join(dataRoot, 'runs'))
    if (basename(source) !== 'cad' || !tail || /[\\/]/.test(tail)) throw new Error('TRIAL_SOURCE_REQUIRED')
  }
  checkIdle()
  const lock = lockFactory()
  const cancellation = new AbortController(), interrupt = () => cancellation.abort()
  process.on('SIGINT', interrupt); process.on('SIGTERM', interrupt)
  let dir, before, result, originalError = null
  const secondaryErrors = []
  const remember = error => { if (!originalError) originalError = error; else if (error !== originalError) secondaryErrors.push(error) }
  try {
    checkIdle(); lock.assert()
    const id = new Date().toISOString().replace(/[:.]/g, '-') + '-' + tag + '-' + randomBytes(4).toString('hex')
    dir = directory(resolve(dataRoot, 'runs', id), dataRoot, true)
    before = protectionSnapshot(protectedScopes(source, { root, seed }), { root })
    write(join(dir, 'protected-before.json'), before, { root: dataRoot })
    if (before.nativeTaskIndex?.validationErrors.length) throw new Error('PROTECTED_NATIVE_INDEX_INVALID ' + JSON.stringify(before.nativeTaskIndex.validationErrors))
    write(join(dir, 'lock-owner.json'), lock.owner, { root: dataRoot })
    const copy = copyTree(source, join(dir, 'cad'), { outputRoot: dataRoot, sourceRoot: root, excludeRelativeDirectories })
    write(join(dir, 'copy-manifest.json'), copy, { root: dataRoot })
    result = await executeNative({ ...job, sourceCadDirectory: source, cadDirectory: join(dir, 'cad') }, dir, { lock, dataRoot, signal: cancellation.signal })
  } catch (error) { remember(error) }
  if (!result && !originalError?.cadSupervised) {
    const supervision = await superviseCadSession({ lock, observeNative: () => ({ ended: true }), signal: cancellation.signal, queryProcesses, wait, onObservation: observation => {
      if (dir) write(join(dir, 'supervision.json'), observation, { root: dataRoot })
    } })
    if (supervision.error) remember(supervision.error)
  }
  try {
    lock.assert()
    if (before) {
      let nativeTaskIndex = null
      const changed = compareProtection(before, { root, onNativeTaskIndexComparison: value => { nativeTaskIndex = value } })
      write(join(dir, 'native-index-protection.json'), nativeTaskIndex || { accepted: true, mode: 'index_not_present', rawHashChanged: false, hashChangeOnlyRebuiltAt: false }, { root: dataRoot })
      write(join(dir, 'protection-result.json'), { changed, pass: changed.length === 0,
        nativeTaskIndex: { diagnosticPath: join(dir, 'native-index-protection.json'),
          mode: nativeTaskIndex?.mode || 'index_not_present', accepted: nativeTaskIndex?.accepted ?? true,
          rawHashChanged: nativeTaskIndex?.rawHashChanged || false, hashChangeOnlyRebuiltAt: nativeTaskIndex?.hashChangeOnlyRebuiltAt || false,
          beforeSha256: nativeTaskIndex?.before?.sha256 || null, afterSha256: nativeTaskIndex?.after?.sha256 || null,
          changedJsonFields: nativeTaskIndex?.changedJsonFields || [], rejectionReasons: nativeTaskIndex?.rejectionReasons || [] } }, { root: dataRoot })
      if (changed.length) throw new Error('PROTECTED_FILES_CHANGED ' + JSON.stringify(changed))
    }
    if (!originalError) {
      write(join(dir, 'result.json'), result, { root: dataRoot })
      if (currentFile) write(join(dataRoot, currentFile), { dir, cadDirectory: join(dir, 'cad'), nativeResult: join(dir, 'native-result.json') }, { root: dataRoot })
    }
  } catch (error) { remember(error) }
  try { lock.release() } catch (error) { remember(error) }
  process.removeListener('SIGINT', interrupt); process.removeListener('SIGTERM', interrupt)
  if (originalError) { originalError.secondaryErrors = [...(originalError.secondaryErrors || []), ...secondaryErrors]; throw originalError }
  return { phase: tag, pass: true, dir }
}

async function main(mode = process.argv[2] || 'check') {
  if (mode === 'check') {
    assertIdle()
    console.log(JSON.stringify({ idle: true, source: SEED, cadFiles: files(SEED).filter(path => /\.sld(prt|asm|drw)$/i.test(path)).length, compiler: existsSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'), sharedLock: 'data/native_model_requests/.native-worker-instance.lock' }))
    return
  }
  if (!['prepare', 'variables', 'matrix', 'reopen'].includes(mode)) throw new Error('UNKNOWN_MODE ' + mode)
  const tag = mode === 'prepare' ? 'T0' : mode === 'variables' ? 'T1' : mode === 'matrix' ? 'T1-matrix' : 'reopen'
  const sourceDirectory = mode === 'prepare' ? SEED : trialSource(read(inside(join(DATA, mode === 'reopen' ? 'T1-current.json' : 'T0-current.json'), DATA)))
  const targets = ['door_panel_left_W437_H471p25.SLDPRT', 'door_panel_right_W437_H471p25.SLDPRT']
  if (mode === 'prepare') targets.push('door_stiffener_W437_H471p25.SLDPRT', 'ordinary_door_left_W437_H471p25.SLDASM', 'ordinary_door_right_W437_H471p25.SLDASM', '箱体竖隔板L.sldprt', '箱体竖隔板R_parametric.SLDPRT')
  const action = mode === 'reopen' ? 'reopen-validate' : mode === 'prepare' ? 'rewire-inspect' : mode === 'matrix' ? 'variables-matrix' : 'variables'
  console.log(JSON.stringify(await runProtectedPhase({ tag, sourceDirectory, job: { action, targets }, currentFile: mode === 'prepare' ? 'T0-current.json' : mode === 'reopen' ? 'reopen-current.json' : 'T1-current.json' })))
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { console.error(error); process.exitCode = 1 })
}
