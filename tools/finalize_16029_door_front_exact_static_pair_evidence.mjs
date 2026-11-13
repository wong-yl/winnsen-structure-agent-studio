import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'

const evidencePath = path.resolve(process.argv[2] || 'data/locker_16029_door_front_exact_static_pair.json')
const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'))
const assemblyPath = path.resolve(evidence.assembly_path)
const assemblyBytes = fs.readFileSync(assemblyPath)
const postHash = crypto.createHash('sha256').update(assemblyBytes).digest('hex').toUpperCase()
const assemblyStat = fs.statSync(assemblyPath)

evidence.status = 'SW2020_EXACT_STATIC_PAIR_INCOMPLETE_COM_SESSION_UNAVAILABLE'
evidence.exact_static_status = 'INCOMPLETE_NOT_RUN'
evidence.exact_interference_api = 'IModeler.CheckInterferenceBetweenTwoBodies'
evidence.minimum_distance_api = 'IModelDoc2.ClosestDistance'
evidence.minimum_distance_available = false
evidence.minimum_solid_distance_mm = null
evidence.native_bbox_status = 'PRIOR_SCREEN_SEPARATED_X_7MM_NOT_USED_AS_EXACT_RESULT'
evidence.engineering_door_gap_datum_status = 'NOT_VALIDATED'
evidence.hinge_sweep_status = 'NOT_VALIDATED'
evidence.tolerance_chain_status = 'NOT_VALIDATED'
evidence.engineering_boundary = 'No exact PASS/FAIL is claimed. Static exact body interference and minimum solid distance were not reached because the SW2020 COM session was unavailable.'
evidence.release_boundary = 'Candidate placement only; formal CAD, placement, DXF, BOM, and full-cabinet release are excluded.'
evidence.input_integrity = {
  pre_run_sha256: evidence.assembly_sha256,
  post_run_sha256: postHash,
  hash_unchanged: evidence.assembly_sha256 === postHash,
  post_run_size_bytes: assemblyStat.size,
  post_run_last_write_time_utc: assemblyStat.mtime.toISOString(),
  cad_opened_by_exact_checker: false,
  cad_saved_by_exact_checker: false,
  cad_modified_by_exact_checker: false,
}
evidence.session_diagnostic = {
  blocker_classification: 'SW2020_COM_SESSION_AVAILABILITY',
  file_problem_indicated: false,
  geometry_problem_indicated: false,
  placement_adjustment_allowed: false,
  exact_checker_attempts: 2,
  exact_checker_final_error: evidence.error,
  exact_checker_final_hresult: evidence.hresult || '',
  exact_checker_final_opened: evidence.opened,
  exact_checker_final_rebuilt: evidence.rebuilt,
  exact_api_reached: false,
  minimum_distance_api_reached: false,
  modal_incident: {
    observed_local_time: '2026-06-19T15:24:00+08:00',
    cancelled_without_opening_selected_file: true,
    dialog_filename_text: '柜模型.SLDPRT',
    trigger: 'InspectAssemblyComponents.exe legacy session probe continued after command timeout',
    solidworks_pid: 22232,
    probe_evidence_path: 'C:/tmp/16029_exact_session_probe.json',
    probe_final_state: 'opened=true, rebuilt=true, component_count=3',
    exact_evidence_state_at_incident: 'SolidWorks unavailable, opened=false, exact API not reached',
  },
  final_process_state: 'NO_SOLIDWORKS_AUTOMATION_RESIDUALS',
  minimum_repair: 'Repair deterministic COM activation for the read-only exact checker, then rerun only InspectExactStaticPair.exe with the absolute candidate SLDASM path and Silent OpenDoc6. Do not use a blank-session or legacy-enumerator probe.',
}

fs.writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`)
console.log(evidencePath)
