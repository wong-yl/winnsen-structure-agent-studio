from __future__ import annotations

import argparse
import csv
import json
import math
import re
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE_ROOT = (
    ROOT
    / "workers"
    / "analysis"
    / "desktop_reference"
    / "16029_\u91d1\u6807\u51c6\u539f\u59cb\u7d20\u6750_U\u76d8_20260526"
)
DEFAULT_JSON = ROOT / "data" / "locker_16029_gold_sheetmetal_evidence.json"
DEFAULT_MD = ROOT / "data" / "locker_16029_gold_sheetmetal_evidence.md"
DEFAULT_CSV = ROOT / "data" / "locker_16029_gold_sheetmetal_evidence.csv"

SCHEMA = "winnsen.locker16029.gold_sheetmetal_evidence.v1"

EVIDENCE_EXTENSIONS = {
    ".dxf",
    ".dwg",
    ".sldprt",
    ".sldasm",
    ".slddrw",
    ".xlsx",
    ".xls",
    ".pdf",
    ".step",
    ".stp",
}

ROLE_PATTERNS: list[tuple[str, list[str]]] = [
    (
        "electrical_or_electric_lock_reference",
        [
            "\u9501\u63a7\u677f",
            "\u7535\u63a7\u9501",
            "\u7535\u8def",
            "\u4e3b\u677f",
            "\u7535\u6e90",
            "LK-4-6",
            "M9",
            "\u626b\u63cf",
            "\u663e\u793a",
            "\u8bfb\u5361",
            "\u5de5\u63a7",
            "\u952e\u76d8",
        ],
    ),
    (
        "door_panel",
        [
            "\u50a8\u7269\u67dc\u95e8\u677f",
            "\u67dc\u95e8\u677f",
            "\u4e2d\u63a7\u95e8\u677f",
            "\u540e\u4e0b\u95e8\u677f",
            "\u5e94\u6025\u7ef4\u62a4\u95e8",
        ],
    ),
    (
        "door_stiffener_or_press_strip",
        [
            "\u67dc\u95e8\u52a0\u5f3a\u7b4b",
            "\u95e8\u6846\u52a0\u5f3a\u7b4b",
            "\u538b\u6761",
            "U\u578b\u9501\u94a9\u57ab\u677f",
            "\u9501\u94a9\u57ab\u677f",
        ],
    ),
    (
        "shelf_or_horizontal_layer",
        [
            "\u5c42\u677f",
            "\u6a2a\u5c42\u677f",
            "\u7f6e\u7269\u677f",
        ],
    ),
    ("vertical_partition", ["\u7ad6\u9694\u677f"]),
    ("horizontal_partition", ["\u6a2a\u9694\u677f", "\u9694\u677f"]),
    ("side_panel", ["\u4fa7\u677f"]),
    ("base_or_bottom", ["\u5e95\u5ea7", "\u5e95\u677f", "\u5916\u6846"]),
    ("top_cover", ["\u4e0a\u76d6"]),
    ("front_frame_or_crossbar", ["\u95e8\u6846", "\u7acb\u67f1", "\u6a2a\u6881", "\u8fb9\u6846"]),
    ("lock_or_hinge_interface", ["\u9501", "\u9501\u94a9", "\u63d2\u9500", "\u9500\u9489", "\u95e8\u8f74"]),
    ("rear_or_service_panel", ["\u540e\u95e8", "\u80cc\u677f", "\u540e\u677f", "\u7ef4\u62a4\u95e8"]),
]

BEND_LAYER_RE = re.compile(r"(bend|fold|center|dash|\u6298|\u6298\u5f2f|\u4e2d\u5fc3)", re.IGNORECASE)
THICKNESS_RE = re.compile(r"(?:^|[\[(\s_-])(?:t\s*)?(\d+(?:\.\d+)?)\s*(?:mm)?(?:[\])\s_-]|$)", re.IGNORECASE)
DOOR_RATIO_RE = re.compile(r"(\d+)\s*[\/\u2571\uff0f]\s*(\d+)")


@dataclass
class BBox:
    xmin: float = math.inf
    ymin: float = math.inf
    xmax: float = -math.inf
    ymax: float = -math.inf

    def add(self, x: float | None, y: float | None) -> None:
        if x is None or y is None:
            return
        if not math.isfinite(x) or not math.isfinite(y):
            return
        self.xmin = min(self.xmin, x)
        self.ymin = min(self.ymin, y)
        self.xmax = max(self.xmax, x)
        self.ymax = max(self.ymax, y)

    def merge(self, other: "BBox") -> None:
        if other.valid:
            self.add(other.xmin, other.ymin)
            self.add(other.xmax, other.ymax)

    @property
    def valid(self) -> bool:
        return self.xmin <= self.xmax and self.ymin <= self.ymax

    @property
    def width(self) -> float:
        return self.xmax - self.xmin if self.valid else 0.0

    @property
    def height(self) -> float:
        return self.ymax - self.ymin if self.valid else 0.0

    @property
    def area(self) -> float:
        return max(0.0, self.width) * max(0.0, self.height)

    def to_json(self) -> dict[str, float] | None:
        if not self.valid:
            return None
        return {
            "xmin_mm": round(self.xmin, 3),
            "ymin_mm": round(self.ymin, 3),
            "xmax_mm": round(self.xmax, 3),
            "ymax_mm": round(self.ymax, 3),
            "width_mm": round(self.width, 3),
            "height_mm": round(self.height, 3),
            "area_mm2": round(self.area, 3),
        }


def now_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def max_mtime_iso(paths: list[Path]) -> str:
    if not paths:
        return now_iso()
    newest = max(path.stat().st_mtime for path in paths)
    return datetime.fromtimestamp(newest).astimezone().replace(microsecond=0).isoformat()


def rel(path: Path) -> str:
    return str(path.relative_to(ROOT)).replace("\\", "/")


def read_text(path: Path) -> tuple[str, str]:
    data = path.read_bytes()
    for encoding in ("utf-8-sig", "gb18030", "cp936", "latin-1"):
        try:
            return data.decode(encoding), encoding
        except UnicodeDecodeError:
            continue
    return data.decode("latin-1", errors="replace"), "latin-1-replace"


def parse_pairs(path: Path) -> tuple[list[tuple[str, str]], str]:
    text, encoding = read_text(path)
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    if len(lines) % 2:
        lines = lines[:-1]
    pairs: list[tuple[str, str]] = []
    for index in range(0, len(lines), 2):
        code = lines[index].strip()
        value = lines[index + 1].strip()
        if code:
            pairs.append((code, value))
    return pairs, encoding


def first_float(groups: list[tuple[str, str]], code: str) -> float | None:
    for group_code, value in groups:
        if group_code == code:
            try:
                return float(value)
            except ValueError:
                return None
    return None


def all_floats(groups: list[tuple[str, str]], code: str) -> list[float]:
    values: list[float] = []
    for group_code, value in groups:
        if group_code != code:
            continue
        try:
            values.append(float(value))
        except ValueError:
            pass
    return values


def first_value(groups: list[tuple[str, str]], code: str, fallback: str = "") -> str:
    for group_code, value in groups:
        if group_code == code:
            return value
    return fallback


def angle_in_sweep(angle: float, start: float, end: float) -> bool:
    angle = angle % 360.0
    start = start % 360.0
    end = end % 360.0
    if start <= end:
        return start <= angle <= end
    return angle >= start or angle <= end


def arc_bbox(cx: float, cy: float, radius: float, start: float, end: float) -> BBox:
    box = BBox()
    for angle in [start, end, 0.0, 90.0, 180.0, 270.0]:
        if angle in (start, end) or angle_in_sweep(angle, start, end):
            rad = math.radians(angle)
            box.add(cx + radius * math.cos(rad), cy + radius * math.sin(rad))
    return box


def poly_area(points: list[tuple[float, float]]) -> float:
    if len(points) < 3:
        return 0.0
    total = 0.0
    for index, (x1, y1) in enumerate(points):
        x2, y2 = points[(index + 1) % len(points)]
        total += x1 * y2 - x2 * y1
    return abs(total) / 2.0


def collect_entities(pairs: list[tuple[str, str]]) -> list[dict[str, Any]]:
    entities: list[dict[str, Any]] = []
    in_entities = False
    current: dict[str, Any] | None = None
    pending_section = False

    for code, value in pairs:
        if code == "0" and value == "SECTION":
            pending_section = True
            continue
        if pending_section and code == "2":
            in_entities = value.upper() == "ENTITIES"
            pending_section = False
            continue
        if not in_entities:
            continue
        if code == "0":
            if value == "ENDSEC":
                if current:
                    entities.append(current)
                current = None
                in_entities = False
                continue
            if current:
                entities.append(current)
            current = {"type": value.upper(), "groups": []}
            continue
        if current is not None:
            current["groups"].append((code, value))

    if current:
        entities.append(current)
    return entities


def entity_bbox(entity_type: str, groups: list[tuple[str, str]]) -> tuple[BBox, dict[str, Any]]:
    box = BBox()
    details: dict[str, Any] = {}
    if entity_type == "LINE":
        x1, y1 = first_float(groups, "10"), first_float(groups, "20")
        x2, y2 = first_float(groups, "11"), first_float(groups, "21")
        box.add(x1, y1)
        box.add(x2, y2)
        if None not in (x1, y1, x2, y2):
            details["length_mm"] = math.hypot(float(x2) - float(x1), float(y2) - float(y1))
    elif entity_type == "CIRCLE":
        cx, cy, radius = first_float(groups, "10"), first_float(groups, "20"), first_float(groups, "40")
        if None not in (cx, cy, radius):
            r = float(radius)
            box.add(float(cx) - r, float(cy) - r)
            box.add(float(cx) + r, float(cy) + r)
            details.update({"cx_mm": float(cx), "cy_mm": float(cy), "radius_mm": r})
    elif entity_type == "ARC":
        cx, cy, radius = first_float(groups, "10"), first_float(groups, "20"), first_float(groups, "40")
        start, end = first_float(groups, "50"), first_float(groups, "51")
        if None not in (cx, cy, radius, start, end):
            box = arc_bbox(float(cx), float(cy), float(radius), float(start), float(end))
            details.update(
                {
                    "cx_mm": float(cx),
                    "cy_mm": float(cy),
                    "radius_mm": float(radius),
                    "start_deg": float(start),
                    "end_deg": float(end),
                }
            )
    elif entity_type in {"LWPOLYLINE", "POLYLINE", "VERTEX"}:
        xs, ys = all_floats(groups, "10"), all_floats(groups, "20")
        points = list(zip(xs, ys))
        for x, y in points:
            box.add(x, y)
        flag = int(first_float(groups, "70") or 0)
        is_closed = entity_type == "LWPOLYLINE" and bool(flag & 1)
        details.update({"point_count": len(points), "closed": is_closed})
        if is_closed:
            details["area_mm2"] = poly_area(points)
    elif entity_type in {"POINT", "TEXT", "MTEXT", "INSERT"}:
        box.add(first_float(groups, "10"), first_float(groups, "20"))
    return box, details


def parse_dxf(path: Path) -> dict[str, Any]:
    try:
        pairs, encoding = parse_pairs(path)
        entities = collect_entities(pairs)
    except Exception as exc:  # pragma: no cover - evidence collection should be tolerant.
        return {"status": "failed", "error": str(exc)}

    all_box = BBox()
    model_box = BBox()
    by_type: Counter[str] = Counter()
    by_layer: Counter[str] = Counter()
    model_space_count = 0
    paper_space_count = 0
    line_lengths: list[float] = []
    horizontal_lines = 0
    vertical_lines = 0
    circles: list[dict[str, Any]] = []
    arcs: list[dict[str, Any]] = []
    closed_polylines: list[dict[str, Any]] = []
    bend_entity_count = 0

    for entity in entities:
        entity_type = str(entity["type"])
        groups = entity["groups"]
        layer = first_value(groups, "8", "0")
        linetype = first_value(groups, "6", "")
        space = int(first_float(groups, "67") or 0)
        box, details = entity_bbox(entity_type, groups)
        by_type[entity_type] += 1
        by_layer[layer] += 1
        all_box.merge(box)
        if space == 0:
            model_space_count += 1
            model_box.merge(box)
        else:
            paper_space_count += 1
        if BEND_LAYER_RE.search(layer) or BEND_LAYER_RE.search(linetype):
            bend_entity_count += 1
        if entity_type == "LINE" and "length_mm" in details:
            length = float(details["length_mm"])
            line_lengths.append(length)
            xs = all_floats(groups, "10") + all_floats(groups, "11")
            ys = all_floats(groups, "20") + all_floats(groups, "21")
            if len(xs) >= 2 and abs(xs[0] - xs[1]) < 0.001:
                vertical_lines += 1
            if len(ys) >= 2 and abs(ys[0] - ys[1]) < 0.001:
                horizontal_lines += 1
        if entity_type == "CIRCLE" and {"cx_mm", "cy_mm", "radius_mm"} <= details.keys():
            radius = float(details["radius_mm"])
            circles.append(
                {
                    "layer": layer,
                    "cx_mm": round(float(details["cx_mm"]), 3),
                    "cy_mm": round(float(details["cy_mm"]), 3),
                    "radius_mm": round(radius, 3),
                    "diameter_mm": round(radius * 2.0, 3),
                    "is_model_space": space == 0,
                }
            )
        if entity_type == "ARC" and "radius_mm" in details:
            arcs.append(
                {
                    "layer": layer,
                    "radius_mm": round(float(details["radius_mm"]), 3),
                    "is_model_space": space == 0,
                }
            )
        if entity_type == "LWPOLYLINE" and details.get("closed") and box.valid:
            closed_polylines.append(
                {
                    "layer": layer,
                    "point_count": int(details.get("point_count") or 0),
                    "bbox": box.to_json(),
                    "area_mm2": round(float(details.get("area_mm2") or 0.0), 3),
                    "is_model_space": space == 0,
                }
            )

    manufacturing_source = "model_space_bbox"
    manufacturing_box = model_box if model_box.valid else all_box
    closed_model = [
        row
        for row in closed_polylines
        if row.get("is_model_space") and row.get("bbox") and float(row.get("area_mm2") or 0) > 1.0
    ]
    if closed_model:
        largest = max(closed_model, key=lambda item: float(item.get("area_mm2") or 0.0))
        bbox = largest.get("bbox") or {}
        manufacturing_box = BBox(
            xmin=float(bbox["xmin_mm"]),
            ymin=float(bbox["ymin_mm"]),
            xmax=float(bbox["xmax_mm"]),
            ymax=float(bbox["ymax_mm"]),
        )
        manufacturing_source = "largest_closed_lwpolyline"
    elif not model_box.valid:
        manufacturing_source = "all_entity_bbox"

    hole_candidates = [
        row
        for row in circles
        if row.get("is_model_space") and 0.8 <= float(row.get("diameter_mm") or 0.0) <= 80.0
    ]
    if manufacturing_box.valid:
        for row in hole_candidates:
            cx = float(row["cx_mm"])
            cy = float(row["cy_mm"])
            row["distance_to_left_mm"] = round(cx - manufacturing_box.xmin, 3)
            row["distance_to_right_mm"] = round(manufacturing_box.xmax - cx, 3)
            row["distance_to_bottom_mm"] = round(cy - manufacturing_box.ymin, 3)
            row["distance_to_top_mm"] = round(manufacturing_box.ymax - cy, 3)

    quality_flags: list[str] = []
    if not manufacturing_box.valid:
        quality_flags.append("no_usable_bbox")
    if paper_space_count > 0:
        quality_flags.append("contains_paper_space_entities")
    if not closed_model:
        quality_flags.append("needs_closed_loop_rebuild")
    if manufacturing_box.valid and (manufacturing_box.width > 5000 or manufacturing_box.height > 5000):
        quality_flags.append("layout_or_titleblock_noise_possible")
    if all_box.valid and manufacturing_box.valid and manufacturing_box.area > 0 and all_box.area / manufacturing_box.area > 4.0:
        quality_flags.append("all_entity_bbox_much_larger_than_manufacturing_bbox")

    line_summary = {
        "count": len(line_lengths),
        "horizontal_count": horizontal_lines,
        "vertical_count": vertical_lines,
        "longest_mm": round(max(line_lengths), 3) if line_lengths else 0.0,
        "total_length_mm": round(sum(line_lengths), 3),
    }

    diameter_counts = Counter(round(float(row["diameter_mm"]), 3) for row in hole_candidates)
    return {
        "status": "parsed",
        "encoding": encoding,
        "pair_count": len(pairs),
        "entity_count": len(entities),
        "model_space_entity_count": model_space_count,
        "paper_space_entity_count": paper_space_count,
        "entity_type_counts": dict(sorted(by_type.items())),
        "layer_counts": dict(sorted(by_layer.items())),
        "bbox_all_mm": all_box.to_json(),
        "manufacturing_bbox_source": manufacturing_source,
        "manufacturing_bbox_mm": manufacturing_box.to_json(),
        "line_summary": line_summary,
        "circle_count": len(circles),
        "arc_count": len(arcs),
        "closed_polyline_count": len(closed_polylines),
        "bend_layer_entity_count": bend_entity_count,
        "hole_candidate_count": len(hole_candidates),
        "hole_diameter_counts": {str(key): value for key, value in sorted(diameter_counts.items())},
        "hole_candidates": sorted(hole_candidates, key=lambda item: (float(item["diameter_mm"]), float(item["cx_mm"]), float(item["cy_mm"])))[:250],
        "arc_radii_mm": sorted({round(float(row["radius_mm"]), 3) for row in arcs})[:120],
        "closed_polylines": sorted(closed_polylines, key=lambda item: float(item.get("area_mm2") or 0.0), reverse=True)[:60],
        "quality_flags": quality_flags,
        "quality_status": "usable_rule_evidence" if "no_usable_bbox" not in quality_flags else "parse_limited",
    }


def classify_roles(text: str) -> list[str]:
    roles: list[str] = []
    for role, patterns in ROLE_PATTERNS:
        if any(pattern.lower() in text.lower() for pattern in patterns):
            roles.append(role)
    return roles or ["unclassified"]


def extract_thickness(text: str) -> float | None:
    candidates: list[float] = []
    for match in THICKNESS_RE.finditer(text):
        try:
            value = float(match.group(1))
        except ValueError:
            continue
        if 0.2 <= value <= 6.0:
            candidates.append(value)
    return candidates[-1] if candidates else None


def extract_door_ratio(text: str) -> str | None:
    match = DOOR_RATIO_RE.search(text)
    if not match:
        return None
    return f"{int(match.group(1))}/{int(match.group(2))}"


def normalize_stem(path: Path) -> str:
    text = path.stem.lower()
    for token in [
        "\u5c55\u5f00\u56fe",
        "\u5c55\u5f00",
        "\u710a\u63a5",
        "\u88c5\u914d",
        "\u6a21\u578b",
        "\u5de5\u7a0b\u56fe",
    ]:
        text = text.replace(token, "")
    text = re.sub(r"[\s_\-()\[\]\uff08\uff09]+", "", text)
    text = re.sub(r"\d+(?:\.\d+)?mm?", "", text)
    return text


def build_related_index(files: list[Path]) -> dict[str, list[Path]]:
    index: dict[str, list[Path]] = defaultdict(list)
    for path in files:
        if path.suffix.lower() == ".dxf":
            continue
        index[normalize_stem(path)].append(path)
    return index


def find_related(path: Path, related_index: dict[str, list[Path]]) -> dict[str, list[str]]:
    stem = normalize_stem(path)
    scored: list[tuple[int, Path]] = []
    for key, files in related_index.items():
        score = 0
        if key == stem:
            score = 100
        elif key and stem and (key in stem or stem in key):
            score = 70
        if score:
            scored.extend((score, item) for item in files)
    by_ext: dict[str, list[tuple[int, Path]]] = defaultdict(list)
    for score, item in scored:
        by_ext[item.suffix.lower().lstrip(".")].append((score, item))
    result: dict[str, list[str]] = {}
    for ext, rows in by_ext.items():
        rows.sort(key=lambda item: (-item[0], len(str(item[1]))))
        result[ext] = [rel(item) for _, item in rows[:8]]
    return result


def source_bucket(path: Path, source_root: Path) -> str:
    parts = [part.lower() for part in path.relative_to(source_root).parts]
    text = "/".join(parts)
    if "10.\u5907\u4efd" in text:
        return "backup_reference"
    if "2.\u94a3\u91d1\u5c55\u5f00\u56fe" in text:
        return "current_flat_pattern"
    if "1.\u5de5\u7a0b\u56fe" in text:
        return "engineering_drawing_folder"
    if "3.bom" in text:
        return "bom_folder"
    return "other_reference"


def collect(source_root: Path) -> dict[str, Any]:
    source_root = source_root.resolve()
    files = [path for path in source_root.rglob("*") if path.is_file() and not path.name.startswith("~$")]
    evidence_files = [path for path in files if path.suffix.lower() in EVIDENCE_EXTENSIONS]
    related_index = build_related_index(evidence_files)
    inventory_counts = Counter(path.suffix.lower() or "<none>" for path in files)

    dxf_records: list[dict[str, Any]] = []
    role_counts: Counter[str] = Counter()
    source_bucket_counts: Counter[str] = Counter()
    door_ratio_counts: Counter[str] = Counter()
    thickness_counts: Counter[str] = Counter()
    total_holes = 0
    parsed = 0
    failed = 0

    for path in sorted([item for item in evidence_files if item.suffix.lower() == ".dxf"], key=lambda p: rel(p)):
        text = f"{path.name} {path.parent.name}"
        roles = classify_roles(text)
        bucket = source_bucket(path, source_root)
        parsed_dxf = parse_dxf(path)
        if parsed_dxf.get("status") == "parsed":
            parsed += 1
            total_holes += int(parsed_dxf.get("hole_candidate_count") or 0)
        else:
            failed += 1
        ratio = extract_door_ratio(path.name)
        thickness = extract_thickness(path.name)
        if ratio:
            door_ratio_counts[ratio] += 1
        if thickness is not None:
            thickness_counts[str(thickness)] += 1
        role_counts.update(roles)
        source_bucket_counts[bucket] += 1
        dxf_records.append(
            {
                "relative_path": rel(path),
                "file_name": path.name,
                "source_bucket": bucket,
                "roles": roles,
                "primary_role": roles[0],
                "door_ratio": ratio,
                "thickness_mm_from_name": thickness,
                "size_bytes": path.stat().st_size,
                "modified_at": datetime.fromtimestamp(path.stat().st_mtime).astimezone().replace(microsecond=0).isoformat(),
                "related_source_files": find_related(path, related_index),
                "dxf": parsed_dxf,
            }
        )

    bom_files = [
        {
            "relative_path": rel(path),
            "file_name": path.name,
            "size_bytes": path.stat().st_size,
            "source_bucket": source_bucket(path, source_root),
        }
        for path in sorted(evidence_files, key=lambda p: rel(p))
        if path.suffix.lower() in {".xlsx", ".xls"}
    ]

    required_role_names = [
        "door_panel",
        "door_stiffener_or_press_strip",
        "shelf_or_horizontal_layer",
        "vertical_partition",
        "side_panel",
        "base_or_bottom",
        "top_cover",
        "lock_or_hinge_interface",
    ]
    required_roles = {
        role: {
            "count": int(role_counts.get(role, 0)),
            "covered": int(role_counts.get(role, 0)) > 0,
        }
        for role in required_role_names
    }
    door_class_coverage = {
        f"{index}/12": {
            "count": int(door_ratio_counts.get(f"{index}/12", 0)),
            "covered": int(door_ratio_counts.get(f"{index}/12", 0)) > 0,
        }
        for index in range(1, 7)
    }

    return {
        "schema": SCHEMA,
        "generated_at": max_mtime_iso(files),
        "generated_at_basis": "max_source_file_mtime",
        "source_root": str(source_root),
        "gold_source_reference": "16029 1000W x 1917H x 550D source-reference folder",
        "boundary": {
            "cad_mainline": "SolidWorks 2020",
            "freecad_usage": "internal parameter evidence only",
            "generated_model_rule": "exclude electrical parts and cabinet-side electric locks; keep hole/interface evidence",
        },
        "summary": {
            "total_file_count": len(files),
            "evidence_file_count": len(evidence_files),
            "dxf_count": len(dxf_records),
            "parsed_dxf_count": parsed,
            "failed_dxf_count": failed,
            "bom_file_count": len(bom_files),
            "hole_candidate_count": total_holes,
            "role_counts": dict(sorted(role_counts.items())),
            "source_bucket_counts": dict(sorted(source_bucket_counts.items())),
            "door_ratio_counts": dict(sorted(door_ratio_counts.items())),
            "thickness_counts": dict(sorted(thickness_counts.items(), key=lambda item: float(item[0]))),
            "required_roles": required_roles,
            "door_class_coverage_1_to_6": door_class_coverage,
            "inventory_extension_counts": dict(sorted(inventory_counts.items())),
        },
        "bom_files": bom_files,
        "dxf_files": dxf_records,
    }


def write_csv(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "relative_path",
        "source_bucket",
        "primary_role",
        "roles",
        "door_ratio",
        "thickness_mm_from_name",
        "quality_status",
        "manufacturing_bbox_source",
        "width_mm",
        "height_mm",
        "entity_count",
        "line_count",
        "circle_count",
        "arc_count",
        "hole_candidate_count",
        "closed_polyline_count",
        "bend_layer_entity_count",
        "quality_flags",
        "related_sldprt_count",
        "related_slddrw_count",
        "related_sldasm_count",
    ]
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for row in report["dxf_files"]:
            dxf = row.get("dxf", {})
            bbox = dxf.get("manufacturing_bbox_mm") or {}
            related = row.get("related_source_files") or {}
            writer.writerow(
                {
                    "relative_path": row.get("relative_path"),
                    "source_bucket": row.get("source_bucket"),
                    "primary_role": row.get("primary_role"),
                    "roles": ";".join(row.get("roles") or []),
                    "door_ratio": row.get("door_ratio") or "",
                    "thickness_mm_from_name": row.get("thickness_mm_from_name") or "",
                    "quality_status": dxf.get("quality_status") or dxf.get("status"),
                    "manufacturing_bbox_source": dxf.get("manufacturing_bbox_source") or "",
                    "width_mm": bbox.get("width_mm", ""),
                    "height_mm": bbox.get("height_mm", ""),
                    "entity_count": dxf.get("entity_count", ""),
                    "line_count": (dxf.get("line_summary") or {}).get("count", ""),
                    "circle_count": dxf.get("circle_count", ""),
                    "arc_count": dxf.get("arc_count", ""),
                    "hole_candidate_count": dxf.get("hole_candidate_count", ""),
                    "closed_polyline_count": dxf.get("closed_polyline_count", ""),
                    "bend_layer_entity_count": dxf.get("bend_layer_entity_count", ""),
                    "quality_flags": ";".join(dxf.get("quality_flags") or []),
                    "related_sldprt_count": len(related.get("sldprt") or []),
                    "related_slddrw_count": len(related.get("slddrw") or []),
                    "related_sldasm_count": len(related.get("sldasm") or []),
                }
            )


def write_markdown(path: Path, report: dict[str, Any]) -> None:
    summary = report["summary"]
    lines = [
        "# 16029 1000W Gold Sheet-Metal Evidence",
        "",
        f"- Generated at: `{report['generated_at']}`",
        f"- Source root: `{report['source_root']}`",
        f"- DXF files: `{summary['dxf_count']}`, parsed: `{summary['parsed_dxf_count']}`, failed: `{summary['failed_dxf_count']}`",
        f"- Hole candidates from DXF circles: `{summary['hole_candidate_count']}`",
        f"- BOM files indexed: `{summary['bom_file_count']}`",
        "",
        "## Boundary",
        "",
        "- SolidWorks 2020 remains the engineer-facing CAD mainline.",
        "- FreeCAD is not used as engineer approval evidence.",
        "- Generated models must exclude electrical boards and cabinet-side electric locks, while preserving hole/interface evidence.",
        "",
        "## Source Buckets",
        "",
        "| Bucket | Count |",
        "|---|---:|",
    ]
    for key, value in summary["source_bucket_counts"].items():
        lines.append(f"| {key} | {value} |")
    lines.extend(["", "## Role Coverage", "", "| Role | Count | Covered |", "|---|---:|---|"])
    for role, item in summary["required_roles"].items():
        lines.append(f"| {role} | {item['count']} | {'yes' if item['covered'] else 'no'} |")
    lines.extend(["", "## Door Class Coverage", "", "| Class | Count | Covered |", "|---|---:|---|"])
    for role, item in summary["door_class_coverage_1_to_6"].items():
        lines.append(f"| {role} | {item['count']} | {'yes' if item['covered'] else 'no'} |")
    lines.extend(["", "## Thickness Evidence From File Names", "", "| Thickness mm | Count |", "|---:|---:|"])
    for key, value in summary["thickness_counts"].items():
        lines.append(f"| {key} | {value} |")
    lines.extend(["", "## Largest DXF BBoxes", "", "| File | Role | Bucket | W | H | Holes | Flags |", "|---|---|---|---:|---:|---:|---|"])
    rows = sorted(
        report["dxf_files"],
        key=lambda row: float(((row.get("dxf") or {}).get("manufacturing_bbox_mm") or {}).get("area_mm2") or 0.0),
        reverse=True,
    )[:30]
    for row in rows:
        dxf = row.get("dxf") or {}
        bbox = dxf.get("manufacturing_bbox_mm") or {}
        flags = ",".join(dxf.get("quality_flags") or [])
        lines.append(
            "| "
            + " | ".join(
                [
                    f"`{row['relative_path']}`",
                    row.get("primary_role") or "",
                    row.get("source_bucket") or "",
                    str(bbox.get("width_mm", "")),
                    str(bbox.get("height_mm", "")),
                    str(dxf.get("hole_candidate_count", "")),
                    flags,
                ]
            )
            + " |"
        )
    lines.extend(
        [
            "",
            "## Next Use",
            "",
            "- Use `manufacturing_bbox_mm`, `hole_candidates`, `closed_polylines`, and thickness evidence to drive door/shelf/partition rules.",
            "- Treat rows with `needs_closed_loop_rebuild` or `layout_or_titleblock_noise_possible` as review evidence, not direct generator inputs.",
            "- Add these evidence checks to model gates before marking a generated variant engineer-ready.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Collect 16029 1000W gold sheet-metal DXF evidence.")
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--out-json", default=str(DEFAULT_JSON))
    parser.add_argument("--out-md", default=str(DEFAULT_MD))
    parser.add_argument("--out-csv", default=str(DEFAULT_CSV))
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()

    source_root = Path(args.source_root)
    if not source_root.exists():
        raise SystemExit(f"source root does not exist: {source_root}")

    report = collect(source_root)
    out_json = Path(args.out_json)
    out_json.parent.mkdir(parents=True, exist_ok=True)
    out_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_csv(Path(args.out_csv), report)
    write_markdown(Path(args.out_md), report)

    if not args.quiet:
        summary = report["summary"]
        print(f"wrote {out_json}")
        print(f"DXF parsed: {summary['parsed_dxf_count']}/{summary['dxf_count']}")
        print(f"hole candidates: {summary['hole_candidate_count']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
