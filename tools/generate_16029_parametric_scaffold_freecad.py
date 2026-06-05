from __future__ import annotations

import json
import os
from pathlib import Path

import FreeCAD as App
import Import
import Part


SCHEMA = "winnsen.locker16029.source_sheetmetal_geometry.v1"
SOURCE_STEP_ROOT = Path(os.environ.get("WINNSEN_16029_SOURCE_STEP_ROOT", r"C:\sw16029_standard_ascii"))
SOURCE_WIDTH_MM = 1000.0
SOURCE_HEIGHT_MM = 1917.0
SOURCE_DEPTH_MM = 550.0


def env_path(name: str) -> Path:
    value = os.environ.get(name, "").strip()
    if not value:
        raise SystemExit(f"missing {name}")
    return Path(value)


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def number(value, fallback: float) -> float:
    try:
        parsed = float(value)
    except (TypeError, ValueError):
        return fallback
    return parsed if parsed > 0 else fallback


def zh(*codes: int) -> str:
    return "".join(chr(code) for code in codes)


def box_shape(dx: float, dy: float, dz: float, cx: float = 0, cy: float = 0, cz: float = 0):
    return Part.makeBox(
        dx,
        dy,
        dz,
        App.Vector(cx - dx / 2.0, cy - dy / 2.0, cz - dz / 2.0),
    )


def shape_from_boxes(boxes: list[tuple[float, float, float, float, float, float]]):
    shapes = [box_shape(*box) for box in boxes]
    if len(shapes) == 1:
        return shapes[0]
    return Part.makeCompound(shapes)


def solid_objects(doc):
    for obj in doc.Objects:
        shape = getattr(obj, "Shape", None)
        if shape and not shape.isNull() and len(shape.Solids) > 0:
            yield obj


def import_source_compound(*step_names: str, bbox_filter=None):
    shapes = []
    for step_name in step_names:
        path = SOURCE_STEP_ROOT / step_name
        if not path.exists():
            raise SystemExit(f"missing source STEP: {path}")
        doc = App.newDocument(("src_" + Path(step_name).stem)[:60])
        Part.insert(str(path), doc.Name)
        doc.recompute()
        for obj in solid_objects(doc):
            shape = obj.Shape.copy()
            if bbox_filter is None or bbox_filter(shape.BoundBox):
                shapes.append(shape)
        App.closeDocument(doc.Name)
    if not shapes:
        raise SystemExit(f"no source solids imported from: {', '.join(step_names)}")
    return shapes[0] if len(shapes) == 1 else Part.makeCompound(shapes)


def import_source_solids(*step_names: str, bbox_filter=None):
    shapes = []
    for step_name in step_names:
        path = SOURCE_STEP_ROOT / step_name
        if not path.exists():
            raise SystemExit(f"missing source STEP: {path}")
        doc = App.newDocument(("src_solids_" + Path(step_name).stem)[:60])
        Part.insert(str(path), doc.Name)
        doc.recompute()
        for obj in solid_objects(doc):
            for solid in obj.Shape.Solids:
                shape = solid.copy()
                if bbox_filter is None or bbox_filter(shape.BoundBox):
                    shapes.append(shape)
        App.closeDocument(doc.Name)
    if not shapes:
        raise SystemExit(f"no source solids imported from: {', '.join(step_names)}")
    return shapes


def scale_source_shape(shape, target_width: float, target_height: float, target_depth: float, scale_x: bool, scale_y: bool, scale_z: bool):
    matrix = App.Matrix()
    matrix.A11 = (target_width / SOURCE_WIDTH_MM) if scale_x else 1.0
    matrix.A22 = (target_height / SOURCE_HEIGHT_MM) if scale_y else 1.0
    matrix.A33 = (target_depth / SOURCE_DEPTH_MM) if scale_z else 1.0
    if abs(matrix.A11 - 1.0) > 0.0001 or abs(matrix.A22 - 1.0) > 0.0001 or abs(matrix.A33 - 1.0) > 0.0001:
        return shape.transformGeometry(matrix)
    return shape


def transformed_source_shape(
    *step_names: str,
    target_width: float,
    target_height: float,
    target_depth: float,
    scale_x: bool = True,
    scale_y: bool = True,
    scale_z: bool = True,
    bbox_filter=None,
):
    shape = import_source_compound(*step_names, bbox_filter=bbox_filter)
    return scale_source_shape(shape, target_width, target_height, target_depth, scale_x, scale_y, scale_z)


def transformed_source_solids(
    *step_names: str,
    target_width: float,
    target_height: float,
    target_depth: float,
    scale_x: bool = True,
    scale_y: bool = True,
    scale_z: bool = True,
    bbox_filter=None,
):
    return [
        scale_source_shape(shape, target_width, target_height, target_depth, scale_x, scale_y, scale_z)
        for shape in import_source_solids(*step_names, bbox_filter=bbox_filter)
    ]


def normalize_shape_to_bbox_center(shape):
    local = shape.copy()
    bbox = local.BoundBox
    center = App.Vector(
        (bbox.XMin + bbox.XMax) / 2.0,
        (bbox.YMin + bbox.YMax) / 2.0,
        (bbox.ZMin + bbox.ZMax) / 2.0,
    )
    matrix = App.Matrix()
    matrix.move(App.Vector(-center.x, -center.y, -center.z))
    local = local.transformGeometry(matrix)
    return local, center


def source_part(
    key: str,
    step_names: list[str],
    width: float,
    height: float,
    depth: float,
    role: str | None = None,
    scale_x: bool = True,
    scale_y: bool = True,
    scale_z: bool = True,
    default_placement: dict | None = None,
    bbox_filter=None,
):
    shape = transformed_source_shape(
        *step_names,
        target_width=width,
        target_height=height,
        target_depth=depth,
        scale_x=scale_x,
        scale_y=scale_y,
        scale_z=scale_z,
        bbox_filter=bbox_filter,
    )
    local_shape, center = normalize_shape_to_bbox_center(shape)
    placement = default_placement if default_placement is not None else {
        "txMm": round(float(center.x), 3),
        "tyMm": round(float(center.y), 3),
        "tzMm": round(float(center.z), 3),
    }
    return {
        "key": key,
        "role": role,
        "shape": local_shape,
        "defaultPlacement": placement,
        "sourceStepNames": step_names,
        "source": "solidworks_1000w_gold_source_step_scaled",
    }


def source_solid_parts(
    key_prefix: str,
    step_name: str,
    width: float,
    height: float,
    depth: float,
    role_prefix: str,
    scale_x: bool = True,
    scale_y: bool = True,
    scale_z: bool = True,
    bbox_filter=None,
):
    parts = []
    shapes = transformed_source_solids(
        step_name,
        target_width=width,
        target_height=height,
        target_depth=depth,
        scale_x=scale_x,
        scale_y=scale_y,
        scale_z=scale_z,
        bbox_filter=bbox_filter,
    )
    for index, shape in enumerate(shapes, start=1):
        local_shape, center = normalize_shape_to_bbox_center(shape)
        token = f"{index:03d}"
        parts.append({
            "key": f"{key_prefix}_body_{token}",
            "role": f"{role_prefix}{token}",
            "shape": local_shape,
            "defaultPlacement": {
                "txMm": round(float(center.x), 3),
                "tyMm": round(float(center.y), 3),
                "tzMm": round(float(center.z), 3),
            },
            "sourceStepNames": [step_name],
            "source": "solidworks_1000w_gold_source_step_solid_scaled",
        })
    return parts


def vertical_partition_stiffener_filter(zone: str):
    def matches(bbox):
        center_z = (bbox.ZMin + bbox.ZMax) / 2.0
        long_vertical_strip = bbox.XLength <= 8.0 and bbox.YLength >= 1500.0 and 35.0 <= bbox.ZLength <= 75.0
        if not long_vertical_strip:
            return False
        return center_z > -250.0 if zone == "front" else center_z <= -250.0
    return matches


def shelf_boxes(door_width: float, shelf_t: float, shelf_depth: float):
    tab_w = 20.0
    tab_h = 24.0
    tab_d = 18.0
    lip_h = 24.0
    lip_t = max(1.2, min(2.0, shelf_t / 6.0))
    side_lip_w = 18.0
    front_z = shelf_depth / 2.0 - tab_d / 2.0
    return [
        (door_width, lip_t, shelf_depth, 0, 0, 0),
        (door_width, lip_h, lip_t, 0, -lip_h / 2.0, shelf_depth / 2.0 - lip_t / 2.0),
        (door_width, lip_h * 0.65, lip_t, 0, -lip_h * 0.325, -shelf_depth / 2.0 + lip_t / 2.0),
        (side_lip_w, lip_h * 0.55, shelf_depth, -door_width / 2.0 + side_lip_w / 2.0, -lip_h * 0.275, 0),
        (side_lip_w, lip_h * 0.55, shelf_depth, door_width / 2.0 - side_lip_w / 2.0, -lip_h * 0.275, 0),
        (tab_w, tab_h, tab_d, -door_width / 2.0 + 34.0, -shelf_t / 2.0 - tab_h / 2.0, front_z),
        (tab_w, tab_h, tab_d, door_width / 2.0 - 34.0, -shelf_t / 2.0 - tab_h / 2.0, front_z),
    ]


def partition_boxes(panel_t: float, panel_h: float, shelf_depth: float):
    stiffener_w = 18.0
    stiffener_h = max(120.0, panel_h - 160.0)
    stiffener_d = 16.0
    front_return = 24.0
    rear_return = 18.0
    return [
        (panel_t, panel_h, shelf_depth, 0, 0, 0),
        (front_return, panel_h, panel_t, 0, 0, shelf_depth / 2.0 - panel_t / 2.0),
        (rear_return, panel_h, panel_t, 0, 0, -shelf_depth / 2.0 + panel_t / 2.0),
        (stiffener_w, stiffener_h, stiffener_d, 0, 0, shelf_depth / 2.0 - 72.0),
        (stiffener_w, stiffener_h, stiffener_d, 0, 0, -shelf_depth / 2.0 + 72.0),
    ]


def stiffener_channel_boxes(stiffener_h: float):
    web_w = 34.0
    flange_w = 8.0
    web_d = 8.0
    flange_d = 26.0
    return [
        (web_w, stiffener_h, web_d, 0, 0, 0),
        (flange_w, stiffener_h, flange_d, -web_w / 2.0 - flange_w / 2.0, 0, 0),
        (flange_w, stiffener_h, flange_d, web_w / 2.0 + flange_w / 2.0, 0, 0),
    ]


def side_panel_sheetmetal_boxes(width: float, height: float, depth: float, side: str, panel_t: float):
    front_return = 28.0
    rear_flange_depth = 30.0
    rear_lap = 12.0
    rear_edge = 0.5
    top_bottom_return = 22.0
    boxes = [
        (panel_t, height, depth, 0, 0, 0),
    ]
    if side == "L":
        rear_dx = width / 2.0 + rear_lap
        rear_cx = (-panel_t / 2.0 + (width / 2.0 + rear_lap - panel_t / 2.0)) / 2.0
        front_cx = front_return / 2.0 - panel_t / 2.0
    else:
        rear_dx = width / 2.0 - rear_edge
        rear_min = -width / 2.0 + rear_edge + panel_t / 2.0
        rear_cx = (rear_min + panel_t / 2.0) / 2.0
        front_cx = -front_return / 2.0 + panel_t / 2.0
    boxes.extend([
        (front_return, height, panel_t, front_cx, 0, depth / 2.0 - panel_t / 2.0),
        (rear_dx, height, rear_flange_depth, rear_cx, 0, -depth / 2.0 + rear_flange_depth / 2.0),
        (top_bottom_return, panel_t, depth, front_cx, height / 2.0 - panel_t / 2.0, 0),
        (top_bottom_return, panel_t, depth, front_cx, -height / 2.0 + panel_t / 2.0, 0),
    ])
    return boxes


def tray_weldment_boxes(width: float, height: float, depth: float, sheet_t: float, open_up: bool):
    y_sign = -1.0 if open_up else 1.0
    web_cy = y_sign * (height / 2.0 - sheet_t / 2.0)
    lip_cy = -web_cy / 2.0
    lip_h = height
    lip_d = 32.0
    side_lip_w = 32.0
    return [
        (width, sheet_t, depth, 0, web_cy, 0),
        (width, lip_h, lip_d, 0, lip_cy, depth / 2.0 - lip_d / 2.0),
        (width, lip_h, lip_d, 0, lip_cy, -depth / 2.0 + lip_d / 2.0),
        (side_lip_w, lip_h, depth, -width / 2.0 + side_lip_w / 2.0, lip_cy, 0),
        (side_lip_w, lip_h, depth, width / 2.0 - side_lip_w / 2.0, lip_cy, 0),
    ]


def front_frame_sheetmetal_boxes(width: float, height: float, frame_t: float, frame_z: float, sheet_t: float):
    rail_lip = 18.0
    center_post_w = frame_t
    side_post_w = frame_t
    return [
        (side_post_w, height, sheet_t, -width / 2.0 + side_post_w / 2.0, 0, 0),
        (side_post_w, height, sheet_t, width / 2.0 - side_post_w / 2.0, 0, 0),
        (center_post_w, height - 2.0 * frame_t, sheet_t, 0, 0, 0),
        (width, frame_t, sheet_t, 0, -height / 2.0 + frame_t / 2.0, 0),
        (width, frame_t, sheet_t, 0, height / 2.0 - frame_t / 2.0, 0),
        (side_post_w, height, frame_z, -width / 2.0 + side_post_w / 2.0, 0, -frame_z / 2.0),
        (side_post_w, height, frame_z, width / 2.0 - side_post_w / 2.0, 0, -frame_z / 2.0),
        (center_post_w, height - 2.0 * frame_t, frame_z, 0, 0, -frame_z / 2.0),
        (width, rail_lip, frame_z, 0, -height / 2.0 + rail_lip / 2.0, -frame_z / 2.0),
        (width, rail_lip, frame_z, 0, height / 2.0 - rail_lip / 2.0, -frame_z / 2.0),
    ]


def rear_center_seam_connector_boxes(sheet_t: float):
    tab_w = 86.0
    tab_h = 18.0
    return [
        (tab_w, tab_h, sheet_t, 0, 0, 0),
        (sheet_t, tab_h + 18.0, 22.0, -tab_w / 2.0 + sheet_t / 2.0, 0, -11.0),
        (sheet_t, tab_h + 18.0, 22.0, tab_w / 2.0 - sheet_t / 2.0, 0, -11.0),
        (26.0, tab_h + 18.0, sheet_t, 0, 0, -22.0),
    ]


def center_lock_maintenance_strip_boxes(strip_h: float, sheet_t: float):
    cover_w = 72.0
    cover_d = 42.0
    cap_h = 22.0
    raised_w = 24.0
    access_h = 96.0
    return [
        (cover_w, strip_h, sheet_t, 0, 0, 0),
        (sheet_t, strip_h, cover_d, -cover_w / 2.0 + sheet_t / 2.0, 0, -cover_d / 2.0),
        (sheet_t, strip_h, cover_d, cover_w / 2.0 - sheet_t / 2.0, 0, -cover_d / 2.0),
        (cover_w, cap_h, cover_d, 0, strip_h / 2.0 - cap_h / 2.0, -cover_d / 2.0),
        (cover_w, cap_h, cover_d, 0, -strip_h / 2.0 + cap_h / 2.0, -cover_d / 2.0),
        (raised_w, strip_h - 120.0, sheet_t * 2.0, 0, 0, sheet_t),
        (cover_w - 14.0, access_h, sheet_t * 2.5, 0, strip_h * 0.28, sheet_t * 1.5),
        (cover_w - 14.0, access_h, sheet_t * 2.5, 0, -strip_h * 0.28, sheet_t * 1.5),
    ]


def top_lock_cover_sheetmetal_boxes(width: float, depth: float, sheet_t: float):
    cover_w = min(max(width - 220.0, 320.0), 520.0)
    cover_d = min(max(depth - 190.0, 240.0), 360.0)
    front_lip_h = 28.0
    rear_lip_h = 18.0
    side_lip_h = 18.0
    lock_x = cover_w / 2.0 - 92.0
    lock_z = cover_d / 2.0 - 52.0
    return [
        (cover_w, sheet_t, cover_d, 0, 0, 0),
        (cover_w, front_lip_h, sheet_t, 0, -front_lip_h / 2.0, cover_d / 2.0 - sheet_t / 2.0),
        (cover_w, rear_lip_h, sheet_t, 0, -rear_lip_h / 2.0, -cover_d / 2.0 + sheet_t / 2.0),
        (sheet_t, side_lip_h, cover_d, -cover_w / 2.0 + sheet_t / 2.0, -side_lip_h / 2.0, 0),
        (sheet_t, side_lip_h, cover_d, cover_w / 2.0 - sheet_t / 2.0, -side_lip_h / 2.0, 0),
        (cover_w - 24.0, sheet_t * 2.0, 6.0, 0, sheet_t * 1.5, cover_d / 2.0 - 14.0),
        (cover_w - 24.0, sheet_t * 2.0, 6.0, 0, sheet_t * 1.5, -cover_d / 2.0 + 14.0),
        (6.0, sheet_t * 2.0, cover_d - 24.0, -cover_w / 2.0 + 14.0, sheet_t * 1.5, 0),
        (6.0, sheet_t * 2.0, cover_d - 24.0, cover_w / 2.0 - 14.0, sheet_t * 1.5, 0),
        (54.0, sheet_t * 4.0, 34.0, lock_x, sheet_t * 3.0, lock_z),
        (24.0, sheet_t * 5.0, 18.0, lock_x, sheet_t * 5.0, lock_z),
    ]


def export_part(defn: dict, step_dir: Path, native_dir: Path, import_dir: Path) -> dict:
    key = defn["key"]
    step_path = step_dir / f"{key}.step"
    native_path = native_dir / f"{key}.SLDPRT"
    roundtrip_path = step_dir / f"{key}_roundtrip.step"
    import_json = import_dir / f"{key}_import.json"

    doc = App.newDocument(key[:60])
    obj = doc.addObject("Part::Feature", key)
    obj.Label = defn.get("role") or key
    obj.Shape = defn["shape"] if "shape" in defn else shape_from_boxes(defn["boxes"])
    doc.recompute()
    Import.export([obj], str(step_path))
    bbox = obj.Shape.BoundBox
    App.closeDocument(doc.Name)

    return {
        "key": key,
        "role": defn.get("role") or key,
        "sourceStepPath": str(step_path),
        "nativePreferredPath": str(native_path),
        "roundtripStepPath": str(roundtrip_path),
        "importResultJson": str(import_json),
        "defaultPlacement": defn.get("defaultPlacement") or {},
        "dimensionsMm": {
            "x": round(float(bbox.XLength), 3),
            "y": round(float(bbox.YLength), 3),
            "z": round(float(bbox.ZLength), 3),
        },
        "source": defn.get("source") or "freecad_interface_datum_marker",
        "sourceStepNames": defn.get("sourceStepNames") or [],
    }


def main() -> int:
    plan_path = env_path("WINNSEN_16029_PARAMETRIC_SCAFFOLD_PLAN")
    out_dir = env_path("WINNSEN_16029_PARAMETRIC_SCAFFOLD_OUT_DIR")
    manifest_path = env_path("WINNSEN_16029_PARAMETRIC_SCAFFOLD_MANIFEST")
    plan = read_json(plan_path)

    requested = plan.get("requested", {})
    derived = plan.get("derived", {})
    width = number(requested.get("cabinetWidthMm"), 740.0)
    height = number(requested.get("cabinetHeightMm"), 1917.0)
    depth = number(requested.get("cabinetDepthMm"), 550.0)
    door_width = number(derived.get("doorWidthMm") or requested.get("doorWidthMm"), (width - 126.0) / 2.0)
    lock_abs_x = number(derived.get("lockBodyAbsXmm"), 55.2)
    lock_z = float(derived.get("lockBodyZmm") or -101.5)

    step_dir = out_dir / "step"
    native_dir = out_dir / "native"
    import_dir = out_dir / "solidworks_import"
    for directory in (step_dir, native_dir, import_dir):
        directory.mkdir(parents=True, exist_ok=True)

    side_t = 1.5
    panel_t = 1.5
    frame_t = 28.0
    frame_z = 36.0
    shelf_t = 18.0
    base_h = 58.0
    top_h = 46.0
    foot_w = 38.0
    foot_h = 34.0
    partition_stiffener_t = 4.8
    partition_stiffener_h = 1805.8 * (height / SOURCE_HEIGHT_MM)
    partition_stiffener_d = 50.0 * (depth / SOURCE_DEPTH_MM)
    partition_stiffener_x = 65.6
    partition_stiffener_y = 933.5 * (height / SOURCE_HEIGHT_MM)
    partition_stiffener_front_z = -148.5 * (depth / SOURCE_DEPTH_MM)
    partition_stiffener_rear_z = -398.5 * (depth / SOURCE_DEPTH_MM)
    lock_strip_w = 26.0
    lock_strip_h = 1821.0 * (height / SOURCE_HEIGHT_MM)
    lock_strip_d = 36.0 * (depth / SOURCE_DEPTH_MM)
    datum_w = 18.0
    datum_d = 18.0
    datum_h = 90.0
    shelf_depth = max(120.0, depth - 54.0)
    partition_h = height - 96.0
    stiffener_h = max(420.0, partition_h - 230.0)

    left = -width / 2.0 + side_t / 2.0
    right = width / 2.0 - side_t / 2.0
    center_left = -frame_t / 2.0
    center_right = frame_t / 2.0

    role_lock_datum_left = zh(0x9501, 0x5B54, 0x57FA, 0x51C6, 0x5DE6)
    role_lock_datum_right = zh(0x9501, 0x5B54, 0x57FA, 0x51C6, 0x53F3)
    role_lock_side_strip_left = zh(0x9501, 0x4FA7, 0x5B89, 0x88C5, 0x57FA, 0x51C6, 0x957F, 0x6761, 0x5DE6)
    role_lock_side_strip_right = zh(0x9501, 0x4FA7, 0x5B89, 0x88C5, 0x57FA, 0x51C6, 0x957F, 0x6761, 0x53F3)
    role_leveling_foot = zh(0x8C03, 0x8282, 0x811A)
    role_partition_stiffener_left_front = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x5DE6, 0x524D)
    role_partition_stiffener_left_rear = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x5DE6, 0x540E)
    role_partition_stiffener_right_front = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x53F3, 0x524D)
    role_partition_stiffener_right_rear = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x53F3, 0x540E)
    role_shelf_locator = zh(0x5C42, 0x677F, 0x5B9A, 0x4F4D, 0x811A)
    role_front_frame_notch = zh(0x95E8, 0x6846, 0x5B9A, 0x4F4D, 0x7F3A, 0x53E3, 0x57FA, 0x51C6)
    role_top_cover = zh(0x4E0A, 0x76D6, 0x710A, 0x63A5) + "_" + zh(0x6E90, 0x94A3, 0x91D1)
    role_source_sheetmetal = zh(0x6E90, 0x94A3, 0x91D1)
    role_front_frame_source = zh(0x95E8, 0x6846, 0x710A, 0x63A5) + "_" + zh(0x6E90, 0x94A3, 0x91D1) + "_body"

    parts = [
        source_part("cabinet_left_side_weldment", ["structural_004_cabinet_body.step"], width, height, depth),
        source_part("cabinet_right_side_weldment", ["structural_003_cabinet_body.step"], width, height, depth),
        source_part("cabinet_left_partition_weldment", ["cabinet_vertical_L_weld.step"], width, height, depth),
        source_part("cabinet_right_partition_weldment", ["cabinet_vertical_R_weld.step"], width, height, depth),
        *source_solid_parts("front_frame_weldment", "door_frame_weld.step", width, height, depth, role_front_frame_source, scale_z=False),
        source_part("base_weldment", ["base_model_from_sw.step"], width, height, depth),
        source_part("top_cover_weldment", ["structural_040_top_cover.step"], width, height, depth, role=role_top_cover + "040"),
        source_part("top_cover_weldment_panel_041", ["structural_041_top_cover.step"], width, height, depth, role=role_top_cover + "041"),
        source_part("top_cover_weldment_panel_042", ["structural_042_top_cover.step"], width, height, depth, role=role_top_cover + "042"),
        source_part("top_cover_weldment_panel_043", ["structural_043_top_cover.step"], width, height, depth, role=role_top_cover + "043"),
        source_part("top_cover_weldment_panel_044", ["structural_044_top_cover.step"], width, height, depth, role=role_top_cover + "044"),
        source_part("cabinet_left_shelf_weldment", ["shelf_L_weld.step"], width, height, depth, default_placement={}),
        source_part("cabinet_right_shelf_weldment", ["shelf_R_weld.step"], width, height, depth, default_placement={}),
        {
            "key": "lock_mounting_hole_datum_left",
            "role": role_lock_datum_left,
            "boxes": [(datum_w, datum_h, datum_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": -lock_abs_x, "tyMm": height / 2.0, "tzMm": lock_z},
        },
        {
            "key": "lock_mounting_hole_datum_right",
            "role": role_lock_datum_right,
            "boxes": [(datum_w, datum_h, datum_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": lock_abs_x, "tyMm": height / 2.0, "tzMm": lock_z},
        },
        {
            "key": "lock_side_mounting_datum_strip_left",
            "role": role_lock_side_strip_left,
            "boxes": [(lock_strip_w, lock_strip_h, lock_strip_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": -lock_abs_x, "tyMm": height / 2.0, "tzMm": lock_z},
            "source": "solidworks_1000w_gold_lock_side_mounting_datum_scaled",
        },
        {
            "key": "lock_side_mounting_datum_strip_right",
            "role": role_lock_side_strip_right,
            "boxes": [(lock_strip_w, lock_strip_h, lock_strip_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": lock_abs_x, "tyMm": height / 2.0, "tzMm": lock_z},
            "source": "solidworks_1000w_gold_lock_side_mounting_datum_scaled",
        },
        {
            "key": "leveling_foot",
            "role": role_leveling_foot,
            "boxes": [(foot_w, foot_h, foot_w, 0, 0, 0)],
            "defaultPlacement": {},
        },
        {
            "key": "partition_stiffener_left_front",
            "role": role_partition_stiffener_left_front,
            "boxes": [(partition_stiffener_t, partition_stiffener_h, partition_stiffener_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": -partition_stiffener_x, "tyMm": partition_stiffener_y, "tzMm": partition_stiffener_front_z},
            "source": "solidworks_1000w_gold_source_stiffener_localized",
            "sourceStepNames": ["cabinet_vertical_L_weld.step"],
        },
        {
            "key": "partition_stiffener_left_rear",
            "role": role_partition_stiffener_left_rear,
            "boxes": [(partition_stiffener_t, partition_stiffener_h, partition_stiffener_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": -partition_stiffener_x, "tyMm": partition_stiffener_y, "tzMm": partition_stiffener_rear_z},
            "source": "solidworks_1000w_gold_source_stiffener_localized",
            "sourceStepNames": ["cabinet_vertical_L_weld.step"],
        },
        {
            "key": "partition_stiffener_right_front",
            "role": role_partition_stiffener_right_front,
            "boxes": [(partition_stiffener_t, partition_stiffener_h, partition_stiffener_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": partition_stiffener_x, "tyMm": partition_stiffener_y, "tzMm": partition_stiffener_front_z},
            "source": "solidworks_1000w_gold_source_stiffener_localized",
            "sourceStepNames": ["cabinet_vertical_R_weld.step"],
        },
        {
            "key": "partition_stiffener_right_rear",
            "role": role_partition_stiffener_right_rear,
            "boxes": [(partition_stiffener_t, partition_stiffener_h, partition_stiffener_d, 0, 0, 0)],
            "defaultPlacement": {"txMm": partition_stiffener_x, "tyMm": partition_stiffener_y, "tzMm": partition_stiffener_rear_z},
            "source": "solidworks_1000w_gold_source_stiffener_localized",
            "sourceStepNames": ["cabinet_vertical_R_weld.step"],
        },
        {
            "key": "shelf_locating_foot_datum",
            "role": role_shelf_locator,
            "boxes": [
                (86.0, 10.0, 18.0, 0, 0, 0),
                (18.0, 26.0, 18.0, -34.0, -8.0, 0),
                (18.0, 26.0, 18.0, 34.0, -8.0, 0),
            ],
            "defaultPlacement": {},
        },
        {
            "key": "front_frame_locating_notch_datum",
            "role": role_front_frame_notch,
            "boxes": [
                (96.0, 12.0, 8.0, 0, 0, 0),
                (16.0, 32.0, 8.0, -40.0, 10.0, 0),
                (16.0, 32.0, 8.0, 40.0, 10.0, 0),
            ],
            "defaultPlacement": {},
        },
    ]

    exported = [export_part(defn, step_dir, native_dir, import_dir) for defn in parts]
    manifest = {
        "schema": SCHEMA,
        "cadMainline": "SolidWorks 2020",
        "freeCadRole": "internal derived sheet-metal geometry; SolidWorks 2020 remains the review mainline",
        "plan": str(plan_path),
        "requested": {
            "cabinetWidthMm": width,
            "cabinetHeightMm": height,
            "cabinetDepthMm": depth,
            "doorWidthMm": door_width,
        },
        "internalSheetMetalRepair": {
            "doorRouteFrozen": True,
            "sidePanels": "left/right side panels are derived as sheet-metal webs with front returns and rear center-lap flanges; rear seam is not a separate overlay box",
            "shelves": "shelves are derived as tray-like sheet-metal parts with front lips, rear lips, side returns, and locating tabs",
            "topBase": "top cover and base are derived as folded tray weldments instead of solid blocks",
            "frontFrame": "front frame is derived as sheet-metal rails/posts with returns instead of one thick frame block",
            "shelfLocatingFeet": "exported as short two-tab sheet-metal locating ledges at row boundaries",
            "frontFrameLocatingNotches": "exported as short front-frame notch/ledge interface parts at row boundaries",
            "innerPartitionStiffeners": "left/right front/rear long channel stiffener plates exported as separate internal sheet-metal datums",
            "lockHoleDatums": "row-specific placement is handled by the SolidWorks 2020 assembly worker",
            "centerLockMaintenanceStrip": "exported as a visible folded center lock-control maintenance sheet-metal strip, not an electrical board or electric-lock body",
            "topLockCoverFeature": "exported as a visible top lockable sheet-metal cover feature, not an electrical or electric-lock component",
            "externalThroughHolePolicy": "external top/side/rear through-hole candidate parts are not generated",
        },
        "parts": exported,
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(str(manifest_path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
