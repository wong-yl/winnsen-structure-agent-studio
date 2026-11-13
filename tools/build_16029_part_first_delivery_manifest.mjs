import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  routePlaybook: 'data/locker_16029_part_first_route_playbook.json',
  routeGate: 'data/locker_16029_part_first_route_playbook_gate.json',
  interfaceReport: 'data/locker_16029_three_part_interface_verification.json',
  interfaceGate: 'data/locker_16029_three_part_interface_gate.json',
  nativeReport: 'data/locker_16029_three_part_native_rerun_report.json',
  nativeGate: 'data/locker_16029_three_part_native_rerun_gate.json',
  supplierPrecheck: 'data/locker_16029_supplier_dxf_precheck.json',
  defaultPilotGate: 'data/locker_16029_part_first_pilot_gate_report.json',
  strictPilotGate: 'data/locker_16029_part_first_pilot_gate_report.strict.json',
  outJson: 'data/locker_16029_part_first_delivery_manifest.json',
  outMd: 'docs/16029_part_first_delivery_manifest.md',
}

const CORE_SCRIPTS = [
  'tools/build_16029_part_first_route_playbook.mjs',
  'tools/verify_16029_part_first_route_playbook.mjs',
  'tools/build_16029_three_part_interface_verification.mjs',
  'tools/verify_16029_three_part_interface_verification.mjs',
  'tools/build_16029_three_part_native_rerun_report.mjs',
  'tools/verify_16029_three_part_native_rerun_report.mjs',
  'tools/build_16029_part_first_delivery_manifest.mjs',
  'tools/verify_16029_part_first_delivery_gate.mjs',
  'tools/build_16029_min_interface_sw_bbox_clearance_report.mjs',
  'tools/inspect_16029_min_interface_step_bboxes.mjs',
  'tools/inspect_16029_flat_dxf.mjs',
]

const CORE_REPORTS = [
  PATHS.routePlaybook,
  PATHS.routeGate,
  PATHS.interfaceReport,
  PATHS.interfaceGate,
  PATHS.nativeReport,
  PATHS.nativeGate,
  PATHS.supplierPrecheck,
  PATHS.defaultPilotGate,
  PATHS.strictPilotGate,
  PATHS.outJson,
  'data/locker_16029_part_first_delivery_gate.json',
]

const HUMAN_REPORTS = [
  'docs/16029_part_first_route_playbook.md',
  'docs/16029_three_part_interface_verification.md',
  'docs/16029_three_part_native_rerun_report.md',
  PATHS.outMd,
]

const SOLIDWORKS_EVIDENCE = [
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal.SLDPRT',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal.step',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat.dxf',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43.SLDPRT',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43.step',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/front_frame_vertical_left_v43/front_frame_vertical_left_v43_flat.dxf',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43.SLDPRT',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43.step',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43_flat.dxf',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun.SLDASM',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun.step',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_build.json',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_components.json',
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_clearance.json',
]

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path, fallback = {}) {
  if (!existsSync(abs(path))) return fallback
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function fileInfo(path) {
  const full = abs(path)
  if (!existsSync(full)) return { path, exists: false, bytes: 0, modified_at: null }
  const stat = statSync(full)
  return {
    path,
    exists: true,
    bytes: stat.size,
    modified_at: stat.mtime.toISOString(),
  }
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function gateSummary(label, path) {
  const data = readJson(path)
  return {
    label,
    path,
    schema: data.schema || null,
    status: data.status || null,
    checks_total: data.checks_total ?? null,
    checks_failed: data.checks_failed ?? null,
    engineering_clearance_status: data.engineering_clearance_status || null,
    blocking_reasons: asArray(data.blocking_reasons),
  }
}

function buildManifest() {
  const route = readJson(PATHS.routePlaybook)
  const interfaceReport = readJson(PATHS.interfaceReport)
  const nativeReport = readJson(PATHS.nativeReport)
  const supplier = readJson(PATHS.supplierPrecheck)
  const nativePairs = nativeReport.native_bbox_clearance?.pairs || {}
  const gates = [
    gateSummary('pilot_default', PATHS.defaultPilotGate),
    gateSummary('pilot_strict', PATHS.strictPilotGate),
    gateSummary('route_playbook', PATHS.routeGate),
    gateSummary('three_part_interface', PATHS.interfaceGate),
    gateSummary('native_rerun', PATHS.nativeGate),
  ]
  const coreGatePass = gates.every((gate) => gate.status === 'PASS' && asNumber(gate.checks_failed) === 0)
  const supplierBlocked = supplier.status === 'BLOCKED' && supplier.can_issue_supplier_dxf_now === false
  const doorFrontBlocked =
    nativeReport.status === 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER' &&
    nativePairs.door_to_front_frame?.aabb_intersects === true
  const releaseExcluded =
    interfaceReport.release_boundary?.supplier_release_status === 'supplier_release_excluded' &&
    nativeReport.release_boundary?.supplier_release_status === 'supplier_release_excluded'

  return {
    schema: 'winnsen.locker16029.part_first_delivery_manifest.v1',
    generated_at: new Date().toISOString(),
    status: coreGatePass && supplierBlocked && doorFrontBlocked && releaseExcluded
      ? 'MANAGED_READY_FOR_NEXT_ENGINEERING_GOAL'
      : 'MANIFEST_INCOMPLETE_OR_OVERCLAIM_RISK',
    project: {
      product: '16029 locker',
      route: 'part-first scattered SW2020 sheet-metal pilot',
      management_scope: 'code and evidence closeout only',
    },
    current_engineering_state: {
      route_status: route.status || null,
      interface_status: interfaceReport.status || null,
      native_rerun_status: nativeReport.status || null,
      native_clearance_conclusion: nativeReport.native_bbox_clearance?.conclusion || null,
      closed_by_aabb: [
        {
          pair: 'shelf_to_door',
          conclusion: nativePairs.shelf_to_door?.clearance_conclusion || null,
          minimum_separating_gap_mm: nativePairs.shelf_to_door?.minimum_separating_gap_mm ?? null,
        },
        {
          pair: 'shelf_to_front_frame',
          conclusion: nativePairs.shelf_to_front_frame?.clearance_conclusion || null,
          minimum_separating_gap_mm: nativePairs.shelf_to_front_frame?.minimum_separating_gap_mm ?? null,
        },
      ],
      open_blockers: [
        {
          id: 'door_panel_2_12_right_v43_vs_front_frame_vertical_left_v43:aabb_overlap',
          status: doorFrontBlocked ? 'OPEN_REQUIRES_EXACT_INTERFERENCE_OR_ENGINEERING_GAP_REVIEW' : 'UNKNOWN',
          evidence: nativePairs.door_to_front_frame || null,
        },
        {
          id: 'supplier_dxf_release',
          status: supplierBlocked ? 'BLOCKED_NOT_READY' : 'UNKNOWN',
          evidence: {
            supplier_precheck_status: supplier.status || null,
            blocking_reasons: asArray(supplier.blocking_reasons),
            can_issue_supplier_dxf_now: supplier.can_issue_supplier_dxf_now ?? null,
          },
        },
      ],
    },
    gates,
    release_boundary: {
      bom_status: nativeReport.release_boundary?.bom_status || null,
      supplier_release_status: nativeReport.release_boundary?.supplier_release_status || null,
      supplier_precheck_status: nativeReport.release_boundary?.supplier_precheck_status || null,
      can_issue_supplier_dxf_now: false,
      can_expand_full_cabinet_now: false,
      can_create_released_bom_now: false,
    },
    deliverables: {
      core_scripts: CORE_SCRIPTS.map(fileInfo),
      core_machine_reports: CORE_REPORTS.map(fileInfo),
      human_reports: HUMAN_REPORTS.map(fileInfo),
      solidworks_evidence: SOLIDWORKS_EVIDENCE.map(fileInfo),
    },
    worktree_management: {
      git_stage_status: 'NOT_STAGED_BY_CODEX',
      commit_status: 'NOT_COMMITTED_BY_CODEX',
      policy: 'Do not stage all dirty files. Stage only reviewed core scripts/reports after user approval.',
      recommended_review_order: [
        'core_scripts',
        'core_machine_reports',
        'human_reports',
        'solidworks_evidence only if repository policy tracks generated CAD artifacts',
      ],
      ignore_or_review_separately: [
        'tmp/',
        'tmp_chm/',
        'older probe files not referenced by this manifest',
        'unrelated modified files that predate this closeout',
      ],
    },
    next_recommended_goal: {
      objective:
        'Run exact SolidWorks interference or engineering datum/gap review for door_panel_2_12_right_v43 vs front_frame_vertical_left_v43 only.',
      non_goals: [
        'do not expand to full cabinet',
        'do not issue supplier DXF',
        'do not change shelf/front-frame/door geometry without a specific blocker fix plan',
      ],
    },
  }
}

function markdown(manifest) {
  const gateRows = manifest.gates
    .map((gate) => `| ${gate.label} | ${gate.status} | ${gate.checks_total ?? ''} | ${gate.checks_failed ?? ''} | ${gate.engineering_clearance_status || ''} |`)
    .join('\n')
  const groups = Object.entries(manifest.deliverables)
    .map(([name, files]) => {
      const existing = files.filter((item) => item.exists).length
      return `| ${name} | ${existing}/${files.length} |`
    })
    .join('\n')
  const blockers = manifest.current_engineering_state.open_blockers
    .map((item) => `- \`${item.id}\`：${item.status}`)
    .join('\n')

  return `# 16029 散件先行交付物收口 Manifest

生成时间：${manifest.generated_at}

## 总状态

- 状态：\`${manifest.status}\`
- 路线：${manifest.project.route}
- 当前工程结论：\`${manifest.current_engineering_state.native_clearance_conclusion}\`
- 代码管理状态：${manifest.worktree_management.git_stage_status}，${manifest.worktree_management.commit_status}

## Gate 汇总

| Gate | Status | Checks | Failed | Engineering status |
|---|---:|---:|---:|---|
${gateRows}

## 交付物分组

| Group | Existing |
|---|---:|
${groups}

## 仍未关闭的工程项

${blockers}

## Release 边界

- BOM：\`${manifest.release_boundary.bom_status}\`
- supplier release：\`${manifest.release_boundary.supplier_release_status}\`
- supplier precheck：\`${manifest.release_boundary.supplier_precheck_status}\`
- 不允许：supplier DXF、完整柜体、released BOM。

## Git 管理建议

${manifest.worktree_management.policy}

建议先 review/stage：
1. core_scripts
2. core_machine_reports
3. human_reports
4. CAD 生成物只在仓库策略允许时纳入

暂不建议直接 stage all：\`${manifest.worktree_management.ignore_or_review_separately.join('`, `')}\`

## 下一目标

${manifest.next_recommended_goal.objective}
`
}

const manifest = buildManifest()
for (const path of [PATHS.outJson, PATHS.outMd]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(manifest), 'utf8')
console.log(`${manifest.status}: ${PATHS.outJson}`)
console.log(`markdown: ${PATHS.outMd}`)
