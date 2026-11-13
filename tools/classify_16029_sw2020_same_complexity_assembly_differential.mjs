import fs from 'node:fs'
import path from 'node:path'

const controlPath = path.resolve(process.argv[2] || 'data/locker_16029_sw2020_readiness_formal_native_rerun.json')
const candidatePath = path.resolve(process.argv[3] || 'data/locker_16029_sw2020_readiness_candidate_same_complexity.json')
const gatePath = path.resolve(process.argv[4] || 'data/locker_16029_sw2020_same_complexity_assembly_differential_gate.json')
const controlBuildPath = path.resolve('workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_build.json')
const candidateBuildPath = path.resolve('workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_build.json')
const control = JSON.parse(fs.readFileSync(controlPath, 'utf8'))
const candidate = JSON.parse(fs.readFileSync(candidatePath, 'utf8'))
const controlBuild = JSON.parse(fs.readFileSync(controlBuildPath, 'utf8'))
const candidateBuild = JSON.parse(fs.readFileSync(candidateBuildPath, 'utf8'))

const refs = (build) => (build.components || []).map((item) => path.resolve(item.path).toLowerCase()).sort()
const sameRefs = JSON.stringify(refs(controlBuild)) === JSON.stringify(refs(candidateBuild))
const controlValid =
  controlBuild.build_status === 'completed' &&
  candidateBuild.build_status === 'completed' &&
  controlBuild.placement_count === 3 &&
  candidateBuild.placement_count === 3 &&
  controlBuild.reference_count === 3 &&
  candidateBuild.reference_count === 3 &&
  sameRefs
const openPass = (item) =>
  item.opened === true &&
  item.rebuilt === true &&
  item.component_count === 3 &&
  item.opened_requested_absolute_path === true
const clean = (item) =>
  item.file_dialog_used === false &&
  item.manual_file_selection_used === false &&
  item.hash_unchanged === true &&
  item.timestamp_unchanged === true &&
  (item.no_residual_process === true || item.post_run_cleanup?.no_residual_observed === true) &&
  item.cad_saved === false &&
  item.cad_modified === false

let classification
if (!controlValid) classification = 'CONTROL_ASSEMBLY_INVALID'
else if (openPass(candidate) && clean(candidate)) classification = 'CANDIDATE_LOAD_PATH_RECOVERED'
else if (openPass(control) && clean(control) && !openPass(candidate)) classification = 'CANDIDATE_CONTAINER_LOAD_DEFECT'
else classification = 'GENERAL_ASSEMBLY_LOAD_PATH_DEFECT'

const checks = [
  { name: 'control_same_complexity', ok: controlValid, actual: { same_refs: sameRefs, control_count: controlBuild.reference_count, candidate_count: candidateBuild.reference_count }, expected: 'same 3 references and build path' },
  { name: 'control_readiness', ok: control.readiness_passed === true, actual: control.readiness_passed, expected: true },
  { name: 'candidate_readiness', ok: candidate.readiness_passed === true, actual: candidate.readiness_passed, expected: true },
  { name: 'control_clean', ok: clean(control), actual: cleanSummary(control), expected: 'clean' },
  { name: 'candidate_clean', ok: clean(candidate), actual: cleanSummary(candidate), expected: 'clean' },
  { name: 'no_exact_calls', ok: !control.exact_api_called && !control.closest_distance_called && !candidate.exact_api_called && !candidate.closest_distance_called, actual: false, expected: false },
  { name: 'classification_unique', ok: ['GENERAL_ASSEMBLY_LOAD_PATH_DEFECT', 'CANDIDATE_CONTAINER_LOAD_DEFECT', 'CANDIDATE_LOAD_PATH_RECOVERED', 'CONTROL_ASSEMBLY_INVALID'].includes(classification), actual: classification, expected: 'known classification' },
]
const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.sw2020_same_complexity_assembly_differential_gate.v1',
  generated_at: new Date().toISOString(),
  status: classification === 'CANDIDATE_LOAD_PATH_RECOVERED' && failed.length === 0 ? 'PASS' : 'INCOMPLETE',
  classification,
  exact_static_next_goal_allowed: classification === 'CANDIDATE_LOAD_PATH_RECOVERED' && failed.length === 0,
  control_valid: controlValid,
  control_assembly: summarize(control, controlBuild),
  candidate_assembly: summarize(candidate, candidateBuild),
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  minimum_next_action: classification === 'GENERAL_ASSEMBLY_LOAD_PATH_DEFECT'
    ? 'Keep both assemblies immutable and diagnose the SW2020 assembly loading subsystem or shared child-resolution path; do not rebuild the candidate.'
    : classification === 'CANDIDATE_CONTAINER_LOAD_DEFECT'
      ? 'Quarantine the candidate container and compare/rebuild only on a separate copy after approval; keep the current candidate immutable.'
      : classification === 'CANDIDATE_LOAD_PATH_RECOVERED'
        ? 'A separate next goal may resume read-only exact-static validation using the same readiness policy.'
        : 'Choose a valid same-reference, same-builder three-component control assembly before further classification.',
  release_boundary: 'AABB is not exact. Candidate placement, DXF/BOM, and full-cabinet release remain closed.',
  checks,
}

function cleanSummary(item) {
  return {
    no_dialog: !item.file_dialog_used && !item.manual_file_selection_used,
    hash_unchanged: item.hash_unchanged,
    timestamp_unchanged: item.timestamp_unchanged,
    no_residual: item.no_residual_process || item.post_run_cleanup?.no_residual_observed,
  }
}

function summarize(item, build) {
  return {
    path: item.assembly_path,
    sha256: item.assembly_sha256_before,
    last_write_time: item.assembly_last_write_before_utc,
    size_bytes: fs.statSync(item.assembly_path).size,
    reference_count: build.reference_count,
    placement_count: build.placement_count,
    com_attach_elapsed_ms: item.com_attach_elapsed_ms,
    readiness_elapsed_ms: item.readiness_elapsed_ms,
    readiness_passed: item.readiness_passed,
    open_elapsed_ms: item.open_elapsed_ms,
    open_timed_out: item.open_timed_out,
    hresult: item.hresult,
    open_errors: item.open_errors,
    open_warnings: item.open_warnings,
    opened: item.opened,
    rebuilt: item.rebuilt,
    component_count: item.component_count,
    file_dialog_used: item.file_dialog_used,
    hash_unchanged: item.hash_unchanged,
    timestamp_unchanged: item.timestamp_unchanged,
    no_residual_process: item.no_residual_process || item.post_run_cleanup?.no_residual_observed,
  }
}

fs.writeFileSync(gatePath, `${JSON.stringify(gate, null, 2)}\n`)
console.log(`${gate.status} ${classification}: ${path.relative(process.cwd(), gatePath)}`)
process.exitCode = gate.status === 'PASS' ? 0 : 1
