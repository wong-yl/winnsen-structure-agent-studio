import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  report: 'data/locker_16029_three_part_interface_verification.json',
  out: 'data/locker_16029_three_part_interface_gate.json',
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path) {
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function checkExists(name, path, category = 'evidence') {
  check(name, existsSync(abs(path)), path, 'file exists', category)
}

const checks = []

function check(name, ok, actual, expected, category = 'three_part_interface') {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

function part(report, id) {
  return asArray(report.part_evidence).find((item) => item.part_id === id) || {}
}

function iface(report, id) {
  return asArray(report.interface_checks).find((item) => item.id === id) || {}
}

const report = readJson(PATHS.report)
const formal = report.current_formal_interface_package || {}
const stepProbe = report.current_formal_step_bbox_probe || {}
const historical = report.historical_native_envelope_reference || {}
const release = report.release_boundary || {}
const door = part(report, 'door_panel_2_12_right_v43')
const front = part(report, 'front_frame_vertical_left_v43')
const shelf = part(report, 'shelf_cross_panel_left_v43')

check('schema', report.schema === 'winnsen.locker16029.three_part_interface_verification.v1', report.schema, 'winnsen.locker16029.three_part_interface_verification.v1')
check('status_ready_for_native_rerun', report.status === 'READY_FOR_SW_NATIVE_INTERFACE_RERUN_NOT_RELEASED', report.status, 'READY_FOR_SW_NATIVE_INTERFACE_RERUN_NOT_RELEASED')
check('scope_three_parts_only', JSON.stringify(report.scope?.included_parts || []) === JSON.stringify([
  'door_panel_2_12_right_v43',
  'front_frame_vertical_left_v43',
  'shelf_cross_panel_left_v43',
]), report.scope?.included_parts, 'exact three pilot parts')
check('source_playbook_ready', report.source_route?.playbook_status === 'READY_FOR_THREE_PART_INTERFACE_ITERATION', report.source_route?.playbook_status, 'READY_FOR_THREE_PART_INTERFACE_ITERATION')
check('source_route_gate_pass', report.source_route?.route_gate_status === 'PASS' && asNumber(report.source_route?.route_gate_checks_failed) === 0, report.source_route, 'route gate PASS/0 failed')

check('placement_count_3', asNumber(formal.placement_count) === 3, formal.placement_count, 3)
check('formal_build_completed', formal.build_status === 'completed', formal.build_status, 'completed')
check('formal_sldasm_saved', formal.saved === true, formal.saved, true)
check('formal_reference_count_3', asNumber(formal.reference_count) === 3, formal.reference_count, 3)
check('formal_components_added_3', asNumber(formal.components_added) === 3, formal.components_added, 3)
check('formal_native_clearance_not_overclaimed', formal.formal_native_clearance_status === 'NOT_CLOSED_CURRENT_SLDASM_INSPECT_DID_NOT_OPEN_OR_ENUMERATE', formal.formal_native_clearance_status, 'current formal native clearance not closed')

check('door_validation_pilot_closed', door.validation_status === 'cad_closed_pending_engineering_signoff_for_supplier', door.validation_status, 'cad_closed_pending_engineering_signoff_for_supplier')
check('door_placement_matches_bom', door.placement_matches_bom_sldprt === true, door.placement?.path, 'current door SLDPRT')
check('door_added_to_sldasm', door.build_component?.added === true && door.build_component?.transform_applied === true, door.build_component, 'added and transformed')
check('door_step_bbox_sanity_pass', door.step_probe?.bbox_sanity?.status === 'PASS', door.step_probe?.bbox_sanity, 'PASS')
check('door_folded_bbox', JSON.stringify(door.part_checks?.folded_bbox_mm) === JSON.stringify([307, 298, 18.2]), door.part_checks?.folded_bbox_mm, [307, 298, 18.2])

check('front_validation_verified', front.validation_status === 'second_part_sheetmetal_verified', front.validation_status, 'second_part_sheetmetal_verified')
check('front_placement_matches_bom', front.placement_matches_bom_sldprt === true, front.placement?.path, 'current front-frame SLDPRT')
check('front_added_to_sldasm', front.build_component?.added === true && front.build_component?.transform_applied === true, front.build_component, 'added and transformed')
check('front_step_bbox_sanity_pass', front.step_probe?.bbox_sanity?.status === 'PASS', front.step_probe?.bbox_sanity, 'PASS')
check('front_hole_cut_count_12', asNumber(front.part_checks?.hole_cut_count) === 12, front.part_checks?.hole_cut_count, 12)

check('shelf_validation_verified', shelf.validation_status === 'third_part_sheetmetal_verified', shelf.validation_status, 'third_part_sheetmetal_verified')
check('shelf_placement_matches_bom', shelf.placement_matches_bom_sldprt === true, shelf.placement?.path, 'current shelf SLDPRT')
check('shelf_added_to_sldasm', shelf.build_component?.added === true && shelf.build_component?.transform_applied === true, shelf.build_component, 'added and transformed')
check('shelf_flat_verified', shelf.part_checks?.has_sheet_metal === true && shelf.part_checks?.has_flat_pattern === true && shelf.part_checks?.flat_exported === true, shelf.part_checks, 'sheetmetal/flatpattern/flat exported')
check('shelf_step_bbox_sanity_failure_recorded', shelf.step_probe?.bbox_sanity?.status === 'FAIL', shelf.step_probe?.bbox_sanity, 'FAIL recorded, not used as clearance')
check('shelf_flat_entity_total_92', asNumber(shelf.part_checks?.entity_count_total) === 92, shelf.part_checks?.entity_count_total, 92)

check('current_step_probe_degraded_recorded', stepProbe.status === 'FALLBACK_AABB_DEGRADED', stepProbe.status, 'FALLBACK_AABB_DEGRADED')
check('current_step_probe_not_used_as_clearance', String(stepProbe.use_in_gate || '').includes('must not be used as clearance signoff'), stepProbe.use_in_gate, 'diagnostic only')
check('historical_clearance_reference_only', historical.use_in_gate === 'reference_only_not_current_formal_geometry', historical.use_in_gate, 'reference_only_not_current_formal_geometry')
check('historical_clearance_was_separated', historical.clearance_status === 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED', historical.clearance_status, 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED')
check('historical_overlaps_zero', asNumber(historical.overlapping_pair_count) === 0, historical.overlapping_pair_count, 0)

check('interface_door_front_defined', iface(report, 'door_to_front_frame_datum').relation_contract?.check === 'door gap and hinge-side datum alignment', iface(report, 'door_to_front_frame_datum').relation_contract, 'door gap and hinge-side datum alignment')
check('interface_shelf_front_defined', iface(report, 'shelf_to_front_frame_locating').relation_contract?.check === 'front-edge and locating-hole interface', iface(report, 'shelf_to_front_frame_locating').relation_contract, 'front-edge and locating-hole interface')
check('interface_shelf_door_defined', iface(report, 'shelf_to_door_swing_clearance').relation_contract?.check === 'door swing and shelf front-edge clearance', iface(report, 'shelf_to_door_swing_clearance').relation_contract, 'door swing and shelf front-edge clearance')
check('sw_rerun_required', report.required_next_sw2020_rerun?.status === 'REQUIRED_BEFORE_INTERFACE_CLEARANCE_SIGNOFF', report.required_next_sw2020_rerun?.status, 'REQUIRED_BEFORE_INTERFACE_CLEARANCE_SIGNOFF')
check('sw_rerun_actions_cover_three_pairs', asArray(report.required_next_sw2020_rerun?.minimum_actions).some((item) => String(item).includes('door-front, shelf-front, and shelf-door')), report.required_next_sw2020_rerun?.minimum_actions, 'three pair rerun action')

check('bom_still_pilot_only', release.bom_status === 'pilot_only_not_released', release.bom_status, 'pilot_only_not_released')
check('supplier_release_excluded', release.supplier_release_status === 'supplier_release_excluded', release.supplier_release_status, 'supplier_release_excluded')
check('cannot_issue_supplier_dxf', release.can_issue_supplier_dxf_now === false, release.can_issue_supplier_dxf_now, false)
check('cannot_expand_full_cabinet', release.can_expand_to_full_cabinet_now === false, release.can_expand_to_full_cabinet_now, false)

for (const path of [
  report.source_route?.playbook,
  report.source_route?.route_gate,
  formal.placements_tsv,
  formal.build_result_json,
  formal.inspect_json,
  stepProbe.report,
  historical.build_json,
  historical.components_json,
  historical.clearance_json,
].filter(Boolean)) {
  checkExists(`evidence_exists:${path}`, path)
}

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.three_part_interface_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length ? 'FAIL' : 'PASS',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  engineering_clearance_status: 'NOT_CLOSED_REQUIRES_SW2020_NATIVE_RERUN',
  release_boundary: {
    pilot_only: true,
    supplier_release_excluded: true,
    full_cabinet_generation_excluded: true,
  },
  checks,
}

writeFileSync(abs(PATHS.out), `${JSON.stringify(gate, null, 2)}\n`, 'utf8')
console.log(`${gate.status}: ${PATHS.out}`)
if (failed.length) {
  for (const item of failed) console.log(`- ${item.name}`)
  process.exit(1)
}
