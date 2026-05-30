from __future__ import annotations

import json
import math
import sys
from datetime import datetime
from pathlib import Path


def now_iso() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def as_float(value: object, fallback: float) -> float:
    try:
        parsed = float(value)
    except (TypeError, ValueError):
        return fallback
    return parsed if math.isfinite(parsed) and parsed > 0 else fallback


def add_box(doc, name: str, x: float, y: float, z: float, dx: float, dy: float, dz: float, color: tuple[float, float, float]) -> object:
    import FreeCAD as App
    import Part

    shape = Part.makeBox(dx, dy, dz)
    obj = doc.addObject("Part::Feature", name)
    obj.Shape = shape
    obj.Placement = App.Placement(App.Vector(x, y, z), App.Rotation(0, 0, 0))
    try:
        obj.ViewObject.ShapeColor = color
    except Exception:
        pass
    return obj


def add_cylinder_x(doc, name: str, x: float, y: float, z: float, radius: float, length: float, color: tuple[float, float, float]) -> object:
    import FreeCAD as App
    import Part

    shape = Part.makeCylinder(radius, length, App.Vector(x, y, z), App.Vector(1, 0, 0))
    obj = doc.addObject("Part::Feature", name)
    obj.Shape = shape
    try:
        obj.ViewObject.ShapeColor = color
    except Exception:
        pass
    return obj


def build_model(payload_path: Path, output_dir: Path) -> dict:
    import FreeCAD as App
    import Import

    payload = read_json(payload_path)
    boundary = payload.get("modelBoundary", {})
    door_module = payload.get("doorModule", {})

    door_width = as_float(boundary.get("doorWidthMm"), 300.0)
    door_height = as_float(boundary.get("doorHeightMm"), 1917.0)
    thickness = 1.2
    flange_depth = max(10.0, min(24.0, door_width * 0.16))
    flange_width = min(12.0, max(5.0, door_width * 0.07))
    rib_width = min(22.0, max(8.0, door_width * 0.18))
    rib_depth = flange_depth * 0.72
    margin = max(8.0, min(28.0, door_width * 0.12))

    output_dir.mkdir(parents=True, exist_ok=True)
    doc_name = f"review_{payload.get('sourceRequestId', 'task')}".replace("-", "_")[:40]
    doc = App.newDocument(doc_name)

    panel_color = (0.72, 0.70, 0.66)
    flange_color = (0.35, 0.34, 0.32)
    rib_color = (0.18, 0.18, 0.18)
    hardware_color = (0.05, 0.36, 0.75)
    lock_color = (0.90, 0.32, 0.12)

    add_box(doc, "door_outer_panel_sheet", 0, 0, 0, door_width, thickness, door_height, panel_color)
    add_box(doc, "left_folded_side_return", 0, -flange_depth, 0, flange_width, flange_depth, door_height, flange_color)
    add_box(doc, "right_folded_side_return", door_width - flange_width, -flange_depth, 0, flange_width, flange_depth, door_height, flange_color)
    add_box(doc, "top_folded_return", 0, -flange_depth, door_height - flange_width, door_width, flange_depth, flange_width, flange_color)
    add_box(doc, "bottom_folded_return", 0, -flange_depth, 0, door_width, flange_depth, flange_width, flange_color)

    if door_width < 130:
        add_box(
            doc,
            "inside_single_vertical_reinforcement_rib",
            (door_width - rib_width) / 2,
            -flange_depth - rib_depth,
            margin,
            rib_width,
            rib_depth,
            max(30.0, door_height - margin * 2),
            rib_color,
        )
    else:
        left_rib_x = door_width * 0.34 - rib_width / 2
        right_rib_x = door_width * 0.66 - rib_width / 2
        for name, x in (("inside_left_vertical_reinforcement_rib", left_rib_x), ("inside_right_vertical_reinforcement_rib", right_rib_x)):
            add_box(doc, name, x, -flange_depth - rib_depth, margin, rib_width, rib_depth, max(30.0, door_height - margin * 2), rib_color)

    hinge_radius = min(5.5, max(2.8, door_width * 0.035))
    hinge_x = flange_width / 2
    add_cylinder_x(doc, "top_hinge_axis_reference", hinge_x - 10, -flange_depth - hinge_radius * 2, door_height - margin, hinge_radius, 20, hardware_color)
    add_cylinder_x(doc, "bottom_hinge_axis_reference", hinge_x - 10, -flange_depth - hinge_radius * 2, margin, hinge_radius, 20, hardware_color)

    lock_plate_w = min(36.0, max(16.0, door_width * 0.28))
    lock_plate_h = min(58.0, max(24.0, door_height * 0.16))
    add_box(
        doc,
        "inside_lock_latch_reference_plate",
        door_width - flange_width - lock_plate_w,
        -flange_depth - 4.0,
        (door_height - lock_plate_h) / 2,
        lock_plate_w,
        4.0,
        lock_plate_h,
        lock_color,
    )

    label = doc.addObject("App::FeaturePython", "generation_note")
    label.addProperty("App::PropertyString", "Boundary", "Review").Boundary = payload.get("boundary", "")
    label.addProperty("App::PropertyString", "SourceRequestId", "Review").SourceRequestId = str(payload.get("sourceRequestId", ""))
    label.addProperty("App::PropertyString", "DoorSpec", "Review").DoorSpec = f"{door_width:g}W x {door_height:g}H"
    label.addProperty("App::PropertyString", "Caution", "Review").Caution = "Internal FreeCAD reference only; SolidWorks 2020 and engineer signoff required."

    doc.recompute()
    fcstd_path = output_dir / f"{payload.get('sourceRequestId', 'review_task')}_simple_door.FCStd"
    step_path = output_dir / f"{payload.get('sourceRequestId', 'review_task')}_simple_door.step"
    doc.saveAs(str(fcstd_path))
    Import.export([obj for obj in doc.Objects if hasattr(obj, "Shape")], str(step_path))

    report = {
        "generated_at": now_iso(),
        "source_payload": str(payload_path),
        "output_dir": str(output_dir),
        "fcstd": str(fcstd_path),
        "step": str(step_path),
        "door_width_mm": door_width,
        "door_height_mm": door_height,
        "object_count": len(doc.Objects),
        "door_module": door_module,
        "boundary": "Internal FreeCAD reference model only; not a production CAD release.",
    }
    report_path = output_dir / "simple_freecad_generation_report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    App.closeDocument(doc.Name)
    return report


def main() -> int:
    if len(sys.argv) < 3:
        print("Usage: generate_review_task_simple_freecad_model.py <cad_worker_payload.json> <output-dir>", file=sys.stderr)
        return 2
    payload_path = Path(sys.argv[1]).resolve()
    output_dir = Path(sys.argv[2]).resolve()
    report = build_model(payload_path, output_dir)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
