import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

const root = process.cwd();
const originalPath = path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'shelf_cross_panel_left_v43', 'shelf_cross_panel_left_v43.SLDPRT');
const copyPath = 'C:\\tmp\\16029_shelf_load_probe\\shelf_cross_panel_left_v43_copy.SLDPRT';
const outPath = path.join(root, 'data', 'locker_16029_shelf_load_original_copy_differential_gate.json');

function readJson(relativePath) {
  return JSON.parse(fs.readFileSync(path.join(root, relativePath), 'utf8'));
}

function sha256(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex').toUpperCase();
}

function mtimeUtc(filePath) {
  const escaped = filePath.replaceAll("'", "''");
  return execFileSync('powershell.exe', ['-NoProfile', '-Command', `(Get-Item -LiteralPath '${escaped}').LastWriteTimeUtc.ToString('o')`], { encoding: 'utf8' }).trim();
}

function fileMeta(filePath) {
  const stat = fs.statSync(filePath);
  return {
    path: filePath,
    exists: true,
    size_bytes: stat.size,
    last_write_utc: mtimeUtc(filePath),
    sha256: sha256(filePath),
    path_length: filePath.length,
  };
}

function processResiduals() {
  const command = "Get-Process -Name SLDWORKS,sldProcMon,DiagnoseSolidWorksComActivation -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path,StartTime | ConvertTo-Json -Compress";
  try {
    const stdout = execFileSync('powershell.exe', ['-NoProfile', '-Command', command], { encoding: 'utf8' }).trim();
    if (!stdout) return [];
    const parsed = JSON.parse(stdout);
    return Array.isArray(parsed) ? parsed : [parsed];
  } catch {
    return [];
  }
}

function compactEvidence(relativePath) {
  const e = readJson(relativePath);
  return {
    evidence_path: relativePath,
    status: e.status,
    exit_code: e.exit_code,
    assembly_path: e.assembly_path,
    assembly_sha256_before: e.assembly_sha256_before,
    assembly_sha256_after: e.assembly_sha256_after,
    assembly_last_write_before_utc: e.assembly_last_write_before_utc,
    assembly_last_write_after_utc: e.assembly_last_write_after_utc,
    hash_unchanged: e.hash_unchanged,
    timestamp_unchanged: e.timestamp_unchanged,
    com_acquired: e.com_acquired,
    solidworks_process_id: e.solidworks_process_id,
    readiness_passed: e.readiness_passed,
    readiness_elapsed_ms: e.readiness_elapsed_ms,
    open_method: e.open_method,
    open_timed_out: e.open_timed_out,
    open_watchdog_killed_process: e.open_watchdog_killed_process,
    hresult: e.hresult,
    open_errors: e.open_errors,
    open_warnings: e.open_warnings,
    opened: e.opened,
    rebuilt: e.rebuilt,
    document_closed: e.document_closed,
    file_dialog_used: e.file_dialog_used,
    manual_file_selection_used: e.manual_file_selection_used,
    exact_api_called: e.exact_api_called,
    closest_distance_called: e.closest_distance_called,
    cad_saved: e.cad_saved,
    cad_modified: e.cad_modified,
    no_residual_process: e.no_residual_process,
  };
}

const inputShelfGate = readJson('data/locker_16029_shelf_load_differential_gate.json');
const timeoutGate = readJson('data/locker_16029_timeout_mechanism_gate.json');
const originalMeta = fileMeta(originalPath);
const copyMeta = fs.existsSync(copyPath)
  ? fileMeta(copyPath)
  : { path: copyPath, exists: false };
const copySameHash = copyMeta.exists && originalMeta.sha256 === copyMeta.sha256;
const originalEvidence = compactEvidence('data/locker_16029_shelf_load_original_copy_original.json');
const copyEvidence = compactEvidence('data/locker_16029_shelf_load_original_copy_copy.json');
const residuals = processResiduals();

const originalFailed = originalEvidence.open_timed_out === true
  && originalEvidence.hresult === '0x800706BE'
  && originalEvidence.opened === false
  && originalEvidence.rebuilt === false
  && originalEvidence.no_residual_process === true;
const copyFailed = copyEvidence.open_timed_out === true
  && copyEvidence.hresult === '0x800706BE'
  && copyEvidence.opened === false
  && copyEvidence.rebuilt === false
  && copyEvidence.no_residual_process === true;
const originalSucceeded = originalEvidence.opened === true && originalEvidence.rebuilt === true;
const copySucceeded = copyEvidence.opened === true && copyEvidence.rebuilt === true;

let classification = 'SHELF_LOAD_DIAGNOSTIC_INCOMPLETE';
if (originalSucceeded) {
  classification = 'SHELF_LOAD_RECOVERED_ON_RETRY';
} else if (originalFailed && copySameHash && copySucceeded) {
  classification = 'SHELF_PATH_OR_LOCATION_LOAD_DEFECT';
} else if (originalFailed && copySameHash && copyFailed) {
  classification = 'SHELF_FILE_CONTENT_LOAD_DEFECT';
} else if ((originalSucceeded && copyFailed) || (originalFailed && copySucceeded && !copySameHash)) {
  classification = 'SHELF_NONDETERMINISTIC_LOAD_DEFECT';
}

const gate = {
  schema: 'winnsen.locker16029.shelf_load_original_copy_differential_gate.v1',
  generated_at: new Date().toISOString(),
  status: classification === 'SHELF_LOAD_DIAGNOSTIC_INCOMPLETE' ? 'INCOMPLETE' : 'PASS',
  classification,
  exact_static_next_goal_allowed: false,
  input_gates: {
    shelf_load_differential: {
      path: 'data/locker_16029_shelf_load_differential_gate.json',
      status: inputShelfGate.status,
      classification: inputShelfGate.classification,
    },
    timeout_mechanism: {
      path: 'data/locker_16029_timeout_mechanism_gate.json',
      status: timeoutGate.status,
      classification: timeoutGate.classification,
    },
  },
  original: {
    metadata: originalMeta,
    evidence: originalEvidence,
  },
  copy: {
    metadata: copyMeta,
    same_hash_as_original: copySameHash,
    diagnostic_only_not_release: true,
    evidence: copyEvidence,
  },
  residual_processes_after_tests: residuals,
  no_residual_process: residuals.length === 0 && originalEvidence.no_residual_process && copyEvidence.no_residual_process,
  original_cad_unchanged: originalEvidence.hash_unchanged === true && originalEvidence.timestamp_unchanged === true && originalMeta.sha256 === originalEvidence.assembly_sha256_before,
  file_dialog_used: originalEvidence.file_dialog_used === true || copyEvidence.file_dialog_used === true,
  manual_file_selection_used: originalEvidence.manual_file_selection_used === true || copyEvidence.manual_file_selection_used === true,
  release_boundary: {
    original_cad_modified: false,
    copy_is_release_or_replacement: false,
    candidate_resaved: false,
    placement_modified: false,
    scratch_assembly_generated: false,
    exact_api_called: false,
    closest_distance_called: false,
    full_cabinet_expanded: false,
    dxf_bom_released: false,
    native_bbox_is_not_exact_interference: true,
  },
  minimum_next_action: classification === 'SHELF_FILE_CONTENT_LOAD_DEFECT'
    ? 'Diagnose the shelf part content itself using read-only/offline methods or a disposable repair copy; do not return to candidate assembly or exact API yet.'
    : 'Follow the classification-specific next diagnostic step; keep exact API and release closed.',
};

fs.writeFileSync(outPath, JSON.stringify(gate, null, 2));
console.log(outPath);
