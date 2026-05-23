from __future__ import annotations

import csv
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any

from validate_native_16029_cabinet_skeleton_geometry import (
    DOOR_HEIGHT_BY_COUNT,
    EXPECTED_LEFT_DOOR_COLUMN_X_MM,
    EXPECTED_RIGHT_DOOR_COLUMN_X_MM,
    TOL_COUNT_PITCH_MM,
    TOL_PAIR_MM,
    TOL_PLACEMENT_X_MM,
    VISUAL_GAP_MM,
    SHELF_AND_CROSSBAR_FROM_LOWER_DOOR_Y_MAX_MM,
    boundary_offset_summary,
    center,
    cluster_centers,
    cluster_pitch_deltas,
    count_ok_cluster_pairs,
    count_rows,
    count_rows_where,
    is_crossbar,
    is_door_module_row,
    load_door_array_result,
    max_pair_y_delta,
    placement_column_rows,
    placement_pitch_deltas,
    placement_transform_summary,
    placement_x_error,
    right_column_rotation_ok,
    row_label,
    split_door_columns,
    valid_geometry,
)


ROOT_DIR = Path(__file__).resolve().parents[2]
DOOR_COUNT = int(os.getenv("STUDIO_16029_ENRICHED_DOOR_COUNT", "12"))
DEFAULT_OUT_DIR = ROOT_DIR / "workers" / "generated_models" / f"SW-NATIVE-16029-CABINET-ENRICHED-{DOOR_COUNT}DOOR-20260521"
OUT_DIR = Path(
    os.getenv(
        "STUDIO_16029_ENRICHED_DIR",
        os.getenv("STUDIO_16029_ENRICHED_12DOOR_DIR", str(DEFAULT_OUT_DIR)),
    )
)
MODE = os.getenv(
    "STUDIO_16029_ENRICHED_MODE",
    os.getenv("STUDIO_16029_ENRICHED_12DOOR_MODE", "identity"),
).strip().lower()
MATRIX_MODE = MODE == "matrix"
DEFAULT_STEM = (
    f"native_16029_{DOOR_COUNT}door_cabinet_enriched_v2"
    if MATRIX_MODE
    else f"native_16029_{DOOR_COUNT}door_cabinet_enriched_v1"
)
STEM = os.getenv("STUDIO_16029_ENRICHED_STEM", os.getenv("STUDIO_16029_ENRICHED_12DOOR_STEM", DEFAULT_STEM))

CANDIDATE_JSON_PATH = ROOT_DIR / "data" / "solidworks_16029_fixed_module_candidate_map.json"
RESULT_JSON_PATH = OUT_DIR / f"{STEM}_result.json"
BBOX_CSV_PATH = OUT_DIR / f"{STEM}_step_bbox.csv"
ASSEMBLY_PATH = OUT_DIR / f"{STEM}.SLDASM"
STEP_PATH = OUT_DIR / f"{STEM}.step"

if DOOR_COUNT == 12:
    DATA_PREFIX = "solidworks_16029_enriched_12door_matrix_validation" if MATRIX_MODE else "solidworks_16029_enriched_12door_validation"
else:
    DATA_PREFIX = (
        f"solidworks_16029_enriched_{DOOR_COUNT}door_matrix_validation"
        if MATRIX_MODE
        else f"solidworks_16029_enriched_{DOOR_COUNT}door_validation"
    )
LOCAL_PREFIX = (
    f"enriched_{DOOR_COUNT}door_matrix_validation" if MATRIX_MODE else f"enriched_{DOOR_COUNT}door_validation"
)
DATA_JSON_PATH = ROOT_DIR / "data" / f"{DATA_PREFIX}.json"
DATA_MD_PATH = ROOT_DIR / "data" / f"{DATA_PREFIX}.md"
DATA_CSV_PATH = ROOT_DIR / "data" / f"{DATA_PREFIX}.csv"
LOCAL_JSON_PATH = OUT_DIR / f"{LOCAL_PREFIX}.json"
LOCAL_MD_PATH = OUT_DIR / f"{LOCAL_PREFIX}.md"
LOCAL_CSV_PATH = OUT_DIR / f"{LOCAL_PREFIX}.csv"

EXPECTED_CABINET_WIDTH_MM = 1000.0
EXPECTED_CABINET_HEIGHT_TOP_MM = 1917.0
EXPECTED_CABINET_DEPTH_MM = 550.0
TOL_MM = 0.75


def now_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def parse_float(value: str | None) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def rounded(value: float | None) -> float | None:
    return round(value, 3) if value is not None else None


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def load_bbox_rows(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for raw in reader:
            row: dict[str, Any] = dict(raw)
            for key in (
                "x_min",
                "x_max",
                "x_len",
                "y_min",
                "y_max",
                "y_len",
                "z_min",
                "z_max",
                "z_len",
                "volume",
            ):
                row[key] = parse_float(raw.get(key))
            row["x_center"] = center(row.get("x_min"), row.get("x_max"))
            row["y_center"] = center(row.get("y_min"), row.get("y_max"))
            row["z_center"] = center(row.get("z_min"), row.get("z_max"))
            rows.append(row)
    return rows


def finite_row(row: dict[str, Any]) -> bool:
    values = [row.get(key) for key in ("x_min", "x_max", "y_min", "y_max", "z_min", "z_max")]
    return all(isinstance(value, float) and abs(value) < 1e20 for value in values)


def union_bbox(rows: list[dict[str, Any]]) -> dict[str, float | None]:
    valid = [row for row in rows if finite_row(row) and isinstance(row.get("volume"), float) and row["volume"] > 0]
    if not valid:
        return {}
    x_min = min(float(row["x_min"]) for row in valid)
    x_max = max(float(row["x_max"]) for row in valid)
    y_min = min(float(row["y_min"]) for row in valid)
    y_max = max(float(row["y_max"]) for row in valid)
    z_min = min(float(row["z_min"]) for row in valid)
    z_max = max(float(row["z_max"]) for row in valid)
    return {
        "x_min": rounded(x_min),
        "x_max": rounded(x_max),
        "x_len": rounded(x_max - x_min),
        "y_min": rounded(y_min),
        "y_max": rounded(y_max),
        "y_len": rounded(y_max - y_min),
        "z_min": rounded(z_min),
        "z_max": rounded(z_max),
        "z_len": rounded(z_max - z_min),
    }


def bbox_payload(row: dict[str, Any] | None) -> dict[str, float | None]:
    if not row:
        return {}
    return {
        "x_min": rounded(row.get("x_min")),
        "x_max": rounded(row.get("x_max")),
        "x_len": rounded(row.get("x_len")),
        "y_min": rounded(row.get("y_min")),
        "y_max": rounded(row.get("y_max")),
        "y_len": rounded(row.get("y_len")),
        "z_min": rounded(row.get("z_min")),
        "z_max": rounded(row.get("z_max")),
        "z_len": rounded(row.get("z_len")),
    }


def find_root_bbox_row(rows: list[dict[str, Any]]) -> dict[str, Any] | None:
    for row in rows:
        if row.get("type_id") == "App::Part" and str(row.get("label") or "") == STEM:
            return row
    return None


def bbox_match_error(expected: dict[str, Any], row: dict[str, Any]) -> float | None:
    errors: list[float] = []
    for key in ("x_min", "x_max", "y_min", "y_max", "z_min", "z_max"):
        expected_value = expected.get(key)
        row_value = row.get(key)
        if expected_value is None or row_value is None:
            return None
        errors.append(abs(float(expected_value) - float(row_value)))
    return max(errors) if errors else None


def find_bbox_match(rows: list[dict[str, Any]], expected: dict[str, Any]) -> dict[str, Any]:
    best_row: dict[str, Any] | None = None
    best_error: float | None = None
    for row in rows:
        if not finite_row(row):
            continue
        error = bbox_match_error(expected, row)
        if error is None:
            continue
        if best_error is None or error < best_error:
            best_error = error
            best_row = row
    return {
        "matched": best_error is not None and best_error <= TOL_MM,
        "max_error_mm": rounded(best_error),
        "label": best_row.get("label") if best_row else "",
        "type_id": best_row.get("type_id") if best_row else "",
    }


def add_check(checks: list[dict[str, Any]], name: str, ok: bool, actual: Any, expected: Any, severity: str = "error") -> None:
    checks.append(
        {
            "name": name,
            "ok": bool(ok),
            "actual": actual,
            "expected": expected,
            "severity": severity,
        }
    )


def build_payload() -> dict[str, Any]:
    candidate_map = load_json(CANDIDATE_JSON_PATH)
    recommended = [
        item
        for item in candidate_map["candidates"]
        if (
            item.get("recommended_for_matrix_enriched_model") is True
            if MATRIX_MODE
            else item.get("recommended_for_first_enriched_model") is True
            and item.get("placement_strategy") == "identity_transform_ready"
        )
    ]
    result = load_json(RESULT_JSON_PATH) if RESULT_JSON_PATH.exists() else {}
    rows = load_bbox_rows(BBOX_CSV_PATH) if BBOX_CSV_PATH.exists() else []
    checks: list[dict[str, Any]] = []

    add_check(checks, "assembly_exists", ASSEMBLY_PATH.exists(), str(ASSEMBLY_PATH), "native SolidWorks assembly")
    add_check(checks, "step_exists", STEP_PATH.exists(), str(STEP_PATH), "STEP export")
    add_check(checks, "bbox_csv_exists", BBOX_CSV_PATH.exists(), str(BBOX_CSV_PATH), "FreeCAD bbox CSV")
    add_check(checks, "builder_saved", result.get("saved") is True, result.get("saved"), True)
    add_check(
        checks,
        "placement_count",
        result.get("placement_count") == len(recommended) + 1,
        result.get("placement_count"),
        len(recommended) + 1,
    )

    components = result.get("components") or []
    failed_components = [
        item
        for item in components
        if item.get("exists") is not True or item.get("added") is not True or item.get("transform_applied") is not True
    ]
    add_check(checks, "all_components_added", not failed_components, failed_components, "no failed component additions")

    root_bbox_row = find_root_bbox_row(rows)
    combined_bbox = bbox_payload(root_bbox_row) or union_bbox(rows)
    add_check(
        checks,
        "cabinet_width_preserved",
        abs(float(combined_bbox.get("x_len") or 0) - EXPECTED_CABINET_WIDTH_MM) <= TOL_MM,
        combined_bbox.get("x_len"),
        EXPECTED_CABINET_WIDTH_MM,
    )
    add_check(
        checks,
        "cabinet_height_top_preserved",
        abs(float(combined_bbox.get("y_max") or 0) - EXPECTED_CABINET_HEIGHT_TOP_MM) <= TOL_MM,
        combined_bbox.get("y_max"),
        EXPECTED_CABINET_HEIGHT_TOP_MM,
    )
    add_check(
        checks,
        "cabinet_depth_preserved",
        abs(float(combined_bbox.get("z_len") or 0) - EXPECTED_CABINET_DEPTH_MM) <= TOL_MM,
        combined_bbox.get("z_len"),
        EXPECTED_CABINET_DEPTH_MM,
    )

    candidate_matches = []
    for candidate in recommended:
        match = find_bbox_match(rows, candidate["target_bbox_mm"])
        candidate_matches.append(
            {
                "role": candidate["role"],
                "component_path": candidate["component_path"],
                "target_bbox_mm": candidate["target_bbox_mm"],
                **match,
            }
        )
    add_check(
        checks,
        "recommended_candidate_bboxes_match",
        all(item["matched"] for item in candidate_matches),
        f"{sum(1 for item in candidate_matches if item['matched'])}/{len(candidate_matches)} matched",
        f"{len(recommended)} candidate bboxes match within {TOL_MM} mm",
    )

    door_height = DOOR_HEIGHT_BY_COUNT[DOOR_COUNT]
    expected_pitch = door_height + VISUAL_GAP_MM
    rows_per_column = DOOR_COUNT // 2
    expected_internal_levels = rows_per_column - 1
    valid_rows = [row for row in rows if valid_geometry(row)]
    door_rows = [row for row in valid_rows if is_door_module_row(row, DOOR_COUNT)]
    left_doors, right_doors = split_door_columns(door_rows)
    door_array_result = load_door_array_result(DOOR_COUNT)
    left_placements = placement_column_rows(door_array_result, "L")
    right_placements = placement_column_rows(door_array_result, "R")
    all_placements = left_placements + right_placements
    transform_summary = placement_transform_summary(all_placements)
    left_x_error = placement_x_error(left_placements, EXPECTED_LEFT_DOOR_COLUMN_X_MM)
    right_x_error = placement_x_error(right_placements, EXPECTED_RIGHT_DOOR_COLUMN_X_MM)
    left_pitch = placement_pitch_deltas(left_placements, expected_pitch)
    right_pitch = placement_pitch_deltas(right_placements, expected_pitch)
    left_pitch_error = left_pitch.get("max_error")
    right_pitch_error = right_pitch.get("max_error")
    bbox_pair_delta = max_pair_y_delta(left_doors, right_doors)
    placement_pair_delta = (
        max(abs(float(left["tyMm"]) - float(right["tyMm"])) for left, right in zip(left_placements, right_placements))
        if len(left_placements) == len(right_placements) and left_placements
        else None
    )
    pair_delta = bbox_pair_delta if bbox_pair_delta is not None else placement_pair_delta

    add_check(checks, "embedded_ordinary_door_count", len(door_rows) == DOOR_COUNT, len(door_rows), DOOR_COUNT)
    add_check(checks, "embedded_left_column_door_count", len(left_doors) == rows_per_column, len(left_doors), rows_per_column)
    add_check(checks, "embedded_right_column_door_count", len(right_doors) == rows_per_column, len(right_doors), rows_per_column)
    add_check(
        checks,
        "embedded_door_array_all_transforms_applied",
        transform_summary["total"] == DOOR_COUNT and transform_summary["ok"] == DOOR_COUNT,
        transform_summary,
        f"{DOOR_COUNT} placements with added=true and transformApplied=true",
    )
    add_check(
        checks,
        "embedded_left_column_x_position",
        isinstance(left_x_error, (int, float)) and float(left_x_error) <= TOL_PLACEMENT_X_MM,
        rounded(left_x_error),
        f"{EXPECTED_LEFT_DOOR_COLUMN_X_MM}mm +/- {TOL_PLACEMENT_X_MM}mm",
    )
    add_check(
        checks,
        "embedded_right_column_x_position",
        isinstance(right_x_error, (int, float)) and float(right_x_error) <= TOL_PLACEMENT_X_MM,
        rounded(right_x_error),
        f"{EXPECTED_RIGHT_DOOR_COLUMN_X_MM}mm +/- {TOL_PLACEMENT_X_MM}mm",
    )
    add_check(
        checks,
        "embedded_right_column_standard_rotation",
        right_column_rotation_ok(right_placements),
        [row.get("rotation") for row in right_placements[:2]],
        "right door placements use 180deg Z rotation",
    )
    add_check(
        checks,
        "embedded_left_right_door_y_alignment",
        isinstance(pair_delta, (int, float)) and float(pair_delta) <= TOL_PAIR_MM,
        rounded(pair_delta),
        f"<= {TOL_PAIR_MM}mm",
    )
    add_check(
        checks,
        "embedded_left_column_pitch",
        isinstance(left_pitch_error, (int, float)) and float(left_pitch_error) <= TOL_COUNT_PITCH_MM,
        left_pitch,
        expected_pitch,
    )
    add_check(
        checks,
        "embedded_right_column_pitch",
        isinstance(right_pitch_error, (int, float)) and float(right_pitch_error) <= TOL_COUNT_PITCH_MM,
        right_pitch,
        expected_pitch,
    )

    door_weld_count = count_rows_where(
        valid_rows,
        "App::Part",
        lambda label: ("\u50a8\u7269\u67dc\u95e8" in label and "\u710a\u63a5" in label)
        or "native_16029_door_weld" in label,
    )
    door_panel_count = count_rows_where(
        valid_rows,
        "Part::Feature",
        lambda label: "\u50a8\u7269\u67dc\u95e8\u677f" in label or "native_16029_door_panel" in label,
    )
    hinge_pin_count = count_rows_where(
        valid_rows,
        "Part::Feature",
        lambda label: "\u95e8\u8f74\u9500" in label or "door_hinge_pin" in label,
    )
    lock_hook_pad_count = count_rows(valid_rows, "Part::Feature", "U\u578b\u9501\u94a9\u57ab\u677f")
    electric_lock_hook_count = count_rows_where(
        valid_rows,
        "Part::Feature",
        lambda label: "\u7535\u63a7U\u578b\u9501\u94a9" in label or "electric_lock_hook" in label,
    )
    add_check(checks, "embedded_door_weld_subassembly_count", door_weld_count == DOOR_COUNT, door_weld_count, DOOR_COUNT)
    add_check(checks, "embedded_door_panel_feature_count", door_panel_count == DOOR_COUNT, door_panel_count, DOOR_COUNT)
    add_check(checks, "embedded_hinge_pin_count", hinge_pin_count == DOOR_COUNT, hinge_pin_count, DOOR_COUNT)
    add_check(checks, "embedded_lock_hook_pad_count", lock_hook_pad_count == DOOR_COUNT, lock_hook_pad_count, DOOR_COUNT)
    add_check(checks, "embedded_electric_lock_hook_count", electric_lock_hook_count == DOOR_COUNT, electric_lock_hook_count, DOOR_COUNT)

    shelf_rows = [row for row in valid_rows if row.get("type_id") == "App::Part" and "\u6a2a\u5c42\u677f" in row_label(row)]
    shelf_clusters = cluster_centers(shelf_rows)
    shelf_pitch = cluster_pitch_deltas(shelf_clusters, expected_pitch)
    shelf_pitch_error = shelf_pitch.get("max_error")
    add_check(
        checks,
        "embedded_shelf_module_count",
        len(shelf_rows) == expected_internal_levels * 2,
        len(shelf_rows),
        expected_internal_levels * 2,
    )
    add_check(
        checks,
        "embedded_shelf_level_pairs",
        count_ok_cluster_pairs(shelf_clusters, expected_internal_levels),
        shelf_clusters,
        f"{expected_internal_levels} levels x 2",
    )
    add_check(
        checks,
        "embedded_shelf_pitch",
        isinstance(shelf_pitch_error, (int, float)) and float(shelf_pitch_error) <= TOL_COUNT_PITCH_MM,
        shelf_pitch,
        expected_pitch,
    )
    shelf_boundary = boundary_offset_summary(left_doors, right_doors, shelf_rows, rows_per_column)
    add_check(
        checks,
        "embedded_shelf_driven_from_lower_door_boundary",
        bool(shelf_boundary.get("ok")),
        shelf_boundary,
        f"center_y = lower door y_max + {SHELF_AND_CROSSBAR_FROM_LOWER_DOOR_Y_MAX_MM}mm",
    )

    crossbar_rows = [row for row in valid_rows if is_crossbar(row)]
    crossbar_clusters = cluster_centers(crossbar_rows)
    crossbar_pitch = cluster_pitch_deltas(crossbar_clusters, expected_pitch)
    crossbar_pitch_error = crossbar_pitch.get("max_error")
    add_check(
        checks,
        "embedded_front_frame_crossbar_count",
        len(crossbar_rows) == expected_internal_levels * 2,
        len(crossbar_rows),
        expected_internal_levels * 2,
    )
    add_check(
        checks,
        "embedded_front_frame_crossbar_level_pairs",
        count_ok_cluster_pairs(crossbar_clusters, expected_internal_levels),
        crossbar_clusters,
        f"{expected_internal_levels} levels x 2",
    )
    add_check(
        checks,
        "embedded_front_frame_crossbar_pitch",
        isinstance(crossbar_pitch_error, (int, float)) and float(crossbar_pitch_error) <= TOL_COUNT_PITCH_MM,
        crossbar_pitch,
        expected_pitch,
    )
    crossbar_boundary = boundary_offset_summary(left_doors, right_doors, crossbar_rows, rows_per_column)
    add_check(
        checks,
        "embedded_front_frame_crossbar_driven_from_lower_door_boundary",
        bool(crossbar_boundary.get("ok")),
        crossbar_boundary,
        f"center_y = lower door y_max + {SHELF_AND_CROSSBAR_FROM_LOWER_DOOR_Y_MAX_MM}mm",
    )

    ok = all(item["ok"] or item["severity"] != "error" for item in checks)
    return {
        "generated_at": now_iso(),
        "ok": ok,
        "out_dir": str(OUT_DIR),
        "mode": MODE,
        "assembly": str(ASSEMBLY_PATH),
        "step": str(STEP_PATH),
        "bbox_csv": str(BBOX_CSV_PATH),
        "recommended_candidate_count": len(recommended),
        "combined_bbox_mm": combined_bbox,
        "combined_bbox_source": "root_app_part" if root_bbox_row else "union_valid_shapes",
        "candidate_matches": candidate_matches,
        "embedded_door_quality": {
            "door_count": len(door_rows),
            "left_column_door_count": len(left_doors),
            "right_column_door_count": len(right_doors),
            "door_array_transform_summary": transform_summary,
            "left_column_pitch": left_pitch,
            "right_column_pitch": right_pitch,
            "left_right_door_bbox_y_delta": rounded(bbox_pair_delta),
            "left_right_door_placement_y_delta": rounded(placement_pair_delta),
            "door_weld_count": door_weld_count,
            "door_panel_feature_count": door_panel_count,
            "hinge_pin_count": hinge_pin_count,
            "lock_hook_pad_count": lock_hook_pad_count,
            "electric_lock_hook_count": electric_lock_hook_count,
            "shelf_count": len(shelf_rows),
            "shelf_levels": shelf_clusters,
            "shelf_pitch": shelf_pitch,
            "shelf_boundary_offset": shelf_boundary,
            "crossbar_count": len(crossbar_rows),
            "crossbar_levels": crossbar_clusters,
            "crossbar_pitch": crossbar_pitch,
            "crossbar_boundary_offset": crossbar_boundary,
        },
        "checks": checks,
    }


def write_outputs(payload: dict[str, Any]) -> None:
    for path in (DATA_JSON_PATH, LOCAL_JSON_PATH):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    fieldnames = ["name", "ok", "actual", "expected", "severity"]
    for path in (DATA_CSV_PATH, LOCAL_CSV_PATH):
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=fieldnames)
            writer.writeheader()
            for check in payload["checks"]:
                writer.writerow({key: check.get(key) for key in fieldnames})

    lines = [
        f"# 16029 Enriched {DOOR_COUNT}-Door Validation",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Mode: `{payload['mode']}`",
        f"- Result: `{'PASS' if payload['ok'] else 'CHECK'}`",
        f"- Assembly: `{payload['assembly']}`",
        f"- STEP: `{payload['step']}`",
        f"- Recommended fixed modules placed: `{payload['recommended_candidate_count']}`",
        f"- Combined bbox source: `{payload['combined_bbox_source']}`",
        f"- Combined bbox: `{payload['combined_bbox_mm']}`",
        "",
        "## Checks",
        "",
        "| check | result | actual | expected |",
        "|---|---|---|---|",
    ]
    for check in payload["checks"]:
        lines.append(
            "| {name} | {result} | {actual} | {expected} |".format(
                name=check["name"],
                result="PASS" if check["ok"] else "CHECK",
                actual=str(check["actual"]).replace("|", "/"),
                expected=str(check["expected"]).replace("|", "/"),
            )
        )
    lines.append("")
    for path in (DATA_MD_PATH, LOCAL_MD_PATH):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    payload = build_payload()
    write_outputs(payload)
    print(json.dumps({"ok": payload["ok"], "checks": payload["checks"]}, ensure_ascii=False, indent=2))
    return 0 if payload["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
