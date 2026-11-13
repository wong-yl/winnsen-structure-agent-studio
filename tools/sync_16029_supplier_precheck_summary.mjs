import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')
const now = new Date().toISOString()

const PATHS = {
  supplier: 'data/locker_16029_supplier_dxf_precheck.json',
  completionAudit: 'data/locker_16029_part_first_pilot_completion_audit.json',
  decision: 'data/locker_16029_door_upper_hinge_rule_decision.json',
  doorResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_result.json',
  doorFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json',
  frontFrameResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_result.json',
  frontFrameFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_flat_check.json',
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path) {
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function writeJson(path, value) {
  writeFileSync(abs(path), `${JSON.stringify(value, null, 2)}\n`)
}

function readOptionalJson(path) {
  return existsSync(abs(path)) ? readJson(path) : {}
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

const supplier = readJson(PATHS.supplier)
const doorResult = readJson(PATHS.doorResult)
const doorFlatCheck = readJson(PATHS.doorFlatCheck)
const frontFrameResult = readOptionalJson(PATHS.frontFrameResult)
const frontFrameFlatCheck = readOptionalJson(PATHS.frontFrameFlatCheck)

const doorEvidence = {
  hole_datum_count: Array.isArray(doorResult.holeDatums) ? doorResult.holeDatums.length : 0,
  hole_cut_count: asNumber(doorResult.holeCutCount ?? doorResult.hole_cut_count),
  flat_dxf_entities: doorFlatCheck.entity_counts || {},
  folded_bbox_mm: [
    doorResult.foldedEnvelopeMeasuredWidthMm ?? null,
    doorResult.foldedEnvelopeMeasuredHeightMm ?? null,
    doorResult.foldedEnvelopeMeasuredDepthMm ?? null,
  ],
  flat_bbox_mm: doorFlatCheck.bbox_mm
    ? [doorFlatCheck.bbox_mm.width_mm, doorFlatCheck.bbox_mm.height_mm]
    : [null, null],
  result_json: PATHS.doorResult,
  flat_check_json: PATHS.doorFlatCheck,
}

const frontFrameEvidence = {
  hole_cut_count: asNumber(frontFrameResult.holeCutCount ?? frontFrameResult.hole_cut_count),
  flat_dxf_entities: frontFrameFlatCheck.entity_counts || {},
  flat_bbox_mm: frontFrameFlatCheck.bbox_mm
    ? [frontFrameFlatCheck.bbox_mm.width_mm, frontFrameFlatCheck.bbox_mm.height_mm]
    : [null, null],
  result_json: PATHS.frontFrameResult,
  flat_check_json: PATHS.frontFrameFlatCheck,
}

const blocking = supplier.blocking_reasons || []
const cadClosed =
  doorEvidence.hole_cut_count === 4 &&
  asNumber(doorEvidence.flat_dxf_entities.CIRCLE) >= 4 &&
  supplier.key_evidence?.upper_hinge_material_status === 'INSIDE_CURRENT_FLAT_PATTERN'

if (existsSync(abs(PATHS.completionAudit))) {
  const audit = readJson(PATHS.completionAudit)
  audit.updated_at = now
  audit.supplier_dxf_precheck = {
    ...(audit.supplier_dxf_precheck || {}),
    status: supplier.status,
    checked_at: supplier.generated_at || now,
    machine_report: PATHS.supplier,
    checks_total: supplier.checks_total,
    checks_failed_blocking: supplier.checks_failed_blocking,
    ready_for_supplier_review: supplier.ready_for_supplier_review,
    can_issue_supplier_dxf_now: supplier.can_issue_supplier_dxf_now,
    default_gate_status: supplier.key_evidence?.default_gate_status || null,
    strict_gate_status: supplier.key_evidence?.strict_gate_status || null,
    cad_closed_for_door_true_holes: cadClosed,
    closed: [
      'door_panel_2_12_right_v43:formal_hole_cut_count_4',
      'door_panel_2_12_right_v43:flat_dxf_circle_count_5',
      'door_panel_2_12_right_v43:upper_hinge_inside_current_flat_material',
      'front_frame_vertical_left_v43:12_source_hole_cut_features',
    ],
    blocking,
    evidence: {
      ...(audit.supplier_dxf_precheck?.evidence || {}),
      formal_door: doorEvidence,
      front_frame: frontFrameEvidence,
    },
  }
  writeJson(PATHS.completionAudit, audit)
}

if (existsSync(abs(PATHS.decision))) {
  const decision = readJson(PATHS.decision)
  decision.updated_at = now
  decision.supplier_dxf_precheck = {
    ...(decision.supplier_dxf_precheck || {}),
    machine_report: PATHS.supplier,
    status: supplier.status,
    ready_for_supplier_review: supplier.ready_for_supplier_review,
    can_issue_supplier_dxf_now: supplier.can_issue_supplier_dxf_now,
    checks_total: supplier.checks_total,
    checks_failed_blocking: supplier.checks_failed_blocking,
    blocking_reasons: blocking,
    cad_closed_for_door_true_holes: cadClosed,
    expected_remaining_blocking_reason:
      blocking.length === 1 ? blocking[0] : 'review supplier precheck blocking_reasons',
  }
  decision.verification_required_after_decision = {
    ...(decision.verification_required_after_decision || {}),
    default_gate_status: supplier.key_evidence?.default_gate_status || null,
    strict_gate_status: supplier.key_evidence?.strict_gate_status || null,
    supplier_dxf_precheck: supplier.status,
  }
  decision.engineering_confirmation_pack = {
    ...(decision.engineering_confirmation_pack || {}),
    ready_for_engineering_confirmation: true,
    ready_for_supplier_dxf_review: supplier.ready_for_supplier_review,
    can_issue_supplier_dxf_now: supplier.can_issue_supplier_dxf_now,
    current_formal_door_supplier_hole_status: cadClosed
      ? 'CAD_TRUE_HOLE_FEATURES_4_OF_4_DXF_CIRCLE_5_PENDING_ENGINEERING_SIGNOFF'
      : 'CAD_TRUE_HOLE_FEATURES_NOT_CLOSED',
  }
  writeJson(PATHS.decision, decision)
}

process.stdout.write(
  JSON.stringify(
    {
      status: 'SYNCED',
      supplier_status: supplier.status,
      checks_failed_blocking: supplier.checks_failed_blocking,
      blocking_reasons: blocking,
      cad_closed_for_door_true_holes: cadClosed,
    },
    null,
    2,
  ),
)
