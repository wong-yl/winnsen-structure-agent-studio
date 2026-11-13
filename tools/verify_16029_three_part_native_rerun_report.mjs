import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  report: 'data/locker_16029_three_part_native_rerun_report.json',
  out: 'data/locker_16029_three_part_native_rerun_gate.json',
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

const checks = []

function check(name, ok, actual, expected, category = 'native_rerun') {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

function checkExists(name, path, category = 'evidence') {
  check(name, existsSync(abs(path)), path, 'file exists', category)
}

function component(report, fragment) {
  return asArray(report.component_evidence?.components).find((item) => String(item.name || '').includes(fragment)) || {}
}

const report = readJson(PATHS.report)
const build = report.build_evidence || {}
const inspect = report.inspect_failure_resolved || {}
const componentEvidence = report.component_evidence || {}
const clearance = report.native_bbox_clearance || {}
const pairs = clearance.pairs || {}
const release = report.release_boundary || {}
const door = component(report, 'door')
const front = component(report, 'front_frame')
const shelf = component(report, 'shelf_cross')

check('schema', report.schema === 'winnsen.locker16029.three_part_native_rerun_report.v1', report.schema, 'winnsen.locker16029.three_part_native_rerun_report.v1')
check('status_complete_with_blocker', report.status === 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER', report.status, 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER')
check('route_ready', report.source_route?.route_status === 'READY_FOR_THREE_PART_INTERFACE_ITERATION', report.source_route?.route_status, 'READY_FOR_THREE_PART_INTERFACE_ITERATION')
check('route_gate_pass', report.source_route?.route_gate_status === 'PASS', report.source_route?.route_gate_status, 'PASS')
check('previous_interface_gate_pass', report.source_route?.previous_interface_gate_status === 'PASS', report.source_route?.previous_interface_gate_status, 'PASS')

check('previous_inspect_failure_recorded', inspect.previous_opened === false && asNumber(inspect.previous_component_count) === 0, inspect, 'previous opened=false/component_count=0')
check('rerun_inspect_resolved', inspect.conclusion === 'RESOLVED_CURRENT_FORMAL_SLDASM_OPENS_REBUILDS_AND_ENUMERATES_THREE_COMPONENTS', inspect.conclusion, 'resolved')
check('build_completed', build.build_status === 'completed', build.build_status, 'completed')
check('build_saved', build.saved === true && build.step_saved === true, build, 'assembly and STEP saved')
check('build_counts', asNumber(build.placement_count) === 3 && asNumber(build.reference_count) === 3 && asNumber(build.components_added) === 3, build, '3 placements/references/components')

check('components_opened_rebuilt', componentEvidence.opened === true && componentEvidence.rebuilt === true, componentEvidence, 'opened/rebuilt true')
check('component_count_3', asNumber(componentEvidence.component_count) === 3, componentEvidence.component_count, 3)
check('enumeration_mode_recursive', componentEvidence.enumeration_mode === 'root_children_recursive', componentEvidence.enumeration_mode, 'root_children_recursive')
check('door_component_bbox', asNumber(door.bbox_mm?.xlen_mm).toFixed(1) === '307.0' && asNumber(door.bbox_mm?.ylen_mm).toFixed(1) === '298.0', door.bbox_mm, 'door bbox 307 x 298')
check('front_component_bbox', asNumber(front.bbox_mm?.xlen_mm).toFixed(1) === '49.3' && asNumber(front.bbox_mm?.ylen_mm).toFixed(0) === '1834', front.bbox_mm, 'front bbox 49.3 x 1834')
check('shelf_component_bbox_present', asNumber(shelf.bbox_mm?.xlen_mm) > 0 && asNumber(shelf.bbox_mm?.zlen_mm) > 0, shelf.bbox_mm, 'shelf native bbox present')

check('clearance_status_blocked', clearance.status === 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_HAS_AABB_OVERLAPS', clearance.status, 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_HAS_AABB_OVERLAPS')
check('clearance_counts', asNumber(clearance.pair_count) === 3 && asNumber(clearance.separated_pair_count) === 2 && asNumber(clearance.overlapping_pair_count) === 1, clearance, '3 pairs, 2 separated, 1 overlap')
check('door_front_overlap_recorded', pairs.door_to_front_frame?.aabb_intersects === true, pairs.door_to_front_frame, 'AABB overlap')
check('door_front_requires_exact_check', String(pairs.door_to_front_frame?.clearance_conclusion || '').includes('EXACT_SOLID_INTERFERENCE_CHECK'), pairs.door_to_front_frame?.clearance_conclusion, 'exact check required')
check('shelf_door_separated', pairs.shelf_to_door?.aabb_intersects === false && asNumber(pairs.shelf_to_door?.minimum_separating_gap_mm) > 0, pairs.shelf_to_door, 'separated')
check('shelf_front_separated', pairs.shelf_to_front_frame?.aabb_intersects === false && asNumber(pairs.shelf_to_front_frame?.minimum_separating_gap_mm) > 0, pairs.shelf_to_front_frame, 'separated')
check('blocker_summary_present', asArray(clearance.blocking_reasons).some((item) => String(item.id || '').includes('door_panel_2_12_right_v43_vs_front_frame_vertical_left_v43')), clearance.blocking_reasons, 'door-front blocker')

check('pilot_bom_only', release.bom_status === 'pilot_only_not_released', release.bom_status, 'pilot_only_not_released')
check('supplier_release_excluded', release.supplier_release_status === 'supplier_release_excluded', release.supplier_release_status, 'supplier_release_excluded')
check('cannot_issue_supplier_dxf', release.can_issue_supplier_dxf_now === false, release.can_issue_supplier_dxf_now, false)
check('cannot_expand_full_cabinet', release.can_expand_to_full_cabinet_now === false, release.can_expand_to_full_cabinet_now, false)

for (const path of [
  report.source_route?.route_playbook,
  report.source_route?.route_gate,
  report.source_route?.previous_interface_gate,
  inspect.previous_inspect_json,
  inspect.rerun_components_json,
  build.build_json,
  build.assembly_path,
  build.step_path,
  clearance.clearance_json,
].filter(Boolean)) {
  checkExists(`evidence_exists:${path}`, path)
}

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.three_part_native_rerun_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length ? 'FAIL' : 'PASS',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  engineering_clearance_status: 'BLOCKED_BY_DOOR_FRONT_AABB_REQUIRES_EXACT_INTERFERENCE_OR_GAP_REVIEW',
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
