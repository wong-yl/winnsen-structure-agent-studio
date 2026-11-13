import fs from 'node:fs';
import path from 'node:path';
import { spawn, execFileSync } from 'node:child_process';

const root = process.cwd();
const gatePath = path.join(root, 'data', 'locker_16029_timeout_mechanism_gate.json');
const inputGatePath = path.join(root, 'data', 'locker_16029_shelf_load_differential_gate.json');

function now() {
  return new Date().toISOString();
}

function listProcessesByName(names) {
  const quoted = names.map((name) => `'${name.replaceAll("'", "''")}'`).join(',');
  const command = `Get-Process -Name ${quoted} -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path,StartTime | ConvertTo-Json -Compress`;
  try {
    const stdout = execFileSync('powershell.exe', ['-NoProfile', '-Command', command], { encoding: 'utf8' }).trim();
    if (!stdout) return [];
    const parsed = JSON.parse(stdout);
    return Array.isArray(parsed) ? parsed : [parsed];
  } catch {
    return [];
  }
}

function pidAlive(pid) {
  if (!pid) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function waitForExit(child) {
  return new Promise((resolve) => {
    child.once('exit', (code, signal) => resolve({ code, signal }));
  });
}

async function runCase({ label, childScript, timeoutMs, expectedKill }) {
  const startedAt = now();
  const child = spawn(process.execPath, ['-e', childScript], {
    windowsHide: true,
    stdio: ['ignore', 'ignore', 'ignore'],
  });
  const pid = child.pid;
  let killAttempted = false;
  let killSucceeded = false;
  let timeoutFired = false;
  const startedMs = Date.now();

  const timer = setTimeout(() => {
    timeoutFired = true;
    if (pidAlive(pid)) {
      killAttempted = true;
      killSucceeded = child.kill('SIGKILL');
    }
  }, timeoutMs);

  const exit = await waitForExit(child);
  clearTimeout(timer);
  const elapsedMs = Date.now() - startedMs;
  const completedWithoutKill = !killAttempted && !timeoutFired && exit.code === 0;

  await new Promise((resolve) => setTimeout(resolve, 100));

  return {
    label,
    started_at: startedAt,
    child_executable: process.execPath,
    pid,
    timeout_ms: timeoutMs,
    expected_kill: expectedKill,
    elapsed_ms: elapsedMs,
    timeout_fired: timeoutFired,
    completed_without_kill: completedWithoutKill,
    kill_attempted: killAttempted,
    kill_succeeded: killSucceeded,
    exit_code: exit.code,
    exit_signal: exit.signal,
    outer_process_exited: true,
    no_residual_process: !pidAlive(pid),
  };
}

function classify(caseA, caseB, evidenceJsonValid, noCadProcessesStarted) {
  const caseAOk = caseA.completed_without_kill
    && !caseA.kill_attempted
    && caseA.exit_code === 0
    && caseA.no_residual_process;
  const caseBOk = caseB.timeout_fired
    && caseB.kill_attempted
    && caseB.kill_succeeded
    && caseB.outer_process_exited
    && caseB.no_residual_process;

  if (!evidenceJsonValid) return 'EVIDENCE_WRITE_INVALID';
  if (!caseB.kill_succeeded || !caseB.no_residual_process) return 'WATCHDOG_KILL_FAILED';
  if (!caseA.outer_process_exited || !caseB.outer_process_exited) return 'OUTER_TIMEOUT_FAILED';
  if (caseAOk && caseBOk && noCadProcessesStarted) return 'TIMEOUT_MECHANISM_PASS';
  return 'TIMEOUT_MECHANISM_INCOMPLETE';
}

const inputGate = JSON.parse(fs.readFileSync(inputGatePath, 'utf8'));
const cadProcessesBefore = listProcessesByName(['SLDWORKS', 'sldProcMon', 'DiagnoseSolidWorksComActivation']);

const caseA = await runCase({
  label: 'case_a_fast_exit',
  timeoutMs: 1000,
  expectedKill: false,
  childScript: 'setTimeout(() => process.exit(0), 50);',
});

const caseB = await runCase({
  label: 'case_b_long_sleep_watchdog_kill',
  timeoutMs: 500,
  expectedKill: true,
  childScript: 'setTimeout(() => process.exit(0), 10000);',
});

const cadProcessesAfter = listProcessesByName(['SLDWORKS', 'sldProcMon', 'DiagnoseSolidWorksComActivation']);
const noCadProcessesStarted = cadProcessesBefore.length === 0 && cadProcessesAfter.length === 0;

let evidenceJsonValid = false;
let classification = 'TIMEOUT_MECHANISM_INCOMPLETE';
let gate = {
  schema: 'winnsen.locker16029.timeout_mechanism_gate.v1',
  generated_at: now(),
  status: 'INCOMPLETE',
  classification,
  input_gate: {
    path: 'data/locker_16029_shelf_load_differential_gate.json',
    status: inputGate.status,
    classification: inputGate.classification,
  },
  cases: [caseA, caseB],
  evidence_json_valid: false,
  no_residual_fake_process: caseA.no_residual_process && caseB.no_residual_process,
  cad_processes_before: cadProcessesBefore,
  cad_processes_after: cadProcessesAfter,
  no_cad_or_sw_process_started: noCadProcessesStarted,
  release_boundary: {
    cad_modified: false,
    cad_opened: false,
    sw_started: false,
    com_called: false,
    opendoc6_called: false,
    candidate_resaved: false,
    placement_modified: false,
    scratch_assembly_generated: false,
    exact_api_called: false,
    closest_distance_called: false,
    full_cabinet_expanded: false,
    dxf_bom_released: false,
    native_bbox_is_not_exact_interference: true,
  },
  minimum_next_action: '',
};

fs.writeFileSync(gatePath, JSON.stringify(gate, null, 2));
try {
  JSON.parse(fs.readFileSync(gatePath, 'utf8'));
  evidenceJsonValid = true;
} catch {
  evidenceJsonValid = false;
}

classification = classify(caseA, caseB, evidenceJsonValid, noCadProcessesStarted);
gate = {
  ...gate,
  status: classification === 'TIMEOUT_MECHANISM_PASS' ? 'PASS' : 'INCOMPLETE',
  classification,
  evidence_json_valid: evidenceJsonValid,
  minimum_next_action: classification === 'TIMEOUT_MECHANISM_PASS'
    ? 'Rerun the original shelf part plus C:\\tmp same-hash copy differential in read-only mode; keep exact API and DXF/BOM release closed.'
    : 'Fix only the timeout/watchdog/outer-timeout convergence path; do not return to CAD or start SW/OpenDoc6.',
};

fs.writeFileSync(gatePath, JSON.stringify(gate, null, 2));
console.log(gatePath);
