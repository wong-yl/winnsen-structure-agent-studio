from __future__ import annotations

import csv
import json
import math
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import FreeCAD as App
import Import


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def bbox_row(obj: Any, index: int) -> dict[str, Any] | None:
    shape = getattr(obj, "Shape", None)
    if shape is None or getattr(shape, "isNull", lambda: True)():
        return None
    box = shape.BoundBox
    values = (box.XMin, box.YMin, box.ZMin, box.XMax, box.YMax, box.ZMax, box.XLength, box.YLength, box.ZLength)
    if not all(math.isfinite(value) for value in values):
        return None
    if any(abs(value) > 1_000_000 for value in values):
        return None
    solids = getattr(shape, "Solids", [])
    faces = getattr(shape, "Faces", [])
    edges = getattr(shape, "Edges", [])
    return {
        "object_index": index,
        "object_name": getattr(obj, "Name", ""),
        "object_label": getattr(obj, "Label", ""),
        "type_id": getattr(obj, "TypeId", ""),
        "bbox_min_x_mm": round(box.XMin, 4),
        "bbox_min_y_mm": round(box.YMin, 4),
        "bbox_min_z_mm": round(box.ZMin, 4),
        "bbox_max_x_mm": round(box.XMax, 4),
        "bbox_max_y_mm": round(box.YMax, 4),
        "bbox_max_z_mm": round(box.ZMax, 4),
        "bbox_size_x_mm": round(box.XLength, 4),
        "bbox_size_y_mm": round(box.YLength, 4),
        "bbox_size_z_mm": round(box.ZLength, 4),
        "solid_count": len(solids),
        "face_count": len(faces),
        "edge_count": len(edges),
        "volume_mm3": round(getattr(shape, "Volume", 0.0), 4),
    }


def inspect_step(step_path: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    if not step_path.exists():
        raise FileNotFoundError(step_path)
    doc = App.newDocument("step_bbox_inspect")
    Import.insert(str(step_path), doc.Name)
    doc.recompute()

    rows: list[dict[str, Any]] = []
    source_object_count = len(doc.Objects)
    for index, obj in enumerate(doc.Objects, start=1):
        row = bbox_row(obj, index)
        if row is not None:
            rows.append(row)

    if rows:
        min_x = min(row["bbox_min_x_mm"] for row in rows)
        min_y = min(row["bbox_min_y_mm"] for row in rows)
        min_z = min(row["bbox_min_z_mm"] for row in rows)
        max_x = max(row["bbox_max_x_mm"] for row in rows)
        max_y = max(row["bbox_max_y_mm"] for row in rows)
        max_z = max(row["bbox_max_z_mm"] for row in rows)
    else:
        min_x = min_y = min_z = max_x = max_y = max_z = 0.0

    summary = {
        "generatedAt": utc_now(),
        "sourceStep": str(step_path),
        "sourceObjectCount": source_object_count,
        "objectCount": len(rows),
        "skippedObjectCount": source_object_count - len(rows),
        "assemblyBBoxMm": {
            "minX": round(min_x, 4),
            "minY": round(min_y, 4),
            "minZ": round(min_z, 4),
            "maxX": round(max_x, 4),
            "maxY": round(max_y, 4),
            "maxZ": round(max_z, 4),
            "sizeX": round(max_x - min_x, 4),
            "sizeY": round(max_y - min_y, 4),
            "sizeZ": round(max_z - min_z, 4),
        },
    }
    App.closeDocument(doc.Name)
    return rows, summary


def write_outputs(rows: list[dict[str, Any]], summary: dict[str, Any], out_csv: Path, out_json: Path, out_md: Path) -> None:
    out_csv.parent.mkdir(parents=True, exist_ok=True)
    if rows:
        with out_csv.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)
    else:
        out_csv.write_text("", encoding="utf-8-sig")

    payload = {**summary, "objects": rows}
    out_json.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    box = summary["assemblyBBoxMm"]
    lines = [
        "# STEP assembly bbox evidence",
        "",
        f"- Generated at: `{summary['generatedAt']}`",
        f"- Source STEP: `{summary['sourceStep']}`",
        f"- Object count: `{summary['objectCount']}`",
        f"- Assembly bbox: `{box['sizeX']} x {box['sizeY']} x {box['sizeZ']} mm`",
        "",
        "| Object | Label | BBox mm | Solids | Faces | Edges |",
        "| --- | --- | ---: | ---: | ---: | ---: |",
    ]
    for row in rows[:120]:
        lines.append(
            "| "
            + " | ".join(
                [
                    str(row["object_name"]).replace("|", "/"),
                    str(row["object_label"]).replace("|", "/"),
                    f"{row['bbox_size_x_mm']} x {row['bbox_size_y_mm']} x {row['bbox_size_z_mm']}",
                    str(row["solid_count"]),
                    str(row["face_count"]),
                    str(row["edge_count"]),
                ]
            )
            + " |"
        )
    lines.append("")
    out_md.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    env_args = [
        os.getenv("STEP_BBOX_INPUT"),
        os.getenv("STEP_BBOX_CSV"),
        os.getenv("STEP_BBOX_JSON"),
        os.getenv("STEP_BBOX_MD"),
    ]
    if all(env_args):
        step_path = Path(str(env_args[0]))
        out_csv = Path(str(env_args[1]))
        out_json = Path(str(env_args[2]))
        out_md = Path(str(env_args[3]))
    elif len(sys.argv) == 5:
        step_path = Path(sys.argv[1])
        out_csv = Path(sys.argv[2])
        out_json = Path(sys.argv[3])
        out_md = Path(sys.argv[4])
    else:
        print("usage: FreeCADCmd inspect_step_assembly_bboxes_freecad.py input.step out.csv out.json out.md")
        print("or set STEP_BBOX_INPUT, STEP_BBOX_CSV, STEP_BBOX_JSON, STEP_BBOX_MD")
        print(f"argv={sys.argv!r}")
        return 2
    rows, summary = inspect_step(step_path)
    write_outputs(rows, summary, out_csv, out_json, out_md)
    print(f"objects={len(rows)}")
    print(f"csv={out_csv}")
    print(f"json={out_json}")
    print(f"md={out_md}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
