from __future__ import annotations

import csv
import sys
from pathlib import Path

import FreeCAD as App
import Part


def solid_objects(doc):
    for obj in doc.Objects:
        shape = getattr(obj, "Shape", None)
        if shape and not shape.isNull() and len(shape.Solids) > 0:
            yield obj


PANEL_HEIGHT_BY_STEM = {
    "door_1_12_assembly_exact": 145.5,
    "door_2_12_assembly_exact": 298.0,
    "door_3_12_assembly_exact": 450.5,
    "door_4_12_assembly_exact": 603.0,
    "door_5_12_assembly_exact": 755.5,
    "door_6_12_assembly_exact": 908.0,
}


def is_size(bb, sx: float, sy: float, sz: float, tol: float = 0.45) -> bool:
    return abs(bb.XLength - sx) <= tol and abs(bb.YLength - sy) <= tol and abs(bb.ZLength - sz) <= tol


def bbox_fields(prefix: str, bb):
    return {
        f"{prefix}_x_min": round(bb.XMin, 6),
        f"{prefix}_x_max": round(bb.XMax, 6),
        f"{prefix}_x_center": round((bb.XMin + bb.XMax) / 2, 6),
        f"{prefix}_x_len": round(bb.XLength, 6),
        f"{prefix}_y_min": round(bb.YMin, 6),
        f"{prefix}_y_max": round(bb.YMax, 6),
        f"{prefix}_y_center": round((bb.YMin + bb.YMax) / 2, 6),
        f"{prefix}_y_len": round(bb.YLength, 6),
        f"{prefix}_z_min": round(bb.ZMin, 6),
        f"{prefix}_z_max": round(bb.ZMax, 6),
        f"{prefix}_z_center": round((bb.ZMin + bb.ZMax) / 2, 6),
        f"{prefix}_z_len": round(bb.ZLength, 6),
    }


def find_panel_bbox(path: Path, solids):
    expected_height = PANEL_HEIGHT_BY_STEM.get(path.stem)
    if expected_height is None:
        return None
    for obj in solids:
        bb = obj.Shape.BoundBox
        if is_size(bb, 437.0, expected_height, 15.0):
            return bb
    return None


def measure_step(path: Path):
    doc = App.newDocument("bbox_probe")
    Part.insert(str(path), doc.Name)
    doc.recompute()
    solids = list(solid_objects(doc))
    if not solids:
        App.closeDocument(doc.Name)
        raise RuntimeError(f"No solids imported from {path}")
    compound = Part.makeCompound([obj.Shape.copy() for obj in solids])
    bb = compound.BoundBox
    row = {
        "path": str(path),
        "solid_count": len(solids),
        **bbox_fields("assembly", bb),
    }
    panel_bb = find_panel_bbox(path, solids)
    row["panel_found"] = panel_bb is not None
    if panel_bb is not None:
        row.update(bbox_fields("panel", panel_bb))
        row["insert_x_for_panel_center_0"] = round(-((panel_bb.XMin + panel_bb.XMax) / 2), 6)
        row["insert_y_for_panel_center_0"] = round(-((panel_bb.YMin + panel_bb.YMax) / 2), 6)
        row["insert_z_for_panel_front_0"] = round(-panel_bb.ZMax, 6)
    App.closeDocument(doc.Name)
    return row


def main() -> int:
    if len(sys.argv) < 3:
        print("Usage: FreeCADCmd.exe tools/measure_freecad_step_bbox.py OUT.csv STEP...")
        return 2
    out = Path(sys.argv[1])
    paths = [Path(item) for item in sys.argv[2:]]
    rows = [measure_step(path) for path in paths]
    out.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = []
    for row in rows:
        for key in row:
            if key not in fieldnames:
                fieldnames.append(key)
    with out.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)
    print(out)
    for row in rows:
        print(
            f"{Path(row['path']).name}: "
            f"assembly_center=({row['assembly_x_center']},{row['assembly_y_center']},{row['assembly_z_center']}), "
            f"panel_center=({row.get('panel_x_center')},{row.get('panel_y_center')},{row.get('panel_z_center')}), "
            f"panel_insert_offset=({row.get('insert_x_for_panel_center_0')},{row.get('insert_y_for_panel_center_0')},{row.get('insert_z_for_panel_front_0')}), "
            f"solids={row['solid_count']}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
