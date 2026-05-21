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
  if ($proc.ExitCode -eq 0 -and $output -match "active=") {{
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
        return "FCStd and STEP are both available; STEP is the SolidWorks-neutral handoff."
    if status == "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT":
        return "Use STEP for SolidWorks review; FCStd remains a FreeCAD reference until integrity audit passes."
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

    readme_path = target_dir / "README.md"
    readme = f"""# 16029 {door_count} door engineering handoff

Output level: engineering reference, not released production drawing.

## Recommended file

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
                    "handoff_dir": row["handoff_dir"],
                }
            )


def write_root_launchers(payload: dict[str, Any]) -> list[dict[str, Any]]:
    launchers: list[dict[str, Any]] = []
    handoff_dir = Path(payload["handoff_dir"])
    for row in payload["variants"]:
        door_count = int(row["door_count"])
        solidworks_launcher = Path(row["solidworks_launcher"])
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
            }
        )
    return launchers


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
        "2. 脚本会先启动 SolidWorks 主程序，再尝试 API 打开已审计 `.stp` 交接文件。",
        "3. 如果 API 打开未确认，脚本会在资源管理器中选中 STEP 文件，由工程师在 SolidWorks 里手动 File > Open。",
        "4. 只在需要看 FreeCAD 原生参考时，再进入对应门数目录打开 `.FCStd`。",
        "5. 不要再使用历史 direct assembly 逐零件装配任务；该路线已因装配基准/transform 错乱停用。",
        "",
        "## 可打开模型",
        "",
        "| 门数 | SolidWorks 安全脚本 | STP 交接文件 | 门高 | 门距 | 质量结论 |",
        "| ---: | --- | --- | ---: | ---: | --- |",
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
                "- 已做 SolidWorks 2025 受控打开验证：主程序可见启动通过，但 12 门 STP 自动导入未确认成功。",
                "- 结论：STEP/FCStd 几何质量可作为工程参考，SolidWorks API/一键可视化打开仍按阻塞项跟踪。",
                f"- 验证记录：`{solidworks_open_verification}`",
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
        lines.append(
            "| "
            + " | ".join(
                [
                    str(row["door_count"]),
                    row["status_label"],
                    f"`{row['stp']}`",
                    f"`{row['solidworks_launcher']}`",
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
            "- Root launchers are available as `open_10door_in_solidworks.cmd`, `open_12door_in_solidworks.cmd`, and `open_14door_in_solidworks.cmd`.",
            "- The launchers start the SolidWorks main window first, try API open, and fall back to selecting the STEP file in Explorer.",
            "",
            "## Software verification",
            "",
            "- STEP and FCStd quality gates are available for this bundle.",
            "- SolidWorks API/visual-open automation is not production-ready until a visible document open is confirmed.",
            f"- SolidWorks open verification: `{solidworks_open_verification or ''}`",
            "",
            "## Use rules",
            "",
            "- Prefer the `.stp` files for SolidWorks engineering review.",
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
    write_text(Path(payload["handoff_dir"]) / "ENGINEERING_HANDOFF.md", "\n".join(lines))
    write_text(HANDOFF_MARKDOWN_PATH, "\n".join(lines))


def build_notes(variants: list[dict[str, Any]]) -> list[str]:
    pending_fcstd = [
        row["door_count"]
        for row in variants
        if (row.get("metrics") or {}).get("fcstd_integrity_status") != "PASS"
    ]
    notes = [
        "This handoff bundle is for engineering review only.",
        "SolidWorks users should open the standardized .stp files; generated SolidWorks scripts use a safe API-open attempt and manual fallback.",
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
    }
    payload["root_launchers"] = write_root_launchers(payload)
    payload["engineer_open_index"] = write_engineer_open_index(payload)
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
