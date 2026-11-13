"""Read back explicitly scoped trial DXF/PDF outputs without changing CAD files."""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path

import pdfplumber
import pypdfium2

import dxf_check as dxf


ROOT = dxf.TRIAL_ROOT
DXF_ONLY_SCOPE = "parts_dxf_only"
DRAWING_DEFERRED = "deferred_by_user_20261113"
PDF_FRAME_LINES_MM = (
    ("outer_bottom", ((20, 10), (410, 10))),
    ("outer_right", ((410, 10), (410, 287))),
    ("outer_top", ((410, 287), (20, 287))),
    ("outer_left", ((20, 287), (20, 10))),
    ("header_bottom", ((20, 258), (410, 258))),
    ("views_bottom", ((20, 55), (410, 55))),
    ("title_bottom", ((20, 25), (410, 25))),
    ("title_divider", ((205, 25), (205, 55))),
)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def output_scope(run, document, models):
    job_path, manifest_path = run / "job.json", run / "cad/outputs/output-manifest.json"
    dxf.require(job_path.is_file() and manifest_path.is_file(), "OUTPUT_SCOPE_JOB_AND_MANIFEST_REQUIRED")
    job, manifest = load(job_path), load(manifest_path)
    action = document.get("action")
    dxf.require(action in {"outputs", "outputs-dxf"} and job.get("action") == action
                and document.get("production_release") is False and job.get("production_release") is False,
                "OUTPUT_ACTION_OR_RELEASE_SCOPE_MISMATCH")
    dxf_only = action == "outputs-dxf"
    scope = DXF_ONLY_SCOPE if dxf_only else "parts_dxf_pdf"
    dxf.require(job.get("outputScope") == scope if dxf_only else job.get("outputScope", scope) == scope,
                "OUTPUT_SCOPE_MISMATCH")
    if "outputScope" in document:
        dxf.require(document["outputScope"] == scope, "NATIVE_OUTPUT_SCOPE_MISMATCH")
    dxf.require(manifest.get("schema") == "16029-variable-output-manifest/" + ("v2" if dxf_only else "v1")
                and manifest.get("complete") is True and isinstance(manifest.get("cases"), list)
                and len(manifest["cases"]) == 6, "COMPLETE_SCOPED_OUTPUT_MANIFEST_REQUIRED")
    if dxf_only:
        dxf.require(manifest.get("outputScope") == scope and manifest.get("drawingStatus") == DRAWING_DEFERRED
                    and manifest.get("production_release") is False, "DXF_ONLY_MANIFEST_SCOPE_MISMATCH")
    else:
        dxf.require(manifest.get("outputScope", scope) == scope and "drawingStatus" not in manifest,
                    "FULL_OUTPUT_MANIFEST_SCOPE_MISMATCH")
    model_paths = {str(Path(model["path"]).resolve()): model for model in models}
    dxf.require(len(model_paths) == 6, "UNIQUE_OUTPUT_MODEL_PATHS_REQUIRED")
    events = document["events"]
    scopes = [event for event in events if event.get("inspection") == "output_scope"]
    deferred = [event for event in events if event.get("inspection") == "output_drawing_deferred"]
    drawings = [event for event in events if event.get("inspection") == "output_drawing"]
    if dxf_only:
        dxf.require(len(scopes) == 1 and scopes[0].get("outputScope") == scope
                    and scopes[0].get("drawingStatus") == DRAWING_DEFERRED
                    and scopes[0].get("production_release") is False and scopes[0].get("fullPlanPass") is False
                    and events.index(scopes[0]) < min(events.index(model) for model in models),
                    "EXPLICIT_NATIVE_DXF_ONLY_SCOPE_REQUIRED")
        dxf.require(not drawings and len(deferred) == 6
                    and {str(Path(event.get("path", "")).resolve()) for event in deferred} == set(model_paths)
                    and all(event.get("reason") == DRAWING_DEFERRED for event in deferred),
                    "EXACT_SIX_MATCHING_DEFERRED_DRAWINGS_REQUIRED")
    else:
        dxf.require(not deferred and not scopes and len(drawings) == 6
                    and {str(Path(event.get("path", "")).resolve()) for event in drawings} == set(model_paths),
                    "FULL_DRAWING_OUTPUT_EVIDENCE_REQUIRED")
    native_dxfs = [event for event in events if event.get("inspection") == "output_dxf"]
    native_flats = [event for event in events if event.get("inspection") == "output_flat_preparation"]
    dxf.require(len(native_dxfs) == len(native_flats) == 6
                and {str(Path(event["path"]).resolve()) for event in native_dxfs} == set(model_paths)
                and {str(Path(event["path"]).resolve()) for event in native_flats} == set(model_paths),
                "EXACT_SIX_NATIVE_DXF_AND_FLAT_PROOFS_REQUIRED")
    by_path, root = {}, (run / "cad/outputs").resolve()
    for item in manifest["cases"]:
        dxf.require(isinstance(item, dict) and isinstance(item.get("folder"), str)
                    and isinstance(item.get("part"), str), "OUTPUT_MANIFEST_CASE_INVALID")
        part = (root / item["folder"] / item["part"]).resolve()
        dxf.require(root in part.parents and part.is_file() and str(part) in model_paths and str(part) not in by_path,
                    "OUTPUT_MANIFEST_PART_PATH_MISMATCH")
        model = model_paths[str(part)]
        dxf.require(all(item.get(key) == model[key] for key in ("hand", "widthMm", "heightMm"))
                    and digest(part) == item.get("partSha256"), "OUTPUT_MANIFEST_PART_IDENTITY_MISMATCH")
        native_dxf = next(event["result"] for event in native_dxfs if Path(event["path"]).resolve() == part)
        native_flat = next(event["result"] for event in native_flats if Path(event["path"]).resolve() == part)
        path = part.parent / "flat.dxf"
        dxf.require(item.get("dxf") == native_dxf and path.is_file()
                    and Path(native_dxf.get("path", "")).resolve() == path
                    and digest(path) == native_dxf.get("sha256"), "OUTPUT_MANIFEST_DXF_IDENTITY_MISMATCH")
        if dxf_only:
            dxf.require("drawing" in item and item["drawing"] is None and item.get("flatPreparation") == native_flat,
                        "DXF_ONLY_MANIFEST_DRAWING_OR_FLAT_CONFLICT")
        else:
            drawing = next(event["result"] for event in drawings if Path(event["path"]).resolve() == part)
            dxf.require(isinstance(item.get("drawing"), dict) and item["drawing"] == drawing,
                        "FULL_DRAWING_MANIFEST_EVIDENCE_REQUIRED")
        by_path[str(part)] = item
    dxf.require(set(by_path) == set(model_paths), "OUTPUT_MANIFEST_CASE_SET_MISMATCH")
    return {"outputScope": scope, "dxfOnly": dxf_only,
            "drawingStatus": DRAWING_DEFERRED if dxf_only else "pending_visual_review",
            "jobSha256": digest(job_path), "manifestPath": str(manifest_path),
            "manifestSha256": digest(manifest_path), "manifestCases": by_path}


def native_cut_readback(native, exported):
    """Keep source/structure exact and compare only schema-defined geometric doubles by unit."""
    result = {"wholeDictionaryEqual": native == exported, "identityExact": True,
              "nativeReadbackEquivalent": False, "readbackDifferences": [], "readbackErrors": [],
              "readbackNumericFieldsCompared": 0, "readbackToleranceMm": 1e-9,
              "readbackCurveParameterTolerance": 1e-12,
              "readbackToleranceIsManufacturingTolerance": False}

    def tolerance(path):
        if path == ("topZMm",):
            return 1e-9, "mm"
        if len(path) >= 3 and path[0] == "primitives" and isinstance(path[1], int):
            if len(path) == 3 and path[2] in {"lengthMm", "radiusMm"}:
                return 1e-9, "mm"
            if len(path) == 4 and path[2] in {"startMm", "endMm", "centerMm", "midpointMm"} and isinstance(path[3], int):
                return 1e-9, "mm"
            if len(path) == 3 and path[2] in {"parameterMin", "parameterMax"}:
                return 1e-12, "native curve parameter"
        if len(path) >= 4 and path[0] == "arcSampling" and isinstance(path[1], int):
            if len(path) == 4 and path[2] == "edgeParameterRange" and isinstance(path[3], int):
                return 1e-12, "native curve parameter"
            if len(path) == 4 and path[2] == "curveParameterDomain" and path[3] in {"minimum", "maximum"}:
                return 1e-12, "native curve parameter"
            if len(path) == 5 and path[2] == "samples" and isinstance(path[3], int) and path[4] == "parameter":
                return 1e-12, "native curve parameter"
            if len(path) == 6 and path[2] == "samples" and isinstance(path[3], int) and path[4] == "pointMm" and isinstance(path[5], int):
                return 1e-9, "mm"
        return None

    def record(path, before, after, reason, accepted=False, limit=0, units="exact"):
        def safe(value):
            return str(value) if isinstance(value, float) and not math.isfinite(value) else value
        numeric = all(type(value) in (int, float) and math.isfinite(value) for value in (before, after))
        result["readbackDifferences"].append({"path": "$" + "".join(
            "[%d]" % key if isinstance(key, int) else "." + key for key in path),
            "before": safe(before), "after": safe(after),
            "absoluteDifference": abs(before-after) if numeric else None,
            "threshold": limit, "units": units, "accepted": accepted, "reason": reason})
        if not accepted:
            result["readbackErrors"].append(reason + " " + result["readbackDifferences"][-1]["path"])

    def compare(before, after, path=()):
        allowed = tolerance(path)
        if allowed is not None:
            limit, units = allowed
            result["readbackNumericFieldsCompared"] += 1
            finite = all(type(value) in (int, float) and math.isfinite(value) for value in (before, after))
            if not finite or before != after:
                record(path, before, after, "GEOMETRIC_READBACK_DOUBLE" if finite else "FINITE_GEOMETRIC_DOUBLE_REQUIRED",
                       finite and abs(before-after) <= limit, limit, units)
        elif type(before) is not type(after):
            result["identityExact"] = False
            record(path, before, after, "EXACT_FIELD_TYPE_REQUIRED")
        elif isinstance(before, dict):
            if before.keys() != after.keys():
                result["identityExact"] = False
                record(path, sorted(before), sorted(after), "EXACT_KEY_SET_REQUIRED")
            for key in before:
                if key in after:
                    compare(before[key], after[key], (*path, key))
        elif isinstance(before, list):
            if len(before) != len(after):
                result["identityExact"] = False
                record(path, len(before), len(after), "EXACT_LIST_LENGTH_REQUIRED")
            for index, (a, b) in enumerate(zip(before, after)):
                compare(a, b, (*path, index))
        elif isinstance(before, (int, float)) and not math.isfinite(before):
            result["identityExact"] = False
            record(path, before, after, "FINITE_EXACT_NUMBER_REQUIRED")
        elif before != after:
            result["identityExact"] = False
            record(path, before, after, "EXACT_FIELD_VALUE_REQUIRED")

    compare(native, exported)
    result["nativeReadbackEquivalent"] = result["identityExact"] and not result["readbackErrors"]
    return result


def flat_cut_mapping(report, flat, model, exported):
    """Compare full CUT geometry without requiring or inventing drawing/handedness evidence."""
    result = {"verified": False, "completeCutGeometryEquivalent": False,
              "coordinateMappingUniqueInSearchedFamily": False, "candidateCount": 0,
              "searchedTransformFamily": "Eight unit-scale axis-aligned mappings, including reflections",
              "errors": [], "candidates": [], "source": "native flat geometry.cutGeometry",
              "handednessProvenByThisComparison": False, "manufacturingOrientationVerified": False,
              "dxfGeometryChanged": False, "scale": 1, "toleranceMm": 0.01,
              "wholeDictionaryEqual": False, "identityExact": False, "nativeReadbackEquivalent": False}
    try:
        native = flat["geometry"]["cutGeometry"]
        dxf.require(native.get("schema") == "native-top-z-cut-v1" and native.get("complete") is True
                    and native.get("errors") == [] and native.get("units") == "mm", "NATIVE_FLAT_CUT_INCOMPLETE")
        result.update(native_cut_readback(native, exported.get("nativeCutGeometry")))
        result["sourceIdentityChecks"] = {
            "modelPath": Path(native["modelPath"]).resolve() == Path(model["path"]).resolve(),
            "modelHand": native.get("modelHand") == model["hand"],
            "configuration": native.get("configuration") == exported.get("exportConfiguration")}
        source_identity = all(result["sourceIdentityChecks"].values())
        result["identityExact"] = result["identityExact"] and source_identity
        result["nativeReadbackEquivalent"] = result["nativeReadbackEquivalent"] and source_identity
        dxf.require(result["nativeReadbackEquivalent"], "NATIVE_FLAT_CUT_SOURCE_IDENTITY_MISMATCH")
        dxf.require(report["auditPassed"] and report["units"]["verified"] and report["units"]["insunits"] == 4,
                    "NATIVE_FLAT_CUT_REQUIRES_AUDITED_MM_DXF")
        top, tolerance = native["topZMm"], native["topToleranceMm"]
        dxf.require(math.isfinite(top) and 0 < tolerance <= 0.0001 + 1e-12, "NATIVE_FLAT_TOP_Z_FILTER_REQUIRED")
        def point(value):
            dxf.require(len(value) == 3 and all(math.isfinite(v) for v in value)
                        and abs(value[2]-top) <= tolerance, "NATIVE_FLAT_CUT_POINT_OFF_TOP_Z")
            return value[:2]
        source, target = [], []
        for item in native["primitives"]:
            kind, length = item["type"], item["lengthMm"]
            dxf.require(kind in {"LINE", "ARC", "CIRCLE"} and math.isfinite(length) and length > 0,
                        "NATIVE_FLAT_CUT_PRIMITIVE_INVALID")
            row = {"kind": kind, "length": length}
            if kind in {"LINE", "ARC"}:
                row.update(a=point(item["startMm"]), b=point(item["endMm"]))
            if kind in {"ARC", "CIRCLE"}:
                row.update(c=point(item["centerMm"]), r=item["radiusMm"])
                dxf.require(math.isfinite(row["r"]) and row["r"] > 0, "NATIVE_FLAT_CUT_RADIUS_INVALID")
            if kind == "ARC":
                row["mid"] = point(item["midpointMm"])
            source.append(row)
        for item in report["cuts"]["primitives"]:
            kind = item["kind"]
            row = {"kind": kind}
            if kind in {"LINE", "ARC"}:
                row["a"], row["b"] = dxf.ends(item)
            if kind == "LINE":
                row["length"] = math.dist(row["a"], row["b"])
            else:
                row.update(c=item["c"], r=item["r"], length=(dxf.TAU if kind == "CIRCLE" else abs(item["sweep"]))*item["r"])
                if kind == "ARC":
                    angle = item["start"]+item["sweep"]/2
                    row["mid"] = [item["c"][0]+item["r"]*math.cos(angle), item["c"][1]+item["r"]*math.sin(angle)]
            target.append(row)
        dxf.require(source and len(source) == native["boundaryEdgeCount"] == len(target), "NATIVE_FLAT_CUT_COUNT_MISMATCH")
        def center(rows):
            points = [row[key] for row in rows for key in ("a", "b", "c", "mid") if key in row]
            return [sum(p[i] for p in points)/len(points) for i in (0, 1)]
        def same(a, b):
            if a["kind"] != b["kind"] or abs(a["length"]-b["length"]) > 0.01:
                return False
            if "c" in a and (math.dist(a["c"], b["c"]) > 0.01 or abs(a["r"]-b["r"]) > 0.01):
                return False
            if "a" in a and min(max(math.dist(a["a"], b["a"]), math.dist(a["b"], b["b"])),
                                max(math.dist(a["a"], b["b"]), math.dist(a["b"], b["a"]))) > 0.01:
                return False
            return "mid" not in a or math.dist(a["mid"], b["mid"]) <= 0.01
        source_center, target_center = center(source), center(target)
        for swap in (False, True):
            for sx in (-1, 1):
                for sy in (-1, 1):
                    matrix = [[0, sx], [sy, 0]] if swap else [[sx, 0], [0, sy]]
                    rotated = dxf.rigid_point(source_center, matrix, [0, 0])
                    translation = [target_center[i]-rotated[i] for i in (0, 1)]
                    transformed = [dict(row, **{key:dxf.rigid_point(row[key], matrix, translation)
                                               for key in ("a", "b", "c", "mid") if key in row}) for row in source]
                    matches = [[i for i, other in enumerate(target) if same(row, other)] for row in transformed]
                    if all(len(row) == 1 for row in matches) and len({row[0] for row in matches}) == len(target):
                        determinant = matrix[0][0]*matrix[1][1]-matrix[0][1]*matrix[1][0]
                        result["candidates"].append({"matrix": matrix, "translationMm": translation,
                                                     "determinant": determinant, "matchedPrimitiveCount": len(source)})
        result["candidateCount"] = len(result["candidates"])
        result["completeCutGeometryEquivalent"] = result["candidateCount"] > 0
        result["coordinateMappingUniqueInSearchedFamily"] = result["candidateCount"] == 1
        dxf.require(len(result["candidates"]) == 1, "UNIQUE_COMPLETE_NATIVE_FLAT_CUT_MAPPING_REQUIRED")
        result.update(verified=True, mapping=result["candidates"][0])
    except (dxf.AuditError, KeyError, TypeError, ValueError, IndexError) as error:
        result["errors"].append(str(error))
    return result


def flat_hole_comparison(report, flat, mapping):
    native_holes = [(index,row) for index,row in enumerate(flat.get("geometry", {}).get("cutGeometry", {}).get("primitives", []))
                    if row.get("type") == "CIRCLE"]
    candidates = []
    if mapping["completeCutGeometryEquivalent"]:
        for candidate_index, transform in enumerate(mapping["candidates"]):
            rows = []
            for index,hole in native_holes:
                center = dxf.rigid_point(hole["centerMm"][:2], transform["matrix"], transform["translationMm"])
                matches = [i for i,row in enumerate(report["roundHoles"])
                           if math.dist(center,row["center"]) <= 0.01 and abs(2*hole["radiusMm"]-row["diameter"]) <= 0.01]
                rows.append({"nativePrimitiveIndex": index, "mappedNativeCenterMm": center, "matchingDxfIndices": matches})
            passed = (len(native_holes) == report["roundHoleCount"] == 2
                      and all(len(row["matchingDxfIndices"]) == 1 for row in rows)
                      and {row["matchingDxfIndices"][0] for row in rows if len(row["matchingDxfIndices"]) == 1} == set(range(report["roundHoleCount"])))
            candidates.append({"candidateIndex": candidate_index, "determinant": transform["determinant"],
                               "holes": rows, "uniqueCentersAndDiametersMatch": passed})
    equivalent = (mapping["completeCutGeometryEquivalent"] and bool(candidates)
                  and len(candidates) == mapping["candidateCount"]
                  and all(candidate["uniqueCentersAndDiametersMatch"] for candidate in candidates))
    strict = mapping["verified"] and mapping["coordinateMappingUniqueInSearchedFamily"] and equivalent
    return {"source": "complete native flat top-Z CIRCLE primitives", "nativeHoleCount": len(native_holes),
            "dxfHoleCount": report["roundHoleCount"], "expectedHoleCount": 2,
            "uniqueCentersAndDiametersMatchInFullCutMapping": strict,
            "holesEquivalentUnderAllCandidates": equivalent, "candidateComparisons": candidates,
            "coordinateMapping": mapping, "holes": candidates[0]["holes"] if strict else [], "toleranceMm": 0.01,
            "handednessProvenByThisComparison": False, "manufacturingOrientationVerified": False}


def shape(data):
    sections = dxf.section_records(dxf.pairs_from_bytes(data))
    primitives, circles, _, _, unsupported = dxf.geometry(dxf.records(sections["ENTITIES"]), 1)
    dxf.require(not unsupported, "BASELINE_UNSUPPORTED_GEOMETRY")
    box = dxf.bounds(primitives, circles)
    origin = box["min"]
    def point(p):
        return tuple(round(p[i] - origin[i], 5) for i in (0, 1))
    rows = []
    for p in primitives:
        if p["kind"] == "LINE":
            rows.append(("LINE", *sorted(point(v) for v in dxf.ends(p))))
        else:
            mid_angle = p["start"] + p["sweep"] / 2
            rows.append(("ARC", point(p["c"]), round(p["r"], 5),
                         *sorted(point(v) for v in dxf.ends(p)),
                         point([p["c"][0]+p["r"]*math.cos(mid_angle),p["c"][1]+p["r"]*math.sin(mid_angle)]),
                         round(abs(p["sweep"]), 7)))
    rows.extend(("CIRCLE", point(c["c"]), round(c["r"], 5)) for c in circles)
    return collections.Counter(rows)


def pdf_trial_frame(page):
    tolerance_mm = 0.2
    actual = []
    for index, line in enumerate(page.lines):
        points = line.get("pts", [])
        if line.get("stroke") is not True or len(points) != 2:
            continue
        endpoints = [[x * 25.4 / 72, (page.height - y) * 25.4 / 72] for x, y in points]
        if all(math.isfinite(value) for point in endpoints for value in point):
            actual.append((index, endpoints))
    rows, used = [], []
    for name, expected in PDF_FRAME_LINES_MM:
        candidates = []
        for index, endpoints in actual:
            aligned = min((endpoints, endpoints[::-1]), key=lambda points:
                          max(abs(points[i][j] - expected[i][j]) for i in (0, 1) for j in (0, 1)))
            deltas = [[aligned[i][j] - expected[i][j] for j in (0, 1)] for i in (0, 1)]
            candidates.append({"pdfLineIndex": index, "actualEndpointsMm": aligned,
                               "endpointCoordinateErrorsMm": deltas,
                               "maximumAbsoluteCoordinateErrorMm": max(abs(v) for point in deltas for v in point)})
        matches = [row for row in candidates if row["maximumAbsoluteCoordinateErrorMm"] <= tolerance_mm]
        if len(matches) == 1:
            used.append(matches[0]["pdfLineIndex"])
        rows.append({"id": name, "expectedEndpointsMm": expected, "matchingLineCount": len(matches),
                     "matches": matches, "nearestPdfLine": min(candidates, key=lambda row:
                         row["maximumAbsoluteCoordinateErrorMm"], default=None), "pass": len(matches) == 1})
    unique = len(used) == len(PDF_FRAME_LINES_MM) and len(set(used)) == len(used)
    return {"source": "pdfplumber.page.lines", "coordinateSystem": "paper millimetres from PDF bottom-left",
            "coordinateReadToleranceMm": tolerance_mm, "manufacturingTolerance": False,
            "nativeSketchEndpointsUsed": False, "expectedLineCount": len(PDF_FRAME_LINES_MM),
            "pageVectorLineCount": len(page.lines), "readableStrokedLineCount": len(actual),
            "uniquelyMatchedLineCount": len(used), "uniquePdfLines": unique, "lines": rows, "pass": unique}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    run, target = args.run.resolve(), args.out.resolve()
    dxf.require(ROOT in run.parents and run.parent.name == "runs", "TRIAL_RUN_REQUIRED")
    dxf.require(ROOT in target.parents and not target.exists(), "NEW_TRIAL_AUDIT_DIRECTORY_REQUIRED")
    for p in (run, target):
        dxf.require(not any(x.exists() and (x.is_symlink() or getattr(x.stat(follow_symlinks=False), "st_file_attributes", 0) & 0x400) for x in [p, *p.parents]), "REPARSE_PATH")
    target.mkdir(parents=True, exist_ok=False)
    document = load(run / "native-result.json")
    dxf.require(document["events"][-1].get("inspection") == "finished"
                and document["events"][-1].get("success") is True
                and document["events"][-1].get("cadExited") is True, "NATIVE_RUN_NOT_SUCCESSFULLY_FINISHED")
    models = [e for e in document["events"] if e["inspection"] == "output_case_model"]
    expected = {(hand,width,height) for hand in ("L","R") for width,height in ((437,471.25),(287,200),(537,1000))}
    dxf.require(len(models) == 6 and {(e["hand"],e["widthMm"],e["heightMm"]) for e in models} == expected,
                "EXACT_SIX_DOOR_CASES_REQUIRED")
    scope = output_scope(run, document, models)
    dxf_only = scope["dxfOnly"]
    drawings = {e["path"]: e["result"] for e in document["events"] if e["inspection"] == "output_drawing"}
    flats = {e["path"]: e["result"] for e in document["events"] if e["inspection"] == "output_flat_preparation"}
    history = ROOT.parents[1] / "review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108"
    cases = []
    for model in models:
        part = Path(model["path"])
        name, folder = part.parent.name, target / part.parent.name
        folder.mkdir()
        path = part.parent / "flat.dxf"
        if not path.exists():
            cases.append({"case": name, "error": "DXF_MISSING", "pass": False})
            continue
        baseline = model["widthMm"] == 437 and model["heightMm"] == 471.25
        report = dxf.audit(path.read_bytes(), 473.4 if baseline else None,
                           507.65 if baseline else None, 12, dimension_tolerance_mm=0.01)
        report["path"] = str(path)
        proof = None
        try:
            proof = dxf.find_native_bends(document, report["sha256"], path)
        except dxf.AuditError:
            if not dxf_only:
                raise
        comparison = (dxf.native_bend_comparison(report, proof) if proof is not None else
                      {"positionsAndDirectionsVerified": False, "nativeViewDirectionAssociationVerified": False,
                       "manufacturingOrientationVerified": False, "directionConvention": "UNKNOWN",
                       "errors": ["NATIVE_BEND_DIRECTION_EVIDENCE_REQUIRED"],
                       "reason": "Native flat-view direction proof was not generated while drawings are deferred."})
        report["nativeBendComparison"] = comparison
        report["outputScope"] = scope["outputScope"]
        report["drawingStatus"] = scope["drawingStatus"]
        report["fullPlanPass"] = False
        report["geometryAuditPassed"] = report["auditPassed"]
        flat = flats.get(model["path"], {})
        native_flat = flat.get("geometry", {})
        native_size = native_flat.get("widthHeightThicknessMm", [])
        actual_size = report["measurements"]["cutExtents"]
        native_flat_match = (native_flat.get("flatGeometryVerified") is True and len(native_size) == 3
                             and abs(actual_size["width"]-native_size[0]) <= 0.01
                             and abs(actual_size["height"]-native_size[1]) <= 0.01)
        report["nativeFlatSizeComparison"] = {"nativeWidthHeightThicknessMm": native_size,
            "actualDxfWidthHeightMm": [actual_size["width"], actual_size["height"]],
            "toleranceMm": 0.01, "pass": native_flat_match,
            "constantAllowanceUsedAsSoleEvidence": False}
        report["geometryAuditPassed"] = report["geometryAuditPassed"] and native_flat_match
        if dxf_only:
            declared = scope["manifestCases"][str(part.resolve())]
            mapping = flat_cut_mapping(dict(report, auditPassed=report["geometryAuditPassed"]), flat, model, declared["dxf"])
            report["nativeFlatCutComparison"] = mapping
            holes = flat_hole_comparison(report, flat, mapping)
            report["nativeHoleGeometryComparison"] = holes
            report["geometryAuditPassed"] = (report["geometryAuditPassed"] and mapping["completeCutGeometryEquivalent"]
                                             and holes["holesEquivalentUnderAllCandidates"])
        report["auditPassed"] = report["geometryAuditPassed"] and comparison["positionsAndDirectionsVerified"]
        report["production_release"] = False
        if baseline:
            side = "left" if model["hand"] == "L" else "right"
            certificate = history / ("door_dxf_certificate20_door_panel_" + side + "_W437_H471p25.json")
            prior = load(certificate)
            source = Path(prior["dxf"])
            historical, current = shape(source.read_bytes()), shape(path.read_bytes())
            report["historicalGeometry"] = {"certificate": str(certificate), "certificateSha256": digest(certificate),
                "path": str(source), "sha256": digest(source), "recordedSha256Matches": digest(source) == prior["dxfSha256"],
                "translationOnlyFullGeometryEqual": historical == current,
                "missingPrimitiveCount": sum((historical-current).values()), "extraPrimitiveCount": sum((current-historical).values()),
                "coordinateRoundingMm": 0.00001, "rotationMirrorScaleUsed": False}
            historical_pass = (report["historicalGeometry"]["recordedSha256Matches"]
                               and report["historicalGeometry"]["translationOnlyFullGeometryEqual"])
            report["geometryAuditPassed"] = report["geometryAuditPassed"] and historical_pass
            report["auditPassed"] = report["geometryAuditPassed"] and comparison["positionsAndDirectionsVerified"]
        drawing = drawings.get(model["path"])
        pdf_report = {"pass": False, "error": "PDF_OUTPUT_EVIDENCE_MISSING"}
        if dxf_only:
            pdf_report = {"pass": False, "structuralPass": None, "trialFramePass": None,
                          "checkSkipped": True, "drawingStatus": DRAWING_DEFERRED, "reason": DRAWING_DEFERRED}
        if drawing:
            native_holes = drawing["holes"]["roundHoles"]
            mapping_proof = comparison.get("cutCoordinateMapping")
            if mapping_proof is None:
                mapping_proof = dxf.cut_coordinate_mapping(dict(report,auditPassed=report["geometryAuditPassed"]), proof)
            mapping = mapping_proof.get("mapping")
            hole_rows = []
            if mapping_proof["verified"]:
                for hole in native_holes:
                    center = dxf.rigid_point([v*1000 for v in hole["nativeCenterMetres"][:2]],mapping["matrix"],mapping["translationMm"])
                    matches = [i for i,row in enumerate(report["roundHoles"])
                               if math.dist(center,row["center"]) <= 0.01 and abs(hole["diameterMm"]-row["diameter"]) <= 0.01]
                    hole_rows.append({"id":hole["id"],"mappedNativeCenterMm":center,"matchingDxfIndices":matches})
            hole_match = (mapping_proof["verified"] and len(native_holes) == report["roundHoleCount"]
                          and all(len(row["matchingDxfIndices"]) == 1 for row in hole_rows)
                          and len({row["matchingDxfIndices"][0] for row in hole_rows if len(row["matchingDxfIndices"]) == 1}) == len(native_holes))
            report["nativeHoleGeometryComparison"] = {"nativeHoleCount": len(native_holes), "dxfHoleCount": report["roundHoleCount"],
                "uniqueCentersAndDiametersMatchInFullCutMapping": hole_match,
                "toleranceMm": 0.01, "coordinateMapping": mapping_proof,"holes":hole_rows,
                "handednessProvenByThisComparison": False}
            report["geometryAuditPassed"] = report["geometryAuditPassed"] and hole_match
            report["auditPassed"] = report["geometryAuditPassed"] and comparison["positionsAndDirectionsVerified"]
            pdf = Path(drawing["pdfPath"])
            pages = []
            with pdfplumber.open(pdf) as reader:
                for i, page in enumerate(reader.pages):
                    text = page.extract_text() or ""
                    page_report = {"page": i+1, "widthPt": page.width, "heightPt": page.height,
                        "text": text, "charCount": len(page.chars),
                        "outOfPageCharacters": [{k: c.get(k) for k in ("text", "x0", "top", "x1", "bottom")}
                            for c in page.chars if c["x0"] < -0.5 or c["top"] < -0.5 or c["x1"] > page.width+0.5 or c["bottom"] > page.height+0.5],
                        "hasReplacementGlyph": "\ufffd" in text or "(cid:" in text}
                    page_report["a3Landscape"] = abs(page.width-420/25.4*72) < 1 and abs(page.height-297/25.4*72) < 1
                    page_report["requiredTextPresent"] = all(word in text for word in ("16029","TRIAL","2026-11-12"))
                    page_report["trialFrame"] = pdf_trial_frame(page)
                    page_report["structuralPass"] = (page_report["charCount"] > 50 and page_report["a3Landscape"]
                        and page_report["requiredTextPresent"] and not page_report["outOfPageCharacters"]
                        and not page_report["hasReplacementGlyph"] and page_report["trialFrame"]["pass"])
                    pages.append(page_report)
            native_pdf = pypdfium2.PdfDocument(str(pdf))
            try:
                for i in range(len(native_pdf)):
                    page = native_pdf[i]
                    bitmap = page.render(scale=2.0)
                    bitmap.to_pil().save(folder / ("page-%02d.png" % (i+1)))
                    bitmap.close()
                    page.close()
            finally:
                native_pdf.close()
            pdf_hash_matches = digest(pdf) == drawing["pdfSha256"]
            pdf_report = {"path": str(pdf), "sha256": digest(pdf), "matchesNativeSha256": pdf_hash_matches,
                          "pages": pages, "pageCount": len(pages),
                          "trialFramePass": len(pages) == 1 and all(p["trialFrame"]["pass"] for p in pages),
                          "structuralPass": len(pages) == 1 and pdf_hash_matches and all(p["structuralPass"] for p in pages),
                          "visualReview": "pending", "pass": False}
            (folder / "pdf-audit.json").write_text(json.dumps(pdf_report, ensure_ascii=False, indent=2), encoding="utf-8")
        (folder / "dxf-audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        cases.append({"case": name, "part": str(part), "hand": model["hand"], "widthMm": model["widthMm"], "heightMm": model["heightMm"],
                      "dxfGeometryPass": report["geometryAuditPassed"], "dxfDirectionPass": comparison["positionsAndDirectionsVerified"],
                      "extents": report["measurements"]["cutExtents"], "bendCount": report["bends"]["segmentCount"],
                      "dxfErrors": report["errors"], "pdfStructuralPass": pdf_report.get("structuralPass", False),
                      "pdfFramePass": pdf_report.get("trialFramePass", False),
                      "drawingStatus": scope["drawingStatus"], "pdfCheckSkipped": dxf_only,
                      "nativeHoleGeometryPass": report.get("nativeHoleGeometryComparison", {}).get(
                          "holesEquivalentUnderAllCandidates" if dxf_only else "uniqueCentersAndDiametersMatchInFullCutMapping", False),
                      **({"completeCutGeometryEquivalent": mapping["completeCutGeometryEquivalent"],
                          "wholeDictionaryEqual": mapping["wholeDictionaryEqual"],
                          "identityExact": mapping["identityExact"],
                          "nativeReadbackEquivalent": mapping["nativeReadbackEquivalent"],
                          "nativeReadbackDifferenceCount": len(mapping.get("readbackDifferences", [])),
                          "coordinateMappingUniqueInSearchedFamily": mapping["coordinateMappingUniqueInSearchedFamily"],
                          "cutMappingCandidateCount": mapping["candidateCount"],
                          "cutMappingDeterminants": [candidate["determinant"] for candidate in mapping["candidates"]],
                          "holesEquivalentUnderAllCandidates": holes["holesEquivalentUnderAllCandidates"]} if dxf_only else {}),
                      "handednessProvenByThisComparison": False, "manufacturingOrientationVerified": False,
                      "historicalGeometry": report.get("historicalGeometry"), "visualReview": "pending"})
    geometry_pass = len(cases) == 6 and all(case.get("dxfGeometryPass") is True for case in cases)
    direction_pass = len(cases) == 6 and all(case.get("dxfDirectionPass") is True for case in cases)
    runner_path, runner_result = run / "result.json", None
    runner_record = {"path": str(runner_path), "exists": runner_path.is_file(), "sha256": None,
                     "reason": "RUNNER_SUCCESS_RECORD_MISSING"}
    if runner_record["exists"]:
        runner_record["sha256"] = digest(runner_path)
        try:
            runner_result = load(runner_path)
        except (ValueError, TypeError):
            pass
        runner_record["reason"] = "RUNNER_SUCCESS_RECORD_INVALID"
    supervision_accepted = (isinstance(runner_result, dict) and type(runner_result.get("code")) is int
                            and runner_result["code"] == 0 and runner_result.get("cadExited") is True)
    if supervision_accepted:
        runner_record["reason"] = "RUNNER_SUCCESS_RECORD_PRESENT"
    result = {"schema": "16029.variable-trial.output-audit.v2" if dxf_only else "16029.variable-trial.output-audit.v1",
              "source": str(run), "nativeResultSha256": digest(run/"native-result.json"),
              "outputScope": scope["outputScope"], "drawingStatus": scope["drawingStatus"],
              "scopeEvidence": {key:scope[key] for key in ("jobSha256", "manifestPath", "manifestSha256")},
              "caseCount": len(cases), "cases": cases, "dxfGeometryScopePass": geometry_pass,
              "nativeBatchSupervisionAccepted": supervision_accepted, "runnerSuccessRecord": runner_record,
              "dxfDirectionPass": direction_pass, "dxfTechnicalPass": geometry_pass and direction_pass,
              "geometryScope": "DXF units, contour, native flat size, holes, CUT mapping and baseline historical full geometry",
              "directionScope": "native flat-view association only; manufacturing orientation remains unverified",
              "fullPlanPass": False, "production_release": False, "visualReview": "pending"}
    (target / "output-audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
