from __future__ import annotations

import csv
import json
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]

OUTPUT_JSON_PATH = ROOT_DIR / "data" / "locker_16029_variable_door_stack_contract.json"
OUTPUT_MARKDOWN_PATH = ROOT_DIR / "data" / "locker_16029_variable_door_stack_contract.md"
OUTPUT_VALIDATION_JSON_PATH = ROOT_DIR / "data" / "locker_16029_variable_door_stack_contract_validation.json"
OUTPUT_VALIDATION_MARKDOWN_PATH = ROOT_DIR / "data" / "locker_16029_variable_door_stack_contract_validation.md"
OUTPUT_VALIDATION_CSV_PATH = ROOT_DIR / "data" / "locker_16029_variable_door_stack_contract_validation.csv"

SOURCE_ITEM_DIR = ROOT_DIR / "workers" / "drawing_sheetmetal" / "runs" / "BATCH-16029-SHEETMETAL-20260519" / "items"
SOURCE_CLASS_PATHS = {
    1: SOURCE_ITEM_DIR / "033_储物柜门板1_12展开图" / "summary.json",
    2: SOURCE_ITEM_DIR / "034_储物柜门板2_12展开图" / "summary.json",
    3: SOURCE_ITEM_DIR / "035_储物柜门板3_12展开图" / "summary.json",
    4: SOURCE_ITEM_DIR / "036_储物柜门板4_12展开图" / "summary.json",
    5: SOURCE_ITEM_DIR / "037_储物柜门板5_12展开图" / "summary.json",
    6: SOURCE_ITEM_DIR / "038_储物柜门板6_12展开图" / "summary.json",
}
STANDARD_SOLIDWORKS_ROOT = Path("C:/sw16029_standard_ascii")

OUTER_WIDTH_MM = 1200.0
OUTER_HEIGHT_MM = 1917.0
OUTER_DEPTH_MM = 550.0
SIDE_MARGIN_MM = 23.0
CENTER_GAP_MM = 80.0
DOOR_WIDTH_MM = 537.0
BASELINE_DOOR_WIDTH_MM = 437.0
BASELINE_FLAT_WIDTH_MM = 473.4
DOOR_AREA_BOTTOM_Y_MM = 30.0
DOOR_AREA_TOP_Y_MM = 1857.0
GRID_BOTTOM_GAP_MM = 2.0
GRID_TOP_GAP_MM = 2.0
UNIT_PITCH_MM = 152.5
TOTAL_VERTICAL_UNITS = 12
VISUAL_GAP_MM = 7.0
INTERNAL_CLEARANCE_BELOW_SEPARATOR_MM = 2.0
ROW_SEPARATOR_THICKNESS_MM = 3.0
INTERNAL_CLEARANCE_ABOVE_SEPARATOR_MM = 2.0
DOOR_FLAT_HEIGHT_EXTRA_MM = 36.4
SHELF_FROM_LOWER_DOOR_Y_MAX_MM = 2.0
FRONT_FRAME_FROM_LOWER_DOOR_Y_MAX_MM = -10.0
TOL_MM = 0.001


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def rounded(value: float) -> float:
    return round(float(value), 3)


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def first_existing(paths: list[Path]) -> str | None:
    for path in paths:
        if path.exists():
            return str(path)
    return None


def door_height_from_units(slot_units: int) -> float:
    return slot_units * UNIT_PITCH_MM - VISUAL_GAP_MM


def door_flat_height_from_units(slot_units: int) -> float:
    return door_height_from_units(slot_units) + DOOR_FLAT_HEIGHT_EXTRA_MM


def build_vertical_gap_stack_rule() -> dict[str, Any]:
    internal_gap_parts = [
        INTERNAL_CLEARANCE_BELOW_SEPARATOR_MM,
        ROW_SEPARATOR_THICKNESS_MM,
        INTERNAL_CLEARANCE_ABOVE_SEPARATOR_MM,
    ]
    return {
        "top_boundary_clearance_mm": GRID_TOP_GAP_MM,
        "bottom_boundary_clearance_mm": GRID_BOTTOM_GAP_MM,
        "internal_gap_pattern_mm": internal_gap_parts,
        "internal_gap_total_mm": rounded(sum(internal_gap_parts)),
        "internal_gap_rule": "lower door upper edge + 2 clearance + 3 row separator + 2 clearance + upper door lower edge",
        "visual_gap_mm": VISUAL_GAP_MM,
        "source_evidence": [
            {
                "path": "data/locker_16029_dimension_contract.json",
                "evidence": "door_grid_top_gap=2.0, door_grid_bottom_gap=2.0, visual_gap=7.0",
            },
            {
                "path": "C:/sw16029_standard_ascii/generate_locker_16029_freecad.py",
                "evidence": "standard generator uses top_gap=2.0, bottom_gap=2.0, visual_gap=7.0",
            },
            {
                "path": "data/locker_16029_height_candidate_shell_role_review.md",
                "evidence": "row separator candidates have 3.0 mm Y thickness; historical evidence only, not a 2117H source geometry reuse",
            },
        ],
    }


def build_source_class(slot_units: int, source_path: Path) -> dict[str, Any]:
    summary = read_json(source_path)
    bbox = summary.get("manufacturing_bbox_mm", {})
    door_panel_path = first_existing(
        [
            STANDARD_SOLIDWORKS_ROOT / f"door_panel_{slot_units}_12.SLDPRT",
            STANDARD_SOLIDWORKS_ROOT / f"储物柜门板{slot_units}╱12.SLDPRT",
        ]
    )
    stiffener_path = first_existing(
        [
            STANDARD_SOLIDWORKS_ROOT / f"rib_{slot_units}_12.SLDPRT",
            STANDARD_SOLIDWORKS_ROOT / f"柜门加强筋{slot_units}╱12.SLDPRT",
        ]
    )
    return {
        "source_class_id": f"{slot_units}/12",
        "slot_units": slot_units,
        "source_summary_path": str(source_path),
        "source_file_name": summary.get("file_name"),
        "baseline_flat_width_mm": rounded(float(bbox.get("width", 0.0))),
        "baseline_flat_height_mm": rounded(float(bbox.get("height", 0.0))),
        "expected_installed_door_height_mm": rounded(door_height_from_units(slot_units)),
        "expected_flat_height_mm": rounded(door_flat_height_from_units(slot_units)),
        "quality_status": summary.get("quality_status"),
        "manufacturing_bbox_source": summary.get("manufacturing_bbox_source"),
        "solidworks_source_parts": {
            "door_panel": door_panel_path,
            "stiffener": stiffener_path,
            "stiffener_status": "bound" if stiffener_path else "not_found_confirm_if_this_height_class_uses_no_stiffener",
        },
    }


def column_rows(column: str, units: list[int]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    cursor_units = 0
    for index, slot_units in enumerate(units, start=1):
        y_min = DOOR_AREA_BOTTOM_Y_MM + GRID_BOTTOM_GAP_MM + cursor_units * UNIT_PITCH_MM
        height = door_height_from_units(slot_units)
        y_max = y_min + height
        rows.append(
            {
                "column": column,
                "row_index_from_bottom": index,
                "slot_units": slot_units,
                "source_class_id": f"{slot_units}/12",
                "y_min_mm": rounded(y_min),
                "y_max_mm": rounded(y_max),
                "center_y_mm": rounded((y_min + y_max) / 2.0),
                "installed_door_height_mm": rounded(height),
                "pitch_span_mm": rounded(slot_units * UNIT_PITCH_MM),
                "baseline_flat_height_mm": rounded(door_flat_height_from_units(slot_units)),
            }
        )
        cursor_units += slot_units
    return rows


def boundary_rows(column: str, rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    boundaries: list[dict[str, Any]] = []
    for row in rows[:-1]:
        y_max = float(row["y_max_mm"])
        boundaries.append(
            {
                "column": column,
                "after_row_index_from_bottom": row["row_index_from_bottom"],
                "after_cumulative_units": sum(int(r["slot_units"]) for r in rows[: int(row["row_index_from_bottom"])]),
                "lower_door_y_max_mm": rounded(y_max),
                "internal_gap_decomposition_mm": {
                    "lower_door_to_row_separator": INTERNAL_CLEARANCE_BELOW_SEPARATOR_MM,
                    "row_separator_thickness": ROW_SEPARATOR_THICKNESS_MM,
                    "row_separator_to_upper_door": INTERNAL_CLEARANCE_ABOVE_SEPARATOR_MM,
                    "total": VISUAL_GAP_MM,
                    "row_separator_y_min_mm": rounded(y_max + INTERNAL_CLEARANCE_BELOW_SEPARATOR_MM),
                    "row_separator_y_max_mm": rounded(
                        y_max + INTERNAL_CLEARANCE_BELOW_SEPARATOR_MM + ROW_SEPARATOR_THICKNESS_MM
                    ),
                    "next_upper_door_y_min_mm": rounded(y_max + VISUAL_GAP_MM),
                },
                "shelf_center_y_mm": rounded(y_max + SHELF_FROM_LOWER_DOOR_Y_MAX_MM),
                "front_frame_crossbar_center_y_mm": rounded(y_max + FRONT_FRAME_FROM_LOWER_DOOR_Y_MAX_MM),
            }
        )
    return boundaries


def build_recipe(
    recipe_id: str,
    status: str,
    left_units: list[int],
    right_units: list[int],
    notes: list[str],
) -> dict[str, Any]:
    left_rows = column_rows("L", left_units)
    right_rows = column_rows("R", right_units)
    boundaries = boundary_rows("L", left_rows) + boundary_rows("R", right_rows)
    door_count = len(left_rows) + len(right_rows)
    expected_between_count = max(len(left_rows) - 1, 0) + max(len(right_rows) - 1, 0)
    used_units = sorted(set(left_units + right_units))
    source_classes = {item["slot_units"]: item for item in [build_source_class(slot_units, SOURCE_CLASS_PATHS[slot_units]) for slot_units in SOURCE_CLASS_PATHS]}
    missing_stiffeners = [
        slot_units
        for slot_units in used_units
        if source_classes[slot_units]["solidworks_source_parts"]["stiffener"] is None
    ]
    cad_ready = not missing_stiffeners
    return {
        "recipe_id": recipe_id,
        "status": status,
        "outer_size_mm": {"width": OUTER_WIDTH_MM, "height": OUTER_HEIGHT_MM, "depth": OUTER_DEPTH_MM},
        "door_width_mm": DOOR_WIDTH_MM,
        "door_width_rule": "(outer_width_mm - 2 * 23 - 80) / 2",
        "left_door_column_center_x_mm": -308.5,
        "right_door_column_center_x_mm": 308.5,
        "left_column_units_from_bottom": left_units,
        "right_column_units_from_bottom": right_units,
        "columns": {"L": left_rows, "R": right_rows},
        "driven_boundaries": boundaries,
        "expected_component_counts": {
            "door_modules": door_count,
            "hinge_pin_count": door_count,
            "lock_hook_pad_count": door_count,
            "electric_lock_hook_count": door_count,
            "shelf_modules": expected_between_count,
            "front_frame_crossbars": expected_between_count,
        },
        "cad_source_readiness": {
            "used_slot_units": used_units,
            "ready_for_native_door_chain": cad_ready,
            "missing_stiffener_slot_units": missing_stiffeners,
            "interpretation": (
                "all required door panel and stiffener sources are bound"
                if cad_ready
                else "blocked until missing stiffener source classes are confirmed or a no-stiffener rule is approved"
            ),
        },
        "width_changed_feature_x_mm": {
            "left_hinge_pin_center_x": -258.5,
            "right_hinge_pin_center_x": 258.5,
            "left_electric_lock_hook_center_x": 253.5,
            "right_electric_lock_hook_center_x": -253.5,
            "left_u_hook_pad_center_x": 254.9,
            "right_u_hook_pad_center_x": -254.9,
        },
        "notes": notes,
    }


def build_contract() -> dict[str, Any]:
    source_classes = [build_source_class(slot_units, path) for slot_units, path in SOURCE_CLASS_PATHS.items()]
    return {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "purpose": "Variable-height door row-stack contract for the 1200W x 1917H x 550D W537 rebuild path.",
        "do_not_use_height_mm": [2117.0],
        "fixed_outer_size_mm": {"width": OUTER_WIDTH_MM, "height": OUTER_HEIGHT_MM, "depth": OUTER_DEPTH_MM},
        "door_area_mm": {
            "bottom_y": DOOR_AREA_BOTTOM_Y_MM,
            "top_y": DOOR_AREA_TOP_Y_MM,
            "height": DOOR_AREA_TOP_Y_MM - DOOR_AREA_BOTTOM_Y_MM,
            "bottom_grid_gap": GRID_BOTTOM_GAP_MM,
            "top_grid_gap": GRID_TOP_GAP_MM,
        },
        "unit_grid": {
            "total_vertical_units_per_column": TOTAL_VERTICAL_UNITS,
            "unit_pitch_mm": UNIT_PITCH_MM,
            "visual_gap_mm": VISUAL_GAP_MM,
            "installed_door_height_rule": "slot_units * 152.5 - 7, where 7 is the fixed internal 2 + 3 + 2 gap stack",
            "flat_height_rule": "installed_door_height + 36.4",
            "row_start_y_rule": "30 + 2 + cumulative_previous_slot_units * 152.5",
            "row_end_y_rule": "row_start_y + installed_door_height",
        },
        "vertical_gap_stack_rule": build_vertical_gap_stack_rule(),
        "source_height_classes": source_classes,
        "structure_follow_rules": {
            "shelf_modules": "one shelf per column at each internal row boundary",
            "front_frame_crossbars": "one front-frame crossbar per column at each internal row boundary",
            "shelf_center_y": "lower_door_y_max + 2.0",
            "front_frame_crossbar_center_y": "lower_door_y_max - 10.0",
            "door_hinge_lock_hook_counts": "one complete set per door module",
            "left_right_handedness": "left uses left module; right uses right module with identity transform",
            "width_feature_offsets": "W537 hinge/lock/hook X positions are edge-derived from the W437 source probe, not copied as fixed W437 X values",
        },
        "recipes": [
            build_recipe(
                "width_1200x1917x550_12door_equal_2unit_rows_w537",
                "r8_rebuild_contract_candidate",
                [2, 2, 2, 2, 2, 2],
                [2, 2, 2, 2, 2, 2],
                [
                    "Current mainline: 16029 indoor cabinet, 1200W x 1917H x 550D, 12 doors, W537.",
                    "This is the equal-height 12-door recipe expressed through the same row-stack model used for future unequal-height doors.",
                ],
            ),
            build_recipe(
                "schema_smoke_mixed_height_1_2_3_6_units_not_for_handoff",
                "schema_smoke_only_not_for_engineering_review",
                [1, 2, 3, 6],
                [1, 2, 3, 6],
                [
                    "This recipe proves the data model can express unequal-height doors using verified 1/12 to 6/12 source height classes.",
                    "Do not generate an engineer-review CAD package from this recipe until the actual product configuration is selected.",
                ],
            ),
        ],
        "hard_stop_rules": [
            "No R7-derived geometry may be used as a rebuild source.",
            "Any recipe whose column slot_units do not sum to 12 is blocked.",
            "Any recipe using a source_class outside 1/12..6/12 is blocked until a source part/drawing is bound.",
            "Any output whose root bbox is not 1200W x 1917H x 550D is blocked.",
            "Any output with outboard/floating hardware, missing doors, or missing native component tree is blocked before engineer review.",
            "Any output whose internal row boundary does not preserve 2 + 3 + 2 = 7 mm is blocked.",
        ],
    }


def add_check(checks: list[dict[str, Any]], name: str, ok: bool, actual: Any, expected: Any, severity: str = "error") -> None:
    checks.append({"name": name, "ok": bool(ok), "actual": actual, "expected": expected, "severity": severity})


def validate_contract(contract: dict[str, Any]) -> list[dict[str, Any]]:
    checks: list[dict[str, Any]] = []
    add_check(checks, "product_family", contract.get("product_family") == "16029", contract.get("product_family"), "16029")
    add_check(checks, "outer_height_not_2117", contract["fixed_outer_size_mm"]["height"] != 2117.0, contract["fixed_outer_size_mm"]["height"], "not 2117")
    add_check(
        checks,
        "door_width_w537_formula",
        abs((OUTER_WIDTH_MM - 2 * SIDE_MARGIN_MM - CENTER_GAP_MM) / 2.0 - DOOR_WIDTH_MM) <= TOL_MM,
        DOOR_WIDTH_MM,
        537.0,
    )
    gap_rule = contract.get("vertical_gap_stack_rule", {})
    gap_parts = gap_rule.get("internal_gap_pattern_mm", [])
    add_check(
        checks,
        "vertical_gap_internal_2_3_2_sums_to_7",
        abs(sum(float(part) for part in gap_parts) - VISUAL_GAP_MM) <= TOL_MM,
        gap_parts,
        [2.0, 3.0, 2.0],
    )
    add_check(
        checks,
        "vertical_gap_top_bottom_clearance_2",
        abs(float(gap_rule.get("top_boundary_clearance_mm", 0.0)) - GRID_TOP_GAP_MM) <= TOL_MM
        and abs(float(gap_rule.get("bottom_boundary_clearance_mm", 0.0)) - GRID_BOTTOM_GAP_MM) <= TOL_MM,
        {
            "top": gap_rule.get("top_boundary_clearance_mm"),
            "bottom": gap_rule.get("bottom_boundary_clearance_mm"),
        },
        {"top": 2.0, "bottom": 2.0},
    )
    add_check(
        checks,
        "vertical_gap_separator_thickness_3",
        abs(float(gap_parts[1]) - ROW_SEPARATOR_THICKNESS_MM) <= TOL_MM if len(gap_parts) == 3 else False,
        gap_parts[1] if len(gap_parts) == 3 else gap_parts,
        3.0,
    )

    source_classes = {int(item["slot_units"]): item for item in contract.get("source_height_classes", [])}
    add_check(checks, "source_classes_1_to_6_exist", sorted(source_classes) == [1, 2, 3, 4, 5, 6], sorted(source_classes), [1, 2, 3, 4, 5, 6])
    for slot_units, item in source_classes.items():
        add_check(
            checks,
            f"source_class_{slot_units}_solidworks_panel_exists",
            item["solidworks_source_parts"]["door_panel"] is not None,
            item["solidworks_source_parts"]["door_panel"],
            "existing SolidWorks door panel source",
        )
        add_check(
            checks,
            f"source_class_{slot_units}_solidworks_stiffener_exists_or_is_unconfirmed_1_12",
            item["solidworks_source_parts"]["stiffener"] is not None or slot_units == 1,
            item["solidworks_source_parts"]["stiffener"],
            "existing stiffener source; 1/12 may require no-stiffener confirmation",
            severity="warning" if slot_units == 1 else "error",
        )
        add_check(
            checks,
            f"source_class_{slot_units}_flat_height",
            abs(float(item["baseline_flat_height_mm"]) - door_flat_height_from_units(slot_units)) <= TOL_MM,
            item["baseline_flat_height_mm"],
            rounded(door_flat_height_from_units(slot_units)),
        )
        add_check(
            checks,
            f"source_class_{slot_units}_baseline_flat_width",
            abs(float(item["baseline_flat_width_mm"]) - BASELINE_FLAT_WIDTH_MM) <= TOL_MM,
            item["baseline_flat_width_mm"],
            BASELINE_FLAT_WIDTH_MM,
        )

    for recipe in contract.get("recipes", []):
        rid = recipe["recipe_id"]
        for column_key in ("left_column_units_from_bottom", "right_column_units_from_bottom"):
            units = recipe[column_key]
            add_check(checks, f"{rid}:{column_key}_sum_12", sum(units) == TOTAL_VERTICAL_UNITS, sum(units), TOTAL_VERTICAL_UNITS)
            add_check(
                checks,
                f"{rid}:{column_key}_uses_bound_source_classes",
                all(unit in source_classes for unit in units),
                units,
                "all units in 1..6 source classes",
            )

        all_rows = recipe["columns"]["L"] + recipe["columns"]["R"]
        for row in all_rows:
            slot_units = int(row["slot_units"])
            add_check(
                checks,
                f"{rid}:{row['column']}{row['row_index_from_bottom']}:height_from_units",
                abs(float(row["installed_door_height_mm"]) - door_height_from_units(slot_units)) <= TOL_MM,
                row["installed_door_height_mm"],
                rounded(door_height_from_units(slot_units)),
            )

        for column, rows in recipe["columns"].items():
            first = rows[0]
            last = rows[-1]
            add_check(
                checks,
                f"{rid}:{column}:stack_bottom_top",
                abs(float(first["y_min_mm"]) - (DOOR_AREA_BOTTOM_Y_MM + GRID_BOTTOM_GAP_MM)) <= TOL_MM
                and abs(float(last["y_max_mm"]) - (DOOR_AREA_TOP_Y_MM - GRID_TOP_GAP_MM)) <= TOL_MM,
                {"first_y_min": first["y_min_mm"], "last_y_max": last["y_max_mm"]},
                {"first_y_min": 32.0, "last_y_max": 1855.0},
            )
            for lower_row, upper_row in zip(rows, rows[1:]):
                actual_gap = float(upper_row["y_min_mm"]) - float(lower_row["y_max_mm"])
                add_check(
                    checks,
                    f"{rid}:{column}:row_{lower_row['row_index_from_bottom']}_internal_gap_2_3_2",
                    abs(actual_gap - VISUAL_GAP_MM) <= TOL_MM,
                    rounded(actual_gap),
                    "2 + 3 + 2 = 7",
                )

        expected_boundary_count = (len(recipe["columns"]["L"]) - 1) + (len(recipe["columns"]["R"]) - 1)
        counts = recipe["expected_component_counts"]
        readiness = recipe.get("cad_source_readiness", {})
        add_check(
            checks,
            f"{rid}:cad_source_ready_flag_matches_missing_stiffeners",
            readiness.get("ready_for_native_door_chain") == (len(readiness.get("missing_stiffener_slot_units", [])) == 0),
            readiness,
            "ready only when no missing stiffener source classes",
        )
        add_check(checks, f"{rid}:door_module_count", counts["door_modules"] == len(all_rows), counts["door_modules"], len(all_rows))
        add_check(checks, f"{rid}:shelf_boundary_count", counts["shelf_modules"] == expected_boundary_count, counts["shelf_modules"], expected_boundary_count)
        add_check(
            checks,
            f"{rid}:front_frame_boundary_count",
            counts["front_frame_crossbars"] == expected_boundary_count,
            counts["front_frame_crossbars"],
            expected_boundary_count,
        )

        if rid == "width_1200x1917x550_12door_equal_2unit_rows_w537":
            add_check(
                checks,
                f"{rid}:cad_source_ready_for_r8",
                readiness.get("ready_for_native_door_chain") is True,
                readiness,
                "2/12 door panel and stiffener sources are bound",
            )
            add_check(checks, f"{rid}:mainline_door_count_12", counts["door_modules"] == 12, counts["door_modules"], 12)
            add_check(
                checks,
                f"{rid}:mainline_all_2unit_rows",
                recipe["left_column_units_from_bottom"] == [2, 2, 2, 2, 2, 2]
                and recipe["right_column_units_from_bottom"] == [2, 2, 2, 2, 2, 2],
                {"L": recipe["left_column_units_from_bottom"], "R": recipe["right_column_units_from_bottom"]},
                "six 2/12 rows per column",
            )
            add_check(checks, f"{rid}:mainline_shelf_count_10", counts["shelf_modules"] == 10, counts["shelf_modules"], 10)
            add_check(checks, f"{rid}:mainline_crossbar_count_10", counts["front_frame_crossbars"] == 10, counts["front_frame_crossbars"], 10)
        if rid == "schema_smoke_mixed_height_1_2_3_6_units_not_for_handoff":
            add_check(
                checks,
                f"{rid}:schema_smoke_not_ready_for_native_handoff",
                readiness.get("ready_for_native_door_chain") is False,
                readiness,
                "blocked because 1/12 stiffener/no-stiffener rule is unconfirmed",
                severity="warning",
            )

    return checks


def write_markdown(contract: dict[str, Any]) -> None:
    lines = [
        "# 16029 Variable Door Stack Contract",
        "",
        f"- Generated at: `{contract['generated_at']}`",
        "- Mainline: `1200W x 1917H x 550D / 12 doors / W537`",
        "- Explicitly excluded: `2117H`",
        "",
        "## Source Height Classes",
        "",
        "| Class | Slot units | Installed height | Baseline flat height | Source | Quality |",
        "| --- | ---: | ---: | ---: | --- | --- |",
    ]
    for item in contract["source_height_classes"]:
        source_parts = item["solidworks_source_parts"]
        lines.append(
            f"| {item['source_class_id']} | {item['slot_units']} | {item['expected_installed_door_height_mm']} | "
            f"{item['baseline_flat_height_mm']} | {item['source_file_name']} | {item['quality_status']} |"
        )
    lines.extend(
        [
            "",
            "## SolidWorks Source Readiness",
            "",
            "| Class | Door panel source | Stiffener source |",
            "| --- | --- | --- |",
        ]
    )
    for item in contract["source_height_classes"]:
        source_parts = item["solidworks_source_parts"]
        lines.append(
            f"| {item['source_class_id']} | {source_parts['door_panel'] or 'MISSING'} | "
            f"{source_parts['stiffener'] or source_parts['stiffener_status']} |"
        )
    gap_rule = contract["vertical_gap_stack_rule"]
    lines.extend(
        [
            "",
            "## Row-Stack Rule",
            "",
            "- Each column owns a bottom-to-top `slot_units` list.",
            "- `sum(slot_units)` must be `12` per column.",
            "- Installed door height is `slot_units * 152.5 - 7`; the `7` is the fixed internal `2 + 3 + 2` gap stack.",
            f"- Top boundary clearance is `{gap_rule['top_boundary_clearance_mm']}` mm.",
            f"- Internal row boundary is `{gap_rule['internal_gap_pattern_mm'][0]} + {gap_rule['internal_gap_pattern_mm'][1]} + {gap_rule['internal_gap_pattern_mm'][2]} = {gap_rule['internal_gap_total_mm']}` mm.",
            f"- Bottom boundary clearance is `{gap_rule['bottom_boundary_clearance_mm']}` mm.",
            "- Internal shelf and front-frame crossbar positions are derived from each lower door boundary.",
            "- Locks, hooks and hinge pins are one set per door, with W537 X offsets derived from the source edge offsets.",
            "",
            "## Vertical Gap Evidence",
            "",
        ]
    )
    for evidence in gap_rule["source_evidence"]:
        lines.append(f"- `{evidence['path']}`: {evidence['evidence']}")
    lines.extend(
        [
            "",
            "## Recipes",
            "",
            "| Recipe | Status | L units | R units | Doors | Shelves | Crossbars |",
            "| --- | --- | --- | --- | ---: | ---: | ---: |",
        ]
    )
    for recipe in contract["recipes"]:
        counts = recipe["expected_component_counts"]
        lines.append(
            f"| {recipe['recipe_id']} | {recipe['status']} | {recipe['left_column_units_from_bottom']} | "
            f"{recipe['right_column_units_from_bottom']} | {counts['door_modules']} | {counts['shelf_modules']} | "
            f"{counts['front_frame_crossbars']} |"
        )
    lines.extend(["", "## Hard Stops", ""])
    for rule in contract["hard_stop_rules"]:
        lines.append(f"- {rule}")
    lines.append("")
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def write_validation(contract: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    failed = [check for check in checks if not check["ok"] and check.get("severity") == "error"]
    payload = {
        "generated_at": now_local_iso(),
        "contract": str(OUTPUT_JSON_PATH),
        "status": "PASS" if not failed else "FAIL",
        "summary": {"total": len(checks), "failed": len(failed)},
        "checks": checks,
    }
    OUTPUT_VALIDATION_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    with OUTPUT_VALIDATION_CSV_PATH.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["name", "ok", "actual", "expected", "severity"])
        writer.writeheader()
        for check in checks:
            writer.writerow(check)

    lines = [
        "# 16029 Variable Door Stack Contract Validation",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Status: `{payload['status']}`",
        f"- Checks: `{payload['summary']['total']}`",
        f"- Failed: `{payload['summary']['failed']}`",
        "",
        "| Check | Result | Actual | Expected |",
        "| --- | --- | --- | --- |",
    ]
    for check in checks:
        result = "PASS" if check["ok"] else "FAIL"
        lines.append(f"| {check['name']} | {result} | {check['actual']} | {check['expected']} |")
    lines.append("")
    OUTPUT_VALIDATION_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    contract = build_contract()
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(contract, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(contract)
    checks = validate_contract(contract)
    write_validation(contract, checks)
    failed = [check for check in checks if not check["ok"] and check.get("severity") == "error"]
    print(
        json.dumps(
            {
                "status": "PASS" if not failed else "FAIL",
                "contract": str(OUTPUT_JSON_PATH),
                "validation": str(OUTPUT_VALIDATION_JSON_PATH),
                "checks": len(checks),
                "failed": len(failed),
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
