"""Compare a resumed T3 case with T4, preserving the frozen measurement checker.

Only reused case1/case2 toolchain identities may be corrected. The original
comparison and both lineage views remain in the report. This is technical
recovery evidence, never engineering acceptance or production release.
"""

import argparse
import copy
import hashlib
import json
import ntpath
import re
import types
from datetime import datetime
from pathlib import Path, PureWindowsPath


CHECKER_SHA256 = "0920CDD6E3EEACE21ED36F40D4A0D64EBC2CD73B4DED063AF0FE2F55C1B57B27"
RESUME_SCHEMA = "16029-variable-assembly-resume/v1"
CAD_EXTENSIONS = {".SLDPRT", ".SLDASM", ".SLDDRW"}
SOURCE_EVIDENCE = {
    "job.json", "native-result.json", "toolchain.json", "protected-before.json",
    "protection-result.json", "supervision.json", "copy-manifest.json", "native-index-protection.json",
}
MEASUREMENT_METHODS = (
    "ValidateWorldPanel", "ValidateWorldBoundary", "MeasureHingeInterfaces", "MeasureHingeSupportChains",
    "MeasureInterference", "Bounds", "CaptureBoundaryCircle", "CheckFeatureHealth",
)
MEASUREMENT_HELPERS = (
    "ReopenExisting", "ReopenAndMeasure", "MeasureBuiltCase", "ReadDocumentMetadata",
    "RequireCompleteInterference", "SameTransform", "Transform", "BodyBounds", "CylinderRelation",
)


def digest(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def need(condition, message):
    if not condition:
        raise ValueError(message)


def load_checker(path=None):
    path = path or Path(__file__).with_name("compare_assembly_measurements.py")
    raw = path.read_bytes()
    need(digest(raw) == CHECKER_SHA256, "frozen_checker_sha256_mismatch")
    module = types.ModuleType("frozen_assembly_measurement_checker")
    module.__file__ = str(path.resolve())
    exec(compile(raw, str(path), "exec"), module.__dict__)
    return module


def disk_path(value):
    return Path(str(PureWindowsPath(value))).resolve()


def same_path(value, expected):
    return PureWindowsPath(str(value)) == PureWindowsPath(str(expected))


def relative_name(value):
    path = PureWindowsPath(value)
    need(bool(path.name) and not path.drive and not path.is_absolute()
         and not any(part in (".", "..") for part in path.parts), "unsafe_inventory_relative_path:" + str(value))
    return path.as_posix()


def row_map(rows):
    need(isinstance(rows, list), "inventory_list_required")
    result = {}
    for row in rows:
        name = relative_name(row["relativePath"])
        need(name.casefold() not in result and type(row.get("size")) is int and row["size"] >= 0
             and re.fullmatch(r"[A-F0-9]{64}", row.get("sha256", "")) is not None,
             "duplicate_or_invalid_inventory_row:" + name)
        result[name.casefold()] = {"relativePath": name, "size": row["size"], "sha256": row["sha256"]}
    return result


def under(path, root):
    try:
        value = path.relative_to(root)
    except ValueError:
        raise ValueError("path_outside_bound_root:" + str(path)) from None
    need(bool(value.name), "file_or_child_path_required:" + str(path))
    return value.as_posix()


class EvidenceReads:
    def __init__(self):
        self.files = {}
        self.roots = {}

    def raw(self, path):
        path = path.resolve()
        raw = path.read_bytes()
        row = {"path": str(path), "size": len(raw), "sha256": digest(raw)}
        need(path not in self.files or self.files[path] == row, "evidence_changed_while_reading:" + str(path))
        self.files[path] = row
        return raw

    def read(self, path):
        return json.loads(self.raw(path).decode("utf-8-sig"))

    def inventory(self, root):
        root = root.resolve()
        need(root.is_dir(), "cad_inventory_root_missing:" + str(root))
        rows = []
        for path in sorted(root.rglob("*")):
            need(not path.is_symlink() and not getattr(path, "is_junction", lambda: False)(),
                 "linked_inventory_member_forbidden:" + str(path))
            if path.is_file():
                name = under(path.resolve(), root)
                raw = self.raw(path)
                rows.append({"relativePath": name, "size": len(raw), "sha256": digest(raw)})
        result = row_map(rows)
        need(root not in self.roots or self.roots[root] == result, "cad_inventory_changed:" + str(root))
        self.roots[root] = result
        return result

    def unchanged(self):
        for path, row in list(self.files.items()):
            self.raw(path)
        for root, expected in list(self.roots.items()):
            need(self.inventory(root) == expected, "cad_inventory_changed:" + str(root))
        return True


def owning_run(path):
    runs = [parent for parent in path.resolve().parents
            if (parent / "job.json").is_file() and (parent / "cad").is_dir()]
    need(bool(runs), "owning_run_not_found:" + str(path))
    return runs[0]


def method_hash(source, name):
    lines = source.replace("\r\n", "\n").splitlines(keepends=True)
    starts = [i for i, line in enumerate(lines) if line.startswith("    ") and not line.startswith("     ")
              and "(" in line and line.split("(", 1)[0].rstrip().endswith(" " + name)]
    need(len(starts) == 1, "measurement_or_generation_helper_missing_or_ambiguous:" + name)
    end = next(i for i in range(starts[0] + 1, len(lines)) if lines[i].rstrip("\n") == "    }")
    return digest("".join(lines[starts[0]:end + 1]).encode("utf-8"))


def toolchain_proof(checker, reads, run, data_root):
    manifest = reads.read(run / "toolchain.json")
    exe = disk_path(manifest["exe"])
    under(exe, data_root / "toolchain")
    need(manifest.get("schema") == "winnsen.variable_trial.toolchain.v2" and exe.name == "VariableTrial.exe"
         and digest(reads.raw(exe)) == manifest.get("sha256"), "run_toolchain_executable_not_bound:" + str(run))
    for field, names in (("sources", {"TrialAssembly.cs", "TrialOutput.cs", "VariableTrial.cs"}),
                         ("dlls", {"SolidWorks.Interop.sldworks.dll", "SolidWorks.Interop.swconst.dll"})):
        rows = manifest.get(field, [])
        paths = [disk_path(row["archivePath" if field == "sources" else "path"]) for row in rows]
        need(len(paths) == len(names) and {p.name for p in paths} == names,
             "run_toolchain_archive_membership_invalid:" + field)
        for path, row in zip(paths, rows):
            parent = exe.parent / "sources" if field == "sources" else exe.parent
            need(path.parent == parent and digest(reads.raw(path)) == row.get("sha256"),
                 "run_toolchain_archive_hash_mismatch:" + str(path))
    proof = checker.toolchain_proof(run)
    need(proof["toolchainSha256"] == reads.files[(run / "toolchain.json").resolve()]["sha256"],
         "toolchain_manifest_changed_during_proof")
    source = reads.raw(disk_path(proof["archivePath"])).decode("utf-8-sig")
    proof["measurementHelperSha256"] = {name: method_hash(source, name) for name in MEASUREMENT_HELPERS}
    proof["generationAuxiliarySha256"] = {"RefreshNativeLinks": method_hash(source, "RefreshNativeLinks")}
    return proof


def run_proof(reads, run, action, failed=False):
    job, native = reads.read(run / "job.json"), reads.read(run / "native-result.json")
    need(job.get("action") == native.get("action") == action and job.get("production_release") is False
         and native.get("production_release") is False and job.get("outputScope") == "parts_dxf_only",
         "run_action_or_scope_invalid:" + str(run))
    for field, expected in (("cadDirectory", run / "cad"), ("resultPath", run / "native-result.json"),
                            ("toolchainPath", run / "toolchain.json")):
        need(same_path(job.get(field, ""), expected), "run_path_not_bound:" + field)
    events = native.get("events", [])
    finished = [event for event in events if event.get("inspection") == "finished"]
    need(len(finished) == 1 and bool(events) and finished[0] == events[-1]
         and finished[0].get("cadExited") is True and finished[0].get("success") is (not failed),
         "native_finished_not_bound:" + str(run))
    failures = [event for event in events if event.get("inspection") == "failure"]
    need(not any(event.get("inspection") == "native_guard_retained" for event in events), "native_guard_retained")
    if failed:
        need(isinstance(finished[0].get("error"), str) and "MINIMUM_ASSEMBLY_GENERATION_FAILED case3" in finished[0]["error"]
             and len(failures) == 1 and failures[0].get("error") == finished[0]["error"]
             and not (run / "result.json").exists() and not (run / "cancel.json").exists(),
             "specific_old_case3_failure_not_preserved")
    else:
        result = reads.read(run / "result.json")
        need(finished[0].get("error") is None and not failures and not (run / "cancel.json").exists()
             and type(result.get("code")) is int and result["code"] == 0 and result.get("cadExited") is True,
             "successful_runner_completion_required:" + str(run))
    claims = [event for event in events if event.get("inspection") == "cad_owned"]
    claim = claims[0] if len(claims) == 1 else {}
    need(len(claims) == 1 and type(claim.get("pid")) is int and claim["pid"] > 0
         and type(claim.get("startTicks")) is int and claim["startTicks"] > 0
         and claim.get("pid") == native.get("cadPid") and claim.get("startTicks") == native.get("cadTicks")
         and ntpath.normcase(claim.get("executable", "")) == ntpath.normcase(native.get("cadExe", ""))
         and same_path(claim.get("executable", ""), r"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe")
         and str(claim.get("revision", "")).startswith("28."), "native_cad_ownership_not_bound:" + str(run))
    guard, supervision = reads.read(run / "protection-result.json"), reads.read(run / "supervision.json")
    need(guard.get("pass") is True and guard.get("changed") == []
         and (not guard.get("nativeTaskIndex") or guard["nativeTaskIndex"].get("accepted") is True),
         "run_protection_not_passed:" + str(run))
    need(supervision.get("state") == "cad_exited" and supervision.get("nativeEnded") is True
         and supervision.get("cad") == [], "run_supervision_exit_not_observed:" + str(run))
    return job, native


def verify_copy(reads, run, source_run, source_inventory, final_inventory, excluded=(), added=()):
    manifest, job = reads.read(run / "copy-manifest.json"), reads.read(run / "job.json")
    need(same_path(manifest.get("source", ""), source_run / "cad")
         and same_path(manifest.get("target", ""), run / "cad")
         and same_path(job.get("sourceCadDirectory", ""), source_run / "cad"), "exact_copy_hop_not_bound:" + str(run))
    need(manifest.get("excludedRelativeDirectories", []) == list(excluded), "copy_exclusion_not_exact")
    expected = {key: row for key, row in source_inventory.items()
                if not any(key.startswith(prefix.casefold() + "/") for prefix in excluded)}
    copied = row_map(manifest.get("files", []))
    need(copied == expected, "copy_manifest_not_exact_source_inventory:" + str(run))
    native = reads.read(run / "native-result.json")
    events = native.get("events", [])
    rewrites = [event for event in events if event.get("inspection") == "rewrite_reference"]
    rewritten = set()
    for row in rewrites:
        file = disk_path(row["file"])
        key = under(file, run / "cad").casefold()
        dependency = under(disk_path(row["old"]), source_run / "cad")
        dependency_key = dependency.casefold()
        need(row.get("success") is True and key in copied and file.suffix.upper() in CAD_EXTENSIONS
             and dependency_key in copied and same_path(row.get("target", ""), run / "cad" / Path(dependency)),
             "reference_rewrite_not_exact_relative_copy:" + str(file))
        rewritten.add(key)
    closure = [event for event in events if event.get("inspection") == "dependency_closure"]
    need(len(closure) == 1 and closure[0].get("pass") is True
         and closure[0].get("fileCount") == sum(PureWindowsPath(key).suffix.upper() in CAD_EXTENSIONS for key in copied),
         "copy_native_dependency_closure_incomplete")
    expected_members = set(copied) | {name.casefold() for name in added}
    if excluded:
        expected_members |= {key for key in final_inventory if any(key.startswith(prefix.casefold() + "/") for prefix in excluded)}
    need(set(final_inventory) == expected_members, "final_cad_inventory_has_unbound_members:" + str(run))
    for key, before in copied.items():
        after = final_inventory[key]
        need(after == before or (PureWindowsPath(key).suffix.upper() in CAD_EXTENSIONS and key in rewritten),
             "copied_bytes_changed_without_native_relative_rewrite:" + key)
    return copied, rewritten


def utc_ticks(value):
    match = re.fullmatch(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.([0-9]{1,7}))?Z", value if isinstance(value, str) else "")
    need(match is not None, "flat_reopen_invalid_utc_timestamp:" + str(value))
    date = datetime.strptime(match[1], "%Y-%m-%dT%H:%M:%S")
    seconds = date.toordinal() * 86400 + date.hour * 3600 + date.minute * 60 + date.second
    return seconds * 10000000 + int((match[2] or "").ljust(7, "0"))


def relocated_flat_diagnostics(reads, run, job, native, source_inventory, final_inventory):
    """Bind only the six native readbacks' exact two newly created trace files."""
    manifest_name = "outputs/output-manifest.json"
    manifest_path = run / "cad" / manifest_name
    need(manifest_name in source_inventory and final_inventory.get(manifest_name) == source_inventory[manifest_name]
         and digest(reads.raw(manifest_path)) == source_inventory[manifest_name]["sha256"],
         "flat_reopen_output_manifest_not_exact_copy")
    need(not any(PureWindowsPath(key).name.startswith("flat-reopen.trace-") for key in source_inventory),
         "flat_reopen_requires_source_without_prior_trace_files")
    manifest = reads.read(manifest_path)
    need(isinstance(manifest, dict), "flat_reopen_output_manifest_object_required")
    cases, doors = manifest.get("cases", []), job.get("doors", [])
    folders = ("L0", "R1", "L2", "R3", "L4", "R5")
    need(manifest.get("schema") == "16029-variable-output-manifest/v2" and manifest.get("complete") is True
         and manifest.get("outputScope") == "parts_dxf_only" and manifest.get("production_release") is False
         and manifest.get("drawingStatus") == "deferred_by_user_20261113"
         and isinstance(cases, list) and all(isinstance(row, dict) for row in cases)
         and [row.get("folder") for row in cases] == list(folders)
         and isinstance(doors, list) and len(doors) == 6 and all(isinstance(row, dict) for row in doors),
         "flat_reopen_exact_six_output_cases_required")
    events, times = native.get("events", []), native.get("eventTimes", [])
    need(isinstance(times, list) and len(times) == len(events) and events[0].get("inspection") == "cad_owned"
         and events[-1].get("inspection") == "finished", "flat_reopen_native_event_times_not_bound")
    ticks, elapsed = [], []
    for index, row in enumerate(times):
        need(isinstance(row, dict) and type(row.get("index")) is int and row["index"] == index
             and type(row.get("elapsedMs")) is int and row["elapsedMs"] >= 0, "flat_reopen_native_event_times_not_bound")
        ticks.append(utc_ticks(row.get("atUtc")))
        elapsed.append(row["elapsedMs"])
    need(ticks == sorted(ticks) and elapsed == sorted(elapsed), "flat_reopen_native_event_times_not_ordered")
    panels = [(i, row) for i, row in enumerate(events) if row.get("inspection") == "relocated_panel"]
    flats = [(i, row) for i, row in enumerate(events) if row.get("inspection") == "relocated_flat"]
    need(len(panels) == len(flats) == 6, "flat_reopen_exact_six_native_panel_and_flat_events_required")
    identities, added, models = set(), set(), []
    for item, door, folder in zip(cases, doors, folders):
        hand, width, height = item.get("hand"), item.get("widthMm"), item.get("heightMm")
        identity = (hand, width, height)
        need(hand == folder[0] == door.get("hand") and width == door.get("variables", {}).get("DOOR_W")
             and height == door.get("variables", {}).get("DOOR_H") and identity not in identities,
             "flat_reopen_output_case_identity_not_bound:" + folder)
        identities.add(identity)
        part = relative_name(item.get("part", ""))
        need("/" not in part and PureWindowsPath(part).suffix.upper() == ".SLDPRT",
             "flat_reopen_output_part_name_invalid:" + folder)
        model = run / "cad/outputs" / folder / part
        model_key = under(model, run / "cad").casefold()
        need(model_key in source_inventory and final_inventory.get(model_key) == source_inventory[model_key]
             and digest(reads.raw(model)) == item.get("partSha256") == source_inventory[model_key]["sha256"],
             "flat_reopen_output_part_hash_not_bound:" + folder)
        panel_rows = [(i, row) for i, row in panels if same_path(row.get("path", ""), model)]
        flat_rows = [(i, row) for i, row in flats if same_path(row.get("path", ""), model)]
        need(len(panel_rows) == len(flat_rows) == 1, "flat_reopen_unique_native_model_events_required:" + folder)
        panel_index, panel = panel_rows[0]
        flat_index, event = flat_rows[0]
        result, prior = event.get("result", {}), item.get("flatPreparation", {})
        need(isinstance(result, dict) and isinstance(prior, dict), "flat_reopen_native_result_object_required:" + folder)
        need(panel_index < flat_index and panel.get("hand") == hand and panel.get("widthMm") == width
             and panel.get("heightMm") == height and panel.get("savedActiveConfiguration") == item.get("activeConfiguration") == "Default"
             and same_path(result.get("modelPath", ""), model), "flat_reopen_native_model_identity_not_bound:" + folder)
        need(result.get("pass") is True and prior.get("success") is True
             and result.get("flatConfiguration") == prior.get("flatConfiguration") == "DefaultSM-FLAT-PATTERN"
             and result.get("configurationNames") == prior.get("configurationNames") == item.get("configurations")
             == ["Default", "DefaultSM-FLAT-PATTERN"]
             and all(result.get(flag) is True for flag in ("originalConfigurationRestored", "featureSuppressionUnchanged", "partDiskUnchanged"))
             and result.get("partSaveCalled") is False and result.get("manufacturingAccepted") is False,
             "flat_reopen_native_success_or_cleanup_not_bound:" + folder)
        geometry = result.get("geometry", {})
        need(isinstance(geometry, dict) and geometry.get("flatGeometryVerified") is True
             and geometry.get("manufacturingAccepted") is False, "flat_reopen_native_geometry_not_verified:" + folder)
        paths = result.get("diagnosticFiles", [])
        expected_paths = [model.parent / ("flat-reopen.trace-%03d.json" % i) for i in (1, 2)]
        need(isinstance(paths, list) and len(paths) == 2
             and all(isinstance(value, str) and same_path(value, expected) for value, expected in zip(paths, expected_paths)),
             "flat_reopen_exact_two_diagnostic_paths_required:" + folder)
        cleanup = {"originalConfiguration": "Default", "originalConfigurationRestored": True,
                   "partDiskUnchanged": True, "featureSuppressionUnchanged": True, "partSaveCalled": False}
        traces, trace_ticks = [], []
        for path, stage, details in zip(expected_paths, ("native_flat_body", "reopened_flat_cleanup"), (geometry, cleanup)):
            key = under(path, run / "cad").casefold()
            need(key not in source_inventory and key not in added and key in final_inventory,
                 "flat_reopen_diagnostic_not_unique_new_member:" + key)
            raw = reads.raw(path)
            need(digest(raw) == final_inventory[key]["sha256"], "flat_reopen_diagnostic_hash_not_bound:" + key)
            trace = json.loads(raw.decode("utf-8-sig"))
            need(isinstance(trace, dict) and set(trace) == {"inspection", "atUtc", "stage", "details"}
                 and trace.get("inspection") == "trial_native_flat_output" and trace.get("stage") == stage
                 and json.dumps(trace.get("details"), sort_keys=True, allow_nan=False) == json.dumps(details, sort_keys=True, allow_nan=False),
                 "flat_reopen_diagnostic_native_content_mismatch:" + key)
            trace_ticks.append(utc_ticks(trace.get("atUtc")))
            traces.append(dict(reads.files[path.resolve()], relativePath=final_inventory[key]["relativePath"],
                               stage=stage, atUtc=trace["atUtc"]))
            added.add(key)
        need(ticks[0] <= ticks[panel_index] <= trace_ticks[0] < trace_ticks[1] <= ticks[flat_index] <= ticks[-1],
             "flat_reopen_diagnostic_time_not_bound_to_native_model_events:" + folder)
        models.append({"folder": folder, "hand": hand, "widthMm": width, "heightMm": height,
                       "model": dict(reads.files[model.resolve()], relativePath=final_inventory[model_key]["relativePath"]),
                       "panelEventIndex": panel_index, "flatEventIndex": flat_index, "diagnostics": traces})
    return {"verified": True, "sourceHadNoFlatReopenTraces": True, "addedRelativePaths": sorted(added),
            "outputManifest": dict(reads.files[manifest_path.resolve()]),
            "nativeResult": dict(reads.files[(run / "native-result.json").resolve()]), "models": models}


def complete_case(checker, reads, path, data, layout):
    raw = reads.raw(path)
    need(json.loads(raw.decode("utf-8-sig")) == data, "native_case_evidence_not_exact:" + str(path))
    need(data.get("schema") == "16029-independent-variable-minimum-assembly/v1"
         and data.get("layout") == layout and not data.get("error") and not data.get("cleanupError"),
         "complete_generated_case_required:" + str(path))
    snapshot = checker.Snapshot(data, path.resolve(), raw, purpose="relocation")
    need(not snapshot.errors and not snapshot.metadata_errors and not snapshot.runtime_lineage.get("errors"),
         "complete_case_checker_errors:" + repr(snapshot.errors + snapshot.metadata_errors + snapshot.runtime_lineage.get("errors", [])))
    need(same_path(data.get("outputDirectory", ""), path.parent)
         and same_path(data.get("assemblyPath", ""), path.parent / "16029_variable_minimum.SLDASM")
         and digest(reads.raw(path.parent / "16029_variable_minimum.SLDASM")) == data.get("assemblySha256"),
         "generated_case_assembly_bytes_not_bound")
    reopen, count = data.get("reopen", {}), len(snapshot.instances)
    need(reopen.get("pass") is True and reopen.get("componentCount") == count
         and reopen.get("assemblySha256") == data.get("assemblySha256"), "generated_case_reopen_not_complete")
    parts = data.get("parts", [])
    need(bool(parts), "generated_case_part_inventory_missing")
    for row in parts:
        part = disk_path(row["path"])
        under(part, path.parent)
        need(digest(reads.raw(part)) == row.get("sha256"), "generated_case_part_hash_mismatch:" + str(part))
    return snapshot


def resolve_reused_case(checker, reads, left, right):
    """Return a strict provenance proof; never mutate either checker snapshot."""
    proof = {"applicable": False, "verified": False, "errors": [], "onlyToolchainIdentityCorrected": True}
    try:
        resume_run, target_run = owning_run(left.path), owning_run(right.path)
        job = reads.read(resume_run / "job.json")
        resume = job.get("assemblyResume")
        if resume is None:
            need(same_path(left.data.get("outputDirectory", ""), left.path.parent),
                 "copied_baseline_requires_explicit_assembly_resume_descriptor")
            proof.update(verified=True, reason="baseline_has_no_assembly_resume")
            return proof
        proof["applicable"] = True
        need(resume.get("schema") == RESUME_SCHEMA and resume.get("generateCaseIndices") == [3]
             and resume.get("reuseCaseIndices") == [1, 2] and [row.get("index") for row in resume.get("cases", [])] == [1, 2],
             "exact_two_reused_cases_and_one_generated_case_required")
        relative = under(left.path, resume_run / "cad")
        matches = re.fullmatch(r"assemblies/case([123])/assembly-evidence\.json", relative)
        need(matches is not None, "baseline_must_be_exact_resume_case_evidence_path")
        index = int(matches[1])
        if index == 3:
            need(same_path(left.data.get("outputDirectory", ""), left.path.parent), "generated_case3_actual_directory_required")
            proof.update(applicable=False, verified=True, reason="case3_was_generated_and_uses_original_checker")
            return proof
        old_run = disk_path(resume["sourceRun"])
        data_root = resume_run.parent.parent
        need(resume_run.parent.name == "runs" and old_run.parent == resume_run.parent == target_run.parent
             and len({old_run, resume_run, target_run}) == 3, "three_distinct_direct_runs_required")
        proof.update(caseIndex=index, sourceRun=str(old_run), resumedRun=str(resume_run), targetRun=str(target_run))
        for flag in ("mixedProvenance", "technicalRecoveryOnly", "recoveredCasesFromFailedPhase", "oldFailurePreserved"):
            need(resume.get(flag) is True, "resume_scope_flag_not_true:" + flag)
        for flag in ("oldSupervisionAccepted", "acceptancePass", "fullPlanPass", "production_release"):
            need(resume.get(flag) is False, "resume_scope_flag_not_false:" + flag)
        sources = resume.get("sourceEvidence", [])
        need(len(sources) == len(SOURCE_EVIDENCE)
             and {disk_path(row["path"]).name for row in sources} == SOURCE_EVIDENCE, "resume_source_evidence_membership_not_exact")
        for row in sources:
            source = disk_path(row["path"])
            need(source.parent == old_run and digest(reads.raw(source)) == row.get("sha256"),
                 "resume_original_source_evidence_hash_mismatch:" + str(source))
        old_job, old_native = run_proof(reads, old_run, "assembly", failed=True)
        new_job, new_native = run_proof(reads, resume_run, "assembly")
        target_job, target_native = run_proof(reads, target_run, "relocate")
        need(not old_job.get("assemblyResume") and target_job.get("assemblyResume") == resume,
             "resume_descriptor_not_bound_across_runs")
        for field in ("doors", "layouts", "specification", "process", "inputVersions", "recovery"):
            need(old_job.get(field) is not None and old_job[field] == new_job.get(field) == target_job.get(field),
                 "source_build_input_changed:" + field)
        need(len(job["layouts"]) == 3, "exact_three_layouts_required")
        recovery = job["recovery"]
        need(recovery.get("oldFailurePreserved") is True and recovery.get("oldSupervisionAccepted") is False
             and recovery.get("fullPlanPass") is False and recovery.get("production_release") is False
             and bool(recovery.get("sources")), "original_output_recovery_scope_missing")
        for row in recovery["sources"]:
            need(digest(reads.raw(disk_path(row["path"]))) == row.get("sha256"), "original_output_recovery_source_hash_mismatch")
        for run in (resume_run, target_run):
            check = reads.read(run / "recovery-source-check.json")
            need(all(check.get(key) == value for key, value in recovery.items())
                 and same_path(check.get("newRun", ""), run) and check.get("outputsUnchanged") is True
                 and check.get("sourcesUnchanged") is True, "recovery_source_check_missing_or_unbound")
            if run == target_run:
                need(check.get("assemblyResume") == resume, "target_recovery_resume_binding_missing")
        source_check_path = resume_run / "assembly-resume-source-check.json"
        source_check = reads.read(source_check_path)
        pointer = reads.read(data_root / "recovery-T3-current.json")
        need(same_path(pointer.get("dir", ""), resume_run) and same_path(pointer.get("cadDirectory", ""), resume_run / "cad")
             and same_path(pointer.get("nativeResult", ""), resume_run / "native-result.json")
             and same_path(pointer.get("resumeSourceRun", ""), old_run) and pointer.get("mixedProvenance") is True
             and pointer.get("production_release") is False
             and pointer.get("resumeSourceCheckSha256") == digest(reads.raw(source_check_path)), "resume_source_check_pointer_hash_not_bound")
        target_pointer = reads.read(data_root / "recovery-T4-current.json")
        need(same_path(target_pointer.get("dir", ""), target_run)
             and same_path(target_pointer.get("cadDirectory", ""), target_run / "cad")
             and same_path(target_pointer.get("nativeResult", ""), target_run / "native-result.json")
             and same_path(target_pointer.get("resumeSourceRun", ""), old_run)
             and target_pointer.get("mixedProvenance") is True and target_pointer.get("production_release") is False,
             "target_recovery_pointer_not_bound")
        need(all(source_check.get(key) == value for key, value in resume.items() if key != "cases")
             and same_path(source_check.get("newRun", ""), resume_run)
             and source_check.get("sourceEvidenceUnchanged") is True and source_check.get("sourceCadFilesUnchanged") is True
             and source_check.get("jobSha256") == digest(reads.raw(resume_run / "job.json"))
             and source_check.get("nativeResultSha256") == digest(reads.raw(resume_run / "native-result.json")),
             "completed_resume_source_check_not_bound")
        old_inventory, new_inventory, target_inventory = (reads.inventory(run / "cad") for run in (old_run, resume_run, target_run))
        need(row_map(source_check.get("sourceInventory", [])) == old_inventory
             and row_map(source_check.get("finalCadInventory", [])) == new_inventory, "completed_source_or_final_inventory_changed")
        verify_copy(reads, resume_run, old_run, old_inventory, new_inventory,
                    excluded=("assemblies/case3",), added=("assembly-resume-receipt.json",))
        diagnostics = relocated_flat_diagnostics(reads, target_run, target_job, target_native, new_inventory, target_inventory)
        verify_copy(reads, target_run, resume_run, new_inventory, target_inventory,
                    added=tuple("assemblies/case" + str(i) + "/relocation-evidence.json" for i in (1, 2, 3))
                    + tuple(diagnostics["addedRelativePaths"]))
        receipt = reads.read(resume_run / "cad" / "assembly-resume-receipt.json")
        need(receipt.get("schema") == "16029-variable-assembly-resume-receipt/v1"
             and same_path(receipt.get("sourceRun", ""), old_run) and receipt.get("mixedProvenance") is True
             and receipt.get("technicalRecoveryOnly") is True and receipt.get("production_release") is False
             and receipt.get("acceptancePass") is False, "resume_native_receipt_scope_not_bound")
        receipts = {}
        for row in receipt.get("files", []):
            key = under(disk_path(row["path"]), resume_run / "cad").casefold()
            need(key not in receipts, "duplicate_resume_receipt_file")
            receipts[key] = row
        expected_receipts = set()
        checked_cases = source_check.get("cases", [])
        need([row.get("index") for row in checked_cases] == [1, 2, 3], "completed_three_case_descriptors_required")
        old_events = [event for event in old_native["events"] if event.get("inspection") == "minimum_assembly"]
        need([event.get("index") for event in old_events] == [1, 2, 3]
             and old_events[2].get("result", {}).get("success") is False
             and old_events[2]["result"].get("layout") == job["layouts"][2]
             and old_events[2]["result"].get("acceptancePass") is False
             and old_events[2]["result"].get("production_release") is False
             and "ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED" in old_events[2]["result"].get("error", ""),
             "old_complete_two_cases_and_specific_failed_third_required")
        for descriptor in resume["cases"]:
            i = descriptor["index"]
            prefix = "assemblies/case" + str(i) + "/"
            expected_relative = prefix + "assembly-evidence.json"
            need(descriptor.get("evidenceRelativePath") == expected_relative
                 and descriptor.get("layout") == job["layouts"][i - 1], "reused_case_identity_or_layout_mismatch")
            old_case = {key: row for key, row in old_inventory.items() if key.startswith(prefix)}
            new_case = {key: row for key, row in new_inventory.items() if key.startswith(prefix)}
            checked = checked_cases[i - 1]
            need(row_map(descriptor.get("inventory", [])) == old_case and set(old_case) == set(new_case)
                 and {key: value for key, value in checked.items() if key != "inventory"}
                 == {key: value for key, value in descriptor.items() if key != "inventory"}
                 and row_map(checked.get("inventory", [])) == new_case,
                 "reused_case_inventory_descriptor_mismatch")
            evidence_path = old_run / "cad" / Path(expected_relative)
            evidence = reads.read(evidence_path)
            need(old_events[i - 1].get("result") == evidence
                 and digest(reads.raw(evidence_path)) == descriptor.get("evidenceSha256")
                 and digest(reads.raw(resume_run / "cad" / Path(expected_relative))) == descriptor.get("evidenceSha256")
                 and evidence.get("assemblySha256") == descriptor.get("assemblySha256"), "reused_case_evidence_hash_or_native_binding_mismatch")
            complete_case(checker, reads, evidence_path, evidence, descriptor["layout"])
            for key, before in old_case.items():
                expected_receipts.add(key)
                row = receipts.get(key, {})
                need(row.get("index") == i and same_path(row.get("sourcePath", ""), old_run / "cad" / Path(before["relativePath"]))
                     and row.get("sourceSha256") == row.get("copiedSha256") == before["sha256"]
                     and row.get("rewiredSha256") == new_case[key]["sha256"], "resume_receipt_source_copy_or_rewire_hash_mismatch:" + key)
        need(set(receipts) == expected_receipts, "resume_receipt_membership_not_exact")
        new_events = new_native["events"]
        for inspection in ("resume_copied_bytes_verified", "resume_rewired_copies_bound"):
            rows = [event for event in new_events if event.get("inspection") == inspection]
            need(len(rows) == 1 and same_path(rows[0].get("sourceRun", ""), old_run)
                 and rows[0].get("reusedCaseCount") == 2 and rows[0].get("production_release") is False,
                 "resume_native_byte_copy_or_rewire_event_missing:" + inspection)
        reused = [event for event in new_events if event.get("inspection") == "minimum_assembly_reused"]
        need([event.get("index") for event in reused] == [1, 2], "exact_two_native_reuse_events_required")
        for event, descriptor in zip(reused, resume["cases"]):
            need(same_path(event.get("evidencePath", ""), resume_run / "cad" / Path(descriptor["evidenceRelativePath"]))
                 and event.get("evidenceSha256") == descriptor["evidenceSha256"] and event.get("oldFailurePreserved") is True
                 and event.get("acceptancePass") is False and event.get("production_release") is False, "native_reuse_case_binding_mismatch")
        generated = [event for event in new_events if event.get("inspection") == "minimum_assembly"]
        third = checked_cases[2]
        third_path = resume_run / "cad" / "assemblies/case3/assembly-evidence.json"
        need(len(generated) == 1 and generated[0].get("index") == 3
             and third.get("evidenceRelativePath") == "assemblies/case3/assembly-evidence.json"
             and third.get("layout") == job["layouts"][2]
             and third.get("evidenceSha256") == digest(reads.raw(third_path))
             and row_map(third.get("inventory", [])) == {key: row for key, row in new_inventory.items() if key.startswith("assemblies/case3/")},
             "resume_only_case3_generation_and_inventory_required")
        complete_case(checker, reads, third_path, generated[0]["result"], third["layout"])
        need(third.get("assemblySha256") == generated[0]["result"].get("assemblySha256"), "generated_third_assembly_hash_not_bound")
        old_tool, resumed_tool, target_tool = (toolchain_proof(checker, reads, run, data_root)
                                                 for run in (old_run, resume_run, target_run))
        relocated = [event for event in target_native["events"] if event.get("inspection") == "relocated_assembly"]
        need(len(relocated) == 3 and {PureWindowsPath(event.get("path", "")).name for event in relocated}
             == {"case1", "case2", "case3"}, "all_three_actual_target_relocations_required")
        for event in relocated:
            folder = disk_path(event["path"])
            need(folder.parent == target_run / "cad" / "assemblies", "target_relocation_case_path_invalid")
            path = folder / "relocation-evidence.json"
            raw = reads.raw(path)
            data = json.loads(raw.decode("utf-8-sig"))
            snapshot = checker.Snapshot(data, path, raw, purpose="relocation")
            runtime = checker.relocation_runtime_proof(target_run, path, data, target_tool)
            need(event.get("result") == data and not snapshot.errors and not snapshot.metadata_errors
                 and not snapshot.runtime_lineage.get("errors") and runtime.get("verified") is True,
                 "complete_target_cold_readback_or_runtime_failed:" + folder.name)
        need(not left.runtime_lineage.get("errors") and not right.runtime_lineage.get("errors"), "original_lineage_errors_cannot_be_corrected")
        for snapshot, expected in ((left, [resume_run, old_run]), (right, [target_run, resume_run, old_run])):
            chain = snapshot.runtime_lineage.get("copyChain", [])
            need(len(chain) >= len(expected) and all(same_path(row.get("run", ""), run) for row, run in zip(chain, expected)),
                 "observed_lineage_does_not_contain_exact_resume_hops")
        need(all(old_tool["engineeringMethodSha256"][name] == target_tool["engineeringMethodSha256"][name] for name in MEASUREMENT_METHODS)
             and old_tool["measurementHelperSha256"] == target_tool["measurementHelperSha256"], "actual_old_and_target_measurement_methods_changed")
        descriptor = resume["cases"][index - 1]
        need(left.sha256 == descriptor["evidenceSha256"] and right.data.get("sourceEvidenceSha256") == left.sha256
             and same_path(right.path, target_run / "cad" / "assemblies" / ("case" + str(index)) / "relocation-evidence.json"),
             "baseline_and_candidate_do_not_bind_same_reused_case")
        proof.update(verified=True, actualBuildToolchain=old_tool, actualBaselineMeasurementToolchain=old_tool,
                     actualCandidateMeasurementToolchain=target_tool, resumedRunToolchain=resumed_tool,
                     sourceCheck={"path": str(source_check_path), "sha256": digest(reads.raw(source_check_path))},
                     originalBatchStillFailed=True, reusedCasesWereNotRegenerated=True,
                     sourceGenerationDifferences=[name for name in old_tool["engineeringMethodSha256"]
                                                  if old_tool["engineeringMethodSha256"][name] != resumed_tool["engineeringMethodSha256"][name]]
                     + [name for name in old_tool["generationAuxiliarySha256"]
                        if old_tool["generationAuxiliarySha256"][name] != resumed_tool["generationAuxiliarySha256"][name]],
                     inventoryFileCounts={"oldSource": len(old_inventory), "resumedT3": len(new_inventory), "targetT4": len(target_inventory)},
                     flatReopenDiagnostics=diagnostics,
                     exactSourceCopyAndRewireReceiptVerified=True, allThreeTargetCasesColdReadbackVerified=True)
    except (OSError, ValueError, KeyError, TypeError, StopIteration) as error:
        proof["errors"].append(str(error))
    return proof


class SnapshotView:
    """Delegate every original validation surface except the proven toolchain identity."""

    def __init__(self, snapshot, lineage):
        self.snapshot = snapshot
        self.runtime_lineage = lineage

    def __getattr__(self, name):
        return getattr(self.snapshot, name)


def compare_files(baseline, candidate):
    checker, reads = load_checker(), EvidenceReads()
    reads.raw(Path(checker.__file__))
    wrapper_raw = reads.raw(Path(__file__))
    baseline, candidate = baseline.resolve(), candidate.resolve()
    left_raw, right_raw = reads.raw(baseline), reads.raw(candidate)
    left = checker.Snapshot(json.loads(left_raw.decode("utf-8-sig")), baseline, left_raw, purpose="relocation")
    right = checker.Snapshot(json.loads(right_raw.decode("utf-8-sig")), candidate, right_raw, purpose="relocation")
    observed = checker.compare_snapshots(copy.deepcopy(left), copy.deepcopy(right), "relocation")
    provenance = resolve_reused_case(checker, reads, left, right)
    a, b = copy.deepcopy(left.runtime_lineage), copy.deepcopy(right.runtime_lineage)
    if provenance["applicable"] and provenance["verified"]:
        a["buildToolchain"] = provenance["actualBuildToolchain"]
        a["measurementToolchain"] = provenance["actualBaselineMeasurementToolchain"]
        b["buildToolchain"] = provenance["actualBuildToolchain"]
        b["measurementToolchain"] = provenance["actualCandidateMeasurementToolchain"]
    result = checker.compare_snapshots(SnapshotView(left, a), SnapshotView(right, b), "relocation")
    result["resumeCaseProvenance"] = provenance
    result["observedLineage"] = {"pass": observed["pass"], "validationStatus": observed["validationStatus"],
                                  "runtimeProof": observed["sourceLineage"]["runtimeProof"],
                                  "purpose": "Frozen checker attribution by containing run; retained without error suppression"}
    result["correctedLineagePurpose"] = ("Actual old build and baseline measurement source; T4 actual measurement source. "
                                         "Only proven reused-case toolchain identity changes; all original checks and errors remain.")
    result["tool"] = {"path": str(Path(__file__).resolve()), "sha256": digest(wrapper_raw),
                      "frozenCheckerPath": checker.__file__, "frozenCheckerSha256": CHECKER_SHA256}
    result["technicalRecoveryOnly"] = True
    try:
        result["evidenceFilesUnchangedDuringComparison"] = reads.unchanged()
    except (OSError, ValueError) as error:
        result["evidenceFilesUnchangedDuringComparison"] = False
        provenance["errors"].append(str(error))
        provenance["verified"] = False
    result["provenanceReferences"] = sorted(reads.files.values(), key=lambda row: row["path"])
    if not provenance["verified"] or not result["evidenceFilesUnchangedDuringComparison"]:
        result["originalComparisonValidationStatus"] = result["validationStatus"]
        result.update({"pass": False, "validationStatus": "unverified_resumed_case_provenance",
                       "relocationValidationStatus": "unverified_resumed_case_provenance", "relocationMeasurementEquivalent": False})
        result["requiredNextEvidence"] = ["Resolve the exact reported resume source/copy/rewire/runtime binding gaps.",
                                          "This report never approves engineering acceptance or production release."]
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--purpose", choices=("relocation",), default="relocation")
    parser.add_argument("--out", type=Path, help="New JSON report; existing files are never overwritten")
    args = parser.parse_args()
    result = compare_files(args.baseline, args.candidate)
    if args.out:
        target = args.out.resolve()
        need(target not in (args.baseline.resolve(), args.candidate.resolve()), "report_must_not_replace_input")
        with target.open("x", encoding="utf-8", newline="\n") as output:
            json.dump(result, output, ensure_ascii=False, indent=2, allow_nan=False)
            output.write("\n")
        print(json.dumps({"report": str(target), "pass": result["pass"], "validationStatus": result["validationStatus"]}))
    else:
        print(json.dumps(result, ensure_ascii=True, indent=2, allow_nan=False))
    return 0 if result["pass"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
