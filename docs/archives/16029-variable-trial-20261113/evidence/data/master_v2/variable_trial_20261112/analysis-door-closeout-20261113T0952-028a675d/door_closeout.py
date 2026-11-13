"""Close the explicitly limited six-door generation/flat-geometry/reopen evidence."""
import argparse
import hashlib
import json
import math
import os
import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DATA = ROOT / "data/master_v2/variable_trial_20261112"
SOURCE = ROOT / "workers/master_v2/variable_trial"
EXPECTED = {(hand, width, height) for hand in ("L", "R") for width, height in ((437, 471.25), (287, 200), (537, 1000))}


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def safe(path):
    require(".." not in Path(path).parts, "PATH_TRAVERSAL_FORBIDDEN")
    path = Path(os.path.abspath(path))
    require(DATA in path.parents, "TRIAL_PATH_REQUIRED")
    for ancestor in [path, *path.parents]:
        if ancestor.exists():
            stat = ancestor.lstat()
            require(not ancestor.is_symlink() and not getattr(stat, "st_file_attributes", 0) & 0x400, "REPARSE_PATH")
    return path


def identity(item):
    return item["hand"], item["widthMm"], item["heightMm"]


def manifest_cases(manifest):
    require(manifest["schema"] == "16029-variable-output-manifest/v2" and manifest["complete"] is True
            and manifest["outputScope"] == "parts_dxf_only" and manifest["drawingStatus"] == "deferred_by_user_20261113"
            and manifest["production_release"] is False and len(manifest["cases"]) == 6, "COMPLETE_DXF_MANIFEST_REQUIRED")
    rows, folders = {}, set()
    for item in manifest["cases"]:
        key = identity(item)
        require(key in EXPECTED and key not in rows, "EXACT_UNIQUE_SIX_CASES_REQUIRED")
        require(re.fullmatch(r"[LR][0-9]+", item["folder"]) and item["folder"] not in folders
                and Path(item["part"]).name == item["part"] and item["part"].lower().endswith(".sldprt"), "UNIQUE_CASE_PATH_REQUIRED")
        require(item["drawing"] is None and item["flatPreparation"]["success"] is True and item["dxf"]["success"] is True, "CASE_OUTPUT_FAILED")
        rows[key] = item
        folders.add(item["folder"])
    require(set(rows) == EXPECTED, "EXACT_SIX_CASES_REQUIRED")
    return rows


def event_index(events, name, expected_paths):
    rows = [event for event in events if event.get("inspection") == name]
    require(len(rows) == 6 and all(isinstance(row.get("path"), str) for row in rows), "SIX_EVENT_PATHS_REQUIRED " + name)
    paths = [str(safe(row["path"])) for row in rows]
    require(len(set(paths)) == 6 and set(paths) == expected_paths, "EXACT_EVENT_PATH_SET_REQUIRED " + name)
    return dict(zip(paths, rows))


def flat_pass(result, prior, path):
    require(result["pass"] is True and str(safe(result["modelPath"])) == path
            and all(result[key] is True for key in ["originalConfigurationRestored", "featureSuppressionUnchanged", "partDiskUnchanged"])
            and result["partSaveCalled"] is False and result["manufacturingAccepted"] is False, "FLAT_READONLY_REOPEN_FAILED")
    require(result["flatConfiguration"] == prior["flatConfiguration"] and result["configurationNames"] == prior["configurationNames"]
            and result["geometry"]["flatGeometryVerified"] is True, "FLAT_REOPEN_CONFIGURATION_CHANGED")
    actual_size, prior_size = result["geometry"]["widthHeightThicknessMm"], prior["geometry"]["widthHeightThicknessMm"]
    require(len(actual_size) == len(prior_size) == 3, "THREE_FLAT_DIMENSIONS_REQUIRED")
    for actual, expected in zip(actual_size, prior_size):
        require(math.isfinite(actual) and abs(actual - expected) <= .01, "FLAT_REOPEN_SIZE_CHANGED")
    actual_cut, prior_cut = result["geometry"]["cutGeometry"], prior["geometry"]["cutGeometry"]
    for cut, expected_path in [(actual_cut, path), (prior_cut, str(safe(prior["modelPath"])) )]:
        require(cut["schema"] == "native-top-z-cut-v1" and cut["complete"] is True and cut["errors"] == []
                and cut["units"] == "mm" and str(safe(cut["modelPath"])) == expected_path
                and len(cut["primitives"]) == cut["boundaryEdgeCount"] > 0, "COMPLETE_FLAT_CUT_REQUIRED")
    require(numeric_snapshot_equal({key: value for key, value in actual_cut.items() if key != "modelPath"},
                                   {key: value for key, value in prior_cut.items() if key != "modelPath"}), "FLAT_CUT_REOPEN_CHANGED")


def numeric_snapshot_equal(actual, expected):
    if type(actual) in (int, float) and type(expected) in (int, float):
        return math.isfinite(actual) and math.isfinite(expected) and abs(actual - expected) <= 1e-7
    if type(actual) is not type(expected):
        return False
    if isinstance(actual, dict):
        return actual.keys() == expected.keys() and all(numeric_snapshot_equal(actual[key], expected[key]) for key in actual)
    if isinstance(actual, list):
        return len(actual) == len(expected) and all(numeric_snapshot_equal(a, b) for a, b in zip(actual, expected))
    return actual == expected


def claim_time_pass(claim):
    require(type(claim["startTicks"]) is int and claim["startTicks"] > 0, "POSITIVE_CAD_START_TICKS_REQUIRED")
    match = re.fullmatch(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?Z", claim["startedAtUtc"])
    require(match is not None, "UTC_CAD_START_TIME_REQUIRED")
    elapsed = datetime.strptime(match[1], "%Y-%m-%dT%H:%M:%S") - datetime(1, 1, 1)
    expected_ticks = (elapsed.days * 86400 + elapsed.seconds) * 10000000 + int((match[2] or "").ljust(7, "0"))
    require(claim["startTicks"] == expected_ticks, "CAD_START_TIME_TICKS_MISMATCH")


def folded_panel(event, item):
    require(identity(event) == identity(item) and len(event["geometry"]["bodies"]) == 1, "FOLDED_PANEL_IDENTITY_REQUIRED")
    body = event["geometry"]["bodies"][0]
    width, height = item["widthMm"], item["heightMm"]
    box = body["boxMm"]
    require(body["sheetMetal"] is True and len(box) == 6 and abs(box[3] - box[0] - width) <= .01
            and abs(box[4] - box[1] - height) <= .01, "FOLDED_PANEL_SIZE_CHANGED")
    sign = 1 if item["hand"] == "L" else -1
    circles = body["circleEdges"]
    hinges = [row for row in circles if abs(row[6] * 1000 - 5.1) < .01]
    locks = [row for row in circles if abs(row[6] * 1000 - 2) < .01]
    require(len(hinges) == 4 and len(locks) == 2, "FOLDED_HOLE_COUNTS_CHANGED")
    require(all(abs(row[0] * 1000 + (width / 2 - 10) * sign) < .01 and abs(row[2] * 1000 + 7) < .01 for row in hinges)
            and all(sum(abs(row[1] * 1000 - y) < .01 for row in hinges) == 1 for y in [-height / 2, -height / 2 + .8, height / 2 - .8, height / 2]), "FOLDED_HINGE_HAND_OR_POSITION_CHANGED")
    require(all(abs(row[0] * 1000 - (width / 2 - 6) * sign) < .01 and abs(row[1] * 1000) < .01 for row in locks)
            and all(sum(abs(row[2] * 1000 - z) < .01 for row in locks) == 1 for z in [-15, -14.2]), "FOLDED_LOCK_HAND_OR_POSITION_CHANGED")


def tracked(path, refs, expected=None, source_file=False):
    path = Path(path)
    if source_file:
        require(".." not in path.parts, "SOURCE_PATH_TRAVERSAL")
        path = Path(os.path.abspath(path))
        require(SOURCE in path.parents and path.is_file(), "TRIAL_SOURCE_FILE_REQUIRED")
        for ancestor in [path, *path.parents]:
            stat = ancestor.lstat()
            require(not ancestor.is_symlink() and not getattr(stat, "st_file_attributes", 0) & 0x400, "REPARSE_SOURCE")
    else:
        path = safe(path)
    value = path.read_bytes()
    sha = hashlib.sha256(value).hexdigest().upper()
    require(expected is None or sha == expected, "BOUND_FILE_CHANGED " + str(path))
    refs.append({"path": str(path), "sha256": sha})
    return value


def tracked_json(path, refs):
    return json.loads(tracked(path, refs).decode("utf-8-sig"))


def run_evidence(run, action, refs):
    result = {}
    for name in ["job", "native-result", "result", "supervision", "protection-result", "toolchain"]:
        path = safe(run / (name + ".json"))
        result[name] = tracked_json(path, refs)
    job, native, completed = result["job"], result["native-result"], result["result"]
    require(job["action"] == action == native["action"] and job["production_release"] is False and native["production_release"] is False, "ACTION_SCOPE_MISMATCH")
    require(job["cadDirectory"] == str(run / "cad") and job["resultPath"] == str(run / "native-result.json")
            and job["toolchainPath"] == str(run / "toolchain.json")
            and job["cancellationPath"] == str(run / "cancel.json"), "JOB_PATH_BINDING_CHANGED")
    require(job["outputScope"] == "parts_dxf_only" and native["events"][-1]["inspection"] == "finished" and native["events"][-1]["success"] is True
            and native["events"][-1]["cadExited"] is True, "NATIVE_NOT_FINISHED")
    require(type(completed["code"]) is int and completed["code"] == 0 and completed["cadExited"] is True, "RUNNER_NOT_ACCEPTED")
    require(result["supervision"]["state"] == "cad_exited" and result["supervision"]["nativeEnded"] is True and result["supervision"]["cad"] == [], "SUPERVISION_NOT_CLOSED")
    require(result["protection-result"]["pass"] is True and result["protection-result"]["changed"] == [], "PROTECTED_FILES_CHANGED")
    require(result["protection-result"].get("nativeTaskIndex", {}).get("accepted", True) is True, "TASK_INDEX_PROTECTION_FAILED")
    require(not (run / "cancel.json").exists(), "CANCELLED_RUN")
    claims = [event for event in native["events"] if event.get("inspection") == "cad_owned"]
    require(len(claims) == 1 and type(claims[0]["pid"]) is int and claims[0]["pid"] > 0
            and claims[0]["pid"] == native["cadPid"] and claims[0]["startTicks"] == native["cadTicks"], "NATIVE_OWNERSHIP_MISSING")
    claim_time_pass(claims[0])
    tool = result["toolchain"]
    require(tool["schema"] == "winnsen.variable_trial.toolchain.v2", "TOOLCHAIN_SCHEMA_REQUIRED")
    exe = safe(tool["exe"])
    require(exe.name == "VariableTrial.exe" and exe.parent.parent == DATA / "toolchain", "TOOLCHAIN_EXE_PATH_INVALID")
    require(len(tool["sources"]) == 3 and {Path(item["path"]).name for item in tool["sources"]}
            == {"VariableTrial.cs", "TrialAssembly.cs", "TrialOutput.cs"}, "EXACT_NATIVE_SOURCES_REQUIRED")
    for item in tool["sources"]:
        require(Path(item["path"]) == SOURCE / Path(item["path"]).name
                and Path(item["archivePath"]) == exe.parent / "sources" / Path(item["path"]).name, "TOOLCHAIN_SOURCE_PATH_CHANGED")
        tracked(item["archivePath"], refs, item["sha256"])
    require(len(tool["dlls"]) == 2 and {Path(item["path"]).name for item in tool["dlls"]}
            == {"SolidWorks.Interop.sldworks.dll", "SolidWorks.Interop.swconst.dll"}, "EXACT_INTEROP_DLLS_REQUIRED")
    for item in tool["dlls"]:
        require(Path(item["path"]).parent == exe.parent, "TOOLCHAIN_DLL_PATH_CHANGED")
        tracked(item["path"], refs, item["sha256"])
    require({p.name for p in exe.parent.glob("*.dll")} == {Path(item["path"]).name for item in tool["dlls"]}
            and not exe.with_suffix(".exe.config").exists(), "UNMANIFESTED_TOOLCHAIN_DEPENDENCY")
    tracked(exe, refs, tool["sha256"])
    require(len(job["inputVersions"]) == 2 and len({item["path"] for item in job["inputVersions"]}) == 2, "EXACT_INPUT_VERSIONS_REQUIRED")
    for item in job["inputVersions"]:
        tracked(item["path"], refs, item["sha256"], source_file=SOURCE in Path(item["path"]).parents)
    result["claim"] = claims[0]
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("generation", type=Path)
    parser.add_argument("reopen", type=Path)
    parser.add_argument("audit", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    generation, reopen, target = safe(args.generation), safe(args.reopen), safe(args.out)
    require(generation.parent == DATA / "runs" and reopen.parent == DATA / "runs" and generation != reopen, "TWO_DISTINCT_RUNS_REQUIRED")
    require(not target.exists(), "PRESERVE_EXISTING_CLOSEOUT")
    refs = []
    source = run_evidence(generation, "outputs-dxf", refs)
    cold = run_evidence(reopen, "doors-reopen", refs)
    require((source["claim"]["pid"], source["claim"]["startTicks"]) != (cold["claim"]["pid"], cold["claim"]["startTicks"]), "NEW_CAD_PROCESS_REQUIRED")
    require(cold["job"]["sourceCadDirectory"] == str(generation / "cad") and cold["job"]["doorSource"]["run"] == str(generation), "REOPEN_SOURCE_MISMATCH")
    door_source = cold["job"]["doorSource"]
    require(door_source["schema"] == "16029-variable-door-reopen-source/v1" and door_source["fullPlanPass"] is False
            and door_source["production_release"] is False, "REOPEN_SOURCE_SCOPE_CHANGED")
    needed = {str(generation / (name + ".json")) for name in ["job", "native-result", "result", "supervision", "protection-result"]}
    needed.add(str(generation / "cad/outputs/output-manifest.json"))
    bound = {item["path"] for item in door_source["evidence"]}
    require(needed <= bound and len(bound) == len(door_source["evidence"]), "REOPEN_SOURCE_EVIDENCE_SET_INCOMPLETE")
    for item in door_source["evidence"]:
        tracked(item["path"], refs, item["sha256"])
    for key in ["doors", "layouts", "process", "specification", "inputVersions"]:
        require(source["job"][key] == cold["job"][key], "REOPEN_INPUT_MISMATCH " + key)
    audit_path = safe(args.audit)
    audit = tracked_json(audit_path, refs)
    auditor = tracked_json(audit_path.parent / "auditor-toolchain.json", refs)
    require(auditor["schema"] == "16029-dxf-auditor-evidence/v1" and auditor["source"] == str(generation)
            and auditor["outputAuditSha256"] == digest(audit_path) and auditor["sourceBytesStableDuringAudit"] is True
            and auditor["drawingAndPdfActionsExecuted"] is False and auditor["production_release"] is False, "AUDITOR_VERSION_EVIDENCE_REQUIRED")
    require(len(auditor["sources"]) == 2 and {Path(item["path"]).name for item in auditor["sources"]}
            == {"audit_outputs.py", "dxf_check.py"}, "EXACT_AUDITOR_SOURCES_REQUIRED")
    for item in auditor["sources"]:
        tracked(item["archivePath"], refs, item["sha256"])
    require(audit["source"] == str(generation) and audit["nativeResultSha256"] == digest(generation / "native-result.json") and audit["caseCount"] == 6
            and audit["dxfGeometryScopePass"] is True and audit["nativeBatchSupervisionAccepted"] is True, "COMPLETE_SCOPED_GEOMETRY_AUDIT_REQUIRED")
    manifest_path = generation / "cad/outputs/output-manifest.json"
    manifest = tracked_json(manifest_path, refs)
    relocated_manifest = tracked_json(reopen / "cad/outputs/output-manifest.json", refs)
    require(manifest == relocated_manifest and manifest["complete"] is True and len(manifest["cases"]) == 6, "REOPEN_MANIFEST_CHANGED")
    declared_cases = manifest_cases(manifest)
    require({(door["hand"], door["variables"]["DOOR_W"], door["variables"]["DOOR_H"]) for door in source["job"]["doors"]} == EXPECTED
            and len(source["job"]["doors"]) == 6, "JOB_EXACT_SIX_DOORS_REQUIRED")
    require(audit["schema"] == "16029.variable-trial.output-audit.v2" and audit["outputScope"] == "parts_dxf_only"
            and audit["drawingStatus"] == "deferred_by_user_20261113" and audit["fullPlanPass"] is False and audit["production_release"] is False
            and audit["scopeEvidence"]["jobSha256"] == digest(generation / "job.json")
            and audit["scopeEvidence"]["manifestSha256"] == digest(manifest_path)
            and audit["scopeEvidence"]["manifestPath"] == str(manifest_path), "AUDIT_SCOPE_BINDING_CHANGED")
    require(len(audit["cases"]) == 6 and {identity(row) for row in audit["cases"]} == EXPECTED
            and all(row["dxfGeometryPass"] is True and row["nativeHoleGeometryPass"] is True
                    and row["dxfErrors"] == [] and row["bendCount"] == 12 for row in audit["cases"]), "AUDIT_SIX_SUCCESSFUL_CASES_REQUIRED")
    events = cold["native-result"]["events"]
    expected_paths = {str(reopen / "cad/outputs" / item["folder"] / item["part"]) for item in declared_cases.values()}
    indexes = {}
    for name in ["relocated_panel", "relocated_flat", "relocated_process_readback", "door_readonly_dependencies"]:
        indexes[name] = event_index(events, name, expected_paths)
    require(not any(event.get("inspection") == "relocated_assembly" for event in events), "DOOR_ONLY_SCOPE_REQUIRED")
    full_hashes = [event for event in events if event.get("inspection") == "door_reopen_full_action_hashes"]
    require(len(full_hashes) == 1 and full_hashes[0]["pass"] is True, "FULL_ACTION_READONLY_HASH_PROOF_REQUIRED")
    cad_hashes = {str(safe(path)): digest(path) for path in (reopen / "cad").rglob("*") if path.is_file() and path.suffix.lower() in {".sldprt", ".sldasm", ".slddrw"}}
    require(cad_hashes and full_hashes[0]["files"] == cad_hashes, "FULL_ACTION_CAD_HASH_INVENTORY_CHANGED")
    for path, sha in cad_hashes.items():
        tracked(path, refs, sha)
    cases = []
    artifact_paths = {generation: set(), reopen: set()}
    source_artifacts = []
    for item in manifest["cases"]:
        process = item["processReadback"]
        require(process["schema"] == "16029-door-process-readback/v1" and all(process[key] is True for key in ["rawReadbackComplete", "pilotTopologyMatches", "fixedExplicitOverridesMatch", "candidateFixedProcessMatches"]), "PROCESS_READBACK_INCOMPLETE")
        require(process["effectiveInheritanceVerified"] is False and process["manufacturingOrientationVerified"] is False and process["manufacturingAccepted"] is False, "PROCESS_ACCEPTANCE_SCOPE_CHANGED")
        cold_path = str(reopen / "cad/outputs" / item["folder"] / item["part"])
        panel = indexes["relocated_panel"][cold_path]
        require(identity(panel) == identity(item) and panel["savedActiveConfiguration"] == item["activeConfiguration"] == "Default"
                and panel["properties"] == item["properties"] and panel["geometry"]["bodies"], "PANEL_REOPEN_IDENTITY_OR_PROPERTIES_CHANGED")
        folded_panel(panel, item)
        require(indexes["relocated_process_readback"][cold_path]["result"] == process, "PROCESS_REOPEN_CHANGED")
        dependencies = indexes["door_readonly_dependencies"][cold_path]
        require(dependencies["pass"] is True and dependencies["rewritten"] is False and dependencies["dependencies"] == [], "DOOR_DEPENDENCIES_CHANGED")
        flat_pass(indexes["relocated_flat"][cold_path]["result"], item["flatPreparation"], cold_path)
        audit_case = next(row for row in audit["cases"] if identity(row) == identity(item))
        require(audit_case["part"] == str(generation / "cad/outputs" / item["folder"] / item["part"])
                and audit_case["case"] == item["folder"], "AUDIT_CASE_PATH_CHANGED")
        audit_detail = tracked_json(audit_path.parent / item["folder"] / "dxf-audit.json", refs)
        require(audit_detail["sha256"] == item["dxf"]["sha256"] and audit_detail["geometryAuditPassed"] is True
                and audit_detail["units"]["verified"] is True and audit_detail["errors"] == []
                and audit_detail["bends"]["segmentCount"] == 12, "CASE_AUDIT_REPORT_CHANGED")
        for path_key, filename in [("path", "flat.dxf"), ("nativeCutPath", "flat.native-cut.dxf"), ("nativeBendsPath", "flat.native-bends.dxf")]:
            require(item["dxf"][path_key] == str(generation / "cad/outputs" / item["folder"] / filename), "DXF_MANIFEST_PATH_CHANGED")
        artifacts = [(item["part"], item["partSha256"]), ("flat.dxf", item["dxf"]["sha256"]), ("flat.native-cut.dxf", item["dxf"]["nativeCutSha256"]), ("flat.native-bends.dxf", item["dxf"]["nativeBendsSha256"])]
        for filename, expected in artifacts:
            for run in [generation, reopen]:
                path = safe(run / "cad/outputs" / item["folder"] / filename)
                require(path not in artifact_paths[run], "DUPLICATE_OUTPUT_ARTIFACT")
                artifact_paths[run].add(path)
                tracked(path, refs, expected)
        source_artifacts.append({"folder": item["folder"], "part": item["part"], "hand": item["hand"], "widthMm": item["widthMm"], "heightMm": item["heightMm"],
                                 "partSha256": item["partSha256"], "dxfSha256": item["dxf"]["sha256"], "nativeCutSha256": item["dxf"]["nativeCutSha256"], "nativeBendsSha256": item["dxf"]["nativeBendsSha256"]})
        cases.append({"hand": item["hand"], "widthMm": item["widthMm"], "heightMm": item["heightMm"], "part": item["part"], "artifacts": artifacts,
                      "flatSizeMm": item["flatPreparation"]["geometry"]["widthHeightThicknessMm"], "materials": item["properties"]["configurationMaterials"]})
    require(door_source["artifacts"] == source_artifacts, "REOPEN_SOURCE_ARTIFACT_MANIFEST_CHANGED")
    for run in [generation, reopen]:
        actual = {safe(path) for path in (run / "cad/outputs").rglob("*") if path.is_file() and path.suffix.lower() in {".sldprt", ".sldasm", ".slddrw", ".pdf", ".dxf"}}
        require(len(artifact_paths[run]) == 24 and actual == artifact_paths[run], "EXACT_24_OUTPUT_ARTIFACTS_REQUIRED")
    tracked(Path(__file__), refs, source_file=True)
    result = {"schema": "16029-door-geometry-closure/v2", "generation": str(generation), "reopen": str(reopen), "cases": cases, "references": refs,
              "generationFlatGeometryReopenPass": True, "acceptanceScope": "six native door samples, dimensions, hand-specific folded holes, fixed-process readback, DXF geometry and independent read-only reopen",
              "effectiveInheritanceVerified": False, "manufacturingOrientationVerified": False, "factoryMaterialConfirmed": False,
              "drawingStatus": "deferred_by_user_20261113", "engineeringAcceptancePass": False, "fullPlanPass": False, "production_release": False,
              "remaining": ["factory material confirmation (right panel unspecified)", "independent effective inheritance semantics", "DXF manufacturing face and bend directions", "assembly weight metadata stability and structural corrections"]}
    require(all(digest(Path(item["path"])) == item["sha256"] for item in refs), "EVIDENCE_CHANGED_DURING_CLOSEOUT")
    target.mkdir(parents=True, exist_ok=False)
    (target / "door-closeout.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"directory": str(target), "generationFlatGeometryReopenPass": True, "caseCount": len(cases), "production_release": False}, ensure_ascii=False))


if __name__ == "__main__":
    main()
