from __future__ import annotations

import csv
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_OUT_DIR = ROOT_DIR / "workers" / "generated_models" / "SW-NATIVE-16029-CABINET-ENRICHED-12DOOR-20260521"
OUT_DIR = Path(os.getenv("STUDIO_16029_ENRICHED_12DOOR_DIR", DEFAULT_OUT_DIR))
STEM = "native_16029_12door_cabinet_enriched_v1"

CANDIDATE_JSON_PATH = ROOT_DIR / "data" / "solidworks_16029_fixed_module_candidate_map.json"
RESULT_JSON_PATH = OUT_DIR / f"{STEM}_result.json"
BBOX_CSV_PATH = OUT_DIR / f"{STEM}_step_bbox.csv"
ASSEMBLY_PATH = OUT_DIR / f"{STEM}.SLDASM"
STEP_PATH = OUT_DIR / f"{STEM}.step"

DATA_JSON_PATH = ROOT_DIR / "data" / "solidworks_16029_enriched_12door_validation.json"
DATA_MD_PATH = ROOT_DIR / "data" / "solidworks_16029_enriched_12door_validation.md"
DATA_CSV_PATH = ROOT_DIR / "data" / "solidworks_16029_enriched_12door_validation.csv"
LOCAL_JSON_PATH = OUT_DIR / "enriched_12door_validation.json"
LOCAL_MD_PATH = OUT_DIR / "enriched_12door_validation.md"
LOCAL_CSV_PATH = OUT_DIR / "enriched_12door_validation.csv"

EXPECTED_CABINET_WIDTH_MM = 1000.0
EXPECTED_CABINET_HEIGHT_TOP_MM = 1917.0
EXPECTED_CABINET_DEPTH_MM = 550.0
TOL_MM = 0.75


def now_iso() -> str:
    return datetime.now().astimezone().replace(microsecond=0).isoformat()


def parse_float(value: str | None) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def rounded(value: float | None) -> float | None:
    return round(value, 3) if value is not None else None


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def load_bbox_rows(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for raw in reader:
            row: dict[str, Any] = dict(raw)
            for key in (
                "x_min",
                "x_max",
                "x_len",
                "y_min",
                "y_max",
                "y_len",
                "z_min",
                "z_max",
                "z_len",
                "volume",
            ):
                row[key] = parse_float(raw.get(key))
            rows.append(row)
    return rows


def finite_row(row: dict[str, Any]) -> bool:
    values = [row.get(key) for key in ("x_min", "x_max", "y_min", "y_max", "z_min", "z_max")]
    return all(isinstance(value, float) and abs(value) < 1e20 for value in values)


def union_bbox(rows: list[dict[str, Any]]) -> dict[str, float | None]:
    valid = [row for row in rows if finite_row(row) and isinstance(row.get("volume"), float) and row["volume"] > 0]
    if not valid:
        return {}
    x_min = min(float(row["x_min"]) for row in valid)
    x_max = max(float(row["x_max"]) for row in valid)
    y_min = min(float(row["y_min"]) for row in valid)
    y_max = max(float(row["y_max"]) for row in valid)
    z_min = min(float(row["z_min"]) for row in valid)
    z_max = max(float(row["z_max"]) for row in valid)
    return {
        "x_min": rounded(x_min),
        "x_max": rounded(x_max),
        "x_len": rounded(x_max - x_min),
        "y_min": rounded(y_min),
        "y_max": rounded(y_max),
        "y_len": rounded(y_max - y_min),
        "z_min": rounded(z_min),
        "z_max": rounded(z_max),
        "z_len": rounded(z_max - z_min),
    }


def bbox_match_error(expected: dict[str, Any], row: dict[str, Any]) -> float | None:
    errors: list[float] = []
    for key in ("x_min", "x_max", "y_min", "y_max", "z_min", "z_max"):
        expected_value = expected.get(key)
        row_value = row.get(key)
        if expected_value is None or row_value is None:
            return None
        errors.append(abs(float(expected_value) - float(row_value)))
    return max(errors) if errors else None


def find_bbox_match(rows: list[dict[str, Any]], expected: dict[str, Any]) -> dict[str, Any]:
    best_row: dict[str, Any] | None = None
    best_error: float | None = None
    for row in rows:
        if not finite_row(row):
            continue
        error = bbox_match_error(expected, row)
        if error is None:
            continue
        if best_error is None or error < best_error:
            best_error = error
            best_row = row
    return {
        "matched": best_error is not None and best_error <= TOL_MM,
        "max_error_mm": rounded(best_error),
        "label": best_row.get("label") if best_row else "",
        "type_id": best_row.get("type_id") if best_row else "",
    }


def add_check(checks: list[dict[str, Any]], name: str, ok: bool, actual: Any, expected: Any, severity: str = "error") -> None:
    checks.append(
        {
            "name": name,
            "ok": bool(ok),
            "actual": actual,
            "expected": expected,
            "severity": severity,
        }
    )


def build_payload() -> dict[str, Any]:
    candidate_map = load_json(CANDIDATE_JSON_PATH)
    recommended = [
        item
        for item in candidate_map["candidates"]
        if item.get("recommended_for_first_enriched_model") is True
        and item.get("placement_strategy") == "identity_transform_ready"
    ]
    result = load_json(RESULT_JSON_PATH) if RESULT_JSON_PATH.exists() else {}
    rows = load_bbox_rows(BBOX_CSV_PATH) if BBOX_CSV_PATH.exists() else []
    checks: list[dict[str, Any]] = []

    add_check(checks, "assembly_exists", ASSEMBLY_PATH.exists(), str(ASSEMBLY_PATH), "native SolidWorks assembly")
    add_check(checks, "step_exists", STEP_PATH.exists(), str(STEP_PATH), "STEP export")
    add_check(checks, "bbox_csv_exists", BBOX_CSV_PATH.exists(), str(BBOX_CSV_PATH), "FreeCAD bbox CSV")
    add_check(checks, "builder_saved", result.get("saved") is True, result.get("saved"), True)
    add_check(
        checks,
        "placement_count",
        result.get("placement_count") == len(recommended) + 1,
        result.get("placement_count"),
        len(recommended) + 1,
    )

    components = result.get("components") or []
    failed_components = [
        item
        for item in components
        if item.get("exists") is not True or item.get("added") is not True or item.get("transform_applied") is not True
    ]
    add_check(checks, "all_components_added", not failed_components, failed_components, "no failed component additions")

    combined_bbox = union_bbox(rows)
    add_check(
        checks,
        "cabinet_width_preserved",
        abs(float(combined_bbox.get("x_len") or 0) - EXPECTED_CABINET_WIDTH_MM) <= TOL_MM,
        combined_bbox.get("x_len"),
        EXPECTED_CABINET_WIDTH_MM,
    )
    add_check(
        checks,
        "cabinet_height_top_preserved",
        abs(float(combined_bbox.get("y_max") or 0) - EXPECTED_CABINET_HEIGHT_TOP_MM) <= TOL_MM,
        combined_bbox.get("y_max"),
        EXPECTED_CABINET_HEIGHT_TOP_MM,
    )
    add_check(
        checks,
        "cabinet_depth_preserved",
        abs(float(combined_bbox.get("z_len") or 0) - EXPECTED_CABINET_DEPTH_MM) <= TOL_MM,
        combined_bbox.get("z_len"),
        EXPECTED_CABINET_DEPTH_MM,
    )

    candidate_matches = []
    for candidate in recommended:
        match = find_bbox_match(rows, candidate["target_bbox_mm"])
        candidate_matches.append(
            {
                "role": candidate["role"],
                "component_path": candidate["component_path"],
                "target_bbox_mm": candidate["target_bbox_mm"],
                **match,
            }
        )
    add_check(
        checks,
        "recommended_candidate_bboxes_match",
        all(item["matched"] for item in candidate_matches),
        f"{sum(1 for item in candidate_matches if item['matched'])}/{len(candidate_matches)} matched",
        f"{len(recommended)} candidate bboxes match within {TOL_MM} mm",
    )

    ok = all(item["ok"] or item["severity"] != "error" for item in checks)
    return {
        "generated_at": now_iso(),
        "ok": ok,
        "out_dir": str(OUT_DIR),
        "assembly": str(ASSEMBLY_PATH),
        "step": str(STEP_PATH),
        "bbox_csv": str(BBOX_CSV_PATH),
        "recommended_candidate_count": len(recommended),
        "combined_bbox_mm": combined_bbox,
        "candidate_matches": candidate_matches,
        "checks": checks,
    }


def write_outputs(payload: dict[str, Any]) -> None:
    for path in (DATA_JSON_PATH, LOCAL_JSON_PATH):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    fieldnames = ["name", "ok", "actual", "expected", "severity"]
    for path in (DATA_CSV_PATH, LOCAL_CSV_PATH):
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=fieldnames)
            writer.writeheader()
            for check in payload["checks"]:
                writer.writerow({key: check.get(key) for key in fieldnames})

    lines = [
        "# 16029 Enriched 12-Door Validation",
        "",
        f"- Generated at: `{payload['generated_at']}`",
        f"- Result: `{'PASS' if payload['ok'] else 'CHECK'}`",
        f"- Assembly: `{payload['assembly']}`",
        f"- STEP: `{payload['step']}`",
        f"- Recommended fixed modules placed: `{payload['recommended_candidate_count']}`",
        f"- Combined bbox: `{payload['combined_bbox_mm']}`",
        "",
        "## Checks",
        "",
        "| check | result | actual | expected |",
        "|---|---|---|---|",
    ]
    for check in payload["checks"]:
        lines.append(
            "| {name} | {result} | {actual} | {expected} |".format(
                name=check["name"],
                result="PASS" if check["ok"] else "CHECK",
                actual=str(check["actual"]).replace("|", "/"),
                expected=str(check["expected"]).replace("|", "/"),
            )
        )
    lines.append("")
    for path in (DATA_MD_PATH, LOCAL_MD_PATH):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    payload = build_payload()
    write_outputs(payload)
    print(json.dumps({"ok": payload["ok"], "checks": payload["checks"]}, ensure_ascii=False, indent=2))
    return 0 if payload["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
