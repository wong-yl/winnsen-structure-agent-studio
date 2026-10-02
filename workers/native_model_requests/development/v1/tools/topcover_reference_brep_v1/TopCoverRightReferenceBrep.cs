using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.TopCoverReferenceBrepV1
{
    // Read-only reference capture only. This executable never creates or changes CAD files.
    internal static class TopCoverRightReferenceBrep
    {
        private const string ToolId = "topcover_reference_brep_v1";
        private const string Mode = "left-side-760-and-1000-sharp-flat-detailed-v7";
        private const string V37InputName = "v37_left_side.sldprt";
        private const string GoldInputName = "gold_left_side.sldprt";
        private const string EvidenceName = "topcover_left_reference_brep_capture_v7.json";
        private const string ExpectedEvidenceRelative = "evidence/private/" + EvidenceName;
        private const string ExpectedSourceSha256 = "96471E368A33E72E0C5AB9B484CEE93D3FE4154998A9EA5F4EE32EFF0DF156BD";
        private const string ExpectedSolidWorksExePath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string ExpectedSolidWorksExeSha256 = "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string ExpectedMonitorExePath = @"D:\soildworks2020\SOLIDWORKS\sldProcMon.exe";
        private const string ExpectedMonitorExeSha256 = "A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70";
        private const string ExpectedRevisionPrefix = "28.";
        private const int ContractExit = 64;
        private const int CaptureExit = 65;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 128 };

        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                if (args.Length == 1 && args[0] == "--help") { Help(); return 0; }
                if (args.Length == 1 && args[0] == "--identity")
                {
                    Console.WriteLine(Json.Serialize(new Dictionary<string, object> {
                        { "toolId", ToolId }, { "mode", Mode }, { "sourceNormalizedSha256", ExpectedSourceSha256 },
                        { "probeOnly", true }, { "consumable", false }, { "cadStarted", false } }));
                    return 0;
                }
                if (args.Length == 1 && args[0] == "--self-test") return SelfTest();
                Invocation input = Parse(args);
                Validate(input);
                return Capture(input);
            }
            catch (ContractException e) { Console.Error.WriteLine("REFERENCE_CAPTURE_CONTRACT_FAILED: " + e.Message); return ContractExit; }
            catch (Exception e) { Console.Error.WriteLine("REFERENCE_CAPTURE_CONTRACT_FAILED: UNEXPECTED_" + e.GetType().Name); return ContractExit; }
        }

        private static void Help()
        {
            Console.WriteLine("TopCoverRightReferenceBrep read-only capture");
            Console.WriteLine("--mode left-side-760-and-1000-sharp-flat-detailed-v7 --attempt-dir <attempt> --v37 <capture_inputs/v37_left_side.sldprt> --v37-sha256 <SHA256> --gold <capture_inputs/gold_left_side.sldprt> --gold-sha256 <SHA256> --out <attempt/evidence/private/topcover_left_reference_brep_capture_v7.json>");
        }

        private static Invocation Parse(string[] a)
        {
            Need(a.Length == 14 && a[0] == "--mode" && a[1] == Mode && a[2] == "--attempt-dir" &&
                a[4] == "--v37" && a[6] == "--v37-sha256" && a[8] == "--gold" &&
                a[10] == "--gold-sha256" && a[12] == "--out", "ARGUMENT_GRAMMAR_INVALID");
            return new Invocation { attempt = Full(a[3]), v37 = Full(a[5]), v37Hash = Upper(a[7]), gold = Full(a[9]), goldHash = Upper(a[11]), output = Full(a[13]) };
        }

        // Kept apart from parser to make the pinned attempt-local contract auditable.
        private static Invocation ParseLegacyFree(string[] a) { throw new ContractException("ORDINARY_MODE_FORBIDDEN"); }

        private static void Validate(Invocation x)
        {
            Need(Directory.Exists(x.attempt), "ATTEMPT_MISSING");
            string repositoryRoot = FindRepositoryRoot();
            string attemptsRoot = Path.Combine(repositoryRoot, "data",
                "native_model_requests", "attempts");
            string taskRoot = Path.GetDirectoryName(x.attempt);
            Need(Same(Path.GetDirectoryName(taskRoot), attemptsRoot) &&
                String.Equals(Path.GetFileName(x.attempt), "attempt-0001",
                    StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(Path.GetFileName(taskRoot) ?? "",
                    "^TOPCOVER-REFERENCE-[A-Z0-9-]{8,120}$",
                    RegexOptions.CultureInvariant), "ATTEMPT_SHAPE_INVALID");
            string inputs = Path.Combine(x.attempt, "capture_inputs");
            Need(Directory.Exists(inputs) && !HasReparse(inputs) && !HasReparse(x.attempt), "CAPTURE_INPUTS_DIRECTORY_INVALID");
            Need(Same(x.v37, Path.Combine(inputs, V37InputName)) && Same(x.gold, Path.Combine(inputs, GoldInputName)), "INPUT_NAME_OR_DIRECTORY_INVALID");
            Need(Directory.GetDirectories(inputs, "*", SearchOption.TopDirectoryOnly).Length == 0 &&
                new HashSet<string>(Directory.GetFiles(inputs, "*", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase).SetEquals(
                        new[] { V37InputName, GoldInputName }),
                "CAPTURE_INPUTS_NOT_EXACT_TWO_FLAT_FILES");
            Need(Regex.IsMatch(x.v37Hash ?? "", "^[A-F0-9]{64}$") && Regex.IsMatch(x.goldHash ?? "", "^[A-F0-9]{64}$"), "INPUT_HASH_GRAMMAR_INVALID");
            ValidateInput(x.v37, x.v37Hash, x.attempt);
            ValidateInput(x.gold, x.goldHash, x.attempt);
            string expected = Path.Combine(x.attempt, ExpectedEvidenceRelative.Replace('/', Path.DirectorySeparatorChar));
            Need(Same(x.output, expected) && !File.Exists(x.output) && Directory.Exists(Path.GetDirectoryName(x.output)), "EVIDENCE_OUTPUT_INVALID");
            AssertNoReparseChain(Path.GetDirectoryName(x.output), x.attempt);
            Need(SourceIdentityValid(), "TOOL_SOURCE_IDENTITY_INVALID");
        }

        private static void ValidateInput(string path, string hash, string attempt)
        {
            Need(File.Exists(path) && !HasReparse(path) && LinkCount(path) == 1, "INPUT_SINGLE_HARDLINK_OR_REPARSE_INVALID");
            AssertNoReparseChain(path, attempt);
            Need(string.Equals(Sha(path), hash, StringComparison.OrdinalIgnoreCase), "INPUT_SHA256_MISMATCH");
        }

        private static int Capture(Invocation x)
        {
            string v37Before = Sha(x.v37), goldBefore = Sha(x.gold);
            Audit audit = new Audit();
            Dictionary<string, object> evidence = null;
            try
            {
                Need(StableZero(30000, 1000, 200), "CAD_BASELINE_NOT_STABLY_ZERO");
                ValidateToolchain();
                PartPairSnapshot v37 = CaptureOne("v37_760", x.v37, x.v37Hash, audit);
                PartPairSnapshot gold = CaptureOne("gold_1000", x.gold, x.goldHash, audit);
                Need(String.Equals(v37.folded.translationNormalizedDetailedGeometrySignature,
                    gold.folded.translationNormalizedDetailedGeometrySignature,
                    StringComparison.Ordinal) && String.Equals(
                        v37.sharpRollback.translationNormalizedDetailedGeometrySignature,
                        gold.sharpRollback.translationNormalizedDetailedGeometrySignature,
                        StringComparison.Ordinal) && String.Equals(
                        v37.flat.translationNormalizedDetailedGeometrySignature,
                        gold.flat.translationNormalizedDetailedGeometrySignature,
                        StringComparison.Ordinal) && DeltasMatch(v37, gold),
                        "REFERENCE_760_1000_SHARP_FLAT_SIGNATURE_OR_DELTA_MISMATCH");
                Need(string.Equals(v37Before, Sha(x.v37), StringComparison.OrdinalIgnoreCase) && string.Equals(goldBefore, Sha(x.gold), StringComparison.OrdinalIgnoreCase), "INPUT_CHANGED_DURING_CAPTURE");
                Need(StableZero(30000, 1000, 200) && audit.AllExited && audit.errors.Count == 0, "CAD_FINAL_ZERO_GATE_FAILED");
                evidence = new Dictionary<string, object> {
                    { "schema", "winnsen.16029.topcover_left_reference_brep_capture.v7" }, { "status", "TOPCOVER_LEFT_REFERENCE_BREP_CAPTURE_V7_COMPLETE" },
                    { "mode", Mode }, { "purpose", "structure_engineering_assistance" }, { "probeOnly", true }, { "consumable", false },
                    { "tool", new Dictionary<string, object> { { "id", ToolId }, { "sourceNormalizedSha256", ExpectedSourceSha256 }, { "executableSha256", Sha(Assembly.GetExecutingAssembly().Location) } } },
                    { "inputs", new Dictionary<string, object> { { "v37_760", InputRecord(x.v37, x.v37Hash, v37Before) }, { "gold_1000", InputRecord(x.gold, x.goldHash, goldBefore) } } },
                    { "parts", new Dictionary<string, object> { { "v37_760", v37 }, { "gold_1000", gold } } },
                    { "processAudit", audit }, { "completedAtUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                    { "qualityBoundary", "flat 为 manufacturing flat body，可能排除后续 formed/PEM features，不能当作完整侧板；只读参考采集不构成结构验收或整柜结论。" }
                };
                byte[] bytes = StandardJson(evidence);
                WriteNewAndReread(x.output, bytes, x.attempt);
                Console.WriteLine(Json.Serialize(new Dictionary<string, object> { { "status", "TOPCOVER_LEFT_REFERENCE_BREP_CAPTURE_V7_COMPLETE" }, { "cadStarted", true }, { "evidenceSha256", Sha(x.output) } }));
                return 0;
            }
            catch (Exception e)
            {
                audit.errors.Add("capture:" + e.GetType().Name);
                bool unchanged = File.Exists(x.v37) && File.Exists(x.gold) && Sha(x.v37) == v37Before && Sha(x.gold) == goldBefore;
                bool globalZero = StableZero(30000, 1000, 200);
                bool allExited = audit.AllExited;
                Console.Error.WriteLine("REFERENCE_CAPTURE_FAILED: " + e.GetType().Name +
                    ":" + (e.Message ?? "").Replace('\r', ' ').Replace('\n', ' ') +
                    " inputUnchanged=" + unchanged + " globalCadZero=" + globalZero +
                    " ownedSessionsExited=" + allExited + " sessions=" +
                    String.Join(";", audit.sessions.Select(value => value.label +
                        ":sw=" + value.sldworksExited + ",mon=" +
                        value.monitors.All(monitor => monitor.exited) + ",all=" +
                        value.allExited)));
                return CaptureExit;
            }
        }

        private static Dictionary<string, object> InputRecord(string path, string expected, string before)
        {
            return new Dictionary<string, object> { { "path", path }, { "expectedSha256", expected }, { "beforeSha256", before }, { "afterSha256", Sha(path) }, { "singleHardlink", LinkCount(path) == 1 }, { "reparseFree", !HasReparse(path) } };
        }

        private static PartPairSnapshot CaptureOne(string label, string path, string expectedHash, Audit audit)
        {
            Need(StableZero(30000, 1000, 200), "CAD_SESSION_BASELINE_INVALID_" + label);
            string before = Sha(path);
            OwnedSession session = new OwnedSession { label = label }; audit.sessions.Add(session);
            ISldWorks app = null; ModelDoc2 doc = null;
            try
            {
                app = StartOwned(session);
                int errors = 0, warnings = 0;
                doc = app.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                    "", ref errors, ref warnings) as ModelDoc2;
                Need(doc != null && errors == 0 && warnings == 0 && doc.IsOpenedReadOnly(), "READONLY_OPENDOC6_FAILED_" + label);
                Need(doc.ForceRebuild3(false), "READONLY_FORCE_REBUILD_FAILED_" + label);
                PartSnapshot folded = Inspect(doc, true);
                folded.label = label; folded.path = path; folded.expectedSha256 = expectedHash;
                folded.beforeSha256 = before; folded.openErrors = errors;
                folded.openWarnings = warnings; folded.readOnly = true; folded.rebuilt = true;

                PartPairSnapshot pair = new PartPairSnapshot { folded = folded };
                Feature sheetMetal = FindUniqueRootFeature(doc, "SheetMetal");
                object sheetMetalDefinition = null;
                ISheetMetalFeatureData sheetMetalData = null;
                try
                {
                    sheetMetalDefinition = sheetMetal.GetDefinition();
                    sheetMetalData = sheetMetalDefinition as ISheetMetalFeatureData;
                    Need(sheetMetalData != null,
                        "SHEET_METAL_ROLLBACK_DEFINITION_INVALID_" + label);
                    pair.sheetMetalSelectionAccessed =
                        sheetMetalData.IAccessSelections2(doc, null);
                    Need(pair.sheetMetalSelectionAccessed,
                        "SHEET_METAL_ROLLBACK_ACCESS_FAILED_" + label);
                    pair.sharpRollback = InspectGeometryOnly(doc);
                    pair.sharpRollback.label = label + "_sharp_rollback";
                    pair.sharpRollback.path = path;
                    pair.sharpRollback.expectedSha256 = expectedHash;
                    pair.sharpRollback.beforeSha256 = before;
                    pair.sharpRollback.openErrors = errors;
                    pair.sharpRollback.openWarnings = warnings;
                    pair.sharpRollback.readOnly = doc.IsOpenedReadOnly();
                    pair.sharpRollback.rebuilt = false;
                    pair.sharpRollbackCaptured =
                        pair.sharpRollback.readOnly && pair.sharpRollback.faceCount > 0 &&
                        pair.sharpRollback.bboxMm.Count == 6 &&
                        pair.sharpRollback.volumeMm3 > 0.0 &&
                        pair.sharpRollback.massKg > 0.0;
                    Need(pair.sharpRollbackCaptured,
                        "SHARP_ROLLBACK_GEOMETRY_INVALID_" + label);
                }
                finally
                {
                    if (sheetMetalData != null && pair.sheetMetalSelectionAccessed)
                    {
                        sheetMetalData.ReleaseSelectionAccess();
                        pair.sheetMetalSelectionReleased = true;
                    }
                    Release(sheetMetalDefinition);
                    Release(sheetMetal);
                }
                pair.sharpRollbackRestoreRebuild = doc.ForceRebuild3(false);
                Need(pair.sharpRollbackRestoreRebuild,
                    "SHARP_ROLLBACK_RESTORE_REBUILD_FAILED_" + label);
                PartSnapshot sharpRestored = Inspect(doc, true);
                pair.sharpRollbackRestoredDetailedSignature =
                    sharpRestored.translationNormalizedDetailedGeometrySignature;
                Need(pair.sheetMetalSelectionReleased && String.Equals(
                        folded.translationNormalizedDetailedGeometrySignature,
                        pair.sharpRollbackRestoredDetailedSignature,
                        StringComparison.Ordinal),
                    "SHARP_ROLLBACK_RESTORE_SIGNATURE_MISMATCH_" + label);
                Feature flatPattern = FindUniqueFlatPattern(doc);
                try
                {
                    pair.flatPatternOriginallySuppressed = flatPattern.IsSuppressed();
                    Need(pair.flatPatternOriginallySuppressed,
                        "FLAT_PATTERN_NOT_INITIALLY_SUPPRESSED_" + label);
                    pair.unsuppressReturn = flatPattern.SetSuppression2(
                        (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                        (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    Need(pair.unsuppressReturn, "FLAT_PATTERN_UNSUPPRESS_FAILED_" + label);
                }
                finally { Release(flatPattern); }
                Need(doc.ForceRebuild3(false), "READONLY_FLAT_FORCE_REBUILD_FAILED_" + label);
                flatPattern = FindUniqueFlatPattern(doc);
                try
                {
                    pair.flatPatternUnsuppressedAfterRebuild = !flatPattern.IsSuppressed();
                    Need(pair.flatPatternUnsuppressedAfterRebuild,
                        "FLAT_PATTERN_REMAINS_SUPPRESSED_" + label);
                }
                finally { Release(flatPattern); }
                pair.flat = Inspect(doc, false);
                pair.flat.label = label; pair.flat.path = path;
                pair.flat.expectedSha256 = expectedHash; pair.flat.beforeSha256 = before;
                pair.flat.openErrors = errors; pair.flat.openWarnings = warnings;
                pair.flat.readOnly = doc.IsOpenedReadOnly(); pair.flat.rebuilt = true;
                Need(pair.flat.readOnly, "READONLY_FLAT_DOCUMENT_INVALID_" + label);
                Need(pair.flat.faceCount > 0 && pair.flat.bboxMm.Count == 6 &&
                    pair.flat.volumeMm3 > 0.0 && pair.flat.massKg > 0.0,
                    "MANUFACTURING_FLAT_BODY_NOT_POSITIVE_" + label);
                pair.massConserved = Near(folded.massKg, pair.flat.massKg, 0.000001);
                pair.volumeConserved = Near(folded.volumeMm3, pair.flat.volumeMm3, 0.01);
                pair.foldedMinusFlatVolumeMm3 = folded.volumeMm3 - pair.flat.volumeMm3;
                pair.foldedMinusFlatMassKg = folded.massKg - pair.flat.massKg;
                pair.foldedMinusFlatSurfaceAreaMm2 = folded.surfaceAreaMm2 - pair.flat.surfaceAreaMm2;
                Need(BboxSignificantlyDifferent(folded.bboxMm, pair.flat.bboxMm) &&
                    !String.Equals(folded.translationNormalizedDetailedGeometrySignature,
                        pair.flat.translationNormalizedDetailedGeometrySignature,
                        StringComparison.Ordinal), "FLAT_PATTERN_BBOX_OR_SIGNATURE_NOT_DIFFERENT_" + label);
                flatPattern = FindUniqueFlatPattern(doc);
                try
                {
                    pair.restoreSuppressReturn = flatPattern.SetSuppression2(
                        (int)swFeatureSuppressionAction_e.swSuppressFeature,
                        (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    Need(pair.restoreSuppressReturn, "FLAT_PATTERN_RESTORE_SUPPRESS_FAILED_" + label);
                }
                finally { Release(flatPattern); }
                pair.foldedRestoreRebuild = doc.ForceRebuild3(false);
                Need(pair.foldedRestoreRebuild, "READONLY_FOLDED_RESTORE_REBUILD_FAILED_" + label);
                flatPattern = FindUniqueFlatPattern(doc);
                try
                {
                    pair.flatPatternSuppressedAfterRestore = flatPattern.IsSuppressed();
                    Need(pair.flatPatternSuppressedAfterRestore,
                        "FLAT_PATTERN_RESTORE_STATE_INVALID_" + label);
                }
                finally { Release(flatPattern); }
                PartSnapshot restored = Inspect(doc, true);
                pair.restoreLightweightSignature = restored.geometrySignature;
                pair.restoreDetailedSignature = restored.translationNormalizedDetailedGeometrySignature;
                Need(String.Equals(folded.geometrySignature, pair.restoreLightweightSignature,
                    StringComparison.Ordinal) && String.Equals(
                        folded.translationNormalizedDetailedGeometrySignature,
                        pair.restoreDetailedSignature, StringComparison.Ordinal),
                    "FLAT_PATTERN_RESTORE_SIGNATURE_MISMATCH_" + label);
                return pair;
            }
            finally
            {
                if (doc != null) { Try(() => app.CloseDoc(doc.GetTitle())); Release(doc); }
                StopOwned(app, session); app = null;
                string after = Sha(path);
                session.inputUnchanged = String.Equals(before, after, StringComparison.OrdinalIgnoreCase);
                Need(session.inputUnchanged, "INPUT_MUTATED_" + label);
            }
        }

        private static PartSnapshot Inspect(ModelDoc2 doc, bool requireFoldedFaceCount)
        {
            PartSnapshot s = InspectGeometryOnly(doc);
            Need(s.faces.Count == s.faceCount && (!requireFoldedFaceCount ||
                s.faceCount == 135), "REFERENCE_DETAILED_FACE_COUNT_INVALID");
            s.featureTypeCounts = FeatureCounts(doc, s);
            Need(s.featureTypeCounts.ContainsKey("SheetMetal") &&
                s.featureTypeCounts["SheetMetal"] == 1 &&
                s.featureTypeCounts.ContainsKey("OneBend") &&
                s.featureTypeCounts["OneBend"] == 5 &&
                s.featureTypeCounts.ContainsKey("ProcessBends") &&
                s.featureTypeCounts["ProcessBends"] == 1 &&
                s.featureIssues.Count == 0,
                "REFERENCE_SHEET_METAL_FEATURE_CONTRACT_INCOMPLETE");
            s.externalReferences = ExternalReferences(doc);
                Need(s.externalReferences.Count == 2,
                    "REFERENCE_LEFT_EXTERNAL_REFERENCE_COUNT_NOT_TWO");
            return s;
        }

        private static PartSnapshot InspectGeometryOnly(ModelDoc2 doc)
        {
            PartDoc part = doc as PartDoc; Need(part != null, "NOT_PART_DOCUMENT");
            Array bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            Need(bodies != null && bodies.Length == 1, "SOLID_BODY_COUNT_NOT_ONE");
            Body2 body = bodies.GetValue(0) as Body2; Need(body != null, "SOLID_BODY_NULL");
            PartSnapshot s = new PartSnapshot();
            try
            {
                s.bboxMm = ToList(body.GetBodyBox(), 1000.0);
                s.faceCount = body.GetFaceCount();
                s.edgeCount = body.GetEdgeCount();
                List<double> mp = ToList(body.GetMassProperties(7850.0), 1.0);
                Need(mp.Count >= 6 && mp.All(Finite), "MASS_PROPERTIES_INVALID");
                s.volumeMm3 = mp[3] * 1e9;
                s.surfaceAreaMm2 = mp[4] * 1e6;
                s.massKg = mp[5];
                Array faces = body.GetFaces() as Array;
                foreach (object raw in faces ?? new object[0])
                    InspectFace(raw as Face2, s);
                s.cylinderFaceCount = s.cylinders.Count;
                Need(s.faces.Count == s.faceCount &&
                    s.loopCount == s.faces.Sum(value => value.loops.Count) &&
                    s.loopEdgeReferenceCount == s.faces.Sum(value =>
                        value.loops.Sum(loop => loop.edges.Count)),
                    "REFERENCE_DETAILED_LOOP_EDGE_COUNT_MISMATCH");
                s.geometrySignature = GeometrySignature(s);
                s.translationNormalizedDetailedGeometrySignature =
                    DetailedGeometrySignature(s);
                Need(s.bboxMm.Count == 6 && s.bboxMm.All(Finite) &&
                    Finite(s.massKg) && Finite(s.volumeMm3) &&
                    Finite(s.surfaceAreaMm2), "NONFINITE_GEOMETRY");
                return s;
            }
            finally { Release(body); }
        }

        private static void InspectFace(Face2 face, PartSnapshot s)
        {
            Need(face != null, "FACE_NULL"); Surface surface = null;
            try
            {
                DetailedFace detail = new DetailedFace();
                detail.areaMm2 = face.GetArea() * 1000000.0;
                detail.boxMm = ToList(face.GetBox(), 1000.0);
                detail.normal = ToList(face.Normal, 1.0);
                Need(Finite(detail.areaMm2) && detail.boxMm.Count == 6 &&
                    detail.boxMm.All(Finite) && detail.normal.Count == 3 &&
                    detail.normal.All(Finite), "DETAILED_FACE_NONFINITE");
                surface = face.GetSurface() as Surface;
                Need(surface != null, "FACE_SURFACE_NULL");
                detail.surfaceIdentity = surface.Identity();
                detail.isPlane = surface.IsPlane();
                detail.isCylinder = surface.IsCylinder();
                detail.planeParams = detail.isPlane ? ToListOrEmpty(surface.PlaneParams, 1.0) :
                    new List<double>();
                detail.cylinderParams = detail.isCylinder ? ToListOrEmpty(surface.CylinderParams, 1.0) :
                    new List<double>();
                Need(detail.planeParams.All(Finite) && detail.cylinderParams.All(Finite),
                    "DETAILED_SURFACE_PARAMS_NONFINITE");
                if (surface != null && surface.IsCylinder())
                {
                    List<double> p = ToList(surface.CylinderParams, 1.0); Need(p.Count >= 7 && p.All(Finite), "CYLINDER_PARAMS_INVALID");
                    s.cylinders.Add(new Dictionary<string, object> { { "centerMm", new [] { p[0]*1000, p[1]*1000, p[2]*1000 } }, { "axis", new [] { p[3],p[4],p[5] } }, { "radiusMm", p[6]*1000 } });
                }
                Array loops = face.GetLoops() as Array; s.loopCount += loops == null ? 0 : loops.Length;
                foreach (object rawLoop in loops ?? new object[0])
                {
                    Loop2 loop = rawLoop as Loop2; Need(loop != null, "LOOP_NULL");
                    try
                    {
                        DetailedLoop detailedLoop = new DetailedLoop { isOuter = loop.IsOuter() };
                        Array edges = loop.GetEdges() as Array;
                        detailedLoop.edgeCount = edges == null ? 0 : edges.Length;
                        s.loopEdgeReferenceCount += detailedLoop.edgeCount;
                        foreach (object rawEdge in edges ?? new object[0])
                            detailedLoop.edges.Add(InspectEdge(rawEdge as Edge, s));
                        Need(detailedLoop.edgeCount == detailedLoop.edges.Count,
                            "DETAILED_LOOP_EDGE_COUNT_INVALID");
                        detail.loops.Add(detailedLoop);
                    }
                    finally { Release(loop); }
                }
                s.faces.Add(detail);
            }
            finally { Release(surface); Release(face); }
        }

        private static DetailedEdge InspectEdge(Edge edge, PartSnapshot s)
        {
            Need(edge != null, "EDGE_NULL"); Curve curve = null;
            try
            {
                curve = edge.GetCurve() as Curve;
                Need(curve != null, "EDGE_CURVE_NULL");
                DetailedEdge detail = new DetailedEdge();
                detail.curveType = curve.Identity();
                detail.isLine = curve.IsLine();
                detail.isCircle = curve.IsCircle();
                double min = 0.0, max = 0.0; bool periodic = false, closed = false;
                Need(curve.GetEndParams(out min, out max, out periodic, out closed),
                    "EDGE_END_PARAMS_UNRESOLVED");
                detail.uMin = min; detail.uMax = max;
                detail.lengthMm = curve.GetLength3(min, max) * 1000.0;
                detail.startPointMm = EdgePoint(curve, min);
                detail.endPointMm = EdgePoint(curve, max);
                detail.startVertexPointMm = VertexPoint(edge.GetStartVertex() as Vertex);
                detail.endVertexPointMm = VertexPoint(edge.GetEndVertex() as Vertex);
                detail.lineParams = detail.isLine ? ToListOrEmpty(curve.LineParams, 1.0) :
                    new List<double>();
                detail.circleParams = detail.isCircle ? ToListOrEmpty(curve.CircleParams, 1.0) :
                    new List<double>();
                Need(Finite(detail.uMin) && Finite(detail.uMax) && Finite(detail.lengthMm) &&
                    (detail.startPointMm.Count == 0 || detail.startPointMm.Count == 3) &&
                    (detail.endPointMm.Count == 0 || detail.endPointMm.Count == 3) &&
                    detail.startPointMm.All(Finite) && detail.endPointMm.All(Finite) &&
                    detail.startVertexPointMm.All(Finite) && detail.endVertexPointMm.All(Finite) &&
                    detail.lineParams.All(Finite) && detail.circleParams.All(Finite),
                    "DETAILED_EDGE_NONFINITE");
                if (curve != null && curve.IsCircle())
                {
                    List<double> p = ToList(curve.CircleParams, 1.0); Need(p.Count >= 7 && p.All(Finite), "CIRCLE_PARAMS_INVALID");
                    s.circleEdgeReferences.Add(new Dictionary<string, object> { { "centerMm", new [] { p[0]*1000,p[1]*1000,p[2]*1000 } }, { "axis", new [] { p[3],p[4],p[5] } }, { "radiusMm", p[6]*1000 } });
                }
                return detail;
            }
            finally { Release(curve); Release(edge); }
        }

        private static Dictionary<string, int> FeatureCounts(ModelDoc2 doc, PartSnapshot s)
        {
            Dictionary<string, int> types = new Dictionary<string, int>(StringComparer.Ordinal);
            Feature f = doc.FirstFeature() as Feature; int guard = 0;
            while (f != null && guard++ < 5000)
            {
                CaptureFeatureRecursive(f, types, s, 0);
                Feature next = f.GetNextFeature() as Feature; Release(f); f = next;
            }
            Need(f == null, "FEATURE_GUARD_EXCEEDED"); return types;
        }

        private static Feature FindUniqueRootFeature(ModelDoc2 doc, string typeName)
        {
            Feature match = null;
            Feature feature = doc.FirstFeature() as Feature;
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                Feature next = feature.GetNextFeature() as Feature;
                if (String.Equals(feature.GetTypeName2(), typeName,
                        StringComparison.Ordinal))
                {
                    Need(match == null, "ROOT_FEATURE_NOT_UNIQUE_" + typeName);
                    match = feature;
                }
                else Release(feature);
                feature = next;
            }
            Need(feature == null && match != null,
                "ROOT_FEATURE_NOT_FOUND_" + typeName);
            return match;
        }

        private static Feature FindUniqueFlatPattern(ModelDoc2 doc)
        {
            List<Feature> matches = new List<Feature>();
            Feature feature = doc.FirstFeature() as Feature; int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CollectFlatPatterns(feature, matches, 0);
                Feature next = feature.GetNextFeature() as Feature;
                if (!matches.Contains(feature)) Release(feature); feature = next;
            }
            Need(feature == null && matches.Count == 1, "FLAT_PATTERN_NOT_UNIQUE");
            Feature result = matches[0];
            foreach (Feature candidate in matches.Skip(1)) Release(candidate);
            return result;
        }

        private static void CollectFlatPatterns(Feature feature, List<Feature> matches,
            int depth)
        {
            Need(feature != null && depth <= 30, "FLAT_PATTERN_RECURSION_INVALID");
            if (String.Equals(feature.GetTypeName2(), "FlatPattern",
                StringComparison.Ordinal)) matches.Add(feature);
            Feature child = feature.GetFirstSubFeature() as Feature; int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CollectFlatPatterns(child, matches, depth + 1);
                Feature next = child.GetNextSubFeature() as Feature;
                if (!matches.Contains(child)) Release(child);
                child = next;
            }
            Need(child == null, "FLAT_PATTERN_CHILD_GUARD_EXCEEDED");
        }

        private static void CaptureFeatureRecursive(Feature feature,
            Dictionary<string, int> types, PartSnapshot snapshot, int depth)
        {
            Need(feature != null && depth <= 30, "FEATURE_RECURSION_INVALID");
            string type = feature.GetTypeName2() ?? "";
            if (!types.ContainsKey(type)) types[type] = 0;
            types[type]++;
            bool warning = false;
            int error = feature.GetErrorCode2(out warning);
            if (error != 0 || warning)
                snapshot.featureIssues.Add(type + ":" + (feature.Name ?? "") +
                    ":error=" + error.ToString(CultureInfo.InvariantCulture) +
                    ":warning=" + warning);
            if (type == "OneBend" || type == "ProcessBends" || type == "SheetMetal")
                snapshot.sheetMetalFeatures.Add(FeatureParameters(feature, type));
            Feature child = feature.GetFirstSubFeature() as Feature;
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureRecursive(child, types, snapshot, depth + 1);
                Feature next = child.GetNextSubFeature() as Feature;
                Release(child);
                child = next;
            }
            Need(child == null, "FEATURE_CHILD_GUARD_EXCEEDED");
        }

        private static Dictionary<string, object> FeatureParameters(Feature f, string type)
        {
            Dictionary<string, object> result = new Dictionary<string, object> { { "type", type }, { "name", f.Name } };
            object definition = null;
            CustomBendAllowance allowance = null;
            try
            {
                definition = f.GetDefinition();
                Need(definition != null, "FEATURE_DEFINITION_NULL_" + type);
                if (type == "OneBend")
                {
                    IOneBendFeatureData data = definition as IOneBendFeatureData;
                    Need(data != null, "ONE_BEND_DEFINITION_INVALID");
                    result["bendType"] = data.GetType();
                    result["angleRadians"] = data.BendAngle;
                    result["direction"] = data.BendDirection;
                    result["down"] = data.BendDown;
                    result["order"] = data.BendOrder;
                    result["radiusMm"] = data.BendRadius * 1000.0;
                    result["kFactor"] = data.KFactor;
                    result["allowanceMm"] = data.BendAllowance * 1000.0;
                    result["allowanceType"] = data.BendAllowanceType;
                    result["useDefaultRadius"] = data.UseDefaultBendRadius;
                    result["useDefaultAllowance"] = data.UseDefaultBendAllowance;
                    allowance = data.GetCustomBendAllowance();
                }
                else if (type == "ProcessBends")
                {
                    IBendsFeatureData data = definition as IBendsFeatureData;
                    Need(data != null, "PROCESS_BENDS_DEFINITION_INVALID");
                    result["radiusMm"] = data.BendRadius * 1000.0;
                    result["kFactor"] = data.KFactor;
                    result["allowanceMm"] = data.BendAllowance * 1000.0;
                    result["allowanceType"] = data.BendAllowanceType;
                    result["useDefaultRadius"] = data.UseDefaultBendRadius;
                    result["useDefaultAllowance"] = data.UseDefaultBendAllowance;
                    allowance = data.GetCustomBendAllowance();
                }
                else
                {
                    ISheetMetalFeatureData data = definition as ISheetMetalFeatureData;
                    Need(data != null, "SHEET_METAL_DEFINITION_INVALID");
                    result["thicknessMm"] = data.Thickness * 1000.0;
                    result["radiusMm"] = data.BendRadius * 1000.0;
                    result["kFactor"] = data.KFactor;
                    result["allowanceMm"] = data.BendAllowance * 1000.0;
                    result["allowanceType"] = data.BendAllowanceType;
                    allowance = data.GetCustomBendAllowance();
                }
                if (allowance != null)
                {
                    result["customAllowanceType"] = allowance.Type;
                    result["customKFactor"] = allowance.KFactor;
                    result["customAllowanceMm"] = allowance.BendAllowance * 1000.0;
                    result["customDeductionMm"] = allowance.BendDeduction * 1000.0;
                }
                return result;
            }
            finally { Release(allowance); Release(definition); }
        }

        private static List<string> ExternalReferences(ModelDoc2 doc)
        {
            List<string> result = new List<string>(); ModelDocExtension ext = doc.Extension;
            try { int count = ext.ListExternalFileReferencesCount(); for (int i=0; i<count; i++) result.Add("external_reference_" + i.ToString(CultureInfo.InvariantCulture)); return result; }
            finally { Release(ext); }
        }

        private static string GeometrySignature(PartSnapshot s)
        {
            List<string> bits = new List<string> { "b=" + String.Join(",", s.bboxMm.Select(Round)), "f=" + s.faceCount, "e=" + s.edgeCount, "l=" + s.loopCount, "le=" + s.loopEdgeReferenceCount };
            bits.AddRange(s.cylinders.Select(CylinderKey).OrderBy(v => v)); bits.AddRange(s.circleEdgeReferences.Select(CylinderKey).OrderBy(v => v));
            return ShaBytes(Encoding.UTF8.GetBytes(String.Join("|", bits)));
        }
        private static string CylinderKey(Dictionary<string, object> x) { return Json.Serialize(x); }
        private static string Round(double x) { return Math.Round(x, 6).ToString("0.######", CultureInfo.InvariantCulture); }

        // The detailed signature excludes global translation.  Cylinder centers are further
        // projected to the plane normal to the canonicalized cylinder axis, so shifting a
        // cylindrical face along its own axis does not change the comparison key.
        private static string DetailedGeometrySignature(PartSnapshot s)
        {
            List<double> anchor = new List<double> { s.bboxMm[0], s.bboxMm[1], s.bboxMm[2] };
            List<string> faces = s.faces.Select(face => DetailedFaceKey(face, anchor))
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
            return ShaBytes(Encoding.UTF8.GetBytes("faces=" + String.Join("|", faces)));
        }

        private static string DetailedFaceKey(DetailedFace face, List<double> anchor)
        {
            List<string> loopKeys = face.loops.Select(loop => (loop.isOuter ? "O" : "I") +
                ":" + String.Join(",", loop.edges.Select(edge => DetailedEdgeKey(edge, anchor))
                    .OrderBy(value => value, StringComparer.Ordinal))).OrderBy(value => value,
                    StringComparer.Ordinal).ToList();
            List<double> normal = CanonicalAxis(face.normal);
            string surface = "surface=" + face.surfaceIdentity.ToString(CultureInfo.InvariantCulture) +
                ",plane=" + face.isPlane +
                ",cylinder=" + DetailedCylinderKey(face.cylinderParams, anchor);
            return "n=" + String.Join(",", normal.Select(Round)) + ";" + surface + ";loops=" +
                String.Join("/", loopKeys);
        }

        private static string DetailedEdgeKey(DetailedEdge edge, List<double> anchor)
        {
            return "t=" + edge.curveType.ToString(CultureInfo.InvariantCulture) +
                ",l=" + edge.isLine + ",c=" + edge.isCircle + ",len=" + Round(edge.lengthMm) +
                ",p=" + PointKey(edge.startPointMm, anchor) + ">" + PointKey(edge.endPointMm, anchor) +
                ",v=" + PointKey(edge.startVertexPointMm, anchor) + ">" +
                PointKey(edge.endVertexPointMm, anchor) + ",cp=" +
                DetailedCylinderKey(edge.circleParams, anchor);
        }

        private static string DetailedCylinderKey(List<double> parameters, List<double> anchor)
        {
            if (parameters == null || parameters.Count < 7) return "[]";
            List<double> axis = CanonicalAxis(parameters.Skip(3).Take(3).ToList());
            List<double> center = new List<double> { parameters[0] * 1000.0 - anchor[0],
                parameters[1] * 1000.0 - anchor[1], parameters[2] * 1000.0 - anchor[2] };
            double parallel = center[0] * axis[0] + center[1] * axis[1] + center[2] * axis[2];
            List<double> perpendicular = new List<double> { center[0] - parallel * axis[0],
                center[1] - parallel * axis[1], center[2] - parallel * axis[2] };
            return String.Join(",", perpendicular.Select(Round)) + ";a=" +
                String.Join(",", axis.Select(Round)) + ";r=" + Round(parameters[6] * 1000.0);
        }

        private static List<double> CanonicalAxis(List<double> raw)
        {
            if (raw == null || raw.Count != 3 || raw.Any(value => !Finite(value))) return new List<double>();
            double length = Math.Sqrt(raw.Sum(value => value * value));
            if (!Finite(length) || length < 1e-12) return new List<double>();
            List<double> axis = raw.Select(value => value / length).ToList();
            double first = axis.First(value => Math.Abs(value) > 1e-12);
            if (first < 0) axis = axis.Select(value => -value).ToList();
            return axis;
        }

        private static string PointKey(List<double> point, List<double> anchor)
        {
            if (point == null || point.Count != 3) return "[]";
            return String.Join(",", new [] { point[0] - anchor[0], point[1] - anchor[1],
                point[2] - anchor[2] }.Select(Round));
        }

        private static List<double> EdgePoint(Curve curve, double parameter)
        {
            try { return FirstThree(curve.Evaluate(parameter)); }
            catch { return new List<double>(); }
        }

        private static List<double> VertexPoint(Vertex vertex)
        {
            try { return vertex == null ? new List<double>() : FirstThree(vertex.GetPoint()); }
            catch { return new List<double>(); }
            finally { Release(vertex); }
        }

        private static List<double> FirstThree(object raw)
        {
            try
            {
                List<double> points = ToList(raw, 1000.0);
                return points.Count < 3 || !points.Take(3).All(Finite) ? new List<double>() :
                    points.Take(3).ToList();
            }
            catch { return new List<double>(); }
        }

        private static List<double> ToListOrEmpty(object raw, double factor)
        {
            try { return ToList(raw, factor); }
            catch { return new List<double>(); }
        }

        private static bool SameMassAndVolume(PartSnapshot folded, PartSnapshot flat)
        {
            return folded != null && flat != null && Near(folded.massKg, flat.massKg,
                0.000001) && Near(folded.volumeMm3, flat.volumeMm3, 0.01);
        }

        private static bool DeltasMatch(PartPairSnapshot left, PartPairSnapshot right)
        {
            return Near(left.foldedMinusFlatVolumeMm3, right.foldedMinusFlatVolumeMm3,
                0.01) && Near(left.foldedMinusFlatMassKg, right.foldedMinusFlatMassKg,
                0.000001) && Near(left.foldedMinusFlatSurfaceAreaMm2,
                    right.foldedMinusFlatSurfaceAreaMm2, 0.01);
        }

        private static bool Near(double left, double right, double tolerance)
        {
            return Finite(left) && Finite(right) && Math.Abs(left - right) <= tolerance;
        }

        private static bool BboxSignificantlyDifferent(List<double> folded,
            List<double> flat)
        {
            return folded != null && flat != null && folded.Count == 6 && flat.Count == 6 &&
                folded.Zip(flat, (left, right) => Math.Abs(left - right)).Max() > 0.5;
        }

        private static ISldWorks StartOwned(OwnedSession session)
        {
            Need(StableZero(30000, 1000, 200), "CAD_NOT_EXCLUSIVE");
            ISldWorks app = null;
            try
            {
                app = Activator.CreateInstance(
                    Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks;
                Need(app != null, "SLDWORKS_ACTIVATION_FAILED");
                app.Visible = false;
                app.UserControl = false;
                app.CommandInProgress = true;
                int pid = app.GetProcessID();
                session.sldworks = ProcessIdentity.From(pid, "SLDWORKS");
                Need(session.sldworks != null &&
                    session.sldworks.Valid(ExpectedSolidWorksExePath,
                        ExpectedSolidWorksExeSha256) &&
                    String.Equals(session.sldworks.name, "SLDWORKS",
                        StringComparison.OrdinalIgnoreCase) &&
                    Processes("SLDWORKS").Select(value => value.pid).OrderBy(value => value)
                        .SequenceEqual(new[] { pid }),
                    "SLDWORKS_OWNERSHIP_OR_HASH_INVALID");
                Need((app.RevisionNumber() ?? "").StartsWith(ExpectedRevisionPrefix,
                    StringComparison.Ordinal), "SLDWORKS_COM_OR_REVISION_INVALID");
                Thread.Sleep(500);
                CaptureOwnedMonitors(session);
                return app;
            }
            catch
            {
                if (app != null)
                {
                    Try(() => app.CommandInProgress = false);
                    Try(() => app.ExitApp());
                    Release(app);
                }
                if (session.sldworks != null && session.sldworks.StillSame())
                    KillExact(session.sldworks, session);
                throw;
            }
        }

        private static void StopOwned(ISldWorks app, OwnedSession session)
        {
            CaptureOwnedMonitors(session);
            if (app != null) { Try(() => app.CloseAllDocuments(true)); Try(() => app.CommandInProgress = false); Try(() => app.ExitApp()); Release(app); }
            session.sldworksExited = WaitExit(session.sldworks, 30000);
            if (!session.sldworksExited && KillExact(session.sldworks, session))
                session.sldworksExited = WaitExit(session.sldworks, 3000);
            foreach (ProcessIdentity m in session.monitors)
            {
                m.exited = WaitExit(m, 10000);
                if (!m.exited && KillExact(m, session)) m.exited = WaitExit(m, 3000);
            }
            session.allExited = session.sldworksExited && session.monitors.All(m => m.exited) && StableZero(30000, 1000, 200);
            // Only exact PID/start/path/hash identities captured by this session are stopped.
        }

        private static void CaptureOwnedMonitors(OwnedSession session)
        {
            foreach (ProcessIdentity m in Processes("sldProcMon"))
            {
                bool parent = m.parentPid == session.sldworks.pid;
                bool ppid = Regex.IsMatch(m.commandLine ?? "", "(?:^|\\s)--ppid=" + session.sldworks.pid + "(?:\\s|$)");
                if (parent && ppid && m.Valid(ExpectedMonitorExePath, ExpectedMonitorExeSha256) && !session.monitors.Any(x => x.pid == m.pid && x.startTicks == m.startTicks)) session.monitors.Add(m);
            }
        }

        private static bool WaitExit(ProcessIdentity identity, int timeout)
        {
            if (identity == null) return false;
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeout); while (DateTime.UtcNow < end) { if (!identity.StillSame()) return true; Thread.Sleep(200); } return !identity.StillSame();
        }
        private static bool KillExact(ProcessIdentity identity, OwnedSession session)
        {
            if (identity == null || !identity.StillSame()) return false;
            try
            {
                using (Process process = Process.GetProcessById(identity.pid))
                {
                    ProcessIdentity live = ProcessIdentity.From(process.Id, identity.name);
                    if (live == null || live.startTicks != identity.startTicks ||
                        !Same(live.executablePath, identity.executablePath) ||
                        !String.Equals(live.executableSha256, identity.executableSha256,
                            StringComparison.OrdinalIgnoreCase)) return false;
                    process.Kill();
                    process.WaitForExit(3000);
                }
                session.forceStoppedProcessIds.Add(identity.pid);
                return true;
            }
            catch (ArgumentException) { return true; }
            catch { return false; }
        }
        private static bool StableZero(int timeout, int quietWindowMs, int pause)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeout);
            DateTime? emptySince = null;
            while (DateTime.UtcNow < end)
            {
                DateTime now = DateTime.UtcNow;
                if (Processes("SLDWORKS").Count == 0 &&
                    Processes("sldProcMon").Count == 0)
                {
                    if (!emptySince.HasValue) emptySince = now;
                    if ((now - emptySince.Value).TotalMilliseconds >= quietWindowMs)
                        return true;
                }
                else emptySince = null;
                Thread.Sleep(pause);
            }
            return false;
        }
        private static void ValidateToolchain()
        {
            Need(File.Exists(ExpectedSolidWorksExePath) && File.Exists(ExpectedMonitorExePath) && !HasReparse(ExpectedSolidWorksExePath) && !HasReparse(ExpectedMonitorExePath) && LinkCount(ExpectedSolidWorksExePath) == 1 && LinkCount(ExpectedMonitorExePath) == 1 && String.Equals(Sha(ExpectedSolidWorksExePath), ExpectedSolidWorksExeSha256, StringComparison.OrdinalIgnoreCase) && String.Equals(Sha(ExpectedMonitorExePath), ExpectedMonitorExeSha256, StringComparison.OrdinalIgnoreCase), "PINNED_TOOLCHAIN_INVALID");
        }

        private static int SelfTest()
        {
            Dictionary<string,bool> c = new Dictionary<string,bool>();
            c["mode_is_fixed"] = Mode == "left-side-760-and-1000-sharp-flat-detailed-v7";
            c["input_names_are_fixed"] = V37InputName == "v37_left_side.sldprt" && GoldInputName == "gold_left_side.sldprt";
            c["evidence_name_is_fixed"] = EvidenceName == "topcover_left_reference_brep_capture_v7.json";
            c["sharp_rollback_contract_present"] =
                typeof(PartPairSnapshot).GetField("sharpRollback") != null &&
                typeof(PartPairSnapshot).GetField("sheetMetalSelectionAccessed") != null;
            c["detailed_signature_contract_present"] = typeof(DetailedFace) != null &&
                typeof(DetailedLoop) != null && typeof(DetailedEdge) != null;
            c["toolchain_paths_and_hashes_pinned"] = ExpectedSolidWorksExePath.EndsWith("SLDWORKS.exe") && ExpectedMonitorExePath.EndsWith("sldProcMon.exe") && ExpectedSolidWorksExeSha256.Length == 64 && ExpectedMonitorExeSha256.Length == 64 && ExpectedRevisionPrefix == "28.";
            c["source_identity_valid"] = SourceIdentityValid();
            c["standard_json_rejects_nonfinite"] = StandardJson(new Dictionary<string,object>{{"x",1.0}}).Length > 0;
            c["single_hardlink_function_present"] = LinkCount(Assembly.GetExecutingAssembly().Location) >= 1;
            c["no_cad_started"] = Processes("SLDWORKS").Count == 0 && Processes("sldProcMon").Count == 0;
            int fail = c.Count(kv => !kv.Value); Console.WriteLine(Json.Serialize(new Dictionary<string,object>{{"status",fail==0?"PASS":"FAIL"},{"cadStarted",false},{"checksTotal",c.Count},{"checksFailed",fail},{"checks",c}})); return fail==0?0:1;
        }

        private static byte[] StandardJson(object value)
        {
            string text = Json.Serialize(value); Need(!Regex.IsMatch(text, "(?<![A-Za-z])(?:NaN|Infinity|-Infinity)(?![A-Za-z])"), "NONFINITE_JSON"); return new UTF8Encoding(false).GetBytes(text);
        }
        private static void WriteNewAndReread(string path, byte[] b, string attempt)
        {
            AssertNoReparseChain(Path.GetDirectoryName(path), attempt);
            using (FileStream f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(b,0,b.Length); f.Flush(true); }
            Need(File.ReadAllBytes(path).SequenceEqual(b) && LinkCount(path) == 1 &&
                !HasReparse(path), "EVIDENCE_REREAD_MISMATCH");
        }
        private static bool SourceIdentityValid()
        {
            string path = Assembly.GetExecutingAssembly().Location; string dir = Path.GetDirectoryName(path); string source = Path.Combine(dir, "TopCoverRightReferenceBrep.cs");
            if (!File.Exists(source)) return false; string t = File.ReadAllText(source, Encoding.UTF8).Replace("\r\n","\n").Replace("\r","\n");
            string norm = Regex.Replace(t, "(ExpectedSourceSha256\\s*=\\s*\\\")(?:__SOURCE_SHA256__|[A-F0-9]{64})(\\\";)", "$1__SOURCE_SHA256__$2"); return String.Equals(ShaBytes(new UTF8Encoding(false).GetBytes(norm)), ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase);
        }
        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
            for (int index = 0; index < 12 && current != null;
                index++, current = current.Parent)
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tools")))
                    return current.FullName;
            throw new ContractException("REPOSITORY_ROOT_NOT_FOUND");
        }
        private static List<ProcessIdentity> Processes(string name)
        {
            List<ProcessIdentity> r = new List<ProcessIdentity>(); foreach (ManagementObject o in new ManagementObjectSearcher("SELECT ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine,CreationDate FROM Win32_Process WHERE Name='" + name + ".exe'").Get()) { ProcessIdentity p = ProcessIdentity.FromWmi(o); if (p != null) r.Add(p); } return r;
        }
        private static List<double> ToList(object raw, double factor) { Array a=raw as Array; Need(a != null, "NUMERIC_ARRAY_NULL"); return a.Cast<object>().Select(x=>Convert.ToDouble(x,CultureInfo.InvariantCulture)*factor).ToList(); }
        private static bool Finite(double x) { return !Double.IsNaN(x) && !Double.IsInfinity(x); }
        private static void Release(object x) { if (x != null && Marshal.IsComObject(x)) try { Marshal.FinalReleaseComObject(x); } catch { } }
        private static bool Try(Action a) { try { a(); return true; } catch { return false; } }
        private static string Upper(string s) { return (s ?? "").ToUpperInvariant(); }
        private static string Full(string p) { return Path.GetFullPath(p ?? "").TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar); }
        private static bool Same(string a,string b) { return String.Equals(Full(a),Full(b),StringComparison.OrdinalIgnoreCase); }
        private static string Sha(string p) { using (SHA256 h=SHA256.Create()) using(FileStream f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.Read)) return BitConverter.ToString(h.ComputeHash(f)).Replace("-",""); }
        private static string ShaBytes(byte[] b) { using (SHA256 h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(b)).Replace("-",""); }
        private static bool HasReparse(string p) { try { return (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0; } catch { return true; } }
        private static void AssertNoReparseChain(string p,string stop) { string cur=Full(p), root=Full(stop); while(true) { Need(!HasReparse(cur),"REPARSE_POINT_IN_PATH"); if(Same(cur,root)) return; string parent=Path.GetDirectoryName(cur); Need(!String.IsNullOrEmpty(parent) && !Same(parent,cur),"PATH_OUTSIDE_ATTEMPT"); cur=parent; } }
        private static int LinkCount(string p) { using(FileStream f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) { ByHandleFileInformation i; Need(GetFileInformationByHandle(f.SafeFileHandle,out i),"LINKCOUNT_READ_FAILED"); return (int)i.nNumberOfLinks; } }
        private static void Need(bool ok,string m) { if(!ok) throw new ContractException(m); }
        private sealed class ContractException:Exception { public ContractException(string x):base(x){} }
        private sealed class Invocation { public string attempt,v37,v37Hash,gold,goldHash,output; }
        private sealed class Audit { public List<OwnedSession> sessions=new List<OwnedSession>(); public List<string> errors=new List<string>(); public bool AllExited { get { return sessions.All(x=>x.allExited); } } }
        private sealed class OwnedSession { public string label; public ProcessIdentity sldworks; public List<ProcessIdentity> monitors=new List<ProcessIdentity>(); public List<int> forceStoppedProcessIds=new List<int>(); public bool sldworksExited,allExited,inputUnchanged; }
        private sealed class PartSnapshot { public string label,path,expectedSha256,beforeSha256; public int openErrors,openWarnings,faceCount,edgeCount,loopCount,loopEdgeReferenceCount,cylinderFaceCount; public bool readOnly,rebuilt; public List<double> bboxMm; public double massKg,volumeMm3,surfaceAreaMm2; public List<Dictionary<string,object>> cylinders=new List<Dictionary<string,object>>(),circleEdgeReferences=new List<Dictionary<string,object>>(),sheetMetalFeatures=new List<Dictionary<string,object>>(); public List<DetailedFace> faces=new List<DetailedFace>(); public Dictionary<string,int> featureTypeCounts; public List<string> externalReferences; public List<string> featureIssues=new List<string>(); public string geometrySignature,translationNormalizedDetailedGeometrySignature; }
        private sealed class PartPairSnapshot { public PartSnapshot folded,sharpRollback,flat; public bool sheetMetalSelectionAccessed,sharpRollbackCaptured,sheetMetalSelectionReleased,sharpRollbackRestoreRebuild,flatPatternOriginallySuppressed,unsuppressReturn,flatPatternUnsuppressedAfterRebuild,restoreSuppressReturn,flatPatternSuppressedAfterRestore,foldedRestoreRebuild,massConserved,volumeConserved; public double foldedMinusFlatVolumeMm3,foldedMinusFlatMassKg,foldedMinusFlatSurfaceAreaMm2; public string sharpRollbackRestoredDetailedSignature,restoreLightweightSignature,restoreDetailedSignature; }
        private sealed class DetailedFace { public double areaMm2; public List<double> boxMm,normal,planeParams,cylinderParams; public int surfaceIdentity; public bool isPlane,isCylinder; public List<DetailedLoop> loops=new List<DetailedLoop>(); }
        private sealed class DetailedLoop { public bool isOuter; public int edgeCount; public List<DetailedEdge> edges=new List<DetailedEdge>(); }
        private sealed class DetailedEdge { public int curveType; public bool isLine,isCircle; public double uMin,uMax,lengthMm; public List<double> startPointMm,endPointMm,startVertexPointMm,endVertexPointMm,lineParams,circleParams; }
        private sealed class ProcessIdentity
        {
            public int pid,parentPid; public long startTicks; public string name,executablePath,executableSha256,commandLine; public bool exited;
            public static ProcessIdentity From(int id,string expected) { try { using(Process p=Process.GetProcessById(id)) return new ProcessIdentity {pid=id,name=p.ProcessName,executablePath=p.MainModule.FileName,startTicks=p.StartTime.ToUniversalTime().Ticks,executableSha256=Sha(p.MainModule.FileName)}; } catch { return null; } }
            public static ProcessIdentity FromWmi(ManagementObject o) { try { string e=Convert.ToString(o["ExecutablePath"]); int id=Convert.ToInt32((UInt32)o["ProcessId"]); using(Process p=Process.GetProcessById(id)) return new ProcessIdentity {pid=id,parentPid=Convert.ToInt32((UInt32)o["ParentProcessId"]),name=Convert.ToString(o["Name"]).Replace(".exe",""),executablePath=e,commandLine=Convert.ToString(o["CommandLine"]),startTicks=p.StartTime.ToUniversalTime().Ticks,executableSha256=String.IsNullOrEmpty(e)?"":Sha(e)}; } catch { return null; } }
            public bool Valid(string path,string hash) { return this!=null && Same(executablePath,path) && String.Equals(executableSha256,hash,StringComparison.OrdinalIgnoreCase); }
            public bool StillSame() { ProcessIdentity x=From(pid,name); return x!=null && x.startTicks==startTicks && Same(x.executablePath,executablePath) && String.Equals(x.executableSha256,executableSha256,StringComparison.OrdinalIgnoreCase); }
        }
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle f,out ByHandleFileInformation i);
        [StructLayout(LayoutKind.Sequential)] private struct ByHandleFileInformation { public uint dwFileAttributes; public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime,ftLastAccessTime,ftLastWriteTime; public uint dwVolumeSerialNumber,nFileSizeHigh,nFileSizeLow,nNumberOfLinks,nFileIndexHigh,nFileIndexLow; }
    }
}
