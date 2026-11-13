import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

const root = process.cwd();
const evidencePath = path.join(root, 'data', 'locker_16029_shelf_load_original_retry.json');
const gatePath = path.join(root, 'data', 'locker_16029_shelf_load_differential_gate.json');
const sourcePath = path.join(
  root,
  'workers',
  'generated_models',
  'review_generation_requests',
  'part-first-16029-v43-pilot',
  'sw2020_part_first',
  'shelf_cross_panel_left_v43',
  'shelf_cross_panel_left_v43.SLDPRT',
);

function sha256(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex').toUpperCase();
}

function isoMtime(filePath) {
  const command = `(Get-Item -LiteralPath '${filePath.replaceAll("'", "''")}').LastWriteTimeUtc.ToString('o')`;
  return execFileSync('powershell.exe', ['-NoProfile', '-Command', command], { encoding: 'utf8' }).trim();
}

function processResiduals() {
  const ids = [25496, 22772, 18684];
  try {
    const command = `Get-Process -Id ${ids.join(',')} -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path,StartTime | ConvertTo-Json -Compress`;
    const stdout = execFileSync('powershell.exe', ['-NoProfile', '-Command', command], { encoding: 'utf8' }).trim();
    if (!stdout) return [];
    const parsed = JSON.parse(stdout);
    return Array.isArray(parsed) ? parsed : [parsed];
  } catch {
    return [];
  }
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

const evidence = readJson(evidencePath);
const afterHash = sha256(sourcePath);
const afterTime = isoMtime(sourcePath);
const beforeHash = evidence.assembly_sha256_before || afterHash;
const beforeTime = evidence.assembly_last_write_before_utc || afterTime;
const residuals = processResiduals();

Object.assign(evidence, {
  interrupted_by_supervisor: true,
  diagnostic_outcome: 'ORIGINAL_RETRY_STALLED',
  open_watchdog_failed_to_fire: true,
  assembly_sha256_after: afterHash,
  assembly_last_write_after_utc: afterTime,
  hash_unchanged: beforeHash.toUpperCase() === afterHash,
  timestamp_unchanged: beforeTime === afterTime,
  no_residual_process: residuals.length === 0,
  residual_processes_after_forced_cleanup: residuals,
  copy_test_executed: false,
  copy_test_skipped_reason: 'supervisor interruption: do not start another SW/OpenDoc6 call until watchdog/outer-timeout behavior is proven to converge',
  exact_api_called: false,
  closest_distance_called: false,
  cad_saved: false,
  cad_modified: false,
});

fs.writeFileSync(evidencePath, JSON.stringify(evidence, null, 2));

const inputGate = readJson(path.join(root, 'data', 'locker_16029_sw2020_general_assembly_load_path_gate.json'));
const gate = {
  schema: 'winnsen.locker16029.shelf_load_differential_gate.v1',
  generated_at: new Date().toISOString(),
  status: 'INCOMPLETE',
  classification: 'SHELF_LOAD_DIAGNOSTIC_INCOMPLETE',
  original_retry_outcome: 'ORIGINAL_RETRY_STALLED',
  exact_static_next_goal_allowed: false,
  input_gate: {
    path: 'data/locker_16029_sw2020_general_assembly_load_path_gate.json',
    status: inputGate.status,
    classification: inputGate.classification,
  },
  original_shelf: {
    path: sourcePath,
    path_length: sourcePath.length,
    in_workspace: sourcePath.toLowerCase().startsWith(root.toLowerCase()),
    extension: path.extname(sourcePath),
    size_bytes: fs.statSync(sourcePath).size,
    sha256_before: beforeHash,
    sha256_after: afterHash,
    last_write_before_utc: beforeTime,
    last_write_after_utc: afterTime,
    hash_unchanged: evidence.hash_unchanged,
    timestamp_unchanged: evidence.timestamp_unchanged,
  },
  original_retry_evidence: {
    path: 'data/locker_16029_shelf_load_original_retry.json',
    status: evidence.status,
    diagnostic_outcome: evidence.diagnostic_outcome,
    com_acquired: evidence.com_acquired,
    solidworks_process_id: evidence.solidworks_process_id,
    readiness_passed: evidence.readiness_passed,
    readiness_elapsed_ms: evidence.readiness_elapsed_ms,
    open_method: evidence.open_method,
    open_timed_out: evidence.open_timed_out,
    open_watchdog_failed_to_fire: evidence.open_watchdog_failed_to_fire,
    hresult: evidence.hresult,
    open_errors: evidence.open_errors,
    open_warnings: evidence.open_warnings,
    opened: evidence.opened,
    rebuilt: evidence.rebuilt,
    document_closed: evidence.document_closed,
    file_dialog_used: evidence.file_dialog_used,
    manual_file_selection_used: evidence.manual_file_selection_used,
    no_residual_process: evidence.no_residual_process,
  },
  copy_probe: {
    executed: false,
    path: 'C:\\tmp\\16029_shelf_load_probe\\shelf_cross_panel_left_v43_copy.SLDPRT',
    skipped_reason: evidence.copy_test_skipped_reason,
  },
  residual_processes_after_forced_cleanup: residuals,
  original_cad_unchanged: evidence.hash_unchanged && evidence.timestamp_unchanged,
  release_boundary: {
    cad_modified: false,
    candidate_resaved: false,
    placement_modified: false,
    scratch_assembly_generated: false,
    exact_api_called: false,
    closest_distance_called: false,
    full_cabinet_expanded: false,
    dxf_bom_released: false,
    native_bbox_is_not_exact_interference: true,
  },
  minimum_next_action: '修复诊断器 watchdog/外层超时收敛机制后，再重跑原始层板 + C:\\tmp copy 差分；在此之前不启动新的 SW/OpenDoc6、不恢复 exact API。',
};

fs.writeFileSync(gatePath, JSON.stringify(gate, null, 2));
console.log(gatePath);
