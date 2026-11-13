import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const root = process.cwd();
const outPath = path.join(root, 'data', 'locker_16029_shelf_load_original_copy_postcheck.json');

function readJson(relativePath) {
  return JSON.parse(fs.readFileSync(path.join(root, relativePath), 'utf8'));
}

function runPowerShell(command) {
  try {
    return execFileSync('powershell.exe', ['-NoProfile', '-Command', command], { encoding: 'utf8' }).trim();
  } catch (error) {
    return (error.stdout || '').toString().trim();
  }
}

function processList() {
  const command = "Get-Process -Name SLDWORKS,sldProcMon,DiagnoseSolidWorksComActivation -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path,StartTime,MainWindowTitle | ConvertTo-Json -Compress";
  const stdout = runPowerShell(command);
  if (!stdout) return [];
  const parsed = JSON.parse(stdout);
  return Array.isArray(parsed) ? parsed : [parsed];
}

function visibleWindowList() {
  const script = String.raw`
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class Win32Enum {
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
"@
$rows = New-Object System.Collections.Generic.List[object]
[Win32Enum]::EnumWindows({ param($hWnd,$lParam)
  if ([Win32Enum]::IsWindowVisible($hWnd)) {
    $sb = New-Object System.Text.StringBuilder 512
    [void][Win32Enum]::GetWindowText($hWnd, $sb, $sb.Capacity)
    $title = $sb.ToString()
    if ($title -match '打开|Open|SOLIDWORKS|shelf|SLDPRT|16029') {
      [uint32]$pid = 0
      [void][Win32Enum]::GetWindowThreadProcessId($hWnd, [ref]$pid)
      $p = Get-Process -Id $pid -ErrorAction SilentlyContinue
      $rows.Add([pscustomobject]@{Handle=$hWnd.ToInt64(); ProcessId=$pid; ProcessName=$p.ProcessName; Title=$title})
    }
  }
  return $true
}, [IntPtr]::Zero) | Out-Null
$rows | ConvertTo-Json -Compress
`;
  const stdout = runPowerShell(script);
  if (!stdout) return [];
  const parsed = JSON.parse(stdout);
  return Array.isArray(parsed) ? parsed : [parsed];
}

function evidenceFields(relativePath) {
  const e = readJson(relativePath);
  return {
    path: relativePath,
    file_dialog_used: e.file_dialog_used,
    manual_file_selection_used: e.manual_file_selection_used,
    no_residual_process: e.no_residual_process,
    open_timed_out: e.open_timed_out,
    open_watchdog_killed_process: e.open_watchdog_killed_process,
    hresult: e.hresult,
    opened: e.opened,
    rebuilt: e.rebuilt,
  };
}

const original = evidenceFields('data/locker_16029_shelf_load_original_copy_original.json');
const copy = evidenceFields('data/locker_16029_shelf_load_original_copy_copy.json');
const gate = readJson('data/locker_16029_shelf_load_original_copy_differential_gate.json');
const processes = processList();
const windows = visibleWindowList();
const modalOrResidualObserved = processes.length > 0 || windows.length > 0;

const postcheck = {
  schema: 'winnsen.locker16029.shelf_load_original_copy_postcheck.v1',
  generated_at: new Date().toISOString(),
  status: modalOrResidualObserved ? 'OBSERVATION_REQUIRES_FOLLOWUP' : 'PASS',
  classification: modalOrResidualObserved ? 'POST_TEST_UI_RESIDUE_OBSERVED' : 'NO_POST_TEST_MODAL_OR_RESIDUAL',
  post_gate_modal_or_residual_observed: modalOrResidualObserved,
  process_check: {
    queried_names: ['SLDWORKS', 'sldProcMon', 'DiagnoseSolidWorksComActivation'],
    residual_processes: processes,
  },
  visible_window_check: {
    title_patterns: ['打开', 'Open', 'SOLIDWORKS', 'shelf', 'SLDPRT', '16029'],
    matching_windows: windows,
  },
  evidence_field_review: {
    original,
    copy,
    gate: {
      path: 'data/locker_16029_shelf_load_original_copy_differential_gate.json',
      status: gate.status,
      classification: gate.classification,
      file_dialog_used: gate.file_dialog_used,
      manual_file_selection_used: gate.manual_file_selection_used,
      no_residual_process: gate.no_residual_process,
      residual_processes_after_tests: gate.residual_processes_after_tests,
    },
  },
  conclusion: modalOrResidualObserved
    ? 'Current UI/process state conflicts with the no-modal/no-residual wording; do not generalize the gate until OpenDoc6 modal behavior is repaired.'
    : 'Gate remains PASS; screenshot is not used as manual file selection evidence, and no current modal/residual was observed.',
  release_boundary: {
    cad_modified: false,
    cad_opened: false,
    sw_started_by_postcheck: false,
    opendoc6_called_by_postcheck: false,
    exact_api_called: false,
    closest_distance_called: false,
    placement_modified: false,
    dxf_bom_released: false,
    full_cabinet_expanded: false,
  },
  minimum_next_action: modalOrResidualObserved
    ? 'First repair OpenDoc6/modal cleanup behavior; do not proceed to shelf content diagnostics.'
    : 'Proceed to shelf SLDPRT content diagnostics using offline/read-only methods or a disposable repair copy; do not modify the original part or return to candidate assembly/exact API.',
};

fs.writeFileSync(outPath, JSON.stringify(postcheck, null, 2));
console.log(outPath);
