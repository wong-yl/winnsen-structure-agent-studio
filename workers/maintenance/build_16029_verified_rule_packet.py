from __future__ import annotations

import csv
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
BASE_RULE_PACKET_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_RULE_PACKET_JSON", ROOT_DIR / "data" / "locker_16029_variant_rule_packet.json")
)
SKELETON_GATE_PATH = Path(
    os.getenv(
        "STUDIO_16029_NATIVE_SKELETON_GATE_JSON",
        ROOT_DIR / "data" / "solidworks_16029_native_skeleton_geometry_gate.json",
    )
)
HANDOFF_DIR = Path(
    os.getenv(
        "STUDIO_16029_ENGINEERING_HANDOFF_DIR",
        ROOT_DIR / "workers" / "handoffs" / "16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519",
    )
)
HANDOFF_QUALITY_CSV = HANDOFF_DIR / "handoff_quality_summary.csv"
PACK_AND_GO_CSV = HANDOFF_DIR / "solidworks_pack_and_go_summary.csv"
PACK_AND_GO_INDEPENDENCE_CSV = HANDOFF_DIR / "solidworks_pack_and_go_independence_summary.csv"

OUTPUT_JSON_PATH = Path(
    os.getenv("STUDIO_16029_VERIFIED_RULE_PACKET_JSON", ROOT_DIR / "data" / "locker_16029_verified_rule_packet.json")
)
OUTPUT_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_VERIFIED_RULE_PACKET_MD", ROOT_DIR / "data" / "locker_16029_verified_rule_packet.md")
)
OUTPUT_CSV_PATH = Path(
    os.getenv("STUDIO_16029_VERIFIED_RULE_PACKET_CSV", ROOT_DIR / "data" / "locker_16029_verified_rule_packet.csv")
)

VERIFIED_DOOR_COUNTS = [10, 12, 14]
FORMULA_CANDIDATE_DOOR_COUNTS = [8, 16]

CABINET_RULES = {
    "product_family": "16029",
    "nominal_width_mm": 1000.0,
    "nominal_height_mm": 1917.0,
    "nominal_depth_mm": 550.0,
    "door_area_bottom_y_mm": 30.0,
    "door_area_top_y_mm": 1857.0,
    "door_area_height_mm": 1827.0,
    "door_grid_top_gap_mm": 2.0,
    "door_grid_bottom_gap_mm": 2.0,
    "visual_gap_mm": 7.0,
    "door_width_mm": 437.0,
    "door_flat_width_extra_mm": 38.4,
    "door_flat_height_extra_mm": 36.4,
    "hinge_axis_abs_x_mm": 467.0,
    "lock_center_abs_x_mm": 55.0,
}

RIGHT_COLUMN_ROTATION_Z_180 = [[-1, 0, 0], [0, -1, 0], [0, 0, 1]]
RIGHT_COLUMN_MIRROR_Y_COMPENSATION_MM = -51.7


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise FileNotFoundError(path)
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def as_int(value: Any, default: int = 0) -> int:
    try:
        return int(float(str(value)))
    except (TypeError, ValueError):
        return default


def as_float(value: Any, default: float = 0.0) -> float:
    try:
        return float(str(value))
    except (TypeError, ValueError):
        return default


def index_by_int(rows: list[dict[str, str]], key: str) -> dict[int, dict[str, str]]:
    return {as_int(row.get(key)): row for row in rows if row.get(key) not in (None, "")}


def gate_variant_by_door(gate_payload: dict[str, Any]) -> dict[int, dict[str, Any]]:
    variants: dict[int, dict[str, Any]] = {}
    for variant in gate_payload.get("variants", []):
        if isinstance(variant, dict):
            variants[as_int(variant.get("door_count"))] = variant
    return variants


def base_variant_by_door(base_payload: dict[str, Any]) -> dict[int, dict[str, Any]]:
    variants: dict[int, dict[str, Any]] = {}
    for variant in base_payload.get("variants", []):
        if isinstance(variant, dict):
            variants[as_int(variant.get("door_count"))] = variant
    return variants


def expected_layout(door_count: int) -> dict[str, Any]:
    rows_per_column = door_count // 2
    door_height = (
        CABINET_RULES["door_area_height_mm"]
        - CABINET_RULES["door_grid_top_gap_mm"]
        - CABINET_RULES["door_grid_bottom_gap_mm"]
        - (rows_per_column - 1) * CABINET_RULES["visual_gap_mm"]
    ) / rows_per_column
    door_pitch = door_height + CABINET_RULES["visual_gap_mm"]
    return {
        "rows_per_column": rows_per_column,
        "door_height_mm": round(door_height, 3),
        "door_pitch_mm": round(door_pitch, 3),
        "left_column_door_count": rows_per_column,
        "right_column_door_count": rows_per_column,
        "door_width_mm": CABINET_RULES["door_width_mm"],
        "door_flat_estimate_mm": {
            "width": round(CABINET_RULES["door_width_mm"] + CABINET_RULES["door_flat_width_extra_mm"], 3),
            "height": round(door_height + CABINET_RULES["door_flat_height_extra_mm"], 3),
        },
    }


def expected_counts(door_count: int) -> dict[str, int]:
    rows_per_column = door_count // 2
    between_row_pair_count = (rows_per_column - 1) * 2
    return {
        "door_modules": door_count,
        "door_panel_features": door_count,
        "hinge_pins": door_count,
        "lock_hook_pads": door_count,
        "electric_lock_hooks": door_count,
        "shelves": between_row_pair_count,
        "front_frame_crossbars": between_row_pair_count,
        "cabinet_vertical_dividers": 2,
        "door_frame_vertical_dividers": 2,
    }


def build_verified_variant(
    door_count: int,
    base_variant: dict[str, Any],
    gate_variant: dict[str, Any],
    quality_row: dict[str, str],
    pack_row: dict[str, str],
    independence_row: dict[str, str],
) -> dict[str, Any]:
    layout = expected_layout(door_count)
    counts = expected_counts(door_count)
    root_bbox = gate_variant.get("root_bbox", {}) if isinstance(gate_variant, dict) else {}
    return {
        "door_count": door_count,
        "verification_level": "solidworks_native_pack_and_go_verified",
        "output_level": "engineering_reference_model",
        "enabled_for_engineering_handoff": True,
        "scope": "16029 1000W x 1917H x 550D same-outer-size door-count variant",
        "layout_rule": layout,
        "component_count_rule": counts,
        "placement_rule": {
            "left_and_right_columns_must_have_equal_row_count": True,
            "right_column_mirror_rule": "right door column must use 180deg Z rotation plus source-origin Y compensation, not a copied left-door orientation",
            "right_column_rotation_matrix": RIGHT_COLUMN_ROTATION_Z_180,
            "right_column_mirror_y_compensation_mm": RIGHT_COLUMN_MIRROR_Y_COMPENSATION_MM,
            "left_right_exported_door_bbox_y_delta_max_mm": 0.1,
            "door_pitch_drives_shelf_and_crossbar_pitch": True,
            "shelf_and_crossbar_count_formula": "(rows_per_column - 1) * 2",
            "lock_hinge_and_hook_count_formula": "door_count",
        },
        "measured_gate": {
            "native_skeleton_status": gate_variant.get("status"),
            "native_skeleton_check_count": gate_variant.get("check_count"),
            "native_skeleton_failed_error_count": gate_variant.get("failed_error_count"),
            "root_bbox_mm": {
                "x_len": root_bbox.get("x_len"),
                "y_max": root_bbox.get("y_max"),
                "z_len": root_bbox.get("z_len"),
            },
            "handoff_native_validation_ok": quality_row.get("native_validation_ok"),
            "handoff_native_failed_check_count": as_int(quality_row.get("native_failed_check_count")),
            "dependency_existing_count": as_int(quality_row.get("dependency_existing_count")),
            "dependency_row_count": as_int(quality_row.get("dependency_row_count")),
            "dependency_missing_count": as_int(quality_row.get("dependency_missing_count")),
            "reported_door_count": as_int(quality_row.get("reported_door_count")),
            "left_column_door_count": as_int(quality_row.get("left_column_door_count")),
            "right_column_door_count": as_int(quality_row.get("right_column_door_count")),
            "right_column_rotation_ok": quality_row.get("right_column_rotation_ok"),
            "left_right_y_alignment_ok": quality_row.get("left_right_y_alignment_ok"),
            "left_right_door_bbox_y_delta": as_float(quality_row.get("left_right_door_bbox_y_delta")),
            "left_right_door_placement_y_delta": as_float(quality_row.get("left_right_door_placement_y_delta")),
            "shelf_count": as_int(quality_row.get("shelf_count")),
            "crossbar_count": as_int(quality_row.get("crossbar_count")),
        },
        "pack_and_go_handoff": {
            "ok": pack_row.get("ok"),
            "file_count": as_int(pack_row.get("file_count")),
            "sldasm_count": as_int(pack_row.get("sldasm_count")),
            "sldprt_count": as_int(pack_row.get("sldprt_count")),
            "total_mb": as_float(pack_row.get("total_mb")),
            "package_dir": pack_row.get("out_dir"),
            "top_assembly": pack_row.get("top_assembly"),
            "opened_for_independence_check": independence_row.get("opened"),
            "external_top_reference_count": as_int(independence_row.get("external_top_reference_count")),
            "missing_top_reference_path_count": as_int(independence_row.get("missing_top_reference_path_count")),
            "preview": independence_row.get("preview"),
        },
        "source_evidence": {
            "base_rule_packet_variant": base_variant.get("source_mode"),
            "validation_report": quality_row.get("validation_report"),
            "native_assembly": quality_row.get("native_assembly"),
            "open_script": quality_row.get("open_script"),
        },
        "quality_gates": [
            "native_skeleton_status must be PASS",
            "handoff_native_validation_ok must be yes",
            "dependency_missing_count must be 0",
            "right_column_rotation_ok must be yes",
            "left_right_y_alignment_ok must be yes and left_right_door_bbox_y_delta must be <= 0.1",
            "reported_door_count, lock, hinge and hook counts must equal door_count",
            "shelf_count and crossbar_count must equal (rows_per_column - 1) * 2",
            "Pack-and-Go ok must be True and external_top_reference_count must be 0",
        ],
    }


def build_candidate_variant(door_count: int) -> dict[str, Any]:
    return {
        "door_count": door_count,
        "verification_level": "formula_candidate_only",
        "output_level": "not_enabled_for_engineering_handoff",
        "enabled_for_engineering_handoff": False,
        "layout_rule": expected_layout(door_count),
        "component_count_rule": expected_counts(door_count),
        "blocked_reason": "formula is available, but this door count has not passed native SolidWorks and Pack-and-Go verification",
    }


def validate_payload(payload: dict[str, Any]) -> list[str]:
    problems: list[str] = []
    for variant in payload["verified_variants"]:
        door_count = variant["door_count"]
        layout = variant["layout_rule"]
        counts = variant["component_count_rule"]
        measured = variant["measured_gate"]
        pack = variant["pack_and_go_handoff"]
        if measured["native_skeleton_status"] != "PASS":
            problems.append(f"{door_count}door native skeleton status is not PASS")
        if measured["handoff_native_validation_ok"] != "yes":
            problems.append(f"{door_count}door native handoff validation is not yes")
        if measured["dependency_missing_count"] != 0:
            problems.append(f"{door_count}door has missing dependencies")
        if measured["right_column_rotation_ok"] != "yes":
            problems.append(f"{door_count}door right column mirror/rotation is not verified")
        if measured["left_right_y_alignment_ok"] != "yes":
            problems.append(f"{door_count}door left/right exported door bbox Y alignment is not verified")
        if measured["left_right_door_bbox_y_delta"] > 0.1:
            problems.append(f"{door_count}door left/right exported door bbox Y delta is over 0.1mm")
        if measured["reported_door_count"] != door_count:
            problems.append(f"{door_count}door measured door count mismatch")
        if measured["left_column_door_count"] != layout["rows_per_column"]:
            problems.append(f"{door_count}door left column count mismatch")
        if measured["right_column_door_count"] != layout["rows_per_column"]:
            problems.append(f"{door_count}door right column count mismatch")
        if measured["shelf_count"] != counts["shelves"]:
            problems.append(f"{door_count}door shelf count mismatch")
        if measured["crossbar_count"] != counts["front_frame_crossbars"]:
            problems.append(f"{door_count}door crossbar count mismatch")
        if pack["ok"] != "True":
            problems.append(f"{door_count}door Pack-and-Go is not ok")
        if pack["external_top_reference_count"] != 0:
            problems.append(f"{door_count}door Pack-and-Go still has external top references")
        if pack["missing_top_reference_path_count"] != 0:
            problems.append(f"{door_count}door Pack-and-Go still has empty top reference paths")
    return problems


def build_payload() -> dict[str, Any]:
    base_packet = read_json(BASE_RULE_PACKET_PATH)
    skeleton_gate = read_json(SKELETON_GATE_PATH)
    quality_rows = index_by_int(read_csv(HANDOFF_QUALITY_CSV), "door_count")
    pack_rows = index_by_int(read_csv(PACK_AND_GO_CSV), "door")
    independence_rows = index_by_int(read_csv(PACK_AND_GO_INDEPENDENCE_CSV), "door")
    base_variants = base_variant_by_door(base_packet)
    gate_variants = gate_variant_by_door(skeleton_gate)

    verified_variants = [
        build_verified_variant(
            door_count,
            base_variants.get(door_count, {}),
            gate_variants.get(door_count, {}),
            quality_rows.get(door_count, {}),
            pack_rows.get(door_count, {}),
            independence_rows.get(door_count, {}),
        )
        for door_count in VERIFIED_DOOR_COUNTS
    ]
    payload: dict[str, Any] = {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "purpose": "Verified rule packet for same-outer-size 16029 locker door-count generation.",
        "scope": "1000W x 1917H x 550D; currently verified for 10/12/14 doors only.",
        "status": "PASS",
        "cabinet_rule": CABINET_RULES,
        "verified_door_counts": VERIFIED_DOOR_COUNTS,
        "formula_candidate_door_counts": FORMULA_CANDIDATE_DOOR_COUNTS,
        "verified_variants": verified_variants,
        "formula_candidates_not_enabled": [build_candidate_variant(door_count) for door_count in FORMULA_CANDIDATE_DOOR_COUNTS],
        "generator_contract": {
            "use_this_packet_as_single_source_of_truth": True,
            "must_not_enable_unverified_door_count_for_engineering_handoff": True,
            "right_column_doors_must_be_mirrored": True,
            "shelves_and_crossbars_must_recompute_from_rows_per_column": True,
            "locks_hinges_hooks_must_recompute_from_door_count": True,
            "solidworks_native_pack_and_go_is_current_engineering_handoff_route": True,
        },
        "source_files": {
            "base_rule_packet": str(BASE_RULE_PACKET_PATH),
            "solidworks_native_skeleton_gate": str(SKELETON_GATE_PATH),
            "handoff_quality_summary": str(HANDOFF_QUALITY_CSV),
            "pack_and_go_summary": str(PACK_AND_GO_CSV),
            "pack_and_go_independence_summary": str(PACK_AND_GO_INDEPENDENCE_CSV),
        },
        "output_files": {
            "json": str(OUTPUT_JSON_PATH),
            "markdown": str(OUTPUT_MARKDOWN_PATH),
            "csv": str(OUTPUT_CSV_PATH),
        },
    }
    problems = validate_payload(payload)
    payload["status"] = "PASS" if not problems else "FAIL"
    payload["validation_problems"] = problems
    return payload


def write_csv(payload: dict[str, Any]) -> None:
    rows: list[dict[str, Any]] = []
    for variant in payload["verified_variants"]:
        layout = variant["layout_rule"]
        counts = variant["component_count_rule"]
        measured = variant["measured_gate"]
        pack = variant["pack_and_go_handoff"]
        rows.append(
            {
                "door_count": variant["door_count"],
                "verification_level": variant["verification_level"],
                "enabled_for_engineering_handoff": variant["enabled_for_engineering_handoff"],
                "rows_per_column": layout["rows_per_column"],
                "door_width_mm": layout["door_width_mm"],
                "door_height_mm": layout["door_height_mm"],
                "door_pitch_mm": layout["door_pitch_mm"],
                "door_modules": counts["door_modules"],
                "shelves": counts["shelves"],
                "front_frame_crossbars": counts["front_frame_crossbars"],
                "hinge_pins": counts["hinge_pins"],
                "lock_hook_pads": counts["lock_hook_pads"],
                "electric_lock_hooks": counts["electric_lock_hooks"],
                "right_column_rotation_ok": measured["right_column_rotation_ok"],
                "left_right_y_alignment_ok": measured["left_right_y_alignment_ok"],
                "left_right_door_bbox_y_delta": measured["left_right_door_bbox_y_delta"],
                "left_right_door_placement_y_delta": measured["left_right_door_placement_y_delta"],
                "native_skeleton_status": measured["native_skeleton_status"],
                "native_failed_errors": measured["native_skeleton_failed_error_count"],
                "handoff_native_validation_ok": measured["handoff_native_validation_ok"],
                "dependency_missing_count": measured["dependency_missing_count"],
                "pack_and_go_ok": pack["ok"],
                "pack_and_go_file_count": pack["file_count"],
                "pack_and_go_sldasm_count": pack["sldasm_count"],
                "pack_and_go_sldprt_count": pack["sldprt_count"],
                "pack_and_go_total_mb": pack["total_mb"],
                "external_top_reference_count": pack["external_top_reference_count"],
                "missing_top_reference_path_count": pack["missing_top_reference_path_count"],
            }
        )
    OUTPUT_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 已验证门数变化规则包",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Status: `{payload['status']}`",
        f"- Scope: `{payload['scope']}`",
        "",
        "## 一句话结论",
        "",
        "10/12/14 门已经形成同外形 16029 柜体的已验证规则包；后续生成器必须按这里的门高、门距、左右镜像、层板/横隔板、锁具/合页数量规则生成，8/16 门暂时只能作为公式候选，不能交给工程师复核。",
        "",
        "## 已验证规则",
        "",
        "| 门数 | 每列 | 门高 | 门距 | 门宽 | 门模块 | 层板 | 横隔板 | 锁/钩/合页 | 右门镜像 | Pack-and-Go | 外部引用 |",
        "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: |",
    ]
    for variant in payload["verified_variants"]:
        layout = variant["layout_rule"]
        counts = variant["component_count_rule"]
        measured = variant["measured_gate"]
        pack = variant["pack_and_go_handoff"]
        lines.append(
            "| "
            + " | ".join(
                [
                    str(variant["door_count"]),
                    str(layout["rows_per_column"]),
                    f"{layout['door_height_mm']} mm",
                    f"{layout['door_pitch_mm']} mm",
                    f"{layout['door_width_mm']} mm",
                    str(counts["door_modules"]),
                    str(counts["shelves"]),
                    str(counts["front_frame_crossbars"]),
                    str(counts["electric_lock_hooks"]),
                    f"{measured['right_column_rotation_ok'] or ''}; bbox_d={measured['left_right_door_bbox_y_delta']}mm",
                    pack["ok"] or "",
                    str(pack["external_top_reference_count"]),
                ]
            )
            + " |"
        )
    lines.extend(
        [
            "",
            "## 固化给生成器的约束",
            "",
            "- `rows_per_column = door_count / 2`，当前仅允许 10/12/14 门进入工程交接。",
            "- `door_height = (1827 - 2 - 2 - (rows_per_column - 1) * 7) / rows_per_column`。",
            "- `door_pitch = door_height + 7`，层板和门框横隔板必须跟随同一 pitch。",
            "- 层板数和门框横隔板数必须等于 `(rows_per_column - 1) * 2`。",
            "- 门模块、门板特征、合页销、锁钩垫、电控 U 型锁钩数量必须等于 `door_count`。",
            "- 右侧门列必须使用 180deg Z 旋转 + source-origin Y compensation，且左右门导出 bbox Y delta <= 0.1mm。",
            "- Pack-and-Go 交接包必须外部顶层引用为 0 才能交给工程师。",
            "",
            "## 暂不放开的公式候选",
            "",
            "| 门数 | 每列 | 门高 | 门距 | 状态 |",
            "| ---: | ---: | ---: | ---: | --- |",
        ]
    )
    for candidate in payload["formula_candidates_not_enabled"]:
        layout = candidate["layout_rule"]
        lines.append(
            f"| {candidate['door_count']} | {layout['rows_per_column']} | {layout['door_height_mm']} mm | {layout['door_pitch_mm']} mm | 未通过 CAD 验证，不能工程交接 |"
        )
    lines.extend(["", "## 证据文件", ""])
    for label, path in payload["source_files"].items():
        lines.append(f"- {label}: `{path}`")
    if payload["validation_problems"]:
        lines.extend(["", "## Validation Problems", ""])
        for problem in payload["validation_problems"]:
            lines.append(f"- {problem}")
    lines.append("")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    payload = build_payload()
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_csv(payload)
    write_markdown(payload)
    print(
        json.dumps(
            {
                "status": payload["status"],
                "verified_door_counts": payload["verified_door_counts"],
                "validation_problem_count": len(payload["validation_problems"]),
                "json": str(OUTPUT_JSON_PATH),
                "markdown": str(OUTPUT_MARKDOWN_PATH),
                "csv": str(OUTPUT_CSV_PATH),
            },
            ensure_ascii=False,
        )
    )
    if payload["status"] != "PASS":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
