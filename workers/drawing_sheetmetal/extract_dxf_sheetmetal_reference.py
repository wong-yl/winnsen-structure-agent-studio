from __future__ import annotations

import argparse
import csv
import json
import math
import re
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any


NUMBER_RE = re.compile(r"[-+]?\d+(?:\.\d+)?")
THICKNESS_RE = re.compile(r"[\[(（]([0-9]+(?:\.[0-9]+)?)[]）)]")
LOOP_TOLERANCE_MM = 0.05
MIN_OUTLINE_AREA_RATIO = 0.05
MIN_OUTLINE_DIMENSION_MM = 30


@dataclass
class Point:
    x: float
    y: float


def read_text(path: Path) -> str:
    raw = path.read_bytes()
    for encoding in ("utf-8-sig", "gbk", "latin-1"):
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    return raw.decode("latin-1", errors="replace")


def dxf_pairs(path: Path) -> list[tuple[str, str]]:
    lines = [line.rstrip("\r\n") for line in read_text(path).splitlines()]
    pairs: list[tuple[str, str]] = []
    index = 0
    while index + 1 < len(lines):
        code = lines[index].strip()
        value = lines[index + 1].strip()
        pairs.append((code, value))
        index += 2
    return pairs


def as_float(value: str) -> float | None:
    try:
        return float(value)
    except ValueError:
        return None


def entity_layer(pairs: list[tuple[str, str]]) -> str:
    for code, value in pairs:
        if code == "8":
            return value or "0"
    return "0"


def entity_space(pairs: list[tuple[str, str]]) -> dict[str, str]:
    paper_space = any(code == "67" and value == "1" for code, value in pairs)
    layout = next((value for code, value in pairs if code == "410"), "Model" if not paper_space else "Layout")
    return {"space": "paper" if paper_space else "model", "layout": layout or ("Layout" if paper_space else "Model")}


def point_to_dict(point: Point) -> dict[str, float]:
    return {"x": round(point.x, 6), "y": round(point.y, 6)}


def point_on_circle(cx: float, cy: float, radius: float, angle_degrees: float) -> Point:
    rad = math.radians(angle_degrees)
    return Point(cx + radius * math.cos(rad), cy + radius * math.sin(rad))


def points_from_entity(entity_type: str, pairs: list[tuple[str, str]]) -> tuple[list[Point], dict[str, Any]]:
    metadata: dict[str, Any] = {}
    points: list[Point] = []

    if entity_type == "LINE":
        values = {code: as_float(value) for code, value in pairs if code in {"10", "20", "11", "21"}}
        if all(values.get(code) is not None for code in ("10", "20", "11", "21")):
            start = Point(values["10"], values["20"])
            end = Point(values["11"], values["21"])
            points.extend([start, end])
            metadata["curve_endpoints"] = [point_to_dict(start), point_to_dict(end)]
        return points, metadata

    if entity_type == "CIRCLE":
        values = {code: as_float(value) for code, value in pairs if code in {"10", "20", "40"}}
        cx, cy, radius = values.get("10"), values.get("20"), values.get("40")
        if cx is not None and cy is not None and radius is not None:
            metadata["radius"] = radius
            points.extend([Point(cx - radius, cy - radius), Point(cx + radius, cy + radius)])
        return points, metadata

    if entity_type == "ARC":
        values = {code: as_float(value) for code, value in pairs if code in {"10", "20", "40", "50", "51"}}
        cx, cy, radius = values.get("10"), values.get("20"), values.get("40")
        start, end = values.get("50"), values.get("51")
        if cx is not None and cy is not None and radius is not None and start is not None and end is not None:
            metadata.update({"radius": radius, "start_angle": start, "end_angle": end})
            metadata["curve_endpoints"] = [point_to_dict(point_on_circle(cx, cy, radius, start)), point_to_dict(point_on_circle(cx, cy, radius, end))]
            points.extend(sample_arc_points(cx, cy, radius, start, end))
        return points, metadata

    if entity_type == "LWPOLYLINE":
        pending_x: float | None = None
        flag = next((int(as_float(value) or 0) for code, value in pairs if code == "70"), 0)
        for code, value in pairs:
            if code == "10":
                pending_x = as_float(value)
            elif code == "20" and pending_x is not None:
                y = as_float(value)
                if y is not None:
                    points.append(Point(pending_x, y))
                pending_x = None
        metadata["vertex_count"] = len(points)
        metadata["closed"] = bool(flag & 1)
        return points, metadata

    return points, metadata


def sample_arc_points(cx: float, cy: float, radius: float, start: float, end: float) -> list[Point]:
    if end < start:
        end += 360
    samples = [start, end]
    for quadrant in (0, 90, 180, 270, 360):
        angle = quadrant
        if angle < start:
            angle += 360
        if start <= angle <= end:
            samples.append(angle)
    if end - start > 45:
        steps = max(2, math.ceil((end - start) / 15))
        samples.extend(start + (end - start) * index / steps for index in range(steps + 1))
    points = []
    for angle in sorted(set(round(item, 6) for item in samples)):
        rad = math.radians(angle)
        points.append(Point(cx + radius * math.cos(rad), cy + radius * math.sin(rad)))
    return points


def collect_entities(pairs: list[tuple[str, str]]) -> list[dict[str, Any]]:
    entities: list[dict[str, Any]] = []
    index = 0
    in_entities = False

    while index < len(pairs):
        code, value = pairs[index]
        if code == "0" and value == "SECTION" and index + 1 < len(pairs) and pairs[index + 1] == ("2", "ENTITIES"):
            in_entities = True
            index += 2
            continue
        if in_entities and code == "0" and value == "ENDSEC":
            break
        if not in_entities or code != "0":
            index += 1
            continue

        entity_type = value
        index += 1
        current: list[tuple[str, str]] = []
        if entity_type == "POLYLINE":
            layer = "0"
            header_pairs: list[tuple[str, str]] = []
            vertices: list[Point] = []
            flag = 0
            while index < len(pairs):
                next_code, next_value = pairs[index]
                if next_code == "8":
                    layer = next_value or layer
                if next_code == "70":
                    flag = int(as_float(next_value) or 0)
                if next_code == "0" and next_value == "VERTEX":
                    vertex_pairs: list[tuple[str, str]] = []
                    index += 1
                    while index < len(pairs) and pairs[index][0] != "0":
                        vertex_pairs.append(pairs[index])
                        index += 1
                    vx = next((as_float(item_value) for item_code, item_value in vertex_pairs if item_code == "10"), None)
                    vy = next((as_float(item_value) for item_code, item_value in vertex_pairs if item_code == "20"), None)
                    if vx is not None and vy is not None:
                        vertices.append(Point(vx, vy))
                    continue
                if next_code == "0" and next_value == "SEQEND":
                    index += 1
                    break
                if next_code == "0":
                    break
                header_pairs.append(pairs[index])
                index += 1
            space_data = entity_space(header_pairs)
            entities.append(
                {
                    "type": "POLYLINE",
                    "layer": layer,
                    "space": space_data["space"],
                    "layout": space_data["layout"],
                    "points": [point_to_dict(point) for point in vertices],
                    "metadata": {"vertex_count": len(vertices), "closed": bool(flag & 1)},
                }
            )
            continue

        while index < len(pairs) and pairs[index][0] != "0":
            current.append(pairs[index])
            index += 1
        points, metadata = points_from_entity(entity_type, current)
        space_data = entity_space(current)
        entities.append(
            {
                "type": entity_type,
                "layer": entity_layer(current),
                "space": space_data["space"],
                "layout": space_data["layout"],
                "points": [point_to_dict(point) for point in points],
                "metadata": metadata,
            }
        )

    return entities


def role_from_name(name: str) -> str:
    lowered = name.lower()
    if "门板" in name or "door" in lowered:
        return "door_panel"
    if "层板" in name or "shelf" in lowered:
        return "shelf"
    if "横隔" in name or "隔板" in name or "divider" in lowered:
        return "divider"
    if "锁" in name or "lock" in lowered:
        return "lock_related"
    if "铰链" in name or "hinge" in lowered:
        return "hinge_related"
    if "加强筋" in name or "stiffen" in lowered:
        return "stiffener"
    return "sheetmetal_part"


def thickness_from_name(name: str) -> float | None:
    match = THICKNESS_RE.search(name)
    if not match:
        return None
    return float(match.group(1))


def bbox(points: list[dict[str, float]]) -> dict[str, float | None]:
    if not points:
        return {"min_x": None, "min_y": None, "max_x": None, "max_y": None, "width": None, "height": None}
    xs = [point["x"] for point in points]
    ys = [point["y"] for point in points]
    min_x, max_x = min(xs), max(xs)
    min_y, max_y = min(ys), max(ys)
    return {
        "min_x": round(min_x, 4),
        "min_y": round(min_y, 4),
        "max_x": round(max_x, 4),
        "max_y": round(max_y, 4),
        "width": round(max_x - min_x, 4),
        "height": round(max_y - min_y, 4),
    }


def bbox_area(value: dict[str, float | None]) -> float:
    return float(value.get("width") or 0) * float(value.get("height") or 0)


def manufacturing_bbox_from_loops(flat_bbox: dict[str, float | None], loops: dict[str, Any]) -> tuple[dict[str, float | None], str]:
    closed_bbox = loops.get("largest_closed_loop_bbox_mm") or {}
    if closed_bbox.get("width") and closed_bbox.get("height"):
        raw_area = bbox_area(flat_bbox)
        closed_area = bbox_area(closed_bbox)
        closed_min_dimension = min(float(closed_bbox["width"] or 0), float(closed_bbox["height"] or 0))
        raw_min_dimension = min(float(flat_bbox.get("width") or 0), float(flat_bbox.get("height") or 0))
        if (
            raw_area > 0
            and closed_area >= raw_area * MIN_OUTLINE_AREA_RATIO
            and (closed_min_dimension >= MIN_OUTLINE_DIMENSION_MM or raw_min_dimension < MIN_OUTLINE_DIMENSION_MM)
        ):
            return closed_bbox, "largest_closed_loop"
        return flat_bbox, "raw_curve_bbox_closed_loop_too_small"
    return flat_bbox, "raw_curve_bbox"


def distance(a: dict[str, float], b: dict[str, float]) -> float:
    return math.hypot(a["x"] - b["x"], a["y"] - b["y"])


def segment_length(segment: tuple[dict[str, float], dict[str, float], str]) -> float:
    return distance(segment[0], segment[1])


def entity_curve_segments(entity: dict[str, Any]) -> list[tuple[dict[str, float], dict[str, float], str]]:
    entity_type = entity["type"]
    points = entity["points"]
    metadata = entity["metadata"]
    if entity_type in {"LINE", "ARC"} and metadata.get("curve_endpoints"):
        start, end = metadata["curve_endpoints"]
        return [(start, end, entity_type)]
    if entity_type in {"LWPOLYLINE", "POLYLINE"} and len(points) >= 2:
        segments = [(points[index], points[index + 1], entity_type) for index in range(len(points) - 1)]
        if metadata.get("closed") or distance(points[0], points[-1]) <= LOOP_TOLERANCE_MM:
            segments.append((points[-1], points[0], entity_type))
        return segments
    return []


def node_key(point: dict[str, float], tolerance: float = LOOP_TOLERANCE_MM) -> tuple[int, int]:
    return (round(point["x"] / tolerance), round(point["y"] / tolerance))


def loop_analysis(entities: list[dict[str, Any]]) -> dict[str, Any]:
    segments = [
        segment
        for entity in entities
        if entity["type"] != "VIEWPORT"
        for segment in entity_curve_segments(entity)
        if segment_length(segment) > LOOP_TOLERANCE_MM
    ]
    if not segments:
        return {
            "curve_segment_count": 0,
            "connected_component_count": 0,
            "closed_loop_count": 0,
            "open_endpoint_count": 0,
            "largest_component_bbox_mm": bbox([]),
            "largest_closed_loop_bbox_mm": bbox([]),
        }

    parent: dict[tuple[int, int], tuple[int, int]] = {}

    def find(key: tuple[int, int]) -> tuple[int, int]:
        parent.setdefault(key, key)
        if parent[key] != key:
            parent[key] = find(parent[key])
        return parent[key]

    def union(left: tuple[int, int], right: tuple[int, int]) -> None:
        root_left = find(left)
        root_right = find(right)
        if root_left != root_right:
            parent[root_right] = root_left

    edge_records: list[dict[str, Any]] = []
    node_points: dict[tuple[int, int], dict[str, float]] = {}
    for start, end, source_type in segments:
        start_key = node_key(start)
        end_key = node_key(end)
        node_points.setdefault(start_key, start)
        node_points.setdefault(end_key, end)
        union(start_key, end_key)
        edge_records.append({"start": start_key, "end": end_key, "source_type": source_type, "points": [start, end]})

    components: dict[tuple[int, int], dict[str, Any]] = {}
    for edge in edge_records:
        root = find(edge["start"])
        component = components.setdefault(root, {"edges": [], "nodes": set(), "source_types": Counter()})
        component["edges"].append(edge)
        component["nodes"].update([edge["start"], edge["end"]])
        component["source_types"][edge["source_type"]] += 1

    component_summaries = []
    for component in components.values():
        degree: Counter[tuple[int, int]] = Counter()
        component_points: list[dict[str, float]] = []
        for edge in component["edges"]:
            degree[edge["start"]] += 1
            degree[edge["end"]] += 1
            component_points.extend(edge["points"])
        is_closed = len(component["edges"]) >= 3 and all(value == 2 for value in degree.values())
        component_bbox = bbox(component_points)
        width = component_bbox["width"] or 0
        height = component_bbox["height"] or 0
        component_summaries.append(
            {
                "edge_count": len(component["edges"]),
                "node_count": len(component["nodes"]),
                "closed": is_closed,
                "open_endpoint_count": sum(1 for value in degree.values() if value == 1),
                "bbox_mm": component_bbox,
                "bbox_area_mm2": round(width * height, 4),
                "source_types": dict(component["source_types"]),
            }
        )

    component_summaries.sort(key=lambda item: item["bbox_area_mm2"], reverse=True)
    closed_components = [item for item in component_summaries if item["closed"]]
    return {
        "curve_segment_count": len(segments),
        "connected_component_count": len(component_summaries),
        "closed_loop_count": len(closed_components),
        "open_endpoint_count": sum(item["open_endpoint_count"] for item in component_summaries),
        "largest_component_bbox_mm": component_summaries[0]["bbox_mm"] if component_summaries else bbox([]),
        "largest_closed_loop_bbox_mm": closed_components[0]["bbox_mm"] if closed_components else bbox([]),
        "components": component_summaries[:12],
    }


def quality_status_for(
    entity_counts: Counter[str],
    flat_bbox: dict[str, float | None],
    manufacturing_bbox: dict[str, float | None],
    loops: dict[str, Any],
    path: Path,
) -> str:
    if not manufacturing_bbox.get("width") or not manufacturing_bbox.get("height"):
        return "blocked_no_bbox"
    source = manufacturing_bbox_from_loops(flat_bbox, loops)[1]
    if entity_counts.get("VIEWPORT", 0) and loops["closed_loop_count"] == 0:
        return "needs_layout_filter"
    if loops["closed_loop_count"] == 0 or source == "raw_curve_bbox_closed_loop_too_small":
        return "needs_closed_loop_rebuild"
    if thickness_from_name(path.name) is None:
        return "geometry_rule_seed_only"
    return "rule_seed_candidate"


def summarize_dxf(path: Path) -> dict[str, Any]:
    pairs = dxf_pairs(path)
    entities = collect_entities(pairs)
    all_points = [point for entity in entities for point in entity["points"]]
    entity_counts = Counter(entity["type"] for entity in entities)
    layer_counts = Counter(entity["layer"] for entity in entities)
    space_counts = Counter(entity["space"] for entity in entities)
    layout_counts = Counter(entity["layout"] for entity in entities)
    circle_radii = [
        round(float(entity["metadata"]["radius"]), 4)
        for entity in entities
        if entity["type"] == "CIRCLE" and "radius" in entity["metadata"]
    ]
    radius_counts = Counter(circle_radii)
    flat_bbox = bbox(all_points)
    loops = loop_analysis(entities)
    manufacturing_bbox, manufacturing_bbox_source = manufacturing_bbox_from_loops(flat_bbox, loops)
    quality_warnings = quality_warnings_for(entity_counts, flat_bbox, manufacturing_bbox, manufacturing_bbox_source, path, space_counts, loops)
    quality_status = quality_status_for(entity_counts, flat_bbox, manufacturing_bbox, loops, path)
    missing_inputs = [
        "material grade",
        "sheet thickness" if thickness_from_name(path.name) is None else None,
        "bend radius",
        "K-factor or bend deduction table",
        "datum and tolerance rules",
        "formal drawing title block/version",
    ]

    return {
        "source_path": str(path),
        "file_name": path.name,
        "role_guess": role_from_name(path.name),
        "thickness_mm_from_name": thickness_from_name(path.name),
        "flat_bbox_mm": flat_bbox,
        "manufacturing_bbox_mm": manufacturing_bbox,
        "manufacturing_bbox_source": manufacturing_bbox_source,
        "entity_counts": dict(sorted(entity_counts.items())),
        "layer_counts": dict(layer_counts.most_common()),
        "space_counts": dict(space_counts.most_common()),
        "layout_counts": dict(layout_counts.most_common()),
        "circle_count": entity_counts.get("CIRCLE", 0),
        "arc_count": entity_counts.get("ARC", 0),
        "line_count": entity_counts.get("LINE", 0),
        "polyline_count": entity_counts.get("LWPOLYLINE", 0) + entity_counts.get("POLYLINE", 0),
        "circle_radius_counts": {str(radius): count for radius, count in sorted(radius_counts.items())},
        "point_count_for_bbox": len(all_points),
        "loop_analysis": loops,
        "quality_status": quality_status,
        "quality_warnings": quality_warnings,
        "missing_inputs_before_production_release": [item for item in missing_inputs if item],
        "output_level": "engineering_reference",
    }


def quality_warnings_for(
    entity_counts: Counter[str],
    flat_bbox: dict[str, float | None],
    manufacturing_bbox: dict[str, float | None],
    manufacturing_bbox_source: str,
    path: Path,
    space_counts: Counter[str],
    loops: dict[str, Any],
) -> list[str]:
    warnings: list[str] = []
    if entity_counts.get("VIEWPORT", 0):
        warnings.append("DXF contains VIEWPORT; bbox may describe drawing/layout space instead of true flat manufacturing extents.")
    if space_counts.get("paper", 0):
        warnings.append("DXF has paper-space entities; filter model-space geometry before deriving manufacturing rules.")
    if manufacturing_bbox_source == "largest_closed_loop" and bbox_area(flat_bbox) > bbox_area(manufacturing_bbox) * 1.25:
        warnings.append("Raw bbox is much larger than the largest closed loop; using closed-loop bbox as manufacturing-rule bbox.")
    if manufacturing_bbox_source == "raw_curve_bbox_closed_loop_too_small":
        warnings.append("Detected closed loops are too small to be treated as sheet-metal outline; using raw bbox and requiring outline rebuild.")
    if entity_counts.get("LWPOLYLINE", 0) + entity_counts.get("POLYLINE", 0) == 0 and entity_counts.get("LINE", 0) > 20:
        warnings.append("Outline appears fragmented into LINE/ARC entities; closed-loop reconstruction is required before automatic unfold output.")
    if loops["closed_loop_count"] == 0:
        warnings.append("No closed curve loop was detected; geometry can be used for reference statistics but not for automatic unfold output yet.")
    if thickness_from_name(path.name) is None:
        warnings.append("Sheet thickness was not found in the file name; confirm material/thickness before bend deduction.")
    if not flat_bbox.get("width") or not flat_bbox.get("height"):
        warnings.append("No reliable 2D bbox was extracted.")
    return warnings


def write_outputs(summary: dict[str, Any], out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "sheetmetal_reference_summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    with (out_dir / "sheetmetal_reference_summary.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow(["field", "value"])
        writer.writerow(["source_path", summary["source_path"]])
        writer.writerow(["file_name", summary["file_name"]])
        writer.writerow(["role_guess", summary["role_guess"]])
        writer.writerow(["thickness_mm_from_name", summary["thickness_mm_from_name"]])
        for key, value in summary["flat_bbox_mm"].items():
            writer.writerow([f"flat_bbox_mm.{key}", value])
        for key, value in summary["manufacturing_bbox_mm"].items():
            writer.writerow([f"manufacturing_bbox_mm.{key}", value])
        writer.writerow(["manufacturing_bbox_source", summary["manufacturing_bbox_source"]])
        writer.writerow(["entity_counts", json.dumps(summary["entity_counts"], ensure_ascii=False)])
        writer.writerow(["layer_counts", json.dumps(summary["layer_counts"], ensure_ascii=False)])
        writer.writerow(["space_counts", json.dumps(summary["space_counts"], ensure_ascii=False)])
        writer.writerow(["layout_counts", json.dumps(summary["layout_counts"], ensure_ascii=False)])
        writer.writerow(["circle_radius_counts", json.dumps(summary["circle_radius_counts"], ensure_ascii=False)])
        writer.writerow(["quality_status", summary["quality_status"]])
        writer.writerow(["closed_loop_count", summary["loop_analysis"]["closed_loop_count"]])
        writer.writerow(["open_endpoint_count", summary["loop_analysis"]["open_endpoint_count"]])
        writer.writerow(["quality_warnings", "; ".join(summary["quality_warnings"])])
        writer.writerow(
            [
                "missing_inputs_before_production_release",
                "; ".join(summary["missing_inputs_before_production_release"]),
            ]
        )
    (out_dir / "sheetmetal_reference_card.md").write_text(markdown_card(summary), encoding="utf-8")


def markdown_card(summary: dict[str, Any]) -> str:
    bbox_data = summary["flat_bbox_mm"]
    manufacturing_bbox = summary["manufacturing_bbox_mm"]
    loops = summary["loop_analysis"]
    warnings = "\n".join(f"- {item}" for item in summary["quality_warnings"]) or "- 暂无"
    missing = "\n".join(f"- {item}" for item in summary["missing_inputs_before_production_release"])
    return "\n".join(
        [
            "# DXF 钣金单件解析卡",
            "",
            "> 输出级别：工程参考。不能作为正式生产展开图。",
            "",
            f"- 来源文件：`{summary['source_path']}`",
            f"- 角色猜测：`{summary['role_guess']}`",
            f"- 文件名板厚线索：`{summary['thickness_mm_from_name']}` mm",
            f"- 原始 bbox：X `{bbox_data['width']}` mm × Y `{bbox_data['height']}` mm",
            f"- 规则 bbox：X `{manufacturing_bbox['width']}` mm × Y `{manufacturing_bbox['height']}` mm",
            f"- 规则 bbox 来源：`{summary['manufacturing_bbox_source']}`",
            f"- 质量状态：`{summary['quality_status']}`",
            f"- 空间统计：`{json.dumps(summary['space_counts'], ensure_ascii=False)}`",
            f"- 实体数量：`{json.dumps(summary['entity_counts'], ensure_ascii=False)}`",
            f"- 圆孔/圆实体数量：`{summary['circle_count']}`",
            f"- 圆弧数量：`{summary['arc_count']}`",
            f"- 闭合轮廓：`{loops['closed_loop_count']}`，开放端点：`{loops['open_endpoint_count']}`",
            "",
            "## 质量警告",
            "",
            warnings,
            "",
            "## 生产释放前缺项",
            "",
            missing,
            "",
            "## 下一步",
            "",
            "把 bbox、孔径、折弯线和角色猜测与 SolidWorks/BOM/工程图进行交叉验证，通过后再写入生成器规则。",
            "",
        ]
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract a lightweight sheet-metal reference card from a DXF file.")
    parser.add_argument("dxf", type=Path, help="DXF source file.")
    parser.add_argument("--out-dir", type=Path, required=True, help="Output directory for JSON/CSV/Markdown evidence.")
    args = parser.parse_args()

    if not args.dxf.exists():
        raise SystemExit(f"DXF not found: {args.dxf}")
    summary = summarize_dxf(args.dxf)
    write_outputs(summary, args.out_dir)
    print(json.dumps({"status": "ok", "out_dir": str(args.out_dir), "summary": summary}, ensure_ascii=False))


if __name__ == "__main__":
    main()
