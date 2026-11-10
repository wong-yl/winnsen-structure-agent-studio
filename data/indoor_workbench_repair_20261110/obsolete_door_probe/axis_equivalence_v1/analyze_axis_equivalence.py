# Throwaway offline analysis. Does not change Equal, CAD files, or acceptance gates.
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import math
import ntpath
import os

ROOT = Path(r"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\axis_equivalence_v1")
PROBE_ROOT = ROOT.parent
INPUTS = {
    "edit": (PROBE_ROOT / "runs" / "20261110084549485-v6-edit-31788" / "probe.json", "87D9334E7BC9992962021F810FF230B401DC7F1DFFB2BC8FF07E87AD6BE88F5F"),
    "diagnostic33": (PROBE_ROOT / "runs" / "20261110091308144-v6-diagnostic33-18608" / "probe.json", "4F79819F58A4FC5B8CBE0833E887201DE7D4FEE5F98B448884624C578978BD20"),
}
OWNER = str(PROBE_ROOT / "candidate_pack_v6" / "箱体竖隔板L焊接.SLDASM")
COMPONENT = "箱体竖隔板L-1"
COMPONENT_PATH = str(PROBE_ROOT / "candidate_pack_v6" / "箱体竖隔板L.sldprt")
FEATURES = ("同心1", "同心2", "同心3", "同心4")
ABSOLUTE = 1e-12
RELATIVE = 1e-10
GEOMETRY_SCOPE = "existing aggregate indicators only; finite cylinder boundaries not directly compared"


def sha_bytes(value):
    return hashlib.sha256(value).hexdigest().upper()


def canonical_bytes(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def event(log, inspection, stage=None):
    rows = [row for row in log["events"] if row.get("inspection") == inspection and (stage is None or row.get("stage") == stage)]
    if len(rows) != 1:
        raise ValueError("EVIDENCE_EVENT_NOT_UNIQUE " + inspection + "/" + str(stage))
    return rows[0]


def finite_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(float(value))


def numeric_equal(before, after):
    try:
        x, y = float(before), float(after)
    except (ValueError, TypeError, OverflowError):
        return False
    return math.isfinite(x) and math.isfinite(y) and abs(x - y) <= ABSOLUTE + max(abs(x), abs(y)) * RELATIVE


def invariant_scalar(value):
    if isinstance(value, bool):
        return "True" if value else "False"
    return str(value)


def raw_differences(before, after, path="state", result=None, stats=None):
    # Mirrors the existing Equal traversal, dictionary/array contracts and tolerance.
    # It accumulates every mismatch instead of throwing at the first mismatch.
    if result is None:
        result = []
    if stats is None:
        stats = {"leafCount": 0, "toleratedNumericChangeCount": 0}
    if before is None or after is None:
        stats["leafCount"] += 1
        if before is not None or after is not None:
            result.append({"path": path, "kind": "STATE_DRIFT", "old": before, "new": after})
        return result, stats
    if isinstance(before, dict) or isinstance(after, dict):
        if not isinstance(before, dict) or not isinstance(after, dict):
            result.append({"path": path, "kind": "OBJECT_FIELDS_DRIFT", "old": before, "new": after})
            return result, stats
        if set(before) != set(after):
            result.append({"path": path, "kind": "OBJECT_FIELDS_DRIFT", "oldKeys": list(before), "newKeys": list(after)})
        for key in before:
            if key in after:
                raw_differences(before[key], after[key], path + "/" + key, result, stats)
        return result, stats
    if isinstance(before, list) or isinstance(after, list):
        if not isinstance(before, list) or not isinstance(after, list):
            result.append({"path": path, "kind": "ARRAY_COUNT_DRIFT", "old": before, "new": after})
            return result, stats
        if len(before) != len(after):
            result.append({"path": path, "kind": "ARRAY_COUNT_DRIFT", "oldLength": len(before), "newLength": len(after)})
        for index, (old, new) in enumerate(zip(before, after)):
            raw_differences(old, new, path + "[" + str(index) + "]", result, stats)
        return result, stats
    stats["leafCount"] += 1
    floating = isinstance(before, float) or isinstance(after, float)
    equal = numeric_equal(before, after) if floating else invariant_scalar(before) == invariant_scalar(after)
    if not equal:
        row = {"path": path, "kind": "NUMERIC_STATE_DRIFT" if floating else "STATE_DRIFT", "old": before, "new": after}
        if finite_number(before) and finite_number(after):
            row["delta"] = float(after) - float(before)
            row["tolerance"] = ABSOLUTE + max(abs(float(before)), abs(float(after))) * RELATIVE
        result.append(row)
    elif finite_number(before) and finite_number(after) and before != after:
        stats["toleratedNumericChangeCount"] += 1
    return result, stats


def raw_equal(before, after):
    return not raw_differences(before, after)[0]


def locate(state):
    docs = [(index, doc) for index, doc in enumerate(state["documents"]) if doc.get("path") == OWNER]
    if len(docs) != 1:
        raise ValueError("EXACT_OWNER_REQUIRED")
    di, doc = docs[0]
    found = []
    for name in FEATURES:
        rows = [(index, feature) for index, feature in enumerate(doc["features"]) if feature.get("name") == name]
        if len(rows) != 1:
            raise ValueError("EXACT_FEATURE_REQUIRED " + name)
        fi, feature = rows[0]
        found.append((di, fi, doc, feature))
    return found


def identity(doc, feature, entity):
    return {
        "document": {key: value for key, value in doc.items() if key not in ("features", "externalReferences")},
        "feature": {key: value for key, value in feature.items() if key not in ("mate", "externalReferences")},
        "mate": {key: value for key, value in feature["mate"].items() if key != "entities"},
        "entity": {key: value for key, value in entity.items() if key != "parameters"},
    }


def analyze_row(old, new):
    odi, ofi, odoc, ofeature = old
    ndi, nfi, ndoc, nfeature = new
    checks = {}
    checks["sameDocumentAndFeatureIndex"] = (odi, ofi) == (ndi, nfi)
    checks["boundOwnerFeatureAndMateType"] = all(
        feature.get("type") == "MateConcentric" and feature["mate"].get("type") == 1 and feature["mate"].get("ownerDocumentPath") == OWNER
        for feature in (ofeature, nfeature)
    ) and ofeature.get("name") == nfeature.get("name") in FEATURES
    oe = ofeature["mate"]["entities"][0]
    ne = nfeature["mate"]["entities"][0]
    checks["boundCylinderEntity0AndComponent"] = all(
        entity.get("index") == 0 and entity.get("componentName") == COMPONENT and entity.get("componentPath") == COMPONENT_PATH and entity.get("referenceType") == 4 and entity.get("ownerDocumentReference") is False
        for entity in (oe, ne)
    )
    oi, ni = identity(odoc, ofeature, oe), identity(ndoc, nfeature, ne)
    checks["allIdentityBindingsExactlyEqual"] = oi == ni
    checks["allIdentityBindingsPassOriginalEqual"] = raw_equal(oi, ni)
    checks["nonemptyFeatureAndEntityPersistentReferences"] = all(isinstance(value, str) and len(value) > 0 for value in (ofeature.get("persistentReference"), nfeature.get("persistentReference"), oe.get("persistentReference"), ne.get("persistentReference")))
    op, np = oe.get("parameters"), ne.get("parameters")
    checks["eightFiniteParameters"] = all(isinstance(params, list) and len(params) == 8 and all(finite_number(value) for value in params) for params in (op, np))
    row = {"owner": OWNER, "feature": ofeature.get("name"), "featureType": ofeature.get("type"), "entityIndex": 0,
           "parameterPath": "state/documents[" + str(odi) + "]/features[" + str(ofi) + "]/mate/entities[0]/parameters",
           "oldParameters": op, "newParameters": np, "identityBindings": oi,
           "oldIdentitySha256": sha_bytes(canonical_bytes(oi)), "newIdentitySha256": sha_bytes(canonical_bytes(ni)), "checks": checks}
    if checks["eightFiniteParameters"]:
        old_axis, new_axis = [float(v) for v in op[3:6]], [float(v) for v in np[3:6]]
        old_norm, new_norm = math.sqrt(sum(v * v for v in old_axis)), math.sqrt(sum(v * v for v in new_axis))
        checks["axisDirectionsRawValuesExactlyEqual"] = op[3:6] == np[3:6]
        checks["nonzeroUnitAxes"] = old_norm > 0 and new_norm > 0 and numeric_equal(old_norm, 1.0) and numeric_equal(new_norm, 1.0)
        checks["positiveRadiusAndOriginalEqual"] = op[6] > 0 and np[6] > 0 and raw_equal(op[6], np[6])
        checks["remainingParametersPassOriginalEqual"] = raw_equal(op[7:], np[7:])
        row.update({"oldAxisDirection": op[3:6], "newAxisDirection": np[3:6], "oldAxisNorm": old_norm, "newAxisNorm": new_norm,
                    "oldRadius": op[6], "newRadius": np[6], "oldRemainingParameters": op[7:], "newRemainingParameters": np[7:]})
        if old_norm > 0 and new_norm > 0:
            unit_old = [v / old_norm for v in old_axis]
            unit_new = [v / new_norm for v in new_axis]
            delta = [float(n) - float(o) for o, n in zip(op[:3], np[:3])]
            axial = sum(d * u for d, u in zip(delta, unit_old))
            projection = [axial * u for u in unit_old]
            residual = [d - p for d, p in zip(delta, projection)]
            checks["deltaMinusAxialProjectionPassesOriginalToleranceAgainstZero"] = raw_equal(residual, [0.0, 0.0, 0.0])
            row.update({"oldUnitAxis": unit_old, "newUnitAxis": unit_new, "pointDeltaNewMinusOld": delta,
                        "signedAxialDifference": axial, "axialProjection": projection, "deltaMinusAxialProjectionResidual": residual,
                        "residualNorm": math.sqrt(sum(v * v for v in residual))})
        else:
            checks["deltaMinusAxialProjectionPassesOriginalToleranceAgainstZero"] = False
    row["cylinderAxisRepresentationEquivalent"] = all(checks.values())
    return row


def analyze(old_state, new_state):
    differences, stats = raw_differences(old_state, new_state)
    rows, binding_error = [], None
    try:
        rows = [analyze_row(old, new) for old, new in zip(locate(old_state), locate(new_state))]
    except (KeyError, IndexError, TypeError, ValueError, OverflowError) as error:
        binding_error = type(error).__name__ + ": " + str(error)
    expected_paths = {row["parameterPath"] + "[0]" for row in rows}
    unapproved = [difference for difference in differences if difference["path"] not in expected_paths]
    exact_four = len(rows) == 4 and len(differences) == 4 and {difference["path"] for difference in differences} == expected_paths
    equivalent = binding_error is None and exact_four and not unapproved and all(row["cylinderAxisRepresentationEquivalent"] for row in rows)
    return {"rawEqual": not differences, "cylinderAxisRepresentationEquivalent": equivalent,
            "rawDifferenceCount": len(differences), "rawDifferences": differences, "comparisonStats": stats,
            "exactFourBoundParameter0Differences": exact_four, "allOtherFieldsPassOriginalRules": not unapproved,
            "unapprovedDifferences": unapproved, "bindingError": binding_error, "cylinders": rows}


def negative_controls(old_state, new_state):
    _, _, _, target = locate(new_state)[0]
    finite_index = next(index for index, component in enumerate(new_state["components"]) if component.get("solidBodies", {}).get("bodies"))
    controls = []
    def run(name, mutation):
        changed = deepcopy(new_state)
        mutation(changed)
        analysis = analyze(old_state, changed)
        failed_checks = [row["feature"] + "/" + key for row in analysis["cylinders"] for key, value in row["checks"].items() if not value]
        rejected = analysis["rawEqual"] is False and analysis["cylinderAxisRepresentationEquivalent"] is False
        controls.append({"name": name, "expected": "reject", "rejected": rejected, "pass": rejected,
                         "rawDifferenceCount": analysis["rawDifferenceCount"], "unapprovedDifferencePaths": [row["path"] for row in analysis["unapprovedDifferences"]],
                         "failedCylinderChecks": failed_checks, "bindingError": analysis["bindingError"]})
    def entity(state):
        return locate(state)[0][3]["mate"]["entities"][0]
    def feature(state):
        return locate(state)[0][3]
    run("perpendicular_translation_0.01", lambda s: entity(s)["parameters"].__setitem__(1, entity(s)["parameters"][1] + 0.01))
    run("radius_change_0.000001", lambda s: entity(s)["parameters"].__setitem__(6, entity(s)["parameters"][6] + 0.000001))
    run("direction_reversed", lambda s: entity(s)["parameters"].__setitem__(slice(3, 6), [-v for v in entity(s)["parameters"][3:6]]))
    run("direction_tilted_0.00001", lambda s: entity(s)["parameters"].__setitem__(4, 0.00001))
    run("zero_axis", lambda s: entity(s)["parameters"].__setitem__(slice(3, 6), [0, 0, 0]))
    run("non_unit_axis", lambda s: entity(s)["parameters"].__setitem__(3, -2))
    run("non_cylinder_reference_type", lambda s: entity(s).__setitem__("referenceType", 1))
    run("non_concentric_feature", lambda s: feature(s).__setitem__("type", "MateCoincident"))
    run("component_identity_changed", lambda s: entity(s).__setitem__("componentName", COMPONENT + "-changed"))
    run("entity_persistent_reference_changed", lambda s: entity(s).__setitem__("persistentReference", entity(s)["persistentReference"] + "A"))
    run("feature_persistent_reference_changed", lambda s: feature(s).__setitem__("persistentReference", feature(s)["persistentReference"] + "A"))
    run("additional_existing_field_outside_tolerance", lambda s: feature(s).__setitem__("id", feature(s)["id"] + 1))
    run("additional_field_added", lambda s: feature(s).__setitem__("unexpectedField", 1))
    run("remaining_parameter_changed", lambda s: entity(s)["parameters"].__setitem__(7, 0.000001))
    run("finite_body_transform_changed_0.0001", lambda s: s["components"][finite_index]["transform"].__setitem__(9, s["components"][finite_index]["transform"][9] + 0.0001))
    return controls


def main():
    if Path(__file__).resolve().parent != ROOT.resolve():
        raise ValueError("ANALYSIS_DIRECTORY_NOT_AUTHORIZED")
    inputs, logs = {}, {}
    for label, (path, expected) in INPUTS.items():
        raw = path.read_bytes()
        actual = sha_bytes(raw)
        if actual != expected:
            raise ValueError("FROZEN_INPUT_SHA256_MISMATCH " + label)
        inputs[label] = {"path": str(path), "sha256": actual, "size": len(raw)}
        logs[label] = json.loads(raw.decode("utf-8-sig"))
    old_state = event(logs["edit"], "snapshot", "after-save")["state"]
    new_state = event(logs["diagnostic33"], "snapshot", "diagnostic33-reopen")["state"]
    before_state_hashes = [sha_bytes(canonical_bytes(state)) for state in (old_state, new_state)]
    analysis = analyze(old_state, new_state)
    controls = negative_controls(old_state, new_state)
    raw_open = event(logs["diagnostic33"], "raw_open")
    inputs_still_frozen = all(sha_bytes(path.read_bytes()) == expected for path, expected in INPUTS.values())
    memory_states_unchanged = before_state_hashes == [sha_bytes(canonical_bytes(state)) for state in (old_state, new_state)]
    report = {
        "schema": "obsolete-door-cylinder-axis-representation-analysis-v1", "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "purpose": "throwaway offline analysis only; does not alter raw data, Equal, or acceptance gates",
        "script": str(Path(__file__).resolve()), "scriptSha256": sha_bytes(Path(__file__).read_bytes()), "inputs": inputs,
        "oldSnapshotStage": "after-save", "newSnapshotStage": "diagnostic33-reopen",
        "originalNumericTolerance": {"absolute": ABSOLUTE, "relative": RELATIVE, "formula": "abs(old-new) <= 1e-12 + max(abs(old),abs(new))*1e-10"},
        **analysis, "negativeControls": controls, "negativeControlCount": len(controls),
        "negativeControlsPassed": sum(row["pass"] for row in controls), "inputsUnchanged": inputs_still_frozen,
        "rawMemoryStatesUnchanged": memory_states_unchanged, "diagnosticRawOpen": raw_open,
        "diagnosticReadFailure": event(logs["diagnostic33"], "read_failure"),
        "warning32Unresolved": True, "modelAccepted": False, "strictColdAcceptancePassed": False,
        "geometryEvidenceScope": GEOMETRY_SCOPE, "nativeCadExecuted": False, "originalEqualModified": False,
    }
    report["offlineChecksPassed"] = (analysis["rawEqual"] is False and analysis["cylinderAxisRepresentationEquivalent"] is True and all(row["pass"] for row in controls)
                                     and raw_open["options"] == 33 and raw_open["errors"] == 0 and raw_open["warnings"] == 32
                                     and inputs_still_frozen and memory_states_unchanged)
    name = "report-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + "-" + str(os.getpid()) + ".json"
    output = ROOT / name
    with output.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    print(json.dumps({"report": str(output), "reportSha256": sha_bytes(output.read_bytes()), "scriptSha256": report["scriptSha256"],
                      "rawEqual": report["rawEqual"], "cylinderAxisRepresentationEquivalent": report["cylinderAxisRepresentationEquivalent"],
                      "rawDifferenceCount": report["rawDifferenceCount"], "negativeControlsPassed": report["negativeControlsPassed"], "negativeControlCount": report["negativeControlCount"],
                      "warning32Unresolved": report["warning32Unresolved"], "modelAccepted": report["modelAccepted"], "geometryEvidenceScope": GEOMETRY_SCOPE,
                      "offlineChecksPassed": report["offlineChecksPassed"], "nativeCadExecuted": False}, ensure_ascii=True, indent=2))
    return 0 if report["offlineChecksPassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
