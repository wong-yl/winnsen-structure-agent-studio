from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def truthy(value: str) -> bool:
    return value.strip().lower() in {"true", "1", "yes"}


def has_bbox(row: dict[str, str]) -> bool:
    return any(
        row.get(key, "").strip()
        for key in ("bbox_size_x_mm", "bbox_size_y_mm", "bbox_size_z_mm")
    )


def role_for_name(name: str) -> str:
    checks = [
        ("door_or_door_assembly", ("门", "door")),
        ("shelf_or_partition", ("层板", "隔板", "partition", "shelf")),
        ("side_or_panel", ("侧板", "面板", "panel")),
        ("base_or_leveling", ("底座", "调整脚", "leveling")),
        ("top_cover", ("上盖", "top")),
        ("lock_or_latch", ("锁", "插销", "lock", "latch")),
        ("hanger_or_rail", ("衣架", "衣杆", "rail")),
        ("electronics_or_power", ("电源", "插座", "板", "power")),
        ("reinforcement", ("加强筋", "stiffener", "rib")),
    ]
    lowered = name.lower()
    for role, tokens in checks:
        if any(token in name or token in lowered for token in tokens):
            return role
    return "unclassified"


def parse_float(value: str) -> float | None:
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def extract_pattern_rules(dimensions: list[dict[str, str]]) -> list[dict[str, Any]]:
    grouped: dict[str, dict[str, Any]] = {}
    for row in dimensions:
        if row.get("feature_type") != "LocalLPattern":
            continue
        feature_name = row.get("feature_name", "").strip()
        if not feature_name:
            continue
        item = grouped.setdefault(
            feature_name,
            {
                "featureName": feature_name,
                "featureType": row.get("feature_type", ""),
                "instanceCount": None,
                "spacingMm": None,
                "rawDimensions": [],
            },
        )
        dimension_name = row.get("dimension_name", "").strip()
        raw_value = parse_float(row.get("system_value_m", ""))
        value_mm = parse_float(row.get("value_mm", ""))
        item["rawDimensions"].append(
            {
                "dimensionName": dimension_name,
                "dimensionFullName": row.get("dimension_full_name", ""),
                "valueMm": value_mm,
                "systemValue": raw_value,
            }
        )
        if dimension_name == "D1" and raw_value is not None and raw_value.is_integer() and raw_value <= 100:
            item["instanceCount"] = int(raw_value)
        elif dimension_name in {"D2", "D3"} and value_mm is not None and value_mm > 0:
            item["spacingMm"] = value_mm
    return list(grouped.values())


def first_file(output_dir: Path, suffix: str) -> Path:
    matches = sorted(output_dir.glob(f"*{suffix}"))
    if not matches:
        raise SystemExit(f"Missing extraction file with suffix {suffix} in {output_dir}")
    return matches[0]


def optional_first_file(output_dir: Path, suffix: str) -> Path | None:
    matches = sorted(output_dir.glob(f"*{suffix}"))
    return matches[0] if matches else None


def build_summary(output_dir: Path) -> dict[str, Any]:
    snapshot_path = first_file(output_dir, "_sw_api_snapshot.json")
    component_path = first_file(output_dir, "_sw_api_snapshot_components.csv")
    mate_path = first_file(output_dir, "_sw_api_snapshot_mates.csv")
    feature_path = first_file(output_dir, "_sw_api_snapshot_features.csv")
    dimension_path = first_file(output_dir, "_sw_api_snapshot_dimensions.csv")

    snapshot = read_json(snapshot_path)
    components = read_csv(component_path)
    mates = read_csv(mate_path)
    features = read_csv(feature_path)
    dimensions = read_csv(dimension_path)

    transform_rows = sum(1 for row in components if truthy(row.get("has_transform", "")))
    bbox_rows = sum(1 for row in components if has_bbox(row))
    component_count = len(components)
    mate_feature_count = int(snapshot.get("counts", {}).get("mate_count") or 0)
    role_counts = Counter(role_for_name(f"{row.get('name', '')} {row.get('file_name', '')}") for row in components)
    mate_type_counts = Counter(row.get("mate_type_name", "") or "unknown" for row in mates)
    pattern_rules = extract_pattern_rules(dimensions)
    step_bbox_path = optional_first_file(output_dir, "_step_object_bboxes.json")
    step_bbox_summary = read_json(step_bbox_path) if step_bbox_path else None
    step_object_bbox_count = int(step_bbox_summary.get("objectCount") or 0) if step_bbox_summary else 0
    step_role_path = optional_first_file(output_dir, "_step_component_role_bindings.json")
    step_role_summary = read_json(step_role_path) if step_role_path else None
    step_role_binding_count = int(step_role_summary.get("objectCount") or 0) if step_role_summary else 0
    step_role_matched_component_count = int(step_role_summary.get("matchedComponentCount") or 0) if step_role_summary else 0
    step_role_repeated_group_count = int(step_role_summary.get("repeatedGroupCount") or 0) if step_role_summary else 0

    if component_count == 0:
        quality_gate = "blocked_no_component_evidence"
        next_action = "Open the assembly manually and verify file references before rule learning."
    elif transform_rows == 0 or bbox_rows == 0:
        if step_role_binding_count > 0:
            quality_gate = "step_role_binding_available_needs_formula_derivation"
            next_action = (
                "STEP 角色绑定证据已可用；下一步抽取门板、层板、锁具、铰链和分隔件公式，"
                "再用 BOM/DXF 做数量闭环。"
            )
        elif step_object_bbox_count > 0:
            quality_gate = "step_bbox_available_needs_component_mapping"
            next_action = (
                "SolidWorks 导出的 STEP 对象 bbox 已可用；下一步把 STEP 标签绑定回 SolidWorks 组件角色，再推导摆放和门数规则。"
            )
        elif pattern_rules:
            quality_gate = "component_tree_available_needs_position_evidence"
            next_action = (
                "SolidWorks 组件树、配合和阵列尺寸已可读；下一步补 STEP/bbox 或 transform 位置证据，"
                "再推导摆放和门数规则。"
            )
        else:
            quality_gate = "component_tree_available_needs_position_evidence"
            next_action = (
                "SolidWorks 组件树和配合名称已可读；当前只能作为模块命名证据，"
                "需要补 STEP/bbox 或 transform 位置证据后才能推导摆放规则。"
            )
    elif mate_feature_count == 0:
        quality_gate = "needs_mate_or_relation_binding"
        next_action = "Component placement evidence exists, but mate semantics still need binding before generation."
    else:
        quality_gate = "ready_for_rule_binding"
        next_action = "Bind component roles to door count, shelf pitch, frame divider, lock, and hinge rules."

    return {
        "generatedAt": utc_now(),
        "outputDir": str(output_dir),
        "snapshot": {
            "path": str(snapshot_path),
            "documentTitle": snapshot.get("document_title", ""),
            "solidworksRevision": snapshot.get("solidworks_revision", ""),
            "openErrorsCode": snapshot.get("open_errors_code"),
            "openWarningsCode": snapshot.get("open_warnings_code"),
            "bboxMm": snapshot.get("bbox_mm"),
        },
        "counts": {
            "components": component_count,
            "componentsWithTransform": transform_rows,
            "componentsWithBBox": bbox_rows,
            "mateFeatureCount": mate_feature_count,
            "mateEntityRows": len(mates),
            "features": len(features),
            "dimensions": len(dimensions),
            "patternRuleCount": len(pattern_rules),
            "sheetMetalFeatureCount": snapshot.get("counts", {}).get("sheet_metal_feature_count", 0),
            "maxComponentDepth": snapshot.get("counts", {}).get("max_component_depth", 0),
            "stepObjectBBoxCount": step_object_bbox_count,
            "stepSourceObjectCount": int(step_bbox_summary.get("sourceObjectCount") or 0) if step_bbox_summary else 0,
            "stepSkippedObjectCount": int(step_bbox_summary.get("skippedObjectCount") or 0) if step_bbox_summary else 0,
            "stepRoleBindingCount": step_role_binding_count,
            "stepRoleMatchedComponentCount": step_role_matched_component_count,
            "stepRoleRepeatedGroupCount": step_role_repeated_group_count,
        },
        "stepAssemblyBBoxEvidence": {
            "path": str(step_bbox_path) if step_bbox_path else None,
            "objectCount": step_object_bbox_count,
            "assemblyBBoxMm": step_bbox_summary.get("assemblyBBoxMm") if step_bbox_summary else None,
        },
        "stepComponentRoleEvidence": {
            "path": str(step_role_path) if step_role_path else None,
            "objectCount": step_role_binding_count,
            "matchedComponentCount": step_role_matched_component_count,
            "repeatedGroupCount": step_role_repeated_group_count,
            "roleCounts": step_role_summary.get("roleCounts", {}) if step_role_summary else {},
            "repeatedGroups": (step_role_summary.get("repeatedGroups", []) if step_role_summary else [])[:20],
        },
        "roleCounts": dict(role_counts),
        "mateTypeEntityRows": dict(mate_type_counts),
        "patternRules": pattern_rules,
        "qualityGate": quality_gate,
        "nextAction": next_action,
        "evidenceFiles": {
            "componentsCsv": str(component_path),
            "matesCsv": str(mate_path),
            "featuresCsv": str(feature_path),
            "dimensionsCsv": str(dimension_path),
            "stepObjectBBoxesJson": str(step_bbox_path) if step_bbox_path else None,
            "stepComponentRoleBindingsJson": str(step_role_path) if step_role_path else None,
        },
    }


def write_markdown(summary: dict[str, Any], path: Path) -> None:
    counts = summary["counts"]
    lines = [
        "# SolidWorks 规则提取质量摘要",
        "",
        f"- Generated at: `{summary['generatedAt']}`",
        f"- Document: `{summary['snapshot']['documentTitle']}`",
        f"- SolidWorks revision: `{summary['snapshot']['solidworksRevision']}`",
        f"- Quality gate: `{summary['qualityGate']}`",
        f"- Next action: {summary['nextAction']}",
        "",
        "## Counts",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Components | {counts['components']} |",
        f"| Components with transform | {counts['componentsWithTransform']} |",
        f"| Components with bbox | {counts['componentsWithBBox']} |",
        f"| Mate features | {counts['mateFeatureCount']} |",
        f"| Mate entity rows | {counts['mateEntityRows']} |",
        f"| Features | {counts['features']} |",
        f"| Dimensions | {counts['dimensions']} |",
        f"| Pattern rule seeds | {counts['patternRuleCount']} |",
        f"| Sheet-metal features | {counts['sheetMetalFeatureCount']} |",
        f"| STEP object bboxes | {counts.get('stepObjectBBoxCount', 0)} |",
        f"| STEP role bindings | {counts.get('stepRoleBindingCount', 0)} |",
        f"| STEP matched components | {counts.get('stepRoleMatchedComponentCount', 0)} |",
        f"| STEP repeated role groups | {counts.get('stepRoleRepeatedGroupCount', 0)} |",
        "",
        "## Role Counts",
        "",
        "| Role | Count |",
        "|---|---:|",
    ]
    for role, count in sorted(summary["roleCounts"].items()):
        lines.append(f"| {role} | {count} |")

    lines.extend(["", "## Mate Entity Rows", "", "| Mate type | Rows |", "|---|---:|"])
    for mate_type, count in sorted(summary["mateTypeEntityRows"].items()):
        lines.append(f"| {mate_type} | {count} |")

    if summary["patternRules"]:
        lines.extend(["", "## Pattern Rule Seeds", "", "| Feature | Instances | Spacing mm |", "|---|---:|---:|"])
        for rule in summary["patternRules"]:
            lines.append(
                f"| {rule['featureName']} | {rule.get('instanceCount') or ''} | {rule.get('spacingMm') or ''} |"
            )

    step_evidence = summary.get("stepAssemblyBBoxEvidence") or {}
    if step_evidence.get("assemblyBBoxMm"):
        box = step_evidence["assemblyBBoxMm"]
        lines.extend(
            [
                "",
                "## STEP BBox Evidence",
                "",
                f"- Objects with valid bbox: `{step_evidence.get('objectCount', 0)}`",
                f"- Evidence file: `{step_evidence.get('path')}`",
                f"- Assembly bbox: `{box.get('sizeX')} x {box.get('sizeY')} x {box.get('sizeZ')} mm`",
            ]
        )

    role_evidence = summary.get("stepComponentRoleEvidence") or {}
    if role_evidence.get("objectCount"):
        lines.extend(
            [
                "",
                "## STEP Role Binding Evidence",
                "",
                f"- Objects bound to roles: `{role_evidence.get('objectCount', 0)}`",
                f"- Matched SolidWorks component rows: `{role_evidence.get('matchedComponentCount', 0)}`",
                f"- Repeated role bbox groups: `{role_evidence.get('repeatedGroupCount', 0)}`",
                f"- Evidence file: `{role_evidence.get('path')}`",
                "",
                "| Role | Count |",
                "|---|---:|",
            ]
        )
        for role, count in sorted((role_evidence.get("roleCounts") or {}).items()):
            lines.append(f"| {role} | {count} |")

        repeated_groups = role_evidence.get("repeatedGroups") or []
        if repeated_groups:
            lines.extend(["", "| Repeated role | BBox mm | Count |", "|---|---:|---:|"])
            for group in repeated_groups[:10]:
                lines.append(f"| {group.get('role')}::{group.get('subrole')} | {group.get('bboxSignatureMm')} | {group.get('count')} |")

    lines.extend(
        [
            "",
            "## Interpretation",
            "",
            "- `componentsWithTransform=0` or `componentsWithBBox=0` means this extraction cannot yet derive reliable placement, pitch, or spacing rules.",
            "- `stepRoleBindingCount>0` means STEP geometry has been automatically grouped into door, shelf, lock, hinge, frame, and cabinet evidence roles for formula derivation.",
            "- `stepObjectBBoxCount>0` means SolidWorks-exported STEP geometry has usable object-level bboxes, but those labels still need to be bound back to component roles before automatic generation.",
            "- Local pattern dimensions can be used as rule seeds, but they need DXF/BOM evidence closure before a generator uses them.",
            "- Mate and component names can still be used as module naming evidence, but generation must remain blocked until transform/bbox evidence is repaired or a checked pattern/DXF rule replaces it.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Summarize a SolidWorks rule extraction output folder.")
    parser.add_argument("output_dir")
    args = parser.parse_args()
    output_dir = Path(args.output_dir)
    summary = build_summary(output_dir)
    json_path = output_dir / "rule_learning_summary.json"
    markdown_path = output_dir / "rule_learning_summary.md"
    json_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(summary, markdown_path)
    print(f"Wrote {json_path}")
    print(f"Wrote {markdown_path}")
    print(f"quality_gate={summary['qualityGate']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
