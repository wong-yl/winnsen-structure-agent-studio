from __future__ import annotations

import csv
import importlib.util
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT = Path(r"D:\Winnsen_Structure_Agent_Studio")
DATA = ROOT / "data"
STD_SCRIPT = Path(os.environ.get("LOCKER_STANDARD_GENERATOR", r"C:\sw16029_standard_ascii\generate_locker_16029_freecad.py"))
VARIANT_TOKEN = os.environ.get("LOCKER_800W_VARIANT_TOKEN", "lms").lower()
ROW_UNITS = [
    int(part)
    for part in os.environ.get("LOCKER_800W_ROW_UNITS_BOTTOM_TO_TOP", "6,4,2").split(",")
    if part.strip()
]
MODEL_DIR = Path(
    os.environ.get(
        "LOCKER_800W_MODEL_DIR",
        str(ROOT / "workers" / "generated_models" / f"FC-16029-800W-1917H-550D-{VARIANT_TOKEN.upper()}-GOLD-VARIABLE-20260528"),
    )
)
STEM = os.environ.get("LOCKER_800W_STEM", f"candidate_16029_800W_1917H_550D_{VARIANT_TOKEN}_6door_gold_variable")

FCSTD_PATH = MODEL_DIR / f"{STEM}.FCStd"
STEP_PATH = MODEL_DIR / f"{STEM}.step"
VERIFY_CSV = MODEL_DIR / f"{STEM}_verify.csv"
REPORT_MD = MODEL_DIR / f"{STEM}_report.md"
PREVIEW_PNG = MODEL_DIR / f"{STEM}_front_self_review.png"
MODEL_GATE_JSON = DATA / f"locker_16029_800w_{VARIANT_TOKEN}_gold_variable_model_gate.json"
MODEL_GATE_MD = DATA / f"locker_16029_800w_{VARIANT_TOKEN}_gold_variable_model_gate.md"
MODEL_GATE_CSV = DATA / f"locker_16029_800w_{VARIANT_TOKEN}_gold_variable_model_gate.csv"

UNIT_PITCH_MM = 152.5
DOOR_AREA_BOTTOM_MM = 30.0
GRID_BOTTOM_GAP_MM = 2.0
GRID_TOP_GAP_MM = 2.0
VISUAL_GAP_MM = 7.0
TOL = 0.01


def load_standard_module() -> Any:
    if not STD_SCRIPT.exists():
        raise FileNotFoundError(STD_SCRIPT)
    spec = importlib.util.spec_from_file_location("sw16029_standard_generator", STD_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot import {STD_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


g = load_standard_module()
App = g.App
Part = g.Part


def now_local_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def rounded(value: float) -> float:
    return round(float(value), 3)


def label_for_units(slot_units: int) -> str:
    return {6: "large", 4: "medium", 2: "small"}.get(slot_units, f"{slot_units}/12")


def row_sequence_text() -> str:
    return ", ".join(f"{label_for_units(unit)} {unit}/12" for unit in ROW_UNITS)


def rows_from_units(units: list[int]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    cursor_units = 0
    for index, units_value in enumerate(units, start=1):
        y_min = DOOR_AREA_BOTTOM_MM + GRID_BOTTOM_GAP_MM + cursor_units * UNIT_PITCH_MM
        height = units_value * UNIT_PITCH_MM - VISUAL_GAP_MM
        y_max = y_min + height
        rows.append(
            {
                "index": index,
                "slot_units": units_value,
                "size_label": label_for_units(units_value),
                "y_min": y_min,
                "y_max": y_max,
                "center_y": (y_min + y_max) / 2.0,
                "height": height,
            }
        )
        cursor_units += units_value
    return rows


def add_check(checks: list[dict[str, Any]], name: str, ok: bool, actual: Any, expected: Any, severity: str = "error") -> None:
    checks.append({"name": name, "ok": bool(ok), "actual": actual, "expected": expected, "severity": severity})


def write_table_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames: list[str] = []
    for row in rows:
        for key in row:
            if key not in fieldnames:
                fieldnames.append(key)
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def render_preview(rows: list[dict[str, Any]]) -> None:
    try:
        from PIL import Image, ImageDraw, ImageFont
    except Exception:
        return

    width_mm = 800.0
    height_mm = 1917.0
    scale = 0.42
    margin = 40
    extra_w = 270
    img_w = int(width_mm * scale + margin * 2 + extra_w)
    img_h = int(height_mm * scale + margin * 2)
    image = Image.new("RGB", (img_w, img_h), "#f8f8f5")
    draw = ImageDraw.Draw(image)
    try:
        font = ImageFont.truetype("arial.ttf", 14)
        small_font = ImageFont.truetype("arial.ttf", 12)
        title_font = ImageFont.truetype("arialbd.ttf", 16)
    except Exception:
        font = ImageFont.load_default()
        small_font = font
        title_font = font

    def sx(x: float) -> int:
        return int(round(margin + (x + width_mm / 2.0) * scale))

    def sy(y: float) -> int:
        return int(round(margin + (height_mm - y) * scale))

    def rect(x1: float, y1: float, x2: float, y2: float, fill: str, outline: str = "#111827", width: int = 1) -> None:
        draw.rectangle([sx(x1), sy(y2), sx(x2), sy(y1)], fill=fill, outline=outline, width=width)

    def center_text(x: float, y: float, value: str, active_font=None, fill: str = "#111827") -> None:
        active_font = active_font or small_font
        box = draw.textbbox((0, 0), value, font=active_font)
        draw.text((sx(x) - (box[2] - box[0]) / 2, sy(y) - (box[3] - box[1]) / 2), value, font=active_font, fill=fill)

    rect(-400, 0, 400, 1917, "#ffffff", "#111827", 2)
    rect(-380, 30, 380, 1857, "#eceff3", "#374151", 1)
    rect(-40, 30, 40, 1857, "#d8dde5", "#111827", 1)
    colors = {"large": "#9aa7b5", "medium": "#b7c0ca", "small": "#d1d7de"}
    for col in ("L", "R"):
        cx = -208.5 if col == "L" else 208.5
        for row in rows:
            x1 = cx - 337.0 / 2.0
            x2 = cx + 337.0 / 2.0
            rect(x1, row["y_min"], x2, row["y_max"], colors[row["size_label"]])
            center_text(cx, row["center_y"] + 16, f"{row['size_label']} {row['slot_units']}/12")
            center_text(cx, row["center_y"] - 16, f"H{row['height']:.0f}")
            lock_x = cx + (337.0 / 2.0 - 15.0) if col == "L" else cx - (337.0 / 2.0 - 15.0)
            hinge_x = cx - (337.0 / 2.0 - 10.0) if col == "L" else cx + (337.0 / 2.0 - 10.0)
            draw.ellipse([sx(lock_x) - 4, sy(row["center_y"]) - 4, sx(lock_x) + 4, sy(row["center_y"]) + 4], fill="#d35400")
            draw.rectangle([sx(hinge_x) - 2, sy(row["y_max"] - 35), sx(hinge_x) + 2, sy(row["y_min"] + 35)], fill="#1f4f9a")

    x0 = int(width_mm * scale + margin * 2 + 18)
    draw.text((x0, 64), f"16029 800W {VARIANT_TOKEN.upper()}", font=title_font, fill="#111827")
    draw.text((x0, 108), "Gold-rule variable model", font=font, fill="#111827")
    draw.text((x0, 148), "800W x 1917H x 550D", font=font, fill="#111827")
    draw.text((x0, 188), "Door width W337", font=font, fill="#111827")
    draw.text((x0, 236), "Bottom-to-top:", font=font, fill="#111827")
    for idx, item in enumerate(row_sequence_text().split(", ")):
        draw.text((x0, 276 + idx * 38), item, font=font, fill="#111827")
    draw.text((x0, 420), "Gap: 2 + 3 + 2 = 7", font=font, fill="#111827")
    draw.text((x0, 472), "Orange=lock, blue=hinge", font=small_font, fill="#374151")
    draw.text((x0, 520), "Self-reviewed before handoff", font=font, fill="#7c2d12")
    image.save(PREVIEW_PNG)


def build_model() -> dict[str, Any]:
    if sum(ROW_UNITS) != 12:
        raise ValueError(f"ROW_UNITS must sum to 12, got {ROW_UNITS}")
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    width_info = g.apply_width_rules(cabinet_width=800.0)
    grid_rows = rows_from_units(ROW_UNITS)

    colors = {
        "door": (0.55, 0.63, 0.70),
        "rib": (0.40, 0.47, 0.50),
        "shelf": (0.72, 0.70, 0.58),
        "frame": (0.30, 0.33, 0.34),
        "vertical": (0.24, 0.28, 0.30),
        "lock": (0.95, 0.10, 0.08),
        "hinge": (0.10, 0.25, 0.95),
        "hardware_ref": (0.95, 0.55, 0.10),
        "bend_ref": (0.05, 0.70, 0.95),
    }

    name = f"locker_16029_800w_{VARIANT_TOKEN}_gold_variable"
    doc = App.newDocument(name)
    Part.insert(str(g.TOP_STEP), doc.Name)
    doc.recompute()
    removed = g.remove_seed_layout(doc)
    doc.recompute()
    shell_mode_info = g.adjust_imported_shell_to_width_rules(doc, g.RULES["cabinet"]["width"])
    doc.recompute()

    shelf_shapes = {side: g.import_compound(path, f"shelf_{side}_source") for side, path in g.SHELF_STEPS.items()}
    frame_h = g.extract_frame_horizontal_shapes()
    vertical_shapes = {vname: g.import_compound(path, vname + "_source") for vname, path in g.VERTICAL_STEPS.items()}
    hardware_templates = g.load_door_hardware_templates()

    rows: list[dict[str, Any]] = []
    for vname, shape in vertical_shapes.items():
        vshape = shape
        if vname.endswith("_L"):
            vshape = shape.copy()
            vshape.translate(App.Vector(g.RULES["cabinet"]["left_column"][0] - (-477.0), 0, 0))
        elif vname.endswith("_R"):
            vshape = shape.copy()
            vshape.translate(App.Vector(g.RULES["cabinet"]["right_column"][1] - 477.0, 0, 0))
        obj = g.add_shape(doc, "SW_" + vname + "_explicit", vshape, colors["vertical"])
        bb = obj.Shape.BoundBox
        rows.append(
            {
                "item": obj.Name,
                "type": "fixed_center_vertical_structure",
                "driver": "center_control_band",
                "x_min": rounded(bb.XMin),
                "x_max": rounded(bb.XMax),
                "y_min": rounded(bb.YMin),
                "y_max": rounded(bb.YMax),
                "source_level": "solidworks_step",
                "note": "fixed center-band datum structure; not scaled with door height",
            }
        )

    columns = {"L": g.RULES["cabinet"]["left_column"], "R": g.RULES["cabinet"]["right_column"]}
    for col, (x0, x1) in columns.items():
        cx = (x0 + x1) / 2.0
        mirror = col == "R"
        for r in grid_rows:
            i = int(r["index"])
            cy = float(r["center_y"])
            door_h = float(r["height"])
            obj = g.add_parametric_derived_door(doc, col, i, cx, cy, door_h, colors, template=None)
            lock_x = cx + g.RULES["ordinary_door"]["lock_dx"] if col == "L" else cx - g.RULES["ordinary_door"]["lock_dx"]
            hinge_x = cx + g.RULES["ordinary_door"]["hinge_dx"] if col == "L" else cx - g.RULES["ordinary_door"]["hinge_dx"]
            g.add_box(doc, f"hinge_axis_reference_{col}{i:02d}", hinge_x - 1.5, cy - door_h / 2 + 3.0, 1.0, 3.0, door_h - 6.0, 1.0, colors["hinge"])
            for hw in hardware_templates:
                hw_obj = g.add_shape(
                    doc,
                    f"SW_door_hardware_{hw['name']}_{col}{i:02d}",
                    g.translated_hardware_by_door(hw, cx, cy, door_h, mirror=mirror),
                    colors["hardware_ref"],
                )
                hw_bb = hw_obj.Shape.BoundBox
                rows.append(
                    {
                        "item": hw_obj.Name,
                        "type": "door_hardware_step_repositioned",
                        "hardware_type": hw["type"],
                        "driver": "door_local_coordinate",
                        "column": col,
                        "row": i,
                        "slot_units": r["slot_units"],
                        "size_label": r["size_label"],
                        "x_center": rounded((hw_bb.XMin + hw_bb.XMax) / 2),
                        "y_center": rounded((hw_bb.YMin + hw_bb.YMax) / 2),
                        "z_center": rounded((hw_bb.ZMin + hw_bb.ZMax) / 2),
                        "x_len": rounded(hw_bb.XLength),
                        "y_len": rounded(hw_bb.YLength),
                        "z_len": rounded(hw_bb.ZLength),
                        "door_height": rounded(door_h),
                        "y_relation": hw["y_relation"],
                        "y_offset": rounded(hw["y_offset"]),
                        "source_level": "validated_3_12_assembly_step_template_repositioned",
                        "note": "real hardware geometry from validated door assembly; y position follows door-local relation",
                    }
                )
            rows.append(
                {
                    "item": obj.Name,
                    "type": "door_module",
                    "driver": "door_local_coordinate",
                    "column": col,
                    "row": i,
                    "slot_units": r["slot_units"],
                    "size_label": r["size_label"],
                    "x_center": rounded(cx),
                    "y_min": rounded(r["y_min"]),
                    "y_max": rounded(r["y_max"]),
                    "door_height": rounded(door_h),
                    "door_width": rounded(g.RULES["ordinary_door"]["width"]),
                    "flat_width": rounded(g.RULES["ordinary_door"]["flat_width"]),
                    "flat_height": rounded(door_h + g.RULES["ordinary_door"]["flat_height_extra"]),
                    "sheet_thickness": rounded(g.RULES["ordinary_door"]["sheet_thickness"]),
                    "return_depth": rounded(g.RULES["ordinary_door"]["return_depth"]),
                    "lock_hole_center_x": rounded(lock_x),
                    "lock_hole_center_y": rounded(cy),
                    "hinge_axis_x": rounded(hinge_x),
                    "source_level": "gold_semantic_one_piece_folded_sheetmetal",
                    "note": "door drives panel, weldment, rib, lock, hinge and local hardware references",
                }
            )
            g.add_door_sheetmetal_bend_references(doc, rows, col, i, cx, cy, door_h, colors)
            g.add_door_driven_reference_parts(doc, rows, col, i, cx, cy, door_h, colors, has_real_rib=False)

    for col, shelf_shape in shelf_shapes.items():
        for r in grid_rows[:-1]:
            shelf_y0 = float(r["y_max"]) + g.RULES["driven_parts"]["shelf_y_min_from_lower_door_y_max"]
            x0, x1 = columns[col]
            shelf = g.scaled_width_shape_by_bbox(shelf_shape, x1 - x0, (x0 + x1) / 2.0)
            obj = g.add_shape(doc, f"SW_shelf_{col}_weld_{VARIANT_TOKEN}_{r['index']:02d}", g.translated_by_bbox(shelf, shelf_y0), colors["shelf"])
            rows.append(
                {
                    "item": obj.Name,
                    "type": "shelf_weld",
                    "driver": "adjacent_door_boundary",
                    "column": col,
                    "row": r["index"],
                    "slot_units_below": r["slot_units"],
                    "y_min": rounded(obj.Shape.BoundBox.YMin),
                    "y_max": rounded(obj.Shape.BoundBox.YMax),
                    "source_level": "solidworks_step_repositioned",
                    "note": "shelf y_min = lower_door_y_max + 2",
                }
            )

    for col, shape in frame_h.items():
        for r in grid_rows[:-1]:
            frame_y0 = float(r["y_max"]) + g.RULES["driven_parts"]["frame_horizontal_y_min_from_lower_door_y_max"]
            x0, x1 = columns[col]
            frame_shape = g.scaled_width_shape_by_bbox(shape, x1 - x0 + 8.0, (x0 + x1) / 2.0)
            obj = g.add_shape(doc, f"SW_door_frame_horizontal_{col}_{VARIANT_TOKEN}_{r['index']:02d}", g.translated_by_bbox(frame_shape, frame_y0), colors["frame"])
            rows.append(
                {
                    "item": obj.Name,
                    "type": "door_frame_horizontal",
                    "driver": "adjacent_door_boundary",
                    "column": col,
                    "row": r["index"],
                    "slot_units_below": r["slot_units"],
                    "y_min": rounded(obj.Shape.BoundBox.YMin),
                    "y_max": rounded(obj.Shape.BoundBox.YMax),
                    "source_level": "solidworks_step_repositioned",
                    "note": "frame horizontal y_min = lower_door_y_max - 10",
                }
            )

    doc.recompute()
    solids = list(g.solid_objects(doc))
    invalid = [obj.Name for obj in solids if not obj.Shape.isValid()]
    if invalid:
        raise RuntimeError("Invalid solids: " + ", ".join(invalid))

    doc.saveAs(str(FCSTD_PATH))
    Part.export(solids, str(STEP_PATH))
    write_table_csv(VERIFY_CSV, rows)
    render_preview(grid_rows)
    return {
        "doc_name": name,
        "rows": rows,
        "grid_rows": grid_rows,
        "width_info": width_info,
        "shell_mode_info": shell_mode_info,
        "removed_seed_objects": removed,
        "solid_count": len(solids),
    }


def write_model_gate(payload: dict[str, Any]) -> dict[str, Any]:
    rows = payload["rows"]
    grid_rows = payload["grid_rows"]
    counts: dict[str, int] = {}
    for row in rows:
        counts[row["type"]] = counts.get(row["type"], 0) + 1
    checks: list[dict[str, Any]] = []
    add_check(checks, "row_units_sum_12", sum(ROW_UNITS) == 12, ROW_UNITS, "sum 12")
    add_check(checks, "step_exists_nontrivial", STEP_PATH.exists() and STEP_PATH.stat().st_size > 10_000_000, STEP_PATH.stat().st_size if STEP_PATH.exists() else 0, "> 10 MB")
    add_check(checks, "fcstd_exists_nontrivial", FCSTD_PATH.exists() and FCSTD_PATH.stat().st_size > 1_000_000, FCSTD_PATH.stat().st_size if FCSTD_PATH.exists() else 0, "> 1 MB")
    add_check(checks, "door_module_count_6", counts.get("door_module", 0) == 6, counts.get("door_module", 0), 6)
    add_check(checks, "hardware_template_count_54", counts.get("door_hardware_step_repositioned", 0) == 54, counts.get("door_hardware_step_repositioned", 0), 54)
    add_check(checks, "shelf_weld_count_4", counts.get("shelf_weld", 0) == 4, counts.get("shelf_weld", 0), 4)
    add_check(checks, "door_frame_horizontal_count_4", counts.get("door_frame_horizontal", 0) == 4, counts.get("door_frame_horizontal", 0), 4)
    add_check(checks, "fixed_vertical_structure_count_4", counts.get("fixed_center_vertical_structure", 0) == 4, counts.get("fixed_center_vertical_structure", 0), 4)
    add_check(checks, "preview_exists", PREVIEW_PNG.exists() and PREVIEW_PNG.stat().st_size > 10_000, PREVIEW_PNG.stat().st_size if PREVIEW_PNG.exists() else 0, "> 10 KB")
    expected_labels = [label_for_units(unit) for unit in ROW_UNITS]
    for col in ("L", "R"):
        door_rows = [row for row in rows if row.get("type") == "door_module" and row.get("column") == col]
        door_rows.sort(key=lambda row: int(row["row"]))
        add_check(checks, f"{col}_row_order", [row["size_label"] for row in door_rows] == expected_labels, [row["size_label"] for row in door_rows], expected_labels)
        for row, grid in zip(door_rows, grid_rows):
            add_check(checks, f"{col}_row_{row['row']}_door_width_W337", abs(float(row["door_width"]) - 337.0) <= TOL, row["door_width"], 337.0)
            add_check(checks, f"{col}_row_{row['row']}_door_height", abs(float(row["door_height"]) - float(grid["height"])) <= TOL, row["door_height"], rounded(grid["height"]))
            add_check(checks, f"{col}_row_{row['row']}_y_min", abs(float(row["y_min"]) - float(grid["y_min"])) <= TOL, row["y_min"], rounded(grid["y_min"]))
            add_check(checks, f"{col}_row_{row['row']}_y_max", abs(float(row["y_max"]) - float(grid["y_max"])) <= TOL, row["y_max"], rounded(grid["y_max"]))
    for lower, upper in zip(grid_rows, grid_rows[1:]):
        gap = float(upper["y_min"]) - float(lower["y_max"])
        add_check(checks, f"gap_after_row_{lower['index']}_7mm", abs(gap - 7.0) <= TOL, rounded(gap), "2 + 3 + 2 = 7")

    failed = [check for check in checks if not check["ok"] and check["severity"] == "error"]
    gate = {
        "generated_at": now_local_iso(),
        "status": "PASS" if not failed else "FAIL",
        "candidate": f"16029 800W x 1917H x 550D / {VARIANT_TOKEN.upper()} / {row_sequence_text()} / W337",
        "source_policy": "Gold/source SolidWorks STEP shell, shelf/frame STEP, validated hardware STEP templates, and executable row-stack rule. No block-placeholder model.",
        "outputs": {
            "model_dir": str(MODEL_DIR),
            "step": str(STEP_PATH),
            "fcstd": str(FCSTD_PATH),
            "verify_csv": str(VERIFY_CSV),
            "report": str(REPORT_MD),
            "preview": str(PREVIEW_PNG),
        },
        "inventory": {
            "row_units_bottom_to_top": ROW_UNITS,
            "row_sequence": row_sequence_text(),
            "solid_count": payload["solid_count"],
            "removed_seed_objects": len(payload["removed_seed_objects"]),
            "component_type_counts": counts,
            "width_info": payload["width_info"],
            "shell_mode_info": payload["shell_mode_info"],
        },
        "checks_total": len(checks),
        "checks_failed": len(failed),
        "checks": checks,
    }
    MODEL_GATE_JSON.write_text(json.dumps(gate, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with MODEL_GATE_CSV.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["name", "ok", "actual", "expected", "severity"])
        writer.writeheader()
        writer.writerows(checks)
    lines = [
        f"# 16029 800W {VARIANT_TOKEN.upper()} Gold Variable Model Gate",
        "",
        f"- Status: {gate['status']}",
        f"- Candidate: {gate['candidate']}",
        f"- STEP: `{STEP_PATH}`",
        f"- FCStd: `{FCSTD_PATH}`",
        f"- Checks: {gate['checks_total']}",
        f"- Failed: {gate['checks_failed']}",
        f"- Solid count: {payload['solid_count']}",
        f"- Row sequence: {row_sequence_text()}",
        "",
        "## Checks",
        "",
    ]
    for check in checks:
        lines.append(f"- {'PASS' if check['ok'] else 'FAIL'} `{check['name']}`: actual `{check['actual']}` expected `{check['expected']}`")
    MODEL_GATE_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")
    REPORT_MD.write_text(
        "\n".join(
            [
                f"# 16029 800W {VARIANT_TOKEN.upper()} Gold Variable Model",
                "",
                f"- STEP: `{STEP_PATH}`",
                f"- FCStd: `{FCSTD_PATH}`",
                f"- Verify CSV: `{VERIFY_CSV}`",
                f"- Preview: `{PREVIEW_PNG}`",
                f"- Candidate: 800W x 1917H x 550D / {row_sequence_text()} / W337",
                f"- Source policy: {gate['source_policy']}",
                f"- Solid count: {payload['solid_count']}",
                f"- Removed seed objects from standard source STEP: {len(payload['removed_seed_objects'])}",
                "",
                "Rule hierarchy:",
                "1. Cabinet datum fixes outer frame, center band and full cabinet coordinate system.",
                "2. Row-stack contract drives each door height and vertical position.",
                "3. Door-local coordinate drives folded panel, lock, hinge and hardware references.",
                "4. Adjacent door boundary drives shelf weldments and front-frame horizontal dividers.",
                "",
                "Boundary:",
                "- Review geometry, not production drawing release.",
                "- Height is fixed at 1917H for this review line.",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    return gate


def main() -> int:
    payload = build_model()
    gate = write_model_gate(payload)
    print(json.dumps({"status": gate["status"], "checks_total": gate["checks_total"], "checks_failed": gate["checks_failed"], "step": str(STEP_PATH), "preview": str(PREVIEW_PNG)}, ensure_ascii=False, indent=2))
    return 0 if gate["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
