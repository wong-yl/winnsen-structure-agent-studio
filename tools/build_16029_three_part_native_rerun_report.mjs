import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const BASE =
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly'

const PATHS = {
  routePlaybook: 'data/locker_16029_part_first_route_playbook.json',
  routeGate: 'data/locker_16029_part_first_route_playbook_gate.json',
  previousInterfaceGate: 'data/locker_16029_three_part_interface_gate.json',
  previousInspect: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_inspect.json`,
  build: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_build.json`,
  components: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_components.json`,
  clearance: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_clearance.json`,
  assembly: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun.SLDASM`,
  step: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun.step`,
  outJson: 'data/locker_16029_three_part_native_rerun_report.json',
  outMd: 'docs/16029_three_part_native_rerun_report.md',
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

function pairFor(clearance, roleA, roleB) {
  return asArray(clearance.pairwise).find((pair) => {
    const roles = asArray(pair.roles)
    return roles.includes(roleA) && roles.includes(roleB)
  }) || null
}

function componentSummary(components) {
  return asArray(components.components).map((component) => ({
    name: component.name,
    path: String(component.path || '').replaceAll('\\', '/'),
    is_suppressed: component.is_suppressed === true,
    is_hidden: component.is_hidden === true,
    tx_mm: round(component.transform?.tx_mm),
    ty_mm: round(component.transform?.ty_mm),
    tz_mm: round(component.transform?.tz_mm),
    bbox_mm: component.box || null,
  }))
}

function partStatus(playbook, partId) {
  return asArray(playbook.pilot_bom?.rows).find((row) => row.part_id === partId)?.validation_status || null
}

function buildReport() {
  const playbook = readJson(PATHS.routePlaybook)
  const routeGate = readJson(PATHS.routeGate)
  const previousInterfaceGate = readJson(PATHS.previousInterfaceGate)
  const previousInspect = readJson(PATHS.previousInspect)
  const build = readJson(PATHS.build)
  const components = readJson(PATHS.components)
  const clearance = readJson(PATHS.clearance)
  const doorFront = pairFor(clearance, 'door_panel_2_12_right_v43', 'front_frame_vertical_left_v43')
  const shelfDoor = pairFor(clearance, 'shelf_cross_panel_left_v43', 'door_panel_2_12_right_v43')
  const shelfFront = pairFor(clearance, 'shelf_cross_panel_left_v43', 'front_frame_vertical_left_v43')
  const openedAndEnumerated =
    components.opened === true &&
    components.rebuilt === true &&
    asNumber(components.component_count) === 3
  const bboxRerunComplete =
    clearance.opened === true &&
    clearance.rebuilt === true &&
    asNumber(clearance.component_count) === 3 &&
    asNumber(clearance.pair_count) === 3
  const hasDoorFrontBlocker =
    doorFront?.aabb_intersects === true &&
    String(doorFront.clearance_conclusion || '').includes('EXACT_SOLID_INTERFERENCE_CHECK')
  const status = openedAndEnumerated && bboxRerunComplete && hasDoorFrontBlocker
    ? 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER'
    : 'SW2020_NATIVE_RERUN_INCOMPLETE'

  return {
    schema: 'winnsen.locker16029.three_part_native_rerun_report.v1',
    generated_at: new Date().toISOString(),
    status,
    scope: {
      assembly_id: 'door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun',
      included_parts: [
        'door_panel_2_12_right_v43',
        'front_frame_vertical_left_v43',
        'shelf_cross_panel_left_v43',
      ],
      route: 'part-first scattered SW2020 formal pilot SLDPRTs only',
      explicit_non_goals: [
        'do not edit the three part geometries',
        'do not generate full cabinet',
        'do not issue supplier DXF',
        'do not create released BOM',
      ],
    },
    source_route: {
      route_playbook: PATHS.routePlaybook,
      route_status: playbook.status || null,
      route_gate: PATHS.routeGate,
      route_gate_status: routeGate.status || null,
      previous_interface_gate: PATHS.previousInterfaceGate,
      previous_interface_gate_status: previousInterfaceGate.status || null,
    },
    inspect_failure_resolved: {
      previous_inspect_json: PATHS.previousInspect,
      previous_opened: previousInspect.opened === true,
      previous_component_count: asNumber(previousInspect.component_count),
      rerun_components_json: PATHS.components,
      rerun_opened: components.opened === true,
      rerun_rebuilt: components.rebuilt === true,
      rerun_component_count: asNumber(components.component_count),
      conclusion: openedAndEnumerated
        ? 'RESOLVED_CURRENT_FORMAL_SLDASM_OPENS_REBUILDS_AND_ENUMERATES_THREE_COMPONENTS'
        : 'NOT_RESOLVED',
    },
    build_evidence: {
      build_json: PATHS.build,
      assembly_path: PATHS.assembly,
      step_path: PATHS.step,
      assembly_exists: existsSync(abs(PATHS.assembly)),
      step_exists: existsSync(abs(PATHS.step)),
      build_status: build.build_status || null,
      saved: build.saved === true,
      step_saved: build.step_saved === true,
      placement_count: asNumber(build.placement_count),
      reference_count: asNumber(build.reference_count),
      components_added: asArray(build.components).filter((component) => component.added === true).length,
      component_open_warnings_or_errors: asArray(build.components)
        .filter((component) => asNumber(component.open_errors) > 0 || asNumber(component.open_warnings) > 0 || component.error)
        .map((component) => ({
          role: component.role,
          open_errors: asNumber(component.open_errors),
          open_warnings: asNumber(component.open_warnings),
          error: component.error || '',
        })),
    },
    component_evidence: {
      components_json: PATHS.components,
      opened: components.opened === true,
      rebuilt: components.rebuilt === true,
      component_count: asNumber(components.component_count),
      enumeration_mode: components.enumeration_mode || '',
      open_errors: asNumber(components.open_errors),
      open_warnings: asNumber(components.open_warnings),
      parts: {
        door_panel_2_12_right_v43: partStatus(playbook, 'door_panel_2_12_right_v43'),
        front_frame_vertical_left_v43: partStatus(playbook, 'front_frame_vertical_left_v43'),
        shelf_cross_panel_left_v43: partStatus(playbook, 'shelf_cross_panel_left_v43'),
      },
      components: componentSummary(components),
    },
    native_bbox_clearance: {
      clearance_json: PATHS.clearance,
      status: clearance.status || null,
      opened: clearance.opened === true,
      rebuilt: clearance.rebuilt === true,
      component_count: asNumber(clearance.component_count),
      pair_count: asNumber(clearance.pair_count),
      separated_pair_count: asNumber(clearance.separated_pair_count),
      overlapping_pair_count: asNumber(clearance.overlapping_pair_count),
      blocking_reasons: asArray(clearance.blocking_reasons),
      pairs: {
        door_to_front_frame: doorFront,
        shelf_to_door: shelfDoor,
        shelf_to_front_frame: shelfFront,
      },
      conclusion: hasDoorFrontBlocker
        ? 'TWO_PAIRS_AABB_SEPARATED_DOOR_FRONT_REQUIRES_EXACT_INTERFERENCE_OR_DATUM_GAP_REVIEW'
        : 'NO_AABB_BLOCKER_RECORDED',
    },
    release_boundary: {
      bom_status: playbook.pilot_bom?.status || null,
      supplier_release_status: playbook.minimal_gate_flow?.status || null,
      supplier_precheck_status: playbook.minimal_gate_flow?.supplier_release_boundary?.supplier_precheck_status || null,
      can_issue_supplier_dxf_now: false,
      can_expand_to_full_cabinet_now: false,
    },
    next_required_action: hasDoorFrontBlocker
      ? 'Run exact SolidWorks interference or engineering datum/gap review for door_panel_2_12_right_v43 vs front_frame_vertical_left_v43. Do not treat bbox overlap as pass.'
      : 'Review whether native bbox clearance is sufficient or exact interference is still required by engineering policy.',
  }
}

function markdown(report) {
  const pairs = Object.entries(report.native_bbox_clearance.pairs)
    .map(([name, pair]) => {
      const gap = pair?.minimum_separating_gap_mm ?? 0
      const conclusion = pair?.clearance_conclusion || 'missing'
      return `| ${name} | ${pair?.aabb_intersects === true} | ${gap} | ${conclusion} |`
    })
    .join('\n')
  const components = report.component_evidence.components
    .map((component) => `| ${component.name} | ${component.tx_mm}, ${component.ty_mm}, ${component.tz_mm} | ${component.bbox_mm?.xlen_mm ?? ''} x ${component.bbox_mm?.ylen_mm ?? ''} x ${component.bbox_mm?.zlen_mm ?? ''} |`)
    .join('\n')

  return `# 16029 三散件 SW2020 Native Rerun 报告

生成时间：${report.generated_at}

## 结论

- 状态：\`${report.status}\`
- inspect 修复：\`${report.inspect_failure_resolved.conclusion}\`
- clearance 结论：\`${report.native_bbox_clearance.conclusion}\`
- 下一步：${report.next_required_action}

## 构建与枚举

- build：\`${report.build_evidence.build_status}\`，saved=\`${report.build_evidence.saved}\`，step_saved=\`${report.build_evidence.step_saved}\`
- assembly：\`${report.build_evidence.assembly_path}\`
- components：opened=\`${report.component_evidence.opened}\`，rebuilt=\`${report.component_evidence.rebuilt}\`，count=\`${report.component_evidence.component_count}\`
- enumeration：\`${report.component_evidence.enumeration_mode}\`

| Component | Transform tx,ty,tz mm | Native bbox mm |
|---|---:|---:|
${components}

## 三对接口 BBox

| Pair | AABB overlaps | Min separating gap mm | Conclusion |
|---|---:|---:|---|
${pairs}

## 边界

- BOM：\`${report.release_boundary.bom_status}\`
- supplier release：\`${report.release_boundary.supplier_release_status}\`
- supplier precheck：\`${report.release_boundary.supplier_precheck_status}\`
- 不修改散件几何，不进入完整柜体，不进入供应商 DXF release。
`
}

const report = buildReport()
for (const path of [PATHS.outJson, PATHS.outMd]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(report), 'utf8')
console.log(`${report.status}: ${PATHS.outJson}`)
console.log(`markdown: ${PATHS.outMd}`)
