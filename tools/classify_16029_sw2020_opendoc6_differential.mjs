import fs from 'node:fs'
import path from 'node:path'

const partPath = path.resolve(process.argv[2] || 'data/locker_16029_sw2020_opendoc6_stable_part.json')
const assemblyPath = path.resolve(process.argv[3] || 'data/locker_16029_sw2020_opendoc6_candidate_assembly.json')
const gatePath = path.resolve(process.argv[4] || 'data/locker_16029_sw2020_opendoc6_differential_gate.json')
const part = JSON.parse(fs.readFileSync(partPath, 'utf8'))
const assembly = JSON.parse(fs.readFileSync(assemblyPath, 'utf8'))

const sessionReady = (item) => item.com_acquired === true
const openPass = (item) => item.opened === true && item.rebuilt === true && item.opened_requested_absolute_path === true
const clean = (item) =>
  item.file_dialog_used === false &&
  item.manual_file_selection_used === false &&
  item.hash_unchanged === true &&
  item.timestamp_unchanged === true &&
  (item.no_residual_process === true || item.post_run_cleanup?.no_residual_observed === true) &&
  item.cad_saved === false &&
  item.cad_modified === false

let classification
if (openPass(assembly) && clean(assembly)) classification = 'OPEN_PATH_RECOVERED'
else if (!openPass(part)) classification = 'SESSION_WIDE_OPEN_FAILURE'
else classification = 'CANDIDATE_ASSEMBLY_OPEN_FAILURE'

const checks = [
  { name: 'independent_inputs', ok: part.document_type === 'PART' && assembly.document_type === 'ASSEMBLY', actual: [part.document_type, assembly.document_type], expected: ['PART', 'ASSEMBLY'] },
  { name: 'part_com_acquired', ok: sessionReady(part), actual: part.com_acquired, expected: true },
  { name: 'assembly_com_acquired', ok: sessionReady(assembly), actual: assembly.com_acquired, expected: true },
  { name: 'part_clean', ok: clean(part), actual: { hash: part.hash_unchanged, timestamp: part.timestamp_unchanged, residual: part.no_residual_process }, expected: 'clean' },
  { name: 'assembly_clean', ok: clean(assembly), actual: { hash: assembly.hash_unchanged, timestamp: assembly.timestamp_unchanged, residual: assembly.no_residual_process }, expected: 'clean' },
  { name: 'no_exact_calls', ok: !part.exact_api_called && !part.closest_distance_called && !assembly.exact_api_called && !assembly.closest_distance_called, actual: false, expected: false },
  { name: 'classification_unique', ok: ['SESSION_WIDE_OPEN_FAILURE', 'CANDIDATE_ASSEMBLY_OPEN_FAILURE', 'OPEN_PATH_RECOVERED'].includes(classification), actual: classification, expected: 'known classification' },
]

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.sw2020_opendoc6_differential_gate.v1',
  generated_at: new Date().toISOString(),
  status: classification === 'OPEN_PATH_RECOVERED' && failed.length === 0 ? 'PASS' : 'INCOMPLETE',
  classification,
  exact_api_resume_allowed: classification === 'OPEN_PATH_RECOVERED' && failed.length === 0,
  stable_part: {
    path: part.assembly_path,
    sha256: part.assembly_sha256_before,
    com_acquired: part.com_acquired,
    opened: part.opened,
    rebuilt: part.rebuilt,
    hresult: part.hresult,
    error: part.error,
    open_elapsed_ms: part.open_elapsed_ms,
    open_timed_out: part.open_timed_out,
    process_launch_exit_code: part.launched_process_exit_code,
    cleanup: part.post_run_cleanup,
  },
  candidate_assembly: {
    path: assembly.assembly_path,
    sha256: assembly.assembly_sha256_before,
    com_acquired: assembly.com_acquired,
    opened: assembly.opened,
    rebuilt: assembly.rebuilt,
    hresult: assembly.hresult,
    error: assembly.error,
    open_elapsed_ms: assembly.open_elapsed_ms,
    open_timed_out: assembly.open_timed_out,
    process_launch_exit_code: assembly.launched_process_exit_code,
    cleanup: assembly.post_run_cleanup,
  },
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  release_boundary: 'Exact API remains disabled unless OPEN_PATH_RECOVERED. No CAD/placement edits or DXF/BOM/full-cabinet release.',
  checks,
}

fs.writeFileSync(gatePath, `${JSON.stringify(gate, null, 2)}\n`)
console.log(`${gate.status} ${classification}: ${path.relative(process.cwd(), gatePath)}`)
process.exitCode = gate.status === 'PASS' ? 0 : 1
