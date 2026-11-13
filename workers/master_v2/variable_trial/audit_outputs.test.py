"""Offline behavior tests for the output audit's public entry points."""
import contextlib
import copy
import hashlib
import io
import json
import math
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.dont_write_bytecode = True
import audit_outputs as audit


def dxf_arc(start, end, dx=0, dy=0):
    tags = [(0, "SECTION"), (2, "ENTITIES")]
    for a, b in (((-20, -20), (20, -20)), ((20, -20), (20, 20)),
                 ((20, 20), (-20, 20)), ((-20, 20), (-20, -20))):
        tags.extend([(0, "LINE"), (10, a[0]+dx), (20, a[1]+dy),
                     (11, b[0]+dx), (21, b[1]+dy)])
    tags.extend([(0, "ARC"), (10, dx), (20, dy), (40, 5),
                 (50, start), (51, end), (0, "ENDSEC"), (0, "EOF")])
    return "".join("%s\n%s\n" % tag for tag in tags).encode("ascii")


def pdf_bytes(label, frame_scale=1, omit_frame_lines=(), duplicate_frame_line=None, frame_offset_mm=0):
    text = ("16029 TRIAL 2026-11-12 PDF identity fixture with enough visible "
            "text for the structural checks. " + label)
    lines = (((20, 10), (410, 10)), ((410, 10), (410, 287)),
             ((410, 287), (20, 287)), ((20, 287), (20, 10)),
             ((20, 258), (410, 258)), ((20, 55), (410, 55)),
             ((20, 25), (410, 25)), ((205, 25), (205, 55)))
    commands = []
    for index, endpoints in enumerate(lines):
        if index in omit_frame_lines:
            continue
        command = "%.6f %.6f m %.6f %.6f l S\n" % tuple(
            (v * frame_scale + frame_offset_mm) / 25.4 * 72 for point in endpoints for v in point)
        commands.extend([command] * (2 if index == duplicate_frame_line else 1))
    stream = ("".join(commands) + "BT /F1 12 Tf 40 780 Td (" + text + ") Tj ET\n").encode("ascii")
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        (b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1190.551181 841.889764] "
         b"/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"),
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        b"<< /Length " + str(len(stream)).encode("ascii") + b" >>\nstream\n" + stream + b"endstream",
    ]
    result = bytearray(b"%PDF-1.4\n")
    offsets = [0]
    for index, value in enumerate(objects, 1):
        offsets.append(len(result))
        result.extend(("%d 0 obj\n" % index).encode("ascii") + value + b"\nendobj\n")
    xref = len(result)
    result.extend(("xref\n0 %d\n0000000000 65535 f \n" % len(offsets)).encode("ascii"))
    for offset in offsets[1:]:
        result.extend(("%010d 00000 n \n" % offset).encode("ascii"))
    result.extend(("trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n"
                   % (len(offsets), xref)).encode("ascii"))
    return bytes(result)


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


class OutputAuditTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="winnsen-output-audit-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve() / "data" / "master_v2" / "trial"
        self.run = self.root / "runs" / "offline-fixture"
        self.run.mkdir(parents=True)

    def invoke(self, name):
        target = self.run / name
        with mock.patch.object(audit, "ROOT", self.root), \
                mock.patch.object(sys, "argv", ["audit_outputs.py", str(self.run), "--out", str(target)]), \
                contextlib.redirect_stdout(io.StringIO()):
            audit.main()
        return target

    def model_events(self):
        return [{"inspection": "output_case_model", "path": str(self.run / "cad" / "outputs" / (hand+str(index)) / "door.SLDPRT"),
                 "hand": hand, "widthMm": width, "heightMm": height}
                for hand in ("L", "R") for index, (width, height) in
                enumerate(((437, 471.25), (287, 200), (537, 1000)))]

    def test_historical_shape_distinguishes_complementary_semicircles(self):
        upper = audit.shape(dxf_arc(0, 180))
        lower = audit.shape(dxf_arc(180, 360))
        self.assertNotEqual(upper, lower)
        self.assertEqual(upper, audit.shape(dxf_arc(0, 180, dx=17, dy=-29)))

    def test_failed_native_run_cannot_produce_successful_audit(self):
        events = self.model_events() + [{"inspection": "finished", "success": False, "cadExited": True}]
        write_json(self.run / "native-result.json", {"action": "outputs", "production_release": False, "events": events})
        with self.assertRaisesRegex(audit.dxf.AuditError, "NATIVE_RUN_NOT_SUCCESSFULLY_FINISHED"):
            self.invoke("failed-audit")
        self.assertFalse((self.run / "failed-audit" / "output-audit.json").exists())

    def test_fewer_than_six_native_cases_cannot_produce_successful_audit(self):
        for count in (0, 5):
            with self.subTest(count=count):
                events = self.model_events()[:count] + [{"inspection": "finished", "success": True, "cadExited": True}]
                write_json(self.run / "native-result.json", {"action": "outputs", "production_release": False, "events": events})
                name = "incomplete-audit-%d" % count
                with self.assertRaisesRegex(audit.dxf.AuditError, "EXACT_SIX_DOOR_CASES_REQUIRED"):
                    self.invoke(name)
                self.assertFalse((self.run / name / "output-audit.json").exists())

    @contextlib.contextmanager
    def fixture(self, original):
        events, models, manifest_cases = [], self.model_events(), []
        for model in models:
            part = Path(model["path"])
            part.parent.mkdir(parents=True, exist_ok=True)
            part.write_bytes(b"synthetic native part fixture; not a CAD model")
            (part.parent / "flat.dxf").write_bytes(dxf_arc(0, 180))
            pdf = part.parent / "drawing.pdf"
            pdf.write_bytes(original)
            flat = {"geometry": {"flatGeometryVerified": True, "widthHeightThicknessMm": [473.4, 507.65, 0.8]}}
            exported = {"path": str(part.parent / "flat.dxf"), "sha256": audit.digest(part.parent / "flat.dxf")}
            drawing = {"pdfPath": str(pdf), "pdfSha256": hashlib.sha256(original).hexdigest().upper(),
                       "holes": {"roundHoles": []}}
            events.extend([model,
                {"inspection": "output_flat_preparation", "path": str(part),
                 "result": flat},
                {"inspection": "output_dxf", "path": str(part), "result": exported},
                {"inspection": "output_drawing", "path": str(part),
                 "result": drawing}])
            manifest_cases.append({key:model[key] for key in ("hand", "widthMm", "heightMm")})
            manifest_cases[-1].update(folder=part.parent.name, part=part.name, partSha256=audit.digest(part),
                                      drawing=drawing, dxf=exported, flatPreparation=flat)
        events.append({"inspection": "finished", "success": True, "cadExited": True})
        write_json(self.run / "native-result.json", {"action": "outputs", "production_release": False, "events": events})
        write_json(self.run / "job.json", {"action": "outputs", "production_release": False})
        write_json(self.run / "cad/outputs/output-manifest.json",
                   {"schema": "16029-variable-output-manifest/v1", "complete": True, "cases": manifest_cases})
        history = self.root.parents[1] / "review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108"
        for side in ("left", "right"):
            source = history / (side + ".dxf")
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(dxf_arc(0, 180))
            write_json(history / ("door_dxf_certificate20_door_panel_"+side+"_W437_H471p25.json"),
                       {"dxf": str(source), "dxfSha256": audit.digest(source)})
        dxf_report = {"sha256": audit.digest(Path(models[0]["path"]).parent / "flat.dxf"), "auditPassed": True,
                      "measurements": {"cutExtents": {"width": 473.4, "height": 507.65}},
                      "roundHoles": [], "roundHoleCount": 0, "bends": {"segmentCount": 12}, "errors": []}
        with mock.patch.object(audit.dxf, "audit", side_effect=lambda *_args, **_kwargs: copy.deepcopy(dxf_report)), \
                mock.patch.object(audit.dxf, "find_native_bends", return_value={}), \
                mock.patch.object(audit.dxf, "native_bend_comparison", return_value={"positionsAndDirectionsVerified": True}), \
                mock.patch.object(audit.dxf, "cut_coordinate_mapping", return_value={"verified": True, "mapping": {}}):
            yield models

    def test_pdf_frame_matches_actual_page_coordinates_and_keeps_visual_pending(self):
        with self.fixture(pdf_bytes("valid-frame")):
            target = self.invoke("valid-frame-audit")
        result = audit.load(target / "output-audit.json")
        self.assertTrue(all(case["pdfStructuralPass"] and case["pdfFramePass"] for case in result["cases"]))
        pdf = audit.load(target / "L0" / "pdf-audit.json")
        frame = pdf["pages"][0]["trialFrame"]
        self.assertTrue(frame["pass"])
        self.assertTrue(frame["uniquePdfLines"])
        self.assertEqual(frame["uniquelyMatchedLineCount"], 8)
        self.assertEqual(frame["source"], "pdfplumber.page.lines")
        self.assertEqual(frame["coordinateSystem"], "paper millimetres from PDF bottom-left")
        self.assertEqual(frame["coordinateReadToleranceMm"], 0.2)
        self.assertFalse(frame["manufacturingTolerance"])
        self.assertFalse(frame["nativeSketchEndpointsUsed"])
        self.assertEqual(len({row["matches"][0]["pdfLineIndex"] for row in frame["lines"]}), 8)
        for row in frame["lines"]:
            self.assertEqual(row["matchingLineCount"], 1)
            match = row["matches"][0]
            self.assertLess(match["maximumAbsoluteCoordinateErrorMm"], 0.00001)
            for expected, actual in zip(row["expectedEndpointsMm"], match["actualEndpointsMm"]):
                for a, b in zip(expected, actual):
                    self.assertAlmostEqual(a, b, places=5)
        self.assertFalse(pdf["pass"])
        self.assertEqual(pdf["visualReview"], "pending")
        self.assertFalse(result["production_release"])
        self.assertEqual(result["visualReview"], "pending")

    def test_pdf_frame_rejects_missing_scaled_and_duplicate_lines(self):
        for label, options in (("absent", {"omit_frame_lines": tuple(range(8))}),
                               ("one-missing", {"omit_frame_lines": (7,)}),
                               ("scaled-0p2", {"frame_scale": 0.2}),
                               ("duplicate", {"duplicate_frame_line": 0})):
            with self.subTest(label=label), self.fixture(pdf_bytes(label, **options)):
                target = self.invoke(label + "-audit")
            result = audit.load(target / "output-audit.json")
            self.assertTrue(all(not case["pdfStructuralPass"] and not case["pdfFramePass"] for case in result["cases"]))
            pdf = audit.load(target / "L0" / "pdf-audit.json")
            self.assertTrue(pdf["matchesNativeSha256"])
            self.assertTrue(pdf["pages"][0]["requiredTextPresent"])
            frame = pdf["pages"][0]["trialFrame"]
            self.assertFalse(frame["pass"])
            self.assertTrue(any(row["matchingLineCount"] != 1 for row in frame["lines"]))
            if label == "scaled-0p2":
                self.assertTrue(all(row["nearestPdfLine"] is not None for row in frame["lines"]))
                self.assertTrue(all(row["matchingLineCount"] == 0 for row in frame["lines"]))
            self.assertFalse(pdf["pass"])
            self.assertEqual(pdf["visualReview"], "pending")

    def test_pdf_frame_coordinate_read_tolerance(self):
        for offset, passes in ((0.199, True), (0.201, False)):
            with self.subTest(offset=offset), self.fixture(pdf_bytes("coordinate-tolerance", frame_offset_mm=offset)):
                target = self.invoke("offset-%s-audit" % offset)
            pdf = audit.load(target / "L0" / "pdf-audit.json")
            self.assertEqual(pdf["trialFramePass"], passes)
            self.assertEqual(pdf["structuralPass"], passes)
            self.assertFalse(pdf["pass"])
            self.assertEqual(pdf["visualReview"], "pending")

    def test_replaced_pdf_fails_structural_check_even_with_valid_page_content(self):
        with self.fixture(pdf_bytes("original")) as models:
            control = self.invoke("control-audit")
            first_pdf = Path(models[0]["path"]).parent / "drawing.pdf"
            first_pdf.write_bytes(pdf_bytes("replacement"))
            replaced = self.invoke("replaced-audit")
        control_report = audit.load(control / "output-audit.json")
        self.assertTrue(all(case["pdfStructuralPass"] for case in control_report["cases"]))
        changed = audit.load(replaced / "L0" / "pdf-audit.json")
        self.assertTrue(all(page["structuralPass"] for page in changed["pages"]))
        self.assertFalse(changed["matchesNativeSha256"])
        self.assertFalse(changed["structuralPass"])
        self.assertFalse(changed["pass"])
        self.assertEqual(changed["visualReview"], "pending")
        replaced_report = audit.load(replaced / "output-audit.json")
        self.assertFalse(next(case for case in replaced_report["cases"] if case["case"] == "L0")["pdfStructuralPass"])
        self.assertFalse(replaced_report["production_release"])
        self.assertEqual(replaced_report["visualReview"], "pending")

    def dxf_only_fixture(self, symmetric=False, with_arc=False):
        width, height = 473.4, 507.65
        entities = [("LINE", "CUT", [(10,a[0]), (20,a[1]), (11,b[0]), (21,b[1])])
                    for a,b in (((0,0),(width,0)), ((width,0),(width,height)),
                                ((width,height),(0,height)), ((0,height),(0,0)))]
        entities.append(("CIRCLE", "CUT", [(10,43), (20,59), (40,5.1)]))
        entities.append(("CIRCLE", "CUT", [(10,43 if symmetric else 61), (20,height-59 if symmetric else 113), (40,5.1)]))
        if with_arc:
            entities.extend([("ARC", "CUT", [(10,200), (20,100), (40,7), (50,0), (51,180)]),
                             ("LINE", "CUT", [(10,193), (20,100), (11,207), (21,100)])])
        entities.extend(("LINE", "BEND", [(10,x), (20,10), (11,x), (21,height-10)]) for x in range(20,140,20))
        entities.extend(("LINE", "BEND", [(10,10), (20,y), (11,width-10), (21,y)]) for y in range(30,150,20))
        data = audit.dxf.dxf_fixture(entities)
        parsed = audit.dxf.audit(data, width, height, 12)
        self.assertTrue(parsed["auditPassed"], parsed["errors"])
        events = [{"inspection":"output_scope", "outputScope":audit.DXF_ONLY_SCOPE,
                   "drawingStatus":audit.DRAWING_DEFERRED, "fullPlanPass":False, "production_release":False}]
        cases = []
        for model in self.model_events():
            part = Path(model["path"])
            part.parent.mkdir(parents=True, exist_ok=True)
            part.write_bytes(b"synthetic native part fixture; not a CAD model")
            path = part.parent / "flat.dxf"
            path.write_bytes(data)
            primitives = []
            for row in parsed["cuts"]["primitives"]:
                native = {"type":row["kind"]}
                if row["kind"] in {"LINE", "ARC"}:
                    a,b = audit.dxf.ends(row)
                    native.update(startMm=[*a,0.8], endMm=[*b,0.8])
                if row["kind"] == "LINE":
                    native["lengthMm"] = math.dist(a,b)
                else:
                    native.update(centerMm=[*row["c"],0.8], radiusMm=row["r"], lengthMm=audit.dxf.TAU*row["r"])
                    if row["kind"] == "ARC":
                        angle = row["start"]+row["sweep"]/2
                        native["lengthMm"] = abs(row["sweep"])*row["r"]
                        native["midpointMm"] = [row["c"][0]+row["r"]*math.cos(angle), row["c"][1]+row["r"]*math.sin(angle),0.8]
                        native["midpointEvaluationApi"] = "IEdge.Evaluate2 with GetCurveParams3 domain and status=1"
                native.update(id="C%d" % (len(primitives)+1), curveType=3001 if row["kind"] == "LINE" else 3002,
                              curveAndEdgeSameSense=True, parameterMin=0,
                              parameterMax=native["lengthMm"]/1000 if row["kind"] == "LINE" else audit.dxf.TAU)
                primitives.append(native)
            cut = {"schema":"native-top-z-cut-v1", "complete":True, "errors":[], "units":"mm",
                   "modelPath":str(part), "modelHand":model["hand"], "configuration":"DefaultSM-FLAT-PATTERN",
                   "topZMm":0.8, "topToleranceMm":0.0001, "bodyEdgeCount":len(primitives)*2,
                   "skippedNonTopEdges":len(primitives), "skippedInternalSeams":0,
                   "boundaryEdgeCount":len(primitives), "primitives":primitives, "arcSampling":[]}
            for native in primitives:
                if native["type"] == "ARC":
                    cut["arcSampling"].append({"id":native["id"], "evaluationApi":"IEdge.Evaluate2",
                        "parameterSource":"IEdge.GetCurveParams3", "edgeParameterRange":[0,audit.dxf.TAU],
                        "endpointsVerified":True, "curveParameterDomain":{"source":"ICurve.GetEndParams",
                            "minimum":0, "maximum":audit.dxf.TAU, "closed":False, "periodic":False},
                        "samples":[{"parameter":0, "pointMm":list(native["startMm"]), "status":1,
                                    "packedStatusBits":"00007FF800000001"}]})
            flat = {"geometry":{"flatGeometryVerified":True, "widthHeightThicknessMm":[width,height,0.8], "cutGeometry":cut}}
            exported = {"path":str(path), "sha256":audit.digest(path), "exportConfiguration":cut["configuration"],
                        "nativeCutGeometry":cut, "bendDirection":"UNKNOWN"}
            events.extend([{"inspection":"output_flat_preparation", "path":str(part), "result":flat}, model,
                           {"inspection":"output_dxf", "path":str(part), "result":exported},
                           {"inspection":"output_drawing_deferred", "path":str(part), "reason":audit.DRAWING_DEFERRED}])
            cases.append({key:model[key] for key in ("hand","widthMm","heightMm")})
            cases[-1].update(folder=part.parent.name, part=part.name, partSha256=audit.digest(part),
                             flatPreparation=flat, drawing=None, dxf=exported)
        events.append({"inspection":"finished", "success":True, "cadExited":True})
        document = {"action":"outputs-dxf", "production_release":False, "events":events}
        job = {"action":"outputs-dxf", "outputScope":audit.DXF_ONLY_SCOPE, "production_release":False}
        manifest = {"schema":"16029-variable-output-manifest/v2", "complete":True, "outputScope":audit.DXF_ONLY_SCOPE,
                    "drawingStatus":audit.DRAWING_DEFERRED, "production_release":False, "cases":cases}
        history = self.root.parents[1] / "review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108"
        for side in ("left","right"):
            source = history / (side+".dxf")
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(data)
            write_json(history / ("door_dxf_certificate20_door_panel_"+side+"_W437_H471p25.json"),
                       {"dxf":str(source), "dxfSha256":audit.digest(source)})
        self.save_dxf_fixture(job, document, manifest)
        return job, document, manifest

    def save_dxf_fixture(self, job, document, manifest):
        write_json(self.run / "job.json", job)
        write_json(self.run / "native-result.json", document)
        write_json(self.run / "cad/outputs/output-manifest.json", manifest)

    def test_explicit_dxf_only_scope_audits_real_fixture_geometry_without_fabricating_directions(self):
        self.dxf_only_fixture()
        inputs = [path for path in self.run.rglob("*") if path.is_file()]
        before = {str(path):audit.digest(path) for path in inputs}
        target = self.invoke("explicit-dxf-only-audit")
        result = audit.load(target / "output-audit.json")
        self.assertTrue(result["dxfGeometryScopePass"])
        self.assertFalse(result["dxfDirectionPass"])
        self.assertFalse(result["dxfTechnicalPass"])
        self.assertFalse(result["fullPlanPass"])
        self.assertFalse(result["production_release"])
        self.assertFalse(result["nativeBatchSupervisionAccepted"])
        self.assertEqual(result["runnerSuccessRecord"]["reason"], "RUNNER_SUCCESS_RECORD_MISSING")
        self.assertEqual(result["drawingStatus"], audit.DRAWING_DEFERRED)
        for case in result["cases"]:
            self.assertTrue(case["dxfGeometryPass"])
            self.assertTrue(case["nativeHoleGeometryPass"])
            self.assertTrue(case["pdfCheckSkipped"])
            self.assertIsNone(case["pdfStructuralPass"])
            self.assertFalse(case["handednessProvenByThisComparison"])
            report = audit.load(target / case["case"] / "dxf-audit.json")
            self.assertFalse(report["auditPassed"])
            self.assertEqual(report["nativeBendComparison"]["directionConvention"], "UNKNOWN")
            self.assertTrue(report["nativeFlatCutComparison"]["verified"])
            self.assertFalse(report["nativeFlatCutComparison"]["manufacturingOrientationVerified"])
            self.assertEqual(report["nativeHoleGeometryComparison"]["nativeHoleCount"], 2)
        self.assertFalse(list(target.rglob("pdf-audit.json")))
        self.assertFalse(list(target.rglob("page-*.png")))
        self.assertEqual(before, {str(path):audit.digest(path) for path in inputs})

    def test_dxf_only_rejects_missing_or_conflicting_job_native_and_manifest_scope(self):
        changes = ("missing-job-scope", "wrong-job-action", "wrong-native-action", "wrong-schema", "missing-manifest-scope",
                   "wrong-deferred-status", "wrong-release", "incomplete", "nonnull-drawing", "missing-flat")
        for label in changes:
            with self.subTest(label=label):
                job,document,manifest = self.dxf_only_fixture()
                if label == "missing-job-scope": job.pop("outputScope")
                elif label == "wrong-job-action": job["action"] = "outputs"
                elif label == "wrong-native-action": document["action"] = "outputs"
                elif label == "wrong-schema": manifest["schema"] = "16029-variable-output-manifest/v1"
                elif label == "missing-manifest-scope": manifest.pop("outputScope")
                elif label == "wrong-deferred-status": manifest["drawingStatus"] = "complete"
                elif label == "wrong-release": manifest["production_release"] = True
                elif label == "incomplete": manifest["complete"] = False
                elif label == "nonnull-drawing": manifest["cases"][0]["drawing"] = {}
                elif label == "missing-flat": manifest["cases"][0].pop("flatPreparation")
                self.save_dxf_fixture(job,document,manifest)
                with self.assertRaises(audit.dxf.AuditError):
                    self.invoke(label+"-audit")

    def test_dxf_only_rejects_missing_duplicate_mixed_and_wrongly_deferred_native_evidence(self):
        for label in ("missing-scope", "late-scope", "missing-deferred", "duplicate-deferred", "wrong-reason", "mixed-drawing"):
            with self.subTest(label=label):
                job,document,manifest = self.dxf_only_fixture()
                events = document["events"]
                deferred = next(event for event in events if event["inspection"] == "output_drawing_deferred")
                if label == "missing-scope": events.pop(0)
                elif label == "late-scope": events.insert(-1,events.pop(0))
                elif label == "missing-deferred": events.remove(deferred)
                elif label == "duplicate-deferred": events.insert(-1,copy.deepcopy(deferred))
                elif label == "wrong-reason": deferred["reason"] = "unverified"
                elif label == "mixed-drawing": events.insert(-1,{"inspection":"output_drawing", "path":deferred["path"], "result":{}})
                self.save_dxf_fixture(job,document,manifest)
                with self.assertRaises(audit.dxf.AuditError):
                    self.invoke(label+"-audit")

    def test_full_mode_with_missing_drawing_cannot_use_dxf_deferral(self):
        with self.fixture(pdf_bytes("full-missing-drawing")):
            document = audit.load(self.run / "native-result.json")
            document["events"] = [event for event in document["events"] if event["inspection"] != "output_drawing"]
            write_json(self.run / "native-result.json",document)
            with self.assertRaisesRegex(audit.dxf.AuditError,"FULL_DRAWING_OUTPUT_EVIDENCE_REQUIRED"):
                self.invoke("full-missing-drawing-audit")

    def test_full_mode_with_missing_pdf_file_still_fails(self):
        with self.fixture(pdf_bytes("full-missing-pdf")) as models:
            (Path(models[0]["path"]).parent / "drawing.pdf").unlink()
            with self.assertRaises(FileNotFoundError):
                self.invoke("full-missing-pdf-audit")

    def test_dxf_only_cannot_skip_pdf_without_both_authorizing_records(self):
        for name in ("job.json", "cad/outputs/output-manifest.json"):
            with self.subTest(name=name):
                self.dxf_only_fixture()
                (self.run / name).unlink()
                with self.assertRaisesRegex(audit.dxf.AuditError,"OUTPUT_SCOPE_JOB_AND_MANIFEST_REQUIRED"):
                    self.invoke("missing-"+Path(name).name+"-audit")

    def test_dxf_only_does_not_bypass_unit_checks(self):
        job,document,manifest = self.dxf_only_fixture()
        first = manifest["cases"][0]
        path = Path(first["dxf"]["path"])
        path.write_bytes(path.read_bytes().replace(b"$INSUNITS\n70\n4\n", b"$INSUNITS\n70\n0\n",1))
        first["dxf"]["sha256"] = audit.digest(path)
        self.save_dxf_fixture(job,document,manifest)
        target = self.invoke("unknown-units-audit")
        result = audit.load(target / "output-audit.json")
        self.assertFalse(result["dxfGeometryScopePass"])
        self.assertTrue(any("UNVERIFIED_UNITS" in error for error in result["cases"][0]["dxfErrors"]))

    def test_dxf_only_keeps_native_flat_hole_geometry_comparison_fail_closed(self):
        job,document,manifest = self.dxf_only_fixture()
        case = manifest["cases"][0]
        next(row for row in case["flatPreparation"]["geometry"]["cutGeometry"]["primitives"] if row["type"] == "CIRCLE")["centerMm"][0] += 1
        self.save_dxf_fixture(job,document,manifest)
        target = self.invoke("wrong-native-hole-audit")
        result = audit.load(target / "output-audit.json")
        self.assertFalse(result["dxfGeometryScopePass"])
        self.assertFalse(result["cases"][0]["nativeHoleGeometryPass"])
        report = audit.load(target / "L0/dxf-audit.json")
        self.assertIn("UNIQUE_COMPLETE_NATIVE_FLAT_CUT_MAPPING_REQUIRED",report["nativeFlatCutComparison"]["errors"])

    def test_dxf_only_rejects_changed_part_or_dxf_bytes(self):
        for label in ("part", "dxf"):
            with self.subTest(label=label):
                _,_,manifest = self.dxf_only_fixture()
                item = manifest["cases"][0]
                folder = self.run / "cad/outputs" / item["folder"]
                path = folder / (item["part"] if label == "part" else "flat.dxf")
                path.write_bytes(path.read_bytes()+b"changed")
                with self.assertRaisesRegex(audit.dxf.AuditError,"OUTPUT_MANIFEST_(PART|DXF)_IDENTITY_MISMATCH"):
                    self.invoke("changed-"+label+"-audit")

    def test_symmetric_dxf_geometry_matches_all_candidates_while_unique_mapping_and_direction_stay_false(self):
        self.dxf_only_fixture(symmetric=True)
        target = self.invoke("symmetric-two-candidate-audit")
        result = audit.load(target / "output-audit.json")
        self.assertTrue(result["dxfGeometryScopePass"])
        self.assertFalse(result["dxfDirectionPass"])
        self.assertFalse(result["dxfTechnicalPass"])
        self.assertFalse(result["fullPlanPass"])
        for case in result["cases"]:
            self.assertTrue(case["completeCutGeometryEquivalent"])
            self.assertFalse(case["coordinateMappingUniqueInSearchedFamily"])
            self.assertEqual(case["cutMappingCandidateCount"], 2)
            self.assertEqual(set(case["cutMappingDeterminants"]), {-1,1})
            self.assertTrue(case["holesEquivalentUnderAllCandidates"])
            self.assertTrue(case["nativeHoleGeometryPass"])
            self.assertFalse(case["handednessProvenByThisComparison"])
            self.assertFalse(case["manufacturingOrientationVerified"])
            report = audit.load(target / case["case"] / "dxf-audit.json")
            mapping,holes = report["nativeFlatCutComparison"],report["nativeHoleGeometryComparison"]
            self.assertFalse(mapping["verified"])
            self.assertNotIn("mapping",mapping)
            self.assertFalse(holes["uniqueCentersAndDiametersMatchInFullCutMapping"])
            self.assertEqual(holes["holes"],[])
            self.assertEqual(len(holes["candidateComparisons"]),2)
            self.assertTrue(all(candidate["uniqueCentersAndDiametersMatch"] for candidate in holes["candidateComparisons"]))
            indices = [[row["matchingDxfIndices"][0] for row in candidate["holes"]]
                       for candidate in holes["candidateComparisons"]]
            self.assertNotEqual(indices[0],indices[1])

    def test_dxf_only_rejects_actual_moved_hole_missing_hole_scaling_and_wrong_arc_trim(self):
        for label in ("moved-hole", "missing-hole", "scaled", "wrong-arc-trim"):
            with self.subTest(label=label):
                job,document,manifest = self.dxf_only_fixture(symmetric=True,with_arc=label=="wrong-arc-trim")
                exported = manifest["cases"][0]["dxf"]
                path = Path(exported["path"])
                pairs = audit.dxf.pairs_from_bytes(path.read_bytes())
                if label == "scaled":
                    pairs = [(code,str(float(value)*1.01) if code in (10,20,11,21,40) else value) for code,value in pairs]
                else:
                    kind = "ARC" if label == "wrong-arc-trim" else "CIRCLE"
                    begin = next(index for index,row in enumerate(pairs) if row == (0,kind))
                    end = next(index for index in range(begin+1,len(pairs)) if pairs[index][0] == 0)
                    if label == "missing-hole":
                        del pairs[begin:end]
                    else:
                        for index in range(begin,end):
                            code,value = pairs[index]
                            if label == "moved-hole" and code == 10:
                                pairs[index] = (code,str(float(value)+1))
                            elif label == "wrong-arc-trim" and code in (50,51):
                                pairs[index] = (code,str((float(value)+180)%360))
                path.write_bytes("".join("%s\n%s\n" % pair for pair in pairs).encode("ascii"))
                exported["sha256"] = audit.digest(path)
                self.save_dxf_fixture(job,document,manifest)
                target = self.invoke(label+"-geometry-audit")
                result = audit.load(target / "output-audit.json")
                report = audit.load(target / "L0/dxf-audit.json")
                self.assertFalse(result["dxfGeometryScopePass"])
                self.assertFalse(result["cases"][0]["completeCutGeometryEquivalent"])
                self.assertFalse(result["cases"][0]["holesEquivalentUnderAllCandidates"])
                self.assertFalse(report["nativeFlatCutComparison"]["verified"])
                self.assertFalse(result["fullPlanPass"])

    def test_native_readback_accepts_only_defined_geometric_doubles_and_records_each_difference(self):
        job,document,manifest = self.dxf_only_fixture(with_arc=True)
        exported = manifest["cases"][0]["dxf"]
        exported["nativeCutGeometry"] = copy.deepcopy(exported["nativeCutGeometry"])
        cut = exported["nativeCutGeometry"]
        line,circle,arc = (next(row for row in cut["primitives"] if row["type"] == kind) for kind in ("LINE","CIRCLE","ARC"))
        changes = ((cut,"topZMm",5e-10), (line,"lengthMm",5e-10),
                   (line["startMm"],0,5e-10), (line["endMm"],1,5e-10),
                   (circle["centerMm"],0,5e-10), (circle,"radiusMm",5e-10),
                   (arc["midpointMm"],1,5e-10),
                   (line,"parameterMin",5e-13), (line,"parameterMax",5e-13),
                   (cut["arcSampling"][0]["edgeParameterRange"],0,5e-13),
                   (cut["arcSampling"][0]["curveParameterDomain"],"minimum",5e-13),
                   (cut["arcSampling"][0]["curveParameterDomain"],"maximum",5e-13),
                   (cut["arcSampling"][0]["samples"][0],"parameter",5e-13),
                   (cut["arcSampling"][0]["samples"][0]["pointMm"],2,5e-10))
        for row,key,delta in changes:
            row[key] += delta
        self.save_dxf_fixture(job,document,manifest)
        target = self.invoke("defined-native-readback-doubles-audit")
        result = audit.load(target / "output-audit.json")
        mapping = audit.load(target / "L0/dxf-audit.json")["nativeFlatCutComparison"]
        self.assertTrue(result["dxfGeometryScopePass"])
        self.assertFalse(mapping["wholeDictionaryEqual"])
        self.assertTrue(mapping["identityExact"])
        self.assertTrue(mapping["nativeReadbackEquivalent"])
        self.assertEqual(mapping["readbackErrors"], [])
        self.assertEqual(len(mapping["readbackDifferences"]), len(changes))
        self.assertEqual({row["threshold"] for row in mapping["readbackDifferences"]}, {1e-9,1e-12})
        for row in mapping["readbackDifferences"]:
            self.assertTrue(row["accepted"])
            self.assertEqual(row["absoluteDifference"], abs(row["before"]-row["after"]))
            self.assertLessEqual(row["absoluteDifference"], row["threshold"])
            self.assertTrue(row["path"].startswith("$."))
        self.assertEqual(mapping["toleranceMm"], 0.01)
        self.assertFalse(mapping["readbackToleranceIsManufacturingTolerance"])
        self.assertFalse(result["nativeBatchSupervisionAccepted"])
        self.assertTrue(all(case["wholeDictionaryEqual"] for case in result["cases"][1:]))

    def test_native_readback_keeps_identity_types_keys_counts_and_order_exact(self):
        labels = ("path", "hand", "configuration", "schema", "complete", "units", "errors", "id", "curveType",
                  "primitive-type", "sense", "count", "count-type", "top-filter", "missing-key", "extra-key",
                  "missing-primitive", "extra-primitive", "swapped-indices", "unknown-number", "sampling-status",
                  "sampling-api", "sampling-bits", "sampling-order")
        for label in labels:
            with self.subTest(label=label):
                job,document,manifest = self.dxf_only_fixture(with_arc=True)
                exported = manifest["cases"][0]["dxf"]
                exported["nativeCutGeometry"] = copy.deepcopy(exported["nativeCutGeometry"])
                cut = exported["nativeCutGeometry"]
                first = cut["primitives"][0]
                if label == "path": cut["modelPath"] += ".other"
                elif label == "hand": cut["modelHand"] = "R"
                elif label == "configuration": cut["configuration"] = "Default"
                elif label == "schema": cut["schema"] += "-other"
                elif label == "complete": cut["complete"] = False
                elif label == "units": cut["units"] = "m"
                elif label == "errors": cut["errors"].append("readback error")
                elif label == "id": first["id"] = "C2"
                elif label == "curveType": first["curveType"] += 1
                elif label == "primitive-type": first["type"] = "ARC"
                elif label == "sense": first["curveAndEdgeSameSense"] = False
                elif label == "count": cut["bodyEdgeCount"] += 1
                elif label == "count-type": cut["bodyEdgeCount"] = float(cut["bodyEdgeCount"])
                elif label == "top-filter": cut["topToleranceMm"] += 1e-15
                elif label == "missing-key": first.pop("parameterMin")
                elif label == "extra-key": first["unrecognized"] = 0
                elif label == "missing-primitive": cut["primitives"].pop()
                elif label == "extra-primitive": cut["primitives"].append(copy.deepcopy(first))
                elif label == "swapped-indices": cut["primitives"][0],cut["primitives"][1] = cut["primitives"][1],first
                elif label == "unknown-number":
                    manifest["cases"][0]["flatPreparation"]["geometry"]["cutGeometry"]["unknownNumber"] = 0
                    cut["unknownNumber"] = 1e-15
                elif label == "sampling-status": cut["arcSampling"][0]["samples"][0]["status"] = 0
                elif label == "sampling-api": cut["arcSampling"][0]["evaluationApi"] += "Other"
                elif label == "sampling-bits": cut["arcSampling"][0]["samples"][0]["packedStatusBits"] = "0000000000000001"
                elif label == "sampling-order": cut["arcSampling"][0]["edgeParameterRange"].reverse()
                self.save_dxf_fixture(job,document,manifest)
                target = self.invoke(label+"-native-readback-reject-audit")
                result = audit.load(target / "output-audit.json")
                mapping = audit.load(target / "L0/dxf-audit.json")["nativeFlatCutComparison"]
                self.assertFalse(result["dxfGeometryScopePass"])
                self.assertFalse(mapping["nativeReadbackEquivalent"])
                self.assertFalse(mapping["completeCutGeometryEquivalent"])
                self.assertIn("NATIVE_FLAT_CUT_SOURCE_IDENTITY_MISMATCH",mapping["errors"])

    def test_native_readback_rejects_nonfinite_wrong_types_and_changes_below_dxf_tolerance(self):
        for label in ("coordinate", "length", "radius", "curve-parameter", "top", "sample-parameter", "sample-point",
                      "domain", "range", "nan", "infinity", "bool", "string", "equal-nan"):
            with self.subTest(label=label):
                job,document,manifest = self.dxf_only_fixture(with_arc=True)
                exported = manifest["cases"][0]["dxf"]
                exported["nativeCutGeometry"] = copy.deepcopy(exported["nativeCutGeometry"])
                cut = exported["nativeCutGeometry"]
                first,sampling = cut["primitives"][0],cut["arcSampling"][0]
                if label == "coordinate": first["startMm"][0] += 2e-9
                elif label == "length": first["lengthMm"] += 2e-9
                elif label == "radius": next(row for row in cut["primitives"] if row["type"] == "CIRCLE")["radiusMm"] += 2e-9
                elif label == "curve-parameter": first["parameterMin"] += 2e-12
                elif label == "top": cut["topZMm"] += 2e-9
                elif label == "sample-parameter": sampling["samples"][0]["parameter"] += 2e-12
                elif label == "sample-point": sampling["samples"][0]["pointMm"][2] += 2e-9
                elif label == "domain": sampling["curveParameterDomain"]["maximum"] += 2e-12
                elif label == "range": sampling["edgeParameterRange"][1] += 2e-12
                elif label == "nan": first["lengthMm"] = float("nan")
                elif label == "infinity": first["lengthMm"] = float("inf")
                elif label == "bool": first["lengthMm"] = True
                elif label == "string": first["lengthMm"] = str(first["lengthMm"])
                elif label == "equal-nan":
                    first["lengthMm"] = float("nan")
                    manifest["cases"][0]["flatPreparation"]["geometry"]["cutGeometry"]["primitives"][0]["lengthMm"] = float("nan")
                self.save_dxf_fixture(job,document,manifest)
                target = self.invoke(label+"-native-readback-value-reject-audit")
                result = audit.load(target / "output-audit.json")
                mapping = audit.load(target / "L0/dxf-audit.json")["nativeFlatCutComparison"]
                self.assertFalse(result["dxfGeometryScopePass"])
                self.assertFalse(mapping["nativeReadbackEquivalent"])
                self.assertFalse(mapping["completeCutGeometryEquivalent"])
                self.assertTrue(mapping["readbackErrors"])
                self.assertIn("NATIVE_FLAT_CUT_SOURCE_IDENTITY_MISMATCH",mapping["errors"])
                self.assertEqual(mapping["toleranceMm"], 0.01)

    def test_equal_native_snapshots_cannot_override_model_source_or_export_configuration(self):
        for label in ("path", "hand", "export-configuration"):
            with self.subTest(label=label):
                job,document,manifest = self.dxf_only_fixture()
                case = manifest["cases"][0]
                cut = case["flatPreparation"]["geometry"]["cutGeometry"]
                if label == "path": cut["modelPath"] += ".other"
                elif label == "hand": cut["modelHand"] = "R"
                else: case["dxf"]["exportConfiguration"] = "Default"
                self.save_dxf_fixture(job,document,manifest)
                target = self.invoke(label+"-equal-snapshot-wrong-source-audit")
                mapping = audit.load(target / "L0/dxf-audit.json")["nativeFlatCutComparison"]
                self.assertTrue(mapping["wholeDictionaryEqual"])
                self.assertFalse(mapping["identityExact"])
                self.assertFalse(mapping["nativeReadbackEquivalent"])
                self.assertFalse(all(mapping["sourceIdentityChecks"].values()))
                self.assertFalse(mapping["completeCutGeometryEquivalent"])


if __name__ == "__main__":
    unittest.main()
