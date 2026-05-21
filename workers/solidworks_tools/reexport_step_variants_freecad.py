from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import FreeCAD as App
import Import
import Part


def json_default(value):
    return str(value)


def collect_shape_objects(doc):
    objects = []
    for obj in doc.Objects:
        shape = getattr(obj, "Shape", None)
        if shape is None:
            continue
        try:
            if shape.isNull() or not shape.isValid():
                continue
        except Exception:
            continue
        if getattr(obj, "TypeId", "") == "App::Link":
            continue
        objects.append(obj)
    return objects


def is_production_object(obj):
    label = str(getattr(obj, "Label", "") or "")
    name = str(getattr(obj, "Name", "") or "")
    markers = (label, name)
    for value in markers:
        if value.startswith("REF_"):
            return False
        if value.startswith("hinge_axis_reference"):
            return False
    return True


def bbox_dict(shape):
    bb = shape.BoundBox
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


def shape_stats(objects):
    solids = 0
    faces = 0
    edges = 0
    invalid = 0
    valid_shapes = []
    for obj in objects:
        shape = obj.Shape
        if shape.isNull() or not shape.isValid():
            invalid += 1
            continue
        solids += len(shape.Solids)
        faces += len(shape.Faces)
        edges += len(shape.Edges)
        valid_shapes.append(shape)
    compound = Part.makeCompound(valid_shapes) if valid_shapes else None
    return {
        "object_count": len(objects),
        "invalid_shape_count": invalid,
        "solid_count": solids,
        "face_count": faces,
        "edge_count": edges,
        "bbox_mm": bbox_dict(compound) if compound else None,
    }


def set_step_preferences(scheme):
    part_step = App.ParamGet("User parameter:BaseApp/Preferences/Mod/Part/STEP")
    import_prefs = App.ParamGet("User parameter:BaseApp/Preferences/Mod/Import")
    previous = {
        "scheme": part_step.GetString("Scheme"),
        "visible_export_dialog": part_step.GetBool("VisibleExportDialog"),
        "unit": part_step.GetInt("Unit"),
        "export_hidden_object": import_prefs.GetBool("ExportHiddenObject"),
        "export_legacy": import_prefs.GetBool("ExportLegacy"),
        "export_keep_placement": import_prefs.GetBool("ExportKeepPlacement"),
    }
    part_step.SetString("Scheme", scheme)
    part_step.SetBool("VisibleExportDialog", False)
    part_step.SetInt("Unit", 0)
    import_prefs.SetBool("ExportHiddenObject", False)
    import_prefs.SetBool("ExportLegacy", False)
    import_prefs.SetBool("ExportKeepPlacement", True)
    return previous


def restore_step_preferences(previous):
    part_step = App.ParamGet("User parameter:BaseApp/Preferences/Mod/Part/STEP")
    import_prefs = App.ParamGet("User parameter:BaseApp/Preferences/Mod/Import")
    part_step.SetString("Scheme", previous["scheme"])
    part_step.SetBool("VisibleExportDialog", previous["visible_export_dialog"])
    part_step.SetInt("Unit", previous["unit"])
    import_prefs.SetBool("ExportHiddenObject", previous["export_hidden_object"])
    import_prefs.SetBool("ExportLegacy", previous["export_legacy"])
    import_prefs.SetBool("ExportKeepPlacement", previous["export_keep_placement"])


def export_part_objects(objects, output_path):
    Part.export(objects, str(output_path))


def export_import_objects(objects, output_path):
    Import.export(objects, str(output_path))


def export_compound(doc, objects, output_path):
    shapes = [obj.Shape.copy() for obj in objects]
    compound = Part.makeCompound(shapes)
    holder = doc.addObject("Part::Feature", "solidworks_import_compound")
    holder.Shape = compound
    doc.recompute()
    try:
        Part.export([holder], str(output_path))
    finally:
        doc.removeObject(holder.Name)
        doc.recompute()


def export_variants(source_path: Path, output_dir: Path):
    source_path = source_path.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    doc = App.openDocument(str(source_path))
    objects = collect_shape_objects(doc)
    production_objects = [obj for obj in objects if is_production_object(obj)]
    stats = shape_stats(objects)
    production_stats = shape_stats(production_objects)

    variants = [
        ("part_objects_ap214is", "AP214IS", export_part_objects),
        ("part_compound_ap214is", "AP214IS", export_compound),
        ("import_objects_ap214is", "AP214IS", export_import_objects),
        ("part_objects_ap203", "AP203", export_part_objects),
        ("part_compound_ap203", "AP203", export_compound),
    ]
    production_variants = [
        ("production_part_objects_ap214is", "AP214IS", export_part_objects),
        ("production_part_compound_ap214is", "AP214IS", export_compound),
        ("production_part_objects_ap203", "AP203", export_part_objects),
    ]

    report = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "source_path": str(source_path),
        "output_dir": str(output_dir.resolve()),
        "source_stats": stats,
        "production_stats": production_stats,
        "variants": [],
    }

    for name, scheme, exporter in variants:
        objects_to_export = objects
        output_path = output_dir / f"{source_path.stem}_{name}.stp"
        previous = set_step_preferences(scheme)
        item = {
            "name": name,
            "scheme": scheme,
            "method": exporter.__name__,
            "object_count": len(objects_to_export),
            "path": str(output_path),
            "status": "pending",
        }
        try:
            if exporter is export_compound:
                exporter(doc, objects_to_export, output_path)
            else:
                exporter(objects_to_export, output_path)
            item["status"] = "exported"
            item["size_bytes"] = output_path.stat().st_size
        except Exception as exc:
            item["status"] = "failed"
            item["error"] = repr(exc)
        finally:
            restore_step_preferences(previous)
        report["variants"].append(item)

    for name, scheme, exporter in production_variants:
        objects_to_export = production_objects
        output_path = output_dir / f"{source_path.stem}_{name}.stp"
        previous = set_step_preferences(scheme)
        item = {
            "name": name,
            "scheme": scheme,
            "method": exporter.__name__,
            "object_count": len(objects_to_export),
            "path": str(output_path),
            "status": "pending",
        }
        try:
            if exporter is export_compound:
                exporter(doc, objects_to_export, output_path)
            else:
                exporter(objects_to_export, output_path)
            item["status"] = "exported"
            item["size_bytes"] = output_path.stat().st_size
        except Exception as exc:
            item["status"] = "failed"
            item["error"] = repr(exc)
        finally:
            restore_step_preferences(previous)
        report["variants"].append(item)

    report_path = output_dir / "solidworks_step_reexport_variants_report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2, default=json_default), encoding="utf-8")
    print(str(report_path))
    App.closeDocument(doc.Name)
    return report


def main(argv):
    if len(argv) != 3:
        print("Usage: FreeCADCmd.exe reexport_step_variants_freecad.py <source.FCStd> <output-dir>", file=sys.stderr)
        return 2
    source_path = Path(argv[1])
    output_dir = Path(argv[2])
    export_variants(source_path, output_dir)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
