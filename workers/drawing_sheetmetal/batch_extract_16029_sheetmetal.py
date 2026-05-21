from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter
from datetime import datetime
from pathlib import Path
from typing import Any

from extract_dxf_sheetmetal_reference import summarize_dxf, write_outputs


DEFAULT_ROOT = Path(r"C:\Users\Administrator\Desktop\参数化模板素材\16029 寄存柜(标准组合式 1917×1000×550)")
DEFAULT_OUT_DIR = Path(r"D:\Winnsen_Structure_Agent_Studio\workers\drawing_sheetmetal\runs\BATCH-16029-SHEETMETAL-20260519")
DEFAULT_NAME_PATTERN = r"门板|层板|横隔|竖隔|隔板"
RULE_SEED_STATUSES = {"rule_seed_candidate", "geometry_rule_seed_only"}


def safe_name(value: str, max_length: int = 80) -> str:
    cleaned = re.sub(r"[^\w\u4e00-\u9fff.-]+", "_", value, flags=re.UNICODE).strip("._")
    return (cleaned or "item")[:max_length]


def iter_dxf_files(root: Path, name_pattern: str, limit: int | None) -> list[Path]:
    pattern = re.compile(name_pattern, re.IGNORECASE)
    files = [
        path
        for path in root.rglob("*")
        if path.is_file() and path.suffix.lower() == ".dxf" and pattern.search(path.name)
    ]
    files.sort(key=lambda item: (str(item.parent), item.name.lower()))
    return files[:limit] if limit else files


def compact_item(summary: dict[str, Any]) -> dict[str, Any]:
    flat_bbox = summary["flat_bbox_mm"]
    bbox = summary.get("manufacturing_bbox_mm") or flat_bbox
    loops = summary["loop_analysis"]
    return {
        "file_name": summary["file_name"],
        "source_path": summary["source_path"],
        "role_guess": summary["role_guess"],
        "quality_status": summary["quality_status"],
        "thickness_mm_from_name": summary["thickness_mm_from_name"],
        "manufacturing_bbox_source": summary.get("manufacturing_bbox_source", "raw_curve_bbox"),
        "bbox_width_mm": bbox["width"],
        "bbox_height_mm": bbox["height"],
        "raw_bbox_width_mm": flat_bbox["width"],
        "raw_bbox_height_mm": flat_bbox["height"],
        "closed_loop_count": loops["closed_loop_count"],
        "open_endpoint_count": loops["open_endpoint_count"],
        "connected_component_count": loops["connected_component_count"],
        "circle_count": summary["circle_count"],
        "arc_count": summary["arc_count"],
        "line_count": summary["line_count"],
        "polyline_count": summary["polyline_count"],
        "circle_radius_counts": summary["circle_radius_counts"],
        "space_counts": summary["space_counts"],
        "warning_count": len(summary["quality_warnings"]),
        "quality_warnings": summary["quality_warnings"],
    }


def write_manifest(manifest: dict[str, Any], out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    with (out_dir / "summary.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "file_name",
                "role_guess",
                "quality_status",
                "thickness_mm_from_name",
                "manufacturing_bbox_source",
                "bbox_width_mm",
                "bbox_height_mm",
                "raw_bbox_width_mm",
                "raw_bbox_height_mm",
                "closed_loop_count",
                "open_endpoint_count",
                "connected_component_count",
                "circle_count",
                "arc_count",
                "line_count",
                "polyline_count",
                "warning_count",
                "source_path",
            ],
        )
        writer.writeheader()
        for item in manifest["items"]:
            writer.writerow({field: item.get(field) for field in writer.fieldnames})

    (out_dir / "BATCH_SUMMARY.md").write_text(markdown_summary(manifest), encoding="utf-8")


def markdown_summary(manifest: dict[str, Any]) -> str:
    status_rows = "\n".join(f"- `{key}`: {value}" for key, value in manifest["qualityStatusCounts"].items())
    role_rows = "\n".join(f"- `{key}`: {value}" for key, value in manifest["roleCounts"].items())
    table_rows = []
    for item in manifest["items"][:40]:
        bbox_value = f"{item['bbox_width_mm']} x {item['bbox_height_mm']}"
        table_rows.append(
            "| {file} | {role} | {status} | {bbox} | {source} | {loops} | {circles} | {warns} |".format(
                file=item["file_name"].replace("|", "/"),
                role=item["role_guess"],
                status=item["quality_status"],
                bbox=bbox_value,
                source=item["manufacturing_bbox_source"],
                loops=item["closed_loop_count"],
                circles=item["circle_count"],
                warns=item["warning_count"],
            )
        )

    recommended = "\n".join(
        f"- `{item['file_name']}`: {item['role_guess']} / {item['bbox_width_mm']} x {item['bbox_height_mm']} mm"
        for item in manifest["ruleSeedCandidates"][:12]
    )
    if not recommended:
        recommended = "- 暂无。先做闭合轮廓重建和图纸空间过滤。"

    return "\n".join(
        [
            "# 16029 钣金 DXF 批量解析摘要",
            "",
            "> 输出级别：工程参考。用于筛选规则学习样本，不是正式生产展开图。",
            "",
            f"- 来源目录：`{manifest['sourceRoot']}`",
            f"- 输出目录：`{manifest['outDir']}`",
            f"- 扫描文件：`{manifest['fileCount']}`",
            f"- 可直接作为几何规则种子的文件：`{manifest['ruleSeedCandidateCount']}`",
            "",
            "## 质量状态统计",
            "",
            status_rows,
            "",
            "## 角色统计",
            "",
            role_rows,
            "",
            "## 优先规则种子",
            "",
            recommended,
            "",
            "## 明细前 40 项",
            "",
            "| 文件 | 角色 | 状态 | 规则 bbox mm | bbox 来源 | 闭合轮廓 | 圆/孔 | 警告 |",
            "| --- | --- | --- | --- | --- | ---: | ---: | ---: |",
            *table_rows,
            "",
            "## 下一步",
            "",
            "- 对 `needs_closed_loop_rebuild` 文件做 LINE/ARC 闭合轮廓重建。",
            "- 对 `needs_layout_filter` 文件剥离 paper-space / VIEWPORT 干扰。",
            "- 把门板、层板、横隔板的 bbox、孔径和阵列关系交叉绑定到 SolidWorks/BOM 角色规则。",
            "",
        ]
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Batch extract lightweight sheet-metal rule evidence from 16029 DXF files.")
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    parser.add_argument("--name-pattern", default=DEFAULT_NAME_PATTERN)
    parser.add_argument("--limit", type=int, default=120)
    parser.add_argument("--write-item-cards", action="store_true")
    args = parser.parse_args()

    if not args.root.exists():
        raise SystemExit(f"source root not found: {args.root}")

    out_dir = args.out_dir
    out_dir.mkdir(parents=True, exist_ok=True)
    items_dir = out_dir / "items"
    items_dir.mkdir(parents=True, exist_ok=True)

    summaries: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    for index, path in enumerate(iter_dxf_files(args.root, args.name_pattern, args.limit), start=1):
        try:
            summary = summarize_dxf(path)
            summaries.append(summary)
            item_dir = items_dir / f"{index:03d}_{safe_name(path.stem)}"
            item_dir.mkdir(parents=True, exist_ok=True)
            (item_dir / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
            if args.write_item_cards:
                write_outputs(summary, item_dir)
        except Exception as error:  # noqa: BLE001 - record bad CAD evidence and continue batch.
            errors.append({"source_path": str(path), "error": str(error)})

    compact_items = [compact_item(summary) for summary in summaries]
    quality_counts = Counter(item["quality_status"] for item in compact_items)
    role_counts = Counter(item["role_guess"] for item in compact_items)
    rule_seed_candidates = [
        item
        for item in compact_items
        if item["quality_status"] in RULE_SEED_STATUSES
        and item["bbox_width_mm"]
        and item["bbox_height_mm"]
        and item["closed_loop_count"] > 0
    ]
    rule_seed_candidates.sort(key=lambda item: (item["role_guess"], -(item["bbox_width_mm"] or 0) * (item["bbox_height_mm"] or 0)))

    manifest = {
        "runId": out_dir.name,
        "createdAt": datetime.now().isoformat(timespec="seconds"),
        "sourceRoot": str(args.root),
        "outDir": str(out_dir),
        "namePattern": args.name_pattern,
        "fileCount": len(compact_items),
        "errorCount": len(errors),
        "qualityStatusCounts": dict(quality_counts.most_common()),
        "roleCounts": dict(role_counts.most_common()),
        "ruleSeedCandidateCount": len(rule_seed_candidates),
        "ruleSeedCandidates": rule_seed_candidates,
        "items": compact_items,
        "errors": errors,
    }
    write_manifest(manifest, out_dir)
    print(json.dumps({"status": "ok", "out_dir": str(out_dir), "file_count": len(compact_items), "errors": len(errors)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
