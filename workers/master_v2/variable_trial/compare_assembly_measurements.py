"""Compare native assembly measurements without opening or changing CAD.

Readback equivalence is assessed for the explicit cache or relocation purpose.
Mechanical acceptance and production release remain false on success.
Exit 0: requested comparison verified; 1: mismatch/invalid evidence;
2: recorded measurements agree but the requested proof remains incomplete.
"""
import argparse
import collections
import copy
import hashlib
import json
import math
import ntpath
import sys
from pathlib import Path, PureWindowsPath


MM = 0.001
DIRECTION = 1e-7
TRANSFORM = 1e-8
NUMERIC_ABS = 1e-6
NUMERIC_REL = 1e-9
SCHEMAS = {
    "16029-built-assembly-measurement/v1",
    "16029-independent-variable-minimum-assembly/v1",
}
RELOCATION_SCHEMA = "16029-variable-relocation/v1"
METADATA = {
    "nativeFaceId", "nativeEdgeId", "curveTag", "identityScope",
    "sourceFaceIdentity", "sourceNativeFaceId", "sourceEdgeIdentity",
    "sourceNativeEdgeId", "sourceCurveTag",
    "independentNativeTopologyReadbackForThisInstance", "topologyReadback",
    "componentNameBeforeRename", "componentNameAfterRename",
    "componentNameAfterReopen",
}
REFERENCES = {
    "faceIdentity", "edgeIdentity", "holeFaceIdentity", "candidateFaceIdentity",
    "tongueEdgeIdentity", "lockHoleEdgeIdentity",
}
COUNTS = {
    "index", "boundaryEdges", "seamEdges", "heightUnits",
    "columnDoorCounts", "requestedDoorIndices",
}
ANCHORS = (
    ("identity",), ("componentA",), ("componentB",), ("component",),
    ("side",), ("index",), ("kind",), ("end",), ("faceIdentity",),
    ("edgeIdentity",), ("holeFaceIdentity",), ("candidateFaceIdentity",),
    ("tongueEdgeIdentity",), ("lockHoleEdgeIdentity",),
    ("owner", "component"), ("bushingOwner", "component"),
    ("actualPinOwner", "component"), ("surface", "faceIdentity"),
    ("plane", "faceIdentity"), ("panelFace", "faceIdentity"),
    ("stiffenerFace", "faceIdentity"),
)


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def tolerance(key, index=None, a=0, b=0):
    if key == "transform":
        return TRANSFORM, "transform"
    if key == "circleParams":
        return (1e-6, "metres") if index in (0, 1, 2, 6) else (DIRECTION, "direction")
    if key == "planeParams":
        return (1e-6, "metres") if index in (3, 4, 5) else (DIRECTION, "direction")
    if key == "uvBounds":
        return (1e-6, "metres") if index in (2, 3) else (DIRECTION, "radians")
    if key in ("edgeParameterRange", "parameterSpanRadians"):
        return DIRECTION, "radians"
    if "Count" in key or key in COUNTS or "Tolerance" in key:
        return 0, "exact"
    if key.endswith(("Mm3", "Mm2")):
        return max(NUMERIC_ABS, NUMERIC_REL * max(abs(a), abs(b))), "numeric_precision"
    if key.lower().endswith("mm") or key.endswith("XyMm"):
        return MM, "millimetres"
    if any(word in key.lower() for word in ("normal", "direction", "axisdot")) or key.endswith("Dot"):
        return DIRECTION, "direction"
    return 0, "exact"


def anchor(value):
    if not isinstance(value, dict):
        return type(value).__name__
    result = [("keys", tuple(sorted(value)))]
    for selector in ANCHORS:
        current = value
        for key in selector:
            if not isinstance(current, dict) or key not in current:
                break
            current = current[key]
        else:
            if isinstance(current, (str, bool, int)):
                result.append(("/".join(selector), current))
    return tuple(result)


class Comparison:
    def __init__(self):
        self.differences = []
        self.difference_count = 0
        self.numeric_maxima = collections.defaultdict(float)
        self.compared_numbers = 0

    def fail(self, path, reason, a=None, b=None, report=True):
        if report:
            self.difference_count += 1
            if len(self.differences) < 200:
                self.differences.append({"path": path, "reason": reason,
                                         "baseline": a, "candidate": b})
        return False

    def matching(self, left, right, path, key):
        groups = collections.defaultdict(list)
        for j, item in enumerate(right):
            groups[anchor(item)].append(j)
        edges = {}
        for i, item in enumerate(left):
            edges[i] = [j for j in groups[anchor(item)]
                        if self.equal(item, right[j], path, key, False)]
        owners = {}

        def assign(i, seen):
            for j in edges[i]:
                if j in seen:
                    continue
                seen.add(j)
                if j not in owners or assign(owners[j], seen):
                    owners[j] = i
                    return True
            return False

        for i in sorted(edges, key=lambda k: len(edges[k])):
            assign(i, set())
        pairs = sorted((i, j) for j, i in owners.items())
        return pairs, sorted(set(range(len(left))) - {i for i, _ in pairs}), sorted(set(range(len(right))) - set(owners))

    def equal(self, a, b, path="", key="", report=True, index=None):
        if isinstance(a, bool) or isinstance(b, bool) or a is None or b is None:
            return (type(a) is type(b) and a == b) or self.fail(path, "boolean_or_null_changed", a, b, report)
        if number(a) and number(b):
            if not math.isfinite(a) or not math.isfinite(b):
                return self.fail(path, "nonfinite_number", a, b, report)
            limit, unit = tolerance(key, index, a, b)
            delta = abs(a - b)
            if report:
                self.compared_numbers += 1
                self.numeric_maxima[unit] = max(self.numeric_maxima[unit], delta)
            same_zero_state = not key.endswith("Mm3") or ((a == 0) == (b == 0))
            valid = delta < limit if key == "transform" else delta <= limit
            return (same_zero_state and valid) or self.fail(
                path, "numeric_delta_exceeds_contract" if same_zero_state else "zero_and_nonzero_volume_differ",
                {"value": a, "tolerance": limit, "unit": unit},
                {"value": b, "absoluteDelta": delta}, report)
        if type(a) is not type(b):
            return self.fail(path, "type_changed", type(a).__name__, type(b).__name__, report)
        if isinstance(a, dict):
            ok = True
            if set(a) != set(b):
                ok = self.fail(path, "field_set_changed", sorted(a), sorted(b), report)
                if not report:
                    return False
            for name in sorted(set(a) & set(b)):
                same = self.equal(a[name], b[name], path + "/" + name, name, report)
                ok = same and ok
                if not same and not report:
                    return False
            return ok
        if isinstance(a, list):
            ok = True
            if len(a) != len(b):
                ok = self.fail(path, "list_count_changed", len(a), len(b), report)
                if not report:
                    return False
            unordered = bool(a or b) and all(isinstance(v, (dict, list)) for v in a + b)
            if unordered:
                pairs, missing, extra = self.matching(a, b, path, key)
                if missing or extra:
                    ok = self.fail(path, "unmatched_unordered_items",
                                   [{"index": i, "anchor": repr(anchor(a[i]))[:400]} for i in missing[:8]],
                                   [{"index": i, "anchor": repr(anchor(b[i]))[:400]} for i in extra[:8]], report)
                    if not report:
                        return False
                if not report:
                    return ok
                for i, j in pairs:
                    same = self.equal(a[i], b[j], path + "/" + str(i), key, report)
                    ok = same and ok
                return ok
            for i, (left, right) in enumerate(zip(a, b)):
                same = self.equal(left, right, path + "/" + str(i), key, report, i)
                ok = same and ok
                if not same and not report:
                    return False
            return ok
        return a == b or self.fail(path, "value_changed", a, b, report)


def json_file(path):
    raw = path.read_bytes()
    return json.loads(raw.decode("utf-8-sig")), digest(raw)


def engineering_sections(source):
    result = {}
    source = source.replace("\r\n", "\n")
    methods = (
        "Build", "ValidateLayout", "PrepareStructure", "PrepareHardware",
        "PreparePlacements", "BuildNativeAssembly", "ValidateWorldPanel",
        "ValidateWorldBoundary", "MeasureHingeInterfaces", "MeasureHingeSupportChains",
        "MeasureInterference", "Bounds", "CaptureBoundaryCircle", "CheckFeatureHealth",
    )
    for name in methods:
        lines = source.splitlines(keepends=True)
        starts = [i for i, line in enumerate(lines)
                  if line.startswith("    ") and not line.startswith("     ")
                  and "(" in line and line.split("(", 1)[0].rstrip().endswith(" " + name)]
        if len(starts) != 1:
            raise ValueError("engineering_method_missing_or_ambiguous:" + name)
        start = starts[0]
        end = next(i for i in range(start + 1, len(lines)) if lines[i].rstrip("\n") == "    }")
        result[name] = digest("".join(lines[start:end+1]).encode("utf-8"))
    return result


def toolchain_proof(run):
    data, sha = json_file(run / "toolchain.json")
    candidates = [v for v in data.get("sources", [])
                  if PureWindowsPath(v.get("path", "")).name == "TrialAssembly.cs"]
    if len(candidates) != 1:
        raise ValueError("one_archived_TrialAssembly_source_required")
    entry = candidates[0]
    archive = Path(str(PureWindowsPath(entry["archivePath"])))
    raw = archive.read_bytes()
    actual = digest(raw)
    if actual != entry.get("sha256"):
        raise ValueError("archived_TrialAssembly_hash_mismatch")
    return {"toolchainPath": str(run / "toolchain.json"), "toolchainSha256": sha,
            "archivePath": str(archive), "sourceSha256": actual,
            "engineeringMethodSha256": engineering_sections(raw.decode("utf-8-sig"))}


def relocation_runtime_proof(run, evidence_path, evidence, toolchain):
    conditions, references = {}, {}

    def read(name):
        value, sha = json_file(run / name)
        references[name] = {"path": str(run / name), "sha256": sha}
        return value

    job, native, manifest = read("job.json"), read("native-result.json"), read("toolchain.json")
    events = native.get("events", [])
    owned = [v for v in events if v.get("inspection") == "cad_owned"]
    finished = [v for v in events if v.get("inspection") == "finished"]
    conditions["jobAndNativeActionRelocate"] = job.get("action") == native.get("action") == "relocate"
    conditions["owningRunPathsBound"] = all(
        PureWindowsPath(job.get(field, "")) == PureWindowsPath(str(expected))
        for field, expected in (("cadDirectory", run / "cad"), ("resultPath", run / "native-result.json"),
                                ("toolchainPath", run / "toolchain.json")))
    conditions["nativeProductionReleaseFalse"] = native.get("production_release") is False
    conditions["uniqueFinishedLastAndExited"] = (len(finished) == 1 and bool(events) and events[-1] == finished[0]
                                                  and finished[0].get("success") is True
                                                  and finished[0].get("cadExited") is True
                                                  and finished[0].get("error") is None)
    conditions["noNativeFailureEvent"] = not any(v.get("inspection") in ("failure", "native_guard_retained") for v in events)
    claim = owned[0] if len(owned) == 1 else {}
    conditions["uniqueCadOwnershipMatchesNativeTop"] = (
        len(owned) == 1 and type(claim.get("pid")) is int and claim["pid"] > 0
        and type(claim.get("startTicks")) is int and claim["startTicks"] > 0
        and claim.get("pid") == native.get("cadPid") and claim.get("startTicks") == native.get("cadTicks")
        and bool(claim.get("executable"))
        and ntpath.normcase(claim.get("executable", "")) == ntpath.normcase(native.get("cadExe", "")))
    conditions["cadExecutableIsSw2020"] = (ntpath.normcase(claim.get("executable", ""))
                                             == ntpath.normcase(r"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe")
                                             and str(claim.get("revision", "")).startswith("28."))
    relocated = [v for v in events if v.get("inspection") == "relocated_assembly"
                 and PureWindowsPath(v.get("path", "")) == PureWindowsPath(evidence.get("outputDirectory", ""))]
    conditions["nativeRecordedExactRelocationEvidence"] = len(relocated) == 1 and relocated[0].get("result") == evidence
    conditions["evidenceBelongsToOwningCadDirectory"] = (PureWindowsPath(str(evidence_path))
                                                         == PureWindowsPath(evidence.get("outputDirectory", "")) / "relocation-evidence.json")
    exe = Path(str(PureWindowsPath(manifest.get("exe", ""))))
    conditions["runToolchainSchemaAndExecutable"] = (manifest.get("schema") == "winnsen.variable_trial.toolchain.v2"
                                                       and exe.name == "VariableTrial.exe"
                                                       and exe.is_file() and digest(exe.read_bytes()) == manifest.get("sha256"))
    sources = manifest.get("sources", [])
    expected_sources = {"TrialAssembly.cs", "TrialOutput.cs", "VariableTrial.cs"}
    source_names = [PureWindowsPath(v.get("archivePath", "")).name for v in sources]
    archives_verified = len(sources) == len(expected_sources) and set(source_names) == expected_sources
    for row in sources:
        archive = Path(str(PureWindowsPath(row.get("archivePath", ""))))
        archives_verified = (archives_verified and archive == exe.parent / "sources" / archive.name
                             and archive.is_file() and digest(archive.read_bytes()) == row.get("sha256"))
    conditions["runToolchainArchivedSourcesVerified"] = archives_verified
    dlls = manifest.get("dlls", [])
    expected_dlls = {"SolidWorks.Interop.sldworks.dll", "SolidWorks.Interop.swconst.dll"}
    dll_names = [PureWindowsPath(v.get("path", "")).name for v in dlls]
    dlls_verified = len(dlls) == len(expected_dlls) and set(dll_names) == expected_dlls
    for row in dlls:
        dll = Path(str(PureWindowsPath(row.get("path", ""))))
        dlls_verified = (dlls_verified and dll == exe.parent / dll.name and dll.is_file()
                         and digest(dll.read_bytes()) == row.get("sha256"))
    conditions["runToolchainInteropFilesVerified"] = dlls_verified
    conditions["measurementArchiveBoundToRunToolchain"] = references["toolchain.json"]["sha256"] == toolchain.get("toolchainSha256")
    result, supervision = read("result.json"), read("supervision.json")
    conditions["runnerCompletedAfterCadExit"] = type(result.get("code")) is int and result["code"] == 0 and result.get("cadExited") is True
    conditions["supervisorObservedCadExit"] = (supervision.get("state") == "cad_exited"
                                               and supervision.get("nativeEnded") is True and supervision.get("cad") == [])
    return {"verified": all(conditions.values()), "conditions": conditions, "references": references,
            "cadOwnership": {k: claim.get(k) for k in ("pid", "startTicks", "startedAtUtc", "executable", "revision")},
            "owningRun": str(run)}


def runtime_lineage(path, relocation_evidence=None):
    proof = {"copyChain": [], "rootInputs": [], "errors": []}
    if path is None:
        proof["errors"].append("runtime_lineage_missing")
        return proof
    runs = [p for p in path.parents if (p / "job.json").is_file() and (p / "cad").is_dir()]
    if not runs:
        proof["errors"].append("owning_run_not_found")
        return proof
    run, visited, build_job = runs[0], set(), None
    try:
        proof["measurementToolchain"] = toolchain_proof(run)
        if relocation_evidence is not None:
            proof["relocationRuntime"] = relocation_runtime_proof(run, path, relocation_evidence, proof["measurementToolchain"])
            if not proof["relocationRuntime"]["verified"]:
                proof["errors"].extend("relocation_runtime_" + key for key, passed in proof["relocationRuntime"]["conditions"].items() if not passed)
        while (run / "copy-manifest.json").is_file():
            if run in visited:
                raise ValueError("copy_lineage_cycle")
            visited.add(run)
            manifest, manifest_sha = json_file(run / "copy-manifest.json")
            job, job_sha = json_file(run / "job.json")
            source = Path(str(PureWindowsPath(manifest["source"])))
            target = Path(str(PureWindowsPath(manifest["target"])))
            if target.resolve() != (run / "cad").resolve():
                raise ValueError("copy_manifest_target_mismatch")
            if job.get("sourceCadDirectory") and Path(str(PureWindowsPath(job["sourceCadDirectory"]))).resolve() != source.resolve():
                raise ValueError("job_copy_source_mismatch")
            protection, protection_sha = json_file(run / "protection-result.json")
            if protection.get("pass") is not True or protection.get("changed") != []:
                raise ValueError("copy_ancestor_protection_failed:" + str(run))
            native, native_sha = json_file(run / "native-result.json")
            rewrites = [v for v in native.get("events", []) if v.get("inspection") == "rewrite_reference"]
            if any(v.get("success") is not True for v in rewrites):
                raise ValueError("reference_rewrite_failed:" + str(run))
            closure = [v for v in native.get("events", []) if v.get("inspection") == "dependency_closure"]
            proof["copyChain"].append({"run": str(run), "action": job.get("action"),
                                       "manifestSha256": manifest_sha, "jobSha256": job_sha,
                                       "source": str(source), "target": str(target),
                                       "copiedFileCount": len(manifest.get("files", [])),
                                       "assemblyEvidenceCopies": [v for v in manifest.get("files", [])
                                                                  if PureWindowsPath(v.get("relativePath", "")).name.casefold() == "assembly-evidence.json"],
                                       "nativeResultSha256": native_sha,
                                       "successfulRewriteCount": len(rewrites),
                                       "dependencyClosure": closure, "protectionSha256": protection_sha,
                                       "protectedFilesUnchanged": True})
            if build_job is None and job.get("action") == "assembly":
                build_job = job
                proof["buildToolchain"] = toolchain_proof(run)
                proof["specification"] = job.get("specification")
                proof["process"] = job.get("process")
                proof["inputVersions"] = sorted(
                    [{"basename": PureWindowsPath(v["path"]).name.casefold(), "sha256": v["sha256"]}
                     for v in job.get("inputVersions", [])], key=lambda v: v["basename"])
            root_rows = [v for v in manifest.get("files", [])
                         if not PureWindowsPath(v.get("relativePath", "")).parent.parts
                         and PureWindowsPath(v.get("relativePath", "")).suffix.upper() in (".SLDPRT", ".SLDASM", ".SLDDRW")]
            proof["rootSourceDirectory"] = str(source)
            proof["rootInputs"] = sorted(
                [{"basename": PureWindowsPath(v["relativePath"]).name.casefold(),
                  "size": v["size"], "sha256": v["sha256"]} for v in root_rows], key=lambda v: v["basename"])
            next_run = source.parent
            if not (next_run / "copy-manifest.json").is_file():
                for row in root_rows:
                    seed = source / row["relativePath"]
                    if seed.stat().st_size != row["size"] or digest(seed.read_bytes()) != row["sha256"]:
                        raise ValueError("original_root_seed_hash_mismatch:" + str(seed))
                break
            run = next_run
        if len(proof["rootInputs"]) != 60:
            raise ValueError("complete_60_original_root_inputs_required")
        if build_job is None or proof.get("specification") is None or proof.get("process") is None or not proof.get("inputVersions"):
            raise ValueError("assembly_build_specification_lineage_missing")
    except (OSError, ValueError, KeyError, StopIteration) as error:
        proof["errors"].append(str(error))
    return proof


def lineage_comparison(left, right):
    a, b = left.runtime_lineage, right.runtime_lineage
    generation_methods = ("Build", "ValidateLayout", "PrepareStructure", "PrepareHardware", "PreparePlacements", "BuildNativeAssembly")
    measurement_methods = ("ValidateWorldPanel", "ValidateWorldBoundary", "MeasureHingeInterfaces", "MeasureHingeSupportChains", "MeasureInterference", "Bounds", "CaptureBoundaryCircle", "CheckFeatureHealth")
    def same_methods(section, names):
        aa, bb = a.get(section, {}).get("engineeringMethodSha256", {}), b.get(section, {}).get("engineeringMethodSha256", {})
        return bool(aa) and bool(bb) and all(aa.get(k) is not None and aa.get(k) == bb.get(k) for k in names)
    conditions = {
        "rootSourceDirectoryEqual": ntpath.normcase(a.get("rootSourceDirectory", "")) == ntpath.normcase(b.get("rootSourceDirectory", "")) and bool(a.get("rootSourceDirectory")),
        "original60RootSeedHashesEqual": bool(a.get("rootInputs")) and a.get("rootInputs") == b.get("rootInputs"),
        "buildSpecificationEqual": a.get("specification") is not None and a.get("specification") == b.get("specification"),
        "buildProcessEqual": a.get("process") is not None and a.get("process") == b.get("process"),
        "inputVersionHashesEqual": bool(a.get("inputVersions")) and a.get("inputVersions") == b.get("inputVersions"),
        "generationEngineeringMethodsUnchanged": same_methods("buildToolchain", generation_methods),
        "measurementAndAcceptanceMethodsUnchanged": same_methods("measurementToolchain", measurement_methods),
        "allLineageProofsAvailable": not a.get("errors") and not b.get("errors"),
    }
    return {"verified": all(conditions.values()), "conditions": conditions,
            "baseline": a, "candidate": b,
            "derivedFileByteDifferencesAreNotAssumedToBeMetadataOnly": True}


class Snapshot:
    def __init__(self, data, path=None, raw=None, verify_files=True, purpose="cache"):
        self.data, self.path = data, path
        self.purpose = purpose
        self.sha256 = digest(raw) if raw is not None else None
        self.byte_count = len(raw) if raw is not None else None
        self.errors, self.references = [], {}
        self.instances, self.names, self.manifest = {}, {}, {}
        self.actual_hashes = {}
        self.lineage = []
        self.lineage_reference = None
        self.source_evidence = None
        self.document_metadata = {}
        self.metadata_errors = []
        self.root = PureWindowsPath(data.get("outputDirectory", ""))
        self.verify_files = verify_files
        self.validate()
        self.runtime_lineage = runtime_lineage(path, data if purpose == "relocation" and data.get("schema") == RELOCATION_SCHEMA else None)

    def need(self, condition, message):
        if not condition:
            self.errors.append(message)
        return condition

    def relative(self, value):
        path = PureWindowsPath(value)
        try:
            relative = path.relative_to(self.root)
            if ".." in relative.parts or not relative.name:
                raise ValueError("unsafe relative path")
            return relative.as_posix().casefold()
        except ValueError:
            self.errors.append("path_outside_output_directory:" + str(value))
            return "UNRESOLVED:" + str(value)

    def validate(self):
        d = self.data
        schemas = SCHEMAS | {RELOCATION_SCHEMA} if self.purpose == "relocation" else SCHEMAS
        self.need(d.get("schema") in schemas, "unsupported_schema")
        for field in ("success", "nativeReadbackPass", "componentIdentityReadbackPass", "inputFilesUnchanged"):
            self.need(d.get(field) is True, field + "_not_true")
        for field in ("production_release", "acceptancePass"):
            self.need(d.get(field) is False, field + "_not_false")
        if "stageAcceptance" in d:
            self.need(d["stageAcceptance"] is False, "stageAcceptance_not_false")
        if d.get("schema") in ("16029-built-assembly-measurement/v1", RELOCATION_SCHEMA):
            self.need(d.get("sourceEvidenceUnchanged") is True, "sourceEvidenceUnchanged_not_true")
        self.need(d.get("changedInputFiles") == [], "changed_input_files")
        for item in d.get("expectedManifest", []):
            role = item.get("identity")
            self.need(isinstance(role, str) and role not in self.manifest, "missing_or_duplicate_manifest_role:" + str(role))
            self.manifest[role] = item
        for item in d.get("instances", []):
            role, name = item.get("identity"), item.get("component")
            self.need(isinstance(role, str) and role not in self.instances, "missing_or_duplicate_instance_role:" + str(role))
            self.need(isinstance(name, str) and name not in self.names, "missing_or_duplicate_native_name:" + str(name))
            self.instances[role], self.names[name] = item, role
            expected = self.manifest.get(role, {})
            self.need(item.get("fixedState") is True and item.get("transformPass") is True, "invalid_component_state:" + str(role))
            self.need(item.get("componentNameAfterReopen") == name, "native_name_readback_inconsistent:" + str(role))
            for key in ("side", "index", "kind"):
                self.need(item.get(key) == expected.get(key), "manifest_" + key + "_mismatch:" + str(role))
            self.need(self.relative(item.get("path", "")) == self.relative(expected.get("path", "")), "manifest_path_mismatch:" + str(role))
            transform, wanted = item.get("transform", []), expected.get("transform", [])
            self.need(len(transform) >= 13 and len(wanted) >= 13 and
                      all(number(a) and number(b) and math.isfinite(a) and math.isfinite(b) and abs(a-b) < TRANSFORM
                          for a, b in zip(transform[:13], wanted[:13])),
                      "manifest_transform_mismatch:" + str(role))
            self.need(isinstance(item.get("configuration"), str) and bool(item["configuration"]), "configuration_missing:" + str(role))
            recorded = item.get("sha256")
            self.need(isinstance(recorded, str) and len(recorded) == 64, "sha256_missing:" + str(role))
            if self.verify_files and item.get("path"):
                actual_path = Path(str(PureWindowsPath(item["path"])))
                if actual_path not in self.actual_hashes:
                    try:
                        self.actual_hashes[actual_path] = digest(actual_path.read_bytes())
                    except OSError as error:
                        self.errors.append("cad_bytes_unreadable:" + str(error))
                        self.actual_hashes[actual_path] = None
                self.need(self.actual_hashes[actual_path] == recorded, "recorded_cad_sha256_differs_from_disk:" + str(role))
            self.need(len(item.get("bodies", [])) == 1, "one_body_required:" + str(role))
        count = len(self.instances)
        self.need(count > 0 and set(self.instances) == set(self.manifest), "manifest_instance_bijection_failed")
        self.need(d.get("minimumScope", {}).get("expectedComponentCount") == count, "component_count_scope_mismatch")
        rows = d.get("layout", {}).get("rowsByColumn", {})
        expected_doors = {(side, row["index"]) for side in ("L", "R") for row in rows.get(side, [])}
        actual_doors = {(v.get("side"), v.get("index")) for v in self.instances.values() if v.get("kind") == "panel"}
        self.need(expected_doors == actual_doors, "panel_column_layout_mismatch")
        self.need(d.get("layout", {}).get("columnDoorCounts") == [len(rows.get("L", [])), len(rows.get("R", []))], "column_door_counts_mismatch")
        interfaces = d.get("hingeInterfaceMeasurements", [])
        keys = [(v.get("side"), v.get("index")) for v in interfaces]
        self.need(set(keys) == expected_doors and len(keys) == len(expected_doors), "door_interface_coverage_mismatch")
        checks = d.get("componentIdentityReadback", [])
        self.need(len(checks) == count and {v.get("identity") for v in checks} == set(self.instances), "identity_readback_coverage_mismatch")
        for check in checks:
            self.need(all(check.get(k) is True for k in ("pass", "identityUnique", "nativeNameUnique", "statePass")) and check.get("candidateCount") == 1,
                      "identity_readback_failed:" + str(check.get("identity")))
        self.validate_interference()
        self.read_lineage()
        self.validate_document_metadata()
        if d.get("schema") == RELOCATION_SCHEMA:
            self.validate_relocation()

    def validate_document_metadata(self):
        def need(condition, message):
            if not condition:
                self.metadata_errors.append(message)
        rows = self.data.get("documentMetadata")
        if rows is None:
            self.metadata_errors.append("document_metadata_not_recorded")
            return
        need(isinstance(rows, list), "document_metadata_list_required")
        if not isinstance(rows, list):
            return
        need(self.data.get("documentMetadataReadbackPass") is True, "document_metadata_readback_not_true")
        for row in rows:
            if not isinstance(row, dict):
                self.metadata_errors.append("document_metadata_object_required")
                continue
            role = row.get("identity")
            need(isinstance(role, str) and role in self.instances and role not in self.document_metadata,
                 "document_metadata_identity_invalid:" + str(role))
            self.document_metadata[role] = row
            instance = self.instances.get(role, {})
            need(ntpath.normcase(row.get("path", "")) == ntpath.normcase(instance.get("path", "")),
                 "document_metadata_path_mismatch:" + str(role))
            configurations = row.get("configurations", [])
            valid_configurations = (isinstance(configurations, list) and bool(configurations)
                                    and all(isinstance(v, str) and v for v in configurations))
            need(valid_configurations, "document_metadata_configuration_list_invalid:" + str(role))
            if not valid_configurations:
                configurations = []
            need(len(configurations) == len(set(configurations)), "document_metadata_configuration_duplicate:" + str(role))
            need(row.get("referencedConfiguration") == instance.get("configuration")
                 and row.get("referencedConfiguration") in configurations,
                 "document_metadata_referenced_configuration_mismatch:" + str(role))
            for field in ("materialName", "materialDatabase"):
                need(field in row and (row[field] is None or isinstance(row[field], str)),
                     "document_metadata_material_field_missing_or_invalid:" + str(role) + "/" + field)
            materials = row.get("configurationMaterials", [])
            valid_materials = isinstance(materials, list) and all(isinstance(v, dict) for v in materials)
            need(valid_materials, "document_metadata_configuration_materials_invalid:" + str(role))
            if valid_materials:
                material_names = [v.get("configuration") for v in materials]
                need(all(isinstance(v, str) for v in material_names)
                     and len(material_names) == len(set(str(v) for v in material_names))
                     and set(str(v) for v in material_names) == set(configurations),
                     "document_metadata_all_configuration_materials_required:" + str(role))
                for material in materials:
                    need(all(k in material and (material[k] is None or isinstance(material[k], str))
                             for k in ("materialName", "materialDatabase")),
                         "document_metadata_configuration_material_fields_invalid:" + str(role))
                referenced_material = [v for v in materials if v.get("configuration") == row.get("referencedConfiguration")]
                need(len(referenced_material) == 1 and all(referenced_material[0].get(k) == row.get(k)
                                                          for k in ("materialName", "materialDatabase")),
                     "document_metadata_referenced_material_inconsistent:" + str(role))
            for field in ("activeConfigurationBefore", "activeConfigurationAfterPropertyRead", "activeConfigurationAfterRestore"):
                need(row.get(field) in configurations, "document_metadata_active_configuration_invalid:" + str(role) + "/" + field)
            need(row.get("activeConfigurationBefore") == row.get("activeConfigurationAfterRestore")
                 and row.get("configurationRestored") is True and row.get("referencedConfigurationUnchanged") is True,
                 "document_metadata_configuration_not_restored:" + str(role))
            need(row.get("readbackComplete") is True, "document_metadata_readback_incomplete:" + str(role))
            scopes = row.get("customPropertyScopes", [])
            valid_scopes = isinstance(scopes, list) and all(isinstance(v, dict) for v in scopes)
            need(valid_scopes, "document_metadata_property_scopes_invalid:" + str(role))
            if not valid_scopes:
                continue
            names = [scope.get("configuration") for scope in scopes]
            need(all(isinstance(v, str) for v in names) and len(names) == len(set(str(v) for v in names))
                 and set(str(v) for v in names) == {""} | set(configurations),
                 "document_metadata_all_property_scopes_required:" + str(role))
            for scope in scopes:
                properties = scope.get("properties", [])
                valid_properties = isinstance(properties, list) and all(isinstance(v, dict) for v in properties)
                need(valid_properties, "document_metadata_properties_invalid:" + str(role))
                if not valid_properties:
                    continue
                need(type(scope.get("propertyCount")) is int and scope["propertyCount"] == len(properties),
                     "document_metadata_property_count_mismatch:" + str(role))
                property_names = [value.get("name") for value in properties]
                need(all(isinstance(v, str) and v for v in property_names)
                     and len(property_names) == len(set(str(v) for v in property_names)),
                     "document_metadata_property_name_invalid:" + str(role))
                for value in properties:
                    need(all(k in value and (value[k] is None or isinstance(value[k], str)) for k in ("value", "resolved"))
                         and type(value.get("status")) is int and value.get("status") in (0, 2)
                         and type(value.get("wasResolved")) is bool
                         and type(value.get("linked")) is bool,
                         "document_metadata_property_readback_invalid:" + str(role) + "/" + str(value.get("name")))
        need(len(rows) == len(self.instances) and set(self.document_metadata) == set(self.instances),
             "document_metadata_instance_coverage_mismatch")

    def metadata_summary(self):
        return {"recorded": "documentMetadata" in self.data,
                "complete": not self.metadata_errors and bool(self.document_metadata),
                "coveredInstanceCount": len(self.document_metadata),
                "requiredInstanceCount": len(self.instances), "errors": sorted(set(self.metadata_errors))}

    def canonical_metadata(self):
        result = []
        for source in self.document_metadata.values():
            row = {k: source[k] for k in ("identity", "referencedConfiguration", "materialName", "materialDatabase",
                                          "activeConfigurationBefore", "activeConfigurationAfterRestore")}
            row["path"] = self.relative(source["path"])
            row["configurations"] = sorted(source["configurations"])
            row["configurationMaterials"] = sorted(
                [{k: material[k] for k in ("configuration", "materialName", "materialDatabase")}
                 for material in source["configurationMaterials"]], key=lambda v: v["configuration"])
            row["customPropertyScopes"] = sorted(
                [{"configuration": scope["configuration"], "properties": sorted(
                    [{k: value[k] for k in ("name", "value", "resolved", "linked")} for value in scope["properties"]],
                    key=lambda v: v["name"])} for scope in source["customPropertyScopes"]], key=lambda v: v["configuration"])
            result.append(row)
        return sorted(result, key=lambda v: v["identity"])

    def validate_relocation(self):
        d = self.data
        reopen = d.get("reopen", {})
        self.need(reopen.get("pass") is True and reopen.get("coldSession") is True
                  and type(reopen.get("componentCount")) is int and reopen["componentCount"] == len(self.instances),
                  "relocation_successful_cold_reopen_required")
        self.need(d.get("referenceRemappingExpected") is True, "relocation_reference_remapping_not_declared")
        old_root = PureWindowsPath(d.get("sourceOutputDirectory", ""))
        self.need(old_root.is_absolute() and self.root.is_absolute() and old_root != self.root,
                  "relocation_distinct_absolute_roots_required")
        self.need(PureWindowsPath(d.get("sourceAssemblyPath", "")) == old_root / "16029_variable_minimum.SLDASM"
                  and PureWindowsPath(d.get("assemblyPath", "")) == self.root / "16029_variable_minimum.SLDASM",
                  "relocation_assembly_mapping_invalid")
        self.need(PureWindowsPath(d.get("sourceEvidencePath", "")) == self.root / "assembly-evidence.json",
                  "relocation_copied_source_evidence_path_invalid")
        mappings = d.get("referenceRemapping", [])
        roles = [row.get("identity") for row in mappings]
        self.need(len(roles) == len(self.instances) and set(roles) == set(self.instances),
                  "relocation_mapping_coverage_invalid")
        for row in mappings:
            instance = self.instances.get(row.get("identity"), {})
            try:
                relative = PureWindowsPath(row.get("sourcePath", "")).relative_to(old_root)
                safe = bool(relative.name) and ".." not in relative.parts
            except ValueError:
                relative, safe = PureWindowsPath(), False
            self.need(safe and PureWindowsPath(row.get("path", "")) == self.root / relative
                      and PureWindowsPath(row.get("path", "")) == PureWindowsPath(instance.get("path", "")),
                      "relocation_component_mapping_invalid:" + str(row.get("identity")))
        inputs = d.get("readOnlyInputFiles", [])
        actual_paths = {ntpath.normcase(str(PureWindowsPath(v.get("path", "")))) for v in inputs}
        required_paths = {ntpath.normcase(str(PureWindowsPath(v.get("path", "")))) for v in self.instances.values()}
        required_paths.add(ntpath.normcase(d.get("assemblyPath", "")))
        self.need(len(inputs) == len(actual_paths) and required_paths <= actual_paths,
                  "relocation_readonly_inventory_incomplete")
        cad_root = self.root.parent.parent
        for row in inputs:
            path = PureWindowsPath(row.get("path", ""))
            try:
                relative = path.relative_to(cad_root)
                safe = ".." not in relative.parts and bool(relative.name)
            except ValueError:
                safe = False
            before, after = row.get("sha256Before"), row.get("sha256After")
            self.need(safe and isinstance(before, str) and len(before) == 64 and before == after,
                      "relocation_readonly_inventory_changed_or_outside_scope:" + str(path))
            if self.verify_files:
                try:
                    self.need(digest(Path(str(path)).read_bytes()) == after,
                              "relocation_readonly_inventory_differs_from_disk:" + str(path))
                except OSError as error:
                    self.errors.append("relocation_readonly_inventory_unreadable:" + str(error))
        if self.verify_files:
            actual = {ntpath.normcase(str(v)) for v in Path(str(cad_root)).rglob("*")
                      if v.is_file() and v.suffix.upper() in (".SLDPRT", ".SLDASM", ".SLDDRW")}
            self.need(actual == actual_paths, "relocation_readonly_inventory_not_all_cad_files")

    def pair(self, value):
        participants = [self.names.get(value.get(k)) for k in ("componentA", "componentB")]
        if None in participants or participants[0] == participants[1]:
            self.errors.append("interference_participants_unresolved")
        return tuple(sorted(str(v) for v in participants))

    def validate_interference(self):
        interference = self.data.get("interference", {})
        n = len(self.instances)
        total = n * (n-1) // 2
        pairs = interference.get("pairs", [])
        keys = [self.pair(v) for v in pairs]
        self.need(interference.get("complete") is True, "native_interference_not_complete")
        self.need(interference.get("componentCount") == n, "interference_component_count_mismatch")
        for field in ("expectedPairCount", "enumeratedPairCount", "measuredPairCount"):
            self.need(interference.get(field) == total, "interference_" + field + "_mismatch")
        self.need(interference.get("unknownPairCount") == 0, "unknown_interference_pairs")
        self.need(len(pairs) == total and len(set(keys)) == total, "interference_pair_bijection_failed")
        self.need(interference.get("errors") == [] and interference.get("excludedComponentPairs") == [], "interference_errors_or_exclusions")
        self.need(all(v.get("measurementComplete") is True and v.get("nativeDetectionComplete") is True for v in pairs), "interference_pair_incomplete")
        collisions = interference.get("collisions", [])
        self.need(interference.get("declaredNativeInterferenceCount") == len(collisions), "native_collision_count_mismatch")
        grouped = collections.defaultdict(list)
        for collision in collisions:
            grouped[self.pair(collision)].append(collision)
            self.need(collision.get("measurementComplete") is True and collision.get("possibleInterference") is False,
                      "collision_measurement_incomplete")
            self.need(collision.get("approved") is False and collision.get("pass") is False, "collision_approval_changed")
        self.need(interference.get("pass") is (not collisions), "interference_overall_status_inconsistent")
        for pair, key in zip(pairs, keys):
            found = grouped.get(key, [])
            self.need(pair.get("interferenceCount") == len(found), "pair_collision_count_inconsistent:" + repr(key))
            self.need(pair.get("pass") is (not found) and pair.get("status") == ("unapproved_interference" if found else "no_interference"), "pair_status_inconsistent:" + repr(key))
            self.need(Comparison().equal(pair.get("intersectionBoundsMm"), [v.get("intersectionBoundsMm") for v in found], key="intersectionBoundsMm", report=False), "pair_intersection_bodies_inconsistent:" + repr(key))
            volume = sum(v.get("volumeMm3", float("nan")) for v in found)
            self.need(Comparison().equal(pair.get("volumeMm3"), volume, key="volumeMm3", report=False),
                      "pair_collision_volume_inconsistent:" + repr(key))

    def read_lineage(self):
        source = self.data
        if self.path and self.data.get("sourceEvidencePath"):
            source_path = Path(str(PureWindowsPath(self.data["sourceEvidencePath"])))
            try:
                raw = source_path.read_bytes()
                actual = digest(raw)
                expected = self.data.get("sourceEvidenceSha256")
                self.lineage_reference = {"path": str(source_path), "actualSha256": actual, "recordedSha256": expected,
                                          "verified": actual == expected}
                self.need(actual == expected, "source_evidence_hash_mismatch")
                source = json.loads(raw.decode("utf-8-sig"))
                self.source_evidence = source
            except (OSError, ValueError) as error:
                self.errors.append("source_evidence_unreadable:" + str(error))
        for event in source.get("events", []):
            if "sourceSha256" in event:
                self.lineage.append({"inspection": event.get("inspection"),
                                     "targetBasename": PureWindowsPath(event.get("path", "")).name.casefold(),
                                     "sourceBasename": PureWindowsPath(event.get("source", "")).name.casefold(),
                                     "sourceSha256": event["sourceSha256"]})
        self.lineage.sort(key=lambda v: (v["targetBasename"], v["sourceBasename"], v["sourceSha256"]))

    def clean(self, value, ignore_references=False, key=""):
        if isinstance(value, dict):
            return {k: self.clean(v, ignore_references, k) for k, v in value.items()
                    if k not in METADATA and k != "sha256" and not (ignore_references and k in REFERENCES)}
        if isinstance(value, list):
            return [self.clean(v, ignore_references, key) for v in value]
        if isinstance(value, str):
            if key in ("component", "componentA", "componentB"):
                if value not in self.names:
                    self.errors.append("component_reference_unresolved:" + value)
                return self.names.get(value, "UNRESOLVED:" + value)
            if key == "path":
                return self.relative(value)
            if key in REFERENCES:
                if value not in self.references:
                    self.errors.append("topology_reference_unresolved:" + value)
                return self.references.get(value, "UNRESOLVED:" + value)
        return value

    def summary(self):
        counts = collections.Counter((v.get("side"), v.get("kind")) for v in self.instances.values())
        return {"path": str(self.path) if self.path else None, "sha256": self.sha256,
                "schema": self.data.get("schema"), "instanceCount": len(self.instances),
                "columnDoorCounts": self.data.get("layout", {}).get("columnDoorCounts"),
                "countsBySideAndKind": [{"side": side, "kind": kind, "count": count}
                                       for (side, kind), count in sorted(counts.items())],
                "generationSuccess": self.data.get("success"),
                "recordedAcceptancePass": self.data.get("acceptancePass"),
                "recordedStageAcceptance": {"present": "stageAcceptance" in self.data,
                                            "value": self.data.get("stageAcceptance")},
                "recordedProductionRelease": self.data.get("production_release"),
                "interference": {k: self.data.get("interference", {}).get(k) for k in
                                 ("complete", "expectedPairCount", "measuredPairCount",
                                  "unknownPairCount", "declaredNativeInterferenceCount", "pass")},
                "sourceEvidence": self.lineage_reference, "recordedSourceSeeds": self.lineage,
                "errors": sorted(set(self.errors))}


def register(snapshot, native_id, canonical):
    if not isinstance(native_id, str):
        snapshot.errors.append("topology_identity_missing:" + canonical)
        return
    old = snapshot.references.get(native_id)
    snapshot.need(old is None or old == canonical, "topology_identity_not_unique:" + native_id)
    snapshot.references[native_id] = canonical


def map_topology(left, right):
    matching = Comparison()
    for role in sorted(set(left.instances) | set(right.instances)):
        bodies_a = left.instances.get(role, {}).get("bodies", [])
        bodies_b = right.instances.get(role, {}).get("bodies", [])
        for body_index in range(max(len(bodies_a), len(bodies_b))):
            a = bodies_a[body_index] if body_index < len(bodies_a) else {}
            b = bodies_b[body_index] if body_index < len(bodies_b) else {}
            for collection in ("cylinders", "planarFaces"):
                aa, bb = a.get(collection, []), b.get(collection, [])
                pairs, _, unmatched_b = matching.matching(left.clean(aa, True), right.clean(bb, True), role, collection)
                partner = {j: i for i, j in pairs}
                for snapshot, items, indices in (
                        (left, aa, {i: str(i) for i in range(len(aa))}),
                        (right, bb, {j: str(partner[j]) if j in partner else "candidate-only-" + str(j) for j in range(len(bb))})):
                    for i, item in enumerate(items):
                        label = role + "/body:" + str(body_index) + "/" + collection + ":" + indices[i]
                        register(snapshot, item.get("faceIdentity"), label)
                if collection == "cylinders":
                    for i, item in enumerate(aa):
                        prefix = left.references.get(item.get("faceIdentity"), role)
                        for edge_index, edge in enumerate(item.get("boundaryCircles", [])):
                            register(left, edge.get("edgeIdentity"), prefix + "/edge:" + str(edge_index))
                    for j, item in enumerate(bb):
                        edges_b = item.get("boundaryCircles", [])
                        i = partner.get(j)
                        edges_a = aa[i].get("boundaryCircles", []) if i is not None else []
                        edge_pairs, _, _ = matching.matching(left.clean(edges_a, True), right.clean(edges_b, True), role, "boundaryCircles")
                        edge_partner = {y: x for x, y in edge_pairs}
                        prefix = right.references.get(item.get("faceIdentity"), role)
                        for edge_index, edge in enumerate(edges_b):
                            suffix = str(edge_partner[edge_index]) if edge_index in edge_partner else "candidate-only-" + str(edge_index)
                            register(right, edge.get("edgeIdentity"), prefix + "/edge:" + suffix)


def cache_evidence(snapshot):
    rows, errors = [], []
    for role, instance in snapshot.instances.items():
        for body in instance.get("bodies", []):
            provenance = body.get("topologyReadback")
            if not provenance:
                continue
            mode = provenance.get("mode")
            if mode == "native_transformed_body_scan":
                if provenance.get("independentNativeTopologyReadbackForThisInstance") is not True:
                    errors.append("native_scan_not_independent:" + role)
                continue
            if mode != "same_native_source_translation_reuse":
                errors.append("unknown_topology_mode:" + role)
                continue
            rows.append(role)
            source = snapshot.instances.get(provenance.get("sourceIdentity"), {})
            source_bodies = source.get("bodies", [])
            source_body = source_bodies[0] if len(source_bodies) == 1 else {}
            source_scan = source_body.get("topologyReadback", {})
            checks = provenance.get("sourceNativeObjectIdentityChecks", [])
            exact = provenance.get("nativeSixDirectionBoundsCheck", {})
            valid = (provenance.get("independentNativeTopologyReadbackForThisInstance") is False
                     and provenance.get("cacheEligible") is True
                     and provenance.get("cacheFallbackReason") is None and provenance.get("cacheError") is None
                     and provenance.get("normalBodyConfirmed") is True and provenance.get("normalBodyInfo") == 1
                     and ntpath.normcase(instance.get("path", "")) == ntpath.normcase(source.get("path", ""))
                     and provenance.get("sourceActualSha256") == instance.get("sha256") == source.get("sha256")
                     and provenance.get("sourceReferencedConfiguration") == instance.get("configuration") == source.get("configuration")
                     and provenance.get("sourceComponent") == source.get("component")
                     and source_scan.get("mode") == "native_transformed_body_scan"
                     and source_scan.get("normalBodyConfirmed") is True and source_scan.get("normalBodyInfo") == 1
                     and any(v.get("sameIUnknown") is True and v.get("swObjectEquality") == 1
                             and v.get("sourceIdentity") == source.get("identity") and v.get("disabled") is False for v in checks)
                     and exact.get("pass") is True
                     and provenance.get("nativeFaceAndEdgeIdsReferToSourceReadback") is True)
            source_transform = provenance.get("sourceNativeTotalTransform", [])
            target_transform = provenance.get("nativeTotalTransform", [])
            valid = valid and len(source_transform) >= 13 and len(target_transform) >= 13
            if valid:
                valid = all(float(source_transform[i]).hex() == float(target_transform[i]).hex()
                            for i in list(range(9)) + [12])
                valid = valid and Comparison().equal(source_transform[:13], source.get("transform", [])[:13], key="transform", report=False)
                valid = valid and Comparison().equal(target_transform[:13], instance.get("transform", [])[:13], key="transform", report=False)
                valid = valid and Comparison().equal(exact.get("targetNativeExactBodyBoundsMm"), body.get("boxMm"), key="boxMm", report=False)
                valid = valid and Comparison().equal(exact.get("sourceNativeExactBodyBoundsMm"), source_body.get("boxMm"), key="boxMm", report=False)
                valid = valid and source_transform == source_scan.get("nativeTotalTransform")
                delta = [(target_transform[i+9]-source_transform[i+9])*1000 for i in range(3)]
                predicted = [v+delta[i % 3] for i, v in enumerate(source_body.get("boxMm", []))]
                actual_delta = [v-predicted[i] for i, v in enumerate(body.get("boxMm", []))]
                valid = valid and Comparison().equal(provenance.get("translationMm"), delta, key="translationMm", report=False)
                valid = valid and Comparison().equal(exact.get("translatedSourceNativeExactBodyBoundsMm"), predicted, key="boxMm", report=False)
                difference = exact.get("targetMinusTranslatedSourceBoundsMm", [])
                valid = valid and Comparison().equal(difference, actual_delta, key="boxMm", report=False)
                valid = valid and len(actual_delta) == 6 and all(number(v) and math.isfinite(v) and abs(v) <= MM for v in actual_delta)
            source_faces = {v.get("faceIdentity"): v for v in source_body.get("cylinders", []) + source_body.get("planarFaces", [])}
            for face in body.get("cylinders", []) + body.get("planarFaces", []):
                origin = source_faces.get(face.get("sourceFaceIdentity"), {})
                valid = valid and bool(origin) and face.get("nativeFaceId") is None and face.get("sourceNativeFaceId") == origin.get("nativeFaceId") and face.get("independentNativeTopologyReadbackForThisInstance") is False
                source_edges = {v.get("edgeIdentity"): v for v in origin.get("boundaryCircles", [])}
                for edge in face.get("boundaryCircles", []):
                    origin_edge = source_edges.get(edge.get("sourceEdgeIdentity"), {})
                    valid = valid and bool(origin_edge) and edge.get("nativeEdgeId") is None and edge.get("curveTag") is None and edge.get("sourceNativeEdgeId") == origin_edge.get("nativeEdgeId") and edge.get("sourceCurveTag") == origin_edge.get("curveTag") and edge.get("sourceFaceIdentity") == origin.get("faceIdentity") and edge.get("independentNativeTopologyReadbackForThisInstance") is False
            if not valid:
                errors.append("cache_provenance_unverified:" + role)
    performance = snapshot.data.get("measurementPerformance", {})
    reported = performance.get("topologyCacheHits", 0)
    if reported != len(rows):
        errors.append("reported_cache_hit_count_inconsistent")
    if performance and performance.get("nativeSurfaceScans") != sum(len(v.get("bodies", [])) for v in snapshot.instances.values()) - len(rows):
        errors.append("reported_native_scan_count_inconsistent")
    return {"actualReusedBodies": len(rows), "reportedTopologyCacheHits": reported,
            "reusedRoles": sorted(rows), "errors": errors,
            "nativeSurfaceScans": performance.get("nativeSurfaceScans"),
            "elapsedMilliseconds": performance.get("elapsedMilliseconds")}


def relocation_source_comparison(left, right):
    d = right.data
    chain = right.runtime_lineage.get("copyChain", [])
    direct = chain[0] if chain else {}
    reference = right.lineage_reference or {}
    relative = None
    try:
        relative = PureWindowsPath(d.get("sourceEvidencePath", "")).relative_to(PureWindowsPath(direct.get("target", "")))
        if ".." in relative.parts or not relative.name:
            relative = None
    except ValueError:
        pass
    copied = [v for v in direct.get("assemblyEvidenceCopies", [])
              if relative is not None and PureWindowsPath(v.get("relativePath", "")) == relative]
    source_path = PureWindowsPath(direct.get("source", "")) / relative if relative is not None else None
    conditions = {
        "candidateIsRelocationEvidence": d.get("schema") == RELOCATION_SCHEMA,
        "baselineIsGeneratedAssemblyEvidence": left.data.get("schema") == "16029-independent-variable-minimum-assembly/v1",
        "copiedSourceEvidenceHashVerified": reference.get("verified") is True,
        "copiedSourceEvidenceIsExactBaseline": bool(left.sha256) and d.get("sourceEvidenceSha256") == left.sha256,
        "sourceOutputDirectoryMatchesBaseline": PureWindowsPath(d.get("sourceOutputDirectory", "")) == left.root,
        "sourceAssemblyPathMatchesBaseline": bool(left.data.get("assemblyPath"))
                                             and PureWindowsPath(d.get("sourceAssemblyPath", "")) == PureWindowsPath(left.data["assemblyPath"]),
        "directCopyActionIsRelocate": direct.get("action") == "relocate",
        "directCopySourcePathIsBaseline": left.path is not None and source_path == PureWindowsPath(str(left.path)),
        "directCopyManifestBindsBaselineBytes": len(copied) == 1 and copied[0].get("sha256") == left.sha256
                                               and left.byte_count is not None and copied[0].get("size") == left.byte_count,
        "owningRelocationRunCompletedWithVerifiedCadAndToolchain": right.runtime_lineage.get("relocationRuntime", {}).get("verified") is True,
    }
    return {"verified": all(conditions.values()), "conditions": conditions,
            "baselinePath": str(left.path) if left.path else None,
            "copiedSourceEvidencePath": d.get("sourceEvidencePath"),
            "mappedOriginalEvidencePath": str(source_path) if source_path else None,
            "copyManifestRow": copied[0] if len(copied) == 1 else None}


def compare_snapshots(left, right, purpose="cache"):
    map_topology(left, right)
    comparison = Comparison()
    sections = {}
    for name in ("layout", "minimumScope", "expectedManifest", "instances", "hingeInterfaceMeasurements", "interference"):
        a, b = left.clean(left.data.get(name)), right.clean(right.data.get(name))
        if name == "interference":
            for value in (a, b):
                if isinstance(value, dict):
                    for collection in ("pairs", "collisions", "excludedComponentPairs"):
                        for row in value.get(collection, []):
                            if isinstance(row, dict) and "componentA" in row and "componentB" in row:
                                row["componentA"], row["componentB"] = sorted((row["componentA"], row["componentB"]))
        sections[name] = comparison.equal(a, b, "/" + name, name)
    byte_rows = []
    for role in sorted(set(left.instances) | set(right.instances)):
        a, b = left.instances.get(role, {}), right.instances.get(role, {})
        byte_rows.append({"identity": role, "baselinePath": a.get("path"), "candidatePath": b.get("path"),
                          "baselineRelativePath": left.relative(a.get("path", "")),
                          "candidateRelativePath": right.relative(b.get("path", "")),
                          "baselineSha256": a.get("sha256"), "candidateSha256": b.get("sha256"),
                          "sameActualBytes": bool(a.get("sha256")) and a.get("sha256") == b.get("sha256")})
    bytes_equal = bool(byte_rows) and all(v["sameActualBytes"] for v in byte_rows)
    baseline_cache, candidate_cache = cache_evidence(left), cache_evidence(right)
    lineage = lineage_comparison(left, right)
    metadata_comparison = Comparison()
    metadata_complete = not left.metadata_errors and not right.metadata_errors
    metadata_equal = (metadata_comparison.equal(left.canonical_metadata(), right.canonical_metadata(),
                                               "/documentMetadata", "documentMetadata")
                      if metadata_complete else None)
    relocation = relocation_source_comparison(left, right) if purpose == "relocation" else None
    valid = not left.errors and not right.errors and not baseline_cache["errors"] and not candidate_cache["errors"]
    measurements_equal = valid and all(sections.values())
    if not valid:
        status = "invalid_evidence"
    elif not measurements_equal:
        status = "measurement_difference"
    elif purpose == "relocation" and not metadata_complete:
        status = "unverified_document_metadata_coverage"
    elif purpose == "relocation" and not metadata_equal:
        status = "document_metadata_difference"
    elif purpose == "relocation" and not relocation["verified"]:
        status = "unverified_relocation_copy_source_binding"
    elif purpose == "cache" and baseline_cache["actualReusedBodies"]:
        status = "unverified_baseline_uses_cache"
    elif not lineage["verified"]:
        status = "unverified_build_or_root_input_lineage"
    elif purpose == "cache" and candidate_cache["actualReusedBodies"] == 0:
        status = "unverified_no_cache_reuse"
    else:
        status = "relocation_measurements_and_document_metadata_equal" if purpose == "relocation" else "controlled_cache_measurements_equal"
    verified = status in ("controlled_cache_measurements_equal", "relocation_measurements_and_document_metadata_equal")
    return {
        "schema": "winnsen.assembly-measurement-comparison/v1",
        "production_release": False, "engineeringAcceptancePass": False, "stageAcceptance": False,
        "pass": verified, "purpose": purpose, "validationStatus": status,
        "cacheValidationStatus": status if purpose == "cache" else "not_requested",
        "relocationValidationStatus": status if purpose == "relocation" else "not_requested",
        "relocationMeasurementEquivalent": verified if purpose == "relocation" else None,
        "measurementComparisonEqual": measurements_equal, "sectionsEqual": sections,
        "documentMetadataComparison": {"baseline": left.metadata_summary(), "candidate": right.metadata_summary(),
                                       "equal": metadata_equal, "includedInPass": purpose == "relocation",
                                       "differenceCount": metadata_comparison.difference_count,
                                       "differences": metadata_comparison.differences,
                                       "historicalMissingMetadataIsNotAssumedCovered": True},
        "baseline": left.summary(), "candidate": right.summary(),
        "byteIdentity": {"allInstanceFilesEqual": bytes_equal,
                         "differentInstanceFileCount": sum(not v["sameActualBytes"] for v in byte_rows),
                         "instances": byte_rows},
        "sourceLineage": {"recordedPartCopySourceSeedsEqual": left.lineage == right.lineage,
                          "sameSeedsDoNotProveDerivedByteIdentity": True,
                          "runtimeProof": lineage},
        "relocationSourceBinding": relocation,
        "baselineCacheEvidence": baseline_cache, "candidateCacheEvidence": candidate_cache,
        "differenceCount": comparison.difference_count + (metadata_comparison.difference_count if purpose == "relocation" else 0),
        "differences": comparison.differences + (metadata_comparison.differences if purpose == "relocation" else []),
        "numericComparison": {"comparedNumbers": comparison.compared_numbers,
                              "maximumAbsoluteDeltas": dict(comparison.numeric_maxima)},
        "contract": {"instanceMatch": "unique role plus relative actual path/basename, configuration and first 13 native transform values; actual file SHA recorded and checked separately",
                     "millimetresAbsoluteTolerance": MM, "transformStrictAbsoluteTolerance": TRANSFORM,
                     "directionAndRadiansAbsoluteTolerance": DIRECTION,
                     "nativeAreaAndVolumeAbsolutePrecision": NUMERIC_ABS,
                     "nativeAreaAndVolumeRelativePrecision": NUMERIC_REL,
                     "zeroToPositiveVolumeAlwaysDifferent": True,
                     "booleanNullCountAndPolicyFieldsExact": True,
                     "unorderedTopologyMatchedByCompleteMeasuredGeometry": True,
                     "ignoredFields": sorted(METADATA),
                     "volatileTopologyIdsResolvedToMatchedGeometry": sorted(REFERENCES),
                     "performanceFieldsExcludedFromGeometryEquality": True,
                     "documentMetadataRequiredForRelocation": True,
                     "documentMetadataValuesComparedExactlyAfterIdentityPathAndOrderNormalization": True,
                     "documentMetadataReadStatusRetainedAndValidatedButExcludedFromContentEquality": [
                         "activeConfigurationAfterPropertyRead", "status", "wasResolved"],
                     "allActualConfigurationMaterialsRequiredForRelocation": True,
                     "successfulColdReopenAndOwningNativeRunRequiredForRelocation": True,
                     "cachePurposePassCoversOnlyTheSixRecordedMeasurementSections": True,
                     "relocationAllowsCachedBaselineAndZeroHitCandidate": True,
                     "relocationRequiresExactBaselineEvidenceBytesInDirectCopyManifest": True},
        "requiredNextEvidence": [] if verified else [
            "Resolve the reported measurement, document-metadata, copy-source, cache-provenance or common-root/build-lineage gap; no extra CAD run is inferred solely from derived-file SHA differences.",
            "Measurement equality does not approve collisions, missing support, fit, motion, factory acceptance or production release."],
    }


def read_snapshot(path, purpose="cache"):
    raw = path.read_bytes()
    return Snapshot(json.loads(raw.decode("utf-8-sig")), path.resolve(), raw, purpose=purpose)


def fixture():
    root = r"C:\fixture\case1"
    transform = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]
    instances, manifest, checks, interfaces = [], [], [], []
    for index in (1, 2):
        role, component = "panel_" + str(index), "native_" + str(index)
        y = (index-1)*10
        matrix = transform.copy()
        matrix[10] = y/1000
        face = component + "/face:1"
        circles = []
        for edge_index, z in enumerate((0, 1)):
            circles.append({"edgeIdentity": face + "/edge:" + str(edge_index), "faceIdentity": face,
                            "nativeEdgeId": edge_index, "curveTag": 100+edge_index,
                            "circleParams": [0, y/1000, z/1000, 0, 0, 1, .002],
                            "centreMm": [0, y, z], "normal": [0, 0, 1], "radiusMm": 2.0,
                            "startPointMm": [2, y, z], "endPointMm": [2, y, z],
                            "edgeParameterRange": [0, 2*math.pi], "parameterSpanRadians": 2*math.pi,
                            "underlyingCurveClosed": True, "underlyingCurvePeriodic": True,
                            "completeCircle": True, "curveAndEdgeSameSense": True,
                            "edgeLengthMm": 4*math.pi, "endpointDistanceMm": 0,
                            "circumferenceErrorMm": 0, "completeCircleCriterion": "fixture"})
        cylinder = {"faceIdentity": face, "nativeFaceId": 1, "identityScope": "fixture",
                    "axisPointMm": [0, y, 0], "axisDirection": [0, 0, 1], "radiusMm": 2.0,
                    "faceInSurfaceSense": False, "surfaceNormalAtBoundary": [1, 0, 0],
                    "outwardFaceNormalAtBoundary": [1, 0, 0], "radialNormalDot": 1,
                    "wallClassification": "external_cylindrical_wall", "completeFiniteCylinder": True,
                    "axialIntervalFromBoundaryCircleCentresMm": [0, 1], "boundaryEdges": 2,
                    "seamEdges": 0, "boundaryCircles": circles, "nativeFaceAreaMm2": 4*math.pi,
                    "uvBounds": [0, 2*math.pi, 0, .001],
                    "approximateFaceBoxMm": [-2, y-2, 0, 2, y+2, 1],
                    "faceBoxCandidateOnly": True, "errors": []}
        plane = {"faceIdentity": component + "/face:2", "nativeFaceId": 2,
                 "planeParams": [0, 0, 1, 0, y/1000, 0], "rootPointMm": [0, y, 0],
                 "surfaceNormal": [0, 0, 1], "outwardFaceNormal": [0, 0, 1],
                 "faceInSurfaceSense": False, "approximateFaceBoxMm": [-2, y-2, 0, 2, y+2, 0],
                 "faceBoxCandidateOnly": True}
        body = {"bodyName": "body", "boxMm": [-2, y-2, 0, 2, y+2, 1],
                "cylinders": [cylinder], "planarFaces": [plane],
                "zNormalPlanarFaceBoxesMm": [plane["approximateFaceBoxMm"]],
                "faceBoxesAreApproximateCandidatesOnly": True}
        item = {"identity": role, "component": component, "componentNameAfterReopen": component,
                "path": root + r"\parts\panel.SLDPRT", "sha256": "A"*64, "side": "L",
                "index": index, "kind": "panel", "configuration": "Default",
                "fixedState": True, "transformPass": True, "transform": matrix, "bodies": [body]}
        instances.append(item)
        manifest.append({k: copy.deepcopy(item[k]) for k in ("identity", "path", "side", "index", "kind", "transform")})
        checks.append({"identity": role, "pass": True, "identityUnique": True,
                       "nativeNameUnique": True, "statePass": True, "candidateCount": 1})
        interfaces.append({"side": "L", "index": index, "candidateCount": 1,
                           "uniqueFiniteCandidate": False, "mechanicalFitPass": None,
                           "surface": copy.deepcopy(cylinder), "plane": copy.deepcopy(plane)})
    collision = {"componentA": "native_1", "componentB": "native_2", "volumeMm3": 1.5,
                 "intersectionBoundsMm": [0, 0, 0, 1, 1, 1], "measurementComplete": True,
                 "possibleInterference": False, "nativeIgnored": False, "approved": False, "pass": False}
    pair = {"componentA": "native_1", "componentB": "native_2", "volumeMm3": 1.5,
            "intersectionBoundsMm": [[0, 0, 0, 1, 1, 1]], "measurementComplete": True,
            "nativeDetectionComplete": True, "interferenceCount": 1, "pass": False,
            "status": "unapproved_interference", "disjointExactBounds": False}
    return {"schema": "16029-built-assembly-measurement/v1", "success": True,
            "nativeReadbackPass": True, "componentIdentityReadbackPass": True,
            "inputFilesUnchanged": True, "sourceEvidenceUnchanged": True,
            "production_release": False, "acceptancePass": False, "stageAcceptance": False,
            "changedInputFiles": [], "outputDirectory": root,
            "layout": {"rowsByColumn": {"L": [{"index": 1}, {"index": 2}], "R": []},
                       "columnDoorCounts": [2, 0]}, "minimumScope": {"expectedComponentCount": 2},
            "expectedManifest": manifest, "instances": instances, "componentIdentityReadback": checks,
            "hingeInterfaceMeasurements": interfaces,
            "interference": {"complete": True, "componentCount": 2, "expectedPairCount": 1,
                             "enumeratedPairCount": 1, "measuredPairCount": 1, "unknownPairCount": 0,
                             "declaredNativeInterferenceCount": 1, "pairs": [pair], "collisions": [collision],
                             "pass": False, "errors": [], "excludedComponentPairs": []}}


def fixture_cache(data):
    first, target = data["instances"]
    for item in data["instances"]:
        item["bodies"][0]["topologyReadback"] = {
            "mode": "native_transformed_body_scan", "normalBodyConfirmed": True, "normalBodyInfo": 1,
            "nativeTotalTransform": item["transform"], "independentNativeTopologyReadbackForThisInstance": True}
    source_body, body = first["bodies"][0], target["bodies"][0]
    info = body["topologyReadback"]
    delta = [0, 10, 0]
    info.update({"mode": "same_native_source_translation_reuse", "cacheEligible": True,
                 "cacheFallbackReason": None, "cacheError": None,
                 "independentNativeTopologyReadbackForThisInstance": False,
                 "sourceIdentity": first["identity"], "sourceComponent": first["component"],
                 "sourceActualSha256": first["sha256"], "sourceReferencedConfiguration": first["configuration"],
                 "sourceNativeTotalTransform": first["transform"], "translationMm": delta,
                 "sourceNativeObjectIdentityChecks": [{"sameIUnknown": True, "swObjectEquality": 1,
                                                       "sourceIdentity": first["identity"], "disabled": False}],
                 "nativeFaceAndEdgeIdsReferToSourceReadback": True,
                 "nativeSixDirectionBoundsCheck": {
                     "pass": True, "sourceNativeExactBodyBoundsMm": source_body["boxMm"],
                     "targetNativeExactBodyBoundsMm": body["boxMm"],
                     "translatedSourceNativeExactBodyBoundsMm": body["boxMm"],
                     "targetMinusTranslatedSourceBoundsMm": [0]*6}})
    for collection in ("cylinders", "planarFaces"):
        for source_face, face in zip(source_body[collection], body[collection]):
            face["sourceFaceIdentity"], face["sourceNativeFaceId"] = source_face["faceIdentity"], source_face["nativeFaceId"]
            face["nativeFaceId"], face["independentNativeTopologyReadbackForThisInstance"] = None, False
            for source_edge, edge in zip(source_face.get("boundaryCircles", []), face.get("boundaryCircles", [])):
                edge.update({"sourceEdgeIdentity": source_edge["edgeIdentity"],
                             "sourceFaceIdentity": source_face["faceIdentity"],
                             "sourceNativeEdgeId": source_edge["nativeEdgeId"], "sourceCurveTag": source_edge["curveTag"],
                             "nativeEdgeId": None, "curveTag": None,
                             "independentNativeTopologyReadbackForThisInstance": False})
    data["measurementPerformance"] = {"topologyCacheHits": 1, "nativeSurfaceScans": 1, "elapsedMilliseconds": 1234}
    data["hingeInterfaceMeasurements"][1]["surface"] = copy.deepcopy(body["cylinders"][0])
    data["hingeInterfaceMeasurements"][1]["plane"] = copy.deepcopy(body["planarFaces"][0])


def fixture_metadata(data):
    data["documentMetadataReadbackPass"] = True
    data["documentMetadata"] = []
    for instance in data["instances"]:
        data["documentMetadata"].append({
            "identity": instance["identity"], "path": instance["path"],
            "configurations": ["Alternate", "Default"], "referencedConfiguration": "Default",
            "materialName": "Fixture steel", "materialDatabase": r"C:\materials\fixture.sldmat",
            "configurationMaterials": [
                {"configuration": name, "materialName": "Fixture steel", "materialDatabase": r"C:\materials\fixture.sldmat"}
                for name in ("Alternate", "Default")],
            "activeConfigurationBefore": "Default", "activeConfigurationAfterPropertyRead": "Alternate",
            "activeConfigurationAfterRestore": "Default", "configurationRestored": True,
            "referencedConfigurationUnchanged": True, "readbackComplete": True,
            "customPropertyScopes": [
                {"configuration": "", "propertyCount": 1, "properties": [
                    {"name": "PART_ID", "value": "fixture", "resolved": "fixture", "status": 2,
                     "wasResolved": True, "linked": False}]},
                {"configuration": "Alternate", "propertyCount": 0, "properties": []},
                {"configuration": "Default", "propertyCount": 1, "properties": [
                    {"name": "HAND", "value": "L", "resolved": "L", "status": 2,
                     "wasResolved": True, "linked": False}]}]})


def fixture_relocation(cached_baseline=False):
    source = json.loads(json.dumps(fixture()).replace(r"C:\\fixture\\case1", r"C:\\fixture\\cad\\assemblies\\case1"))
    source["schema"] = "16029-independent-variable-minimum-assembly/v1"
    source["assemblyPath"] = str(PureWindowsPath(source["outputDirectory"]) / "16029_variable_minimum.SLDASM")
    source["assemblySha256"] = "C"*64
    source["reopen"] = {"pass": True, "coldSession": False, "componentCount": len(source["instances"])}
    fixture_metadata(source)
    candidate = copy.deepcopy(source)
    if cached_baseline:
        fixture_cache(source)
    old_root, new_root = source["outputDirectory"], r"D:\relocated\cad\assemblies\case1"
    candidate = json.loads(json.dumps(candidate).replace(json.dumps(old_root)[1:-1], json.dumps(new_root)[1:-1]))
    candidate.update(schema=RELOCATION_SCHEMA, sourceOutputDirectory=old_root,
                     sourceAssemblyPath=source["assemblyPath"], referenceRemappingExpected=True,
                     sourceEvidencePath=str(PureWindowsPath(new_root) / "assembly-evidence.json"),
                     sourceEvidenceSha256=digest(json.dumps(source, sort_keys=True).encode("utf-8")))
    candidate["referenceRemapping"] = [
        {"identity": a["identity"], "sourcePath": a["path"], "path": b["path"]}
        for a, b in zip(source["instances"], candidate["instances"])]
    files = {v["path"]: v["sha256"] for v in candidate["instances"]}
    files[candidate["assemblyPath"]] = candidate["assemblySha256"]
    candidate["readOnlyInputFiles"] = [{"path": path, "sha256Before": sha, "sha256After": sha}
                                      for path, sha in files.items()]
    candidate["measurementPerformance"] = {"topologyCacheHits": 0, "nativeSurfaceScans": 2}
    candidate["reopen"]["coldSession"] = True
    return source, candidate


def self_test():
    checks = []

    def run(a, b, lineage=True, purpose="cache", mutate_snapshot=None):
        raw = json.dumps(a, sort_keys=True).encode("utf-8")
        left = Snapshot(a, raw=raw, verify_files=False, purpose=purpose)
        right = Snapshot(b, verify_files=False, purpose=purpose)
        methods = {v: "fixture" for v in ("Build", "ValidateLayout", "PrepareStructure", "PrepareHardware",
                   "PreparePlacements", "BuildNativeAssembly", "ValidateWorldPanel", "ValidateWorldBoundary",
                   "MeasureHingeInterfaces", "MeasureHingeSupportChains", "MeasureInterference", "Bounds",
                   "CaptureBoundaryCircle", "CheckFeatureHealth")}
        proof = {"rootSourceDirectory": r"C:\fixture\seeds", "rootInputs": [{"fixture": True}],
                 "specification": {"fixture": True}, "process": {"fixture": True},
                 "inputVersions": [{"fixture": True}], "errors": [],
                 "buildToolchain": {"engineeringMethodSha256": methods},
                 "measurementToolchain": {"engineeringMethodSha256": methods}}
        if lineage:
            left.runtime_lineage, right.runtime_lineage = copy.deepcopy(proof), copy.deepcopy(proof)
            if purpose == "relocation":
                left.path = Path(str(PureWindowsPath(a["outputDirectory"]) / "assembly-evidence.json"))
                right.lineage_reference = {"verified": b.get("sourceEvidenceSha256") == left.sha256}
                source_root, target_root = left.root.parent.parent, right.root.parent.parent
                relative = PureWindowsPath(b["sourceEvidencePath"]).relative_to(target_root)
                right.runtime_lineage["copyChain"] = [{
                    "action": "relocate", "source": str(source_root), "target": str(target_root),
                    "assemblyEvidenceCopies": [{"relativePath": str(relative), "sha256": left.sha256, "size": len(raw)}]}]
                right.runtime_lineage["relocationRuntime"] = {"verified": True, "syntheticFixture": True}
        if mutate_snapshot:
            mutate_snapshot(left, right)
        return compare_snapshots(left, right, purpose)

    baseline = fixture()
    unchanged = run(copy.deepcopy(baseline), copy.deepcopy(baseline))
    checks.append(("native_self_comparison_not_cache_validation",
                   unchanged["measurementComparisonEqual"] and not unchanged["pass"]
                   and unchanged["cacheValidationStatus"] == "unverified_no_cache_reuse"))
    cached = copy.deepcopy(baseline)
    fixture_cache(cached)
    verified = run(copy.deepcopy(baseline), copy.deepcopy(cached))
    checks.append(("positive_cache_path_with_provenance", verified["pass"]))
    renamed = copy.deepcopy(cached)
    text = json.dumps(renamed).replace("native_1", "renamed_A").replace("native_2", "renamed_B").replace(r"C:\\fixture", r"D:\\new-copy")
    renamed = json.loads(text)
    renamed["instances"].reverse()
    renamed["expectedManifest"].reverse()
    renamed["hingeInterfaceMeasurements"].reverse()
    for row in renamed["interference"]["pairs"] + renamed["interference"]["collisions"]:
        row["componentA"], row["componentB"] = row["componentB"], row["componentA"]
    checks.append(("rename_relocation_reorder_and_pair_orientation",
                   run(copy.deepcopy(baseline), renamed)["pass"]))
    changed_bytes = copy.deepcopy(cached)
    for item in changed_bytes["instances"]:
        item["sha256"] = "B"*64
    changed_bytes["instances"][1]["bodies"][0]["topologyReadback"]["sourceActualSha256"] = "B"*64
    r = run(copy.deepcopy(baseline), changed_bytes)
    checks.append(("cross_build_sha_recorded_without_false_block",
                   r["pass"] and not r["byteIdentity"]["allInstanceFilesEqual"]
                   and r["byteIdentity"]["differentInstanceFileCount"] == 2))
    checks.append(("missing_lineage_unverified",
                   not run(copy.deepcopy(baseline), copy.deepcopy(cached), False)["pass"]))

    mutations = (
        ("cylinder_radius", lambda d: d["instances"][0]["bodies"][0]["cylinders"][0].update(radiusMm=2.01)),
        ("boundary_circle_length", lambda d: d["instances"][0]["bodies"][0]["cylinders"][0]["boundaryCircles"][0].update(edgeLengthMm=99)),
        ("circle_parameter_range", lambda d: d["instances"][0]["bodies"][0]["cylinders"][0]["boundaryCircles"][0]["edgeParameterRange"].__setitem__(1, 7.0)),
        ("finite_cylinder_status", lambda d: d["instances"][0]["bodies"][0]["cylinders"][0].update(completeFiniteCylinder=False)),
        ("native_surface_error", lambda d: d["instances"][0]["bodies"][0]["cylinders"][0]["errors"].append("READBACK_FAILED")),
        ("plane_position", lambda d: d["instances"][0]["bodies"][0]["planarFaces"][0]["rootPointMm"].__setitem__(2, .01)),
        ("interface_null_to_bool", lambda d: d["hingeInterfaceMeasurements"][0].update(mechanicalFitPass=True)),
        ("interface_candidate_count", lambda d: d["hingeInterfaceMeasurements"][0].update(candidateCount=2)),
        ("interference_volume", lambda d: d["interference"]["collisions"][0].update(volumeMm3=1.51)),
        ("interference_box", lambda d: d["interference"]["collisions"][0]["intersectionBoundsMm"].__setitem__(0, .01)),
        ("pair_missing", lambda d: d["interference"]["pairs"].clear()),
        ("role_duplicate", lambda d: d["instances"][1].update(identity="panel_1")),
        ("transform_change", lambda d: d["instances"][1]["transform"].__setitem__(9, .001)),
        ("forged_cache_identity", lambda d: d["instances"][1]["bodies"][0]["topologyReadback"]["sourceNativeObjectIdentityChecks"][0].update(sameIUnknown=False)),
        ("source_id_missing", lambda d: d["instances"][1]["bodies"][0]["cylinders"][0].update(sourceFaceIdentity="missing")),
    )
    for name, mutate in mutations:
        target = copy.deepcopy(cached)
        mutate(target)
        checks.append((name + "_rejected", not run(copy.deepcopy(baseline), target)["pass"]))
    c = Comparison()
    checks.extend([
        ("mm_within_tolerance", c.equal(1.0, 1.0009, key="radiusMm", report=False)),
        ("mm_outside_tolerance", not c.equal(1.0, 1.0011, key="radiusMm", report=False)),
        ("zero_to_positive_volume_rejected", not c.equal(0, 1e-12, key="volumeMm3", report=False)),
        ("bool_not_numeric_zero", not c.equal(False, 0, key="candidateCount", report=False)),
        ("nonfinite_rejected", not c.equal(float("nan"), float("nan"), key="radiusMm", report=False)),
    ])
    a, b = fixture_relocation()
    r = run(copy.deepcopy(a), copy.deepcopy(b), purpose="relocation")
    checks.append(("relocation_without_any_cache_hits_verified", r["pass"] and r["relocationMeasurementEquivalent"]
                   and r["documentMetadataComparison"]["equal"] and r["cacheValidationStatus"] == "not_requested"))
    checks.append(("relocation_preserves_engineering_failure", r["pass"] and r["baseline"]["interference"]["pass"] is False
                   and r["candidate"]["interference"]["pass"] is False and r["engineeringAcceptancePass"] is False
                   and r["stageAcceptance"] is False and r["production_release"] is False))
    checks.append(("relocation_schema_requires_explicit_purpose",
                   not run(copy.deepcopy(a), copy.deepcopy(b))["pass"]))
    aa, bb = fixture_relocation(True)
    r = run(copy.deepcopy(aa), copy.deepcopy(bb), purpose="relocation")
    checks.append(("relocation_allows_cached_baseline_and_zero_hit_candidate", r["pass"]
                   and r["baselineCacheEvidence"]["actualReusedBodies"] == 1
                   and r["candidateCacheEvidence"]["actualReusedBodies"] == 0))
    checks.append(("relocation_missing_runtime_lineage_rejected",
                   not run(copy.deepcopy(a), copy.deepcopy(b), False, "relocation")["pass"]))
    reordered = copy.deepcopy(b)
    reordered["documentMetadata"].reverse()
    for row in reordered["documentMetadata"]:
        row["configurations"].reverse()
        row["customPropertyScopes"].reverse()
    checks.append(("relocation_metadata_order_does_not_change_content",
                   run(copy.deepcopy(a), reordered, purpose="relocation")["pass"]))
    unspecified_a, unspecified_b = copy.deepcopy(a), copy.deepcopy(b)
    for data in (unspecified_a, unspecified_b):
        for row in data["documentMetadata"]:
            row.update(materialName=None, materialDatabase=None)
            for material in row["configurationMaterials"]:
                material.update(materialName=None, materialDatabase=None)
    unspecified_b["sourceEvidenceSha256"] = digest(json.dumps(unspecified_a, sort_keys=True).encode("utf-8"))
    checks.append(("relocation_preserves_legitimate_unspecified_material",
                   run(unspecified_a, unspecified_b, purpose="relocation")["pass"]))
    legacy = copy.deepcopy(cached)
    fixture_metadata(legacy)
    r = run(copy.deepcopy(baseline), legacy)
    checks.append(("legacy_cache_scope_never_claims_missing_document_metadata", r["pass"]
                   and r["documentMetadataComparison"]["baseline"]["recorded"] is False
                   and r["documentMetadataComparison"]["equal"] is None
                   and r["documentMetadataComparison"]["includedInPass"] is False))
    metadata_mutations = (
        ("missing_metadata", lambda d: d.pop("documentMetadata")),
        ("missing_metadata_instance", lambda d: d["documentMetadata"].pop()),
        ("metadata_duplicate_identity", lambda d: d["documentMetadata"][1].update(identity="panel_1")),
        ("metadata_material_name", lambda d: d["documentMetadata"][0].update(materialName="Other steel")),
        ("metadata_material_database", lambda d: d["documentMetadata"][0].update(materialDatabase="Other.sldmat")),
        ("metadata_material_field_absent", lambda d: d["documentMetadata"][0].pop("materialName")),
        ("metadata_all_configuration_materials_missing", lambda d: d["documentMetadata"][0].pop("configurationMaterials")),
        ("metadata_configuration_material_missing", lambda d: d["documentMetadata"][0]["configurationMaterials"].pop(0)),
        ("metadata_other_configuration_material_changed", lambda d: d["documentMetadata"][0]["configurationMaterials"][0].update(materialName="Changed alternate steel")),
        ("metadata_other_configuration_material_database_changed", lambda d: d["documentMetadata"][0]["configurationMaterials"][0].update(materialDatabase="Other.sldmat")),
        ("metadata_configuration_material_duplicate", lambda d: d["documentMetadata"][0]["configurationMaterials"][0].update(configuration="Default")),
        ("metadata_referenced_material_inconsistent", lambda d: d["documentMetadata"][0]["configurationMaterials"][1].update(materialName="Changed default steel")),
        ("metadata_referenced_configuration", lambda d: d["documentMetadata"][0].update(referencedConfiguration="Alternate")),
        ("metadata_configuration_list", lambda d: d["documentMetadata"][0]["configurations"].remove("Alternate")),
        ("metadata_document_scope_absent", lambda d: d["documentMetadata"][0]["customPropertyScopes"].pop(0)),
        ("metadata_empty_configuration_scope_absent", lambda d: d["documentMetadata"][0]["customPropertyScopes"].pop(1)),
        ("metadata_raw_property", lambda d: d["documentMetadata"][0]["customPropertyScopes"][0]["properties"][0].update(value="changed")),
        ("metadata_resolved_property", lambda d: d["documentMetadata"][0]["customPropertyScopes"][0]["properties"][0].update(resolved="changed")),
        ("metadata_linked_property", lambda d: d["documentMetadata"][0]["customPropertyScopes"][0]["properties"][0].update(linked=True)),
        ("metadata_named_property_not_present", lambda d: d["documentMetadata"][0]["customPropertyScopes"][0]["properties"][0].update(status=1)),
        ("metadata_missing_property", lambda d: d["documentMetadata"][0]["customPropertyScopes"][0]["properties"].clear()),
        ("metadata_property_count", lambda d: d["documentMetadata"][0]["customPropertyScopes"][0].update(propertyCount=2)),
        ("metadata_active_configuration_not_restored", lambda d: d["documentMetadata"][0].update(activeConfigurationAfterRestore="Alternate")),
        ("metadata_referenced_configuration_changed", lambda d: d["documentMetadata"][0].update(referencedConfigurationUnchanged=False)),
        ("metadata_incomplete_readback", lambda d: d["documentMetadata"][0].update(readbackComplete=False)),
        ("relocation_wrong_source_hash", lambda d: d.update(sourceEvidenceSha256="F"*64)),
        ("relocation_wrong_source_output", lambda d: d.update(sourceOutputDirectory=r"C:\other\case1")),
        ("relocation_mapping_missing", lambda d: d["referenceRemapping"].pop()),
        ("relocation_mapping_escapes_source", lambda d: d["referenceRemapping"][0].update(sourcePath=r"C:\outside\panel.SLDPRT")),
        ("relocation_inventory_missing", lambda d: d["readOnlyInputFiles"].clear()),
        ("relocation_inventory_changed", lambda d: d["readOnlyInputFiles"][0].update(sha256After="F"*64)),
        ("relocation_native_interference_incomplete", lambda d: d["interference"].update(complete=False)),
        ("relocation_interference_errors", lambda d: d["interference"]["errors"].append("native error")),
        ("relocation_unknown_pair", lambda d: d["interference"].update(unknownPairCount=1)),
        ("relocation_measured_pair_count", lambda d: d["interference"].update(measuredPairCount=0)),
        ("relocation_enumerated_pair_count", lambda d: d["interference"].update(enumeratedPairCount=0)),
        ("relocation_pair_missing", lambda d: d["interference"]["pairs"].clear()),
        ("relocation_reopen_missing", lambda d: d.pop("reopen")),
        ("relocation_reopen_not_cold", lambda d: d["reopen"].update(coldSession=False)),
        ("relocation_reopen_failed", lambda d: d["reopen"].__setitem__("pass", False)),
        ("relocation_reopen_count_wrong", lambda d: d["reopen"].update(componentCount=1)),
    )
    for name, mutate in metadata_mutations:
        target = copy.deepcopy(b)
        mutate(target)
        checks.append((name + "_rejected", not run(copy.deepcopy(a), target, purpose="relocation")["pass"]))
    copy_mutations = (
        ("direct_copy_wrong_action", lambda l, r: r.runtime_lineage["copyChain"][0].update(action="assembly")),
        ("direct_copy_wrong_source_root", lambda l, r: r.runtime_lineage["copyChain"][0].update(source=r"C:\other\cad")),
        ("direct_copy_wrong_evidence_hash", lambda l, r: r.runtime_lineage["copyChain"][0]["assemblyEvidenceCopies"][0].update(sha256="F"*64)),
        ("direct_copy_wrong_evidence_size", lambda l, r: r.runtime_lineage["copyChain"][0]["assemblyEvidenceCopies"][0].update(size=1)),
        ("direct_copy_wrong_evidence_relative_path", lambda l, r: r.runtime_lineage["copyChain"][0]["assemblyEvidenceCopies"][0].update(relativePath=r"assemblies\other\assembly-evidence.json")),
        ("direct_copy_evidence_row_missing", lambda l, r: r.runtime_lineage["copyChain"][0]["assemblyEvidenceCopies"].clear()),
    )
    for name, mutate in copy_mutations:
        checks.append((name + "_rejected", not run(copy.deepcopy(a), copy.deepcopy(b), purpose="relocation", mutate_snapshot=mutate)["pass"]))
    invalid_cache_a, invalid_cache_b = fixture_relocation(True)
    invalid_cache_a["instances"][1]["bodies"][0]["topologyReadback"]["sourceNativeObjectIdentityChecks"][0]["sameIUnknown"] = False
    invalid_cache_b["sourceEvidenceSha256"] = digest(json.dumps(invalid_cache_a, sort_keys=True).encode("utf-8"))
    r = run(invalid_cache_a, invalid_cache_b, purpose="relocation")
    checks.append(("relocation_still_rejects_invalid_existing_cache_provenance", not r["pass"] and r["validationStatus"] == "invalid_evidence"))
    r = run(copy.deepcopy(cached), copy.deepcopy(cached))
    checks.append(("default_cache_mode_still_rejects_cached_baseline", not r["pass"]
                   and r["cacheValidationStatus"] == "unverified_baseline_uses_cache"))
    variable_status = copy.deepcopy(b)
    for row in variable_status["documentMetadata"]:
        row["activeConfigurationAfterPropertyRead"] = "Default"
        for scope in row["customPropertyScopes"]:
            for value in scope["properties"]:
                value.update(status=0, wasResolved=False)
    checks.append(("relocation_runtime_property_status_changes_do_not_change_content",
                   run(copy.deepcopy(a), variable_status, purpose="relocation")["pass"]))
    checks.append(("relocation_requires_verified_owning_native_run",
                   not run(copy.deepcopy(a), copy.deepcopy(b), purpose="relocation",
                           mutate_snapshot=lambda l, r: r.runtime_lineage.update(relocationRuntime={"verified": False}))["pass"]))
    return {"schema": "winnsen.assembly-comparison-self-test/v1", "production_release": False,
            "pass": all(ok for _, ok in checks), "testCount": len(checks),
            "checks": [{"name": name, "pass": bool(ok)} for name, ok in checks]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", type=Path, nargs="?")
    parser.add_argument("candidate", type=Path, nargs="?")
    parser.add_argument("--out", type=Path, help="New JSON report; existing files are never overwritten")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--purpose", choices=("cache", "relocation"), default="cache",
                        help="cache preserves the controlled cache rules; relocation also requires complete unchanged document metadata and direct copy-source binding")
    args = parser.parse_args()
    if args.self_test:
        result = self_test()
        print(json.dumps(result, ensure_ascii=True, indent=2))
        return 0 if result["pass"] else 1
    if args.baseline is None or args.candidate is None:
        parser.error("baseline and candidate evidence JSON paths are required")
    left, right = read_snapshot(args.baseline, args.purpose), read_snapshot(args.candidate, args.purpose)
    result = compare_snapshots(left, right, args.purpose)
    result["tool"] = {"path": str(Path(__file__).resolve()), "sha256": digest(Path(__file__).read_bytes())}
    result["evidenceFilesUnchangedDuringComparison"] = (
        digest(args.baseline.read_bytes()) == left.sha256 and digest(args.candidate.read_bytes()) == right.sha256)
    if not result["evidenceFilesUnchangedDuringComparison"]:
        result["pass"], result["validationStatus"] = False, "evidence_changed_during_comparison"
        result["cacheValidationStatus" if args.purpose == "cache" else "relocationValidationStatus"] = "evidence_changed_during_comparison"
        if args.purpose == "relocation":
            result["relocationMeasurementEquivalent"] = False
    if args.out:
        target = args.out.resolve()
        if target in (args.baseline.resolve(), args.candidate.resolve()):
            parser.error("output must be a new report, not an input")
        with target.open("x", encoding="utf-8", newline="\n") as output:
            json.dump(result, output, ensure_ascii=False, indent=2, allow_nan=False)
            output.write("\n")
        print(json.dumps({"report": str(target), "pass": result["pass"],
                          "measurementComparisonEqual": result["measurementComparisonEqual"],
                          "cacheValidationStatus": result["cacheValidationStatus"],
                          "purpose": result["purpose"], "validationStatus": result["validationStatus"],
                          "relocationMeasurementEquivalent": result["relocationMeasurementEquivalent"],
                          "differenceCount": result["differenceCount"]}, ensure_ascii=True))
    else:
        print(json.dumps(result, ensure_ascii=True, indent=2, allow_nan=False))
    if args.purpose == "relocation" and result["validationStatus"] == "document_metadata_difference":
        return 1
    return 0 if result["pass"] else 2 if result["measurementComparisonEqual"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
