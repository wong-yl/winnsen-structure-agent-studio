import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, extname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { planTemplateFullAssemblyRequest } from './locker_16029_template_rules.mjs'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')
const CONTRACT_PATH = resolve(ROOT, 'data/locker_16029_part_first_pilot_contract.json')
const MANIFEST_PATH = resolve(ROOT, 'data/locker_16029_v43_internal_sheetmetal_delivery.json')
const GOLD_RULES_PATH = resolve(ROOT, 'data/locker_16029_gold_sheetmetal_rules.json')
const DOOR_TARGET_SPEC_PATH = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json'
const DOOR_GENERATION_SUMMARY_PATH = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/solidworks_2020_native_generation_summary.json'
const DOOR_SMOKE_RESULT_PATH = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43_smoke_rebuild_v4/smoke_door_panel_2_12_right_v43_result.json'
const DOOR_SMOKE_FLAT_CHECK_PATH = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43_smoke_rebuild_v4/smoke_door_panel_2_12_right_v43_flat_check.json'

function parseArgs(argv) {
  const options = {
    strictArtifacts: false,
    out: '',
  }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--strict-artifacts') options.strictArtifacts = true
    else if (arg === '--out') options.out = argv[++index] || ''
  }
  return options
}

function readText(path) {
  return readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
}

function readJson(path) {
  return JSON.parse(readText(path))
}

function rel(path) {
  return path.replaceAll('\\', '/').replace(ROOT.replaceAll('\\', '/') + '/', '')
}

function abs(path) {
  return resolve(ROOT, path)
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

function roundMm(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function fileInfo(path) {
  const full = abs(path)
  if (!existsSync(full)) return { path, exists: false, size: 0 }
  const stat = statSync(full)
  return { path, exists: true, size: stat.size }
}

function artifactExists(path) {
  if (!path) return false
  if (existsSync(String(path))) return true
  return existsSync(abs(rel(resolve(String(path)))))
}

const checks = []

function check(name, ok, actual, expected, options = {}) {
  checks.push({
    name,
    ok: Boolean(ok),
    actual,
    expected,
    category: options.category || 'contract',
    required: options.required !== false,
    strictOnly: options.strictOnly === true,
  })
}

function checkFile(name, path, options = {}) {
  const info = fileInfo(path)
  check(name, info.exists, info, 'file exists', options)
  return info
}

function partById(contract, id) {
  return asArray(contract.selected_parts).find((part) => part.id === id)
}

function rowBySideAndUnit(plan, side, unit) {
  for (const column of asArray(plan.columns)) {
    if (column.side !== side) continue
    const found = asArray(column.rows).find((row) => String(row.label) === unit)
    if (found) return found
  }
  return null
}

function artifactListForPart(part) {
  return Object.entries(part.expected_artifacts || {}).map(([kind, path]) => ({
    kind,
    path,
    extension: extname(path).toLowerCase(),
  }))
}

function seedArtifactListForPart(part) {
  return Object.entries(part.seed_evidence_artifacts || {}).map(([kind, path]) => ({
    kind,
    path,
    extension: extname(path).toLowerCase(),
  }))
}

function validateResultJsonIfPresent(label, path, expectedKind) {
  const full = abs(path)
  if (!existsSync(full)) return
  let json = null
  try {
    json = readJson(full)
    check(`${label}:result_json_parse`, true, path, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check(`${label}:result_json_parse`, false, String(error), 'valid JSON', { category: 'pilot' })
    return
  }

  if (expectedKind === 'door_panel') {
    const savedPart = json.savedPart === true || json.partSaved === true
    const savedStep = json.savedStep === true || json.stepSaved === true
    const hasSheetMetal = asArray(json.features).some((feature) => String(feature.type) === 'SheetMetal')
    const hasFlatPattern = asArray(json.features).some((feature) => String(feature.type) === 'FlatPattern')
    check(`${label}:result_saved_part`, savedPart, savedPart, true, { category: 'pilot' })
    check(`${label}:result_saved_step`, savedStep, savedStep, true, { category: 'pilot' })
    check(`${label}:result_has_sheet_metal`, hasSheetMetal, hasSheetMetal, true, { category: 'pilot' })
    check(`${label}:result_has_flat_pattern`, hasFlatPattern, hasFlatPattern, true, { category: 'pilot' })
  } else {
    if (Object.hasOwn(json, 'build_status')) {
      check(`${label}:result_build_completed`, json.build_status === 'completed' && json.saved === true, { build_status: json.build_status, saved: json.saved, error: json.error || '' }, 'minimum assembly built and saved', { category: 'pilot' })
    }
    if (Object.hasOwn(json, 'opened')) {
      check(`${label}:result_opened`, json.opened === true, json.opened, true, { category: 'pilot' })
    }
    if (Object.hasOwn(json, 'step_saved')) {
      check(`${label}:result_step_saved`, json.step_saved === true && json.step_exists === true, { step_saved: json.step_saved, step_exists: json.step_exists }, 'STEP exported', { category: 'pilot' })
    }
  }
}

function validateFlatExportJsonIfPresent(label, path) {
  const full = abs(path)
  if (!existsSync(full)) return
  let json = null
  try {
    json = readJson(full)
    check(`${label}:flat_export_json_parse`, true, path, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check(`${label}:flat_export_json_parse`, false, String(error), 'valid JSON', { category: 'pilot' })
    return
  }
  if (label.startsWith('front_frame_') && json.gap_accepted === true) {
    check(`${label}:flat_export_gap_accepted`, true, json.status || 'GAP_ACCEPTED_FOR_PILOT_ONLY', 'front-frame flat-pattern gap accepted for pilot only', { category: 'pilot' })
    return
  }
  check(`${label}:flat_export_opened`, json.opened === true, json.opened, true, { category: 'pilot' })
  check(`${label}:flat_export_has_sheet_metal`, json.has_sheet_metal === true, json.has_sheet_metal, true, { category: 'pilot' })
  check(`${label}:flat_export_has_flat_pattern`, json.has_flat_pattern === true, json.has_flat_pattern, true, { category: 'pilot' })
  check(`${label}:flat_exported`, json.flat_exported === true && json.dxf_exists === true, { flat_exported: json.flat_exported, dxf_exists: json.dxf_exists }, 'flat DXF exported', { category: 'pilot' })
}

function validateDoorFlatReconciliationIfPresent(label, flatCheckPath, flatCheckJson) {
  if (label !== 'door_panel_2_12_right_v43') return false
  const reconciliationPath = flatCheckPath.replace(/_flat_check\.json$/i, '_flat_rule_reconciliation.json')
  const full = abs(reconciliationPath)
  check(`${label}:flat_rule_reconciliation_exists`, existsSync(full), fileInfo(reconciliationPath), 'reconciliation JSON exists', { category: 'pilot' })
  if (!existsSync(full)) return false

  let json = null
  try {
    json = readJson(full)
    check(`${label}:flat_rule_reconciliation_json_parse`, true, reconciliationPath, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check(`${label}:flat_rule_reconciliation_json_parse`, false, String(error), 'valid JSON', { category: 'pilot' })
    return false
  }

  const statusOk = json.status === 'RECONCILED_FOR_PILOT_ONLY'
  const releaseBlocker = json.release_blocker === true
  const current = json.sw2020_current_pilot_flat || {}
  const gold = json.gold_manufacturing_rule || {}
  const targetSpecRef = json.target_spec || {}
  const bbox = flatCheckJson.bbox_mm || {}
  const currentMatchesFlatCheck =
    almostEqual(current.bbox_width_mm, bbox.width_mm, 0.001) &&
    almostEqual(current.bbox_height_mm, bbox.height_mm, 0.001)
  const goldExpectationRecorded =
    almostEqual(gold.expected_flat_width_mm, flatCheckJson.expected_width_mm, 0.001) &&
    almostEqual(gold.expected_flat_height_mm, flatCheckJson.expected_height_mm, 0.001)
  let targetSpecOk = true
  if (targetSpecRef.json) {
    const targetSpecPath = String(targetSpecRef.json)
    const targetSpecFull = abs(targetSpecPath)
    check(`${label}:flatpattern_target_spec_exists`, existsSync(targetSpecFull), fileInfo(targetSpecPath), 'target spec JSON exists', { category: 'pilot' })
    if (existsSync(targetSpecFull)) {
      try {
        const targetSpec = readJson(targetSpecFull)
        const target = targetSpec.target || {}
        const holes = asArray(targetSpec.target_hole_datums)
        const dimensionsOk =
          targetSpec.status === 'TARGET_SPEC_READY_RELEASE_BLOCKER_STILL_OPEN' &&
          almostEqual(target.flat_width_mm, gold.expected_flat_width_mm, 0.001) &&
          almostEqual(target.flat_height_mm, gold.expected_flat_height_mm, 0.001)
        const holeDatumOk = holes.length === 4
        const stillBlocking = targetSpec.release_blocker === true
        targetSpecOk = dimensionsOk && holeDatumOk && stillBlocking
        check(`${label}:flatpattern_target_spec_json_parse`, true, targetSpecPath, 'valid JSON', { category: 'pilot' })
        check(`${label}:flatpattern_target_spec_dimensions`, dimensionsOk, target, 'target flat dimensions match gold expectation', { category: 'pilot' })
        check(`${label}:flatpattern_target_spec_hole_datums`, holeDatumOk, holes.length, 4, { category: 'pilot' })
        check(`${label}:flatpattern_target_spec_release_blocker`, stillBlocking, targetSpec.release_blocker, true, { category: 'pilot' })
      } catch (error) {
        targetSpecOk = false
        check(`${label}:flatpattern_target_spec_json_parse`, false, String(error), 'valid JSON', { category: 'pilot' })
      }
    } else {
      targetSpecOk = false
    }
  }

  check(`${label}:flat_rule_reconciliation_status`, statusOk, json.status, 'RECONCILED_FOR_PILOT_ONLY', { category: 'pilot' })
  check(`${label}:flat_rule_reconciliation_release_blocker`, releaseBlocker, json.release_blocker, true, { category: 'pilot' })
  check(`${label}:flat_rule_reconciliation_release_blocker_closed`, !releaseBlocker, json.release_blocker, false, { category: 'pilot', strictOnly: true })
  check(`${label}:flat_rule_reconciliation_matches_current_dxf`, currentMatchesFlatCheck, { current, bbox }, 'reconciliation matches parsed current DXF bbox', { category: 'pilot' })
  check(`${label}:flat_rule_reconciliation_records_gold_expectation`, goldExpectationRecorded, gold, 'gold expected flat bbox recorded', { category: 'pilot' })
  return statusOk && releaseBlocker && currentMatchesFlatCheck && goldExpectationRecorded && targetSpecOk
}

function validateFlatCheckJsonIfPresent(label, path) {
  const full = abs(path)
  if (!existsSync(full)) return
  let json = null
  try {
    json = readJson(full)
    check(`${label}:flat_check_json_parse`, true, path, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check(`${label}:flat_check_json_parse`, false, String(error), 'valid JSON', { category: 'pilot' })
    return
  }
  if (label.startsWith('front_frame_') && json.gap_accepted === true) {
    check(`${label}:flat_check_gap_accepted`, true, json.status || 'GAP_ACCEPTED_FOR_PILOT_ONLY', 'front-frame flat-pattern gap accepted for pilot only', { category: 'pilot' })
    return
  }
  if (json.status !== 'PASS' && validateDoorFlatReconciliationIfPresent(label, path, json)) {
    check(`${label}:flat_check_mismatch_reconciled_for_pilot`, true, json.status, 'mismatch explained and accepted for pilot only', { category: 'pilot' })
    check(`${label}:flat_check_entity_count`, asNumber(json.entity_count_total) > 0, json.entity_count_total, '> 0', { category: 'pilot' })
    return
  }
  if (json.release_blocker === true) {
    check(`${label}:flat_check_release_blocker_closed`, false, json.release_blocker, false, { category: 'pilot', strictOnly: true })
  }
  check(`${label}:flat_check_status`, json.status === 'PASS', json.status, 'PASS', { category: 'pilot' })
  check(`${label}:flat_check_entity_count`, asNumber(json.entity_count_total) > 0, json.entity_count_total, '> 0', { category: 'pilot' })
  if (json.expected_width_mm != null) {
    check(`${label}:flat_check_width`, json.width_ok === true, { width_ok: json.width_ok, bbox: json.bbox_mm, expected_width_mm: json.expected_width_mm }, 'width within tolerance', { category: 'pilot' })
  }
  if (json.expected_height_mm != null) {
    check(`${label}:flat_check_height`, json.height_ok === true, { height_ok: json.height_ok, bbox: json.bbox_mm, expected_height_mm: json.expected_height_mm }, 'height within tolerance', { category: 'pilot' })
  }
}

function targetDatumRows(targetSpec) {
  return asArray(targetSpec?.target_hole_datums).map((datum) => ({
    role: String(datum.role || ''),
    diameterMm: asNumber(datum.diameter_mm),
    cxMm: asNumber(datum.target_cx_from_center_mm),
    cyMm: asNumber(datum.target_cy_from_center_mm),
    horizontalAnchor: String(datum.horizontal_anchor || ''),
    horizontalOffsetMm: asNumber(datum.horizontal_offset_from_anchor_mm),
    verticalAnchor: String(datum.vertical_anchor || ''),
    verticalOffsetMm: asNumber(datum.vertical_offset_from_anchor_mm),
  }))
}

function observedDatumRows(datums) {
  return asArray(datums).map((datum) => ({
    role: String(datum.role || ''),
    diameterMm: asNumber(datum.diameterMm ?? datum.diameter_mm),
    cxMm: asNumber(datum.generatedFlatCxFromCenterMm ?? datum.target_cx_from_center_mm ?? datum.cxMm),
    cyMm: asNumber(datum.generatedFlatCyFromCenterMm ?? datum.target_cy_from_center_mm ?? datum.cyMm),
    horizontalAnchor: String(datum.horizontalAnchor ?? datum.horizontal_anchor ?? ''),
    horizontalOffsetMm: asNumber(datum.horizontalOffsetFromAnchorMm ?? datum.horizontal_offset_from_anchor_mm ?? datum.horizontalOffsetMm),
    verticalAnchor: String(datum.verticalAnchor ?? datum.vertical_anchor ?? ''),
    verticalOffsetMm: asNumber(datum.verticalOffsetFromAnchorMm ?? datum.vertical_offset_from_anchor_mm ?? datum.verticalOffsetMm),
  }))
}

function summarizeDatumRows(rows) {
  return rows.map((row) => ({
    role: row.role,
    dia: roundMm(row.diameterMm),
    cx: roundMm(row.cxMm),
    cy: roundMm(row.cyMm),
    x_anchor: row.horizontalAnchor,
    x_offset: roundMm(row.horizontalOffsetMm),
    y_anchor: row.verticalAnchor,
    y_offset: roundMm(row.verticalOffsetMm),
  }))
}

function validateHoleDatumsAgainstTarget(label, observedDatums, targetSpec, options = {}) {
  const observed = observedDatumRows(observedDatums)
  const target = targetDatumRows(targetSpec)
  const category = options.category || 'pilot'
  check(`${label}:hole_datum_count_matches_target_spec`, observed.length === target.length, observed.length, target.length, { category })
  const coordinatesOk = observed.length === target.length && observed.every((row, index) => {
    const expected = target[index]
    return row.role === expected.role &&
      almostEqual(row.diameterMm, expected.diameterMm, 0.001) &&
      almostEqual(row.cxMm, expected.cxMm, 0.001) &&
      almostEqual(row.cyMm, expected.cyMm, 0.001)
  })
  const anchorsOk = observed.length === target.length && observed.every((row, index) => {
    const expected = target[index]
    return row.horizontalAnchor === expected.horizontalAnchor &&
      row.verticalAnchor === expected.verticalAnchor &&
      almostEqual(row.horizontalOffsetMm, expected.horizontalOffsetMm, 0.001) &&
      almostEqual(row.verticalOffsetMm, expected.verticalOffsetMm, 0.001)
  })
  check(`${label}:hole_datum_coordinates_match_target_spec`, coordinatesOk, summarizeDatumRows(observed), summarizeDatumRows(target), { category })
  check(`${label}:hole_datum_edge_anchors_match_target_spec`, anchorsOk, summarizeDatumRows(observed), summarizeDatumRows(target), { category })
}

function validateDoorGenerationSummaryIfPresent(targetSpec) {
  const full = abs(DOOR_GENERATION_SUMMARY_PATH)
  check('door_panel_2_12_right_v43:generation_summary_exists', existsSync(full), fileInfo(DOOR_GENERATION_SUMMARY_PATH), 'generation summary exists', { category: 'pilot' })
  if (!existsSync(full)) return
  let json = null
  try {
    json = readJson(full)
    check('door_panel_2_12_right_v43:generation_summary_json_parse', true, DOOR_GENERATION_SUMMARY_PATH, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check('door_panel_2_12_right_v43:generation_summary_json_parse', false, String(error), 'valid JSON', { category: 'pilot' })
    return
  }
  const target = targetSpec?.target || {}
  check('door_panel_2_12_right_v43:generation_summary_rule_binding', json.sheetMetalRuleBindingStatus === 'bound_to_1000w_gold_dxf', json.sheetMetalRuleBindingStatus, 'bound_to_1000w_gold_dxf', { category: 'pilot' })
  check('door_panel_2_12_right_v43:generation_summary_expected_flat_width', almostEqual(json.sheetMetalExpectedFlatWidthMm, target.flat_width_mm, 0.001), json.sheetMetalExpectedFlatWidthMm, target.flat_width_mm, { category: 'pilot' })
  check('door_panel_2_12_right_v43:generation_summary_expected_flat_height', almostEqual(json.sheetMetalExpectedFlatHeightMm, target.flat_height_mm, 0.001), json.sheetMetalExpectedFlatHeightMm, target.flat_height_mm, { category: 'pilot' })
  check('door_panel_2_12_right_v43:generation_summary_datum_mapping_status', json.sheetMetalHoleDatumMappingStatus === 'edge_anchored_target_spec_aligned', json.sheetMetalHoleDatumMappingStatus, 'edge_anchored_target_spec_aligned', { category: 'pilot' })
  check('door_panel_2_12_right_v43:generation_summary_geometry_still_blocked', String(json.sheetMetalHoleDatumCorrection?.status || '').includes('geometry_still_release_blocked'), json.sheetMetalHoleDatumCorrection?.status || '', 'metadata corrected, geometry still release-blocked', { category: 'pilot' })
  validateHoleDatumsAgainstTarget('door_panel_2_12_right_v43:generation_summary', json.sheetMetalHoleDatums, targetSpec, { category: 'pilot' })
}

function validateDoorSmokeRebuildIfPresent(targetSpec) {
  const resultFull = abs(DOOR_SMOKE_RESULT_PATH)
  if (!existsSync(resultFull)) return
  let result = null
  try {
    result = readJson(resultFull)
    check('door_panel_2_12_right_v43:smoke_result_json_parse', true, DOOR_SMOKE_RESULT_PATH, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check('door_panel_2_12_right_v43:smoke_result_json_parse', false, String(error), 'valid JSON', { category: 'pilot' })
    return
  }
  check('door_panel_2_12_right_v43:smoke_artifact_sldprt_exists', artifactExists(result.outPart), result.outPart || '', 'smoke SLDPRT exists', { category: 'pilot', required: false })
  check('door_panel_2_12_right_v43:smoke_artifact_step_exists', artifactExists(result.outStep), result.outStep || '', 'smoke STEP exists', { category: 'pilot', required: false })
  check('door_panel_2_12_right_v43:smoke_artifact_dxf_exists', artifactExists(result.outDxf), result.outDxf || '', 'smoke DXF exists', { category: 'pilot', required: false })
  check('door_panel_2_12_right_v43:smoke_release_blocker_not_closed', result.releaseBlockerClosed === false, result.releaseBlockerClosed, false, { category: 'pilot' })
  check('door_panel_2_12_right_v43:smoke_status_degraded_or_complete', String(result.status || '').startsWith('SMOKE_'), result.status || '', 'SMOKE_* status', { category: 'pilot' })
  check('door_panel_2_12_right_v43:smoke_hole_cut_count', asNumber(result.holeCutCount) === 4, result.holeCutCount, 4, { category: 'pilot' })
  check('door_panel_2_12_right_v43:smoke_bend_line_count', asNumber(result.bendLineCount) === 4, result.bendLineCount, 4, { category: 'pilot' })
  const trueSheetMetalSmoke = result.hasSheetMetalFeature === true && result.hasFlatPatternFeature === true
  const smokeStateExplained = trueSheetMetalSmoke || String(result.status || '').includes('DEGRADED')
  check('door_panel_2_12_right_v43:smoke_sheetmetal_state_explained', smokeStateExplained, {
    hasSheetMetalFeature: result.hasSheetMetalFeature,
    hasFlatPatternFeature: result.hasFlatPatternFeature,
    sketchedBendCreated: result.sketchedBendCreated,
    status: result.status,
  }, 'true sheet-metal smoke or degraded status explains the gap', { category: 'pilot' })
  if (targetSpec) validateHoleDatumsAgainstTarget('door_panel_2_12_right_v43:smoke_result', result.holeDatums, targetSpec, { category: 'pilot' })

  const flatFull = abs(DOOR_SMOKE_FLAT_CHECK_PATH)
  check('door_panel_2_12_right_v43:smoke_flat_check_exists', existsSync(flatFull), fileInfo(DOOR_SMOKE_FLAT_CHECK_PATH), 'smoke flat check exists', { category: 'pilot', required: false })
  if (!existsSync(flatFull)) return
  try {
    const flat = readJson(flatFull)
    const target = targetSpec?.target || {}
    check('door_panel_2_12_right_v43:smoke_flat_check_json_parse', true, DOOR_SMOKE_FLAT_CHECK_PATH, 'valid JSON', { category: 'pilot' })
    check('door_panel_2_12_right_v43:smoke_flat_check_status', flat.status === 'PASS', flat.status, 'PASS', { category: 'pilot' })
    check('door_panel_2_12_right_v43:smoke_flat_check_target_width', almostEqual(flat.bbox_mm?.width_mm, target.flat_width_mm, 0.25), flat.bbox_mm, target.flat_width_mm, { category: 'pilot' })
    check('door_panel_2_12_right_v43:smoke_flat_check_target_height', almostEqual(flat.bbox_mm?.height_mm, target.flat_height_mm, 0.25), flat.bbox_mm, target.flat_height_mm, { category: 'pilot' })
  } catch (error) {
    check('door_panel_2_12_right_v43:smoke_flat_check_json_parse', false, String(error), 'valid JSON', { category: 'pilot' })
  }
}

function validateInterferenceJsonIfPresent(label, path) {
  const full = abs(path)
  if (!existsSync(full)) return
  let json = null
  try {
    json = readJson(full)
    check(`${label}:interference_json_parse`, true, path, 'valid JSON', { category: 'pilot' })
  } catch (error) {
    check(`${label}:interference_json_parse`, false, String(error), 'valid JSON', { category: 'pilot' })
    return
  }

  const status = String(json.status || '')
  const checksNotPerformed = asArray(json.checks_not_performed)
  const pending = status.includes('PENDING') || checksNotPerformed.length > 0
  check(`${label}:interference_or_clearance_complete`, !pending, { status, checks_not_performed: checksNotPerformed }, 'true SolidWorks interference/clearance check complete', { category: 'pilot', strictOnly: true })

  const blockingReasons = asArray(json.blocking_reasons)
  const releaseBlocking = blockingReasons.filter((item) => String(item.severity || '').includes('blocking'))
  check(`${label}:release_blockers_closed`, releaseBlocking.length === 0, releaseBlocking.map((item) => item.id || item.severity), 'no release-blocking minimum-interface reasons', { category: 'pilot', strictOnly: true })
}

function validateSeedResultJson(label, path, expectedKind) {
  const full = abs(path)
  if (!existsSync(full)) return
  let json = null
  try {
    json = readJson(full)
    check(`${label}:seed_result_json_parse`, true, path, 'valid JSON', { category: 'seed' })
  } catch (error) {
    check(`${label}:seed_result_json_parse`, false, String(error), 'valid JSON', { category: 'seed' })
    return
  }

  if (expectedKind === 'door_panel') {
    const opened = json.opened === true
    const rebuilt = json.rebuilt === true
    const savedPart = json.savedPart === true || json.partSaved === true
    const savedStep = json.savedStep === true || json.stepSaved === true
    const hasSheetMetal = asArray(json.features).some((feature) => String(feature.type) === 'SheetMetal')
    const hasFlatPattern = asArray(json.features).some((feature) => String(feature.type) === 'FlatPattern')
    check(`${label}:seed_opened`, opened, opened, true, { category: 'seed' })
    check(`${label}:seed_rebuilt`, rebuilt, rebuilt, true, { category: 'seed' })
    check(`${label}:seed_saved_part`, savedPart, savedPart, true, { category: 'seed' })
    check(`${label}:seed_saved_step`, savedStep, savedStep, true, { category: 'seed' })
    check(`${label}:seed_has_sheet_metal`, hasSheetMetal, hasSheetMetal, true, { category: 'seed' })
    check(`${label}:seed_has_flat_pattern`, hasFlatPattern, hasFlatPattern, true, { category: 'seed' })
  }

  if (expectedKind === 'front_frame_seed') {
    check(`${label}:seed_assembly_rebuilt`, json.rebuilt === true, json.rebuilt, true, { category: 'seed' })
    check(`${label}:seed_assembly_saved`, json.saved === true, json.saved, true, { category: 'seed' })
    check(`${label}:seed_component_count`, asArray(json.components).length >= 6, asArray(json.components).length, '>= 6', { category: 'seed' })
  }
}

function buildNextActions(status) {
  if (status === 'PASS') {
    return [
      'Run structural review on the passing minimum assembly before expanding to side panels, bottom, top, partitions, and full cabinet assembly.',
    ]
  }

  const names = new Set(checks.filter((item) => !item.ok).map((item) => item.name))
  const actions = []

  if (names.has('door_panel_2_12_right_v43:flat_check_status') || names.has('door_panel_2_12_right_v43:flat_check_width') || names.has('door_panel_2_12_right_v43:flat_check_height')) {
    actions.push('Resolve the door-panel flat-pattern conflict: current SW2020 flat DXF measures 307 x 298 mm, while the v43 gold-rule expectation is 343.4 x 334.4 mm.')
  }
  if (names.has('door_panel_2_12_right_v43:flat_rule_reconciliation_release_blocker_closed')) {
    actions.push('Close the door-panel release blocker by promoting the v4 smoke result into the real door artifact: keep 343.4 x 334.4 mm FlatPattern and 4/4 holes, then prove the SketchedBend/folded 307 x 298 mm installed state or obtain engineering approval to revise the rule.')
  }
  if (names.has('front_frame_vertical_left_v43:flat_check_release_blocker_closed')) {
    actions.push('Close the front-frame release blocker by rebuilding a true SW2020 SheetMetal/FlatPattern part; the current source DXF reference is pilot evidence only.')
  }
  if (names.has('front_frame_vertical_left_v43:result_opened') || names.has('front_frame_vertical_left_v43:flat_export_opened')) {
    actions.push('Repair or replace the front-frame single-part route: the copied front-frame SLDPRT exists, but current automated SW2020 open/export checks do not produce STEP or flat DXF.')
  }
  if ([...names].some((name) => name.startsWith('minimum_assembly_artifact_exists:'))) {
    actions.push('Build the minimum door/frame/lock-datum/shelf interface assembly only after the front-frame single-part gate is usable, then record interference or datum-gap results.')
  }
  if (names.has('minimum_interface_assembly:result_build_completed')) {
    actions.push('Restore a working SolidWorks COM automation session and rerun the prepared minimum-interface placement TSV; the latest build result says SolidWorks unavailable.')
  }
  if (names.has('minimum_interface_assembly:interference_or_clearance_complete')) {
    actions.push('Run a true SolidWorks minimum-interface clearance/interference check; the fallback STEP proves exchange/readability only.')
  }
  if (names.has('minimum_interface_assembly:release_blockers_closed')) {
    actions.push('Keep the minimum-interface assembly blocked until door folded-state evidence, front-frame FlatPattern, native SLDASM-to-STEP export, and trusted clearance evidence are all closed.')
  }
  if (!actions.length) {
    actions.push('Resolve the remaining pilot checks and rerun the gate.')
  }
  actions.push('Keep all BOM rows at pilot_only_not_released until every required part artifact and the minimum interface assembly gate pass.')
  return actions
}

function main() {
  const options = parseArgs(process.argv.slice(2))

  checkFile('contract_file_exists', rel(CONTRACT_PATH), { category: 'contract' })
  checkFile('v43_manifest_exists', rel(MANIFEST_PATH), { category: 'source' })
  checkFile('gold_rules_exists', rel(GOLD_RULES_PATH), { category: 'source' })

  const contract = readJson(CONTRACT_PATH)
  const manifest = readJson(MANIFEST_PATH)
  const goldRules = readJson(GOLD_RULES_PATH)
  let doorTargetSpec = null
  try {
    if (existsSync(abs(DOOR_TARGET_SPEC_PATH))) doorTargetSpec = readJson(abs(DOOR_TARGET_SPEC_PATH))
  } catch {
    doorTargetSpec = null
  }
  const plan = planTemplateFullAssemblyRequest({
    cabinetWidthMm: contract.route?.cabinet_width_mm,
    cabinetHeightMm: contract.route?.cabinet_height_mm,
    cabinetDepthMm: contract.route?.cabinet_depth_mm,
    doorCount: contract.route?.door_count,
    rowSequence: contract.route?.row_sequence,
  })

  check('contract_schema', contract.schema === 'winnsen.locker16029.part_first_pilot_contract.v1', contract.schema, 'winnsen.locker16029.part_first_pilot_contract.v1')
  check('contract_status', contract.status === 'pilot_contract_ready', contract.status, 'pilot_contract_ready')
  check('route_product', contract.route?.product === '16029', contract.route?.product, '16029')
  check('route_width_matches_manifest', asNumber(contract.route?.cabinet_width_mm) === asNumber(manifest.route?.cabinet_width_mm), contract.route?.cabinet_width_mm, manifest.route?.cabinet_width_mm)
  check('route_row_sequence_matches_manifest', contract.route?.row_sequence === manifest.route?.row_sequence, contract.route?.row_sequence, manifest.route?.row_sequence)
  check('route_cad_mainline', contract.route?.cad_mainline === 'SolidWorks 2020', contract.route?.cad_mainline, 'SolidWorks 2020')
  check('route_not_direct_full_assembly', contract.route?.direct_full_assembly_generation === false, contract.route?.direct_full_assembly_generation, false)

  const outOfScope = asArray(contract.boundaries?.out_of_scope)
  check('boundary_excludes_electrical_boards', outOfScope.includes('electrical boards'), outOfScope, 'electrical boards excluded')
  check('boundary_excludes_cabinet_side_electric_locks', outOfScope.includes('cabinet-side electric lock bodies') && outOfScope.includes('cabinet-side electric lock hooks'), outOfScope, 'cabinet-side electric locks excluded')
  check('boundary_not_production_release', outOfScope.includes('production drawing release') && outOfScope.includes('released BOM'), outOfScope, 'not production release')

  check('gold_rules_generator_binding_ready', goldRules.generatorBinding?.status === 'ready_for_rule_plan_binding', goldRules.generatorBinding?.status, 'ready_for_rule_plan_binding', { category: 'source' })
  check('gold_rules_hole_feature_boundary', goldRules.generatorBinding?.solidWorksHoleFeatureStatus === 'datum_ready_not_cut_feature', goldRules.generatorBinding?.solidWorksHoleFeatureStatus, 'datum_ready_not_cut_feature', { category: 'source' })
  check('template_plan_status', plan.status === 'ready', plan.status, 'ready', { category: 'source' })
  check('template_plan_door_width', almostEqual(plan.derived?.doorWidthMm, 307), plan.derived?.doorWidthMm, 307, { category: 'source' })
  check('template_plan_is_v43_native_seed', plan.derived?.compatibleWithNativeTemplate === true, plan.derived?.compatibleWithNativeTemplate, true, { category: 'source' })

  const doorRow = rowBySideAndUnit(plan, 'R', '2/12')
  check('template_plan_selected_door_row_exists', Boolean(doorRow), doorRow?.label || '', 'R 2/12 row', { category: 'source' })
  if (doorRow) {
    check('template_plan_selected_door_height', almostEqual(doorRow.heightMm, 298), doorRow.heightMm, 298, { category: 'source' })
    check('template_plan_selected_door_flat_width', almostEqual(doorRow.sheetMetal?.generatedFlatWidthMm, 343.4), doorRow.sheetMetal?.generatedFlatWidthMm, 343.4, { category: 'source' })
    check('template_plan_selected_door_flat_height', almostEqual(doorRow.sheetMetal?.generatedFlatHeightMm, 334.4), doorRow.sheetMetal?.generatedFlatHeightMm, 334.4, { category: 'source' })
    check('template_plan_selected_door_hole_count', asNumber(doorRow.sheetMetal?.holeDatumCount) === 4, doorRow.sheetMetal?.holeDatumCount, 4, { category: 'source' })
    if (doorTargetSpec) validateHoleDatumsAgainstTarget('template_plan_selected_door', doorRow.sheetMetal?.holeDatums, doorTargetSpec, { category: 'source' })
  }

  const parts = asArray(contract.selected_parts)
  check('selected_part_count', parts.length === 3, parts.map((part) => part.id), '3 pilot parts')
  for (const id of ['door_panel_2_12_right_v43', 'front_frame_vertical_left_v43', 'shelf_cross_panel_left_v43']) {
    check(`selected_part_present:${id}`, Boolean(partById(contract, id)), parts.map((part) => part.id), id)
  }

  const doorPart = partById(contract, 'door_panel_2_12_right_v43')
  if (doorPart) {
    check('door_part_installed_width_contract', almostEqual(doorPart.expected_parameters?.installed_width_mm, 307), doorPart.expected_parameters?.installed_width_mm, 307)
    check('door_part_installed_height_contract', almostEqual(doorPart.expected_parameters?.installed_height_mm, 298), doorPart.expected_parameters?.installed_height_mm, 298)
    check('door_part_flat_width_contract', almostEqual(doorPart.expected_parameters?.flat_width_mm, 343.4), doorPart.expected_parameters?.flat_width_mm, 343.4)
    check('door_part_flat_height_contract', almostEqual(doorPart.expected_parameters?.flat_height_mm, 334.4), doorPart.expected_parameters?.flat_height_mm, 334.4)
    check('door_part_hole_count_contract', asNumber(doorPart.expected_parameters?.expected_hole_datum_count) === 4, doorPart.expected_parameters?.expected_hole_datum_count, 4)
    if (doorTargetSpec) validateDoorGenerationSummaryIfPresent(doorTargetSpec)
    if (doorTargetSpec) validateDoorSmokeRebuildIfPresent(doorTargetSpec)
  }

  const bomRows = asArray(contract.trial_bom?.rows)
  check('trial_bom_status', contract.trial_bom?.status === 'pilot_only_not_released', contract.trial_bom?.status, 'pilot_only_not_released')
  check('trial_bom_row_count', bomRows.length === parts.length, bomRows.length, parts.length)
  for (const part of parts) {
    const row = bomRows.find((item) => item.part_id === part.id)
    check(`trial_bom_row_present:${part.id}`, Boolean(row), bomRows.map((item) => item.part_id), part.id)
    if (row) {
      check(`trial_bom_row_not_released:${part.id}`, row.validation_status === 'pending_pilot_artifacts', row.validation_status, 'pending_pilot_artifacts')
    }
  }

  const minimumAssembly = contract.minimum_interface_assembly || {}
  check('minimum_assembly_id', minimumAssembly.id === 'door_frame_lock_shelf_minimum_interface_v43', minimumAssembly.id, 'door_frame_lock_shelf_minimum_interface_v43')
  check('minimum_assembly_relation_count', asArray(minimumAssembly.relations).length >= 4, asArray(minimumAssembly.relations).length, '>= 4')
  check('minimum_assembly_purpose_not_full_cabinet', String(minimumAssembly.purpose || '').includes('Do not expand'), minimumAssembly.purpose, 'interface-only boundary')

  for (const part of parts) {
    for (const artifact of artifactListForPart(part)) {
      checkFile(`pilot_artifact_exists:${part.id}:${artifact.kind}`, artifact.path, {
        category: 'pilot',
        required: options.strictArtifacts,
        strictOnly: true,
      })
      if (artifact.kind === 'result_json') validateResultJsonIfPresent(part.id, artifact.path, part.role === 'ordinary_door_panel' ? 'door_panel' : 'generic')
      if (artifact.kind === 'flat_export_json') validateFlatExportJsonIfPresent(part.id, artifact.path)
      if (artifact.kind === 'flat_check_json') validateFlatCheckJsonIfPresent(part.id, artifact.path)
    }

    for (const artifact of seedArtifactListForPart(part)) {
      checkFile(`seed_artifact_exists:${part.id}:${artifact.kind}`, artifact.path, {
        category: 'seed',
        required: false,
      })
      if (artifact.kind === 'result_json') validateSeedResultJson(part.id, artifact.path, 'door_panel')
      if (artifact.kind === 'front_frame_result_json') validateSeedResultJson(part.id, artifact.path, 'front_frame_seed')
    }
  }

  for (const [kind, path] of Object.entries(minimumAssembly.expected_artifacts || {})) {
    checkFile(`minimum_assembly_artifact_exists:${kind}`, path, {
      category: 'pilot',
      required: options.strictArtifacts,
      strictOnly: true,
    })
    if (kind === 'result_json') validateResultJsonIfPresent('minimum_interface_assembly', path, 'generic')
    if (kind === 'interference_json') validateInterferenceJsonIfPresent('minimum_interface_assembly', path)
  }

  const blocking = checks.filter((item) => !item.ok && item.required && (!item.strictOnly || options.strictArtifacts))
  const pilotChecksFailed = checks.filter((item) => !item.ok && item.category === 'pilot')
  const missingPilotArtifacts = pilotChecksFailed.filter((item) => (
    item.name.startsWith('pilot_artifact_exists:') ||
    item.name.startsWith('minimum_assembly_artifact_exists:')
  ))
  const status = blocking.length
    ? 'FAIL'
    : pilotChecksFailed.length
      ? 'IN_PROGRESS'
      : 'PASS'

  const report = {
    schema: 'winnsen.locker16029.part_first_pilot_gate.v1',
    generated_at: new Date().toISOString(),
    strict_artifacts: options.strictArtifacts,
    status,
    contract: rel(CONTRACT_PATH),
    checks_total: checks.length,
    checks_failed_blocking: blocking.length,
    missing_pilot_artifacts: missingPilotArtifacts.length,
    pilot_checks_failed: pilotChecksFailed.length,
    checks,
    next_actions: buildNextActions(status),
  }

  const text = `${JSON.stringify(report, null, 2)}\n`
  if (options.out) {
    writeFileSync(resolve(ROOT, options.out), text, 'utf8')
  }
  process.stdout.write(text)
  if (blocking.length) process.exit(1)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) {
  main()
}
