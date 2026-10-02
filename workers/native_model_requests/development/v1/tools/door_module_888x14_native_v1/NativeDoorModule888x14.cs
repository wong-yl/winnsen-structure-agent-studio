using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.NativeDoorModule888x14
{
    internal static class NativeDoorModule888x14
    {
        internal const string RequestSchema = "winnsen.16029.native_door_module_request.v1";
        internal const string SentinelSchema = "winnsen.16029.native_isolated_root.v1";
        internal const string PanelProofSchema = "winnsen.16029.native_panel_proof.v1";
        internal const string PanelProofAuditSchema =
            "winnsen.16029.native_panel_proof_audit.v1";
        internal const string PanelProofRelativePath =
            "evidence/left-panel-proof.v1.json";
        internal const string PanelProofAuditRelativePath =
            "evidence/left-panel-readonly-audit.v1.json";
        internal const string RightStrategyMirrorPart = "solidworks_mirror_part";
        internal const double TargetCabinetWidthMm = 888.0;
        internal const double TargetDoorWidthMm = 381.0;
        internal const double TargetDoorHeightMm = 1781.0 / 7.0;
        internal const double TargetStiffenerLengthMm = TargetDoorHeightMm - 10.5;
        internal const double DimensionToleranceMm = 0.01;
        internal const double BboxToleranceMm = 0.08;
        internal const string ExpectedRecipeId = "winnsen-16029-888w-14door-native-v1";
        internal const string ExpectedRecipeDigest =
            "f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8";
        internal const string AttemptRootEnvironmentVariable =
            "WINNSEN_NATIVE_16029_ATTEMPTS_ROOT";
        internal const string PlanShaEnvironmentVariable =
            "WINNSEN_NATIVE_16029_PLAN_SHA256";
        internal const string ExecutionAuthorizationPathEnvironmentVariable =
            "WINNSEN_NATIVE_EXECUTION_AUTH_PATH";
        internal const string ExecutionAuthorizationShaEnvironmentVariable =
            "WINNSEN_NATIVE_EXECUTION_AUTH_SHA256";
        internal const string ExecutionAuthorizationSchema =
            "winnsen.native_execution_authorization.v1";
        internal const string ExecutionAuthorizationRelativePath =
            "execution_authorizations\\native_door_module_888x14_v1.json";
        internal const string DoorModulePhase = "native_door_module_888x14_v1";
        internal const string DoorModuleExecutionPhase = "door_module_888x14";
        internal const string ExecutionAuthorizationPurpose = "structure_engineering_assistance";
        internal const string FlatImportManifestFileName =
            "door_module_flat_import_manifest.json";
        internal const string RootAssemblerImportBoundary =
            "ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK";
        internal const string V37CanonicalInventoryDigest =
            "9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B";
        internal const string DoorReceiptSchema =
            "winnsen.16029.native_door_module_receipt.v1";
        internal const string DoorEvidenceRelativePath =
            "evidence/door_module_888x14.result.v1.json";
        internal const string DoorReceiptRelativePath =
            "receipts/door_module_888x14.json";
        internal const string ToolchainManifestSchema =
            "winnsen.16029.native_toolchain_manifest.v1";
        internal const string LockToolchainManifestSchema =
            "winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1";
        internal const string LockToolchainManifestGeneratedBy =
            "VerifyLockTopology888x14Static.mjs";

        private static readonly string[] PanelProofKeys =
        {
            "schema", "handedness", "construction", "task_id", "attempt",
            "authorization_id", "authorization_sha256",
            "tool_source_normalized_sha256", "tool_executable_sha256", "source_path",
            "source_sha256", "reopen_sha256", "solidworks_major", "body_count",
            "has_sheet_metal", "has_flat_pattern", "error_feature_count",
            "warning_feature_count", "foreign_feature_count", "traversal_complete",
            "external_reference_count", "read_only_reopen_verified", "height_dimension",
            "width_dimension", "height_dimension_state", "width_dimension_state",
            "generated_at_utc", "evidence_files", "evidence_commitment_sha256"
        };

        private static readonly string[] PanelProofAuditKeys =
        {
            "schema", "taskId", "attempt", "authorizationId", "authorizationSha256",
            "toolSourceNormalizedSha256", "toolExecutableSha256", "sourcePath",
            "sourceSha256Before", "sourceSha256After", "sourceUnchanged",
            "solidworksMajor", "solidworksRevision", "solidworksExecutablePath",
            "ownedProcessId", "openErrors", "openWarnings", "openedReadOnly",
            "bodyCount", "featureCount", "hasSheetMetal", "hasFlatPattern",
            "mirrorPartFeatureCount", "foreignFeatureCount", "errorFeatureCount",
            "warningFeatureCount", "traversalComplete", "externalReferenceCount",
            "heightDimension", "widthDimension", "heightDimensionState",
            "widthDimensionState", "completedAt", "evidenceCommitmentSha256"
        };

        private static readonly string[] RuntimeReceiptKeys =
        {
            "schema", "phase", "success", "completedAt", "taskId", "taskRevision",
            "taskDigest", "requestDigest", "leaseId", "leaseExpiresAt", "attempt",
            "planSha256", "authorizationId", "authorizationSha256",
            "authorizationJsonBase64", "authorizationIssuedAt", "authorizationExpiresAt",
            "recipeId", "recipeDigest", "toolId", "toolSourceNormalizedSha256",
            "toolExecutableSha256", "evidencePath", "evidenceSha256",
            "evidenceCommitmentSha256", "preInventoryDigest", "postInventoryDigest",
            "predecessorReceiptSha256"
        };

        private static readonly string[] SeedReceiptKeys =
        {
            "schema", "phase", "success", "completedAt", "taskId", "taskRevision",
            "taskDigest", "requestDigest", "leaseId", "leaseExpiresAt", "attempt",
            "planSha256", "authorizationId", "authorizationSha256",
            "authorizationJsonBase64", "authorizationIssuedAt", "authorizationExpiresAt",
            "recipeId", "recipeDigest", "toolId", "toolSourceNormalizedSha256",
            "toolExecutableSha256", "evidencePath", "evidenceSha256",
            "evidenceCommitmentSha256", "sourceInventoryDigest", "targetInventoryDigest",
            "nonRootFileCount", "nonRootExactSource", "rootFileName", "rootShaBefore",
            "rootShaAfterStable", "initialOpenErrors", "initialOpenWarnings",
            "stabilizeSaveErrors", "stabilizeSaveWarnings", "reopenErrors", "reopenWarnings",
            "dependencyClosureCount", "dependenciesAllTargetLocal", "knownRootIssueGate",
            "predecessorReceiptSha256"
        };

        private static readonly string[] StageReceiptOrder =
        {
            "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14",
            "final_pack_and_relocated_reopen"
        };

        private static readonly string[] StageReceiptSchemas =
        {
            "winnsen.16029.native_seed_pack_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            DoorReceiptSchema,
            "winnsen.16029.native_lock_topology_receipt.v1",
            "winnsen.16029.native_root_assembly_receipt.v1",
            "winnsen.16029.native_final_pack_receipt.v1"
        };

        private static readonly string[] StageReceiptPaths =
        {
            "receipts/clone_native_seed.json", "evidence/width-dimensions.receipt.json",
            "evidence/width-derived.receipt.json", "evidence/width-base-hole.receipt.json",
            "evidence/width-assemblies.receipt.json", DoorReceiptRelativePath,
            "receipts/lock_topology_888x14.json", "receipts/root_assembly_888x14.json",
            "receipts/final_pack_and_relocated_reopen.json"
        };

        private static readonly string[] StageReceiptTools =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1", "native_width_888_v1",
            "native_width_888_v1", "native_width_888_v1", DoorModulePhase,
            "native_lock_topology_888x14_v1", "native_root_assembly_888x14_v1",
            "native_final_pack_888x14_v1"
        };

        private static readonly string[] StageReceiptPredecessors =
        {
            "", "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14"
        };

        private static readonly string[] TrustedToolchainManifestKeys =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1",
            "native_door_module_888x14_v1", "native_lock_topology_888x14_v1",
            "native_root_assembly_888x14_v1", "native_final_pack_888x14_v1"
        };

        private static readonly string[] LockToolchainIds =
        {
            "native_lock_topology_888x14_v1", "native_lock_topology_inspector_v1",
            "native_assembly_tongue_inspector_888x14_v1"
        };

        private static readonly string[] EvidenceCommitmentExcludedKeys =
        {
            "evidence_write_attempted", "evidence_write_succeeded",
            "evidence_finalize_write_succeeded", "evidence_finalize_error",
            "failure_evidence_write_succeeded", "phase_receipt_path",
            "phase_receipt_sha256", "phase_receipt_committed", "authorization_checkpoints",
            "completed_at_utc", "commit_completed_at_utc", "evidence_commitment_sha256",
            "backup_deleted", "backup_delete_error"
        };

        private static readonly Dictionary<string, string> TrustedSourceHashes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "left_panel_seed", "F34138A9BE009D5A0C68604F57B278584DB686623943C2DE319A2999ACA488A2" },
                { "stiffener_seed", "F4D4973F72CB604BF52BF802BC378E9BA042A4FE8034C196CA7666A964ACCB28" },
                { "latch_plate", "4B511C2FB2371060D998F2C0667C12980DF4A151E5CF4FDCD2934F7C7AAC1C5C" },
                { "hook_pad", "93378BDFB9666CDE8379F59A0506E7A4508AFAEDAC47121C3B8CBDAE56480834" },
                { "bushing", "930EE6070D278D5299D1EECED4F43367EE404A0E921517DE45297C30A8A12C7B" },
                { "hinge_pin", "B7289A146C9827050573FACEF66DCA06919FC41B943C39548AF536FE21516BC0" },
                { "circlip", "84ACBBAD72BC3DB175AD77C5DB5ED5F4A74410435C3CDEF1574B6A7808DCC754" },
                { "mechanical_lock_tongue", "26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719" }
            };

        private static readonly HashSet<string> V37WorkingPackFileNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "U型锁钩垫板.SLDPRT", "标准寄存柜 模型.SLDPRT",
                "标准寄存柜1917×760×550(总装配).SLDASM", "插销固定板.SLDPRT",
                "插销固定板2╱12_右.SLDPRT", "插销固定板2╱12_左.SLDPRT",
                "插销固定板4╱12_右.SLDPRT", "插销固定板4╱12_左.SLDPRT",
                "插销固定板6╱12_右.SLDPRT", "插销固定板6╱12_左.SLDPRT",
                "储物柜门2╱12焊接_右.SLDASM", "储物柜门2╱12焊接_左.SLDASM",
                "储物柜门4╱12焊接_右.SLDASM", "储物柜门4╱12焊接_左.SLDASM",
                "储物柜门6╱12焊接_右.SLDASM", "储物柜门6╱12焊接_左.SLDASM",
                "储物柜门板2╱12_W317.SLDPRT", "储物柜门板2╱12_右.SLDPRT",
                "储物柜门板4╱12_W317.SLDPRT", "储物柜门板4╱12_右.SLDPRT",
                "储物柜门板6╱12_W317.SLDPRT", "储物柜门板6╱12_右.SLDPRT",
                "储物柜门装配_L2.SLDASM", "储物柜门装配_L4.SLDASM",
                "储物柜门装配_L6.SLDASM", "储物柜门装配_R2.SLDASM",
                "储物柜门装配_R4.SLDASM", "储物柜门装配_R6.SLDASM",
                "底座 模型.sldprt", "底座底板.sldprt", "底座焊接.SLDASM",
                "底座加强筋.sldprt", "底座外框.sldprt",
                "调整脚 M12X60(模型).SLDPRT", "柜门加强筋2╱12.SLDPRT",
                "柜门加强筋4╱12.SLDPRT", "柜门加强筋6╱12.SLDPRT",
                "开口挡圈5.SLDPRT", "螺母M12.SLDPRT", "门框 横隔板.sldprt",
                "门框 横隔板R.SLDPRT", "门框 上.sldprt", "门框 竖隔板L.sldprt",
                "门框 竖隔板R.SLDPRT", "门框 下.sldprt", "门框 右.sldprt",
                "门框 左.sldprt", "门框焊接.sldasm", "门轴销.SLDPRT",
                "上盖 模型.sldprt", "上盖焊接.SLDASM", "上盖壳体底板.sldprt",
                "上盖壳体后侧板.sldprt", "上盖壳体前侧板.sldprt",
                "上盖壳体右侧板.SLDPRT", "上盖壳体左侧板.sldprt",
                "塑料轴套(云绅模具).SLDPRT", "锁控维护条_源钣金.SLDPRT",
                "锁舌.SLDPRT", "箱体侧板加强筋1.sldprt", "箱体侧板加强筋2.sldprt",
                "箱体横层板L.sldprt", "箱体横层板L焊接.SLDASM",
                "箱体横层板R.SLDPRT", "箱体横层板R焊接.SLDASM",
                "箱体横层板加强筋.SLDPRT", "箱体竖隔板L.sldprt",
                "箱体竖隔板L焊接.SLDASM", "箱体竖隔板R.SLDPRT",
                "箱体竖隔板R焊接.SLDASM", "箱体竖隔板加强件.sldprt",
                "箱体右侧板.sldprt", "箱体右侧板焊接.SLDASM",
                "箱体左侧板.sldprt", "箱体左侧板焊接.sldasm"
            };

        private static readonly HashSet<string> DeniedRightPanelHashes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "735F6DABE58153F7023F0327AF4C23C98DECEB62BAE55F13361D3BAF7FA23139",
                "9912C4FC006DCEEB84758A332ACF36EFC5A78EF7978922A6AE303121736902F2",
                "C2C2456362719C57B1FCBBA599DA0CE5B41ADAF7B1BD1C2CBDA01A15B5C48783"
            };

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && string.Equals(args[0], "--receipt-self-test",
                    StringComparison.OrdinalIgnoreCase))
                return RunReceiptSelfTest();
            if (args.Length == 1 && string.Equals(args[0], "--toolchain-self-test",
                    StringComparison.OrdinalIgnoreCase))
                return RunToolchainSelfTest();
            if (args.Length == 1 && string.Equals(args[0], "--toolchain-live-check",
                    StringComparison.OrdinalIgnoreCase))
                return RunToolchainLiveCheck();
            if (args.Length == 1 && string.Equals(args[0],
                    "--panel-proof-contract-self-test", StringComparison.OrdinalIgnoreCase))
                return RunPanelProofContractSelfTest();
            if (args.Any(delegate(string value)
            {
                return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
            }))
            {
                PrintUsage();
                return 0;
            }

            string mode = Argument(args, "--mode");
            string requestPath = Argument(args, "--request");
            string outPath = Argument(args, "--out");
            string confirmation = Argument(args, "--confirm-task");
            bool panelProofMode = args.Any(delegate(string value)
            { return string.Equals(value, "--panel-proof", StringComparison.OrdinalIgnoreCase); });
            var result = new RunResult
            {
                schema = "winnsen.16029.native_door_module_result.v1",
                generated_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                mode = mode,
                request_path = FullPathOrEmpty(requestPath),
                evidence_path = FullPathOrEmpty(outPath),
                target_cabinet_width_mm = TargetCabinetWidthMm,
                target_width_mm = TargetDoorWidthMm,
                target_height_mm = TargetDoorHeightMm,
                target_stiffener_length_mm = TargetStiffenerLengthMm,
                mechanical_lock_tongue_boundary =
                    "EXTERNAL_PREREQUISITE_FOR_ROOT_ASSEMBLY_TOPOLOGY_STAGE; " +
                    "this door module verifies only isolated SLDPRT path, SHA-256, and exactly " +
                    "one placement per ordinary door",
                status = "NO_GO"
            };
            if (panelProofMode) result.mode = "panel-proof";

            DoorRequest request = null;
            string lockPath = "";
            FileStream transactionLock = null;
            bool transactionLockOwned = false;
            try
            {
                Require(panelProofMode || mode == "preflight" || mode == "execute",
                    "INVALID_MODE", 2,
                    "use --panel-proof or --mode preflight|execute");
                Require(!string.IsNullOrWhiteSpace(requestPath) && File.Exists(requestPath),
                    "REQUEST_NOT_FOUND", 2, "--request must be an existing JSON file");
                request = ReadJson<DoorRequest>(requestPath);
                result.task_id = request == null ? "" : request.task_id;
                result.worker_id = request == null ? "" : request.worker_id;
                result.lease_id = request == null ? "" : request.lease_id;

                if (panelProofMode)
                    return RunPanelProof(request, requestPath, outPath, confirmation, result);

                Preflight(request, requestPath, outPath, result);
                result.preflight_passed = true;

                if (mode == "preflight")
                {
                    result.status = "PREFLIGHT_PASS";
                    result.execution_started = false;
                    result.note = "No SolidWorks session was started in preflight mode.";
                    WriteJsonAtomicNew(outPath, result);
                    return 0;
                }

                Require(string.Equals(confirmation, request.task_id, StringComparison.Ordinal),
                    "TASK_CONFIRMATION_MISMATCH", 3,
                    "--confirm-task must exactly match request.task_id");
                BindExecutionAuthorization(request, result);
                Require(result.plan_execution_authorized,
                    "IMMUTABLE_PLAN_NOT_EXECUTION_AUTHORIZED", 3,
                    "the frozen plan must remain planning-only and a current worker authorization is required");
                ValidatePanelProof(request.left_panel_proof, "left",
                    NormalizeHash(request.sources.left_panel_seed.sha256),
                    request.isolated_root, result, true);
                Require(ProcessIds("SLDWORKS").Count == 0 && ProcessIds("sldProcMon").Count == 0,
                    "CAD_BASELINE_NOT_EMPTY", 3,
                    "execute requires an empty SLDWORKS and sldProcMon process baseline");

                lockPath = Path.Combine(Path.GetFullPath(request.isolated_root),
                    ".native-door-module-888x14.lock");
                RequireSafeMutationPath(lockPath, request.isolated_root, "TRANSACTION_LOCK_PATH_UNSAFE");
                transactionLock = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None);
                transactionLockOwned = true;
                byte[] lockBytes = Encoding.UTF8.GetBytes(request.task_id + "\n" +
                    request.worker_id + "\n" + request.lease_id + "\n");
                transactionLock.Write(lockBytes, 0, lockBytes.Length);
                transactionLock.Flush(true);

                result.execution_started = true;
                Execute(request, result);
                transactionLock.Dispose();
                transactionLock = null;
                RequireSafeMutationPath(lockPath, request.isolated_root, "TRANSACTION_LOCK_PATH_UNSAFE");
                File.Delete(lockPath);
                Require(!File.Exists(lockPath), "TRANSACTION_LOCK_RELEASE_FAILED", 29,
                    "transaction lock still exists before evidence commit");
                transactionLockOwned = false;
                CommitExecutionEvidenceAndReceipt(outPath, request, result);
                return 0;
            }
            catch (GateException gate)
            {
                result.error_code = gate.Code;
                result.error = gate.Message;
                result.status = "NO_GO";
                result.success = false;
                result.completed = false;
                result.committed = false;
                CleanupUnpairedCommitArtifacts(request, result);
                RollbackUncommittedOutput(request, result);
                TryWriteResult(outPath, result, request);
                Console.Error.WriteLine(gate.Code + ": " + gate.Message);
                return gate.ExitCode;
            }
            catch (Exception ex)
            {
                result.error_code = "UNHANDLED_EXCEPTION";
                result.error = SafeExceptionText(ex);
                result.status = "NO_GO";
                result.success = false;
                result.completed = false;
                result.committed = false;
                CleanupUnpairedCommitArtifacts(request, result);
                RollbackUncommittedOutput(request, result);
                TryWriteResult(outPath, result, request);
                Console.Error.WriteLine(result.error);
                return 90;
            }
            finally
            {
                if (transactionLock != null) transactionLock.Dispose();
                if (transactionLockOwned && !string.IsNullOrWhiteSpace(lockPath) &&
                    File.Exists(lockPath))
                {
                    try
                    {
                        RequireSafeMutationPath(lockPath, request == null ? "" : request.isolated_root,
                            "TRANSACTION_LOCK_PATH_UNSAFE");
                        File.Delete(lockPath);
                    }
                    catch { }
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
                "NativeDoorModule888x14.exe --mode preflight|execute " +
                "--request <absolute-request.json> --out <absolute-result.json> " +
                "[--confirm-task <task-id>]");
            Console.WriteLine("NativeDoorModule888x14.exe --receipt-self-test");
            Console.WriteLine("NativeDoorModule888x14.exe --toolchain-self-test");
            Console.WriteLine("NativeDoorModule888x14.exe --toolchain-live-check");
            Console.WriteLine("NativeDoorModule888x14.exe --panel-proof --request " +
                "<absolute-request.json> --out <fixed-proof.json> --confirm-task <task-id>");
            Console.WriteLine("NativeDoorModule888x14.exe --panel-proof-contract-self-test");
            Console.WriteLine("preflight never starts SolidWorks; execute supports only the " +
                "SolidWorks MirrorPart2 native right-hand route and requires an empty CAD " +
                "process baseline.");
        }

        private static int RunReceiptSelfTest()
        {
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            string temp = Path.Combine(Path.GetTempPath(), "winnsen-door-receipt-self-test-" +
                Guid.NewGuid().ToString("N"));
            string error = "";
            try
            {
                Directory.CreateDirectory(Path.Combine(temp, "evidence"));
                Directory.CreateDirectory(Path.Combine(temp, "receipts"));
                var basicFixture = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "z", 7 },
                    { "nested", new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "beta", false },
                            { "alpha", new object[] { "L", 381,
                                new Dictionary<string, object>(StringComparer.Ordinal)
                                { { "y", 2 }, { "x", 1 } } } }
                        } },
                    { "phase", "clone_native_seed" },
                    { "completed_at_utc", "ignored" },
                    { "phase_receipt_sha256", "ignored" }
                };
                checks["shared_commitment_basic_fixture"] =
                    EvidenceCommitmentDigest(basicFixture) ==
                    "684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1";
                var numericFixture = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "a", 0.1592142857142857 }, { "b", -1.4150714285714288 },
                    { "c", 0.0000001 }, { "d", 0.000001 }, { "e", 1e20 },
                    { "f", 1e21 }, { "g", 254.4285714285714 },
                    { "ticks", 638906112000000000L },
                    { "html", "<structure>&engineering" }
                };
                checks["evidence_commitment_matches_shared_float_vector"] =
                    EvidenceCommitmentDigest(numericFixture) ==
                    "3BC67213D48D35E839E000955BD2B60A862C13A1EA0DBC0BE4ACBEAD12035C65";

                string evidencePath = Path.Combine(temp, "evidence", "width-assemblies.json");
                var evidence = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "schema", "self-test.width-assemblies-evidence.v1" },
                    { "success", true }, { "completed_at_utc", "2098-01-01T00:01:00.000Z" },
                    { "phase", "assemblies" }, { "value", 254.4285714285714 },
                    { "evidence_commitment_sha256", "" }
                };
                evidence["evidence_commitment_sha256"] = EvidenceCommitmentDigest(evidence);
                WriteJsonAtomicNew(evidencePath, evidence);
                string evidenceSha = Sha256(evidencePath);
                Dictionary<string, object> receipt = SelfTestRuntimeReceipt(evidenceSha,
                    ValueText(evidence["evidence_commitment_sha256"]));
                checks["exact_runtime_receipt_keys"] =
                    ExactKeys(receipt, RuntimeReceiptKeys) &&
                    !ExactKeys(new Dictionary<string, object>(receipt, StringComparer.Ordinal)
                        { { "forged", true } }, RuntimeReceiptKeys);
                Dictionary<string, object> pairedEvidence;
                checks["consumes_exact_width_assemblies_predecessor"] =
                    RuntimeReceiptEnvelopeValid(receipt,
                        "winnsen.native_width_888.phase_receipt.v1", "assemblies",
                        "native_width_888_v1") &&
                    EvidencePairValid(temp, receipt, "evidence/width-assemblies.json",
                        out pairedEvidence);

                byte[] originalEvidence = File.ReadAllBytes(evidencePath);
                File.AppendAllText(evidencePath, " ", new UTF8Encoding(false));
                checks["rejects_modified_evidence"] = !EvidencePairValid(temp, receipt,
                    "evidence/width-assemblies.json", out pairedEvidence);
                File.Delete(evidencePath);
                File.WriteAllBytes(evidencePath, originalEvidence);

                bool refusedEvidenceOnly = !SuccessPairPathsAvailable(evidencePath,
                    Path.Combine(temp, "receipts", "door_module_888x14.json"));
                string receiptPath = Path.Combine(temp, "receipts", "width-assemblies.json");
                WriteJsonAtomicNew(receiptPath, receipt);
                string receiptSha = Sha256(receiptPath);
                byte[] originalReceipt = File.ReadAllBytes(receiptPath);
                File.AppendAllText(receiptPath, " ", new UTF8Encoding(false));
                checks["rejects_modified_predecessor_receipt"] =
                    !ImmutableHashValid(receiptPath, receiptSha);
                File.Delete(receiptPath);
                File.WriteAllBytes(receiptPath, originalReceipt);

                string doorReceiptPath = Path.Combine(temp, "receipts",
                    "door_module_888x14.json");
                string evidenceHashBeforeReceipt = Sha256(evidencePath);
                WriteJsonAtomicNew(doorReceiptPath, receipt);
                checks["evidence_then_receipt_pair_has_no_hash_cycle"] =
                    !ContainsReceiptIdentity(ReadJsonObject(evidencePath)) &&
                    string.Equals(Sha256(evidencePath), evidenceHashBeforeReceipt,
                        StringComparison.OrdinalIgnoreCase) && File.Exists(doorReceiptPath);
                checks["refuses_unpaired_existing_evidence_or_receipt"] =
                    refusedEvidenceOnly && !SuccessPairPathsAvailable(
                        Path.Combine(temp, "evidence", "absent.json"), doorReceiptPath);

                string unownedEvidencePath = Path.Combine(temp, "evidence", "unowned.json");
                File.WriteAllText(unownedEvidencePath, "foreign\n", new UTF8Encoding(false));
                var unownedCleanupRequest = new DoorRequest { isolated_root = temp };
                var unownedCleanupResult = new RunResult
                {
                    evidence_path = unownedEvidencePath,
                    evidence_write_attempted = true
                };
                CleanupUnpairedCommitArtifacts(unownedCleanupRequest, unownedCleanupResult);
                checks["cleanup_refuses_unowned_existing_artifact"] =
                    File.Exists(unownedEvidencePath);

                string ownedEvidencePath = Path.Combine(temp, "evidence", "owned.json");
                string ownedReceiptPath = Path.Combine(temp, "receipts", "owned.json");
                File.WriteAllText(ownedEvidencePath, "owned evidence\n",
                    new UTF8Encoding(false));
                File.WriteAllText(ownedReceiptPath, "owned receipt\n",
                    new UTF8Encoding(false));
                var ownedCleanupResult = new RunResult
                {
                    evidence_path = ownedEvidencePath,
                    receipt_path = ownedReceiptPath,
                    evidence_artifact_owned = true,
                    receipt_artifact_owned = true
                };
                CleanupUnpairedCommitArtifacts(unownedCleanupRequest, ownedCleanupResult);
                checks["cleanup_removes_only_owned_unpaired_artifacts"] =
                    !File.Exists(ownedEvidencePath) && !File.Exists(ownedReceiptPath);

                checks["rejects_expired_authorization_before_commit"] =
                    !CompletionWithinAuthorization("2098-01-01T00:11:00.000Z",
                        "2098-01-01T00:00:00.000Z", "2098-01-01T00:10:00.000Z",
                        "2098-01-01T00:20:00.000Z");
                checks["exact_evidence_and_receipt_paths"] =
                    PathsEqual(Path.Combine(temp, DoorEvidenceRelativePath.Replace('/', '\\')),
                        Path.Combine(temp, "evidence", "door_module_888x14.result.v1.json")) &&
                    PathsEqual(Path.Combine(temp, DoorReceiptRelativePath.Replace('/', '\\')),
                        Path.Combine(temp, "receipts", "door_module_888x14.json"));

                string a = Path.Combine(temp, "a.SLDPRT");
                string b = Path.Combine(temp, "b.SLDASM");
                File.WriteAllBytes(a, new byte[] { 65 });
                File.WriteAllBytes(b, new byte[] { 66 });
                checks["source_inventory_digest_is_canonical_9e9c"] =
                    V37CanonicalInventoryDigest ==
                        "9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B" &&
                    CanonicalInventoryDigest(new[] { b, a }) ==
                        "7FFA3C2576F001DB334AF0506519747054E779BA60670CC80917B471522E76A3";

                string linkTarget = Path.Combine(temp, "hardlink-target.bin");
                string link = Path.Combine(temp, "hardlink-alias.bin");
                File.WriteAllBytes(linkTarget, new byte[] { 1, 2, 3 });
                bool hardLinkCreated = CreateHardLink(link, linkTarget, IntPtr.Zero);
                checks["safe_cleanup_refuses_reparse_or_hardlink"] = hardLinkCreated &&
                    !ArtifactAttributesSafe(File.GetAttributes(linkTarget),
                        GetHardLinkCount(linkTarget)) &&
                    !ArtifactAttributesSafe(FileAttributes.ReparsePoint, 1);
            }
            catch (Exception ex)
            {
                error = SafeExceptionText(ex);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
            bool cadStarted = ProcessIds("SLDWORKS").Count != 0 ||
                ProcessIds("sldProcMon").Count != 0;
            int failed = checks.Count(delegate(KeyValuePair<string, bool> row)
            { return !row.Value; });
            var output = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "status", string.IsNullOrEmpty(error) && failed == 0 && !cadStarted ?
                    "PASS" : "FAIL" },
                { "cadStarted", cadStarted }, { "checksFailed", failed },
                { "checks", checks }, { "error", error }
            };
            Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 }.Serialize(output));
            return string.IsNullOrEmpty(error) && failed == 0 && !cadStarted ? 0 : 1;
        }

        private static int RunToolchainSelfTest()
        {
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            string temp = Path.Combine(Path.GetTempPath(), "winnsen-door-toolchain-self-test-" +
                Guid.NewGuid().ToString("N"));
            string error = "";
            try
            {
                Directory.CreateDirectory(temp);
                var documents = new Dictionary<string, Dictionary<string, object>>(
                    StringComparer.Ordinal);
                foreach (string id in TrustedToolchainManifestKeys.Where(delegate(string value)
                    { return value != LockToolchainIds[0]; }))
                    documents[id] = SelfTestGenericToolchainManifest(temp, id);

                var lockArtifacts = new List<string>();
                string validatorPath;
                documents[LockToolchainIds[0]] = SelfTestLockToolchainManifest(temp,
                    lockArtifacts, out validatorPath);

                bool allAccepted = true;
                foreach (string id in TrustedToolchainManifestKeys)
                {
                    string sourceSha, executableSha;
                    try
                    {
                        ValidateToolchainManifestDocument(id, documents[id], temp,
                            out sourceSha, out executableSha);
                        allAccepted = allAccepted && IsSha256(sourceSha) &&
                            IsSha256(executableSha);
                    }
                    catch { allAccepted = false; }
                }
                checks["accepts_exact_five_generic_and_one_lock_composite"] = allAccepted;

                bool allLockArtifactsBound = lockArtifacts.Count == 6;
                foreach (string path in lockArtifacts)
                {
                    byte[] original = File.ReadAllBytes(path);
                    File.AppendAllText(path, "tampered", new UTF8Encoding(false));
                    allLockArtifactsBound = allLockArtifactsBound && RejectsToolchainDocument(
                        LockToolchainIds[0], documents[LockToolchainIds[0]], temp);
                    File.WriteAllBytes(path, original);
                }
                checks["lock_composite_binds_primary_and_two_inspectors"] =
                    allLockArtifactsBound;

                byte[] originalValidator = File.ReadAllBytes(validatorPath);
                File.AppendAllText(validatorPath, "tampered", new UTF8Encoding(false));
                checks["lock_composite_binds_live_validator"] = RejectsToolchainDocument(
                    LockToolchainIds[0], documents[LockToolchainIds[0]], temp);
                File.WriteAllBytes(validatorPath, originalValidator);

                Dictionary<string, object> missingTool = CloneJsonObject(
                    documents[LockToolchainIds[0]]);
                ChildMap(missingTool, "tools").Remove(LockToolchainIds[2]);
                checks["rejects_lock_manifest_missing_required_tool"] =
                    RejectsToolchainDocument(LockToolchainIds[0], missingTool, temp);

                Dictionary<string, object> extraTop = CloneJsonObject(
                    documents[LockToolchainIds[0]]);
                extraTop["unexpected"] = true;
                checks["rejects_lock_manifest_with_extra_top_level_key"] =
                    RejectsToolchainDocument(LockToolchainIds[0], extraTop, temp);

                Dictionary<string, object> extraTool = CloneJsonObject(
                    documents[LockToolchainIds[0]]);
                ChildMap(ChildMap(extraTool, "tools"), LockToolchainIds[1])["unexpected"] = true;
                checks["rejects_lock_manifest_with_extra_tool_key"] =
                    RejectsToolchainDocument(LockToolchainIds[0], extraTool, temp);

                string lockSourcePath = lockArtifacts[0];
                byte[] originalLockSource = File.ReadAllBytes(lockSourcePath);
                File.AppendAllText(lockSourcePath, "tampered", new UTF8Encoding(false));
                checks["rejects_lock_manifest_with_tampered_live_artifact"] =
                    RejectsToolchainDocument(LockToolchainIds[0],
                        documents[LockToolchainIds[0]], temp);
                File.WriteAllBytes(lockSourcePath, originalLockSource);

                Dictionary<string, object> genericLock = SelfTestGenericToolchainManifest(temp,
                    LockToolchainIds[0]);
                checks["rejects_generic_manifest_for_lock_tool"] = RejectsToolchainDocument(
                    LockToolchainIds[0], genericLock, temp);
                checks["rejects_composite_manifest_for_non_lock_tool"] =
                    RejectsToolchainDocument(DoorModulePhase,
                        documents[LockToolchainIds[0]], temp);

                string identityFixture =
                    "private  const string ExpectedSourceSha256 = \"" +
                    new string('A', 64) + "\";\r\n" +
                    "private const string ExpectedContractSnapshotSha256=\"" +
                    new string('B', 64) + "\";\r\n" +
                    "private const string Unrelated = \"" + new string('C', 64) + "\";\r\n";
                string normalizedFixture = NormalizeGenericIdentitySource(identityFixture);
                checks["generic_source_normalization_masks_only_identity_stamps"] =
                    normalizedFixture.Contains("ExpectedSourceSha256 = \"__SOURCE_SHA256__\"") &&
                    normalizedFixture.Contains(
                        "ExpectedContractSnapshotSha256=\"__CONTRACT_SHA256__\"") &&
                    normalizedFixture.Contains("Unrelated = \"" + new string('C', 64) + "\"");
                checks["generic_source_normalization_preserves_declaration_format"] =
                    normalizedFixture.StartsWith("private  const string",
                        StringComparison.Ordinal) && normalizedFixture.IndexOf('\r') < 0;
                string tamperedFixture = normalizedFixture.Replace("Unrelated =",
                    "UnrelatedChanged =");
                checks["generic_source_normalization_detects_nonstamp_tamper"] =
                    !string.Equals(Sha256Text(normalizedFixture), Sha256Text(tamperedFixture),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                error = SafeExceptionText(ex);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
            bool cadStarted = ProcessIds("SLDWORKS").Count != 0 ||
                ProcessIds("sldProcMon").Count != 0;
            int failed = checks.Count(delegate(KeyValuePair<string, bool> row)
            { return !row.Value; });
            var output = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "status", string.IsNullOrEmpty(error) && failed == 0 && !cadStarted ?
                    "PASS" : "FAIL" },
                { "cadStarted", cadStarted }, { "checksFailed", failed },
                { "checks", checks }, { "error", error }
            };
            Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 }.Serialize(output));
            return string.IsNullOrEmpty(error) && failed == 0 && !cadStarted ? 0 : 1;
        }

        private static int RunToolchainLiveCheck()
        {
            var hashes = new Dictionary<string, object>(StringComparer.Ordinal);
            var schemas = new Dictionary<string, object>(StringComparer.Ordinal);
            string error = "";
            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule.FileName;
                string repositoryRoot = FindRepositoryRoot(executablePath);
                Require(!string.IsNullOrWhiteSpace(repositoryRoot), "REPOSITORY_ROOT_NOT_FOUND", 11,
                    "repository root is required for live toolchain validation");
                Dictionary<string, string> paths = TrustedToolchainManifestRelativePaths();
                foreach (string id in TrustedToolchainManifestKeys)
                {
                    string path = Path.GetFullPath(Path.Combine(repositoryRoot,
                        paths[id].Replace('/', '\\')));
                    RequireSafeReadPath(path, repositoryRoot,
                        "TRUSTED_TOOLCHAIN_MANIFEST_PATH_UNSAFE");
                    Dictionary<string, object> document = ReadJsonObject(path);
                    string sourceSha, executableSha;
                    ValidateToolchainManifestDocument(id, document, repositoryRoot,
                        out sourceSha, out executableSha);
                    hashes[id] = Sha256(path);
                    schemas[id] = TextValue(document, "schema");
                    if (id == DoorModulePhase)
                    {
                        Require(string.Equals(sourceSha, NormalizedSourceSha256(),
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(executableSha, Sha256(executablePath),
                                StringComparison.OrdinalIgnoreCase),
                            "DOOR_TOOLCHAIN_MANIFEST_SELF_IDENTITY_MISMATCH", 11,
                            "door live manifest does not bind this source and executable");
                    }
                }
            }
            catch (Exception ex) { error = SafeExceptionText(ex); }
            bool cadStarted = ProcessIds("SLDWORKS").Count != 0 ||
                ProcessIds("sldProcMon").Count != 0;
            var output = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "status", string.IsNullOrEmpty(error) && hashes.Count == 6 && !cadStarted ?
                    "PASS" : "FAIL" },
                { "cadStarted", cadStarted }, { "manifestHashes", hashes },
                { "schemas", schemas }, { "error", error }
            };
            Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 }.Serialize(output));
            return string.IsNullOrEmpty(error) && hashes.Count == 6 && !cadStarted ? 0 : 1;
        }

        private static Dictionary<string, object> SelfTestGenericToolchainManifest(
            string repositoryRoot, string id)
        {
            string directory = Path.Combine(repositoryRoot, "generic", SafeLeaf(id));
            Directory.CreateDirectory(directory);
            string sourcePath = Path.Combine(directory, "source.cs");
            string executablePath = Path.Combine(directory, "tool.exe");
            string verifierPath = Path.Combine(directory, "verify.mjs");
            File.WriteAllText(sourcePath, "// " + id + "\r\nclass Fixture {}\r\n",
                new UTF8Encoding(false));
            File.WriteAllBytes(executablePath, Encoding.UTF8.GetBytes("exe:" + id));
            File.WriteAllText(verifierPath, "// verifier:" + id + "\n",
                new UTF8Encoding(false));
            string prefix = "generic/" + SafeLeaf(id) + "/";
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", ToolchainManifestSchema },
                { "generatedBy", "self-test" },
                { "tool", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", id }, { "sourcePath", prefix + "source.cs" },
                        { "sourceNormalizedSha256", NormalizedSourceSha256At(sourcePath) },
                        { "executablePath", prefix + "tool.exe" },
                        { "executableSha256", Sha256(executablePath) },
                        { "verifierPath", prefix + "verify.mjs" },
                        { "verifierSha256", Sha256(verifierPath) }
                    }
                }
            };
        }

        private static Dictionary<string, object> SelfTestLockToolchainManifest(
            string repositoryRoot, List<string> artifactPaths, out string validatorPath)
        {
            string directory = Path.Combine(repositoryRoot, "lock");
            Directory.CreateDirectory(directory);
            var tools = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string id in LockToolchainIds)
            {
                string sourcePath = Path.Combine(directory, id + ".cs");
                string executablePath = Path.Combine(directory, id + ".exe");
                File.WriteAllText(sourcePath, "// " + id + "\r\nclass Fixture {}\r\n",
                    new UTF8Encoding(false));
                File.WriteAllBytes(executablePath, Encoding.UTF8.GetBytes("exe:" + id));
                artifactPaths.Add(sourcePath);
                artifactPaths.Add(executablePath);
                tools[id] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "sourcePath", sourcePath },
                    { "sourceNormalizedSha256", NormalizedLockSourceSha256At(sourcePath) },
                    { "executablePath", executablePath },
                    { "executableSha256", Sha256(executablePath) }
                };
            }
            validatorPath = Path.Combine(directory, "ValidateLockTopology888x14.mjs");
            File.WriteAllText(validatorPath, "// lock validator\n", new UTF8Encoding(false));
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", LockToolchainManifestSchema },
                { "generatedBy", LockToolchainManifestGeneratedBy },
                { "tools", tools },
                { "validator", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "path", validatorPath }, { "sha256", Sha256(validatorPath) }
                    }
                }
            };
        }

        private static Dictionary<string, object> CloneJsonObject(
            Dictionary<string, object> value)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 };
            return serializer.DeserializeObject(serializer.Serialize(value)) as
                Dictionary<string, object>;
        }

        private static bool RejectsToolchainDocument(string id,
            Dictionary<string, object> document, string repositoryRoot)
        {
            try
            {
                string sourceSha, executableSha;
                ValidateToolchainManifestDocument(id, document, repositoryRoot,
                    out sourceSha, out executableSha);
                return false;
            }
            catch (GateException) { return true; }
            catch { return false; }
        }

        private static int RunPanelProofContractSelfTest()
        {
            var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            string temp = Path.Combine(Path.GetTempPath(), "winnsen-door-panel-proof-self-test-" +
                Guid.NewGuid().ToString("N"));
            string error = "";
            try
            {
                Directory.CreateDirectory(Path.Combine(temp, "evidence"));
                string hash = new string('A', 64);
                string proofPath = Path.Combine(temp,
                    PanelProofRelativePath.Replace('/', '\\'));
                string auditPath = Path.Combine(temp,
                    PanelProofAuditRelativePath.Replace('/', '\\'));
                checks["exact_fixed_proof_and_audit_paths"] =
                    PathsEqual(proofPath, Path.Combine(temp, "evidence",
                        "left-panel-proof.v1.json")) &&
                    PathsEqual(auditPath, Path.Combine(temp, "evidence",
                        "left-panel-readonly-audit.v1.json"));

                var audit = SelfTestPanelProofAudit(temp, hash);
                audit["evidenceCommitmentSha256"] = PanelArtifactCommitment(audit);
                WriteJsonAtomicNew(auditPath, audit);
                var proof = SelfTestPanelProof(temp, hash, auditPath);
                proof["evidence_commitment_sha256"] = PanelArtifactCommitment(proof);
                WriteJsonAtomicNew(proofPath, proof);

                checks["exact_proof_keys_and_commitment"] = ExactKeys(proof, PanelProofKeys) &&
                    string.Equals(TextValue(proof, "evidence_commitment_sha256"),
                        PanelArtifactCommitment(proof), StringComparison.OrdinalIgnoreCase);
                checks["exact_audit_keys_and_commitment"] = ExactKeys(audit,
                        PanelProofAuditKeys) &&
                    string.Equals(TextValue(audit, "evidenceCommitmentSha256"),
                        PanelArtifactCommitment(audit), StringComparison.OrdinalIgnoreCase);
                checks["proof_binds_task_attempt_tool_authorization_and_source"] =
                    PanelProofDocumentValid(proof, temp, "self-test-task", 1,
                        "native-auth-" + new string('b', 32), hash, hash, hash, hash);

                Dictionary<string, object> extra = CloneJsonObject(proof);
                extra["unexpected"] = true;
                checks["rejects_extra_proof_key"] = !PanelProofDocumentValid(extra, temp,
                    "self-test-task", 1, "native-auth-" + new string('b', 32), hash,
                    hash, hash, hash);

                Dictionary<string, object> tamperedProof = CloneJsonObject(proof);
                tamperedProof["body_count"] = 2;
                checks["rejects_tampered_proof_commitment"] = !PanelProofDocumentValid(
                    tamperedProof, temp, "self-test-task", 1,
                    "native-auth-" + new string('b', 32), hash, hash, hash, hash);

                byte[] auditBytes = File.ReadAllBytes(auditPath);
                File.AppendAllText(auditPath, " ", new UTF8Encoding(false));
                checks["rejects_tampered_audit_bytes"] = !PanelProofDocumentValid(proof, temp,
                    "self-test-task", 1, "native-auth-" + new string('b', 32), hash,
                    hash, hash, hash);
                File.WriteAllBytes(auditPath, auditBytes);

                checks["rejects_wrong_authorization_binding"] = !PanelProofDocumentValid(proof,
                    temp, "self-test-task", 1, "native-auth-" + new string('c', 32), hash,
                    hash, hash, hash);
                checks["rejects_wrong_source_hash"] = !PanelProofDocumentValid(proof, temp,
                    "self-test-task", 1, "native-auth-" + new string('b', 32), hash,
                    new string('D', 64), hash, hash);
                checks["rejects_expired_same_authorization"] =
                    !CompletionWithinAuthorization("2098-01-01T00:31:00.000Z",
                        "2098-01-01T00:00:00.000Z", "2098-01-01T00:30:00.000Z",
                        "2098-01-01T00:30:00.000Z");
                checks["panel_proof_does_not_create_stage_receipt"] =
                    !File.Exists(Path.Combine(temp, DoorReceiptRelativePath.Replace('/', '\\')));
            }
            catch (Exception ex) { error = SafeExceptionText(ex); }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
            bool cadStarted = ProcessIds("SLDWORKS").Count != 0 ||
                ProcessIds("sldProcMon").Count != 0;
            int failed = checks.Count(delegate(KeyValuePair<string, bool> row)
            { return !row.Value; });
            var output = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "status", string.IsNullOrEmpty(error) && failed == 0 && !cadStarted ?
                    "PASS" : "FAIL" },
                { "cadStarted", cadStarted }, { "checksFailed", failed },
                { "checks", checks }, { "error", error }
            };
            Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 }.Serialize(output));
            return string.IsNullOrEmpty(error) && failed == 0 && !cadStarted ? 0 : 1;
        }

        private static Dictionary<string, object> SelfTestPanelProofAudit(string root,
            string hash)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", PanelProofAuditSchema }, { "taskId", "self-test-task" },
                { "attempt", 1 }, { "authorizationId", "native-auth-" +
                    new string('b', 32) }, { "authorizationSha256", hash },
                { "toolSourceNormalizedSha256", hash },
                { "toolExecutableSha256", hash },
                { "sourcePath", Path.Combine(root, "native_cad", "working_pack", "left.SLDPRT") },
                { "sourceSha256Before", hash }, { "sourceSha256After", hash },
                { "sourceUnchanged", true }, { "solidworksMajor", 28 },
                { "solidworksRevision", "28.5.0" },
                { "solidworksExecutablePath", @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe" },
                { "ownedProcessId", 100 }, { "openErrors", 0 }, { "openWarnings", 0 },
                { "openedReadOnly", true }, { "bodyCount", 1 }, { "featureCount", 20 },
                { "hasSheetMetal", true }, { "hasFlatPattern", true },
                { "mirrorPartFeatureCount", 0 }, { "foreignFeatureCount", 0 },
                { "errorFeatureCount", 0 }, { "warningFeatureCount", 0 },
                { "traversalComplete", true }, { "externalReferenceCount", 0 },
                { "heightDimension", "D1@草图1" }, { "widthDimension", "D2@草图1" },
                { "heightDimensionState", 2 }, { "widthDimensionState", 2 },
                { "completedAt", "2098-01-01T00:01:00.000Z" },
                { "evidenceCommitmentSha256", "" }
            };
        }

        private static Dictionary<string, object> SelfTestPanelProof(string root, string hash,
            string auditPath)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", PanelProofSchema }, { "handedness", "left" },
                { "construction", "independent_native_left_hand_seed" },
                { "task_id", "self-test-task" }, { "attempt", 1 },
                { "authorization_id", "native-auth-" + new string('b', 32) },
                { "authorization_sha256", hash },
                { "tool_source_normalized_sha256", hash },
                { "tool_executable_sha256", hash },
                { "source_path", Path.Combine(root, "native_cad", "working_pack", "left.SLDPRT") },
                { "source_sha256", hash }, { "reopen_sha256", hash },
                { "solidworks_major", 28 }, { "body_count", 1 },
                { "has_sheet_metal", true }, { "has_flat_pattern", true },
                { "error_feature_count", 0 }, { "warning_feature_count", 0 },
                { "foreign_feature_count", 0 }, { "traversal_complete", true },
                { "external_reference_count", 0 }, { "read_only_reopen_verified", true },
                { "height_dimension", "D1@草图1" }, { "width_dimension", "D2@草图1" },
                { "height_dimension_state", 2 }, { "width_dimension_state", 2 },
                { "generated_at_utc", "2098-01-01T00:01:00.000Z" },
                { "evidence_files", new object[] { new Dictionary<string, object>(
                    StringComparer.Ordinal) { { "path", auditPath },
                    { "sha256", Sha256(auditPath) } } } },
                { "evidence_commitment_sha256", "" }
            };
        }

        private static string PanelArtifactCommitment(Dictionary<string, object> value)
        {
            var projection = new Dictionary<string, object>(value, StringComparer.Ordinal);
            projection.Remove("evidenceCommitmentSha256");
            projection.Remove("evidence_commitment_sha256");
            return Sha256Text(StableJson(projection));
        }

        private static bool PanelProofDocumentValid(Dictionary<string, object> proof,
            string root, string taskId, int attempt, string authorizationId,
            string authorizationSha, string sourceSha, string toolSourceSha,
            string toolExecutableSha)
        {
            try
            {
                if (!ExactKeys(proof, PanelProofKeys) ||
                    !string.Equals(TextValue(proof, "evidence_commitment_sha256"),
                        PanelArtifactCommitment(proof), StringComparison.OrdinalIgnoreCase) ||
                    TextValue(proof, "schema") != PanelProofSchema ||
                    TextValue(proof, "handedness") != "left" ||
                    TextValue(proof, "construction") != "independent_native_left_hand_seed" ||
                    TextValue(proof, "task_id") != taskId ||
                    NumberValue(proof, "attempt") != attempt ||
                    TextValue(proof, "authorization_id") != authorizationId ||
                    !string.Equals(TextValue(proof, "authorization_sha256"), authorizationSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TextValue(proof, "source_sha256"), sourceSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TextValue(proof, "reopen_sha256"), sourceSha,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TextValue(proof, "tool_source_normalized_sha256"),
                        toolSourceSha, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TextValue(proof, "tool_executable_sha256"),
                        toolExecutableSha, StringComparison.OrdinalIgnoreCase) ||
                    !IsUnderRoot(TextValue(proof, "source_path"), root) ||
                    NumberValue(proof, "solidworks_major") != 28 ||
                    NumberValue(proof, "body_count") != 1 ||
                    !BoolValue(proof, "has_sheet_metal") ||
                    !BoolValue(proof, "has_flat_pattern") ||
                    NumberValue(proof, "error_feature_count") != 0 ||
                    NumberValue(proof, "warning_feature_count") != 0 ||
                    NumberValue(proof, "foreign_feature_count") != 0 ||
                    !BoolValue(proof, "traversal_complete") ||
                    NumberValue(proof, "external_reference_count") != 0 ||
                    !BoolValue(proof, "read_only_reopen_verified") ||
                    TextValue(proof, "height_dimension") != "D1@草图1" ||
                    TextValue(proof, "width_dimension") != "D2@草图1" ||
                    NumberValue(proof, "height_dimension_state") !=
                        (int)swDimensionDrivenState_e.swDimensionDriving ||
                    NumberValue(proof, "width_dimension_state") !=
                        (int)swDimensionDrivenState_e.swDimensionDriving) return false;
                object[] evidence = ArrayItems(proof, "evidence_files");
                if (evidence.Length != 1) return false;
                Dictionary<string, object> row = evidence[0] as Dictionary<string, object>;
                if (!ExactKeys(row, "path", "sha256")) return false;
                string path = TextValue(row, "path");
                if (!PathsEqual(path, Path.Combine(root,
                        PanelProofAuditRelativePath.Replace('/', '\\'))) ||
                    !File.Exists(path)) return false;
                if (!File.Exists(path) || !string.Equals(Sha256(path), TextValue(row, "sha256"),
                        StringComparison.OrdinalIgnoreCase)) return false;
                Dictionary<string, object> audit = ReadJsonObject(path);
                return ExactKeys(audit, PanelProofAuditKeys) &&
                    TextValue(audit, "schema") == PanelProofAuditSchema &&
                    string.Equals(TextValue(audit, "evidenceCommitmentSha256"),
                        PanelArtifactCommitment(audit), StringComparison.OrdinalIgnoreCase) &&
                    TextValue(audit, "taskId") == taskId &&
                    NumberValue(audit, "attempt") == attempt &&
                    TextValue(audit, "authorizationId") == authorizationId &&
                    string.Equals(TextValue(audit, "authorizationSha256"), authorizationSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(audit, "toolSourceNormalizedSha256"), toolSourceSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(audit, "toolExecutableSha256"), toolExecutableSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    PathsEqual(TextValue(audit, "sourcePath"), TextValue(proof, "source_path")) &&
                    string.Equals(TextValue(audit, "sourceSha256Before"), sourceSha,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(audit, "sourceSha256After"), sourceSha,
                        StringComparison.OrdinalIgnoreCase) && BoolValue(audit, "sourceUnchanged") &&
                    NumberValue(audit, "solidworksMajor") == 28 &&
                    TextValue(audit, "solidworksRevision").StartsWith("28.",
                        StringComparison.Ordinal) &&
                    string.Equals(Path.GetFileName(TextValue(audit,
                        "solidworksExecutablePath")), "SLDWORKS.exe",
                        StringComparison.OrdinalIgnoreCase) &&
                    NumberValue(audit, "ownedProcessId") > 0 &&
                    NumberValue(audit, "openErrors") == 0 &&
                    NumberValue(audit, "openWarnings") == 0 &&
                    BoolValue(audit, "openedReadOnly") &&
                    NumberValue(audit, "bodyCount") == 1 &&
                    NumberValue(audit, "featureCount") > 0 &&
                    BoolValue(audit, "hasSheetMetal") && BoolValue(audit, "hasFlatPattern") &&
                    NumberValue(audit, "mirrorPartFeatureCount") == 0 &&
                    NumberValue(audit, "foreignFeatureCount") == 0 &&
                    NumberValue(audit, "errorFeatureCount") == 0 &&
                    NumberValue(audit, "warningFeatureCount") == 0 &&
                    BoolValue(audit, "traversalComplete") &&
                    NumberValue(audit, "externalReferenceCount") == 0 &&
                    TextValue(audit, "heightDimension") == "D1@草图1" &&
                    TextValue(audit, "widthDimension") == "D2@草图1" &&
                    NumberValue(audit, "heightDimensionState") ==
                        (int)swDimensionDrivenState_e.swDimensionDriving &&
                    NumberValue(audit, "widthDimensionState") ==
                        (int)swDimensionDrivenState_e.swDimensionDriving;
            }
            catch { return false; }
        }

        private static Dictionary<string, object> SelfTestRuntimeReceipt(string evidenceSha,
            string commitment)
        {
            string hash = new string('A', 64);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", "winnsen.native_width_888.phase_receipt.v1" },
                { "phase", "assemblies" }, { "success", true },
                { "completedAt", "2098-01-01T00:01:00.000Z" },
                { "taskId", "self-test" }, { "taskRevision", 1 },
                { "taskDigest", hash }, { "requestDigest", hash },
                { "leaseId", "self-test-lease" },
                { "leaseExpiresAt", "2098-01-01T00:20:00.000Z" }, { "attempt", 1 },
                { "planSha256", hash }, { "authorizationId", "native-auth-" +
                    new string('a', 32) }, { "authorizationSha256", hash },
                { "authorizationJsonBase64", "e30=" },
                { "authorizationIssuedAt", "2098-01-01T00:00:00.000Z" },
                { "authorizationExpiresAt", "2098-01-01T00:10:00.000Z" },
                { "recipeId", ExpectedRecipeId },
                { "recipeDigest", ExpectedRecipeDigest.ToUpperInvariant() },
                { "toolId", "native_width_888_v1" },
                { "toolSourceNormalizedSha256", hash },
                { "toolExecutableSha256", hash },
                { "evidencePath", "evidence/width-assemblies.json" },
                { "evidenceSha256", evidenceSha },
                { "evidenceCommitmentSha256", commitment },
                { "preInventoryDigest", hash }, { "postInventoryDigest", hash },
                { "predecessorReceiptSha256", hash }
            };
        }

        private static bool ImmutableHashValid(string path, string expectedSha)
        {
            try
            {
                return File.Exists(path) && GetHardLinkCount(path) == 1 &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 &&
                    string.Equals(Sha256(path), expectedSha, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static int RunPanelProof(DoorRequest request, string requestPath,
            string outPath, string confirmation, RunResult result)
        {
            string root = "";
            string proofPath = FullPathOrEmpty(outPath);
            string auditPath = "";
            string lockPath = "";
            bool auditOwned = false;
            bool proofOwned = false;
            bool lockOwned = false;
            FileStream transactionLock = null;
            OwnedSession session = null;
            ModelDoc2 model = null;
            try
            {
                ValidatePanelProofRequest(request, requestPath, outPath, result);
                root = Path.GetFullPath(request.isolated_root);
                proofPath = Path.Combine(root, PanelProofRelativePath.Replace('/', '\\'));
                auditPath = Path.Combine(root, PanelProofAuditRelativePath.Replace('/', '\\'));
                Require(string.Equals(confirmation, request.task_id, StringComparison.Ordinal),
                    "TASK_CONFIRMATION_MISMATCH", 3,
                    "--confirm-task must exactly match request.task_id");
                BindExecutionAuthorization(request, result);
                Require(result.plan_execution_authorized,
                    "PANEL_PROOF_EXECUTION_NOT_AUTHORIZED", 3,
                    "panel proof requires the current door execution authorization");
                RevalidateAuthorizationTimeWindow(request, result);
                Require(ProcessIds("SLDWORKS").Count == 0 &&
                    ProcessIds("sldProcMon").Count == 0, "CAD_BASELINE_NOT_EMPTY", 3,
                    "panel proof requires an empty SLDWORKS and sldProcMon baseline");

                lockPath = Path.Combine(root, ".native-door-module-888x14.lock");
                RequireSafeMutationPath(lockPath, root, "TRANSACTION_LOCK_PATH_UNSAFE");
                transactionLock = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough);
                lockOwned = true;
                byte[] lockBytes = Encoding.UTF8.GetBytes(request.task_id + "\n" +
                    request.worker_id + "\n" + request.lease_id + "\npanel-proof\n");
                transactionLock.Write(lockBytes, 0, lockBytes.Length);
                transactionLock.Flush(true);

                string sourcePath = Path.GetFullPath(request.sources.left_panel_seed.path);
                string sourceShaBefore = Sha256(sourcePath);
                int openErrors = 0, openWarnings = 0;
                bool openedReadOnly = false;
                PartHealth health;
                int heightState = -1, widthState = -1;
                int externalReferenceCount = int.MaxValue;
                session = StartOwnedSession(request, "left_panel_proof_read_only", result);
                model = OpenDocument(session.sw, sourcePath,
                    (int)swDocumentTypes_e.swDocPART, true, ref openErrors, ref openWarnings);
                openedReadOnly = Safe(delegate { return model.IsOpenedReadOnly(); }, false);
                Require(model != null && openErrors == 0 && openWarnings == 0 && openedReadOnly,
                    "PANEL_PROOF_READONLY_OPEN_FAILED", 13,
                    "left panel seed must open read-only without errors or warnings");
                health = CapturePartHealth(model);
                Dimension heightDimension = FindDimension(model, "D1@草图1");
                Dimension widthDimension = FindDimension(model, "D2@草图1");
                heightState = heightDimension == null ? -1 :
                    Safe(delegate { return heightDimension.DrivenState; }, -1);
                widthState = widthDimension == null ? -1 :
                    Safe(delegate { return widthDimension.DrivenState; }, -1);
                Release(heightDimension);
                Release(widthDimension);
                externalReferenceCount = ExternalReferenceCount(model);
                Require(NativeSheetMetalGate(health) && externalReferenceCount == 0 &&
                    heightState == (int)swDimensionDrivenState_e.swDimensionDriving &&
                    widthState == (int)swDimensionDrivenState_e.swDimensionDriving,
                    "PANEL_PROOF_NATIVE_HEALTH_FAILED", 13,
                    "left panel seed must be one native sheet-metal body with FlatPattern, " +
                    "complete issue-free traversal, no foreign feature/reference, and driving D1/D2");
                CloseDocument(session.sw, ref model);
                StopOwnedSession(session, result);
                session = null;
                string sourceShaAfter = Sha256(sourcePath);
                Require(string.Equals(sourceShaBefore, sourceShaAfter,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sourceShaAfter,
                        NormalizeHash(request.sources.left_panel_seed.sha256),
                        StringComparison.OrdinalIgnoreCase),
                    "PANEL_PROOF_SOURCE_CHANGED", 13,
                    "read-only panel proof changed the left panel seed bytes");
                RevalidateAuthorizationTimeWindow(request, result);

                SessionEvidence sessionEvidence = result.sessions.Single(delegate(
                    SessionEvidence row) { return row.phase == "left_panel_proof_read_only"; });
                string completedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                var audit = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "schema", PanelProofAuditSchema }, { "taskId", request.task_id },
                    { "attempt", result.attempt_number },
                    { "authorizationId", result.authorization_id },
                    { "authorizationSha256", result.execution_authorization_sha256 },
                    { "toolSourceNormalizedSha256", result.tool_source_normalized_sha256 },
                    { "toolExecutableSha256", result.tool_executable_sha256 },
                    { "sourcePath", sourcePath }, { "sourceSha256Before", sourceShaBefore },
                    { "sourceSha256After", sourceShaAfter }, { "sourceUnchanged", true },
                    { "solidworksMajor", 28 },
                    { "solidworksRevision", sessionEvidence.solidworks_revision },
                    { "solidworksExecutablePath", sessionEvidence.executable_path },
                    { "ownedProcessId", sessionEvidence.process_id },
                    { "openErrors", openErrors }, { "openWarnings", openWarnings },
                    { "openedReadOnly", openedReadOnly }, { "bodyCount", health.body_count },
                    { "featureCount", health.feature_count },
                    { "hasSheetMetal", health.has_sheet_metal },
                    { "hasFlatPattern", health.has_flat_pattern },
                    { "mirrorPartFeatureCount", health.mirror_part_feature_count },
                    { "foreignFeatureCount", health.foreign_feature_count },
                    { "errorFeatureCount", health.error_feature_count },
                    { "warningFeatureCount", health.warning_feature_count },
                    { "traversalComplete", health.traversal_complete },
                    { "externalReferenceCount", externalReferenceCount },
                    { "heightDimension", "D1@草图1" },
                    { "widthDimension", "D2@草图1" },
                    { "heightDimensionState", heightState },
                    { "widthDimensionState", widthState }, { "completedAt", completedAt },
                    { "evidenceCommitmentSha256", "" }
                };
                audit["evidenceCommitmentSha256"] = PanelArtifactCommitment(audit);
                Require(ExactKeys(audit, PanelProofAuditKeys),
                    "PANEL_PROOF_AUDIT_SHAPE_INVALID", 13,
                    "panel proof audit keys differ from the exact contract");
                WriteJsonAtomicNew(auditPath, audit);
                auditOwned = true;
                Dictionary<string, object> auditReread = ReadJsonObject(auditPath);
                Require(ExactKeys(auditReread, PanelProofAuditKeys) &&
                    string.Equals(TextValue(auditReread, "evidenceCommitmentSha256"),
                        PanelArtifactCommitment(auditReread), StringComparison.OrdinalIgnoreCase),
                    "PANEL_PROOF_AUDIT_ATOMIC_REREAD_FAILED", 13,
                    "committed panel proof audit failed exact re-read");

                var proof = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "schema", PanelProofSchema }, { "handedness", "left" },
                    { "construction", "independent_native_left_hand_seed" },
                    { "task_id", request.task_id }, { "attempt", result.attempt_number },
                    { "authorization_id", result.authorization_id },
                    { "authorization_sha256", result.execution_authorization_sha256 },
                    { "tool_source_normalized_sha256", result.tool_source_normalized_sha256 },
                    { "tool_executable_sha256", result.tool_executable_sha256 },
                    { "source_path", sourcePath }, { "source_sha256", sourceShaBefore },
                    { "reopen_sha256", sourceShaAfter }, { "solidworks_major", 28 },
                    { "body_count", health.body_count },
                    { "has_sheet_metal", health.has_sheet_metal },
                    { "has_flat_pattern", health.has_flat_pattern },
                    { "error_feature_count", health.error_feature_count },
                    { "warning_feature_count", health.warning_feature_count },
                    { "foreign_feature_count", health.foreign_feature_count },
                    { "traversal_complete", health.traversal_complete },
                    { "external_reference_count", externalReferenceCount },
                    { "read_only_reopen_verified", true },
                    { "height_dimension", "D1@草图1" },
                    { "width_dimension", "D2@草图1" },
                    { "height_dimension_state", heightState },
                    { "width_dimension_state", widthState },
                    { "generated_at_utc", completedAt },
                    { "evidence_files", new object[] { new Dictionary<string, object>(
                        StringComparer.Ordinal) { { "path", auditPath },
                        { "sha256", Sha256(auditPath) } } } },
                    { "evidence_commitment_sha256", "" }
                };
                proof["evidence_commitment_sha256"] = PanelArtifactCommitment(proof);
                Require(ExactKeys(proof, PanelProofKeys), "PANEL_PROOF_SHAPE_INVALID", 13,
                    "panel proof keys differ from the exact contract");
                WriteJsonAtomicNew(proofPath, proof);
                proofOwned = true;
                Dictionary<string, object> proofReread = ReadJsonObject(proofPath);
                Require(PanelProofDocumentValid(proofReread, root, request.task_id,
                        result.attempt_number, result.authorization_id,
                        result.execution_authorization_sha256, sourceShaBefore,
                        result.tool_source_normalized_sha256, result.tool_executable_sha256),
                    "PANEL_PROOF_ATOMIC_REREAD_FAILED", 13,
                    "committed panel proof failed exact identity, health or evidence re-read");
                RevalidateAuthorizationTimeWindow(request, result);
                Require(!File.Exists(Path.Combine(root,
                        DoorReceiptRelativePath.Replace('/', '\\'))) &&
                    !File.Exists(Path.Combine(root,
                        DoorEvidenceRelativePath.Replace('/', '\\'))),
                    "PANEL_PROOF_STAGE_RECEIPT_POLLUTION", 13,
                    "panel proof must not create door stage evidence or receipt");

                transactionLock.Dispose();
                transactionLock = null;
                File.Delete(lockPath);
                Require(!File.Exists(lockPath), "TRANSACTION_LOCK_RELEASE_FAILED", 29,
                    "panel proof lock still exists after immutable proof commit");
                lockOwned = false;
                var output = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "status", "PANEL_PROOF_COMMITTED" }, { "cadStarted", true },
                    { "taskId", request.task_id }, { "attempt", result.attempt_number },
                    { "authorizationId", result.authorization_id },
                    { "authorizationConsumed", false }, { "stageReceiptCreated", false },
                    { "proofPath", proofPath }, { "proofSha256", Sha256(proofPath) },
                    { "auditPath", auditPath }, { "auditSha256", Sha256(auditPath) },
                    { "sourceUnchanged", true }, { "cadBaselineRestored", true }
                };
                Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                    RecursionLimit = 256 }.Serialize(output));
                return 0;
            }
            catch (Exception ex)
            {
                try { CloseDocument(session == null ? null : session.sw, ref model); }
                catch { }
                if (session != null)
                {
                    try { StopOwnedSession(session, result); }
                    catch { }
                }
                if (proofOwned) SafeDeleteOwnedArtifact(proofPath, root);
                if (auditOwned) SafeDeleteOwnedArtifact(auditPath, root);
                if (transactionLock != null) transactionLock.Dispose();
                if (lockOwned && !string.IsNullOrWhiteSpace(lockPath) && File.Exists(lockPath))
                    SafeDeleteOwnedArtifact(lockPath, root);
                string error = SafeExceptionText(ex);
                var output = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "status", "NO_GO" }, { "cadStarted", result.sessions.Count > 0 },
                    { "authorizationConsumed", false }, { "stageReceiptCreated", false },
                    { "proofCommitted", false }, { "error", error }
                };
                Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                    RecursionLimit = 256 }.Serialize(output));
                Console.Error.WriteLine(error);
                GateException gate = ex as GateException;
                return gate == null ? 90 : gate.ExitCode;
            }
        }

        private static void ValidatePanelProofRequest(DoorRequest request, string requestPath,
            string outPath, RunResult result)
        {
            Require(request != null && request.sources != null,
                "PANEL_PROOF_REQUEST_INVALID", 10,
                "panel proof requires a deserialized door request and source set");
            Require(request.schema == RequestSchema &&
                !string.IsNullOrWhiteSpace(request.task_id) &&
                !string.IsNullOrWhiteSpace(request.worker_id) &&
                !string.IsNullOrWhiteSpace(request.lease_id) &&
                Near(request.cabinet_width_mm, TargetCabinetWidthMm, 1e-9) &&
                Near(request.door_width_mm, TargetDoorWidthMm, 1e-9) &&
                Near(request.door_height_mm, TargetDoorHeightMm, 1e-9) &&
                request.columns == 2 && request.rows_per_column == 7 &&
                request.total_doors == 14 && !request.visible &&
                request.right_panel_strategy == RightStrategyMirrorPart,
                "PANEL_PROOF_REQUEST_CONTRACT_MISMATCH", 10,
                "panel proof is locked to the hidden 888W 2x7 MirrorPart door request");
            string root = Path.GetFullPath(request.isolated_root ?? "");
            Require(Directory.Exists(root), "ISOLATED_ROOT_NOT_FOUND", 11,
                "isolated_root must exist");
            BindTrustedAttemptAndPlan(request, root, result);
            Require(IsUnderRoot(requestPath, root), "REQUEST_OUTSIDE_ISOLATED_ROOT", 11,
                "request JSON must be inside isolated_root");
            RequireSafeReadPath(requestPath, root, "REQUEST_PATH_UNSAFE");
            string proofPath = Path.Combine(root, PanelProofRelativePath.Replace('/', '\\'));
            string auditPath = Path.Combine(root,
                PanelProofAuditRelativePath.Replace('/', '\\'));
            Require(PathsEqual(outPath, proofPath) &&
                PathsEqual(request.left_panel_proof, proofPath),
                "PANEL_PROOF_PATH_NOT_FIXED", 11,
                "--out and request.left_panel_proof must exactly name " +
                    PanelProofRelativePath);
            RequireSafeMutationPath(proofPath, root, "PANEL_PROOF_PATH_UNSAFE");
            RequireSafeMutationPath(auditPath, root, "PANEL_PROOF_AUDIT_PATH_UNSAFE");
            Require(!File.Exists(proofPath) && !Directory.Exists(proofPath) &&
                !File.Exists(auditPath) && !Directory.Exists(auditPath),
                "PANEL_PROOF_ARTIFACT_ALREADY_EXISTS", 11,
                "panel proof and audit are immutable and must both be absent");
            Require(!File.Exists(Path.Combine(root,
                    DoorEvidenceRelativePath.Replace('/', '\\'))) &&
                !File.Exists(Path.Combine(root,
                    DoorReceiptRelativePath.Replace('/', '\\'))),
                "PANEL_PROOF_STAGE_ALREADY_COMMITTED", 11,
                "panel proof must precede door evidence and receipt");
            for (int later = 6; later < StageReceiptPaths.Length; later++)
                Require(!File.Exists(Path.Combine(root,
                        StageReceiptPaths[later].Replace('/', '\\'))),
                    "PANEL_PROOF_STAGE_ALREADY_ADVANCED", 11,
                    "a later immutable stage receipt already exists: " + StageReceiptOrder[later]);
            Require(IsUnderRoot(request.output_dir, root) &&
                !PathsEqual(request.output_dir, root), "OUTPUT_SCOPE_INVALID", 11,
                "output_dir must be a child of isolated_root");
            RequireSafeMutationPath(request.output_dir, root, "OUTPUT_PATH_UNSAFE");
            string sentinelPath = Path.Combine(root, ".winnsen-native-isolated-root.json");
            Require(File.Exists(sentinelPath), "ISOLATION_SENTINEL_MISSING", 11,
                "isolated root sentinel is missing");
            IsolationSentinel sentinel = ReadJson<IsolationSentinel>(sentinelPath);
            Require(sentinel != null && sentinel.schema == SentinelSchema &&
                sentinel.task_id == request.task_id && sentinel.allow_native_solidworks_write,
                "ISOLATION_SENTINEL_INVALID", 11,
                "sentinel schema/task/write authorization is invalid");
            CaptureCurrentWorkingPackInventory(root, result);
            ValidateLivePredecessorReceiptChain(root, result);
            List<SourceRow> sources = SourceRows(request.sources, true);
            Require(sources.Count == 8, "SOURCE_SET_INCOMPLETE", 12,
                "panel proof requires the exact eight MirrorPart source records");
            foreach (SourceRow row in sources) ValidateSource(row, root);
            Require(request.sources.right_panel_seed == null &&
                string.IsNullOrWhiteSpace(request.right_panel_proof),
                "MIRROR_ROUTE_RIGHT_INPUT_POLLUTION", 12,
                "panel proof does not accept a right seed or right proof");
            result.attempt_number = AttemptNumber(root);
            result.cad_process_baseline_sldworks = ProcessIds("SLDWORKS");
            result.cad_process_baseline_sldprocmon = ProcessIds("sldProcMon");
            result.preflight_passed = true;
        }

        private static void Preflight(DoorRequest request, string requestPath, string outPath,
            RunResult result)
        {
            Require(request != null, "REQUEST_DESERIALIZE_FAILED", 10,
                "request JSON did not deserialize");
            Require(string.Equals(request.schema, RequestSchema, StringComparison.Ordinal),
                "REQUEST_SCHEMA_MISMATCH", 10, "unexpected request schema");
            Require(!string.IsNullOrWhiteSpace(request.task_id) &&
                !string.IsNullOrWhiteSpace(request.worker_id) &&
                !string.IsNullOrWhiteSpace(request.lease_id), "OWNERSHIP_FIELDS_MISSING", 10,
                "task_id, worker_id, and lease_id are required");
            Require(Near(request.cabinet_width_mm, TargetCabinetWidthMm, 1e-9) &&
                Near(request.door_width_mm, TargetDoorWidthMm, 1e-9) &&
                Near(request.door_height_mm, TargetDoorHeightMm, 1e-9) &&
                request.columns == 2 && request.rows_per_column == 7 && request.total_doors == 14,
                "TARGET_CONTRACT_MISMATCH", 10,
                "this executable is locked to 888W / 14 doors / 2 columns / 7 rows, " +
                "with door W381 and H1781/7 mm");
            Require(string.Equals(request.right_panel_strategy, RightStrategyMirrorPart,
                    StringComparison.Ordinal), "RIGHT_PANEL_STRATEGY_INVALID", 10,
                "right_panel_strategy must be solidworks_mirror_part");
            result.right_panel_strategy = request.right_panel_strategy;

            string root = Path.GetFullPath(request.isolated_root ?? "");
            Require(Directory.Exists(root), "ISOLATED_ROOT_NOT_FOUND", 11,
                "isolated_root must exist");
            BindTrustedAttemptAndPlan(request, root, result);
            Require(IsUnderRoot(requestPath, root), "REQUEST_OUTSIDE_ISOLATED_ROOT", 11,
                "request JSON must be inside isolated_root");
            RequireSafeReadPath(requestPath, root, "REQUEST_PATH_UNSAFE");
            Require(!string.IsNullOrWhiteSpace(outPath) && IsUnderRoot(outPath, root),
                "EVIDENCE_OUTSIDE_ISOLATED_ROOT", 11,
                "result JSON must be inside isolated_root");
            string expectedEvidence = Path.Combine(root,
                DoorEvidenceRelativePath.Replace('/', '\\'));
            Require(PathsEqual(outPath, expectedEvidence), "EVIDENCE_PATH_NOT_FIXED", 11,
                "--out must be exactly " + DoorEvidenceRelativePath);
            RequireSafeMutationPath(outPath, root, "EVIDENCE_PATH_UNSAFE");
            result.receipt_path = Path.Combine(root,
                DoorReceiptRelativePath.Replace('/', '\\'));
            RequireSafeMutationPath(result.receipt_path, root, "RECEIPT_PATH_UNSAFE");
            Require(SuccessPairPathsAvailable(outPath, result.receipt_path),
                "UNPAIRED_OR_EXISTING_EVIDENCE_RECEIPT", 11,
                "door success evidence and receipt are immutable and both final paths must be absent");
            for (int later = 6; later < StageReceiptPaths.Length; later++)
                Require(!File.Exists(Path.Combine(root,
                        StageReceiptPaths[later].Replace('/', '\\'))),
                    "DOOR_STAGE_ALREADY_ADVANCED", 11,
                    "a later immutable stage receipt already exists: " + StageReceiptOrder[later]);
            Require(IsUnderRoot(request.output_dir, root) &&
                !PathsEqual(request.output_dir, root), "OUTPUT_SCOPE_INVALID", 11,
                "output_dir must be a child of isolated_root");
            RequireSafeMutationPath(request.output_dir, root, "OUTPUT_PATH_UNSAFE");
            Require(!IsUnderRoot(outPath, request.output_dir) &&
                !PathsEqual(outPath, request.output_dir), "EVIDENCE_INSIDE_OUTPUT_FORBIDDEN", 11,
                "result evidence must remain outside the exact 13-CAD plus manifest output tree");

            string sentinelPath = Path.Combine(root, ".winnsen-native-isolated-root.json");
            Require(File.Exists(sentinelPath), "ISOLATION_SENTINEL_MISSING", 11,
                "isolated root sentinel is missing");
            IsolationSentinel sentinel = ReadJson<IsolationSentinel>(sentinelPath);
            Require(sentinel != null && string.Equals(sentinel.schema, SentinelSchema,
                StringComparison.Ordinal) && string.Equals(sentinel.task_id, request.task_id,
                StringComparison.Ordinal) && sentinel.allow_native_solidworks_write,
                "ISOLATION_SENTINEL_INVALID", 11,
                "sentinel schema/task/write authorization is invalid");

            Require(request.sources != null, "SOURCE_SET_MISSING", 12,
                "sources object is required");
            CaptureCurrentWorkingPackInventory(root, result);
            ValidateLivePredecessorReceiptChain(root, result);
            bool mirrorRight = true;
            var sources = SourceRows(request.sources, mirrorRight);
            Require(sources.Count == 8, "SOURCE_SET_INCOMPLETE", 12,
                "exactly eight native part sources are required for MirrorPart2");
            foreach (SourceRow row in sources)
            {
                ValidateSource(row, root);
                result.sources.Add(new SourceEvidence
                {
                    role = row.role,
                    path = Path.GetFullPath(row.spec.path),
                    expected_sha256 = NormalizeHash(row.spec.sha256),
                    actual_sha256 = Sha256(row.spec.path),
                    size_bytes = new FileInfo(row.spec.path).Length,
                    inside_isolated_root = true
                });
            }

            string leftHash = NormalizeHash(request.sources.left_panel_seed.sha256);
            string expectedPanelProof = Path.Combine(root,
                PanelProofRelativePath.Replace('/', '\\'));
            Require(PathsEqual(request.left_panel_proof, expectedPanelProof),
                "PANEL_PROOF_PATH_NOT_FIXED", 13,
                "left panel proof must exactly name " + PanelProofRelativePath);
            ValidatePanelProof(request.left_panel_proof, "left", leftHash, root, result, false);
            Require(request.sources.right_panel_seed == null &&
                string.IsNullOrWhiteSpace(request.right_panel_proof),
                "MIRROR_ROUTE_RIGHT_INPUT_POLLUTION", 12,
                "solidworks_mirror_part must not accept a right seed or right proof");
            result.right_panel_route_gate = "MIRRORPART2_BREAK_LINK_REQUIRED";
            result.denied_v37_right_hashes.AddRange(DeniedRightPanelHashes.OrderBy(
                delegate(string value) { return value; }, StringComparer.OrdinalIgnoreCase));
            result.cad_process_baseline_sldworks = ProcessIds("SLDWORKS");
            result.cad_process_baseline_sldprocmon = ProcessIds("sldProcMon");
        }

        private static void ValidateSource(SourceRow row, string root)
        {
            Require(row.spec != null && !string.IsNullOrWhiteSpace(row.spec.path),
                "SOURCE_PATH_MISSING", 12, row.role + " path is missing");
            string path = Path.GetFullPath(row.spec.path);
            Require(IsUnderRoot(path, root), "SOURCE_OUTSIDE_ISOLATED_ROOT", 12,
                row.role + " must be an isolated copy inside isolated_root");
            Require(File.Exists(path), "SOURCE_NOT_FOUND", 12, row.role + " file is missing");
            RequireSafeReadPath(path, root, "SOURCE_PATH_UNSAFE");
            Require(string.Equals(Path.GetExtension(path), ".SLDPRT",
                StringComparison.OrdinalIgnoreCase), "NON_NATIVE_PART_SOURCE_DENIED", 12,
                row.role + " must be a .SLDPRT source");
            string expected = NormalizeHash(row.spec.sha256);
            Require(expected.Length == 64, "SOURCE_HASH_INVALID", 12,
                row.role + " requires an exact SHA-256");
            string trustedExpected;
            Require(TrustedSourceHashes.TryGetValue(row.role, out trustedExpected) &&
                string.Equals(expected, trustedExpected, StringComparison.OrdinalIgnoreCase),
                "SOURCE_NOT_IN_TRUSTED_HASH_REGISTRY", 12,
                row.role + " does not match the compiled trusted source SHA-256");
            Require(string.Equals(expected, Sha256(path), StringComparison.OrdinalIgnoreCase),
                "SOURCE_HASH_MISMATCH", 12, row.role + " SHA-256 does not match");
        }

        private static void BindTrustedAttemptAndPlan(DoorRequest request, string root,
            RunResult result)
        {
            string configuredRoot = System.Environment.GetEnvironmentVariable(
                AttemptRootEnvironmentVariable) ?? "";
            Require(!string.IsNullOrWhiteSpace(configuredRoot) &&
                Path.IsPathRooted(configuredRoot), "TRUSTED_ATTEMPT_ROOT_MISSING", 11,
                AttemptRootEnvironmentVariable + " must be an absolute path");
            string attemptsRoot = Path.GetFullPath(configuredRoot).TrimEnd('\\', '/');
            Require(Directory.Exists(attemptsRoot) && IsUnderRoot(root, attemptsRoot),
                "ISOLATED_ROOT_OUTSIDE_TRUSTED_ATTEMPTS", 11,
                "isolated_root is outside the worker-owned attempts root");
            string relative = root.Substring(attemptsRoot.Length).TrimStart('\\', '/');
            string[] segments = relative.Split(new[] { '\\', '/' },
                StringSplitOptions.RemoveEmptyEntries);
            Require(segments.Length == 2 &&
                string.Equals(segments[0], request.task_id, StringComparison.Ordinal) &&
                segments[1].StartsWith("attempt-", StringComparison.OrdinalIgnoreCase) &&
                segments[1].Length == 12 && segments[1].Substring(8).All(char.IsDigit),
                "TRUSTED_ATTEMPT_DIRECTORY_SHAPE_MISMATCH", 11,
                "isolated_root must be <attempts-root>/<task-id>/attempt-NNNN");
            Require(NoReparsePoints(attemptsRoot, root), "ATTEMPT_PATH_HAS_REPARSE_POINT", 11,
                "worker-owned attempt path contains a reparse point");

            string planPath = Path.Combine(root, "native_build_plan.json");
            string expectedPlanSha = NormalizeHash(System.Environment.GetEnvironmentVariable(
                PlanShaEnvironmentVariable));
            Require(File.Exists(planPath), "IMMUTABLE_PLAN_MISSING", 11,
                "native_build_plan.json is missing");
            RequireSafeReadPath(planPath, root, "IMMUTABLE_PLAN_PATH_UNSAFE");
            Require(expectedPlanSha.Length == 64 &&
                string.Equals(Sha256(planPath), expectedPlanSha,
                    StringComparison.OrdinalIgnoreCase), "IMMUTABLE_PLAN_HASH_MISMATCH", 11,
                "native_build_plan.json does not match the task-store SHA-256");
            NativeBuildPlan plan = ReadJson<NativeBuildPlan>(planPath);
            Dictionary<string, object> planObject = ReadJsonObject(planPath);
            Require(plan != null &&
                string.Equals(plan.schema, "winnsen.native_build_plan.v1",
                    StringComparison.Ordinal) &&
                string.Equals(plan.purpose, "structure_engineering_assistance",
                    StringComparison.Ordinal) &&
                string.Equals(plan.workerId, request.worker_id, StringComparison.Ordinal) &&
                plan.task != null &&
                string.Equals(plan.task.id, request.task_id, StringComparison.Ordinal) &&
                plan.task.revisionAtPlanning > 0 && plan.request != null &&
                NormalizeHash(plan.request.fingerprint).Length == 64 &&
                NormalizeHash(plan.request.digest).Length == 64 && plan.recipe != null &&
                string.Equals(plan.recipe.id, ExpectedRecipeId, StringComparison.Ordinal) &&
                plan.recipe.version == 1 &&
                string.Equals(plan.recipe.digest, ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) && plan.geometry != null &&
                Near(plan.geometry.cabinetWidthMm, 888.0, 1e-9) &&
                Near(plan.geometry.cabinetHeightMm, 1917.0, 1e-9) &&
                Near(plan.geometry.cabinetDepthMm, 550.0, 1e-9) &&
                plan.geometry.columns == 2 && plan.geometry.doorCount == 14 &&
                plan.geometry.columnDoorCounts != null &&
                plan.geometry.columnDoorCounts.SequenceEqual(new[] { 7, 7 }) &&
                string.Equals(plan.geometry.rowSequence, "L1111111-R1111111",
                    StringComparison.Ordinal) &&
                Near(plan.geometry.doorPanelWidthMm, 381.0, 1e-9) &&
                plan.requiredChecks != null &&
                plan.requiredChecks.Contains("one_door_one_lock") &&
                plan.requiredChecks.Contains("rebuild_save_reopen") &&
                plan.requiredChecks.Contains("relocated_reopen") &&
                plan.executionBoundary != null &&
                !plan.executionBoundary.executorImplemented &&
                !plan.executionBoundary.cadStarted && !plan.executionBoundary.modelGenerated &&
                !plan.executionBoundary.modelReady && !plan.executionBoundary.legacyFallbackUsed &&
                plan.qualityBoundary != null &&
                plan.qualityBoundary.planningOnly &&
                !plan.qualityBoundary.engineeringAssistanceReady &&
                plan.qualityBoundary.readyOnlyAfterEveryRequiredCheckPasses,
                "IMMUTABLE_PLAN_CONTRACT_MISMATCH", 11,
                "native build plan is not a permanent planning-only exact trusted 888x14 recipe");
            result.plan_path = planPath;
            result.plan_sha256 = expectedPlanSha;
            result.task_revision_at_planning = plan.task.revisionAtPlanning;
            result.task_digest = NormalizeHash(plan.task.digest);
            result.request_fingerprint = NormalizeHash(plan.request.fingerprint);
            result.request_digest = NormalizeHash(plan.request.digest);
            result.recipe_id = plan.recipe.id;
            result.recipe_digest = plan.recipe.digest;
            result.plan_worker_id = plan.workerId;
            result.plan_execution_authorized = false;
            result.trusted_attempt_gate = true;
            ValidateStageReceiptContracts(planObject, result);
            ValidateTrustedToolchainManifests(planObject, result);
        }

        private static void ValidateStageReceiptContracts(Dictionary<string, object> plan,
            RunResult result)
        {
            Dictionary<string, object> contracts = ChildMap(plan, "stageReceiptContracts");
            RequireExactKeys(contracts, "plan.stageReceiptContracts", StageReceiptOrder);
            for (int index = 0; index < StageReceiptOrder.Length; index++)
            {
                string id = StageReceiptOrder[index];
                Dictionary<string, object> row = ChildMap(contracts, id);
                RequireExactKeys(row, "plan.stageReceiptContracts." + id, "schema", "path",
                    "producerToolId", "predecessor");
                Require(TextValue(row, "schema") == StageReceiptSchemas[index] &&
                    TextValue(row, "path") == StageReceiptPaths[index] &&
                    TextValue(row, "producerToolId") == StageReceiptTools[index] &&
                    TextValue(row, "predecessor") == StageReceiptPredecessors[index] &&
                    IsCanonicalReceiptRelativePath(TextValue(row, "path")),
                    "IMMUTABLE_NATIVE_STAGE_RECEIPT_CONTRACT_MISMATCH", 11,
                    "stageReceiptContracts differs from the shared nine-stage contract: " + id);
            }
            result.stage_receipt_contract_gate = true;
        }

        private static void ValidateTrustedToolchainManifests(Dictionary<string, object> plan,
            RunResult result)
        {
            Dictionary<string, object> frozen = ChildMap(plan, "trustedToolchainManifests");
            RequireExactKeys(frozen, "plan.trustedToolchainManifests",
                TrustedToolchainManifestKeys);
            string repositoryRoot = FindRepositoryRoot(
                Process.GetCurrentProcess().MainModule.FileName);
            Require(!string.IsNullOrWhiteSpace(repositoryRoot), "REPOSITORY_ROOT_NOT_FOUND", 11,
                "repository root is required for frozen toolchain validation");
            Dictionary<string, string> relativePaths =
                TrustedToolchainManifestRelativePaths();
            foreach (string id in TrustedToolchainManifestKeys)
            {
                string expectedSha = NormalizeHash(ValueText(frozen[id]));
                string path = Path.GetFullPath(Path.Combine(repositoryRoot,
                    relativePaths[id].Replace('/', '\\')));
                Require(IsSha256(expectedSha) && File.Exists(path),
                    "TRUSTED_TOOLCHAIN_MANIFEST_MISSING", 11,
                    "plan-frozen toolchain manifest is missing: " + id);
                RequireSafeReadPath(path, repositoryRoot, "TRUSTED_TOOLCHAIN_MANIFEST_PATH_UNSAFE");
                Require(string.Equals(Sha256(path), expectedSha,
                        StringComparison.OrdinalIgnoreCase),
                    "TRUSTED_TOOLCHAIN_MANIFEST_LIVE_HASH_MISMATCH", 11,
                    "plan-frozen toolchain manifest changed: " + id);
                Dictionary<string, object> document = ReadJsonObject(path);
                string sourceSha, executableSha;
                ValidateToolchainManifestDocument(id, document, repositoryRoot,
                    out sourceSha, out executableSha);
                result.toolchain_manifests.Add(new TrustedArtifactEvidence
                {
                    id = id,
                    schema = TextValue(document, "schema"),
                    path = path,
                    sha256 = expectedSha
                });
                if (id == DoorModulePhase)
                {
                    result.tool_source_normalized_sha256 = sourceSha;
                    result.tool_executable_sha256 = executableSha;
                }
            }
            Require(string.Equals(result.tool_source_normalized_sha256,
                    NormalizedSourceSha256(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(result.tool_executable_sha256,
                    Sha256(Process.GetCurrentProcess().MainModule.FileName),
                    StringComparison.OrdinalIgnoreCase),
                "DOOR_TOOLCHAIN_MANIFEST_SELF_IDENTITY_MISMATCH", 11,
                "door toolchain manifest does not bind this reviewed source and executable");
            result.trusted_toolchain_manifest_gate = true;
        }

        private static Dictionary<string, string> TrustedToolchainManifestRelativePaths()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { TrustedToolchainManifestKeys[0],
                    "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json" },
                { TrustedToolchainManifestKeys[1],
                    "workers/native_model_requests/development/v1/tools/width_888_native_v1/toolchain_manifest.json" },
                { TrustedToolchainManifestKeys[2],
                    "workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/toolchain_manifest.json" },
                { TrustedToolchainManifestKeys[3],
                    "workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json" },
                { TrustedToolchainManifestKeys[4],
                    "workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/toolchain_manifest.json" },
                { TrustedToolchainManifestKeys[5],
                    "workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/toolchain_manifest.json" }
            };
        }

        private static void ValidateToolchainManifestDocument(string id,
            Dictionary<string, object> document, string repositoryRoot,
            out string sourceSha, out string executableSha)
        {
            sourceSha = "";
            executableSha = "";
            if (id == LockToolchainIds[0])
            {
                RequireToolchainExactKeys(document, "lock toolchain manifest",
                    "schema", "generatedBy", "tools", "validator");
                Require(TextValue(document, "schema") == LockToolchainManifestSchema &&
                    TextValue(document, "generatedBy") == LockToolchainManifestGeneratedBy,
                    "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 11,
                    "lock toolchain manifest schema or producer is invalid");
                Dictionary<string, object> tools = ChildMap(document, "tools");
                RequireToolchainExactKeys(tools, "lock toolchain manifest.tools",
                    LockToolchainIds);
                foreach (string lockToolId in LockToolchainIds)
                {
                    Dictionary<string, object> tool = ChildMap(tools, lockToolId);
                    RequireToolchainExactKeys(tool,
                        "lock toolchain manifest.tools." + lockToolId,
                        "sourcePath", "sourceNormalizedSha256", "executablePath",
                        "executableSha256");
                    Require(LiveLockManifestArtifact(repositoryRoot, tool, "sourcePath",
                            "sourceNormalizedSha256", true) &&
                        LiveLockManifestArtifact(repositoryRoot, tool, "executablePath",
                            "executableSha256", false),
                        "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 11,
                        "lock toolchain manifest has a non-live tool binding: " + lockToolId);
                }
                Dictionary<string, object> validator = ChildMap(document, "validator");
                RequireToolchainExactKeys(validator, "lock toolchain manifest.validator",
                    "path", "sha256");
                Require(LiveLockManifestArtifact(repositoryRoot, validator, "path", "sha256",
                        false), "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 11,
                    "lock toolchain manifest validator binding is not live");
                Dictionary<string, object> primary = ChildMap(tools, id);
                sourceSha = NormalizeHash(TextValue(primary, "sourceNormalizedSha256"));
                executableSha = NormalizeHash(TextValue(primary, "executableSha256"));
                return;
            }

            RequireToolchainExactKeys(document, "toolchain manifest " + id,
                "schema", "generatedBy", "tool");
            Dictionary<string, object> genericTool = ChildMap(document, "tool");
            RequireToolchainExactKeys(genericTool, "toolchain manifest " + id + ".tool", "id",
                "sourcePath", "sourceNormalizedSha256", "executablePath",
                "executableSha256", "verifierPath", "verifierSha256");
            Require(TextValue(document, "schema") == ToolchainManifestSchema &&
                !string.IsNullOrWhiteSpace(TextValue(document, "generatedBy")) &&
                TextValue(genericTool, "id") == id &&
                LiveManifestArtifact(repositoryRoot, genericTool, "sourcePath",
                    "sourceNormalizedSha256", true) &&
                LiveManifestArtifact(repositoryRoot, genericTool, "executablePath",
                    "executableSha256", false) &&
                LiveManifestArtifact(repositoryRoot, genericTool, "verifierPath",
                    "verifierSha256", false),
                "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 11,
                "toolchain manifest is not the exact generic live binding: " + id);
            sourceSha = NormalizeHash(TextValue(genericTool, "sourceNormalizedSha256"));
            executableSha = NormalizeHash(TextValue(genericTool, "executableSha256"));
        }

        private static void RequireToolchainExactKeys(IDictionary<string, object> value,
            string label, params string[] expected)
        {
            Require(ExactKeys(value, expected), "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 11,
                label + " keys do not exactly match the registered toolchain contract");
        }

        private static bool LiveLockManifestArtifact(string repositoryRoot,
            Dictionary<string, object> artifact, string pathKey, string hashKey,
            bool normalizedSource)
        {
            string value = TextValue(artifact, pathKey);
            string expected = NormalizeHash(TextValue(artifact, hashKey));
            if (string.IsNullOrWhiteSpace(value) || !IsSha256(expected)) return false;
            string path;
            if (Path.IsPathRooted(value)) path = Path.GetFullPath(value);
            else
            {
                if (value.Contains("\\") || value.Split('/').Any(delegate(string segment)
                    { return segment == "." || segment == ".."; })) return false;
                path = Path.GetFullPath(Path.Combine(repositoryRoot, value.Replace('/', '\\')));
            }
            try
            {
                RequireSafeReadPath(path, repositoryRoot, "TOOLCHAIN_ARTIFACT_PATH_UNSAFE");
                string actual = normalizedSource ? NormalizedLockSourceSha256At(path) :
                    Sha256(path);
                return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool LiveManifestArtifact(string repositoryRoot,
            Dictionary<string, object> tool, string pathKey, string hashKey,
            bool normalizedSource)
        {
            string relative = TextValue(tool, pathKey);
            string expected = NormalizeHash(TextValue(tool, hashKey));
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("\\") ||
                Path.IsPathRooted(relative) || relative.Split('/').Any(delegate(string segment)
                { return segment == "." || segment == ".."; }) || !IsSha256(expected)) return false;
            string path = Path.GetFullPath(Path.Combine(repositoryRoot,
                relative.Replace('/', '\\')));
            try
            {
                RequireSafeReadPath(path, repositoryRoot, "TOOLCHAIN_ARTIFACT_PATH_UNSAFE");
                string actual = normalizedSource ? NormalizedSourceSha256At(path) : Sha256(path);
                return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void CaptureCurrentWorkingPackInventory(string attemptRoot,
            RunResult result)
        {
            string workingPack = Path.Combine(attemptRoot, "native_cad", "working_pack");
            Require(Directory.Exists(workingPack), "CURRENT_WORKING_PACK_MISSING", 11,
                "the width assemblies handoff working pack is missing");
            RequireSafeReadPath(workingPack, attemptRoot, "CURRENT_WORKING_PACK_PATH_UNSAFE");
            Require(Directory.GetDirectories(workingPack, "*", SearchOption.TopDirectoryOnly).Length == 0,
                "CURRENT_WORKING_PACK_NOT_FLAT", 11,
                "the width assemblies handoff must remain a flat CAD pack");
            string[] files = Directory.GetFiles(workingPack, "*", SearchOption.TopDirectoryOnly);
            var names = new HashSet<string>(files.Select(Path.GetFileName),
                StringComparer.OrdinalIgnoreCase);
            int parts = files.Count(delegate(string path)
            { return string.Equals(Path.GetExtension(path), ".SLDPRT", StringComparison.OrdinalIgnoreCase); });
            int assemblies = files.Count(delegate(string path)
            { return string.Equals(Path.GetExtension(path), ".SLDASM", StringComparison.OrdinalIgnoreCase); });
            Require(files.Length == 75 && parts == 53 && assemblies == 22 &&
                names.SetEquals(V37WorkingPackFileNames) && files.All(delegate(string path)
                {
                    try { RequireSafeReadPath(path, attemptRoot, "CURRENT_WORKING_PACK_FILE_UNSAFE");
                        return true; }
                    catch { return false; }
                }), "CURRENT_WORKING_PACK_INVENTORY_INVALID", 11,
                "door stage requires the exact live 75-file width assemblies handoff");
            result.working_pack_path = workingPack;
            result.current_pre_inventory_digest = CanonicalInventoryDigest(files);
            result.post_inventory_digest = result.current_pre_inventory_digest;
        }

        private static void ValidateLivePredecessorReceiptChain(string attemptRoot,
            RunResult result)
        {
            result.attempt_number = AttemptNumber(attemptRoot);
            string predecessorSha = "";
            string predecessorPostDigest = "";
            for (int index = 0; index <= 4; index++)
            {
                string phase = StageReceiptOrder[index];
                string path = Path.GetFullPath(Path.Combine(attemptRoot,
                    StageReceiptPaths[index].Replace('/', '\\')));
                Require(File.Exists(path), "LIVE_STAGE_RECEIPT_FILE_MISSING", 11,
                    "required predecessor receipt is missing: " + phase);
                RequireSafeReadPath(path, attemptRoot, "LIVE_STAGE_RECEIPT_PATH_UNSAFE");
                string receiptSha = Sha256(path);
                Dictionary<string, object> receipt = ReadJsonObject(path);
                RequireExactKeys(receipt, "runtime receipt " + phase,
                    index == 0 ? SeedReceiptKeys : RuntimeReceiptKeys);
                string preDigest = index == 0 ? TextValue(receipt, "sourceInventoryDigest") :
                    TextValue(receipt, "preInventoryDigest");
                string postDigest = index == 0 ? TextValue(receipt, "targetInventoryDigest") :
                    TextValue(receipt, "postInventoryDigest");
                Require(RuntimeReceiptEnvelopeValid(receipt, StageReceiptSchemas[index], phase,
                        StageReceiptTools[index]) &&
                    TextValue(receipt, "taskId") == result.task_id &&
                    NumberValue(receipt, "taskRevision") == result.task_revision_at_planning &&
                    string.Equals(TextValue(receipt, "taskDigest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "requestDigest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    NumberValue(receipt, "attempt") == AttemptNumber(attemptRoot) &&
                    TextValue(receipt, "recipeId") == ExpectedRecipeId &&
                    string.Equals(TextValue(receipt, "recipeDigest"), ExpectedRecipeDigest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "predecessorReceiptSha256"), predecessorSha,
                        StringComparison.OrdinalIgnoreCase) && IsSha256(postDigest) &&
                    (index == 0 ? string.Equals(preDigest, V37CanonicalInventoryDigest,
                        StringComparison.OrdinalIgnoreCase) : IsSha256(preDigest)) &&
                    (index == 0 || string.Equals(preDigest, predecessorPostDigest,
                        StringComparison.OrdinalIgnoreCase)),
                    "LIVE_STAGE_RECEIPT_CHAIN_BINDING_INVALID", 11,
                    "predecessor receipt identity or inventory chain is invalid: " + phase);
                ValidateReceiptToolchainBinding(receipt, result, phase);
                ValidateHistoricalReceiptAuthorization(receipt, preDigest, result, phase);
                ValidateReceiptEvidencePair(receipt, attemptRoot, phase, index);
                Require(string.Equals(Sha256(path), receiptSha,
                        StringComparison.OrdinalIgnoreCase),
                    "LIVE_STAGE_RECEIPT_CHANGED_DURING_READ", 11,
                    "predecessor receipt changed while being validated: " + phase);
                result.predecessor_receipts.Add(new TrustedArtifactEvidence
                {
                    id = phase, schema = TextValue(receipt, "schema"), path = path,
                    sha256 = receiptSha
                });
                predecessorSha = receiptSha;
                predecessorPostDigest = postDigest;
            }
            Require(string.Equals(predecessorPostDigest, result.current_pre_inventory_digest,
                    StringComparison.OrdinalIgnoreCase),
                "ASSEMBLIES_RECEIPT_POST_TO_DOOR_PRE_MISMATCH", 11,
                "width assemblies post-inventory must equal the live door-stage pre-inventory");
            result.predecessor_receipt_sha256 = predecessorSha;
            result.predecessor_receipt_chain_gate = true;
        }

        private static bool RuntimeReceiptEnvelopeValid(Dictionary<string, object> receipt,
            string schema, string phase, string toolId)
        {
            return receipt != null && TextValue(receipt, "schema") == schema &&
                TextValue(receipt, "phase") == phase && BoolValue(receipt, "success") &&
                TextValue(receipt, "toolId") == toolId &&
                IsSha256(TextValue(receipt, "toolSourceNormalizedSha256")) &&
                IsSha256(TextValue(receipt, "toolExecutableSha256")) &&
                IsSha256(TextValue(receipt, "authorizationSha256")) &&
                IsSha256(TextValue(receipt, "evidenceSha256")) &&
                IsSha256(TextValue(receipt, "evidenceCommitmentSha256"));
        }

        private static void ValidateReceiptToolchainBinding(Dictionary<string, object> receipt,
            RunResult result, string phase)
        {
            string toolId = TextValue(receipt, "toolId");
            TrustedArtifactEvidence artifact = result.toolchain_manifests.SingleOrDefault(
                delegate(TrustedArtifactEvidence row) { return row.id == toolId; });
            Require(artifact != null, "RECEIPT_TOOLCHAIN_MANIFEST_MISSING", 11,
                "receipt producer has no frozen toolchain manifest: " + phase);
            Dictionary<string, object> tool = ChildMap(ReadJsonObject(artifact.path), "tool");
            Require(string.Equals(TextValue(tool, "sourceNormalizedSha256"),
                    TextValue(receipt, "toolSourceNormalizedSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(tool, "executableSha256"),
                    TextValue(receipt, "toolExecutableSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "RECEIPT_TOOLCHAIN_IDENTITY_MISMATCH", 11,
                "receipt tool identity differs from its frozen manifest: " + phase);
        }

        private static void ValidateHistoricalReceiptAuthorization(
            Dictionary<string, object> receipt, string preDigest, RunResult result, string phase)
        {
            byte[] authorizationBytes;
            try { authorizationBytes = Convert.FromBase64String(
                    TextValue(receipt, "authorizationJsonBase64")); }
            catch { throw new GateException("RECEIPT_AUTHORIZATION_BASE64_INVALID", 11,
                    "embedded historical authorization is invalid: " + phase); }
            Require(string.Equals(Sha256Bytes(authorizationBytes),
                    TextValue(receipt, "authorizationSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "RECEIPT_AUTHORIZATION_BYTES_HASH_MISMATCH", 11,
                "embedded historical authorization bytes do not match: " + phase);
            Dictionary<string, object> authorization;
            try
            {
                authorization = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                    RecursionLimit = 256 }.DeserializeObject(
                        new UTF8Encoding(false).GetString(authorizationBytes)) as
                        Dictionary<string, object>;
            }
            catch { authorization = null; }
            Require(authorization != null, "RECEIPT_AUTHORIZATION_JSON_INVALID", 11,
                "embedded historical authorization is not an object: " + phase);
            ValidateAuthorizationDictionaryShape(authorization);
            Dictionary<string, object> task = ChildMap(authorization, "task");
            Dictionary<string, object> request = ChildMap(authorization, "request");
            Dictionary<string, object> plan = ChildMap(authorization, "plan");
            Dictionary<string, object> recipe = ChildMap(authorization, "recipe");
            Dictionary<string, object> tool = ChildMap(authorization, "tool");
            Dictionary<string, object> seed = ChildMap(authorization, "seed");
            Dictionary<string, object> execution = ChildMap(authorization, "execution");
            Dictionary<string, object> quality = ChildMap(authorization, "qualityBoundary");
            DateTime issued, expires, leaseExpires, completed;
            string[] expectedPhases = TextValue(receipt, "toolId") == "native_width_888_v1" ?
                new[] { "dimensions", "derived", "base-hole", "assemblies" } :
                new[] { phase };
            Require(ParseUtc(TextValue(authorization, "issuedAt"), out issued) &&
                ParseUtc(TextValue(authorization, "expiresAt"), out expires) &&
                ParseUtc(TextValue(task, "leaseExpiresAt"), out leaseExpires) &&
                ParseUtc(TextValue(receipt, "completedAt"), out completed) &&
                completed >= issued && completed <= expires && completed <= leaseExpires &&
                expires > issued && expires <= leaseExpires &&
                expires - issued <= TimeSpan.FromMinutes(30),
                "RECEIPT_HISTORICAL_AUTHORIZATION_TIME_INVALID", 11,
                "historical completion is outside authorization/lease: " + phase);
            Require(TextValue(authorization, "schema") == ExecutionAuthorizationSchema &&
                TextValue(authorization, "purpose") == ExecutionAuthorizationPurpose &&
                TextValue(authorization, "workerId") == result.plan_worker_id &&
                TextValue(authorization, "authorizationId") ==
                    ExpectedAuthorizationId(authorization) &&
                TextValue(receipt, "authorizationId") ==
                    TextValue(authorization, "authorizationId") &&
                TextValue(receipt, "authorizationIssuedAt") ==
                    TextValue(authorization, "issuedAt") &&
                TextValue(receipt, "authorizationExpiresAt") ==
                    TextValue(authorization, "expiresAt") &&
                TextValue(receipt, "leaseId") == TextValue(task, "leaseId") &&
                TextValue(receipt, "leaseExpiresAt") == TextValue(task, "leaseExpiresAt") &&
                TextValue(task, "id") == result.task_id &&
                NumberValue(task, "revision") == result.task_revision_at_planning &&
                string.Equals(TextValue(task, "digest"), result.task_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                TextValue(request, "fingerprint") == result.request_fingerprint &&
                string.Equals(TextValue(request, "digest"), result.request_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(plan, "sha256"), TextValue(receipt, "planSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                TextValue(recipe, "id") == ExpectedRecipeId &&
                NumberValue(recipe, "version") == 1 &&
                string.Equals(TextValue(recipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                TextValue(tool, "id") == TextValue(receipt, "toolId") &&
                string.Equals(TextValue(tool, "sourceNormalizedSha256"),
                    TextValue(receipt, "toolSourceNormalizedSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(tool, "executableSha256"),
                    TextValue(receipt, "toolExecutableSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(seed, "inventoryDigest"), preDigest,
                    StringComparison.OrdinalIgnoreCase) && BoolValue(execution, "authorized") &&
                NumberValue(execution, "attempt") == result.attempt_number &&
                StringArrayEquals(ArrayItems(execution, "phases"), expectedPhases) &&
                !BoolValue(quality, "engineeringAssistanceReady") &&
                BoolValue(quality, "readyOnlyAfterEveryRequiredCheckPasses"),
                "RECEIPT_HISTORICAL_AUTHORIZATION_BINDING_INVALID", 11,
                "receipt does not bind its historical authorization: " + phase);
        }

        private static void ValidateReceiptEvidencePair(Dictionary<string, object> receipt,
            string attemptRoot, string phase, int index)
        {
            string expectedRelative = index == 0 ? "evidence/seed_pack_888_native_v1.json" :
                "evidence/width-" + phase + ".json";
            Dictionary<string, object> evidence;
            Require(EvidencePairValid(attemptRoot, receipt, expectedRelative, out evidence),
                "RECEIPT_EVIDENCE_PAIR_INVALID", 11,
                "receipt evidence bytes/hash/commitment are invalid: " + phase);
            string completed = FirstNonEmpty(TextValue(evidence, "completedAtUtc"),
                TextValue(evidence, "completed_at_utc"), TextValue(evidence, "completedAt"),
                TextValue(evidence, "evidence_commit_completed_at_utc"));
            Require(BoolValue(evidence, "success") &&
                TextValue(receipt, "completedAt") == completed,
                "RECEIPT_EVIDENCE_COMPLETION_BINDING_INVALID", 11,
                "receipt completion differs from its live evidence: " + phase);
        }

        private static bool EvidencePairValid(string attemptRoot,
            Dictionary<string, object> receipt, string expectedRelative,
            out Dictionary<string, object> evidence)
        {
            evidence = null;
            try
            {
                string relative = TextValue(receipt, "evidencePath");
                if (relative != expectedRelative || !IsCanonicalReceiptRelativePath(relative))
                    return false;
                string path = Path.GetFullPath(Path.Combine(attemptRoot,
                    relative.Replace('/', '\\')));
                RequireSafeReadPath(path, attemptRoot, "RECEIPT_EVIDENCE_PATH_UNSAFE");
                string hashBefore = Sha256(path);
                if (!string.Equals(hashBefore, TextValue(receipt, "evidenceSha256"),
                        StringComparison.OrdinalIgnoreCase)) return false;
                evidence = ReadJsonObject(path);
                string commitment = TextValue(receipt, "evidenceCommitmentSha256");
                return IsSha256(commitment) &&
                    string.Equals(FirstNonEmpty(TextValue(evidence,
                            "evidenceCommitmentSha256"),
                        TextValue(evidence, "evidence_commitment_sha256")), commitment,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(EvidenceCommitmentDigest(evidence), commitment,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Sha256(path), hashBefore, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsCanonicalReceiptRelativePath(string value)
        {
            return value != null && value.IndexOf('\\') < 0 &&
                Regex.IsMatch(value, "^(receipts|evidence)/[A-Za-z0-9_.-]+\\.json$",
                    RegexOptions.CultureInvariant) &&
                value.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static void BindExecutionAuthorization(DoorRequest request, RunResult result)
        {
            string root = Path.GetFullPath(request.isolated_root);
            string expectedPath = Path.Combine(root, ExecutionAuthorizationRelativePath);
            string configuredPath = System.Environment.GetEnvironmentVariable(
                ExecutionAuthorizationPathEnvironmentVariable) ?? "";
            string configuredSha = NormalizeHash(System.Environment.GetEnvironmentVariable(
                ExecutionAuthorizationShaEnvironmentVariable));
            Require(Path.IsPathRooted(configuredPath) && PathsEqual(configuredPath, expectedPath),
                "EXECUTION_AUTHORIZATION_PATH_INVALID", 3,
                ExecutionAuthorizationPathEnvironmentVariable + " must exactly name " +
                ExecutionAuthorizationRelativePath);
            Require(File.Exists(expectedPath), "EXECUTION_AUTHORIZATION_MISSING", 3,
                "execution authorization is missing");
            RequireSafeReadPath(expectedPath, root, "EXECUTION_AUTHORIZATION_PATH_UNSAFE");
            Require(configuredSha.Length == 64 &&
                string.Equals(Sha256(expectedPath), configuredSha, StringComparison.OrdinalIgnoreCase),
                "EXECUTION_AUTHORIZATION_HASH_MISMATCH", 3,
                "execution authorization SHA-256 does not match the worker environment");
            ValidateAuthorizationJsonShape(expectedPath);
            ExecutionAuthorization authorization = ReadJson<ExecutionAuthorization>(expectedPath);
            DateTime issued, expires, leaseExpires;
            bool parsedIssued = DateTime.TryParse(authorization == null ? "" : authorization.issuedAt,
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out issued);
            bool parsedExpires = DateTime.TryParse(authorization == null ? "" : authorization.expiresAt,
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out expires);
            bool parsedLeaseExpires = DateTime.TryParse(authorization == null ||
                authorization.task == null ? "" : authorization.task.leaseExpiresAt,
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out leaseExpires);
            DateTime now = DateTime.UtcNow;
            int attempt = AttemptNumber(root);
            Require(authorization != null && string.Equals(authorization.schema,
                ExecutionAuthorizationSchema, StringComparison.Ordinal) &&
                Regex.IsMatch(authorization.authorizationId ?? "", "^native-auth-[a-f0-9]{32}$") &&
                string.Equals(authorization.authorizationId, ExpectedAuthorizationId(expectedPath),
                    StringComparison.Ordinal) &&
                string.Equals(authorization.purpose, ExecutionAuthorizationPurpose,
                    StringComparison.Ordinal) && parsedIssued && parsedExpires && parsedLeaseExpires &&
                issued <= now && now < expires && expires <= leaseExpires && now < leaseExpires &&
                authorization.task != null && string.Equals(authorization.task.id, request.task_id,
                    StringComparison.Ordinal) && authorization.task.revision > 0 &&
                authorization.task.revision == result.task_revision_at_planning &&
                string.Equals(NormalizeHash(authorization.task.digest), result.task_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(authorization.task.leaseId, request.lease_id, StringComparison.Ordinal) &&
                string.Equals(authorization.workerId, request.worker_id, StringComparison.Ordinal) &&
                string.Equals(authorization.workerId, result.plan_worker_id, StringComparison.Ordinal) &&
                authorization.plan != null && string.Equals(NormalizeHash(authorization.plan.sha256),
                    result.plan_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                authorization.recipe != null && string.Equals(authorization.recipe.id, result.recipe_id,
                    StringComparison.Ordinal) && authorization.recipe.version == 1 &&
                string.Equals(NormalizeHash(authorization.recipe.digest), result.recipe_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                authorization.request != null && string.Equals(NormalizeHash(authorization.request.fingerprint),
                    result.request_fingerprint, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeHash(authorization.request.digest), result.request_digest,
                    StringComparison.OrdinalIgnoreCase) && authorization.tool != null &&
                string.Equals(authorization.tool.id, DoorModulePhase, StringComparison.Ordinal) &&
                string.Equals(NormalizeHash(authorization.tool.sourceNormalizedSha256),
                    result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeHash(authorization.tool.executableSha256),
                    result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                authorization.seed != null && string.Equals(NormalizeHash(authorization.seed.inventoryDigest),
                    result.current_pre_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                authorization.execution != null && authorization.execution.authorized &&
                authorization.execution.attempt == attempt && authorization.execution.phases != null &&
                authorization.execution.phases.SequenceEqual(new[] { DoorModuleExecutionPhase }) &&
                authorization.qualityBoundary != null &&
                !authorization.qualityBoundary.engineeringAssistanceReady &&
                authorization.qualityBoundary.readyOnlyAfterEveryRequiredCheckPasses,
                "EXECUTION_AUTHORIZATION_CONTRACT_MISMATCH", 3,
                "authorization does not bind this task, plan, recipe, tool, seed set and phase");
            result.execution_authorization_path = expectedPath;
            result.execution_authorization_sha256 = configuredSha;
            result.authorization_id = authorization.authorizationId;
            result.authorization_json_base64 = Convert.ToBase64String(
                File.ReadAllBytes(expectedPath));
            result.authorization_issued_at_utc = authorization.issuedAt;
            result.execution_authorization_expires_at_utc = expires.ToUniversalTime().ToString("o",
                CultureInfo.InvariantCulture);
            result.task_lease_expires_at_utc = leaseExpires.ToUniversalTime().ToString("o",
                CultureInfo.InvariantCulture);
            result.plan_execution_authorized = true;
        }

        private static void ValidateAuthorizationJsonShape(string path)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as
                IDictionary<string, object>;
            ValidateAuthorizationDictionaryShape(root == null ? null :
                new Dictionary<string, object>(root, StringComparer.Ordinal));
        }

        private static void ValidateAuthorizationDictionaryShape(
            Dictionary<string, object> root)
        {
            RequireExactKeys(root, "authorization", "schema", "authorizationId", "issuedAt",
                "expiresAt", "purpose", "workerId", "task", "request", "plan", "recipe",
                "tool", "seed", "execution", "qualityBoundary");
            RequireExactKeys(ObjectMap(root, "task"), "authorization.task", "id", "revision",
                "digest", "leaseId", "leaseExpiresAt");
            RequireExactKeys(ObjectMap(root, "request"), "authorization.request", "fingerprint",
                "digest");
            RequireExactKeys(ObjectMap(root, "plan"), "authorization.plan", "sha256");
            RequireExactKeys(ObjectMap(root, "recipe"), "authorization.recipe", "id", "version",
                "digest");
            RequireExactKeys(ObjectMap(root, "tool"), "authorization.tool", "id",
                "sourceNormalizedSha256", "executableSha256");
            RequireExactKeys(ObjectMap(root, "seed"), "authorization.seed", "inventoryDigest");
            RequireExactKeys(ObjectMap(root, "execution"), "authorization.execution", "authorized",
                "attempt", "phases");
            RequireExactKeys(ObjectMap(root, "qualityBoundary"), "authorization.qualityBoundary",
                "engineeringAssistanceReady", "readyOnlyAfterEveryRequiredCheckPasses");
        }

        private static string ExpectedAuthorizationId(string path)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as
                IDictionary<string, object>;
            Require(root != null, "EXECUTION_AUTHORIZATION_JSON_SHAPE_INVALID", 3,
                "authorization root must be an object");
            var identity = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string key in new[] { "workerId", "task", "request", "plan", "recipe",
                "tool", "seed", "execution", "issuedAt", "expiresAt" })
            {
                object value = null;
                Require(root.TryGetValue(key, out value),
                    "EXECUTION_AUTHORIZATION_JSON_SHAPE_INVALID", 3,
                    "authorization identity field is missing: " + key);
                identity.Add(key, value);
            }
            string digest = Sha256Text(StableJson(identity));
            return "native-auth-" + digest.Substring(0, 32).ToLowerInvariant();
        }

        private static string ExpectedAuthorizationId(Dictionary<string, object> authorization)
        {
            var identity = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "workerId", authorization["workerId"] },
                { "task", authorization["task"] },
                { "request", authorization["request"] },
                { "plan", authorization["plan"] },
                { "recipe", authorization["recipe"] },
                { "tool", authorization["tool"] },
                { "seed", authorization["seed"] },
                { "execution", authorization["execution"] },
                { "issuedAt", authorization["issuedAt"] },
                { "expiresAt", authorization["expiresAt"] }
            };
            return "native-auth-" + Sha256Text(StableJson(identity)).Substring(0, 32)
                .ToLowerInvariant();
        }

        private static string CanonicalJson(object value)
        {
            return StableJson(value);
        }

        private static IDictionary<string, object> ObjectMap(IDictionary<string, object> parent,
            string key)
        {
            object value = null;
            Require(parent != null && parent.TryGetValue(key, out value) &&
                value is IDictionary<string, object>, "EXECUTION_AUTHORIZATION_JSON_SHAPE_INVALID", 3,
                key + " must be an object");
            return (IDictionary<string, object>)value;
        }

        private static void RequireExactKeys(IDictionary<string, object> value, string label,
            params string[] expected)
        {
            Require(value != null && new HashSet<string>(value.Keys, StringComparer.Ordinal).SetEquals(
                expected), "EXECUTION_AUTHORIZATION_JSON_SHAPE_INVALID", 3,
                label + " keys do not exactly match the unified authorization schema");
        }

        private static bool ExactKeys(IDictionary<string, object> value,
            params string[] expected)
        {
            return value != null &&
                new HashSet<string>(value.Keys, StringComparer.Ordinal).SetEquals(expected);
        }

        private static void RevalidateAuthorizationTimeWindow(DoorRequest request,
            RunResult result)
        {
            DateTime expires = DateTime.MinValue, leaseExpires = DateTime.MinValue;
            bool parsed = DateTime.TryParse(result.execution_authorization_expires_at_utc ?? "",
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out expires) &&
                DateTime.TryParse(result.task_lease_expires_at_utc ?? "",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out leaseExpires);
            DateTime now = DateTime.UtcNow;
            Require(parsed && now <= expires && now <= leaseExpires && expires <= leaseExpires,
                "EXECUTION_AUTHORIZATION_EXPIRED_BEFORE_EVIDENCE_COMMIT", 29,
                "authorization and task lease must remain valid through evidence commit");
            string path = Path.GetFullPath(result.execution_authorization_path ?? "");
            RequireSafeReadPath(path, request.isolated_root,
                "EXECUTION_AUTHORIZATION_PATH_UNSAFE");
            Require(string.Equals(Sha256(path), result.execution_authorization_sha256,
                StringComparison.OrdinalIgnoreCase), "EXECUTION_AUTHORIZATION_CHANGED", 29,
                "execution authorization changed before evidence commit");
            Dictionary<string, object> authorization = ReadJsonObject(path);
            Dictionary<string, object> task = ChildMap(authorization, "task");
            Dictionary<string, object> tool = ChildMap(authorization, "tool");
            Dictionary<string, object> seed = ChildMap(authorization, "seed");
            Dictionary<string, object> execution = ChildMap(authorization, "execution");
            Require(TextValue(authorization, "authorizationId") == result.authorization_id &&
                ExpectedAuthorizationId(authorization) == result.authorization_id &&
                TextValue(task, "id") == result.task_id &&
                TextValue(task, "leaseId") == result.lease_id &&
                TextValue(tool, "id") == DoorModulePhase &&
                string.Equals(TextValue(tool, "sourceNormalizedSha256"),
                    result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(tool, "executableSha256"),
                    result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(seed, "inventoryDigest"),
                    result.current_pre_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                BoolValue(execution, "authorized") &&
                NumberValue(execution, "attempt") == result.attempt_number &&
                StringArrayEquals(ArrayItems(execution, "phases"),
                    new[] { DoorModuleExecutionPhase }),
                "EXECUTION_AUTHORIZATION_CHANGED", 29,
                "execution authorization binding changed before commit");
        }

        private static int AttemptNumber(string root)
        {
            string leaf = new DirectoryInfo(Path.GetFullPath(root)).Name;
            int value = 0;
            Require(leaf.StartsWith("attempt-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(leaf.Substring(8), NumberStyles.None, CultureInfo.InvariantCulture,
                    out value) && value > 0, "EXECUTION_AUTHORIZATION_ATTEMPT_INVALID", 3,
                "attempt directory does not contain a positive attempt number");
            return value;
        }

        private static bool NoReparsePoints(string baseRoot, string target)
        {
            try
            {
                string current = Path.GetFullPath(baseRoot).TrimEnd('\\', '/');
                string fullTarget = Path.GetFullPath(target).TrimEnd('\\', '/');
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return false;
                string relative = fullTarget.Substring(current.Length).TrimStart('\\', '/');
                foreach (string segment in relative.Split(new[] { '\\', '/' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, segment);
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        return false;
                }
                return true;
            }
            catch { return false; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(IntPtr file, StringBuilder path,
            uint capacity, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint fileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME creationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME lastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME lastWriteTime;
            internal uint volumeSerialNumber;
            internal uint fileSizeHigh;
            internal uint fileSizeLow;
            internal uint numberOfLinks;
            internal uint fileIndexHigh;
            internal uint fileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(IntPtr file,
            out ByHandleFileInformation information);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string newFileName, string existingFileName,
            IntPtr securityAttributes);

        private static string GetFinalPathNameByHandle(string path)
        {
            const uint ShareAll = 0x00000007;
            const uint OpenExisting = 3;
            const uint BackupSemantics = 0x02000000;
            IntPtr handle = CreateFile(path, 0, ShareAll, IntPtr.Zero, OpenExisting,
                BackupSemantics, IntPtr.Zero);
            if (handle == new IntPtr(-1)) return "";
            try
            {
                var buffer = new StringBuilder(32768);
                uint count = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
                if (count == 0 || count >= buffer.Capacity) return "";
                string finalPath = buffer.ToString();
                return finalPath.StartsWith("\\\\?\\", StringComparison.Ordinal) ?
                    finalPath.Substring(4) : finalPath;
            }
            finally { CloseHandle(handle); }
        }

        private static uint GetHardLinkCount(string path)
        {
            const uint ShareAll = 0x00000007;
            const uint OpenExisting = 3;
            const uint BackupSemantics = 0x02000000;
            IntPtr handle = CreateFile(path, 0, ShareAll, IntPtr.Zero, OpenExisting,
                BackupSemantics, IntPtr.Zero);
            if (handle == new IntPtr(-1)) return 0;
            try
            {
                ByHandleFileInformation information;
                return GetFileInformationByHandle(handle, out information) ?
                    information.numberOfLinks : 0;
            }
            finally { CloseHandle(handle); }
        }

        private static void RequireSafeReadPath(string path, string root, string code)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root);
            Require(File.Exists(fullPath) || Directory.Exists(fullPath), code, 11,
                "path does not exist: " + fullPath);
            Require(NoReparsePoints(fullRoot, fullPath), code, 11,
                "path contains a reparse point: " + fullPath);
            string finalRoot = GetFinalPathNameByHandle(fullRoot);
            string finalPath = GetFinalPathNameByHandle(fullPath);
            Require(!string.IsNullOrWhiteSpace(finalRoot) && !string.IsNullOrWhiteSpace(finalPath) &&
                (PathsEqual(finalPath, finalRoot) || IsUnderRoot(finalPath, finalRoot)), code, 11,
                "path real location escapes isolated root: " + fullPath);
            if (File.Exists(fullPath))
                Require(GetHardLinkCount(fullPath) == 1, code, 11,
                    "file must have exactly one hard link: " + fullPath);
        }

        private static void RequireSafeMutationPath(string path, string root, string code)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root);
            Require(IsUnderRoot(fullPath, fullRoot) && !PathsEqual(fullPath, fullRoot), code, 11,
                "mutation target escapes isolated root: " + fullPath);
            string existing = fullPath;
            while (!File.Exists(existing) && !Directory.Exists(existing))
            {
                string parent = Path.GetDirectoryName(existing);
                Require(!string.IsNullOrWhiteSpace(parent) && !PathsEqual(parent, existing), code, 11,
                    "mutation target has no existing ancestor: " + fullPath);
                existing = parent;
            }
            RequireSafeReadPath(existing, fullRoot, code);
            Require(NoReparsePoints(fullRoot, existing), code, 11,
                "mutation ancestor contains a reparse point: " + existing);
        }

        private static void RequireSafeRecursiveDeleteTree(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            Require(Directory.Exists(fullPath), "ROLLBACK_TREE_MISSING", 29,
                "rollback tree does not exist");
            RequireSafeReadPath(fullPath, root, "ROLLBACK_PATH_UNSAFE");
            var pending = new Stack<string>();
            pending.Push(fullPath);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                RequireSafeReadPath(directory, root, "ROLLBACK_TREE_PATH_UNSAFE");
                foreach (string entry in Directory.GetFileSystemEntries(directory, "*",
                    SearchOption.TopDirectoryOnly))
                {
                    Require(IsUnderRoot(entry, fullPath), "ROLLBACK_TREE_ESCAPE", 29,
                        "rollback tree entry escaped output root");
                    FileAttributes attributes = File.GetAttributes(entry);
                    Require((attributes & FileAttributes.ReparsePoint) == 0,
                        "ROLLBACK_TREE_REPARSE_POINT", 29,
                        "rollback refuses a tree containing a reparse point: " + entry);
                    RequireSafeReadPath(entry, root, "ROLLBACK_TREE_PATH_UNSAFE");
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                }
            }
        }

        private static void RequireSafeTree(string path, string root, string code)
        {
            string fullPath = Path.GetFullPath(path);
            Require(Directory.Exists(fullPath), code, 29, "tree does not exist: " + fullPath);
            RequireSafeReadPath(fullPath, root, code);
            var pending = new Stack<string>();
            pending.Push(fullPath);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                foreach (string entry in Directory.GetFileSystemEntries(directory, "*",
                    SearchOption.TopDirectoryOnly))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    Require((attributes & FileAttributes.ReparsePoint) == 0, code, 29,
                        "tree contains a reparse point: " + entry);
                    RequireSafeReadPath(entry, root, code);
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                }
            }
        }

        private static void ValidatePanelProof(string proofPath, string handedness,
            string sourceHash, string root, RunResult result, bool requireAuthorizationBinding)
        {
            Require(!string.IsNullOrWhiteSpace(proofPath) && IsUnderRoot(proofPath, root) &&
                PathsEqual(proofPath, Path.Combine(root,
                    PanelProofRelativePath.Replace('/', '\\'))) && File.Exists(proofPath),
                "PANEL_PROOF_MISSING", 13,
                handedness + " native panel proof is missing or outside isolated_root");
            RequireSafeReadPath(proofPath, root, "PANEL_PROOF_PATH_UNSAFE");
            Dictionary<string, object> proofObject = ReadJsonObject(proofPath);
            string authorizationId = requireAuthorizationBinding ? result.authorization_id :
                TextValue(proofObject, "authorization_id");
            string authorizationSha = requireAuthorizationBinding ?
                result.execution_authorization_sha256 :
                TextValue(proofObject, "authorization_sha256");
            string toolSourceSha = requireAuthorizationBinding ?
                result.tool_source_normalized_sha256 :
                TextValue(proofObject, "tool_source_normalized_sha256");
            string toolExecutableSha = requireAuthorizationBinding ?
                result.tool_executable_sha256 :
                TextValue(proofObject, "tool_executable_sha256");
            Require(PanelProofDocumentValid(proofObject, root, result.task_id,
                    result.attempt_number, authorizationId, authorizationSha, sourceHash,
                    toolSourceSha, toolExecutableSha), "PANEL_PROOF_EXACT_CONTRACT_INVALID", 13,
                handedness + " panel proof does not match the exact immutable proof contract");
            SourceEvidence leftSource = result.sources.SingleOrDefault(delegate(
                SourceEvidence row) { return row.role == "left_panel_seed"; });
            Require(leftSource != null &&
                PathsEqual(TextValue(proofObject, "source_path"), leftSource.path) &&
                string.Equals(Sha256(leftSource.path), sourceHash,
                    StringComparison.OrdinalIgnoreCase), "PANEL_PROOF_SOURCE_PATH_MISMATCH", 13,
                "left panel proof is not bound to the request's isolated left seed");
            NativePanelProof proof = ReadJson<NativePanelProof>(proofPath);
            Require(proof != null && string.Equals(proof.schema, PanelProofSchema,
                StringComparison.Ordinal), "PANEL_PROOF_SCHEMA_MISMATCH", 13,
                handedness + " panel proof schema is invalid");
            Require(string.Equals(proof.handedness, handedness, StringComparison.Ordinal) &&
                string.Equals(proof.construction, "independent_native_" + handedness +
                    "_hand_seed", StringComparison.Ordinal),
                "PANEL_HANDEDNESS_PROOF_INVALID", 13,
                handedness + " panel proof does not establish an independent native hand seed");
            Require(string.Equals(NormalizeHash(proof.source_sha256), sourceHash,
                StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeHash(proof.reopen_sha256), sourceHash,
                StringComparison.OrdinalIgnoreCase), "PANEL_PROOF_HASH_MISMATCH", 13,
                handedness + " proof hashes do not match its source");
            Require(proof.solidworks_major == 28 && proof.body_count == 1 &&
                proof.has_sheet_metal && proof.has_flat_pattern &&
                proof.error_feature_count == 0 && proof.warning_feature_count == 0 &&
                proof.foreign_feature_count == 0 && proof.traversal_complete &&
                proof.external_reference_count == 0 && proof.read_only_reopen_verified,
                "PANEL_NATIVE_HEALTH_PROOF_INVALID", 13,
                handedness + " proof must show SolidWorks 2020, one body, SheetMetal, " +
                "FlatPattern, zero foreign features, zero feature issues, and read-only reopen");
            Require(string.Equals(proof.height_dimension, "D1@草图1", StringComparison.Ordinal) &&
                string.Equals(proof.width_dimension, "D2@草图1", StringComparison.Ordinal) &&
                proof.height_dimension_state ==
                    (int)swDimensionDrivenState_e.swDimensionDriving &&
                proof.width_dimension_state ==
                    (int)swDimensionDrivenState_e.swDimensionDriving,
                "PANEL_DIMENSION_SCHEMA_UNVERIFIED", 13,
                handedness + " proof must establish D1/D2@草图1 height/width roles " +
                "with SolidWorks driving state 2");
            Require(proof.evidence_files != null && proof.evidence_files.Count == 1,
                "PANEL_PROOF_EVIDENCE_EMPTY", 13,
                handedness + " proof requires at least one hashed audit evidence file");
            foreach (SourceSpec evidence in proof.evidence_files)
                ValidateEvidenceFile(evidence, root, handedness);
            if (!requireAuthorizationBinding)
                result.panel_proofs.Add(new PanelProofEvidence
                {
                    handedness = handedness,
                    path = Path.GetFullPath(proofPath),
                    sha256 = Sha256(proofPath),
                    source_sha256 = sourceHash,
                    evidence_count = proof.evidence_files.Count,
                    gate = true
                });
        }

        private static void ValidateEvidenceFile(SourceSpec spec, string root, string handedness)
        {
            Require(spec != null && !string.IsNullOrWhiteSpace(spec.path) &&
                IsUnderRoot(spec.path, root) && File.Exists(spec.path),
                "PANEL_PROOF_EVIDENCE_MISSING", 13,
                handedness + " proof evidence is missing or outside isolated_root");
            RequireSafeReadPath(spec.path, root, "PANEL_PROOF_PATH_UNSAFE");
            Require(string.Equals(NormalizeHash(spec.sha256), Sha256(spec.path),
                StringComparison.OrdinalIgnoreCase), "PANEL_PROOF_EVIDENCE_HASH_MISMATCH", 13,
                handedness + " proof evidence SHA-256 does not match");
        }

        private static void Execute(DoorRequest request, RunResult result)
        {
            string root = Path.GetFullPath(request.isolated_root);
            string output = Path.GetFullPath(request.output_dir);
            RequireSafeMutationPath(output, root, "OUTPUT_PATH_UNSAFE");
            Require(!Directory.Exists(output) && !File.Exists(output), "OUTPUT_ALREADY_EXISTS", 20,
                "output_dir must not already exist; overwrite is forbidden");

            string runId = request.task_id + "-" + DateTime.UtcNow.ToString(
                "yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            string transactionRoot = Path.Combine(root, "transactions", SafeLeaf(runId));
            string backupRoot = Path.Combine(transactionRoot, "source_backups");
            string stagingRoot = Path.Combine(transactionRoot, "candidate");
            string relocationRoot = Path.Combine(transactionRoot, "relocation_probe");
            RequireSafeMutationPath(transactionRoot, root, "TRANSACTION_PATH_UNSAFE");
            RequireSafeMutationPath(backupRoot, root, "BACKUP_PATH_UNSAFE");
            RequireSafeMutationPath(stagingRoot, root, "STAGING_PATH_UNSAFE");
            RequireSafeMutationPath(relocationRoot, root, "RELOCATION_PATH_UNSAFE");
            Directory.CreateDirectory(backupRoot);
            Directory.CreateDirectory(stagingRoot);
            result.transaction_root = transactionRoot;
            result.staging_root = stagingRoot;
            result.relocation_probe_root = relocationRoot;

            bool mirrorRight = IsMirrorRightStrategy(request);
            List<SourceRow> sourceRows = SourceRows(request.sources, mirrorRight);
            foreach (SourceRow row in sourceRows)
            {
                string backup = Path.Combine(backupRoot, SafeLeaf(row.role) +
                    Path.GetExtension(row.spec.path));
                RequireSafeMutationPath(backup, root, "BACKUP_PATH_UNSAFE");
                File.Copy(row.spec.path, backup, false);
                result.backups.Add(new BackupEvidence
                {
                    role = row.role,
                    source_path = Path.GetFullPath(row.spec.path),
                    backup_path = backup,
                    source_sha256 = Sha256(row.spec.path),
                    backup_sha256 = Sha256(backup)
                });
            }
            Require(result.backups.All(delegate(BackupEvidence row)
            {
                return string.Equals(row.source_sha256, row.backup_sha256,
                    StringComparison.OrdinalIgnoreCase);
            }), "SOURCE_BACKUP_FAILED", 20, "source backup hash gate failed");

            BuildPaths paths = StageNativeParts(request.sources, stagingRoot, root, mirrorRight);
            OwnedSession buildSession = null;
            OwnedSession reopenSession = null;
            OwnedSession relocationSession = null;
            OwnedSession committedSession = null;
            try
            {
                buildSession = StartOwnedSession(request, "build", result);
                ConfigurePanel(buildSession.sw, paths.left_panel, "left", result);
                if (mirrorRight)
                    CreateNativeRightMirrorPart(buildSession.sw, paths.left_panel,
                        paths.right_panel, result);
                else
                    ConfigurePanel(buildSession.sw, paths.right_panel, "right", result);
                paths.left_panel_sha256 = Sha256(paths.left_panel);
                paths.right_panel_sha256 = Sha256(paths.right_panel);
                Require(!string.Equals(paths.left_panel_sha256, paths.right_panel_sha256,
                    StringComparison.OrdinalIgnoreCase), "LEFT_RIGHT_OUTPUT_HASH_COLLISION", 21,
                    "left and right panel outputs must have different SHA-256 hashes");
                ConfigureStiffener(buildSession.sw, paths.stiffener, result);
                paths.left_weld = BuildWeldAssembly(buildSession.sw, stagingRoot, paths,
                    "left", result);
                paths.right_weld = BuildWeldAssembly(buildSession.sw, stagingRoot, paths,
                    "right", result);
                paths.left_door = BuildOrdinaryDoorAssembly(buildSession.sw, stagingRoot, paths,
                    "left", result);
                paths.right_door = BuildOrdinaryDoorAssembly(buildSession.sw, stagingRoot, paths,
                    "right", result);
                StopOwnedSession(buildSession, result);
                buildSession = null;

                reopenSession = StartOwnedSession(request, "read_only_reopen", result);
                VerifyCandidate(reopenSession.sw, stagingRoot, paths, result,
                    "read_only_reopen", mirrorRight);
                StopOwnedSession(reopenSession, result);
                reopenSession = null;

                RequireSafeTree(stagingRoot, root, "STAGING_TREE_UNSAFE");
                CopyTree(stagingRoot, relocationRoot);
                RequireSafeTree(relocationRoot, root, "RELOCATION_TREE_UNSAFE");
                BuildPaths relocated = paths.Relocated(stagingRoot, relocationRoot);
                relocationSession = StartOwnedSession(request, "relocation_reopen", result);
                VerifyCandidate(relocationSession.sw, relocationRoot, relocated, result,
                    "relocation_reopen", mirrorRight);
                StopOwnedSession(relocationSession, result);
                relocationSession = null;
                result.relocation_reopen_passed = true;

                Directory.CreateDirectory(Path.GetDirectoryName(output));
                RequireSafeTree(stagingRoot, root, "STAGING_TREE_UNSAFE");
                RequireSafeMutationPath(output, root, "OUTPUT_PATH_UNSAFE");
                Directory.Move(stagingRoot, output);
                RequireSafeTree(output, root, "COMMITTED_OUTPUT_TREE_UNSAFE");
                BuildPaths committed = paths.Relocated(stagingRoot, output);
                committedSession = StartOwnedSession(request, "committed_reopen", result);
                VerifyCandidate(committedSession.sw, output, committed, result,
                    "committed_reopen", mirrorRight);
                StopOwnedSession(committedSession, result);
                committedSession = null;
                result.committed_output_dir = output;
                result.committed_reopen_passed = true;
                List<FileHashEvidence> cadOutput = HashTree(output);
                WriteFlatImportManifest(output, cadOutput, result);
                RequireSafeTree(output, root, "COMMITTED_OUTPUT_TREE_UNSAFE");
                result.output_files.AddRange(HashTree(output));
                Require(VerifyExactOutputInventory(output, result.output_files),
                    "OUTPUT_CAD_INVENTORY_MISMATCH", 29,
                    "output must contain exactly the 13 approved native CAD relative paths");

                foreach (SourceRow row in sourceRows)
                {
                    string actual = Sha256(row.spec.path);
                    Require(string.Equals(actual, NormalizeHash(row.spec.sha256),
                        StringComparison.OrdinalIgnoreCase), "SOURCE_MUTATED", 29,
                        row.role + " source hash changed during execution");
                }
                Require(ProcessIds("SLDWORKS").Count == 0 &&
                    ProcessIds("sldProcMon").Count == 0, "CAD_RESIDUAL_PROCESS", 29,
                    "SolidWorks or sldProcMon remained after the final session");
            }
            finally
            {
                if (buildSession != null) StopOwnedSession(buildSession, result);
                if (reopenSession != null) StopOwnedSession(reopenSession, result);
                if (relocationSession != null) StopOwnedSession(relocationSession, result);
                if (committedSession != null) StopOwnedSession(committedSession, result);
            }
        }

        private static BuildPaths StageNativeParts(SourceSet sources, string stagingRoot,
            string isolatedRoot, bool mirrorRight)
        {
            string partRoot = Path.Combine(stagingRoot, "parts");
            RequireSafeMutationPath(partRoot, isolatedRoot, "PART_STAGING_PATH_UNSAFE");
            Directory.CreateDirectory(partRoot);
            var paths = new BuildPaths
            {
                left_panel = CopyNativePart(sources.left_panel_seed.path, partRoot, isolatedRoot,
                    "door_panel_left_W381_H254p428571.SLDPRT"),
                right_panel = mirrorRight ? Path.Combine(partRoot,
                    "door_panel_right_W381_H254p428571.SLDPRT") :
                    CopyNativePart(sources.right_panel_seed.path, partRoot, isolatedRoot,
                        "door_panel_right_W381_H254p428571.SLDPRT"),
                stiffener = CopyNativePart(sources.stiffener_seed.path, partRoot, isolatedRoot,
                    "door_stiffener_H243p928571.SLDPRT"),
                latch_plate = CopyNativePart(sources.latch_plate.path, partRoot, isolatedRoot,
                    "hinge_latch_plate.SLDPRT"),
                hook_pad = CopyNativePart(sources.hook_pad.path, partRoot, isolatedRoot,
                    "u_hook_pad.SLDPRT"),
                bushing = CopyNativePart(sources.bushing.path, partRoot, isolatedRoot,
                    "plastic_bushing.SLDPRT"),
                hinge_pin = CopyNativePart(sources.hinge_pin.path, partRoot, isolatedRoot,
                    "door_hinge_pin.SLDPRT"),
                circlip = CopyNativePart(sources.circlip.path, partRoot, isolatedRoot,
                    "circlip.SLDPRT"),
                lock_tongue = CopyNativePart(sources.mechanical_lock_tongue.path, partRoot, isolatedRoot,
                    "mechanical_lock_tongue.SLDPRT")
            };
            return paths;
        }

        private static string CopyNativePart(string source, string partRoot, string isolatedRoot,
            string leaf)
        {
            string target = Path.Combine(partRoot, leaf);
            RequireSafeMutationPath(target, isolatedRoot, "PART_COPY_PATH_UNSAFE");
            File.Copy(source, target, false);
            return target;
        }

        private static void ConfigurePanel(ISldWorks sw, string path, string handedness,
            RunResult result)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, false,
                    ref errors, ref warnings);
                Require(model != null && errors == 0 && warnings == 0,
                    "PANEL_OPEN_FAILED", 21, handedness + " panel open failed");
                PartHealth before = CapturePartHealth(model);
                Require(NativeSheetMetalGate(before), "PANEL_SOURCE_HEALTH_FAILED", 21,
                    handedness + " panel copy is not healthy native sheet metal");
                SetDrivingDimension(model, "D1@草图1", TargetDoorHeightMm);
                SetDrivingDimension(model, "D2@草图1", TargetDoorWidthMm);
                bool rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                PartHealth after = CapturePartHealth(model);
                Require(rebuilt && NativeSheetMetalGate(after) &&
                    PartBboxGate(after, TargetDoorWidthMm, TargetDoorHeightMm),
                    "PANEL_REBUILD_HEALTH_FAILED", 21,
                    handedness + " panel failed native feature/bbox gate after dimension edit");
                SaveModel(model, handedness + " panel");
                result.part_edits.Add(new PartEditEvidence
                {
                    role = handedness + "_panel",
                    path = path,
                    target_width_mm = TargetDoorWidthMm,
                    target_height_mm = TargetDoorHeightMm,
                    before = before,
                    after = after,
                    sha256_after_save = Sha256(path)
                });
            }
            finally { CloseDocument(sw, ref model); }
        }

        private static void CreateNativeRightMirrorPart(ISldWorks sw, string leftPath,
            string rightPath, RunResult result)
        {
            ModelDoc2 leftModel = null;
            ModelDoc2 rightModel = null;
            Feature mirrorFeature = null;
            MirrorPartFeatureData mirrorData = null;
            try
            {
                Require(!File.Exists(rightPath), "MIRROR_RIGHT_TARGET_EXISTS", 21,
                    "MirrorPart2 right target must not already exist");
                int errors = 0, warnings = 0;
                leftModel = OpenDocument(sw, leftPath, (int)swDocumentTypes_e.swDocPART, true,
                    ref errors, ref warnings);
                Require(leftModel != null && errors == 0 && warnings == 0,
                    "MIRROR_LEFT_OPEN_FAILED", 21,
                    "configured native left panel failed read-only open for MirrorPart2");
                PartHealth leftHealth = CapturePartHealth(leftModel);
                Require(NativeSheetMetalGate(leftHealth) &&
                    PartBboxGate(leftHealth, TargetDoorWidthMm, TargetDoorHeightMm) &&
                    ExternalReferenceCount(leftModel) == 0,
                    "MIRROR_LEFT_SOURCE_HEALTH_FAILED", 21,
                    "MirrorPart2 left input must be one healthy link-free native sheet-metal body");

                leftModel.ClearSelection2(true);
                bool planeSelected = SelectRightPlane(leftModel);
                Require(planeSelected, "MIRROR_PLANE_SELECTION_FAILED", 21,
                    "could not select the native Right plane for MirrorPart2");
                PartDoc leftPart = leftModel as PartDoc;
                Require(leftPart != null, "MIRROR_LEFT_TYPE_INVALID", 21,
                    "left input is not a PartDoc");
                int options = (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSolids |
                    (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSMInfo |
                    (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportIndProps |
                    (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportCutListProperties;
                mirrorFeature = leftPart.MirrorPart2(true, options, out rightModel);
                Require(mirrorFeature != null && rightModel != null,
                    "MIRRORPART2_CREATE_FAILED", 21,
                    "IPartDoc.MirrorPart2 did not create a right-hand part");

                mirrorData = mirrorFeature.GetDefinition() as MirrorPartFeatureData;
                Require(mirrorData != null, "MIRROR_FEATURE_DATA_MISSING", 21,
                    "MirrorPart2 did not expose MirrorPartFeatureData");
                bool selectionsAccessed = mirrorData.AccessSelections(rightModel, null);
                bool sheetMetalInformation = selectionsAccessed && mirrorData.SheetMetalInformation;
                if (selectionsAccessed) mirrorData.ReleaseSelectionAccess();
                Require(sheetMetalInformation, "MIRROR_SHEET_METAL_INFO_NOT_IMPORTED", 21,
                    "MirrorPart2 must preserve sheet-metal information");

                bool rebuilt = Safe(delegate { return rightModel.ForceRebuild3(false); }, false);
                int externalReferenceCount = ExternalReferenceCount(rightModel);
                PartHealth rightHealth = CapturePartHealth(rightModel);
                Require(rebuilt && externalReferenceCount == 0 &&
                    NativeMirrorSheetMetalGate(rightHealth) &&
                    PartBboxGate(rightHealth, TargetDoorWidthMm, TargetDoorHeightMm),
                    "MIRROR_RIGHT_HEALTH_FAILED", 21,
                    "right part must be link-free MirrorPart/MirrorStock sheet metal with one body, " +
                    "SheetMetal, FlatPattern, target bbox, and zero feature issues");

                int saveErrors = 0, saveWarnings = 0;
                bool saved = rightModel.Extension.SaveAs(rightPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null,
                    ref saveErrors, ref saveWarnings);
                Require(saved && saveErrors == 0 && saveWarnings == 0 && File.Exists(rightPath) &&
                    !Safe(delegate { return rightModel.GetSaveFlag(); }, true),
                    "MIRROR_RIGHT_SAVE_FAILED", 21,
                    "MirrorPart2 right part did not save cleanly to the isolated target");
                Require(ExternalReferenceCount(rightModel) == 0,
                    "MIRROR_RIGHT_EXTERNAL_LINK_REAPPEARED", 21,
                    "right part acquired an external reference after save");

                result.mirror_part = new MirrorPartEvidence
                {
                    source_path = leftPath,
                    output_path = rightPath,
                    api = "IPartDoc.MirrorPart2",
                    break_link = true,
                    options = options,
                    import_solids = true,
                    import_sheet_metal_information = true,
                    import_individual_properties = true,
                    import_cut_list_properties = true,
                    selected_plane = "Right",
                    mirror_feature_type = Safe(delegate
                    {
                        return mirrorFeature.GetTypeName2();
                    }, ""),
                    external_reference_count = ExternalReferenceCount(rightModel),
                    after = rightHealth,
                    sha256_after_save = Sha256(rightPath),
                    gate = true
                };
                result.part_edits.Add(new PartEditEvidence
                {
                    role = "right_panel_mirror_part",
                    path = rightPath,
                    target_width_mm = TargetDoorWidthMm,
                    target_height_mm = TargetDoorHeightMm,
                    before = leftHealth,
                    after = rightHealth,
                    sha256_after_save = Sha256(rightPath)
                });
            }
            finally
            {
                Release(mirrorData);
                Release(mirrorFeature);
                CloseDocument(sw, ref rightModel);
                CloseDocument(sw, ref leftModel);
            }
        }

        private static bool SelectRightPlane(ModelDoc2 model)
        {
            string[] names = new[] { "右视", "Right Plane", "Right" };
            foreach (string name in names)
            {
                bool selected = Safe(delegate
                {
                    return model.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0,
                        null, 0);
                }, false);
                if (selected) return true;
            }
            return false;
        }

        private static void ConfigureStiffener(ISldWorks sw, string path, RunResult result)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, false,
                    ref errors, ref warnings);
                Require(model != null && errors == 0 && warnings == 0,
                    "STIFFENER_OPEN_FAILED", 22, "stiffener open failed");
                PartHealth before = CapturePartHealth(model);
                Require(NativeSheetMetalGate(before), "STIFFENER_SOURCE_HEALTH_FAILED", 22,
                    "stiffener copy is not healthy native sheet metal");
                SetDrivingDimension(model, "D2@草图1", TargetStiffenerLengthMm);
                bool rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                PartHealth after = CapturePartHealth(model);
                Require(rebuilt && NativeSheetMetalGate(after) && StiffenerBboxGate(after),
                    "STIFFENER_REBUILD_HEALTH_FAILED", 22,
                    "stiffener failed native feature or solid-body length gate after dimension edit");
                SaveModel(model, "stiffener");
                result.part_edits.Add(new PartEditEvidence
                {
                    role = "stiffener",
                    path = path,
                    target_width_mm = 0,
                    target_height_mm = TargetStiffenerLengthMm,
                    before = before,
                    after = after,
                    sha256_after_save = Sha256(path)
                });
            }
            finally { CloseDocument(sw, ref model); }
        }

        private static string BuildWeldAssembly(ISldWorks sw, string root, BuildPaths paths,
            string handedness, RunResult result)
        {
            string asmRoot = Path.Combine(root, "assemblies");
            Directory.CreateDirectory(asmRoot);
            string outPath = Path.Combine(asmRoot, "door_weld_" + handedness +
                "_W381_H254p428571.SLDASM");
            double side = handedness == "left" ? 1.0 : -1.0;
            double hingeX = -(TargetDoorWidthMm / 2.0 - 10.0) * side;
            double lockX = (TargetDoorWidthMm / 2.0 - 15.0) * side;
            double latchY = TargetDoorHeightMm / 2.0 - 28.8;
            double[] handRotation = handedness == "left" ? Identity() : RotateY180();
            var placements = new List<Placement>
            {
                new Placement("door_panel_" + handedness,
                    handedness == "left" ? paths.left_panel : paths.right_panel,
                    Identity(), 0, 0, 0),
                new Placement("door_stiffener", paths.stiffener,
                    Multiply(handRotation, RotateY180()), 0, 0, -0.8),
                new Placement("hinge_latch_plate_bottom", paths.latch_plate,
                    Multiply(handRotation, Multiply(RotateY180(), RotateXMinus90())),
                    hingeX, -latchY, -0.8),
                new Placement("hinge_latch_plate_top", paths.latch_plate,
                    Multiply(handRotation, Multiply(RotateY180(), RotateX90())),
                    hingeX, latchY, -0.8),
                new Placement("u_hook_pad", paths.hook_pad,
                    Multiply(handRotation, RotateY180()), lockX, 0, -10.5)
            };
            BuildAssembly(sw, outPath, placements, 5, result, "weld_" + handedness);
            return outPath;
        }

        private static string BuildOrdinaryDoorAssembly(ISldWorks sw, string root,
            BuildPaths paths, string handedness, RunResult result)
        {
            string asmRoot = Path.Combine(root, "assemblies");
            Directory.CreateDirectory(asmRoot);
            string outPath = Path.Combine(asmRoot, "ordinary_door_" + handedness +
                "_W381_H254p428571.SLDASM");
            double side = handedness == "left" ? 1.0 : -1.0;
            double hingeX = -(TargetDoorWidthMm / 2.0 - 10.0) * side;
            double lockX = (TargetDoorWidthMm / 2.0 - 15.0) * side;
            double half = TargetDoorHeightMm / 2.0;
            double[] handRotation = handedness == "left" ? Identity() : RotateY180();
            var placements = new List<Placement>
            {
                new Placement("door_weld_" + handedness,
                    handedness == "left" ? paths.left_weld : paths.right_weld,
                    Identity(), 0, 0, 0),
                new Placement("plastic_bushing_top", paths.bushing,
                    Multiply(handRotation, RotateX180()), hingeX, half + 1.5, -7.0),
                new Placement("plastic_bushing_bottom", paths.bushing,
                    handRotation, hingeX, -half - 1.5, -7.0),
                new Placement("door_hinge_pin", paths.hinge_pin,
                    handRotation, hingeX, -half - 9.7, -7.0),
                new Placement("circlip_top", paths.circlip,
                    handRotation, hingeX, half - 29.7, -7.7236),
                new Placement("circlip_bottom", paths.circlip,
                    handRotation, hingeX, -half + 29.2, -7.7236),
                new Placement("mechanical_lock_tongue", paths.lock_tongue,
                    Multiply(handRotation, RotateY180()), lockX, 0, -11.3)
            };
            BuildAssembly(sw, outPath, placements, 7, result, "ordinary_" + handedness);
            return outPath;
        }

        private static void BuildAssembly(ISldWorks sw, string outPath,
            List<Placement> placements, int expectedCount, RunResult result, string role)
        {
            ModelDoc2 model = null;
            try
            {
                model = sw.NewAssembly() as ModelDoc2;
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(model != null && assembly != null, "NEW_ASSEMBLY_FAILED", 23,
                    role + " could not create a new assembly");
                MathUtility math = sw.GetMathUtility() as MathUtility;
                Require(math != null, "MATH_UTILITY_FAILED", 23,
                    role + " could not acquire MathUtility");
                var rows = new List<PlacementEvidence>();
                foreach (Placement placement in placements)
                    rows.Add(AddComponent(sw, model, assembly, math, placement));
                Require(rows.Count == expectedCount && rows.All(delegate(PlacementEvidence row)
                {
                    return row.opened && row.added && row.transform_applied &&
                        Near(MatrixDeterminant(row.rotation), 1.0, 1e-9);
                }), "ASSEMBLY_PLACEMENT_FAILED", 23,
                    role + " placement or proper-rotation gate failed");
                bool rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                Require(rebuilt, "ASSEMBLY_REBUILD_FAILED", 23,
                    role + " rebuild failed");
                Require(VerifyAssemblyPlacements(CaptureTopLevelComponents(assembly), rows,
                    Directory.GetParent(Path.GetDirectoryName(outPath)).FullName),
                    "ASSEMBLY_TRANSFORM_READBACK_FAILED", 23,
                    role + " component paths or transforms changed after rebuild");
                int saveErrors = 0, saveWarnings = 0;
                bool saved = Safe(delegate
                {
                    return model.Extension.SaveAs(outPath,
                        (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                        (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null,
                        ref saveErrors, ref saveWarnings);
                }, false);
                Require(saved && saveErrors == 0 && saveWarnings == 0 &&
                    File.Exists(outPath) && !Safe(delegate { return model.GetSaveFlag(); }, true),
                    "ASSEMBLY_SAVE_FAILED", 23, role + " save failed or remained dirty");
                result.assemblies.Add(new AssemblyEvidence
                {
                    role = role,
                    path = outPath,
                    expected_component_count = expectedCount,
                    rebuilt = rebuilt,
                    saved = saved,
                    sha256_after_save = Sha256(outPath),
                    placements = rows
                });
                Release(math);
            }
            finally { CloseDocument(sw, ref model); }
        }

        private static PlacementEvidence AddComponent(ISldWorks sw, ModelDoc2 assemblyModel,
            AssemblyDoc assembly, MathUtility math, Placement placement)
        {
            var row = new PlacementEvidence
            {
                role = placement.role,
                path = placement.path,
                sha256 = Sha256(placement.path),
                rotation = placement.rotation,
                tx_mm = placement.tx_mm,
                ty_mm = placement.ty_mm,
                tz_mm = placement.tz_mm
            };
            int errors = 0, warnings = 0;
            int documentType = string.Equals(Path.GetExtension(placement.path), ".SLDASM",
                StringComparison.OrdinalIgnoreCase)
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART;
            ModelDoc2 componentDocument = OpenDocument(sw, placement.path, documentType, true,
                ref errors, ref warnings);
            row.opened = componentDocument != null && errors == 0 && warnings == 0;
            Require(row.opened, "ASSEMBLY_COMPONENT_OPEN_FAILED", 23,
                placement.role + " failed read-only open");
            Try(delegate { sw.ActivateDoc2(assemblyModel.GetTitle(), false, ref errors); });
            Component2 component = assembly.AddComponent5(placement.path,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "", false, "", placement.tx_mm / 1000.0, placement.ty_mm / 1000.0,
                placement.tz_mm / 1000.0) as Component2;
            row.added = component != null;
            Require(component != null, "ASSEMBLY_COMPONENT_ADD_FAILED", 23,
                placement.role + " add failed");
            Try(delegate { component.Name2 = placement.role; });
            row.component_name = Safe(delegate { return component.Name2; }, "");
            Require(ComponentRoleMatches(row.component_name, placement.role),
                "ASSEMBLY_COMPONENT_NAME_FAILED", 23,
                placement.role + " component role name did not persist");
            MathTransform transform = math.CreateTransform(TransformArray(placement)) as MathTransform;
            Require(transform != null, "ASSEMBLY_TRANSFORM_CREATE_FAILED", 23,
                placement.role + " transform create failed");
            row.transform_applied = Safe(delegate
            {
                return component.SetTransformAndSolve2(transform);
            }, false);
            Release(transform);
            Release(component);
            CloseDocument(sw, ref componentDocument);
            return row;
        }

        private static void VerifyCandidate(ISldWorks sw, string root, BuildPaths paths,
            RunResult result, string phase, bool mirrorRight)
        {
            VerifyPanelReadOnly(sw, paths.left_panel, "left", false,
                paths.left_panel_sha256, result, phase);
            VerifyPanelReadOnly(sw, paths.right_panel, "right", mirrorRight,
                paths.right_panel_sha256, result, phase);
            VerifyStiffenerReadOnly(sw, paths.stiffener, result, phase);
            VerifyAssemblyReadOnly(sw, paths.left_weld, root, 5, "weld_left", result, phase);
            VerifyAssemblyReadOnly(sw, paths.right_weld, root, 5, "weld_right", result, phase);
            VerifyAssemblyReadOnly(sw, paths.left_door, root, 7, "ordinary_left", result, phase);
            VerifyAssemblyReadOnly(sw, paths.right_door, root, 7, "ordinary_right", result, phase);
        }

        private static void VerifyPanelReadOnly(ISldWorks sw, string path, string handedness,
            bool mirrorDerived, string expectedHash, RunResult result, string phase)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, true,
                    ref errors, ref warnings);
                Require(model != null && errors == 0 && warnings == 0,
                    "PANEL_READONLY_REOPEN_FAILED", 24,
                    phase + " " + handedness + " panel open failed");
                bool rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                PartHealth health = CapturePartHealth(model);
                string reopenedHash = Sha256(path);
                bool featureGate = mirrorDerived ? NativeMirrorSheetMetalGate(health) :
                    NativeSheetMetalGate(health);
                bool dimensionGate = mirrorDerived ||
                    (DimensionGate(model, "D1@草图1", TargetDoorHeightMm) &&
                    DimensionGate(model, "D2@草图1", TargetDoorWidthMm));
                Require(rebuilt && featureGate &&
                    PartBboxGate(health, TargetDoorWidthMm, TargetDoorHeightMm) &&
                    ExternalReferenceCount(model) == 0 && dimensionGate &&
                    string.Equals(reopenedHash, expectedHash,
                        StringComparison.OrdinalIgnoreCase),
                    "PANEL_READONLY_REOPEN_HEALTH_FAILED", 24,
                    phase + " " + handedness +
                    " panel native/bbox/link/hash/reopen gate failed");
                result.reopen_checks.Add(new ReopenEvidence
                {
                    phase = phase,
                    role = handedness + "_panel",
                    path = path,
                    sha256 = reopenedHash,
                    reference_count = 0,
                    gate = true
                });
            }
            finally { CloseDocument(sw, ref model); }
        }

        private static void VerifyStiffenerReadOnly(ISldWorks sw, string path, RunResult result,
            string phase)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocPART, true,
                    ref errors, ref warnings);
                Require(model != null && errors == 0 && warnings == 0,
                    "STIFFENER_READONLY_REOPEN_FAILED", 24,
                    phase + " stiffener open failed");
                bool rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                PartHealth health = CapturePartHealth(model);
                Require(rebuilt && NativeSheetMetalGate(health) &&
                    DimensionGate(model, "D2@草图1", TargetStiffenerLengthMm) &&
                    StiffenerBboxGate(health),
                    "STIFFENER_READONLY_REOPEN_HEALTH_FAILED", 24,
                    phase + " stiffener native/dimension/solid-body length gate failed");
                result.reopen_checks.Add(new ReopenEvidence
                {
                    phase = phase,
                    role = "stiffener",
                    path = path,
                    sha256 = Sha256(path),
                    reference_count = 0,
                    gate = true
                });
            }
            finally { CloseDocument(sw, ref model); }
        }

        private static void VerifyAssemblyReadOnly(ISldWorks sw, string path, string root,
            int expectedCount, string role, RunResult result, string phase)
        {
            ModelDoc2 model = null;
            try
            {
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, path, (int)swDocumentTypes_e.swDocASSEMBLY, true,
                    ref errors, ref warnings);
                Require(model != null && errors == 0 && warnings == 0,
                    "ASSEMBLY_READONLY_REOPEN_FAILED", 25,
                    phase + " assembly open failed: " + path);
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "ASSEMBLY_READONLY_TYPE_INVALID", 25,
                    phase + " did not open as AssemblyDoc");
                Try(delegate { assembly.ResolveAllLightWeightComponents(false); });
                bool rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                object raw = Safe(delegate { return assembly.GetComponents(true); }, null);
                Array components = raw as Array;
                int count = components == null ? 0 : components.Length;
                AssemblyEvidence expected = result.assemblies.SingleOrDefault(
                    delegate(AssemblyEvidence row) { return row.role == role; });
                Require(rebuilt && count == expectedCount,
                    "ASSEMBLY_COMPONENT_COUNT_MISMATCH", 25,
                    phase + " expected " + expectedCount.ToString(CultureInfo.InvariantCulture) +
                    " top-level components");
                Require(expected != null && expected.placements != null &&
                    expected.placements.Count == expectedCount &&
                    VerifyAssemblyPlacements(components, expected.placements, root),
                    "ASSEMBLY_TRANSFORM_REOPEN_MISMATCH", 25,
                    phase + " component paths or transforms do not match saved evidence");
                result.reopen_checks.Add(new ReopenEvidence
                {
                    phase = phase,
                    role = Path.GetFileNameWithoutExtension(path),
                    path = path,
                    sha256 = Sha256(path),
                    reference_count = count,
                    transform_gate = true,
                    gate = true
                });
            }
            finally { CloseDocument(sw, ref model); }
        }

        private static Array CaptureTopLevelComponents(AssemblyDoc assembly)
        {
            return assembly == null ? null : Safe(delegate
            {
                return assembly.GetComponents(true) as Array;
            }, null);
        }

        private static bool VerifyAssemblyPlacements(Array raw,
            List<PlacementEvidence> expectedRows, string allowedRoot)
        {
            if (expectedRows == null) return false;
            if (raw == null || raw.Length != expectedRows.Count) return false;
            var components = new List<Component2>();
            try
            {
                foreach (object item in raw)
                {
                    Component2 component = item as Component2;
                    if (component == null) return false;
                    components.Add(component);
                }
                var used = new HashSet<int>();
                foreach (PlacementEvidence expected in expectedRows)
                {
                    var matches = new List<int>();
                    for (int index = 0; index < components.Count; index++)
                    {
                        if (used.Contains(index)) continue;
                        Component2 component = components[index];
                        string name = Safe(delegate { return component.Name2; }, "");
                        string path = Safe(delegate { return component.GetPathName(); }, "");
                        if (ComponentRoleMatches(name, expected.role) &&
                            PathsEqualRelative(path, expected.path, allowedRoot) &&
                            string.Equals(Sha256(path), expected.sha256,
                                StringComparison.OrdinalIgnoreCase))
                            matches.Add(index);
                    }
                    if (matches.Count != 1) return false;
                    int match = matches[0];
                    used.Add(match);
                    Component2 actual = components[match];
                    string actualPath = Safe(delegate { return actual.GetPathName(); }, "");
                    if (string.IsNullOrWhiteSpace(actualPath) || !File.Exists(actualPath) ||
                        !IsUnderRoot(actualPath, allowedRoot)) return false;
                    MathTransform transform = Safe(delegate
                    {
                        return actual.Transform2 as MathTransform;
                    }, null);
                    if (transform == null) return false;
                    Array array = Safe(delegate { return transform.ArrayData as Array; }, null);
                    double[] values = DoubleArray(array);
                    Release(transform);
                    if (values.Length < 12 || expected.rotation == null ||
                        expected.rotation.Length != 9) return false;
                    for (int index = 0; index < 9; index++)
                        if (!Near(values[index], expected.rotation[index], 1e-9)) return false;
                    if (!Near(values[9], expected.tx_mm / 1000.0, 1e-7) ||
                        !Near(values[10], expected.ty_mm / 1000.0, 1e-7) ||
                        !Near(values[11], expected.tz_mm / 1000.0, 1e-7)) return false;
                }
                return used.Count == components.Count;
            }
            finally
            {
                foreach (Component2 component in components) Release(component);
            }
        }

        private static bool ComponentRoleMatches(string componentName, string role)
        {
            return string.Equals(componentName, role, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(componentName) &&
                componentName.StartsWith(role + "-", StringComparison.OrdinalIgnoreCase));
        }

        private static bool PathsEqualRelative(string actualPath, string expectedPath,
            string allowedRoot)
        {
            try
            {
                RequireSafeReadPath(actualPath, allowedRoot, "ASSEMBLY_COMPONENT_PATH_UNSAFE");
                RequireSafeReadPath(expectedPath, allowedRoot, "ASSEMBLY_EXPECTED_PATH_UNSAFE");
                return string.Equals(RelativePath(allowedRoot, actualPath),
                    RelativePath(allowedRoot, expectedPath), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static PartHealth CapturePartHealth(ModelDoc2 model)
        {
            var health = new PartHealth { traversal_complete = true };
            PartDoc part = model as PartDoc;
            Array bodies = part == null ? null : Safe(delegate
            {
                return part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            }, null);
            health.body_count = bodies == null ? 0 : bodies.Length;
            if (bodies != null && bodies.Length == 1)
            {
                Body2 body = bodies.GetValue(0) as Body2;
                if (body != null)
                {
                    double maxX = 0, minX = 0, maxY = 0, minY = 0, maxZ = 0, minZ = 0;
                    double unused1, unused2;
                    bool boxGate = false;
                    try
                    {
                        boxGate = body.GetExtremePoint(1, 0, 0, out maxX, out unused1,
                            out unused2) &&
                            body.GetExtremePoint(-1, 0, 0, out minX, out unused1,
                                out unused2) &&
                            body.GetExtremePoint(0, 1, 0, out unused1, out maxY,
                                out unused2) &&
                            body.GetExtremePoint(0, -1, 0, out unused1, out minY,
                                out unused2) &&
                            body.GetExtremePoint(0, 0, 1, out unused1, out unused2,
                                out maxZ) &&
                            body.GetExtremePoint(0, 0, -1, out unused1, out unused2,
                                out minZ);
                    }
                    catch { boxGate = false; }
                    if (boxGate)
                    {
                        health.x_length_mm = Math.Abs(maxX - minX) * 1000.0;
                        health.y_length_mm = Math.Abs(maxY - minY) * 1000.0;
                        health.z_length_mm = Math.Abs(maxZ - minZ) * 1000.0;
                        health.bbox_from_body_extreme_points = true;
                    }
                    Release(body);
                }
            }
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureFeature(feature, health, 0);
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            if (feature != null)
            {
                health.traversal_complete = false;
                Release(feature);
            }
            return health;
        }

        private static void CaptureFeature(Feature feature, PartHealth health, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || health.feature_count > 20000)
            {
                health.traversal_complete = false;
                return;
            }
            health.feature_count++;
            string type = Safe(delegate { return feature.GetTypeName2(); }, "");
            if (string.Equals(type, "SheetMetal", StringComparison.OrdinalIgnoreCase))
                health.has_sheet_metal = true;
            if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase))
                health.has_flat_pattern = true;
            if (string.Equals(type, "MirrorStock", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "MirrorPart", StringComparison.OrdinalIgnoreCase))
                health.mirror_part_feature_count++;
            if (IsForeignFeatureType(type)) health.foreign_feature_count++;
            bool warning = false;
            int error2 = FeatureErrorCode2(feature, out warning);
            int error1 = Safe(delegate { return feature.GetErrorCode(); }, 0);
            bool suppressed = Safe(delegate { return feature.IsSuppressed(); }, false);
            if (!suppressed && (error1 > 0 || error2 > 0)) health.error_feature_count++;
            if (!suppressed && warning) health.warning_feature_count++;
            Feature child = Safe(delegate { return feature.GetFirstSubFeature() as Feature; }, null);
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeature(child, health, depth + 1);
                Feature next = Safe(delegate { return child.GetNextSubFeature() as Feature; }, null);
                Release(child);
                child = next;
            }
            if (child != null)
            {
                health.traversal_complete = false;
                Release(child);
            }
        }

        private static bool IsForeignFeatureType(string type)
        {
            return string.Equals(type, "BaseBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Imported", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ImportedBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ForeignBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Stock", StringComparison.OrdinalIgnoreCase);
        }

        private static bool NativeSheetMetalGate(PartHealth health)
        {
            return health != null && health.body_count == 1 && health.has_sheet_metal &&
                health.has_flat_pattern && health.foreign_feature_count == 0 &&
                health.mirror_part_feature_count == 0 &&
                health.error_feature_count == 0 && health.warning_feature_count == 0 &&
                health.traversal_complete;
        }

        private static bool NativeMirrorSheetMetalGate(PartHealth health)
        {
            return health != null && health.body_count == 1 && health.has_sheet_metal &&
                health.has_flat_pattern && health.foreign_feature_count == 0 &&
                health.mirror_part_feature_count >= 1 &&
                health.error_feature_count == 0 && health.warning_feature_count == 0 &&
                health.traversal_complete;
        }

        private static bool StiffenerBboxGate(PartHealth health)
        {
            return health != null && health.bbox_from_body_extreme_points &&
                Near(Math.Max(health.x_length_mm, health.y_length_mm),
                    TargetStiffenerLengthMm, BboxToleranceMm);
        }

        private static int ExternalReferenceCount(ModelDoc2 model)
        {
            if (model == null) return int.MaxValue;
            int extensionCount = Safe(delegate
            {
                return model.Extension.ListExternalFileReferencesCount();
            }, int.MaxValue);
            int documentCount = Safe(delegate
            {
                return model.ListExternalFileReferencesCount2();
            }, int.MaxValue);
            if (extensionCount < 0 || documentCount < 0) return int.MaxValue;
            return Math.Max(extensionCount, documentCount);
        }

        private static bool PartBboxGate(PartHealth health, double widthMm, double heightMm)
        {
            return health != null && health.bbox_from_body_extreme_points &&
                Near(health.x_length_mm, widthMm, BboxToleranceMm) &&
                Near(health.y_length_mm, heightMm, BboxToleranceMm);
        }

        private static void SetDrivingDimension(ModelDoc2 model, string name, double targetMm)
        {
            Dimension dimension = FindDimension(model, name);
            Require(dimension != null, "DRIVING_DIMENSION_NOT_FOUND", 21,
                "missing " + name);
            int state = Safe(delegate { return dimension.DrivenState; }, -1);
            Require(state == (int)swDimensionDrivenState_e.swDimensionDriving,
                "DRIVING_DIMENSION_STATE_INVALID", 21,
                name + " is not a driving dimension");
            dimension.SystemValue = targetMm / 1000.0;
            double readback = Safe(delegate { return dimension.SystemValue * 1000.0; },
                double.NaN);
            Release(dimension);
            Require(Near(readback, targetMm, DimensionToleranceMm),
                "DRIVING_DIMENSION_SET_FAILED", 21, name + " did not read back at target");
        }

        private static bool DimensionGate(ModelDoc2 model, string name, double expectedMm)
        {
            Dimension dimension = FindDimension(model, name);
            double actual = dimension == null ? double.NaN :
                Safe(delegate { return dimension.SystemValue * 1000.0; }, double.NaN);
            int state = dimension == null ? -1 :
                Safe(delegate { return dimension.DrivenState; }, -1);
            Release(dimension);
            return state == (int)swDimensionDrivenState_e.swDimensionDriving &&
                Near(actual, expectedMm, DimensionToleranceMm);
        }

        private static Dimension FindDimension(ModelDoc2 model, string name)
        {
            Dimension dimension = Safe(delegate { return model.Parameter(name) as Dimension; }, null);
            if (dimension != null) return dimension;
            string path = Safe(delegate { return model.GetPathName(); }, "");
            string qualified = name + "@" + Path.GetFileNameWithoutExtension(path) + ".Part";
            return Safe(delegate { return model.Parameter(qualified) as Dimension; }, null);
        }

        private static void SaveModel(ModelDoc2 model, string role)
        {
            int errors = 0, warnings = 0;
            bool saved = Safe(delegate
            {
                return model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref errors, ref warnings);
            }, false);
            bool dirty = Safe(delegate { return model.GetSaveFlag(); }, true);
            Require(saved && errors == 0 && warnings == 0 && !dirty,
                "PART_SAVE_FAILED", 22, role + " save failed or left a dirty document");
        }

        private static ModelDoc2 OpenDocument(ISldWorks sw, string path, int documentType,
            bool readOnly, ref int errors, ref int warnings)
        {
            int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
            if (readOnly) options |= (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;
            try
            {
                return sw.OpenDoc6(path, documentType, options, "", ref errors, ref warnings)
                    as ModelDoc2;
            }
            catch
            {
                return null;
            }
        }

        private static OwnedSession StartOwnedSession(DoorRequest request, string phase,
            RunResult result)
        {
            List<int> baseline = ProcessIds("SLDWORKS");
            List<int> monitorBaseline = ProcessIds("sldProcMon");
            Require(baseline.Count == 0 && monitorBaseline.Count == 0,
                "CAD_SESSION_BASELINE_NOT_EMPTY", 26,
                phase + " requires an empty CAD process baseline");
            ISldWorks sw = null;
            int pid = 0;
            long activationStartedTicksUtc = DateTime.UtcNow.Ticks;
            try
            {
                Type type = Type.GetTypeFromProgID("SldWorks.Application.28", true);
                result.activation_prog_id = "SldWorks.Application.28";
                sw = Activator.CreateInstance(type) as ISldWorks;
                List<int> activationDelta = ProcessIds("SLDWORKS").Except(baseline).ToList();
                result.activation_delta_process_ids = activationDelta;
                Require(sw != null, "SOLIDWORKS_2020_START_FAILED", 26,
                    phase + " could not start SldWorks.Application.28");
                pid = Safe(delegate { return sw.GetProcessID(); }, 0);
                Require(pid > 0, "SOLIDWORKS_PROCESS_IDENTITY_FAILED", 26,
                    phase + " did not expose an owned SLDWORKS PID immediately after creation");
                string executablePath = "";
                long processStartTicks = 0;
                int fileMajor = 0;
                using (Process ownedProcess = Process.GetProcessById(pid))
                {
                    executablePath = ownedProcess.MainModule.FileName;
                    processStartTicks = ownedProcess.StartTime.ToUniversalTime().Ticks;
                    fileMajor = ownedProcess.MainModule.FileVersionInfo.FileMajorPart;
                }
                sw.Visible = request.visible;
                sw.UserControl = false;
                sw.CommandInProgress = true;
                string revision = Safe(delegate { return sw.RevisionNumber(); }, "");
                Require(revision.StartsWith("28.", StringComparison.Ordinal),
                    "SOLIDWORKS_VERSION_MISMATCH", 26,
                    phase + " requires SolidWorks 2020 revision 28.x");
                List<int> running = ProcessIds("SLDWORKS");
                Require(pid > 0 && running.Count == 1 && running[0] == pid,
                    "SOLIDWORKS_PROCESS_IDENTITY_FAILED", 26,
                    phase + " did not establish one exclusive owned SLDWORKS PID");
                Require(fileMajor == 28 &&
                    string.Equals(Path.GetFileName(executablePath), "SLDWORKS.exe",
                        StringComparison.OrdinalIgnoreCase),
                    "SOLIDWORKS_EXECUTABLE_IDENTITY_FAILED", 26,
                    phase + " did not establish the SolidWorks 2020 executable identity");
                var session = new OwnedSession
                {
                    phase = phase,
                    sw = sw,
                    process_id = pid,
                    baseline_process_ids = baseline,
                    baseline_monitor_ids = monitorBaseline,
                    process_start_ticks_utc = processStartTicks,
                    executable_path = executablePath,
                    solidworks_revision = revision,
                    started_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                };
                result.sessions.Add(new SessionEvidence
                {
                    phase = phase,
                    process_id = pid,
                    process_start_ticks_utc = processStartTicks,
                    executable_path = executablePath,
                    solidworks_revision = revision,
                    started_at_utc = session.started_at_utc,
                    exclusive_identity_gate = true
                });
                return session;
            }
            catch
            {
                TryCleanupActivationDelta(sw, pid, baseline, activationStartedTicksUtc, result);
                sw = null;
                Require(ProcessIds("SLDWORKS").Count == 0 &&
                    ProcessIds("sldProcMon").Count == 0, "SOLIDWORKS_START_CLEANUP_FAILED", 26,
                    phase + " failed to restore the CAD process baseline after start failure");
                throw;
            }
        }

        private static void TryCleanupActivationDelta(ISldWorks sw, int reportedPid,
            List<int> baseline, long activationStartedTicksUtc, RunResult result)
        {
            result.activation_cleanup_attempted = true;
            List<int> activationDelta = WaitForActivationDelta(baseline, 5000);
            result.activation_delta_process_ids = activationDelta;
            var proven = new List<int>();
            foreach (int candidatePid in activationDelta)
            {
                try
                {
                    using (Process candidate = Process.GetProcessById(candidatePid))
                    {
                        string executable = candidate.MainModule.FileName;
                        long started = candidate.StartTime.ToUniversalTime().Ticks;
                        int major = candidate.MainModule.FileVersionInfo.FileMajorPart;
                        if (string.Equals(Path.GetFileName(executable), "SLDWORKS.exe",
                            StringComparison.OrdinalIgnoreCase) && major == 28 &&
                            started >= activationStartedTicksUtc &&
                            (reportedPid == 0 || candidatePid == reportedPid)) proven.Add(candidatePid);
                    }
                }
                catch { }
            }
            bool uniqueOwned = proven.Count == 1 && activationDelta.Count == 1;
            result.activation_cleanup_proven_owned = uniqueOwned;
            if (!uniqueOwned)
            {
                result.activation_cleanup_preserved_unowned = activationDelta.Count > 0;
                Release(sw);
                return;
            }
            if (sw != null)
            {
                Try(delegate { sw.CloseAllDocuments(true); });
                Try(delegate { sw.CommandInProgress = false; });
                Try(delegate { sw.ExitApp(); });
                Release(sw);
            }
            int ownedPid = proven[0];
            if (!WaitForExit(ownedPid, 15000))
            {
                try
                {
                    using (Process owned = Process.GetProcessById(ownedPid))
                    {
                        string executable = owned.MainModule.FileName;
                        long started = owned.StartTime.ToUniversalTime().Ticks;
                        int major = owned.MainModule.FileVersionInfo.FileMajorPart;
                        if (major == 28 && started >= activationStartedTicksUtc &&
                            string.Equals(Path.GetFileName(executable), "SLDWORKS.exe",
                                StringComparison.OrdinalIgnoreCase)) owned.Kill();
                    }
                }
                catch { }
                WaitForExit(ownedPid, 15000);
            }
            result.activation_cleanup_completed = ProcessIds("SLDWORKS").Count == 0;
        }

        private static List<int> WaitForActivationDelta(List<int> baseline, int timeoutMs)
        {
            Stopwatch timer = Stopwatch.StartNew();
            List<int> activationDelta;
            do
            {
                activationDelta = ProcessIds("SLDWORKS").Except(
                    baseline ?? new List<int>()).ToList();
                if (activationDelta.Count > 0) return activationDelta;
                Thread.Sleep(100);
            }
            while (timer.ElapsedMilliseconds < timeoutMs);
            return activationDelta;
        }

        private static void StopOwnedSession(OwnedSession session, RunResult result)
        {
            if (session == null) return;
            SessionEvidence evidence = result.sessions.LastOrDefault(delegate(SessionEvidence row)
            {
                return row.phase == session.phase && row.process_id == session.process_id;
            });
            ISldWorks sw = session.sw;
            if (sw != null)
            {
                Try(delegate { sw.CloseAllDocuments(true); });
                Try(delegate { sw.CommandInProgress = false; });
                if (evidence != null) evidence.exit_requested = TryAction(delegate { sw.ExitApp(); });
                Release(sw);
                session.sw = null;
            }
            bool exited = WaitForExit(session.process_id, 30000);
            bool empty = ProcessIds("SLDWORKS").Count == 0;
            bool monitorsEmpty = WaitForProcessNameExit("sldProcMon", 15000);
            if (evidence != null)
            {
                evidence.process_exited = exited;
                evidence.cad_baseline_restored = empty && monitorsEmpty;
                evidence.completed_at_utc = DateTime.UtcNow.ToString("o",
                    CultureInfo.InvariantCulture);
            }
            Require(exited && empty && monitorsEmpty, "OWNED_CAD_SESSION_EXIT_FAILED", 27,
                session.phase + " did not restore the empty CAD process baseline");
        }

        private static bool WaitForExit(int pid, int timeoutMs)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using (Process process = Process.GetProcessById(pid))
                        if (process.HasExited) return true;
                }
                catch (ArgumentException) { return true; }
                Thread.Sleep(200);
            }
            return false;
        }

        private static bool WaitForProcessNameExit(string name, int timeoutMs)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMs)
            {
                if (ProcessIds(name).Count == 0) return true;
                Thread.Sleep(200);
            }
            return ProcessIds(name).Count == 0;
        }

        private static List<int> ProcessIds(string name)
        {
            var ids = new List<int>();
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try { ids.Add(process.Id); }
                finally { process.Dispose(); }
            }
            ids.Sort();
            return ids;
        }

        private static void CloseDocument(ISldWorks sw, ref ModelDoc2 model)
        {
            if (model == null) return;
            string title = "";
            try { title = model.GetTitle(); }
            catch { }
            Release(model);
            model = null;
            if (!string.IsNullOrWhiteSpace(title)) Try(delegate { sw.CloseDoc(title); });
        }

        private static void CopyTree(string sourceRoot, string targetRoot)
        {
            Require(!Directory.Exists(targetRoot) && !File.Exists(targetRoot),
                "RELOCATION_TARGET_EXISTS", 28,
                "relocation probe directory already exists");
            Directory.CreateDirectory(targetRoot);
            foreach (string directory in Directory.GetDirectories(sourceRoot, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(targetRoot,
                    RelativePath(sourceRoot, directory)));
            foreach (string file in Directory.GetFiles(sourceRoot, "*",
                SearchOption.AllDirectories))
            {
                string target = Path.Combine(targetRoot, RelativePath(sourceRoot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
                Require(string.Equals(Sha256(file), Sha256(target),
                    StringComparison.OrdinalIgnoreCase), "RELOCATION_COPY_HASH_MISMATCH", 28,
                    "relocation copy hash mismatch: " + file);
            }
        }

        private static List<FileHashEvidence> HashTree(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase)
                .Select(delegate(string path)
                {
                    return new FileHashEvidence
                    {
                        relative_path = RelativePath(root, path).Replace('\\', '/'),
                        size_bytes = new FileInfo(path).Length,
                        sha256 = Sha256(path)
                    };
                }).ToList();
        }

        private static bool VerifyExactOutputInventory(string outputRoot,
            List<FileHashEvidence> files)
        {
            HashSet<string> expectedCad = ExpectedCadRelativePaths();
            var allowedNonCad = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                FlatImportManifestFileName
            };
            if (files == null || files.Any(delegate(FileHashEvidence row)
            {
                return string.IsNullOrWhiteSpace(row.relative_path) ||
                    row.relative_path.IndexOf(".tmp-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.relative_path.StartsWith("~$", StringComparison.OrdinalIgnoreCase);
            })) return false;
            var actualCad = new HashSet<string>(files.Where(delegate(FileHashEvidence row)
            {
                string extension = Path.GetExtension(row.relative_path ?? "");
                return string.Equals(extension, ".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".SLDASM", StringComparison.OrdinalIgnoreCase);
            }).Select(delegate(FileHashEvidence row)
            {
                return row.relative_path.Replace('\\', '/');
            }), StringComparer.OrdinalIgnoreCase);
            var actualNonCad = new HashSet<string>(files.Where(delegate(FileHashEvidence row)
            {
                string extension = Path.GetExtension(row.relative_path ?? "");
                return !string.Equals(extension, ".SLDPRT", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".SLDASM", StringComparison.OrdinalIgnoreCase);
            }).Select(delegate(FileHashEvidence row)
            {
                return row.relative_path.Replace('\\', '/');
            }), StringComparer.OrdinalIgnoreCase);
            var actualDirectories = new HashSet<string>(Directory.GetDirectories(outputRoot, "*",
                SearchOption.AllDirectories).Select(delegate(string directory)
            {
                return RelativePath(outputRoot, directory).Replace('\\', '/');
            }), StringComparer.OrdinalIgnoreCase);
            var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "parts", "assemblies"
            };
            string manifestPath = Path.Combine(outputRoot, FlatImportManifestFileName);
            return files.Count == 14 && actualCad.Count == 13 && actualCad.SetEquals(expectedCad) &&
                actualNonCad.SetEquals(allowedNonCad) && actualDirectories.SetEquals(expectedDirectories) &&
                VerifyFlatImportManifest(manifestPath, files) &&
                files.All(delegate(FileHashEvidence row)
                {
                    string candidate = Path.Combine(outputRoot, row.relative_path.Replace('/', '\\'));
                    return File.Exists(candidate) && Sha256(candidate) == row.sha256;
                });
        }

        private static HashSet<string> ExpectedCadRelativePaths()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "parts/door_panel_left_W381_H254p428571.SLDPRT",
                "parts/door_panel_right_W381_H254p428571.SLDPRT",
                "parts/door_stiffener_H243p928571.SLDPRT",
                "parts/hinge_latch_plate.SLDPRT",
                "parts/u_hook_pad.SLDPRT", "parts/plastic_bushing.SLDPRT",
                "parts/door_hinge_pin.SLDPRT", "parts/circlip.SLDPRT",
                "parts/mechanical_lock_tongue.SLDPRT",
                "assemblies/door_weld_left_W381_H254p428571.SLDASM",
                "assemblies/door_weld_right_W381_H254p428571.SLDASM",
                "assemblies/ordinary_door_left_W381_H254p428571.SLDASM",
                "assemblies/ordinary_door_right_W381_H254p428571.SLDASM"
            };
        }

        private static void WriteFlatImportManifest(string outputRoot,
            List<FileHashEvidence> cadFiles, RunResult result)
        {
            HashSet<string> expected = ExpectedCadRelativePaths();
            Require(cadFiles != null && cadFiles.Count == 13 && cadFiles.All(
                delegate(FileHashEvidence row) { return expected.Contains(row.relative_path); }),
                "FLAT_IMPORT_SOURCE_INVENTORY_INVALID", 29,
                "flat import manifest requires exactly the approved 13 CAD files");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<FlatImportEntry>();
            foreach (FileHashEvidence row in cadFiles.OrderBy(
                delegate(FileHashEvidence value) { return value.relative_path; },
                StringComparer.OrdinalIgnoreCase))
            {
                string flatTargetName = Path.GetFileName(row.relative_path);
                Require(names.Add(flatTargetName), "FLAT_IMPORT_TARGET_COLLISION", 29,
                    "flat import target names must be unique: " + flatTargetName);
                Require(!V37WorkingPackFileNames.Contains(flatTargetName),
                    "V37_WORKING_PACK_NAME_COLLISION", 29,
                    "flat import target collides with the trusted V37 75-file inventory: " +
                    flatTargetName);
                entries.Add(new FlatImportEntry
                {
                    canonicalRelativePath = row.relative_path.Replace('\\', '/'),
                    flatTargetName = flatTargetName,
                    sizeBytes = row.size_bytes,
                    sha256 = row.sha256,
                    category = string.Equals(Path.GetExtension(flatTargetName), ".SLDASM",
                        StringComparison.OrdinalIgnoreCase) ? "assembly" : "part"
                });
            }
            Require(V37WorkingPackFileNames.Count == 75, "V37_WORKING_PACK_INVENTORY_INVALID", 29,
                "trusted V37 working-pack filename registry must contain exactly 75 files");
            var manifest = new FlatImportManifest
            {
                schema = "winnsen.16029.native_door_module_flat_import_manifest.v1",
                immutable = true,
                cadFileCount = 13,
                v37WorkingPackFileCount = 75,
                v37WorkingPackInventoryDigest =
                    V37CanonicalInventoryDigest,
                workingPackImportState = "NOT_IMPORTED",
                rootAssemblerBoundary = RootAssemblerImportBoundary,
                entries = entries
            };
            string path = Path.Combine(outputRoot, FlatImportManifestFileName);
            RequireSafeMutationPath(path, outputRoot, "FLAT_IMPORT_MANIFEST_PATH_UNSAFE");
            Require(!File.Exists(path), "FLAT_IMPORT_MANIFEST_ALREADY_EXISTS", 29,
                "flat import manifest is immutable and must not already exist");
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 };
            byte[] bytes = new UTF8Encoding(false).GetBytes(serializer.Serialize(manifest));
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            RequireSafeReadPath(path, outputRoot, "FLAT_IMPORT_MANIFEST_PATH_UNSAFE");
            result.flat_import_manifest_path = path;
            result.flat_import_manifest_sha256 = Sha256(path);
            result.working_pack_imported = false;
            result.root_assembler_boundary = RootAssemblerImportBoundary;
        }

        private static bool VerifyFlatImportManifest(string path, List<FileHashEvidence> files)
        {
            if (!File.Exists(path)) return false;
            FlatImportManifest manifest;
            try { manifest = ReadJson<FlatImportManifest>(path); }
            catch { return false; }
            if (manifest == null || !manifest.immutable || manifest.cadFileCount != 13 ||
                manifest.v37WorkingPackFileCount != 75 ||
                manifest.workingPackImportState != "NOT_IMPORTED" ||
                manifest.rootAssemblerBoundary != RootAssemblerImportBoundary ||
                manifest.entries == null || manifest.entries.Count != 13) return false;
            var rows = files.Where(delegate(FileHashEvidence row)
            {
                return ExpectedCadRelativePaths().Contains(row.relative_path);
            }).ToDictionary(delegate(FileHashEvidence row) { return row.relative_path; },
                StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return manifest.entries.All(delegate(FlatImportEntry entry)
            {
                FileHashEvidence row;
                return entry != null && rows.TryGetValue(entry.canonicalRelativePath, out row) &&
                    names.Add(entry.flatTargetName) &&
                    !V37WorkingPackFileNames.Contains(entry.flatTargetName) &&
                    string.Equals(Path.GetFileName(entry.canonicalRelativePath), entry.flatTargetName,
                        StringComparison.OrdinalIgnoreCase) && row.size_bytes == entry.sizeBytes &&
                    string.Equals(row.sha256, entry.sha256, StringComparison.OrdinalIgnoreCase) &&
                    (entry.category == "part" || entry.category == "assembly");
            });
        }

        private static string NormalizedSourceSha256()
        {
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            string source = Path.Combine(Path.GetDirectoryName(executable),
                "NativeDoorModule888x14.cs");
            Require(File.Exists(source), "TOOL_SOURCE_NOT_FOUND", 3,
                "compiled tool directory must contain the reviewed source file");
            return NormalizedSourceSha256At(source);
        }

        private static string RelativePath(string root, string path)
        {
            string canonicalRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string canonicalPath = Path.GetFullPath(path);
            Require(canonicalPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase),
                "RELATIVE_PATH_SCOPE_FAILED", 80, "path is outside root");
            return canonicalPath.Substring(canonicalRoot.Length);
        }

        private static List<SourceRow> SourceRows(SourceSet sources, bool mirrorRight)
        {
            var rows = new List<SourceRow>
            {
                new SourceRow("left_panel_seed", sources == null ? null : sources.left_panel_seed),
                new SourceRow("stiffener_seed", sources == null ? null : sources.stiffener_seed),
                new SourceRow("latch_plate", sources == null ? null : sources.latch_plate),
                new SourceRow("hook_pad", sources == null ? null : sources.hook_pad),
                new SourceRow("bushing", sources == null ? null : sources.bushing),
                new SourceRow("hinge_pin", sources == null ? null : sources.hinge_pin),
                new SourceRow("circlip", sources == null ? null : sources.circlip),
                new SourceRow("mechanical_lock_tongue",
                    sources == null ? null : sources.mechanical_lock_tongue)
            };
            if (!mirrorRight)
                rows.Insert(1, new SourceRow("right_panel_seed",
                    sources == null ? null : sources.right_panel_seed));
            return rows;
        }

        private static bool IsMirrorRightStrategy(DoorRequest request)
        {
            return request != null && string.Equals(request.right_panel_strategy,
                RightStrategyMirrorPart, StringComparison.Ordinal);
        }

        private static int FeatureErrorCode2(Feature feature, out bool warning)
        {
            warning = false;
            try { return feature.GetErrorCode2(out warning); }
            catch { return Safe(delegate { return feature.GetErrorCode(); }, 0); }
        }

        private static double[] DoubleArray(Array array)
        {
            if (array == null) return new double[0];
            var output = new double[array.Length];
            for (int index = 0; index < array.Length; index++)
                output[index] = Convert.ToDouble(array.GetValue(index), CultureInfo.InvariantCulture);
            return output;
        }

        private static object TransformArray(Placement placement)
        {
            double[] r = placement.rotation;
            return new double[]
            {
                r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8],
                placement.tx_mm / 1000.0, placement.ty_mm / 1000.0,
                placement.tz_mm / 1000.0, 1.0, 0.0, 0.0, 0.0
            };
        }

        private static double[] Identity()
        {
            return new[] { 1.0, 0, 0, 0, 1.0, 0, 0, 0, 1.0 };
        }

        private static double[] RotateX90()
        {
            return new[] { 1.0, 0, 0, 0, 0, 1.0, 0, -1.0, 0 };
        }

        private static double[] RotateXMinus90()
        {
            return new[] { 1.0, 0, 0, 0, 0, -1.0, 0, 1.0, 0 };
        }

        private static double[] RotateX180()
        {
            return new[] { 1.0, 0, 0, 0, -1.0, 0, 0, 0, -1.0 };
        }

        private static double[] RotateY180()
        {
            return new[] { -1.0, 0, 0, 0, 1.0, 0, 0, 0, -1.0 };
        }

        private static double[] Multiply(double[] a, double[] b)
        {
            return new[]
            {
                a[0] * b[0] + a[1] * b[3] + a[2] * b[6],
                a[0] * b[1] + a[1] * b[4] + a[2] * b[7],
                a[0] * b[2] + a[1] * b[5] + a[2] * b[8],
                a[3] * b[0] + a[4] * b[3] + a[5] * b[6],
                a[3] * b[1] + a[4] * b[4] + a[5] * b[7],
                a[3] * b[2] + a[4] * b[5] + a[5] * b[8],
                a[6] * b[0] + a[7] * b[3] + a[8] * b[6],
                a[6] * b[1] + a[7] * b[4] + a[8] * b[7],
                a[6] * b[2] + a[7] * b[5] + a[8] * b[8]
            };
        }

        private static double MatrixDeterminant(double[] r)
        {
            return r[0] * (r[4] * r[8] - r[5] * r[7]) -
                r[1] * (r[3] * r[8] - r[5] * r[6]) +
                r[2] * (r[3] * r[7] - r[4] * r[6]);
        }

        private static bool IsUnderRoot(string path, string root)
        {
            try
            {
                string canonicalRoot = Path.GetFullPath(root).TrimEnd('\\', '/') +
                    Path.DirectorySeparatorChar;
                string canonicalPath = Path.GetFullPath(path);
                return canonicalPath.StartsWith(canonicalRoot,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
                    Path.GetFullPath(right).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool Near(double left, double right, double tolerance)
        {
            return !double.IsNaN(left) && !double.IsNaN(right) &&
                Math.Abs(left - right) <= tolerance;
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private static string Sha256Text(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(
                    new UTF8Encoding(false).GetBytes(value ?? ""))).Replace("-", "");
        }

        private static string Sha256Bytes(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(value ?? new byte[0]))
                    .Replace("-", "");
        }

        private static string CanonicalInventoryDigest(IEnumerable<string> paths)
        {
            var builder = new StringBuilder();
            foreach (string path in paths.OrderBy(delegate(string value)
            { return Path.GetFileName(value); }, StringComparer.OrdinalIgnoreCase))
                builder.Append(Path.GetFileName(path)).Append('|').Append(Sha256(path)).Append('\n');
            return Sha256Text(builder.ToString());
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
            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
                return "{" + string.Join(",", dictionary.Keys.OrderBy(delegate(string key)
                    { return key; }, StringComparer.Ordinal).Select(delegate(string key)
                    { return JsonString(key) + ":" + StableJson(dictionary[key]); }).ToArray()) + "}";
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
            var builder = new StringBuilder((value ?? "").Length + 2);
            builder.Append('"');
            string input = value ?? "";
            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];
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
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4",
                                CultureInfo.InvariantCulture));
                        else if (char.IsHighSurrogate(character) && index + 1 < input.Length &&
                            char.IsLowSurrogate(input[index + 1]))
                        {
                            builder.Append(character).Append(input[index + 1]);
                            index++;
                        }
                        else if (char.IsSurrogate(character))
                            builder.Append("\\u").Append(((int)character).ToString("x4",
                                CultureInfo.InvariantCulture));
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

        private static string EvidenceCommitmentDigest(RunResult result)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 };
            Dictionary<string, object> evidence = serializer.DeserializeObject(
                serializer.Serialize(result)) as Dictionary<string, object>;
            return EvidenceCommitmentDigest(evidence);
        }

        private static string EvidenceCommitmentDigest(Dictionary<string, object> evidence)
        {
            if (evidence == null) return "";
            var projection = new Dictionary<string, object>(evidence, StringComparer.Ordinal);
            foreach (string key in EvidenceCommitmentExcludedKeys) projection.Remove(key);
            return Sha256Text(StableJson(projection));
        }

        private static Dictionary<string, object> ReadJsonObject(string path)
        {
            object value = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 }.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
            Dictionary<string, object> output = value as Dictionary<string, object>;
            Require(output != null, "JSON_OBJECT_REQUIRED", 11,
                "JSON artifact must contain an object: " + path);
            return output;
        }

        private static Dictionary<string, object> ChildMap(Dictionary<string, object> parent,
            string name)
        {
            object value;
            Dictionary<string, object> child;
            return parent != null && parent.TryGetValue(name, out value) &&
                (child = value as Dictionary<string, object>) != null ? child :
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static string TextValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value != null ?
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        }

        private static string ValueText(object value)
        {
            return value == null ? "" : Convert.ToString(value,
                CultureInfo.InvariantCulture) ?? "";
        }

        private static double NumberValue(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return double.NaN;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return double.NaN; }
        }

        private static bool BoolValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value is bool &&
                (bool)value;
        }

        private static object[] ArrayItems(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return new object[0];
            object[] direct = value as object[];
            if (direct != null) return direct;
            ArrayList list = value as ArrayList;
            return list == null ? new object[0] : list.Cast<object>().ToArray();
        }

        private static bool StringArrayEquals(object[] actual, string[] expected)
        {
            return actual != null && expected != null && actual.Length == expected.Length &&
                actual.Select(ValueText).SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static bool IsSha256(string value)
        {
            return Regex.IsMatch(value ?? "", "^[A-Fa-f0-9]{64}$",
                RegexOptions.CultureInvariant);
        }

        private static bool ParseUtc(string value, out DateTime output)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out output);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values == null ? "" : values.FirstOrDefault(delegate(string value)
            { return !string.IsNullOrWhiteSpace(value); }) ?? "";
        }

        private static string FindRepositoryRoot(string start)
        {
            DirectoryInfo current = new FileInfo(start).Directory;
            int guard = 0;
            while (current != null && guard++ < 20)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                    return current.FullName;
                current = current.Parent;
            }
            return "";
        }

        private static string NormalizedSourceSha256At(string path)
        {
            return Sha256Text(NormalizeGenericIdentitySource(
                File.ReadAllText(path, Encoding.UTF8)));
        }

        private static string NormalizeGenericIdentitySource(string source)
        {
            string normalized = (source ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = Regex.Replace(normalized,
                "(private\\s+const\\s+string\\s+ExpectedSourceSha256\\s*=\\s*\")" +
                "(?:__SOURCE_SHA256__|[0-9A-F]{64})(\"\\s*;)",
                delegate(Match match)
                {
                    return match.Groups[1].Value + "__SOURCE_SHA256__" +
                        match.Groups[2].Value;
                }, RegexOptions.CultureInvariant);
            return Regex.Replace(normalized,
                "(private\\s+const\\s+string\\s+ExpectedContractSnapshotSha256\\s*=\\s*\")" +
                "(?:__CONTRACT_SHA256__|[0-9A-F]{64})(\"\\s*;)",
                delegate(Match match)
                {
                    return match.Groups[1].Value + "__CONTRACT_SHA256__" +
                        match.Groups[2].Value;
                }, RegexOptions.CultureInvariant);
        }

        private static string NormalizedLockSourceSha256At(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            source = Regex.Replace(source,
                "(private\\s+const\\s+string\\s+ExpectedSourceSha256\\s*=\\s*\")" +
                "(?:__SOURCE_SHA256__|[0-9A-F]{64})(\"\\s*;)",
                delegate(Match match)
                {
                    return match.Groups[1].Value + "__SOURCE_SHA256__" +
                        match.Groups[2].Value;
                }, RegexOptions.CultureInvariant);
            return Sha256Text(source);
        }

        private static string NormalizeHash(string value)
        {
            return (value ?? "").Trim().Replace("-", "").ToUpperInvariant();
        }

        private static string SafeLeaf(string value)
        {
            string output = value ?? "";
            foreach (char c in Path.GetInvalidFileNameChars()) output = output.Replace(c, '_');
            return output;
        }

        private static string FullPathOrEmpty(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path); }
            catch { return ""; }
        }

        private static string Argument(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            return "";
        }

        private static T ReadJson<T>(string path)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return serializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
        }

        private static void WriteJsonAtomicNew(string path, object value)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 };
            string tempPath = fullPath + ".tmp-" + Process.GetCurrentProcess().Id.ToString(
                CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            byte[] bytes = new UTF8Encoding(false).GetBytes(serializer.Serialize(value) + "\n");
            try
            {
                Require(!File.Exists(fullPath), "IMMUTABLE_ARTIFACT_ALREADY_EXISTS", 29,
                    "atomic CreateNew target already exists: " + fullPath);
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(tempPath, fullPath);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static void CommitExecutionEvidenceAndReceipt(string outPath, DoorRequest request,
            RunResult result)
        {
            RevalidateAuthorizationTimeWindow(request, result);
            Require(string.Equals(Sha256(Path.Combine(request.isolated_root,
                        StageReceiptPaths[4].Replace('/', '\\'))),
                    result.predecessor_receipt_sha256, StringComparison.OrdinalIgnoreCase),
                "ASSEMBLIES_PREDECESSOR_CHANGED_BEFORE_COMMIT", 29,
                "width assemblies receipt changed before door evidence commit");
            string livePost = CurrentWorkingPackDigest(request.isolated_root);
            Require(string.Equals(livePost, result.current_pre_inventory_digest,
                    StringComparison.OrdinalIgnoreCase),
                "DOOR_STAGE_WORKING_PACK_MUTATED", 29,
                "door module must not mutate the 75-file working pack");
            result.post_inventory_digest = livePost;
            DateTime completed = DateTime.UtcNow;
            result.status = "EXECUTION_PASS";
            result.success = true;
            result.completed = true;
            result.committed = true;
            result.error_code = "";
            result.error = "";
            result.completed_at_utc = completed.ToString("o", CultureInfo.InvariantCulture);
            result.evidence_commit_started_at_utc = result.completed_at_utc;
            result.evidence_commit_completed_at_utc = result.completed_at_utc;
            result.commit_completed_at_utc = result.completed_at_utc;
            result.evidence_write_attempted = true;
            result.evidence_write_succeeded = true;
            result.evidence_finalize_write_succeeded = true;
            result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
            RequireSafeMutationPath(outPath, request.isolated_root, "EVIDENCE_PATH_UNSAFE");
            WriteJsonAtomicNew(outPath, result);
            result.evidence_artifact_owned = true;
            result.evidence_sha256 = Sha256(outPath);
            Dictionary<string, object> committedEvidence = ReadJsonObject(outPath);
            Require(BoolValue(committedEvidence, "success") &&
                !ContainsReceiptIdentity(committedEvidence) &&
                string.Equals(TextValue(committedEvidence, "evidence_commitment_sha256"),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(EvidenceCommitmentDigest(committedEvidence),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256(outPath), result.evidence_sha256,
                    StringComparison.OrdinalIgnoreCase),
                "EVIDENCE_ATOMIC_REREAD_FAILED", 29,
                "door success evidence failed immutable SHA/commitment re-read");

            RevalidateAuthorizationTimeWindow(request, result);
            Dictionary<string, object> receipt = BuildDoorReceipt(result);
            RequireExactKeys(receipt, "door runtime receipt", RuntimeReceiptKeys);
            result.receipt_write_attempted = true;
            RequireSafeMutationPath(result.receipt_path, request.isolated_root,
                "RECEIPT_PATH_UNSAFE");
            WriteJsonAtomicNew(result.receipt_path, receipt);
            result.receipt_artifact_owned = true;
            result.receipt_sha256 = Sha256(result.receipt_path);
            Dictionary<string, object> committedReceipt = ReadJsonObject(result.receipt_path);
            RequireExactKeys(committedReceipt, "committed door runtime receipt",
                RuntimeReceiptKeys);
            Require(RuntimeReceiptEnvelopeValid(committedReceipt, DoorReceiptSchema,
                    DoorModuleExecutionPhase, DoorModulePhase) &&
                TextValue(committedReceipt, "evidencePath") == DoorEvidenceRelativePath &&
                string.Equals(TextValue(committedReceipt, "evidenceSha256"),
                    result.evidence_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(committedReceipt, "evidenceCommitmentSha256"),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(committedReceipt, "predecessorReceiptSha256"),
                    result.predecessor_receipt_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256(result.receipt_path), result.receipt_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256(outPath), result.evidence_sha256,
                    StringComparison.OrdinalIgnoreCase),
                "RECEIPT_ATOMIC_REREAD_FAILED", 29,
                "door receipt failed immutable evidence pair re-read");
            RevalidateAuthorizationTimeWindow(request, result);
            Require(CompletionWithinAuthorization(result.completed_at_utc,
                    result.authorization_issued_at_utc,
                    result.execution_authorization_expires_at_utc,
                    result.task_lease_expires_at_utc),
                "COMPLETION_OUTSIDE_AUTHORIZATION_WINDOW", 29,
                "door evidence/receipt completion is outside authorization or lease");
        }

        private static Dictionary<string, object> BuildDoorReceipt(RunResult result)
        {
            byte[] authorizationBytes = File.ReadAllBytes(result.execution_authorization_path);
            Require(string.Equals(Sha256Bytes(authorizationBytes),
                    result.execution_authorization_sha256, StringComparison.OrdinalIgnoreCase),
                "AUTHORIZATION_BYTES_CHANGED_BEFORE_RECEIPT", 29,
                "authorization bytes changed before door receipt creation");
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", DoorReceiptSchema },
                { "phase", DoorModuleExecutionPhase }, { "success", true },
                { "completedAt", result.completed_at_utc }, { "taskId", result.task_id },
                { "taskRevision", result.task_revision_at_planning },
                { "taskDigest", result.task_digest }, { "requestDigest", result.request_digest },
                { "leaseId", result.lease_id },
                { "leaseExpiresAt", result.task_lease_expires_at_utc },
                { "attempt", result.attempt_number }, { "planSha256", result.plan_sha256 },
                { "authorizationId", result.authorization_id },
                { "authorizationSha256", result.execution_authorization_sha256 },
                { "authorizationJsonBase64", Convert.ToBase64String(authorizationBytes) },
                { "authorizationIssuedAt", result.authorization_issued_at_utc },
                { "authorizationExpiresAt", result.execution_authorization_expires_at_utc },
                { "recipeId", ExpectedRecipeId },
                { "recipeDigest", ExpectedRecipeDigest.ToUpperInvariant() },
                { "toolId", DoorModulePhase },
                { "toolSourceNormalizedSha256", result.tool_source_normalized_sha256 },
                { "toolExecutableSha256", result.tool_executable_sha256 },
                { "evidencePath", DoorEvidenceRelativePath },
                { "evidenceSha256", result.evidence_sha256 },
                { "evidenceCommitmentSha256", result.evidence_commitment_sha256 },
                { "preInventoryDigest", result.current_pre_inventory_digest },
                { "postInventoryDigest", result.post_inventory_digest },
                { "predecessorReceiptSha256", result.predecessor_receipt_sha256 }
            };
        }

        private static bool ContainsReceiptIdentity(Dictionary<string, object> evidence)
        {
            return evidence.Keys.Any(delegate(string key)
            {
                return key.IndexOf("receipt_path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    key.IndexOf("receipt_sha", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    key.Equals("phase_receipt_committed", StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool CompletionWithinAuthorization(string completedText,
            string issuedText, string expiresText, string leaseExpiresText)
        {
            DateTime completed, issued, expires, leaseExpires;
            return ParseUtc(completedText, out completed) && ParseUtc(issuedText, out issued) &&
                ParseUtc(expiresText, out expires) && ParseUtc(leaseExpiresText, out leaseExpires) &&
                completed >= issued && completed <= expires && completed <= leaseExpires;
        }

        private static string CurrentWorkingPackDigest(string attemptRoot)
        {
            string workingPack = Path.Combine(attemptRoot, "native_cad", "working_pack");
            if (!Directory.Exists(workingPack) ||
                Directory.GetDirectories(workingPack, "*", SearchOption.TopDirectoryOnly).Length != 0)
                return "";
            string[] files = Directory.GetFiles(workingPack, "*", SearchOption.TopDirectoryOnly);
            var names = new HashSet<string>(files.Select(Path.GetFileName),
                StringComparer.OrdinalIgnoreCase);
            if (files.Length != 75 || !names.SetEquals(V37WorkingPackFileNames)) return "";
            foreach (string file in files)
            {
                try { RequireSafeReadPath(file, attemptRoot, "CURRENT_WORKING_PACK_FILE_UNSAFE"); }
                catch { return ""; }
            }
            return CanonicalInventoryDigest(files);
        }

        private static bool SuccessPairPathsAvailable(string evidencePath, string receiptPath)
        {
            return !string.IsNullOrWhiteSpace(evidencePath) &&
                !string.IsNullOrWhiteSpace(receiptPath) &&
                !File.Exists(evidencePath) && !Directory.Exists(evidencePath) &&
                !File.Exists(receiptPath) && !Directory.Exists(receiptPath);
        }

        private static void CleanupUnpairedCommitArtifacts(DoorRequest request, RunResult result)
        {
            if (request == null || result == null ||
                string.IsNullOrWhiteSpace(request.isolated_root)) return;
            if (result.receipt_artifact_owned)
                SafeDeleteOwnedArtifact(result.receipt_path, request.isolated_root);
            if (result.evidence_artifact_owned)
                SafeDeleteOwnedArtifact(result.evidence_path, request.isolated_root);
            result.receipt_artifact_owned = false;
            result.evidence_artifact_owned = false;
            result.receipt_sha256 = "";
            result.evidence_sha256 = "";
        }

        private static void SafeDeleteOwnedArtifact(string path, string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                    !IsUnderRoot(path, root)) return;
                FileAttributes attributes = File.GetAttributes(path);
                if (!ArtifactAttributesSafe(attributes, GetHardLinkCount(path))) return;
                RequireSafeReadPath(path, root, "UNPAIRED_ARTIFACT_PATH_UNSAFE");
                File.Delete(path);
            }
            catch { }
        }

        private static bool ArtifactAttributesSafe(FileAttributes attributes, uint linkCount)
        {
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0 &&
                linkCount == 1;
        }

        private static void RollbackUncommittedOutput(DoorRequest request, RunResult result)
        {
            if (request == null || result == null ||
                string.IsNullOrWhiteSpace(request.isolated_root) ||
                string.IsNullOrWhiteSpace(request.output_dir)) return;
            try
            {
                result.rollback_attempted = true;
                string root = Path.GetFullPath(request.isolated_root);
                string output = Path.GetFullPath(request.output_dir);
                if (!IsUnderRoot(output, root) || PathsEqual(output, root))
                {
                    result.rollback_refused_reason = "ROLLBACK_SCOPE_UNPROVEN";
                    return;
                }
                if (ProcessIds("SLDWORKS").Count != 0 || ProcessIds("sldProcMon").Count != 0)
                {
                    result.rollback_refused_reason = "CAD_PROCESS_OWNERSHIP_NOT_CLEAN";
                    return;
                }
                RequireSafeMutationPath(output, root, "ROLLBACK_PATH_UNSAFE");
                if (Directory.Exists(output))
                {
                    RequireSafeRecursiveDeleteTree(output, root);
                    Directory.Delete(output, true);
                }
                else if (File.Exists(output))
                {
                    RequireSafeReadPath(output, root, "ROLLBACK_PATH_UNSAFE");
                    File.Delete(output);
                }
                result.rollback_completed = !Directory.Exists(output) && !File.Exists(output);
            }
            catch (Exception ex)
            {
                result.rollback_completed = false;
                result.rollback_refused_reason = SafeExceptionText(ex);
            }
        }

        private static void TryWriteResult(string path, RunResult result, DoorRequest request)
        {
            try
            {
                if (request != null && !string.IsNullOrWhiteSpace(request.isolated_root) &&
                    !string.IsNullOrWhiteSpace(path) &&
                    IsUnderRoot(path, request.isolated_root))
                {
                    RequireSafeMutationPath(path, request.isolated_root, "EVIDENCE_PATH_UNSAFE");
                    if (File.Exists(path) || Directory.Exists(path)) return;
                    result.failure_evidence_write_succeeded = true;
                    result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                    WriteJsonAtomicNew(path, result);
                }
            }
            catch { }
        }

        private static string SafeExceptionText(Exception ex)
        {
            try { return ex == null ? "" : ex.ToString(); }
            catch { return "unprintable exception"; }
        }

        private static T Safe<T>(Func<T> action, T fallback)
        {
            try { return action(); }
            catch { return fallback; }
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch { }
        }

        private static bool TryAction(Action action)
        {
            try { action(); return true; }
            catch { return false; }
        }

        private static void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); }
            catch { }
        }

        private static void Require(bool condition, string code, int exitCode, string message)
        {
            if (!condition) throw new GateException(code, exitCode, message);
        }
    }

    internal sealed class GateException : Exception
    {
        internal readonly string Code;
        internal readonly int ExitCode;
        internal GateException(string code, int exitCode, string message) : base(message)
        {
            Code = code;
            ExitCode = exitCode;
        }
    }

    public sealed class DoorRequest
    {
        public string schema { get; set; }
        public string task_id { get; set; }
        public string worker_id { get; set; }
        public string lease_id { get; set; }
        public double cabinet_width_mm { get; set; }
        public double door_width_mm { get; set; }
        public double door_height_mm { get; set; }
        public int columns { get; set; }
        public int rows_per_column { get; set; }
        public int total_doors { get; set; }
        public string isolated_root { get; set; }
        public string output_dir { get; set; }
        public string left_panel_proof { get; set; }
        public string right_panel_proof { get; set; }
        public string right_panel_strategy { get; set; }
        public bool visible { get; set; }
        public SourceSet sources { get; set; }
    }

    public sealed class SourceSet
    {
        public SourceSpec left_panel_seed { get; set; }
        public SourceSpec right_panel_seed { get; set; }
        public SourceSpec stiffener_seed { get; set; }
        public SourceSpec latch_plate { get; set; }
        public SourceSpec hook_pad { get; set; }
        public SourceSpec bushing { get; set; }
        public SourceSpec hinge_pin { get; set; }
        public SourceSpec circlip { get; set; }
        public SourceSpec mechanical_lock_tongue { get; set; }
    }

    public sealed class SourceSpec
    {
        public string path { get; set; }
        public string sha256 { get; set; }
    }

    public sealed class IsolationSentinel
    {
        public string schema { get; set; }
        public string task_id { get; set; }
        public bool allow_native_solidworks_write { get; set; }
    }

    public sealed class NativeBuildPlan
    {
        public string schema { get; set; }
        public string purpose { get; set; }
        public string workerId { get; set; }
        public NativePlanTask task { get; set; }
        public NativePlanRequest request { get; set; }
        public NativePlanRecipe recipe { get; set; }
        public NativePlanGeometry geometry { get; set; }
        public List<string> requiredChecks { get; set; }
        public NativePlanQualityBoundary qualityBoundary { get; set; }
        public NativePlanExecutionBoundary executionBoundary { get; set; }
    }

    public sealed class NativePlanTask
    {
        public string id { get; set; }
        public int revisionAtPlanning { get; set; }
        public string digest { get; set; }
    }

    public sealed class NativePlanRequest
    {
        public string fingerprint { get; set; }
        public string digest { get; set; }
    }

    public sealed class NativePlanRecipe
    {
        public string id { get; set; }
        public int version { get; set; }
        public string digest { get; set; }
    }

    public sealed class NativePlanGeometry
    {
        public double cabinetWidthMm { get; set; }
        public double cabinetHeightMm { get; set; }
        public double cabinetDepthMm { get; set; }
        public int columns { get; set; }
        public int doorCount { get; set; }
        public int[] columnDoorCounts { get; set; }
        public string rowSequence { get; set; }
        public double doorPanelWidthMm { get; set; }
    }

    public sealed class NativePlanQualityBoundary
    {
        public bool planningOnly { get; set; }
        public bool engineeringAssistanceReady { get; set; }
        public bool readyOnlyAfterEveryRequiredCheckPasses { get; set; }
    }

    public sealed class NativePlanExecutionBoundary
    {
        public bool executorImplemented { get; set; }
        public bool cadStarted { get; set; }
        public bool modelGenerated { get; set; }
        public bool modelReady { get; set; }
        public bool legacyFallbackUsed { get; set; }
    }

    public sealed class ExecutionAuthorization
    {
        public string schema { get; set; }
        public string authorizationId { get; set; }
        public string issuedAt { get; set; }
        public string expiresAt { get; set; }
        public string purpose { get; set; }
        public string workerId { get; set; }
        public ExecutionAuthorizationTask task { get; set; }
        public ExecutionAuthorizationRequest request { get; set; }
        public ExecutionAuthorizationPlan plan { get; set; }
        public ExecutionAuthorizationRecipe recipe { get; set; }
        public ExecutionAuthorizationTool tool { get; set; }
        public ExecutionAuthorizationSeed seed { get; set; }
        public ExecutionAuthorizationExecution execution { get; set; }
        public ExecutionAuthorizationQualityBoundary qualityBoundary { get; set; }
    }

    public sealed class ExecutionAuthorizationTask
    {
        public string id { get; set; }
        public int revision { get; set; }
        public string digest { get; set; }
        public string leaseId { get; set; }
        public string leaseExpiresAt { get; set; }
    }

    public sealed class ExecutionAuthorizationRequest
    {
        public string fingerprint { get; set; }
        public string digest { get; set; }
    }

    public sealed class ExecutionAuthorizationTool
    {
        public string id { get; set; }
        public string sourceNormalizedSha256 { get; set; }
        public string executableSha256 { get; set; }
    }

    public sealed class ExecutionAuthorizationPlan
    {
        public string sha256 { get; set; }
    }

    public sealed class ExecutionAuthorizationRecipe
    {
        public string id { get; set; }
        public int version { get; set; }
        public string digest { get; set; }
    }

    public sealed class ExecutionAuthorizationSeed
    {
        public string inventoryDigest { get; set; }
    }

    public sealed class ExecutionAuthorizationExecution
    {
        public bool authorized { get; set; }
        public int attempt { get; set; }
        public List<string> phases { get; set; }
    }

    public sealed class ExecutionAuthorizationQualityBoundary
    {
        public bool engineeringAssistanceReady { get; set; }
        public bool readyOnlyAfterEveryRequiredCheckPasses { get; set; }
    }

    public sealed class FlatImportManifest
    {
        public string schema { get; set; }
        public bool immutable { get; set; }
        public int cadFileCount { get; set; }
        public int v37WorkingPackFileCount { get; set; }
        public string v37WorkingPackInventoryDigest { get; set; }
        public string workingPackImportState { get; set; }
        public string rootAssemblerBoundary { get; set; }
        public List<FlatImportEntry> entries { get; set; }
    }

    public sealed class FlatImportEntry
    {
        public string canonicalRelativePath { get; set; }
        public string flatTargetName { get; set; }
        public long sizeBytes { get; set; }
        public string sha256 { get; set; }
        public string category { get; set; }
    }

    public sealed class NativePanelProof
    {
        public string schema { get; set; }
        public string handedness { get; set; }
        public string construction { get; set; }
        public string task_id { get; set; }
        public int attempt { get; set; }
        public string authorization_id { get; set; }
        public string authorization_sha256 { get; set; }
        public string tool_source_normalized_sha256 { get; set; }
        public string tool_executable_sha256 { get; set; }
        public string source_path { get; set; }
        public string source_sha256 { get; set; }
        public string reopen_sha256 { get; set; }
        public int solidworks_major { get; set; }
        public int body_count { get; set; }
        public bool has_sheet_metal { get; set; }
        public bool has_flat_pattern { get; set; }
        public int error_feature_count { get; set; }
        public int warning_feature_count { get; set; }
        public int foreign_feature_count { get; set; }
        public bool traversal_complete { get; set; }
        public int external_reference_count { get; set; }
        public bool read_only_reopen_verified { get; set; }
        public string height_dimension { get; set; }
        public string width_dimension { get; set; }
        public int height_dimension_state { get; set; }
        public int width_dimension_state { get; set; }
        public string generated_at_utc { get; set; }
        public List<SourceSpec> evidence_files { get; set; }
        public string evidence_commitment_sha256 { get; set; }
    }

    internal sealed class SourceRow
    {
        internal readonly string role;
        internal readonly SourceSpec spec;
        internal SourceRow(string roleValue, SourceSpec specValue)
        {
            role = roleValue;
            spec = specValue;
        }
    }

    internal sealed class Placement
    {
        internal readonly string role;
        internal readonly string path;
        internal readonly double[] rotation;
        internal readonly double tx_mm;
        internal readonly double ty_mm;
        internal readonly double tz_mm;
        internal Placement(string roleValue, string pathValue, double[] rotationValue,
            double tx, double ty, double tz)
        {
            role = roleValue;
            path = pathValue;
            rotation = rotationValue;
            tx_mm = tx;
            ty_mm = ty;
            tz_mm = tz;
        }
    }

    internal sealed class BuildPaths
    {
        internal string left_panel;
        internal string right_panel;
        internal string left_panel_sha256;
        internal string right_panel_sha256;
        internal string stiffener;
        internal string latch_plate;
        internal string hook_pad;
        internal string bushing;
        internal string hinge_pin;
        internal string circlip;
        internal string lock_tongue;
        internal string left_weld;
        internal string right_weld;
        internal string left_door;
        internal string right_door;

        internal BuildPaths Relocated(string oldRoot, string newRoot)
        {
            return new BuildPaths
            {
                left_panel = ReplaceRoot(left_panel, oldRoot, newRoot),
                right_panel = ReplaceRoot(right_panel, oldRoot, newRoot),
                left_panel_sha256 = left_panel_sha256,
                right_panel_sha256 = right_panel_sha256,
                stiffener = ReplaceRoot(stiffener, oldRoot, newRoot),
                latch_plate = ReplaceRoot(latch_plate, oldRoot, newRoot),
                hook_pad = ReplaceRoot(hook_pad, oldRoot, newRoot),
                bushing = ReplaceRoot(bushing, oldRoot, newRoot),
                hinge_pin = ReplaceRoot(hinge_pin, oldRoot, newRoot),
                circlip = ReplaceRoot(circlip, oldRoot, newRoot),
                lock_tongue = ReplaceRoot(lock_tongue, oldRoot, newRoot),
                left_weld = ReplaceRoot(left_weld, oldRoot, newRoot),
                right_weld = ReplaceRoot(right_weld, oldRoot, newRoot),
                left_door = ReplaceRoot(left_door, oldRoot, newRoot),
                right_door = ReplaceRoot(right_door, oldRoot, newRoot)
            };
        }

        private static string ReplaceRoot(string path, string oldRoot, string newRoot)
        {
            string oldPrefix = Path.GetFullPath(oldRoot).TrimEnd('\\', '/') +
                Path.DirectorySeparatorChar;
            string canonical = Path.GetFullPath(path);
            if (!canonical.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("path is outside old root: " + path);
            return Path.Combine(Path.GetFullPath(newRoot), canonical.Substring(oldPrefix.Length));
        }
    }

    internal sealed class OwnedSession
    {
        internal string phase;
        internal ISldWorks sw;
        internal int process_id;
        internal long process_start_ticks_utc;
        internal string executable_path;
        internal string solidworks_revision;
        internal List<int> baseline_process_ids;
        internal List<int> baseline_monitor_ids;
        internal string started_at_utc;
    }

    public sealed class RunResult
    {
        public RunResult()
        {
            cad_process_baseline_sldworks = new List<int>();
            cad_process_baseline_sldprocmon = new List<int>();
            denied_v37_right_hashes = new List<string>();
            sources = new List<SourceEvidence>();
            panel_proofs = new List<PanelProofEvidence>();
            backups = new List<BackupEvidence>();
            part_edits = new List<PartEditEvidence>();
            assemblies = new List<AssemblyEvidence>();
            sessions = new List<SessionEvidence>();
            reopen_checks = new List<ReopenEvidence>();
            output_files = new List<FileHashEvidence>();
            toolchain_manifests = new List<TrustedArtifactEvidence>();
            predecessor_receipts = new List<TrustedArtifactEvidence>();
        }

        public string schema { get; set; }
        public string generated_at_utc { get; set; }
        public string mode { get; set; }
        public string request_path { get; set; }
        public string evidence_path { get; set; }
        public string task_id { get; set; }
        public string worker_id { get; set; }
        public string lease_id { get; set; }
        public string plan_worker_id { get; set; }
        public int attempt_number { get; set; }
        public bool trusted_attempt_gate { get; set; }
        public bool stage_receipt_contract_gate { get; set; }
        public bool trusted_toolchain_manifest_gate { get; set; }
        public bool predecessor_receipt_chain_gate { get; set; }
        public bool plan_execution_authorized { get; set; }
        public string execution_authorization_path { get; set; }
        public string execution_authorization_sha256 { get; set; }
        public string execution_authorization_expires_at_utc { get; set; }
        public string task_lease_expires_at_utc { get; set; }
        public string authorization_id { get; set; }
        public string authorization_json_base64 { get; set; }
        public string authorization_issued_at_utc { get; set; }
        public string plan_path { get; set; }
        public string plan_sha256 { get; set; }
        public int task_revision_at_planning { get; set; }
        public string task_digest { get; set; }
        public string request_fingerprint { get; set; }
        public string request_digest { get; set; }
        public string recipe_id { get; set; }
        public string recipe_digest { get; set; }
        public string tool_source_normalized_sha256 { get; set; }
        public string tool_executable_sha256 { get; set; }
        public string working_pack_path { get; set; }
        public string current_pre_inventory_digest { get; set; }
        public string post_inventory_digest { get; set; }
        public string predecessor_receipt_sha256 { get; set; }
        public double target_cabinet_width_mm { get; set; }
        public double target_width_mm { get; set; }
        public double target_height_mm { get; set; }
        public double target_stiffener_length_mm { get; set; }
        public string status { get; set; }
        public bool success { get; set; }
        public bool preflight_passed { get; set; }
        public bool execution_started { get; set; }
        public bool completed { get; set; }
        public bool committed { get; set; }
        public string evidence_commit_started_at_utc { get; set; }
        public string evidence_commit_completed_at_utc { get; set; }
        public string completed_at_utc { get; set; }
        public string commit_completed_at_utc { get; set; }
        public string evidence_commitment_sha256 { get; set; }
        public bool evidence_write_attempted { get; set; }
        public bool evidence_write_succeeded { get; set; }
        public bool evidence_finalize_write_succeeded { get; set; }
        public string evidence_finalize_error { get; set; }
        public bool failure_evidence_write_succeeded { get; set; }
        [ScriptIgnore]
        public string evidence_sha256 { get; set; }
        [ScriptIgnore]
        public string receipt_path { get; set; }
        [ScriptIgnore]
        public string receipt_sha256 { get; set; }
        [ScriptIgnore]
        public bool receipt_write_attempted { get; set; }
        [ScriptIgnore]
        public bool evidence_artifact_owned { get; set; }
        [ScriptIgnore]
        public bool receipt_artifact_owned { get; set; }
        public string transaction_root { get; set; }
        public string staging_root { get; set; }
        public string relocation_probe_root { get; set; }
        public string committed_output_dir { get; set; }
        public string flat_import_manifest_path { get; set; }
        public string flat_import_manifest_sha256 { get; set; }
        public bool working_pack_imported { get; set; }
        public string root_assembler_boundary { get; set; }
        public bool relocation_reopen_passed { get; set; }
        public bool committed_reopen_passed { get; set; }
        public string error_code { get; set; }
        public string error { get; set; }
        public string note { get; set; }
        public string right_panel_strategy { get; set; }
        public string right_panel_route_gate { get; set; }
        public string mechanical_lock_tongue_boundary { get; set; }
        public MirrorPartEvidence mirror_part { get; set; }
        public List<int> cad_process_baseline_sldworks { get; set; }
        public List<int> cad_process_baseline_sldprocmon { get; set; }
        public List<string> denied_v37_right_hashes { get; set; }
        public List<SourceEvidence> sources { get; set; }
        public List<PanelProofEvidence> panel_proofs { get; set; }
        public List<BackupEvidence> backups { get; set; }
        public List<PartEditEvidence> part_edits { get; set; }
        public List<AssemblyEvidence> assemblies { get; set; }
        public List<SessionEvidence> sessions { get; set; }
        public List<ReopenEvidence> reopen_checks { get; set; }
        public List<FileHashEvidence> output_files { get; set; }
        public List<TrustedArtifactEvidence> toolchain_manifests { get; set; }
        public List<TrustedArtifactEvidence> predecessor_receipts { get; set; }
        public bool rollback_attempted { get; set; }
        public bool rollback_completed { get; set; }
        public string rollback_refused_reason { get; set; }
        public List<int> activation_delta_process_ids { get; set; }
        public bool activation_cleanup_attempted { get; set; }
        public bool activation_cleanup_proven_owned { get; set; }
        public bool activation_cleanup_completed { get; set; }
        public bool activation_cleanup_preserved_unowned { get; set; }
        public string activation_prog_id { get; set; }
    }

    public sealed class TrustedArtifactEvidence
    {
        public string id { get; set; }
        public string schema { get; set; }
        public string path { get; set; }
        public string sha256 { get; set; }
    }

    public sealed class SourceEvidence
    {
        public string role { get; set; }
        public string path { get; set; }
        public string expected_sha256 { get; set; }
        public string actual_sha256 { get; set; }
        public long size_bytes { get; set; }
        public bool inside_isolated_root { get; set; }
    }

    public sealed class PanelProofEvidence
    {
        public string handedness { get; set; }
        public string path { get; set; }
        public string sha256 { get; set; }
        public string source_sha256 { get; set; }
        public int evidence_count { get; set; }
        public bool gate { get; set; }
    }

    public sealed class BackupEvidence
    {
        public string role { get; set; }
        public string source_path { get; set; }
        public string backup_path { get; set; }
        public string source_sha256 { get; set; }
        public string backup_sha256 { get; set; }
    }

    public sealed class PartHealth
    {
        public int body_count { get; set; }
        public int feature_count { get; set; }
        public bool has_sheet_metal { get; set; }
        public bool has_flat_pattern { get; set; }
        public int mirror_part_feature_count { get; set; }
        public int foreign_feature_count { get; set; }
        public int error_feature_count { get; set; }
        public int warning_feature_count { get; set; }
        public bool traversal_complete { get; set; }
        public double x_length_mm { get; set; }
        public double y_length_mm { get; set; }
        public double z_length_mm { get; set; }
        public bool bbox_from_body_extreme_points { get; set; }
    }

    public sealed class PartEditEvidence
    {
        public string role { get; set; }
        public string path { get; set; }
        public double target_width_mm { get; set; }
        public double target_height_mm { get; set; }
        public PartHealth before { get; set; }
        public PartHealth after { get; set; }
        public string sha256_after_save { get; set; }
    }

    public sealed class MirrorPartEvidence
    {
        public string source_path { get; set; }
        public string output_path { get; set; }
        public string api { get; set; }
        public bool break_link { get; set; }
        public int options { get; set; }
        public bool import_solids { get; set; }
        public bool import_sheet_metal_information { get; set; }
        public bool import_individual_properties { get; set; }
        public bool import_cut_list_properties { get; set; }
        public string selected_plane { get; set; }
        public string mirror_feature_type { get; set; }
        public int external_reference_count { get; set; }
        public PartHealth after { get; set; }
        public string sha256_after_save { get; set; }
        public bool gate { get; set; }
    }

    public sealed class PlacementEvidence
    {
        public string role { get; set; }
        public string path { get; set; }
        public string sha256 { get; set; }
        public string component_name { get; set; }
        public bool opened { get; set; }
        public bool added { get; set; }
        public bool transform_applied { get; set; }
        public double[] rotation { get; set; }
        public double tx_mm { get; set; }
        public double ty_mm { get; set; }
        public double tz_mm { get; set; }
    }

    public sealed class AssemblyEvidence
    {
        public string role { get; set; }
        public string path { get; set; }
        public int expected_component_count { get; set; }
        public bool rebuilt { get; set; }
        public bool saved { get; set; }
        public string sha256_after_save { get; set; }
        public List<PlacementEvidence> placements { get; set; }
    }

    public sealed class SessionEvidence
    {
        public string phase { get; set; }
        public int process_id { get; set; }
        public long process_start_ticks_utc { get; set; }
        public string executable_path { get; set; }
        public string solidworks_revision { get; set; }
        public string started_at_utc { get; set; }
        public string completed_at_utc { get; set; }
        public bool exclusive_identity_gate { get; set; }
        public bool exit_requested { get; set; }
        public bool process_exited { get; set; }
        public bool cad_baseline_restored { get; set; }
    }

    public sealed class ReopenEvidence
    {
        public string phase { get; set; }
        public string role { get; set; }
        public string path { get; set; }
        public string sha256 { get; set; }
        public int reference_count { get; set; }
        public bool transform_gate { get; set; }
        public bool gate { get; set; }
    }

    public sealed class FileHashEvidence
    {
        public string relative_path { get; set; }
        public long size_bytes { get; set; }
        public string sha256 { get; set; }
    }
}
