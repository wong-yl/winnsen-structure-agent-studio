from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter, defaultdict
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


def first_file(output_dir: Path, suffix: str) -> Path:
    matches = sorted(output_dir.glob(f"*{suffix}"))
    if not matches:
        raise SystemExit(f"Missing file with suffix {suffix} in {output_dir}")
    return matches[0]


def optional_first_file(output_dir: Path, suffix: str) -> Path | None:
    matches = sorted(output_dir.glob(f"*{suffix}"))
    return matches[0] if matches else None


def has_cjk(text: str) -> bool:
    return any("\u4e00" <= char <= "\u9fff" for char in text)


def recover_mojibake_label(text: str) -> str:
    if has_cjk(text):
        return text
    candidates = [text]
    for source_encoding in ("latin1", "cp1252"):
        for target_encoding in ("gbk", "gb18030"):
            try:
                recovered = text.encode(source_encoding).decode(target_encoding)
            except UnicodeError:
                continue
            candidates.append(recovered)
    return max(candidates, key=lambda candidate: sum(1 for char in candidate if "\u4e00" <= char <= "\u9fff"))


def clean_name(value: Any) -> str:
    text = str(value or "").strip()
    text = recover_mojibake_label(text)
    text = text.replace("╱", "/").replace("\\", "/")
    text = re.sub(r"\.(sldprt|sldasm)$", "", text, flags=re.IGNORECASE)
    text = re.sub(r"-\d+$", "", text)
    text = re.sub(r"(?<!M)(?<!V)(?<!ZJA-S)\d{3}$", "", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def normalize_key(value: Any) -> str:
    return clean_name(value).lower().replace(" ", "")


def parse_float(value: Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def bbox_center(row: dict[str, Any], axis: str) -> float:
    return round((parse_float(row[f"bbox_min_{axis}_mm"]) + parse_float(row[f"bbox_max_{axis}_mm"])) / 2, 4)


def classify_role(label: str) -> tuple[str, str]:
    if not label or label.lower().startswith("origin"):
        return "datum_or_origin", "ignore"
    if "电控锁" in label or "U型锁钩" in label or "锁架" in label or "锁安装" in label or "门锁" in label or "机柜锁" in label or "锁片" in label or "锁杆" in label or "插销" in label:
        return "lock_system", "lock_or_latch"
    if "门轴" in label or "轴套" in label or "挡圈" in label:
        return "hinge_or_pivot", "pin_bushing_retainer"
    if "储物柜门" in label or "后下门板" in label:
        if "装配" in label:
            return "door_module", "door_assembly"
        if "焊接" in label:
            return "door_module", "door_weldment"
        if "加强筋" in label:
            return "door_module", "door_reinforcement"
        return "door_module", "door_panel"
    if "门框" in label:
        if "焊接" in label:
            return "door_frame", "frame_weldment"
        if "竖隔板" in label:
            return "door_frame", "vertical_divider"
        if "横隔板" in label:
            return "door_frame", "horizontal_divider"
        if "上" in label:
            return "door_frame", "top_rail"
        if "下" in label:
            return "door_frame", "bottom_rail"
        return "door_frame", "frame_part"
    if "层板" in label:
        if "加强筋" in label:
            return "shelf_or_partition", "shelf_reinforcement"
        return "shelf_or_partition", "shelf_panel"
    if "竖隔板" in label:
        return "shelf_or_partition", "vertical_partition"
    if "横隔板" in label:
        return "shelf_or_partition", "horizontal_partition"
    if "上盖" in label:
        return "top_cover", "top_cover_part"
    if "侧板" in label:
        if "加强筋" in label:
            return "cabinet_body", "side_panel_reinforcement"
        return "cabinet_body", "side_panel"
    if "加强筋" in label:
        return "reinforcement", "general_reinforcement"
    if "底座" in label or "调整脚" in label or "螺母" in label:
        return "base_or_leveling", "base_leveling_part"
    if "衣架" in label or "衣杆" in label:
        return "hanger_rail", "hanger_or_bracket"
    if "电源" in label or "插座" in label or "24口锁控板" in label or label.startswith("LK-") or label.startswith("M9"):
        return "electronics_or_power", "electronics_part"
    if "拉绳" in label:
        return "emergency_release", "pull_rope_bracket"
    return "unclassified", "unclassified"


def component_keys(components: list[dict[str, str]]) -> list[dict[str, str]]:
    indexed: list[dict[str, str]] = []
    for row in components:
        candidates = {row.get("name", ""), row.get("file_name", "")}
        for part in str(row.get("name", "")).split("/"):
            candidates.add(part)
        keys = sorted({normalize_key(candidate) for candidate in candidates if normalize_key(candidate)}, key=len, reverse=True)
        indexed.append(
            {
                "name": row.get("name", ""),
                "file_name": row.get("file_name", ""),
                "path": row.get("path", ""),
                "keys": "\n".join(keys),
            }
        )
    return indexed


def match_component(label: str, indexed_components: list[dict[str, str]]) -> tuple[dict[str, str] | None, str, int]:
    label_key = normalize_key(label)
    if not label_key:
        return None, "", 0

    best: tuple[dict[str, str] | None, str, int] = (None, "", 0)
    for component in indexed_components:
        for key in component["keys"].splitlines():
            if not key or len(key) < 2:
                continue
            score = 0
            method = ""
            if label_key == key:
                score = 100
                method = "exact"
            elif label_key.startswith(key) and len(key) >= 4:
                score = 92
                method = "label_startswith_component"
            elif key.startswith(label_key) and len(label_key) >= 4:
                score = 88
                method = "component_startswith_label"
            elif key in label_key and len(key) >= 5:
                score = 74
                method = "component_key_in_label"
            elif label_key in key and len(label_key) >= 5:
                score = 70
                method = "label_key_in_component"
            if score > best[2]:
                best = (component, method, score)
    if best[2] < 70:
        return None, "", 0
    return best


def bbox_signature(row: dict[str, Any]) -> str:
    sizes = [parse_float(row.get(f"bbox_size_{axis}_mm")) for axis in ("x", "y", "z")]
    return " x ".join(f"{size:.1f}" for size in sizes)


def near(value: float, target: float, tolerance: float = 1.2) -> bool:
    return abs(value - target) <= tolerance


def classify_geometry(obj: dict[str, Any]) -> tuple[str, str] | None:
    x = parse_float(obj.get("bbox_size_x_mm"))
    y = parse_float(obj.get("bbox_size_y_mm"))
    z = parse_float(obj.get("bbox_size_z_mm"))

    if near(x, 233) and near(z, 20) and 850 <= y <= 1900:
        return "door_module", "door_panel"
    if near(x, 233) and 40 <= z <= 46 and 930 <= y <= 1900:
        return "door_module", "door_assembly"
    if x in {51.4, 58.4} and near(z, 12.5 if x == 51.4 else 15.0) and 850 <= y <= 1850:
        return "door_frame", "frame_part"
    if near(x, 241) and near(y, 15) and 22 <= z <= 24:
        return "door_frame", "horizontal_divider"
    if (near(x, 58.1) or near(x, 3.0)) and 1800 <= y <= 1850 and 20 <= z <= 35:
        return "door_frame", "vertical_divider"

    if (230 <= x <= 260 and near(y, 25) and 360 <= z <= 530) or (230 <= x <= 260 and 360 <= y <= 530 and near(z, 25)):
        return "shelf_or_partition", "shelf_panel"
    if 230 <= x <= 255 and 10 <= y <= 12 and 50 <= z <= 55:
        return "shelf_or_partition", "shelf_reinforcement"
    if 14 <= x <= 42 and 1800 <= y <= 1850 and 420 <= z <= 530:
        return "shelf_or_partition", "vertical_partition"

    lock_signatures = [
        (26, 116, 98.8),
        (30, 116, 98.8),
        (14, 80.8, 80.8),
        (14, 80.8, 92.8),
        (14, 80.8, 93.5),
        (5, 23.8, 17.6),
        (5, 25.6, 21.0),
        (5, 25.0, 13.0),
        (3, 4.5, 13.3),
        (14, 3.0, 3.0),
        (20.6, 34.0, 29.3),
        (18, 28.0, 13.2),
        (18, 30.0, 13.2),
        (30, 1826.0, 113.8),
    ]
    if any(near(x, sx, 1.4) and near(y, sy, 1.8) and near(z, sz, 1.8) for sx, sy, sz in lock_signatures):
        return "lock_system", "lock_or_latch"

    hinge_signatures = [
        (6, 60, 6),
        (6, 87, 6),
        (13, 7, 13),
        (10, 0.8, 8.6),
        (10, 0.8, 9.6),
    ]
    if any(near(x, sx, 1.0) and near(y, sy, 1.0) and near(z, sz, 1.0) for sx, sy, sz in hinge_signatures):
        return "hinge_or_pivot", "pin_bushing_retainer"

    if near(x, 20) and near(y, 30) and 85 <= z <= 90:
        return "hanger_rail", "hanger_or_bracket"
    if (near(x, 4.8) and 1750 <= y <= 1850 and 45 <= z <= 55) or (45 <= x <= 55 and 1750 <= y <= 1850 and near(z, 4.8)):
        return "cabinet_body", "side_panel_reinforcement"
    if near(x, 20.8) and near(y, 10.8) and near(z, 20.8):
        return "base_or_leveling", "base_leveling_part"

    return None


def bind_rows(output_dir: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    step_bbox_path = first_file(output_dir, "_step_object_bboxes.json")
    component_path = optional_first_file(output_dir, "_sw_api_snapshot_components.csv")
    step_payload = read_json(step_bbox_path)
    components = read_csv(component_path) if component_path else []
    indexed_components = component_keys(components)

    rows: list[dict[str, Any]] = []
    for obj in step_payload.get("objects", []):
        if not isinstance(obj, dict):
            continue
        label = clean_name(obj.get("object_label") or obj.get("object_name"))
        role, subrole = classify_role(label)
        if role == "unclassified":
            geometry_role = classify_geometry(obj)
            if geometry_role is not None:
                role, subrole = geometry_role
        if role == "datum_or_origin":
            continue
        matched, match_method, match_score = match_component(label, indexed_components)
        row = {
            "object_index": obj.get("object_index"),
            "object_label": label,
            "role": role,
            "subrole": subrole,
            "matched_component": matched.get("name", "") if matched else "",
            "matched_file": matched.get("file_name", "") if matched else "",
            "match_method": match_method,
            "match_score": match_score,
            "bbox_min_x_mm": obj.get("bbox_min_x_mm"),
            "bbox_min_y_mm": obj.get("bbox_min_y_mm"),
            "bbox_min_z_mm": obj.get("bbox_min_z_mm"),
            "bbox_max_x_mm": obj.get("bbox_max_x_mm"),
            "bbox_max_y_mm": obj.get("bbox_max_y_mm"),
            "bbox_max_z_mm": obj.get("bbox_max_z_mm"),
            "bbox_size_x_mm": obj.get("bbox_size_x_mm"),
            "bbox_size_y_mm": obj.get("bbox_size_y_mm"),
            "bbox_size_z_mm": obj.get("bbox_size_z_mm"),
            "bbox_center_x_mm": bbox_center(obj, "x"),
            "bbox_center_y_mm": bbox_center(obj, "y"),
            "bbox_center_z_mm": bbox_center(obj, "z"),
            "bbox_signature_mm": bbox_signature(obj),
            "solid_count": obj.get("solid_count"),
            "face_count": obj.get("face_count"),
            "edge_count": obj.get("edge_count"),
        }
        rows.append(row)

    role_counts = Counter(row["role"] for row in rows)
    subrole_counts = Counter(f"{row['role']}::{row['subrole']}" for row in rows)
    groups: dict[tuple[str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        groups[(row["role"], row["subrole"], row["bbox_signature_mm"])].append(row)

    repeated_groups = []
    for (role, subrole, signature), group_rows in groups.items():
        if len(group_rows) < 2:
            continue
        repeated_groups.append(
            {
                "role": role,
                "subrole": subrole,
                "bboxSignatureMm": signature,
                "count": len(group_rows),
                "labelSamples": [str(row["object_label"]) for row in group_rows[:8]],
                "centerXRangeMm": [min(row["bbox_center_x_mm"] for row in group_rows), max(row["bbox_center_x_mm"] for row in group_rows)],
                "centerYRangeMm": [min(row["bbox_center_y_mm"] for row in group_rows), max(row["bbox_center_y_mm"] for row in group_rows)],
                "centerZRangeMm": [min(row["bbox_center_z_mm"] for row in group_rows), max(row["bbox_center_z_mm"] for row in group_rows)],
            }
        )
    repeated_groups.sort(key=lambda item: (-int(item["count"]), str(item["role"]), str(item["subrole"])))

    summary = {
        "generatedAt": utc_now(),
        "outputDir": str(output_dir),
        "sourceStepBBox": str(step_bbox_path),
        "sourceComponentsCsv": str(component_path) if component_path else None,
        "objectCount": len(rows),
        "matchedComponentCount": sum(1 for row in rows if row["matched_component"]),
        "roleCounts": dict(role_counts),
        "subroleCounts": dict(subrole_counts),
        "repeatedGroupCount": len(repeated_groups),
        "repeatedGroups": repeated_groups[:80],
    }
    return rows, summary


def write_outputs(rows: list[dict[str, Any]], summary: dict[str, Any], output_dir: Path) -> None:
    run_id = output_dir.name
    csv_path = output_dir / f"{run_id}_step_component_role_bindings.csv"
    json_path = output_dir / f"{run_id}_step_component_role_bindings.json"
    md_path = output_dir / f"{run_id}_step_component_role_bindings.md"

    if rows:
        with csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)
    else:
        csv_path.write_text("", encoding="utf-8-sig")

    payload = {**summary, "bindings": rows}
    json_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    lines = [
        "# STEP component role bindings",
        "",
        f"- Generated at: `{summary['generatedAt']}`",
        f"- Objects bound: `{summary['objectCount']}`",
        f"- Matched to SolidWorks component rows: `{summary['matchedComponentCount']}`",
        f"- Repeated bbox groups: `{summary['repeatedGroupCount']}`",
        "",
        "## Role Counts",
        "",
        "| Role | Count |",
        "| --- | ---: |",
    ]
    for role, count in sorted(summary["roleCounts"].items()):
        lines.append(f"| {role} | {count} |")

    lines.extend(["", "## Repeated BBox Groups", "", "| Role | Subrole | BBox mm | Count | Samples |", "| --- | --- | ---: | ---: | --- |"])
    for group in summary["repeatedGroups"][:40]:
        samples = ", ".join(group["labelSamples"]).replace("|", "/")
        lines.append(f"| {group['role']} | {group['subrole']} | {group['bboxSignatureMm']} | {group['count']} | {samples} |")

    lines.extend(["", "## Interpretation", ""])
    lines.append("- This file binds SolidWorks-exported STEP object bboxes to engineering roles for rule derivation.")
    lines.append("- It is automatic evidence, not production drawing approval. Use repeated role groups as seeds for door count, shelf count, lock array, and divider rules.")
    md_path.write_text("\n".join(lines), encoding="utf-8")

    print(f"Wrote {csv_path}")
    print(f"Wrote {json_path}")
    print(f"Wrote {md_path}")
    print(f"bindings={summary['objectCount']} matched={summary['matchedComponentCount']} repeated_groups={summary['repeatedGroupCount']}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Bind STEP bbox objects back to component role evidence.")
    parser.add_argument("output_dir")
    args = parser.parse_args()
    output_dir = Path(args.output_dir)
    rows, summary = bind_rows(output_dir)
    write_outputs(rows, summary, output_dir)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
