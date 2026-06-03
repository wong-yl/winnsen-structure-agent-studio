from __future__ import annotations

import json
import os
from pathlib import Path

import FreeCAD as App
import Import
import Part


SCHEMA = "winnsen.locker16029.parametric_scaffold.v1"


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


def shelf_boxes(door_width: float, shelf_t: float, shelf_depth: float):
    tab_w = 20.0
    tab_h = 24.0
    tab_d = 18.0
    front_z = shelf_depth / 2.0 - tab_d / 2.0
    return [
        (door_width, shelf_t, shelf_depth, 0, 0, 0),
        (tab_w, tab_h, tab_d, -door_width / 2.0 + 34.0, -shelf_t / 2.0 - tab_h / 2.0, front_z),
        (tab_w, tab_h, tab_d, door_width / 2.0 - 34.0, -shelf_t / 2.0 - tab_h / 2.0, front_z),
    ]


def partition_boxes(panel_t: float, panel_h: float, shelf_depth: float):
    stiffener_w = 18.0
    stiffener_h = max(120.0, panel_h - 160.0)
    stiffener_d = 16.0
    return [
        (panel_t, panel_h, shelf_depth, 0, 0, 0),
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


def export_part(defn: dict, step_dir: Path, native_dir: Path, import_dir: Path) -> dict:
    key = defn["key"]
    step_path = step_dir / f"{key}.step"
    native_path = native_dir / f"{key}.SLDPRT"
    roundtrip_path = step_dir / f"{key}_roundtrip.step"
    import_json = import_dir / f"{key}_import.json"

    doc = App.newDocument(key[:60])
    obj = doc.addObject("Part::Feature", key)
    obj.Label = defn.get("role") or key
    obj.Shape = shape_from_boxes(defn["boxes"])
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
        "source": "freecad_internal_parameter_scaffold",
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

    side_t = 2.0
    panel_t = 2.0
    frame_t = 28.0
    frame_z = 36.0
    shelf_t = 18.0
    base_h = 58.0
    top_h = 46.0
    foot_w = 38.0
    foot_h = 34.0
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
    role_leveling_foot = zh(0x8C03, 0x8282, 0x811A)
    role_partition_stiffener_left_front = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x5DE6, 0x524D)
    role_partition_stiffener_left_rear = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x5DE6, 0x540E)
    role_partition_stiffener_right_front = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x53F3, 0x524D)
    role_partition_stiffener_right_rear = zh(0x5185, 0x4FA7, 0x7AD6, 0x9694, 0x677F, 0x52A0, 0x5F3A, 0x677F, 0x53F3, 0x540E)
    role_shelf_locator = zh(0x5C42, 0x677F, 0x5B9A, 0x4F4D, 0x811A)
    role_front_frame_notch = zh(0x95E8, 0x6846, 0x5B9A, 0x4F4D, 0x7F3A, 0x53E3, 0x57FA, 0x51C6)

    parts = [
        {
            "key": "cabinet_left_side_weldment",
            "boxes": [(side_t, height, depth, 0, 0, 0)],
            "defaultPlacement": {"txMm": left, "tyMm": height / 2.0, "tzMm": -depth / 2.0},
        },
        {
            "key": "cabinet_right_side_weldment",
            "boxes": [(side_t, height, depth, 0, 0, 0)],
            "defaultPlacement": {"txMm": right, "tyMm": height / 2.0, "tzMm": -depth / 2.0},
        },
        {
            "key": "cabinet_left_partition_weldment",
            "boxes": partition_boxes(panel_t, partition_h, shelf_depth),
            "defaultPlacement": {"txMm": center_left, "tyMm": height / 2.0, "tzMm": -depth / 2.0},
        },
        {
            "key": "cabinet_right_partition_weldment",
            "boxes": partition_boxes(panel_t, partition_h, shelf_depth),
            "defaultPlacement": {"txMm": center_right, "tyMm": height / 2.0, "tzMm": -depth / 2.0},
        },
        {
            "key": "front_frame_weldment",
            "boxes": [
                (frame_t, height, frame_z, -width / 2.0 + frame_t / 2.0, 0, 0),
                (frame_t, height, frame_z, width / 2.0 - frame_t / 2.0, 0, 0),
                (width, frame_t, frame_z, 0, -height / 2.0 + frame_t / 2.0, 0),
                (width, frame_t, frame_z, 0, height / 2.0 - frame_t / 2.0, 0),
                (frame_t, height - 2.0 * frame_t, frame_z, 0, 0, 0),
            ],
            "defaultPlacement": {"txMm": 0, "tyMm": height / 2.0, "tzMm": -18.0},
        },
        {
            "key": "base_weldment",
            "boxes": [
                (width, base_h, 42.0, 0, 0, -depth / 2.0 + 21.0),
                (width, base_h, 42.0, 0, 0, depth / 2.0 - 21.0),
                (42.0, base_h, depth, -width / 2.0 + 21.0, 0, 0),
                (42.0, base_h, depth, width / 2.0 - 21.0, 0, 0),
            ],
            "defaultPlacement": {"txMm": 0, "tyMm": base_h / 2.0, "tzMm": -depth / 2.0},
        },
        {
            "key": "top_cover_weldment",
            "boxes": [(width, top_h, depth, 0, 0, 0)],
            "defaultPlacement": {"txMm": 0, "tyMm": height - top_h / 2.0, "tzMm": -depth / 2.0},
        },
        {
            "key": "cabinet_left_shelf_weldment",
            "boxes": shelf_boxes(door_width, shelf_t, shelf_depth),
            "defaultPlacement": {},
        },
        {
            "key": "cabinet_right_shelf_weldment",
            "boxes": shelf_boxes(door_width, shelf_t, shelf_depth),
            "defaultPlacement": {},
        },
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
            "key": "leveling_foot",
            "role": role_leveling_foot,
            "boxes": [(foot_w, foot_h, foot_w, 0, 0, 0)],
            "defaultPlacement": {},
        },
        {
            "key": "partition_stiffener_left_front",
            "role": role_partition_stiffener_left_front,
            "boxes": stiffener_channel_boxes(stiffener_h),
            "defaultPlacement": {"txMm": center_left, "tyMm": height / 2.0, "tzMm": -72.0},
        },
        {
            "key": "partition_stiffener_left_rear",
            "role": role_partition_stiffener_left_rear,
            "boxes": stiffener_channel_boxes(stiffener_h),
            "defaultPlacement": {"txMm": center_left, "tyMm": height / 2.0, "tzMm": -depth + 72.0},
        },
        {
            "key": "partition_stiffener_right_front",
            "role": role_partition_stiffener_right_front,
            "boxes": stiffener_channel_boxes(stiffener_h),
            "defaultPlacement": {"txMm": center_right, "tyMm": height / 2.0, "tzMm": -72.0},
        },
        {
            "key": "partition_stiffener_right_rear",
            "role": role_partition_stiffener_right_rear,
            "boxes": stiffener_channel_boxes(stiffener_h),
            "defaultPlacement": {"txMm": center_right, "tyMm": height / 2.0, "tzMm": -depth + 72.0},
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
        "freeCadRole": "internal parameter evidence only",
        "plan": str(plan_path),
        "requested": {
            "cabinetWidthMm": width,
            "cabinetHeightMm": height,
            "cabinetDepthMm": depth,
            "doorWidthMm": door_width,
        },
        "internalSheetMetalRepair": {
            "doorRouteFrozen": True,
            "shelfLocatingFeet": "exported as short two-tab sheet-metal locating ledges at row boundaries",
            "frontFrameLocatingNotches": "exported as short front-frame notch/ledge interface parts at row boundaries",
            "innerPartitionStiffeners": "left/right front/rear long channel stiffener plates exported as separate internal sheet-metal datums",
            "lockHoleDatums": "row-specific placement is handled by the SolidWorks 2020 assembly worker",
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
