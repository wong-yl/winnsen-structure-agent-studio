"""Frozen, read-only closeout analysis; never modifies CAD or earlier evidence."""
import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = ROOT.parents[2]
T3 = ROOT / "runs/2026-11-13T04-20-56-558Z-resume-T3-338f953c"
T4 = ROOT / "runs/2026-11-13T05-08-41-897Z-recovery-T4-f58333e9"
READS = {}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def read(path):
    path = Path(path).resolve(strict=True)
    READS[str(path)] = digest(path)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def property_rows(metadata):
    return {
        (scope["configuration"], prop["name"]): prop
        for scope in metadata["customPropertyScopes"]
        for prop in scope["properties"]
    }


def file_identity(data, metadata):
    instance = next(x for x in data["instances"] if x["identity"] == metadata["identity"])
    path = Path(metadata["path"]).resolve(strict=True)
    actual = digest(path)
    expected = instance["sha256"].upper()
    if actual != expected:
        raise ValueError(f"Instance evidence SHA mismatch: {path}")
    READS[str(path)] = actual
    return {"path": str(path), "sha256": actual,
            "evidenceInstanceIdentity": instance["identity"],
            "referencedConfiguration": metadata["referencedConfiguration"]}


def metadata_differences(left, right):
    lm = {x["identity"]: x for x in left["documentMetadata"]}
    rm = {x["identity"]: x for x in right["documentMetadata"]}
    if lm.keys() != rm.keys():
        raise ValueError("Metadata instance identity sets differ")
    differences = []
    for identity in sorted(lm):
        lp, rp = property_rows(lm[identity]), property_rows(rm[identity])
        if lp.keys() != rp.keys():
            raise ValueError(f"Property scope/name sets differ: {identity}")
        for key in sorted(lp):
            changed = [k for k in ("value", "resolved", "linked") if lp[key][k] != rp[key][k]]
            if changed:
                differences.append({
                    "identity": identity, "propertyConfiguration": key[0], "propertyName": key[1],
                    "changedFields": changed, "baselineProperty": lp[key], "candidateProperty": rp[key],
                    "baselineFile": file_identity(left, lm[identity]),
                    "candidateFile": file_identity(right, rm[identity]),
                    "crossRunFileBytesEqual": digest(lm[identity]["path"]) == digest(rm[identity]["path"]),
                })
    return differences


def internal_inconsistencies(data):
    groups = {}
    instances = {x["identity"]: x for x in data["instances"]}
    for row in data["documentMetadata"]:
        instance = instances[row["identity"]]
        for key, prop in property_rows(row).items():
            group = (str(Path(row["path"]).resolve()).casefold(), instance["sha256"].upper(),
                     row["referencedConfiguration"], key[0], key[1])
            groups.setdefault(group, []).append((row, prop))
    found = []
    for key, rows in groups.items():
        if len({(r[1]["value"], r[1]["resolved"], r[1]["linked"]) for r in rows}) > 1:
            found.append({
                "file": file_identity(data, rows[0][0]),
                "propertyConfiguration": key[3], "propertyName": key[4],
                "reads": [{"identity": row["identity"], "property": prop} for row, prop in rows],
            })
    return found


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--comparisons", type=Path, required=True)
    args = parser.parse_args()
    out = Path(__file__).with_name("closeout.json")
    if out.exists():
        raise FileExistsError("Do not overwrite an earlier closeout")
    native = read(T4 / "native-result.json")
    finished = [(e, native["eventTimes"][i]) for i, e in enumerate(native["events"])
                if e.get("inspection") == "finished"]
    if len(finished) != 1:
        raise ValueError("Expected one native finished event")
    if not (finished[0][0].get("success") is True and finished[0][0].get("cadExited") is True
            and finished[0][0].get("error") is None):
        raise ValueError("Native execution did not finish successfully")
    cases = []
    for index in (1, 2, 3):
        left = read(T3 / f"cad/assemblies/case{index}/assembly-evidence.json")
        right = read(T4 / f"cad/assemblies/case{index}/relocation-evidence.json")
        comparison_path = args.comparisons / f"case{index}.json"
        comparison = read(comparison_path)
        cases.append({
            "case": index, "comparisonPath": str(comparison_path.resolve()),
            "pass": comparison["pass"], "validationStatus": comparison["validationStatus"],
            "measurementComparisonEqual": comparison["measurementComparisonEqual"],
            "sectionsEqual": comparison["sectionsEqual"],
            "numericComparison": comparison["numericComparison"],
            "resumeCaseProvenance": comparison.get("resumeCaseProvenance"),
            "documentMetadataEqual": comparison["documentMetadataComparison"]["equal"],
            "propertyDifferences": metadata_differences(left, right),
            "baselineInternalPropertyInconsistencies": internal_inconsistencies(left),
            "candidateInternalPropertyInconsistencies": internal_inconsistencies(right),
            "candidateReadback": {k: right[k] for k in (
                "success", "nativeReadbackPass", "componentIdentityReadbackPass",
                "documentMetadataReadbackPass", "inputFilesUnchanged", "reopen")},
            "candidateMeasurementPerformance": right["measurementPerformance"],
            "structuralAcceptancePass": right["acceptancePass"],
        })
    changes = [x for case in cases for x in case["propertyDifferences"]]
    if len(changes) != 4 or any(x["changedFields"] != ["resolved"] for x in changes):
        raise ValueError("Unexpected metadata change set; investigate before publishing")
    result = {
        "schema": "16029-variable-trial-closeout/v1",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "scope": "parts_dxf_only", "drawingStatus": "deferred_by_user_20261113",
        "production_release": False, "fullPlanPass": False,
        "t4NativeExecutionCompleted": True,
        "t4MigrationConsistencyPass": all(x["pass"] for x in cases),
        "t4NativeFinish": {"event": finished[0][0], "time": finished[0][1]},
        "result": read(T4 / "result.json"), "supervision": read(T4 / "supervision.json"),
        "protection": read(T4 / "protection-result.json"),
        "recoverySourceCheck": {k: v for k, v in read(T4 / "recovery-source-check.json").items()
                                if not isinstance(v, (dict, list))},
        "nativeToolchain": read(T4 / "toolchain.json"),
        "recoveryPointer": read(ROOT / "recovery-T4-current.json"),
        "sixCaseDxfAudit": {k: v for k, v in read(ROOT / "analysis-dxf-native-readback-20261113-095112-65e2af10/six-case-audit/output-audit.json").items()
                           if not isinstance(v, (dict, list))},
        "panelReopenCount": sum(x.get("inspection") == "relocated_panel" for x in native["events"]),
        "flatReopenCount": sum(x.get("inspection") == "relocated_flat" for x in native["events"]),
        "cases": cases,
        "unverified": ["cause of four resolved weight differences", "controlled repeated cold property reads",
                       "independent mass measurements", "manufacturing direction", "factory DFM thresholds"],
        "expertReviewRecords": [{"path": str(Path(__file__).with_name(name)),
                                 "sha256": digest(Path(__file__).with_name(name)),
                                 "provenance": "Primary agent saved the independent expert's read-only message"}
                                for name in ("expert-review.md", "wrapper-review.md")],
        "wrapperTestVerification": read(Path(__file__).with_name("wrapper-test-verification.json")),
        "wrapperTestLog": {"path": str(Path(__file__).with_name("wrapper-test-output.txt")),
                           "sha256": digest(Path(__file__).with_name("wrapper-test-output.txt"))},
        "evidenceReads": [{"path": p, "sha256": h} for p, h in sorted(READS.items())],
        "analysisScriptSha256": digest(__file__),
        "currentFrozenToolHashes": {name: digest(WORKSPACE / "workers/master_v2/variable_trial" / name)
                                   for name in ("TrialAssembly.cs", "VariableTrial.cs", "TrialOutput.cs",
                                                "recover.mjs", "common.mjs", "run.mjs")},
        "fixedComparatorSha256": digest(WORKSPACE / "workers/master_v2/variable_trial/compare_assembly_measurements.py"),
    }
    if result["fixedComparatorSha256"] != "0920CDD6E3EEACE21ED36F40D4A0D64EBC2CD73B4DED063AF0FE2F55C1B57B27":
        raise ValueError("Frozen comparator changed")
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"path": str(out), "sha256": digest(out),
                      "t4MigrationConsistencyPass": result["t4MigrationConsistencyPass"],
                      "propertyDifferenceCount": len(changes)}))


if __name__ == "__main__":
    main()
