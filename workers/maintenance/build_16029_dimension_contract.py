from __future__ import annotations

import csv
import json
import math
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
VERIFIED_RULE_PACKET_PATH = Path(
    os.getenv(
        "STUDIO_16029_VERIFIED_RULE_PACKET_JSON",
        ROOT_DIR / "data" / "locker_16029_verified_rule_packet.json",
    )
)
CANDIDATES_PATH = os.getenv("STUDIO_16029_DIMENSION_CANDIDATES_JSON")
OUTPUT_JSON_PATH = Path(
    os.getenv("STUDIO_16029_DIMENSION_CONTRACT_JSON", ROOT_DIR / "data" / "locker_16029_dimension_contract.json")
)
OUTPUT_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_DIMENSION_CONTRACT_MD", ROOT_DIR / "data" / "locker_16029_dimension_contract.md")
)
OUTPUT_CSV_PATH = Path(
    os.getenv("STUDIO_16029_DIMENSION_CONTRACT_CSV", ROOT_DIR / "data" / "locker_16029_dimension_contract.csv")
)
WIDTH_CANDIDATE_DOOR_CHAIN_GATE_PATH = Path(
    os.getenv(
        "STUDIO_16029_WIDTH_CANDIDATE_DOOR_CHAIN_GATE_JSON",
        ROOT_DIR / "data" / "locker_16029_width_candidate_door_chain_gate.json",
    )
)
WIDTH_CANDIDATE_SKELETON_SEED_GATE_PATH = Path(
    os.getenv(
        "STUDIO_16029_WIDTH_CANDIDATE_SKELETON_SEED_GATE_JSON",
        ROOT_DIR / "data" / "locker_16029_width_candidate_skeleton_seed_gate.json",
    )
)
WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_PATH = Path(
    os.getenv(
        "STUDIO_16029_WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_JSON",
        ROOT_DIR / "data" / "locker_16029_width_candidate_geometry_review_bundle_gate.json",
    )
)
CURRENT_PHASE_FIXED_HEIGHT_MM = 1917.0
CURRENT_PHASE_FIXED_DEPTH_MM = 550.0
BASELINE_DOOR_COLUMN_CENTER_ABS_X_MM = 258.5
BASELINE_SIDE_MARGIN_MM = 23.0
BASELINE_CENTER_GAP_MM = 80.0
BASELINE_DOOR_ARRAY_COLUMNS_X_MM = {"L": -258.5, "R": 258.5}
DOOR_ARRAY_RIGHT_COLUMN_ROUTE = "right_handed_module_identity_transform"
RIGHT_COLUMN_IDENTITY_ROTATION = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
LEFT_RIGHT_EXPORTED_DOOR_BBOX_Y_DELTA_MAX_MM = 0.1

FEATURE_LOCAL_PLACEMENT_X_BY_COLUMN_MM = {
    "hinge_pin": {"L": -208.5, "R": 208.5},
    "u_hook_pad_placement_tx": {"L": 206.3, "R": -203.5},
    "electric_lock_hook": {"L": 203.5, "R": -203.5},
}
FEATURE_LOCAL_GATE_X_BY_COLUMN_MM = {
    "hinge_pin": {"L": -208.5, "R": 208.5},
    "u_hook_pad_bbox_center": {"L": 204.9, "R": -204.9},
    "electric_lock_hook": {"L": 203.5, "R": -203.5},
}

DEFAULT_CANDIDATES = [
    {
        "candidate_id": "baseline_1000x1917x550_10door",
        "label": "Current verified 10-door reference",
        "outer_width_mm": 1000.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 10,
        "intent": "verified_reference",
    },
    {
        "candidate_id": "baseline_1000x1917x550_12door",
        "label": "Current verified 12-door reference",
        "outer_width_mm": 1000.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 12,
        "intent": "verified_reference",
    },
    {
        "candidate_id": "baseline_1000x1917x550_14door",
        "label": "Current verified 14-door reference",
        "outer_width_mm": 1000.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 14,
        "intent": "verified_reference",
    },
    {
        "candidate_id": "door_count_1000x1917x550_8door",
        "label": "Planning example: fewer taller doors, same outer size",
        "outer_width_mm": 1000.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 8,
        "intent": "door_count_formula_candidate",
    },
    {
        "candidate_id": "door_count_1000x1917x550_16door",
        "label": "Planning example: more shorter doors, same outer size",
        "outer_width_mm": 1000.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 16,
        "intent": "door_count_formula_candidate",
    },
    {
        "candidate_id": "width_1200x1917x550_10door",
        "label": "Planning example: wider shell, 10 doors",
        "outer_width_mm": 1200.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 10,
        "intent": "width_door_size_formula_candidate",
    },
    {
        "candidate_id": "width_1200x1917x550_12door",
        "label": "Planning example: wider shell, 12 doors",
        "outer_width_mm": 1200.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 12,
        "intent": "width_door_size_formula_candidate",
    },
    {
        "candidate_id": "width_1200x1917x550_14door",
        "label": "Planning example: wider shell, 14 doors",
        "outer_width_mm": 1200.0,
        "outer_height_mm": 1917.0,
        "outer_depth_mm": 550.0,
        "door_count": 14,
        "intent": "width_door_size_formula_candidate",
    },
]


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_optional_json(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    return read_json(path)


def as_float(value: Any, name: str) -> float:
    try:
        output = float(value)
    except (TypeError, ValueError):
        raise ValueError(f"{name} must be numeric, got {value!r}") from None
    if not math.isfinite(output):
        raise ValueError(f"{name} must be finite, got {value!r}")
    return output


def as_int(value: Any, name: str) -> int:
    try:
        output = int(value)
    except (TypeError, ValueError):
        raise ValueError(f"{name} must be an integer, got {value!r}") from None
    return output


def rounded(value: float) -> float:
    return round(value, 3)


def is_close(left: float, right: float, tolerance: float = 0.001) -> bool:
    return abs(left - right) <= tolerance


def load_candidates() -> tuple[list[dict[str, Any]], str]:
    if not CANDIDATES_PATH:
        return [dict(candidate) for candidate in DEFAULT_CANDIDATES], "built_in_default_candidates"
    path = Path(CANDIDATES_PATH)
    payload = read_json(path)
    raw_candidates = payload.get("candidates", payload)
    if not isinstance(raw_candidates, list):
        raise ValueError("dimension candidates JSON must be a list or an object with a 'candidates' list")
    candidates = [dict(candidate) for candidate in raw_candidates if isinstance(candidate, dict)]
    return candidates, str(path)


def baseline_rules(verified_packet: dict[str, Any]) -> dict[str, Any]:
    rules = verified_packet.get("cabinet_rule")
    if not isinstance(rules, dict):
        raise ValueError("verified rule packet is missing cabinet_rule")
    required_keys = [
        "nominal_width_mm",
        "nominal_height_mm",
        "nominal_depth_mm",
        "door_area_bottom_y_mm",
        "door_area_top_y_mm",
        "door_grid_top_gap_mm",
        "door_grid_bottom_gap_mm",
        "visual_gap_mm",
        "door_width_mm",
        "door_flat_width_extra_mm",
        "door_flat_height_extra_mm",
        "hinge_axis_abs_x_mm",
        "lock_center_abs_x_mm",
    ]
    missing = [key for key in required_keys if key not in rules]
    if missing:
        raise ValueError(f"verified rule packet cabinet_rule missing keys: {missing}")
    return rules


def verified_variant_index(verified_packet: dict[str, Any]) -> dict[int, dict[str, Any]]:
    variants: dict[int, dict[str, Any]] = {}
    for variant in verified_packet.get("verified_variants", []):
        if isinstance(variant, dict):
            variants[as_int(variant.get("door_count"), "door_count")] = variant
    return variants


def build_door_width_evidence_layers(verified_packet: dict[str, Any]) -> list[dict[str, Any]]:
    width_gate = read_optional_json(WIDTH_CANDIDATE_DOOR_CHAIN_GATE_PATH)
    skeleton_gate = read_optional_json(WIDTH_CANDIDATE_SKELETON_SEED_GATE_PATH)
    bundle_gate = read_optional_json(WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_PATH)
    verified_counts = sorted(as_int(count, "verified_door_count") for count in verified_packet.get("verified_door_counts", []))
    width_537_status = "door_chain_gate_missing"
    width_537_checks = ""
    width_537_failures = ""
    width_537_sources = [str(WIDTH_CANDIDATE_DOOR_CHAIN_GATE_PATH)]
    if bundle_gate and bundle_gate.get("status") == "PASS":
        width_537_status = "geometry_review_bundle_pass"
        width_537_checks = bundle_gate.get("check_count", "")
        width_537_failures = bundle_gate.get("failed_error_count", "")
        width_537_sources.extend(
            [
                str(WIDTH_CANDIDATE_SKELETON_SEED_GATE_PATH),
                str(WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_PATH),
            ]
        )
    elif skeleton_gate and skeleton_gate.get("status") == "PASS":
        width_537_status = "skeleton_seed_gate_pass"
        width_537_checks = skeleton_gate.get("check_count", "")
        width_537_failures = skeleton_gate.get("failed_error_count", "")
        width_537_sources.append(str(WIDTH_CANDIDATE_SKELETON_SEED_GATE_PATH))
    elif width_gate:
        width_537_status = "door_chain_gate_pass" if width_gate.get("status") == "PASS" else "door_chain_gate_not_pass"
        width_537_checks = width_gate.get("check_count", "")
        width_537_failures = width_gate.get("failed_error_count", "")

    return [
        {
            "id": "door_panel_W329_historical_single_part",
            "width_label": "W329",
            "door_width_mm": 329.0,
            "scope": "ordinary_door_single_part_or_series_card",
            "source": "apps/web/src/data/studioData.ts capability card: W329/W437, 多高度门板",
            "status": "historical_capability_needs_current_gate_rebind",
            "engineering_handoff_enabled": False,
            "interpretation": "Historical UI capability marker only in the current repo scan; no current W329 SolidWorks door-chain gate was found, so it must not be treated as a verified full-cabinet width rule.",
        },
        {
            "id": "door_panel_W437_verified_baseline",
            "width_label": "W437",
            "door_width_mm": 437.0,
            "scope": "1000W x 1917H x 550D baseline full cabinet",
            "source": str(VERIFIED_RULE_PACKET_PATH),
            "status": "solidworks_native_pack_and_go_verified",
            "verified_door_counts": verified_counts,
            "engineering_handoff_enabled": True,
            "interpretation": "Current verified engineering-reference door width for 10/12/14-door baseline cabinets.",
        },
        {
            "id": "door_panel_W537_1200W_candidate",
            "width_label": "W537",
            "door_width_mm": 537.0,
            "scope": "1200W x 1917H x 550D / 12-door resized door chain plus skeleton seed when available",
            "source": width_537_sources,
            "status": width_537_status,
            "check_count": width_537_checks,
            "failed_error_count": width_537_failures,
            "engineering_handoff_enabled": False,
            "interpretation": "Validated through resized door chain, 1200W master-body skeleton seed, and geometry-review bundle when PASS; still needs width-specific sheet-metal part split, front-frame/shelf rule confirmation, drawings, tolerances and structural sign-off before production handoff.",
        },
        {
            "id": "width_marker_18door_W784",
            "width_label": "18door_W784",
            "door_width_mm": "",
            "scope": "historical width-derived rows marker",
            "source": "apps/web/src/data/generatedSnapshot.ts verifiedCases and STD-P2-W784 review item",
            "status": "source_confirmation_required",
            "engineering_handoff_enabled": False,
            "interpretation": "Historical width-derived X-row marker; keep as review evidence until a width-specific source or drawing rule is bound.",
        },
    ]


def candidate_exactly_matches_baseline(candidate: dict[str, Any], rules: dict[str, Any]) -> bool:
    return (
        is_close(as_float(candidate.get("outer_width_mm"), "outer_width_mm"), as_float(rules["nominal_width_mm"], "nominal_width_mm"))
        and is_close(
            as_float(candidate.get("outer_height_mm"), "outer_height_mm"),
            as_float(rules["nominal_height_mm"], "nominal_height_mm"),
        )
        and is_close(as_float(candidate.get("outer_depth_mm"), "outer_depth_mm"), as_float(rules["nominal_depth_mm"], "nominal_depth_mm"))
    )


def derive_dimension_candidate(
    candidate: dict[str, Any],
    rules: dict[str, Any],
    verified_counts: set[int],
    formula_counts: set[int],
    verified_by_door_count: dict[int, dict[str, Any]],
) -> dict[str, Any]:
    candidate_id = str(candidate.get("candidate_id") or candidate.get("id") or "")
    if not candidate_id:
        raise ValueError("candidate_id is required")
    outer_width = as_float(candidate.get("outer_width_mm"), "outer_width_mm")
    outer_height = as_float(candidate.get("outer_height_mm"), "outer_height_mm")
    outer_depth = as_float(candidate.get("outer_depth_mm"), "outer_depth_mm")
    door_count = as_int(candidate.get("door_count"), "door_count")
    column_count = as_int(candidate.get("column_count", 2), "column_count")

    blockers: list[str] = []
    warnings: list[str] = []
    if outer_width <= 0 or outer_height <= 0 or outer_depth <= 0:
        blockers.append("outer width, height and depth must all be positive")
    if column_count != 2:
        blockers.append("only the two-column 16029 route is currently defined")
    if door_count <= 0 or door_count % column_count != 0:
        blockers.append("door_count must be a positive even number for the current two-column route")

    rows_per_column = door_count // column_count if column_count and door_count > 0 else 0
    baseline_height = as_float(rules["nominal_height_mm"], "nominal_height_mm")
    baseline_door_area_top = as_float(rules["door_area_top_y_mm"], "door_area_top_y_mm")
    baseline_top_service_gap = baseline_height - baseline_door_area_top
    door_area_bottom = as_float(candidate.get("door_area_bottom_y_mm", rules["door_area_bottom_y_mm"]), "door_area_bottom_y_mm")
    door_area_top = as_float(
        candidate.get("door_area_top_y_mm", outer_height - baseline_top_service_gap),
        "door_area_top_y_mm",
    )
    door_area_height = door_area_top - door_area_bottom
    grid_top_gap = as_float(candidate.get("door_grid_top_gap_mm", rules["door_grid_top_gap_mm"]), "door_grid_top_gap_mm")
    grid_bottom_gap = as_float(candidate.get("door_grid_bottom_gap_mm", rules["door_grid_bottom_gap_mm"]), "door_grid_bottom_gap_mm")
    visual_gap = as_float(candidate.get("visual_gap_mm", rules["visual_gap_mm"]), "visual_gap_mm")
    if rows_per_column <= 0:
        door_height = 0.0
        door_pitch = 0.0
    else:
        door_height = (door_area_height - grid_top_gap - grid_bottom_gap - (rows_per_column - 1) * visual_gap) / rows_per_column
        door_pitch = door_height + visual_gap
    if door_area_height <= 0:
        blockers.append("door area top must be above door area bottom")
    if door_height <= 0:
        blockers.append("derived door height must be positive")
    if door_height < 200.0:
        warnings.append("derived door height is below 200mm; storage usability and lock reach need review")

    baseline_width = as_float(rules["nominal_width_mm"], "nominal_width_mm")
    baseline_depth = as_float(rules["nominal_depth_mm"], "nominal_depth_mm")
    baseline_door_width = as_float(rules["door_width_mm"], "door_width_mm")
    if "door_width_mm" in candidate:
        door_width = as_float(candidate["door_width_mm"], "door_width_mm")
        door_width_source = "candidate_override"
    else:
        door_width = (outer_width - 2.0 * BASELINE_SIDE_MARGIN_MM - BASELINE_CENTER_GAP_MM) / column_count
        door_width_source = "outer_width_formula"
    door_column_center_abs_x = door_width / 2.0 + BASELINE_CENTER_GAP_MM / 2.0
    side_margin = outer_width / 2.0 - door_column_center_abs_x - door_width / 2.0
    center_gap = 2.0 * (door_column_center_abs_x - door_width / 2.0)
    width_uses_standard_formula = is_close(side_margin, BASELINE_SIDE_MARGIN_MM) and is_close(
        center_gap,
        BASELINE_CENTER_GAP_MM,
    )

    baseline_match = candidate_exactly_matches_baseline(candidate, rules)
    width_changed = not is_close(outer_width, baseline_width)
    height_changed = not is_close(outer_height, baseline_height)
    depth_changed = not is_close(outer_depth, baseline_depth)
    geometry_review_ready = False
    if door_width <= 0:
        blockers.append("derived door width must be positive")
    if not width_uses_standard_formula:
        blockers.append("door width must follow the current width formula unless a new width rule is approved")
    if width_changed:
        blockers.append(
            "outer_width changed; door_width follows total width, but resized door frame/panel, side clearance and lock/hinge/hook edge offsets need native CAD validation"
        )
    if depth_changed:
        blockers.append("outer_depth changed; current phase keeps depth at 550mm until shell depth rules are validated")
    if height_changed:
        blockers.append("outer_height changed; current phase keeps total height at 1917mm and varies door height by door_count only")
    if door_count not in verified_counts:
        blocker = "door_count has not passed native SolidWorks Pack-and-Go validation"
        if door_count in formula_counts:
            blocker += " and is currently only a formula candidate"
        blockers.append(blocker)

    enabled_for_handoff = baseline_match and door_count in verified_counts and not blockers
    enabled_for_geometry_review_handoff = geometry_review_ready
    if enabled_for_handoff:
        verification_level = "verified_engineering_reference"
        status = "PASS"
    elif width_changed:
        verification_level = "formula_candidate_needs_width_door_cad_validation"
        status = "CANDIDATE_ONLY"
    elif depth_changed:
        verification_level = "blocked_until_depth_shell_rules"
        status = "BLOCKED"
    elif height_changed:
        verification_level = "blocked_current_phase_fixed_height"
        status = "BLOCKED"
    elif door_count not in verified_counts:
        verification_level = "formula_candidate_needs_cad_validation"
        status = "CANDIDATE_ONLY"
    else:
        verification_level = "not_enabled_for_engineering_handoff"
        status = "BLOCKED"

    if enabled_for_handoff:
        source_variant = verified_by_door_count.get(door_count, {})
        pack = source_variant.get("pack_and_go_handoff", {}) if isinstance(source_variant, dict) else {}
        evidence = {
            "verified_rule_packet_variant": f"{door_count}door",
            "pack_and_go_ok": pack.get("ok"),
            "external_top_reference_count": pack.get("external_top_reference_count"),
        }
    else:
        evidence = {
            "verified_rule_packet_variant": "",
            "pack_and_go_ok": "",
            "external_top_reference_count": "",
        }

    counts = expected_counts(door_count, rows_per_column)
    return {
        "candidate_id": candidate_id,
        "label": str(candidate.get("label") or candidate_id),
        "intent": str(candidate.get("intent") or ""),
        "status": status,
        "verification_level": verification_level,
        "enabled_for_engineering_handoff": enabled_for_handoff,
        "enabled_for_geometry_review_handoff": enabled_for_geometry_review_handoff,
        "input": {
            "outer_width_mm": rounded(outer_width),
            "outer_height_mm": rounded(outer_height),
            "outer_depth_mm": rounded(outer_depth),
            "door_count": door_count,
            "column_count": column_count,
            "door_area_bottom_y_mm": rounded(door_area_bottom),
            "door_area_top_y_mm": rounded(door_area_top),
            "door_grid_top_gap_mm": rounded(grid_top_gap),
            "door_grid_bottom_gap_mm": rounded(grid_bottom_gap),
            "visual_gap_mm": rounded(visual_gap),
            "door_width_mm": rounded(door_width),
        },
        "derived_layout": {
            "rows_per_column": rows_per_column,
            "door_area_height_mm": rounded(door_area_height),
            "door_height_mm": rounded(door_height),
            "door_pitch_mm": rounded(door_pitch),
            "door_flat_estimate_mm": {
                "width": rounded(door_width + as_float(rules["door_flat_width_extra_mm"], "door_flat_width_extra_mm")),
                "height": rounded(door_height + as_float(rules["door_flat_height_extra_mm"], "door_flat_height_extra_mm")),
            },
            "shelf_count": counts["shelves"],
            "front_frame_crossbar_count": counts["front_frame_crossbars"],
            "door_module_count": counts["door_modules"],
            "hinge_pin_count": counts["hinge_pins"],
            "lock_hook_pad_count": counts["lock_hook_pads"],
            "electric_lock_hook_count": counts["electric_lock_hooks"],
        },
        "position_contract": {
            "door_width_source": door_width_source,
            "door_width_formula": "(outer_width_mm - 2 * side_margin_mm - center_gap_mm) / 2",
            "side_margin_mm": rounded(side_margin),
            "center_gap_between_columns_mm": rounded(center_gap),
            "door_column_center_abs_x_formula": "door_width_mm / 2 + center_gap_mm / 2",
            "left_door_column_center_x_mm": rounded(-door_column_center_abs_x),
            "right_door_column_center_x_mm": rounded(door_column_center_abs_x),
            "right_column_route": DOOR_ARRAY_RIGHT_COLUMN_ROUTE,
            "right_column_expected_rotation_matrix": RIGHT_COLUMN_IDENTITY_ROTATION,
            "right_column_legacy_180deg_rotation_allowed": False,
            "feature_local_placement_x_by_column_mm": FEATURE_LOCAL_PLACEMENT_X_BY_COLUMN_MM,
            "feature_local_gate_x_by_column_mm": FEATURE_LOCAL_GATE_X_BY_COLUMN_MM,
            "variable_width_feature_x_rule": "for width-changed candidates, hinge pin, U-hook pad and electric lock hook X must be re-derived from door edge offsets and pass native CAD gates before engineering handoff",
            "candidate_hinge_axis_abs_x_mm": rounded(door_column_center_abs_x + 208.5),
            "candidate_lock_center_abs_x_mm": rounded(abs(door_column_center_abs_x - 203.5)),
            "left_right_exported_door_bbox_y_delta_max_mm": LEFT_RIGHT_EXPORTED_DOOR_BBOX_Y_DELTA_MAX_MM,
        },
        "blockers": blockers,
        "warnings": warnings,
        "evidence": evidence,
    }


def expected_counts(door_count: int, rows_per_column: int) -> dict[str, int]:
    between_row_pair_count = max(rows_per_column - 1, 0) * 2
    return {
        "door_modules": max(door_count, 0),
        "door_panel_features": max(door_count, 0),
        "hinge_pins": max(door_count, 0),
        "lock_hook_pads": max(door_count, 0),
        "electric_lock_hooks": max(door_count, 0),
        "shelves": between_row_pair_count,
        "front_frame_crossbars": between_row_pair_count,
        "cabinet_vertical_dividers": 2,
        "door_frame_vertical_dividers": 2,
    }


def build_contract(verified_packet: dict[str, Any], candidates: list[dict[str, Any]], candidate_source: str) -> dict[str, Any]:
    rules = baseline_rules(verified_packet)
    if verified_packet.get("status") != "PASS":
        raise ValueError("verified rule packet must be PASS before building a dimension contract")
    verified_counts = {as_int(count, "verified_door_count") for count in verified_packet.get("verified_door_counts", [])}
    formula_counts = {as_int(count, "formula_candidate_door_count") for count in verified_packet.get("formula_candidate_door_counts", [])}
    verified_by_count = verified_variant_index(verified_packet)
    derived_candidates = [
        derive_dimension_candidate(candidate, rules, verified_counts, formula_counts, verified_by_count)
        for candidate in candidates
    ]
    payload = {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "purpose": "Fixed-height 16029 width and door-size parameter contract before producing non-baseline CAD models.",
        "source_verified_rule_packet_json": str(VERIFIED_RULE_PACKET_PATH),
        "candidate_source": candidate_source,
        "current_verified_scope": verified_packet.get("scope"),
        "current_verified_door_counts": sorted(verified_counts),
        "formula_candidate_door_counts": sorted(formula_counts),
        "resource_policy": {
            "cad_validation_mode": "single_thread_only",
            "solidworks_process_policy": "one SolidWorks automation job at a time",
            "freecad_process_policy": "one FreeCAD validation job at a time",
            "reason": "avoid exhausting workstation resources and reduce nondeterministic CAD automation failures",
        },
        "baseline_outer_size_mm": {
            "width": rules["nominal_width_mm"],
            "height": rules["nominal_height_mm"],
            "depth": rules["nominal_depth_mm"],
        },
        "baseline_door_stack_mm": {
            "door_area_bottom_y": rules["door_area_bottom_y_mm"],
            "door_area_top_y": rules["door_area_top_y_mm"],
            "door_area_height": rules["door_area_height_mm"],
            "door_grid_top_gap": rules["door_grid_top_gap_mm"],
            "door_grid_bottom_gap": rules["door_grid_bottom_gap_mm"],
            "visual_gap": rules["visual_gap_mm"],
            "door_width": rules["door_width_mm"],
        },
        "door_width_evidence_layers": build_door_width_evidence_layers(verified_packet),
        "door_width_interpretation": [
            "W329/W437 in the management UI are door-panel or ordinary-door capability markers, not automatically full-cabinet width validation.",
            "W437 is the current verified baseline full-cabinet door width for 1000W x 1917H x 550D 10/12/14-door SolidWorks handoff.",
            "W537 is the current 1200W candidate door width; the door-chain, skeleton seed and geometry-review bundle gates can pass while production handoff remains disabled.",
            "Historical width-derived markers such as 18door_W784 stay review-only until source drawings or current CAD gates are bound.",
        ],
        "dimension_input_contract": [
            {
                "name": "outer_width_mm",
                "required": True,
                "rule": "May vary in the current planning phase; door_width_mm is derived from total width and width-changed candidates need native CAD validation before handoff.",
            },
            {
                "name": "outer_height_mm",
                "required": True,
                "rule": "Current phase keeps total height fixed at 1917mm; door height changes only through door_count/row stack.",
            },
            {
                "name": "outer_depth_mm",
                "required": True,
                "rule": "Current phase keeps depth fixed at 550mm until shell depth rules are validated.",
            },
            {
                "name": "door_count",
                "required": True,
                "rule": "Current engineering handoff is limited to 10/12/14 doors.",
            },
            {
                "name": "door_area_bottom_y_mm",
                "required": False,
                "rule": "Defaults to baseline 30mm.",
            },
            {
                "name": "door_area_top_y_mm",
                "required": False,
                "rule": "Defaults to outer_height_mm minus the baseline 60mm top service gap.",
            },
        ],
        "derivation_rules": {
            "rows_per_column": "door_count / 2",
            "door_area_height": "door_area_top_y_mm - door_area_bottom_y_mm",
            "door_height": "(door_area_height - top_grid_gap - bottom_grid_gap - (rows_per_column - 1) * visual_gap) / rows_per_column",
            "door_pitch": "door_height + visual_gap",
            "door_width": "(outer_width_mm - 2 * 23mm side_margin - 80mm center_gap) / 2",
            "door_column_center_abs_x": "door_width_mm / 2 + 40mm",
            "shelf_count": "(rows_per_column - 1) * 2",
            "front_frame_crossbar_count": "(rows_per_column - 1) * 2",
            "door_hinge_lock_hook_count": "door_count",
        },
        "fixed_handedness_contract": {
            "left_module": "left door module",
            "right_module": "right door module",
            "right_column_route": DOOR_ARRAY_RIGHT_COLUMN_ROUTE,
            "right_column_expected_rotation_matrix": RIGHT_COLUMN_IDENTITY_ROTATION,
            "u_hook_pad_placement_tx_mm": FEATURE_LOCAL_PLACEMENT_X_BY_COLUMN_MM["u_hook_pad_placement_tx"],
            "electric_lock_hook_tx_mm": FEATURE_LOCAL_PLACEMENT_X_BY_COLUMN_MM["electric_lock_hook"],
            "hinge_pin_tx_mm": FEATURE_LOCAL_PLACEMENT_X_BY_COLUMN_MM["hinge_pin"],
            "baseline_only_note": "The fixed Tx values are verified for the 437mm baseline door; width-changed doors must re-derive feature X from edge offsets before CAD handoff.",
        },
        "quality_gates_before_engineering_handoff": [
            "root bbox must match candidate outer_width/outer_height/outer_depth within CAD tolerance",
            "door stack top/bottom, door height and door pitch must match the contract",
            "for width-changed candidates, door panel/frame width and hinge/lock/hook X offsets must match the resized door contract",
            "left and right columns must have equal row count and aligned Y bounds",
            "right column must use the right-hand door module with identity transform",
            "hinge pin, U-hook pad and electric lock hook local X baseline gates must pass for left and right doors",
            "door modules, hinge pins, U-hook pads and electric lock hooks must equal door_count",
            "shelves and front frame crossbars must equal (rows_per_column - 1) * 2",
            "SolidWorks Pack-and-Go must have zero external top references before handoff",
        ],
        "structural_engineer_questions": [
            "Confirm whether changed width uses the formula door_width=(outer_width-46-80)/2, or whether side margins/center gap need a different rule.",
            "Confirm resized door panel/frame flat patterns and whether hinge, U-hook pad and electric lock hook X offsets stay edge-based.",
            "Confirm door gaps after paint/powder coating and acceptable accumulated tolerance across the full stack.",
            "Confirm lock engagement allowance after door sag and hinge clearance changes.",
            "Confirm hole-to-bend distances and supplier minimums for any resized sheet-metal panels.",
            "Confirm whether operation panel, wiring, back panel and service clearances move when width changes.",
        ],
        "candidates": derived_candidates,
        "output_paths": {
            "json": str(OUTPUT_JSON_PATH),
            "markdown": str(OUTPUT_MARKDOWN_PATH),
            "csv": str(OUTPUT_CSV_PATH),
        },
    }
    validate_contract(payload)
    return payload


def validate_contract(payload: dict[str, Any]) -> None:
    problems: list[str] = []
    verified_counts = set(payload["current_verified_door_counts"])
    baseline = payload["baseline_outer_size_mm"]
    for candidate in payload["candidates"]:
        input_data = candidate["input"]
        baseline_size = (
            is_close(input_data["outer_width_mm"], baseline["width"])
            and is_close(input_data["outer_height_mm"], baseline["height"])
            and is_close(input_data["outer_depth_mm"], baseline["depth"])
        )
        if candidate["enabled_for_engineering_handoff"]:
            if not baseline_size:
                problems.append(f"{candidate['candidate_id']} is handoff-enabled but does not match the verified outer size")
            if input_data["door_count"] not in verified_counts:
                problems.append(f"{candidate['candidate_id']} is handoff-enabled with an unverified door count")
            if candidate["blockers"]:
                problems.append(f"{candidate['candidate_id']} is handoff-enabled with blockers")
        if input_data["door_count"] % 2 != 0:
            problems.append(f"{candidate['candidate_id']} has an odd door_count")
        if candidate["derived_layout"]["door_height_mm"] <= 0:
            problems.append(f"{candidate['candidate_id']} has a non-positive derived door height")
    if problems:
        raise ValueError("dimension contract validation failed: " + "; ".join(problems))


def write_json(payload: dict[str, Any]) -> None:
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def write_csv(payload: dict[str, Any]) -> None:
    OUTPUT_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "candidate_id",
        "status",
        "verification_level",
        "enabled_for_engineering_handoff",
        "enabled_for_geometry_review_handoff",
        "outer_width_mm",
        "outer_height_mm",
        "outer_depth_mm",
        "door_count",
        "rows_per_column",
        "door_area_bottom_y_mm",
        "door_area_top_y_mm",
        "door_area_height_mm",
        "door_width_mm",
        "door_height_mm",
        "door_pitch_mm",
        "left_door_column_center_x_mm",
        "right_door_column_center_x_mm",
        "shelf_count",
        "front_frame_crossbar_count",
        "hinge_pin_count",
        "lock_hook_pad_count",
        "electric_lock_hook_count",
        "blockers",
        "warnings",
    ]
    with OUTPUT_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for candidate in payload["candidates"]:
            input_data = candidate["input"]
            layout = candidate["derived_layout"]
            position = candidate["position_contract"]
            writer.writerow(
                {
                    "candidate_id": candidate["candidate_id"],
                    "status": candidate["status"],
                    "verification_level": candidate["verification_level"],
                    "enabled_for_engineering_handoff": candidate["enabled_for_engineering_handoff"],
                    "enabled_for_geometry_review_handoff": candidate["enabled_for_geometry_review_handoff"],
                    "outer_width_mm": input_data["outer_width_mm"],
                    "outer_height_mm": input_data["outer_height_mm"],
                    "outer_depth_mm": input_data["outer_depth_mm"],
                    "door_count": input_data["door_count"],
                    "rows_per_column": layout["rows_per_column"],
                    "door_area_bottom_y_mm": input_data["door_area_bottom_y_mm"],
                    "door_area_top_y_mm": input_data["door_area_top_y_mm"],
                    "door_area_height_mm": layout["door_area_height_mm"],
                    "door_width_mm": input_data["door_width_mm"],
                    "door_height_mm": layout["door_height_mm"],
                    "door_pitch_mm": layout["door_pitch_mm"],
                    "left_door_column_center_x_mm": position["left_door_column_center_x_mm"],
                    "right_door_column_center_x_mm": position["right_door_column_center_x_mm"],
                    "shelf_count": layout["shelf_count"],
                    "front_frame_crossbar_count": layout["front_frame_crossbar_count"],
                    "hinge_pin_count": layout["hinge_pin_count"],
                    "lock_hook_pad_count": layout["lock_hook_pad_count"],
                    "electric_lock_hook_count": layout["electric_lock_hook_count"],
                    "blockers": " | ".join(candidate["blockers"]),
                    "warnings": " | ".join(candidate["warnings"]),
                }
            )


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 Outer-Dimension Contract",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Source verified packet: `{payload['source_verified_rule_packet_json']}`",
        f"- Current verified scope: `{payload['current_verified_scope']}`",
        f"- CAD validation mode: `{payload['resource_policy']['cad_validation_mode']}`",
        "",
        "## Decision",
        "",
        "The current 1000W x 1917H x 550D 10/12/14 models remain enabled as verified engineering references.",
        "Current planning keeps total height fixed at 1917mm and depth fixed at 550mm; door height changes through door_count/row stack.",
        "Width candidates derive door_width from total width using the 23mm side margin and 80mm center gap rule, but remain CAD-validation candidates until resized door/frame/lock/hinge gates pass.",
        "The earlier 2117H height candidate is outside the current phase and is not part of this contract.",
        "",
        "## Door Width Evidence Layers",
        "",
        "| Width marker | Door width | Scope | Status | Eng. ref | Interpretation |",
        "| --- | ---: | --- | --- | --- | --- |",
    ]
    for layer in payload.get("door_width_evidence_layers", []):
        lines.append(
            "| "
            + " | ".join(
                [
                    str(layer["width_label"]),
                    str(layer["door_width_mm"]),
                    str(layer["scope"]),
                    str(layer["status"]),
                    "yes" if layer.get("engineering_handoff_enabled") else "no",
                    str(layer["interpretation"]),
                ]
            )
            + " |"
        )
    lines.extend(
        [
            "",
            "### Width Interpretation",
            "",
        ]
    )
    for item in payload.get("door_width_interpretation", []):
        lines.append(f"- {item}")
    lines.extend(
        [
            "",
        "## Candidate Matrix",
        "",
        "| Candidate | Size | Doors | Door height | Pitch | Column X L/R | Status | Eng. ref | Geometry review |",
        "| --- | --- | ---: | ---: | ---: | --- | --- | --- | --- |",
        ]
    )
    for candidate in payload["candidates"]:
        input_data = candidate["input"]
        layout = candidate["derived_layout"]
        position = candidate["position_contract"]
        lines.append(
            "| "
            + " | ".join(
                [
                    candidate["candidate_id"],
                    f"{input_data['outer_width_mm']} x {input_data['outer_height_mm']} x {input_data['outer_depth_mm']}",
                    str(input_data["door_count"]),
                    f"{layout['door_height_mm']} mm",
                    f"{layout['door_pitch_mm']} mm",
                    f"{position['left_door_column_center_x_mm']} / {position['right_door_column_center_x_mm']} mm",
                    candidate["verification_level"],
                    "yes" if candidate["enabled_for_engineering_handoff"] else "no",
                    "yes" if candidate["enabled_for_geometry_review_handoff"] else "no",
                ]
            )
            + " |"
        )
    lines.extend(
        [
            "",
            "## Required Inputs",
            "",
            "| Name | Required | Rule |",
            "| --- | --- | --- |",
        ]
    )
    for item in payload["dimension_input_contract"]:
        lines.append(f"| {item['name']} | {item['required']} | {item['rule']} |")
    lines.extend(["", "## Fixed Handedness Rules", ""])
    fixed = payload["fixed_handedness_contract"]
    lines.extend(
        [
            f"- Left column uses `{fixed['left_module']}`.",
            f"- Right column uses `{fixed['right_module']}`.",
            f"- Right column route is `{fixed['right_column_route']}`.",
            "- Right column rotation matrix must stay identity.",
            f"- U-hook pad placement Tx is L={fixed['u_hook_pad_placement_tx_mm']['L']}mm, R={fixed['u_hook_pad_placement_tx_mm']['R']}mm.",
            f"- Electric lock hook Tx is L={fixed['electric_lock_hook_tx_mm']['L']}mm, R={fixed['electric_lock_hook_tx_mm']['R']}mm.",
            f"- Hinge pin Tx is L={fixed['hinge_pin_tx_mm']['L']}mm, R={fixed['hinge_pin_tx_mm']['R']}mm.",
            f"- {fixed['baseline_only_note']}",
        ]
    )
    lines.extend(["", "## Gates Before Engineering Handoff", ""])
    for gate in payload["quality_gates_before_engineering_handoff"]:
        lines.append(f"- {gate}")
    lines.extend(["", "## Blockers By Candidate", ""])
    for candidate in payload["candidates"]:
        blockers = candidate["blockers"] or ["none"]
        lines.append(f"### {candidate['candidate_id']}")
        for blocker in blockers:
            lines.append(f"- {blocker}")
        if candidate["warnings"]:
            for warning in candidate["warnings"]:
                lines.append(f"- warning: {warning}")
        lines.append("")
    lines.extend(["## Structural Engineer Questions", ""])
    for question in payload["structural_engineer_questions"]:
        lines.append(f"- {question}")
    lines.append("")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    verified_packet = read_json(VERIFIED_RULE_PACKET_PATH)
    candidates, candidate_source = load_candidates()
    payload = build_contract(verified_packet, candidates, candidate_source)
    write_json(payload)
    write_markdown(payload)
    write_csv(payload)
    print(
        json.dumps(
            {
                "status": "ok",
                "json": str(OUTPUT_JSON_PATH),
                "markdown": str(OUTPUT_MARKDOWN_PATH),
                "csv": str(OUTPUT_CSV_PATH),
                "candidate_count": len(payload["candidates"]),
                "handoff_enabled_count": sum(1 for candidate in payload["candidates"] if candidate["enabled_for_engineering_handoff"]),
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
