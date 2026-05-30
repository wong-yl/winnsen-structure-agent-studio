from __future__ import annotations

import csv
import json
import os
import shutil
import zipfile
from collections import Counter
from datetime import datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
IN_PLACE_REFRESH = os.environ.get("LOCKER_FINALIZE_IN_PLACE", "").strip().lower() in {"1", "true", "yes"}


VARIANTS = [
    {
        "token": "lms",
        "label": "LMS",
        "row_units": [6, 4, 2],
        "row_sequence": "large 6/12, medium 4/12, small 2/12",
        "model_dir": "FC-16029-800W-1917H-550D-LMS-GOLD-VARIABLE-20260528",
        "stem": "candidate_16029_800W_1917H_550D_lms_6door_gold_variable",
        "handoff": "16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528",
    },
    {
        "token": "sml",
        "label": "SML",
        "row_units": [2, 4, 6],
        "row_sequence": "small 2/12, medium 4/12, large 6/12",
        "model_dir": "FC-16029-800W-1917H-550D-SML-GOLD-VARIABLE-20260528",
        "stem": "candidate_16029_800W_1917H_550D_sml_6door_gold_variable",
        "handoff": "16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528",
    },
]


def now_iso() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def file_size(path: Path) -> int:
    return path.stat().st_size if path.exists() else 0


def approx(actual: float, expected: float, tolerance: float = 0.2) -> bool:
    return abs(float(actual) - expected) <= tolerance


def collect_type_counts(verify_csv: Path) -> Counter:
    counts: Counter = Counter()
    with verify_csv.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            counts[row.get("type", "")] += 1
    return counts


def bbox_context(bbox: dict) -> dict:
    dims = bbox["assemblyBBoxMm"]
    objects = bbox.get("objects", [])
    min_y = float(dims["minY"])
    max_y = float(dims["maxY"])
    max_z = float(dims["maxZ"])
    nominal_body_span_present = any(
        approx(obj.get("bbox_min_y_mm", 99999), 0.0)
        and approx(obj.get("bbox_max_y_mm", -99999), 1917.0)
        for obj in objects
    )
    bottom_extension_mm = round(abs(min_y), 3) if min_y < 0 else 0.0
    top_extension_mm = round(max(0.0, max_y - 1917.0), 3)
    front_extension_mm = round(max(0.0, max_z), 3)
    context_note = (
        "Nominal cabinet body is 800W x 1917H x 550D. Raw STEP bbox includes "
        f"bottom feet/reference extension {bottom_extension_mm}mm below Y=0, "
        f"top hardware/reference extension {top_extension_mm}mm above Y=1917, "
        f"and front-side hardware/reference extension {front_extension_mm}mm beyond nominal depth."
    )
    return {
        "nominal_body_size_mm": {"width": 800.0, "height": 1917.0, "depth": 550.0},
        "nominal_body_y_span_present": nominal_body_span_present,
        "raw_step_assembly_bbox_mm": dims,
        "bottom_extension_below_body_mm": bottom_extension_mm,
        "top_extension_above_body_mm": top_extension_mm,
        "front_extension_beyond_depth_mm": front_extension_mm,
        "engineer_review_note": context_note,
    }


def write_gate_files(prefix: Path, title: str, checks: list[dict], extra: dict) -> dict:
    failed = [check for check in checks if not check["ok"]]
    gate = {
        "generated_at": now_iso(),
        "status": "PASS" if not failed else "FAIL",
        "checks_total": len(checks),
        "checks_failed": len(failed),
        **extra,
        "checks": checks,
    }
    write_json(prefix.with_suffix(".json"), gate)

    with prefix.with_suffix(".csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["name", "ok", "actual", "expected", "severity"])
        writer.writeheader()
        for check in checks:
            writer.writerow(check)

    lines = [
        f"# {title}",
        "",
        f"- Status: {gate['status']}",
        f"- Checks: {len(checks)}",
        f"- Failed: {len(failed)}",
    ]
    if "assembly_bbox_mm" in extra:
        bbox = extra["assembly_bbox_mm"]
        lines.append(
            f"- Assembly bbox: {bbox['sizeX']} x {bbox['sizeY']} x {bbox['sizeZ']} mm"
        )
    if "bbox_context" in extra:
        lines.append(f"- BBox context: {extra['bbox_context']['engineer_review_note']}")
    lines.extend(["", "## Checks", ""])
    for check in checks:
        status = "PASS" if check["ok"] else "FAIL"
        lines.append(
            f"- {status} `{check['name']}`: actual `{check['actual']}` expected `{check['expected']}`"
        )
    prefix.with_suffix(".md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return gate


def validate_variant(variant: dict) -> dict:
    token = variant["token"]
    stem = variant["stem"]
    model_dir = ROOT / "workers" / "generated_models" / variant["model_dir"]
    paths = {
        "step": model_dir / f"{stem}.step",
        "fcstd": model_dir / f"{stem}.FCStd",
        "verify_csv": model_dir / f"{stem}_verify.csv",
        "report": model_dir / f"{stem}_report.md",
        "preview": model_dir / f"{stem}_front_self_review.png",
        "bbox_csv": model_dir / f"{stem}_step_bbox.csv",
        "bbox_json": model_dir / f"{stem}_step_bbox.json",
        "bbox_md": model_dir / f"{stem}_step_bbox.md",
        "model_gate_json": ROOT / "data" / f"locker_16029_800w_{token}_gold_variable_model_gate.json",
        "model_gate_csv": ROOT / "data" / f"locker_16029_800w_{token}_gold_variable_model_gate.csv",
        "model_gate_md": ROOT / "data" / f"locker_16029_800w_{token}_gold_variable_model_gate.md",
    }
    missing = [name for name, path in paths.items() if not path.exists()]
    if missing:
        raise FileNotFoundError(f"{variant['label']} missing files: {missing}")

    model_gate = read_json(paths["model_gate_json"])
    bbox = read_json(paths["bbox_json"])
    bbox_dims = bbox["assemblyBBoxMm"]
    bbox_ctx = bbox_context(bbox)
    type_counts = collect_type_counts(paths["verify_csv"])

    model_checks = [
        {
            "name": "model_gate_pass",
            "ok": model_gate["status"] == "PASS" and model_gate["checks_failed"] == 0,
            "actual": f"{model_gate['status']} failed={model_gate['checks_failed']}",
            "expected": "PASS failed=0",
            "severity": "error",
        },
        {
            "name": "step_size_nontrivial",
            "ok": file_size(paths["step"]) > 10_000_000,
            "actual": file_size(paths["step"]),
            "expected": "> 10000000 bytes",
            "severity": "error",
        },
        {
            "name": "preview_size_nontrivial",
            "ok": file_size(paths["preview"]) > 10_000,
            "actual": file_size(paths["preview"]),
            "expected": "> 10000 bytes",
            "severity": "error",
        },
        {
            "name": "door_module_count_6",
            "ok": type_counts["door_module"] == 6,
            "actual": type_counts["door_module"],
            "expected": 6,
            "severity": "error",
        },
        {
            "name": "hardware_template_count_54",
            "ok": type_counts["door_hardware_step_repositioned"] == 54,
            "actual": type_counts["door_hardware_step_repositioned"],
            "expected": 54,
            "severity": "error",
        },
        {
            "name": "shelf_weld_count_4",
            "ok": type_counts["shelf_weld"] == 4,
            "actual": type_counts["shelf_weld"],
            "expected": 4,
            "severity": "error",
        },
        {
            "name": "door_frame_horizontal_count_4",
            "ok": type_counts["door_frame_horizontal"] == 4,
            "actual": type_counts["door_frame_horizontal"],
            "expected": 4,
            "severity": "error",
        },
        {
            "name": "row_units_match",
            "ok": model_gate["inventory"]["row_units_bottom_to_top"] == variant["row_units"],
            "actual": model_gate["inventory"]["row_units_bottom_to_top"],
            "expected": variant["row_units"],
            "severity": "error",
        },
    ]

    model_prefix = ROOT / "data" / f"locker_16029_800w_{token}_gold_variable_handoff_gate"
    model_handoff_gate = write_gate_files(
        model_prefix,
        f"16029 800W {variant['label']} Gold Variable Handoff Gate",
        model_checks,
        {
            "candidate": f"16029 800W x 1917H x 550D / {variant['label']} / {variant['row_sequence']} / W337",
            "source_policy": model_gate["source_policy"],
            "model_dir": str(model_dir),
            "type_counts": dict(type_counts),
        },
    )

    bbox_checks = [
        {
            "name": "bbox_object_count_nontrivial",
            "ok": bbox["objectCount"] >= 250,
            "actual": bbox["objectCount"],
            "expected": ">= 250",
            "severity": "error",
        },
        {
            "name": "bbox_skipped_object_count_bounded",
            "ok": bbox["skippedObjectCount"] <= 10,
            "actual": bbox["skippedObjectCount"],
            "expected": "<= 10",
            "severity": "warning",
        },
        {
            "name": "bbox_width_800",
            "ok": approx(bbox_dims["sizeX"], 800.0),
            "actual": bbox_dims["sizeX"],
            "expected": "800.0 +/- 0.2",
            "severity": "error",
        },
        {
            "name": "bbox_depth_550_with_sheetmetal_allowance",
            "ok": 548.0 <= float(bbox_dims["sizeZ"]) <= 556.0,
            "actual": bbox_dims["sizeZ"],
            "expected": "548..556",
            "severity": "error",
        },
        {
            "name": "bbox_height_contains_1917_body_and_references",
            "ok": 1900.0 <= float(bbox_dims["sizeY"]) <= 2000.0,
            "actual": bbox_dims["sizeY"],
            "expected": "1900..2000",
            "severity": "error",
        },
        {
            "name": "bbox_minmax_centered_width",
            "ok": approx(bbox_dims["minX"], -400.0) and approx(bbox_dims["maxX"], 400.0),
            "actual": [bbox_dims["minX"], bbox_dims["maxX"]],
            "expected": [-400.0, 400.0],
            "severity": "error",
        },
        {
            "name": "bbox_nominal_1917_body_span_present",
            "ok": bbox_ctx["nominal_body_y_span_present"],
            "actual": bbox_ctx["nominal_body_y_span_present"],
            "expected": "a real object spans Y=0..1917mm",
            "severity": "error",
        },
        {
            "name": "bbox_bottom_feet_extension_expected",
            "ok": approx(bbox_ctx["bottom_extension_below_body_mm"], 60.0, 0.5),
            "actual": bbox_ctx["bottom_extension_below_body_mm"],
            "expected": "60mm below nominal body",
            "severity": "error",
        },
        {
            "name": "bbox_top_reference_extension_bounded",
            "ok": 0.0 <= float(bbox_ctx["top_extension_above_body_mm"]) <= 10.0,
            "actual": bbox_ctx["top_extension_above_body_mm"],
            "expected": "0..10mm above nominal body",
            "severity": "error",
        },
        {
            "name": "bbox_front_hardware_extension_bounded",
            "ok": 0.0 <= float(bbox_ctx["front_extension_beyond_depth_mm"]) <= 5.0,
            "actual": bbox_ctx["front_extension_beyond_depth_mm"],
            "expected": "0..5mm beyond nominal depth",
            "severity": "error",
        },
    ]

    bbox_prefix = ROOT / "data" / f"locker_16029_800w_{token}_gold_variable_step_bbox_gate"
    bbox_gate = write_gate_files(
        bbox_prefix,
        f"16029 800W {variant['label']} Gold Variable STEP BBox Gate",
        bbox_checks,
        {
            "candidate": f"16029 800W x 1917H x 550D / {variant['label']} / {variant['row_sequence']} / W337",
            "source_step": str(paths["step"]),
            "object_count": bbox["objectCount"],
            "skipped_object_count": bbox["skippedObjectCount"],
            "assembly_bbox_mm": bbox_dims,
            "bbox_context": bbox_ctx,
        },
    )
    paths.update(
        {
            "handoff_gate_json": model_prefix.with_suffix(".json"),
            "handoff_gate_csv": model_prefix.with_suffix(".csv"),
            "handoff_gate_md": model_prefix.with_suffix(".md"),
            "bbox_gate_json": bbox_prefix.with_suffix(".json"),
            "bbox_gate_csv": bbox_prefix.with_suffix(".csv"),
            "bbox_gate_md": bbox_prefix.with_suffix(".md"),
        }
    )

    if model_handoff_gate["status"] != "PASS" or bbox_gate["status"] != "PASS":
        raise RuntimeError(f"{variant['label']} handoff validation failed")

    return {
        "variant": variant,
        "model_dir": model_dir,
        "paths": paths,
        "model_gate": model_gate,
        "bbox_gate": bbox_gate,
    }


def copy_file(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)


def write_variant_brief(target: Path, result: dict) -> None:
    variant = result["variant"]
    stem = variant["stem"]
    bbox = result["bbox_gate"]["assembly_bbox_mm"]
    bbox_ctx = result["bbox_gate"]["bbox_context"]
    text = f"""# 16029 800W {variant['label']} Gold Variable Review Brief

Review target:

- Product: 16029 indoor locker
- Nominal cabinet body: 800W x 1917H x 550D
- Raw STEP assembly bbox: {bbox['sizeX']} x {bbox['sizeY']} x {bbox['sizeZ']} mm
- BBox context: {bbox_ctx['engineer_review_note']}
- Door layout: 2 columns x 3 rows, 6 doors total
- Door sequence bottom-to-top: {variant['row_sequence']}
- Per-column door clear width: W337
- Height rule: one unit = 152.5mm; door installed height = units * 152.5 - 7
- Internal row gap: 2 + 3 + 2 = 7mm
- Top/bottom clearances: 2mm

Source policy:

- Generated from the standard/gold SolidWorks STEP shell, shelf weldment STEP, front-frame STEP, and validated door hardware STEP templates.
- It is still a review candidate, not a production release drawing package.
- Current CAD mainline: SolidWorks 2020.

Open first:

- `step_review/{stem}.step`

Evidence:

- Model gate: `evidence/data/locker_16029_800w_{variant['token']}_gold_variable_model_gate.md`
- Handoff gate: `evidence/data/locker_16029_800w_{variant['token']}_gold_variable_handoff_gate.md`
- STEP bbox gate: `evidence/data/locker_16029_800w_{variant['token']}_gold_variable_step_bbox_gate.md`
- STEP bbox evidence: `evidence/{stem}_step_bbox.md`
- Self-review preview: `step_review/{stem}_front_self_review.png`

Current automated results:

- Model gate: PASS, {result['model_gate']['checks_total']} checks, 0 failed
- Handoff gate: PASS, {result['model_gate']['checks_total']} source model checks already included plus package validation
- STEP bbox gate: PASS, {result['bbox_gate']['checks_total']} checks, 0 failed
- STEP object count: {result['bbox_gate']['object_count']}
- STEP assembly bbox: {bbox['sizeX']} x {bbox['sizeY']} x {bbox['sizeZ']} mm

Engineer signoff focus:

- Confirm whether production/customer size should use nominal body envelope 800W x 1917H x 550D or installed envelope including feet/top/front protrusions.
- Confirm lock cut shape against DXF/drawing semantics; current CSV keeps the lock-hole center as a reference, not a production cut approval.
- Confirm electric lock hook and U-lock hook pad placement against SolidWorks mate-level evidence before production release.
- Confirm hinge-side holes/bushings retain sufficient edge distance and assembly clearance after coating.
"""
    target.write_text(text, encoding="utf-8")


def make_zip(source_dir: Path, zip_path: Path, overwrite: bool = False) -> None:
    target_path = zip_path
    if zip_path.exists() and not overwrite:
        raise FileExistsError(f"Refusing to overwrite existing zip: {zip_path}")
    if overwrite:
        timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
        target_path = zip_path.with_name(f"{zip_path.stem}.tmp-{timestamp}{zip_path.suffix}")
    with zipfile.ZipFile(target_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(source_dir.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(source_dir.parent))
    if overwrite:
        target_path.replace(zip_path)


def package_variant(result: dict) -> Path:
    variant = result["variant"]
    stem = variant["stem"]
    handoff_dir = ROOT / "workers" / "handoffs" / variant["handoff"]
    zip_path = handoff_dir.with_suffix(".zip")
    if IN_PLACE_REFRESH:
        handoff_dir.mkdir(parents=True, exist_ok=True)
    else:
        backup_existing(handoff_dir)
        backup_existing(zip_path)

    paths = result["paths"]
    copy_file(paths["step"], handoff_dir / "step_review" / f"{stem}.step")
    copy_file(paths["preview"], handoff_dir / "step_review" / f"{stem}_front_self_review.png")
    copy_file(paths["verify_csv"], handoff_dir / "evidence" / f"{stem}_verify.csv")
    copy_file(paths["report"], handoff_dir / "evidence" / f"{stem}_report.md")
    copy_file(paths["bbox_csv"], handoff_dir / "evidence" / f"{stem}_step_bbox.csv")
    copy_file(paths["bbox_json"], handoff_dir / "evidence" / f"{stem}_step_bbox.json")
    copy_file(paths["bbox_md"], handoff_dir / "evidence" / f"{stem}_step_bbox.md")

    for key in [
        "model_gate_json",
        "model_gate_csv",
        "model_gate_md",
        "handoff_gate_json",
        "handoff_gate_csv",
        "handoff_gate_md",
        "bbox_gate_json",
        "bbox_gate_csv",
        "bbox_gate_md",
    ]:
        copy_file(paths[key], handoff_dir / "evidence" / "data" / paths[key].name)

    write_variant_brief(handoff_dir / "ENGINEER_REVIEW_BRIEF.md", result)
    make_zip(handoff_dir, zip_path, overwrite=IN_PLACE_REFRESH)
    return zip_path


def write_dual_readme(target: Path, variant_results: list[dict], zip_paths: list[Path]) -> None:
    lines = [
        "# 16029 800W Dual Gold Variable Review",
        "",
        "This is the engineer review package for two 800W variable-door layouts.",
        "",
        "Shared rules:",
        "",
        "- Product: 16029 indoor locker",
        "- Size: 800W x 1917H x 550D",
        "- Door count: 6 total, 2 columns x 3 rows",
        "- Per-column door clear width: W337",
        "- Internal row gap: 2 + 3 + 2 = 7mm",
        "- Top/bottom clearances: 2mm",
        "",
        "Source policy:",
        "",
        "- Standard/gold SolidWorks STEP shell",
        "- Standard shelf weldment STEP",
        "- Standard front-frame STEP",
        "- Validated door hardware STEP templates",
        "- Executable row-stack rule from the 16029 records",
        "- Current CAD mainline for engineer review: SolidWorks 2020 opens the STEP files",
        "",
        "Variant folders:",
        "",
    ]
    for result, zip_path in zip(variant_results, zip_paths):
        variant = result["variant"]
        bbox = result["bbox_gate"]["assembly_bbox_mm"]
        bbox_ctx = result["bbox_gate"]["bbox_context"]
        lines.extend(
            [
                f"- `{variant['label']}/`",
                f"  - {variant['label']}: bottom-to-top {variant['row_sequence']}",
                f"  - Separate single-package zip: `{zip_path.name}`",
                f"  - Model gate: PASS, {result['model_gate']['checks_total']} checks, 0 failed",
                f"  - STEP bbox gate: PASS, {result['bbox_gate']['checks_total']} checks, 0 failed",
                f"  - STEP object count: {result['bbox_gate']['object_count']}",
                f"  - STEP assembly bbox: {bbox['sizeX']} x {bbox['sizeY']} x {bbox['sizeZ']} mm",
                f"  - BBox context: {bbox_ctx['engineer_review_note']}",
            ]
        )
    lines.extend(
        [
            "",
            "Quick previews:",
            "",
            "- `LMS_front_self_review.png`",
            "- `SML_front_self_review.png`",
            "",
            "Review note:",
            "",
            "These are review candidates for structural confirmation. They are not production release drawings.",
            "",
            "Engineer signoff focus:",
            "",
            "- Confirm nominal body envelope vs installed envelope including feet/top/front protrusions.",
            "- Confirm lock cut shape, electric lock hook, U-lock hook pad and hinge-side hole clearances before production release.",
        ]
    )
    target.write_text("\n".join(lines) + "\n", encoding="utf-8")


def backup_existing(path: Path) -> Path | None:
    if not path.exists():
        return None
    suffix = datetime.now().strftime("%Y%m%d%H%M%S")
    if path.is_dir():
        backup = path.with_name(f"{path.name}_NESTED_LEGACY_{suffix}")
    else:
        backup = path.with_name(f"{path.stem}_NESTED_LEGACY_{suffix}{path.suffix}")
    if backup.exists():
        raise FileExistsError(f"Backup path already exists: {backup}")
    shutil.move(str(path), str(backup))
    return backup


def package_dual(results: list[dict], zip_paths: list[Path]) -> Path:
    dual_dir = ROOT / "workers" / "handoffs" / "16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528"
    dual_zip = dual_dir.with_suffix(".zip")
    if IN_PLACE_REFRESH:
        dual_dir.mkdir(parents=True, exist_ok=True)
    else:
        backup_existing(dual_dir)
        backup_existing(dual_zip)
        dual_dir.mkdir(parents=True)
    for result in results:
        variant = result["variant"]
        source_dir = ROOT / "workers" / "handoffs" / variant["handoff"]
        if not source_dir.exists():
            raise FileNotFoundError(f"Variant handoff dir not found: {source_dir}")
        shutil.copytree(source_dir, dual_dir / variant["label"], dirs_exist_ok=IN_PLACE_REFRESH)
        preview = result["paths"]["preview"]
        copy_file(preview, dual_dir / f"{variant['label']}_front_self_review.png")
    write_dual_readme(dual_dir / "README.md", results, zip_paths)
    make_zip(dual_dir, dual_zip, overwrite=IN_PLACE_REFRESH)
    return dual_zip


def main() -> None:
    results = [validate_variant(variant) for variant in VARIANTS]
    zip_paths = [package_variant(result) for result in results]
    dual_zip = package_dual(results, zip_paths)
    summary = {
        "generated_at": now_iso(),
        "status": "PASS",
        "variant_zips": [str(path) for path in zip_paths],
        "dual_zip": str(dual_zip),
        "variants": [
            {
                "label": result["variant"]["label"],
                "row_sequence": result["variant"]["row_sequence"],
                "model_gate": result["model_gate"]["status"],
                "bbox_gate": result["bbox_gate"]["status"],
                "step_object_count": result["bbox_gate"]["object_count"],
                "assembly_bbox_mm": result["bbox_gate"]["assembly_bbox_mm"],
            }
            for result in results
        ],
    }
    summary_path = ROOT / "data" / "locker_16029_800w_dual_gold_variable_handoff_summary.json"
    write_json(summary_path, summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
