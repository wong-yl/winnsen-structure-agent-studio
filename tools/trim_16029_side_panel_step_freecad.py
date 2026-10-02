import json
import os

import FreeCAD
import Import
import Part


def mm(value):
    return round(float(value), 6)


def bbox_dict(shape):
    box = shape.BoundBox
    return {
        "xmin_mm": mm(box.XMin),
        "ymin_mm": mm(box.YMin),
        "zmin_mm": mm(box.ZMin),
        "xmax_mm": mm(box.XMax),
        "ymax_mm": mm(box.YMax),
        "zmax_mm": mm(box.ZMax),
        "xlen_mm": mm(box.XLength),
        "ylen_mm": mm(box.YLength),
        "zlen_mm": mm(box.ZLength),
    }


def main():
    source_step = os.environ["SOURCE_STEP"]
    output_step = os.environ["OUTPUT_STEP"]
    side = os.environ["SIDE"].lower()
    keep_boundary = float(os.environ["KEEP_BOUNDARY_MM"])
    report_json = os.environ["REPORT_JSON"]

    shape = Part.read(source_step)
    before = bbox_dict(shape)
    margin = 120.0
    box = shape.BoundBox

    if side == "left":
        cutter_x = keep_boundary
        cutter_width = max(1.0, box.XMax - keep_boundary + margin)
    elif side == "right":
        cutter_x = box.XMin - margin
        cutter_width = max(1.0, keep_boundary - (box.XMin - margin))
    else:
        raise ValueError(f"unsupported SIDE: {side}")

    cutter = Part.makeBox(
        cutter_width,
        box.YLength + margin * 2,
        box.ZLength + margin * 2,
        FreeCAD.Vector(cutter_x, box.YMin - margin, box.ZMin - margin),
    )
    trimmed = shape.cut(cutter)
    trimmed = trimmed.removeSplitter()
    if trimmed.isNull():
        raise RuntimeError("trimmed shape is null")

    export_shapes = list(trimmed.Solids) or [trimmed]
    os.makedirs(os.path.dirname(output_step), exist_ok=True)
    doc = FreeCAD.newDocument("trim_16029_side_panel")
    try:
        for index, export_shape in enumerate(export_shapes):
            obj = doc.addObject("Part::Feature", f"trimmed_side_panel_{index}")
            obj.Shape = export_shape
        doc.recompute()
        Import.export(doc.Objects, output_step)
    finally:
        FreeCAD.closeDocument(doc.Name)
    after = bbox_dict(trimmed)

    result = {
        "sourceStep": source_step,
        "outputStep": output_step,
        "side": side,
        "keepBoundaryMm": keep_boundary,
        "beforeBox": before,
        "afterBox": after,
        "solidCount": len(trimmed.Solids),
        "exportShapeCount": len(export_shapes),
        "saved": os.path.exists(output_step) and os.path.getsize(output_step) > 0,
    }
    os.makedirs(os.path.dirname(report_json), exist_ok=True)
    with open(report_json, "w", encoding="utf-8") as fh:
        json.dump(result, fh, ensure_ascii=False, indent=2)
        fh.write("\n")
    if not result["saved"]:
        raise RuntimeError("trimmed STEP was not saved")


if __name__ == "__main__":
    main()
