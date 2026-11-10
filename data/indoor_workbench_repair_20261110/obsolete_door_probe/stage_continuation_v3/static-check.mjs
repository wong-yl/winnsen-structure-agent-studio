// Throwaway offline evidence-gate checks; synthetic snapshots never prove native CAD behavior.
import assert from 'node:assert/strict'
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { hash, makePlan, ROOT } from './run-stage-continuation.mjs'
import { STAGES, DOOR_MODULES, RIGHT_NAME, LEFT_NAME, RIGHT_SOURCE_NAME, checkPrecision, checkSourceManifest, checkAuthorization, checkRepair, checkContinuationClone, checkLocks, checkReflectedVertices, equalPath } from './research-gates.mjs'

const here = dirname(fileURLToPath(import.meta.url)), checks = []
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const source = read(resolve(here, 'source-binding.json')), prior = read(resolve(source.sourceAttempt, 'parametric_execution.json'))
const attempts = resolve(ROOT, 'data/parametric_attempts'), beforeAttempts = readdirSync(attempts).sort()
const check = (name, action) => { action(); checks.push({ name, pass: true }) }
const reject = (name, value, mutate, gate) => check(name, () => { const changed = structuredClone(value); mutate(changed); assert.throws(() => gate(changed)) })

check('four-phase continuation stops after locks-verify', () => assert.deepEqual(STAGES, ['clone', 'repair-base-foot', 'locks', 'locks-verify']))
check('new plan preserves exact contract and has no historical resume receipt', () => {
  const plan = makePlan(prior, 'PARAM-INMEMORY-ONLY', 'research-inmemory', 'D:\\inmemory\\task_store')
  assert.deepEqual(plan.contract, prior.contract); assert.deepEqual(plan.doorModules, DOOR_MODULES); assert.equal(plan.resumedEvidence, null); checkPrecision(plan)
})
reject('rounded module precision is rejected', prior, value => { value.doorModules[1].heightMm = 205.555556 }, checkPrecision)
reject('rounded R9 row precision is rejected', prior, value => { value.contract.rowsByColumn.R[0].doorHeightMm = 205.555556 }, checkPrecision)
check('source binding remains pinned and marks the current graph unavailable', () => {
  assert.equal(hash(resolve(here, 'source-binding.json')), 'E2F59AE0B0DAE5665C249D63F9A7FD7612E70F976895604813C0B0129AFE4D7B')
  assert.equal(source.currentDependencyGraphComplete, false); assert.equal(source.allFrozenCurrentDependencyEdgesCovered, false)
  assert.equal(source.ancestorCloneCountIsNotV3Expectation, true); assert.equal(source.referenceAliases.length, 131)
})

const manifest = read(source.sourceManifest.path), sourceDirectory = resolve(source.sourceAttempt, 'door_sources')
const sourceRows = readdirSync(sourceDirectory).map(name => { const path = resolve(sourceDirectory, name); return { path, sha256: hash(path), size: statSync(path).size } })
check('actual thirteen source roles retain exact ordinal path/SHA binding', () => assert.equal(checkSourceManifest(manifest, sourceDirectory, sourceRows).roles, 13))
reject('source manifest path case changes remain rejected', manifest, value => { value.left_panel_seed.path = value.left_panel_seed.path.toLowerCase() }, value => checkSourceManifest(value, sourceDirectory, sourceRows))
reject('same basename outside source directory is rejected', manifest, value => { value.left_panel_seed.path = resolve(ROOT, 'other/left_panel_seed.SLDPRT') }, value => checkSourceManifest(value, sourceDirectory, sourceRows))
reject('wrong source SHA is rejected', sourceRows, value => { value[0].sha256 = 'A'.repeat(64) }, value => checkSourceManifest(manifest, sourceDirectory, value))

const interfaceNode = source.stageNodes.find(node => node.phase === 'repair-interfaces'), interfaceProof = read(interfaceNode.nativeEvidence.path)
const ancestorWorking = resolve(source.sourceAttempt, 'native_cad/working_pack'), ancestorDoors = resolve(source.sourceAttempt, 'doors')
const interfaceGate = value => checkRepair(value, 'repair-interfaces', ancestorWorking, ancestorDoors, DOOR_MODULES)
check('actual v2 native interfaces pass corrected Windows path comparison', () => assert.equal(interfaceGate(interfaceProof).nativeEvidencePassed, true))
reject('a different interface part remains rejected', interfaceProof, value => { value.rows.find(row => row.part).part = resolve(ancestorWorking, 'different.SLDPRT') }, interfaceGate)
check('API path case changes are accepted', () => {
  const changed = structuredClone(interfaceProof); changed.rows.find(row => row.part).part = changed.rows.find(row => row.part).part.toLowerCase()
  assert.equal(interfaceGate(changed).nativeEvidencePassed, true); assert.equal(equalPath(ancestorWorking.toUpperCase(), ancestorWorking), true)
})

const attempt = resolve(attempts, 'PARAM-INMEMORY-ONLY'), working = resolve(attempt, 'native_cad/working_pack'), doors = resolve(attempt, 'doors')
const mapping = [...source.working.map(row => ({ source: row.path, destination: resolve(working, row.relativePath), kind: 'working' })), ...source.doors.map(row => ({ source: row.path, destination: resolve(doors, row.relativePath), kind: 'door' }))]
const generatedAlias = source.referenceAliases.find(row => !equalPath(row.path, row.file)), aliasTarget = mapping.find(row => equalPath(row.source, generatedAlias.file))
const clone = { phase: 'clone', success: true, rows: [
  { file: mapping[0].destination, replacements: [{ source: mapping[1].source, target: mapping[1].destination, replaced: true }] },
  { file: mapping[75].destination, replacements: [{ source: generatedAlias.path, target: aliasTarget.destination, replaced: true }] },
] }
const cloneGate = value => checkContinuationClone(value, mapping, source.referenceAliases)
let cloneObservation
check('105-copy mapping accepts observed working and exact generated-door aliases without a 140-edge expectation', () => {
  cloneObservation = cloneGate(clone); assert.equal(cloneObservation.replacementCount, 2)
  for (const field of ['frozenCurrentDependencyGraphAvailable', 'allFrozenCurrentDependencyEdgesCovered', 'allFrozenDependencyEdgesReplaced', 'fullPostCloneDependencyReenumerationVerified', 'ancestor140EdgesReusedAsExpectation']) assert.equal(cloneObservation[field], false)
})
check('clone API path case changes are accepted', () => { const value = structuredClone(clone); for (const row of value.rows) { row.file = row.file.toUpperCase(); for (const replacement of row.replacements) { replacement.source = replacement.source.toUpperCase(); replacement.target = replacement.target.toUpperCase() } } assert.equal(cloneGate(value).replacementCount, 2) })
check('missing complete 75+30 copy mapping is rejected', () => assert.throws(() => checkContinuationClone(clone, mapping.slice(1), source.referenceAliases)))
reject('unbound old source path is rejected', clone, value => { value.rows[1].replacements[0].source = resolve(ROOT, 'outside', 'same-name.SLDASM') }, cloneGate)
reject('wrong mapped target is rejected', clone, value => { value.rows[0].replacements[0].target = mapping[2].destination }, cloneGate)
reject('external replacement target is rejected', clone, value => { value.rows[0].replacements[0].target = resolve(ROOT, 'outside/part.SLDPRT') }, cloneGate)
reject('false native replacement result is rejected', clone, value => { value.rows[0].replacements[0].replaced = false }, cloneGate)
reject('duplicate owner is rejected', clone, value => { value.rows.push(structuredClone(value.rows[0])) }, cloneGate)
reject('duplicate replacement edge is rejected', clone, value => { value.rows[0].replacements.push({ ...value.rows[0].replacements[0] }) }, cloneGate)
reject('missing real door replacements is rejected', clone, value => { value.rows[1].replacements = [] }, cloneGate)
check('frozen observed clone list binds only the actual observed list', () => assert.equal(checkContinuationClone(clone, mapping, source.referenceAliases, { rows: clone.rows, replacementCount: 2, replacements: cloneObservation.replacements }).replacementCount, 2))
check('different frozen observed replacement count is rejected', () => assert.throws(() => checkContinuationClone(clone, mapping, source.referenceAliases, { rows: clone.rows, replacementCount: 140, replacements: cloneObservation.replacements })))

const now = Date.now(), authorization = { phase: 'locks', planSha256: 'A'.repeat(64), executableSha256: 'B'.repeat(64), taskId: 'PARAM-INMEMORY-ONLY', workerId: 'worker-inmemory', leaseId: 'lease-inmemory', rightPartitionSha256: 'C'.repeat(64), previousResearchSha256: 'D'.repeat(64), researchOnly: true, predecessor: '', preInventory: [], preDoorInventory: [], issuedAtUtc: new Date(now).toISOString(), expiresAt: new Date(now + 22 * 60 * 1000).toISOString() }
const authExpected = { ...authorization, attempt, authorizationPath: resolve(attempt, 'authorizations/locks.json'), taskPath: resolve(attempt, 'task_store/PARAM-INMEMORY-ONLY.json'), state: 'building', leaseExpiresAt: new Date(now + 30 * 60 * 1000).toISOString() }
check('fresh phase authorization binds the new task and lease', () => assert.equal(checkAuthorization(authorization, authExpected, now), true))
reject('old phase authorization is rejected', authorization, value => { value.phase = 'envelope' }, value => checkAuthorization(value, authExpected, now))
reject('wrong lease is rejected', authorization, value => { value.leaseId = 'old-lease' }, value => checkAuthorization(value, authExpected, now))
reject('stale authorization is rejected', authorization, value => { value.issuedAtUtc = new Date(now - 61000).toISOString() }, value => checkAuthorization(value, authExpected, now))
reject('expired authorization is rejected', authorization, value => { value.expiresAt = new Date(now - 1).toISOString() }, value => checkAuthorization(value, authExpected, now))

function geometry(side, rows, mirrored = false) {
  const sign = side === 'R' ? -1 : 1
  return { side, body_count: 1, feature_count: 30, error_feature_count: 0, warning_feature_count: 0, has_sheet_metal: true, has_flat_pattern: true, traversal_complete: true, forbidden_import_feature_count: 0, mirror_feature_count: mirrored ? 1 : 0, edge_count: 2,
    edges: [{ vertex_start_mm: [sign * 20, 0, -1], vertex_end_mm: [sign * 20, 10, -1] }, { vertex_start_mm: [sign * 20, 10, -1], vertex_end_mm: [sign * 30, 10, -1] }],
    slot_rows_mm: [...rows], circle_rows_mm: rows.map(row => row - 60), slot_fragment_count: 0, circle_fragment_count: 0, unique_slot_profile_signature_count: 1,
    slot_analyses: rows.map(() => ({ full_fingerprint: true, side_orientation_exact: true })), circle_analyses: rows.map(() => ({ full_diameter5_pair: true, side_orientation_exact: true })) }
}
const leftPath = resolve(working, LEFT_NAME), rightPath = resolve(working, RIGHT_NAME), parameterPath = resolve(working, RIGHT_SOURCE_NAME)
const leftGeometry = geometry('L', prior.contract.rowsByColumn.L.map(row => row.centerYmm)), generatedGeometry = geometry('L', prior.contract.rowsByColumn.R.map(row => row.centerYmm)), rightGeometry = geometry('R', prior.contract.rowsByColumn.R.map(row => row.centerYmm), true)
const locks = { phase: 'locks', success: true, rows: [{ side: 'L', reusedMatchingNativeSlots: false, geometry: leftGeometry }, { side: 'R', generatedGeometry }, { side: 'R', mirrorGeometry: rightGeometry, rightParameterSource: parameterPath, referenceFiles: [parameterPath], referenceStatuses: [3], localSourceBound: true }, { side: 'R', reusedMatchingNativeSlots: false, geometry: rightGeometry }] }
const before = [{ path: leftPath, sha256: '1'.repeat(64) }, { path: rightPath, sha256: '2'.repeat(64) }], after = [{ path: leftPath, sha256: '3'.repeat(64) }, { path: rightPath, sha256: '4'.repeat(64) }, { path: parameterPath, sha256: '5'.repeat(64) }]
const pending = { ...source.inheritedPending, sourceRightAfterEnvelopeSha256: source.sourceRightAfterEnvelopeSha256, sourceRightAfterInterfacesSha256: source.sourceRightAfterInterfacesSha256, sourceLeftAfterEnvelopeSha256: source.sourceLeftAfterEnvelopeSha256, sourceLeftAfterInterfacesSha256: source.sourceLeftAfterInterfacesSha256, postCloneRightSha256: before[1].sha256, postCloneLeftSha256: before[0].sha256 }
const binding = { working, before, after, pending, rightBackupSha256: before[1].sha256, leftBackupSha256: before[0].sha256, leftSeedSha256: before[0].sha256, processProof: { stdout: 'MIRROR_VERTICES_VERIFIED 3\nPARAMETRIC_LOCK_COLUMN_VERIFIED L\nPARAMETRIC_LOCK_COLUMN_VERIFIED R\n' } }
const locksGate = value => checkLocks(value, 'locks', prior.contract, binding)
let locksObservation
check('lock provenance preserves ancestor SHA separately from fresh post-clone seed and backups', () => {
  locksObservation = locksGate(locks); assert.equal(locksObservation.lockSeedExactlyMatchesPostCloneLeft, true); assert.equal(locksObservation.lockSeedExactlyMatchesPreLocksLeft, true)
  assert.equal(locksObservation.sourceLeftAfterEnvelopeSha256, source.sourceLeftAfterEnvelopeSha256); assert.notEqual(locksObservation.postCloneLeftSha256, locksObservation.sourceLeftAfterEnvelopeSha256)
  assert.equal(Object.hasOwn(locksObservation, 'lockSeedExactlyMatchesEnvelopeLeft'), false); assert.equal(locksObservation.exactRightBoundingBoxVerified, false)
})
reject('old matching-slot reuse cannot discharge deferred right', locks, value => { value.rows.at(-1).reusedMatchingNativeSlots = true }, locksGate)
reject('L4 row drift is rejected', locks, value => { value.rows[0].geometry.slot_rows_mm[0] += 1 }, locksGate)
reject('R9 row drift is rejected', locks, value => { value.rows.at(-1).geometry.slot_rows_mm.pop() }, locksGate)
reject('external parameter reference is rejected', locks, value => { value.rows[2].referenceFiles[0] = resolve(ROOT, 'old', RIGHT_SOURCE_NAME) }, locksGate)
for (const status of [0, 1, 2, 5, 99]) reject('broken/locked/dangling/unknown right status is rejected: ' + status, locks, value => { value.rows[2].referenceStatuses[0] = status }, locksGate)
check('known out-of-context local source status remains explicit', () => { const value = structuredClone(locks); value.rows[2].referenceStatuses[0] = 4; assert.deepEqual(locksGate(value).referenceStatuses, [4]) })
check('wrong ancestor, post-clone, seed and backup hashes are rejected', () => {
  for (const mutate of [value => { value.pending.sourceRightAfterInterfacesSha256 = 'A'.repeat(64) }, value => { value.pending.envelopeLeftSha256 = value.pending.postCloneLeftSha256 }, value => { value.pending.postCloneRightSha256 = 'A'.repeat(64) }, value => { value.pending.postCloneLeftSha256 = 'A'.repeat(64) }, value => { value.leftSeedSha256 = 'A'.repeat(64) }, value => { value.leftBackupSha256 = 'A'.repeat(64) }, value => { value.rightBackupSha256 = 'A'.repeat(64) }]) { const value = structuredClone(binding); mutate(value); assert.throws(() => checkLocks(locks, 'locks', prior.contract, value)) }
})
check('unchanged formal right is rejected', () => assert.throws(() => checkLocks(locks, 'locks', prior.contract, { ...binding, after: [after[0], before[1], after[2]] })))
check('missing native mirror stdout is rejected', () => assert.throws(() => checkLocks(locks, 'locks', prior.contract, { ...binding, processProof: { stdout: '' } })))
check('two-way captured-vertex reflection does not invent finite boundaries', () => { assert.equal(checkReflectedVertices(generatedGeometry, rightGeometry).finiteSurfaceBoundariesFullyCompared, false); const value = structuredClone(rightGeometry); value.edges[0].vertex_start_mm[0] += 1; assert.throws(() => checkReflectedVertices(generatedGeometry, value)) })
const verify = { phase: 'locks-verify', success: true, rows: [{ side: 'L', reusedMatchingNativeSlots: true, geometry: leftGeometry }, { side: 'R', reusedMatchingNativeSlots: true, geometry: rightGeometry }] }
check('saved-file verify returns research discharge with modelAccepted false', () => { const result = checkLocks(verify, 'locks-verify', prior.contract, { ...binding, locksObservation }); assert.equal(result.state, 'deferred_right_discharged_for_research_only'); assert.equal(result.modelAccepted, false) })
check('saved-file verify rejects changed right bytes', () => { const value = structuredClone(binding); value.after[1].sha256 = '9'.repeat(64); assert.throws(() => checkLocks(verify, 'locks-verify', prior.contract, { ...value, locksObservation })) })
check('static run created no attempt', () => assert.deepEqual(readdirSync(attempts).sort(), beforeAttempts))

const path = resolve(here, 'static-check-' + new Date().toISOString().replace(/[^0-9]/g, '') + '-' + process.pid + '.json')
writeFileSync(path, JSON.stringify({ schema: 'obsolete-door-stage-continuation-static-check-v3', nativeCadExecuted: false, attemptCreated: false, taskCreated: false, fixtureBoundary: 'Actual v2 interfaces and source-manifest files are historical evidence. Clone and lock snapshots are in-memory gate fixtures and prove no native CAD behavior.', checks, count: checks.length, failed: 0, artifactVersions: ['source-binding.json', 'run-stage-continuation.mjs', 'research-gates.mjs', 'static-check.mjs'].map(name => ({ path: resolve(here, name), sha256: hash(resolve(here, name)) })), modelAccepted: false }, null, 2) + '\n', { flag: 'wx' })
console.log(JSON.stringify({ path, sha256: hash(path), count: checks.length, failed: 0, nativeCadExecuted: false, attemptCreated: false }, null, 2))
