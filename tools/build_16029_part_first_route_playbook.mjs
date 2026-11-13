import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  contract: 'data/locker_16029_part_first_pilot_contract.json',
  doorResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_result.json',
  doorFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json',
  doorTargetSpec:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json',
  doorDecision: 'data/locker_16029_door_upper_hinge_rule_decision.json',
  engineeringPack: 'data/locker_16029_door_upper_hinge_engineering_confirmation_pack.json',
  frontFrameResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_result.json',
  frontFrameFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_flat_check.json',
  shelfResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43_result.json',
  shelfFlatExport:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43_flat_export.json',
  shelfFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43_flat_check.json',
  defaultGate: 'data/locker_16029_part_first_pilot_gate_report.json',
  strictGate: 'data/locker_16029_part_first_pilot_gate_report.strict.json',
  supplierPrecheck: 'data/locker_16029_supplier_dxf_precheck.json',
  outJson: 'data/locker_16029_part_first_route_playbook.json',
  outMd: 'docs/16029_part_first_route_playbook.md',
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

function bboxFromDoorResult(result) {
  return [
    result.foldedEnvelopeMeasuredWidthMm ?? null,
    result.foldedEnvelopeMeasuredHeightMm ?? null,
    result.foldedEnvelopeMeasuredDepthMm ?? null,
  ]
}

function buildPlaybook() {
  const contract = readJson(PATHS.contract)
  const doorResult = readJson(PATHS.doorResult)
  const doorFlatCheck = readJson(PATHS.doorFlatCheck)
  const doorTargetSpec = readJson(PATHS.doorTargetSpec)
  const doorDecision = readJson(PATHS.doorDecision)
  const engineeringPack = readJson(PATHS.engineeringPack)
  const frontFrameResult = readJson(PATHS.frontFrameResult)
  const frontFrameFlatCheck = readJson(PATHS.frontFrameFlatCheck)
  const shelfResult = readJson(PATHS.shelfResult)
  const shelfFlatExport = readJson(PATHS.shelfFlatExport)
  const shelfFlatCheck = readJson(PATHS.shelfFlatCheck)
  const defaultGate = readJson(PATHS.defaultGate)
  const strictGate = readJson(PATHS.strictGate)
  const supplierPrecheck = existsSync(abs(PATHS.supplierPrecheck)) ? readJson(PATHS.supplierPrecheck) : {}

  const selectedParts = asArray(contract.selected_parts)
  const doorPart = selectedParts.find((part) => part.id === 'door_panel_2_12_right_v43') || {}
  const frontFramePart = selectedParts.find((part) => part.id === 'front_frame_vertical_left_v43') || {}
  const shelfPart = selectedParts.find((part) => part.id === 'shelf_cross_panel_left_v43') || {}
  const upperHinge = asArray(doorDecision.holes).find((hole) => hole.role === 'hinge_upper') || {}

  const doorCadClosed =
    doorResult.hasSheetMetalFeature === true &&
    doorResult.hasFlatPatternFeature === true &&
    asNumber(doorResult.holeCutCount) === 4 &&
    doorFlatCheck.status === 'PASS' &&
    asNumber(doorFlatCheck.entity_counts?.CIRCLE) >= 4

  const secondPartClosed =
    frontFrameResult.has_sheet_metal === true &&
    frontFrameResult.has_flat_pattern === true &&
    frontFrameResult.flat_exported === true &&
    frontFrameFlatCheck.status === 'PASS' &&
    asNumber(frontFrameResult.hole_cut_count) === 12

  const thirdPartClosed =
    shelfResult.step_saved === true &&
    shelfResult.step_exists === true &&
    shelfFlatExport.has_sheet_metal === true &&
    shelfFlatExport.has_flat_pattern === true &&
    shelfFlatExport.flat_exported === true &&
    shelfFlatCheck.status === 'PASS' &&
    asNumber(shelfFlatCheck.entity_count_total) > 0

  const gatesClosed = defaultGate.status === 'PASS' && strictGate.status === 'PASS'

  return {
    schema: 'winnsen.locker16029.part_first_route_playbook.v1',
    generated_at: new Date().toISOString(),
    status: doorCadClosed && secondPartClosed && thirdPartClosed && gatesClosed ? 'READY_FOR_THREE_PART_INTERFACE_ITERATION' : 'INCOMPLETE',
    scope: {
      product: '16029 locker',
      route: 'part-first scattered SW2020 sheet-metal pilot',
      purpose:
        'Make the door-panel success reusable, prove the front-frame and shelf/cross-panel sheet-metal parts, and define the minimum BOM/interface/gate flow before any supplier release.',
      explicitly_out_of_scope_until_signoff: [
        'supplier DXF release',
        'production drawing release',
        'released BOM',
        'one-shot full cabinet generation',
        'v20 full assembly repair route',
      ],
    },
    door_panel_rule: {
      part_id: 'door_panel_2_12_right_v43',
      expected_parameters: doorPart.expected_parameters || {},
      proven_route:
        'official SketchUseEdge stepped 14.1 mm edge-flange profile + folded top-return SimpleHole2/SketchHole',
      solidworks_route_notes: [
        'Keep the generated SW2020 feature tree as a single door-panel sheet-metal part before assembly.',
        'Use the official EdgeFlange custom profile path; do not return to ordinary boss patch or unsafe profile-line sweeps.',
        'Create the upper hinge 10.2 mm hole on the folded top return with SimpleHole2/SketchHole.',
      ],
      hinge_rule: {
        selected_option: 'B_ADJUST_TOP_LEFT_CORNER_RELIEF',
        upper_hinge_source_center_mm: upperHinge.source_center_mm || null,
        upper_hinge_mapped_center_mm: upperHinge.mapped_center_mm || null,
        upper_hinge_material_status: upperHinge.material_status || null,
        top_left_profile_step_mm: 14.1,
        keep_hinge_datum_collinear: true,
        signoff_boundary:
          'CAD evidence is closed, but supplier DXF release still requires data/locker_16029_door_upper_hinge_engineering_signoff.json.',
      },
      hole_and_flat_validation: {
        target_hole_datums: asArray(doorTargetSpec.target_hole_datums),
        result_hole_cut_count: asNumber(doorResult.holeCutCount),
        flat_dxf_entity_counts: doorFlatCheck.entity_counts || {},
        folded_bbox_mm: bboxFromDoorResult(doorResult),
        flat_bbox_mm: doorFlatCheck.bbox_mm || null,
        required_checks: [
          'SheetMetal feature exists',
          'FlatPattern feature exists',
          'holeCutCount == 4',
          'flat DXF CIRCLE >= 4',
          'flat bbox == 343.4 x 334.4 mm within tolerance',
          'folded bbox == 307 x 298 x 18.2 mm',
        ],
      },
      evidence_files: {
        result_json: PATHS.doorResult,
        flat_check_json: PATHS.doorFlatCheck,
        engineering_confirmation_pack: PATHS.engineeringPack,
        rule_decision: PATHS.doorDecision,
      },
    },
    second_part_verification: {
      selected_part_id: 'front_frame_vertical_left_v43',
      reason:
        'Chosen before shelf panel because it already has clean SW2020 SheetMetal/FlatPattern evidence, 12 source hole cuts, and refreshed flat DXF validation.',
      expected_parameters: frontFramePart.expected_parameters || {},
      result_status: frontFrameResult.status || null,
      has_sheet_metal: frontFrameResult.has_sheet_metal === true,
      has_flat_pattern: frontFrameResult.has_flat_pattern === true,
      flat_exported: frontFrameResult.flat_exported === true,
      step_saved: frontFrameResult.step_saved === true,
      hole_cut_count: asNumber(frontFrameResult.hole_cut_count),
      body_bbox_mm: [
        frontFrameResult.body_bbox_width_mm ?? null,
        frontFrameResult.body_bbox_height_mm ?? null,
        frontFrameResult.body_bbox_depth_mm ?? null,
      ],
      flat_check: {
        status: frontFrameFlatCheck.status || null,
        expected_width_mm: frontFrameFlatCheck.expected_width_mm ?? null,
        expected_height_mm: frontFrameFlatCheck.expected_height_mm ?? null,
        bbox_mm: frontFrameFlatCheck.bbox_mm || null,
        entity_counts: frontFrameFlatCheck.entity_counts || {},
      },
      evidence_files: {
        result_json: PATHS.frontFrameResult,
        flat_check_json: PATHS.frontFrameFlatCheck,
        source_artifacts: frontFramePart.expected_artifacts || {},
      },
    },
    third_part_verification: {
      selected_part_id: 'shelf_cross_panel_left_v43',
      reason:
        'Promoted after the front frame because it completes the current minimum interface trio: door panel, front frame, and shelf/cross panel.',
      evidence_reading_rule:
        'STEP/open evidence is read from shelf_cross_panel_left_v43_result.json; SheetMetal/FlatPattern/flat-DXF evidence is read from shelf_cross_panel_left_v43_flat_export.json and shelf_cross_panel_left_v43_flat_check.json. All JSON reads strip a leading UTF-8 BOM.',
      expected_parameters: shelfPart.expected_parameters || {},
      result_status: shelfResult.status || 'STEP_EXPORT_RESULT',
      opened: shelfResult.opened === true,
      step_saved: shelfResult.step_saved === true,
      step_exists: shelfResult.step_exists === true,
      has_sheet_metal: shelfFlatExport.has_sheet_metal === true,
      has_flat_pattern: shelfFlatExport.has_flat_pattern === true,
      flat_exported: shelfFlatExport.flat_exported === true,
      dxf_exists: shelfFlatExport.dxf_exists === true,
      flat_check: {
        status: shelfFlatCheck.status || null,
        expected_width_mm: shelfFlatCheck.expected_width_mm ?? null,
        expected_height_mm: shelfFlatCheck.expected_height_mm ?? null,
        bbox_mm: shelfFlatCheck.bbox_mm || null,
        entity_counts: shelfFlatCheck.entity_counts || {},
        entity_count_total: shelfFlatCheck.entity_count_total ?? null,
      },
      evidence_files: {
        result_json: PATHS.shelfResult,
        flat_export_json: PATHS.shelfFlatExport,
        flat_check_json: PATHS.shelfFlatCheck,
        source_artifacts: shelfPart.expected_artifacts || {},
      },
    },
    pilot_bom: {
      status: 'pilot_only_not_released',
      rows: [
        {
          part_id: 'door_panel_2_12_right_v43',
          role: 'ordinary door panel',
          quantity_scope: 'pilot row R 2/12 only',
          validation_status: doorCadClosed ? 'cad_closed_pending_engineering_signoff_for_supplier' : 'not_closed',
          artifacts: doorPart.expected_artifacts || {},
        },
        {
          part_id: 'front_frame_vertical_left_v43',
          role: 'front frame vertical / side frame',
          quantity_scope: 'minimum interface pilot only',
          validation_status: secondPartClosed ? 'second_part_sheetmetal_verified' : 'not_closed',
          artifacts: frontFramePart.expected_artifacts || {},
        },
        {
          part_id: 'shelf_cross_panel_left_v43',
          role: 'shelf / cross panel',
          quantity_scope: 'minimum interface pilot only',
          validation_status: thirdPartClosed ? 'third_part_sheetmetal_verified' : 'not_closed',
          artifacts: shelfPart.expected_artifacts || {},
        },
      ],
    },
    assembly_interfaces: {
      status: 'minimum_interface_only_not_full_cabinet',
      assembly_id: contract.minimum_interface_assembly?.id || '',
      purpose: contract.minimum_interface_assembly?.purpose || '',
      relations: asArray(contract.minimum_interface_assembly?.relations),
      expected_artifacts: contract.minimum_interface_assembly?.expected_artifacts || {},
      rule:
        'Assemble only after individual parts pass their own SheetMetal/FlatPattern/flat-DXF checks; do not use assembly constraints to hide part-level geometry defects.',
    },
    minimal_gate_flow: {
      status: 'supplier_release_excluded',
      sequence: [
        'Generate or promote one SW2020 SLDPRT per sheet-metal part.',
        'Export matching STEP and flat DXF for each part.',
        'Parse flat DXF with tools/inspect_16029_flat_dxf.mjs using part-specific expected bbox.',
        'Run tools/verify_16029_part_first_pilot.mjs --out data/locker_16029_part_first_pilot_gate_report.json.',
        'Run tools/verify_16029_part_first_pilot.mjs --strict-artifacts --out data/locker_16029_part_first_pilot_gate_report.strict.json.',
        'Run tools/verify_16029_part_first_route_playbook.mjs for the reusable workflow package.',
      ],
      current_gate_evidence: {
        default_gate_status: defaultGate.status || null,
        default_gate_report: PATHS.defaultGate,
        strict_gate_status: strictGate.status || null,
        strict_gate_report: PATHS.strictGate,
      },
      supplier_release_boundary: {
        supplier_precheck_status: supplierPrecheck.status || null,
        supplier_precheck_blocking_reasons: asArray(supplierPrecheck.blocking_reasons),
        do_not_enter_supplier_release_until:
          'data/locker_16029_door_upper_hinge_engineering_signoff.json exists and supplier precheck is rerun intentionally.',
      },
    },
    engineering_review_questions: [
      'B rule: Is the stepped top-left/top-flange contour acceptable for bend relief, coating, appearance gap, and hinge installation?',
      'Door module: Should the hinge-side datum be made a named reference that every row/handedness variant inherits?',
      'Front frame: Are the 12 holes and 49.3 x 1834 mm flat envelope sufficient for the next interface assembly review, or must the source DXF be re-probed before broader scaling?',
      'Shelf/cross panel: Are the 11 circular holes, 11 arcs, and 574.4 x 480.4 mm flat envelope acceptable for the front-frame locating interface and door swing clearance review?',
      'Assembly: Which interfaces are allowed in the minimum interface assembly, and which must stay out until each scattered part has passed part-level gates?',
      'BOM: What naming, revision, and quantity rules should be used when moving from pilot-only rows to released BOM rows?',
    ],
  }
}

function markdown(playbook) {
  const door = playbook.door_panel_rule
  const second = playbook.second_part_verification
  const third = playbook.third_part_verification
  const gate = playbook.minimal_gate_flow
  const bomRows = playbook.pilot_bom.rows
    .map((row) => `| ${row.part_id} | ${row.role} | ${row.validation_status} |`)
    .join('\n')
  const relations = playbook.assembly_interfaces.relations
    .map((item) => `- ${item.from} -> ${item.to}: ${item.check}`)
    .join('\n')
  const questions = playbook.engineering_review_questions.map((item) => `- ${item}`).join('\n')
  const sequence = gate.sequence.map((item, index) => `${index + 1}. ${item}`).join('\n')

  return `# 16029 散件先行最小工程流程

生成时间：${playbook.generated_at}

## 当前状态

- 流程状态：\`${playbook.status}\`
- 边界：不进入供应商 DXF release，不发布生产图，不生成 released BOM。
- 当前 supplier precheck 状态仅作为边界提示：\`${gate.supplier_release_boundary.supplier_precheck_status}\`

## 门板成功规则

- 零件：\`${door.part_id}\`
- 已证实路线：${door.proven_route}
- 上铰链方案：\`${door.hinge_rule.selected_option}\`
- 上铰链 source center：\`${door.hinge_rule.upper_hinge_source_center_mm?.join(', ')} mm\`
- 上铰链 mapped center：\`${door.hinge_rule.upper_hinge_mapped_center_mm?.join(', ')} mm\`
- 左上轮廓 step：\`${door.hinge_rule.top_left_profile_step_mm} mm\`
- 真实孔数：\`${door.hole_and_flat_validation.result_hole_cut_count}\`
- flat DXF 实体：\`${JSON.stringify(door.hole_and_flat_validation.flat_dxf_entity_counts)}\`
- folded：\`${door.hole_and_flat_validation.folded_bbox_mm.join(' x ')} mm\`
- flat：\`${door.hole_and_flat_validation.flat_bbox_mm.width_mm} x ${door.hole_and_flat_validation.flat_bbox_mm.height_mm} mm\`

## 第二散件验证

- 选择：\`${second.selected_part_id}\`
- 原因：${second.reason}
- SW2020 SheetMetal：\`${second.has_sheet_metal}\`
- SW2020 FlatPattern：\`${second.has_flat_pattern}\`
- flat DXF：\`${second.flat_check.status}\`
- flat bbox：\`${second.flat_check.bbox_mm.width_mm} x ${second.flat_check.bbox_mm.height_mm} mm\`
- 孔特征数：\`${second.hole_cut_count}\`

## 第三散件验证

- 选择：\`${third.selected_part_id}\`
- 原因：${third.reason}
- 证据读取口径：${third.evidence_reading_rule}
- STEP saved：\`${third.step_saved}\`
- SW2020 SheetMetal：\`${third.has_sheet_metal}\`
- SW2020 FlatPattern：\`${third.has_flat_pattern}\`
- flat DXF：\`${third.flat_check.status}\`
- flat bbox：\`${third.flat_check.bbox_mm.width_mm} x ${third.flat_check.bbox_mm.height_mm} mm\`
- flat 实体：\`${JSON.stringify(third.flat_check.entity_counts)}\`

## 试点 BOM

| Part | Role | Status |
|---|---|---|
${bomRows}

## 装配接口

${relations}

规则：${playbook.assembly_interfaces.rule}

## 最小 Gate 流程

${sequence}

当前 gate：
- default：\`${gate.current_gate_evidence.default_gate_status}\`
- strict：\`${gate.current_gate_evidence.strict_gate_status}\`

## 签核边界

${door.hinge_rule.signoff_boundary}

## 结构工程问题

${questions}
`
}

const playbook = buildPlaybook()
for (const path of [PATHS.outJson, PATHS.outMd]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(playbook, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(playbook), 'utf8')
console.log(`${playbook.status}: ${PATHS.outJson}`)
console.log(`markdown: ${PATHS.outMd}`)
