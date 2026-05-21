from __future__ import annotations

import csv
import sys
from collections import Counter
from pathlib import Path
from typing import Any

import FreeCAD as App


CENTER_VERTICAL_SIGNATURES = {
    "SW_cabinet_vertical_L_explicit": {"solids": 5, "faces": 749, "edges": 2447},
    "SW_cabinet_vertical_R_explicit": {"solids": 5, "faces": 749, "edges": 2447},
    "SW_door_frame_vertical_L_explicit": {"solids": 1, "faces": 126, "edges": 396},
    "SW_door_frame_vertical_R_explicit": {"solids": 1, "faces": 126, "edges": 396},
}


def classify(name: str, label: str) -> str:
    text = f"{name} {label}"
    if name in CENTER_VERTICAL_SIGNATURES:
        return "center_vertical_cut"
    if name.startswith(("SW_exact_sheetmetal_door_panel", "door_panel")):
        return "door_panel"
    if name.startswith(("SW_rebuilt_sheetmetal_rib", "door_rib")):
        return "door_rib"
    if name.startswith(("SW_door_hardware", "door_hardware")):
        return "door_hardware"
    if name.startswith("REF_") or name.startswith("hinge_axis_reference"):
        return "semantic_reference"
    if name.startswith(("SW_shelf_", "shelf_")):
        return "shelf_weld"
    if name.startswith(("SW_door_frame_horizontal", "door_frame_cross")):
        return "door_frame_horizontal"
    if name.startswith("SW_API_"):
        return "solidworks_api_step_backed"
    if name.startswith("standard_top_export"):
        return "gold_cabinet_shell"
    if "Group" in text:
        return "group"
    return "other"


def rounded(value: float) -> str:
    return f"{value:.3f}"


def safe_call(default: Any, func):
    try:
        return func()
    except Exception:
        return default


def row_for_no_shape(obj, category: str) -> dict[str, str]:
    return {
        "name": obj.Name,
        "label": obj.Label,
        "category": category,
        "has_shape": "False",
        "valid": "",
        "closed": "",
        "solids": "0",
        "faces": "0",
        "edges": "0",
        "x_min": "",
        "x_max": "",
        "y_min": "",
        "y_max": "",
        "z_min": "",
        "z_max": "",
        "signature_status": "no_shape",
        "error": "",
    }


def main() -> None:
    if len(sys.argv) < 3:
        raise SystemExit("usage: freecad_audit_fcstd_geometry_integrity.py model.FCStd out_prefix [expected_x_len]")
    model_path = Path(sys.argv[1])
    out_prefix = Path(sys.argv[2])
    expected_x = float(sys.argv[3]) if len(sys.argv) > 3 and sys.argv[3] else None
    out_prefix.parent.mkdir(parents=True, exist_ok=True)
    out_csv = out_prefix.with_suffix(".csv")
    out_md = out_prefix.with_suffix(".md")

    doc = App.openDocument(str(model_path))
    rows: list[dict[str, str]] = []
    total_bb = None
    for obj in doc.Objects:
        category = classify(obj.Name, obj.Label)
        shape = getattr(obj, "Shape", None)
        has_shape = shape is not None and not safe_call(True, shape.isNull)
        if not has_shape:
            rows.append(row_for_no_shape(obj, category))
            continue

        error = ""
        bb = safe_call(None, lambda: shape.BoundBox)
        if bb is not None:
            if total_bb is None:
                total_bb = App.BoundBox(bb)
            else:
                total_bb.add(bb)

        solids = safe_call([], lambda: shape.Solids)
        faces = safe_call([], lambda: shape.Faces)
        edges = safe_call([], lambda: shape.Edges)
        valid = safe_call(False, shape.isValid)
        if valid is False:
            error = "shape_is_valid_false_or_exception"

        signature_status = "not_applicable"
        expected = CENTER_VERTICAL_SIGNATURES.get(obj.Name)
        if expected:
            signature_status = (
                "PASS"
                if len(solids) == expected["solids"]
                and len(faces) == expected["faces"]
                and len(edges) == expected["edges"]
                else "FAIL"
            )

        rows.append(
            {
                "name": obj.Name,
                "label": obj.Label,
                "category": category,
                "has_shape": "True",
                "valid": str(valid),
                "closed": "not_checked",
                "solids": str(len(solids)),
                "faces": str(len(faces)),
                "edges": str(len(edges)),
                "x_min": rounded(bb.XMin) if bb is not None else "",
                "x_max": rounded(bb.XMax) if bb is not None else "",
                "y_min": rounded(bb.YMin) if bb is not None else "",
                "y_max": rounded(bb.YMax) if bb is not None else "",
                "z_min": rounded(bb.ZMin) if bb is not None else "",
                "z_max": rounded(bb.ZMax) if bb is not None else "",
                "signature_status": signature_status,
                "error": error,
            }
        )
    App.closeDocument(doc.Name)

    fieldnames = [
        "name",
        "label",
        "category",
        "has_shape",
        "valid",
        "closed",
        "solids",
        "faces",
        "edges",
        "x_min",
        "x_max",
        "y_min",
        "y_max",
        "z_min",
        "z_max",
        "signature_status",
        "error",
    ]
    with out_csv.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    invalid = [row for row in rows if row["has_shape"] == "True" and row["valid"] != "True"]
    no_shape = [row for row in rows if row["has_shape"] != "True" and row["category"] != "group"]
    signature_fail = [row for row in rows if row["signature_status"] == "FAIL"]
    signature_checked = [row for row in rows if row["signature_status"] in {"PASS", "FAIL"}]
    counts = Counter(row["category"] for row in rows if row["has_shape"] == "True")

    bbox_status = "SKIP"
    actual_x = ""
    if total_bb is not None and expected_x is not None:
        actual_x = total_bb.XLength
        bbox_status = "PASS" if abs(actual_x - expected_x) <= 0.03 else "FAIL"

    status = "PASS" if not invalid and not no_shape and not signature_fail and bbox_status != "FAIL" else "FAIL"
    lines = [
        "# 16029 FCStd Geometry Integrity Audit",
        "",
        f"- model: `{model_path}`",
        f"- status: `{status}`",
        f"- shape_objects: `{sum(1 for row in rows if row['has_shape'] == 'True')}`",
        f"- invalid_shape_objects: `{len(invalid)}`",
        f"- non_group_no_shape_objects: `{len(no_shape)}`",
        f"- center_vertical_signature_checks: `{len(signature_checked)}`",
        f"- center_vertical_signature_failures: `{len(signature_fail)}`",
        f"- total_bbox_x_status: `{bbox_status}`",
    ]
    if total_bb is not None:
        lines.append(
            "- total_bbox: "
            f"`{rounded(total_bb.XMin)}..{rounded(total_bb.XMax)}, "
            f"{rounded(total_bb.YMin)}..{rounded(total_bb.YMax)}, "
            f"{rounded(total_bb.ZMin)}..{rounded(total_bb.ZMax)}`"
        )
    if actual_x != "":
        lines.append(f"- total_bbox_x_len: `{rounded(actual_x)}`")
    lines.extend(
        [
            "",
            "## Audit Notes",
            "",
            "- closed: `not_checked`; this avoids a known FreeCADCmd stability risk on large imported assemblies.",
            "- This audit is a geometry integrity gate for engineering-reference models, not a production drawing release.",
            "",
            "## Category Counts",
            "",
            "| category | shape_count |",
            "|---|---:|",
        ]
    )
    for category, count in sorted(counts.items()):
        lines.append(f"| {category} | {count} |")
    lines.extend(["", "## Failures", ""])
    failures = invalid + no_shape + signature_fail
    if failures:
        lines.extend(
            f"- `{row['name']}` category={row['category']} signature={row['signature_status']} "
            f"valid={row['valid']} error={row['error']}"
            for row in failures
        )
    else:
        lines.append("- none")
    out_md.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(out_csv)
    print(out_md)
    if status != "PASS":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
