from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from collect_16029_gold_sheetmetal_evidence import (
    all_floats,
    collect_entities,
    entity_bbox,
    first_float,
    first_value,
    parse_dxf,
    parse_pairs,
)


ROOT = Path(__file__).resolve().parents[1]
ATTEMPTS_ROOT = ROOT / "data" / "native_model_requests" / "attempts"
SOURCE_DIR = (
    ROOT
    / "workers"
    / "analysis"
    / "desktop_reference"
    / "16029_金标准原始素材_U盘_20260526"
    / "2.钣金展开图"
)
OUTPUT_NAME = "topcover_native_rebuild_gold_dxf_capture.json"

EXPECTED_FILES = {
    "上盖壳体左侧板.sldprt": (
        "上盖壳体左侧板展开图.DXF",
        "8160ECC4E9051BD541929C1F4C73A4DCF5B747AB666A05111CA9A8705806287A",
    ),
    "上盖壳体右侧板.SLDPRT": (
        "上盖壳体右侧板展开图.DXF",
        "DE8FF1A6A1C99E537310769BB04A684BAFAAA7E15C225B331E563F130EF41D26",
    ),
    "上盖壳体前侧板.sldprt": (
        "上盖壳体前侧板展开图.DXF",
        "1AC67CA04FD2F95AC32CB809E199220C9D269F3B72C705AE57542C1CA5041DD1",
    ),
    "上盖壳体后侧板.sldprt": (
        "上盖壳体后侧板展开图.DXF",
        "7D7415A4E157D96DCC265430D7C803354C7657C00554D1134890E958FE937459",
    ),
    "上盖壳体底板.sldprt": (
        "上盖壳体底板展开图.DXF",
        "BAD01389D1EA57CCC75F555DA330F5C09DFCDB3F39C2E78E4B84F66111BD131D",
    ),
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def stable_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def is_under(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def path_chain_has_symlink(path: Path, stop: Path) -> bool:
    current = path
    while True:
        if current.is_symlink():
            return True
        if current == stop:
            return False
        if current.parent == current:
            return True
        current = current.parent


def source_file_gate(path: Path, expected_sha256: str) -> None:
    resolved_source = SOURCE_DIR.resolve(strict=True)
    resolved = path.resolve(strict=True)
    if resolved.parent != resolved_source:
        raise RuntimeError(f"DXF escaped the fixed gold source directory: {path}")
    if path_chain_has_symlink(resolved, ROOT.resolve(strict=True)):
        raise RuntimeError(f"DXF path contains a symlink: {path}")
    stat = resolved.stat()
    if not resolved.is_file() or stat.st_nlink != 1:
        raise RuntimeError(f"DXF is missing or linked: {path}")
    actual = sha256_file(resolved)
    if actual != expected_sha256:
        raise RuntimeError(f"DXF SHA-256 drifted: {path.name}: {actual}")


def point(groups: list[tuple[str, str]], x: str, y: str, z: str) -> list[float] | None:
    values = [first_float(groups, x), first_float(groups, y), first_float(groups, z)]
    if values[0] is None or values[1] is None:
        return None
    return [float(values[0]), float(values[1]), float(values[2] or 0.0)]


def geometry_for(entity_type: str, groups: list[tuple[str, str]]) -> dict[str, Any]:
    if entity_type == "LINE":
        return {
            "startMm": point(groups, "10", "20", "30"),
            "endMm": point(groups, "11", "21", "31"),
        }
    if entity_type == "CIRCLE":
        return {
            "centerMm": point(groups, "10", "20", "30"),
            "radiusMm": first_float(groups, "40"),
        }
    if entity_type == "ARC":
        return {
            "centerMm": point(groups, "10", "20", "30"),
            "radiusMm": first_float(groups, "40"),
            "startDegrees": first_float(groups, "50"),
            "endDegrees": first_float(groups, "51"),
        }
    if entity_type in {"LWPOLYLINE", "POLYLINE", "VERTEX"}:
        xs = all_floats(groups, "10")
        ys = all_floats(groups, "20")
        return {
            "pointsMm": [[float(x), float(y), 0.0] for x, y in zip(xs, ys)],
            "bulges": all_floats(groups, "42"),
            "flag": int(first_float(groups, "70") or 0),
        }
    if entity_type == "INSERT":
        return {
            "blockName": first_value(groups, "2", ""),
            "insertionMm": point(groups, "10", "20", "30"),
            "scale": [
                float(first_float(groups, "41") or 1.0),
                float(first_float(groups, "42") or 1.0),
                float(first_float(groups, "43") or 1.0),
            ],
            "rotationDegrees": float(first_float(groups, "50") or 0.0),
        }
    return {}


def capture_file(part_name: str, file_name: str, expected_sha256: str) -> dict[str, Any]:
    path = SOURCE_DIR / file_name
    source_file_gate(path, expected_sha256)
    hash_before = sha256_file(path)
    pairs, encoding = parse_pairs(path)
    entities = collect_entities(pairs)
    summary = parse_dxf(path)
    records: list[dict[str, Any]] = []
    for index, entity in enumerate(entities):
        entity_type = str(entity["type"])
        groups = [(str(code), str(value)) for code, value in entity["groups"]]
        box, details = entity_bbox(entity_type, groups)
        records.append(
            {
                "index": index,
                "type": entity_type,
                "handle": first_value(groups, "5", ""),
                "layer": first_value(groups, "8", "0"),
                "lineType": first_value(groups, "6", ""),
                "space": int(first_float(groups, "67") or 0),
                "geometry": geometry_for(entity_type, groups),
                "bboxMm": box.to_json(),
                "details": details,
                "rawGroups": [[code, value] for code, value in groups],
            }
        )
    hash_after = sha256_file(path)
    supported = {"LINE", "CIRCLE", "ARC"}
    model_records = [record for record in records if record["space"] == 0]
    complete = bool(records) and summary.get("status") == "parsed" and all(
        record["type"] in supported and bool(record["geometry"])
        for record in model_records
    )
    return {
        "partName": part_name,
        "fileName": file_name,
        "relativePath": str(path.relative_to(ROOT)).replace("\\", "/"),
        "sizeBytes": path.stat().st_size,
        "sha256Before": hash_before,
        "sha256After": hash_after,
        "sourceUnchanged": hash_before == hash_after == expected_sha256,
        "encoding": encoding,
        "pairCount": len(pairs),
        "entityCount": len(records),
        "modelSpaceEntityCount": len(model_records),
        "entityTypeCounts": summary.get("entity_type_counts", {}),
        "manufacturingBboxMm": summary.get("manufacturing_bbox_mm"),
        "holeCandidates": summary.get("hole_candidates", []),
        "qualityFlags": summary.get("quality_flags", []),
        "needsLoopReconstruction": "needs_closed_loop_rebuild" in summary.get("quality_flags", []),
        "canonicalGeometrySha256": hashlib.sha256(
            stable_json(records).encode("utf-8")
        ).hexdigest().upper(),
        "entities": records,
        "complete": complete,
    }


def build_capture() -> dict[str, Any]:
    resolved_source = SOURCE_DIR.resolve(strict=True)
    if path_chain_has_symlink(resolved_source, ROOT.resolve(strict=True)):
        raise RuntimeError("fixed gold DXF source path contains a symlink")
    files = [
        capture_file(part_name, file_name, expected_sha256)
        for part_name, (file_name, expected_sha256) in EXPECTED_FILES.items()
    ]
    capture_pass = len(files) == 5 and all(
        row["complete"] and row["sourceUnchanged"] for row in files
    )
    return {
        "schema": "winnsen.16029.topcover_native_rebuild_gold_dxf_capture.v1",
        "probeOnly": True,
        "consumable": False,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "sourceRoot": str(resolved_source),
        "exactFive": True,
        "files": files,
        "sourceFilesUnchanged": all(row["sourceUnchanged"] for row in files),
        "capturePass": capture_pass,
        "qualityBoundary": (
            "This is lossless DXF geometry evidence for native top-cover reconstruction; "
            "it does not prove a SolidWorks model complete."
        ),
    }


def write_attempt_evidence(attempt_dir: Path, capture: dict[str, Any]) -> Path:
    attempts_root = ATTEMPTS_ROOT.resolve(strict=True)
    attempt = attempt_dir.resolve(strict=True)
    if not is_under(attempt, attempts_root) or attempt == attempts_root:
        raise RuntimeError("attempt directory escaped the fixed native attempts root")
    if path_chain_has_symlink(attempt, attempts_root):
        raise RuntimeError("attempt path contains a symlink")
    evidence_dir = attempt / "evidence" / "private"
    evidence_dir.mkdir(parents=True, exist_ok=True)
    resolved_evidence = evidence_dir.resolve(strict=True)
    if not is_under(resolved_evidence, attempt) or path_chain_has_symlink(resolved_evidence, attempt):
        raise RuntimeError("private evidence directory escaped the attempt")
    output = resolved_evidence / OUTPUT_NAME
    payload = (json.dumps(capture, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_BINARY"):
        flags |= os.O_BINARY
    descriptor = os.open(output, flags, 0o600)
    try:
        os.write(descriptor, payload)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    if output.read_bytes() != payload or output.is_symlink() or output.stat().st_nlink != 1:
        raise RuntimeError("CreateNew DXF evidence reread proof failed")
    return output


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description="Capture lossless top-cover gold DXF geometry for native reconstruction."
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--verify-only", action="store_true")
    mode.add_argument("--attempt-dir")
    args = parser.parse_args()

    capture = build_capture()
    if not capture["capturePass"]:
        raise SystemExit("top-cover gold DXF capture is incomplete")
    if args.verify_only:
        summary = {
            "status": "PASS",
            "capturePass": True,
            "fileCount": len(capture["files"]),
            "entityCounts": {
                row["fileName"]: row["entityCount"] for row in capture["files"]
            },
            "canonicalGeometrySha256": {
                row["fileName"]: row["canonicalGeometrySha256"]
                for row in capture["files"]
            },
        }
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 0
    output = write_attempt_evidence(Path(args.attempt_dir), capture)
    print(
        json.dumps(
            {
                "status": "PASS",
                "capturePass": True,
                "output": str(output),
                "outputSha256": sha256_file(output),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
