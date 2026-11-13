import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  playbook: 'data/locker_16029_part_first_route_playbook.json',
  out: 'data/locker_16029_part_first_route_playbook_gate.json',
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

function almostEqual(a, b, tolerance = 0.001) {
  return Math.abs(Number(a) - Number(b)) <= tolerance
}

const checks = []

function check(name, ok, actual, expected, category = 'route_playbook') {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

function checkExists(name, path, category = 'route_playbook') {
  check(name, existsSync(abs(path)), path, 'file exists', category)
}

const playbook = readJson(PATHS.playbook)
const door = playbook.door_panel_rule || {}
const doorValidation = door.hole_and_flat_validation || {}
const second = playbook.second_part_verification || {}
const secondFlat = second.flat_check || {}
const third = playbook.third_part_verification || {}
const thirdFlat = third.flat_check || {}
const bomRows = asArray(playbook.pilot_bom?.rows)
const interfaces = playbook.assembly_interfaces || {}
const gate = playbook.minimal_gate_flow || {}
const releaseBoundary = gate.supplier_release_boundary || {}

check('schema', playbook.schema === 'winnsen.locker16029.part_first_route_playbook.v1', playbook.schema, 'winnsen.locker16029.part_first_route_playbook.v1')
check('status_ready_three_part_interface', playbook.status === 'READY_FOR_THREE_PART_INTERFACE_ITERATION', playbook.status, 'READY_FOR_THREE_PART_INTERFACE_ITERATION')
check('supplier_release_excluded', gate.status === 'supplier_release_excluded', gate.status, 'supplier_release_excluded')

check('door_route_simplehole', /SimpleHole2|SketchHole/i.test(door.proven_route || ''), door.proven_route, 'SimpleHole2/SketchHole route')
check('door_hinge_option_b', door.hinge_rule?.selected_option === 'B_ADJUST_TOP_LEFT_CORNER_RELIEF', door.hinge_rule?.selected_option, 'B_ADJUST_TOP_LEFT_CORNER_RELIEF')
check('door_hinge_inside_flat', door.hinge_rule?.upper_hinge_material_status === 'INSIDE_CURRENT_FLAT_PATTERN', door.hinge_rule?.upper_hinge_material_status, 'INSIDE_CURRENT_FLAT_PATTERN')
check('door_hole_cut_count', asNumber(doorValidation.result_hole_cut_count) === 4, doorValidation.result_hole_cut_count, 4)
check('door_flat_circle_count', asNumber(doorValidation.flat_dxf_entity_counts?.CIRCLE) >= 4, doorValidation.flat_dxf_entity_counts, 'CIRCLE >= 4')
check('door_flat_width', almostEqual(doorValidation.flat_bbox_mm?.width_mm, 343.4, 0.25), doorValidation.flat_bbox_mm, '343.4 +/- 0.25')
check('door_flat_height', almostEqual(doorValidation.flat_bbox_mm?.height_mm, 334.4, 0.25), doorValidation.flat_bbox_mm, '334.4 +/- 0.25')
check('door_folded_bbox', JSON.stringify(doorValidation.folded_bbox_mm) === JSON.stringify([307, 298, 18.2]), doorValidation.folded_bbox_mm, [307, 298, 18.2])

check('second_part_selected_front_frame', second.selected_part_id === 'front_frame_vertical_left_v43', second.selected_part_id, 'front_frame_vertical_left_v43')
check('second_part_sheetmetal', second.has_sheet_metal === true, second.has_sheet_metal, true)
check('second_part_flatpattern', second.has_flat_pattern === true, second.has_flat_pattern, true)
check('second_part_flat_exported', second.flat_exported === true, second.flat_exported, true)
check('second_part_step_saved', second.step_saved === true, second.step_saved, true)
check('second_part_hole_cut_count', asNumber(second.hole_cut_count) === 12, second.hole_cut_count, 12)
check('second_part_flat_status', secondFlat.status === 'PASS', secondFlat.status, 'PASS')
check('second_part_flat_width', almostEqual(secondFlat.bbox_mm?.width_mm, 49.3, 0.25), secondFlat.bbox_mm, '49.3 +/- 0.25')
check('second_part_flat_height', almostEqual(secondFlat.bbox_mm?.height_mm, 1834, 0.25), secondFlat.bbox_mm, '1834 +/- 0.25')

check('third_part_selected_shelf', third.selected_part_id === 'shelf_cross_panel_left_v43', third.selected_part_id, 'shelf_cross_panel_left_v43')
check('third_part_evidence_rule_splits_step_and_flat', /STEP\/open/i.test(third.evidence_reading_rule || '') && /SheetMetal\/FlatPattern/i.test(third.evidence_reading_rule || ''), third.evidence_reading_rule, 'STEP/open and SheetMetal/FlatPattern evidence split')
check('third_part_evidence_rule_bom_safe', /BOM/i.test(third.evidence_reading_rule || ''), third.evidence_reading_rule, 'UTF-8 BOM handling documented')
check('third_part_opened', third.opened === true, third.opened, true)
check('third_part_step_saved', third.step_saved === true, third.step_saved, true)
check('third_part_step_exists', third.step_exists === true, third.step_exists, true)
check('third_part_sheetmetal', third.has_sheet_metal === true, third.has_sheet_metal, true)
check('third_part_flatpattern', third.has_flat_pattern === true, third.has_flat_pattern, true)
check('third_part_flat_exported', third.flat_exported === true, third.flat_exported, true)
check('third_part_dxf_exists', third.dxf_exists === true, third.dxf_exists, true)
check('third_part_flat_status', thirdFlat.status === 'PASS', thirdFlat.status, 'PASS')
check('third_part_flat_width', almostEqual(thirdFlat.bbox_mm?.width_mm, 574.4, 0.25), thirdFlat.bbox_mm, '574.4 +/- 0.25')
check('third_part_flat_height', almostEqual(thirdFlat.bbox_mm?.height_mm, 480.4, 0.25), thirdFlat.bbox_mm, '480.4 +/- 0.25')
check('third_part_flat_circle_count', asNumber(thirdFlat.entity_counts?.CIRCLE) === 11, thirdFlat.entity_counts, 'CIRCLE == 11')
check('third_part_flat_arc_count', asNumber(thirdFlat.entity_counts?.ARC) === 11, thirdFlat.entity_counts, 'ARC == 11')
check('third_part_flat_entity_total', asNumber(thirdFlat.entity_count_total) === 92, thirdFlat.entity_count_total, 92)

check('bom_rows_cover_pilot_parts', ['door_panel_2_12_right_v43', 'front_frame_vertical_left_v43', 'shelf_cross_panel_left_v43'].every((id) => bomRows.some((row) => row.part_id === id)), bomRows.map((row) => row.part_id), 'door/front-frame/shelf rows')
check('bom_not_released', playbook.pilot_bom?.status === 'pilot_only_not_released', playbook.pilot_bom?.status, 'pilot_only_not_released')
check('bom_shelf_verified', bomRows.some((row) => row.part_id === 'shelf_cross_panel_left_v43' && row.validation_status === 'third_part_sheetmetal_verified'), bomRows, 'shelf row third_part_sheetmetal_verified')
check('interfaces_minimum_only', interfaces.status === 'minimum_interface_only_not_full_cabinet', interfaces.status, 'minimum_interface_only_not_full_cabinet')
check('interface_relation_count', asArray(interfaces.relations).length >= 4, asArray(interfaces.relations).length, '>= 4')
check('interface_includes_shelf_to_front_frame', asArray(interfaces.relations).some((item) => item.from === 'shelf_cross_panel_left_v43' && item.to === 'front_frame_vertical_left_v43'), asArray(interfaces.relations), 'shelf -> front frame relation')
check('interface_includes_shelf_to_door', asArray(interfaces.relations).some((item) => item.from === 'shelf_cross_panel_left_v43' && item.to === 'door_panel_2_12_right_v43'), asArray(interfaces.relations), 'shelf -> door relation')

check('default_gate_pass', gate.current_gate_evidence?.default_gate_status === 'PASS', gate.current_gate_evidence?.default_gate_status, 'PASS')
check('strict_gate_pass', gate.current_gate_evidence?.strict_gate_status === 'PASS', gate.current_gate_evidence?.strict_gate_status, 'PASS')
check('supplier_precheck_not_required_ready', releaseBoundary.supplier_precheck_status !== 'READY', releaseBoundary.supplier_precheck_status, 'not READY until engineering signoff')
check('supplier_signoff_boundary_documented', String(releaseBoundary.do_not_enter_supplier_release_until || '').includes('engineering_signoff.json'), releaseBoundary.do_not_enter_supplier_release_until, 'engineering signoff boundary')

for (const path of [
  door.evidence_files?.result_json,
  door.evidence_files?.flat_check_json,
  door.evidence_files?.engineering_confirmation_pack,
  second.evidence_files?.result_json,
  second.evidence_files?.flat_check_json,
  third.evidence_files?.result_json,
  third.evidence_files?.flat_export_json,
  third.evidence_files?.flat_check_json,
  gate.current_gate_evidence?.default_gate_report,
  gate.current_gate_evidence?.strict_gate_report,
].filter(Boolean)) {
  checkExists(`evidence_exists:${path}`, path, 'evidence')
}

const failed = checks.filter((item) => !item.ok)
const report = {
  schema: 'winnsen.locker16029.part_first_route_playbook_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length ? 'FAIL' : 'PASS',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  checks,
}

writeFileSync(abs(PATHS.out), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
console.log(`${report.status}: ${PATHS.out}`)
if (failed.length) {
  for (const item of failed) console.log(`- ${item.name}`)
  process.exit(1)
}
