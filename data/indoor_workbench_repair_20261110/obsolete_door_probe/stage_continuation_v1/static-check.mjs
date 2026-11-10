// Pure in-memory evidence rejection checks. No CAD, task, attempt, authorization, or receipt is created.
import assert from 'node:assert/strict'
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { checkClone, checkEnvelope, DERIVED_NAMES, hash, makePlan, ROOT } from './run-stage-continuation.mjs'

const here = dirname(fileURLToPath(import.meta.url)), results = []
function check(name, action) { action(); results.push({ name, pass: true }) }
const original = JSON.parse(readFileSync(resolve(ROOT, 'data/parametric_attempts/PARAM-20261110035409405-0C0A4D/parametric_execution.json'), 'utf8'))
check('plan retains exact original contract and module map without historical resume', () => {
  const plan = makePlan(original, 'PARAM-INMEMORY-ONLY', 'research-inmemory', 'D:\\inmemory\\task_store')
  assert.deepEqual(plan.contract, original.contract); assert.deepEqual(plan.doorModules, original.doorModules)
  assert.equal(plan.sourceRightPartitionSha256, original.sourceRightPartitionSha256); assert.equal(plan.resumedEvidence, null)
})
const mapping = [{ source: 'D:\\source\\a.SLDPRT', destination: 'D:\\destination\\a.SLDPRT' }]
const clone = { phase: 'clone', success: true, rows: [{ file: mapping[0].destination, replacements: [{ source: mapping[0].source, target: mapping[0].destination, replaced: true }] }] }
const dependencies = [{ owner: mapping[0].source, dependencySources: [mapping[0].source] }]
check('clone requires exact source and destination', () => { assert.equal(checkClone(clone, mapping, dependencies).replacementCount, 1); const changed = structuredClone(clone); changed.rows[0].replacements[0].target = 'D:\\outside\\a.SLDPRT'; assert.throws(() => checkClone(changed, mapping, dependencies)) })
check('clone rejects external source and omitted frozen edges', () => { const changed = structuredClone(clone); changed.rows[0].replacements[0].source = 'D:\\other\\a.SLDPRT'; assert.throws(() => checkClone(changed, mapping, dependencies)); changed.rows[0].replacements = []; assert.throws(() => checkClone(changed, mapping, dependencies)) })
check('clone rejects missing nonempty owners and duplicate dependency edges', () => { const changed = structuredClone(clone); changed.rows[0].replacements.push({ ...changed.rows[0].replacements[0] }); assert.throws(() => checkClone(changed, mapping, dependencies)); assert.throws(() => checkClone(clone, mapping, [])) })
const box = [0, 0, 0, 1, 2, 3], snapshot = () => ({ part_box_m: [...box], body_count: 1 })
const rows = DERIVED_NAMES.map(file => ({ diagnosticOnly: true, file, beforeGeometry: snapshot(), afterGeometry: snapshot(), links: [], issues: [], rebuilt: true, healthy: true, saved: true }))
rows.push(...DERIVED_NAMES.filter(file => file !== '箱体竖隔板R.SLDPRT').map(file => ({ inspection: 'envelope_target_boundary', file, healthy: true, afterGeometry: snapshot(), expectedBox: [...box] })))
rows.push({ file: '标准寄存柜 模型.SLDPRT', dimension: 'D2@草图1', rebuilt: true, healthy: true, targetMm: 2000, readbackMm: 2000, afterGeometry: snapshot() },
  { inspection: 'master_frame_pattern_saved_reopened', rebuilt: true, snapshot: snapshot() },
  ...['箱体右侧板.sldprt', '箱体左侧板.sldprt'].map(file => ({ inspection: 'rear_hole_saved_reopened', file, snapshot: snapshot() })),
  { diagnosticOnly: false, targetHeightMm: 2000, targetDepthMm: 500, deliveryEligible: false })
const valid = { phase: 'envelope', success: true, rows }
check('valid fixture is research evidence only and still lacks complete cold reopen', () => { const observation = checkEnvelope(valid, original.contract); assert.equal(observation.derivedSummaryCount, 27); assert.equal(observation.completeColdReopenVerified, false); assert.equal(observation.explicitDepthReadbackInJson, false) })
for (const [name, mutate] of [
  ['missing derived summary', proof => proof.rows.shift()],
  ['unsaved or unhealthy derived part', proof => { proof.rows[0].saved = false }],
  ['target boundary drift', proof => { proof.rows.find(row => row.expectedBox).afterGeometry.part_box_m[0] = .01 }],
  ['missing saved reopen', proof => { proof.rows = proof.rows.filter(row => row.inspection !== 'master_frame_pattern_saved_reopened') }],
  ['wrong master readback', proof => { proof.rows.find(row => row.dimension).readbackMm = 1917 }],
  ['false native success', proof => { proof.success = false }],
]) check('rejects ' + name, () => { const changed = structuredClone(valid); mutate(changed); assert.throws(() => checkEnvelope(changed, original.contract)) })
const path = resolve(here, 'static-check-' + new Date().toISOString().replace(/[^0-9]/g, '') + '-' + process.pid + '.json')
writeFileSync(path, JSON.stringify({ schema: 'obsolete-door-stage-continuation-static-check-v1', nativeCadExecuted: false, attemptCreated: false, taskCreated: false, checks: results, count: results.length, failed: 0, runnerSha256: hash(resolve(here, 'run-stage-continuation.mjs')), modelAccepted: false }, null, 2) + '\n', { flag: 'wx' })
console.log(JSON.stringify({ path, sha256: hash(path), count: results.length, failed: 0, nativeCadExecuted: false }, null, 2))
