import csv
import json
import os
import sys
from pathlib import Path

import FreeCAD as App
import Import


def shape_stats(obj):
    shape = getattr(obj, "Shape", None)
    if shape is None or shape.isNull():
        return None
    bb = shape.BoundBox
    return {
        "shape_type": getattr(shape, "ShapeType", ""),
        "solid_count": len(getattr(shape, "Solids", []) or []),
        "face_count": len(getattr(shape, "Faces", []) or []),
        "edge_count": len(getattr(shape, "Edges", []) or []),
        "volume": round(float(getattr(shape, "Volume", 0.0)), 6),
        "area": round(float(getattr(shape, "Area", 0.0)), 6),
        "x_min": round(float(bb.XMin), 6),
        "x_max": round(float(bb.XMax), 6),
        "y_min": round(float(bb.YMin), 6),
        "y_max": round(float(bb.YMax), 6),
        "z_min": round(float(bb.ZMin), 6),
        "z_max": round(float(bb.ZMax), 6),
        "x_len": round(float(bb.XLength), 6),
        "y_len": round(float(bb.YLength), 6),
        "z_len": round(float(bb.ZLength), 6),
        "x_center": round(float((bb.XMin + bb.XMax) / 2.0), 6),
        "y_center": round(float((bb.YMin + bb.YMax) / 2.0), 6),
        "z_center": round(float((bb.ZMin + bb.ZMax) / 2.0), 6),
    }


def main():
    if len(sys.argv) >= 4:
        step_path = Path(sys.argv[1])
        json_out = Path(sys.argv[2])
        csv_out = Path(sys.argv[3])
    elif os.environ.get("STEP_PATH") and os.environ.get("JSON_OUT") and os.environ.get("CSV_OUT"):
        step_path = Path(os.environ["STEP_PATH"])
        json_out = Path(os.environ["JSON_OUT"])
        csv_out = Path(os.environ["CSV_OUT"])
    else:
        print("usage: freecad_step_bbox_inventory.py <step-path> <json-out> <csv-out>", file=sys.stderr)
        return 2

    doc = App.newDocument("step_inventory")
    Import.insert(str(step_path), doc.Name)
    doc.recompute()

    rows = []
    for index, obj in enumerate(doc.Objects):
        stats = shape_stats(obj)
        if not stats:
            continue
        row = {
            "index": index,
            "name": obj.Name,
            "label": obj.Label,
            "type_id": obj.TypeId,
        }
        row.update(stats)
        rows.append(row)

    summary = {
        "step_path": str(step_path),
        "object_count": len(doc.Objects),
        "shape_object_count": len(rows),
        "objects": rows,
    }

    json_out.parent.mkdir(parents=True, exist_ok=True)
    json_out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    if rows:
        csv_out.parent.mkdir(parents=True, exist_ok=True)
        with csv_out.open("w", newline="", encoding="utf-8-sig") as f:
            writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)

    print(json_out)
    print(csv_out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
