from __future__ import annotations

import csv
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
CONTRACT_JSON_PATH = Path(
    os.getenv("STUDIO_16029_DIMENSION_CONTRACT_JSON", ROOT_DIR / "data" / "locker_16029_dimension_contract.json")
)
CONTRACT_CSV_PATH = Path(
    os.getenv("STUDIO_16029_DIMENSION_CONTRACT_CSV", ROOT_DIR / "data" / "locker_16029_dimension_contract.csv")
)
VERIFIED_RULE_PACKET_PATH = Path(
    os.getenv("STUDIO_16029_VERIFIED_RULE_PACKET_JSON", ROOT_DIR / "data" / "locker_16029_verified_rule_packet.json")
)
DOOR_WELD_CS_PATH = ROOT_DIR / "workers" / "solidworks_tools" / "BuildDoorWeldModule.cs"
ORDINARY_DOOR_CS_PATH = ROOT_DIR / "workers" / "solidworks_tools" / "BuildOrdinaryDoorModule.cs"
DOOR_ARRAY_CS_PATH = ROOT_DIR / "workers" / "solidworks_tools" / "BuildDoorArrayModule.cs"
WIDTH_CANDIDATE_DOOR_CHAIN_GATE_JSON_PATH = ROOT_DIR / "data" / "locker_16029_width_candidate_door_chain_gate.json"
WIDTH_CANDIDATE_SKELETON_SEED_GATE_JSON_PATH = ROOT_DIR / "data" / "locker_16029_width_candidate_skeleton_seed_gate.json"
WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_JSON_PATH = (
    ROOT_DIR / "data" / "locker_16029_width_candidate_geometry_review_bundle_gate.json"
)

OUTPUT_JSON_PATH = Path(
    os.getenv(
        "STUDIO_16029_DIMENSION_CONTRACT_VALIDATION_JSON",
        ROOT_DIR / "data" / "locker_16029_dimension_contract_validation.json",
    )
)
OUTPUT_MARKDOWN_PATH = Path(
    os.getenv(
        "STUDIO_16029_DIMENSION_CONTRACT_VALIDATION_MD",
        ROOT_DIR / "data" / "locker_16029_dimension_contract_validation.md",
    )
)
OUTPUT_CSV_PATH = Path(
    os.getenv(
        "STUDIO_16029_DIMENSION_CONTRACT_VALIDATION_CSV",
        ROOT_DIR / "data" / "locker_16029_dimension_contract_validation.csv",
    )
)

TOL_MM = 0.001
EXPECTED_VERIFIED_DOOR_COUNTS = [10, 12, 14]
EXPECTED_BASELINE_SIZE_MM = {"width": 1000.0, "height": 1917.0, "depth": 550.0}
EXPECTED_BASELINE_COLUMN_X_MM = {"L": -258.5, "R": 258.5}
EXPECTED_SIDE_MARGIN_MM = 23.0
EXPECTED_CENTER_GAP_MM = 80.0
EXPECTED_RESOURCE_MODE = "single_thread_only"


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise FileNotFoundError(path)
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def read_text(path: Path) -> str:
    if not path.exists():
        raise FileNotFoundError(path)
    return path.read_text(encoding="utf-8-sig")


def as_float(value: Any, default: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def as_int(value: Any, default: int = 0) -> int:
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


def close(left: Any, right: Any, tolerance: float = TOL_MM) -> bool:
    return abs(as_float(left) - as_float(right)) <= tolerance


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


def candidate_by_id(payload: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {
        str(candidate.get("candidate_id")): candidate
        for candidate in payload.get("candidates", [])
        if isinstance(candidate, dict) and candidate.get("candidate_id")
    }


def verified_by_door_count(payload: dict[str, Any]) -> dict[int, dict[str, Any]]:
    return {
        as_int(variant.get("door_count")): variant
        for variant in payload.get("verified_variants", [])
        if isinstance(variant, dict)
    }


def csv_by_candidate_id(rows: list[dict[str, str]]) -> dict[str, dict[str, str]]:
    return {str(row.get("candidate_id")): row for row in rows if row.get("candidate_id")}


def validate_contract_header(contract: dict[str, Any], verified_packet: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    add_check(checks, "contract_product_family", contract.get("product_family") == "16029", contract.get("product_family"), "16029")
    add_check(checks, "verified_rule_packet_status", verified_packet.get("status") == "PASS", verified_packet.get("status"), "PASS")
    add_check(
        checks,
        "contract_verified_counts",
        contract.get("current_verified_door_counts") == EXPECTED_VERIFIED_DOOR_COUNTS,
        contract.get("current_verified_door_counts"),
        EXPECTED_VERIFIED_DOOR_COUNTS,
    )
    add_check(
        checks,
        "resource_policy_single_thread",
        contract.get("resource_policy", {}).get("cad_validation_mode") == EXPECTED_RESOURCE_MODE,
        contract.get("resource_policy", {}).get("cad_validation_mode"),
        EXPECTED_RESOURCE_MODE,
    )
    baseline = contract.get("baseline_outer_size_mm", {})
    for axis, expected in EXPECTED_BASELINE_SIZE_MM.items():
        add_check(
            checks,
            f"baseline_outer_{axis}",
            close(baseline.get(axis), expected),
            baseline.get(axis),
            expected,
        )


def validate_candidate_math(candidate: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    cid = candidate["candidate_id"]
    input_data = candidate["input"]
    layout = candidate["derived_layout"]
    position = candidate["position_contract"]
    door_count = as_int(input_data.get("door_count"))
    rows_per_column = as_int(layout.get("rows_per_column"))
    door_area_height = as_float(input_data.get("door_area_top_y_mm")) - as_float(input_data.get("door_area_bottom_y_mm"))
    expected_door_height = (
        door_area_height
        - as_float(input_data.get("door_grid_top_gap_mm"))
        - as_float(input_data.get("door_grid_bottom_gap_mm"))
        - (rows_per_column - 1) * as_float(input_data.get("visual_gap_mm"))
    ) / rows_per_column
    expected_pitch = expected_door_height + as_float(input_data.get("visual_gap_mm"))
    expected_between_count = (rows_per_column - 1) * 2
    expected_door_width = (as_float(input_data.get("outer_width_mm")) - 2.0 * EXPECTED_SIDE_MARGIN_MM - EXPECTED_CENTER_GAP_MM) / 2.0
    expected_column_x = expected_door_width / 2.0 + EXPECTED_CENTER_GAP_MM / 2.0

    add_check(checks, f"{cid}:rows_per_column", rows_per_column == door_count // 2, rows_per_column, door_count // 2)
    add_check(checks, f"{cid}:door_area_height", close(layout.get("door_area_height_mm"), door_area_height), layout.get("door_area_height_mm"), round(door_area_height, 3))
    add_check(checks, f"{cid}:door_height_formula", close(layout.get("door_height_mm"), expected_door_height), layout.get("door_height_mm"), round(expected_door_height, 3))
    add_check(checks, f"{cid}:door_pitch_formula", close(layout.get("door_pitch_mm"), expected_pitch), layout.get("door_pitch_mm"), round(expected_pitch, 3))
    add_check(checks, f"{cid}:door_width_formula", close(input_data.get("door_width_mm"), expected_door_width), input_data.get("door_width_mm"), round(expected_door_width, 3))
    add_check(checks, f"{cid}:side_margin_contract", close(position.get("side_margin_mm"), EXPECTED_SIDE_MARGIN_MM), position.get("side_margin_mm"), EXPECTED_SIDE_MARGIN_MM)
    add_check(checks, f"{cid}:center_gap_contract", close(position.get("center_gap_between_columns_mm"), EXPECTED_CENTER_GAP_MM), position.get("center_gap_between_columns_mm"), EXPECTED_CENTER_GAP_MM)
    add_check(checks, f"{cid}:shelf_count_formula", layout.get("shelf_count") == expected_between_count, layout.get("shelf_count"), expected_between_count)
    add_check(
        checks,
        f"{cid}:crossbar_count_formula",
        layout.get("front_frame_crossbar_count") == expected_between_count,
        layout.get("front_frame_crossbar_count"),
        expected_between_count,
    )
    for key in ("door_module_count", "hinge_pin_count", "lock_hook_pad_count", "electric_lock_hook_count"):
        add_check(checks, f"{cid}:{key}", layout.get(key) == door_count, layout.get(key), door_count)
    add_check(
        checks,
        f"{cid}:left_column_x_formula",
        close(position.get("left_door_column_center_x_mm"), -expected_column_x),
        position.get("left_door_column_center_x_mm"),
        round(-expected_column_x, 3),
    )
    add_check(
        checks,
        f"{cid}:right_column_x_formula",
        close(position.get("right_door_column_center_x_mm"), expected_column_x),
        position.get("right_door_column_center_x_mm"),
        round(expected_column_x, 3),
    )


def validate_baseline_candidates(
    contract: dict[str, Any],
    verified_packet: dict[str, Any],
    checks: list[dict[str, Any]],
) -> None:
    candidates = candidate_by_id(contract)
    verified_variants = verified_by_door_count(verified_packet)
    handoff_enabled = sorted(
        as_int(candidate.get("input", {}).get("door_count"))
        for candidate in candidates.values()
        if candidate.get("enabled_for_engineering_handoff")
    )
    add_check(checks, "handoff_enabled_counts_only_baseline", handoff_enabled == EXPECTED_VERIFIED_DOOR_COUNTS, handoff_enabled, EXPECTED_VERIFIED_DOOR_COUNTS)
    geometry_review_enabled = sorted(
        str(candidate.get("candidate_id"))
        for candidate in candidates.values()
        if candidate.get("enabled_for_geometry_review_handoff")
    )
    add_check(checks, "no_geometry_review_candidates_enabled", geometry_review_enabled == [], geometry_review_enabled, [])
    add_check(
        checks,
        "current_phase_excludes_2117_height_candidate",
        "height_1000x2117x550_14door" not in candidates,
        sorted(candidates),
        "no 2117H height candidate in current contract",
    )

    for door_count in EXPECTED_VERIFIED_DOOR_COUNTS:
        cid = f"baseline_1000x1917x550_{door_count}door"
        candidate = candidates.get(cid)
        verified = verified_variants.get(door_count)
        add_check(checks, f"{cid}:exists", candidate is not None, bool(candidate), True)
        if not candidate or not verified:
            continue
        layout = candidate["derived_layout"]
        input_data = candidate["input"]
        verified_layout = verified.get("layout_rule", {})
        verified_counts = verified.get("component_count_rule", {})
        position = candidate.get("position_contract", {})
        evidence = candidate.get("evidence", {})
        add_check(checks, f"{cid}:handoff_enabled", candidate.get("enabled_for_engineering_handoff") is True, candidate.get("enabled_for_engineering_handoff"), True)
        add_check(checks, f"{cid}:no_blockers", candidate.get("blockers") == [], candidate.get("blockers"), [])
        add_check(checks, f"{cid}:door_height_matches_verified", close(layout.get("door_height_mm"), verified_layout.get("door_height_mm")), layout.get("door_height_mm"), verified_layout.get("door_height_mm"))
        add_check(checks, f"{cid}:door_pitch_matches_verified", close(layout.get("door_pitch_mm"), verified_layout.get("door_pitch_mm")), layout.get("door_pitch_mm"), verified_layout.get("door_pitch_mm"))
        add_check(checks, f"{cid}:door_width_matches_verified", close(input_data.get("door_width_mm"), verified_layout.get("door_width_mm")), input_data.get("door_width_mm"), verified_layout.get("door_width_mm"))
        add_check(checks, f"{cid}:shelf_count_matches_verified", layout.get("shelf_count") == verified_counts.get("shelves"), layout.get("shelf_count"), verified_counts.get("shelves"))
        add_check(
            checks,
            f"{cid}:crossbar_count_matches_verified",
            layout.get("front_frame_crossbar_count") == verified_counts.get("front_frame_crossbars"),
            layout.get("front_frame_crossbar_count"),
            verified_counts.get("front_frame_crossbars"),
        )
        add_check(checks, f"{cid}:left_column_x_baseline", close(position.get("left_door_column_center_x_mm"), EXPECTED_BASELINE_COLUMN_X_MM["L"]), position.get("left_door_column_center_x_mm"), EXPECTED_BASELINE_COLUMN_X_MM["L"])
        add_check(checks, f"{cid}:right_column_x_baseline", close(position.get("right_door_column_center_x_mm"), EXPECTED_BASELINE_COLUMN_X_MM["R"]), position.get("right_door_column_center_x_mm"), EXPECTED_BASELINE_COLUMN_X_MM["R"])
        add_check(checks, f"{cid}:pack_and_go_evidence_ok", str(evidence.get("pack_and_go_ok")) == "True", evidence.get("pack_and_go_ok"), "True")
        add_check(
            checks,
            f"{cid}:pack_and_go_external_refs_zero",
            as_int(evidence.get("external_top_reference_count")) == 0,
            evidence.get("external_top_reference_count"),
            0,
        )


def validate_nonbaseline_blockers(contract: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    for candidate in contract.get("candidates", []):
        if not isinstance(candidate, dict):
            continue
        cid = str(candidate.get("candidate_id"))
        input_data = candidate.get("input", {})
        door_count = as_int(input_data.get("door_count"))
        is_baseline_size = (
            close(input_data.get("outer_width_mm"), EXPECTED_BASELINE_SIZE_MM["width"])
            and close(input_data.get("outer_height_mm"), EXPECTED_BASELINE_SIZE_MM["height"])
            and close(input_data.get("outer_depth_mm"), EXPECTED_BASELINE_SIZE_MM["depth"])
        )
        if is_baseline_size and door_count in EXPECTED_VERIFIED_DOOR_COUNTS:
            continue
        blockers = candidate.get("blockers") or []
        add_check(checks, f"{cid}:candidate_not_handoff_enabled", candidate.get("enabled_for_engineering_handoff") is False, candidate.get("enabled_for_engineering_handoff"), False)
        add_check(checks, f"{cid}:candidate_has_blocker", len(blockers) > 0, blockers, "at least one blocker")
        if not close(input_data.get("outer_width_mm"), EXPECTED_BASELINE_SIZE_MM["width"]):
            add_check(
                checks,
                f"{cid}:width_change_needs_width_door_cad_validation",
                candidate.get("verification_level") == "formula_candidate_needs_width_door_cad_validation",
                candidate.get("verification_level"),
                "formula_candidate_needs_width_door_cad_validation",
            )
            add_check(
                checks,
                f"{cid}:width_change_candidate_only",
                candidate.get("status") == "CANDIDATE_ONLY",
                candidate.get("status"),
                "CANDIDATE_ONLY",
            )
        if not close(input_data.get("outer_depth_mm"), EXPECTED_BASELINE_SIZE_MM["depth"]):
            add_check(
                checks,
                f"{cid}:depth_change_blocked",
                candidate.get("verification_level") == "blocked_until_depth_shell_rules",
                candidate.get("verification_level"),
                "blocked_until_depth_shell_rules",
            )
        if not close(input_data.get("outer_height_mm"), EXPECTED_BASELINE_SIZE_MM["height"]):
            add_check(
                checks,
                f"{cid}:height_change_blocked_current_phase",
                candidate.get("verification_level") == "blocked_current_phase_fixed_height",
                candidate.get("verification_level"),
                "blocked_current_phase_fixed_height",
            )
        if is_baseline_size and door_count not in EXPECTED_VERIFIED_DOOR_COUNTS:
            add_check(
                checks,
                f"{cid}:door_count_formula_candidate",
                candidate.get("verification_level") == "formula_candidate_needs_cad_validation",
                candidate.get("verification_level"),
                "formula_candidate_needs_cad_validation",
            )


def validate_csv_consistency(contract: dict[str, Any], csv_rows: list[dict[str, str]], checks: list[dict[str, Any]]) -> None:
    candidates = candidate_by_id(contract)
    rows = csv_by_candidate_id(csv_rows)
    add_check(checks, "csv_row_count", len(rows) == len(candidates), len(rows), len(candidates))
    for cid, candidate in candidates.items():
        row = rows.get(cid)
        add_check(checks, f"{cid}:csv_row_exists", row is not None, bool(row), True)
        if row is None:
            continue
        input_data = candidate["input"]
        layout = candidate["derived_layout"]
        add_check(checks, f"{cid}:csv_status", row.get("status") == candidate.get("status"), row.get("status"), candidate.get("status"))
        add_check(
            checks,
            f"{cid}:csv_handoff_enabled",
            str(row.get("enabled_for_engineering_handoff")).lower() == str(candidate.get("enabled_for_engineering_handoff")).lower(),
            row.get("enabled_for_engineering_handoff"),
            candidate.get("enabled_for_engineering_handoff"),
        )
        add_check(
            checks,
            f"{cid}:csv_geometry_review_enabled",
            str(row.get("enabled_for_geometry_review_handoff")).lower() == str(candidate.get("enabled_for_geometry_review_handoff")).lower(),
            row.get("enabled_for_geometry_review_handoff"),
            candidate.get("enabled_for_geometry_review_handoff"),
        )
        for key in ("outer_width_mm", "outer_height_mm", "outer_depth_mm", "door_width_mm"):
            add_check(checks, f"{cid}:csv_{key}", close(row.get(key), input_data.get(key)), row.get(key), input_data.get(key))
        for key in ("door_height_mm", "door_pitch_mm", "shelf_count", "front_frame_crossbar_count"):
            add_check(checks, f"{cid}:csv_{key}", close(row.get(key), layout.get(key)), row.get(key), layout.get(key))


def validate_door_width_evidence_layers(contract: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    layers = {
        str(layer.get("id")): layer
        for layer in contract.get("door_width_evidence_layers", [])
        if isinstance(layer, dict) and layer.get("id")
    }
    expected_ids = {
        "door_panel_W329_historical_single_part",
        "door_panel_W437_verified_baseline",
        "door_panel_W537_1200W_candidate",
        "width_marker_18door_W784",
    }
    add_check(checks, "door_width_evidence_layer_ids", set(layers) == expected_ids, sorted(layers), sorted(expected_ids))

    w329 = layers.get("door_panel_W329_historical_single_part", {})
    add_check(
        checks,
        "door_width_W329_is_not_handoff_enabled",
        w329.get("engineering_handoff_enabled") is False
        and w329.get("status") == "historical_capability_needs_current_gate_rebind",
        w329,
        "historical only; no current handoff",
    )

    w437 = layers.get("door_panel_W437_verified_baseline", {})
    add_check(
        checks,
        "door_width_W437_is_verified_baseline",
        w437.get("engineering_handoff_enabled") is True
        and w437.get("status") == "solidworks_native_pack_and_go_verified"
        and w437.get("verified_door_counts") == EXPECTED_VERIFIED_DOOR_COUNTS,
        w437,
        "verified 10/12/14 baseline",
    )

    w537 = layers.get("door_panel_W537_1200W_candidate", {})
    width_gate = read_json(WIDTH_CANDIDATE_DOOR_CHAIN_GATE_JSON_PATH) if WIDTH_CANDIDATE_DOOR_CHAIN_GATE_JSON_PATH.exists() else {}
    skeleton_gate = (
        read_json(WIDTH_CANDIDATE_SKELETON_SEED_GATE_JSON_PATH)
        if WIDTH_CANDIDATE_SKELETON_SEED_GATE_JSON_PATH.exists()
        else {}
    )
    bundle_gate = (
        read_json(WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_JSON_PATH)
        if WIDTH_CANDIDATE_GEOMETRY_BUNDLE_GATE_JSON_PATH.exists()
        else {}
    )
    add_check(
        checks,
        "door_width_W537_geometry_bundle_pass_not_handoff",
        w537.get("engineering_handoff_enabled") is False
        and w537.get("status") == "geometry_review_bundle_pass"
        and width_gate.get("status") == "PASS"
        and skeleton_gate.get("status") == "PASS"
        and bundle_gate.get("status") == "PASS"
        and as_int(width_gate.get("failed_error_count")) == 0
        and as_int(skeleton_gate.get("failed_error_count")) == 0
        and as_int(bundle_gate.get("failed_error_count")) == 0,
        {
            "layer": w537,
            "door_chain_gate_status": width_gate.get("status"),
            "skeleton_seed_gate_status": skeleton_gate.get("status"),
            "geometry_bundle_gate_status": bundle_gate.get("status"),
            "door_chain_failures": width_gate.get("failed_error_count"),
            "skeleton_seed_failures": skeleton_gate.get("failed_error_count"),
            "geometry_bundle_failures": bundle_gate.get("failed_error_count"),
        },
        "door-chain, skeleton seed and geometry-review bundle PASS, production handoff still disabled",
    )

    w784 = layers.get("width_marker_18door_W784", {})
    add_check(
        checks,
        "width_marker_18door_W784_stays_review_only",
        w784.get("engineering_handoff_enabled") is False and w784.get("status") == "source_confirmation_required",
        w784,
        "review only until source confirmation",
    )

    interpretations = contract.get("door_width_interpretation", [])
    add_check(
        checks,
        "door_width_interpretation_separates_panel_and_cabinet_width",
        any("not automatically full-cabinet width validation" in str(item) for item in interpretations)
        and any("geometry-review bundle gates" in str(item) for item in interpretations),
        interpretations,
        "panel width markers are separated from full-cabinet gates",
    )


def validate_csharp_handedness(contract: dict[str, Any], checks: list[dict[str, Any]]) -> None:
    door_weld = read_text(DOOR_WELD_CS_PATH)
    ordinary = read_text(ORDINARY_DOOR_CS_PATH)
    door_array = read_text(DOOR_ARRAY_CS_PATH)
    fixed = contract.get("fixed_handedness_contract", {})

    add_check(
        checks,
        "csharp_u_hook_pad_tx_matches_contract",
        'handedness == "right" ? -(doorHalfWidth - 15.0) : doorHalfWidth - 12.2' in door_weld
        and fixed.get("u_hook_pad_placement_tx_mm") == {"L": 206.3, "R": -203.5},
        fixed.get("u_hook_pad_placement_tx_mm"),
        "baseline L=206.3/R=-203.5 from width-aware edge offsets",
    )
    add_check(
        checks,
        "csharp_hinge_pin_tx_matches_contract",
        "double hingeX = -(doorHalfWidth - 10.0) * side;" in ordinary
        and fixed.get("hinge_pin_tx_mm") == {"L": -208.5, "R": 208.5},
        fixed.get("hinge_pin_tx_mm"),
        "baseline L=-208.5/R=208.5 from width-aware edge offsets",
    )
    add_check(
        checks,
        "csharp_electric_lock_hook_tx_matches_contract",
        "double lockHookX = (doorHalfWidth - 15.0) * side;" in ordinary
        and 'new Placement("electric_lock_hook", lockHook, Identity(), lockHookX' in ordinary
        and fixed.get("electric_lock_hook_tx_mm") == {"L": 203.5, "R": -203.5},
        fixed.get("electric_lock_hook_tx_mm"),
        "baseline L=203.5/R=-203.5 from width-aware edge offsets",
    )
    add_check(
        checks,
        "csharp_door_array_columns_are_width_aware",
        "double columnCenterX = doorWidth / 2.0 + 40.0;" in door_array
        and "double[] columns = { -columnCenterX, columnCenterX };" in door_array,
        "BuildDoorArrayModule.cs column formula",
        "columnCenterX = doorWidth / 2 + 40",
    )
    add_check(
        checks,
        "csharp_right_door_uses_right_module",
        "string doorAsm = c == 0 ? leftDoorAsm : rightDoorAsm;" in door_array,
        "doorAsm selection",
        "rightDoorAsm for right column",
    )
    add_check(
        checks,
        "csharp_right_column_identity_transform",
        "var p = new Placement(role, doorAsm, Identity(), columns[c], compensatedCenterY, 0);" in door_array,
        "Placement Identity()",
        "identity transform for both columns",
    )
    add_check(
        checks,
        "csharp_right_y_compensation_zero",
        "private const double RightColumnMirrorYCompensationMm = 0.0;" in door_array,
        "RightColumnMirrorYCompensationMm",
        0.0,
    )


def write_json(payload: dict[str, Any]) -> None:
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def write_csv(payload: dict[str, Any]) -> None:
    OUTPUT_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["name", "ok", "severity", "actual", "expected", "detail"])
        writer.writeheader()
        for check in payload["checks"]:
            writer.writerow(
                {
                    "name": check["name"],
                    "ok": check["ok"],
                    "severity": check["severity"],
                    "actual": json.dumps(check["actual"], ensure_ascii=False),
                    "expected": json.dumps(check["expected"], ensure_ascii=False),
                    "detail": check["detail"],
                }
            )


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 Dimension Contract Validation",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Status: `{payload['status']}`",
        f"- Check count: `{payload['check_count']}`",
        f"- Failed error count: `{payload['failed_error_count']}`",
        "",
        "| Check | Status | Actual | Expected |",
        "| --- | --- | --- | --- |",
    ]
    for check in payload["checks"]:
        status = "PASS" if check["ok"] else f"FAIL/{check['severity']}"
        lines.append(
            "| "
            + " | ".join(
                [
                    str(check["name"]),
                    status,
                    json.dumps(check["actual"], ensure_ascii=False),
                    json.dumps(check["expected"], ensure_ascii=False),
                ]
            )
            + " |"
        )
    lines.append("")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    checks: list[dict[str, Any]] = []
    contract = read_json(CONTRACT_JSON_PATH)
    verified_packet = read_json(VERIFIED_RULE_PACKET_PATH)
    csv_rows = read_csv_rows(CONTRACT_CSV_PATH)

    validate_contract_header(contract, verified_packet, checks)
    for candidate in contract.get("candidates", []):
        if isinstance(candidate, dict):
            validate_candidate_math(candidate, checks)
    validate_baseline_candidates(contract, verified_packet, checks)
    validate_nonbaseline_blockers(contract, checks)
    validate_csv_consistency(contract, csv_rows, checks)
    validate_door_width_evidence_layers(contract, checks)
    validate_csharp_handedness(contract, checks)

    failed_errors = [check for check in checks if not check["ok"] and check["severity"] == "error"]
    payload = {
        "generated_at": now_local_iso(),
        "status": "PASS" if not failed_errors else "FAIL",
        "contract_json": str(CONTRACT_JSON_PATH),
        "contract_csv": str(CONTRACT_CSV_PATH),
        "verified_rule_packet_json": str(VERIFIED_RULE_PACKET_PATH),
        "check_count": len(checks),
        "failed_error_count": len(failed_errors),
        "checks": checks,
        "output_paths": {
            "json": str(OUTPUT_JSON_PATH),
            "markdown": str(OUTPUT_MARKDOWN_PATH),
            "csv": str(OUTPUT_CSV_PATH),
        },
    }
    write_json(payload)
    write_markdown(payload)
    write_csv(payload)
    print(json.dumps({"status": payload["status"], "check_count": len(checks), "failed_error_count": len(failed_errors)}, ensure_ascii=False))
    return 0 if not failed_errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
