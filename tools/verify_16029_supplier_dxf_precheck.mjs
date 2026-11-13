import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  defaultGate: 'data/locker_16029_part_first_pilot_gate_report.json',
  strictGate: 'data/locker_16029_part_first_pilot_gate_report.strict.json',
  decision: 'data/locker_16029_door_upper_hinge_rule_decision.json',
  engineeringSignoff: 'data/locker_16029_door_upper_hinge_engineering_signoff.json',
  engineeringSignoffTemplate: 'data/locker_16029_door_upper_hinge_engineering_signoff.template.json',
  completionAudit: 'data/locker_16029_part_first_pilot_completion_audit.json',
  doorResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_result.json',
  doorFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json',
  doorTargetSpec:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json',
  frontFrameResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_result.json',
  frontFrameFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_flat_check.json',
}

const OPTIONAL_INPUTS = new Set(['engineeringSignoff'])

function parseArgs(argv) {
  const options = {
    out: 'data/locker_16029_supplier_dxf_precheck.json',
    failOnBlocked: false,
  }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--out') options.out = argv[++index] || options.out
    else if (arg === '--fail-on-blocked') options.failOnBlocked = true
  }
  return options
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path) {
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function tryReadJson(path) {
  if (!existsSync(abs(path))) return { ok: false, value: null, error: 'missing file' }
  try {
    return { ok: true, value: readJson(path), error: '' }
  } catch (error) {
    return { ok: false, value: null, error: String(error) }
  }
}

function fileInfo(path) {
  const full = abs(path)
  if (!existsSync(full)) return { path, exists: false, size: 0 }
  const stat = statSync(full)
  return { path, exists: true, size: stat.size }
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function nonEmpty(value) {
  return typeof value === 'string' && value.trim().length > 0
}

function almostEqual(a, b, tolerance = 0.001) {
  return Math.abs(Number(a) - Number(b)) <= tolerance
}

function featureTypes(json) {
  return new Set(asArray(json?.features).map((feature) => String(feature.type || '')))
}

function hasFeature(json, type) {
  return featureTypes(json).has(type)
}

function holeCutCount(json) {
  if (Object.hasOwn(json || {}, 'holeCutCount')) return asNumber(json.holeCutCount)
  if (Object.hasOwn(json || {}, 'hole_cut_count')) return asNumber(json.hole_cut_count)
  return 0
}

function holeDatumCount(doorResult, targetSpec) {
  const resultDatums = asArray(doorResult?.holeDatums)
  if (resultDatums.length > 0) return resultDatums.length
  return asArray(targetSpec?.target_hole_datums).length
}

const checks = []

function check(name, ok, actual, expected, options = {}) {
  checks.push({
    name,
    ok: Boolean(ok),
    actual,
    expected,
    category: options.category || 'supplier_dxf_precheck',
    required: options.required !== false,
    blocking_reason: options.blockingReason || '',
  })
}

function checkJson(label, path, loaded) {
  check(`${label}:json_exists_and_parses`, loaded.ok, loaded.ok ? fileInfo(path) : { path, error: loaded.error }, 'valid JSON file', {
    category: 'inputs',
    blockingReason: `${label}:missing_or_invalid_json`,
  })
}

function buildReport() {
  const loaded = Object.fromEntries(Object.entries(PATHS).map(([key, path]) => [key, tryReadJson(path)]))
  for (const [key, path] of Object.entries(PATHS)) {
    if (!OPTIONAL_INPUTS.has(key)) checkJson(key, path, loaded[key])
  }

  const defaultGate = loaded.defaultGate.value || {}
  const strictGate = loaded.strictGate.value || {}
  const decision = loaded.decision.value || {}
  const engineeringSignoff = loaded.engineeringSignoff.value || {}
  const completionAudit = loaded.completionAudit.value || {}
  const doorResult = loaded.doorResult.value || {}
  const doorFlatCheck = loaded.doorFlatCheck.value || {}
  const doorTargetSpec = loaded.doorTargetSpec.value || {}
  const frontFrameResult = loaded.frontFrameResult.value || {}
  const frontFrameFlatCheck = loaded.frontFrameFlatCheck.value || {}

  check('pilot_default_gate_status', defaultGate.status === 'PASS', defaultGate.status, 'PASS', {
    category: 'existing_gate',
    blockingReason: 'pilot_default_gate_not_pass',
  })
  check(
    'pilot_default_gate_blocking_failures',
    asNumber(defaultGate.checks_failed_blocking) === 0 && asNumber(defaultGate.pilot_checks_failed) === 0,
    {
      checks_failed_blocking: defaultGate.checks_failed_blocking,
      pilot_checks_failed: defaultGate.pilot_checks_failed,
    },
    '0 blocking failures',
    { category: 'existing_gate', blockingReason: 'pilot_default_gate_has_blocking_failures' },
  )
  check('pilot_strict_gate_status', strictGate.status === 'PASS', strictGate.status, 'PASS', {
    category: 'existing_gate',
    blockingReason: 'pilot_strict_gate_not_pass',
  })
  check(
    'pilot_strict_gate_blocking_failures',
    asNumber(strictGate.checks_failed_blocking) === 0 && asNumber(strictGate.missing_pilot_artifacts) === 0,
    {
      checks_failed_blocking: strictGate.checks_failed_blocking,
      missing_pilot_artifacts: strictGate.missing_pilot_artifacts,
    },
    '0 strict blocking failures',
    { category: 'existing_gate', blockingReason: 'pilot_strict_gate_has_blocking_failures' },
  )

  check(
    'door_formal_result_status',
    doorResult.status === 'SMOKE_EDGE_FLANGE_SHEETMETAL_FLAT_AND_FOLDED_ENVELOPE_EXPORTED',
    doorResult.status,
    'SMOKE_EDGE_FLANGE_SHEETMETAL_FLAT_AND_FOLDED_ENVELOPE_EXPORTED',
    { category: 'door_panel', blockingReason: 'door_formal_result_not_current_release_candidate' },
  )
  check('door_saved_native_part', doorResult.savedPart === true || doorResult.partSaved === true, { savedPart: doorResult.savedPart, partSaved: doorResult.partSaved }, true, {
    category: 'door_panel',
    blockingReason: 'door_native_part_not_saved',
  })
  check('door_saved_step', doorResult.savedStep === true || doorResult.stepSaved === true, { savedStep: doorResult.savedStep, stepSaved: doorResult.stepSaved }, true, {
    category: 'door_panel',
    blockingReason: 'door_step_not_saved',
  })
  check('door_has_sheetmetal_feature', hasFeature(doorResult, 'SheetMetal'), [...featureTypes(doorResult)], 'SheetMetal', {
    category: 'door_panel',
    blockingReason: 'door_sheetmetal_feature_missing',
  })
  check('door_has_flatpattern_feature', hasFeature(doorResult, 'FlatPattern'), [...featureTypes(doorResult)], 'FlatPattern', {
    category: 'door_panel',
    blockingReason: 'door_flatpattern_feature_missing',
  })
  check('door_flat_dxf_exported', doorResult.flatDxfExported === true, doorResult.flatDxfExported, true, {
    category: 'door_panel',
    blockingReason: 'door_flat_dxf_not_exported',
  })
  check(
    'door_folded_bbox_mm',
    almostEqual(doorResult.foldedEnvelopeMeasuredWidthMm, 307) &&
      almostEqual(doorResult.foldedEnvelopeMeasuredHeightMm, 298) &&
      almostEqual(doorResult.foldedEnvelopeMeasuredDepthMm, 18.2),
    [
      doorResult.foldedEnvelopeMeasuredWidthMm,
      doorResult.foldedEnvelopeMeasuredHeightMm,
      doorResult.foldedEnvelopeMeasuredDepthMm,
    ],
    [307, 298, 18.2],
    { category: 'door_panel', blockingReason: 'door_folded_envelope_regressed' },
  )
  check('door_flat_check_status', doorFlatCheck.status === 'PASS', doorFlatCheck.status, 'PASS', {
    category: 'door_panel_flat_dxf',
    blockingReason: 'door_flat_check_not_pass',
  })
  check(
    'door_flat_bbox_mm',
    doorFlatCheck.status === 'PASS' &&
      almostEqual(doorFlatCheck.bbox_mm?.width_mm, 343.4, asNumber(doorFlatCheck.tolerance_mm) || 0.25) &&
      almostEqual(doorFlatCheck.bbox_mm?.height_mm, 334.4, asNumber(doorFlatCheck.tolerance_mm) || 0.25),
    doorFlatCheck.bbox_mm,
    { width_mm: 343.4, height_mm: 334.4 },
    { category: 'door_panel_flat_dxf', blockingReason: 'door_flat_bbox_not_343p4_x_334p4' },
  )
  check(
    'door_target_hole_datums',
    holeDatumCount(doorResult, doorTargetSpec) === 4 && asArray(doorTargetSpec.target_hole_datums).length === 4,
    {
      result_hole_datums: asArray(doorResult.holeDatums).length,
      target_spec_hole_datums: asArray(doorTargetSpec.target_hole_datums).length,
    },
    4,
    { category: 'door_panel_holes', blockingReason: 'door_target_hole_datums_not_four' },
  )

  const doorCuts = holeCutCount(doorResult)
  const doorCircleCount = asNumber(doorFlatCheck.entity_counts?.CIRCLE)
  check('door_physical_hole_cut_count', doorCuts === 4, doorCuts, 4, {
    category: 'door_panel_holes',
    blockingReason: `door_panel_2_12_right_v43:formal_hole_cut_count_${doorCuts}_expected_4`,
  })
  check('door_flat_dxf_circle_count', doorCircleCount >= 4, doorFlatCheck.entity_counts || {}, 'at least 4 CIRCLE entities', {
    category: 'door_panel_holes',
    blockingReason: `door_panel_2_12_right_v43:flat_dxf_circle_count_${doorCircleCount}_expected_at_least_4`,
  })

  const upperHinge = asArray(decision.holes).find((hole) => hole.role === 'hinge_upper')
  check(
    'door_upper_hinge_center_inside_current_flat_material',
    upperHinge?.material_status === 'INSIDE_CURRENT_FLAT_PATTERN',
    upperHinge || null,
    'INSIDE_CURRENT_FLAT_PATTERN',
    { category: 'door_panel_holes', blockingReason: 'door_panel_2_12_right_v43:upper_hinge_10p2_maps_outside_current_flat_material' },
  )

  const decisionStatus = String(decision.status || '')
  const bEngineeringSignoffAccepted =
    loaded.engineeringSignoff.ok === true &&
    engineeringSignoff.schema === 'winnsen.locker16029.door_upper_hinge_engineering_signoff.v1' &&
    engineeringSignoff.accepted === true &&
    engineeringSignoff.selected_option === 'B_ADJUST_TOP_LEFT_CORNER_RELIEF' &&
    nonEmpty(engineeringSignoff.signer?.structure_engineer) &&
    nonEmpty(engineeringSignoff.signer?.signed_at)
  const cProcessFallbackAccepted =
    loaded.engineeringSignoff.ok === true &&
    engineeringSignoff.schema === 'winnsen.locker16029.door_upper_hinge_engineering_signoff.v1' &&
    engineeringSignoff.accepted === true &&
    engineeringSignoff.selected_option === 'C_POST_PROCESS_UPPER_HINGE_HOLE' &&
    (nonEmpty(engineeringSignoff.signer?.structure_engineer) || nonEmpty(engineeringSignoff.signer?.process_engineer)) &&
    nonEmpty(engineeringSignoff.signer?.signed_at)
  check(
    'door_upper_hinge_rule_direction_documented',
    decisionStatus.startsWith('B_CORNER_RELIEF_RULE_SUPPORTED'),
    decision.status,
    'B_CORNER_RELIEF_RULE_SUPPORTED*',
    { category: 'rule_decision', blockingReason: 'door_upper_hinge_rule_direction_not_documented' },
  )
  check(
    'door_upper_hinge_rule_closed_for_supplier',
    !/NOT_CLOSED|BLOCKED|STILL_OPEN/i.test(decisionStatus),
    decision.status,
    'decision status without NOT_CLOSED/BLOCKED/STILL_OPEN',
    { category: 'rule_decision', blockingReason: 'door_panel_2_12_right_v43:upper_hinge_rule_not_closed_for_supplier' },
  )
  check(
    'door_upper_hinge_b_engineering_signoff_for_true_cut_rule',
    bEngineeringSignoffAccepted,
    {
      signoff_path: PATHS.engineeringSignoff,
      signoff_exists_and_parses: loaded.engineeringSignoff.ok,
      selected_option: engineeringSignoff.selected_option || '',
      accepted: engineeringSignoff.accepted === true,
      structure_engineer: engineeringSignoff.signer?.structure_engineer || '',
      signed_at: engineeringSignoff.signer?.signed_at || '',
      c_process_fallback_accepted: cProcessFallbackAccepted,
    },
    'accepted B_ADJUST_TOP_LEFT_CORNER_RELIEF signoff with structure engineer and signed_at',
    {
      category: 'rule_decision',
      blockingReason: 'door_panel_2_12_right_v43:b_engineering_signoff_not_accepted_for_true_cut_precheck',
    },
  )

  const processSignoff =
    cProcessFallbackAccepted ||
    decision.supplier_process_signoff?.accepted === true ||
    decision.process_signoff?.accepted === true ||
    completionAudit.supplier_dxf_precheck?.process_signoff?.accepted === true
  check('door_true_cuts_or_process_signoff', doorCuts === 4 || processSignoff, { door_hole_cut_count: doorCuts, process_signoff: processSignoff }, '4 true cuts or explicit process signoff', {
    category: 'supplier_release',
    blockingReason: 'door_panel_2_12_right_v43:manual_macro_or_process_signoff_required',
  })

  check('front_frame_hole_cut_count', holeCutCount(frontFrameResult) === 12, holeCutCount(frontFrameResult), 12, {
    category: 'front_frame',
    blockingReason: 'front_frame_vertical_left_v43:hole_cut_count_not_12',
  })
  check('front_frame_flat_dxf_arcs', asNumber(frontFrameFlatCheck.entity_counts?.ARC) === 12, frontFrameFlatCheck.entity_counts || {}, '12 ARC entities', {
    category: 'front_frame',
    blockingReason: 'front_frame_vertical_left_v43:flat_dxf_arc_count_not_12',
  })

  const failedRequired = checks.filter((item) => item.required && !item.ok)
  const blockingReasons = [...new Set(failedRequired.map((item) => item.blocking_reason || item.name))]
  const status = blockingReasons.length === 0 ? 'READY' : 'BLOCKED'

  return {
    schema: 'winnsen.locker16029.supplier_dxf_precheck.v1',
    generated_at: new Date().toISOString(),
    status,
    ready_for_supplier_review: status === 'READY',
    can_issue_supplier_dxf_now: status === 'READY',
    scope: {
      product: '16029 locker',
      route: 'part-first scattered sheet-metal pilot',
      checked_parts: ['door_panel_2_12_right_v43', 'front_frame_vertical_left_v43'],
      boundary:
        'Precheck only verifies release evidence already generated by the pilot. It does not repair SolidWorks geometry or override structure/process signoff.',
    },
    source_paths: PATHS,
    key_evidence: {
      default_gate_status: defaultGate.status || null,
      strict_gate_status: strictGate.status || null,
      door_formal_status: doorResult.status || null,
      door_folded_bbox_mm: [
        doorResult.foldedEnvelopeMeasuredWidthMm ?? null,
        doorResult.foldedEnvelopeMeasuredHeightMm ?? null,
        doorResult.foldedEnvelopeMeasuredDepthMm ?? null,
      ],
      door_flat_bbox_mm: doorFlatCheck.bbox_mm || null,
      door_hole_datum_count: holeDatumCount(doorResult, doorTargetSpec),
      door_hole_cut_count: doorCuts,
      door_flat_dxf_entity_counts: doorFlatCheck.entity_counts || null,
      upper_hinge_material_status: upperHinge?.material_status || null,
      upper_hinge_mapped_center_mm: upperHinge?.mapped_center_mm || null,
      b_engineering_signoff_accepted: bEngineeringSignoffAccepted,
      c_process_fallback_accepted: cProcessFallbackAccepted,
      engineering_signoff_path: PATHS.engineeringSignoff,
      front_frame_hole_cut_count: holeCutCount(frontFrameResult),
      front_frame_flat_dxf_entity_counts: frontFrameFlatCheck.entity_counts || null,
    },
    blocking_reasons: blockingReasons,
    checks_total: checks.length,
    checks_failed_blocking: failedRequired.length,
    checks,
    next_actions:
      status === 'READY'
        ? [
            'Issue the checked DXF set only with matching SLDPRT/STEP/result JSON evidence.',
            'Have structure engineering confirm hinge datum, bend relief, coating/tolerance, and supplier process notes before production release.',
          ]
        : [
            'Do not issue the current door flat DXF to supplier as production manufacturing data.',
            'For true-cut release, add an accepted B_ADJUST_TOP_LEFT_CORNER_RELIEF engineering signoff in data/locker_16029_door_upper_hinge_engineering_signoff.json.',
            'Close the door panel with 4/4 true SolidWorks cut features while keeping SheetMetal/FlatPattern and folded 307 x 298 x 18.2 mm.',
            'If option C is formally selected, keep this true-cut supplier precheck blocked and use a separate process-release review for the post-process upper hinge drilling operation.',
          ],
  }
}

const options = parseArgs(process.argv.slice(2))
const report = buildReport()
const outPath = abs(options.out)
mkdirSync(dirname(outPath), { recursive: true })
writeFileSync(outPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8')

console.log(`${report.status}: ${options.out}`)
for (const reason of report.blocking_reasons) console.log(`- ${reason}`)

if (options.failOnBlocked && report.status !== 'READY') process.exit(1)
