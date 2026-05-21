from __future__ import annotations

import base64
import binascii
import json
import os
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

from fastapi import FastAPI, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from .config import (
    CAD_WORKSPACE,
    DB_PATH,
    DRAWING_SHEETMETAL_DXF_EXTRACTOR,
    DRAWING_SHEETMETAL_EVIDENCE_MARKDOWN_PATH,
    DRAWING_SHEETMETAL_EVIDENCE_PATH,
    DRAWING_SHEETMETAL_FREECAD_CHECKER,
    DRAWING_SHEETMETAL_INTAKE_DIR,
    DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH,
    FREECAD_EXE,
    FREECAD_FCSTD_GEOMETRY_AUDITOR,
    FREECAD_RUN_LOCK_PATH,
    FREECAD_SHORTCUT,
    GENERATED_MODEL_DIR,
    HANDOFF_DIR,
    LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_MARKDOWN_PATH,
    LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_PATH,
    LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_SCRIPT,
    LOCKER_16029_STRUCTURAL_RULE_AUDIT_SCRIPT,
    LOCKER_16029_VARIANT_QUALITY_MATRIX_MARKDOWN_PATH,
    LOCKER_16029_VARIANT_QUALITY_MATRIX_PATH,
    LOCKER_16029_VARIANT_QUALITY_MATRIX_SCRIPT,
    LOCKER_16029_VARIANT_RULE_PACKET_MARKDOWN_PATH,
    LOCKER_16029_VARIANT_RULE_PACKET_PATH,
    MANUAL_RUN_DIR,
    MAX_DRAWING_SHEETMETAL_UPLOAD_BYTES,
    ROOT_DIR,
    RULE_EXTRACTION_DIR,
    RULE_SEED_CANDIDATES_PATH,
    RULE_SEED_EVIDENCE_CHECKLIST_PATH,
    RULE_SEED_QUANTITY_FORMULAS_PATH,
    RULE_SEED_REVIEW_LEDGER_PATH,
    SHEETMETAL_RULE_EVIDENCE_16029_MARKDOWN_PATH,
    SHEETMETAL_RULE_EVIDENCE_16029_PATH,
    SOLIDWORKS_EXE,
    SOLIDWORKS_RUN_LOCK_PATH,
    SOLIDWORKS_SHORTCUT,
    SUPPORTED_16029_FREECAD_RULE_DOOR_COUNTS,
    SUPPORTED_16029_SOLIDWORKS_TEMPLATE_DOOR_COUNTS,
    SUPPORTED_16038_FREECAD_DOOR_COUNTS,
    SUPPORTED_16038_SOLIDWORKS_DOOR_COUNTS,
    SUPPORTED_DRAWING_SHEETMETAL_SUFFIXES,
    SUPPORTED_LOCKER_DOOR_COUNTS,
    TEMPLATE_ASSET_ROOT,
    TEMPLATE_CATALOG_MARKDOWN_PATH,
    TEMPLATE_CATALOG_PATH,
    VERIFIED_LOCKER_16029_SOLIDWORKS_DOOR_COUNTS,
    WORKER_LOG_DIR,
)

CadRunner = Literal["freecad", "solidworks"]
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
    layout_units: str | None = None
    layout_rule_status: str | None = None
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
    freecad_quality_status: str | None = None
    freecad_quality_summary: str | None = None
    freecad_quality_report: str | None = None
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


class DrawingSheetMetalUploadFile(BaseModel):
    name: str = Field(..., min_length=1)
    content_base64: str = Field(..., min_length=1)
    size_bytes: int | None = Field(default=None, ge=0)


class DrawingSheetMetalIntakeCreate(BaseModel):
    source_type: Literal["drawing_or_sheetmetal", "drawing", "sheetmetal_model"] = "drawing_or_sheetmetal"
    notes: str = ""
    files: list[DrawingSheetMetalUploadFile] = Field(..., min_length=1, max_length=12)


class DrawingSheetMetalIntakeFileRecord(BaseModel):
    file_name: str
    saved_path: str
    size_bytes: int
    suffix: str
    source_category: str


class DrawingSheetMetalExtractionFileResult(BaseModel):
    file_name: str
    status: Literal["completed", "failed"]
    output_dir: str
    summary_path: str | None = None
    card_path: str | None = None
    quality_status: str | None = None
    role_guess: str | None = None
    manufacturing_bbox_mm: dict[str, float | None] = Field(default_factory=dict)
    circle_count: int | None = None
    closed_loop_count: int | None = None
    message: str


class DrawingSheetMetalExtractionSummary(BaseModel):
    status: Literal["not_started", "not_applicable", "completed", "partial_failed", "failed"] = "not_started"
    updated_at: str | None = None
    output_dir: str | None = None
    processed_files: int = 0
    failed_files: int = 0
    rule_seed_candidates: int = 0
    quality_status_counts: dict[str, int] = Field(default_factory=dict)
    results: list[DrawingSheetMetalExtractionFileResult] = Field(default_factory=list)
    message: str = "尚未运行 DXF 轻量解析。"


class DrawingSheetMetalCadCheckFileResult(BaseModel):
    file_name: str
    status: Literal["completed", "failed"]
    output_dir: str
    summary_path: str | None = None
    report_path: str | None = None
    quality_status: str | None = None
    model_type: str | None = None
    assembly_bbox_mm: dict[str, float | None] = Field(default_factory=dict)
    shape_object_count: int | None = None
    solid_count: int | None = None
    invalid_shape_count: int | None = None
    message: str


class DrawingSheetMetalCadCheckSummary(BaseModel):
    status: Literal["not_started", "not_applicable", "completed", "partial_failed", "failed"] = "not_started"
    updated_at: str | None = None
    output_dir: str | None = None
    processed_files: int = 0
    failed_files: int = 0
    pass_files: int = 0
    quality_status_counts: dict[str, int] = Field(default_factory=dict)
    results: list[DrawingSheetMetalCadCheckFileResult] = Field(default_factory=list)
    message: str = "尚未运行 FreeCAD 几何检查。"


class DrawingSheetMetalIntakeRecord(BaseModel):
    id: str
    status: Literal["intake_received", "blocked_unsupported_file"]
    created_at: str
    source_type: str
    notes: str
    output_dir: str
    files: list[DrawingSheetMetalIntakeFileRecord]
    next_action: str
    dxf_extraction: DrawingSheetMetalExtractionSummary = Field(default_factory=DrawingSheetMetalExtractionSummary)
    cad_check: DrawingSheetMetalCadCheckSummary = Field(default_factory=DrawingSheetMetalCadCheckSummary)


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


def parse_float(value: str | None) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def first_match(text: str, pattern: str) -> str | None:
    match = re.search(pattern, text, flags=re.MULTILINE)
    return match.group(1).strip() if match else None


def ensure_column(connection: sqlite3.Connection, table_name: str, column_name: str, column_type: str) -> None:
    columns = connection.execute(f"PRAGMA table_info({table_name})").fetchall()
    if any(column["name"] == column_name for column in columns):
        return
    connection.execute(f"ALTER TABLE {table_name} ADD COLUMN {column_name} {column_type}")


def make_task_id() -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    return f"GEN-{stamp}-{uuid.uuid4().hex[:6].upper()}"


def make_rule_extraction_id() -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    return f"RULE-{stamp}-{uuid.uuid4().hex[:6].upper()}"


def make_drawing_sheetmetal_intake_id() -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    return f"INTAKE-{stamp}-{uuid.uuid4().hex[:6].upper()}"


def safe_upload_filename(raw_name: str) -> str:
    base_name = Path(raw_name).name.strip()
    if not base_name or base_name in {".", ".."}:
        raise HTTPException(status_code=400, detail="Uploaded file name is empty.")
    safe_name = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", base_name).strip()
    if not safe_name or safe_name in {".", ".."}:
        raise HTTPException(status_code=400, detail=f"Uploaded file name is invalid: {raw_name}")
    return safe_name


def decode_upload_base64(file: DrawingSheetMetalUploadFile) -> bytes:
    content = file.content_base64
    if "," in content and content.lstrip().lower().startswith("data:"):
        content = content.split(",", 1)[1]
    try:
        decoded = base64.b64decode(content, validate=True)
    except (binascii.Error, ValueError) as error:
        raise HTTPException(status_code=400, detail=f"{file.name} is not valid base64 upload content.") from error
    if not decoded:
        raise HTTPException(status_code=400, detail=f"{file.name} is empty.")
    if file.size_bytes is not None and file.size_bytes != len(decoded):
        raise HTTPException(
            status_code=400,
            detail=f"{file.name} size mismatch: browser reported {file.size_bytes}, decoded {len(decoded)}.",
        )
    return decoded


def drawing_sheetmetal_source_category(suffix: str) -> str:
    if suffix in {".dxf", ".dwg"}:
        return "2d_drawing"
    if suffix in {".pdf", ".png", ".jpg", ".jpeg"}:
        return "drawing_image"
    if suffix == ".slddrw":
        return "solidworks_drawing"
    return "cad_model"


def drawing_sheetmetal_next_action(files: list[DrawingSheetMetalIntakeFileRecord]) -> str:
    suffixes = {file.suffix for file in files}
    if ".dxf" in suffixes:
        return "DXF 已进入 intake，可先运行轻量解析，抽取 bbox、孔位、闭合轮廓和规则种子。"
    if suffixes & {".step", ".stp", ".fcstd", ".sldprt", ".sldasm"}:
        return "CAD 模型已进入 intake，下一步做低并发几何检查和结构角色归类，不自动启动重 CAD 批处理。"
    if suffixes & {".pdf", ".png", ".jpg", ".jpeg", ".slddrw", ".dwg"}:
        return "图纸/图片已进入 intake，下一步先登记待识别项，再决定是否转 DXF 或走人工标注辅助。"
    return "文件已进入 intake，等待人工确认处理策略。"


def unique_output_path(output_dir: Path, file_name: str) -> Path:
    candidate = output_dir / file_name
    if not candidate.exists():
        return candidate
    stem = candidate.stem
    suffix = candidate.suffix
    for index in range(2, 1000):
        next_candidate = output_dir / f"{stem}_{index}{suffix}"
        if not next_candidate.exists():
            return next_candidate
    raise HTTPException(status_code=409, detail=f"Too many duplicated upload names for {file_name}.")


def read_drawing_sheetmetal_intake_records() -> list[DrawingSheetMetalIntakeRecord]:
    if not DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH.exists():
        return []
    try:
        loaded = json.loads(DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError) as error:
        raise HTTPException(status_code=500, detail=f"Drawing intake manifest is unreadable: {error}") from error
    raw_records = loaded.get("records", []) if isinstance(loaded, dict) else []
    if not isinstance(raw_records, list):
        raise HTTPException(status_code=500, detail="Drawing intake manifest records are not a list.")
    return [DrawingSheetMetalIntakeRecord.model_validate(record) for record in raw_records if isinstance(record, dict)]


def write_drawing_sheetmetal_intake_records(records: list[DrawingSheetMetalIntakeRecord]) -> None:
    DRAWING_SHEETMETAL_INTAKE_DIR.mkdir(parents=True, exist_ok=True)
    payload = {
        "updated_at": now_iso(),
        "records": [record.model_dump(mode="json") for record in records[:100]],
    }
    temp_path = DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH.with_suffix(".tmp")
    temp_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    temp_path.replace(DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH)
    write_drawing_sheetmetal_evidence_catalog(records)


def drawing_sheetmetal_evidence_items(records: list[DrawingSheetMetalIntakeRecord]) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for record in records:
        for result in record.dxf_extraction.results:
            if result.status != "completed":
                continue
            items.append(
                {
                    "intake_id": record.id,
                    "created_at": record.created_at,
                    "evidence_type": "dxf_sheetmetal_reference",
                    "file_name": result.file_name,
                    "quality_status": result.quality_status,
                    "role_guess": result.role_guess,
                    "manufacturing_bbox_mm": result.manufacturing_bbox_mm,
                    "circle_count": result.circle_count,
                    "closed_loop_count": result.closed_loop_count,
                    "summary_path": result.summary_path,
                    "report_path": result.card_path,
                    "recommended_rule_action": (
                        "candidate_for_rule_seed_review"
                        if result.quality_status in {"rule_seed_candidate", "geometry_rule_seed_only"}
                        else "needs_geometry_cleanup_before_rule_seed"
                    ),
                }
            )
        for result in record.cad_check.results:
            if result.status != "completed":
                continue
            items.append(
                {
                    "intake_id": record.id,
                    "created_at": record.created_at,
                    "evidence_type": "freecad_geometry_check",
                    "file_name": result.file_name,
                    "quality_status": result.quality_status,
                    "model_type": result.model_type,
                    "assembly_bbox_mm": result.assembly_bbox_mm,
                    "shape_object_count": result.shape_object_count,
                    "solid_count": result.solid_count,
                    "invalid_shape_count": result.invalid_shape_count,
                    "summary_path": result.summary_path,
                    "report_path": result.report_path,
                    "recommended_rule_action": (
                        "candidate_for_cad_bbox_and_integrity_rule"
                        if result.quality_status == "geometry_check_pass"
                        else "needs_freecad_geometry_repair_before_rule_seed"
                    ),
                }
            )
    return items


def write_drawing_sheetmetal_evidence_catalog(records: list[DrawingSheetMetalIntakeRecord]) -> None:
    items = drawing_sheetmetal_evidence_items(records)
    generated_at = now_iso()
    payload = {
        "generated_at": generated_at,
        "source_manifest": str(DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH),
        "item_count": len(items),
        "items": items,
    }
    DRAWING_SHEETMETAL_EVIDENCE_PATH.parent.mkdir(parents=True, exist_ok=True)
    DRAWING_SHEETMETAL_EVIDENCE_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    lines = [
        "# 图纸/钣金 intake 规则证据清单",
        "",
        f"- Generated at: `{generated_at}`",
        f"- Evidence items: `{len(items)}`",
        f"- Source manifest: `{DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH}`",
        "",
        "| Intake | 类型 | 文件 | 质量 | 尺寸证据 | 动作 | 报告 |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    for item in items:
        bbox = item.get("manufacturing_bbox_mm") or item.get("assembly_bbox_mm") or {}
        if "width" in bbox or "height" in bbox:
            bbox_text = f"{bbox.get('width')} x {bbox.get('height')} mm"
        else:
            bbox_text = f"{bbox.get('size_x')} x {bbox.get('size_y')} x {bbox.get('size_z')} mm"
        lines.append(
            "| "
            + " | ".join(
                [
                    str(item.get("intake_id", "")),
                    str(item.get("evidence_type", "")),
                    str(item.get("file_name", "")).replace("|", "/"),
                    str(item.get("quality_status", "")),
                    bbox_text,
                    str(item.get("recommended_rule_action", "")),
                    f"`{item.get('report_path') or ''}`",
                ]
            )
            + " |"
        )
    lines.append("")
    DRAWING_SHEETMETAL_EVIDENCE_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def read_sheetmetal_rule_evidence_16029() -> dict[str, Any]:
    if not SHEETMETAL_RULE_EVIDENCE_16029_PATH.exists():
        return {
            "generated_at": None,
            "product_family": "16029",
            "source_batch": {},
            "summary": {
                "file_count": 0,
                "main_source_file_count": 0,
                "accepted_rule_seed_file_count": 0,
                "formula_count": 0,
                "candidate_count": 0,
                "quality_status_counts": {},
                "role_counts": {},
            },
            "formulas": [],
            "rule_candidates": [],
            "blocked_evidence_summary": {
                "blocked_count": 0,
                "quality_status_counts": {},
                "large_bbox_noise_count": 0,
                "blocked_examples": [],
                "large_bbox_noise_examples": [],
            },
            "generator_guidance": ["尚未生成 16029 钣金规则证据文件。"],
            "output_paths": {
                "json": str(SHEETMETAL_RULE_EVIDENCE_16029_PATH),
                "markdown": str(SHEETMETAL_RULE_EVIDENCE_16029_MARKDOWN_PATH),
            },
        }
    try:
        return json.loads(SHEETMETAL_RULE_EVIDENCE_16029_PATH.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=500, detail=f"16029 sheet-metal rule evidence JSON is invalid: {exc}") from exc


def read_locker_16029_variant_rule_packet() -> dict[str, Any]:
    if not LOCKER_16029_VARIANT_RULE_PACKET_PATH.exists():
        return {
            "generated_at": None,
            "product_family": "16029",
            "purpose": "Door-count rule packet for standard 1000W x 1917H x 550D locker variants.",
            "source_evidence_json": str(SHEETMETAL_RULE_EVIDENCE_16029_PATH),
            "cabinet_rules": {},
            "supported_counts": {
                "freecad_rule_validation": sorted(SUPPORTED_16029_FREECAD_RULE_DOOR_COUNTS),
                "solidworks_native_skeleton": sorted(SUPPORTED_16029_SOLIDWORKS_TEMPLATE_DOOR_COUNTS),
            },
            "variants": [],
            "notes": ["尚未生成 16029 10/12/14 门规则包。"],
            "output_paths": {
                "json": str(LOCKER_16029_VARIANT_RULE_PACKET_PATH),
                "markdown": str(LOCKER_16029_VARIANT_RULE_PACKET_MARKDOWN_PATH),
            },
        }
    try:
        return json.loads(LOCKER_16029_VARIANT_RULE_PACKET_PATH.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=500, detail=f"16029 variant rule packet JSON is invalid: {exc}") from exc


def read_locker_16029_variant_quality_matrix() -> dict[str, Any]:
    if not LOCKER_16029_VARIANT_QUALITY_MATRIX_PATH.exists():
        return {
            "generated_at": None,
            "product_family": "16029",
            "scope": "1000W x 1917H x 550D FreeCAD engineering-reference variants",
            "source_rule_packet": str(LOCKER_16029_VARIANT_RULE_PACKET_PATH),
            "summary": {
                "target_door_counts": [10, 12, 14],
                "pass_ready_count": 0,
                "pass_rule_counts_needs_fcstd_audit_count": 0,
                "fail_count": 0,
                "missing_output_count": 0,
            },
            "variants": [],
            "output_paths": {
                "json": str(LOCKER_16029_VARIANT_QUALITY_MATRIX_PATH),
                "markdown": str(LOCKER_16029_VARIANT_QUALITY_MATRIX_MARKDOWN_PATH),
            },
            "notes": ["尚未生成 16029 10/12/14 门质量矩阵。"],
        }
    try:
        return json.loads(LOCKER_16029_VARIANT_QUALITY_MATRIX_PATH.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=500, detail=f"16029 variant quality matrix JSON is invalid: {exc}") from exc


def read_locker_16029_engineering_handoff_bundle() -> dict[str, Any]:
    if not LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_PATH.exists():
        return {
            "generated_at": None,
            "product_family": "16029",
            "scope": "1000W x 1917H x 550D 10/12/14 door engineering-reference handoff",
            "source_quality_matrix": str(LOCKER_16029_VARIANT_QUALITY_MATRIX_PATH),
            "handoff_dir": str(ROOT_DIR / "workers" / "handoffs" / "16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519"),
            "variants": [],
            "output_paths": {
                "json": str(LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_PATH),
                "markdown": str(LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_MARKDOWN_PATH),
            },
            "readiness_summary": summarize_locker_16029_handoff_readiness({"variants": []}),
            "notes": ["尚未生成 16029 10/12/14 门工程交接包。"],
        }
    try:
        bundle = json.loads(LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_PATH.read_text(encoding="utf-8-sig"))
        return enrich_locker_16029_handoff_file_status(bundle)
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=500, detail=f"16029 engineering handoff bundle JSON is invalid: {exc}") from exc


def path_exists(raw_path: Any) -> bool:
    if not isinstance(raw_path, str) or not raw_path.strip():
        return False
    return Path(raw_path).exists()


def read_solidworks_open_verification(raw_path: Any) -> dict[str, Any]:
    default = {
        "status": None,
        "verified": False,
        "message": "SolidWorks 自动打开验证记录不存在。",
    }
    if not isinstance(raw_path, str) or not raw_path.strip():
        return default
    path = Path(raw_path)
    if not path.exists():
        return default
    try:
        content = path.read_text(encoding="utf-8-sig")
    except OSError:
        return {
            "status": None,
            "verified": False,
            "message": "SolidWorks 自动打开验证记录无法读取。",
        }

    status_match = re.search(r"Status:\s*`?([^`\r\n]+)`?", content)
    status = status_match.group(1).strip() if status_match else None
    verified_statuses = {
        "pass",
        "passed",
        "opened",
        "verified",
        "visible_open_confirmed",
        "solidworks_visual_open_confirmed",
    }
    verified = (status or "").lower() in verified_statuses
    normalized_status = (status or "").lower()
    if verified:
        message = "SolidWorks 已确认可自动打开工程交接 STEP。"
    elif normalized_status == "solidworks_step_api_blocked_manual_open_required":
        message = "模型文件已就绪，但本机 SolidWorks API 自动导入 STEP 阻塞；请工程师用 SolidWorks 手动打开/修复，下一步转原生 SolidWorks 宏生成。"
    elif normalized_status == "manual_repair_prompt_required":
        message = "模型文件已就绪，但 SolidWorks 导入 STEP 会弹出修复确认；需工程师点击“是”后再复核。"
    else:
        message = "模型文件已就绪，但 SolidWorks 自动打开 STEP 尚未确认；按钮会尝试打开，失败时选中文件供手动打开。"
    return {
        "status": status,
        "verified": verified,
        "message": message,
    }


def enrich_locker_16029_handoff_file_status(bundle: dict[str, Any]) -> dict[str, Any]:
    bundle["handoff_dir_exists"] = path_exists(bundle.get("handoff_dir"))
    bundle["manifest_csv_exists"] = path_exists(bundle.get("manifest_csv"))
    bundle["engineer_open_index_exists"] = path_exists(bundle.get("engineer_open_index"))
    bundle["solidworks_open_verification_exists"] = path_exists(bundle.get("solidworks_open_verification"))
    bundle["solidworks_open_verification_status"] = read_solidworks_open_verification(
        bundle.get("solidworks_open_verification")
    )

    root_launcher_status_by_door_count: dict[int, bool] = {}
    for launcher in bundle.get("root_launchers", []):
        if not isinstance(launcher, dict):
            continue
        door_count = launcher.get("door_count")
        solidworks_launcher_exists = path_exists(launcher.get("solidworks_launcher"))
        launcher["file_status"] = {
            "solidworks_launcher": solidworks_launcher_exists,
            "target_launcher": path_exists(launcher.get("target_launcher")),
            "stp": path_exists(launcher.get("stp")),
        }
        if isinstance(door_count, int):
            root_launcher_status_by_door_count[door_count] = solidworks_launcher_exists

    for variant in bundle.get("variants", []):
        if not isinstance(variant, dict):
            continue
        door_count = variant.get("door_count")
        root_solidworks_launcher_exists = (
            root_launcher_status_by_door_count.get(door_count)
            if isinstance(door_count, int)
            else None
        )
        file_status = {
            "handoff_dir": path_exists(variant.get("handoff_dir")),
            "step": path_exists(variant.get("step")),
            "stp": path_exists(variant.get("stp")),
            "fcstd": path_exists(variant.get("fcstd")),
            "root_solidworks_launcher": root_solidworks_launcher_exists,
            "solidworks_launcher": path_exists(variant.get("solidworks_launcher")),
            "freecad_launcher": path_exists(variant.get("freecad_launcher")),
            "report_md": path_exists(variant.get("report_md")),
            "verify_csv": path_exists(variant.get("verify_csv")),
            "step_geometry_check_json": path_exists(variant.get("step_geometry_check_json")),
            "fcstd_integrity_md": path_exists(variant.get("fcstd_integrity_md")),
            "structural_rule_audit_md": path_exists(variant.get("structural_rule_audit_md")),
        }
        variant["file_status"] = file_status
        variant["handoff_files_ready"] = all(
            file_status[key]
            for key in (
                "handoff_dir",
                "stp",
                "root_solidworks_launcher",
                "solidworks_launcher",
                "report_md",
                "verify_csv",
                "step_geometry_check_json",
                "fcstd_integrity_md",
                "structural_rule_audit_md",
            )
        )
    bundle["readiness_summary"] = summarize_locker_16029_handoff_readiness(bundle)
    return bundle


def summarize_locker_16029_handoff_readiness(bundle: dict[str, Any]) -> dict[str, Any]:
    target_door_counts = [10, 12, 14]
    required_file_keys = (
        "handoff_dir",
        "stp",
        "root_solidworks_launcher",
        "solidworks_launcher",
        "report_md",
        "verify_csv",
        "step_geometry_check_json",
        "fcstd_integrity_md",
        "structural_rule_audit_md",
    )
    variants = [variant for variant in bundle.get("variants", []) if isinstance(variant, dict)]
    ready_door_counts: list[int] = []
    blocked_door_counts: list[int] = []
    missing_files: list[dict[str, Any]] = []

    for variant in variants:
        door_count = variant.get("door_count")
        metrics = variant.get("metrics") if isinstance(variant.get("metrics"), dict) else {}
        file_status = variant.get("file_status") if isinstance(variant.get("file_status"), dict) else {}

        for key in required_file_keys:
            if file_status.get(key) is not True:
                missing_files.append(
                    {
                        "door_count": door_count,
                        "key": key,
                        "path": variant.get(key),
                    }
                )

        is_ready = (
            variant.get("status") == "PASS_READY_FOR_ENGINEERING_REVIEW"
            and variant.get("handoff_files_ready") is True
            and all(file_status.get(key) is True for key in required_file_keys)
            and metrics.get("step_geometry_status") == "geometry_check_pass"
            and metrics.get("step_invalid_shape_count") == 0
            and metrics.get("fcstd_integrity_status") == "PASS"
            and metrics.get("structural_rule_status") == "PASS"
        )
        if isinstance(door_count, int) and is_ready:
            ready_door_counts.append(door_count)
        elif isinstance(door_count, int):
            blocked_door_counts.append(door_count)

    ready_door_counts.sort()
    blocked_door_counts.sort()
    missing_file_count = len(missing_files)
    ready_count = len(ready_door_counts)
    blocked_count = len(blocked_door_counts)

    return {
        "target_door_counts": target_door_counts,
        "variant_count": len(variants),
        "ready_count": ready_count,
        "model_file_ready_count": ready_count,
        "blocked_count": blocked_count,
        "missing_file_count": missing_file_count,
        "ready_door_counts": ready_door_counts,
        "blocked_door_counts": blocked_door_counts,
        "missing_files": missing_files[:12],
        "solidworks_open_verified": bool(
            (bundle.get("solidworks_open_verification_status") or {}).get("verified")
        ),
        "solidworks_open_status": (bundle.get("solidworks_open_verification_status") or {}).get("status"),
        "solidworks_open_message": (bundle.get("solidworks_open_verification_status") or {}).get("message"),
        "all_ready": (
            ready_count == len(target_door_counts)
            and blocked_count == 0
            and missing_file_count == 0
            and bool((bundle.get("solidworks_open_verification_status") or {}).get("verified"))
        ),
    }


def write_drawing_sheetmetal_intake_note(record: DrawingSheetMetalIntakeRecord) -> None:
    output_dir = Path(record.output_dir)
    lines = [
        f"# {record.id} 图纸/钣金模型 intake",
        "",
        f"- 状态: {record.status}",
        f"- 创建时间: {record.created_at}",
        f"- 来源类型: {record.source_type}",
        f"- 下一步: {record.next_action}",
        f"- 备注: {record.notes or '无'}",
        "",
        "## 文件",
        "",
        "| 文件 | 类型 | 大小 | 路径 |",
        "| --- | --- | ---: | --- |",
    ]
    for file in record.files:
        lines.append(f"| {file.file_name} | {file.source_category} | {file.size_bytes} | `{file.saved_path}` |")
    if record.dxf_extraction.status != "not_started":
        extraction = record.dxf_extraction
        lines.extend(
            [
                "",
                "## DXF 轻量解析",
                "",
                f"- 状态: {extraction.status}",
                f"- 更新时间: {extraction.updated_at or '未记录'}",
                f"- 输出目录: `{extraction.output_dir or ''}`",
                f"- 处理文件: {extraction.processed_files}",
                f"- 失败文件: {extraction.failed_files}",
                f"- 规则种子候选: {extraction.rule_seed_candidates}",
                f"- 说明: {extraction.message}",
                "",
                "| 文件 | 状态 | 质量 | 角色 | 规则 bbox | 输出 |",
                "| --- | --- | --- | --- | --- | --- |",
            ]
        )
        for result in extraction.results:
            bbox = result.manufacturing_bbox_mm
            bbox_label = ""
            if bbox:
                bbox_label = f"{bbox.get('width')} x {bbox.get('height')} mm"
            lines.append(
                f"| {result.file_name} | {result.status} | {result.quality_status or ''} | "
                f"{result.role_guess or ''} | {bbox_label} | `{result.output_dir}` |"
            )
    if record.cad_check.status != "not_started":
        cad_check = record.cad_check
        lines.extend(
            [
                "",
                "## FreeCAD 几何检查",
                "",
                f"- 状态: {cad_check.status}",
                f"- 更新时间: {cad_check.updated_at or '未记录'}",
                f"- 输出目录: `{cad_check.output_dir or ''}`",
                f"- 处理文件: {cad_check.processed_files}",
                f"- 失败文件: {cad_check.failed_files}",
                f"- PASS 文件: {cad_check.pass_files}",
                f"- 说明: {cad_check.message}",
                "",
                "| 文件 | 状态 | 质量 | 实体 | bbox | 输出 |",
                "| --- | --- | --- | ---: | --- | --- |",
            ]
        )
        for result in cad_check.results:
            bbox = result.assembly_bbox_mm
            bbox_label = ""
            if bbox:
                bbox_label = f"{bbox.get('size_x')} x {bbox.get('size_y')} x {bbox.get('size_z')} mm"
            lines.append(
                f"| {result.file_name} | {result.status} | {result.quality_status or ''} | "
                f"{result.solid_count or 0} | {bbox_label} | `{result.output_dir}` |"
            )
    (output_dir / "INTAKE_PENDING_REVIEW.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (output_dir / "intake_record.json").write_text(record.model_dump_json(indent=2), encoding="utf-8")


def create_drawing_sheetmetal_intake_record(payload: DrawingSheetMetalIntakeCreate) -> DrawingSheetMetalIntakeRecord:
    decoded_files: list[tuple[str, bytes, str, str]] = []
    total_size = 0
    for upload_file in payload.files:
        file_name = safe_upload_filename(upload_file.name)
        suffix = Path(file_name).suffix.lower()
        if suffix not in SUPPORTED_DRAWING_SHEETMETAL_SUFFIXES:
            allowed = ", ".join(sorted(SUPPORTED_DRAWING_SHEETMETAL_SUFFIXES))
            raise HTTPException(status_code=400, detail=f"{file_name} is not supported. Allowed suffixes: {allowed}")
        content = decode_upload_base64(upload_file)
        total_size += len(content)
        if total_size > MAX_DRAWING_SHEETMETAL_UPLOAD_BYTES:
            raise HTTPException(
                status_code=413,
                detail=f"Upload batch exceeds {MAX_DRAWING_SHEETMETAL_UPLOAD_BYTES} bytes. Split large CAD files into smaller intake batches.",
            )
        decoded_files.append((file_name, content, suffix, drawing_sheetmetal_source_category(suffix)))

    intake_id = make_drawing_sheetmetal_intake_id()
    created_at = now_iso()
    output_dir = DRAWING_SHEETMETAL_INTAKE_DIR / intake_id
    output_dir.mkdir(parents=True, exist_ok=False)
    file_records: list[DrawingSheetMetalIntakeFileRecord] = []
    for file_name, content, suffix, category in decoded_files:
        target_path = unique_output_path(output_dir, file_name)
        target_path.write_bytes(content)
        file_records.append(
            DrawingSheetMetalIntakeFileRecord(
                file_name=target_path.name,
                saved_path=str(target_path),
                size_bytes=len(content),
                suffix=suffix,
                source_category=category,
            )
        )

    record = DrawingSheetMetalIntakeRecord(
        id=intake_id,
        status="intake_received",
        created_at=created_at,
        source_type=payload.source_type,
        notes=payload.notes.strip(),
        output_dir=str(output_dir),
        files=file_records,
        next_action=drawing_sheetmetal_next_action(file_records),
    )
    write_drawing_sheetmetal_intake_note(record)
    records = [record, *read_drawing_sheetmetal_intake_records()]
    write_drawing_sheetmetal_intake_records(records)
    return record


def drawing_sheetmetal_record_output_dir(record: DrawingSheetMetalIntakeRecord) -> Path:
    output_dir = Path(record.output_dir)
    if not output_dir.is_absolute():
        raise HTTPException(status_code=500, detail=f"Intake output dir is not absolute: {record.output_dir}")
    if not output_dir.exists():
        raise HTTPException(status_code=404, detail=f"Intake output dir does not exist: {output_dir}")
    resolved = output_dir.resolve()
    intake_root = DRAWING_SHEETMETAL_INTAKE_DIR.resolve()
    if resolved != intake_root and not is_under_root(resolved, intake_root):
        raise HTTPException(status_code=403, detail=f"Intake output dir is outside intake root: {resolved}")
    return resolved


def dxf_rule_seed_candidate(summary: dict[str, Any]) -> bool:
    return summary.get("quality_status") in {"rule_seed_candidate", "geometry_rule_seed_only"}


def dxf_extraction_result_from_summary(
    file: DrawingSheetMetalIntakeFileRecord,
    out_dir: Path,
    summary: dict[str, Any],
) -> DrawingSheetMetalExtractionFileResult:
    loop_analysis = summary.get("loop_analysis") if isinstance(summary.get("loop_analysis"), dict) else {}
    manufacturing_bbox = summary.get("manufacturing_bbox_mm")
    if not isinstance(manufacturing_bbox, dict):
        manufacturing_bbox = {}
    return DrawingSheetMetalExtractionFileResult(
        file_name=file.file_name,
        status="completed",
        output_dir=str(out_dir),
        summary_path=str(out_dir / "sheetmetal_reference_summary.json"),
        card_path=str(out_dir / "sheetmetal_reference_card.md"),
        quality_status=summary.get("quality_status"),
        role_guess=summary.get("role_guess"),
        manufacturing_bbox_mm={
            "min_x": manufacturing_bbox.get("min_x"),
            "min_y": manufacturing_bbox.get("min_y"),
            "max_x": manufacturing_bbox.get("max_x"),
            "max_y": manufacturing_bbox.get("max_y"),
            "width": manufacturing_bbox.get("width"),
            "height": manufacturing_bbox.get("height"),
        },
        circle_count=summary.get("circle_count"),
        closed_loop_count=loop_analysis.get("closed_loop_count"),
        message="DXF 轻量解析完成。",
    )


def failed_dxf_extraction_result(
    file: DrawingSheetMetalIntakeFileRecord,
    out_dir: Path,
    message: str,
) -> DrawingSheetMetalExtractionFileResult:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "sheetmetal_reference_error.txt").write_text(message, encoding="utf-8", errors="replace")
    return DrawingSheetMetalExtractionFileResult(
        file_name=file.file_name,
        status="failed",
        output_dir=str(out_dir),
        message=message,
    )


def update_drawing_sheetmetal_intake_record(record: DrawingSheetMetalIntakeRecord) -> DrawingSheetMetalIntakeRecord:
    records = read_drawing_sheetmetal_intake_records()
    updated = False
    next_records: list[DrawingSheetMetalIntakeRecord] = []
    for item in records:
        if item.id == record.id:
            next_records.append(record)
            updated = True
        else:
            next_records.append(item)
    if not updated:
        raise HTTPException(status_code=404, detail=f"Drawing intake {record.id} was not found.")
    write_drawing_sheetmetal_intake_records(next_records)
    write_drawing_sheetmetal_intake_note(record)
    return record


def fetch_drawing_sheetmetal_intake_record(intake_id: str) -> DrawingSheetMetalIntakeRecord:
    for record in read_drawing_sheetmetal_intake_records():
        if record.id == intake_id:
            return record
    raise HTTPException(status_code=404, detail=f"Drawing intake {intake_id} was not found.")


def run_drawing_sheetmetal_dxf_extraction(intake_id: str) -> DrawingSheetMetalIntakeRecord:
    record = fetch_drawing_sheetmetal_intake_record(intake_id)
    record_dir = drawing_sheetmetal_record_output_dir(record)
    dxf_files = [file for file in record.files if file.suffix == ".dxf"]
    extraction_root = record_dir / "dxf_extraction"
    extraction_root.mkdir(parents=True, exist_ok=True)

    if not dxf_files:
        record.dxf_extraction = DrawingSheetMetalExtractionSummary(
            status="not_applicable",
            updated_at=now_iso(),
            output_dir=str(extraction_root),
            message="该 intake 没有 DXF 文件；PDF/图片/STEP/SolidWorks 文件需要走对应识别或 CAD 检查路线。",
        )
        record.next_action = "没有可直接轻量解析的 DXF。下一步按文件类型进入图片识别、STEP/FCStd 几何检查或 SolidWorks 工程图处理。"
        return update_drawing_sheetmetal_intake_record(record)

    if not DRAWING_SHEETMETAL_DXF_EXTRACTOR.exists():
        raise HTTPException(status_code=500, detail=f"DXF extractor was not found: {DRAWING_SHEETMETAL_DXF_EXTRACTOR}")

    results: list[DrawingSheetMetalExtractionFileResult] = []
    quality_counts: dict[str, int] = {}
    timeout_seconds = int(os.getenv("STUDIO_DXF_EXTRACTION_TIMEOUT_SECONDS", "90"))
    for file in dxf_files:
        source_path = Path(file.saved_path)
        out_dir = unique_output_path(extraction_root, Path(file.file_name).stem)
        try:
            if not source_path.exists():
                raise FileNotFoundError(f"DXF file is missing: {source_path}")
            completed = subprocess.run(
                [sys.executable, str(DRAWING_SHEETMETAL_DXF_EXTRACTOR), str(source_path), "--out-dir", str(out_dir)],
                cwd=ROOT_DIR,
                capture_output=True,
                text=True,
                timeout=timeout_seconds,
                check=False,
            )
            out_dir.mkdir(parents=True, exist_ok=True)
            (out_dir / "extract_stdout.txt").write_text(completed.stdout or "", encoding="utf-8", errors="replace")
            (out_dir / "extract_stderr.txt").write_text(completed.stderr or "", encoding="utf-8", errors="replace")
            if completed.returncode != 0:
                raise RuntimeError(completed.stderr.strip() or f"DXF extractor failed with exit code {completed.returncode}.")
            summary_path = out_dir / "sheetmetal_reference_summary.json"
            summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
            result = dxf_extraction_result_from_summary(file, out_dir, summary)
            if result.quality_status:
                quality_counts[result.quality_status] = quality_counts.get(result.quality_status, 0) + 1
            results.append(result)
        except Exception as error:  # Keep one bad DXF from blocking the rest of the intake batch.
            results.append(failed_dxf_extraction_result(file, out_dir, str(error)))

    failed_count = sum(1 for result in results if result.status == "failed")
    processed_count = len(results)
    rule_seed_count = 0
    for result in results:
        if result.status != "completed" or not result.summary_path:
            continue
        try:
            summary = json.loads(Path(result.summary_path).read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            continue
        if dxf_rule_seed_candidate(summary):
            rule_seed_count += 1

    if failed_count == 0:
        status: Literal["completed", "partial_failed", "failed"] = "completed"
        message = f"DXF 轻量解析完成：{processed_count} 个文件，{rule_seed_count} 个规则种子候选。"
    elif failed_count < processed_count:
        status = "partial_failed"
        message = f"DXF 轻量解析部分完成：{processed_count - failed_count}/{processed_count} 成功。"
    else:
        status = "failed"
        message = "DXF 轻量解析失败；请打开输出目录查看 extract_stderr.txt。"

    record.dxf_extraction = DrawingSheetMetalExtractionSummary(
        status=status,
        updated_at=now_iso(),
        output_dir=str(extraction_root),
        processed_files=processed_count,
        failed_files=failed_count,
        rule_seed_candidates=rule_seed_count,
        quality_status_counts=quality_counts,
        results=results,
        message=message,
    )
    record.next_action = (
        f"{message} 下一步：把 rule_seed_candidate/geometry_rule_seed_only 文件纳入门板、层板、横隔板规则比对。"
        if status != "failed"
        else message
    )
    return update_drawing_sheetmetal_intake_record(record)


def cad_check_result_from_summary(
    file: DrawingSheetMetalIntakeFileRecord,
    out_dir: Path,
    summary: dict[str, Any],
) -> DrawingSheetMetalCadCheckFileResult:
    bbox = summary.get("assembly_bbox_mm")
    if not isinstance(bbox, dict):
        bbox = {}
    return DrawingSheetMetalCadCheckFileResult(
        file_name=file.file_name,
        status="completed",
        output_dir=str(out_dir),
        summary_path=str(out_dir / "freecad_geometry_check.json"),
        report_path=str(out_dir / "freecad_geometry_check.md"),
        quality_status=summary.get("quality_status"),
        model_type=summary.get("model_type"),
        assembly_bbox_mm={
            "min_x": bbox.get("min_x"),
            "min_y": bbox.get("min_y"),
            "min_z": bbox.get("min_z"),
            "max_x": bbox.get("max_x"),
            "max_y": bbox.get("max_y"),
            "max_z": bbox.get("max_z"),
            "size_x": bbox.get("size_x"),
            "size_y": bbox.get("size_y"),
            "size_z": bbox.get("size_z"),
        },
        shape_object_count=summary.get("shape_object_count"),
        solid_count=summary.get("solid_count"),
        invalid_shape_count=summary.get("invalid_shape_count"),
        message="FreeCAD 几何检查完成。",
    )


def failed_cad_check_result(
    file: DrawingSheetMetalIntakeFileRecord,
    out_dir: Path,
    message: str,
) -> DrawingSheetMetalCadCheckFileResult:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "freecad_geometry_check_error.txt").write_text(message, encoding="utf-8", errors="replace")
    return DrawingSheetMetalCadCheckFileResult(
        file_name=file.file_name,
        status="failed",
        output_dir=str(out_dir),
        message=message,
    )


def run_drawing_sheetmetal_freecad_check(intake_id: str) -> DrawingSheetMetalIntakeRecord:
    record = fetch_drawing_sheetmetal_intake_record(intake_id)
    record_dir = drawing_sheetmetal_record_output_dir(record)
    cad_files = [file for file in record.files if file.suffix in {".step", ".stp", ".fcstd"}]
    check_root = record_dir / "freecad_geometry_check"
    check_root.mkdir(parents=True, exist_ok=True)

    if not cad_files:
        record.cad_check = DrawingSheetMetalCadCheckSummary(
            status="not_applicable",
            updated_at=now_iso(),
            output_dir=str(check_root),
            message="该 intake 没有 STEP/STP/FCStd 文件；SolidWorks 原生文件暂不交给 FreeCAD 自动打开。",
        )
        record.next_action = "没有可用 FreeCAD 轻量检查的 STEP/STP/FCStd。下一步按 DXF 解析、图片识别或 SolidWorks 原生检查路线处理。"
        return update_drawing_sheetmetal_intake_record(record)

    if not DRAWING_SHEETMETAL_FREECAD_CHECKER.exists():
        raise HTTPException(status_code=500, detail=f"FreeCAD checker was not found: {DRAWING_SHEETMETAL_FREECAD_CHECKER}")
    freecad_ok, freecad_cmd = resolve_freecad_cmd("FreeCADCmd.exe")
    if not freecad_ok:
        raise HTTPException(status_code=409, detail=freecad_cmd)

    results: list[DrawingSheetMetalCadCheckFileResult] = []
    quality_counts: dict[str, int] = {}
    timeout_seconds = int(os.getenv("STUDIO_FREECAD_INTAKE_CHECK_TIMEOUT_SECONDS", "180"))
    for file in cad_files:
        source_path = Path(file.saved_path)
        out_dir = unique_output_path(check_root, Path(file.file_name).stem)
        try:
            if not source_path.exists():
                raise FileNotFoundError(f"CAD file is missing: {source_path}")
            env = os.environ.copy()
            env["INTAKE_MODEL_PATH"] = str(source_path)
            env["INTAKE_CHECK_OUT_DIR"] = str(out_dir)
            python_code = f"import runpy; runpy.run_path(r'''{DRAWING_SHEETMETAL_FREECAD_CHECKER}''', run_name='__main__')"
            completed = subprocess.run(
                [freecad_cmd, "-c", python_code],
                cwd=ROOT_DIR,
                env=env,
                capture_output=True,
                text=True,
                timeout=timeout_seconds,
                check=False,
            )
            out_dir.mkdir(parents=True, exist_ok=True)
            (out_dir / "freecad_stdout.txt").write_text(completed.stdout or "", encoding="utf-8", errors="replace")
            (out_dir / "freecad_stderr.txt").write_text(completed.stderr or "", encoding="utf-8", errors="replace")
            if completed.returncode != 0:
                raise RuntimeError(completed.stderr.strip() or f"FreeCAD checker failed with exit code {completed.returncode}.")
            summary = json.loads((out_dir / "freecad_geometry_check.json").read_text(encoding="utf-8-sig"))
            result = cad_check_result_from_summary(file, out_dir, summary)
            if result.quality_status:
                quality_counts[result.quality_status] = quality_counts.get(result.quality_status, 0) + 1
            results.append(result)
        except Exception as error:  # Keep one bad CAD file from blocking the rest of the intake batch.
            results.append(failed_cad_check_result(file, out_dir, str(error)))

    failed_count = sum(1 for result in results if result.status == "failed")
    processed_count = len(results)
    pass_count = sum(1 for result in results if result.quality_status == "geometry_check_pass")
    if failed_count == 0:
        status: Literal["completed", "partial_failed", "failed"] = "completed"
        message = f"FreeCAD 几何检查完成：{processed_count} 个文件，{pass_count} 个 PASS。"
    elif failed_count < processed_count:
        status = "partial_failed"
        message = f"FreeCAD 几何检查部分完成：{processed_count - failed_count}/{processed_count} 成功。"
    else:
        status = "failed"
        message = "FreeCAD 几何检查失败；请打开输出目录查看 freecad_stderr.txt。"

    record.cad_check = DrawingSheetMetalCadCheckSummary(
        status=status,
        updated_at=now_iso(),
        output_dir=str(check_root),
        processed_files=processed_count,
        failed_files=failed_count,
        pass_files=pass_count,
        quality_status_counts=quality_counts,
        results=results,
        message=message,
    )
    record.next_action = (
        f"{message} 下一步：把 PASS 的 STEP/FCStd bbox、实体数和 invalid shape 数写入规则证据。"
        if status != "failed"
        else message
    )
    return update_drawing_sheetmetal_intake_record(record)


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
            gate_ok, gate_message = solidworks_execution_quality_gate(
                Path(execution_result.output_dir),
                execution_result.outputs,
                execution_result.exit_code,
                execution_result.solidworks_quality_status,
            )
            if gate_ok:
                status = "completed_reference"
                execution_result.status = "completed_reference"
            else:
                status = "failed_worker"
                execution_result.status = "failed_worker"
                execution_result.message = gate_message
            if gate_ok and execution_result.solidworks_quality_status == "reference_feature_only":
                execution_result.message = (
                    "SolidWorks worker wrote a visual engineering-reference assembly, but diagnostics found "
                    "reference features instead of a traversable component tree."
                )
            elif gate_ok and execution_result.solidworks_quality_status == "component_reference_tree":
                execution_result.message = (
                    "SolidWorks worker wrote a validated component-reference assembly; manifest and validation counts match."
                )
            elif gate_ok:
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
        HANDOFF_DIR.resolve(),
        WORKER_LOG_DIR.resolve(),
        RULE_EXTRACTION_DIR.resolve(),
        DRAWING_SHEETMETAL_INTAKE_DIR.resolve(),
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


def open_path_with_windows(path: Path, mode: Literal["open", "reveal"]) -> str:
    try:
        if path.is_dir():
            subprocess.Popen(["explorer.exe", str(path)])
            return "已打开输出目录。"
        if mode == "reveal":
            subprocess.Popen(["explorer.exe", f"/select,{path}"])
            return "已在资源管理器中定位文件。"
        os.startfile(str(path))  # type: ignore[attr-defined]
        return "已请求 Windows 使用默认程序打开文件。"
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


def command_tokens(command: str) -> list[str]:
    if command == "not_enabled":
        return []
    try:
        return [token.strip('"') for token in shlex.split(command, posix=False)]
    except ValueError:
        return [token.strip('"') for token in command.split()]


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
    script_path = Path(script_token)
    if script_path.is_absolute():
        return script_path.exists(), str(script_path)

    candidate = CAD_WORKSPACE / script_path
    if "*" in script_token:
        matches = list(CAD_WORKSPACE.glob(script_token.replace("\\", "/")))
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


def solidworks_16029_source_root() -> Path:
    return Path(os.getenv("STUDIO_SOLIDWORKS_SOURCE_ROOT", r"C:\sw16029_direct_18door\source"))


def solidworks_16029_native_skeleton_series_dir() -> Path:
    return Path(
        os.getenv(
            "STUDIO_SOLIDWORKS_16029_NATIVE_SKELETON_SERIES_DIR",
            str(GENERATED_MODEL_DIR / "SW-NATIVE-16029-CABINET-SKELETON-SERIES-20260521"),
        )
    )


def solidworks_16029_template_source(door_count: int) -> tuple[Path, str]:
    skeleton_dir = solidworks_16029_native_skeleton_series_dir()
    sources = {
        10: (
            skeleton_dir / "native_16029_10door_cabinet_skeleton_v2.SLDASM",
            "16029 10-door native SolidWorks cabinet skeleton v2",
        ),
        12: (
            skeleton_dir / "native_16029_12door_cabinet_skeleton_v2.SLDASM",
            "16029 12-door native SolidWorks cabinet skeleton v2",
        ),
        14: (
            skeleton_dir / "native_16029_14door_cabinet_skeleton_v2.SLDASM",
            "16029 14-door native SolidWorks cabinet skeleton v2",
        ),
    }
    if door_count not in sources:
        supported = ", ".join(str(value) for value in sorted(SUPPORTED_16029_SOLIDWORKS_TEMPLATE_DOOR_COUNTS))
        raise HTTPException(
            status_code=400,
            detail=(
                "16029 SolidWorks native cabinet skeleton currently supports only "
                f"{supported}-door output. Other door counts stay in rule-learning until their "
                "door-frame, shelf, lock, hinge and BOM/DXF rules are verified."
            ),
        )
    return sources[door_count]


def solidworks_16029_template_source_check(door_count: int) -> tuple[bool, str]:
    try:
        source_path, variant_label = solidworks_16029_template_source(door_count)
    except HTTPException as error:
        return False, str(error.detail)
    if not source_path.exists():
        return False, f"16029 SolidWorks native cabinet skeleton source was not found: {source_path}"
    return True, f"{variant_label}; source={source_path}"


def solidworks_16029_layout_info(door_count: int) -> tuple[int, float, int | None, str]:
    if door_count % 2:
        raise ValueError("door_count must be even.")
    rows_per_column = door_count // 2
    if rows_per_column < 2 or rows_per_column > 12:
        raise ValueError("door_count is outside the equal-row 4..24 even-door layout range.")
    door_height = (1827.0 - 2.0 - 2.0 - (rows_per_column - 1) * 7.0) / rows_per_column
    unit = round((door_height + 7.0) / 152.5)
    exact_unit = unit if 1 <= unit <= 6 and abs(((door_height + 7.0) / 152.5) - unit) < 0.01 else None
    descriptor = f"{rows_per_column} rows @ {door_height:.3f} mm"
    return rows_per_column, door_height, exact_unit, descriptor


def solidworks_16029_layout_units(door_count: int) -> list[int]:
    rows_per_column, _door_height, exact_unit, _descriptor = solidworks_16029_layout_info(door_count)
    return [exact_unit] * rows_per_column if exact_unit else []


def solidworks_16029_door_source_candidates(units: int) -> list[str]:
    candidates = {
        1: ["door_1_12_assembly.SLDASM", "储物柜门1╱12装配.SLDASM"],
        2: ["door_2_12_assembly_alias.SLDASM", "door_2_12_assembly.SLDASM", "储物柜门2╱12装配.SLDASM"],
        3: ["door_3_12_assembly.SLDASM", "储物柜门3╱12装配.SLDASM"],
        4: ["door_4_12_assembly.SLDASM", "储物柜门4╱12装配.SLDASM", "door_panel_4_12.SLDPRT"],
        5: ["door_5_12_assembly.SLDASM", "储物柜门5╱12装配.SLDASM", "door_panel_5_12.SLDPRT"],
        6: ["door_6_12_assembly.SLDASM", "储物柜门6╱12装配.SLDASM", "door_panel_6_12.SLDPRT"],
    }
    return candidates.get(units, [f"door_panel_{units}_12.SLDPRT"])


def solidworks_16029_required_source_groups(door_count: int) -> list[tuple[str, list[str]]]:
    _rows_per_column, _door_height, exact_unit, _descriptor = solidworks_16029_layout_info(door_count)
    groups: list[tuple[str, list[str]]] = [
        ("base_outer_frame", ["base_outer_frame.SLDPRT", "底座外框.sldprt"]),
        ("base_bottom_panel", ["base_bottom_panel.SLDPRT", "底座底板.sldprt"]),
        ("base_stiffener", ["base_stiffener.SLDPRT", "底座加强筋.sldprt"]),
        ("base_model", ["base_model.SLDPRT", "底座 模型.sldprt"]),
        ("cabinet_left_side", ["cabinet_left_side_native.SLDPRT", "箱体左侧板.sldprt"]),
        ("cabinet_right_side", ["cabinet_right_side_native.SLDPRT", "箱体右侧板.sldprt"]),
        ("cabinet_vertical_L", ["cabinet_vertical_L_native.SLDPRT", "箱体竖隔板L.sldprt"]),
        ("cabinet_vertical_R", ["cabinet_vertical_R_native.SLDPRT", "箱体竖隔板R.SLDPRT"]),
        ("door_frame_top", ["door_frame_top.SLDPRT", "门框 上.sldprt"]),
        ("door_frame_bottom", ["door_frame_bottom.SLDPRT", "门框 下.sldprt"]),
        ("door_frame_left", ["door_frame_left.SLDPRT", "门框 左.sldprt"]),
        ("door_frame_right", ["door_frame_right.SLDPRT", "门框 右.sldprt"]),
        ("door_frame_vertical_L", ["door_frame_vertical_L.SLDPRT", "门框 竖隔板L.sldprt"]),
        ("door_frame_vertical_R", ["door_frame_vertical_R.SLDPRT", "门框 竖隔板R.SLDPRT"]),
        ("door_frame_cross_L", ["door_frame_cross_L.SLDPRT", "门框 横隔板.sldprt"]),
        ("door_frame_cross_R", ["door_frame_cross_R.SLDPRT", "门框 横隔板R.SLDPRT"]),
        ("top_cover_bottom", ["top_cover_bottom.SLDPRT", "上盖壳体底板.sldprt"]),
        ("top_cover_front", ["top_cover_front.SLDPRT", "上盖壳体前侧板.sldprt"]),
        ("top_cover_back", ["top_cover_back.SLDPRT", "上盖壳体后侧板.sldprt"]),
        ("top_cover_left", ["top_cover_left.SLDPRT", "上盖壳体左侧板.sldprt"]),
        ("top_cover_right", ["top_cover_right.SLDPRT", "上盖壳体右侧板.SLDPRT"]),
        ("shelf_L", ["shelf_L_native.SLDPRT", "箱体横层板L.sldprt"]),
        ("shelf_R", ["shelf_R_native.SLDPRT", "箱体横层板R.SLDPRT"]),
    ]
    if exact_unit:
        groups.append((f"door_{exact_unit}_12", solidworks_16029_door_source_candidates(exact_unit)))
    elif door_count == 14:
        groups.append(("door_14_parametric_candidate", ["door_panel_14door_derived_production_candidate.SLDPRT"]))
    else:
        groups.append(("door_parametric_template", ["door_panel_2_12.SLDPRT", "储物柜门板2╱12.SLDPRT"]))
    return groups


def solidworks_16029_source_check(door_count: int) -> tuple[bool, str]:
    root = solidworks_16029_source_root()
    if not root.exists():
        return False, f"16029 SolidWorks source root was not found: {root}"
    try:
        groups = solidworks_16029_required_source_groups(door_count)
        _rows_per_column, _door_height, exact_unit, descriptor = solidworks_16029_layout_info(door_count)
    except ValueError as error:
        return False, str(error)
    missing: list[str] = []
    resolved: list[str] = []
    for label, candidates in groups:
        match = next((candidate for candidate in candidates if (root / candidate).exists()), None)
        if match:
            resolved.append(f"{label}={match}")
        else:
            missing.append(f"{label}: {' | '.join(candidates)}")
    if missing:
        return False, f"{root}; missing source groups: " + "; ".join(missing)
    if door_count in VERIFIED_LOCKER_16029_SOLIDWORKS_DOOR_COUNTS and exact_unit:
        status = "native single-part body + verified equal-row door source"
    elif door_count == 14:
        status = "native single-part body + equal-row rebuilt panel candidate"
    else:
        status = "native single-part body + equal-row parametric panel reference"
    return True, f"{root}; layout={descriptor}; status={status}; sources={len(resolved)} groups"


def active_solidworks_automation_processes() -> list[str]:
    command = [
        "powershell.exe",
        "-NoProfile",
        "-Command",
        (
            "$patterns = 'sw_build_locker|sw_clone_|sw_measure_part_bboxes|"
            "sw_clean_reference_display|sw_diagnose_assembly_quality|sw_export_step'; "
            "Get-CimInstance Win32_Process | "
            "Where-Object { $_.Name -match '^(cscript|wscript)\\.exe$' -and $_.CommandLine -match $patterns } | "
            "ForEach-Object { \"$($_.ProcessId)|$($_.CommandLine)\" }"
        ),
    ]
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=5,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return []
    if completed.returncode != 0:
        return []
    return [line.strip() for line in (completed.stdout or "").splitlines() if line.strip()]


def solidworks_run_guard_status() -> tuple[bool, str]:
    active = active_solidworks_automation_processes()
    if active:
        return False, "Active SolidWorks automation process detected: " + " ; ".join(active[:3])

    if SOLIDWORKS_RUN_LOCK_PATH.exists():
        stale_seconds = int(os.getenv("STUDIO_SOLIDWORKS_LOCK_STALE_SECONDS", "7200"))
        try:
            age_seconds = max(0.0, datetime.now(timezone.utc).timestamp() - SOLIDWORKS_RUN_LOCK_PATH.stat().st_mtime)
        except OSError:
            age_seconds = 0.0
        if age_seconds <= stale_seconds:
            detail = read_optional_text(SOLIDWORKS_RUN_LOCK_PATH).strip() or str(SOLIDWORKS_RUN_LOCK_PATH)
            return False, f"SolidWorks run lock is active: {detail}"
        try:
            SOLIDWORKS_RUN_LOCK_PATH.unlink()
        except OSError as error:
            return False, f"Stale SolidWorks run lock could not be removed: {error}"

    return True, "No active SolidWorks automation process or run lock."


def run_solidworks_16029_spec_preview(script_path: str, door_count: int, task_id: str) -> tuple[bool, str]:
    if shutil.which("cscript.exe") is None:
        return False, "cscript.exe not found in PATH."
    output_dir = MANUAL_RUN_DIR / task_id / "preflight_spec"
    output_dir.mkdir(parents=True, exist_ok=True)
    output_name = f"{task_id}-spec"
    command = [
        "cscript.exe",
        "//Nologo",
        script_path,
        str(output_dir),
        output_name,
        str(solidworks_16029_source_root()),
        str(door_count),
        "spec_only",
    ]
    stdout_path = WORKER_LOG_DIR / f"{task_id}-solidworks-spec-preview.stdout.txt"
    stderr_path = WORKER_LOG_DIR / f"{task_id}-solidworks-spec-preview.stderr.txt"
    try:
        completed = subprocess.run(
            command,
            cwd=CAD_WORKSPACE,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=int(os.getenv("STUDIO_SOLIDWORKS_SPEC_TIMEOUT_SECONDS", "30")),
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as error:
        stderr_path.write_text(str(error), encoding="utf-8", errors="replace")
        return False, f"16029 spec preview could not run: {error}"
    stdout_path.write_text(completed.stdout or "", encoding="utf-8", errors="replace")
    stderr_path.write_text(completed.stderr or "", encoding="utf-8", errors="replace")
    detail_lines = [
        line.strip()
        for line in (completed.stdout or "").splitlines()
        if line.strip().startswith(("components=", "missing_sources=", "category_status=", "layout_units=", "layout_rule_status=", "spec_report="))
    ]
    detail = "; ".join(detail_lines) if detail_lines else (completed.stderr or completed.stdout or "").strip()
    if completed.returncode != 0:
        return False, detail or f"16029 spec preview failed with exit code {completed.returncode}."
    return True, detail or "16029 spec preview passed."


def acquire_solidworks_run_lock(task_id: str) -> tuple[bool, str]:
    WORKER_LOG_DIR.mkdir(parents=True, exist_ok=True)
    guard_ok, guard_detail = solidworks_run_guard_status()
    if not guard_ok:
        return False, guard_detail
    payload = {
        "task_id": task_id,
        "pid": os.getpid(),
        "started_at": now_iso(),
        "note": "Only one SolidWorks automation run is allowed at a time.",
    }
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY
    try:
        fd = os.open(str(SOLIDWORKS_RUN_LOCK_PATH), flags)
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=2)
    except FileExistsError:
        detail = read_optional_text(SOLIDWORKS_RUN_LOCK_PATH).strip() or str(SOLIDWORKS_RUN_LOCK_PATH)
        return False, f"SolidWorks run lock is already active: {detail}"
    except OSError as error:
        return False, f"Could not create SolidWorks run lock: {error}"
    return True, str(SOLIDWORKS_RUN_LOCK_PATH)


def release_solidworks_run_lock(task_id: str) -> None:
    if not SOLIDWORKS_RUN_LOCK_PATH.exists():
        return
    try:
        payload = json.loads(SOLIDWORKS_RUN_LOCK_PATH.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        payload = {}
    if payload and str(payload.get("task_id")) != task_id:
        return
    try:
        SOLIDWORKS_RUN_LOCK_PATH.unlink()
    except OSError:
        pass


def acquire_freecad_run_lock(task_id: str) -> tuple[bool, str]:
    WORKER_LOG_DIR.mkdir(parents=True, exist_ok=True)
    if FREECAD_RUN_LOCK_PATH.exists():
        stale_seconds = int(os.getenv("STUDIO_FREECAD_LOCK_STALE_SECONDS", "7200"))
        try:
            age_seconds = max(0.0, datetime.now(timezone.utc).timestamp() - FREECAD_RUN_LOCK_PATH.stat().st_mtime)
        except OSError:
            age_seconds = 0.0
        if age_seconds <= stale_seconds:
            detail = read_optional_text(FREECAD_RUN_LOCK_PATH).strip() or str(FREECAD_RUN_LOCK_PATH)
            return False, f"FreeCAD run lock is already active: {detail}"
        try:
            FREECAD_RUN_LOCK_PATH.unlink()
        except OSError as error:
            return False, f"Stale FreeCAD run lock could not be removed: {error}"
    payload = {
        "task_id": task_id,
        "pid": os.getpid(),
        "started_at": now_iso(),
        "note": "Only one FreeCAD worker/postprocess run is allowed at a time on this workstation.",
    }
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY
    try:
        fd = os.open(str(FREECAD_RUN_LOCK_PATH), flags)
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=2)
    except FileExistsError:
        detail = read_optional_text(FREECAD_RUN_LOCK_PATH).strip() or str(FREECAD_RUN_LOCK_PATH)
        return False, f"FreeCAD run lock is already active: {detail}"
    except OSError as error:
        return False, f"Could not create FreeCAD run lock: {error}"
    return True, str(FREECAD_RUN_LOCK_PATH)


def release_freecad_run_lock(task_id: str) -> None:
    if not FREECAD_RUN_LOCK_PATH.exists():
        return
    try:
        payload = json.loads(FREECAD_RUN_LOCK_PATH.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        payload = {}
    if payload and str(payload.get("task_id")) != task_id:
        return
    try:
        FREECAD_RUN_LOCK_PATH.unlink()
    except OSError:
        pass


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
        door_count: int | None = None
        try:
            door_count = int(raw_door_count)
        except ValueError:
            errors.append("door_count must be an integer.")
        else:
            if door_count < 2:
                errors.append("door_count must be at least 2.")
            elif door_count % 2:
                errors.append("door_count must be even for the current two-column 16029 layout.")
            else:
                supported_counts = (
                    SUPPORTED_16029_SOLIDWORKS_TEMPLATE_DOOR_COUNTS
                    if task.cad_runner == "solidworks"
                    else SUPPORTED_16029_FREECAD_RULE_DOOR_COUNTS
                )
                if door_count not in supported_counts:
                    supported = ", ".join(str(value) for value in sorted(supported_counts))
                    route = (
                        "SolidWorks native cabinet skeleton"
                        if task.cad_runner == "solidworks"
                        else "FreeCAD rule-validation"
                    )
                    errors.append(
                        f"{route} for 16029 currently supports only {supported}-door output. "
                        "Other door counts stay in rule-learning until their transform/mate and geometry gates pass."
                    )
        raw_cabinet_width = str(task.parameters.get("cabinet_width", "1000")).strip() or "1000"
        try:
            cabinet_width = float(raw_cabinet_width)
        except ValueError:
            errors.append("cabinet_width must be numeric for the 16029 mainline.")
        else:
            if abs(cabinet_width - 1000.0) > 0.001:
                errors.append(
                    "16029 generation handoff currently supports only the 1000mm-wide, 1917mm-high, 550mm-deep outer size; "
                    "width-rule experiments must not enter this engineering-reference queue."
                )
        geometry_source = str(task.parameters.get("geometry_source", "auto")).strip() or "auto"
        if geometry_source != "auto":
            errors.append("16029 generation handoff uses the locked evidence-backed route; geometry_source must remain auto.")
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
        extra_checks: list[DryRunCheck] = []
        if task.cad_runner == "solidworks":
            guard_ok, guard_detail = solidworks_run_guard_status()
            extra_checks.append(
                DryRunCheck(
                    name="solidworks_single_run_guard",
                    ok=guard_ok,
                    detail=guard_detail,
                )
            )
        if (
            task.cad_runner == "solidworks"
            and task.capability_id == "locker_16029_regression"
            and script_token
            and Path(script_token).name == "sw_build_locker_16029_direct_assembly.js"
        ):
            try:
                source_door_count = int(str(task.parameters.get("door_count", "")).strip())
            except ValueError:
                source_ok, source_detail = False, "door_count must be an integer before checking SolidWorks source files."
            else:
                source_ok, source_detail = solidworks_16029_source_check(source_door_count)
            extra_checks.append(
                DryRunCheck(
                    name="solidworks_16029_source_files",
                    ok=source_ok,
                    detail=source_detail,
                )
            )
            if source_ok:
                spec_ok, spec_detail = run_solidworks_16029_spec_preview(script_detail, source_door_count, task.id)
                extra_checks.append(
                    DryRunCheck(
                        name="solidworks_16029_spec_preview",
                        ok=spec_ok,
                        detail=spec_detail,
                    )
                )
        if (
            task.cad_runner == "solidworks"
            and task.capability_id == "locker_16029_regression"
            and script_token
            and Path(script_token).name == "sw_clone_16029_baseline_template.js"
        ):
            try:
                source_door_count = int(str(task.parameters.get("door_count", "")).strip())
            except ValueError:
                template_ok, template_detail = False, "door_count must be an integer before checking the 16029 native skeleton source."
            else:
                template_ok, template_detail = solidworks_16029_template_source_check(source_door_count)
            extra_checks.append(
                DryRunCheck(
                    name="solidworks_16029_native_skeleton_source",
                    ok=template_ok,
                    detail=template_detail,
                )
            )

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
            + extra_checks
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


def solidworks_execution_quality_gate(
    output_dir: Path,
    outputs: list[str],
    exit_code: int | None,
    quality_status: str | None,
) -> tuple[bool, str]:
    has_native_output = any(path.lower().endswith((".sldasm", ".sldprt")) for path in outputs)
    if exit_code not in (None, 0):
        return False, f"SolidWorks worker failed with exit code {exit_code}."
    if not has_native_output:
        return False, "SolidWorks worker finished without a native SLDASM/SLDPRT output."

    build_report_path = first_output_path(outputs, "_build_report.md")
    validation_report_path = first_output_path(outputs, "_solidworks_validation_report.md")
    build_text = read_optional_text(build_report_path)
    validation_text = read_optional_text(validation_report_path)
    title_line = build_text.splitlines()[0].strip().lower() if build_text else ""
    strict_direct_gate = "direct 16029 locker assembly" in title_line or "SolidWorks Direct Assembly Validation" in validation_text

    if not strict_direct_gate:
        return True, "SolidWorks native output exists; no direct-component validation report was required for this route."

    if quality_status == "reference_feature_only":
        return False, "SolidWorks output is reference-feature-only; it is not accepted as a generated model."
    if quality_status == "component_reference_tree":
        return False, (
            "SolidWorks direct component assembly is blocked after visual QA found datum/transform misalignment; "
            "use the 16029 10/12/14-door native cabinet skeleton route until the transform-backed route is implemented."
        )
    if not build_text or not validation_text:
        return False, "SolidWorks direct assembly is missing build or validation report."

    failures: list[str] = []
    requested = first_int(markdown_bullet_value(build_text, "Requested components"))
    added = first_int(markdown_bullet_value(build_text, "Added components"))
    if requested is None or added is None:
        failures.append("component insertion counts are missing")
    elif added != requested:
        failures.append(f"component insertion count mismatch: added {added} / requested {requested}")

    _pass_count, _warn_count, fail_count = validation_counts(validation_text)
    if fail_count is None:
        failures.append("validation fail count is missing")
    elif fail_count != 0:
        failures.append(f"validation has {fail_count} failed rows")

    category_status = (markdown_bullet_value(validation_text, "Category count check") or "").lower()
    if category_status != "pass":
        failures.append(f"category count check is {category_status or 'missing'}")

    if failures:
        return False, "SolidWorks direct assembly quality gate failed: " + "; ".join(failures) + "."
    return True, "SolidWorks direct assembly quality gate passed."


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
    outputs = output_files(output_dir)
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
    elif "native cabinet skeleton" in title_line:
        generation_mode = "native_skeleton_reference"
        interpretation = "打开并另存已验证的 10/12/14 门 SolidWorks 原生整柜骨架；用于工程参考和后续模块补齐。"
    elif "template clone" in title_line:
        generation_mode = "template_clone"
        interpretation = "复制并保存 SolidWorks 10/12门模板，绑定为同尺寸门数组参考；当前不是逐个零件重排。"
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

    if quality_status == "component_reference_tree":
        next_action = "下一步：打开 SLDASM 和验证报告复核；该模型是验证通过的 SolidWorks 组件引用装配，可节省结构摆放和变体检查时间。"
    elif quality_status == "reference_feature_only":
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
        layout_units=markdown_bullet_value(build_text, "Layout units per column")
        or markdown_bullet_value(validation_text, "Layout units per column"),
        layout_rule_status=markdown_bullet_value(build_text, "Layout rule status")
        or markdown_bullet_value(validation_text, "Layout rule status"),
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


def list_rule_extraction_results(limit: int = 20) -> list[RuleExtractionResult]:
    if not RULE_EXTRACTION_DIR.exists():
        return []
    results = [
        result
        for output_dir in sorted(RULE_EXTRACTION_DIR.iterdir(), key=lambda item: item.stat().st_mtime, reverse=True)
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
    if shutil.which("cscript.exe") is None:
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
    export_script = CAD_WORKSPACE / "scripts" / "sw_export_step_ascii.js"
    inspect_script = ROOT_DIR / "workers" / "maintenance" / "inspect_step_assembly_bboxes_freecad.py"
    binding_script = ROOT_DIR / "workers" / "maintenance" / "bind_step_bbox_component_roles.py"
    step_path = output_dir / f"{result.id}_assembly_export.step"

    if export_script.exists() and shutil.which("cscript.exe") is not None:
        export_completed = subprocess.run(
            ["cscript.exe", "//Nologo", str(export_script), result.assembly_path, str(step_path)],
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


def run_freecad_step_geometry_check(model_path: Path, out_dir: Path, timeout_seconds: int) -> dict[str, Any]:
    if not DRAWING_SHEETMETAL_FREECAD_CHECKER.exists():
        return {"status": "checker_missing", "message": str(DRAWING_SHEETMETAL_FREECAD_CHECKER)}
    freecad_ok, freecad_cmd = resolve_freecad_cmd("FreeCADCmd.exe")
    if not freecad_ok:
        return {"status": "freecad_missing", "message": freecad_cmd}
    out_dir.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env["INTAKE_MODEL_PATH"] = str(model_path)
    env["INTAKE_CHECK_OUT_DIR"] = str(out_dir)
    python_code = f"import runpy; runpy.run_path(r'''{DRAWING_SHEETMETAL_FREECAD_CHECKER}''', run_name='__main__')"
    try:
        completed = subprocess.run(
            [freecad_cmd, "-c", python_code],
            cwd=ROOT_DIR,
            env=env,
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        (out_dir / "freecad_stdout.txt").write_text(error.stdout or "", encoding="utf-8", errors="replace")
        (out_dir / "freecad_stderr.txt").write_text(
            error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace"
        )
        return {"status": "step_geometry_check_timeout", "message": f"Timed out after {timeout_seconds} seconds."}
    except OSError as error:
        return {"status": "step_geometry_check_start_failed", "message": str(error)}
    (out_dir / "freecad_stdout.txt").write_text(completed.stdout or "", encoding="utf-8", errors="replace")
    (out_dir / "freecad_stderr.txt").write_text(completed.stderr or "", encoding="utf-8", errors="replace")
    summary_path = out_dir / "freecad_geometry_check.json"
    if completed.returncode != 0:
        return {
            "status": "step_geometry_check_failed",
            "exit_code": completed.returncode,
            "report": str(summary_path),
            "message": (completed.stderr or completed.stdout or "").strip()[-500:],
        }
    try:
        summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        return {"status": "step_geometry_check_invalid", "report": str(summary_path), "message": str(error)}
    return {
        "status": summary.get("quality_status", "unknown"),
        "report": str(summary_path),
        "invalid_shape_count": summary.get("invalid_shape_count"),
        "solid_count": summary.get("solid_count"),
        "shape_object_count": summary.get("shape_object_count"),
        "bbox": summary.get("assembly_bbox_mm"),
    }


def run_freecad_fcstd_integrity_check(model_path: Path, out_prefix: Path, expected_x_mm: float, timeout_seconds: int) -> dict[str, Any]:
    if not FREECAD_FCSTD_GEOMETRY_AUDITOR.exists():
        return {"status": "checker_missing", "message": str(FREECAD_FCSTD_GEOMETRY_AUDITOR)}
    freecad_ok, freecad_cmd = resolve_freecad_cmd("FreeCADCmd.exe")
    if not freecad_ok:
        return {"status": "freecad_missing", "message": freecad_cmd}
    out_prefix.parent.mkdir(parents=True, exist_ok=True)
    python_code = (
        "import runpy, sys; "
        f"sys.argv = {[str(FREECAD_FCSTD_GEOMETRY_AUDITOR), str(model_path), str(out_prefix), str(expected_x_mm)]!r}; "
        f"runpy.run_path(r'''{FREECAD_FCSTD_GEOMETRY_AUDITOR}''', run_name='__main__')"
    )
    stdout_path = out_prefix.with_name(out_prefix.name + "_freecad_stdout.txt")
    stderr_path = out_prefix.with_name(out_prefix.name + "_freecad_stderr.txt")
    try:
        completed = subprocess.run(
            [freecad_cmd, "-c", python_code],
            cwd=ROOT_DIR,
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(
            error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace"
        )
        return {
            "status": "fcstd_integrity_check_timeout",
            "report": str(out_prefix.with_suffix(".md")),
            "message": f"Timed out after {timeout_seconds} seconds.",
        }
    except OSError as error:
        return {"status": "fcstd_integrity_check_start_failed", "message": str(error)}
    stdout_path.write_text(completed.stdout or "", encoding="utf-8", errors="replace")
    stderr_path.write_text(completed.stderr or "", encoding="utf-8", errors="replace")
    report_path = out_prefix.with_suffix(".md")
    if completed.returncode != 0:
        return {
            "status": "fcstd_integrity_check_failed",
            "exit_code": completed.returncode,
            "report": str(report_path),
            "message": (completed.stderr or completed.stdout or "").strip()[-500:],
        }
    text = read_optional_text(report_path)
    status = first_match(text, r"^- status:\s*`?([^`\n]+)`?") or "unknown"
    return {
        "status": status,
        "report": str(report_path),
        "invalid_shape_objects": parse_float(first_match(text, r"^- invalid_shape_objects:\s*`?([^`\n]+)`?")),
        "center_vertical_signature_failures": parse_float(
            first_match(text, r"^- center_vertical_signature_failures:\s*`?([^`\n]+)`?")
        ),
        "total_bbox_x_len": parse_float(first_match(text, r"^- total_bbox_x_len:\s*`?([^`\n]+)`?")),
    }


def run_16029_quality_refresh_scripts(output_dir: Path) -> list[dict[str, Any]]:
    scripts = [
        LOCKER_16029_VARIANT_QUALITY_MATRIX_SCRIPT,
        LOCKER_16029_STRUCTURAL_RULE_AUDIT_SCRIPT,
        LOCKER_16029_VARIANT_QUALITY_MATRIX_SCRIPT,
        LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_SCRIPT,
    ]
    results: list[dict[str, Any]] = []
    timeout_seconds = int(os.getenv("STUDIO_16029_QUALITY_REFRESH_TIMEOUT_SECONDS", "90"))
    for index, script in enumerate(scripts, start=1):
        if not script.exists():
            results.append({"script": str(script), "status": "missing"})
            continue
        stdout_path = output_dir / f"quality_refresh_{index}_{script.stem}.stdout.txt"
        stderr_path = output_dir / f"quality_refresh_{index}_{script.stem}.stderr.txt"
        try:
            completed = subprocess.run(
                [sys.executable, str(script)],
                cwd=ROOT_DIR,
                capture_output=True,
                text=True,
                timeout=timeout_seconds,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
            stderr_path.write_text(
                error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace"
            )
            results.append(
                {
                    "script": str(script),
                    "status": "timeout",
                    "exit_code": None,
                    "stdout": str(stdout_path),
                    "stderr": str(stderr_path),
                }
            )
            continue
        except OSError as error:
            stderr_path.write_text(str(error), encoding="utf-8", errors="replace")
            results.append({"script": str(script), "status": "start_failed", "exit_code": None, "stderr": str(stderr_path)})
            continue
        stdout_path.write_text(completed.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(completed.stderr or "", encoding="utf-8", errors="replace")
        results.append(
            {
                "script": str(script),
                "status": "ok" if completed.returncode == 0 else "failed",
                "exit_code": completed.returncode,
                "stdout": str(stdout_path),
                "stderr": str(stderr_path),
            }
        )
    return results


def run_16029_freecad_postprocess(task: GenerationTask, output_dir: Path) -> dict[str, Any] | None:
    if task.capability_id != "locker_16029_regression" or task.cad_runner != "freecad":
        return None
    raw_door_count = str(task.parameters.get("door_count", "")).strip()
    if not raw_door_count:
        return {"status": "skipped_missing_door_count"}
    step_path = output_dir / f"locker_16029_{raw_door_count}door_rule_driven.step"
    fcstd_path = output_dir / f"locker_16029_{raw_door_count}door_rule_driven.FCStd"
    timeout_seconds = int(os.getenv("STUDIO_FREECAD_POSTPROCESS_TIMEOUT_SECONDS", "240"))
    postprocess: dict[str, Any] = {
        "door_count": raw_door_count,
        "step": str(step_path),
        "fcstd": str(fcstd_path),
    }
    if step_path.exists():
        postprocess["step_geometry_check"] = run_freecad_step_geometry_check(
            step_path,
            output_dir / "step_geometry_check",
            timeout_seconds,
        )
    else:
        postprocess["step_geometry_check"] = {"status": "missing_step", "message": str(step_path)}
    if fcstd_path.exists():
        postprocess["fcstd_integrity_check"] = run_freecad_fcstd_integrity_check(
            fcstd_path,
            output_dir / f"locker_16029_{raw_door_count}door_rule_driven_geometry_integrity",
            1000.0,
            timeout_seconds,
        )
    else:
        postprocess["fcstd_integrity_check"] = {"status": "missing_fcstd", "message": str(fcstd_path)}
    postprocess["quality_refresh"] = run_16029_quality_refresh_scripts(output_dir)
    postprocess_path = output_dir / "locker_16029_freecad_postprocess.json"
    postprocess["path"] = str(postprocess_path)
    postprocess_path.write_text(json.dumps(postprocess, ensure_ascii=False, indent=2), encoding="utf-8")
    return postprocess


def freecad_postprocess_quality(postprocess: dict[str, Any] | None) -> tuple[str | None, str | None, str | None]:
    if not postprocess:
        return None, None, None
    step = postprocess.get("step_geometry_check") if isinstance(postprocess.get("step_geometry_check"), dict) else {}
    fcstd = postprocess.get("fcstd_integrity_check") if isinstance(postprocess.get("fcstd_integrity_check"), dict) else {}
    refresh = postprocess.get("quality_refresh") if isinstance(postprocess.get("quality_refresh"), list) else []
    refresh_failed = any(isinstance(item, dict) and item.get("status") not in {"ok"} for item in refresh)
    step_ok = step.get("status") == "geometry_check_pass" and step.get("invalid_shape_count") == 0
    fcstd_ok = fcstd.get("status") == "PASS"
    report = str(postprocess.get("path") or "")
    if step_ok and fcstd_ok and not refresh_failed:
        return "freecad_geometry_pass", "FreeCAD 生成完成，STEP 几何和 FCStd 完整性检查均通过，16029 质量矩阵已刷新。", report
    if step_ok and not refresh_failed:
        return "freecad_step_pass_fcstd_pending", "FreeCAD 生成完成，STEP 几何通过；FCStd 完整性仍需复核。", report
    return "freecad_postprocess_needs_review", "FreeCAD 生成完成，但自动几何后处理未全部通过，请查看 postprocess 报告。", report


def solidworks_manual_command(task: GenerationTask, output_dir: Path) -> list[str]:
    tokens = command_tokens(task.command)
    if not tokens:
        raise HTTPException(status_code=409, detail="Task has no SolidWorks command.")

    script_token = find_script_token(tokens)
    script_ok, script_detail = resolve_script(script_token) if script_token else (False, "Missing SolidWorks script token.")
    if not script_ok:
        raise HTTPException(status_code=409, detail=script_detail)

    if Path(script_detail).name == "sw_build_locker_16029_direct_assembly.js":
        source_root = str(solidworks_16029_source_root())
        door_count = str(task.parameters.get("door_count", "18")).strip() or "18"
        return ["cscript.exe", "//Nologo", script_detail, str(output_dir), task.id, source_root, door_count]
    if Path(script_detail).name == "sw_clone_16029_baseline_template.js":
        raw_door_count = str(task.parameters.get("door_count", "12")).strip() or "12"
        try:
            door_count = int(raw_door_count)
        except ValueError as error:
            raise HTTPException(status_code=400, detail="door_count must be an integer.") from error
        source_path, variant_label = solidworks_16029_template_source(door_count)
        if not source_path.exists():
            raise HTTPException(status_code=404, detail=f"16029 SolidWorks native cabinet skeleton source was not found: {source_path}")
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


def solidworks_manual_script_name(command: list[str]) -> str:
    if len(command) >= 3 and Path(command[0]).name.lower() == "cscript.exe":
        return Path(command[2]).name
    return ""


def solidworks_command_runs_expensive_diagnostics(command: list[str]) -> bool:
    return solidworks_manual_script_name(command) != "sw_clone_16029_baseline_template.js"


def write_execution_log(result: WorkerExecutionResult) -> None:
    Path(result.log_path).write_text(result.model_dump_json(indent=2), encoding="utf-8")


def build_solidworks_manual_result(task: GenerationTask) -> WorkerExecutionResult:
    started_at = now_iso()
    output_dir = MANUAL_RUN_DIR / task.id
    output_dir.mkdir(parents=True, exist_ok=True)
    command = solidworks_manual_command(task, output_dir)
    run_expensive_diagnostics = solidworks_command_runs_expensive_diagnostics(command)
    clean_script = CAD_WORKSPACE / "scripts" / "sw_clean_reference_display.js"
    diagnostic_script = CAD_WORKSPACE / "scripts" / "sw_diagnose_assembly_quality.js"
    assembly_path = output_dir / f"{task.id}.SLDASM"
    run_script = output_dir / "run-solidworks-worker.ps1"
    run_lines = [
        "$ErrorActionPreference = 'Stop'",
        f"Set-Location -LiteralPath '{CAD_WORKSPACE}'",
        "& " + " ".join(shlex.quote(part) for part in command),
        f"$assemblyPath = '{assembly_path}'",
        f"$cleanScript = '{clean_script}'",
        "if ((Test-Path -LiteralPath $assemblyPath) -and (Test-Path -LiteralPath $cleanScript)) {",
        "  & cscript.exe //Nologo $cleanScript $assemblyPath",
        "}",
    ]
    if run_expensive_diagnostics:
        run_lines.extend(
            [
                f"$diagnosticScript = '{diagnostic_script}'",
                "if ((Test-Path -LiteralPath $assemblyPath) -and (Test-Path -LiteralPath $diagnosticScript)) {",
                f"  & cscript.exe //Nologo $diagnosticScript $assemblyPath '{output_dir}'",
                "}",
            ]
        )
    else:
        run_lines.append("# 16029 native skeleton save-as skips the expensive SolidWorks quality diagnostic by default.")
    run_lines.append("")
    run_script.write_text("\n".join(run_lines), encoding="utf-8-sig")
    notes = output_dir / "solidworks_manual_run_notes.md"
    notes.write_text(
        "\n".join(
            [
                "# SolidWorks manual run notes",
                "",
                f"- Door count requested by this task: `{task.parameters.get('door_count', '18')}`.",
                "- Run `run-solidworks-worker.ps1` from a desktop PowerShell session after confirming the SolidWorks 2025 license/session is available.",
                "- The script creates a SolidWorks-native `.SLDASM`, component manifest, build report, and validation report in this folder.",
                "- After native assembly creation, the runner hides reference planes, sketches, origins, and reference labels, then saves the cleaned first-open view.",
                "- 16029 native skeleton save-as skips the expensive quality diagnostic by default to avoid long SolidWorks sessions.",
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
    manual_command = solidworks_manual_command(task, output_dir)
    run_expensive_diagnostics = solidworks_command_runs_expensive_diagnostics(manual_command)
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

    lock_ok, lock_detail = acquire_solidworks_run_lock(task.id)
    if not lock_ok:
        stdout_path.write_text("", encoding="utf-8", errors="replace")
        stderr_path.write_text(lock_detail, encoding="utf-8", errors="replace")
        finished_at = now_iso()
        result = WorkerExecutionResult(
            task_id=task.id,
            status="failed_worker",
            cad_runner=task.cad_runner,
            started_at=started_at,
            finished_at=finished_at,
            command=command,
            cwd=str(output_dir),
            output_dir=str(output_dir),
            outputs=output_files(output_dir),
            stdout_path=str(stdout_path),
            stderr_path=str(stderr_path),
            log_path=str(log_path),
            exit_code=None,
            message="SolidWorks worker was not started because another SolidWorks automation run is active.",
        )
        write_execution_log(result)
        return result

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
        if assembly_path and run_expensive_diagnostics:
            quality_status, quality_summary = run_solidworks_quality_diagnostics(output_dir, assembly_path)
            outputs = output_files(output_dir)
        elif assembly_path:
            quality_status, quality_summary = read_solidworks_quality(output_dir, assembly_path)
        gate_ok, gate_message = solidworks_execution_quality_gate(output_dir, outputs, completed.returncode, quality_status)
        status: TaskStatus = "completed_reference" if gate_ok else "failed_worker"
        if status == "completed_reference":
            if quality_status == "component_reference_tree":
                message = "SolidWorks worker wrote a validated component-reference assembly with matching manifest and validation counts."
            elif quality_status == "reference_feature_only":
                message = (
                    "SolidWorks worker wrote a visual engineering-reference assembly, but diagnostics found "
                    "reference features instead of a traversable component tree."
                )
            else:
                message = gate_message
        elif completed.returncode == 0 and not has_native_output:
            message = "SolidWorks worker finished without an error code, but no native SLDASM/SLDPRT output was found."
        else:
            message = gate_message
    except subprocess.TimeoutExpired as error:
        stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace")
        exit_code = None
        outputs = output_files(output_dir)
        quality_status = None
        quality_summary = None
        status = "failed_worker"
        message = f"SolidWorks worker timed out after {timeout_seconds} seconds."
    except OSError as error:
        stdout_path.write_text("", encoding="utf-8", errors="replace")
        stderr_path.write_text(str(error), encoding="utf-8", errors="replace")
        exit_code = None
        outputs = output_files(output_dir)
        quality_status = None
        quality_summary = None
        status = "failed_worker"
        message = f"SolidWorks worker could not be started: {error}"
    finally:
        release_solidworks_run_lock(task.id)

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
    lock_ok, lock_detail = acquire_freecad_run_lock(task.id)
    if not lock_ok:
        stdout_path.write_text("", encoding="utf-8", errors="replace")
        stderr_path.write_text(lock_detail, encoding="utf-8", errors="replace")
        finished_at = now_iso()
        result = WorkerExecutionResult(
            task_id=task.id,
            status="failed_worker",
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
            exit_code=None,
            freecad_quality_status="freecad_run_lock_active",
            freecad_quality_summary="FreeCAD worker was not started because another FreeCAD generation or postprocess run is active.",
            message="FreeCAD worker was not started because another FreeCAD generation or postprocess run is active.",
        )
        write_execution_log(result)
        return result
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
            freecad_postprocess = run_16029_freecad_postprocess(task, output_dir)
        exit_code = completed.returncode
    except subprocess.TimeoutExpired as error:
        stdout_path.write_text(error.stdout or "", encoding="utf-8", errors="replace")
        stderr_path.write_text(error.stderr or f"Timed out after {timeout_seconds} seconds.", encoding="utf-8", errors="replace")
        status = "failed_worker"
        message = f"FreeCAD worker timed out after {timeout_seconds} seconds."
        exit_code = None
        freecad_postprocess = None
    except OSError as error:
        stdout_path.write_text("", encoding="utf-8", errors="replace")
        stderr_path.write_text(str(error), encoding="utf-8", errors="replace")
        status = "failed_worker"
        message = f"FreeCAD worker could not be started: {error}"
        exit_code = None
        freecad_postprocess = None
    finally:
        release_freecad_run_lock(task.id)

    finished_at = now_iso()
    freecad_quality_status, freecad_quality_summary, freecad_quality_report = freecad_postprocess_quality(
        freecad_postprocess if "freecad_postprocess" in locals() else None
    )
    if freecad_quality_summary and status == "completed_reference":
        message = freecad_quality_summary
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
        freecad_quality_status=freecad_quality_status,
        freecad_quality_summary=freecad_quality_summary,
        freecad_quality_report=freecad_quality_report,
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


@app.get("/api/drawing-sheetmetal-intake", response_model=list[DrawingSheetMetalIntakeRecord])
def list_drawing_sheetmetal_intake(limit: int = Query(default=12, ge=1, le=100)) -> list[DrawingSheetMetalIntakeRecord]:
    return read_drawing_sheetmetal_intake_records()[:limit]


@app.get("/api/sheetmetal-rule-evidence-16029")
def get_sheetmetal_rule_evidence_16029() -> dict[str, Any]:
    return read_sheetmetal_rule_evidence_16029()


@app.get("/api/locker-16029-variant-rule-packet")
def get_locker_16029_variant_rule_packet() -> dict[str, Any]:
    return read_locker_16029_variant_rule_packet()


@app.get("/api/locker-16029-variant-quality-matrix")
def get_locker_16029_variant_quality_matrix() -> dict[str, Any]:
    return read_locker_16029_variant_quality_matrix()


@app.get("/api/locker-16029-engineering-handoff-bundle")
def get_locker_16029_engineering_handoff_bundle() -> dict[str, Any]:
    return read_locker_16029_engineering_handoff_bundle()


@app.post("/api/drawing-sheetmetal-intake", response_model=DrawingSheetMetalIntakeRecord, status_code=201)
def create_drawing_sheetmetal_intake(payload: DrawingSheetMetalIntakeCreate) -> DrawingSheetMetalIntakeRecord:
    return create_drawing_sheetmetal_intake_record(payload)


@app.post("/api/drawing-sheetmetal-intake/{intake_id}/run-dxf-extraction", response_model=DrawingSheetMetalIntakeRecord)
def run_drawing_sheetmetal_intake_dxf_extraction(intake_id: str) -> DrawingSheetMetalIntakeRecord:
    return run_drawing_sheetmetal_dxf_extraction(intake_id)


@app.post("/api/drawing-sheetmetal-intake/{intake_id}/run-freecad-check", response_model=DrawingSheetMetalIntakeRecord)
def run_drawing_sheetmetal_intake_freecad_check(intake_id: str) -> DrawingSheetMetalIntakeRecord:
    return run_drawing_sheetmetal_freecad_check(intake_id)


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
    message = open_path_with_windows(path, payload.mode)
    return LocalActionResult(status="opened", path=str(path), message=message)


@app.post("/api/local-actions/open-freecad-model", response_model=LocalActionResult)
def open_freecad_model(payload: FreeCadOpenRequest) -> LocalActionResult:
    task = row_to_task(fetch_task_or_404(payload.task_id))
    output_dir = task_output_dir(task)
    fcstd = first_output_with_suffix(output_dir, ".fcstd")
    if not fcstd:
        raise HTTPException(status_code=404, detail=f"No FCStd model found in {output_dir}")
    if not FREECAD_EXE.exists():
        raise HTTPException(status_code=409, detail=f"FreeCAD.exe not found: {FREECAD_EXE}")

    write_freecad_opening_files(output_dir)
    launcher = output_dir / "open_in_freecad_with_view.py"
    launch_target = launcher if launcher.exists() else fcstd
    try:
        subprocess.Popen([str(FREECAD_EXE), str(launch_target)], cwd=str(output_dir))
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
    candidate_task = GenerationTask(
        id=task_id,
        cad_runner=payload.cad_runner,
        capability_id=payload.capability_id,
        capability_title=payload.capability_title,
        product_type=payload.product_type,
        module=payload.module,
        maturity=payload.maturity,
        output_level=payload.output_level,
        command=payload.command,
        parameters=payload.parameters,
        evidence=payload.evidence,
        limitation=payload.limitation,
        status=payload.status,
        created_at=created_at,
        updated_at=created_at,
    )
    parameter_errors = validate_task_parameters(candidate_task)
    if parameter_errors:
        raise HTTPException(status_code=400, detail="; ".join(parameter_errors))

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
    parameter_errors = validate_task_parameters(task)
    if parameter_errors:
        raise HTTPException(status_code=409, detail="; ".join(parameter_errors))

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
    parameter_errors = validate_task_parameters(task)
    if parameter_errors:
        raise HTTPException(status_code=409, detail="; ".join(parameter_errors))

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
