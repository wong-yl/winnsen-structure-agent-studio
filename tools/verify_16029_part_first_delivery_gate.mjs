import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  manifest: 'data/locker_16029_part_first_delivery_manifest.json',
  out: 'data/locker_16029_part_first_delivery_gate.json',
}

const CORE_GATE_COMMANDS = [
  {
    label: 'route_playbook',
    script: 'tools/verify_16029_part_first_route_playbook.mjs',
  },
  {
    label: 'three_part_interface',
    script: 'tools/verify_16029_three_part_interface_verification.mjs',
  },
  {
    label: 'native_rerun',
    script: 'tools/verify_16029_three_part_native_rerun_report.mjs',
  },
]

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

function runCoreGates() {
  return CORE_GATE_COMMANDS.map((command) => {
    const result = spawnSync(process.execPath, [abs(command.script)], {
      cwd: ROOT,
      encoding: 'utf8',
      windowsHide: true,
    })
    return {
      label: command.label,
      script: command.script,
      exit_code: result.status ?? 1,
      stdout: String(result.stdout || '').trim(),
      stderr: String(result.stderr || '').trim(),
      ok: result.status === 0,
    }
  })
}

const coreGateRuns = runCoreGates()
const manifest = readJson(PATHS.manifest)
const checks = []

function check(name, ok, actual, expected, category = 'delivery_gate') {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

function checkExists(name, path, category = 'evidence') {
  check(name, existsSync(abs(path)), path, 'file exists', category)
}

function gate(manifestData, label) {
  return asArray(manifestData.gates).find((item) => item.label === label) || {}
}

function blocker(manifestData, idFragment) {
  return asArray(manifestData.current_engineering_state?.open_blockers)
    .find((item) => String(item.id || '').includes(idFragment)) || {}
}

check('core_gate_commands_passed', coreGateRuns.every((item) => item.ok), coreGateRuns, 'all rerun commands exit 0')
check('schema', manifest.schema === 'winnsen.locker16029.part_first_delivery_manifest.v1', manifest.schema, 'winnsen.locker16029.part_first_delivery_manifest.v1')
check('manifest_status_managed', manifest.status === 'MANAGED_READY_FOR_NEXT_ENGINEERING_GOAL', manifest.status, 'MANAGED_READY_FOR_NEXT_ENGINEERING_GOAL')

for (const label of ['pilot_default', 'pilot_strict', 'route_playbook', 'three_part_interface', 'native_rerun']) {
  const item = gate(manifest, label)
  check(`gate_${label}_pass`, item.status === 'PASS' && asNumber(item.checks_failed) === 0, item, 'PASS and 0 failed')
}

check('route_status_ready', manifest.current_engineering_state?.route_status === 'READY_FOR_THREE_PART_INTERFACE_ITERATION', manifest.current_engineering_state?.route_status, 'READY_FOR_THREE_PART_INTERFACE_ITERATION')
check('interface_status_ready_for_native_rerun', manifest.current_engineering_state?.interface_status === 'READY_FOR_SW_NATIVE_INTERFACE_RERUN_NOT_RELEASED', manifest.current_engineering_state?.interface_status, 'READY_FOR_SW_NATIVE_INTERFACE_RERUN_NOT_RELEASED')
check('native_status_complete_with_blocker', manifest.current_engineering_state?.native_rerun_status === 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER', manifest.current_engineering_state?.native_rerun_status, 'SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER')
check('native_conclusion_not_overclaimed', String(manifest.current_engineering_state?.native_clearance_conclusion || '').includes('DOOR_FRONT_REQUIRES_EXACT_INTERFERENCE'), manifest.current_engineering_state?.native_clearance_conclusion, 'door-front exact interference/gap still required')

const doorFrontBlocker = blocker(manifest, 'door_panel_2_12_right_v43_vs_front_frame_vertical_left_v43')
check('door_front_blocker_open', doorFrontBlocker.status === 'OPEN_REQUIRES_EXACT_INTERFERENCE_OR_ENGINEERING_GAP_REVIEW', doorFrontBlocker, 'open exact interference/gap blocker')
const supplierBlocker = blocker(manifest, 'supplier_dxf_release')
check('supplier_blocker_open', supplierBlocker.status === 'BLOCKED_NOT_READY', supplierBlocker, 'supplier blocked')

check('release_bom_pilot_only', manifest.release_boundary?.bom_status === 'pilot_only_not_released', manifest.release_boundary?.bom_status, 'pilot_only_not_released')
check('release_supplier_excluded', manifest.release_boundary?.supplier_release_status === 'supplier_release_excluded', manifest.release_boundary?.supplier_release_status, 'supplier_release_excluded')
check('release_supplier_dxf_false', manifest.release_boundary?.can_issue_supplier_dxf_now === false, manifest.release_boundary?.can_issue_supplier_dxf_now, false)
check('release_full_cabinet_false', manifest.release_boundary?.can_expand_full_cabinet_now === false, manifest.release_boundary?.can_expand_full_cabinet_now, false)
check('release_bom_false', manifest.release_boundary?.can_create_released_bom_now === false, manifest.release_boundary?.can_create_released_bom_now, false)

check('worktree_not_staged_by_codex', manifest.worktree_management?.git_stage_status === 'NOT_STAGED_BY_CODEX', manifest.worktree_management?.git_stage_status, 'NOT_STAGED_BY_CODEX')
check('worktree_not_committed_by_codex', manifest.worktree_management?.commit_status === 'NOT_COMMITTED_BY_CODEX', manifest.worktree_management?.commit_status, 'NOT_COMMITTED_BY_CODEX')

for (const group of ['core_scripts', 'core_machine_reports', 'human_reports']) {
  const files = asArray(manifest.deliverables?.[group])
  check(`${group}_all_exist`, files.length > 0 && files.every((item) => item.exists === true), files.filter((item) => !item.exists).map((item) => item.path), 'all listed files exist')
}

const swEvidence = asArray(manifest.deliverables?.solidworks_evidence)
check('solidworks_evidence_core_present', swEvidence.length > 0 && swEvidence.filter((item) => item.exists === true).length >= 10, swEvidence.filter((item) => item.exists === true).length, '>= 10 SW evidence files')

for (const path of [
  PATHS.manifest,
  'docs/16029_part_first_delivery_manifest.md',
  'data/locker_16029_part_first_route_playbook_gate.json',
  'data/locker_16029_three_part_interface_gate.json',
  'data/locker_16029_three_part_native_rerun_gate.json',
  'data/locker_16029_supplier_dxf_precheck.json',
].filter(Boolean)) {
  checkExists(`evidence_exists:${path}`, path)
}

const failed = checks.filter((item) => !item.ok)
const report = {
  schema: 'winnsen.locker16029.part_first_delivery_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length ? 'FAIL' : 'PASS',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  core_gate_runs: coreGateRuns,
  managed_state: {
    ready_for_next_engineering_goal: failed.length === 0,
    next_goal: manifest.next_recommended_goal?.objective || null,
    still_blocked_by: [
      'door_panel_2_12_right_v43_vs_front_frame_vertical_left_v43 exact interference/gap review',
      'supplier DXF release blocked',
    ],
  },
  checks,
}

writeFileSync(abs(PATHS.out), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
console.log(`${report.status}: ${PATHS.out}`)
if (failed.length) {
  for (const item of failed) console.log(`- ${item.name}`)
  process.exit(1)
}
