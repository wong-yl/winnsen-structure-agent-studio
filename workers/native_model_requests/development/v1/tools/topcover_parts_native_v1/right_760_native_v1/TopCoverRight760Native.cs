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

namespace Winnsen.StructureAgent.TopCoverRight760NativeV1
{
    internal static class TopCoverRight760Native
    {
        private const string ToolId = "topcover_right_760_native_v1";
        private const string Mode = "right-760-pilot";
        private const string RuleSchema = "winnsen.16029.topcover_right_native_rule.v2";
        private const string RuleRelativePath =
            "data/native_model_requests/attempts/NATIVE-20260824T132708Z-888W14D-RIGHT-RULE-R1/" +
            "attempt-0001/evidence/private/topcover_right_native_rule_v2.json";
        private const string RuleSha256 =
            "EFAB0AE94FC50C0ECC05CA2384D24A93DE3E276918BAAB68CA96EFADBCAEF1F0";
        private const string SignatureCorrectionSchema =
            "winnsen.16029.topcover_right_native_signature_correction.v1";
        private const string SignatureCorrectionRelativePath =
            "data/native_model_requests/attempts/NATIVE-20260826T023534Z-888W14D-RIGHT-SIGNATURE-R1/" +
            "attempt-0001/evidence/private/topcover_right_native_signature_correction_v1.json";
        private const string SignatureCorrectionSha256 =
            "39C2E5BB2A44EDEF64BF40C4C5DB5023B0FD0BC27F7A8304662B6CEDA0CC7507";
        private const string EvidenceFileName = "topcover_right_760_native_pilot.json";
        private const string ReceiptFileName = "topcover_right_760_native_pilot.json";
        private const string AuthorizationFileName = "topcover_right_760_native_v1.json";
        private const string AuthorizationSchema =
            "winnsen.16029.topcover_right_native_authorization.v1";
        private const string ReceiptSchema =
            "winnsen.16029.topcover_right_native_receipt.v1";
        private const string PilotOperation = "create_new_right_760_native_pilot";
        private const string NativeRouteId =
            "right_760_sharp_stock_insert_bends_v1";
        private const string SharpReferenceRelativePath =
            "data/native_model_requests/attempts/" +
            "TOPCOVER-REFERENCE-20260828T001317Z-LEFT-SHARP-R9/attempt-0001/" +
            "evidence/private/topcover_left_reference_brep_capture_v7.json";
        private const string SharpReferenceSha256 =
            "9B841451011285DE50EFBC35F979519B7B1082FF411C86852B67BBA45A8952B8";
        private const string OutputRelativePath =
            "native_cad/right_760_native_pilot/上盖壳体右侧板.SLDPRT";
        private const string EvidenceRelativePath =
            "evidence/private/topcover_right_760_native_pilot.json";
        private const string ReceiptRelativePath =
            "receipts/topcover_right_760_native_pilot.json";
        private const string ExpectedSourceSha256 =
            "1D4397E5438F890A8C59F837A87DFE064627B91EDBDD0325303555CE01E64F15";
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
            "schema", "generatedAtUtc", "purpose", "inputs", "partName",
            "widthRelation", "dxfToWidth760Model", "sheetMetal",
            "manufacturingFlat760", "bends760", "expectedFolded760",
            "nativeBuildSequence", "right760NativePilotContractReady",
            "right888NativeBuildReady", "nativeCadCreated", "unresolved",
            "qualityBoundary"
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
                Require(contract.widthMm == 760 && contract.outlineLineCount == 22 &&
                    contract.knockoutLoopCount == 4 && contract.holeCount == 11 &&
                    contract.pemBossCount == 3 && contract.bendCount == 5,
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
            Console.WriteLine("TopCoverRight760Native right-side 760 native pilot");
            Console.WriteLine("  --help | --identity | --self-test");
            Console.WriteLine("  --mode right-760-pilot --rule <fixed-rule> " +
                "--attempt-dir <TOPCOVER-RIGHT-PILOT-.../attempt-0001> " +
                "--authorization <attempt/execution_authorizations/topcover_right_760_native_v1.json> " +
                "--authorization-sha256 <SHA256> --out <private-json>");
        }

        private static int RunSelfTests()
        {
            Dictionary<string, bool> checks = new Dictionary<string, bool>(
                StringComparer.Ordinal);
            string failure = null;
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
                checks["signature_correction_is_fixed_and_live"] =
                    contract.signatureCorrectionValid &&
                    contract.expectedFlatDetailedSignature ==
                        "DECF4787EFF44B05A148527A7A512ED27D2539182D6A8909D9C28E8E6B2CC0B6" &&
                    contract.expectedFoldedDetailedSignature ==
                        "4698418C907999F50B193BDC3C6F5E5DC2C6DED8E8D270C2E05E4D6C8CBBFECF";
                checks["outer_and_knockout_loops_are_exact"] =
                    contract.outlineLineCount == 22 && contract.outlineVertexCount == 22 &&
                    contract.knockoutLoopCount == 4 && contract.knockoutLineCount == 48;
                checks["manufacturing_flat_bbox_is_exact"] =
                    contract.expectedFlatBoxMm.Count == 6 &&
                    Near(contract.expectedFlatBoxMm[0], 299.8, 0.000001) &&
                    Near(contract.expectedFlatBoxMm[1], 1912.2, 0.000001) &&
                    Near(contract.expectedFlatBoxMm[5], -47.0, 0.000001);
                checks["eleven_holes_and_three_pem_bosses_are_exact"] =
                    contract.holeCount == 11 && contract.pemBossCount == 3 &&
                    SequenceNear(contract.holeRadiiMm.OrderBy(value => value), new[]
                    { 1.0, 1.0, 2.75, 2.75, 2.75, 2.75, 3.25, 4.4, 4.4, 4.4, 5.0 },
                        1e-6);
                checks["five_bend_sequence_is_exact"] =
                    contract.bendCount == 5 && contract.bendSequenceValid &&
                    contract.bendLineNames.SequenceEqual(new[]
                        { "直线7", "直线8", "直线9", "直线6", "直线10" });
                checks["flat_and_folded_topology_are_exact"] =
                    contract.flatTopologyValid && contract.topologyValid &&
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
                    AuthorizationFileName == "topcover_right_760_native_v1.json" &&
                    ReceiptRelativePath == "receipts/topcover_right_760_native_pilot.json";
                checks["runtime_tolerances_are_bounded"] = BoxToleranceMm <= 0.02 &&
                    VolumeToleranceMm3 <= 2.0 && AreaToleranceMm2 <= 5.0 &&
                    MassToleranceKg <= 0.00002;
                string sharpReferencePath = Path.Combine(repositoryRoot,
                    SharpReferenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
                checks["fixed_left_sharp_reference_is_live"] =
                    File.Exists(sharpReferencePath) &&
                    FileLinkCount(sharpReferencePath) == 1 &&
                    !HasReparsePoint(sharpReferencePath) &&
                    string.Equals(Sha256File(sharpReferencePath), SharpReferenceSha256,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }
            string[] required =
            {
                "fixed_rule_exists_and_is_single_link", "fixed_rule_sha256",
                "strict_schema_and_exact_keys", "input_evidence_hashes_are_live",
                "signature_correction_is_fixed_and_live",
                "outer_and_knockout_loops_are_exact",
                "manufacturing_flat_bbox_is_exact",
                "eleven_holes_and_three_pem_bosses_are_exact",
                "five_bend_sequence_is_exact",
                "flat_and_folded_topology_are_exact",
                "sheet_metal_contract_is_exact",
                "folded_bbox_mass_and_topology_are_exact",
                "nonfinite_json_tokens_are_rejected",
                "attempt_shape_rejects_nonpilot_task",
                "source_identity_is_normalized_and_embedded",
                "runtime_requires_one_fixed_authorization",
                "runtime_tolerances_are_bounded",
                "fixed_left_sharp_reference_is_live"
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
                { "failure", failure },
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
                "right_760_native_pilot");
            string partPath = Path.Combine(partDirectory, "上盖壳体右侧板.SLDPRT");
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
                { "schema", "winnsen.16029.topcover_right_native_pilot.v1" },
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
                { "nativeRouteId", NativeRouteId },
                { "sharpReferenceRelativePath", SharpReferenceRelativePath },
                { "sharpReferenceSha256", SharpReferenceSha256 },
                { "qualityBoundary", "760 right-side native pilot regression only; structural engineer review remains required." },
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
                BuildNativeRightSharpInsertBendsCore(application, model, evidence);
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
                evidence["status"] = "TOPCOVER_RIGHT_760_NATIVE_PILOT_COMPLETE";
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
                evidence["status"] = "TOPCOVER_RIGHT_760_NATIVE_PILOT_FAILED";
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
                Console.Error.WriteLine("TOPCOVER_RIGHT_760_NATIVE_PILOT_FAILED: " +
                    SafeException(exception));
                return PilotFailureExitCode;
            }
            finally
            {
                CloseDocument(application, ref model);
                Release(application);
            }
        }

        private static void BuildNativeRightSharpInsertBendsCore(ISldWorks application,
            ModelDoc2 model, Dictionary<string, object> evidence)
        {
            MathUtility math = application.GetMathUtility() as MathUtility;
            Feature startPlane = null;
            Feature endPlane = null;
            Feature thinExtrusion = null;
            try
            {
                Require(math != null, "SHARP_STOCK_MATH_UTILITY_UNAVAILABLE");
                startPlane = CreateFrontOffsetPlane(model, -549.0);
                Require(startPlane != null, "SHARP_STOCK_START_PLANE_CREATE_FAILED");
                endPlane = CreateFrontOffsetPlane(model, -47.0);
                Require(endPlane != null, "SHARP_STOCK_END_PLANE_CREATE_FAILED");
                evidence["sharpStartPlaneTransform16"] = RefPlaneTransform(startPlane);
                evidence["sharpEndPlaneTransform16"] = RefPlaneTransform(endPlane);

                Require(startPlane.Select2(false, 0),
                    "SHARP_STOCK_START_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                Point3[] path =
                {
                    new Point3(320.8, 1894.4, -549.0),
                    new Point3(330.4, 1894.4, -549.0),
                    new Point3(330.4, 1916.6, -549.0),
                    new Point3(379.6, 1916.6, -549.0),
                    new Point3(379.6, 1839.6, -549.0),
                    new Point3(365.4, 1839.6, -549.0),
                    new Point3(365.4, 1851.2, -549.0)
                };
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    for (int index = 0; index < path.Length - 1; index++)
                    {
                        Point3 start = ToActiveSketchPoint(model, math, path[index]);
                        Point3 end = ToActiveSketchPoint(model, math, path[index + 1]);
                        Require(model.SketchManager.CreateLine(start.x, start.y, 0,
                            end.x, end.y, 0) != null,
                            "SHARP_STOCK_OPEN_PROFILE_LINE_FAILED");
                    }
                }
                finally
                {
                    TryAction(() => model.SetDisplayWhenAdded(true));
                    TryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                Feature profile = LastRootFeatureOfType(model, "ProfileFeature");
                Require(profile != null && profile.Select2(false, 0),
                    "SHARP_STOCK_PROFILE_SELECT_FAILED");
                thinExtrusion = model.FeatureManager.FeatureExtrusionThin2(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    0.502, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true,
                    0.0008, 0.0, 0.0,
                    (int)swThinWallType_e.swThinWallMidPlane, 0, false, 0.0,
                    false, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                Release(profile);
                Require(thinExtrusion != null && thinExtrusion.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), "SHARP_STOCK_THIN_EXTRUSION_FAILED");

                CreateBlindRectangleCut(model, math, startPlane, -549.0,
                    320.8, 1894.0, 330.0, 1894.8, 60.7, true,
                    "SHARP_STOCK_BOTTOM_TERMINAL_RELIEF");
                CreateBlindRectangleCut(model, math, startPlane, -549.0,
                    330.0, 1894.0, 330.8, 1916.2, 50.2, true,
                    "SHARP_STOCK_BOTTOM_VERTICAL_RELIEF");
                CreateBlindRectangleCut(model, math, startPlane, -549.0,
                    365.0, 1840.0, 365.8, 1851.2, 13.2, true,
                    "SHARP_STOCK_BOTTOM_RETURN_RELIEF");
                CreateBlindRectangleCut(model, math, endPlane, -47.0,
                    320.8, 1894.0, 330.0, 1894.8, 10.7, false,
                    "SHARP_STOCK_TOP_TERMINAL_RELIEF");
                CreateBlindRectangleCut(model, math, endPlane, -47.0,
                    330.0, 1894.0, 330.8, 1916.2, 0.2, false,
                    "SHARP_STOCK_TOP_VERTICAL_RELIEF");

                Require(SelectFaceByRay(model, 0.5, 1.8642, -0.3,
                    -1, 0, 0), "SHARP_STOCK_MAIN_HOLE_FACE_SELECT_FAILED");
                CreateThroughHolesOnSelectedFace(model, math, new[]
                {
                    new HolePoint(380.0, 1864.2, -448.0, 1.0),
                    new HolePoint(380.0, 1864.2, -148.0, 1.0)
                }, "SHARP_STOCK_MAIN_HOLES");
                Require(SelectFaceByRay(model, 0.34, 1.847, -0.3,
                    1, 0, 0), "SHARP_STOCK_RETURN_HOLE_FACE_SELECT_FAILED");
                CreateThroughHolesOnSelectedFace(model, math, new[]
                {
                    new HolePoint(365.0, 1847.0, -65.0, 2.75),
                    new HolePoint(365.0, 1847.0, -215.0, 2.75),
                    new HolePoint(365.0, 1847.0, -365.0, 2.75),
                    new HolePoint(365.0, 1847.0, -515.0, 2.75)
                }, "SHARP_STOCK_RETURN_HOLES");
                Require(SelectFaceByRay(model, 0.2, 1.906, -0.3,
                    1, 0, 0), "SHARP_STOCK_INNER_HOLE_FACE_SELECT_FAILED");
                CreateThroughHolesOnSelectedFace(model, math, new[]
                {
                    new HolePoint(330.0, 1911.1, -489.0, 3.25),
                    new HolePoint(330.0, 1906.0, -85.0, 5.0)
                }, "SHARP_STOCK_INNER_HOLES");

                Require(model.ForceRebuild3(false), "SHARP_STOCK_FINAL_REBUILD_FAILED");
                GeometrySnapshot sharp = CaptureGeometry(model);
                evidence["sharpStockGeometryDiagnostic"] = sharp;
                bool sharpPass = SharpStockMatchesReference(sharp);
                evidence["sharpStockGeometryPass"] = sharpPass;
                Require(sharpPass, "NATIVE_SHARP_STOCK_GEOMETRY_REGRESSION_FAILED");
                AddBuildStage(evidence, "native_sharp_stock", sharp);

                Require(SelectFixedFaceByRay(model, false, 0),
                    "INSERT_BENDS_FIXED_FACE_SELECT_FAILED");
                PartDoc part = model as PartDoc;
                Require(part != null && part.InsertBends2(0.0005, "", 0.5, 0.0,
                    true, 0.5, true), "NATIVE_INSERT_BENDS2_FAILED");
                Require(model.ForceRebuild3(false),
                    "NATIVE_INSERT_BENDS2_REBUILD_FAILED");
                evidence["insertBendsParameterBoundary"] =
                    new Dictionary<string, object>
                    {
                        { "preserveNativeKFactor", true },
                        { "preserveGlobalRadiusMm", 0.5 },
                        { "deductionOverrideApplied", false },
                        { "reason", "SW2020 nested sharp bends return error 51 when " +
                            "the parent is changed to global R0.2 or deduction 1.4." }
                    };
                FeatureSnapshot insertFeatures = CaptureFeatureSnapshot(model);
                GeometrySnapshot insertGeometry = CaptureGeometry(model);
                bool insertPass = InsertBendsCoreMatches(insertFeatures);
                evidence["insertBendsCoreFeatureDiagnostic"] = insertFeatures;
                evidence["insertBendsCoreGeometryDiagnostic"] = insertGeometry;
                evidence["insertBendsCorePass"] = insertPass;
                Require(insertPass, "NATIVE_INSERT_BENDS_CORE_REGRESSION_FAILED");
                AddBuildStage(evidence, "native_insert_bends_core", insertFeatures);
                CreateNativeRightPostFeatures(model, math, evidence);
            }
            finally
            {
                Release(thinExtrusion);
                Release(endPlane);
                Release(startPlane);
                Release(math);
            }
        }

        private static Feature CreateFrontOffsetPlane(ModelDoc2 model, double offsetMm)
        {
            string name;
            List<double> transform;
            Require(SelectDefaultFrontPlane(model, out name, out transform),
                "DEFAULT_FRONT_PLANE_SELECT_FAILED");
            int constraint =
                (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
            if (offsetMm < 0.0)
                constraint |= (int)swRefPlaneReferenceConstraints_e.
                    swRefPlaneReferenceConstraint_OptionFlip;
            Feature plane = model.FeatureManager.InsertRefPlane(
                constraint, Math.Abs(offsetMm) / 1000.0, 0, 0, 0, 0) as Feature;
            List<double> values = RefPlaneTransform(plane);
            Require(plane != null && values.Count >= 12 &&
                Near(values[9], 0.0, 0.000001) &&
                Near(values[10], 0.0, 0.000001) &&
                Near(values[11], offsetMm / 1000.0, 0.000001),
                "FRONT_OFFSET_PLANE_TRANSFORM_INVALID");
            return plane;
        }

        private static void CreateBlindRectangleCut(ModelDoc2 model, MathUtility math,
            Feature plane, double zMm, double x1Mm, double y1Mm, double x2Mm,
            double y2Mm, double depthMm, bool reverseDirection, string failurePrefix)
        {
            Feature cut = null;
            Feature sketchFeature = null;
            try
            {
                model.ClearSelection2(true);
                Require(plane != null && plane.Select2(false, 0),
                    failurePrefix + "_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                Point3[] points =
                {
                    new Point3(x1Mm, y1Mm, zMm),
                    new Point3(x2Mm, y1Mm, zMm),
                    new Point3(x2Mm, y2Mm, zMm),
                    new Point3(x1Mm, y2Mm, zMm)
                };
                List<SketchSegment> rectangle = new List<SketchSegment>();
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    for (int index = 0; index < points.Length; index++)
                    {
                        Point3 start = ToActiveSketchPoint(model, math, points[index]);
                        Point3 end = ToActiveSketchPoint(model, math,
                            points[(index + 1) % points.Length]);
                        SketchSegment segment = model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) as SketchSegment;
                        Require(segment != null, failurePrefix + "_LINE_FAILED_" +
                            index.ToString(CultureInfo.InvariantCulture) + "_" +
                            start.Key() + "_" + end.Key());
                        rectangle.Add(segment);
                    }
                }
                finally
                {
                    TryAction(() => model.SetDisplayWhenAdded(true));
                    TryAction(() => model.SetAddToDB(false));
                    foreach (SketchSegment segment in rectangle) Release(segment);
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = LastRootFeatureOfType(model, "ProfileFeature");
                Require(sketchFeature != null && sketchFeature.Select2(false, 0),
                    failurePrefix + "_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, reverseDirection,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depthMm / 1000.0, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                Require(cut != null && cut.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), failurePrefix + "_CUT_FAILED");
            }
            finally
            {
                Release(cut);
                Release(sketchFeature);
                model.ClearSelection2(true);
            }
        }

        private static void CreateThroughHolesOnSelectedFace(ModelDoc2 model,
            MathUtility math, IEnumerable<HolePoint> holes, string failurePrefix)
        {
            Feature cut = null;
            Feature sketchFeature = null;
            try
            {
                model.SketchManager.InsertSketch(true);
                foreach (HolePoint hole in holes)
                {
                    Point3 center = ToActiveSketchPoint(model, math,
                        new Point3(hole.xMm, hole.yMm, hole.zMm));
                    Require(model.SketchManager.CreateCircleByRadius(center.x, center.y,
                        0, hole.radiusMm / 1000.0) != null,
                        failurePrefix + "_CIRCLE_FAILED");
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = LastRootFeatureOfType(model, "ProfileFeature");
                Require(sketchFeature != null && sketchFeature.Select2(false, 0),
                    failurePrefix + "_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    0.001, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                Require(cut != null && cut.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), failurePrefix + "_CUT_FAILED");
            }
            finally
            {
                Release(cut);
                Release(sketchFeature);
                model.ClearSelection2(true);
            }
        }

        private static bool SelectFaceByRay(ModelDoc2 model, double x, double y,
            double z, double dx, double dy, double dz)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                model.ClearSelection2(true);
                return extension != null && extension.SelectByRay(x, y, z, dx, dy, dz,
                    0.001, (int)swSelectType_e.swSelFACES, false, 0, 0);
            }
            finally { Release(extension); }
        }

        private static bool SharpStockMatchesReference(GeometrySnapshot geometry)
        {
            double[] box = { 320.8, 1839.2, -549.0, 380.0, 1917.0, -47.0 };
            double[] radii = { 1.0, 1.0, 2.75, 2.75, 2.75, 2.75, 3.25, 5.0 };
            return geometry != null && geometry.bodyCount == 1 && geometry.finite &&
                geometry.bodyBoxMm.Count == 6 && box.Select((value, index) =>
                    Near(geometry.bodyBoxMm[index], value, BoxToleranceMm)).All(value => value) &&
                Near(geometry.volumeMm3, 72104.76859853993, VolumeToleranceMm3) &&
                Near(geometry.surfaceAreaMm2, 181466.01564657912,
                    AreaToleranceMm2) &&
                Near(geometry.massKg, 0.5660224334985384, MassToleranceKg) &&
                geometry.faceCount == 29 && geometry.edgeCount == 73 &&
                geometry.loopCount == 53 && geometry.loopEdgeReferenceCount == 146 &&
                geometry.cylinderFaceCount == 8 &&
                geometry.circleEdgeReferenceCount == 32 &&
                RadiiMatch(geometry.cylinderRadiiMm, radii);
        }

        private static bool InsertBendsCoreMatches(FeatureSnapshot value)
        {
            return value != null && value.sheetMetalCount == 1 &&
                value.oneBendCount == 5 && value.flatPatternCount == 1 &&
                value.flattenBendsCount == 1 && value.processBendsCount == 1 &&
                value.sketchedBendGroupCount == 0 && value.bends.Count == 5 &&
                value.issues.Count == 0 &&
                Near(value.sheetMetalThicknessMm, 0.8, 0.0001) &&
                Near(value.sheetMetalRadiusMm, 0.5, 0.0001) &&
                Near(value.sheetMetalKFactor, 0.5, 0.000001) &&
                value.sheetMetalAllowanceType ==
                    (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                value.bends.All(bend => bend.bendType ==
                    (int)swBendType_e.swSharpBend && !bend.suppressed &&
                    Near(bend.angleRadians, Math.PI / 2.0, 0.000001) &&
                    Near(bend.radiusMm, 0.5, 0.0001) &&
                    Near(bend.kFactor, 0.5, 0.000001) &&
                    Near(bend.deductionMm, 0.0, 0.000001) &&
                    bend.allowanceType ==
                        (int)swBendAllowanceTypes_e.swBendAllowanceKFactor) &&
                value.bends.Count(bend => bend.canonicalDirection == 2 &&
                    bend.canonicalDown) == 4 &&
                value.bends.Count(bend => bend.canonicalDirection == 1 &&
                    !bend.canonicalDown) == 1;
        }

        private static void CreateNativeRightPostFeatures(ModelDoc2 model,
            MathUtility math, Dictionary<string, object> evidence)
        {
            Require(SelectFaceByRay(model, 0.355, 2.0, -0.15, 0, -1, 0),
                "POST_TOP_FACE_SELECT_FAILED");
            CreateCutProfileOnSelectedFace(model, math, new[]
            {
                new[]
                {
                    new Point3(375, 1917, -180), new Point3(375, 1917, -120),
                    new Point3(358, 1917, -120), new Point3(358, 1917, -118),
                    new Point3(356, 1917, -118), new Point3(356, 1917, -122),
                    new Point3(373, 1917, -122), new Point3(373, 1917, -178),
                    new Point3(356, 1917, -178), new Point3(356, 1917, -182),
                    new Point3(358, 1917, -182), new Point3(358, 1917, -180)
                },
                new[]
                {
                    new Point3(337, 1917, -178), new Point3(337, 1917, -122),
                    new Point3(354, 1917, -122), new Point3(354, 1917, -118),
                    new Point3(352, 1917, -118), new Point3(352, 1917, -120),
                    new Point3(335, 1917, -120), new Point3(335, 1917, -180),
                    new Point3(352, 1917, -180), new Point3(352, 1917, -182),
                    new Point3(354, 1917, -182), new Point3(354, 1917, -178)
                }
            }, new[]
            {
                new HolePoint(355, 1917, -100, 4.4),
                new HolePoint(355, 1917, -200, 4.4),
                new HolePoint(355, 1917, -420, 4.4)
            }, 1.0, false, "POST_TOP_CUTS");

            Require(SelectFaceByRay(model, 0.5, 1.88, -0.15, -1, 0, 0),
                "POST_OUTER_FACE_SELECT_FAILED");
            CreateCutProfileOnSelectedFace(model, math, new[]
            {
                new[]
                {
                    new Point3(380, 1869, -132), new Point3(380, 1886, -132),
                    new Point3(380, 1886, -136), new Point3(380, 1884, -136),
                    new Point3(380, 1884, -134), new Point3(380, 1867, -134),
                    new Point3(380, 1867, -74), new Point3(380, 1884, -74),
                    new Point3(380, 1884, -72), new Point3(380, 1886, -72),
                    new Point3(380, 1886, -76), new Point3(380, 1869, -76)
                },
                new[]
                {
                    new Point3(380, 1907, -134), new Point3(380, 1890, -134),
                    new Point3(380, 1890, -136), new Point3(380, 1888, -136),
                    new Point3(380, 1888, -132), new Point3(380, 1905, -132),
                    new Point3(380, 1905, -76), new Point3(380, 1888, -76),
                    new Point3(380, 1888, -72), new Point3(380, 1890, -72),
                    new Point3(380, 1890, -74), new Point3(380, 1907, -74)
                }
            }, new HolePoint[0], 1.0, false, "POST_OUTER_CUTS");

            Require(SelectFaceByRay(model, 0.355, 1.9, -0.3, 0, 1, 0),
                "POST_PEM_UNDERSIDE_FACE_SELECT_FAILED");
            CreatePemBossesOnSelectedFace(model, math, new[]
            {
                new Point3(355, 1916.2, -100),
                new Point3(355, 1916.2, -200),
                new Point3(355, 1916.2, -420)
            });
            Require(model.ForceRebuild3(false), "POST_FEATURES_REBUILD_FAILED");
            GeometrySnapshot geometry = CaptureGeometry(model);
            FeatureSnapshot features = CaptureFeatureSnapshot(model);
            evidence["postFeatureGeometryDiagnostic"] = geometry;
            evidence["postFeatureFeatureDiagnostic"] = features;
            AddBuildStage(evidence, "native_post_cuts_and_pem", geometry);
        }

        private static void CreateCutProfileOnSelectedFace(ModelDoc2 model,
            MathUtility math, IEnumerable<Point3[]> loops, IEnumerable<HolePoint> holes,
            double depthMm, bool reverseDirection, string failurePrefix)
        {
            List<SketchSegment> segments = new List<SketchSegment>();
            Feature sketchFeature = null;
            Feature cut = null;
            try
            {
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (Point3[] loop in loops)
                    {
                        Require(loop != null && loop.Length >= 3,
                            failurePrefix + "_LOOP_INVALID");
                        for (int index = 0; index < loop.Length; index++)
                        {
                            Point3 start = ToActiveSketchPoint(model, math, loop[index]);
                            Point3 end = ToActiveSketchPoint(model, math,
                                loop[(index + 1) % loop.Length]);
                            SketchSegment segment = model.SketchManager.CreateLine(start.x,
                                start.y, 0, end.x, end.y, 0) as SketchSegment;
                            Require(segment != null, failurePrefix + "_LINE_FAILED");
                            segments.Add(segment);
                        }
                    }
                    foreach (HolePoint hole in holes)
                    {
                        Point3 center = ToActiveSketchPoint(model, math,
                            new Point3(hole.xMm, hole.yMm, hole.zMm));
                        SketchSegment circle = model.SketchManager.CreateCircleByRadius(
                            center.x, center.y, 0, hole.radiusMm / 1000.0) as SketchSegment;
                        Require(circle != null, failurePrefix + "_CIRCLE_FAILED");
                        segments.Add(circle);
                    }
                }
                finally
                {
                    TryAction(() => model.SetDisplayWhenAdded(true));
                    TryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = LastRootFeatureOfType(model, "ProfileFeature");
                Require(sketchFeature != null && sketchFeature.Select2(false, 0),
                    failurePrefix + "_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, reverseDirection,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depthMm / 1000.0, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                Require(cut != null && cut.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), failurePrefix + "_CUT_FAILED");
            }
            finally
            {
                Release(cut);
                Release(sketchFeature);
                foreach (SketchSegment segment in segments) Release(segment);
                model.ClearSelection2(true);
            }
        }

        private static void CreatePemBossesOnSelectedFace(ModelDoc2 model,
            MathUtility math, IEnumerable<Point3> centers)
        {
            List<SketchSegment> segments = new List<SketchSegment>();
            Feature sketchFeature = null;
            Feature boss = null;
            try
            {
                model.SketchManager.InsertSketch(true);
                foreach (Point3 modelCenter in centers)
                {
                    Point3 center = ToActiveSketchPoint(model, math, modelCenter);
                    SketchSegment outer = model.SketchManager.CreateCircleByRadius(center.x,
                        center.y, 0, 0.0055) as SketchSegment;
                    SketchSegment inner = model.SketchManager.CreateCircleByRadius(center.x,
                        center.y, 0, 0.0030) as SketchSegment;
                    Require(outer != null && inner != null,
                        "POST_PEM_CIRCLE_CREATE_FAILED");
                    segments.Add(outer);
                    segments.Add(inner);
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = LastRootFeatureOfType(model, "ProfileFeature");
                Require(sketchFeature != null && sketchFeature.Select2(false, 0),
                    "POST_PEM_SKETCH_SELECT_FAILED");
                boss = model.FeatureManager.FeatureExtrusion2(true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind, 0.004, 0.0,
                    false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true, true, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false) as Feature;
                Require(boss != null && boss.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), "POST_PEM_EXTRUSION_FAILED");
            }
            finally
            {
                Release(boss);
                Release(sketchFeature);
                foreach (SketchSegment segment in segments) Release(segment);
                model.ClearSelection2(true);
            }
        }

        private static bool SelectDefaultFrontPlane(ModelDoc2 model,
            out string planeName, out List<double> transformValues)
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
                                Near(values[9], 0, 1e-10) &&
                                Near(values[10], 0, 1e-10) &&
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

        private static bool SelectFixedFaceByRay(ModelDoc2 model, bool append, int mark,
            bool fromBelow = false)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                return extension != null && extension.SelectByRay(0.355,
                    fromBelow ? 1.8 : 2.0, -0.3, 0,
                    fromBelow ? 1.0 : -1.0, 0, 0.001,
                    (int)swSelectType_e.swSelFACES, append, mark, 0);
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
                        CaptureDetailedFace(face, output);
                    }
                }
                finally { Release(body); }
            }
            finally { }
            output.cylinderFaceCount = output.cylinders.Count;
            output.cylinderRadiiMm = output.cylinders.Select(value => value.radiusMm)
                .OrderBy(value => value).ToList();
            output.translationNormalizedDetailedSignature =
                DetailedGeometrySignature(output);
            output.finite = output.bodyBoxMm.Count == 6 &&
                output.bodyBoxMm.All(IsFinite) && IsFinite(output.volumeMm3) &&
                IsFinite(output.surfaceAreaMm2) && IsFinite(output.massKg) &&
                output.cylinders.All(value => value.Finite) &&
                output.faces.Count == output.faceCount &&
                output.loopCount == output.faces.Sum(value => value.loops.Count) &&
                output.loopEdgeReferenceCount == output.faces.Sum(value =>
                    value.loops.Sum(loop => loop.edges.Count)) &&
                Regex.IsMatch(output.translationNormalizedDetailedSignature ?? "",
                    "^[A-F0-9]{64}$", RegexOptions.CultureInvariant);
            return output;
        }

        private static void CaptureDetailedFace(Face2 face, GeometrySnapshot output)
        {
            Require(face != null, "GEOMETRY_FACE_INVALID");
            Surface surface = null;
            try
            {
                DetailedFace detail = new DetailedFace();
                detail.areaMm2 = face.GetArea() * 1000000.0;
                detail.boxMm = DoubleList(face.GetBox()).Select(value =>
                    value * 1000.0).ToList();
                detail.normal = DoubleList(face.Normal);
                Require(IsFinite(detail.areaMm2) && detail.boxMm.Count == 6 &&
                    detail.boxMm.All(IsFinite) && detail.normal.Count == 3 &&
                    detail.normal.All(IsFinite), "DETAILED_FACE_NONFINITE");
                surface = face.GetSurface() as Surface;
                Require(surface != null, "DETAILED_FACE_SURFACE_NULL");
                detail.surfaceIdentity = surface.Identity();
                detail.isPlane = surface.IsPlane();
                detail.isCylinder = surface.IsCylinder();
                detail.planeParams = detail.isPlane ? ToListOrEmpty(surface.PlaneParams,
                    1.0) : new List<double>();
                detail.cylinderParams = detail.isCylinder ? ToListOrEmpty(
                    surface.CylinderParams, 1.0) : new List<double>();
                Require(detail.planeParams.All(IsFinite) &&
                    detail.cylinderParams.All(IsFinite),
                    "DETAILED_SURFACE_PARAMS_NONFINITE");
                if (detail.isCylinder)
                {
                    List<double> parameters = detail.cylinderParams;
                    Require(parameters.Count >= 7, "DETAILED_CYLINDER_PARAMS_INVALID");
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
                    Require(loop != null, "DETAILED_LOOP_NULL");
                    try
                    {
                        DetailedLoop loopDetail = new DetailedLoop
                            { isOuter = loop.IsOuter() };
                        Array edges = loop.GetEdges() as Array;
                        loopDetail.edgeCount = edges == null ? 0 : edges.Length;
                        output.loopEdgeReferenceCount += loopDetail.edgeCount;
                        foreach (object rawEdge in edges ?? new object[0])
                            loopDetail.edges.Add(CaptureDetailedEdge(rawEdge as Edge,
                                output));
                        Require(loopDetail.edgeCount == loopDetail.edges.Count,
                            "DETAILED_LOOP_EDGE_COUNT_INVALID");
                        detail.loops.Add(loopDetail);
                    }
                    finally { Release(loop); }
                }
                output.faces.Add(detail);
            }
            finally { Release(surface); Release(face); }
        }

        private static DetailedEdge CaptureDetailedEdge(Edge edge,
            GeometrySnapshot output)
        {
            Require(edge != null, "DETAILED_EDGE_NULL");
            Curve curve = null;
            try
            {
                curve = edge.GetCurve() as Curve;
                Require(curve != null, "DETAILED_EDGE_CURVE_NULL");
                DetailedEdge detail = new DetailedEdge();
                detail.curveType = curve.Identity();
                detail.isLine = curve.IsLine();
                detail.isCircle = curve.IsCircle();
                double minimum = 0.0, maximum = 0.0;
                bool periodic = false, closed = false;
                Require(curve.GetEndParams(out minimum, out maximum, out periodic,
                    out closed), "DETAILED_EDGE_END_PARAMS_UNRESOLVED");
                detail.uMin = minimum;
                detail.uMax = maximum;
                detail.lengthMm = curve.GetLength3(minimum, maximum) * 1000.0;
                detail.startPointMm = EdgePoint(curve, minimum);
                detail.endPointMm = EdgePoint(curve, maximum);
                detail.startVertexPointMm = VertexPoint(edge.GetStartVertex() as Vertex);
                detail.endVertexPointMm = VertexPoint(edge.GetEndVertex() as Vertex);
                detail.lineParams = detail.isLine ? ToListOrEmpty(curve.LineParams, 1.0) :
                    new List<double>();
                detail.circleParams = detail.isCircle ? ToListOrEmpty(curve.CircleParams,
                    1.0) : new List<double>();
                Require(IsFinite(detail.uMin) && IsFinite(detail.uMax) &&
                    IsFinite(detail.lengthMm) && detail.lengthMm > 0 &&
                    detail.startPointMm.Count == 3 && detail.endPointMm.Count == 3 &&
                    detail.startPointMm.All(IsFinite) && detail.endPointMm.All(IsFinite) &&
                    detail.startVertexPointMm.All(IsFinite) &&
                    detail.endVertexPointMm.All(IsFinite) &&
                    detail.lineParams.All(IsFinite) && detail.circleParams.All(IsFinite),
                    "DETAILED_EDGE_NONFINITE");
                if (detail.isCircle) output.circleEdgeReferenceCount++;
                return detail;
            }
            finally { Release(curve); Release(edge); }
        }

        private static string DetailedGeometrySignature(GeometrySnapshot snapshot)
        {
            List<double> anchor = new List<double>
                { snapshot.bodyBoxMm[0], snapshot.bodyBoxMm[1], snapshot.bodyBoxMm[2] };
            List<string> faces = snapshot.faces.Select(face =>
                DetailedFaceKey(face, anchor)).OrderBy(value => value,
                    StringComparer.Ordinal).ToList();
            return Sha256Bytes(Encoding.UTF8.GetBytes("faces=" +
                String.Join("|", faces)));
        }

        private static string DetailedFaceKey(DetailedFace face, List<double> anchor)
        {
            List<string> loops = face.loops.Select(loop =>
                (loop.isOuter ? "O" : "I") + ":" + String.Join(",",
                    loop.edges.Select(edge => DetailedEdgeKey(edge, anchor)).OrderBy(
                        value => value, StringComparer.Ordinal))).OrderBy(value => value,
                            StringComparer.Ordinal).ToList();
            List<double> normal = CanonicalAxis(face.normal);
            string surface = "surface=" + face.surfaceIdentity.ToString(
                    CultureInfo.InvariantCulture) + ",plane=" + BoolKey(face.isPlane) +
                ",cylinder=" + DetailedCylinderKey(face.cylinderParams, anchor);
            return "n=" + String.Join(",", normal.Select(RoundDetailed)) + ";" +
                surface + ";loops=" + String.Join("/", loops);
        }

        private static string DetailedEdgeKey(DetailedEdge edge, List<double> anchor)
        {
            bool fullCircle = edge.isCircle &&
                (SameDetailedPoint(edge.startPointMm, edge.endPointMm) ||
                    edge.startVertexPointMm == null ||
                    edge.startVertexPointMm.Count != 3 ||
                    edge.endVertexPointMm == null ||
                    edge.endVertexPointMm.Count != 3);
            string points;
            string vertices;
            if (fullCircle)
            {
                points = "FULL";
                vertices = "FULL";
            }
            else
            {
                points = String.Join(">", new[]
                {
                    PointKey(edge.startPointMm, anchor),
                    PointKey(edge.endPointMm, anchor)
                }.OrderBy(value => value, StringComparer.Ordinal));
                vertices = String.Join(">", new[]
                {
                    PointKey(edge.startVertexPointMm, anchor),
                    PointKey(edge.endVertexPointMm, anchor)
                }.OrderBy(value => value, StringComparer.Ordinal));
            }
            return "t=" + edge.curveType.ToString(CultureInfo.InvariantCulture) +
                ",l=" + BoolKey(edge.isLine) + ",c=" + BoolKey(edge.isCircle) +
                ",len=" + RoundDetailed(edge.lengthMm) + ",p=" + points +
                ",v=" + vertices + ",cp=" +
                DetailedCylinderKey(edge.circleParams, anchor);
        }

        private static string DetailedCylinderKey(List<double> parameters,
            List<double> anchor)
        {
            if (parameters == null || parameters.Count < 7) return "[]";
            List<double> axis = CanonicalAxis(parameters.Skip(3).Take(3).ToList());
            List<double> center = new List<double>
            {
                parameters[0] * 1000.0 - anchor[0],
                parameters[1] * 1000.0 - anchor[1],
                parameters[2] * 1000.0 - anchor[2]
            };
            double parallel = center[0] * axis[0] + center[1] * axis[1] +
                center[2] * axis[2];
            List<double> perpendicular = new List<double>
            {
                center[0] - parallel * axis[0], center[1] - parallel * axis[1],
                center[2] - parallel * axis[2]
            };
            return String.Join(",", perpendicular.Select(RoundDetailed)) + ";a=" +
                String.Join(",", axis.Select(RoundDetailed)) + ";r=" +
                RoundDetailed(parameters[6] * 1000.0);
        }

        private static List<double> CanonicalAxis(List<double> raw)
        {
            if (raw == null || raw.Count != 3 || raw.Any(value => !IsFinite(value)))
                return new List<double>();
            double length = Math.Sqrt(raw.Sum(value => value * value));
            if (!IsFinite(length) || length < 1e-12) return new List<double>();
            List<double> axis = raw.Select(value => value / length).ToList();
            double first = axis.First(value => Math.Abs(value) > 1e-12);
            if (first < 0) axis = axis.Select(value => -value).ToList();
            return axis;
        }

        private static string PointKey(List<double> point, List<double> anchor)
        {
            if (point == null || point.Count != 3) return "[]";
            return String.Join(",", new[] { point[0] - anchor[0],
                point[1] - anchor[1], point[2] - anchor[2] }.Select(RoundDetailed));
        }

        private static bool SameDetailedPoint(List<double> left, List<double> right)
        {
            return left != null && right != null && left.Count == 3 && right.Count == 3 &&
                left.Select((value, index) => Near(value, right[index], 0.000001))
                    .All(value => value);
        }

        private static string BoolKey(bool value)
        {
            return value ? "1" : "0";
        }

        private static string RoundDetailed(double value)
        {
            double rounded = Math.Floor(value * 1000000.0 + 0.5) / 1000000.0;
            if (Math.Abs(rounded) < 0.0000005) rounded = 0;
            return rounded.ToString("0.######", CultureInfo.InvariantCulture);
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
                List<double> points = DoubleList(raw).Select(value => value * 1000.0).ToList();
                return points.Count < 3 || !points.Take(3).All(IsFinite) ?
                    new List<double>() : points.Take(3).ToList();
            }
            catch { return new List<double>(); }
        }

        private static List<double> ToListOrEmpty(object raw, double factor)
        {
            try { return DoubleList(raw).Select(value => value * factor).ToList(); }
            catch { return new List<double>(); }
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
            for (int index = 0; index < output.bends.Count; index++)
            {
                BendSnapshot bend = output.bends[index];
                bend.canonicalDirection = bend.direction;
                bend.canonicalDown = bend.down;
            }
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
            else if (type.EndsWith("3dBend", StringComparison.OrdinalIgnoreCase))
                output.sketchedBendGroupCount++;
            else if (string.Equals(type, "MirrorStock",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "MirrorPart", StringComparison.OrdinalIgnoreCase))
                output.mirrorPartCount++;
            else if (string.Equals(type, "FlattenBends",
                StringComparison.OrdinalIgnoreCase)) output.flattenBendsCount++;
            else if (string.Equals(type, "ProcessBends",
                StringComparison.OrdinalIgnoreCase)) output.processBendsCount++;
            else if (string.Equals(type, "BaseBody",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Imported", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ImportedBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ForeignBody", StringComparison.OrdinalIgnoreCase))
                output.forbiddenImportFeatureCount++;
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
                output.sheetMetalUseAutoRelief = data.UseAutoRelief;
                output.sheetMetalAutoReliefType = data.AutoReliefType;
                output.sheetMetalReliefRatio = data.ReliefRatio;
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
                output.useDefaultRelief = data.UseDefaultBendRelief;
                output.useAutoRelief = data.UseAutoRelief;
                output.autoReliefType = data.AutoReliefType;
                output.reliefDepthMm = data.ReliefDepth * 1000.0;
                output.reliefWidthMm = data.ReliefWidth * 1000.0;
                output.reliefRatio = data.ReliefRatio;
                allowance = data.GetCustomBendAllowance();
                if (allowance != null)
                {
                    output.allowanceType = allowance.Type;
                    output.kFactor = allowance.KFactor;
                    output.allowanceMm = allowance.BendAllowance * 1000.0;
                    output.deductionMm = allowance.BendDeduction * 1000.0;
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

        private static bool GeometryMatchesContract(GeometrySnapshot geometry,
            RuleContract contract)
        {
            if (!MatchesBox(geometry, contract.expectedFoldedBoxMm)) return false;
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
                    contract.expectedCircleEdgeReferenceCount ||
                !string.Equals(geometry.translationNormalizedDetailedSignature,
                    contract.expectedFoldedDetailedSignature,
                    StringComparison.OrdinalIgnoreCase)) return false;
            double[] expectedRadii =
            {
                0.5, 0.5, 0.5, 0.5, 0.5, 1, 1,
                1.3, 1.3, 1.3, 1.3, 1.3, 2.75, 2.75, 2.75, 2.75,
                3, 3, 3, 3.25, 4.4, 4.4, 4.4, 5, 5.5, 5.5, 5.5
            };
            return RadiiMatch(geometry.cylinderRadiiMm, expectedRadii);
        }

        private static bool MatchesBox(GeometrySnapshot geometry, List<double> expected)
        {
            return geometry != null && geometry.bodyCount == 1 && geometry.finite &&
                geometry.bodyBoxMm.Count == 6 && expected != null && expected.Count == 6 &&
                expected.Select((value, index) => Near(geometry.bodyBoxMm[index], value,
                    BoxToleranceMm)).All(value => value);
        }

        private static bool RadiiMatch(List<double> actual, double[] expected)
        {
            if (actual == null || actual.Count != expected.Length) return false;
            List<double> ordered = actual.OrderBy(value => value).ToList();
            for (int index = 0; index < expected.Length; index++)
                if (!Near(ordered[index], expected[index], 0.002)) return false;
            return true;
        }

        private static bool FeatureMatchesContract(FeatureSnapshot value)
        {
            return InsertBendsCoreMatches(value) && value.mirrorPartCount == 0 &&
                value.forbiddenImportFeatureCount == 0 &&
                value.sketchedBendGroupCount == 0;
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
                "routeId", "sharpReferenceRelativePath", "sharpReferenceSha256",
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
                Text(root, "routeId") == NativeRouteId &&
                Text(root, "sharpReferenceRelativePath") ==
                    SharpReferenceRelativePath &&
                string.Equals(Text(root, "sharpReferenceSha256"),
                    SharpReferenceSha256, StringComparison.OrdinalIgnoreCase) &&
                Text(root, "outputRelativePath") == OutputRelativePath &&
                Text(root, "evidenceRelativePath") == EvidenceRelativePath &&
                Text(root, "receiptRelativePath") == ReceiptRelativePath,
                "AUTHORIZATION_BINDING_INVALID");
            Dictionary<string, object> boundary = Child(root, "qualityBoundary");
            Require(ExactKeys(boundary, new[] { "structureEngineerAssistanceOnly",
                    "pilotNotAcceptedModel", "requiresRuntimeGeometryRegression",
                    "sharpStockInsertBendsProbeOnly",
                    "noLegacyGeneratorFallback" }) &&
                Bool(boundary, "structureEngineerAssistanceOnly") &&
                Bool(boundary, "pilotNotAcceptedModel") &&
                Bool(boundary, "requiresRuntimeGeometryRegression") &&
                Bool(boundary, "sharpStockInsertBendsProbeOnly") &&
                Bool(boundary, "noLegacyGeneratorFallback"),
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
                { "schema", ReceiptSchema }, { "phase", "topcover_right_760_pilot" },
                { "success", true }, { "consumable", false },
                { "completedAtUtc", completedAtUtc },
                { "authorizationId", authorization.authorizationId },
                { "authorizationSha256", invocation.authorizationSha256 },
                { "authorizationIssuedAtUtc", authorization.issuedAtUtc },
                { "authorizationExpiresAtUtc", authorization.expiresAtUtc },
                { "ruleSha256", RuleSha256 }, { "toolId", ToolId },
                { "toolSourceNormalizedSha256", ExpectedSourceSha256 },
                { "toolExecutableSha256", executableSha256 },
                { "routeId", NativeRouteId },
                { "sharpReferenceRelativePath", SharpReferenceRelativePath },
                { "sharpReferenceSha256", SharpReferenceSha256 },
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
                "routeId",
                "sharpReferenceRelativePath", "sharpReferenceSha256",
                "outputRelativePath", "outputSha256", "evidenceRelativePath",
                "evidenceSha256", "qualityBoundary"
            };
            return ExactKeys(receipt, receiptKeys) && Text(receipt, "schema") == ReceiptSchema &&
                Bool(receipt, "success") && !Bool(receipt, "consumable") &&
                Bool(evidence, "pilotPass") && !Bool(evidence, "consumable") &&
                Text(receipt, "routeId") == Text(evidence, "nativeRouteId") &&
                Text(receipt, "authorizationId") == Text(evidence, "authorizationId") &&
                string.Equals(Text(receipt, "authorizationSha256"),
                    Text(evidence, "authorizationSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "ruleSha256"), Text(evidence, "ruleSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "sharpReferenceSha256"),
                    Text(evidence, "sharpReferenceSha256"),
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
            Require(Text(root, "partName") == "上盖壳体右侧板.SLDPRT" &&
                Bool(root, "right760NativePilotContractReady") &&
                !Bool(root, "right888NativeBuildReady") &&
                !Bool(root, "nativeCadCreated"),
                "RULE_STATE_INVALID");
            Require(!string.IsNullOrWhiteSpace(Text(root, "qualityBoundary")),
                "RULE_QUALITY_BOUNDARY_MISSING");

            RuleContract output = new RuleContract { exactKeys = true };
            Dictionary<string, object> inputs = Child(root, "inputs");
            string[] inputNames = { "ruleV1", "physical", "dxf", "readiness",
                "referenceV5" };
            Require(ExactKeys(inputs, inputNames), "RULE_INPUT_KEYS_INVALID");
            output.inputBindingsValid = inputNames.All(name =>
                InputBindingValid(Child(inputs, name), repositoryRoot));
            Require(output.inputBindingsValid, "RULE_INPUT_BINDING_INVALID");

            Dictionary<string, object> sheet = Child(root, "sheetMetal");
            output.sheetMetalValid = ExactKeys(sheet, new[] { "thicknessMm",
                    "defaultBendRadiusMm", "kFactorReadback", "customAllowanceType",
                    "bendDeductionMm" }) &&
                Near(Number(sheet, "thicknessMm"), 0.8, 1e-9) &&
                Near(Number(sheet, "defaultBendRadiusMm"), 0.2, 1e-9) &&
                Near(Number(sheet, "kFactorReadback"), 0.5, 1e-9) &&
                Number(sheet, "customAllowanceType") == 4 &&
                Near(Number(sheet, "bendDeductionMm"), 1.4, 1e-9);
            Require(output.sheetMetalValid, "RULE_SHEET_METAL_INVALID");

            Dictionary<string, object> relation = Child(root, "widthRelation");
            Dictionary<string, object> shifts = Child(relation, "xShiftFrom760Mm");
            Require(Bool(relation, "localGeometryInvariant") &&
                Bool(relation, "foldedAndFlatTranslationSignaturesProven") &&
                Near(Number(shifts, "width760"), 0, 1e-9) &&
                Near(Number(shifts, "width888"), 64, 1e-9) &&
                Near(Number(shifts, "width1000"), 120, 1e-9),
                "WIDTH_RELATION_INVALID");
            output.widthMm = 760;
            Dictionary<string, object> flat = Child(root, "manufacturingFlat760");
            Dictionary<string, object> baseSheet = Child(flat, "baseSheet");
            ValidateLoop(Child(baseSheet, "outerLoop"), 22, "OUTER", output);
            object[] knockoutLoops = ArrayValue(baseSheet, "knockoutCutLoops");
            Require(knockoutLoops.Length == 4, "KNOCKOUT_LOOP_COUNT_INVALID");
            output.knockoutLoopCount = knockoutLoops.Length;
            foreach (object raw in knockoutLoops)
                ValidateLoop(ObjectValue(raw), 12, "KNOCKOUT", output);
            output.knockoutLineCount = knockoutLoops.Sum(value =>
                ArrayValue(ObjectValue(value), "points760ModelMm").Length);
            ValidateRightHoles(baseSheet, output);
            ValidatePemBosses(flat, output);
            ValidateRightBends(root, output);
            ValidateRightExpectedGeometry(flat, Child(root, "expectedFolded760"), output);
            LoadAndApplySignatureCorrection(repositoryRoot, output);
            return output;
        }

        private static void LoadAndApplySignatureCorrection(string repositoryRoot,
            RuleContract output)
        {
            string path = Path.Combine(repositoryRoot,
                SignatureCorrectionRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Require(File.Exists(path) && FileLinkCount(path) == 1 &&
                !HasReparsePoint(path) && string.Equals(Sha256File(path),
                    SignatureCorrectionSha256, StringComparison.OrdinalIgnoreCase),
                "SIGNATURE_CORRECTION_IDENTITY_INVALID");
            string text = File.ReadAllText(path, new UTF8Encoding(false, true));
            Require(StandardJsonNumbersOnly(text),
                "SIGNATURE_CORRECTION_JSON_NONSTANDARD_NUMBER");
            Dictionary<string, object> root = ObjectValue(Json.DeserializeObject(text));
            Require(ExactKeys(root, new[]
            {
                "schema", "generatedAtUtc", "purpose", "inputs", "finding",
                "algorithm", "expectedSignatures", "proof", "cadStarted",
                "nativeCadCreated", "qualityBoundary"
            }) && Text(root, "schema") == SignatureCorrectionSchema &&
                Text(root, "purpose") == "structure_engineering_assistance" &&
                !Bool(root, "cadStarted") && !Bool(root, "nativeCadCreated") &&
                !string.IsNullOrWhiteSpace(Text(root, "qualityBoundary")),
                "SIGNATURE_CORRECTION_STATE_INVALID");

            Dictionary<string, object> inputs = Child(root, "inputs");
            string[] inputNames = { "ruleV2", "referenceV5", "failedNativeR2" };
            Require(ExactKeys(inputs, inputNames) && inputNames.All(name =>
                InputBindingValid(Child(inputs, name), repositoryRoot)),
                "SIGNATURE_CORRECTION_INPUT_INVALID");

            Dictionary<string, object> finding = Child(root, "finding");
            object[] causes = ArrayValue(finding, "mismatchCause");
            Require(ExactKeys(finding, new[]
            {
                "legacyFlatReferenceSignature", "legacyFlatNativeR2Signature",
                "physicalGeometryExact", "mismatchCause"
            }) && Text(finding, "legacyFlatReferenceSignature") ==
                    output.legacyExpectedFlatDetailedSignature &&
                Text(finding, "legacyFlatNativeR2Signature") ==
                    "DB8F1F935092F08F7E1515175DC5B29FE4B0BFB9CF1E315F22538780735E9C98" &&
                Bool(finding, "physicalGeometryExact") && causes.Length == 2 &&
                Convert.ToString(causes[0], CultureInfo.InvariantCulture) ==
                    "edge_endpoint_orientation" &&
                Convert.ToString(causes[1], CultureInfo.InvariantCulture) ==
                    "full_circle_parameter_seam",
                "SIGNATURE_CORRECTION_FINDING_INVALID");

            Dictionary<string, object> algorithm = Child(root, "algorithm");
            Require(ExactKeys(algorithm, new[]
            {
                "id", "translationAnchor", "roundingDecimals", "roundingMode",
                "edgeEndpointOrientationInvariant", "fullCircleSeamInvariant",
                "faceNormalSignInvariant", "cylinderAxisSignInvariant",
                "booleanEncoding", "payloadPrefix"
            }) && Text(algorithm, "id") ==
                    "translation_normalized_brep_orientation_and_full_circle_seam_invariant_v1" &&
                Text(algorithm, "translationAnchor") == "body_bbox_min_xyz" &&
                Number(algorithm, "roundingDecimals") == 6 &&
                Text(algorithm, "roundingMode") ==
                    "nearest_ties_toward_positive_infinity" &&
                Bool(algorithm, "edgeEndpointOrientationInvariant") &&
                Bool(algorithm, "fullCircleSeamInvariant") &&
                Bool(algorithm, "faceNormalSignInvariant") &&
                Bool(algorithm, "cylinderAxisSignInvariant") &&
                Text(algorithm, "booleanEncoding") == "1_or_0" &&
                Text(algorithm, "payloadPrefix") == "faces=",
                "SIGNATURE_CORRECTION_ALGORITHM_INVALID");

            Dictionary<string, object> expected = Child(root, "expectedSignatures");
            Require(ExactKeys(expected, new[] { "manufacturingFlat760", "folded760" }) &&
                Text(expected, "manufacturingFlat760") ==
                    "DECF4787EFF44B05A148527A7A512ED27D2539182D6A8909D9C28E8E6B2CC0B6" &&
                Text(expected, "folded760") ==
                    "4698418C907999F50B193BDC3C6F5E5DC2C6DED8E8D270C2E05E4D6C8CBBFECF",
                "SIGNATURE_CORRECTION_EXPECTED_INVALID");
            Dictionary<string, object> proof = Child(root, "proof");
            Require(ExactKeys(proof, new[]
            {
                "v37FlatEqualsGoldFlat", "v37FoldedEqualsGoldFolded",
                "nativeR2FlatEqualsReference", "flatFaceKeysMatched",
                "flatFaceKeysTotal", "nativeR2PhysicalMetricsExact"
            }) && Bool(proof, "v37FlatEqualsGoldFlat") &&
                Bool(proof, "v37FoldedEqualsGoldFolded") &&
                Bool(proof, "nativeR2FlatEqualsReference") &&
                Number(proof, "flatFaceKeysMatched") == 95 &&
                Number(proof, "flatFaceKeysTotal") == 95 &&
                Bool(proof, "nativeR2PhysicalMetricsExact"),
                "SIGNATURE_CORRECTION_PROOF_INVALID");
            output.expectedFlatDetailedSignature = Text(expected,
                "manufacturingFlat760");
            output.expectedFoldedDetailedSignature = Text(expected, "folded760");
            output.signatureCorrectionValid = true;
        }

        private static void ValidateLoop(Dictionary<string, object> loop, int expectedCount,
            string label, RuleContract output)
        {
            object[] values = ArrayValue(loop, "points760ModelMm");
            Require(values.Length == expectedCount, label + "_POINT_COUNT_INVALID");
            List<Point3> points = values.Select(value => Point(ObjectArray(value))).ToList();
            Require(points.All(value => Near(value.y, 1917, 1e-7)) &&
                points.Select(value => value.Key()).Distinct().Count() == expectedCount,
                label + "_POINTS_INVALID");
            for (int index = 0; index < points.Count; index++)
                Require(!points[index].Near(points[(index + 1) % points.Count], 1e-9),
                    label + "_ZERO_LENGTH_EDGE");
            if (label == "OUTER")
            {
                output.outlineLineCount = expectedCount;
                output.outlineVertexCount = expectedCount;
            }
        }

        private static void ValidateRightHoles(Dictionary<string, object> baseSheet,
            RuleContract output)
        {
            object[] rows = ArrayValue(baseSheet, "throughHoles");
            Require(rows.Length == 11, "RIGHT_HOLE_COUNT_INVALID");
            foreach (object raw in rows)
            {
                Dictionary<string, object> row = ObjectValue(raw);
                Point3 center = Point(ArrayValue(row, "center760ModelMm"));
                Require(Near(center.y, 1917, 1e-7) &&
                    Near(Number(row, "diameterMm"), Number(row, "radiusMm") * 2, 1e-9),
                    "RIGHT_HOLE_ROW_INVALID");
                output.holeRadiiMm.Add(Number(row, "radiusMm"));
            }
            output.holeCount = rows.Length;
            Require(SequenceNear(output.holeRadiiMm.OrderBy(value => value), new[]
                { 1.0, 1.0, 2.75, 2.75, 2.75, 2.75, 3.25, 4.4, 4.4, 4.4, 5.0 },
                    1e-6),
                "RIGHT_HOLE_RADIUS_MULTISET_INVALID");
        }

        private static void ValidatePemBosses(Dictionary<string, object> flat,
            RuleContract output)
        {
            object[] rows = ArrayValue(flat, "pemBosses");
            Require(rows.Length == 3, "PEM_BOSS_COUNT_INVALID");
            foreach (object raw in rows)
            {
                Dictionary<string, object> boss = ObjectValue(raw);
                Require(Near(Number(boss, "outerRadiusMm"), 5.5, 1e-9) &&
                    Near(Number(boss, "innerRadiusMm"), 3, 1e-9) &&
                    Near(Number(boss, "heightMm"), 4, 1e-9) &&
                    Near(Number(boss, "fromYmm"), 1916.2, 1e-9) &&
                    Near(Number(boss, "toYmm"), 1912.2, 1e-9) &&
                    Bool(boss, "mergeResult"), "PEM_BOSS_ROW_INVALID");
            }
            output.pemBossCount = rows.Length;
        }

        private static void ValidateRightBends(Dictionary<string, object> root,
            RuleContract output)
        {
            List<Dictionary<string, object>> bends = ArrayValue(root, "bends760")
                .Select(ObjectValue).OrderBy(value => Number(value, "order")).ToList();
            string[] expectedNames = { "直线7", "直线8", "直线9", "直线6", "直线10" };
            Require(bends.Count == 5, "FIVE_BENDS_REQUIRED");
            for (int index = 0; index < bends.Count; index++)
            {
                Dictionary<string, object> bend = bends[index];
                Require(Number(bend, "order") == index + 1 &&
                    Text(bend, "lineName") == expectedNames[index] &&
                    Near(Number(bend, "angleRadians"), Math.PI / 2, 1e-12) &&
                    Near(Number(bend, "radiusMm"), 0.5, 1e-6) &&
                    !Bool(bend, "useDefaultRadius") &&
                    Number(bend, "bendPosition") == 0 &&
                    Number(bend, "customAllowanceType") == 4 &&
                    Near(Number(bend, "bendDeductionMm"), 1.4, 1e-9) &&
                    Near(Number(bend, "kFactorReadback"), 0.5, 1e-9),
                    "RIGHT_BEND_ROW_INVALID");
                output.bendLineNames.Add(Text(bend, "lineName"));
            }
            output.bendCount = bends.Count;
            output.bendSequenceValid = bends.Take(4).All(value =>
                Number(value, "direction") == 2 && Bool(value, "down")) &&
                Number(bends[4], "direction") == 1 && !Bool(bends[4], "down");
            Require(output.bendSequenceValid, "RIGHT_BEND_SEQUENCE_INVALID");
        }

        private static void ValidateRightExpectedGeometry(Dictionary<string, object> flat,
            Dictionary<string, object> folded, RuleContract output)
        {
            Dictionary<string, object> flatExpected = Child(flat, "expected");
            output.expectedFlatBoxMm = NumberArray(flatExpected, "bboxMm");
            output.expectedFlatVolumeMm3 = Number(flatExpected, "volumeMm3");
            output.expectedFlatMassKg = Number(flatExpected, "massKg");
            output.expectedFlatSurfaceAreaMm2 = Number(flatExpected, "surfaceAreaMm2");
            Dictionary<string, object> flatTopology = Child(flatExpected, "topology");
            output.expectedFlatFaceCount = (int)Number(flatTopology, "faceCount");
            output.expectedFlatEdgeCount = (int)Number(flatTopology, "edgeCount");
            output.expectedFlatLoopCount = (int)Number(flatTopology, "loopCount");
            output.expectedFlatLoopEdgeReferenceCount = (int)Number(flatTopology,
                "loopEdgeReferenceCount");
            output.expectedFlatCylinderFaceCount = (int)Number(flatTopology,
                "cylinderFaceCount");
            output.expectedFlatCircleEdgeReferenceCount = (int)Number(flatTopology,
                "circleEdgeReferenceCount");
            output.legacyExpectedFlatDetailedSignature = Text(flatExpected,
                "translationNormalizedDetailedGeometrySignature");
            output.expectedFlatDetailedSignature =
                output.legacyExpectedFlatDetailedSignature;
            output.flatTopologyValid = output.expectedFlatFaceCount == 95 &&
                output.expectedFlatEdgeCount == 244 && output.expectedFlatLoopCount == 148 &&
                output.expectedFlatLoopEdgeReferenceCount == 488 &&
                output.expectedFlatCylinderFaceCount == 17 &&
                output.expectedFlatCircleEdgeReferenceCount == 68;
            output.expectedFoldedBoxMm = NumberArray(folded, "bboxMm");
            output.expectedVolumeMm3 = Number(folded, "volumeMm3");
            output.expectedMassKg = Number(folded, "massKg");
            output.expectedSurfaceAreaMm2 = Number(folded, "surfaceAreaMm2");
            Dictionary<string, object> topology = Child(folded, "topology");
            output.expectedFaceCount = (int)Number(topology, "faceCount");
            output.expectedEdgeCount = (int)Number(topology, "edgeCount");
            output.expectedLoopCount = (int)Number(topology, "loopCount");
            output.expectedLoopEdgeReferenceCount = (int)Number(topology,
                "loopEdgeReferenceCount");
            output.expectedCylinderFaceCount = (int)Number(topology,
                "cylinderFaceCount");
            output.expectedCircleEdgeReferenceCount = (int)Number(topology,
                "circleEdgeReferenceCount");
            output.legacyExpectedFoldedDetailedSignature = Text(folded,
                "translationNormalizedDetailedGeometrySignature");
            output.expectedFoldedDetailedSignature =
                output.legacyExpectedFoldedDetailedSignature;
            output.topologyValid = output.expectedFaceCount == 135 &&
                output.expectedEdgeCount == 324 && output.expectedLoopCount == 188 &&
                output.expectedLoopEdgeReferenceCount == 648 &&
                output.expectedCylinderFaceCount == 27 &&
                output.expectedCircleEdgeReferenceCount == 108;
            output.baseTopYMm = Number(Child(flat, "baseSheet"), "topYmm");
            output.baseBottomYMm = Number(Child(flat, "baseSheet"), "bottomYmm");
            output.baseOuterAreaMm2 = Number(Child(Child(flat, "baseSheet"),
                "outerLoop"), "areaMm2");
            Dictionary<string, object> breakdown = Child(flat, "volumeBreakdownMm3");
            output.expectedCutBaseSheetVolumeMm3 = Number(breakdown, "baseSheetVolume");
            output.foldedAndMassValid = output.expectedFlatBoxMm.Count == 6 &&
                output.expectedFoldedBoxMm.Count == 6 &&
                Near(output.expectedFlatVolumeMm3, 70990.25576414, 0.0001) &&
                Near(output.expectedVolumeMm3, 71396.31748296, 0.0001);
            Require(output.flatTopologyValid && output.topologyValid &&
                output.foldedAndMassValid, "RIGHT_EXPECTED_GEOMETRY_INVALID");
        }

        private static bool InputBindingValid(Dictionary<string, object> row,
            string repositoryRoot)
        {
            if (!ExactKeys(row, new[] { "path", "sha256" })) return false;
            string rawPath = Text(row, "path");
            string path = FullPath(Path.IsPathRooted(rawPath) ? rawPath :
                Path.Combine(repositoryRoot,
                    rawPath.Replace('/', Path.DirectorySeparatorChar)));
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
                        "^TOPCOVER-RIGHT-PILOT-[A-Z0-9-]{8,120}$", RegexOptions.CultureInvariant) &&
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
                "right_760_native_v1", "TopCoverRight760Native.cs");
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

        private static bool SequenceNear(IEnumerable<double> actual,
            IEnumerable<double> expected, double tolerance)
        {
            double[] left = actual.ToArray();
            double[] right = expected.ToArray();
            return left.Length == right.Length && left.Zip(right,
                (a, b) => Near(a, b, tolerance)).All(value => value);
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

        private static object[] ObjectArray(object value)
        {
            IEnumerable sequence = value as IEnumerable;
            Require(sequence != null && !(value is string), "ARRAY_VALUE_REQUIRED");
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
            public bool signatureCorrectionValid;
            public bool sheetMetalValid;
            public double widthMm;
            public int outlineLineCount;
            public int outlineVertexCount;
            public int knockoutLoopCount;
            public int knockoutLineCount;
            public int holeCount;
            public List<double> holeRadiiMm = new List<double>();
            public int pemBossCount;
            public int bendCount;
            public List<string> bendLineNames = new List<string>();
            public bool bendSequenceValid;
            public bool foldedAndMassValid;
            public bool flatTopologyValid;
            public bool topologyValid;
            public double baseTopYMm;
            public double baseBottomYMm;
            public double baseOuterAreaMm2;
            public double expectedCutBaseSheetVolumeMm3;
            public List<double> expectedFlatBoxMm = new List<double>();
            public List<double> expectedFoldedBoxMm = new List<double>();
            public double expectedFlatVolumeMm3;
            public double expectedFlatMassKg;
            public double expectedFlatSurfaceAreaMm2;
            public int expectedFlatCylinderFaceCount;
            public int expectedFlatFaceCount;
            public int expectedFlatEdgeCount;
            public int expectedFlatLoopCount;
            public int expectedFlatLoopEdgeReferenceCount;
            public int expectedFlatCircleEdgeReferenceCount;
            public string legacyExpectedFlatDetailedSignature = "";
            public string expectedFlatDetailedSignature = "";
            public double expectedVolumeMm3;
            public double expectedMassKg;
            public double expectedSurfaceAreaMm2;
            public int expectedCylinderFaceCount;
            public int expectedFaceCount;
            public int expectedEdgeCount;
            public int expectedLoopCount;
            public int expectedLoopEdgeReferenceCount;
            public int expectedCircleEdgeReferenceCount;
            public string legacyExpectedFoldedDetailedSignature = "";
            public string expectedFoldedDetailedSignature = "";
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
            public List<DetailedFace> faces = new List<DetailedFace>();
            public string translationNormalizedDetailedSignature = "";
            public bool finite;
        }

        private sealed class DetailedFace
        {
            public double areaMm2;
            public List<double> boxMm = new List<double>();
            public List<double> normal = new List<double>();
            public List<double> planeParams = new List<double>();
            public List<double> cylinderParams = new List<double>();
            public int surfaceIdentity;
            public bool isPlane;
            public bool isCylinder;
            public List<DetailedLoop> loops = new List<DetailedLoop>();
        }

        private sealed class DetailedLoop
        {
            public bool isOuter;
            public int edgeCount;
            public List<DetailedEdge> edges = new List<DetailedEdge>();
        }

        private sealed class DetailedEdge
        {
            public int curveType;
            public bool isLine;
            public bool isCircle;
            public double uMin;
            public double uMax;
            public double lengthMm;
            public List<double> startPointMm = new List<double>();
            public List<double> endPointMm = new List<double>();
            public List<double> startVertexPointMm = new List<double>();
            public List<double> endVertexPointMm = new List<double>();
            public List<double> lineParams = new List<double>();
            public List<double> circleParams = new List<double>();
        }

        private sealed class BendSnapshot
        {
            public string name = "";
            public bool suppressed;
            public int bendType = -1;
            public double angleRadians;
            public int direction = -1;
            public bool down;
            public int canonicalDirection = -1;
            public bool canonicalDown;
            public int order = -1;
            public double radiusMm;
            public double kFactor;
            public double allowanceMm;
            public double deductionMm;
            public int allowanceType = -1;
            public bool useDefaultRadius;
            public bool useDefaultAllowance;
            public bool useDefaultRelief;
            public bool useAutoRelief;
            public int autoReliefType = -1;
            public double reliefDepthMm;
            public double reliefWidthMm;
            public double reliefRatio;
        }

        private sealed class FeatureSnapshot
        {
            public int featureCount;
            public int sheetMetalCount;
            public int oneBendCount;
            public int sketchedBendGroupCount;
            public int mirrorPartCount;
            public int flattenBendsCount;
            public int processBendsCount;
            public int forbiddenImportFeatureCount;
            public int flatPatternCount;
            public double sheetMetalThicknessMm;
            public double sheetMetalRadiusMm;
            public double sheetMetalKFactor;
            public double sheetMetalAllowanceMm;
            public int sheetMetalAllowanceType = -1;
            public bool sheetMetalUseAutoRelief;
            public int sheetMetalAutoReliefType = -1;
            public double sheetMetalReliefRatio;
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

        private sealed class HolePoint
        {
            public readonly double xMm;
            public readonly double yMm;
            public readonly double zMm;
            public readonly double radiusMm;
            public HolePoint(double xValueMm, double yValueMm, double zValueMm,
                double radiusValueMm)
            {
                xMm = xValueMm;
                yMm = yValueMm;
                zMm = zValueMm;
                radiusMm = radiusValueMm;
            }
        }

        private sealed class ContractException : Exception
        {
            public ContractException(string message) : base(message) { }
        }
    }
}
