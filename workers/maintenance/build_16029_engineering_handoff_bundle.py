from __future__ import annotations

import csv
import json
import os
import shutil
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
QUALITY_MATRIX_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_JSON", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.json")
)
DEFAULT_HANDOFF_DIR = ROOT_DIR / "workers" / "handoffs" / "16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519"
HANDOFF_DIR = Path(os.getenv("STUDIO_16029_ENGINEERING_HANDOFF_DIR", DEFAULT_HANDOFF_DIR))
SOLIDWORKS_OPEN_VERIFICATION_PATH = HANDOFF_DIR / "SOLIDWORKS_OPEN_VERIFICATION_20260520.md"
SOLIDWORKS_NATIVE_OPEN_VERIFICATION_PATH = HANDOFF_DIR / "SOLIDWORKS_NATIVE_OPEN_VERIFICATION_20260521.md"
HANDOFF_MANIFEST_PATH = Path(
    os.getenv("STUDIO_16029_ENGINEERING_HANDOFF_MANIFEST_JSON", ROOT_DIR / "data" / "locker_16029_engineering_handoff_bundle.json")
)
HANDOFF_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_ENGINEERING_HANDOFF_MD", ROOT_DIR / "data" / "locker_16029_engineering_handoff_bundle.md")
)
SOLIDWORKS_SHORTCUT = Path(os.getenv("STUDIO_SOLIDWORKS_SHORTCUT", r"C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk"))
SOLIDWORKS_EXE = Path(
    os.getenv("STUDIO_SOLIDWORKS_EXE", r"D:\软件安装录\soildworks\SOLIDWORKS\SLDWORKS.exe")
)
FREECAD_EXE = Path(
    os.getenv(
        "STUDIO_FREECAD_EXE",
        r"D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCAD.exe",
    )
)
SOLIDWORKS_OPEN_SCRIPT = Path(
    os.getenv("STUDIO_SOLIDWORKS_OPEN_SCRIPT", r"D:\机械结构工程师智能体\scripts\sw_open_and_activate.js")
)

STATUS_LABELS = {
    "PASS_READY_FOR_ENGINEERING_REVIEW": "ready_for_engineering_review",
    "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT": "step_geometry_pass_fcstd_audit_pending",
    "PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT": "rule_counts_pass_geometry_audit_pending",
    "FAIL": "failed_quality_gate",
    "MISSING_OUTPUT": "missing_output",
}
HANDOFF_ALLOWED_STATUSES = {
    "PASS_READY_FOR_ENGINEERING_REVIEW",
    "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT",
}


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def file_size_mb(path: Path | None) -> float | None:
    if not path or not path.exists():
        return None
    return round(path.stat().st_size / (1024 * 1024), 3)


def ensure_link_or_copy(source: Path, target: Path) -> str:
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        if target.stat().st_size == source.stat().st_size:
            return "existing"
        target.unlink()
    try:
        os.link(source, target)
        return "hardlink"
    except OSError:
        shutil.copy2(source, target)
        return "copy"


def copy_if_exists(source_value: str | None, target_dir: Path) -> str | None:
    if not source_value:
        return None
    source = Path(source_value)
    if not source.exists():
        return None
    target = target_dir / source.name
    shutil.copy2(source, target)
    return str(target)


def ps_single_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def write_powershell_script(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8-sig")


def write_solidworks_launcher(path: Path, step_path: Path) -> None:
    ps1 = path.with_suffix(".ps1")
    status_path = path.with_suffix(".status.txt")
    stdout_path = path.with_suffix(".solidworks_open.stdout.txt")
    stderr_path = path.with_suffix(".solidworks_open.stderr.txt")
    script = f"""$ErrorActionPreference = 'Stop'
$stepPath = {ps_single_quote(str(step_path))}
$solidWorksExe = {ps_single_quote(str(SOLIDWORKS_EXE))}
$solidWorksShortcut = {ps_single_quote(str(SOLIDWORKS_SHORTCUT))}
$solidWorksOpenScript = {ps_single_quote(str(SOLIDWORKS_OPEN_SCRIPT))}
$statusPath = {ps_single_quote(str(status_path))}
$stdoutPath = {ps_single_quote(str(stdout_path))}
$stderrPath = {ps_single_quote(str(stderr_path))}

function Write-OpenStatus([string] $status, [string] $message) {{
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $status | $message"
  Set-Content -LiteralPath $statusPath -Value $line -Encoding UTF8
}}

function Wait-SolidWorksMainWindow([int] $timeoutSeconds) {{
  $deadline = (Get-Date).AddSeconds($timeoutSeconds)
  while ((Get-Date) -lt $deadline) {{
    $proc = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object {{ $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }} | Select-Object -First 1
    if ($null -ne $proc) {{
      return $true
    }}
    Start-Sleep -Seconds 3
  }}
  return $false
}}

if (-not (Test-Path -LiteralPath $stepPath)) {{
  throw "STEP file was not found: $stepPath"
}}

$runningSolidWorks = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object {{ $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }} | Select-Object -First 1
if ($null -eq $runningSolidWorks) {{
  if (Test-Path -LiteralPath $solidWorksExe) {{
    Start-Process -FilePath $solidWorksExe -WorkingDirectory (Split-Path -LiteralPath $solidWorksExe)
    [void] (Wait-SolidWorksMainWindow 120)
  }} elseif (Test-Path -LiteralPath $solidWorksShortcut) {{
    Start-Process -FilePath $solidWorksShortcut
    [void] (Wait-SolidWorksMainWindow 120)
  }}
}}

if (Test-Path -LiteralPath $solidWorksOpenScript) {{
  Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
  $proc = Start-Process -FilePath "cscript.exe" -ArgumentList @("//Nologo", $solidWorksOpenScript, $stepPath) -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
  $deadline = (Get-Date).AddSeconds(300)
  while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {{
    Start-Sleep -Seconds 5
    $proc.Refresh()
  }}
  if (-not $proc.HasExited) {{
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-OpenStatus "manual_open_required" "SolidWorks API open timed out; selected STEP in Explorer: $stepPath"
    Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
    exit 0
  }}
  $output = ""
  if (Test-Path -LiteralPath $stdoutPath) {{
    $output = Get-Content -LiteralPath $stdoutPath -Raw -Encoding Default
  }}
  if ($output -match "active=") {{
    Write-OpenStatus "opened" "SolidWorks API reported active document for STEP: $stepPath"
    exit 0
  }}
  Write-OpenStatus "manual_open_required" "SolidWorks API did not confirm open; selected STEP in Explorer: $stepPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
  exit 0
}}

if (Test-Path -LiteralPath $solidWorksExe) {{
  Start-Process -FilePath $solidWorksExe -WorkingDirectory (Split-Path -LiteralPath $solidWorksExe)
  Write-OpenStatus "manual_open_required" "Started SolidWorks main window only; select STEP manually: $stepPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
  exit 0
}}

if (Test-Path -LiteralPath $solidWorksShortcut) {{
  Start-Process -FilePath $solidWorksShortcut
  Write-OpenStatus "manual_open_required" "Started SolidWorks shortcut only; select STEP manually: $stepPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
  exit 0
}}

Write-OpenStatus "manual_open_required" "SolidWorks executable not found; selected STEP in Explorer: $stepPath"
Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
"""
    write_powershell_script(ps1, script)
    cmd = f"""@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1" *> "%~dpn0.log"
if errorlevel 1 start "" notepad.exe "%~dpn0.log"
"""
    write_text(path, cmd)


def write_native_solidworks_launcher(path: Path, assembly_path: Path, dependency_manifest_path: Path | None = None) -> None:
    ps1 = path.with_suffix(".ps1")
    status_path = path.with_suffix(".status.txt")
    stdout_path = path.with_suffix(".solidworks_open.stdout.txt")
    stderr_path = path.with_suffix(".solidworks_open.stderr.txt")
    missing_dependency_path = path.with_suffix(".missing_dependencies.txt")
    script = f"""$ErrorActionPreference = 'Stop'
$assemblyPath = {ps_single_quote(str(assembly_path))}
$solidWorksExe = {ps_single_quote(str(SOLIDWORKS_EXE))}
$solidWorksShortcut = {ps_single_quote(str(SOLIDWORKS_SHORTCUT))}
$solidWorksOpenScript = {ps_single_quote(str(SOLIDWORKS_OPEN_SCRIPT))}
$dependencyManifestPath = {ps_single_quote(str(dependency_manifest_path or ""))}
$missingDependencyPath = {ps_single_quote(str(missing_dependency_path))}
$statusPath = {ps_single_quote(str(status_path))}
$stdoutPath = {ps_single_quote(str(stdout_path))}
$stderrPath = {ps_single_quote(str(stderr_path))}

function Write-OpenStatus([string] $status, [string] $message) {{
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $status | $message"
  Set-Content -LiteralPath $statusPath -Value $line -Encoding UTF8
}}

function Wait-SolidWorksMainWindow([int] $timeoutSeconds) {{
  $deadline = (Get-Date).AddSeconds($timeoutSeconds)
  while ((Get-Date) -lt $deadline) {{
    $proc = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object {{ $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }} | Select-Object -First 1
    if ($null -ne $proc) {{
      return $true
    }}
    Start-Sleep -Seconds 3
  }}
  return $false
}}

if (-not (Test-Path -LiteralPath $assemblyPath)) {{
  throw "Native SolidWorks assembly was not found: $assemblyPath"
}}

if ($dependencyManifestPath -and (Test-Path -LiteralPath $dependencyManifestPath)) {{
  $manifestRows = Import-Csv -LiteralPath $dependencyManifestPath
  $missingRows = @($manifestRows | Where-Object {{
    -not $_.source_path -or
    $_.exists -ne 'yes' -or
    -not (Test-Path -LiteralPath $_.source_path)
  }})
  if ($missingRows.Count -gt 0) {{
    $lines = @(
      "Missing SolidWorks native dependency references before opening:",
      "Assembly: $assemblyPath",
      "Manifest: $dependencyManifestPath",
      ""
    )
    foreach ($row in $missingRows) {{
      $lines += ("{0} | {1}" -f $row.role, $row.source_path)
    }}
    Set-Content -LiteralPath $missingDependencyPath -Value $lines -Encoding UTF8
    Write-OpenStatus "missing_dependency" "Missing $($missingRows.Count) native dependency reference(s). See: $missingDependencyPath"
    Start-Process -FilePath "notepad.exe" -ArgumentList @($missingDependencyPath)
    Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
    exit 1
  }}
}}

$runningSolidWorks = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object {{ $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }} | Select-Object -First 1
if ($null -eq $runningSolidWorks) {{
  if (Test-Path -LiteralPath $solidWorksExe) {{
    Start-Process -FilePath $solidWorksExe -WorkingDirectory (Split-Path -LiteralPath $solidWorksExe)
    [void] (Wait-SolidWorksMainWindow 120)
  }} elseif (Test-Path -LiteralPath $solidWorksShortcut) {{
    Start-Process -FilePath $solidWorksShortcut
    [void] (Wait-SolidWorksMainWindow 120)
  }}
}}

if (Test-Path -LiteralPath $solidWorksOpenScript) {{
  Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
  $proc = Start-Process -FilePath "cscript.exe" -ArgumentList @("//Nologo", $solidWorksOpenScript, $assemblyPath) -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
  $deadline = (Get-Date).AddSeconds(240)
  while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {{
    Start-Sleep -Seconds 5
    $proc.Refresh()
  }}
  if (-not $proc.HasExited) {{
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-OpenStatus "manual_open_required" "SolidWorks API open timed out; selected native assembly in Explorer: $assemblyPath"
    Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
    exit 0
  }}
  $output = ""
  if (Test-Path -LiteralPath $stdoutPath) {{
    $output = Get-Content -LiteralPath $stdoutPath -Raw -Encoding Default
  }}
  if ($output -match "active=") {{
    Write-OpenStatus "opened" "SolidWorks API reported active document for native assembly: $assemblyPath"
    exit 0
  }}
  Write-OpenStatus "manual_open_required" "SolidWorks API did not confirm open; selected native assembly in Explorer: $assemblyPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
  exit 0
}}

Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
Write-OpenStatus "manual_open_required" "Selected native assembly in Explorer: $assemblyPath"
"""
    write_powershell_script(ps1, script)
    cmd = f"""@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1" *> "%~dpn0.log"
if errorlevel 1 start "" notepad.exe "%~dpn0.log"
"""
    write_text(path, cmd)


def write_freecad_launcher(path: Path, model_path: Path) -> None:
    ps1 = path.with_suffix(".ps1")
    status_path = path.with_suffix(".status.txt")
    script = f"""$ErrorActionPreference = 'Stop'
$modelPath = {ps_single_quote(str(model_path))}
$freecadExe = {ps_single_quote(str(FREECAD_EXE))}
$statusPath = {ps_single_quote(str(status_path))}

if (-not (Test-Path -LiteralPath $modelPath)) {{
  throw "Model file was not found: $modelPath"
}}
if (-not (Test-Path -LiteralPath $freecadExe)) {{
  throw "FreeCAD.exe was not found: $freecadExe"
}}

Start-Process -FilePath $freecadExe -ArgumentList @($modelPath)
$line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | launched | Started FreeCAD with file=$modelPath"
Set-Content -LiteralPath $statusPath -Value $line -Encoding UTF8
"""
    write_powershell_script(ps1, script)
    cmd = f"""@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1" *> "%~dpn0.log"
if errorlevel 1 start "" notepad.exe "%~dpn0.log"
"""
    write_text(path, cmd)


def recommended_model_for_variant(variant: dict[str, Any]) -> str:
    status = variant.get("status")
    if status == "PASS_READY_FOR_ENGINEERING_REVIEW":
        return "Use the native SolidWorks enriched assembly first; STEP and FCStd remain neutral/open-source review backups."
    if status == "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT":
        return "Use the native SolidWorks enriched assembly first; use STEP as the neutral backup while FCStd integrity audit is pending."
    return "Do not use for engineering review until quality status improves."


def handoff_skip_reason(variant: dict[str, Any]) -> str | None:
    status = str(variant.get("status") or "")
    if status in HANDOFF_ALLOWED_STATUSES:
        return None
    if status == "PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT":
        return "Rule counts pass, but no STEP/FCStd geometry gate has passed yet."
    if status == "FAIL":
        return "Quality gate failed."
    if status == "MISSING_OUTPUT":
        return "Expected model output is missing."
    return f"Status is not approved for engineering handoff: {status or 'unknown'}."


def native_enriched_sources(door_count: int) -> dict[str, Path]:
    source_dir = ROOT_DIR / "workers" / "generated_models" / f"SW-NATIVE-16029-CABINET-ENRICHED-{door_count}DOOR-20260521"
    stem = f"native_16029_{door_count}door_cabinet_enriched_v2"
    return {
        "source_dir": source_dir,
        "assembly": source_dir / f"{stem}.SLDASM",
        "step": source_dir / f"{stem}.step",
        "placements": source_dir / f"{stem}_placements.tsv",
        "result_json": source_dir / f"{stem}_result.json",
        "bbox_json": source_dir / f"{stem}_step_bbox.json",
        "bbox_csv": source_dir / f"{stem}_step_bbox.csv",
        "validation_json": ROOT_DIR / "data" / f"solidworks_16029_enriched_{door_count}door_matrix_validation.json",
        "validation_md": ROOT_DIR / "data" / f"solidworks_16029_enriched_{door_count}door_matrix_validation.md",
        "validation_csv": ROOT_DIR / "data" / f"solidworks_16029_enriched_{door_count}door_matrix_validation.csv",
    }


def read_placements(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def write_native_dependency_manifest(placements_path: Path, target_path: Path) -> dict[str, Any]:
    rows = read_placements(placements_path)
    target_path.parent.mkdir(parents=True, exist_ok=True)
    with target_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["role", "source_path", "exists", "size_mb", "note"],
        )
        writer.writeheader()
        for row in rows:
            source_path = Path(row.get("path") or "")
            exists = source_path.exists()
            writer.writerow(
                {
                    "role": row.get("role", ""),
                    "source_path": str(source_path),
                    "exists": "yes" if exists else "no",
                    "size_mb": file_size_mb(source_path) if exists else "",
                    "note": "top-level placement reference; true independent delivery still needs SolidWorks Pack-and-Go",
                }
            )
    missing = [row for row in rows if not Path(row.get("path") or "").exists()]
    return {
        "path": str(target_path),
        "row_count": len(rows),
        "existing_count": len(rows) - len(missing),
        "missing_count": len(missing),
        "missing_roles": [row.get("role") for row in missing],
    }


def _check_status(checks: list[dict[str, Any]], name: str) -> bool | None:
    for check in checks:
        if check.get("name") == name:
            return bool(check.get("ok"))
    return None


def _pitch_value(summary: Any) -> float | None:
    if not isinstance(summary, dict):
        return None
    values = summary.get("values")
    if isinstance(values, list) and values:
        return values[0]
    return None


def read_native_validation_summary(validation_json_path: Path, door_count: int) -> dict[str, Any]:
    if not validation_json_path.exists():
        return {
            "path": str(validation_json_path),
            "exists": False,
            "ok": False,
            "check_count": 0,
            "failed_check_count": 1,
            "failed_checks": ["validation_json_missing"],
        }

    data = read_json(validation_json_path)
    checks = data.get("checks") or []
    failed_checks = [check for check in checks if not bool(check.get("ok"))]
    candidate_matches = data.get("candidate_matches") or []
    unmatched_candidates = [row for row in candidate_matches if not bool(row.get("matched"))]
    embedded = data.get("embedded_door_quality") or {}
    transform_summary = embedded.get("door_array_transform_summary") or {}
    left_pitch = embedded.get("left_column_pitch") or {}
    right_pitch = embedded.get("right_column_pitch") or {}
    shelf_pitch = embedded.get("shelf_pitch") or {}
    crossbar_pitch = embedded.get("crossbar_pitch") or {}
    bbox = data.get("combined_bbox_mm") or {}
    expected_per_column = int(door_count / 2)

    return {
        "path": str(validation_json_path),
        "exists": True,
        "ok": bool(data.get("ok")) and not failed_checks,
        "check_count": len(checks),
        "failed_check_count": len(failed_checks),
        "failed_checks": [str(check.get("name")) for check in failed_checks],
        "candidate_count": len(candidate_matches),
        "candidate_matched_count": len(candidate_matches) - len(unmatched_candidates),
        "candidate_unmatched_roles": [str(row.get("role")) for row in unmatched_candidates],
        "bbox_x_mm": bbox.get("x_len"),
        "bbox_y_mm": bbox.get("y_len"),
        "bbox_z_mm": bbox.get("z_len"),
        "reported_door_count": embedded.get("door_count"),
        "expected_door_count": door_count,
        "left_column_door_count": embedded.get("left_column_door_count"),
        "right_column_door_count": embedded.get("right_column_door_count"),
        "expected_column_door_count": expected_per_column,
        "door_array_transform_total": transform_summary.get("total"),
        "door_array_transform_ok": transform_summary.get("ok"),
        "door_array_transform_failed_roles": transform_summary.get("failed_roles") or [],
        "left_column_pitch_mm": _pitch_value(left_pitch),
        "left_column_pitch_max_error": left_pitch.get("max_error") if isinstance(left_pitch, dict) else None,
        "right_column_pitch_mm": _pitch_value(right_pitch),
        "right_column_pitch_max_error": right_pitch.get("max_error") if isinstance(right_pitch, dict) else None,
        "right_column_rotation_ok": _check_status(checks, "embedded_right_column_standard_rotation"),
        "left_right_y_alignment_ok": _check_status(checks, "embedded_left_right_door_y_alignment"),
        "door_weld_count": embedded.get("door_weld_count"),
        "door_panel_feature_count": embedded.get("door_panel_feature_count"),
        "hinge_pin_count": embedded.get("hinge_pin_count"),
        "lock_hook_pad_count": embedded.get("lock_hook_pad_count"),
        "electric_lock_hook_count": embedded.get("electric_lock_hook_count"),
        "shelf_count": embedded.get("shelf_count"),
        "shelf_pitch_mm": _pitch_value(shelf_pitch),
        "shelf_pitch_max_error": shelf_pitch.get("max_error") if isinstance(shelf_pitch, dict) else None,
        "crossbar_count": embedded.get("crossbar_count"),
        "crossbar_pitch_mm": _pitch_value(crossbar_pitch),
        "crossbar_pitch_max_error": crossbar_pitch.get("max_error") if isinstance(crossbar_pitch, dict) else None,
    }


def build_native_reference_handoff(door_count: int, target_dir: Path) -> dict[str, Any]:
    sources = native_enriched_sources(door_count)
    native_dir = target_dir / "solidworks_native"
    native_dir.mkdir(parents=True, exist_ok=True)

    assembly_target = native_dir / f"16029_1000W_1917H_550D_{door_count}door_enriched_v2.SLDASM"
    step_target = native_dir / f"16029_1000W_1917H_550D_{door_count}door_enriched_v2.step"
    placements_target = native_dir / f"16029_{door_count}door_enriched_v2_placements.tsv"
    result_target = native_dir / f"16029_{door_count}door_enriched_v2_result.json"
    bbox_json_target = native_dir / f"16029_{door_count}door_enriched_v2_step_bbox.json"
    bbox_csv_target = native_dir / f"16029_{door_count}door_enriched_v2_step_bbox.csv"
    validation_json_target = native_dir / f"solidworks_16029_enriched_{door_count}door_matrix_validation.json"
    validation_md_target = native_dir / f"solidworks_16029_enriched_{door_count}door_matrix_validation.md"
    validation_csv_target = native_dir / f"solidworks_16029_enriched_{door_count}door_matrix_validation.csv"

    transfers: dict[str, str | None] = {}
    transfers["assembly"] = ensure_link_or_copy(sources["assembly"], assembly_target) if sources["assembly"].exists() else None
    transfers["step"] = ensure_link_or_copy(sources["step"], step_target) if sources["step"].exists() else None
    transfers["placements"] = ensure_link_or_copy(sources["placements"], placements_target) if sources["placements"].exists() else None
    transfers["result_json"] = ensure_link_or_copy(sources["result_json"], result_target) if sources["result_json"].exists() else None
    transfers["bbox_json"] = ensure_link_or_copy(sources["bbox_json"], bbox_json_target) if sources["bbox_json"].exists() else None
    transfers["bbox_csv"] = ensure_link_or_copy(sources["bbox_csv"], bbox_csv_target) if sources["bbox_csv"].exists() else None
    transfers["validation_json"] = (
        ensure_link_or_copy(sources["validation_json"], validation_json_target) if sources["validation_json"].exists() else None
    )
    transfers["validation_md"] = (
        ensure_link_or_copy(sources["validation_md"], validation_md_target) if sources["validation_md"].exists() else None
    )
    transfers["validation_csv"] = (
        ensure_link_or_copy(sources["validation_csv"], validation_csv_target) if sources["validation_csv"].exists() else None
    )

    dependency_manifest = write_native_dependency_manifest(placements_target, native_dir / "native_dependency_manifest.csv")
    validation_summary = read_native_validation_summary(validation_json_target, door_count)
    launcher = native_dir / f"open_{door_count}door_native_enriched_solidworks.cmd"
    write_native_solidworks_launcher(launcher, assembly_target, Path(dependency_manifest["path"]))

    readme_path = native_dir / "README.md"
    readme = f"""# 16029 {door_count} door native SolidWorks enriched reference

Output level: engineering reference, not released production drawing.

## Open first

- Native SolidWorks assembly: `{assembly_target}`
- Safe launcher: `{launcher}`
- Neutral STEP from the same native assembly: `{step_target}`

## Validation

- Validation report: `{validation_md_target}`
- Validation data: `{validation_csv_target}`
- STEP bbox CSV: `{bbox_csv_target}`
- Builder result JSON: `{result_target}`
- Placement TSV: `{placements_target}`
- Dependency manifest: `{dependency_manifest['path']}`

## Native validation summary

- Result: `{"PASS" if validation_summary.get("ok") else "FAIL"}`
- Checks: `{validation_summary.get('check_count') - validation_summary.get('failed_check_count')}/{validation_summary.get('check_count')}`
- Door modules: `{validation_summary.get('reported_door_count')}` total, `{validation_summary.get('left_column_door_count')}` left, `{validation_summary.get('right_column_door_count')}` right
- Door hardware: weld `{validation_summary.get('door_weld_count')}`, panel `{validation_summary.get('door_panel_feature_count')}`, hinge `{validation_summary.get('hinge_pin_count')}`, lock hook `{validation_summary.get('electric_lock_hook_count')}`
- Shelf / crossbar: shelf `{validation_summary.get('shelf_count')}`, crossbar `{validation_summary.get('crossbar_count')}`

## Boundary

- This folder collects the current native enhanced reference and its evidence in one place.
- This is not a true independent Pack-and-Go release yet. The dependency manifest lists top-level source references that must stay available on this workstation.
- The launcher preflights the dependency manifest before opening SolidWorks and writes a missing-dependency report if references are not available.
- Use this before the older STEP-only handoff when reviewing in SolidWorks 2025.
"""
    write_text(readme_path, readme)

    return {
        "package_dir": str(native_dir),
        "assembly": str(assembly_target),
        "step": str(step_target),
        "solidworks_launcher": str(launcher),
        "placements_tsv": str(placements_target),
        "result_json": str(result_target),
        "bbox_json": str(bbox_json_target),
        "bbox_csv": str(bbox_csv_target),
        "validation_json": str(validation_json_target),
        "validation_md": str(validation_md_target),
        "validation_csv": str(validation_csv_target),
        "dependency_manifest": dependency_manifest,
        "validation_summary": validation_summary,
        "transfer": transfers,
        "boundary": "native engineering reference package; not independent Pack-and-Go",
    }


def build_variant_handoff(variant: dict[str, Any]) -> dict[str, Any]:
    door_count = int(variant["door_count"])
    target_dir = HANDOFF_DIR / f"{door_count:02d}door"
    target_dir.mkdir(parents=True, exist_ok=True)

    source_step = Path(str(variant.get("step") or ""))
    if not source_step.exists():
        raise FileNotFoundError(f"Missing STEP for {door_count}door: {source_step}")
    step_target = target_dir / f"16029_1000W_1917H_550D_{door_count}door_engineering_reference.step"
    step_transfer = ensure_link_or_copy(source_step, step_target)

    stp_target = target_dir / f"16029_1000W_1917H_550D_{door_count}door_solidworks_import.stp"
    stp_transfer = ensure_link_or_copy(source_step, stp_target)

    fcstd_target = None
    source_fcstd = Path(str(variant.get("fcstd") or ""))
    fcstd_transfer = None
    if source_fcstd.exists():
        fcstd_target = target_dir / f"16029_1000W_1917H_550D_{door_count}door_freecad_reference.FCStd"
        fcstd_transfer = ensure_link_or_copy(source_fcstd, fcstd_target)

    copied_report = copy_if_exists(variant.get("report_md"), target_dir)
    copied_verify = copy_if_exists(variant.get("verify_csv"), target_dir)
    step_check = variant.get("step_geometry_check", {})
    copied_step_check = copy_if_exists(step_check.get("path") if isinstance(step_check, dict) else None, target_dir)
    fcstd_integrity = variant.get("geometry_integrity", {})
    copied_fcstd_integrity = copy_if_exists(fcstd_integrity.get("path") if isinstance(fcstd_integrity, dict) else None, target_dir)
    structural_rule_audit = variant.get("structural_rule_audit", {})
    copied_structural_rule_audit = copy_if_exists(
        structural_rule_audit.get("markdown") if isinstance(structural_rule_audit, dict) else None,
        target_dir,
    )

    sw_launcher = target_dir / f"open_{door_count}door_step_in_solidworks.cmd"
    write_solidworks_launcher(sw_launcher, stp_target)
    freecad_launcher = target_dir / f"open_{door_count}door_reference_in_freecad.cmd"
    write_freecad_launcher(freecad_launcher, fcstd_target or step_target)
    native_reference = build_native_reference_handoff(door_count, target_dir)

    readme_path = target_dir / "README.md"
    readme = f"""# 16029 {door_count} door engineering handoff

Output level: engineering reference, not released production drawing.

## Recommended file

- Native SolidWorks enriched reference: `{native_reference['assembly']}`
- Native SolidWorks launcher: `{native_reference['solidworks_launcher']}`
- SolidWorks review: `{stp_target}`
- FreeCAD reference: `{fcstd_target or step_target}`
- Status: `{variant.get('status')}`
- Interpretation: {recommended_model_for_variant(variant)}

## Quality snapshot

- bbox X: `{variant.get('verify_bbox_x_len')}` mm
- verify rows: `{variant.get('verify_rows')}`
- STEP geometry: `{step_check.get('status') if isinstance(step_check, dict) else 'not_run'}`
- STEP invalid shapes: `{step_check.get('invalid_shape_count') if isinstance(step_check, dict) else ''}`
- FCStd integrity: `{fcstd_integrity.get('status') if isinstance(fcstd_integrity, dict) else 'not_run'}`
- Structural rule audit: `{structural_rule_audit.get('status') if isinstance(structural_rule_audit, dict) else 'not_run'}`
- Door height / pitch: `{structural_rule_audit.get('door_height_mm') if isinstance(structural_rule_audit, dict) else ''}` / `{structural_rule_audit.get('door_pitch_mm') if isinstance(structural_rule_audit, dict) else ''}` mm
- STEP size: `{file_size_mb(step_target)}` MB
- FCStd size: `{file_size_mb(fcstd_target) if fcstd_target else ''}` MB

## Open scripts

- `open_{door_count}door_step_in_solidworks.cmd`
- `open_{door_count}door_reference_in_freecad.cmd`

## Evidence

- report: `{copied_report or ''}`
- verify csv: `{copied_verify or ''}`
- STEP geometry json: `{copied_step_check or ''}`
- FCStd integrity report: `{copied_fcstd_integrity or ''}`
- structural rule audit: `{copied_structural_rule_audit or ''}`

## Native SolidWorks package

- package dir: `{native_reference['package_dir']}`
- dependency manifest: `{native_reference['dependency_manifest']['path']}`
- dependency rows: `{native_reference['dependency_manifest']['existing_count']}/{native_reference['dependency_manifest']['row_count']}`
- boundary: `{native_reference['boundary']}`
"""
    write_text(readme_path, readme)

    return {
        "door_count": door_count,
        "status": variant.get("status"),
        "status_label": STATUS_LABELS.get(str(variant.get("status")), str(variant.get("status"))),
        "handoff_dir": str(target_dir),
        "recommended_model": recommended_model_for_variant(variant),
        "step": str(step_target),
        "stp": str(stp_target),
        "fcstd": str(fcstd_target) if fcstd_target else None,
        "solidworks_launcher": str(sw_launcher),
        "freecad_launcher": str(freecad_launcher),
        "native_reference": native_reference,
        "report_md": copied_report,
        "verify_csv": copied_verify,
        "step_geometry_check_json": copied_step_check,
        "fcstd_integrity_md": copied_fcstd_integrity,
        "structural_rule_audit_md": copied_structural_rule_audit,
        "transfer": {
            "step": step_transfer,
            "stp": stp_transfer,
            "fcstd": fcstd_transfer,
        },
        "metrics": {
            "bbox_x_mm": variant.get("verify_bbox_x_len"),
            "verify_rows": variant.get("verify_rows"),
            "step_mb": file_size_mb(step_target),
            "fcstd_mb": file_size_mb(fcstd_target) if fcstd_target else None,
            "step_geometry_status": (variant.get("step_geometry_check") or {}).get("status"),
            "step_invalid_shape_count": (variant.get("step_geometry_check") or {}).get("invalid_shape_count"),
            "fcstd_integrity_status": (variant.get("geometry_integrity") or {}).get("status"),
            "structural_rule_status": (variant.get("structural_rule_audit") or {}).get("status"),
            "structural_rule_failed_checks": (variant.get("structural_rule_audit") or {}).get("failed_checks"),
            "door_width_mm": (variant.get("structural_rule_audit") or {}).get("door_width_mm"),
            "door_height_mm": (variant.get("structural_rule_audit") or {}).get("door_height_mm"),
            "door_pitch_mm": (variant.get("structural_rule_audit") or {}).get("door_pitch_mm"),
            "lock_center_x_abs_mm": (variant.get("structural_rule_audit") or {}).get("lock_center_x_abs_mm"),
            "hinge_axis_x_abs_mm": (variant.get("structural_rule_audit") or {}).get("hinge_axis_x_abs_mm"),
            "native_validation_ok": native_reference["validation_summary"].get("ok"),
            "native_validation_check_count": native_reference["validation_summary"].get("check_count"),
            "native_validation_failed_check_count": native_reference["validation_summary"].get("failed_check_count"),
            "native_reported_door_count": native_reference["validation_summary"].get("reported_door_count"),
            "native_left_column_door_count": native_reference["validation_summary"].get("left_column_door_count"),
            "native_right_column_door_count": native_reference["validation_summary"].get("right_column_door_count"),
            "native_right_column_rotation_ok": native_reference["validation_summary"].get("right_column_rotation_ok"),
            "native_door_weld_count": native_reference["validation_summary"].get("door_weld_count"),
            "native_door_panel_feature_count": native_reference["validation_summary"].get("door_panel_feature_count"),
            "native_hinge_pin_count": native_reference["validation_summary"].get("hinge_pin_count"),
            "native_lock_hook_pad_count": native_reference["validation_summary"].get("lock_hook_pad_count"),
            "native_electric_lock_hook_count": native_reference["validation_summary"].get("electric_lock_hook_count"),
            "native_shelf_count": native_reference["validation_summary"].get("shelf_count"),
            "native_crossbar_count": native_reference["validation_summary"].get("crossbar_count"),
        },
    }


def write_manifest_csv(rows: list[dict[str, Any]], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "door_count",
                "status_label",
                "bbox_x_mm",
                "step_geometry_status",
                "step_invalid_shape_count",
                "fcstd_integrity_status",
                "structural_rule_status",
                "door_width_mm",
                "door_height_mm",
                "door_pitch_mm",
                "step",
                "solidworks_launcher",
                "native_assembly",
                "native_solidworks_launcher",
                "native_dependency_manifest",
                "native_dependency_existing_count",
                "native_dependency_row_count",
                "native_dependency_missing_count",
                "native_validation_ok",
                "native_validation_check_count",
                "native_validation_failed_check_count",
                "native_reported_door_count",
                "native_left_column_door_count",
                "native_right_column_door_count",
                "native_right_column_rotation_ok",
                "native_door_weld_count",
                "native_door_panel_feature_count",
                "native_hinge_pin_count",
                "native_electric_lock_hook_count",
                "native_shelf_count",
                "native_crossbar_count",
                "handoff_dir",
            ],
        )
        writer.writeheader()
        for row in rows:
            metrics = row["metrics"]
            writer.writerow(
                {
                    "door_count": row["door_count"],
                    "status_label": row["status_label"],
                    "bbox_x_mm": metrics.get("bbox_x_mm"),
                    "step_geometry_status": metrics.get("step_geometry_status"),
                    "step_invalid_shape_count": metrics.get("step_invalid_shape_count"),
                    "fcstd_integrity_status": metrics.get("fcstd_integrity_status"),
                    "structural_rule_status": metrics.get("structural_rule_status"),
                    "door_width_mm": metrics.get("door_width_mm"),
                    "door_height_mm": metrics.get("door_height_mm"),
                    "door_pitch_mm": metrics.get("door_pitch_mm"),
                    "step": row["step"],
                    "solidworks_launcher": row["solidworks_launcher"],
                    "native_assembly": (row.get("native_reference") or {}).get("assembly"),
                    "native_solidworks_launcher": (row.get("native_reference") or {}).get("solidworks_launcher"),
                    "native_dependency_manifest": ((row.get("native_reference") or {}).get("dependency_manifest") or {}).get("path"),
                    "native_dependency_existing_count": ((row.get("native_reference") or {}).get("dependency_manifest") or {}).get("existing_count"),
                    "native_dependency_row_count": ((row.get("native_reference") or {}).get("dependency_manifest") or {}).get("row_count"),
                    "native_dependency_missing_count": ((row.get("native_reference") or {}).get("dependency_manifest") or {}).get("missing_count"),
                    "native_validation_ok": "yes" if metrics.get("native_validation_ok") else "no",
                    "native_validation_check_count": metrics.get("native_validation_check_count"),
                    "native_validation_failed_check_count": metrics.get("native_validation_failed_check_count"),
                    "native_reported_door_count": metrics.get("native_reported_door_count"),
                    "native_left_column_door_count": metrics.get("native_left_column_door_count"),
                    "native_right_column_door_count": metrics.get("native_right_column_door_count"),
                    "native_right_column_rotation_ok": "yes" if metrics.get("native_right_column_rotation_ok") else "no",
                    "native_door_weld_count": metrics.get("native_door_weld_count"),
                    "native_door_panel_feature_count": metrics.get("native_door_panel_feature_count"),
                    "native_hinge_pin_count": metrics.get("native_hinge_pin_count"),
                    "native_electric_lock_hook_count": metrics.get("native_electric_lock_hook_count"),
                    "native_shelf_count": metrics.get("native_shelf_count"),
                    "native_crossbar_count": metrics.get("native_crossbar_count"),
                    "handoff_dir": row["handoff_dir"],
                }
            )


def write_root_launchers(payload: dict[str, Any]) -> list[dict[str, Any]]:
    launchers: list[dict[str, Any]] = []
    handoff_dir = Path(payload["handoff_dir"])
    for row in payload["variants"]:
        door_count = int(row["door_count"])
        native_reference = row.get("native_reference") or {}
        solidworks_launcher = Path(native_reference.get("solidworks_launcher") or row["solidworks_launcher"])
        root_launcher = handoff_dir / f"open_{door_count}door_in_solidworks.cmd"
        cmd = f"""@echo off
call "{solidworks_launcher}"
"""
        write_text(root_launcher, cmd)
        launchers.append(
            {
                "door_count": door_count,
                "solidworks_launcher": str(root_launcher),
                "target_launcher": str(solidworks_launcher),
                "stp": row["stp"],
                "native_assembly": native_reference.get("assembly"),
                "step_launcher": row["solidworks_launcher"],
            }
        )
    return launchers


def yes_no(value: Any) -> str:
    return "yes" if bool(value) else "no"


def write_dependency_summary_csv(payload: dict[str, Any]) -> str:
    path = Path(payload["handoff_dir"]) / "handoff_dependency_summary.csv"
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = ["door_count", "role", "source_path", "exists", "size_mb", "package_dir", "manifest"]
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in payload["variants"]:
            native_reference = row.get("native_reference") or {}
            manifest_path = Path(((native_reference.get("dependency_manifest") or {}).get("path")) or "")
            if not manifest_path.exists():
                writer.writerow(
                    {
                        "door_count": row["door_count"],
                        "role": "dependency_manifest_missing",
                        "source_path": "",
                        "exists": "no",
                        "size_mb": "",
                        "package_dir": native_reference.get("package_dir", ""),
                        "manifest": str(manifest_path),
                    }
                )
                continue
            with manifest_path.open("r", encoding="utf-8-sig", newline="") as manifest_handle:
                for manifest_row in csv.DictReader(manifest_handle):
                    source_path = Path(manifest_row.get("source_path") or "")
                    exists = source_path.exists()
                    writer.writerow(
                        {
                            "door_count": row["door_count"],
                            "role": manifest_row.get("role", ""),
                            "source_path": str(source_path),
                            "exists": "yes" if exists else "no",
                            "size_mb": file_size_mb(source_path) if exists else "",
                            "package_dir": native_reference.get("package_dir", ""),
                            "manifest": str(manifest_path),
                        }
                    )
    return str(path)


def write_quality_summary_csv(payload: dict[str, Any]) -> str:
    path = Path(payload["handoff_dir"]) / "handoff_quality_summary.csv"
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "door_count",
        "native_validation_ok",
        "native_check_count",
        "native_failed_check_count",
        "dependency_existing_count",
        "dependency_row_count",
        "dependency_missing_count",
        "bbox_x_mm",
        "bbox_z_mm",
        "reported_door_count",
        "left_column_door_count",
        "right_column_door_count",
        "right_column_rotation_ok",
        "door_weld_count",
        "door_panel_feature_count",
        "hinge_pin_count",
        "lock_hook_pad_count",
        "electric_lock_hook_count",
        "shelf_count",
        "crossbar_count",
        "candidate_matched_count",
        "candidate_count",
        "validation_report",
        "native_assembly",
        "open_script",
    ]
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in payload["variants"]:
            native_reference = row.get("native_reference") or {}
            validation = native_reference.get("validation_summary") or {}
            dependencies = native_reference.get("dependency_manifest") or {}
            writer.writerow(
                {
                    "door_count": row["door_count"],
                    "native_validation_ok": yes_no(validation.get("ok")),
                    "native_check_count": validation.get("check_count"),
                    "native_failed_check_count": validation.get("failed_check_count"),
                    "dependency_existing_count": dependencies.get("existing_count"),
                    "dependency_row_count": dependencies.get("row_count"),
                    "dependency_missing_count": dependencies.get("missing_count"),
                    "bbox_x_mm": validation.get("bbox_x_mm"),
                    "bbox_z_mm": validation.get("bbox_z_mm"),
                    "reported_door_count": validation.get("reported_door_count"),
                    "left_column_door_count": validation.get("left_column_door_count"),
                    "right_column_door_count": validation.get("right_column_door_count"),
                    "right_column_rotation_ok": yes_no(validation.get("right_column_rotation_ok")),
                    "door_weld_count": validation.get("door_weld_count"),
                    "door_panel_feature_count": validation.get("door_panel_feature_count"),
                    "hinge_pin_count": validation.get("hinge_pin_count"),
                    "lock_hook_pad_count": validation.get("lock_hook_pad_count"),
                    "electric_lock_hook_count": validation.get("electric_lock_hook_count"),
                    "shelf_count": validation.get("shelf_count"),
                    "crossbar_count": validation.get("crossbar_count"),
                    "candidate_matched_count": validation.get("candidate_matched_count"),
                    "candidate_count": validation.get("candidate_count"),
                    "validation_report": native_reference.get("validation_md"),
                    "native_assembly": native_reference.get("assembly"),
                    "open_script": native_reference.get("solidworks_launcher"),
                }
            )
    return str(path)


def write_handoff_ready_summary(payload: dict[str, Any]) -> str:
    path = Path(payload["handoff_dir"]) / "HANDOFF_READY_SUMMARY.md"
    root_launchers = {int(row["door_count"]): row for row in payload.get("root_launchers", [])}
    lines = [
        "# 16029 工程交付包打开前检查",
        "",
        "用途：在结构工程师打开 SolidWorks 前，先确认 10/12/14 门原生增强装配体的依赖和结构质量门是否通过。",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- One-click check: `{payload.get('handoff_preflight_cmd', '')}`",
        f"- Quality CSV: `{payload.get('quality_summary_csv', '')}`",
        f"- Dependency CSV: `{payload.get('dependency_summary_csv', '')}`",
        "",
        "## 快速结论",
        "",
        "| 门数 | 根目录打开脚本 | 依赖 | 原生验证 | 门模块 | 右门镜像 | 门/铰链/锁 | 层板/横档 | bbox X/Z |",
        "| ---: | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for row in payload["variants"]:
        door_count = int(row["door_count"])
        native_reference = row.get("native_reference") or {}
        validation = native_reference.get("validation_summary") or {}
        dependencies = native_reference.get("dependency_manifest") or {}
        root_launcher = root_launchers.get(door_count, {}).get("solidworks_launcher", native_reference.get("solidworks_launcher", ""))
        dep_text = f"{dependencies.get('existing_count')}/{dependencies.get('row_count')} missing={dependencies.get('missing_count')}"
        validation_text = f"{'PASS' if validation.get('ok') else 'FAIL'} {validation.get('check_count', 0) - validation.get('failed_check_count', 0)}/{validation.get('check_count', 0)}"
        door_text = (
            f"{validation.get('reported_door_count')}"
            f" ({validation.get('left_column_door_count')}/{validation.get('right_column_door_count')})"
        )
        hardware_text = (
            f"weld {validation.get('door_weld_count')}; "
            f"panel {validation.get('door_panel_feature_count')}; "
            f"hinge {validation.get('hinge_pin_count')}; "
            f"lock {validation.get('electric_lock_hook_count')}"
        )
        shelf_text = f"shelf {validation.get('shelf_count')}; crossbar {validation.get('crossbar_count')}"
        bbox_text = f"{validation.get('bbox_x_mm')}/{validation.get('bbox_z_mm')} mm"
        lines.append(
            "| "
            + " | ".join(
                [
                    str(door_count),
                    f"`{root_launcher}`",
                    dep_text,
                    validation_text,
                    door_text,
                    "PASS" if validation.get("right_column_rotation_ok") else "FAIL",
                    hardware_text,
                    shelf_text,
                    bbox_text,
                ]
            )
            + " |"
        )

    lines.extend(
        [
            "",
            "## 当前工程边界",
            "",
            "- 这些文件是工程参考模型，用于结构规则复核、门数变化对比和方案沟通。",
            "- 当前可复核范围是 16029 外形 1000W x 1917H x 550D 的 10/12/14 门。",
            "- 依赖清单为当前工作站路径校验，不等同于独立 Pack-and-Go 包。",
            "- 工程图、BOM、DXF、钣金展开仍属于下一阶段工程化输出。",
            "",
        ]
    )
    write_text(path, "\n".join(lines))
    return str(path)


def write_handoff_ready_preflight(payload: dict[str, Any]) -> dict[str, str]:
    cmd_path = Path(payload["handoff_dir"]) / "CHECK_HANDOFF_READY.cmd"
    ps1_path = cmd_path.with_suffix(".ps1")
    status_path = cmd_path.with_suffix(".status.txt")
    dependency_csv = Path(payload["dependency_summary_csv"])
    quality_csv = Path(payload["quality_summary_csv"])
    script = f"""$ErrorActionPreference = 'Stop'
$dependencyCsv = {ps_single_quote(str(dependency_csv))}
$qualityCsv = {ps_single_quote(str(quality_csv))}
$statusPath = {ps_single_quote(str(status_path))}

$lines = @()
$lines += "16029 handoff ready check"
$lines += "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$lines += ""

if (-not (Test-Path -LiteralPath $dependencyCsv)) {{
  $lines += "FAIL: dependency summary CSV missing: $dependencyCsv"
  Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
  exit 1
}}
if (-not (Test-Path -LiteralPath $qualityCsv)) {{
  $lines += "FAIL: quality summary CSV missing: $qualityCsv"
  Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
  exit 1
}}

$dependencyRows = @(Import-Csv -LiteralPath $dependencyCsv)
$missingDependencies = @($dependencyRows | Where-Object {{
  -not $_.source_path -or
  $_.exists -ne 'yes' -or
  -not (Test-Path -LiteralPath $_.source_path)
}})

$qualityRows = @(Import-Csv -LiteralPath $qualityCsv)
$qualityFailures = @($qualityRows | Where-Object {{
  $_.native_validation_ok -ne 'yes' -or
  [int]$_.native_failed_check_count -ne 0 -or
  [int]$_.dependency_missing_count -ne 0
}})

if ($missingDependencies.Count -gt 0 -or $qualityFailures.Count -gt 0) {{
  $lines += "FAIL: handoff is not ready."
  $lines += "Missing dependencies: $($missingDependencies.Count)"
  foreach ($row in $missingDependencies) {{
    $lines += ("  {{0}}door | {{1}} | {{2}}" -f $row.door_count, $row.role, $row.source_path)
  }}
  $lines += "Quality failures: $($qualityFailures.Count)"
  foreach ($row in $qualityFailures) {{
    $lines += ("  {{0}}door | validation={{1}} | failed_checks={{2}} | missing_dependencies={{3}}" -f $row.door_count, $row.native_validation_ok, $row.native_failed_check_count, $row.dependency_missing_count)
  }}
  Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
  exit 1
}}

$lines += "PASS: 10/12/14 native SolidWorks references are ready for engineering review on this workstation."
$lines += "Dependency rows: $($dependencyRows.Count)"
foreach ($row in $qualityRows) {{
  $lines += ("  {{0}}door | checks={{1}}/{{2}} | dependencies={{3}}/{{4}} | doors={{5}} | shelves={{6}} | crossbars={{7}}" -f $row.door_count, ([int]$row.native_check_count - [int]$row.native_failed_check_count), $row.native_check_count, $row.dependency_existing_count, $row.dependency_row_count, $row.reported_door_count, $row.shelf_count, $row.crossbar_count)
}}
Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
exit 0
"""
    write_powershell_script(ps1_path, script)
    cmd = """@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1"
set EXITCODE=%ERRORLEVEL%
start "" notepad.exe "%~dpn0.status.txt"
exit /b %EXITCODE%
"""
    write_text(cmd_path, cmd)
    return {
        "cmd": str(cmd_path),
        "ps1": str(ps1_path),
        "status": str(status_path),
    }


def write_engineer_open_index(payload: dict[str, Any]) -> str:
    index_path = Path(payload["handoff_dir"]) / "ENGINEER_OPEN_INDEX.md"
    solidworks_open_verification = payload.get("solidworks_open_verification")
    lines = [
        "# 16029 工程师打开清单",
        "",
        "用途：给结构工程师快速打开 10/12/14 门工程参考模型。这里的模型用于复核结构规则和方案，不是正式生产图纸、BOM 或 DXF。",
        "",
        "## 推荐操作",
        "",
        "1. 先打开对应门数的根目录脚本，例如 `open_12door_in_solidworks.cmd`。",
        "2. 脚本会优先打开原生 SolidWorks 增强样机 `.SLDASM`，这是当前给结构工程师复核的主文件。",
        "3. 如果 API 打开未确认，脚本会在资源管理器中选中原生装配体，由工程师在 SolidWorks 里手动 File > Open。",
        "4. 只有需要中性格式复核时，再进入对应门数目录打开 `.stp`；需要看 FreeCAD 参考时再打开 `.FCStd`。",
        "5. 不要再使用历史 direct assembly 逐零件装配任务；该路线已因装配基准/transform 错乱停用。",
        "",
        "## 可打开模型",
        "",
        "| 门数 | SolidWorks 安全脚本 | 原生增强装配 | STP 备选 | 门高 | 门距 | 质量结论 |",
        "| ---: | --- | --- | --- | ---: | ---: | --- |",
    ]
    root_launchers = {int(row["door_count"]): row for row in payload.get("root_launchers", [])}
    for row in payload["variants"]:
        metrics = row["metrics"]
        door_count = int(row["door_count"])
        root_launcher = root_launchers.get(door_count, {}).get("solidworks_launcher", row["solidworks_launcher"])
        quality = (
            f"STEP {metrics.get('step_geometry_status')}; "
            f"FCStd {metrics.get('fcstd_integrity_status')}; "
            f"结构 {metrics.get('structural_rule_status')}"
        )
        lines.append(
            "| "
            + " | ".join(
                [
                    str(door_count),
                    f"`{root_launcher}`",
                    f"`{(row.get('native_reference') or {}).get('assembly', '')}`",
                    f"`{row['stp']}`",
                    str(metrics.get("door_height_mm") or ""),
                    str(metrics.get("door_pitch_mm") or ""),
                    quality,
                ]
            )
            + " |"
        )
    if solidworks_open_verification:
        lines.extend(
            [
                "",
                "## 软件实测状态",
                "",
                "- 已做 SolidWorks 2025 受控打开验证：主程序可见启动通过，STEP 自动导入曾不稳定。",
                "- 结论：当前根脚本已改为优先打开原生 SLDASM；STEP/FCStd 作为中性和开源复核备选。",
                f"- 验证记录：`{solidworks_open_verification}`",
                "",
            ]
        )
    if payload.get("solidworks_native_open_verification"):
        lines.extend(
            [
                "- 原生 SLDASM 打开验证：10 门增强样机已通过 SolidWorks API active document 确认。",
                f"- 原生验证记录：`{payload['solidworks_native_open_verification']}`",
                "",
            ]
        )
    lines.extend(
        [
            "",
            "## 交付边界",
            "",
            "- 当前可靠范围：16029 外形 1000W x 1917H x 550D，10/12/14 门。",
            "- 当前模型类型：工程参考模型，适合方案复核、规则验证、结构对比。",
            "- 未完成项：正式 SolidWorks 可编辑装配树、工程图、BOM、DXF、钣金展开仍需后续工程化。",
            "",
        ]
    )
    write_text(index_path, "\n".join(lines))
    return str(index_path)


def write_engineer_open_index_clean(payload: dict[str, Any]) -> str:
    index_path = Path(payload["handoff_dir"]) / "ENGINEER_OPEN_INDEX.md"
    solidworks_open_verification = payload.get("solidworks_open_verification")

    def cell(value: Any) -> str:
        return str(value or "").replace("|", "/")

    lines = [
        "# 16029 工程师打开清单",
        "",
        "用途：给结构工程师快速打开 10/12/14 门工程参考模型。这里的模型用于复核结构规则和方案，不是正式生产图纸、BOM 或 DXF。",
        "",
        "## 推荐操作",
        "",
        "1. 先运行 `CHECK_HANDOFF_READY.cmd`，确认依赖和结构质量门都是 PASS。",
        "2. 再打开对应门数的根目录脚本，例如 `open_12door_in_solidworks.cmd`。",
        "3. 脚本会优先打开原生 SolidWorks 增强样机 `.SLDASM`，这是当前给结构工程师复核的主文件。",
        "4. 如果 API 打开未确认，脚本会在资源管理器中选中原生装配体，由工程师在 SolidWorks 里手动 File > Open。",
        "5. 只有需要中性格式复核时，再进入对应门数目录打开 `.stp`；需要看 FreeCAD 参考时再打开 `.FCStd`。",
        "6. 不要再使用历史 direct assembly 逐零件装配任务；该路线已因装配基准 transform 错乱停用。",
        "",
        "## 打开前检查",
        "",
        f"- 一键检查：`{cell((payload.get('handoff_preflight') or {}).get('cmd'))}`",
        f"- 检查汇总：`{cell(payload.get('handoff_ready_summary'))}`",
        f"- 质量 CSV：`{cell(payload.get('quality_summary_csv'))}`",
        f"- 依赖 CSV：`{cell(payload.get('dependency_summary_csv'))}`",
        "",
        "## 可打开模型",
        "",
        "| 门数 | SolidWorks 安全脚本 | 原生增强装配 | STP 备用 | 门高 mm | 门距 mm | 原生验证 | 结构结论 |",
        "| ---: | --- | --- | --- | ---: | ---: | --- | --- |",
    ]
    root_launchers = {int(row["door_count"]): row for row in payload.get("root_launchers", [])}
    for row in payload["variants"]:
        metrics = row["metrics"]
        door_count = int(row["door_count"])
        native_reference = row.get("native_reference") or {}
        root_launcher = root_launchers.get(door_count, {}).get("solidworks_launcher", row["solidworks_launcher"])
        quality = (
            f"STEP {metrics.get('step_geometry_status')}; "
            f"FCStd {metrics.get('fcstd_integrity_status')}; "
            f"结构 {metrics.get('structural_rule_status')}"
        )
        native_check_count = metrics.get("native_validation_check_count") or 0
        native_failed_check_count = metrics.get("native_validation_failed_check_count") or 0
        native_quality = (
            f"{'PASS' if metrics.get('native_validation_ok') else 'FAIL'} "
            f"{native_check_count - native_failed_check_count}"
            f"/{native_check_count}"
        )
        lines.append(
            "| "
            + " | ".join(
                [
                    str(door_count),
                    f"`{cell(root_launcher)}`",
                    f"`{cell(native_reference.get('assembly'))}`",
                    f"`{cell(row['stp'])}`",
                    cell(metrics.get("door_height_mm")),
                    cell(metrics.get("door_pitch_mm")),
                    cell(native_quality),
                    cell(quality),
                ]
            )
            + " |"
        )

    lines.extend(
        [
            "",
            "## 软件实测状态",
            "",
            "- 已做 SolidWorks 2025 受控打开验证：主程序可启动，原生 SLDASM 是当前优先交付通道。",
            "- STEP/FCStd 作为中性格式和开源复核备选，不作为当前 SolidWorks 主交付入口。",
        ]
    )
    if solidworks_open_verification:
        lines.append(f"- 验证记录：`{solidworks_open_verification}`")
    if payload.get("solidworks_native_open_verification"):
        lines.append(f"- 原生验证记录：`{payload['solidworks_native_open_verification']}`")

    lines.extend(
        [
            "",
            "## 交付边界",
            "",
            "- 当前可靠范围：16029 外形 1000W x 1917H x 550D，10/12/14 门。",
            "- 当前模型类型：工程参考模型，适合方案复核、规则验证、结构对比。",
            "- 未完成项：正式 SolidWorks 可编辑装配树、工程图、BOM、DXF、钣金展开仍需后续工程化。",
            "",
        ]
    )
    write_text(index_path, "\n".join(lines))
    return str(index_path)


def write_root_readme(payload: dict[str, Any]) -> None:
    pending_fcstd = [
        row["door_count"]
        for row in payload["variants"]
        if (row.get("metrics") or {}).get("fcstd_integrity_status") != "PASS"
    ]
    solidworks_open_verification = payload.get("solidworks_open_verification")
    lines = [
        "# 16029 10/12/14 door engineering handoff",
        "",
        "Output level: engineering reference, not released production drawing.",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Handoff directory: `{payload['handoff_dir']}`",
        f"- Source quality matrix: `{payload['source_quality_matrix']}`",
        "",
        "## Files to open",
        "",
        "| Door count | Status | Recommended SolidWorks file | Open script | Key quality |",
        "| ---: | --- | --- | --- | --- |",
    ]
    for row in payload["variants"]:
        metrics = row["metrics"]
        native_reference = row.get("native_reference") or {}
        lines.append(
            "| "
            + " | ".join(
                [
                    str(row["door_count"]),
                    row["status_label"],
                    f"`{native_reference.get('assembly') or row['stp']}`",
                    f"`{native_reference.get('solidworks_launcher') or row['solidworks_launcher']}`",
                    f"bbox X={metrics.get('bbox_x_mm')}mm; STEP={metrics.get('step_geometry_status')}; invalid={metrics.get('step_invalid_shape_count')}; FCStd={metrics.get('fcstd_integrity_status')}; structural={metrics.get('structural_rule_status')}",
                ]
            )
            + " |"
        )
    skipped_variants = payload.get("skipped_variants", [])
    if skipped_variants:
        lines.extend(
            [
                "",
                "## Skipped variants",
                "",
                "| Door count | Status | Reason |",
                "| ---: | --- | --- |",
            ]
        )
        for row in skipped_variants:
            lines.append(
                "| "
                + " | ".join(
                    [
                        str(row.get("door_count", "")),
                        str(row.get("status", "")),
                        str(row.get("reason", "")).replace("|", "/"),
                    ]
                )
                + " |"
            )
    lines.extend(
        [
            "",
        "## Engineer quick open",
        "",
        f"- Chinese index: `{payload.get('engineer_open_index', '')}`",
        f"- Handoff ready summary: `{payload.get('handoff_ready_summary', '')}`",
        f"- One-click ready check: `{(payload.get('handoff_preflight') or {}).get('cmd', '')}`",
        f"- Quality summary CSV: `{payload.get('quality_summary_csv', '')}`",
        f"- Dependency summary CSV: `{payload.get('dependency_summary_csv', '')}`",
        "- Root launchers are available as `open_10door_in_solidworks.cmd`, `open_12door_in_solidworks.cmd`, and `open_14door_in_solidworks.cmd`.",
        "- The launchers start the SolidWorks main window first, try API open on the native enriched `.SLDASM`, and fall back to selecting the native assembly in Explorer.",
            "",
            "## Software verification",
            "",
        "- Native SolidWorks enriched assemblies, STEP exports, and FCStd/STEP quality gates are available for this bundle.",
        "- Native validation covers door left/right placement, right-door 180 degree rotation, door module counts, hinge/lock counts, shelf levels, front-frame crossbars, fixed-module bbox matching, and source dependency presence.",
        "- This is a native engineering-reference package, not a true independent Pack-and-Go release yet.",
        f"- SolidWorks open verification: `{solidworks_open_verification or ''}`",
            f"- Native SolidWorks open verification: `{payload.get('solidworks_native_open_verification') or ''}`",
            "",
            "## Use rules",
            "",
            "- Prefer the native enriched `.SLDASM` files for SolidWorks engineering review on this workstation.",
            "- Keep the listed source dependency paths available until true Pack-and-Go is implemented.",
            "- Use `.stp` files when a neutral exchange file is required.",
            "- Use `.FCStd` as FreeCAD reference after confirming the FCStd integrity status is PASS.",
            "- Do not treat these files as production drawings or released BOM/DXF.",
        "",
        ]
    )
    if pending_fcstd:
        lines.insert(
            -1,
            "- Door variants with pending FCStd integrity should be reviewed through STEP first: "
            + ", ".join(f"{door_count}door" for door_count in pending_fcstd)
            + ".",
        )
    root_text = "\n".join(lines)
    write_text(Path(payload["handoff_dir"]) / "ENGINEERING_HANDOFF.md", root_text)
    write_text(Path(payload["handoff_dir"]) / "README.md", root_text)
    write_text(HANDOFF_MARKDOWN_PATH, root_text)


def build_notes(variants: list[dict[str, Any]]) -> list[str]:
    pending_fcstd = [
        row["door_count"]
        for row in variants
        if (row.get("metrics") or {}).get("fcstd_integrity_status") != "PASS"
    ]
    notes = [
        "This handoff bundle is for engineering review only.",
        "SolidWorks users should open the native enriched .SLDASM files first; generated SolidWorks scripts use a safe API-open attempt and manual fallback.",
    ]
    if pending_fcstd:
        notes.append(
            "Some variants have STEP geometry PASS but still need FCStd integrity audit before FreeCAD-native review: "
            + ", ".join(f"{door_count}door" for door_count in pending_fcstd)
            + "."
        )
    else:
        notes.append("All included variants have STEP geometry PASS and FCStd integrity PASS.")
    if all((row.get("metrics") or {}).get("structural_rule_status") == "PASS" for row in variants):
        notes.append("All included variants have structural rule audit PASS for door grid, lock relation, shelf/frame offsets, L/R symmetry, and bbox X.")
    if SOLIDWORKS_OPEN_VERIFICATION_PATH.exists():
        notes.append("SolidWorks visual-open automation is tracked separately; see the SolidWorks open verification report.")
    if SOLIDWORKS_NATIVE_OPEN_VERIFICATION_PATH.exists():
        notes.append("Native SolidWorks 10-door enriched SLDASM open smoke passed; see the native open verification report.")
    return notes


def build_payload() -> dict[str, Any]:
    matrix = read_json(QUALITY_MATRIX_PATH)
    HANDOFF_DIR.mkdir(parents=True, exist_ok=True)
    source_variants = matrix.get("variants", [])
    variants = [build_variant_handoff(variant) for variant in source_variants if handoff_skip_reason(variant) is None]
    skipped_variants = [
        {
            "door_count": variant.get("door_count"),
            "status": variant.get("status"),
            "reason": reason,
        }
        for variant in source_variants
        if (reason := handoff_skip_reason(variant)) is not None
    ]
    manifest_csv = HANDOFF_DIR / "handoff_manifest.csv"
    write_manifest_csv(variants, manifest_csv)
    payload = {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "scope": "1000W x 1917H x 550D 10/12/14 door engineering-reference handoff",
        "source_quality_matrix": str(QUALITY_MATRIX_PATH),
        "handoff_dir": str(HANDOFF_DIR),
        "manifest_csv": str(manifest_csv),
        "handoff_gate": {
            "allowed_statuses": sorted(HANDOFF_ALLOWED_STATUSES),
            "included_count": len(variants),
            "skipped_count": len(skipped_variants),
        },
        "variants": variants,
        "skipped_variants": skipped_variants,
        "notes": build_notes(variants),
        "solidworks_open_verification": str(SOLIDWORKS_OPEN_VERIFICATION_PATH)
        if SOLIDWORKS_OPEN_VERIFICATION_PATH.exists()
        else None,
        "solidworks_native_open_verification": str(SOLIDWORKS_NATIVE_OPEN_VERIFICATION_PATH)
        if SOLIDWORKS_NATIVE_OPEN_VERIFICATION_PATH.exists()
        else None,
    }
    payload["root_launchers"] = write_root_launchers(payload)
    payload["dependency_summary_csv"] = write_dependency_summary_csv(payload)
    payload["quality_summary_csv"] = write_quality_summary_csv(payload)
    payload["handoff_preflight"] = write_handoff_ready_preflight(payload)
    payload["handoff_preflight_cmd"] = payload["handoff_preflight"]["cmd"]
    payload["handoff_ready_summary"] = write_handoff_ready_summary(payload)
    payload["engineer_open_index"] = write_engineer_open_index_clean(payload)
    write_root_readme(payload)
    return payload


def main() -> None:
    payload = build_payload()
    HANDOFF_MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    HANDOFF_MANIFEST_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(
        json.dumps(
            {
                "status": "ok",
                "handoff_dir": payload["handoff_dir"],
                "variants": len(payload["variants"]),
                "manifest": str(HANDOFF_MANIFEST_PATH),
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
