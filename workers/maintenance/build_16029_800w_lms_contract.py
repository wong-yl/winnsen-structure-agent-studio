from __future__ import annotations

import csv
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]

VARIANT_TOKEN = os.environ.get("LOCKER_800W_VARIANT_TOKEN", "lms")
CONTRACT_BASENAME = os.environ.get(
    "LOCKER_800W_CONTRACT_BASENAME",
    f"locker_16029_800w_{VARIANT_TOKEN}_rule_review_contract",
)
OUT_JSON = ROOT_DIR / "data" / f"{CONTRACT_BASENAME}.json"
OUT_MD = ROOT_DIR / "data" / f"{CONTRACT_BASENAME}.md"
OUT_VALIDATION_JSON = ROOT_DIR / "data" / f"{CONTRACT_BASENAME}_validation.json"
OUT_VALIDATION_MD = ROOT_DIR / "data" / f"{CONTRACT_BASENAME}_validation.md"
OUT_VALIDATION_CSV = ROOT_DIR / "data" / f"{CONTRACT_BASENAME}_validation.csv"

SOURCE_ROOT = Path("C:/sw16029_standard_ascii")

OUTER_WIDTH_MM = 800.0
OUTER_HEIGHT_MM = 1917.0
OUTER_DEPTH_MM = 550.0
SIDE_MARGIN_MM = 23.0
CENTER_GAP_MM = 80.0
DOOR_AREA_BOTTOM_Y_MM = 30.0
DOOR_AREA_TOP_Y_MM = 1857.0
GRID_BOTTOM_GAP_MM = 2.0
GRID_TOP_GAP_MM = 2.0
UNIT_PITCH_MM = 152.5
VISUAL_GAP_MM = 7.0
GAP_STACK_MM = [2.0, 3.0, 2.0]
TOTAL_VERTICAL_UNITS = 12
DOOR_FLAT_HEIGHT_EXTRA_MM = 36.4
TOL_MM = 0.001

ROW_UNITS_BOTTOM_TO_TOP = [
    int(part)
    for part in os.environ.get("LOCKER_800W_ROW_UNITS_BOTTOM_TO_TOP", "6,4,2").split(",")
    if part.strip()
]


def label_for_units(slot_units: int) -> str:
    return {6: "large", 4: "medium", 2: "small"}.get(slot_units, f"{slot_units}/12")


def row_order_label(units: list[int]) -> str:
    return " / ".join(label_for_units(unit) for unit in units)


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def rounded(value: float) -> float:
    return round(float(value), 3)


def door_width() -> float:
    return (OUTER_WIDTH_MM - 2 * SIDE_MARGIN_MM - CENTER_GAP_MM) / 2.0


def column_center_abs_x() -> float:
    return door_width() / 2.0 + CENTER_GAP_MM / 2.0


def door_height(slot_units: int) -> float:
    return slot_units * UNIT_PITCH_MM - VISUAL_GAP_MM


def source_parts(slot_units: int) -> dict[str, Any]:
    panel = SOURCE_ROOT / f"door_panel_{slot_units}_12.SLDPRT"
    assembly = SOURCE_ROOT / f"door_{slot_units}_12_assembly.SLDASM"
    rib = SOURCE_ROOT / f"rib_{slot_units}_12.SLDPRT"
    return {
        "door_panel": str(panel) if panel.exists() else None,
        "door_assembly": str(assembly) if assembly.exists() else None,
        "stiffener": str(rib) if rib.exists() else None,
    }


def rows_for_column(column: str, units: list[int]) -> list[dict[str, Any]]:
    cursor_units = 0
    rows: list[dict[str, Any]] = []
    x_center = -column_center_abs_x() if column == "L" else column_center_abs_x()
    for index, slot_units in enumerate(units, start=1):
        y_min = DOOR_AREA_BOTTOM_Y_MM + GRID_BOTTOM_GAP_MM + cursor_units * UNIT_PITCH_MM
        height = door_height(slot_units)
        y_max = y_min + height
        rows.append(
            {
                "column": column,
                "row_index_from_bottom": index,
                "size_label": {6: "large", 4: "medium", 2: "small"}.get(slot_units, f"{slot_units}/12"),
                "slot_units": slot_units,
                "source_class_id": f"{slot_units}/12",
                "x_center_mm": rounded(x_center),
                "y_min_mm": rounded(y_min),
                "y_max_mm": rounded(y_max),
                "center_y_mm": rounded((y_min + y_max) / 2.0),
                "installed_door_height_mm": rounded(height),
                "door_width_mm": rounded(door_width()),
                "baseline_flat_height_mm": rounded(height + DOOR_FLAT_HEIGHT_EXTRA_MM),
            }
        )
        cursor_units += slot_units
    return rows


def boundaries(column: str, rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    out: list[dict[str, Any]] = []
    for row in rows[:-1]:
        y_max = float(row["y_max_mm"])
        out.append(
            {
                "column": column,
                "after_row_index_from_bottom": row["row_index_from_bottom"],
                "lower_door_y_max_mm": rounded(y_max),
                "row_separator_y_min_mm": rounded(y_max + GAP_STACK_MM[0]),
                "row_separator_y_max_mm": rounded(y_max + GAP_STACK_MM[0] + GAP_STACK_MM[1]),
                "next_door_y_min_mm": rounded(y_max + VISUAL_GAP_MM),
                "gap_stack_mm": GAP_STACK_MM,
                "shelf_rule_y_mm": rounded(y_max + 2.0),
                "front_frame_crossbar_rule_y_mm": rounded(y_max - 10.0),
            }
        )
    return out


def build_contract() -> dict[str, Any]:
    left_rows = rows_for_column("L", ROW_UNITS_BOTTOM_TO_TOP)
    right_rows = rows_for_column("R", ROW_UNITS_BOTTOM_TO_TOP)
    used_units = sorted(set(ROW_UNITS_BOTTOM_TO_TOP))
    sequence_text = row_order_label(ROW_UNITS_BOTTOM_TO_TOP)
    return {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "status": "r8_rule_review_prototype_not_production_release",
        "purpose": "800W large/medium/small variable-height door review model for engineer validation.",
        "assumptions": [
            "Height remains 1917mm and depth remains 550mm.",
            f"Each column uses bottom-to-top rows: {sequence_text}.",
            "Door width follows the same side-margin and center-gap formula as the 1200W W537 candidate.",
        ],
        "fixed_outer_size_mm": {"width": OUTER_WIDTH_MM, "height": OUTER_HEIGHT_MM, "depth": OUTER_DEPTH_MM},
        "width_rule": {
            "side_margin_mm": SIDE_MARGIN_MM,
            "center_gap_mm": CENTER_GAP_MM,
            "door_width_rule": "(outer_width - 2 * 23 - 80) / 2",
            "door_width_mm": rounded(door_width()),
            "left_column_center_x_mm": rounded(-column_center_abs_x()),
            "right_column_center_x_mm": rounded(column_center_abs_x()),
        },
        "vertical_rule": {
            "total_vertical_units_per_column": TOTAL_VERTICAL_UNITS,
            "unit_pitch_mm": UNIT_PITCH_MM,
            "row_units_bottom_to_top": ROW_UNITS_BOTTOM_TO_TOP,
            "door_height_rule": "slot_units * 152.5 - 7",
            "top_boundary_clearance_mm": GRID_TOP_GAP_MM,
            "internal_gap_stack_mm": GAP_STACK_MM,
            "internal_gap_total_mm": VISUAL_GAP_MM,
            "bottom_boundary_clearance_mm": GRID_BOTTOM_GAP_MM,
            "row_start_y_rule": "30 + 2 + cumulative_previous_slot_units * 152.5",
        },
        "source_readiness": {
            f"{unit}/12": source_parts(unit) for unit in used_units
        },
        "columns": {"L": left_rows, "R": right_rows},
        "driven_boundaries": boundaries("L", left_rows) + boundaries("R", right_rows),
        "expected_component_counts": {
            "door_modules": len(left_rows) + len(right_rows),
            "row_separators": (len(left_rows) - 1) + (len(right_rows) - 1),
            "shelf_modules": (len(left_rows) - 1) + (len(right_rows) - 1),
            "front_frame_crossbars": (len(left_rows) - 1) + (len(right_rows) - 1),
            "hinge_sets": len(left_rows) + len(right_rows),
            "lock_hook_sets": len(left_rows) + len(right_rows),
        },
        "hard_stop_rules": [
            "Do not use 2117H geometry.",
            "Do not treat this as production release geometry; it is a rule-review prototype.",
            "Every column must sum to 12 vertical units.",
            "Top and bottom clearances must remain 2mm.",
            "Every internal row boundary must preserve 2 + 3 + 2 = 7mm.",
            "Root envelope must remain 800W x 1917H x 550D.",
        ],
    }


def add_check(checks: list[dict[str, Any]], name: str, ok: bool, actual: Any, expected: Any, severity: str = "error") -> None:
    checks.append({"name": name, "ok": bool(ok), "actual": actual, "expected": expected, "severity": severity})


def validate(contract: dict[str, Any]) -> list[dict[str, Any]]:
    checks: list[dict[str, Any]] = []
    width_rule = contract["width_rule"]
    vertical_rule = contract["vertical_rule"]
    counts = contract["expected_component_counts"]
    expected_order = [label_for_units(int(unit)) for unit in vertical_rule["row_units_bottom_to_top"]]
    add_check(
        checks,
        "outer_size_800_1917_550",
        contract["fixed_outer_size_mm"] == {"width": 800.0, "height": 1917.0, "depth": 550.0},
        contract["fixed_outer_size_mm"],
        {"width": 800.0, "height": 1917.0, "depth": 550.0},
    )
    add_check(checks, "door_width_w337_formula", abs(float(width_rule["door_width_mm"]) - 337.0) <= TOL_MM, width_rule["door_width_mm"], 337.0)
    add_check(checks, "column_centers_w337", width_rule["left_column_center_x_mm"] == -208.5 and width_rule["right_column_center_x_mm"] == 208.5, width_rule, {"L": -208.5, "R": 208.5})
    add_check(checks, "row_units_sum_12", sum(vertical_rule["row_units_bottom_to_top"]) == 12, vertical_rule["row_units_bottom_to_top"], "sum 12")
    add_check(checks, "internal_gap_stack_2_3_2", vertical_rule["internal_gap_stack_mm"] == [2.0, 3.0, 2.0], vertical_rule["internal_gap_stack_mm"], [2.0, 3.0, 2.0])
    add_check(checks, "internal_gap_total_7", abs(sum(vertical_rule["internal_gap_stack_mm"]) - VISUAL_GAP_MM) <= TOL_MM, sum(vertical_rule["internal_gap_stack_mm"]), 7.0)
    add_check(checks, "top_bottom_clearance_2", vertical_rule["top_boundary_clearance_mm"] == 2.0 and vertical_rule["bottom_boundary_clearance_mm"] == 2.0, vertical_rule, "top=2,bottom=2")
    for source_class, parts in contract["source_readiness"].items():
        add_check(checks, f"source_{source_class}_door_panel_exists", bool(parts["door_panel"]), parts["door_panel"], "existing panel source")
        add_check(checks, f"source_{source_class}_stiffener_exists", bool(parts["stiffener"]), parts["stiffener"], "existing stiffener source")
    for column, rows in contract["columns"].items():
        add_check(checks, f"{column}_row_count_3", len(rows) == 3, len(rows), 3)
        add_check(
            checks,
            f"{column}_row_order_{'_'.join(expected_order)}",
            [row["size_label"] for row in rows] == expected_order,
            [row["size_label"] for row in rows],
            expected_order,
        )
        add_check(checks, f"{column}_bottom_top_stack", rows[0]["y_min_mm"] == 32.0 and rows[-1]["y_max_mm"] == 1855.0, {"first_y_min": rows[0]["y_min_mm"], "last_y_max": rows[-1]["y_max_mm"]}, {"first_y_min": 32.0, "last_y_max": 1855.0})
        for lower, upper in zip(rows, rows[1:]):
            add_check(checks, f"{column}_row_{lower['row_index_from_bottom']}_gap_7", abs(float(upper["y_min_mm"]) - float(lower["y_max_mm"]) - 7.0) <= TOL_MM, rounded(float(upper["y_min_mm"]) - float(lower["y_max_mm"])), 7.0)
    add_check(checks, "door_count_6", counts["door_modules"] == 6, counts["door_modules"], 6)
    add_check(checks, "boundary_count_4", counts["row_separators"] == 4, counts["row_separators"], 4)
    return checks


def write_outputs(contract: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(json.dumps(contract, ensure_ascii=False, indent=2), encoding="utf-8")
    sequence_text = row_order_label(contract["vertical_rule"]["row_units_bottom_to_top"])

    lines = [
        f"# 16029 800W {sequence_text} Rule-Review Contract",
        "",
        f"- Generated at: `{contract['generated_at']}`",
        "- Scope: `800W x 1917H x 550D`",
        f"- Door layout: each column bottom-to-top `{sequence_text}`",
        "- Door width: `W337`, from `(800 - 46 - 80) / 2`",
        "- Status: rule-review prototype, not production release",
        "",
        "## Rows",
        "",
        "| Column | Row from bottom | Size | Units | Y min | Y max | Center Y | Door W | Door H |",
        "| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for column in ("L", "R"):
        for row in contract["columns"][column]:
            lines.append(
                f"| {column} | {row['row_index_from_bottom']} | {row['size_label']} | {row['slot_units']} | "
                f"{row['y_min_mm']} | {row['y_max_mm']} | {row['center_y_mm']} | {row['door_width_mm']} | {row['installed_door_height_mm']} |"
            )
    lines.extend(["", "## Hard Stops", ""])
    for rule in contract["hard_stop_rules"]:
        lines.append(f"- {rule}")
    OUT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")

    failed = [check for check in checks if not check["ok"] and check["severity"] == "error"]
    payload = {
        "generated_at": now_local_iso(),
        "contract": str(OUT_JSON),
        "status": "PASS" if not failed else "FAIL",
        "summary": {"total": len(checks), "failed": len(failed)},
        "checks": checks,
    }
    OUT_VALIDATION_JSON.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    with OUT_VALIDATION_CSV.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["name", "ok", "actual", "expected", "severity"])
        writer.writeheader()
        for check in checks:
            writer.writerow(check)
    md = [
        f"# 16029 800W {sequence_text} Contract Validation",
        "",
        f"- Status: `{payload['status']}`",
        f"- Checks: `{payload['summary']['total']}`",
        f"- Failed: `{payload['summary']['failed']}`",
        "",
        "| Check | Result | Actual | Expected |",
        "| --- | --- | --- | --- |",
    ]
    for check in checks:
        md.append(f"| {check['name']} | {'PASS' if check['ok'] else 'FAIL'} | `{check['actual']}` | `{check['expected']}` |")
    OUT_VALIDATION_MD.write_text("\n".join(md) + "\n", encoding="utf-8")


def main() -> int:
    contract = build_contract()
    checks = validate(contract)
    write_outputs(contract, checks)
    failed = [check for check in checks if not check["ok"] and check["severity"] == "error"]
    print(json.dumps({"status": "PASS" if not failed else "FAIL", "checks": len(checks), "failed": len(failed), "contract": str(OUT_JSON)}, ensure_ascii=False))
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
