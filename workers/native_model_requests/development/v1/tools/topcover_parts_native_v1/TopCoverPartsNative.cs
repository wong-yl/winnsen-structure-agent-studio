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

namespace Winnsen.StructureAgent.TopCoverPartsNativeV1
{
    internal static class TopCoverPartsNative
    {
        private const string ToolId = "topcover_parts_native_v1";
        private const string Mode = "front-760-pilot";
        private const string RuleSchema = "winnsen.16029.topcover_front_native_rule.v2";
        private const string RuleRelativePath =
            "data/native_model_requests/attempts/NATIVE-20260824T101117-888W14D-3E50/" +
            "attempt-0001/evidence/private/topcover_front_native_rule_v2.json";
        private const string RuleSha256 =
            "15CDAA0B19189F3D6E41D7A277F15AA1A0CEF58CA304B6BE5E6B207F43B14553";
        private const string EvidenceFileName = "topcover_front_760_native_pilot.json";
        private const string ReceiptFileName = "topcover_front_760_native_pilot.json";
        private const string AuthorizationFileName = "topcover_parts_native_v1.json";
        private const string AuthorizationSchema =
            "winnsen.16029.topcover_front_native_authorization.v1";
        private const string ReceiptSchema =
            "winnsen.16029.topcover_front_native_receipt.v1";
        private const string PilotOperation = "create_new_front_760_native_pilot";
        private const string OutputRelativePath =
            "native_cad/front_760_native_pilot/上盖壳体前侧板.sldprt";
        private const string EvidenceRelativePath =
            "evidence/private/topcover_front_760_native_pilot.json";
        private const string ReceiptRelativePath =
            "receipts/topcover_front_760_native_pilot.json";
        private const string ExpectedSourceSha256 =
            "3E9677498266700A7CFCC80E9847E92B9BC912475F9BA6BC1BB8DAD6A4D7D15F";
        private const int PendingExitCode = 63;
        private const int ContractFailureExitCode = 64;
        private const int PilotFailureExitCode = 65;
        private const string ExpectedSolidWorksExePath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string ExpectedSolidWorksExeSha256 = "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string ExpectedMonitorExePath = @"D:\soildworks2020\SOLIDWORKS\sldProcMon.exe";
        private const string ExpectedMonitorExeSha256 = "A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70";
        private const string ExpectedSolidWorksVersionPrefix = "28.";
        private const double BoxToleranceMm = 0.02;
        private const double VolumeToleranceMm3 = 2.0;
        private const double AreaToleranceMm2 = 5.0;
        private const double MassToleranceKg = 0.00002;

        private static readonly string[] TopLevelKeys =
        {
            "schema", "generatedAtUtc", "purpose", "inputs", "sheetMetal",
            "flatMapping", "holePattern", "buildSpecs", "frontNativePilotReady",
            "nativeCadCreated", "qualityBoundary"
        };
        private static readonly string[] SpecKeys =
        {
            "cabinetWidthMm", "outline", "holes", "bendLines", "bends",
            "flatPlaneZmm", "flatBboxMm", "foldedBboxMm", "expectedTopology",
            "expectedMass"
        };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer
            { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };

        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                if (args.Length == 1 && string.Equals(args[0], "--help",
                        StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    return 0;
                }
                if (args.Length == 1 && string.Equals(args[0], "--identity",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Json.Serialize(new Dictionary<string, object>
                    {
                        { "toolId", ToolId },
                        { "sourceNormalizedSha256", ExpectedSourceSha256 },
                        { "cadImplemented", true },
                        { "ordinaryExitCode", 0 }
                    }));
                    return 0;
                }
                if (args.Length == 1 && string.Equals(args[0], "--self-test",
                        StringComparison.OrdinalIgnoreCase)) return RunSelfTests();

                Invocation invocation = ParseInvocation(args);
                string repositoryRoot = FindRepositoryRoot();
                ValidateInvocation(invocation, repositoryRoot, true);
                RuleContract contract = LoadAndValidateRule(invocation.rulePath,
                    repositoryRoot);
                Require(contract.widthMm == 760 && contract.outlineLineCount == 20 &&
                    contract.holeCount == 10 && contract.bendCount == 2,
                    "RULE_CONTRACT_SUMMARY_INVALID");
                Require(string.Equals(Sha256File(invocation.rulePath), RuleSha256,
                    StringComparison.OrdinalIgnoreCase), "RULE_CHANGED_DURING_VALIDATION");
                return RunNativePilot(invocation, contract, repositoryRoot);
            }
            catch (ContractException exception)
            {
                Console.Error.WriteLine("RULE_CONTRACT_FAILED: " + exception.Message);
                return ContractFailureExitCode;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("RULE_CONTRACT_FAILED: UNEXPECTED_" +
                    exception.GetType().Name);
                return ContractFailureExitCode;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("TopCoverPartsNative 760 front-plate native pilot");
            Console.WriteLine("  --help | --identity | --self-test");
            Console.WriteLine("  --mode front-760-pilot --rule <fixed-rule> " +
                "--attempt-dir <TOPCOVER-PILOT-.../attempt-0001> " +
                "--authorization <attempt/execution_authorizations/topcover_parts_native_v1.json> " +
                "--authorization-sha256 <SHA256> --out <private-json>");
        }

        private static int RunSelfTests()
        {
            Dictionary<string, bool> checks = new Dictionary<string, bool>(
                StringComparer.Ordinal);
            try
            {
                string repositoryRoot = FindRepositoryRoot();
                string rulePath = Path.Combine(repositoryRoot,
                    RuleRelativePath.Replace('/', Path.DirectorySeparatorChar));
                checks["fixed_rule_exists_and_is_single_link"] = File.Exists(rulePath) &&
                    FileLinkCount(rulePath) == 1 && !HasReparsePoint(rulePath);
                checks["fixed_rule_sha256"] = checks["fixed_rule_exists_and_is_single_link"] &&
                    string.Equals(Sha256File(rulePath), RuleSha256,
                        StringComparison.OrdinalIgnoreCase);
                RuleContract contract = LoadAndValidateRule(rulePath, repositoryRoot);
                checks["strict_schema_and_exact_keys"] = contract.exactKeys;
                checks["input_evidence_hashes_are_live"] = contract.inputBindingsValid;
                checks["outline_is_one_connected_20_line_cycle"] =
                    contract.outlineLineCount == 20 && contract.outlineVertexCount == 20;
                checks["outline_uses_exact_offset_plane_and_760_bbox"] =
                    Near(contract.flatPlaneZMm, -47.7, 0.000001) &&
                    Near(contract.flatWidthMm, 760, 0.000001) &&
                    Near(contract.flatHeightMm, 109.456199, 0.00001);
                checks["hole_pattern_is_exact_five_pairs"] = contract.holeCount == 10 &&
                    contract.smallHoleCount == 5 && contract.largeHoleCount == 5 &&
                    contract.holeX.SequenceEqual(new[] { -260.0, -120.0, 20.0, 160.0,
                        300.0 });
                checks["bend_lines_and_bend_sequence_are_exact"] =
                    contract.bendLineCount == 2 && contract.bendCount == 2 &&
                    contract.bendSequenceValid;
                checks["sheet_metal_contract_is_exact"] = contract.sheetMetalValid;
                checks["folded_bbox_mass_and_topology_are_exact"] =
                    contract.foldedAndMassValid && contract.topologyValid;
                checks["nonfinite_json_tokens_are_rejected"] =
                    !StandardJsonNumbersOnly("{\"x\":NaN}") &&
                    !StandardJsonNumbersOnly("{\"x\":Infinity}") &&
                    StandardJsonNumbersOnly("{\"x\":\"NaN,Infinity\",\"y\":1}");
                checks["attempt_shape_rejects_nonpilot_task"] = !SafeAttemptShape(
                    Path.Combine(repositoryRoot, "data", "native_model_requests", "attempts",
                        "NOT-A-PILOT", "attempt-0001"), repositoryRoot);
                checks["source_identity_is_normalized_and_embedded"] =
                    SourceIdentityValid(repositoryRoot);
                checks["runtime_requires_one_fixed_authorization"] =
                    AuthorizationFileName == "topcover_parts_native_v1.json" &&
                    ReceiptRelativePath == "receipts/topcover_front_760_native_pilot.json";
                checks["bend_line_mapping_is_geometry_proven"] =
                    BendLineSourceIndexForOrder(1) == 1 && BendLineSourceIndexForOrder(2) == 0;
                checks["runtime_tolerances_are_bounded"] = BoxToleranceMm <= 0.02 &&
                    VolumeToleranceMm3 <= 2.0 && AreaToleranceMm2 <= 5.0 &&
                    MassToleranceKg <= 0.00002;
            }
            catch
            {
                // Missing checks are added as false below.
            }
            string[] required =
            {
                "fixed_rule_exists_and_is_single_link", "fixed_rule_sha256",
                "strict_schema_and_exact_keys", "input_evidence_hashes_are_live",
                "outline_is_one_connected_20_line_cycle",
                "outline_uses_exact_offset_plane_and_760_bbox",
                "hole_pattern_is_exact_five_pairs",
                "bend_lines_and_bend_sequence_are_exact",
                "sheet_metal_contract_is_exact",
                "folded_bbox_mass_and_topology_are_exact",
                "nonfinite_json_tokens_are_rejected",
                "attempt_shape_rejects_nonpilot_task",
                "source_identity_is_normalized_and_embedded",
                "runtime_requires_one_fixed_authorization",
                "bend_line_mapping_is_geometry_proven",
                "runtime_tolerances_are_bounded"
            };
            foreach (string name in required)
                if (!checks.ContainsKey(name)) checks[name] = false;
            int failed = checks.Count(value => !value.Value);
            Console.WriteLine(Json.Serialize(new Dictionary<string, object>
            {
                { "status", failed == 0 ? "PASS" : "FAIL" },
                { "cadStarted", false },
                { "checksTotal", checks.Count },
                { "checksFailed", failed },
                { "checks", checks }
            }));
            return failed == 0 ? 0 : 1;
        }

        private static Invocation ParseInvocation(string[] args)
        {
            Require(args.Length == 12 && args[0] == "--mode" && args[1] == Mode &&
                args[2] == "--rule" && args[4] == "--attempt-dir" &&
                args[6] == "--authorization" && args[8] == "--authorization-sha256" &&
                args[10] == "--out",
                "ARGUMENT_GRAMMAR_INVALID");
            return new Invocation
            {
                rulePath = FullPath(args[3]),
                attemptDirectory = FullPath(args[5]),
                authorizationPath = FullPath(args[7]),
                authorizationSha256 = args[9].ToUpperInvariant(),
                outputPath = FullPath(args[11])
            };
        }

        private static void ValidateInvocation(Invocation value, string repositoryRoot,
            bool requireExistingAttempt)
        {
            string expectedRule = Path.Combine(repositoryRoot,
                RuleRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Require(SamePath(value.rulePath, expectedRule), "RULE_PATH_NOT_FIXED");
            Require(File.Exists(value.rulePath) && FileLinkCount(value.rulePath) == 1 &&
                !HasReparsePoint(value.rulePath) &&
                string.Equals(Sha256File(value.rulePath), RuleSha256,
                    StringComparison.OrdinalIgnoreCase), "RULE_FILE_IDENTITY_INVALID");
            Require(SafeAttemptShape(value.attemptDirectory, repositoryRoot),
                "ATTEMPT_SHAPE_INVALID");
            if (requireExistingAttempt)
            {
                Require(Directory.Exists(value.attemptDirectory), "ATTEMPT_DIRECTORY_MISSING");
                AssertPathChainNoReparse(value.attemptDirectory,
                    Path.Combine(repositoryRoot, "data", "native_model_requests", "attempts"));
            }
            string expectedOutput = Path.Combine(value.attemptDirectory, "evidence", "private",
                EvidenceFileName);
            Require(SamePath(value.outputPath, expectedOutput) && !File.Exists(value.outputPath),
                "OUTPUT_PATH_INVALID");
            string expectedAuthorization = Path.Combine(value.attemptDirectory,
                "execution_authorizations", AuthorizationFileName);
            Require(SamePath(value.authorizationPath, expectedAuthorization) &&
                Regex.IsMatch(value.authorizationSha256 ?? "", "^[A-F0-9]{64}$",
                    RegexOptions.CultureInvariant) && File.Exists(value.authorizationPath) &&
                FileLinkCount(value.authorizationPath) == 1 &&
                !HasReparsePoint(value.authorizationPath) &&
                string.Equals(Sha256File(value.authorizationPath), value.authorizationSha256,
                    StringComparison.OrdinalIgnoreCase), "AUTHORIZATION_PATH_OR_HASH_INVALID");
            string receiptPath = Path.Combine(value.attemptDirectory, "receipts", ReceiptFileName);
            Require(!File.Exists(receiptPath), "RECEIPT_PATH_ALREADY_EXISTS");
            if (requireExistingAttempt)
            {
                string parent = Path.GetDirectoryName(value.outputPath);
                Require(Directory.Exists(parent), "OUTPUT_PARENT_MISSING");
                AssertPathChainNoReparse(parent, value.attemptDirectory);
                AssertPathChainNoReparse(value.authorizationPath, value.attemptDirectory);
                string receiptParent = Path.GetDirectoryName(receiptPath);
                Require(Directory.Exists(receiptParent), "RECEIPT_PARENT_MISSING");
                AssertPathChainNoReparse(receiptParent, value.attemptDirectory);
            }
        }

        // This pilot is fail-closed: it saves one new attempt-local part only after native
        // sheet-metal construction, then commits evidence and receipt with CreateNew writes.
        private static int RunNativePilot(Invocation invocation, RuleContract contract,
            string repositoryRoot)
        {
            string partDirectory = Path.Combine(invocation.attemptDirectory, "native_cad",
                "front_760_native_pilot");
            string partPath = Path.Combine(partDirectory, "上盖壳体前侧板.sldprt");
            string receiptPath = Path.Combine(invocation.attemptDirectory, "receipts",
                ReceiptFileName);
            string executableSha256 = Sha256File(Assembly.GetExecutingAssembly().Location);
            AuthorizationBinding authorization = LoadAndValidateAuthorization(invocation,
                executableSha256);
            ProcessAudit processAudit = new ProcessAudit();
            CadSession session = null;
            ISldWorks application = null;
            ModelDoc2 model = null;
            bool partDirectoryCreated = false;
            bool evidenceCreated = false;
            bool receiptCreated = false;
            string evidenceCreatedSha256 = "";
            string receiptCreatedSha256 = "";
            Dictionary<string, object> evidence = new Dictionary<string, object>
            {
                { "schema", "winnsen.16029.topcover_front_native_pilot.v3" },
                { "mode", Mode }, { "purpose", "structure_engineering_assistance" },
                { "consumable", false }, { "pilotPass", false },
                { "rulePath", RelativePath(repositoryRoot, invocation.rulePath).Replace('\\', '/') },
                { "ruleSha256", RuleSha256 },
                { "authorizationId", authorization.authorizationId },
                { "authorizationPath", RelativePath(invocation.attemptDirectory,
                    invocation.authorizationPath).Replace('\\', '/') },
                { "authorizationSha256", invocation.authorizationSha256 },
                { "toolId", ToolId }, { "toolSourceNormalizedSha256", ExpectedSourceSha256 },
                { "toolExecutableSha256", executableSha256 },
                { "outputPath", partPath },
                { "qualityBoundary", "760 front-plate native pilot regression only; structural engineer review remains required." },
                { "buildStages", new List<Dictionary<string, object>>() }
            };
            try
            {
                Require(SourceIdentityValid(repositoryRoot), "TOOL_SOURCE_IDENTITY_INVALID");
                Require(!Directory.Exists(partDirectory) && !File.Exists(partPath),
                    "NATIVE_OUTPUT_MUST_BE_NEW");
                ValidateToolchainFiles();
                Require(WaitForGlobalCadQuiescence(30000, 1000, 200),
                    "CAD_PROCESS_BASELINE_NOT_STABLY_ZERO");
                Directory.CreateDirectory(partDirectory);
                partDirectoryCreated = true;
                AssertPathChainNoReparse(partDirectory, invocation.attemptDirectory);

                session = new CadSession { purpose = "native_build" };
                processAudit.sessions.Add(session);
                application = StartOwnedSolidWorks(processAudit, session);
                string template = application.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                Require(File.Exists(template) && FileLinkCount(template) == 1 &&
                    !HasReparsePoint(template), "PART_TEMPLATE_INVALID");
                evidence["templatePath"] = template;
                evidence["templateSha256"] = Sha256File(template);
                model = application.NewDocument(template, 0, 0, 0) as ModelDoc2;
                Require(model != null, "NATIVE_NEW_PART_FAILED");

                BuildNativeFront(application, model, invocation.rulePath, contract, evidence);
                GeometrySnapshot liveGeometry = CaptureGeometry(model);
                FeatureSnapshot liveFeatures = CaptureFeatureSnapshot(model);
                bool liveGeometryPass = GeometryMatchesContract(liveGeometry, contract);
                bool liveFeaturePass = FeatureMatchesContract(liveFeatures);
                evidence["liveGeometryDiagnostic"] = liveGeometry;
                evidence["liveFeaturesDiagnostic"] = liveFeatures;
                evidence["liveGeometryPass"] = liveGeometryPass;
                evidence["liveFeaturePass"] = liveFeaturePass;
                Require(liveGeometryPass,
                    "LIVE_GEOMETRY_REGRESSION_FAILED");
                Require(liveFeaturePass,
                    "LIVE_SHEET_METAL_FEATURE_REGRESSION_FAILED");
                Require(model.Extension.ListExternalFileReferencesCount() == 0,
                    "LIVE_EXTERNAL_REFERENCE_FOUND");
                evidence["liveGeometry"] = liveGeometry;
                evidence["liveFeatures"] = liveFeatures;

                int errors = 0;
                int warnings = 0;
                Require(model.Extension.SaveAs3(partPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null,
                    ref errors, ref warnings) && errors == 0 && warnings == 0 &&
                    !model.GetSaveFlag(), "NATIVE_SAVE_FAILED");
                evidence["saveErrors"] = errors;
                evidence["saveWarnings"] = warnings;

                CloseDocument(application, ref model);
                CloseOwnedSolidWorks(ref application, processAudit, session);
                Require(session.ExitProven && WaitForGlobalCadQuiescence(30000, 1000, 200),
                    "BUILD_CAD_SESSION_EXIT_NOT_PROVEN");
                Require(File.Exists(partPath) && FileLinkCount(partPath) == 1 &&
                    !HasReparsePoint(partPath), "NATIVE_SAVED_FILE_IDENTITY_INVALID");
                string partSha256 = Sha256File(partPath);
                evidence["outputSha256"] = partSha256;

                FreshVerification fresh = FreshReadOnlyVerify(partPath, contract, processAudit);
                evidence["freshReadOnlyVerification"] = fresh;
                Require(fresh.pass, "FRESH_READONLY_REGRESSION_FAILED");
                Require(string.Equals(Sha256File(invocation.rulePath), RuleSha256,
                    StringComparison.OrdinalIgnoreCase), "RULE_CHANGED_AFTER_NATIVE_PILOT");
                ValidateAuthorizationStillLive(invocation, authorization, executableSha256);
                Require(WaitForGlobalCadQuiescence(30000, 1000, 200),
                    "CAD_PROCESS_FINAL_NOT_STABLY_ZERO");
                processAudit.finalSldworks = ProcessIds("SLDWORKS");
                processAudit.finalMonitors = ProcessIds("sldProcMon");
                processAudit.finalGate = processAudit.finalSldworks.Count == 0 &&
                    processAudit.finalMonitors.Count == 0 &&
                    processAudit.sessions.All(value => value.ExitProven) &&
                    processAudit.cleanupErrors.Count == 0;
                Require(processAudit.finalGate, "CAD_PROCESS_FINAL_GATE_FAILED");

                string completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                evidence["processAudit"] = processAudit;
                evidence["completedAtUtc"] = completedAtUtc;
                evidence["pilotPass"] = true;
                evidence["status"] = "TOPCOVER_FRONT_760_NATIVE_PILOT_COMPLETE";
                byte[] evidenceBytes = StandardJsonBytes(evidence);
                WriteCreateNewAndReread(invocation.outputPath, evidenceBytes,
                    invocation.attemptDirectory);
                evidenceCreated = true;
                evidenceCreatedSha256 = Sha256Bytes(evidenceBytes);

                Dictionary<string, object> receipt = BuildPilotReceipt(invocation,
                    authorization, executableSha256, partSha256, evidenceCreatedSha256,
                    completedAtUtc);
                byte[] receiptBytes = StandardJsonBytes(receipt);
                WriteCreateNewAndReread(receiptPath, receiptBytes,
                    invocation.attemptDirectory);
                receiptCreated = true;
                receiptCreatedSha256 = Sha256Bytes(receiptBytes);
                Require(PilotEvidenceReceiptPairValid(invocation.outputPath, receiptPath,
                    partPath, evidenceCreatedSha256, receiptCreatedSha256),
                    "PILOT_EVIDENCE_RECEIPT_PAIR_INVALID");
                Console.WriteLine(new UTF8Encoding(false).GetString(evidenceBytes).TrimEnd());
                return 0;
            }
            catch (Exception exception)
            {
                CloseDocument(application, ref model);
                if (session != null) CloseOwnedSolidWorks(ref application, processAudit, session);
                else Release(application);
                application = null;
                bool cadZero = WaitForGlobalCadQuiescence(30000, 1000, 200);
                bool receiptRolledBack = RollbackOwnedFile(receiptPath, receiptCreated,
                    receiptCreatedSha256, invocation.attemptDirectory);
                bool evidenceRolledBack = RollbackOwnedFile(invocation.outputPath,
                    evidenceCreated, evidenceCreatedSha256, invocation.attemptDirectory);
                bool outputRolledBack = cadZero && RollbackOwnedPartDirectory(partDirectory,
                    partPath, partDirectoryCreated, invocation.attemptDirectory);
                evidence["status"] = "TOPCOVER_FRONT_760_NATIVE_PILOT_FAILED";
                evidence["failure"] = SafeException(exception);
                evidence["cadZeroAfterFailure"] = cadZero;
                evidence["receiptRolledBack"] = receiptRolledBack;
                evidence["evidenceRolledBack"] = evidenceRolledBack;
                evidence["outputRolledBack"] = outputRolledBack;
                evidence["processAudit"] = processAudit;
                evidence["completedAtUtc"] = DateTime.UtcNow.ToString("o",
                    CultureInfo.InvariantCulture);
                try
                {
                    if (!File.Exists(invocation.outputPath))
                        WriteCreateNewAndReread(invocation.outputPath,
                            StandardJsonBytes(evidence), invocation.attemptDirectory);
                }
                catch (Exception writeException)
                {
                    Console.Error.WriteLine("FAILURE_EVIDENCE_WRITE_FAILED: " +
                        SafeException(writeException));
                }
                Console.Error.WriteLine("TOPCOVER_FRONT_760_NATIVE_PILOT_FAILED: " +
                    SafeException(exception));
                return PilotFailureExitCode;
            }
            finally
            {
                CloseDocument(application, ref model);
                Release(application);
            }
        }

        private static void BuildNativeFront(ISldWorks application, ModelDoc2 model,
            string rulePath, RuleContract contract, Dictionary<string, object> evidence)
        {
            Dictionary<string, object> root = ObjectValue(Json.DeserializeObject(
                File.ReadAllText(rulePath, new UTF8Encoding(false, true))));
            Dictionary<string, object> spec = ObjectValue(Child(root, "buildSpecs")
                ["width760Pilot"]);
            MathUtility math = application.GetMathUtility() as MathUtility;
            Feature plane = null;
            Feature baseFlange = null;
            Feature cut = null;
            CustomBendAllowance allowance = null;
            try
            {
                Require(math != null, "MATH_UTILITY_UNAVAILABLE");
                string frontPlaneName;
                List<double> frontPlaneTransform;
                Require(SelectDefaultFrontPlane(model, out frontPlaneName,
                    out frontPlaneTransform), "FRONT_PLANE_SELECT_FAILED");
                evidence["frontPlaneName"] = frontPlaneName;
                evidence["frontPlaneTransform16"] = frontPlaneTransform;
                double baseSketchPlaneZMm = contract.flatPlaneZMm + 0.8;
                evidence["baseSketchPlaneZmm"] = baseSketchPlaneZMm;
                plane = model.FeatureManager.InsertRefPlane(
                    (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance |
                    (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_OptionFlip,
                    Math.Abs(baseSketchPlaneZMm) / 1000.0, 0, 0, 0, 0) as Feature;
                Require(plane != null, "OFFSET_PLANE_CREATE_FAILED");
                List<double> offsetPlaneTransform = RefPlaneTransform(plane);
                evidence["offsetPlaneTransform16"] = offsetPlaneTransform;
                Require(offsetPlaneTransform.Count >= 12 &&
                    Near(offsetPlaneTransform[9], 0, 0.000001) &&
                    Near(offsetPlaneTransform[10], 0, 0.000001) &&
                    Near(offsetPlaneTransform[11], baseSketchPlaneZMm / 1000.0,
                        0.000001), "OFFSET_PLANE_TRANSFORM_INVALID");
                Require(plane.Select2(false, 0), "OFFSET_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (object row in ArrayValue(spec, "outline"))
                    {
                        Dictionary<string, object> line = ObjectValue(row);
                        Point3 start = ToActiveSketchPoint(model, math,
                            Point(ArrayValue(line, "startMm")));
                        Point3 end = ToActiveSketchPoint(model, math,
                            Point(ArrayValue(line, "endMm")));
                        Require(model.SketchManager.CreateLine(start.x, start.y, 0,
                            end.x, end.y, 0) != null, "NATIVE_OUTLINE_LINE_FAILED");
                    }
                }
                finally
                {
                    TryAction(() => model.SetDisplayWhenAdded(true));
                    TryAction(() => model.SetAddToDB(false));
                }
                allowance = model.FeatureManager.CreateCustomBendAllowance();
                Require(allowance != null, "NATIVE_BASE_ALLOWANCE_FAILED");
                allowance.Type = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
                allowance.KFactor = 0.5;
                baseFlange = model.FeatureManager.InsertSheetMetalBaseFlange2(
                    0.0008, true, 0.0002, 0.02, 0.01, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind, 1, allowance, false,
                    (int)swSheetMetalReliefTypes_e.swSheetMetalReliefTear,
                    0.0001, 0.0001, 0.5, true, false, true, true);
                Require(baseFlange != null, "NATIVE_BASE_FLANGE_INSERT_FAILED");
                Require(model.ForceRebuild3(false), "NATIVE_BASE_FLANGE_REBUILD_FAILED");
                GeometrySnapshot baseGeometry = CaptureGeometry(model);
                evidence["baseGeometryDiagnostic"] = baseGeometry;
                Require(FlatEnvelopeMatches(baseGeometry, contract),
                    "NATIVE_BASE_FLANGE_ENVELOPE_FAILED");
                AddBuildStage(evidence, "base_flange", baseGeometry);

                Require(plane.Select2(false, 0), "HOLE_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (object raw in ArrayValue(spec, "holes"))
                    {
                        Dictionary<string, object> hole = ObjectValue(raw);
                        Point3 center = ToActiveSketchPoint(model, math, new Point3(
                            Number(hole, "xMm"), Number(hole, "yMm"),
                            baseSketchPlaneZMm));
                        Require(model.SketchManager.CreateCircleByRadius(center.x, center.y, 0,
                            Number(hole, "diameterMm") / 2000.0) != null,
                            "NATIVE_HOLE_CIRCLE_FAILED");
                    }
                }
                finally
                {
                    TryAction(() => model.SetDisplayWhenAdded(true));
                    TryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                Feature holeSketch = LastRootFeatureOfType(model, "ProfileFeature");
                evidence["holeSketchFeatureName"] = holeSketch == null ? "" :
                    holeSketch.Name ?? "";
                Require(holeSketch != null && holeSketch.Select2(false, 0),
                    "NATIVE_HOLE_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, false,
                    (int)swEndConditions_e.swEndCondThroughAll,
                    (int)swEndConditions_e.swEndCondThroughAll, 0, 0,
                    false, false, false, false, 0, 0, false, false, false, false,
                    true, true, true, false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0, false);
                Release(holeSketch);
                Require(cut != null && model.ForceRebuild3(false),
                    "NATIVE_THROUGH_ALL_CUT_FAILED");
                GeometrySnapshot cutGeometry = CaptureGeometry(model);
                evidence["cutGeometryDiagnostic"] = cutGeometry;
                Require(FlatHoleGeometryMatches(cutGeometry, spec, contract),
                    "NATIVE_FLAT_HOLE_GEOMETRY_FAILED");
                AddBuildStage(evidence, "through_all_holes", cutGeometry);

                List<Dictionary<string, object>> bends = ArrayValue(spec, "bends")
                    .Select(ObjectValue).OrderBy(value => Number(value, "order")).ToList();
                foreach (Dictionary<string, object> bend in bends)
                {
                    int order = (int)Number(bend, "order");
                    int sourceIndex = BendLineSourceIndexForOrder(order);
                    Dictionary<string, object> bendLine = ArrayValue(spec, "bendLines")
                        .Select(ObjectValue).Single(value =>
                            (int)Number(value, "sourceIndex") == sourceIndex);
                    Dictionary<string, object> bendDiagnostic =
                        new Dictionary<string, object>
                        {
                            { "order", order }, { "lineSourceIndex", sourceIndex },
                            { "sketchFaceSelected", false }, { "lineCreated", false },
                            { "lineSelected", false }, { "fixedFaceSelected", false },
                            { "lineMarkCount", 0 }, { "faceMarkCount", 0 },
                            { "apiReturnedFeature", false }
                        };
                    evidence["activeBendDiagnostic"] = bendDiagnostic;
                    CreateNativeBend(model, math, bendLine, bend, order,
                        bendDiagnostic);
                    FeatureSnapshot stageFeatures = CaptureFeatureSnapshot(model);
                    stageFeatures.pass = stageFeatures.oneBendCount == order &&
                        stageFeatures.issues.Count == 0;
                    Require(stageFeatures.pass,
                        "NATIVE_BEND_COUNT_AFTER_ORDER_" + order.ToString(
                            CultureInfo.InvariantCulture));
                    AddBuildStage(evidence, "native_bend_" + order.ToString(
                        CultureInfo.InvariantCulture), stageFeatures);
                }
                Require(model.ForceRebuild3(false), "NATIVE_FINAL_REBUILD_FAILED");
            }
            finally
            {
                Release(cut);
                Release(baseFlange);
                Release(allowance);
                Release(plane);
                Release(math);
            }
        }

        private static int BendLineSourceIndexForOrder(int order)
        {
            // The captured sharp-bend child sketch orders y=1894.528761 before
            // y=1916.856861. Folded geometry proves the latter is the 180-degree return.
            if (order == 1) return 1;
            if (order == 2) return 0;
            throw new ContractException("NATIVE_BEND_ORDER_INVALID");
        }

        private static bool SelectDefaultFrontPlane(ModelDoc2 model, out string planeName,
            out List<double> transformValues)
        {
            planeName = "";
            transformValues = new List<double>();
            Feature feature = model.FirstFeature() as Feature;
            Feature selected = null;
            List<double> selectedTransform = null;
            int guard = 0;
            try
            {
                while (feature != null && guard++ < 200)
                {
                    if (string.Equals(feature.GetTypeName2(), "RefPlane",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        RefPlane plane = feature.GetSpecificFeature2() as RefPlane;
                        MathTransform transform = null;
                        try
                        {
                            transform = plane == null ? null : plane.Transform;
                            List<double> values = transform == null ?
                                new List<double>() : DoubleList(transform.ArrayData);
                            bool origin = values.Count >= 12 &&
                                Near(values[9], 0, 1e-10) && Near(values[10], 0, 1e-10) &&
                                Near(values[11], 0, 1e-10);
                            bool normalZ = values.Count >= 9 &&
                                Math.Abs(values[8]) > 0.999999 &&
                                Math.Abs(values[6]) < 0.000001 &&
                                Math.Abs(values[7]) < 0.000001;
                            if (origin && normalZ)
                            {
                                Require(selected == null,
                                    "MULTIPLE_DEFAULT_FRONT_PLANES_FOUND");
                                selected = feature;
                                selectedTransform = values;
                                feature = null;
                                break;
                            }
                        }
                        finally { Release(transform); Release(plane); }
                    }
                    Feature next = feature.GetNextFeature() as Feature;
                    Release(feature);
                    feature = next;
                }
                if (selected == null || selectedTransform == null) return false;
                planeName = selected.Name ?? "";
                transformValues = selectedTransform;
                return selected.Select2(false, 0);
            }
            finally
            {
                Release(feature);
                Release(selected);
            }
        }

        private static List<double> RefPlaneTransform(Feature feature)
        {
            RefPlane plane = feature == null ? null : feature.GetSpecificFeature2() as RefPlane;
            MathTransform transform = null;
            try
            {
                transform = plane == null ? null : plane.Transform;
                return transform == null ? new List<double>() :
                    DoubleList(transform.ArrayData);
            }
            finally { Release(transform); Release(plane); }
        }

        private static Feature LastRootFeatureOfType(ModelDoc2 model, string typeName)
        {
            Feature feature = model.FirstFeature() as Feature;
            Feature output = null;
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                Feature next = feature.GetNextFeature() as Feature;
                if (string.Equals(feature.GetTypeName2(), typeName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Release(output);
                    output = feature;
                }
                else Release(feature);
                feature = next;
            }
            Require(feature == null, "LAST_ROOT_FEATURE_TRAVERSAL_GUARD_EXCEEDED");
            return output;
        }

        private static void CreateNativeBend(ModelDoc2 model, MathUtility math,
            Dictionary<string, object> line, Dictionary<string, object> bend, int order,
            Dictionary<string, object> diagnostic)
        {
            SketchSegment segment = null;
            SelectData lineSelection = null;
            Feature created = null;
            try
            {
                bool sketchFaceSelected = SelectFixedFaceByRay(model, false, 0);
                diagnostic["sketchFaceSelected"] = sketchFaceSelected;
                Require(sketchFaceSelected, "NATIVE_BEND_SKETCH_FACE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    Point3 start = ToActiveSketchPoint(model, math,
                        Point(ArrayValue(line, "startMm")));
                    Point3 end = ToActiveSketchPoint(model, math,
                        Point(ArrayValue(line, "endMm")));
                    segment = model.SketchManager.CreateLine(start.x, start.y, 0,
                        end.x, end.y, 0) as SketchSegment;
                }
                finally
                {
                    TryAction(() => model.SetDisplayWhenAdded(true));
                    TryAction(() => model.SetAddToDB(false));
                }
                diagnostic["lineCreated"] = segment != null;
                Require(segment != null, "NATIVE_BEND_LINE_CREATE_FAILED");
                model.SketchManager.InsertSketch(true);
                model.ClearSelection2(true);
                SelectionMgr selection = model.SelectionManager as SelectionMgr;
                Require(selection != null, "NATIVE_BEND_SELECTION_MANAGER_MISSING");
                lineSelection = selection.CreateSelectData();
                Require(lineSelection != null, "NATIVE_BEND_SELECT_DATA_MISSING");
                lineSelection.Mark = 0;
                bool lineSelected = segment.Select4(false, lineSelection);
                diagnostic["lineSelected"] = lineSelected;
                Require(lineSelected, "NATIVE_BEND_LINE_SELECT_FAILED");
                bool fixedFaceSelected = SelectFixedFaceByRay(model, true, 1);
                diagnostic["fixedFaceSelected"] = fixedFaceSelected;
                Require(fixedFaceSelected, "NATIVE_BEND_FIXED_FACE_SELECT_FAILED");
                int lineMarkCount = selection.GetSelectedObjectCount2(0);
                int faceMarkCount = selection.GetSelectedObjectCount2(1);
                diagnostic["lineMarkCount"] = lineMarkCount;
                diagnostic["faceMarkCount"] = faceMarkCount;
                Require(lineMarkCount == 1 && faceMarkCount == 1,
                    "NATIVE_BEND_SELECTION_MARKS_INVALID");
                bool useDefaultRadius = order == 1;
                created = model.FeatureManager.InsertSheetMetal3dBend(
                    Number(bend, "angleRadians"), useDefaultRadius,
                    Number(bend, "radiusMm") / 1000.0, Bool(bend, "down"),
                    (short)0, null);
                diagnostic["apiReturnedFeature"] = created != null;
                diagnostic["featureErrorCode"] = created == null ? -1 :
                    created.GetErrorCode();
                Require(created != null && created.GetErrorCode() == 0,
                    "NATIVE_SKETCHED_BEND_FAILED");
                created.Name = order == 1 ? "native_sharp_bend_1" :
                    "native_round_return_bend_2";
                Require(model.ForceRebuild3(false), "NATIVE_BEND_REBUILD_FAILED");
                model.ClearSelection2(true);
            }
            finally
            {
                Release(created);
                Release(lineSelection);
                Release(segment);
            }
        }

        private static Point3 ToActiveSketchPoint(ModelDoc2 model, MathUtility math,
            Point3 modelPointMm)
        {
            Sketch sketch = model.SketchManager.ActiveSketch;
            MathTransform transform = null;
            MathPoint modelPoint = null;
            MathPoint sketchPoint = null;
            try
            {
                Require(sketch != null, "ACTIVE_SKETCH_MISSING");
                transform = sketch.ModelToSketchTransform;
                Require(transform != null, "MODEL_TO_SKETCH_TRANSFORM_MISSING");
                modelPoint = math.CreatePoint(new double[]
                {
                    modelPointMm.x / 1000.0,
                    modelPointMm.y / 1000.0,
                    modelPointMm.z / 1000.0
                }) as MathPoint;
                Require(modelPoint != null, "MODEL_MATH_POINT_CREATE_FAILED");
                sketchPoint = modelPoint.MultiplyTransform(transform) as MathPoint;
                double[] values = sketchPoint == null ? null :
                    sketchPoint.ArrayData as double[];
                Require(values != null && values.Length >= 3 && values.All(IsFinite),
                    "SKETCH_POINT_TRANSFORM_FAILED");
                return new Point3(values[0], values[1], values[2]);
            }
            finally
            {
                Release(sketchPoint);
                Release(modelPoint);
                Release(transform);
                Release(sketch);
            }
        }

        private static bool SelectFixedFaceByRay(ModelDoc2 model, bool append, int mark)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                return extension != null && extension.SelectByRay(0, 1.905692811, -0.1,
                    0, 0, 1, 0.001, (int)swSelectType_e.swSelFACES, append, mark, 0);
            }
            finally { Release(extension); }
        }

        private static FreshVerification FreshReadOnlyVerify(string partPath,
            RuleContract contract, ProcessAudit processAudit)
        {
            FreshVerification output = new FreshVerification();
            string beforeSha256 = Sha256File(partPath);
            ISldWorks application = null;
            ModelDoc2 model = null;
            CadSession session = new CadSession { purpose = "fresh_read_only_regression" };
            processAudit.sessions.Add(session);
            try
            {
                Require(WaitForGlobalCadQuiescence(30000, 1000, 200),
                    "FRESH_CAD_BASELINE_NOT_STABLY_ZERO");
                application = StartOwnedSolidWorks(processAudit, session);
                int errors = 0;
                int warnings = 0;
                model = application.OpenDoc6(partPath, (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref errors,
                    ref warnings) as ModelDoc2;
                output.openErrors = errors;
                output.openWarnings = warnings;
                Require(model != null && errors == 0 && warnings == 0,
                    "FRESH_READONLY_OPEN_FAILED");
                output.readOnly = model.IsOpenedReadOnly();
                Require(output.readOnly, "FRESH_DOCUMENT_NOT_READ_ONLY");
                output.rebuilt = model.ForceRebuild3(false);
                Require(output.rebuilt, "FRESH_REBUILD_FAILED");
                output.externalReferenceCount =
                    model.Extension.ListExternalFileReferencesCount();
                output.geometry = CaptureGeometry(model);
                output.features = CaptureFeatureSnapshot(model);
                output.geometryPass = GeometryMatchesContract(output.geometry, contract);
                output.featurePass = FeatureMatchesContract(output.features);
                Require(output.externalReferenceCount == 0 && output.geometryPass &&
                    output.featurePass, "FRESH_NATIVE_REGRESSION_GATE_FAILED");
            }
            finally
            {
                CloseDocument(application, ref model);
                CloseOwnedSolidWorks(ref application, processAudit, session);
            }
            output.sessionExitProven = session.ExitProven;
            output.fileSha256Before = beforeSha256;
            output.fileSha256After = File.Exists(partPath) ? Sha256File(partPath) : "";
            output.fileUnchanged = string.Equals(output.fileSha256Before,
                output.fileSha256After, StringComparison.OrdinalIgnoreCase);
            output.pass = output.openErrors == 0 && output.openWarnings == 0 &&
                output.readOnly && output.rebuilt && output.externalReferenceCount == 0 &&
                output.geometryPass && output.featurePass && output.sessionExitProven &&
                output.fileUnchanged;
            return output;
        }

        private static GeometrySnapshot CaptureGeometry(ModelDoc2 model)
        {
            GeometrySnapshot output = new GeometrySnapshot();
            PartDoc part = model as PartDoc;
            Array bodies = null;
            Require(part != null, "GEOMETRY_DOCUMENT_NOT_PART");
            try
            {
                bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
                output.bodyCount = bodies == null ? 0 : bodies.Length;
                Require(output.bodyCount == 1, "GEOMETRY_SOLID_BODY_COUNT_INVALID");
                Body2 body = bodies.GetValue(0) as Body2;
                try
                {
                    Require(body != null, "GEOMETRY_SOLID_BODY_MISSING");
                    output.bodyBoxMm = DoubleList(body.GetBodyBox()).Select(value =>
                        value * 1000.0).ToList();
                    output.faceCount = body.GetFaceCount();
                    output.edgeCount = body.GetEdgeCount();
                    List<double> mass = DoubleList(body.GetMassProperties(7850.0));
                    Require(mass.Count >= 6 && mass.All(IsFinite),
                        "GEOMETRY_MASS_PROPERTIES_INVALID");
                    output.volumeMm3 = mass[3] * 1000000000.0;
                    output.surfaceAreaMm2 = mass[4] * 1000000.0;
                    output.massKg = mass[5];
                    Array faces = body.GetFaces() as Array;
                    foreach (object rawFace in faces ?? new object[0])
                    {
                        Face2 face = rawFace as Face2;
                        Surface surface = null;
                        try
                        {
                            Require(face != null, "GEOMETRY_FACE_INVALID");
                            surface = face.GetSurface() as Surface;
                            if (surface != null && surface.IsCylinder())
                            {
                                List<double> parameters = DoubleList(surface.CylinderParams);
                                Require(parameters.Count >= 7 && parameters.All(IsFinite),
                                    "GEOMETRY_CYLINDER_PARAMETERS_INVALID");
                                output.cylinders.Add(new CylinderSnapshot
                                {
                                    centerXmm = parameters[0] * 1000.0,
                                    centerYmm = parameters[1] * 1000.0,
                                    centerZmm = parameters[2] * 1000.0,
                                    axisX = parameters[3], axisY = parameters[4],
                                    axisZ = parameters[5], radiusMm = parameters[6] * 1000.0
                                });
                            }
                            Array loops = face.GetLoops() as Array;
                            output.loopCount += loops == null ? 0 : loops.Length;
                            foreach (object rawLoop in loops ?? new object[0])
                            {
                                Loop2 loop = rawLoop as Loop2;
                                try
                                {
                                    Require(loop != null, "GEOMETRY_LOOP_INVALID");
                                    Array edges = loop.GetEdges() as Array;
                                    output.loopEdgeReferenceCount += edges == null ? 0 :
                                        edges.Length;
                                    foreach (object rawEdge in edges ?? new object[0])
                                    {
                                        Edge edge = rawEdge as Edge;
                                        Curve curve = null;
                                        try
                                        {
                                            Require(edge != null, "GEOMETRY_EDGE_INVALID");
                                            curve = edge.GetCurve() as Curve;
                                            if (curve != null && curve.IsCircle())
                                                output.circleEdgeReferenceCount++;
                                        }
                                        finally { Release(curve); Release(edge); }
                                    }
                                }
                                finally { Release(loop); }
                            }
                        }
                        finally { Release(surface); Release(face); }
                    }
                }
                finally { Release(body); }
            }
            finally { }
            output.cylinderFaceCount = output.cylinders.Count;
            output.cylinderRadiiMm = output.cylinders.Select(value => value.radiusMm)
                .OrderBy(value => value).ToList();
            output.finite = output.bodyBoxMm.Count == 6 &&
                output.bodyBoxMm.All(IsFinite) && IsFinite(output.volumeMm3) &&
                IsFinite(output.surfaceAreaMm2) && IsFinite(output.massKg) &&
                output.cylinders.All(value => value.Finite);
            return output;
        }

        private static FeatureSnapshot CaptureFeatureSnapshot(ModelDoc2 model)
        {
            FeatureSnapshot output = new FeatureSnapshot();
            Feature feature = model.FirstFeature() as Feature;
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureFeatureRecursive(model, feature, output, 0);
                Feature next = feature.GetNextFeature() as Feature;
                Release(feature);
                feature = next;
            }
            Require(feature == null, "FEATURE_TRAVERSAL_GUARD_EXCEEDED");
            output.bends = output.bends.OrderBy(value => value.order).ToList();
            output.pass = FeatureMatchesContract(output);
            return output;
        }

        private static void CaptureFeatureRecursive(ModelDoc2 model, Feature feature,
            FeatureSnapshot output, int depth)
        {
            Require(feature != null && depth <= 30, "FEATURE_RECURSION_INVALID");
            string name = feature.Name ?? "";
            string type = feature.GetTypeName2() ?? "";
            bool warning = false;
            int error = feature.GetErrorCode2(out warning);
            bool suppressed = feature.IsSuppressed();
            output.featureCount++;
            if (error != 0 || warning)
                output.issues.Add(type + ":" + name + ":error=" +
                    error.ToString(CultureInfo.InvariantCulture) + ":warning=" + warning);
            if (string.Equals(type, "SheetMetal", StringComparison.OrdinalIgnoreCase))
            {
                output.sheetMetalCount++;
                CaptureSheetMetalDefinition(model, feature, output);
            }
            else if (string.Equals(type, "OneBend", StringComparison.OrdinalIgnoreCase))
            {
                output.oneBendCount++;
                BendSnapshot bend = CaptureOneBendDefinition(model, feature, name,
                    suppressed);
                output.bends.Add(bend);
            }
            else if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase))
                output.flatPatternCount++;

            Feature child = feature.GetFirstSubFeature() as Feature;
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureRecursive(model, child, output, depth + 1);
                Feature next = child.GetNextSubFeature() as Feature;
                Release(child);
                child = next;
            }
            Require(child == null, "FEATURE_CHILD_TRAVERSAL_GUARD_EXCEEDED");
        }

        private static void CaptureSheetMetalDefinition(ModelDoc2 model, Feature feature,
            FeatureSnapshot output)
        {
            object raw = feature.GetDefinition();
            ISheetMetalFeatureData data = raw as ISheetMetalFeatureData;
            CustomBendAllowance allowance = null;
            bool accessed = false;
            try
            {
                Require(data != null, "SHEET_METAL_DEFINITION_UNAVAILABLE");
                accessed = data.IAccessSelections2(model, null);
                output.sheetMetalThicknessMm = data.Thickness * 1000.0;
                output.sheetMetalRadiusMm = data.BendRadius * 1000.0;
                output.sheetMetalKFactor = data.KFactor;
                output.sheetMetalAllowanceType = data.BendAllowanceType;
                output.sheetMetalAllowanceMm = data.BendAllowance * 1000.0;
                allowance = data.GetCustomBendAllowance();
                if (allowance != null)
                {
                    output.sheetMetalAllowanceType = allowance.Type;
                    output.sheetMetalKFactor = allowance.KFactor;
                    output.sheetMetalAllowanceMm = allowance.BendAllowance * 1000.0;
                }
            }
            finally
            {
                if (data != null && accessed) data.ReleaseSelectionAccess();
                Release(allowance);
                Release(raw);
            }
        }

        private static BendSnapshot CaptureOneBendDefinition(ModelDoc2 model, Feature feature,
            string name, bool suppressed)
        {
            BendSnapshot output = new BendSnapshot { name = name, suppressed = suppressed };
            object raw = feature.GetDefinition();
            IOneBendFeatureData data = raw as IOneBendFeatureData;
            CustomBendAllowance allowance = null;
            bool accessed = false;
            try
            {
                Require(data != null, "ONE_BEND_DEFINITION_UNAVAILABLE");
                accessed = data.IAccessSelections2(model, null);
                output.bendType = data.GetType();
                output.angleRadians = data.BendAngle;
                output.direction = data.BendDirection;
                output.down = data.BendDown;
                output.order = data.BendOrder;
                output.radiusMm = data.BendRadius * 1000.0;
                output.kFactor = data.KFactor;
                output.allowanceMm = data.BendAllowance * 1000.0;
                output.allowanceType = data.BendAllowanceType;
                output.useDefaultRadius = data.UseDefaultBendRadius;
                output.useDefaultAllowance = data.UseDefaultBendAllowance;
                allowance = data.GetCustomBendAllowance();
                if (allowance != null)
                {
                    output.allowanceType = allowance.Type;
                    output.kFactor = allowance.KFactor;
                    output.allowanceMm = allowance.BendAllowance * 1000.0;
                }
            }
            finally
            {
                if (data != null && accessed) data.ReleaseSelectionAccess();
                Release(allowance);
                Release(raw);
            }
            return output;
        }

        private static bool FlatEnvelopeMatches(GeometrySnapshot geometry,
            RuleContract contract)
        {
            if (geometry == null || geometry.bodyCount != 1 || !geometry.finite ||
                geometry.bodyBoxMm.Count != 6 || contract.expectedFlatBoxMm.Count != 6)
                return false;
            return Near(geometry.bodyBoxMm[0], contract.expectedFlatBoxMm[0],
                    BoxToleranceMm) &&
                Near(geometry.bodyBoxMm[1], contract.expectedFlatBoxMm[1],
                    BoxToleranceMm) &&
                Near(geometry.bodyBoxMm[3], contract.expectedFlatBoxMm[3],
                    BoxToleranceMm) &&
                Near(geometry.bodyBoxMm[4], contract.expectedFlatBoxMm[4],
                    BoxToleranceMm) &&
                Near(geometry.bodyBoxMm[5] - geometry.bodyBoxMm[2], 0.8,
                    BoxToleranceMm) &&
                (Near(geometry.bodyBoxMm[2], contract.flatPlaneZMm, BoxToleranceMm) ||
                 Near(geometry.bodyBoxMm[5], contract.flatPlaneZMm, BoxToleranceMm));
        }

        private static bool FlatHoleGeometryMatches(GeometrySnapshot geometry,
            Dictionary<string, object> spec, RuleContract contract)
        {
            if (!FlatEnvelopeMatches(geometry, contract) || geometry.cylinderFaceCount != 10)
                return false;
            List<CylinderSnapshot> holes = geometry.cylinders.Where(IsHoleCylinder).ToList();
            if (holes.Count != 10) return false;
            foreach (object raw in ArrayValue(spec, "holes"))
            {
                Dictionary<string, object> expected = ObjectValue(raw);
                double x = Number(expected, "xMm");
                double y = Number(expected, "yMm");
                double radius = Number(expected, "diameterMm") / 2.0;
                if (!holes.Any(value => Near(value.centerXmm, x, 0.02) &&
                    Near(value.centerYmm, y, 0.02) &&
                    Near(value.radiusMm, radius, 0.002))) return false;
            }
            return true;
        }

        private static bool GeometryMatchesContract(GeometrySnapshot geometry,
            RuleContract contract)
        {
            if (geometry == null || contract == null || geometry.bodyCount != 1 ||
                !geometry.finite || geometry.bodyBoxMm.Count != 6 ||
                contract.expectedFoldedBoxMm.Count != 6) return false;
            for (int index = 0; index < 6; index++)
                if (!Near(geometry.bodyBoxMm[index], contract.expectedFoldedBoxMm[index],
                    BoxToleranceMm)) return false;
            if (!Near(geometry.volumeMm3, contract.expectedVolumeMm3,
                    VolumeToleranceMm3) ||
                !Near(geometry.surfaceAreaMm2, contract.expectedSurfaceAreaMm2,
                    AreaToleranceMm2) ||
                !Near(geometry.massKg, contract.expectedMassKg, MassToleranceKg) ||
                geometry.faceCount != contract.expectedFaceCount ||
                geometry.edgeCount != contract.expectedEdgeCount ||
                geometry.cylinderFaceCount != contract.expectedCylinderFaceCount ||
                geometry.loopCount != contract.expectedLoopCount ||
                geometry.loopEdgeReferenceCount !=
                    contract.expectedLoopEdgeReferenceCount ||
                geometry.circleEdgeReferenceCount !=
                    contract.expectedCircleEdgeReferenceCount) return false;
            double[] expectedRadii =
            {
                0.05, 0.2, 0.85, 1.0,
                1.75, 1.75, 1.75, 1.75, 1.75,
                5.0, 5.0, 5.0, 5.0, 5.0
            };
            if (geometry.cylinderRadiiMm.Count != expectedRadii.Length) return false;
            for (int index = 0; index < expectedRadii.Length; index++)
                if (!Near(geometry.cylinderRadiiMm[index], expectedRadii[index], 0.002))
                    return false;
            List<CylinderSnapshot> holes = geometry.cylinders.Where(IsHoleCylinder).ToList();
            return holes.Count == 10 && contract.holeX.All(x =>
                holes.Count(value => Near(value.centerXmm, x, 0.02) &&
                    (Near(value.radiusMm, 1.75, 0.002) ||
                     Near(value.radiusMm, 5.0, 0.002))) == 2);
        }

        private static bool IsHoleCylinder(CylinderSnapshot value)
        {
            return value != null && Math.Abs(value.axisZ) > 0.999 &&
                Math.Abs(value.axisX) < 0.001 && Math.Abs(value.axisY) < 0.001 &&
                (Near(value.radiusMm, 1.75, 0.002) || Near(value.radiusMm, 5.0, 0.002));
        }

        private static bool FeatureMatchesContract(FeatureSnapshot value)
        {
            if (value == null || value.sheetMetalCount != 1 || value.oneBendCount != 2 ||
                value.flatPatternCount != 1 || value.issues.Count != 0 ||
                value.bends.Count != 2 ||
                !Near(value.sheetMetalThicknessMm, 0.8, 0.0001) ||
                !Near(value.sheetMetalRadiusMm, 0.2, 0.0001) ||
                !Near(value.sheetMetalKFactor, 0.5, 0.000001) ||
                value.sheetMetalAllowanceType !=
                    (int)swBendAllowanceTypes_e.swBendAllowanceKFactor) return false;
            BendSnapshot first = value.bends[0];
            BendSnapshot second = value.bends[1];
            return first.order == 1 && first.bendType == 2 && !first.suppressed &&
                Near(first.angleRadians, Math.PI / 2.0, 0.000001) &&
                first.direction == 1 && !first.down &&
                Near(first.radiusMm, 0.2, 0.0001) &&
                Near(first.kFactor, 0.5, 0.000001) && first.useDefaultRadius &&
                first.allowanceType == (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                first.useDefaultAllowance &&
                second.order == 2 && second.bendType == 2 && !second.suppressed &&
                Near(second.angleRadians, Math.PI, 0.000001) &&
                second.direction == 2 && second.down &&
                Near(second.radiusMm, 0.05, 0.0001) &&
                Near(second.kFactor, 0.5, 0.000001) && second.useDefaultRadius &&
                second.allowanceType == (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                second.useDefaultAllowance;
        }

        private static void AddBuildStage(Dictionary<string, object> evidence,
            string stage, object proof)
        {
            List<Dictionary<string, object>> stages =
                evidence["buildStages"] as List<Dictionary<string, object>>;
            Require(stages != null, "BUILD_STAGE_LIST_MISSING");
            stages.Add(new Dictionary<string, object>
            {
                { "stage", stage }, { "pass", true }, { "proof", proof }
            });
        }

        private static List<double> DoubleList(object value)
        {
            Array array = value as Array;
            if (array == null) return new List<double>();
            List<double> output = new List<double>();
            foreach (object item in array)
                output.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
            return output;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static AuthorizationBinding LoadAndValidateAuthorization(
            Invocation invocation, string executableSha256)
        {
            byte[] bytes = File.ReadAllBytes(invocation.authorizationPath);
            Require(string.Equals(Sha256Bytes(bytes), invocation.authorizationSha256,
                StringComparison.OrdinalIgnoreCase), "AUTHORIZATION_LIVE_HASH_INVALID");
            string text = new UTF8Encoding(false, true).GetString(bytes);
            Require(StandardJsonNumbersOnly(text), "AUTHORIZATION_JSON_NONSTANDARD_NUMBER");
            Dictionary<string, object> root = ObjectValue(Json.DeserializeObject(text));
            string[] keys =
            {
                "schema", "authorizationId", "issuedAtUtc", "expiresAtUtc", "purpose",
                "approved", "operation", "taskId", "attempt", "ruleSha256", "toolId",
                "toolSourceNormalizedSha256", "toolExecutableSha256",
                "outputRelativePath", "evidenceRelativePath", "receiptRelativePath",
                "qualityBoundary"
            };
            Require(ExactKeys(root, keys) && Text(root, "schema") == AuthorizationSchema &&
                Regex.IsMatch(Text(root, "authorizationId"),
                    "^topcover-auth-[a-f0-9]{32}$", RegexOptions.CultureInvariant) &&
                Text(root, "purpose") == "structure_engineering_assistance" &&
                Bool(root, "approved") && Text(root, "operation") == PilotOperation &&
                Text(root, "taskId") == Path.GetFileName(
                    Path.GetDirectoryName(invocation.attemptDirectory)) &&
                Text(root, "attempt") == "attempt-0001" &&
                string.Equals(Text(root, "ruleSha256"), RuleSha256,
                    StringComparison.OrdinalIgnoreCase) && Text(root, "toolId") == ToolId &&
                string.Equals(Text(root, "toolSourceNormalizedSha256"),
                    ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(root, "toolExecutableSha256"), executableSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(root, "outputRelativePath") == OutputRelativePath &&
                Text(root, "evidenceRelativePath") == EvidenceRelativePath &&
                Text(root, "receiptRelativePath") == ReceiptRelativePath,
                "AUTHORIZATION_BINDING_INVALID");
            Dictionary<string, object> boundary = Child(root, "qualityBoundary");
            Require(ExactKeys(boundary, new[] { "structureEngineerAssistanceOnly",
                    "pilotNotAcceptedModel", "requiresRuntimeGeometryRegression" }) &&
                Bool(boundary, "structureEngineerAssistanceOnly") &&
                Bool(boundary, "pilotNotAcceptedModel") &&
                Bool(boundary, "requiresRuntimeGeometryRegression"),
                "AUTHORIZATION_QUALITY_BOUNDARY_INVALID");
            DateTime issued = ParseUtc(Text(root, "issuedAtUtc"),
                "AUTHORIZATION_ISSUED_TIME_INVALID");
            DateTime expires = ParseUtc(Text(root, "expiresAtUtc"),
                "AUTHORIZATION_EXPIRY_TIME_INVALID");
            DateTime now = DateTime.UtcNow;
            Require(issued <= now && expires > now && expires > issued &&
                (expires - issued).TotalMinutes <= 30.0,
                "AUTHORIZATION_NOT_LIVE_OR_TOO_LONG");
            return new AuthorizationBinding
            {
                authorizationId = Text(root, "authorizationId"),
                issuedAtUtc = Text(root, "issuedAtUtc"),
                expiresAtUtc = Text(root, "expiresAtUtc")
            };
        }

        private static void ValidateAuthorizationStillLive(Invocation invocation,
            AuthorizationBinding expected, string executableSha256)
        {
            AuthorizationBinding current = LoadAndValidateAuthorization(invocation,
                executableSha256);
            Require(current.authorizationId == expected.authorizationId &&
                current.issuedAtUtc == expected.issuedAtUtc &&
                current.expiresAtUtc == expected.expiresAtUtc,
                "AUTHORIZATION_CHANGED_DURING_PILOT");
        }

        private static DateTime ParseUtc(string value, string failure)
        {
            DateTime output;
            Require(DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out output), failure);
            output = output.ToUniversalTime();
            Require(output.Kind == DateTimeKind.Utc, failure);
            return output;
        }

        private static Dictionary<string, object> BuildPilotReceipt(Invocation invocation,
            AuthorizationBinding authorization, string executableSha256,
            string outputSha256, string evidenceSha256, string completedAtUtc)
        {
            return new Dictionary<string, object>
            {
                { "schema", ReceiptSchema }, { "phase", "topcover_front_760_pilot" },
                { "success", true }, { "consumable", false },
                { "completedAtUtc", completedAtUtc },
                { "authorizationId", authorization.authorizationId },
                { "authorizationSha256", invocation.authorizationSha256 },
                { "authorizationIssuedAtUtc", authorization.issuedAtUtc },
                { "authorizationExpiresAtUtc", authorization.expiresAtUtc },
                { "ruleSha256", RuleSha256 }, { "toolId", ToolId },
                { "toolSourceNormalizedSha256", ExpectedSourceSha256 },
                { "toolExecutableSha256", executableSha256 },
                { "outputRelativePath", OutputRelativePath },
                { "outputSha256", outputSha256 },
                { "evidenceRelativePath", EvidenceRelativePath },
                { "evidenceSha256", evidenceSha256 },
                { "qualityBoundary", "Pilot regression passed; structural engineer review remains required." }
            };
        }

        private static bool PilotEvidenceReceiptPairValid(string evidencePath,
            string receiptPath, string partPath, string evidenceSha256,
            string receiptSha256)
        {
            if (!File.Exists(evidencePath) || !File.Exists(receiptPath) ||
                !File.Exists(partPath) || FileLinkCount(evidencePath) != 1 ||
                FileLinkCount(receiptPath) != 1 || FileLinkCount(partPath) != 1 ||
                HasReparsePoint(evidencePath) || HasReparsePoint(receiptPath) ||
                HasReparsePoint(partPath) ||
                !string.Equals(Sha256File(evidencePath), evidenceSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Sha256File(receiptPath), receiptSha256,
                    StringComparison.OrdinalIgnoreCase)) return false;
            Dictionary<string, object> evidence = ObjectValue(Json.DeserializeObject(
                File.ReadAllText(evidencePath, new UTF8Encoding(false, true))));
            Dictionary<string, object> receipt = ObjectValue(Json.DeserializeObject(
                File.ReadAllText(receiptPath, new UTF8Encoding(false, true))));
            string[] receiptKeys =
            {
                "schema", "phase", "success", "consumable", "completedAtUtc",
                "authorizationId", "authorizationSha256", "authorizationIssuedAtUtc",
                "authorizationExpiresAtUtc", "ruleSha256", "toolId",
                "toolSourceNormalizedSha256", "toolExecutableSha256",
                "outputRelativePath", "outputSha256", "evidenceRelativePath",
                "evidenceSha256", "qualityBoundary"
            };
            return ExactKeys(receipt, receiptKeys) && Text(receipt, "schema") == ReceiptSchema &&
                Bool(receipt, "success") && !Bool(receipt, "consumable") &&
                Bool(evidence, "pilotPass") && !Bool(evidence, "consumable") &&
                Text(receipt, "authorizationId") == Text(evidence, "authorizationId") &&
                string.Equals(Text(receipt, "authorizationSha256"),
                    Text(evidence, "authorizationSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "ruleSha256"), Text(evidence, "ruleSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "outputSha256"), Sha256File(partPath),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "evidenceSha256"), evidenceSha256,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] StandardJsonBytes(object value)
        {
            string text = Json.Serialize(value) + System.Environment.NewLine;
            Require(StandardJsonNumbersOnly(text), "RUNTIME_JSON_NONSTANDARD_NUMBER");
            return new UTF8Encoding(false).GetBytes(text);
        }

        private static void WriteCreateNewAndReread(string path, byte[] bytes,
            string attemptDirectory)
        {
            Require(bytes != null && bytes.Length > 0 && !File.Exists(path) &&
                IsUnder(path, attemptDirectory), "CREATE_NEW_OUTPUT_PATH_INVALID");
            string parent = Path.GetDirectoryName(path);
            Require(Directory.Exists(parent), "CREATE_NEW_OUTPUT_PARENT_MISSING");
            AssertPathChainNoReparse(parent, attemptDirectory);
            using (FileStream stream = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            byte[] reread = File.ReadAllBytes(path);
            Require(FileLinkCount(path) == 1 && !HasReparsePoint(path) &&
                bytes.SequenceEqual(reread), "CREATE_NEW_OUTPUT_REREAD_FAILED");
        }

        private static bool RollbackOwnedFile(string path, bool createdByThisRun,
            string expectedSha256, string attemptDirectory)
        {
            if (!createdByThisRun) return !File.Exists(path);
            try
            {
                if (!IsUnder(path, attemptDirectory) || !File.Exists(path) ||
                    FileLinkCount(path) != 1 || HasReparsePoint(path) ||
                    !Regex.IsMatch(expectedSha256 ?? "", "^[A-F0-9]{64}$",
                        RegexOptions.CultureInvariant) ||
                    !string.Equals(Sha256File(path), expectedSha256,
                        StringComparison.OrdinalIgnoreCase)) return false;
                File.Delete(path);
                return !File.Exists(path);
            }
            catch { return false; }
        }

        private static bool RollbackOwnedPartDirectory(string partDirectory,
            string partPath, bool createdByThisRun, string attemptDirectory)
        {
            if (!createdByThisRun) return !Directory.Exists(partDirectory);
            try
            {
                if (!IsUnder(partDirectory, attemptDirectory) ||
                    !SamePath(Path.GetDirectoryName(partPath), partDirectory) ||
                    !Directory.Exists(partDirectory) || HasReparsePoint(partDirectory))
                    return false;
                string lockName = "~$" + Path.GetFileName(partPath);
                foreach (string entry in Directory.GetFileSystemEntries(partDirectory, "*",
                    SearchOption.TopDirectoryOnly))
                {
                    if (Directory.Exists(entry) || HasReparsePoint(entry) ||
                        FileLinkCount(entry) != 1) return false;
                    bool isPart = SamePath(entry, partPath);
                    bool isLock = string.Equals(Path.GetFileName(entry), lockName,
                        StringComparison.OrdinalIgnoreCase) &&
                        new FileInfo(entry).Length <= 4096;
                    if (!isPart && !isLock) return false;
                }
                foreach (string entry in Directory.GetFiles(partDirectory, "*",
                    SearchOption.TopDirectoryOnly)) File.Delete(entry);
                Directory.Delete(partDirectory, false);
                return !Directory.Exists(partDirectory) && !File.Exists(partPath);
            }
            catch { return false; }
        }

        private static void ValidateToolchainFiles()
        {
            Require(File.Exists(ExpectedSolidWorksExePath) &&
                File.Exists(ExpectedMonitorExePath) &&
                FileLinkCount(ExpectedSolidWorksExePath) == 1 &&
                FileLinkCount(ExpectedMonitorExePath) == 1 &&
                !HasReparsePoint(ExpectedSolidWorksExePath) &&
                !HasReparsePoint(ExpectedMonitorExePath) &&
                string.Equals(Sha256File(ExpectedSolidWorksExePath),
                    ExpectedSolidWorksExeSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256File(ExpectedMonitorExePath),
                    ExpectedMonitorExeSha256, StringComparison.OrdinalIgnoreCase),
                "SOLIDWORKS_TOOLCHAIN_HASH_INVALID");
        }

        private static ISldWorks StartOwnedSolidWorks(ProcessAudit audit,
            CadSession session)
        {
            ISldWorks application = null;
            try
            {
                Require(WaitForGlobalCadQuiescence(30000, 1000, 200),
                    "CAD_SESSION_NOT_EXCLUSIVE");
                List<ProcessIdentity> monitorsBefore = ProcessInfoByName("sldProcMon");
                application = Activator.CreateInstance(
                    Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks;
                Require(application != null, "SOLIDWORKS_ACTIVATION_FAILED");
                application.Visible = false;
                application.UserControl = false;
                application.CommandInProgress = true;
                int pid = application.GetProcessID();
                ProcessIdentity identity;
                Require(TryCaptureProcessIdentity(pid, "SLDWORKS", out identity) &&
                    SamePath(identity.executablePath, ExpectedSolidWorksExePath) &&
                    string.Equals(identity.executableSha256, ExpectedSolidWorksExeSha256,
                        StringComparison.OrdinalIgnoreCase),
                    "SOLIDWORKS_PROCESS_IDENTITY_UNPROVEN");
                string revision = application.RevisionNumber();
                Require(revision.StartsWith(ExpectedSolidWorksVersionPrefix,
                    StringComparison.Ordinal), "SOLIDWORKS_REVISION_INVALID");
                session.sldworks = identity;
                session.revision = revision;
                session.created = ProcessIds("SLDWORKS").SequenceEqual(new[] { pid });
                Require(session.created, "SOLIDWORKS_OWNERSHIP_OR_EXCLUSIVITY_INVALID");
                Thread.Sleep(500);
                CaptureOwnedMonitors(session, monitorsBefore);
                return application;
            }
            catch
            {
                if (application != null)
                {
                    TryAction(() => application.ExitApp());
                    Release(application);
                    application = null;
                }
                if (session.sldworks != null && session.sldworks.pid > 0)
                    CloseOwnedSolidWorks(ref application, audit, session);
                throw;
            }
        }

        private static void CloseOwnedSolidWorks(ref ISldWorks application,
            ProcessAudit audit, CadSession session)
        {
            if (session == null) return;
            CaptureOwnedMonitors(session, new List<ProcessIdentity>());
            if (application != null)
            {
                ISldWorks current = application;
                TryAction(() => current.CloseAllDocuments(true));
                TryAction(() => current.CommandInProgress = false);
                session.exitRequested = TryAction(() => current.ExitApp());
                Release(current);
                application = null;
            }
            session.exitState = WaitForIdentityExit(session.sldworks, 10000);
            if (session.exitState == "unproven")
                session.exitState = ResolveUnprovenState(
                    () => ExactIdentityState(session.sldworks), 30000, 200);
            if (session.exitState == "alive_owned")
            {
                if (KillExactIdentity(session.sldworks, audit))
                    session.exitState = WaitForIdentityExit(session.sldworks, 3000);
            }
            else if (session.exitState == "unproven")
                audit.cleanupErrors.Add("refused to kill unproven SolidWorks PID " +
                    (session.sldworks == null ? "0" : session.sldworks.pid.ToString(
                        CultureInfo.InvariantCulture)));
            CleanupOwnedMonitors(session, audit);
        }

        private static void CaptureOwnedMonitors(CadSession session,
            List<ProcessIdentity> before)
        {
            if (session == null || session.sldworks == null || session.sldworks.pid <= 0)
                return;
            HashSet<string> beforeKeys = new HashSet<string>(before.Select(IdentityKey),
                StringComparer.OrdinalIgnoreCase);
            string pattern = "(?:^|\\s)--ppid=" + session.sldworks.pid.ToString(
                CultureInfo.InvariantCulture) + "(?:\\s|$)";
            foreach (ProcessIdentity current in ProcessInfoByName("sldProcMon"))
            {
                if (beforeKeys.Contains(IdentityKey(current)) ||
                    current.parentPid != session.sldworks.pid ||
                    !Regex.IsMatch(current.commandLine ?? "", pattern,
                        RegexOptions.CultureInvariant) ||
                    !SamePath(current.executablePath, ExpectedMonitorExePath) ||
                    !string.Equals(current.executableSha256, ExpectedMonitorExeSha256,
                        StringComparison.OrdinalIgnoreCase)) continue;
                if (!session.monitors.Any(value => value.pid == current.pid &&
                    value.startUtcTicks == current.startUtcTicks))
                    session.monitors.Add(current);
            }
        }

        private static void CleanupOwnedMonitors(CadSession session, ProcessAudit audit)
        {
            CaptureOwnedMonitors(session, new List<ProcessIdentity>());
            foreach (ProcessIdentity monitor in session.monitors)
            {
                string state = ExactMonitorIdentityState(monitor,
                    session.sldworks.pid);
                if (state == "unproven")
                    state = ResolveUnprovenState(
                        () => ExactMonitorIdentityState(monitor, session.sldworks.pid),
                        30000, 200);
                if (state == "alive_owned" && KillExactIdentity(monitor, audit))
                    state = WaitForMonitorExit(monitor, session.sldworks.pid, 3000);
                if (state == "unproven")
                    audit.cleanupErrors.Add("refused to kill unproven sldProcMon PID " +
                        monitor.pid.ToString(CultureInfo.InvariantCulture));
                monitor.exitState = state;
            }
            session.monitorsExitProven = session.monitors.All(value =>
                value.exitState == "exited") && WaitForEmptyProcessSet("sldProcMon",
                10000, 200);
        }

        private static bool KillExactIdentity(ProcessIdentity identity, ProcessAudit audit)
        {
            if (ExactIdentityState(identity) != "alive_owned") return false;
            try
            {
                using (Process process = Process.GetProcessById(identity.pid))
                {
                    if (!ProcessObjectMatchesIdentity(process, identity)) return false;
                    process.Kill();
                    process.WaitForExit(3000);
                }
                audit.forceStoppedOwnedProcessIds.Add(identity.pid);
                return true;
            }
            catch (ArgumentException) { return true; }
            catch (Exception exception)
            {
                audit.cleanupErrors.Add(SafeException(exception));
                return false;
            }
        }

        private static bool ProcessObjectMatchesIdentity(Process process,
            ProcessIdentity expected)
        {
            try
            {
                return process != null && expected != null && !process.HasExited &&
                    process.Id == expected.pid && string.Equals(process.ProcessName,
                        expected.name, StringComparison.OrdinalIgnoreCase) &&
                    process.StartTime.ToUniversalTime().Ticks == expected.startUtcTicks &&
                    process.MainModule != null && SamePath(process.MainModule.FileName,
                        expected.executablePath) && string.Equals(
                        Sha256File(process.MainModule.FileName), expected.executableSha256,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string WaitForIdentityExit(ProcessIdentity identity, int timeoutMs)
        {
            return WaitForVerifiedExit(() => ExactIdentityState(identity), timeoutMs, 200);
        }

        private static string WaitForMonitorExit(ProcessIdentity identity, int parentPid,
            int timeoutMs)
        {
            return WaitForVerifiedExit(() => ExactMonitorIdentityState(identity, parentPid),
                timeoutMs, 200);
        }

        private static string WaitForVerifiedExit(Func<string> probe, int timeoutMs,
            int pollMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                string state = probe();
                if (state == "exited" || DateTime.UtcNow >= deadline) return state;
                Thread.Sleep(pollMs);
            }
        }

        private static string ResolveUnprovenState(Func<string> probe, int timeoutMs,
            int pollMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                string state = probe();
                if (state != "unproven" || DateTime.UtcNow >= deadline) return state;
                Thread.Sleep(pollMs);
            }
        }

        private static string ExactIdentityState(ProcessIdentity expected)
        {
            if (expected == null || expected.pid <= 0 || expected.startUtcTicks <= 0 ||
                string.IsNullOrWhiteSpace(expected.executablePath) ||
                !Regex.IsMatch(expected.executableSha256 ?? "", "^[A-F0-9]{64}$",
                    RegexOptions.CultureInvariant)) return "unproven";
            ProcessIdentity current;
            CaptureIdentityResult result = CaptureProcessIdentity(expected.pid,
                expected.name, out current);
            if (result == CaptureIdentityResult.Exited) return "exited";
            if (result != CaptureIdentityResult.Captured) return "unproven";
            return current.startUtcTicks == expected.startUtcTicks &&
                SamePath(current.executablePath, expected.executablePath) &&
                string.Equals(current.executableSha256, expected.executableSha256,
                    StringComparison.OrdinalIgnoreCase) ? "alive_owned" : "unproven";
        }

        private static string ExactMonitorIdentityState(ProcessIdentity expected,
            int parentPid)
        {
            if (expected == null || expected.parentPid != parentPid ||
                !string.Equals(expected.name, "sldProcMon",
                    StringComparison.OrdinalIgnoreCase) ||
                !Regex.IsMatch(expected.commandLine ?? "", "(?:^|\\s)--ppid=" +
                    parentPid.ToString(CultureInfo.InvariantCulture) + "(?:\\s|$)",
                    RegexOptions.CultureInvariant) ||
                !SamePath(expected.executablePath, ExpectedMonitorExePath) ||
                !string.Equals(expected.executableSha256, ExpectedMonitorExeSha256,
                    StringComparison.OrdinalIgnoreCase)) return "unproven";
            return ExactIdentityState(expected);
        }

        private static CaptureIdentityResult CaptureProcessIdentity(int pid,
            string expectedName, out ProcessIdentity identity)
        {
            identity = null;
            if (pid <= 0 || string.IsNullOrWhiteSpace(expectedName))
                return CaptureIdentityResult.Unproven;
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    if (process.HasExited) return CaptureIdentityResult.Exited;
                    if (!string.Equals(process.ProcessName, expectedName,
                        StringComparison.OrdinalIgnoreCase)) return CaptureIdentityResult.Unproven;
                    string executable = process.MainModule == null ? "" :
                        process.MainModule.FileName;
                    if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                        return CaptureIdentityResult.Unproven;
                    identity = new ProcessIdentity
                    {
                        pid = pid, name = expectedName,
                        startUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                        executablePath = FullPath(executable),
                        executableSha256 = Sha256File(executable)
                    };
                    return CaptureIdentityResult.Captured;
                }
            }
            catch (ArgumentException) { return CaptureIdentityResult.Exited; }
            catch { return CaptureIdentityResult.Unproven; }
        }

        private static bool TryCaptureProcessIdentity(int pid, string expectedName,
            out ProcessIdentity identity)
        {
            return CaptureProcessIdentity(pid, expectedName, out identity) ==
                CaptureIdentityResult.Captured;
        }

        private static List<ProcessIdentity> ProcessInfoByName(string name)
        {
            List<ProcessIdentity> output = new List<ProcessIdentity>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId,ParentProcessId,Name,CommandLine,ExecutablePath " +
                    "FROM Win32_Process WHERE Name='" + name.Replace("'", "''") + ".exe'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        int pid = Convert.ToInt32(row["ProcessId"],
                            CultureInfo.InvariantCulture);
                        ProcessIdentity identity;
                        if (!TryCaptureProcessIdentity(pid, name, out identity)) continue;
                        identity.parentPid = Convert.ToInt32(row["ParentProcessId"],
                            CultureInfo.InvariantCulture);
                        identity.commandLine = Convert.ToString(row["CommandLine"],
                            CultureInfo.InvariantCulture) ?? "";
                        string wmiPath = Convert.ToString(row["ExecutablePath"],
                            CultureInfo.InvariantCulture) ?? "";
                        if (string.IsNullOrWhiteSpace(wmiPath) ||
                            !SamePath(wmiPath, identity.executablePath)) continue;
                        output.Add(identity);
                    }
                }
            }
            catch { }
            return output;
        }

        private static string IdentityKey(ProcessIdentity value)
        {
            return value == null ? "" : value.pid.ToString(CultureInfo.InvariantCulture) +
                "|" + value.startUtcTicks.ToString(CultureInfo.InvariantCulture);
        }

        private static List<int> ProcessIds(string name)
        {
            List<int> output = new List<int>();
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try { output.Add(process.Id); }
                finally { process.Dispose(); }
            }
            output.Sort();
            return output;
        }

        private static bool WaitForEmptyProcessSet(string name, int timeoutMs,
            int pollMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                if (ProcessIds(name).Count == 0) return true;
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(pollMs);
            }
        }

        private static bool WaitForGlobalCadQuiescence(int timeoutMs,
            int quietWindowMs, int pollMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            DateTime? emptySince = null;
            while (true)
            {
                DateTime now = DateTime.UtcNow;
                if (ProcessIds("SLDWORKS").Count == 0 &&
                    ProcessIds("sldProcMon").Count == 0)
                {
                    if (!emptySince.HasValue) emptySince = now;
                    if ((now - emptySince.Value).TotalMilliseconds >= quietWindowMs)
                        return true;
                }
                else emptySince = null;
                if (now >= deadline) return false;
                Thread.Sleep(pollMs);
            }
        }

        private static void CloseDocument(ISldWorks application, ref ModelDoc2 model)
        {
            if (model == null) return;
            try
            {
                if (application != null) application.CloseDoc(model.GetTitle());
            }
            catch { }
            Release(model);
            model = null;
        }

        private static bool TryAction(Action action)
        {
            try { action(); return true; }
            catch { return false; }
        }

        private static string SafeException(Exception exception)
        {
            return exception == null ? "unknown" : exception.GetType().Name + ":" +
                (exception.Message ?? "").Replace('\r', ' ').Replace('\n', ' ');
        }

        private static void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); }
            catch { }
        }

        private static RuleContract LoadAndValidateRule(string path, string repositoryRoot)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text = new UTF8Encoding(false, true).GetString(bytes);
            Require(StandardJsonNumbersOnly(text), "RULE_JSON_NONSTANDARD_NUMBER");
            Dictionary<string, object> root = ObjectValue(Json.DeserializeObject(text));
            Require(ExactKeys(root, TopLevelKeys), "RULE_TOP_LEVEL_KEYS_INVALID");
            Require(Text(root, "schema") == RuleSchema, "RULE_SCHEMA_INVALID");
            Require(Text(root, "purpose") == "structure_engineering_assistance",
                "RULE_PURPOSE_INVALID");
            Require(Bool(root, "frontNativePilotReady") && !Bool(root, "nativeCadCreated"),
                "RULE_STATE_INVALID");
            Require(!string.IsNullOrWhiteSpace(Text(root, "qualityBoundary")),
                "RULE_QUALITY_BOUNDARY_MISSING");

            RuleContract output = new RuleContract { exactKeys = true };
            Dictionary<string, object> inputs = Child(root, "inputs");
            string[] inputNames =
            {
                "physicalCapture", "goldDxfCapture", "physicalBundle", "masterCapture",
                "ruleV1", "frontBrep", "frontBrepCleanup"
            };
            Require(ExactKeys(inputs, inputNames), "RULE_INPUT_KEYS_INVALID");
            output.inputBindingsValid = inputNames.All(name =>
                InputBindingValid(Child(inputs, name), repositoryRoot));
            Require(output.inputBindingsValid, "RULE_INPUT_BINDING_INVALID");

            Dictionary<string, object> sheet = Child(root, "sheetMetal");
            output.sheetMetalValid = ExactKeys(sheet, new[] { "thicknessMm",
                    "defaultBendRadiusMm", "kFactor", "allowanceMm" }) &&
                Near(Number(sheet, "thicknessMm"), 0.8, 1e-9) &&
                Near(Number(sheet, "defaultBendRadiusMm"), 0.2, 1e-9) &&
                Near(Number(sheet, "kFactor"), 0.5, 1e-9) &&
                Near(Number(sheet, "allowanceMm"), 1.4, 1e-9);
            Require(output.sheetMetalValid, "RULE_SHEET_METAL_INVALID");

            Dictionary<string, object> builds = Child(root, "buildSpecs");
            Require(builds.ContainsKey("width760Pilot"), "WIDTH760_SPEC_MISSING");
            Dictionary<string, object> spec = ObjectValue(builds["width760Pilot"]);
            Require(ExactKeys(spec, SpecKeys), "WIDTH760_SPEC_KEYS_INVALID");
            output.widthMm = Number(spec, "cabinetWidthMm");
            Require(Near(output.widthMm, 760, 1e-9), "WIDTH760_REQUIRED");
            ValidateOutline(spec, output);
            ValidateHoles(spec, output);
            ValidateBends(spec, output);
            ValidateBboxMassTopology(spec, output);
            return output;
        }

        private static void ValidateOutline(Dictionary<string, object> spec, RuleContract output)
        {
            object[] rows = ArrayValue(spec, "outline");
            Require(rows.Length == 20, "OUTLINE_LINE_COUNT_INVALID");
            Dictionary<string, int> degree = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> graph =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            HashSet<int> sourceIndexes = new HashSet<int>();
            List<Point3> points = new List<Point3>();
            foreach (object raw in rows)
            {
                Dictionary<string, object> row = ObjectValue(raw);
                Require(ExactKeys(row, new[] { "sourceIndex", "startMm", "endMm" }),
                    "OUTLINE_ROW_KEYS_INVALID");
                Require(sourceIndexes.Add((int)Number(row, "sourceIndex")),
                    "OUTLINE_SOURCE_INDEX_DUPLICATE");
                Point3 start = Point(ArrayValue(row, "startMm"));
                Point3 end = Point(ArrayValue(row, "endMm"));
                Require(!start.Near(end, 1e-9), "OUTLINE_ZERO_LENGTH_LINE");
                points.Add(start); points.Add(end);
                AddGraphEdge(graph, degree, start.Key(), end.Key());
            }
            Require(degree.Count == 20 && degree.Values.All(value => value == 2),
                "OUTLINE_NOT_DEGREE_TWO_CYCLE");
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(graph.Keys.First());
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!visited.Add(current)) continue;
                foreach (string next in graph[current]) queue.Enqueue(next);
            }
            Require(visited.Count == graph.Count, "OUTLINE_NOT_CONNECTED");
            output.outlineLineCount = rows.Length;
            output.outlineVertexCount = degree.Count;
            output.flatPlaneZMm = points[0].z;
            Require(points.All(value => Near(value.z, output.flatPlaneZMm, 1e-7)),
                "OUTLINE_NOT_ON_ONE_PLANE");
            output.flatWidthMm = points.Max(value => value.x) - points.Min(value => value.x);
            output.flatHeightMm = points.Max(value => value.y) - points.Min(value => value.y);
        }

        private static void ValidateHoles(Dictionary<string, object> spec, RuleContract output)
        {
            object[] rows = ArrayValue(spec, "holes");
            List<double> x = new List<double>();
            List<double> y = new List<double>();
            int small = 0; int large = 0;
            foreach (object raw in rows)
            {
                Dictionary<string, object> row = ObjectValue(raw);
                Require(ExactKeys(row, new[] { "xMm", "yMm", "diameterMm" }),
                    "HOLE_ROW_KEYS_INVALID");
                double diameter = Number(row, "diameterMm");
                if (Near(diameter, 3.5, 1e-9)) small++;
                else if (Near(diameter, 10, 1e-9)) large++;
                else throw new ContractException("HOLE_DIAMETER_INVALID");
                x.Add(Number(row, "xMm"));
                y.Add(Number(row, "yMm"));
            }
            output.holeCount = rows.Length;
            output.smallHoleCount = small;
            output.largeHoleCount = large;
            output.holeX = x.Distinct().OrderBy(value => value).ToList();
            List<double> uniqueY = y.Distinct().OrderBy(value => value).ToList();
            Require(output.holeX.SequenceEqual(new[] { -260.0, -120.0, 20.0, 160.0,
                    300.0 }) && uniqueY.Count == 2 &&
                Near(uniqueY[0], 1908.999923, 0.000001) &&
                Near(uniqueY[1], 1924.713644, 0.000001), "HOLE_POSITION_INVALID");
        }

        private static void ValidateBends(Dictionary<string, object> spec, RuleContract output)
        {
            object[] lines = ArrayValue(spec, "bendLines");
            object[] bends = ArrayValue(spec, "bends");
            Require(lines.Length == 2 && bends.Length == 2, "TWO_BENDS_REQUIRED");
            foreach (object raw in lines)
            {
                Dictionary<string, object> line = ObjectValue(raw);
                Require(ExactKeys(line, new[] { "sourcePath", "sourceIndex", "startMm",
                        "endMm" }), "BEND_LINE_KEYS_INVALID");
                Point3 start = Point(ArrayValue(line, "startMm"));
                Point3 end = Point(ArrayValue(line, "endMm"));
                Require(Near(start.z, -47.7, 1e-7) && Near(end.z, -47.7, 1e-7) &&
                    Near(Math.Abs(end.x - start.x), 659.6, 0.000001) &&
                    Near(start.y, end.y, 0.000001), "BEND_LINE_GEOMETRY_INVALID");
            }
            List<Dictionary<string, object>> ordered = bends.Select(ObjectValue).OrderBy(value =>
                Number(value, "order")).ToList();
            string[] bendKeys = { "path", "bendType", "angleRadians", "direction", "down",
                "order", "radiusMm", "kFactor", "allowanceMm" };
            Require(ordered.All(value => ExactKeys(value, bendKeys)), "BEND_KEYS_INVALID");
            bool first = Number(ordered[0], "order") == 1 &&
                Number(ordered[0], "bendType") == 0 &&
                Near(Number(ordered[0], "angleRadians"), Math.PI / 2, 1e-12) &&
                Number(ordered[0], "direction") == 1 && !Bool(ordered[0], "down") &&
                Near(Number(ordered[0], "radiusMm"), 0.2, 1e-9) &&
                Near(Number(ordered[0], "kFactor"), 0.5, 1e-9);
            bool second = Number(ordered[1], "order") == 2 &&
                Number(ordered[1], "bendType") == 1 &&
                Near(Number(ordered[1], "angleRadians"), Math.PI, 1e-12) &&
                Number(ordered[1], "direction") == 2 && Bool(ordered[1], "down") &&
                Near(Number(ordered[1], "radiusMm"), 0.05, 1e-9) &&
                Near(Number(ordered[1], "kFactor"), 0.5, 1e-9);
            output.bendLineCount = lines.Length;
            output.bendCount = bends.Length;
            output.bendSequenceValid = first && second;
            Require(output.bendSequenceValid, "BEND_SEQUENCE_INVALID");
        }

        private static void ValidateBboxMassTopology(Dictionary<string, object> spec,
            RuleContract output)
        {
            output.flatPlaneZMm = Number(spec, "flatPlaneZmm");
            List<double> flat = NumberArray(spec, "flatBboxMm");
            List<double> folded = NumberArray(spec, "foldedBboxMm");
            Require(flat.Count == 6 && folded.Count == 6, "BBOX_COUNT_INVALID");
            bool flatValid = Near(flat[0], -380, 1e-6) && Near(flat[1], 1885.057522,
                1e-6) && Near(flat[2], -47.7, 1e-6) && Near(flat[3], 380, 1e-6) &&
                Near(flat[4], 1994.513721, 1e-6) && Near(flat[5], -47.7, 1e-6);
            bool foldedValid = Near(folded[0], -380, 1e-6) &&
                Near(folded[1], 1839.19998, 1e-5) && Near(folded[2], -56.9, 1e-6) &&
                Near(folded[3], 380, 1e-6) && Near(folded[4], 1917.000005, 1e-5) &&
                Near(folded[5], -45.99995, 1e-6);
            Dictionary<string, object> mass = Child(spec, "expectedMass");
            bool massValid = ExactKeys(mass, new[] { "volumeMm3", "massKg",
                    "surfaceAreaMm2" }) && Near(Number(mass, "volumeMm3"), 27485.460097,
                    0.000001) && Near(Number(mass, "massKg"), 0.215760862, 1e-9) &&
                Near(Number(mass, "surfaceAreaMm2"), 70383.22241, 0.00001);
            Dictionary<string, object> topology = Child(spec, "expectedTopology");
            output.topologyValid = ExactKeys(topology, new[] { "holeCount",
                    "cylinderFaceCount", "faceCount", "edgeCount", "loopCount",
                    "loopEdgeReferenceCount", "circleEdgeReferenceCount" }) &&
                Number(topology, "holeCount") == 10 &&
                Number(topology, "cylinderFaceCount") == 14 &&
                Number(topology, "faceCount") == 48 && Number(topology, "edgeCount") == 112 &&
                Number(topology, "loopCount") == 78 &&
                Number(topology, "loopEdgeReferenceCount") == 224 &&
                Number(topology, "circleEdgeReferenceCount") == 56;
            output.foldedAndMassValid = flatValid && foldedValid && massValid;
            Require(output.foldedAndMassValid && output.topologyValid,
                "BBOX_MASS_OR_TOPOLOGY_INVALID");
            output.expectedFlatBoxMm = flat;
            output.expectedFoldedBoxMm = folded;
            output.expectedVolumeMm3 = Number(mass, "volumeMm3");
            output.expectedMassKg = Number(mass, "massKg");
            output.expectedSurfaceAreaMm2 = Number(mass, "surfaceAreaMm2");
            output.expectedCylinderFaceCount = (int)Number(topology,
                "cylinderFaceCount");
            output.expectedFaceCount = (int)Number(topology, "faceCount");
            output.expectedEdgeCount = (int)Number(topology, "edgeCount");
            output.expectedLoopCount = (int)Number(topology, "loopCount");
            output.expectedLoopEdgeReferenceCount = (int)Number(topology,
                "loopEdgeReferenceCount");
            output.expectedCircleEdgeReferenceCount = (int)Number(topology,
                "circleEdgeReferenceCount");
        }

        private static bool InputBindingValid(Dictionary<string, object> row,
            string repositoryRoot)
        {
            if (!ExactKeys(row, new[] { "path", "sha256" })) return false;
            string path = FullPath(Text(row, "path"));
            string rel = RelativePath(repositoryRoot, path);
            return !rel.StartsWith("..", StringComparison.Ordinal) &&
                !Path.IsPathRooted(rel) && File.Exists(path) && FileLinkCount(path) == 1 &&
                !HasReparsePoint(path) && string.Equals(Sha256File(path), Text(row, "sha256"),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void AddGraphEdge(Dictionary<string, HashSet<string>> graph,
            Dictionary<string, int> degree, string left, string right)
        {
            if (!graph.ContainsKey(left)) graph[left] = new HashSet<string>(StringComparer.Ordinal);
            if (!graph.ContainsKey(right)) graph[right] = new HashSet<string>(StringComparer.Ordinal);
            graph[left].Add(right); graph[right].Add(left);
            degree[left] = degree.ContainsKey(left) ? degree[left] + 1 : 1;
            degree[right] = degree.ContainsKey(right) ? degree[right] + 1 : 1;
        }

        private static bool SafeAttemptShape(string attempt, string repositoryRoot)
        {
            try
            {
                string root = FullPath(Path.Combine(repositoryRoot, "data",
                    "native_model_requests", "attempts"));
                string full = FullPath(attempt);
                string parent = Path.GetDirectoryName(full);
                return SamePath(Path.GetDirectoryName(parent), root) &&
                    Regex.IsMatch(Path.GetFileName(parent) ?? "",
                        "^TOPCOVER-PILOT-[A-Z0-9-]{8,120}$", RegexOptions.CultureInvariant) &&
                    string.Equals(Path.GetFileName(full), "attempt-0001",
                        StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int index = 0; index < 12 && current != null; index++, current = current.Parent)
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tools")))
                    return FullPath(current.FullName);
            throw new ContractException("REPOSITORY_ROOT_NOT_FOUND");
        }

        private static bool SourceIdentityValid(string repositoryRoot)
        {
            string source = Path.Combine(repositoryRoot, "workers", "native_model_requests",
                "development", "v1", "tools", "topcover_parts_native_v1",
                "TopCoverPartsNative.cs");
            if (!File.Exists(source)) return false;
            string text = File.ReadAllText(source, new UTF8Encoding(false, true)).Replace("\r\n",
                "\n").Replace("\r", "\n");
            Regex pattern = new Regex("(ExpectedSourceSha256\\s*=\\s*\")(?:__SOURCE_SHA256__|" +
                "[A-F0-9]{64})(\";)", RegexOptions.CultureInvariant);
            if (pattern.Matches(text).Count != 1) return false;
            string normalized = pattern.Replace(text, "$1__SOURCE_SHA256__$2");
            return string.Equals(Sha256Bytes(new UTF8Encoding(false).GetBytes(normalized)),
                ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StandardJsonNumbersOnly(string json)
        {
            bool inside = false; bool escaped = false;
            StringBuilder token = new StringBuilder();
            for (int index = 0; index < json.Length; index++)
            {
                char value = json[index];
                if (inside)
                {
                    if (escaped) escaped = false;
                    else if (value == '\\') escaped = true;
                    else if (value == '"') inside = false;
                    continue;
                }
                if (value == '"') { inside = true; token.Clear(); continue; }
                if (char.IsLetter(value) || value == '-') token.Append(value);
                else
                {
                    string raw = token.ToString();
                    if (raw == "NaN" || raw == "Infinity" || raw == "-Infinity") return false;
                    token.Clear();
                }
            }
            string last = token.ToString();
            return last != "NaN" && last != "Infinity" && last != "-Infinity";
        }

        private static void AssertPathChainNoReparse(string path, string stop)
        {
            string current = FullPath(path);
            string root = FullPath(stop);
            Require(IsUnder(current, root), "PATH_ESCAPED_FIXED_ROOT");
            while (true)
            {
                FileSystemInfo item = Directory.Exists(current) ?
                    (FileSystemInfo)new DirectoryInfo(current) : new FileInfo(current);
                Require(item.Exists && (item.Attributes & FileAttributes.ReparsePoint) == 0,
                    "PATH_REPARSE_OR_MISSING");
                if (SamePath(current, root)) break;
                current = Path.GetDirectoryName(current);
                Require(!string.IsNullOrWhiteSpace(current), "PATH_PARENT_MISSING");
            }
        }

        private static bool HasReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        private static bool IsUnder(string path, string root)
        {
            string full = FullPath(path);
            string parent = FullPath(root);
            return full.StartsWith(parent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) || SamePath(full, parent);
        }

        private static string RelativePath(string root, string path)
        {
            Uri rootUri = new Uri(FullPath(root) + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(new Uri(FullPath(path))).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string Sha256File(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }

        private static string Sha256Bytes(byte[] value)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "");
        }

        private static int FileLinkCount(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                ByHandleFileInformation info;
                return GetFileInformationByHandle(stream.SafeFileHandle, out info) ?
                    checked((int)info.NumberOfLinks) : -1;
            }
        }

        private static string FullPath(string path)
        {
            return Path.GetFullPath(path ?? "").TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(FullPath(left), FullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool Near(double left, double right, double tolerance)
        {
            return Math.Abs(left - right) <= tolerance;
        }

        private static Dictionary<string, object> ObjectValue(object value)
        {
            Dictionary<string, object> result = value as Dictionary<string, object>;
            Require(result != null, "OBJECT_REQUIRED");
            return result;
        }

        private static Dictionary<string, object> Child(Dictionary<string, object> value,
            string key)
        {
            Require(value.ContainsKey(key), "KEY_REQUIRED_" + key);
            return ObjectValue(value[key]);
        }

        private static object[] ArrayValue(Dictionary<string, object> value, string key)
        {
            Require(value.ContainsKey(key), "KEY_REQUIRED_" + key);
            IEnumerable sequence = value[key] as IEnumerable;
            Require(sequence != null && !(value[key] is string), "ARRAY_REQUIRED_" + key);
            return sequence.Cast<object>().ToArray();
        }

        private static List<double> NumberArray(Dictionary<string, object> value, string key)
        {
            return ArrayValue(value, key).Select(NumberValue).ToList();
        }

        private static Point3 Point(object[] value)
        {
            Require(value.Length == 3, "POINT_DIMENSION_INVALID");
            return new Point3(NumberValue(value[0]), NumberValue(value[1]),
                NumberValue(value[2]));
        }

        private static string Text(Dictionary<string, object> value, string key)
        {
            Require(value.ContainsKey(key) && value[key] is string,
                "STRING_REQUIRED_" + key);
            return (string)value[key];
        }

        private static bool Bool(Dictionary<string, object> value, string key)
        {
            Require(value.ContainsKey(key) && value[key] is bool,
                "BOOL_REQUIRED_" + key);
            return (bool)value[key];
        }

        private static double Number(Dictionary<string, object> value, string key)
        {
            Require(value.ContainsKey(key), "NUMBER_REQUIRED_" + key);
            return NumberValue(value[key]);
        }

        private static double NumberValue(object value)
        {
            try
            {
                double result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                Require(!double.IsNaN(result) && !double.IsInfinity(result),
                    "NONFINITE_NUMBER");
                return result;
            }
            catch (ContractException) { throw; }
            catch { throw new ContractException("NUMBER_INVALID"); }
        }

        private static bool ExactKeys(Dictionary<string, object> value,
            IEnumerable<string> expected)
        {
            return value != null && new HashSet<string>(value.Keys, StringComparer.Ordinal)
                .SetEquals(expected);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ContractException(message);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file,
            out ByHandleFileInformation information);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        private sealed class Invocation
        {
            public string rulePath = "";
            public string attemptDirectory = "";
            public string outputPath = "";
            public string authorizationPath = "";
            public string authorizationSha256 = "";
        }

        private sealed class RuleContract
        {
            public bool exactKeys;
            public bool inputBindingsValid;
            public bool sheetMetalValid;
            public double widthMm;
            public int outlineLineCount;
            public int outlineVertexCount;
            public double flatPlaneZMm;
            public double flatWidthMm;
            public double flatHeightMm;
            public int holeCount;
            public int smallHoleCount;
            public int largeHoleCount;
            public List<double> holeX = new List<double>();
            public int bendLineCount;
            public int bendCount;
            public bool bendSequenceValid;
            public bool foldedAndMassValid;
            public bool topologyValid;
            public List<double> expectedFlatBoxMm = new List<double>();
            public List<double> expectedFoldedBoxMm = new List<double>();
            public double expectedVolumeMm3;
            public double expectedMassKg;
            public double expectedSurfaceAreaMm2;
            public int expectedCylinderFaceCount;
            public int expectedFaceCount;
            public int expectedEdgeCount;
            public int expectedLoopCount;
            public int expectedLoopEdgeReferenceCount;
            public int expectedCircleEdgeReferenceCount;
        }

        private sealed class AuthorizationBinding
        {
            public string authorizationId = "";
            public string issuedAtUtc = "";
            public string expiresAtUtc = "";
        }

        private sealed class CylinderSnapshot
        {
            public double centerXmm;
            public double centerYmm;
            public double centerZmm;
            public double axisX;
            public double axisY;
            public double axisZ;
            public double radiusMm;
            public bool Finite
            {
                get
                {
                    return IsFinite(centerXmm) && IsFinite(centerYmm) &&
                        IsFinite(centerZmm) && IsFinite(axisX) && IsFinite(axisY) &&
                        IsFinite(axisZ) && IsFinite(radiusMm) && radiusMm > 0;
                }
            }
        }

        private sealed class GeometrySnapshot
        {
            public int bodyCount;
            public List<double> bodyBoxMm = new List<double>();
            public double volumeMm3;
            public double surfaceAreaMm2;
            public double massKg;
            public int faceCount;
            public int edgeCount;
            public int cylinderFaceCount;
            public int loopCount;
            public int loopEdgeReferenceCount;
            public int circleEdgeReferenceCount;
            public List<double> cylinderRadiiMm = new List<double>();
            public List<CylinderSnapshot> cylinders = new List<CylinderSnapshot>();
            public bool finite;
        }

        private sealed class BendSnapshot
        {
            public string name = "";
            public bool suppressed;
            public int bendType = -1;
            public double angleRadians;
            public int direction = -1;
            public bool down;
            public int order = -1;
            public double radiusMm;
            public double kFactor;
            public double allowanceMm;
            public int allowanceType = -1;
            public bool useDefaultRadius;
            public bool useDefaultAllowance;
        }

        private sealed class FeatureSnapshot
        {
            public int featureCount;
            public int sheetMetalCount;
            public int oneBendCount;
            public int flatPatternCount;
            public double sheetMetalThicknessMm;
            public double sheetMetalRadiusMm;
            public double sheetMetalKFactor;
            public double sheetMetalAllowanceMm;
            public int sheetMetalAllowanceType = -1;
            public List<BendSnapshot> bends = new List<BendSnapshot>();
            public List<string> issues = new List<string>();
            public bool pass;
        }

        private sealed class FreshVerification
        {
            public int openErrors = -1;
            public int openWarnings = -1;
            public bool readOnly;
            public bool rebuilt;
            public int externalReferenceCount = -1;
            public GeometrySnapshot geometry = new GeometrySnapshot();
            public FeatureSnapshot features = new FeatureSnapshot();
            public bool geometryPass;
            public bool featurePass;
            public bool sessionExitProven;
            public string fileSha256Before = "";
            public string fileSha256After = "";
            public bool fileUnchanged;
            public bool pass;
        }

        private sealed class ProcessIdentity
        {
            public int pid;
            public string name = "";
            public long startUtcTicks;
            public string executablePath = "";
            public string executableSha256 = "";
            public int parentPid;
            public string commandLine = "";
            public string exitState = "not_checked";
        }

        private sealed class CadSession
        {
            public string purpose = "";
            public bool created;
            public string revision = "";
            public ProcessIdentity sldworks = new ProcessIdentity();
            public List<ProcessIdentity> monitors = new List<ProcessIdentity>();
            public bool exitRequested;
            public string exitState = "not_checked";
            public bool monitorsExitProven;
            public bool ExitProven
            {
                get { return exitState == "exited" && monitorsExitProven; }
            }
        }

        private sealed class ProcessAudit
        {
            public List<CadSession> sessions = new List<CadSession>();
            public List<int> forceStoppedOwnedProcessIds = new List<int>();
            public List<int> finalSldworks = new List<int>();
            public List<int> finalMonitors = new List<int>();
            public List<string> cleanupErrors = new List<string>();
            public bool finalGate;
        }

        private enum CaptureIdentityResult
        {
            Captured,
            Exited,
            Unproven
        }

        private sealed class Point3
        {
            public readonly double x;
            public readonly double y;
            public readonly double z;
            public Point3(double xValue, double yValue, double zValue)
            {
                x = xValue; y = yValue; z = zValue;
            }
            public string Key()
            {
                return x.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    y.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    z.ToString("F6", CultureInfo.InvariantCulture);
            }
            public bool Near(Point3 other, double tolerance)
            {
                return other != null && Math.Abs(x - other.x) <= tolerance &&
                    Math.Abs(y - other.y) <= tolerance &&
                    Math.Abs(z - other.z) <= tolerance;
            }
        }

        private sealed class ContractException : Exception
        {
            public ContractException(string message) : base(message) { }
        }
    }
}
