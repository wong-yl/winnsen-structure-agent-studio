from __future__ import annotations

import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
RULE_EVIDENCE_PATH = Path(
    os.getenv("STUDIO_16029_SHEETMETAL_RULE_EVIDENCE_JSON", ROOT_DIR / "data" / "sheetmetal_rule_evidence_16029.json")
)
OUTPUT_JSON_PATH = Path(os.getenv("STUDIO_16029_VARIANT_RULE_PACKET_JSON", ROOT_DIR / "data" / "locker_16029_variant_rule_packet.json"))
OUTPUT_MARKDOWN_PATH = Path(os.getenv("STUDIO_16029_VARIANT_RULE_PACKET_MD", ROOT_DIR / "data" / "locker_16029_variant_rule_packet.md"))

SUPPORTED_VARIANT_DOOR_COUNTS = [10, 12, 14]
SOLIDWORKS_TEMPLATE_DOOR_COUNTS = [10, 12]
FREECAD_RULE_VALIDATION_DOOR_COUNTS = [10, 12, 14]

CABINET_RULES = {
    "width_mm": 1000.0,
    "height_mm": 1917.0,
    "depth_mm": 550.0,
    "door_area_height_mm": 1827.0,
    "door_width_mm": 437.0,
    "visual_gap_mm": 7.0,
    "grid_edge_gap_mm": 2.0,
    "door_flat_height_extra_mm": 36.4,
    "door_flat_width_extra_mm": 38.4,
    "door_unit_pitch_mm": 152.5,
}


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def candidate_by_id(evidence: dict[str, Any], candidate_id: str) -> dict[str, Any]:
    for candidate in evidence.get("rule_candidates", []):
        if isinstance(candidate, dict) and candidate.get("id") == candidate_id:
            return candidate
    return {}


def first_formula(evidence: dict[str, Any], formula_id: str) -> dict[str, Any]:
    for formula in evidence.get("formulas", []):
        if isinstance(formula, dict) and formula.get("id") == formula_id:
            return formula
    return {}


def equal_row_layout(door_count: int) -> dict[str, Any]:
    rows_per_column = door_count // 2
    door_height = (
        CABINET_RULES["door_area_height_mm"]
        - CABINET_RULES["grid_edge_gap_mm"] * 2
        - (rows_per_column - 1) * CABINET_RULES["visual_gap_mm"]
    ) / rows_per_column
    unit = (door_height + CABINET_RULES["visual_gap_mm"]) / CABINET_RULES["door_unit_pitch_mm"]
    rounded_unit = round(unit)
    exact_unit = rounded_unit if 1 <= rounded_unit <= 6 and abs(unit - rounded_unit) < 0.01 else None
    return {
        "rows_per_column": rows_per_column,
        "door_height_mm": round(door_height, 3),
        "door_pitch_mm": round(door_height + CABINET_RULES["visual_gap_mm"], 3),
        "exact_12_unit": exact_unit,
        "layout_descriptor": f"{rows_per_column} rows/column"
        + (f", exact {exact_unit}/12 door source" if exact_unit else ", equal-row derived height"),
        "row_labels": [f"{index + 1}: {door_height:.3f}mm" for index in range(rows_per_column)],
    }


def door_source_mode(door_count: int, layout: dict[str, Any]) -> str:
    if door_count == 10:
        return "solidworks_10door_practice_clone_or_freecad_equal_row_reference"
    if layout.get("exact_12_unit"):
        return f"solidworks_exact_{layout['exact_12_unit']}_12_panel_source"
    if door_count == 14:
        return "freecad_rule_validation_with_derived_14door_panel_candidate"
    return "freecad_parametric_reference"


def build_variant_rule(door_count: int, evidence: dict[str, Any]) -> dict[str, Any]:
    layout = equal_row_layout(door_count)
    rows_per_column = layout["rows_per_column"]
    shelf_count = (rows_per_column - 1) * 2
    door_height = float(layout["door_height_mm"])
    door_width = CABINET_RULES["door_width_mm"]
    flat_height = door_height + CABINET_RULES["door_flat_height_extra_mm"]
    flat_width = door_width + CABINET_RULES["door_flat_width_extra_mm"]
    return {
        "door_count": door_count,
        "output_level": "engineering_reference",
        "cad_routes": {
            "freecad": {
                "enabled": door_count in FREECAD_RULE_VALIDATION_DOOR_COUNTS,
                "status": "rule_validation_mainline",
                "reason": "FreeCAD is the low-cost path for 10/12/14 door-count rule validation before SolidWorks handoff.",
            },
            "solidworks": {
                "enabled": door_count in SOLIDWORKS_TEMPLATE_DOOR_COUNTS,
                "status": "template_clone_only" if door_count in SOLIDWORKS_TEMPLATE_DOOR_COUNTS else "blocked_until_transform_route",
                "reason": (
                    "SolidWorks template clone is available for 10/12 only."
                    if door_count in SOLIDWORKS_TEMPLATE_DOOR_COUNTS
                    else "SolidWorks 14-door direct assembly remains blocked until transform/mate route is stable."
                ),
            },
        },
        "layout": layout,
        "derived_dimensions_mm": {
            "cabinet_width": CABINET_RULES["width_mm"],
            "cabinet_height": CABINET_RULES["height_mm"],
            "cabinet_depth": CABINET_RULES["depth_mm"],
            "door_width": door_width,
            "door_height": round(door_height, 3),
            "door_flat_width_estimate": round(flat_width, 3),
            "door_flat_height_estimate": round(flat_height, 3),
        },
        "expected_component_counts": {
            "door_modules": door_count,
            "lock_holes": door_count,
            "lock_hooks": door_count,
            "shelves": shelf_count,
            "door_frame_horizontal_dividers": shelf_count,
            "cabinet_vertical_dividers": 2,
            "door_frame_vertical_dividers": 2,
        },
        "source_mode": door_source_mode(door_count, layout),
        "quality_gates": [
            "bbox X must remain 1000mm for the standard-width route",
            "door_modules, lock_holes and lock_hooks must equal door_count",
            "shelves and door_frame_horizontal_dividers must equal (rows_per_column - 1) x 2",
            "invalid_shape_count must be 0 before promoting to SolidWorks handoff",
        ],
        "evidence_refs": {
            "door_flat_formula": first_formula(evidence, "16029-door-panel-1-to-6-flat-height-series").get("id", ""),
            "shelf": candidate_by_id(evidence, "16029-cabinet-shelf-flat").get("id", ""),
            "vertical_divider": candidate_by_id(evidence, "16029-cabinet-vertical-divider-flat").get("id", ""),
            "door_frame_horizontal": candidate_by_id(evidence, "16029-door-frame-horizontal-divider-flat").get("id", ""),
            "door_frame_vertical": candidate_by_id(evidence, "16029-door-frame-vertical-divider-flat").get("id", ""),
        },
    }


def build_payload() -> dict[str, Any]:
    evidence = read_json(RULE_EVIDENCE_PATH)
    return {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "purpose": "Door-count rule packet for standard 1000W x 1917H x 550D locker variants.",
        "source_evidence_json": str(RULE_EVIDENCE_PATH),
        "cabinet_rules": CABINET_RULES,
        "supported_counts": {
            "freecad_rule_validation": FREECAD_RULE_VALIDATION_DOOR_COUNTS,
            "solidworks_template_clone": SOLIDWORKS_TEMPLATE_DOOR_COUNTS,
        },
        "variants": [build_variant_rule(door_count, evidence) for door_count in SUPPORTED_VARIANT_DOOR_COUNTS],
        "notes": [
            "This packet is a generator input, not a production drawing release.",
            "10/12/14 are the convergence targets; the system is not generating all door-count inventory variants.",
            "FreeCAD is used first for geometry/rule validation; SolidWorks remains the current engineering handoff route.",
        ],
        "output_paths": {"json": str(OUTPUT_JSON_PATH), "markdown": str(OUTPUT_MARKDOWN_PATH)},
    }


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 10/12/14 门规则包",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Source evidence: `{payload['source_evidence_json']}`",
        f"- FreeCAD counts: `{payload['supported_counts']['freecad_rule_validation']}`",
        f"- SolidWorks counts: `{payload['supported_counts']['solidworks_template_clone']}`",
        "",
        "| 门数 | 每列排数 | 门高 | 门展开估算 | 门模块 | 层板 | 门框横隔板 | FreeCAD | SolidWorks |",
        "| ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | --- |",
    ]
    for variant in payload["variants"]:
        dims = variant["derived_dimensions_mm"]
        counts = variant["expected_component_counts"]
        lines.append(
            "| "
            + " | ".join(
                [
                    str(variant["door_count"]),
                    str(variant["layout"]["rows_per_column"]),
                    f"{dims['door_height']} mm",
                    f"{dims['door_flat_width_estimate']} x {dims['door_flat_height_estimate']} mm",
                    str(counts["door_modules"]),
                    str(counts["shelves"]),
                    str(counts["door_frame_horizontal_dividers"]),
                    "enabled" if variant["cad_routes"]["freecad"]["enabled"] else "blocked",
                    "enabled" if variant["cad_routes"]["solidworks"]["enabled"] else "blocked",
                ]
            )
            + " |"
        )
    lines.extend(["", "## 质量门槛", ""])
    for gate in payload["variants"][0]["quality_gates"]:
        lines.append(f"- {gate}")
    lines.extend(["", "## 说明", ""])
    for note in payload["notes"]:
        lines.append(f"- {note}")
    lines.append("")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    payload = build_payload()
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(payload)
    print(json.dumps({"status": "ok", "json": str(OUTPUT_JSON_PATH), "variants": len(payload["variants"])}, ensure_ascii=False))


if __name__ == "__main__":
    main()
