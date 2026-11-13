import fs from 'node:fs'
import path from 'node:path'

const frontPath = path.resolve(process.argv[2] || 'data/locker_16029_sw2020_readiness_front_frame.json')
const assemblyPath = path.resolve(process.argv[3] || 'data/locker_16029_sw2020_readiness_candidate_assembly.json')
const priorFrontPath = path.resolve('data/locker_16029_sw2020_opendoc6_front_frame_part.json')
const priorAssemblyPath = path.resolve('data/locker_16029_sw2020_opendoc6_candidate_assembly.json')
const gatePath = path.resolve(process.argv[4] || 'data/locker_16029_sw2020_session_readiness_differential_gate.json')
const front = JSON.parse(fs.readFileSync(frontPath, 'utf8'))
const assembly = JSON.parse(fs.readFileSync(assemblyPath, 'utf8'))
const priorFront = JSON.parse(fs.readFileSync(priorFrontPath, 'utf8'))
const priorAssembly = JSON.parse(fs.readFileSync(priorAssemblyPath, 'utf8'))

const openPass = (item) => item.opened && item.rebuilt && item.opened_requested_absolute_path
const cleaned = (item) =>
  item.hash_unchanged &&
  item.timestamp_unchanged &&
  !item.file_dialog_used &&
  !item.manual_file_selection_used &&
  (item.no_residual_process || item.post_run_cleanup?.no_residual_observed)

let classification
if (openPass(assembly) && cleaned(assembly)) classification = 'SESSION_LOAD_PATH_RECOVERED'
else if (openPass(front) && !openPass(assembly)) classification = 'COMPLEX_DOCUMENT_LOAD_DEFECT'
else classification = 'STARTUP_READINESS_DEFECT'

const checks = [
  { name: 'front_readiness_passed', ok: front.readiness_passed === true, actual: front.readiness_passed, expected: true },
  { name: 'assembly_readiness_passed', ok: assembly.readiness_passed === true, actual: assembly.readiness_passed, expected: true },
  { name: 'front_clean', ok: cleaned(front), actual: { hash: front.hash_unchanged, timestamp: front.timestamp_unchanged, residual: front.no_residual_process }, expected: 'clean' },
  { name: 'assembly_clean', ok: cleaned(assembly), actual: { hash: assembly.hash_unchanged, timestamp: assembly.timestamp_unchanged, residual: assembly.no_residual_process }, expected: 'clean' },
  { name: 'no_exact_calls', ok: !front.exact_api_called && !front.closest_distance_called && !assembly.exact_api_called && !assembly.closest_distance_called, actual: false, expected: false },
  { name: 'classification_unique', ok: ['STARTUP_READINESS_DEFECT', 'COMPLEX_DOCUMENT_LOAD_DEFECT', 'SESSION_LOAD_PATH_RECOVERED'].includes(classification), actual: classification, expected: 'known classification' },
]
const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.sw2020_session_readiness_differential_gate.v1',
  generated_at: new Date().toISOString(),
  status: classification === 'SESSION_LOAD_PATH_RECOVERED' && failed.length === 0 ? 'PASS' : 'INCOMPLETE',
  classification,
  exact_api_resume_allowed: classification === 'SESSION_LOAD_PATH_RECOVERED' && failed.length === 0,
  readiness_policy: {
    wait_seconds: front.readiness_wait_seconds,
    consecutive_required: front.readiness_consecutive_required,
    poll_interval_ms: front.readiness_poll_interval_ms,
  },
  front_frame: summarize(front, priorFront),
  candidate_assembly: summarize(assembly, priorAssembly),
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  minimum_repair: classification === 'STARTUP_READINESS_DEFECT'
    ? 'Do not open documents immediately after ROT attachment. Standardize the readiness wait/probe policy before every SW2020 automation operation.'
    : classification === 'COMPLEX_DOCUMENT_LOAD_DEFECT'
      ? 'Keep the candidate immutable and isolate the complex assembly loading path; startup stabilization alone is insufficient.'
      : 'Use the same readiness policy for the next read-only exact-static validation target.',
  release_boundary: 'No CAD/placement edits, no exact API call, no DXF/BOM/full-cabinet release.',
  checks,
}

function summarize(current, prior) {
  return {
    path: current.assembly_path,
    sha256: current.assembly_sha256_before,
    com_attach_elapsed_ms: current.com_attach_elapsed_ms,
    readiness_elapsed_ms: current.readiness_elapsed_ms,
    readiness_passed: current.readiness_passed,
    readiness_probe_count: current.readiness_probes?.length,
    revision_number: current.readiness_probes?.findLast?.((item) => item.succeeded)?.revision_number || current.readiness_probes?.at(-1)?.revision_number,
    current: {
      opened: current.opened,
      rebuilt: current.rebuilt,
      open_elapsed_ms: current.open_elapsed_ms,
      open_timed_out: current.open_timed_out,
      hresult: current.hresult,
      open_errors: current.open_errors,
      open_warnings: current.open_warnings,
    },
    prior_immediate_open: {
      opened: prior.opened,
      rebuilt: prior.rebuilt,
      hresult: prior.hresult,
      open_timed_out: prior.open_timed_out,
    },
    hash_unchanged: current.hash_unchanged,
    timestamp_unchanged: current.timestamp_unchanged,
    no_residual_process: current.no_residual_process,
  }
}

fs.writeFileSync(gatePath, `${JSON.stringify(gate, null, 2)}\n`)
console.log(`${gate.status} ${classification}: ${path.relative(process.cwd(), gatePath)}`)
process.exitCode = gate.status === 'PASS' ? 0 : 1
