import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  routePlaybook: 'data/locker_16029_part_first_route_playbook.json',
  routeGate: 'data/locker_16029_part_first_route_playbook_gate.json',
  currentPlacements:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_placements.tsv',
  currentBuild:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_result.json',
  currentInspect:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_inspect.json',
  currentStepProbe: 'data/locker_16029_three_part_interface_step_bbox_probe.json',
  historicalEnvelopeBuild:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_build.json',
  historicalEnvelopeComponents:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_components.json',
  historicalEnvelopeClearance:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_clearance.json',
  outJson: 'data/locker_16029_three_part_interface_verification.json',
  outMd: 'docs/16029_three_part_interface_verification.md',
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path, fallback = null) {
  if (!existsSync(abs(path))) return fallback
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function readText(path, fallback = '') {
  if (!existsSync(abs(path))) return fallback
  return readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, '')
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function round(value) {
  return Number.isFinite(value) ? Math.round(value * 1000) / 1000 : null
}

function splitTsv(line) {
  return line.split('\t').map((value) => value.trim())
}

function parsePlacements(path) {
  const text = readText(path)
  const lines = text.split(/\r?\n/).filter((line) => line.trim())
  const headers = splitTsv(lines.shift() || '')
  const index = new Map(headers.map((name, i) => [name, i]))
  return lines.map((line) => {
    const values = splitTsv(line)
    const get = (name) => values[index.get(name)] || ''
    return {
      role: get('role'),
      path: get('path').replaceAll('\\', '/'),
      tx_mm: asNumber(get('tx_mm')),
      ty_mm: asNumber(get('ty_mm')),
      tz_mm: asNumber(get('tz_mm')),
      rotation: get('rotation'),
    }
  }).filter((row) => row.role)
}

function rowFor(playbook, partId) {
  return asArray(playbook.pilot_bom?.rows).find((row) => row.part_id === partId) || {}
}

function componentForBuild(build, role) {
  return asArray(build?.components).find((component) => component.role === role) || {}
}

function componentForProbe(probe, role) {
  return asArray(probe?.components).find((component) => component.role === role) || {}
}

function pairFor(clearance, roleA, roleB) {
  return asArray(clearance?.pairwise).find((pair) => {
    const roles = asArray(pair.roles)
    return roles.includes(roleA) && roles.includes(roleB)
  }) || null
}

function expectedPath(row) {
  return String(row.artifacts?.sldprt || '').replaceAll('\\', '/')
}

function placementMatchesBom(placement, row) {
  const expected = expectedPath(row)
  return expected && placement?.path?.endsWith(expected)
}

function buildPartEvidence(playbook, placements, currentBuild, currentStepProbe) {
  const door = playbook.door_panel_rule || {}
  const front = playbook.second_part_verification || {}
  const shelf = playbook.third_part_verification || {}
  const rows = {
    door_panel_2_12_right_v43: rowFor(playbook, 'door_panel_2_12_right_v43'),
    front_frame_vertical_left_v43: rowFor(playbook, 'front_frame_vertical_left_v43'),
    shelf_cross_panel_left_v43: rowFor(playbook, 'shelf_cross_panel_left_v43'),
  }

  return [
    {
      part_id: 'door_panel_2_12_right_v43',
      role: rows.door_panel_2_12_right_v43.role || 'ordinary door panel',
      validation_status: rows.door_panel_2_12_right_v43.validation_status || null,
      placement: placements.find((item) => item.role === 'door_panel_2_12_right_v43') || null,
      placement_matches_bom_sldprt: placementMatchesBom(
        placements.find((item) => item.role === 'door_panel_2_12_right_v43'),
        rows.door_panel_2_12_right_v43,
      ),
      build_component: componentForBuild(currentBuild, 'door_panel_2_12_right_v43'),
      step_probe: componentForProbe(currentStepProbe, 'door_panel_2_12_right_v43'),
      part_checks: {
        folded_bbox_mm: door.hole_and_flat_validation?.folded_bbox_mm || null,
        flat_bbox_mm: door.hole_and_flat_validation?.flat_bbox_mm || null,
        hole_cut_count: door.hole_and_flat_validation?.result_hole_cut_count ?? null,
        flat_circle_count: door.hole_and_flat_validation?.flat_dxf_entity_counts?.CIRCLE ?? null,
      },
    },
    {
      part_id: 'front_frame_vertical_left_v43',
      role: rows.front_frame_vertical_left_v43.role || 'front frame vertical / side frame',
      validation_status: rows.front_frame_vertical_left_v43.validation_status || null,
      placement: placements.find((item) => item.role === 'front_frame_vertical_left_v43') || null,
      placement_matches_bom_sldprt: placementMatchesBom(
        placements.find((item) => item.role === 'front_frame_vertical_left_v43'),
        rows.front_frame_vertical_left_v43,
      ),
      build_component: componentForBuild(currentBuild, 'front_frame_vertical_left_v43'),
      step_probe: componentForProbe(currentStepProbe, 'front_frame_vertical_left_v43'),
      part_checks: {
        has_sheet_metal: front.has_sheet_metal === true,
        has_flat_pattern: front.has_flat_pattern === true,
        hole_cut_count: front.hole_cut_count ?? null,
        flat_bbox_mm: front.flat_check?.bbox_mm || null,
      },
    },
    {
      part_id: 'shelf_cross_panel_left_v43',
      role: rows.shelf_cross_panel_left_v43.role || 'shelf / cross panel',
      validation_status: rows.shelf_cross_panel_left_v43.validation_status || null,
      placement: placements.find((item) => item.role === 'shelf_cross_panel_left_v43') || null,
      placement_matches_bom_sldprt: placementMatchesBom(
        placements.find((item) => item.role === 'shelf_cross_panel_left_v43'),
        rows.shelf_cross_panel_left_v43,
      ),
      build_component: componentForBuild(currentBuild, 'shelf_cross_panel_left_v43'),
      step_probe: componentForProbe(currentStepProbe, 'shelf_cross_panel_left_v43'),
      part_checks: {
        has_sheet_metal: shelf.has_sheet_metal === true,
        has_flat_pattern: shelf.has_flat_pattern === true,
        flat_exported: shelf.flat_exported === true,
        flat_bbox_mm: shelf.flat_check?.bbox_mm || null,
        entity_counts: shelf.flat_check?.entity_counts || {},
        entity_count_total: shelf.flat_check?.entity_count_total ?? null,
      },
    },
  ]
}

function buildInterfaceChecks(playbook, currentStepProbe, historicalClearance) {
  const relations = asArray(playbook.assembly_interfaces?.relations)
  const historicalDoorFront = pairFor(historicalClearance, 'door_panel_2_12_right_v43', 'front_frame_vertical_left_v43')
  const historicalShelfFront = pairFor(historicalClearance, 'shelf_cross_panel_left_v43', 'front_frame_vertical_left_v43')
  const historicalShelfDoor = pairFor(historicalClearance, 'shelf_cross_panel_left_v43', 'door_panel_2_12_right_v43')
  const stepDoorFront = pairFor(currentStepProbe, 'door_panel_2_12_right_v43', 'front_frame_vertical_left_v43')
  const stepShelfFront = pairFor(currentStepProbe, 'shelf_cross_panel_left_v43', 'front_frame_vertical_left_v43')
  const stepShelfDoor = pairFor(currentStepProbe, 'shelf_cross_panel_left_v43', 'door_panel_2_12_right_v43')

  return [
    {
      id: 'door_to_front_frame_datum',
      relation_contract: relations.find((item) => item.from === 'door_panel_2_12_right_v43' && item.to === 'front_frame_vertical_left_v43') || null,
      required_method:
        'Current production SLDASM must open/rebuild in SW2020 and pass native component bbox or exact mate/interference check; STEP AABB overlap alone is not a fail/pass datum signoff.',
      current_formal_step_probe: stepDoorFront,
      historical_envelope_reference: historicalDoorFront
        ? {
          status: historicalDoorFront.clearance_conclusion,
          minimum_separating_gap_mm: historicalDoorFront.minimum_separating_gap_mm,
          use: 'reference_only_not_current_formal_sldasm_clearance',
        }
        : null,
      package_conclusion:
        'Current formal parts and placement are packaged; native clearance must be rerun because current SLDASM inspect did not open and STEP AABB is not sufficient for hinge datum/gap signoff.',
    },
    {
      id: 'shelf_to_front_frame_locating',
      relation_contract: relations.find((item) => item.from === 'shelf_cross_panel_left_v43' && item.to === 'front_frame_vertical_left_v43') || null,
      required_method:
        'Use current production shelf and front frame SLDPRTs in SW2020; check locating hole/front-edge relation and exact interference. Do not use the current shelf STEP AABB because its point envelope is degraded.',
      current_formal_step_probe: stepShelfFront,
      historical_envelope_reference: historicalShelfFront
        ? {
          status: historicalShelfFront.clearance_conclusion,
          minimum_separating_gap_mm: historicalShelfFront.minimum_separating_gap_mm,
          use: 'reference_only_not_current_formal_shelf_geometry',
        }
        : null,
      package_conclusion:
        'Shelf part-level SheetMetal/FlatPattern is verified, but production interface clearance remains open until SW native rerun with the current SLDPRT succeeds.',
    },
    {
      id: 'shelf_to_door_swing_clearance',
      relation_contract: relations.find((item) => item.from === 'shelf_cross_panel_left_v43' && item.to === 'door_panel_2_12_right_v43') || null,
      required_method:
        'Static bbox separation is only a screen. Final pass requires current SW2020 assembly open/rebuild plus door swing swept clearance or an explicit engineering gap record.',
      current_formal_step_probe: stepShelfDoor,
      historical_envelope_reference: historicalShelfDoor
        ? {
          status: historicalShelfDoor.clearance_conclusion,
          minimum_separating_gap_mm: historicalShelfDoor.minimum_separating_gap_mm,
          use: 'reference_only_static_aabb_not_swing_sweep',
        }
        : null,
      package_conclusion:
        'Door and shelf are ready for narrow clearance rerun; this package does not close swing clearance.',
    },
  ]
}

function packageStatus(currentInspect, currentStepProbe) {
  const formalSldasmOpened = currentInspect?.opened === true && currentInspect?.rebuilt === true && asNumber(currentInspect?.component_count) === 3
  const stepProbeUsable = currentStepProbe?.status === 'FALLBACK_AABB_PROBED'
  if (formalSldasmOpened && stepProbeUsable) return 'READY_FOR_EXACT_INTERFACE_REVIEW_NOT_RELEASED'
  return 'READY_FOR_SW_NATIVE_INTERFACE_RERUN_NOT_RELEASED'
}

function buildReport() {
  const playbook = readJson(PATHS.routePlaybook, {})
  const routeGate = readJson(PATHS.routeGate, {})
  const currentBuild = readJson(PATHS.currentBuild, {})
  const currentInspect = readJson(PATHS.currentInspect, {})
  const currentStepProbe = readJson(PATHS.currentStepProbe, {})
  const historicalEnvelopeBuild = readJson(PATHS.historicalEnvelopeBuild, {})
  const historicalEnvelopeComponents = readJson(PATHS.historicalEnvelopeComponents, {})
  const historicalEnvelopeClearance = readJson(PATHS.historicalEnvelopeClearance, {})
  const placements = parsePlacements(PATHS.currentPlacements)
  const partEvidence = buildPartEvidence(playbook, placements, currentBuild, currentStepProbe)
  const interfaceChecks = buildInterfaceChecks(playbook, currentStepProbe, historicalEnvelopeClearance)
  const currentInspectClosed =
    currentInspect.opened === true &&
    currentInspect.rebuilt === true &&
    asNumber(currentInspect.component_count) === 3
  const currentShelfStep = componentForProbe(currentStepProbe, 'shelf_cross_panel_left_v43')

  return {
    schema: 'winnsen.locker16029.three_part_interface_verification.v1',
    generated_at: new Date().toISOString(),
    status: packageStatus(currentInspect, currentStepProbe),
    scope: {
      product: '16029 locker',
      route: 'part-first scattered SW2020 sheet-metal pilot',
      assembly_id: playbook.assembly_interfaces?.assembly_id || 'door_frame_lock_shelf_minimum_interface_v43',
      included_parts: partEvidence.map((part) => part.part_id),
      explicit_non_goals: [
        'full cabinet generation',
        'v20 full assembly repair',
        'released production BOM',
        'supplier DXF release',
      ],
    },
    source_route: {
      playbook: PATHS.routePlaybook,
      playbook_status: playbook.status || null,
      route_gate: PATHS.routeGate,
      route_gate_status: routeGate.status || null,
      route_gate_checks_failed: routeGate.checks_failed ?? null,
    },
    current_formal_interface_package: {
      placements_tsv: PATHS.currentPlacements,
      placement_count: placements.length,
      placements,
      build_result_json: PATHS.currentBuild,
      build_status: currentBuild.build_status || null,
      saved: currentBuild.saved === true,
      rebuilt: currentBuild.rebuilt === true,
      reference_count: asNumber(currentBuild.reference_count),
      components_added: asArray(currentBuild.components).filter((item) => item.added === true).length,
      component_errors: asArray(currentBuild.components).filter((item) => item.error || asNumber(item.open_errors) > 0)
        .map((item) => ({ role: item.role, open_errors: item.open_errors, open_warnings: item.open_warnings, error: item.error || '' })),
      inspect_json: PATHS.currentInspect,
      inspect_opened: currentInspect.opened === true,
      inspect_rebuilt: currentInspect.rebuilt === true,
      inspect_component_count: asNumber(currentInspect.component_count),
      formal_native_clearance_status: currentInspectClosed
        ? 'CURRENT_FORMAL_SLDASM_ENUMERATED_READY_FOR_CLEARANCE'
        : 'NOT_CLOSED_CURRENT_SLDASM_INSPECT_DID_NOT_OPEN_OR_ENUMERATE',
    },
    part_evidence: partEvidence,
    current_formal_step_bbox_probe: {
      report: PATHS.currentStepProbe,
      status: currentStepProbe.status || null,
      component_count: asNumber(currentStepProbe.component_count),
      clearance_usable_component_count: asNumber(currentStepProbe.clearance_usable_component_count),
      shelf_step_sanity_status: currentShelfStep.bbox_sanity?.status || null,
      shelf_step_sanity_reason: currentShelfStep.bbox_sanity?.reason || '',
      use_in_gate:
        'Diagnostic only. It proves current formal shelf STEP point bbox is degraded and must not be used as clearance signoff.',
    },
    historical_native_envelope_reference: {
      use_in_gate: 'reference_only_not_current_formal_geometry',
      build_json: PATHS.historicalEnvelopeBuild,
      components_json: PATHS.historicalEnvelopeComponents,
      clearance_json: PATHS.historicalEnvelopeClearance,
      build_status: historicalEnvelopeBuild.build_status || null,
      opened: historicalEnvelopeComponents.opened === true,
      rebuilt: historicalEnvelopeComponents.rebuilt === true,
      component_count: asNumber(historicalEnvelopeComponents.component_count),
      clearance_status: historicalEnvelopeClearance.status || null,
      overlapping_pair_count: historicalEnvelopeClearance.overlapping_pair_count ?? null,
      separated_pair_count: historicalEnvelopeClearance.separated_pair_count ?? null,
    },
    interface_checks: interfaceChecks,
    required_next_sw2020_rerun: {
      status: 'REQUIRED_BEFORE_INTERFACE_CLEARANCE_SIGNOFF',
      reason:
        'The current formal three-part SLDASM build exists, but its inspect evidence did not open/enumerate the assembly, and the current shelf STEP bbox probe is degraded.',
      minimum_actions: [
        'Open the current formal SLDASM from door_frame_lock_shelf_minimum_interface_v43_placements.tsv in SW2020.',
        'Rebuild and enumerate exactly three unsuppressed components using the current production SLDPRTs.',
        'Run native component bbox or exact interference for door-front, shelf-front, and shelf-door pairs.',
        'Run a door swing swept clearance check or record an engineering gap for shelf-to-door swing.',
      ],
    },
    release_boundary: {
      bom_status: playbook.pilot_bom?.status || null,
      supplier_release_status: playbook.minimal_gate_flow?.status || null,
      supplier_precheck_status: playbook.minimal_gate_flow?.supplier_release_boundary?.supplier_precheck_status || null,
      can_issue_supplier_dxf_now: false,
      can_expand_to_full_cabinet_now: false,
    },
  }
}

function markdown(report) {
  const parts = report.part_evidence
    .map((part) => `| ${part.part_id} | ${part.validation_status} | ${part.placement_matches_bom_sldprt} | ${part.build_component?.added === true} | ${part.step_probe?.bbox_sanity?.status || 'N/A'} |`)
    .join('\n')
  const interfaces = report.interface_checks
    .map((item) => `| ${item.id} | ${item.relation_contract?.check || ''} | ${item.historical_envelope_reference?.minimum_separating_gap_mm ?? 'N/A'} | ${item.package_conclusion} |`)
    .join('\n')
  const actions = report.required_next_sw2020_rerun.minimum_actions.map((item, index) => `${index + 1}. ${item}`).join('\n')

  return `# 16029 三散件最小接口验证包

生成时间：${report.generated_at}

## 结论

- 状态：\`${report.status}\`
- 当前正式三件：${report.scope.included_parts.map((item) => `\`${item}\``).join('、')}
- 结论口径：本包证明三散件证据链、placement 和接口检查口径已经闭合；它不签核正式几何 clearance。
- 边界：不生成完整柜体，不进入供应商 DXF release，不生成 released BOM。

## 正式接口包

- placement：\`${report.current_formal_interface_package.placements_tsv}\`
- build：\`${report.current_formal_interface_package.build_status}\`，saved=\`${report.current_formal_interface_package.saved}\`，reference_count=\`${report.current_formal_interface_package.reference_count}\`
- inspect：opened=\`${report.current_formal_interface_package.inspect_opened}\`，rebuilt=\`${report.current_formal_interface_package.inspect_rebuilt}\`，component_count=\`${report.current_formal_interface_package.inspect_component_count}\`
- formal native clearance：\`${report.current_formal_interface_package.formal_native_clearance_status}\`

## 三散件证据

| Part | Validation | Placement matches BOM SLDPRT | Added to SLDASM | STEP bbox sanity |
|---|---|---:|---:|---|
${parts}

## 接口检查口径

| Interface | Contract check | Historical envelope gap mm | Current package conclusion |
|---|---|---:|---|
${interfaces}

## STEP BBox 诊断

- 当前正式三件 STEP probe：\`${report.current_formal_step_bbox_probe.status}\`
- usable component：\`${report.current_formal_step_bbox_probe.clearance_usable_component_count}/${report.current_formal_step_bbox_probe.component_count}\`
- 层板 STEP sanity：\`${report.current_formal_step_bbox_probe.shelf_step_sanity_status}\`
- 说明：${report.current_formal_step_bbox_probe.shelf_step_sanity_reason}

## 历史 native-envelope 参考

- clearance：\`${report.historical_native_envelope_reference.clearance_status}\`
- separated pairs：\`${report.historical_native_envelope_reference.separated_pair_count}\`
- overlapping pairs：\`${report.historical_native_envelope_reference.overlapping_pair_count}\`
- 用途：${report.historical_native_envelope_reference.use_in_gate}

## 下一步 SW2020 复核

${actions}

## Release 边界

- BOM：\`${report.release_boundary.bom_status}\`
- supplier release：\`${report.release_boundary.supplier_release_status}\`
- supplier precheck：\`${report.release_boundary.supplier_precheck_status}\`
`
}

const report = buildReport()
for (const path of [PATHS.outJson, PATHS.outMd]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(report), 'utf8')
console.log(`${report.status}: ${PATHS.outJson}`)
console.log(`markdown: ${PATHS.outMd}`)
