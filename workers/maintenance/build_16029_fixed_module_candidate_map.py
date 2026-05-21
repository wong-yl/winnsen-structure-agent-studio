from __future__ import annotations

import csv
import json
import math
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
VERIFY_CSV_PATH = (
    ROOT_DIR
    / "workers"
    / "handoffs"
    / "16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519"
    / "12door"
    / "locker_16029_12door_rule_driven_verify.csv"
)
BBOX_SUMMARY_PATH = (
    ROOT_DIR
    / "workers"
    / "rule_extractions"
    / "RULE-16029-FIXED-MODULE-CANDIDATES-20260521"
    / "candidate_bbox_summary.json"
)

DATA_JSON_PATH = ROOT_DIR / "data" / "solidworks_16029_fixed_module_candidate_map.json"
DATA_MD_PATH = ROOT_DIR / "data" / "solidworks_16029_fixed_module_candidate_map.md"
DATA_CSV_PATH = ROOT_DIR / "data" / "solidworks_16029_fixed_module_candidate_map.csv"

TARGET_MODULES = {"maintenance_door", "latch_system", "lock_system"}
BLOCKED_LOCAL_ONLY_IDS = {
    "rear_lower_door_assembly",
    "rear_lower_door_weld",
    "clothes_rail",
}
MODULE_ORDER = {
    "maintenance_door": 10,
    "latch_system": 20,
    "lock_system": 30,
}
RECOMMENDED_NATIVE_ROLES = {
    "maintenance_door_panel",
    "maintenance_door_lock_hole",
    "push_latch_mount_plate",
    "auto_lock_latch",
    "electronics_board_bracket",
    "lock_control_board_24ch",
    "switching_power_supply_assembly",
}
ROLE_OVERRIDES = {
    "maintenance_door_panel_from_sw": "maintenance_door_panel",
    "maintenance_door_lock_hole_from_sw": "maintenance_door_lock_hole",
    "push_latch_mount_plate": "push_latch_mount_plate",
    "auto_lock_latch_d3016z2": "auto_lock_latch",
    "electronics_board_bracket_from_sw": "electronics_board_bracket",
    "wifi_serial_server_from_sw": "wifi_serial_server",
    "lock_control_board_24ch_from_sw": "lock_control_board_24ch",
    "switching_power_supply_assembly_from_sw": "switching_power_supply_assembly",
    "m9_v11_from_sw": "m9_v11_board",
}


def now_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def parse_float(value: str | None) -> float | None:
    if value is None or value == "":
        return None
    try:
        numeric = float(value)
    except ValueError:
        return None
    if math.isnan(numeric) or math.isinf(numeric):
        return None
    return numeric


def rounded(value: float | None) -> float | None:
    return round(value, 3) if value is not None else None


def read_verify_rows() -> list[dict[str, str]]:
    with VERIFY_CSV_PATH.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def read_bbox_summary() -> list[dict[str, Any]]:
    if not BBOX_SUMMARY_PATH.exists():
        return []
    items_by_step: dict[str, dict[str, Any]] = {}
    with BBOX_SUMMARY_PATH.open("r", encoding="utf-8-sig") as handle:
        for item in json.load(handle):
            items_by_step[step_key(item.get("step"))] = item

    # Some small module bboxes are generated as individual FreeCAD evidence files
    # after the first coarse pass. Merge them when present so the placement gate
    # can detect parts that need rotation, not just translation.
    for path in BBOX_SUMMARY_PATH.parent.glob("*_bbox.json"):
        try:
            payload = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception:
            continue
        box = payload.get("assemblyBBoxMm")
        step = payload.get("sourceStep")
        if not isinstance(box, dict) or not step:
            continue
        items_by_step[step_key(step)] = {
            "id": path.stem.removesuffix("_bbox"),
            "step": step,
            "minX": box.get("minX"),
            "maxX": box.get("maxX"),
            "minY": box.get("minY"),
            "maxY": box.get("maxY"),
            "minZ": box.get("minZ"),
            "maxZ": box.get("maxZ"),
            "sizeX": box.get("sizeX"),
            "sizeY": box.get("sizeY"),
            "sizeZ": box.get("sizeZ"),
            "objects": payload.get("objectCount"),
        }
    return list(items_by_step.values())


def step_key(path: str | None) -> str:
    if not path:
        return ""
    return Path(path).stem.lower()


def role_from_row(row: dict[str, str]) -> str:
    key = step_key(row.get("step_source"))
    if key in ROLE_OVERRIDES:
        return ROLE_OVERRIDES[key]
    name = row.get("component_name") or row.get("item") or "fixed_module"
    safe = []
    for char in name.lower():
        if char.isalnum():
            safe.append(char)
        elif safe and safe[-1] != "_":
            safe.append("_")
    return "".join(safe).strip("_") or "fixed_module"


def bbox_from_row(row: dict[str, str]) -> dict[str, float | None]:
    return {
        "x_min": rounded(parse_float(row.get("x_min"))),
        "x_max": rounded(parse_float(row.get("x_max"))),
        "y_min": rounded(parse_float(row.get("y_min"))),
        "y_max": rounded(parse_float(row.get("y_max"))),
        "z_min": rounded(parse_float(row.get("z_min"))),
        "z_max": rounded(parse_float(row.get("z_max"))),
    }


def local_bbox_from_summary(item: dict[str, Any]) -> dict[str, float | None]:
    return {
        "x_min": rounded(parse_float(str(item.get("minX")))),
        "x_max": rounded(parse_float(str(item.get("maxX")))),
        "y_min": rounded(parse_float(str(item.get("minY")))),
        "y_max": rounded(parse_float(str(item.get("maxY")))),
        "z_min": rounded(parse_float(str(item.get("minZ")))),
        "z_max": rounded(parse_float(str(item.get("maxZ")))),
    }


def transform_from_row(row: dict[str, str]) -> dict[str, float | None]:
    return {
        "tx_mm": rounded(parse_float(row.get("x_center"))),
        "ty_mm": rounded(parse_float(row.get("y_center"))),
        "tz_mm": rounded(parse_float(row.get("z_center"))),
    }


def shifted_bbox(local_bbox: dict[str, float | None], transform: dict[str, float | None]) -> dict[str, float | None]:
    return {
        "x_min": add(local_bbox.get("x_min"), transform.get("tx_mm")),
        "x_max": add(local_bbox.get("x_max"), transform.get("tx_mm")),
        "y_min": add(local_bbox.get("y_min"), transform.get("ty_mm")),
        "y_max": add(local_bbox.get("y_max"), transform.get("ty_mm")),
        "z_min": add(local_bbox.get("z_min"), transform.get("tz_mm")),
        "z_max": add(local_bbox.get("z_max"), transform.get("tz_mm")),
    }


def add(left: float | None, right: float | None) -> float | None:
    if left is None or right is None:
        return None
    return rounded(left + right)


def bbox_error(expected: dict[str, float | None], actual: dict[str, float | None]) -> float | None:
    errors: list[float] = []
    for key in ("x_min", "x_max", "y_min", "y_max", "z_min", "z_max"):
        left = expected.get(key)
        right = actual.get(key)
        if left is None or right is None:
            continue
        errors.append(abs(left - right))
    return rounded(max(errors)) if errors else None


def build_candidates(rows: list[dict[str, str]], bbox_summary: list[dict[str, Any]]) -> list[dict[str, Any]]:
    summary_by_step = {step_key(item.get("step")): item for item in bbox_summary}
    candidates: list[dict[str, Any]] = []
    for row in rows:
        if row.get("type") != "sw_api_module_reference":
            continue
        module = row.get("module") or ""
        if module not in TARGET_MODULES:
            continue

        role = role_from_row(row)
        component_path = row.get("component_path") or ""
        component = Path(component_path)
        transform = transform_from_row(row)
        target_bbox = bbox_from_row(row)
        local_item = summary_by_step.get(step_key(row.get("step_source")))
        local_bbox = local_bbox_from_summary(local_item) if local_item else {}
        predicted_bbox = shifted_bbox(local_bbox, transform) if local_bbox else {}
        max_error = bbox_error(target_bbox, predicted_bbox) if predicted_bbox else None
        translation_only_safe = local_bbox != {} and max_error is not None and max_error <= 0.5
        native_exists = component.exists()
        file_type = component.suffix.upper().lstrip(".")
        can_place_native = native_exists and file_type in {"SLDPRT", "SLDASM"}
        if translation_only_safe:
            placement_strategy = "identity_transform_ready"
        elif local_bbox:
            placement_strategy = "requires_rotation_or_full_matrix"
        else:
            placement_strategy = "needs_local_bbox_evidence"

        candidates.append(
            {
                "role": role,
                "module": module,
                "component_name": row.get("component_name") or "",
                "component_path": component_path,
                "step_source": row.get("step_source") or "",
                "placement_transform_mm": transform,
                "target_bbox_mm": target_bbox,
                "source_local_bbox_mm": local_bbox,
                "predicted_target_bbox_mm": predicted_bbox,
                "local_bbox_match_max_error_mm": max_error,
                "transform_bbox_match_ok": translation_only_safe,
                "translation_only_safe": translation_only_safe,
                "native_exists": native_exists,
                "native_file_type": file_type,
                "can_place_native": can_place_native,
                "recommended_for_first_enriched_model": role in RECOMMENDED_NATIVE_ROLES and can_place_native and translation_only_safe,
                "placement_strategy": placement_strategy,
                "evidence_level": "sw_api_transform" if local_bbox else "sw_api_transform_without_local_bbox",
                "source_item": row.get("item") or "",
            }
        )

    candidates.sort(key=lambda item: (MODULE_ORDER.get(item["module"], 99), item["role"]))
    return candidates


def build_blocked_items(candidates: list[dict[str, Any]], bbox_summary: list[dict[str, Any]]) -> list[dict[str, Any]]:
    represented_steps = {step_key(candidate.get("step_source")) for candidate in candidates}
    blocked: list[dict[str, Any]] = []
    for item in bbox_summary:
        item_id = str(item.get("id") or "")
        if item_id not in BLOCKED_LOCAL_ONLY_IDS and step_key(item.get("step")) in represented_steps:
            continue
        if item_id not in BLOCKED_LOCAL_ONLY_IDS:
            continue
        blocked.append(
            {
                "id": item_id,
                "step": item.get("step") or "",
                "local_bbox_mm": local_bbox_from_summary(item),
                "objects": item.get("objects"),
                "placement_status": "blocked_no_transform",
                "reason": "Only local STEP bbox was found; no transform-backed assembly placement row was found in the 12-door verification CSV.",
            }
        )
    return blocked


def build_payload() -> dict[str, Any]:
    rows = read_verify_rows()
    bbox_summary = read_bbox_summary()
    candidates = build_candidates(rows, bbox_summary)
    blocked = build_blocked_items(candidates, bbox_summary)
    recommended = [item for item in candidates if item["recommended_for_first_enriched_model"]]
    return {
        "generated_at": now_iso(),
        "source_verify_csv": str(VERIFY_CSV_PATH),
        "source_bbox_summary": str(BBOX_SUMMARY_PATH),
        "target_modules": sorted(TARGET_MODULES),
        "summary": {
            "transform_backed_candidates": len(candidates),
            "native_placeable_candidates": sum(1 for item in candidates if item["can_place_native"]),
            "identity_transform_ready_candidates": sum(1 for item in candidates if item["translation_only_safe"]),
            "requires_rotation_or_full_matrix_candidates": sum(
                1 for item in candidates if item["placement_strategy"] == "requires_rotation_or_full_matrix"
            ),
            "recommended_first_enriched_model_candidates": len(recommended),
            "blocked_local_only_candidates": len(blocked),
            "all_recommended_native_placeable": all(item["can_place_native"] for item in recommended),
        },
        "candidates": candidates,
        "blocked_local_only_candidates": blocked,
        "notes": [
            "Use these rows as placement evidence for fixed modules only; do not infer placements from local bbox alone.",
            "The first enriched native SolidWorks sample should use the recommended rows on one 12-door skeleton before batch generation.",
            "Rear lower door and clothes rail are intentionally blocked until transform-backed placement evidence is extracted.",
        ],
    }


def write_json(payload: dict[str, Any]) -> None:
    DATA_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    DATA_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def write_csv(payload: dict[str, Any]) -> None:
    DATA_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "role",
        "module",
        "component_name",
        "component_path",
        "tx_mm",
        "ty_mm",
        "tz_mm",
        "x_min",
        "x_max",
        "y_min",
        "y_max",
        "z_min",
        "z_max",
        "native_exists",
        "can_place_native",
        "recommended_for_first_enriched_model",
        "evidence_level",
        "local_bbox_match_max_error_mm",
        "placement_strategy",
    ]
    with DATA_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for item in payload["candidates"]:
            transform = item["placement_transform_mm"]
            bbox = item["target_bbox_mm"]
            writer.writerow(
                {
                    "role": item["role"],
                    "module": item["module"],
                    "component_name": item["component_name"],
                    "component_path": item["component_path"],
                    "tx_mm": transform["tx_mm"],
                    "ty_mm": transform["ty_mm"],
                    "tz_mm": transform["tz_mm"],
                    "x_min": bbox["x_min"],
                    "x_max": bbox["x_max"],
                    "y_min": bbox["y_min"],
                    "y_max": bbox["y_max"],
                    "z_min": bbox["z_min"],
                    "z_max": bbox["z_max"],
                    "native_exists": item["native_exists"],
                    "can_place_native": item["can_place_native"],
                    "recommended_for_first_enriched_model": item["recommended_for_first_enriched_model"],
                    "evidence_level": item["evidence_level"],
                    "local_bbox_match_max_error_mm": item["local_bbox_match_max_error_mm"],
                    "placement_strategy": item["placement_strategy"],
                }
            )


def write_markdown(payload: dict[str, Any]) -> None:
    summary = payload["summary"]
    lines = [
        "# 16029 Fixed Module Candidate Map",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Source verify CSV: `{payload['source_verify_csv']}`",
        f"- Transform-backed candidates: `{summary['transform_backed_candidates']}`",
        f"- Native-placeable candidates: `{summary['native_placeable_candidates']}`",
        f"- Identity-transform-ready candidates: `{summary['identity_transform_ready_candidates']}`",
        f"- Requires rotation/full-matrix candidates: `{summary['requires_rotation_or_full_matrix_candidates']}`",
        f"- Recommended first enriched model candidates: `{summary['recommended_first_enriched_model_candidates']}`",
        f"- Blocked local-only candidates: `{summary['blocked_local_only_candidates']}`",
        "",
        "## Recommended First Enriched 12-Door Model",
        "",
        "| role | module | tx | ty | tz | native | bbox evidence |",
        "|---|---:|---:|---:|---:|---|---|",
    ]
    for item in payload["candidates"]:
        if not item["recommended_for_first_enriched_model"]:
            continue
        transform = item["placement_transform_mm"]
        error = item["local_bbox_match_max_error_mm"]
        evidence = "transform"
        if error is not None:
            evidence = f"transform + local bbox, max error {error} mm"
        lines.append(
            "| {role} | {module} | {tx} | {ty} | {tz} | {native} | {evidence} |".format(
                role=item["role"],
                module=item["module"],
                tx=transform["tx_mm"],
                ty=transform["ty_mm"],
                tz=transform["tz_mm"],
                native="yes" if item["can_place_native"] else "no",
                evidence=evidence,
            )
        )

    lines.extend(
        [
            "",
            "## Not Ready For Translation-Only Placement",
            "",
            "| role | module | strategy | max bbox error mm |",
            "|---|---|---|---:|",
        ]
    )
    for item in payload["candidates"]:
        if item["placement_strategy"] == "identity_transform_ready":
            continue
        lines.append(
            "| {role} | {module} | {strategy} | {error} |".format(
                role=item["role"],
                module=item["module"],
                strategy=item["placement_strategy"],
                error=item["local_bbox_match_max_error_mm"],
            )
        )

    lines.extend(
        [
            "",
            "## Blocked Until More Evidence",
            "",
            "| id | reason |",
            "|---|---|",
        ]
    )
    for item in payload["blocked_local_only_candidates"]:
        lines.append(f"| {item['id']} | {item['reason']} |")

    lines.extend(
        [
            "",
            "## Rule",
            "",
            "Only transform-backed rows should enter the SolidWorks generator. Local STEP bbox can describe part size, but it is not enough to place the part in the cabinet.",
            "",
        ]
    )
    DATA_MD_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    payload = build_payload()
    write_json(payload)
    write_csv(payload)
    write_markdown(payload)
    print(json.dumps(payload["summary"], ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
