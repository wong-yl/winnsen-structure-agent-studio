import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const outPath = path.join(root, 'data', 'locker_16029_sw2020_general_assembly_load_path_gate.json');

function readJson(relativePath) {
  return JSON.parse(fs.readFileSync(path.join(root, relativePath), 'utf8'));
}

function compactRun(label, evidencePath) {
  const e = readJson(evidencePath);
  return {
    label,
    evidence_path: evidencePath,
    path: e.assembly_path,
    document_type: e.document_type,
    sha256_before: e.assembly_sha256_before,
    sha256_after: e.assembly_sha256_after,
    last_write_before_utc: e.assembly_last_write_before_utc,
    last_write_after_utc: e.assembly_last_write_after_utc,
    com_acquired: e.com_acquired,
    com_attach_elapsed_ms: e.com_attach_elapsed_ms,
    readiness_elapsed_ms: e.readiness_elapsed_ms,
    readiness_passed: e.readiness_passed,
    open_method: e.open_method,
    open_elapsed_ms: e.open_elapsed_ms,
    open_timed_out: e.open_timed_out,
    open_errors: e.open_errors,
    open_warnings: e.open_warnings,
    hresult: e.hresult,
    opened: e.opened,
    rebuilt: e.rebuilt,
    document_closed: e.document_closed,
    file_dialog_used: e.file_dialog_used,
    manual_file_selection_used: e.manual_file_selection_used,
    hash_unchanged: e.hash_unchanged,
    timestamp_unchanged: e.timestamp_unchanged,
    no_residual_process: e.no_residual_process,
    exit_code: e.exit_code,
    error: e.error,
  };
}

const sameComplexityGate = readJson('data/locker_16029_sw2020_same_complexity_assembly_differential_gate.json');
const referenceDiagnostic = readJson('data/locker_16029_candidate_sldasm_reference_chain_diagnostic.json');
const referenceGate = readJson('data/locker_16029_candidate_sldasm_reference_chain_gate.json');

const childRuns = [
  compactRun('door_panel_2_12_right_v43', 'data/locker_16029_sw2020_general_load_child_door_panel.json'),
  compactRun('front_frame_vertical_left_v43', 'data/locker_16029_sw2020_general_load_child_front_frame.json'),
  compactRun('shelf_cross_panel_left_v43', 'data/locker_16029_sw2020_general_load_child_shelf.json'),
];

const referencesComplete = referenceDiagnostic.reference_chain_checks?.all_exist === true
  && referenceDiagnostic.reference_chain_checks?.all_sldprt === true
  && referenceDiagnostic.reference_chain_checks?.duplicate_paths?.length === 0
  && referenceDiagnostic.reference_chain_checks?.cycle_references?.length === 0
  && referenceGate.status === 'PASS';

const childFailures = childRuns.filter((r) => !(r.opened && r.rebuilt && r.hash_unchanged && r.timestamp_unchanged && r.no_residual_process));
let classification = 'REFERENCE_RESOLUTION_DEFECT';
let status = 'INCOMPLETE';
let scratchGenerated = false;
let scratchPath = '';
let scratchSkippedReason = '';
let minimumNextAction = '';

if (!referencesComplete) {
  classification = 'REFERENCE_RESOLUTION_DEFECT';
  scratchSkippedReason = 'reference evidence is incomplete, so scratch assembly isolation is not meaningful';
  minimumNextAction = 'Repair the reference/path evidence first; do not rebuild existing CAD.';
} else if (childFailures.length > 0) {
  classification = 'CHILD_PART_LOAD_DEFECT';
  scratchSkippedReason = 'at least one child SLDPRT failed stable read-only OpenDoc6; scratch assembly was intentionally not generated';
  minimumNextAction = 'Isolate the failed child part load path first, starting with shelf_cross_panel_left_v43 under the same stable-session policy.';
} else {
  classification = 'CHILD_PARTS_STABLE_SCRATCH_NOT_RUN';
  scratchSkippedReason = 'all child parts are stable but scratch assembly evidence is absent';
  minimumNextAction = 'Run the scratch assembly AddComponent5 probe in C:\\tmp, then classify existing container vs assembly subsystem.';
}

const gate = {
  schema: 'winnsen.locker16029.sw2020_general_assembly_load_path_gate.v1',
  generated_at: new Date().toISOString(),
  status,
  classification,
  exact_static_next_goal_allowed: false,
  same_complexity_input_gate: {
    path: 'data/locker_16029_sw2020_same_complexity_assembly_differential_gate.json',
    status: sameComplexityGate.status,
    classification: sameComplexityGate.classification,
    control_assembly: sameComplexityGate.control_assembly,
    candidate_assembly: sameComplexityGate.candidate_assembly,
  },
  reference_resolution: {
    diagnostic_path: 'data/locker_16029_candidate_sldasm_reference_chain_diagnostic.json',
    gate_path: 'data/locker_16029_candidate_sldasm_reference_chain_gate.json',
    status: referenceGate.status,
    classification: referenceGate.classification,
    complete: referencesComplete,
    reference_chain_checks: referenceDiagnostic.reference_chain_checks,
    references: referenceDiagnostic.references?.map((r) => ({
      role: r.role,
      path: r.path,
      path_form: r.path_form,
      extension: r.extension,
      exists: r.exists,
      sha256: r.sha256,
      size_bytes: r.size_bytes,
    })),
  },
  child_part_open_runs: childRuns,
  child_failures: childFailures.map((r) => ({
    label: r.label,
    path: r.path,
    hresult: r.hresult,
    open_timed_out: r.open_timed_out,
    opened: r.opened,
    rebuilt: r.rebuilt,
    error: r.error,
  })),
  scratch_probe: {
    generated: scratchGenerated,
    path: scratchPath,
    skipped_reason: scratchSkippedReason,
  },
  existing_cad_unchanged: childRuns.every((r) => r.hash_unchanged && r.timestamp_unchanged)
    && sameComplexityGate.control_assembly?.hash_unchanged === true
    && sameComplexityGate.candidate_assembly?.hash_unchanged === true
    && sameComplexityGate.control_assembly?.timestamp_unchanged === true
    && sameComplexityGate.candidate_assembly?.timestamp_unchanged === true,
  no_residual_process: childRuns.every((r) => r.no_residual_process)
    && sameComplexityGate.control_assembly?.no_residual_process === true
    && sameComplexityGate.candidate_assembly?.no_residual_process === true,
  file_dialog_used: childRuns.some((r) => r.file_dialog_used || r.manual_file_selection_used) || false,
  release_boundary: {
    cad_modified: false,
    candidate_resaved: false,
    placement_modified: false,
    exact_api_called: false,
    closest_distance_called: false,
    full_cabinet_expanded: false,
    dxf_bom_released: false,
    native_bbox_is_not_exact_interference: true,
  },
  minimum_next_action: minimumNextAction,
};

fs.writeFileSync(outPath, JSON.stringify(gate, null, 2));
console.log(outPath);
