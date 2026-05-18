from __future__ import annotations

import csv
import json
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
EXTRACTIONS_DIR = ROOT / "workers" / "rule_extractions"
OUTPUT_JSON = ROOT / "data" / "rule_seed_candidates.json"
OUTPUT_MD = ROOT / "data" / "rule_seed_candidates.md"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_json(path: Path) -> dict[str, Any]:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def first_file(output_dir: Path, suffix: str) -> Path | None:
    matches = sorted(output_dir.glob(f"*{suffix}"))
    return matches[0] if matches else None


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


def normalize_float(value: Any) -> float | None:
    try:
        return round(float(value), 4)
    except (TypeError, ValueError):
        return None


def classify_pattern(feature_name: str) -> tuple[str, str]:
    if "层板" in feature_name:
        return "shelf_pitch", "层板节距"
    if "柜门" in feature_name or "门" in feature_name:
        return "door_column_pitch", "柜门列距/阵列"
    if "衣架" in feature_name or "钢管" in feature_name:
        return "hanger_rail_spacing", "衣架钢管间距"
    return "local_pattern", "局部阵列"


def classify_distance(component_names: str, distance_mm: float) -> tuple[str, str]:
    if "调整脚" in component_names:
        return "leveling_foot_offset", "调整脚高度/底部距离"
    if "应急维护门" in component_names:
        return "service_door_clearance", "应急维护门间隙"
    if "门框" in component_names and "门板" in component_names:
        return "door_panel_frame_gap", "门板与门框间隙"
    if "门框" in component_names and distance_mm in {2.0, 0.5}:
        return "frame_gap", "门框局部间隙"
    return "distance_mate", "距离配合"


def run_dirs() -> list[Path]:
    if not EXTRACTIONS_DIR.exists():
        return []
    latest_by_template: dict[str, tuple[float, Path]] = {}
    for path in EXTRACTIONS_DIR.iterdir():
        summary_path = path / "rule_extraction_run.json"
        if not path.is_dir() or not summary_path.exists():
            continue
        run = read_json(summary_path)
        if run.get("status") != "completed":
            continue
        template_key = str(run.get("template_id") or run.get("template_title") or path.name)
        timestamp = parse_run_timestamp(run.get("updated_at") or run.get("created_at"), path.stat().st_mtime)
        current = latest_by_template.get(template_key)
        if current is None or timestamp > current[0]:
            latest_by_template[template_key] = (timestamp, path)
    return [path for _, path in sorted(latest_by_template.values(), key=lambda item: item[1].name.lower())]


def collect_pattern_candidates(rows: list[dict[str, Any]], run_dir: Path, run: dict[str, Any], summary: dict[str, Any]) -> None:
    for pattern in summary.get("patternRules", []):
        if not isinstance(pattern, dict):
            continue
        feature_name = str(pattern.get("featureName", "")).strip()
        spacing = normalize_float(pattern.get("spacingMm"))
        instance_count = pattern.get("instanceCount")
        if not feature_name or spacing is None:
            continue
        rule_type, label = classify_pattern(feature_name)
        rows.append(
            {
                "ruleType": rule_type,
                "label": label,
                "source": "LocalLPattern dimension",
                "value": f"{instance_count or '?'} x {spacing:g}mm",
                "numericValueMm": spacing,
                "templateTitle": run.get("template_title", ""),
                "runId": run.get("id", run_dir.name),
                "evidenceFile": str(run_dir / "rule_learning_summary.json"),
                "confidence": "seed",
                "blocker": "Needs DXF/BOM evidence closure before generation.",
            }
        )


def collect_distance_candidates(rows: list[dict[str, Any]], run_dir: Path, run: dict[str, Any]) -> None:
    mate_path = first_file(run_dir, "_sw_api_snapshot_mates.csv")
    if not mate_path:
        return
    mates = read_csv(mate_path)
    grouped: dict[tuple[str, float, str], set[str]] = defaultdict(set)
    for row in mates:
        if row.get("mate_type_name") != "distance":
            continue
        distance = normalize_float(row.get("dimension_value_mm"))
        if distance is None:
            continue
        component_name = row.get("component_name", "").strip()
        rule_type, label = classify_distance(component_name, distance)
        grouped[(rule_type, distance, label)].add(component_name)

    for (rule_type, distance, label), components in sorted(grouped.items(), key=lambda item: (item[0][0], item[0][1])):
        component_preview = "; ".join(sorted(name for name in components if name)[:5])
        rows.append(
            {
                "ruleType": rule_type,
                "label": label,
                "source": "distance mate",
                "value": f"{distance:g}mm",
                "numericValueMm": distance,
                "templateTitle": run.get("template_title", ""),
                "runId": run.get("id", run_dir.name),
                "evidenceFile": str(mate_path),
                "confidence": "seed",
                "blocker": "Needs role binding and evidence closure before generation.",
                "componentPreview": component_preview,
            }
        )


def add_cross_template_notes(rows: list[dict[str, Any]]) -> None:
    buckets: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        buckets[(row["ruleType"], row["value"])].append(row)
    for row in rows:
        peers = buckets[(row["ruleType"], row["value"])]
        titles = sorted({str(peer["templateTitle"]) for peer in peers if peer.get("templateTitle")})
        row["matchedTemplates"] = titles
        row["confidence"] = "cross_template_seed" if len(titles) >= 2 else row["confidence"]


def collect() -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for run_dir in run_dirs():
        run = read_json(run_dir / "rule_extraction_run.json")
        summary = read_json(run_dir / "rule_learning_summary.json")
        collect_pattern_candidates(rows, run_dir, run, summary)
        collect_distance_candidates(rows, run_dir, run)
    add_cross_template_notes(rows)
    return rows


def write_markdown(rows: list[dict[str, Any]], path: Path, generated_at: str) -> None:
    lines = [
        "# 钣金规则种子候选表",
        "",
        f"- Generated at: `{generated_at}`",
        f"- Source: `{EXTRACTIONS_DIR}`",
        "",
        "这些是从 SolidWorks API 读到的候选规则，不是已放行的自动生成规则。",
        "同一个模板重复提取时，只保留最新一次 completed 结果，避免旧证据重复进入规则库。",
        "",
        "| Rule | Source | Value | Template | Confidence | Blocker |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for row in rows:
        template = str(row["templateTitle"]).replace("|", "/")
        lines.append(
            "| "
            + " | ".join(
                [
                    str(row["label"]).replace("|", "/"),
                    str(row["source"]).replace("|", "/"),
                    str(row["value"]).replace("|", "/"),
                    template,
                    str(row["confidence"]),
                    str(row["blocker"]).replace("|", "/"),
                ]
            )
            + " |"
        )

    lines.extend(["", "## Cross-template seeds", ""])
    seen: set[tuple[str, str]] = set()
    for row in rows:
        key = (row["ruleType"], row["value"])
        if key in seen or len(row.get("matchedTemplates", [])) < 2:
            continue
        seen.add(key)
        templates = " / ".join(row["matchedTemplates"])
        lines.append(f"- {row['label']} `{row['value']}` appears in: {templates}.")
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    generated_at = utc_now()
    rows = collect()
    payload = {
        "generatedAt": generated_at,
        "sourceDir": str(EXTRACTIONS_DIR),
        "candidateCount": len(rows),
        "candidates": rows,
    }
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(rows, OUTPUT_MD, generated_at)
    print(f"Wrote {OUTPUT_JSON}")
    print(f"Wrote {OUTPUT_MD}")
    print(f"candidates={len(rows)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
