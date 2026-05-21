from __future__ import annotations

import argparse
import csv
import json
import math
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import FreeCAD as App
import Import


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def finite(value: float) -> bool:
    return math.isfinite(value) and abs(value) < 1_000_000


def bbox_to_dict(box: Any) -> dict[str, float]:
    return {
        "min_x": round(box.XMin, 4),
        "min_y": round(box.YMin, 4),
        "min_z": round(box.ZMin, 4),
        "max_x": round(box.XMax, 4),
        "max_y": round(box.YMax, 4),
        "max_z": round(box.ZMax, 4),
        "size_x": round(box.XLength, 4),
        "size_y": round(box.YLength, 4),
        "size_z": round(box.ZLength, 4),
    }


def row_for_shape(obj: Any, index: int) -> dict[str, Any] | None:
    shape = getattr(obj, "Shape", None)
    if shape is None or getattr(shape, "isNull", lambda: True)():
        return None
    box = shape.BoundBox
    values = (box.XMin, box.YMin, box.ZMin, box.XMax, box.YMax, box.ZMax, box.XLength, box.YLength, box.ZLength)
    if not all(finite(float(value)) for value in values):
        return None
    solids = getattr(shape, "Solids", [])
    faces = getattr(shape, "Faces", [])
    edges = getattr(shape, "Edges", [])
    try:
        valid = bool(shape.isValid())
    except Exception:
        valid = False
    return {
        "object_index": index,
        "object_name": getattr(obj, "Name", ""),
        "object_label": getattr(obj, "Label", ""),
        "type_id": getattr(obj, "TypeId", ""),
        "valid": valid,
        "solid_count": len(solids),
        "face_count": len(faces),
        "edge_count": len(edges),
        "volume_mm3": round(float(getattr(shape, "Volume", 0.0)), 4),
        **bbox_to_dict(box),
    }


def open_model(path: Path) -> tuple[Any, str]:
    suffix = path.suffix.lower()
    if suffix == ".fcstd":
        doc = App.openDocument(str(path))
        return doc, "fcstd"
    if suffix in {".step", ".stp"}:
        doc = App.newDocument("intake_step_check")
        Import.insert(str(path), doc.Name)
        doc.recompute()
        return doc, "step"
    raise ValueError(f"Unsupported FreeCAD geometry check suffix: {suffix}")


def inspect_model(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(path)
    doc, model_type = open_model(path)
    try:
        rows: list[dict[str, Any]] = []
        for index, obj in enumerate(doc.Objects, start=1):
            row = row_for_shape(obj, index)
            if row is not None:
                rows.append(row)

        if rows:
            min_x = min(row["min_x"] for row in rows)
            min_y = min(row["min_y"] for row in rows)
            min_z = min(row["min_z"] for row in rows)
            max_x = max(row["max_x"] for row in rows)
            max_y = max(row["max_y"] for row in rows)
            max_z = max(row["max_z"] for row in rows)
        else:
            min_x = min_y = min_z = max_x = max_y = max_z = 0.0

        invalid_count = sum(1 for row in rows if not row["valid"])
        solid_count = sum(int(row["solid_count"]) for row in rows)
        if not rows:
            quality_status = "blocked_no_shape"
        elif invalid_count:
            quality_status = "geometry_check_needs_repair"
        elif solid_count == 0:
            quality_status = "geometry_check_no_solids"
        else:
            quality_status = "geometry_check_pass"

        return {
            "generated_at": utc_now(),
            "source_path": str(path),
            "file_name": path.name,
            "model_type": model_type,
            "quality_status": quality_status,
            "source_object_count": len(doc.Objects),
            "shape_object_count": len(rows),
            "invalid_shape_count": invalid_count,
            "solid_count": solid_count,
            "face_count": sum(int(row["face_count"]) for row in rows),
            "edge_count": sum(int(row["edge_count"]) for row in rows),
            "assembly_bbox_mm": {
                "min_x": round(min_x, 4),
                "min_y": round(min_y, 4),
                "min_z": round(min_z, 4),
                "max_x": round(max_x, 4),
                "max_y": round(max_y, 4),
                "max_z": round(max_z, 4),
                "size_x": round(max_x - min_x, 4),
                "size_y": round(max_y - min_y, 4),
                "size_z": round(max_z - min_z, 4),
            },
            "objects": rows,
        }
    finally:
        App.closeDocument(doc.Name)


def write_outputs(summary: dict[str, Any], out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    json_path = out_dir / "freecad_geometry_check.json"
    csv_path = out_dir / "freecad_geometry_objects.csv"
    md_path = out_dir / "freecad_geometry_check.md"
    json_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    rows = summary["objects"]
    if rows:
        with csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)
    else:
        csv_path.write_text("", encoding="utf-8-sig")

    bbox = summary["assembly_bbox_mm"]
    lines = [
        "# FreeCAD intake geometry check",
        "",
        "> Output level: engineering reference. This is not a released production drawing.",
        "",
        f"- Source file: `{summary['source_path']}`",
        f"- Model type: `{summary['model_type']}`",
        f"- Quality status: `{summary['quality_status']}`",
        f"- Shape objects: `{summary['shape_object_count']}`",
        f"- Solids: `{summary['solid_count']}`",
        f"- Invalid shapes: `{summary['invalid_shape_count']}`",
        f"- Assembly bbox: `{bbox['size_x']} x {bbox['size_y']} x {bbox['size_z']} mm`",
        "",
        "| Object | Label | Valid | BBox mm | Solids | Faces | Edges |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: |",
    ]
    for row in rows[:120]:
        lines.append(
            "| "
            + " | ".join(
                [
                    str(row["object_name"]).replace("|", "/"),
                    str(row["object_label"]).replace("|", "/"),
                    str(row["valid"]),
                    f"{row['size_x']} x {row['size_y']} x {row['size_z']}",
                    str(row["solid_count"]),
                    str(row["face_count"]),
                    str(row["edge_count"]),
                ]
            )
            + " |"
        )
    lines.append("")
    md_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    env_model = os.getenv("INTAKE_MODEL_PATH")
    env_out_dir = os.getenv("INTAKE_CHECK_OUT_DIR")
    if env_model and env_out_dir:
        summary = inspect_model(Path(env_model))
        write_outputs(summary, Path(env_out_dir))
        print(json.dumps({"status": "ok", "out_dir": env_out_dir, "summary": summary}, ensure_ascii=False))
        return 0

    parser = argparse.ArgumentParser(description="Run a lightweight FreeCAD geometry check for STEP/STP/FCStd intake files.")
    parser.add_argument("model", type=Path, help="STEP/STP/FCStd source file.")
    parser.add_argument("--out-dir", type=Path, required=True, help="Output directory.")
    args = parser.parse_args()

    summary = inspect_model(args.model)
    write_outputs(summary, args.out_dir)
    print(json.dumps({"status": "ok", "out_dir": str(args.out_dir), "summary": summary}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
