import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  report: 'data/locker_16029_door_front_gap_review.json',
  out: 'data/locker_16029_door_front_gap_review_gate.json',
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path) {
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function almostEqual(a, b, tolerance = 0.001) {
  return Math.abs(Number(a) - Number(b)) <= tolerance
}

const checks = []

function check(name, ok, actual, expected, category = 'door_front_gap_review') {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

function checkExists(name, path, category = 'evidence') {
  check(name, existsSync(abs(path)), path, 'file exists', category)
}

const report = readJson(PATHS.report)
const bbox = report.current_native_bbox || {}
const fixedDoor = report.transform_candidates?.preferred_fixed_door_adjust_front_frame || {}
const fixedFront = report.transform_candidates?.alternate_fixed_front_frame_adjust_door || {}
const next = report.required_next_check || {}
const release = report.release_boundary || {}

check('schema', report.schema === 'winnsen.locker16029.door_front_gap_review.v1', report.schema, 'winnsen.locker16029.door_front_gap_review.v1')
check('status_blocker_localized', report.status === 'BLOCKER_LOCALIZED_GAP_REVIEW_READY_NOT_CAD_CHANGED', report.status, 'BLOCKER_LOCALIZED_GAP_REVIEW_READY_NOT_CAD_CHANGED')
check('pair_scope', report.scope?.pair === 'door_panel_2_12_right_v43 vs front_frame_vertical_left_v43', report.scope?.pair, 'door/front pair')
check('native_rerun_source_complete', report.source_evidence?.native_rerun_status === 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER', report.source_evidence?.native_rerun_status, 'native rerun complete with blocker')
check('manifest_source_managed', report.source_evidence?.manifest_status === 'MANAGED_READY_FOR_NEXT_ENGINEERING_GOAL', report.source_evidence?.manifest_status, 'managed manifest')

check('target_gap_7mm', almostEqual(bbox.target_gap_mm, 7, 0.001), bbox.target_gap_mm, 7)
check('door_xmin', almostEqual(bbox.door?.bbox_mm?.xmin_mm, -153.5, 0.001), bbox.door?.bbox_mm?.xmin_mm, -153.5)
check('front_xmax', almostEqual(bbox.front_frame?.bbox_mm?.xmax_mm, -143.35, 0.001), bbox.front_frame?.bbox_mm?.xmax_mm, -143.35)
check('current_negative_gap', almostEqual(bbox.measured_x_gap_mm, -10.15, 0.001), bbox.measured_x_gap_mm, -10.15)
check('current_overlap', almostEqual(bbox.measured_x_overlap_mm, 10.15, 0.001), bbox.measured_x_overlap_mm, 10.15)
check('y_z_overlap_recorded', asNumber(bbox.y_overlap_mm) > 0 && asNumber(bbox.z_overlap_mm) > 0, { y: bbox.y_overlap_mm, z: bbox.z_overlap_mm }, 'positive Y/Z AABB overlap')

check('front_contact_candidate', almostEqual(fixedDoor.front_tx_for_0mm_contact_mm, -178.15, 0.001), fixedDoor, 'front tx -178.15 for contact')
check('front_target_gap_candidate', almostEqual(fixedDoor.front_tx_for_target_gap_mm, -185.15, 0.001), fixedDoor, 'front tx -185.15 for 7mm gap')
check('front_target_shift', almostEqual(fixedDoor.front_shift_for_target_gap_mm, -17.15, 0.001), fixedDoor.front_shift_for_target_gap_mm, '-17.15 mm')
check('door_contact_candidate', almostEqual(fixedFront.door_tx_for_0mm_contact_mm, 10.15, 0.001), fixedFront, 'door tx 10.15 for contact')
check('door_target_gap_candidate', almostEqual(fixedFront.door_tx_for_target_gap_mm, 17.15, 0.001), fixedFront, 'door tx 17.15 for 7mm gap')

check('historical_reference_positive_gap', asNumber(report.historical_reference?.historical_minimum_gap_mm) > 0, report.historical_reference, 'positive reference gap')
check('next_check_required', next.status === 'EXACT_INTERFERENCE_OR_ENGINEERING_GAP_REVIEW_REQUIRED', next.status, 'exact interference/gap required')
check('next_check_has_candidate_tx', String(next.recommended_order || '').includes('-185.15'), next.recommended_order, 'candidate tx included')

check('cad_not_modified', release.cad_modified_by_this_report === false, release.cad_modified_by_this_report, false)
check('supplier_release_excluded', release.supplier_release_status === 'supplier_release_excluded', release.supplier_release_status, 'supplier_release_excluded')
check('supplier_dxf_false', release.can_issue_supplier_dxf_now === false, release.can_issue_supplier_dxf_now, false)
check('full_cabinet_false', release.can_expand_full_cabinet_now === false, release.can_expand_full_cabinet_now, false)

for (const path of [
  report.source_evidence?.route_playbook,
  report.source_evidence?.native_rerun_report,
  report.source_evidence?.delivery_manifest,
  report.source_evidence?.historical_envelope_clearance,
  PATHS.report,
  'docs/16029_door_front_gap_review.md',
].filter(Boolean)) {
  checkExists(`evidence_exists:${path}`, path)
}

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.door_front_gap_review_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length ? 'FAIL' : 'PASS',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  engineering_status: 'GAP_REVIEW_READY_EXACT_INTERFERENCE_STILL_REQUIRED',
  checks,
}

writeFileSync(abs(PATHS.out), `${JSON.stringify(gate, null, 2)}\n`, 'utf8')
console.log(`${gate.status}: ${PATHS.out}`)
if (failed.length) {
  for (const item of failed) console.log(`- ${item.name}`)
  process.exit(1)
}
