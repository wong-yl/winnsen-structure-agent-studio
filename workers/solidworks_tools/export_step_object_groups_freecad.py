from __future__ import annotations

import json
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import FreeCAD as App
import Part


def valid_shape_object(obj):
    shape = getattr(obj, "Shape", None)
    if shape is None:
        return False
    try:
        return not shape.isNull() and shape.isValid()
    except Exception:
        return False


def production_object(obj):
    label = str(getattr(obj, "Label", "") or "")
    name = str(getattr(obj, "Name", "") or "")
    return not (
        label.startswith("REF_")
        or name.startswith("REF_")
        or label.startswith("hinge_axis_reference")
        or name.startswith("hinge_axis_reference")
    )


def object_group(obj):
    label = str(getattr(obj, "Label", "") or "")
    if label.startswith("standard_top_export_from_sw"):
        return "01_shell_seed_from_solidworks_step"
    if label.startswith("SW_cabinet_vertical") or label.startswith("SW_door_frame_vertical"):
        return "02_verticals_and_door_frame"
    if label.startswith("SW_API_base") or label.startswith("SW_API_maintenance") or label.startswith("SW_API_latch") or label.startswith("SW_API_lock"):
        return "03_base_maintenance_lock_electrical"
    if label.startswith("SW_exact_sheetmetal_door_panel") or label.startswith("SW_rebuilt_sheetmetal_rib"):
        return "04_door_panels_and_ribs"
    if label.startswith("SW_door_hardware"):
        return "05_door_hardware"
    if label.startswith("shelf_") or label.startswith("SW_shelf"):
        return "06_shelves"
    if "shelf" in label.lower():
        return "06_shelves"
    return "99_other_production"


def bbox_dict(objects):
    shapes = [obj.Shape for obj in objects]
    if not shapes:
        return None
    compound = Part.makeCompound(shapes)
    bb = compound.BoundBox
    return {
        "min_x": round(bb.XMin, 4),
        "min_y": round(bb.YMin, 4),
        "min_z": round(bb.ZMin, 4),
        "max_x": round(bb.XMax, 4),
        "max_y": round(bb.YMax, 4),
        "max_z": round(bb.ZMax, 4),
        "size_x": round(bb.XLength, 4),
        "size_y": round(bb.YLength, 4),
        "size_z": round(bb.ZLength, 4),
    }


def export_groups(source_path: Path, output_dir: Path):
    source_path = source_path.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    doc = App.openDocument(str(source_path))

    groups = defaultdict(list)
    for obj in doc.Objects:
        if valid_shape_object(obj) and production_object(obj):
            groups[object_group(obj)].append(obj)

    previous_scheme = App.ParamGet("User parameter:BaseApp/Preferences/Mod/Part/STEP").GetString("Scheme")
    App.ParamGet("User parameter:BaseApp/Preferences/Mod/Part/STEP").SetString("Scheme", "AP214IS")
    App.ParamGet("User parameter:BaseApp/Preferences/Mod/Part/STEP").SetBool("VisibleExportDialog", False)
    App.ParamGet("User parameter:BaseApp/Preferences/Mod/Import").SetBool("ExportHiddenObject", False)
    App.ParamGet("User parameter:BaseApp/Preferences/Mod/Import").SetBool("ExportKeepPlacement", True)

    report = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "source_path": str(source_path),
        "output_dir": str(output_dir.resolve()),
        "groups": [],
    }

    for group_name in sorted(groups):
        objects = groups[group_name]
        output_path = output_dir / f"{source_path.stem}_{group_name}.stp"
        item = {
            "group": group_name,
            "object_count": len(objects),
            "solid_count": sum(len(obj.Shape.Solids) for obj in objects),
            "face_count": sum(len(obj.Shape.Faces) for obj in objects),
            "bbox_mm": bbox_dict(objects),
            "path": str(output_path),
            "status": "pending",
            "sample_labels": [obj.Label for obj in objects[:8]],
        }
        try:
            Part.export(objects, str(output_path))
            item["status"] = "exported"
            item["size_bytes"] = output_path.stat().st_size
        except Exception as exc:
            item["status"] = "failed"
            item["error"] = repr(exc)
        report["groups"].append(item)

    App.ParamGet("User parameter:BaseApp/Preferences/Mod/Part/STEP").SetString("Scheme", previous_scheme)
    report_path = output_dir / "solidworks_step_object_group_exports.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(str(report_path))
    App.closeDocument(doc.Name)


def main(argv):
    if len(argv) != 3:
        print("Usage: FreeCADCmd.exe export_step_object_groups_freecad.py <source.FCStd> <output-dir>", file=sys.stderr)
        return 2
    export_groups(Path(argv[1]), Path(argv[2]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
