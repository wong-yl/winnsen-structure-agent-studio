"""Offline contracts for the limited six-door evidence closeout; no CAD calls."""
import copy
import json
from pathlib import Path
import unittest

import door_closeout as closeout


class DoorCloseoutTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.generation = closeout.DATA / "runs/2026-11-13T08-27-26-311Z-T2-outputs-dxf-9f7e1153"
        cls.manifest = json.loads((cls.generation / "cad/outputs/output-manifest.json").read_text("utf-8-sig"))
        native = json.loads((cls.generation / "native-result.json").read_text("utf-8-sig"))
        cls.panels = [event for event in native["events"] if event["inspection"] == "output_case_model"]

    def test_real_six_case_manifest(self):
        self.assertEqual(set(closeout.manifest_cases(self.manifest)), closeout.EXPECTED)

    def test_manifest_rejects_duplicates_missing_wrong_identity_and_scope(self):
        for mutation in ["duplicate", "missing", "identity", "folder", "escape", "scope", "drawing", "flat", "dxf"]:
            with self.subTest(mutation=mutation):
                value = copy.deepcopy(self.manifest)
                if mutation == "duplicate": value["cases"][-1] = value["cases"][0]
                elif mutation == "missing": value["cases"].pop()
                elif mutation == "identity": value["cases"][0]["hand"] = "UNKNOWN"
                elif mutation == "folder": value["cases"][1]["folder"] = value["cases"][0]["folder"]
                elif mutation == "escape": value["cases"][0]["part"] = "../wrong.SLDPRT"
                elif mutation == "scope": value["production_release"] = True
                elif mutation == "drawing": value["cases"][0]["drawing"] = {}
                elif mutation == "flat": value["cases"][0]["flatPreparation"]["success"] = False
                elif mutation == "dxf": value["cases"][0]["dxf"]["success"] = False
                with self.assertRaises(ValueError): closeout.manifest_cases(value)

    def test_event_paths_must_cover_six_unique_models(self):
        paths = {event["path"] for event in self.panels}
        self.assertEqual(set(closeout.event_index(self.panels, "output_case_model", paths)), paths)
        duplicate = copy.deepcopy(self.panels)
        duplicate[-1]["path"] = duplicate[0]["path"]
        with self.assertRaises(ValueError): closeout.event_index(duplicate, "output_case_model", paths)
        unrelated = copy.deepcopy(self.panels)
        unrelated[0]["path"] = str(self.generation / "unrelated.SLDPRT")
        with self.assertRaises(ValueError): closeout.event_index(unrelated, "output_case_model", paths)

    def test_folded_holes_preserve_hand_and_position(self):
        for event in self.panels:
            item = next(row for row in self.manifest["cases"] if closeout.identity(row) == closeout.identity(event))
            closeout.folded_panel(event, item)
            changed = copy.deepcopy(event)
            changed["geometry"]["bodies"][0]["circleEdges"] = []
            with self.assertRaises(ValueError): closeout.folded_panel(changed, item)

    def test_traversal_rejected_before_write(self):
        with self.assertRaises(ValueError): closeout.safe(closeout.DATA / ".." / "outside")

    def test_flat_reopen_requires_restoration_and_no_save(self):
        item = self.manifest["cases"][0]
        path = str(self.generation / "cad/outputs" / item["folder"] / item["part"])
        result = {"pass": True, "modelPath": path, "originalConfigurationRestored": True,
                  "featureSuppressionUnchanged": True, "partDiskUnchanged": True, "partSaveCalled": False,
                  "manufacturingAccepted": False, "flatConfiguration": item["flatPreparation"]["flatConfiguration"],
                  "configurationNames": item["flatPreparation"]["configurationNames"], "geometry": item["flatPreparation"]["geometry"]}
        closeout.flat_pass(result, item["flatPreparation"], path)
        for key in ["pass", "originalConfigurationRestored", "featureSuppressionUnchanged", "partDiskUnchanged", "partSaveCalled", "manufacturingAccepted"]:
            with self.subTest(key=key):
                changed = copy.deepcopy(result); changed[key] = not changed[key]
                with self.assertRaises(ValueError): closeout.flat_pass(changed, item["flatPreparation"], path)

    def test_flat_reopen_rejects_short_prior_and_changed_cut(self):
        item = self.manifest["cases"][0]
        path = str(self.generation / "cad/outputs" / item["folder"] / item["part"])
        result = {"pass": True, "modelPath": path, "originalConfigurationRestored": True,
                  "featureSuppressionUnchanged": True, "partDiskUnchanged": True, "partSaveCalled": False,
                  "manufacturingAccepted": False, "flatConfiguration": item["flatPreparation"]["flatConfiguration"],
                  "configurationNames": item["flatPreparation"]["configurationNames"], "geometry": copy.deepcopy(item["flatPreparation"]["geometry"])}
        for size in [[], [473.4], [473.4, 507.65]]:
            prior = copy.deepcopy(item["flatPreparation"]); prior["geometry"]["widthHeightThicknessMm"] = size
            with self.assertRaises(ValueError): closeout.flat_pass(result, prior, path)
        changed = copy.deepcopy(result)
        changed["geometry"]["cutGeometry"]["primitives"][0]["startMm"][0] += .1
        with self.assertRaises(ValueError): closeout.flat_pass(changed, item["flatPreparation"], path)

    def test_cad_start_timestamp_retains_dotnet_precision(self):
        claim = {"startedAtUtc": "2026-11-13T09:29:11.2887427Z", "startTicks": 639301589512887427}
        closeout.claim_time_pass(claim)
        for value in [639301589512887420, True, -1]:
            changed = dict(claim, startTicks=value)
            with self.assertRaises(ValueError): closeout.claim_time_pass(changed)


if __name__ == "__main__":
    unittest.main()
