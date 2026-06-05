from __future__ import annotations

import json
import os
from pathlib import Path

import FreeCAD as App
import Part


def env_path(name: str) -> Path:
    value = os.environ.get(name)
    if not value:
        raise SystemExit(f"missing required env var: {name}")
    return Path(value)


def env_float(name: str, default: float) -> float:
    value = os.environ.get(name)
    if value is None or value == "":
        return default
    return float(value)


def solid_shapes(doc):
    for obj in doc.Objects:
        shape = getattr(obj, "Shape", None)
        if shape and not shape.isNull() and len(shape.Solids) > 0:
            yield shape.copy()


def bbox_dict(shape):
    box = shape.BoundBox
    return {
        "xminMm": round(float(box.XMin), 6),
        "xmaxMm": round(float(box.XMax), 6),
        "yminMm": round(float(box.YMin), 6),
        "ymaxMm": round(float(box.YMax), 6),
        "zminMm": round(float(box.ZMin), 6),
        "zmaxMm": round(float(box.ZMax), 6),
        "xlenMm": round(float(box.XLength), 6),
        "ylenMm": round(float(box.YLength), 6),
        "zlenMm": round(float(box.ZLength), 6),
    }


source_step = env_path("WINNSEN_16029_SCALE_SOURCE_STEP")
output_step = env_path("WINNSEN_16029_SCALE_OUTPUT_STEP")
report_json = os.environ.get("WINNSEN_16029_SCALE_REPORT_JSON", "")
scale_x = env_float("WINNSEN_16029_SCALE_X", 1.0)
scale_y = env_float("WINNSEN_16029_SCALE_Y", 1.0)
scale_z = env_float("WINNSEN_16029_SCALE_Z", 1.0)

if not source_step.exists():
    raise SystemExit(f"source STEP not found: {source_step}")

doc = App.newDocument("scale_16029_source_step")
Part.insert(str(source_step), doc.Name)
doc.recompute()
shapes = list(solid_shapes(doc))
if not shapes:
    raise SystemExit(f"no solids found in source STEP: {source_step}")

shape = shapes[0] if len(shapes) == 1 else Part.makeCompound(shapes)
before = bbox_dict(shape)
matrix = App.Matrix()
matrix.A11 = scale_x
matrix.A22 = scale_y
matrix.A33 = scale_z
scaled = shape.transformGeometry(matrix)

out_doc = App.newDocument("scaled_16029_source_step")
obj = out_doc.addObject("Part::Feature", "scaled_source")
obj.Shape = scaled
out_doc.recompute()
output_step.parent.mkdir(parents=True, exist_ok=True)
Part.export([obj], str(output_step))

if report_json:
    report_path = Path(report_json)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "winnsen.locker16029.scaled_source_step.v1",
        "sourceStep": str(source_step),
        "outputStep": str(output_step),
        "scale": {"x": scale_x, "y": scale_y, "z": scale_z},
        "before": before,
        "after": bbox_dict(scaled),
        "solidCount": len(shapes),
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

