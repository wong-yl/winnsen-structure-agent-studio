from __future__ import annotations

import csv
import json
import math
import os
import re
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
BATCH_DIR = Path(
    os.getenv(
        "STUDIO_16029_SHEETMETAL_BATCH_DIR",
        ROOT_DIR / "workers" / "drawing_sheetmetal" / "runs" / "BATCH-16029-SHEETMETAL-20260519",
    )
)
MANIFEST_PATH = Path(os.getenv("STUDIO_16029_SHEETMETAL_BATCH_MANIFEST", BATCH_DIR / "manifest.json"))
SUMMARY_CSV_PATH = Path(os.getenv("STUDIO_16029_SHEETMETAL_BATCH_SUMMARY", BATCH_DIR / "summary.csv"))
OUTPUT_JSON_PATH = Path(os.getenv("STUDIO_16029_SHEETMETAL_RULE_EVIDENCE_JSON", ROOT_DIR / "data" / "sheetmetal_rule_evidence_16029.json"))
OUTPUT_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_SHEETMETAL_RULE_EVIDENCE_MD", ROOT_DIR / "data" / "sheetmetal_rule_evidence_16029.md")
)
OUTPUT_CSV_PATH = Path(os.getenv("STUDIO_16029_SHEETMETAL_RULE_EVIDENCE_CSV", ROOT_DIR / "data" / "sheetmetal_rule_evidence_16029.csv"))

ACCEPTED_STATUSES = {"rule_seed_candidate", "geometry_rule_seed_only"}
BLOCKED_STATUSES = {"needs_layout_filter", "needs_closed_loop_rebuild", "blocked_no_bbox"}
MAIN_SOURCE_MARKER = "\\2.钣金展开图\\"
BACKUP_SOURCE_MARKER = "\\10.备份\\"
DOOR_INDEX_RE = re.compile(r"门板(?P<index>\d+)[╱/\\]12", re.IGNORECASE)


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_summary_rows(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        raise FileNotFoundError(f"Summary CSV was not found: {path}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    for row in rows:
        for field in (
            "bbox_width_mm",
            "bbox_height_mm",
            "raw_bbox_width_mm",
            "raw_bbox_height_mm",
            "thickness_mm_from_name",
        ):
            row[field] = to_float(row.get(field))
        for field in (
            "closed_loop_count",
            "open_endpoint_count",
            "connected_component_count",
            "circle_count",
            "arc_count",
            "line_count",
            "polyline_count",
            "warning_count",
        ):
            row[field] = to_int(row.get(field))
        row["is_main_source"] = is_main_source(str(row.get("source_path", "")))
    return rows


def to_float(value: Any) -> float | None:
    if value is None or value == "":
        return None
    try:
        number = float(value)
    except (TypeError, ValueError):
        return None
    if math.isnan(number) or math.isinf(number):
        return None
    return number


def to_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return None


def is_main_source(source_path: str) -> bool:
    normalized = source_path.replace("/", "\\")
    return MAIN_SOURCE_MARKER in normalized and BACKUP_SOURCE_MARKER not in normalized


def accepted(row: dict[str, Any]) -> bool:
    return str(row.get("quality_status", "")) in ACCEPTED_STATUSES


def finite_bbox(row: dict[str, Any]) -> bool:
    return to_float(row.get("bbox_width_mm")) is not None and to_float(row.get("bbox_height_mm")) is not None


def bbox_pair(row: dict[str, Any]) -> tuple[float, float] | None:
    width = to_float(row.get("bbox_width_mm"))
    height = to_float(row.get("bbox_height_mm"))
    if width is None or height is None:
        return None
    return width, height


def normalized_bbox(row: dict[str, Any]) -> tuple[float, float] | None:
    pair = bbox_pair(row)
    if pair is None:
        return None
    width, height = pair
    return round(max(width, height), 1), round(min(width, height), 1)


def bbox_text(row: dict[str, Any]) -> str:
    pair = bbox_pair(row)
    if pair is None:
        return "-"
    return f"{round(pair[0], 3)} x {round(pair[1], 3)} mm"


def source_example(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "file_name": row.get("file_name", ""),
        "quality_status": row.get("quality_status", ""),
        "bbox_mm": {"width": row.get("bbox_width_mm"), "height": row.get("bbox_height_mm")},
        "source_path": row.get("source_path", ""),
    }


def round_mm(value: float | None, digits: int = 3) -> float | None:
    if value is None:
        return None
    return round(value, digits)


def fit_door_panel_sequence(rows: list[dict[str, Any]]) -> dict[str, Any] | None:
    samples: list[dict[str, Any]] = []
    for row in rows:
        file_name = str(row.get("file_name", ""))
        match = DOOR_INDEX_RE.search(file_name)
        if not match or not row.get("is_main_source") or not accepted(row):
            continue
        pair = bbox_pair(row)
        if pair is None:
            continue
        width, height = pair
        if not (450 <= width <= 500 and 100 <= height <= 1000):
            continue
        samples.append({"index": int(match.group("index")), "width": width, "height": height, "row": row})

    samples = sorted(samples, key=lambda item: item["index"])
    if len(samples) < 2:
        return None

    xs = [float(item["index"]) for item in samples]
    ys = [float(item["height"]) for item in samples]
    x_mean = sum(xs) / len(xs)
    y_mean = sum(ys) / len(ys)
    denominator = sum((x - x_mean) ** 2 for x in xs)
    if denominator <= 0:
        return None
    slope = sum((x - x_mean) * (y - y_mean) for x, y in zip(xs, ys)) / denominator
    intercept = y_mean - slope * x_mean
    base_at_index_1 = intercept + slope
    max_residual = max(abs(y - (intercept + slope * x)) for x, y in zip(xs, ys))
    widths = [float(item["width"]) for item in samples]
    width_avg = sum(widths) / len(widths)
    width_span = max(widths) - min(widths)
    confidence = "high" if len(samples) >= 5 and max_residual <= 0.2 and width_span <= 0.5 else "medium"

    return {
        "id": "16029-door-panel-1-to-6-flat-height-series",
        "title": "16029 储物柜门板 1/12-6/12 展开高度序列",
        "role": "door_panel",
        "evidence_level": "engineering_reference",
        "confidence": confidence,
        "formula_text": f"flat_height_mm = {base_at_index_1:.1f} + (door_index - 1) * {slope:.1f}",
        "dimensions_mm": {
            "flat_width_avg": round_mm(width_avg, 1),
            "flat_width_span": round_mm(width_span, 3),
            "base_height_at_index_1": round_mm(base_at_index_1, 1),
            "height_step_per_index": round_mm(slope, 1),
            "max_fit_residual": round_mm(max_residual, 3),
        },
        "sample_count": len(samples),
        "samples": [
            {
                "door_index": item["index"],
                "flat_width_mm": round_mm(item["width"], 3),
                "flat_height_mm": round_mm(item["height"], 3),
                "file_name": item["row"].get("file_name", ""),
                "source_path": item["row"].get("source_path", ""),
            }
            for item in samples
        ],
        "usage_note": "这是 DXF 展开图的门板尺寸序列证据，可用于生成器的单件规则校准；不是直接等同于整柜装配门缝或外观尺寸。",
    }


def group_by_normalized_bbox(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    grouped: dict[tuple[float, float], list[tuple[dict[str, Any], tuple[float, float]]]] = defaultdict(list)
    for row in rows:
        pair = normalized_bbox(row)
        if pair is not None:
            long_mm, short_mm = pair
            bucket = (round(long_mm * 2) / 2, round(short_mm * 2) / 2)
            grouped[bucket].append((row, pair))
    groups = []
    for group_items in grouped.values():
        group_rows = [item[0] for item in group_items]
        long_values = [item[1][0] for item in group_items]
        short_values = [item[1][1] for item in group_items]
        long_mm = round(sum(long_values) / len(long_values), 1)
        short_mm = round(sum(short_values) / len(short_values), 1)
        accepted_count = sum(1 for item in group_rows if accepted(item))
        main_count = sum(1 for item in group_rows if item.get("is_main_source"))
        groups.append(
            {
                "long_mm": long_mm,
                "short_mm": short_mm,
                "rows": group_rows,
                "source_files_count": len(group_rows),
                "accepted_source_files_count": accepted_count,
                "main_source_files_count": main_count,
            }
        )
    return sorted(
        groups,
        key=lambda item: (
            item["accepted_source_files_count"],
            item["main_source_files_count"],
            item["source_files_count"],
        ),
        reverse=True,
    )


def candidate_from_rows(
    *,
    rows: list[dict[str, Any]],
    candidate_id: str,
    title: str,
    role: str,
    rule_seed: str,
    note: str,
    confidence_floor: str = "medium",
) -> dict[str, Any] | None:
    rows = [row for row in rows if finite_bbox(row)]
    if not rows:
        return None
    grouped = group_by_normalized_bbox(rows)
    if not grouped:
        return None
    best = grouped[0]
    group_rows = best["rows"]
    accepted_count = best["accepted_source_files_count"]
    blocked_count = len(group_rows) - accepted_count
    if accepted_count >= 2 and blocked_count == 0:
        confidence = "high"
    elif accepted_count >= 1:
        confidence = confidence_floor
    else:
        confidence = "low"
    return {
        "id": candidate_id,
        "title": title,
        "role": role,
        "evidence_level": "engineering_reference",
        "confidence": confidence,
        "dimensions_mm": {"long": best["long_mm"], "short": best["short_mm"]},
        "source_files_count": best["source_files_count"],
        "accepted_source_files_count": accepted_count,
        "blocked_source_files_count": blocked_count,
        "rule_seed": rule_seed,
        "notes": note,
        "source_examples": [source_example(row) for row in group_rows[:4]],
    }


def build_rule_candidates(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []

    full_door_rows = [
        row
        for row in rows
        if row.get("is_main_source")
        and accepted(row)
        and "储物柜门板12" in str(row.get("file_name", ""))
        and "加强筋" not in str(row.get("file_name", ""))
    ]
    full_door = candidate_from_rows(
        rows=full_door_rows,
        candidate_id="16029-door-panel-12-flat",
        title="16029 储物柜门板 12/12 展开件",
        role="door_panel",
        rule_seed="door_panel_full_height_flat_pattern_bbox",
        note="文件名带 0.8mm 厚度，适合作为门板展开外形和厚度的强证据；后续需要与 10/12/14 门装配门缝规则分开使用。",
        confidence_floor="high",
    )
    if full_door:
        candidates.append(full_door)

    reinforcement_rows = [
        row
        for row in rows
        if row.get("is_main_source")
        and accepted(row)
        and "门框加强筋" in str(row.get("file_name", ""))
    ]
    reinforcement = candidate_from_rows(
        rows=reinforcement_rows,
        candidate_id="16029-door-panel-reinforcement-flat",
        title="16029 门板/门框加强筋展开件",
        role="door_reinforcement",
        rule_seed="door_reinforcement_bbox_and_hole_pattern",
        note="可先做加强筋长度、宽度和孔位数量证据，暂不自动推断折弯扣减。",
    )
    if reinforcement:
        candidates.append(reinforcement)

    shelf_rows = [
        row
        for row in rows
        if row.get("is_main_source")
        and "箱体横层板" in str(row.get("file_name", ""))
        and "加强筋" not in str(row.get("file_name", ""))
        and str(row.get("quality_status", "")) in ACCEPTED_STATUSES.union({"needs_closed_loop_rebuild"})
    ]
    shelf = candidate_from_rows(
        rows=shelf_rows,
        candidate_id="16029-cabinet-shelf-flat",
        title="16029 箱体横层板 L/R 展开外形",
        role="shelf",
        rule_seed="shelf_flat_pattern_normalized_bbox",
        note="L 件已可作为几何种子，R 件同尺寸但需要闭合轮廓修复；生成器先用归一化 bbox，不直接释放正式展开。",
    )
    if shelf:
        candidates.append(shelf)

    shelf_stiffener_rows = [
        row
        for row in rows
        if row.get("is_main_source")
        and accepted(row)
        and "箱体横层板加强筋" in str(row.get("file_name", ""))
    ]
    shelf_stiffener = candidate_from_rows(
        rows=shelf_stiffener_rows,
        candidate_id="16029-shelf-stiffener-flat",
        title="16029 箱体横层板加强筋展开件",
        role="shelf_stiffener",
        rule_seed="shelf_stiffener_bbox",
        note="可用于层板加强筋单件外形候选，后续补孔位/折弯方向。",
    )
    if shelf_stiffener:
        candidates.append(shelf_stiffener)

    vertical_divider_rows = [
        row
        for row in rows
        if "箱体竖隔板" in str(row.get("file_name", ""))
        and "加强" not in str(row.get("file_name", ""))
        and accepted(row)
        and (pair := normalized_bbox(row)) is not None
        and 1700 <= pair[0] <= 2000
        and 500 <= pair[1] <= 650
    ]
    vertical_divider = candidate_from_rows(
        rows=vertical_divider_rows,
        candidate_id="16029-cabinet-vertical-divider-flat",
        title="16029 箱体竖隔板 L/R 展开外形",
        role="cabinet_divider",
        rule_seed="vertical_divider_flat_pattern_bbox_and_hole_count",
        note="主目录和备份目录均反复出现 1856.6 x 583.7mm 级别外形，可作为竖隔板规则基准。",
        confidence_floor="high",
    )
    if vertical_divider:
        candidates.append(vertical_divider)

    frame_horizontal_rows = [
        row
        for row in rows
        if row.get("is_main_source")
        and accepted(row)
        and "门框 横隔板" in str(row.get("file_name", ""))
        and (pair := normalized_bbox(row)) is not None
        and 430 <= pair[0] <= 460
        and 45 <= pair[1] <= 55
    ]
    frame_horizontal = candidate_from_rows(
        rows=frame_horizontal_rows,
        candidate_id="16029-door-frame-horizontal-divider-flat",
        title="16029 门框横隔板展开外形",
        role="door_frame_divider",
        rule_seed="door_frame_horizontal_divider_bbox",
        note="横隔板两种方向文件尺寸一致，可优先用于门数变化时的横隔板数量和位置规则。",
        confidence_floor="high",
    )
    if frame_horizontal:
        candidates.append(frame_horizontal)

    frame_vertical_rows = [
        row
        for row in rows
        if row.get("is_main_source")
        and accepted(row)
        and "门框 竖隔板" in str(row.get("file_name", ""))
        and (pair := normalized_bbox(row)) is not None
        and 1750 <= pair[0] <= 1900
        and 45 <= pair[1] <= 55
    ]
    frame_vertical = candidate_from_rows(
        rows=frame_vertical_rows,
        candidate_id="16029-door-frame-vertical-divider-flat",
        title="16029 门框竖隔板展开外形",
        role="door_frame_divider",
        rule_seed="door_frame_vertical_divider_bbox",
        note="竖隔板 L/R 主目录尺寸稳定；备份目录中 9170mm 异常样本被排除。",
    )
    if frame_vertical:
        candidates.append(frame_vertical)

    return candidates


def build_blocked_summary(rows: list[dict[str, Any]]) -> dict[str, Any]:
    blocked = [row for row in rows if str(row.get("quality_status", "")) in BLOCKED_STATUSES]
    noisy_large = [
        row
        for row in rows
        if (pair := normalized_bbox(row)) is not None
        and (pair[0] > 2500 or pair[1] > 1200)
        and not ("储物柜门板12" in str(row.get("file_name", "")) and pair[0] < 2000)
    ]
    counts = Counter(str(row.get("quality_status", "")) for row in blocked)
    return {
        "blocked_count": len(blocked),
        "quality_status_counts": dict(counts),
        "large_bbox_noise_count": len(noisy_large),
        "blocked_examples": [
            {
                "file_name": row.get("file_name", ""),
                "quality_status": row.get("quality_status", ""),
                "bbox_text": bbox_text(row),
                "source_path": row.get("source_path", ""),
            }
            for row in blocked[:8]
        ],
        "large_bbox_noise_examples": [
            {
                "file_name": row.get("file_name", ""),
                "quality_status": row.get("quality_status", ""),
                "bbox_text": bbox_text(row),
                "source_path": row.get("source_path", ""),
            }
            for row in noisy_large[:5]
        ],
    }


def build_payload() -> dict[str, Any]:
    manifest = read_json(MANIFEST_PATH)
    rows = read_summary_rows(SUMMARY_CSV_PATH)
    status_counts = Counter(str(row.get("quality_status", "")) for row in rows)
    role_counts = Counter(str(row.get("role_guess", "")) for row in rows)
    main_rows = [row for row in rows if row.get("is_main_source")]
    accepted_rows = [row for row in rows if accepted(row)]
    formulas = []
    door_formula = fit_door_panel_sequence(rows)
    if door_formula:
        formulas.append(door_formula)
    candidates = build_rule_candidates(rows)
    blocked_summary = build_blocked_summary(rows)

    return {
        "generated_at": now_local_iso(),
        "product_family": "16029",
        "source_batch": {
            "run_id": manifest.get("runId", BATCH_DIR.name),
            "source_root": manifest.get("sourceRoot", ""),
            "batch_dir": str(BATCH_DIR),
            "manifest_path": str(MANIFEST_PATH),
            "summary_csv_path": str(SUMMARY_CSV_PATH),
        },
        "summary": {
            "file_count": len(rows),
            "main_source_file_count": len(main_rows),
            "accepted_rule_seed_file_count": len(accepted_rows),
            "formula_count": len(formulas),
            "candidate_count": len(candidates),
            "quality_status_counts": dict(status_counts),
            "role_counts": dict(role_counts),
        },
        "formulas": formulas,
        "rule_candidates": candidates,
        "blocked_evidence_summary": blocked_summary,
        "generator_guidance": [
            "先把 16029 同外形 10/12/14 门的门板、层板、门框横隔板、竖隔板规则跑通，不做全量变种库存。",
            "门板 1/12-6/12 展开高度序列可作为尺寸变化证据，但整柜门缝、铰链、锁孔位置仍需独立装配规则。",
            "needs_layout_filter 和 needs_closed_loop_rebuild 样本不能直接驱动模型生成，只能进入待修复证据池。",
            "SolidWorks 交付前，应先用 FreeCAD/STEP 或 SolidWorks 低并发检查 bbox、实体数和装配位置。",
        ],
        "output_paths": {
            "json": str(OUTPUT_JSON_PATH),
            "markdown": str(OUTPUT_MARKDOWN_PATH),
            "csv": str(OUTPUT_CSV_PATH),
        },
    }


def write_markdown(payload: dict[str, Any]) -> None:
    summary = payload["summary"]
    lines = [
        "# 16029 钣金规则证据候选",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Source batch: `{payload['source_batch']['run_id']}`",
        f"- Source root: `{payload['source_batch']['source_root']}`",
        f"- Files: `{summary['file_count']}` / accepted seeds: `{summary['accepted_rule_seed_file_count']}` / candidates: `{summary['candidate_count']}`",
        "",
        "## 已提炼公式",
        "",
    ]
    if payload["formulas"]:
        for formula in payload["formulas"]:
            dims = formula["dimensions_mm"]
            lines.extend(
                [
                    f"### {formula['title']}",
                    "",
                    f"- Formula: `{formula['formula_text']}`",
                    f"- Width avg: `{dims['flat_width_avg']} mm`",
                    f"- Step residual: `{dims['max_fit_residual']} mm`",
                    f"- Confidence: `{formula['confidence']}`",
                    f"- Samples: `{formula['sample_count']}`",
                    f"- Note: {formula['usage_note']}",
                    "",
                ]
            )
    else:
        lines.append("No formula candidate was derived.")
        lines.append("")

    lines.extend(
        [
            "## 规则候选件",
            "",
            "| ID | 角色 | 尺寸证据 | 样本数 | 可信度 | 用途 |",
            "| --- | --- | --- | ---: | --- | --- |",
        ]
    )
    for candidate in payload["rule_candidates"]:
        dims = candidate["dimensions_mm"]
        lines.append(
            "| "
            + " | ".join(
                [
                    candidate["id"],
                    candidate["role"],
                    f"{dims['long']} x {dims['short']} mm",
                    str(candidate["source_files_count"]),
                    candidate["confidence"],
                    candidate["rule_seed"],
                ]
            )
            + " |"
        )
    lines.extend(["", "## 不进入自动生成的证据", ""])
    blocked = payload["blocked_evidence_summary"]
    lines.append(f"- Blocked files: `{blocked['blocked_count']}`")
    lines.append(f"- Large bbox noise files: `{blocked['large_bbox_noise_count']}`")
    lines.append(f"- Status counts: `{blocked['quality_status_counts']}`")
    lines.append("")
    lines.extend(["## 生成器使用建议", ""])
    for item in payload["generator_guidance"]:
        lines.append(f"- {item}")
    lines.append("")
    OUTPUT_MARKDOWN_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MARKDOWN_PATH.write_text("\n".join(lines), encoding="utf-8")


def write_candidate_csv(payload: dict[str, Any]) -> None:
    OUTPUT_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "id",
                "title",
                "role",
                "long_mm",
                "short_mm",
                "source_files_count",
                "accepted_source_files_count",
                "confidence",
                "rule_seed",
                "notes",
            ],
        )
        writer.writeheader()
        for candidate in payload["rule_candidates"]:
            dims = candidate["dimensions_mm"]
            writer.writerow(
                {
                    "id": candidate["id"],
                    "title": candidate["title"],
                    "role": candidate["role"],
                    "long_mm": dims.get("long"),
                    "short_mm": dims.get("short"),
                    "source_files_count": candidate["source_files_count"],
                    "accepted_source_files_count": candidate["accepted_source_files_count"],
                    "confidence": candidate["confidence"],
                    "rule_seed": candidate["rule_seed"],
                    "notes": candidate["notes"],
                }
            )


def main() -> None:
    payload = build_payload()
    OUTPUT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(payload)
    write_candidate_csv(payload)
    print(json.dumps({"status": "ok", "json": str(OUTPUT_JSON_PATH), "candidates": payload["summary"]["candidate_count"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
