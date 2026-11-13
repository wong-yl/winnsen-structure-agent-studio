import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  routePlaybook: 'data/locker_16029_part_first_route_playbook.json',
  nativeReport: 'data/locker_16029_three_part_native_rerun_report.json',
  deliveryManifest: 'data/locker_16029_part_first_delivery_manifest.json',
  historicalEnvelopeClearance:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_clearance.json',
  outJson: 'data/locker_16029_door_front_gap_review.json',
  outMd: 'docs/16029_door_front_gap_review.md',
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path, fallback = {}) {
  if (!existsSync(abs(path))) return fallback
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function round(value) {
  return Number.isFinite(Number(value)) ? Math.round(Number(value) * 1000) / 1000 : null
}

function component(report, role) {
  return asArray(report.component_evidence?.components).find((item) => {
    const text = `${item.name || ''} ${item.path || ''}`.toLowerCase()
    if (role === 'door') return text.includes('door_panel') || text.includes('door')
    if (role === 'front') return text.includes('front_frame_vertical_left')
    return false
  }) || {}
}

function pairFor(report, roleA, roleB) {
  return asArray(report.pairwise).find((pair) => {
    const roles = asArray(pair.roles)
    return roles.includes(roleA) && roles.includes(roleB)
  }) || null
}

function buildReport() {
  const playbook = readJson(PATHS.routePlaybook)
  const native = readJson(PATHS.nativeReport)
  const manifest = readJson(PATHS.deliveryManifest)
  const historical = readJson(PATHS.historicalEnvelopeClearance)
  const door = component(native, 'door')
  const front = component(native, 'front')
  const doorBox = door.bbox_mm || {}
  const frontBox = front.bbox_mm || {}
  const targetGapMm = asNumber(playbook.second_part_verification?.expected_parameters?.door_gap_mm || 7)
  const currentFrontTxMm = asNumber(front.tx_mm)
  const currentDoorTxMm = asNumber(door.tx_mm)
  const currentGapMm = round(asNumber(doorBox.xmin_mm) - asNumber(frontBox.xmax_mm))
  const currentOverlapMm = currentGapMm < 0 ? round(Math.abs(currentGapMm)) : 0
  const requiredFrontTxForContactMm = round(currentFrontTxMm + currentGapMm)
  const requiredFrontTxForTargetGapMm = round(currentFrontTxMm + currentGapMm - targetGapMm)
  const requiredDoorTxForContactMm = round(currentDoorTxMm - currentGapMm)
  const requiredDoorTxForTargetGapMm = round(currentDoorTxMm - currentGapMm + targetGapMm)
  const frontShiftForContactMm = round(requiredFrontTxForContactMm - currentFrontTxMm)
  const frontShiftForTargetGapMm = round(requiredFrontTxForTargetGapMm - currentFrontTxMm)
  const doorShiftForContactMm = round(requiredDoorTxForContactMm - currentDoorTxMm)
  const doorShiftForTargetGapMm = round(requiredDoorTxForTargetGapMm - currentDoorTxMm)
  const historicalDoorFront = pairFor(historical, 'door_panel_2_12_right_v43', 'front_frame_vertical_left_v43')
  const releaseBoundary = native.release_boundary || {}

  return {
    schema: 'winnsen.locker16029.door_front_gap_review.v1',
    generated_at: new Date().toISOString(),
    status: currentOverlapMm > 0
      ? 'BLOCKER_LOCALIZED_GAP_REVIEW_READY_NOT_CAD_CHANGED'
      : 'NO_DOOR_FRONT_AABB_OVERLAP_IN_CURRENT_NATIVE_RERUN',
    scope: {
      pair: 'door_panel_2_12_right_v43 vs front_frame_vertical_left_v43',
      purpose:
        'Localize the door-front AABB blocker and produce engineering gap/interference review inputs only.',
      non_goals: [
        'do not edit CAD geometry',
        'do not change the assembly placement TSV',
        'do not issue supplier DXF',
        'do not generate full cabinet',
      ],
    },
    source_evidence: {
      route_playbook: PATHS.routePlaybook,
      native_rerun_report: PATHS.nativeReport,
      delivery_manifest: PATHS.deliveryManifest,
      historical_envelope_clearance: PATHS.historicalEnvelopeClearance,
      native_rerun_status: native.status || null,
      manifest_status: manifest.status || null,
    },
    current_native_bbox: {
      target_gap_mm: targetGapMm,
      door: {
        name: door.name || null,
        tx_mm: currentDoorTxMm,
        bbox_mm: doorBox,
      },
      front_frame: {
        name: front.name || null,
        tx_mm: currentFrontTxMm,
        bbox_mm: frontBox,
      },
      measured_x_gap_mm: currentGapMm,
      measured_x_overlap_mm: currentOverlapMm,
      y_overlap_mm: native.native_bbox_clearance?.pairs?.door_to_front_frame?.axes?.y?.overlap_mm ?? null,
      z_overlap_mm: native.native_bbox_clearance?.pairs?.door_to_front_frame?.axes?.z?.overlap_mm ?? null,
      blocker_read:
        currentOverlapMm > 0
          ? 'The front-frame bbox right edge is inside the door bbox by the measured X overlap; bbox cannot clear this pair.'
          : 'No X overlap was measured in the current native bbox evidence.',
    },
    transform_candidates: {
      preferred_fixed_door_adjust_front_frame: {
        current_front_tx_mm: currentFrontTxMm,
        front_tx_for_0mm_contact_mm: requiredFrontTxForContactMm,
        front_shift_for_0mm_contact_mm: frontShiftForContactMm,
        front_tx_for_target_gap_mm: requiredFrontTxForTargetGapMm,
        front_shift_for_target_gap_mm: frontShiftForTargetGapMm,
        target_gap_mm: targetGapMm,
        engineering_read:
          'If the door datum is fixed, move the front-frame component in X- by this amount before rerunning exact interference/gap checks.',
      },
      alternate_fixed_front_frame_adjust_door: {
        current_door_tx_mm: currentDoorTxMm,
        door_tx_for_0mm_contact_mm: requiredDoorTxForContactMm,
        door_shift_for_0mm_contact_mm: doorShiftForContactMm,
        door_tx_for_target_gap_mm: requiredDoorTxForTargetGapMm,
        door_shift_for_target_gap_mm: doorShiftForTargetGapMm,
        target_gap_mm: targetGapMm,
        engineering_read:
          'Use only if the front-frame datum is fixed and the door module datum is allowed to move.',
      },
    },
    historical_reference: historicalDoorFront
      ? {
        status: historicalDoorFront.clearance_conclusion,
        historical_minimum_gap_mm: historicalDoorFront.minimum_separating_gap_mm,
        use:
          'Reference only. The old native-envelope run had a positive gap, but it used envelope geometry and cannot release the current formal parts.',
      }
      : null,
    required_next_check: {
      status: 'EXACT_INTERFERENCE_OR_ENGINEERING_GAP_REVIEW_REQUIRED',
      recommended_order: [
        'Confirm which datum is fixed: door module datum or front-frame vertical datum.',
        `If door is fixed, test front-frame tx=${requiredFrontTxForTargetGapMm} mm for the ${targetGapMm} mm target gap.`,
        'Rerun SW2020 native component enumeration and exact interference, not only AABB.',
        'Only after exact check passes should the interface gate be upgraded from blocker to closed.',
      ],
    },
    release_boundary: {
      bom_status: releaseBoundary.bom_status || null,
      supplier_release_status: releaseBoundary.supplier_release_status || null,
      supplier_precheck_status: releaseBoundary.supplier_precheck_status || null,
      can_issue_supplier_dxf_now: false,
      can_expand_full_cabinet_now: false,
      cad_modified_by_this_report: false,
    },
  }
}

function markdown(report) {
  const fixedDoor = report.transform_candidates.preferred_fixed_door_adjust_front_frame
  const fixedFront = report.transform_candidates.alternate_fixed_front_frame_adjust_door
  const actions = report.required_next_check.recommended_order.map((item, index) => `${index + 1}. ${item}`).join('\n')

  return `# 16029 门板-前框 Gap / Blocker 定位包

生成时间：${report.generated_at}

## 结论

- 状态：\`${report.status}\`
- 当前 X 向间隙：\`${report.current_native_bbox.measured_x_gap_mm} mm\`
- 当前 X 向重叠：\`${report.current_native_bbox.measured_x_overlap_mm} mm\`
- 目标门缝：\`${report.current_native_bbox.target_gap_mm} mm\`
- 结论：当前 placement 下前框右边界进入门板 bbox，不能用 AABB 放行；需要 exact interference 或工程 datum/gap 复核。

## 当前 BBox 读数

| Item | tx mm | xmin | xmax | xlen |
|---|---:|---:|---:|---:|
| Door | ${report.current_native_bbox.door.tx_mm} | ${report.current_native_bbox.door.bbox_mm.xmin_mm} | ${report.current_native_bbox.door.bbox_mm.xmax_mm} | ${report.current_native_bbox.door.bbox_mm.xlen_mm} |
| Front frame | ${report.current_native_bbox.front_frame.tx_mm} | ${report.current_native_bbox.front_frame.bbox_mm.xmin_mm} | ${report.current_native_bbox.front_frame.bbox_mm.xmax_mm} | ${report.current_native_bbox.front_frame.bbox_mm.xlen_mm} |

## 候选偏移

固定门板，移动前框：
- 0mm 接触：front tx = \`${fixedDoor.front_tx_for_0mm_contact_mm} mm\`，相对当前 shift = \`${fixedDoor.front_shift_for_0mm_contact_mm} mm\`
- ${fixedDoor.target_gap_mm}mm 目标门缝：front tx = \`${fixedDoor.front_tx_for_target_gap_mm} mm\`，相对当前 shift = \`${fixedDoor.front_shift_for_target_gap_mm} mm\`

固定前框，移动门板：
- 0mm 接触：door tx = \`${fixedFront.door_tx_for_0mm_contact_mm} mm\`，相对当前 shift = \`${fixedFront.door_shift_for_0mm_contact_mm} mm\`
- ${fixedFront.target_gap_mm}mm 目标门缝：door tx = \`${fixedFront.door_tx_for_target_gap_mm} mm\`，相对当前 shift = \`${fixedFront.door_shift_for_target_gap_mm} mm\`

## 历史参考

- historical envelope gap：\`${report.historical_reference?.historical_minimum_gap_mm ?? 'N/A'} mm\`
- 用途：${report.historical_reference?.use ?? 'N/A'}

## 下一步检查

${actions}

## 边界

- 本报告没有修改 CAD：\`${report.release_boundary.cad_modified_by_this_report}\`
- supplier release：\`${report.release_boundary.supplier_release_status}\`
- supplier precheck：\`${report.release_boundary.supplier_precheck_status}\`
- 不进入完整柜体，不进入供应商 DXF release。
`
}

const report = buildReport()
for (const path of [PATHS.outJson, PATHS.outMd]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(report), 'utf8')
console.log(`${report.status}: ${PATHS.outJson}`)
console.log(`markdown: ${PATHS.outMd}`)
