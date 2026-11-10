// Pure behavior checks with in-memory synthetic fixtures and read-only historical evidence.
// No CAD session, native attempt, task store, copied CAD, lease, or authorization file is created.
import assert from 'node:assert/strict'
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { checkClone, checkEnvelope, hash, makePlan, ROOT } from './run-stage-continuation.mjs'
import { STAGES, DOOR_MODULES, RIGHT_NAME, LEFT_NAME, RIGHT_SOURCE_NAME, V6_RIGHT_SHA, SOURCE_SHA, sourceLeaf,
  checkPrecision, checkSourceManifest, checkAuthorization, checkDeferredRight, checkDoor, checkLocks, checkReflectedVertices } from './research-gates.mjs'

const here = dirname(fileURLToPath(import.meta.url)), results = []
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const originalAttempt = resolve(ROOT, 'data/parametric_attempts/PARAM-20261110035409405-0C0A4D')
const v1Attempt = resolve(ROOT, 'data/parametric_attempts/PARAM-20261110095819500-614465')
const original = read(resolve(originalAttempt, 'parametric_execution.json'))
const beforeAttempts = readdirSync(resolve(ROOT, 'data/parametric_attempts')).sort()
function check(name, action) { action(); results.push({ name, pass: true }) }
function reject(name, value, mutate, gate) { check(name, () => { const changed = structuredClone(value); mutate(changed); assert.throws(() => gate(changed)) }) }

check('fixed nine-phase order stops at locks-verify', () => assert.deepEqual(STAGES, ['clone', 'envelope', 'door-471p25', 'door-205p555556', 'repair-door-placements', 'repair-interfaces', 'repair-base-foot', 'locks', 'locks-verify']))
check('fresh plan preserves exact contract and module map without historical resume', () => {
  const plan = makePlan(original, 'PARAM-INMEMORY-ONLY', 'research-inmemory', 'D:\\inmemory\\task_store')
  assert.deepEqual(plan.contract, original.contract); assert.deepEqual(plan.doorModules, original.doorModules)
  assert.equal(plan.sourceRightPartitionSha256, original.sourceRightPartitionSha256); assert.equal(plan.resumedEvidence, null); checkPrecision(plan)
})
reject('rounded module height is rejected', original, value => { value.doorModules[1].heightMm = 205.555556 }, checkPrecision)
reject('contract door row precision drift is rejected', original, value => { value.contract.rowsByColumn.R[0].doorHeightMm = 205.555556 }, checkPrecision)

const sourceDirectory = resolve(originalAttempt, 'door_sources'), manifest = read(resolve(originalAttempt, 'door_sources.json'))
const sourceRows = readdirSync(sourceDirectory).map(name => { const path = resolve(sourceDirectory, name); return { path, sha256: hash(path), size: statSync(path).size } })
check('actual original manifest binds all thirteen roles to exact absolute path and bytes', () => assert.equal(checkSourceManifest(manifest, sourceDirectory, sourceRows).roles, 13))
reject('missing door source manifest role is rejected', manifest, value => { delete value.left_panel_seed }, value => checkSourceManifest(value, sourceDirectory, sourceRows))
check('missing source file is rejected', () => assert.throws(() => checkSourceManifest(manifest, sourceDirectory, sourceRows.slice(1))))
reject('changed source SHA is rejected', sourceRows, value => { value[0].sha256 = 'A'.repeat(64) }, value => checkSourceManifest(manifest, sourceDirectory, value))
reject('same-basename source outside exact directory is rejected', manifest, value => { value.left_panel_seed.path = resolve(ROOT, 'other', 'left_panel_seed.SLDPRT') }, value => checkSourceManifest(value, sourceDirectory, sourceRows))

const mapping = [{ source: resolve(ROOT, 'inmemory-source/a.SLDPRT'), destination: resolve(ROOT, 'inmemory-dest/a.SLDPRT') }]
const clone = { phase: 'clone', success: true, rows: [{ file: mapping[0].destination, replacements: [{ source: mapping[0].source, target: mapping[0].destination, replaced: true }] }] }
const dependencies = [{ owner: mapping[0].source, dependencySources: [mapping[0].source] }]
check('clone exact dependency multiset remains enforced', () => assert.equal(checkClone(clone, mapping, dependencies).replacementCount, 1))
reject('clone external replacement target rejected', clone, value => { value.rows[0].replacements[0].target = resolve(ROOT, 'outside/a.SLDPRT') }, value => checkClone(value, mapping, dependencies))
reject('clone omitted frozen edge rejected', clone, value => { value.rows[0].replacements = [] }, value => checkClone(value, mapping, dependencies))
reject('clone duplicate frozen edge rejected', clone, value => { value.rows[0].replacements.push({ ...value.rows[0].replacements[0] }) }, value => checkClone(value, mapping, dependencies))

const working = resolve(v1Attempt, 'native_cad/working_pack')
const envelope = read(resolve(v1Attempt, 'evidence/envelope.json')), envelopeAuth = read(resolve(v1Attempt, 'authorizations/envelope.json'))
const cloneObservation = read(resolve(v1Attempt, 'research/clone-observation.json')).observation
const rightBefore = envelopeAuth.preInventory.find(row => basename(row.path) === RIGHT_NAME)
const binding = { working, sourceRightSha256: V6_RIGHT_SHA, cloneObservation, rightBefore, authorization: envelopeAuth }
const envelopeGate = value => checkEnvelope(value, original.contract, binding)
check('actual v1 envelope accepts only the exact frozen right as pending research work', () => {
  const result = envelopeGate(envelope)
  assert.equal(result.derivedSummaryCount, 27); assert.equal(result.targetBoundaryCount, 26)
  assert.equal(result.pendingRight.state, 'pending_native_locks_replacement'); assert.equal(result.pendingRight.finalGeometryAccepted, false)
})
const rightSummary = envelope.rows.find(row => row.file === RIGHT_NAME)
for (const [name, mutate] of [
  ['wrong deferred filename', value => { value.file = LEFT_NAME }],
  ['changed right body boundary', value => { value.afterGeometry.part_box_m[0] += .000001 }],
  ['changed right feature count', value => { value.afterGeometry.feature_count++ }],
  ['wrong MirrorStock feature', value => { value.links[0].feature += 'x' }],
  ['wrong MirrorStock type', value => { value.links[0].type = 'Imported' }],
  ['wrong source status', value => { value.links[0].sourceStatuses[0] = 1 }],
  ['wrong reference source', value => { value.links[0].referenceFiles[0] = resolve(ROOT, 'outside', LEFT_NAME) }],
  ['extra MirrorStock link', value => { value.links.push({ ...value.links[0] }) }],
]) reject('exact frozen right gate rejects ' + name, rightSummary, mutate, value => checkDeferredRight(value, binding))
check('wrong v6 SHA and wrong fresh right authorization are rejected', () => {
  assert.throws(() => checkDeferredRight(rightSummary, { ...binding, sourceRightSha256: 'A'.repeat(64) }))
  assert.throws(() => checkDeferredRight(rightSummary, { ...binding, authorization: { rightPartitionSha256: 'A'.repeat(64) } }))
})
reject('another broken derived link is rejected', envelope, value => { value.rows.find(row => row.file === LEFT_NAME && row.diagnosticOnly).links.push({ blockedByBrokenReference: true }) }, envelopeGate)
reject('missing saved derived summary is rejected', envelope, value => { value.rows.find(row => row.file === LEFT_NAME && row.diagnosticOnly).saved = false }, envelopeGate)
reject('missing original target boundary is rejected', envelope, value => { value.rows.splice(value.rows.findIndex(row => row.inspection === 'envelope_target_boundary'), 1) }, envelopeGate)
reject('missing original master saved reopen is rejected', envelope, value => { value.rows = value.rows.filter(row => row.inspection !== 'master_frame_pattern_saved_reopened') }, envelopeGate)

const now = Date.now(), attempt = resolve(ROOT, 'data/parametric_attempts/PARAM-INMEMORY-ONLY')
const authorization = { phase: 'locks', planSha256: 'A'.repeat(64), executableSha256: 'B'.repeat(64), taskId: 'PARAM-INMEMORY-ONLY', workerId: 'worker-inmemory', leaseId: 'lease-inmemory', rightPartitionSha256: 'C'.repeat(64),
  previousResearchSha256: 'D'.repeat(64), researchOnly: true, predecessor: '', preInventory: [], preDoorInventory: [], issuedAtUtc: new Date(now).toISOString(), expiresAt: new Date(now + 22 * 60 * 1000).toISOString() }
const authExpected = { ...authorization, attempt, authorizationPath: resolve(attempt, 'authorizations/locks.json'), taskPath: resolve(attempt, 'task_store/PARAM-INMEMORY-ONLY.json'), state: 'building', leaseExpiresAt: new Date(now + 30 * 60 * 1000).toISOString() }
check('new matching phase authorization passes pure binding checks', () => assert.equal(checkAuthorization(authorization, authExpected, now), true))
reject('old phase authorization is rejected', authorization, value => { value.phase = 'envelope' }, value => checkAuthorization(value, authExpected, now))
reject('wrong task lease is rejected', authorization, value => { value.leaseId = 'old-lease' }, value => checkAuthorization(value, authExpected, now))
reject('stale authorization is rejected', authorization, value => { value.issuedAtUtc = new Date(now - 61000).toISOString() }, value => checkAuthorization(value, authExpected, now))
reject('expired authorization is rejected', authorization, value => { value.expiresAt = new Date(now - 1).toISOString() }, value => checkAuthorization(value, authExpected, now))

function lockGeometry(side, rows, mirrored = false) {
  const sign = side === 'R' ? -1 : 1
  return { side, body_count: 1, feature_count: 30, error_feature_count: 0, warning_feature_count: 0, has_sheet_metal: true, has_flat_pattern: true, traversal_complete: true,
    forbidden_import_feature_count: 0, mirror_feature_count: mirrored ? 1 : 0, edge_count: 2,
    edges: [{ vertex_start_mm: [sign * 20, 0, -1], vertex_end_mm: [sign * 20, 10, -1] }, { vertex_start_mm: [sign * 20, 10, -1], vertex_end_mm: [sign * 30, 10, -1] }],
    slot_rows_mm: [...rows], circle_rows_mm: rows.map(row => row - 60), slot_fragment_count: 0, circle_fragment_count: 0, unique_slot_profile_signature_count: 1,
    slot_analyses: rows.map(() => ({ full_fingerprint: true, side_orientation_exact: true })), circle_analyses: rows.map(() => ({ full_diameter5_pair: true, side_orientation_exact: true })) }
}
const testWorking = resolve(attempt, 'native_cad/working_pack'), leftPath = resolve(testWorking, LEFT_NAME), rightPath = resolve(testWorking, RIGHT_NAME), parameterPath = resolve(testWorking, RIGHT_SOURCE_NAME)
const leftRows = original.contract.rowsByColumn.L.map(row => row.centerYmm), rightRows = original.contract.rowsByColumn.R.map(row => row.centerYmm)
const leftGeometry = lockGeometry('L', leftRows), parameterGeometry = lockGeometry('L', rightRows), rightGeometry = lockGeometry('R', rightRows, true)
const locks = { phase: 'locks', success: true, rows: [{ side: 'L', reusedMatchingNativeSlots: false, geometry: leftGeometry }, { side: 'R', generatedGeometry: parameterGeometry },
  { side: 'R', mirrorGeometry: rightGeometry, rightParameterSource: parameterPath, referenceFiles: [parameterPath], referenceStatuses: [3], localSourceBound: true }, { side: 'R', reusedMatchingNativeSlots: false, geometry: rightGeometry }] }
const before = [{ path: leftPath, sha256: '1'.repeat(64) }, { path: rightPath, sha256: '2'.repeat(64) }]
const after = [{ path: leftPath, sha256: '3'.repeat(64) }, { path: rightPath, sha256: '4'.repeat(64) }, { path: parameterPath, sha256: '5'.repeat(64) }]
const lockBinding = { working: testWorking, before, after, pending: { state: 'pending_native_locks_replacement', finalGeometryAccepted: false, rightAfterEnvelopeSha256: before[1].sha256, envelopeLeftSha256: before[0].sha256 },
  rightBackupSha256: before[1].sha256, leftBackupSha256: before[0].sha256, leftSeedSha256: before[0].sha256,
  processProof: { stdout: 'MIRROR_VERTICES_VERIFIED 3\nPARAMETRIC_LOCK_COLUMN_VERIFIED L\nPARAMETRIC_LOCK_COLUMN_VERIFIED R\n' } }
const locksGate = value => checkLocks(value, 'locks', original.contract, lockBinding)
let lockObservation
check('native locks binding and exact left seed/backups yield research-only replacement observation', () => { lockObservation = locksGate(locks); assert.equal(lockObservation.lockSeedExactlyMatchesPreLocksLeft, true); assert.equal(lockObservation.exactRightBoundingBoxVerified, false) })
reject('old matching-slot reuse cannot discharge the right', locks, value => { value.rows.at(-1).reusedMatchingNativeSlots = true }, locksGate)
reject('L4 lock row drift is rejected', locks, value => { value.rows[0].geometry.slot_rows_mm[0] += 1 }, locksGate)
reject('R9 lock row drift is rejected', locks, value => { value.rows.at(-1).geometry.slot_rows_mm.pop() }, locksGate)
reject('nonlocal right parameter source is rejected', locks, value => { value.rows[2].referenceFiles[0] = resolve(ROOT, 'old', RIGHT_SOURCE_NAME) }, locksGate)
for (const status of [0, 1, 2, 5, 99]) reject('right broken/locked/dangling/unknown status rejected: ' + status, locks, value => { value.rows[2].referenceStatuses[0] = status }, locksGate)
check('known out-of-context local source status is recorded and accepted', () => { const changed = structuredClone(locks); changed.rows[2].referenceStatuses[0] = 4; assert.deepEqual(locksGate(changed).referenceStatuses, [4]) })
check('missing local parameter source is rejected', () => assert.throws(() => checkLocks(locks, 'locks', original.contract, { ...lockBinding, after: after.slice(0, 2) })))
check('unchanged formal right, wrong backup and wrong left seed are rejected', () => {
  for (const binding of [{ ...lockBinding, after: [after[0], before[1], after[2]] }, { ...lockBinding, rightBackupSha256: 'A'.repeat(64) }, { ...lockBinding, leftSeedSha256: 'A'.repeat(64) }]) assert.throws(() => checkLocks(locks, 'locks', original.contract, binding))
})
check('missing original mirror stdout evidence is rejected', () => assert.throws(() => checkLocks(locks, 'locks', original.contract, { ...lockBinding, processProof: { stdout: '' } })))
check('captured reflected vertices are checked without inventing a bbox', () => { assert.equal(checkReflectedVertices(parameterGeometry, rightGeometry).twoWayReflectionPassed, true); const changed = structuredClone(rightGeometry); changed.edges[0].vertex_start_mm[0] += 1; assert.throws(() => checkReflectedVertices(parameterGeometry, changed)) })
const verify = { phase: 'locks-verify', success: true, rows: [{ side: 'L', reusedMatchingNativeSlots: true, geometry: leftGeometry }, { side: 'R', reusedMatchingNativeSlots: true, geometry: rightGeometry }] }
check('saved locks verify discharges exact deferred right for research only', () => assert.equal(checkLocks(verify, 'locks-verify', original.contract, { ...lockBinding, locksObservation: lockObservation }).state, 'deferred_right_discharged_for_research_only'))
check('saved locks verify rejects changed right bytes', () => { const changed = structuredClone(after); changed[1].sha256 = '9'.repeat(64); assert.throws(() => checkLocks(verify, 'locks-verify', original.contract, { ...lockBinding, after: changed, locksObservation: lockObservation })) })

// A minimal native-shaped fixture tests exact precision and local provenance, not CAD behavior.
function doorFixture(module) {
  const transaction = resolve(attempt, 'transactions/inmemory'), output = resolve(attempt, 'doors/H' + module.token)
  const health = { body_count: 1, feature_count: 20, error_feature_count: 0, warning_feature_count: 0, has_sheet_metal: true, has_flat_pattern: true, traversal_complete: true, foreign_feature_count: 0, bbox_from_body_extreme_points: true, x_length_mm: 387, y_length_mm: module.heightMm, detached_mirror_folder_count: 1 }
  const localSources = Object.fromEntries(Object.entries(SOURCE_SHA).map(([role, sha256]) => [role, { path: resolve(attempt, 'door_sources', sourceLeaf(role)), sha256 }]))
  const files = Array.from({ length: 13 }, (_, i) => ({ relative_path: 'parts/p' + i + '.SLDPRT', sha256: String(i % 10).repeat(64), size_bytes: 100 }))
  const result = { committed_output_dir: output, relocation_reopen_passed: true, committed_reopen_passed: true, transaction_root: transaction, relocation_probe_root: resolve(transaction, 'relocation_probe'),
    backups: Object.entries(localSources).slice(0, 8).map(([role, spec]) => ({ role, source_path: spec.path, source_sha256: spec.sha256, backup_path: resolve(transaction, 'source_backups', sourceLeaf(role)), backup_sha256: spec.sha256 })),
    part_edits: [{ role: 'left_panel', target_width_mm: 387, target_height_mm: module.heightMm, after: { ...health } }, { role: 'stiffener', target_width_mm: 0, target_height_mm: module.heightMm - 10.5, after: { ...health } }],
    mirror_part: { gate: true, api: 'IPartDoc.MirrorPart2', break_link: true, external_reference_count: 0, after: { ...health } },
    assemblies: Array.from({ length: 4 }, (_, i) => ({ rebuilt: true, saved: true, expected_component_count: i < 2 ? 5 : 7, placements: Array.from({ length: i < 2 ? 5 : 7 }, () => ({ opened: true, added: true, transform_applied: true, path: resolve(transaction, 'candidate/parts/p.SLDPRT') })) })),
    reopen_checks: ['read_only_reopen', 'relocation_reopen', 'committed_reopen'].flatMap(phase => [{ phase, role: 'left_panel', reference_count: 0, gate: true }, { phase, role: 'right_panel', reference_count: 0, gate: true }, { phase, role: 'stiffener', reference_count: 0, gate: true }, ...Array.from({ length: 4 }, () => ({ phase, role: 'assembly', reference_count: 5, transform_gate: true, open_warnings: 32, gate: true }))]),
    sessions: Array.from({ length: 4 }, () => ({ exclusive_identity_gate: true, process_exited: true, cad_baseline_restored: true })), output_files: files }
  return { proof: { success: true, heightMm: module.heightMm, result }, sources: localSources, rows: files.map(row => ({ path: resolve(output, row.relative_path), sha256: row.sha256, size: row.size_bytes })) }
}
const door = doorFixture(DOOR_MODULES[1]), doorGate = value => checkDoor(value, DOOR_MODULES[1], attempt, door.sources, door.rows, { stdout: 'MIRROR_VERTICES_VERIFIED 5' })
check('full-precision native-shaped door fixture passes local/reopen gates', () => assert.equal(doorGate(door.proof).heightMm, 205.55555555555554))
reject('rounded native door wrapper height rejected', door.proof, value => { value.heightMm = 205.555556 }, doorGate)
reject('rounded native panel edit height rejected', door.proof, value => { value.result.part_edits[0].target_height_mm = 205.555556 }, doorGate)
reject('missing native door reopen pass rejected', door.proof, value => { value.result.reopen_checks.pop() }, doorGate)
reject('old door source SHA rejected', door.proof, value => { value.result.backups[0].source_sha256 = 'A'.repeat(64) }, doorGate)
reject('old door output directory rejected', door.proof, value => { value.result.committed_output_dir = resolve(v1Attempt, 'doors/H205p555556') }, doorGate)

check('static checks did not create an attempt', () => assert.deepEqual(readdirSync(resolve(ROOT, 'data/parametric_attempts')).sort(), beforeAttempts))
const path = resolve(here, 'static-check-' + new Date().toISOString().replace(/[^0-9]/g, '') + '-' + process.pid + '.json')
writeFileSync(path, JSON.stringify({ schema: 'obsolete-door-stage-continuation-static-check-v2', nativeCadExecuted: false, attemptCreated: false, taskCreated: false,
  fixtureBoundary: 'Synthetic fixtures are in-memory evidence-gate tests; only the actual v1 envelope and original sources are historical file evidence. No CAD geometry is inferred.',
  checks: results, count: results.length, failed: 0, artifactVersions: ['run-stage-continuation.mjs', 'research-gates.mjs', 'static-check.mjs'].map(name => ({ path: resolve(here, name), sha256: hash(resolve(here, name)) })), modelAccepted: false }, null, 2) + '\n', { flag: 'wx' })
console.log(JSON.stringify({ path, sha256: hash(path), count: results.length, failed: 0, nativeCadExecuted: false }, null, 2))
