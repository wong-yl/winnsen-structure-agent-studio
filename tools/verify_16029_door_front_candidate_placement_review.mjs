import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const REVIEW_PATH = 'data/locker_16029_door_front_candidate_placement_review.json'
const GATE_PATH = 'data/locker_16029_door_front_candidate_placement_gate.json'

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path) {
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function round(value) {
  return Number.isFinite(Number(value)) ? Math.round(Number(value) * 1000) / 1000 : null
}

function almostEqual(actual, expected, tolerance = 0.001) {
  return Math.abs(Number(actual) - expected) <= tolerance
}

const review = readJson(REVIEW_PATH)
const checks = []

function check(name, ok, actual, expected, category = 'candidate_placement') {
  checks.push({ name, ok: ok === true, actual, expected, category })
}

const delta = review.placement_delta || {}
const expected = review.expected_bbox_screen || {}
const pair = expected.door_to_front_frame || {}
const axes = pair.axes || {}
const release = review.release_boundary || {}
const files = review.candidate_files || {}
const sw = review.sw2020_candidate_rerun || {}

check('schema', review.schema === 'winnsen.locker16029.door_front_candidate_placement_review.v1', review.schema, 'candidate placement schema')
check('status_valid', [
  'CANDIDATE_PLACEMENT_READY_FOR_SW2020_NATIVE_RERUN',
  'CANDIDATE_SW2020_NATIVE_AABB_SEPARATED_ENGINEERING_GAP_REVIEW_READY',
].includes(review.status), review.status, 'known candidate status')
check('source_gap_review_ready', review.source_evidence?.gap_review_status === 'BLOCKER_LOCALIZED_GAP_REVIEW_READY_NOT_CAD_CHANGED', review.source_evidence?.gap_review_status, 'gap review ready')
check('native_rerun_source_blocker', review.source_evidence?.native_rerun_status === 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER', review.source_evidence?.native_rerun_status, 'native blocker source')
check('manifest_managed', review.source_evidence?.manifest_status === 'MANAGED_READY_FOR_NEXT_ENGINEERING_GOAL', review.source_evidence?.manifest_status, 'manifest managed')
check('fixed_door', delta.fixed_datum === 'door_panel_2_12_right_v43', delta.fixed_datum, 'door fixed')
check('adjusted_front_frame', delta.adjusted_component === 'front_frame_vertical_left_v43', delta.adjusted_component, 'front frame adjusted')
check('current_front_tx', almostEqual(delta.current_front_tx_mm, -168), delta.current_front_tx_mm, -168)
check('candidate_front_tx', almostEqual(delta.candidate_front_tx_mm, -185.15), delta.candidate_front_tx_mm, -185.15)
check('front_shift', almostEqual(delta.front_shift_mm, -17.15), delta.front_shift_mm, -17.15)
check('target_gap', almostEqual(delta.target_x_gap_mm, 7), delta.target_x_gap_mm, 7)
check('expected_aabb_separated', pair.aabb_intersects === false, pair.aabb_intersects, false)
check('expected_separating_axis_x', Array.isArray(pair.separating_axes) && pair.separating_axes.includes('x'), pair.separating_axes, 'x')
check('expected_x_gap_7', almostEqual(axes.x?.gap_mm, 7), axes.x?.gap_mm, 7)
check('expected_min_gap_7', almostEqual(pair.minimum_separating_gap_mm, 7), pair.minimum_separating_gap_mm, 7)
check('expected_front_xmax', almostEqual(expected.shifted_front_frame_bbox_mm?.xmax_mm, -160.5), expected.shifted_front_frame_bbox_mm?.xmax_mm, -160.5)
check('candidate_tsv_exists', existsSync(abs(files.candidate_placements_tsv || '')), files.candidate_placements_tsv, 'file exists', 'evidence')

if (sw.rerun_complete === true) {
  check('sw_build_completed', sw.build_status === 'completed', sw.build_status, 'completed', 'sw2020_rerun')
  check('sw_opened_rebuilt_three', sw.opened === true && sw.rebuilt === true && Number(sw.component_count) === 3, sw, 'opened/rebuilt/3', 'sw2020_rerun')
  check('sw_clearance_separated', sw.clearance_status === 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED', sw.clearance_status, 'AABB separated', 'sw2020_rerun')
  check('sw_door_front_separated', sw.door_front_pair?.aabb_intersects === false && almostEqual(sw.door_front_pair?.axes?.x?.gap_mm, 7), sw.door_front_pair, 'door-front x gap 7', 'sw2020_rerun')
} else {
  check('sw_rerun_not_overclaimed', review.status === 'CANDIDATE_PLACEMENT_READY_FOR_SW2020_NATIVE_RERUN', { status: review.status, sw }, 'ready, not complete', 'sw2020_rerun')
}

check('exact_gap_status_not_release', String(review.exact_or_engineering_gap_review?.status || '').includes('REVIEW'), review.exact_or_engineering_gap_review?.status, 'review still required')
check('cad_not_modified', release.cad_modified_by_this_report === false, release.cad_modified_by_this_report, false, 'release_boundary')
check('formal_placement_not_overwritten', release.formal_placement_overwritten === false, release.formal_placement_overwritten, false, 'release_boundary')
check('supplier_release_excluded', release.supplier_release_status === 'supplier_release_excluded', release.supplier_release_status, 'supplier_release_excluded', 'release_boundary')
check('supplier_dxf_false', release.can_issue_supplier_dxf_now === false, release.can_issue_supplier_dxf_now, false, 'release_boundary')
check('full_cabinet_false', release.can_expand_full_cabinet_now === false, release.can_expand_full_cabinet_now, false, 'release_boundary')

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.door_front_candidate_placement_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length === 0 ? 'PASS' : 'FAIL',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => ({
    id: item.name,
    category: item.category,
    actual: item.actual,
    expected: item.expected,
  })),
  engineering_status: sw.rerun_complete === true
    ? 'CANDIDATE_SW2020_AABB_CLEAR_EXACT_GAP_DATUM_STILL_REQUIRED'
    : 'CANDIDATE_READY_FOR_SW2020_NATIVE_RERUN',
  candidate_front_tx_mm: round(delta.candidate_front_tx_mm),
  expected_x_gap_mm: round(axes.x?.gap_mm),
  checks,
}

writeFileSync(abs(GATE_PATH), `${JSON.stringify(gate, null, 2)}\n`, 'utf8')
console.log(`${gate.status}: ${GATE_PATH}`)
if (gate.status !== 'PASS') process.exitCode = 1
