import { spawn, spawnSync } from 'node:child_process'
import { existsSync, lstatSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

export const MAX_PARAMETRIC_PHASE_TIMEOUT_MS = 60 * 60 * 1000

// Exact PID, creation time and executable checks run together before termination.
const STOP_OWNED_CAD = String.raw`
$ErrorActionPreference='Stop'
$request=[Console]::In.ReadToEnd() | ConvertFrom-Json
$stopped=@(); $errors=@()
foreach($owner in $request.owners){
  try {
    $cad=Get-Process -Id ([int]$owner.pid) -ErrorAction SilentlyContinue
    if($null -eq $cad){continue}
    if($cad.StartTime.ToUniversalTime().Ticks.ToString() -ne $owner.startTicks -or $cad.Path -ine $owner.executable -or $cad.ProcessName -ine 'SLDWORKS'){throw 'CAD process identity changed'}
    $monitors=@(Get-CimInstance Win32_Process -Filter "Name='sldProcMon.exe'" | Where-Object { $_.ParentProcessId -eq [int]$owner.pid -and $_.CommandLine -match ('(?:^|\s)--ppid='+[int]$owner.pid+'(?:\s|$)') })
    foreach($monitor in $monitors){
      $live=Get-Process -Id $monitor.ProcessId -ErrorAction SilentlyContinue
      if($null -eq $live){continue}
      if($live.StartTime.ToUniversalTime().Ticks -lt [long]$owner.startTicks -or $live.Path -ine $monitor.ExecutablePath -or $live.ProcessName -ine 'sldProcMon'){throw 'CAD monitor identity changed'}
      $live.Kill(); if(-not $live.WaitForExit(5000)){throw 'owned monitor did not exit'}
      $stopped+=$monitor.ProcessId
    }
    $cad.Kill(); if(-not $cad.WaitForExit(10000)){throw 'owned CAD did not exit'}
    $stopped+=[int]$owner.pid
  }catch{$errors+=$_.Exception.Message}
}
$remaining=@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
@{ stoppedPids=$stopped; errors=$errors; remainingPids=$remaining; verified=($errors.Count -eq 0 -and $remaining.Count -eq 0) } | ConvertTo-Json -Compress
`

export function read16029OwnedCadEvidence(attempt, startedAtMs) {
  const evidenceDirectory = resolve(attempt, 'evidence')
  const owners = []
  for (const name of readdirSync(evidenceDirectory).filter(name => /^session-\d+\.json$/.test(name))) {
    const path = resolve(evidenceDirectory, name), stat = lstatSync(path)
    if (!stat.isFile() || stat.isSymbolicLink() || stat.mtimeMs < startedAtMs) continue
    // .NET ticks exceed JavaScript's safe integer range, so preserve their decimal text.
    const row = JSON.parse(readFileSync(path, 'utf8').replace(/("process_start_ticks_utc"\s*:\s*)(\d+)/, '$1"$2"'))
    if (row.exclusive_identity_gate !== true || !Number.isInteger(row.process_id) || row.process_id <= 0 || !/^\d{17,19}$/.test(String(row.process_start_ticks_utc)) || !/[\\/]SLDWORKS\.exe$/i.test(row.executable_path || '')) throw new Error('owned CAD startup evidence is invalid')
    owners.push({ pid: row.process_id, startTicks: String(row.process_start_ticks_utc), executable: row.executable_path })
  }
  return owners
}

export function stop16029OwnedCad(attempt, startedAtMs, { platform = process.platform, run = spawnSync } = {}) {
  if (platform !== 'win32') return { verified: true, stoppedPids: [], remainingPids: [], errors: [] }
  try {
    const owners = read16029OwnedCadEvidence(attempt, startedAtMs)
    const result = run('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', STOP_OWNED_CAD], { input: JSON.stringify({ owners }), windowsHide: true, encoding: 'utf8', timeout: 30000 })
    if (result.status !== 0) throw new Error(result.stderr || result.error?.message || 'owned CAD cleanup command failed')
    const cleanup = JSON.parse(result.stdout)
    if (cleanup.verified !== true) return { ...cleanup, verified: false }
    return cleanup
  } catch (error) { return { verified: false, stoppedPids: [], remainingPids: [], errors: [error.message] } }
}

export function inspect16029CadProcesses({ platform = process.platform, run = spawnSync } = {}) {
  if (platform !== 'win32') return { verified: true, stoppedPids: [], remainingPids: [], errors: [] }
  try {
    const result = run('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', "$ErrorActionPreference='Stop'; $rows=@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | ForEach-Object { @{ pid=$_.Id; name=$_.ProcessName; startTicks=$_.StartTime.ToUniversalTime().Ticks.ToString(); executable=$_.Path } }); @{ processes=$rows } | ConvertTo-Json -Depth 4 -Compress"], { windowsHide: true, encoding: 'utf8', timeout: 10000 })
    if (result.status !== 0) throw new Error(result.stderr || result.error?.message || 'CAD process inspection failed')
    const { processes } = JSON.parse(result.stdout)
    if (!Array.isArray(processes) || processes.some(row => !Number.isInteger(row.pid) || row.pid <= 0 || !['SLDWORKS', 'sldProcMon'].includes(row.name))) throw new Error('CAD process inspection returned invalid identities')
    return { verified: processes.length === 0, stoppedPids: [], remainingPids: processes.map(row => row.pid), remainingProcesses: processes, errors: [] }
  } catch (error) { return { verified: false, stoppedPids: [], remainingPids: [], errors: [error.message] } }
}

export async function run16029ParametricPhaseProcess(executable, args, {
  phase, attempt, cwd, env, timeoutMs = 20 * 60 * 1000, signal,
  isCancelled = async () => false, controlPollMs = 1000, onProgress = () => {},
  stopOwnedCad = stop16029OwnedCad, onStarted = async () => {},
  preserveCadOnFailure = false, inspectCad = inspect16029CadProcesses,
} = {}) {
  if (!Number.isInteger(timeoutMs) || timeoutMs < 1 || timeoutMs > MAX_PARAMETRIC_PHASE_TIMEOUT_MS) throw new Error('phase timeout must be 1–3600000 milliseconds')
  if (signal?.aborted) throw Object.assign(new Error('PARAMETRIC_PHASE_CANCELLED ' + phase), { code: 'PARAMETRIC_PHASE_CANCELLED', cleanupVerified: true })
  const startedAtMs = Date.now(), processEvidencePath = resolve(attempt, 'evidence', phase + '.process.json')
  const child = spawn(executable, args, { shell: false, windowsHide: true, cwd, env })
  let stdout = '', stderr = '', stopReason = '', closed = false, rejectClosed
  let resolveChildClosed
  const childClosedPromise = new Promise(resolveObserved => { resolveChildClosed = resolveObserved })
  const closedPromise = new Promise((resolveClosed, rejectRun) => {
    rejectClosed = rejectRun
    child.on('error', rejectRun)
    child.on('close', (code, terminationSignal) => { closed = true; const result = { code, signal: terminationSignal }; resolveChildClosed(result); resolveClosed(result) })
  })
  closedPromise.catch(() => {})
  const append = (text, value) => (text + value).slice(-1024 * 1024)
  child.stdout.on('data', data => { stdout = append(stdout, data.toString()); report(data.toString().trim()) })
  child.stderr.on('data', data => { stderr = append(stderr, data.toString()) })
  let cleanup = null, stopPromise = null, controlError = null, manualReported = false
  function report(message) {
    if (!preserveCadOnFailure) return onProgress(message)
    try { onProgress(message) } catch (error) { controlError ||= error }
  }
  const inspectPreserved = async () => {
    try {
      const observed = await inspectCad()
      if (!observed || typeof observed.verified !== 'boolean' || !Array.isArray(observed.remainingPids) || (observed.verified && observed.remainingPids.length)) throw new Error('invalid CAD inspection result')
      return observed
    } catch (error) { controlError ||= error; return { verified: false, stoppedPids: [], remainingPids: [], errors: [error.message] } }
  }
  const handoffPath = resolve(attempt, 'evidence', phase + '.manual-handoff.json')
  const recordPreserved = (reason, observed) => {
    cleanup = { ...observed, verified: false, preserved: true, nativePid: child.pid, nativeClosed: closed }
    if (manualReported) return
    manualReported = true
    try {
      if (existsSync(handoffPath)) throw new Error('manual handoff evidence already exists')
      writeFileSync(handoffPath, JSON.stringify({ phase, reason, executable, pid: child.pid, startedAtMs, observedAtMs: Date.now(), cleanup,
        message: 'Waiting for manual disposition; native and CAD processes may still be running. No process was terminated. The runner retains its lock and waits for all processes to exit.' }, null, 2) + '\n', { flag: 'wx' })
    } catch (error) { controlError ||= error }
    report(JSON.stringify({ phase, state: 'manual_disposition_required', pid: child.pid, reason, nativeClosed: closed, remainingCadPids: observed.remainingPids }))
  }
  const awaitPreservedCadExit = async reason => {
    let observed = await inspectPreserved()
    if (!observed.verified) {
      controlError ||= new Error('CAD process baseline is not empty after native process exit')
      recordPreserved(reason, observed)
      while (!observed.verified) {
        await new Promise(resolveWait => setTimeout(resolveWait, 1000))
        observed = await inspectPreserved()
      }
    }
    return { ...observed, preserved: true, nativePid: child.pid, nativeClosed: true }
  }
  const stop = reason => {
    if (stopPromise || closed) return stopPromise
    stopReason = reason
    stopPromise = (async () => {
      if (preserveCadOnFailure) {
        const observed = await inspectPreserved()
        recordPreserved(reason, observed)
        await childClosedPromise
        cleanup = await awaitPreservedCadExit(reason)
        return
      }
      // child is the process object returned by this spawn, never a process-name lookup.
      if (!closed && !child.kill()) throw new Error('owned native process did not accept termination')
      cleanup = await stopOwnedCad(attempt, startedAtMs)
      const stopDeadline = setTimeout(() => rejectClosed(new Error('owned native process did not exit')), 15000)
      try { await closedPromise } finally { clearTimeout(stopDeadline) }
    })().catch(async error => {
      controlError = error; cleanup = { verified: false, preserved: preserveCadOnFailure, errors: [error.message] }
      if (preserveCadOnFailure) { await childClosedPromise; cleanup = await awaitPreservedCadExit('preserve_control_failed'); return }
      rejectClosed(error)
    })
    return stopPromise
  }
  const aborted = () => { void stop('cancelled') }
  signal?.addEventListener('abort', aborted, { once: true })
  const deadline = setTimeout(() => { void stop('timeout') }, timeoutMs)
  let polling = false
  const control = setInterval(async () => {
    if (polling || closed || stopReason) return
    polling = true
    try { if (await isCancelled()) await stop('cancelled') }
    catch (error) { controlError = error; await stop('control_failed') }
    finally { polling = false }
  }, controlPollMs)
  try {
    await onStarted({ pid: child.pid, startedAtMs, phase, executable })
    const result = await closedPromise
    if (preserveCadOnFailure) cleanup = await awaitPreservedCadExit(result.code === 0 ? 'cad_remaining_after_native_exit' : 'nonzero_exit')
    if (stopPromise) await stopPromise
    if (!stopReason && result.code !== 0 && !preserveCadOnFailure) cleanup = await stopOwnedCad(attempt, startedAtMs)
    const record = { ...result, pid: child.pid, executable, startedAtMs, finishedAtMs: Date.now(), timeoutMs, stopReason, preserveCadOnFailure, cleanup, stdout, stderr }
    writeFileSync(processEvidencePath, JSON.stringify(record, null, 2) + '\n', { flag: 'wx' })
    if (stopReason || result.code !== 0 || controlError) throw Object.assign(new Error(controlError?.message || (stopReason ? 'PARAMETRIC_PHASE_' + (stopReason === 'timeout' ? 'TIMEOUT' : 'CANCELLED') + ' ' + phase : 'PARAMETRIC_PHASE_FAILED ' + phase + ': ' + (stderr || stdout))), { code: stopReason === 'timeout' ? 'PARAMETRIC_PHASE_TIMEOUT' : stopReason ? 'PARAMETRIC_PHASE_CANCELLED' : 'PARAMETRIC_PHASE_FAILED', cleanupVerified: cleanup?.verified !== false, processEvidencePath })
    return record
  } catch (error) {
    try {
      if (!closed) await stop('control_failed')
      if (preserveCadOnFailure && closed) cleanup = await awaitPreservedCadExit(stopReason || 'exception_after_native_exit')
      if (!existsSync(processEvidencePath)) writeFileSync(processEvidencePath, JSON.stringify({ pid: child.pid, executable, startedAtMs, finishedAtMs: Date.now(), stopReason, cleanup, stdout, stderr, error: error.message }, null, 2) + '\n', { flag: 'wx' })
    } catch (recordError) { throw Object.assign(recordError, { cleanupVerified: preserveCadOnFailure ? cleanup?.verified === true : cleanup?.verified !== false }) }
    throw Object.assign(error, { cleanupVerified: preserveCadOnFailure ? cleanup?.verified === true : cleanup?.verified !== false })
  } finally {
    clearTimeout(deadline); clearInterval(control); signal?.removeEventListener('abort', aborted)
  }
}
