// Throwaway research gates; these observations never authorize production acceptance.
import { basename, resolve, relative, isAbsolute } from 'node:path'

const need = (condition, message) => { if (!condition) throw Error(message) }
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b)
export const equalPath = (a, b) => typeof a === 'string' && typeof b === 'string' && resolve(a).toLowerCase() === resolve(b).toLowerCase()
const under = (root, path) => { const rel = relative(root, resolve(path)); return rel && !rel.startsWith('..') && !isAbsolute(rel) }
const unique = (rows, predicate, label) => { const found = rows.filter(predicate); need(found.length === 1, 'Expected unique ' + label); return found[0] }
const near = (a, b, tolerance) => Number.isFinite(a) && Number.isFinite(b) && Math.abs(a - b) < tolerance
const numbers = (a, b, tolerance) => Array.isArray(a) && Array.isArray(b) && a.length === b.length && a.every((value, index) => near(value, b[index], tolerance))
export const STAGES = ['clone', 'repair-base-foot', 'locks', 'locks-verify']
export const DOOR_MODULES = [{ token: '471p25', heightMm: 471.25 }, { token: '205p555556', heightMm: 205.55555555555554 }]
export const RIGHT_NAME = '箱体竖隔板R.SLDPRT'
export const LEFT_NAME = '箱体竖隔板L.sldprt'
export const RIGHT_SOURCE_NAME = '箱体竖隔板R参数源.SLDPRT'
export const V6_RIGHT_SHA = '1DB48AC81F9D48B6BE43E86348E0363CCC5195A8BD21AC7C282063C7A2C6384A'
export const FROZEN_RIGHT_GEOMETRY = { body_count: 1, feature_count: 58, error_feature_count: 0, warning_feature_count: 0, has_sheet_metal: true, has_flat_pattern: true,
  part_box_m: [0.02119999999999972, 0.028799999999999937, -0.5482000000000001, 0.06300252499999982, 1.8582, -0.018497474999999874] }
export const SOURCE_SHA = {
  left_panel_seed: 'F34138A9BE009D5A0C68604F57B278584DB686623943C2DE319A2999ACA488A2',
  stiffener_seed: 'F4D4973F72CB604BF52BF802BC378E9BA042A4FE8034C196CA7666A964ACCB28',
  latch_plate: '4B511C2FB2371060D998F2C0667C12980DF4A151E5CF4FDCD2934F7C7AAC1C5C',
  hook_pad: '93378BDFB9666CDE8379F59A0506E7A4508AFAEDAC47121C3B8CBDAE56480834',
  bushing: '930EE6070D278D5299D1EECED4F43367EE404A0E921517DE45297C30A8A12C7B',
  hinge_pin: 'B7289A146C9827050573FACEF66DCA06919FC41B943C39548AF536FE21516BC0',
  circlip: '84ACBBAD72BC3DB175AD77C5DB5ED5F4A74410435C3CDEF1574B6A7808DCC754',
  mechanical_lock_tongue: '26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719',
  top_latch_left: 'BC48FAC6E6D83F93E82E67AF0F71180C4733C5F0C9F695A079648972BD5DE847',
  top_latch_right: 'B9EFC9E1F65A15922A8887A2BE2F7B8B8B5A76D4648DC74DB3983C79F44C7A97',
  base_stiffener_source: 'C66D9077E508AAD2CD8CC6C0B0E73F903FA619493E9A4769C53A14865A74B249',
  maintenance_weldment: '3FDCFE0B02BE24BFD290C5EA7AC60A2457E9854805E0657B624C77474919D0BB',
  maintenance_lock_bracket: '2A5724BFCB31E468A55A9DB04943C8D65C28BFB59795835395E8EBCE515403A9',
}
export const sourceLeaf = role => role + (role === 'maintenance_weldment' ? '.SLDASM' : '.SLDPRT')

export function checkPrecision(plan) {
  need(same(plan.doorModules, DOOR_MODULES), 'Full-precision contract door module map changed')
  need(plan.contract?.geometry?.doorCount === 13 && same(plan.contract.geometry.columnDoorCounts, [4, 9]), 'L4/R9 contract changed')
  for (const [index, side] of ['L', 'R'].entries()) {
    const rows = plan.contract.rowsByColumn[side]
    need(rows.length === [4, 9][index] && rows.every(row => row.side === side && row.doorHeightMm === DOOR_MODULES[index].heightMm), 'Door module precision drift: ' + side)
  }
  return true
}

export function checkSourceManifest(manifest, directory, actualRows) {
  need(same(Object.keys(manifest).sort(), Object.keys(SOURCE_SHA).sort()), 'Exact 13 source roles required')
  need(actualRows.length === 13, 'Exact 13 source files required')
  for (const [role, expected] of Object.entries(SOURCE_SHA)) {
    const path = resolve(directory, sourceLeaf(role)), spec = manifest[role]
    need(spec && same(Object.keys(spec).sort(), ['path', 'sha256']) && spec.path === path && spec.sha256 === expected, 'Door source role/path/SHA mismatch: ' + role)
    const row = unique(actualRows, row => row.path === path, 'source file ' + role)
    need(row.sha256 === expected && row.size > 0, 'Door source bytes changed: ' + role)
  }
  return { roles: 13, exactRolePathShaBinding: true, originalBytesUnmodified: true }
}

export function checkAuthorization(auth, expected, now = Date.now()) {
  for (const field of ['phase', 'planSha256', 'executableSha256', 'taskId', 'workerId', 'leaseId', 'rightPartitionSha256', 'previousResearchSha256']) need(auth[field] === expected[field], 'Fresh phase authorization mismatch: ' + field)
  need(auth.researchOnly === true && auth.predecessor === '' && same(auth.preInventory, expected.preInventory) && same(auth.preDoorInventory, expected.preDoorInventory), 'Phase authorization inventory/receipt boundary mismatch')
  need(under(expected.attempt, expected.authorizationPath) && equalPath(expected.taskPath, resolve(expected.attempt, 'task_store', auth.taskId + '.json')), 'Authorization/task outside new attempt')
  need(expected.state === 'building' && Number.isFinite(Date.parse(expected.leaseExpiresAt)) && Date.parse(expected.leaseExpiresAt) > now &&
    Number.isFinite(Date.parse(auth.expiresAt)) && Date.parse(auth.expiresAt) > now && Date.parse(auth.expiresAt) <= now + 23 * 60 * 1000, 'Authorization/lease expired or invalid')
  need(Number.isFinite(Date.parse(auth.issuedAtUtc)) && Date.parse(auth.issuedAtUtc) <= now && now - Date.parse(auth.issuedAtUtc) < 60000, 'Old phase authorization forbidden')
  return true
}

export function checkDeferredRight(summary, binding) {
  need(summary.file === RIGHT_NAME && same(summary.beforeGeometry, FROZEN_RIGHT_GEOMETRY) && same(summary.afterGeometry, FROZEN_RIGHT_GEOMETRY), 'Only the exact frozen right geometry may be deferred')
  need(summary.links.length === 1, 'Right must contain the single frozen MirrorStock')
  const link = summary.links[0]
  need(link.feature === '箱体竖隔板L镜向' && link.type === 'MirrorStock' && link.blockedByBrokenReference === true && same(link.sourceStatuses, [0, 0, 0, 0, 0, 0, 0]) &&
    Array.isArray(link.referenceFiles) && link.referenceFiles.length === 7 && link.referenceFiles.every(path => equalPath(path, resolve(binding.working, LEFT_NAME))), 'Right frozen MirrorStock binding differs')
  need(binding.sourceRightSha256 === V6_RIGHT_SHA && binding.cloneObservation.allFrozenDependencyEdgesReplaced === true && binding.cloneObservation.replacementCount === 140 &&
    equalPath(binding.rightBefore.path, resolve(binding.working, RIGHT_NAME)) && /^[A-F0-9]{64}$/.test(binding.rightBefore.sha256) && binding.authorization.rightPartitionSha256 === binding.rightBefore.sha256, 'Right lacks legal frozen clone and fresh authorization provenance')
  return { state: 'pending_native_locks_replacement', rightBeforeSha256: binding.rightBefore.sha256, sourceRightSha256: binding.sourceRightSha256,
    exactFrozenGeometryOnly: true, sourceStatuses: [...link.sourceStatuses], finalGeometryAccepted: false, mustBeDischargedBy: ['locks', 'locks-verify'],
    frozenNativeProfile: 'readonly-open-20261110-r4-doorprecision3', sourceResponsibility: 'NativeParametricEnvelope.cs retains this exact MirrorStock; NativeParametricLocks.cs Run creates the R parameter source and replaces the formal right only after native geometry/reflection gates' }
}

function partGate(value, mirror = false) {
  return value?.body_count === 1 && value.feature_count > 0 && value.error_feature_count === 0 && value.warning_feature_count === 0 && value.has_sheet_metal === true && value.has_flat_pattern === true &&
    value.traversal_complete === true && value.foreign_feature_count === 0 && value.bbox_from_body_extreme_points === true && (!mirror || value.mirror_part_feature_count >= 1 || value.detached_mirror_folder_count >= 1)
}
export function checkDoor(proof, module, attempt, sourceManifest, outputRows, processProof) {
  need(proof.success === true && proof.heightMm === module.heightMm, 'Door height lost contract precision or native phase failed')
  const result = proof.result, output = resolve(attempt, 'doors', 'H' + module.token), width = 387
  need(result && equalPath(result.committed_output_dir, output) && result.relocation_reopen_passed === true && result.committed_reopen_passed === true, 'Door commit/reopen missing')
  need(under(resolve(attempt, 'transactions'), result.transaction_root) && under(result.transaction_root, result.relocation_probe_root), 'Door transaction is outside the new attempt')
  const roles = ['left_panel_seed', 'stiffener_seed', 'latch_plate', 'hook_pad', 'bushing', 'hinge_pin', 'circlip', 'mechanical_lock_tongue']
  need(result.backups.length === 8, 'Door source backup coverage differs')
  for (const role of roles) {
    const row = unique(result.backups, row => row.role === role, 'door source backup ' + role)
    need(equalPath(row.source_path, sourceManifest[role].path) && row.source_sha256 === SOURCE_SHA[role] && row.backup_sha256 === SOURCE_SHA[role] && under(result.transaction_root, row.backup_path), 'Door source backup path/SHA mismatch: ' + role)
  }
  const left = unique(result.part_edits, row => row.role === 'left_panel', 'left panel edit')
  need(left.target_width_mm === width && left.target_height_mm === module.heightMm && partGate(left.after) && near(left.after.x_length_mm, width, .001) && near(left.after.y_length_mm, module.heightMm, .001), 'Door panel dimension or native health gate failed')
  const stiffener = unique(result.part_edits, row => row.role === 'stiffener', 'stiffener edit')
  need(stiffener.target_width_mm === 0 && stiffener.target_height_mm === module.heightMm - 10.5 && partGate(stiffener.after), 'Door stiffener precision/health failed')
  need(result.mirror_part?.gate === true && result.mirror_part.api === 'IPartDoc.MirrorPart2' && result.mirror_part.break_link === true && result.mirror_part.external_reference_count === 0 && partGate(result.mirror_part.after, true), 'Door native mirror evidence incomplete')
  need(result.assemblies.length === 4 && result.assemblies.every(row => row.rebuilt === true && row.saved === true && row.placements.length === row.expected_component_count && row.placements.every(item => item.opened === true && item.added === true && item.transform_applied === true && under(result.transaction_root, item.path))), 'Door assembly construction evidence incomplete')
  need(result.reopen_checks.length === 21, 'Door requires three complete seven-document reopen passes')
  for (const phase of ['read_only_reopen', 'relocation_reopen', 'committed_reopen']) {
    const rows = result.reopen_checks.filter(row => row.phase === phase)
    need(rows.length === 7 && rows.every(row => row.gate === true), 'Door reopen checks incomplete: ' + phase)
    for (const role of ['left_panel', 'right_panel', 'stiffener']) need(unique(rows, row => row.role === role, 'door reopen ' + role).reference_count === 0, 'Door part has external reference')
    need(rows.filter(row => row.reference_count > 0).length === 4 && rows.filter(row => row.reference_count > 0).every(row => row.transform_gate === true && [0, 32].includes(row.open_warnings)), 'Door assembly path/transform reopen failed')
  }
  need(result.sessions.length === 4 && result.sessions.every(row => row.exclusive_identity_gate === true && row.process_exited === true && row.cad_baseline_restored === true), 'Door owned sessions did not finish cleanly')
  const nativeFiles = result.output_files.filter(row => /\.SLD(?:PRT|ASM)$/i.test(row.relative_path))
  need(nativeFiles.length === 13 && outputRows.length === 13, 'Door requires exact 13 CAD output files')
  for (const row of outputRows) {
    const expected = unique(nativeFiles, file => equalPath(resolve(output, file.relative_path), row.path), 'door output file')
    need(expected.sha256 === row.sha256 && expected.size_bytes === row.size, 'Door output bytes differ from native evidence')
  }
  need(processProof.stdout.includes('MIRROR_VERTICES_VERIFIED '), 'Door mirror vertex evidence missing')
  return { token: module.token, heightMm: module.heightMm, independentSourceCopyRoles: 8, sourceManifestRoles: 13, outputCadCount: 13,
    completeDoorReopenChecks: 21, nativeMirrorVerticesVerified: true, completeCabinetColdReopenVerified: false }
}

export function checkRepair(proof, phase, working, doors, modules) {
  need(proof.phase === phase && proof.success === true && Array.isArray(proof.rows), 'Native repair phase failed: ' + phase)
  if (phase === 'repair-door-placements') {
    need(proof.rows.length === 4, 'Door placement rows must cover two sides of both modules')
    for (const module of modules) for (const side of ['left', 'right']) unique(proof.rows, row => row.heightMm === module.heightMm &&
      equalPath(row.assembly, resolve(doors, 'H' + module.token, 'assemblies', 'door_weld_' + side + '_W387_H' + module.token + '.SLDASM')) && row.method === 'V35 verified orientations and parameter-translated original top latch', 'door placement ' + module.token + '/' + side)
  } else if (phase === 'repair-interfaces') {
    const part = unique(proof.rows, row => equalPath(row.part, resolve(working, '箱体右侧板.sldprt')), 'interface part')
    need([0, 2].includes(part.removedReferences) && numbers(part.before, part.after, .001), 'Interface removed-reference or geometry gate failed')
    for (const name of ['箱体右侧板焊接.SLDASM', '标准寄存柜1917×1000×550(总装配).SLDASM']) unique(proof.rows, row => equalPath(resolve(working, row.rebuilt || 'missing'), resolve(working, name)), 'interface rebuilt ' + name)
    need(proof.rows.length === 3, 'Extra/missing interface rows')
  } else if (phase === 'repair-base-foot') {
    const measured = unique(proof.rows, row => Array.isArray(row.measuredCylinderXmm), 'base measured cylinder')
    const actual = unique(proof.rows, row => Array.isArray(row.actualCylinderXmm), 'base actual cylinder')
    for (const [values, expected] of [[measured.measuredCylinderXmm, measured.expectedFootXmm], [actual.actualCylinderXmm, actual.expectedFootXmm]]) need(same(expected, [-415, 415]) && expected.every(value => values.some(item => near(item, value, .001))), 'Base foot cylinder alignment failed')
  }
  return { phase, nativeEvidencePassed: true, completeCabinetColdReopenVerified: false }
}

function lockGeometry(value, side, rows) {
  need(value && value.side === side && value.body_count === 1 && value.feature_count > 0 && value.error_feature_count === 0 && value.warning_feature_count === 0 && value.has_sheet_metal === true && value.has_flat_pattern === true && value.traversal_complete === true && value.forbidden_import_feature_count === 0 &&
    value.edge_count > 0 && value.edges.length === value.edge_count && numbers(value.slot_rows_mm, rows, .01) && numbers(value.circle_rows_mm, rows.map(row => row - 60), .01) &&
    value.slot_fragment_count === 0 && value.circle_fragment_count === 0 && value.unique_slot_profile_signature_count === 1, 'Native lock geometry/rows failed: ' + side)
  need(value.slot_analyses.filter(row => row.full_fingerprint === true).length === rows.length && value.slot_analyses.filter(row => row.full_fingerprint === true).every(row => row.side_orientation_exact === true) &&
    value.circle_analyses.filter(row => row.full_diameter5_pair === true).length === rows.length && value.circle_analyses.filter(row => row.full_diameter5_pair === true).every(row => row.side_orientation_exact === true), 'Native lock row fingerprints failed: ' + side)
}
function vertices(value) {
  const output = []
  for (const edge of value.edges) for (const vertex of [edge.vertex_start_mm, edge.vertex_end_mm]) {
    need(Array.isArray(vertex) && [0, 3].includes(vertex.length) && vertex.every(Number.isFinite), 'Invalid captured vertex')
    if (vertex.length && !output.some(existing => numbers(existing, vertex, .002))) output.push(vertex)
  }
  need(output.length > 0, 'Missing captured lock vertices')
  return output
}
export function checkReflectedVertices(source, right) {
  const a = vertices(source), b = vertices(right)
  need(a.length === b.length && a.every(vertex => b.some(other => numbers([-vertex[0], vertex[1], vertex[2]], other, .002))) &&
    b.every(vertex => a.some(other => numbers([-vertex[0], vertex[1], vertex[2]], other, .002))), 'Captured lock vertices are not an X reflection')
  return { distinctCapturedVertices: a.length, toleranceMm: .002, twoWayReflectionPassed: true, finiteSurfaceBoundariesFullyCompared: false }
}
export function checkLocks(proof, phase, contract, binding) {
  need(proof.phase === phase && proof.success === true && Array.isArray(proof.rows), 'Native lock phase failed')
  const result = {}
  for (const side of ['L', 'R']) {
    const row = unique(proof.rows, row => row.side === side && Object.hasOwn(row, 'reusedMatchingNativeSlots'), 'final lock ' + side)
    need(row.reusedMatchingNativeSlots === (phase === 'locks-verify'), phase === 'locks' ? 'Old native slots cannot discharge deferred right replacement' : 'Verification may not rebuild or replace lock slots')
    lockGeometry(row.geometry, side, contract.rowsByColumn[side].map(row => row.centerYmm))
    result[side] = row.geometry
  }
  need(result.R.mirror_feature_count >= 1, 'Right must retain native mirror feature')
  const parameterSource = resolve(binding.working, RIGHT_SOURCE_NAME), right = resolve(binding.working, RIGHT_NAME)
  const rightAfter = unique(binding.after, row => equalPath(row.path, right), 'right file after locks')
  const sourceAfter = unique(binding.after, row => equalPath(row.path, parameterSource), 'parameter source after locks')
  need(binding.pending?.state === 'pending_native_locks_replacement' && binding.pending.finalGeometryAccepted === false, 'Locks require exact pending right provenance')
  need(binding.pending.rightAfterEnvelopeSha256 === binding.pending.sourceRightAfterEnvelopeSha256 && binding.pending.sourceRightAfterEnvelopeSha256 === binding.pending.sourceRightAfterInterfacesSha256 &&
    binding.pending.envelopeLeftSha256 === binding.pending.sourceLeftAfterEnvelopeSha256 && binding.pending.sourceLeftAfterEnvelopeSha256 === binding.pending.sourceLeftAfterInterfacesSha256 &&
    [binding.pending.sourceRightAfterEnvelopeSha256, binding.pending.sourceLeftAfterEnvelopeSha256, binding.pending.postCloneRightSha256, binding.pending.postCloneLeftSha256].every(value => typeof value === 'string' && /^[A-F0-9]{64}$/.test(value)), 'Ancestor envelope/interface hashes or fresh post-clone provenance differ')
  if (phase === 'locks') {
    const mirror = unique(proof.rows, row => row.side === 'R' && Object.hasOwn(row, 'rightParameterSource'), 'right local mirror binding')
    need(equalPath(mirror.rightParameterSource, parameterSource) && mirror.localSourceBound === true && Array.isArray(mirror.referenceFiles) && mirror.referenceFiles.length > 0 && mirror.referenceFiles.every(path => equalPath(path, parameterSource)) &&
      Array.isArray(mirror.referenceStatuses) && mirror.referenceStatuses.length === mirror.referenceFiles.length && mirror.referenceStatuses.every(value => [3, 4].includes(value)), 'Right mirror source/status is not a healthy new local parameter-source binding')
    need(same(mirror.mirrorGeometry, result.R), 'Right mirror/final snapshots disagree')
    const generated = unique(proof.rows, row => row.side === 'R' && Object.hasOwn(row, 'generatedGeometry'), 'R generated source geometry').generatedGeometry
    lockGeometry(generated, 'L', contract.rowsByColumn.R.map(row => row.centerYmm))
    const before = unique(binding.before, row => equalPath(row.path, right), 'right before locks')
    need(before.sha256 === binding.pending.postCloneRightSha256 && rightAfter.sha256 !== before.sha256 && sourceAfter.sha256 !== rightAfter.sha256 && binding.rightBackupSha256 === before.sha256, 'Right actual replacement SHA/backup provenance failed')
    need(binding.leftBackupSha256 === unique(binding.before, row => equalPath(row.path, resolve(binding.working, LEFT_NAME)), 'left before locks').sha256 && binding.leftSeedSha256 === binding.leftBackupSha256 && binding.pending.postCloneLeftSha256 === binding.leftSeedSha256, 'Post-clone left / lock seed / transactional backup provenance failed')
    need((binding.processProof.stdout.match(/MIRROR_VERTICES_VERIFIED [1-9][0-9]*/g) || []).length === 1 && ['L', 'R'].every(side => binding.processProof.stdout.includes('PARAMETRIC_LOCK_COLUMN_VERIFIED ' + side)), 'Native mirror/replacement stdout evidence missing')
    return { state: 'right_replaced_pending_saved_verify', rightSha256: rightAfter.sha256, rightParameterSource: sourceAfter, leftSha256: unique(binding.after, row => equalPath(row.path, resolve(binding.working, LEFT_NAME)), 'left after locks').sha256,
      preLocksLeftSha256: binding.leftBackupSha256, lockSeedLeftSha256: binding.leftSeedSha256, lockSeedExactlyMatchesPreLocksLeft: true,
      sourceLeftAfterEnvelopeSha256: binding.pending.sourceLeftAfterEnvelopeSha256, sourceLeftAfterInterfacesSha256: binding.pending.sourceLeftAfterInterfacesSha256,
      sourceRightAfterEnvelopeSha256: binding.pending.sourceRightAfterEnvelopeSha256, sourceRightAfterInterfacesSha256: binding.pending.sourceRightAfterInterfacesSha256, ancestorEnvelopeAndInterfaceLeftRightHashesMatched: true,
      postCloneLeftSha256: binding.pending.postCloneLeftSha256, postCloneRightSha256: binding.pending.postCloneRightSha256, lockSeedExactlyMatchesPostCloneLeft: true,
      preLocksRightSha256: before.sha256, rightBackupSha256: binding.rightBackupSha256, leftBackupSha256: binding.leftBackupSha256,
      referenceFiles: mirror.referenceFiles, referenceStatuses: mirror.referenceStatuses, acceptedReferenceStatusNames: { 3: 'swExternalReferenceInContext', 4: 'swExternalReferenceOutOfContext' }, brokenLockedDanglingAndUnknownStatusesRejected: true,
      rightParameterSourceGeometry: generated, reflection: checkReflectedVertices(generated, result.R), slotCounts: { L: 4, R: 9 }, nativeReplacementAndReflectionObserved: true,
      exactRightBoundingBoxVerified: false, fullPersistedSourceDependencyReenumerationVerified: false, completeColdReopenVerified: false }
  }
  need(proof.rows.length === 2 && binding.locksObservation?.state === 'right_replaced_pending_saved_verify' && rightAfter.sha256 === binding.locksObservation.rightSha256 &&
    sourceAfter.sha256 === binding.locksObservation.rightParameterSource.sha256 && equalPath(sourceAfter.path, binding.locksObservation.rightParameterSource.path) &&
    unique(binding.after, row => equalPath(row.path, resolve(binding.working, LEFT_NAME)), 'verified left').sha256 === binding.locksObservation.leftSha256, 'Saved locks verification reused different files')
  return { state: 'deferred_right_discharged_for_research_only', slotCounts: { L: 4, R: 9 }, unchangedSavedLeftRightAndParameterSource: true,
    reflection: checkReflectedVertices(binding.locksObservation.rightParameterSourceGeometry, result.R), exactRightBoundingBoxVerified: false, fullPersistedSourceDependencyReenumerationVerified: false, completeColdReopenVerified: false, modelAccepted: false }
}

export function checkContinuationClone(proof, mapping, aliases, frozenObserved = null) {
  need(proof.phase === 'clone' && proof.success === true && Array.isArray(proof.rows), 'Continuation clone native evidence failed')
  need(mapping.length === 105 && mapping.filter(row => row.kind === 'working').length === 75 && mapping.filter(row => row.kind === 'door').length === 30, 'Clone requires complete 75 working plus 30 door mapping')
  const owners = new Set(), replacements = [], observed = []
  for (const row of proof.rows) {
    const owner = unique(mapping, item => equalPath(item.destination, row.file), 'clone mapped owner')
    need(!owners.has(owner.source.toLowerCase()) && Array.isArray(row.replacements), 'Clone duplicate/invalid owner row')
    owners.add(owner.source.toLowerCase())
    const seen = new Set()
    for (const replacement of row.replacements) {
      const alias = unique(aliases, item => equalPath(item.path, replacement.source), 'exact dependency source or evidenced native alias')
      const target = unique(mapping, item => equalPath(item.source, alias.file), 'dependency target source mapping')
      need(replacement.replaced === true && equalPath(replacement.target, target.destination), 'Clone API replacement failed or escaped exact cross-pack mapping')
      const edge = replacement.source.toLowerCase() + '|' + replacement.target.toLowerCase()
      need(!seen.has(edge), 'Clone duplicate replacement edge'); seen.add(edge)
      replacements.push({ ownerSource: owner.source, oldSource: alias.path, mappedSource: target.source, destination: target.destination })
    }
    observed.push({ ownerSource: owner.source, replacementCount: row.replacements.length })
  }
  need(replacements.length > 0 && replacements.some(row => mapping.some(item => item.kind === 'working' && equalPath(item.source, row.ownerSource))) && replacements.some(row => mapping.some(item => item.kind === 'door' && equalPath(item.source, row.ownerSource))), 'Clone must observe real replacements in both working and doors')
  if (frozenObserved) {
    need(frozenObserved.rows.length === proof.rows.length && frozenObserved.replacementCount === replacements.length, 'Frozen observed clone row/replacement counts differ')
    need(same(frozenObserved.replacements, replacements), 'Frozen observed clone replacement inventory differs')
  }
  return { phase: 'clone', ownerRowsObserved: observed, replacementCount: replacements.length, replacements, allObservedApisReturnedTrue: true, exactWorkingAndDoorCrossMapping: true,
    frozenCurrentDependencyGraphAvailable: false, allFrozenCurrentDependencyEdgesCovered: false, allFrozenDependencyEdgesReplaced: false, fullPostCloneDependencyReenumerationVerified: false,
    ancestor140EdgesReusedAsExpectation: false, currentReplacementCountIsObservedNotExpected: true, modelAccepted: false }
}
