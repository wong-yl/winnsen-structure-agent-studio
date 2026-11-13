"""Synthetic filesystem unit tests only; no CAD process or real native evidence is used."""

import copy
import importlib.util
import json
import os
import shutil
import tempfile
import unittest
from datetime import datetime, timedelta
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("compare_resumed_relocation.py")
SPEC = importlib.util.spec_from_file_location("resumed_comparison_under_test", MODULE_PATH)
RESUMED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RESUMED)
CHECKER = RESUMED.load_checker()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=True, sort_keys=True), encoding="utf-8")


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return RESUMED.digest(path.read_bytes())


def inventory(root):
    return [{"relativePath": path.relative_to(root).as_posix(), "size": path.stat().st_size, "sha256": sha(path)}
            for path in sorted(root.rglob("*")) if path.is_file()]


def replace_root(data, old, new):
    return json.loads(json.dumps(data).replace(json.dumps(str(old))[1:-1], json.dumps(str(new))[1:-1]))


class SyntheticRuns:
    """Small text files and declared synthetic native records exercise binding logic."""

    def __init__(self, root):
        root = root.resolve()
        self.root = root
        self.old, self.resume, self.target = (root / "runs" / name for name in ("old-failed-T3", "resumed-T3", "T4"))
        self.seed = root / "original-seeds"
        self.seed.mkdir(parents=True)
        for index in range(60):
            (self.seed / ("seed_%02d.SLDPRT" % index)).write_bytes(("SYNTHETIC SEED %d" % index).encode())
        self.seed_inventory = inventory(self.seed)
        self.recovery_source = root / "synthetic-output-audit.json"
        write_json(self.recovery_source, {"syntheticFixture": True, "production_release": False, "fullPlanPass": False})
        self.recovery = {"schema": "16029-variable-artifact-recovery/v1", "oldFailurePreserved": True,
                         "oldSupervisionAccepted": False, "fullPlanPass": False, "production_release": False,
                         "sources": [{"path": str(self.recovery_source), "sha256": sha(self.recovery_source)}]}
        self.old_tool = self.toolchain("old", False)
        self.new_tool = self.toolchain("new", True)
        for run in (self.old, self.resume, self.target):
            (run / "cad").mkdir(parents=True)
        for row in self.seed_inventory:
            shutil.copyfile(self.seed / row["relativePath"], self.old / "cad" / row["relativePath"])
        self.output_cases = self.generated_outputs()
        self.doors = [{"hand": row["hand"], "variables": {"DOOR_W": row["widthMm"], "DOOR_H": row["heightMm"]}}
                      for row in self.output_cases]
        self.old_cases = [self.generated_case(self.old, i) for i in (1, 2)]
        self.third_layout = copy.deepcopy(self.old_cases[0]["layout"])
        self.third_layout["syntheticCaseIndex"] = 3
        self.layouts = [case["layout"] for case in self.old_cases] + [self.third_layout]
        failed_third = {"success": False, "acceptancePass": False, "production_release": False,
                        "layout": self.third_layout, "error": "SYNTHETIC ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED"}
        write_json(self.old / "cad/assemblies/case3/assembly-evidence.json", failed_third)
        (self.old / "cad/assemblies/case3/partial.SLDPRT").write_bytes(b"SYNTHETIC FAILED PART")
        old_job = self.job(self.old, self.seed, "assembly", self.old_tool)
        write_json(self.old / "job.json", old_job)
        write_json(self.old / "toolchain.json", self.old_tool)
        write_json(self.old / "copy-manifest.json", {"source": str(self.seed), "target": str(self.old / "cad"), "files": self.seed_inventory})
        write_json(self.old / "protected-before.json", {"syntheticFixture": True})
        write_json(self.old / "native-index-protection.json", {"syntheticFixture": True})
        self.native(self.old, "assembly", [{"inspection": "minimum_assembly", "index": i, "result": case}
                                           for i, case in enumerate(self.old_cases + [failed_third], 1)], failed=True)
        self.descriptor = {"schema": RESUMED.RESUME_SCHEMA, "sourceRun": str(self.old), "generateCaseIndices": [3],
                           "reuseCaseIndices": [1, 2], "sourceEvidence": [
                               {"path": str(self.old / name), "sha256": sha(self.old / name)} for name in sorted(RESUMED.SOURCE_EVIDENCE)],
                           "cases": [self.case_descriptor(self.old, i) for i in (1, 2)],
                           "mixedProvenance": True, "technicalRecoveryOnly": True, "recoveredCasesFromFailedPhase": True,
                           "oldFailurePreserved": True, "oldSupervisionAccepted": False,
                           "acceptancePass": False, "fullPlanPass": False, "production_release": False}
        old_inventory = inventory(self.old / "cad")
        copied = [row for row in old_inventory if not row["relativePath"].startswith("assemblies/case3/")]
        self.copy_files(self.old, self.resume, copied)
        write_json(self.resume / "copy-manifest.json", {"source": str(self.old / "cad"), "target": str(self.resume / "cad"),
                                                         "files": copied, "excludedRelativeDirectories": ["assemblies/case3"]})
        write_json(self.resume / "job.json", self.job(self.resume, self.old / "cad", "assembly", self.new_tool, self.descriptor))
        write_json(self.resume / "toolchain.json", self.new_tool)
        resume_rewrites = self.rewire(self.old, self.resume, (1, 2))
        receipt_rows = []
        for descriptor in self.descriptor["cases"]:
            for row in descriptor["inventory"]:
                copied_path = self.resume / "cad" / row["relativePath"]
                receipt_rows.append({"index": descriptor["index"], "path": str(copied_path),
                                     "sourcePath": str(self.old / "cad" / row["relativePath"]),
                                     "sourceSha256": row["sha256"], "copiedSha256": row["sha256"], "rewiredSha256": sha(copied_path)})
        write_json(self.resume / "cad/assembly-resume-receipt.json", {
            "schema": "16029-variable-assembly-resume-receipt/v1", "sourceRun": str(self.old), "mixedProvenance": True,
            "technicalRecoveryOnly": True, "acceptancePass": False, "production_release": False, "files": receipt_rows})
        self.third = self.generated_case(self.resume, 3)
        events = [{"inspection": "resume_copied_bytes_verified", "sourceRun": str(self.old), "reusedCaseCount": 2, "production_release": False}]
        events += resume_rewrites
        events += [{"inspection": "dependency_closure", "pass": True,
                    "fileCount": sum(Path(row["relativePath"]).suffix.upper() in RESUMED.CAD_EXTENSIONS for row in copied)},
                   {"inspection": "resume_rewired_copies_bound", "sourceRun": str(self.old), "reusedCaseCount": 2, "production_release": False}]
        events += [{"inspection": "minimum_assembly_reused", "index": row["index"],
                    "evidencePath": str(self.resume / "cad" / row["evidenceRelativePath"]), "evidenceSha256": row["evidenceSha256"],
                    "oldFailurePreserved": True, "acceptancePass": False, "production_release": False} for row in self.descriptor["cases"]]
        events += [{"inspection": "minimum_assembly", "index": 3, "result": self.third}]
        self.native(self.resume, "assembly", events)
        self.recovery_check(self.resume)
        self.source_check = dict(self.descriptor, newRun=str(self.resume), sourceEvidenceUnchanged=True,
                                 sourceCadFilesUnchanged=True, sourceInventory=old_inventory, finalCadInventory=inventory(self.resume / "cad"),
                                 cases=[self.case_descriptor(self.resume, i, copied_from=self.descriptor["cases"][i - 1]) for i in (1, 2)]
                                 + [self.case_descriptor(self.resume, 3)],
                                 jobSha256=sha(self.resume / "job.json"), nativeResultSha256=sha(self.resume / "native-result.json"))
        self.bind_source_check()
        new_inventory = inventory(self.resume / "cad")
        self.copy_files(self.resume, self.target, new_inventory)
        write_json(self.target / "copy-manifest.json", {"source": str(self.resume / "cad"), "target": str(self.target / "cad"), "files": new_inventory})
        write_json(self.target / "job.json", self.job(self.target, self.resume / "cad", "relocate", self.new_tool, self.descriptor))
        write_json(self.target / "toolchain.json", self.new_tool)
        target_rewrites = self.rewire(self.resume, self.target, (1, 2, 3))
        self.target_cases = [self.relocated_case(case, index) for index, case in enumerate(self.old_cases + [self.third], 1)]
        self.native(self.target, "relocate", target_rewrites + [{"inspection": "dependency_closure", "pass": True,
                    "fileCount": sum(Path(row["relativePath"]).suffix.upper() in RESUMED.CAD_EXTENSIONS for row in new_inventory)}]
                    + self.relocated_outputs()
                    + [{"inspection": "relocated_assembly", "path": case["outputDirectory"], "result": case} for case in self.target_cases])
        self.recovery_check(self.target)
        write_json(root / "recovery-T4-current.json", {"dir": str(self.target), "cadDirectory": str(self.target / "cad"),
                                                      "nativeResult": str(self.target / "native-result.json"), "resumeSourceRun": str(self.old),
                                                      "mixedProvenance": True, "production_release": False})

    def toolchain(self, name, changed):
        folder = self.root / "toolchain" / name
        (folder / "sources").mkdir(parents=True)
        names = ("Build", "ValidateLayout", "PrepareStructure", "PrepareHardware", "PreparePlacements", "BuildNativeAssembly")
        names += RESUMED.MEASUREMENT_METHODS + RESUMED.MEASUREMENT_HELPERS + ("RefreshNativeLinks",)
        lines = ["// SYNTHETIC UNIT FIXTURE, NEVER COMPILED OR EXECUTED\nclass Synthetic {\n"]
        for method in names:
            variant = changed and method in ("PrepareStructure", "RefreshNativeLinks")
            lines.append("    static void " + method + "()\n    {\n        // synthetic " + str(variant) + "\n    }\n")
        lines.append("}\n")
        (folder / "sources/TrialAssembly.cs").write_text("".join(lines), encoding="utf-8")
        for filename in ("TrialOutput.cs", "VariableTrial.cs"):
            (folder / "sources" / filename).write_text("// SYNTHETIC UNIT FIXTURE " + filename, encoding="utf-8")
        (folder / "VariableTrial.exe").write_bytes(b"SYNTHETIC NOT AN EXECUTABLE")
        for filename in ("SolidWorks.Interop.sldworks.dll", "SolidWorks.Interop.swconst.dll"):
            (folder / filename).write_bytes(b"SYNTHETIC NOT A DLL")
        return {"schema": "winnsen.variable_trial.toolchain.v2", "exe": str(folder / "VariableTrial.exe"),
                "sha256": sha(folder / "VariableTrial.exe"), "sources": [
                    {"path": str(folder / "sources" / filename), "archivePath": str(folder / "sources" / filename),
                     "sha256": sha(folder / "sources" / filename)} for filename in ("TrialAssembly.cs", "TrialOutput.cs", "VariableTrial.cs")],
                "dlls": [{"path": str(folder / filename), "sha256": sha(folder / filename)}
                         for filename in ("SolidWorks.Interop.sldworks.dll", "SolidWorks.Interop.swconst.dll")]}

    def job(self, run, source_cad, action, toolchain, descriptor=None):
        result = {"action": action, "outputScope": "parts_dxf_only", "production_release": False,
                  "cadDirectory": str(run / "cad"), "sourceCadDirectory": str(source_cad),
                  "resultPath": str(run / "native-result.json"), "toolchainPath": str(run / "toolchain.json"),
                  "doors": self.doors, "layouts": self.layouts, "specification": {"synthetic": True},
                  "process": {"synthetic": True}, "inputVersions": [{"path": str(self.seed / "seed_00.SLDPRT"), "sha256": sha(self.seed / "seed_00.SLDPRT")}],
                  "recovery": self.recovery}
        if descriptor is not None:
            result["assemblyResume"] = descriptor
        return result

    def generated_outputs(self):
        rows = []
        for index, (hand, width, height) in enumerate((("L", 437, 471.25), ("R", 437, 471.25),
                                                     ("L", 287, 200), ("R", 287, 200),
                                                     ("L", 537, 1000), ("R", 537, 1000))):
            folder = hand + str(index)
            part = self.old / "cad/outputs" / folder / ("door_" + hand + "_SYNTHETIC.SLDPRT")
            part.parent.mkdir(parents=True)
            part.write_bytes(("SYNTHETIC OUTPUT PART " + folder).encode())
            geometry = {"body": {"name": "SYNTHETIC FLAT " + folder}, "bodyCount": 1,
                        "widthHeightThicknessMm": [width + 30, height + 30, 0.8], "sheetMetalSolid": True,
                        "flatGeometryVerified": True, "cutGeometry": {"complete": True, "errors": []},
                        "manufacturingAccepted": False}
            rows.append({"folder": folder, "part": part.name, "hand": hand, "widthMm": width, "heightMm": height,
                         "partSha256": sha(part), "activeConfiguration": "Default",
                         "configurations": ["Default", "DefaultSM-FLAT-PATTERN"], "drawing": None,
                         "flatPreparation": {"success": True, "modelPath": str(part),
                                             "flatConfiguration": "DefaultSM-FLAT-PATTERN",
                                             "configurationNames": ["Default", "DefaultSM-FLAT-PATTERN"],
                                             "geometry": geometry, "manufacturingAccepted": False}})
        write_json(self.old / "cad/outputs/output-manifest.json", {
            "schema": "16029-variable-output-manifest/v2", "complete": True, "cases": rows,
            "outputScope": "parts_dxf_only", "drawingStatus": "deferred_by_user_20261113", "production_release": False})
        return rows

    def relocated_outputs(self):
        events = []
        for row in self.output_cases:
            model = self.target / "cad/outputs" / row["folder"] / row["part"]
            events.append({"inspection": "relocated_panel", "path": str(model), "hand": row["hand"],
                           "widthMm": row["widthMm"], "heightMm": row["heightMm"], "savedActiveConfiguration": "Default"})
            result = {"pass": True, "modelPath": str(model), "flatConfiguration": "DefaultSM-FLAT-PATTERN",
                      "configurationNames": row["configurations"], "geometry": copy.deepcopy(row["flatPreparation"]["geometry"]),
                      "originalConfigurationRestored": True, "featureSuppressionUnchanged": True,
                      "partDiskUnchanged": True, "partSaveCalled": False, "manufacturingAccepted": False,
                      "diagnosticFiles": [str(model.parent / ("flat-reopen.trace-%03d.json" % i)) for i in (1, 2)]}
            events.append({"inspection": "relocated_flat", "path": str(model), "result": result})
        return events

    def generated_case(self, run, index):
        source, _ = CHECKER.fixture_relocation()
        folder = run / "cad/assemblies" / ("case" + str(index))
        source = replace_root(source, source["outputDirectory"], folder)
        (folder / "parts").mkdir(parents=True, exist_ok=True)
        part, assembly = folder / "parts/panel.SLDPRT", folder / "16029_variable_minimum.SLDASM"
        part.write_bytes(("SYNTHETIC PART " + str(index)).encode())
        assembly.write_bytes(("SYNTHETIC ASSEMBLY " + str(index)).encode())
        source["layout"]["syntheticCaseIndex"] = index
        source["assemblySha256"] = sha(assembly)
        source["reopen"]["assemblySha256"] = source["assemblySha256"]
        for row in source["instances"]:
            row["sha256"] = sha(part)
        source["parts"] = [{"path": str(part), "sha256": sha(part)}]
        source["measurementPerformance"] = {"topologyCacheHits": 0, "nativeSurfaceScans": 2}
        write_json(folder / "assembly-evidence.json", source)
        return source

    def case_descriptor(self, run, index, copied_from=None):
        relative = "assemblies/case" + str(index) + "/assembly-evidence.json"
        path = run / "cad" / relative
        data = read_json(path)
        result = {"index": index, "layout": data["layout"], "evidenceRelativePath": relative,
                  "evidenceSha256": sha(path), "assemblySha256": data["assemblySha256"],
                  "inventory": [row for row in inventory(run / "cad") if row["relativePath"].startswith("assemblies/case" + str(index) + "/")]}
        if copied_from:
            result = dict(copied_from, inventory=result["inventory"])
        return result

    def copy_files(self, source, target, rows):
        for row in rows:
            path = target / "cad" / row["relativePath"]
            path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / "cad" / row["relativePath"], path)

    def rewire(self, source, target, indices):
        events = []
        for index in indices:
            for name, dependency in (("parts/panel.SLDPRT", "seed_00.SLDPRT"),
                                     ("16029_variable_minimum.SLDASM", "assemblies/case" + str(index) + "/parts/panel.SLDPRT")):
                path = target / "cad/assemblies" / ("case" + str(index)) / name
                path.write_bytes(path.read_bytes() + b" SYNTHETIC REFERENCE REWIRE")
                events.append({"inspection": "rewrite_reference", "file": str(path),
                               "old": str(source / "cad" / dependency), "target": str(target / "cad" / dependency), "success": True})
        return events

    def native(self, run, action, events, failed=False):
        executable = r"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe"
        claim = {"inspection": "cad_owned", "pid": 1001, "startTicks": 1002,
                 "startedAtUtc": "2026-11-13T00:00:00Z", "executable": executable, "revision": "28.synthetic"}
        error = "SYNTHETIC MINIMUM_ASSEMBLY_GENERATION_FAILED case3" if failed else None
        rows = [claim] + events
        if failed:
            rows.append({"inspection": "failure", "error": error})
        rows.append({"inspection": "finished", "success": not failed, "cadExited": True, "error": error})
        start = datetime(2026, 11, 13)
        times = [{"index": i, "atUtc": (start + timedelta(seconds=i * 10)).isoformat() + "Z", "elapsedMs": i * 10000}
                 for i in range(len(rows))]
        for index, row in enumerate(rows):
            if row.get("inspection") == "relocated_flat":
                cleanup = {"originalConfiguration": "Default", "originalConfigurationRestored": True,
                           "partDiskUnchanged": True, "featureSuppressionUnchanged": True, "partSaveCalled": False}
                result = row["result"]
                for number, (stage, details) in enumerate((("native_flat_body", result["geometry"]), ("reopened_flat_cleanup", cleanup))):
                    write_json(Path(result["diagnosticFiles"][number]), {
                        "inspection": "trial_native_flat_output", "stage": stage, "details": details,
                        "atUtc": (start + timedelta(seconds=index * 10 - 8 + number * 6)).isoformat() + "Z"})
        write_json(run / "native-result.json", {"syntheticFixture": True, "action": action, "cadPid": 1001,
                                                "cadTicks": 1002, "cadExe": executable, "production_release": False,
                                                "events": rows, "eventTimes": times})
        write_json(run / "protection-result.json", {"pass": True, "changed": []})
        write_json(run / "supervision.json", {"state": "cad_exited", "nativeEnded": True, "cad": []})
        if not failed:
            write_json(run / "result.json", {"code": 0, "cadExited": True})

    def relocated_case(self, original, index):
        folder = self.target / "cad/assemblies" / ("case" + str(index))
        data = replace_root(original, original["outputDirectory"], folder)
        data.update(schema=CHECKER.RELOCATION_SCHEMA, sourceOutputDirectory=original["outputDirectory"],
                    sourceAssemblyPath=original["assemblyPath"], sourceAssemblySha256=original["assemblySha256"],
                    sourceEvidencePath=str(folder / "assembly-evidence.json"), sourceEvidenceSha256=sha(folder / "assembly-evidence.json"),
                    sourceEvidenceUnchanged=True, referenceRemappingExpected=True, assemblySha256=sha(folder / "16029_variable_minimum.SLDASM"))
        data["referenceRemapping"] = [{"identity": before["identity"], "sourcePath": before["path"], "path": after["path"]}
                                      for before, after in zip(original["instances"], data["instances"])]
        for row in data["instances"]:
            row["sha256"] = sha(Path(row["path"]))
        data["parts"] = []
        data["reopen"]["coldSession"] = True
        data["reopen"]["assemblySha256"] = data["assemblySha256"]
        data["readOnlyInputFiles"] = [{"path": str(self.target / "cad" / row["relativePath"]),
                                       "sha256Before": row["sha256"], "sha256After": row["sha256"]}
                                      for row in inventory(self.target / "cad") if Path(row["relativePath"]).suffix.upper() in RESUMED.CAD_EXTENSIONS]
        write_json(folder / "relocation-evidence.json", data)
        return data

    def recovery_check(self, run):
        data = dict(self.recovery, newRun=str(run), outputsUnchanged=True, sourcesUnchanged=True)
        if run == self.target:
            data["assemblyResume"] = self.descriptor
        write_json(run / "recovery-source-check.json", data)

    def bind_source_check(self):
        path = self.resume / "assembly-resume-source-check.json"
        write_json(path, self.source_check)
        write_json(self.root / "recovery-T3-current.json", {"dir": str(self.resume), "cadDirectory": str(self.resume / "cad"),
                                                           "nativeResult": str(self.resume / "native-result.json"), "resumeSourceRun": str(self.old),
                                                           "mixedProvenance": True, "production_release": False, "resumeSourceCheckSha256": sha(path)})

    def rebind_receipt(self, receipt):
        path = self.resume / "cad/assembly-resume-receipt.json"
        write_json(path, receipt)
        shutil.copyfile(path, self.target / "cad/assembly-resume-receipt.json")
        self.source_check["finalCadInventory"] = inventory(self.resume / "cad")
        self.bind_source_check()
        manifest = read_json(self.target / "copy-manifest.json")
        manifest["files"] = inventory(self.resume / "cad")
        write_json(self.target / "copy-manifest.json", manifest)

    def mutate_target_evidence(self, index, mutate):
        path = self.target / "cad/assemblies" / ("case" + str(index)) / "relocation-evidence.json"
        data = read_json(path)
        mutate(data)
        write_json(path, data)
        native = read_json(self.target / "native-result.json")
        for row in native["events"]:
            if row.get("inspection") == "relocated_assembly" and row["path"] == str(path.parent):
                row["result"] = data
        write_json(self.target / "native-result.json", native)

    def mutate_target_native(self, mutate):
        path = self.target / "native-result.json"
        native = read_json(path)
        mutate(native)
        write_json(path, native)

    def compare(self, baseline_index=1, candidate_index=None):
        candidate_index = candidate_index or baseline_index
        return RESUMED.compare_files(self.resume / "cad/assemblies" / ("case" + str(baseline_index)) / "assembly-evidence.json",
                                     self.target / "cad/assemblies" / ("case" + str(candidate_index)) / "relocation-evidence.json")


@unittest.skipUnless(os.name == "nt", "The frozen checker verifies native Windows absolute paths")
class ResumedRelocationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="resumed-comparison-SYNTHETIC-")
        self.addCleanup(self.temporary.cleanup)
        self.fixture = SyntheticRuns(Path(self.temporary.name) / "data")

    def rejected(self, text=None):
        report = self.fixture.compare()
        self.assertFalse(report["pass"])
        self.assertFalse(report["engineeringAcceptancePass"])
        self.assertFalse(report["production_release"])
        if text:
            self.assertIn(text, " ".join(report["resumeCaseProvenance"]["errors"]))
        return report

    def test_synthetic_complete_chain_corrects_only_actual_toolchain_identity(self):
        report = self.fixture.compare()
        self.assertTrue(report["pass"], report["resumeCaseProvenance"]["errors"])
        proof = report["resumeCaseProvenance"]
        self.assertTrue(proof["verified"])
        self.assertEqual(set(proof["sourceGenerationDifferences"]), {"PrepareStructure", "RefreshNativeLinks"})
        diagnostics = proof["flatReopenDiagnostics"]
        self.assertTrue(diagnostics["verified"])
        self.assertTrue(diagnostics["sourceHadNoFlatReopenTraces"])
        self.assertEqual(len(diagnostics["models"]), 6)
        self.assertEqual(len(diagnostics["addedRelativePaths"]), 12)
        self.assertEqual(proof["inventoryFileCounts"]["targetT4"] - proof["inventoryFileCounts"]["resumedT3"], 15)
        references = {row["path"]: row for row in report["provenanceReferences"]}
        self.assertEqual(diagnostics["nativeResult"]["sha256"], sha(self.fixture.target / "native-result.json"))
        for model in diagnostics["models"]:
            self.assertEqual(model["model"]["sha256"], sha(Path(model["model"]["path"])))
            self.assertEqual([row["stage"] for row in model["diagnostics"]], ["native_flat_body", "reopened_flat_cleanup"])
            for row in model["diagnostics"]:
                self.assertEqual(row["sha256"], sha(Path(row["path"])))
                self.assertEqual(row["sha256"], references[row["path"]]["sha256"])
        runtime = report["sourceLineage"]["runtimeProof"]
        self.assertEqual(runtime["baseline"]["measurementToolchain"]["toolchainPath"], str(self.fixture.old / "toolchain.json"))
        self.assertEqual(runtime["candidate"]["buildToolchain"]["toolchainPath"], str(self.fixture.old / "toolchain.json"))
        self.assertEqual(runtime["candidate"]["measurementToolchain"]["toolchainPath"], str(self.fixture.target / "toolchain.json"))
        observed = report["observedLineage"]["runtimeProof"]
        self.assertEqual(observed["baseline"]["measurementToolchain"]["toolchainPath"], str(self.fixture.resume / "toolchain.json"))
        for field in ("copyChain", "rootSourceDirectory", "rootInputs", "specification", "process", "inputVersions", "errors"):
            self.assertEqual(runtime["baseline"][field], observed["baseline"][field])
            self.assertEqual(runtime["candidate"][field], observed["candidate"][field])
        self.assertFalse(report["engineeringAcceptancePass"])
        self.assertFalse(report["stageAcceptance"])
        self.assertFalse(report["production_release"])

    def test_synthetic_case2_uses_its_own_complete_descriptor(self):
        report = self.fixture.compare(2)
        self.assertTrue(report["pass"], report["resumeCaseProvenance"]["errors"])
        self.assertEqual(report["resumeCaseProvenance"]["caseIndex"], 2)

    def test_generated_case3_uses_original_checker_without_correction(self):
        report = self.fixture.compare(3)
        self.assertTrue(report["pass"], report["resumeCaseProvenance"]["errors"])
        self.assertFalse(report["resumeCaseProvenance"]["applicable"])
        self.assertEqual(report["sourceLineage"]["runtimeProof"], report["observedLineage"]["runtimeProof"])

    def test_receipt_source_hash_tampering_is_rejected_even_with_updated_inventories(self):
        receipt = read_json(self.fixture.resume / "cad/assembly-resume-receipt.json")
        receipt["files"][0]["sourceSha256"] = "F" * 64
        self.fixture.rebind_receipt(receipt)
        self.rejected("resume_receipt_source_copy_or_rewire_hash_mismatch")

    def test_receipt_rewired_hash_tampering_is_rejected(self):
        receipt = read_json(self.fixture.resume / "cad/assembly-resume-receipt.json")
        receipt["files"][0]["rewiredSha256"] = "F" * 64
        self.fixture.rebind_receipt(receipt)
        self.rejected("resume_receipt_source_copy_or_rewire_hash_mismatch")

    def test_receipt_other_case_index_is_rejected(self):
        receipt = read_json(self.fixture.resume / "cad/assembly-resume-receipt.json")
        receipt["files"][0]["index"] = 2
        self.fixture.rebind_receipt(receipt)
        self.rejected("resume_receipt_source_copy_or_rewire_hash_mismatch")

    def test_receipt_missing_member_is_rejected(self):
        receipt = read_json(self.fixture.resume / "cad/assembly-resume-receipt.json")
        receipt["files"].pop()
        self.fixture.rebind_receipt(receipt)
        self.rejected("resume_receipt_source_copy_or_rewire_hash_mismatch")

    def test_original_source_cad_tampering_is_rejected(self):
        path = self.fixture.old / "cad/assemblies/case1/parts/panel.SLDPRT"
        path.write_bytes(b"SYNTHETIC SOURCE TAMPERING")
        self.rejected("completed_source_or_final_inventory_changed")

    def test_original_source_evidence_hash_tampering_is_rejected(self):
        path = self.fixture.old / "toolchain.json"
        path.write_bytes(path.read_bytes() + b" ")
        self.rejected("resume_original_source_evidence_hash_mismatch")

    def test_missing_completed_source_report_is_rejected(self):
        (self.fixture.resume / "assembly-resume-source-check.json").unlink()
        self.rejected()

    def test_missing_resume_descriptor_cannot_fall_back_to_incorrect_toolchain(self):
        path = self.fixture.resume / "job.json"
        job = read_json(path)
        del job["assemblyResume"]
        write_json(path, job)
        self.rejected("copied_baseline_requires_explicit_assembly_resume_descriptor")

    def test_candidate_source_evidence_hash_binding_is_retained(self):
        self.fixture.mutate_target_evidence(1, lambda data: data.__setitem__("sourceEvidenceSha256", "F" * 64))
        report = self.rejected("complete_target_cold_readback_or_runtime_failed")
        self.assertIn("source_evidence_hash_mismatch", report["candidate"]["errors"])
        self.assertFalse(report["relocationSourceBinding"]["conditions"]["copiedSourceEvidenceIsExactBaseline"])

    def test_direct_copy_baseline_path_check_is_retained(self):
        report = RESUMED.compare_files(self.fixture.old / "cad/assemblies/case1/assembly-evidence.json",
                                      self.fixture.target / "cad/assemblies/case1/relocation-evidence.json")
        self.assertFalse(report["pass"])
        self.assertFalse(report["relocationSourceBinding"]["conditions"]["directCopySourcePathIsBaseline"])

    def test_stale_source_report_pointer_hash_is_rejected(self):
        path = self.fixture.root / "recovery-T3-current.json"
        pointer = read_json(path)
        pointer["resumeSourceCheckSha256"] = "F" * 64
        write_json(path, pointer)
        self.rejected("resume_source_check_pointer_hash_not_bound")

    def test_missing_output_recovery_report_is_rejected(self):
        (self.fixture.target / "recovery-source-check.json").unlink()
        self.rejected()

    def test_target_recovery_report_requires_resume_descriptor(self):
        path = self.fixture.target / "recovery-source-check.json"
        check = read_json(path)
        del check["assemblyResume"]
        write_json(path, check)
        self.rejected("target_recovery_resume_binding_missing")

    def test_target_pointer_provenance_cannot_contradict_actual_job(self):
        path = self.fixture.root / "recovery-T4-current.json"
        pointer = read_json(path)
        pointer["resumeSourceRun"] = str(self.fixture.resume)
        write_json(path, pointer)
        self.rejected("target_recovery_pointer_not_bound")

    def test_target_job_resume_descriptor_is_required(self):
        path = self.fixture.target / "job.json"
        job = read_json(path)
        del job["assemblyResume"]
        write_json(path, job)
        self.rejected("resume_descriptor_not_bound_across_runs")

    def test_wrong_case_candidate_is_rejected(self):
        report = self.fixture.compare(1, 2)
        self.assertFalse(report["pass"])
        self.assertIn("baseline_and_candidate_do_not_bind_same_reused_case", report["resumeCaseProvenance"]["errors"])

    def test_duplicate_case_descriptors_are_rejected(self):
        path = self.fixture.resume / "job.json"
        job = read_json(path)
        job["assemblyResume"]["cases"][1]["index"] = 1
        write_json(path, job)
        self.rejected("exact_two_reused_cases_and_one_generated_case_required")

    def test_wrong_copy_hop_is_rejected(self):
        path = self.fixture.target / "copy-manifest.json"
        manifest = read_json(path)
        manifest["source"] = str(self.fixture.old / "cad")
        write_json(path, manifest)
        self.rejected("exact_copy_hop_not_bound")

    def test_unbound_target_file_is_rejected(self):
        (self.fixture.target / "cad/unbound.txt").write_text("SYNTHETIC", encoding="utf-8")
        self.rejected("final_cad_inventory_has_unbound_members")

    def test_extra_flat_reopen_trace_is_not_implicitly_allowed(self):
        write_json(self.fixture.target / "cad/outputs/L0/flat-reopen.trace-003.json", {"syntheticFixture": True})
        self.rejected("final_cad_inventory_has_unbound_members")

    def test_unknown_json_and_cad_members_remain_rejected(self):
        for name in ("unknown.json", "unknown.SLDPRT"):
            with self.subTest(name=name):
                path = self.fixture.target / "cad/outputs/L0" / name
                path.write_bytes(b"SYNTHETIC UNKNOWN MEMBER")
                try:
                    self.rejected("final_cad_inventory_has_unbound_members")
                finally:
                    path.unlink()

    def test_native_diagnostic_paths_must_be_the_exact_two_new_json_files(self):
        native_path = self.fixture.target / "native-result.json"
        original = read_json(native_path)
        model = next(row for row in original["events"] if row.get("inspection") == "relocated_flat")["result"]
        first, second = model["diagnosticFiles"]
        folder = Path(first).parent
        variants = {"missing": [], "extra": [first, second, str(folder / "flat-reopen.trace-003.json")],
                    "duplicate": [first, first], "cross_group": [str(folder.parent / "R1/flat-reopen.trace-001.json"), second],
                    "escape": [str(folder / "../L0/flat-reopen.trace-001.json"), second],
                    "cad": [model["modelPath"], second]}
        for label, paths in variants.items():
            with self.subTest(label=label):
                native = copy.deepcopy(original)
                next(row for row in native["events"] if row.get("inspection") == "relocated_flat")["result"]["diagnosticFiles"] = paths
                write_json(native_path, native)
                try:
                    self.rejected("flat_reopen_exact_two_diagnostic_paths_required")
                finally:
                    write_json(native_path, original)

    def test_missing_or_duplicate_native_panel_and_flat_identity_is_rejected(self):
        native_path = self.fixture.target / "native-result.json"
        original = read_json(native_path)
        for inspection in ("relocated_panel", "relocated_flat"):
            with self.subTest(inspection=inspection):
                native = copy.deepcopy(original)
                next(row for row in native["events"] if row.get("inspection") == inspection)["inspection"] = "synthetic_missing_event"
                write_json(native_path, native)
                try:
                    self.rejected("flat_reopen_exact_six_native_panel_and_flat_events_required")
                finally:
                    write_json(native_path, original)
        native = copy.deepcopy(original)
        flats = [row for row in native["events"] if row.get("inspection") == "relocated_flat"]
        flats[1]["path"] = flats[0]["path"]
        write_json(native_path, native)
        self.rejected("flat_reopen_unique_native_model_events_required")

    def test_native_flat_model_path_cannot_reference_a_different_panel(self):
        def mutate(native):
            flats = [row for row in native["events"] if row.get("inspection") == "relocated_flat"]
            flats[0]["result"]["modelPath"] = flats[1]["path"]
        self.fixture.mutate_target_native(mutate)
        self.rejected("flat_reopen_native_model_identity_not_bound")

    def test_native_panel_identity_must_match_the_copied_output_manifest(self):
        self.fixture.mutate_target_native(lambda native: next(row for row in native["events"]
            if row.get("inspection") == "relocated_panel").__setitem__("widthMm", 438))
        self.rejected("flat_reopen_native_model_identity_not_bound")

    def test_native_flat_success_and_cleanup_flags_cannot_be_changed(self):
        native_path = self.fixture.target / "native-result.json"
        original = read_json(native_path)
        for field, value in (("pass", False), ("partSaveCalled", True), ("manufacturingAccepted", True)):
            with self.subTest(field=field):
                native = copy.deepcopy(original)
                next(row for row in native["events"] if row.get("inspection") == "relocated_flat")["result"][field] = value
                write_json(native_path, native)
                try:
                    self.rejected("flat_reopen_native_success_or_cleanup_not_bound")
                finally:
                    write_json(native_path, original)

    def test_missing_native_diagnostic_file_is_rejected(self):
        (self.fixture.target / "cad/outputs/L0/flat-reopen.trace-001.json").unlink()
        self.rejected("flat_reopen_diagnostic_not_unique_new_member")

    def test_diagnostic_replacement_from_another_panel_is_rejected(self):
        folder = self.fixture.target / "cad/outputs"
        shutil.copyfile(folder / "R1/flat-reopen.trace-001.json", folder / "L0/flat-reopen.trace-001.json")
        self.rejected("flat_reopen_diagnostic_native_content_mismatch")

    def test_diagnostic_stage_inspection_geometry_and_cleanup_must_match_native(self):
        folder = self.fixture.target / "cad/outputs/L0"
        variants = ((1, "stage", lambda row: row.__setitem__("stage", "reopened_flat_cleanup")),
                    (1, "inspection", lambda row: row.__setitem__("inspection", "synthetic_unknown")),
                    (1, "geometry", lambda row: row["details"]["body"].__setitem__("name", "SYNTHETIC REPLACEMENT")),
                    (2, "cleanup", lambda row: row["details"].__setitem__("partDiskUnchanged", False)),
                    (2, "boolean_type", lambda row: row["details"].__setitem__("partDiskUnchanged", 1)))
        for number, label, mutate in variants:
            with self.subTest(label=label):
                path = folder / ("flat-reopen.trace-%03d.json" % number)
                original = read_json(path)
                row = copy.deepcopy(original)
                mutate(row)
                write_json(path, row)
                try:
                    self.rejected("flat_reopen_diagnostic_native_content_mismatch")
                finally:
                    write_json(path, original)

    def test_diagnostic_time_must_be_utc_ordered_and_inside_its_own_native_events(self):
        folder = self.fixture.target / "cad/outputs/L0"
        path = folder / "flat-reopen.trace-001.json"
        original = read_json(path)
        cleanup = read_json(folder / "flat-reopen.trace-002.json")
        other = read_json(folder.parent / "R1/flat-reopen.trace-001.json")
        for label, value in (("before_run", "2026-11-12T00:00:00Z"), ("after_run", "2026-11-14T00:00:00Z"),
                             ("wrong_panel_window", other["atUtc"]), ("not_before_cleanup", cleanup["atUtc"]),
                             ("non_utc", original["atUtc"].replace("Z", "+00:00"))):
            with self.subTest(label=label):
                row = dict(original, atUtc=value)
                write_json(path, row)
                try:
                    self.rejected("flat_reopen_invalid_utc_timestamp" if label == "non_utc"
                                  else "flat_reopen_diagnostic_time_not_bound_to_native_model_events")
                finally:
                    write_json(path, original)

    def test_native_event_time_indices_must_bind_the_recorded_events(self):
        self.fixture.mutate_target_native(lambda native: native["eventTimes"][1].__setitem__("index", 2))
        self.rejected("flat_reopen_native_event_times_not_bound")

    def test_output_manifest_bytes_must_remain_the_exact_source_copy(self):
        path = self.fixture.target / "cad/outputs/output-manifest.json"
        path.write_bytes(path.read_bytes() + b" ")
        self.rejected("flat_reopen_output_manifest_not_exact_copy")

    def test_flat_output_part_must_match_the_copied_manifest_sha256(self):
        path = self.fixture.target / "cad/outputs/L0" / self.fixture.output_cases[0]["part"]
        path.write_bytes(b"SYNTHETIC PART REPLACEMENT")
        self.rejected("flat_reopen_output_part_hash_not_bound")

    def test_source_with_prior_flat_reopen_diagnostics_is_outside_the_exact_contract(self):
        path = self.fixture.target / "native-result.json"
        reads = RESUMED.EvidenceReads()
        native = reads.read(path)
        source_inventory = RESUMED.row_map(inventory(self.fixture.resume / "cad"))
        final_inventory = reads.inventory(self.fixture.target / "cad")
        source_inventory["outputs/l0/flat-reopen.trace-001.json"] = final_inventory["outputs/l0/flat-reopen.trace-001.json"]
        with self.assertRaisesRegex(ValueError, "flat_reopen_requires_source_without_prior_trace_files"):
            RESUMED.relocated_flat_diagnostics(reads, self.fixture.target, read_json(self.fixture.target / "job.json"),
                                               native, source_inventory, final_inventory)

    def test_diagnostic_hashes_cannot_change_after_inventory_and_native_binding(self):
        for phase in ("before_binding", "after_binding"):
            with self.subTest(phase=phase):
                reads = RESUMED.EvidenceReads()
                native = reads.read(self.fixture.target / "native-result.json")
                source_inventory = RESUMED.row_map(inventory(self.fixture.resume / "cad"))
                final_inventory = reads.inventory(self.fixture.target / "cad")
                path = self.fixture.target / "cad/outputs/L0/flat-reopen.trace-001.json"
                original = path.read_bytes()
                if phase == "after_binding":
                    proof = RESUMED.relocated_flat_diagnostics(reads, self.fixture.target,
                        read_json(self.fixture.target / "job.json"), native, source_inventory, final_inventory)
                    self.assertTrue(proof["verified"])
                path.write_bytes(original + b" ")
                try:
                    with self.assertRaisesRegex(ValueError, "evidence_changed_while_reading"):
                        if phase == "before_binding":
                            RESUMED.relocated_flat_diagnostics(reads, self.fixture.target,
                                read_json(self.fixture.target / "job.json"), native, source_inventory, final_inventory)
                        else:
                            reads.unchanged()
                finally:
                    path.write_bytes(original)

    def test_changed_cad_without_native_relative_rewrite_is_rejected(self):
        path = self.fixture.target / "native-result.json"
        native = read_json(path)
        retained = [(row, timing) for row, timing in zip(native["events"], native["eventTimes"])
                    if not (row.get("inspection") == "rewrite_reference" and row["file"].endswith("case1\\parts\\panel.SLDPRT"))]
        native["events"] = [row for row, timing in retained]
        native["eventTimes"] = [dict(timing, index=i) for i, (row, timing) in enumerate(retained)]
        write_json(path, native)
        self.rejected("copied_bytes_changed_without_native_relative_rewrite")

    def test_wrong_toolchain_archive_hash_is_rejected(self):
        path = self.fixture.root / "toolchain/new/sources/TrialAssembly.cs"
        path.write_bytes(path.read_bytes() + b"// SYNTHETIC TAMPERING")
        self.rejected("archived_TrialAssembly_hash_mismatch")

    def change_measurement_source(self, method):
        old_folder = self.fixture.root / "toolchain/new"
        new_folder = self.fixture.root / "toolchain/target-changed"
        shutil.copytree(old_folder, new_folder)
        path = new_folder / "sources/TrialAssembly.cs"
        source = path.read_text(encoding="utf-8")
        source = source.replace("static void " + method + "()\n    {", "static void " + method + "()\n    {\n        // changed synthetic measurement")
        path.write_text(source, encoding="utf-8")
        manifest = replace_root(read_json(self.fixture.target / "toolchain.json"), old_folder, new_folder)
        for row in manifest["sources"]:
            row["sha256"] = sha(Path(row["archivePath"]))
        write_json(self.fixture.target / "toolchain.json", manifest)

    def test_changed_measurement_method_is_rejected(self):
        self.change_measurement_source("MeasureInterference")
        self.rejected("actual_old_and_target_measurement_methods_changed")

    def test_changed_measurement_helper_outside_original_fourteen_is_rejected(self):
        self.change_measurement_source("ReadDocumentMetadata")
        self.rejected("actual_old_and_target_measurement_methods_changed")

    def test_original_numeric_comparison_is_retained(self):
        self.fixture.mutate_target_evidence(1, lambda data: data["instances"][0]["bodies"][0]["boxMm"].__setitem__(0, -2.5))
        report = self.rejected()
        self.assertTrue(report["resumeCaseProvenance"]["verified"])
        self.assertEqual(report["validationStatus"], "measurement_difference")
        self.assertFalse(report["sectionsEqual"]["instances"])

    def test_original_metadata_comparison_is_retained(self):
        self.fixture.mutate_target_evidence(1, lambda data: data["documentMetadata"][0].__setitem__("materialName", "SYNTHETIC DIFFERENT"))
        report = self.rejected()
        self.assertFalse(report["documentMetadataComparison"]["equal"])

    def test_original_cold_reopen_validation_is_retained(self):
        self.fixture.mutate_target_evidence(1, lambda data: data["reopen"].__setitem__("coldSession", False))
        report = self.rejected("complete_target_cold_readback_or_runtime_failed")
        self.assertIn("relocation_successful_cold_reopen_required", report["candidate"]["errors"])

    def test_all_three_target_cases_require_actual_complete_readback(self):
        self.fixture.mutate_target_evidence(3, lambda data: data["reopen"].__setitem__("coldSession", False))
        self.rejected("complete_target_cold_readback_or_runtime_failed:case3")

    def test_original_errors_and_module_globals_are_never_replaced(self):
        checker = RESUMED.load_checker()
        original = checker.runtime_lineage
        left = checker.Snapshot(CHECKER.fixture(), verify_files=False)
        left.errors.append("synthetic_existing_error_must_survive")
        view = RESUMED.SnapshotView(left, {"errors": ["synthetic_existing_lineage_error"]})
        self.assertIs(view.errors, left.errors)
        self.assertIn("synthetic_existing_error_must_survive", view.summary()["errors"])
        self.assertIs(checker.runtime_lineage, original)

    def test_frozen_checker_hash_is_required(self):
        path = Path(self.temporary.name) / "tampered_checker.py"
        path.write_text("raise RuntimeError('must never execute')", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "frozen_checker_sha256_mismatch"):
            RESUMED.load_checker(path)

    def test_evidence_reads_detect_mutation_after_validation(self):
        path = Path(self.temporary.name) / "synthetic.txt"
        path.write_bytes(b"before")
        reads = RESUMED.EvidenceReads()
        reads.raw(path)
        path.write_bytes(b"after")
        with self.assertRaisesRegex(ValueError, "evidence_changed_while_reading"):
            reads.unchanged()


if __name__ == "__main__":
    unittest.main()
