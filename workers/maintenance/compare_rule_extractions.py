from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EXTRACTIONS_DIR = ROOT / "workers" / "rule_extractions"
OUTPUT_JSON = ROOT / "data" / "rule_extraction_comparison.json"
OUTPUT_MD = ROOT / "data" / "rule_extraction_comparison.md"


def read_json(path: Path) -> dict:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def collect_rows() -> list[dict]:
    if not EXTRACTIONS_DIR.exists():
        return []

    rows: list[dict] = []
    for run_dir in sorted(EXTRACTIONS_DIR.iterdir(), key=lambda item: item.name.lower()):
        if not run_dir.is_dir():
            continue
        if not (run_dir / "rule_extraction_run.json").exists():
            continue
        run = read_json(run_dir / "rule_extraction_run.json")
        summary = read_json(run_dir / "rule_learning_summary.json")
        counts = summary.get("counts") if isinstance(summary.get("counts"), dict) else {}
        snapshot = summary.get("snapshot") if isinstance(summary.get("snapshot"), dict) else {}
        rows.append(
            {
                "id": run.get("id", run_dir.name),
                "templateTitle": run.get("template_title", ""),
                "assemblyPath": run.get("assembly_path", ""),
                "documentTitle": snapshot.get("documentTitle", ""),
                "status": run.get("status", "unknown"),
                "exitCode": run.get("exit_code"),
                "updatedAt": run.get("updated_at", ""),
                "components": counts.get("components", 0),
                "componentsWithTransform": counts.get("componentsWithTransform", 0),
                "componentsWithBBox": counts.get("componentsWithBBox", 0),
                "mateFeatureCount": counts.get("mateFeatureCount", 0),
                "features": counts.get("features", 0),
                "dimensions": counts.get("dimensions", 0),
                "patternRuleCount": counts.get("patternRuleCount", 0),
                "sheetMetalFeatureCount": counts.get("sheetMetalFeatureCount", 0),
                "stepObjectBBoxCount": counts.get("stepObjectBBoxCount", 0),
                "stepRoleBindingCount": counts.get("stepRoleBindingCount", 0),
                "stepRoleMatchedComponentCount": counts.get("stepRoleMatchedComponentCount", 0),
                "qualityGate": summary.get("qualityGate", "missing_summary"),
                "nextAction": summary.get("nextAction", "Generate rule_learning_summary before using this run."),
                "outputDir": str(run_dir),
            }
        )
    return rows


def write_outputs(rows: list[dict]) -> None:
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    generated_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    payload = {"generatedAt": generated_at, "sourceDir": str(EXTRACTIONS_DIR), "runs": rows}
    OUTPUT_JSON.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    lines = [
        "# SolidWorks rule extraction comparison",
        "",
        f"- Generated at: `{generated_at}`",
        f"- Source: `{EXTRACTIONS_DIR}`",
        "",
        "| Run | Template | Components | Transform | BBox | Mates | Features | Dimensions | Pattern seeds | STEP bboxes | Role bindings | Quality gate |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |",
    ]
    for row in rows:
        components = row["components"]
        lines.append(
            "| "
            + " | ".join(
                [
                    f"`{row['id']}`",
                    str(row["templateTitle"]).replace("|", "/"),
                    str(components),
                    f"{row['componentsWithTransform']}/{components}",
                    f"{row['componentsWithBBox']}/{components}",
                    str(row["mateFeatureCount"]),
                    str(row["features"]),
                    str(row["dimensions"]),
                    str(row["patternRuleCount"]),
                    str(row["stepObjectBBoxCount"]),
                    f"{row['stepRoleBindingCount']} / {row['stepRoleMatchedComponentCount']}",
                    f"`{row['qualityGate']}`",
                ]
            )
            + " |"
        )
    lines.extend(["", "## Next actions", ""])
    for row in rows:
        lines.append(f"- `{row['id']}`: {row['nextAction']}")
    lines.append("")
    OUTPUT_MD.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    rows = collect_rows()
    write_outputs(rows)
    print(f"Wrote {OUTPUT_JSON}")
    print(f"Wrote {OUTPUT_MD}")
    print(f"runs={len(rows)}")


if __name__ == "__main__":
    main()
