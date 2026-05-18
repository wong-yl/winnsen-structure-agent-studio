from __future__ import annotations

import json
import re
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
CANDIDATES_JSON = ROOT / "data" / "rule_seed_candidates.json"
OUTPUT_JSON = ROOT / "data" / "rule_seed_review_ledger.json"
OUTPUT_MD = ROOT / "data" / "rule_seed_review_ledger.md"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_json(path: Path) -> dict[str, Any]:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def slug(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9]+", "_", value).strip("_")
    return cleaned.upper()[:36] or "ITEM"


def unique(values: list[Any]) -> list[str]:
    return sorted({str(value) for value in values if str(value).strip()})


def evidence_requirements(rule_type: str) -> list[str]:
    common = ["证据闭环记录"]
    if rule_type in {"shelf_pitch", "door_column_pitch", "hanger_rail_spacing", "local_pattern"}:
        return ["DXF 展开尺寸", "BOM/装配数量", *common]
    if rule_type in {"door_panel_frame_gap", "frame_gap", "service_door_clearance"}:
        return ["SolidWorks mate 角色绑定", "DXF/工程图间隙", *common]
    if rule_type == "leveling_foot_offset":
        return ["底座/调整脚工程图", "BOM 标准件", *common]
    return ["SolidWorks mate 角色绑定", *common]


def recommended_action(rule_type: str, label: str) -> str:
    if rule_type == "shelf_pitch":
        return "自动核对层板 DXF 高度、BOM 层板数量和总装阵列数量，闭环后进入层板节距规则。"
    if rule_type == "door_column_pitch":
        return "自动核对门框横隔板、门板宽高和门数量关系，闭环后再进入同尺寸补门数生成器。"
    if rule_type == "hanger_rail_spacing":
        return "自动核对衣架钢管数量、安装孔位和左右仓关系，先作为附件布置规则。"
    if rule_type in {"door_panel_frame_gap", "frame_gap"}:
        return "自动绑定距离配合两侧零件角色，再判断是否为可复用门缝/框缝规则。"
    if rule_type == "service_door_clearance":
        return "自动分离应急维护门与普通门的间隙定义，避免误套到普通门。"
    if rule_type == "leveling_foot_offset":
        return "自动区分调整脚高度是装配状态、采购规格还是安装余量，不直接参与钣金展开。"
    return f"先做 {label} 的来源证据闭环，再决定是否进入生成器。"


def priority_for(candidates: list[dict[str, Any]]) -> str:
    template_count = len(unique([template for item in candidates for template in item.get("matchedTemplates", [])]))
    has_pattern = any(str(item.get("source", "")).lower().startswith("locallpattern") for item in candidates)
    if template_count >= 2 and has_pattern:
        return "P0"
    if template_count >= 2:
        return "P1"
    return "P2"


def confidence_for(candidates: list[dict[str, Any]]) -> str:
    template_count = len(unique([template for item in candidates for template in item.get("matchedTemplates", [])]))
    if template_count >= 3:
        return "cross_template_strong_seed"
    if template_count == 2:
        return "cross_template_seed"
    return "single_template_seed"


def build_ledger() -> dict[str, Any]:
    payload = read_json(CANDIDATES_JSON)
    candidates = payload.get("candidates", [])
    if not isinstance(candidates, list):
        candidates = []

    grouped: dict[tuple[str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for candidate in candidates:
        if not isinstance(candidate, dict):
            continue
        key = (
            str(candidate.get("ruleType", "")),
            str(candidate.get("label", "")),
            str(candidate.get("value", "")),
        )
        grouped[key].append(candidate)

    items: list[dict[str, Any]] = []
    for index, ((rule_type, label, value), rows) in enumerate(sorted(grouped.items()), start=1):
        templates = unique([template for item in rows for template in item.get("matchedTemplates", [])])
        run_ids = unique([item.get("runId", "") for item in rows])
        sources = unique([item.get("source", "") for item in rows])
        evidence_files = unique([item.get("evidenceFile", "") for item in rows])
        priority = priority_for(rows)
        item = {
            "id": f"SEED-{index:03d}-{slug(rule_type)}",
            "ruleType": rule_type,
            "label": label,
            "value": value,
            "priority": priority,
            "status": "needs_rule_calibration",
            "generationGate": "blocked_pending_evidence_closure",
            "calibrationMode": "automated_first",
            "confidence": confidence_for(rows),
            "sourceCount": len(rows),
            "matchedTemplates": templates,
            "runIds": run_ids,
            "sources": sources,
            "evidenceFiles": evidence_files,
            "requiredEvidence": evidence_requirements(rule_type),
            "recommendedAction": recommended_action(rule_type, label),
        }
        items.append(item)

    order = {"P0": 0, "P1": 1, "P2": 2}
    items.sort(key=lambda item: (order.get(item["priority"], 9), item["label"], item["value"]))
    return {
        "generatedAt": utc_now(),
        "source": str(CANDIDATES_JSON),
        "itemCount": len(items),
        "items": items,
    }


def write_markdown(ledger: dict[str, Any]) -> None:
    lines = [
        "# 规则种子定标台账",
        "",
        f"- Generated at: `{ledger['generatedAt']}`",
        f"- Source: `{ledger['source']}`",
        f"- Items: `{ledger['itemCount']}`",
        "",
        "这些项目默认不能进入生成器。系统先自动补 DXF/BOM/图纸证据闭环；只有冲突、缺失、或高风险项才进入人工定标。",
        "",
        "| ID | Priority | Rule | Value | Templates | Gate | Required evidence |",
        "| --- | --- | --- | --- | ---: | --- | --- |",
    ]
    for item in ledger["items"]:
        lines.append(
            "| "
            + " | ".join(
                [
                    f"`{item['id']}`",
                    item["priority"],
                    item["label"],
                    item["value"],
                    str(len(item["matchedTemplates"])),
                    f"`{item['generationGate']}`",
                    " / ".join(item["requiredEvidence"]),
                ]
            )
            + " |"
        )
    lines.extend(["", "## Recommended Actions", ""])
    for item in ledger["items"]:
        lines.append(f"- `{item['id']}` {item['label']} `{item['value']}`: {item['recommendedAction']}")
    lines.append("")
    OUTPUT_MD.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    ledger = build_ledger()
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON.write_text(json.dumps(ledger, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(ledger)
    print(f"Wrote {OUTPUT_JSON}")
    print(f"Wrote {OUTPUT_MD}")
    print(f"items={ledger['itemCount']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
