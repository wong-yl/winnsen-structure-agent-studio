using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static class ConfigureNativeWidth888
    {
        private const string AttemptRootEnvironmentVariable = "WINNSEN_NATIVE_ATTEMPT_ROOT";
        private const string PlanShaEnvironmentVariable = "WINNSEN_NATIVE_PLAN_SHA256";
        private const string AuthorizationPathEnvironmentVariable = "WINNSEN_NATIVE_EXECUTION_AUTH_PATH";
        private const string AuthorizationShaEnvironmentVariable = "WINNSEN_NATIVE_EXECUTION_AUTH_SHA256";
        private const string ExpectedAuthorizationSchema = "winnsen.native_execution_authorization.v1";
        private const string ExpectedToolchainManifestSchema =
            "winnsen.16029.native_toolchain_manifest.v1";
        private const string ExpectedSeedReceiptSchema =
            "winnsen.16029.native_seed_pack_receipt.v1";
        private const string ExpectedEvidenceSchema =
            "winnsen.locker16029.native_width_888.v1";
        private const string ExpectedToolId = "native_width_888_v1";
        private const string ExpectedSourceSha256 = "F75D752830BB5DBB9C4EE3C7963701A6AF4D73668042DDA96A3355A6235EE5D4";
        private const string ExpectedCadLeaf = @"native_cad\working_pack";
        private const string ExpectedRecipeId = "winnsen-16029-888w-14door-native-v1";
        private const string ExpectedRecipeDigest =
            "F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8";
        private const string ExpectedPurpose = "structure_engineering_assistance";
        private const string SeedRootFileName = "标准寄存柜1917×760×550(总装配).SLDASM";
        private const string ExpectedSeedRootSha256 =
            "5AED314E89A65A3875C517B2F4DC179635E9C684E0CD1877AFEBD33CAA77CA5B";
        private const string ExpectedSeedInventoryDigest =
            "9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B";
        private const string ExpectedSolidWorksExePath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string ExpectedSolidWorksExeSha256 =
            "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string ExpectedSolidWorksVersionPrefix = "28.";
        private const string PhaseLockDirectoryName = ".native-width-888.lock";
        private static FileStream phaseLockHandle;
        private static readonly string[] AuthorizationTopLevelKeys =
        {
            "schema", "authorizationId", "issuedAt", "expiresAt", "purpose", "workerId",
            "task", "request", "plan", "recipe", "tool", "seed", "execution", "qualityBoundary"
        };
        private static readonly string[] AuthorizationTaskKeys =
            { "id", "revision", "digest", "leaseId", "leaseExpiresAt" };
        private static readonly string[] AuthorizationRequestKeys = { "fingerprint", "digest" };
        private static readonly string[] AuthorizationPlanKeys = { "sha256" };
        private static readonly string[] AuthorizationRecipeKeys = { "id", "version", "digest" };
        private static readonly string[] AuthorizationToolKeys =
            { "id", "sourceNormalizedSha256", "executableSha256" };
        private static readonly string[] AuthorizationSeedKeys = { "inventoryDigest" };
        private static readonly string[] AuthorizationExecutionKeys = { "authorized", "attempt", "phases" };
        private static readonly string[] AuthorizationQualityBoundaryKeys =
            { "engineeringAssistanceReady", "readyOnlyAfterEveryRequiredCheckPasses" };
        private static readonly string[] PhaseReceiptKeys =
        {
            "schema", "phase", "success", "completedAt", "taskId", "taskRevision", "taskDigest",
            "requestDigest", "leaseId", "leaseExpiresAt", "attempt", "planSha256", "authorizationId",
            "authorizationSha256", "authorizationJsonBase64", "authorizationIssuedAt",
            "authorizationExpiresAt", "recipeId", "recipeDigest", "toolId",
            "toolSourceNormalizedSha256", "toolExecutableSha256", "evidencePath",
            "evidenceSha256", "evidenceCommitmentSha256",
            "preInventoryDigest", "postInventoryDigest", "predecessorReceiptSha256"
        };
        private static readonly string[] TrustedToolchainManifestKeys =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1",
            "native_door_module_888x14_v1", "native_lock_topology_888x14_v1",
            "native_root_assembly_888x14_v1", "native_final_pack_888x14_v1"
        };
        private static readonly string[] ToolchainManifestTopLevelKeys =
            { "schema", "generatedBy", "tool" };
        private static readonly string[] ToolchainManifestToolKeys =
        {
            "id", "sourcePath", "sourceNormalizedSha256", "executablePath",
            "executableSha256", "verifierPath", "verifierSha256"
        };
        private static readonly string[] StageReceiptContractKeys =
            { "schema", "path", "producerToolId", "predecessor" };
        private static readonly string[] SeedReceiptKeys =
        {
            "schema", "phase", "success", "completedAt", "taskId", "taskRevision",
            "taskDigest", "requestDigest", "leaseId", "leaseExpiresAt", "attempt",
            "planSha256", "authorizationId", "authorizationSha256", "authorizationJsonBase64",
            "authorizationIssuedAt", "authorizationExpiresAt", "recipeId", "recipeDigest",
            "toolId", "toolSourceNormalizedSha256", "toolExecutableSha256", "evidencePath",
            "evidenceSha256", "evidenceCommitmentSha256", "sourceInventoryDigest",
            "targetInventoryDigest", "nonRootFileCount", "nonRootExactSource", "rootFileName",
            "rootShaBefore", "rootShaAfterStable", "initialOpenErrors", "initialOpenWarnings",
            "stabilizeSaveErrors", "stabilizeSaveWarnings", "reopenErrors", "reopenWarnings",
            "dependencyClosureCount", "dependenciesAllTargetLocal", "knownRootIssueGate",
            "predecessorReceiptSha256"
        };
        private static readonly string[] EvidenceCommitmentExcludedKeys =
        {
            "evidence_write_attempted", "evidence_write_succeeded",
            "evidence_finalize_write_succeeded", "evidence_finalize_error",
            "failure_evidence_write_succeeded", "phase_receipt_path", "phase_receipt_sha256",
            "phase_receipt_committed", "authorization_checkpoints", "completed_at_utc",
            "commit_completed_at_utc", "evidence_commitment_sha256", "backup_deleted",
            "backup_delete_error"
        };
        private const double DimensionToleranceMm = 0.001;
        private const double TransformToleranceMm = 0.001;
        private const double BoundingBoxToleranceMm = 0.2;
        private const string BaseHolePartFileName = "底座底板.sldprt";
        private const string BaseHoleSketchName = "草图9";
        private const string BaseHoleConsumerFeatureName = "切除-拉伸5";
        private const double BaseHoleSourceAbsXMm = 305.0;
        private const double BaseHoleTargetAbsXMm = 369.0;
        private const double BaseHoleSourceSpanMm = 610.0;
        private const double BaseHoleTargetSpanMm = 738.0;
        private const double BaseHoleDepthSpanMm = 426.0;
        private static readonly string[] BaseHoleBlockNames =
        {
            "块-敲落孔φ45mm-1", "块-敲落孔φ45mm-3",
            "块-敲落孔φ45mm-4", "块-敲落孔φ45mm-5"
        };
        private const string FrozenRightPartitionSha256 =
            "C2B923131D743817E5E38594AF9C5C7AC9A255E77F702642FDE0E1917FE70EA3";
        private const string NativeRightTopCoverRoute =
            "right_888_sharp_stock_insert_bends_v1";
        private const string NativeRightRuleRelativePath =
            "data/native_model_requests/attempts/" +
            "NATIVE-20260824T132708Z-888W14D-RIGHT-RULE-R1/attempt-0001/" +
            "evidence/private/topcover_right_native_rule_v2.json";
        private const string NativeRightRuleSha256 =
            "EFAB0AE94FC50C0ECC05CA2384D24A93DE3E276918BAAB68CA96EFADBCAEF1F0";
        private const string NativeRightR34RootRelativePath =
            "data/native_model_requests/attempts/" +
            "TOPCOVER-RIGHT-PILOT-20260828T152022Z-760-RIGHT-R34/attempt-0001/";
        private const string NativeRightR34EvidenceRelativePath = NativeRightR34RootRelativePath +
            "evidence/private/topcover_right_760_native_pilot.json";
        private const string NativeRightR34ReceiptRelativePath = NativeRightR34RootRelativePath +
            "receipts/topcover_right_760_native_pilot.json";
        private const string NativeRightR34PartRelativePath = NativeRightR34RootRelativePath +
            "native_cad/right_760_native_pilot/上盖壳体右侧板.SLDPRT";
        private const string NativeRightR34AuthorizationRelativePath = NativeRightR34RootRelativePath +
            "execution_authorizations/topcover_right_760_native_v1.json";
        private const string NativeRightR34EvidenceSha256 =
            "E23D31674308456A341D4100C4EA833911193367389DF281F327B2D26BC819BE";
        private const string NativeRightR34ReceiptSha256 =
            "C9FA85375C2906752AECCC28887B0FAD361B271D0C1C2CFF48B0BAB81A9B2E91";
        private const string NativeRightR34PartSha256 =
            "64EF1FBC4FD86EA816EAACC08CA5F376314BB2D5B95EBFA535E0FAC75FDB6319";
        private const string NativeRightR34AuthorizationSha256 =
            "C87D85C8B8790A75B60FC9C533F92C978DBE46A34975FDB8F445E63AF4F01AB7";
        private const string NativeRightR34DetailedSignature =
            "4698418C907999F50B193BDC3C6F5E5DC2C6DED8E8D270C2E05E4D6C8CBBFECF";
        private const double NativeRightDeltaXMm = 64.0;
        private const double NativeRightBoxToleranceMm = 0.02;
        private const double NativeRightVolumeToleranceMm3 = 2.0;
        private const double NativeRightAreaToleranceMm2 = 5.0;
        private const double NativeRightMassToleranceKg = 0.00002;
        private const string NativeFrameCrossbarRightRoute =
            "frame_crossbar_right_888_open_profile_insert_bends_v1";
        private const string NativeFrameCrossbarPilotRootRelativePath =
            "data/native_model_requests/attempts/" +
            "NATIVE-20260830T062627Z-3130A1/attempt-0001/";
        private const string NativeFrameCrossbarPilotEvidenceRelativePath =
            NativeFrameCrossbarPilotRootRelativePath +
            "diagnostics/frame-crossbar-native-detail-v11.json";
        private const string NativeFrameCrossbarBooleanDiffRelativePath =
            NativeFrameCrossbarPilotRootRelativePath +
            "diagnostics/frame-crossbar-reference-vs-v11-boolean-diff.json";
        private const string NativeFrameCrossbarReopenEvidenceRelativePath =
            NativeFrameCrossbarPilotRootRelativePath +
            "diagnostics/frame-crossbar-native-detail-v11-fresh-reopen.json";
        private const string NativeFrameCrossbarPilotPartRelativePath =
            NativeFrameCrossbarPilotRootRelativePath +
            "diagnostics/frame-crossbar-native-detail-v11/门框 横隔板R.SLDPRT";
        private const string NativeFrameCrossbarReferencePartRelativePath =
            NativeFrameCrossbarPilotRootRelativePath +
            "native_cad/working_pack/门框 横隔板R.SLDPRT";
        private const string NativeFrameCrossbarPilotEvidenceSha256 =
            "940ECCBF4F46DD1FBE021436EE5A52D00A8C7A66F7F5625BFCFAFCB23D66A95F";
        private const string NativeFrameCrossbarBooleanDiffSha256 =
            "1F5C34D8B9F2588FE22E189B2AE543B2F545A020B986DE4472264AF4E12FA5B0";
        private const string NativeFrameCrossbarReopenEvidenceSha256 =
            "7F8898DD69660B530F9F38F0B87A3F1CB7BAFF6D93F6751E44EEE9210CB5FB41";
        private const string NativeFrameCrossbarPilotPartSha256 =
            "2DB68BB29F96E5277E9D0FFA444D2BB9D8C2DDCB2383305B7EEAE26A24DB5BD8";
        private const string NativeFrameCrossbarReferencePartSha256 =
            "7044D2E8523E7E97371B7168423B224EBA55269F18B18110C9379F72C35EB29F";
        private const string NativeFrameCrossbarDetailedSignature =
            "A32FE7BAFC3780BC746D2CF82612E8C69B252FF94146A1986CC4C4C3564777CF";
        private const double NativeFrameCrossbarBoxToleranceMm = 0.002;
        private const double NativeFrameCrossbarVolumeToleranceMm3 = 0.01;
        private const double NativeFrameCrossbarAreaToleranceMm2 = 0.02;
        private const double NativeFrameCrossbarMassToleranceKg = 0.0000001;
        private const string NativeDoorFrameRightRoute =
            "door_frame_right_888_open_profile_insert_bends_v1";
        private const string NativeDoorFrameRightPilotRootRelativePath =
            "data/native_model_requests/attempts/" +
            "NATIVE-20260830T083519Z-75C0A0/attempt-0001/";
        private const string NativeDoorFrameRightPilotEvidenceRelativePath =
            NativeDoorFrameRightPilotRootRelativePath +
            "diagnostics/door-frame-right-native-detail-v12.json";
        private const string NativeDoorFrameRightBooleanDiffRelativePath =
            NativeDoorFrameRightPilotRootRelativePath +
            "diagnostics/door-frame-right-reference-vs-v12-boolean-diff.json";
        private const string NativeDoorFrameRightReopenEvidenceRelativePath =
            NativeDoorFrameRightPilotRootRelativePath +
            "diagnostics/door-frame-right-native-detail-v12-fresh-reopen.json";
        private const string NativeDoorFrameRightPilotPartRelativePath =
            NativeDoorFrameRightPilotRootRelativePath +
            "diagnostics/door-frame-right-native-detail-v12/" +
            "door-frame-right-native-v12.SLDPRT";
        private const string NativeDoorFrameRightReferencePartRelativePath =
            NativeDoorFrameRightPilotRootRelativePath +
            "native_cad/working_pack/门框 右.sldprt";
        private const string NativeDoorFrameRightPilotEvidenceSha256 =
            "B11F8E8D6A6CD7B7E05AE822B66DC36A209426FA9C3DC7811A3F3CB2496EA99A";
        private const string NativeDoorFrameRightBooleanDiffSha256 =
            "91C6BD7411B4CFDA007C599E8C4E708F168D33887E4DBDAC80D7B8D7789DAAC7";
        private const string NativeDoorFrameRightReopenEvidenceSha256 =
            "420D139CBD828FF084F7B8045DCDA833B489CA00472058ACB943BE6DBE5A66A5";
        private const string NativeDoorFrameRightPilotPartSha256 =
            "5DC7C08E37D23CBC4C842EF5147238F0CFDA233162C30306878A3DA984E1864F";
        private const string NativeDoorFrameRightReferencePartSha256 =
            "5BE5FA1D9B4BC36385F98088CCF640A6D30FC25EACC86ACB25BDBA1FBAF51C7A";
        private const string NativeDoorFrameRightDetailedSignature =
            "25A9C9CC0E194B49735A21E9DD18C93489F5B28D296CBCEB65FDE48E37A95D19";
        private const double NativeDoorFrameRightBoxToleranceMm = 0.002;
        private const double NativeDoorFrameRightVolumeToleranceMm3 = 0.01;
        private const double NativeDoorFrameRightAreaToleranceMm2 = 0.02;
        private const double NativeDoorFrameRightMassToleranceKg = 0.0000001;
        private const string NativeCabinetShelfRightRoute =
            "cabinet_shelf_right_888_sharp_tray_insert_bends_v1";
        private const string NativeCabinetShelfRightPilotRootRelativePath =
            "data/native_model_requests/attempts/" +
            "NATIVE-20260830T160538Z-ED5C8C/attempt-0001/";
        private const string NativeCabinetShelfRightPilotEvidenceRelativePath =
            NativeCabinetShelfRightPilotRootRelativePath +
            "diagnostics/cabinet-shelf-right-detail-v25.json";
        private const string NativeCabinetShelfRightBooleanDiffRelativePath =
            NativeCabinetShelfRightPilotRootRelativePath +
            "diagnostics/cabinet-shelf-right-reference-vs-v25-boolean-diff.json";
        private const string NativeCabinetShelfRightReopenEvidenceRelativePath =
            NativeCabinetShelfRightPilotRootRelativePath +
            "diagnostics/cabinet-shelf-right-detail-v25-fresh-reopen.json";
        private const string NativeCabinetShelfRightPilotPartRelativePath =
            NativeCabinetShelfRightPilotRootRelativePath +
            "diagnostics/cabinet-shelf-right-detail-v25/" +
            "cabinet-shelf-right-v25.SLDPRT";
        private const string NativeCabinetShelfRightReferencePartRelativePath =
            "data/native_model_requests/attempts/" +
            "NATIVE-20260830T083519Z-75C0A0/attempt-0001/" +
            "native_cad/working_pack/箱体横层板R.SLDPRT";
        private const string NativeCabinetShelfRightPilotEvidenceSha256 =
            "A815B8D1373F829052ABC3D2282C33ED9D390618EA6C4C04C1D4D543FC52C1F4";
        private const string NativeCabinetShelfRightBooleanDiffSha256 =
            "4475C4C99DE90C1D713286D12E1586D32C2A107512A58676E26D096045352CC7";
        private const string NativeCabinetShelfRightReopenEvidenceSha256 =
            "A4E30B9186A82BF8CBD281851D8FC1A83671F601FF81DEB09C11226624DD6C69";
        private const string NativeCabinetShelfRightPilotPartSha256 =
            "8ACC93419989341416D4B96A3C0BF49C0D945A6CF2B2CBBA672D69355AB03397";
        private const string NativeCabinetShelfRightReferencePartSha256 =
            "F828AE08CADAAB1F3DE998921580D63F0348CA5D78734D8ACD4B7E021552E5BC";
        private const string NativeCabinetShelfRightDetailedSignature =
            "E0DD4DBFBA311E6AB1455250A32E0E469B3EA260A083ECF36A733801C93D3D38";
        private const double NativeCabinetShelfRightBoxToleranceMm = 0.002;
        private const double NativeCabinetShelfRightVolumeToleranceMm3 = 0.01;
        private const double NativeCabinetShelfRightAreaToleranceMm2 = 0.02;
        private const double NativeCabinetShelfRightMassToleranceKg = 0.0000001;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && string.Equals(args[0], "--identity", StringComparison.Ordinal))
            {
                Console.WriteLine("sourceNormalizedSha256=" + ExpectedSourceSha256);
                Console.WriteLine("executableSha256=" + Sha256(System.Reflection.Assembly.GetExecutingAssembly().Location));
                Console.WriteLine("compiledSeedCount=" + SeedInventoryShaByFile().Count.ToString(
                    CultureInfo.InvariantCulture));
                Console.WriteLine("compiledSeedInventoryDigest=" + CompiledSeedProfileDigest());
                return 0;
            }
            if (args.Length == 4 && string.Equals(args[0], "--preflight-only",
                    StringComparison.Ordinal))
            {
                string preflightCadDir = Path.GetFullPath(args[1]).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string preflightOutJson = Path.GetFullPath(args[2]);
                string preflightPhase = (args[3] ?? "").Trim().ToLowerInvariant();
                Result preflightResult = NewResult(preflightCadDir, preflightOutJson,
                    preflightPhase);
                try
                {
                    AssertTrustedAttemptDirectory(preflightCadDir, preflightResult);
                    Require(Directory.Exists(preflightCadDir), "NATIVE_888_CAD_DIRECTORY_MISSING", 4,
                        "isolated 760W seed clone directory does not exist");
                    Require(string.Equals(Path.GetExtension(preflightOutJson), ".json",
                            StringComparison.OrdinalIgnoreCase),
                        "OUTPUT_MUST_BE_JSON", 3, "out-json must end in .json");
                    Require(!IsUnder(preflightOutJson, preflightCadDir),
                        "REFUSED_OUTPUT_INSIDE_CAD_DIRECTORY", 3,
                        "out-json must not be written into native_cad");
                    Require(!File.Exists(preflightOutJson) && !Directory.Exists(preflightOutJson),
                        "PHASE_EVIDENCE_ALREADY_EXISTS", 3,
                        "phase evidence path must be unused before any CAD mutation begins");
                    Require(preflightPhase == "dimensions" || preflightPhase == "derived" ||
                        preflightPhase == "base-hole" || preflightPhase == "assemblies",
                        "UNSUPPORTED_PHASE", 2,
                        "phase must be dimensions, derived, base-hole, or assemblies");
                    AssertInitialInventory(preflightCadDir, preflightResult, preflightPhase);
                    ValidatePhaseSequence(preflightResult, preflightPhase);
                    AssertAuthorizationStillValid(preflightResult,
                        "preflight_only_after_sequence");
                    Console.WriteLine("PREFLIGHT_PASS_CAD_NOT_STARTED");
                    return 0;
                }
                catch (GateException exception)
                {
                    Console.Error.WriteLine(exception.Status + ": " + exception.Message);
                    return exception.ExitCode;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("PREFLIGHT_EXCEPTION: " +
                        SafeExceptionText(exception));
                    return 90;
                }
            }
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ConfigureNativeWidth888.exe <cad-dir> <out-json> <dimensions|derived|base-hole|assemblies> | --preflight-only <cad-dir> <out-json> <phase>");
                return 2;
            }

            string cadDir = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string outJson = Path.GetFullPath(args[1]);
            string phase = (args[2] ?? "").Trim().ToLowerInvariant();
            var result = NewResult(cadDir, outJson, phase);
            ISldWorks editSw = null;
            ISldWorks verifySw = null;
            SessionRecord editSession = null;
            SessionRecord verifySession = null;
            PreparedReceipt preparedReceipt = null;
            int exitCode = 99;

            try
            {
                AssertTrustedAttemptDirectory(cadDir, result);
                Require(Directory.Exists(cadDir), "NATIVE_888_CAD_DIRECTORY_MISSING", 4,
                    "isolated 760W seed clone directory does not exist");
                Require(string.Equals(Path.GetExtension(outJson), ".json", StringComparison.OrdinalIgnoreCase),
                    "OUTPUT_MUST_BE_JSON", 3, "out-json must end in .json");
                Require(!IsUnder(outJson, cadDir), "REFUSED_OUTPUT_INSIDE_CAD_DIRECTORY", 3,
                    "out-json must not be written into native_cad");
                EnsureParent(outJson);
                Require(!File.Exists(outJson) && !Directory.Exists(outJson),
                    "PHASE_EVIDENCE_ALREADY_EXISTS", 3,
                    "phase evidence path must be unused before any CAD mutation begins");
                result.evidence_path_unused_at_start = true;
                Require(phase == "dimensions" || phase == "derived" || phase == "base-hole" ||
                    phase == "assemblies", "UNSUPPORTED_PHASE", 2,
                    "phase must be dimensions, derived, base-hole, or assemblies");
                AcquirePhaseLock(result, phase);
                AssertInitialInventory(cadDir, result, phase);
                ValidatePhaseSequence(result, phase);
                AssertAuthorizationStillValid(result, "phase_boundary_after_sequence");

                result.baseline_sldworks_process_ids.AddRange(ProcessIds("SLDWORKS"));
                result.baseline_sldprocmon_process_ids.AddRange(ProcessIds("sldProcMon"));
                Require(result.baseline_sldworks_process_ids.Count == 0 &&
                    result.baseline_sldprocmon_process_ids.Count == 0,
                    "DIRTY_SOLIDWORKS_BASELINE", 5,
                    "SLDWORKS or sldProcMon was already running; refusing non-owned-session mutation");

                List<string> phaseFiles = PhaseFiles(cadDir, phase);
                Require(phaseFiles.Count > 0, "EMPTY_PHASE_PLAN", 6, "phase plan contains no files");
                Require(phaseFiles.Count == 75, "FULL_CAD_BACKUP_FILE_COUNT_DRIFT", 6,
                    "every phase must back up the complete 75-file native CAD pack");
                foreach (string path in phaseFiles)
                    Require(File.Exists(path), "PHASE_INPUT_FILE_MISSING", 6, "missing phase file: " + path);
                CreateBackups(result, phaseFiles);

                editSession = new SessionRecord { purpose = "edit" };
                result.sessions.Add(editSession);
                editSw = StartOwnedSession(result, editSession);
                Require(editSw != null && editSession.created, "EDIT_SESSION_UNAVAILABLE", 7,
                    "could not create an owned SolidWorks edit session");
                Require(OwnedSessionIsExclusive(editSession), "EDIT_SESSION_NOT_EXCLUSIVE", 7,
                    "edit session PID was not the only active SolidWorks process");
                AssertPackInventoryAndDependencies(editSw, cadDir, result, phase);

                if (phase == "dimensions") RunDimensionsEdit(editSw, cadDir, result);
                else if (phase == "derived") RunDerivedEdit(editSw, cadDir, result);
                else if (phase == "base-hole") RunBaseHoleEdit(editSw, cadDir, result);
                else
                {
                    CloseOwnedSession(ref editSw, result, editSession);
                    Require(editSession.process_exited, "ASSEMBLY_INVENTORY_SESSION_DID_NOT_EXIT", 8,
                        "assembly inventory SolidWorks process did not exit cleanly");
                    RunAssembliesEditIsolated(cadDir, result, "primary");
                    RunAssembliesEditIsolated(cadDir, result, "stabilize");
                }

                if (editSw != null) CloseOwnedSession(ref editSw, result, editSession);
                Require(editSession.process_exited, "EDIT_SESSION_DID_NOT_EXIT", 8,
                    "owned edit SolidWorks process did not exit cleanly");
                CaptureAfterSaveHashes(result);
                AuditChangedPaths(result, PhaseAllowlist(cadDir, phase));
                Require(result.unexpected_changed_paths.Count == 0,
                    "UNEXPECTED_PHASE_WRITE_DETECTED", 8,
                    "a file outside the phase allowlist changed; whole-pack rollback required");
                if (phase == "base-hole")
                {
                    int expectedChanges = result.base_hole != null && result.base_hole.changed ? 1 : 0;
                    Require(result.actual_changed_paths.Count == expectedChanges,
                        "BASE_HOLE_CHANGED_PATH_COUNT_DRIFT", 8,
                        "base-hole phase must change exactly the bottom plate on first run and no files on idempotent rerun");
                }

                if (phase == "assemblies")
                {
                    RunAssembliesVerifyIsolated(cadDir, result);
                }
                else
                {
                    verifySession = new SessionRecord { purpose = "read_only_reopen_verify" };
                    result.sessions.Add(verifySession);
                    verifySw = StartOwnedSession(result, verifySession);
                    Require(verifySw != null && verifySession.created, "VERIFY_SESSION_UNAVAILABLE", 9,
                        "could not create an independent read-only verification session");
                    Require(verifySession.sldworks_process_id > 0 &&
                        verifySession.sldworks_process_id != editSession.sldworks_process_id,
                        "VERIFY_SESSION_NOT_INDEPENDENT", 9,
                        "verification did not use a new SolidWorks process");
                    Require(OwnedSessionIsExclusive(verifySession), "VERIFY_SESSION_NOT_EXCLUSIVE", 9,
                        "verify session PID was not the only active SolidWorks process");

                    if (phase == "dimensions") RunDimensionsVerify(verifySw, cadDir, result);
                    else if (phase == "derived") RunDerivedVerify(verifySw, cadDir, result);
                    else RunBaseHoleVerify(verifySw, cadDir, result);

                    CloseOwnedSession(ref verifySw, result, verifySession);
                    Require(verifySession.process_exited, "VERIFY_SESSION_DID_NOT_EXIT", 10,
                        "owned verification SolidWorks process did not exit cleanly");
                }

                AssertPostPhaseInventoryAndClosure(cadDir, result);
                CaptureEndHashes(result);
                AuditChangedPaths(result, PhaseAllowlist(cadDir, phase));
                Require(result.unexpected_changed_paths.Count == 0,
                    "UNEXPECTED_READONLY_REOPEN_WRITE_DETECTED", 11,
                    "read-only verification changed a file outside the phase allowlist");
                Require(result.files.TrueForAll(delegate(FileRecord file)
                {
                    return string.Equals(file.sha_after_save, file.sha_after_readonly_reopen,
                        StringComparison.OrdinalIgnoreCase);
                }), "READONLY_REOPEN_CHANGED_PHASE_FILE", 11,
                    "one or more files changed during read-only reopen verification");
                AssertAuthorizationStillValid(result, "before_success_gate");
                result.functional_success = true;
                result.success = true;
                bool assemblyNeedsRegen = phase == "assemblies" &&
                    result.assembly_reopen_needs_regen_files.Count > 0;
                result.status = assemblyNeedsRegen ?
                    "ASSEMBLIES_PHASE_VERIFIED_WITH_EXACT_NEEDS_REGEN" :
                    phase.ToUpperInvariant() + "_PHASE_VERIFIED";
                exitCode = 0;
            }
            catch (GateException ex)
            {
                result.status = ex.Status;
                result.error = ex.Message;
                exitCode = ex.ExitCode;
            }
            catch (Exception ex)
            {
                result.status = "CONFIGURE_NATIVE_WIDTH_888_EXCEPTION";
                result.error = SafeExceptionText(ex);
                exitCode = 90;
            }
            finally
            {
                try
                {
                    if (verifySw != null) CloseOwnedSession(ref verifySw, result, verifySession);
                }
                catch (Exception closeVerifyError)
                {
                    result.cleanup_errors.Add("close verify session: " +
                        SafeExceptionText(closeVerifyError));
                }
                try
                {
                    if (editSw != null) CloseOwnedSession(ref editSw, result, editSession);
                }
                catch (Exception closeEditError)
                {
                    result.cleanup_errors.Add("close edit session: " +
                        SafeExceptionText(closeEditError));
                }
                try
                {
                    CleanupOnlyCreatedProcesses(result);
                }
                catch (Exception cleanupError)
                {
                    result.cleanup_errors.Add("cleanup boundary: " + SafeExceptionText(cleanupError));
                }
                try
                {
                    result.final_sldworks_process_ids.AddRange(ProcessIds("SLDWORKS"));
                    result.final_sldprocmon_process_ids.AddRange(ProcessIds("sldProcMon"));
                }
                catch (Exception enumerateError)
                {
                    result.cleanup_errors.Add("final process enumeration: " +
                        SafeExceptionText(enumerateError));
                }

                if (result.success && (result.final_sldworks_process_ids.Count != 0 ||
                    result.final_sldprocmon_process_ids.Count != 0 || result.cleanup_errors.Count != 0))
                {
                    result.success = false;
                    result.status = "OWNED_PROCESS_CLEANUP_GATE_FAILED";
                    result.error = "owned SolidWorks process cleanup was incomplete";
                    if (exitCode == 0) exitCode = 12;
                }

                if (!result.success)
                {
                    RollBack(result);
                    AssertRollbackInventory(result);
                }
                else result.original_restored_on_failure = true;

                result.completed_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                if (!result.output_path_allowlist_match)
                {
                    DeleteBackupDirectory(result);
                    Console.Error.WriteLine("evidence not written because out-json was not allowlisted");
                }
                else
                {
                    if (!result.success)
                    {
                        if (result.evidence_path_unused_at_start)
                        {
                            result.evidence_write_attempted = true;
                            result.failure_evidence_write_succeeded = true;
                            try { WriteFailureEvidenceBestEffort(outJson, result); }
                            catch (Exception failureEvidenceError)
                            {
                                result.failure_evidence_write_succeeded = false;
                                result.cleanup_errors.Add("failure evidence commit: " +
                                    SafeExceptionText(failureEvidenceError));
                            }
                        }
                        else Console.Error.WriteLine("evidence not written because its fixed path was pre-existing");
                        DeleteBackupDirectory(result);
                    }
                    else try
                    {
                        AssertAuthorizationStillValid(result, "before_evidence_and_receipt_commit");
                        result.evidence_write_attempted = true;
                        result.evidence_write_succeeded = true;
                        result.evidence_finalize_write_succeeded = true;
                        result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                        WriteJsonAtomicNew(outJson, CanonicalWidthEvidenceDocument(result));
                        result.evidence_write_succeeded = File.Exists(outJson) && JsonEvidenceCommitted(outJson, result);
                        Require(result.evidence_write_succeeded, "EVIDENCE_COMMIT_VERIFY_FAILED", 91,
                            "evidence file was not durably committed with success=true and finalize=true");
                        string evidenceSha256 = Sha256(outJson);
                        AssertAuthorizationStillValid(result, "after_evidence_commit_before_receipt");
                        preparedReceipt = PreparePhaseReceipt(result, evidenceSha256);
                        CommitPreparedReceipt(preparedReceipt);
                        string receiptPreDigest;
                        string receiptPostDigest;
                        Require(ValidatePhaseReceipt(preparedReceipt.path, result.phase, result,
                                preparedReceipt.predecessor_sha256, result.initial_inventory_digest,
                                out receiptPreDigest, out receiptPostDigest),
                            "PHASE_RECEIPT_COMMIT_VERIFY_FAILED", 91,
                            "phase receipt failed post-write validation");
                        AssertAuthorizationStillValid(result, "after_receipt_commit");
                        result.commit_completed_at_utc = DateTime.UtcNow.ToString("o",
                            CultureInfo.InvariantCulture);
                        Require(CompletionTimeWithinRecordedExpiries(result.commit_completed_at_utc, result),
                            "AUTHORIZATION_EXPIRED_DURING_COMMIT", 91,
                            "authorization or lease expired before the evidence/receipt pair finished committing");
                        Require(JsonEvidenceCommitted(outJson, result) &&
                                string.Equals(Sha256(outJson), evidenceSha256,
                                    StringComparison.OrdinalIgnoreCase) &&
                                ValidatePhaseReceipt(preparedReceipt.path, result.phase, result,
                                    preparedReceipt.predecessor_sha256, result.initial_inventory_digest,
                                    out receiptPreDigest, out receiptPostDigest),
                            "EVIDENCE_RECEIPT_PAIR_FINAL_VERIFY_FAILED", 91,
                            "the final evidence and receipt pair are inconsistent");
                        DeleteBackupDirectory(result);
                        Console.WriteLine(outJson);
                    }
                    catch (Exception jsonError)
                    {
                        result.evidence_write_succeeded = false;
                        result.evidence_finalize_write_succeeded = false;
                        result.evidence_finalize_error = SafeExceptionText(jsonError);
                        Console.Error.WriteLine(result.evidence_finalize_error);
                        result.success = false;
                        result.functional_success = false;
                        result.status = "EVIDENCE_WRITE_FAILED_ROLLED_BACK";
                        result.error = result.evidence_finalize_error;
                        RollBack(result);
                        AssertRollbackInventory(result);
                        if (!result.original_restored_on_failure)
                        {
                            result.status = "EVIDENCE_WRITE_FAILED_ROLLBACK_INCOMPLETE";
                            result.error += " | rollback did not restore the exact original inventory";
                        }
                        DeletePreparedReceiptBestEffort(preparedReceipt);
                        DeleteCurrentPhaseReceiptBestEffort(result);
                        DeleteUnpairedSuccessEvidenceBestEffort(outJson, result);
                        result.failure_evidence_write_succeeded = true;
                        try { WriteFailureEvidenceBestEffort(outJson, result); }
                        catch (Exception failureEvidenceError)
                        {
                            result.failure_evidence_write_succeeded = false;
                            result.cleanup_errors.Add("failure evidence commit: " +
                                SafeExceptionText(failureEvidenceError));
                        }
                        DeleteBackupDirectory(result);
                        exitCode = 91;
                    }
                }
                DeletePreparedReceiptBestEffort(preparedReceipt);
                ReleasePhaseLock();
            }

            return exitCode;
        }

        private static Result NewResult(string cadDir, string outJson, string phase)
        {
            var result = new Result
            {
                schema = ExpectedEvidenceSchema,
                generated_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                cad_directory = cadDir,
                out_json = outJson,
                phase = phase,
                target_width_mm = 888.0,
                source_width_mm = 760.0,
                purpose = ExpectedPurpose,
                transaction_mode = "full_pack_backup_phase_allowlist_mutate_close_new_readonly_reopen_verify_or_rollback"
            };
            result.external_geometry_pending.AddRange(new[]
            {
                "door panels are excluded from this width stage",
                "the exact 1/12 W381 door module is supplied by the dedicated door-module stage",
                "door-panel BaseBody or mirrored geometry is never refreshed by this tool"
            });
            result.derived_exclusions.AddRange(new[]
            {
                "箱体竖隔板L.sldprt: geometry unchanged; local SplitBody reference is rebound and locked",
                "箱体竖隔板R.SLDPRT: frozen independent right partition; byte hash must remain unchanged",
                "门框 竖隔板L.sldprt: central frame-divider geometry unchanged",
                "门框 竖隔板R.SLDPRT: central frame-divider geometry unchanged",
                "箱体竖隔板加强件.sldprt: central reinforcement geometry unchanged",
                "箱体侧板加强筋2.sldprt: audited width-invariant; excluded from derived writes",
                "all door-panel in-context references: excluded from derived refresh"
            });
            return result;
        }

        private static void AssertTrustedAttemptDirectory(string cadDir, Result result)
        {
            string configuredRoot = System.Environment.GetEnvironmentVariable(AttemptRootEnvironmentVariable) ?? "";
            Require(!string.IsNullOrWhiteSpace(configuredRoot) && Path.IsPathRooted(configuredRoot),
                "TRUSTED_ATTEMPT_ROOT_MISSING", 3,
                AttemptRootEnvironmentVariable + " must name the worker-owned absolute attempts directory");
            string attemptRoot = Path.GetFullPath(configuredRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Require(Directory.Exists(attemptRoot), "TRUSTED_ATTEMPT_ROOT_NOT_FOUND", 3,
                "configured worker-owned attempts directory does not exist");
            Require(IsUnder(cadDir, attemptRoot) && !SamePath(cadDir, attemptRoot),
                "REFUSED_OUTSIDE_TRUSTED_ATTEMPT_ROOT", 3,
                "cad-dir is outside the worker-owned attempts directory");

            string relative = cadDir.Substring(attemptRoot.Length).TrimStart('\\', '/');
            string[] segments = relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            bool taskSegmentSafe = segments.Length == 4 && segments[0].Length > 0 &&
                segments[0].All(delegate(char value)
                {
                    return char.IsLetterOrDigit(value) || value == '-' || value == '_' || value == '.';
                });
            bool attemptSegmentSafe = segments.Length == 4 && segments[1].StartsWith("attempt-",
                StringComparison.OrdinalIgnoreCase) && segments[1].Length == 12 &&
                segments[1].Substring(8).All(char.IsDigit);
            bool leafMatches = segments.Length == 4 &&
                string.Equals(segments[2], "native_cad", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[3], "working_pack", StringComparison.OrdinalIgnoreCase) &&
                cadDir.EndsWith(ExpectedCadLeaf, StringComparison.OrdinalIgnoreCase);
            Require(taskSegmentSafe && attemptSegmentSafe && leafMatches,
                "TRUSTED_ATTEMPT_DIRECTORY_SHAPE_MISMATCH", 3,
                "cad-dir must be <attempt-root>/<task-id>/attempt-NNNN/native_cad/working_pack");

            string walk = attemptRoot;
            Require(!HasReparsePoint(walk), "TRUSTED_ATTEMPT_ROOT_IS_REPARSE_POINT", 3,
                "worker-owned attempts directory must not be a reparse point");
            foreach (string segment in segments)
            {
                walk = Path.Combine(walk, segment);
                Require(!Directory.Exists(walk) || !HasReparsePoint(walk),
                    "TRUSTED_ATTEMPT_PATH_HAS_REPARSE_POINT", 3,
                    "attempt path contains a reparse point: " + walk);
            }

            string attemptDir = Directory.GetParent(Directory.GetParent(cadDir).FullName).FullName;
            string evidenceDir = Path.Combine(attemptDir, "evidence");
            Require(SamePath(Path.GetDirectoryName(result.out_json), evidenceDir),
                "OUTPUT_OUTSIDE_ATTEMPT_EVIDENCE_DIRECTORY", 3,
                "out-json must be a direct child of the current attempt evidence directory");
            Require(!Directory.Exists(evidenceDir) || !HasReparsePoint(evidenceDir),
                "ATTEMPT_EVIDENCE_DIRECTORY_IS_REPARSE_POINT", 3,
                "attempt evidence directory must not be a reparse point");
            result.output_path_allowlist_match = true;
            string planPath = Path.Combine(attemptDir, "native_build_plan.json");
            Require(File.Exists(planPath), "IMMUTABLE_NATIVE_PLAN_MISSING", 3,
                "native_build_plan.json is missing beside native_cad");
            Require(!HasReparsePoint(planPath) && FileLinkCount(planPath) == 1,
                "IMMUTABLE_NATIVE_PLAN_FILE_IDENTITY_INVALID", 3,
                "native_build_plan.json must be a non-reparse, single-link regular file");
            string expectedPlanSha = (System.Environment.GetEnvironmentVariable(PlanShaEnvironmentVariable) ?? "")
                .Trim();
            Require(expectedPlanSha.Length == 64 && expectedPlanSha.All(IsHex),
                "IMMUTABLE_NATIVE_PLAN_SHA_MISSING", 3,
                PlanShaEnvironmentVariable + " must contain the task-store plan SHA-256");
            string actualPlanSha = Sha256(planPath);
            Require(string.Equals(expectedPlanSha, actualPlanSha, StringComparison.OrdinalIgnoreCase),
                "IMMUTABLE_NATIVE_PLAN_SHA_MISMATCH", 3,
                "native_build_plan.json no longer matches the task-store SHA-256");

            Dictionary<string, object> plan = ReadJsonObject(planPath);
            Dictionary<string, object> planTask = ChildObject(plan, "task");
            Dictionary<string, object> planRequest = ChildObject(plan, "request");
            Dictionary<string, object> recipe = ChildObject(plan, "recipe");
            Dictionary<string, object> geometry = ChildObject(plan, "geometry");
            Dictionary<string, object> qualityBoundary = ChildObject(plan, "qualityBoundary");
            Dictionary<string, object> executionBoundary = ChildObject(plan, "executionBoundary");
            Require(string.Equals(TextValue(plan, "schema"), "winnsen.native_build_plan.v1",
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(plan, "purpose"), ExpectedPurpose, StringComparison.Ordinal) &&
                string.Equals(TextValue(recipe, "id"), ExpectedRecipeId, StringComparison.Ordinal) &&
                Near(NumberValue(recipe, "version"), 1.0, 0.0) &&
                string.Equals(TextValue(recipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase),
                "IMMUTABLE_NATIVE_PLAN_RECIPE_MISMATCH", 3,
                "native build plan is not the trusted fixed 888W/14-door recipe");
            Require(Near(NumberValue(geometry, "cabinetWidthMm"), 888.0, 1e-9) &&
                Near(NumberValue(geometry, "cabinetHeightMm"), 1917.0, 1e-9) &&
                Near(NumberValue(geometry, "cabinetDepthMm"), 550.0, 1e-9) &&
                Near(NumberValue(geometry, "columns"), 2.0, 1e-9) &&
                Near(NumberValue(geometry, "doorCount"), 14.0, 1e-9) &&
                Near(NumberValue(geometry, "doorPanelWidthMm"), 381.0, 1e-9) &&
                NumberArrayEquals(ArrayValue(geometry, "columnDoorCounts"), new[] { 7.0, 7.0 }) &&
                string.Equals(TextValue(geometry, "rowSequence"), "L1111111-R1111111",
                    StringComparison.Ordinal),
                "IMMUTABLE_NATIVE_PLAN_GEOMETRY_MISMATCH", 3,
                "native build plan geometry is not exact 888x1917x550, 2-column, 14-door, W381");
            Require(string.Equals(TextValue(planTask, "id"), segments[0], StringComparison.Ordinal) &&
                NumberValue(planTask, "revisionAtPlanning") > 0 &&
                IsSha256(TextValue(planTask, "digest")) &&
                !string.IsNullOrWhiteSpace(TextValue(planRequest, "fingerprint")) &&
                IsSha256(TextValue(planRequest, "digest")),
                "IMMUTABLE_NATIVE_PLAN_TASK_BINDING_MISMATCH", 3,
                "plan task id/revision/digest/request binding is incomplete or mismatched");
            Require(ValidateStageContracts(ArrayValue(plan, "stageContracts")) &&
                StringArrayEquals(ArrayValue(plan, "requiredChecks"), RequiredChecks()),
                "IMMUTABLE_NATIVE_PLAN_CONTRACT_MISMATCH", 3,
                "stageContracts or requiredChecks differ from the trusted recipe v1 contract");
            Require(ValidateStageReceiptContracts(ChildObject(plan, "stageReceiptContracts")),
                "IMMUTABLE_NATIVE_STAGE_RECEIPT_CONTRACT_MISMATCH", 3,
                "stageReceiptContracts must exactly match the shared nine-stage receipt contract");
            Require(BoolValue(qualityBoundary, "planningOnly") &&
                !BoolValue(executionBoundary, "executorImplemented"),
                "PLAN_EXECUTION_BOUNDARY_DRIFT", 3,
                "the current frozen plan must remain planningOnly with executorImplemented=false");

            string repositoryRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", ".."));
            Dictionary<string, object> trustedManifests = ChildObject(plan,
                "trustedToolchainManifests");
            Require(ExactKeys(trustedManifests, TrustedToolchainManifestKeys) &&
                TrustedToolchainManifestKeys.All(delegate(string key)
                {
                    return IsSha256(TextValue(trustedManifests, key));
                }), "IMMUTABLE_NATIVE_TOOLCHAIN_MANIFEST_MAP_MISMATCH", 3,
                "trustedToolchainManifests must have the exact six registered tool SHA-256 bindings");
            ToolchainManifestBinding seedManifest = new ToolchainManifestBinding();
            ToolchainManifestBinding widthManifest = new ToolchainManifestBinding();
            Require(ValidateToolchainManifest(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json",
                    "native_seed_pack_888x14_v1",
                    "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.cs",
                    "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.exe",
                    "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/Verify-SeedPack888Native.ps1",
                    TextValue(trustedManifests, "native_seed_pack_888x14_v1"), out seedManifest) &&
                ValidateToolchainManifest(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/width_888_native_v1/toolchain_manifest.json",
                    ExpectedToolId,
                    "workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.cs",
                    "workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.exe",
                    "workers/native_model_requests/development/v1/tools/width_888_native_v1/verify_static.mjs",
                    TextValue(trustedManifests, ExpectedToolId), out widthManifest),
                "TRUSTED_TOOLCHAIN_MANIFEST_LIVE_BINDING_FAILED", 3,
                "seed and width manifests must match the immutable plan and all live tool artifacts");
            Require(string.Equals(widthManifest.source_normalized_sha256, ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(widthManifest.executable_sha256,
                    Sha256(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    StringComparison.OrdinalIgnoreCase),
                "WIDTH_TOOLCHAIN_MANIFEST_SELF_IDENTITY_MISMATCH", 3,
                "width manifest is not bound to this compiled source/executable pair");

            string configuredAuthorizationPath = (System.Environment.GetEnvironmentVariable(
                AuthorizationPathEnvironmentVariable) ?? "").Trim();
            Require(!string.IsNullOrWhiteSpace(configuredAuthorizationPath) &&
                Path.IsPathRooted(configuredAuthorizationPath),
                "WORKER_EXECUTION_AUTHORIZATION_PATH_MISSING", 3,
                AuthorizationPathEnvironmentVariable + " must name the worker-issued authorization artifact");
            string authorizationPath = Path.GetFullPath(configuredAuthorizationPath);
            string expectedAuthorizationPath = Path.Combine(attemptDir, "execution_authorizations",
                ExpectedToolId + ".json");
            Require(SamePath(authorizationPath, expectedAuthorizationPath) &&
                !HasReparsePoint(Path.GetDirectoryName(authorizationPath)) &&
                !HasReparsePoint(authorizationPath) && FileLinkCount(authorizationPath) == 1,
                "WORKER_EXECUTION_AUTHORIZATION_PATH_MISMATCH", 3,
                "authorization must be the non-reparse worker artifact execution_authorizations/native_width_888_v1.json");
            Require(File.Exists(authorizationPath), "WORKER_EXECUTION_AUTHORIZATION_MISSING", 3,
                "planning-only plan cannot execute without a separate immutable worker authorization");
            string expectedAuthorizationSha = (System.Environment.GetEnvironmentVariable(
                AuthorizationShaEnvironmentVariable) ?? "").Trim();
            Require(IsSha256(expectedAuthorizationSha), "WORKER_EXECUTION_AUTHORIZATION_SHA_MISSING", 3,
                AuthorizationShaEnvironmentVariable + " must contain the task-store authorization SHA-256");
            string actualAuthorizationSha = Sha256(authorizationPath);
            Require(string.Equals(expectedAuthorizationSha, actualAuthorizationSha,
                    StringComparison.OrdinalIgnoreCase),
                "WORKER_EXECUTION_AUTHORIZATION_SHA_MISMATCH", 3,
                "native_execution_authorization.json does not match the task-store SHA-256");
            Dictionary<string, object> authorization = ReadJsonObject(authorizationPath);
            Dictionary<string, object> authTask = ChildObject(authorization, "task");
            Dictionary<string, object> authRequest = ChildObject(authorization, "request");
            Dictionary<string, object> authPlan = ChildObject(authorization, "plan");
            Dictionary<string, object> authRecipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> authTool = ChildObject(authorization, "tool");
            Dictionary<string, object> authSeed = ChildObject(authorization, "seed");
            Dictionary<string, object> authExecution = ChildObject(authorization, "execution");
            int attemptNumber = int.Parse(segments[1].Substring(8), CultureInfo.InvariantCulture);
            string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            AuthorizationBinding authorizationBinding;
            Require(AuthorizationContractValid(authorization, TextValue(plan, "workerId"), segments[0],
                    NumberValue(planTask, "revisionAtPlanning"), TextValue(planTask, "digest"),
                    TextValue(planRequest, "fingerprint"), TextValue(planRequest, "digest"), actualPlanSha,
                    ExpectedRecipeId, 1.0, ExpectedRecipeDigest, ExpectedToolId, ExpectedSourceSha256,
                    Sha256(executablePath), AuthorizationPreInventoryDigest(cadDir), attemptNumber,
                    true, "",
                    out authorizationBinding),
                "WORKER_EXECUTION_AUTHORIZATION_BINDING_MISMATCH", 3,
                "authorization must exactly match the shared task/lease/request/plan/recipe/tool/seed/phase contract");
            Require(string.Equals(Sha256(authorizationPath), actualAuthorizationSha,
                    StringComparison.OrdinalIgnoreCase),
                "WORKER_EXECUTION_AUTHORIZATION_CHANGED_DURING_VALIDATION", 3,
                "authorization artifact changed while its binding was being validated");
            Require(string.Equals(Sha256(planPath), actualPlanSha, StringComparison.OrdinalIgnoreCase),
                "IMMUTABLE_NATIVE_PLAN_CHANGED_DURING_VALIDATION", 3,
                "native_build_plan.json changed while its contract was being validated");

            result.cad_directory_allowlist_match = true;
            result.attempt_root = attemptRoot;
            result.plan_path = planPath;
            result.plan_sha256 = actualPlanSha;
            result.plan_sha_match = true;
            result.recipe_id = TextValue(recipe, "id");
            result.recipe_digest = TextValue(recipe, "digest");
            result.recipe_trust_gate = true;
            result.authorization_path = authorizationPath;
            result.authorization_sha256 = actualAuthorizationSha;
            result.authorization_gate = true;
            result.task_id = segments[0];
            result.task_revision = Convert.ToInt32(NumberValue(planTask, "revisionAtPlanning"),
                CultureInfo.InvariantCulture);
            result.task_digest = TextValue(planTask, "digest");
            result.request_digest = TextValue(planRequest, "digest");
            result.request_fingerprint = TextValue(planRequest, "fingerprint");
            result.worker_id = TextValue(plan, "workerId");
            result.recipe_version = 1;
            result.authorization_id = authorizationBinding.authorization_id;
            result.authorization_issued_at_utc = authorizationBinding.issued_at_utc;
            result.authorization_json_base64 = Convert.ToBase64String(File.ReadAllBytes(authorizationPath));
            result.authorization_seed_inventory_digest = AuthorizationPreInventoryDigest(cadDir);
            result.lease_id = authorizationBinding.lease_id;
            result.lease_expires_at_utc = authorizationBinding.lease_expires_at_utc;
            result.authorization_expires_at_utc = authorizationBinding.expires_at_utc;
            result.attempt_number = attemptNumber;
            result.tool_source_normalized_sha256 = ExpectedSourceSha256;
            result.tool_executable_sha256 = Sha256(executablePath);
            result.repository_root = repositoryRoot;
            result.seed_toolchain_manifest_path = seedManifest.path;
            result.seed_toolchain_manifest_sha256 = seedManifest.sha256;
            result.seed_tool_source_normalized_sha256 = seedManifest.source_normalized_sha256;
            result.seed_tool_executable_sha256 = seedManifest.executable_sha256;
            result.width_toolchain_manifest_path = widthManifest.path;
            result.width_toolchain_manifest_sha256 = widthManifest.sha256;
            result.toolchain_manifest_gate = true;
            result.authorization_checkpoints.Add(new AuthorizationCheckpoint
            {
                name = "initial_binding",
                checked_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                valid = true
            });
        }

        private static bool ExactKeys(Dictionary<string, object> value, string[] expected)
        {
            return value != null && value.Keys.OrderBy(delegate(string key) { return key; },
                StringComparer.Ordinal).SequenceEqual(expected.OrderBy(delegate(string key) { return key; },
                    StringComparer.Ordinal), StringComparer.Ordinal);
        }

        private static bool ExactBoolean(Dictionary<string, object> value, string name, bool expected)
        {
            object raw;
            return value != null && value.TryGetValue(name, out raw) && raw is bool && (bool)raw == expected;
        }

        private static bool ExactNumber(Dictionary<string, object> value, string name, double expected)
        {
            object raw;
            if (value == null || !value.TryGetValue(name, out raw) || raw == null || raw is bool ||
                !(raw is byte || raw is sbyte || raw is short || raw is ushort || raw is int ||
                  raw is uint || raw is long || raw is ulong || raw is float || raw is double ||
                  raw is decimal)) return false;
            return Near(Convert.ToDouble(raw, CultureInfo.InvariantCulture), expected, 0.0);
        }

        private static Dictionary<string, object> ExpectedAuthorizationBindings(string workerId,
            string taskId, double taskRevision, string taskDigest, string leaseId, string leaseExpiresAt,
            string requestFingerprint, string requestDigest, string planSha, string recipeId,
            double recipeVersion, string recipeDigest, string toolId, string toolSourceSha,
            string toolExecutableSha, string seedDigest, int attempt)
        {
            return ExpectedAuthorizationBindingsForPhases(workerId, taskId, taskRevision, taskDigest,
                leaseId, leaseExpiresAt, requestFingerprint, requestDigest, planSha, recipeId,
                recipeVersion, recipeDigest, toolId, toolSourceSha, toolExecutableSha, seedDigest,
                attempt, PhaseOrder());
        }

        private static Dictionary<string, object> ExpectedAuthorizationBindingsForPhases(string workerId,
            string taskId, double taskRevision, string taskDigest, string leaseId, string leaseExpiresAt,
            string requestFingerprint, string requestDigest, string planSha, string recipeId,
            double recipeVersion, string recipeDigest, string toolId, string toolSourceSha,
            string toolExecutableSha, string seedDigest, int attempt, string[] expectedPhases)
        {
            return new Dictionary<string, object>
            {
                { "workerId", workerId },
                { "task", new Dictionary<string, object>
                    {
                        { "id", taskId }, { "revision", taskRevision },
                        { "digest", taskDigest.ToUpperInvariant() }, { "leaseId", leaseId },
                        { "leaseExpiresAt", leaseExpiresAt }
                    }
                },
                { "request", new Dictionary<string, object>
                    {
                        { "fingerprint", requestFingerprint },
                        { "digest", requestDigest.ToUpperInvariant() }
                    }
                },
                { "plan", new Dictionary<string, object> { { "sha256", planSha.ToUpperInvariant() } } },
                { "recipe", new Dictionary<string, object>
                    {
                        { "id", recipeId }, { "version", recipeVersion },
                        { "digest", recipeDigest.ToUpperInvariant() }
                    }
                },
                { "tool", new Dictionary<string, object>
                    {
                        { "id", toolId }, { "sourceNormalizedSha256", toolSourceSha.ToUpperInvariant() },
                        { "executableSha256", toolExecutableSha.ToUpperInvariant() }
                    }
                },
                { "seed", new Dictionary<string, object>
                    {
                        { "inventoryDigest", seedDigest.ToUpperInvariant() }
                    }
                },
                { "execution", new Dictionary<string, object>
                    {
                        { "authorized", true }, { "attempt", attempt }, { "phases", expectedPhases }
                    }
                }
            };
        }

        private static string StableJson(object value)
        {
            if (value == null) return "null";
            string text = value as string;
            if (text != null) return JsonString(text);
            if (value is bool) return (bool)value ? "true" : "false";
            if (value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal)
                return EcmaJsonNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            var objectRow = value as Dictionary<string, object>;
            if (objectRow != null)
                return "{" + string.Join(",", objectRow.Keys.OrderBy(delegate(string key) { return key; },
                    StringComparer.Ordinal).Select(delegate(string key)
                    {
                        return JsonString(key) + ":" + StableJson(objectRow[key]);
                    }).ToArray()) + "}";
            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                var rows = new List<string>();
                foreach (object row in enumerable) rows.Add(StableJson(row));
                return "[" + string.Join(",", rows.ToArray()) + "]";
            }
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 };
            return StableJson(serializer.DeserializeObject(serializer.Serialize(value)));
        }

        private static string JsonString(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(
                            ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else if (char.IsHighSurrogate(character) && index + 1 < value.Length &&
                            char.IsLowSurrogate(value[index + 1]))
                        {
                            builder.Append(character).Append(value[index + 1]);
                            index++;
                        }
                        else if (char.IsSurrogate(character)) builder.Append("\\u").Append(
                            ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        private static string EcmaJsonNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "null";
            if (value == 0.0) return "0";
            long bits = BitConverter.DoubleToInt64Bits(value);
            string candidate = value.ToString("G17", CultureInfo.InvariantCulture);
            for (int precision = 1; precision <= 17; precision++)
            {
                string current = value.ToString("G" + precision.ToString(
                    CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                double parsed;
                if (double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out parsed) && BitConverter.DoubleToInt64Bits(parsed) == bits)
                {
                    candidate = current;
                    break;
                }
            }
            bool negative = candidate.StartsWith("-", StringComparison.Ordinal);
            if (negative) candidate = candidate.Substring(1);
            int exponentMarker = candidate.IndexOfAny(new[] { 'E', 'e' });
            int explicitExponent = 0;
            if (exponentMarker >= 0)
            {
                explicitExponent = int.Parse(candidate.Substring(exponentMarker + 1),
                    NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                candidate = candidate.Substring(0, exponentMarker);
            }
            int decimalPoint = candidate.IndexOf('.');
            int digitsBeforePoint = decimalPoint < 0 ? candidate.Length : decimalPoint;
            string rawDigits = candidate.Replace(".", "");
            int firstNonZero = 0;
            while (firstNonZero < rawDigits.Length && rawDigits[firstNonZero] == '0')
                firstNonZero++;
            if (firstNonZero == rawDigits.Length) return "0";
            string digits = rawDigits.Substring(firstNonZero);
            int scientificExponent = explicitExponent + digitsBeforePoint - firstNonZero - 1;
            string output;
            if (scientificExponent >= -6 && scientificExponent < 21)
            {
                int decimalPosition = scientificExponent + 1;
                if (decimalPosition <= 0)
                    output = "0." + new string('0', -decimalPosition) + digits;
                else if (decimalPosition >= digits.Length)
                    output = digits + new string('0', decimalPosition - digits.Length);
                else
                    output = digits.Substring(0, decimalPosition) + "." +
                        digits.Substring(decimalPosition);
            }
            else
            {
                output = digits.Substring(0, 1);
                if (digits.Length > 1) output += "." + digits.Substring(1);
                output += "e" + (scientificExponent >= 0 ? "+" : "") +
                    scientificExponent.ToString(CultureInfo.InvariantCulture);
            }
            return negative ? "-" + output : output;
        }

        private static bool AuthorizationContractValid(Dictionary<string, object> authorization,
            string expectedWorkerId, string expectedTaskId, double expectedTaskRevision,
            string expectedTaskDigest, string expectedRequestFingerprint, string expectedRequestDigest,
            string expectedPlanSha, string expectedRecipeId, double expectedRecipeVersion,
            string expectedRecipeDigest, string expectedToolId, string expectedToolSourceSha,
            string expectedToolExecutableSha, string expectedSeedDigest, int expectedAttempt,
            bool requireCurrentTime, string completedAtText, out AuthorizationBinding binding)
        {
            return AuthorizationContractValidForPhases(authorization, expectedWorkerId, expectedTaskId,
                expectedTaskRevision, expectedTaskDigest, expectedRequestFingerprint,
                expectedRequestDigest, expectedPlanSha, expectedRecipeId, expectedRecipeVersion,
                expectedRecipeDigest, expectedToolId, expectedToolSourceSha, expectedToolExecutableSha,
                expectedSeedDigest, expectedAttempt, PhaseOrder(), requireCurrentTime, completedAtText,
                out binding);
        }

        private static bool AuthorizationContractValidForPhases(Dictionary<string, object> authorization,
            string expectedWorkerId, string expectedTaskId, double expectedTaskRevision,
            string expectedTaskDigest, string expectedRequestFingerprint, string expectedRequestDigest,
            string expectedPlanSha, string expectedRecipeId, double expectedRecipeVersion,
            string expectedRecipeDigest, string expectedToolId, string expectedToolSourceSha,
            string expectedToolExecutableSha, string expectedSeedDigest, int expectedAttempt,
            string[] expectedPhases, bool requireCurrentTime, string completedAtText,
            out AuthorizationBinding binding)
        {
            binding = new AuthorizationBinding();
            try
            {
                if (!ExactKeys(authorization, AuthorizationTopLevelKeys)) return false;
                Dictionary<string, object> task = ChildObject(authorization, "task");
                Dictionary<string, object> request = ChildObject(authorization, "request");
                Dictionary<string, object> plan = ChildObject(authorization, "plan");
                Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
                Dictionary<string, object> tool = ChildObject(authorization, "tool");
                Dictionary<string, object> seed = ChildObject(authorization, "seed");
                Dictionary<string, object> execution = ChildObject(authorization, "execution");
                Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");
                if (!ExactKeys(task, AuthorizationTaskKeys) ||
                    !ExactKeys(request, AuthorizationRequestKeys) || !ExactKeys(plan, AuthorizationPlanKeys) ||
                    !ExactKeys(recipe, AuthorizationRecipeKeys) || !ExactKeys(tool, AuthorizationToolKeys) ||
                    !ExactKeys(seed, AuthorizationSeedKeys) || !ExactKeys(execution, AuthorizationExecutionKeys) ||
                    !ExactKeys(quality, AuthorizationQualityBoundaryKeys) ||
                    !ExactBoolean(quality, "engineeringAssistanceReady", false) ||
                    !ExactBoolean(quality, "readyOnlyAfterEveryRequiredCheckPasses", true)) return false;
                string leaseId = TextValue(task, "leaseId");
                string leaseExpiresAt = TextValue(task, "leaseExpiresAt");
                if (string.IsNullOrWhiteSpace(leaseId)) return false;
                Dictionary<string, object> expected = ExpectedAuthorizationBindingsForPhases(expectedWorkerId,
                    expectedTaskId, expectedTaskRevision, expectedTaskDigest, leaseId, leaseExpiresAt,
                    expectedRequestFingerprint, expectedRequestDigest, expectedPlanSha, expectedRecipeId,
                    expectedRecipeVersion, expectedRecipeDigest, expectedToolId, expectedToolSourceSha,
                    expectedToolExecutableSha, expectedSeedDigest, expectedAttempt, expectedPhases);
                var actual = new Dictionary<string, object>
                {
                    { "workerId", authorization["workerId"] }, { "task", task }, { "request", request },
                    { "plan", plan }, { "recipe", recipe }, { "tool", tool }, { "seed", seed },
                    { "execution", execution }
                };
                if (!string.Equals(StableJson(actual), StableJson(expected), StringComparison.Ordinal) ||
                    !string.Equals(TextValue(authorization, "schema"), ExpectedAuthorizationSchema,
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(authorization, "purpose"), ExpectedPurpose,
                        StringComparison.Ordinal)) return false;
                DateTime issued, expires, leaseExpires, completed;
                if (!DateTime.TryParse(TextValue(authorization, "issuedAt"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out issued) ||
                    !DateTime.TryParse(TextValue(authorization, "expiresAt"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out expires) ||
                    !DateTime.TryParse(leaseExpiresAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out leaseExpires) ||
                    expires <= issued || expires - issued > TimeSpan.FromMinutes(30) ||
                    expires > leaseExpires) return false;
                DateTime now = DateTime.UtcNow;
                if (issued > now || requireCurrentTime && (expires <= now || leaseExpires <= now)) return false;
                if (!string.IsNullOrWhiteSpace(completedAtText) &&
                    (!DateTime.TryParse(completedAtText, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out completed) ||
                        completed < issued || completed > expires || completed > leaseExpires)) return false;
                var identity = new Dictionary<string, object>(expected, StringComparer.Ordinal)
                {
                    { "issuedAt", TextValue(authorization, "issuedAt") },
                    { "expiresAt", TextValue(authorization, "expiresAt") }
                };
                string expectedId = "native-auth-" + Sha256Text(StableJson(identity)).Substring(0, 32)
                    .ToLowerInvariant();
                if (!string.Equals(TextValue(authorization, "authorizationId"), expectedId,
                        StringComparison.Ordinal)) return false;
                binding.authorization_id = expectedId;
                binding.issued_at_utc = TextValue(authorization, "issuedAt");
                binding.expires_at_utc = TextValue(authorization, "expiresAt");
                binding.lease_id = leaseId;
                binding.lease_expires_at_utc = leaseExpiresAt;
                return true;
            }
            catch { return false; }
        }

        private static void AssertAuthorizationStillValid(Result result, string checkpoint)
        {
            bool valid = result != null && result.authorization_gate &&
                File.Exists(result.authorization_path) &&
                !HasReparsePoint(Path.GetDirectoryName(result.authorization_path)) &&
                !HasReparsePoint(result.authorization_path) &&
                FileLinkCount(result.authorization_path) == 1 &&
                string.Equals(Sha256(result.authorization_path), result.authorization_sha256,
                    StringComparison.OrdinalIgnoreCase);
            Dictionary<string, object> authorization = null;
            try { if (valid) authorization = ReadJsonObject(result.authorization_path); }
            catch { valid = false; }
            AuthorizationBinding binding = new AuthorizationBinding();
            valid = valid && AuthorizationContractValid(authorization, result.worker_id, result.task_id,
                result.task_revision, result.task_digest, result.request_fingerprint, result.request_digest,
                result.plan_sha256, result.recipe_id, result.recipe_version, result.recipe_digest,
                ExpectedToolId, ExpectedSourceSha256, result.tool_executable_sha256,
                result.authorization_seed_inventory_digest, result.attempt_number, true,
                result.completed_at_utc,
                out binding) &&
                string.Equals(binding.authorization_id, result.authorization_id, StringComparison.Ordinal) &&
                string.Equals(binding.lease_id, result.lease_id, StringComparison.Ordinal) &&
                string.Equals(binding.lease_expires_at_utc, result.lease_expires_at_utc,
                    StringComparison.Ordinal) &&
                string.Equals(binding.expires_at_utc, result.authorization_expires_at_utc,
                    StringComparison.Ordinal) &&
                string.Equals(Sha256(result.authorization_path), result.authorization_sha256,
                    StringComparison.OrdinalIgnoreCase);
            result.authorization_checkpoints.Add(new AuthorizationCheckpoint
            {
                name = checkpoint,
                checked_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                valid = valid
            });
            Require(valid, "EXECUTION_AUTHORIZATION_EXPIRED_OR_CHANGED", 3,
                "short-lived worker authorization expired or changed at checkpoint " + checkpoint +
                "; this tool does not claim continuous task-store CAS validation");
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(IsHex);
        }

        private static object[] ArrayValue(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null) return new object[0];
            object[] direct = value as object[];
            if (direct != null) return direct;
            var list = value as ArrayList;
            return list == null ? new object[0] : list.Cast<object>().ToArray();
        }

        private static bool BoolValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value is bool && (bool)value;
        }

        private static bool NumberArrayEquals(object[] actual, double[] expected)
        {
            return actual.Length == expected.Length && actual.Select(delegate(object value)
            {
                return Safe(delegate { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }, double.NaN);
            }).Zip(expected, delegate(double left, double right) { return Near(left, right, 0.0); }).All(
                delegate(bool value) { return value; });
        }

        private static bool StringArrayEquals(object[] actual, string[] expected)
        {
            return actual.Length == expected.Length && actual.Select(delegate(object value)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            }).SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static string[] PhaseOrder()
        {
            return new[] { "dimensions", "derived", "base-hole", "assemblies" };
        }

        private static string[] RequiredChecks()
        {
            return new[]
            {
                "trusted_recipe_digest_match", "solidworks_2020_native_only", "no_legacy_fallback",
                "no_electrical_components", "measured_cabinet_envelope_matches_recipe",
                "door_count_and_row_centers_match_recipe", "shelf_and_front_frame_boundaries_match_recipe",
                "one_door_one_lock", "native_lock_slot_edge_profile",
                "paired_diameter_5_circle_per_lock_opening", "mechanical_lock_tongue_alignment",
                "sheet_metal_and_flat_pattern_preserved", "rebuild_save_reopen",
                "component_reference_closure", "zero_missing_or_external_component_files",
                "relocated_reopen", "final_hash_inventory_receipt"
            };
        }

        private static bool ValidateStageContracts(object[] values)
        {
            string[] ids = { "claim_and_validate", "clone_native_seed", "configure_native_width",
                "build_native_door_module", "build_native_lock_topology", "assemble_native_model",
                "relocate_reopen_and_package" };
            string[] tools = { "native_task_guard_v1", "native_seed_pack_888x14_v1",
                "native_width_888_v1", "native_door_module_888x14_v1",
                "native_lock_topology_888x14_v1", "native_root_assembly_888x14_v1",
                "native_final_pack_888x14_v1" };
            if (values.Length != ids.Length) return false;
            for (int index = 0; index < ids.Length; index++)
            {
                var row = values[index] as Dictionary<string, object>;
                if (row == null || !string.Equals(TextValue(row, "id"), ids[index], StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "toolId"), tools[index], StringComparison.Ordinal) ||
                    ArrayValue(row, "outputs").Length == 0) return false;
            }
            return true;
        }

        private static bool ValidateStageReceiptContracts(Dictionary<string, object> contracts)
        {
            string[] stages =
            {
                "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
                "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14",
                "final_pack_and_relocated_reopen"
            };
            string[] schemas =
            {
                ExpectedSeedReceiptSchema, "winnsen.native_width_888.phase_receipt.v1",
                "winnsen.native_width_888.phase_receipt.v1",
                "winnsen.native_width_888.phase_receipt.v1",
                "winnsen.native_width_888.phase_receipt.v1",
                "winnsen.16029.native_door_module_receipt.v1",
                "winnsen.16029.native_lock_topology_receipt.v1",
                "winnsen.16029.native_root_assembly_receipt.v1",
                "winnsen.16029.native_final_pack_receipt.v1"
            };
            string[] paths =
            {
                "receipts/clone_native_seed.json", "evidence/width-dimensions.receipt.json",
                "evidence/width-derived.receipt.json", "evidence/width-base-hole.receipt.json",
                "evidence/width-assemblies.receipt.json", "receipts/door_module_888x14.json",
                "receipts/lock_topology_888x14.json", "receipts/root_assembly_888x14.json",
                "receipts/final_pack_and_relocated_reopen.json"
            };
            string[] tools =
            {
                "native_seed_pack_888x14_v1", ExpectedToolId, ExpectedToolId, ExpectedToolId,
                ExpectedToolId, "native_door_module_888x14_v1", "native_lock_topology_888x14_v1",
                "native_root_assembly_888x14_v1", "native_final_pack_888x14_v1"
            };
            string[] predecessors =
            {
                "", "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
                "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14"
            };
            if (!ExactKeys(contracts, stages)) return false;
            for (int index = 0; index < stages.Length; index++)
            {
                Dictionary<string, object> row;
                try { row = ChildObject(contracts, stages[index]); }
                catch { return false; }
                if (!ExactKeys(row, StageReceiptContractKeys) ||
                    !string.Equals(TextValue(row, "schema"), schemas[index], StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "path"), paths[index], StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "producerToolId"), tools[index], StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "predecessor"), predecessors[index],
                        StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static bool ValidateToolchainManifest(string repositoryRoot, string manifestRelativePath,
            string expectedToolId, string expectedSourcePath, string expectedExecutablePath,
            string expectedVerifierPath, string expectedManifestSha, out ToolchainManifestBinding binding)
        {
            binding = new ToolchainManifestBinding();
            try
            {
                string manifestPath = ResolveSafeRepositoryFile(repositoryRoot, manifestRelativePath);
                if (string.IsNullOrWhiteSpace(manifestPath) || !IsSha256(expectedManifestSha) ||
                    !string.Equals(Sha256(manifestPath), expectedManifestSha,
                        StringComparison.OrdinalIgnoreCase)) return false;
                Dictionary<string, object> manifest = ReadJsonObject(manifestPath);
                Dictionary<string, object> tool = ChildObject(manifest, "tool");
                if (!ExactKeys(manifest, ToolchainManifestTopLevelKeys) ||
                    !ExactKeys(tool, ToolchainManifestToolKeys) ||
                    !string.Equals(TextValue(manifest, "schema"), ExpectedToolchainManifestSchema,
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(manifest, "generatedBy"), expectedVerifierPath,
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(tool, "id"), expectedToolId, StringComparison.Ordinal) ||
                    !string.Equals(TextValue(tool, "sourcePath"), expectedSourcePath,
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(tool, "executablePath"), expectedExecutablePath,
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(tool, "verifierPath"), expectedVerifierPath,
                        StringComparison.Ordinal)) return false;
                string sourcePath = ResolveSafeRepositoryFile(repositoryRoot, expectedSourcePath);
                string executablePath = ResolveSafeRepositoryFile(repositoryRoot, expectedExecutablePath);
                string verifierPath = ResolveSafeRepositoryFile(repositoryRoot, expectedVerifierPath);
                if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(executablePath) ||
                    string.IsNullOrWhiteSpace(verifierPath)) return false;
                string sourceSha = NormalizedToolSourceSha256(sourcePath);
                string executableSha = Sha256(executablePath);
                string verifierSha = Sha256(verifierPath);
                if (!string.Equals(TextValue(tool, "sourceNormalizedSha256"), sourceSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TextValue(tool, "executableSha256"), executableSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TextValue(tool, "verifierSha256"), verifierSha,
                        StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.Equals(Sha256(manifestPath), expectedManifestSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(NormalizedToolSourceSha256(sourcePath), sourceSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Sha256(executablePath), executableSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Sha256(verifierPath), verifierSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    new[] { manifestPath, sourcePath, executablePath, verifierPath }.Any(
                        delegate(string path)
                        {
                            return HasReparsePoint(path) || FileLinkCount(path) != 1;
                        })) return false;
                binding.path = manifestPath;
                binding.sha256 = expectedManifestSha.ToUpperInvariant();
                binding.source_normalized_sha256 = sourceSha;
                binding.executable_sha256 = executableSha;
                binding.verifier_sha256 = verifierSha;
                return true;
            }
            catch { return false; }
        }

        private static string ResolveSafeRepositoryFile(string repositoryRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot) || string.IsNullOrWhiteSpace(relativePath) ||
                relativePath.Contains("\\") || Path.IsPathRooted(relativePath)) return "";
            string[] segments = relativePath.Split('/');
            if (segments.Length == 0 || segments.Any(delegate(string segment)
                {
                    return string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..";
                })) return "";
            string root = Path.GetFullPath(repositoryRoot).TrimEnd('\\', '/');
            string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', '\\')));
            if (!IsUnder(path, root) || !File.Exists(path) || HasReparsePoint(root)) return "";
            string walk = root;
            foreach (string segment in segments)
            {
                walk = Path.Combine(walk, segment);
                if (!File.Exists(walk) && !Directory.Exists(walk) || HasReparsePoint(walk)) return "";
            }
            return FileLinkCount(path) == 1 ? path : "";
        }

        private static string NormalizedToolSourceSha256(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n")
                .Replace("\r", "\n");
            source = Regex.Replace(source,
                "private const string ExpectedSourceSha256 = \"[A-F0-9_]+\";",
                "private const string ExpectedSourceSha256 = \"__SOURCE_SHA256__\";");
            source = Regex.Replace(source,
                "private const string ExpectedContractSnapshotSha256 = \"[A-F0-9_]+\";",
                "private const string ExpectedContractSnapshotSha256 = \"__CONTRACT_SHA256__\";");
            return Sha256Text(source);
        }

        private static void AcquirePhaseLock(Result result, string phase)
        {
            string attemptDir = Path.GetDirectoryName(result.plan_path);
            Require(!string.IsNullOrWhiteSpace(attemptDir), "ATTEMPT_DIRECTORY_UNAVAILABLE", 3,
                "could not derive the current attempt directory");
            result.attempt_directory = attemptDir;
            string expectedOutput = Path.Combine(attemptDir, "evidence", "width-" + phase + ".json");
            Require(SamePath(result.out_json, expectedOutput), "PHASE_EVIDENCE_FILE_NAME_MISMATCH", 3,
                "out-json must be exactly evidence/width-" + phase + ".json");
            string lockPath = Path.Combine(attemptDir, PhaseLockDirectoryName);
            try
            {
                phaseLockHandle = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.WriteThrough);
                byte[] payload = Encoding.UTF8.GetBytes(Process.GetCurrentProcess().Id.ToString(
                    CultureInfo.InvariantCulture) + "|" + phase + "|" + DateTime.UtcNow.ToString("o",
                    CultureInfo.InvariantCulture));
                phaseLockHandle.Write(payload, 0, payload.Length);
                phaseLockHandle.Flush(true);
                result.phase_lock_acquired = true;
                result.phase_lock_path = lockPath;
            }
            catch (IOException)
            {
                throw new GateException("WIDTH_STAGE_ATTEMPT_LOCKED", 3,
                    "another width stage owns the current attempt lock");
            }

        }

        private static void ValidatePhaseSequence(Result result, string phase)
        {
            string[] order = PhaseOrder();
            int phaseIndex = Array.IndexOf(order, phase);
            string predecessorReceiptSha;
            string predecessorPostDigest;
            Require(ValidateSeedReceipt(result, phaseIndex == 0, out predecessorReceiptSha,
                    out predecessorPostDigest),
                "WIDTH_STAGE_SEED_RECEIPT_INVALID", 3,
                "dimensions requires the exact live clone_native_seed receipt and stabilized 75-file pack");
            for (int index = 0; index < order.Length; index++)
            {
                string receiptPath = PhaseReceiptPath(result.attempt_directory, order[index]);
                if (index < phaseIndex)
                {
                    string receiptPreDigest;
                    string receiptPostDigest;
                    Require(ValidatePhaseReceipt(receiptPath, order[index], result,
                            predecessorReceiptSha, predecessorPostDigest,
                            out receiptPreDigest, out receiptPostDigest),
                        "WIDTH_STAGE_PREDECESSOR_RECEIPT_INVALID", 3,
                        "missing, tampered, or unchained predecessor receipt for " + order[index]);
                    predecessorReceiptSha = Sha256(receiptPath);
                    predecessorPostDigest = receiptPostDigest;
                }
                else
                    Require(!File.Exists(receiptPath), "WIDTH_STAGE_SEQUENCE_ALREADY_ADVANCED", 3,
                        "receipt already exists for current or later phase " + order[index]);
            }
            Require(string.Equals(predecessorPostDigest,
                    result.initial_inventory_digest, StringComparison.OrdinalIgnoreCase),
                "WIDTH_STAGE_PREDECESSOR_POST_TO_CURRENT_PRE_MISMATCH", 3,
                "only the immediate predecessor post-inventory may bind the current phase pre-inventory");
            result.predecessor_receipt_sha256 = predecessorReceiptSha;
            result.phase_sequence_gate = true;
        }

        private static string PhaseReceiptPath(string attemptDir, string phase)
        {
            return Path.Combine(attemptDir, "evidence", "width-" + phase + ".receipt.json");
        }

        private static bool ValidateSeedReceipt(Result result, bool requireLiveSeedState,
            out string receiptSha, out string targetInventoryDigest)
        {
            receiptSha = "";
            targetInventoryDigest = "";
            try
            {
                string receiptPath = Path.Combine(result.attempt_directory, "receipts",
                    "clone_native_seed.json");
                string evidencePath = Path.Combine(result.attempt_directory, "evidence",
                    "seed_pack_888_native_v1.json");
                if (!File.Exists(receiptPath) || HasReparsePoint(Path.GetDirectoryName(receiptPath)) ||
                    HasReparsePoint(receiptPath) || FileLinkCount(receiptPath) != 1 ||
                    !File.Exists(evidencePath) || HasReparsePoint(evidencePath) ||
                    FileLinkCount(evidencePath) != 1) return false;
                string receiptHashBefore = Sha256(receiptPath);
                string evidenceHashBefore = Sha256(evidencePath);
                Dictionary<string, object> receipt = ReadJsonObject(receiptPath);
                Dictionary<string, object> evidence = ReadJsonObject(evidencePath);
                if (!ExactKeys(receipt, SeedReceiptKeys)) return false;
                string authorizationSha = TextValue(receipt, "authorizationSha256");
                byte[] authorizationBytes = Convert.FromBase64String(TextValue(receipt,
                    "authorizationJsonBase64"));
                if (!IsSha256(authorizationSha) || !string.Equals(Sha256Bytes(authorizationBytes),
                        authorizationSha, StringComparison.OrdinalIgnoreCase)) return false;
                Dictionary<string, object> historicalAuthorization = new JavaScriptSerializer
                    { MaxJsonLength = int.MaxValue, RecursionLimit = 200 }
                    .DeserializeObject(new UTF8Encoding(false).GetString(authorizationBytes)) as
                    Dictionary<string, object>;
                AuthorizationBinding authorizationBinding;
                bool authorizationGate = AuthorizationContractValidForPhases(historicalAuthorization,
                    result.worker_id, result.task_id, result.task_revision, result.task_digest,
                    result.request_fingerprint, result.request_digest, result.plan_sha256,
                    result.recipe_id, result.recipe_version, result.recipe_digest,
                    "native_seed_pack_888x14_v1", result.seed_tool_source_normalized_sha256,
                    result.seed_tool_executable_sha256, ExpectedSeedInventoryDigest,
                    result.attempt_number, new[] { "clone_native_seed" }, false,
                    TextValue(receipt, "completedAt"), out authorizationBinding);
                string evidenceRelativePath = TextValue(receipt, "evidencePath");
                string liveInventoryDigest = requireLiveSeedState ?
                    InventoryDigest(CadInventory(result.cad_directory)) : "";
                string liveRootSha = requireLiveSeedState ?
                    Sha256(ExactFile(result.cad_directory, SeedRootFileName)) : "";
                bool exactNonRoot = !requireLiveSeedState ||
                    SeedInventoryShaByFile().Where(delegate(KeyValuePair<string, string> row)
                    {
                        return !string.Equals(row.Key, SeedRootFileName,
                            StringComparison.OrdinalIgnoreCase);
                    }).All(delegate(KeyValuePair<string, string> row)
                    {
                        string path = ExactFile(result.cad_directory, row.Key);
                        return File.Exists(path) && string.Equals(Sha256(path), row.Value,
                            StringComparison.OrdinalIgnoreCase);
                    });
                targetInventoryDigest = TextValue(receipt, "targetInventoryDigest");
                string evidenceCommitment = TextValue(receipt, "evidenceCommitmentSha256");
                bool evidenceGate = string.Equals(evidenceRelativePath,
                        "evidence/seed_pack_888_native_v1.json", StringComparison.Ordinal) &&
                    string.Equals(evidenceHashBefore, TextValue(receipt, "evidenceSha256"),
                        StringComparison.OrdinalIgnoreCase) && IsSha256(evidenceCommitment) &&
                    string.Equals(TextValue(evidence, "evidence_commitment_sha256"),
                        evidenceCommitment, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(EvidenceCommitmentDigest(evidence), evidenceCommitment,
                        StringComparison.OrdinalIgnoreCase) &&
                    BoolValue(evidence, "success") &&
                    string.Equals(TextValue(evidence, "task_id"), result.task_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "plan_sha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "authorization_id"),
                        authorizationBinding.authorization_id, StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "authorization_sha256"), authorizationSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "target_inventory_digest"),
                        targetInventoryDigest, StringComparison.OrdinalIgnoreCase);
                bool exactGate = authorizationGate && evidenceGate &&
                    string.Equals(TextValue(receipt, "schema"), ExpectedSeedReceiptSchema,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "phase"), "clone_native_seed",
                        StringComparison.Ordinal) && BoolValue(receipt, "success") &&
                    string.Equals(TextValue(receipt, "taskId"), result.task_id,
                        StringComparison.Ordinal) &&
                    ExactNumber(receipt, "taskRevision", result.task_revision) &&
                    string.Equals(TextValue(receipt, "taskDigest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "requestDigest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "leaseId"), authorizationBinding.lease_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "leaseExpiresAt"),
                        authorizationBinding.lease_expires_at_utc, StringComparison.Ordinal) &&
                    ExactNumber(receipt, "attempt", result.attempt_number) &&
                    string.Equals(TextValue(receipt, "planSha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "authorizationId"),
                        authorizationBinding.authorization_id, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationIssuedAt"),
                        authorizationBinding.issued_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationExpiresAt"),
                        authorizationBinding.expires_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "recipeId"), result.recipe_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "recipeDigest"), result.recipe_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "toolId"), "native_seed_pack_888x14_v1",
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "toolSourceNormalizedSha256"),
                        result.seed_tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "toolExecutableSha256"),
                        result.seed_tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "sourceInventoryDigest"),
                        ExpectedSeedInventoryDigest, StringComparison.OrdinalIgnoreCase) &&
                    IsSha256(targetInventoryDigest) &&
                    ExactNumber(receipt, "nonRootFileCount", 74.0) &&
                    BoolValue(receipt, "nonRootExactSource") &&
                    string.Equals(TextValue(receipt, "rootFileName"), SeedRootFileName,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "rootShaBefore"), ExpectedSeedRootSha256,
                        StringComparison.OrdinalIgnoreCase) && IsSha256(TextValue(receipt,
                        "rootShaAfterStable")) && SeedLiveBindingValid(receipt, requireLiveSeedState,
                        liveInventoryDigest, liveRootSha, exactNonRoot) &&
                    ExactNumber(receipt, "initialOpenErrors", 0.0) &&
                    (ExactNumber(receipt, "initialOpenWarnings", 32.0) ||
                        ExactNumber(receipt, "initialOpenWarnings", 96.0)) &&
                    (((int)NumberValue(receipt, "initialOpenWarnings")) & 1) == 0 &&
                    ExactNumber(receipt, "stabilizeSaveErrors", 0.0) &&
                    ExactNumber(receipt, "stabilizeSaveWarnings", 0.0) &&
                    ExactNumber(receipt, "reopenErrors", 0.0) &&
                    (ExactNumber(receipt, "reopenWarnings", 32.0) ||
                        ExactNumber(receipt, "reopenWarnings", 96.0)) &&
                    (((int)NumberValue(receipt, "reopenWarnings")) & 1) == 0 &&
                    ExactNumber(receipt, "dependencyClosureCount", 75.0) &&
                    BoolValue(receipt, "dependenciesAllTargetLocal") &&
                    BoolValue(receipt, "knownRootIssueGate") &&
                    string.Equals(TextValue(receipt, "predecessorReceiptSha256"), "",
                        StringComparison.Ordinal) &&
                    string.Equals(Sha256(receiptPath), receiptHashBefore,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Sha256(evidencePath), evidenceHashBefore,
                        StringComparison.OrdinalIgnoreCase);
                if (!exactGate) return false;
                receiptSha = receiptHashBefore;
                result.seed_receipt_path = receiptPath;
                result.seed_receipt_sha256 = receiptSha;
                result.seed_target_inventory_digest = targetInventoryDigest;
                result.seed_root_sha_after_stable = liveRootSha;
                result.seed_receipt_gate = true;
                return true;
            }
            catch { return false; }
        }

        private static bool SeedLiveBindingValid(Dictionary<string, object> receipt,
            bool requireLiveSeedState, string liveInventoryDigest, string liveRootSha,
            bool exactNonRoot)
        {
            return !requireLiveSeedState || exactNonRoot &&
                string.Equals(TextValue(receipt, "targetInventoryDigest"), liveInventoryDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "rootShaAfterStable"), liveRootSha,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool ValidatePhaseReceipt(string receiptPath, string expectedPhase, Result result,
            string expectedPredecessorReceiptSha, string expectedPreInventoryDigest,
            out string receiptPreInventoryDigest, out string receiptPostInventoryDigest)
        {
            receiptPreInventoryDigest = "";
            receiptPostInventoryDigest = "";
            try
            {
                if (!File.Exists(receiptPath) || HasReparsePoint(receiptPath) || FileLinkCount(receiptPath) != 1)
                    return false;
                string receiptHashBefore = Sha256(receiptPath);
                Dictionary<string, object> receipt = ReadJsonObject(receiptPath);
                string evidencePath = Path.Combine(result.attempt_directory, "evidence",
                    "width-" + expectedPhase + ".json");
                if (!File.Exists(evidencePath) || HasReparsePoint(evidencePath) ||
                    FileLinkCount(evidencePath) != 1) return false;
                string evidenceHashBefore = Sha256(evidencePath);
                Dictionary<string, object> evidence = ReadJsonObject(evidencePath);
                receiptPreInventoryDigest = TextValue(receipt, "preInventoryDigest");
                receiptPostInventoryDigest = TextValue(receipt, "postInventoryDigest");
                string receiptAuthorizationSha = TextValue(receipt, "authorizationSha256");
                string receiptAuthorizationId = TextValue(receipt, "authorizationId");
                byte[] authorizationBytes = Convert.FromBase64String(TextValue(receipt,
                    "authorizationJsonBase64"));
                string authorizationBytesSha = Sha256Bytes(authorizationBytes);
                object authorizationValue = new JavaScriptSerializer().DeserializeObject(
                    new UTF8Encoding(false).GetString(authorizationBytes));
                Dictionary<string, object> historicalAuthorization = authorizationValue as
                    Dictionary<string, object>;
                AuthorizationBinding historicalBinding;
                bool historicalAuthorizationGate = AuthorizationContractValid(historicalAuthorization,
                    result.worker_id, result.task_id, result.task_revision, result.task_digest,
                    result.request_fingerprint, result.request_digest, result.plan_sha256, result.recipe_id,
                    result.recipe_version, result.recipe_digest, ExpectedToolId, ExpectedSourceSha256,
                    result.tool_executable_sha256, receiptPreInventoryDigest, result.attempt_number,
                    false, TextValue(receipt, "completedAt"), out historicalBinding);
                return ExactKeys(receipt, PhaseReceiptKeys) && historicalAuthorizationGate &&
                    string.Equals(TextValue(receipt, "schema"),
                        "winnsen.native_width_888.phase_receipt.v1", StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "phase"), expectedPhase, StringComparison.Ordinal) &&
                    BoolValue(receipt, "success") &&
                    string.Equals(TextValue(receipt, "taskId"), result.task_id, StringComparison.Ordinal) &&
                    ExactNumber(receipt, "taskRevision", result.task_revision) &&
                    string.Equals(TextValue(receipt, "taskDigest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "requestDigest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "leaseId"), historicalBinding.lease_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "leaseExpiresAt"),
                        historicalBinding.lease_expires_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationIssuedAt"),
                        historicalBinding.issued_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationExpiresAt"),
                        historicalBinding.expires_at_utc, StringComparison.Ordinal) &&
                    ExactNumber(receipt, "attempt", result.attempt_number) &&
                    string.Equals(TextValue(receipt, "planSha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsSha256(receiptAuthorizationSha) &&
                    string.Equals(receiptAuthorizationSha, authorizationBytesSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(receiptAuthorizationId, historicalBinding.authorization_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "recipeId"), result.recipe_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "recipeDigest"), result.recipe_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "toolId"), ExpectedToolId, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "toolSourceNormalizedSha256"), ExpectedSourceSha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "toolExecutableSha256"), result.tool_executable_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "evidencePath"),
                        "evidence/width-" + expectedPhase + ".json", StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "evidenceSha256"), evidenceHashBefore,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "evidenceCommitmentSha256"),
                        EvidenceCommitmentDigest(evidence),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "predecessorReceiptSha256"),
                        expectedPredecessorReceiptSha, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(receiptPreInventoryDigest, expectedPreInventoryDigest,
                        StringComparison.OrdinalIgnoreCase) && IsSha256(receiptPostInventoryDigest) &&
                    BoolValue(evidence, "success") && BoolValue(evidence, "functional_success") &&
                    BoolValue(evidence, "evidence_write_succeeded") &&
                    BoolValue(evidence, "evidence_finalize_write_succeeded") &&
                    string.Equals(TextValue(evidence, "phase"), expectedPhase, StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "task_id"), result.task_id, StringComparison.Ordinal) &&
                    Near(NumberValue(evidence, "task_revision"), result.task_revision, 0.0) &&
                    string.Equals(TextValue(evidence, "task_digest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "request_digest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "lease_id"), historicalBinding.lease_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "lease_expires_at_utc"),
                        historicalBinding.lease_expires_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "authorization_id"), receiptAuthorizationId,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "authorization_issued_at_utc"),
                        historicalBinding.issued_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "authorization_expires_at_utc"),
                        historicalBinding.expires_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "completed_at_utc"),
                        TextValue(receipt, "completedAt"), StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "plan_sha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "recipe_id"), result.recipe_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(evidence, "recipe_digest"), result.recipe_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "authorization_sha256"), receiptAuthorizationSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "tool_source_normalized_sha256"),
                        ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "tool_executable_sha256"),
                        result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "initial_inventory_digest"),
                        receiptPreInventoryDigest, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "post_inventory_digest"),
                        receiptPostInventoryDigest, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(evidence, "evidence_commitment_sha256"),
                        TextValue(receipt, "evidenceCommitmentSha256"),
                        StringComparison.OrdinalIgnoreCase) &&
                    !evidence.ContainsKey("phase_receipt_path") &&
                    !evidence.ContainsKey("phase_receipt_sha256") &&
                    !evidence.ContainsKey("phase_receipt_committed") &&
                    string.Equals(Sha256(receiptPath), receiptHashBefore,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Sha256(evidencePath), evidenceHashBefore,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static PreparedReceipt PreparePhaseReceipt(Result result, string evidenceSha256)
        {
            string phase = result.phase;
            string[] order = PhaseOrder();
            int index = Array.IndexOf(order, phase);
            string predecessorSha = index == 0 ? result.seed_receipt_sha256 :
                Sha256(PhaseReceiptPath(result.attempt_directory, order[index - 1]));
            Require(string.Equals(predecessorSha, result.predecessor_receipt_sha256,
                    StringComparison.OrdinalIgnoreCase),
                "PHASE_PREDECESSOR_RECEIPT_CHANGED_DURING_EXECUTION", 91,
                "the immediate predecessor receipt changed after the phase began");
            Require(string.Equals(result.evidence_commitment_sha256,
                    EvidenceCommitmentDigest(result), StringComparison.OrdinalIgnoreCase),
                "EVIDENCE_COMMITMENT_CHANGED_BEFORE_RECEIPT", 91,
                "covered evidence fields changed after final evidence bytes were committed");
            var receipt = new Dictionary<string, object>
            {
                { "schema", "winnsen.native_width_888.phase_receipt.v1" },
                { "phase", phase }, { "success", true },
                { "completedAt", result.completed_at_utc },
                { "taskId", result.task_id }, { "taskRevision", result.task_revision },
                { "taskDigest", result.task_digest }, { "requestDigest", result.request_digest },
                { "leaseId", result.lease_id }, { "attempt", result.attempt_number },
                { "leaseExpiresAt", result.lease_expires_at_utc },
                { "authorizationId", result.authorization_id },
                { "authorizationExpiresAt", result.authorization_expires_at_utc },
                { "authorizationIssuedAt", result.authorization_issued_at_utc },
                { "planSha256", result.plan_sha256 },
                { "authorizationSha256", result.authorization_sha256 },
                { "authorizationJsonBase64", result.authorization_json_base64 },
                { "recipeId", result.recipe_id }, { "recipeDigest", result.recipe_digest },
                { "toolId", ExpectedToolId },
                { "toolSourceNormalizedSha256", ExpectedSourceSha256 },
                { "toolExecutableSha256", result.tool_executable_sha256 },
                { "evidencePath", "evidence/width-" + phase + ".json" },
                { "evidenceSha256", evidenceSha256 },
                { "evidenceCommitmentSha256", result.evidence_commitment_sha256 },
                { "preInventoryDigest", result.initial_inventory_digest },
                { "postInventoryDigest", result.post_inventory_digest },
                { "predecessorReceiptSha256", predecessorSha }
            };
            string path = PhaseReceiptPath(result.attempt_directory, phase);
            Require(ExactKeys(receipt, PhaseReceiptKeys), "PHASE_RECEIPT_SCHEMA_DRIFT", 91,
                "prepared phase receipt does not have the exact v1 keys");
            byte[] bytes = SerializeJsonLine(receipt);
            string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            var prepared = new PreparedReceipt
            {
                path = path,
                temporary_path = temporary,
                sha256 = Sha256(temporary),
                predecessor_sha256 = predecessorSha
            };
            return prepared;
        }

        private static void CommitPreparedReceipt(PreparedReceipt prepared)
        {
            Require(prepared != null && File.Exists(prepared.temporary_path) &&
                !File.Exists(prepared.path) && !HasReparsePoint(prepared.temporary_path) &&
                FileLinkCount(prepared.temporary_path) == 1 &&
                string.Equals(Sha256(prepared.temporary_path), prepared.sha256,
                    StringComparison.OrdinalIgnoreCase), "PHASE_RECEIPT_PENDING_INVALID", 91,
                "pending phase receipt changed or its final path already exists");
            File.Move(prepared.temporary_path, prepared.path);
            Require(File.Exists(prepared.path) && !HasReparsePoint(prepared.path) &&
                FileLinkCount(prepared.path) == 1 && string.Equals(Sha256(prepared.path),
                    prepared.sha256, StringComparison.OrdinalIgnoreCase),
                "PHASE_RECEIPT_ATOMIC_COMMIT_FAILED", 91,
                "phase receipt was not atomically committed as a single-link regular file");
        }

        private static void DeletePreparedReceiptBestEffort(PreparedReceipt prepared)
        {
            if (prepared == null || string.IsNullOrWhiteSpace(prepared.temporary_path)) return;
            try { if (File.Exists(prepared.temporary_path)) File.Delete(prepared.temporary_path); }
            catch { }
        }

        private static string EvidenceCommitmentDigest(Result result)
        {
            return EvidenceCommitmentDigest(CanonicalWidthEvidenceDocument(result));
        }

        private static string EvidenceCommitmentDigest(Dictionary<string, object> row)
        {
            if (row == null) return "";
            var projection = new Dictionary<string, object>(row, StringComparer.Ordinal);
            foreach (string key in EvidenceCommitmentExcludedKeys) projection.Remove(key);
            if (string.Equals(TextValue(row, "schema"), ExpectedEvidenceSchema,
                    StringComparison.Ordinal))
                projection = (Dictionary<string, object>)CanonicalWidthEvidenceValue(projection);
            return Sha256Text(StableJson(projection));
        }

        private static Dictionary<string, object> CanonicalWidthEvidenceDocument(Result result)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 200 };
            Dictionary<string, object> row = serializer.DeserializeObject(serializer.Serialize(result)) as
                Dictionary<string, object>;
            Require(row != null && string.Equals(TextValue(row, "schema"), ExpectedEvidenceSchema,
                    StringComparison.Ordinal),
                "EVIDENCE_CANONICALIZATION_FAILED", 91,
                "width evidence could not be projected to its exact schema");
            return (Dictionary<string, object>)CanonicalWidthEvidenceValue(row);
        }

        private static object CanonicalWidthEvidenceValue(object value)
        {
            if (value == null || value is string || value is bool || value is byte ||
                value is sbyte || value is short || value is ushort || value is int ||
                value is uint) return value;
            if (value is long)
            {
                long number = (long)value;
                return number > 9007199254740991L || number < -9007199254740991L
                    ? (object)number.ToString(CultureInfo.InvariantCulture) : number;
            }
            if (value is ulong)
            {
                ulong number = (ulong)value;
                return number > 9007199254740991UL
                    ? (object)number.ToString(CultureInfo.InvariantCulture) : number;
            }
            if (value is float || value is double || value is decimal)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number)) return null;
                if (Math.Abs(number) < 1e-12) return 0m;
                string text = number.ToString("G15", CultureInfo.InvariantCulture);
                decimal canonical;
                return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out canonical) ? (object)canonical : number;
            }
            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                var output = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> row in dictionary)
                    output[row.Key] = CanonicalWidthEvidenceValue(row.Value);
                return output;
            }
            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                var output = new List<object>();
                foreach (object row in enumerable) output.Add(CanonicalWidthEvidenceValue(row));
                return output;
            }
            throw new InvalidDataException("unsupported width evidence value type: " +
                value.GetType().FullName);
        }

        private static bool JsonEvidenceCommitted(string path, Result result)
        {
            try
            {
                Dictionary<string, object> row = ReadJsonObject(path);
                return BoolValue(row, "success") && BoolValue(row, "functional_success") &&
                    BoolValue(row, "evidence_write_succeeded") &&
                    BoolValue(row, "evidence_finalize_write_succeeded") &&
                    string.Equals(TextValue(row, "phase"), result.phase, StringComparison.Ordinal) &&
                    string.Equals(TextValue(row, "task_id"), result.task_id, StringComparison.Ordinal) &&
                    Near(NumberValue(row, "task_revision"), result.task_revision, 0.0) &&
                    string.Equals(TextValue(row, "task_digest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "request_digest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "lease_id"), result.lease_id, StringComparison.Ordinal) &&
                    CompletionTimeWithinRecordedExpiries(TextValue(row, "completed_at_utc"), result) &&
                    string.Equals(TextValue(row, "plan_sha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "authorization_sha256"), result.authorization_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "authorization_id"), result.authorization_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(row, "tool_source_normalized_sha256"),
                        ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "tool_executable_sha256"),
                        result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "initial_inventory_digest"),
                        result.initial_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(row, "post_inventory_digest"),
                        result.post_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                    !row.ContainsKey("phase_receipt_path") &&
                    !row.ContainsKey("phase_receipt_sha256") &&
                    !row.ContainsKey("phase_receipt_committed") &&
                    string.Equals(TextValue(row, "evidence_commitment_sha256"),
                        EvidenceCommitmentDigest(row), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool CompletionTimeWithinRecordedExpiries(string completedAtText, Result result)
        {
            DateTime completedAt;
            DateTime authorizationIssued;
            DateTime authorizationExpires;
            DateTime leaseExpires;
            return result != null &&
                DateTime.TryParse(completedAtText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out completedAt) &&
                DateTime.TryParse(result.lease_expires_at_utc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out leaseExpires) &&
                DateTime.TryParse(result.authorization_issued_at_utc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out authorizationIssued) && completedAt >= authorizationIssued &&
                DateTime.TryParse(result.authorization_expires_at_utc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out authorizationExpires) && completedAt <= authorizationExpires && completedAt <= leaseExpires;
        }

        private static Dictionary<string, object> CloneJsonObject(Dictionary<string, object> value)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
            return serializer.DeserializeObject(serializer.Serialize(value)) as Dictionary<string, object>;
        }

        private static Dictionary<string, object> BuildAuthorizationFixture(DateTime now,
            string workerId, string taskId, int taskRevision, string taskDigest,
            string requestFingerprint, string requestDigest, string planSha, string executableSha,
            int attempt)
        {
            string leaseId = "lease-width-888-self-test";
            string issuedAt = now.AddMinutes(-1).ToString("o", CultureInfo.InvariantCulture);
            string expiresAt = now.AddMinutes(20).ToString("o", CultureInfo.InvariantCulture);
            string leaseExpiresAt = now.AddMinutes(25).ToString("o", CultureInfo.InvariantCulture);
            Dictionary<string, object> bindings = ExpectedAuthorizationBindings(workerId, taskId,
                taskRevision, taskDigest, leaseId, leaseExpiresAt, requestFingerprint, requestDigest,
                planSha, ExpectedRecipeId, 1, ExpectedRecipeDigest, ExpectedToolId,
                ExpectedSourceSha256, executableSha, ExpectedSeedInventoryDigest, attempt);
            var identity = new Dictionary<string, object>(bindings, StringComparer.Ordinal)
            {
                { "issuedAt", issuedAt }, { "expiresAt", expiresAt }
            };
            string authorizationId = "native-auth-" + Sha256Text(StableJson(identity)).Substring(0, 32)
                .ToLowerInvariant();
            return new Dictionary<string, object>
            {
                { "schema", ExpectedAuthorizationSchema }, { "authorizationId", authorizationId },
                { "issuedAt", issuedAt }, { "expiresAt", expiresAt }, { "purpose", ExpectedPurpose },
                { "workerId", bindings["workerId"] }, { "task", bindings["task"] },
                { "request", bindings["request"] }, { "plan", bindings["plan"] },
                { "recipe", bindings["recipe"] }, { "tool", bindings["tool"] },
                { "seed", bindings["seed"] }, { "execution", bindings["execution"] },
                { "qualityBoundary", new Dictionary<string, object>
                    {
                        { "engineeringAssistanceReady", false },
                        { "readyOnlyAfterEveryRequiredCheckPasses", true }
                    }
                }
            };
        }

        private static void RecomputeAuthorizationFixtureId(Dictionary<string, object> authorization)
        {
            var identity = new Dictionary<string, object>
            {
                { "workerId", authorization["workerId"] },
                { "task", ChildObject(authorization, "task") },
                { "request", ChildObject(authorization, "request") },
                { "plan", ChildObject(authorization, "plan") },
                { "recipe", ChildObject(authorization, "recipe") },
                { "tool", ChildObject(authorization, "tool") },
                { "seed", ChildObject(authorization, "seed") },
                { "execution", ChildObject(authorization, "execution") },
                { "issuedAt", TextValue(authorization, "issuedAt") },
                { "expiresAt", TextValue(authorization, "expiresAt") }
            };
            authorization["authorizationId"] = "native-auth-" +
                Sha256Text(StableJson(identity)).Substring(0, 32).ToLowerInvariant();
        }

        private static bool AuthorizationFixtureValid(Dictionary<string, object> authorization,
            string workerId, string taskId, int taskRevision, string taskDigest,
            string requestFingerprint, string requestDigest, string planSha, string executableSha,
            int attempt, string completedAt)
        {
            AuthorizationBinding binding;
            return AuthorizationContractValid(authorization, workerId, taskId, taskRevision, taskDigest,
                requestFingerprint, requestDigest, planSha, ExpectedRecipeId, 1, ExpectedRecipeDigest,
                ExpectedToolId, ExpectedSourceSha256, executableSha, ExpectedSeedInventoryDigest,
                attempt, true, completedAt, out binding);
        }

        private static string RunStaticSelfTests(string testRoot)
        {
            var report = new StaticSelfTestReport();
            string root = Path.GetFullPath(testRoot);
            Directory.CreateDirectory(root);
            DateTime now = DateTime.UtcNow;
            const string workerId = "native-width-888-self-test-worker";
            const string taskId = "NATIVE-88814-WIDTH-SELF-TEST";
            const int taskRevision = 9;
            string taskDigest = new string('A', 64);
            string requestFingerprint = new string('B', 64);
            string requestDigest = new string('C', 64);
            string planSha = new string('D', 64);
            string executableSha = Sha256(System.Reflection.Assembly.GetExecutingAssembly().Location);
            const int attempt = 1;
            Dictionary<string, object> authorization = BuildAuthorizationFixture(now, workerId, taskId,
                taskRevision, taskDigest, requestFingerprint, requestDigest, planSha, executableSha, attempt);
            AddStaticSelfTest(report, "accepts_exact_shared_authorization_contract",
                AuthorizationFixtureValid(authorization, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt,
                    now.ToString("o", CultureInfo.InvariantCulture)), "exact fixture was rejected");

            Dictionary<string, object> extraTop = CloneJsonObject(authorization);
            extraTop["unexpected"] = true;
            AddStaticSelfTest(report, "rejects_authorization_extra_top_level_key",
                !AuthorizationFixtureValid(extraTop, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization with an extra top-level key was accepted");
            Dictionary<string, object> wrongQuality = CloneJsonObject(authorization);
            ChildObject(wrongQuality, "qualityBoundary")["engineeringAssistanceReady"] = true;
            AddStaticSelfTest(report, "rejects_wrong_quality_boundary",
                !AuthorizationFixtureValid(wrongQuality, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization with a permissive quality boundary was accepted");
            Dictionary<string, object> wrongIdentity = CloneJsonObject(authorization);
            wrongIdentity["authorizationId"] = "native-auth-00000000000000000000000000000000";
            AddStaticSelfTest(report, "rejects_non_deterministic_authorization_id",
                !AuthorizationFixtureValid(wrongIdentity, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization with a forged identity was accepted");
            Dictionary<string, object> extraTaskKey = CloneJsonObject(authorization);
            ChildObject(extraTaskKey, "task")["unexpected"] = true;
            RecomputeAuthorizationFixtureId(extraTaskKey);
            AddStaticSelfTest(report, "rejects_authorization_extra_nested_key",
                !AuthorizationFixtureValid(extraTaskKey, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization with an extra nested task key was accepted");
            Dictionary<string, object> wrongPurpose = CloneJsonObject(authorization);
            wrongPurpose["purpose"] = "model_release";
            AddStaticSelfTest(report, "rejects_wrong_authorization_purpose",
                !AuthorizationFixtureValid(wrongPurpose, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization with the wrong purpose was accepted");
            Dictionary<string, object> wrongPhases = CloneJsonObject(authorization);
            ChildObject(wrongPhases, "execution")["phases"] = new object[] { "dimensions" };
            RecomputeAuthorizationFixtureId(wrongPhases);
            AddStaticSelfTest(report, "rejects_wrong_tool_phase_contract",
                !AuthorizationFixtureValid(wrongPhases, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization with a partial width phase list was accepted");
            Dictionary<string, object> wrongExecutable = CloneJsonObject(authorization);
            ChildObject(wrongExecutable, "tool")["executableSha256"] = new string('8', 64);
            RecomputeAuthorizationFixtureId(wrongExecutable);
            AddStaticSelfTest(report, "rejects_wrong_tool_executable_binding",
                !AuthorizationFixtureValid(wrongExecutable, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization bound to a different executable was accepted");
            Dictionary<string, object> overlongAuthorization = CloneJsonObject(authorization);
            ChildObject(overlongAuthorization, "task")["leaseExpiresAt"] = now.AddMinutes(40)
                .ToString("o", CultureInfo.InvariantCulture);
            overlongAuthorization["expiresAt"] = now.AddMinutes(31)
                .ToString("o", CultureInfo.InvariantCulture);
            RecomputeAuthorizationFixtureId(overlongAuthorization);
            AddStaticSelfTest(report, "rejects_authorization_over_30_minutes",
                !AuthorizationFixtureValid(overlongAuthorization, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization longer than 30 minutes was accepted");
            Dictionary<string, object> expiresAfterLease = CloneJsonObject(authorization);
            expiresAfterLease["expiresAt"] = now.AddMinutes(26)
                .ToString("o", CultureInfo.InvariantCulture);
            RecomputeAuthorizationFixtureId(expiresAfterLease);
            AddStaticSelfTest(report, "rejects_authorization_expiring_after_lease",
                !AuthorizationFixtureValid(expiresAfterLease, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt, ""),
                "authorization that outlives its lease was accepted");
            AddStaticSelfTest(report, "rejects_completion_after_authorization_expiry",
                !AuthorizationFixtureValid(authorization, workerId, taskId, taskRevision, taskDigest,
                    requestFingerprint, requestDigest, planSha, executableSha, attempt,
                    now.AddMinutes(21).ToString("o", CultureInfo.InvariantCulture)),
                "completion after authorization expiry was accepted");

            string manifestRoot = Path.Combine(root, "manifest-root");
            string manifestToolDir = Path.Combine(manifestRoot, "tool");
            Directory.CreateDirectory(manifestToolDir);
            string manifestSource = Path.Combine(manifestToolDir, "source.cs");
            string manifestExecutable = Path.Combine(manifestToolDir, "tool.exe");
            string manifestVerifier = Path.Combine(manifestToolDir, "verify.ps1");
            string manifestPath = Path.Combine(manifestToolDir, "toolchain_manifest.json");
            const string stampedFixtureSource =
                "private const string ExpectedSourceSha256 = \"" +
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\";\r\n" +
                "private const string ExpectedContractSnapshotSha256 = \"" +
                "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\";\r\n";
            File.WriteAllText(manifestSource, stampedFixtureSource, new UTF8Encoding(false));
            File.WriteAllText(manifestExecutable, "compiled", new UTF8Encoding(false));
            File.WriteAllText(manifestVerifier, "verified", new UTF8Encoding(false));
            const string normalizedFixtureSource =
                "private const string ExpectedSourceSha256 = \"__SOURCE_SHA256__\";\n" +
                "private const string ExpectedContractSnapshotSha256 = \"__CONTRACT_SHA256__\";\n";
            AddStaticSelfTest(report, "normalizes_source_and_contract_snapshot_stamps",
                string.Equals(NormalizedToolSourceSha256(manifestSource),
                    Sha256Text(normalizedFixtureSource), StringComparison.OrdinalIgnoreCase),
                "normalized source identity did not mask both embedded source and contract snapshot hashes");
            var manifestFixture = new Dictionary<string, object>
            {
                { "schema", ExpectedToolchainManifestSchema }, { "generatedBy", "tool/verify.ps1" },
                { "tool", new Dictionary<string, object>
                    {
                        { "id", "fixture_tool_v1" }, { "sourcePath", "tool/source.cs" },
                        { "sourceNormalizedSha256", NormalizedToolSourceSha256(manifestSource) },
                        { "executablePath", "tool/tool.exe" },
                        { "executableSha256", Sha256(manifestExecutable) },
                        { "verifierPath", "tool/verify.ps1" },
                        { "verifierSha256", Sha256(manifestVerifier) }
                    }
                }
            };
            WriteJsonAtomic(manifestPath, manifestFixture);
            string manifestSha = Sha256(manifestPath);
            ToolchainManifestBinding manifestBinding;
            AddStaticSelfTest(report, "accepts_live_exact_toolchain_manifest",
                ValidateToolchainManifest(manifestRoot, "tool/toolchain_manifest.json", "fixture_tool_v1",
                    "tool/source.cs", "tool/tool.exe", "tool/verify.ps1", manifestSha,
                    out manifestBinding), "exact live toolchain manifest was rejected");
            File.WriteAllText(manifestExecutable, "tampered", new UTF8Encoding(false));
            AddStaticSelfTest(report, "rejects_toolchain_manifest_with_tampered_live_executable",
                !ValidateToolchainManifest(manifestRoot, "tool/toolchain_manifest.json", "fixture_tool_v1",
                    "tool/source.cs", "tool/tool.exe", "tool/verify.ps1", manifestSha,
                    out manifestBinding), "manifest accepted a live executable whose hash changed");
            File.WriteAllText(manifestExecutable, "compiled", new UTF8Encoding(false));
            Dictionary<string, object> manifestExtraKey = CloneJsonObject(manifestFixture);
            manifestExtraKey["unexpected"] = true;
            WriteJsonAtomic(manifestPath, manifestExtraKey);
            string extraManifestSha = Sha256(manifestPath);
            AddStaticSelfTest(report, "rejects_toolchain_manifest_extra_key",
                !ValidateToolchainManifest(manifestRoot, "tool/toolchain_manifest.json", "fixture_tool_v1",
                    "tool/source.cs", "tool/tool.exe", "tool/verify.ps1", extraManifestSha,
                    out manifestBinding), "manifest with an extra top-level key was accepted");
            AddStaticSelfTest(report, "rejects_toolchain_manifest_path_escape",
                !ValidateToolchainManifest(manifestRoot, "../toolchain_manifest.json", "fixture_tool_v1",
                    "tool/source.cs", "tool/tool.exe", "tool/verify.ps1", extraManifestSha,
                    out manifestBinding), "manifest path traversal escaped the repository root");

            string attemptDir = Path.Combine(root, "receipt-attempt");
            string evidenceDir = Path.Combine(attemptDir, "evidence");
            Directory.CreateDirectory(evidenceDir);
            var result = new Result
            {
                attempt_directory = attemptDir,
                worker_id = workerId,
                task_id = taskId,
                task_revision = taskRevision,
                task_digest = taskDigest,
                request_fingerprint = requestFingerprint,
                request_digest = requestDigest,
                plan_sha256 = planSha,
                recipe_id = ExpectedRecipeId,
                recipe_version = 1,
                recipe_digest = ExpectedRecipeDigest,
                tool_executable_sha256 = executableSha,
                attempt_number = attempt,
                phase = "dimensions",
                initial_inventory_digest = ExpectedSeedInventoryDigest,
                post_inventory_digest = new string('F', 64)
            };
            Dictionary<string, object> fixtureTask = ChildObject(authorization, "task");
            result.authorization_id = TextValue(authorization, "authorizationId");
            result.authorization_issued_at_utc = TextValue(authorization, "issuedAt");
            result.authorization_expires_at_utc = TextValue(authorization, "expiresAt");
            result.lease_id = TextValue(fixtureTask, "leaseId");
            result.lease_expires_at_utc = TextValue(fixtureTask, "leaseExpiresAt");
            byte[] authorizationBytes = SerializeJsonLine(authorization);
            result.authorization_json_base64 = Convert.ToBase64String(authorizationBytes);
            result.authorization_sha256 = Sha256Bytes(authorizationBytes);
            result.completed_at_utc = now.ToString("o", CultureInfo.InvariantCulture);

            const string seedSourceSha =
                "3333333333333333333333333333333333333333333333333333333333333333";
            const string seedExecutableSha =
                "4444444444444444444444444444444444444444444444444444444444444444";
            Dictionary<string, object> seedAuthorization = CloneJsonObject(authorization);
            Dictionary<string, object> seedTool = ChildObject(seedAuthorization, "tool");
            seedTool["id"] = "native_seed_pack_888x14_v1";
            seedTool["sourceNormalizedSha256"] = seedSourceSha;
            seedTool["executableSha256"] = seedExecutableSha;
            ChildObject(seedAuthorization, "execution")["phases"] =
                new object[] { "clone_native_seed" };
            RecomputeAuthorizationFixtureId(seedAuthorization);
            byte[] seedAuthorizationBytes = SerializeJsonLine(seedAuthorization);
            string seedAuthorizationSha = Sha256Bytes(seedAuthorizationBytes);
            string seedAuthorizationBase64 = Convert.ToBase64String(seedAuthorizationBytes);
            result.seed_tool_source_normalized_sha256 = seedSourceSha;
            result.seed_tool_executable_sha256 = seedExecutableSha;
            string seedEvidencePath = Path.Combine(evidenceDir, "seed_pack_888_native_v1.json");
            string seedTargetDigest = new string('6', 64);
            var seedEvidence = new Dictionary<string, object>
            {
                { "success", true }, { "task_id", taskId }, { "plan_sha256", planSha },
                { "authorization_id", TextValue(seedAuthorization, "authorizationId") },
                { "authorization_sha256", seedAuthorizationSha },
                { "target_inventory_digest", seedTargetDigest },
                { "evidence_commitment_sha256", "" }
            };
            string seedEvidenceCommitment = EvidenceCommitmentDigest(seedEvidence);
            seedEvidence["evidence_commitment_sha256"] = seedEvidenceCommitment;
            WriteJsonAtomic(seedEvidencePath, seedEvidence);
            string seedReceiptDir = Path.Combine(attemptDir, "receipts");
            Directory.CreateDirectory(seedReceiptDir);
            string seedReceiptPath = Path.Combine(seedReceiptDir, "clone_native_seed.json");
            var seedReceipt = new Dictionary<string, object>
            {
                { "schema", ExpectedSeedReceiptSchema }, { "phase", "clone_native_seed" },
                { "success", true }, { "completedAt", result.completed_at_utc },
                { "taskId", taskId }, { "taskRevision", taskRevision }, { "taskDigest", taskDigest },
                { "requestDigest", requestDigest }, { "leaseId", result.lease_id },
                { "leaseExpiresAt", result.lease_expires_at_utc }, { "attempt", attempt },
                { "planSha256", planSha },
                { "authorizationId", TextValue(seedAuthorization, "authorizationId") },
                { "authorizationSha256", seedAuthorizationSha },
                { "authorizationJsonBase64", seedAuthorizationBase64 },
                { "authorizationIssuedAt", TextValue(seedAuthorization, "issuedAt") },
                { "authorizationExpiresAt", TextValue(seedAuthorization, "expiresAt") },
                { "recipeId", ExpectedRecipeId }, { "recipeDigest", ExpectedRecipeDigest },
                { "toolId", "native_seed_pack_888x14_v1" },
                { "toolSourceNormalizedSha256", seedSourceSha },
                { "toolExecutableSha256", seedExecutableSha },
                { "evidencePath", "evidence/seed_pack_888_native_v1.json" },
                { "evidenceSha256", Sha256(seedEvidencePath) },
                { "evidenceCommitmentSha256", seedEvidenceCommitment },
                { "sourceInventoryDigest", ExpectedSeedInventoryDigest },
                { "targetInventoryDigest", seedTargetDigest }, { "nonRootFileCount", 74 },
                { "nonRootExactSource", true }, { "rootFileName", SeedRootFileName },
                { "rootShaBefore", ExpectedSeedRootSha256 },
                { "rootShaAfterStable", new string('5', 64) },
                { "initialOpenErrors", 0 }, { "initialOpenWarnings", 96 },
                { "stabilizeSaveErrors", 0 }, { "stabilizeSaveWarnings", 0 },
                { "reopenErrors", 0 }, { "reopenWarnings", 96 },
                { "dependencyClosureCount", 75 }, { "dependenciesAllTargetLocal", true },
                { "knownRootIssueGate", true }, { "predecessorReceiptSha256", "" }
            };
            WriteJsonAtomic(seedReceiptPath, seedReceipt);
            string seedReceiptSha;
            string validatedSeedTargetDigest;
            AddStaticSelfTest(report, "accepts_exact_historical_seed_receipt_fixture",
                ValidateSeedReceipt(result, false, out seedReceiptSha,
                    out validatedSeedTargetDigest) &&
                    string.Equals(validatedSeedTargetDigest, seedTargetDigest,
                        StringComparison.OrdinalIgnoreCase),
                "exact historical seed receipt/evidence/auth fixture was rejected");
            AddStaticSelfTest(report, "accepts_exact_live_seed_root_binding",
                SeedLiveBindingValid(seedReceipt, true, seedTargetDigest,
                    TextValue(seedReceipt, "rootShaAfterStable"), true),
                "exact seed receipt did not bind the supplied live target digest/root hash");
            Dictionary<string, object> wrongLiveRoot = CloneJsonObject(seedReceipt);
            wrongLiveRoot["rootShaAfterStable"] = new string('7', 64);
            AddStaticSelfTest(report, "rejects_seed_receipt_wrong_live_root_binding",
                !SeedLiveBindingValid(wrongLiveRoot, true, seedTargetDigest,
                    TextValue(seedReceipt, "rootShaAfterStable"), true),
                "seed receipt accepted a root SHA different from the live stabilized root");
            Dictionary<string, object> seedReceiptExtra = CloneJsonObject(seedReceipt);
            seedReceiptExtra["unexpected"] = true;
            WriteJsonAtomic(seedReceiptPath, seedReceiptExtra);
            AddStaticSelfTest(report, "rejects_seed_receipt_extra_key",
                !ValidateSeedReceipt(result, false, out seedReceiptSha,
                    out validatedSeedTargetDigest),
                "seed receipt with an extra key was accepted");
            Dictionary<string, object> seedReceiptBadWarning = CloneJsonObject(seedReceipt);
            seedReceiptBadWarning["initialOpenWarnings"] = 97;
            WriteJsonAtomic(seedReceiptPath, seedReceiptBadWarning);
            AddStaticSelfTest(report, "rejects_seed_receipt_warning_with_bit_one",
                !ValidateSeedReceipt(result, false, out seedReceiptSha,
                    out validatedSeedTargetDigest),
                "seed receipt accepted initial warning 97");
            WriteJsonAtomic(seedReceiptPath, seedReceipt);
            Dictionary<string, object> tamperedSeedEvidence = CloneJsonObject(seedEvidence);
            tamperedSeedEvidence["unexpectedCoveredField"] = true;
            WriteJsonAtomic(seedEvidencePath, tamperedSeedEvidence);
            Dictionary<string, object> seedReceiptRehashedEvidence = CloneJsonObject(seedReceipt);
            seedReceiptRehashedEvidence["evidenceSha256"] = Sha256(seedEvidencePath);
            WriteJsonAtomic(seedReceiptPath, seedReceiptRehashedEvidence);
            AddStaticSelfTest(report, "rejects_seed_evidence_rehashed_without_commitment",
                !ValidateSeedReceipt(result, false, out seedReceiptSha,
                    out validatedSeedTargetDigest),
                "seed evidence tampering was accepted after only its byte SHA was updated");
            WriteJsonAtomic(seedEvidencePath, seedEvidence);
            WriteJsonAtomic(seedReceiptPath, seedReceipt);

            string receiptPath = PhaseReceiptPath(attemptDir, "dimensions");
            string evidencePath = Path.Combine(evidenceDir, "width-dimensions.json");
            var evidence = new Dictionary<string, object>
            {
                { "success", true }, { "functional_success", true },
                { "evidence_write_succeeded", true }, { "evidence_finalize_write_succeeded", true },
                { "phase", "dimensions" }, { "task_id", taskId }, { "task_revision", taskRevision },
                { "task_digest", taskDigest }, { "request_digest", requestDigest },
                { "lease_id", result.lease_id }, { "lease_expires_at_utc", result.lease_expires_at_utc },
                { "authorization_id", result.authorization_id },
                { "authorization_issued_at_utc", result.authorization_issued_at_utc },
                { "authorization_expires_at_utc", result.authorization_expires_at_utc },
                { "completed_at_utc", result.completed_at_utc }, { "plan_sha256", planSha },
                { "recipe_id", ExpectedRecipeId }, { "recipe_digest", ExpectedRecipeDigest },
                { "authorization_sha256", result.authorization_sha256 },
                { "tool_source_normalized_sha256", ExpectedSourceSha256 },
                { "tool_executable_sha256", executableSha },
                { "initial_inventory_digest", result.initial_inventory_digest },
                { "post_inventory_digest", result.post_inventory_digest },
                { "evidence_commitment_sha256", "" }
            };
            string evidenceCommitment = EvidenceCommitmentDigest(evidence);
            evidence["evidence_commitment_sha256"] = evidenceCommitment;
            WriteJsonAtomic(evidencePath, evidence);
            string evidenceSha = Sha256(evidencePath);
            var receipt = new Dictionary<string, object>
            {
                { "schema", "winnsen.native_width_888.phase_receipt.v1" },
                { "phase", "dimensions" }, { "success", true }, { "completedAt", result.completed_at_utc },
                { "taskId", taskId }, { "taskRevision", taskRevision }, { "taskDigest", taskDigest },
                { "requestDigest", requestDigest }, { "leaseId", result.lease_id },
                { "leaseExpiresAt", result.lease_expires_at_utc }, { "attempt", attempt },
                { "planSha256", planSha }, { "authorizationId", result.authorization_id },
                { "authorizationSha256", result.authorization_sha256 },
                { "authorizationJsonBase64", result.authorization_json_base64 },
                { "authorizationIssuedAt", result.authorization_issued_at_utc },
                { "authorizationExpiresAt", result.authorization_expires_at_utc },
                { "recipeId", ExpectedRecipeId }, { "recipeDigest", ExpectedRecipeDigest },
                { "toolId", ExpectedToolId }, { "toolSourceNormalizedSha256", ExpectedSourceSha256 },
                { "toolExecutableSha256", executableSha },
                { "evidencePath", "evidence/width-dimensions.json" },
                { "evidenceSha256", evidenceSha },
                { "evidenceCommitmentSha256", evidenceCommitment },
                { "preInventoryDigest", result.initial_inventory_digest },
                { "postInventoryDigest", result.post_inventory_digest },
                { "predecessorReceiptSha256", "" }
            };
            WriteJsonAtomic(receiptPath, receipt);
            string preDigest;
            string postDigest;
            AddStaticSelfTest(report, "accepts_exact_receipt_authorization_snapshot_pair",
                ValidatePhaseReceipt(receiptPath, "dimensions", result, "",
                    result.initial_inventory_digest, out preDigest, out postDigest),
                "exact receipt/evidence pair was rejected");
            AddStaticSelfTest(report, "rejects_receipt_with_wrong_predecessor_binding",
                !ValidatePhaseReceipt(receiptPath, "dimensions", result, new string('7', 64),
                    result.initial_inventory_digest, out preDigest, out postDigest),
                "receipt with the wrong predecessor binding was accepted");

            Dictionary<string, object> extraReceiptKey = CloneJsonObject(receipt);
            extraReceiptKey["unexpected"] = true;
            WriteJsonAtomic(receiptPath, extraReceiptKey);
            AddStaticSelfTest(report, "rejects_receipt_extra_key",
                !ValidatePhaseReceipt(receiptPath, "dimensions", result, "",
                    result.initial_inventory_digest, out preDigest, out postDigest),
                "receipt with an extra key was accepted");

            WriteJsonAtomic(receiptPath, receipt);
            evidence["unexpectedCoveredField"] = "tampered";
            WriteJsonAtomic(evidencePath, evidence);
            AddStaticSelfTest(report, "rejects_evidence_outside_receipt_commitment",
                !ValidatePhaseReceipt(receiptPath, "dimensions", result, "",
                    result.initial_inventory_digest, out preDigest, out postDigest),
                "evidence changed after receipt commitment was accepted");
            evidence.Remove("unexpectedCoveredField");
            WriteJsonAtomic(evidencePath, evidence);

            Dictionary<string, object> forgedReceipt = CloneJsonObject(receipt);
            Dictionary<string, object> forgedEvidence = CloneJsonObject(evidence);
            forgedReceipt["authorizationSha256"] = new string('9', 64);
            forgedEvidence["authorization_sha256"] = new string('9', 64);
            forgedEvidence["evidence_commitment_sha256"] = "";
            string forgedCommitment = EvidenceCommitmentDigest(forgedEvidence);
            forgedEvidence["evidence_commitment_sha256"] = forgedCommitment;
            forgedReceipt["evidenceCommitmentSha256"] = forgedCommitment;
            WriteJsonAtomic(evidencePath, forgedEvidence);
            forgedReceipt["evidenceSha256"] = Sha256(evidencePath);
            WriteJsonAtomic(receiptPath, forgedReceipt);
            AddStaticSelfTest(report, "rejects_rewritten_receipt_with_unbacked_authorization_sha",
                !ValidatePhaseReceipt(receiptPath, "dimensions", result, "",
                    result.initial_inventory_digest, out preDigest, out postDigest),
                "receipt/evidence pair with a forged authorization SHA was accepted");

            string hardlinkDir = Path.Combine(root, "hardlink-rollback");
            Directory.CreateDirectory(hardlinkDir);
            string backup = Path.Combine(root, "approved-backup.bin");
            string target = Path.Combine(hardlinkDir, "target.SLDPRT");
            string outsideLink = Path.Combine(root, "outside-linked.bin");
            File.WriteAllText(backup, "approved", new UTF8Encoding(false));
            File.WriteAllText(target, "untrusted-linked", new UTF8Encoding(false));
            bool hardlinkCreated = CreateHardLink(outsideLink, target, IntPtr.Zero);
            bool hardlinkRestored = false;
            if (hardlinkCreated)
            {
                RestoreRegularFileFromBackup(backup, target, hardlinkDir);
                hardlinkRestored = File.ReadAllText(target, Encoding.UTF8) == "approved" &&
                    File.ReadAllText(outsideLink, Encoding.UTF8) == "untrusted-linked" &&
                    FileLinkCount(target) == 1 && FileLinkCount(outsideLink) == 1;
            }
            AddStaticSelfTest(report, "rollback_unlinks_hardlink_before_restoring_regular_file",
                hardlinkCreated && hardlinkRestored,
                hardlinkCreated ? "rollback overwrote the linked external inode" :
                    "CreateHardLink fixture could not be created");

            report.checksFailed = report.checks.Count(delegate(StaticSelfTestCheck row) { return !row.ok; });
            report.status = report.checksFailed == 0 ? "PASS" : "FAIL";
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(report);
        }

        private static void AddStaticSelfTest(StaticSelfTestReport report, string name, bool ok,
            string failure)
        {
            report.checks.Add(new StaticSelfTestCheck { name = name, ok = ok, detail = ok ? "" : failure });
        }

        private static string RunEvidenceCommitmentVector(string json)
        {
            object parsed = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 }
                .DeserializeObject(json);
            Dictionary<string, object> row = parsed as Dictionary<string, object>;
            if (row == null) throw new InvalidDataException("evidence vector must be an object");
            return EvidenceCommitmentDigest(row);
        }

        private static string RunCanonicalWidthEvidenceVector()
        {
            var row = new Dictionary<string, object>
            {
                { "schema", ExpectedEvidenceSchema },
                { "success", true },
                { "floating_artifact", 381.00000000000011 },
                { "small_measurement", 0.00007139631748296092 },
                { "near_zero_noise", 2.1250362580700002e-17 },
                { "ticks", 639236583144904455L },
                { "nested", new object[] { 179242.4290120627, -0.44400000000000006 } },
                { "evidence_commitment_sha256", "" }
            };
            var canonical = (Dictionary<string, object>)CanonicalWidthEvidenceValue(row);
            canonical["evidence_commitment_sha256"] = EvidenceCommitmentDigest(canonical);
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 200 }.Serialize(canonical);
        }

        private static void WriteFailureEvidenceBestEffort(string path, Result result)
        {
            result.success = false;
            result.functional_success = false;
            result.evidence_write_succeeded = false;
            result.evidence_finalize_write_succeeded = false;
            result.evidence_commitment_sha256 = "";
            SafeUnlinkEvidencePath(path, result);
            WriteJsonAtomicNew(path, result);
        }

        private static void SafeUnlinkEvidencePath(string path, Result result)
        {
            string full = Path.GetFullPath(path);
            string expectedDirectory = Path.Combine(result.attempt_directory, "evidence");
            Require(SamePath(Path.GetDirectoryName(full), expectedDirectory) &&
                Directory.Exists(expectedDirectory) && !HasReparsePoint(expectedDirectory),
                "FAILURE_EVIDENCE_PATH_INVALID", 91,
                "failure evidence must remain a direct child of the non-reparse attempt evidence directory");
            FileAttributes attributes;
            try { attributes = File.GetAttributes(full); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            bool directory = (attributes & FileAttributes.Directory) != 0;
            bool reparse = (attributes & FileAttributes.ReparsePoint) != 0;
            if (directory)
            {
                Require(reparse, "FAILURE_EVIDENCE_REFUSED_REAL_DIRECTORY", 91,
                    "refused to replace a real directory at the evidence file path");
                Directory.Delete(full, false);
            }
            else File.Delete(full);
            Require(!File.Exists(full) && !Directory.Exists(full),
                "FAILURE_EVIDENCE_UNLINK_FAILED", 91,
                "existing evidence directory entry could not be safely unlinked");
        }

        private static void DeleteCurrentPhaseReceiptBestEffort(Result result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.attempt_directory) ||
                Array.IndexOf(PhaseOrder(), result.phase) < 0) return;
            string path = PhaseReceiptPath(result.attempt_directory, result.phase);
            try
            {
                if (File.Exists(path) && !HasReparsePoint(path) &&
                    SamePath(Path.GetDirectoryName(path), Path.Combine(result.attempt_directory, "evidence")))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                result.cleanup_errors.Add("delete uncommitted phase receipt: " + SafeExceptionText(ex));
            }
        }

        private static void DeleteUnpairedSuccessEvidenceBestEffort(string path, Result result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || HasReparsePoint(path) ||
                    FileLinkCount(path) != 1 || !SamePath(Path.GetDirectoryName(path),
                        Path.Combine(result.attempt_directory, "evidence"))) return;
                Dictionary<string, object> row = ReadJsonObject(path);
                if (BoolValue(row, "success")) File.Delete(path);
            }
            catch (Exception ex)
            {
                result.cleanup_errors.Add("delete unpaired success evidence: " + SafeExceptionText(ex));
            }
        }

        private static void ReleasePhaseLock()
        {
            if (phaseLockHandle == null) return;
            try { phaseLockHandle.Dispose(); } catch { }
            phaseLockHandle = null;
        }

        private static bool HasReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint file_attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME creation_time;
            public System.Runtime.InteropServices.ComTypes.FILETIME last_access_time;
            public System.Runtime.InteropServices.ComTypes.FILETIME last_write_time;
            public uint volume_serial_number;
            public uint file_size_high;
            public uint file_size_low;
            public uint number_of_links;
            public uint file_index_high;
            public uint file_index_low;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(IntPtr fileHandle,
            out ByHandleFileInformation information);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true,
            EntryPoint = "CreateHardLinkW")]
        private static extern bool CreateHardLink(string fileName, string existingFileName,
            IntPtr securityAttributes);

        private static int FileLinkCount(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    ByHandleFileInformation information;
                    return GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(),
                        out information) ? checked((int)information.number_of_links) : -1;
                }
            }
            catch { return -1; }
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') ||
                (value >= 'A' && value <= 'F');
        }

        private static Dictionary<string, object> ReadJsonObject(string path)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 };
            object value = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
            var result = value as Dictionary<string, object>;
            Require(result != null, "IMMUTABLE_NATIVE_PLAN_JSON_INVALID", 3,
                "native_build_plan.json must contain a JSON object");
            return result;
        }

        private static Dictionary<string, object> ChildObject(Dictionary<string, object> parent, string name)
        {
            object value;
            var result = parent != null && parent.TryGetValue(name, out value)
                ? value as Dictionary<string, object> : null;
            Require(result != null, "IMMUTABLE_NATIVE_PLAN_FIELD_MISSING", 3,
                "native_build_plan.json is missing object field " + name);
            return result;
        }

        private static string TextValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        }

        private static double NumberValue(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return double.NaN;
            return Safe(delegate { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }, double.NaN);
        }

        private static List<string> PhaseFiles(string cadDir, string phase)
        {
            return CadInventory(cadDir);
        }

        private static Dictionary<string, string> SeedInventoryShaByFile()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "U型锁钩垫板.SLDPRT", "93378BDFB9666CDE8379F59A0506E7A4508AFAEDAC47121C3B8CBDAE56480834" },
                { "标准寄存柜 模型.SLDPRT", "12A69BDCC4BBC0CE2E7336D7ADF460FA1DC47279DA1F7A0D8F886651E22FE0A6" },
                { "标准寄存柜1917×760×550(总装配).SLDASM", "5AED314E89A65A3875C517B2F4DC179635E9C684E0CD1877AFEBD33CAA77CA5B" },
                { "插销固定板.SLDPRT", "4B511C2FB2371060D998F2C0667C12980DF4A151E5CF4FDCD2934F7C7AAC1C5C" },
                { "插销固定板2╱12_右.SLDPRT", "531FC517512BA4DC47EDADCBA59ABDEE27659EE71AC5595685D794A4C6D62DEC" },
                { "插销固定板2╱12_左.SLDPRT", "DE00F2437C44F203BF738285AD2B5E88BE6C4F06697644E47AD95DF7FFE5EE54" },
                { "插销固定板4╱12_右.SLDPRT", "A1694B12B13EFCA6C39B34C599048A80595AE42E64D73268E6A44C47FC51C113" },
                { "插销固定板4╱12_左.SLDPRT", "E54810968EB6F151372F6774DE510F6F9280AE730EB5EAF9E88CA6AC48BAD4F9" },
                { "插销固定板6╱12_右.SLDPRT", "DF93CA2675AE7D5F5DE077EF953E29D4EAC7C5CCF99AF6B429D9DBAB47E0B30D" },
                { "插销固定板6╱12_左.SLDPRT", "266FF91F001C82FD992BA2B9863CB65C586605B407AF334309065D745F891CED" },
                { "储物柜门2╱12焊接_右.SLDASM", "FE384F2EBBFCEB2DBBCE46DF2B376A27A7BEF19891524C3E459DCCDC12B86D9C" },
                { "储物柜门2╱12焊接_左.SLDASM", "68DB9E0DCB0BB5B83A919FF8C19A8CAD0D1E5255E507869B9EC8DDA4A7705455" },
                { "储物柜门4╱12焊接_右.SLDASM", "FAE2E5B9E14A3D8C1B5635157CCAA307F81EA6AFB33C9011F213F592E4959D4B" },
                { "储物柜门4╱12焊接_左.SLDASM", "8494F00F634CA00CE1A41FE94FF2E1509AE0378B01A0B9B94B89B7C4BCB0714B" },
                { "储物柜门6╱12焊接_右.SLDASM", "E16EE71F284D2BD212CC971E222C665E659B5B65A71FFBC312B8D44488EFC213" },
                { "储物柜门6╱12焊接_左.SLDASM", "931411848A8F8C2333B5783FC21D5503B19A6F6E0538DBF52EA8251193EB331D" },
                { "储物柜门板2╱12_W317.SLDPRT", "79D17A1AC85A5059CBD9C3094AB78EB2027E403E43D0E738B638C8BC12E2E135" },
                { "储物柜门板2╱12_右.SLDPRT", "735F6DABE58153F7023F0327AF4C23C98DECEB62BAE55F13361D3BAF7FA23139" },
                { "储物柜门板4╱12_W317.SLDPRT", "BFC881E6FA540C63373896CA14E0816B222726FC3D9634017B0A955D9DB01852" },
                { "储物柜门板4╱12_右.SLDPRT", "9912C4FC006DCEEB84758A332ACF36EFC5A78EF7978922A6AE303121736902F2" },
                { "储物柜门板6╱12_W317.SLDPRT", "4D5F102979BA1556295A7D4E95D376C1910B1FC7B9E3D16EAA1043A65BCF9D8B" },
                { "储物柜门板6╱12_右.SLDPRT", "C2C2456362719C57B1FCBBA599DA0CE5B41ADAF7B1BD1C2CBDA01A15B5C48783" },
                { "储物柜门装配_L2.SLDASM", "28208011F7DD368B59F1426CA3767222773CFD5D7857389BDA726B00AA4F8C70" },
                { "储物柜门装配_L4.SLDASM", "20E0D36E1C3BA2BC1658DEBA2C7C7900F23F304E238E43950D9F90B9091A0AAC" },
                { "储物柜门装配_L6.SLDASM", "7E4AE74852D526005C1F0F6EA65DF9AA14882EBC50E6EE4D1A33C02B36A8BC47" },
                { "储物柜门装配_R2.SLDASM", "E33AA7D2D67F31334A5D8EBAE6166253E5816F571090E31A6670F77C5111B724" },
                { "储物柜门装配_R4.SLDASM", "D53CE8564677C7481F77BDFB357E53723D6AFCCEF772CD78A8A5279CC36A0B05" },
                { "储物柜门装配_R6.SLDASM", "04E6DF9C3FA5664F37E66139DD7E5F3739CB7F1189338643B292F329F7B12918" },
                { "底座 模型.sldprt", "483B1487553E495DE8A1051C0CDFC78B9ED259B3CA329BF7F72B9517730C3F7F" },
                { "底座底板.sldprt", "EBCC3E1FDC9D906E4CD547C084E6306CA23E50B80EE1B1702AF91C2755261375" },
                { "底座焊接.SLDASM", "74B0EEEF76A764BEC40846336F3823A6A0DD7DAD36616F4B06ABD0B2F3CF9CAF" },
                { "底座加强筋.sldprt", "0B80DBC4CE9C67F02BB7F2870E212AA1AF806E9A851FA4F18B0D946BB17CC1DB" },
                { "底座外框.sldprt", "11C87FF9998EC14B77C9730438D0120E1E9F142FED09FD6186E90CDC234D8EDC" },
                { "调整脚 M12X60(模型).SLDPRT", "24FC77C1205C48873B964D3707D23F7644BE7934D796B52BFB79116A7ACEE344" },
                { "柜门加强筋2╱12.SLDPRT", "20735027C2DD2009DA1607C7350FDF80CA3559D3E6CC0F542718F48A16E2DFC7" },
                { "柜门加强筋4╱12.SLDPRT", "80C1FC45A9F46E7148BD6B0FD7F600A029D091F847A5E730DD9D842950919426" },
                { "柜门加强筋6╱12.SLDPRT", "48C5E0EF8A8EBE435BDA735FDD6EFEE7A32B43787D4E80006C57053378AD4639" },
                { "开口挡圈5.SLDPRT", "84ACBBAD72BC3DB175AD77C5DB5ED5F4A74410435C3CDEF1574B6A7808DCC754" },
                { "螺母M12.SLDPRT", "AD189C4AAFFDFDCE2E19DCFCC55DEB4BA80C88B16EF74F23AB635E13E7FB10DD" },
                { "门框 横隔板.sldprt", "77D7A468626441DB0673FBEB759894B2E11A6B10A4009060779C83666779B771" },
                { "门框 横隔板R.SLDPRT", "D58A6610DFCC67CB4BDCA4BE3AAB241751B4FE6B0A567BA4E627EB73DB577507" },
                { "门框 上.sldprt", "90F8521A8DF3CAE350D42C927E611CEA34E62C9332ACE5CC5DCB37F9EB4079AB" },
                { "门框 竖隔板L.sldprt", "B05B55EEF8F53DCF30490E247418D731B5926F9CD1AA56C7926B2ADD683D68FA" },
                { "门框 竖隔板R.SLDPRT", "2E9C39D83147047BBA67FCF161A05DAD686AD88CDAD5D46E66A1F79CC332FF15" },
                { "门框 下.sldprt", "D7BA5690671BBF940FE6B8FA04C95A3A5BE5FEDF718D6A3B401334462B172176" },
                { "门框 右.sldprt", "53DE0526B546DA495C601B0E5141CD7C45EF8FE68709E5CEA4087D9AAABA6A11" },
                { "门框 左.sldprt", "72B1494D8C4DBF813E4B01DC1DD4750D1338FA466274ABB8CD6784E4F5B4D4AB" },
                { "门框焊接.sldasm", "DB63F41DA2B589A70180E439C7246F4FD0B2E25B36DA0AABC57897FB99C292E4" },
                { "门轴销.SLDPRT", "B7289A146C9827050573FACEF66DCA06919FC41B943C39548AF536FE21516BC0" },
                { "上盖 模型.sldprt", "8E695DAA430AACFD242AC456716A1878E52B2EA08B465F2B2D5BED9C3CCC01CD" },
                { "上盖焊接.SLDASM", "4B2CF44D1CB80029FF0CEDFC98128E42C2E3E7EBC050B2450504FF62469C3936" },
                { "上盖壳体底板.sldprt", "C95699600DF8E84C1D3F938DBD36961BDBA185853C7E6A0E6A087B3475D9370B" },
                { "上盖壳体后侧板.sldprt", "1D9FB562BED66B88830CE5CAC49F58BA2DEC8CAE147AAA5BF1236AE8CEC3049E" },
                { "上盖壳体前侧板.sldprt", "DC5AB78914588D20022D1CE49736BC4D0F4480969A7988F74D173BF66C709C25" },
                { "上盖壳体右侧板.SLDPRT", "F01F8FDA28C4028C629BA3AE428B277CEE88B61D0D73145C46C17845A8CC2641" },
                { "上盖壳体左侧板.sldprt", "E937ACB7B5819284B4FC8C648B246002CB1A9E8EE6B3BBBF91BA788CE9FF6B70" },
                { "塑料轴套(云绅模具).SLDPRT", "930EE6070D278D5299D1EECED4F43367EE404A0E921517DE45297C30A8A12C7B" },
                { "锁控维护条_源钣金.SLDPRT", "78B607C43E7A180275982E3997FF63C250633AF8060BD714735434A6327ECA35" },
                { "锁舌.SLDPRT", "26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719" },
                { "箱体侧板加强筋1.sldprt", "61F8916F05AA9374382BCF55885BADBF9FCE64BF35F3E7C40482C18B552AE949" },
                { "箱体侧板加强筋2.sldprt", "113757268FAC2A24F6DCE06DEBB091F13852A256C8091CFC1D0BF795CE4183DC" },
                { "箱体横层板L.sldprt", "86B2189A93CE3758C548E72E38819DE2730267180FFCD0DE57394418FD66DA70" },
                { "箱体横层板L焊接.SLDASM", "09A3020B973FF6E93597A2E8FBC4B688CAA96570E868D0EE6D45E23591E692E7" },
                { "箱体横层板R.SLDPRT", "8CF52F351BA83582431AD9AA3349C84E2CF4675D44099A4679ECA71663E17F93" },
                { "箱体横层板R焊接.SLDASM", "88BD23A94D00199D37270B95D274389C6905D8D9AD64E31AD280DC64130E8480" },
                { "箱体横层板加强筋.SLDPRT", "7E73028D689B8B222349AC11DD25245B35561A3BCFA9992347F9CF1F642B2A3E" },
                { "箱体竖隔板L.sldprt", "9E14771F9EB8912245B0FB4238D62D6A53E88CE41FF8344F4C237BA43417C198" },
                { "箱体竖隔板L焊接.SLDASM", "36E4C42A59D8E36A02284540A427880A286F59106AB1E2EFB82CE62121A0F855" },
                { "箱体竖隔板R.SLDPRT", "C2B923131D743817E5E38594AF9C5C7AC9A255E77F702642FDE0E1917FE70EA3" },
                { "箱体竖隔板R焊接.SLDASM", "56AAA0FD6FBC8F3EEAF731770FD93C871451AC30E13B6BB8F525E178DAB29120" },
                { "箱体竖隔板加强件.sldprt", "585925A0BC85D6F865F11F410CE01A2B426883C4C351AD61B7A8A8ECF0BA5646" },
                { "箱体右侧板.sldprt", "D459D29BAE8CBCA4DDB261BD2C9FCF4423F641A3894741DE807E23F36FFAB087" },
                { "箱体右侧板焊接.SLDASM", "4BD4BC82EFCEB9CC49381ED6104EB4BA94C02B804B95F9E9449E274DB5B1F031" },
                { "箱体左侧板.sldprt", "B9D294D3CD5E03B934A24238A75BCBD4C903222319826F74AD8664A5971CA73A" },
                { "箱体左侧板焊接.sldasm", "991D62E31B10861FC00F5981EFFAB6BFF7F43F879CA750AB7A282CABD9C2754F" },
            };
        }

        private static List<string> PhaseAllowlist(string cadDir, string phase)
        {
            IEnumerable<string> names = phase == "dimensions"
                ? DimensionPlans(cadDir).Select(delegate(DimensionPlan plan) { return plan.file_name; })
                : phase == "derived"
                    ? DerivedPlans(cadDir).Select(delegate(DerivedPlan plan) { return plan.file_name; })
                    : phase == "base-hole"
                        ? new[] { "底座底板.sldprt" }
                        : AssemblyPlans(cadDir).Select(delegate(AssemblyPlan plan) { return plan.file_name; });
            return names.Select(delegate(string name) { return ExactFile(cadDir, name); })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> CadInventory(string cadDir)
        {
            return CaptureCadTree(cadDir).cad_files;
        }

        private static string AuthorizationPreInventoryDigest(string cadDir)
        {
            CadTreeSnapshot tree = CaptureCadTree(cadDir);
            AssertFlatSafeCadTree(cadDir, tree, "AUTHORIZATION_SEED_CAD_FILE_SYSTEM_SAFETY_FAILED", 3);
            return InventoryDigest(tree.cad_files);
        }

        private static CadTreeSnapshot CaptureCadTree(string cadDir)
        {
            var snapshot = new CadTreeSnapshot();
            string root = Path.GetFullPath(cadDir).TrimEnd('\\', '/');
            var pending = new Queue<string>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                string directory = pending.Dequeue();
                foreach (string child in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string fullChild = Path.GetFullPath(child);
                    if (HasReparsePoint(fullChild))
                        throw new InvalidDataException("CAD tree contains a reparse directory: " + fullChild);
                    snapshot.directories.Add(fullChild);
                    pending.Enqueue(fullChild);
                }
                foreach (string path in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string extension = Path.GetExtension(path);
                    if (string.Equals(extension, ".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".SLDASM", StringComparison.OrdinalIgnoreCase))
                        snapshot.cad_files.Add(Path.GetFullPath(path));
                }
            }
            snapshot.directories.Sort(StringComparer.OrdinalIgnoreCase);
            snapshot.cad_files.Sort(StringComparer.OrdinalIgnoreCase);
            return snapshot;
        }

        private static void AssertFlatSafeCadTree(string cadDir, CadTreeSnapshot tree,
            string status, int exitCode)
        {
            string root = Path.GetFullPath(cadDir).TrimEnd('\\', '/');
            Require(tree != null && tree.directories.Count == 0, status, exitCode,
                "working_pack must be flat; nested CAD directories are forbidden");
            foreach (string path in tree.cad_files)
                Require(SamePath(Path.GetDirectoryName(path), root) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1, status, exitCode,
                    "every one of the 75 CAD files must be top-level, non-reparse, and single-link: " + path);
        }

        private static void AssertInitialInventory(string cadDir, Result result, string phase)
        {
            CadTreeSnapshot tree = CaptureCadTree(cadDir);
            List<string> inventory = tree.cad_files;
            AssertFlatSafeCadTree(cadDir, tree, "CAD_FILE_SYSTEM_SAFETY_GATE_FAILED", 6);
            Dictionary<string, string> seed = SeedInventoryShaByFile();
            List<string> names = inventory.Select(Path.GetFileName).OrderBy(delegate(string value)
            {
                return value;
            }, StringComparer.OrdinalIgnoreCase).ToList();
            List<string> expectedNames = seed.Keys.OrderBy(delegate(string value) { return value; },
                StringComparer.OrdinalIgnoreCase).ToList();
            int partCount = inventory.Count(delegate(string path)
            {
                return string.Equals(Path.GetExtension(path), ".SLDPRT", StringComparison.OrdinalIgnoreCase);
            });
            int assemblyCount = inventory.Count(delegate(string path)
            {
                return string.Equals(Path.GetExtension(path), ".SLDASM", StringComparison.OrdinalIgnoreCase);
            });
            Require(inventory.Count == 75 && partCount == 53 && assemblyCount == 22 &&
                names.SequenceEqual(expectedNames, StringComparer.OrdinalIgnoreCase),
                "CAD_INVENTORY_NAME_OR_COUNT_DRIFT", 6,
                "working pack must contain exactly the compiled 75 names: 53 SLDPRT and 22 SLDASM");
            result.initial_cad_file_names.AddRange(names);
            result.initial_part_count = partCount;
            result.initial_assembly_count = assemblyCount;
            result.initial_inventory_digest = InventoryDigest(inventory);
            if (phase == "dimensions")
            {
                bool exactNonRootSeed = inventory.Where(delegate(string path)
                {
                    return !string.Equals(Path.GetFileName(path), SeedRootFileName,
                        StringComparison.OrdinalIgnoreCase);
                }).All(delegate(string path)
                {
                    string expected;
                    return seed.TryGetValue(Path.GetFileName(path), out expected) &&
                        string.Equals(Sha256(path), expected, StringComparison.OrdinalIgnoreCase);
                });
                Require(exactNonRootSeed && IsSha256(Sha256(ExactFile(cadDir, SeedRootFileName))),
                    "COMPILED_V37_NON_ROOT_SEED_HASH_PROFILE_DRIFT", 6,
                    "dimensions phase requires the exact 74 non-root V37 files; the stabilized root is bound by the seed receipt");
                result.compiled_seed_profile_gate = exactNonRootSeed;
            }
            else
            {
                result.compiled_seed_profile_gate = true;
            }
        }

        private static string InventoryDigest(IEnumerable<string> paths)
        {
            var builder = new StringBuilder();
            foreach (string path in paths.OrderBy(delegate(string value) { return Path.GetFileName(value); },
                StringComparer.OrdinalIgnoreCase))
                builder.Append(Path.GetFileName(path)).Append('|').Append(Sha256(path)).Append('\n');
            return Sha256Text(builder.ToString());
        }

        private static string CompiledSeedProfileDigest()
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> row in SeedInventoryShaByFile().OrderBy(
                delegate(KeyValuePair<string, string> value) { return value.Key; },
                StringComparer.OrdinalIgnoreCase))
                builder.Append(row.Key).Append('|').Append(row.Value).Append('\n');
            return Sha256Text(builder.ToString());
        }

        private static void AssertPostPhaseInventoryAndClosure(string cadDir, Result result)
        {
            CadTreeSnapshot tree = CaptureCadTree(cadDir);
            List<string> inventory = tree.cad_files;
            AssertFlatSafeCadTree(cadDir, tree, "POST_PHASE_CAD_FILE_SYSTEM_SAFETY_GATE_FAILED", 11);
            List<string> names = inventory.Select(Path.GetFileName).OrderBy(delegate(string value)
            {
                return value;
            }, StringComparer.OrdinalIgnoreCase).ToList();
            int partCount = inventory.Count(delegate(string path)
            {
                return string.Equals(Path.GetExtension(path), ".SLDPRT", StringComparison.OrdinalIgnoreCase);
            });
            int assemblyCount = inventory.Count(delegate(string path)
            {
                return string.Equals(Path.GetExtension(path), ".SLDASM", StringComparison.OrdinalIgnoreCase);
            });
            Require(inventory.Count == 75 && partCount == 53 && assemblyCount == 22 &&
                names.SequenceEqual(result.initial_cad_file_names, StringComparer.OrdinalIgnoreCase),
                "POST_PHASE_CAD_INVENTORY_DRIFT", 11,
                "phase added, removed, or renamed CAD files, or changed the exact 53/22 split");

            var session = new SessionRecord { purpose = "post_phase_dependency_closure" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && session.created && OwnedSessionIsExclusive(session),
                    "POST_PHASE_CLOSURE_SESSION_UNAVAILABLE", 11,
                    "could not create an exact owned SolidWorks 2020 closure session");
                AssertPackInventoryAndDependencies(sw, cadDir, result, "post-phase");
            }
            finally
            {
                if (sw != null) CloseOwnedSession(ref sw, result, session);
            }
            Require(session.process_exited, "POST_PHASE_CLOSURE_SESSION_DID_NOT_EXIT", 11,
                "post-phase closure SolidWorks process did not exit cleanly");
            tree = CaptureCadTree(cadDir);
            AssertFlatSafeCadTree(cadDir, tree, "POST_CLOSURE_CAD_FILE_SYSTEM_SAFETY_GATE_FAILED", 11);
            inventory = tree.cad_files;
            names = inventory.Select(Path.GetFileName).OrderBy(delegate(string value) { return value; },
                StringComparer.OrdinalIgnoreCase).ToList();
            Require(inventory.Count == 75 && names.SequenceEqual(result.initial_cad_file_names,
                    StringComparer.OrdinalIgnoreCase) && result.root_dependency_pack_total_count == 75 &&
                result.root_dependencies_all_local,
                "POST_PHASE_CLOSURE_OR_INVENTORY_DRIFT", 11,
                "post-phase dependency closure must equal the same 75 local CAD files");
            result.post_part_count = inventory.Count(delegate(string path)
            {
                return string.Equals(Path.GetExtension(path), ".SLDPRT", StringComparison.OrdinalIgnoreCase);
            });
            result.post_assembly_count = inventory.Count(delegate(string path)
            {
                return string.Equals(Path.GetExtension(path), ".SLDASM", StringComparison.OrdinalIgnoreCase);
            });
            result.post_inventory_digest = InventoryDigest(inventory);
            result.post_inventory_and_closure_gate = result.post_part_count == 53 &&
                result.post_assembly_count == 22;
            Require(result.post_inventory_and_closure_gate, "POST_PHASE_CAD_TYPE_COUNT_DRIFT", 11,
                "post-phase inventory must remain exactly 53 SLDPRT and 22 SLDASM");
        }

        private static void AssertPackInventoryAndDependencies(ISldWorks sw, string cadDir, Result result,
            string phase)
        {
            CadTreeSnapshot tree = CaptureCadTree(cadDir);
            AssertFlatSafeCadTree(cadDir, tree, "PACK_CAD_FILE_SYSTEM_SAFETY_GATE_FAILED", 14);
            List<string> inventory = tree.cad_files;
            result.pack_inventory_count = inventory.Count;
            result.pack_inventory_all_local = inventory.TrueForAll(delegate(string path)
            {
                return IsUnder(path, cadDir) && File.Exists(path);
            });
            Require(result.pack_inventory_count == 75 && result.pack_inventory_all_local,
                "PACK_AND_GO_INVENTORY_GATE_FAILED", 14,
                "candidate must contain exactly 75 local CAD files");

            string rightPartition = ExactFile(cadDir, "箱体竖隔板R.SLDPRT");
            result.frozen_right_partition_path = rightPartition;
            result.frozen_right_partition_sha256 = File.Exists(rightPartition) ? Sha256(rightPartition) : "";
            result.frozen_right_partition_sha_match = string.Equals(
                result.frozen_right_partition_sha256, FrozenRightPartitionSha256,
                StringComparison.OrdinalIgnoreCase);
            Require(result.frozen_right_partition_sha_match,
                "FROZEN_RIGHT_PARTITION_SHA_DRIFT", 14,
                "frozen R partition hash no longer matches the approved artifact");

            string rootPath = ExactFile(cadDir, SeedRootFileName);
            result.seed_root_path = rootPath;
            result.seed_root_sha256 = File.Exists(rootPath) ? Sha256(rootPath) : "";
            result.seed_root_initial_hash_gate = phase != "dimensions" ||
                IsSha256(result.seed_root_sha_after_stable) && string.Equals(
                    result.seed_root_sha256, result.seed_root_sha_after_stable,
                    StringComparison.OrdinalIgnoreCase);
            Require(result.seed_root_initial_hash_gate, "STABILIZED_SEED_ROOT_SHA_DRIFT", 14,
                "dimensions phase root assembly must match rootShaAfterStable in the exact consumed seed receipt");
            object raw = Safe(delegate
            {
                return sw.GetDocumentDependencies2(rootPath, true, true, false);
            }, null);
            Array values = raw as Array;
            result.root_dependency_raw_count = values == null ? 0 : values.Length;
            var dependencyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
                foreach (object value in values)
                {
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                    if (!Path.IsPathRooted(text)) continue;
                    string extension = Path.GetExtension(text);
                    if (!string.Equals(extension, ".SLDPRT", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".SLDASM", StringComparison.OrdinalIgnoreCase)) continue;
                    dependencyPaths.Add(Path.GetFullPath(text));
                }
            dependencyPaths.Add(rootPath);
            result.root_dependency_pack_total_count = dependencyPaths.Count;
            result.root_dependencies_all_local = dependencyPaths.All(delegate(string path)
            {
                return IsUnder(path, cadDir) && File.Exists(path);
            });
            var inventorySet = new HashSet<string>(inventory.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            result.root_dependency_exact_inventory_match = dependencyPaths.SetEquals(inventorySet);
            Require(result.root_dependency_pack_total_count == 75 && result.root_dependencies_all_local &&
                result.root_dependency_exact_inventory_match,
                "ROOT_DEPENDENCY_LOCAL_INVENTORY_GATE_FAILED", 14,
                "root dependency closure must resolve to the same 75 local CAD files");
        }

        private static void CreateBackups(Result result, List<string> paths)
        {
            string parent = Path.GetDirectoryName(result.out_json) ?? Path.GetTempPath();
            result.backup_directory = Path.Combine(parent,
                ".ConfigureNativeWidth888-" + result.phase + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(result.backup_directory);
            int index = 0;
            foreach (string path in paths)
            {
                string backup = Path.Combine(result.backup_directory,
                    index.ToString("D3", CultureInfo.InvariantCulture) + "-" + Path.GetFileName(path) + ".bak");
                var record = new FileRecord
                {
                    path = path,
                    backup_path = backup,
                    sha_before = Sha256(path),
                    size_before = new FileInfo(path).Length,
                    last_write_utc_ticks_before = File.GetLastWriteTimeUtc(path).Ticks
                };
                File.Copy(path, backup, false);
                record.backup_created = File.Exists(backup) &&
                    string.Equals(record.sha_before, Sha256(backup), StringComparison.OrdinalIgnoreCase);
                Require(record.backup_created, "PHASE_BACKUP_FAILED", 13,
                    "backup hash mismatch for " + path);
                result.files.Add(record);
                index++;
            }
            result.backup_complete = result.files.Count == paths.Count &&
                result.files.TrueForAll(delegate(FileRecord file) { return file.backup_created; });
            Require(result.backup_complete, "PHASE_BACKUP_INCOMPLETE", 13,
                "phase backup set is incomplete");
        }

        private static void CaptureEndHashes(Result result)
        {
            foreach (FileRecord file in result.files)
            {
                file.sha_after_readonly_reopen = File.Exists(file.path) ? Sha256(file.path) : "";
                file.size_after = File.Exists(file.path) ? new FileInfo(file.path).Length : -1;
            }
        }

        private static void CaptureAfterSaveHashes(Result result)
        {
            foreach (FileRecord file in result.files)
                file.sha_after_save = File.Exists(file.path) ? Sha256(file.path) : "";
        }

        private static void AuditChangedPaths(Result result, List<string> allowlist)
        {
            result.actual_changed_paths.Clear();
            result.unexpected_changed_paths.Clear();
            var allowed = new HashSet<string>(allowlist.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            foreach (FileRecord file in result.files)
            {
                string current = File.Exists(file.path) ? Sha256(file.path) : "";
                if (string.Equals(current, file.sha_before, StringComparison.OrdinalIgnoreCase)) continue;
                result.actual_changed_paths.Add(file.path);
                if (!allowed.Contains(Path.GetFullPath(file.path)))
                    result.unexpected_changed_paths.Add(file.path);
            }
        }

        private static void RunDimensionsEdit(ISldWorks sw, string cadDir, Result result)
        {
            foreach (DimensionPlan plan in DimensionPlans(cadDir))
            {
                string path = ExactFile(cadDir, plan.file_name);
                var record = new DimensionPartRecord { file_name = plan.file_name, path = path };
                result.dimension_parts.Add(record);
                ModelDoc2 model = null;
                try
                {
                    int errors = 0, warnings = 0;
                    model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, false, ref errors, ref warnings);
                    record.opened = model != null;
                    record.open_errors = errors;
                    record.open_warnings = warnings;
                    Require(model != null, "DIMENSION_PART_OPEN_FAILED", 20, "could not open " + path);
                    Require(errors == 0 && warnings == 0, "DIMENSION_PART_OPEN_ERRORS_OR_WARNINGS", 20,
                        plan.file_name + " opened with errors/warnings=" +
                        errors.ToString(CultureInfo.InvariantCulture) + "/" +
                        warnings.ToString(CultureInfo.InvariantCulture));
                    Try(delegate { model.ShowFeatureErrorDialog = false; });
                    record.before = CapturePartSnapshot(model);
                    if (string.Equals(plan.file_name, "标准寄存柜 模型.SLDPRT",
                        StringComparison.OrdinalIgnoreCase))
                        Require(MasterTopologyGate(record.before), "MASTER_TOPOLOGY_PRECONDITION_DRIFT", 22,
                            "master part must start with exact 20 bodies and 220 features, error/warning zero");
                    bool anyChanged = false;
                    foreach (DimensionTarget target in plan.targets)
                    {
                        var row = new DimensionRow
                        {
                            name = target.name,
                            source_mm = target.source_mm,
                            target_mm = target.target_mm
                        };
                        record.dimensions.Add(row);
                        Dimension dimension = FindDimension(model, target.name);
                        row.found = dimension != null;
                        Require(dimension != null, "DRIVING_DIMENSION_NOT_FOUND", 21,
                            plan.file_name + " missing " + target.name);
                        row.before_mm = Safe(delegate { return dimension.SystemValue * 1000.0; }, double.NaN);
                        row.driven_state_before = Safe(delegate { return dimension.DrivenState; }, -1);
                        row.driving_before = row.driven_state_before == target.expected_driven_state;
                        Require(row.driving_before, "DRIVING_DIMENSION_STATE_DRIFT", 22,
                            plan.file_name + " " + target.name + " is not a driving dimension");
                        row.precondition_source_or_target = Near(row.before_mm, target.source_mm, DimensionToleranceMm) ||
                            Near(row.before_mm, target.target_mm, DimensionToleranceMm);
                        Require(row.precondition_source_or_target, "DRIVING_DIMENSION_PRECONDITION_DRIFT", 22,
                            plan.file_name + " " + target.name + " is neither expected source nor target value");
                        if (Near(row.before_mm, target.source_mm, DimensionToleranceMm))
                        {
                            dimension.SystemValue = target.target_mm / 1000.0;
                            row.changed = true;
                            anyChanged = true;
                        }
                        row.after_mm = Safe(delegate { return dimension.SystemValue * 1000.0; }, double.NaN);
                        row.driven_state_after = Safe(delegate { return dimension.DrivenState; }, -1);
                        row.target_readback = Near(row.after_mm, target.target_mm, DimensionToleranceMm) &&
                            row.driven_state_after == target.expected_driven_state;
                        Release(dimension);
                        Require(row.target_readback, "DRIVING_DIMENSION_SET_FAILED", 23,
                            plan.file_name + " " + target.name + " did not read back at target");
                    }
                    record.changed = anyChanged;
                    record.idempotent_already_target = !anyChanged;
                    record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                    record.after = CapturePartSnapshot(model);
                    ValidateDimensionReadbacks(model, plan, record, false);
                    Require(record.rebuilt && PartHealthGate(record.after, plan.require_sheet_metal),
                        "DIMENSION_PART_HEALTH_GATE_FAILED", 24,
                        plan.file_name + " failed body/SheetMetal/FlatPattern/error0 gate");
                    if (string.Equals(plan.file_name, "标准寄存柜 模型.SLDPRT",
                        StringComparison.OrdinalIgnoreCase))
                        Require(MasterTopologyGate(record.after) && TopologySame(record.before, record.after),
                            "MASTER_TOPOLOGY_CHANGED_AFTER_REBUILD", 24,
                            "master part body/feature topology changed during dimensions stage");
                    if (anyChanged) SaveModel(model, record, result);
                    else record.save_not_required_idempotent = true;
                    Require(record.save_not_required_idempotent ||
                        (record.saved && record.save_errors == 0 && record.save_warnings == 0 &&
                            !record.save_flag_after_save),
                        "DIMENSION_PART_SAVE_FAILED", 25, "save failed for " + plan.file_name);
                }
                finally
                {
                    CloseDocument(sw, ref model);
                }
                FileRecord file = FileRecordFor(result, path);
                file.sha_after_save = Sha256(path);
                record.sha_after_save = file.sha_after_save;
                record.edit_gate = true;
            }
        }

        private static void RunDimensionsVerify(ISldWorks sw, string cadDir, Result result)
        {
            List<DimensionPlan> plans = DimensionPlans(cadDir);
            for (int index = 0; index < plans.Count; index++)
            {
                DimensionPlan plan = plans[index];
                DimensionPartRecord record = result.dimension_parts[index];
                ModelDoc2 model = null;
                try
                {
                    int errors = 0, warnings = 0;
                    model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART, true, ref errors, ref warnings);
                    record.reopen_open_errors = errors;
                    record.reopen_open_warnings = warnings;
                    Require(model != null, "DIMENSION_PART_READONLY_REOPEN_FAILED", 26,
                        "could not reopen " + record.path);
                    Require(errors == 0 && warnings == 0,
                        "DIMENSION_PART_READONLY_REOPEN_ERRORS_OR_WARNINGS", 26,
                        plan.file_name + " reopened with errors/warnings=" +
                        errors.ToString(CultureInfo.InvariantCulture) + "/" +
                        warnings.ToString(CultureInfo.InvariantCulture));
                    record.reopen_rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                    record.reopen = CapturePartSnapshot(model);
                    Require(record.reopen_rebuilt && PartHealthGate(record.reopen, plan.require_sheet_metal),
                        "DIMENSION_PART_REOPEN_HEALTH_FAILED", 27,
                        plan.file_name + " failed read-only reopen health gate");
                    if (string.Equals(plan.file_name, "标准寄存柜 模型.SLDPRT",
                        StringComparison.OrdinalIgnoreCase))
                        Require(MasterTopologyGate(record.reopen) && TopologySame(record.before, record.reopen),
                            "MASTER_TOPOLOGY_CHANGED_AFTER_REOPEN", 27,
                            "master part body/feature topology changed after fresh read-only reopen");
                    foreach (DimensionRow row in record.dimensions)
                    {
                        Dimension dimension = FindDimension(model, row.name);
                        row.reopen_found = dimension != null;
                        row.reopen_mm = dimension == null ? double.NaN :
                            Safe(delegate { return dimension.SystemValue * 1000.0; }, double.NaN);
                        row.reopen_driven_state = dimension == null ? -1 :
                            Safe(delegate { return dimension.DrivenState; }, -1);
                        row.reopen_target_readback = row.reopen_found &&
                            Near(row.reopen_mm, row.target_mm, DimensionToleranceMm) &&
                            row.reopen_driven_state == 2;
                        Release(dimension);
                        Require(row.reopen_target_readback, "DIMENSION_REOPEN_READBACK_FAILED", 28,
                            plan.file_name + " " + row.name + " did not reopen at target");
                    }
                    ValidateDimensionReadbacks(model, plan, record, true);
                    record.verify_gate = true;
                }
                finally
                {
                    CloseDocument(sw, ref model);
                }
                string hash = Sha256(record.path);
                FileRecord file = FileRecordFor(result, record.path);
                file.sha_after_readonly_reopen = hash;
                Require(string.Equals(file.sha_after_save, hash, StringComparison.OrdinalIgnoreCase),
                    "DIMENSION_READONLY_REOPEN_CHANGED_HASH", 29,
                    "read-only reopen changed " + record.path);
            }
        }

        private static void RunBaseHoleEdit(ISldWorks sw, string cadDir, Result result)
        {
            string path = ExactFile(cadDir, BaseHolePartFileName);
            DerivedPlan sourcePlan = DerivedPlans(cadDir).Single(delegate(DerivedPlan plan)
            {
                return string.Equals(plan.file_name, BaseHolePartFileName,
                    StringComparison.OrdinalIgnoreCase);
            });
            var record = new BaseHoleRecord
            {
                file_name = BaseHolePartFileName,
                path = path,
                source_path = ExactFile(cadDir, sourcePlan.source_file_name),
                sketch_name = BaseHoleSketchName,
                consumer_feature_name = BaseHoleConsumerFeatureName,
                source_abs_x_mm = BaseHoleSourceAbsXMm,
                target_abs_x_mm = BaseHoleTargetAbsXMm,
                source_span_mm = BaseHoleSourceSpanMm,
                target_span_mm = BaseHoleTargetSpanMm,
                expected_d2_mm = BaseHoleDepthSpanMm
            };
            result.base_hole = record;
            record.source_sha_before = Sha256(record.source_path);
            ModelDoc2 sourceModel = null;
            ModelDoc2 model = null;
            try
            {
                int sourceErrors = 0, sourceWarnings = 0;
                sourceModel = OpenDocument(sw, record.source_path,
                    (int)swDocumentTypes_e.swDocPART, true, ref sourceErrors, ref sourceWarnings);
                record.source_opened = sourceModel != null;
                record.source_open_errors = sourceErrors;
                record.source_open_warnings = sourceWarnings;
                Require(sourceModel != null && sourceErrors == 0 && sourceWarnings == 0,
                    "BASE_HOLE_SOURCE_CONTEXT_OPEN_FAILED", 30,
                    "could not read-only open the local base source before the bottom plate");
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, false,
                    ref errors, ref warnings);
                record.opened = model != null;
                record.open_errors = errors;
                record.open_warnings = warnings;
                Require(model != null, "BASE_HOLE_PART_OPEN_FAILED", 30,
                    "could not open " + path);
                Require(errors == 0 && warnings == 0,
                    "BASE_HOLE_PART_OPEN_ERRORS_OR_WARNINGS", 30,
                    BaseHolePartFileName + " opened with errors/warnings=" +
                    errors.ToString(CultureInfo.InvariantCulture) + "/" +
                    warnings.ToString(CultureInfo.InvariantCulture));
                Try(delegate { model.ShowFeatureErrorDialog = false; });

                record.before = CapturePartSnapshot(model);
                Require(PartHealthGate(record.before, true) && record.before.feature_count == 78,
                    "BASE_HOLE_PART_BASELINE_HEALTH_FAILED", 31,
                    "base bottom plate must remain one-body native sheet metal with 78 healthy features");
                record.links_before = CaptureLinkSnapshot(model, sourcePlan, cadDir, true);
                record.links_before_digest = LinkSnapshotDigest(record.links_before);
                Require(LinkGate(record.links_before, sourcePlan, true),
                    "BASE_HOLE_SOURCE_LINK_BASELINE_FAILED", 31,
                    "base bottom plate SplitBody source link is not the expected local source");

                Feature consumer = FindFeature(model, BaseHoleConsumerFeatureName);
                record.consumer_before = CaptureBaseHoleConsumerFeature(consumer);
                record.consumer_feature_found = consumer != null;
                bool consumerWarning = false;
                record.consumer_error_code2 = consumer == null ? -1 :
                    FeatureErrorCode2(consumer, out consumerWarning);
                record.consumer_warning = consumerWarning;
                record.consumer_error_code = consumer == null ? -1 :
                    Safe(delegate { return consumer.GetErrorCode(); }, -1);
                Require(BaseHoleConsumerFeatureGate(record.consumer_before),
                    "BASE_HOLE_CONSUMER_FEATURE_HEALTH_FAILED", 31,
                    BaseHoleConsumerFeatureName + " is missing or unhealthy");
                Release(consumer);

                Feature sketchFeature = FindFeature(model, BaseHoleSketchName);
                Require(sketchFeature != null, "BASE_HOLE_SKETCH_MISSING", 32,
                    BaseHoleSketchName + " is missing");
                Sketch sketch = Safe(delegate { return sketchFeature.GetSpecificFeature2() as Sketch; }, null);
                record.before_blocks.AddRange(CaptureBaseHoleBlocks(sketch));
                record.relations_before = CaptureBaseHoleRelations(sketch);
                CaptureBaseHoleDimensions(model, record, false);
                record.precondition_source = BaseHoleBlocksGate(record.before_blocks,
                    BaseHoleSourceAbsXMm) && Near(record.d1_before_mm, BaseHoleSourceSpanMm,
                    DimensionToleranceMm);
                record.precondition_target = BaseHoleBlocksGate(record.before_blocks,
                    BaseHoleTargetAbsXMm) && Near(record.d1_before_mm, BaseHoleTargetSpanMm,
                    DimensionToleranceMm);
                Require((record.precondition_source || record.precondition_target) &&
                    record.d1_driven_state_before == 1 && record.d2_driven_state_before == 1 &&
                    Near(record.d2_before_mm, BaseHoleDepthSpanMm, DimensionToleranceMm) &&
                    BaseHoleRelationBaselineGate(record.relations_before),
                    "BASE_HOLE_SKETCH_PRECONDITION_DRIFT", 32,
                    "草图9 must be the four native knockout blocks at source or target with reference D1/D2");

                if (record.precondition_source)
                {
                    Sketch active = null;
                    MathUtility math = null;
                    try
                    {
                        model.ClearSelection2(true);
                        record.sketch_selected = Safe(delegate
                        {
                            return sketchFeature.Select2(false, 0);
                        }, false);
                        Require(record.sketch_selected, "BASE_HOLE_SKETCH_SELECT_FAILED", 33,
                            "could not select " + BaseHoleSketchName);
                        model.EditSketch();
                        active = Safe(delegate { return model.SketchManager.ActiveSketch; }, null);
                        record.sketch_edit_entered = active != null;
                        Require(record.sketch_edit_entered, "BASE_HOLE_SKETCH_EDIT_FAILED", 33,
                            "could not enter " + BaseHoleSketchName);
                        math = Safe(delegate { return sw.GetMathUtility() as MathUtility; }, null);
                        Require(math != null, "BASE_HOLE_MATH_UTILITY_UNAVAILABLE", 33,
                            "SolidWorks MathUtility unavailable");
                        record.block_positions_set = SetBaseHoleBlockPositions(active, math,
                            BaseHoleTargetAbsXMm, record.position_updates);
                        model.SketchManager.InsertSketch(true);
                        record.sketch_edit_exited = Safe(delegate
                        {
                            return model.SketchManager.ActiveSketch == null;
                        }, false);
                    }
                    finally
                    {
                        if (Safe(delegate { return model.SketchManager.ActiveSketch != null; }, false))
                        {
                            record.sketch_exit_fallback_attempted = true;
                            record.sketch_exit_fallback_succeeded = TryAction(delegate
                            {
                                model.SketchManager.InsertSketch(false);
                            });
                        }
                        Release(active);
                        Release(math);
                    }
                    Require(record.block_positions_set == 4 && record.sketch_edit_exited,
                        "BASE_HOLE_BLOCK_POSITION_UPDATE_FAILED", 33,
                        "not all four native knockout blocks were moved and sketch exit failed");
                    record.changed = true;
                }
                else
                {
                    record.idempotent_already_target = true;
                }
                Release(sketch);
                Release(sketchFeature);

                record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                record.after = CapturePartSnapshot(model);
                Feature afterSketchFeature = FindFeature(model, BaseHoleSketchName);
                Sketch afterSketch = afterSketchFeature == null ? null :
                    Safe(delegate { return afterSketchFeature.GetSpecificFeature2() as Sketch; }, null);
                record.after_blocks.AddRange(CaptureBaseHoleBlocks(afterSketch));
                record.relations_after = CaptureBaseHoleRelations(afterSketch);
                Feature afterConsumer = FindFeature(model, BaseHoleConsumerFeatureName);
                record.consumer_after = CaptureBaseHoleConsumerFeature(afterConsumer);
                Release(afterConsumer);
                CaptureBaseHoleDimensions(model, record, true);
                record.links_after = CaptureLinkSnapshot(model, sourcePlan, cadDir, true);
                record.links_after_digest = LinkSnapshotDigest(record.links_after);
                record.part_box_preserved_after = SameNumericArray(record.before.part_box_m,
                    record.after.part_box_m, 1e-8);
                record.topology_preserved_after = DerivedPartHealthGate(record.after, record.before);
                Require(record.rebuilt && BaseHoleBlocksGate(record.after_blocks,
                        BaseHoleTargetAbsXMm) && Near(record.d1_after_mm,
                        BaseHoleTargetSpanMm, DimensionToleranceMm) &&
                    record.d1_driven_state_after == 1 &&
                    Near(record.d2_after_mm, BaseHoleDepthSpanMm, DimensionToleranceMm) &&
                    record.d2_driven_state_after == 1 && record.topology_preserved_after &&
                    record.part_box_preserved_after && LinkGate(record.links_after, sourcePlan, true) &&
                    BaseHoleConsumerFeatureGate(record.consumer_after) &&
                    LinkSnapshotsEqual(record.links_before, record.links_after),
                    "BASE_HOLE_IN_MEMORY_GATE_FAILED", 34,
                    "base bottom plate did not rebuild at four blocks X=±369 with D1=738 and preserved topology");
                Require(BaseHoleRelationsEqual(record.relations_before, record.relations_after),
                    "BASE_HOLE_RELATION_TOPOLOGY_CHANGED", 34,
                    "草图9 relation topology changed while moving the four block instances");
                Release(afterSketch);
                Release(afterSketchFeature);

                if (record.changed)
                {
                    SaveModel(model, record, result);
                    Require(record.saved && record.save_errors == 0 && record.save_warnings == 0 &&
                        !record.save_flag_after_save, "BASE_HOLE_SAVE_FAILED", 35,
                        "base bottom plate save failed or remained dirty");
                }
                else
                {
                    record.save_not_required_idempotent = true;
                }
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseDocument(sw, ref sourceModel);
            }
            record.source_sha_after = Sha256(record.source_path);
            record.source_hash_unchanged = string.Equals(record.source_sha_before,
                record.source_sha_after, StringComparison.OrdinalIgnoreCase);
            Require(record.source_hash_unchanged, "BASE_HOLE_SOURCE_CONTEXT_CHANGED_HASH", 35,
                "read-only source context changed " + record.source_path);
            FileRecord file = FileRecordFor(result, path);
            file.sha_after_save = Sha256(path);
            record.sha_after_save = file.sha_after_save;
            record.edit_gate = true;
        }

        private static void RunBaseHoleVerify(ISldWorks sw, string cadDir, Result result)
        {
            BaseHoleRecord record = result.base_hole;
            Require(record != null && record.edit_gate, "BASE_HOLE_EDIT_RECORD_MISSING", 36,
                "base-hole edit record is missing");
            DerivedPlan sourcePlan = DerivedPlans(cadDir).Single(delegate(DerivedPlan plan)
            {
                return string.Equals(plan.file_name, BaseHolePartFileName,
                    StringComparison.OrdinalIgnoreCase);
            });
            ModelDoc2 sourceModel = null;
            ModelDoc2 model = null;
            try
            {
                int sourceErrors = 0, sourceWarnings = 0;
                sourceModel = OpenDocument(sw, record.source_path,
                    (int)swDocumentTypes_e.swDocPART, true, ref sourceErrors, ref sourceWarnings);
                record.reopen_source_opened = sourceModel != null;
                record.reopen_source_open_errors = sourceErrors;
                record.reopen_source_open_warnings = sourceWarnings;
                Require(sourceModel != null && sourceErrors == 0 && sourceWarnings == 0,
                    "BASE_HOLE_READONLY_SOURCE_CONTEXT_OPEN_FAILED", 36,
                    "could not read-only reopen the local base source before the bottom plate");
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART, true,
                    ref errors, ref warnings);
                record.reopen_open_errors = errors;
                record.reopen_open_warnings = warnings;
                Require(model != null, "BASE_HOLE_READONLY_REOPEN_FAILED", 36,
                    "could not read-only reopen " + record.path);
                Require(errors == 0 && warnings == 0,
                    "BASE_HOLE_READONLY_REOPEN_ERRORS_OR_WARNINGS", 36,
                    BaseHolePartFileName + " reopened with errors/warnings=" +
                    errors.ToString(CultureInfo.InvariantCulture) + "/" +
                    warnings.ToString(CultureInfo.InvariantCulture));
                record.reopen_rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                record.reopen = CapturePartSnapshot(model);
                Feature sketchFeature = FindFeature(model, BaseHoleSketchName);
                Sketch sketch = sketchFeature == null ? null :
                    Safe(delegate { return sketchFeature.GetSpecificFeature2() as Sketch; }, null);
                record.reopen_blocks.AddRange(CaptureBaseHoleBlocks(sketch));
                record.relations_reopen = CaptureBaseHoleRelations(sketch);
                Feature reopenConsumer = FindFeature(model, BaseHoleConsumerFeatureName);
                record.consumer_reopen = CaptureBaseHoleConsumerFeature(reopenConsumer);
                Release(reopenConsumer);
                CaptureBaseHoleDimensions(model, record, null);
                record.links_reopen = CaptureLinkSnapshot(model, sourcePlan, cadDir, true);
                record.links_reopen_digest = LinkSnapshotDigest(record.links_reopen);
                record.part_box_preserved_reopen = SameNumericArray(record.before.part_box_m,
                    record.reopen.part_box_m, 1e-8);
                record.topology_preserved_reopen = DerivedPartHealthGate(record.reopen, record.before);
                record.verify_gate = record.reopen_rebuilt &&
                    BaseHoleBlocksGate(record.reopen_blocks, BaseHoleTargetAbsXMm) &&
                    Near(record.d1_reopen_mm, BaseHoleTargetSpanMm, DimensionToleranceMm) &&
                    record.d1_driven_state_reopen == 1 &&
                    Near(record.d2_reopen_mm, BaseHoleDepthSpanMm, DimensionToleranceMm) &&
                    record.d2_driven_state_reopen == 1 && record.topology_preserved_reopen &&
                    record.part_box_preserved_reopen && LinkGate(record.links_reopen, sourcePlan, true) &&
                    BaseHoleRelationsEqual(record.relations_before, record.relations_reopen) &&
                    BaseHoleConsumerFeatureGate(record.consumer_reopen) &&
                    LinkSnapshotsEqual(record.links_before, record.links_reopen);
                Require(record.verify_gate, "BASE_HOLE_READONLY_REOPEN_GATE_FAILED", 37,
                    "base bottom plate did not persist four blocks X=±369/D1=738 with preserved native topology");
                Release(sketch);
                Release(sketchFeature);
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseDocument(sw, ref sourceModel);
            }
            record.source_sha_after_readonly_reopen = Sha256(record.source_path);
            record.source_reopen_hash_unchanged = string.Equals(record.source_sha_before,
                record.source_sha_after_readonly_reopen, StringComparison.OrdinalIgnoreCase);
            Require(record.source_reopen_hash_unchanged,
                "BASE_HOLE_READONLY_SOURCE_CONTEXT_CHANGED_HASH", 38,
                "read-only source context reopen changed " + record.source_path);
            string hash = Sha256(record.path);
            FileRecord file = FileRecordFor(result, record.path);
            file.sha_after_readonly_reopen = hash;
            Require(string.Equals(file.sha_after_save, hash, StringComparison.OrdinalIgnoreCase),
                "BASE_HOLE_READONLY_REOPEN_CHANGED_HASH", 38,
                "read-only reopen changed " + record.path);
        }

        private static void CaptureBaseHoleDimensions(ModelDoc2 model, BaseHoleRecord record,
            bool? after)
        {
            Dimension d1 = FindDimension(model, "D1@" + BaseHoleSketchName);
            Dimension d2 = FindDimension(model, "D2@" + BaseHoleSketchName);
            double d1Mm = d1 == null ? double.NaN :
                Safe(delegate { return d1.SystemValue * 1000.0; }, double.NaN);
            double d2Mm = d2 == null ? double.NaN :
                Safe(delegate { return d2.SystemValue * 1000.0; }, double.NaN);
            int d1State = d1 == null ? -1 : Safe(delegate { return d1.DrivenState; }, -1);
            int d2State = d2 == null ? -1 : Safe(delegate { return d2.DrivenState; }, -1);
            if (!after.HasValue)
            {
                record.d1_reopen_mm = d1Mm;
                record.d2_reopen_mm = d2Mm;
                record.d1_driven_state_reopen = d1State;
                record.d2_driven_state_reopen = d2State;
            }
            else if (after.Value)
            {
                record.d1_after_mm = d1Mm;
                record.d2_after_mm = d2Mm;
                record.d1_driven_state_after = d1State;
                record.d2_driven_state_after = d2State;
            }
            else
            {
                record.d1_before_mm = d1Mm;
                record.d2_before_mm = d2Mm;
                record.d1_driven_state_before = d1State;
                record.d2_driven_state_before = d2State;
            }
            Release(d1);
            Release(d2);
        }

        private static BaseHoleFeatureHealth CaptureBaseHoleConsumerFeature(Feature feature)
        {
            var health = new BaseHoleFeatureHealth
            {
                found = feature != null,
                name = feature == null ? "" : Safe(delegate { return feature.Name; }, ""),
                type = feature == null ? "" : Safe(delegate { return feature.GetTypeName2(); }, ""),
                suppressed = feature != null && Safe(delegate { return feature.IsSuppressed(); }, true),
                error_code = feature == null ? -1 : Safe(delegate { return feature.GetErrorCode(); }, -1)
            };
            bool warning = false;
            health.error_code2 = feature == null ? -1 : FeatureErrorCode2(feature, out warning);
            health.warning = warning;
            return health;
        }

        private static bool BaseHoleConsumerFeatureGate(BaseHoleFeatureHealth health)
        {
            return health != null && health.found &&
                string.Equals(health.name, BaseHoleConsumerFeatureName, StringComparison.Ordinal) &&
                string.Equals(health.type, "ICE", StringComparison.OrdinalIgnoreCase) &&
                !health.suppressed && health.error_code == 0 && health.error_code2 == 0 &&
                !health.warning;
        }

        private static List<BaseHoleBlockRow> CaptureBaseHoleBlocks(Sketch sketch)
        {
            var rows = new List<BaseHoleBlockRow>();
            Array raw = sketch == null ? null :
                Safe(delegate { return sketch.GetSketchBlockInstances() as Array; }, null);
            if (raw == null) return rows;
            foreach (object value in raw)
            {
                SketchBlockInstance instance = value as SketchBlockInstance;
                MathPoint point = instance == null ? null :
                    Safe(delegate { return instance.InstancePosition; }, null);
                double[] data = DoubleArray(point == null ? null :
                    Safe(delegate { return point.ArrayData as Array; }, null));
                if (instance != null && data != null && data.Length >= 3)
                    rows.Add(new BaseHoleBlockRow
                    {
                        name = Safe(delegate { return instance.Name; }, ""),
                        x_mm = data[0] * 1000.0,
                        y_mm = data[1] * 1000.0,
                        z_mm = data[2] * 1000.0,
                        angle_rad = Safe(delegate { return instance.Angle; }, double.NaN),
                        scale = Safe(delegate { return instance.Scale; }, double.NaN)
                    });
                Release(point);
                Release(instance);
            }
            return rows.OrderBy(delegate(BaseHoleBlockRow row) { return row.name; },
                StringComparer.Ordinal).ToList();
        }

        private static BaseHoleRelationSnapshot CaptureBaseHoleRelations(Sketch sketch)
        {
            var snapshot = new BaseHoleRelationSnapshot();
            SketchRelationManager manager = sketch == null ? null :
                Safe(delegate { return sketch.RelationManager; }, null);
            Array raw = manager == null ? null :
                Safe(delegate { return manager.GetRelations(0) as Array; }, null);
            if (raw != null)
            {
                foreach (object value in raw)
                {
                    SketchRelation relation = value as SketchRelation;
                    if (relation == null) continue;
                    int type = Safe(delegate { return relation.GetRelationType(); }, -1);
                    var row = new BaseHoleRelationRow
                    {
                        relation_type = type,
                        relation_name = Enum.GetName(typeof(swConstraintType_e), type) ?? "",
                        suppressed = Safe(delegate { return relation.Suppressed; }, false),
                        entity_count = Safe(delegate { return relation.GetEntitiesCount(); }, -1),
                        definition_entity_count = ArrayLength(Safe(delegate
                        {
                            return relation.GetDefinitionEntities2() as Array;
                        }, null))
                    };
                    snapshot.rows.Add(row);
                    if (string.Equals(row.relation_name, "swConstraintType_COINCIDENT",
                        StringComparison.OrdinalIgnoreCase) ||
                        row.relation_name.IndexOf("COINCIDENT", StringComparison.OrdinalIgnoreCase) >= 0)
                        snapshot.coincident_count++;
                    Release(relation);
                }
            }
            Release(manager);
            snapshot.relation_count = snapshot.rows.Count;
            var builder = new StringBuilder();
            foreach (BaseHoleRelationRow row in snapshot.rows
                .OrderBy(delegate(BaseHoleRelationRow value) { return value.relation_type; })
                .ThenBy(delegate(BaseHoleRelationRow value) { return value.entity_count; })
                .ThenBy(delegate(BaseHoleRelationRow value) { return value.definition_entity_count; }))
                builder.Append(row.relation_type.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(row.relation_name).Append('|').Append(row.suppressed ? '1' : '0')
                    .Append('|').Append(row.entity_count.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(row.definition_entity_count.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            snapshot.digest_sha256 = Sha256Text(builder.ToString());
            return snapshot;
        }

        private static bool BaseHoleRelationBaselineGate(BaseHoleRelationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.relation_count != 6 ||
                snapshot.coincident_count != 0) return false;
            int horizontal = snapshot.rows.Count(delegate(BaseHoleRelationRow row)
            {
                return row.relation_type == (int)swConstraintType_e.swConstraintType_HORIZONTAL &&
                    !row.suppressed && row.entity_count == 1 && row.definition_entity_count == 1;
            });
            int distance = snapshot.rows.Count(delegate(BaseHoleRelationRow row)
            {
                return row.relation_type == (int)swConstraintType_e.swConstraintType_DISTANCE &&
                    row.suppressed && row.entity_count == 2 && row.definition_entity_count == 2;
            });
            return horizontal == 4 && distance == 2;
        }

        private static bool BaseHoleRelationsEqual(BaseHoleRelationSnapshot left,
            BaseHoleRelationSnapshot right)
        {
            return BaseHoleRelationBaselineGate(left) && BaseHoleRelationBaselineGate(right) &&
                string.Equals(left.digest_sha256, right.digest_sha256,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static int SetBaseHoleBlockPositions(Sketch sketch, MathUtility math,
            double targetAbsXMm, List<BaseHolePositionUpdate> updates)
        {
            Array raw = sketch == null ? null :
                Safe(delegate { return sketch.GetSketchBlockInstances() as Array; }, null);
            if (raw == null) return 0;
            int count = 0;
            foreach (object value in raw)
            {
                SketchBlockInstance instance = value as SketchBlockInstance;
                MathPoint beforePoint = instance == null ? null :
                    Safe(delegate { return instance.InstancePosition; }, null);
                double[] before = DoubleArray(beforePoint == null ? null :
                    Safe(delegate { return beforePoint.ArrayData as Array; }, null));
                string name = instance == null ? "" : Safe(delegate { return instance.Name; }, "");
                var update = new BaseHolePositionUpdate { name = name };
                updates.Add(update);
                if (instance == null || before == null || before.Length < 3 ||
                    !BaseHoleBlockNames.Contains(name, StringComparer.Ordinal) ||
                    Math.Abs(before[0]) < 1e-9)
                {
                    Release(beforePoint);
                    Release(instance);
                    continue;
                }
                update.before_x_mm = before[0] * 1000.0;
                update.before_y_mm = before[1] * 1000.0;
                update.before_z_mm = before[2] * 1000.0;
                update.before_angle_rad = Safe(delegate { return instance.Angle; }, double.NaN);
                update.before_scale = Safe(delegate { return instance.Scale; }, double.NaN);
                double targetX = before[0] < 0 ? -targetAbsXMm : targetAbsXMm;
                MathPoint targetPoint = Safe(delegate
                {
                    return math.CreatePoint(new[]
                    {
                        targetX / 1000.0, before[1], before[2]
                    }) as MathPoint;
                }, null);
                update.point_created = targetPoint != null;
                update.position_set = targetPoint != null && TryAction(delegate
                {
                    instance.InstancePosition = targetPoint;
                });
                MathPoint afterPoint = Safe(delegate { return instance.InstancePosition; }, null);
                double[] after = DoubleArray(afterPoint == null ? null :
                    Safe(delegate { return afterPoint.ArrayData as Array; }, null));
                if (after != null && after.Length >= 3)
                {
                    update.after_x_mm = after[0] * 1000.0;
                    update.after_y_mm = after[1] * 1000.0;
                    update.after_z_mm = after[2] * 1000.0;
                    update.after_angle_rad = Safe(delegate { return instance.Angle; }, double.NaN);
                    update.after_scale = Safe(delegate { return instance.Scale; }, double.NaN);
                    update.readback_pass = Near(Math.Abs(update.after_x_mm), targetAbsXMm,
                            DimensionToleranceMm) &&
                        Near(update.after_y_mm, update.before_y_mm, DimensionToleranceMm) &&
                        Near(update.after_z_mm, update.before_z_mm, DimensionToleranceMm) &&
                        Near(update.after_angle_rad, update.before_angle_rad, 1e-10) &&
                        Near(update.after_scale, update.before_scale, 1e-10);
                }
                if (update.position_set && update.readback_pass) count++;
                Release(beforePoint);
                Release(targetPoint);
                Release(afterPoint);
                Release(instance);
            }
            return count;
        }

        private static bool BaseHoleBlocksGate(List<BaseHoleBlockRow> rows, double absXMm)
        {
            if (rows == null || rows.Count != 4) return false;
            string[] names = rows.Select(delegate(BaseHoleBlockRow row) { return row.name; })
                .OrderBy(delegate(string name) { return name; }, StringComparer.Ordinal).ToArray();
            string[] expected = BaseHoleBlockNames.OrderBy(delegate(string name) { return name; },
                StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(expected, StringComparer.Ordinal)) return false;
            var expectedRows = new Dictionary<string, double[]>(StringComparer.Ordinal)
            {
                { "块-敲落孔φ45mm-1", new[] { -absXMm, 500.5 } },
                { "块-敲落孔φ45mm-3", new[] { -absXMm, 74.5 } },
                { "块-敲落孔φ45mm-4", new[] { absXMm, 500.5 } },
                { "块-敲落孔φ45mm-5", new[] { absXMm, 74.5 } }
            };
            return rows.TrueForAll(delegate(BaseHoleBlockRow row)
            {
                double[] target = expectedRows[row.name];
                return Near(row.x_mm, target[0], DimensionToleranceMm) &&
                    Near(row.y_mm, target[1], DimensionToleranceMm) &&
                    Near(row.z_mm, 0.0, DimensionToleranceMm) &&
                    Near(row.angle_rad, 4.71238898038469, 1e-10) &&
                    Near(row.scale, 1.0, 1e-10);
            });
        }

        private static bool SameNumericArray(double[] left, double[] right, double tolerance)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (Math.Abs(left[index] - right[index]) > tolerance) return false;
            return true;
        }

        private static void RunDerivedEdit(ISldWorks sw, string cadDir, Result result)
        {
            foreach (DerivedPlan plan in DerivedPlans(cadDir))
            {
                if (plan.native_right_topcover)
                {
                    RunNativeRightTopCoverEdit(sw, cadDir, result, plan);
                    continue;
                }
                if (plan.native_frame_crossbar_right)
                {
                    RunNativeFrameCrossbarRightEdit(sw, cadDir, result, plan);
                    continue;
                }
                if (plan.native_door_frame_right)
                {
                    RunNativeDoorFrameRightEdit(sw, cadDir, result, plan);
                    continue;
                }
                if (plan.native_cabinet_shelf_right)
                {
                    RunNativeCabinetShelfRightEdit(sw, cadDir, result, plan);
                    continue;
                }
                string path = ExactFile(cadDir, plan.file_name);
                string sourcePath = ExactFile(cadDir, plan.source_file_name);
                var record = new DerivedPartRecord
                {
                    file_name = plan.file_name,
                    path = path,
                    source_file_name = plan.source_file_name,
                    expected_source_path = sourcePath,
                    link_type = plan.link_type,
                    relock_required = plan.relock_required,
                    lock_after_refresh = plan.lock_after_refresh
                };
                result.derived_parts.Add(record);
                ModelDoc2 source = null;
                ModelDoc2 model = null;
                try
                {
                    int sourceErrors = 0, sourceWarnings = 0;
                    source = OpenDocument(sw, sourcePath, (int)swDocumentTypes_e.swDocPART, true,
                        ref sourceErrors, ref sourceWarnings);
                    record.source_open_errors = sourceErrors;
                    record.source_open_warnings = sourceWarnings;
                    Require(source != null, "DERIVED_SOURCE_OPEN_FAILED", 30,
                        "could not open exact local source " + sourcePath);
                    Require(sourceErrors == 0 && sourceWarnings == 0,
                        "DERIVED_SOURCE_OPEN_ERRORS_OR_WARNINGS", 30,
                        plan.source_file_name + " source opened with errors/warnings");

                    int errors = 0, warnings = 0;
                    model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, false, ref errors, ref warnings);
                    record.opened = model != null;
                    record.open_errors = errors;
                    record.open_warnings = warnings;
                    record.initial_open_warning_is_expected_needs_regen =
                        warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen &&
                        DerivedNeedsRegenWarningAllowlist().Contains(plan.file_name);
                    Require(model != null, "DERIVED_PART_OPEN_FAILED", 31, "could not open " + path);
                    Require(errors == 0 &&
                        (warnings == 0 || record.initial_open_warning_is_expected_needs_regen),
                        "DERIVED_PART_OPEN_ERRORS_OR_WARNINGS", 31,
                        plan.file_name + " opened with errors/warnings=" +
                        errors.ToString(CultureInfo.InvariantCulture) + "/" +
                        warnings.ToString(CultureInfo.InvariantCulture));
                    Try(delegate { model.ShowFeatureErrorDialog = false; });
                    record.before = CapturePartSnapshot(model);
                    record.bbox_before_source_or_target = DerivedBboxBeforeGate(record.before, plan);
                    Require(record.bbox_before_source_or_target,
                        "DERIVED_BBOX_PRECONDITION_DRIFT", 32,
                        plan.file_name + " X bbox is neither the audited 760W source nor 888W target");
                    record.links_before = CaptureLinkSnapshot(model, plan, cadDir);
                    Require(LinkGate(record.links_before, plan, false), "DERIVED_LOCAL_SOURCE_GATE_FAILED", 32,
                        plan.file_name + " does not point to the exact local source without Broken/Dangling links");

                    if (plan.relock_required)
                    {
                        Require(record.links_before.all_primary_locked,
                            "DOOR_FRAME_BOTTOM_NOT_LOCKED_BEFORE_REFRESH", 33,
                            "门框 下 must start with status Locked(1)");
                        record.unlock_attempted = true;
                        record.unlock_updates = UpdatePrimaryLinksDetailed(model, plan,
                            (int)swExternalFileReferencesUpdate_e.swExternalFileReferencesunlockAll,
                            "unlock_all");
                        record.unlock_succeeded = record.unlock_updates.success;
                        Require(record.unlock_succeeded, "DOOR_FRAME_BOTTOM_UNLOCK_FAILED", 33,
                            "门框 下 SplitBody could not be explicitly unlocked");
                        record.links_after_unlock = CaptureLinkSnapshot(model, plan, cadDir, false);
                        Require(record.links_after_unlock.primary_feature_count > 0 &&
                            record.links_after_unlock.locked_count == 0,
                            "DOOR_FRAME_BOTTOM_UNLOCK_STATUS_GATE_FAILED", 33,
                            "门框 下 remained Locked after unlockAll");
                    }

                    record.update_attempted = true;
                    record.update_none_updates = UpdatePrimaryLinksDetailed(model, plan,
                        (int)swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone,
                        "update_none");
                    record.update_succeeded = record.update_none_updates.success;
                    Require(record.update_succeeded, "DERIVED_UPDATE_NONE_FAILED", 34,
                        plan.file_name + " primary feature UpdateExternalFileReferences(UpdateNone) failed");
                    record.links_after_update_none = CaptureLinkSnapshot(model, plan, cadDir, false);
                    record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                    Require(record.rebuilt, "DERIVED_REBUILD_FAILED", 34,
                        "ForceRebuild3 failed for " + plan.file_name);

                    if (plan.lock_after_refresh)
                    {
                        record.relock_attempted = true;
                        record.relock_updates = UpdatePrimaryLinksDetailed(model, plan,
                            (int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll,
                            "lock_all");
                        record.relock_succeeded = record.relock_updates.success;
                        Require(record.relock_succeeded, "DERIVED_SPLITBODY_RELOCK_FAILED", 35,
                            plan.file_name + " SplitBody could not be locked after local refresh");
                        record.links_after_relock = CaptureLinkSnapshot(model, plan, cadDir, true);
                        Require(LinkGate(record.links_after_relock, plan, true),
                            "DERIVED_SPLITBODY_RELOCK_STATUS_GATE_FAILED", 35,
                            plan.file_name + " did not persist exact local Locked(1) references");
                        record.rebuilt_after_relock = Safe(delegate { return model.ForceRebuild3(false); }, false);
                        Require(record.rebuilt_after_relock, "DERIVED_SPLITBODY_RELOCK_REBUILD_FAILED", 35,
                            plan.file_name + " failed rebuild after locking local references");
                    }

                    record.after = CapturePartSnapshot(model);
                    record.links_after = CaptureLinkSnapshot(model, plan, cadDir,
                        plan.lock_after_refresh);
                    record.bbox_after_target = DerivedBboxTargetGate(record.after, plan, record.before);
                    Require(DerivedPartHealthGate(record.after, record.before) &&
                        LinkGate(record.links_after, plan, true) &&
                        record.bbox_after_target,
                        "DERIVED_POST_REBUILD_GATE_FAILED", 36,
                        plan.file_name + " failed exact-source/topology-preserved/error0/target-bbox gate");
                    SaveModel(model, record, result);
                    Require(record.saved && record.save_errors == 0 && record.save_warnings == 0 &&
                        !record.save_flag_after_save,
                        "DERIVED_SAVE_FAILED", 37, "save failed for " + plan.file_name);
                    record.edit_gate = true;
                }
                finally
                {
                    CloseDocument(sw, ref model);
                    CloseDocument(sw, ref source);
                }
                FileRecord file = FileRecordFor(result, path);
                file.sha_after_save = Sha256(path);
                record.sha_after_save = file.sha_after_save;
            }
        }

        private static void RunDerivedVerify(ISldWorks sw, string cadDir, Result result)
        {
            List<DerivedPlan> plans = DerivedPlans(cadDir);
            for (int index = 0; index < plans.Count; index++)
            {
                DerivedPlan plan = plans[index];
                DerivedPartRecord record = result.derived_parts[index];
                if (plan.native_right_topcover)
                {
                    RunNativeRightTopCoverVerify(sw, cadDir, result, plan, record);
                    continue;
                }
                if (plan.native_frame_crossbar_right)
                {
                    RunNativeFrameCrossbarRightVerify(sw, cadDir, result, plan, record);
                    continue;
                }
                if (plan.native_door_frame_right)
                {
                    RunNativeDoorFrameRightVerify(sw, cadDir, result, plan, record);
                    continue;
                }
                if (plan.native_cabinet_shelf_right)
                {
                    RunNativeCabinetShelfRightVerify(sw, cadDir, result, plan, record);
                    continue;
                }
                ModelDoc2 source = null;
                ModelDoc2 model = null;
                try
                {
                    int sourceErrors = 0, sourceWarnings = 0;
                    source = OpenDocument(sw, record.expected_source_path, (int)swDocumentTypes_e.swDocPART,
                        true, ref sourceErrors, ref sourceWarnings);
                    Require(source != null, "DERIVED_VERIFY_SOURCE_OPEN_FAILED", 38,
                        "could not reopen source " + record.expected_source_path);
                    Require(sourceErrors == 0 && sourceWarnings == 0,
                        "DERIVED_VERIFY_SOURCE_ERRORS_OR_WARNINGS", 38,
                        plan.source_file_name + " verify source opened with errors/warnings");
                    int errors = 0, warnings = 0;
                    model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART, true,
                        ref errors, ref warnings);
                    record.reopen_open_errors = errors;
                    record.reopen_open_warnings = warnings;
                    Require(model != null, "DERIVED_READONLY_REOPEN_FAILED", 39,
                        "could not reopen " + record.path);
                    Require(errors == 0 && warnings == 0,
                        "DERIVED_READONLY_REOPEN_ERRORS_OR_WARNINGS", 39,
                        plan.file_name + " reopened with errors/warnings");
                    record.reopen_rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                    record.reopen = CapturePartSnapshot(model);
                    record.links_reopen = CaptureLinkSnapshot(model, plan, cadDir,
                        plan.lock_after_refresh);
                    record.bbox_reopen_target = DerivedBboxTargetGate(record.reopen, plan, record.before);
                    Require(record.reopen_rebuilt &&
                        DerivedPartHealthGate(record.reopen, record.before) &&
                        LinkGate(record.links_reopen, plan, true) && record.bbox_reopen_target,
                        "DERIVED_READONLY_REOPEN_GATE_FAILED", 40,
                        plan.file_name + " failed new-session read-only reopen target-bbox gate");
                    record.verify_gate = true;
                }
                finally
                {
                    CloseDocument(sw, ref model);
                    CloseDocument(sw, ref source);
                }
                string hash = Sha256(record.path);
                FileRecord file = FileRecordFor(result, record.path);
                file.sha_after_readonly_reopen = hash;
                Require(string.Equals(file.sha_after_save, hash, StringComparison.OrdinalIgnoreCase),
                    "DERIVED_READONLY_REOPEN_CHANGED_HASH", 41,
                    "read-only reopen changed " + record.path);
            }
        }

        private static void RunNativeRightTopCoverEdit(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan)
        {
            string path = ExactFile(cadDir, plan.file_name);
            string sourcePath = ExactFile(cadDir, plan.source_file_name);
            var record = new DerivedPartRecord
            {
                file_name = plan.file_name,
                path = path,
                source_file_name = plan.source_file_name,
                expected_source_path = sourcePath,
                link_type = plan.link_type,
                relock_required = false,
                native_right_route = true,
                native_right_route_id = NativeRightTopCoverRoute
            };
            result.derived_parts.Add(record);
            ModelDoc2 prior = null;
            ModelDoc2 model = null;
            try
            {
                ValidateNativeRightEvidenceInputs(result.repository_root, record);
                int priorErrors = 0, priorWarnings = 0;
                prior = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, true,
                    ref priorErrors, ref priorWarnings);
                record.opened = prior != null;
                record.open_errors = priorErrors;
                record.open_warnings = priorWarnings;
                Require(prior != null && priorErrors == 0 && priorWarnings == 0,
                    "NATIVE_RIGHT_LEGACY_OPEN_FAILED", 31,
                    "could not read the pre-replacement right top-cover part");
                record.before = CapturePartSnapshot(prior);
                record.bbox_before_source_or_target = DerivedBboxBeforeGate(record.before, plan);
                Require(record.bbox_before_source_or_target,
                    "NATIVE_RIGHT_LEGACY_BBOX_PRECONDITION_DRIFT", 32,
                    "right top-cover X bbox is neither the audited 760W source nor 888W target");
                DerivedPlan legacyMirror = LegacyRightTopCoverMirrorPlan(plan);
                record.links_before = CaptureLinkSnapshot(prior, legacyMirror, cadDir);
                record.native_right_legacy_mirror_precondition =
                    LinkGate(record.links_before, legacyMirror, false);
                Require(record.native_right_legacy_mirror_precondition,
                    "NATIVE_RIGHT_LEGACY_MIRROR_PRECONDITION_FAILED", 32,
                    "right top-cover must start as the exact local left-panel MirrorStock route");
                CloseDocument(sw, ref prior);

                FileRecord backup = FileRecordFor(result, path);
                record.native_right_backup_precondition = backup.backup_created &&
                    File.Exists(backup.backup_path) && !HasReparsePoint(backup.backup_path) &&
                    FileLinkCount(backup.backup_path) == 1 &&
                    string.Equals(Sha256(backup.backup_path), backup.sha_before,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsUnder(path, cadDir) && File.Exists(path) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1;
                Require(record.native_right_backup_precondition,
                    "NATIVE_RIGHT_REPLACE_BACKUP_PRECONDITION_FAILED", 32,
                    "right top-cover replacement requires its exact full-pack backup and safe target path");
                AssertAuthorizationStillValid(result, "before_native_right_replace");
                File.Delete(path);
                Require(!File.Exists(path), "NATIVE_RIGHT_REPLACE_UNLINK_FAILED", 32,
                    "could not remove the closed MirrorStock right top-cover path");
                record.native_right_replaced = true;

                string template = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                Require(File.Exists(template) && !HasReparsePoint(template) &&
                    FileLinkCount(template) == 1, "NATIVE_RIGHT_PART_TEMPLATE_INVALID", 32,
                    "default SolidWorks part template is not an isolated regular file");
                model = sw.NewDocument(template, 0, 0, 0) as ModelDoc2;
                Require(model != null, "NATIVE_RIGHT_NEW_PART_FAILED", 32,
                    "could not create the replacement native right top-cover part");
                BuildNativeRight888SharpInsertBends(sw, model, record);
                record.after = CapturePartSnapshot(model);
                record.bbox_after_target = DerivedBboxTargetGate(record.after, plan, record.before);
                record.native_right_geometry = CaptureNativeRightGeometry(model);
                record.native_right_features = CaptureNativeRightFeatures(model);
                record.native_right_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_right_geometry_gate =
                    NativeRightGeometryMatchesContract(record.native_right_geometry);
                record.native_right_feature_gate =
                    NativeRightFeatureMatchesContract(record.native_right_features);
                Require(record.bbox_after_target && record.native_right_geometry_gate &&
                    record.native_right_feature_gate &&
                    record.native_right_external_reference_count == 0,
                    "NATIVE_RIGHT_FINAL_REGRESSION_FAILED", 36,
                    "replacement right top-cover failed full native BRep, feature, or zero-external-reference gate");

                int errors = 0, warnings = 0;
                AssertAuthorizationStillValid(result, "before_native_right_save");
                record.save_attempted = true;
                record.saved = model.Extension.SaveAs3(path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null,
                    ref errors, ref warnings);
                record.save_errors = errors;
                record.save_warnings = warnings;
                record.save_flag_after_save = model.GetSaveFlag();
                Require(record.saved && errors == 0 && warnings == 0 &&
                    !record.save_flag_after_save, "NATIVE_RIGHT_SAVE_FAILED", 37,
                    "could not save the native replacement right top-cover part");
                record.edit_gate = true;
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseDocument(sw, ref prior);
            }
            FileRecord file = FileRecordFor(result, path);
            file.sha_after_save = Sha256(path);
            record.sha_after_save = file.sha_after_save;
        }

        private static void RunNativeRightTopCoverVerify(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan, DerivedPartRecord record)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART,
                    true, ref errors, ref warnings);
                record.reopen_open_errors = errors;
                record.reopen_open_warnings = warnings;
                Require(model != null && errors == 0 && warnings == 0 &&
                    model.IsOpenedReadOnly(), "NATIVE_RIGHT_READONLY_REOPEN_FAILED", 39,
                    "could not open the native replacement right top-cover as a new read-only document");
                record.reopen_rebuilt = model.ForceRebuild3(false);
                record.reopen = CapturePartSnapshot(model);
                record.bbox_reopen_target = DerivedBboxTargetGate(record.reopen, plan,
                    record.before);
                record.native_right_reopen_geometry = CaptureNativeRightGeometry(model);
                record.native_right_reopen_features = CaptureNativeRightFeatures(model);
                record.native_right_reopen_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_right_reopen_geometry_gate =
                    NativeRightGeometryMatchesContract(record.native_right_reopen_geometry);
                record.native_right_reopen_feature_gate =
                    NativeRightFeatureMatchesContract(record.native_right_reopen_features);
                Require(record.reopen_rebuilt && record.bbox_reopen_target &&
                    record.native_right_reopen_geometry_gate &&
                    record.native_right_reopen_feature_gate &&
                    record.native_right_reopen_external_reference_count == 0,
                    "NATIVE_RIGHT_READONLY_REOPEN_REGRESSION_FAILED", 40,
                    "read-only reopen failed full native right top-cover geometry, feature, or external-reference gate");
                record.verify_gate = true;
            }
            finally { CloseDocument(sw, ref model); }
            string hash = Sha256(record.path);
            FileRecord file = FileRecordFor(result, record.path);
            file.sha_after_readonly_reopen = hash;
            Require(string.Equals(file.sha_after_save, hash,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_RIGHT_READONLY_REOPEN_CHANGED_HASH", 41,
                "read-only reopen changed the native replacement right top-cover file");
        }

        private static void RunNativeFrameCrossbarRightEdit(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan)
        {
            string path = ExactFile(cadDir, plan.file_name);
            string sourcePath = ExactFile(cadDir, plan.source_file_name);
            var record = new DerivedPartRecord
            {
                file_name = plan.file_name,
                path = path,
                source_file_name = plan.source_file_name,
                expected_source_path = sourcePath,
                link_type = plan.link_type,
                relock_required = false,
                native_frame_crossbar_route = true,
                native_frame_crossbar_route_id = NativeFrameCrossbarRightRoute
            };
            result.derived_parts.Add(record);
            ModelDoc2 prior = null;
            ModelDoc2 model = null;
            try
            {
                ValidateNativeFrameCrossbarEvidenceInputs(result.repository_root, record);
                int priorErrors = 0, priorWarnings = 0;
                prior = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, true,
                    ref priorErrors, ref priorWarnings);
                record.opened = prior != null;
                record.open_errors = priorErrors;
                record.open_warnings = priorWarnings;
                record.initial_open_warning_is_expected_needs_regen =
                    priorWarnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen &&
                    DerivedNeedsRegenWarningAllowlist().Contains(plan.file_name);
                Require(prior != null && priorErrors == 0 &&
                    (priorWarnings == 0 || record.initial_open_warning_is_expected_needs_regen),
                    "NATIVE_FRAME_CROSSBAR_LEGACY_OPEN_FAILED", 31,
                    "could not read the pre-replacement right frame crossbar");
                record.before = CapturePartSnapshot(prior);
                record.bbox_before_source_or_target = DerivedBboxBeforeGate(record.before, plan);
                Require(record.bbox_before_source_or_target,
                    "NATIVE_FRAME_CROSSBAR_LEGACY_BBOX_PRECONDITION_DRIFT", 32,
                    "right frame crossbar X bbox is neither the audited 760W source nor 888W target");
                DerivedPlan legacyMirror = LegacyNativeFrameCrossbarMirrorPlan(plan);
                record.links_before = CaptureLinkSnapshot(prior, legacyMirror, cadDir);
                record.native_frame_crossbar_legacy_mirror_precondition =
                    LinkGate(record.links_before, legacyMirror, false);
                Require(record.native_frame_crossbar_legacy_mirror_precondition,
                    "NATIVE_FRAME_CROSSBAR_LEGACY_MIRROR_PRECONDITION_FAILED", 32,
                    "right frame crossbar must start as the exact local MirrorStock route");
                CloseDocument(sw, ref prior);

                FileRecord backup = FileRecordFor(result, path);
                record.native_frame_crossbar_backup_precondition = backup.backup_created &&
                    File.Exists(backup.backup_path) && !HasReparsePoint(backup.backup_path) &&
                    FileLinkCount(backup.backup_path) == 1 &&
                    string.Equals(Sha256(backup.backup_path), backup.sha_before,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsUnder(path, cadDir) && File.Exists(path) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1;
                Require(record.native_frame_crossbar_backup_precondition,
                    "NATIVE_FRAME_CROSSBAR_REPLACE_BACKUP_PRECONDITION_FAILED", 32,
                    "right frame crossbar replacement requires its exact full-pack backup");
                AssertAuthorizationStillValid(result, "before_native_frame_crossbar_replace");
                File.Delete(path);
                Require(!File.Exists(path), "NATIVE_FRAME_CROSSBAR_REPLACE_UNLINK_FAILED", 32,
                    "could not remove the closed MirrorStock right frame crossbar path");
                record.native_frame_crossbar_replaced = true;

                string template = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                Require(File.Exists(template) && !HasReparsePoint(template) &&
                    FileLinkCount(template) == 1, "NATIVE_FRAME_CROSSBAR_PART_TEMPLATE_INVALID", 32,
                    "default SolidWorks part template is not an isolated regular file");
                model = sw.NewDocument(template, 0, 0, 0) as ModelDoc2;
                Require(model != null, "NATIVE_FRAME_CROSSBAR_NEW_PART_FAILED", 32,
                    "could not create the replacement native right frame crossbar");
                BuildNativeFrameCrossbarRight888(sw, model);
                record.after = CapturePartSnapshot(model);
                record.bbox_after_target = DerivedBboxTargetGate(record.after, plan, record.before);
                record.native_frame_crossbar_geometry = CaptureNativeRightGeometry(model);
                record.native_frame_crossbar_features = CaptureNativeRightFeatures(model);
                record.native_frame_crossbar_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_frame_crossbar_geometry_gate =
                    NativeFrameCrossbarGeometryMatchesContract(
                        record.native_frame_crossbar_geometry);
                record.native_frame_crossbar_feature_gate =
                    NativeFrameCrossbarFeatureMatchesContract(
                        record.native_frame_crossbar_features);
                Require(record.bbox_after_target &&
                    record.native_frame_crossbar_geometry_gate &&
                    record.native_frame_crossbar_feature_gate &&
                    record.native_frame_crossbar_external_reference_count == 0,
                    "NATIVE_FRAME_CROSSBAR_FINAL_REGRESSION_FAILED", 36,
                    "replacement frame crossbar failed native BRep, feature, or zero-reference gate");

                int errors = 0, warnings = 0;
                AssertAuthorizationStillValid(result, "before_native_frame_crossbar_save");
                record.save_attempted = true;
                record.saved = model.Extension.SaveAs3(path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null,
                    ref errors, ref warnings);
                record.save_errors = errors;
                record.save_warnings = warnings;
                record.save_flag_after_save = model.GetSaveFlag();
                Require(record.saved && errors == 0 && warnings == 0 &&
                    !record.save_flag_after_save, "NATIVE_FRAME_CROSSBAR_SAVE_FAILED", 37,
                    "could not save the native replacement right frame crossbar");
                record.edit_gate = true;
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseDocument(sw, ref prior);
            }
            FileRecord file = FileRecordFor(result, path);
            file.sha_after_save = Sha256(path);
            record.sha_after_save = file.sha_after_save;
        }

        private static void RunNativeFrameCrossbarRightVerify(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan, DerivedPartRecord record)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART,
                    true, ref errors, ref warnings);
                record.reopen_open_errors = errors;
                record.reopen_open_warnings = warnings;
                Require(model != null && errors == 0 && warnings == 0 &&
                    model.IsOpenedReadOnly(), "NATIVE_FRAME_CROSSBAR_READONLY_REOPEN_FAILED", 39,
                    "could not reopen the native frame crossbar as a new read-only document");
                record.reopen_rebuilt = model.ForceRebuild3(false);
                record.reopen = CapturePartSnapshot(model);
                record.bbox_reopen_target = DerivedBboxTargetGate(record.reopen, plan,
                    record.before);
                record.native_frame_crossbar_reopen_geometry =
                    CaptureNativeRightGeometry(model);
                record.native_frame_crossbar_reopen_features =
                    CaptureNativeRightFeatures(model);
                record.native_frame_crossbar_reopen_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_frame_crossbar_reopen_geometry_gate =
                    NativeFrameCrossbarGeometryMatchesContract(
                        record.native_frame_crossbar_reopen_geometry);
                record.native_frame_crossbar_reopen_feature_gate =
                    NativeFrameCrossbarFeatureMatchesContract(
                        record.native_frame_crossbar_reopen_features);
                Require(record.reopen_rebuilt && record.bbox_reopen_target &&
                    record.native_frame_crossbar_reopen_geometry_gate &&
                    record.native_frame_crossbar_reopen_feature_gate &&
                    record.native_frame_crossbar_reopen_external_reference_count == 0,
                    "NATIVE_FRAME_CROSSBAR_READONLY_REOPEN_REGRESSION_FAILED", 40,
                    "read-only reopen failed native frame-crossbar geometry, feature, or reference gate");
                record.verify_gate = true;
            }
            finally { CloseDocument(sw, ref model); }
            string hash = Sha256(record.path);
            FileRecord file = FileRecordFor(result, record.path);
            file.sha_after_readonly_reopen = hash;
            Require(string.Equals(file.sha_after_save, hash,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_FRAME_CROSSBAR_READONLY_REOPEN_CHANGED_HASH", 41,
                "read-only reopen changed the native right frame crossbar file");
        }

        private static void RunNativeDoorFrameRightEdit(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan)
        {
            string path = ExactFile(cadDir, plan.file_name);
            string sourcePath = ExactFile(cadDir, plan.source_file_name);
            var record = new DerivedPartRecord
            {
                file_name = plan.file_name,
                path = path,
                source_file_name = plan.source_file_name,
                expected_source_path = sourcePath,
                link_type = plan.link_type,
                relock_required = false,
                native_door_frame_right_route = true,
                native_door_frame_right_route_id = NativeDoorFrameRightRoute
            };
            result.derived_parts.Add(record);
            ModelDoc2 prior = null;
            ModelDoc2 model = null;
            try
            {
                ValidateNativeDoorFrameRightEvidenceInputs(result.repository_root, record);
                int priorErrors = 0, priorWarnings = 0;
                prior = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, true,
                    ref priorErrors, ref priorWarnings);
                record.opened = prior != null;
                record.open_errors = priorErrors;
                record.open_warnings = priorWarnings;
                record.initial_open_warning_is_expected_needs_regen =
                    priorWarnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen &&
                    DerivedNeedsRegenWarningAllowlist().Contains(plan.file_name);
                Require(prior != null && priorErrors == 0 &&
                    (priorWarnings == 0 || record.initial_open_warning_is_expected_needs_regen),
                    "NATIVE_DOOR_FRAME_RIGHT_LEGACY_OPEN_FAILED", 31,
                    "could not read the pre-replacement right door-frame part");
                record.before = CapturePartSnapshot(prior);
                record.bbox_before_source_or_target = DerivedBboxBeforeGate(record.before, plan);
                Require(record.bbox_before_source_or_target,
                    "NATIVE_DOOR_FRAME_RIGHT_LEGACY_BBOX_PRECONDITION_DRIFT", 32,
                    "right door frame X bbox is neither the audited 760W source nor 888W target");
                DerivedPlan legacyMirror = LegacyNativeDoorFrameRightMirrorPlan(plan);
                record.links_before = CaptureLinkSnapshot(prior, legacyMirror, cadDir);
                record.native_door_frame_right_legacy_mirror_precondition =
                    LinkGate(record.links_before, legacyMirror, false);
                Require(record.native_door_frame_right_legacy_mirror_precondition,
                    "NATIVE_DOOR_FRAME_RIGHT_LEGACY_MIRROR_PRECONDITION_FAILED", 32,
                    "right door frame must start as the exact local MirrorStock route");
                CloseDocument(sw, ref prior);

                FileRecord backup = FileRecordFor(result, path);
                record.native_door_frame_right_backup_precondition = backup.backup_created &&
                    File.Exists(backup.backup_path) && !HasReparsePoint(backup.backup_path) &&
                    FileLinkCount(backup.backup_path) == 1 &&
                    string.Equals(Sha256(backup.backup_path), backup.sha_before,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsUnder(path, cadDir) && File.Exists(path) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1;
                Require(record.native_door_frame_right_backup_precondition,
                    "NATIVE_DOOR_FRAME_RIGHT_REPLACE_BACKUP_PRECONDITION_FAILED", 32,
                    "right door-frame replacement requires its exact full-pack backup");
                AssertAuthorizationStillValid(result, "before_native_door_frame_right_replace");
                File.Delete(path);
                Require(!File.Exists(path), "NATIVE_DOOR_FRAME_RIGHT_REPLACE_UNLINK_FAILED", 32,
                    "could not remove the closed MirrorStock right door-frame path");
                record.native_door_frame_right_replaced = true;

                string template = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                Require(File.Exists(template) && !HasReparsePoint(template) &&
                    FileLinkCount(template) == 1, "NATIVE_DOOR_FRAME_RIGHT_PART_TEMPLATE_INVALID", 32,
                    "default SolidWorks part template is not an isolated regular file");
                model = sw.NewDocument(template, 0, 0, 0) as ModelDoc2;
                Require(model != null, "NATIVE_DOOR_FRAME_RIGHT_NEW_PART_FAILED", 32,
                    "could not create the native right door-frame replacement");
                BuildNativeDoorFrameRight888(sw, model);
                record.after = CapturePartSnapshot(model);
                record.bbox_after_target = DerivedBboxTargetGate(record.after, plan, record.before);
                record.native_door_frame_right_geometry = CaptureNativeRightGeometry(model);
                record.native_door_frame_right_features = CaptureNativeRightFeatures(model);
                record.native_door_frame_right_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_door_frame_right_geometry_gate =
                    NativeDoorFrameRightGeometryMatchesContract(
                        record.native_door_frame_right_geometry);
                record.native_door_frame_right_feature_gate =
                    NativeDoorFrameRightFeatureMatchesContract(
                        record.native_door_frame_right_features);
                Require(record.bbox_after_target &&
                    record.native_door_frame_right_geometry_gate &&
                    record.native_door_frame_right_feature_gate &&
                    record.native_door_frame_right_external_reference_count == 0,
                    "NATIVE_DOOR_FRAME_RIGHT_FINAL_REGRESSION_FAILED", 36,
                    "right door-frame replacement failed native BRep, feature, or zero-reference gate");

                int errors = 0, warnings = 0;
                AssertAuthorizationStillValid(result, "before_native_door_frame_right_save");
                record.save_attempted = true;
                record.saved = model.Extension.SaveAs3(path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null,
                    ref errors, ref warnings);
                record.save_errors = errors;
                record.save_warnings = warnings;
                record.save_flag_after_save = model.GetSaveFlag();
                Require(record.saved && errors == 0 && warnings == 0 &&
                    !record.save_flag_after_save, "NATIVE_DOOR_FRAME_RIGHT_SAVE_FAILED", 37,
                    "could not save the native replacement right door-frame part");
                record.edit_gate = true;
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseDocument(sw, ref prior);
            }
            FileRecord file = FileRecordFor(result, path);
            file.sha_after_save = Sha256(path);
            record.sha_after_save = file.sha_after_save;
        }

        private static void RunNativeDoorFrameRightVerify(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan, DerivedPartRecord record)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART,
                    true, ref errors, ref warnings);
                record.reopen_open_errors = errors;
                record.reopen_open_warnings = warnings;
                Require(model != null && errors == 0 && warnings == 0 &&
                    model.IsOpenedReadOnly(), "NATIVE_DOOR_FRAME_RIGHT_READONLY_REOPEN_FAILED", 39,
                    "could not reopen the native right door frame as a new read-only document");
                record.reopen_rebuilt = model.ForceRebuild3(false);
                record.reopen = CapturePartSnapshot(model);
                record.bbox_reopen_target = DerivedBboxTargetGate(record.reopen, plan,
                    record.before);
                record.native_door_frame_right_reopen_geometry =
                    CaptureNativeRightGeometry(model);
                record.native_door_frame_right_reopen_features =
                    CaptureNativeRightFeatures(model);
                record.native_door_frame_right_reopen_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_door_frame_right_reopen_geometry_gate =
                    NativeDoorFrameRightGeometryMatchesContract(
                        record.native_door_frame_right_reopen_geometry);
                record.native_door_frame_right_reopen_feature_gate =
                    NativeDoorFrameRightFeatureMatchesContract(
                        record.native_door_frame_right_reopen_features);
                Require(record.reopen_rebuilt && record.bbox_reopen_target &&
                    record.native_door_frame_right_reopen_geometry_gate &&
                    record.native_door_frame_right_reopen_feature_gate &&
                    record.native_door_frame_right_reopen_external_reference_count == 0,
                    "NATIVE_DOOR_FRAME_RIGHT_READONLY_REOPEN_REGRESSION_FAILED", 40,
                    "read-only reopen failed native right door-frame geometry, feature, or reference gate");
                record.verify_gate = true;
            }
            finally { CloseDocument(sw, ref model); }
            string hash = Sha256(record.path);
            FileRecord file = FileRecordFor(result, record.path);
            file.sha_after_readonly_reopen = hash;
            Require(string.Equals(file.sha_after_save, hash,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_DOOR_FRAME_RIGHT_READONLY_REOPEN_CHANGED_HASH", 41,
                "read-only reopen changed the native right door-frame file");
        }

        private static void RunNativeCabinetShelfRightEdit(ISldWorks sw, string cadDir,
            Result result, DerivedPlan plan)
        {
            string path = ExactFile(cadDir, plan.file_name);
            string sourcePath = ExactFile(cadDir, plan.source_file_name);
            var record = new DerivedPartRecord
            {
                file_name = plan.file_name,
                path = path,
                source_file_name = plan.source_file_name,
                expected_source_path = sourcePath,
                link_type = plan.link_type,
                relock_required = false,
                native_cabinet_shelf_right_route = true,
                native_cabinet_shelf_right_route_id = NativeCabinetShelfRightRoute
            };
            result.derived_parts.Add(record);
            ModelDoc2 prior = null;
            ModelDoc2 model = null;
            try
            {
                ValidateNativeCabinetShelfRightEvidenceInputs(result.repository_root, record);
                int priorErrors = 0, priorWarnings = 0;
                prior = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, true,
                    ref priorErrors, ref priorWarnings);
                record.opened = prior != null;
                record.open_errors = priorErrors;
                record.open_warnings = priorWarnings;
                record.initial_open_warning_is_expected_needs_regen =
                    priorWarnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen &&
                    DerivedNeedsRegenWarningAllowlist().Contains(plan.file_name);
                Require(prior != null && priorErrors == 0 &&
                    (priorWarnings == 0 || record.initial_open_warning_is_expected_needs_regen),
                    "NATIVE_CABINET_SHELF_RIGHT_LEGACY_OPEN_FAILED", 31,
                    "could not read the pre-replacement right cabinet shelf");
                record.before = CapturePartSnapshot(prior);
                record.bbox_before_source_or_target = DerivedBboxBeforeGate(record.before, plan);
                Require(record.bbox_before_source_or_target,
                    "NATIVE_CABINET_SHELF_RIGHT_LEGACY_BBOX_PRECONDITION_DRIFT", 32,
                    "right cabinet shelf X bbox is neither the audited 760W source nor 888W target");
                DerivedPlan legacyMirror = LegacyNativeCabinetShelfRightMirrorPlan(plan);
                record.links_before = CaptureLinkSnapshot(prior, legacyMirror, cadDir);
                record.native_cabinet_shelf_right_legacy_mirror_precondition =
                    LinkGate(record.links_before, legacyMirror, false);
                Require(record.native_cabinet_shelf_right_legacy_mirror_precondition,
                    "NATIVE_CABINET_SHELF_RIGHT_LEGACY_MIRROR_PRECONDITION_FAILED", 32,
                    "right cabinet shelf must start as the exact local MirrorStock route");
                CloseDocument(sw, ref prior);

                FileRecord backup = FileRecordFor(result, path);
                record.native_cabinet_shelf_right_backup_precondition = backup.backup_created &&
                    File.Exists(backup.backup_path) && !HasReparsePoint(backup.backup_path) &&
                    FileLinkCount(backup.backup_path) == 1 &&
                    string.Equals(Sha256(backup.backup_path), backup.sha_before,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsUnder(path, cadDir) && File.Exists(path) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1;
                Require(record.native_cabinet_shelf_right_backup_precondition,
                    "NATIVE_CABINET_SHELF_RIGHT_REPLACE_BACKUP_PRECONDITION_FAILED", 32,
                    "right cabinet-shelf replacement requires its exact full-pack backup");
                AssertAuthorizationStillValid(result,
                    "before_native_cabinet_shelf_right_replace");
                File.Delete(path);
                Require(!File.Exists(path),
                    "NATIVE_CABINET_SHELF_RIGHT_REPLACE_UNLINK_FAILED", 32,
                    "could not remove the closed MirrorStock right cabinet-shelf path");
                record.native_cabinet_shelf_right_replaced = true;

                string template = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                Require(File.Exists(template) && !HasReparsePoint(template) &&
                    FileLinkCount(template) == 1,
                    "NATIVE_CABINET_SHELF_RIGHT_PART_TEMPLATE_INVALID", 32,
                    "default SolidWorks part template is not an isolated regular file");
                model = sw.NewDocument(template, 0, 0, 0) as ModelDoc2;
                Require(model != null, "NATIVE_CABINET_SHELF_RIGHT_NEW_PART_FAILED", 32,
                    "could not create the native right cabinet-shelf replacement");
                BuildNativeCabinetShelfRight888(sw, model);
                record.after = CapturePartSnapshot(model);
                record.bbox_after_target = DerivedBboxTargetGate(record.after, plan, record.before);
                record.native_cabinet_shelf_right_geometry = CaptureNativeRightGeometry(model);
                record.native_cabinet_shelf_right_features = CaptureNativeRightFeatures(model);
                record.native_cabinet_shelf_right_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_cabinet_shelf_right_geometry_gate =
                    NativeCabinetShelfRightGeometryMatchesContract(
                        record.native_cabinet_shelf_right_geometry);
                record.native_cabinet_shelf_right_feature_gate =
                    NativeCabinetShelfRightFeatureMatchesContract(
                        record.native_cabinet_shelf_right_features);
                Require(record.bbox_after_target &&
                    record.native_cabinet_shelf_right_geometry_gate &&
                    record.native_cabinet_shelf_right_feature_gate &&
                    record.native_cabinet_shelf_right_external_reference_count == 0,
                    "NATIVE_CABINET_SHELF_RIGHT_FINAL_REGRESSION_FAILED", 36,
                    "right cabinet-shelf replacement failed native BRep, feature, or zero-reference gate");

                int errors = 0, warnings = 0;
                AssertAuthorizationStillValid(result,
                    "before_native_cabinet_shelf_right_save");
                record.save_attempted = true;
                record.saved = model.Extension.SaveAs3(path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null,
                    ref errors, ref warnings);
                record.save_errors = errors;
                record.save_warnings = warnings;
                record.save_flag_after_save = model.GetSaveFlag();
                Require(record.saved && errors == 0 && warnings == 0 &&
                    !record.save_flag_after_save,
                    "NATIVE_CABINET_SHELF_RIGHT_SAVE_FAILED", 37,
                    "could not save the native replacement right cabinet shelf");
                record.edit_gate = true;
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseDocument(sw, ref prior);
            }
            FileRecord file = FileRecordFor(result, path);
            file.sha_after_save = Sha256(path);
            record.sha_after_save = file.sha_after_save;
        }

        private static void RunNativeCabinetShelfRightVerify(ISldWorks sw,
            string cadDir, Result result, DerivedPlan plan, DerivedPartRecord record)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART,
                    true, ref errors, ref warnings);
                record.reopen_open_errors = errors;
                record.reopen_open_warnings = warnings;
                Require(model != null && errors == 0 && warnings == 0 &&
                    model.IsOpenedReadOnly(),
                    "NATIVE_CABINET_SHELF_RIGHT_READONLY_REOPEN_FAILED", 39,
                    "could not reopen the native right cabinet shelf as a new read-only document");
                record.reopen_rebuilt = model.ForceRebuild3(false);
                record.reopen = CapturePartSnapshot(model);
                record.bbox_reopen_target = DerivedBboxTargetGate(record.reopen,
                    plan, record.before);
                record.native_cabinet_shelf_right_reopen_geometry =
                    CaptureNativeRightGeometry(model);
                record.native_cabinet_shelf_right_reopen_features =
                    CaptureNativeRightFeatures(model);
                record.native_cabinet_shelf_right_reopen_external_reference_count =
                    model.Extension.ListExternalFileReferencesCount();
                record.native_cabinet_shelf_right_reopen_geometry_gate =
                    NativeCabinetShelfRightGeometryMatchesContract(
                        record.native_cabinet_shelf_right_reopen_geometry);
                record.native_cabinet_shelf_right_reopen_feature_gate =
                    NativeCabinetShelfRightFeatureMatchesContract(
                        record.native_cabinet_shelf_right_reopen_features);
                Require(record.reopen_rebuilt && record.bbox_reopen_target &&
                    record.native_cabinet_shelf_right_reopen_geometry_gate &&
                    record.native_cabinet_shelf_right_reopen_feature_gate &&
                    record.native_cabinet_shelf_right_reopen_external_reference_count == 0,
                    "NATIVE_CABINET_SHELF_RIGHT_READONLY_REOPEN_REGRESSION_FAILED", 40,
                    "read-only reopen failed native right cabinet-shelf geometry, feature, or reference gate");
                record.verify_gate = true;
            }
            finally { CloseDocument(sw, ref model); }
            string hash = Sha256(record.path);
            FileRecord file = FileRecordFor(result, record.path);
            file.sha_after_readonly_reopen = hash;
            Require(string.Equals(file.sha_after_save, hash,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_CABINET_SHELF_RIGHT_READONLY_REOPEN_CHANGED_HASH", 41,
                "read-only reopen changed the native right cabinet-shelf file");
        }

        private static DerivedPlan LegacyNativeCabinetShelfRightMirrorPlan(
            DerivedPlan nativePlan)
        {
            return new DerivedPlan
            {
                file_name = nativePlan.file_name,
                source_file_name = nativePlan.source_file_name,
                link_type = "MirrorStock",
                source_xmin_mm = nativePlan.source_xmin_mm,
                source_xmax_mm = nativePlan.source_xmax_mm,
                target_xmin_mm = nativePlan.target_xmin_mm,
                target_xmax_mm = nativePlan.target_xmax_mm
            };
        }

        private static DerivedPlan LegacyNativeDoorFrameRightMirrorPlan(
            DerivedPlan nativePlan)
        {
            return new DerivedPlan
            {
                file_name = nativePlan.file_name,
                source_file_name = nativePlan.source_file_name,
                link_type = "MirrorStock",
                source_xmin_mm = nativePlan.source_xmin_mm,
                source_xmax_mm = nativePlan.source_xmax_mm,
                target_xmin_mm = nativePlan.target_xmin_mm,
                target_xmax_mm = nativePlan.target_xmax_mm
            };
        }

        private static void ValidateNativeDoorFrameRightEvidenceInputs(
            string repositoryRoot, DerivedPartRecord record)
        {
            string pilotEvidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeDoorFrameRightPilotEvidenceRelativePath);
            string booleanDiffPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeDoorFrameRightBooleanDiffRelativePath);
            string reopenEvidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeDoorFrameRightReopenEvidenceRelativePath);
            string pilotPartPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeDoorFrameRightPilotPartRelativePath);
            string referencePartPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeDoorFrameRightReferencePartRelativePath);
            Require(!string.IsNullOrWhiteSpace(pilotEvidencePath) &&
                string.Equals(Sha256(pilotEvidencePath),
                    NativeDoorFrameRightPilotEvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(booleanDiffPath) &&
                string.Equals(Sha256(booleanDiffPath),
                    NativeDoorFrameRightBooleanDiffSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(reopenEvidencePath) &&
                string.Equals(Sha256(reopenEvidencePath),
                    NativeDoorFrameRightReopenEvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pilotPartPath) &&
                string.Equals(Sha256(pilotPartPath),
                    NativeDoorFrameRightPilotPartSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(referencePartPath) &&
                string.Equals(Sha256(referencePartPath),
                    NativeDoorFrameRightReferencePartSha256,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_DOOR_FRAME_RIGHT_PILOT_INPUT_HASH_INVALID", 32,
                "fixed door-frame pilot, Boolean-diff, reopen, or reference input changed");
            Dictionary<string, object> pilot = ReadJsonObject(pilotEvidencePath);
            Dictionary<string, object> difference = ReadJsonObject(booleanDiffPath);
            Dictionary<string, object> reopen = ReadJsonObject(reopenEvidencePath);
            Dictionary<string, object> referenceMinus = ChildObject(difference,
                "reference_minus_candidate");
            Dictionary<string, object> candidateMinus = ChildObject(difference,
                "candidate_minus_reference");
            Dictionary<string, object> pilotGeometry = ChildObject(pilot,
                "folded_geometry");
            Dictionary<string, object> pilotFeatures = ChildObject(pilot,
                "folded_features");
            Dictionary<string, object> reopenGeometry = ChildObject(reopen,
                "geometry");
            Dictionary<string, object> reopenFeatures = ChildObject(reopen,
                "features");
            Require(BoolValue(pilot, "success") &&
                SamePath(TextValue(pilot, "part_path"), pilotPartPath) &&
                ExactNumber(pilot, "external_reference_count", 0) &&
                ExactNumber(pilot, "save_errors", 0) &&
                ExactNumber(pilot, "save_warnings", 0) &&
                string.Equals(TextValue(pilotGeometry,
                    "translationNormalizedDetailedSignature"),
                    NativeDoorFrameRightDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactNumber(pilotFeatures, "mirrorPartCount", 0) &&
                ExactNumber(pilotFeatures, "oneBendCount", 4) &&
                ExactNumber(pilotFeatures, "forbiddenImportFeatureCount", 0) &&
                BoolValue(pilotFeatures, "pass") &&
                BoolValue(difference, "success") &&
                SamePath(TextValue(difference, "reference_path"), referencePartPath) &&
                SamePath(TextValue(difference, "candidate_path"), pilotPartPath) &&
                ExactNumber(referenceMinus, "error", 0) &&
                ExactNumber(referenceMinus, "body_count", 0) &&
                ExactNumber(referenceMinus, "total_volume_mm3", 0) &&
                ExactNumber(candidateMinus, "error", 0) &&
                ExactNumber(candidateMinus, "body_count", 0) &&
                ExactNumber(candidateMinus, "total_volume_mm3", 0) &&
                BoolValue(reopen, "success") &&
                SamePath(TextValue(reopen, "part_path"), pilotPartPath) &&
                ExactNumber(reopen, "open_errors", 0) &&
                ExactNumber(reopen, "open_warnings", 0) &&
                BoolValue(reopen, "opened_read_only") &&
                ExactNumber(reopen, "save_calls", 0) &&
                ExactNumber(reopen, "external_reference_count", 0) &&
                string.Equals(TextValue(reopenGeometry,
                    "translationNormalizedDetailedSignature"),
                    NativeDoorFrameRightDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactNumber(reopenFeatures, "mirrorPartCount", 0) &&
                ExactNumber(reopenFeatures, "oneBendCount", 4) &&
                ExactNumber(reopenFeatures, "forbiddenImportFeatureCount", 0) &&
                BoolValue(reopenFeatures, "pass"),
                "NATIVE_DOOR_FRAME_RIGHT_PILOT_EVIDENCE_INVALID", 32,
                "door-frame pilot evidence is not exact Boolean-equivalent native CAD proof");
            record.native_door_frame_right_evidence_inputs_gate = true;
        }

        private static void ValidateNativeCabinetShelfRightEvidenceInputs(
            string repositoryRoot, DerivedPartRecord record)
        {
            string pilotEvidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeCabinetShelfRightPilotEvidenceRelativePath);
            string booleanDiffPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeCabinetShelfRightBooleanDiffRelativePath);
            string reopenEvidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeCabinetShelfRightReopenEvidenceRelativePath);
            string pilotPartPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeCabinetShelfRightPilotPartRelativePath);
            string referencePartPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeCabinetShelfRightReferencePartRelativePath);
            Require(!string.IsNullOrWhiteSpace(pilotEvidencePath) &&
                string.Equals(Sha256(pilotEvidencePath),
                    NativeCabinetShelfRightPilotEvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(booleanDiffPath) &&
                string.Equals(Sha256(booleanDiffPath),
                    NativeCabinetShelfRightBooleanDiffSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(reopenEvidencePath) &&
                string.Equals(Sha256(reopenEvidencePath),
                    NativeCabinetShelfRightReopenEvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pilotPartPath) &&
                string.Equals(Sha256(pilotPartPath),
                    NativeCabinetShelfRightPilotPartSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(referencePartPath) &&
                string.Equals(Sha256(referencePartPath),
                    NativeCabinetShelfRightReferencePartSha256,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_CABINET_SHELF_RIGHT_PILOT_INPUT_HASH_INVALID", 32,
                "fixed cabinet-shelf pilot, Boolean-diff, reopen, or reference input changed");
            Dictionary<string, object> pilot = ReadJsonObject(pilotEvidencePath);
            Dictionary<string, object> difference = ReadJsonObject(booleanDiffPath);
            Dictionary<string, object> reopen = ReadJsonObject(reopenEvidencePath);
            Dictionary<string, object> referenceMinus = ChildObject(difference,
                "reference_minus_candidate");
            Dictionary<string, object> candidateMinus = ChildObject(difference,
                "candidate_minus_reference");
            Dictionary<string, object> pilotGeometry = ChildObject(pilot, "geometry");
            Dictionary<string, object> pilotFeatures = ChildObject(pilot, "features");
            Dictionary<string, object> reopenGeometry = ChildObject(reopen, "geometry");
            Dictionary<string, object> reopenFeatures = ChildObject(reopen, "features");
            Require(BoolValue(pilot, "success") &&
                SamePath(TextValue(pilot, "part_path"), pilotPartPath) &&
                ExactNumber(pilot, "external_reference_count", 0) &&
                ExactNumber(pilot, "save_errors", 0) &&
                ExactNumber(pilot, "save_warnings", 0) &&
                string.Equals(TextValue(pilotGeometry,
                    "translationNormalizedDetailedSignature"),
                    NativeCabinetShelfRightDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactNumber(pilotFeatures, "mirrorPartCount", 0) &&
                ExactNumber(pilotFeatures, "oneBendCount", 4) &&
                ExactNumber(pilotFeatures, "forbiddenImportFeatureCount", 0) &&
                BoolValue(pilotFeatures, "pass") &&
                BoolValue(difference, "success") &&
                SamePath(TextValue(difference, "reference_path"), referencePartPath) &&
                SamePath(TextValue(difference, "candidate_path"), pilotPartPath) &&
                ExactNumber(referenceMinus, "error", 0) &&
                ExactNumber(referenceMinus, "body_count", 0) &&
                ExactNumber(referenceMinus, "total_volume_mm3", 0) &&
                ExactNumber(candidateMinus, "error", 0) &&
                ExactNumber(candidateMinus, "body_count", 0) &&
                ExactNumber(candidateMinus, "total_volume_mm3", 0) &&
                BoolValue(reopen, "success") &&
                SamePath(TextValue(reopen, "part_path"), pilotPartPath) &&
                ExactNumber(reopen, "open_errors", 0) &&
                ExactNumber(reopen, "open_warnings", 0) &&
                BoolValue(reopen, "opened_read_only") &&
                ExactNumber(reopen, "save_calls", 0) &&
                ExactNumber(reopen, "external_reference_count", 0) &&
                string.Equals(TextValue(reopenGeometry,
                    "translationNormalizedDetailedSignature"),
                    NativeCabinetShelfRightDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactNumber(reopenFeatures, "mirrorPartCount", 0) &&
                ExactNumber(reopenFeatures, "oneBendCount", 4) &&
                ExactNumber(reopenFeatures, "forbiddenImportFeatureCount", 0) &&
                BoolValue(reopenFeatures, "pass"),
                "NATIVE_CABINET_SHELF_RIGHT_PILOT_EVIDENCE_INVALID", 32,
                "cabinet-shelf pilot evidence is not exact Boolean-equivalent native CAD proof");
            record.native_cabinet_shelf_right_evidence_inputs_gate = true;
        }

        private static DerivedPlan LegacyNativeFrameCrossbarMirrorPlan(
            DerivedPlan nativePlan)
        {
            return new DerivedPlan
            {
                file_name = nativePlan.file_name,
                source_file_name = nativePlan.source_file_name,
                link_type = "MirrorStock",
                source_xmin_mm = nativePlan.source_xmin_mm,
                source_xmax_mm = nativePlan.source_xmax_mm,
                target_xmin_mm = nativePlan.target_xmin_mm,
                target_xmax_mm = nativePlan.target_xmax_mm
            };
        }

        private static void ValidateNativeFrameCrossbarEvidenceInputs(
            string repositoryRoot, DerivedPartRecord record)
        {
            string pilotEvidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeFrameCrossbarPilotEvidenceRelativePath);
            string booleanDiffPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeFrameCrossbarBooleanDiffRelativePath);
            string reopenEvidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeFrameCrossbarReopenEvidenceRelativePath);
            string pilotPartPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeFrameCrossbarPilotPartRelativePath);
            string referencePartPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeFrameCrossbarReferencePartRelativePath);
            Require(!string.IsNullOrWhiteSpace(pilotEvidencePath) &&
                string.Equals(Sha256(pilotEvidencePath),
                    NativeFrameCrossbarPilotEvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(booleanDiffPath) &&
                string.Equals(Sha256(booleanDiffPath),
                    NativeFrameCrossbarBooleanDiffSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(reopenEvidencePath) &&
                string.Equals(Sha256(reopenEvidencePath),
                    NativeFrameCrossbarReopenEvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pilotPartPath) &&
                string.Equals(Sha256(pilotPartPath),
                    NativeFrameCrossbarPilotPartSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(referencePartPath) &&
                string.Equals(Sha256(referencePartPath),
                    NativeFrameCrossbarReferencePartSha256,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_FRAME_CROSSBAR_PILOT_INPUT_HASH_INVALID", 32,
                "fixed frame-crossbar pilot, Boolean-diff, reopen, or reference input changed");
            Dictionary<string, object> pilot = ReadJsonObject(pilotEvidencePath);
            Dictionary<string, object> difference = ReadJsonObject(booleanDiffPath);
            Dictionary<string, object> reopen = ReadJsonObject(reopenEvidencePath);
            Dictionary<string, object> referenceMinus = ChildObject(difference,
                "reference_minus_candidate");
            Dictionary<string, object> candidateMinus = ChildObject(difference,
                "candidate_minus_reference");
            Dictionary<string, object> pilotGeometry = ChildObject(pilot,
                "detailed_geometry");
            Dictionary<string, object> pilotFeatures = ChildObject(pilot,
                "detailed_features");
            Dictionary<string, object> reopenGeometry = ChildObject(reopen,
                "geometry");
            Dictionary<string, object> reopenFeatures = ChildObject(reopen,
                "features");
            Require(BoolValue(pilot, "success") &&
                ExactNumber(pilot, "external_reference_count", 0) &&
                ExactNumber(pilot, "save_errors", 0) &&
                ExactNumber(pilot, "save_warnings", 0) &&
                string.Equals(TextValue(pilotGeometry,
                    "translationNormalizedDetailedSignature"),
                    NativeFrameCrossbarDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactNumber(pilotFeatures, "mirrorPartCount", 0) &&
                BoolValue(difference, "success") &&
                ExactNumber(referenceMinus, "error", 0) &&
                ExactNumber(referenceMinus, "body_count", 0) &&
                ExactNumber(referenceMinus, "total_volume_mm3", 0) &&
                ExactNumber(candidateMinus, "error", 0) &&
                ExactNumber(candidateMinus, "body_count", 0) &&
                ExactNumber(candidateMinus, "total_volume_mm3", 0) &&
                BoolValue(reopen, "success") &&
                ExactNumber(reopen, "open_errors", 0) &&
                ExactNumber(reopen, "open_warnings", 0) &&
                BoolValue(reopen, "opened_read_only") &&
                ExactNumber(reopen, "external_reference_count", 0) &&
                string.Equals(TextValue(reopenGeometry,
                    "translationNormalizedDetailedSignature"),
                    NativeFrameCrossbarDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactNumber(reopenFeatures, "mirrorPartCount", 0),
                "NATIVE_FRAME_CROSSBAR_PILOT_EVIDENCE_INVALID", 32,
                "frame-crossbar pilot evidence is not exact Boolean-equivalent native CAD proof");
            record.native_frame_crossbar_evidence_inputs_gate = true;
        }

        private static bool NativeFrameCrossbarGeometryMatchesContract(
            NativeRightGeometrySnapshot geometry)
        {
            double[] box = { 36.5, 1692.5, -19.7, 425.5, 1707.5, 0.0 };
            double[] radii =
            {
                0.2, 0.2, 0.2, 1.4, 1.4, 1.4,
                1.75, 1.75, 1.75, 3.1, 3.5
            };
            return geometry != null && geometry.bodyCount == 1 && geometry.finite &&
                geometry.bodyBoxMm.Count == 6 && box.Select((value, index) =>
                    Near(geometry.bodyBoxMm[index], value,
                        NativeFrameCrossbarBoxToleranceMm)).All(value => value) &&
                Near(geometry.volumeMm3, 23042.325502284795,
                    NativeFrameCrossbarVolumeToleranceMm3) &&
                Near(geometry.surfaceAreaMm2, 39559.96757532858,
                    NativeFrameCrossbarAreaToleranceMm2) &&
                Near(geometry.massKg, 0.18088225519293563,
                    NativeFrameCrossbarMassToleranceKg) &&
                geometry.faceCount == 40 && geometry.edgeCount == 105 &&
                geometry.loopCount == 55 && geometry.loopEdgeReferenceCount == 210 &&
                geometry.cylinderFaceCount == 11 &&
                geometry.circleEdgeReferenceCount == 44 &&
                string.Equals(geometry.translationNormalizedDetailedSignature,
                    NativeFrameCrossbarDetailedSignature,
                    StringComparison.OrdinalIgnoreCase) &&
                NativeRightRadiiMatch(geometry.cylinderRadiiMm, radii);
        }

        private static bool NativeFrameCrossbarFeatureMatchesContract(
            NativeRightFeatureSnapshot value)
        {
            return value != null && value.sheetMetalCount == 1 &&
                value.oneBendCount == 3 && value.flatPatternCount == 1 &&
                value.flattenBendsCount == 1 && value.processBendsCount == 1 &&
                value.sketchedBendGroupCount == 0 && value.mirrorPartCount == 0 &&
                value.forbiddenImportFeatureCount == 0 && value.bends.Count == 3 &&
                value.issues.Count == 0 &&
                Near(value.sheetMetalThicknessMm, 1.2, 0.0001) &&
                Near(value.sheetMetalRadiusMm, 0.2, 0.0001) &&
                Near(value.sheetMetalKFactor, 0.333333, 0.000001) &&
                value.sheetMetalAllowanceType ==
                    (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                value.bends.All(bend => bend.bendType ==
                    (int)swBendType_e.swSharpBend && !bend.suppressed &&
                    Near(bend.angleRadians, Math.PI / 2.0, 0.000001) &&
                    Near(bend.radiusMm, 0.2, 0.0001) &&
                    Near(bend.kFactor, 0.333333, 0.000001) &&
                    Near(bend.deductionMm, 0.0, 0.000001) &&
                    bend.allowanceType ==
                        (int)swBendAllowanceTypes_e.swBendAllowanceKFactor) &&
                value.bends.Count(bend => bend.canonicalDirection == 2 &&
                    bend.canonicalDown) == 2 &&
                value.bends.Count(bend => bend.canonicalDirection == 1 &&
                    !bend.canonicalDown) == 1;
        }

        private static void BuildNativeCabinetShelfRight888(ISldWorks application,
            ModelDoc2 model)
        {
            MathUtility math = application.GetMathUtility() as MathUtility;
            Feature basePlate = null;
            Feature leftWall = null;
            Feature rightWall = null;
            Feature backWall = null;
            Feature frontWall = null;
            try
            {
                NativeRightRequire(math != null,
                    "CABINET_SHELF_RIGHT_MATH_UTILITY_UNAVAILABLE");
                basePlate = CabinetShelfRightCreateBasePlate(model, math);
                leftWall = CabinetShelfRightCreateThinWall(model, math,
                    new NativeRightPoint3(64.9, 1717.5, -546.4),
                    new NativeRightPoint3(64.9, 1717.5, -21.8), "LEFT");
                rightWall = CabinetShelfRightCreateThinWall(model, math,
                    new NativeRightPoint3(441.3, 1717.5, -546.4),
                    new NativeRightPoint3(441.3, 1717.5, -47.8), "RIGHT");
                backWall = CabinetShelfRightCreateThinWall(model, math,
                    new NativeRightPoint3(65.8, 1717.5, -547.3),
                    new NativeRightPoint3(440.4, 1717.5, -547.3), "BACK");
                frontWall = CabinetShelfRightCreateThinWall(model, math,
                    new NativeRightPoint3(65.8, 1717.5, -20.9),
                    new NativeRightPoint3(426.2, 1717.5, -20.9), "FRONT");
                CabinetShelfRightApplySideAndBackDetails(model, math);

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                    0.253, 2.0, -0.284, 0, -1, 0),
                    "CABINET_SHELF_RIGHT_FIXED_FACE_SELECT_FAILED");
                PartDoc part = model as PartDoc;
                NativeRightRequire(part != null && part.InsertBends2(0.0005,
                    "", 0.333333, 0.0, true, 0.5, true) &&
                    model.ForceRebuild3(false),
                    "CABINET_SHELF_RIGHT_INSERT_BENDS2_FAILED");
                NativeRightFeatureSnapshot inserted = CaptureNativeRightFeatures(model);
                NativeRightRequire(inserted != null && inserted.sheetMetalCount == 1 &&
                    inserted.oneBendCount == 4 && inserted.flatPatternCount == 1 &&
                    inserted.issues.Count == 0,
                    "CABINET_SHELF_RIGHT_INSERT_BENDS_CORE_REGRESSION_FAILED");
                CabinetShelfRightApplyBaseAndFrontDetails(model, math);
                CabinetShelfRightApplyExactBooleanCorrections(model, math);
                CabinetShelfRightApplyReferenceFaceSplits(model, math);
                NativeRightRequire(model.ForceRebuild3(false),
                    "CABINET_SHELF_RIGHT_DETAILED_REBUILD_FAILED");
            }
            finally
            {
                Release(frontWall);
                Release(backWall);
                Release(rightWall);
                Release(leftWall);
                Release(basePlate);
                Release(math);
            }
        }

        private static Feature CabinetShelfRightCreateBasePlate(ModelDoc2 model,
            MathUtility math)
        {
            Feature plane = DoorFrameRightCreateYOffsetPlane(model, 1717.5);
            Feature sketch = null;
            try
            {
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    "CABINET_SHELF_RIGHT_BASE_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                NativeRightPoint3 corner1 = FrameCrossbarToActiveSketchPoint(model,
                    math, new NativeRightPoint3(64.5, 1717.5, -547.7));
                NativeRightPoint3 corner2 = FrameCrossbarToActiveSketchPoint(model,
                    math, new NativeRightPoint3(441.7, 1717.5, -20.5));
                object rectangle = model.SketchManager.CreateCornerRectangle(
                    corner1.x, corner1.y, 0, corner2.x, corner2.y, 0);
                NativeRightRequire(rectangle != null,
                    "CABINET_SHELF_RIGHT_BASE_RECTANGLE_FAILED");
                model.SketchManager.InsertSketch(true);
                sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                    "CABINET_SHELF_RIGHT_BASE_SKETCH_SELECT_FAILED");
                Feature plate = model.FeatureManager.FeatureExtrusion2(
                    true, false, true,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    0.0008, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true, true, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false) as Feature;
                NativeRightRequire(plate != null && plate.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false),
                    "CABINET_SHELF_RIGHT_BASE_EXTRUSION_FAILED");
                return plate;
            }
            finally
            {
                Release(sketch);
                Release(plane);
                model.ClearSelection2(true);
            }
        }

        private static Feature CabinetShelfRightCreateThinWall(ModelDoc2 model,
            MathUtility math, NativeRightPoint3 startPoint,
            NativeRightPoint3 endPoint, string label)
        {
            Feature plane = DoorFrameRightCreateYOffsetPlane(model, startPoint.y);
            Feature sketch = null;
            try
            {
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    "CABINET_SHELF_RIGHT_" + label + "_WALL_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(model,
                    math, startPoint);
                NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(model,
                    math, endPoint);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    NativeRightRequire(model.SketchManager.CreateLine(start.x,
                        start.y, 0, end.x, end.y, 0) != null,
                        "CABINET_SHELF_RIGHT_" + label + "_WALL_LINE_FAILED");
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                    "CABINET_SHELF_RIGHT_" + label + "_WALL_SKETCH_SELECT_FAILED");
                Feature wall = model.FeatureManager.FeatureExtrusionThin2(
                    true, false, true,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    0.025, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true,
                    0.0008, 0.0, 0.0,
                    (int)swThinWallType_e.swThinWallMidPlane, 0, false, 0.0,
                    true, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false) as Feature;
                NativeRightRequire(wall != null && wall.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false),
                    "CABINET_SHELF_RIGHT_" + label + "_WALL_EXTRUSION_FAILED");
                return wall;
            }
            finally
            {
                Release(sketch);
                Release(plane);
                model.ClearSelection2(true);
            }
        }

        private static void CabinetShelfRightApplyBaseAndFrontDetails(
            ModelDoc2 model, MathUtility math)
        {
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.253, 2.0, -0.284, 0, -1, 0),
                "CABINET_SHELF_RIGHT_BASE_DETAIL_FACE_SELECT_FAILED");
            var holes = new List<NativeRightPoint3>();
            var radii = new List<double>();
            foreach (double x in new[] { 173.1, 333.1 })
                foreach (double z in new[] { -418.5, -378.5, -168.5, -128.5 })
                {
                    holes.Add(new NativeRightPoint3(x, 1717.5, z));
                    radii.Add(1.0);
                }
            FrameCrossbarCreateCut(model, math, new[]
            {
                CabinetShelfRightRectangleOnY(1717.5, 426.2, 442.0, -47.8, -20.5),
                CabinetShelfRightRectangleOnY(1717.5, 406.7, 416.7, -22.5, -20.5),
                CabinetShelfRightRectangleOnY(1717.5, 64.5, 66.2, -22.2, -20.5),
                CabinetShelfRightRectangleOnY(1717.5, 64.5, 66.2, -547.7, -546.0),
                CabinetShelfRightRectangleOnY(1717.5, 440.0, 441.7, -547.7, -546.0)
            }, holes, radii, 0.001, "CABINET_SHELF_RIGHT_BASE_DETAILS");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.253, 1.6955, 0.0, 0, 0, -1),
                "CABINET_SHELF_RIGHT_FRONT_DETAIL_FACE_SELECT_FAILED");
            FrameCrossbarCreateCut(model, math, new[]
            {
                CabinetShelfRightRectangleOnZ(-20.5, 406.7, 416.7, 1705.5, 1717.5)
            }, new[]
            {
                new NativeRightPoint3(69.0, 1695.5, -20.5),
                new NativeRightPoint3(244.0, 1695.5, -20.5),
                new NativeRightPoint3(419.0, 1695.5, -20.5)
            }, new[] { 1.75, 1.75, 1.75 }, 0.001,
                "CABINET_SHELF_RIGHT_FRONT_DETAILS");
        }

        private static NativeRightPoint3[] CabinetShelfRightRectangleOnY(
            double y, double x1, double x2, double z1, double z2)
        {
            return new[]
            {
                new NativeRightPoint3(x1, y, z1),
                new NativeRightPoint3(x2, y, z1),
                new NativeRightPoint3(x2, y, z2),
                new NativeRightPoint3(x1, y, z2)
            };
        }

        private static NativeRightPoint3[] CabinetShelfRightRectangleOnZ(
            double z, double x1, double x2, double y1, double y2)
        {
            return new[]
            {
                new NativeRightPoint3(x1, y1, z),
                new NativeRightPoint3(x2, y1, z),
                new NativeRightPoint3(x2, y2, z),
                new NativeRightPoint3(x1, y2, z)
            };
        }

        private static void CabinetShelfRightApplySideAndBackDetails(
            ModelDoc2 model, MathUtility math)
        {
            CabinetShelfRightTrimSideWall(model, math, 64.5, true,
                -546.4, -21.8, "LEFT_SIDE");
            CabinetShelfRightTrimSideWall(model, math, 441.7, false,
                -546.4, -47.8, "RIGHT_SIDE");
            CabinetShelfRightTrimBackWall(model, math);
            CabinetShelfRightCutRightFrontCornerSector(model, math);
        }

        private static void CabinetShelfRightTrimSideWall(ModelDoc2 model,
            MathUtility math, double x, bool left, double zMin, double zMax,
            string label)
        {
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                left ? 0.0 : 0.6, 1.71, -0.284, left ? 1 : -1, 0, 0),
                "CABINET_SHELF_RIGHT_" + label + "_FACE_SELECT_FAILED");
            FrameCrossbarCreateCut(model, math, new[]
            {
                CabinetShelfRightRectangleOnX(x, 1692.4, 1705.5, zMin, zMax)
            }, new NativeRightPoint3[0], new double[0], 0.001,
                "CABINET_SHELF_RIGHT_" + label + "_LOWER_STRIP");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                left ? 0.0 : 0.6, 1.71, -0.284, left ? 1 : -1, 0, 0),
                "CABINET_SHELF_RIGHT_" + label + "_BOSS_FACE_SELECT_FAILED");
            CabinetShelfRightCreateTabBosses(model, math, new[]
            {
                CabinetShelfRightSideTab(x, -398.5),
                CabinetShelfRightSideTab(x, -148.5)
            }, true, true, 0.0008,
                "CABINET_SHELF_RIGHT_" + label + "_TABS");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                left ? 0.0 : 0.6, 1.698, -0.3985, left ? 1 : -1, 0, 0),
                "CABINET_SHELF_RIGHT_" + label + "_HOLE_FACE_SELECT_FAILED");
            FrameCrossbarCreateCut(model, math, new[]
            {
                CabinetShelfRightRectangleOnX(x, 1696.0, 1701.0,
                    -400.5, -396.5),
                CabinetShelfRightRectangleOnX(x, 1696.0, 1701.0,
                    -150.5, -146.5)
            }, new NativeRightPoint3[0], new double[0], 0.0008,
                "CABINET_SHELF_RIGHT_" + label + "_TAB_HOLES");
        }

        private static void CabinetShelfRightTrimBackWall(ModelDoc2 model,
            MathUtility math)
        {
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.253, 1.71, -1.0, 0, 0, 1),
                "CABINET_SHELF_RIGHT_BACK_FACE_SELECT_FAILED");
            FrameCrossbarCreateCut(model, math, new[]
            {
                CabinetShelfRightRectangleOnZ(-547.7, 64.5, 441.7,
                    1692.4, 1705.5)
            }, new NativeRightPoint3[0], new double[0], 0.001,
                "CABINET_SHELF_RIGHT_BACK_LOWER_STRIP");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.253, 1.71, -0.5, 0, 0, -1),
                "CABINET_SHELF_RIGHT_BACK_BOSS_FACE_SELECT_FAILED");
            CabinetShelfRightCreateTabBosses(model, math,
                new[] { CabinetShelfRightBackTab(-546.9) },
                true, true, 0.0008, "CABINET_SHELF_RIGHT_BACK_TAB");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.253, 1.698, -1.0, 0, 0, 1),
                "CABINET_SHELF_RIGHT_BACK_HOLE_FACE_SELECT_FAILED");
            FrameCrossbarCreateCut(model, math, new[]
            {
                CabinetShelfRightRectangleOnZ(-547.7, 251.1, 255.1,
                    1696.0, 1701.0)
            }, new NativeRightPoint3[0], new double[0], 0.0008,
                "CABINET_SHELF_RIGHT_BACK_TAB_HOLE");
        }

        private static NativeRightPoint3[] CabinetShelfRightRectangleOnX(
            double x, double y1, double y2, double z1, double z2)
        {
            return new[]
            {
                new NativeRightPoint3(x, y1, z1),
                new NativeRightPoint3(x, y2, z1),
                new NativeRightPoint3(x, y2, z2),
                new NativeRightPoint3(x, y1, z2)
            };
        }

        private static CabinetShelfTabLoop CabinetShelfRightSideTab(
            double x, double centerZ)
        {
            const double shortDelta = 0.5176380902050415;
            const double longDelta = 1.9318516525781366;
            const double centerDelta = 2.98200652243755;
            return new CabinetShelfTabLoop
            {
                top_left = new NativeRightPoint3(x, 1705.5, centerZ - 8.0),
                left_arc_start = new NativeRightPoint3(x,
                    1694.5 - shortDelta, centerZ - centerDelta - longDelta),
                left_center = new NativeRightPoint3(x, 1694.5,
                    centerZ - centerDelta),
                left_bottom = new NativeRightPoint3(x, 1692.5,
                    centerZ - centerDelta),
                right_bottom = new NativeRightPoint3(x, 1692.5,
                    centerZ + centerDelta),
                right_center = new NativeRightPoint3(x, 1694.5,
                    centerZ + centerDelta),
                right_arc_end = new NativeRightPoint3(x,
                    1694.5 - shortDelta, centerZ + centerDelta + longDelta),
                top_right = new NativeRightPoint3(x, 1705.5, centerZ + 8.0)
            };
        }

        private static CabinetShelfTabLoop CabinetShelfRightBackTab(double z)
        {
            const double shortDelta = 0.5176380902050415;
            const double longDelta = 1.9318516525781366;
            const double centerDelta = 2.98200652243755;
            return new CabinetShelfTabLoop
            {
                top_left = new NativeRightPoint3(245.1, 1705.5, z),
                left_arc_start = new NativeRightPoint3(
                    253.1 - centerDelta - longDelta,
                    1694.5 - shortDelta, z),
                left_center = new NativeRightPoint3(253.1 - centerDelta,
                    1694.5, z),
                left_bottom = new NativeRightPoint3(253.1 - centerDelta,
                    1692.5, z),
                right_bottom = new NativeRightPoint3(253.1 + centerDelta,
                    1692.5, z),
                right_center = new NativeRightPoint3(253.1 + centerDelta,
                    1694.5, z),
                right_arc_end = new NativeRightPoint3(
                    253.1 + centerDelta + longDelta,
                    1694.5 - shortDelta, z),
                top_right = new NativeRightPoint3(261.1, 1705.5, z)
            };
        }

        private static void CabinetShelfRightCreateTabBosses(ModelDoc2 model,
            MathUtility math, IEnumerable<CabinetShelfTabLoop> tabs,
            bool flip, bool reverseDirection, double depth, string label)
        {
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                foreach (CabinetShelfTabLoop tab in tabs)
                    CabinetShelfRightDrawTabLoop(model, math, tab, label);
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature boss = model.FeatureManager.FeatureExtrusion2(true, flip,
                reverseDirection, (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false) as Feature;
            Release(sketch);
            NativeRightRequire(boss != null && boss.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_BOSS_FAILED");
            Release(boss);
            NativeRightRequire(CabinetShelfRightSolidBodyCount(model) == 1,
                label + "_BODY_COUNT_INVALID");
            model.ClearSelection2(true);
        }

        private static void CabinetShelfRightDrawTabLoop(ModelDoc2 model,
            MathUtility math, CabinetShelfTabLoop tab, string label)
        {
            NativeRightPoint3 topLeft = FrameCrossbarToActiveSketchPoint(model,
                math, tab.top_left);
            NativeRightPoint3 leftArcStart = FrameCrossbarToActiveSketchPoint(model,
                math, tab.left_arc_start);
            NativeRightPoint3 leftCenter = FrameCrossbarToActiveSketchPoint(model,
                math, tab.left_center);
            NativeRightPoint3 leftBottom = FrameCrossbarToActiveSketchPoint(model,
                math, tab.left_bottom);
            NativeRightPoint3 rightBottom = FrameCrossbarToActiveSketchPoint(model,
                math, tab.right_bottom);
            NativeRightPoint3 rightCenter = FrameCrossbarToActiveSketchPoint(model,
                math, tab.right_center);
            NativeRightPoint3 rightArcEnd = FrameCrossbarToActiveSketchPoint(model,
                math, tab.right_arc_end);
            NativeRightPoint3 topRight = FrameCrossbarToActiveSketchPoint(model,
                math, tab.top_right);
            CabinetShelfRightRequireLine(model, topLeft, leftArcStart,
                label + "_LEFT_SLOPE");
            CabinetShelfRightCreateMinorArc(model, leftCenter, leftArcStart,
                leftBottom, 0.002, label + "_LEFT_ARC");
            CabinetShelfRightRequireLine(model, leftBottom, rightBottom,
                label + "_BOTTOM");
            CabinetShelfRightCreateMinorArc(model, rightCenter, rightBottom,
                rightArcEnd, 0.002, label + "_RIGHT_ARC");
            CabinetShelfRightRequireLine(model, rightArcEnd, topRight,
                label + "_RIGHT_SLOPE");
            CabinetShelfRightRequireLine(model, topRight, topLeft,
                label + "_TOP");
        }

        private static void CabinetShelfRightRequireLine(ModelDoc2 model,
            NativeRightPoint3 start, NativeRightPoint3 end, string label)
        {
            NativeRightRequire(model.SketchManager.CreateLine(start.x,
                start.y, 0, end.x, end.y, 0) != null,
                label + "_LINE_FAILED");
        }

        private static void CabinetShelfRightCreateMinorArc(ModelDoc2 model,
            NativeRightPoint3 center, NativeRightPoint3 start,
            NativeRightPoint3 end, double radius, string label)
        {
            double sx = (start.x - center.x) / radius;
            double sy = (start.y - center.y) / radius;
            double ex = (end.x - center.x) / radius;
            double ey = (end.y - center.y) / radius;
            double bx = sx + ex;
            double by = sy + ey;
            double length = Math.Sqrt(bx * bx + by * by);
            NativeRightRequire(length > 1e-9, label + "_BISECTOR_FAILED");
            double middleX = center.x + radius * bx / length;
            double middleY = center.y + radius * by / length;
            SketchSegment arc = model.SketchManager.Create3PointArc(start.x,
                start.y, 0, end.x, end.y, 0, middleX, middleY, 0);
            NativeRightRequire(arc != null && arc.GetLength() <= Math.PI * radius,
                label + "_MINOR_ARC_FAILED");
            Release(arc);
        }

        private static int CabinetShelfRightSolidBodyCount(ModelDoc2 model)
        {
            PartDoc part = model as PartDoc;
            object[] bodies = part == null ? null : part.GetBodies2(
                (int)swBodyType_e.swSolidBody, true) as object[];
            int count = bodies == null ? 0 : bodies.Length;
            if (bodies != null) foreach (object body in bodies) Release(body);
            return count;
        }

        private static void CabinetShelfRightCutRightFrontCornerSector(
            ModelDoc2 model, MathUtility math)
        {
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.6, 1.71, -0.049, -1, 0, 0),
                "CABINET_SHELF_RIGHT_FRONT_CORNER_FACE_SELECT_FAILED");
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                NativeRightPoint3 corner = FrameCrossbarToActiveSketchPoint(model,
                    math, new NativeRightPoint3(441.7, 1705.5, -47.8));
                NativeRightPoint3 bottom = FrameCrossbarToActiveSketchPoint(model,
                    math, new NativeRightPoint3(441.7, 1705.5, -49.8));
                NativeRightPoint3 center = FrameCrossbarToActiveSketchPoint(model,
                    math, new NativeRightPoint3(441.7, 1707.5, -49.8));
                NativeRightPoint3 right = FrameCrossbarToActiveSketchPoint(model,
                    math, new NativeRightPoint3(441.7, 1707.5, -47.8));
                CabinetShelfRightRequireLine(model, corner, bottom,
                    "CABINET_SHELF_RIGHT_FRONT_CORNER_BOTTOM");
                CabinetShelfRightCreateMinorArc(model, center, bottom, right,
                    0.002, "CABINET_SHELF_RIGHT_FRONT_CORNER_ARC");
                CabinetShelfRightRequireLine(model, right, corner,
                    "CABINET_SHELF_RIGHT_FRONT_CORNER_SIDE");
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                "CABINET_SHELF_RIGHT_FRONT_CORNER_SKETCH_SELECT_FAILED");
            Feature cut = model.FeatureManager.FeatureCut3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                0.001, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                model.ForceRebuild3(false),
                "CABINET_SHELF_RIGHT_FRONT_CORNER_CUT_FAILED");
            Release(cut);
            model.ClearSelection2(true);
        }

        private static void CabinetShelfRightApplyExactBooleanCorrections(
            ModelDoc2 model, MathUtility math)
        {
            const double farX = 68.18599434057;
            const double nearY = 1695.77841282804;
            const double farY = 1700.58840339566;
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -150.5,
                new[]
                {
                    new NativeRightPoint3(64.5, 1696.0, -150.5),
                    new NativeRightPoint3(67.5, 1701.0, -150.5),
                    new NativeRightPoint3(farX, farY, -150.5),
                    new NativeRightPoint3(65.3, nearY, -150.5)
                }, 0.004, false, "CABINET_SHELF_RIGHT_LEFT_FRONT_WEDGE");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -400.5,
                new[]
                {
                    new NativeRightPoint3(64.5, 1696.0, -400.5),
                    new NativeRightPoint3(67.5, 1701.0, -400.5),
                    new NativeRightPoint3(farX, farY, -400.5),
                    new NativeRightPoint3(65.3, nearY, -400.5)
                }, 0.004, false, "CABINET_SHELF_RIGHT_LEFT_REAR_WEDGE");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -150.5,
                new[]
                {
                    new NativeRightPoint3(441.7, 1696.0, -150.5),
                    new NativeRightPoint3(438.7, 1701.0, -150.5),
                    new NativeRightPoint3(438.01400565943, farY, -150.5),
                    new NativeRightPoint3(440.9, nearY, -150.5)
                }, 0.004, false, "CABINET_SHELF_RIGHT_RIGHT_FRONT_WEDGE");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -400.5,
                new[]
                {
                    new NativeRightPoint3(441.7, 1696.0, -400.5),
                    new NativeRightPoint3(438.7, 1701.0, -400.5),
                    new NativeRightPoint3(438.01400565943, farY, -400.5),
                    new NativeRightPoint3(440.9, nearY, -400.5)
                }, 0.004, false, "CABINET_SHELF_RIGHT_RIGHT_REAR_WEDGE");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 0, 251.1,
                new[]
                {
                    new NativeRightPoint3(251.1, 1696.0, -547.7),
                    new NativeRightPoint3(251.1, 1701.0, -544.7),
                    new NativeRightPoint3(251.1, farY, -544.01400565943),
                    new NativeRightPoint3(251.1, nearY, -546.9),
                    new NativeRightPoint3(251.1, 1696.0, -546.9)
                }, 0.004, false, "CABINET_SHELF_RIGHT_BACK_WEDGE");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -547.7,
                CabinetShelfRightRectangleOnZ(-547.7, 64.5, 65.8,
                    1705.5, 1715.8),
                0.0008, false, "CABINET_SHELF_RIGHT_BACK_LEFT_CORNER_BLOCK");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -547.7,
                CabinetShelfRightRectangleOnZ(-547.7, 440.4, 441.7,
                    1705.5, 1715.8),
                0.0008, false, "CABINET_SHELF_RIGHT_BACK_RIGHT_CORNER_BLOCK");
            CabinetShelfRightAddBossFromOffsetPlane(model, math, 2, -21.3,
                new[]
                {
                    new NativeRightPoint3(65.8, 1692.5, -21.3),
                    new NativeRightPoint3(65.5, 1692.5, -21.3),
                    new NativeRightPoint3(64.5, 1693.5, -21.3),
                    new NativeRightPoint3(64.5, 1715.8, -21.3),
                    new NativeRightPoint3(65.8, 1715.8, -21.3)
                }, 0.0008, false, "CABINET_SHELF_RIGHT_FRONT_LEFT_BLOCK");
            CabinetShelfRightCutFromOffsetPlane(model, math, 2, -21.3,
                new[]
                {
                    new NativeRightPoint3(425.2, 1692.5, -21.3),
                    new NativeRightPoint3(426.2, 1692.5, -21.3),
                    new NativeRightPoint3(426.2, 1693.5, -21.3)
                }, 0.0008, true, "CABINET_SHELF_RIGHT_FRONT_RIGHT_EXCESS");
        }

        private static void CabinetShelfRightAddBossFromOffsetPlane(
            ModelDoc2 model, MathUtility math, int axis, double offsetMm,
            IEnumerable<NativeRightPoint3> points, double depth,
            bool reverse, string label)
        {
            Feature plane = CabinetShelfRightCreateAxisOffsetPlane(model,
                axis, offsetMm);
            Feature sketch = null;
            try
            {
                model.ClearSelection2(true);
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    label + "_PLANE_SELECT_FAILED");
                CabinetShelfRightDrawClosedPolygon(model, math, points, label);
                sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                    label + "_SKETCH_SELECT_FAILED");
                Feature boss = model.FeatureManager.FeatureExtrusion2(true,
                    false, reverse, (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depth, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true, true, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false) as Feature;
                NativeRightRequire(boss != null && boss.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), label + "_BOSS_FAILED");
                Release(boss);
                NativeRightRequire(CabinetShelfRightSolidBodyCount(model) == 1,
                    label + "_BODY_COUNT_INVALID");
            }
            finally
            {
                Release(sketch);
                Release(plane);
                model.ClearSelection2(true);
            }
        }

        private static void CabinetShelfRightCutFromOffsetPlane(
            ModelDoc2 model, MathUtility math, int axis, double offsetMm,
            IEnumerable<NativeRightPoint3> points, double depth,
            bool reverse, string label)
        {
            Feature plane = CabinetShelfRightCreateAxisOffsetPlane(model,
                axis, offsetMm);
            Feature sketch = null;
            try
            {
                model.ClearSelection2(true);
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    label + "_PLANE_SELECT_FAILED");
                CabinetShelfRightDrawClosedPolygon(model, math, points, label);
                sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                    label + "_SKETCH_SELECT_FAILED");
                Feature cut = model.FeatureManager.FeatureCut3(true, false,
                    reverse, (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depth, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), label + "_CUT_FAILED");
                Release(cut);
            }
            finally
            {
                Release(sketch);
                Release(plane);
                model.ClearSelection2(true);
            }
        }

        private static void CabinetShelfRightDrawClosedPolygon(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightPoint3> rawPoints,
            string label)
        {
            List<NativeRightPoint3> points = rawPoints.ToList();
            NativeRightRequire(points.Count >= 3, label + "_POLYGON_INVALID");
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                for (int index = 0; index < points.Count; index++)
                {
                    NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                        model, math, points[index]);
                    NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                        model, math, points[(index + 1) % points.Count]);
                    NativeRightRequire(model.SketchManager.CreateLine(start.x,
                        start.y, 0, end.x, end.y, 0) != null,
                        label + "_LINE_FAILED");
                }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
        }

        private static Feature CabinetShelfRightCreateAxisOffsetPlane(
            ModelDoc2 model, int axis, double offsetMm)
        {
            Feature basePlane = FrameCrossbarFindDefaultPlane(model, axis);
            try
            {
                List<double> transform = FrameCrossbarPlaneTransform(basePlane);
                double normal = transform.Count >= 9 ? transform[6 + axis] : 0;
                model.ClearSelection2(true);
                NativeRightRequire(basePlane.Select2(false, 0),
                    "CABINET_SHELF_RIGHT_AXIS_PLANE_SELECT_FAILED");
                int constraint =
                    (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
                if (offsetMm * normal < 0) constraint |=
                    (int)swRefPlaneReferenceConstraints_e.
                        swRefPlaneReferenceConstraint_OptionFlip;
                Feature plane = model.FeatureManager.InsertRefPlane(constraint,
                    Math.Abs(offsetMm) / 1000.0, 0, 0, 0, 0) as Feature;
                NativeRightRequire(plane != null,
                    "CABINET_SHELF_RIGHT_AXIS_OFFSET_PLANE_FAILED");
                return plane;
            }
            finally { Release(basePlane); }
        }

        private static void CabinetShelfRightApplyReferenceFaceSplits(
            ModelDoc2 model, MathUtility math)
        {
            CabinetShelfRightProjectionSplit(model, math, 2, -22.3,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(65.8, 1716.7, -22.3),
                        new NativeRightPoint3(65.8, 1717.5, -22.3)
                    }
                }, new[]
                {
                    new[] { 65.0, 1717.0, -10.0, 0.0, 0.0, -1.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_FRONT_LEFT_Z");
            CabinetShelfRightProjectionSplit(model, math, 2, -47.9,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(440.4, 1716.7, -47.9),
                        new NativeRightPoint3(440.4, 1717.5, -47.9)
                    }
                }, new[]
                {
                    new[] { 435.0, 1717.0, -30.0, 0.0, 0.0, -1.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_FRONT_RIGHT_Z");
            CabinetShelfRightProjectionSplit(model, math, 2, -546.1,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(65.8, 1716.7, -546.1),
                        new NativeRightPoint3(65.8, 1717.5, -546.1)
                    }
                }, new[]
                {
                    new[] { 65.0, 1717.0, -500.0, 0.0, 0.0, -1.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_BACK_LEFT_Z");
            CabinetShelfRightProjectionSplit(model, math, 2, -546.1,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(440.4, 1716.7, -546.1),
                        new NativeRightPoint3(440.4, 1717.5, -546.1)
                    }
                }, new[]
                {
                    new[] { 441.0, 1717.0, -500.0, 0.0, 0.0, -1.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_BACK_RIGHT_Z");
            CabinetShelfRightProjectionSplit(model, math, 0, 406.6,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(406.6, 1716.2, -21.3),
                        new NativeRightPoint3(406.6, 1716.2, -20.5)
                    },
                    new[]
                    {
                        new NativeRightPoint3(406.6, 1716.7, -21.8),
                        new NativeRightPoint3(406.6, 1717.5, -21.8)
                    }
                }, new[]
                {
                    new[] { 400.0, 1716.5, -21.0, 1.0, 0.0, 0.0 }
                }, 2, "CABINET_SHELF_RIGHT_SPLIT_SLOT_LEFT_X");
            CabinetShelfRightProjectionSplit(model, math, 0, 416.6,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(416.6, 1716.2, -21.3),
                        new NativeRightPoint3(416.6, 1716.2, -20.5)
                    },
                    new[]
                    {
                        new NativeRightPoint3(416.6, 1716.7, -21.8),
                        new NativeRightPoint3(416.6, 1717.5, -21.8)
                    }
                }, new[]
                {
                    new[] { 420.0, 1716.5, -21.0, -1.0, 0.0, 0.0 }
                }, 2, "CABINET_SHELF_RIGHT_SPLIT_SLOT_RIGHT_X");
            CabinetShelfRightProjectionSplit(model, math, 0, 426.1,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(426.1, 1716.7, -21.8),
                        new NativeRightPoint3(426.1, 1717.5, -21.8)
                    }
                }, new[]
                {
                    new[] { 400.0, 1717.0, -30.0, 1.0, 0.0, 0.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_FRONT_STEP_X");
            CabinetShelfRightProjectionSplit(model, math, 0, 439.9,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(439.9, 1716.7, -546.4),
                        new NativeRightPoint3(439.9, 1717.5, -546.4)
                    }
                }, new[]
                {
                    new[] { 430.0, 1717.0, -546.8, 1.0, 0.0, 0.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_BACK_STEP_X");
            CabinetShelfRightProjectionSplit(model, math, 0, 66.1,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(66.1, 1716.7, -21.8),
                        new NativeRightPoint3(66.1, 1717.5, -21.8)
                    }
                }, new[]
                {
                    new[] { 70.0, 1717.0, -21.0, -1.0, 0.0, 0.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_LEFT_FRONT_X");
            CabinetShelfRightProjectionSplit(model, math, 0, 66.1,
                new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(66.1, 1716.7, -546.4),
                        new NativeRightPoint3(66.1, 1717.5, -546.4)
                    }
                }, new[]
                {
                    new[] { 70.0, 1717.0, -546.8, -1.0, 0.0, 0.0 }
                }, 1, "CABINET_SHELF_RIGHT_SPLIT_LEFT_BACK_X");
        }

        private static void CabinetShelfRightProjectionSplit(ModelDoc2 model,
            MathUtility math, int axis, double planeOffsetMm,
            IEnumerable<NativeRightPoint3[]> segments,
            IEnumerable<double[]> targetRays, int expectedFaceIncrease,
            string label)
        {
            int before = CabinetShelfRightSolidBodyFaceCount(model);
            Feature plane = CabinetShelfRightCreateAxisOffsetPlane(model,
                axis, planeOffsetMm);
            Feature sketch = null;
            ModelDocExtension extension = null;
            try
            {
                model.ClearSelection2(true);
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    label + "_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (NativeRightPoint3[] segment in segments)
                    {
                        NativeRightRequire(segment != null && segment.Length == 2,
                            label + "_SEGMENT_INVALID");
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, segment[0]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, segment[1]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            label + "_LINE_FAILED");
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketch = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                model.ClearSelection2(true);
                NativeRightRequire(sketch != null && sketch.Select2(false, 4),
                    label + "_SKETCH_MARK_FAILED");
                extension = model.Extension;
                foreach (double[] ray in targetRays)
                {
                    NativeRightRequire(ray != null && ray.Length == 6 &&
                        extension != null && extension.SelectByRay(
                            ray[0] / 1000.0, ray[1] / 1000.0,
                            ray[2] / 1000.0, ray[3], ray[4], ray[5],
                            0.001, (int)swSelectType_e.swSelFACES,
                            true, 1, 0), label + "_TARGET_MARK_FAILED");
                }
                model.InsertSplitLineProject(false, false);
                NativeRightRequire(model.ForceRebuild3(false),
                    label + "_REBUILD_FAILED");
                int after = CabinetShelfRightSolidBodyFaceCount(model);
                NativeRightRequire(after == before + expectedFaceIncrease,
                    label + "_FACE_COUNT_INVALID");
            }
            finally
            {
                Release(extension);
                Release(sketch);
                Release(plane);
                model.ClearSelection2(true);
            }
        }

        private static int CabinetShelfRightSolidBodyFaceCount(ModelDoc2 model)
        {
            PartDoc part = model as PartDoc;
            object[] bodies = part == null ? null : part.GetBodies2(
                (int)swBodyType_e.swSolidBody, true) as object[];
            NativeRightRequire(bodies != null && bodies.Length == 1,
                "CABINET_SHELF_RIGHT_FACE_COUNT_BODY_INVALID");
            Body2 body = bodies[0] as Body2;
            int count = body == null ? 0 : body.GetFaceCount();
            foreach (object value in bodies) Release(value);
            return count;
        }

        private static bool NativeCabinetShelfRightGeometryMatchesContract(
            NativeRightGeometrySnapshot geometry)
        {
            double[] box = { 64.5, 1692.5, -547.7, 441.7, 1717.5, -20.5 };
            double[] radii =
            {
                0.5, 0.5, 0.5, 0.5, 0.5,
                1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
                1.3, 1.3, 1.3, 1.3, 1.3,
                1.75, 1.75, 1.75,
                2.0, 2.0, 2.0, 2.0, 2.0, 2.0,
                2.0, 2.0, 2.0, 2.0, 2.0
            };
            return geometry != null && geometry.bodyCount == 1 && geometry.finite &&
                geometry.bodyBoxMm.Count == 6 && box.Select((value, index) =>
                    Near(geometry.bodyBoxMm[index], value,
                        NativeCabinetShelfRightBoxToleranceMm)).All(value => value) &&
                Near(geometry.volumeMm3, 178255.1993277,
                    NativeCabinetShelfRightVolumeToleranceMm3) &&
                Near(geometry.surfaceAreaMm2, 447469.005578149,
                    NativeCabinetShelfRightAreaToleranceMm2) &&
                Near(geometry.massKg, 1.39930331472244,
                    NativeCabinetShelfRightMassToleranceKg) &&
                geometry.faceCount == 152 && geometry.edgeCount == 395 &&
                geometry.loopCount == 195 &&
                geometry.loopEdgeReferenceCount == 790 &&
                geometry.cylinderFaceCount == 32 &&
                geometry.circleEdgeReferenceCount == 128 &&
                NativeRightRadiiMatch(geometry.cylinderRadiiMm, radii) &&
                string.Equals(geometry.translationNormalizedDetailedSignature,
                    NativeCabinetShelfRightDetailedSignature,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool NativeCabinetShelfRightFeatureMatchesContract(
            NativeRightFeatureSnapshot value)
        {
            return value != null && value.sheetMetalCount == 1 &&
                value.oneBendCount == 4 && value.flatPatternCount == 1 &&
                value.flattenBendsCount == 1 && value.processBendsCount == 1 &&
                value.mirrorPartCount == 0 && value.forbiddenImportFeatureCount == 0 &&
                value.sketchedBendGroupCount == 0 && value.bends.Count == 4 &&
                value.issues.Count == 0 &&
                Near(value.sheetMetalThicknessMm, 0.8, 0.0001) &&
                Near(value.sheetMetalRadiusMm, 0.5, 0.0001) &&
                Near(value.sheetMetalKFactor, 0.333333, 0.000001) &&
                Near(value.sheetMetalReliefRatio, 0.5, 0.000001) &&
                value.sheetMetalAllowanceType ==
                    (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                value.bends.All(bend => bend.bendType ==
                    (int)swBendType_e.swSharpBend && !bend.suppressed &&
                    Near(bend.angleRadians, Math.PI / 2.0, 0.000001) &&
                    Near(bend.radiusMm, 0.5, 0.0001) &&
                    Near(bend.kFactor, 0.333333, 0.000001) &&
                    bend.allowanceType ==
                        (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                    bend.canonicalDirection == 2 && bend.canonicalDown);
        }

        private static void BuildNativeDoorFrameRight888(ISldWorks application,
            ModelDoc2 model)
        {
            MathUtility math = application.GetMathUtility() as MathUtility;
            Feature topPlane = null;
            Feature thinExtrusion = null;
            try
            {
                NativeRightRequire(math != null,
                    "DOOR_FRAME_RIGHT_MATH_UTILITY_UNAVAILABLE");
                topPlane = FrameCrossbarFindDefaultPlane(model, 1);
                NativeRightRequire(topPlane != null && topPlane.Select2(false, 0),
                    "DOOR_FRAME_RIGHT_TOP_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                NativeRightPoint3[] path =
                {
                    new NativeRightPoint3(424.6, 0.0, -18.5),
                    new NativeRightPoint3(424.6, 0.0, -0.6),
                    new NativeRightPoint3(443.4, 0.0, -0.6),
                    new NativeRightPoint3(443.4, 0.0, -44.4),
                    new NativeRightPoint3(429.6, 0.0, -44.4),
                    new NativeRightPoint3(429.6, 0.0, -35.0)
                };
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    for (int index = 0; index < path.Length - 1; index++)
                    {
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, path[index]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, path[index + 1]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            "DOOR_FRAME_RIGHT_OPEN_PROFILE_LINE_FAILED");
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                Feature profile = NativeRightLastRootFeatureOfType(model,
                    "ProfileFeature");
                NativeRightRequire(profile != null && profile.Select2(false, 0),
                    "DOOR_FRAME_RIGHT_PROFILE_SELECT_FAILED");
                thinExtrusion = model.FeatureManager.FeatureExtrusionThin2(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    1.917, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true,
                    0.0012, 0.0, 0.0,
                    (int)swThinWallType_e.swThinWallMidPlane, 0, false, 0.0,
                    false, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                Release(profile);
                NativeRightRequire(thinExtrusion != null &&
                    thinExtrusion.GetErrorCode() == 0 && model.ForceRebuild3(false),
                    "DOOR_FRAME_RIGHT_THIN_EXTRUSION_FAILED");

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                    0.435, 0.9, 0.01, 0, 0, -1),
                    "DOOR_FRAME_RIGHT_FIXED_FACE_SELECT_FAILED");
                PartDoc part = model as PartDoc;
                NativeRightRequire(part != null && part.InsertBends2(0.0002,
                    "", 0.333333, 0.0, true, 0.333333, true) &&
                    model.ForceRebuild3(false),
                    "DOOR_FRAME_RIGHT_INSERT_BENDS2_FAILED");
                NativeRightFeatureSnapshot inserted = CaptureNativeRightFeatures(model);
                NativeRightRequire(inserted != null && inserted.sheetMetalCount == 1 &&
                    inserted.oneBendCount == 4 && inserted.flatPatternCount == 1 &&
                    inserted.issues.Count == 0,
                    "DOOR_FRAME_RIGHT_INSERT_BENDS_CORE_REGRESSION_FAILED");

                DoorFrameRightCreateCutDetails(model, math);
                DoorFrameRightRemoveEndBendSectors(model, math);
                DoorFrameRightApplyReferenceBlocks(model, math);
                DoorFrameRightApplyReferenceFaceSplits(model, math);
                NativeRightRequire(model.ForceRebuild3(false),
                    "DOOR_FRAME_RIGHT_DETAILED_REBUILD_FAILED");
            }
            finally
            {
                Release(thinExtrusion);
                Release(topPlane);
                Release(math);
            }
        }

        private static void DoorFrameRightCreateCutDetails(ModelDoc2 model,
            MathUtility math)
        {
            var firstFlangeCuts = new List<NativeRightPoint3[]>
            {
                DoorFrameRightRectangleOnX(424.0, -1.0, 28.8, -18.6, -0.6),
                DoorFrameRightRectangleOnX(424.0, 1858.2, 1918.0, -18.6, -0.6)
            };
            for (int index = 0; index < 11; index++)
            {
                double centerY = 181.0 + 152.5 * index;
                firstFlangeCuts.Add(DoorFrameRightRectangleOnX(424.0,
                    centerY - 1.8, centerY + 1.8, -18.6, -16.7));
            }
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.4, 0.9, -0.01, 1, 0, 0),
                "DOOR_FRAME_RIGHT_FIRST_FLANGE_FACE_SELECT_FAILED");
            DoorFrameRightCreateCut(model, math, firstFlangeCuts,
                new NativeRightPoint3[0], new double[0], 0.0013,
                "DOOR_FRAME_RIGHT_FIRST_FLANGE_CUT_DETAILS");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.435, 0.9, 0.01, 0, 0, -1),
                "DOOR_FRAME_RIGHT_UPPER_BRIDGE_FACE_SELECT_FAILED");
            DoorFrameRightCreateCut(model, math, new[]
            {
                DoorFrameRightRectangleOnZ(0.0, 423.9, 426.0, 28.2, 28.8),
                DoorFrameRightRectangleOnZ(0.0, 423.9, 426.0, 1858.2, 1858.8)
            }, new NativeRightPoint3[0], new double[0], 0.0013,
                "DOOR_FRAME_RIGHT_UPPER_BRIDGE_RELIEFS");

            NativeRightPoint3[][] lastFlangeCuts =
            {
                DoorFrameRightRectangleOnX(429.0, -1.0, 13.8, -44.4, -34.9),
                DoorFrameRightRectangleOnX(429.0, 1903.2, 1918.0, -44.4, -34.9)
            };
            var holes = new List<NativeRightPoint3>();
            var radii = new List<double>();
            for (int index = 0; index < 11; index++)
            {
                holes.Add(new NativeRightPoint3(429.0,
                    104.75 + 152.5 * index, -39.0));
                radii.Add(2.75);
            }
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.4, 0.9, -0.04, 1, 0, 0),
                "DOOR_FRAME_RIGHT_LAST_FLANGE_FACE_SELECT_FAILED");
            DoorFrameRightCreateCut(model, math, lastFlangeCuts,
                holes, radii, 0.0013,
                "DOOR_FRAME_RIGHT_LAST_FLANGE_CUT_DETAILS");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.435, 0.9, -0.05, 0, 0, 1),
                "DOOR_FRAME_RIGHT_LOWER_BRIDGE_FACE_SELECT_FAILED");
            DoorFrameRightCreateCut(model, math, new[]
            {
                DoorFrameRightRectangleOnZ(-45.0, 428.9, 431.0, 13.2, 13.8),
                DoorFrameRightRectangleOnZ(-45.0, 428.9, 431.0, 1903.2, 1903.8)
            }, new NativeRightPoint3[0], new double[0], 0.0013,
                "DOOR_FRAME_RIGHT_LOWER_BRIDGE_RELIEFS");
        }

        private static NativeRightPoint3[] DoorFrameRightRectangleOnX(double x,
            double y1, double y2, double z1, double z2)
        {
            return new[]
            {
                new NativeRightPoint3(x, y1, z1),
                new NativeRightPoint3(x, y2, z1),
                new NativeRightPoint3(x, y2, z2),
                new NativeRightPoint3(x, y1, z2)
            };
        }

        private static NativeRightPoint3[] DoorFrameRightRectangleOnZ(double z,
            double x1, double x2, double y1, double y2)
        {
            return new[]
            {
                new NativeRightPoint3(x1, y1, z),
                new NativeRightPoint3(x2, y1, z),
                new NativeRightPoint3(x2, y2, z),
                new NativeRightPoint3(x1, y2, z)
            };
        }

        private static void DoorFrameRightCreateCut(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightPoint3[]> polygons,
            IList<NativeRightPoint3> holes, IList<double> radiiMm,
            double depth, string label)
        {
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                foreach (NativeRightPoint3[] polygon in polygons)
                    for (int index = 0; index < polygon.Length; index++)
                    {
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[index]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[(index + 1) % polygon.Length]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            label + "_LINE_FAILED");
                    }
                for (int index = 0; index < holes.Count; index++)
                {
                    NativeRightPoint3 center = FrameCrossbarToActiveSketchPoint(
                        model, math, holes[index]);
                    NativeRightRequire(model.SketchManager.CreateCircleByRadius(
                        center.x, center.y, 0, radiiMm[index] / 1000.0) != null,
                        label + "_CIRCLE_FAILED");
                }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature cut = model.FeatureManager.FeatureCut3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_CUT_FAILED");
            Release(cut);
            model.ClearSelection2(true);
        }

        private static void DoorFrameRightRemoveEndBendSectors(ModelDoc2 model,
            MathUtility math)
        {
            Feature plane = null;
            try
            {
                plane = FrameCrossbarFindDefaultPlane(model, 1);
                DoorFrameRightCutBendSector(model, math, plane,
                    DoorFrameRightFirstBendSector(0.0), 0.0288, true,
                    "DOOR_FRAME_RIGHT_LOWER_FIRST_BEND_SECTOR");
                Release(plane);
                plane = FrameCrossbarFindDefaultPlane(model, 1);
                DoorFrameRightCutBendSector(model, math, plane,
                    DoorFrameRightLastBendSector(0.0), 0.0138, true,
                    "DOOR_FRAME_RIGHT_LOWER_LAST_BEND_SECTOR");
                Release(plane);
                plane = DoorFrameRightCreateYOffsetPlane(model, 1917.0);
                DoorFrameRightCutBendSector(model, math, plane,
                    DoorFrameRightFirstBendSector(1917.0), 0.0588, false,
                    "DOOR_FRAME_RIGHT_UPPER_FIRST_BEND_SECTOR");
                Release(plane);
                plane = DoorFrameRightCreateYOffsetPlane(model, 1917.0);
                DoorFrameRightCutBendSector(model, math, plane,
                    DoorFrameRightLastBendSector(1917.0), 0.0138, false,
                    "DOOR_FRAME_RIGHT_UPPER_LAST_BEND_SECTOR");
            }
            finally { Release(plane); }
        }

        private static FrameCrossbarBendSector DoorFrameRightFirstBendSector(
            double y)
        {
            return new FrameCrossbarBendSector(
                new NativeRightPoint3(425.4, y, -1.4),
                new NativeRightPoint3(424.0, y, -1.4),
                new NativeRightPoint3(425.4, y, 0.0),
                new NativeRightPoint3(425.4, y, -1.2),
                new NativeRightPoint3(425.2, y, -1.4));
        }

        private static FrameCrossbarBendSector DoorFrameRightLastBendSector(
            double y)
        {
            return new FrameCrossbarBendSector(
                new NativeRightPoint3(430.4, y, -43.6),
                new NativeRightPoint3(430.4, y, -45.0),
                new NativeRightPoint3(429.0, y, -43.6),
                new NativeRightPoint3(430.2, y, -43.6),
                new NativeRightPoint3(430.4, y, -43.8));
        }

        private static Feature DoorFrameRightCreateYOffsetPlane(ModelDoc2 model,
            double yMm)
        {
            Feature basePlane = FrameCrossbarFindDefaultPlane(model, 1);
            try
            {
                List<double> transform = FrameCrossbarPlaneTransform(basePlane);
                double normalY = transform.Count >= 9 ? transform[7] : 0;
                model.ClearSelection2(true);
                NativeRightRequire(basePlane.Select2(false, 0),
                    "DOOR_FRAME_RIGHT_TOP_PLANE_RESELECT_FAILED");
                int constraint =
                    (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
                if (normalY < 0) constraint |=
                    (int)swRefPlaneReferenceConstraints_e.
                        swRefPlaneReferenceConstraint_OptionFlip;
                Feature plane = model.FeatureManager.InsertRefPlane(
                    constraint, Math.Abs(yMm) / 1000.0, 0, 0, 0, 0) as Feature;
                NativeRightRequire(plane != null,
                    "DOOR_FRAME_RIGHT_Y_OFFSET_PLANE_CREATE_FAILED");
                return plane;
            }
            finally { Release(basePlane); }
        }

        private static void DoorFrameRightCutBendSector(ModelDoc2 model,
            MathUtility math, Feature plane, FrameCrossbarBendSector sector,
            double depth, bool reverse, string label)
        {
            model.ClearSelection2(true);
            NativeRightRequire(plane != null && plane.Select2(false, 0),
                label + "_PLANE_SELECT_FAILED");
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                NativeRightPoint3 center = FrameCrossbarToActiveSketchPoint(
                    model, math, sector.center);
                NativeRightPoint3 outerStart = FrameCrossbarToActiveSketchPoint(
                    model, math, sector.outer_start);
                NativeRightPoint3 outerEnd = FrameCrossbarToActiveSketchPoint(
                    model, math, sector.outer_end);
                NativeRightPoint3 innerStart = FrameCrossbarToActiveSketchPoint(
                    model, math, sector.inner_start);
                NativeRightPoint3 innerEnd = FrameCrossbarToActiveSketchPoint(
                    model, math, sector.inner_end);
                FrameCrossbarCreateQuarterArc(model, center, outerStart,
                    outerEnd, FrameCrossbarDistance(center, outerStart),
                    label + "_OUTER_ARC");
                NativeRightRequire(model.SketchManager.CreateLine(outerEnd.x,
                    outerEnd.y, 0, innerStart.x, innerStart.y, 0) != null,
                    label + "_END_RADIAL_FAILED");
                FrameCrossbarCreateQuarterArc(model, center, innerStart,
                    innerEnd, FrameCrossbarDistance(center, innerStart),
                    label + "_INNER_ARC");
                NativeRightRequire(model.SketchManager.CreateLine(innerEnd.x,
                    innerEnd.y, 0, outerStart.x, outerStart.y, 0) != null,
                    label + "_START_RADIAL_FAILED");
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature cut = model.FeatureManager.FeatureCut3(true, false, reverse,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_CUT_FAILED");
            Release(cut);
            model.ClearSelection2(true);
        }

        private static void DoorFrameRightApplyReferenceBlocks(ModelDoc2 model,
            MathUtility math)
        {
            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.435, 0.9, 0.01, 0, 0, -1),
                "DOOR_FRAME_RIGHT_UPPER_REFERENCE_FACE_SELECT_FAILED");
            DoorFrameRightCreateBoss(model, math, new[]
            {
                DoorFrameRightRectangleOnZ(0.0, 424.0, 425.5, 0.0, 28.2),
                DoorFrameRightRectangleOnZ(0.0, 424.0, 425.5, 1858.8, 1917.0)
            }, 0.0012, true, "DOOR_FRAME_RIGHT_UPPER_REFERENCE_BLOCKS");

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model,
                0.435, 0.9, -0.05, 0, 0, 1),
                "DOOR_FRAME_RIGHT_LOWER_REFERENCE_FACE_SELECT_FAILED");
            DoorFrameRightCreateBoss(model, math, new[]
            {
                DoorFrameRightRectangleOnZ(-45.0, 429.0, 430.5, 0.0, 13.2),
                DoorFrameRightRectangleOnZ(-45.0, 429.0, 430.5, 1903.8, 1917.0)
            }, 0.0012, true, "DOOR_FRAME_RIGHT_LOWER_REFERENCE_BLOCKS");
        }

        private static void DoorFrameRightCreateBoss(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightPoint3[]> polygons,
            double depth, bool flip, string label)
        {
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                foreach (NativeRightPoint3[] polygon in polygons)
                    for (int index = 0; index < polygon.Length; index++)
                    {
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[index]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[(index + 1) % polygon.Length]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            label + "_LINE_FAILED");
                    }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature boss = model.FeatureManager.FeatureExtrusion2(true, flip,
                true, (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(boss != null && boss.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_BOSS_FAILED");
            Release(boss);
            model.ClearSelection2(true);
        }

        private static void DoorFrameRightApplyReferenceFaceSplits(ModelDoc2 model,
            MathUtility math)
        {
            DoorFrameRightCreateProjectionSplit(model, math, 28.7, new[]
            {
                new[]
                {
                    new NativeRightPoint3(424.0, 28.7, -1.4),
                    new NativeRightPoint3(425.2, 28.7, -1.4)
                },
                new[]
                {
                    new NativeRightPoint3(425.4, 28.7, -1.2),
                    new NativeRightPoint3(425.4, 28.7, 0.0)
                }
            }, 0.425, 0.020, -0.010, 0, 1, 0,
                "DOOR_FRAME_RIGHT_LOWER_FIRST_FLANGE_FACE_SPLIT");
            DoorFrameRightCreateProjectionSplit(model, math, 1858.3, new[]
            {
                new[]
                {
                    new NativeRightPoint3(424.0, 1858.3, -1.4),
                    new NativeRightPoint3(425.2, 1858.3, -1.4)
                },
                new[]
                {
                    new NativeRightPoint3(425.4, 1858.3, -1.2),
                    new NativeRightPoint3(425.4, 1858.3, 0.0)
                }
            }, 0.425, 1.900, -0.010, 0, -1, 0,
                "DOOR_FRAME_RIGHT_UPPER_FIRST_FLANGE_FACE_SPLIT");
            DoorFrameRightCreateProjectionSplit(model, math, 13.7, new[]
            {
                new[]
                {
                    new NativeRightPoint3(429.0, 13.7, -43.6),
                    new NativeRightPoint3(430.2, 13.7, -43.6)
                },
                new[]
                {
                    new NativeRightPoint3(430.4, 13.7, -45.0),
                    new NativeRightPoint3(430.4, 13.7, -43.8)
                }
            }, 0.430, 0.005, -0.040, 0, 1, 0,
                "DOOR_FRAME_RIGHT_LOWER_LAST_FLANGE_FACE_SPLIT");
            DoorFrameRightCreateProjectionSplit(model, math, 1903.3, new[]
            {
                new[]
                {
                    new NativeRightPoint3(429.0, 1903.3, -43.6),
                    new NativeRightPoint3(430.2, 1903.3, -43.6)
                },
                new[]
                {
                    new NativeRightPoint3(430.4, 1903.3, -45.0),
                    new NativeRightPoint3(430.4, 1903.3, -43.8)
                }
            }, 0.430, 1.910, -0.040, 0, -1, 0,
                "DOOR_FRAME_RIGHT_UPPER_LAST_FLANGE_FACE_SPLIT");
        }

        private static void DoorFrameRightCreateProjectionSplit(ModelDoc2 model,
            MathUtility math, double planeYMm,
            IEnumerable<NativeRightPoint3[]> segments,
            double rayX, double rayY, double rayZ,
            double rayDx, double rayDy, double rayDz, string label)
        {
            Feature plane = DoorFrameRightCreateYOffsetPlane(model, planeYMm);
            Feature sketch = null;
            try
            {
                model.ClearSelection2(true);
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    label + "_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (NativeRightPoint3[] segment in segments)
                    {
                        NativeRightRequire(segment != null && segment.Length == 2,
                            label + "_SEGMENT_INVALID");
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, segment[0]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, segment[1]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            label + "_LINE_FAILED");
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketch = NativeRightLastRootFeatureOfType(model,
                    "ProfileFeature");
                model.ClearSelection2(true);
                NativeRightRequire(sketch != null && sketch.Select2(false, 4),
                    label + "_SKETCH_MARK_FAILED");
                NativeRightRequire(DoorFrameRightSelectFaceByRayMark(model,
                    rayX, rayY, rayZ, rayDx, rayDy, rayDz, true, 1),
                    label + "_TARGET_FACE_MARK_FAILED");
                model.InsertSplitLineProject(false, false);
                NativeRightRequire(model.ForceRebuild3(false),
                    label + "_REBUILD_FAILED");
            }
            finally
            {
                Release(sketch);
                Release(plane);
                model.ClearSelection2(true);
            }
        }

        private static bool DoorFrameRightSelectFaceByRayMark(ModelDoc2 model,
            double x, double y, double z, double dx, double dy, double dz,
            bool append, int mark)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                return extension != null && extension.SelectByRay(x, y, z,
                    dx, dy, dz, 0.001, (int)swSelectType_e.swSelFACES,
                    append, mark, 0);
            }
            finally { Release(extension); }
        }

        private static bool NativeDoorFrameRightGeometryMatchesContract(
            NativeRightGeometrySnapshot geometry)
        {
            double[] box = { 424.0, 0.0, -45.0, 444.0, 1917.0, 0.0 };
            double[] radii =
            {
                0.2, 0.2, 0.2, 0.2,
                1.4, 1.4, 1.4, 1.4,
                2.75, 2.75, 2.75, 2.75, 2.75, 2.75,
                2.75, 2.75, 2.75, 2.75, 2.75
            };
            return geometry != null && geometry.bodyCount == 1 && geometry.finite &&
                geometry.bodyBoxMm.Count == 6 && box.Select((value, index) =>
                    Near(geometry.bodyBoxMm[index], value,
                        NativeDoorFrameRightBoxToleranceMm)).All(value => value) &&
                Near(geometry.volumeMm3, 232924.52059047756,
                    NativeDoorFrameRightVolumeToleranceMm3) &&
                Near(geometry.surfaceAreaMm2, 393348.7176599304,
                    NativeDoorFrameRightAreaToleranceMm2) &&
                Near(geometry.massKg, 1.828457486635249,
                    NativeDoorFrameRightMassToleranceKg) &&
                geometry.faceCount == 109 && geometry.edgeCount == 278 &&
                geometry.loopCount == 142 &&
                geometry.loopEdgeReferenceCount == 556 &&
                geometry.cylinderFaceCount == 19 &&
                geometry.circleEdgeReferenceCount == 76 &&
                NativeRightRadiiMatch(geometry.cylinderRadiiMm, radii) &&
                string.Equals(geometry.translationNormalizedDetailedSignature,
                    NativeDoorFrameRightDetailedSignature,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool NativeDoorFrameRightFeatureMatchesContract(
            NativeRightFeatureSnapshot value)
        {
            return value != null && value.sheetMetalCount == 1 &&
                value.oneBendCount == 4 && value.flatPatternCount == 1 &&
                value.flattenBendsCount == 1 && value.processBendsCount == 1 &&
                value.mirrorPartCount == 0 && value.forbiddenImportFeatureCount == 0 &&
                value.sketchedBendGroupCount == 0 && value.bends.Count == 4 &&
                value.issues.Count == 0 &&
                Near(value.sheetMetalThicknessMm, 1.2, 0.0001) &&
                Near(value.sheetMetalRadiusMm, 0.2, 0.0001) &&
                Near(value.sheetMetalKFactor, 0.333333, 0.000001) &&
                value.sheetMetalAllowanceType ==
                    (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                value.bends.All(bend => bend.bendType ==
                    (int)swBendType_e.swSharpBend && !bend.suppressed &&
                    Near(bend.angleRadians, Math.PI / 2.0, 0.000001) &&
                    Near(bend.radiusMm, 0.2, 0.0001) &&
                    Near(bend.kFactor, 0.333333, 0.000001) &&
                    bend.allowanceType ==
                        (int)swBendAllowanceTypes_e.swBendAllowanceKFactor &&
                    bend.canonicalDirection == 2 && bend.canonicalDown);
        }

        private static void BuildNativeFrameCrossbarRight888(ISldWorks application,
            ModelDoc2 model)
        {
            MathUtility math = application.GetMathUtility() as MathUtility;
            Feature startPlane = null;
            Feature rightEndPlane = null;
            Feature thinExtrusion = null;
            try
            {
                NativeRightRequire(math != null,
                    "FRAME_CROSSBAR_MATH_UTILITY_UNAVAILABLE");
                startPlane = FrameCrossbarCreateXOffsetPlane(model, 36.5);
                NativeRightRequire(startPlane != null && startPlane.Select2(false, 0),
                    "FRAME_CROSSBAR_START_PLANE_CREATE_OR_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                NativeRightPoint3[] path =
                {
                    new NativeRightPoint3(36.5, 1692.5, -19.1),
                    new NativeRightPoint3(36.5, 1705.1, -19.1),
                    new NativeRightPoint3(36.5, 1705.1, -0.6),
                    new NativeRightPoint3(36.5, 1706.9, -0.6),
                    new NativeRightPoint3(36.5, 1706.9, -19.7)
                };
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    for (int index = 0; index < path.Length - 1; index++)
                    {
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, path[index]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, path[index + 1]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            "FRAME_CROSSBAR_OPEN_PROFILE_LINE_FAILED");
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                Feature profile = NativeRightLastRootFeatureOfType(model,
                    "ProfileFeature");
                NativeRightRequire(profile != null && profile.Select2(false, 0),
                    "FRAME_CROSSBAR_PROFILE_SELECT_FAILED");
                thinExtrusion = model.FeatureManager.FeatureExtrusionThin2(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    0.389, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true,
                    0.0012, 0.0, 0.0,
                    (int)swThinWallType_e.swThinWallMidPlane, 0, false, 0.0,
                    false, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                Release(profile);
                NativeRightRequire(thinExtrusion != null &&
                    thinExtrusion.GetErrorCode() == 0 && model.ForceRebuild3(false),
                    "FRAME_CROSSBAR_THIN_EXTRUSION_FAILED");

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model, 0.244,
                    1.8, -0.01, 0, -1, 0),
                    "FRAME_CROSSBAR_FIXED_FACE_SELECT_FAILED");
                PartDoc part = model as PartDoc;
                NativeRightRequire(part != null && part.InsertBends2(0.0002,
                    "", 0.333333, 0.0, true, 0.333333, true) &&
                    model.ForceRebuild3(false),
                    "FRAME_CROSSBAR_INSERT_BENDS2_FAILED");

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model, 0.2,
                    1.8, -0.01, 0, -1, 0),
                    "FRAME_CROSSBAR_UPPER_PLATE_FACE_SELECT_FAILED");
                FrameCrossbarCreateCut(model, math, new[]
                {
                    FrameCrossbarRectangle(36.5, -19.7, 64.5, -18.5, 1707.5),
                    FrameCrossbarRectangle(36.5, -16.7, 38.5, -1.4, 1707.5),
                    FrameCrossbarRectangle(423.5, -16.7, 425.5, -1.4, 1707.5)
                }, new NativeRightPoint3[0], new double[0], 0.03,
                    "FRAME_CROSSBAR_THROUGH_SECTION_END_DETAILS");

                FrameCrossbarCutBendSectors(model, math, startPlane, new[]
                {
                    FrameCrossbarBottomBendSector(36.5)
                }, 0.028, true, "FRAME_CROSSBAR_LEFT_BOTTOM_BEND_TRIM");
                FrameCrossbarCutBendSectors(model, math, startPlane, new[]
                {
                    FrameCrossbarUpperBendSector(36.5),
                    FrameCrossbarLowerUpperBendSector(36.5)
                }, 0.002, true, "FRAME_CROSSBAR_LEFT_UPPER_BEND_TRIMS");
                rightEndPlane = FrameCrossbarCreateXOffsetPlane(model, 425.5);
                FrameCrossbarCutBendSectors(model, math, rightEndPlane, new[]
                {
                    FrameCrossbarUpperBendSector(425.5),
                    FrameCrossbarLowerUpperBendSector(425.5)
                }, 0.002, false, "FRAME_CROSSBAR_RIGHT_UPPER_BEND_TRIMS");

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model, 0.2,
                    1.8, -0.01, 0, -1, 0),
                    "FRAME_CROSSBAR_UPPER_PLATE_RESELECT_FAILED");
                FrameCrossbarCreateCut(model, math, new NativeRightPoint3[0][],
                    new[] { new NativeRightPoint3(411.0, 1707.5, -7.0) },
                    new[] { 3.1 }, 0.0013, "FRAME_CROSSBAR_UPPER_HOLE");

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model, 0.2,
                    1.7060, -0.01, 0, -1, 0),
                    "FRAME_CROSSBAR_LOWER_PLATE_FACE_SELECT_FAILED");
                FrameCrossbarCreateCut(model, math, new NativeRightPoint3[0][],
                    new[] { new NativeRightPoint3(411.0, 1705.7, -7.0) },
                    new[] { 3.5 }, 0.0013, "FRAME_CROSSBAR_LOWER_HOLE");

                NativeRightRequire(FrameCrossbarSelectFaceByRay(model, 0.2,
                    1.6955, -0.03, 0, 0, 1),
                    "FRAME_CROSSBAR_VERTICAL_PLATE_FACE_SELECT_FAILED");
                FrameCrossbarCreateCut(model, math, new[]
                {
                    new[]
                    {
                        new NativeRightPoint3(36.5, 1692.5, -19.7),
                        new NativeRightPoint3(65.5, 1692.5, -19.7),
                        new NativeRightPoint3(64.5, 1693.5, -19.7),
                        new NativeRightPoint3(64.5, 1704.3, -19.7),
                        new NativeRightPoint3(36.5, 1704.3, -19.7)
                    }
                }, new[]
                {
                    new NativeRightPoint3(69.0, 1695.5, -19.7),
                    new NativeRightPoint3(244.0, 1695.5, -19.7),
                    new NativeRightPoint3(419.0, 1695.5, -19.7)
                }, new[] { 1.75, 1.75, 1.75 }, 0.0013,
                    "FRAME_CROSSBAR_VERTICAL_PLATE_DETAILS");

                FrameCrossbarApplyBooleanDifferenceCorrections(model, math);
                NativeRightRequire(model.ForceRebuild3(false),
                    "FRAME_CROSSBAR_DETAILED_REBUILD_FAILED");
            }
            finally
            {
                Release(thinExtrusion);
                Release(rightEndPlane);
                Release(startPlane);
                Release(math);
            }
        }

        private static Feature FrameCrossbarFindDefaultPlane(ModelDoc2 model,
            int normalAxis)
        {
            Feature feature = model.FirstFeature() as Feature;
            Feature selected = null;
            int guard = 0;
            while (feature != null && guard++ < 200)
            {
                Feature next = feature.GetNextFeature() as Feature;
                if (string.Equals(feature.GetTypeName2(), "RefPlane",
                    StringComparison.OrdinalIgnoreCase))
                {
                    List<double> values = FrameCrossbarPlaneTransform(feature);
                    bool origin = values.Count >= 12 &&
                        Near(values[9], 0, 1e-10) && Near(values[10], 0, 1e-10) &&
                        Near(values[11], 0, 1e-10);
                    bool normal = values.Count >= 9 &&
                        Math.Abs(values[6 + normalAxis]) > 0.999999 &&
                        Enumerable.Range(0, 3).Where(index => index != normalAxis)
                            .All(index => Math.Abs(values[6 + index]) < 0.000001);
                    if (origin && normal)
                    {
                        NativeRightRequire(selected == null,
                            "FRAME_CROSSBAR_MULTIPLE_DEFAULT_PLANES_FOUND");
                        selected = feature;
                        feature = next;
                        continue;
                    }
                }
                Release(feature);
                feature = next;
            }
            NativeRightRequire(feature == null,
                "FRAME_CROSSBAR_DEFAULT_PLANE_TRAVERSAL_GUARD_EXCEEDED");
            NativeRightRequire(selected != null,
                "FRAME_CROSSBAR_DEFAULT_RIGHT_PLANE_MISSING");
            return selected;
        }

        private static List<double> FrameCrossbarPlaneTransform(Feature feature)
        {
            RefPlane plane = feature == null ? null : feature.GetSpecificFeature2() as RefPlane;
            MathTransform transform = null;
            try
            {
                transform = plane == null ? null : plane.Transform;
                return transform == null ? new List<double>() :
                    NativeRightDoubleList(transform.ArrayData);
            }
            finally { Release(transform); Release(plane); }
        }

        private static Feature FrameCrossbarCreateXOffsetPlane(ModelDoc2 model,
            double xMm)
        {
            Feature basePlane = FrameCrossbarFindDefaultPlane(model, 0);
            try
            {
                List<double> transform = FrameCrossbarPlaneTransform(basePlane);
                double normalX = transform.Count >= 9 ? transform[6] : 0;
                NativeRightRequire(basePlane.Select2(false, 0),
                    "FRAME_CROSSBAR_RIGHT_PLANE_SELECT_FAILED");
                int constraint =
                    (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
                if (normalX < 0) constraint |=
                    (int)swRefPlaneReferenceConstraints_e.
                        swRefPlaneReferenceConstraint_OptionFlip;
                Feature plane = model.FeatureManager.InsertRefPlane(
                    constraint, Math.Abs(xMm) / 1000.0, 0, 0, 0, 0) as Feature;
                NativeRightRequire(plane != null,
                    "FRAME_CROSSBAR_X_OFFSET_PLANE_CREATE_FAILED");
                return plane;
            }
            finally { Release(basePlane); }
        }

        private static NativeRightPoint3 FrameCrossbarToActiveSketchPoint(
            ModelDoc2 model, MathUtility math, NativeRightPoint3 modelPointMm)
        {
            Sketch sketch = model.SketchManager.ActiveSketch;
            MathTransform transform = null;
            MathPoint modelPoint = null;
            MathPoint sketchPoint = null;
            try
            {
                NativeRightRequire(sketch != null,
                    "FRAME_CROSSBAR_ACTIVE_SKETCH_MISSING");
                transform = sketch.ModelToSketchTransform;
                NativeRightRequire(transform != null,
                    "FRAME_CROSSBAR_MODEL_TO_SKETCH_TRANSFORM_MISSING");
                modelPoint = math.CreatePoint(new[]
                {
                    modelPointMm.x / 1000.0,
                    modelPointMm.y / 1000.0,
                    modelPointMm.z / 1000.0
                }) as MathPoint;
                NativeRightRequire(modelPoint != null,
                    "FRAME_CROSSBAR_MODEL_POINT_CREATE_FAILED");
                sketchPoint = modelPoint.MultiplyTransform(transform) as MathPoint;
                double[] values = sketchPoint == null ? null :
                    sketchPoint.ArrayData as double[];
                NativeRightRequire(values != null && values.Length >= 3 &&
                    values.All(NativeRightIsFinite),
                    "FRAME_CROSSBAR_SKETCH_POINT_TRANSFORM_FAILED");
                return new NativeRightPoint3(values[0], values[1], values[2]);
            }
            finally
            {
                Release(sketchPoint);
                Release(modelPoint);
                Release(transform);
                Release(sketch);
            }
        }

        private static bool FrameCrossbarSelectFaceByRay(ModelDoc2 model,
            double x, double y, double z, double dx, double dy, double dz)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                model.ClearSelection2(true);
                return extension != null && extension.SelectByRay(x, y, z,
                    dx, dy, dz, 0.001, (int)swSelectType_e.swSelFACES,
                    false, 0, 0);
            }
            finally { Release(extension); }
        }

        private static NativeRightPoint3[] FrameCrossbarRectangle(double x1,
            double z1, double x2, double z2, double y)
        {
            return new[]
            {
                new NativeRightPoint3(x1, y, z1),
                new NativeRightPoint3(x2, y, z1),
                new NativeRightPoint3(x2, y, z2),
                new NativeRightPoint3(x1, y, z2)
            };
        }

        private static NativeRightPoint3[] FrameCrossbarEndRectangle(double x,
            double y1, double z1, double y2, double z2)
        {
            return new[]
            {
                new NativeRightPoint3(x, y1, z1),
                new NativeRightPoint3(x, y2, z1),
                new NativeRightPoint3(x, y2, z2),
                new NativeRightPoint3(x, y1, z2)
            };
        }

        private static void FrameCrossbarCreateCut(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightPoint3[]> polygons,
            IList<NativeRightPoint3> holes, IList<double> radiiMm,
            double depth, string label)
        {
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                foreach (NativeRightPoint3[] polygon in polygons)
                    for (int index = 0; index < polygon.Length; index++)
                    {
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[index]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[(index + 1) % polygon.Length]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            label + "_LINE_FAILED");
                    }
                for (int index = 0; index < holes.Count; index++)
                {
                    NativeRightPoint3 center = FrameCrossbarToActiveSketchPoint(
                        model, math, holes[index]);
                    NativeRightRequire(model.SketchManager.CreateCircleByRadius(
                        center.x, center.y, 0, radiiMm[index] / 1000.0) != null,
                        label + "_CIRCLE_FAILED");
                }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature cut = model.FeatureManager.FeatureCut3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_CUT_FAILED");
            Release(cut);
            model.ClearSelection2(true);
        }

        private static FrameCrossbarBendSector FrameCrossbarBottomBendSector(
            double x)
        {
            return new FrameCrossbarBendSector(
                new NativeRightPoint3(x, 1704.3, -18.3),
                new NativeRightPoint3(x, 1705.7, -18.3),
                new NativeRightPoint3(x, 1704.3, -19.7),
                new NativeRightPoint3(x, 1704.5, -18.3),
                new NativeRightPoint3(x, 1704.3, -18.5));
        }

        private static FrameCrossbarBendSector FrameCrossbarUpperBendSector(
            double x)
        {
            return new FrameCrossbarBendSector(
                new NativeRightPoint3(x, 1706.1, -1.4),
                new NativeRightPoint3(x, 1707.5, -1.4),
                new NativeRightPoint3(x, 1706.1, 0.0),
                new NativeRightPoint3(x, 1706.3, -1.4),
                new NativeRightPoint3(x, 1706.1, -1.2));
        }

        private static FrameCrossbarBendSector
            FrameCrossbarLowerUpperBendSector(double x)
        {
            return new FrameCrossbarBendSector(
                new NativeRightPoint3(x, 1705.9, -1.4),
                new NativeRightPoint3(x, 1704.5, -1.4),
                new NativeRightPoint3(x, 1705.9, 0.0),
                new NativeRightPoint3(x, 1705.7, -1.4),
                new NativeRightPoint3(x, 1705.9, -1.2));
        }

        private static void FrameCrossbarCutBendSectors(ModelDoc2 model,
            MathUtility math, Feature plane, IEnumerable<FrameCrossbarBendSector> sectors,
            double depth, bool reverse, string label)
        {
            NativeRightRequire(plane != null && plane.Select2(false, 0),
                label + "_PLANE_SELECT_FAILED");
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                foreach (FrameCrossbarBendSector sector in sectors)
                {
                    NativeRightPoint3 center = FrameCrossbarToActiveSketchPoint(
                        model, math, sector.center);
                    NativeRightPoint3 outerStart = FrameCrossbarToActiveSketchPoint(
                        model, math, sector.outer_start);
                    NativeRightPoint3 outerEnd = FrameCrossbarToActiveSketchPoint(
                        model, math, sector.outer_end);
                    NativeRightPoint3 innerStart = FrameCrossbarToActiveSketchPoint(
                        model, math, sector.inner_start);
                    NativeRightPoint3 innerEnd = FrameCrossbarToActiveSketchPoint(
                        model, math, sector.inner_end);
                    FrameCrossbarCreateQuarterArc(model, center, outerStart,
                        outerEnd, FrameCrossbarDistance(center, outerStart),
                        label + "_OUTER_ARC");
                    NativeRightRequire(model.SketchManager.CreateLine(outerEnd.x,
                        outerEnd.y, 0, innerEnd.x, innerEnd.y, 0) != null,
                        label + "_END_RADIAL_FAILED");
                    FrameCrossbarCreateQuarterArc(model, center, innerEnd,
                        innerStart, FrameCrossbarDistance(center, innerStart),
                        label + "_INNER_ARC");
                    NativeRightRequire(model.SketchManager.CreateLine(innerStart.x,
                        innerStart.y, 0, outerStart.x, outerStart.y, 0) != null,
                        label + "_START_RADIAL_FAILED");
                }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature cut = model.FeatureManager.FeatureCut3(true, false, reverse,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_CUT_FAILED");
            Release(cut);
            model.ClearSelection2(true);
        }

        private static void FrameCrossbarCreateQuarterArc(ModelDoc2 model,
            NativeRightPoint3 center, NativeRightPoint3 start,
            NativeRightPoint3 end, double radius, string label)
        {
            SketchSegment arc = model.SketchManager.CreateArc(center.x,
                center.y, 0, start.x, start.y, 0, end.x, end.y, 0, (short)1);
            double expected = Math.PI * radius / 2.0;
            if (arc == null || Math.Abs(arc.GetLength() - expected) > 0.000001)
            {
                if (arc != null)
                {
                    arc.Select4(false, null);
                    model.EditDelete();
                    model.ClearSelection2(true);
                    Release(arc);
                }
                arc = model.SketchManager.CreateArc(center.x, center.y, 0,
                    start.x, start.y, 0, end.x, end.y, 0, (short)-1);
            }
            NativeRightRequire(arc != null &&
                Math.Abs(arc.GetLength() - expected) <= 0.000001,
                label + "_NOT_QUARTER_ARC");
            Release(arc);
        }

        private static double FrameCrossbarDistance(NativeRightPoint3 left,
            NativeRightPoint3 right)
        {
            double x = left.x - right.x;
            double y = left.y - right.y;
            return Math.Sqrt(x * x + y * y);
        }

        private static void FrameCrossbarApplyBooleanDifferenceCorrections(
            ModelDoc2 model, MathUtility math)
        {
            Feature leftPlane = FrameCrossbarCreateXOffsetPlane(model, 36.5);
            Feature rightPlane = null;
            Feature lowerPlane = null;
            try
            {
                FrameCrossbarCutEndPlanePolygons(model, math, leftPlane,
                    new[]
                    {
                        FrameCrossbarEndRectangle(36.5, 1705.9, -1.2,
                            1706.1, 0.0)
                    }, 0.002, true, "FRAME_CROSSBAR_LEFT_TOP_BOOLEAN_CORRECTION");
                rightPlane = FrameCrossbarCreateXOffsetPlane(model, 425.5);
                FrameCrossbarCutEndPlanePolygons(model, math, rightPlane,
                    new[]
                    {
                        FrameCrossbarEndRectangle(425.5, 1705.9, -1.2,
                            1706.1, 0.0)
                    }, 0.002, false, "FRAME_CROSSBAR_RIGHT_TOP_BOOLEAN_CORRECTION");
                lowerPlane = FrameCrossbarCreateXOffsetPlane(model, 64.5);
                FrameCrossbarCutEndPlanePolygons(model, math, lowerPlane,
                    new[]
                    {
                        FrameCrossbarEndRectangle(64.5, 1704.5, -18.3,
                            1705.7, -17.7)
                    }, 0.0006, false,
                    "FRAME_CROSSBAR_LOWER_BOOLEAN_CORRECTION");
            }
            finally
            {
                Release(lowerPlane);
                Release(rightPlane);
                Release(leftPlane);
            }

            NativeRightRequire(FrameCrossbarSelectFaceByRay(model, 0.05,
                1.7051, -0.0180, 0, 0, -1),
                "FRAME_CROSSBAR_REFERENCE_BLOCK_FACE_SELECT_FAILED");
            FrameCrossbarCreateBoss(model, math, new[]
            {
                new NativeRightPoint3(36.5, 1704.5, -18.3),
                new NativeRightPoint3(63.9, 1704.5, -18.3),
                new NativeRightPoint3(63.9, 1705.7, -18.3),
                new NativeRightPoint3(36.5, 1705.7, -18.3)
            }, 0.0002, false, "FRAME_CROSSBAR_REFERENCE_BOOLEAN_BLOCK");
        }

        private static void FrameCrossbarCutEndPlanePolygons(ModelDoc2 model,
            MathUtility math, Feature plane, IEnumerable<NativeRightPoint3[]> polygons,
            double depth, bool reverse, string label)
        {
            NativeRightRequire(plane != null && plane.Select2(false, 0),
                label + "_PLANE_SELECT_FAILED");
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                foreach (NativeRightPoint3[] polygon in polygons)
                    for (int index = 0; index < polygon.Length; index++)
                    {
                        NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[index]);
                        NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                            model, math, polygon[(index + 1) % polygon.Length]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) != null,
                            label + "_LINE_FAILED");
                    }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature cut = model.FeatureManager.FeatureCut3(true, false, reverse,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, false, true, true,
                false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_CUT_FAILED");
            Release(cut);
            model.ClearSelection2(true);
        }

        private static void FrameCrossbarCreateBoss(ModelDoc2 model,
            MathUtility math, NativeRightPoint3[] polygon, double depth,
            bool flip, string label)
        {
            model.SketchManager.InsertSketch(true);
            model.SetAddToDB(true);
            model.SetDisplayWhenAdded(false);
            try
            {
                for (int index = 0; index < polygon.Length; index++)
                {
                    NativeRightPoint3 start = FrameCrossbarToActiveSketchPoint(
                        model, math, polygon[index]);
                    NativeRightPoint3 end = FrameCrossbarToActiveSketchPoint(
                        model, math, polygon[(index + 1) % polygon.Length]);
                    NativeRightRequire(model.SketchManager.CreateLine(start.x,
                        start.y, 0, end.x, end.y, 0) != null,
                        label + "_LINE_FAILED");
                }
            }
            finally
            {
                NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                NativeRightTryAction(() => model.SetAddToDB(false));
            }
            model.SketchManager.InsertSketch(true);
            Feature sketch = NativeRightLastRootFeatureOfType(model,
                "ProfileFeature");
            NativeRightRequire(sketch != null && sketch.Select2(false, 0),
                label + "_SKETCH_SELECT_FAILED");
            Feature boss = model.FeatureManager.FeatureExtrusion2(true, flip,
                false, (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0.0, false, false, false, false, 0.0, 0.0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
            Release(sketch);
            NativeRightRequire(boss != null && boss.GetErrorCode() == 0 &&
                model.ForceRebuild3(false), label + "_BOSS_FAILED");
            Release(boss);
            model.ClearSelection2(true);
        }

        private static DerivedPlan LegacyRightTopCoverMirrorPlan(DerivedPlan nativePlan)
        {
            return new DerivedPlan
            {
                file_name = nativePlan.file_name,
                source_file_name = nativePlan.source_file_name,
                link_type = "MirrorStock",
                source_xmin_mm = nativePlan.source_xmin_mm,
                source_xmax_mm = nativePlan.source_xmax_mm,
                target_xmin_mm = nativePlan.target_xmin_mm,
                target_xmax_mm = nativePlan.target_xmax_mm
            };
        }

        private static void ValidateNativeRightEvidenceInputs(string repositoryRoot,
            DerivedPartRecord record)
        {
            string rulePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeRightRuleRelativePath);
            string evidencePath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeRightR34EvidenceRelativePath);
            string receiptPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeRightR34ReceiptRelativePath);
            string partPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeRightR34PartRelativePath);
            string authorizationPath = ResolveSafeRepositoryFile(repositoryRoot,
                NativeRightR34AuthorizationRelativePath);
            Require(!string.IsNullOrWhiteSpace(rulePath) &&
                string.Equals(Sha256(rulePath), NativeRightRuleSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(evidencePath) &&
                string.Equals(Sha256(evidencePath), NativeRightR34EvidenceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(receiptPath) &&
                string.Equals(Sha256(receiptPath), NativeRightR34ReceiptSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(partPath) &&
                string.Equals(Sha256(partPath), NativeRightR34PartSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(authorizationPath) &&
                string.Equals(Sha256(authorizationPath), NativeRightR34AuthorizationSha256,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_RIGHT_R34_INPUT_HASH_INVALID", 32,
                "fixed R34 rule, evidence, receipt, part, or authorization input changed");
            Dictionary<string, object> evidence = ReadJsonObject(evidencePath);
            Dictionary<string, object> receipt = ReadJsonObject(receiptPath);
            Require(BoolValue(evidence, "pilotPass") && BoolValue(receipt, "success") &&
                string.Equals(TextValue(evidence, "ruleSha256"), NativeRightRuleSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(evidence, "outputSha256"), NativeRightR34PartSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(evidence, "authorizationSha256"),
                    NativeRightR34AuthorizationSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "evidenceSha256"),
                    NativeRightR34EvidenceSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "outputSha256"), NativeRightR34PartSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "authorizationSha256"),
                    NativeRightR34AuthorizationSha256, StringComparison.OrdinalIgnoreCase),
                "NATIVE_RIGHT_R34_EVIDENCE_RECEIPT_BINDING_INVALID", 32,
                "fixed R34 evidence, receipt, output, rule, and authorization bindings disagree");
            record.native_right_evidence_inputs_gate = true;
        }

        private static void BuildNativeRight888SharpInsertBends(ISldWorks application,
            ModelDoc2 model, DerivedPartRecord record)
        {
            MathUtility math = application.GetMathUtility() as MathUtility;
            Feature startPlane = null;
            Feature endPlane = null;
            Feature thinExtrusion = null;
            try
            {
                NativeRightRequire(math != null, "SHARP_STOCK_MATH_UTILITY_UNAVAILABLE");
                startPlane = NativeRightCreateFrontOffsetPlane(model, -549.0);
                NativeRightRequire(startPlane != null, "SHARP_STOCK_START_PLANE_CREATE_FAILED");
                endPlane = NativeRightCreateFrontOffsetPlane(model, -47.0);
                NativeRightRequire(endPlane != null, "SHARP_STOCK_END_PLANE_CREATE_FAILED");

                NativeRightRequire(startPlane.Select2(false, 0),
                    "SHARP_STOCK_START_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                NativeRightPoint3[] path =
                {
                    new NativeRightPoint3(320.8, 1894.4, -549.0),
                    new NativeRightPoint3(330.4, 1894.4, -549.0),
                    new NativeRightPoint3(330.4, 1916.6, -549.0),
                    new NativeRightPoint3(379.6, 1916.6, -549.0),
                    new NativeRightPoint3(379.6, 1839.6, -549.0),
                    new NativeRightPoint3(365.4, 1839.6, -549.0),
                    new NativeRightPoint3(365.4, 1851.2, -549.0)
                };
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    for (int index = 0; index < path.Length - 1; index++)
                    {
                        NativeRightPoint3 start = NativeRightToActiveSketchPoint(model, math, path[index]);
                        NativeRightPoint3 end = NativeRightToActiveSketchPoint(model, math, path[index + 1]);
                        NativeRightRequire(model.SketchManager.CreateLine(start.x, start.y, 0,
                            end.x, end.y, 0) != null,
                            "SHARP_STOCK_OPEN_PROFILE_LINE_FAILED");
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                Feature profile = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(profile != null && profile.Select2(false, 0),
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
                NativeRightRequire(thinExtrusion != null && thinExtrusion.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), "SHARP_STOCK_THIN_EXTRUSION_FAILED");

                NativeRightCreateBlindRectangleCut(model, math, startPlane, -549.0,
                    320.8, 1894.0, 330.0, 1894.8, 60.7, true,
                    "SHARP_STOCK_BOTTOM_TERMINAL_RELIEF");
                NativeRightCreateBlindRectangleCut(model, math, startPlane, -549.0,
                    330.0, 1894.0, 330.8, 1916.2, 50.2, true,
                    "SHARP_STOCK_BOTTOM_VERTICAL_RELIEF");
                NativeRightCreateBlindRectangleCut(model, math, startPlane, -549.0,
                    365.0, 1840.0, 365.8, 1851.2, 13.2, true,
                    "SHARP_STOCK_BOTTOM_RETURN_RELIEF");
                NativeRightCreateBlindRectangleCut(model, math, endPlane, -47.0,
                    320.8, 1894.0, 330.0, 1894.8, 10.7, false,
                    "SHARP_STOCK_TOP_TERMINAL_RELIEF");
                NativeRightCreateBlindRectangleCut(model, math, endPlane, -47.0,
                    330.0, 1894.0, 330.8, 1916.2, 0.2, false,
                    "SHARP_STOCK_TOP_VERTICAL_RELIEF");

                NativeRightRequire(NativeRightSelectFaceByRay(model, 0.5, 1.8642, -0.3,
                    -1, 0, 0), "SHARP_STOCK_MAIN_HOLE_FACE_SELECT_FAILED");
                NativeRightCreateThroughHolesOnSelectedFace(model, math, new[]
                {
                    new NativeRightHolePoint(380.0, 1864.2, -448.0, 1.0),
                    new NativeRightHolePoint(380.0, 1864.2, -148.0, 1.0)
                }, "SHARP_STOCK_MAIN_HOLES");
                NativeRightRequire(NativeRightSelectFaceByRay(model, 0.34, 1.847, -0.3,
                    1, 0, 0), "SHARP_STOCK_RETURN_HOLE_FACE_SELECT_FAILED");
                NativeRightCreateThroughHolesOnSelectedFace(model, math, new[]
                {
                    new NativeRightHolePoint(365.0, 1847.0, -65.0, 2.75),
                    new NativeRightHolePoint(365.0, 1847.0, -215.0, 2.75),
                    new NativeRightHolePoint(365.0, 1847.0, -365.0, 2.75),
                    new NativeRightHolePoint(365.0, 1847.0, -515.0, 2.75)
                }, "SHARP_STOCK_RETURN_HOLES");
                NativeRightRequire(NativeRightSelectFaceByRay(model, 0.2, 1.906, -0.3,
                    1, 0, 0), "SHARP_STOCK_INNER_HOLE_FACE_SELECT_FAILED");
                NativeRightCreateThroughHolesOnSelectedFace(model, math, new[]
                {
                    new NativeRightHolePoint(330.0, 1911.1, -489.0, 3.25),
                    new NativeRightHolePoint(330.0, 1906.0, -85.0, 5.0)
                }, "SHARP_STOCK_INNER_HOLES");

                NativeRightRequire(model.ForceRebuild3(false), "SHARP_STOCK_FINAL_REBUILD_FAILED");
                record.native_right_sharp_stock = CaptureNativeRightGeometry(model);
                record.native_right_sharp_stock_gate =
                    NativeRightSharpStockMatchesContract(record.native_right_sharp_stock);
                NativeRightRequire(record.native_right_sharp_stock_gate,
                    "NATIVE_RIGHT_SHARP_STOCK_GEOMETRY_REGRESSION_FAILED");

                NativeRightRequire(NativeRightSelectFixedFaceByRay(model, false, 0),
                    "INSERT_BENDS_FIXED_FACE_SELECT_FAILED");
                PartDoc part = model as PartDoc;
                NativeRightRequire(part != null && part.InsertBends2(0.0005, "", 0.5, 0.0,
                    true, 0.5, true), "NATIVE_INSERT_BENDS2_FAILED");
                NativeRightRequire(model.ForceRebuild3(false),
                    "NATIVE_INSERT_BENDS2_REBUILD_FAILED");
                record.native_right_insert_bends_features = CaptureNativeRightFeatures(model);
                record.native_right_insert_bends_geometry = CaptureNativeRightGeometry(model);
                record.native_right_insert_bends_gate =
                    NativeRightInsertBendsCoreMatches(record.native_right_insert_bends_features);
                NativeRightRequire(record.native_right_insert_bends_gate,
                    "NATIVE_RIGHT_INSERT_BENDS_CORE_REGRESSION_FAILED");
                NativeRightCreatePostFeatures(model, math);
            }
            finally
            {
                Release(thinExtrusion);
                Release(endPlane);
                Release(startPlane);
                Release(math);
            }
        }

        private static bool NativeRightSharpStockMatchesContract(
            NativeRightGeometrySnapshot geometry)
        {
            double[] box = { 384.8, 1839.2, -549.0, 444.0, 1917.0, -47.0 };
            double[] radii = { 1.0, 1.0, 2.75, 2.75, 2.75, 2.75, 3.25, 5.0 };
            return geometry != null && geometry.bodyCount == 1 && geometry.finite &&
                geometry.bodyBoxMm.Count == 6 && box.Select((value, index) =>
                    Near(geometry.bodyBoxMm[index], value,
                        NativeRightBoxToleranceMm)).All(value => value) &&
                Near(geometry.volumeMm3, 72104.76859853993,
                    NativeRightVolumeToleranceMm3) &&
                Near(geometry.surfaceAreaMm2, 181466.01564657912,
                    NativeRightAreaToleranceMm2) &&
                Near(geometry.massKg, 0.5660224334985384,
                    NativeRightMassToleranceKg) &&
                geometry.faceCount == 29 && geometry.edgeCount == 73 &&
                geometry.loopCount == 53 && geometry.loopEdgeReferenceCount == 146 &&
                geometry.cylinderFaceCount == 8 &&
                geometry.circleEdgeReferenceCount == 32 &&
                NativeRightRadiiMatch(geometry.cylinderRadiiMm, radii);
        }

        private static bool NativeRightInsertBendsCoreMatches(
            NativeRightFeatureSnapshot value)
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

        private static bool NativeRightGeometryMatchesContract(
            NativeRightGeometrySnapshot geometry)
        {
            double[] box = { 384.8, 1839.2, -549.0, 444.0, 1917.0, -47.0 };
            if (geometry == null || geometry.bodyCount != 1 || !geometry.finite ||
                geometry.bodyBoxMm.Count != 6 ||
                !box.Select((value, index) => Near(geometry.bodyBoxMm[index], value,
                    NativeRightBoxToleranceMm)).All(value => value) ||
                !Near(geometry.volumeMm3, 71396.31748296092,
                    NativeRightVolumeToleranceMm3) ||
                !Near(geometry.surfaceAreaMm2, 179242.4290120627,
                    NativeRightAreaToleranceMm2) ||
                !Near(geometry.massKg, 0.5604610922412432,
                    NativeRightMassToleranceKg) ||
                geometry.faceCount != 135 || geometry.edgeCount != 324 ||
                geometry.loopCount != 188 ||
                geometry.loopEdgeReferenceCount != 648 ||
                geometry.cylinderFaceCount != 27 ||
                geometry.circleEdgeReferenceCount != 108 ||
                !string.Equals(geometry.translationNormalizedDetailedSignature,
                    NativeRightR34DetailedSignature,
                    StringComparison.OrdinalIgnoreCase)) return false;
            double[] radii =
            {
                0.5, 0.5, 0.5, 0.5, 0.5, 1, 1,
                1.3, 1.3, 1.3, 1.3, 1.3, 2.75, 2.75, 2.75, 2.75,
                3, 3, 3, 3.25, 4.4, 4.4, 4.4, 5, 5.5, 5.5, 5.5
            };
            return NativeRightRadiiMatch(geometry.cylinderRadiiMm, radii);
        }

        private static bool NativeRightFeatureMatchesContract(
            NativeRightFeatureSnapshot value)
        {
            return NativeRightInsertBendsCoreMatches(value) &&
                value.mirrorPartCount == 0 &&
                value.forbiddenImportFeatureCount == 0 &&
                value.sketchedBendGroupCount == 0;
        }

        private static void NativeRightRequire(bool condition, string code)
        {
            Require(condition, code, 36, code);
        }

        private static bool NativeRightTryAction(Action action)
        {
            try { action(); return true; }
            catch { return false; }
        }

        private static Feature NativeRightCreateFrontOffsetPlane(ModelDoc2 model, double offsetMm)
        {
            string name;
            List<double> transform;
            NativeRightRequire(NativeRightSelectDefaultFrontPlane(model, out name, out transform),
                "DEFAULT_FRONT_PLANE_SELECT_FAILED");
            int constraint =
                (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
            if (offsetMm < 0.0)
                constraint |= (int)swRefPlaneReferenceConstraints_e.
                    swRefPlaneReferenceConstraint_OptionFlip;
            Feature plane = model.FeatureManager.InsertRefPlane(
                constraint, Math.Abs(offsetMm) / 1000.0, 0, 0, 0, 0) as Feature;
            List<double> values = NativeRightRefPlaneTransform(plane);
            NativeRightRequire(plane != null && values.Count >= 12 &&
                Near(values[9], 0.0, 0.000001) &&
                Near(values[10], 0.0, 0.000001) &&
                Near(values[11], offsetMm / 1000.0, 0.000001),
                "FRONT_OFFSET_PLANE_TRANSFORM_INVALID");
            return plane;
        }

        private static void NativeRightCreateBlindRectangleCut(ModelDoc2 model, MathUtility math,
            Feature plane, double zMm, double x1Mm, double y1Mm, double x2Mm,
            double y2Mm, double depthMm, bool reverseDirection, string failurePrefix)
        {
            Feature cut = null;
            Feature sketchFeature = null;
            try
            {
                model.ClearSelection2(true);
                NativeRightRequire(plane != null && plane.Select2(false, 0),
                    failurePrefix + "_PLANE_SELECT_FAILED");
                model.SketchManager.InsertSketch(true);
                NativeRightPoint3[] points =
                {
                    new NativeRightPoint3(x1Mm, y1Mm, zMm),
                    new NativeRightPoint3(x2Mm, y1Mm, zMm),
                    new NativeRightPoint3(x2Mm, y2Mm, zMm),
                    new NativeRightPoint3(x1Mm, y2Mm, zMm)
                };
                List<SketchSegment> rectangle = new List<SketchSegment>();
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    for (int index = 0; index < points.Length; index++)
                    {
                        NativeRightPoint3 start = NativeRightToActiveSketchPoint(model, math, points[index]);
                        NativeRightPoint3 end = NativeRightToActiveSketchPoint(model, math,
                            points[(index + 1) % points.Length]);
                        SketchSegment segment = model.SketchManager.CreateLine(start.x,
                            start.y, 0, end.x, end.y, 0) as SketchSegment;
                        NativeRightRequire(segment != null, failurePrefix + "_LINE_FAILED_" +
                            index.ToString(CultureInfo.InvariantCulture) + "_" +
                            start.Key() + "_" + end.Key());
                        rectangle.Add(segment);
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                    foreach (SketchSegment segment in rectangle) Release(segment);
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketchFeature != null && sketchFeature.Select2(false, 0),
                    failurePrefix + "_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, reverseDirection,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depthMm / 1000.0, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), failurePrefix + "_CUT_FAILED");
            }
            finally
            {
                Release(cut);
                Release(sketchFeature);
                model.ClearSelection2(true);
            }
        }

        private static void NativeRightCreateThroughHolesOnSelectedFace(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightHolePoint> holes, string failurePrefix)
        {
            Feature cut = null;
            Feature sketchFeature = null;
            try
            {
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (NativeRightHolePoint hole in holes)
                    {
                        NativeRightPoint3 center = NativeRightToActiveSketchPoint(model, math,
                            new NativeRightPoint3(hole.xMm, hole.yMm, hole.zMm));
                        NativeRightRequire(model.SketchManager.CreateCircleByRadius(center.x, center.y,
                            0, hole.radiusMm / 1000.0) != null,
                            failurePrefix + "_CIRCLE_FAILED");
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketchFeature != null && sketchFeature.Select2(false, 0),
                    failurePrefix + "_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    0.001, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
                    model.ForceRebuild3(false), failurePrefix + "_CUT_FAILED");
            }
            finally
            {
                Release(cut);
                Release(sketchFeature);
                model.ClearSelection2(true);
            }
        }

        private static bool NativeRightSelectFaceByRay(ModelDoc2 model, double x, double y,
            double z, double dx, double dy, double dz)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                model.ClearSelection2(true);
                return extension != null && extension.SelectByRay(x + NativeRightDeltaXMm / 1000.0, y, z, dx, dy, dz,
                    0.001, (int)swSelectType_e.swSelFACES, false, 0, 0);
            }
            finally { Release(extension); }
        }


        private static void NativeRightCreatePostFeatures(ModelDoc2 model,
            MathUtility math)
        {
            NativeRightRequire(NativeRightSelectFaceByRay(model, 0.355, 2.0, -0.15, 0, -1, 0),
                "POST_TOP_FACE_SELECT_FAILED");
            NativeRightCreateCutProfileOnSelectedFace(model, math, new[]
            {
                new[]
                {
                    new NativeRightPoint3(375, 1917, -180), new NativeRightPoint3(375, 1917, -120),
                    new NativeRightPoint3(358, 1917, -120), new NativeRightPoint3(358, 1917, -118),
                    new NativeRightPoint3(356, 1917, -118), new NativeRightPoint3(356, 1917, -122),
                    new NativeRightPoint3(373, 1917, -122), new NativeRightPoint3(373, 1917, -178),
                    new NativeRightPoint3(356, 1917, -178), new NativeRightPoint3(356, 1917, -182),
                    new NativeRightPoint3(358, 1917, -182), new NativeRightPoint3(358, 1917, -180)
                },
                new[]
                {
                    new NativeRightPoint3(337, 1917, -178), new NativeRightPoint3(337, 1917, -122),
                    new NativeRightPoint3(354, 1917, -122), new NativeRightPoint3(354, 1917, -118),
                    new NativeRightPoint3(352, 1917, -118), new NativeRightPoint3(352, 1917, -120),
                    new NativeRightPoint3(335, 1917, -120), new NativeRightPoint3(335, 1917, -180),
                    new NativeRightPoint3(352, 1917, -180), new NativeRightPoint3(352, 1917, -182),
                    new NativeRightPoint3(354, 1917, -182), new NativeRightPoint3(354, 1917, -178)
                }
            }, new[]
            {
                new NativeRightHolePoint(355, 1917, -100, 4.4),
                new NativeRightHolePoint(355, 1917, -200, 4.4),
                new NativeRightHolePoint(355, 1917, -420, 4.4)
            }, 1.0, false, "POST_TOP_CUTS");

            NativeRightRequire(NativeRightSelectFaceByRay(model, 0.5, 1.88, -0.15, -1, 0, 0),
                "POST_OUTER_FACE_SELECT_FAILED");
            NativeRightCreateCutProfileOnSelectedFace(model, math, new[]
            {
                new[]
                {
                    new NativeRightPoint3(380, 1869, -132), new NativeRightPoint3(380, 1886, -132),
                    new NativeRightPoint3(380, 1886, -136), new NativeRightPoint3(380, 1884, -136),
                    new NativeRightPoint3(380, 1884, -134), new NativeRightPoint3(380, 1867, -134),
                    new NativeRightPoint3(380, 1867, -74), new NativeRightPoint3(380, 1884, -74),
                    new NativeRightPoint3(380, 1884, -72), new NativeRightPoint3(380, 1886, -72),
                    new NativeRightPoint3(380, 1886, -76), new NativeRightPoint3(380, 1869, -76)
                },
                new[]
                {
                    new NativeRightPoint3(380, 1907, -134), new NativeRightPoint3(380, 1890, -134),
                    new NativeRightPoint3(380, 1890, -136), new NativeRightPoint3(380, 1888, -136),
                    new NativeRightPoint3(380, 1888, -132), new NativeRightPoint3(380, 1905, -132),
                    new NativeRightPoint3(380, 1905, -76), new NativeRightPoint3(380, 1888, -76),
                    new NativeRightPoint3(380, 1888, -72), new NativeRightPoint3(380, 1890, -72),
                    new NativeRightPoint3(380, 1890, -74), new NativeRightPoint3(380, 1907, -74)
                }
            }, new NativeRightHolePoint[0], 1.0, false, "POST_OUTER_CUTS");

            NativeRightRequire(NativeRightSelectFaceByRay(model, 0.355, 1.9, -0.3, 0, 1, 0),
                "POST_PEM_UNDERSIDE_FACE_SELECT_FAILED");
            NativeRightCreatePemBossesOnSelectedFace(model, math, new[]
            {
                new NativeRightPoint3(355, 1916.2, -100),
                new NativeRightPoint3(355, 1916.2, -200),
                new NativeRightPoint3(355, 1916.2, -420)
            });
            NativeRightRequire(model.ForceRebuild3(false), "POST_FEATURES_REBUILD_FAILED");
        }

        private static void NativeRightCreateCutProfileOnSelectedFace(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightPoint3[]> loops, IEnumerable<NativeRightHolePoint> holes,
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
                    foreach (NativeRightPoint3[] loop in loops)
                    {
                        NativeRightRequire(loop != null && loop.Length >= 3,
                            failurePrefix + "_LOOP_INVALID");
                        for (int index = 0; index < loop.Length; index++)
                        {
                            NativeRightPoint3 start = NativeRightToActiveSketchPoint(model, math, loop[index]);
                            NativeRightPoint3 end = NativeRightToActiveSketchPoint(model, math,
                                loop[(index + 1) % loop.Length]);
                            SketchSegment segment = model.SketchManager.CreateLine(start.x,
                                start.y, 0, end.x, end.y, 0) as SketchSegment;
                            NativeRightRequire(segment != null, failurePrefix + "_LINE_FAILED");
                            segments.Add(segment);
                        }
                    }
                    foreach (NativeRightHolePoint hole in holes)
                    {
                        NativeRightPoint3 center = NativeRightToActiveSketchPoint(model, math,
                            new NativeRightPoint3(hole.xMm, hole.yMm, hole.zMm));
                        SketchSegment circle = model.SketchManager.CreateCircleByRadius(
                            center.x, center.y, 0, hole.radiusMm / 1000.0) as SketchSegment;
                        NativeRightRequire(circle != null, failurePrefix + "_CIRCLE_FAILED");
                        segments.Add(circle);
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketchFeature != null && sketchFeature.Select2(false, 0),
                    failurePrefix + "_SKETCH_SELECT_FAILED");
                cut = model.FeatureManager.FeatureCut3(true, false, reverseDirection,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depthMm / 1000.0, 0.0, false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true,
                    false, false, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                NativeRightRequire(cut != null && cut.GetErrorCode() == 0 &&
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

        private static void NativeRightCreatePemBossesOnSelectedFace(ModelDoc2 model,
            MathUtility math, IEnumerable<NativeRightPoint3> centers)
        {
            List<SketchSegment> segments = new List<SketchSegment>();
            Feature sketchFeature = null;
            Feature boss = null;
            try
            {
                model.SketchManager.InsertSketch(true);
                model.SetAddToDB(true);
                model.SetDisplayWhenAdded(false);
                try
                {
                    foreach (NativeRightPoint3 modelCenter in centers)
                    {
                        NativeRightPoint3 center = NativeRightToActiveSketchPoint(model, math,
                            modelCenter);
                        SketchSegment outer = model.SketchManager.CreateCircleByRadius(center.x,
                            center.y, 0, 0.0055) as SketchSegment;
                        SketchSegment inner = model.SketchManager.CreateCircleByRadius(center.x,
                            center.y, 0, 0.0030) as SketchSegment;
                        NativeRightRequire(outer != null && inner != null,
                            "POST_PEM_CIRCLE_CREATE_FAILED");
                        segments.Add(outer);
                        segments.Add(inner);
                    }
                }
                finally
                {
                    NativeRightTryAction(() => model.SetDisplayWhenAdded(true));
                    NativeRightTryAction(() => model.SetAddToDB(false));
                }
                model.SketchManager.InsertSketch(true);
                sketchFeature = NativeRightLastRootFeatureOfType(model, "ProfileFeature");
                NativeRightRequire(sketchFeature != null && sketchFeature.Select2(false, 0),
                    "POST_PEM_SKETCH_SELECT_FAILED");
                boss = model.FeatureManager.FeatureExtrusion2(true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind, 0.004, 0.0,
                    false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true, true, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false) as Feature;
                NativeRightRequire(boss != null && boss.GetErrorCode() == 0 &&
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

        private static bool NativeRightSelectDefaultFrontPlane(ModelDoc2 model,
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
                                new List<double>() : NativeRightDoubleList(transform.ArrayData);
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
                                NativeRightRequire(selected == null,
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

        private static List<double> NativeRightRefPlaneTransform(Feature feature)
        {
            RefPlane plane = feature == null ? null : feature.GetSpecificFeature2() as RefPlane;
            MathTransform transform = null;
            try
            {
                transform = plane == null ? null : plane.Transform;
                return transform == null ? new List<double>() :
                    NativeRightDoubleList(transform.ArrayData);
            }
            finally { Release(transform); Release(plane); }
        }

        private static Feature NativeRightLastRootFeatureOfType(ModelDoc2 model, string typeName)
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
            NativeRightRequire(feature == null, "LAST_ROOT_FEATURE_TRAVERSAL_GUARD_EXCEEDED");
            return output;
        }

        private static NativeRightPoint3 NativeRightToActiveSketchPoint(ModelDoc2 model, MathUtility math,
            NativeRightPoint3 modelPointMm)
        {
            Sketch sketch = model.SketchManager.ActiveSketch;
            MathTransform transform = null;
            MathPoint modelPoint = null;
            MathPoint sketchPoint = null;
            try
            {
                NativeRightRequire(sketch != null, "ACTIVE_SKETCH_MISSING");
                transform = sketch.ModelToSketchTransform;
                NativeRightRequire(transform != null, "MODEL_TO_SKETCH_TRANSFORM_MISSING");
                modelPoint = math.CreatePoint(new double[]
                {
                    (modelPointMm.x + NativeRightDeltaXMm) / 1000.0,
                    modelPointMm.y / 1000.0,
                    modelPointMm.z / 1000.0
                }) as MathPoint;
                NativeRightRequire(modelPoint != null, "MODEL_MATH_POINT_CREATE_FAILED");
                sketchPoint = modelPoint.MultiplyTransform(transform) as MathPoint;
                double[] values = sketchPoint == null ? null :
                    sketchPoint.ArrayData as double[];
                NativeRightRequire(values != null && values.Length >= 3 && values.All(NativeRightIsFinite),
                    "SKETCH_POINT_TRANSFORM_FAILED");
                return new NativeRightPoint3(values[0], values[1], values[2]);
            }
            finally
            {
                Release(sketchPoint);
                Release(modelPoint);
                Release(transform);
                Release(sketch);
            }
        }

        private static bool NativeRightSelectFixedFaceByRay(ModelDoc2 model, bool append, int mark,
            bool fromBelow = false)
        {
            ModelDocExtension extension = model.Extension;
            try
            {
                return extension != null && extension.SelectByRay(0.355 + NativeRightDeltaXMm / 1000.0,
                    fromBelow ? 1.8 : 2.0, -0.3, 0,
                    fromBelow ? 1.0 : -1.0, 0, 0.001,
                    (int)swSelectType_e.swSelFACES, append, mark, 0);
            }
            finally { Release(extension); }
        }


        private static NativeRightGeometrySnapshot CaptureNativeRightGeometry(ModelDoc2 model)
        {
            NativeRightGeometrySnapshot output = new NativeRightGeometrySnapshot();
            PartDoc part = model as PartDoc;
            Array bodies = null;
            NativeRightRequire(part != null, "GEOMETRY_DOCUMENT_NOT_PART");
            try
            {
                bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
                output.bodyCount = bodies == null ? 0 : bodies.Length;
                NativeRightRequire(output.bodyCount == 1, "GEOMETRY_SOLID_BODY_COUNT_INVALID");
                Body2 body = bodies.GetValue(0) as Body2;
                try
                {
                    NativeRightRequire(body != null, "GEOMETRY_SOLID_BODY_MISSING");
                    output.bodyBoxMm = NativeRightDoubleList((object)body.GetBodyBox()).Select(value =>
                        value * 1000.0).ToList();
                    output.faceCount = body.GetFaceCount();
                    output.edgeCount = body.GetEdgeCount();
                    List<double> mass = NativeRightDoubleList(body.GetMassProperties(7850.0));
                    NativeRightRequire(mass.Count >= 6 && mass.All(NativeRightIsFinite),
                        "GEOMETRY_MASS_PROPERTIES_INVALID");
                    output.volumeMm3 = mass[3] * 1000000000.0;
                    output.surfaceAreaMm2 = mass[4] * 1000000.0;
                    output.massKg = mass[5];
                    Array faces = body.GetFaces() as Array;
                    foreach (object rawFace in faces ?? new object[0])
                    {
                        Face2 face = rawFace as Face2;
                        CaptureNativeRightDetailedFace(face, output);
                    }
                }
                finally { Release(body); }
            }
            finally { }
            output.cylinderFaceCount = output.cylinders.Count;
            output.cylinderRadiiMm = output.cylinders.Select(value => value.radiusMm)
                .OrderBy(value => value).ToList();
            output.translationNormalizedDetailedSignature =
                NativeRightDetailedGeometrySignature(output);
            output.finite = output.bodyBoxMm.Count == 6 &&
                output.bodyBoxMm.All(NativeRightIsFinite) && NativeRightIsFinite(output.volumeMm3) &&
                NativeRightIsFinite(output.surfaceAreaMm2) && NativeRightIsFinite(output.massKg) &&
                output.cylinders.All(value => value.Finite) &&
                output.faces.Count == output.faceCount &&
                output.loopCount == output.faces.Sum(value => value.loops.Count) &&
                output.loopEdgeReferenceCount == output.faces.Sum(value =>
                    value.loops.Sum(loop => loop.edges.Count)) &&
                Regex.IsMatch(output.translationNormalizedDetailedSignature ?? "",
                    "^[A-F0-9]{64}$", RegexOptions.CultureInvariant);
            return output;
        }

        private static void CaptureNativeRightDetailedFace(Face2 face, NativeRightGeometrySnapshot output)
        {
            NativeRightRequire(face != null, "GEOMETRY_FACE_INVALID");
            Surface surface = null;
            try
            {
                NativeRightDetailedFace detail = new NativeRightDetailedFace();
                detail.areaMm2 = face.GetArea() * 1000000.0;
                detail.boxMm = NativeRightDoubleList((object)face.GetBox()).Select(value =>
                    value * 1000.0).ToList();
                detail.normal = NativeRightDoubleList(face.Normal);
                NativeRightRequire(NativeRightIsFinite(detail.areaMm2) && detail.boxMm.Count == 6 &&
                    detail.boxMm.All(NativeRightIsFinite) && detail.normal.Count == 3 &&
                    detail.normal.All(NativeRightIsFinite), "DETAILED_FACE_NONFINITE");
                surface = face.GetSurface() as Surface;
                NativeRightRequire(surface != null, "DETAILED_FACE_SURFACE_NULL");
                detail.surfaceIdentity = surface.Identity();
                detail.isPlane = surface.IsPlane();
                detail.isCylinder = surface.IsCylinder();
                detail.planeParams = detail.isPlane ? NativeRightToListOrEmpty(surface.PlaneParams,
                    1.0) : new List<double>();
                detail.cylinderParams = detail.isCylinder ? NativeRightToListOrEmpty(
                    surface.CylinderParams, 1.0) : new List<double>();
                NativeRightRequire(detail.planeParams.All(NativeRightIsFinite) &&
                    detail.cylinderParams.All(NativeRightIsFinite),
                    "DETAILED_SURFACE_PARAMS_NONFINITE");
                if (detail.isCylinder)
                {
                    List<double> parameters = detail.cylinderParams;
                    NativeRightRequire(parameters.Count >= 7, "DETAILED_CYLINDER_PARAMS_INVALID");
                    output.cylinders.Add(new NativeRightCylinderSnapshot
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
                    NativeRightRequire(loop != null, "DETAILED_LOOP_NULL");
                    try
                    {
                        NativeRightDetailedLoop loopDetail = new NativeRightDetailedLoop
                            { isOuter = loop.IsOuter() };
                        Array edges = loop.GetEdges() as Array;
                        loopDetail.edgeCount = edges == null ? 0 : edges.Length;
                        output.loopEdgeReferenceCount += loopDetail.edgeCount;
                        foreach (object rawEdge in edges ?? new object[0])
                            loopDetail.edges.Add(CaptureNativeRightDetailedEdge(rawEdge as Edge,
                                output));
                        NativeRightRequire(loopDetail.edgeCount == loopDetail.edges.Count,
                            "DETAILED_LOOP_EDGE_COUNT_INVALID");
                        detail.loops.Add(loopDetail);
                    }
                    finally { Release(loop); }
                }
                output.faces.Add(detail);
            }
            finally { Release(surface); Release(face); }
        }

        private static NativeRightDetailedEdge CaptureNativeRightDetailedEdge(Edge edge,
            NativeRightGeometrySnapshot output)
        {
            NativeRightRequire(edge != null, "DETAILED_EDGE_NULL");
            Curve curve = null;
            try
            {
                curve = edge.GetCurve() as Curve;
                NativeRightRequire(curve != null, "DETAILED_EDGE_CURVE_NULL");
                NativeRightDetailedEdge detail = new NativeRightDetailedEdge();
                detail.curveType = curve.Identity();
                detail.isLine = curve.IsLine();
                detail.isCircle = curve.IsCircle();
                double minimum = 0.0, maximum = 0.0;
                bool periodic = false, closed = false;
                NativeRightRequire(curve.GetEndParams(out minimum, out maximum, out periodic,
                    out closed), "DETAILED_EDGE_END_PARAMS_UNRESOLVED");
                detail.uMin = minimum;
                detail.uMax = maximum;
                detail.lengthMm = curve.GetLength3(minimum, maximum) * 1000.0;
                detail.startPointMm = NativeRightEdgePoint(curve, minimum);
                detail.endPointMm = NativeRightEdgePoint(curve, maximum);
                detail.startVertexPointMm = NativeRightVertexPoint(edge.GetStartVertex() as Vertex);
                detail.endVertexPointMm = NativeRightVertexPoint(edge.GetEndVertex() as Vertex);
                detail.lineParams = detail.isLine ? NativeRightToListOrEmpty(curve.LineParams, 1.0) :
                    new List<double>();
                detail.circleParams = detail.isCircle ? NativeRightToListOrEmpty(curve.CircleParams,
                    1.0) : new List<double>();
                NativeRightRequire(NativeRightIsFinite(detail.uMin) && NativeRightIsFinite(detail.uMax) &&
                    NativeRightIsFinite(detail.lengthMm) && detail.lengthMm > 0 &&
                    detail.startPointMm.Count == 3 && detail.endPointMm.Count == 3 &&
                    detail.startPointMm.All(NativeRightIsFinite) && detail.endPointMm.All(NativeRightIsFinite) &&
                    detail.startVertexPointMm.All(NativeRightIsFinite) &&
                    detail.endVertexPointMm.All(NativeRightIsFinite) &&
                    detail.lineParams.All(NativeRightIsFinite) && detail.circleParams.All(NativeRightIsFinite),
                    "DETAILED_EDGE_NONFINITE");
                if (detail.isCircle) output.circleEdgeReferenceCount++;
                return detail;
            }
            finally { Release(curve); Release(edge); }
        }

        private static string NativeRightDetailedGeometrySignature(NativeRightGeometrySnapshot snapshot)
        {
            List<double> anchor = new List<double>
                { snapshot.bodyBoxMm[0], snapshot.bodyBoxMm[1], snapshot.bodyBoxMm[2] };
            List<string> faces = snapshot.faces.Select(face =>
                NativeRightDetailedFaceKey(face, anchor)).OrderBy(value => value,
                    StringComparer.Ordinal).ToList();
            return Sha256Bytes(Encoding.UTF8.GetBytes("faces=" +
                String.Join("|", faces)));
        }

        private static string NativeRightDetailedFaceKey(NativeRightDetailedFace face, List<double> anchor)
        {
            List<string> loops = face.loops.Select(loop =>
                (loop.isOuter ? "O" : "I") + ":" + String.Join(",",
                    loop.edges.Select(edge => NativeRightDetailedEdgeKey(edge, anchor)).OrderBy(
                        value => value, StringComparer.Ordinal))).OrderBy(value => value,
                            StringComparer.Ordinal).ToList();
            List<double> normal = NativeRightCanonicalAxis(face.normal);
            string surface = "surface=" + face.surfaceIdentity.ToString(
                    CultureInfo.InvariantCulture) + ",plane=" + NativeRightBoolKey(face.isPlane) +
                ",cylinder=" + NativeRightDetailedCylinderKey(face.cylinderParams, anchor);
            return "n=" + String.Join(",", normal.Select(NativeRightRoundDetailed)) + ";" +
                surface + ";loops=" + String.Join("/", loops);
        }

        private static string NativeRightDetailedEdgeKey(NativeRightDetailedEdge edge, List<double> anchor)
        {
            bool fullCircle = edge.isCircle &&
                (NativeRightSameDetailedPoint(edge.startPointMm, edge.endPointMm) ||
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
                    NativeRightPointKey(edge.startPointMm, anchor),
                    NativeRightPointKey(edge.endPointMm, anchor)
                }.OrderBy(value => value, StringComparer.Ordinal));
                vertices = String.Join(">", new[]
                {
                    NativeRightPointKey(edge.startVertexPointMm, anchor),
                    NativeRightPointKey(edge.endVertexPointMm, anchor)
                }.OrderBy(value => value, StringComparer.Ordinal));
            }
            return "t=" + edge.curveType.ToString(CultureInfo.InvariantCulture) +
                ",l=" + NativeRightBoolKey(edge.isLine) + ",c=" + NativeRightBoolKey(edge.isCircle) +
                ",len=" + NativeRightRoundDetailed(edge.lengthMm) + ",p=" + points +
                ",v=" + vertices + ",cp=" +
                NativeRightDetailedCylinderKey(edge.circleParams, anchor);
        }

        private static string NativeRightDetailedCylinderKey(List<double> parameters,
            List<double> anchor)
        {
            if (parameters == null || parameters.Count < 7) return "[]";
            List<double> axis = NativeRightCanonicalAxis(parameters.Skip(3).Take(3).ToList());
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
            return String.Join(",", perpendicular.Select(NativeRightRoundDetailed)) + ";a=" +
                String.Join(",", axis.Select(NativeRightRoundDetailed)) + ";r=" +
                NativeRightRoundDetailed(parameters[6] * 1000.0);
        }

        private static List<double> NativeRightCanonicalAxis(List<double> raw)
        {
            if (raw == null || raw.Count != 3 || raw.Any(value => !NativeRightIsFinite(value)))
                return new List<double>();
            double length = Math.Sqrt(raw.Sum(value => value * value));
            if (!NativeRightIsFinite(length) || length < 1e-12) return new List<double>();
            List<double> axis = raw.Select(value => value / length).ToList();
            double first = axis.First(value => Math.Abs(value) > 1e-12);
            if (first < 0) axis = axis.Select(value => -value).ToList();
            return axis;
        }

        private static string NativeRightPointKey(List<double> point, List<double> anchor)
        {
            if (point == null || point.Count != 3) return "[]";
            return String.Join(",", new[] { point[0] - anchor[0],
                point[1] - anchor[1], point[2] - anchor[2] }.Select(NativeRightRoundDetailed));
        }

        private static bool NativeRightSameDetailedPoint(List<double> left, List<double> right)
        {
            return left != null && right != null && left.Count == 3 && right.Count == 3 &&
                left.Select((value, index) => Near(value, right[index], 0.000001))
                    .All(value => value);
        }

        private static string NativeRightBoolKey(bool value)
        {
            return value ? "1" : "0";
        }

        private static string NativeRightRoundDetailed(double value)
        {
            double rounded = Math.Floor(value * 1000000.0 + 0.5) / 1000000.0;
            if (Math.Abs(rounded) < 0.0000005) rounded = 0;
            return rounded.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static List<double> NativeRightEdgePoint(Curve curve, double parameter)
        {
            try { return NativeRightFirstThree(curve.Evaluate(parameter)); }
            catch { return new List<double>(); }
        }

        private static List<double> NativeRightVertexPoint(Vertex vertex)
        {
            try { return vertex == null ? new List<double>() : NativeRightFirstThree(vertex.GetPoint()); }
            catch { return new List<double>(); }
            finally { Release(vertex); }
        }

        private static List<double> NativeRightFirstThree(object raw)
        {
            try
            {
                List<double> points = NativeRightDoubleList(raw).Select(value => value * 1000.0).ToList();
                return points.Count < 3 || !points.Take(3).All(NativeRightIsFinite) ?
                    new List<double>() : points.Take(3).ToList();
            }
            catch { return new List<double>(); }
        }

        private static List<double> NativeRightToListOrEmpty(object raw, double factor)
        {
            try { return NativeRightDoubleList(raw).Select(value => value * factor).ToList(); }
            catch { return new List<double>(); }
        }

        private static NativeRightFeatureSnapshot CaptureNativeRightFeatures(ModelDoc2 model)
        {
            NativeRightFeatureSnapshot output = new NativeRightFeatureSnapshot();
            Feature feature = model.FirstFeature() as Feature;
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureNativeRightFeatureRecursive(model, feature, output, 0);
                Feature next = feature.GetNextFeature() as Feature;
                Release(feature);
                feature = next;
            }
            NativeRightRequire(feature == null, "FEATURE_TRAVERSAL_GUARD_EXCEEDED");
            output.bends = output.bends.OrderBy(value => value.order).ToList();
            for (int index = 0; index < output.bends.Count; index++)
            {
                NativeRightBendSnapshot bend = output.bends[index];
                bend.canonicalDirection = bend.direction;
                bend.canonicalDown = bend.down;
            }
            output.pass = output.featureCount > 0 && output.issues.Count == 0 &&
                output.bends.Count == output.oneBendCount;
            return output;
        }

        private static void CaptureNativeRightFeatureRecursive(ModelDoc2 model, Feature feature,
            NativeRightFeatureSnapshot output, int depth)
        {
            NativeRightRequire(feature != null && depth <= 30, "FEATURE_RECURSION_INVALID");
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
                CaptureNativeRightSheetMetalDefinition(model, feature, output);
            }
            else if (string.Equals(type, "OneBend", StringComparison.OrdinalIgnoreCase))
            {
                output.oneBendCount++;
                NativeRightBendSnapshot bend = CaptureNativeRightOneBendDefinition(model, feature, name,
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
                CaptureNativeRightFeatureRecursive(model, child, output, depth + 1);
                Feature next = child.GetNextSubFeature() as Feature;
                Release(child);
                child = next;
            }
            NativeRightRequire(child == null, "FEATURE_CHILD_TRAVERSAL_GUARD_EXCEEDED");
        }

        private static void CaptureNativeRightSheetMetalDefinition(ModelDoc2 model, Feature feature,
            NativeRightFeatureSnapshot output)
        {
            object raw = feature.GetDefinition();
            ISheetMetalFeatureData data = raw as ISheetMetalFeatureData;
            CustomBendAllowance allowance = null;
            bool accessed = false;
            try
            {
                NativeRightRequire(data != null, "SHEET_METAL_DEFINITION_UNAVAILABLE");
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

        private static NativeRightBendSnapshot CaptureNativeRightOneBendDefinition(ModelDoc2 model, Feature feature,
            string name, bool suppressed)
        {
            NativeRightBendSnapshot output = new NativeRightBendSnapshot { name = name, suppressed = suppressed };
            object raw = feature.GetDefinition();
            IOneBendFeatureData data = raw as IOneBendFeatureData;
            CustomBendAllowance allowance = null;
            bool accessed = false;
            try
            {
                NativeRightRequire(data != null, "ONE_BEND_DEFINITION_UNAVAILABLE");
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


        private static bool NativeRightRadiiMatch(List<double> actual, double[] expected)
        {
            if (actual == null || actual.Count != expected.Length) return false;
            List<double> ordered = actual.OrderBy(value => value).ToList();
            for (int index = 0; index < expected.Length; index++)
                if (!Near(ordered[index], expected[index], 0.002)) return false;
            return true;
        }

        private static List<double> NativeRightDoubleList(object value)
        {
            Array array = value as Array;
            if (array == null) return new List<double>();
            List<double> output = new List<double>();
            foreach (object item in array)
                output.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
            return output;
        }

        private static bool NativeRightIsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void RunAssembliesEditIsolated(string cadDir, Result result,
            string editPass)
        {
            ISldWorks sw = null;
            var session = new SessionRecord
            {
                purpose = "assembly_edit_pass:" + editPass
            };
            result.sessions.Add(session);
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && session.created, "ASSEMBLY_EDIT_SESSION_UNAVAILABLE", 50,
                    "could not create an owned SolidWorks session for assembly pass " + editPass);
                Require(OwnedSessionIsExclusive(session), "ASSEMBLY_EDIT_SESSION_NOT_EXCLUSIVE", 50,
                    "assembly edit pass did not own the only active SolidWorks process");
                foreach (AssemblyPlan plan in AssemblyPlans(cadDir))
                    RunAssemblyEditPlan(sw, cadDir, result, plan, editPass);
            }
            finally
            {
                if (sw != null) CloseOwnedSession(ref sw, result, session);
            }
            Require(session.process_exited, "ASSEMBLY_EDIT_SESSION_DID_NOT_EXIT", 50,
                "assembly edit SolidWorks process did not exit cleanly for pass " + editPass);
        }

        private static void RunAssemblyEditPlan(ISldWorks sw, string cadDir, Result result,
            AssemblyPlan plan, string editPass)
        {
            MathUtility math = Safe(delegate { return sw.GetMathUtility() as MathUtility; }, null);
            Require(math != null, "MATH_UTILITY_UNAVAILABLE", 50, "SolidWorks MathUtility unavailable");
            try
            {
                string path = ExactFile(cadDir, plan.file_name);
                var record = new AssemblyRecord
                {
                    edit_pass = editPass,
                    file_name = plan.file_name,
                    path = path,
                    expected_stable_open_warnings = plan.expected_stable_open_warnings
                };
                result.assemblies.Add(record);
                ModelDoc2 model = null;
                try
                {
                    int errors = 0, warnings = 0;
                    model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocASSEMBLY, false,
                        ref errors, ref warnings);
                    record.opened = model != null;
                    record.open_errors = errors;
                    record.open_warnings = warnings;
                    record.initial_open_warning_is_expected_needs_regen =
                        warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen ||
                        warnings == plan.expected_stable_open_warnings +
                            (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen;
                    Require(model != null, "ASSEMBLY_OPEN_FAILED", 51, "could not open " + path);
                    bool allowNeedsRegenOnly = string.Equals(editPass, "stabilize",
                        StringComparison.OrdinalIgnoreCase) || plan.is_root;
                    Require(AssemblyInitialOpenGate(plan, errors, warnings,
                        allowNeedsRegenOnly),
                        "ASSEMBLY_OPEN_ERRORS_OR_WARNINGS", 51,
                        plan.file_name + " initial open expected 0/" +
                        plan.expected_stable_open_warnings.ToString(CultureInfo.InvariantCulture) +
                        " or exact NeedsRegen variant, got " +
                        errors.ToString(CultureInfo.InvariantCulture) + "/" +
                        warnings.ToString(CultureInfo.InvariantCulture));
                    AssemblyDoc assembly = model as AssemblyDoc;
                    Require(assembly != null, "ASSEMBLY_DOCUMENT_TYPE_INVALID", 51,
                        path + " did not open as AssemblyDoc");
                    Try(delegate { assembly.ResolveAllLightWeightComponents(false); });
                    List<Component2> components = TopLevelComponents(model);
                    ComponentDigest beforeDigest = CaptureNonTargetDigest(components, plan, cadDir);
                    record.non_target_before_count = beforeDigest.count;
                    record.non_target_before_digest = beforeDigest.sha256;
                    record.non_target_baseline.AddRange(beforeDigest.states);
                    bool changed = false;
                    foreach (TransformTarget target in plan.targets)
                    {
                        TransformRow row = ApplyTransformTarget(model, assembly, math, components,
                            cadDir, target);
                        record.transforms.Add(row);
                        Require(row.unique_match && row.precondition_source_or_target && row.target_readback &&
                            row.rotation_y_z_preserved && row.fixed_state_preserved &&
                            row.suppressed_state_preserved &&
                            (!row.changed || (row.transform_created && row.transform_applied &&
                                (!row.was_fixed || (row.selected_for_unfix &&
                                    row.unfix_action_succeeded && row.unfixed &&
                                    row.selected_for_refix && row.refix_action_succeeded &&
                                    row.is_fixed_after)))),
                            "ASSEMBLY_COMPONENT_TRANSFORM_GATE_FAILED", 52,
                            plan.file_name + " / " + target.component_name + " failed exact file+name+beforeX gate");
                        if (row.changed) changed = true;
                    }
                    record.changed = changed;
                    record.idempotent_already_target = !changed;
                    if (string.Equals(editPass, "stabilize", StringComparison.OrdinalIgnoreCase))
                        Require(!changed, "ASSEMBLY_STABILIZATION_TARGET_DRIFT", 52,
                            plan.file_name + " was not already at every target before stabilization");
                    record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                    record.after = CaptureAssemblySnapshot(model, plan.is_root);
                    Require(record.rebuilt && AssemblyHealthGate(record.after, plan.is_root),
                        "ASSEMBLY_HEALTH_GATE_FAILED", 53,
                        plan.file_name + " contains a non-whitelisted feature issue");
                    components = TopLevelComponents(model);
                    record.after_rebuild_transform_gate = VerifyTransformRows(components,
                        record.transforms, cadDir, false);
                    Require(record.after_rebuild_transform_gate,
                        "ASSEMBLY_AFTER_REBUILD_TRANSFORM_GATE_FAILED", 53,
                        plan.file_name + " did not retain exact target transforms after ForceRebuild");
                    ComponentDigest afterDigest = CaptureNonTargetDigest(components, plan, cadDir);
                    record.non_target_after_rebuild_count = afterDigest.count;
                    record.non_target_after_rebuild_digest = afterDigest.sha256;
                    record.non_target_after_rebuild_unchanged = NonTargetStatesEqual(
                        record.non_target_baseline, afterDigest.states);
                    Require(record.non_target_after_rebuild_unchanged,
                        "ASSEMBLY_NON_TARGET_CHANGED_AFTER_REBUILD", 53,
                        plan.file_name + " changed a non-target top-level component state");
                    if (plan.is_root)
                    {
                        record.left_shelf_root_instance_count = CountTopLevelByPath(components,
                            ExactFile(cadDir, "箱体横层板L焊接.SLDASM"));
                        record.right_shelf_root_instance_count = CountTopLevelByPath(components,
                            ExactFile(cadDir, "箱体横层板R焊接.SLDASM"));
                        record.four_shelf_root_instances = record.left_shelf_root_instance_count == 2 &&
                            record.right_shelf_root_instance_count == 2;
                        Require(record.four_shelf_root_instances, "ROOT_SHELF_INSTANCE_COUNT_DRIFT", 54,
                            "root assembly does not contain exactly two L and two R shelf-weld instances");
                    }
                    record.save_flag_after_rebuild = Safe(delegate { return model.GetSaveFlag(); }, true);
                    bool saveNeeded = string.Equals(editPass, "stabilize",
                        StringComparison.OrdinalIgnoreCase) || changed ||
                        record.save_flag_after_rebuild;
                    if (saveNeeded) SaveModel(model, record, result);
                    else record.save_not_required_idempotent = true;
                    Require(record.save_not_required_idempotent ||
                        (record.saved && record.save_errors == 0 && record.save_warnings == 0 &&
                            !record.save_flag_after_save),
                        "ASSEMBLY_SAVE_FAILED", 55, "save failed for " + plan.file_name);
                    record.edit_gate = true;
                }
                finally
                {
                    CloseDocument(sw, ref model);
                }
                FileRecord file = FileRecordFor(result, path);
                file.sha_after_save = Sha256(path);
                record.sha_after_save = file.sha_after_save;
            }
            finally
            {
                Release(math);
            }
        }

        private static void RunAssembliesVerifyIsolated(string cadDir, Result result)
        {
            List<AssemblyPlan> plans = AssemblyPlans(cadDir);
            Require(plans.Count == 22, "ASSEMBLY_VERIFY_PLAN_COUNT_DRIFT", 56,
                "assembly verification requires exactly twenty-two plans");
            Require(result.assemblies.Count == 44 &&
                result.assemblies.Count == plans.Count * 2,
                "ASSEMBLY_RECORD_COUNT_DRIFT", 56,
                "assembly verification requires exactly twenty-two primary and twenty-two stabilization records");
            for (int index = 0; index < plans.Count; index++)
            {
                AssemblyPlan plan = plans[index];
                AssemblyRecord primaryRecord = result.assemblies[index];
                AssemblyRecord stabilizationRecord = result.assemblies[plans.Count + index];
                Require(string.Equals(primaryRecord.edit_pass, "primary",
                    StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(primaryRecord.file_name, plan.file_name,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(stabilizationRecord.edit_pass, "stabilize",
                    StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(stabilizationRecord.file_name, plan.file_name,
                        StringComparison.OrdinalIgnoreCase),
                    "ASSEMBLY_VERIFY_RECORD_ORDER_DRIFT", 56,
                    "assembly primary and stabilization records do not match the verification plan order");
            }

            ISldWorks sw = null;
            var session = new SessionRecord
            {
                purpose = "assembly_verify_pass:read_only_reopen"
            };
            result.sessions.Add(session);
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && session.created, "ASSEMBLY_VERIFY_SESSION_UNAVAILABLE", 56,
                    "could not create an owned assembly read-only verification session");
                Require(OwnedSessionIsExclusive(session), "ASSEMBLY_VERIFY_SESSION_NOT_EXCLUSIVE", 56,
                    "assembly verification pass did not own the only active SolidWorks process");
                for (int index = 0; index < plans.Count; index++)
                {
                    AssemblyPlan plan = plans[index];
                    AssemblyRecord record = result.assemblies[plans.Count + index];
                    RunAssemblyVerifyPlan(sw, cadDir, result, plan, record);
                }
            }
            finally
            {
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.process_exited, "ASSEMBLY_VERIFY_SESSION_DID_NOT_EXIT", 60,
                "assembly verification SolidWorks process did not exit cleanly");
        }

        private static void RunAssemblyVerifyPlan(ISldWorks sw, string cadDir, Result result,
            AssemblyPlan plan, AssemblyRecord record)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocASSEMBLY, true,
                    ref errors, ref warnings);
                record.reopen_open_errors = errors;
                record.reopen_open_warnings = warnings;
                Require(model != null, "ASSEMBLY_READONLY_REOPEN_FAILED", 56,
                    "could not reopen " + record.path);
                Require(AssemblyReopenGate(plan, errors, warnings),
                    "ASSEMBLY_REOPEN_ERRORS_OR_WARNINGS", 56,
                    plan.file_name + " did not meet its exact read-only reopen error/warning gate");
                if (warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen &&
                    !result.assembly_reopen_needs_regen_files.Contains(plan.file_name))
                    result.assembly_reopen_needs_regen_files.Add(plan.file_name);
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "ASSEMBLY_REOPEN_TYPE_INVALID", 56,
                    record.path + " did not reopen as AssemblyDoc");
                Try(delegate { assembly.ResolveAllLightWeightComponents(false); });
                record.reopen_rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                record.reopen_save_flag_after_rebuild = Safe(delegate
                {
                    return model.GetSaveFlag();
                }, true);
                record.reopen = CaptureAssemblySnapshot(model, plan.is_root);
                Require(record.reopen_rebuilt && AssemblyHealthGate(record.reopen, plan.is_root),
                    "ASSEMBLY_REOPEN_HEALTH_GATE_FAILED", 57,
                    plan.file_name + " contains a new-session non-whitelisted issue");
                List<Component2> components = TopLevelComponents(model);
                record.reopen_transform_gate = VerifyTransformRows(components,
                    record.transforms, cadDir, true);
                Require(record.reopen_transform_gate, "ASSEMBLY_REOPEN_TRANSFORM_FAILED", 58,
                    plan.file_name + " did not reopen with exact target transforms");
                ComponentDigest reopenDigest = CaptureNonTargetDigest(components, plan, cadDir);
                record.non_target_reopen_count = reopenDigest.count;
                record.non_target_reopen_digest = reopenDigest.sha256;
                record.non_target_reopen_unchanged = NonTargetStatesEqual(
                    record.non_target_baseline, reopenDigest.states);
                Require(record.non_target_reopen_unchanged,
                    "ASSEMBLY_NON_TARGET_CHANGED_ON_REOPEN", 58,
                    plan.file_name + " reopened with a changed non-target top-level component state");
                if (plan.is_root)
                {
                    record.reopen_left_shelf_root_instance_count = CountTopLevelByPath(components,
                        ExactFile(cadDir, "箱体横层板L焊接.SLDASM"));
                    record.reopen_right_shelf_root_instance_count = CountTopLevelByPath(components,
                        ExactFile(cadDir, "箱体横层板R焊接.SLDASM"));
                    Require(record.reopen_left_shelf_root_instance_count == 2 &&
                        record.reopen_right_shelf_root_instance_count == 2,
                        "ROOT_SHELF_REOPEN_INSTANCE_COUNT_DRIFT", 59,
                        "root shelf instance count changed on reopen");
                }
                record.verify_gate = true;
            }
            finally
            {
                CloseDocument(sw, ref model);
            }
            string hash = Sha256(record.path);
            FileRecord file = FileRecordFor(result, record.path);
            file.sha_after_readonly_reopen = hash;
            Require(string.Equals(file.sha_after_save, hash, StringComparison.OrdinalIgnoreCase),
                "ASSEMBLY_READONLY_REOPEN_CHANGED_HASH", 60,
                "read-only reopen changed " + record.path);
        }

        private static TransformRow ApplyTransformTarget(ModelDoc2 model, AssemblyDoc assembly,
            MathUtility math, List<Component2> components, string cadDir, TransformTarget target)
        {
            string componentPath = ExactFile(cadDir, target.component_file_name);
            List<Component2> matches = ExactComponents(components, componentPath, target.component_name);
            var row = new TransformRow
            {
                component_name = target.component_name,
                component_file_name = target.component_file_name,
                component_path = componentPath,
                source_x_mm = target.source_x_mm,
                target_x_mm = target.target_x_mm,
                explicit_z_target = target.explicit_z_target,
                source_z_mm = target.source_z_mm,
                target_z_mm = target.target_z_mm,
                match_count = matches.Count,
                unique_match = matches.Count == 1
            };
            if (matches.Count != 1) return row;
            Component2 component = matches[0];
            MathTransform beforeTransform = Safe(delegate { return component.Transform2 as MathTransform; }, null);
            double[] before = TransformData(beforeTransform);
            row.before_transform = before;
            if (before == null) return row;
            row.before_x_mm = before[9] * 1000.0;
            row.before_y_mm = before[10] * 1000.0;
            row.before_z_mm = before[11] * 1000.0;
            bool sourcePosition = Near(row.before_x_mm, target.source_x_mm,
                    TransformToleranceMm) && (!target.explicit_z_target ||
                Near(row.before_z_mm, target.source_z_mm, TransformToleranceMm));
            bool targetPosition = Near(row.before_x_mm, target.target_x_mm,
                    TransformToleranceMm) && (!target.explicit_z_target ||
                Near(row.before_z_mm, target.target_z_mm, TransformToleranceMm));
            row.precondition_source_or_target = sourcePosition || targetPosition;
            if (!row.precondition_source_or_target) return row;
            row.was_fixed = Safe(delegate { return component.IsFixed(); }, false);
            row.was_suppressed = Safe(delegate { return component.IsSuppressed(); }, false);
            if (sourcePosition)
            {
                double[] data = (double[])before.Clone();
                data[9] = target.target_x_mm / 1000.0;
                if (target.explicit_z_target) data[11] = target.target_z_mm / 1000.0;
                MathTransform targetTransform = Safe(delegate { return math.CreateTransform(data) as MathTransform; }, null);
                row.transform_created = targetTransform != null;
                if (row.was_fixed)
                {
                    Try(delegate { model.ClearSelection2(true); });
                    row.selected_for_unfix = Safe(delegate { return component.Select4(false, null, false); }, false);
                    if (row.selected_for_unfix)
                        row.unfix_action_succeeded = TryAction(delegate { assembly.UnfixComponent(); });
                    row.unfixed = row.unfix_action_succeeded &&
                        !Safe(delegate { return component.IsFixed(); }, true);
                }
                else row.unfixed = true;
                row.transform_applied = targetTransform != null &&
                    Safe(delegate { return component.SetTransformAndSolve2(targetTransform); }, false);
                if (row.was_fixed)
                {
                    Try(delegate { model.ClearSelection2(true); });
                    row.selected_for_refix = Safe(delegate { return component.Select4(false, null, false); }, false);
                    if (row.selected_for_refix)
                        row.refix_action_succeeded = TryAction(delegate { assembly.FixComponent(); });
                }
                row.changed = true;
                Release(targetTransform);
            }
            else
            {
                row.idempotent_already_target = true;
            }
            MathTransform afterTransform = Safe(delegate { return component.Transform2 as MathTransform; }, null);
            double[] after = TransformData(afterTransform);
            row.after_transform = after;
            row.after_x_mm = after == null ? double.NaN : after[9] * 1000.0;
            row.after_y_mm = after == null ? double.NaN : after[10] * 1000.0;
            row.after_z_mm = after == null ? double.NaN : after[11] * 1000.0;
            row.target_z_readback = !target.explicit_z_target || after != null &&
                Near(row.after_z_mm, target.target_z_mm, TransformToleranceMm);
            row.target_readback = after != null && Near(row.after_x_mm,
                target.target_x_mm, TransformToleranceMm) && row.target_z_readback;
            row.rotation_y_z_preserved = target.explicit_z_target ?
                SameExceptXZ(before, after, 1e-10) : SameExceptX(before, after, 1e-10);
            row.is_fixed_after = Safe(delegate { return component.IsFixed(); }, false);
            row.is_suppressed_after = Safe(delegate { return component.IsSuppressed(); }, false);
            row.fixed_state_preserved = row.was_fixed == row.is_fixed_after;
            row.suppressed_state_preserved = row.was_suppressed == row.is_suppressed_after;
            Release(beforeTransform);
            Release(afterTransform);
            return row;
        }

        private static List<DimensionPlan> DimensionPlans(string cadDir)
        {
            return new List<DimensionPlan>
            {
                new DimensionPlan("标准寄存柜 模型.SLDPRT", false,
                    D("D1@草图1", 760, 888), D("D2@草图130", 610, 738),
                    D("D2@草图142", 300, 364), D("D1@草图143", 300, 364))
                    .WithReadbacks(R("D6@草图115", 381, 1), R("D8@草图76", 381, 1)),
                new DimensionPlan("箱体横层板加强筋.SLDPRT", true, D("D2@草图1", 309, 373))
            };
        }

        private static DimensionTarget D(string name, double source, double target)
        {
            return new DimensionTarget
            {
                name = name,
                source_mm = source,
                target_mm = target,
                expected_driven_state = 2
            };
        }

        private static DimensionReadbackTarget R(string name, double target, int drivenState)
        {
            return new DimensionReadbackTarget
            {
                name = name,
                target_mm = target,
                expected_driven_state = drivenState
            };
        }

        private static void ValidateDimensionReadbacks(ModelDoc2 model, DimensionPlan plan,
            DimensionPartRecord record, bool reopen)
        {
            for (int index = 0; index < plan.readbacks.Count; index++)
            {
                DimensionReadbackTarget target = plan.readbacks[index];
                DimensionReadbackRow row;
                if (reopen)
                {
                    Require(index < record.readbacks.Count, "DIMENSION_READBACK_RECORD_MISSING", 28,
                        plan.file_name + " missing persisted readback record");
                    row = record.readbacks[index];
                }
                else
                {
                    row = new DimensionReadbackRow
                    {
                        name = target.name,
                        target_mm = target.target_mm,
                        expected_driven_state = target.expected_driven_state
                    };
                    record.readbacks.Add(row);
                }

                Dimension dimension = FindDimension(model, target.name);
                double actual = dimension == null ? double.NaN :
                    Safe(delegate { return dimension.SystemValue * 1000.0; }, double.NaN);
                int drivenState = dimension == null ? -1 :
                    Safe(delegate { return dimension.DrivenState; }, -1);
                bool pass = dimension != null && Near(actual, target.target_mm, DimensionToleranceMm) &&
                    drivenState == target.expected_driven_state;
                if (reopen)
                {
                    row.reopen_found = dimension != null;
                    row.reopen_mm = actual;
                    row.reopen_driven_state = drivenState;
                    row.reopen_pass = pass;
                }
                else
                {
                    row.found = dimension != null;
                    row.after_rebuild_mm = actual;
                    row.driven_state = drivenState;
                    row.pass = pass;
                }
                Release(dimension);
                Require(pass, reopen ? "DIMENSION_POST_REBUILD_REOPEN_GATE_FAILED" :
                    "DIMENSION_POST_REBUILD_GATE_FAILED", reopen ? 28 : 24,
                    plan.file_name + " " + target.name + " expected " +
                    target.target_mm.ToString("R", CultureInfo.InvariantCulture) + " mm/state " +
                    target.expected_driven_state.ToString(CultureInfo.InvariantCulture) +
                    " but read " + actual.ToString("R", CultureInfo.InvariantCulture) + "/" +
                    drivenState.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static List<DerivedPlan> DerivedPlans(string cadDir)
        {
            const string master = "标准寄存柜 模型.SLDPRT";
            return new List<DerivedPlan>
            {
                P("底座 模型.sldprt", master, "SplitBody", -380, 380, -444, 444),
                P("底座底板.sldprt", "底座 模型.sldprt", "SplitBody", -359, 359, -423, 423),
                P("底座加强筋.sldprt", "底座 模型.sldprt", "SplitBody", -369.7, 369.7, -433.7, 433.7),
                P("底座外框.sldprt", "底座 模型.sldprt", "SplitBody", -380, 380, -444, 444),
                P("上盖 模型.sldprt", master, "SplitBody", -380, 380, -444, 444),
                P("上盖壳体底板.sldprt", "上盖 模型.sldprt", "SplitBody", -379, 379, -443, 443),
                P("上盖壳体后侧板.sldprt", "上盖 模型.sldprt", "SplitBody", -380, 380, -444, 444),
                P("上盖壳体前侧板.sldprt", "上盖 模型.sldprt", "SplitBody", -380, 380, -444, 444),
                P("上盖壳体左侧板.sldprt", "上盖 模型.sldprt", "SplitBody", -380, -320.8, -444, -384.8),
                PNativeRightTopCover("上盖壳体右侧板.SLDPRT", "上盖壳体左侧板.sldprt",
                    320.8, 380, 384.8, 444),
                P("箱体横层板L.sldprt", master, "SplitBody", -377.7, -64.5, -441.7, -64.5),
                PNativeCabinetShelfRight("箱体横层板R.SLDPRT", "箱体横层板L.sldprt",
                    64.5, 377.7, 64.5, 441.7),
                P("门框 横隔板.sldprt", master, "SplitBody", -361.5, -36.5, -425.5, -36.5),
                PNativeFrameCrossbarRight("门框 横隔板R.SLDPRT", "门框 横隔板.sldprt",
                    36.5, 361.5, 36.5, 425.5),
                P("门框 上.sldprt", master, "SplitBody", -378.6, 378.6, -442.6, 442.6),
                P("门框 左.sldprt", master, "SplitBody", -380, -360, -444, -424),
                PNativeDoorFrameRight("门框 右.sldprt", "门框 左.sldprt",
                    360, 380, 424, 444),
                P("门框 下.sldprt", master, "SplitBody", -378.6, 378.6, -442.6, 442.6, true),
                P("箱体侧板加强筋1.sldprt", master, "SplitBody", -379, -374.2, -443, -438.2),
                P("箱体右侧板.sldprt", master, "SplitBody", 0.5, 380, 0.5, 444),
                P("箱体左侧板.sldprt", master, "SplitBody", -380, 14.7, -444, 14.7),
                P("箱体竖隔板L.sldprt", master, "SplitBody",
                    -63.002525, -21.2, -63.002525, -21.2)
            };
        }

        private static HashSet<string> DerivedNeedsRegenWarningAllowlist()
        {
            return new HashSet<string>(new[]
            {
                "底座 模型.sldprt", "底座底板.sldprt", "底座加强筋.sldprt", "底座外框.sldprt",
                "上盖 模型.sldprt", "上盖壳体底板.sldprt", "上盖壳体后侧板.sldprt",
                "上盖壳体前侧板.sldprt", "上盖壳体左侧板.sldprt", "上盖壳体右侧板.SLDPRT",
                "箱体横层板L.sldprt", "箱体横层板R.SLDPRT", "门框 横隔板.sldprt",
                "门框 横隔板R.SLDPRT", "门框 上.sldprt", "门框 左.sldprt",
                "门框 右.sldprt", "箱体侧板加强筋1.sldprt", "箱体右侧板.sldprt",
                "箱体左侧板.sldprt", "箱体竖隔板L.sldprt"
            }, StringComparer.OrdinalIgnoreCase);
        }

        private static DerivedPlan P(string file, string source, string type,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax,
            bool relock)
        {
            return new DerivedPlan { file_name = file, source_file_name = source,
                link_type = type, relock_required = relock, lock_after_refresh = true,
                source_xmin_mm = sourceXmin, source_xmax_mm = sourceXmax,
                target_xmin_mm = targetXmin, target_xmax_mm = targetXmax };
        }

        private static DerivedPlan P(string file, string source, string type,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax)
        {
            return P(file, source, type, sourceXmin, sourceXmax, targetXmin, targetXmax, false);
        }

        private static DerivedPlan PNativeRightTopCover(string file, string source,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax)
        {
            return new DerivedPlan
            {
                file_name = file,
                source_file_name = source,
                link_type = NativeRightTopCoverRoute,
                native_right_topcover = true,
                source_xmin_mm = sourceXmin,
                source_xmax_mm = sourceXmax,
                target_xmin_mm = targetXmin,
                target_xmax_mm = targetXmax
            };
        }

        private static DerivedPlan PNativeFrameCrossbarRight(string file, string source,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax)
        {
            return new DerivedPlan
            {
                file_name = file,
                source_file_name = source,
                link_type = NativeFrameCrossbarRightRoute,
                native_frame_crossbar_right = true,
                source_xmin_mm = sourceXmin,
                source_xmax_mm = sourceXmax,
                target_xmin_mm = targetXmin,
                target_xmax_mm = targetXmax
            };
        }

        private static DerivedPlan PNativeDoorFrameRight(string file, string source,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax)
        {
            return new DerivedPlan
            {
                file_name = file,
                source_file_name = source,
                link_type = NativeDoorFrameRightRoute,
                native_door_frame_right = true,
                source_xmin_mm = sourceXmin,
                source_xmax_mm = sourceXmax,
                target_xmin_mm = targetXmin,
                target_xmax_mm = targetXmax
            };
        }

        private static DerivedPlan PNativeCabinetShelfRight(string file, string source,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax)
        {
            return new DerivedPlan
            {
                file_name = file,
                source_file_name = source,
                link_type = NativeCabinetShelfRightRoute,
                native_cabinet_shelf_right = true,
                source_xmin_mm = sourceXmin,
                source_xmax_mm = sourceXmax,
                target_xmin_mm = targetXmin,
                target_xmax_mm = targetXmax
            };
        }

        private static List<AssemblyPlan> AssemblyPlans(string cadDir)
        {
            var plans = new List<AssemblyPlan>();
            foreach (string ratio in new[] { "2", "4", "6" })
            {
                plans.Add(new AssemblyPlan("储物柜门" + ratio + "╱12焊接_左.SLDASM", false,
                    T("插销固定板-1", "插销固定板.SLDPRT", -148.5, -180.5),
                    T("U型锁钩垫板-1", "U型锁钩垫板.SLDPRT", 143.5, 175.5)));
                plans.Add(new AssemblyPlan("储物柜门" + ratio + "╱12焊接_右.SLDASM", false,
                    T("插销固定板-1", "插销固定板.SLDPRT", 148.5, 180.5),
                    T("U型锁钩垫板-1", "U型锁钩垫板.SLDPRT", -143.5, -175.5)));
            }
            foreach (string ratio in new[] { "2", "4", "6" })
            {
                plans.Add(OuterDoorPlan("L" + ratio, true));
                plans.Add(OuterDoorPlan("R" + ratio, false));
            }
            plans.Add(new AssemblyPlan("底座焊接.SLDASM", false,
                T("螺母M12-1", "螺母M12.SLDPRT", -345, -409),
                T("螺母M12-2", "螺母M12.SLDPRT", 345, 409),
                T("螺母M12-3", "螺母M12.SLDPRT", 345, 409),
                T("螺母M12-4", "螺母M12.SLDPRT", -345, -409)));
            plans.Add(new AssemblyPlan("箱体横层板L焊接.SLDASM", false,
                T("箱体横层板加强筋-1", "箱体横层板加强筋.SLDPRT", -221.1, -285.1),
                T("箱体横层板加强筋-2", "箱体横层板加强筋.SLDPRT", -221.1, -285.1)));
            plans.Add(new AssemblyPlan("箱体横层板R焊接.SLDASM", false, 1,
                T("箱体横层板加强筋-1", "箱体横层板加强筋.SLDPRT", 221.1, 285.1),
                T("箱体横层板加强筋-2", "箱体横层板加强筋.SLDPRT", 221.1, 285.1)));
            plans.Add(new AssemblyPlan("箱体竖隔板R焊接.SLDASM", false,
                T("箱体侧板加强筋1-1", "箱体侧板加强筋1.sldprt", 442.2, 506.2),
                T("箱体侧板加强筋1-2", "箱体侧板加强筋1.sldprt", 442.2, 506.2)));
            plans.Add(new AssemblyPlan("箱体竖隔板L焊接.SLDASM", false,
                T("箱体侧板加强筋1-1", "箱体侧板加强筋1.sldprt", -442.2, -506.2),
                T("箱体侧板加强筋1-2", "箱体侧板加强筋1.sldprt", -442.2, -506.2)));
            plans.Add(new AssemblyPlan("箱体左侧板焊接.sldasm", false,
                T("箱体侧板加强筋1-3", "箱体侧板加强筋1.sldprt", -369.6, -401.6)));
            plans.Add(new AssemblyPlan("箱体右侧板焊接.SLDASM", false,
                TZ("箱体侧板加强筋1-3", "箱体侧板加强筋1.sldprt",
                    72.6, -170.0, 104.6, -106.0)));
            plans.Add(new AssemblyPlan("上盖焊接.SLDASM", false, 1));
            plans.Add(new AssemblyPlan("门框焊接.sldasm", false, 1));
            plans.Add(new AssemblyPlan(SeedRootFileName, true,
                T("储物柜门装配_L2-2", "储物柜门装配_L2.SLDASM", -198.5, -230.5),
                T("储物柜门装配_L4-2", "储物柜门装配_L4.SLDASM", -198.5, -230.5),
                T("储物柜门装配_L6-2", "储物柜门装配_L6.SLDASM", -198.5, -230.5),
                T("储物柜门装配_R2-2", "储物柜门装配_R2.SLDASM", 198.5, 230.5),
                T("储物柜门装配_R4-2", "储物柜门装配_R4.SLDASM", 198.5, 230.5),
                T("储物柜门装配_R6-2", "储物柜门装配_R6.SLDASM", 198.5, 230.5),
                T("调整脚 M12X60(模型)-5", "调整脚 M12X60(模型).SLDPRT", -345, -409),
                T("调整脚 M12X60(模型)-6", "调整脚 M12X60(模型).SLDPRT", 345, 409),
                T("调整脚 M12X60(模型)-7", "调整脚 M12X60(模型).SLDPRT", 345, 409),
                T("调整脚 M12X60(模型)-8", "调整脚 M12X60(模型).SLDPRT", -345, -409)));
            return plans;
        }

        private static AssemblyPlan OuterDoorPlan(string code, bool left)
        {
            double outerSource = left ? -148.5 : 148.5;
            double outerTarget = left ? -180.5 : 180.5;
            double innerSource = left ? 143.5 : -143.5;
            double innerTarget = left ? 175.5 : -175.5;
            return new AssemblyPlan("储物柜门装配_" + code + ".SLDASM", false,
                T("开口挡圈5-1", "开口挡圈5.SLDPRT", outerSource, outerTarget),
                T("开口挡圈5-2", "开口挡圈5.SLDPRT", outerSource, outerTarget),
                T("门轴销-1", "门轴销.SLDPRT", outerSource, outerTarget),
                T("塑料轴套(云绅模具)-1", "塑料轴套(云绅模具).SLDPRT", outerSource, outerTarget),
                T("塑料轴套(云绅模具)-2", "塑料轴套(云绅模具).SLDPRT", outerSource, outerTarget),
                T("锁舌-1", "锁舌.SLDPRT", innerSource, innerTarget));
        }

        private static TransformTarget T(string name, string file, double source, double target)
        {
            return new TransformTarget { component_name = name, component_file_name = file,
                source_x_mm = source, target_x_mm = target };
        }

        private static TransformTarget TZ(string name, string file, double sourceX,
            double sourceZ, double targetX, double targetZ)
        {
            return new TransformTarget
            {
                component_name = name,
                component_file_name = file,
                source_x_mm = sourceX,
                target_x_mm = targetX,
                explicit_z_target = true,
                source_z_mm = sourceZ,
                target_z_mm = targetZ
            };
        }

        private static Feature FindFeature(ModelDoc2 model, string name)
        {
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { return null; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                if (string.Equals(Safe(delegate { return feature.Name; }, ""), name,
                    StringComparison.OrdinalIgnoreCase)) return feature;
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { Release(feature); return null; }
                Release(feature);
                feature = next;
            }
            return null;
        }

        private static Dimension FindDimension(ModelDoc2 model, string name)
        {
            Dimension dimension = Safe(delegate { return model.Parameter(name) as Dimension; }, null);
            if (dimension != null) return dimension;
            string path = Safe(delegate { return model.GetPathName(); }, "");
            string qualified = name + "@" + Path.GetFileNameWithoutExtension(path) + ".Part";
            return Safe(delegate { return model.Parameter(qualified) as Dimension; }, null);
        }

        private static PartSnapshot CapturePartSnapshot(ModelDoc2 model)
        {
            var snapshot = new PartSnapshot { traversal_complete = true };
            PartDoc part = model as PartDoc;
            Array bodies = part == null ? null : Safe(delegate
            {
                return part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            }, null);
            snapshot.body_count = bodies == null ? 0 : bodies.Length;
            snapshot.part_box_m = DoubleArray(part == null ? null :
                Safe(delegate { return part.GetPartBox(true) as Array; }, null));
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { snapshot.traversal_complete = false; return snapshot; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CapturePartFeatureRecursive(feature, snapshot, 0);
                if (!snapshot.traversal_complete) { Release(feature); return snapshot; }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { snapshot.traversal_complete = false; }
                Release(feature);
                if (!snapshot.traversal_complete) return snapshot;
                feature = next;
            }
            if (feature != null) snapshot.traversal_complete = false;
            return snapshot;
        }

        private static void CapturePartFeatureRecursive(Feature feature, PartSnapshot snapshot, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || snapshot.feature_count > 20000)
            {
                snapshot.traversal_complete = false;
                return;
            }
            snapshot.feature_count++;
            string type;
            bool warning;
            int error2;
            int error1;
            bool suppressed;
            Feature child;
            try
            {
                type = feature.GetTypeName2();
                warning = false;
                error2 = feature.GetErrorCode2(out warning);
                error1 = feature.GetErrorCode();
                suppressed = feature.IsSuppressed();
                child = feature.GetFirstSubFeature() as Feature;
            }
            catch { snapshot.traversal_complete = false; return; }
            if (string.Equals(type, "SheetMetal", StringComparison.OrdinalIgnoreCase)) snapshot.has_sheet_metal = true;
            if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase)) snapshot.has_flat_pattern = true;
            if (!suppressed && (error1 > 0 || error2 > 0)) snapshot.error_feature_count++;
            if (!suppressed && warning) snapshot.warning_feature_count++;
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CapturePartFeatureRecursive(child, snapshot, depth + 1);
                if (!snapshot.traversal_complete) { Release(child); return; }
                Feature next = null;
                try { next = child.GetNextSubFeature() as Feature; }
                catch { snapshot.traversal_complete = false; }
                Release(child);
                if (!snapshot.traversal_complete) return;
                child = next;
            }
            if (child != null) snapshot.traversal_complete = false;
        }

        private static bool PartHealthGate(PartSnapshot snapshot, bool requireSheetMetal)
        {
            if (snapshot == null || !snapshot.traversal_complete || snapshot.error_feature_count != 0 ||
                snapshot.warning_feature_count != 0 || snapshot.body_count <= 0) return false;
            if (!requireSheetMetal) return true;
            return snapshot.body_count == 1 && snapshot.has_sheet_metal && snapshot.has_flat_pattern;
        }

        private static bool MasterTopologyGate(PartSnapshot snapshot)
        {
            return snapshot != null && snapshot.traversal_complete && snapshot.body_count == 20 && snapshot.feature_count == 220 &&
                snapshot.error_feature_count == 0 && snapshot.warning_feature_count == 0;
        }

        private static bool TopologySame(PartSnapshot left, PartSnapshot right)
        {
            return left != null && right != null && left.body_count == right.body_count &&
                left.traversal_complete && right.traversal_complete &&
                left.feature_count == right.feature_count &&
                left.error_feature_count == right.error_feature_count &&
                left.warning_feature_count == right.warning_feature_count &&
                left.has_sheet_metal == right.has_sheet_metal &&
                left.has_flat_pattern == right.has_flat_pattern;
        }

        private static bool DerivedPartHealthGate(PartSnapshot snapshot, PartSnapshot baseline)
        {
            if (snapshot == null || baseline == null || !snapshot.traversal_complete ||
                !baseline.traversal_complete ||
                snapshot.error_feature_count != 0 || snapshot.warning_feature_count != 0 ||
                baseline.error_feature_count != 0 || baseline.warning_feature_count != 0 ||
                snapshot.body_count <= 0) return false;
            return snapshot.body_count == baseline.body_count &&
                snapshot.feature_count == baseline.feature_count &&
                snapshot.has_sheet_metal == baseline.has_sheet_metal &&
                snapshot.has_flat_pattern == baseline.has_flat_pattern;
        }

        private static LinkSnapshot CaptureLinkSnapshot(ModelDoc2 model, DerivedPlan plan,
            string cadDir)
        {
            return CaptureLinkSnapshot(model, plan, cadDir, plan.relock_required);
        }

        private static LinkSnapshot CaptureLinkSnapshot(ModelDoc2 model, DerivedPlan plan,
            string cadDir, bool expectLocked)
        {
            var snapshot = new LinkSnapshot
            {
                traversal_complete = true,
                expected_source_path = ExactFile(cadDir, plan.source_file_name),
                expected_primary_feature_name = ExpectedPrimaryFeatureName(plan),
                expected_reference_count = string.Equals(plan.link_type, "MirrorStock",
                    StringComparison.OrdinalIgnoreCase) ? 6 : 2,
                expected_reference_status = expectLocked ?
                    (int)swExternalReferenceStatus_e.swExternalReferenceLocked : 3
            };
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { snapshot.traversal_complete = false; return snapshot; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                string type;
                try { type = feature.GetTypeName2(); }
                catch { snapshot.traversal_complete = false; Release(feature); return snapshot; }
                if (string.Equals(type, plan.link_type, StringComparison.OrdinalIgnoreCase))
                {
                    PrimaryLinkFeature row = CapturePrimaryFeature(feature, type);
                    snapshot.matched_feature_count++;
                    snapshot.primary_features.Add(row);
                }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { snapshot.traversal_complete = false; }
                Release(feature);
                if (!snapshot.traversal_complete) return snapshot;
                feature = next;
            }
            if (feature != null) snapshot.traversal_complete = false;
            foreach (PrimaryLinkFeature primary in snapshot.primary_features)
                foreach (ReferenceRow reference in primary.references)
                {
                    if (SamePath(reference.model_path, snapshot.expected_source_path)) snapshot.has_exact_expected_source = true;
                    if (!string.IsNullOrWhiteSpace(reference.model_path) && !IsUnder(reference.model_path, cadDir))
                        snapshot.nonlocal_paths.Add(reference.model_path);
                    if (!string.IsNullOrWhiteSpace(reference.component_path) && !IsUnder(reference.component_path, cadDir))
                        snapshot.nonlocal_paths.Add(reference.component_path);
                    if (reference.status == (int)swExternalReferenceStatus_e.swExternalReferenceBroken ||
                        reference.status == (int)swExternalReferenceStatus_e.swExternalReferenceDangling)
                        snapshot.broken_or_dangling_count++;
                    if (reference.status == (int)swExternalReferenceStatus_e.swExternalReferenceLocked)
                        snapshot.locked_count++;
                    if (reference.status != (int)swExternalReferenceStatus_e.swExternalReferenceLocked)
                        snapshot.nonlocked_count++;
                }
            snapshot.primary_feature_count = snapshot.primary_features.Count;
            snapshot.capture_success_count = snapshot.primary_features.Count(delegate(PrimaryLinkFeature row)
            {
                return row.capture_succeeded;
            });
            snapshot.total_reference_count = snapshot.primary_features.Sum(delegate(PrimaryLinkFeature row)
            {
                return row.references.Count;
            });
            snapshot.all_paths_local = snapshot.nonlocal_paths.Count == 0;
            snapshot.all_primary_locked = snapshot.primary_feature_count > 0 && snapshot.nonlocked_count == 0;
            snapshot.exact_contract_match = snapshot.matched_feature_count == 1 &&
                snapshot.primary_feature_count == 1 && snapshot.capture_success_count == 1 &&
                string.Equals(snapshot.primary_features[0].name, snapshot.expected_primary_feature_name,
                    StringComparison.Ordinal) && snapshot.total_reference_count == snapshot.expected_reference_count &&
                snapshot.primary_features[0].references.All(delegate(ReferenceRow reference)
                {
                    return SamePath(reference.model_path, snapshot.expected_source_path) &&
                        SamePath(reference.component_path, snapshot.expected_source_path) &&
                        string.Equals(reference.feature, snapshot.expected_primary_feature_name,
                            StringComparison.Ordinal) && reference.status == snapshot.expected_reference_status;
                });
            return snapshot;
        }

        private static string ExpectedPrimaryFeatureName(DerivedPlan plan)
        {
            if (string.Equals(plan.link_type, "MirrorStock", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(plan.source_file_name) + "镜向";
            return "基体零件-" + Path.GetFileNameWithoutExtension(plan.source_file_name) + "-1";
        }

        private static PrimaryLinkFeature CapturePrimaryFeature(Feature feature, string type)
        {
            var row = new PrimaryLinkFeature
            {
                name = Safe(delegate { return feature.Name; }, ""),
                type = type
            };
            object modelPaths = null, componentPaths = null, features = null, dataTypes = null;
            object statuses = null, entities = null, featureComponents = null;
            int configOption = 0;
            string configName = "";
            try
            {
                feature.ListExternalFileReferences2(out modelPaths, out componentPaths, out features,
                    out dataTypes, out statuses, out entities, out featureComponents,
                    out configOption, out configName);
                row.capture_succeeded = true;
                row.config_option = configOption;
                row.config_name = configName ?? "";
            }
            catch (Exception ex)
            {
                row.error = ex.GetType().FullName + " " + ex.Message;
                return row;
            }
            Array modelArray = modelPaths as Array;
            Array componentArray = componentPaths as Array;
            Array featureArray = features as Array;
            Array statusArray = statuses as Array;
            int count = MaxLength(modelArray, componentArray, featureArray, statusArray);
            for (int index = 0; index < count; index++)
                row.references.Add(new ReferenceRow
                {
                    model_path = ArrayString(modelArray, index),
                    component_path = ArrayString(componentArray, index),
                    feature = ArrayString(featureArray, index),
                    status = ArrayInt(statusArray, index)
                });
            return row;
        }

        private static bool LinkGate(LinkSnapshot snapshot, DerivedPlan plan, bool afterRefresh)
        {
            if (snapshot == null || !snapshot.traversal_complete || snapshot.primary_feature_count < 1 ||
                !snapshot.has_exact_expected_source || !snapshot.all_paths_local ||
                snapshot.broken_or_dangling_count != 0 || !snapshot.exact_contract_match) return false;
            if (afterRefresh && plan.lock_after_refresh && !snapshot.all_primary_locked) return false;
            return true;
        }

        private static string LinkSnapshotDigest(LinkSnapshot snapshot)
        {
            if (snapshot == null) return "";
            var builder = new StringBuilder();
            builder.Append(Path.GetFullPath(snapshot.expected_source_path ?? "").ToLowerInvariant())
                .Append('|').Append(snapshot.traversal_complete ? '1' : '0')
                .Append('|').Append(snapshot.primary_feature_count.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(snapshot.has_exact_expected_source ? '1' : '0')
                .Append('|').Append(snapshot.all_paths_local ? '1' : '0').Append('\n');
            foreach (PrimaryLinkFeature feature in snapshot.primary_features
                .OrderBy(delegate(PrimaryLinkFeature row) { return row.name; },
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(delegate(PrimaryLinkFeature row) { return row.type; },
                    StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(feature.name.ToLowerInvariant()).Append('|')
                    .Append(feature.type.ToLowerInvariant()).Append('|')
                    .Append(feature.capture_succeeded ? '1' : '0').Append('|')
                    .Append(feature.config_option.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append((feature.config_name ?? "").ToLowerInvariant()).Append('\n');
                foreach (ReferenceRow reference in feature.references
                    .OrderBy(delegate(ReferenceRow row) { return row.model_path; },
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(delegate(ReferenceRow row) { return row.component_path; },
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(delegate(ReferenceRow row) { return row.feature; },
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(delegate(ReferenceRow row) { return row.status; }))
                    builder.Append(Path.GetFullPath(reference.model_path ?? "").ToLowerInvariant())
                        .Append('|')
                        .Append(Path.GetFullPath(reference.component_path ?? "").ToLowerInvariant())
                        .Append('|').Append((reference.feature ?? "").ToLowerInvariant())
                        .Append('|').Append(reference.status.ToString(CultureInfo.InvariantCulture))
                        .Append('\n');
            }
            return Sha256Text(builder.ToString());
        }

        private static bool LinkSnapshotsEqual(LinkSnapshot left, LinkSnapshot right)
        {
            string leftDigest = LinkSnapshotDigest(left);
            return !string.IsNullOrWhiteSpace(leftDigest) &&
                string.Equals(leftDigest, LinkSnapshotDigest(right),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static LinkUpdateBatch UpdatePrimaryLinksDetailed(ModelDoc2 model,
            DerivedPlan plan, int action, string stage)
        {
            var batch = new LinkUpdateBatch { stage = stage, action = action, traversal_complete = true };
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { batch.traversal_complete = false; return batch; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                string type;
                try { type = feature.GetTypeName2(); }
                catch { batch.traversal_complete = false; Release(feature); return batch; }
                if (string.Equals(type, plan.link_type, StringComparison.OrdinalIgnoreCase))
                {
                    PrimaryLinkFeature refs = CapturePrimaryFeature(feature, type);
                    if (refs.references.Count > 0)
                    {
                        var row = new LinkUpdateRow
                        {
                            feature_name = refs.name,
                            feature_type = refs.type,
                            config_option = refs.config_option,
                            config_name = refs.config_name,
                            attempted = true,
                            statuses_before = refs.references.Select(
                                delegate(ReferenceRow reference) { return reference.status; }).ToArray()
                        };
                        if (!names.Add(refs.name)) batch.duplicate_feature_name_count++;
                        if (!refs.capture_succeeded || refs.references.Count !=
                            (string.Equals(plan.link_type, "MirrorStock", StringComparison.OrdinalIgnoreCase) ? 6 : 2) ||
                            !string.Equals(refs.name, ExpectedPrimaryFeatureName(plan), StringComparison.Ordinal))
                        {
                            row.status = "PRE_UPDATE_REFERENCE_CONTRACT_MISMATCH";
                            batch.rows.Add(row);
                            Feature rejectedNext = null;
                            try { rejectedNext = feature.GetNextFeature() as Feature; }
                            catch { batch.traversal_complete = false; }
                            Release(feature);
                            if (!batch.traversal_complete) return batch;
                            feature = rejectedNext;
                            continue;
                        }
                        row.call_succeeded = TryAction(delegate
                        {
                            feature.UpdateExternalFileReferences(refs.config_option,
                                refs.config_name ?? "", action);
                        });
                        PrimaryLinkFeature after = CapturePrimaryFeature(feature, type);
                        row.statuses_after = after.references.Select(
                            delegate(ReferenceRow reference) { return reference.status; }).ToArray();
                        row.status = row.call_succeeded && after.capture_succeeded &&
                            after.references.Count == refs.references.Count ? "CALL_SUCCEEDED" : "CALL_FAILED";
                        row.call_succeeded = row.status == "CALL_SUCCEEDED";
                        batch.rows.Add(row);
                    }
                }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { batch.traversal_complete = false; }
                Release(feature);
                if (!batch.traversal_complete) return batch;
                feature = next;
            }
            if (feature != null) batch.traversal_complete = false;
            batch.success = batch.traversal_complete && batch.rows.Count == 1 &&
                batch.duplicate_feature_name_count == 0 &&
                batch.rows.TrueForAll(delegate(LinkUpdateRow row) { return row.call_succeeded; });
            return batch;
        }

        private static bool DerivedBboxBeforeGate(PartSnapshot snapshot, DerivedPlan plan)
        {
            return BboxX(snapshot, plan.source_xmin_mm, plan.source_xmax_mm) ||
                BboxX(snapshot, plan.target_xmin_mm, plan.target_xmax_mm);
        }

        private static bool DerivedBboxTargetGate(PartSnapshot snapshot, DerivedPlan plan,
            PartSnapshot baseline)
        {
            return BboxX(snapshot, plan.target_xmin_mm, plan.target_xmax_mm) &&
                BboxYzSame(snapshot, baseline);
        }

        private static bool BboxX(PartSnapshot snapshot, double xminMm, double xmaxMm)
        {
            return snapshot != null && snapshot.part_box_m != null && snapshot.part_box_m.Length >= 6 &&
                Near(snapshot.part_box_m[0] * 1000.0, xminMm, BoundingBoxToleranceMm) &&
                Near(snapshot.part_box_m[3] * 1000.0, xmaxMm, BoundingBoxToleranceMm);
        }

        private static bool BboxYzSame(PartSnapshot current, PartSnapshot baseline)
        {
            if (current == null || baseline == null || current.part_box_m == null ||
                baseline.part_box_m == null || current.part_box_m.Length < 6 ||
                baseline.part_box_m.Length < 6) return false;
            foreach (int index in new[] { 1, 2, 4, 5 })
                if (!Near(current.part_box_m[index] * 1000.0,
                    baseline.part_box_m[index] * 1000.0, BoundingBoxToleranceMm)) return false;
            return true;
        }

        private static AssemblySnapshot CaptureAssemblySnapshot(ModelDoc2 model, bool root)
        {
            var snapshot = new AssemblySnapshot { is_root = root, traversal_complete = true };
            snapshot.top_level_component_count = TopLevelComponents(model).Count;
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { snapshot.traversal_complete = false; return snapshot; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureAssemblyFeatureRecursive(feature, snapshot, 0);
                if (!snapshot.traversal_complete) { Release(feature); return snapshot; }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { snapshot.traversal_complete = false; }
                Release(feature);
                if (!snapshot.traversal_complete) return snapshot;
                feature = next;
            }
            if (feature != null) snapshot.traversal_complete = false;
            return snapshot;
        }

        private static void CaptureAssemblyFeatureRecursive(Feature feature, AssemblySnapshot snapshot, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || snapshot.feature_count > 20000)
            {
                snapshot.traversal_complete = false;
                return;
            }
            snapshot.feature_count++;
            bool warning;
            int error2;
            int error1;
            bool suppressed;
            string name;
            string type;
            Feature child;
            try
            {
                warning = false;
                error2 = feature.GetErrorCode2(out warning);
                error1 = feature.GetErrorCode();
                suppressed = feature.IsSuppressed();
                name = feature.Name;
                type = feature.GetTypeName2();
                child = feature.GetFirstSubFeature() as Feature;
            }
            catch { snapshot.traversal_complete = false; return; }
            if (!suppressed && (error1 > 0 || error2 > 0 || warning))
                snapshot.issues.Add(new FeatureIssue
                {
                    name = name,
                    type = type,
                    error_code = error1,
                    error_code2 = error2,
                    warning = warning,
                    depth = depth
                });
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureAssemblyFeatureRecursive(child, snapshot, depth + 1);
                if (!snapshot.traversal_complete) { Release(child); return; }
                Feature next = null;
                try { next = child.GetNextSubFeature() as Feature; }
                catch { snapshot.traversal_complete = false; }
                Release(child);
                if (!snapshot.traversal_complete) return;
                child = next;
            }
            if (child != null) snapshot.traversal_complete = false;
        }

        private static bool AssemblyHealthGate(AssemblySnapshot snapshot, bool root)
        {
            if (snapshot == null || !snapshot.traversal_complete ||
                snapshot.top_level_component_count <= 0) return false;
            return snapshot.issues.Count == 0;
        }

        private static List<Component2> TopLevelComponents(ModelDoc2 model)
        {
            var output = new List<Component2>();
            Configuration configuration = Safe(delegate { return model.GetActiveConfiguration() as Configuration; }, null);
            Component2 root = configuration == null ? null :
                Safe(delegate { return configuration.GetRootComponent3(true); }, null);
            Array children = root == null ? null : Safe(delegate { return root.GetChildren() as Array; }, null);
            if (children != null)
                foreach (object value in children)
                {
                    Component2 component = value as Component2;
                    if (component != null) output.Add(component);
                }
            return output;
        }

        private static List<Component2> ExactComponents(List<Component2> components,
            string exactPath, string exactName)
        {
            var output = new List<Component2>();
            foreach (Component2 component in components)
            {
                string path = Safe(delegate { return component.GetPathName(); }, "");
                string name = Safe(delegate { return component.Name2; }, "");
                int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
                if (slash >= 0) name = name.Substring(slash + 1);
                if (SamePath(path, exactPath) && string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase))
                    output.Add(component);
            }
            return output;
        }

        private static int CountTopLevelByPath(List<Component2> components, string path)
        {
            int count = 0;
            foreach (Component2 component in components)
                if (SamePath(Safe(delegate { return component.GetPathName(); }, ""), path)) count++;
            return count;
        }

        private static double ComponentXmm(Component2 component)
        {
            double[] values = TransformData(Safe(delegate { return component.Transform2 as MathTransform; }, null));
            return values == null ? double.NaN : values[9] * 1000.0;
        }

        private static double[] TransformData(MathTransform transform)
        {
            Array raw = transform == null ? null : Safe(delegate { return transform.ArrayData as Array; }, null);
            if (raw == null || raw.Length < 13) return null;
            var output = new double[Math.Max(16, raw.Length)];
            for (int index = 0; index < raw.Length; index++)
                output[index] = Convert.ToDouble(raw.GetValue(index), CultureInfo.InvariantCulture);
            if (output[12] == 0.0) output[12] = 1.0;
            return output;
        }

        private static bool SameExceptX(double[] before, double[] after, double tolerance)
        {
            if (before == null || after == null || before.Length < 13 || after.Length < 13) return false;
            for (int index = 0; index < 13; index++)
            {
                if (index == 9) continue;
                if (Math.Abs(before[index] - after[index]) > tolerance) return false;
            }
            return true;
        }

        private static bool SameExceptXZ(double[] before, double[] after, double tolerance)
        {
            if (before == null || after == null || before.Length < 13 || after.Length < 13)
                return false;
            for (int index = 0; index < 13; index++)
            {
                if (index == 9 || index == 11) continue;
                if (Math.Abs(before[index] - after[index]) > tolerance) return false;
            }
            return true;
        }

        private static bool SameTransform(double[] before, double[] after, double tolerance)
        {
            if (before == null || after == null || before.Length < 13 || after.Length < 13) return false;
            for (int index = 0; index < 13; index++)
                if (Math.Abs(before[index] - after[index]) > tolerance) return false;
            return true;
        }

        private static bool AssemblyInitialOpenGate(AssemblyPlan plan, int errors, int warnings,
            bool allowNeedsRegenOnly)
        {
            int stable = plan.expected_stable_open_warnings;
            int needsRegen = stable + (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen;
            bool needsRegenOnly = allowNeedsRegenOnly &&
                warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen;
            return errors == 0 && (warnings == stable || warnings == needsRegen ||
                needsRegenOnly);
        }

        private static bool AssemblyReopenGate(AssemblyPlan plan, int errors, int warnings)
        {
            return errors == 0 && (warnings == plan.expected_stable_open_warnings ||
                (warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen &&
                    AssemblyNeedsRegenWarningAllowlist().Contains(plan.file_name)));
        }

        private static HashSet<string> AssemblyNeedsRegenWarningAllowlist()
        {
            return new HashSet<string>(new[]
            {
                "储物柜门2╱12焊接_左.SLDASM", "储物柜门2╱12焊接_右.SLDASM",
                "储物柜门4╱12焊接_左.SLDASM", "储物柜门4╱12焊接_右.SLDASM",
                "储物柜门6╱12焊接_左.SLDASM", "储物柜门6╱12焊接_右.SLDASM",
                "储物柜门装配_L2.SLDASM", "储物柜门装配_R2.SLDASM",
                "储物柜门装配_L4.SLDASM", "储物柜门装配_R4.SLDASM",
                "储物柜门装配_L6.SLDASM", "储物柜门装配_R6.SLDASM",
                "底座焊接.SLDASM", "上盖焊接.SLDASM", "门框焊接.sldasm",
                "箱体横层板L焊接.SLDASM", "箱体横层板R焊接.SLDASM",
                "箱体竖隔板L焊接.SLDASM", "箱体竖隔板R焊接.SLDASM",
                "箱体左侧板焊接.sldasm", "箱体右侧板焊接.SLDASM",
                SeedRootFileName
            }, StringComparer.OrdinalIgnoreCase);
        }

        private static bool VerifyTransformRows(List<Component2> components,
            List<TransformRow> rows, string cadDir, bool reopen)
        {
            bool success = true;
            foreach (TransformRow row in rows)
            {
                List<Component2> matches = ExactComponents(components,
                    ExactFile(cadDir, row.component_file_name), row.component_name);
                MathTransform transform = matches.Count == 1 ?
                    Safe(delegate { return matches[0].Transform2 as MathTransform; }, null) : null;
                double[] values = TransformData(transform);
                double xMm = values == null ? double.NaN : values[9] * 1000.0;
                double zMm = values == null ? double.NaN : values[11] * 1000.0;
                bool fixedState = matches.Count == 1 &&
                    Safe(delegate { return matches[0].IsFixed(); }, false);
                bool suppressedState = matches.Count == 1 &&
                    Safe(delegate { return matches[0].IsSuppressed(); }, false);
                bool targetZ = !row.explicit_z_target || matches.Count == 1 && values != null &&
                    Near(zMm, row.target_z_mm, TransformToleranceMm);
                bool target = matches.Count == 1 && values != null &&
                    Near(xMm, row.target_x_mm, TransformToleranceMm) && targetZ;
                bool nonXPreserved = matches.Count == 1 &&
                    (row.explicit_z_target ? SameExceptXZ(row.before_transform, values, 1e-10) :
                        SameExceptX(row.before_transform, values, 1e-10));
                bool statesPreserved = matches.Count == 1 &&
                    fixedState == row.was_fixed && suppressedState == row.was_suppressed;
                bool pass = target && nonXPreserved && statesPreserved;
                if (reopen)
                {
                    row.reopen_match_count = matches.Count;
                    row.reopen_transform = values;
                    row.reopen_x_mm = xMm;
                    row.reopen_z_mm = zMm;
                    row.reopen_target_z_readback = targetZ;
                    row.reopen_is_fixed = fixedState;
                    row.reopen_is_suppressed = suppressedState;
                    row.reopen_target_readback = target;
                    row.reopen_rotation_y_z_preserved = nonXPreserved;
                    row.reopen_fixed_suppressed_preserved = statesPreserved;
                    row.reopen_gate = pass;
                }
                else
                {
                    row.after_rebuild_match_count = matches.Count;
                    row.after_rebuild_transform = values;
                    row.after_rebuild_x_mm = xMm;
                    row.after_rebuild_z_mm = zMm;
                    row.after_rebuild_target_z_readback = targetZ;
                    row.after_rebuild_is_fixed = fixedState;
                    row.after_rebuild_is_suppressed = suppressedState;
                    row.after_rebuild_target_readback = target;
                    row.after_rebuild_rotation_y_z_preserved = nonXPreserved;
                    row.after_rebuild_fixed_suppressed_preserved = statesPreserved;
                    row.after_rebuild_gate = pass;
                }
                if (!pass) success = false;
                Release(transform);
            }
            return success;
        }

        private static ComponentDigest CaptureNonTargetDigest(List<Component2> components,
            AssemblyPlan plan, string cadDir)
        {
            var digest = new ComponentDigest();
            foreach (Component2 component in components)
            {
                string path = Safe(delegate { return component.GetPathName(); }, "");
                string name = ComponentLeafName(Safe(delegate { return component.Name2; }, ""));
                if (IsAssemblyTarget(path, name, plan, cadDir)) continue;
                MathTransform transform = Safe(delegate { return component.Transform2 as MathTransform; }, null);
                digest.states.Add(new ComponentState
                {
                    component_name = name,
                    component_path = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path),
                    transform = TransformData(transform),
                    is_fixed = Safe(delegate { return component.IsFixed(); }, false),
                    is_suppressed = Safe(delegate { return component.IsSuppressed(); }, false)
                });
                Release(transform);
            }
            digest.states.Sort(delegate(ComponentState left, ComponentState right)
            {
                int name = StringComparer.OrdinalIgnoreCase.Compare(left.component_name, right.component_name);
                return name != 0 ? name :
                    StringComparer.OrdinalIgnoreCase.Compare(left.component_path, right.component_path);
            });
            digest.count = digest.states.Count;
            var builder = new StringBuilder();
            foreach (ComponentState state in digest.states)
            {
                builder.Append(state.component_name.ToLowerInvariant()).Append('|')
                    .Append(state.component_path.ToLowerInvariant()).Append('|')
                    .Append(state.is_fixed ? '1' : '0').Append('|')
                    .Append(state.is_suppressed ? '1' : '0');
                if (state.transform != null)
                    for (int index = 0; index < Math.Min(13, state.transform.Length); index++)
                        builder.Append('|').Append(state.transform[index].ToString("R",
                            CultureInfo.InvariantCulture));
                builder.Append('\n');
            }
            digest.sha256 = Sha256Text(builder.ToString());
            return digest;
        }

        private static bool NonTargetStatesEqual(List<ComponentState> baseline,
            List<ComponentState> current)
        {
            if (baseline == null || current == null || baseline.Count != current.Count) return false;
            for (int index = 0; index < baseline.Count; index++)
            {
                ComponentState left = baseline[index], right = current[index];
                bool sameComponentPath = string.IsNullOrWhiteSpace(left.component_path) &&
                    string.IsNullOrWhiteSpace(right.component_path) ||
                    SamePath(left.component_path, right.component_path);
                if (!sameComponentPath ||
                    !string.Equals(left.component_name, right.component_name,
                        StringComparison.OrdinalIgnoreCase) ||
                    left.is_fixed != right.is_fixed || left.is_suppressed != right.is_suppressed ||
                    !SameTransform(left.transform, right.transform, 1e-10)) return false;
            }
            return true;
        }

        private static bool IsAssemblyTarget(string path, string name,
            AssemblyPlan plan, string cadDir)
        {
            foreach (TransformTarget target in plan.targets)
                if (SamePath(path, ExactFile(cadDir, target.component_file_name)) &&
                    string.Equals(name, target.component_name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ComponentLeafName(string name)
        {
            int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            return slash >= 0 ? name.Substring(slash + 1) : name;
        }

        private static ModelDoc2 OpenDocument(ISldWorks sw, string path, int documentType,
            bool readOnly, ref int errors, ref int warnings)
        {
            int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
            if (readOnly) options |= (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;
            try
            {
                return sw.OpenDoc6(path, documentType, options, "", ref errors, ref warnings) as ModelDoc2;
            }
            catch { return null; }
        }

        private static void SaveModel(ModelDoc2 model, SaveRecord record, Result result)
        {
            int errors = 0, warnings = 0;
            AssertAuthorizationStillValid(result, "before_cad_save");
            record.save_attempted = true;
            record.saved = Safe(delegate
            {
                return model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref errors, ref warnings);
            }, false);
            record.save_errors = errors;
            record.save_warnings = warnings;
            record.save_flag_after_save = Safe(delegate { return model.GetSaveFlag(); }, true);
        }

        private static void CloseDocument(ISldWorks sw, ref ModelDoc2 model)
        {
            if (model == null) return;
            ModelDoc2 current = model;
            string title = Safe(delegate { return current.GetTitle(); }, "");
            Release(current);
            model = null;
            if (!string.IsNullOrWhiteSpace(title)) Try(delegate { sw.CloseDoc(title); });
        }

        private static ISldWorks StartOwnedSession(Result result, SessionRecord session)
        {
            List<int> monitorsBefore = ProcessIds("sldProcMon");
            ISldWorks sw = CreateOwned();
            if (sw == null) return null;
            ConfigureOwned(sw);
            session.sldworks_process_id = Safe(delegate { return sw.GetProcessID(); }, 0);
            session.sldworks_start_utc_ticks = ProcessStartUtcTicks(
                session.sldworks_process_id, "SLDWORKS");
            session.solidworks_revision = Safe(delegate { return sw.RevisionNumber(); }, "");
            session.solidworks_executable_path = ProcessExecutablePath(session.sldworks_process_id,
                "SLDWORKS");
            session.solidworks_executable_sha256 = File.Exists(session.solidworks_executable_path) ?
                Sha256(session.solidworks_executable_path) : "";
            session.created = session.sldworks_process_id > 0 &&
                session.sldworks_start_utc_ticks > 0 &&
                !result.baseline_sldworks_process_ids.Contains(session.sldworks_process_id) &&
                session.solidworks_revision.StartsWith(ExpectedSolidWorksVersionPrefix,
                    StringComparison.Ordinal) && SamePath(session.solidworks_executable_path,
                    ExpectedSolidWorksExePath) && string.Equals(session.solidworks_executable_sha256,
                    ExpectedSolidWorksExeSha256, StringComparison.OrdinalIgnoreCase);
            if (session.created && !result.created_sldworks_process_ids.Contains(session.sldworks_process_id))
                result.created_sldworks_process_ids.Add(session.sldworks_process_id);
            Thread.Sleep(500);
            CaptureSessionSldProcMon(result, session, monitorsBefore);
            session.started = true;
            return sw;
        }

        private static bool OwnedSessionIsExclusive(SessionRecord session)
        {
            if (session == null || !session.created || session.sldworks_process_id <= 0) return false;
            List<int> running = ProcessIds("SLDWORKS");
            return running.Count == 1 && running[0] == session.sldworks_process_id &&
                ExactProcessIdentityExists(session.sldworks_process_id, "SLDWORKS",
                    session.sldworks_start_utc_ticks, session.solidworks_executable_path);
        }

        private static ISldWorks CreateOwned()
        {
            const string progId = "SldWorks.Application.28";
            try
            {
                Type type = Type.GetTypeFromProgID(progId, true);
                return type == null ? null : Activator.CreateInstance(type) as ISldWorks;
            }
            catch { }
            return null;
        }

        private static void ConfigureOwned(ISldWorks sw)
        {
            Try(delegate { sw.Visible = false; });
            Try(delegate { sw.UserControl = false; });
            Try(delegate { sw.CommandInProgress = true; });
        }

        private static void CloseOwnedSession(ref ISldWorks sw, Result result, SessionRecord session)
        {
            if (session == null) return;
            if (sw == null)
            {
                session.process_exited = WaitForExactProcessExit(session, 1000);
                if (!session.process_exited) ForceStopOwnedSessionProcess(result, session);
                CleanupSessionSldProcMon(result, session);
                return;
            }
            CaptureSessionSldProcMon(result, session, null);
            ISldWorks current = sw;
            Try(delegate { current.CloseAllDocuments(true); });
            Try(delegate { current.CommandInProgress = false; });
            session.exit_requested = TryAction(delegate { current.ExitApp(); });
            Release(current);
            sw = null;
            session.process_exited = WaitForExactProcessExit(session, 10000);
            if (!session.process_exited) ForceStopOwnedSessionProcess(result, session);
            CleanupSessionSldProcMon(result, session);
        }

        private static void CleanupOnlyCreatedProcesses(Result result)
        {
            foreach (SessionRecord session in result.sessions)
            {
                if (!session.process_exited) ForceStopOwnedSessionProcess(result, session);
                CleanupSessionSldProcMon(result, session);
            }
        }

        private static void CaptureSessionSldProcMon(Result result, SessionRecord session,
            List<int> monitorsBefore)
        {
            if (session == null || !OwnedSessionIsExclusive(session)) return;
            var before = new HashSet<int>(monitorsBefore ?? new List<int>());
            var assigned = new HashSet<int>(result.sessions.SelectMany(delegate(SessionRecord row)
            {
                return row.sldprocmon_process_ids;
            }));
            string exactParent = ExactParentPattern(session.sldworks_process_id);
            foreach (ProcessIdentity identity in ProcessInfoByName("sldProcMon"))
                if (!before.Contains(identity.pid) && !assigned.Contains(identity.pid) &&
                    !result.baseline_sldprocmon_process_ids.Contains(identity.pid) &&
                    identity.start_ticks_utc > 0 && Regex.IsMatch(identity.command_line ?? "", exactParent,
                        RegexOptions.CultureInvariant))
                {
                    identity.parent_sldworks_process_id = session.sldworks_process_id;
                    identity.ownership_verified = true;
                    session.sldprocmon_processes.Add(identity);
                    session.sldprocmon_process_ids.Add(identity.pid);
                    assigned.Add(identity.pid);
                    if (!result.created_sldprocmon_process_ids.Contains(identity.pid))
                    result.created_sldprocmon_process_ids.Add(identity.pid);
                }
        }

        private static List<int> ProcessIds(string name)
        {
            var output = new List<int>();
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try { output.Add(process.Id); }
                finally { process.Dispose(); }
            }
            output.Sort();
            return output;
        }

        private static void CleanupSessionSldProcMon(Result result, SessionRecord session)
        {
            if (session == null) return;
            foreach (ProcessIdentity identity in session.sldprocmon_processes.ToList())
            {
                int pid = identity.pid;
                if (result.baseline_sldprocmon_process_ids.Contains(pid))
                {
                    AddUnique(result.cleanup_refused_baseline_process_ids, pid);
                    continue;
                }
                try
                {
                    using (Process process = Process.GetProcessById(pid))
                    {
                        if (process.HasExited)
                        {
                            AddUnique(result.cleanup_already_exited_process_ids, pid);
                            continue;
                        }
                        ProcessIdentity current = ExactOwnedMonitor(identity,
                            session.sldworks_process_id);
                        if (current == null)
                        {
                            result.cleanup_errors.Add("refused reused monitor PID " +
                                pid.ToString(CultureInfo.InvariantCulture) + " named " +
                                process.ProcessName);
                            continue;
                        }
                        process.Kill();
                        process.WaitForExit(10000);
                        AddUnique(result.cleanup_force_stopped_process_ids, pid);
                    }
                }
                catch (ArgumentException)
                {
                    AddUnique(result.cleanup_already_exited_process_ids, pid);
                }
                catch (Exception ex)
                {
                    result.cleanup_errors.Add("monitor PID " +
                        pid.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
                }
            }
        }

        private static void ForceStopOwnedSessionProcess(Result result, SessionRecord session)
        {
            if (session == null || session.sldworks_process_id <= 0) return;
            int pid = session.sldworks_process_id;
            if (result.baseline_sldworks_process_ids.Contains(pid))
            {
                AddUnique(result.cleanup_refused_baseline_process_ids, pid);
                return;
            }
            if (!ExactProcessIdentityExists(pid, "SLDWORKS",
                session.sldworks_start_utc_ticks, session.solidworks_executable_path))
            {
                session.process_exited = true;
                AddUnique(result.cleanup_already_exited_process_ids, pid);
                return;
            }
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
                session.process_exited = WaitForExactProcessExit(session, 2000);
                if (session.process_exited) AddUnique(result.cleanup_force_stopped_process_ids, pid);
                else result.cleanup_errors.Add("owned SLDWORKS PID " +
                    pid.ToString(CultureInfo.InvariantCulture) + " did not exit after exact kill");
            }
            catch (ArgumentException)
            {
                session.process_exited = true;
                AddUnique(result.cleanup_already_exited_process_ids, pid);
            }
            catch (Exception ex)
            {
                result.cleanup_errors.Add("owned SLDWORKS PID " +
                    pid.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
            }
        }

        private static long ProcessStartUtcTicks(int pid, string expectedName)
        {
            if (pid <= 0) return 0;
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    if (process.HasExited || !string.Equals(process.ProcessName, expectedName,
                        StringComparison.OrdinalIgnoreCase)) return 0;
                    return process.StartTime.ToUniversalTime().Ticks;
                }
            }
            catch { return 0; }
        }

        private static string ProcessExecutablePath(int pid, string expectedName)
        {
            if (pid <= 0) return "";
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    if (process.HasExited || !string.Equals(process.ProcessName, expectedName,
                        StringComparison.OrdinalIgnoreCase)) return "";
                    return Path.GetFullPath(process.MainModule.FileName);
                }
            }
            catch (Win32Exception) { return ""; }
            catch { return ""; }
        }

        private static string ExactParentPattern(int parentPid)
        {
            return "(?:^|\\s)--ppid=" + parentPid.ToString(CultureInfo.InvariantCulture) + "(?:\\s|$)";
        }

        private static List<ProcessIdentity> ProcessInfoByName(string name)
        {
            var output = new List<ProcessIdentity>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId,Name,CommandLine,ExecutablePath FROM Win32_Process WHERE Name='" +
                    name.Replace("'", "''") + ".exe'"))
                using (ManagementObjectCollection rows = searcher.Get())
                    foreach (ManagementObject row in rows)
                    {
                        int pid = Convert.ToInt32(row["ProcessId"], CultureInfo.InvariantCulture);
                        output.Add(new ProcessIdentity
                        {
                            pid = pid,
                            name = Path.GetFileNameWithoutExtension(Convert.ToString(row["Name"],
                                CultureInfo.InvariantCulture) ?? ""),
                            command_line = Convert.ToString(row["CommandLine"], CultureInfo.InvariantCulture) ?? "",
                            executable_path = Convert.ToString(row["ExecutablePath"],
                                CultureInfo.InvariantCulture) ?? "",
                            start_ticks_utc = ProcessStartUtcTicks(pid, name)
                        });
                    }
            }
            catch { }
            return output;
        }

        private static ProcessIdentity ExactOwnedMonitor(ProcessIdentity expected, int parentPid)
        {
            if (expected == null || !expected.ownership_verified ||
                expected.parent_sldworks_process_id != parentPid) return null;
            string pattern = ExactParentPattern(parentPid);
            return ProcessInfoByName("sldProcMon").FirstOrDefault(delegate(ProcessIdentity current)
            {
                return current.pid == expected.pid && current.start_ticks_utc == expected.start_ticks_utc &&
                    SamePath(current.executable_path, expected.executable_path) &&
                    Regex.IsMatch(current.command_line ?? "", pattern, RegexOptions.CultureInvariant);
            });
        }

        private static bool ExactProcessIdentityExists(int pid, string expectedName,
            long expectedStartUtcTicks, string expectedExecutablePath)
        {
            if (pid <= 0 || expectedStartUtcTicks <= 0) return false;
            return ProcessStartUtcTicks(pid, expectedName) == expectedStartUtcTicks &&
                SamePath(ProcessExecutablePath(pid, expectedName), expectedExecutablePath);
        }

        private static bool WaitForExactProcessExit(SessionRecord session, int timeoutMs)
        {
            if (session == null || session.sldworks_process_id <= 0) return true;
            Stopwatch watch = Stopwatch.StartNew();
            int consecutiveAbsentSamples = 0;
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                if (!ExactProcessIdentityExists(session.sldworks_process_id, "SLDWORKS",
                    session.sldworks_start_utc_ticks, session.solidworks_executable_path))
                    consecutiveAbsentSamples++;
                else consecutiveAbsentSamples = 0;
                if (consecutiveAbsentSamples >= 3) return true;
                Thread.Sleep(250);
            }
            return false;
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (value > 0 && !values.Contains(value)) values.Add(value);
        }

        private static void RollBack(Result result)
        {
            if (!result.backup_complete)
            {
                result.original_restored_on_failure = result.files.Count == 0;
                return;
            }
            result.rollback_attempted = true;
            bool all = true;
            try
            {
                var initialPaths = new HashSet<string>(result.files.Select(delegate(FileRecord row)
                {
                    return Path.GetFullPath(row.path);
                }),
                    StringComparer.OrdinalIgnoreCase);
                foreach (string path in CadInventory(result.cad_directory))
                    if (!initialPaths.Contains(Path.GetFullPath(path)))
                    {
                        File.Delete(path);
                        result.rollback_deleted_new_cad_paths.Add(path);
                    }
                CadTreeSnapshot afterDelete = CaptureCadTree(result.cad_directory);
                foreach (string directory in afterDelete.directories.OrderByDescending(
                    delegate(string value) { return value.Length; }))
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory, false);
                        result.rollback_deleted_new_directories.Add(directory);
                    }
            }
            catch (Exception ex)
            {
                result.rollback_inventory_error = SafeExceptionText(ex);
                all = false;
            }
            foreach (FileRecord file in result.files)
            {
                try
                {
                    if (!File.Exists(file.backup_path)) { all = false; continue; }
                    RestoreRegularFileFromBackup(file.backup_path, file.path, result.cad_directory);
                    File.SetLastWriteTimeUtc(file.path, new DateTime(file.last_write_utc_ticks_before, DateTimeKind.Utc));
                    file.rollback_hash = Sha256(file.path);
                    file.rollback_succeeded = string.Equals(file.rollback_hash, file.sha_before,
                        StringComparison.OrdinalIgnoreCase);
                    if (!file.rollback_succeeded) all = false;
                }
                catch (Exception ex)
                {
                    file.rollback_error = SafeExceptionText(ex);
                    all = false;
                }
            }
            result.rollback_succeeded = all;
            result.original_restored_on_failure = all;
        }

        private static void RestoreRegularFileFromBackup(string backupPath, string targetPath,
            string cadDirectory)
        {
            string backup = Path.GetFullPath(backupPath);
            string target = Path.GetFullPath(targetPath);
            string cadRoot = Path.GetFullPath(cadDirectory).TrimEnd('\\', '/');
            Require(File.Exists(backup) && !HasReparsePoint(backup) && FileLinkCount(backup) == 1,
                "ROLLBACK_BACKUP_FILE_IDENTITY_INVALID", 92,
                "rollback backup must be a single-link regular file");
            Require(SamePath(Path.GetDirectoryName(target), cadRoot) && !HasReparsePoint(cadRoot),
                "ROLLBACK_TARGET_PATH_INVALID", 92,
                "rollback target must be a direct child of the non-reparse working_pack");
            if (File.Exists(target) || Directory.Exists(target))
            {
                FileAttributes attributes = File.GetAttributes(target);
                bool directory = (attributes & FileAttributes.Directory) != 0;
                bool reparse = (attributes & FileAttributes.ReparsePoint) != 0;
                if (directory)
                {
                    Require(reparse, "ROLLBACK_REFUSED_REAL_DIRECTORY_AT_FILE_PATH", 92,
                        "rollback will only unlink a directory reparse point at a CAD file path");
                    Directory.Delete(target, false);
                }
                else File.Delete(target);
            }
            Require(!File.Exists(target) && !Directory.Exists(target),
                "ROLLBACK_TARGET_UNLINK_FAILED", 92,
                "rollback target directory entry could not be safely removed");
            File.Copy(backup, target, false);
            Require(File.Exists(target) && !HasReparsePoint(target) && FileLinkCount(target) == 1,
                "ROLLBACK_RESTORED_FILE_IDENTITY_INVALID", 92,
                "rollback must recreate a new single-link regular file before hash verification");
        }

        private static void AssertRollbackInventory(Result result)
        {
            try
            {
                CadTreeSnapshot tree = CaptureCadTree(result.cad_directory);
                List<string> inventory = tree.cad_files;
                List<string> names = inventory.Select(Path.GetFileName).OrderBy(delegate(string value)
                {
                    return value;
                }, StringComparer.OrdinalIgnoreCase).ToList();
                bool hashes = result.files.Count == 75 && result.files.All(delegate(FileRecord file)
                {
                    return File.Exists(file.path) && string.Equals(Sha256(file.path), file.sha_before,
                        StringComparison.OrdinalIgnoreCase);
                });
                int parts = inventory.Count(delegate(string path)
                {
                    return string.Equals(Path.GetExtension(path), ".SLDPRT", StringComparison.OrdinalIgnoreCase);
                });
                int assemblies = inventory.Count(delegate(string path)
                {
                    return string.Equals(Path.GetExtension(path), ".SLDASM", StringComparison.OrdinalIgnoreCase);
                });
                bool safeFiles = tree.directories.Count == 0 && inventory.All(delegate(string path)
                {
                    return SamePath(Path.GetDirectoryName(path), result.cad_directory) &&
                        !HasReparsePoint(path) && FileLinkCount(path) == 1;
                });
                result.rollback_inventory_gate = safeFiles && inventory.Count == 75 &&
                    parts == 53 && assemblies == 22 &&
                    names.SequenceEqual(result.initial_cad_file_names, StringComparer.OrdinalIgnoreCase) && hashes;
                result.rollback_succeeded = result.rollback_succeeded && result.rollback_inventory_gate;
                result.original_restored_on_failure = result.rollback_succeeded;
                if (!result.rollback_inventory_gate)
                    result.cleanup_errors.Add("rollback did not restore the exact original 75 CAD names/hashes");
            }
            catch (Exception ex)
            {
                result.rollback_inventory_error = SafeExceptionText(ex);
                result.rollback_inventory_gate = false;
                result.rollback_succeeded = false;
                result.original_restored_on_failure = false;
                result.cleanup_errors.Add("rollback inventory audit failed: " + ex.Message);
            }
        }

        private static void DeleteBackupDirectory(Result result)
        {
            if (string.IsNullOrWhiteSpace(result.backup_directory) ||
                !Directory.Exists(result.backup_directory)) return;
            if (!result.success && !result.original_restored_on_failure) return;
            try
            {
                Directory.Delete(result.backup_directory, true);
                result.backup_deleted = !Directory.Exists(result.backup_directory);
            }
            catch (Exception ex)
            {
                result.backup_delete_error = SafeExceptionText(ex);
            }
        }

        private static FileRecord FileRecordFor(Result result, string path)
        {
            FileRecord record = result.files.FirstOrDefault(delegate(FileRecord item)
            {
                return SamePath(item.path, path);
            });
            if (record == null) throw new InvalidOperationException("missing backup record for " + path);
            return record;
        }

        private static string ExactFile(string cadDir, string fileName)
        {
            return Path.GetFullPath(Path.Combine(cadDir, fileName));
        }

        private static bool IsUnder(string path, string directory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory)) return false;
            try
            {
                string fullPath = Path.GetFullPath(path).TrimEnd('\\', '/') + "\\";
                string fullDirectory = Path.GetFullPath(directory).TrimEnd('\\', '/') + "\\";
                return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fullPath.TrimEnd('\\'), fullDirectory.TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
                    Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static int FeatureErrorCode2(Feature feature, out bool warning)
        {
            warning = false;
            try { return feature.GetErrorCode2(out warning); }
            catch { return Safe(delegate { return feature.GetErrorCode(); }, 0); }
        }

        private static int MaxLength(params Array[] arrays)
        {
            int maximum = 0;
            foreach (Array array in arrays) if (array != null) maximum = Math.Max(maximum, array.Length);
            return maximum;
        }

        private static string ArrayString(Array array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return "";
            return Convert.ToString(array.GetValue(index), CultureInfo.InvariantCulture) ?? "";
        }

        private static int ArrayInt(Array array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return -1;
            return Convert.ToInt32(array.GetValue(index), CultureInfo.InvariantCulture);
        }

        private static double[] DoubleArray(Array array)
        {
            if (array == null) return null;
            var output = new double[array.Length];
            for (int index = 0; index < array.Length; index++)
                output[index] = Convert.ToDouble(array.GetValue(index), CultureInfo.InvariantCulture);
            return output;
        }

        private static int ArrayLength(Array array)
        {
            return array == null ? 0 : array.Length;
        }

        private static bool Near(double left, double right, double tolerance)
        {
            return !double.IsNaN(left) && !double.IsInfinity(left) && Math.Abs(left - right) <= tolerance;
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 hash = SHA256.Create())
            {
                var builder = new StringBuilder();
                foreach (byte value in hash.ComputeHash(stream))
                    builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string Sha256Bytes(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
            {
                var builder = new StringBuilder();
                foreach (byte value in hash.ComputeHash(bytes ?? new byte[0]))
                    builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string Sha256Text(string value)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(value ?? "");
                var builder = new StringBuilder();
                foreach (byte item in hash.ComputeHash(bytes))
                    builder.Append(item.ToString("X2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static void WriteJsonAtomic(string path, object result)
        {
            EnsureParent(path);
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = SerializeJsonLine(result);
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) Try(delegate { File.Delete(temporary); });
            }
        }

        private static void WriteJsonAtomicNew(string path, object result)
        {
            EnsureParent(path);
            Require(!File.Exists(path) && !Directory.Exists(path),
                "EVIDENCE_ALREADY_EXISTS", 91,
                "success evidence must be atomically created at a new fixed path");
            string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            byte[] bytes = SerializeJsonLine(result);
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                Require(!HasReparsePoint(temporary) && FileLinkCount(temporary) == 1 &&
                    File.ReadAllBytes(temporary).SequenceEqual(bytes),
                    "EVIDENCE_PENDING_BYTES_VERIFY_FAILED", 91,
                    "pending evidence bytes or file identity changed before commit");
                File.Move(temporary, path);
                Require(File.Exists(path) && !HasReparsePoint(path) && FileLinkCount(path) == 1 &&
                    File.ReadAllBytes(path).SequenceEqual(bytes),
                    "EVIDENCE_ATOMIC_CREATE_VERIFY_FAILED", 91,
                    "final evidence bytes or file identity changed after commit");
            }
            finally
            {
                if (File.Exists(temporary)) Try(delegate { File.Delete(temporary); });
            }
        }

        private static byte[] SerializeJsonLine(object value)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
            return new UTF8Encoding(false).GetBytes(serializer.Serialize(value) +
                System.Environment.NewLine);
        }

        private static void EnsureParent(string path)
        {
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
        }

        private static void Require(bool condition, string status, int exitCode, string message)
        {
            if (!condition) throw new GateException(status, exitCode, message);
        }

        private static string SafeExceptionText(Exception ex)
        {
            try { return ex == null ? "" : ex.ToString().Replace("\r\n", "\n"); }
            catch { return "unprintable exception"; }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                try { Marshal.FinalReleaseComObject(value); } catch { }
        }

        private static void Try(Action action) { try { action(); } catch { } }
        private static bool TryAction(Action action) { try { action(); return true; } catch { return false; } }
        private static T Safe<T>(Func<T> action, T fallback) { try { return action(); } catch { return fallback; } }

        private sealed class AuthorizationBinding
        {
            public string authorization_id = "";
            public string issued_at_utc = "";
            public string expires_at_utc = "";
            public string lease_id = "";
            public string lease_expires_at_utc = "";
        }

        private sealed class PreparedReceipt
        {
            public string path = "";
            public string temporary_path = "";
            public string sha256 = "";
            public string predecessor_sha256 = "";
        }

        private sealed class ToolchainManifestBinding
        {
            public string path = "";
            public string sha256 = "";
            public string source_normalized_sha256 = "";
            public string executable_sha256 = "";
            public string verifier_sha256 = "";
        }

        private sealed class StaticSelfTestReport
        {
            public string status = "";
            public int checksFailed;
            public readonly List<StaticSelfTestCheck> checks = new List<StaticSelfTestCheck>();
        }

        private sealed class StaticSelfTestCheck
        {
            public string name = "";
            public bool ok;
            public string detail = "";
        }

        private sealed class GateException : Exception
        {
            public readonly string Status;
            public readonly int ExitCode;
            public GateException(string status, int exitCode, string message) : base(message)
            {
                Status = status;
                ExitCode = exitCode;
            }
        }

        private sealed class DimensionPlan
        {
            public string file_name;
            public bool require_sheet_metal;
            public readonly List<DimensionTarget> targets = new List<DimensionTarget>();
            public readonly List<DimensionReadbackTarget> readbacks = new List<DimensionReadbackTarget>();
            public DimensionPlan(string file, bool sheetMetal, params DimensionTarget[] values)
            {
                file_name = file;
                require_sheet_metal = sheetMetal;
                targets.AddRange(values);
            }
            public DimensionPlan WithReadbacks(params DimensionReadbackTarget[] values)
            {
                readbacks.AddRange(values);
                return this;
            }
        }

        private sealed class DimensionTarget
        {
            public string name = "";
            public double source_mm;
            public double target_mm;
            public int expected_driven_state;
        }

        private sealed class DimensionReadbackTarget
        {
            public string name = "";
            public double target_mm;
            public int expected_driven_state;
        }

        private sealed class DerivedPlan
        {
            public string file_name = "";
            public string source_file_name = "";
            public string link_type = "";
            public bool relock_required;
            public bool lock_after_refresh;
            public bool native_right_topcover;
            public bool native_frame_crossbar_right;
            public bool native_door_frame_right;
            public bool native_cabinet_shelf_right;
            public double source_xmin_mm;
            public double source_xmax_mm;
            public double target_xmin_mm;
            public double target_xmax_mm;
        }

        private sealed class AssemblyPlan
        {
            public string file_name;
            public bool is_root;
            public int expected_stable_open_warnings;
            public readonly List<TransformTarget> targets = new List<TransformTarget>();
            public AssemblyPlan(string file, bool root, params TransformTarget[] values)
                : this(file, root, root ? 1 : 0, values)
            {
            }
            public AssemblyPlan(string file, bool root, int stableWarnings,
                params TransformTarget[] values)
            {
                file_name = file;
                is_root = root;
                expected_stable_open_warnings = stableWarnings;
                targets.AddRange(values);
            }
        }

        private sealed class TransformTarget
        {
            public string component_name = "";
            public string component_file_name = "";
            public double source_x_mm;
            public double target_x_mm;
            public bool explicit_z_target;
            public double source_z_mm;
            public double target_z_mm;
        }

        private sealed class ComponentDigest
        {
            public int count;
            public string sha256 = "";
            public readonly List<ComponentState> states = new List<ComponentState>();
        }

        private sealed class CadTreeSnapshot
        {
            public readonly List<string> directories = new List<string>();
            public readonly List<string> cad_files = new List<string>();
        }

        internal sealed class ComponentState
        {
            public string component_name = "";
            public string component_path = "";
            public double[] transform;
            public bool is_fixed;
            public bool is_suppressed;
        }

        public class SaveRecord
        {
            public bool save_attempted;
            public bool saved;
            public int save_errors;
            public int save_warnings;
            public bool save_flag_after_save = true;
        }

        public sealed class Result
        {
            public string schema = "";
            public string generated_at_utc = "";
            public string completed_at_utc = "";
            public string commit_completed_at_utc = "";
            public string cad_directory = "";
            public string repository_root = "";
            public string out_json = "";
            public string phase = "";
            public string purpose = "";
            public double source_width_mm;
            public double target_width_mm;
            public string transaction_mode = "";
            public bool cad_directory_allowlist_match;
            public bool output_path_allowlist_match;
            public string attempt_root = "";
            public string plan_path = "";
            public string plan_sha256 = "";
            public bool plan_sha_match;
            public string recipe_id = "";
            public double recipe_version;
            public string recipe_digest = "";
            public bool recipe_trust_gate;
            public string authorization_path = "";
            public string authorization_sha256 = "";
            public string authorization_id = "";
            public string authorization_issued_at_utc = "";
            public string authorization_json_base64 = "";
            public string authorization_seed_inventory_digest = "";
            public bool authorization_gate;
            public string authorization_validation_boundary =
                "short_lived_immutable_authorization_at_phase_start_before_each_save_and_before_commits_no_continuous_task_store_cas_claim";
            public readonly List<AuthorizationCheckpoint> authorization_checkpoints =
                new List<AuthorizationCheckpoint>();
            public string attempt_directory = "";
            public string worker_id = "";
            public string task_id = "";
            public int task_revision;
            public string task_digest = "";
            public string request_fingerprint = "";
            public string request_digest = "";
            public string lease_id = "";
            public string lease_expires_at_utc = "";
            public string authorization_expires_at_utc = "";
            public int attempt_number;
            public string tool_source_normalized_sha256 = "";
            public string tool_executable_sha256 = "";
            public bool toolchain_manifest_gate;
            public string seed_toolchain_manifest_path = "";
            public string seed_toolchain_manifest_sha256 = "";
            public string seed_tool_source_normalized_sha256 = "";
            public string seed_tool_executable_sha256 = "";
            public string width_toolchain_manifest_path = "";
            public string width_toolchain_manifest_sha256 = "";
            public bool seed_receipt_gate;
            public string seed_receipt_path = "";
            public string seed_receipt_sha256 = "";
            public string seed_target_inventory_digest = "";
            public string seed_root_sha_after_stable = "";
            public bool phase_lock_acquired;
            public string phase_lock_path = "";
            public bool phase_sequence_gate;
            public string evidence_commitment_sha256 = "";
            public string predecessor_receipt_sha256 = "";
            public bool width_stage_only = true;
            public bool functional_success;
            public bool success;
            public string status = "";
            public string error = "";
            public readonly List<string> external_geometry_pending = new List<string>();
            public readonly List<string> not_changed_pending_bbox = new List<string>();
            public readonly List<string> derived_exclusions = new List<string>();
            public readonly List<int> baseline_sldworks_process_ids = new List<int>();
            public readonly List<int> baseline_sldprocmon_process_ids = new List<int>();
            public readonly List<int> created_sldworks_process_ids = new List<int>();
            public readonly List<int> created_sldprocmon_process_ids = new List<int>();
            public readonly List<int> final_sldworks_process_ids = new List<int>();
            public readonly List<int> final_sldprocmon_process_ids = new List<int>();
            public readonly List<int> cleanup_refused_baseline_process_ids = new List<int>();
            public readonly List<int> cleanup_force_stopped_process_ids = new List<int>();
            public readonly List<int> cleanup_already_exited_process_ids = new List<int>();
            public readonly List<string> cleanup_errors = new List<string>();
            public readonly List<SessionRecord> sessions = new List<SessionRecord>();
            public string backup_directory = "";
            public bool backup_complete;
            public bool backup_deleted;
            public string backup_delete_error = "";
            public readonly List<string> rollback_deleted_new_cad_paths = new List<string>();
            public readonly List<string> rollback_deleted_new_directories = new List<string>();
            public string rollback_inventory_error = "";
            public bool rollback_inventory_gate;
            public bool rollback_attempted;
            public bool rollback_succeeded;
            public bool original_restored_on_failure;
            public bool evidence_write_attempted;
            public bool evidence_path_unused_at_start;
            public bool evidence_write_succeeded;
            public bool evidence_finalize_write_succeeded;
            public string evidence_finalize_error = "";
            public bool failure_evidence_write_succeeded;
            public int pack_inventory_count;
            public bool pack_inventory_all_local;
            public string frozen_right_partition_path = "";
            public string frozen_right_partition_sha256 = "";
            public bool frozen_right_partition_sha_match;
            public string seed_root_path = "";
            public string seed_root_sha256 = "";
            public bool seed_root_initial_hash_gate;
            public int root_dependency_raw_count;
            public int root_dependency_pack_total_count;
            public bool root_dependencies_all_local;
            public bool root_dependency_exact_inventory_match;
            public readonly List<string> initial_cad_file_names = new List<string>();
            public int initial_part_count;
            public int initial_assembly_count;
            public string initial_inventory_digest = "";
            public bool compiled_seed_profile_gate;
            public int post_part_count;
            public int post_assembly_count;
            public string post_inventory_digest = "";
            public bool post_inventory_and_closure_gate;
            public readonly List<string> actual_changed_paths = new List<string>();
            public readonly List<string> unexpected_changed_paths = new List<string>();
            public readonly List<FileRecord> files = new List<FileRecord>();
            public readonly List<DimensionPartRecord> dimension_parts = new List<DimensionPartRecord>();
            public BaseHoleRecord base_hole;
            public readonly List<DerivedPartRecord> derived_parts = new List<DerivedPartRecord>();
            public readonly List<string> assembly_reopen_needs_regen_files =
                new List<string>();
            public readonly List<AssemblyRecord> assemblies = new List<AssemblyRecord>();
        }

        public sealed class AuthorizationCheckpoint
        {
            public string name = "";
            public string checked_at_utc = "";
            public bool valid;
        }

        public sealed class SessionRecord
        {
            public string purpose = "";
            public bool started;
            public int sldworks_process_id;
            public long sldworks_start_utc_ticks;
            public readonly List<int> sldprocmon_process_ids = new List<int>();
            public readonly List<ProcessIdentity> sldprocmon_processes = new List<ProcessIdentity>();
            public string solidworks_revision = "";
            public string solidworks_executable_path = "";
            public string solidworks_executable_sha256 = "";
            public bool created;
            public bool exit_requested;
            public bool process_exited;
        }

        public sealed class ProcessIdentity
        {
            public int pid;
            public string name = "";
            public string command_line = "";
            public string executable_path = "";
            public long start_ticks_utc;
            public int parent_sldworks_process_id;
            public bool ownership_verified;
        }

        public sealed class FileRecord
        {
            public string path = "";
            public string backup_path = "";
            public bool backup_created;
            public string sha_before = "";
            public long size_before;
            public long last_write_utc_ticks_before;
            public string sha_after_save = "";
            public string sha_after_readonly_reopen = "";
            public long size_after;
            public string rollback_hash = "";
            public bool rollback_succeeded;
            public string rollback_error = "";
        }

        public sealed class PartSnapshot
        {
            public int body_count;
            public int feature_count;
            public bool traversal_complete;
            public int error_feature_count;
            public int warning_feature_count;
            public bool has_sheet_metal;
            public bool has_flat_pattern;
            public double[] part_box_m;
        }

        public sealed class DimensionPartRecord : SaveRecord
        {
            public string file_name = "";
            public string path = "";
            public bool opened;
            public int open_errors;
            public int open_warnings;
            public PartSnapshot before;
            public PartSnapshot after;
            public bool changed;
            public bool idempotent_already_target;
            public bool rebuilt;
            public bool save_not_required_idempotent;
            public string sha_after_save = "";
            public int reopen_open_errors;
            public int reopen_open_warnings;
            public bool reopen_rebuilt;
            public PartSnapshot reopen;
            public bool edit_gate;
            public bool verify_gate;
            public readonly List<DimensionRow> dimensions = new List<DimensionRow>();
            public readonly List<DimensionReadbackRow> readbacks = new List<DimensionReadbackRow>();
        }

        public sealed class DimensionRow
        {
            public string name = "";
            public double source_mm;
            public double target_mm;
            public bool found;
            public double before_mm;
            public int driven_state_before;
            public bool driving_before;
            public bool precondition_source_or_target;
            public bool changed;
            public double after_mm;
            public int driven_state_after;
            public bool target_readback;
            public bool reopen_found;
            public double reopen_mm;
            public int reopen_driven_state;
            public bool reopen_target_readback;
        }

        public sealed class DimensionReadbackRow
        {
            public string name = "";
            public double target_mm;
            public int expected_driven_state;
            public bool found;
            public double after_rebuild_mm;
            public int driven_state;
            public bool pass;
            public bool reopen_found;
            public double reopen_mm;
            public int reopen_driven_state;
            public bool reopen_pass;
        }

        public sealed class BaseHoleRecord : SaveRecord
        {
            public string file_name = "";
            public string path = "";
            public string source_path = "";
            public string source_sha_before = "";
            public bool source_opened;
            public int source_open_errors;
            public int source_open_warnings;
            public string source_sha_after = "";
            public bool source_hash_unchanged;
            public string sketch_name = "";
            public string consumer_feature_name = "";
            public double source_abs_x_mm;
            public double target_abs_x_mm;
            public double source_span_mm;
            public double target_span_mm;
            public double expected_d2_mm;
            public bool opened;
            public int open_errors;
            public int open_warnings;
            public PartSnapshot before;
            public LinkSnapshot links_before;
            public string links_before_digest = "";
            public bool consumer_feature_found;
            public int consumer_error_code;
            public int consumer_error_code2;
            public bool consumer_warning;
            public BaseHoleFeatureHealth consumer_before;
            public readonly List<BaseHoleBlockRow> before_blocks = new List<BaseHoleBlockRow>();
            public BaseHoleRelationSnapshot relations_before;
            public double d1_before_mm;
            public double d2_before_mm;
            public int d1_driven_state_before;
            public int d2_driven_state_before;
            public bool precondition_source;
            public bool precondition_target;
            public bool sketch_selected;
            public bool sketch_edit_entered;
            public int block_positions_set;
            public bool sketch_edit_exited;
            public bool sketch_exit_fallback_attempted;
            public bool sketch_exit_fallback_succeeded;
            public readonly List<BaseHolePositionUpdate> position_updates =
                new List<BaseHolePositionUpdate>();
            public bool changed;
            public bool idempotent_already_target;
            public bool rebuilt;
            public PartSnapshot after;
            public readonly List<BaseHoleBlockRow> after_blocks = new List<BaseHoleBlockRow>();
            public BaseHoleRelationSnapshot relations_after;
            public BaseHoleFeatureHealth consumer_after;
            public double d1_after_mm;
            public double d2_after_mm;
            public int d1_driven_state_after;
            public int d2_driven_state_after;
            public LinkSnapshot links_after;
            public string links_after_digest = "";
            public bool topology_preserved_after;
            public bool part_box_preserved_after;
            public bool save_not_required_idempotent;
            public string sha_after_save = "";
            public bool edit_gate;
            public bool reopen_source_opened;
            public int reopen_source_open_errors;
            public int reopen_source_open_warnings;
            public string source_sha_after_readonly_reopen = "";
            public bool source_reopen_hash_unchanged;
            public int reopen_open_errors;
            public int reopen_open_warnings;
            public bool reopen_rebuilt;
            public PartSnapshot reopen;
            public readonly List<BaseHoleBlockRow> reopen_blocks = new List<BaseHoleBlockRow>();
            public BaseHoleRelationSnapshot relations_reopen;
            public BaseHoleFeatureHealth consumer_reopen;
            public double d1_reopen_mm;
            public double d2_reopen_mm;
            public int d1_driven_state_reopen;
            public int d2_driven_state_reopen;
            public LinkSnapshot links_reopen;
            public string links_reopen_digest = "";
            public bool topology_preserved_reopen;
            public bool part_box_preserved_reopen;
            public bool verify_gate;
        }

        public sealed class BaseHoleBlockRow
        {
            public string name = "";
            public double x_mm;
            public double y_mm;
            public double z_mm;
            public double angle_rad;
            public double scale;
        }

        public sealed class BaseHoleFeatureHealth
        {
            public bool found;
            public string name = "";
            public string type = "";
            public bool suppressed;
            public int error_code;
            public int error_code2;
            public bool warning;
        }

        public sealed class BaseHolePositionUpdate
        {
            public string name = "";
            public double before_x_mm;
            public double before_y_mm;
            public double before_z_mm;
            public double before_angle_rad;
            public double before_scale;
            public bool point_created;
            public bool position_set;
            public double after_x_mm;
            public double after_y_mm;
            public double after_z_mm;
            public double after_angle_rad;
            public double after_scale;
            public bool readback_pass;
        }

        public sealed class BaseHoleRelationSnapshot
        {
            public int relation_count;
            public int coincident_count;
            public string digest_sha256 = "";
            public readonly List<BaseHoleRelationRow> rows = new List<BaseHoleRelationRow>();
        }

        public sealed class BaseHoleRelationRow
        {
            public int relation_type;
            public string relation_name = "";
            public bool suppressed;
            public int entity_count;
            public int definition_entity_count;
        }

        public sealed class NativeRightCylinderSnapshot
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
                    return NativeRightIsFinite(centerXmm) && NativeRightIsFinite(centerYmm) &&
                        NativeRightIsFinite(centerZmm) && NativeRightIsFinite(axisX) && NativeRightIsFinite(axisY) &&
                        NativeRightIsFinite(axisZ) && NativeRightIsFinite(radiusMm) && radiusMm > 0;
                }
            }
        }

        public sealed class NativeRightGeometrySnapshot
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
            public List<NativeRightCylinderSnapshot> cylinders = new List<NativeRightCylinderSnapshot>();
            public List<NativeRightDetailedFace> faces = new List<NativeRightDetailedFace>();
            public string translationNormalizedDetailedSignature = "";
            public bool finite;
        }

        public sealed class NativeRightDetailedFace
        {
            public double areaMm2;
            public List<double> boxMm = new List<double>();
            public List<double> normal = new List<double>();
            public List<double> planeParams = new List<double>();
            public List<double> cylinderParams = new List<double>();
            public int surfaceIdentity;
            public bool isPlane;
            public bool isCylinder;
            public List<NativeRightDetailedLoop> loops = new List<NativeRightDetailedLoop>();
        }

        public sealed class NativeRightDetailedLoop
        {
            public bool isOuter;
            public int edgeCount;
            public List<NativeRightDetailedEdge> edges = new List<NativeRightDetailedEdge>();
        }

        public sealed class NativeRightDetailedEdge
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

        public sealed class NativeRightBendSnapshot
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

        public sealed class NativeRightFeatureSnapshot
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
            public List<NativeRightBendSnapshot> bends = new List<NativeRightBendSnapshot>();
            public List<string> issues = new List<string>();
            public bool pass;
        }

        private sealed class FrameCrossbarBendSector
        {
            public readonly NativeRightPoint3 center;
            public readonly NativeRightPoint3 outer_start;
            public readonly NativeRightPoint3 outer_end;
            public readonly NativeRightPoint3 inner_start;
            public readonly NativeRightPoint3 inner_end;
            public FrameCrossbarBendSector(NativeRightPoint3 centerValue,
                NativeRightPoint3 outerStartValue, NativeRightPoint3 outerEndValue,
                NativeRightPoint3 innerStartValue, NativeRightPoint3 innerEndValue)
            {
                center = centerValue;
                outer_start = outerStartValue;
                outer_end = outerEndValue;
                inner_start = innerStartValue;
                inner_end = innerEndValue;
            }
        }

        private sealed class CabinetShelfTabLoop
        {
            public NativeRightPoint3 top_left;
            public NativeRightPoint3 left_arc_start;
            public NativeRightPoint3 left_center;
            public NativeRightPoint3 left_bottom;
            public NativeRightPoint3 right_bottom;
            public NativeRightPoint3 right_center;
            public NativeRightPoint3 right_arc_end;
            public NativeRightPoint3 top_right;
        }

        private sealed class NativeRightPoint3
        {
            public readonly double x;
            public readonly double y;
            public readonly double z;
            public NativeRightPoint3(double xValue, double yValue, double zValue)
            {
                x = xValue; y = yValue; z = zValue;
            }
            public string Key()
            {
                return x.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    y.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    z.ToString("F6", CultureInfo.InvariantCulture);
            }
            public bool Near(NativeRightPoint3 other, double tolerance)
            {
                return other != null && Math.Abs(x - other.x) <= tolerance &&
                    Math.Abs(y - other.y) <= tolerance &&
                    Math.Abs(z - other.z) <= tolerance;
            }
        }

        private sealed class NativeRightHolePoint
        {
            public readonly double xMm;
            public readonly double yMm;
            public readonly double zMm;
            public readonly double radiusMm;
            public NativeRightHolePoint(double xValueMm, double yValueMm, double zValueMm,
                double radiusValueMm)
            {
                xMm = xValueMm;
                yMm = yValueMm;
                zMm = zValueMm;
                radiusMm = radiusValueMm;
            }
        }

        public sealed class DerivedPartRecord : SaveRecord
        {
            public string file_name = "";
            public string path = "";
            public string source_file_name = "";
            public string expected_source_path = "";
            public string link_type = "";
            public bool relock_required;
            public bool lock_after_refresh;
            public bool native_right_route;
            public string native_right_route_id = "";
            public bool native_right_evidence_inputs_gate;
            public bool native_right_legacy_mirror_precondition;
            public bool native_right_backup_precondition;
            public bool native_right_replaced;
            public NativeRightGeometrySnapshot native_right_sharp_stock;
            public bool native_right_sharp_stock_gate;
            public NativeRightFeatureSnapshot native_right_insert_bends_features;
            public NativeRightGeometrySnapshot native_right_insert_bends_geometry;
            public bool native_right_insert_bends_gate;
            public NativeRightGeometrySnapshot native_right_geometry;
            public NativeRightFeatureSnapshot native_right_features;
            public bool native_right_geometry_gate;
            public bool native_right_feature_gate;
            public int native_right_external_reference_count = -1;
            public NativeRightGeometrySnapshot native_right_reopen_geometry;
            public NativeRightFeatureSnapshot native_right_reopen_features;
            public bool native_right_reopen_geometry_gate;
            public bool native_right_reopen_feature_gate;
            public int native_right_reopen_external_reference_count = -1;
            public bool native_frame_crossbar_route;
            public string native_frame_crossbar_route_id = "";
            public bool native_frame_crossbar_evidence_inputs_gate;
            public bool native_frame_crossbar_legacy_mirror_precondition;
            public bool native_frame_crossbar_backup_precondition;
            public bool native_frame_crossbar_replaced;
            public NativeRightGeometrySnapshot native_frame_crossbar_geometry;
            public NativeRightFeatureSnapshot native_frame_crossbar_features;
            public bool native_frame_crossbar_geometry_gate;
            public bool native_frame_crossbar_feature_gate;
            public int native_frame_crossbar_external_reference_count = -1;
            public NativeRightGeometrySnapshot native_frame_crossbar_reopen_geometry;
            public NativeRightFeatureSnapshot native_frame_crossbar_reopen_features;
            public bool native_frame_crossbar_reopen_geometry_gate;
            public bool native_frame_crossbar_reopen_feature_gate;
            public int native_frame_crossbar_reopen_external_reference_count = -1;
            public bool native_door_frame_right_route;
            public string native_door_frame_right_route_id = "";
            public bool native_door_frame_right_evidence_inputs_gate;
            public bool native_door_frame_right_legacy_mirror_precondition;
            public bool native_door_frame_right_backup_precondition;
            public bool native_door_frame_right_replaced;
            public NativeRightGeometrySnapshot native_door_frame_right_geometry;
            public NativeRightFeatureSnapshot native_door_frame_right_features;
            public bool native_door_frame_right_geometry_gate;
            public bool native_door_frame_right_feature_gate;
            public int native_door_frame_right_external_reference_count = -1;
            public NativeRightGeometrySnapshot native_door_frame_right_reopen_geometry;
            public NativeRightFeatureSnapshot native_door_frame_right_reopen_features;
            public bool native_door_frame_right_reopen_geometry_gate;
            public bool native_door_frame_right_reopen_feature_gate;
            public int native_door_frame_right_reopen_external_reference_count = -1;
            public bool native_cabinet_shelf_right_route;
            public string native_cabinet_shelf_right_route_id = "";
            public bool native_cabinet_shelf_right_evidence_inputs_gate;
            public bool native_cabinet_shelf_right_legacy_mirror_precondition;
            public bool native_cabinet_shelf_right_backup_precondition;
            public bool native_cabinet_shelf_right_replaced;
            public NativeRightGeometrySnapshot native_cabinet_shelf_right_geometry;
            public NativeRightFeatureSnapshot native_cabinet_shelf_right_features;
            public bool native_cabinet_shelf_right_geometry_gate;
            public bool native_cabinet_shelf_right_feature_gate;
            public int native_cabinet_shelf_right_external_reference_count = -1;
            public NativeRightGeometrySnapshot native_cabinet_shelf_right_reopen_geometry;
            public NativeRightFeatureSnapshot native_cabinet_shelf_right_reopen_features;
            public bool native_cabinet_shelf_right_reopen_geometry_gate;
            public bool native_cabinet_shelf_right_reopen_feature_gate;
            public int native_cabinet_shelf_right_reopen_external_reference_count = -1;
            public int source_open_errors;
            public int source_open_warnings;
            public bool opened;
            public int open_errors;
            public int open_warnings;
            public bool initial_open_warning_is_expected_needs_regen;
            public PartSnapshot before;
            public bool bbox_before_source_or_target;
            public LinkSnapshot links_before;
            public bool unlock_attempted;
            public bool unlock_succeeded;
            public LinkUpdateBatch unlock_updates;
            public LinkSnapshot links_after_unlock;
            public bool update_attempted;
            public bool update_succeeded;
            public LinkUpdateBatch update_none_updates;
            public LinkSnapshot links_after_update_none;
            public bool rebuilt;
            public bool relock_attempted;
            public bool relock_succeeded;
            public LinkUpdateBatch relock_updates;
            public LinkSnapshot links_after_relock;
            public bool rebuilt_after_relock;
            public PartSnapshot after;
            public bool bbox_after_target;
            public LinkSnapshot links_after;
            public string sha_after_save = "";
            public int reopen_open_errors;
            public int reopen_open_warnings;
            public bool reopen_rebuilt;
            public PartSnapshot reopen;
            public bool bbox_reopen_target;
            public LinkSnapshot links_reopen;
            public bool edit_gate;
            public bool verify_gate;
        }

        public sealed class LinkSnapshot
        {
            public bool traversal_complete;
            public string expected_source_path = "";
            public string expected_primary_feature_name = "";
            public int expected_reference_count;
            public int expected_reference_status;
            public int primary_feature_count;
            public int matched_feature_count;
            public int capture_success_count;
            public int total_reference_count;
            public bool has_exact_expected_source;
            public bool all_paths_local;
            public bool exact_contract_match;
            public int broken_or_dangling_count;
            public int locked_count;
            public int nonlocked_count;
            public bool all_primary_locked;
            public readonly List<string> nonlocal_paths = new List<string>();
            public readonly List<PrimaryLinkFeature> primary_features = new List<PrimaryLinkFeature>();
        }

        public sealed class PrimaryLinkFeature
        {
            public string name = "";
            public string type = "";
            public bool capture_succeeded;
            public int config_option;
            public string config_name = "";
            public string error = "";
            public readonly List<ReferenceRow> references = new List<ReferenceRow>();
        }

        public sealed class ReferenceRow
        {
            public string model_path = "";
            public string component_path = "";
            public string feature = "";
            public int status;
        }

        public sealed class LinkUpdateBatch
        {
            public string stage = "";
            public int action;
            public int duplicate_feature_name_count;
            public bool traversal_complete;
            public bool success;
            public readonly List<LinkUpdateRow> rows = new List<LinkUpdateRow>();
        }

        public sealed class LinkUpdateRow
        {
            public string feature_name = "";
            public string feature_type = "";
            public int config_option;
            public string config_name = "";
            public bool attempted;
            public bool call_succeeded;
            public int[] statuses_before = new int[0];
            public int[] statuses_after = new int[0];
            public string status = "";
        }

        public sealed class AssemblyRecord : SaveRecord
        {
            public string edit_pass = "";
            public string file_name = "";
            public string path = "";
            public bool opened;
            public int open_errors;
            public int open_warnings;
            public int expected_stable_open_warnings;
            public bool initial_open_warning_is_expected_needs_regen;
            public bool changed;
            public bool idempotent_already_target;
            public bool rebuilt;
            public AssemblySnapshot after;
            public bool after_rebuild_transform_gate;
            public bool save_flag_after_rebuild;
            public bool save_not_required_idempotent;
            public string sha_after_save = "";
            public double non_target_transform_tolerance = 1e-10;
            public int non_target_before_count;
            public string non_target_before_digest = "";
            public int non_target_after_rebuild_count;
            public string non_target_after_rebuild_digest = "";
            public bool non_target_after_rebuild_unchanged;
            public int non_target_reopen_count;
            public string non_target_reopen_digest = "";
            public bool non_target_reopen_unchanged;
            public int left_shelf_root_instance_count;
            public int right_shelf_root_instance_count;
            public bool four_shelf_root_instances;
            public int reopen_open_errors;
            public int reopen_open_warnings;
            public bool reopen_rebuilt;
            public bool reopen_save_flag_after_rebuild;
            public AssemblySnapshot reopen;
            public bool reopen_transform_gate;
            public int reopen_left_shelf_root_instance_count;
            public int reopen_right_shelf_root_instance_count;
            public bool edit_gate;
            public bool verify_gate;
            public readonly List<TransformRow> transforms = new List<TransformRow>();
            internal readonly List<ComponentState> non_target_baseline = new List<ComponentState>();
        }

        public sealed class TransformRow
        {
            public string component_name = "";
            public string component_file_name = "";
            public string component_path = "";
            public int match_count;
            public bool unique_match;
            public double source_x_mm;
            public double target_x_mm;
            public bool explicit_z_target;
            public double source_z_mm;
            public double target_z_mm;
            public double before_x_mm;
            public double before_y_mm;
            public double before_z_mm;
            public double[] before_transform;
            public bool precondition_source_or_target;
            public bool was_fixed;
            public bool was_suppressed;
            public bool selected_for_unfix;
            public bool unfix_action_succeeded;
            public bool unfixed;
            public bool transform_created;
            public bool transform_applied;
            public bool selected_for_refix;
            public bool refix_action_succeeded;
            public bool changed;
            public bool idempotent_already_target;
            public double after_x_mm;
            public double after_y_mm;
            public double after_z_mm;
            public double[] after_transform;
            public bool target_readback;
            public bool target_z_readback;
            public bool rotation_y_z_preserved;
            public bool is_fixed_after;
            public bool is_suppressed_after;
            public bool fixed_state_preserved;
            public bool suppressed_state_preserved;
            public int after_rebuild_match_count;
            public double[] after_rebuild_transform;
            public double after_rebuild_x_mm;
            public double after_rebuild_z_mm;
            public bool after_rebuild_target_z_readback;
            public bool after_rebuild_is_fixed;
            public bool after_rebuild_is_suppressed;
            public bool after_rebuild_target_readback;
            public bool after_rebuild_rotation_y_z_preserved;
            public bool after_rebuild_fixed_suppressed_preserved;
            public bool after_rebuild_gate;
            public int reopen_match_count;
            public double[] reopen_transform;
            public double reopen_x_mm;
            public double reopen_z_mm;
            public bool reopen_target_z_readback;
            public bool reopen_is_fixed;
            public bool reopen_is_suppressed;
            public bool reopen_target_readback;
            public bool reopen_rotation_y_z_preserved;
            public bool reopen_fixed_suppressed_preserved;
            public bool reopen_gate;
        }

        public sealed class AssemblySnapshot
        {
            public bool is_root;
            public bool traversal_complete;
            public int feature_count;
            public int top_level_component_count;
            public readonly List<FeatureIssue> issues = new List<FeatureIssue>();
        }

        public sealed class FeatureIssue
        {
            public int depth;
            public string name = "";
            public string type = "";
            public int error_code;
            public int error_code2;
            public bool warning;
        }
    }
}
