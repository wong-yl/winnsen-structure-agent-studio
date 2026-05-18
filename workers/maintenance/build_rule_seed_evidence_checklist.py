from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import openpyxl


ROOT = Path(__file__).resolve().parents[2]
LEDGER_JSON = ROOT / "data" / "rule_seed_review_ledger.json"
EXTRACTIONS_DIR = ROOT / "workers" / "rule_extractions"
OUTPUT_JSON = ROOT / "data" / "rule_seed_evidence_checklist.json"
OUTPUT_MD = ROOT / "data" / "rule_seed_evidence_checklist.md"

MAX_MATCHES_PER_TYPE = 12
EVIDENCE_SUFFIXES = {".dxf", ".xlsx", ".xls", ".slddrw", ".pdf"}
MAX_BOM_ROWS_PER_FILE = 24


FALLBACK_BOM_COLUMNS = {
    "sort": 4,
    "code": 5,
    "name": 6,
    "material": 9,
    "spec": 10,
    "quantity": 11,
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_json(path: Path) -> dict[str, Any]:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def template_root_from_run(run_id: str) -> Path | None:
    run = read_json(EXTRACTIONS_DIR / run_id / "rule_extraction_run.json")
    assembly_path = run.get("assembly_path")
    if not assembly_path:
        return None
    assembly = Path(str(assembly_path))
    if not assembly.exists():
        return None
    parts = assembly.parts
    for index, part in enumerate(parts):
        if part == "参数化模板素材" and index + 1 < len(parts):
            return Path(*parts[: index + 2])
    return assembly.parent


def tokens_for_rule(rule_type: str, label: str) -> list[str]:
    if rule_type == "shelf_pitch":
        return ["层板", "横层板", "置物架"]
    if rule_type == "door_column_pitch":
        return ["门板", "柜门", "储物柜门", "门框", "横隔板"]
    if rule_type in {"frame_gap", "door_panel_frame_gap"}:
        return ["门框", "门板", "横隔板"]
    if rule_type == "service_door_clearance":
        return ["应急维护门", "后下门板"]
    if rule_type == "leveling_foot_offset":
        return ["调整脚", "底座"]
    if rule_type == "hanger_rail_spacing":
        return ["衣架", "钢管"]
    return [label]


def evidence_type(path: Path) -> str:
    suffix = path.suffix.lower()
    if suffix == ".dxf":
        return "DXF"
    if suffix in {".xlsx", ".xls"}:
        return "BOM/XLS"
    if suffix == ".slddrw":
        return "SolidWorks drawing"
    if suffix == ".pdf":
        return "PDF drawing"
    return suffix.upper().lstrip(".")


def clean_cell(value: Any) -> str:
    if value is None:
        return ""
    return str(value).strip()


def normalize_quantity(value: Any) -> int | float | None:
    if value is None or value == "":
        return None
    try:
        number = float(value)
    except (TypeError, ValueError):
        return None
    return int(number) if number.is_integer() else round(number, 4)


def bom_files(root: Path) -> list[Path]:
    if not root.exists():
        return []
    files: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".xlsx", ".xls"}:
            continue
        if path.name.startswith("~$"):
            continue
        parent_text = str(path.parent).lower()
        if "bom" in path.name.lower() or "bom" in parent_text:
            files.append(path)
    return sorted(files, key=lambda item: str(item).lower())


def bom_header_map(rows: list[tuple[Any, ...]]) -> tuple[int, dict[str, int]]:
    for row_index, row in enumerate(rows[:12]):
        values = [clean_cell(value) for value in row]
        if "$文件名" in values and "$总数量" in values:
            return row_index, {
                "sort": values.index("$排序号") if "$排序号" in values else FALLBACK_BOM_COLUMNS["sort"],
                "code": values.index("零件图号") if "零件图号" in values else FALLBACK_BOM_COLUMNS["code"],
                "name": values.index("$文件名"),
                "material": values.index("材料") if "材料" in values else FALLBACK_BOM_COLUMNS["material"],
                "spec": values.index("材料规格") if "材料规格" in values else FALLBACK_BOM_COLUMNS["spec"],
                "quantity": values.index("$总数量"),
            }
    return 1, FALLBACK_BOM_COLUMNS.copy()


def row_value(row: tuple[Any, ...], index: int) -> Any:
    return row[index] if index < len(row) else None


def parse_bom_quantity_links(path: Path, tokens: list[str]) -> list[dict[str, Any]]:
    if path.suffix.lower() != ".xlsx":
        return []
    try:
        workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
    except Exception:
        return []

    matches: list[dict[str, Any]] = []
    for sheet in workbook.worksheets:
        rows = list(sheet.iter_rows(values_only=True))
        if not rows:
            continue
        header_index, columns = bom_header_map(rows)
        for row_number, row in enumerate(rows[header_index + 1 :], start=header_index + 2):
            name = clean_cell(row_value(row, columns["name"]))
            code = clean_cell(row_value(row, columns["code"]))
            if not name and not code:
                continue
            haystack = f"{name} {code}"
            matched_token = next((token for token in tokens if token and token in haystack), "")
            if not matched_token:
                continue
            quantity = normalize_quantity(row_value(row, columns["quantity"]))
            matches.append(
                {
                    "sheet": sheet.title,
                    "rowNumber": row_number,
                    "sort": clean_cell(row_value(row, columns["sort"])),
                    "code": code,
                    "name": name,
                    "material": clean_cell(row_value(row, columns["material"])),
                    "spec": clean_cell(row_value(row, columns["spec"])),
                    "quantity": quantity if quantity is not None else clean_cell(row_value(row, columns["quantity"])),
                    "matchedToken": matched_token,
                }
            )
            if len(matches) >= MAX_BOM_ROWS_PER_FILE:
                return matches
    return matches


def find_bom_quantity_links(root: Path, tokens: list[str]) -> list[dict[str, Any]]:
    links: list[dict[str, Any]] = []
    for path in bom_files(root):
        rows = parse_bom_quantity_links(path, tokens)
        if not rows:
            continue
        quantity_sum = 0.0
        numeric_count = 0
        for row in rows:
            quantity = row.get("quantity")
            if isinstance(quantity, (int, float)):
                quantity_sum += float(quantity)
                numeric_count += 1
        links.append(
            {
                "type": "BOM/XLS",
                "path": str(path),
                "name": path.name,
                "matchedRows": len(rows),
                "numericQuantitySum": int(quantity_sum) if quantity_sum.is_integer() else round(quantity_sum, 4),
                "numericQuantityCount": numeric_count,
                "rows": rows,
            }
        )
    return links


def find_evidence_files(root: Path, tokens: list[str]) -> list[dict[str, str]]:
    matches: list[dict[str, str]] = []
    if not root.exists():
        return matches
    counts: dict[str, int] = {}
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in EVIDENCE_SUFFIXES:
            continue
        name = path.name
        if not any(token and token in name for token in tokens):
            continue
        kind = evidence_type(path)
        if counts.get(kind, 0) >= MAX_MATCHES_PER_TYPE:
            continue
        counts[kind] = counts.get(kind, 0) + 1
        matches.append({"type": kind, "path": str(path), "name": path.name})
    return matches


def evidence_counts(template_evidence: list[dict[str, Any]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for evidence in template_evidence:
        for file in evidence.get("files", []):
            kind = str(file.get("type", ""))
            if not kind:
                continue
            counts[kind] = counts.get(kind, 0) + 1
        for link in evidence.get("bomQuantityLinks", []):
            counts["BOM linked rows"] = counts.get("BOM linked rows", 0) + int(link.get("matchedRows", 0) or 0)
    return counts


def evidence_state(item: dict[str, Any], counts: dict[str, int]) -> tuple[str, str]:
    has_dxf = counts.get("DXF", 0) > 0
    has_bom = counts.get("BOM/XLS", 0) > 0 or counts.get("BOM linked rows", 0) > 0
    has_drawing = counts.get("SolidWorks drawing", 0) > 0 or counts.get("PDF drawing", 0) > 0
    template_count = len(item.get("matchedTemplates", []))
    if has_dxf and has_bom and has_drawing and template_count >= 2:
        return "quantity_evidence_linked", "DXF、工程图和 BOM 行级数量已匹配，下一步核对阵列数、门数和 BOM 数量公式。"
    if has_dxf and has_drawing and template_count >= 2:
        return "needs_bom_or_quantity_link", "DXF 和工程图候选已匹配，还需要把 BOM/数量关系自动连上。"
    if has_dxf or has_drawing:
        return "partial_evidence", "已有局部文件证据，但还不足以自动闭环。"
    return "evidence_gap", "未找到足够候选文件，需要扩大搜索词或补充资料。"


def build_checklist() -> dict[str, Any]:
    ledger = read_json(LEDGER_JSON)
    items = ledger.get("items", [])
    if not isinstance(items, list):
        items = []

    checklist_items: list[dict[str, Any]] = []
    for item in items:
        if not isinstance(item, dict):
            continue
        if item.get("priority") not in {"P0", "P1"}:
            continue
        tokens = tokens_for_rule(str(item.get("ruleType", "")), str(item.get("label", "")))
        template_evidence: list[dict[str, Any]] = []
        for run_id in item.get("runIds", []):
            root = template_root_from_run(str(run_id))
            if root is None:
                continue
            bom_links = find_bom_quantity_links(root, tokens)
            files = find_evidence_files(root, tokens)
            for link in bom_links:
                files.append(
                    {
                        "type": "BOM/XLS",
                        "path": link["path"],
                        "name": link["name"],
                        "matchedRows": str(link["matchedRows"]),
                    }
                )
            template_evidence.append(
                {
                    "runId": run_id,
                    "templateRoot": str(root),
                    "tokens": tokens,
                    "files": files,
                    "bomQuantityLinks": bom_links,
                }
            )
        counts = evidence_counts(template_evidence)
        state, next_action = evidence_state(item, counts)
        checklist_items.append(
            {
                "id": item.get("id"),
                "priority": item.get("priority"),
                "label": item.get("label"),
                "value": item.get("value"),
                "generationGate": item.get("generationGate"),
                "matchedTemplates": item.get("matchedTemplates", []),
                "requiredEvidence": item.get("requiredEvidence", []),
                "recommendedAction": item.get("recommendedAction"),
                "evidenceCounts": counts,
                "evidenceState": state,
                "automationNextAction": next_action,
                "templateEvidence": template_evidence,
                "bomQuantityLinks": [
                    {
                        "runId": evidence.get("runId"),
                        "templateRoot": evidence.get("templateRoot"),
                        **link,
                    }
                    for evidence in template_evidence
                    for link in evidence.get("bomQuantityLinks", [])
                ],
            }
        )

    return {
        "generatedAt": utc_now(),
        "source": str(LEDGER_JSON),
        "itemCount": len(checklist_items),
        "items": checklist_items,
    }


def write_markdown(payload: dict[str, Any]) -> None:
    lines = [
        "# 规则种子证据核对清单",
        "",
        f"- Generated at: `{payload['generatedAt']}`",
        f"- Source: `{payload['source']}`",
        f"- Items: `{payload['itemCount']}`",
        "",
        "这里只列 P0/P1 项。系统先自动做证据闭环；候选文件不代表规则已放行。",
        "",
    ]
    for item in payload["items"]:
        lines.extend(
            [
                f"## {item['id']} {item['priority']} {item['label']} `{item['value']}`",
                "",
                f"- Gate: `{item['generationGate']}`",
                f"- Evidence state: `{item.get('evidenceState', '')}`",
                f"- Required: {' / '.join(item.get('requiredEvidence', []))}",
                f"- Action: {item.get('recommendedAction', '')}",
                f"- Automation next: {item.get('automationNextAction', '')}",
                "",
            ]
        )
        for evidence in item.get("templateEvidence", []):
            files = evidence.get("files", [])
            lines.append(f"- Template root: `{evidence.get('templateRoot', '')}`")
            bom_links = evidence.get("bomQuantityLinks", [])
            for link in bom_links:
                lines.append(
                    f"  - `BOM linked rows` `{link.get('path', '')}` rows={link.get('matchedRows', 0)} "
                    f"quantitySum={link.get('numericQuantitySum', 0)}"
                )
                for row in link.get("rows", [])[:8]:
                    lines.append(
                        f"    - row {row.get('rowNumber')}: {row.get('name')} qty={row.get('quantity')} "
                        f"token={row.get('matchedToken')}"
                    )
            if not files:
                lines.append("  - No token-matched evidence files found.")
                continue
            for file in files[:24]:
                lines.append(f"  - `{file['type']}` `{file['path']}`")
        lines.append("")
    OUTPUT_MD.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    payload = build_checklist()
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(payload)
    print(f"Wrote {OUTPUT_JSON}")
    print(f"Wrote {OUTPUT_MD}")
    print(f"items={payload['itemCount']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
