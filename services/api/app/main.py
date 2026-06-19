from __future__ import annotations

import json
import os
import platform
import re
import shlex
import shutil
import sqlite3
import subprocess
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Literal

IS_WINDOWS = platform.system() == "Windows"

from fastapi import FastAPI, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field


ROOT_DIR = Path(__file__).resolve().parents[3]
DEFAULT_DB_PATH = ROOT_DIR / "data" / "studio.sqlite"
DB_PATH = Path(os.getenv("STUDIO_DB_PATH", DEFAULT_DB_PATH))
CAD_WORKSPACE = Path(os.getenv("STUDIO_CAD_WORKSPACE", ROOT_DIR / "cad_workspace"))
WORKER_LOG_DIR = Path(os.getenv("STUDIO_WORKER_LOG_DIR", ROOT_DIR / "workers" / "generation_logs"))
GENERATED_MODEL_DIR = Path(os.getenv("STUDIO_GENERATED_MODEL_DIR", ROOT_DIR / "workers" / "generated_models"))
MANUAL_RUN_DIR = Path(os.getenv("STUDIO_MANUAL_RUN_DIR", ROOT_DIR / "workers" / "manual_runs"))
RULE_EXTRACTION_DIR = Path(os.getenv("STUDIO_RULE_EXTRACTION_DIR", ROOT_DIR / "workers" / "rule_extractions"))
TEMPLATE_ASSET_ROOT = Path(os.getenv("STUDIO_PARAMETRIC_TEMPLATE_ROOT", r"C:\Users\Administrator\Desktop\参数化模板素材"))
TEMPLATE_CATALOG_PATH = Path(os.getenv("STUDIO_TEMPLATE_CATALOG_PATH", ROOT_DIR / "data" / "parametric_template_catalog.json"))
TEMPLATE_CATALOG_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_TEMPLATE_CATALOG_MARKDOWN_PATH", ROOT_DIR / "data" / "parametric_template_catalog.md")
)
RULE_SEED_CANDIDATES_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_CANDIDATES_PATH", ROOT_DIR / "data" / "rule_seed_candidates.json")
)
RULE_SEED_REVIEW_LEDGER_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_REVIEW_LEDGER_PATH", ROOT_DIR / "data" / "rule_seed_review_ledger.json")
)
RULE_SEED_EVIDENCE_CHECKLIST_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_EVIDENCE_CHECKLIST_PATH", ROOT_DIR / "data" / "rule_seed_evidence_checklist.json")
)
RULE_SEED_QUANTITY_FORMULAS_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_QUANTITY_FORMULAS_PATH", ROOT_DIR / "data" / "rule_seed_quantity_formulas.json")
)
FREECAD_SHORTCUT = Path(os.getenv("STUDIO_FREECAD_SHORTCUT", r"C:\Users\Administrator\Desktop\FreeCAD 1.1.1.lnk"))
FREECAD_EXE = Path(
    os.getenv(
        "STUDIO_FREECAD_EXE",
        r"D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCAD.exe",
    )
)
SOLIDWORKS_SHORTCUT = Path(os.getenv("STUDIO_SOLIDWORKS_SHORTCUT", r"C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk"))
SOLIDWORKS_EXE = Path(
    os.getenv(
        "STUDIO_SOLIDWORKS_EXE",
        r"C:\WINDOWS\Installer\{DB2C3F1B-3025-4743-AAA8-1B5E20047E34}\i386_SldWorks.exe",
    )
)

CadRunner = Literal["freecad", "solidworks"]
SUPPORTED_LOCKER_DOOR_COUNTS = {8, 12, 14, 16, 18}
SUPPORTED_16038_SOLIDWORKS_DOOR_COUNTS = {4, 7, 8, 12}
SUPPORTED_16038_FREECAD_DOOR_COUNTS = {4, 7, 8}
TaskStatus = Literal[
    "draft_pending_worker",
    "ready_to_run",
    "blocked_pending_evidence",
    "blocked_preflight_failed",
    "running",
    "completed_reference",
    "failed_worker",
    "requires_manual_run",
]


class DryRunCheck(BaseModel):
    name: str
    ok: bool
    detail: str


class DryRunResult(BaseModel):
    task_id: str
    status: TaskStatus
    checks: list[DryRunCheck]
    log_path: str
    checked_at: str


class SolidWorksRunSummary(BaseModel):
    generation_mode: str = "unknown"
    interpretation: str = ""
    source_assembly: str | None = None
    source_folder: str | None = None
    door_count: str | None = None
    variant_label: str | None = None
    component_manifest_path: str | None = None
    component_manifest_rows: int | None = None
    requested_component_count: int | None = None
    added_component_count: int | None = None
    validation_pass_count: int | None = None
    validation_warn_count: int | None = None
    validation_fail_count: int | None = None
    transform_coverage: str | None = None
    bbox_coverage: str | None = None
    quality_status: str | None = None
    quality_summary: str | None = None
    flat_component_count: int | None = None
    all_component_count: int | None = None
    root_child_count: int | None = None
    reference_like_feature_count: int | None = None
    feature_total: int | None = None
    reference_sample_count: int | None = None
    reference_sample: list[str] = Field(default_factory=list)
    next_action: str = ""


class WorkerExecutionResult(BaseModel):
    task_id: str
    status: TaskStatus
    cad_runner: CadRunner
    started_at: str
    finished_at: str
    command: list[str]
    cwd: str
    output_dir: str
    outputs: list[str] = Field(default_factory=list)
    stdout_path: str | None = None
    stderr_path: str | None = None
    log_path: str
    exit_code: int | None = None
    solidworks_quality_status: str | None = None
    solidworks_quality_summary: str | None = None
    solidworks_run_summary: SolidWorksRunSummary | None = None
    message: str


class GenerationTaskCreate(BaseModel):
    cad_runner: CadRunner = "freecad"
    capability_id: str = Field(..., min_length=1)
    capability_title: str = Field(..., min_length=1)
    product_type: str = Field(..., min_length=1)
    module: str = Field(..., min_length=1)
    maturity: str = Field(..., min_length=1)
    output_level: str = Field(default="engineering_reference", min_length=1)
    command: str = Field(..., min_length=1)
    parameters: dict[str, str] = Field(default_factory=dict)
    evidence: list[str] = Field(default_factory=list)
    limitation: str = ""
    status: TaskStatus = "draft_pending_worker"


class GenerationTask(BaseModel):
    id: str
    cad_runner: CadRunner
    capability_id: str
    capability_title: str
    product_type: str
    module: str
    maturity: str
    output_level: str
    command: str
    parameters: dict[str, str]
    evidence: list[str]
    limitation: str
    status: TaskStatus
    dry_run_result: DryRunResult | None = None
    execution_result: WorkerExecutionResult | None = None
    worker_log_path: str | None = None
    created_at: str
    updated_at: str


class LocalOpenRequest(BaseModel):
    path: str = Field(..., min_length=1)
    mode: Literal["open", "reveal"] = "open"


class FreeCadOpenRequest(BaseModel):
    task_id: str = Field(..., min_length=1)


class LocalActionResult(BaseModel):
    status: str
    path: str
    message: str


class RuleExtractionCreate(BaseModel):
    template_id: str = Field(..., min_length=1)
    template_title: str = Field(..., min_length=1)
    assembly_path: str = Field(..., min_length=1)


class RuleExtractionResult(BaseModel):
    id: str
    template_id: str
    template_title: str
    assembly_path: str
    status: Literal["requires_solidworks_run", "running", "completed", "failed"]
    created_at: str
    updated_at: str
    output_dir: str
    run_script_path: str
    stdout_path: str | None = None
    stderr_path: str | None = None
    exit_code: int | None = None
    outputs: list[str] = Field(default_factory=list)
    learning_summary: dict[str, Any] | None = None
    message: str


app = FastAPI(title="Winnsen Structure Agent Studio API", version="0.1.0")

allowed_origins = [
    origin.strip()
    for origin in os.getenv("STUDIO_CORS_ORIGINS", "http://127.0.0.1:5173,http://localhost:5173").split(",")
    if origin.strip()
]

app.add_middleware(
    CORSMiddleware,
    allow_origins=allowed_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def connect() -> sqlite3.Connection:
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    connection = sqlite3.connect(DB_PATH)
    connection.row_factory = sqlite3.Row
    return connection


def init_db() -> None:
    with connect() as connection:
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS generation_tasks (
              id TEXT PRIMARY KEY,
              cad_runner TEXT NOT NULL DEFAULT 'freecad',
              capability_id TEXT NOT NULL,
              capability_title TEXT NOT NULL,
              product_type TEXT NOT NULL,
              module TEXT NOT NULL,
              maturity TEXT NOT NULL,
              output_level TEXT NOT NULL,
              command TEXT NOT NULL,
              parameters_json TEXT NOT NULL,
              evidence_json TEXT NOT NULL,
              limitation TEXT NOT NULL,
              status TEXT NOT NULL,
              dry_run_json TEXT,
              execution_json TEXT,
              worker_log_path TEXT,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            )
            """
        )
        ensure_column(connection, "generation_tasks", "cad_runner", "TEXT NOT NULL DEFAULT 'freecad'")
        ensure_column(connection, "generation_tasks", "dry_run_json", "TEXT")
        ensure_column(connection, "generation_tasks", "execution_json", "TEXT")
        ensure_column(connection, "generation_tasks", "worker_log_path", "TEXT")
        connection.execute("CREATE INDEX IF NOT EXISTS idx_generation_tasks_created_at ON generation_tasks(created_at)")


@app.on_event("startup")
def on_startup() -> None:
    init_db()


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


_VALID_SQL_IDENTIFIER = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


def ensure_column(connection: sqlite3.Connection, table_name: str, column_name: str, column_type: str) -> None:
    if not (_VALID_SQL_IDENTIFIER.match(table_name) and _VALID_SQL_IDENTIFIER.match(column_name)):
        raise ValueError(f"Invalid SQL identifier: table={table_name!r}, column={column_name!r}")
    columns = connection.execute(f'PRAGMA table_info("{table_name}")').fetchall()
    if any(column["name"] == column_name for column in columns):
        return
    connection.execute(f'ALTER TABLE "{table_name}" ADD COLUMN "{column_name}" {column_type}')


def make_task_id() -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    return f"GEN-{stamp}-{uuid.uuid4().hex[:6].upper()}"


def make_rule_extraction_id() -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    return f"RULE-{stamp}-{uuid.uuid4().hex[:6].upper()}"


def row_to_task(row: sqlite3.Row) -> GenerationTask:
    dry_run_json = row["dry_run_json"] if "dry_run_json" in row.keys() else None
    execution_json = row["execution_json"] if "execution_json" in row.keys() else None
    dry_run_result = DryRunResult.model_validate_json(dry_run_json) if dry_run_json else None
    execution_result = WorkerExecutionResult.model_validate_json(execution_json) if execution_json else None
    cad_runner = row["cad_runner"] if "cad_runner" in row.keys() and row["cad_runner"] else "freecad"
    status = row["status"]
    if execution_result:
        current_outputs = output_files(Path(execution_result.output_dir))
        if current_outputs:
            execution_result.outputs = current_outputs
        enrich_solidworks_quality(execution_result)
        if cad_runner == "solidworks" and any(path.lower().endswith((".sldasm", ".sldprt")) for path in execution_result.outputs):
            status = "completed_reference"
            execution_result.status = "completed_reference"
            if execution_result.solidworks_quality_status == "reference_feature_only":
                execution_result.message = (
                    "SolidWorks worker wrote a visual engineering-reference assembly, but diagnostics found "
                    "reference features instead of a traversable component tree."
                )
            else:
                execution_result.message = "SolidWorks worker completed and wrote native engineering-reference output."
    return GenerationTask(
        id=row["id"],
        cad_runner=cad_runner,
        capability_id=row["capability_id"],
        capability_title=row["capability_title"],
        product_type=row["product_type"],
        module=row["module"],
        maturity=row["maturity"],
        output_level=row["output_level"],
        command=row["command"],
        parameters=json.loads(row["parameters_json"]),
        evidence=json.loads(row["evidence_json"]),
        limitation=row["limitation"],
        status=status,
        dry_run_result=dry_run_result,
        execution_result=execution_result,
        worker_log_path=row["worker_log_path"] if "worker_log_path" in row.keys() else None,
        created_at=row["created_at"],
        updated_at=row["updated_at"],
    )


def fetch_task_or_404(task_id: str) -> sqlite3.Row:
    init_db()
    with connect() as connection:
        row = connection.execute("SELECT * FROM generation_tasks WHERE id = ?", (task_id,)).fetchone()
    if row is None:
        raise HTTPException(status_code=404, detail=f"Generation task {task_id} was not found.")
    return row


def allowed_local_action_roots() -> list[Path]:
    return [
        GENERATED_MODEL_DIR.resolve(),
        MANUAL_RUN_DIR.resolve(),
        WORKER_LOG_DIR.resolve(),
        RULE_EXTRACTION_DIR.resolve(),
    ]


def is_under_root(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def checked_local_action_path(raw_path: str) -> Path:
    path = Path(os.path.expandvars(raw_path)).expanduser()
    if not path.is_absolute():
        raise HTTPException(status_code=400, detail="Path must be absolute.")
    if not path.exists():
        raise HTTPException(status_code=404, detail=f"Path does not exist: {path}")

    resolved = path.resolve()
    if not any(resolved == root or is_under_root(resolved, root) for root in allowed_local_action_roots()):
        raise HTTPException(status_code=403, detail=f"Path is outside local-action allowlist: {resolved}")
    return resolved


def open_path_local(path: Path, mode: Literal["open", "reveal"]) -> str:
    try:
        system = platform.system()
        if system == "Windows":
            if path.is_dir():
                subprocess.Popen(["explorer.exe", str(path)])
                return "已打开输出目录。"
            if mode == "reveal":
                subprocess.Popen(["explorer.exe", f"/select,{path}"])
                return "已在资源管理器中定位文件。"
            os.startfile(str(path))  # type: ignore[attr-defined]
            return "已请求 Windows 使用默认程序打开文件。"
        elif system == "Darwin":
            if mode == "reveal" and not path.is_dir():
                subprocess.Popen(["open", "-R", str(path)])
                return "已在 Finder 中定位文件。"
            subprocess.Popen(["open", str(path)])
            return "已用 macOS 默认程序打开。"
        else:
            target = str(path.parent) if not path.is_dir() and mode == "reveal" else str(path)
            subprocess.Popen(["xdg-open", target])
            return "已用系统默认程序打开。"
    except OSError as error:
        raise HTTPException(status_code=500, detail=f"Open path failed: {error}") from error


def task_output_dir(task: GenerationTask) -> Path:
    if not task.execution_result:
        raise HTTPException(status_code=409, detail="Task has no execution result.")
    return checked_local_action_path(task.execution_result.output_dir)


def first_output_with_suffix(output_dir: Path, suffix: str) -> Path | None:
    for path in sorted(output_dir.iterdir()):
        if path.is_file() and path.name.lower().endswith(suffix):
            return path
    return None


def _strip_one_quote_pair(token: str) -> str:
    if len(token) >= 2 and token[0] == '"' and token[-1] == '"':
        return token[1:-1]
    return token


def command_tokens(command: str) -> list[str]:
    if command == "not_enabled":
        return []
    try:
        return [_strip_one_quote_pair(token) for token in shlex.split(command, posix=False)]
    except ValueError:
        return [_strip_one_quote_pair(token) for token in command.split()]


def resolve_freecad_cmd(executable: str) -> tuple[bool, str]:
    configured = os.getenv("STUDIO_FREECAD_CMD")
    if configured:
        configured_path = Path(configured)
        return configured_path.exists(), str(configured_path)

    bundled_cmd = FREECAD_EXE.with_name("FreeCADCmd.exe")
    if bundled_cmd.exists():
        return True, str(bundled_cmd)

    executable_path = Path(executable)
    if executable_path.exists():
        return True, str(executable_path)

    discovered = shutil.which(executable)
    if discovered:
        return True, discovered
    return False, f"{executable} not found in PATH; set STUDIO_FREECAD_CMD if installed outside PATH."


def find_script_token(tokens: list[str]) -> str:
    for token in tokens:
        if token.lower().endswith((".py", ".js", ".vbs")):
            return token
    return ""


def resolve_script(script_token: str) -> tuple[bool, str]:
    # Commands may carry Windows-style backslash separators (e.g. "scripts\\foo.py").
    # Normalize to forward slashes so relative script paths resolve on POSIX too,
    # where a backslash is a literal filename character rather than a separator.
    normalized_token = script_token.replace("\\", "/")
    script_path = Path(normalized_token)
    if script_path.is_absolute():
        return script_path.exists(), str(script_path)

    candidate = CAD_WORKSPACE / script_path
    if "*" in normalized_token:
        matches = list(CAD_WORKSPACE.glob(normalized_token))
        if len(matches) == 1:
            return True, str(matches[0])
        if len(matches) > 1:
            return False, f"Ambiguous script wildcard: {len(matches)} matches under {CAD_WORKSPACE}."
        return False, f"No script matched wildcard under {CAD_WORKSPACE}: {script_token}"

    return candidate.exists(), str(candidate)


def variant_16038_root() -> Path:
    return TEMPLATE_ASSET_ROOT / "16038 寄存柜XY(标准组合式 1917×1000×550)"


def solidworks_16038_variant_source(door_count: int) -> tuple[Path, str]:
    root = variant_16038_root()
    nested = root / "16038 寄存柜XY(标准组合式 1917×1000×550)"
    variants = {
        4: (
            nested / "4门" / "1.工程图-4门" / "标准洗衣寄存柜(总装配).SLDASM",
            "16038 4-door full assembly",
        ),
        7: (
            root / "1.工程图" / "标准洗衣寄存柜(总装配).SLDASM",
            "16038 7-door full assembly",
        ),
        8: (
            nested / "8门" / "1.工程图 8门" / "标准洗衣寄存柜(总装配).SLDASM",
            "16038 8-door full assembly",
        ),
        12: (
            root / "1.工程图" / "储物柜门12╱12装配.SLDASM",
            "16038 12/12 door module",
        ),
    }
    if door_count not in variants:
        supported = ", ".join(str(value) for value in sorted(SUPPORTED_16038_SOLIDWORKS_DOOR_COUNTS))
        raise HTTPException(status_code=400, detail=f"16038 SolidWorks template supports door_count/module values: {supported}.")
    return variants[door_count]


def validate_task_parameters(task: GenerationTask) -> list[str]:
    errors: list[str] = []
    if task.capability_id == "locker_16029_door_panel":
        category = str(task.parameters.get("category", "ordinary_door_panel")).strip()
        if category != "ordinary_door_panel":
            errors.append(
                "category must be ordinary_door_panel for the 16029 door-panel generator; "
                "use locker_16029_regression for door-count/full-cabinet tasks."
            )
        for parameter_name in ("door_width", "door_height"):
            raw_value = str(task.parameters.get(parameter_name, "")).strip()
            try:
                value = float(raw_value)
            except ValueError:
                errors.append(f"{parameter_name} must be numeric.")
            else:
                if value <= 0:
                    errors.append(f"{parameter_name} must be greater than 0.")
    if task.capability_id == "locker_16029_regression":
        raw_door_count = str(task.parameters.get("door_count", "")).strip()
        try:
            door_count = int(raw_door_count)
        except ValueError:
            errors.append("door_count must be an integer.")
        else:
            if door_count < 2:
                errors.append("door_count must be at least 2.")
            elif door_count % 2:
                errors.append("door_count must be even for the current two-column 16029 layout.")
            elif door_count not in SUPPORTED_LOCKER_DOOR_COUNTS:
                supported = ", ".join(str(value) for value in sorted(SUPPORTED_LOCKER_DOOR_COUNTS))
                errors.append(f"door_count must be one of the MVP-verified values: {supported}.")
    if task.capability_id == "locker_16038_variant_template":
        raw_door_count = str(task.parameters.get("door_count", "")).strip()
        try:
            door_count = int(raw_door_count)
        except ValueError:
            errors.append("door_count must be an integer.")
        else:
            solidworks_supported = ", ".join(str(value) for value in sorted(SUPPORTED_16038_SOLIDWORKS_DOOR_COUNTS))
            freecad_supported = ", ".join(str(value) for value in sorted(SUPPORTED_16038_FREECAD_DOOR_COUNTS))
            if task.cad_runner == "solidworks" and door_count not in SUPPORTED_16038_SOLIDWORKS_DOOR_COUNTS:
                errors.append(f"16038 SolidWorks template supports door_count/module values: {solidworks_supported}.")
            if task.cad_runner == "freecad" and door_count not in SUPPORTED_16038_FREECAD_DOOR_COUNTS:
                errors.append(f"16038 FreeCAD/STEP reference supports full assemblies for door counts: {freecad_supported}.")
    return errors


def build_dry_run(task: GenerationTask) -> DryRunResult:
    tokens = command_tokens(task.command)
    checks: list[DryRunCheck] = []

    if not tokens:
        checks.append(DryRunCheck(name="worker_command", ok=False, detail="Generator command is disabled."))
        status: TaskStatus = "blocked_preflight_failed"
    else:
        executable = tokens[0]
        script_token = find_script_token(tokens)
        if task.cad_runner == "freecad":
            runner_checks = [
                DryRunCheck(
                    name="freecad_shortcut",
                    ok=FREECAD_SHORTCUT.exists(),
                    detail=str(FREECAD_SHORTCUT),
                ),
                DryRunCheck(
                    name="freecad_gui",
                    ok=FREECAD_EXE.exists(),
                    detail=str(FREECAD_EXE),
                ),
            ]
            command_ok, command_detail = resolve_freecad_cmd(executable)
            command_check_name = "freecad_command"
        else:
            runner_checks = [
                DryRunCheck(
                    name="solidworks_shortcut",
                    ok=SOLIDWORKS_SHORTCUT.exists(),
                    detail=str(SOLIDWORKS_SHORTCUT),
                ),
                DryRunCheck(
                    name="solidworks_gui",
                    ok=SOLIDWORKS_EXE.exists(),
                    detail=str(SOLIDWORKS_EXE),
                ),
                DryRunCheck(
                    name="solidworks_automation_host",
                    ok=shutil.which("cscript.exe") is not None,
                    detail=shutil.which("cscript.exe") or "cscript.exe not found in PATH.",
                ),
            ]
            command_ok = True
            command_detail = "SOLIDWORKS execution is routed through COM automation; dry-run does not launch the GUI."
            command_check_name = "solidworks_command"

        script_ok, script_detail = resolve_script(script_token) if script_token else (False, "Missing generator script token.")
        missing_parameters = [name for name, value in task.parameters.items() if not str(value).strip() or value == "pending"]
        command_parameters = {token[2:].replace("-", "_") for token in tokens if token.startswith("--")}
        missing_command_flags = [] if task.cad_runner == "solidworks" else [name for name in task.parameters if name not in command_parameters]
        parameter_errors = validate_task_parameters(task)
        parameters_ok = not missing_parameters and not missing_command_flags and not parameter_errors

        checks.extend(
            runner_checks
            + [
                DryRunCheck(name=command_check_name, ok=command_ok, detail=command_detail),
                DryRunCheck(name="generator_script", ok=script_ok, detail=script_detail),
                DryRunCheck(
                    name="parameters",
                    ok=parameters_ok,
                    detail=(
                        "All task parameters are present in the command."
                        if parameters_ok
                        else (
                            f"Missing values: {missing_parameters}; missing flags: {missing_command_flags}; "
                            f"validation errors: {parameter_errors}"
                        )
                    ),
                ),
                DryRunCheck(
                    name="evidence",
                    ok=bool(task.evidence),
                    detail=", ".join(task.evidence) if task.evidence else "No evidence attached.",
                ),
                DryRunCheck(
                    name="output_boundary",
                    ok=task.output_level.startswith("engineering_reference"),
                    detail=f"{task.output_level}; dry-run does not create production drawings.",
                ),
            ]
        )
        status = "ready_to_run" if all(check.ok for check in checks) else "blocked_preflight_failed"

    checked_at = now_iso()
    WORKER_LOG_DIR.mkdir(parents=True, exist_ok=True)
    log_path = WORKER_LOG_DIR / f"{task.id}-dry-run.json"
    result = DryRunResult(task_id=task.id, status=status, checks=checks, log_path=str(log_path), checked_at=checked_at)
    log_path.write_text(result.model_dump_json(indent=2), encoding="utf-8")
    return result


def output_files(output_dir: Path) -> list[str]:
    if not output_dir.exists():
        return []
    files: list[Path] = []
    for path in sorted(output_dir.rglob("*")):
        if not path.is_file():
            continue
        if path.name.startswith("~$"):
            continue
        if "__pycache__" in path.parts or path.name == "freecad_worker_entry.py":
            continue
        if path.suffix.lower() == ".fcbak":
            continue
        files.append(path)

    def output_priority(path: Path) -> tuple[int, str]:
        lower_name = path.name.lower()
        lower_path = str(path).lower()
        if lower_name.endswith((".sldasm", ".sldprt")):
            return (0, lower_name)
        if lower_name.endswith(".fcstd"):
            return (1, lower_name)
        if lower_name.endswith("_solidworks_validation_report.md"):
            return (2, lower_name)
        if lower_name.endswith("_solidworks_quality_report.md") or lower_name.endswith("_solidworks_quality.json"):
            return (3, lower_name)
        if lower_name.endswith("_build_report.md") or lower_name.endswith("_report.md"):
            return (4, lower_name)
        if lower_name.endswith("_solidworks_validation.csv") or lower_name.endswith("_component_manifest.csv"):
            return (5, lower_name)
        if lower_name.endswith(("_solidworks_import.stp", ".step", ".stp")):
            return (6, lower_name)
        if "diagnostics" in lower_path:
            return (8, lower_name)
        return (9, lower_name)

    return [str(path) for path in sorted(files, key=output_priority)]


def first_output_path(outputs: list[str], suffix: str) -> Path | None:
    lower_suffix = suffix.lower()
    for output in outputs:
        if output.lower().endswith(lower_suffix):
            return Path(output)
    return None


def read_optional_text(path: Path | None) -> str:
    if path is None or not path.exists():
        return ""
    try:
        return path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return ""


def markdown_bullet_value(text: str, label: str) -> str | None:
    pattern = re.compile(rf"^- {re.escape(label)}:\s*`?([^`\r\n]+)`?", re.MULTILINE)
    match = pattern.search(text)
    return match.group(1).strip() if match else None


def first_int(value: str | None) -> int | None:
    if not value:
        return None
    match = re.search(r"-?\d+", value)
    return int(match.group(0)) if match else None


def payload_int(payload: dict[str, Any], key: str) -> int | None:
    value = payload.get(key)
    if value is None:
        return None
    return first_int(str(value))


def count_manifest_rows(path: Path | None) -> int | None:
    if path is None or not path.exists():
        return None
    try:
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except OSError:
        return None
    data_rows = [line for line in lines[1:] if line.strip() and not line.lstrip().startswith("#")]
    return len(data_rows)


def validation_counts(text: str) -> tuple[int | None, int | None, int | None]:
    match = re.search(r"Validation status counts:\s*pass=`?(\d+)`?,\s*warn=`?(\d+)`?,\s*fail=`?(\d+)`?", text)
    if not match:
        return None, None, None
    return int(match.group(1)), int(match.group(2)), int(match.group(3))


def coverage_value(text: str, label: str) -> str | None:
    value = markdown_bullet_value(text, label)
    return value.strip() if value else None


def read_solidworks_quality_payload(output_dir: Path, assembly_path: Path | None) -> dict[str, Any]:
    if assembly_path is None:
        return {}
    quality_path = solidworks_quality_json(output_dir, assembly_path)
    if not quality_path.exists():
        return {}
    try:
        loaded = json.loads(quality_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def build_solidworks_run_summary(result: WorkerExecutionResult) -> SolidWorksRunSummary | None:
    if result.cad_runner != "solidworks":
        return None

    output_dir = Path(result.output_dir)
    outputs = result.outputs if result.outputs else output_files(output_dir)
    assembly_path = next((Path(path) for path in outputs if path.lower().endswith(".sldasm")), None)
    build_report_path = first_output_path(outputs, "_build_report.md")
    validation_report_path = first_output_path(outputs, "_solidworks_validation_report.md")
    validation_csv_path = first_output_path(outputs, "_solidworks_validation.csv")
    manifest_path = first_output_path(outputs, "_component_manifest.csv")

    build_text = read_optional_text(build_report_path)
    validation_text = read_optional_text(validation_report_path)
    quality_payload = read_solidworks_quality_payload(output_dir, assembly_path)
    quality_status = str(quality_payload.get("quality_status") or result.solidworks_quality_status or "") or None
    quality_summary = str(quality_payload.get("quality_summary") or result.solidworks_quality_summary or "") or None

    title_line = build_text.splitlines()[0].strip().lower() if build_text else ""
    if result.status == "requires_manual_run":
        generation_mode = "manual_package"
        interpretation = "已准备 SolidWorks 本地执行包；还没有完成原生装配生成。"
    elif "variant template clone" in title_line:
        generation_mode = "template_clone"
        interpretation = "复制并保存 SolidWorks 标准总装模板，绑定为同尺寸门数组参考；当前不是逐个零件重排。"
    elif "direct 16029 locker assembly" in title_line:
        generation_mode = "direct_component_assembly"
        interpretation = "通过 SolidWorks API 按门数规则逐个插入门框、柜体、层板和门组件，并写出装配验证表。"
    else:
        generation_mode = "unknown"
        interpretation = "SolidWorks 已返回输出，但生成方式需要结合报告继续判断。"

    pass_count, warn_count, fail_count = validation_counts(validation_text)
    reference_sample_raw = str(quality_payload.get("reference_sample") or "")
    reference_sample = [item for item in reference_sample_raw.split("|") if item][:8]
    component_manifest_rows = count_manifest_rows(manifest_path)
    added_value = markdown_bullet_value(build_text, "Added components")

    if quality_status == "reference_feature_only":
        next_action = "下一步：用 Pack-and-Go 源装配或组件树重建路线，解决 SolidWorks 打开后只暴露 Reference 特征的问题。"
    elif fail_count and fail_count > 0:
        next_action = "下一步：打开验证 CSV，优先修复未插入或位置异常的组件。"
    elif result.status == "completed_reference":
        next_action = "下一步：打开 SLDASM 和验证报告复核；通过后再推进可编辑组件树和工程图输出。"
    elif result.status == "requires_manual_run":
        next_action = "下一步：点击运行 SolidWorks 生成，让本地 SolidWorks 执行这个任务包。"
    else:
        next_action = "下一步：查看 stdout/stderr 和生成报告，确认 SolidWorks 执行状态。"

    return SolidWorksRunSummary(
        generation_mode=generation_mode,
        interpretation=interpretation,
        source_assembly=markdown_bullet_value(build_text, "Source assembly"),
        source_folder=markdown_bullet_value(build_text, "Source folder"),
        door_count=markdown_bullet_value(build_text, "Door count / module") or markdown_bullet_value(build_text, "Door count"),
        variant_label=markdown_bullet_value(build_text, "Variant label"),
        component_manifest_path=str(manifest_path) if manifest_path else None,
        component_manifest_rows=component_manifest_rows,
        requested_component_count=first_int(markdown_bullet_value(build_text, "Requested components")),
        added_component_count=first_int(added_value),
        validation_pass_count=pass_count,
        validation_warn_count=warn_count,
        validation_fail_count=fail_count,
        transform_coverage=coverage_value(validation_text, "COM transform coverage"),
        bbox_coverage=coverage_value(validation_text, "COM bounding-box coverage"),
        quality_status=quality_status,
        quality_summary=quality_summary,
        flat_component_count=payload_int(quality_payload, "component_count_flat"),
        all_component_count=payload_int(quality_payload, "component_count_all"),
        root_child_count=payload_int(quality_payload, "root_child_count"),
        reference_like_feature_count=payload_int(quality_payload, "reference_like_feature_count"),
        feature_total=payload_int(quality_payload, "feature_total"),
        reference_sample_count=len([item for item in reference_sample_raw.split("|") if item]),
        reference_sample=reference_sample,
        next_action=next_action,
    )


def solidworks_quality_json(output_dir: Path, assembly_path: Path) -> Path:
    return output_dir / f"{assembly_path.stem}_solidworks_quality.json"


def read_solidworks_quality(output_dir: Path, assembly_path: Path) -> tuple[str | None, str | None]:
    quality_path = solidworks_quality_json(output_dir, assembly_path)
    if not quality_path.exists():
        return None, None
    try:
        payload = json.loads(quality_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None, None
    status = payload.get("quality_status")
    summary = payload.get("quality_summary")
    return (str(status) if status else None, str(summary) if summary else None)


def enrich_solidworks_quality(result: WorkerExecutionResult) -> None:
    if result.cad_runner != "solidworks":
        return
    output_dir = Path(result.output_dir)
    assembly_path = next((Path(path) for path in result.outputs if path.lower().endswith(".sldasm")), None)
    if assembly_path is not None:
        status, summary = read_solidworks_quality(output_dir, assembly_path)
        if status and not result.solidworks_quality_status:
            result.solidworks_quality_status = status
        if summary and not result.solidworks_quality_summary:
            result.solidworks_quality_summary = summary
    result.solidworks_run_summary = build_solidworks_run_summary(result)


def run_solidworks_quality_diagnostics(output_dir: Path, assembly_path: Path) -> tuple[str | None, str | None]:
    diagnostic_script = CAD_WORKSPACE / "scripts" / "sw_diagnose_assembly_quality.js"
    if not diagnostic_script.exists() or not assembly_path.exists():
        return None, None

    stdout_path = WORKER_LOG_DIR / f"{assembly_path.stem}-solidworks-quality.stdout.txt"
    stderr_path = WORKER_LOG_DIR / f"{assembly_path.stem}-solidworks-quality.stderr.txt"
    try:
        completed = subprocess.run(
            ["cscript.exe", "//Nologo", str(diagnostic_script), str(assembly_path), str(output_dir)],
            cwd=CAD_WORKSPACE,
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )
        stdout_path.write_text(completed.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(completed.stderr or "", encoding="utf-8", errors="replace")
    except (OSError, subprocess.TimeoutExpired) as error:
        stderr_path.write_text(str(error), encoding="utf-8", errors="replace")
        return None, None
    return read_solidworks_quality(output_dir, assembly_path)


def write_solidworks_handoff_files(task: GenerationTask, output_dir: Path) -> None:
    primary_step = output_dir / f"locker_16029_{task.parameters.get('door_count', '').strip()}door_rule_driven.step"
    step_files = [primary_step] if primary_step.exists() else [
        path
        for path in output_dir.glob("*.step")
        if not path.name.endswith("_solidworks_import.step") and path.is_file()
    ]
    for step_path in step_files:
        stp_path = step_path.with_name(f"{step_path.stem}_solidworks_import.stp")
        if not stp_path.exists() or stp_path.stat().st_mtime < step_path.stat().st_mtime:
            shutil.copy2(step_path, stp_path)

    readme = output_dir / "solidworks_opening_notes.md"
    readme.write_text(
        "\n".join(
            [
                "# SolidWorks opening notes",
                "",
                "- Open `*_solidworks_import.stp` in SolidWorks 2025 for the FreeCAD-generated engineering reference.",
                "- `.FCStd` is FreeCAD native and is not a SolidWorks file.",
                "- `.step` and `.stp` contain the same neutral geometry; `.stp` is provided to match common SolidWorks import filters.",
                "- SolidWorks-native `.SLDPRT/.SLDASM` output is only produced by the separate SolidWorks manual automation package.",
                "",
            ]
        ),
        encoding="utf-8",
    )


def write_freecad_opening_files(output_dir: Path) -> None:
    macro = output_dir / "show_all_objects_and_fit_view.FCMacro"
    macro.write_text(
        "\n".join(
            [
                "import FreeCAD as App",
                "import FreeCADGui as Gui",
                "",
                "doc = App.ActiveDocument",
                "if doc is not None:",
                "    for obj in doc.Objects:",
                "        try:",
                "            obj.Visibility = True",
                "        except Exception:",
                "            pass",
                "        view = getattr(obj, 'ViewObject', None)",
                "        if view is not None:",
                "            try:",
                "                view.Visibility = True",
                "            except Exception:",
                "                pass",
                "    doc.recompute()",
                "    try:",
                "        Gui.SendMsgToActiveView('ViewFit')",
                "    except Exception:",
                "        pass",
                "",
            ]
        ),
        encoding="utf-8",
    )

    fcstd = first_output_with_suffix(output_dir, ".fcstd")
    launcher_script = output_dir / "open_in_freecad_with_view.py"
    launcher_cmd = output_dir / "open-in-freecad-with-view.cmd"
    if fcstd:
        launcher_script.write_text(
            "\n".join(
                [
                    "import FreeCAD as App",
                    "import FreeCADGui as Gui",
                    "",
                    f"MODEL_PATH = {str(fcstd)!r}",
                    "",
                    "doc = App.openDocument(MODEL_PATH)",
                    "Gui.showMainWindow()",
                    "Gui.activateWorkbench('PartWorkbench')",
                    "Gui.getDocument(doc.Name)",
                    "for obj in doc.Objects:",
                    "    try:",
                    "        obj.Visibility = True",
                    "    except Exception:",
                    "        pass",
                    "    view = getattr(obj, 'ViewObject', None)",
                    "    if view is not None:",
                    "        try:",
                    "            view.Visibility = True",
                    "        except Exception:",
                    "            pass",
                    "doc.recompute()",
                    "try:",
                    "    Gui.ActiveDocument.ActiveView.viewAxometric()",
                    "    Gui.SendMsgToActiveView('ViewFit')",
                    "except Exception:",
                    "    pass",
                    "Gui.exec_loop()",
                    "",
                ]
            ),
            encoding="utf-8",
        )
        launcher_cmd.write_text(
            "\n".join(
                [
                    "@echo off",
                    f'"{FREECAD_EXE}" "%~dp0{launcher_script.name}"',
                    "",
                ]
            ),
            encoding="utf-8",
        )

    readme = output_dir / "freecad_opening_notes.md"
    readme.write_text(
        "\n".join(
            [
                "# FreeCAD opening notes",
                "",
                "- `.FCStd` is the native FreeCAD engineering-reference model.",
                "- The file is generated by `FreeCADCmd`, so it may not contain a saved GUI camera/view state.",
                "- Use `open-in-freecad-with-view.cmd` or `open_in_freecad_with_view.py` to open the model and apply show-all + fit-view automatically.",
                "- If the 3D view still looks blank after opening, use `View > Fit all` or run `show_all_objects_and_fit_view.FCMacro` in FreeCAD.",
                "- The companion `.step/.stp` file is the neutral geometry handoff and can also be opened directly in FreeCAD or SolidWorks.",
                "",
            ]
        ),
        encoding="utf-8",
    )


def read_template_asset_catalog() -> dict[str, object]:
    if not TEMPLATE_CATALOG_PATH.exists():
        raise HTTPException(
            status_code=404,
            detail=f"Template asset catalog was not found: {TEMPLATE_CATALOG_PATH}",
        )
    try:
        return json.loads(TEMPLATE_CATALOG_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise HTTPException(status_code=500, detail=f"Template asset catalog read failed: {error}") from error


def rescan_template_asset_catalog() -> dict[str, object]:
    script_path = ROOT_DIR / "workers" / "maintenance" / "index_parametric_template_assets.py"
    if not script_path.exists():
        raise HTTPException(status_code=404, detail=f"Template catalog scanner was not found: {script_path}")
    if not TEMPLATE_ASSET_ROOT.exists():
        raise HTTPException(status_code=404, detail=f"Template asset root was not found: {TEMPLATE_ASSET_ROOT}")

    completed = subprocess.run(
        [
            sys.executable,
            str(script_path),
            "--root",
            str(TEMPLATE_ASSET_ROOT),
            "--json-out",
            str(TEMPLATE_CATALOG_PATH),
            "--md-out",
            str(TEMPLATE_CATALOG_MARKDOWN_PATH),
        ],
        cwd=ROOT_DIR,
        capture_output=True,
        text=True,
        timeout=180,
        check=False,
    )
    if completed.returncode != 0:
        raise HTTPException(
            status_code=500,
            detail=f"Template catalog rescan failed: {completed.stderr or completed.stdout}",
        )
    return read_template_asset_catalog()


def ps_single_quote(value: Path | str) -> str:
    return "'" + str(value).replace("'", "''") + "'"


def checked_template_assembly_path(raw_path: str) -> Path:
    path = Path(os.path.expandvars(raw_path)).expanduser()
    if not path.is_absolute():
        raise HTTPException(status_code=400, detail="Assembly path must be absolute.")
    if path.suffix.lower() != ".sldasm":
        raise HTTPException(status_code=400, detail="Rule extraction currently accepts SolidWorks .SLDASM assemblies only.")
    if not path.exists():
        raise HTTPException(status_code=404, detail=f"Assembly file does not exist: {path}")

    resolved = path.resolve()
    root = TEMPLATE_ASSET_ROOT.resolve()
    if not (resolved == root or is_under_root(resolved, root)):
        raise HTTPException(status_code=403, detail=f"Assembly is outside template asset root: {resolved}")
    return resolved


def rule_extraction_summary_path(output_dir: Path) -> Path:
    return output_dir / "rule_extraction_run.json"


def rule_extraction_outputs(output_dir: Path) -> list[str]:
    if not output_dir.exists():
        return []
    return [
        str(path)
        for path in sorted(output_dir.iterdir(), key=lambda item: item.name.lower())
        if path.is_file() and not path.name.startswith("~$")
    ]


def read_rule_learning_summary(output_dir: Path) -> dict[str, Any] | None:
    summary_path = output_dir / "rule_learning_summary.json"
    if not summary_path.exists():
        return None
    try:
        loaded = json.loads(summary_path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return loaded if isinstance(loaded, dict) else None


def write_rule_extraction_result(result: RuleExtractionResult) -> None:
    output_dir = Path(result.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    rule_extraction_summary_path(output_dir).write_text(result.model_dump_json(indent=2), encoding="utf-8")


def read_rule_extraction_result(output_dir: Path) -> RuleExtractionResult | None:
    summary_path = rule_extraction_summary_path(output_dir)
    if not summary_path.exists():
        return None
    try:
        result = RuleExtractionResult.model_validate_json(summary_path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    result.outputs = rule_extraction_outputs(output_dir)
    result.learning_summary = read_rule_learning_summary(output_dir)
    return result


def _safe_mtime(path: Path) -> float:
    try:
        return path.stat().st_mtime
    except OSError:
        return 0.0


def list_rule_extraction_results(limit: int = 20) -> list[RuleExtractionResult]:
    if not RULE_EXTRACTION_DIR.exists():
        return []
    results = [
        result
        for output_dir in sorted(RULE_EXTRACTION_DIR.iterdir(), key=_safe_mtime, reverse=True)
        if output_dir.is_dir()
        for result in [read_rule_extraction_result(output_dir)]
        if result is not None
    ]
    return results[:limit]


def fetch_rule_extraction_or_404(run_id: str) -> RuleExtractionResult:
    output_dir = RULE_EXTRACTION_DIR / run_id
    result = read_rule_extraction_result(output_dir)
    if result is None:
        raise HTTPException(status_code=404, detail=f"Rule extraction run {run_id} was not found.")
    return result


def create_rule_extraction_package(payload: RuleExtractionCreate) -> RuleExtractionResult:
    assembly_path = checked_template_assembly_path(payload.assembly_path)
    extractor_script = CAD_WORKSPACE / "scripts" / "sw_extract_structure_queue_item.js"
    if not extractor_script.exists():
        raise HTTPException(status_code=404, detail=f"SolidWorks extractor script was not found: {extractor_script}")
    if IS_WINDOWS and shutil.which("cscript.exe") is None:
        raise HTTPException(status_code=409, detail="cscript.exe was not found in PATH.")

    run_id = make_rule_extraction_id()
    created_at = now_iso()
    output_dir = RULE_EXTRACTION_DIR / run_id
    output_dir.mkdir(parents=True, exist_ok=True)
    run_script = output_dir / "run-solidworks-rule-extraction.ps1"
    run_script.write_text(
        "\n".join(
            [
                "$ErrorActionPreference = 'Stop'",
                f"Set-Location -LiteralPath {ps_single_quote(CAD_WORKSPACE)}",
                "& cscript.exe //Nologo "
                + " ".join(
                    [
                        ps_single_quote(extractor_script),
                        ps_single_quote(assembly_path),
                        ps_single_quote(output_dir),
                        ps_single_quote(run_id),
                        ps_single_quote("template_rule_learning"),
                        ps_single_quote("true"),
                    ]
                ),
                "",
            ]
        ),
        encoding="utf-8-sig",
    )
    notes = output_dir / "rule_extraction_notes.md"
    notes.write_text(
        "\n".join(
            [
                "# SolidWorks template rule extraction",
                "",
                f"- Template ID: `{payload.template_id}`",
                f"- Template title: `{payload.template_title}`",
                f"- Source assembly: `{assembly_path}`",
                "- The extractor reads component tree rows, nested transforms, bounding boxes, mate entities, features, and dimensions.",
                "- This is evidence for rule learning. It is not a generated production model.",
                "- Use `run-solidworks-rule-extraction.ps1` when a SolidWorks desktop session/license is available, or run it from the web button.",
                "",
            ]
        ),
        encoding="utf-8",
    )
    result = RuleExtractionResult(
        id=run_id,
        template_id=payload.template_id,
        template_title=payload.template_title,
        assembly_path=str(assembly_path),
        status="requires_solidworks_run",
        created_at=created_at,
        updated_at=created_at,
        output_dir=str(output_dir),
        run_script_path=str(run_script),
        outputs=rule_extraction_outputs(output_dir),
        message="SolidWorks rule extraction package prepared. Run it to collect component transforms, mates, bbox, and feature evidence.",
    )
    write_rule_extraction_result(result)
    return result


def summarize_rule_extraction_output(output_dir: Path) -> None:
    script_path = ROOT_DIR / "workers" / "maintenance" / "summarize_solidworks_rule_extraction.py"
    if not script_path.exists():
        return
    completed = subprocess.run(
        [sys.executable, str(script_path), str(output_dir)],
        cwd=ROOT_DIR,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=60,
        check=False,
    )
    if completed.returncode != 0:
        (output_dir / "rule_learning_summary_error.txt").write_text(
            completed.stderr or completed.stdout or "Summary generation failed.",
            encoding="utf-8",
            errors="replace",
        )


def run_rule_extraction_step_bbox_postprocess(result: RuleExtractionResult, output_dir: Path) -> None:
    try:
        validated_assembly_path = checked_template_assembly_path(result.assembly_path)
    except HTTPException as exc:
        (output_dir / "step_bbox_postprocess_error.txt").write_text(
            f"assembly_path validation failed: {exc.detail}",
            encoding="utf-8",
        )
        return

    export_script = CAD_WORKSPACE / "scripts" / "sw_export_step_ascii.js"
    inspect_script = ROOT_DIR / "workers" / "maintenance" / "inspect_step_assembly_bboxes_freecad.py"
    binding_script = ROOT_DIR / "workers" / "maintenance" / "bind_step_bbox_component_roles.py"
    step_path = output_dir / f"{result.id}_assembly_export.step"

    if export_script.exists() and shutil.which("cscript.exe") is not None:
        export_completed = subprocess.run(
            ["cscript.exe", "//Nologo", str(export_script), str(validated_assembly_path), str(step_path)],
            cwd=CAD_WORKSPACE,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=int(os.getenv("STUDIO_SOLIDWORKS_STEP_EXPORT_TIMEOUT_SECONDS", "300")),
            check=False,
        )
        (output_dir / "solidworks_step_export.stdout.txt").write_text(
            export_completed.stdout or "",
            encoding="utf-8",
            errors="replace",
        )
        (output_dir / "solidworks_step_export.stderr.txt").write_text(
            export_completed.stderr or "",
            encoding="utf-8",
            errors="replace",
        )
        if export_completed.returncode != 0:
            (output_dir / "step_bbox_postprocess_error.txt").write_text(
                f"SolidWorks STEP export failed with exit code {export_completed.returncode}.",
                encoding="utf-8",
            )
            return

    if not step_path.exists() or not inspect_script.exists():
        return

    freecad_ok, freecad_cmd = resolve_freecad_cmd("FreeCADCmd.exe")
    if not freecad_ok:
        (output_dir / "step_bbox_postprocess_error.txt").write_text(freecad_cmd, encoding="utf-8")
        return

    env = os.environ.copy()
    env["STEP_BBOX_INPUT"] = str(step_path)
    env["STEP_BBOX_CSV"] = str(output_dir / f"{result.id}_step_object_bboxes.csv")
    env["STEP_BBOX_JSON"] = str(output_dir / f"{result.id}_step_object_bboxes.json")
    env["STEP_BBOX_MD"] = str(output_dir / f"{result.id}_step_object_bboxes.md")
    python_code = f"import runpy; runpy.run_path(r'''{inspect_script}''', run_name='__main__')"
    bbox_completed = subprocess.run(
        [freecad_cmd, "-c", python_code],
        cwd=ROOT_DIR,
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=int(os.getenv("STUDIO_STEP_BBOX_TIMEOUT_SECONDS", "300")),
        check=False,
    )
    (output_dir / "step_bbox_inspection.stdout.txt").write_text(
        bbox_completed.stdout or "",
        encoding="utf-8",
        errors="replace",
    )
    (output_dir / "step_bbox_inspection.stderr.txt").write_text(
        bbox_completed.stderr or "",
        encoding="utf-8",
        errors="replace",
    )
    if bbox_completed.returncode != 0:
        (output_dir / "step_bbox_postprocess_error.txt").write_text(
            f"STEP bbox inspection failed with exit code {bbox_completed.returncode}.",
            encoding="utf-8",
        )
        return

    if not binding_script.exists():
        return

    binding_completed = subprocess.run(
        [sys.executable, str(binding_script), str(output_dir)],
        cwd=ROOT_DIR,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=int(os.getenv("STUDIO_STEP_ROLE_BINDING_TIMEOUT_SECONDS", "60")),
        check=False,
    )
    (output_dir / "step_role_binding.stdout.txt").write_text(
        binding_completed.stdout or "",
        encoding="utf-8",
        errors="replace",
    )
    (output_dir / "step_role_binding.stderr.txt").write_text(
        binding_completed.stderr or "",
        encoding="utf-8",
        errors="replace",
    )
    if binding_completed.returncode != 0:
        (output_dir / "step_role_binding_error.txt").write_text(
            f"STEP role binding failed with exit code {binding_completed.returncode}.",
            encoding="utf-8",
        )


def run_rule_extraction_package(run_id: str) -> RuleExtractionResult:
    result = fetch_rule_extraction_or_404(run_id)
    output_dir = checked_local_action_path(result.output_dir)
    run_script = checked_local_action_path(result.run_script_path)
    if not run_script.is_file():
        raise HTTPException(status_code=404, detail=f"Rule extraction script does not exist: {run_script}")

    stdout_path = output_dir / "solidworks_rule_extraction.stdout.txt"
    stderr_path = output_dir / "solidworks_rule_extraction.stderr.txt"
    timeout_seconds = int(os.getenv("STUDIO_SOLIDWORKS_RULE_TIMEOUT_SECONDS", os.getenv("STUDIO_SOLIDWORKS_TIMEOUT_SECONDS", "900")))
    command = [
        "powershell.exe",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(run_script),
    ]

    result.status = "running"
    result.updated_at = now_iso()
    result.message = "SolidWorks rule extraction is running."
    write_rule_extraction_result(result)

    try:
        completed = subprocess.run(
            command,
            cwd=output_dir,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_seconds,
            check=False,
        )
        stdout_path.write_text(completed.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(completed.stderr or "", encoding="utf-8", errors="replace")
        outputs = rule_extraction_outputs(output_dir)
        has_component_csv = any(path.lower().endswith("_components.csv") for path in outputs)
        status: Literal["completed", "failed"] = "completed" if completed.returncode == 0 and has_component_csv else "failed"
        if status == "completed":
            run_rule_extraction_step_bbox_postprocess(result, output_dir)
            summarize_rule_extraction_output(output_dir)
            outputs = rule_extraction_outputs(output_dir)
        if status == "completed":
            message = "SolidWorks rule extraction completed. Component transform, mate, feature, JSON, CSV, and report files are available."
        elif completed.returncode == 0:
            message = "SolidWorks extraction exited without an error code, but the components CSV was not found."
        else:
            message = "SolidWorks rule extraction failed. Review stdout/stderr in the extraction folder."
        exit_code = completed.returncode
    except subprocess.TimeoutExpired as error:
        stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace")
        outputs = rule_extraction_outputs(output_dir)
        status = "failed"
        message = f"SolidWorks rule extraction timed out after {timeout_seconds} seconds."
        exit_code = None

    updated = RuleExtractionResult(
        id=result.id,
        template_id=result.template_id,
        template_title=result.template_title,
        assembly_path=result.assembly_path,
        status=status,
        created_at=result.created_at,
        updated_at=now_iso(),
        output_dir=result.output_dir,
        run_script_path=result.run_script_path,
        stdout_path=str(stdout_path),
        stderr_path=str(stderr_path),
        exit_code=exit_code,
        outputs=outputs,
        learning_summary=read_rule_learning_summary(output_dir),
        message=message,
    )
    write_rule_extraction_result(updated)
    return updated


def freecad_execution_command(task: GenerationTask, output_dir: Path) -> list[str]:
    tokens = command_tokens(task.command)
    if not tokens:
        raise HTTPException(status_code=409, detail="Task has no executable command.")

    script_token = find_script_token(tokens)
    if not script_token:
        raise HTTPException(status_code=409, detail="Task command does not include a FreeCAD Python script.")

    command_ok, command_detail = resolve_freecad_cmd(tokens[0])
    if not command_ok:
        raise HTTPException(status_code=409, detail=command_detail)

    script_ok, script_detail = resolve_script(script_token)
    if not script_ok:
        raise HTTPException(status_code=409, detail=script_detail)

    script_index = tokens.index(script_token)
    script_args = tokens[script_index + 1 :]
    if script_args and script_args[0] == "--pass":
        script_args = script_args[1:]

    wrapper_path = output_dir / "freecad_worker_entry.py"
    wrapper_path.write_text(
        "\n".join(
            [
                "import runpy",
                "import sys",
                f"sys.argv = {[script_detail, *script_args]!r}",
                f"runpy.run_path({script_detail!r}, run_name='__main__')",
                "",
            ]
        ),
        encoding="utf-8",
    )
    return [command_detail, str(wrapper_path)]


def solidworks_manual_command(task: GenerationTask, output_dir: Path) -> list[str]:
    tokens = command_tokens(task.command)
    if not tokens:
        raise HTTPException(status_code=409, detail="Task has no SolidWorks command.")

    script_token = find_script_token(tokens)
    script_ok, script_detail = resolve_script(script_token) if script_token else (False, "Missing SolidWorks script token.")
    if not script_ok:
        raise HTTPException(status_code=409, detail=script_detail)

    if Path(script_detail).name == "sw_build_locker_16029_direct_assembly.js":
        source_root = os.getenv("STUDIO_SOLIDWORKS_SOURCE_ROOT", r"C:\sw16029_direct_18door\source")
        door_count = str(task.parameters.get("door_count", "18")).strip() or "18"
        return ["cscript.exe", "//Nologo", script_detail, str(output_dir), task.id, source_root, door_count]
    if Path(script_detail).name == "sw_clone_16038_variant_template.js":
        raw_door_count = str(task.parameters.get("door_count", "7")).strip() or "7"
        try:
            door_count = int(raw_door_count)
        except ValueError as error:
            raise HTTPException(status_code=400, detail="door_count must be an integer.") from error
        source_path, variant_label = solidworks_16038_variant_source(door_count)
        if not source_path.exists():
            raise HTTPException(status_code=404, detail=f"16038 SolidWorks source was not found: {source_path}")
        return [
            "cscript.exe",
            "//Nologo",
            script_detail,
            str(output_dir),
            task.id,
            str(source_path),
            str(door_count),
            variant_label,
        ]

    return ["cscript.exe", "//Nologo", script_detail, *tokens[tokens.index(script_token) + 1 :]]


def write_execution_log(result: WorkerExecutionResult) -> None:
    Path(result.log_path).write_text(result.model_dump_json(indent=2), encoding="utf-8")


def build_solidworks_manual_result(task: GenerationTask) -> WorkerExecutionResult:
    started_at = now_iso()
    output_dir = MANUAL_RUN_DIR / task.id
    output_dir.mkdir(parents=True, exist_ok=True)
    command = solidworks_manual_command(task, output_dir)
    clean_script = CAD_WORKSPACE / "scripts" / "sw_clean_reference_display.js"
    diagnostic_script = CAD_WORKSPACE / "scripts" / "sw_diagnose_assembly_quality.js"
    assembly_path = output_dir / f"{task.id}.SLDASM"
    run_script = output_dir / "run-solidworks-worker.ps1"
    run_script.write_text(
        "\n".join(
            [
                "$ErrorActionPreference = 'Stop'",
                f"Set-Location -LiteralPath {ps_single_quote(CAD_WORKSPACE)}",
                "& " + " ".join(ps_single_quote(part) for part in command),
                f"$assemblyPath = {ps_single_quote(assembly_path)}",
                f"$cleanScript = {ps_single_quote(clean_script)}",
                "if ((Test-Path -LiteralPath $assemblyPath) -and (Test-Path -LiteralPath $cleanScript)) {",
                "  & cscript.exe //Nologo $cleanScript $assemblyPath",
                "}",
                f"$diagnosticScript = {ps_single_quote(diagnostic_script)}",
                "if ((Test-Path -LiteralPath $assemblyPath) -and (Test-Path -LiteralPath $diagnosticScript)) {",
                f"  & cscript.exe //Nologo $diagnosticScript $assemblyPath {ps_single_quote(output_dir)}",
                "}",
                "",
            ]
        ),
        encoding="utf-8-sig",
    )
    notes = output_dir / "solidworks_manual_run_notes.md"
    notes.write_text(
        "\n".join(
            [
                "# SolidWorks manual run notes",
                "",
                f"- Door count requested by this task: `{task.parameters.get('door_count', '18')}`.",
                "- Run `run-solidworks-worker.ps1` from a desktop PowerShell session after confirming the SolidWorks 2025 license/session is available.",
                "- The script creates a SolidWorks-native `.SLDASM`, component manifest, build report, SolidWorks validation CSV, and validation report in this folder.",
                "- After native assembly creation, the runner hides reference planes, sketches, origins, and reference labels, then saves the cleaned first-open view.",
                "- If SolidWorks reports missing external references, keep the source folder available and do not move the generated assembly without Pack-and-Go.",
                "",
            ]
        ),
        encoding="utf-8",
    )
    log_path = WORKER_LOG_DIR / f"{task.id}-execute.json"
    finished_at = now_iso()
    result = WorkerExecutionResult(
        task_id=task.id,
        status="requires_manual_run",
        cad_runner=task.cad_runner,
        started_at=started_at,
        finished_at=finished_at,
        command=command,
        cwd=str(CAD_WORKSPACE),
        output_dir=str(output_dir),
        outputs=output_files(output_dir),
        log_path=str(log_path),
        message="SolidWorks worker package prepared. The API did not launch SolidWorks; run the generated PowerShell script manually after confirming license/session state.",
    )
    enrich_solidworks_quality(result)
    write_execution_log(result)
    return result


def solidworks_output_dir_for_run(task: GenerationTask) -> Path:
    if task.execution_result:
        return checked_local_action_path(task.execution_result.output_dir)
    return MANUAL_RUN_DIR / task.id


def ensure_solidworks_run_script(task: GenerationTask) -> tuple[Path, Path]:
    if task.cad_runner != "solidworks":
        raise HTTPException(status_code=409, detail="Only SolidWorks tasks have a SolidWorks run package.")

    output_dir = solidworks_output_dir_for_run(task)
    run_script = output_dir / "run-solidworks-worker.ps1"
    if not run_script.exists():
        package_result = build_solidworks_manual_result(task)
        output_dir = Path(package_result.output_dir)
        run_script = output_dir / "run-solidworks-worker.ps1"
    if not run_script.exists():
        raise HTTPException(status_code=404, detail=f"SolidWorks run script was not found: {run_script}")
    return output_dir, run_script


def run_solidworks_manual_package(task: GenerationTask) -> WorkerExecutionResult:
    started_at = now_iso()
    output_dir, run_script = ensure_solidworks_run_script(task)
    WORKER_LOG_DIR.mkdir(parents=True, exist_ok=True)
    stdout_path = WORKER_LOG_DIR / f"{task.id}-solidworks-run.stdout.txt"
    stderr_path = WORKER_LOG_DIR / f"{task.id}-solidworks-run.stderr.txt"
    log_path = WORKER_LOG_DIR / f"{task.id}-solidworks-run.json"
    command = [
        "powershell.exe",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(run_script),
    ]
    timeout_seconds = int(os.getenv("STUDIO_SOLIDWORKS_TIMEOUT_SECONDS", os.getenv("STUDIO_WORKER_TIMEOUT_SECONDS", "900")))

    try:
        completed = subprocess.run(
            command,
            cwd=output_dir,
            env=os.environ.copy(),
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
        stdout_path.write_text(completed.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(completed.stderr or "", encoding="utf-8", errors="replace")
        exit_code = completed.returncode
        outputs = output_files(output_dir)
        has_native_output = any(path.lower().endswith((".sldasm", ".sldprt")) for path in outputs)
        assembly_path = next((Path(path) for path in outputs if path.lower().endswith(".sldasm")), None)
        quality_status = None
        quality_summary = None
        if assembly_path:
            quality_status, quality_summary = run_solidworks_quality_diagnostics(output_dir, assembly_path)
            outputs = output_files(output_dir)
        status: TaskStatus = "completed_reference" if completed.returncode == 0 and has_native_output else "failed_worker"
        if status == "completed_reference":
            if quality_status == "reference_feature_only":
                message = (
                    "SolidWorks worker wrote a visual engineering-reference assembly, but diagnostics found "
                    "reference features instead of a traversable component tree."
                )
            else:
                message = "SolidWorks worker ran directly and wrote native engineering-reference output."
        elif completed.returncode == 0:
            message = "SolidWorks worker finished without an error code, but no native SLDASM/SLDPRT output was found."
        else:
            message = "SolidWorks worker failed. Review stderr and the execution log."
    except subprocess.TimeoutExpired as error:
        stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace")
        exit_code = None
        outputs = output_files(output_dir)
        quality_status = None
        quality_summary = None
        status = "failed_worker"
        message = f"SolidWorks worker timed out after {timeout_seconds} seconds."

    finished_at = now_iso()
    result = WorkerExecutionResult(
        task_id=task.id,
        status=status,
        cad_runner=task.cad_runner,
        started_at=started_at,
        finished_at=finished_at,
        command=command,
        cwd=str(output_dir),
        output_dir=str(output_dir),
        outputs=outputs,
        stdout_path=str(stdout_path),
        stderr_path=str(stderr_path),
        log_path=str(log_path),
        exit_code=exit_code,
        solidworks_quality_status=quality_status,
        solidworks_quality_summary=quality_summary,
        message=message,
    )
    enrich_solidworks_quality(result)
    write_execution_log(result)
    return result


def run_freecad_worker(task: GenerationTask) -> WorkerExecutionResult:
    started_at = now_iso()
    output_dir = GENERATED_MODEL_DIR / task.id
    output_dir.mkdir(parents=True, exist_ok=True)
    command = freecad_execution_command(task, output_dir)
    WORKER_LOG_DIR.mkdir(parents=True, exist_ok=True)
    stdout_path = WORKER_LOG_DIR / f"{task.id}-execute.stdout.txt"
    stderr_path = WORKER_LOG_DIR / f"{task.id}-execute.stderr.txt"
    log_path = WORKER_LOG_DIR / f"{task.id}-execute.json"
    env = os.environ.copy()
    env["LOCKER_OUT_DIR"] = str(output_dir)
    env.setdefault("STRUCTURE_ROOT", str(CAD_WORKSPACE))

    timeout_seconds = int(os.getenv("STUDIO_WORKER_TIMEOUT_SECONDS", "300"))
    try:
        completed = subprocess.run(
            command,
            cwd=CAD_WORKSPACE,
            env=env,
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
        stdout_path.write_text(completed.stdout, encoding="utf-8", errors="replace")
        stderr_path.write_text(completed.stderr, encoding="utf-8", errors="replace")
        status: TaskStatus = "completed_reference" if completed.returncode == 0 else "failed_worker"
        message = (
            "FreeCAD worker completed and wrote engineering-reference outputs."
            if completed.returncode == 0
            else "FreeCAD worker failed. Review stderr and the execution log."
        )
        if completed.returncode == 0:
            write_freecad_opening_files(output_dir)
            write_solidworks_handoff_files(task, output_dir)
        exit_code = completed.returncode
    except subprocess.TimeoutExpired as error:
        stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace")
        status = "failed_worker"
        message = f"FreeCAD worker timed out after {timeout_seconds} seconds."
        exit_code = None

    finished_at = now_iso()
    result = WorkerExecutionResult(
        task_id=task.id,
        status=status,
        cad_runner=task.cad_runner,
        started_at=started_at,
        finished_at=finished_at,
        command=command,
        cwd=str(CAD_WORKSPACE),
        output_dir=str(output_dir),
        outputs=output_files(output_dir),
        stdout_path=str(stdout_path),
        stderr_path=str(stderr_path),
        log_path=str(log_path),
        exit_code=exit_code,
        message=message,
    )
    write_execution_log(result)
    return result


@app.get("/health")
def health() -> dict[str, str]:
    init_db()
    return {"status": "ok", "database": str(DB_PATH)}


@app.get("/api/template-assets")
def get_template_assets() -> dict[str, object]:
    return read_template_asset_catalog()


@app.post("/api/template-assets/rescan")
def rescan_template_assets() -> dict[str, object]:
    return rescan_template_asset_catalog()


@app.get("/api/template-rule-extractions", response_model=list[RuleExtractionResult])
def list_template_rule_extractions(limit: int = Query(default=20, ge=1, le=100)) -> list[RuleExtractionResult]:
    return list_rule_extraction_results(limit)


@app.post("/api/template-rule-extractions", response_model=RuleExtractionResult, status_code=201)
def create_template_rule_extraction(payload: RuleExtractionCreate) -> RuleExtractionResult:
    return create_rule_extraction_package(payload)


@app.post("/api/template-rule-extractions/{run_id}/run", response_model=RuleExtractionResult)
def run_template_rule_extraction(run_id: str) -> RuleExtractionResult:
    return run_rule_extraction_package(run_id)


@app.get("/api/rule-seed-candidates")
def get_rule_seed_candidates() -> dict[str, Any]:
    if not RULE_SEED_CANDIDATES_PATH.exists():
        return {
            "generatedAt": None,
            "sourceDir": str(RULE_EXTRACTION_DIR),
            "candidateCount": 0,
            "candidates": [],
        }
    try:
        loaded = json.loads(RULE_SEED_CANDIDATES_PATH.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise HTTPException(status_code=500, detail=f"Rule seed candidate file is unreadable: {error}") from error
    if not isinstance(loaded, dict):
        raise HTTPException(status_code=500, detail="Rule seed candidate file is not a JSON object.")
    return loaded


@app.get("/api/rule-seed-review-ledger")
def get_rule_seed_review_ledger() -> dict[str, Any]:
    if not RULE_SEED_REVIEW_LEDGER_PATH.exists():
        return {
            "generatedAt": None,
            "source": str(RULE_SEED_CANDIDATES_PATH),
            "itemCount": 0,
            "items": [],
        }
    try:
        loaded = json.loads(RULE_SEED_REVIEW_LEDGER_PATH.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise HTTPException(status_code=500, detail=f"Rule seed review ledger is unreadable: {error}") from error
    if not isinstance(loaded, dict):
        raise HTTPException(status_code=500, detail="Rule seed review ledger is not a JSON object.")
    return loaded


@app.get("/api/rule-seed-evidence-checklist")
def get_rule_seed_evidence_checklist() -> dict[str, Any]:
    if not RULE_SEED_EVIDENCE_CHECKLIST_PATH.exists():
        return {
            "generatedAt": None,
            "source": str(RULE_SEED_REVIEW_LEDGER_PATH),
            "itemCount": 0,
            "items": [],
        }
    try:
        loaded = json.loads(RULE_SEED_EVIDENCE_CHECKLIST_PATH.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise HTTPException(status_code=500, detail=f"Rule seed evidence checklist is unreadable: {error}") from error
    if not isinstance(loaded, dict):
        raise HTTPException(status_code=500, detail="Rule seed evidence checklist is not a JSON object.")
    return loaded


@app.get("/api/rule-seed-quantity-formulas")
def get_rule_seed_quantity_formulas() -> dict[str, Any]:
    if not RULE_SEED_QUANTITY_FORMULAS_PATH.exists():
        return {
            "generatedAt": None,
            "source": str(RULE_SEED_EVIDENCE_CHECKLIST_PATH),
            "itemCount": 0,
            "items": [],
        }
    try:
        loaded = json.loads(RULE_SEED_QUANTITY_FORMULAS_PATH.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise HTTPException(status_code=500, detail=f"Rule seed quantity formulas is unreadable: {error}") from error
    if not isinstance(loaded, dict):
        raise HTTPException(status_code=500, detail="Rule seed quantity formulas is not a JSON object.")
    return loaded


@app.get("/api/generation-tasks", response_model=list[GenerationTask])
def list_generation_tasks(limit: int = Query(default=20, ge=1, le=100)) -> list[GenerationTask]:
    init_db()
    with connect() as connection:
        rows = connection.execute(
            "SELECT * FROM generation_tasks ORDER BY created_at DESC LIMIT ?",
            (limit,),
        ).fetchall()
    return [row_to_task(row) for row in rows]


@app.get("/api/generation-tasks/{task_id}", response_model=GenerationTask)
def get_generation_task(task_id: str) -> GenerationTask:
    return row_to_task(fetch_task_or_404(task_id))


@app.post("/api/local-actions/open-path", response_model=LocalActionResult)
def open_local_path(payload: LocalOpenRequest) -> LocalActionResult:
    path = checked_local_action_path(payload.path)
    message = open_path_local(path, payload.mode)
    return LocalActionResult(status="opened", path=str(path), message=message)


@app.post("/api/local-actions/open-freecad-model", response_model=LocalActionResult)
def open_freecad_model(payload: FreeCadOpenRequest) -> LocalActionResult:
    task = row_to_task(fetch_task_or_404(payload.task_id))
    output_dir = task_output_dir(task)
    fcstd = first_output_with_suffix(output_dir, ".fcstd")
    if not fcstd:
        raise HTTPException(status_code=404, detail=f"No FCStd model found in {output_dir}")

    freecad_exe: str | None = None
    if FREECAD_EXE.exists():
        freecad_exe = str(FREECAD_EXE)
    else:
        freecad_exe = shutil.which("FreeCAD") or shutil.which("freecad") or shutil.which("FreeCAD.exe")
    if freecad_exe is None:
        raise HTTPException(
            status_code=409,
            detail=f"FreeCAD not found. Set STUDIO_FREECAD_EXE or add FreeCAD to PATH. Checked: {FREECAD_EXE}",
        )

    write_freecad_opening_files(output_dir)
    launcher = output_dir / "open_in_freecad_with_view.py"
    launch_target = launcher if launcher.exists() else fcstd
    try:
        subprocess.Popen([freecad_exe, str(launch_target)], cwd=str(output_dir))
    except OSError as error:
        raise HTTPException(status_code=500, detail=f"FreeCAD launch failed: {error}") from error

    macro = output_dir / "show_all_objects_and_fit_view.FCMacro"
    macro_note = f" 如视图仍为空，请在同目录运行 {macro.name}。" if macro.exists() else ""
    return LocalActionResult(status="opened", path=str(fcstd), message=f"已用 FreeCAD 打开模型，并尝试执行显示全部/适配视图。{macro_note}")


@app.post("/api/generation-tasks", response_model=GenerationTask, status_code=201)
def create_generation_task(payload: GenerationTaskCreate) -> GenerationTask:
    if payload.status == "draft_pending_worker" and payload.command == "not_enabled":
        raise HTTPException(status_code=400, detail="Cannot create a worker draft for a disabled generator.")

    task_id = make_task_id()
    created_at = now_iso()
    parameters_json = json.dumps(payload.parameters, ensure_ascii=False, sort_keys=True)
    evidence_json = json.dumps(payload.evidence, ensure_ascii=False)

    with connect() as connection:
        connection.execute(
            """
            INSERT INTO generation_tasks (
              id,
              cad_runner,
              capability_id,
              capability_title,
              product_type,
              module,
              maturity,
              output_level,
              command,
              parameters_json,
              evidence_json,
              limitation,
              status,
              created_at,
              updated_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                task_id,
                payload.cad_runner,
                payload.capability_id,
                payload.capability_title,
                payload.product_type,
                payload.module,
                payload.maturity,
                payload.output_level,
                payload.command,
                parameters_json,
                evidence_json,
                payload.limitation,
                payload.status,
                created_at,
                created_at,
            ),
        )
        row = connection.execute("SELECT * FROM generation_tasks WHERE id = ?", (task_id,)).fetchone()

    if row is None:
        raise HTTPException(status_code=500, detail="Generation task was not persisted.")
    return row_to_task(row)


@app.post("/api/generation-tasks/{task_id}/dry-run", response_model=GenerationTask)
def dry_run_generation_task(task_id: str) -> GenerationTask:
    task = row_to_task(fetch_task_or_404(task_id))
    result = build_dry_run(task)
    updated_at = now_iso()

    with connect() as connection:
        connection.execute(
            """
            UPDATE generation_tasks
            SET status = ?, dry_run_json = ?, worker_log_path = ?, updated_at = ?
            WHERE id = ?
            """,
            (
                result.status,
                result.model_dump_json(),
                result.log_path,
                updated_at,
                task_id,
            ),
        )
        row = connection.execute("SELECT * FROM generation_tasks WHERE id = ?", (task_id,)).fetchone()

    if row is None:
        raise HTTPException(status_code=500, detail="Generation task disappeared after dry-run.")
    return row_to_task(row)


@app.post("/api/generation-tasks/{task_id}/execute", response_model=GenerationTask)
def execute_generation_task(task_id: str) -> GenerationTask:
    task = row_to_task(fetch_task_or_404(task_id))
    if task.status != "ready_to_run":
        raise HTTPException(status_code=409, detail="Run dry-run successfully before executing this worker task.")

    started_update_at = now_iso()
    with connect() as connection:
        connection.execute(
            "UPDATE generation_tasks SET status = ?, updated_at = ? WHERE id = ?",
            ("running", started_update_at, task_id),
        )

    if task.cad_runner == "solidworks":
        result = build_solidworks_manual_result(task)
    else:
        result = run_freecad_worker(task)

    updated_at = now_iso()
    with connect() as connection:
        connection.execute(
            """
            UPDATE generation_tasks
            SET status = ?, execution_json = ?, worker_log_path = ?, updated_at = ?
            WHERE id = ?
            """,
            (
                result.status,
                result.model_dump_json(),
                result.log_path,
                updated_at,
                task_id,
            ),
        )
        row = connection.execute("SELECT * FROM generation_tasks WHERE id = ?", (task_id,)).fetchone()

    if row is None:
        raise HTTPException(status_code=500, detail="Generation task disappeared after worker execution.")
    return row_to_task(row)


@app.post("/api/generation-tasks/{task_id}/run-solidworks-package", response_model=GenerationTask)
def run_solidworks_package(task_id: str) -> GenerationTask:
    task = row_to_task(fetch_task_or_404(task_id))
    if task.cad_runner != "solidworks":
        raise HTTPException(status_code=409, detail="This task is not a SolidWorks task.")
    if task.status not in {"ready_to_run", "requires_manual_run", "completed_reference", "failed_worker"}:
        raise HTTPException(status_code=409, detail="Run dry-run or prepare the SolidWorks package before direct execution.")

    started_update_at = now_iso()
    with connect() as connection:
        connection.execute(
            "UPDATE generation_tasks SET status = ?, updated_at = ? WHERE id = ?",
            ("running", started_update_at, task_id),
        )

    result = run_solidworks_manual_package(task)
    updated_at = now_iso()
    with connect() as connection:
        connection.execute(
            """
            UPDATE generation_tasks
            SET status = ?, execution_json = ?, worker_log_path = ?, updated_at = ?
            WHERE id = ?
            """,
            (
                result.status,
                result.model_dump_json(),
                result.log_path,
                updated_at,
                task_id,
            ),
        )
        row = connection.execute("SELECT * FROM generation_tasks WHERE id = ?", (task_id,)).fetchone()

    if row is None:
        raise HTTPException(status_code=500, detail="Generation task disappeared after SolidWorks package execution.")
    return row_to_task(row)
