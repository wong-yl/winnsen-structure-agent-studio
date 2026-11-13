import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  decision: 'data/locker_16029_door_upper_hinge_rule_decision.json',
  supplierPrecheck: 'data/locker_16029_supplier_dxf_precheck.json',
  completionAudit: 'data/locker_16029_part_first_pilot_completion_audit.json',
  doorResult:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_result.json',
  doorFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json',
  doorTargetSpec:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json',
  candidateBReference: 'data/locker_16029_door_upper_hinge_candidate_b_reference.json',
  reliefFeatureDataProbe: 'data/locker_16029_door_upper_hinge_candidate_b_relief_featuredata_probe.json',
  officialProfileProbe: 'data/locker_16029_door_upper_hinge_official_profile_probe.json',
  engineeringSignoff: 'data/locker_16029_door_upper_hinge_engineering_signoff.json',
  outJson: 'data/locker_16029_door_upper_hinge_engineering_confirmation_pack.json',
  outMd: 'docs/16029_door_upper_hinge_engineering_confirmation_pack.md',
  outSignoffTemplate: 'data/locker_16029_door_upper_hinge_engineering_signoff.template.json',
}

const OPTIONAL_INPUTS = new Set(['engineeringSignoff'])

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

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function featureTypes(json) {
  return asArray(json?.features).map((feature) => String(feature.type || ''))
}

function holeCutCount(json) {
  if (Object.hasOwn(json || {}, 'holeCutCount')) return asNumber(json.holeCutCount)
  if (Object.hasOwn(json || {}, 'hole_cut_count')) return asNumber(json.hole_cut_count)
  return 0
}

function roundMm(value) {
  return Math.round(asNumber(value) * 1000) / 1000
}

function bboxArrayFromDoorResult(result) {
  return [
    result?.foldedEnvelopeMeasuredWidthMm ?? null,
    result?.foldedEnvelopeMeasuredHeightMm ?? null,
    result?.foldedEnvelopeMeasuredDepthMm ?? null,
  ]
}

function makeSignoffTemplate(pack) {
  return {
    schema: 'winnsen.locker16029.door_upper_hinge_engineering_signoff.v1',
    status: 'TEMPLATE_NOT_SIGNED',
    created_from_pack: PATHS.outJson,
    save_signed_copy_as: PATHS.engineeringSignoff,
    part: 'door_panel_2_12_right_v43',
    scope: 'upper hinge 10.2 mm hole rule only',
    selected_option: '',
    allowed_selected_options: ['B_ADJUST_TOP_LEFT_CORNER_RELIEF', 'C_POST_PROCESS_UPPER_HINGE_HOLE'],
    accepted: false,
    signer: {
      structure_engineer: '',
      process_engineer: '',
      supplier_or_manufacturing_reviewer: '',
      signed_at: '',
    },
    required_statements_for_b: {
      keep_hinge_datum: 'upper/lower 10.2 mm hinge holes remain collinear at source X -143.5 mm',
      corner_relief_rule:
        'top-left hinge-side flat strip edge must be kept at mapped X <= -12.8 mm; mapped X <= -8.9 mm is only the minimum full-circle limit',
      manufacturability_confirmed:
        'bend relief, coating allowance, appearance gap, and hinge/door assembly are accepted for prototype verification',
      cad_acceptance:
        'SW2020 result must prove SheetMetal + FlatPattern, flat 343.4 x 334.4 mm, folded 307 x 298 x 18.2 mm, 4/4 true cut features, and >=4 DXF circle entities',
    },
    required_statements_for_c: {
      process_hole: 'upper hinge 10.2 mm hole is explicitly post-process drilling/punching, not a supplier flat-DXF cut feature',
      release_limit:
        'current door flat DXF must not be described as 4/4 production supplier DXF; BOM/process sheet must carry the post-process operation',
      goal_limit: 'this option does not complete the current 4/4 true cut feature objective unless the objective is formally changed',
    },
    reference_to_review_before_b: pack.candidate_b_reference?.reference_dxf || '',
    evidence_to_attach_after_b: pack.acceptance_after_confirmed_rule.evidence_files,
    notes: '',
  }
}

function buildPack() {
  const loaded = Object.fromEntries(
    Object.entries(PATHS)
      .filter(([key]) => !key.startsWith('out'))
      .map(([key, path]) => [key, tryReadJson(path)]),
  )

  const decision = loaded.decision.value || {}
  const supplier = loaded.supplierPrecheck.value || {}
  const audit = loaded.completionAudit.value || {}
  const doorResult = loaded.doorResult.value || {}
  const doorFlatCheck = loaded.doorFlatCheck.value || {}
  const targetSpec = loaded.doorTargetSpec.value || {}
  const candidateBReference = loaded.candidateBReference.value || {}
  const reliefFeatureDataProbe = loaded.reliefFeatureDataProbe.value || {}
  const officialProfileProbe = loaded.officialProfileProbe.value || {}
  const upperHinge = asArray(decision.holes).find((hole) => hole.role === 'hinge_upper') || {}
  const lowerHinge = asArray(decision.holes).find((hole) => hole.role === 'hinge_lower') || {}
  const optionB = asArray(decision.decision_options).find((option) => option.id === 'B_ADJUST_TOP_LEFT_CORNER_RELIEF') || {}
  const optionC = asArray(decision.decision_options).find((option) => option.id === 'C_POST_PROCESS_UPPER_HINGE_HOLE') || {}
  const optionA = asArray(decision.decision_options).find((option) => option.id === 'A_MOVE_UPPER_HINGE_HOLE') || {}
  const gold = decision.gold_contour_audit || {}

  const requiredInputsLoaded = Object.entries(loaded)
    .filter(([key]) => !OPTIONAL_INPUTS.has(key))
    .every(([, item]) => item.ok)
  const doorCuts = holeCutCount(doorResult)
  const dxfCircles = asNumber(doorFlatCheck.entity_counts?.CIRCLE)
  const featureTypeList = featureTypes(doorResult)
  const hasSheetMetal = featureTypeList.includes('SheetMetal')
  const hasFlatPattern = featureTypeList.includes('FlatPattern')
  const foldedBBox = bboxArrayFromDoorResult(doorResult)
  const flatOk =
    doorFlatCheck.status === 'PASS' &&
    roundMm(doorFlatCheck.bbox_mm?.width_mm) === 343.4 &&
    roundMm(doorFlatCheck.bbox_mm?.height_mm) === 334.4
  const foldedOk =
    roundMm(foldedBBox[0]) === 307 &&
    roundMm(foldedBBox[1]) === 298 &&
    roundMm(foldedBBox[2]) === 18.2
  const sourceHingeCollinear =
    lowerHinge.source_center_mm?.[0] === upperHinge.source_center_mm?.[0] &&
    lowerHinge.diameter_mm === upperHinge.diameter_mm
  const misleadingReleaseFlag = doorResult.releaseBlockerClosed === true && doorCuts !== 4
  const cadClosedForTrueCut =
    hasSheetMetal &&
    hasFlatPattern &&
    doorCuts === 4 &&
    dxfCircles >= 4 &&
    flatOk &&
    foldedOk &&
    upperHinge.material_status === 'INSIDE_CURRENT_FLAT_PATTERN'
  const supplierReady = supplier.status === 'READY'

  return {
    schema: 'winnsen.locker16029.door_upper_hinge_engineering_confirmation_pack.v1',
    generated_at: new Date().toISOString(),
    status: requiredInputsLoaded
      ? cadClosedForTrueCut
        ? 'ENGINEERING_CONFIRMATION_PACK_READY_CAD_CLOSED_RULE_NOT_SIGNED'
        : 'ENGINEERING_CONFIRMATION_PACK_READY_RULE_NOT_SIGNED'
      : 'ENGINEERING_CONFIRMATION_PACK_INCOMPLETE_INPUTS',
    ready_for_engineering_confirmation: requiredInputsLoaded,
    ready_for_supplier_dxf_review: supplierReady,
    can_issue_supplier_dxf_now: supplierReady,
    scope: {
      product: '16029 locker',
      route: 'part-first scattered sheet-metal pilot',
      part: 'door_panel_2_12_right_v43',
      issue: cadClosedForTrueCut
        ? 'upper hinge 10.2 mm true-cut CAD evidence is closed; supplier release is waiting for structure engineering signoff'
        : 'upper hinge 10.2 mm hole maps outside the current flat-pattern material boundary',
      boundary: cadClosedForTrueCut
        ? 'This pack proves the CAD side of option B for the pilot door. It does not replace the required structure engineering signoff before supplier DXF issue.'
        : 'This pack closes the engineering decision input, not the final CAD objective. Final completion still requires 4/4 true SolidWorks cut features unless option C is separately accepted as a process change.',
    },
    source_paths: PATHS,
    input_load_status: Object.fromEntries(
      Object.entries(loaded).map(([key, item]) => [key, { ok: item.ok, error: item.error }]),
    ),
    current_evidence: {
      formal_door_status: doorResult.status || null,
      formal_door_release_blocker_closed_field: doorResult.releaseBlockerClosed ?? null,
      formal_door_release_blocker_field_misleading: misleadingReleaseFlag,
      formal_door_release_note: doorResult.releaseBlockerNote || '',
      has_sheetmetal: hasSheetMetal,
      has_flatpattern: hasFlatPattern,
      feature_types: featureTypeList,
      folded_bbox_mm: foldedBBox,
      flat_bbox_mm: doorFlatCheck.bbox_mm || null,
      door_hole_datum_count: asArray(doorResult.holeDatums).length || asArray(targetSpec.target_hole_datums).length,
      door_hole_cut_count: doorCuts,
      door_flat_dxf_entity_counts: doorFlatCheck.entity_counts || {},
      door_flat_dxf_circle_count: dxfCircles,
      cad_closed_for_true_cut_supplier_precheck: cadClosedForTrueCut,
      remaining_release_blocker: supplierReady ? '' : asArray(supplier.blocking_reasons).join('; '),
      supplier_precheck_status: supplier.status || null,
      supplier_precheck_blocking_reasons: asArray(supplier.blocking_reasons),
    },
    rule_conflict: {
      upper_hinge_source_center_mm: upperHinge.source_center_mm || null,
      upper_hinge_mapped_center_mm: upperHinge.mapped_center_mm || null,
      upper_hinge_diameter_mm: upperHinge.diameter_mm ?? null,
      upper_hinge_material_status: upperHinge.material_status || null,
      lower_hinge_source_center_mm: lowerHinge.source_center_mm || null,
      hinge_source_x_collinear_in_current_rule: sourceHingeCollinear,
      gold_supported_candidate_b: gold.status === 'B_CORNER_RELIEF_RULE_SUPPORTED_BY_GOLD_DXF_D_OPTION_NOT_PRIMARY',
      current_top_strip_edge_x_mm: gold.formal_current_top_strip_edge_x_mm ?? null,
      expected_top_strip_edge_x_if_gold_clearance_is_preserved_mm:
        gold.expected_top_strip_edge_x_if_gold_clearance_is_preserved_mm ?? null,
      required_top_strip_edge_x_for_full_circle_inside_mm:
        gold.required_top_strip_edge_x_for_full_circle_inside_mm ?? null,
      missing_material_vs_gold_rule_mm: gold.missing_material_vs_gold_rule_mm ?? null,
      missing_material_vs_full_circle_minimum_mm: gold.missing_material_vs_full_circle_minimum_mm ?? null,
    },
    engineering_decision_request: {
      preferred_option: {
        id: 'B_ADJUST_TOP_LEFT_CORNER_RELIEF',
        status: optionB.status || '',
        action: optionB.action || '',
        reason:
          'Preserves the upper/lower hinge datum and matches the gold DXF evidence that the blocker is excessive top-left corner relief, not a simple hole datum move.',
        required_confirmation:
          'Confirm whether the top-left corner relief/flange flat contour may restore at least 14.1 mm of hinge-side strip, with the engineering target edge X <= -12.8 mm in current mapped flat coordinates.',
      },
      fallback_option: {
        id: 'C_POST_PROCESS_UPPER_HINGE_HOLE',
        status: optionC.status || '',
        action: optionC.action || '',
        release_limit:
          'Allowed only with explicit structure/process signoff. It keeps supplier DXF precheck blocked for the 4/4 true-cut objective.',
      },
      not_recommended_default: {
        id: 'A_MOVE_UPPER_HINGE_HOLE',
        status: optionA.status || '',
        action: optionA.action || '',
        reason: 'Moving only the upper hole breaks hinge-hole collinearity unless hinge axis approval is documented.',
      },
    },
    candidate_b_reference: {
      status: candidateBReference.status || 'REFERENCE_NOT_GENERATED',
      machine_report: candidateBReference.output_paths?.machine_report || PATHS.candidateBReference,
      human_report: candidateBReference.output_paths?.human_report || '',
      reference_dxf: candidateBReference.output_paths?.reference_dxf || '',
      release_use: candidateBReference.release_use || 'NOT_FOR_SUPPLIER_RELEASE_NOT_CAD_EVIDENCE',
      reference_flat: candidateBReference.reference_flat
        ? {
            bbox_mm: candidateBReference.reference_flat.bbox_mm || null,
            entity_counts: candidateBReference.reference_flat.entity_counts || null,
          }
        : null,
      boundary:
        'Reference only for engineering signoff and manual SW2020 rebuild. It is not supplier DXF release evidence and must not replace formal SLDPRT cut-feature proof.',
    },
    engineering_signoff_status: {
      signed_file: PATHS.engineeringSignoff,
      signed_file_exists_and_parses: loaded.engineeringSignoff.ok,
      status: loaded.engineeringSignoff.value?.status || 'NOT_SIGNED_FILE_MISSING',
      selected_option: loaded.engineeringSignoff.value?.selected_option || '',
      accepted: loaded.engineeringSignoff.value?.accepted === true,
      signer: loaded.engineeringSignoff.value?.signer || null,
      template: PATHS.outSignoffTemplate,
      boundary:
        'The template is not a signoff. A signed copy must be saved at data/locker_16029_door_upper_hinge_engineering_signoff.json before true-cut supplier precheck can become READY.',
    },
    blocked_automation_routes: {
      status: decision.true_sheetmetal_feature_probe?.status || '',
      routes_tested: asArray(decision.true_sheetmetal_feature_probe?.routes_tested),
      crash_sensitive_route: 'edge_flange_custom_profile_sketch_line_write_disabled',
      edge_flange_relief_featuredata_probe: {
        status: reliefFeatureDataProbe.status || 'PROBE_REPORT_NOT_LOADED',
        source: reliefFeatureDataProbe.implementation?.source || '',
        compile_status: reliefFeatureDataProbe.implementation?.build_status || '',
        runtime_status: reliefFeatureDataProbe.solidworks_startup_diagnostic?.com_result || '',
        runtime_blocker: [
          reliefFeatureDataProbe.solidworks_startup_diagnostic?.hresult,
          reliefFeatureDataProbe.solidworks_startup_diagnostic?.error,
        ]
          .filter(Boolean)
          .join(' '),
        formal_artifact_impact: reliefFeatureDataProbe.formal_artifact_impact || null,
        machine_report: PATHS.reliefFeatureDataProbe,
      },
      edge_flange_official_profile_probe: {
        status: officialProfileProbe.status || 'PROBE_REPORT_NOT_LOADED',
        source: officialProfileProbe.implementation?.source || '',
        build_status: officialProfileProbe.implementation?.build_status || '',
        best_cut_result:
          asArray(officialProfileProbe.variants).find((item) => item.id === 'step_global_left_14p1_folded_top_return_simplehole_v89') ||
          asArray(officialProfileProbe.variants).find((item) => item.id === 'step_global_left_14p1_folded_top_return_fallback') ||
          null,
        cut_variant_probe: officialProfileProbe.cut_variant_probe || null,
        next_cad_path: officialProfileProbe.engineering_conclusion?.next_cad_path || '',
        machine_report: PATHS.officialProfileProbe,
      },
      required_next_cad_path: cadClosedForTrueCut
        ? 'No additional CAD route is required for the pilot 4/4 true-cut objective. The next release action is signed B engineering approval and supplier precheck rerun.'
        : officialProfileProbe.engineering_conclusion?.next_cad_path ||
          'Continue only on the official SketchUseEdge custom EdgeFlange profile route: make the fourth folded top-return upper hinge sketch become a real CutFeature, or record the equivalent manual SW2020 cut macro. Promote only if the formal result proves all acceptance checks.',
    },
    acceptance_after_confirmed_rule: {
      required: true,
      checks: [
        'SW2020 feature tree contains SheetMetal and FlatPattern',
        'formal door result holeCutCount is 4',
        'flat DXF has at least 4 CIRCLE entities',
        'flat bbox remains 343.4 x 334.4 mm within existing tolerance',
        'folded bbox remains 307 x 298 x 18.2 mm',
        'default part-first gate PASS',
        'strict part-first gate PASS',
        'supplier DXF precheck READY',
      ],
      evidence_files: [
        PATHS.doorResult,
        PATHS.doorFlatCheck,
        'data/locker_16029_part_first_pilot_gate_report.json',
        'data/locker_16029_part_first_pilot_gate_report.strict.json',
        PATHS.supplierPrecheck,
      ],
    },
    audit_notes: {
      completion_audit_status: audit.overall_status || null,
      do_not_promote_current_formal_door:
        !cadClosedForTrueCut,
      reason_current_formal_door_not_release_candidate_for_supplier_holes: cadClosedForTrueCut
        ? 'CAD evidence is release-valid for the pilot hole objective; supplier issue remains blocked only by missing B engineering signoff.'
        : 'The current formal door is valid as flat/folded sheet-metal envelope evidence, but not as supplier 4-hole manufacturing DXF evidence.',
    },
  }
}

function markdown(pack, signoffTemplatePath) {
  const evidence = pack.current_evidence
  const rule = pack.rule_conflict
  const reference = pack.candidate_b_reference || {}
  const signoff = pack.engineering_signoff_status || {}
  const blockers = evidence.supplier_precheck_blocking_reasons.map((item) => `- \`${item}\``).join('\n')
  const routes = pack.blocked_automation_routes.routes_tested.map((item) => `- \`${item}\``).join('\n')
  const checks = pack.acceptance_after_confirmed_rule.checks.map((item) => `- ${item}`).join('\n')
  const officialProfile = pack.blocked_automation_routes.edge_flange_official_profile_probe || {}
  const cutProbe = officialProfile.cut_variant_probe || {}
  const cadClosed = evidence.cad_closed_for_true_cut_supplier_precheck === true
  const conclusion = cadClosed
    ? 'CAD 侧已闭合：正式门板已有 4/4 真实孔特征，SheetMetal/FlatPattern 保留，展开与折弯包络均保持目标值。当前不能发供应商 DXF 的唯一原因是缺少 B 规则结构工程签核。'
    : 'CAD 侧尚未闭合：正式门板不能作为 4 孔供应商生产 DXF 证据。'
  const nextAction = cadClosed
    ? '让结构工程签署 B_ADJUST_TOP_LEFT_CORNER_RELIEF，并保存为 data/locker_16029_door_upper_hinge_engineering_signoff.json，然后重跑 supplier DXF precheck。'
    : '继续官方 SketchUseEdge 轮廓路线，直到形成 4/4 真实 cut features，再重跑 flat/default/strict/supplier gates。'
  const routeNote = cadClosed
    ? '官方 SketchUseEdge stepped 14.1 mm edge-flange profile + folded top-return SimpleHole2/SketchHole'
    : pack.blocked_automation_routes.required_next_cad_path

  return `# 16029 门板上铰链 10.2 mm 孔工程确认包

生成时间：${pack.generated_at}

## 当前结论

${conclusion}

- 正式门板状态：\`${evidence.formal_door_status}\`
- SW 特征：SheetMetal=${evidence.has_sheetmetal}，FlatPattern=${evidence.has_flatpattern}
- 折弯包络：\`${evidence.folded_bbox_mm.join(' x ')} mm\`
- 展开包络：\`${evidence.flat_bbox_mm?.width_mm ?? 'unknown'} x ${evidence.flat_bbox_mm?.height_mm ?? 'unknown'} mm\`
- 目标孔 datum：\`${evidence.door_hole_datum_count}\`
- 真实 cut features：\`${evidence.door_hole_cut_count}\`
- flat DXF 圆孔实体：\`${evidence.door_flat_dxf_circle_count}\`
- supplier DXF precheck：\`${evidence.supplier_precheck_status}\`

## 剩余阻塞

${blockers || '- none'}

模板本身不算签核。正式签核文件必须保存为：
\`${signoff.signed_file || 'data/locker_16029_door_upper_hinge_engineering_signoff.json'}\`

## B 规则确认点

- 选择项：\`B_ADJUST_TOP_LEFT_CORNER_RELIEF\`
- 上铰链孔 source center：\`${rule.upper_hinge_source_center_mm?.join(', ') ?? 'unknown'} mm\`
- 上铰链孔 mapped center：\`${rule.upper_hinge_mapped_center_mm?.join(', ') ?? 'unknown'} mm\`
- 当前材料状态：\`${rule.upper_hinge_material_status}\`
- 上下两个 10.2 mm 铰链孔保持同一 source X datum。
- 通过左上角 / 顶部法兰展开轮廓补料，使上铰链孔落在当前展开材料内。

## CAD 证据

- 路线：${routeNote}
- 门板 result：\`${PATHS.doorResult}\`
- flat DXF 检查：\`${PATHS.doorFlatCheck}\`
- default gate：\`data/locker_16029_part_first_pilot_gate_report.json\`
- strict gate：\`data/locker_16029_part_first_pilot_gate_report.strict.json\`
- supplier precheck：\`${PATHS.supplierPrecheck}\`

## 签核后验收

${checks}

## 下一步

${nextAction}
`
  const cutEvidence = asArray(cutProbe.evidence)
    .map((item) => `- ${item}`)
    .join('\n')

  return `# 16029 门板上铰链 10.2 mm 孔工程确认包

生成时间：${pack.generated_at}

## 当前结论

当前门板可以作为“散件先行”的钣金轮廓/折弯包络证据，但不能作为供应商 4 孔生产 DXF 发出。

- 正式门板状态：\`${evidence.formal_door_status}\`
- SW 特征：SheetMetal=${evidence.has_sheetmetal}，FlatPattern=${evidence.has_flatpattern}
- 折弯包络：\`${evidence.folded_bbox_mm.join(' x ')} mm\`
- 展开包络：\`${evidence.flat_bbox_mm?.width_mm ?? 'unknown'} x ${evidence.flat_bbox_mm?.height_mm ?? 'unknown'} mm\`
- 目标孔 datum：\`${evidence.door_hole_datum_count}\`
- 真实 cut features：\`${evidence.door_hole_cut_count}\`
- flat DXF 圆孔实体：\`${evidence.door_flat_dxf_circle_count}\`
- 供应商 DXF 预检：\`${evidence.supplier_precheck_status}\`

注意：正式门板 result 里 \`releaseBlockerClosed=true\` 只代表 flat/folded 包络关闭；本包按供应商孔位口径重新判定为未关闭。

## 阻塞原因

${blockers || '- 当前没有读取到 blocking reasons'}

## 要结构工程确认的选择

首选 **B：保持铰链 datum 不动，修正左上角 corner relief / 顶部法兰展开轮廓**。

- 上铰链孔 source center：\`${rule.upper_hinge_source_center_mm?.join(', ') ?? 'unknown'} mm\`
- 上铰链孔 mapped center：\`${rule.upper_hinge_mapped_center_mm?.join(', ') ?? 'unknown'} mm\`
- 当前材料状态：\`${rule.upper_hinge_material_status}\`
- 当前左上顶部料带边：\`X=${rule.current_top_strip_edge_x_mm} mm\`
- gold 规则目标边：\`X<=${rule.expected_top_strip_edge_x_if_gold_clearance_is_preserved_mm} mm\`
- 完整容纳 10.2 mm 圆孔的最低边界：\`X<=${rule.required_top_strip_edge_x_for_full_circle_inside_mm} mm\`
- 当前相对 gold 规则少料：\`${rule.missing_material_vs_gold_rule_mm} mm\`

这个选择的工程含义：上下两个 10.2 mm 铰链孔继续共线，不默认移动单个上孔；只改左上角展开轮廓/角部避让规则。

可接受但不完成当前 4/4 cut 目标的 fallback 是 **C：上铰链孔定义为后加工孔**。如果选 C，必须有结构/工艺签字，并且当前 flat DXF 不能标成 4/4 供应商生产 DXF。

不建议默认走 **A：只移动上铰链孔**，因为这会破坏上下铰链孔共线，除非铰链轴和安装支架另有书面批准。

## 已排除的自动化路径

${routes || '- 当前没有读取到 routes_tested'}

\`edge_flange_custom_profile_sketch_line_write\` 已按崩溃风险禁用，不再作为无人值守验证路径。

## B 参考轮廓

- 状态：\`${reference.status || 'REFERENCE_NOT_GENERATED'}\`
- 参考 DXF：\`${reference.reference_dxf || 'not generated'}\`
- 用途边界：只用于工程签字和 SW2020 手工重建对照；不能作为供应商 DXF，也不能替代正式 SLDPRT cut-feature 证据。

## 签字文件门槛

- 签字模板：\`${signoff.template || signoffTemplatePath}\`
- 正式签字文件：\`${signoff.signed_file || 'data/locker_16029_door_upper_hinge_engineering_signoff.json'}\`
- 当前签字状态：\`${signoff.status || 'NOT_SIGNED_FILE_MISSING'}\`

模板本身不算签字。若选择 B，必须把已接受的签字副本保存为正式签字文件后，supplier DXF precheck 才能尝试进入 READY。

## B 方案签字后必须证明

${checks}

签字模板：\`${signoffTemplatePath}\`

## 最小验证路径

1. 结构工程先在签字模板中选择 B 或 C。
2. 如果选择 B，只允许基于记录过的 SW2020 手工/宏操作生成新门板，不能用普通 boss patch 冒充钣金法兰。
3. 新门板生成后，重新导出 flat DXF，并跑 default gate、strict gate、supplier DXF precheck。
4. 只有 supplier DXF precheck 变为 READY，才可进入供应商 DXF 审查。
`
}

const pack = buildPack()
const signoffTemplate = makeSignoffTemplate(pack)

for (const path of [PATHS.outJson, PATHS.outMd, PATHS.outSignoffTemplate]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(pack, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outSignoffTemplate), `${JSON.stringify(signoffTemplate, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(pack, PATHS.outSignoffTemplate), 'utf8')

console.log(`${pack.status}: ${PATHS.outJson}`)
console.log(`signoff_template: ${PATHS.outSignoffTemplate}`)
console.log(`human_report: ${PATHS.outMd}`)
