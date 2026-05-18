from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
CHECKLIST_JSON = ROOT / "data" / "rule_seed_evidence_checklist.json"
EXTRACTIONS_DIR = ROOT / "workers" / "rule_extractions"
VARIANT_STEP_EVIDENCE_DIR = ROOT / "workers" / "variant_step_evidence" / "16038"
OUTPUT_JSON = ROOT / "data" / "rule_seed_quantity_formulas.json"
OUTPUT_MD = ROOT / "data" / "rule_seed_quantity_formulas.md"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_json(path: Path) -> dict[str, Any]:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def parse_pattern_value(value: str) -> dict[str, float | int | None]:
    match = re.search(r"(?P<count>\d+)\s*x\s*(?P<pitch>\d+(?:\.\d+)?)\s*mm", value, re.IGNORECASE)
    if not match:
        return {"instanceCount": None, "pitchMm": None}
    return {"instanceCount": int(match.group("count")), "pitchMm": float(match.group("pitch"))}


def numeric(value: Any) -> float | None:
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def compact_number(value: float | None) -> int | float | None:
    if value is None:
        return None
    return int(value) if value.is_integer() else round(value, 4)


def parse_run_timestamp(value: Any, fallback: float) -> float:
    if not value:
        return fallback
    text = str(value).strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        return datetime.fromisoformat(text).timestamp()
    except ValueError:
        return fallback


def latest_completed_run_dirs() -> list[Path]:
    if not EXTRACTIONS_DIR.exists():
        return []
    latest_by_template: dict[str, tuple[float, Path]] = {}
    for path in EXTRACTIONS_DIR.iterdir():
        run_path = path / "rule_extraction_run.json"
        if not path.is_dir() or not run_path.exists():
            continue
        run = read_json(run_path)
        if run.get("status") != "completed":
            continue
        template_key = str(run.get("template_id") or run.get("template_title") or path.name)
        timestamp = parse_run_timestamp(run.get("updated_at") or run.get("created_at"), path.stat().st_mtime)
        current = latest_by_template.get(template_key)
        if current is None or timestamp > current[0]:
            latest_by_template[template_key] = (timestamp, path)
    return [path for _, path in sorted(latest_by_template.values(), key=lambda item: item[1].name.lower())]


def first_file(output_dir: Path, suffix: str) -> Path | None:
    matches = sorted(output_dir.glob(f"*{suffix}"))
    return matches[0] if matches else None


def rows_with(bindings: list[dict[str, Any]], role: str, subrole: str | None = None, token: str | None = None) -> list[dict[str, Any]]:
    rows = [row for row in bindings if row.get("role") == role and (subrole is None or row.get("subrole") == subrole)]
    if token is not None:
        rows = [row for row in rows if token in str(row.get("object_label", ""))]
    return rows


def count_repeated_group(payload: dict[str, Any], role: str, subrole: str, token: str | None = None) -> int:
    best = 0
    for group in payload.get("repeatedGroups", []):
        if not isinstance(group, dict):
            continue
        if group.get("role") != role or group.get("subrole") != subrole:
            continue
        labels = " ".join(str(item) for item in group.get("labelSamples", []))
        if token is not None and token not in labels:
            continue
        best = max(best, int(group.get("count") or 0))
    return best


def quantity_values(rows: list[dict[str, Any]], include: list[str], exclude: list[str] | None = None) -> list[float]:
    exclude = exclude or []
    values: list[float] = []
    for row in rows:
        name = str(row.get("name", ""))
        if not all(token in name for token in include):
            continue
        if any(token in name for token in exclude):
            continue
        value = numeric(row.get("quantity"))
        if value is not None:
            values.append(value)
    return values


def denominators(rows: list[dict[str, Any]]) -> list[int]:
    found: set[int] = set()
    for row in rows:
        name = str(row.get("name", ""))
        for match in re.finditer(r"[╱/](\d+)", name):
            found.add(int(match.group(1)))
    return sorted(found)


def row_refs(rows: list[dict[str, Any]], include: list[str], limit: int = 8) -> list[dict[str, Any]]:
    refs: list[dict[str, Any]] = []
    for row in rows:
        name = str(row.get("name", ""))
        if all(token in name for token in include):
            refs.append(
                {
                    "rowNumber": row.get("rowNumber"),
                    "name": row.get("name"),
                    "quantity": row.get("quantity"),
                    "matchedToken": row.get("matchedToken"),
                }
            )
        if len(refs) >= limit:
            break
    return refs


def row_size(row: dict[str, Any], axis: str) -> float:
    return numeric(row.get(f"bbox_size_{axis}_mm")) or 0.0


def is_near(value: float, target: float, tolerance: float = 1.2) -> bool:
    return abs(value - target) <= tolerance


def role_rows(payload: dict[str, Any], role: str, subrole: str | None = None) -> list[dict[str, Any]]:
    bindings = payload.get("bindings", [])
    if not isinstance(bindings, list):
        return []
    return [
        row
        for row in bindings
        if isinstance(row, dict) and row.get("role") == role and (subrole is None or row.get("subrole") == subrole)
    ]


def count_signature(rows: list[dict[str, Any]], sx: float, sy: float, sz: float, tolerance: float = 1.5) -> int:
    return sum(
        1
        for row in rows
        if is_near(row_size(row, "x"), sx, tolerance)
        and is_near(row_size(row, "y"), sy, tolerance)
        and is_near(row_size(row, "z"), sz, tolerance)
    )


def variant_door_count_from_name(name: str) -> int | None:
    match = re.search(r"(?P<count>\d+)door", name, re.IGNORECASE)
    if match:
        return int(match.group("count"))
    return None


def step_variant_metrics(variant_dir: Path) -> dict[str, Any] | None:
    role_path = first_file(variant_dir, "_step_component_role_bindings.json")
    if role_path is None:
        return None
    payload = read_json(role_path)
    door_assemblies = [row for row in role_rows(payload, "door_module", "door_assembly") if is_near(row_size(row, "x"), 233, 1.5)]
    small_door_assemblies = sum(1 for row in door_assemblies if row_size(row, "y") < 1300)
    tall_door_assemblies = sum(1 for row in door_assemblies if row_size(row, "y") >= 1700)
    door_total = small_door_assemblies + tall_door_assemblies
    lock_rows = role_rows(payload, "lock_system")
    metrics = {
        "variant": variant_dir.name,
        "expectedDoorCount": variant_door_count_from_name(variant_dir.name),
        "roleEvidenceFile": str(role_path),
        "smallDoorAssemblies": small_door_assemblies,
        "tallDoorAssemblies": tall_door_assemblies,
        "doorTotal": door_total,
        "lockHookCount": count_signature(lock_rows, 20.6, 34.0, 29.3),
        "latchPlateCount": count_signature(lock_rows, 18.0, 30.0, 13.2) + count_signature(lock_rows, 18.0, 28.0, 13.2),
        "lockFrameCount": count_signature(lock_rows, 26.0, 116.0, 98.8) + count_signature(lock_rows, 30.0, 116.0, 98.8),
        "horizontalDividers": len(role_rows(payload, "door_frame", "horizontal_divider")),
        "verticalDividers": len(role_rows(payload, "door_frame", "vertical_divider")),
        "shelfPanelObjects": len(role_rows(payload, "shelf_or_partition", "shelf_panel")),
        "repeatedRoleGroups": int(payload.get("repeatedGroupCount") or 0),
    }
    return metrics


def formula_for_shelf(item: dict[str, Any], link: dict[str, Any]) -> dict[str, Any]:
    rows = link.get("rows", []) if isinstance(link.get("rows"), list) else []
    pattern = parse_pattern_value(str(item.get("value", "")))
    panel_values = quantity_values(rows, ["箱体横层板"], ["焊接", "加强筋"])
    unique_panel_values = sorted(set(panel_values))
    per_column = unique_panel_values[0] if len(unique_panel_values) == 1 else None
    inferred_rows = per_column + 1 if per_column is not None else None
    total_panels = sum(panel_values) if panel_values else None
    checks = [
        {
            "name": "shelf_panels_per_column_consistent",
            "ok": len(unique_panel_values) == 1 and per_column is not None,
            "actual": compact_number(per_column),
            "expected": "L/R 横层板数量一致",
        },
        {
            "name": "shelf_rows_inferred_from_panels",
            "ok": inferred_rows is not None,
            "actual": compact_number(inferred_rows),
            "expected": "横层板数量 + 1",
        },
    ]
    return {
        "templateRoot": link.get("templateRoot"),
        "runId": link.get("runId"),
        "bomFile": link.get("path"),
        "bomName": link.get("name"),
        "status": "quantity_formula_partial",
        "derived": {
            "patternInstanceCount": pattern["instanceCount"],
            "pitchMm": pattern["pitchMm"],
            "shelfPanelsPerColumn": compact_number(per_column),
            "inferredDoorRows": compact_number(inferred_rows),
            "totalShelfPanels": compact_number(total_panels),
        },
        "checks": checks,
        "supportRows": row_refs(rows, ["箱体横层板"]),
        "summary": "BOM 支持每列 5 块横层板、推导 6 行门格；SolidWorks 的 11 个阵列实例还需要继续绑定特征角色。",
    }


def formula_for_door(item: dict[str, Any], link: dict[str, Any]) -> dict[str, Any]:
    rows = link.get("rows", []) if isinstance(link.get("rows"), list) else []
    pattern = parse_pattern_value(str(item.get("value", "")))
    divider_values = quantity_values(rows, ["门框", "横隔板"])
    unique_dividers = sorted(set(divider_values))
    dividers_per_column = unique_dividers[0] if len(unique_dividers) == 1 else None
    inferred_rows = dividers_per_column + 1 if dividers_per_column is not None else None
    denom_values = denominators(rows)
    door_total = max(denom_values) if denom_values else None
    columns = None
    if door_total is not None and inferred_rows:
        columns = door_total / inferred_rows
    checks = [
        {
            "name": "horizontal_dividers_infer_rows",
            "ok": inferred_rows is not None,
            "actual": compact_number(inferred_rows),
            "expected": "横隔板数量 + 1",
        },
        {
            "name": "pattern_count_matches_inferred_rows",
            "ok": pattern["instanceCount"] == int(inferred_rows) if inferred_rows is not None else False,
            "actual": pattern["instanceCount"],
            "expected": compact_number(inferred_rows),
        },
        {
            "name": "door_total_divisible_by_rows",
            "ok": columns is not None and float(columns).is_integer(),
            "actual": compact_number(columns),
            "expected": "总门数 / 行数 = 整数列",
        },
    ]
    all_ok = all(bool(check["ok"]) for check in checks)
    return {
        "templateRoot": link.get("templateRoot"),
        "runId": link.get("runId"),
        "bomFile": link.get("path"),
        "bomName": link.get("name"),
        "status": "formula_consistent_candidate" if all_ok else "quantity_formula_partial",
        "derived": {
            "patternInstanceCount": pattern["instanceCount"],
            "pitchMm": pattern["pitchMm"],
            "horizontalDividersPerColumn": compact_number(dividers_per_column),
            "inferredDoorRows": compact_number(inferred_rows),
            "doorTotalFromBomName": door_total,
            "inferredColumns": compact_number(columns),
        },
        "checks": checks,
        "supportRows": row_refs(rows, ["门框", "横隔板"]) + row_refs(rows, ["储物柜门"], limit=4),
        "summary": "门框横隔板数量推导 6 行，BOM 门名分母推导 12 门；可形成 2 列 x 6 行的阵列公式候选。",
    }


def formula_item(item: dict[str, Any]) -> dict[str, Any] | None:
    if item.get("priority") != "P0":
        return None
    item_id = str(item.get("id", ""))
    links = item.get("bomQuantityLinks", [])
    if not isinstance(links, list) or not links:
        return None
    formulas: list[dict[str, Any]] = []
    for link in links:
        if not isinstance(link, dict):
            continue
        if "SHELF_PITCH" in item_id:
            formulas.append(formula_for_shelf(item, link))
        elif "DOOR_COLUMN_PITCH" in item_id:
            formulas.append(formula_for_door(item, link))
    if not formulas:
        return None
    statuses = {str(formula.get("status", "")) for formula in formulas}
    if statuses == {"formula_consistent_candidate"}:
        formula_status = "formula_consistent_candidate"
    elif "formula_consistent_candidate" in statuses:
        formula_status = "mixed_formula_candidate"
    else:
        formula_status = "quantity_formula_partial"
    return {
        "id": item.get("id"),
        "priority": item.get("priority"),
        "label": item.get("label"),
        "value": item.get("value"),
        "evidenceState": item.get("evidenceState"),
        "formulaStatus": formula_status,
        "formulaSummary": " / ".join(sorted({str(formula.get("summary", "")) for formula in formulas if formula.get("summary")})),
        "generationGate": item.get("generationGate"),
        "templateFormulas": formulas,
    }


def step_role_formula_item(run_dir: Path) -> dict[str, Any] | None:
    role_path = first_file(run_dir, "_step_component_role_bindings.json")
    if role_path is None:
        return None
    payload = read_json(role_path)
    bindings = payload.get("bindings", [])
    if not isinstance(bindings, list) or not bindings:
        return None
    run = read_json(run_dir / "rule_extraction_run.json")
    door_assembly_rows = rows_with(bindings, "door_module", "door_assembly")
    door_panel_rows = rows_with(bindings, "door_module", "door_panel")
    door_weldment_rows = rows_with(bindings, "door_module", "door_weldment")
    tall_door_assemblies = sum(1 for row in door_assembly_rows if "12/12" in str(row.get("object_label", "")))
    small_door_assemblies = count_repeated_group(payload, "door_module", "door_assembly", "小")
    small_door_panels = count_repeated_group(payload, "door_module", "door_panel", "小")
    classified_door_assemblies = small_door_assemblies + tall_door_assemblies
    unclassified_door_assemblies = max(0, len(door_assembly_rows) - classified_door_assemblies)
    electric_lock_numbers = sorted(
        {
            match.group(1)
            for row in rows_with(bindings, "lock_system")
            for match in [re.search(r"电控锁ZJA-S(\d+)", str(row.get("object_label", "")))]
            if match
        }
    )
    electric_lock_sets = len(electric_lock_numbers) // 5 if electric_lock_numbers and len(electric_lock_numbers) % 5 == 0 else None
    lock_hooks = len(rows_with(bindings, "lock_system", token="U型锁钩"))
    latch_plates = len(rows_with(bindings, "lock_system", token="插销固定板"))
    horizontal_dividers = len(rows_with(bindings, "door_frame", "horizontal_divider"))
    vertical_dividers = len(rows_with(bindings, "door_frame", "vertical_divider"))
    shelf_panels = len(rows_with(bindings, "shelf_or_partition", "shelf_panel"))
    repeated_groups = int(payload.get("repeatedGroupCount") or 0)
    template_label = str(run.get("template_title") or run.get("template_id") or run_dir.name)
    variant_expected = (
        "bind 4/7/8/12-door variants before generation"
        if "16038" in template_label
        else "bind same-family target door-count variants before generation"
    )

    door_checks = (
        [
            {
                "name": "small_door_panel_matches_assembly",
                "ok": small_door_assemblies > 0 and small_door_panels == small_door_assemblies,
                "actual": small_door_panels,
                "expected": small_door_assemblies,
            }
        ]
        if classified_door_assemblies > 0
        else [
            {
                "name": "door_panel_rows_match_assembly_rows",
                "ok": len(door_assembly_rows) > 0 and len(door_panel_rows) == len(door_assembly_rows),
                "actual": len(door_panel_rows),
                "expected": len(door_assembly_rows),
            }
        ]
    )
    checks = door_checks + [
        {
            "name": "lock_hook_matches_electric_lock_sets",
            "ok": electric_lock_sets is not None and lock_hooks == electric_lock_sets,
            "actual": lock_hooks,
            "expected": electric_lock_sets,
        },
        {
            "name": "latch_plates_two_per_lock_hook",
            "ok": lock_hooks > 0 and latch_plates == lock_hooks * 2,
            "actual": latch_plates,
            "expected": lock_hooks * 2 if lock_hooks else None,
        },
        {
            "name": "frame_dividers_present",
            "ok": horizontal_dividers > 0 and vertical_dividers > 0,
            "actual": f"{horizontal_dividers} horizontal / {vertical_dividers} vertical",
            "expected": "frame divider evidence",
        },
        {
            "name": "variant_formula_requires_more_templates",
            "ok": False,
            "actual": (
                f"doorAssemblies={len(door_assembly_rows)}, "
                f"classified={classified_door_assemblies}, unclassified={unclassified_door_assemblies}"
            ),
            "expected": variant_expected,
        },
    ]
    if classified_door_assemblies > 0:
        door_phrase = f"小门 {small_door_assemblies} 组、高门 {tall_door_assemblies} 组"
        value_prefix = f"小门{small_door_assemblies}+高门{tall_door_assemblies}"
    else:
        door_phrase = f"门装配 {len(door_assembly_rows)} 组、门板 {len(door_panel_rows)} 个"
        value_prefix = f"门装配{len(door_assembly_rows)}"
    if unclassified_door_assemblies > 0:
        door_phrase += f"、其中 {unclassified_door_assemblies} 组未归入小门/高门分类"
    summary = (
        f"STEP 角色证据自动抽到{door_phrase}、"
        f"电控锁 {electric_lock_sets or '待定'} 组、U 型锁钩 {lock_hooks} 个、插销固定板 {latch_plates} 个；"
        f"可作为 {template_label} 的门/锁/分隔件规则候选。"
    )
    return {
        "id": f"STEP-ROLE-{run_dir.name}-DOOR-LOCK",
        "priority": "P0",
        "label": f"{template_label} 门/锁角色数量",
        "value": f"{value_prefix}; 锁{electric_lock_sets or '?'}; 锁钩{lock_hooks}",
        "evidenceState": "step_role_evidence_linked",
        "formulaStatus": "step_role_formula_candidate",
        "formulaSummary": summary,
        "generationGate": "blocked_pending_variant_formula_closure",
        "templateFormulas": [
            {
                "templateRoot": str(Path(str(run.get("assembly_path", ""))).parent) if run.get("assembly_path") else "",
                "runId": run.get("id", run_dir.name),
                "bomFile": str(role_path),
                "bomName": role_path.name,
                "status": "step_role_formula_candidate",
                "derived": {
                    "smallDoorAssemblies": small_door_assemblies,
                    "tallDoorAssemblies": tall_door_assemblies,
                    "doorAssemblyRows": len(door_assembly_rows),
                    "doorPanelRows": len(door_panel_rows),
                    "doorWeldmentRows": len(door_weldment_rows),
                    "classifiedDoorAssemblies": classified_door_assemblies,
                    "unclassifiedDoorAssemblies": unclassified_door_assemblies,
                    "electricLockSets": electric_lock_sets,
                    "lockHookCount": lock_hooks,
                    "latchPlateCount": latch_plates,
                    "horizontalDividers": horizontal_dividers,
                    "verticalDividers": vertical_dividers,
                    "shelfPanelObjects": shelf_panels,
                    "repeatedRoleGroups": repeated_groups,
                },
                "checks": checks,
                "supportRows": [],
                "summary": summary,
            }
        ],
    }


def latest_12door_module_evidence() -> dict[str, Any]:
    latest: tuple[float, dict[str, Any]] | None = None
    for run_dir in latest_completed_run_dirs():
        role_path = first_file(run_dir, "_step_component_role_bindings.json")
        if role_path is None:
            continue
        payload = read_json(role_path)
        bindings = payload.get("bindings", [])
        if not isinstance(bindings, list):
            continue
        rows = [row for row in bindings if isinstance(row, dict) and "12/12" in str(row.get("object_label", ""))]
        if not rows:
            continue
        timestamp = run_dir.stat().st_mtime
        evidence = {
            "runId": run_dir.name,
            "roleEvidenceFile": str(role_path),
            "module12Rows": len(rows),
            "module12DoorAssemblies": sum(1 for row in rows if row.get("role") == "door_module" and row.get("subrole") == "door_assembly"),
            "module12DoorPanels": sum(1 for row in rows if row.get("role") == "door_module" and row.get("subrole") == "door_panel"),
            "module12Reinforcements": sum(1 for row in rows if row.get("role") == "door_module" and row.get("subrole") == "door_reinforcement"),
        }
        if latest is None or timestamp > latest[0]:
            latest = (timestamp, evidence)
    return latest[1] if latest else {}


def variant_formula_item() -> dict[str, Any] | None:
    if not VARIANT_STEP_EVIDENCE_DIR.exists():
        return None
    variant_rows = [
        metrics
        for variant_dir in sorted(VARIANT_STEP_EVIDENCE_DIR.iterdir())
        if variant_dir.is_dir()
        for metrics in [step_variant_metrics(variant_dir)]
        if metrics is not None and metrics.get("expectedDoorCount") is not None
    ]
    variant_rows = sorted(variant_rows, key=lambda item: int(item["expectedDoorCount"]))
    if not variant_rows:
        return None
    module12 = latest_12door_module_evidence()
    checks: list[dict[str, Any]] = []
    for metrics in variant_rows:
        expected = int(metrics["expectedDoorCount"])
        door_total = int(metrics["doorTotal"])
        checks.extend(
            [
                {
                    "name": "variant_door_total_matches_step",
                    "ok": door_total == expected,
                    "actual": door_total,
                    "expected": expected,
                },
                {
                    "name": "variant_lock_hook_matches_door_total",
                    "ok": int(metrics["lockHookCount"]) == door_total,
                    "actual": metrics["lockHookCount"],
                    "expected": door_total,
                },
                {
                    "name": "variant_latch_plates_two_per_door",
                    "ok": int(metrics["latchPlateCount"]) == door_total * 2,
                    "actual": metrics["latchPlateCount"],
                    "expected": door_total * 2,
                },
            ]
        )
    checks.append(
        {
            "name": "module_12door_evidence_available",
            "ok": bool(module12.get("module12DoorAssemblies") and module12.get("module12DoorPanels")),
            "actual": f"assembly={module12.get('module12DoorAssemblies', 0)}, panel={module12.get('module12DoorPanels', 0)}",
            "expected": "12/12 door assembly and panel evidence",
        }
    )
    full_variants = " / ".join(str(row["expectedDoorCount"]) for row in variant_rows)
    all_ok = all(bool(check["ok"]) for check in checks)
    summary = (
        f"16038 STEP 已闭环 {full_variants} 门总装：doorTotal=小门装配+高门装配，"
        "锁钩数=门数，插销固定板=2×门数；12/12 单门模块已有门板/装配证据。"
    )
    return {
        "id": "STEP-VARIANT-16038-4-7-8-12",
        "priority": "P0",
        "label": "16038 4/7/8/12 门变体公式",
        "value": f"{full_variants}门总装 + 12/12单门模块",
        "evidenceState": "variant_step_evidence_linked",
        "formulaStatus": "variant_formula_candidate" if all_ok else "variant_formula_partial",
        "formulaSummary": summary,
        "generationGate": "blocked_pending_generator_binding",
        "templateFormulas": [
            {
                "templateRoot": str(VARIANT_STEP_EVIDENCE_DIR),
                "runId": "16038_variant_step_evidence",
                "bomFile": str(VARIANT_STEP_EVIDENCE_DIR),
                "bomName": "16038 STEP variant role evidence",
                "status": "variant_formula_candidate" if all_ok else "variant_formula_partial",
                "derived": {
                    "closedFullAssemblyVariants": full_variants,
                    "doorTotalFormula": "smallDoorAssemblies + tallDoorAssemblies",
                    "lockHookFormula": "lockHookCount = doorTotal",
                    "latchPlateFormula": "latchPlateCount = 2 x doorTotal",
                    "module12DoorAssemblies": module12.get("module12DoorAssemblies", 0),
                    "module12DoorPanels": module12.get("module12DoorPanels", 0),
                },
                "variantRows": variant_rows,
                "module12Evidence": module12,
                "checks": checks,
                "supportRows": [],
                "summary": summary,
            }
        ],
    }


def build_formulas() -> dict[str, Any]:
    checklist = read_json(CHECKLIST_JSON)
    items = checklist.get("items", [])
    if not isinstance(items, list):
        items = []
    formula_items = [formula for item in items if isinstance(item, dict) for formula in [formula_item(item)] if formula]
    formula_items.extend(formula for run_dir in latest_completed_run_dirs() for formula in [step_role_formula_item(run_dir)] if formula)
    variant_formula = variant_formula_item()
    if variant_formula:
        formula_items.append(variant_formula)
    return {
        "generatedAt": utc_now(),
        "source": str(CHECKLIST_JSON),
        "itemCount": len(formula_items),
        "items": formula_items,
    }


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 规则种子数量公式候选",
        "",
        f"- Generated at: `{payload['generatedAt']}`",
        f"- Source: `{payload['source']}`",
        f"- Items: `{payload['itemCount']}`",
        "",
        "这里不放行生产模型，只记录 BOM 行级数量能推导出的门数/层板数量公式候选。",
        "",
    ]
    for item in payload["items"]:
        lines.extend(
            [
                f"## {item['id']} {item['label']} `{item['value']}`",
                "",
                f"- Formula status: `{item['formulaStatus']}`",
                f"- Summary: {item['formulaSummary']}",
                "",
            ]
        )
        for formula in item.get("templateFormulas", []):
            lines.append(f"- BOM: `{formula.get('bomName', '')}`")
            lines.append(f"  - Status: `{formula.get('status', '')}`")
            lines.append(f"  - Derived: `{json.dumps(formula.get('derived', {}), ensure_ascii=False)}`")
            for check in formula.get("checks", []):
                mark = "PASS" if check.get("ok") else "OPEN"
                lines.append(f"  - {mark}: {check.get('name')} actual={check.get('actual')} expected={check.get('expected')}")
            for row in formula.get("supportRows", [])[:8]:
                lines.append(f"  - row {row.get('rowNumber')}: {row.get('name')} qty={row.get('quantity')}")
        lines.append("")
    OUTPUT_MD.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    payload = build_formulas()
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(payload)
    print(f"Wrote {OUTPUT_JSON}")
    print(f"Wrote {OUTPUT_MD}")
    print(f"items={payload['itemCount']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
