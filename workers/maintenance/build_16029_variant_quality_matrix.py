from __future__ import annotations

import csv
import json
import os
import re
import sqlite3
from collections import Counter
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
DB_PATH = Path(os.getenv("STUDIO_DB_PATH", ROOT_DIR / "data" / "studio.sqlite"))
GENERATED_MODEL_DIR = Path(os.getenv("STUDIO_GENERATED_MODEL_DIR", ROOT_DIR / "workers" / "generated_models"))
RULE_PACKET_PATH = Path(os.getenv("STUDIO_16029_VARIANT_RULE_PACKET_JSON", ROOT_DIR / "data" / "locker_16029_variant_rule_packet.json"))
OUTPUT_JSON_PATH = Path(os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_JSON", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.json"))
OUTPUT_MARKDOWN_PATH = Path(os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_MD", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.md"))
OUTPUT_CSV_PATH = Path(os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_CSV", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.csv"))
STRUCTURAL_RULE_AUDIT_PATH = Path(
    os.getenv("STUDIO_16029_STRUCTURAL_RULE_AUDIT_JSON", ROOT_DIR / "data" / "locker_16029_structural_rule_audit.json")
)
STRUCTURAL_RULE_AUDIT_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_STRUCTURAL_RULE_AUDIT_MD", ROOT_DIR / "data" / "locker_16029_structural_rule_audit.md")
)

TARGET_DOOR_COUNTS = [10, 12, 14]
EXPECTED_CABINET_WIDTH_MM = 1000.0
COUNT_TYPE_BY_GATE = {
    "door_modules": "door_module",
    "lock_holes": "lock_hole_reference",
    "lock_hooks": "lock_hook_reference",
    "shelves": "shelf_weld",
    "door_frame_horizontal_dividers": "door_frame_horizontal",
}


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_float(value: str | None) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def file_size_mb(path: Path | None) -> float | None:
    if not path or not path.exists():
        return None
    return round(path.stat().st_size / (1024 * 1024), 3)


def variant_by_door_count(rule_packet: dict[str, Any]) -> dict[int, dict[str, Any]]:
    variants: dict[int, dict[str, Any]] = {}
    for variant in rule_packet.get("variants", []):
        if isinstance(variant, dict):
            door_count = variant.get("door_count")
            if isinstance(door_count, int):
                variants[door_count] = variant
    return variants


def completed_freecad_tasks() -> list[dict[str, Any]]:
    if not DB_PATH.exists():
        return []
    with sqlite3.connect(DB_PATH) as connection:
        connection.row_factory = sqlite3.Row
        rows = connection.execute(
            """
            select id, cad_runner, capability_id, status, parameters_json, execution_json, created_at, updated_at
            from generation_tasks
            where capability_id = 'locker_16029_regression'
              and cad_runner = 'freecad'
              and status = 'completed_reference'
            order by created_at desc
            """
        ).fetchall()
    return [dict(row) for row in rows]


def task_output_dir(task: dict[str, Any]) -> Path | None:
    execution_raw = task.get("execution_json")
    if not execution_raw:
        return None
    try:
        execution = json.loads(execution_raw)
    except (TypeError, json.JSONDecodeError):
        return None
    output_dir = execution.get("output_dir")
    return Path(output_dir) if output_dir else None


def evidence_score(output_dir: Path, door_count: int) -> int:
    score = 0
    if (output_dir / f"locker_16029_{door_count}door_rule_driven.FCStd").exists():
        score += 5
    if (output_dir / f"locker_16029_{door_count}door_rule_driven.step").exists():
        score += 5
    step_check = output_dir / "step_geometry_check" / "freecad_geometry_check.json"
    if step_check.exists():
        score += 20
        try:
            data = json.loads(step_check.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError:
            data = {}
        if data.get("status") == "geometry_check_pass" and data.get("invalid_shape_count") == 0:
            score += 20
    geometry_md = output_dir / f"locker_16029_{door_count}door_rule_driven_geometry_integrity.md"
    if geometry_md.exists():
        score += 20
        text = geometry_md.read_text(encoding="utf-8-sig", errors="replace")
        if "- status: `PASS`" in text or "- status: PASS" in text:
            score += 20
    return score


def find_output_dir_from_tasks(door_count: int) -> tuple[Path | None, str | None, str]:
    candidates: list[tuple[int, float, Path, str]] = []
    for task in completed_freecad_tasks():
        try:
            parameters = json.loads(task.get("parameters_json") or "{}")
        except json.JSONDecodeError:
            continue
        if int(str(parameters.get("door_count", "0"))) != door_count:
            continue
        width = parse_float(str(parameters.get("cabinet_width", "1000")))
        if width is None or abs(width - EXPECTED_CABINET_WIDTH_MM) > 0.001:
            continue
        output_dir = task_output_dir(task)
        if output_dir and output_dir.exists():
            candidates.append((evidence_score(output_dir, door_count), output_dir.stat().st_mtime, output_dir, str(task.get("id"))))
    if candidates:
        candidates.sort(reverse=True)
        _, _, output_dir, task_id = candidates[0]
        return output_dir, task_id, "generation_task_best_evidence"
    return None, None, "not_found"


def fallback_output_dir(door_count: int) -> tuple[Path | None, str | None, str]:
    if door_count == 10:
        curated = GENERATED_MODEL_DIR / "FREECAD-16029-10DOOR-VARIANT-20260519"
        if curated.exists():
            return curated, None, "curated_10door_reference"
    candidates = []
    for verify_csv in GENERATED_MODEL_DIR.glob(f"**/locker_16029_{door_count}door_rule_driven_verify.csv"):
        if f"{door_count}door_W" in verify_csv.name:
            continue
        candidates.append(verify_csv.parent)
    if not candidates:
        return None, None, "not_found"
    candidates.sort(key=lambda path: (evidence_score(path, door_count), path.stat().st_mtime), reverse=True)
    return candidates[0], None, "best_verify_csv_evidence"


def find_output_dir(door_count: int) -> tuple[Path | None, str | None, str]:
    output_dir, task_id, source = find_output_dir_from_tasks(door_count)
    if output_dir:
        return output_dir, task_id, source
    return fallback_output_dir(door_count)


def find_first(output_dir: Path, patterns: list[str]) -> Path | None:
    for pattern in patterns:
        matches = sorted(output_dir.glob(pattern), key=lambda path: path.stat().st_mtime, reverse=True)
        if matches:
            return matches[0]
    return None


def read_verify_counts(verify_csv: Path) -> tuple[Counter[str], int, float | None]:
    counts: Counter[str] = Counter()
    x_values: list[float] = []
    row_count = 0
    with verify_csv.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            row_count += 1
            counts[row.get("type", "")] += 1
            for key in ("x_min", "x_max"):
                value = parse_float(row.get(key))
                if value is not None:
                    x_values.append(value)
    bbox_x_len = round(max(x_values) - min(x_values), 3) if x_values else None
    return counts, row_count, bbox_x_len


def first_match(text: str, pattern: str) -> str | None:
    match = re.search(pattern, text, flags=re.MULTILINE)
    return match.group(1).strip() if match else None


def read_report_stats(report_md: Path | None) -> dict[str, Any]:
    if not report_md or not report_md.exists():
        return {}
    text = report_md.read_text(encoding="utf-8-sig", errors="replace")
    valid_solids = first_match(text, r"^- Valid solid objects:\s*([0-9]+)")
    door_height = first_match(text, r"^- Door height:\s*([0-9.]+)")
    return {
        "valid_solid_objects": int(valid_solids) if valid_solids else None,
        "door_height_mm": float(door_height) if door_height else None,
    }


def read_geometry_integrity(geometry_md: Path | None) -> dict[str, Any]:
    if not geometry_md or not geometry_md.exists():
        return {"status": "not_run", "path": str(geometry_md) if geometry_md else None}
    text = geometry_md.read_text(encoding="utf-8-sig", errors="replace")
    return {
        "status": first_match(text, r"^- status:\s*`?([^`\n]+)`?") or "unknown",
        "shape_objects": parse_float(first_match(text, r"^- shape_objects:\s*`?([^`\n]+)`?")),
        "invalid_shape_objects": parse_float(first_match(text, r"^- invalid_shape_objects:\s*`?([^`\n]+)`?")),
        "center_vertical_signature_failures": parse_float(
            first_match(text, r"^- center_vertical_signature_failures:\s*`?([^`\n]+)`?")
        ),
        "total_bbox_x_status": first_match(text, r"^- total_bbox_x_status:\s*`?([^`\n]+)`?") or "unknown",
        "total_bbox_x_len": parse_float(first_match(text, r"^- total_bbox_x_len:\s*`?([^`\n]+)`?")),
        "path": str(geometry_md),
    }


def read_structural_rule_audit(door_count: int) -> dict[str, Any]:
    if not STRUCTURAL_RULE_AUDIT_PATH.exists():
        return {"status": "not_run", "path": str(STRUCTURAL_RULE_AUDIT_PATH)}
    try:
        data = json.loads(STRUCTURAL_RULE_AUDIT_PATH.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {"status": "invalid_json", "path": str(STRUCTURAL_RULE_AUDIT_PATH)}
    for variant in data.get("variants", []):
        if isinstance(variant, dict) and variant.get("door_count") == door_count:
            metrics = variant.get("metrics", {}) if isinstance(variant.get("metrics"), dict) else {}
            return {
                "status": variant.get("status", "unknown"),
                "path": str(STRUCTURAL_RULE_AUDIT_PATH),
                "markdown": str(STRUCTURAL_RULE_AUDIT_MARKDOWN_PATH),
                "rows_per_column": variant.get("rows_per_column"),
                "failed_checks": len(variant.get("failed_checks", [])),
                "door_width_mm": metrics.get("door_width_mm"),
                "door_height_mm": metrics.get("door_height_mm"),
                "door_pitch_mm": metrics.get("door_pitch_mm"),
                "door_y_span": metrics.get("door_y_span"),
                "lock_center_x_abs_mm": metrics.get("lock_center_x_abs_mm"),
                "hinge_axis_x_abs_mm": metrics.get("hinge_axis_x_abs_mm"),
            }
    return {"status": "missing_variant", "path": str(STRUCTURAL_RULE_AUDIT_PATH)}


def read_step_geometry_check(output_dir: Path) -> dict[str, Any]:
    check_json = output_dir / "step_geometry_check" / "freecad_geometry_check.json"
    if not check_json.exists():
        return {"status": "not_run", "path": str(check_json)}
    try:
        data = json.loads(check_json.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {"status": "invalid_json", "path": str(check_json)}
    bbox = data.get("assembly_bbox_mm", {}) if isinstance(data.get("assembly_bbox_mm"), dict) else {}
    return {
        "status": data.get("quality_status", "unknown"),
        "path": str(check_json),
        "shape_object_count": data.get("shape_object_count"),
        "invalid_shape_count": data.get("invalid_shape_count"),
        "solid_count": data.get("solid_count"),
        "bbox_x_len": bbox.get("size_x"),
        "bbox_y_len": bbox.get("size_y"),
        "bbox_z_len": bbox.get("size_z"),
    }


def build_count_checks(expected_counts: dict[str, Any], actual_counts: Counter[str]) -> list[dict[str, Any]]:
    checks: list[dict[str, Any]] = []
    for gate_name, verify_type in COUNT_TYPE_BY_GATE.items():
        expected = expected_counts.get(gate_name)
        actual = actual_counts.get(verify_type, 0)
        checks.append(
            {
                "gate": gate_name,
                "verify_type": verify_type,
                "expected": expected,
                "actual": actual,
                "ok": actual == expected,
            }
        )
    expected_center_vertical = expected_counts.get("cabinet_vertical_dividers", 0) + expected_counts.get("door_frame_vertical_dividers", 0)
    actual_center_vertical = actual_counts.get("fixed_center_vertical_structure", 0)
    checks.append(
        {
            "gate": "fixed_center_vertical_structure",
            "verify_type": "fixed_center_vertical_structure",
            "expected": expected_center_vertical,
            "actual": actual_center_vertical,
            "ok": actual_center_vertical == expected_center_vertical,
        }
    )
    return checks


def build_variant_quality(door_count: int, variant: dict[str, Any]) -> dict[str, Any]:
    output_dir, task_id, source = find_output_dir(door_count)
    if output_dir is None:
        return {
            "door_count": door_count,
            "status": "MISSING_OUTPUT",
            "source": source,
            "task_id": task_id,
            "output_dir": None,
            "checks": [],
            "notes": ["No completed FreeCAD output was found for this door count and 1000mm width."],
        }

    verify_csv = find_first(output_dir, [f"locker_16029_{door_count}door_rule_driven_verify.csv"])
    report_md = find_first(output_dir, [f"locker_16029_{door_count}door_rule_driven_report.md"])
    fcstd = find_first(output_dir, [f"locker_16029_{door_count}door_rule_driven.FCStd"])
    step = find_first(output_dir, [f"locker_16029_{door_count}door_rule_driven.step"])
    geometry_md = find_first(output_dir, [f"locker_16029_{door_count}door_rule_driven_geometry_integrity.md"])

    expected_counts = variant.get("expected_component_counts", {})
    actual_counts: Counter[str] = Counter()
    verify_rows = 0
    verify_bbox_x_len = None
    if verify_csv:
        actual_counts, verify_rows, verify_bbox_x_len = read_verify_counts(verify_csv)
    count_checks = build_count_checks(expected_counts, actual_counts)
    bbox_check = {
        "gate": "verify_bbox_x_len",
        "expected": EXPECTED_CABINET_WIDTH_MM,
        "actual": verify_bbox_x_len,
        "ok": verify_bbox_x_len is not None and abs(verify_bbox_x_len - EXPECTED_CABINET_WIDTH_MM) <= 0.03,
    }
    geometry = read_geometry_integrity(geometry_md)
    step_geometry = read_step_geometry_check(output_dir)
    structural_rule_audit = read_structural_rule_audit(door_count)
    structural_rule_check = {
        "gate": "structural_rule_audit",
        "expected": "PASS for door grid, lock relation, shelf/frame offsets, L/R symmetry, and bbox X",
        "actual": structural_rule_audit,
        "ok": (structural_rule_audit.get("status") == "PASS" and structural_rule_audit.get("failed_checks") == 0)
        if structural_rule_audit.get("status") not in {"not_run"}
        else None,
    }
    step_geometry_check = {
        "gate": "step_geometry_check",
        "expected": "geometry_check_pass with invalid_shape_count=0 and bbox_x_len=1000",
        "actual": step_geometry,
        "ok": (
            step_geometry.get("status") == "geometry_check_pass"
            and step_geometry.get("invalid_shape_count") == 0
            and abs(float(step_geometry.get("bbox_x_len") or 0) - EXPECTED_CABINET_WIDTH_MM) <= 0.03
        )
        if step_geometry.get("status") != "not_run"
        else None,
    }
    geometry_check = {
        "gate": "fcstd_geometry_integrity",
        "expected": "PASS with invalid_shape_objects=0 and center_vertical_signature_failures=0",
        "actual": geometry,
        "ok": (
            geometry.get("status") == "PASS"
            and geometry.get("invalid_shape_objects") == 0
            and geometry.get("center_vertical_signature_failures") == 0
        )
        if geometry.get("status") != "not_run"
        else None,
    }
    checks = [bbox_check, *count_checks, structural_rule_check, step_geometry_check, geometry_check]
    failed = [check for check in checks if check.get("ok") is False]
    pending = [check for check in checks if check.get("ok") is None]
    if failed:
        status = "FAIL"
    elif geometry_check["ok"] is True:
        status = "PASS_READY_FOR_ENGINEERING_REVIEW"
    elif step_geometry_check["ok"] is True:
        status = "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT"
    elif pending:
        status = "PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT"
    else:
        status = "PASS_READY_FOR_ENGINEERING_REVIEW"

    return {
        "door_count": door_count,
        "status": status,
        "source": source,
        "task_id": task_id,
        "output_dir": str(output_dir),
        "verify_csv": str(verify_csv) if verify_csv else None,
        "report_md": str(report_md) if report_md else None,
        "fcstd": str(fcstd) if fcstd else None,
        "step": str(step) if step else None,
        "fcstd_mb": file_size_mb(fcstd),
        "step_mb": file_size_mb(step),
        "verify_rows": verify_rows,
        "verify_bbox_x_len": verify_bbox_x_len,
        "expected_counts": expected_counts,
        "actual_counts": dict(sorted(actual_counts.items())),
        "report_stats": read_report_stats(report_md),
        "structural_rule_audit": structural_rule_audit,
        "step_geometry_check": step_geometry,
        "geometry_integrity": geometry,
        "checks": checks,
        "notes": status_notes(status),
    }


def status_notes(status: str) -> list[str]:
    if status == "PASS_READY_FOR_ENGINEERING_REVIEW":
        return ["All available rule-count and geometry-integrity gates passed."]
    if status == "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT":
        return ["Rule counts and STEP geometry passed; FCStd geometry integrity audit is still required."]
    if status == "PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT":
        return ["Rule counts passed; STEP/FCStd geometry integrity evidence is still required."]
    if status == "MISSING_OUTPUT":
        return ["No completed FreeCAD output was found."]
    return ["At least one quality gate failed."]


def build_payload() -> dict[str, Any]:
    rule_packet = read_json(RULE_PACKET_PATH)
    variants = variant_by_door_count(rule_packet)
    rows = [build_variant_quality(door_count, variants.get(door_count, {})) for door_count in TARGET_DOOR_COUNTS]
    return {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "scope": "1000W x 1917H x 550D FreeCAD engineering-reference variants",
        "source_rule_packet": str(RULE_PACKET_PATH),
        "summary": {
            "target_door_counts": TARGET_DOOR_COUNTS,
            "pass_ready_count": sum(1 for row in rows if row["status"] == "PASS_READY_FOR_ENGINEERING_REVIEW"),
            "pass_step_geometry_needs_fcstd_audit_count": sum(
                1 for row in rows if row["status"] == "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT"
            ),
            "pass_rule_counts_needs_fcstd_audit_count": sum(
                1
                for row in rows
                if row["status"] in {"PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT", "PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT"}
            ),
            "fail_count": sum(1 for row in rows if row["status"] == "FAIL"),
            "missing_output_count": sum(1 for row in rows if row["status"] == "MISSING_OUTPUT"),
        },
        "variants": rows,
        "output_paths": {
            "json": str(OUTPUT_JSON_PATH),
            "markdown": str(OUTPUT_MARKDOWN_PATH),
            "csv": str(OUTPUT_CSV_PATH),
            "structural_rule_audit_json": str(STRUCTURAL_RULE_AUDIT_PATH),
            "structural_rule_audit_markdown": str(STRUCTURAL_RULE_AUDIT_MARKDOWN_PATH),
        },
        "notes": [
            "This matrix gates engineering-reference model quality; it is not a production drawing release.",
            "structural_rule_audit PASS verifies door grid, lock relation, shelf/frame offsets, L/R symmetry, and bbox X from verify.csv evidence.",
            "PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT means verify.csv counts and exported STEP geometry pass, but FCStd shape integrity has not yet passed.",
            "PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT means verify.csv counts and bbox pass, but FCStd shape integrity has not yet passed.",
        ],
    }


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 10/12/14 门生成质量矩阵",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Scope: `{payload['scope']}`",
        f"- Rule packet: `{payload['source_rule_packet']}`",
        "",
        "| 门数 | 状态 | bbox X | 门模块 | 锁孔 | 锁钩 | 层板 | 门框横隔板 | 结构规则 | STEP 几何 | FCStd 完整性 | 输出目录 |",
        "| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- |",
    ]
    for row in payload["variants"]:
        checks = {check["gate"]: check for check in row.get("checks", [])}
        geometry = row.get("geometry_integrity", {})
        step_geometry = row.get("step_geometry_check", {})
        structural = row.get("structural_rule_audit", {})
        lines.append(
            "| "
            + " | ".join(
                [
                    str(row["door_count"]),
                    row["status"],
                    str(row.get("verify_bbox_x_len") or "-"),
                    count_display(checks.get("door_modules")),
                    count_display(checks.get("lock_holes")),
                    count_display(checks.get("lock_hooks")),
                    count_display(checks.get("shelves")),
                    count_display(checks.get("door_frame_horizontal_dividers")),
                    structural.get("status", "-"),
                    step_geometry.get("status", "-"),
                    geometry.get("status", "-"),
                    f"`{row.get('output_dir') or ''}`",
                ]
            )
            + " |"
        )
    lines.extend(["", "## 说明", ""])
    for note in payload["notes"]:
        lines.append(f"- {note}")
    lines.append("")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def count_display(check: dict[str, Any] | None) -> str:
    if not check:
        return "-"
    mark = "PASS" if check.get("ok") else "FAIL"
    return f"{check.get('actual')}/{check.get('expected')} {mark}"


def write_csv(payload: dict[str, Any]) -> None:
    OUTPUT_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "door_count",
                "status",
                "source",
                "task_id",
                "verify_bbox_x_len",
                "verify_rows",
                "fcstd_mb",
                "step_mb",
                "step_geometry_status",
                "step_invalid_shape_count",
                "step_bbox_x_len",
                "structural_rule_status",
                "structural_rule_failed_checks",
                "structural_rule_door_width_mm",
                "structural_rule_door_height_mm",
                "structural_rule_door_pitch_mm",
                "geometry_status",
                "invalid_shape_objects",
                "center_vertical_signature_failures",
                "output_dir",
            ],
        )
        writer.writeheader()
        for row in payload["variants"]:
            geometry = row.get("geometry_integrity", {})
            step_geometry = row.get("step_geometry_check", {})
            structural = row.get("structural_rule_audit", {})
            writer.writerow(
                {
                    "door_count": row.get("door_count"),
                    "status": row.get("status"),
                    "source": row.get("source"),
                    "task_id": row.get("task_id") or "",
                    "verify_bbox_x_len": row.get("verify_bbox_x_len"),
                    "verify_rows": row.get("verify_rows"),
                    "fcstd_mb": row.get("fcstd_mb"),
                    "step_mb": row.get("step_mb"),
                    "step_geometry_status": step_geometry.get("status"),
                    "step_invalid_shape_count": step_geometry.get("invalid_shape_count"),
                    "step_bbox_x_len": step_geometry.get("bbox_x_len"),
                    "structural_rule_status": structural.get("status"),
                    "structural_rule_failed_checks": structural.get("failed_checks"),
                    "structural_rule_door_width_mm": structural.get("door_width_mm"),
                    "structural_rule_door_height_mm": structural.get("door_height_mm"),
                    "structural_rule_door_pitch_mm": structural.get("door_pitch_mm"),
                    "geometry_status": geometry.get("status"),
                    "invalid_shape_objects": geometry.get("invalid_shape_objects"),
                    "center_vertical_signature_failures": geometry.get("center_vertical_signature_failures"),
                    "output_dir": row.get("output_dir"),
                }
            )


def main() -> None:
    payload = build_payload()
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(payload)
    write_csv(payload)
    print(
        json.dumps(
            {
                "status": "ok",
                "json": str(OUTPUT_JSON_PATH),
                "variants": len(payload["variants"]),
                "summary": payload["summary"],
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
