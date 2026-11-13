import fs from 'node:fs'
import path from 'node:path'

const evidencePath = path.resolve(process.argv[2] || 'data/locker_16029_sw2020_com_activation_diagnostic.json')
const gatePath = path.resolve(process.argv[3] || 'data/locker_16029_sw2020_com_activation_diagnostic_gate.json')
const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'))
const checks = []

function check(name, ok, actual, expected, category) {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

check('schema', evidence.schema === 'winnsen.locker16029.sw2020_com_activation_diagnostic.v1', evidence.schema, 'activation diagnostic schema', 'evidence')
check('absolute_existing_input', path.isAbsolute(evidence.assembly_path) && evidence.assembly_exists === true, { path: evidence.assembly_path, exists: evidence.assembly_exists }, 'absolute existing SLDASM', 'input')
check('input_hash', /^[0-9A-F]{64}$/.test(evidence.assembly_sha256_before || ''), evidence.assembly_sha256_before, 'SHA256', 'input')
check('progids_recorded', evidence.progids?.length === 2 && evidence.progids.every((item) => item.type_resolved && Array.isArray(item.get_active_object_attempts) && typeof item.activator_status === 'string'), evidence.progids, 'two ProgIDs with ROT and Activator disposition', 'activation')
check('com_acquired', evidence.com_acquired === true, evidence.com_acquired, true, 'activation')
check('opened', evidence.opened === true, evidence.opened, true, 'open')
check('opened_requested_path', evidence.opened_requested_absolute_path === true, evidence.opened_path, evidence.assembly_path, 'open')
check('rebuilt', evidence.rebuilt === true, evidence.rebuilt, true, 'open')
check('silent_open_only', evidence.open_method === 'OpenDoc6 absolute path, swOpenDocOptions_Silent|swOpenDocOptions_ReadOnly', evidence.open_method, 'silent read-only OpenDoc6', 'boundary')
check('no_file_dialog', evidence.file_dialog_used === false && evidence.manual_file_selection_used === false, { file_dialog_used: evidence.file_dialog_used, manual: evidence.manual_file_selection_used }, 'false/false', 'boundary')
check('no_exact_api', evidence.exact_api_called === false && evidence.closest_distance_called === false, { exact: evidence.exact_api_called, distance: evidence.closest_distance_called }, 'false/false', 'boundary')
check('hash_unchanged', evidence.hash_unchanged === true && evidence.assembly_sha256_before === evidence.assembly_sha256_after, { before: evidence.assembly_sha256_before, after: evidence.assembly_sha256_after }, 'unchanged', 'integrity')
check('timestamp_unchanged', evidence.timestamp_unchanged === true, { before: evidence.assembly_last_write_before_utc, after: evidence.assembly_last_write_after_utc }, 'unchanged', 'integrity')
check('not_saved_or_modified', evidence.cad_saved === false && evidence.cad_modified === false, { saved: evidence.cad_saved, modified: evidence.cad_modified }, 'false/false', 'integrity')
check('document_closed', evidence.document_closed === true, evidence.document_closed, true, 'cleanup')
check('exit_observed', evidence.exit_requested === true && evidence.exit_observed === true, { requested: evidence.exit_requested, observed: evidence.exit_observed }, 'true/true', 'cleanup')
check('no_residual_process', evidence.no_residual_process === true, evidence.no_residual_process, true, 'cleanup')
check('no_error', !evidence.error && !evidence.hresult, { error: evidence.error, hresult: evidence.hresult }, 'empty', 'runtime')

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.sw2020_com_activation_diagnostic_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length === 0 ? 'PASS' : 'INCOMPLETE',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  exact_api_resume_allowed: failed.length === 0,
  release_boundary: 'No CAD/placement edits, no exact geometry call, no DXF/BOM/full-cabinet release.',
  checks,
}

fs.writeFileSync(gatePath, `${JSON.stringify(gate, null, 2)}\n`)
console.log(`${gate.status}: ${path.relative(process.cwd(), gatePath)}`)
process.exitCode = failed.length === 0 ? 0 : 1
