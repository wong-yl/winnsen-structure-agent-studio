from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = ROOT / "data"
BASELINE_DIR = ROOT / "workers" / "manual_runs" / "BASELINE-16029-12DOOR-CLONE"
RULE_RUN_DIR = ROOT / "workers" / "rule_extractions" / "RULE-20260517023937-37D2A4"
SNAPSHOT_DIR = ROOT / "workers" / "analysis" / "solidworks_16029_top_assembly_snapshot"
SOURCE_ASSEMBLY = Path(r"C:\sw16029_direct_18door\source\top_assembly.SLDASM")
TEN_DOOR_SOURCE_ASSEMBLY = Path(
    r"D:\机械结构工程师智能体\work\16029_练习副本_20260429\1.工程图\标准寄存柜1917×1000×550(总装配).SLDASM"
)
TEN_DOOR_RECIPE_MD = DATA_DIR / "solidworks_16029_10door_mutator_recipe.md"

SNAPSHOT_PREFIX = "solidworks_16029_assembly_api_snapshot_top_assembly_20260518"
RULE_PREFIX = "RULE-20260517023937-37D2A4"

HANDOFF_JSON = DATA_DIR / "solidworks_16029_engineering_handoff.json"
HANDOFF_MD = DATA_DIR / "solidworks_16029_engineering_handoff.md"
ROLE_RULES_JSON = DATA_DIR / "solidworks_16029_role_rules.json"
ROLE_RULES_MD = DATA_DIR / "solidworks_16029_role_rules.md"


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def file_info(path: Path | None) -> dict[str, Any]:
    exists = bool(path and path.exists())
    stat = path.stat() if path and exists else None
    return {
        "path": str(path) if path else "",
        "exists": exists,
        "sizeBytes": stat.st_size if stat else None,
        "updatedAt": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat() if stat else None,
    }


def latest_matching_file(pattern: str) -> Path | None:
    matches = [
        path
        for path in (ROOT / "workers" / "manual_runs").glob(pattern)
        if path.is_file() and not path.name.startswith("~$")
    ]
    if not matches:
        return None
    return max(matches, key=lambda path: path.stat().st_mtime)


def classify_feature(name: str) -> str:
    if any(token in name for token in ("电源", "电路板", "锁控板")):
        return "electronics_or_power"
    if any(token in name for token in ("电控锁", "锁", "插销", "U型锁钩")):
        return "lock_system"
    if any(token in name for token in ("门轴", "轴套", "开口挡圈", "铰链")):
        return "hinge_or_pivot"
    if any(token in name for token in ("储物柜门", "柜门", "门板")):
        return "door_module"
    if "门框" in name:
        return "door_frame"
    if any(token in name for token in ("横层板", "竖隔板", "隔板")):
        return "shelf_or_partition"
    if any(token in name for token in ("箱体左侧板", "箱体右侧板", "侧板加强筋", "标准寄存柜 模型")):
        return "cabinet_body"
    if any(token in name for token in ("底座", "调整脚", "螺母M12")):
        return "base_or_leveling"
    if any(token in name for token in ("上盖", "顶棚")):
        return "top_cover"
    if any(token in name for token in ("衣架钢管", "衣杆")):
        return "hanger_rail"
    if any(token in name for token in ("维护门", "后下门", "后门")):
        return "service_rear_door"
    return "unclassified"


def feature_role_summary(features: list[dict[str, str]]) -> dict[str, Any]:
    role_counts: Counter[str] = Counter()
    type_counts: dict[str, Counter[str]] = defaultdict(Counter)
    samples: dict[str, list[str]] = defaultdict(list)
    for row in features:
        feature_type = row.get("solidworks_feature_type", "")
        if feature_type not in {"Reference", "ReferencePattern"}:
            continue
        name = row.get("feature_name", "")
        role = classify_feature(name)
        role_counts[role] += 1
        type_counts[role][feature_type] += 1
        if len(samples[role]) < 8:
            samples[role].append(name)
    return {
        "roleCounts": dict(sorted(role_counts.items())),
        "roleTypeCounts": {role: dict(counts) for role, counts in sorted(type_counts.items())},
        "samples": dict(sorted(samples.items())),
    }


def pattern_seeds(dimensions: list[dict[str, str]]) -> list[dict[str, Any]]:
    grouped: dict[str, dict[str, Any]] = defaultdict(dict)
    for row in dimensions:
        feature_name = row.get("feature_name", "")
        dim_name = row.get("dimension_name", "")
        try:
            value_mm = float(row.get("value_mm", ""))
        except ValueError:
            continue
        item = grouped[feature_name]
        item["feature"] = feature_name
        item["featureType"] = row.get("feature_type", "")
        if dim_name == "D1":
            item["instances"] = int(round(value_mm / 1000)) if value_mm > 999 else int(round(value_mm))
        elif dim_name == "D3":
            item["spacingMm"] = value_mm
    return [value for value in grouped.values() if value.get("feature")]


def distance_mate_seeds(mates: list[dict[str, str]]) -> list[dict[str, Any]]:
    grouped: dict[str, dict[str, Any]] = {}
    for row in mates:
        if row.get("mate_type_name") != "distance" or row.get("entity_index") != "0":
            continue
        key = row.get("dimension_full_name") or row.get("mate_name") or str(len(grouped) + 1)
        try:
            value_mm = float(row.get("dimension_value_mm", ""))
        except ValueError:
            continue
        grouped[key] = {
            "mate": row.get("mate_name", ""),
            "dimension": row.get("dimension_display_name", ""),
            "valueMm": value_mm,
        }
    return sorted(grouped.values(), key=lambda item: (item["valueMm"], item["mate"]))[:20]


def top_repeated_groups(summary: dict[str, Any]) -> list[dict[str, Any]]:
    groups = summary.get("stepComponentRoleEvidence", {}).get("repeatedGroups", [])
    selected_roles = {
        "shelf_or_partition",
        "door_frame",
        "door_module",
        "lock_system",
        "hinge_or_pivot",
        "cabinet_body",
        "base_or_leveling",
        "hanger_rail",
    }
    filtered = [item for item in groups if item.get("role") in selected_roles]
    return sorted(filtered, key=lambda item: int(item.get("count") or 0), reverse=True)[:18]


def build_handoff(now: str, rule_summary: dict[str, Any]) -> dict[str, Any]:
    assembly_path = BASELINE_DIR / "BASELINE-16029-12DOOR-CLONE.SLDASM"
    validation_path = BASELINE_DIR / "BASELINE-16029-12DOOR-CLONE_solidworks_validation_report.md"
    build_report_path = BASELINE_DIR / "BASELINE-16029-12DOOR-CLONE_build_report.md"
    ten_door_smoke_output = latest_matching_file("QA-16029-10DOOR-FAST-CLONE-*/*.SLDASM")
    ten_door_smoke_report = latest_matching_file("QA-16029-10DOOR-FAST-CLONE-*/*_solidworks_validation_report.md")
    return {
        "generatedAt": now,
        "status": "usable_10_12_template_clones_10door_native_save_smoke_passed",
        "modelFamily": "16029 standard locker",
        "outerSizeMm": {"width": 1000, "height": 1917, "depth": 550},
        "solidworksEntry": {
            "uiPath": "可生成模型 -> 16029 标准寄存柜 -> 10/12门 -> 一键运行 SOLIDWORKS 2025",
            "script": r"D:\winnsen_cad_workspace\scripts\sw_clone_16029_baseline_template.js",
            "supportedDoorCounts": [10, 12],
            "blockedDoorCounts": [4, 6, 8, 14, 16, 18, 20, 22, 24],
        },
        "baselineOutput": file_info(assembly_path),
        "sourceAssembly": file_info(SOURCE_ASSEMBLY),
        "tenDoorTemplateSource": file_info(TEN_DOOR_SOURCE_ASSEMBLY),
        "tenDoorRecipe": file_info(TEN_DOOR_RECIPE_MD),
        "tenDoorNativeSaveSmokeOutput": file_info(ten_door_smoke_output),
        "reports": {
            "buildReport": file_info(build_report_path),
            "validationReport": file_info(validation_path),
            "tenDoorSmokeReport": file_info(ten_door_smoke_report),
            "ruleLearningSummary": file_info(RULE_RUN_DIR / "rule_learning_summary.md"),
        },
        "evidenceCounts": rule_summary.get("counts", {}),
        "handoffNotes": [
            "This is a SolidWorks-native 10/12-door template clone route, not a from-zero arbitrary mutator.",
            "Keep the source folder available until Pack-and-Go packaging is implemented.",
            "Other door counts are blocked because direct part insertion lost assembly datum/transform evidence.",
        ],
        "nextGates": [
            "Run a short human visual acceptance pass on the saved 10-door clone if the current SolidWorks view is available.",
            "Add Pack-and-Go handoff packaging for the 10/12-door template assemblies.",
            "Convert the 2/10 practice recipe into a transform/mate-backed mutator before reopening broader door-count options.",
        ],
    }


def build_role_rules(now: str) -> dict[str, Any]:
    summary = read_json(RULE_RUN_DIR / "rule_learning_summary.json")
    dimensions = read_csv(RULE_RUN_DIR / f"{RULE_PREFIX}_sw_api_snapshot_dimensions.csv")
    features = read_csv(RULE_RUN_DIR / f"{RULE_PREFIX}_sw_api_snapshot_features.csv")
    mates = read_csv(RULE_RUN_DIR / f"{RULE_PREFIX}_sw_api_snapshot_mates.csv")
    local_features = read_csv(SNAPSHOT_DIR / f"{SNAPSHOT_PREFIX}_features.csv")
    return {
        "generatedAt": now,
        "modelFamily": "16029 standard locker",
        "outerSizeMm": {"width": 1000, "height": 1917, "depth": 550},
        "sourceEvidence": {
            "ruleRunDir": str(RULE_RUN_DIR),
            "baselineDir": str(BASELINE_DIR),
            "sourceAssembly": str(SOURCE_ASSEMBLY),
        },
        "qualityGate": summary.get("qualityGate"),
        "counts": summary.get("counts", {}),
        "stepRoleCounts": summary.get("stepComponentRoleEvidence", {}).get("roleCounts", {}),
        "solidworksFeatureRoles": feature_role_summary(local_features or features),
        "patternSeeds": pattern_seeds(dimensions),
        "distanceMateSeeds": distance_mate_seeds(mates),
        "repeatedRoleGroups": top_repeated_groups(summary),
        "variantReadiness": [
            {
                "target": "12-door",
                "status": "usable_baseline",
                "reason": "Standard top assembly clone exists and opens as SolidWorks-native baseline.",
            },
            {
                "target": "10-door first mutator",
                "status": "template_clone_available_mutator_pending",
                "reason": "Practice source and 2/10 recipe exist: 5 rows per column, 366mm door pitch, 359mm door panel height, 10 locks, 5 shelves/dividers per side.",
            },
            {
                "target": "14/16/18+ doors",
                "status": "blocked_until_10door_passes",
                "reason": "Adding rows/components is higher risk than suppressing/remapping from 12-door; wait for one successful transform/mate mutator.",
            },
        ],
        "blockedReasons": [
            "SolidWorks component transforms are not exposed by the current API extraction.",
            "STEP role bboxes are usable evidence, but must be bound into editable SolidWorks placement operations.",
            "Door frame divider, lock, hinge, shelf, and BOM/DXF formulas must close before enabling arbitrary door size changes.",
        ],
    }


def write_handoff_md(payload: dict[str, Any]) -> None:
    counts = payload.get("evidenceCounts", {})
    lines = [
        "# 16029 SolidWorks 工程交付索引",
        "",
        f"- Generated at: `{payload['generatedAt']}`",
        f"- Status: `{payload['status']}`",
        "- Current usable models: `10-door practice template clone`, `12-door standard baseline clone`",
        f"- UI path: `{payload['solidworksEntry']['uiPath']}`",
        f"- Baseline SLDASM: `{payload['baselineOutput']['path']}`",
        f"- 10-door template source: `{payload['tenDoorTemplateSource']['path']}`",
        f"- 10-door native save smoke: `{payload['tenDoorNativeSaveSmokeOutput']['path']}`",
        f"- Source SLDASM: `{payload['sourceAssembly']['path']}`",
        "",
        "## 当前能交付",
        "",
        "| Item | Status | Detail |",
        "| --- | --- | --- |",
        f"| SolidWorks 12门基线 | {'ready' if payload['baselineOutput']['exists'] else 'missing'} | {payload['baselineOutput']['path']} |",
        f"| SolidWorks 10门练习副本 | {'ready' if payload['tenDoorTemplateSource']['exists'] else 'missing'} | {payload['tenDoorTemplateSource']['path']} |",
        f"| SolidWorks 10门快速保存烟测 | {'ready' if payload['tenDoorNativeSaveSmokeOutput']['exists'] else 'missing'} | {payload['tenDoorNativeSaveSmokeOutput']['path']} |",
        f"| 10门配方 | {'ready' if payload['tenDoorRecipe']['exists'] else 'missing'} | {payload['tenDoorRecipe']['path']} |",
        f"| 源总装 | {'ready' if payload['sourceAssembly']['exists'] else 'missing'} | {payload['sourceAssembly']['path']} |",
        f"| 验证报告 | {'ready' if payload['reports']['validationReport']['exists'] else 'missing'} | {payload['reports']['validationReport']['path']} |",
        f"| 10门烟测报告 | {'ready' if payload['reports']['tenDoorSmokeReport']['exists'] else 'missing'} | {payload['reports']['tenDoorSmokeReport']['path']} |",
        "",
        "## 证据计数",
        "",
        "| Metric | Value |",
        "| --- | ---: |",
    ]
    for key in [
        "features",
        "mateFeatureCount",
        "dimensions",
        "patternRuleCount",
        "stepObjectBBoxCount",
        "stepRoleBindingCount",
        "stepRoleMatchedComponentCount",
    ]:
        lines.append(f"| {key} | {counts.get(key, '-')} |")
    lines.extend(
        [
            "",
            "## 当前边界",
            "",
            "- SolidWorks 入口只放开 `10` 门练习副本模板和 `12` 门标准基线模板。",
            "- 其它门数不再走逐零件直装，避免继续生成错乱模型。",
            "- 未做 Pack-and-Go 前，工程师移动模型时必须保留源文件目录。",
            "",
            "## 下一步",
            "",
        ]
    )
    for item in payload["nextGates"]:
        lines.append(f"- {item}")
    HANDOFF_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_role_rules_md(payload: dict[str, Any]) -> None:
    lines = [
        "# 16029 组件角色与变体规则表",
        "",
        f"- Generated at: `{payload['generatedAt']}`",
        f"- Quality gate: `{payload.get('qualityGate')}`",
        "- Purpose: 给后续门数变体生成器使用，不作为生产图纸放行。",
        "",
        "## SolidWorks 阵列种子",
        "",
        "| Feature | Instances | Spacing mm |",
        "| --- | ---: | ---: |",
    ]
    for item in payload["patternSeeds"]:
        lines.append(f"| {item.get('feature')} | {item.get('instances', '-')} | {item.get('spacingMm', '-')} |")
    lines.extend(["", "## STEP 角色数量", "", "| Role | Count |", "| --- | ---: |"])
    for role, count in sorted(payload["stepRoleCounts"].items()):
        lines.append(f"| {role} | {count} |")
    lines.extend(["", "## 重复角色组", "", "| Role | Subrole | BBox mm | Count | Samples |", "| --- | --- | ---: | ---: | --- |"])
    for item in payload["repeatedRoleGroups"]:
        samples = ", ".join(item.get("labelSamples", [])[:4])
        lines.append(
            f"| {item.get('role')} | {item.get('subrole')} | {item.get('bboxSignatureMm')} | {item.get('count')} | {samples} |"
        )
    lines.extend(["", "## SolidWorks 引用特征角色", "", "| Role | Count | Samples |", "| --- | ---: | --- |"])
    feature_roles = payload["solidworksFeatureRoles"]
    for role, count in sorted(feature_roles["roleCounts"].items()):
        samples = ", ".join(feature_roles["samples"].get(role, [])[:5])
        lines.append(f"| {role} | {count} | {samples} |")
    lines.extend(["", "## 距离配合种子", "", "| Mate | Dimension | Value mm |", "| --- | --- | ---: |"])
    for item in payload["distanceMateSeeds"]:
        lines.append(f"| {item.get('mate')} | {item.get('dimension')} | {item.get('valueMm')} |")
    lines.extend(["", "## 变体放行顺序", "", "| Target | Status | Reason |", "| --- | --- | --- |"])
    for item in payload["variantReadiness"]:
        lines.append(f"| {item['target']} | {item['status']} | {item['reason']} |")
    lines.extend(["", "## 阻塞原因", ""])
    for item in payload["blockedReasons"]:
        lines.append(f"- {item}")
    ROLE_RULES_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    now = datetime.now(timezone.utc).isoformat()
    rule_summary = read_json(RULE_RUN_DIR / "rule_learning_summary.json")
    handoff = build_handoff(now, rule_summary)
    role_rules = build_role_rules(now)
    HANDOFF_JSON.write_text(json.dumps(handoff, ensure_ascii=False, indent=2), encoding="utf-8")
    ROLE_RULES_JSON.write_text(json.dumps(role_rules, ensure_ascii=False, indent=2), encoding="utf-8")
    write_handoff_md(handoff)
    write_role_rules_md(role_rules)
    print(f"wrote {HANDOFF_JSON}")
    print(f"wrote {HANDOFF_MD}")
    print(f"wrote {ROLE_RULES_JSON}")
    print(f"wrote {ROLE_RULES_MD}")


if __name__ == "__main__":
    main()
