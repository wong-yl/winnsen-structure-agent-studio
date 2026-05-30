from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import FreeCAD as App
import Import


def read_arg(index: int, env_name: str) -> str:
    value = os.environ.get(env_name)
    if value:
        return value
    if len(sys.argv) > index:
        return sys.argv[index]
    raise SystemExit(f"missing {env_name}")


def finite_bbox(shape) -> bool:
    box = shape.BoundBox
    return all(abs(float(v)) < 1e20 for v in (box.XMin, box.XMax, box.YMin, box.YMax, box.ZMin, box.ZMax))


def valid_shape_object(obj) -> bool:
    shape = getattr(obj, "Shape", None)
    return shape is not None and not shape.isNull() and bool(shape.Solids) and finite_bbox(shape)


def bbox(shape) -> dict[str, float]:
    box = shape.BoundBox
    return {
        "x_min": round(float(box.XMin), 6),
        "x_max": round(float(box.XMax), 6),
        "y_min": round(float(box.YMin), 6),
        "y_max": round(float(box.YMax), 6),
        "z_min": round(float(box.ZMin), 6),
        "z_max": round(float(box.ZMax), 6),
        "x_len": round(float(box.XLength), 6),
        "y_len": round(float(box.YLength), 6),
        "z_len": round(float(box.ZLength), 6),
    }


def main() -> int:
    source_step = Path(read_arg(1, "SOURCE_STEP"))
    output_step = Path(read_arg(2, "OUTPUT_STEP"))
    report_json = Path(read_arg(3, "REPORT_JSON"))
    tx = float(os.environ.get("LATCH_TX_MM", "-143.5"))
    bottom_ty = float(os.environ.get("LATCH_BOTTOM_TY_MM", "-120.2"))
    tz = float(os.environ.get("LATCH_TZ_MM", "-0.8"))

    output_step.parent.mkdir(parents=True, exist_ok=True)
    report_json.parent.mkdir(parents=True, exist_ok=True)

    src_doc = App.newDocument("bottom_latch_source")
    Import.insert(str(source_step), src_doc.Name)
    src_doc.recompute()

    # Bottom latch transform in door coordinates:
    #   x=-local_x+tx, y=-local_z+bottom_ty, z=-local_y+tz
    # Mirroring that whole component through the door center plane Y=0 gives:
    #   x=-local_x+tx, y=local_z-bottom_ty, z=-local_y+tz
    matrix = App.Matrix()
    matrix.A11 = -1.0
    matrix.A12 = 0.0
    matrix.A13 = 0.0
    matrix.A14 = tx
    matrix.A21 = 0.0
    matrix.A22 = 0.0
    matrix.A23 = 1.0
    matrix.A24 = -bottom_ty
    matrix.A31 = 0.0
    matrix.A32 = -1.0
    matrix.A33 = 0.0
    matrix.A34 = tz

    out_doc = App.newDocument("top_latch_from_bottom_mirror")
    out_objects = []
    objects = []
    for index, obj in enumerate(src_doc.Objects):
        if not valid_shape_object(obj):
            continue
        transformed = obj.Shape.copy()
        transformed.transformShape(matrix, True)
        out_obj = out_doc.addObject("Part::Feature", f"top_latch_mirror_{index}")
        out_obj.Label = f"top_latch_mirror_{index}"
        out_obj.Shape = transformed
        out_objects.append(out_obj)
        objects.append(
            {
                "source_index": index,
                "source_label": obj.Label,
                "source_bbox_mm": bbox(obj.Shape),
                "top_mirror_bbox_mm": bbox(transformed),
            }
        )

    if not out_objects:
        raise RuntimeError(f"no valid solid bodies found in {source_step}")

    out_doc.recompute()
    Import.export(out_objects, str(output_step))
    report_json.write_text(
        json.dumps(
            {
                "source_step": str(source_step),
                "output_step": str(output_step),
                "transform": {
                    "tx_mm": tx,
                    "bottom_ty_mm": bottom_ty,
                    "top_ty_mm": -bottom_ty,
                    "tz_mm": tz,
                },
                "objects": objects,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    App.closeDocument(out_doc.Name)
    App.closeDocument(src_doc.Name)
    print(str(report_json))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
