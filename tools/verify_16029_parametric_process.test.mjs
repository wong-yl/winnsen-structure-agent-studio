import assert from 'node:assert/strict'
import { execPath } from 'node:process'
import { mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { resolve } from 'node:path'
import { tmpdir } from 'node:os'
import { MAX_PARAMETRIC_PHASE_TIMEOUT_MS, run16029ParametricPhaseProcess } from './lib/locker_16029_parametric_process.mjs'

const root = mkdtempSync(resolve(tmpdir(), '16029-process-'))
const attempt = resolve(root, 'attempt')
mkdirSync(resolve(attempt, 'evidence'), { recursive: true })
const run = (phase, code, options = {}) => run16029ParametricPhaseProcess(execPath, ['-e', code], { phase, attempt, cwd: root, timeoutMs: 5000, stopOwnedCad: async () => ({ verified: true, stoppedPids: [], remainingPids: [], errors: [] }), ...options })
try {
  const success = await run('success', 'process.stdout.write("ok");')
  assert.equal(success.code, 0)
  assert.equal(success.stopReason, '')

  const extended = await run('extended', 'process.stdout.write("ok");', { timeoutMs: MAX_PARAMETRIC_PHASE_TIMEOUT_MS })
  assert.equal(extended.timeoutMs, MAX_PARAMETRIC_PHASE_TIMEOUT_MS)
  await assert.rejects(run('over-limit', 'process.stdout.write("unexpected");', { timeoutMs: MAX_PARAMETRIC_PHASE_TIMEOUT_MS + 1 }), /phase timeout must be/)

  await assert.rejects(run('timeout', 'setTimeout(() => {}, 5000);', { timeoutMs: 100 }), error => error.code === 'PARAMETRIC_PHASE_TIMEOUT' && error.cleanupVerified === true)
  await assert.rejects(run('cancel', 'setTimeout(() => {}, 5000);', { signal: AbortSignal.timeout(80) }), error => error.code === 'PARAMETRIC_PHASE_CANCELLED' && error.cleanupVerified === true)
  await assert.rejects(run('exit', 'process.exit(7);'), error => error.code === 'PARAMETRIC_PHASE_FAILED' && error.cleanupVerified === true)
  await assert.rejects(run('cleanup-fail', 'setTimeout(() => {}, 5000);', { timeoutMs: 80, stopOwnedCad: async () => ({ verified: false, errors: ['unproven CAD owner'] }) }), error => error.code === 'PARAMETRIC_PHASE_TIMEOUT' && error.cleanupVerified === false)
  let destructiveCalls = 0
  const preserve = { preserveCadOnFailure: true,
    stopOwnedCad: async () => { destructiveCalls++; throw new Error('destructive cleanup must not run') },
    inspectCad: async () => ({ verified: true, stoppedPids: [], remainingPids: [], errors: [] }) }
  const naturalExit = 'setTimeout(() => { process.stdout.write("natural-exit"); }, 300);'
  for (const [phase, options, expectedCode] of [
    ['preserved-timeout', { timeoutMs: 80 }, 'PARAMETRIC_PHASE_TIMEOUT'],
    ['preserved-cancel', { cancelAfterMs: 80 }, 'PARAMETRIC_PHASE_CANCELLED'],
    ['preserved-control', { controlPollMs: 30, isCancelled: async () => { throw new Error('control unavailable') } }, 'PARAMETRIC_PHASE_CANCELLED'],
    ['preserved-start', { onStarted: async () => { throw new Error('start callback unavailable') } }, null],
  ]) {
    const began = Date.now()
    let observedRunning = false
    const currentOptions = options.cancelAfterMs ? { signal: AbortSignal.timeout(options.cancelAfterMs) } : options
    await assert.rejects(run(phase, naturalExit, { ...preserve, ...currentOptions, onProgress: message => {
      if (!message.startsWith('{')) return
      const row = JSON.parse(message)
      if (row.state === 'manual_disposition_required') {
        process.kill(row.pid, 0)
        observedRunning = true
      }
    } }), error => (!expectedCode || error.code === expectedCode) && error.cleanupVerified === true)
    assert.equal(observedRunning, true)
    assert.ok(Date.now() - began >= 250, 'preserve mode must await natural child exit')
    const handoff = JSON.parse(readFileSync(resolve(attempt, 'evidence', phase + '.manual-handoff.json'), 'utf8'))
    assert.equal(handoff.cleanup.verified, false)
    const result = JSON.parse(readFileSync(resolve(attempt, 'evidence', phase + '.process.json'), 'utf8'))
    assert.equal(result.cleanup.nativeClosed, true)
    assert.equal(result.cleanup.preserved, true)
    assert.match(result.stdout, /natural-exit/)
  }
  await assert.rejects(run('preserved-nonzero', 'process.exit(7);', preserve), error => error.code === 'PARAMETRIC_PHASE_FAILED' && error.cleanupVerified === true)
  let inspections = 0
  const preservedStarted = Date.now()
  await assert.rejects(run('preserved-live-cad', 'process.exit(7);', { ...preserve,
    inspectCad: async () => ({ verified: ++inspections > 1, stoppedPids: [], remainingPids: inspections > 1 ? [] : [12345], errors: [] }) }), error => error.cleanupVerified === true)
  assert.ok(Date.now() - preservedStarted >= 1000, 'live CAD must withhold rollback until its natural exit is observed')
  assert.equal(JSON.parse(readFileSync(resolve(attempt, 'evidence/preserved-live-cad.manual-handoff.json'), 'utf8')).cleanup.verified, false)
  for (const phase of ['preserved-late-start', 'preserved-progress-error', 'preserved-handoff-write-error']) {
    let observations = 0
    if (phase === 'preserved-handoff-write-error') mkdirSync(resolve(attempt, 'evidence', phase + '.manual-handoff.json'))
    const began = Date.now()
    await assert.rejects(run(phase, phase === 'preserved-progress-error' ? 'process.stdout.write("tick"); process.exit(7);' : 'process.exit(7);', { ...preserve,
      onStarted: phase === 'preserved-late-start' ? async () => { await new Promise(resolveDelay => setTimeout(resolveDelay, 100)); throw new Error('late start error') } : async () => {},
      onProgress: () => { if (phase === 'preserved-progress-error') throw new Error('progress sink error') },
      inspectCad: async () => ({ verified: ++observations > 1, remainingPids: observations > 1 ? [] : [12345], errors: [] }),
    }), error => error.cleanupVerified === true)
    assert.ok(observations >= 2)
    assert.ok(Date.now() - began >= 1000, 'evidence or callback failure must retain the wait for CAD exit')
  }
  await assert.rejects(run16029ParametricPhaseProcess(resolve(root, 'missing-native.exe'), [], { phase: 'preserved-spawn-error', attempt, cwd: root, ...preserve }), error => error.cleanupVerified === true)
  mkdirSync(resolve(attempt, 'evidence/preserved-final-write-error.process.json'))
  await assert.rejects(run('preserved-final-write-error', 'process.exit(7);', preserve), error => error.cleanupVerified === true)
  assert.equal(destructiveCalls, 0)
  console.log('parametric process: success, timeout, cancellation and non-owned-CAD cleanup failure are isolated and evidenced; no CAD started')
} finally { rmSync(root, { recursive: true, force: true, maxRetries: 3, retryDelay: 100 }) }
