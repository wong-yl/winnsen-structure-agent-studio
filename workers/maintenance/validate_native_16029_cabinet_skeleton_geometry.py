from __future__ import annotations

import csv
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_SERIES_DIR = ROOT_DIR / "workers" / "generated_models" / "SW-NATIVE-16029-CABINET-SKELETON-SERIES-20260521"
SERIES_DIR = Path(os.getenv("STUDIO_16029_NATIVE_SKELETON_SERIES_DIR", DEFAULT_SERIES_DIR))
DOOR_ARRAY_DIR = ROOT_DIR / "workers" / "generated_models" / "SW-NATIVE-16029-DOOR-ARRAY-MODULE-20260521"

DATA_JSON_PATH = Path(
    os.getenv("STUDIO_16029_NATIVE_SKELETON_GEOMETRY_GATE_JSON", ROOT_DIR / "data" / "solidworks_16029_native_skeleton_geometry_gate.json")
)
DATA_MD_PATH = Path(
    os.getenv("STUDIO_16029_NATIVE_SKELETON_GEOMETRY_GATE_MD", ROOT_DIR / "data" / "solidworks_16029_native_skeleton_geometry_gate.md")
)
DATA_CSV_PATH = Path(
    os.getenv("STUDIO_16029_NATIVE_SKELETON_GEOMETRY_GATE_CSV", ROOT_DIR / "data" / "solidworks_16029_native_skeleton_geometry_gate.csv")
)

LOCAL_JSON_PATH = SERIES_DIR / "cabinet_skeleton_geometry_gate.json"
LOCAL_MD_PATH = SERIES_DIR / "cabinet_skeleton_geometry_gate.md"
LOCAL_CSV_PATH = SERIES_DIR / "cabinet_skeleton_geometry_gate.csv"

TARGET_DOOR_COUNTS = [10, 12, 14]
DOOR_HEIGHT_BY_COUNT = {10: 359.0, 12: 298.0, 14: 254.429}
EXPECTED_CABINET_WIDTH_MM = 1000.0
EXPECTED_CABINET_HEIGHT_MAX_Y_MM = 1917.0
EXPECTED_CABINET_DEPTH_MM = 550.0
EXPECTED_DOOR_TOP_MAX_Y_MM = 1857.0
EXPECTED_DOOR_BOTTOM_MIN_Y_MM = -25.0
EXPECTED_LEFT_DOOR_COLUMN_X_MM = -258.5
EXPECTED_RIGHT_DOOR_COLUMN_X_MM = 258.5
VISUAL_GAP_MM = 7.0
SHELF_LABEL_TOKEN = "\u6a2a\u5c42\u677f"
TOL_COUNT_PITCH_MM = 0.15
TOL_BBOX_MM = 0.5
TOL_PAIR_MM = 0.1
TOL_PLACEMENT_X_MM = 0.1


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


def load_door_array_result(doors: int) -> dict[str, Any] | None:
    path = DOOR_ARRAY_DIR / f"native_16029_door_array_{doors}door_v1_csharp_result.json"
    if not path.exists():
        return None
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None
    payload["_path"] = str(path)
    return payload


def center(min_value: float | None, max_value: float | None) -> float | None:
    if min_value is None or max_value is None:
        return None
    return (min_value + max_value) / 2.0


def valid_geometry(row: dict[str, Any]) -> bool:
    volume = row.get("volume")
    x_min = row.get("x_min")
    y_min = row.get("y_min")
    z_min = row.get("z_min")
    return (
        isinstance(volume, float)
        and volume > 0
        and isinstance(x_min, float)
        and isinstance(y_min, float)
        and isinstance(z_min, float)
        and abs(x_min) < 1e20
        and abs(y_min) < 1e20
        and abs(z_min) < 1e20
    )


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
            "ok": bool(ok),
            "actual": actual,
            "expected": expected,
            "severity": severity,
            "detail": detail,
        }
    )


def bbox_payload(row: dict[str, Any] | None) -> dict[str, float | None]:
    if row is None:
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


def row_label(row: dict[str, Any]) -> str:
    return str(row.get("label") or "")


def rows_by_prefix(rows: list[dict[str, Any]], type_id: str, prefix: str) -> list[dict[str, Any]]:
    return [row for row in rows if row.get("type_id") == type_id and row_label(row).startswith(prefix)]


def is_door_module_row(row: dict[str, Any], doors: int) -> bool:
    if row.get("type_id") != "App::Part":
        return False
    label = row_label(row)
    if label.startswith(f"native_16029_ordinary_door_{doors}door"):
        return True
    x_len = row.get("x_len")
    return (
        "\u50a8\u7269\u67dc\u95e8" in label
        and "\u88c5\u914d" in label
        and isinstance(x_len, float)
        and abs(x_len - 437.0) <= 0.75
    )


def label_contains(row: dict[str, Any], token: str) -> bool:
    return token in row_label(row)


def count_rows(rows: list[dict[str, Any]], type_id: str, *tokens: str) -> int:
    return sum(
        1
        for row in rows
        if row.get("type_id") == type_id and all(label_contains(row, token) for token in tokens)
    )


def count_rows_where(rows: list[dict[str, Any]], type_id: str, predicate) -> int:
    return sum(1 for row in rows if row.get("type_id") == type_id and predicate(row_label(row)))


def root_row(rows: list[dict[str, Any]], doors: int) -> dict[str, Any] | None:
    exact = f"native_16029_{doors}door_cabinet_skeleton_v2"
    for row in rows:
        if row.get("type_id") == "App::Part" and row_label(row) == exact:
            return row
    return None


def is_crossbar(row: dict[str, Any]) -> bool:
    if row.get("type_id") != "Part::Feature":
        return False
    y_len = row.get("y_len")
    z_min = row.get("z_min")
    z_max = row.get("z_max")
    x_len = row.get("x_len")
    return (
        isinstance(y_len, float)
        and isinstance(z_min, float)
        and isinstance(z_max, float)
        and isinstance(x_len, float)
        and abs(y_len - 15.0) < 0.02
        and abs(z_min - (-19.7)) < 0.2
        and z_max < 0.2
        and 400.0 < x_len < 470.0
    )


def split_door_columns(door_rows: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    left = [row for row in door_rows if isinstance(row.get("x_center"), float) and row["x_center"] < 0]
    right = [row for row in door_rows if isinstance(row.get("x_center"), float) and row["x_center"] > 0]
    left.sort(key=lambda row: float(row["y_center"] or 0))
    right.sort(key=lambda row: float(row["y_center"] or 0))
    return left, right


def max_pair_y_delta(left: list[dict[str, Any]], right: list[dict[str, Any]]) -> float | None:
    if len(left) != len(right) or not left:
        return None
    deltas: list[float] = []
    for left_row, right_row in zip(left, right):
        for key in ("y_min", "y_max", "y_center"):
            left_value = left_row.get(key)
            right_value = right_row.get(key)
            if isinstance(left_value, float) and isinstance(right_value, float):
                deltas.append(abs(left_value - right_value))
    return max(deltas) if deltas else None


def placement_column_rows(result: dict[str, Any] | None, column: str) -> list[dict[str, Any]]:
    if not result:
        return []
    rows: list[dict[str, Any]] = []
    for placement in result.get("placements") or []:
        role = str(placement.get("role") or "")
        marker = f"_{column}"
        if marker not in role:
            continue
        ty = placement.get("tyMm")
        if not isinstance(ty, (int, float)):
            continue
        rows.append(placement)
    rows.sort(key=lambda row: float(row["tyMm"]))
    return rows


def max_placement_y_delta(left: list[dict[str, Any]], right: list[dict[str, Any]]) -> float | None:
    if len(left) != len(right) or not left:
        return None
    return max(abs(float(left_row["tyMm"]) - float(right_row["tyMm"])) for left_row, right_row in zip(left, right))


def placement_transform_summary(placements: list[dict[str, Any]]) -> dict[str, Any]:
    failed: list[str] = []
    for placement in placements:
        ok = (
            placement.get("exists") is True
            and placement.get("opened") is True
            and placement.get("added") is True
            and placement.get("transformCreated") is True
            and placement.get("transformApplied") is True
            and not placement.get("error")
        )
        if not ok:
            failed.append(str(placement.get("role") or ""))
    return {"total": len(placements), "ok": len(placements) - len(failed), "failed_roles": failed}


def placement_x_error(placements: list[dict[str, Any]], expected_x: float) -> float | None:
    values = [float(row["txMm"]) for row in placements if isinstance(row.get("txMm"), (int, float))]
    if not values:
        return None
    return max(abs(value - expected_x) for value in values)


def placement_visual_y_values(placements: list[dict[str, Any]], door_height: float) -> list[float]:
    values: list[float] = []
    half_height = door_height / 2.0
    for placement in placements:
        ty = placement.get("tyMm")
        if isinstance(ty, (int, float)):
            values.extend([float(ty) - half_height, float(ty) + half_height])
    return values


def placement_pitch_deltas(placements: list[dict[str, Any]], expected_pitch: float) -> dict[str, Any]:
    centers = sorted(float(row["tyMm"]) for row in placements if isinstance(row.get("tyMm"), (int, float)))
    pitches = [centers[index + 1] - centers[index] for index in range(len(centers) - 1)]
    if not pitches:
        return {"values": [], "max_error": None}
    return {
        "values": [rounded(value) for value in pitches],
        "max_error": rounded(max(abs(value - expected_pitch) for value in pitches)),
    }


def right_column_rotation_ok(placements: list[dict[str, Any]]) -> bool:
    if not placements:
        return False
    for placement in placements:
        rotation = placement.get("rotation")
        if not isinstance(rotation, list) or len(rotation) < 9:
            return False
        if not (
            abs(float(rotation[0]) - (-1.0)) <= 1e-6
            and abs(float(rotation[4]) - (-1.0)) <= 1e-6
            and abs(float(rotation[8]) - 1.0) <= 1e-6
        ):
            return False
    return True


def pitch_deltas(rows: list[dict[str, Any]], expected_pitch: float) -> dict[str, Any]:
    centers = [float(row["y_center"]) for row in rows if isinstance(row.get("y_center"), float)]
    centers.sort()
    pitches = [centers[index + 1] - centers[index] for index in range(len(centers) - 1)]
    if not pitches:
        return {"values": [], "max_error": None}
    errors = [abs(value - expected_pitch) for value in pitches]
    return {"values": [rounded(value) for value in pitches], "max_error": rounded(max(errors))}


def cluster_centers(rows: list[dict[str, Any]], tolerance: float = 0.5) -> list[dict[str, Any]]:
    centers = sorted(float(row["y_center"]) for row in rows if isinstance(row.get("y_center"), float))
    clusters: list[list[float]] = []
    for value in centers:
        if not clusters or abs(value - clusters[-1][-1]) > tolerance:
            clusters.append([value])
        else:
            clusters[-1].append(value)
    return [
        {
            "center": rounded(sum(values) / len(values)),
            "count": len(values),
        }
        for values in clusters
    ]


def cluster_pitch_deltas(clusters: list[dict[str, Any]], expected_pitch: float) -> dict[str, Any]:
    centers = [float(item["center"]) for item in clusters if isinstance(item.get("center"), (int, float))]
    centers.sort()
    pitches = [centers[index + 1] - centers[index] for index in range(len(centers) - 1)]
    if not pitches:
        return {"values": [], "max_error": None}
    return {
        "values": [rounded(value) for value in pitches],
        "max_error": rounded(max(abs(value - expected_pitch) for value in pitches)),
    }


def count_ok_cluster_pairs(clusters: list[dict[str, Any]], expected_count: int) -> bool:
    return len(clusters) == expected_count and all(item.get("count") == 2 for item in clusters)


def audit_variant(doors: int) -> dict[str, Any]:
    rows_per_column = doors // 2
    door_height = DOOR_HEIGHT_BY_COUNT[doors]
    expected_pitch = door_height + VISUAL_GAP_MM
    expected_internal_levels = rows_per_column - 1
    bbox_csv = SERIES_DIR / f"native_16029_{doors}door_cabinet_skeleton_v2_step_bbox.csv"
    assembly = SERIES_DIR / f"native_16029_{doors}door_cabinet_skeleton_v2.SLDASM"
    step = SERIES_DIR / f"native_16029_{doors}door_cabinet_skeleton_v2.step"
    checks: list[dict[str, Any]] = []

    add_check(checks, "assembly_exists", assembly.exists(), str(assembly), "existing native SLDASM")
    add_check(checks, "step_exists", step.exists(), str(step), "existing STEP export")
    add_check(checks, "bbox_csv_exists", bbox_csv.exists(), str(bbox_csv), "existing FreeCAD bbox CSV")
    if not bbox_csv.exists():
        return build_variant_payload(doors, rows_per_column, door_height, expected_pitch, {}, checks, {})

    rows = [row for row in load_bbox_rows(bbox_csv) if valid_geometry(row)]
    root = root_row(rows, doors)
    add_check(checks, "root_bbox_exists", root is not None, row_label(root or {}), f"native_16029_{doors}door_cabinet_skeleton_v2")
    if root is not None:
        x_len = root.get("x_len")
        y_max = root.get("y_max")
        y_min = root.get("y_min")
        z_len = root.get("z_len")
        add_check(
            checks,
            "root_width_1000mm",
            isinstance(x_len, float) and abs(x_len - EXPECTED_CABINET_WIDTH_MM) <= TOL_BBOX_MM,
            rounded(x_len),
            EXPECTED_CABINET_WIDTH_MM,
        )
        add_check(
            checks,
            "root_top_y_1917mm",
            isinstance(y_max, float) and abs(y_max - EXPECTED_CABINET_HEIGHT_MAX_Y_MM) <= TOL_BBOX_MM,
            rounded(y_max),
            EXPECTED_CABINET_HEIGHT_MAX_Y_MM,
        )
        add_check(
            checks,
            "root_bottom_not_flying",
            isinstance(y_min, float) and y_min >= -35.0,
            rounded(y_min),
            ">= -35mm",
            detail="door hardware may protrude slightly below the nominal datum",
        )
        add_check(
            checks,
            "root_depth_550mm",
            isinstance(z_len, float) and abs(z_len - EXPECTED_CABINET_DEPTH_MM) <= TOL_BBOX_MM,
            rounded(z_len),
            EXPECTED_CABINET_DEPTH_MM,
        )

    door_rows = [row for row in rows if is_door_module_row(row, doors)]
    left_doors, right_doors = split_door_columns(door_rows)
    door_array_result = load_door_array_result(doors)
    left_placements = placement_column_rows(door_array_result, "L")
    right_placements = placement_column_rows(door_array_result, "R")
    all_placements = left_placements + right_placements
    transform_summary = placement_transform_summary(all_placements)
    add_check(checks, "ordinary_door_count", len(door_rows) == doors, len(door_rows), doors)
    add_check(checks, "left_column_door_count", len(left_doors) == rows_per_column, len(left_doors), rows_per_column)
    add_check(checks, "right_column_door_count", len(right_doors) == rows_per_column, len(right_doors), rows_per_column)
    placement_pair_delta = max_placement_y_delta(left_placements, right_placements)
    pair_delta = placement_pair_delta if placement_pair_delta is not None else max_pair_y_delta(left_doors, right_doors)
    add_check(
        checks,
        "door_array_placement_evidence",
        len(left_placements) == rows_per_column and len(right_placements) == rows_per_column,
        {"left": len(left_placements), "right": len(right_placements), "source": (door_array_result or {}).get("_path", "")},
        f"{rows_per_column} left placements and {rows_per_column} right placements",
    )
    add_check(
        checks,
        "door_array_all_transforms_applied",
        transform_summary["total"] == doors and transform_summary["ok"] == doors,
        transform_summary,
        f"{doors} placements with added=true and transformApplied=true",
    )
    left_x_error = placement_x_error(left_placements, EXPECTED_LEFT_DOOR_COLUMN_X_MM)
    right_x_error = placement_x_error(right_placements, EXPECTED_RIGHT_DOOR_COLUMN_X_MM)
    add_check(
        checks,
        "left_column_x_position",
        isinstance(left_x_error, (int, float)) and float(left_x_error) <= TOL_PLACEMENT_X_MM,
        rounded(left_x_error),
        f"{EXPECTED_LEFT_DOOR_COLUMN_X_MM}mm +/- {TOL_PLACEMENT_X_MM}mm",
    )
    add_check(
        checks,
        "right_column_x_position",
        isinstance(right_x_error, (int, float)) and float(right_x_error) <= TOL_PLACEMENT_X_MM,
        rounded(right_x_error),
        f"{EXPECTED_RIGHT_DOOR_COLUMN_X_MM}mm +/- {TOL_PLACEMENT_X_MM}mm",
    )
    add_check(
        checks,
        "right_column_standard_rotation",
        right_column_rotation_ok(right_placements),
        [row.get("rotation") for row in right_placements[:2]],
        "right door placements use 180deg Z rotation [-1,0,0;0,-1,0;0,0,1]",
    )
    add_check(
        checks,
        "left_right_door_y_alignment",
        isinstance(pair_delta, float) and pair_delta <= TOL_PAIR_MM,
        rounded(pair_delta),
        f"<= {TOL_PAIR_MM}mm",
        detail="checked against SolidWorks placement Ty because mirrored door hardware makes whole-assembly bbox asymmetric",
    )

    placement_y_values = placement_visual_y_values(all_placements, door_height)
    all_door_y_values = placement_y_values or [
        value for row in door_rows for value in (row.get("y_min"), row.get("y_max")) if isinstance(value, float)
    ]
    add_check(
        checks,
        "door_array_bottom_in_range",
        bool(all_door_y_values) and min(all_door_y_values) >= EXPECTED_DOOR_BOTTOM_MIN_Y_MM,
        rounded(min(all_door_y_values) if all_door_y_values else None),
        f">= {EXPECTED_DOOR_BOTTOM_MIN_Y_MM}mm",
    )
    add_check(
        checks,
        "door_array_top_in_range",
        bool(all_door_y_values) and max(all_door_y_values) <= EXPECTED_DOOR_TOP_MAX_Y_MM,
        rounded(max(all_door_y_values) if all_door_y_values else None),
        f"<= {EXPECTED_DOOR_TOP_MAX_Y_MM}mm",
    )

    left_pitch = placement_pitch_deltas(left_placements, expected_pitch) if left_placements else pitch_deltas(left_doors, expected_pitch)
    right_pitch = placement_pitch_deltas(right_placements, expected_pitch) if right_placements else pitch_deltas(right_doors, expected_pitch)
    left_pitch_error = left_pitch.get("max_error")
    right_pitch_error = right_pitch.get("max_error")
    add_check(
        checks,
        "left_column_pitch",
        isinstance(left_pitch_error, (int, float)) and float(left_pitch_error) <= TOL_COUNT_PITCH_MM,
        left_pitch,
        expected_pitch,
    )
    add_check(
        checks,
        "right_column_pitch",
        isinstance(right_pitch_error, (int, float)) and float(right_pitch_error) <= TOL_COUNT_PITCH_MM,
        right_pitch,
        expected_pitch,
    )

    door_weld_count = count_rows_where(
        rows,
        "App::Part",
        lambda label: ("\u50a8\u7269\u67dc\u95e8" in label and "\u710a\u63a5" in label)
        or "native_16029_door_weld" in label,
    )
    door_panel_count = count_rows_where(
        rows,
        "Part::Feature",
        lambda label: "\u50a8\u7269\u67dc\u95e8\u677f" in label or "native_16029_door_panel" in label,
    )
    hinge_pin_count = count_rows_where(
        rows,
        "Part::Feature",
        lambda label: "\u95e8\u8f74\u9500" in label or "door_hinge_pin" in label,
    )
    lock_hook_pad_count = count_rows(rows, "Part::Feature", "U\u578b\u9501\u94a9\u57ab\u677f")
    electric_lock_hook_count = count_rows_where(
        rows,
        "Part::Feature",
        lambda label: "\u7535\u63a7U\u578b\u9501\u94a9" in label or "electric_lock_hook" in label,
    )
    add_check(checks, "door_weld_subassembly_count", door_weld_count == doors, door_weld_count, doors)
    add_check(checks, "door_panel_feature_count", door_panel_count == doors, door_panel_count, doors)
    add_check(checks, "hinge_pin_count", hinge_pin_count == doors, hinge_pin_count, doors)
    add_check(checks, "lock_hook_pad_count", lock_hook_pad_count == doors, lock_hook_pad_count, doors)
    add_check(checks, "electric_lock_hook_count", electric_lock_hook_count == doors, electric_lock_hook_count, doors)

    shelf_rows = [row for row in rows if row.get("type_id") == "App::Part" and SHELF_LABEL_TOKEN in row_label(row)]
    shelf_clusters = cluster_centers(shelf_rows)
    shelf_pitch = cluster_pitch_deltas(shelf_clusters, expected_pitch)
    shelf_pitch_error = shelf_pitch.get("max_error")
    add_check(checks, "shelf_module_count", len(shelf_rows) == expected_internal_levels * 2, len(shelf_rows), expected_internal_levels * 2)
    add_check(checks, "shelf_level_pairs", count_ok_cluster_pairs(shelf_clusters, expected_internal_levels), shelf_clusters, f"{expected_internal_levels} levels x 2")
    add_check(
        checks,
        "shelf_pitch",
        isinstance(shelf_pitch_error, (int, float)) and float(shelf_pitch_error) <= TOL_COUNT_PITCH_MM,
        shelf_pitch,
        expected_pitch,
    )

    crossbar_rows = [row for row in rows if is_crossbar(row)]
    crossbar_clusters = cluster_centers(crossbar_rows)
    crossbar_pitch = cluster_pitch_deltas(crossbar_clusters, expected_pitch)
    crossbar_pitch_error = crossbar_pitch.get("max_error")
    add_check(
        checks,
        "front_frame_crossbar_count",
        len(crossbar_rows) == expected_internal_levels * 2,
        len(crossbar_rows),
        expected_internal_levels * 2,
    )
    add_check(
        checks,
        "front_frame_crossbar_level_pairs",
        count_ok_cluster_pairs(crossbar_clusters, expected_internal_levels),
        crossbar_clusters,
        f"{expected_internal_levels} levels x 2",
    )
    add_check(
        checks,
        "front_frame_crossbar_pitch",
        isinstance(crossbar_pitch_error, (int, float)) and float(crossbar_pitch_error) <= TOL_COUNT_PITCH_MM,
        crossbar_pitch,
        expected_pitch,
    )

    metrics = {
        "bbox_csv": str(bbox_csv),
        "assembly": str(assembly),
        "step": str(step),
        "root_bbox": bbox_payload(root),
        "door_count": len(door_rows),
        "left_column_door_count": len(left_doors),
        "right_column_door_count": len(right_doors),
        "door_array_result": (door_array_result or {}).get("_path", ""),
        "left_column_placement_count": len(left_placements),
        "right_column_placement_count": len(right_placements),
        "door_array_transform_summary": transform_summary,
        "left_column_x_error": rounded(left_x_error),
        "right_column_x_error": rounded(right_x_error),
        "left_column_pitch": left_pitch,
        "right_column_pitch": right_pitch,
        "door_weld_count": door_weld_count,
        "door_panel_feature_count": door_panel_count,
        "hinge_pin_count": hinge_pin_count,
        "lock_hook_pad_count": lock_hook_pad_count,
        "electric_lock_hook_count": electric_lock_hook_count,
        "shelf_count": len(shelf_rows),
        "shelf_levels": shelf_clusters,
        "shelf_pitch": shelf_pitch,
        "crossbar_count": len(crossbar_rows),
        "crossbar_levels": crossbar_clusters,
        "crossbar_pitch": crossbar_pitch,
    }
    return build_variant_payload(doors, rows_per_column, door_height, expected_pitch, bbox_payload(root), checks, metrics)


def build_variant_payload(
    doors: int,
    rows_per_column: int,
    door_height: float,
    expected_pitch: float,
    root_bbox: dict[str, Any],
    checks: list[dict[str, Any]],
    metrics: dict[str, Any],
) -> dict[str, Any]:
    failed_errors = [check for check in checks if not check["ok"] and check.get("severity") == "error"]
    failed_warnings = [check for check in checks if not check["ok"] and check.get("severity") != "error"]
    status = "PASS" if not failed_errors and not failed_warnings else ("WARN" if not failed_errors else "FAIL")
    return {
        "door_count": doors,
        "rows_per_column": rows_per_column,
        "door_height_mm": rounded(door_height),
        "expected_pitch_mm": rounded(expected_pitch),
        "status": status,
        "root_bbox": root_bbox,
        "check_count": len(checks),
        "failed_error_count": len(failed_errors),
        "failed_warning_count": len(failed_warnings),
        "checks": checks,
        "metrics": metrics,
    }


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def write_csv(path: Path, variants: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["door_count", "status", "check", "ok", "severity", "actual", "expected", "detail"],
        )
        writer.writeheader()
        for variant in variants:
            for check in variant["checks"]:
                writer.writerow(
                    {
                        "door_count": variant["door_count"],
                        "status": variant["status"],
                        "check": check["name"],
                        "ok": check["ok"],
                        "severity": check["severity"],
                        "actual": json.dumps(check["actual"], ensure_ascii=False),
                        "expected": json.dumps(check["expected"], ensure_ascii=False),
                        "detail": check.get("detail", ""),
                    }
                )


def write_markdown(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# 16029 native SolidWorks cabinet skeleton geometry gate",
        "",
        f"- generated_at: `{payload['generated_at']}`",
        f"- status: `{payload['status']}`",
        f"- source_dir: `{payload['source_dir']}`",
        "",
        "## Variant summary",
        "",
        "| doors | rows/column | door height | pitch | status | failed errors |",
        "| ---: | ---: | ---: | ---: | --- | ---: |",
    ]
    for variant in payload["variants"]:
        lines.append(
            "| {door_count} | {rows_per_column} | {door_height_mm} mm | {expected_pitch_mm} mm | {status} | {failed_error_count} |".format(
                **variant
            )
        )
    lines.extend(
        [
            "",
            "## Gate coverage",
            "",
            "- Root bbox must stay near 1000W x 1917H x 550D.",
            "- Left/right door columns must contain the same row count and aligned Y positions.",
            "- Each SolidWorks door placement must be added and have its transform applied.",
            "- Left/right door column X placements must stay at the learned 16029 datum.",
            "- Door pitch must match the configured door height plus 7 mm visual gap.",
            "- Door weldments, door panels, hinge pins, U-lock hook pads, and electric lock hooks must match the door count.",
            "- Shelf modules and front-frame crossbars must form left/right pairs at each internal level.",
            "- Shelf and front-frame crossbar pitch must match the same row pitch, catching flying or collapsed arrays.",
            "",
            "## Failed checks",
            "",
        ]
    )
    failures = [
        (variant, check)
        for variant in payload["variants"]
        for check in variant["checks"]
        if not check["ok"]
    ]
    if not failures:
        lines.append("- None.")
    else:
        for variant, check in failures:
            lines.append(
                "- {door_count} doors `{name}`: actual `{actual}`, expected `{expected}`. {detail}".format(
                    door_count=variant["door_count"],
                    name=check["name"],
                    actual=check["actual"],
                    expected=check["expected"],
                    detail=check.get("detail", ""),
                )
            )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    variants = [audit_variant(doors) for doors in TARGET_DOOR_COUNTS]
    status = "PASS" if all(variant["status"] == "PASS" for variant in variants) else "FAIL"
    payload = {
        "generated_at": now_iso(),
        "status": status,
        "source_dir": str(SERIES_DIR),
        "target_door_counts": TARGET_DOOR_COUNTS,
        "variants": variants,
        "outputs": {
            "data_json": str(DATA_JSON_PATH),
            "data_markdown": str(DATA_MD_PATH),
            "data_csv": str(DATA_CSV_PATH),
            "local_json": str(LOCAL_JSON_PATH),
            "local_markdown": str(LOCAL_MD_PATH),
            "local_csv": str(LOCAL_CSV_PATH),
        },
    }
    for path in (DATA_JSON_PATH, LOCAL_JSON_PATH):
        write_json(path, payload)
    for path in (DATA_CSV_PATH, LOCAL_CSV_PATH):
        write_csv(path, variants)
    for path in (DATA_MD_PATH, LOCAL_MD_PATH):
        write_markdown(path, payload)
    print(DATA_JSON_PATH)
    return 0 if status == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
