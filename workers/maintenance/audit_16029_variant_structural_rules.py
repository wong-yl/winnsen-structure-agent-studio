from __future__ import annotations

import csv
import json
import os
from collections import defaultdict
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
QUALITY_MATRIX_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_JSON", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.json")
)
OUTPUT_JSON_PATH = Path(
    os.getenv("STUDIO_16029_STRUCTURAL_RULE_AUDIT_JSON", ROOT_DIR / "data" / "locker_16029_structural_rule_audit.json")
)
OUTPUT_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_STRUCTURAL_RULE_AUDIT_MD", ROOT_DIR / "data" / "locker_16029_structural_rule_audit.md")
)
OUTPUT_CSV_PATH = Path(
    os.getenv("STUDIO_16029_STRUCTURAL_RULE_AUDIT_CSV", ROOT_DIR / "data" / "locker_16029_structural_rule_audit.csv")
)

EXPECTED_CABINET_WIDTH_MM = 1000.0
EXPECTED_STEP_BBOX_Y_MM = 1983.0
EXPECTED_STEP_BBOX_Z_MM = 552.0
EXPECTED_DOOR_WIDTH_MM = 437.0
EXPECTED_LOCK_CENTER_ABS_X_MM = 55.0
EXPECTED_HINGE_AXIS_ABS_X_MM = 467.0
DOOR_AREA_BOTTOM_MM = 30.0
DOOR_AREA_TOP_MM = 1857.0
GRID_TOP_GAP_MM = 2.0
GRID_BOTTOM_GAP_MM = 2.0
VISUAL_GAP_MM = 7.0
SHELF_FROM_LOWER_DOOR_Y_MAX_MM = 2.0
FRAME_FROM_LOWER_DOOR_Y_MAX_MM = -10.0
TOL_MM = 0.05


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_float(value: str | None) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def parse_int(value: str | None) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(float(value))
    except ValueError:
        return None


def rounded(value: float | None) -> float | None:
    return round(value, 3) if value is not None else None


def expected_door_height(rows_per_column: int) -> float:
    door_area_height = DOOR_AREA_TOP_MM - DOOR_AREA_BOTTOM_MM
    return (
        door_area_height
        - GRID_TOP_GAP_MM
        - GRID_BOTTOM_GAP_MM
        - (rows_per_column - 1) * VISUAL_GAP_MM
    ) / rows_per_column


def expected_door_pitch(rows_per_column: int) -> float:
    return expected_door_height(rows_per_column) + VISUAL_GAP_MM


def load_rows(verify_csv: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    with verify_csv.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for raw in reader:
            row = dict(raw)
            for key in (
                "x_min",
                "x_max",
                "y_min",
                "y_max",
                "z_min",
                "z_max",
                "x_center",
                "y_center",
                "z_center",
                "door_height",
                "door_width",
                "flat_width",
                "flat_height",
                "lock_hole_center_x",
                "lock_hole_center_y",
                "hinge_axis_x",
                "hinge_axis_y",
            ):
                row[key] = parse_float(raw.get(key))
            row["row"] = parse_int(raw.get("row"))
            rows.append(row)
    return rows


def add_check(
    checks: list[dict[str, Any]],
    name: str,
    ok: bool,
    actual: Any,
    expected: Any,
    severity: str = "error",
    detail: str = "",
) -> None:
    checks.append(
        {
            "name": name,
            "ok": ok,
            "actual": actual,
            "expected": expected,
            "severity": severity,
            "detail": detail,
        }
    )


def group_by_column_and_row(rows: list[dict[str, Any]], row_type: str) -> dict[tuple[str, int], dict[str, Any]]:
    grouped: dict[tuple[str, int], dict[str, Any]] = {}
    for row in rows:
        if row.get("type") != row_type:
            continue
        column = str(row.get("column") or "")
        index = row.get("row")
        if column and isinstance(index, int):
            grouped[(column, index)] = row
    return grouped


def row_sequence(rows: dict[tuple[str, int], dict[str, Any]], column: str) -> list[dict[str, Any]]:
    return [rows[(column, index)] for index in sorted(index for col, index in rows if col == column)]


def y_center(row: dict[str, Any]) -> float | None:
    y_min = row.get("y_min")
    y_max = row.get("y_max")
    if y_min is None or y_max is None:
        return row.get("y_center")
    return (y_min + y_max) / 2


def compare_row_alignment(left_rows: list[dict[str, Any]], right_rows: list[dict[str, Any]]) -> float | None:
    deltas: list[float] = []
    for left, right in zip(left_rows, right_rows):
        for key in ("y_min", "y_max"):
            if left.get(key) is not None and right.get(key) is not None:
                deltas.append(abs(left[key] - right[key]))
    return max(deltas) if deltas else None


def y_lengths(rows: list[dict[str, Any]]) -> list[float]:
    lengths = []
    for row in rows:
        y_min = row.get("y_min")
        y_max = row.get("y_max")
        if y_min is not None and y_max is not None:
            lengths.append(y_max - y_min)
    return lengths


def y_gaps(rows: list[dict[str, Any]]) -> list[float]:
    gaps = []
    for lower, upper in zip(rows, rows[1:]):
        if lower.get("y_max") is not None and upper.get("y_min") is not None:
            gaps.append(upper["y_min"] - lower["y_max"])
    return gaps


def summarize_numbers(values: list[float]) -> dict[str, float | None]:
    if not values:
        return {"min": None, "max": None, "spread": None}
    return {
        "min": rounded(min(values)),
        "max": rounded(max(values)),
        "spread": rounded(max(values) - min(values)),
    }


def step_geometry_value(variant: dict[str, Any], key: str) -> float | None:
    check = variant.get("step_geometry_check")
    if not isinstance(check, dict):
        return None
    value = check.get(key)
    try:
        return float(value) if value is not None else None
    except (TypeError, ValueError):
        return None


def audit_variant(variant: dict[str, Any]) -> dict[str, Any]:
    door_count = int(variant["door_count"])
    rows_per_column = door_count // 2
    verify_csv = Path(str(variant.get("verify_csv") or ""))
    checks: list[dict[str, Any]] = []
    if not verify_csv.exists():
        add_check(checks, "verify_csv_exists", False, str(verify_csv), "existing verify csv")
        return build_variant_payload(door_count, rows_per_column, verify_csv, [], checks, {})

    rows = load_rows(verify_csv)
    by_type: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        by_type[str(row.get("type") or "")].append(row)

    doors = group_by_column_and_row(rows, "door_module")
    lock_holes = group_by_column_and_row(rows, "lock_hole_reference")
    lock_hooks = group_by_column_and_row(rows, "lock_hook_reference")
    shelves = group_by_column_and_row(rows, "shelf_weld")
    frames = group_by_column_and_row(rows, "door_frame_horizontal")
    left_doors = row_sequence(doors, "L")
    right_doors = row_sequence(doors, "R")

    add_check(checks, "door_module_count", len(by_type["door_module"]) == door_count, len(by_type["door_module"]), door_count)
    add_check(checks, "left_column_row_count", len(left_doors) == rows_per_column, len(left_doors), rows_per_column)
    add_check(checks, "right_column_row_count", len(right_doors) == rows_per_column, len(right_doors), rows_per_column)

    expected_height = expected_door_height(rows_per_column)
    expected_pitch = expected_door_pitch(rows_per_column)
    door_lengths = y_lengths(left_doors + right_doors)
    door_gaps = y_gaps(left_doors) + y_gaps(right_doors)
    add_check(
        checks,
        "door_height_formula",
        bool(door_lengths) and max(abs(length - expected_height) for length in door_lengths) <= TOL_MM,
        summarize_numbers(door_lengths),
        round(expected_height, 3),
    )
    add_check(
        checks,
        "door_gap_formula",
        bool(door_gaps) and max(abs(gap - VISUAL_GAP_MM) for gap in door_gaps) <= TOL_MM,
        summarize_numbers(door_gaps),
        VISUAL_GAP_MM,
    )
    add_check(
        checks,
        "door_pitch_formula",
        bool(door_lengths)
        and bool(door_gaps)
        and max(abs((length - expected_height)) for length in door_lengths) <= TOL_MM
        and max(abs((expected_height + gap) - expected_pitch) for gap in door_gaps) <= TOL_MM,
        {"door_height": summarize_numbers(door_lengths), "gap": summarize_numbers(door_gaps)},
        round(expected_pitch, 3),
        detail="door pitch = door height + visual gap",
    )
    if left_doors and right_doors:
        first_min = min(row["y_min"] for row in left_doors + right_doors if row.get("y_min") is not None)
        last_max = max(row["y_max"] for row in left_doors + right_doors if row.get("y_max") is not None)
        add_check(
            checks,
            "door_stack_bottom_top",
            abs(first_min - (DOOR_AREA_BOTTOM_MM + GRID_BOTTOM_GAP_MM)) <= TOL_MM
            and abs(last_max - (DOOR_AREA_TOP_MM - GRID_TOP_GAP_MM)) <= TOL_MM,
            {"first_y_min": rounded(first_min), "last_y_max": rounded(last_max)},
            {"first_y_min": DOOR_AREA_BOTTOM_MM + GRID_BOTTOM_GAP_MM, "last_y_max": DOOR_AREA_TOP_MM - GRID_TOP_GAP_MM},
        )

    row_alignment_delta = compare_row_alignment(left_doors, right_doors)
    add_check(
        checks,
        "left_right_door_row_alignment",
        row_alignment_delta is not None and row_alignment_delta <= TOL_MM,
        rounded(row_alignment_delta),
        f"<= {TOL_MM}",
    )

    door_width_errors = []
    lock_hinge_side_errors = []
    for key, door in doors.items():
        column, index = key
        expected_lock_x = -EXPECTED_LOCK_CENTER_ABS_X_MM if column == "L" else EXPECTED_LOCK_CENTER_ABS_X_MM
        expected_hinge_x = -EXPECTED_HINGE_AXIS_ABS_X_MM if column == "L" else EXPECTED_HINGE_AXIS_ABS_X_MM
        door_width = door.get("door_width")
        lock_x = door.get("lock_hole_center_x")
        hinge_x = door.get("hinge_axis_x")
        if door_width is None or abs(door_width - EXPECTED_DOOR_WIDTH_MM) > TOL_MM:
            door_width_errors.append(
                {
                    "column": column,
                    "row": index,
                    "door_width": rounded(door_width),
                }
            )
        if (
            lock_x is None
            or hinge_x is None
            or abs(lock_x - expected_lock_x) > TOL_MM
            or abs(hinge_x - expected_hinge_x) > TOL_MM
        ):
            lock_hinge_side_errors.append(
                {
                    "column": column,
                    "row": index,
                    "lock_hole_center_x": rounded(lock_x),
                    "expected_lock_hole_center_x": expected_lock_x,
                    "hinge_axis_x": rounded(hinge_x),
                    "expected_hinge_axis_x": expected_hinge_x,
                }
            )
    add_check(
        checks,
        "door_width_reference",
        not door_width_errors,
        door_width_errors or summarize_numbers([row["door_width"] for row in doors.values() if row.get("door_width") is not None]),
        EXPECTED_DOOR_WIDTH_MM,
        detail="guards against wrong door template width when changing row count",
    )
    add_check(
        checks,
        "door_lock_hinge_side_relation",
        not lock_hinge_side_errors,
        lock_hinge_side_errors or {
            "L": {"lock_hole_center_x": -EXPECTED_LOCK_CENTER_ABS_X_MM, "hinge_axis_x": -EXPECTED_HINGE_AXIS_ABS_X_MM},
            "R": {"lock_hole_center_x": EXPECTED_LOCK_CENTER_ABS_X_MM, "hinge_axis_x": EXPECTED_HINGE_AXIS_ABS_X_MM},
        },
        "left negative, right positive",
        detail="guards against mirrored or swapped door modules",
    )

    add_check(checks, "lock_hole_count", len(by_type["lock_hole_reference"]) == door_count, len(by_type["lock_hole_reference"]), door_count)
    add_check(checks, "lock_hook_count", len(by_type["lock_hook_reference"]) == door_count, len(by_type["lock_hook_reference"]), door_count)
    lock_relation_errors = []
    for key, door in doors.items():
        hole = lock_holes.get(key)
        hook = lock_hooks.get(key)
        door_center_y = y_center(door)
        hole_center_y = y_center(hole) if hole else None
        hook_center_y = y_center(hook) if hook else None
        column, index = key
        if not hole or not hook or door_center_y is None or hole_center_y is None or hook_center_y is None:
            lock_relation_errors.append({"column": column, "row": index, "reason": "missing lock evidence"})
            continue
        expected_sign_ok = (column == "L" and hole["x_max"] is not None and hole["x_max"] < 0) or (
            column == "R" and hole["x_min"] is not None and hole["x_min"] > 0
        )
        y_ok = abs(hole_center_y - door_center_y) <= TOL_MM and abs(hook_center_y - door_center_y) <= 0.1
        if not expected_sign_ok or not y_ok:
            lock_relation_errors.append(
                {
                    "column": column,
                    "row": index,
                    "door_center_y": rounded(door_center_y),
                    "hole_center_y": rounded(hole_center_y),
                    "hook_center_y": rounded(hook_center_y),
                    "hole_x_min": rounded(hole.get("x_min")),
                    "hole_x_max": rounded(hole.get("x_max")),
                }
            )
    add_check(checks, "lock_position_relation", not lock_relation_errors, lock_relation_errors or "all lock centers match door centers and side sign", "per-door lock references")

    expected_internal_rows = rows_per_column - 1
    add_check(checks, "shelf_count", len(by_type["shelf_weld"]) == expected_internal_rows * 2, len(by_type["shelf_weld"]), expected_internal_rows * 2)
    add_check(
        checks,
        "door_frame_horizontal_count",
        len(by_type["door_frame_horizontal"]) == expected_internal_rows * 2,
        len(by_type["door_frame_horizontal"]),
        expected_internal_rows * 2,
    )

    driven_errors = []
    for column in ("L", "R"):
        for index in range(1, rows_per_column):
            door = doors.get((column, index))
            shelf = shelves.get((column, index))
            frame = frames.get((column, index))
            if not door or not shelf or not frame or door.get("y_max") is None:
                driven_errors.append({"column": column, "row": index, "reason": "missing driven part"})
                continue
            shelf_delta = shelf["y_min"] - door["y_max"] if shelf.get("y_min") is not None else None
            frame_delta = frame["y_min"] - door["y_max"] if frame.get("y_min") is not None else None
            if (
                shelf_delta is None
                or frame_delta is None
                or abs(shelf_delta - SHELF_FROM_LOWER_DOOR_Y_MAX_MM) > TOL_MM
                or abs(frame_delta - FRAME_FROM_LOWER_DOOR_Y_MAX_MM) > TOL_MM
            ):
                driven_errors.append(
                    {
                        "column": column,
                        "row": index,
                        "shelf_delta": rounded(shelf_delta),
                        "frame_delta": rounded(frame_delta),
                    }
                )
    add_check(
        checks,
        "shelf_and_frame_driven_from_door_boundary",
        not driven_errors,
        driven_errors or "all shelf/frame rows follow door boundary offsets",
        {"shelf_y_min": "+2mm from lower door y_max", "frame_y_min": "-10mm from lower door y_max"},
    )

    bbox_x = variant.get("verify_bbox_x_len")
    add_check(
        checks,
        "overall_bbox_x",
        bbox_x is not None and abs(float(bbox_x) - EXPECTED_CABINET_WIDTH_MM) <= TOL_MM,
        bbox_x,
        EXPECTED_CABINET_WIDTH_MM,
    )
    step_bbox_x = step_geometry_value(variant, "bbox_x_len")
    step_bbox_y = step_geometry_value(variant, "bbox_y_len")
    step_bbox_z = step_geometry_value(variant, "bbox_z_len")
    step_invalid_shape_count = step_geometry_value(variant, "invalid_shape_count")
    add_check(
        checks,
        "step_bbox_xyz_envelope",
        step_bbox_x is not None
        and step_bbox_y is not None
        and step_bbox_z is not None
        and abs(step_bbox_x - EXPECTED_CABINET_WIDTH_MM) <= TOL_MM
        and abs(step_bbox_y - EXPECTED_STEP_BBOX_Y_MM) <= TOL_MM
        and abs(step_bbox_z - EXPECTED_STEP_BBOX_Z_MM) <= TOL_MM,
        {"x": rounded(step_bbox_x), "y": rounded(step_bbox_y), "z": rounded(step_bbox_z)},
        {"x": EXPECTED_CABINET_WIDTH_MM, "y": EXPECTED_STEP_BBOX_Y_MM, "z": EXPECTED_STEP_BBOX_Z_MM},
        detail="guards against exploded or out-of-envelope generated assemblies",
    )
    add_check(
        checks,
        "step_invalid_shape_count",
        step_invalid_shape_count == 0,
        rounded(step_invalid_shape_count),
        0,
        detail="guards against visually present but invalid STEP solids",
    )

    metrics = {
        "door_height_mm": rounded(expected_height),
        "door_width_mm": EXPECTED_DOOR_WIDTH_MM,
        "door_pitch_mm": rounded(expected_pitch),
        "door_gap_mm": VISUAL_GAP_MM,
        "door_rows_per_column": rows_per_column,
        "door_y_span": {
            "bottom": rounded(min((row["y_min"] for row in left_doors + right_doors if row.get("y_min") is not None), default=None)),
            "top": rounded(max((row["y_max"] for row in left_doors + right_doors if row.get("y_max") is not None), default=None)),
        },
        "lock_center_x_abs_mm": summarize_numbers([abs(row["lock_hole_center_x"]) for row in doors.values() if row.get("lock_hole_center_x") is not None]),
        "hinge_axis_x_abs_mm": summarize_numbers([abs(row["hinge_axis_x"]) for row in doors.values() if row.get("hinge_axis_x") is not None]),
        "step_bbox_mm": {"x": rounded(step_bbox_x), "y": rounded(step_bbox_y), "z": rounded(step_bbox_z)},
        "step_invalid_shape_count": rounded(step_invalid_shape_count),
        "door_source_levels": sorted({str(row.get("source_level") or "") for row in by_type["door_module"] if row.get("source_level")}),
        "door_template_names": sorted({str(row.get("template_name") or "") for row in by_type["door_module"] if row.get("template_name")}),
    }
    return build_variant_payload(door_count, rows_per_column, verify_csv, rows, checks, metrics)


def build_variant_payload(
    door_count: int,
    rows_per_column: int,
    verify_csv: Path,
    rows: list[dict[str, Any]],
    checks: list[dict[str, Any]],
    metrics: dict[str, Any],
) -> dict[str, Any]:
    failed = [check for check in checks if not check["ok"] and check["severity"] == "error"]
    warnings = [check for check in checks if not check["ok"] and check["severity"] == "warning"]
    return {
        "door_count": door_count,
        "rows_per_column": rows_per_column,
        "status": "PASS" if not failed else "FAIL",
        "verify_csv": str(verify_csv),
        "verify_rows": len(rows),
        "metrics": metrics,
        "checks": checks,
        "failed_checks": failed,
        "warning_checks": warnings,
    }


def build_payload() -> dict[str, Any]:
    matrix = read_json(QUALITY_MATRIX_PATH)
    variants = [audit_variant(variant) for variant in matrix.get("variants", [])]
    return {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "scope": "1000W x 1917H x 550D 10/12/14 structural rule audit from verify.csv",
        "source_quality_matrix": str(QUALITY_MATRIX_PATH),
        "summary": {
            "variant_count": len(variants),
            "pass_count": sum(1 for variant in variants if variant["status"] == "PASS"),
            "fail_count": sum(1 for variant in variants if variant["status"] == "FAIL"),
        },
        "variants": variants,
        "output_paths": {
            "json": str(OUTPUT_JSON_PATH),
            "markdown": str(OUTPUT_MARKDOWN_PATH),
            "csv": str(OUTPUT_CSV_PATH),
        },
        "notes": [
            "This audit verifies layout rules from generated evidence rows; it does not release production drawings.",
            "Checks cover door grid, door width, lock/hinge side relation, shelf/frame boundary offsets, L/R symmetry, STEP bbox X/Y/Z envelope, invalid STEP shape count, and overall bbox X.",
        ],
    }


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 10/12/14 门结构规则审计",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Scope: `{payload['scope']}`",
        f"- Source quality matrix: `{payload['source_quality_matrix']}`",
        "",
        "| 门数 | 状态 | 每列门数 | 门宽 | 门高 | 门距 | 锁孔X | 铰链X | 门 Y 范围 | STEP bbox X/Y/Z | 失败项 | 门板来源 |",
        "| ---: | --- | ---: | ---: | ---: | ---: | --- | --- | --- | --- | ---: | --- |",
    ]
    for variant in payload["variants"]:
        metrics = variant.get("metrics", {})
        source_levels = ", ".join(metrics.get("door_source_levels") or [])
        step_bbox = metrics.get("step_bbox_mm") or {}
        lock_x = metrics.get("lock_center_x_abs_mm") or {}
        hinge_x = metrics.get("hinge_axis_x_abs_mm") or {}
        lines.append(
            "| "
            + " | ".join(
                [
                    str(variant["door_count"]),
                    variant["status"],
                    str(variant["rows_per_column"]),
                    str(metrics.get("door_width_mm", "-")),
                    str(metrics.get("door_height_mm", "-")),
                    str(metrics.get("door_pitch_mm", "-")),
                    f"±{lock_x.get('min', '-')}",
                    f"±{hinge_x.get('min', '-')}",
                    f"{(metrics.get('door_y_span') or {}).get('bottom', '-')}-{(metrics.get('door_y_span') or {}).get('top', '-')}",
                    f"{step_bbox.get('x', '-')}/{step_bbox.get('y', '-')}/{step_bbox.get('z', '-')}",
                    str(len(variant["failed_checks"])),
                    source_levels or "-",
                ]
            )
            + " |"
        )
    lines.extend(["", "## Check Details", ""])
    for variant in payload["variants"]:
        lines.extend(
            [
                f"### {variant['door_count']} 门",
                "",
                "| check | status | actual | expected |",
                "| --- | --- | --- | --- |",
            ]
        )
        for check in variant["checks"]:
            status = "PASS" if check["ok"] else "FAIL"
            actual = json.dumps(check["actual"], ensure_ascii=False) if isinstance(check["actual"], (dict, list)) else str(check["actual"])
            expected = (
                json.dumps(check["expected"], ensure_ascii=False) if isinstance(check["expected"], (dict, list)) else str(check["expected"])
            )
            lines.append(f"| {check['name']} | {status} | `{actual}` | `{expected}` |")
        lines.append("")
    for note in payload["notes"]:
        lines.append(f"- {note}")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_csv(payload: dict[str, Any]) -> None:
    OUTPUT_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["door_count", "status", "check", "ok", "actual", "expected", "severity", "detail"],
        )
        writer.writeheader()
        for variant in payload["variants"]:
            for check in variant["checks"]:
                writer.writerow(
                    {
                        "door_count": variant["door_count"],
                        "status": variant["status"],
                        "check": check["name"],
                        "ok": check["ok"],
                        "actual": json.dumps(check["actual"], ensure_ascii=False)
                        if isinstance(check["actual"], (dict, list))
                        else check["actual"],
                        "expected": json.dumps(check["expected"], ensure_ascii=False)
                        if isinstance(check["expected"], (dict, list))
                        else check["expected"],
                        "severity": check["severity"],
                        "detail": check["detail"],
                    }
                )


def main() -> None:
    payload = build_payload()
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(payload)
    write_csv(payload)
    print(json.dumps({"status": "ok", "summary": payload["summary"], "json": str(OUTPUT_JSON_PATH)}, ensure_ascii=False))
    if payload["summary"]["fail_count"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
