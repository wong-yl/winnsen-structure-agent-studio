import test from 'node:test'
import assert from 'node:assert/strict'
import { join } from 'node:path'
import { randomBytes } from 'node:crypto'
import { DATA, directory, write, read, hash } from '../common.mjs'
import { validateFirstRoundReceipt, runWeightDiagnostic } from './run.mjs'

function fixture() {
  const run = directory(join(DATA, 'runs', '_offline-weight-receipt-' + randomBytes(8).toString('hex')), DATA, true)
  const seedPath = join(run, 'seed.fixture.json'), seed = { path: seedPath }
  write(seedPath, { description: 'throwaway offline receipt fixture, contains no CAD' })
  seed.sha256 = hash(seedPath)
  const cad = { inspection: 'cad_owned', pid: 123, startTicks: 100, startedAtUtc: '2026-11-13T00:00:00Z' }
  const documents = {
    'job.json': { action: 'weight-readback-diagnostic', weightRound: 'first', production_release: false, weightSeed: seed },
    'native-result.json': { events: [cad, { inspection: 'weight_readback_diagnostic', result: {
      success: true, readbackComplete: true, sourceFilesUnchanged: true, readOnlyCopyFilesUnchanged: true,
    } }, { inspection: 'finished', success: true, cadExited: true }] },
    'result.json': { code: 0, cadExited: true },
    'supervision.json': { state: 'cad_exited', nativeEnded: true, cad: [] },
    'protection-result.json': { pass: true, changed: [] },
    'toolchain.json': { description: 'offline fake, never executed' },
  }
  const evidence = Object.entries(documents).map(([name, value]) => {
    const path = join(run, name); write(path, value)
    return { path, sha256: hash(path) }
  })
  const receipt = { schema: '16029-weight-round/v1', round: 'first', pass: true, run, seed, cad, evidence,
    acceptancePass: false, fullPlanPass: false, production_release: false }
  const path = join(run, 'weight-round.json'); write(path, receipt)
  return { run, path, receipt }
}

test('second round requires a bound completed first receipt', async () => {
  await assert.rejects(runWeightDiagnostic('second'), /ROUND_ARGUMENT_REQUIRED/)
  const f = fixture()
  assert.equal(validateFirstRoundReceipt(f.path).receipt.run, f.run)
  const changed = read(join(f.run, 'native-result.json'))
  changed.events[1].result.readbackComplete = false
  write(join(f.run, 'native-result.json'), changed)
  assert.throws(() => validateFirstRoundReceipt(f.path), /EVIDENCE_CHANGED/)
  f.receipt.evidence.find(item => item.path.endsWith('native-result.json')).sha256 = hash(join(f.run, 'native-result.json'))
  write(f.path, f.receipt)
  assert.throws(() => validateFirstRoundReceipt(f.path), /DIAGNOSTIC_INCOMPLETE/)
})

test('first receipt cannot hide missing protection or another CAD identity', () => {
  const f = fixture()
  f.receipt.evidence = f.receipt.evidence.filter(item => !item.path.endsWith('protection-result.json'))
  write(f.path, f.receipt)
  assert.throws(() => validateFirstRoundReceipt(f.path), /EVIDENCE_MISSING/)
  const other = fixture()
  other.receipt.cad = { ...other.receipt.cad, startTicks: 200 }
  write(other.path, other.receipt)
  assert.throws(() => validateFirstRoundReceipt(other.path), /DIAGNOSTIC_INCOMPLETE/)
})

test('first receipt rejects cancellation, failed index protection and expanded scope', () => {
  const cancelled = fixture()
  write(join(cancelled.run, 'cancel.json'), { cancelled: true })
  assert.throws(() => validateFirstRoundReceipt(cancelled.path), /CANCELLED_FIRST_ROUND/)
  const failed = fixture(), protectionPath = join(failed.run, 'protection-result.json')
  write(protectionPath, { pass: true, changed: [], nativeTaskIndex: { accepted: false } })
  failed.receipt.evidence.find(item => item.path === protectionPath).sha256 = hash(protectionPath)
  write(failed.path, failed.receipt)
  assert.throws(() => validateFirstRoundReceipt(failed.path), /FIRST_ROUND_NATIVE_FAILED/)
  const expanded = fixture()
  expanded.receipt.fullPlanPass = true; write(expanded.path, expanded.receipt)
  assert.throws(() => validateFirstRoundReceipt(expanded.path), /SUCCESSFUL_FIRST_ROUND_REQUIRED/)
})
