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

namespace Winnsen.StructureAgent.NativeSeed888x14
{
    internal static class SeedPack888Native
    {
        private const string ToolId = "native_seed_pack_888x14_v1";
        private const string ExecutionPhase = "clone_native_seed";
        private const string Purpose = "structure_engineering_assistance";
        private const string PlanSchema = "winnsen.native_build_plan.v1";
        private const string AuthorizationSchema = "winnsen.native_execution_authorization.v1";
        private const string ReceiptSchema = "winnsen.16029.native_seed_pack_receipt.v1";
        private const string EvidenceSchema = "winnsen.16029.native_seed_pack_result.v1";
        private const string ToolchainManifestSchema = "winnsen.16029.native_toolchain_manifest.v1";
        private const string LockToolId = "native_lock_topology_888x14_v1";
        private const string LockToolchainManifestSchema =
            "winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1";
        private const string LockToolchainManifestGeneratedBy =
            "VerifyLockTopology888x14Static.mjs";
        private const string LockToolchainValidatorRelativePath =
            "workers/native_model_requests/development/v1/tools/ValidateLockTopology888x14.mjs";
        private const string ExpectedRecipeId = "winnsen-16029-888w-14door-native-v1";
        private const string ExpectedRecipeDigest =
            "F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8";
        private const string ExpectedSourceSha256 = "D7E0C9D2BAEA091CE0071DF2F42E6DD4CE67A4C173D47E9AC13A8EB0A0371C71";
        private const string ExpectedSourceInventoryDigest =
            "9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B";
        private const string RootFileName = "标准寄存柜1917×760×550(总装配).SLDASM";
        private const string TopCoverAssemblyFileName = "上盖焊接.SLDASM";
        // Temporary experiments are explicitly nonconsumable and remain dormant.
        // The registered seed tool must execute only the formal clone/stabilize stage.
        private static readonly bool NativeTopCoverPartMeasurementProbeOnly = false;
        // Temporary, nonconsumable experiment: detach only the five physical top-cover
        // plates in the isolated direct-copy pack.  It never touches an assembly/root.
        private static readonly bool NativeTopCoverPartDetachProbeOnly = false;
        private static readonly bool NativeTopCoverRebuildProbeOnly = false;
        private static readonly bool NativeTopCoverRebuildCaptureProbeOnly = false;
        private static readonly bool NativeTopCoverMasterReferenceCaptureProbeOnly = false;
        // B-rep capture is a strictly read-only, attempt-local comparison probe.  It is
        // deliberately nonconsumable and stops before shared evidence or any CAD mutation.
        private static readonly bool NativeTopCoverFrontBrepCaptureProbeOnly = false;
        private const string NativeTopCoverPartMeasurementPrivateRelativePath =
            "evidence/private/topcover_native_part_measurement_probe_only.json";
        private const string NativeTopCoverPartMeasurementCleanupPrivateRelativePath =
            "evidence/private/topcover_native_part_measurement_probe_cleanup.json";
        private const string NativeTopCoverPartDetachPrivateRelativePath =
            "evidence/private/topcover_native_part_detach_probe_only.json";
        private const string NativeTopCoverPartDetachCleanupPrivateRelativePath =
            "evidence/private/topcover_native_part_detach_probe_cleanup.json";
        private const string NativeTopCoverProbePrivateRelativePath =
            "evidence/private/topcover_native_rebuild_probe_only.json";
        private const string NativeTopCoverProbeCleanupPrivateRelativePath =
            "evidence/private/topcover_native_rebuild_probe_cleanup.json";
        private const string NativeTopCoverRebuildCapturePrivateRelativePath =
            "evidence/private/topcover_native_rebuild_capture_probe_only.json";
        private const string NativeTopCoverRebuildCaptureCleanupPrivateRelativePath =
            "evidence/private/topcover_native_rebuild_capture_probe_cleanup.json";
        private const string NativeTopCoverMasterReferenceCapturePrivateRelativePath =
            "evidence/private/topcover_native_master_reference_capture_probe_only.json";
        private const string NativeTopCoverMasterReferenceCaptureCleanupPrivateRelativePath =
            "evidence/private/topcover_native_master_reference_capture_probe_cleanup.json";
        private const string NativeTopCoverFrontBrepCapturePrivateRelativePath =
            "evidence/private/topcover_native_front_brep_capture_probe_only.json";
        private const string NativeTopCoverFrontBrepCaptureCleanupPrivateRelativePath =
            "evidence/private/topcover_native_front_brep_capture_probe_cleanup.json";
        private const string GoldTopCoverEngineeringRelativeDirectory =
            "workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图";
        private const string KnownGoldBottomHistoricalAssemblyReference =
            @"C:\Users\ys8353\Desktop\DB2\标准寄存柜(总装配).SLDASM";
        private static readonly string[] TopCoverCandidateUpstreamFirstFileNames =
        {
            "标准寄存柜 模型.SLDPRT", "上盖 模型.sldprt",
            "上盖壳体左侧板.sldprt", "上盖壳体右侧板.SLDPRT",
            "上盖壳体前侧板.sldprt", "上盖壳体后侧板.sldprt",
            "上盖壳体底板.sldprt"
        };
        private static readonly string[] TopCoverCandidateDownstreamFirstFileNames =
        {
            "上盖壳体前侧板.sldprt", "上盖壳体后侧板.sldprt",
            "上盖壳体底板.sldprt", "上盖壳体右侧板.SLDPRT",
            "上盖壳体左侧板.sldprt", "上盖 模型.sldprt",
            "标准寄存柜 模型.SLDPRT"
        };
        private static readonly Dictionary<string, string> ExpectedGoldTopCoverSha256 =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "标准寄存柜 模型.SLDPRT", "2D00EBDDA5CE93A83358ADB9E910AA3E023A228AA5E5233894D461EC42223628" },
            { "上盖 模型.sldprt", "4E9AAF6BD6422D7330B5CE33BDCBFF8651E1AA5CD68D84EE90DE9B20FD770625" },
            { "上盖焊接.SLDASM", "FAA607FB615F186EA7B772771B6ACA38BFD22D947CAC4DE6F56D5AFC7FDCC615" },
            { "上盖焊接.SLDDRW", "069047FB00749382669304832402F74A6E24B769448E454FAC98EF3BEB8C8C45" },
            { "上盖壳体左侧板.sldprt", "2DD83A0A1C9FE880B460177F5CD8191B2324738A1E141C652F024DC077A3CBCE" },
            { "上盖壳体左侧板.SLDDRW", "05F6204627D6CEFEABCACD66EDAA6FE7EA6F666F43BCA4096CB5AA1009D1B941" },
            { "上盖壳体右侧板.SLDPRT", "380F2D8DC419F8BDEC809319FC1AB14F0AD279038E50362AA8E1C7E7BA70A679" },
            { "上盖壳体右侧板.SLDDRW", "9FBE33BCE7813DF6FCB14D569075416ABD48C61C12579C2D66091AA7F60EF0BA" },
            { "上盖壳体前侧板.sldprt", "3B7290BE78F0BE495E063C0392F7260339FFEC75CB890ED12E64CF07602FF354" },
            { "上盖壳体前侧板.SLDDRW", "AEC21D458472D74B6AC0B6F86F12CA07593CA70552283919E004487FDA3A011B" },
            { "上盖壳体后侧板.sldprt", "834C35D04C265CB3C26E4469F93A346B37089ED42D4EA534E109CE6B768FB5F5" },
            { "上盖壳体后侧板.SLDDRW", "995649F7BC0268DB0B8787DF6D86D50E3C9D156FDE6622C16A995B96DFF74177" },
            { "上盖壳体底板.sldprt", "8972838A6F0B09DDEE0344C143A1B8941C9C9E6846DADD2470F2F3443718249C" },
            { "上盖壳体底板.SLDDRW", "593527C99FFF60515B9CA8399AE11F8C89C13507741BBE5F1A3C5DD01E20B1FB" }
        };
        private static readonly string[] GoldTopCoverCaptureCadFileNames =
        {
            "标准寄存柜 模型.SLDPRT", "上盖 模型.sldprt", "上盖焊接.SLDASM",
            "上盖壳体左侧板.sldprt", "上盖壳体右侧板.SLDPRT",
            "上盖壳体前侧板.sldprt", "上盖壳体后侧板.sldprt",
            "上盖壳体底板.sldprt"
        };
        private static readonly Dictionary<string, string> ExpectedGoldTopCoverDxfSha256 =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "上盖壳体左侧板展开图.DXF", "8160ECC4E9051BD541929C1F4C73A4DCF5B747AB666A05111CA9A8705806287A" },
            { "上盖壳体右侧板展开图.DXF", "DE8FF1A6A1C99E537310769BB04A684BAFAAA7E15C225B331E563F130EF41D26" },
            { "上盖壳体前侧板展开图.DXF", "1AC67CA04FD2F95AC32CB809E199220C9D269F3B72C705AE57542C1CA5041DD1" },
            { "上盖壳体后侧板展开图.DXF", "7D7415A4E157D96DCC265430D7C803354C7657C00554D1134890E958FE937459" },
            { "上盖壳体底板展开图.DXF", "BAD01389D1EA57CCC75F555DA330F5C09DFCDB3F39C2E78E4B84F66111BD131D" }
        };
        private static readonly string[] RootDoorFrameCandidateFileNames =
        {
            "门框 下.sldprt"
        };
        private static readonly string[] RootRightPartitionCandidateFileNames =
        {
            "箱体竖隔板L.sldprt", "箱体竖隔板R.SLDPRT",
            "箱体竖隔板L焊接.SLDASM", "箱体竖隔板R焊接.SLDASM"
        };
        private static readonly string[] RootBaseBodyCandidateFileNames =
        {
            "储物柜门板2╱12_右.SLDPRT", "储物柜门板4╱12_右.SLDPRT",
            "储物柜门板6╱12_右.SLDPRT", "插销固定板2╱12_左.SLDPRT",
            "插销固定板2╱12_右.SLDPRT", "插销固定板4╱12_左.SLDPRT",
            "插销固定板4╱12_右.SLDPRT", "插销固定板6╱12_左.SLDPRT",
            "插销固定板6╱12_右.SLDPRT", "开口挡圈5.SLDPRT", "锁舌.SLDPRT"
        };
        private const string ExpectedRootSha256 =
            "5AED314E89A65A3875C517B2F4DC179635E9C684E0CD1877AFEBD33CAA77CA5B";
        private const string KnownIssueName = "箱体右侧板焊接-1";
        private const string KnownIssueType = "Reference";
        private const int ExpectedCadCount = 75;
        private const int ExpectedAssemblyCount = 22;
        private const int ExpectedPartCount = 53;
        private const int ExpectedDependencyRawCount = 148;
        private const int ExpectedTopLevelComponents = 25;
        private const int ExpectedRecursiveComponents = 158;
        private const int ExpectedHierarchyMaxDepth = 2;
        private const int ExpectedFeatureCount = 59;
        private const string EvidenceRelativePath = "evidence/seed_pack_888_native_v1.json";
        private const string ReceiptRelativePath = "receipts/clone_native_seed.json";
        private const string AttemptsRootEnvironment = "WINNSEN_NATIVE_16029_ATTEMPTS_ROOT";
        private const string PlanShaEnvironment = "WINNSEN_NATIVE_16029_PLAN_SHA256";
        private const string AuthorizationPathEnvironment = "WINNSEN_NATIVE_EXECUTION_AUTH_PATH";
        private const string AuthorizationShaEnvironment = "WINNSEN_NATIVE_EXECUTION_AUTH_SHA256";
        private const string ExpectedSolidWorksExePath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string ExpectedSolidWorksExeSha256 =
            "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string ExpectedSolidWorksVersionPrefix = "28.";
        private const string ExpectedMonitorExePath = @"D:\soildworks2020\SOLIDWORKS\sldProcMon.exe";
        private const string ExpectedMonitorExeSha256 =
            "A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70";
        private static FileStream attemptLock;
        private static FileStream planReadLock;

        private static readonly string[] AuthorizationKeys =
        {
            "schema", "authorizationId", "issuedAt", "expiresAt", "purpose", "workerId",
            "task", "request", "plan", "recipe", "tool", "seed", "execution",
            "qualityBoundary"
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

        private static readonly string[] EvidenceCommitmentExcludedKeys =
        {
            "evidence_write_attempted", "evidence_write_succeeded",
            "evidence_finalize_write_succeeded", "evidence_finalize_error",
            "failure_evidence_write_succeeded", "phase_receipt_path",
            "phase_receipt_sha256", "phase_receipt_committed", "authorization_checkpoints",
            "completed_at_utc", "commit_completed_at_utc", "evidence_commitment_sha256",
            "backup_deleted", "backup_delete_error"
        };

        private static readonly string[] StageReceiptOrder =
        {
            "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14",
            "final_pack_and_relocated_reopen"
        };

        private static readonly string[] StageReceiptSchemas =
        {
            ReceiptSchema, "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.native_width_888.phase_receipt.v1",
            "winnsen.16029.native_door_module_receipt.v1",
            "winnsen.16029.native_lock_topology_receipt.v1",
            "winnsen.16029.native_root_assembly_receipt.v1",
            "winnsen.16029.native_final_pack_receipt.v1"
        };

        private static readonly string[] StageReceiptPaths =
        {
            ReceiptRelativePath, "evidence/width-dimensions.receipt.json",
            "evidence/width-derived.receipt.json", "evidence/width-base-hole.receipt.json",
            "evidence/width-assemblies.receipt.json", "receipts/door_module_888x14.json",
            "receipts/lock_topology_888x14.json", "receipts/root_assembly_888x14.json",
            "receipts/final_pack_and_relocated_reopen.json"
        };

        private static readonly string[] StageReceiptTools =
        {
            ToolId, "native_width_888_v1", "native_width_888_v1", "native_width_888_v1",
            "native_width_888_v1", "native_door_module_888x14_v1",
            "native_lock_topology_888x14_v1", "native_root_assembly_888x14_v1",
            "native_final_pack_888x14_v1"
        };

        private static readonly string[] StageReceiptPredecessors =
        {
            "", "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14"
        };

        private static readonly string[] TrustedManifestKeys =
        {
            ToolId, "native_width_888_v1", "native_door_module_888x14_v1",
            LockToolId, "native_root_assembly_888x14_v1",
            "native_final_pack_888x14_v1"
        };

        private static readonly string[] LockToolchainToolIds =
        {
            LockToolId, "native_lock_topology_inspector_v1",
            "native_assembly_tongue_inspector_888x14_v1"
        };

        private static readonly string[] LockToolchainSourceRelativePaths =
        {
            "workers/native_model_requests/development/v1/tools/BuildLockTopology888x14.cs",
            "workers/native_model_requests/development/v1/tools/InspectLockTopology888x14.cs",
            "workers/native_model_requests/development/v1/tools/InspectAssemblyTongues888x14.cs"
        };

        private static readonly string[] LockToolchainExecutableRelativePaths =
        {
            "workers/native_model_requests/development/v1/tools/bin/BuildLockTopology888x14.exe",
            "workers/native_model_requests/development/v1/tools/bin/InspectLockTopology888x14.exe",
            "workers/native_model_requests/development/v1/tools/bin/InspectAssemblyTongues888x14.exe"
        };

        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (args.Length == 1 && string.Equals(args[0], "--self-test",
                    StringComparison.OrdinalIgnoreCase)) return RunSelfTests();
            if (args.Length == 1 && string.Equals(args[0], "--identity",
                    StringComparison.OrdinalIgnoreCase))
            {
                PrintIdentity();
                return 0;
            }
            if (args.Any(value => string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)))
            {
                PrintUsage();
                return 0;
            }

            string source = FullPathOrEmpty(Argument(args, "--source"));
            string target = FullPathOrEmpty(Argument(args, "--working-pack"));
            string output = FullPathOrEmpty(Argument(args, "--out"));
            string confirmTask = Argument(args, "--confirm-task") ?? "";
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) ||
                string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(confirmTask))
            {
                PrintUsage();
                return 2;
            }

            Result result = NewResult(source, target, output);
            int exitCode = 1;
            try
            {
                ValidatePreflight(result, confirmTask);
                AcquireAttemptLock(result);
                AssertAuthorizationStillValid(result, "before_direct_copy");
                DirectCopySeed(result);
                if (NativeTopCoverPartMeasurementProbeOnly)
                {
                    RunNativeTopCoverPartMeasurementProbeOnly(result);
                    Require(false, "TOPCOVER_NATIVE_PART_MEASUREMENT_COMPLETE", 69,
                        "nonconsumable native top-cover part measurement probe completed");
                }
                if (NativeTopCoverPartDetachProbeOnly)
                {
                    RunNativeTopCoverPartDetachProbeOnly(result);
                    Require(false, "TOPCOVER_NATIVE_PART_DETACH_COMPLETE", 68,
                        "nonconsumable native top-cover physical-part detach probe completed");
                }
                if (NativeTopCoverRebuildProbeOnly)
                {
                    RunNativeTopCoverRebuildProbeOnly(result);
                    Require(false, "TOPCOVER_NATIVE_REBUILD_PROBE_COMPLETE", 70,
                        "nonconsumable native top-cover rebuild probe completed");
                }
                if (NativeTopCoverRebuildCaptureProbeOnly)
                {
                    RunNativeTopCoverRebuildCaptureProbeOnly(result);
                    Require(false, "TOPCOVER_NATIVE_REBUILD_CAPTURE_PROBE_COMPLETE", 67,
                        "nonconsumable native top-cover rebuild capture probe completed");
                }
                if (NativeTopCoverMasterReferenceCaptureProbeOnly)
                {
                    RunNativeTopCoverMasterReferenceCaptureProbeOnly(result);
                    Require(false, "TOPCOVER_NATIVE_MASTER_REFERENCE_CAPTURE_COMPLETE", 66,
                        "nonconsumable native top-cover master-reference capture completed");
                }
                if (NativeTopCoverFrontBrepCaptureProbeOnly)
                {
                    RunNativeTopCoverFrontBrepCaptureProbeOnly(result);
                    Require(false, "TOPCOVER_NATIVE_FRONT_BREP_CAPTURE_COMPLETE", 65,
                        "nonconsumable native top-cover front B-rep capture completed");
                }
                AssertAuthorizationStillValid(result, "before_prewrite_relocation_diagnostic");
                RunRelocationPrewriteDiagnostic(result);
                AssertAuthorizationStillValid(result, "after_prewrite_relocation_diagnostic");
                AssertAuthorizationStillValid(result, "before_writable_stabilization");
                StabilizeWritableRoot(result);
                AssertAuthorizationStillValid(result, "before_fresh_readonly_reopen");
                VerifyFreshReadonly(result);
                CleanupOwnedProcesses(result);
                CaptureFinalProcessGate(result);
                Require(result.processes.final_gate, "FINAL_CAD_PROCESS_GATE_FAILED", 40,
                    "all owned and global SolidWorks processes must be absent before commit");
                ValidateFinalInventories(result);
                AssertAuthorizationStillValid(result, "before_evidence_commit");

                result.completed_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                Require(CompletionWithinAuthorization(result), "AUTHORIZATION_EXPIRED_BEFORE_COMMIT", 41,
                    "completion is outside authorization or lease expiry");
                result.success = true;
                result.status = "NATIVE_SEED_888X14_STABILIZED";
                result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                CommitEvidenceAndReceipt(result);
                AssertAuthorizationStillValid(result, "after_paired_commit");
                ValidateCommittedPair(result);
                result.pair_committed = true;
                exitCode = 0;
            }
            catch (StageException exception)
            {
                result.status = exception.Status;
                result.error = exception.Message;
                result.exit_code = exception.ExitCode;
                exitCode = exception.ExitCode;
            }
            catch (Exception exception)
            {
                result.status = "NATIVE_SEED_UNHANDLED_FAILURE";
                result.error = SafeException(exception);
                result.exit_code = 90;
                exitCode = 90;
            }
            finally
            {
                CleanupOwnedProcesses(result);
                CaptureFinalProcessGate(result);
                VerifySourceUnchanged(result);
                if (!result.source_unchanged)
                {
                    exitCode = 80;
                    result.exit_code = 80;
                    result.status = "P0_TRUSTED_SOURCE_MUTATION";
                    result.error = "protected V37 exact75 source changed during execution";
                }
                if (exitCode == 0 && (!result.processes.final_gate || !result.source_unchanged))
                {
                    exitCode = 44;
                    result.exit_code = 44;
                    result.status = "FINAL_SUCCESS_BOUNDARY_INVALID";
                    result.error = "CAD process or trusted source state changed after paired commit";
                }
                if (exitCode != 0)
                {
                    result.success = false;
                    RemoveUnpairedCommitArtifacts(result);
                    if (StableRollbackCadGate(result)) RollbackTarget(result);
                    else
                    {
                        result.rollback_attempted = false;
                        result.rollback_error =
                            "rollback refused because zero CAD processes could not be proven";
                    }
                    if (StableRollbackCadGate(result))
                        RollbackNativeTopCoverRepairStaging(result);
                    else if (result.topcover_probe_repair_root_created)
                        result.topcover_probe_repair_rollback_error =
                            "repair rollback refused because stable zero CAD was not reproven";
                    if (StableRollbackCadGate(result))
                        RollbackNativeTopCoverCapturePack(result);
                    else if (result.topcover_capture_pack_created)
                        result.topcover_capture_pack_rollback_error =
                            "gold capture rollback refused because stable zero CAD was not reproven";
                    if (NativeTopCoverRebuildProbeOnly && result.preflight_passed &&
                        !string.IsNullOrWhiteSpace(result.attempt_directory) &&
                        !string.IsNullOrWhiteSpace(result.attempts_root) &&
                        Directory.Exists(result.attempt_directory) &&
                        IsUnder(result.attempt_directory, result.attempts_root) &&
                        (result.target_created_by_this_run ||
                            result.topcover_probe_repair_root_created))
                        WriteNativeTopCoverProbeCleanupEvidence(result);
                    if (NativeTopCoverPartMeasurementProbeOnly && result.preflight_passed &&
                        !string.IsNullOrWhiteSpace(result.attempt_directory) &&
                        !string.IsNullOrWhiteSpace(result.attempts_root) &&
                        Directory.Exists(result.attempt_directory) &&
                        IsUnder(result.attempt_directory, result.attempts_root) &&
                        result.target_created_by_this_run)
                        WriteNativeTopCoverPartMeasurementProbeCleanupEvidence(result);
                    if (NativeTopCoverPartDetachProbeOnly && result.preflight_passed &&
                        !string.IsNullOrWhiteSpace(result.attempt_directory) &&
                        !string.IsNullOrWhiteSpace(result.attempts_root) &&
                        Directory.Exists(result.attempt_directory) &&
                        IsUnder(result.attempt_directory, result.attempts_root) &&
                        result.target_created_by_this_run)
                        WriteNativeTopCoverPartDetachProbeCleanupEvidence(result);
                    if (NativeTopCoverRebuildCaptureProbeOnly && result.preflight_passed &&
                        !string.IsNullOrWhiteSpace(result.attempt_directory) &&
                        !string.IsNullOrWhiteSpace(result.attempts_root) &&
                        Directory.Exists(result.attempt_directory) &&
                        IsUnder(result.attempt_directory, result.attempts_root) &&
                        result.target_created_by_this_run)
                        WriteNativeTopCoverRebuildCaptureProbeCleanupEvidence(result);
                    if (NativeTopCoverMasterReferenceCaptureProbeOnly &&
                        result.preflight_passed &&
                        !string.IsNullOrWhiteSpace(result.attempt_directory) &&
                        !string.IsNullOrWhiteSpace(result.attempts_root) &&
                        Directory.Exists(result.attempt_directory) &&
                        IsUnder(result.attempt_directory, result.attempts_root) &&
                        result.target_created_by_this_run)
                        WriteNativeTopCoverMasterReferenceCaptureProbeCleanupEvidence(result);
                    if (NativeTopCoverFrontBrepCaptureProbeOnly && result.preflight_passed &&
                        !string.IsNullOrWhiteSpace(result.attempt_directory) &&
                        !string.IsNullOrWhiteSpace(result.attempts_root) &&
                        Directory.Exists(result.attempt_directory) &&
                        IsUnder(result.attempt_directory, result.attempts_root) &&
                        result.target_created_by_this_run)
                        WriteNativeTopCoverFrontBrepCaptureProbeCleanupEvidence(result);
                }
                ReleaseLocks(result);
            }

            if (exitCode == 0) Console.WriteLine(result.output_json);
            else Console.Error.WriteLine(result.status + ": " + result.error);
            return exitCode;
        }

        private static Result NewResult(string source, string target, string output)
        {
            return new Result
            {
                phase = ExecutionPhase,
                recipe_id = ExpectedRecipeId,
                recipe_digest = ExpectedRecipeDigest,
                generated_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                source_directory = source,
                target_directory = target,
                output_json = output,
                source_root_path = Path.Combine(source, RootFileName),
                target_root_path = Path.Combine(target, RootFileName),
                root_file_name = RootFileName,
                source_inventory_digest = ExpectedSourceInventoryDigest,
                authorization_seed_inventory_digest = ExpectedSourceInventoryDigest,
                root_sha_before = ExpectedRootSha256,
                tool_id = ToolId,
                tool_source_normalized_sha256 = NormalizedSourceSha256(),
                tool_executable_sha256 = Sha256File(
                    System.Reflection.Assembly.GetExecutingAssembly().Location),
                quality_boundary = new QualityBoundary
                {
                    purpose = Purpose,
                    engineeringAssistanceReady = false,
                    downstreamStagesRequired = true,
                    structuralEngineerReviewRequired = true
                }
            };
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: SeedPack888Native.exe --source <exact-v37-final> --working-pack <attempt\\native_cad\\working_pack> --out <attempt\\evidence\\seed_pack_888_native_v1.json> --confirm-task <task-id>");
            Console.Error.WriteLine("Execution also requires immutable plan and worker authorization environment bindings.");
        }

        private static void PrintIdentity()
        {
            Console.WriteLine("toolId=" + ToolId);
            Console.WriteLine("phase=" + ExecutionPhase);
            Console.WriteLine("recipeDigest=" + ExpectedRecipeDigest);
            Console.WriteLine("sourceNormalizedSha256=" + NormalizedSourceSha256());
            Console.WriteLine("executableSha256=" + Sha256File(
                System.Reflection.Assembly.GetExecutingAssembly().Location));
        }

        private static void ValidatePreflight(Result result, string confirmTask)
        {
            result.repository_root = FindRepositoryRoot(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            Require(!string.IsNullOrWhiteSpace(result.repository_root),
                "REPOSITORY_ROOT_NOT_FOUND", 3, "repository root could not be resolved");
            string expectedSource = Path.Combine(result.repository_root, "workers", "generated_models",
                "review_generation_requests", "v43-int-v37-760w-six-door-l642-r246-r1",
                "native_cad", "final_native_hierarchical_pack_and_go");
            Require(SamePath(result.source_directory, expectedSource) &&
                Directory.Exists(result.source_directory), "SOURCE_PATH_NOT_EXACT_V37_FINAL", 3,
                "source must be the exact reviewed V37 final native directory");
            AssertPathChainNoReparse(result.source_directory, result.repository_root,
                "SOURCE_PATH_REPARSE_CHAIN");
            Require(IsUnder(CanonicalExistingPath(result.source_directory),
                    CanonicalExistingPath(result.repository_root)),
                "SOURCE_REALPATH_ESCAPE", 3,
                "trusted source real path escaped the repository root");

            string attemptsRoot = FullPathOrEmpty(System.Environment.GetEnvironmentVariable(
                AttemptsRootEnvironment));
            Require(!string.IsNullOrWhiteSpace(attemptsRoot) && Directory.Exists(attemptsRoot),
                "ATTEMPTS_ROOT_INVALID", 3, AttemptsRootEnvironment + " is missing or invalid");
            result.attempts_root = attemptsRoot.TrimEnd('\\', '/');
            AssertPathChainNoReparse(result.attempts_root, result.attempts_root,
                "ATTEMPTS_ROOT_REPARSE_PATH");

            string relative = RelativeUnder(result.target_directory, result.attempts_root,
                "TARGET_OUTSIDE_ATTEMPTS_ROOT");
            string[] segments = relative.Split(new[] { '\\', '/' },
                StringSplitOptions.RemoveEmptyEntries);
            Require(segments.Length == 4 && IsSafeIdentifier(segments[0]) &&
                Regex.IsMatch(segments[1], "^attempt-[0-9]{4}$", RegexOptions.CultureInvariant) &&
                segments[1] != "attempt-0000" &&
                string.Equals(segments[2], "native_cad", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[3], "working_pack", StringComparison.OrdinalIgnoreCase),
                "TARGET_ATTEMPT_SHAPE_INVALID", 3,
                "working pack must be <attempts>/<task>/attempt-NNNN/native_cad/working_pack");
            result.task_id = segments[0];
            result.attempt = int.Parse(segments[1].Substring(8), CultureInfo.InvariantCulture);
            result.attempt_directory = Path.Combine(result.attempts_root, segments[0], segments[1]);
            Require(string.Equals(confirmTask, result.task_id, StringComparison.Ordinal),
                "TASK_CONFIRMATION_MISMATCH", 3, "--confirm-task does not match target task");
            Require(SamePath(result.target_directory, Path.Combine(result.attempt_directory,
                    "native_cad", "working_pack")), "TARGET_CANONICAL_PATH_INVALID", 3,
                "working pack path is not canonical");
            Require(!Directory.Exists(result.target_directory) && !File.Exists(result.target_directory),
                "TARGET_ALREADY_EXISTS", 3, "seed target is immutable and must not already exist");
            result.evidence_path = Path.Combine(result.attempt_directory,
                EvidenceRelativePath.Replace('/', '\\'));
            result.receipt_path = Path.Combine(result.attempt_directory,
                ReceiptRelativePath.Replace('/', '\\'));
            Require(SamePath(result.output_json, result.evidence_path), "EVIDENCE_PATH_INVALID", 3,
                "--out must be the exact seed evidence path");
            Require(!File.Exists(result.evidence_path) && !File.Exists(result.receipt_path),
                "SEED_COMMIT_ARTIFACT_ALREADY_EXISTS", 3,
                "seed evidence and receipt are immutable outputs");
            AssertPathChainNoReparse(result.attempt_directory, result.attempts_root,
                "ATTEMPT_REPARSE_PATH");
            AssertPathChainNoReparse(Path.GetDirectoryName(result.target_directory),
                result.attempt_directory, "NATIVE_CAD_REPARSE_PATH");
            AssertPathChainNoReparse(Path.GetDirectoryName(result.evidence_path),
                result.attempt_directory, "EVIDENCE_REPARSE_PATH");
            AssertPathChainNoReparse(Path.GetDirectoryName(result.receipt_path),
                result.attempt_directory, "RECEIPT_REPARSE_PATH");

            Require(File.Exists(result.source_root_path) &&
                string.Equals(Sha256File(result.source_root_path), ExpectedRootSha256,
                    StringComparison.OrdinalIgnoreCase), "SOURCE_ROOT_IDENTITY_INVALID", 3,
                "trusted V37 root is missing or hash-mismatched");
            List<string> sourceFiles = CaptureFlatCadTree(result.source_directory,
                "SOURCE_FILE_SYSTEM_INVALID");
            Require(sourceFiles.Count == ExpectedCadCount &&
                sourceFiles.Count(IsAssembly) == ExpectedAssemblyCount &&
                sourceFiles.Count(IsPart) == ExpectedPartCount,
                "SOURCE_INVENTORY_COUNT_INVALID", 3,
                "trusted source must remain flat 75 = 22 SLDASM + 53 SLDPRT");
            result.source_inventory = sourceFiles.Select(Snapshot).ToList();
            string digest = InventoryDigest(result.source_inventory);
            Require(string.Equals(digest, ExpectedSourceInventoryDigest,
                    StringComparison.OrdinalIgnoreCase), "SOURCE_INVENTORY_DIGEST_INVALID", 3,
                "trusted source name|SHA inventory digest drifted");
            result.source_inventory_digest = digest;
            result.source_hashes = result.source_inventory.ToDictionary(row => row.name,
                row => row.sha256, StringComparer.OrdinalIgnoreCase);

            result.plan_path = Path.Combine(result.attempt_directory, "native_build_plan.json");
            Require(File.Exists(result.plan_path) && !HasReparsePoint(result.plan_path) &&
                FileLinkCount(result.plan_path) == 1, "PLAN_FILE_INVALID", 3,
                "native_build_plan.json must be a single-link direct attempt artifact");
            planReadLock = new FileStream(result.plan_path, FileMode.Open, FileAccess.Read,
                FileShare.Read);
            result.plan_sha256 = Sha256File(result.plan_path);
            string configuredPlanSha = (System.Environment.GetEnvironmentVariable(
                PlanShaEnvironment) ?? "").Trim();
            Require(IsSha256(configuredPlanSha) && string.Equals(configuredPlanSha,
                    result.plan_sha256, StringComparison.OrdinalIgnoreCase),
                "PLAN_SHA256_ENVIRONMENT_MISMATCH", 3,
                "worker-provided plan SHA does not match native_build_plan.json");
            ValidatePlan(result);

            string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Require(string.Equals(result.tool_source_normalized_sha256, ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizedSourceSha256(), ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase), "TOOL_SOURCE_IDENTITY_INVALID", 3,
                "compiled seed tool no longer matches its normalized source identity");
            Require(File.Exists(ExpectedSolidWorksExePath) &&
                string.Equals(Sha256File(ExpectedSolidWorksExePath), ExpectedSolidWorksExeSha256,
                    StringComparison.OrdinalIgnoreCase) && File.Exists(ExpectedMonitorExePath) &&
                string.Equals(Sha256File(ExpectedMonitorExePath), ExpectedMonitorExeSha256,
                    StringComparison.OrdinalIgnoreCase), "SOLIDWORKS_2020_BINARY_IDENTITY_INVALID", 3,
                "pinned SolidWorks 2020 executable identity is unavailable");
            Require(string.Equals(result.tool_executable_sha256, Sha256File(executable),
                    StringComparison.OrdinalIgnoreCase), "TOOL_EXECUTABLE_IDENTITY_INVALID", 3,
                "running executable changed during preflight");
            LoadAndValidateAuthorization(result);

            result.processes.baseline_sldworks = ProcessIds("SLDWORKS");
            result.processes.baseline_sldprocmon = ProcessIds("sldProcMon");
            Require(result.processes.baseline_sldworks.Count == 0 &&
                result.processes.baseline_sldprocmon.Count == 0,
                "CAD_PROCESS_BASELINE_NOT_ZERO", 5,
                "SLDWORKS and sldProcMon must be closed before seed stabilization");
            result.preflight_passed = true;
        }

        private static void ValidatePlan(Result result)
        {
            Dictionary<string, object> plan = ReadJsonObject(result.plan_path);
            Dictionary<string, object> task = ChildObject(plan, "task");
            Dictionary<string, object> request = ChildObject(plan, "request");
            Dictionary<string, object> recipe = ChildObject(plan, "recipe");
            Require(Text(plan, "schema") == PlanSchema && Text(plan, "purpose") == Purpose,
                "PLAN_SCHEMA_OR_PURPOSE_INVALID", 3,
                "plan must remain the native structure-engineering-assistance plan");
            double revisionAtPlanning = Number(task, "revisionAtPlanning");
            Require(Text(task, "id") == result.task_id && revisionAtPlanning > 0 &&
                revisionAtPlanning <= int.MaxValue && Math.Floor(revisionAtPlanning) ==
                    revisionAtPlanning &&
                IsSha256(Text(task, "digest")) &&
                !string.IsNullOrWhiteSpace(Text(request, "fingerprint")) &&
                IsSha256(Text(request, "digest")), "PLAN_TASK_BINDING_INVALID", 3,
                "plan task/request binding is incomplete or mismatched");
            Require(Text(recipe, "id") == ExpectedRecipeId && Number(recipe, "version") == 1 &&
                string.Equals(Text(recipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase), "PLAN_RECIPE_BINDING_INVALID", 3,
                "plan does not bind the trusted 888x14 native recipe");
            Require(PlanBoundaryValid(plan), "PLAN_EXECUTION_BOUNDARY_INVALID", 3,
                "plan must remain planningOnly with executorImplemented=false and no CAD/model claim");
            Require(StageContractPresent(plan), "PLAN_SEED_STAGE_INVALID", 3,
                "plan must bind clone_native_seed to the native seed tool");
            string[] checks = ArrayValue(plan, "requiredChecks").Select(ValueText).ToArray();
            foreach (string required in new[] { "one_door_one_lock", "rebuild_save_reopen",
                "component_reference_closure", "relocated_reopen" })
                Require(checks.Contains(required, StringComparer.Ordinal),
                    "PLAN_REQUIRED_CHECK_MISSING", 3, "plan is missing " + required);
            Require(ValidateStageReceiptContracts(ChildObject(plan, "stageReceiptContracts")),
                "PLAN_STAGE_RECEIPT_CONTRACT_INVALID", 3,
                "stageReceiptContracts differs from the exact shared nine-stage contract");
            ValidateTrustedToolchainManifests(plan, result);

            result.worker_id = Text(plan, "workerId");
            Require(IsSafeIdentifier(result.worker_id), "PLAN_WORKER_ID_INVALID", 3,
                "plan workerId is missing or unsafe");
            result.task_revision = Convert.ToInt32(revisionAtPlanning,
                CultureInfo.InvariantCulture);
            result.task_digest = Text(task, "digest").ToUpperInvariant();
            result.request_fingerprint = Text(request, "fingerprint");
            result.request_digest = Text(request, "digest").ToUpperInvariant();
            result.plan_document = plan;
            result.stage_receipt_contracts_validated = true;
        }

        private static bool PlanBoundaryValid(Dictionary<string, object> plan)
        {
            Dictionary<string, object> quality = ChildObject(plan, "qualityBoundary");
            Dictionary<string, object> execution = ChildObject(plan, "executionBoundary");
            return Bool(quality, "planningOnly") && !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses") &&
                !Bool(execution, "executorImplemented") && !Bool(execution, "cadStarted") &&
                !Bool(execution, "modelGenerated") && !Bool(execution, "modelReady") &&
                !Bool(execution, "legacyFallbackUsed");
        }

        private static bool StageContractPresent(Dictionary<string, object> plan)
        {
            object[] stages = ArrayValue(plan, "stageContracts");
            return stages.Count(value =>
            {
                Dictionary<string, object> row = value as Dictionary<string, object>;
                return row != null && Text(row, "id") == ExecutionPhase &&
                    Text(row, "toolId") == ToolId;
            }) == 1;
        }

        private static bool ValidateStageReceiptContracts(Dictionary<string, object> contracts)
        {
            if (!ExactKeys(contracts, StageReceiptOrder)) return false;
            for (int index = 0; index < StageReceiptOrder.Length; index++)
            {
                Dictionary<string, object> row = ChildObject(contracts, StageReceiptOrder[index]);
                if (!ExactKeys(row, new[] { "schema", "path", "producerToolId", "predecessor" }) ||
                    Text(row, "schema") != StageReceiptSchemas[index] ||
                    Text(row, "path") != StageReceiptPaths[index] ||
                    Text(row, "producerToolId") != StageReceiptTools[index] ||
                    Text(row, "predecessor") != StageReceiptPredecessors[index] ||
                    !IsCanonicalRelativeJsonPath(Text(row, "path"))) return false;
            }
            return true;
        }

        private static void ValidateTrustedToolchainManifests(Dictionary<string, object> plan,
            Result result)
        {
            Dictionary<string, object> map = ChildObject(plan, "trustedToolchainManifests");
            Require(ExactKeys(map, TrustedManifestKeys) && TrustedManifestKeys.All(key =>
                IsSha256(Text(map, key))), "PLAN_TOOLCHAIN_MANIFEST_MAP_INVALID", 3,
                "trustedToolchainManifests must contain exactly the six shared tools");
            Dictionary<string, string> paths = ToolchainManifestPaths(result.repository_root);
            foreach (string id in TrustedManifestKeys)
            {
                string path = paths[id];
                string expectedManifestSha = Text(map, id).ToUpperInvariant();
                Require(File.Exists(path) && PathChainSafe(path, result.repository_root) &&
                    !HasReparsePoint(path) && FileLinkCount(path) == 1 &&
                    IsUnder(CanonicalExistingPath(path),
                        CanonicalExistingPath(result.repository_root)) &&
                    string.Equals(Sha256File(path), expectedManifestSha,
                        StringComparison.OrdinalIgnoreCase),
                    "TOOLCHAIN_MANIFEST_LIVE_HASH_INVALID", 3,
                    "live manifest does not match plan: " + id);
                Dictionary<string, object> manifest = ReadJsonObject(path);
                Require(id == LockToolId ?
                        ValidateSpecializedLockToolchainManifest(result.repository_root, manifest) :
                        ValidateGenericToolchainManifest(result.repository_root, id, manifest),
                    "TOOLCHAIN_MANIFEST_CONTENT_INVALID", 3,
                    "manifest does not bind live source/executable/verifier: " + id);
                if (id == ToolId)
                {
                    Dictionary<string, object> tool = ChildObject(manifest, "tool");
                    Require(Text(tool, "sourcePath") ==
                            "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.cs" &&
                        Text(tool, "executablePath") ==
                            "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.exe" &&
                        Text(tool, "verifierPath") ==
                            "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/Verify-SeedPack888Native.ps1" &&
                        string.Equals(Text(tool, "sourceNormalizedSha256"),
                            result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(Text(tool, "executableSha256"),
                            result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase),
                        "SEED_TOOLCHAIN_MANIFEST_SELF_IDENTITY_INVALID", 3,
                        "seed manifest does not bind this exact executable and normalized source");
                }
                result.trusted_toolchain_manifests.Add(new TrustedManifestEvidence
                {
                    id = id,
                    path = path,
                    sha256 = expectedManifestSha
                });
            }
            result.trusted_toolchain_manifests_validated = true;
        }

        private static bool TrustedToolchainStateStillValid(Result result)
        {
            try
            {
                if (!result.trusted_toolchain_manifests_validated ||
                    result.trusted_toolchain_manifests.Count != TrustedManifestKeys.Length)
                    return false;
                Dictionary<string, object> map = ChildObject(result.plan_document,
                    "trustedToolchainManifests");
                if (!ExactKeys(map, TrustedManifestKeys)) return false;
                Dictionary<string, string> paths = ToolchainManifestPaths(result.repository_root);
                foreach (string id in TrustedManifestKeys)
                {
                    string path = paths[id];
                    string expectedManifestSha = Text(map, id).ToUpperInvariant();
                    TrustedManifestEvidence recorded = result.trusted_toolchain_manifests
                        .SingleOrDefault(row => row.id == id);
                    if (recorded == null || !SamePath(recorded.path, path) ||
                        !string.Equals(recorded.sha256, expectedManifestSha,
                            StringComparison.OrdinalIgnoreCase) || !File.Exists(path) ||
                        !PathChainSafe(path, result.repository_root) || HasReparsePoint(path) ||
                        FileLinkCount(path) != 1) return false;
                    byte[] manifestBytes = File.ReadAllBytes(path);
                    if (!string.Equals(Sha256Bytes(manifestBytes), expectedManifestSha,
                        StringComparison.OrdinalIgnoreCase)) return false;
                    Dictionary<string, object> manifest = ReadJsonObjectBytes(manifestBytes);
                    if (!(id == LockToolId ?
                            ValidateSpecializedLockToolchainManifest(result.repository_root,
                                manifest) :
                            ValidateGenericToolchainManifest(result.repository_root, id,
                                manifest)) ||
                        !string.Equals(Sha256File(path), expectedManifestSha,
                            StringComparison.OrdinalIgnoreCase)) return false;
                }
                string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
                return string.Equals(NormalizedSourceSha256(),
                           result.tool_source_normalized_sha256,
                           StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Sha256File(executable), result.tool_executable_sha256,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static Dictionary<string, string> ToolchainManifestPaths(string repositoryRoot)
        {
            string tools = Path.Combine(repositoryRoot, "workers", "native_model_requests",
                "development", "v1", "tools");
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ToolId, Path.Combine(tools, "seed_pack_888_native_v1", "toolchain_manifest.json") },
                { "native_width_888_v1", Path.Combine(tools, "width_888_native_v1", "toolchain_manifest.json") },
                { "native_door_module_888x14_v1", Path.Combine(tools, "door_module_888x14_native_v1", "toolchain_manifest.json") },
                { "native_lock_topology_888x14_v1", Path.Combine(tools, "lock_toolchain_manifest.json") },
                { "native_root_assembly_888x14_v1", Path.Combine(tools, "root_assembly_888x14_native_v1", "toolchain_manifest.json") },
                { "native_final_pack_888x14_v1", Path.Combine(tools, "final_pack_888x14_native_v1", "toolchain_manifest.json") }
            };
        }

        private static bool ValidateGenericToolchainManifest(string repositoryRoot, string id,
            Dictionary<string, object> manifest)
        {
            Dictionary<string, object> tool = ChildObject(manifest, "tool");
            return ExactKeys(manifest, new[] { "schema", "generatedBy", "tool" }) &&
                !string.IsNullOrWhiteSpace(Text(manifest, "generatedBy")) &&
                ExactKeys(tool, new[] { "id", "sourcePath", "sourceNormalizedSha256",
                    "executablePath", "executableSha256", "verifierPath", "verifierSha256" }) &&
                Text(manifest, "schema") == ToolchainManifestSchema && Text(tool, "id") == id &&
                ValidateManifestArtifact(repositoryRoot, tool, "executablePath",
                    "executableSha256", false) &&
                ValidateManifestArtifact(repositoryRoot, tool, "verifierPath",
                    "verifierSha256", false) &&
                ValidateManifestArtifact(repositoryRoot, tool, "sourcePath",
                    "sourceNormalizedSha256", true);
        }

        private static bool ValidateSpecializedLockToolchainManifest(string repositoryRoot,
            Dictionary<string, object> manifest)
        {
            if (!ExactKeys(manifest, new[] { "schema", "generatedBy", "tools", "validator" }) ||
                Text(manifest, "schema") != LockToolchainManifestSchema ||
                Text(manifest, "generatedBy") != LockToolchainManifestGeneratedBy)
                return false;
            Dictionary<string, object> tools = ChildObject(manifest, "tools");
            if (!ExactKeys(tools, LockToolchainToolIds)) return false;
            for (int index = 0; index < LockToolchainToolIds.Length; index++)
            {
                Dictionary<string, object> tool = ChildObject(tools,
                    LockToolchainToolIds[index]);
                if (!ExactKeys(tool, new[] { "sourcePath", "sourceNormalizedSha256",
                        "executablePath", "executableSha256" }) ||
                    !ValidateFixedLockManifestArtifact(repositoryRoot, tool, "sourcePath",
                        "sourceNormalizedSha256", LockToolchainSourceRelativePaths[index], true) ||
                    !ValidateFixedLockManifestArtifact(repositoryRoot, tool, "executablePath",
                        "executableSha256", LockToolchainExecutableRelativePaths[index], false))
                    return false;
            }
            Dictionary<string, object> validator = ChildObject(manifest, "validator");
            return ExactKeys(validator, new[] { "path", "sha256" }) &&
                ValidateFixedLockManifestArtifact(repositoryRoot, validator, "path", "sha256",
                    LockToolchainValidatorRelativePath, false);
        }

        private static bool ValidateFixedLockManifestArtifact(string repositoryRoot,
            Dictionary<string, object> artifact, string pathKey, string shaKey,
            string expectedRelativePath, bool normalizedSource)
        {
            try
            {
                string pinnedPath = Text(artifact, pathKey);
                string expectedSha = Text(artifact, shaKey);
                string expectedPath = Path.GetFullPath(Path.Combine(repositoryRoot,
                    expectedRelativePath.Replace('/', '\\')));
                if (!Path.IsPathRooted(pinnedPath) || !SamePath(pinnedPath, expectedPath) ||
                    !IsSha256(expectedSha) || !IsUnder(expectedPath, repositoryRoot) ||
                    !File.Exists(expectedPath) || !PathChainSafe(expectedPath, repositoryRoot) ||
                    HasReparsePoint(expectedPath) || FileLinkCount(expectedPath) != 1 ||
                    !IsUnder(CanonicalExistingPath(expectedPath),
                        CanonicalExistingPath(repositoryRoot))) return false;
                string actual = normalizedSource ? NormalizedLockSourceHashAt(expectedPath) :
                    Sha256File(expectedPath);
                return string.Equals(actual, expectedSha, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool ValidateManifestArtifact(string repositoryRoot,
            Dictionary<string, object> tool, string pathKey, string shaKey, bool normalizedSource)
        {
            string relative = Text(tool, pathKey);
            string expected = Text(tool, shaKey);
            if (!IsSafeRepositoryRelativePath(relative) || !IsSha256(expected)) return false;
            string full = Path.GetFullPath(Path.Combine(repositoryRoot, relative.Replace('/', '\\')));
            if (!IsUnder(full, repositoryRoot) || !File.Exists(full) ||
                !PathChainSafe(full, repositoryRoot) || FileLinkCount(full) != 1 ||
                !IsUnder(CanonicalExistingPath(full), CanonicalExistingPath(repositoryRoot)))
                return false;
            string actual = normalizedSource ? NormalizedStandardManifestSourceHashAt(full) :
                Sha256File(full);
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadAndValidateAuthorization(Result result)
        {
            string configuredPath = FullPathOrEmpty(System.Environment.GetEnvironmentVariable(
                AuthorizationPathEnvironment));
            string expectedPath = Path.Combine(result.attempt_directory,
                "execution_authorizations", ToolId + ".json");
            Require(SamePath(configuredPath, expectedPath) && File.Exists(configuredPath) &&
                !HasReparsePoint(Path.GetDirectoryName(configuredPath)) &&
                !HasReparsePoint(configuredPath) && FileLinkCount(configuredPath) == 1,
                "EXECUTION_AUTHORIZATION_PATH_INVALID", 3,
                "authorization must be the exact worker artifact for this seed tool");
            string configuredSha = (System.Environment.GetEnvironmentVariable(
                AuthorizationShaEnvironment) ?? "").Trim();
            string actualSha = Sha256File(configuredPath);
            Require(IsSha256(configuredSha) && string.Equals(configuredSha, actualSha,
                    StringComparison.OrdinalIgnoreCase),
                "EXECUTION_AUTHORIZATION_SHA_INVALID", 3,
                "authorization does not match the worker-recorded SHA-256");
            byte[] bytes = File.ReadAllBytes(configuredPath);
            Dictionary<string, object> authorization = ReadJsonObjectBytes(bytes);
            AuthorizationBinding binding;
            Require(AuthorizationContractValid(authorization, result, DateTime.UtcNow, null,
                    out binding), "EXECUTION_AUTHORIZATION_BINDING_INVALID", 3,
                "authorization does not exactly bind task, request, plan, recipe, tool, seed and phase");
            result.authorization_path = configuredPath;
            result.authorization_sha256 = actualSha;
            result.authorization_json_base64 = Convert.ToBase64String(bytes);
            result.authorization_id = binding.authorization_id;
            result.authorization_issued_at = binding.issued_at;
            result.authorization_expires_at = binding.expires_at;
            result.lease_id = binding.lease_id;
            result.lease_expires_at = binding.lease_expires_at;
            result.authorization_document = authorization;
            result.authorization_checkpoints.Add(new AuthorizationCheckpoint
            {
                name = "initial_binding",
                checked_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                valid = true
            });
        }

        private static bool AuthorizationContractValid(Dictionary<string, object> authorization,
            Result result, DateTime now, string completionAt, out AuthorizationBinding binding)
        {
            binding = new AuthorizationBinding();
            try
            {
                if (!ExactAuthorizationKeys(authorization)) return false;
                Dictionary<string, object> task = ChildObject(authorization, "task");
                Dictionary<string, object> request = ChildObject(authorization, "request");
                Dictionary<string, object> plan = ChildObject(authorization, "plan");
                Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
                Dictionary<string, object> tool = ChildObject(authorization, "tool");
                Dictionary<string, object> seed = ChildObject(authorization, "seed");
                Dictionary<string, object> execution = ChildObject(authorization, "execution");
                Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");
                DateTime issuedAt, expiresAt, leaseExpiresAt;
                if (!ParseUtc(Text(authorization, "issuedAt"), out issuedAt) ||
                    !ParseUtc(Text(authorization, "expiresAt"), out expiresAt) ||
                    !ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpiresAt)) return false;
                DateTime normalizedNow = now.ToUniversalTime();
                if (issuedAt > normalizedNow || expiresAt <= normalizedNow ||
                    expiresAt <= issuedAt || expiresAt - issuedAt > TimeSpan.FromMinutes(30) ||
                    expiresAt > leaseExpiresAt || leaseExpiresAt <= normalizedNow) return false;
                if (!string.IsNullOrWhiteSpace(completionAt))
                {
                    DateTime completion;
                    if (!ParseUtc(completionAt, out completion) || completion > expiresAt ||
                        completion > leaseExpiresAt || completion < issuedAt) return false;
                }
                if (Text(authorization, "schema") != AuthorizationSchema ||
                    Text(authorization, "purpose") != Purpose ||
                    Text(authorization, "workerId") != result.worker_id ||
                    Text(task, "id") != result.task_id ||
                    Number(task, "revision") != result.task_revision ||
                    !string.Equals(Text(task, "digest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(Text(task, "leaseId")) ||
                    Text(request, "fingerprint") != result.request_fingerprint ||
                    !string.Equals(Text(request, "digest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Text(plan, "sha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    Text(recipe, "id") != ExpectedRecipeId || Number(recipe, "version") != 1 ||
                    !string.Equals(Text(recipe, "digest"), ExpectedRecipeDigest,
                        StringComparison.OrdinalIgnoreCase) ||
                    Text(tool, "id") != ToolId ||
                    !string.Equals(Text(tool, "sourceNormalizedSha256"),
                        result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Text(tool, "executableSha256"),
                        result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Text(seed, "inventoryDigest"),
                        result.source_inventory_digest, StringComparison.OrdinalIgnoreCase) ||
                    !Bool(execution, "authorized") || Number(execution, "attempt") != result.attempt ||
                    !StringArrayEquals(ArrayValue(execution, "phases"),
                        new[] { ExecutionPhase }) ||
                    Bool(quality, "engineeringAssistanceReady") ||
                    !Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses")) return false;

                string expectedId = ExpectedAuthorizationId(authorization);
                if (!Regex.IsMatch(Text(authorization, "authorizationId"),
                        "^native-auth-[a-f0-9]{32}$", RegexOptions.CultureInvariant) ||
                    Text(authorization, "authorizationId") != expectedId) return false;
                binding = new AuthorizationBinding
                {
                    authorization_id = expectedId,
                    issued_at = Text(authorization, "issuedAt"),
                    expires_at = Text(authorization, "expiresAt"),
                    lease_id = Text(task, "leaseId"),
                    lease_expires_at = Text(task, "leaseExpiresAt")
                };
                return true;
            }
            catch
            {
                binding = new AuthorizationBinding();
                return false;
            }
        }

        private static bool ExactAuthorizationKeys(Dictionary<string, object> authorization)
        {
            return ExactKeys(authorization, AuthorizationKeys) &&
                ExactKeys(ChildObject(authorization, "task"), new[] { "id", "revision", "digest",
                    "leaseId", "leaseExpiresAt" }) &&
                ExactKeys(ChildObject(authorization, "request"), new[] { "fingerprint", "digest" }) &&
                ExactKeys(ChildObject(authorization, "plan"), new[] { "sha256" }) &&
                ExactKeys(ChildObject(authorization, "recipe"), new[] { "id", "version", "digest" }) &&
                ExactKeys(ChildObject(authorization, "tool"), new[] { "id",
                    "sourceNormalizedSha256", "executableSha256" }) &&
                ExactKeys(ChildObject(authorization, "seed"), new[] { "inventoryDigest" }) &&
                ExactKeys(ChildObject(authorization, "execution"), new[] { "authorized", "attempt",
                    "phases" }) &&
                ExactKeys(ChildObject(authorization, "qualityBoundary"), new[] {
                    "engineeringAssistanceReady", "readyOnlyAfterEveryRequiredCheckPasses" });
        }

        private static string ExpectedAuthorizationId(Dictionary<string, object> authorization)
        {
            Dictionary<string, object> binding = new Dictionary<string, object>(StringComparer.Ordinal)
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
            return "native-auth-" + Sha256Text(StableJson(binding)).Substring(0, 32)
                .ToLowerInvariant();
        }

        private static void AssertAuthorizationStillValid(Result result, string checkpoint)
        {
            bool valid = File.Exists(result.authorization_path) &&
                !HasReparsePoint(result.authorization_path) &&
                FileLinkCount(result.authorization_path) == 1 &&
                string.Equals(Sha256File(result.authorization_path), result.authorization_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(result.plan_path) && FileLinkCount(result.plan_path) == 1 &&
                string.Equals(Sha256File(result.plan_path), result.plan_sha256,
                    StringComparison.OrdinalIgnoreCase) && TrustedToolchainStateStillValid(result);
            AuthorizationBinding binding = new AuthorizationBinding();
            if (valid)
            {
                Dictionary<string, object> current = ReadJsonObject(result.authorization_path);
                valid = AuthorizationContractValid(current, result, DateTime.UtcNow, null,
                    out binding) && binding.authorization_id == result.authorization_id &&
                    string.Equals(Sha256File(result.authorization_path), result.authorization_sha256,
                        StringComparison.OrdinalIgnoreCase);
            }
            result.authorization_checkpoints.Add(new AuthorizationCheckpoint
            {
                name = checkpoint,
                checked_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                valid = valid
            });
            Require(valid, "EXECUTION_AUTHORIZATION_EXPIRED_OR_CHANGED", 3,
                "worker authorization or immutable plan changed at checkpoint " + checkpoint);
        }

        private static bool CompletionWithinAuthorization(Result result)
        {
            AuthorizationBinding binding;
            return AuthorizationContractValid(result.authorization_document, result, DateTime.UtcNow,
                result.completed_at_utc, out binding) &&
                binding.authorization_id == result.authorization_id;
        }

        private static void AcquireAttemptLock(Result result)
        {
            string path = Path.Combine(result.attempt_directory, ".native-seed-888x14.lock");
            AssertPathChainNoReparse(result.attempt_directory, result.attempts_root,
                "ATTEMPT_LOCK_PATH_INVALID");
            attemptLock = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None);
            byte[] payload = new UTF8Encoding(false).GetBytes(
                Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + "\n");
            attemptLock.Write(payload, 0, payload.Length);
            attemptLock.Flush(true);
            result.attempt_lock_path = path;
            result.attempt_lock_acquired = true;
        }

        private static void DirectCopySeed(Result result)
        {
            CopyExact75FromProtectedSource(result);
            result.direct_copy_completed = true;
        }

        private static void CopyExact75FromProtectedSource(Result result)
        {
            string destinationRoot = result.target_directory;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationRoot));
            AssertPathChainNoReparse(Path.GetDirectoryName(destinationRoot),
                result.attempt_directory, "NATIVE_CAD_PARENT_REPARSE_PATH");
            Directory.CreateDirectory(destinationRoot);
            result.target_created_by_this_run = true;
            AssertPathChainNoReparse(destinationRoot, result.attempt_directory,
                "TARGET_REPARSE_PATH");
            Require(IsUnder(CanonicalExistingPath(destinationRoot),
                    CanonicalExistingPath(result.attempt_directory)),
                "TARGET_REALPATH_ESCAPE", 6,
                "copy root real path escaped the attempt");

            foreach (FileSnapshot row in result.source_inventory.OrderBy(value => value.name,
                StringComparer.OrdinalIgnoreCase))
            {
                string destination = Path.Combine(destinationRoot, row.name);
                Require(!File.Exists(destination), "DIRECT_COPY_TARGET_COLLISION", 6, row.name);
                File.Copy(row.path, destination, false);
                Require(FileLinkCount(destination) == 1 && !HasReparsePoint(destination) &&
                    string.Equals(Sha256File(destination), row.sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsUnder(CanonicalExistingPath(destination),
                        CanonicalExistingPath(destinationRoot)),
                    "DIRECT_COPY_FILE_IDENTITY_INVALID", 6,
                    "copied CAD file is linked, escaped or hash-mismatched: " + row.name);
                result.direct_copy_files.Add(row.name);
            }
            List<string> copied = CaptureFlatCadTree(destinationRoot,
                "DIRECT_COPY_TARGET_FILE_SYSTEM_INVALID");
            string digest = InventoryDigest(copied.Select(Snapshot));
            Require(copied.Count == ExpectedCadCount &&
                string.Equals(digest, result.source_inventory_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                result.direct_copy_files.Count == ExpectedCadCount,
                "DIRECT_COPY_INVENTORY_INVALID", 6,
                "direct copy must preserve the exact flat 75-file name/hash inventory");
            result.direct_copy_inventory_digest = digest;
        }

        // A bounded read-only evidence probe.  It deliberately does not create a repair
        // directory, candidate assembly, replacement, save, or shared stage artifact.
        private static void RunNativeTopCoverPartMeasurementProbeOnly(Result result)
        {
            NativeTopCoverPartMeasurementEvidence probe = new NativeTopCoverPartMeasurementEvidence
            {
                probeOnly = true,
                consumable = false,
                mode = "measure_topcover_parts",
                protectedSourceInventoryDigestBefore = result.source_inventory_digest,
                sourceDirectory = result.source_directory,
                workingPack = result.target_directory,
                exactSix = TopCoverCandidateUpstreamFirstFileNames.Skip(1).ToList()
            };
            ISldWorks application = null;
            SessionEvidence session = new SessionEvidence
                { purpose = "topcover_native_part_measurement_readonly" };
            result.sessions.Add(session);
            try
            {
                probe.exactSixSourceBindings = probe.exactSix.Count == 6 &&
                    probe.exactSix.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 6 &&
                    probe.exactSix.All(fileName =>
                    {
                        string path = Path.Combine(result.target_directory, fileName);
                        string expected;
                        return result.source_hashes.TryGetValue(fileName, out expected) &&
                            File.Exists(path) && FileLinkCount(path) == 1 &&
                            !HasReparsePoint(path) &&
                            SamePath(Path.GetDirectoryName(path), result.target_directory) &&
                            string.Equals(Sha256File(path), expected,
                                StringComparison.OrdinalIgnoreCase);
                    });
                Require(result.direct_copy_completed && probe.exactSixSourceBindings,
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_PRECONDITION_FAILED", 69,
                    "measurement requires six exact source-bound files in the direct-copy pack");
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_NOT_QUIESCENT", 69,
                    "CAD processes were not stably empty before the owned measurement session");
                application = StartOwnedSolidWorks(result, session);
                foreach (string fileName in probe.exactSix)
                {
                    string path = Path.Combine(result.target_directory, fileName);
                    probe.parts.Add(CaptureNativeTopCoverPartMeasurement(application, path,
                        result.target_directory));
                }
                probe.templateComponents = CaptureNativeTopCoverTemplateComponentStates(
                    application, Path.Combine(result.target_directory, TopCoverAssemblyFileName),
                    result.target_directory, probe);
                probe.templateComponentsComplete = NativeTopCoverTemplateComponentStatesComplete(
                    probe.templateComponents, result.target_directory);
            }
            catch (StageException exception)
            {
                probe.failureStatus = exception.Status;
                probe.failureExitCode = exception.ExitCode;
                probe.failure = exception.Message;
                throw;
            }
            catch (Exception exception)
            {
                probe.failureStatus = "TOPCOVER_NATIVE_PART_MEASUREMENT_PROBE_FAILED";
                probe.failureExitCode = 69;
                probe.failure = SafeException(exception);
                throw new StageException(probe.failureStatus, 69, probe.failure);
            }
            finally
            {
                if (application != null) Try(() => application.CloseAllDocuments(true));
                CloseOwnedSolidWorks(ref application, result, session);
                probe.sessionExitProven = session.exit_state == "exited" &&
                    session.monitors_exit_proven;
                probe.sourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.workingPackExact75Unchanged = WorkingPackExact75Unchanged(result);
                probe.allMeasurementsComplete = NativeTopCoverPartMeasurementComplete(probe);
                probe.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                probe.privateEvidenceWritten = WriteNativeTopCoverPartMeasurementPrivateEvidence(
                    result, probe);
            }
            Require(probe.allMeasurementsComplete,
                "TOPCOVER_NATIVE_PART_MEASUREMENT_INCOMPLETE", 69,
                "one or more read-only top-cover part measurements were incomplete");
            Require(probe.privateEvidenceWritten,
                "TOPCOVER_NATIVE_PART_MEASUREMENT_PRIVATE_EVIDENCE_WRITE_FAILED", 69,
                "completed measurement probe must persist private evidence with reread proof");
        }

        private static NativeTopCoverPartMeasurement CaptureNativeTopCoverPartMeasurement(
            ISldWorks application, string partPath, string cadDirectory)
        {
            NativeTopCoverPartMeasurement row = new NativeTopCoverPartMeasurement
            {
                name = Path.GetFileName(partPath), path = partPath,
                pathLocal = File.Exists(partPath) && SamePath(Path.GetDirectoryName(partPath),
                    cadDirectory),
                sourceSize = File.Exists(partPath) ? new FileInfo(partPath).Length : -1,
                sourceSha256 = File.Exists(partPath) ? Sha256File(partPath) : ""
            };
            string hashBefore = row.sourceSha256;
            ModelDoc2 model = null;
            try
            {
                int documentsBefore;
                row.cacheBeforeEmpty = DocumentCacheEmpty(application, out documentsBefore);
                if (!row.pathLocal || !row.cacheBeforeEmpty) return row;
                int errors = 0;
                int warnings = 0;
                model = application.OpenDoc6(partPath, (int)swDocumentTypes_e.swDocPART,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                row.opened = model != null;
                row.openErrors = errors;
                row.openWarnings = warnings;
                if (model == null) return row;
                row.readOnly = Safe(() => model.IsOpenedReadOnly(), false);
                try
                {
                    row.saveFlagBeforeRebuild = model.GetSaveFlag();
                    row.saveFlagBeforeCaptured = true;
                }
                catch { row.saveFlagBeforeCaptured = false; }
                Configuration configuration = Safe(() => model.GetActiveConfiguration() as Configuration,
                    null);
                row.configuration = configuration == null ? "" : Safe(() => configuration.Name, "");
                Release(configuration);
                row.rebuilt = Safe(() => model.ForceRebuild3(false), false);
                try
                {
                    row.saveFlagAfterRebuild = model.GetSaveFlag();
                    row.saveFlagAfterCaptured = true;
                }
                catch { row.saveFlagAfterCaptured = false; }
                CaptureNativeTopCoverPartGeometry(model, row);
                CaptureNativeTopCoverPartFeatures(model, row, cadDirectory);
                row.modelExternalReferenceCount = Safe(() =>
                    model.ListExternalFileReferencesCount2(), -1);
                ModelDocExtension extension = Safe(() => model.Extension, null);
                try
                {
                    row.extensionExternalReferenceCount = extension == null ? -1 :
                        Safe(() => extension.ListExternalFileReferencesCount(), -1);
                }
                finally { Release(extension); }
                row.geometrySignatureSha256 = NativeTopCoverPartGeometrySignature(row);
                row.physicalGeometrySignatureSha256 = PhysicalGeometrySignature(row);
            }
            catch (Exception exception)
            {
                row.fault = SafeException(exception);
            }
            finally
            {
                CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int documentsAfter;
                row.cacheAfterEmpty = application != null &&
                    DocumentCacheEmpty(application, out documentsAfter);
                row.fileUnchanged = File.Exists(partPath) &&
                    string.Equals(hashBefore, Sha256File(partPath),
                        StringComparison.OrdinalIgnoreCase);
            }
            return row;
        }

        private static void CaptureNativeTopCoverPartGeometry(ModelDoc2 model,
            NativeTopCoverPartMeasurement row)
        {
            PartDoc part = model as PartDoc;
            if (part == null) { row.geometryFault = "not-part"; return; }
            row.partBoxApiRaw = MeasurementDoubleArray(Safe(() =>
                part.GetPartBox(false), null));
            Array bodies = Safe(() => part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array,
                null);
            row.bodyCount = bodies == null ? 0 : bodies.Length;
            if (bodies == null) return;
            int index = 0;
            foreach (object item in bodies)
            {
                Body2 body = item as Body2;
                NativeTopCoverBodyMeasurement bodyRow = new NativeTopCoverBodyMeasurement
                    { index = ++index };
                try
                {
                    bodyRow.boxMm = MeasurementMmArray(body == null ? null :
                        Safe(() => body.GetBodyBox(), null));
                    bodyRow.faceCount = body == null ? -1 : Safe(() => body.GetFaceCount(), -1);
                    Array edges = body == null ? null : Safe(() => body.GetEdges() as Array, null);
                    bodyRow.edgeCount = edges == null ? 0 : edges.Length;
                    ReleaseComArrayItems(edges);
                    Array mass = body == null ? null : Safe(() =>
                        body.GetMassProperties(7850.0) as Array, null);
                    bodyRow.massPropertiesSi = MeasurementDoubleArray(mass);
                    if (bodyRow.massPropertiesSi.Count >= 6)
                    {
                        bodyRow.volumeMm3 = bodyRow.massPropertiesSi[3] * 1000000000.0;
                        bodyRow.surfaceAreaMm2 = bodyRow.massPropertiesSi[4] * 1000000.0;
                        bodyRow.massKg = bodyRow.massPropertiesSi[5];
                    }
                    bodyRow.complete = bodyRow.boxMm.Count == 6 &&
                        MeasurementValuesFinite(bodyRow.boxMm) &&
                        bodyRow.massPropertiesSi.Count >= 6 &&
                        MeasurementValuesFinite(bodyRow.massPropertiesSi) &&
                        bodyRow.volumeMm3 > 0.0 && bodyRow.surfaceAreaMm2 > 0.0 &&
                        bodyRow.massKg > 0.0 && bodyRow.faceCount > 0 &&
                        bodyRow.edgeCount > 0;
                }
                catch (Exception exception) { bodyRow.fault = SafeException(exception); }
                finally { Release(body); }
                row.bodies.Add(bodyRow);
            }
            if (row.bodies.Count > 0 && row.bodies.All(value => value.boxMm.Count == 6 &&
                    MeasurementValuesFinite(value.boxMm)))
            {
                row.partBoxMm = new List<double>
                {
                    row.bodies.Min(value => value.boxMm[0]),
                    row.bodies.Min(value => value.boxMm[1]),
                    row.bodies.Min(value => value.boxMm[2]),
                    row.bodies.Max(value => value.boxMm[3]),
                    row.bodies.Max(value => value.boxMm[4]),
                    row.bodies.Max(value => value.boxMm[5])
                };
                row.partBoxDerivedFromBodies = true;
            }
        }

        private static bool MeasurementValuesFinite(IEnumerable<double> values)
        {
            return values != null && values.All(value =>
                !double.IsNaN(value) && !double.IsInfinity(value));
        }

        private static void CaptureNativeTopCoverPartFeatures(ModelDoc2 model,
            NativeTopCoverPartMeasurement row, string cadDirectory)
        {
            List<string> diagnosticIssues = new List<string>();
            List<string> diagnosticLinks = new List<string>();
            bool diagnosticTraversalComplete = true;
            int diagnosticFeatureCount = 0;
            CaptureRelocationFeatureDiagnostics(model, row.name, cadDirectory,
                diagnosticIssues, diagnosticLinks, ref diagnosticTraversalComplete,
                ref diagnosticFeatureCount);
            row.featureIssues = diagnosticIssues;
            row.linkFeatures = diagnosticLinks;
            row.relocationFeatureTraversalComplete = diagnosticTraversalComplete;
            row.relocationFeatureCount = diagnosticFeatureCount;
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { row.featureTraversalComplete = false; return; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureNativeTopCoverPartFeatureRecursive(model, feature, "", row,
                    cadDirectory, 0);
                if (!row.featureTraversalComplete) { Release(feature); return; }
                Feature next = Safe(() => feature.GetNextFeature() as Feature, null);
                Release(feature);
                feature = next;
            }
            if (feature != null) row.featureTraversalComplete = false;
        }

        private static void CaptureNativeTopCoverPartFeatureRecursive(ModelDoc2 model,
            Feature feature,
            string parentPath, NativeTopCoverPartMeasurement row, string cadDirectory, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || row.features.Count > 20000)
            {
                row.featureTraversalComplete = false; return;
            }
            NativeTopCoverFeatureMeasurement record = new NativeTopCoverFeatureMeasurement
                { depth = depth };
            Feature child = null;
            try
            {
                record.name = feature.Name ?? "";
                record.type = feature.GetTypeName2() ?? "";
                record.path = string.IsNullOrWhiteSpace(parentPath) ? record.name :
                    parentPath + "/" + record.name;
                bool warning = false;
                record.errorCode = feature.GetErrorCode();
                record.errorCode2 = feature.GetErrorCode2(out warning);
                record.warning = warning;
                record.suppressed = feature.IsSuppressed();
                row.features.Add(record);
                row.typeCounts[record.type] = row.typeCounts.ContainsKey(record.type) ?
                    row.typeCounts[record.type] + 1 : 1;
                if (string.Equals(record.type, "SheetMetal", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(record.type, "FlatPattern", StringComparison.OrdinalIgnoreCase))
                    row.sheetMetalFeatures.Add(CaptureNativeTopCoverSheetMetalFeature(feature,
                        model, record.path, record.type));
                child = feature.GetFirstSubFeature() as Feature;
            }
            catch { row.featureTraversalComplete = false; return; }
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureNativeTopCoverPartFeatureRecursive(model, child, record.path, row,
                    cadDirectory, depth + 1);
                if (!row.featureTraversalComplete) { Release(child); return; }
                Feature next = Safe(() => child.GetNextSubFeature() as Feature, null);
                Release(child);
                child = next;
            }
            if (child != null) row.featureTraversalComplete = false;
        }

        private static NativeTopCoverSheetMetalMeasurement CaptureNativeTopCoverSheetMetalFeature(
            Feature feature, ModelDoc2 model, string path, string type)
        {
            NativeTopCoverSheetMetalMeasurement row = new NativeTopCoverSheetMetalMeasurement
                { path = path, type = type };
            object definition = null;
            try
            {
                definition = feature.GetDefinition();
                ISheetMetalFeatureData data = definition as ISheetMetalFeatureData;
                if (data != null)
                {
                    row.accessSelections = Safe(() => data.AccessSelections(null, null), false) ||
                        Safe(() => data.IAccessSelections2(model, null), false);
                    row.thicknessMm = Safe(() => data.Thickness * 1000.0, double.NaN);
                    row.bendRadiusMm = Safe(() => data.BendRadius * 1000.0, double.NaN);
                    row.kFactor = Safe(() => data.KFactor, double.NaN);
                    row.bendAllowanceType = Safe(() => data.BendAllowanceType, -1);
                    CustomBendAllowance allowance = Safe(() =>
                        data.GetCustomBendAllowance(), null);
                    try
                    {
                        row.customBendAllowanceAvailable = allowance != null;
                        if (allowance != null)
                        {
                            row.customBendAllowanceType = Safe(() => allowance.Type, -1);
                            row.customKFactor = Safe(() => allowance.KFactor, double.NaN);
                            row.customBendAllowanceMm = Safe(() =>
                                allowance.BendAllowance * 1000.0, double.NaN);
                            row.customBendDeductionMm = Safe(() =>
                                allowance.BendDeduction * 1000.0, double.NaN);
                            row.customBendTableFile = Safe(() =>
                                allowance.BendTableFile, "");
                        }
                    }
                    finally { Release(allowance); }
                    row.effectiveBendAllowanceType = row.bendAllowanceType >= 0 ?
                        row.bendAllowanceType : row.customBendAllowanceType;
                    row.readable = (row.accessSelections ||
                        row.customBendAllowanceAvailable) &&
                        MeasurementValuesFinite(new[] { row.thicknessMm,
                            row.bendRadiusMm, row.kFactor }) &&
                        row.thicknessMm > 0.0 && row.bendRadiusMm >= 0.0 &&
                        row.kFactor >= 0.0 && row.kFactor <= 1.0 &&
                        row.effectiveBendAllowanceType >= 0;
                    if (row.accessSelections) Try(() => data.ReleaseSelectionAccess());
                }
            }
            catch (Exception exception) { row.fault = SafeException(exception); }
            finally { Release(definition); }
            return row;
        }

        private static List<NativeTopCoverTemplateComponentState>
            CaptureNativeTopCoverTemplateComponentStates(ISldWorks application,
                string assemblyPath, string cadDirectory,
                NativeTopCoverPartMeasurementEvidence probe)
        {
            List<NativeTopCoverTemplateComponentState> output =
                new List<NativeTopCoverTemplateComponentState>();
            ModelDoc2 model = null;
            string hashBefore = File.Exists(assemblyPath) ? Sha256File(assemblyPath) : "";
            try
            {
                int cacheBefore;
                probe.templateCacheBeforeEmpty = DocumentCacheEmpty(application, out cacheBefore);
                if (!probe.templateCacheBeforeEmpty) return output;
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(assemblyPath, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                probe.templateOpenErrors = errors;
                probe.templateOpenWarnings = warnings;
                probe.templateReadOnly = model != null &&
                    Safe(() => model.IsOpenedReadOnly(), false);
                AssemblyDoc assembly = model as AssemblyDoc;
                if (model == null || assembly == null || errors != 0 ||
                    !probe.templateReadOnly) return output;
                probe.templateRebuilt = Safe(() => model.ForceRebuild3(false), false);
                probe.templateModelExternalReferenceCount = Safe(() =>
                    model.ListExternalFileReferencesCount2(), -1);
                ModelDocExtension extension = Safe(() => model.Extension, null);
                try
                {
                    probe.templateExtensionExternalReferenceCount = extension == null ? -1 :
                        Safe(() => extension.ListExternalFileReferencesCount(), -1);
                }
                finally { Release(extension); }
                Configuration configuration = model.GetActiveConfiguration() as Configuration;
                Component2 root = configuration == null ? null :
                    configuration.GetRootComponent3(true) as Component2;
                foreach (object item in root == null ? new object[0] : ObjectArray(root.GetChildren()))
                {
                    Component2 component = item as Component2;
                    try
                    {
                        string path = Safe(() => component.GetPathName(), "");
                        NativeTopCoverTemplateComponentState state =
                            new NativeTopCoverTemplateComponentState
                        {
                            name = Path.GetFileName(path), path = path,
                            sha256 = File.Exists(path) ? Sha256File(path) : "",
                            configuration = Safe(() => component.ReferencedConfiguration, ""),
                            suppression = Safe(() => component.GetSuppression(), -1),
                            directChildCount = ObjectArray(Safe(() =>
                                component.GetChildren(), null)).Length
                        };
                        try { state.hidden = component.IsHidden(true); state.hiddenCaptured = true; }
                        catch { state.hiddenCaptured = false; }
                        try { state.visible = component.Visible; state.visibleCaptured = true; }
                        catch { state.visibleCaptured = false; }
                        try { state.envelope = component.IsEnvelope(); state.envelopeCaptured = true; }
                        catch { state.envelopeCaptured = false; }
                        try
                        {
                            state.excludeFromBom = component.ExcludeFromBOM;
                            state.excludeFromBomCaptured = true;
                        }
                        catch { state.excludeFromBomCaptured = false; }
                        try
                        {
                            state.suppressed = component.IsSuppressed();
                            state.suppressedCaptured = true;
                        }
                        catch { state.suppressedCaptured = false; }
                        try { state.fixedState = component.IsFixed(); state.fixedCaptured = true; }
                        catch { state.fixedCaptured = false; }
                        MathTransform transform = Safe(() => component.Transform2, null);
                        try { state.transform16 = TransformValues(transform).ToList(); }
                        finally { Release(transform); }
                        output.Add(state);
                    }
                    finally { Release(component); }
                }
                Release(root); Release(configuration);
            }
            finally
            {
                CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int cacheAfter;
                probe.templateCacheAfterEmpty = application != null &&
                    DocumentCacheEmpty(application, out cacheAfter);
                probe.templateFileUnchanged = File.Exists(assemblyPath) &&
                    string.Equals(hashBefore, Sha256File(assemblyPath),
                        StringComparison.OrdinalIgnoreCase);
            }
            return output;
        }

        private static bool NativeTopCoverTemplateComponentStatesComplete(
            List<NativeTopCoverTemplateComponentState> rows, string cadDirectory)
        {
            HashSet<string> expected = new HashSet<string>(TopCoverCandidateUpstreamFirstFileNames
                .Skip(1), StringComparer.OrdinalIgnoreCase);
            HashSet<string> actual = new HashSet<string>((rows ??
                new List<NativeTopCoverTemplateComponentState>()).Select(value => value.name),
                StringComparer.OrdinalIgnoreCase);
            return rows != null && rows.Count == 6 && actual.Count == 6 &&
                actual.SetEquals(expected) && rows.All(value => File.Exists(value.path) &&
                    SamePath(Path.GetDirectoryName(value.path), cadDirectory) &&
                    FileLinkCount(value.path) == 1 && !HasReparsePoint(value.path) &&
                    IsSha256(value.sha256) && string.Equals(value.sha256,
                        Sha256File(value.path), StringComparison.OrdinalIgnoreCase) &&
                    value.hiddenCaptured && value.visibleCaptured &&
                    value.envelopeCaptured && value.excludeFromBomCaptured &&
                    value.suppressedCaptured && value.fixedCaptured &&
                    value.suppression >= 0 && value.visible >= 0 &&
                    ValidTransform16(value.transform16));
        }

        private static bool NativeTopCoverPartMeasurementComplete(
            NativeTopCoverPartMeasurementEvidence probe)
        {
            return probe != null && probe.probeOnly && !probe.consumable &&
                string.Equals(probe.mode, "measure_topcover_parts", StringComparison.Ordinal) &&
                probe.exactSix.Count == 6 && probe.parts.Count == 6 &&
                probe.exactSixSourceBindings &&
                probe.parts.All(NativeTopCoverPartRowMeasurementComplete) &&
                probe.templateComponentsComplete && probe.templateCacheBeforeEmpty &&
                probe.templateCacheAfterEmpty && probe.templateOpenErrors == 0 &&
                probe.templateOpenWarnings == 96 && probe.templateReadOnly &&
                probe.templateRebuilt && probe.templateFileUnchanged &&
                probe.templateModelExternalReferenceCount >= 0 &&
                probe.templateExtensionExternalReferenceCount >= 0 &&
                probe.sessionExitProven &&
                probe.sourceExact75Unchanged && probe.workingPackExact75Unchanged;
        }

        private static bool NativeTopCoverPartRowMeasurementComplete(
            NativeTopCoverPartMeasurement row)
        {
            if (row == null) return false;
            int sheetMetalCount = row.typeCounts.ContainsKey("SheetMetal") ?
                row.typeCounts["SheetMetal"] : 0;
            int flatPatternCount = row.typeCounts.ContainsKey("FlatPattern") ?
                row.typeCounts["FlatPattern"] : 0;
            bool masterModel = string.Equals(row.name,
                TopCoverCandidateUpstreamFirstFileNames[1],
                StringComparison.OrdinalIgnoreCase);
            bool sheetMetalProfileComplete = masterModel ?
                sheetMetalCount == 0 && flatPatternCount == 0 :
                sheetMetalCount > 0 && flatPatternCount > 0 &&
                row.sheetMetalFeatures.Where(value => string.Equals(value.type,
                    "SheetMetal", StringComparison.OrdinalIgnoreCase)).All(value =>
                        value.readable && (value.accessSelections ||
                            value.customBendAllowanceAvailable) &&
                        string.IsNullOrWhiteSpace(value.fault)) &&
                row.sheetMetalFeatures.Count(value => string.Equals(value.type,
                    "SheetMetal", StringComparison.OrdinalIgnoreCase)) == sheetMetalCount;
            return row.pathLocal && row.cacheBeforeEmpty && row.opened &&
                row.openErrors == 0 && row.openWarnings == 0 && row.readOnly && row.rebuilt &&
                row.saveFlagBeforeCaptured && row.saveFlagAfterCaptured &&
                row.cacheAfterEmpty && row.fileUnchanged && row.bodyCount > 0 &&
                row.partBoxMm.Count == 6 && MeasurementValuesFinite(row.partBoxMm) &&
                row.partBoxApiRaw.Count == 6 &&
                MeasurementValuesFinite(row.partBoxApiRaw) &&
                row.partBoxDerivedFromBodies &&
                row.bodies.Count == row.bodyCount && row.bodies.All(value => value.complete &&
                    string.IsNullOrWhiteSpace(value.fault)) && row.featureTraversalComplete &&
                row.relocationFeatureTraversalComplete && row.features.Count > 0 &&
                row.relocationFeatureCount == row.features.Count &&
                sheetMetalProfileComplete &&
                row.modelExternalReferenceCount >= 0 &&
                row.extensionExternalReferenceCount >= 0 && IsSha256(row.geometrySignatureSha256) &&
                string.IsNullOrWhiteSpace(row.fault) && string.IsNullOrWhiteSpace(row.geometryFault);
        }

        private static bool RunNativeTopCoverPartMeasurementCompletenessSelfTest()
        {
            NativeTopCoverBodyMeasurement body = new NativeTopCoverBodyMeasurement
            {
                index = 1,
                boxMm = new List<double> { 0, 0, 0, 10, 20, 1 },
                massPropertiesSi = new List<double> { 0, 0, 0, 0.0002, 0.0046, 1.57 },
                volumeMm3 = 200000,
                surfaceAreaMm2 = 4600,
                massKg = 1.57,
                faceCount = 6,
                edgeCount = 12,
                complete = true
            };
            NativeTopCoverPartMeasurement master = new NativeTopCoverPartMeasurement
            {
                name = TopCoverCandidateUpstreamFirstFileNames[1],
                pathLocal = true,
                cacheBeforeEmpty = true,
                opened = true,
                openErrors = 0,
                openWarnings = 0,
                readOnly = true,
                saveFlagBeforeCaptured = true,
                saveFlagAfterCaptured = true,
                rebuilt = true,
                bodyCount = 1,
                partBoxApiRaw = new List<double> { 0, 0, 0, 10, 20, 1 },
                partBoxMm = new List<double> { 0, 0, 0, 10, 20, 1 },
                partBoxDerivedFromBodies = true,
                bodies = new List<NativeTopCoverBodyMeasurement> { body },
                featureTraversalComplete = true,
                relocationFeatureTraversalComplete = true,
                relocationFeatureCount = 1,
                features = new List<NativeTopCoverFeatureMeasurement>
                {
                    new NativeTopCoverFeatureMeasurement { name = "Shell", path = "Shell",
                        type = "Shell" }
                },
                modelExternalReferenceCount = 1,
                extensionExternalReferenceCount = 1,
                geometrySignatureSha256 = new string('A', 64),
                cacheAfterEmpty = true,
                fileUnchanged = true
            };
            master.typeCounts["Shell"] = 1;
            NativeTopCoverPartMeasurement plate = new NativeTopCoverPartMeasurement
            {
                name = TopCoverCandidateUpstreamFirstFileNames[2],
                pathLocal = master.pathLocal,
                cacheBeforeEmpty = master.cacheBeforeEmpty,
                opened = master.opened,
                openErrors = 0,
                openWarnings = 0,
                readOnly = true,
                saveFlagBeforeCaptured = true,
                saveFlagAfterCaptured = true,
                rebuilt = true,
                bodyCount = 1,
                partBoxApiRaw = master.partBoxApiRaw.ToList(),
                partBoxMm = master.partBoxMm.ToList(),
                partBoxDerivedFromBodies = true,
                bodies = new List<NativeTopCoverBodyMeasurement> { body },
                featureTraversalComplete = true,
                relocationFeatureTraversalComplete = true,
                relocationFeatureCount = 2,
                features = new List<NativeTopCoverFeatureMeasurement>
                {
                    new NativeTopCoverFeatureMeasurement { name = "Sheet-Metal",
                        path = "Sheet-Metal", type = "SheetMetal" },
                    new NativeTopCoverFeatureMeasurement { name = "Flat-Pattern",
                        path = "Flat-Pattern", type = "FlatPattern" }
                },
                sheetMetalFeatures = new List<NativeTopCoverSheetMetalMeasurement>
                {
                    new NativeTopCoverSheetMetalMeasurement { path = "Sheet-Metal",
                        type = "SheetMetal", readable = true, accessSelections = true,
                        thicknessMm = 1.0, bendRadiusMm = 0.5, kFactor = 0.4,
                        bendAllowanceType = 1 },
                    new NativeTopCoverSheetMetalMeasurement { path = "Flat-Pattern",
                        type = "FlatPattern" }
                },
                modelExternalReferenceCount = 1,
                extensionExternalReferenceCount = 1,
                geometrySignatureSha256 = new string('B', 64),
                cacheAfterEmpty = true,
                fileUnchanged = true
            };
            plate.typeCounts["SheetMetal"] = 1;
            plate.typeCounts["FlatPattern"] = 1;
            bool valid = NativeTopCoverPartRowMeasurementComplete(master) &&
                NativeTopCoverPartRowMeasurementComplete(plate);
            plate.bodies[0].complete = false;
            bool bodyRejected = !NativeTopCoverPartRowMeasurementComplete(plate);
            plate.bodies[0].complete = true;
            plate.sheetMetalFeatures[0].readable = false;
            bool sheetMetalRejected = !NativeTopCoverPartRowMeasurementComplete(plate);
            plate.sheetMetalFeatures[0].readable = true;
            plate.saveFlagAfterCaptured = false;
            bool saveFlagCaptureRejected = !NativeTopCoverPartRowMeasurementComplete(plate);
            return valid && bodyRejected && sheetMetalRejected && saveFlagCaptureRejected;
        }

        private static bool RunNativeTopCoverPartDetachContractSelfTest()
        {
            string[] expected =
            {
                "上盖壳体左侧板.sldprt", "上盖壳体右侧板.SLDPRT",
                "上盖壳体前侧板.sldprt", "上盖壳体后侧板.sldprt",
                "上盖壳体底板.sldprt"
            };
            bool names = TopCoverPhysicalPartNames().SequenceEqual(expected) &&
                expected.Select(ExpectedTopCoverPhysicalExternalCount).SequenceEqual(
                    new[] { 2, 6, 2, 2, 9 });
            NativeTopCoverPartMeasurement row = new NativeTopCoverPartMeasurement
            {
                name = expected[0], bodyCount = 1, partBoxMm = new List<double> { 0, 0, 0, 1, 2, 3 },
                bodies = new List<NativeTopCoverBodyMeasurement> { new NativeTopCoverBodyMeasurement
                    { index = 1, boxMm = new List<double> { 0, 0, 0, 1, 2, 3 },
                      volumeMm3 = 6, surfaceAreaMm2 = 22, massKg = 1,
                      faceCount = 6, edgeCount = 12, complete = true } },
                sheetMetalFeatures = new List<NativeTopCoverSheetMetalMeasurement>
                { new NativeTopCoverSheetMetalMeasurement { path = "Sheet-Metal", type = "SheetMetal",
                    thicknessMm = 1, bendRadiusMm = .5, kFactor = .4,
                    effectiveBendAllowanceType = 1 } }
            };
            string physical = PhysicalGeometrySignature(row);
            string sheet = SheetMetalProfileSignature(row);
            row.features.Add(new NativeTopCoverFeatureMeasurement { name = "BaseBody", type = "BaseBody" });
            bool forbiddenRejected = HasDetachForbiddenFeature(row);
            row.features.Clear(); row.bodies[0].volumeMm3 = 7;
            bool geometryMismatchRejected = physical != PhysicalGeometrySignature(row);
            row.bodies[0].volumeMm3 = 6; row.sheetMetalFeatures[0].kFactor = .5;
            bool sheetMetalMismatchRejected = sheet != SheetMetalProfileSignature(row);
            NativeTopCoverPartDetachEvidence evidence = new NativeTopCoverPartDetachEvidence
                { probeOnly = true, consumable = false, mode = "detach_topcover_physical_parts" };
            return names && IsSha256(physical) && IsSha256(sheet) && forbiddenRejected &&
                geometryMismatchRejected && sheetMetalMismatchRejected && evidence.probeOnly &&
                !evidence.consumable;
        }

        private static bool RunNativeTopCoverRebuildCaptureContractSelfTest()
        {
            NativeTopCoverSketchPointCapture start = new NativeTopCoverSketchPointCapture
            {
                localSi = new List<double> { 0, 0, 0 },
                modelSi = new List<double> { 0, 0, 0 }
            };
            NativeTopCoverSketchPointCapture end = new NativeTopCoverSketchPointCapture
            {
                localSi = new List<double> { 1, 0, 0 },
                modelSi = new List<double> { 1, 0, 0 }
            };
            NativeTopCoverSketchSegmentCapture line =
                new NativeTopCoverSketchSegmentCapture
            {
                kind = "line",
                type = 0,
                ids = new List<int> { 1, 2 },
                status = 0,
                lengthSi = 1,
                constraintCount = 0,
                relationCount = 0,
                start = start,
                end = end
            };
            bool validLineAccepted = NativeTopCoverSketchSegmentComplete(line);
            line.ids.Clear();
            bool missingIdRejected = !NativeTopCoverSketchSegmentComplete(line);
            line.ids.AddRange(new[] { 1, 2 });
            line.kind = "unsupported";
            bool unsupportedRejected = !NativeTopCoverSketchSegmentComplete(line);
            bool emptyPartRejected = !NativeTopCoverRebuildPartCaptureComplete(
                new NativeTopCoverRebuildPartCapture
                    { name = "上盖壳体左侧板.sldprt" });
            bool emptyDatasetRejected = !NativeTopCoverRebuildCaptureComplete(
                new NativeTopCoverRebuildCaptureEvidence());
            bool bendCounts = ExpectedTopCoverPhysicalBendCount(
                    "上盖壳体左侧板.sldprt") == 5 &&
                ExpectedTopCoverPhysicalBendCount("上盖壳体前侧板.sldprt") == 2 &&
                ExpectedTopCoverPhysicalBendCount("上盖壳体底板.sldprt") == 7 &&
                ExpectedTopCoverPhysicalBendCount("unknown.sldprt") == -1;
            return validLineAccepted && missingIdRejected && unsupportedRejected &&
                emptyPartRejected && emptyDatasetRejected && bendCounts &&
                ExpectedGoldTopCoverSha256.Count == 14 &&
                GoldTopCoverCaptureCadFileNames.Length == 8 &&
                GoldTopCoverCaptureCadFileNames.Distinct(
                    StringComparer.OrdinalIgnoreCase).Count() == 8;
        }

        private static bool RunNativeTopCoverCapturePackRollbackSelfTest(string temp)
        {
            string repositoryRoot = FindRepositoryRoot(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(repositoryRoot)) return false;
            string source = Path.Combine(repositoryRoot,
                GoldTopCoverEngineeringRelativeDirectory.Replace('/',
                    Path.DirectorySeparatorChar));
            string attempt = Path.Combine(temp, "gold-capture-pack-attempt");
            string pack = Path.Combine(attempt, "native_cad", "gold_capture_pack");
            Directory.CreateDirectory(pack);
            foreach (string name in GoldTopCoverCaptureCadFileNames)
                File.Copy(Path.Combine(source, name), Path.Combine(pack, name), false);
            Result result = new Result
            {
                attempt_directory = attempt,
                topcover_capture_pack = pack,
                topcover_capture_pack_created = true
            };
            NativeTopCoverRebuildCaptureEvidence probe =
                new NativeTopCoverRebuildCaptureEvidence { goldCapturePack = pack };
            bool exactAccepted = GoldTopCoverCapturePackComplete(result, probe);
            string pairedLock = Path.Combine(pack, "~$上盖壳体左侧板.sldprt");
            File.WriteAllText(pairedLock, "lock", new UTF8Encoding(false));
            bool pairedLockAccepted = GoldTopCoverCapturePackComplete(result, probe);
            string orphanLock = Path.Combine(pack, "~$orphan.sldprt");
            File.WriteAllText(orphanLock, "lock", new UTF8Encoding(false));
            bool orphanRejected = !GoldTopCoverCapturePackComplete(result, probe);
            File.Delete(orphanLock);
            RollbackNativeTopCoverCapturePack(result);
            return exactAccepted && pairedLockAccepted && orphanRejected &&
                result.topcover_capture_pack_rollback_attempted &&
                result.topcover_capture_pack_rollback_succeeded &&
                result.topcover_capture_pack_transient_lock_files_removed == 1 &&
                !Directory.Exists(pack);
        }

        private static bool SourceExact75Unchanged(Result result)
        {
            VerifySourceUnchanged(result); return result.source_unchanged;
        }

        private static bool WorkingPackExact75Unchanged(Result result)
        {
            try
            {
                List<string> files = CaptureFlatCadTree(result.target_directory,
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_WORKING_PACK_INVALID");
                return files.Count == ExpectedCadCount && string.Equals(InventoryDigest(files.Select(
                    Snapshot)), result.source_inventory_digest, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool WorkingPackExact75WithPairedLocksUnchanged(Result result)
        {
            try
            {
                string target = result.target_directory;
                if (!Directory.Exists(target) || TreeHasReparsePoint(target) ||
                    Directory.GetDirectories(target, "*",
                        SearchOption.TopDirectoryOnly).Length != 0) return false;
                string[] files = Directory.GetFiles(target, "*", SearchOption.TopDirectoryOnly);
                List<string> lockFiles = files.Where(path => Path.GetFileName(path).StartsWith(
                    "~$", StringComparison.Ordinal)).ToList();
                List<string> cadFiles = files.Except(lockFiles,
                    StringComparer.OrdinalIgnoreCase).ToList();
                HashSet<string> expected = new HashSet<string>(result.source_hashes.Keys,
                    StringComparer.OrdinalIgnoreCase);
                return cadFiles.Count == ExpectedCadCount && new HashSet<string>(cadFiles.Select(
                    Path.GetFileName), StringComparer.OrdinalIgnoreCase).SetEquals(expected) &&
                    cadFiles.All(path => FileLinkCount(path) == 1 && !HasReparsePoint(path) &&
                        result.source_hashes.ContainsKey(Path.GetFileName(path)) && string.Equals(
                            Sha256File(path), result.source_hashes[Path.GetFileName(path)],
                            StringComparison.OrdinalIgnoreCase)) &&
                    lockFiles.Count <= ExpectedCadCount && lockFiles.All(path =>
                    {
                        string name = Path.GetFileName(path);
                        return name.Length > 2 && expected.Contains(name.Substring(2)) &&
                            PairedSolidWorksLockFileSafe(path, target, result.source_hashes);
                    });
            }
            catch { return false; }
        }

        private static List<double> MeasurementDoubleArray(object raw)
        {
            Array values = raw as Array;
            if (values == null) return new List<double>();
            List<double> output = new List<double>();
            foreach (object value in values)
            {
                try { output.Add(Convert.ToDouble(value, CultureInfo.InvariantCulture)); }
                catch { output.Add(double.NaN); }
            }
            return output;
        }

        private static List<double> MeasurementMmArray(object raw)
        {
            return MeasurementDoubleArray(raw).Select(value => value * 1000.0).ToList();
        }

        private static string NativeTopCoverPartGeometrySignature(
            NativeTopCoverPartMeasurement row)
        {
            List<string> lines = new List<string>
            {
                row.name, row.sourceSha256, row.bodyCount.ToString(CultureInfo.InvariantCulture),
                string.Join(",", row.partBoxMm.Select(MeasurementNumber))
            };
            lines.AddRange(row.bodies.Select(value => value.index.ToString(CultureInfo.InvariantCulture) +
                ":" + string.Join(",", value.boxMm.Select(MeasurementNumber)) + ":" +
                MeasurementNumber(value.volumeMm3) + ":" + MeasurementNumber(value.surfaceAreaMm2) +
                ":" + value.faceCount.ToString(CultureInfo.InvariantCulture) + ":" +
                value.edgeCount.ToString(CultureInfo.InvariantCulture)));
            lines.AddRange(row.features.Select(value => value.depth.ToString(
                CultureInfo.InvariantCulture) + ":" + value.path + ":" + value.type + ":" +
                value.errorCode.ToString(CultureInfo.InvariantCulture) + ":" +
                value.errorCode2.ToString(CultureInfo.InvariantCulture) + ":" + value.warning + ":" +
                value.suppressed));
            return Sha256Text(string.Join("\n", lines) + "\n");
        }

        private static string MeasurementNumber(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? "nonfinite" :
                value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool WriteNativeTopCoverPartMeasurementPrivateEvidence(Result result,
            NativeTopCoverPartMeasurementEvidence probe)
        {
            try
            {
                string evidencePath = Path.Combine(result.attempt_directory,
                    NativeTopCoverPartMeasurementPrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(evidencePath);
                string expectedParent = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && Directory.Exists(result.attempt_directory) &&
                    SamePath(parent, expectedParent) && IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_PRIVATE_EVIDENCE_SCOPE_INVALID", 69,
                    "measurement private evidence path escaped the current attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_PRIVATE_EVIDENCE_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_PRIVATE_EVIDENCE_PATH_INVALID");
                if (File.Exists(evidencePath)) return false;
                probe.privateEvidenceWritten = true;
                WriteCreateNewAndReread(evidencePath, JsonBytes(probe));
                bool written = File.Exists(evidencePath) && FileLinkCount(evidencePath) == 1 &&
                    !HasReparsePoint(evidencePath) && Bool(ReadJsonObject(evidencePath),
                        "privateEvidenceWritten") && Bool(ReadJsonObject(evidencePath), "probeOnly") &&
                    !Bool(ReadJsonObject(evidencePath), "consumable");
                probe.privateEvidenceWritten = written;
                return written;
            }
            catch { probe.privateEvidenceWritten = false; return false; }
        }

        private static void WriteNativeTopCoverPartMeasurementProbeCleanupEvidence(Result result)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverPartMeasurementCleanupPrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                if (File.Exists(path)) return;
                string parent = Path.GetDirectoryName(path);
                string expectedParent = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expectedParent) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_CLEANUP_SCOPE_INVALID", 69,
                    "measurement cleanup evidence path escaped the current attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PART_MEASUREMENT_CLEANUP_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "schema", "winnsen.16029.topcover_native_part_measurement_probe_cleanup.v1" },
                    { "probeOnly", true }, { "consumable", false },
                    { "mode", "measure_topcover_parts" },
                    { "processFinalGate", result.processes.final_gate },
                    { "protectedSourceUnchanged", result.source_unchanged },
                    { "targetRollbackAttempted", result.rollback_attempted },
                    { "targetRollbackSucceeded", result.rollback_succeeded },
                    { "targetAbsent", !Directory.Exists(result.target_directory) },
                    { "cleanupPass", result.processes.final_gate && result.source_unchanged &&
                        !Directory.Exists(result.target_directory) }
                };
                WriteCreateNewAndReread(path, JsonBytes(payload));
            }
            catch { }
        }

        // Bounded physical-part experiment.  This deliberately excludes the hidden master
        // model and every assembly: only the five sheet-metal plates may be written.
        private static void RunNativeTopCoverPartDetachProbeOnly(Result result)
        {
            NativeTopCoverPartDetachEvidence probe = new NativeTopCoverPartDetachEvidence
            {
                probeOnly = true, consumable = false, mode = "detach_topcover_physical_parts",
                protectedSourceInventoryDigestBefore = result.source_inventory_digest,
                sourceDirectory = result.source_directory, workingPack = result.target_directory,
                physicalParts = TopCoverPhysicalPartNames().ToList()
            };
            try
            {
                Require(result.direct_copy_completed && probe.physicalParts.Count == 5 &&
                    probe.physicalParts.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 5 &&
                    probe.physicalParts.All(name => ExactSourceBoundTargetFile(result, name)),
                    "TOPCOVER_NATIVE_PART_DETACH_PRECONDITION_FAILED", 68,
                    "detach requires the exact five physical plates in the direct-copy pack");
                probe.baseline = CaptureNativeTopCoverPhysicalParts(result, "baseline_readonly");
                Require(PhysicalPartBaselineComplete(probe.baseline),
                    "TOPCOVER_NATIVE_PART_DETACH_BASELINE_INVALID", 68,
                    "baseline must prove expected external counts and complete physical geometry");
                foreach (string name in probe.physicalParts)
                {
                    NativeTopCoverPartDetachOperation operation =
                        new NativeTopCoverPartDetachOperation
                        {
                            name = name,
                            path = Path.Combine(result.target_directory, name)
                        };
                    probe.operations.Add(operation);
                    DetachNativeTopCoverPhysicalPart(result, operation);
                }
                Require(probe.operations.All(DetachOperationComplete),
                    "TOPCOVER_NATIVE_PART_DETACH_OPERATION_INVALID", 68,
                    "every physical part must detach, save once, and exit its owned session");
                probe.after = CaptureNativeTopCoverPhysicalParts(result, "fresh_readonly_after_detach");
                probe.changedNames = ChangedSourceNames(result).ToList();
                probe.targetExact75Names = TargetExact75NameSet(result);
                probe.sourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.nonPhysicalTargetHashesUnchanged = result.source_inventory.Where(row =>
                    !probe.physicalParts.Contains(row.name, StringComparer.OrdinalIgnoreCase)).All(row =>
                    ExactSourceBoundTargetFile(result, row.name));
                probe.afterComplete = PhysicalPartAfterDetachComplete(probe.baseline, probe.after);
                probe.changedNamesExact = new HashSet<string>(probe.changedNames,
                    StringComparer.OrdinalIgnoreCase).SetEquals(probe.physicalParts);
                probe.pass = probe.afterComplete && probe.targetExact75Names && probe.changedNamesExact &&
                    probe.nonPhysicalTargetHashesUnchanged && probe.sourceExact75Unchanged;
                Require(probe.pass, "TOPCOVER_NATIVE_PART_DETACH_POSTCONDITION_FAILED", 68,
                    "detach must change exactly five physical plates and preserve physical geometry");
            }
            catch (StageException exception)
            {
                probe.failureStatus = exception.Status; probe.failureExitCode = exception.ExitCode;
                probe.failure = exception.Message; throw;
            }
            catch (Exception exception)
            {
                probe.failureStatus = "TOPCOVER_NATIVE_PART_DETACH_PROBE_FAILED";
                probe.failureExitCode = 68; probe.failure = SafeException(exception);
                throw new StageException(probe.failureStatus, 68, probe.failure);
            }
            finally
            {
                probe.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                probe.sourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.privateEvidenceWritten = WriteNativeTopCoverPartDetachPrivateEvidence(result, probe);
            }
            Require(probe.privateEvidenceWritten,
                "TOPCOVER_NATIVE_PART_DETACH_PRIVATE_EVIDENCE_WRITE_FAILED", 68,
                "detach probe must persist private evidence with reread proof");
        }

        private static IEnumerable<string> TopCoverPhysicalPartNames()
        {
            return TopCoverCandidateUpstreamFirstFileNames.Skip(2);
        }

        private static bool ExactSourceBoundTargetFile(Result result, string name)
        {
            string expected; string path = Path.Combine(result.target_directory, name);
            return result.source_hashes.TryGetValue(name, out expected) && File.Exists(path) &&
                FileLinkCount(path) == 1 && !HasReparsePoint(path) &&
                SamePath(Path.GetDirectoryName(path), result.target_directory) &&
                string.Equals(Sha256File(path), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TargetExact75NameSet(Result result)
        {
            try
            {
                List<string> files = CaptureFlatCadTree(result.target_directory,
                    "TOPCOVER_NATIVE_PART_DETACH_TARGET_INVENTORY_INVALID");
                return files.Count == ExpectedCadCount && new HashSet<string>(files.Select(
                    Path.GetFileName), StringComparer.OrdinalIgnoreCase).SetEquals(
                    result.source_hashes.Keys);
            }
            catch { return false; }
        }

        private static List<NativeTopCoverPartMeasurement> CaptureNativeTopCoverPhysicalParts(
            Result result, string purpose)
        {
            List<NativeTopCoverPartMeasurement> rows = new List<NativeTopCoverPartMeasurement>();
            ISldWorks application = null;
            SessionEvidence session = new SessionEvidence { purpose = purpose };
            result.sessions.Add(session);
            try
            {
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_PART_DETACH_NOT_QUIESCENT", 68,
                    "CAD processes were not stably empty before an owned read-only session");
                application = StartOwnedSolidWorks(result, session);
                foreach (string name in TopCoverPhysicalPartNames())
                    rows.Add(CaptureNativeTopCoverPartMeasurement(application,
                        Path.Combine(result.target_directory, name), result.target_directory));
            }
            finally
            {
                if (application != null) Try(() => application.CloseAllDocuments(true));
                CloseOwnedSolidWorks(ref application, result, session);
            }
            return rows;
        }

        private static void DetachNativeTopCoverPhysicalPart(Result result,
            NativeTopCoverPartDetachOperation operation)
        {
            Require(operation != null && !string.IsNullOrWhiteSpace(operation.name) &&
                SamePath(operation.path, Path.Combine(result.target_directory, operation.name)),
                "TOPCOVER_NATIVE_PART_DETACH_OPERATION_SCOPE_INVALID", 68,
                "detach operation evidence must be registered before its writable session");
            string name = operation.name;
            ISldWorks application = null;
            SessionEvidence session = new SessionEvidence
                { purpose = "topcover_native_part_detach_writable:" + name };
            result.sessions.Add(session); ModelDoc2 model = null; ModelDocExtension extension = null;
            try
            {
                operation.sourceHashBefore = Sha256File(operation.path);
                operation.sourceBindingBefore = ExactSourceBoundTargetFile(result, name);
                Require(operation.sourceBindingBefore && WaitForGlobalCadQuiescence(
                        () => ProcessIds("SLDWORKS"), () => ProcessIds("sldProcMon"),
                        30000, 1000, 200), "TOPCOVER_NATIVE_PART_DETACH_WRITE_PRECONDITION_FAILED",
                    68, "writable detach requires a source-bound local part and stable CAD quiescence");
                AssertAuthorizationStillValid(result, "before_topcover_native_part_detach_" + name);
                application = StartOwnedSolidWorks(result, session);
                int beforeDocuments; operation.cacheBeforeEmpty = DocumentCacheEmpty(application,
                    out beforeDocuments);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(operation.path, (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors,
                    ref warnings) as ModelDoc2;
                operation.opened = model != null; operation.openErrors = errors;
                operation.openWarnings = warnings;
                Require(operation.cacheBeforeEmpty && operation.opened && errors == 0 &&
                    warnings == 0 && !model.IsOpenedReadOnly(),
                    "TOPCOVER_NATIVE_PART_DETACH_WRITE_OPEN_FAILED", 68,
                    "physical part must open writable with exact 0/0 status");
                operation.modelExternalBefore = model.ListExternalFileReferencesCount2();
                extension = model.Extension;
                operation.extensionExternalBefore = extension.ListExternalFileReferencesCount();
                operation.expectedExternalBefore = ExpectedTopCoverPhysicalExternalCount(name);
                Require(operation.modelExternalBefore == operation.expectedExternalBefore &&
                    operation.extensionExternalBefore == operation.expectedExternalBefore,
                    "TOPCOVER_NATIVE_PART_DETACH_EXTERNAL_BASELINE_DRIFT", 68,
                    "physical part external-reference counts drifted from the fixed baseline");
                operation.breakCalled = true;
                extension.BreakAllExternalFileReferences2(true);
                operation.rebuilt = model.ForceRebuild3(false);
                operation.modelExternalAfter = model.ListExternalFileReferencesCount2();
                operation.extensionExternalAfter = extension.ListExternalFileReferencesCount();
                operation.featureHealth = CaptureFeatureHealth(model);
                operation.breakSucceeded = operation.modelExternalAfter == 0 &&
                    operation.extensionExternalAfter == 0;
                Require(operation.breakSucceeded && operation.rebuilt && operation.modelExternalAfter == 0 &&
                    operation.extensionExternalAfter == 0 && DetachFeatureHealthClean(operation.featureHealth),
                    "TOPCOVER_NATIVE_PART_DETACH_BREAK_FAILED", 68,
                    "external links or feature health did not pass after the one allowed break call");
                int saveErrors = 0; int saveWarnings = 0;
                operation.saveSucceeded = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref saveErrors, ref saveWarnings);
                operation.saveErrors = saveErrors; operation.saveWarnings = saveWarnings;
                operation.dirtyAfterSave = model.GetSaveFlag();
                Require(operation.saveSucceeded && saveErrors == 0 && saveWarnings == 0 &&
                    !operation.dirtyAfterSave, "TOPCOVER_NATIVE_PART_DETACH_SAVE_FAILED", 68,
                    "physical part requires exactly one silent clean Save3 after link detachment");
            }
            catch (Exception exception)
            {
                operation.fault = SafeException(exception);
                if (exception is StageException) throw;
                throw new StageException("TOPCOVER_NATIVE_PART_DETACH_OPERATION_FAILED", 68,
                    operation.fault);
            }
            finally
            {
                Release(extension); CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int afterDocuments; operation.cacheAfterEmpty = application != null &&
                    DocumentCacheEmpty(application, out afterDocuments);
                CloseOwnedSolidWorks(ref application, result, session);
                operation.sessionExitProven = session.exit_state == "exited" &&
                    session.monitors_exit_proven;
                operation.targetHashAfter = File.Exists(operation.path) ? Sha256File(operation.path) : "";
            }
        }

        private static int ExpectedTopCoverPhysicalExternalCount(string name)
        {
            if (string.Equals(name, "上盖壳体左侧板.sldprt", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(name, "上盖壳体右侧板.SLDPRT", StringComparison.OrdinalIgnoreCase)) return 6;
            if (string.Equals(name, "上盖壳体前侧板.sldprt", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(name, "上盖壳体后侧板.sldprt", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(name, "上盖壳体底板.sldprt", StringComparison.OrdinalIgnoreCase)) return 9;
            return -1;
        }

        private static bool DetachFeatureHealthClean(FeatureHealthEvidence health)
        {
            return health != null && health.traversal_complete && health.issues.Count == 0;
        }

        private static bool DetachOperationComplete(NativeTopCoverPartDetachOperation value)
        {
            return value != null && value.sourceBindingBefore && value.cacheBeforeEmpty &&
                value.opened && value.openErrors == 0 && value.openWarnings == 0 &&
                value.expectedExternalBefore >= 0 &&
                value.modelExternalBefore == value.expectedExternalBefore &&
                value.extensionExternalBefore == value.expectedExternalBefore && value.breakCalled &&
                value.breakSucceeded && value.rebuilt && value.modelExternalAfter == 0 &&
                value.extensionExternalAfter == 0 && DetachFeatureHealthClean(value.featureHealth) &&
                value.saveSucceeded && value.saveErrors == 0 && value.saveWarnings == 0 &&
                !value.dirtyAfterSave && value.sessionExitProven && value.cacheAfterEmpty &&
                IsSha256(value.targetHashAfter) && string.IsNullOrWhiteSpace(value.fault);
        }

        private static bool PhysicalPartBaselineComplete(List<NativeTopCoverPartMeasurement> rows)
        {
            return rows != null && rows.Count == 5 && rows.All(NativeTopCoverPartRowMeasurementComplete) &&
                rows.All(row => row.modelExternalReferenceCount == ExpectedTopCoverPhysicalExternalCount(row.name) &&
                    row.extensionExternalReferenceCount == ExpectedTopCoverPhysicalExternalCount(row.name) &&
                    IsSha256(row.physicalGeometrySignatureSha256) &&
                    row.physicalGeometrySignatureSha256 == PhysicalGeometrySignature(row));
        }

        private static bool PhysicalPartAfterDetachComplete(List<NativeTopCoverPartMeasurement> before,
            List<NativeTopCoverPartMeasurement> after)
        {
            return PhysicalPartBaselineComplete(before) && after != null && after.Count == 5 &&
                after.All(NativeTopCoverPartRowMeasurementComplete) && after.All(row =>
                    row.modelExternalReferenceCount == 0 && row.extensionExternalReferenceCount == 0 &&
                    row.featureIssues.Count == 0 && !HasDetachForbiddenFeature(row) &&
                    IsSha256(row.physicalGeometrySignatureSha256) &&
                    row.physicalGeometrySignatureSha256 == PhysicalGeometrySignature(row)) &&
                before.All(first => after.Any(second => string.Equals(first.name, second.name,
                    StringComparison.OrdinalIgnoreCase) && first.physicalGeometrySignatureSha256 ==
                    second.physicalGeometrySignatureSha256 && SheetMetalProfileSignature(first) ==
                    SheetMetalProfileSignature(second)));
        }

        private static bool HasDetachForbiddenFeature(NativeTopCoverPartMeasurement row)
        {
            string[] forbidden = { "BaseBody", "Imported", "ForeignBody", "SplitBody", "MirrorStock", "Reference" };
            return row.features.Any(feature => forbidden.Any(value =>
                feature.type.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                feature.name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // Geometry signature intentionally excludes source hash and feature tree.
        private static string PhysicalGeometrySignature(NativeTopCoverPartMeasurement row)
        {
            List<string> lines = new List<string> { row.name, row.bodyCount.ToString(
                CultureInfo.InvariantCulture), string.Join(",", row.partBoxMm.Select(MeasurementNumber)) };
            lines.AddRange(row.bodies.Select(value => value.index.ToString(CultureInfo.InvariantCulture) +
                ":" + string.Join(",", value.boxMm.Select(MeasurementNumber)) + ":" +
                MeasurementNumber(value.volumeMm3) + ":" + MeasurementNumber(value.surfaceAreaMm2) +
                ":" + MeasurementNumber(value.massKg) + ":" + value.faceCount + ":" + value.edgeCount));
            return Sha256Text(string.Join("\n", lines) + "\n");
        }

        private static string SheetMetalProfileSignature(NativeTopCoverPartMeasurement row)
        {
            return Sha256Text(string.Join("\n", row.sheetMetalFeatures.OrderBy(value => value.path,
                StringComparer.OrdinalIgnoreCase).Select(value => value.path + ":" + value.type + ":" +
                MeasurementNumber(value.thicknessMm) + ":" + MeasurementNumber(value.bendRadiusMm) +
                ":" + MeasurementNumber(value.kFactor) + ":" + value.effectiveBendAllowanceType)) + "\n");
        }

        private static bool WriteNativeTopCoverPartDetachPrivateEvidence(Result result,
            NativeTopCoverPartDetachEvidence probe)
        {
            try
            {
                string evidencePath = Path.Combine(result.attempt_directory,
                    NativeTopCoverPartDetachPrivateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(evidencePath);
                string expectedParent = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && Directory.Exists(result.attempt_directory) &&
                    SamePath(parent, expectedParent) && IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_PART_DETACH_PRIVATE_EVIDENCE_SCOPE_INVALID", 68,
                    "detach private evidence path escaped the current attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PART_DETACH_PRIVATE_EVIDENCE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                if (File.Exists(evidencePath)) return false;
                probe.privateEvidenceWritten = true; WriteCreateNewAndReread(evidencePath, JsonBytes(probe));
                bool written = File.Exists(evidencePath) && FileLinkCount(evidencePath) == 1 &&
                    !HasReparsePoint(evidencePath) && Bool(ReadJsonObject(evidencePath),
                    "privateEvidenceWritten") && Bool(ReadJsonObject(evidencePath), "probeOnly") &&
                    !Bool(ReadJsonObject(evidencePath), "consumable");
                probe.privateEvidenceWritten = written; return written;
            }
            catch { probe.privateEvidenceWritten = false; return false; }
        }

        private static void WriteNativeTopCoverPartDetachProbeCleanupEvidence(Result result)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverPartDetachCleanupPrivateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return; string parent = Path.GetDirectoryName(path);
                string expectedParent = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expectedParent) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_PART_DETACH_CLEANUP_SCOPE_INVALID", 68,
                    "detach cleanup evidence path escaped the current attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PART_DETACH_CLEANUP_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PART_DETACH_CLEANUP_CREATED_PATH_INVALID");
                Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.Ordinal)
                { { "schema", "winnsen.16029.topcover_native_part_detach_probe_cleanup.v1" },
                  { "probeOnly", true }, { "consumable", false },
                  { "processFinalGate", result.processes.final_gate },
                  { "protectedSourceUnchanged", result.source_unchanged },
                  { "targetRollbackAttempted", result.rollback_attempted },
                  { "targetRollbackSucceeded", result.rollback_succeeded },
                  { "targetRollbackError", result.rollback_error },
                  { "transientLockFilesRemoved",
                      result.rollback_transient_lock_files_removed },
                  { "targetAbsent", !Directory.Exists(result.target_directory) },
                  { "cleanupPass", result.processes.final_gate && result.source_unchanged &&
                    !Directory.Exists(result.target_directory) } };
                WriteCreateNewAndReread(path, JsonBytes(payload));
                Require(Bool(ReadJsonObject(path), "probeOnly") && !Bool(ReadJsonObject(path),
                    "consumable"), "TOPCOVER_NATIVE_PART_DETACH_CLEANUP_REREAD_FAILED", 68,
                    "detach cleanup evidence reread did not preserve private probe boundary");
            }
            catch { }
        }

        // Lightweight rule-completion probe.  The physical-part dataset is already frozen;
        // this branch opens only the V37 and gold five-body master references.
        private static void RunNativeTopCoverMasterReferenceCaptureProbeOnly(Result result)
        {
            NativeTopCoverMasterReferenceCaptureEvidence probe =
                new NativeTopCoverMasterReferenceCaptureEvidence
            {
                probeOnly = true,
                consumable = false,
                mode = "capture_topcover_master_references",
                protectedSourceInventoryDigestBefore = result.source_inventory_digest,
                v37WorkingPack = result.target_directory,
                goldEngineeringDirectory = Path.Combine(result.repository_root,
                    GoldTopCoverEngineeringRelativeDirectory.Replace('/',
                        Path.DirectorySeparatorChar)),
                goldCapturePack = Path.Combine(result.attempt_directory, "native_cad",
                    "gold_capture_pack")
            };
            try
            {
                Require(result.direct_copy_completed && ExactSourceBoundTargetFile(result,
                        "上盖 模型.sldprt"),
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_V37_BINDING_INVALID", 66,
                    "master-reference capture requires the exact V37 top-cover master copy");
                BindGoldTopCoverInputs(result, probe);
                CreateGoldTopCoverCapturePack(result, probe);
                probe.v37Master = CaptureNativeTopCoverRebuildPart(result,
                    Path.Combine(result.target_directory, "上盖 模型.sldprt"),
                    result.target_directory, "v37_master_reference");
                probe.v37Master.complete = NativeTopCoverMasterReferenceCaptureComplete(
                    probe.v37Master);
                probe.goldMaster = CaptureNativeTopCoverRebuildPart(result,
                    Path.Combine(probe.goldCapturePack, "上盖 模型.sldprt"),
                    probe.goldCapturePack, "gold_master_reference_copy");
                probe.goldMaster.complete = NativeTopCoverMasterReferenceCaptureComplete(
                    probe.goldMaster);
                probe.v37SourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.workingPackExact75Unchanged =
                    WorkingPackExact75WithPairedLocksUnchanged(result);
                probe.goldInputsUnchanged = GoldTopCoverInputsUnchanged(probe.goldInputs);
                probe.goldSourceLockFilesAfter = GoldTopCoverSourceLockFiles(
                    probe.goldEngineeringDirectory);
                probe.goldSourceLockStateUnchanged = probe.goldSourceLockFilesBefore.SequenceEqual(
                    probe.goldSourceLockFilesAfter, StringComparer.OrdinalIgnoreCase);
                probe.goldCapturePackComplete = GoldTopCoverCapturePackComplete(result, probe);
                probe.datasetComplete = NativeTopCoverMasterReferenceDatasetComplete(probe);
                Require(probe.datasetComplete,
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_CAPTURE_INCOMPLETE", 66,
                    "one or both five-body master references are incomplete");
            }
            catch (StageException exception)
            {
                probe.failureStatus = exception.Status;
                probe.failureExitCode = exception.ExitCode;
                probe.failure = exception.Message;
                throw;
            }
            catch (Exception exception)
            {
                probe.failureStatus = "TOPCOVER_NATIVE_MASTER_REFERENCE_CAPTURE_EXCEPTION";
                probe.failureExitCode = 66;
                probe.failure = SafeException(exception);
                throw new StageException(probe.failureStatus, 66, probe.failure);
            }
            finally
            {
                probe.completedAtUtc = DateTime.UtcNow.ToString("o",
                    CultureInfo.InvariantCulture);
                probe.v37SourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.workingPackExact75Unchanged =
                    WorkingPackExact75WithPairedLocksUnchanged(result);
                probe.goldInputsUnchanged = GoldTopCoverInputsUnchanged(probe.goldInputs);
                probe.goldSourceLockFilesAfter = GoldTopCoverSourceLockFiles(
                    probe.goldEngineeringDirectory);
                probe.goldSourceLockStateUnchanged = probe.goldSourceLockFilesBefore.SequenceEqual(
                    probe.goldSourceLockFilesAfter, StringComparer.OrdinalIgnoreCase);
                probe.goldCapturePackComplete = GoldTopCoverCapturePackComplete(result, probe);
                probe.privateEvidenceWritten =
                    WriteNativeTopCoverMasterReferenceCapturePrivateEvidence(result, probe);
            }
            Require(probe.privateEvidenceWritten,
                "TOPCOVER_NATIVE_MASTER_REFERENCE_PRIVATE_EVIDENCE_WRITE_FAILED", 66,
                "master-reference capture must persist private nonconsumable evidence");
        }

        private static bool NativeTopCoverMasterReferenceCaptureComplete(
            NativeTopCoverRebuildPartCapture row)
        {
            return row != null && row.sourceBinding.unchanged && row.cacheBeforeEmpty &&
                row.opened && row.openErrors == 0 && row.openWarnings == 0 && row.readOnly &&
                row.rebuilt && row.cacheAfterEmpty && row.sessionExitProven &&
                row.preopenDependencies.closure_count >= 2 &&
                row.preopenDependencies.all_target_local &&
                NativeTopCoverPartRowMeasurementComplete(row.measurement) &&
                row.measurement.bodyCount == 5 && row.measurement.featureIssues.Count == 0 &&
                row.measurement.modelExternalReferenceCount > 0 &&
                row.measurement.modelExternalReferenceCount ==
                    row.measurement.extensionExternalReferenceCount &&
                row.sketches.Count > 0 && row.sketches.All(value => value.complete) &&
                row.sketches.Sum(value => value.segmentCount) > 0 &&
                row.oneBends.Count == 0 && row.processBends.Count == 0 &&
                row.flatPatterns.Count == 0 && row.unresolved.Count == 0 &&
                string.IsNullOrWhiteSpace(row.fault);
        }

        private static bool NativeTopCoverMasterReferenceDatasetComplete(
            NativeTopCoverMasterReferenceCaptureEvidence probe)
        {
            return probe != null && probe.goldInputs.Count == 19 &&
                probe.goldInputs.All(value => value.unchanged) &&
                NativeTopCoverMasterReferenceCaptureComplete(probe.v37Master) &&
                NativeTopCoverMasterReferenceCaptureComplete(probe.goldMaster) &&
                probe.v37SourceExact75Unchanged && probe.workingPackExact75Unchanged &&
                probe.goldInputsUnchanged && probe.goldSourceLockStateUnchanged &&
                probe.goldCapturePackComplete;
        }

        private static bool WriteNativeTopCoverMasterReferenceCapturePrivateEvidence(
            Result result, NativeTopCoverMasterReferenceCaptureEvidence probe)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverMasterReferenceCapturePrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(path);
                string expected = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expected) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_EVIDENCE_SCOPE_INVALID", 66,
                    "master-reference private evidence escaped attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_EVIDENCE_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_EVIDENCE_PATH_INVALID");
                if (File.Exists(path)) return false;
                Require(probe.v37SourceExact75Unchanged &&
                    probe.workingPackExact75Unchanged && probe.goldInputsUnchanged &&
                    probe.goldSourceLockStateUnchanged,
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_EVIDENCE_SOURCE_DRIFT", 66,
                    "source state drifted before master-reference evidence write");
                probe.privateEvidenceWritten = true;
                WriteCreateNewAndReread(path, JsonBytes(probe));
                Dictionary<string, object> reread = ReadJsonObject(path);
                probe.privateEvidenceWritten = FileLinkCount(path) == 1 &&
                    !HasReparsePoint(path) && Bool(reread, "privateEvidenceWritten") &&
                    Bool(reread, "probeOnly") && !Bool(reread, "consumable");
                return probe.privateEvidenceWritten;
            }
            catch { return probe.privateEvidenceWritten = false; }
        }

        private static void WriteNativeTopCoverMasterReferenceCaptureProbeCleanupEvidence(
            Result result)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverMasterReferenceCaptureCleanupPrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                if (File.Exists(path)) return;
                string parent = Path.GetDirectoryName(path);
                string expected = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expected) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_CLEANUP_SCOPE_INVALID", 66,
                    "master-reference cleanup evidence escaped attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_CLEANUP_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_CLEANUP_PATH_INVALID");
                bool goldAbsent = string.IsNullOrWhiteSpace(result.topcover_capture_pack) ||
                    !Directory.Exists(result.topcover_capture_pack);
                Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "schema", "winnsen.16029.topcover_native_master_reference_capture_cleanup.v1" },
                    { "probeOnly", true },
                    { "consumable", false },
                    { "processFinalGate", result.processes.final_gate },
                    { "protectedSourceUnchanged", result.source_unchanged },
                    { "targetRollbackAttempted", result.rollback_attempted },
                    { "targetRollbackSucceeded", result.rollback_succeeded },
                    { "targetRollbackError", result.rollback_error },
                    { "goldCapturePackRollbackAttempted",
                        result.topcover_capture_pack_rollback_attempted },
                    { "goldCapturePackRollbackSucceeded",
                        result.topcover_capture_pack_rollback_succeeded },
                    { "goldCapturePackRollbackError",
                        result.topcover_capture_pack_rollback_error },
                    { "targetAbsent", !Directory.Exists(result.target_directory) },
                    { "goldCapturePackAbsent", goldAbsent },
                    { "cleanupPass", result.processes.final_gate && result.source_unchanged &&
                        !Directory.Exists(result.target_directory) && goldAbsent }
                };
                WriteCreateNewAndReread(path, JsonBytes(payload));
                Require(Bool(ReadJsonObject(path), "probeOnly") &&
                    !Bool(ReadJsonObject(path), "consumable"),
                    "TOPCOVER_NATIVE_MASTER_REFERENCE_CLEANUP_REREAD_FAILED", 66,
                    "master-reference cleanup evidence private boundary drifted");
            }
            catch { }
        }

        // This deliberately narrower capture follows the same protected-input and
        // attempt-local-copy rules as the reconstruction dataset, but opens only the
        // front plate in each local pack.  It is not a construction step.
        private static void RunNativeTopCoverFrontBrepCaptureProbeOnly(Result result)
        {
            NativeTopCoverFrontBrepCaptureEvidence probe =
                new NativeTopCoverFrontBrepCaptureEvidence
            {
                probeOnly = true, consumable = false, mode = "capture_topcover_front_brep",
                protectedSourceInventoryDigestBefore = result.source_inventory_digest,
                v37WorkingPack = result.target_directory,
                goldEngineeringDirectory = Path.Combine(result.repository_root,
                    GoldTopCoverEngineeringRelativeDirectory.Replace('/', Path.DirectorySeparatorChar)),
                goldCapturePack = Path.Combine(result.attempt_directory, "native_cad",
                    "gold_capture_pack")
            };
            const string front = "上盖壳体前侧板.sldprt";
            try
            {
                Require(result.direct_copy_completed && ExactSourceBoundTargetFile(result, front),
                    "TOPCOVER_NATIVE_FRONT_BREP_V37_BINDING_INVALID", 65,
                    "front B-rep capture requires the exact V37 front-plate working copy");
                BindGoldTopCoverInputs(result, probe);
                CreateGoldTopCoverCapturePack(result, probe);
                probe.v37 = CaptureNativeTopCoverFrontBrepPart(result,
                    Path.Combine(result.target_directory, front), result.target_directory,
                    "v37_front_exact75", 48, 112);
                probe.gold = CaptureNativeTopCoverFrontBrepPart(result,
                    Path.Combine(probe.goldCapturePack, front), probe.goldCapturePack,
                    "gold_front_exact8", 52, 120);
                RefreshNativeTopCoverFrontBrepCaptureGuards(result, probe);
                probe.datasetComplete = NativeTopCoverFrontBrepCaptureComplete(probe);
                Require(probe.datasetComplete, "TOPCOVER_NATIVE_FRONT_BREP_CAPTURE_INCOMPLETE", 65,
                    "front B-rep capture has incomplete topology, local dependency, or source guards");
            }
            catch (StageException exception)
            {
                probe.failureStatus = exception.Status; probe.failureExitCode = exception.ExitCode;
                probe.failure = exception.Message; throw;
            }
            catch (Exception exception)
            {
                probe.failureStatus = "TOPCOVER_NATIVE_FRONT_BREP_CAPTURE_EXCEPTION";
                probe.failureExitCode = 65; probe.failure = SafeException(exception);
                throw new StageException(probe.failureStatus, 65, probe.failure);
            }
            finally
            {
                probe.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                RefreshNativeTopCoverFrontBrepCaptureGuards(result, probe);
                probe.privateEvidenceWritten = WriteNativeTopCoverFrontBrepCapturePrivateEvidence(
                    result, probe);
            }
            Require(probe.privateEvidenceWritten,
                "TOPCOVER_NATIVE_FRONT_BREP_PRIVATE_EVIDENCE_WRITE_FAILED", 65,
                "front B-rep capture must persist private nonconsumable evidence");
        }

        private static void RefreshNativeTopCoverFrontBrepCaptureGuards(Result result,
            NativeTopCoverFrontBrepCaptureEvidence probe)
        {
            probe.v37SourceExact75Unchanged = SourceExact75Unchanged(result);
            probe.workingPackExact75Unchanged = WorkingPackExact75WithPairedLocksUnchanged(result);
            probe.goldInputsUnchanged = GoldTopCoverInputsUnchanged(probe.goldInputs);
            probe.goldSourceLockFilesAfter = GoldTopCoverSourceLockFiles(
                probe.goldEngineeringDirectory);
            probe.goldSourceLockStateUnchanged = probe.goldSourceLockFilesBefore.SequenceEqual(
                probe.goldSourceLockFilesAfter, StringComparer.OrdinalIgnoreCase);
            probe.goldCapturePackComplete = GoldTopCoverCapturePackComplete(result, probe);
        }

        private static NativeTopCoverFrontBrepPartCapture CaptureNativeTopCoverFrontBrepPart(
            Result result, string partPath, string expectedDirectory, string purpose,
            int expectedFaces, int expectedEdges)
        {
            NativeTopCoverFrontBrepPartCapture output = new NativeTopCoverFrontBrepPartCapture
            {
                purpose = purpose, path = partPath, name = Path.GetFileName(partPath),
                expectedFaceCount = expectedFaces, expectedEdgeCount = expectedEdges,
                sourceBinding = CaptureNativeTopCoverInputBinding(partPath, expectedDirectory, purpose)
            };
            ISldWorks application = null; ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = purpose };
            result.sessions.Add(session);
            try
            {
                Require(GoldTopCoverInputBindingComplete(output.sourceBinding) &&
                    WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_FRONT_BREP_PRECONDITION_FAILED", 65,
                    "front B-rep capture requires a source-bound file and stable empty CAD set");
                application = StartOwnedSolidWorks(result, session);
                Require(application.SetCurrentWorkingDirectory(expectedDirectory),
                    "TOPCOVER_NATIVE_FRONT_BREP_WORKING_DIRECTORY_FAILED", 65,
                    "front B-rep session could not bind the attempt-local working directory");
                output.preopenDependencies = CaptureDependencies(application, partPath,
                    expectedDirectory);
                Require(output.preopenDependencies.closure_count > 0 &&
                    output.preopenDependencies.all_target_local,
                    "TOPCOVER_NATIVE_FRONT_BREP_DEPENDENCY_ESCAPE", 65,
                    "front B-rep dependencies must all resolve in the local pack");
                int documentsBefore; output.cacheBeforeEmpty = DocumentCacheEmpty(application,
                    out documentsBefore);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(partPath, (int)swDocumentTypes_e.swDocPART,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                output.opened = model != null; output.openErrors = errors;
                output.openWarnings = warnings;
                output.readOnly = model != null && Safe(() => model.IsOpenedReadOnly(), false);
                Require(output.cacheBeforeEmpty && output.opened && errors == 0 && warnings == 0 &&
                    output.readOnly, "TOPCOVER_NATIVE_FRONT_BREP_OPEN_FAILED", 65,
                    "each front plate requires one owned read-only 0/0 session");
                output.rebuilt = Safe(() => model.ForceRebuild3(false), false);
                output.modelExternalReferenceCount = Safe(() =>
                    model.ListExternalFileReferencesCount2(), -1);
                ModelDocExtension extension = Safe(() => model.Extension, null);
                try { output.extensionExternalReferenceCount = extension == null ? -1 :
                    Safe(() => extension.ListExternalFileReferencesCount(), -1); }
                finally { Release(extension); }
                CaptureNativeTopCoverFrontBrep(model, output);
            }
            catch (StageException exception) { output.unresolved.Add(exception.Status); output.fault = exception.Message; }
            catch (Exception exception) { output.unresolved.Add("exception"); output.fault = SafeException(exception); }
            finally
            {
                CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int documentsAfter; output.cacheAfterEmpty = application != null &&
                    DocumentCacheEmpty(application, out documentsAfter);
                CloseOwnedSolidWorks(ref application, result, session);
                output.sessionExitProven = session.exit_state == "exited" &&
                    session.monitors_exit_proven;
                output.sourceBinding.sizeBytesAfter = File.Exists(partPath) ?
                    new FileInfo(partPath).Length : -1;
                output.sourceBinding.sha256After = File.Exists(partPath) ? Sha256File(partPath) : "";
                output.sourceBinding.unchanged = output.sourceBinding.sizeBytesBefore ==
                    output.sourceBinding.sizeBytesAfter && string.Equals(output.sourceBinding.sha256Before,
                    output.sourceBinding.sha256After, StringComparison.OrdinalIgnoreCase) &&
                    FileLinkCount(partPath) == 1 && !HasReparsePoint(partPath);
                output.complete = NativeTopCoverFrontBrepPartCaptureComplete(output);
            }
            return output;
        }

        private static void CaptureNativeTopCoverFrontBrep(ModelDoc2 model,
            NativeTopCoverFrontBrepPartCapture output)
        {
            PartDoc part = model as PartDoc;
            Array bodies = null;
            try
            {
                Require(part != null, "TOPCOVER_NATIVE_FRONT_BREP_NOT_PART", 65,
                    "front B-rep capture opened a non-part document");
                bodies = Safe(() => part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array,
                    null);
                output.bodyCount = bodies == null ? 0 : bodies.Length;
                if (output.bodyCount != 1) { output.unresolved.Add("body_count"); return; }
                Body2 body = bodies.GetValue(0) as Body2;
                try
                {
                    if (body == null) { output.unresolved.Add("solid_body_missing"); return; }
                    output.bodyBoxSi = ComDoubleList(Safe(() => body.GetBodyBox(), null));
                    output.faceCount = Safe(() => body.GetFaceCount(), -1);
                    output.edgeCount = Safe(() => body.GetEdgeCount(), -1);
                    Array faces = Safe(() => body.GetFaces() as Array, null);
                    try
                    {
                        int faceIndex = 0;
                        foreach (object rawFace in faces ?? new object[0])
                        {
                            Face2 face = rawFace as Face2;
                            if (face == null) { output.unresolved.Add("non_face:" + faceIndex++); continue; }
                            try { output.faces.Add(CaptureNativeTopCoverFrontBrepFace(face, faceIndex++)); }
                            finally { Release(face); }
                        }
                    }
                    finally { /* each Face2 above owns exactly one release */ }
                    // Edges are intentionally obtained only from owning Loop2 instances below.
                    // This avoids releasing a body-level edge RCW and its loop-level counterpart.
                    output.loopEdgeReferenceCount = output.faces.SelectMany(value => value.loops).
                        Sum(value => value.edges.Count);
                }
                finally { Release(body); }
            }
            catch (Exception exception) { output.unresolved.Add("brep:" + SafeException(exception)); }
            finally { /* body array items are released individually; never bulk release this array */ }
            output.geometrySignatureSha256 = NativeTopCoverFrontBrepGeometrySignature(output);
        }

        private static NativeTopCoverFrontBrepFace CaptureNativeTopCoverFrontBrepFace(Face2 face,
            int faceIndex)
        {
            NativeTopCoverFrontBrepFace row = new NativeTopCoverFrontBrepFace { index = faceIndex };
            Surface surface = null;
            try
            {
                row.areaSi = Safe(() => face.GetArea(), double.NaN);
                row.boxSi = ComDoubleList(Safe(() => face.GetBox(), null));
                row.normalSi = ComDoubleList(Safe(() => face.Normal, null));
                row.loopCount = Safe(() => face.GetLoopCount(), -1);
                surface = ComRead(face, "GetSurface") as Surface;
                row.surfaceIdentity = ComInt(ComRead(surface, "Identity"), -1);
                row.isPlane = ComBool(ComRead(surface, "IsPlane"));
                row.isCylinder = ComBool(ComRead(surface, "IsCylinder"));
                if (row.isPlane) row.planeParamsSi = ComDoubleList(ComRead(surface, "PlaneParams"));
                if (row.isCylinder) row.cylinderParamsSi = ComDoubleList(ComRead(surface, "CylinderParams"));
                Array loops = ComArray(ComRead(face, "GetLoops"));
                int loopIndex = 0;
                foreach (object rawLoop in loops)
                {
                    Loop2 loop = rawLoop as Loop2;
                    if (loop == null) { row.unresolved.Add("non_loop:" + loopIndex++); continue; }
                    try { row.loops.Add(CaptureNativeTopCoverFrontBrepLoop(loop, loopIndex++)); }
                    finally { Release(loop); }
                }
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            finally { Release(surface); }
            row.complete = NativeTopCoverFrontBrepFaceComplete(row);
            return row;
        }

        private static NativeTopCoverFrontBrepLoop CaptureNativeTopCoverFrontBrepLoop(Loop2 loop,
            int loopIndex)
        {
            NativeTopCoverFrontBrepLoop row = new NativeTopCoverFrontBrepLoop
                { index = loopIndex, isOuter = Safe(() => loop.IsOuter(), false) };
            try
            {
                row.edgeCount = Safe(() => loop.GetEdgeCount(), -1);
                Array edges = ComArray(ComRead(loop, "GetEdges"));
                int order = 0;
                foreach (object rawEdge in edges)
                {
                    Edge edge = rawEdge as Edge;
                    if (edge == null) { row.unresolved.Add("non_edge:" + order++); continue; }
                    try { row.edges.Add(CaptureNativeTopCoverFrontBrepEdge(edge, loopIndex, order++)); }
                    finally { Release(edge); }
                }
                row.capturedEdgeCount = row.edges.Count;
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            row.complete = row.edgeCount > 0 && row.capturedEdgeCount == row.edgeCount &&
                row.edges.All(NativeTopCoverFrontBrepEdgeComplete) && row.unresolved.Count == 0;
            return row;
        }

        private static NativeTopCoverFrontBrepEdge CaptureNativeTopCoverFrontBrepEdge(Edge edge,
            int loopIndex, int order)
        {
            NativeTopCoverFrontBrepEdge row = new NativeTopCoverFrontBrepEdge
                { loopIndex = loopIndex, order = order };
            Curve curve = null; object curveParams = null; Vertex start = null; Vertex end = null;
            try
            {
                curveParams = ComRead(edge, "GetCurveParams3");
                row.curveType = ComInt(ComRead(curveParams, "CurveType"), -1);
                row.curveTag = ComInt(ComRead(curveParams, "CurveTag"), -1);
                row.sense = ComInt(ComRead(curveParams, "Sense"), -1);
                row.uMin = ComDouble(ComRead(curveParams, "UMinValue"), double.NaN);
                row.uMax = ComDouble(ComRead(curveParams, "UMaxValue"), double.NaN);
                row.startPointSi = ComDoubleList(ComRead(curveParams, "StartPoint"));
                row.endPointSi = ComDoubleList(ComRead(curveParams, "EndPoint"));
                curve = ComRead(edge, "GetCurve") as Curve;
                row.isCircle = ComBool(ComRead(curve, "IsCircle"));
                row.isLine = ComBool(ComRead(curve, "IsLine"));
                if (row.isCircle) row.circleParamsSi = ComDoubleList(ComRead(curve, "CircleParams"));
                if (row.isLine) row.lineParamsSi = ComDoubleList(ComRead(curve, "LineParams"));
                row.lengthSi = ComDouble(ComRead(curve, "GetLength3", row.uMin, row.uMax),
                    double.NaN);
                start = ComRead(edge, "GetStartVertex") as Vertex;
                end = ComRead(edge, "GetEndVertex") as Vertex;
                row.startVertexPointSi = ComDoubleList(ComRead(start, "GetPoint"));
                row.endVertexPointSi = ComDoubleList(ComRead(end, "GetPoint"));
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            finally { Release(end); Release(start); Release(curve); Release(curveParams); }
            row.complete = NativeTopCoverFrontBrepEdgeComplete(row);
            return row;
        }

        private static int ComInt(object value, int fallback)
        {
            try { return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }
        private static double ComDouble(object value, double fallback)
        {
            try { return value == null ? fallback : Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }
        private static bool ComBool(object value)
        {
            try
            {
                if (value is bool) return (bool)value;
                return value != null && Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
            }
            catch { return false; }
        }

        private static bool NativeTopCoverFrontBrepEdgeComplete(NativeTopCoverFrontBrepEdge row)
        {
            bool curve = row != null && row.curveType >= 0 && row.curveTag >= 0 &&
                row.sense >= 0 && row.lengthSi > 0 && MeasurementValuesFinite(new[]
                { row.lengthSi, row.uMin, row.uMax }) && row.startPointSi.Count == 3 &&
                row.endPointSi.Count == 3 && MeasurementValuesFinite(row.startPointSi) &&
                MeasurementValuesFinite(row.endPointSi);
            bool vertices = row != null && (row.startVertexPointSi.Count == 0 ||
                (row.startVertexPointSi.Count == 3 && MeasurementValuesFinite(row.startVertexPointSi))) &&
                (row.endVertexPointSi.Count == 0 || (row.endVertexPointSi.Count == 3 &&
                    MeasurementValuesFinite(row.endVertexPointSi)));
            bool specialized = row != null && (!row.isCircle || (row.circleParamsSi.Count == 7 &&
                row.circleParamsSi[6] > 0 && MeasurementValuesFinite(row.circleParamsSi))) && (!row.isLine ||
                (row.lineParamsSi.Count >= 6 && MeasurementValuesFinite(row.lineParamsSi)));
            return curve && vertices && specialized && row.unresolved.Count == 0;
        }

        private static bool NativeTopCoverFrontBrepFaceComplete(NativeTopCoverFrontBrepFace row)
        {
            bool surface = row != null && row.surfaceIdentity >= 0 && (row.isPlane || row.isCylinder) &&
                (!row.isPlane || (row.planeParamsSi.Count >= 6 && MeasurementValuesFinite(row.planeParamsSi))) &&
                (!row.isCylinder || (row.cylinderParamsSi.Count >= 7 &&
                    MeasurementValuesFinite(row.cylinderParamsSi)));
            return surface && row.areaSi > 0 && row.boxSi.Count == 6 &&
                MeasurementValuesFinite(new[] { row.areaSi }) && MeasurementValuesFinite(row.boxSi) &&
                row.loopCount > 0 && row.loops.Count == row.loopCount &&
                row.loops.All(value => value.complete) &&
                row.unresolved.Count == 0;
        }

        private static bool NativeTopCoverFrontBrepPartCaptureComplete(
            NativeTopCoverFrontBrepPartCapture row)
        {
            return row != null && row.sourceBinding.unchanged && row.cacheBeforeEmpty && row.opened &&
                row.openErrors == 0 && row.openWarnings == 0 && row.readOnly && row.rebuilt &&
                row.cacheAfterEmpty && row.sessionExitProven && row.preopenDependencies.closure_count > 0 &&
                row.preopenDependencies.all_target_local && row.bodyCount == 1 &&
                row.faceCount == row.expectedFaceCount && row.edgeCount == row.expectedEdgeCount &&
                row.bodyBoxSi.Count == 6 && MeasurementValuesFinite(row.bodyBoxSi) &&
                row.faces.Count == row.expectedFaceCount && row.faces.All(NativeTopCoverFrontBrepFaceComplete) &&
                row.loopEdgeReferenceCount >= row.expectedEdgeCount &&
                row.faces.SelectMany(value => value.loops).SelectMany(value => value.edges).Count() ==
                    row.loopEdgeReferenceCount && row.faces.Any(value => value.isCylinder) &&
                row.faces.SelectMany(value => value.loops).SelectMany(value => value.edges).Any(
                    value => value.isCircle) && IsSha256(row.geometrySignatureSha256) &&
                row.modelExternalReferenceCount == 2 &&
                row.extensionExternalReferenceCount == 2 &&
                row.unresolved.Count == 0 && string.IsNullOrWhiteSpace(row.fault);
        }

        private static bool NativeTopCoverFrontBrepCaptureComplete(
            NativeTopCoverFrontBrepCaptureEvidence probe)
        {
            return probe != null && probe.goldInputs.Count == 19 &&
                probe.goldInputs.All(value => value.unchanged) &&
                NativeTopCoverFrontBrepPartCaptureComplete(probe.v37) &&
                NativeTopCoverFrontBrepPartCaptureComplete(probe.gold) &&
                probe.v37SourceExact75Unchanged && probe.workingPackExact75Unchanged &&
                probe.goldInputsUnchanged && probe.goldSourceLockStateUnchanged &&
                probe.goldCapturePackComplete;
        }

        private static string NativeTopCoverFrontBrepGeometrySignature(
            NativeTopCoverFrontBrepPartCapture value)
        {
            if (value == null) return "";
            List<string> rows = new List<string>();
            foreach (NativeTopCoverFrontBrepFace face in value.faces)
            {
                rows.Add("F|" + R(face.areaSi) + "|" + JoinR(face.boxSi) +
                    "|" + face.surfaceIdentity + "|" + face.isPlane + "|" + face.isCylinder +
                    "|" + JoinR(face.planeParamsSi) + "|" + JoinR(face.cylinderParamsSi));
                foreach (NativeTopCoverFrontBrepLoop loop in face.loops)
                {
                    rows.Add("L|" + loop.isOuter + "|" + loop.edgeCount);
                    foreach (NativeTopCoverFrontBrepEdge edge in loop.edges)
                        rows.Add("E|" + edge.curveType + "|" + edge.sense + "|" +
                            R(edge.uMin) + "|" + R(edge.uMax) + "|" + R(edge.lengthSi) + "|" +
                            JoinR(edge.startPointSi) + "|" + JoinR(edge.endPointSi) + "|" +
                            edge.isCircle + "|" + JoinR(edge.circleParamsSi) + "|" + edge.isLine +
                            "|" + JoinR(edge.lineParamsSi));
                }
            }
            return Sha256Text(string.Join("\n", rows.OrderBy(item => item, StringComparer.Ordinal)) + "\n");
        }
        private static string R(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static string JoinR(IEnumerable<double> values) { return values == null ? "" :
            string.Join(",", values.Select(R)); }

        private static bool WriteNativeTopCoverFrontBrepCapturePrivateEvidence(Result result,
            NativeTopCoverFrontBrepCaptureEvidence probe)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverFrontBrepCapturePrivateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(path);
                string expected = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expected) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_FRONT_BREP_EVIDENCE_SCOPE_INVALID", 65,
                    "front B-rep private evidence escaped the current attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_FRONT_BREP_EVIDENCE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_FRONT_BREP_EVIDENCE_CREATED_PATH_INVALID");
                if (File.Exists(path)) return false;
                Require(probe.v37SourceExact75Unchanged && probe.workingPackExact75Unchanged &&
                    probe.goldInputsUnchanged && probe.goldSourceLockStateUnchanged,
                    "TOPCOVER_NATIVE_FRONT_BREP_EVIDENCE_SOURCE_DRIFT", 65,
                    "source state drifted before private front B-rep evidence write");
                probe.privateEvidenceWritten = true; WriteCreateNewAndReread(path, JsonBytes(probe));
                Dictionary<string, object> reread = ReadJsonObject(path);
                probe.privateEvidenceWritten = FileLinkCount(path) == 1 && !HasReparsePoint(path) &&
                    Bool(reread, "privateEvidenceWritten") && Bool(reread, "probeOnly") &&
                    !Bool(reread, "consumable");
                return probe.privateEvidenceWritten;
            }
            catch { return probe.privateEvidenceWritten = false; }
        }

        private static void WriteNativeTopCoverFrontBrepCaptureProbeCleanupEvidence(Result result)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverFrontBrepCaptureCleanupPrivateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return;
                string parent = Path.GetDirectoryName(path);
                string expected = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expected) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_FRONT_BREP_CLEANUP_SCOPE_INVALID", 65,
                    "front B-rep cleanup evidence escaped the current attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_FRONT_BREP_CLEANUP_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_FRONT_BREP_CLEANUP_CREATED_PATH_INVALID");
                bool goldAbsent = string.IsNullOrWhiteSpace(result.topcover_capture_pack) ||
                    !Directory.Exists(result.topcover_capture_pack);
                Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "schema", "winnsen.16029.topcover_native_front_brep_capture_cleanup.v1" },
                    { "probeOnly", true }, { "consumable", false },
                    { "processFinalGate", result.processes.final_gate },
                    { "protectedSourceUnchanged", result.source_unchanged },
                    { "targetRollbackSucceeded", result.rollback_succeeded },
                    { "goldCapturePackRollbackSucceeded", result.topcover_capture_pack_rollback_succeeded },
                    { "targetAbsent", !Directory.Exists(result.target_directory) },
                    { "goldCapturePackAbsent", goldAbsent },
                    { "cleanupPass", result.processes.final_gate && result.source_unchanged &&
                        !Directory.Exists(result.target_directory) && goldAbsent }
                };
                WriteCreateNewAndReread(path, JsonBytes(payload));
                Require(Bool(ReadJsonObject(path), "probeOnly") &&
                    !Bool(ReadJsonObject(path), "consumable"),
                    "TOPCOVER_NATIVE_FRONT_BREP_CLEANUP_REREAD_FAILED", 65,
                    "front B-rep cleanup reread did not preserve probe-only boundary");
            }
            catch { }
        }

        // First reconstruction phase: collect only the information needed to write five
        // new native parts later.  It deliberately never calls Save/Break/New/AddComponent
        // and never opens the hidden master as an output part.
        private static void RunNativeTopCoverRebuildCaptureProbeOnly(Result result)
        {
            NativeTopCoverRebuildCaptureEvidence probe = new NativeTopCoverRebuildCaptureEvidence
            {
                probeOnly = true, consumable = false, mode = "capture_topcover_rebuild_dataset",
                protectedSourceInventoryDigestBefore = result.source_inventory_digest,
                v37WorkingPack = result.target_directory,
                goldEngineeringDirectory = Path.Combine(result.repository_root,
                    GoldTopCoverEngineeringRelativeDirectory.Replace('/', Path.DirectorySeparatorChar)),
                goldCapturePack = Path.Combine(result.attempt_directory, "native_cad",
                    "gold_capture_pack")
            };
            try
            {
                Require(result.direct_copy_completed && TopCoverPhysicalPartNames().Count() == 5 &&
                    TopCoverPhysicalPartNames().All(name => ExactSourceBoundTargetFile(result, name)),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_V37_BINDING_INVALID", 67,
                    "capture requires the five exact V37 physical parts in the direct-copy pack");
                BindGoldTopCoverInputs(result, probe);
                CreateGoldTopCoverCapturePack(result, probe);
                foreach (string name in TopCoverPhysicalPartNames())
                {
                    probe.v37Parts.Add(CaptureNativeTopCoverRebuildPart(result,
                        Path.Combine(result.target_directory, name), result.target_directory,
                        "v37_exact75:" + name));
                    probe.goldParts.Add(CaptureNativeTopCoverRebuildPart(result,
                        Path.Combine(probe.goldCapturePack, name),
                        probe.goldCapturePack, "gold_1000w_copy:" + name));
                }
                probe.goldAssembly = CaptureNativeTopCoverRebuildAssembly(result,
                    Path.Combine(probe.goldCapturePack, TopCoverAssemblyFileName),
                    probe.goldCapturePack);
                probe.physicalIdentityTransforms = probe.goldAssembly.components.Where(component =>
                    TopCoverPhysicalPartNames().Contains(component.name,
                        StringComparer.OrdinalIgnoreCase)).Select(component =>
                    new NativeTopCoverPhysicalAssemblyMember
                    {
                        name = component.name, configuration = component.configuration,
                        transform16 = component.transform16.ToList(), fixedState = component.fixedState,
                        fixedCaptured = component.fixedCaptured,
                        suppression = component.suppression
                    }).OrderBy(component => component.name, StringComparer.OrdinalIgnoreCase).ToList();
                probe.masterExcludedFromNewAssembly = !probe.physicalIdentityTransforms.Any(component =>
                    string.Equals(component.name, "上盖 模型.sldprt", StringComparison.OrdinalIgnoreCase));
                probe.v37SourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.workingPackExact75Unchanged =
                    WorkingPackExact75WithPairedLocksUnchanged(result);
                probe.goldInputsUnchanged = GoldTopCoverInputsUnchanged(probe.goldInputs);
                probe.goldSourceLockFilesAfter = GoldTopCoverSourceLockFiles(
                    probe.goldEngineeringDirectory);
                probe.goldSourceLockStateUnchanged = probe.goldSourceLockFilesBefore.SequenceEqual(
                    probe.goldSourceLockFilesAfter, StringComparer.OrdinalIgnoreCase);
                probe.goldCapturePackComplete = GoldTopCoverCapturePackComplete(result, probe);
                probe.datasetComplete = NativeTopCoverRebuildCaptureComplete(probe);
                Require(probe.datasetComplete, "TOPCOVER_NATIVE_REBUILD_CAPTURE_INCOMPLETE", 67,
                    "capture dataset has unresolved sketches, bends, flat data, or assembly contract");
            }
            catch (StageException exception)
            {
                probe.failureStatus = exception.Status; probe.failureExitCode = exception.ExitCode;
                probe.failure = exception.Message; throw;
            }
            catch (Exception exception)
            {
                probe.failureStatus = "TOPCOVER_NATIVE_REBUILD_CAPTURE_EXCEPTION";
                probe.failureExitCode = 67; probe.failure = SafeException(exception);
                throw new StageException(probe.failureStatus, 67, probe.failure);
            }
            finally
            {
                probe.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                probe.v37SourceExact75Unchanged = SourceExact75Unchanged(result);
                probe.workingPackExact75Unchanged =
                    WorkingPackExact75WithPairedLocksUnchanged(result);
                probe.goldInputsUnchanged = GoldTopCoverInputsUnchanged(probe.goldInputs);
                probe.goldSourceLockFilesAfter = GoldTopCoverSourceLockFiles(
                    probe.goldEngineeringDirectory);
                probe.goldSourceLockStateUnchanged = probe.goldSourceLockFilesBefore.SequenceEqual(
                    probe.goldSourceLockFilesAfter, StringComparer.OrdinalIgnoreCase);
                probe.goldCapturePackComplete = GoldTopCoverCapturePackComplete(result, probe);
                probe.privateEvidenceWritten = WriteNativeTopCoverRebuildCapturePrivateEvidence(result, probe);
            }
            Require(probe.privateEvidenceWritten,
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_PRIVATE_EVIDENCE_WRITE_FAILED", 67,
                "capture probe must persist only its private nonconsumable evidence");
        }

        private static void BindGoldTopCoverInputs(Result result,
            NativeTopCoverRebuildCaptureEvidence probe)
        {
            string gold = probe.goldEngineeringDirectory;
            string dxfDirectory = Path.Combine(result.repository_root,
                "workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/2.钣金展开图".Replace('/',
                    Path.DirectorySeparatorChar));
            Require(Directory.Exists(gold) && Directory.Exists(dxfDirectory) &&
                IsUnder(gold, result.repository_root) && IsUnder(dxfDirectory, result.repository_root) &&
                !HasReparsePoint(gold) && !HasReparsePoint(dxfDirectory),
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_GOLD_ROOT_INVALID", 67,
                "gold engineering and current DXF directories must be local non-reparse repository paths");
            AssertPathChainNoReparse(gold, result.repository_root,
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_GOLD_PATH_INVALID");
            AssertPathChainNoReparse(dxfDirectory, result.repository_root,
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_DXF_PATH_INVALID");
            probe.goldSourceLockFilesBefore = GoldTopCoverSourceLockFiles(gold);
            List<string> names = new List<string>(TopCoverPhysicalPartNames());
            names.Add("标准寄存柜 模型.SLDPRT");
            names.Add("上盖 模型.sldprt");
            names.Add(TopCoverAssemblyFileName);
            names.Add("上盖焊接.SLDDRW");
            names.AddRange(TopCoverPhysicalPartNames().Select(name => Path.ChangeExtension(name,
                ".SLDDRW")));
            foreach (string name in names)
                probe.goldInputs.Add(CaptureNativeTopCoverInputBinding(Path.Combine(gold, name),
                    gold, "gold_engineering"));
            foreach (string partName in TopCoverPhysicalPartNames())
            {
                string dxfName = Path.GetFileNameWithoutExtension(partName) + "展开图.DXF";
                probe.goldInputs.Add(CaptureNativeTopCoverInputBinding(Path.Combine(dxfDirectory, dxfName),
                    dxfDirectory, "gold_current_dxf"));
            }
            Require(probe.goldInputs.Count == 19 && probe.goldInputs.All(GoldTopCoverInputBindingComplete) &&
                probe.goldInputs.Where(row => string.Equals(row.role, "gold_engineering",
                    StringComparison.Ordinal)).All(row => ExpectedGoldTopCoverSha256.ContainsKey(row.name) &&
                    string.Equals(ExpectedGoldTopCoverSha256[row.name], row.sha256Before,
                        StringComparison.OrdinalIgnoreCase)) && probe.goldInputs.Where(row =>
                    string.Equals(row.role, "gold_current_dxf", StringComparison.Ordinal)).All(row =>
                    ExpectedGoldTopCoverDxfSha256.ContainsKey(row.name) && string.Equals(
                    ExpectedGoldTopCoverDxfSha256[row.name], row.sha256Before,
                        StringComparison.OrdinalIgnoreCase)),
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_GOLD_BINDING_INVALID", 67,
                "all five parts, two source masters, assembly, six drawings, and five current DXFs need hash bindings");
        }

        private static List<string> GoldTopCoverSourceLockFiles(string directory)
        {
            try
            {
                return Directory.GetFiles(directory, "~$*", SearchOption.TopDirectoryOnly).Select(
                    Path.GetFileName).OrderBy(value => value,
                        StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { return new List<string> { "<inventory-failed>" }; }
        }

        private static void CreateGoldTopCoverCapturePack(Result result,
            NativeTopCoverRebuildCaptureEvidence probe)
        {
            string destination = probe.goldCapturePack;
            string expected = Path.Combine(result.attempt_directory, "native_cad",
                "gold_capture_pack");
            string parent = Path.GetDirectoryName(destination);
            Require(SamePath(destination, expected) && SamePath(parent,
                    Path.Combine(result.attempt_directory, "native_cad")) &&
                Directory.Exists(parent) && IsUnder(destination, result.attempt_directory) &&
                !Directory.Exists(destination),
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_COPY_SCOPE_INVALID", 67,
                "gold capture copy must use the new fixed attempt-local directory");
            AssertPathChainNoReparse(parent, result.attempt_directory,
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_COPY_PARENT_INVALID");
            Directory.CreateDirectory(destination);
            result.topcover_capture_pack = destination;
            result.topcover_capture_pack_created = true;
            AssertPathChainNoReparse(destination, result.attempt_directory,
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_COPY_DIRECTORY_INVALID");
            foreach (string name in GoldTopCoverCaptureCadFileNames)
            {
                string source = Path.Combine(probe.goldEngineeringDirectory, name);
                string target = Path.Combine(destination, name);
                Require(ExpectedGoldTopCoverSha256.ContainsKey(name) && File.Exists(source) &&
                    SamePath(Path.GetDirectoryName(source), probe.goldEngineeringDirectory) &&
                    FileLinkCount(source) == 1 && !HasReparsePoint(source) &&
                    string.Equals(Sha256File(source), ExpectedGoldTopCoverSha256[name],
                        StringComparison.OrdinalIgnoreCase),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_COPY_SOURCE_INVALID", 67,
                    "gold capture copy source drifted: " + name);
                File.Copy(source, target, false);
                Require(File.Exists(target) && FileLinkCount(target) == 1 &&
                    !HasReparsePoint(target) && SamePath(Path.GetDirectoryName(target), destination) &&
                    string.Equals(Sha256File(target), ExpectedGoldTopCoverSha256[name],
                        StringComparison.OrdinalIgnoreCase),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_COPY_INVALID", 67,
                    "gold capture copy did not preserve exact bytes: " + name);
            }
            Require(GoldTopCoverCapturePackComplete(result, probe),
                "TOPCOVER_NATIVE_REBUILD_CAPTURE_COPY_INCOMPLETE", 67,
                "attempt-local gold capture pack is not exact8");
        }

        private static bool GoldTopCoverCapturePackComplete(Result result,
            NativeTopCoverRebuildCaptureEvidence probe)
        {
            try
            {
                if (probe == null || string.IsNullOrWhiteSpace(probe.goldCapturePack) ||
                    !Directory.Exists(probe.goldCapturePack) || !SamePath(probe.goldCapturePack,
                        Path.Combine(result.attempt_directory, "native_cad", "gold_capture_pack")))
                    return false;
                if (TreeHasReparsePoint(probe.goldCapturePack) ||
                    Directory.GetDirectories(probe.goldCapturePack, "*",
                        SearchOption.TopDirectoryOnly).Length != 0) return false;
                string[] files = Directory.GetFiles(probe.goldCapturePack, "*",
                    SearchOption.TopDirectoryOnly);
                HashSet<string> expected = new HashSet<string>(GoldTopCoverCaptureCadFileNames,
                    StringComparer.OrdinalIgnoreCase);
                List<string> lockFiles = files.Where(path => Path.GetFileName(path).StartsWith(
                    "~$", StringComparison.Ordinal)).ToList();
                List<string> cadFiles = files.Except(lockFiles,
                    StringComparer.OrdinalIgnoreCase).ToList();
                return cadFiles.Count == expected.Count && new HashSet<string>(cadFiles.Select(
                    Path.GetFileName), StringComparer.OrdinalIgnoreCase).SetEquals(expected) &&
                    cadFiles.All(path => FileLinkCount(path) == 1 && !HasReparsePoint(path) &&
                        ExpectedGoldTopCoverSha256.ContainsKey(Path.GetFileName(path)) &&
                        string.Equals(Sha256File(path), ExpectedGoldTopCoverSha256[Path.GetFileName(path)],
                            StringComparison.OrdinalIgnoreCase)) && lockFiles.Count <= expected.Count &&
                    lockFiles.All(path =>
                    {
                        string name = Path.GetFileName(path);
                        return name.Length > 2 && expected.Contains(name.Substring(2)) &&
                            PairedSolidWorksLockFileSafe(path, probe.goldCapturePack,
                                ExpectedGoldTopCoverSha256);
                    });
            }
            catch { return false; }
        }

        private static NativeTopCoverInputBinding CaptureNativeTopCoverInputBinding(string path,
            string expectedDirectory, string role)
        {
            NativeTopCoverInputBinding row = new NativeTopCoverInputBinding
            {
                role = role, path = path, name = Path.GetFileName(path), expectedDirectory = expectedDirectory
            };
            try
            {
                row.exists = File.Exists(path) && SamePath(Path.GetDirectoryName(path), expectedDirectory);
                row.noReparse = row.exists && !HasReparsePoint(path);
                row.singleLink = row.exists && FileLinkCount(path) == 1;
                row.sizeBytesBefore = row.exists ? new FileInfo(path).Length : -1;
                row.sha256Before = row.exists ? Sha256File(path) : "";
            }
            catch (Exception exception) { row.fault = SafeException(exception); }
            return row;
        }

        private static bool GoldTopCoverInputBindingComplete(NativeTopCoverInputBinding row)
        {
            return row != null && row.exists && row.noReparse && row.singleLink &&
                row.sizeBytesBefore >= 0 && IsSha256(row.sha256Before) &&
                string.IsNullOrWhiteSpace(row.fault);
        }

        private static bool GoldTopCoverInputsUnchanged(IEnumerable<NativeTopCoverInputBinding> rows)
        {
            bool all = rows != null && rows.All(GoldTopCoverInputBindingComplete);
            if (rows == null) return false;
            foreach (NativeTopCoverInputBinding row in rows)
            {
                try
                {
                    row.sizeBytesAfter = File.Exists(row.path) ? new FileInfo(row.path).Length : -1;
                    row.sha256After = File.Exists(row.path) ? Sha256File(row.path) : "";
                    row.unchanged = row.sizeBytesBefore == row.sizeBytesAfter &&
                        string.Equals(row.sha256Before, row.sha256After,
                            StringComparison.OrdinalIgnoreCase) && FileLinkCount(row.path) == 1 &&
                        !HasReparsePoint(row.path);
                    all = all && row.unchanged;
                }
                catch { row.unchanged = false; all = false; }
            }
            return all;
        }

        private static NativeTopCoverRebuildPartCapture CaptureNativeTopCoverRebuildPart(Result result,
            string partPath, string expectedDirectory, string purpose)
        {
            NativeTopCoverRebuildPartCapture output = new NativeTopCoverRebuildPartCapture
            {
                path = partPath, name = Path.GetFileName(partPath), purpose = purpose,
                sourceBinding = CaptureNativeTopCoverInputBinding(partPath, expectedDirectory, purpose)
            };
            ISldWorks application = null;
            ModelDoc2 model = null;
            MathUtility math = null;
            SessionEvidence session = new SessionEvidence { purpose = purpose };
            result.sessions.Add(session);
            try
            {
                Require(GoldTopCoverInputBindingComplete(output.sourceBinding) &&
                    WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_PART_PRECONDITION_FAILED", 67,
                    "part capture requires a source-bound file and stable empty CAD process set");
                application = StartOwnedSolidWorks(result, session);
                Require(application.SetCurrentWorkingDirectory(expectedDirectory),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_WORKING_DIRECTORY_FAILED", 67,
                    "owned SolidWorks session could not bind its attempt-local search directory");
                output.preopenDependencies = CaptureDependencies(application, partPath,
                    expectedDirectory);
                output.knownGoldBottomHistoricalDependencyAccepted =
                    KnownGoldBottomDependencyOnly(output, expectedDirectory);
                Require(output.preopenDependencies.closure_count > 0 &&
                    (output.preopenDependencies.all_target_local ||
                        output.knownGoldBottomHistoricalDependencyAccepted),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_DEPENDENCY_ESCAPE", 67,
                    "part dependencies would resolve outside the isolated capture directory");
                math = application.GetMathUtility() as MathUtility;
                Require(math != null, "TOPCOVER_NATIVE_REBUILD_CAPTURE_MATH_UNAVAILABLE", 67,
                    "read-only sketch capture requires SolidWorks MathUtility");
                int documentsBefore;
                output.cacheBeforeEmpty = DocumentCacheEmpty(application,
                    out documentsBefore);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(partPath, (int)swDocumentTypes_e.swDocPART,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                output.opened = model != null; output.openErrors = errors; output.openWarnings = warnings;
                output.readOnly = model != null && Safe(() => model.IsOpenedReadOnly(), false);
                Require(output.cacheBeforeEmpty && output.opened && errors == 0 && warnings == 0 &&
                    output.readOnly, "TOPCOVER_NATIVE_REBUILD_CAPTURE_PART_OPEN_FAILED", 67,
                    "each V37/gold part requires its own exact read-only 0/0 session");
                output.measurement.cacheBeforeEmpty = output.cacheBeforeEmpty;
                output.measurement.opened = output.opened;
                output.measurement.openErrors = output.openErrors;
                output.measurement.openWarnings = output.openWarnings;
                output.measurement.readOnly = output.readOnly;
                try
                {
                    output.measurement.saveFlagBeforeRebuild = model.GetSaveFlag();
                    output.measurement.saveFlagBeforeCaptured = true;
                }
                catch { output.measurement.saveFlagBeforeCaptured = false; }
                output.rebuilt = Safe(() => model.ForceRebuild3(false), false);
                try
                {
                    output.measurement.saveFlagAfterRebuild = model.GetSaveFlag();
                    output.measurement.saveFlagAfterCaptured = true;
                }
                catch { output.measurement.saveFlagAfterCaptured = false; }
                CaptureNativeTopCoverPartGeometry(model, output.measurement);
                CaptureNativeTopCoverPartFeatures(model, output.measurement, expectedDirectory);
                output.measurement.name = output.name; output.measurement.path = partPath;
                output.measurement.pathLocal = SamePath(Path.GetDirectoryName(partPath), expectedDirectory);
                output.measurement.sourceSize = output.sourceBinding.sizeBytesBefore;
                output.measurement.sourceSha256 = output.sourceBinding.sha256Before;
                output.measurement.configuration = Safe(() =>
                {
                    Configuration configuration = model.GetActiveConfiguration() as Configuration;
                    try { return configuration == null ? "" : configuration.Name; }
                    finally { Release(configuration); }
                }, "");
                output.measurement.bodyCount = output.measurement.bodies.Count;
                output.measurement.rebuilt = output.rebuilt;
                output.measurement.modelExternalReferenceCount = Safe(() =>
                    model.ListExternalFileReferencesCount2(), -1);
                ModelDocExtension extension = Safe(() => model.Extension, null);
                try { output.measurement.extensionExternalReferenceCount = extension == null ? -1 :
                    Safe(() => extension.ListExternalFileReferencesCount(), -1); }
                finally { Release(extension); }
                output.measurement.geometrySignatureSha256 = NativeTopCoverPartGeometrySignature(
                    output.measurement);
                output.measurement.physicalGeometrySignatureSha256 = PhysicalGeometrySignature(
                    output.measurement);
                CaptureNativeTopCoverRebuildFeatures(model, math, output);
            }
            catch (StageException exception) { output.unresolved.Add(exception.Status); output.fault = exception.Message; }
            catch (Exception exception) { output.unresolved.Add("exception"); output.fault = SafeException(exception); }
            finally
            {
                Release(math);
                CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int documentsAfter; output.cacheAfterEmpty = application != null &&
                    DocumentCacheEmpty(application, out documentsAfter);
                CloseOwnedSolidWorks(ref application, result, session);
                output.sessionExitProven = session.exit_state == "exited" && session.monitors_exit_proven;
                output.sourceBinding.sizeBytesAfter = File.Exists(partPath) ? new FileInfo(partPath).Length : -1;
                output.sourceBinding.sha256After = File.Exists(partPath) ? Sha256File(partPath) : "";
                output.sourceBinding.unchanged = output.sourceBinding.sizeBytesBefore ==
                    output.sourceBinding.sizeBytesAfter && string.Equals(output.sourceBinding.sha256Before,
                    output.sourceBinding.sha256After, StringComparison.OrdinalIgnoreCase) &&
                    FileLinkCount(partPath) == 1 && !HasReparsePoint(partPath);
                output.measurement.cacheAfterEmpty = output.cacheAfterEmpty;
                output.measurement.fileUnchanged = output.sourceBinding.unchanged;
                output.complete = NativeTopCoverRebuildPartCaptureComplete(output);
            }
            return output;
        }

        private static void CaptureNativeTopCoverRebuildFeatures(ModelDoc2 model,
            MathUtility math, NativeTopCoverRebuildPartCapture output)
        {
            Feature feature = Safe(() => model.FirstFeature() as Feature, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureNativeTopCoverRebuildFeatureRecursive(model, math, feature, "", 0, output);
                Feature next = Safe(() => feature.GetNextFeature() as Feature, null);
                Release(feature); feature = next;
            }
            if (feature != null || guard >= 5000) output.unresolved.Add("feature_tree_guard");
        }

        private static void CaptureNativeTopCoverRebuildFeatureRecursive(ModelDoc2 model,
            MathUtility math, Feature feature, string parentPath, int depth,
            NativeTopCoverRebuildPartCapture output)
        {
            if (feature == null || depth > 30) { output.unresolved.Add("feature_tree_depth"); return; }
            string name = Safe(() => feature.Name, "");
            string type = Safe(() => feature.GetTypeName2(), "");
            string path = string.IsNullOrWhiteSpace(parentPath) ? name : parentPath + "/" + name;
            object specific = null; object definition = null;
            try
            {
                specific = Safe(() => feature.GetSpecificFeature2(), null);
                Sketch sketch = specific as Sketch;
                if (string.Equals(type, "ProfileFeature", StringComparison.OrdinalIgnoreCase))
                    output.sketches.Add(CaptureNativeTopCoverRebuildSketch(math, sketch, name, path,
                        type, output.unresolved));
                if (string.Equals(type, "OneBend", StringComparison.OrdinalIgnoreCase))
                {
                    definition = Safe(() => feature.GetDefinition(), null);
                    output.oneBends.Add(CaptureNativeTopCoverOneBend(model, math, definition, name,
                        path, output.unresolved));
                    Release(definition); definition = null;
                }
                else if (string.Equals(type, "ProcessBends", StringComparison.OrdinalIgnoreCase))
                {
                    definition = Safe(() => feature.GetDefinition(), null);
                    output.processBends.Add(CaptureNativeTopCoverProcessBends(model, definition, name,
                        path, output.unresolved));
                    Release(definition); definition = null;
                }
                else if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase))
                {
                    definition = Safe(() => feature.GetDefinition(), null);
                    output.flatPatterns.Add(CaptureNativeTopCoverFlatPattern(model, definition, name,
                        path, output.unresolved));
                    Release(definition); definition = null;
                }
            }
            catch (Exception exception) { output.unresolved.Add("feature:" + path + ":" +
                SafeException(exception)); }
            finally { Release(definition); Release(specific); }
            Feature child = Safe(() => feature.GetFirstSubFeature() as Feature, null);
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureNativeTopCoverRebuildFeatureRecursive(model, math, child, path, depth + 1,
                    output);
                Feature next = Safe(() => child.GetNextSubFeature() as Feature, null);
                Release(child); child = next;
            }
            if (child != null || guard >= 3000) output.unresolved.Add("subfeature_guard:" + path);
        }

        private static NativeTopCoverSketchCapture CaptureNativeTopCoverRebuildSketch(
            MathUtility math, Sketch sketch, string name, string path, string type,
            List<string> unresolved)
        {
            NativeTopCoverSketchCapture row = new NativeTopCoverSketchCapture
            {
                name = name,
                path = path,
                featureType = type,
                found = sketch != null
            };
            if (sketch == null)
            {
                row.unresolved.Add("specific_not_sketch");
                unresolved.Add("sketch:" + path);
                return row;
            }
            MathTransform modelToSketch = null;
            MathTransform sketchToModel = null;
            try
            {
                row.is3D = Safe(() => sketch.Is3D(), false);
                row.isDerived = Safe(() => sketch.IsDerived(), false);
                modelToSketch = Safe(() => sketch.ModelToSketchTransform, null);
                sketchToModel = modelToSketch == null ? null :
                    Safe(() => modelToSketch.Inverse() as MathTransform, null);
                row.modelToSketchTransform16 = TransformValues(modelToSketch).ToList();
                row.sketchToModelTransform16 = TransformValues(sketchToModel).ToList();
                if (!ValidTransform16(row.modelToSketchTransform16) ||
                    !ValidTransform16(row.sketchToModelTransform16))
                    row.unresolved.Add("transform16_invalid");

                Array segments = Safe(() => sketch.GetSketchSegments() as Array, null);
                foreach (object item in segments ?? new object[0])
                {
                    SketchSegment segment = item as SketchSegment;
                    if (segment == null)
                    {
                        row.unresolved.Add("non_segment");
                        continue;
                    }
                    row.segments.Add(CaptureNativeTopCoverSketchSegment(segment, math,
                        sketchToModel));
                    Release(segment);
                }
                row.segmentCount = row.segments.Count;

                Array points = Safe(() => sketch.GetSketchPoints2() as Array, null);
                foreach (object item in points ?? new object[0])
                {
                    SketchPoint point = item as SketchPoint;
                    if (point == null)
                    {
                        row.unresolved.Add("non_point");
                        continue;
                    }
                    row.points.Add(CaptureNativeTopCoverSketchPoint(point, math, sketchToModel));
                    Release(point);
                }

                SketchRelationManager manager = Safe(() => sketch.RelationManager, null);
                try { row.relationCount = manager == null ? -1 : manager.GetRelationsCount(0); }
                finally { Release(manager); }
                CaptureNativeTopCoverSketchBlocks(sketch, math, sketchToModel, row);
                row.emptyInternal = row.segments.Count == 0 && row.blocks.Count == 0 &&
                    row.points.Count == 0 && row.relationCount == 0;
                if (!row.segments.All(NativeTopCoverSketchSegmentComplete) ||
                    !row.points.All(NativeTopCoverSketchPointComplete) ||
                    !row.blocks.All(NativeTopCoverSketchBlockComplete))
                    row.unresolved.Add("incomplete_geometry");
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            finally
            {
                Release(sketchToModel);
                Release(modelToSketch);
            }
            row.complete = row.found && ValidTransform16(row.modelToSketchTransform16) &&
                ValidTransform16(row.sketchToModelTransform16) && row.relationCount >= 0 &&
                (row.segments.Count > 0 || row.blocks.Count > 0 || row.emptyInternal) &&
                row.unresolved.Count == 0;
            if (!row.complete) unresolved.Add("sketch:" + path);
            return row;
        }

        private static NativeTopCoverSketchPointCapture CaptureNativeTopCoverSketchPoint(
            SketchPoint point, MathUtility math, MathTransform sketchToModel)
        {
            List<double> local = new List<double>
            {
                Safe(() => point.X, double.NaN),
                Safe(() => point.Y, double.NaN),
                Safe(() => point.Z, double.NaN)
            };
            return CaptureNativeTopCoverCoordinate(local, math, sketchToModel);
        }

        private static NativeTopCoverSketchPointCapture CaptureNativeTopCoverCoordinate(
            IList<double> localValues, MathUtility math, MathTransform sketchToModel)
        {
            NativeTopCoverSketchPointCapture row = new NativeTopCoverSketchPointCapture
                { localSi = localValues == null ? new List<double>() : localValues.ToList() };
            MathPoint localPoint = null;
            MathPoint modelPoint = null;
            try
            {
                if (math == null || sketchToModel == null || row.localSi.Count != 3 ||
                    !MeasurementValuesFinite(row.localSi)) return row;
                localPoint = math.CreatePoint(row.localSi.ToArray()) as MathPoint;
                modelPoint = localPoint == null ? null :
                    localPoint.MultiplyTransform(sketchToModel) as MathPoint;
                row.modelSi = ComDoubleList(modelPoint == null ? null : modelPoint.ArrayData);
            }
            finally
            {
                if (!ReferenceEquals(localPoint, modelPoint)) Release(modelPoint);
                Release(localPoint);
            }
            return row;
        }

        private static NativeTopCoverSketchSegmentCapture CaptureNativeTopCoverSketchSegment(
            SketchSegment segment, MathUtility math, MathTransform sketchToModel)
        {
            NativeTopCoverSketchSegmentCapture row = new NativeTopCoverSketchSegmentCapture
            {
                runtimeType = ((object)segment).GetType().FullName ?? "",
                type = Safe(() => segment.GetType(), -1),
                ids = ComIntList(Safe(() => segment.GetID(), null)),
                name = Safe(() => segment.GetName(), ""),
                construction = Safe(() => segment.ConstructionGeometry, false),
                status = Safe(() => segment.Status, -1),
                lengthSi = Safe(() => segment.GetLength(), double.NaN),
                constraintCount = ComArray(Safe(() => segment.GetConstraints(), null)).Length,
                relationCount = Safe(() => segment.GetRelationsCount(), -1),
                isBendLine = Safe(() => segment.IsBendLine(), false)
            };
            SketchLine line = segment as SketchLine;
            SketchArc arc = segment as SketchArc;
            if (line != null)
            {
                row.kind = "line";
                SketchPoint first = Safe(() => line.GetStartPoint2() as SketchPoint, null);
                SketchPoint last = Safe(() => line.GetEndPoint2() as SketchPoint, null);
                try
                {
                    row.start = CaptureNativeTopCoverSketchPoint(first, math, sketchToModel);
                    row.end = CaptureNativeTopCoverSketchPoint(last, math, sketchToModel);
                }
                finally { Release(first); Release(last); }
            }
            else if (arc != null)
            {
                row.kind = "arc";
                SketchPoint center = Safe(() => arc.GetCenterPoint2() as SketchPoint, null);
                SketchPoint first = Safe(() => arc.GetStartPoint2() as SketchPoint, null);
                SketchPoint last = Safe(() => arc.GetEndPoint2() as SketchPoint, null);
                try
                {
                    row.center = CaptureNativeTopCoverSketchPoint(center, math, sketchToModel);
                    row.start = CaptureNativeTopCoverSketchPoint(first, math, sketchToModel);
                    row.end = CaptureNativeTopCoverSketchPoint(last, math, sketchToModel);
                }
                finally { Release(center); Release(first); Release(last); }
                row.circle = Safe(() => arc.IsCircle(), 0) != 0;
                row.radiusSi = Safe(() => arc.GetRadius(), double.NaN);
                row.rotationDirection = Safe(() => arc.GetRotationDir(), -1);
                row.normalSi = ComDoubleList(Safe(() => arc.GetNormalVector(), null));
            }
            else
            {
                row.kind = "unsupported";
                row.fault = "unsupported sketch segment runtime type";
            }
            return row;
        }

        private static void CaptureNativeTopCoverSketchBlocks(Sketch sketch, MathUtility math,
            MathTransform sketchToModel, NativeTopCoverSketchCapture row)
        {
            Array raw = Safe(() => sketch.GetSketchBlockInstances() as Array, null);
            foreach (object item in raw ?? new object[0])
            {
                SketchBlockInstance instance = item as SketchBlockInstance;
                if (instance == null)
                {
                    row.unresolved.Add("non_block_instance");
                    continue;
                }
                MathPoint instancePoint = null;
                MathPoint definitionPoint = null;
                MathTransform blockToSketch = null;
                SketchBlockDefinition definition = null;
                try
                {
                    instancePoint = Safe(() => instance.InstancePosition, null);
                    definition = Safe(() => instance.Definition, null);
                    definitionPoint = definition == null ? null :
                        Safe(() => definition.InsertionPoint, null);
                    blockToSketch = Safe(() => instance.BlockToSketchTransform, null);
                    List<double> instanceLocal = ComDoubleList(instancePoint == null ? null :
                        instancePoint.ArrayData);
                    List<double> definitionLocal = ComDoubleList(definitionPoint == null ? null :
                        definitionPoint.ArrayData);
                    row.blocks.Add(new NativeTopCoverSketchBlockCapture
                    {
                        name = Safe(() => instance.Name, ""),
                        angle = Safe(() => instance.Angle, double.NaN),
                        scale = Safe(() => instance.Scale, double.NaN),
                        insertion = CaptureNativeTopCoverCoordinate(instanceLocal, math,
                            sketchToModel),
                        definitionInsertionLocalSi = definitionLocal,
                        blockToSketchTransform16 = TransformValues(blockToSketch).ToList(),
                        definitionFileName = definition == null ? "" :
                            Safe(() => definition.FileName, ""),
                        linkToFile = definition != null && Safe(() => definition.LinkToFile, false)
                    });
                }
                finally
                {
                    Release(blockToSketch);
                    Release(definitionPoint);
                    Release(instancePoint);
                    Release(definition);
                    Release(instance);
                }
            }
        }

        private static NativeTopCoverBendCapture CaptureNativeTopCoverOneBend(ModelDoc2 model,
            MathUtility math, object definition, string name, string path,
            List<string> unresolved)
        {
            NativeTopCoverBendCapture row = new NativeTopCoverBendCapture
                { name = name, path = path, type = "OneBend" };
            IOneBendFeatureData data = definition as IOneBendFeatureData;
            if (data == null)
            {
                row.unresolved.Add("IOneBendFeatureData_unavailable");
                unresolved.Add("onebend:" + path);
                return row;
            }
            try
            {
                row.accessSelections = Safe(() => data.IAccessSelections2(model, null), false);
                row.bendType = Safe(() => data.GetType(), -1);
                row.angle = Safe(() => data.BendAngle, double.NaN);
                row.direction = Safe(() => data.BendDirection, -1);
                row.down = Safe(() => data.BendDown, false);
                row.order = Safe(() => data.BendOrder, -1);
                row.radius = Safe(() => data.BendRadius, double.NaN);
                row.kFactor = Safe(() => data.KFactor, double.NaN);
                row.allowance = Safe(() => data.BendAllowance, double.NaN);
                row.allowanceType = Safe(() => data.BendAllowanceType, -1);
                row.useDefaultRadius = Safe(() => data.UseDefaultBendRadius, false);
                row.useDefaultAllowance = Safe(() => data.UseDefaultBendAllowance, false);
                row.useDefaultRelief = Safe(() => data.UseDefaultBendRelief, false);
                row.useAutoRelief = Safe(() => data.UseAutoRelief, false);
                row.autoReliefType = Safe(() => data.AutoReliefType, -1);
                row.reliefDepth = Safe(() => data.ReliefDepth, double.NaN);
                row.reliefWidth = Safe(() => data.ReliefWidth, double.NaN);
                row.reliefRatio = Safe(() => data.ReliefRatio, double.NaN);
                CaptureNativeTopCoverCustomBendAllowance(
                    Safe(() => data.GetCustomBendAllowance() as CustomBendAllowance, null), row);
                row.flatPatternSegmentApiCount = Safe(() =>
                    data.GetFlatPatternSketchSegmentCount2(), -1);
                row.flatPatternSegments = CaptureComSketchSegments(
                    Safe(() => data.FlatPatternSketchSegments2, null), math);
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            finally { if (row.accessSelections) Try(() => data.ReleaseSelectionAccess()); }
            bool allowanceComplete = row.useDefaultAllowance ||
                MeasurementValuesFinite(new[] { row.allowance }) ||
                (row.customAllowanceAvailable && row.customAllowanceType >= 0);
            row.supplementRequired = !row.accessSelections ||
                row.flatPatternSegmentApiCount == 0;
            bool flatSegmentsComplete = row.flatPatternSegmentApiCount == 0 ||
                (row.flatPatternSegmentApiCount > 0 && row.flatPatternSegments.Count ==
                    row.flatPatternSegmentApiCount && row.flatPatternSegments.All(
                        NativeTopCoverSketchSegmentComplete));
            row.complete = row.bendType >= 0 &&
                row.direction > 0 && row.order > 0 && allowanceComplete &&
                MeasurementValuesFinite(new[] { row.angle, row.radius, row.kFactor }) &&
                row.radius > 0 && flatSegmentsComplete && row.unresolved.Count == 0;
            if (!row.complete) unresolved.Add("onebend:" + path);
            return row;
        }

        private static NativeTopCoverBendCapture CaptureNativeTopCoverProcessBends(ModelDoc2 model,
            object definition, string name, string path, List<string> unresolved)
        {
            NativeTopCoverBendCapture row = new NativeTopCoverBendCapture
                { name = name, path = path, type = "ProcessBends" };
            IBendsFeatureData data = definition as IBendsFeatureData;
            if (data == null)
            {
                row.unresolved.Add("IBendsFeatureData_unavailable");
                unresolved.Add("process:" + path);
                return row;
            }
            try
            {
                row.accessSelections = Safe(() => data.IAccessSelections2(model, null), false);
                row.radius = Safe(() => data.BendRadius, double.NaN);
                row.kFactor = Safe(() => data.KFactor, double.NaN);
                row.allowance = Safe(() => data.BendAllowance, double.NaN);
                row.allowanceType = Safe(() => data.BendAllowanceType, -1);
                row.useDefaultRadius = Safe(() => data.UseDefaultBendRadius, false);
                row.useDefaultAllowance = Safe(() => data.UseDefaultBendAllowance, false);
                CaptureNativeTopCoverCustomBendAllowance(
                    Safe(() => data.GetCustomBendAllowance() as CustomBendAllowance, null), row);
                row.fixedFace = CaptureNativeTopCoverFace(
                    Safe(() => data.GetFixedFace() as Face2, null));
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            finally { if (row.accessSelections) Try(() => data.ReleaseSelectionAccess()); }
            bool allowanceComplete = row.useDefaultAllowance ||
                MeasurementValuesFinite(new[] { row.allowance }) ||
                (row.customAllowanceAvailable && row.customAllowanceType >= 0);
            row.complete = row.accessSelections && allowanceComplete &&
                MeasurementValuesFinite(new[] { row.radius, row.kFactor }) && row.radius > 0 &&
                NativeTopCoverFaceComplete(row.fixedFace) && row.unresolved.Count == 0;
            if (!row.complete) unresolved.Add("process:" + path);
            return row;
        }

        private static NativeTopCoverFlatPatternCapture CaptureNativeTopCoverFlatPattern(ModelDoc2 model,
            object definition, string name, string path, List<string> unresolved)
        {
            NativeTopCoverFlatPatternCapture row = new NativeTopCoverFlatPatternCapture { name = name, path = path };
            IFlatPatternFeatureData data = definition as IFlatPatternFeatureData;
            if (data == null)
            {
                row.definitionReadable = false;
                row.supplementRequired = true;
                row.complete = true;
                return row;
            }
            try
            {
                row.accessSelections = Safe(() => data.IAccessSelections2(model, null), false);
                row.definitionReadable = row.accessSelections;
                if (!row.definitionReadable)
                {
                    row.supplementRequired = true;
                    row.complete = true;
                    return row;
                }
                row.fixedFace = CaptureNativeTopCoverFace(
                    Safe(() => data.FixedFace2 as Face2, null));
                Array excluded = Safe(() => data.ExcludedFaces as Array, null);
                try { row.excludedFaceCount = excluded == null ? 0 : excluded.Length; }
                finally { ReleaseComArrayItems(excluded); }
                row.mergeFace = Safe(() => data.MergeFace, false);
                row.simplifyBends = Safe(() => data.SimplifyBends, false);
                row.cornerTreatment = Safe(() => data.CornerTreatment, false);
                row.breakCornerRadius = Safe(() => data.BreakCornerRadius, double.NaN);
                row.breakCornerType = Safe(() => data.BreakCornerType, -1);
                row.cornerTrimReliefDistance = Safe(() =>
                    data.CornerTrimReliefDistance, double.NaN);
                row.cornerTrimReliefType = Safe(() => data.CornerTrimReliefType, -1);
            }
            catch (Exception exception) { row.unresolved.Add(SafeException(exception)); }
            finally { if (row.accessSelections) Try(() => data.ReleaseSelectionAccess()); }
            // A FlatPattern definition may be unreadable in SW2020; child sketches/OneBend flat
            // segments plus separately-bound DXF are the permitted supplemental evidence.
            row.complete = row.definitionReadable && NativeTopCoverFaceComplete(row.fixedFace) &&
                row.unresolved.Count == 0;
            if (!row.complete) unresolved.Add("flatpattern:" + path);
            return row;
        }

        private static void CaptureNativeTopCoverCustomBendAllowance(CustomBendAllowance value,
            NativeTopCoverBendCapture row)
        {
            try
            {
                row.customAllowanceAvailable = value != null;
                if (value == null) return;
                row.customAllowanceType = Safe(() => value.Type, -1);
                row.customKFactor = Safe(() => value.KFactor, double.NaN);
                row.customAllowance = Safe(() => value.BendAllowance, double.NaN);
                row.customDeduction = Safe(() => value.BendDeduction, double.NaN);
                row.customTableFile = Safe(() => value.BendTableFile, "");
            }
            finally { Release(value); }
        }
        private static NativeTopCoverFaceCapture CaptureNativeTopCoverFace(Face2 face)
        {
            NativeTopCoverFaceCapture row = new NativeTopCoverFaceCapture { found = face != null };
            try
            {
                row.boxSi = ComDoubleList(Safe(() => face.GetBox(), null));
                row.normalSi = ComDoubleList(Safe(() => face.Normal, null));
            }
            finally { Release(face); }
            return row;
        }
        private static List<NativeTopCoverSketchSegmentCapture> CaptureComSketchSegments(object raw,
            MathUtility math)
        {
            List<NativeTopCoverSketchSegmentCapture> rows = new List<NativeTopCoverSketchSegmentCapture>();
            foreach (object item in ComArray(raw))
            {
                SketchSegment segment = item as SketchSegment;
                if (segment == null) continue;
                Sketch sketch = null;
                MathTransform modelToSketch = null;
                MathTransform sketchToModel = null;
                try
                {
                    sketch = Safe(() => segment.GetSketch() as Sketch, null);
                    modelToSketch = sketch == null ? null :
                        Safe(() => sketch.ModelToSketchTransform, null);
                    sketchToModel = modelToSketch == null ? null :
                        Safe(() => modelToSketch.Inverse() as MathTransform, null);
                    rows.Add(CaptureNativeTopCoverSketchSegment(segment, math, sketchToModel));
                }
                finally
                {
                    Release(sketchToModel);
                    Release(modelToSketch);
                    Release(sketch);
                    Release(segment);
                }
            }
            return rows;
        }
        private static NativeTopCoverRebuildAssemblyCapture CaptureNativeTopCoverRebuildAssembly(Result result,
            string assemblyPath, string expectedDirectory)
        {
            NativeTopCoverRebuildAssemblyCapture output = new NativeTopCoverRebuildAssemblyCapture
                { binding = CaptureNativeTopCoverInputBinding(assemblyPath, expectedDirectory, "gold_assembly") };
            ISldWorks application = null;
            ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence
                { purpose = "gold_topcover_assembly_readonly" };
            result.sessions.Add(session);
            try
            {
                Require(GoldTopCoverInputBindingComplete(output.binding) &&
                    WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_ASSEMBLY_PRECONDITION_FAILED", 67,
                    "gold assembly input or CAD process state invalid");
                application = StartOwnedSolidWorks(result, session);
                Require(application.SetCurrentWorkingDirectory(expectedDirectory),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_ASSEMBLY_DIRECTORY_FAILED", 67,
                    "gold assembly session could not bind its attempt-local search directory");
                output.preopenDependencies = CaptureDependencies(application, assemblyPath,
                    expectedDirectory);
                Require(output.preopenDependencies.all_target_local &&
                    output.preopenDependencies.exact_inventory_set &&
                    output.preopenDependencies.closure_count ==
                        GoldTopCoverCaptureCadFileNames.Length,
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_ASSEMBLY_DEPENDENCY_ESCAPE", 67,
                    "gold assembly dependency closure is not the isolated exact8 pack");
                int before;
                output.cacheBeforeEmpty = DocumentCacheEmpty(application, out before);
                int errors = 0;
                int warnings = 0;
                model = application.OpenDoc6(assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                output.opened = model != null;
                output.openErrors = errors;
                output.openWarnings = warnings;
                output.readOnly = model != null && Safe(() => model.IsOpenedReadOnly(), false);
                output.rebuilt = model != null && Safe(() => model.ForceRebuild3(false), false);

                Configuration configuration = model == null ? null :
                    Safe(() => model.GetActiveConfiguration() as Configuration, null);
                Component2 root = configuration == null ? null :
                    Safe(() => configuration.GetRootComponent3(true) as Component2, null);
                try
                {
                    foreach (object item in root == null ? new object[0] :
                        ObjectArray(Safe(() => root.GetChildren(), null)))
                    {
                        Component2 component = item as Component2;
                        if (component == null)
                        {
                            output.unresolved.Add("non_component_child");
                            continue;
                        }
                        MathTransform transform = null;
                        try
                        {
                            string componentPath = Safe(() => component.GetPathName(), "");
                            NativeTopCoverTemplateComponentState state =
                                new NativeTopCoverTemplateComponentState
                            {
                                name = Path.GetFileName(componentPath),
                                path = componentPath,
                                sha256 = File.Exists(componentPath) ? Sha256File(componentPath) : "",
                                configuration = Safe(() => component.ReferencedConfiguration, ""),
                                suppression = Safe(() => component.GetSuppression(), -1),
                                directChildCount = ObjectArray(Safe(() =>
                                    component.GetChildren(), null)).Length
                            };
                            try { state.hidden = component.IsHidden(true); state.hiddenCaptured = true; }
                            catch { state.hiddenCaptured = false; }
                            try { state.visible = component.Visible; state.visibleCaptured = true; }
                            catch { state.visibleCaptured = false; }
                            try { state.envelope = component.IsEnvelope(); state.envelopeCaptured = true; }
                            catch { state.envelopeCaptured = false; }
                            try
                            {
                                state.excludeFromBom = component.ExcludeFromBOM;
                                state.excludeFromBomCaptured = true;
                            }
                            catch { state.excludeFromBomCaptured = false; }
                            try
                            {
                                state.suppressed = component.IsSuppressed();
                                state.suppressedCaptured = true;
                            }
                            catch { state.suppressedCaptured = false; }
                            try
                            {
                                state.fixedState = component.IsFixed();
                                state.fixedCaptured = true;
                            }
                            catch { state.fixedCaptured = false; }
                            transform = Safe(() => component.Transform2, null);
                            state.transform16 = TransformValues(transform).ToList();
                            output.components.Add(state);
                        }
                        finally
                        {
                            Release(transform);
                            Release(component);
                        }
                    }
                }
                finally
                {
                    Release(root);
                    Release(configuration);
                }
            }
            catch (Exception exception) { output.unresolved.Add(SafeException(exception)); }
            finally
            {
                CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int after;
                output.cacheAfterEmpty = application != null &&
                    DocumentCacheEmpty(application, out after);
                CloseOwnedSolidWorks(ref application, result, session);
                output.sessionExitProven = session.exit_state == "exited" &&
                    session.monitors_exit_proven;
                output.binding.sizeBytesAfter = File.Exists(assemblyPath) ?
                    new FileInfo(assemblyPath).Length : -1;
                output.binding.sha256After = File.Exists(assemblyPath) ?
                    Sha256File(assemblyPath) : "";
                output.binding.unchanged = output.binding.sizeBytesAfter ==
                    output.binding.sizeBytesBefore && string.Equals(output.binding.sha256After,
                    output.binding.sha256Before, StringComparison.OrdinalIgnoreCase) &&
                    FileLinkCount(assemblyPath) == 1 && !HasReparsePoint(assemblyPath);
                output.master = output.components.FirstOrDefault(value => string.Equals(
                    value.name, "上盖 模型.sldprt", StringComparison.OrdinalIgnoreCase));
                output.complete = NativeTopCoverRebuildAssemblyCaptureComplete(output,
                    expectedDirectory);
            }
            return output;
        }
        private static int ExpectedTopCoverPhysicalBendCount(string name)
        {
            if (string.Equals(name, "上盖壳体左侧板.sldprt",
                    StringComparison.OrdinalIgnoreCase)) return 5;
            if (string.Equals(name, "上盖壳体右侧板.SLDPRT",
                    StringComparison.OrdinalIgnoreCase)) return 5;
            if (string.Equals(name, "上盖壳体前侧板.sldprt",
                    StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(name, "上盖壳体后侧板.sldprt",
                    StringComparison.OrdinalIgnoreCase)) return 5;
            if (string.Equals(name, "上盖壳体底板.sldprt",
                    StringComparison.OrdinalIgnoreCase)) return 7;
            return -1;
        }

        private static bool KnownGoldBottomDependencyOnly(
            NativeTopCoverRebuildPartCapture row, string expectedDirectory)
        {
            if (row == null || !row.purpose.StartsWith("gold_1000w_copy:",
                    StringComparison.Ordinal) || !string.Equals(row.name,
                    "上盖壳体底板.sldprt", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(KnownGoldBottomHistoricalAssemblyReference)) return false;
            List<string> external = row.preopenDependencies.paths.Where(path =>
                !IsUnder(path, expectedDirectory)).ToList();
            return external.Count == 1 && string.Equals(Path.GetFullPath(external[0]),
                Path.GetFullPath(KnownGoldBottomHistoricalAssemblyReference),
                StringComparison.OrdinalIgnoreCase) && row.preopenDependencies.paths.Where(path =>
                    !string.Equals(path, external[0], StringComparison.OrdinalIgnoreCase)).All(path =>
                        IsUnder(path, expectedDirectory) && File.Exists(path));
        }

        private static bool NativeTopCoverRebuildPartCaptureComplete(
            NativeTopCoverRebuildPartCapture row)
        {
            if (row == null) return false;
            int expectedBends = ExpectedTopCoverPhysicalBendCount(row.name);
            bool v37 = row.purpose.StartsWith("v37_exact75:",
                StringComparison.Ordinal);
            int expectedExternal = ExpectedTopCoverPhysicalExternalCount(row.name);
            bool externalProfile = !v37 || (expectedExternal >= 0 &&
                row.measurement.modelExternalReferenceCount == expectedExternal &&
                row.measurement.extensionExternalReferenceCount == expectedExternal);
            bool dependencyProfile = row.preopenDependencies.closure_count > 0 &&
                (row.preopenDependencies.all_target_local ||
                    row.knownGoldBottomHistoricalDependencyAccepted);
            bool linkDiagnosticsAllowed = row.measurement.linkFeatures.All(value =>
                value.IndexOf("state=missing-path", StringComparison.OrdinalIgnoreCase) < 0 &&
                (value.IndexOf("state=external", StringComparison.OrdinalIgnoreCase) < 0 ||
                    (row.knownGoldBottomHistoricalDependencyAccepted && value.IndexOf(
                        "标准寄存柜(总装配).SLDASM", StringComparison.OrdinalIgnoreCase) >= 0)));
            bool linkPathsLocal = dependencyProfile && row.measurement.linkFeatures.Count > 0 &&
                linkDiagnosticsAllowed &&
                row.measurement.linkFeatures.All(value =>
                    value.IndexOf("state=missing-path", StringComparison.OrdinalIgnoreCase) < 0);
            return expectedBends > 0 && row.sourceBinding.unchanged && row.opened &&
                row.openErrors == 0 && row.openWarnings == 0 && row.readOnly && row.rebuilt &&
                row.cacheBeforeEmpty && row.cacheAfterEmpty && row.sessionExitProven &&
                NativeTopCoverPartRowMeasurementComplete(row.measurement) && externalProfile &&
                linkPathsLocal &&
                row.measurement.bodyCount == 1 && row.measurement.featureIssues.Count == 0 &&
                row.sketches.Count > 0 && row.sketches.All(value => value.complete) &&
                row.sketches.Sum(value => value.segmentCount) > 0 &&
                row.oneBends.Count == expectedBends && row.oneBends.All(value => value.complete) &&
                row.processBends.Count == 1 && row.processBends.All(value => value.complete) &&
                row.flatPatterns.Count == 1 && row.flatPatterns.All(value => value.complete) &&
                row.unresolved.Count == 0 && string.IsNullOrWhiteSpace(row.fault);
        }

        private static bool NativeTopCoverRebuildAssemblyCaptureComplete(
            NativeTopCoverRebuildAssemblyCapture row, string expectedDirectory)
        {
            if (row == null) return false;
            HashSet<string> expected = new HashSet<string>(TopCoverPhysicalPartNames(),
                StringComparer.OrdinalIgnoreCase) { "上盖 模型.sldprt" };
            HashSet<string> actual = new HashSet<string>(row.components.Select(value => value.name),
                StringComparer.OrdinalIgnoreCase);
            List<double> identity = IdentityTransform16();
            bool componentsComplete = row.components.Count == 6 && actual.Count == 6 &&
                actual.SetEquals(expected) && row.components.All(value =>
                    File.Exists(value.path) && SamePath(Path.GetDirectoryName(value.path),
                        expectedDirectory) && FileLinkCount(value.path) == 1 &&
                    !HasReparsePoint(value.path) &&
                    ExpectedGoldTopCoverSha256.ContainsKey(value.name) && string.Equals(
                        value.sha256, ExpectedGoldTopCoverSha256[value.name],
                        StringComparison.OrdinalIgnoreCase) && value.hiddenCaptured &&
                    value.visibleCaptured && value.envelopeCaptured &&
                    value.excludeFromBomCaptured && value.suppressedCaptured &&
                    value.fixedCaptured && value.suppression >= 0 &&
                    ValidTransform16(value.transform16) && SameTransform16(value.transform16,
                        identity));
            bool masterComplete = row.master != null && row.master.hidden &&
                row.master.envelope && row.master.excludeFromBom &&
                !row.master.suppressed;
            return row.binding.unchanged && row.cacheBeforeEmpty && row.opened &&
                row.openErrors == 0 && (row.openWarnings == 0 || row.openWarnings == 96) &&
                row.readOnly && row.rebuilt && row.cacheAfterEmpty && row.sessionExitProven &&
                row.preopenDependencies.all_target_local &&
                row.preopenDependencies.exact_inventory_set &&
                row.preopenDependencies.closure_count == GoldTopCoverCaptureCadFileNames.Length &&
                componentsComplete && masterComplete && row.unresolved.Count == 0;
        }

        private static bool NativeTopCoverRebuildCaptureComplete(NativeTopCoverRebuildCaptureEvidence probe)
        {
            List<double> identity = IdentityTransform16();
            return probe != null && probe.goldInputs.Count == 19 &&
                probe.goldInputs.All(value => value.unchanged) && probe.v37Parts.Count == 5 &&
                probe.goldParts.Count == 5 &&
                probe.v37Parts.All(NativeTopCoverRebuildPartCaptureComplete) &&
                probe.goldParts.All(NativeTopCoverRebuildPartCaptureComplete) &&
                probe.goldAssembly.complete && probe.physicalIdentityTransforms.Count == 5 &&
                probe.physicalIdentityTransforms.All(value => value.fixedCaptured &&
                    value.suppression >= 0 && ValidTransform16(value.transform16) &&
                    SameTransform16(value.transform16, identity)) &&
                probe.masterExcludedFromNewAssembly && probe.v37SourceExact75Unchanged &&
                probe.workingPackExact75Unchanged && probe.goldInputsUnchanged &&
                probe.goldCapturePackComplete && probe.goldSourceLockStateUnchanged;
        }
        private static bool WriteNativeTopCoverRebuildCapturePrivateEvidence(Result result, NativeTopCoverRebuildCaptureEvidence probe)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverRebuildCapturePrivateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(path);
                string expected = Path.Combine(result.attempt_directory, "evidence", "private");
                Require(result.preflight_passed && SamePath(parent, expected) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_EVIDENCE_SCOPE_INVALID", 67,
                    "private evidence escaped attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_EVIDENCE_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory, "TOPCOVER_NATIVE_REBUILD_CAPTURE_EVIDENCE_PATH_INVALID");
                if (File.Exists(path)) return false;
                Require(probe.v37SourceExact75Unchanged &&
                    probe.workingPackExact75Unchanged && probe.goldInputsUnchanged &&
                    probe.goldSourceLockStateUnchanged,
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_EVIDENCE_SOURCE_DRIFT", 67,
                    "source state drifted before private evidence write");
                probe.privateEvidenceWritten = true;
                WriteCreateNewAndReread(path, JsonBytes(probe));
                Dictionary<string, object> reread = ReadJsonObject(path);
                probe.privateEvidenceWritten = FileLinkCount(path) == 1 &&
                    !HasReparsePoint(path) && Bool(reread, "privateEvidenceWritten") &&
                    Bool(reread, "probeOnly") && !Bool(reread, "consumable");
                return probe.privateEvidenceWritten;
            }
            catch { return probe.privateEvidenceWritten = false; }
        }
        private static void WriteNativeTopCoverRebuildCaptureProbeCleanupEvidence(Result result)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverRebuildCaptureCleanupPrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(path);
                string expected = Path.Combine(result.attempt_directory, "evidence", "private");
                if (File.Exists(path)) return;
                Require(result.preflight_passed && SamePath(parent, expected) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_CLEANUP_SCOPE_INVALID", 67,
                    "cleanup evidence escaped attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_CLEANUP_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_CLEANUP_PATH_INVALID");
                Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "schema", "winnsen.16029.topcover_native_rebuild_capture_probe_cleanup.v1" },
                    { "probeOnly", true },
                    { "consumable", false },
                    { "processFinalGate", result.processes.final_gate },
                    { "protectedSourceUnchanged", result.source_unchanged },
                    { "targetRollbackAttempted", result.rollback_attempted },
                    { "targetRollbackSucceeded", result.rollback_succeeded },
                    { "targetRollbackError", result.rollback_error },
                    { "transientLockFilesRemoved",
                        result.rollback_transient_lock_files_removed },
                    { "goldCapturePackRollbackAttempted",
                        result.topcover_capture_pack_rollback_attempted },
                    { "goldCapturePackRollbackSucceeded",
                        result.topcover_capture_pack_rollback_succeeded },
                    { "goldCapturePackRollbackError",
                        result.topcover_capture_pack_rollback_error },
                    { "goldCapturePackTransientLockFilesRemoved",
                        result.topcover_capture_pack_transient_lock_files_removed },
                    { "goldCapturePackAbsent", string.IsNullOrWhiteSpace(
                        result.topcover_capture_pack) ||
                        !Directory.Exists(result.topcover_capture_pack) },
                    { "targetAbsent", !Directory.Exists(result.target_directory) },
                    { "cleanupPass", result.processes.final_gate && result.source_unchanged &&
                        !Directory.Exists(result.target_directory) &&
                        (string.IsNullOrWhiteSpace(result.topcover_capture_pack) ||
                            !Directory.Exists(result.topcover_capture_pack)) }
                };
                WriteCreateNewAndReread(path, JsonBytes(payload));
                Require(Bool(ReadJsonObject(path), "probeOnly") &&
                    !Bool(ReadJsonObject(path), "consumable"),
                    "TOPCOVER_NATIVE_REBUILD_CAPTURE_CLEANUP_REREAD_FAILED", 67,
                    "cleanup private boundary was not preserved");
            }
            catch { }
        }
        private static object ComRead(object target, string member, params object[] args)
        {
            try
            {
                return target == null ? null : target.GetType().InvokeMember(member,
                    BindingFlags.GetProperty | BindingFlags.InvokeMethod, null, target, args);
            }
            catch { return null; }
        }
        private static Array ComArray(object value)
        {
            return value as Array ?? new object[0];
        }
        private static List<double> ComDoubleList(object raw)
        {
            Array values = raw as Array;
            return values == null ? new List<double>() : values.Cast<object>().Select(value =>
            {
                try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
                catch { return double.NaN; }
            }).ToList();
        }
        private static List<int> ComIntList(object raw)
        {
            Array values = raw as Array;
            IEnumerable<object> items = values == null ? new[] { raw } : values.Cast<object>();
            return items.Where(value => value != null).Select(value =>
            {
                try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
                catch { return int.MinValue; }
            }).ToList();
        }
        private static List<double> IdentityTransform16()
        {
            return new List<double> { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
        }
        private static bool NativeTopCoverSketchPointComplete(
            NativeTopCoverSketchPointCapture value)
        {
            return value != null && value.localSi.Count == 3 && value.modelSi.Count == 3 &&
                MeasurementValuesFinite(value.localSi) && MeasurementValuesFinite(value.modelSi);
        }
        private static bool NativeTopCoverSketchSegmentComplete(
            NativeTopCoverSketchSegmentCapture value)
        {
            bool endpoints = NativeTopCoverSketchPointComplete(value == null ? null : value.start) &&
                NativeTopCoverSketchPointComplete(value == null ? null : value.end);
            bool arc = value != null && value.kind == "arc" ?
                NativeTopCoverSketchPointComplete(value.center) && value.radiusSi > 0 &&
                (value.rotationDirection == -1 || value.rotationDirection == 1) &&
                value.normalSi.Count == 3 && MeasurementValuesFinite(value.normalSi) : true;
            return value != null && (value.kind == "line" || value.kind == "arc") &&
                value.type >= 0 && value.ids.Count > 0 && value.status >= 0 &&
                value.constraintCount >= 0 && value.relationCount >= 0 && value.lengthSi > 0 &&
                MeasurementValuesFinite(new[] { value.lengthSi }) && endpoints && arc &&
                string.IsNullOrWhiteSpace(value.fault);
        }
        private static bool NativeTopCoverSketchBlockComplete(
            NativeTopCoverSketchBlockCapture value)
        {
            return value != null && !string.IsNullOrWhiteSpace(value.name) &&
                MeasurementValuesFinite(new[] { value.angle, value.scale }) && value.scale > 0 &&
                NativeTopCoverSketchPointComplete(value.insertion) &&
                value.definitionInsertionLocalSi.Count == 3 &&
                MeasurementValuesFinite(value.definitionInsertionLocalSi) &&
                ValidTransform16(value.blockToSketchTransform16) &&
                (!value.linkToFile || !string.IsNullOrWhiteSpace(value.definitionFileName));
        }
        private static bool NativeTopCoverFaceComplete(NativeTopCoverFaceCapture value)
        {
            return value != null && value.found && value.boxSi.Count == 6 &&
                value.normalSi.Count == 3 && MeasurementValuesFinite(value.boxSi) &&
                MeasurementValuesFinite(value.normalSi);
        }

        // This is intentionally not a stage implementation.  It is a bounded experiment
        // to learn whether a new top-cover assembly removes the relocation fault without
        // touching the protected source or reusing the old assembly bytes.
        private static void RunNativeTopCoverRebuildProbeOnly(Result result)
        {
            NativeTopCoverProbeEvidence probe = new NativeTopCoverProbeEvidence
            {
                probeOnly = true,
                consumable = false,
                protectedSourceInventoryDigestBefore = result.source_inventory_digest,
                sourceDirectory = result.source_directory,
                workingPack = result.target_directory
            };
            string repairRoot = Path.Combine(result.attempt_directory, "repair_staging",
                "topcover_native_rebuild");
            string candidateDirectory = Path.Combine(repairRoot, "candidate");
            string candidatePath = Path.Combine(candidateDirectory, TopCoverAssemblyFileName);
            string oldTopCover = Path.Combine(result.target_directory, TopCoverAssemblyFileName);
            string originalDirectory = Path.Combine(repairRoot, "original");
            string originalBackup = Path.Combine(originalDirectory, TopCoverAssemblyFileName);
            result.topcover_probe_repair_root = repairRoot;
            try
            {
                Require(result.direct_copy_completed && File.Exists(oldTopCover),
                    "TOPCOVER_NATIVE_PROBE_PRECONDITION_FAILED", 70,
                    "the exact direct-copy working pack and old top-cover are required");
                CreateSafeProbeDirectory(repairRoot, result.attempt_directory,
                    "TOPCOVER_NATIVE_PROBE_REPAIR_ROOT_INVALID");
                result.topcover_probe_repair_root_created = true;
                CreateSafeProbeDirectory(candidateDirectory, repairRoot,
                    "TOPCOVER_NATIVE_PROBE_CANDIDATE_ROOT_INVALID");
                CreateSafeProbeDirectory(originalDirectory, repairRoot,
                    "TOPCOVER_NATIVE_PROBE_ORIGINAL_ROOT_INVALID");
                AssertAuthorizationStillValid(result, "before_topcover_template_capture");
                probe.template = CaptureNativeTopCoverTemplate(result, oldTopCover);
                AssertAuthorizationStillValid(result, "before_topcover_candidate_create");
                CreateNativeTopCoverCandidate(result, probe.template, candidatePath, probe);
                AssertAuthorizationStillValid(result, "before_topcover_candidate_verify");
                VerifyNativeTopCoverCandidate(result, candidatePath, probe);
                Require(probe.candidateFreshPassed, "TOPCOVER_NATIVE_CANDIDATE_INVALID", 70,
                    "fresh candidate verification did not prove the exact six fixed components");
                Require(File.Exists(candidatePath) && FileLinkCount(candidatePath) == 1 &&
                    !HasReparsePoint(candidatePath) &&
                    SamePath(Path.GetDirectoryName(candidatePath), candidateDirectory) &&
                    string.Equals(Sha256File(candidatePath), probe.candidateSha256,
                        StringComparison.OrdinalIgnoreCase),
                    "TOPCOVER_NATIVE_CANDIDATE_FILE_IDENTITY_INVALID", 70,
                    "candidate path/hash/link identity changed after fresh verification");

                Require(FileLinkCount(oldTopCover) == 1 && !HasReparsePoint(oldTopCover) &&
                    string.Equals(Sha256File(oldTopCover), result.source_hashes[TopCoverAssemblyFileName],
                        StringComparison.OrdinalIgnoreCase),
                    "TOPCOVER_NATIVE_SWAP_OLD_BYTES_INVALID", 70,
                    "old top-cover was not an exact independent direct-copy byte source");
                AssertAuthorizationStillValid(result, "before_topcover_candidate_swap");
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_SWAP_NOT_QUIESCENT", 70,
                    "CAD processes were not stably empty before candidate swap");
                AssertAuthorizationStillValid(result, "at_topcover_candidate_swap_boundary");
                AssertPathChainNoReparse(result.target_directory,
                    result.attempt_directory, "TOPCOVER_NATIVE_SWAP_TARGET_PATH_INVALID");
                AssertPathChainNoReparse(candidateDirectory, repairRoot,
                    "TOPCOVER_NATIVE_SWAP_CANDIDATE_PATH_INVALID");
                AssertPathChainNoReparse(originalDirectory, repairRoot,
                    "TOPCOVER_NATIVE_SWAP_ORIGINAL_PATH_INVALID");
                List<string> preSwapFiles = CaptureFlatCadTree(result.target_directory,
                    "TOPCOVER_NATIVE_SWAP_PRESTATE_INVALID");
                Require(SamePath(oldTopCover, Path.Combine(result.target_directory,
                            TopCoverAssemblyFileName)) && File.Exists(oldTopCover) &&
                    FileLinkCount(oldTopCover) == 1 && !HasReparsePoint(oldTopCover) &&
                    string.Equals(Sha256File(oldTopCover),
                        result.source_hashes[TopCoverAssemblyFileName],
                        StringComparison.OrdinalIgnoreCase) &&
                    SamePath(candidatePath, Path.Combine(candidateDirectory,
                            TopCoverAssemblyFileName)) && File.Exists(candidatePath) &&
                    FileLinkCount(candidatePath) == 1 && !HasReparsePoint(candidatePath) &&
                    IsUnder(CanonicalExistingPath(candidateDirectory),
                        CanonicalExistingPath(repairRoot)) &&
                    IsUnder(CanonicalExistingPath(originalDirectory),
                        CanonicalExistingPath(repairRoot)) &&
                    string.Equals(Sha256File(candidatePath), probe.candidateSha256,
                        StringComparison.OrdinalIgnoreCase) && !File.Exists(originalBackup) &&
                    preSwapFiles.Count == ExpectedCadCount && new HashSet<string>(
                        preSwapFiles.Select(Path.GetFileName),
                        StringComparer.OrdinalIgnoreCase).SetEquals(result.source_hashes.Keys),
                    "TOPCOVER_NATIVE_SWAP_BOUNDARY_DRIFT", 70,
                    "authorization, exact75, old bytes or candidate identity drifted during quiescence wait");
                File.Copy(oldTopCover, originalBackup, false);
                Require(FileLinkCount(originalBackup) == 1 && !HasReparsePoint(originalBackup) &&
                    string.Equals(Sha256File(originalBackup), result.source_hashes[TopCoverAssemblyFileName],
                        StringComparison.OrdinalIgnoreCase),
                    "TOPCOVER_NATIVE_SWAP_BACKUP_INVALID", 70,
                    "fixed original backup could not be proven");
                File.Delete(oldTopCover);
                File.Move(candidatePath, oldTopCover);
                probe.swapCompleted = File.Exists(oldTopCover) && File.Exists(originalBackup) &&
                    string.Equals(Sha256File(oldTopCover), probe.candidateSha256,
                        StringComparison.OrdinalIgnoreCase);
                Require(probe.swapCompleted, "TOPCOVER_NATIVE_SWAP_FAILED", 70,
                    "candidate replacement did not preserve its exact candidate bytes");
                List<string> swappedFiles = CaptureFlatCadTree(result.target_directory,
                    "TOPCOVER_NATIVE_SWAP_INVENTORY_INVALID");
                Require(swappedFiles.Count == ExpectedCadCount && new HashSet<string>(
                        swappedFiles.Select(Path.GetFileName),
                        StringComparer.OrdinalIgnoreCase).SetEquals(result.source_hashes.Keys),
                    "TOPCOVER_NATIVE_SWAP_NAME_SET_INVALID", 70,
                    "working pack must retain the exact75 basename set immediately after swap");

                AssertAuthorizationStillValid(result, "before_topcover_root_probe");
                ProbeNativeRootAfterSwap(result, probe);
                List<string> targetFiles = CaptureFlatCadTree(result.target_directory,
                    "TOPCOVER_NATIVE_PROBE_TARGET_INVENTORY_INVALID");
                Require(targetFiles.Count == ExpectedCadCount &&
                    targetFiles.Count(IsAssembly) == ExpectedAssemblyCount &&
                    targetFiles.Count(IsPart) == ExpectedPartCount,
                    "TOPCOVER_NATIVE_PROBE_TARGET_COUNT_INVALID", 70,
                    "native rebuild probe must retain flat 75 = 22 assemblies + 53 parts");
                probe.targetInventoryDigest = InventoryDigest(targetFiles.Select(Snapshot));
                probe.changedNames = ChangedSourceNames(result).ToList();
                HashSet<string> allowed = probe.rootIdAcceptanceSaveAttempted ?
                    new HashSet<string>(new[] { RootFileName, TopCoverAssemblyFileName },
                        StringComparer.OrdinalIgnoreCase) :
                    new HashSet<string>(new[] { TopCoverAssemblyFileName },
                        StringComparer.OrdinalIgnoreCase);
                Require(new HashSet<string>(probe.changedNames,
                        StringComparer.OrdinalIgnoreCase).SetEquals(allowed),
                    "TOPCOVER_NATIVE_PROBE_CHANGED_FILES_INVALID", 70,
                    "changed files must be exactly top-cover, plus root only after ID acceptance");
                Require(string.Equals(Sha256File(oldTopCover), probe.candidateSha256,
                    StringComparison.OrdinalIgnoreCase) && TopCoverSixHashesUnchanged(result),
                    "TOPCOVER_NATIVE_PROBE_COMPONENT_HASH_INVALID", 70,
                    "candidate or one of the six top-cover parts changed unexpectedly");
                VerifySourceUnchanged(result);
                probe.protectedSourceUnchanged = result.source_unchanged;
                Require(probe.protectedSourceUnchanged,
                    "TOPCOVER_NATIVE_PROBE_SOURCE_CHANGED", 80,
                    "protected exact75 source changed during native rebuild probe");
                AssertAuthorizationStillValid(result, "before_topcover_private_evidence");
                probe.probePassed = true;
            }
            catch (StageException exception)
            {
                probe.failureStatus = exception.Status;
                probe.failureExitCode = exception.ExitCode;
                probe.failure = exception.Message;
                throw;
            }
            catch (Exception exception)
            {
                probe.failureStatus = "TOPCOVER_NATIVE_REBUILD_PROBE_FAILED";
                probe.failureExitCode = 70;
                probe.failure = SafeException(exception);
                throw new StageException("TOPCOVER_NATIVE_REBUILD_PROBE_FAILED", 70,
                    SafeException(exception));
            }
            finally
            {
                probe.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                probe.privateEvidenceWritten = WriteNativeTopCoverProbePrivateEvidence(result, probe);
                if (probe.probePassed)
                    Require(probe.privateEvidenceWritten,
                        "TOPCOVER_NATIVE_PRIVATE_EVIDENCE_WRITE_FAILED", 70,
                        "completed probe must persist its private nonconsumable evidence");
            }
        }

        private static void CreateSafeProbeDirectory(string path, string parent, string status)
        {
            Require(IsUnder(path, parent) && !Directory.Exists(path), status, 70,
                "probe directory is not a new child of its fixed parent: " + path);
            AssertPathChainNoReparse(path, parent, status + "_PRECREATE");
            Directory.CreateDirectory(path);
            AssertPathChainNoReparse(path, parent, status);
            Require(IsUnder(CanonicalExistingPath(path), CanonicalExistingPath(parent)), status, 70,
                "probe directory escaped its fixed parent: " + path);
        }

        private static NativeTopCoverTemplate CaptureNativeTopCoverTemplate(Result result,
            string templatePath)
        {
            NativeTopCoverTemplate output = new NativeTopCoverTemplate { path = templatePath };
            ISldWorks application = null;
            ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "native_topcover_template_readonly" };
            result.sessions.Add(session);
            string hashBefore = Sha256File(templatePath);
            try
            {
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_TEMPLATE_NOT_QUIESCENT", 70,
                    "CAD was not quiescent before template read-only capture");
                application = StartOwnedSolidWorks(result, session);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(templatePath, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                output.openErrors = errors; output.openWarnings = warnings;
                Require(model != null && errors == 0 && warnings == 96 && (warnings & 1) == 0 &&
                    Safe(() => model.IsOpenedReadOnly(), false),
                    "TOPCOVER_NATIVE_TEMPLATE_OPEN_INVALID", 70,
                    "old top-cover template must open exact read-only 0/96 without ID mismatch");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "TOPCOVER_NATIVE_TEMPLATE_NOT_ASSEMBLY", 70,
                    "old top-cover is not an assembly");
                Require(Safe(() => model.ForceRebuild3(false), false),
                    "TOPCOVER_NATIVE_TEMPLATE_REBUILD_FAILED", 70,
                    "read-only template did not rebuild before transform capture");
                Configuration configuration = model.GetActiveConfiguration() as Configuration;
                Component2 root = configuration == null ? null : configuration.GetRootComponent3(true) as Component2;
                object[] children = root == null ? new object[0] : ObjectArray(root.GetChildren());
                foreach (object value in children)
                {
                    Component2 component = value as Component2;
                    try
                    {
                        string path = Safe(() => component.GetPathName(), "");
                        MathTransform transform = Safe(() => component.Transform2, null);
                        double[] data = TransformValues(transform);
                        output.components.Add(new NativeTopCoverComponent
                        {
                            path = path,
                            name = Path.GetFileName(path),
                            sha256 = File.Exists(path) ? Sha256File(path) : "",
                            configuration = Safe(() => component.ReferencedConfiguration, ""),
                            transform16 = data == null ? new List<double>() : data.ToList(),
                            originallyFixed = Safe(() => component.IsFixed(), false),
                            suppression = Safe(() => component.GetSuppression(), -1),
                            suppressed = Safe(() => component.IsSuppressed(), true),
                            directChildCount = ObjectArray(Safe(() => component.GetChildren(), null)).Length
                        });
                        Release(transform);
                    }
                    finally { Release(component); }
                }
                Release(root); Release(configuration);
                output.exactSix = NativeTopCoverTemplateGate(output, result);
                Require(output.exactSix, "TOPCOVER_NATIVE_TEMPLATE_COMPONENTS_INVALID", 70,
                    "template must prove exact six direct local components and transforms");
            }
            finally
            {
                CloseDocument(application, ref model);
                CloseOwnedSolidWorks(ref application, result, session);
                output.fileUnchanged = File.Exists(templatePath) && string.Equals(hashBefore,
                    Sha256File(templatePath), StringComparison.OrdinalIgnoreCase);
                output.sessionExitProven = session.exit_state == "exited" && session.monitors_exit_proven;
            }
            Require(output.fileUnchanged && output.sessionExitProven, "TOPCOVER_NATIVE_TEMPLATE_MUTATED", 70,
                "template session changed old top-cover bytes or did not exit exactly");
            return output;
        }

        private static bool NativeTopCoverTemplateGate(NativeTopCoverTemplate template,
            Result result)
        {
            if (template == null || result == null || template.components == null) return false;
            string workingPack = result.target_directory;
            HashSet<string> expected = new HashSet<string>(TopCoverCandidateUpstreamFirstFileNames
                .Skip(1).Select(name => Path.GetFullPath(Path.Combine(workingPack, name))),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> actual = new HashSet<string>(template.components.Select(
                row => Path.GetFullPath(row.path)),
                StringComparer.OrdinalIgnoreCase);
            return template != null && template.components.Count == 6 && expected.SetEquals(actual) &&
                actual.Count == 6 && template.components.All(row => File.Exists(row.path) &&
                    SamePath(Path.GetDirectoryName(row.path), workingPack) &&
                    FileLinkCount(row.path) == 1 && !HasReparsePoint(row.path) &&
                    result.source_hashes.ContainsKey(row.name) &&
                    string.Equals(row.sha256, result.source_hashes[row.name],
                        StringComparison.OrdinalIgnoreCase) &&
                    ValidTransform16(row.transform16) && !row.suppressed &&
                    (row.suppression == (int)swComponentSuppressionState_e.swComponentResolved ||
                        row.suppression ==
                            (int)swComponentSuppressionState_e.swComponentFullyResolved) &&
                    row.directChildCount == 0);
        }

        private static double[] TransformValues(MathTransform transform)
        {
            if (transform == null) return new double[0];
            Array raw = Safe(() => transform.ArrayData as Array, null);
            if (raw == null) return new double[0];
            List<double> values = new List<double>();
            foreach (object value in raw)
            {
                try { values.Add(Convert.ToDouble(value, CultureInfo.InvariantCulture)); }
                catch { return new double[0]; }
            }
            return values.ToArray();
        }

        private static bool ValidTransform16(IList<double> values)
        {
            return values != null && values.Count == 16 && values.All(value =>
                !double.IsNaN(value) && !double.IsInfinity(value)) &&
                Math.Abs(values[12] - 1.0) <= 1e-9;
        }

        private static bool SameTransform16(IList<double> left, IList<double> right)
        {
            if (!ValidTransform16(left) || !ValidTransform16(right)) return false;
            for (int index = 0; index < 16; index++)
            {
                double tolerance = index >= 9 && index <= 11 ? 1e-7 : 1e-9;
                if (Math.Abs(left[index] - right[index]) > tolerance) return false;
            }
            return true;
        }

        private static void CreateNativeTopCoverCandidate(Result result,
            NativeTopCoverTemplate template, string candidatePath,
            NativeTopCoverProbeEvidence probe)
        {
            ISldWorks application = null;
            ModelDoc2 candidate = null;
            MathUtility math = null;
            ModelDocExtension extension = null;
            SessionEvidence session = new SessionEvidence { purpose = "native_topcover_candidate_create" };
            result.sessions.Add(session);
            try
            {
                Require(NativeTopCoverTemplateGate(template, result),
                    "TOPCOVER_NATIVE_CANDIDATE_TEMPLATE_INVALID", 70,
                    "candidate creation requires an exact read-only template capture");
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_CANDIDATE_NOT_QUIESCENT", 70,
                    "CAD was not quiescent before native candidate creation");
                application = StartOwnedSolidWorks(result, session);
                candidate = application.NewAssembly() as ModelDoc2;
                Require(candidate != null, "TOPCOVER_NATIVE_NEW_ASSEMBLY_FAILED", 70,
                    "SolidWorks did not create a new empty assembly");
                AssemblyDoc assembly = candidate as AssemblyDoc;
                Require(assembly != null, "TOPCOVER_NATIVE_NEW_ASSEMBLY_TYPE_INVALID", 70,
                    "new candidate is not an assembly");
                math = application.GetMathUtility() as MathUtility;
                Require(math != null, "TOPCOVER_NATIVE_MATH_UTILITY_INVALID", 70,
                    "candidate creation could not acquire MathUtility");
                foreach (NativeTopCoverComponent row in template.components.OrderBy(value =>
                    string.Equals(value.name, TopCoverCandidateUpstreamFirstFileNames[1],
                        StringComparison.OrdinalIgnoreCase) ? 1 : 0).ThenBy(value => value.name,
                            StringComparer.OrdinalIgnoreCase))
                {
                    Require(SamePath(Path.GetDirectoryName(row.path), result.target_directory) &&
                        ValidTransform16(row.transform16),
                        "TOPCOVER_NATIVE_CANDIDATE_INPUT_INVALID", 70,
                        "candidate component source or 16-value transform is invalid");
                    ModelDoc2 componentDocument = null;
                    Component2 component = null;
                    MathTransform transform = null;
                    MathTransform actualTransform = null;
                    try
                    {
                        int openErrors = 0; int openWarnings = 0;
                        componentDocument = application.OpenDoc6(row.path,
                            (int)swDocumentTypes_e.swDocPART,
                            (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                                swOpenDocOptions_e.swOpenDocOptions_ReadOnly),
                            row.configuration ?? "",
                            ref openErrors, ref openWarnings) as ModelDoc2;
                        bool componentReadOnly = componentDocument != null &&
                            Safe(() => componentDocument.IsOpenedReadOnly(), false);
                        probe.candidateComponentOpenProfiles.Add(row.name + ":" +
                            openErrors.ToString(CultureInfo.InvariantCulture) + "/" +
                            openWarnings.ToString(CultureInfo.InvariantCulture) +
                            ",null=" + (componentDocument == null ? "true" : "false") +
                            ",readonly=" + componentReadOnly.ToString().ToLowerInvariant());
                        Require(componentDocument != null && openErrors == 0 && openWarnings == 0 &&
                            componentReadOnly,
                            "TOPCOVER_NATIVE_COMPONENT_OPEN_FAILED", 70,
                            "component must open exact read-only 0/0: " + row.name);
                        int activationErrors = 0;
                        ModelDoc2 activated = application.ActivateDoc2(candidate.GetTitle(), false,
                            ref activationErrors) as ModelDoc2;
                        Require(activated != null && activationErrors == 0,
                            "TOPCOVER_NATIVE_CANDIDATE_ACTIVATE_FAILED", 70,
                            "new assembly could not be reactivated before AddComponent5");
                        ReleaseOneComReference(activated);
                        component = assembly.AddComponent5(row.path,
                            (int)swAddComponentConfigOptions_e.
                                swAddComponentConfigOptions_CurrentSelectedConfig,
                            "", false, "", 0.0, 0.0, 0.0) as Component2;
                        Require(component != null, "TOPCOVER_NATIVE_ADD_COMPONENT_FAILED", 70,
                            "could not add template component to a new candidate: " + row.name);
                        transform = math.CreateTransform(row.transform16.ToArray()) as MathTransform;
                        Require(transform != null &&
                            Safe(() => component.SetTransformAndSolve2(transform), false),
                            "TOPCOVER_NATIVE_SET_TRANSFORM_INVALID", 70,
                            "could not apply exact component transform: " + row.name);
                        Require(component.Select4(false, null, false),
                            "TOPCOVER_NATIVE_FIX_SELECT_FAILED", 70,
                            "could not select candidate component for fixing: " + row.name);
                        assembly.FixComponent();
                        candidate.ClearSelection2(true);
                        actualTransform = Safe(() => component.Transform2, null);
                        Require(Safe(() => component.IsFixed(), false) &&
                            SamePath(Safe(() => component.GetPathName(), ""), row.path) &&
                            SameTransform16(TransformValues(actualTransform), row.transform16),
                            "TOPCOVER_NATIVE_COMPONENT_READBACK_INVALID", 70,
                            "candidate component fixed/path/transform readback failed: " + row.name);
                    }
                    finally
                    {
                        Release(actualTransform); Release(transform); Release(component);
                        CloseDocument(application, ref componentDocument);
                    }
                }
                Require(Safe(() => candidate.ForceRebuild3(false), false),
                    "TOPCOVER_NATIVE_CANDIDATE_REBUILD_FAILED", 70,
                    "new candidate did not rebuild");
                AssertAuthorizationStillValid(result, "before_topcover_candidate_save_as");
                int saveErrors = 0; int saveWarnings = 0;
                extension = candidate.Extension as ModelDocExtension;
                Require(extension != null, "TOPCOVER_NATIVE_CANDIDATE_EXTENSION_INVALID", 70,
                    "new candidate ModelDocExtension is unavailable");
                probe.candidateSaveAttempted = true;
                bool saved = extension.SaveAs(candidatePath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null,
                    ref saveErrors, ref saveWarnings);
                probe.candidateSaveReturned = saved;
                probe.candidateSaveErrors = saveErrors;
                probe.candidateSaveWarnings = saveWarnings;
                probe.candidateFileExistsAfterSave = File.Exists(candidatePath);
                probe.candidateDirtyAfterSave = Safe(() => candidate.GetSaveFlag(), true);
                probe.candidateSaveProfileAllowed = CandidateSaveProfileAllowed(saved,
                    saveErrors, saveWarnings, probe.candidateFileExistsAfterSave,
                    probe.candidateDirtyAfterSave);
                Require(probe.candidateSaveProfileAllowed,
                    "TOPCOVER_NATIVE_CANDIDATE_SAVE_FAILED", 70,
                    "new candidate SaveAs invalid; saved=" + saved.ToString().ToLowerInvariant() +
                    ",errors=" + saveErrors.ToString(CultureInfo.InvariantCulture) +
                    ",warnings=" + saveWarnings.ToString(CultureInfo.InvariantCulture) +
                    ",exists=" + probe.candidateFileExistsAfterSave.ToString().ToLowerInvariant() +
                    ",dirty=" + probe.candidateDirtyAfterSave.ToString().ToLowerInvariant());
                probe.candidateSha256 = Sha256File(candidatePath);
                probe.candidateCreated = IsSha256(probe.candidateSha256);
            }
            finally
            {
                Release(extension); Release(math);
                CloseDocument(application, ref candidate);
                CloseOwnedSolidWorks(ref application, result, session);
            }
            Require(session.exit_state == "exited" && session.monitors_exit_proven &&
                probe.candidateCreated, "TOPCOVER_NATIVE_CANDIDATE_SESSION_INVALID", 70,
                "candidate session did not exit exactly or candidate hash is absent");
        }

        private static bool CandidateSaveProfileAllowed(bool saved, int errors,
            int warnings, bool fileExists, bool dirty)
        {
            bool exactSuccess = saved && errors == 0;
            bool readOnlyReferenceProfile = !saved && errors ==
                (int)swFileSaveError_e.swReadOnlySaveError;
            return warnings == 0 && fileExists && !dirty &&
                (exactSuccess || readOnlyReferenceProfile);
        }

        private static void VerifyNativeTopCoverCandidate(Result result, string candidatePath,
            NativeTopCoverProbeEvidence probe)
        {
            ISldWorks application = null; ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "native_topcover_candidate_fresh_readonly" };
            result.sessions.Add(session);
            string hashBefore = Sha256File(candidatePath);
            try
            {
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_CANDIDATE_VERIFY_NOT_QUIESCENT", 70,
                    "CAD was not quiescent before fresh candidate verification");
                application = StartOwnedSolidWorks(result, session);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(candidatePath, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                probe.candidateFreshErrors = errors; probe.candidateFreshWarnings = warnings;
                probe.candidateFreshReadOnly = model != null &&
                    Safe(() => model.IsOpenedReadOnly(), false);
                probe.candidateFreshRebuilt = model != null &&
                    Safe(() => model.ForceRebuild3(false), false);
                FeatureHealthEvidence health = model == null ? new FeatureHealthEvidence() :
                    CaptureFeatureHealth(model);
                probe.candidateFeatureHealth = health;
                probe.candidateFeatureIssueCount = health.issues.Count;
                if (model != null)
                {
                    bool traversalComplete = true;
                    int featureCount = 0;
                    CaptureRelocationFeatureDiagnostics(model, Path.GetFileName(candidatePath),
                        result.attempt_directory, probe.candidateRelocationFeatureIssues,
                        probe.candidateLinkFeatures, ref traversalComplete, ref featureCount);
                    probe.candidateRelocationFeatureTraversalComplete = traversalComplete;
                    probe.candidateRelocationFeatureCount = featureCount;
                    bool componentScanComplete = true;
                    CaptureRelocationAssemblyComponentDiagnostics(application, model,
                        candidatePath, result.attempt_directory,
                        probe.candidateComponentDiagnostics,
                        probe.candidateDiagnosticComponentPaths, ref componentScanComplete);
                    probe.candidateComponentDiagnosticComplete = componentScanComplete;
                    probe.candidateExternalReferenceDiagnostic =
                        ExternalReferenceDiagnostic(application);
                }
                Configuration configuration = model == null ? null :
                    model.GetActiveConfiguration() as Configuration;
                Component2 root = configuration == null ? null : configuration.GetRootComponent3(true) as Component2;
                object[] children = root == null ? new object[0] : ObjectArray(root.GetChildren());
                foreach (object raw in children)
                {
                    Component2 component = raw as Component2;
                    try
                    {
                        MathTransform transform = Safe(() => component.Transform2, null);
                        double[] data = TransformValues(transform);
                        int suppression = Safe(() => component.GetSuppression(), -1);
                        probe.candidateComponents.Add(new NativeTopCoverComponent
                        {
                            path = Safe(() => component.GetPathName(), ""),
                            name = Path.GetFileName(Safe(() => component.GetPathName(), "")),
                            sha256 = File.Exists(Safe(() => component.GetPathName(), "")) ?
                                Sha256File(Safe(() => component.GetPathName(), "")) : "",
                            configuration = Safe(() => component.ReferencedConfiguration, ""),
                            transform16 = data == null ? new List<double>() : data.ToList(),
                            originallyFixed = Safe(() => component.IsFixed(), false),
                            suppression = suppression,
                            suppressed = Safe(() => component.IsSuppressed(), true),
                            directChildCount = ObjectArray(Safe(() => component.GetChildren(), null)).Length
                        });
                        Release(transform);
                    }
                    finally { Release(component); }
                }
                Release(root); Release(configuration);
                probe.candidateClosurePaths = Strings(Safe(() =>
                    application.GetDocumentDependencies2(candidatePath, true, true, false), null))
                    .Where(value => Path.IsPathRooted(value) &&
                        (IsAssembly(value) || IsPart(value))).Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                probe.candidateClosurePaths.Add(Path.GetFullPath(candidatePath));
                probe.candidateClosurePaths = probe.candidateClosurePaths.Distinct(
                    StringComparer.OrdinalIgnoreCase).OrderBy(Path.GetFileName,
                        StringComparer.OrdinalIgnoreCase).ToList();
                probe.candidateClosureCount = probe.candidateClosurePaths.Count;
                probe.candidateFreshPassed = CandidateComponentGate(result, probe);
                Require(model != null && errors == 0 && warnings == 0 &&
                    probe.candidateFreshReadOnly && probe.candidateFreshRebuilt,
                    "TOPCOVER_NATIVE_CANDIDATE_FRESH_OPEN_FAILED", 70,
                    "fresh candidate must open read-only with exact 0/0 and rebuild; observed=" +
                    errors.ToString(CultureInfo.InvariantCulture) + "/" +
                    warnings.ToString(CultureInfo.InvariantCulture) +
                    ",readonly=" + probe.candidateFreshReadOnly.ToString().ToLowerInvariant() +
                    ",rebuilt=" + probe.candidateFreshRebuilt.ToString().ToLowerInvariant());
                Require(health.traversal_complete && health.issues.Count == 0 &&
                    probe.candidateRelocationFeatureTraversalComplete &&
                    probe.candidateComponentDiagnosticComplete,
                    "TOPCOVER_NATIVE_CANDIDATE_FEATURES_INVALID", 70,
                    "fresh candidate feature/component traversal is incomplete or contains an issue");
            }
            finally
            {
                CloseDocument(application, ref model);
                CloseOwnedSolidWorks(ref application, result, session);
                probe.candidateFileUnchanged = File.Exists(candidatePath) && string.Equals(hashBefore,
                    Sha256File(candidatePath), StringComparison.OrdinalIgnoreCase);
                probe.candidateSessionExitProven = session.exit_state == "exited" && session.monitors_exit_proven;
            }
            probe.candidateFreshPassed = probe.candidateFreshPassed && probe.candidateFileUnchanged &&
                probe.candidateSessionExitProven;
        }

        private static bool CandidateComponentGate(Result result, NativeTopCoverProbeEvidence probe)
        {
            NativeTopCoverTemplate template = probe.template;
            if (template == null || probe.candidateComponents.Count != 6 ||
                probe.candidateFreshErrors != 0 || probe.candidateFreshWarnings != 0 ||
                probe.candidateFeatureIssueCount != 0 || probe.candidateClosureCount != 7) return false;
            Dictionary<string, NativeTopCoverComponent> expected = template.components.ToDictionary(
                row => row.path, StringComparer.OrdinalIgnoreCase);
            HashSet<string> actualPaths = new HashSet<string>(probe.candidateComponents.Select(
                row => Path.GetFullPath(row.path)), StringComparer.OrdinalIgnoreCase);
            HashSet<string> actualClosure = new HashSet<string>(probe.candidateClosurePaths,
                StringComparer.OrdinalIgnoreCase);
            string candidateAssembly = probe.candidateClosurePaths.SingleOrDefault(path =>
                string.Equals(Path.GetFileName(path), TopCoverAssemblyFileName,
                    StringComparison.OrdinalIgnoreCase) &&
                !SamePath(Path.GetDirectoryName(path), result.target_directory));
            HashSet<string> expectedClosure = new HashSet<string>(template.components.Select(row =>
                Path.GetFullPath(row.path)).Concat(new[] { candidateAssembly ?? "" }),
                StringComparer.OrdinalIgnoreCase);
            return actualPaths.Count == 6 && actualPaths.SetEquals(expected.Keys) &&
                !string.IsNullOrWhiteSpace(candidateAssembly) &&
                actualClosure.SetEquals(expectedClosure) &&
                probe.candidateComponents.All(row => expected.ContainsKey(row.path) &&
                SamePath(Path.GetDirectoryName(row.path), result.target_directory) &&
                FileLinkCount(row.path) == 1 && !HasReparsePoint(row.path) &&
                row.directChildCount == 0 && row.originallyFixed && !row.suppressed &&
                (row.suppression == (int)swComponentSuppressionState_e.swComponentResolved ||
                    row.suppression ==
                        (int)swComponentSuppressionState_e.swComponentFullyResolved) &&
                SameTransform16(row.transform16, expected[row.path].transform16) &&
                string.Equals(row.configuration, expected[row.path].configuration,
                    StringComparison.Ordinal) &&
                string.Equals(row.sha256, expected[row.path].sha256,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static void ProbeNativeRootAfterSwap(Result result, NativeTopCoverProbeEvidence probe)
        {
            ISldWorks application = null; ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "native_topcover_root_initial_readonly" };
            result.sessions.Add(session);
            Dictionary<string, string> initialHashesBefore = CaptureFlatCadTree(
                result.target_directory, "TOPCOVER_NATIVE_ROOT_INITIAL_HASH_BASELINE_INVALID")
                .ToDictionary(Path.GetFileName, Sha256File,
                    StringComparer.OrdinalIgnoreCase);
            try
            {
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_ROOT_INITIAL_NOT_QUIESCENT", 70,
                    "CAD was not quiescent before root initial read-only open");
                application = StartOwnedSolidWorks(result, session);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(result.target_root_path, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                probe.initialRootErrors = errors; probe.initialRootWarnings = warnings;
                Require(model != null && errors == 0 && (warnings == 32 || warnings == 33),
                    "TOPCOVER_NATIVE_ROOT_INITIAL_WARNING_INVALID", 70,
                    "initial root after native swap must be exact 0/32 or 0/33");
                Require(Safe(() => model.IsOpenedReadOnly(), false),
                    "TOPCOVER_NATIVE_ROOT_INITIAL_NOT_READONLY", 70,
                    "initial root probe unexpectedly became writable");
                probe.initialRootHierarchy = CaptureHierarchy(model, result.target_directory);
                probe.initialMismatchPaths = CaptureInternalIdMismatchPaths(model);
                probe.initialRootDependencies = CaptureDependencies(application,
                    result.target_root_path, result.target_directory);
                Require(ExactDependencyGate(probe.initialRootDependencies),
                    "TOPCOVER_NATIVE_ROOT_INITIAL_DEPENDENCY_INVALID", 70,
                    "root dependency closure changed after native top-cover swap");
                if (warnings == 33)
                {
                    Require(probe.initialRootHierarchy.internal_id_mismatch_count == 1 &&
                        probe.initialRootHierarchy.top_level_component_count ==
                            ExpectedTopLevelComponents &&
                        probe.initialRootHierarchy.lightweight_component_count == 0 &&
                        probe.initialRootHierarchy.nonlocal_or_missing_path_count == 0 &&
                        probe.initialRootHierarchy.unresolved_active_component_count <= 1 &&
                        probe.initialMismatchPaths.Count == 1 && SamePath(
                            probe.initialMismatchPaths[0], Path.Combine(
                                result.target_directory, TopCoverAssemblyFileName)),
                        "TOPCOVER_NATIVE_ROOT_ID_MISMATCH_NOT_TOPCOVER", 70,
                        "warning 33 must be exactly one top-cover internal-ID mismatch");
                }
                else
                {
                    Require(probe.initialMismatchPaths.Count == 0 &&
                        HierarchyGate(probe.initialRootHierarchy) &&
                        Safe(() => model.ForceRebuild3(false), false),
                        "TOPCOVER_NATIVE_ROOT_INITIAL_32_INVALID", 70,
                        "warning 32 root must already have exact hierarchy and no ID mismatch");
                }
            }
            finally { CloseDocument(application, ref model); CloseOwnedSolidWorks(ref application, result, session); }
            Require(session.exit_state == "exited" && session.monitors_exit_proven,
                "TOPCOVER_NATIVE_ROOT_INITIAL_SESSION_INVALID", 70,
                "initial root read-only session did not exit exactly");
            Dictionary<string, string> initialHashesAfter = CaptureFlatCadTree(
                result.target_directory, "TOPCOVER_NATIVE_ROOT_INITIAL_HASH_POSTSTATE_INVALID")
                .ToDictionary(Path.GetFileName, Sha256File,
                    StringComparer.OrdinalIgnoreCase);
            probe.initialRootReadonlyHashesUnchanged = initialHashesBefore.Count ==
                ExpectedCadCount && initialHashesAfter.Count == ExpectedCadCount &&
                initialHashesBefore.All(row => initialHashesAfter.ContainsKey(row.Key) &&
                    string.Equals(initialHashesAfter[row.Key], row.Value,
                        StringComparison.OrdinalIgnoreCase));
            Require(probe.initialRootReadonlyHashesUnchanged,
                "TOPCOVER_NATIVE_ROOT_INITIAL_READONLY_MUTATION", 70,
                "initial root read-only mismatch diagnosis changed CAD bytes");
            if (probe.initialRootWarnings == 33)
            {
                AssertAuthorizationStillValid(result, "before_topcover_root_id_acceptance");
                AcceptExact33RootIdMismatch(result, probe);
            }
            AssertAuthorizationStillValid(result, "before_topcover_final_verification");
            VerifyNativeRootFinal(result, probe);
        }

        private static bool ExactDependencyGate(DependencyEvidence evidence)
        {
            return evidence != null && evidence.closure_count == ExpectedCadCount &&
                evidence.all_target_local && evidence.exact_inventory_set &&
                evidence.transient_lock_files_valid;
        }

        private static List<string> CaptureInternalIdMismatchPaths(ModelDoc2 model)
        {
            List<string> output = new List<string>();
            Configuration configuration = null; Component2 root = null;
            try
            {
                configuration = model.GetActiveConfiguration() as Configuration;
                root = configuration == null ? null : configuration.GetRootComponent3(true) as Component2;
                CaptureInternalIdMismatchPathsRecursive(root, output, 0);
            }
            finally { Release(root); Release(configuration); }
            return output.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void CaptureInternalIdMismatchPathsRecursive(Component2 component,
            List<string> output, int depth)
        {
            if (component == null || depth > 20) return;
            if (Safe(() => component.GetSuppression(), -1) ==
                (int)swComponentSuppressionState_e.swComponentInternalIdMismatch)
                output.Add(Safe(() => component.GetPathName(), ""));
            foreach (object raw in ObjectArray(Safe(() => component.GetChildren(), null)))
            {
                Component2 child = raw as Component2;
                try { CaptureInternalIdMismatchPathsRecursive(child, output, depth + 1); }
                finally { Release(child); }
            }
        }

        // The sole additional root write path. It is reachable only from the exact 33
        // top-cover mismatch branch and performs exactly one silent Save3.
        private static void AcceptExact33RootIdMismatch(Result result, NativeTopCoverProbeEvidence probe)
        {
            ISldWorks application = null; ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "native_topcover_root_id_acceptance" };
            result.sessions.Add(session);
            try
            {
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_NOT_QUIESCENT", 70,
                    "CAD was not quiescent before root ID acceptance");
                application = StartOwnedSolidWorks(result, session);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(result.target_root_path, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors,
                    ref warnings) as ModelDoc2;
                Require(model != null && errors == 0 && warnings == 33,
                    "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_OPEN_INVALID", 70,
                    "root ID acceptance requires exact writable 0/33");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_NOT_ASSEMBLY", 70,
                    "root ID acceptance document is not an assembly");
                HierarchyEvidence hierarchy = CaptureHierarchy(model, result.target_directory);
                List<string> mismatchPaths = CaptureInternalIdMismatchPaths(model);
                Require(hierarchy.internal_id_mismatch_count == 1 &&
                    hierarchy.top_level_component_count == ExpectedTopLevelComponents &&
                    mismatchPaths.Count == 1 && SamePath(mismatchPaths[0],
                        Path.Combine(result.target_directory, TopCoverAssemblyFileName)),
                    "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_PATH_INVALID", 70,
                    "writable root mismatch is not the exact top-cover leaf");
                int resolveStatus = Safe(() => assembly.ResolveAllLightWeightComponents(false), -1);
                Require((resolveStatus == 0 || resolveStatus == 2) &&
                    Safe(() => model.ForceRebuild3(false), false) &&
                    KnownRootIssueGate(CaptureFeatureHealth(model)),
                    "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_TOPOLOGY_INVALID", 70,
                    "root must retain topology and only trusted Reference 51 before ID save");
                int saveErrors = 0; int saveWarnings = 0;
                probe.rootShaBeforeIdAcceptance = Sha256File(result.target_root_path);
                probe.rootIdAcceptanceSaveAttempted = true;
                AssertAuthorizationStillValid(result, "before_topcover_root_id_save");
                probe.rootIdAcceptanceSaved = Safe(() => model.Save3(
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors,
                    ref saveWarnings), false);
                probe.rootIdAcceptanceSaveErrors = saveErrors;
                probe.rootIdAcceptanceSaveWarnings = saveWarnings;
                Require(probe.rootIdAcceptanceSaved && saveErrors == 0 && saveWarnings == 0 &&
                    !Safe(() => model.GetSaveFlag(), true), "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_SAVE_FAILED", 70,
                    "the sole ID acceptance Save3 did not complete cleanly");
                probe.rootShaAfterIdAcceptance = Sha256File(result.target_root_path);
                Require(!string.Equals(probe.rootShaBeforeIdAcceptance,
                        probe.rootShaAfterIdAcceptance, StringComparison.OrdinalIgnoreCase),
                    "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_HASH_UNCHANGED", 70,
                    "ID acceptance save must change the disposable root bytes exactly once");
            }
            finally { CloseDocument(application, ref model); CloseOwnedSolidWorks(ref application, result, session); }
            Require(session.exit_state == "exited" && session.monitors_exit_proven,
                "TOPCOVER_NATIVE_ROOT_ID_ACCEPT_SESSION_INVALID", 70,
                "root ID acceptance session did not exit exactly");
        }

        private static void VerifyNativeRootFinal(Result result, NativeTopCoverProbeEvidence probe)
        {
            ISldWorks application = null; ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "native_topcover_root_final_readonly" };
            result.sessions.Add(session);
            Dictionary<string, string> hashesBefore = CaptureFlatCadTree(result.target_directory,
                "TOPCOVER_NATIVE_FINAL_HASH_BASELINE_INVALID").ToDictionary(
                    Path.GetFileName, Sha256File, StringComparer.OrdinalIgnoreCase);
            try
            {
                Require(WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "TOPCOVER_NATIVE_ROOT_FINAL_NOT_QUIESCENT", 70,
                    "CAD was not quiescent before final root read-only verification");
                application = StartOwnedSolidWorks(result, session);
                int errors = 0; int warnings = 0;
                model = application.OpenDoc6(result.target_root_path, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                probe.finalRootErrors = errors; probe.finalRootWarnings = warnings;
                probe.finalRootHierarchy = model == null ? new HierarchyEvidence() :
                    CaptureHierarchy(model, result.target_directory);
                probe.finalRootFeature = model == null ? new FeatureHealthEvidence() : CaptureFeatureHealth(model);
                Require(model != null && errors == 0 && warnings == 32 &&
                    Safe(() => model.IsOpenedReadOnly(), false) &&
                    Safe(() => model.ForceRebuild3(false), false) &&
                    HierarchyGate(probe.finalRootHierarchy) && KnownRootIssueGate(probe.finalRootFeature),
                    "TOPCOVER_NATIVE_ROOT_FINAL_INVALID", 70,
                    "final root must be 0/32, exact hierarchy, and unique trusted Reference 51");
                probe.finalRootDependencies = CaptureDependencies(application,
                    result.target_root_path, result.target_directory);
                Require(ExactDependencyGate(probe.finalRootDependencies),
                    "TOPCOVER_NATIVE_ROOT_FINAL_DEPENDENCY_INVALID", 70,
                    "final root dependency closure is not the exact local 75-file inventory");
                CloseDocument(application, ref model);
                Try(() => application.CloseAllDocuments(true));
                int documentsAfterClose;
                Require(DocumentCacheEmpty(application, out documentsAfterClose),
                    "TOPCOVER_NATIVE_FINAL_RELOCATION_CACHE_INVALID", 70,
                    "root cache did not empty before complete relocation verification");
                probe.finalRelocationDiagnostic = CaptureRelocationDocumentDiagnostics(application,
                    result.target_directory);
                Require(FinalProbeRelocationDiagnosticGate(probe.finalRelocationDiagnostic),
                    "TOPCOVER_NATIVE_FINAL_RELOCATION_INVALID", 70,
                    "final relocation diagnostic must contain no warning64/document fault and only trusted Reference 51");
            }
            finally { CloseDocument(application, ref model); CloseOwnedSolidWorks(ref application, result, session); }
            Require(session.exit_state == "exited" && session.monitors_exit_proven,
                "TOPCOVER_NATIVE_ROOT_FINAL_SESSION_INVALID", 70,
                "final root session did not exit exactly");
            Dictionary<string, string> hashesAfter = CaptureFlatCadTree(result.target_directory,
                "TOPCOVER_NATIVE_FINAL_HASH_POSTSTATE_INVALID").ToDictionary(
                    Path.GetFileName, Sha256File, StringComparer.OrdinalIgnoreCase);
            probe.finalReadonlyHashesUnchanged = hashesBefore.Count == ExpectedCadCount &&
                hashesAfter.Count == ExpectedCadCount && hashesBefore.All(row =>
                    hashesAfter.ContainsKey(row.Key) && string.Equals(hashesAfter[row.Key],
                        row.Value, StringComparison.OrdinalIgnoreCase));
            Require(probe.finalReadonlyHashesUnchanged,
                "TOPCOVER_NATIVE_FINAL_READONLY_MUTATION", 70,
                "final root and complete 75-document read-only verification changed CAD bytes");
        }

        private static bool FinalProbeRelocationDiagnosticGate(string diagnostic)
        {
            string expectedIssue = DiagnosticValue(RootFileName) + ">" +
                DiagnosticValue(KnownIssueName) + "[Reference]:51/51/true";
            return !string.IsNullOrWhiteSpace(diagnostic) && diagnostic.StartsWith(
                "scanComplete=true; scanned=75; warning64Docs=none; documentFaults=none; " +
                "featureIssues=" + expectedIssue + "; linkFeatures=", StringComparison.Ordinal) &&
                diagnostic.EndsWith("; assemblyComponentDiagnostics=none",
                    StringComparison.Ordinal);
        }

        private static IEnumerable<string> ChangedSourceNames(Result result)
        {
            return result.source_inventory.Where(row =>
                !File.Exists(Path.Combine(result.target_directory, row.name)) ||
                !string.Equals(row.sha256, Sha256File(Path.Combine(result.target_directory, row.name)),
                    StringComparison.OrdinalIgnoreCase)).Select(row => row.name)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        }

        private static bool TopCoverSixHashesUnchanged(Result result)
        {
            return TopCoverCandidateUpstreamFirstFileNames.Skip(1).All(name =>
            {
                string path = Path.Combine(result.target_directory, name);
                return File.Exists(path) && FileLinkCount(path) == 1 &&
                    !HasReparsePoint(path) && SamePath(Path.GetDirectoryName(path),
                        result.target_directory) && result.source_hashes.ContainsKey(name) &&
                    string.Equals(Sha256File(path), result.source_hashes[name],
                        StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool WriteNativeTopCoverProbePrivateEvidence(Result result,
            NativeTopCoverProbeEvidence probe)
        {
            try
            {
                string evidencePath = Path.Combine(result.attempt_directory,
                    NativeTopCoverProbePrivateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(evidencePath);
                string expectedParent = Path.Combine(result.attempt_directory, "evidence",
                    "private");
                Require(result.preflight_passed && Directory.Exists(result.attempt_directory) &&
                    SamePath(parent, expectedParent) &&
                    IsUnder(parent, result.attempt_directory),
                    "TOPCOVER_NATIVE_PRIVATE_EVIDENCE_SCOPE_INVALID", 70,
                    "private probe evidence path is not inside the proven fixed attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PRIVATE_EVIDENCE_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_PRIVATE_EVIDENCE_PATH_INVALID");
                if (File.Exists(evidencePath)) return false;
                probe.privateEvidenceWritten = true;
                WriteCreateNewAndReread(evidencePath, JsonBytes(probe));
                bool written = File.Exists(evidencePath) && FileLinkCount(evidencePath) == 1 &&
                    !HasReparsePoint(evidencePath) && Bool(ReadJsonObject(evidencePath),
                        "privateEvidenceWritten");
                probe.privateEvidenceWritten = written;
                return written;
            }
            catch
            {
                probe.privateEvidenceWritten = false;
                return false;
            }
        }

        private static void WriteNativeTopCoverProbeCleanupEvidence(Result result)
        {
            try
            {
                string path = Path.Combine(result.attempt_directory,
                    NativeTopCoverProbeCleanupPrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                if (File.Exists(path)) return;
                string parent = Path.GetDirectoryName(path);
                string expectedParent = Path.Combine(result.attempt_directory, "evidence",
                    "private");
                Require(result.preflight_passed && Directory.Exists(result.attempt_directory) &&
                    SamePath(parent, expectedParent) &&
                    IsUnder(parent, result.attempt_directory) &&
                    IsUnder(result.attempt_directory, result.attempts_root),
                    "TOPCOVER_NATIVE_CLEANUP_EVIDENCE_SCOPE_INVALID", 70,
                    "cleanup evidence path is not inside the proven fixed attempt");
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_CLEANUP_EVIDENCE_PRECREATE_PATH_INVALID");
                if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                AssertPathChainNoReparse(parent, result.attempt_directory,
                    "TOPCOVER_NATIVE_CLEANUP_EVIDENCE_PATH_INVALID");
                bool targetAbsent = !Directory.Exists(result.target_directory);
                bool repairAbsent = string.IsNullOrWhiteSpace(
                    result.topcover_probe_repair_root) ||
                    !Directory.Exists(result.topcover_probe_repair_root);
                Dictionary<string, object> payload = new Dictionary<string, object>(
                    StringComparer.Ordinal)
                {
                    { "schema", "winnsen.16029.topcover_native_rebuild_probe_cleanup.v1" },
                    { "probeOnly", true }, { "consumable", false },
                    { "generatedAtUtc", DateTime.UtcNow.ToString("o",
                        CultureInfo.InvariantCulture) },
                    { "processFinalGate", result.processes.final_gate },
                    { "protectedSourceUnchanged", result.source_unchanged },
                    { "targetRollbackAttempted", result.rollback_attempted },
                    { "targetRollbackSucceeded", result.rollback_succeeded },
                    { "targetAbsent", targetAbsent },
                    { "repairRollbackAttempted",
                        result.topcover_probe_repair_rollback_attempted },
                    { "repairRollbackSucceeded",
                        result.topcover_probe_repair_rollback_succeeded },
                    { "repairAbsent", repairAbsent },
                    { "cleanupPass", result.processes.final_gate &&
                        result.source_unchanged && targetAbsent && repairAbsent }
                };
                WriteCreateNewAndReread(path, JsonBytes(payload));
                result.topcover_probe_cleanup_evidence_written = File.Exists(path) &&
                    FileLinkCount(path) == 1 && !HasReparsePoint(path);
                if (result.topcover_probe_cleanup_evidence_written)
                    result.topcover_probe_cleanup_evidence_sha256 = Sha256File(path);
            }
            catch (Exception exception)
            {
                result.topcover_probe_cleanup_evidence_error = SafeException(exception);
            }
        }

        private static void RollbackNativeTopCoverRepairStaging(Result result)
        {
            result.topcover_probe_repair_rollback_attempted = true;
            try
            {
                string repairRoot = result.topcover_probe_repair_root;
                string attemptDirectory = result.attempt_directory;
                if (!result.topcover_probe_repair_root_created ||
                    string.IsNullOrWhiteSpace(repairRoot) || !Directory.Exists(repairRoot))
                {
                    result.topcover_probe_repair_rollback_succeeded =
                        string.IsNullOrWhiteSpace(repairRoot) || !Directory.Exists(repairRoot);
                    return;
                }
                string expectedRoot = Path.Combine(attemptDirectory, "repair_staging",
                    "topcover_native_rebuild");
                string candidateDirectory = Path.Combine(repairRoot, "candidate");
                string originalDirectory = Path.Combine(repairRoot, "original");
                HashSet<string> allowedDirectories = new HashSet<string>(new[] {
                    Path.GetFullPath(candidateDirectory), Path.GetFullPath(originalDirectory) },
                    StringComparer.OrdinalIgnoreCase);
                if (!SamePath(repairRoot, expectedRoot) || !IsUnder(repairRoot, attemptDirectory) ||
                    TreeHasReparsePoint(repairRoot) || !IsUnder(CanonicalExistingPath(repairRoot),
                        CanonicalExistingPath(attemptDirectory)))
                {
                    result.topcover_probe_repair_rollback_error =
                        "repair root is not the fixed owned path";
                    return;
                }
                string[] directories = Directory.GetDirectories(repairRoot, "*",
                    SearchOption.AllDirectories);
                if (directories.Length > 2 || directories.Any(path =>
                    !allowedDirectories.Contains(Path.GetFullPath(path))))
                {
                    result.topcover_probe_repair_rollback_error =
                        "repair root directory set is outside candidate+original";
                    return;
                }
                string[] files = Directory.GetFiles(repairRoot, "*", SearchOption.AllDirectories);
                if (files.Length > 2 || files.Any(path =>
                    !string.Equals(Path.GetFileName(path), TopCoverAssemblyFileName,
                        StringComparison.OrdinalIgnoreCase) ||
                    (!SamePath(Path.GetDirectoryName(path), candidateDirectory) &&
                        !SamePath(Path.GetDirectoryName(path), originalDirectory)) ||
                    HasReparsePoint(path) || FileLinkCount(path) != 1 ||
                    !IsUnder(CanonicalExistingPath(path), CanonicalExistingPath(repairRoot))))
                {
                    result.topcover_probe_repair_rollback_error =
                        "repair root contains an unknown, linked or escaped file";
                    return;
                }
                Directory.Delete(repairRoot, true);
                result.topcover_probe_repair_rollback_succeeded = !Directory.Exists(repairRoot);
                string repairParent = Path.GetDirectoryName(repairRoot);
                string expectedParent = Path.Combine(attemptDirectory, "repair_staging");
                if (result.topcover_probe_repair_rollback_succeeded &&
                    SamePath(repairParent, expectedParent) && Directory.Exists(repairParent) &&
                    !HasReparsePoint(repairParent) &&
                    Directory.GetFileSystemEntries(repairParent).Length == 0)
                    Directory.Delete(repairParent, false);
                if (!result.topcover_probe_repair_rollback_succeeded)
                    result.topcover_probe_repair_rollback_error = "repair root still exists";
            }
            catch (Exception exception)
            {
                result.topcover_probe_repair_rollback_error = SafeException(exception);
            }
        }
        private static void RunRelocationPrewriteDiagnostic(Result result)
        {
            ISldWorks application = null;
            SessionEvidence session = new SessionEvidence
                { purpose = "prewrite_readonly_relocation_diagnostic" };
            result.sessions.Add(session);
            string diagnostic = "not-run";
            List<string> topCoverComponentPaths = new List<string>();
            try
            {
                application = StartOwnedSolidWorks(result, session);
                Require(application != null, "PREWRITE_DIAGNOSTIC_SOLIDWORKS_SESSION_INVALID",
                    10, "could not establish an exact owned SolidWorks 2020 session");
                Require(application.SetCurrentWorkingDirectory(result.target_directory),
                    "PREWRITE_DIAGNOSTIC_WORKING_DIRECTORY_INVALID", 10,
                    "prewrite diagnostic could not bind the attempt-local working pack");
                diagnostic = CaptureRelocationDocumentDiagnostics(application,
                    result.target_directory, out topCoverComponentPaths);
            }
            finally
            {
                if (application != null) Try(() => application.CloseAllDocuments(true));
                CloseOwnedSolidWorks(ref application, result, session);
            }
            Require(session.exit_state == "exited" && session.monitors_exit_proven,
                "PREWRITE_DIAGNOSTIC_SESSION_EXIT_NOT_PROVEN", 10,
                "prewrite diagnostic SolidWorks and monitor exits were not exactly proven; sw=" +
                session.exit_state + "; monitorsProven=" + session.monitors_exit_proven +
                "; diagnostic=" + diagnostic + "; cleanupErrors=" +
                string.Join("|", result.processes.cleanup_errors));

            List<string> freshComponentRows = new List<string>();
            bool freshComponentScanComplete = true;
            foreach (string componentPath in topCoverComponentPaths.Distinct(
                StringComparer.OrdinalIgnoreCase).OrderBy(Path.GetFileName,
                    StringComparer.OrdinalIgnoreCase))
            {
                FreshComponentDiagnostic row =
                    CaptureTopCoverComponentFreshReadOnlyDiagnostics(result, componentPath,
                        result.target_directory);
                freshComponentRows.Add(row.summary);
                freshComponentScanComplete = freshComponentScanComplete &&
                    FreshComponentDiagnosticComplete(row);
            }
            if (topCoverComponentPaths.Count > 0)
                freshComponentScanComplete = freshComponentScanComplete &&
                    freshComponentRows.Count == topCoverComponentPaths.Distinct(
                        StringComparer.OrdinalIgnoreCase).Count();
            diagnostic += "; topCoverFreshScanComplete=" +
                freshComponentScanComplete.ToString().ToLowerInvariant() +
                "; topCoverFreshComponents=" + DiagnosticRows(freshComponentRows);

            List<string> loadOrderRows = new List<string>();
            bool loadOrderScanComplete = topCoverComponentPaths.Count == 0;
            TopCoverLoadOrderDiagnostic candidateUpstreamFirst = null;
            TopCoverLoadOrderDiagnostic rootContext = null;
            if (topCoverComponentPaths.Count > 0 && freshComponentScanComplete)
            {
                candidateUpstreamFirst =
                    CaptureTopCoverAssemblyLoadOrderDiagnostic(result,
                        result.target_directory, "candidate-upstream-first",
                        TopCoverCandidateUpstreamFirstFileNames, null);
                loadOrderRows.Add(candidateUpstreamFirst.summary);
                loadOrderScanComplete =
                    TopCoverLoadOrderDiagnosticComplete(candidateUpstreamFirst);
                rootContext = candidateUpstreamFirst;
                if (loadOrderScanComplete &&
                    (candidateUpstreamFirst.assembly_open_warnings & 64) != 0)
                {
                    TopCoverLoadOrderDiagnostic candidateDownstreamFirst =
                        CaptureTopCoverAssemblyLoadOrderDiagnostic(result,
                            result.target_directory, "candidate-downstream-first",
                            TopCoverCandidateDownstreamFirstFileNames, null);
                    loadOrderRows.Add(candidateDownstreamFirst.summary);
                    loadOrderScanComplete =
                        TopCoverLoadOrderDiagnosticComplete(candidateDownstreamFirst);
                    rootContext = candidateDownstreamFirst;
                }
                if (loadOrderScanComplete &&
                    (rootContext.assembly_open_warnings & 64) == 0 &&
                    RootContextNeedsAnotherCandidate(rootContext))
                {
                    TopCoverLoadOrderDiagnostic doorFrame =
                        CaptureTopCoverAssemblyLoadOrderDiagnostic(result,
                            result.target_directory, "root-plus-door-frame-bottom",
                            TopCoverCandidateUpstreamFirstFileNames,
                            RootDoorFrameCandidateFileNames);
                    loadOrderRows.Add(doorFrame.summary);
                    loadOrderScanComplete = TopCoverLoadOrderDiagnosticComplete(doorFrame);
                    rootContext = doorFrame;
                }
                if (loadOrderScanComplete && RootContextNeedsAnotherCandidate(rootContext))
                {
                    TopCoverLoadOrderDiagnostic rightPartition =
                        CaptureTopCoverAssemblyLoadOrderDiagnostic(result,
                            result.target_directory, "root-plus-right-partition-chain",
                            TopCoverCandidateUpstreamFirstFileNames,
                            RootRightPartitionCandidateFileNames);
                    loadOrderRows.Add(rightPartition.summary);
                    loadOrderScanComplete =
                        TopCoverLoadOrderDiagnosticComplete(rightPartition);
                    rootContext = rightPartition;
                }
                if (loadOrderScanComplete && RootContextNeedsAnotherCandidate(rootContext))
                {
                    string[] externalLinkCandidates = RootDoorFrameCandidateFileNames
                        .Concat(RootRightPartitionCandidateFileNames).ToArray();
                    TopCoverLoadOrderDiagnostic combinedExternal =
                        CaptureTopCoverAssemblyLoadOrderDiagnostic(result,
                            result.target_directory,
                            "root-plus-door-frame-and-right-partition",
                            TopCoverCandidateUpstreamFirstFileNames,
                            externalLinkCandidates);
                    loadOrderRows.Add(combinedExternal.summary);
                    loadOrderScanComplete =
                        TopCoverLoadOrderDiagnosticComplete(combinedExternal);
                    rootContext = combinedExternal;
                }
                if (loadOrderScanComplete && RootContextNeedsAnotherCandidate(rootContext))
                {
                    string[] allKnownCandidates = RootDoorFrameCandidateFileNames
                        .Concat(RootRightPartitionCandidateFileNames)
                        .Concat(RootBaseBodyCandidateFileNames).ToArray();
                    TopCoverLoadOrderDiagnostic allKnown =
                        CaptureTopCoverAssemblyLoadOrderDiagnostic(result,
                            result.target_directory, "root-plus-all-known-basebody",
                            TopCoverCandidateUpstreamFirstFileNames,
                            allKnownCandidates);
                    loadOrderRows.Add(allKnown.summary);
                    loadOrderScanComplete = TopCoverLoadOrderDiagnosticComplete(allKnown);
                }
            }
            diagnostic += "; topCoverLoadOrderScanComplete=" +
                loadOrderScanComplete.ToString().ToLowerInvariant() +
                "; topCoverLoadOrderExperiments=" + DiagnosticRows(loadOrderRows);
            result.known_relocated_warning64_gate =
                KnownRelocatedPrewriteWarningGate(diagnostic);
            Require(diagnostic.StartsWith("scanComplete=true; scanned=75;",
                    StringComparison.Ordinal) &&
                result.known_relocated_warning64_gate &&
                freshComponentScanComplete && loadOrderScanComplete,
                "RELOCATED_BASEPART_NOT_LOADED", 10,
                "the complete 75-document read-only prewrite diagnostic must contain " +
                "either no warning bit 64 or the exact two known V37 relocated documents; " +
                "no reference update or save was attempted; " + diagnostic);
        }

        private static void StabilizeWritableRoot(Result result)
        {
            ISldWorks application = null;
            ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "writable_root_stabilization" };
            result.sessions.Add(session);
            try
            {
                Require(WaitForGlobalCadQuiescence(
                        () => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "WRITABLE_ROOT_SESSION_NOT_QUIESCENT", 10,
                    "global CAD processes did not remain empty before root stabilization");
                application = StartOwnedSolidWorks(result, session);
                Require(application != null, "WRITABLE_SOLIDWORKS_SESSION_INVALID", 10,
                    "could not establish an exact owned SolidWorks 2020 session");
                Require(application.SetCurrentWorkingDirectory(result.target_directory),
                    "WRITABLE_WORKING_DIRECTORY_INVALID", 10,
                    "writable stabilization could not bind the attempt-local working pack");
                int errors = 0;
                int warnings = 0;
                model = application.OpenDoc6(result.target_root_path,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors,
                    ref warnings) as ModelDoc2;
                result.initial_open_errors = errors;
                result.initial_open_warnings = warnings;
                Require(model != null && errors == 0 &&
                    InitialRelocatedWarningAllowed(warnings),
                    "WRITABLE_ROOT_OPEN_GATE_FAILED", 11,
                    "root may become writable only from exact known 0/32 or 0/96 V37 state");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "WRITABLE_ROOT_NOT_ASSEMBLY", 11,
                    "working root did not open as an assembly");
                result.writable_resolve_status = Safe(
                    () => assembly.ResolveAllLightWeightComponents(false), -1);
                HierarchyEvidence hierarchy = CaptureHierarchy(model, result.target_directory);
                result.writable_hierarchy = hierarchy;
                Require((result.writable_resolve_status == 0 ||
                        result.writable_resolve_status == 2) && HierarchyGate(hierarchy),
                    "WRITABLE_RESOLVE_OR_HIERARCHY_GATE_FAILED", 12,
                    "root resolution must return 0/2 and prove exact fully-resolved active hierarchy");
                result.writable_rebuild_succeeded = Safe(() => model.ForceRebuild3(false), false);
                Require(result.writable_rebuild_succeeded, "WRITABLE_FORCE_REBUILD_FAILED", 12,
                    "ForceRebuild3 returned false");
                DependencyEvidence writableDependencies = CaptureDependencies(application,
                    result.target_root_path, result.target_directory);
                result.writable_dependency_gate =
                    writableDependencies.raw_count == ExpectedDependencyRawCount &&
                    writableDependencies.closure_count == ExpectedCadCount &&
                    writableDependencies.all_target_local &&
                    writableDependencies.exact_inventory_set;
                FeatureHealthEvidence writableHealth = CaptureFeatureHealth(model);
                result.writable_known_root_issue_gate = KnownRootIssueGate(writableHealth);
                Require(result.writable_dependency_gate &&
                    result.writable_known_root_issue_gate,
                    "WRITABLE_PRE_SAVE_IDENTITY_GATE_FAILED", 12,
                    "root-only stabilization requires exact local 148/75 dependencies and " +
                    "the unique trusted Reference 51 issue before Save3; raw=" +
                    writableDependencies.raw_count.ToString(CultureInfo.InvariantCulture) +
                    ",closure=" + writableDependencies.closure_count.ToString(
                        CultureInfo.InvariantCulture) +
                    ",allLocal=" + writableDependencies.all_target_local.ToString()
                        .ToLowerInvariant() +
                    ",exactInventory=" + writableDependencies.exact_inventory_set.ToString()
                        .ToLowerInvariant() +
                    ",transientLocks=" + writableDependencies.transient_lock_file_count
                        .ToString(CultureInfo.InvariantCulture) +
                    ",transientLocksValid=" + writableDependencies.transient_lock_files_valid
                        .ToString().ToLowerInvariant() +
                    ",missing=" + DiagnosticRows(writableDependencies.missing_from_closure
                        .Select(path => DiagnosticPath(path, result.target_directory)).ToList()) +
                    ",unexpected=" + DiagnosticRows(writableDependencies.unexpected_in_closure
                        .Select(path => DiagnosticPath(path, result.target_directory)).ToList()) +
                    ",featureTraversal=" + writableHealth.traversal_complete.ToString()
                        .ToLowerInvariant() +
                    ",featureCount=" + writableHealth.feature_count.ToString(
                        CultureInfo.InvariantCulture) +
                    ",featureIssues=" + string.Join("|", writableHealth.issues.Select(issue =>
                        DiagnosticValue(issue.name) + "[" + DiagnosticValue(issue.type) + "]:" +
                        issue.error_code.ToString(CultureInfo.InvariantCulture) + "/" +
                        issue.error_code2.ToString(CultureInfo.InvariantCulture) + "/" +
                        issue.warning.ToString().ToLowerInvariant())));
                int saveErrors = 0;
                int saveWarnings = 0;
                result.writable_save_succeeded = Safe(() => model.Save3(
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors,
                    ref saveWarnings), false);
                result.stabilize_save_errors = saveErrors;
                result.stabilize_save_warnings = saveWarnings;
                result.save_flag_after_stabilize = Safe(() => model.GetSaveFlag(), true);
                Require(result.writable_save_succeeded && saveErrors == 0 && saveWarnings == 0 &&
                    !result.save_flag_after_stabilize,
                    "WRITABLE_SAVE_GATE_FAILED", 13,
                    "Save3 must succeed with errors/warnings 0 and clear the save flag");
            }
            finally
            {
                CloseDocument(application, ref model);
                CloseOwnedSolidWorks(ref application, result, session);
            }
            Require(session.exit_state == "exited" && session.monitors_exit_proven,
                "WRITABLE_SESSION_EXIT_NOT_PROVEN", 14,
                "writable SolidWorks and monitor exits were not exactly proven; sw=" +
                session.exit_state + "; monitorsProven=" + session.monitors_exit_proven +
                "; capturedMonitors=" + session.monitors.Count.ToString(
                    CultureInfo.InvariantCulture) + "; monitorStates=" +
                string.Join(",", session.monitors.Select(row => row.pid.ToString(
                    CultureInfo.InvariantCulture) + ":" + row.exit_state)) +
                "; cleanupErrors=" + string.Join("|", result.processes.cleanup_errors));
            result.root_sha_after_stable = Sha256File(result.target_root_path);
            Require(IsSha256(result.root_sha_after_stable), "STABILIZED_ROOT_HASH_INVALID", 14,
                "stabilized root SHA-256 is unavailable");
        }

        private static void VerifyFreshReadonly(Result result)
        {
            ISldWorks application = null;
            ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence { purpose = "fresh_readonly_root_reopen" };
            result.sessions.Add(session);
            string hashBefore = Sha256File(result.target_root_path);
            try
            {
                Require(WaitForGlobalCadQuiescence(
                        () => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"), 30000, 1000, 200),
                    "READONLY_ROOT_SESSION_NOT_QUIESCENT", 20,
                    "global CAD processes did not remain empty before root read-only reopen");
                application = StartOwnedSolidWorks(result, session);
                Require(application != null, "READONLY_SOLIDWORKS_SESSION_INVALID", 20,
                    "could not establish a fresh exact owned SolidWorks 2020 session");
                Require(application.SetCurrentWorkingDirectory(result.target_directory),
                    "READONLY_WORKING_DIRECTORY_INVALID", 20,
                    "read-only verification could not bind the attempt-local working pack");
                DependencyEvidence dependencies = CaptureDependencies(application,
                    result.target_root_path, result.target_directory);
                result.dependencies = dependencies;
                Require(dependencies.raw_count == ExpectedDependencyRawCount &&
                    dependencies.closure_count == ExpectedCadCount &&
                    dependencies.all_target_local && dependencies.exact_inventory_set,
                    "DEPENDENCY_CLOSURE_GATE_FAILED", 21,
                    "dependency closure must be raw 148 / local exact 75");

                int errors = 0;
                int warnings = 0;
                model = application.OpenDoc6(result.target_root_path,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                result.reopen_errors = errors;
                result.reopen_warnings = warnings;
                string referenceDiagnostic = ExternalReferenceDiagnostic(application);
                if ((warnings & 64) != 0 && !FreshReopenWarningAllowed(errors, warnings))
                {
                    CloseDocument(application, ref model);
                    Try(() => application.CloseAllDocuments(true));
                    int documentsAfterReopenClose;
                    Require(DocumentCacheEmpty(application, out documentsAfterReopenClose),
                        "RELOCATION_DIAGNOSTIC_CACHE_NOT_EMPTY", 22,
                        "root documents remained cached before the post-save read-only relocation diagnostic; count=" +
                        documentsAfterReopenClose.ToString(CultureInfo.InvariantCulture));
                    string relocationDiagnostic = CaptureRelocationDocumentDiagnostics(
                        application, result.target_directory);
                    Require(false, "RELOCATED_BASEPART_NOT_LOADED", 22,
                        "warning 64 was observed on the post-save read-only reopen; " +
                        "no reference update was attempted; refs=" + referenceDiagnostic + "; " +
                        relocationDiagnostic);
                }
                Require(model != null && FreshReopenWarningAllowed(errors, warnings),
                    "READONLY_REOPEN_WARNING_GATE_FAILED", 22,
                    "fresh read-only root reopen must be errors 0 and exact known warning 32/96; errors=" +
                    errors.ToString(CultureInfo.InvariantCulture) + "; warnings=" +
                    warnings.ToString(CultureInfo.InvariantCulture) + "; refs=" +
                    referenceDiagnostic);
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "READONLY_ROOT_NOT_ASSEMBLY", 22,
                    "freshly reopened root is not an assembly");
                result.readonly_resolve_status = Safe(
                    () => assembly.ResolveAllLightWeightComponents(false), -1);
                result.readonly_rebuild_succeeded = Safe(() => model.ForceRebuild3(false), false);
                result.readonly_hierarchy = CaptureHierarchy(model, result.target_directory);
                result.root_feature_health = CaptureFeatureHealth(model);
                result.known_root_issue_gate = KnownRootIssueGate(result.root_feature_health);
                Require((result.readonly_resolve_status == 0 ||
                        result.readonly_resolve_status == 2) &&
                    result.readonly_rebuild_succeeded && HierarchyGate(result.readonly_hierarchy),
                    "READONLY_REBUILD_HIERARCHY_GATE_FAILED", 23,
                    "fresh reopen must rebuild and retain exact 25/158/depth2 resolved hierarchy");
                Require(result.root_feature_health.traversal_complete &&
                    result.root_feature_health.feature_count == ExpectedFeatureCount &&
                    result.known_root_issue_gate, "KNOWN_ROOT_ISSUE_GATE_FAILED", 23,
                    "root feature health must contain only the unique trusted Reference 51 warning");
            }
            finally
            {
                CloseDocument(application, ref model);
                CloseOwnedSolidWorks(ref application, result, session);
            }
            Require(session.exit_state == "exited" && session.monitors_exit_proven,
                "READONLY_SESSION_EXIT_NOT_PROVEN", 24,
                "fresh read-only SolidWorks and monitor exits were not exactly proven; sw=" +
                session.exit_state + "; monitorsProven=" + session.monitors_exit_proven +
                "; capturedMonitors=" + session.monitors.Count.ToString(
                    CultureInfo.InvariantCulture) + "; monitorStates=" +
                string.Join(",", session.monitors.Select(row => row.pid.ToString(
                    CultureInfo.InvariantCulture) + ":" + row.exit_state)) +
                "; cleanupErrors=" + string.Join("|", result.processes.cleanup_errors));
            string hashAfter = Sha256File(result.target_root_path);
            result.readonly_root_hash_unchanged = string.Equals(hashBefore, hashAfter,
                StringComparison.OrdinalIgnoreCase);
            Require(result.readonly_root_hash_unchanged &&
                string.Equals(hashAfter, result.root_sha_after_stable,
                    StringComparison.OrdinalIgnoreCase),
                "READONLY_REOPEN_MUTATED_ROOT", 24,
                "fresh read-only verification changed the stabilized root bytes");
        }

        private static ISldWorks StartOwnedSolidWorks(Result result, SessionEvidence session)
        {
            ISldWorks application = null;
            try
            {
                Require(ProcessIds("SLDWORKS").Count == 0 && ProcessIds("sldProcMon").Count == 0,
                    "CAD_SESSION_NOT_EXCLUSIVE", 10,
                    "a CAD process appeared before owned session creation");
                List<ProcessIdentity> monitorsBefore = ProcessInfoByName("sldProcMon");
                Type type = Type.GetTypeFromProgID("SldWorks.Application.28", true);
                application = Activator.CreateInstance(type) as ISldWorks;
                Require(application != null, "SOLIDWORKS_ACTIVATION_FAILED", 10,
                    "SldWorks.Application.28 returned null");
                Try(() => application.Visible = false);
                Try(() => application.UserControl = false);
                Try(() => application.CommandInProgress = true);
                int pid = Safe(() => application.GetProcessID(), 0);
                ProcessIdentity identity;
                Require(TryCaptureProcessIdentity(pid, "SLDWORKS", out identity) &&
                    SamePath(identity.executable_path, ExpectedSolidWorksExePath) &&
                    string.Equals(identity.executable_sha256, ExpectedSolidWorksExeSha256,
                        StringComparison.OrdinalIgnoreCase),
                    "SOLIDWORKS_PROCESS_IDENTITY_UNPROVEN", 10,
                    "SolidWorks PID/start/path/hash identity could not be proven");
                string revision = Safe(() => application.RevisionNumber(), "");
                Require(revision.StartsWith(ExpectedSolidWorksVersionPrefix,
                    StringComparison.Ordinal), "SOLIDWORKS_VERSION_NOT_2020", 10,
                    "SolidWorks revision must begin with 28.");
                session.sldworks = identity;
                session.revision = revision;
                session.created = !result.processes.baseline_sldworks.Contains(pid);
                Require(session.created && ProcessIds("SLDWORKS").SequenceEqual(new[] { pid }),
                    "SOLIDWORKS_OWNERSHIP_OR_EXCLUSIVITY_INVALID", 10,
                    "created SolidWorks process is not the sole exact session");
                Thread.Sleep(500);
                CaptureOwnedMonitors(session, monitorsBefore);
                return application;
            }
            catch
            {
                if (application != null) CloseOwnedSolidWorks(ref application, result, session);
                throw;
            }
        }

        private static void CloseOwnedSolidWorks(ref ISldWorks application, Result result,
            SessionEvidence session)
        {
            if (session == null) return;
            CaptureOwnedMonitors(session, new List<ProcessIdentity>());
            if (application != null)
            {
                ISldWorks current = application;
                Try(() => current.CloseAllDocuments(true));
                Try(() => current.CommandInProgress = false);
                session.exit_requested = TryAction(() => current.ExitApp());
                Release(current);
                application = null;
            }
            session.exit_state = WaitForIdentityExit(session.sldworks, 10000);
            if (session.exit_state == "unproven")
                session.exit_state = ResolveUnprovenState(
                    () => ExactIdentityState(session.sldworks), 30000, 200);
            if (session.exit_state == "alive_owned")
            {
                if (KillExactIdentity(session.sldworks, result.processes))
                    session.exit_state = WaitForIdentityExit(session.sldworks, 3000);
            }
            else if (session.exit_state == "unproven")
                result.processes.cleanup_errors.Add(
                    "refused to kill unproven SolidWorks PID " + session.sldworks.pid);
            CleanupOwnedMonitors(session, result.processes);
        }

        private static void CleanupOwnedProcesses(Result result)
        {
            foreach (SessionEvidence session in result.sessions)
            {
                if (session.sldworks.pid > 0 && session.exit_state != "exited")
                {
                    string state = ExactIdentityState(session.sldworks);
                    if (state == "unproven")
                        state = ResolveUnprovenState(
                            () => ExactIdentityState(session.sldworks), 30000, 200);
                    if (state == "alive_owned")
                    {
                        if (KillExactIdentity(session.sldworks, result.processes))
                            state = WaitForIdentityExit(session.sldworks, 3000);
                    }
                    else if (state == "unproven")
                        result.processes.cleanup_errors.Add(
                            "refused to kill unproven SolidWorks PID " + session.sldworks.pid);
                    session.exit_state = state;
                }
                CleanupOwnedMonitors(session, result.processes);
            }
        }

        private static void CaptureOwnedMonitors(SessionEvidence session,
            List<ProcessIdentity> before)
        {
            if (session == null || session.sldworks.pid <= 0) return;
            HashSet<string> beforeKeys = new HashSet<string>(before.Select(IdentityKey),
                StringComparer.OrdinalIgnoreCase);
            string pattern = "(?:^|\\s)--ppid=" + session.sldworks.pid.ToString(
                CultureInfo.InvariantCulture) + "(?:\\s|$)";
            foreach (ProcessIdentity current in ProcessInfoByName("sldProcMon"))
            {
                if (beforeKeys.Contains(IdentityKey(current)) ||
                    current.parent_pid != session.sldworks.pid ||
                    !Regex.IsMatch(current.command_line ?? "", pattern,
                        RegexOptions.CultureInvariant) ||
                    !SamePath(current.executable_path, ExpectedMonitorExePath) ||
                    !string.Equals(current.executable_sha256, ExpectedMonitorExeSha256,
                        StringComparison.OrdinalIgnoreCase)) continue;
                if (!session.monitors.Any(row => row.pid == current.pid &&
                    row.start_utc_ticks_value == current.start_utc_ticks_value)) session.monitors.Add(current);
            }
        }

        private static void CleanupOwnedMonitors(SessionEvidence session, ProcessEvidence processes)
        {
            if (session == null) return;
            CaptureOwnedMonitors(session, new List<ProcessIdentity>());
            foreach (ProcessIdentity monitor in session.monitors)
            {
                string state = ExactRecordedMonitorIdentityState(monitor,
                    session.sldworks.pid);
                if (state == "unproven")
                    state = ResolveUnprovenState(
                        () => ExactRecordedMonitorIdentityState(monitor,
                            session.sldworks.pid),
                        30000, 200);
                if (state == "alive_owned")
                {
                    if (KillExactMonitorIdentity(monitor, session.sldworks.pid, processes))
                        state = WaitForMonitorExit(monitor, session.sldworks.pid, 3000);
                    else
                    {
                        state = ResolveUnprovenState(
                            () => ExactRecordedMonitorIdentityState(monitor,
                                session.sldworks.pid),
                            30000, 200);
                        if (state == "alive_owned" &&
                            KillExactMonitorIdentity(monitor, session.sldworks.pid, processes))
                            state = WaitForMonitorExit(monitor, session.sldworks.pid, 3000);
                    }
                }
                if (state == "unproven")
                    state = ResolveUnprovenState(
                        () => ExactRecordedMonitorIdentityState(monitor,
                            session.sldworks.pid),
                        30000, 200);
                if (state == "unproven")
                    processes.cleanup_errors.Add("refused to kill unproven sldProcMon PID " +
                        monitor.pid);
                monitor.exit_state = state;
            }
            session.monitors_exit_proven = session.monitors.All(row =>
                row.exit_state == "exited") && WaitForEmptyProcessSet(
                    () => ProcessIds("sldProcMon"), 10000, 200);
        }

        private static bool KillExactMonitorIdentity(ProcessIdentity monitor, int parentPid,
            ProcessEvidence processes)
        {
            if (ExactRecordedMonitorIdentityState(monitor, parentPid) != "alive_owned")
                return false;
            return KillExactIdentity(monitor, processes);
        }

        private static bool KillExactIdentity(ProcessIdentity identity, ProcessEvidence processes)
        {
            if (ExactIdentityState(identity) != "alive_owned") return false;
            try
            {
                using (Process process = Process.GetProcessById(identity.pid))
                {
                    if (!ProcessObjectMatchesIdentity(process, identity))
                    {
                        processes.cleanup_errors.Add("refused to kill identity changed before stop: " +
                            identity.pid);
                        return false;
                    }
                    process.Kill();
                    process.WaitForExit(3000);
                }
                processes.force_stopped_owned_process_ids.Add(identity.pid);
                return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (Exception exception)
            {
                processes.cleanup_errors.Add(SafeException(exception));
                return false;
            }
        }

        private static bool ProcessObjectMatchesIdentity(Process process,
            ProcessIdentity expected)
        {
            try
            {
                if (process == null || expected == null || process.HasExited ||
                    process.Id != expected.pid || !string.Equals(process.ProcessName,
                        expected.name, StringComparison.OrdinalIgnoreCase) ||
                    process.StartTime.ToUniversalTime().Ticks != expected.start_utc_ticks_value ||
                    process.MainModule == null ||
                    !SamePath(process.MainModule.FileName, expected.executable_path)) return false;
                return string.Equals(Sha256File(process.MainModule.FileName),
                    expected.executable_sha256, StringComparison.OrdinalIgnoreCase);
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
            return WaitForVerifiedExit(
                () => ExactRecordedMonitorIdentityState(identity, parentPid), timeoutMs, 200);
        }

        private static string WaitForVerifiedExit(Func<string> stateProbe, int timeoutMs,
            int pollMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            string state = "unproven";
            do
            {
                state = stateProbe == null ? "unproven" : stateProbe();
                if (state == "exited") return state;
                if (DateTime.UtcNow >= deadline) return state;
                Thread.Sleep(Math.Max(1, pollMilliseconds));
            }
            while (true);
        }

        private static string ResolveUnprovenState(Func<string> stateProbe, int timeoutMs,
            int pollMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            string state = "unproven";
            do
            {
                state = stateProbe == null ? "unproven" : stateProbe();
                if (state != "unproven") return state;
                if (DateTime.UtcNow >= deadline) return state;
                Thread.Sleep(Math.Max(1, pollMilliseconds));
            }
            while (true);
        }

        private static bool WaitForEmptyProcessSet(Func<List<int>> processIdsProbe,
            int timeoutMs, int pollMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                List<int> processIds = processIdsProbe == null ? null : processIdsProbe();
                if (processIds != null && processIds.Count == 0) return true;
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(Math.Max(1, pollMilliseconds));
            }
            while (true);
        }

        private static bool WaitForGlobalCadQuiescence(
            Func<List<int>> sldworksProbe, Func<List<int>> monitorProbe,
            int timeoutMs, int quietWindowMs, int pollMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            DateTime? emptySince = null;
            do
            {
                List<int> sldworks = null;
                List<int> monitors = null;
                try
                {
                    sldworks = sldworksProbe == null ? null : sldworksProbe();
                    monitors = monitorProbe == null ? null : monitorProbe();
                }
                catch
                {
                    emptySince = null;
                }
                DateTime now = DateTime.UtcNow;
                if (sldworks != null && monitors != null &&
                    sldworks.Count == 0 && monitors.Count == 0)
                {
                    if (!emptySince.HasValue) emptySince = now;
                    if ((now - emptySince.Value).TotalMilliseconds >=
                        Math.Max(0, quietWindowMs)) return true;
                }
                else
                {
                    emptySince = null;
                }
                if (now >= deadline) return false;
                Thread.Sleep(Math.Max(1, pollMilliseconds));
            }
            while (true);
        }

        private static string GlobalCadProcessSnapshot()
        {
            try
            {
                List<ProcessIdentity> rows = ProcessInfoByName("SLDWORKS")
                    .Concat(ProcessInfoByName("sldProcMon"))
                    .OrderBy(row => row.name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.pid)
                    .ToList();
                return rows.Count == 0 ? "none" : string.Join("|", rows.Select(row =>
                    row.name + ":pid=" + row.pid.ToString(CultureInfo.InvariantCulture) +
                    ",parent=" + row.parent_pid.ToString(CultureInfo.InvariantCulture) +
                    ",start=" + row.start_utc_ticks_value.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception exception)
            {
                return "snapshot-failed:" + SafeException(exception);
            }
        }

        private static string ExactIdentityState(ProcessIdentity expected)
        {
            if (expected == null || expected.pid <= 0 || expected.start_utc_ticks_value <= 0 ||
                string.IsNullOrWhiteSpace(expected.executable_path) ||
                !IsSha256(expected.executable_sha256)) return "unproven";
            ProcessIdentity current;
            CaptureIdentityResult captured = CaptureProcessIdentity(expected.pid,
                expected.name, out current);
            if (captured == CaptureIdentityResult.Exited) return "exited";
            if (captured != CaptureIdentityResult.Captured) return "unproven";
            return current.start_utc_ticks_value == expected.start_utc_ticks_value &&
                SamePath(current.executable_path, expected.executable_path) &&
                string.Equals(current.executable_sha256, expected.executable_sha256,
                    StringComparison.OrdinalIgnoreCase) ? "alive_owned" : "unproven";
        }

        private static string ExactMonitorIdentityState(ProcessIdentity expected, int parentPid)
        {
            string state = ExactIdentityState(expected);
            if (state != "alive_owned") return state;
            ProcessIdentity live = ProcessInfoByName("sldProcMon").FirstOrDefault(row =>
                row.pid == expected.pid &&
                    row.start_utc_ticks_value == expected.start_utc_ticks_value);
            string pattern = "(?:^|\\s)--ppid=" + parentPid.ToString(
                CultureInfo.InvariantCulture) + "(?:\\s|$)";
            return live != null && live.parent_pid == parentPid &&
                Regex.IsMatch(live.command_line ?? "", pattern, RegexOptions.CultureInvariant) &&
                SamePath(live.executable_path, ExpectedMonitorExePath) &&
                string.Equals(live.executable_sha256, ExpectedMonitorExeSha256,
                    StringComparison.OrdinalIgnoreCase) ? "alive_owned" : "unproven";
        }

        private static string ExactRecordedMonitorIdentityState(ProcessIdentity expected,
            int parentPid)
        {
            if (!RecordedMonitorOwnershipGate(expected, parentPid)) return "unproven";
            return ExactIdentityState(expected);
        }

        private static bool RecordedMonitorOwnershipGate(ProcessIdentity expected,
            int parentPid)
        {
            string pattern = "(?:^|\\s)--ppid=" + parentPid.ToString(
                CultureInfo.InvariantCulture) + "(?:\\s|$)";
            return expected != null && expected.parent_pid == parentPid &&
                string.Equals(expected.name, "sldProcMon", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(expected.command_line ?? "", pattern,
                    RegexOptions.CultureInvariant) &&
                SamePath(expected.executable_path, ExpectedMonitorExePath) &&
                string.Equals(expected.executable_sha256, ExpectedMonitorExeSha256,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void CaptureFinalProcessGate(Result result)
        {
            result.processes.final_sldworks = ProcessIds("SLDWORKS");
            result.processes.final_sldprocmon = ProcessIds("sldProcMon");
            result.processes.final_gate = result.processes.final_sldworks.Count == 0 &&
                result.processes.final_sldprocmon.Count == 0 &&
                result.sessions.All(row => row.exit_state == "exited" &&
                row.monitors_exit_proven) && result.processes.cleanup_errors.Count == 0;
        }

        private static HierarchyEvidence CaptureHierarchy(ModelDoc2 model, string cadDirectory)
        {
            HierarchyEvidence output = new HierarchyEvidence { traversal_complete = true };
            Component2 root = null;
            try
            {
                Configuration configuration = model.GetActiveConfiguration() as Configuration;
                root = configuration == null ? null : configuration.GetRootComponent3(true) as Component2;
                Release(configuration);
                if (root == null)
                {
                    output.traversal_complete = false;
                    return output;
                }
                object[] top = ObjectArray(Safe(() => root.GetChildren(), null));
                output.top_level_component_count = top.Length;
                foreach (object value in top)
                {
                    Component2 component = value as Component2;
                    CaptureComponentRecursive(component, 0, cadDirectory, output);
                    Release(component);
                    if (!output.traversal_complete) break;
                }
            }
            catch
            {
                output.traversal_complete = false;
            }
            finally
            {
                Release(root);
            }
            return output;
        }

        private static void CaptureComponentRecursive(Component2 component, int depth,
            string cadDirectory, HierarchyEvidence output)
        {
            if (component == null || depth > 20 || output.recursive_component_count > 5000)
            {
                output.traversal_complete = false;
                return;
            }
            output.recursive_component_count++;
            output.max_depth = Math.Max(output.max_depth, depth);
            int suppression = Safe(() => component.GetSuppression(), -1);
            string path = Safe(() => component.GetPathName(), "");
            bool suppressed = suppression == (int)swComponentSuppressionState_e.swComponentSuppressed;
            if (!suppressed)
            {
                output.active_component_count++;
                if (suppression != (int)swComponentSuppressionState_e.swComponentFullyResolved &&
                    suppression != (int)swComponentSuppressionState_e.swComponentResolved)
                    output.unresolved_active_component_count++;
            }
            if (suppression == (int)swComponentSuppressionState_e.swComponentLightweight ||
                suppression == (int)swComponentSuppressionState_e.swComponentFullyLightweight)
                output.lightweight_component_count++;
            if (suppression == (int)swComponentSuppressionState_e.swComponentInternalIdMismatch)
                output.internal_id_mismatch_count++;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                !IsUnder(path, cadDirectory)) output.nonlocal_or_missing_path_count++;
            object[] children = ObjectArray(Safe(() => component.GetChildren(), null));
            foreach (object value in children)
            {
                Component2 child = value as Component2;
                CaptureComponentRecursive(child, depth + 1, cadDirectory, output);
                Release(child);
                if (!output.traversal_complete) return;
            }
        }

        private static bool HierarchyGate(HierarchyEvidence hierarchy)
        {
            return hierarchy != null && hierarchy.traversal_complete &&
                hierarchy.top_level_component_count == ExpectedTopLevelComponents &&
                hierarchy.recursive_component_count == ExpectedRecursiveComponents &&
                hierarchy.max_depth == ExpectedHierarchyMaxDepth &&
                hierarchy.unresolved_active_component_count == 0 &&
                hierarchy.lightweight_component_count == 0 &&
                hierarchy.internal_id_mismatch_count == 0 &&
                hierarchy.nonlocal_or_missing_path_count == 0;
        }

        private static DependencyEvidence CaptureDependencies(ISldWorks application,
            string rootPath, string cadDirectory)
        {
            DependencyEvidence output = new DependencyEvidence();
            List<string> raw = Strings(Safe(() => application.GetDocumentDependencies2(
                rootPath, true, true, false), null));
            output.raw_count = raw.Count;
            HashSet<string> closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in raw)
            {
                if (!Path.IsPathRooted(value)) continue;
                string full = Path.GetFullPath(value);
                if (IsAssembly(full) || IsPart(full)) closure.Add(full);
            }
            closure.Add(Path.GetFullPath(rootPath));
            output.closure_count = closure.Count;
            output.paths = closure.OrderBy(value => value,
                StringComparer.OrdinalIgnoreCase).ToList();
            output.all_target_local = closure.All(path => IsUnder(path, cadDirectory) &&
                File.Exists(path));
            List<string> inventoryPaths = CaptureFlatCadTree(cadDirectory,
                "DEPENDENCY_INVENTORY_FILE_SYSTEM_INVALID");
            output.exact_inventory_set = DependencyInventorySetGate(closure,
                inventoryPaths, out output.transient_lock_file_count,
                out output.transient_lock_files_valid,
                out output.missing_from_closure, out output.unexpected_in_closure);
            return output;
        }

        private static bool DependencyInventorySetGate(HashSet<string> closure,
            IEnumerable<string> inventoryPaths, out int transientLockFileCount,
            out bool transientLockFilesValid, out List<string> missingFromClosure,
            out List<string> unexpectedInClosure)
        {
            HashSet<string> normalizedClosure = new HashSet<string>(
                (closure ?? new HashSet<string>()).Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> inventory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> lockFiles = new List<string>();
            foreach (string value in inventoryPaths ?? Enumerable.Empty<string>())
            {
                string full = Path.GetFullPath(value);
                string name = Path.GetFileName(full);
                if (name.StartsWith("~$", StringComparison.Ordinal)) lockFiles.Add(full);
                else inventory.Add(full);
            }
            transientLockFileCount = lockFiles.Count;
            transientLockFilesValid = lockFiles.Count <= normalizedClosure.Count &&
                lockFiles.All(lockPath =>
                {
                    string name = Path.GetFileName(lockPath);
                    string targetName = name.Length > 2 ? name.Substring(2) : "";
                    string targetPath = Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(lockPath) ?? "", targetName));
                    return (IsPart(targetPath) || IsAssembly(targetPath)) &&
                        inventory.Contains(targetPath) &&
                        normalizedClosure.Contains(targetPath);
                });
            missingFromClosure = inventory.Except(normalizedClosure,
                StringComparer.OrdinalIgnoreCase).OrderBy(value => value,
                    StringComparer.OrdinalIgnoreCase).ToList();
            unexpectedInClosure = normalizedClosure.Except(inventory,
                StringComparer.OrdinalIgnoreCase).OrderBy(value => value,
                    StringComparer.OrdinalIgnoreCase).ToList();
            return transientLockFilesValid && normalizedClosure.SetEquals(inventory);
        }

        private static string ExternalReferenceDiagnostic(ISldWorks application)
        {
            try
            {
                object[] documents = ObjectArray(application == null ? null :
                    Safe(() => application.GetDocuments(), null));
                List<string> rows = new List<string>();
                foreach (object value in documents)
                {
                    ModelDoc2 document = value as ModelDoc2;
                    if (document == null) continue;
                    string path = Safe(() => document.GetPathName(), "");
                    int modelCount = Safe(() => document.ListExternalFileReferencesCount2(), -1);
                    int extensionCount = Safe(
                        () => document.Extension.ListExternalFileReferencesCount(), -1);
                    int count = Math.Max(modelCount, extensionCount);
                    if (count != 0)
                        rows.Add((string.IsNullOrWhiteSpace(path) ? "<unnamed>" :
                            Path.GetFileName(path)) + ":" + count.ToString(
                                CultureInfo.InvariantCulture));
                }
                return "docs=" + documents.Length.ToString(CultureInfo.InvariantCulture) +
                    "; nonzero=" + (rows.Count == 0 ? "none" : string.Join(",", rows));
            }
            catch (Exception exception)
            {
                return "diagnostic-error=" + SafeException(exception);
            }
        }

        private static string CaptureRelocationDocumentDiagnostics(ISldWorks application,
            string cadDirectory)
        {
            List<string> ignoredComponentPaths;
            return CaptureRelocationDocumentDiagnostics(application, cadDirectory,
                out ignoredComponentPaths);
        }

        private static string CaptureRelocationDocumentDiagnostics(ISldWorks application,
            string cadDirectory, out List<string> topCoverComponentPaths)
        {
            bool ignoredWarning64;
            return CaptureRelocationDocumentDiagnostics(application, cadDirectory,
                out topCoverComponentPaths, out ignoredWarning64);
        }

        private static string CaptureRelocationDocumentDiagnostics(ISldWorks application,
            string cadDirectory, out List<string> topCoverComponentPaths,
            out bool warning64Observed)
        {
            topCoverComponentPaths = new List<string>();
            List<string> warning64Documents = new List<string>();
            List<string> documentFaults = new List<string>();
            List<string> featureIssues = new List<string>();
            List<string> linkFeatures = new List<string>();
            List<string> assemblyComponentRows = new List<string>();
            bool assemblyComponentScanComplete = true;
            List<string> paths = CaptureFlatCadTree(cadDirectory,
                "RELOCATION_DIAGNOSTIC_FILE_SYSTEM_INVALID")
                .OrderBy(path => IsPart(path) ? 0 :
                    (string.Equals(Path.GetFileName(path), RootFileName,
                        StringComparison.OrdinalIgnoreCase) ? 2 : 1))
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
            int scanned = 0;
            bool scanComplete = true;
            foreach (string path in paths)
            {
                int documentsBefore;
                if (!DocumentCacheEmpty(application, out documentsBefore))
                {
                    scanComplete = false;
                    documentFaults.Add("cache-before:" + Path.GetFileName(path) + ":" +
                        documentsBefore.ToString(CultureInfo.InvariantCulture));
                    break;
                }
                ModelDoc2 model = null;
                int errors = 0;
                int warnings = 0;
                bool rebuilt = false;
                bool readOnly = false;
                bool traversalComplete = true;
                int featureCount = 0;
                try
                {
                    int type = IsAssembly(path) ?
                        (int)swDocumentTypes_e.swDocASSEMBLY :
                        (int)swDocumentTypes_e.swDocPART;
                    model = application.OpenDoc6(path, type,
                        (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                            swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                        ref warnings) as ModelDoc2;
                    scanned++;
                    if ((warnings & 64) != 0)
                        warning64Documents.Add(Path.GetFileName(path) + ":" +
                            errors.ToString(CultureInfo.InvariantCulture) + "/" +
                            warnings.ToString(CultureInfo.InvariantCulture));
                    if (model == null)
                    {
                        documentFaults.Add("open-null:" + Path.GetFileName(path) + ":" +
                            errors.ToString(CultureInfo.InvariantCulture) + "/" +
                            warnings.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Try(() => model.ShowFeatureErrorDialog = false);
                        readOnly = Safe(() => model.IsOpenedReadOnly(), false);
                        rebuilt = Safe(() => model.ForceRebuild3(false), false);
                        CaptureRelocationFeatureDiagnostics(model, Path.GetFileName(path),
                            cadDirectory, featureIssues, linkFeatures,
                            ref traversalComplete, ref featureCount);
                        if ((warnings & 64) != 0 && IsAssembly(path) &&
                            string.Equals(Path.GetFileName(path), TopCoverAssemblyFileName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            CaptureRelocationAssemblyComponentDiagnostics(application, model,
                                path, cadDirectory, assemblyComponentRows,
                                topCoverComponentPaths, ref assemblyComponentScanComplete);
                        }
                        if (!readOnly || !rebuilt || !traversalComplete)
                            documentFaults.Add("observation:" + Path.GetFileName(path) +
                                ":readonly=" + readOnly.ToString().ToLowerInvariant() +
                                ":rebuilt=" + rebuilt.ToString().ToLowerInvariant() +
                                ":traversal=" + traversalComplete.ToString().ToLowerInvariant() +
                                ":features=" + featureCount.ToString(
                                    CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception exception)
                {
                    documentFaults.Add("exception:" + Path.GetFileName(path) + ":" +
                        DiagnosticValue(SafeException(exception)));
                }
                finally
                {
                    CloseDocument(application, ref model);
                    Try(() => application.CloseAllDocuments(true));
                }
                int documentsAfter;
                if (!DocumentCacheEmpty(application, out documentsAfter))
                {
                    scanComplete = false;
                    documentFaults.Add("cache-after:" + Path.GetFileName(path) + ":" +
                        documentsAfter.ToString(CultureInfo.InvariantCulture));
                    break;
                }
            }
            scanComplete = scanComplete && paths.Count == ExpectedCadCount &&
                scanned == ExpectedCadCount && assemblyComponentScanComplete;
            warning64Observed = warning64Documents.Count != 0;
            return "scanComplete=" + scanComplete.ToString().ToLowerInvariant() +
                "; scanned=" + scanned.ToString(CultureInfo.InvariantCulture) +
                "; warning64Docs=" + DiagnosticRows(warning64Documents) +
                "; documentFaults=" + DiagnosticRows(documentFaults) +
                "; featureIssues=" + DiagnosticRows(featureIssues) +
                "; linkFeatures=" + DiagnosticRows(linkFeatures) +
                "; assemblyComponentDiagnostics=" + DiagnosticRows(assemblyComponentRows);
        }

        private static void CaptureRelocationFeatureDiagnostics(ModelDoc2 model,
            string documentName, string cadDirectory, List<string> issues,
            List<string> linkFeatures, ref bool traversalComplete, ref int featureCount)
        {
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { traversalComplete = false; return; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureRelocationFeatureRecursive(feature, documentName, "", cadDirectory,
                    issues, linkFeatures, ref traversalComplete, ref featureCount, 0);
                if (!traversalComplete)
                {
                    Release(feature);
                    return;
                }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { traversalComplete = false; }
                Release(feature);
                feature = next;
            }
            if (feature != null) traversalComplete = false;
        }

        private static void CaptureRelocationFeatureRecursive(Feature feature,
            string documentName, string parentPath, string cadDirectory,
            List<string> issues, List<string> linkFeatures, ref bool traversalComplete,
            ref int featureCount, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || featureCount > 20000)
            {
                traversalComplete = false;
                return;
            }
            featureCount++;
            bool warning = false;
            int errorCode;
            int errorCode2;
            bool suppressed;
            string name;
            string type;
            Feature child;
            try
            {
                errorCode = feature.GetErrorCode();
                errorCode2 = feature.GetErrorCode2(out warning);
                suppressed = feature.IsSuppressed();
                name = feature.Name ?? "";
                type = feature.GetTypeName2() ?? "";
                child = feature.GetFirstSubFeature() as Feature;
            }
            catch
            {
                traversalComplete = false;
                return;
            }
            string featurePath = string.IsNullOrWhiteSpace(parentPath) ? name :
                parentPath + "/" + name;
            if (!suppressed && (errorCode > 0 || errorCode2 > 0 || warning))
                issues.Add(DiagnosticValue(documentName) + ">" +
                    DiagnosticValue(featurePath) + "[" + DiagnosticValue(type) + "]:" +
                    errorCode.ToString(CultureInfo.InvariantCulture) + "/" +
                    errorCode2.ToString(CultureInfo.InvariantCulture) + "/" +
                    warning.ToString().ToLowerInvariant());
            if (IsExternalLinkFeatureType(type))
                linkFeatures.Add(CaptureRelocationExternalLinkFeature(feature, documentName,
                    featurePath, type, cadDirectory));
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureRelocationFeatureRecursive(child, documentName, featurePath,
                    cadDirectory, issues, linkFeatures, ref traversalComplete,
                    ref featureCount, depth + 1);
                if (!traversalComplete)
                {
                    Release(child);
                    return;
                }
                Feature next = null;
                try { next = child.GetNextSubFeature() as Feature; }
                catch { traversalComplete = false; }
                Release(child);
                child = next;
            }
            if (child != null) traversalComplete = false;
        }

        private static bool IsExternalLinkFeatureType(string type)
        {
            return string.Equals(type, "BaseBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "SplitBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "MirrorStock", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Reference", StringComparison.OrdinalIgnoreCase);
        }

        private static string CaptureRelocationExternalLinkFeature(Feature feature,
            string documentName, string featurePath, string type, string cadDirectory)
        {
            object modelPaths = null;
            object componentPaths = null;
            object features = null;
            object dataTypes = null;
            object statuses = null;
            object entities = null;
            object featureComponents = null;
            int configOption = 0;
            string configName = "";
            try
            {
                feature.ListExternalFileReferences2(out modelPaths, out componentPaths,
                    out features, out dataTypes, out statuses, out entities,
                    out featureComponents, out configOption, out configName);
                Array modelArray = modelPaths as Array;
                Array componentArray = componentPaths as Array;
                Array featureArray = features as Array;
                Array statusArray = statuses as Array;
                int count = MaximumArrayLength(modelArray, componentArray, featureArray,
                    statusArray);
                List<string> references = new List<string>();
                for (int index = 0; index < count; index++)
                {
                    string modelPath = ArrayText(modelArray, index);
                    string componentPath = ArrayText(componentArray, index);
                    references.Add("m=" + DiagnosticPath(modelPath, cadDirectory) +
                        ",c=" + DiagnosticPath(componentPath, cadDirectory) +
                        ",f=" + DiagnosticValue(ArrayText(featureArray, index)) +
                        ",s=" + ArrayInteger(statusArray, index).ToString(
                            CultureInfo.InvariantCulture) +
                        ",state=" + DiagnosticReferenceState(modelPath, componentPath,
                            cadDirectory));
                }
                return DiagnosticValue(documentName) + ">" +
                    DiagnosticValue(featurePath) + "[" + DiagnosticValue(type) +
                    "]:config=" + configOption.ToString(CultureInfo.InvariantCulture) +
                    "/" + DiagnosticValue(configName) + ":refs=" +
                    (references.Count == 0 ? "none" : string.Join("~", references));
            }
            catch (Exception exception)
            {
                return DiagnosticValue(documentName) + ">" +
                    DiagnosticValue(featurePath) + "[" + DiagnosticValue(type) +
                    "]:capture-error=" + DiagnosticValue(SafeException(exception));
            }
            finally
            {
                ReleaseComArrayItems(entities);
                ReleaseComArrayItems(featureComponents);
            }
        }

        private static string DiagnosticReferenceState(string modelPath,
            string componentPath, string cadDirectory)
        {
            if (string.IsNullOrWhiteSpace(modelPath) ||
                string.IsNullOrWhiteSpace(componentPath)) return "missing-path";
            return DiagnosticPathLocal(modelPath, cadDirectory) &&
                DiagnosticPathLocal(componentPath, cadDirectory) ? "local" : "external";
        }

        private static bool DiagnosticPathLocal(string path, string cadDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
            try { return IsUnder(Path.GetFullPath(path), cadDirectory); }
            catch { return false; }
        }

        private static string DiagnosticPath(string path, string cadDirectory)
        {
            if (string.IsNullOrWhiteSpace(path)) return "<missing>";
            try
            {
                string full = Path.GetFullPath(path);
                return DiagnosticPathLocal(full, cadDirectory) ?
                    "local/" + DiagnosticValue(Path.GetFileName(full)) :
                    "external/" + DiagnosticValue(full);
            }
            catch { return DiagnosticValue(path); }
        }

        private static string DiagnosticValue(string value)
        {
            return (value ?? "").Replace("\r", " ").Replace("\n", " ")
                .Replace(";", ",").Replace("|", "/");
        }

        private static string DiagnosticRows(List<string> rows)
        {
            return rows == null || rows.Count == 0 ? "none" : string.Join("|", rows);
        }

        private static int MaximumArrayLength(params Array[] arrays)
        {
            return arrays == null ? 0 : arrays.Where(value => value != null)
                .Select(value => value.Length).DefaultIfEmpty(0).Max();
        }

        private static string ArrayText(Array values, int index)
        {
            return values == null || index < 0 || index >= values.Length ? "" :
                ValueText(values.GetValue(index));
        }

        private static int ArrayInteger(Array values, int index)
        {
            if (values == null || index < 0 || index >= values.Length) return -1;
            try { return Convert.ToInt32(values.GetValue(index), CultureInfo.InvariantCulture); }
            catch { return -1; }
        }

        private static void ReleaseComArrayItems(object raw)
        {
            Array values = raw as Array;
            if (values == null) return;
            foreach (object value in values) Release(value);
        }

        private static void CaptureRelocationAssemblyComponentDiagnostics(
            ISldWorks application, ModelDoc2 assemblyModel, string assemblyPath,
            string cadDirectory, List<string> rows, List<string> componentPaths,
            ref bool scanComplete)
        {
            bool traversalComplete = true;
            int instanceCount = 0;
            int activeNonlocalCount = 0;
            int activeUnresolvedCount = 0;
            HashSet<string> uniquePaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            Component2 root = null;
            Configuration configuration = null;
            try
            {
                AssemblyDoc assembly = assemblyModel as AssemblyDoc;
                if (application == null || assembly == null)
                {
                    traversalComplete = false;
                    return;
                }
                configuration = assemblyModel.GetActiveConfiguration() as Configuration;
                root = configuration == null ? null :
                    configuration.GetRootComponent3(true) as Component2;
                if (root == null)
                {
                    traversalComplete = false;
                    return;
                }
                object[] children = ObjectArray(Safe(() => root.GetChildren(), null));
                foreach (object value in children)
                {
                    Component2 component = value as Component2;
                    CaptureRelocationComponentRecursive(component, assemblyPath, cadDirectory,
                        rows, uniquePaths, ref traversalComplete, ref instanceCount,
                        ref activeNonlocalCount, ref activeUnresolvedCount, 0);
                    Release(component);
                    if (!traversalComplete) break;
                }
            }
            catch (Exception exception)
            {
                traversalComplete = false;
                rows.Add(DiagnosticValue(Path.GetFileName(assemblyPath)) +
                    ">component-tree:capture-error=" +
                    DiagnosticValue(SafeException(exception)));
            }
            finally
            {
                Release(root);
                Release(configuration);
                componentPaths.AddRange(uniquePaths.OrderBy(Path.GetFileName,
                    StringComparer.OrdinalIgnoreCase));
                bool complete = TopCoverComponentDiagnosticComplete(traversalComplete,
                    instanceCount, uniquePaths.Count, activeNonlocalCount,
                    activeUnresolvedCount);
                scanComplete = scanComplete && complete;
                rows.Add(DiagnosticValue(Path.GetFileName(assemblyPath)) +
                    ">component-tree:complete=" + complete.ToString().ToLowerInvariant() +
                    ",instances=" + instanceCount.ToString(CultureInfo.InvariantCulture) +
                    ",uniquePaths=" + uniquePaths.Count.ToString(CultureInfo.InvariantCulture) +
                    ",activeNonlocal=" + activeNonlocalCount.ToString(
                        CultureInfo.InvariantCulture) +
                    ",activeUnresolved=" + activeUnresolvedCount.ToString(
                        CultureInfo.InvariantCulture));
            }
        }

        private static void CaptureRelocationComponentRecursive(Component2 component,
            string assemblyPath, string cadDirectory, List<string> rows,
            HashSet<string> uniquePaths, ref bool traversalComplete, ref int instanceCount,
            ref int activeNonlocalCount, ref int activeUnresolvedCount, int depth)
        {
            if (component == null || depth > 20 || instanceCount > 5000)
            {
                traversalComplete = false;
                return;
            }
            instanceCount++;
            string instance = "";
            string name = "";
            string path = "";
            string referencedConfiguration = "";
            int suppression = -1;
            bool suppressed = true;
            bool cachedModelAvailable = false;
            string cachedModelPath = "";
            object[] children;
            try
            {
                instance = component.GetSelectByIDString() ?? "";
                name = component.Name2 ?? "";
                path = component.GetPathName() ?? "";
                referencedConfiguration = component.ReferencedConfiguration ?? "";
                suppression = component.GetSuppression();
                suppressed = component.IsSuppressed();
                ModelDoc2 cachedModel = null;
                try
                {
                    cachedModel = component.GetModelDoc2() as ModelDoc2;
                    cachedModelAvailable = cachedModel != null;
                    if (cachedModel != null)
                        cachedModelPath = Safe(() => cachedModel.GetPathName(), "");
                }
                finally
                {
                    ReleaseOneComReference(cachedModel);
                }
                children = ObjectArray(component.GetChildren());
            }
            catch (Exception exception)
            {
                traversalComplete = false;
                rows.Add(DiagnosticValue(Path.GetFileName(assemblyPath)) +
                    ">component:capture-error=" +
                    DiagnosticValue(SafeException(exception)));
                return;
            }

            bool resolved = suppression ==
                (int)swComponentSuppressionState_e.swComponentFullyResolved ||
                suppression == (int)swComponentSuppressionState_e.swComponentResolved;
            bool local = !string.IsNullOrWhiteSpace(path) && File.Exists(path) &&
                IsUnder(path, cadDirectory);
            if (!suppressed)
            {
                if (!local) activeNonlocalCount++;
                if (!resolved || !cachedModelAvailable) activeUnresolvedCount++;
                if (local && (IsPart(path) || IsAssembly(path)))
                    uniquePaths.Add(Path.GetFullPath(path));
            }
            rows.Add(DiagnosticValue(Path.GetFileName(assemblyPath)) +
                ">component:instance=" + DiagnosticValue(instance) +
                ",name=" + DiagnosticValue(name) +
                ",path=" + DiagnosticPath(path, cadDirectory) +
                ",configuration=" + DiagnosticValue(referencedConfiguration) +
                ",suppression=" + suppression.ToString(CultureInfo.InvariantCulture) +
                ",suppressed=" + suppressed.ToString().ToLowerInvariant() +
                ",resolved=" + resolved.ToString().ToLowerInvariant() +
                ",cachedModel=" + cachedModelAvailable.ToString().ToLowerInvariant() +
                ",cachedPath=" + DiagnosticPath(cachedModelPath, cadDirectory));

            foreach (object value in children)
            {
                Component2 child = value as Component2;
                CaptureRelocationComponentRecursive(child, assemblyPath, cadDirectory, rows,
                    uniquePaths, ref traversalComplete, ref instanceCount,
                    ref activeNonlocalCount, ref activeUnresolvedCount, depth + 1);
                Release(child);
                if (!traversalComplete) return;
            }
        }

        private static bool TopCoverComponentDiagnosticComplete(bool traversalComplete,
            int instanceCount, int uniquePathCount, int activeNonlocalCount,
            int activeUnresolvedCount)
        {
            return traversalComplete && instanceCount > 0 && uniquePathCount > 0 &&
                activeNonlocalCount == 0 && activeUnresolvedCount == 0;
        }

        private static bool TopCoverComponentPathSetGate(
            IEnumerable<string> componentPaths, string cadDirectory)
        {
            HashSet<string> expected = new HashSet<string>(
                TopCoverCandidateUpstreamFirstFileNames.Skip(1).Select(fileName =>
                    Path.GetFullPath(Path.Combine(cadDirectory, fileName))),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> actual = new HashSet<string>(
                (componentPaths ?? Enumerable.Empty<string>()).Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            return expected.Count == 6 && actual.Count == 6 && actual.SetEquals(expected);
        }

        private static FreshComponentDiagnostic
            CaptureTopCoverComponentFreshReadOnlyDiagnostics(Result result,
                string componentPath, string cadDirectory)
        {
            FreshComponentDiagnostic output = new FreshComponentDiagnostic
                { path = componentPath };
            output.path_local = !string.IsNullOrWhiteSpace(componentPath) &&
                File.Exists(componentPath) && IsUnder(componentPath, cadDirectory) &&
                (IsPart(componentPath) || IsAssembly(componentPath));
            if (!output.path_local)
            {
                output.summary = DiagnosticValue(Path.GetFileName(componentPath)) +
                    ":path-local=false";
                return output;
            }
            string hashBefore = Sha256File(componentPath);
            ISldWorks application = null;
            ModelDoc2 model = null;
            SessionEvidence session = new SessionEvidence
                { purpose = "top_cover_component_fresh_readonly_diagnostic" };
            result.sessions.Add(session);
            List<string> issues = new List<string>();
            List<string> links = new List<string>();
            int featureCount = 0;
            try
            {
                Require(WaitForGlobalCadQuiescence(
                        () => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"),
                        30000, 1000, 200),
                    "TOP_COVER_FRESH_SESSION_NOT_QUIESCENT", 10,
                    "global CAD processes did not remain empty before the next " +
                    "fresh read-only component session; snapshot=" +
                    GlobalCadProcessSnapshot());
                application = StartOwnedSolidWorks(result, session);
                Require(application.SetCurrentWorkingDirectory(cadDirectory),
                    "TOP_COVER_FRESH_WORKING_DIRECTORY_INVALID", 10,
                    "fresh component diagnostic could not bind its local pack");
                int documentsBefore;
                output.cache_before_empty =
                    DocumentCacheEmpty(application, out documentsBefore);
                if (!output.cache_before_empty) return output;
                int errors = 0;
                int warnings = 0;
                int type = IsAssembly(componentPath) ?
                    (int)swDocumentTypes_e.swDocASSEMBLY :
                    (int)swDocumentTypes_e.swDocPART;
                model = application.OpenDoc6(componentPath, type,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                    ref warnings) as ModelDoc2;
                output.opened = model != null;
                output.open_errors = errors;
                output.open_warnings = warnings;
                if (model != null)
                {
                    output.read_only = Safe(() => model.IsOpenedReadOnly(), false);
                    output.rebuilt = Safe(() => model.ForceRebuild3(false), false);
                    bool traversalComplete = true;
                    CaptureRelocationFeatureDiagnostics(model,
                        Path.GetFileName(componentPath), cadDirectory, issues, links,
                        ref traversalComplete, ref featureCount);
                    output.traversal_complete = traversalComplete;
                    output.feature_count = featureCount;
                }
            }
            catch (Exception exception)
            {
                output.fault = SafeException(exception);
            }
            finally
            {
                CloseDocument(application, ref model);
                if (application != null) Try(() => application.CloseAllDocuments(true));
                int documentsAfter;
                output.cache_after_empty = application != null &&
                    DocumentCacheEmpty(application, out documentsAfter);
                CloseOwnedSolidWorks(ref application, result, session);
                output.session_exit_proven = session.exit_state == "exited" &&
                    session.monitors_exit_proven;
                output.file_unchanged = File.Exists(componentPath) &&
                    string.Equals(hashBefore, Sha256File(componentPath),
                        StringComparison.OrdinalIgnoreCase);
                output.summary = DiagnosticValue(Path.GetFileName(componentPath)) +
                    ":opened=" + output.opened.ToString().ToLowerInvariant() +
                    ",readonly=" + output.read_only.ToString().ToLowerInvariant() +
                    ",errors=" + output.open_errors.ToString(CultureInfo.InvariantCulture) +
                    ",warnings=" + output.open_warnings.ToString(CultureInfo.InvariantCulture) +
                    ",rebuilt=" + output.rebuilt.ToString().ToLowerInvariant() +
                    ",traversal=" + output.traversal_complete.ToString().ToLowerInvariant() +
                    ",features=" + output.feature_count.ToString(CultureInfo.InvariantCulture) +
                    ",cacheBefore=" + output.cache_before_empty.ToString().ToLowerInvariant() +
                    ",cacheAfter=" + output.cache_after_empty.ToString().ToLowerInvariant() +
                    ",exit=" + output.session_exit_proven.ToString().ToLowerInvariant() +
                    ",unchanged=" + output.file_unchanged.ToString().ToLowerInvariant() +
                    ",issues=" + DiagnosticRows(issues) +
                    ",links=" + DiagnosticRows(links) +
                    ",fault=" + DiagnosticValue(output.fault);
            }
            return output;
        }

        private static bool FreshComponentDiagnosticComplete(
            FreshComponentDiagnostic output)
        {
            return output != null && output.path_local && output.cache_before_empty &&
                output.opened && output.open_errors == 0 &&
                (output.open_warnings & 64) == 0 && output.read_only && output.rebuilt &&
                output.traversal_complete && output.cache_after_empty &&
                output.session_exit_proven && output.file_unchanged &&
                string.IsNullOrWhiteSpace(output.fault);
        }

        private static TopCoverLoadOrderDiagnostic
            CaptureTopCoverAssemblyLoadOrderDiagnostic(Result result,
                string cadDirectory, string label, IEnumerable<string> preloadFileNames,
                IEnumerable<string> additionalPreloadFileNames)
        {
            TopCoverLoadOrderDiagnostic output = new TopCoverLoadOrderDiagnostic
                { label = label ?? "" };
            List<string> expectedNames = (preloadFileNames ?? Enumerable.Empty<string>())
                .ToList();
            List<string> additionalNames = (additionalPreloadFileNames ??
                Enumerable.Empty<string>()).ToList();
            List<string> preloadRows = new List<string>();
            List<string> additionalRows = new List<string>();
            List<string> componentRows = new List<string>();
            List<string> componentPaths = new List<string>();
            List<string> issues = new List<string>();
            List<string> links = new List<string>();
            List<ModelDoc2> preloadModels = new List<ModelDoc2>();
            Dictionary<string, string> hashesBefore = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            ISldWorks application = null;
            ModelDoc2 assemblyModel = null;
            ModelDoc2 rootModel = null;
            SessionEvidence session = new SessionEvidence
                { purpose = "top_cover_load_order_" + output.label };
            result.sessions.Add(session);
            try
            {
                Require(expectedNames.Count == 7 && expectedNames.Distinct(
                        StringComparer.OrdinalIgnoreCase).Count() == 7,
                    "TOP_COVER_LOAD_ORDER_CONTRACT_INVALID", 10,
                    "top-cover load-order diagnostic requires seven unique preload parts");
                Require(WaitForGlobalCadQuiescence(
                        () => ProcessIds("SLDWORKS"),
                        () => ProcessIds("sldProcMon"),
                        30000, 1000, 200),
                    "TOP_COVER_LOAD_ORDER_SESSION_NOT_QUIESCENT", 10,
                    "global CAD processes did not remain empty before the top-cover " +
                    "load-order diagnostic; snapshot=" + GlobalCadProcessSnapshot());
                application = StartOwnedSolidWorks(result, session);
                Require(application.SetCurrentWorkingDirectory(cadDirectory),
                    "TOP_COVER_LOAD_ORDER_WORKING_DIRECTORY_INVALID", 10,
                    "load-order diagnostic could not bind its local pack");
                int documentsBefore;
                output.cache_before_empty =
                    DocumentCacheEmpty(application, out documentsBefore);
                Require(output.cache_before_empty,
                    "TOP_COVER_LOAD_ORDER_CACHE_NOT_EMPTY", 10,
                    "top-cover load-order diagnostic session did not start with an empty cache");

                foreach (string fileName in expectedNames)
                {
                    string path = Path.GetFullPath(Path.Combine(cadDirectory, fileName));
                    Require(File.Exists(path) && IsUnder(path, cadDirectory) && IsPart(path) &&
                        !hashesBefore.ContainsKey(path),
                        "TOP_COVER_LOAD_ORDER_PRELOAD_PATH_INVALID", 10,
                        "top-cover preload file is missing, duplicated or outside the working pack: " +
                        fileName);
                    hashesBefore[path] = Sha256File(path);
                    int errors = 0;
                    int warnings = 0;
                    ModelDoc2 preload = application.OpenDoc6(path,
                        (int)swDocumentTypes_e.swDocPART,
                        (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                            swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                        ref warnings) as ModelDoc2;
                    bool readOnly = preload != null &&
                        Safe(() => preload.IsOpenedReadOnly(), false);
                    preloadRows.Add(DiagnosticValue(fileName) + ":opened=" +
                        (preload != null).ToString().ToLowerInvariant() +
                        ",errors=" + errors.ToString(CultureInfo.InvariantCulture) +
                        ",warnings=" + warnings.ToString(CultureInfo.InvariantCulture) +
                        ",warning64=" + ((warnings & 64) != 0).ToString().ToLowerInvariant() +
                        ",readonly=" + readOnly.ToString().ToLowerInvariant());
                    Require(preload != null && errors == 0 && readOnly,
                        "TOP_COVER_LOAD_ORDER_PRELOAD_OPEN_FAILED", 10,
                        "top-cover preload part did not open read-only with zero errors: " +
                        fileName);
                    preloadModels.Add(preload);
                }
                output.preload_count = preloadRows.Count;
                output.preload_complete = output.preload_count == expectedNames.Count;

                string assemblyPath = Path.GetFullPath(Path.Combine(cadDirectory,
                    TopCoverAssemblyFileName));
                Require(File.Exists(assemblyPath) && IsUnder(assemblyPath, cadDirectory) &&
                    IsAssembly(assemblyPath) && !hashesBefore.ContainsKey(assemblyPath),
                    "TOP_COVER_LOAD_ORDER_ASSEMBLY_PATH_INVALID", 10,
                    "top-cover assembly is missing or outside the working pack");
                hashesBefore[assemblyPath] = Sha256File(assemblyPath);
                int assemblyErrors = 0;
                int assemblyWarnings = 0;
                assemblyModel = application.OpenDoc6(assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref assemblyErrors,
                    ref assemblyWarnings) as ModelDoc2;
                output.assembly_opened = assemblyModel != null;
                output.assembly_open_errors = assemblyErrors;
                output.assembly_open_warnings = assemblyWarnings;
                output.assembly_read_only = assemblyModel != null &&
                    Safe(() => assemblyModel.IsOpenedReadOnly(), false);
                if (assemblyModel != null)
                {
                    output.assembly_rebuilt = Safe(() =>
                        assemblyModel.ForceRebuild3(false), false);
                    bool featureTraversalComplete = true;
                    int featureCount = 0;
                    CaptureRelocationFeatureDiagnostics(assemblyModel,
                        TopCoverAssemblyFileName, cadDirectory, issues, links,
                        ref featureTraversalComplete, ref featureCount);
                    bool componentScanComplete = true;
                    CaptureRelocationAssemblyComponentDiagnostics(application,
                        assemblyModel, assemblyPath, cadDirectory, componentRows,
                        componentPaths, ref componentScanComplete);
                    output.assembly_component_complete = componentScanComplete &&
                        TopCoverComponentPathSetGate(componentPaths, cadDirectory);
                    output.traversal_complete = featureTraversalComplete &&
                        output.assembly_component_complete;
                }
                Require(output.assembly_opened && output.assembly_open_errors == 0 &&
                    output.assembly_read_only && output.assembly_rebuilt &&
                    output.assembly_component_complete && output.traversal_complete,
                    "TOP_COVER_LOAD_ORDER_ASSEMBLY_DIAGNOSTIC_INCOMPLETE", 10,
                    "top-cover assembly did not complete its read-only load-order diagnostic");

                Require(additionalNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    additionalNames.Count,
                    "ROOT_CONTEXT_ADDITIONAL_PRELOAD_CONTRACT_INVALID", 10,
                    "root-context candidate preload names must be unique");
                output.additional_preload_expected = additionalNames.Count;
                foreach (string fileName in additionalNames)
                {
                    string path = Path.GetFullPath(Path.Combine(cadDirectory, fileName));
                    int documentType = IsAssembly(path) ?
                        (int)swDocumentTypes_e.swDocASSEMBLY :
                        (int)swDocumentTypes_e.swDocPART;
                    Require(File.Exists(path) && IsUnder(path, cadDirectory) &&
                        (IsPart(path) || IsAssembly(path)) &&
                        !hashesBefore.ContainsKey(path),
                        "ROOT_CONTEXT_ADDITIONAL_PRELOAD_PATH_INVALID", 10,
                        "root-context candidate preload is missing, duplicated or outside " +
                        "the working pack: " + fileName);
                    hashesBefore[path] = Sha256File(path);
                    int errors = 0;
                    int warnings = 0;
                    ModelDoc2 preload = application.OpenDoc6(path, documentType,
                        (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                            swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errors,
                        ref warnings) as ModelDoc2;
                    bool readOnly = preload != null &&
                        Safe(() => preload.IsOpenedReadOnly(), false);
                    additionalRows.Add(DiagnosticValue(fileName) + ":opened=" +
                        (preload != null).ToString().ToLowerInvariant() +
                        ",errors=" + errors.ToString(CultureInfo.InvariantCulture) +
                        ",warnings=" + warnings.ToString(CultureInfo.InvariantCulture) +
                        ",warning64=" + ((warnings & 64) != 0).ToString().ToLowerInvariant() +
                        ",readonly=" + readOnly.ToString().ToLowerInvariant());
                    Require(preload != null && errors == 0 && readOnly,
                        "ROOT_CONTEXT_ADDITIONAL_PRELOAD_OPEN_FAILED", 10,
                        "root-context candidate did not open read-only with zero errors: " +
                        fileName);
                    preloadModels.Add(preload);
                }
                output.additional_preload_count = additionalRows.Count;
                output.additional_preload_complete =
                    output.additional_preload_count == output.additional_preload_expected;

                string rootPath = Path.GetFullPath(Path.Combine(cadDirectory, RootFileName));
                Require(File.Exists(rootPath) && IsUnder(rootPath, cadDirectory) &&
                    IsAssembly(rootPath) && !hashesBefore.ContainsKey(rootPath),
                    "TOP_COVER_LOAD_ORDER_ROOT_PATH_INVALID", 10,
                    "root assembly is missing or outside the working pack");
                hashesBefore[rootPath] = Sha256File(rootPath);
                int rootErrors = 0;
                int rootWarnings = 0;
                rootModel = application.OpenDoc6(rootPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref rootErrors,
                    ref rootWarnings) as ModelDoc2;
                output.root_opened = rootModel != null;
                output.root_open_errors = rootErrors;
                output.root_open_warnings = rootWarnings;
                output.root_read_only = rootModel != null &&
                    Safe(() => rootModel.IsOpenedReadOnly(), false);
                if (rootModel != null)
                {
                    output.root_rebuilt = Safe(() => rootModel.ForceRebuild3(false), false);
                    output.root_hierarchy_gate = HierarchyGate(
                        CaptureHierarchy(rootModel, cadDirectory));
                    output.root_feature_gate = KnownRootIssueGate(
                        CaptureFeatureHealth(rootModel));
                }
            }
            catch (Exception exception)
            {
                output.fault = SafeException(exception) + "; cadSnapshot=" +
                    GlobalCadProcessSnapshot();
            }
            finally
            {
                if (application != null) Try(() => application.CloseAllDocuments(true));
                Release(rootModel);
                rootModel = null;
                Release(assemblyModel);
                assemblyModel = null;
                foreach (ModelDoc2 preload in preloadModels) Release(preload);
                int documentsAfter;
                output.cache_after_empty = application != null &&
                    DocumentCacheEmpty(application, out documentsAfter);
                CloseOwnedSolidWorks(ref application, result, session);
                output.session_exit_proven = session.exit_state == "exited" &&
                    session.monitors_exit_proven;
                output.files_unchanged = hashesBefore.Count == expectedNames.Count +
                    additionalNames.Count + 2 &&
                    hashesBefore.All(row => File.Exists(row.Key) &&
                        string.Equals(row.Value, Sha256File(row.Key),
                            StringComparison.OrdinalIgnoreCase));
                output.summary = DiagnosticValue(output.label) +
                    ":preloads=" + DiagnosticRows(preloadRows) +
                    ",additionalPreloads=" + DiagnosticRows(additionalRows) +
                    ",assemblyOpened=" + output.assembly_opened.ToString().ToLowerInvariant() +
                    ",errors=" + output.assembly_open_errors.ToString(
                        CultureInfo.InvariantCulture) +
                    ",warnings=" + output.assembly_open_warnings.ToString(
                        CultureInfo.InvariantCulture) +
                    ",warning64=" + ((output.assembly_open_warnings & 64) != 0)
                        .ToString().ToLowerInvariant() +
                    ",readonly=" + output.assembly_read_only.ToString().ToLowerInvariant() +
                    ",rebuilt=" + output.assembly_rebuilt.ToString().ToLowerInvariant() +
                    ",componentComplete=" + output.assembly_component_complete
                        .ToString().ToLowerInvariant() +
                    ",traversal=" + output.traversal_complete.ToString().ToLowerInvariant() +
                    ",rootOpened=" + output.root_opened.ToString().ToLowerInvariant() +
                    ",rootErrors=" + output.root_open_errors.ToString(
                        CultureInfo.InvariantCulture) +
                    ",rootWarnings=" + output.root_open_warnings.ToString(
                        CultureInfo.InvariantCulture) +
                    ",rootWarning64=" + ((output.root_open_warnings & 64) != 0)
                        .ToString().ToLowerInvariant() +
                    ",rootReadonly=" + output.root_read_only.ToString().ToLowerInvariant() +
                    ",rootRebuilt=" + output.root_rebuilt.ToString().ToLowerInvariant() +
                    ",rootHierarchy=" + output.root_hierarchy_gate.ToString()
                        .ToLowerInvariant() +
                    ",rootFeature=" + output.root_feature_gate.ToString()
                        .ToLowerInvariant() +
                    ",cacheBefore=" + output.cache_before_empty.ToString().ToLowerInvariant() +
                    ",cacheAfter=" + output.cache_after_empty.ToString().ToLowerInvariant() +
                    ",exit=" + output.session_exit_proven.ToString().ToLowerInvariant() +
                    ",unchanged=" + output.files_unchanged.ToString().ToLowerInvariant() +
                    ",issues=" + DiagnosticRows(issues) +
                    ",links=" + DiagnosticRows(links) +
                    ",components=" + DiagnosticRows(componentRows) +
                    ",fault=" + DiagnosticValue(output.fault);
            }
            return output;
        }

        private static bool TopCoverLoadOrderDiagnosticComplete(
            TopCoverLoadOrderDiagnostic output)
        {
            return output != null && output.cache_before_empty &&
                output.preload_complete && output.preload_count == 7 &&
                output.additional_preload_complete &&
                output.additional_preload_count == output.additional_preload_expected &&
                output.assembly_opened && output.assembly_open_errors == 0 &&
                output.assembly_read_only && output.assembly_rebuilt &&
                output.assembly_component_complete && output.traversal_complete &&
                output.root_opened && output.root_open_errors == 0 &&
                output.root_read_only && output.root_rebuilt &&
                output.root_hierarchy_gate && output.root_feature_gate &&
                output.cache_after_empty && output.session_exit_proven &&
                output.files_unchanged && string.IsNullOrWhiteSpace(output.fault);
        }

        private static bool RootContextNeedsAnotherCandidate(
            TopCoverLoadOrderDiagnostic output)
        {
            return TopCoverLoadOrderDiagnosticComplete(output) &&
                (output.root_open_warnings & 64) != 0;
        }

        private static FeatureHealthEvidence CaptureFeatureHealth(ModelDoc2 model)
        {
            FeatureHealthEvidence output = new FeatureHealthEvidence
                { traversal_complete = true };
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { output.traversal_complete = false; return output; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureFeatureRecursive(feature, output, 0);
                if (!output.traversal_complete)
                {
                    Release(feature);
                    return output;
                }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { output.traversal_complete = false; }
                Release(feature);
                feature = next;
            }
            if (feature != null) output.traversal_complete = false;
            return output;
        }

        private static void CaptureFeatureRecursive(Feature feature, FeatureHealthEvidence output,
            int depth)
        {
            if (feature == null) return;
            if (depth > 30 || output.feature_count > 20000)
            {
                output.traversal_complete = false;
                return;
            }
            output.feature_count++;
            bool warning = false;
            int errorCode;
            int errorCode2;
            bool suppressed;
            string name;
            string type;
            Feature child;
            try
            {
                errorCode = feature.GetErrorCode();
                errorCode2 = feature.GetErrorCode2(out warning);
                suppressed = feature.IsSuppressed();
                name = feature.Name;
                type = feature.GetTypeName2();
                child = feature.GetFirstSubFeature() as Feature;
            }
            catch
            {
                output.traversal_complete = false;
                return;
            }
            if (!suppressed && (errorCode > 0 || errorCode2 > 0 || warning))
                output.issues.Add(new FeatureIssueEvidence
                {
                    name = name,
                    type = type,
                    error_code = errorCode,
                    error_code2 = errorCode2,
                    warning = warning,
                    depth = depth
                });
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureRecursive(child, output, depth + 1);
                if (!output.traversal_complete)
                {
                    Release(child);
                    return;
                }
                Feature next = null;
                try { next = child.GetNextSubFeature() as Feature; }
                catch { output.traversal_complete = false; }
                Release(child);
                child = next;
            }
            if (child != null) output.traversal_complete = false;
        }

        private static bool KnownRootIssueGate(FeatureHealthEvidence health)
        {
            if (health == null || !health.traversal_complete || health.issues.Count != 1)
                return false;
            FeatureIssueEvidence issue = health.issues[0];
            return string.Equals(issue.name, KnownIssueName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(issue.type, KnownIssueType, StringComparison.OrdinalIgnoreCase) &&
                issue.error_code == 51 && issue.error_code2 == 51 && issue.warning;
        }

        private static void ValidateFinalInventories(Result result)
        {
            List<string> targetFiles = CaptureFlatCadTree(result.target_directory,
                "FINAL_TARGET_FILE_SYSTEM_INVALID");
            Require(targetFiles.Count == ExpectedCadCount &&
                targetFiles.Count(IsAssembly) == ExpectedAssemblyCount &&
                targetFiles.Count(IsPart) == ExpectedPartCount,
                "FINAL_TARGET_INVENTORY_COUNT_INVALID", 30,
                "stabilized target must remain flat 75 = 22 SLDASM + 53 SLDPRT");
            Dictionary<string, string> targetHashes = targetFiles.ToDictionary(Path.GetFileName,
                Sha256File, StringComparer.OrdinalIgnoreCase);
            result.non_root_exact_source = result.source_hashes.Where(row =>
                    !string.Equals(row.Key, RootFileName, StringComparison.OrdinalIgnoreCase))
                .All(row => targetHashes.ContainsKey(row.Key) &&
                    string.Equals(targetHashes[row.Key], row.Value,
                        StringComparison.OrdinalIgnoreCase));
            Require(result.non_root_exact_source && targetHashes.Count == ExpectedCadCount &&
                targetHashes.ContainsKey(RootFileName) &&
                string.Equals(targetHashes[RootFileName], result.root_sha_after_stable,
                    StringComparison.OrdinalIgnoreCase),
                "NONROOT_OR_ROOT_MUTATION_GATE_FAILED", 30,
                "all 74 non-root files must retain source hashes and only root may stabilize");
            result.non_root_file_count = 74;
            result.target_inventory_digest = InventoryDigest(targetFiles.Select(Snapshot));
            Require(IsSha256(result.target_inventory_digest), "TARGET_INVENTORY_DIGEST_INVALID", 30,
                "stabilized target inventory digest is invalid");
            VerifySourceUnchanged(result);
            Require(result.source_unchanged, "TRUSTED_SOURCE_CHANGED", 30,
                "trusted V37 source changed during seed stage");
            Require(result.dependencies.closure_count == ExpectedCadCount &&
                result.dependencies.all_target_local && result.dependencies.exact_inventory_set,
                "FINAL_DEPENDENCY_GATE_INVALID", 30,
                "fresh-readonly dependency closure is not the exact final target inventory");
        }

        private static void VerifySourceUnchanged(Result result)
        {
            if (result.source_hashes == null || result.source_hashes.Count != ExpectedCadCount ||
                !Directory.Exists(result.source_directory))
            {
                result.source_unchanged = false;
                return;
            }
            try
            {
                List<string> files = CaptureFlatCadTree(result.source_directory,
                    "SOURCE_FINAL_FILE_SYSTEM_INVALID");
                result.source_unchanged = files.Count == ExpectedCadCount && files.All(path =>
                {
                    string name = Path.GetFileName(path);
                    string expected;
                    return result.source_hashes.TryGetValue(name, out expected) &&
                        string.Equals(Sha256File(path), expected,
                            StringComparison.OrdinalIgnoreCase);
                }) && string.Equals(InventoryDigest(files.Select(Snapshot)),
                    result.source_inventory_digest, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                result.source_unchanged = false;
            }
        }

        private static bool InitialRelocatedWarningAllowed(int warnings)
        {
            return (warnings == 32 || warnings == 96) && (warnings & 1) == 0 &&
                (warnings & ~(32 | 64)) == 0;
        }

        private static bool KnownRelocatedPrewriteWarningGate(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)) return false;
            string exactKnown = "; warning64Docs=" + TopCoverAssemblyFileName +
                ":0/96|" + RootFileName + ":0/96;";
            return diagnostic.Contains("; warning64Docs=none;") ||
                diagnostic.Contains(exactKnown);
        }

        private static bool RelocationBasePartDiagnosticRequired(int warnings)
        {
            return InitialRelocatedWarningAllowed(warnings) && (warnings & 64) != 0;
        }

        private static bool DocumentCacheEmpty(ISldWorks application, out int documentCount)
        {
            documentCount = -1;
            if (application == null) return false;
            object first = null;
            object documents = null;
            try
            {
                documentCount = application.GetDocumentCount();
                first = application.GetFirstDocument();
                documents = application.GetDocuments();
                Array values = documents as Array;
                return documentCount == 0 && first == null &&
                    (values == null || values.Length == 0);
            }
            catch
            {
                return false;
            }
            finally
            {
                Release(first);
                ReleaseComArrayItems(documents);
                Release(documents);
            }
        }

        private static void CloseDocument(ISldWorks application, ref ModelDoc2 model)
        {
            if (model == null) return;
            ModelDoc2 current = model;
            string title = Safe(() => current.GetTitle(), "");
            Release(current);
            model = null;
            if (application != null && !string.IsNullOrWhiteSpace(title))
                Try(() => application.CloseDoc(title));
        }

        private static void CommitEvidenceAndReceipt(Result result)
        {
            Require(result.success && result.processes.final_gate && result.source_unchanged &&
                result.non_root_exact_source && result.known_root_issue_gate,
                "SUCCESS_EVIDENCE_PRECONDITION_INVALID", 41,
                "success evidence cannot commit before every seed gate passes");
            Require(!ResultContainsReceiptIdentity(result), "EVIDENCE_RECEIPT_HASH_CYCLE", 41,
                "success evidence must not contain receipt path/hash/committed identity");
            Directory.CreateDirectory(Path.GetDirectoryName(result.evidence_path));
            Directory.CreateDirectory(Path.GetDirectoryName(result.receipt_path));
            AssertPathChainNoReparse(Path.GetDirectoryName(result.evidence_path),
                result.attempt_directory, "EVIDENCE_COMMIT_REPARSE_PATH");
            AssertPathChainNoReparse(Path.GetDirectoryName(result.receipt_path),
                result.attempt_directory, "RECEIPT_COMMIT_REPARSE_PATH");
            AssertAuthorizationStillValid(result, "evidence_create_new_boundary");

            byte[] evidenceBytes = JsonBytes(result);
            WriteCreateNewAndReread(result.evidence_path, evidenceBytes,
                () => result.evidence_created_by_this_run = true);
            string evidenceSha = Sha256File(result.evidence_path);
            Dictionary<string, object> liveEvidence = ReadJsonObject(result.evidence_path);
            Require(string.Equals(Text(liveEvidence, "evidence_commitment_sha256"),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(EvidenceCommitmentDigest(liveEvidence),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase),
                "EVIDENCE_COMMITMENT_REREAD_INVALID", 41,
                "final evidence bytes do not preserve the shared semantic commitment");

            Dictionary<string, object> receipt = BuildReceipt(result, evidenceSha);
            Require(ExactKeys(receipt, SeedReceiptKeys), "SEED_RECEIPT_KEYS_INVALID", 42,
                "seed receipt does not have the exact shared key set");
            AssertAuthorizationStillValid(result, "receipt_create_new_boundary");
            byte[] receiptBytes = JsonBytes(receipt);
            WriteCreateNewAndReread(result.receipt_path, receiptBytes,
                () => result.receipt_created_by_this_run = true);
            ValidateCommittedPair(result);
        }

        private static Dictionary<string, object> BuildReceipt(Result result, string evidenceSha)
        {
            Dictionary<string, object> receipt = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", ReceiptSchema },
                { "phase", ExecutionPhase },
                { "success", true },
                { "completedAt", result.completed_at_utc },
                { "taskId", result.task_id },
                { "taskRevision", result.task_revision },
                { "taskDigest", result.task_digest },
                { "requestDigest", result.request_digest },
                { "leaseId", result.lease_id },
                { "leaseExpiresAt", result.lease_expires_at },
                { "attempt", result.attempt },
                { "planSha256", result.plan_sha256 },
                { "authorizationId", result.authorization_id },
                { "authorizationSha256", result.authorization_sha256 },
                { "authorizationJsonBase64", result.authorization_json_base64 },
                { "authorizationIssuedAt", result.authorization_issued_at },
                { "authorizationExpiresAt", result.authorization_expires_at },
                { "recipeId", ExpectedRecipeId },
                { "recipeDigest", ExpectedRecipeDigest },
                { "toolId", ToolId },
                { "toolSourceNormalizedSha256", result.tool_source_normalized_sha256 },
                { "toolExecutableSha256", result.tool_executable_sha256 },
                { "evidencePath", EvidenceRelativePath },
                { "evidenceSha256", evidenceSha },
                { "evidenceCommitmentSha256", result.evidence_commitment_sha256 },
                { "sourceInventoryDigest", result.source_inventory_digest },
                { "targetInventoryDigest", result.target_inventory_digest },
                { "nonRootFileCount", result.non_root_file_count },
                { "nonRootExactSource", result.non_root_exact_source },
                { "rootFileName", RootFileName },
                { "rootShaBefore", result.root_sha_before },
                { "rootShaAfterStable", result.root_sha_after_stable },
                { "initialOpenErrors", result.initial_open_errors },
                { "initialOpenWarnings", result.initial_open_warnings },
                { "stabilizeSaveErrors", result.stabilize_save_errors },
                { "stabilizeSaveWarnings", result.stabilize_save_warnings },
                { "reopenErrors", result.reopen_errors },
                { "reopenWarnings", result.reopen_warnings },
                { "dependencyClosureCount", result.dependencies.closure_count },
                { "dependenciesAllTargetLocal", result.dependencies.all_target_local },
                { "knownRootIssueGate", result.known_root_issue_gate },
                { "predecessorReceiptSha256", "" }
            };
            return receipt;
        }

        private static void ValidateCommittedPair(Result result)
        {
            Require(File.Exists(result.evidence_path) && File.Exists(result.receipt_path) &&
                !HasReparsePoint(result.evidence_path) && !HasReparsePoint(result.receipt_path) &&
                FileLinkCount(result.evidence_path) == 1 && FileLinkCount(result.receipt_path) == 1,
                "COMMITTED_PAIR_FILE_IDENTITY_INVALID", 43,
                "evidence/receipt pair is missing, linked or reparse-backed");
            Dictionary<string, object> evidence = ReadJsonObject(result.evidence_path);
            Dictionary<string, object> receipt = ReadJsonObject(result.receipt_path);
            Require(ValidatePairObjects(evidence, receipt, Sha256File(result.evidence_path)),
                "COMMITTED_PAIR_CONTENT_INVALID", 43,
                "receipt does not exactly bind live evidence SHA and semantic commitment");
            Require(EvidenceMatchesResult(evidence, result) && ReceiptMatchesResult(receipt,
                    result), "COMMITTED_PAIR_RESULT_BINDING_INVALID", 43,
                "committed pair fields drifted from the completed seed result");
            AuthorizationBinding historical;
            byte[] authorizationBytes;
            try { authorizationBytes = Convert.FromBase64String(Text(receipt,
                "authorizationJsonBase64")); }
            catch { authorizationBytes = new byte[0]; }
            Dictionary<string, object> authorization = authorizationBytes.Length > 0 ?
                ReadJsonObjectBytes(authorizationBytes) : null;
            Require(authorization != null &&
                string.Equals(Sha256Bytes(authorizationBytes), Text(receipt,
                    "authorizationSha256"), StringComparison.OrdinalIgnoreCase) &&
                AuthorizationContractValid(authorization, result, DateTime.UtcNow,
                    Text(receipt, "completedAt"), out historical) &&
                historical.authorization_id == Text(receipt, "authorizationId") &&
                ExactKeys(receipt, SeedReceiptKeys) && Text(receipt, "schema") == ReceiptSchema &&
                Text(receipt, "phase") == ExecutionPhase && Bool(receipt, "success") &&
                Text(receipt, "predecessorReceiptSha256") == "",
                "COMMITTED_PAIR_AUTHORIZATION_INVALID", 43,
                "receipt historical authorization is not self-contained and exact");
        }

        private static bool EvidenceMatchesResult(Dictionary<string, object> evidence, Result result)
        {
            return evidence != null && Text(evidence, "schema") == EvidenceSchema &&
                Text(evidence, "phase") == ExecutionPhase && Bool(evidence, "success") &&
                Text(evidence, "status") == "NATIVE_SEED_888X14_STABILIZED" &&
                Text(evidence, "task_id") == result.task_id &&
                Number(evidence, "task_revision") == result.task_revision &&
                string.Equals(Text(evidence, "task_digest"), result.task_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(evidence, "request_digest"), result.request_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                Number(evidence, "attempt") == result.attempt &&
                string.Equals(Text(evidence, "plan_sha256"), result.plan_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(evidence, "recipe_id") == ExpectedRecipeId &&
                string.Equals(Text(evidence, "recipe_digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(evidence, "authorization_id") == result.authorization_id &&
                string.Equals(Text(evidence, "authorization_sha256"),
                    result.authorization_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(evidence, "authorization_seed_inventory_digest"),
                    result.source_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(evidence, "source_inventory_digest"),
                    result.source_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(evidence, "target_inventory_digest"),
                    result.target_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                Text(evidence, "root_file_name") == RootFileName &&
                string.Equals(Text(evidence, "root_sha_before"), ExpectedRootSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(evidence, "root_sha_after_stable"),
                    result.root_sha_after_stable, StringComparison.OrdinalIgnoreCase) &&
                Bool(evidence, "non_root_exact_source") &&
                Bool(evidence, "known_root_issue_gate") &&
                string.Equals(Text(evidence, "evidence_commitment_sha256"),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ReceiptMatchesResult(Dictionary<string, object> receipt, Result result)
        {
            return ExactKeys(receipt, SeedReceiptKeys) &&
                Text(receipt, "schema") == ReceiptSchema &&
                Text(receipt, "phase") == ExecutionPhase && Bool(receipt, "success") &&
                Text(receipt, "completedAt") == result.completed_at_utc &&
                Text(receipt, "taskId") == result.task_id &&
                Number(receipt, "taskRevision") == result.task_revision &&
                string.Equals(Text(receipt, "taskDigest"), result.task_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "requestDigest"), result.request_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(receipt, "leaseId") == result.lease_id &&
                Text(receipt, "leaseExpiresAt") == result.lease_expires_at &&
                Number(receipt, "attempt") == result.attempt &&
                string.Equals(Text(receipt, "planSha256"), result.plan_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(receipt, "authorizationId") == result.authorization_id &&
                string.Equals(Text(receipt, "authorizationSha256"),
                    result.authorization_sha256, StringComparison.OrdinalIgnoreCase) &&
                Text(receipt, "authorizationJsonBase64") == result.authorization_json_base64 &&
                Text(receipt, "authorizationIssuedAt") == result.authorization_issued_at &&
                Text(receipt, "authorizationExpiresAt") == result.authorization_expires_at &&
                Text(receipt, "recipeId") == ExpectedRecipeId &&
                string.Equals(Text(receipt, "recipeDigest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) && Text(receipt, "toolId") == ToolId &&
                string.Equals(Text(receipt, "toolSourceNormalizedSha256"),
                    result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "toolExecutableSha256"),
                    result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                Text(receipt, "evidencePath") == EvidenceRelativePath &&
                IsSha256(Text(receipt, "evidenceSha256")) &&
                string.Equals(Text(receipt, "evidenceCommitmentSha256"),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "sourceInventoryDigest"),
                    result.source_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "targetInventoryDigest"),
                    result.target_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                Number(receipt, "nonRootFileCount") == 74 &&
                Bool(receipt, "nonRootExactSource") &&
                Text(receipt, "rootFileName") == RootFileName &&
                string.Equals(Text(receipt, "rootShaBefore"), ExpectedRootSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "rootShaAfterStable"), result.root_sha_after_stable,
                    StringComparison.OrdinalIgnoreCase) &&
                Number(receipt, "initialOpenErrors") == 0 &&
                (Number(receipt, "initialOpenWarnings") == 32 ||
                    Number(receipt, "initialOpenWarnings") == 96) &&
                Number(receipt, "stabilizeSaveErrors") == 0 &&
                Number(receipt, "stabilizeSaveWarnings") == 0 &&
                Number(receipt, "reopenErrors") == 0 &&
                (Number(receipt, "reopenWarnings") == 32 ||
                    Number(receipt, "reopenWarnings") == 96) &&
                Number(receipt, "dependencyClosureCount") == ExpectedCadCount &&
                Bool(receipt, "dependenciesAllTargetLocal") &&
                Bool(receipt, "knownRootIssueGate") &&
                Text(receipt, "predecessorReceiptSha256") == "";
        }

        private static bool ValidatePairObjects(Dictionary<string, object> evidence,
            Dictionary<string, object> receipt, string evidenceSha)
        {
            if (evidence == null || receipt == null || !ExactKeys(receipt, SeedReceiptKeys) ||
                !Bool(evidence, "success") || !Bool(receipt, "success") ||
                Text(receipt, "evidencePath") != EvidenceRelativePath ||
                !IsSha256(evidenceSha) || !string.Equals(Text(receipt, "evidenceSha256"),
                    evidenceSha, StringComparison.OrdinalIgnoreCase)) return false;
            string commitment = Text(receipt, "evidenceCommitmentSha256");
            return IsSha256(commitment) && string.Equals(Text(evidence,
                    "evidence_commitment_sha256"), commitment,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(EvidenceCommitmentDigest(evidence), commitment,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool ResultContainsReceiptIdentity(Result result)
        {
            Dictionary<string, object> row = ObjectFrom(result);
            return row.ContainsKey("phase_receipt_path") || row.ContainsKey("phase_receipt_sha256") ||
                row.ContainsKey("phase_receipt_committed") || row.ContainsKey("receiptPath") ||
                row.ContainsKey("receiptSha256") || row.ContainsKey("receiptCommitted");
        }

        private static void WriteCreateNewAndReread(string path, byte[] bytes,
            Action createdByThisRun = null)
        {
            Require(!File.Exists(path), "IMMUTABLE_OUTPUT_ALREADY_EXISTS", 42, path);
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None))
            {
                if (createdByThisRun != null) createdByThisRun();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            byte[] reread = File.ReadAllBytes(path);
            Require(reread.SequenceEqual(bytes) && FileLinkCount(path) == 1 &&
                !HasReparsePoint(path), "CREATE_NEW_REREAD_INVALID", 42,
                "CreateNew output bytes or file identity changed: " + path);
        }

        private static void RemoveUnpairedCommitArtifacts(Result result)
        {
            TryDeleteOwnedArtifact(result.receipt_path, result.attempt_directory,
                result.receipt_created_by_this_run);
            TryDeleteOwnedArtifact(result.evidence_path, result.attempt_directory,
                result.evidence_created_by_this_run);
        }

        private static void TryDeleteOwnedArtifact(string path, string attemptDirectory,
            bool createdByThisRun)
        {
            if (!createdByThisRun || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try
            {
                if (IsUnder(path, attemptDirectory) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1 &&
                    IsUnder(CanonicalExistingPath(path), CanonicalExistingPath(attemptDirectory)))
                    File.Delete(path);
            }
            catch { }
        }

        private static bool StableRollbackCadGate(Result result)
        {
            if (result == null) return false;
            if (!WaitForGlobalCadQuiescence(() => ProcessIds("SLDWORKS"),
                    () => ProcessIds("sldProcMon"), 30000, 1000, 200)) return false;
            CaptureFinalProcessGate(result);
            return result.processes.final_gate;
        }

        private static void RollbackTarget(Result result)
        {
            result.rollback_attempted = true;
            if (!result.target_created_by_this_run || !Directory.Exists(result.target_directory))
            {
                result.rollback_succeeded = !Directory.Exists(result.target_directory);
                return;
            }
            string reason = "target is not the fixed working_pack location";
            if (!RollbackPathSafe(result.target_directory, result.attempt_directory,
                    result.source_hashes, out reason))
            {
                result.rollback_error = reason;
                return;
            }
            try
            {
                string[] paths = Directory.GetFiles(result.target_directory, "*",
                    SearchOption.TopDirectoryOnly);
                List<string> lockFiles = paths.Where(path => Path.GetFileName(path)
                    .StartsWith("~$", StringComparison.Ordinal)).ToList();
                List<string> cadFiles = paths.Except(lockFiles,
                    StringComparer.OrdinalIgnoreCase).ToList();
                string rollbackReason;
                if (!RollbackCadBasenameSubsetSafe(cadFiles.Select(Path.GetFileName),
                        result.source_hashes, out rollbackReason))
                {
                    result.rollback_error = rollbackReason;
                    return;
                }
                foreach (string lockFile in lockFiles) File.Delete(lockFile);
                result.rollback_transient_lock_files_removed = lockFiles.Count;
                Directory.Delete(result.target_directory, true);
                result.rollback_succeeded = !Directory.Exists(result.target_directory);
                if (!result.rollback_succeeded) result.rollback_error = "target still exists";
            }
            catch (Exception exception)
            {
                result.rollback_error = SafeException(exception);
            }
        }

        private static void RollbackNativeTopCoverCapturePack(Result result)
        {
            result.topcover_capture_pack_rollback_attempted = true;
            string path = result.topcover_capture_pack;
            try
            {
                if (!result.topcover_capture_pack_created || string.IsNullOrWhiteSpace(path) ||
                    !Directory.Exists(path))
                {
                    result.topcover_capture_pack_rollback_succeeded =
                        string.IsNullOrWhiteSpace(path) || !Directory.Exists(path);
                    return;
                }
                string expected = Path.Combine(result.attempt_directory, "native_cad",
                    "gold_capture_pack");
                if (!SamePath(path, expected) || !IsUnder(path, result.attempt_directory) ||
                    TreeHasReparsePoint(path) || Directory.GetDirectories(path, "*",
                        SearchOption.TopDirectoryOnly).Length != 0 ||
                    !IsUnder(CanonicalExistingPath(path),
                        CanonicalExistingPath(result.attempt_directory)))
                {
                    result.topcover_capture_pack_rollback_error =
                        "gold capture pack is not the fixed flat owned path";
                    return;
                }
                string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                List<string> lockFiles = files.Where(file => Path.GetFileName(file)
                    .StartsWith("~$", StringComparison.Ordinal)).ToList();
                List<string> cadFiles = files.Except(lockFiles,
                    StringComparer.OrdinalIgnoreCase).ToList();
                HashSet<string> expectedNames = new HashSet<string>(
                    GoldTopCoverCaptureCadFileNames, StringComparer.OrdinalIgnoreCase);
                bool safe = cadFiles.Count <= expectedNames.Count && lockFiles.Count <=
                    expectedNames.Count && cadFiles.All(file =>
                        expectedNames.Contains(Path.GetFileName(file)) &&
                        ExpectedGoldTopCoverSha256.ContainsKey(Path.GetFileName(file)) &&
                        FileLinkCount(file) == 1 && !HasReparsePoint(file) &&
                        SamePath(Path.GetDirectoryName(file), path) && string.Equals(
                            Sha256File(file), ExpectedGoldTopCoverSha256[Path.GetFileName(file)],
                            StringComparison.OrdinalIgnoreCase)) && lockFiles.All(file =>
                    {
                        string name = Path.GetFileName(file);
                        return name.Length > 2 && expectedNames.Contains(name.Substring(2)) &&
                            PairedSolidWorksLockFileSafe(file, path,
                                ExpectedGoldTopCoverSha256);
                    });
                if (!safe)
                {
                    result.topcover_capture_pack_rollback_error =
                        "gold capture pack contains an unknown, linked, changed or escaped file";
                    return;
                }
                foreach (string lockFile in lockFiles) File.Delete(lockFile);
                result.topcover_capture_pack_transient_lock_files_removed = lockFiles.Count;
                Directory.Delete(path, true);
                result.topcover_capture_pack_rollback_succeeded = !Directory.Exists(path);
                if (!result.topcover_capture_pack_rollback_succeeded)
                    result.topcover_capture_pack_rollback_error =
                        "gold capture pack still exists";
            }
            catch (Exception exception)
            {
                result.topcover_capture_pack_rollback_error = SafeException(exception);
            }
        }

        private static bool RollbackPathSafe(string target, string attemptDirectory,
            IDictionary<string, string> sourceHashes, out string reason)
        {
            reason = "";
            try
            {
                string expected = Path.Combine(attemptDirectory, "native_cad", "working_pack");
                if (!SamePath(target, expected) || !IsUnder(target, attemptDirectory) ||
                    !Directory.Exists(target))
                {
                    reason = "target is not the exact owned working_pack";
                    return false;
                }
                if (TreeHasReparsePoint(target) ||
                    !IsUnder(CanonicalExistingPath(target), CanonicalExistingPath(attemptDirectory)))
                {
                    reason = "target contains a reparse or realpath escape";
                    return false;
                }
                if (Directory.GetDirectories(target, "*", SearchOption.TopDirectoryOnly).Length != 0)
                {
                    reason = "target is not flat";
                    return false;
                }
                string[] files = Directory.GetFiles(target, "*", SearchOption.TopDirectoryOnly);
                List<string> lockFiles = files.Where(path => Path.GetFileName(path)
                    .StartsWith("~$", StringComparison.Ordinal)).ToList();
                List<string> cadFiles = files.Except(lockFiles,
                    StringComparer.OrdinalIgnoreCase).ToList();
                if (cadFiles.Count > ExpectedCadCount || lockFiles.Count > ExpectedCadCount ||
                    lockFiles.Any(path => !PairedSolidWorksLockFileSafe(path, target,
                        sourceHashes)) || cadFiles.Any(path =>
                        (!IsAssembly(path) && !IsPart(path)) || HasReparsePoint(path) ||
                        FileLinkCount(path) != 1 || !IsUnder(CanonicalExistingPath(path),
                            CanonicalExistingPath(target))))
                {
                    reason = "target contains unexpected, linked or escaped files";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                reason = SafeException(exception);
                return false;
            }
        }

        private static bool PairedSolidWorksLockFileSafe(string path, string target,
            IDictionary<string, string> sourceHashes)
        {
            try
            {
                if (sourceHashes == null || !File.Exists(path) || HasReparsePoint(path) ||
                    FileLinkCount(path) != 1 || !SamePath(Path.GetDirectoryName(path), target) ||
                    !IsUnder(CanonicalExistingPath(path), CanonicalExistingPath(target))) return false;
                string name = Path.GetFileName(path);
                string pairedName = name != null && name.StartsWith("~$", StringComparison.Ordinal) &&
                    name.Length > 2 ? name.Substring(2) : "";
                return (IsPart(pairedName) || IsAssembly(pairedName)) &&
                    sourceHashes.ContainsKey(pairedName) && new FileInfo(path).Length <= 4096;
            }
            catch { return false; }
        }

        private static bool RollbackCadBasenameSubsetSafe(IEnumerable<string> fileNames,
            IDictionary<string, string> sourceHashes, out string reason)
        {
            reason = "";
            List<string> names = fileNames == null ? new List<string>() : fileNames.ToList();
            if (sourceHashes == null || sourceHashes.Count != ExpectedCadCount ||
                names.Count > ExpectedCadCount)
            {
                reason = "rollback source inventory is unavailable or file count exceeds exact75";
                return false;
            }
            if (names.Any(name => string.IsNullOrWhiteSpace(name) ||
                (!IsAssembly(name) && !IsPart(name)) || !sourceHashes.ContainsKey(name)))
            {
                reason = "rollback contains an unknown or non-CAD basename";
                return false;
            }
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            {
                reason = "rollback contains a case-insensitive duplicate CAD basename";
                return false;
            }
            return true;
        }

        private static void ReleaseLocks(Result result)
        {
            if (planReadLock != null)
            {
                planReadLock.Dispose();
                planReadLock = null;
            }
            if (attemptLock != null)
            {
                attemptLock.Dispose();
                attemptLock = null;
            }
            if (result.attempt_lock_acquired && !string.IsNullOrWhiteSpace(result.attempt_lock_path))
            {
                try
                {
                    if (File.Exists(result.attempt_lock_path) &&
                        IsUnder(result.attempt_lock_path, result.attempt_directory) &&
                        !HasReparsePoint(result.attempt_lock_path) &&
                        FileLinkCount(result.attempt_lock_path) == 1)
                        File.Delete(result.attempt_lock_path);
                }
                catch { }
            }
        }

        private static string EvidenceCommitmentDigest(Result result)
        {
            return EvidenceCommitmentDigest(ObjectFrom(result));
        }

        private static string EvidenceCommitmentDigest(Dictionary<string, object> evidence)
        {
            Require(evidence != null, "EVIDENCE_COMMITMENT_OBJECT_REQUIRED", 71,
                "evidence commitment requires a JSON object");
            Dictionary<string, object> projection = new Dictionary<string, object>(evidence,
                StringComparer.Ordinal);
            foreach (string key in EvidenceCommitmentExcludedKeys) projection.Remove(key);
            return Sha256Text(StableJson(projection));
        }

        private static string StableJson(object value)
        {
            if (value == null) return "null";
            string text = value as string;
            if (text != null) return EcmaJsonString(text);
            if (value is bool) return (bool)value ? "true" : "false";
            if (IsJsonNumber(value)) return EcmaJsonNumber(value);
            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
                return "{" + string.Join(",", dictionary.Keys.OrderBy(key => key,
                    StringComparer.Ordinal).Select(key => EcmaJsonString(key) + ":" +
                    StableJson(dictionary[key])).ToArray()) + "}";
            IDictionary genericDictionary = value as IDictionary;
            if (genericDictionary != null)
            {
                Dictionary<string, object> converted = new Dictionary<string, object>(
                    StringComparer.Ordinal);
                foreach (DictionaryEntry entry in genericDictionary)
                    converted[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                return StableJson(converted);
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                List<string> rows = new List<string>();
                foreach (object row in enumerable) rows.Add(StableJson(row));
                return "[" + string.Join(",", rows.ToArray()) + "]";
            }
            return StableJson(ObjectFrom(value));
        }

        private static bool IsJsonNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal;
        }

        private static string EcmaJsonNumber(object value)
        {
            double number;
            try { number = Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return "null"; }
            if (double.IsNaN(number) || double.IsInfinity(number)) return "null";
            if (number == 0.0) return "0";
            bool negative = number < 0;
            double absolute = Math.Abs(number);
            string roundTrip = absolute.ToString("R", CultureInfo.InvariantCulture);
            int exponentMarker = roundTrip.IndexOfAny(new[] { 'E', 'e' });
            string mantissa = exponentMarker < 0 ? roundTrip :
                roundTrip.Substring(0, exponentMarker);
            int exponent = exponentMarker < 0 ? 0 : int.Parse(
                roundTrip.Substring(exponentMarker + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture);
            int point = mantissa.IndexOf('.');
            int pointPosition = point < 0 ? mantissa.Length : point;
            string digits = mantissa.Replace(".", "");
            int leading = 0;
            while (leading < digits.Length - 1 && digits[leading] == '0') leading++;
            if (leading > 0)
            {
                digits = digits.Substring(leading);
                pointPosition -= leading;
            }
            int decimalPosition = pointPosition + exponent;
            string body;
            if (absolute >= 0.000001 && absolute < 1e21)
            {
                if (decimalPosition <= 0)
                    body = "0." + new string('0', -decimalPosition) + digits;
                else if (decimalPosition >= digits.Length)
                    body = digits + new string('0', decimalPosition - digits.Length);
                else body = digits.Substring(0, decimalPosition) + "." +
                    digits.Substring(decimalPosition);
            }
            else
            {
                int scientificExponent = decimalPosition - 1;
                body = digits.Length == 1 ? digits : digits.Substring(0, 1) + "." +
                    digits.Substring(1);
                body += "e" + (scientificExponent >= 0 ? "+" : "-") +
                    Math.Abs(scientificExponent).ToString(CultureInfo.InvariantCulture);
            }
            return negative ? "-" + body : body;
        }

        private static string EcmaJsonString(string value)
        {
            StringBuilder output = new StringBuilder(value.Length + 2);
            output.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 0x20 ||
                            (char.IsSurrogate(character) &&
                             !(char.IsHighSurrogate(character) && index + 1 < value.Length &&
                               char.IsLowSurrogate(value[index + 1])) &&
                             !(char.IsLowSurrogate(character) && index > 0 &&
                               char.IsHighSurrogate(value[index - 1]))))
                        {
                            output.Append("\\u");
                            output.Append(((int)character).ToString("x4",
                                CultureInfo.InvariantCulture));
                        }
                        else output.Append(character);
                        break;
                }
            }
            output.Append('"');
            return output.ToString();
        }

        private static bool RunNativeTopCoverFrontBrepCompletenessSelfTest()
        {
            NativeTopCoverFrontBrepPartCapture empty = new NativeTopCoverFrontBrepPartCapture
                { expectedFaceCount = 48, expectedEdgeCount = 112 };
            NativeTopCoverFrontBrepFace nonFiniteFace = new NativeTopCoverFrontBrepFace
            {
                areaSi = double.NaN, surfaceIdentity = 1, isPlane = true,
                boxSi = new List<double> { 0, 0, 0, 1, 1, 1 },
                planeParamsSi = new List<double> { 0, 0, 0, 0, 0, 1 }
            };
            NativeTopCoverFrontBrepEdge incompleteEdge = new NativeTopCoverFrontBrepEdge
            {
                curveType = 1, curveTag = 1, sense = 1, uMin = 0, uMax = 1,
                lengthSi = 1, startPointSi = new List<double> { 0, 0, 0 },
                endPointSi = new List<double> { 1, 0, 0 }, isCircle = true
            };
            return !NativeTopCoverFrontBrepPartCaptureComplete(empty) &&
                !NativeTopCoverFrontBrepFaceComplete(nonFiniteFace) &&
                !NativeTopCoverFrontBrepEdgeComplete(incompleteEdge) &&
                NativeTopCoverFrontBrepPartCaptureComplete(empty) == false;
        }

        private static int RunSelfTests()
        {
            Dictionary<string, bool> checks = new Dictionary<string, bool>(StringComparer.Ordinal);
            string crossLanguageCanonicalJson = "";
            string crossLanguageCanonicalJsonSha256 = "";
            string temp = Path.Combine(Path.GetTempPath(), "native-seed-static-" +
                Guid.NewGuid().ToString("N"));
            try
            {
                Dictionary<string, object> sharedFixture = new Dictionary<string, object>(
                    StringComparer.Ordinal)
                {
                    { "z", 7 },
                    { "nested", new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "beta", false },
                            { "alpha", new object[] { "L", 381,
                                new Dictionary<string, object>(StringComparer.Ordinal)
                                { { "y", 2 }, { "x", 1 } } } }
                        }
                    },
                    { "phase", "clone_native_seed" },
                    { "completed_at_utc", "ignored" },
                    { "phase_receipt_sha256", "ignored" }
                };
                checks["semantic_commitment_matches_shared_vector"] =
                    EvidenceCommitmentDigest(sharedFixture) ==
                    "684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1";
                string nonFiniteJson = new UTF8Encoding(false, true).GetString(JsonBytes(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "nan", double.NaN },
                        { "positive", double.PositiveInfinity },
                        { "negative", double.NegativeInfinity },
                        { "array", new object[] { 1.0, double.NaN, "NaN,Infinity,-Infinity" } }
                    }));
                checks["json_bytes_replace_nonfinite_numbers_without_touching_strings"] =
                    nonFiniteJson.Contains("\"nan\":null") &&
                    nonFiniteJson.Contains("\"positive\":null") &&
                    nonFiniteJson.Contains("\"negative\":null") &&
                    nonFiniteJson.Contains("\"array\":[1,null,\"NaN,Infinity,-Infinity\"]") &&
                    nonFiniteJson.IndexOf(":NaN", StringComparison.Ordinal) < 0 &&
                    nonFiniteJson.IndexOf(":Infinity", StringComparison.Ordinal) < 0 &&
                    nonFiniteJson.IndexOf(":-Infinity", StringComparison.Ordinal) < 0;

                Dictionary<string, object> crossFixture = new Dictionary<string, object>(
                    StringComparer.Ordinal)
                {
                    { "ticks", 639220258081076150L },
                    { "float", 1.25 },
                    { "oneE20", 1e20 },
                    { "oneE21", 1e21 },
                    { "micro", 1e-6 },
                    { "subMicro", 1e-7 },
                    { "html", "<tag>&/x" },
                    { "control", "line\nquote\"\\" },
                    { "chinese", "结构" }
                };
                crossLanguageCanonicalJson = StableJson(crossFixture);
                crossLanguageCanonicalJsonSha256 = Sha256Text(crossLanguageCanonicalJson);
                checks["ecmascript_json_cross_language_vectors"] =
                    crossLanguageCanonicalJsonSha256 ==
                    "ECBA0EEC2CD1A5A5499E83E2358CE0AC352A38C976750D9723EAF6A09DBA85BF" &&
                    crossLanguageCanonicalJson.Contains("\"oneE20\":100000000000000000000") &&
                    crossLanguageCanonicalJson.Contains("\"oneE21\":1e+21") &&
                    crossLanguageCanonicalJson.Contains("\"micro\":0.000001") &&
                    crossLanguageCanonicalJson.Contains("\"ticks\":639220258081076100") &&
                    crossLanguageCanonicalJson.Contains("\"html\":\"<tag>&/x\"");

                Directory.CreateDirectory(temp);
                string digestDir = Path.Combine(temp, "digest");
                Directory.CreateDirectory(digestDir);
                string a = Path.Combine(digestDir, "a.sldprt");
                string b = Path.Combine(digestDir, "B.SLDASM");
                File.WriteAllText(a, "A", new UTF8Encoding(false));
                File.WriteAllText(b, "B", new UTF8Encoding(false));
                string digest = InventoryDigest(new[] { Snapshot(b), Snapshot(a) });
                checks["canonical_name_sha_inventory_digest"] = digest ==
                    "1694B5A51AAFAF24AFFCE65532E4AB2248513A6314C0110C0FDD6EBC90A0FF19" &&
                    string.Equals(InventoryDigest(new[] { Snapshot(a), Snapshot(b) }), digest,
                        StringComparison.OrdinalIgnoreCase);

                Dictionary<string, object> contracts = BuildStageReceiptContractFixture();
                checks["exact_shared_stage_receipt_contract"] =
                    ValidateStageReceiptContracts(contracts);
                ChildObject(contracts, "clone_native_seed")["path"] = "evidence/forged.json";
                checks["exact_shared_stage_receipt_contract"] =
                    checks["exact_shared_stage_receipt_contract"] &&
                    !ValidateStageReceiptContracts(contracts);
                checks["specialized_lock_manifest_exact_and_mutations_rejected"] =
                    RunSpecializedLockManifestSelfTest();
                string unstampedSource = Path.Combine(temp, "unstamped-standard-source.cs");
                string unstampedText = "internal static class Fixture\r\n{\r\n}\r\n";
                File.WriteAllText(unstampedSource, unstampedText, new UTF8Encoding(false));
                checks["standard_manifest_source_without_stamp_is_supported"] =
                    NormalizedStandardManifestSourceHashAt(unstampedSource) ==
                    Sha256Text(unstampedText.Replace("\r\n", "\n"));

                Result authResult;
                Dictionary<string, object> authorization = BuildAuthorizationFixture(out authResult);
                AuthorizationBinding binding;
                DateTime fixtureNow = new DateTime(2026, 8, 12, 10, 5, 0, DateTimeKind.Utc);
                checks["exact_shared_authorization_contract"] =
                    AuthorizationContractValid(authorization, authResult, fixtureNow, null,
                        out binding) && Text(authorization, "authorizationId") ==
                    "native-auth-07d19e7116d42f92e3c02053e74cb2da";
                Dictionary<string, object> mutatedId = CloneObject(authorization);
                mutatedId["authorizationId"] = "native-auth-00000000000000000000000000000000";
                Dictionary<string, object> mutatedExpiry = CloneObject(authorization);
                mutatedExpiry["expiresAt"] = "2026-08-12T10:40:00.000Z";
                checks["authorization_id_and_expiry_mutations_rejected"] =
                    !AuthorizationContractValid(mutatedId, authResult, fixtureNow, null,
                        out binding) &&
                    !AuthorizationContractValid(mutatedExpiry, authResult, fixtureNow, null,
                        out binding);
                checks["planning_only_plan_requires_short_authorization"] =
                    PlanBoundaryValid(authResult.plan_document) &&
                    AuthorizationContractValid(authorization, authResult, fixtureNow, null,
                        out binding) &&
                    !Bool(ChildObject(authResult.plan_document, "executionBoundary"),
                        "executorImplemented");

                string source = Path.Combine(temp, "copy-source");
                string target = Path.Combine(temp, "copy-target");
                Directory.CreateDirectory(source);
                for (int index = 0; index < ExpectedCadCount; index++)
                {
                    string name = index == 0 ? RootFileName : (index < ExpectedAssemblyCount ?
                        "assembly-" + index.ToString("D2", CultureInfo.InvariantCulture) + ".SLDASM" :
                        "part-" + index.ToString("D2", CultureInfo.InvariantCulture) + ".SLDPRT");
                    File.WriteAllText(Path.Combine(source, name), "fixture-" + index,
                        new UTF8Encoding(false));
                }
                List<FileSnapshot> sourceRows = CaptureFlatCadTree(source,
                    "SELFTEST_SOURCE_INVALID").Select(Snapshot).ToList();
                Directory.CreateDirectory(target);
                foreach (FileSnapshot row in sourceRows)
                    File.Copy(row.path, Path.Combine(target, row.name), false);
                Dictionary<string, string> targetRows = CaptureFlatCadTree(target,
                    "SELFTEST_TARGET_INVALID").ToDictionary(Path.GetFileName, Sha256File,
                    StringComparer.OrdinalIgnoreCase);
                checks["direct_copy_preserves_75_names_and_74_nonroot_hashes"] =
                    targetRows.Count == ExpectedCadCount && sourceRows.Where(row =>
                        row.name != RootFileName).All(row => targetRows[row.name] == row.sha256);
                checks["relocation_initial_warning_allows_only_32_or_96_without_id_mismatch"] =
                    InitialRelocatedWarningAllowed(32) && InitialRelocatedWarningAllowed(96) &&
                    !InitialRelocatedWarningAllowed(0) && !InitialRelocatedWarningAllowed(1) &&
                    !InitialRelocatedWarningAllowed(64) && !InitialRelocatedWarningAllowed(97);
                checks["relocated_warning_64_requires_readonly_diagnostic_before_save"] =
                    !RelocationBasePartDiagnosticRequired(32) &&
                    RelocationBasePartDiagnosticRequired(96) &&
                    !RelocationBasePartDiagnosticRequired(64) &&
                    !RelocationBasePartDiagnosticRequired(97) &&
                    IsExternalLinkFeatureType("BaseBody") &&
                    IsExternalLinkFeatureType("SplitBody") &&
                    IsExternalLinkFeatureType("MirrorStock") &&
                    IsExternalLinkFeatureType("Reference");
                FreshComponentDiagnostic validComponent = new FreshComponentDiagnostic
                {
                    path_local = true,
                    cache_before_empty = true,
                    opened = true,
                    open_errors = 0,
                    open_warnings = 32,
                    read_only = true,
                    rebuilt = true,
                    traversal_complete = true,
                    feature_count = 4,
                    cache_after_empty = true,
                    session_exit_proven = true,
                    file_unchanged = true
                };
                FreshComponentDiagnostic warning64Component = new FreshComponentDiagnostic
                {
                    path_local = true,
                    cache_before_empty = true,
                    opened = true,
                    open_errors = 0,
                    open_warnings = 96,
                    read_only = true,
                    rebuilt = true,
                    traversal_complete = true,
                    feature_count = 4,
                    cache_after_empty = true,
                    session_exit_proven = true,
                    file_unchanged = true
                };
                FreshComponentDiagnostic dirtyCacheComponent = new FreshComponentDiagnostic
                {
                    path_local = true,
                    cache_before_empty = true,
                    opened = true,
                    open_errors = 0,
                    open_warnings = 32,
                    read_only = true,
                    rebuilt = true,
                    traversal_complete = true,
                    feature_count = 4,
                    cache_after_empty = false,
                    session_exit_proven = true,
                    file_unchanged = true
                };
                checks["warning64_assembly_component_diagnostic_fail_closed"] =
                    TopCoverComponentDiagnosticComplete(true, 6, 6, 0, 0) &&
                    !TopCoverComponentDiagnosticComplete(false, 6, 6, 0, 0) &&
                    !TopCoverComponentDiagnosticComplete(true, 6, 6, 1, 0) &&
                    !TopCoverComponentDiagnosticComplete(true, 6, 6, 0, 1) &&
                    FreshComponentDiagnosticComplete(validComponent) &&
                    !FreshComponentDiagnosticComplete(warning64Component) &&
                    !FreshComponentDiagnosticComplete(dirtyCacheComponent);
                checks["fresh_reopen_requires_exact_known_warning"] =
                    FreshReopenWarningAllowed(0, 32) && FreshReopenWarningAllowed(0, 96) &&
                    !FreshReopenWarningAllowed(0, 64) &&
                    !FreshReopenWarningAllowed(1, 32);
                string processIdentityJson = new JavaScriptSerializer
                    { MaxJsonLength = int.MaxValue, RecursionLimit = 256 }.Serialize(
                        new ProcessIdentity
                        {
                            pid = 7,
                            start_utc_ticks_value = 639236152607061250L
                        });
                checks["process_identity_ticks_are_exact_json_strings"] =
                    processIdentityJson.Contains(
                        "\"start_utc_ticks\":\"639236152607061250\"") &&
                    !processIdentityJson.Contains("start_utc_ticks_value");
                FeatureHealthEvidence health = new FeatureHealthEvidence
                    { traversal_complete = true, feature_count = ExpectedFeatureCount };
                health.issues.Add(new FeatureIssueEvidence
                {
                    name = KnownIssueName,
                    type = KnownIssueType,
                    error_code = 51,
                    error_code2 = 51,
                    warning = true
                });
                bool oneIssue = KnownRootIssueGate(health);
                health.issues.Add(new FeatureIssueEvidence
                    { name = "forged", type = "Reference", error_code = 51,
                        error_code2 = 51, warning = true });
                checks["known_root_issue_requires_unique_reference_51"] =
                    oneIssue && !KnownRootIssueGate(health);
                checks["reference_feature_external_links_are_read_only_diagnostic_only"] =
                    IsExternalLinkFeatureType("Reference") &&
                    IsExternalLinkFeatureType("BaseBody") &&
                    IsExternalLinkFeatureType("SplitBody") &&
                    IsExternalLinkFeatureType("MirrorStock") &&
                    !IsExternalLinkFeatureType("ICE");

                checks["exit_wait_retries_transient_unproven_until_exact_exit"] =
                    RunExitWaitSelfTest();
                checks["exit_grace_resolves_unproven_without_weakening_identity_gate"] =
                    RunExitGraceSelfTest();
                checks["monitor_cleanup_requires_recorded_exact_parent_command_and_identity"] =
                    RunRecordedMonitorOwnershipSelfTest();
                checks["monitor_global_zero_gate_waits_for_stable_empty_enumeration"] =
                    RunGlobalProcessZeroSelfTest();
                checks["cad_session_quiescence_rejects_late_process_appearance"] =
                    RunCadSessionQuiescenceSelfTest();
                checks["top_cover_load_order_diagnostic_is_read_only_and_fail_closed"] =
                    RunTopCoverLoadOrderDiagnosticSelfTest();
                checks["top_cover_load_order_diagnostic_captures_root_context_read_only"] =
                    RunTopCoverLoadOrderRootContextSelfTest();
                checks["root_context_candidate_groups_are_read_only_and_conditional"] =
                    RunRootContextCandidateGroupsSelfTest();
                checks["dependency_inventory_ignores_only_paired_solidworks_lock_files"] =
                    RunDependencyInventoryLockFileSelfTest();
                checks["top_cover_component_paths_match_exact_readonly_preload_set"] =
                    RunTopCoverComponentPathSetSelfTest();
                checks["native_topcover_part_measurement_probe_is_nonconsumable"] =
                    !NativeTopCoverPartMeasurementProbeOnly && !NativeTopCoverRebuildProbeOnly &&
                    NativeTopCoverPartMeasurementPrivateRelativePath ==
                        "evidence/private/topcover_native_part_measurement_probe_only.json" &&
                    TopCoverCandidateUpstreamFirstFileNames.Skip(1).Distinct(
                        StringComparer.OrdinalIgnoreCase).Count() == 6;
                checks["native_topcover_part_detach_probe_is_nonconsumable"] =
                    !NativeTopCoverPartDetachProbeOnly && !NativeTopCoverPartMeasurementProbeOnly &&
                    NativeTopCoverPartDetachPrivateRelativePath ==
                        "evidence/private/topcover_native_part_detach_probe_only.json" &&
                    TopCoverPhysicalPartNames().Count() == 5 &&
                    TopCoverPhysicalPartNames().Distinct(StringComparer.OrdinalIgnoreCase).Count() == 5;
                checks["native_topcover_rebuild_capture_probe_is_nonconsumable"] =
                    !NativeTopCoverRebuildCaptureProbeOnly &&
                    !NativeTopCoverPartMeasurementProbeOnly &&
                    !NativeTopCoverPartDetachProbeOnly && !NativeTopCoverRebuildProbeOnly &&
                    NativeTopCoverRebuildCapturePrivateRelativePath ==
                        "evidence/private/topcover_native_rebuild_capture_probe_only.json" &&
                    NativeTopCoverRebuildCaptureCleanupPrivateRelativePath ==
                        "evidence/private/topcover_native_rebuild_capture_probe_cleanup.json" &&
                    ExpectedGoldTopCoverSha256.Count == 14 &&
                    ExpectedGoldTopCoverDxfSha256.Count == 5 &&
                    GoldTopCoverCaptureCadFileNames.Length == 8;
                checks["native_topcover_master_reference_capture_probe_is_retained_dormant"] =
                    !NativeTopCoverMasterReferenceCaptureProbeOnly &&
                    !NativeTopCoverRebuildCaptureProbeOnly &&
                    NativeTopCoverMasterReferenceCapturePrivateRelativePath ==
                        "evidence/private/topcover_native_master_reference_capture_probe_only.json" &&
                    NativeTopCoverMasterReferenceCaptureCleanupPrivateRelativePath ==
                        "evidence/private/topcover_native_master_reference_capture_probe_cleanup.json" &&
                    !NativeTopCoverMasterReferenceDatasetComplete(
                        new NativeTopCoverMasterReferenceCaptureEvidence());
                checks["native_topcover_front_brep_capture_probe_is_nonconsumable"] =
                    !NativeTopCoverFrontBrepCaptureProbeOnly &&
                    !NativeTopCoverMasterReferenceCaptureProbeOnly &&
                    !NativeTopCoverRebuildCaptureProbeOnly &&
                    NativeTopCoverFrontBrepCapturePrivateRelativePath ==
                        "evidence/private/topcover_native_front_brep_capture_probe_only.json" &&
                    NativeTopCoverFrontBrepCaptureCleanupPrivateRelativePath ==
                        "evidence/private/topcover_native_front_brep_capture_probe_cleanup.json" &&
                    !NativeTopCoverFrontBrepCaptureComplete(
                        new NativeTopCoverFrontBrepCaptureEvidence());
                checks["native_topcover_front_brep_contract_fails_closed"] =
                    RunNativeTopCoverFrontBrepCompletenessSelfTest();
                checks["native_topcover_part_measurement_completeness_fails_closed"] =
                    RunNativeTopCoverPartMeasurementCompletenessSelfTest();
                checks["native_topcover_part_detach_contract_fails_closed"] =
                    RunNativeTopCoverPartDetachContractSelfTest();
                checks["native_topcover_rebuild_capture_contract_fails_closed"] =
                    RunNativeTopCoverRebuildCaptureContractSelfTest();
                checks["native_topcover_capture_pack_rollback_is_exact_and_lock_aware"] =
                    RunNativeTopCoverCapturePackRollbackSelfTest(temp);
                double[] identityTransform = new[] { 1.0, 0.0, 0.0, 0.0, 1.0,
                    0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 };
                double[] nearTransform = identityTransform.ToArray();
                nearTransform[9] = 5e-8;
                double[] farTransform = identityTransform.ToArray();
                farTransform[9] = 2e-7;
                double[] badScaleTransform = identityTransform.ToArray();
                badScaleTransform[12] = 0.5;
                string finalProbeDiagnostic = "scanComplete=true; scanned=75; " +
                    "warning64Docs=none; documentFaults=none; featureIssues=" +
                    RootFileName + ">" + KnownIssueName +
                    "[Reference]:51/51/true; linkFeatures=none; " +
                    "assemblyComponentDiagnostics=none";
                checks["native_topcover_transform_and_final_diagnostic_fail_closed"] =
                    ValidTransform16(identityTransform) &&
                    SameTransform16(identityTransform, nearTransform) &&
                    !SameTransform16(identityTransform, farTransform) &&
                    !ValidTransform16(badScaleTransform) &&
                    FinalProbeRelocationDiagnosticGate(finalProbeDiagnostic) &&
                    !FinalProbeRelocationDiagnosticGate(finalProbeDiagnostic.Replace(
                        "warning64Docs=none", "warning64Docs=上盖焊接.SLDASM:0/96")) &&
                    !FinalProbeRelocationDiagnosticGate(finalProbeDiagnostic.Replace(
                        "documentFaults=none", "documentFaults=open-null"));
                Result privateEvidenceResult = new Result
                    { attempt_directory = temp, preflight_passed = true };
                NativeTopCoverPartMeasurementEvidence privateEvidenceProbe =
                    new NativeTopCoverPartMeasurementEvidence
                    {
                        probeOnly = true,
                        consumable = false,
                        mode = "measure_topcover_parts"
                    };
                bool privateEvidenceWrite = WriteNativeTopCoverPartMeasurementPrivateEvidence(
                    privateEvidenceResult, privateEvidenceProbe);
                string privateEvidencePath = Path.Combine(temp,
                    NativeTopCoverPartMeasurementPrivateRelativePath.Replace('/',
                        Path.DirectorySeparatorChar));
                Dictionary<string, object> privateEvidenceReread =
                    File.Exists(privateEvidencePath) ? ReadJsonObject(privateEvidencePath) :
                        new Dictionary<string, object>(StringComparer.Ordinal);
                checks["native_topcover_part_measurement_private_evidence_serializes_written_true"] =
                    privateEvidenceWrite && privateEvidenceProbe.privateEvidenceWritten &&
                    Bool(privateEvidenceReread, "privateEvidenceWritten") &&
                    Bool(privateEvidenceReread, "probeOnly") &&
                    !Bool(privateEvidenceReread, "consumable");
                checks["native_topcover_candidate_save_profile_is_exact"] =
                    CandidateSaveProfileAllowed(true, 0, 0, true, false) &&
                    CandidateSaveProfileAllowed(false,
                        (int)swFileSaveError_e.swReadOnlySaveError, 0, true, false) &&
                    !CandidateSaveProfileAllowed(false, 0, 0, true, false) &&
                    !CandidateSaveProfileAllowed(false,
                        (int)swFileSaveError_e.swReadOnlySaveError, 1, true, false) &&
                    !CandidateSaveProfileAllowed(false,
                        (int)swFileSaveError_e.swReadOnlySaveError, 0, false, false) &&
                    !CandidateSaveProfileAllowed(false,
                        (int)swFileSaveError_e.swReadOnlySaveError, 0, true, true);

                checks["evidence_and_receipt_are_paired_without_hash_cycle"] =
                    RunPairingSelfTest(temp);
                checks["rollback_refuses_reparse_or_hardlink_escape"] =
                    RunRollbackSelfTest(temp);
                checks["rollback_requires_known_exact75_basename_subset"] =
                    RunRollbackKnownBasenameSubsetSelfTest();
            }
            catch
            {
                foreach (string required in RequiredSelfTestNames())
                    if (!checks.ContainsKey(required)) checks[required] = false;
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
            foreach (string required in RequiredSelfTestNames())
                if (!checks.ContainsKey(required)) checks[required] = false;
            int failed = checks.Count(row => !row.Value);
            SelfTestReport report = new SelfTestReport
            {
                status = failed == 0 ? "PASS" : "FAIL",
                cadStarted = false,
                checksTotal = checks.Count,
                checksFailed = failed,
                checks = checks,
                crossLanguageCanonicalJson = crossLanguageCanonicalJson,
                crossLanguageCanonicalJsonSha256 = crossLanguageCanonicalJsonSha256
            };
            Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 256 }.Serialize(report));
            return failed == 0 ? 0 : 1;
        }

        private static string[] RequiredSelfTestNames()
        {
            return new[]
            {
                "canonical_name_sha_inventory_digest",
                "exact_shared_stage_receipt_contract",
                "exact_shared_authorization_contract",
                "authorization_id_and_expiry_mutations_rejected",
                "planning_only_plan_requires_short_authorization",
                "direct_copy_preserves_75_names_and_74_nonroot_hashes",
                "relocation_initial_warning_allows_only_32_or_96_without_id_mismatch",
                "relocated_warning_64_requires_readonly_diagnostic_before_save",
                "warning64_assembly_component_diagnostic_fail_closed",
                "fresh_reopen_requires_exact_known_warning",
                "process_identity_ticks_are_exact_json_strings",
                "known_root_issue_requires_unique_reference_51",
                "exit_wait_retries_transient_unproven_until_exact_exit",
                "exit_grace_resolves_unproven_without_weakening_identity_gate",
                "monitor_cleanup_requires_recorded_exact_parent_command_and_identity",
                "monitor_global_zero_gate_waits_for_stable_empty_enumeration",
                "cad_session_quiescence_rejects_late_process_appearance",
                "top_cover_load_order_diagnostic_is_read_only_and_fail_closed",
                "top_cover_load_order_diagnostic_captures_root_context_read_only",
                "root_context_candidate_groups_are_read_only_and_conditional",
                "reference_feature_external_links_are_read_only_diagnostic_only",
                "dependency_inventory_ignores_only_paired_solidworks_lock_files",
                "top_cover_component_paths_match_exact_readonly_preload_set",
                "native_topcover_part_measurement_probe_is_nonconsumable",
                "native_topcover_part_detach_probe_is_nonconsumable",
                "native_topcover_part_measurement_completeness_fails_closed",
                "native_topcover_part_detach_contract_fails_closed",
                "native_topcover_rebuild_capture_probe_is_nonconsumable",
                "native_topcover_master_reference_capture_probe_is_retained_dormant",
                "native_topcover_front_brep_capture_probe_is_nonconsumable",
                "native_topcover_front_brep_contract_fails_closed",
                "native_topcover_rebuild_capture_contract_fails_closed",
                "native_topcover_capture_pack_rollback_is_exact_and_lock_aware",
                "native_topcover_transform_and_final_diagnostic_fail_closed",
                "native_topcover_part_measurement_private_evidence_serializes_written_true",
                "native_topcover_candidate_save_profile_is_exact",
                "semantic_commitment_matches_shared_vector",
                "json_bytes_replace_nonfinite_numbers_without_touching_strings",
                "ecmascript_json_cross_language_vectors",
                "evidence_and_receipt_are_paired_without_hash_cycle",
                "rollback_refuses_reparse_or_hardlink_escape",
                "rollback_requires_known_exact75_basename_subset",
                "specialized_lock_manifest_exact_and_mutations_rejected",
                "standard_manifest_source_without_stamp_is_supported"
            };
        }

        private static Dictionary<string, object> BuildStageReceiptContractFixture()
        {
            Dictionary<string, object> output = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < StageReceiptOrder.Length; index++)
                output[StageReceiptOrder[index]] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "schema", StageReceiptSchemas[index] },
                    { "path", StageReceiptPaths[index] },
                    { "producerToolId", StageReceiptTools[index] },
                    { "predecessor", StageReceiptPredecessors[index] }
                };
            return output;
        }

        private static Dictionary<string, object> BuildAuthorizationFixture(out Result result)
        {
            result = new Result
            {
                worker_id = "fixture-worker",
                task_id = "NATIVE-88814-FIXTURE",
                task_revision = 4,
                task_digest = new string('A', 64),
                request_fingerprint = new string('B', 64),
                request_digest = new string('C', 64),
                plan_sha256 = new string('D', 64),
                source_inventory_digest = ExpectedSourceInventoryDigest,
                tool_source_normalized_sha256 = new string('E', 64),
                tool_executable_sha256 = new string('F', 64),
                attempt = 2
            };
            result.plan_document = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "qualityBoundary", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "planningOnly", true },
                        { "engineeringAssistanceReady", false },
                        { "readyOnlyAfterEveryRequiredCheckPasses", true }
                    }
                },
                { "executionBoundary", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "executorImplemented", false }, { "cadStarted", false },
                        { "modelGenerated", false }, { "modelReady", false },
                        { "legacyFallbackUsed", false }
                    }
                }
            };
            Dictionary<string, object> authorization = new Dictionary<string, object>(
                StringComparer.Ordinal)
            {
                { "schema", AuthorizationSchema },
                { "authorizationId", "" },
                { "issuedAt", "2026-08-12T10:00:00.000Z" },
                { "expiresAt", "2026-08-12T10:10:00.000Z" },
                { "purpose", Purpose },
                { "workerId", result.worker_id },
                { "task", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", result.task_id }, { "revision", result.task_revision },
                        { "digest", result.task_digest }, { "leaseId", "lease-fixture" },
                        { "leaseExpiresAt", "2026-08-12T10:20:00.000Z" }
                    }
                },
                { "request", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "fingerprint", result.request_fingerprint },
                        { "digest", result.request_digest }
                    }
                },
                { "plan", new Dictionary<string, object>(StringComparer.Ordinal)
                    { { "sha256", result.plan_sha256 } } },
                { "recipe", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", ExpectedRecipeId }, { "version", 1 },
                        { "digest", ExpectedRecipeDigest }
                    }
                },
                { "tool", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", ToolId },
                        { "sourceNormalizedSha256", result.tool_source_normalized_sha256 },
                        { "executableSha256", result.tool_executable_sha256 }
                    }
                },
                { "seed", new Dictionary<string, object>(StringComparer.Ordinal)
                    { { "inventoryDigest", result.source_inventory_digest } } },
                { "execution", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "authorized", true }, { "attempt", result.attempt },
                        { "phases", new object[] { ExecutionPhase } }
                    }
                },
                { "qualityBoundary", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "engineeringAssistanceReady", false },
                        { "readyOnlyAfterEveryRequiredCheckPasses", true }
                    }
                }
            };
            authorization["authorizationId"] = ExpectedAuthorizationId(authorization);
            return authorization;
        }

        private static bool RunPairingSelfTest(string temp)
        {
            string pairDir = Path.Combine(temp, "pair");
            Directory.CreateDirectory(Path.Combine(pairDir, "evidence"));
            Directory.CreateDirectory(Path.Combine(pairDir, "receipts"));
            string evidencePath = Path.Combine(pairDir,
                EvidenceRelativePath.Replace('/', '\\'));
            string receiptPath = Path.Combine(pairDir,
                ReceiptRelativePath.Replace('/', '\\'));
            Dictionary<string, object> evidence = new Dictionary<string, object>(
                StringComparer.Ordinal)
            {
                { "schema", EvidenceSchema }, { "success", true },
                { "phase", ExecutionPhase }, { "semantic", 7 },
                { "completed_at_utc", "excluded" }
            };
            string commitment = EvidenceCommitmentDigest(evidence);
            evidence["evidence_commitment_sha256"] = commitment;
            WriteCreateNewAndReread(evidencePath, JsonBytes(evidence));
            string evidenceSha = Sha256File(evidencePath);
            Dictionary<string, object> receipt = SeedReceiptKeys.ToDictionary(key => key,
                key => (object)"", StringComparer.Ordinal);
            receipt["schema"] = ReceiptSchema;
            receipt["phase"] = ExecutionPhase;
            receipt["success"] = true;
            receipt["evidencePath"] = EvidenceRelativePath;
            receipt["evidenceSha256"] = evidenceSha;
            receipt["evidenceCommitmentSha256"] = commitment;
            receipt["predecessorReceiptSha256"] = "";
            WriteCreateNewAndReread(receiptPath, JsonBytes(receipt));
            bool valid = ValidatePairObjects(ReadJsonObject(evidencePath),
                ReadJsonObject(receiptPath), evidenceSha);
            Dictionary<string, object> forged = CloneObject(receipt);
            forged["evidenceSha256"] = new string('0', 64);
            bool forgedRejected = !ValidatePairObjects(evidence, forged, evidenceSha);
            evidence["completed_at_utc"] = "excluded-change";
            bool noCycle = EvidenceCommitmentDigest(evidence) == commitment &&
                !evidence.ContainsKey("phase_receipt_sha256") &&
                !evidence.ContainsKey("phase_receipt_path");
            return valid && forgedRejected && noCycle;
        }

        private static bool RunExitWaitSelfTest()
        {
            int transientCalls = 0;
            string recovered = WaitForVerifiedExit(() =>
            {
                transientCalls++;
                return transientCalls < 3 ? "unproven" : "exited";
            }, 100, 1);
            int persistentCalls = 0;
            string persistent = WaitForVerifiedExit(() =>
            {
                persistentCalls++;
                return "unproven";
            }, 5, 1);
            return recovered == "exited" && transientCalls == 3 &&
                persistent == "unproven" && persistentCalls > 1;
        }

        private static bool RunExitGraceSelfTest()
        {
            int exitedCalls = 0;
            string exited = ResolveUnprovenState(() =>
            {
                exitedCalls++;
                return exitedCalls < 3 ? "unproven" : "exited";
            }, 100, 1);
            int aliveCalls = 0;
            string alive = ResolveUnprovenState(() =>
            {
                aliveCalls++;
                return aliveCalls < 2 ? "unproven" : "alive_owned";
            }, 100, 1);
            int refusedCalls = 0;
            string refused = ResolveUnprovenState(() =>
            {
                refusedCalls++;
                return "unproven";
            }, 5, 1);
            return exited == "exited" && exitedCalls == 3 &&
                alive == "alive_owned" && aliveCalls == 2 &&
                refused == "unproven" && refusedCalls > 1;
        }

        private static bool RunRecordedMonitorOwnershipSelfTest()
        {
            ProcessIdentity monitor = new ProcessIdentity
            {
                pid = 4001,
                name = "sldProcMon",
                parent_pid = 3001,
                command_line = "sldProcMon --ppid=3001",
                executable_path = ExpectedMonitorExePath,
                executable_sha256 = ExpectedMonitorExeSha256,
                start_utc_ticks_value = 638000000000000000L
            };
            bool exact = RecordedMonitorOwnershipGate(monitor, 3001);
            monitor.command_line = "sldProcMon --ppid=30010";
            bool wrongCommandRejected = !RecordedMonitorOwnershipGate(monitor, 3001);
            monitor.command_line = "sldProcMon --ppid=3001";
            bool wrongParentRejected = !RecordedMonitorOwnershipGate(monitor, 3002);
            monitor.executable_sha256 = new string('0', 64);
            bool wrongHashRejected = !RecordedMonitorOwnershipGate(monitor, 3001);
            return exact && wrongCommandRejected && wrongParentRejected && wrongHashRejected;
        }

        private static bool RunGlobalProcessZeroSelfTest()
        {
            int transientCalls = 0;
            bool transient = WaitForEmptyProcessSet(() =>
            {
                transientCalls++;
                return transientCalls < 3 ? new List<int> { 42 } : new List<int>();
            }, 100, 1);
            int persistentCalls = 0;
            bool persistent = WaitForEmptyProcessSet(() =>
            {
                persistentCalls++;
                return new List<int> { 42 };
            }, 5, 1);
            return transient && transientCalls == 3 && !persistent && persistentCalls > 1;
        }

        private static bool RunCadSessionQuiescenceSelfTest()
        {
            int recoveredSldworksCalls = 0;
            int recoveredMonitorCalls = 0;
            bool recovered = WaitForGlobalCadQuiescence(
                () =>
                {
                    recoveredSldworksCalls++;
                    return new List<int>();
                },
                () =>
                {
                    recoveredMonitorCalls++;
                    return recoveredMonitorCalls == 1 || recoveredMonitorCalls > 3 ?
                        new List<int>() : new List<int> { 9001 };
                }, 100, 5, 1);
            int persistentSldworksCalls = 0;
            bool persistentSldworks = WaitForGlobalCadQuiescence(
                () =>
                {
                    persistentSldworksCalls++;
                    return new List<int> { 9002 };
                },
                () => new List<int>(), 5, 2, 1);
            int persistentMonitorCalls = 0;
            bool persistentMonitor = WaitForGlobalCadQuiescence(
                () => new List<int>(),
                () =>
                {
                    persistentMonitorCalls++;
                    return new List<int> { 9003 };
                }, 5, 2, 1);
            return recovered && recoveredSldworksCalls == recoveredMonitorCalls &&
                recoveredMonitorCalls > 4 && !persistentSldworks &&
                persistentSldworksCalls > 1 && !persistentMonitor &&
                persistentMonitorCalls > 1;
        }

        private static bool RunTopCoverLoadOrderDiagnosticSelfTest()
        {
            TopCoverLoadOrderDiagnostic valid = new TopCoverLoadOrderDiagnostic
            {
                cache_before_empty = true,
                preload_complete = true,
                preload_count = 7,
                additional_preload_complete = true,
                additional_preload_expected = 0,
                additional_preload_count = 0,
                assembly_opened = true,
                assembly_open_errors = 0,
                assembly_open_warnings = 96,
                assembly_read_only = true,
                assembly_rebuilt = true,
                assembly_component_complete = true,
                traversal_complete = true,
                root_opened = true,
                root_open_errors = 0,
                root_open_warnings = 96,
                root_read_only = true,
                root_rebuilt = true,
                root_hierarchy_gate = true,
                root_feature_gate = true,
                cache_after_empty = true,
                session_exit_proven = true,
                files_unchanged = true,
                fault = ""
            };
            bool warningIsDiagnosticNotAcceptance =
                TopCoverLoadOrderDiagnosticComplete(valid);
            valid.assembly_read_only = false;
            bool writableRejected = !TopCoverLoadOrderDiagnosticComplete(valid);
            valid.assembly_read_only = true;
            valid.files_unchanged = false;
            bool mutationRejected = !TopCoverLoadOrderDiagnosticComplete(valid);
            valid.files_unchanged = true;
            valid.fault = "unknown process remained";
            bool faultRejected = !TopCoverLoadOrderDiagnosticComplete(valid);
            return warningIsDiagnosticNotAcceptance && writableRejected &&
                mutationRejected && faultRejected;
        }

        private static bool RunTopCoverLoadOrderRootContextSelfTest()
        {
            TopCoverLoadOrderDiagnostic valid = new TopCoverLoadOrderDiagnostic
            {
                cache_before_empty = true,
                preload_complete = true,
                preload_count = 7,
                additional_preload_complete = true,
                additional_preload_expected = 0,
                additional_preload_count = 0,
                assembly_opened = true,
                assembly_open_errors = 0,
                assembly_open_warnings = 32,
                assembly_read_only = true,
                assembly_rebuilt = true,
                assembly_component_complete = true,
                traversal_complete = true,
                root_opened = true,
                root_open_errors = 0,
                root_open_warnings = 96,
                root_read_only = true,
                root_rebuilt = true,
                root_hierarchy_gate = true,
                root_feature_gate = true,
                cache_after_empty = true,
                session_exit_proven = true,
                files_unchanged = true,
                fault = ""
            };
            bool warningIsObservedWithoutAcceptance =
                TopCoverLoadOrderDiagnosticComplete(valid);
            valid.root_read_only = false;
            bool writableRootRejected = !TopCoverLoadOrderDiagnosticComplete(valid);
            valid.root_read_only = true;
            valid.root_hierarchy_gate = false;
            bool badHierarchyRejected = !TopCoverLoadOrderDiagnosticComplete(valid);
            valid.root_hierarchy_gate = true;
            valid.root_feature_gate = false;
            bool badFeatureGateRejected = !TopCoverLoadOrderDiagnosticComplete(valid);
            return warningIsObservedWithoutAcceptance && writableRootRejected &&
                badHierarchyRejected && badFeatureGateRejected;
        }

        private static bool RunRootContextCandidateGroupsSelfTest()
        {
            TopCoverLoadOrderDiagnostic valid = new TopCoverLoadOrderDiagnostic
            {
                cache_before_empty = true,
                preload_complete = true,
                preload_count = 7,
                additional_preload_complete = true,
                additional_preload_expected = 1,
                additional_preload_count = 1,
                assembly_opened = true,
                assembly_open_errors = 0,
                assembly_open_warnings = 32,
                assembly_read_only = true,
                assembly_rebuilt = true,
                assembly_component_complete = true,
                traversal_complete = true,
                root_opened = true,
                root_open_errors = 0,
                root_open_warnings = 96,
                root_read_only = true,
                root_rebuilt = true,
                root_hierarchy_gate = true,
                root_feature_gate = true,
                cache_after_empty = true,
                session_exit_proven = true,
                files_unchanged = true,
                fault = ""
            };
            bool bit64Continues = RootContextNeedsAnotherCandidate(valid);
            valid.root_open_warnings = 32;
            bool exact32Stops = !RootContextNeedsAnotherCandidate(valid);
            valid.root_open_warnings = 96;
            valid.additional_preload_complete = false;
            bool incompleteStops = !RootContextNeedsAnotherCandidate(valid);
            return bit64Continues && exact32Stops && incompleteStops;
        }

        private static bool RunDependencyInventoryLockFileSelfTest()
        {
            string root = Path.GetFullPath(@"C:\attempt\native_cad");
            string part = Path.Combine(root, "part.SLDPRT");
            string assembly = Path.Combine(root, "assembly.SLDASM");
            HashSet<string> closure = new HashSet<string>(new[] { part, assembly },
                StringComparer.OrdinalIgnoreCase);
            int lockCount;
            bool locksValid;
            List<string> missing;
            List<string> unexpected;
            bool pairedLocksAccepted = DependencyInventorySetGate(closure,
                new[] { part, assembly, Path.Combine(root, "~$part.SLDPRT"),
                    Path.Combine(root, "~$assembly.SLDASM") },
                out lockCount, out locksValid, out missing, out unexpected) &&
                lockCount == 2 && locksValid && missing.Count == 0 &&
                unexpected.Count == 0;
            bool orphanLockRejected = !DependencyInventorySetGate(closure,
                new[] { part, assembly, Path.Combine(root, "~$orphan.SLDPRT") },
                out lockCount, out locksValid, out missing, out unexpected) &&
                !locksValid;
            bool extraCadRejected = !DependencyInventorySetGate(closure,
                new[] { part, assembly, Path.Combine(root, "extra.SLDPRT") },
                out lockCount, out locksValid, out missing, out unexpected) &&
                missing.Count == 1;
            return pairedLocksAccepted && orphanLockRejected && extraCadRejected;
        }

        private static bool RunTopCoverComponentPathSetSelfTest()
        {
            string root = Path.GetFullPath(@"C:\attempt\native_cad");
            string[] expected = TopCoverCandidateUpstreamFirstFileNames.Skip(1)
                .Select(fileName => Path.Combine(root, fileName)).ToArray();
            bool exact = TopCoverComponentPathSetGate(expected, root);
            string[] wrongSameCount = expected.ToArray();
            wrongSameCount[0] = Path.Combine(root, "wrong.SLDPRT");
            bool wrongRejected = !TopCoverComponentPathSetGate(wrongSameCount, root);
            bool missingRejected = !TopCoverComponentPathSetGate(expected.Take(5), root);
            bool duplicateRejected = !TopCoverComponentPathSetGate(
                expected.Take(5).Concat(new[] { expected[0] }), root);
            return exact && wrongRejected && missingRejected && duplicateRejected;
        }

        private static bool RunRollbackSelfTest(string temp)
        {
            string attempt = Path.Combine(temp, "rollback-attempt");
            string target = Path.Combine(attempt, "native_cad", "working_pack");
            Directory.CreateDirectory(target);
            Dictionary<string, string> sourceHashes = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            string source = Path.Combine(target, "source.SLDPRT");
            string linked = Path.Combine(target, "linked.SLDPRT");
            sourceHashes[Path.GetFileName(source)] = "source";
            sourceHashes[Path.GetFileName(linked)] = "linked";
            File.WriteAllText(source, "hardlink", new UTF8Encoding(false));
            bool hardlinkCreated = CreateHardLink(linked, source, IntPtr.Zero);
            string reason;
            bool hardlinkRejected = hardlinkCreated && !RollbackPathSafe(target, attempt,
                sourceHashes, out reason);
            if (File.Exists(linked)) File.Delete(linked);
            if (File.Exists(source)) File.Delete(source);
            sourceHashes.Clear();
            string part = Path.Combine(target, "part.SLDPRT");
            string pairedLock = Path.Combine(target, "~$part.SLDPRT");
            sourceHashes[Path.GetFileName(part)] = "part";
            File.WriteAllText(part, "part", new UTF8Encoding(false));
            File.WriteAllText(pairedLock, "lock", new UTF8Encoding(false));
            bool pairedLockAccepted = RollbackPathSafe(target, attempt, sourceHashes, out reason);
            File.Delete(pairedLock);
            string orphanLock = Path.Combine(target, "~$orphan.SLDPRT");
            File.WriteAllText(orphanLock, "lock", new UTF8Encoding(false));
            bool orphanLockRejected = !RollbackPathSafe(target, attempt, sourceHashes, out reason);
            File.Delete(orphanLock);
            File.Delete(part);
            string outside = Path.Combine(temp, "outside");
            Directory.CreateDirectory(outside);
            bool escapeRejected = !RollbackPathSafe(outside, attempt, sourceHashes, out reason);
            Directory.CreateDirectory(Path.Combine(target, "nested"));
            bool nestedRejected = !RollbackPathSafe(target, attempt, sourceHashes, out reason);
            return hardlinkRejected && pairedLockAccepted && orphanLockRejected &&
                escapeRejected && nestedRejected;
        }

        private static bool RunRollbackKnownBasenameSubsetSelfTest()
        {
            Dictionary<string, string> source = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            source[RootFileName] = "root";
            for (int index = 1; index < ExpectedCadCount; index++)
                source["known-" + index.ToString("D2", CultureInfo.InvariantCulture) +
                    (index < ExpectedAssemblyCount ? ".SLDASM" : ".SLDPRT")] = "hash";
            string reason;
            bool knownSubset = RollbackCadBasenameSubsetSafe(new[]
                { RootFileName, "known-01.SLDASM" }, source, out reason);
            bool unknownCadRejected = !RollbackCadBasenameSubsetSafe(new[]
                { RootFileName, "unknown.SLDPRT" }, source, out reason);
            bool caseDuplicateRejected = !RollbackCadBasenameSubsetSafe(new[]
                { "known-01.SLDASM", "KNOWN-01.sldasm" }, source, out reason);
            return knownSubset && unknownCadRejected && caseDuplicateRejected;
        }

        private static bool RunSpecializedLockManifestSelfTest()
        {
            string repositoryRoot = FindRepositoryRoot(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(repositoryRoot)) return false;
            string manifestPath = ToolchainManifestPaths(repositoryRoot)[LockToolId];
            if (!File.Exists(manifestPath)) return false;
            Dictionary<string, object> manifest = ReadJsonObject(manifestPath);
            if (!ValidateSpecializedLockToolchainManifest(repositoryRoot, manifest)) return false;

            Dictionary<string, object> extraTopKey = CloneObject(manifest);
            extraTopKey["forged"] = true;
            Dictionary<string, object> wrongGenerator = CloneObject(manifest);
            wrongGenerator["generatedBy"] = "forged.mjs";
            Dictionary<string, object> missingInspector = CloneObject(manifest);
            ChildObject(missingInspector, "tools").Remove("native_lock_topology_inspector_v1");
            Dictionary<string, object> wrongToolPath = CloneObject(manifest);
            ChildObject(ChildObject(wrongToolPath, "tools"), LockToolId)["sourcePath"] =
                Path.Combine(repositoryRoot, "workers", "native_model_requests", "development",
                    "v1", "tools", "InspectLockTopology888x14.cs");
            Dictionary<string, object> wrongToolHash = CloneObject(manifest);
            ChildObject(ChildObject(wrongToolHash, "tools"), LockToolId)
                ["executableSha256"] = new string('0', 64);
            Dictionary<string, object> wrongValidator = CloneObject(manifest);
            ChildObject(wrongValidator, "validator")["sha256"] = new string('0', 64);
            return !ValidateSpecializedLockToolchainManifest(repositoryRoot, extraTopKey) &&
                !ValidateSpecializedLockToolchainManifest(repositoryRoot, wrongGenerator) &&
                !ValidateSpecializedLockToolchainManifest(repositoryRoot, missingInspector) &&
                !ValidateSpecializedLockToolchainManifest(repositoryRoot, wrongToolPath) &&
                !ValidateSpecializedLockToolchainManifest(repositoryRoot, wrongToolHash) &&
                !ValidateSpecializedLockToolchainManifest(repositoryRoot, wrongValidator);
        }

        private static bool FreshReopenWarningAllowed(int errors, int warnings)
        {
            return errors == 0 && InitialRelocatedWarningAllowed(warnings);
        }

        private static Dictionary<string, object> CloneObject(Dictionary<string, object> value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer
                { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            return serializer.DeserializeObject(serializer.Serialize(value)) as
                Dictionary<string, object>;
        }

        private static List<string> CaptureFlatCadTree(string directory, string status)
        {
            Require(Directory.Exists(directory), status, 3, directory + " does not exist");
            Require(!HasReparsePoint(directory) &&
                Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly).Length == 0,
                status, 3, directory + " must be a flat non-reparse directory");
            string canonical = CanonicalExistingPath(directory);
            string[] all = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            Require(all.All(path => (IsAssembly(path) || IsPart(path)) &&
                !HasReparsePoint(path) && FileLinkCount(path) == 1 &&
                IsUnder(CanonicalExistingPath(path), canonical)), status, 3,
                directory + " contains a non-CAD, linked, reparse or escaped file");
            Require(all.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                all.Length, status, 3, directory + " contains a case-insensitive name collision");
            return all.Select(Path.GetFullPath).OrderBy(path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static FileSnapshot Snapshot(string path)
        {
            FileInfo file = new FileInfo(path);
            return new FileSnapshot
            {
                path = file.FullName,
                name = file.Name,
                size = file.Length,
                sha256 = Sha256File(file.FullName)
            };
        }

        private static string InventoryDigest(IEnumerable<FileSnapshot> rows)
        {
            string body = string.Join("\n", rows.OrderBy(row => row.name,
                StringComparer.OrdinalIgnoreCase).Select(row => row.name + "|" +
                row.sha256.ToUpperInvariant()).ToArray()) + "\n";
            return Sha256Text(body);
        }

        private static Dictionary<string, object> ObjectFrom(object value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer
                { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
            return serializer.DeserializeObject(serializer.Serialize(value)) as
                Dictionary<string, object>;
        }

        private static byte[] JsonBytes(object value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer
                { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
            string json = NormalizeNonFiniteJsonNumbers(serializer.Serialize(value));
            return new UTF8Encoding(false).GetBytes(json + "\n");
        }

        private static string NormalizeNonFiniteJsonNumbers(string json)
        {
            if (string.IsNullOrEmpty(json)) return json ?? "";
            StringBuilder output = new StringBuilder(json.Length);
            bool insideString = false;
            bool escaped = false;
            for (int index = 0; index < json.Length; index++)
            {
                char current = json[index];
                if (insideString)
                {
                    output.Append(current);
                    if (escaped) escaped = false;
                    else if (current == '\\') escaped = true;
                    else if (current == '"') insideString = false;
                    continue;
                }
                if (current == '"')
                {
                    insideString = true;
                    output.Append(current);
                    continue;
                }
                string token = JsonNonFiniteTokenAt(json, index);
                if (!string.IsNullOrEmpty(token) && JsonTokenBoundaryBefore(json, index) &&
                    JsonTokenBoundaryAfter(json, index + token.Length))
                {
                    output.Append("null");
                    index += token.Length - 1;
                    continue;
                }
                output.Append(current);
            }
            return output.ToString();
        }

        private static string JsonNonFiniteTokenAt(string json, int index)
        {
            string[] tokens = { "-Infinity", "Infinity", "NaN" };
            foreach (string token in tokens)
                if (index + token.Length <= json.Length && string.CompareOrdinal(json, index,
                        token, 0, token.Length) == 0) return token;
            return "";
        }

        private static bool JsonTokenBoundaryBefore(string json, int index)
        {
            if (index <= 0) return true;
            char value = json[index - 1];
            return value == ':' || value == ',' || value == '[' || char.IsWhiteSpace(value);
        }

        private static bool JsonTokenBoundaryAfter(string json, int index)
        {
            if (index >= json.Length) return true;
            char value = json[index];
            return value == ',' || value == ']' || value == '}' || char.IsWhiteSpace(value);
        }

        private static Dictionary<string, object> ReadJsonObject(string path)
        {
            return ReadJsonObjectBytes(File.ReadAllBytes(path));
        }

        private static Dictionary<string, object> ReadJsonObjectBytes(byte[] bytes)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer
                { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
            return serializer.DeserializeObject(new UTF8Encoding(false, true)
                .GetString(bytes)) as Dictionary<string, object>;
        }

        private static Dictionary<string, object> ChildObject(Dictionary<string, object> value,
            string key)
        {
            object raw;
            return value != null && value.TryGetValue(key, out raw) ?
                raw as Dictionary<string, object> : null;
        }

        private static object[] ArrayValue(Dictionary<string, object> value, string key)
        {
            object raw;
            IEnumerable enumerable;
            if (value == null || !value.TryGetValue(key, out raw) || raw == null ||
                raw is string || (enumerable = raw as IEnumerable) == null) return new object[0];
            return enumerable.Cast<object>().ToArray();
        }

        private static string Text(Dictionary<string, object> value, string key)
        {
            object raw;
            return value != null && value.TryGetValue(key, out raw) && raw != null ?
                Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "" : "";
        }

        private static string ValueText(object value)
        {
            return value == null ? "" : Convert.ToString(value,
                CultureInfo.InvariantCulture) ?? "";
        }

        private static double Number(Dictionary<string, object> value, string key)
        {
            object raw;
            double output;
            return value != null && value.TryGetValue(key, out raw) && raw != null &&
                !(raw is bool) && double.TryParse(Convert.ToString(raw,
                    CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out output) ? output : double.NaN;
        }

        private static bool Bool(Dictionary<string, object> value, string key)
        {
            object raw;
            return value != null && value.TryGetValue(key, out raw) && raw is bool && (bool)raw;
        }

        private static bool ExactKeys(Dictionary<string, object> value, IEnumerable<string> expected)
        {
            if (value == null || expected == null) return false;
            string[] rows = expected.ToArray();
            return value.Count == rows.Length && value.Keys.OrderBy(key => key,
                StringComparer.Ordinal).SequenceEqual(rows.OrderBy(key => key,
                    StringComparer.Ordinal), StringComparer.Ordinal);
        }

        private static bool StringArrayEquals(object[] actual, string[] expected)
        {
            return actual != null && expected != null && actual.Select(ValueText)
                .SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static List<string> Strings(object raw)
        {
            IEnumerable enumerable = raw as IEnumerable;
            if (enumerable == null || raw is string) return new List<string>();
            List<string> output = new List<string>();
            foreach (object value in enumerable) output.Add(ValueText(value));
            return output;
        }

        private static object[] ObjectArray(object raw)
        {
            IEnumerable enumerable = raw as IEnumerable;
            if (enumerable == null || raw is string) return new object[0];
            return enumerable.Cast<object>().ToArray();
        }

        private static string Argument(string[] args, string name)
        {
            for (int index = 0; index < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return index + 1 < args.Length ? args[index + 1] : null;
            return null;
        }

        private static bool IsAssembly(string path)
        {
            return string.Equals(Path.GetExtension(path), ".SLDASM",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPart(string path)
        {
            return string.Equals(Path.GetExtension(path), ".SLDPRT",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSha256(string value)
        {
            return value != null && Regex.IsMatch(value, "^[A-Fa-f0-9]{64}$",
                RegexOptions.CultureInvariant);
        }

        private static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160 ||
                !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9_.-]*$",
                    RegexOptions.CultureInvariant)) return false;
            string stem = value.Split('.')[0].ToUpperInvariant();
            string[] reserved = { "CON", "PRN", "AUX", "NUL", "CLOCK$", "COM1", "COM2",
                "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2",
                "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            return !reserved.Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsCanonicalRelativeJsonPath(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf('\\') < 0 &&
                value.IndexOf("..", StringComparison.Ordinal) < 0 &&
                Regex.IsMatch(value, "^(receipts|evidence)/[A-Za-z0-9_.-]+\\.json$",
                    RegexOptions.CultureInvariant);
        }

        private static bool IsSafeRepositoryRelativePath(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
                value.IndexOf('\\') < 0 && value.Split('/').All(segment =>
                    segment != "" && segment != "." && segment != "..");
        }

        private static string FullPathOrEmpty(string value)
        {
            try { return string.IsNullOrWhiteSpace(value) ? "" : Path.GetFullPath(value.Trim()); }
            catch { return ""; }
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            return string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
                Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string candidate = Path.GetFullPath(path).TrimEnd('\\', '/');
            string parent = Path.GetFullPath(root).TrimEnd('\\', '/');
            return candidate.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string RelativeUnder(string path, string root, string status)
        {
            Require(IsUnder(path, root), status, 3, path + " is outside " + root);
            return Path.GetFullPath(path).Substring(Path.GetFullPath(root).TrimEnd('\\', '/').Length)
                .TrimStart('\\', '/');
        }

        private static void AssertPathChainNoReparse(string path, string stopAt, string status)
        {
            Require(IsUnder(path, stopAt) || SamePath(path, stopAt), status, 3,
                "path escaped trusted root");
            string current = Path.GetFullPath(path);
            string root = Path.GetFullPath(stopAt).TrimEnd('\\', '/');
            int guard = 0;
            while (guard++ < 40)
            {
                if (File.Exists(current) || Directory.Exists(current))
                    Require(!HasReparsePoint(current), status, 3,
                        "path contains reparse point: " + current);
                if (SamePath(current, root)) return;
                string parent = Path.GetDirectoryName(current);
                Require(!string.IsNullOrWhiteSpace(parent) && !SamePath(parent, current),
                    status, 3, "path chain did not reach root");
                current = parent;
            }
            Require(false, status, 3, "path traversal exceeded guard");
        }

        private static bool PathChainSafe(string path, string stopAt)
        {
            try
            {
                if (!IsUnder(path, stopAt) && !SamePath(path, stopAt)) return false;
                string current = Path.GetFullPath(path);
                int guard = 0;
                while (guard++ < 40)
                {
                    if ((File.Exists(current) || Directory.Exists(current)) &&
                        HasReparsePoint(current)) return false;
                    if (SamePath(current, stopAt)) return true;
                    string parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrWhiteSpace(parent) || SamePath(parent, current)) return false;
                    current = parent;
                }
            }
            catch { }
            return false;
        }

        private static string CanonicalExistingPath(string path)
        {
            using (SafeFileHandle handle = CreateFile(path, 0, FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero, 3, Directory.Exists(path) ? 0x02000000u : 0u, IntPtr.Zero))
            {
                if (handle == null || handle.IsInvalid) return "";
                StringBuilder builder = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(handle, builder,
                    (uint)builder.Capacity, 0);
                if (length == 0 || length >= builder.Capacity) return "";
                string value = builder.ToString();
                if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    return @"\\" + value.Substring(8);
                if (value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    return value.Substring(4);
                return value;
            }
        }

        private static bool TreeHasReparsePoint(string directory)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (HasReparsePoint(current)) return true;
                foreach (string entry in Directory.GetFileSystemEntries(current, "*",
                    SearchOption.TopDirectoryOnly))
                {
                    if (HasReparsePoint(entry)) return true;
                    if (Directory.Exists(entry)) pending.Push(entry);
                }
            }
            return false;
        }

        private static bool HasReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        private static string FindRepositoryRoot(string start)
        {
            DirectoryInfo current = new FileInfo(start).Directory;
            int guard = 0;
            while (current != null && guard++ < 24)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
                current = current.Parent;
            }
            return "";
        }

        private enum CaptureIdentityResult
        {
            Captured,
            Exited,
            Unproven
        }

        private static CaptureIdentityResult CaptureProcessIdentity(int pid, string expectedName,
            out ProcessIdentity identity)
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
                    long startTicks = process.StartTime.ToUniversalTime().Ticks;
                    if (string.IsNullOrWhiteSpace(executable) || startTicks <= 0 ||
                        !File.Exists(executable)) return CaptureIdentityResult.Unproven;
                    identity = new ProcessIdentity
                    {
                        pid = pid,
                        name = expectedName,
                        start_utc_ticks_value = startTicks,
                        executable_path = Path.GetFullPath(executable),
                        executable_sha256 = Sha256File(executable)
                    };
                    return CaptureIdentityResult.Captured;
                }
            }
            catch (ArgumentException)
            {
                return CaptureIdentityResult.Exited;
            }
            catch
            {
                return CaptureIdentityResult.Unproven;
            }
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
                    "SELECT ProcessId,ParentProcessId,Name,CommandLine,ExecutablePath FROM Win32_Process WHERE Name='" +
                    name.Replace("'", "''") + ".exe'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        int pid = Convert.ToInt32(row["ProcessId"], CultureInfo.InvariantCulture);
                        ProcessIdentity identity;
                        if (!TryCaptureProcessIdentity(pid, name, out identity)) continue;
                        identity.parent_pid = Convert.ToInt32(row["ParentProcessId"],
                            CultureInfo.InvariantCulture);
                        identity.command_line = Convert.ToString(row["CommandLine"],
                            CultureInfo.InvariantCulture) ?? "";
                        string wmiPath = Convert.ToString(row["ExecutablePath"],
                            CultureInfo.InvariantCulture) ?? "";
                        if (string.IsNullOrWhiteSpace(wmiPath) ||
                            !SamePath(wmiPath, identity.executable_path)) continue;
                        output.Add(identity);
                    }
                }
            }
            catch { }
            return output;
        }

        private static string IdentityKey(ProcessIdentity identity)
        {
            return identity == null ? "" : identity.pid.ToString(CultureInfo.InvariantCulture) + "|" +
                identity.start_utc_ticks_value.ToString(CultureInfo.InvariantCulture);
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

        private static string Sha256File(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private static string Sha256Bytes(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(value)).Replace("-", "");
        }

        private static string Sha256Text(string value)
        {
            return Sha256Bytes(new UTF8Encoding(false).GetBytes(value));
        }

        private static string NormalizedSourceSha256()
        {
            string sourcePath = Path.Combine(Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location),
                "SeedPack888Native.cs");
            Require(File.Exists(sourcePath), "TOOL_SOURCE_MISSING", 3,
                "SeedPack888Native.cs must remain beside the executable");
            return NormalizedSourceHashAt(sourcePath);
        }

        private static string NormalizedSourceHashAt(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n")
                .Replace("\r", "\n");
            const string sourceStampPattern =
                "(private\\s+const\\s+string\\s+ExpectedSourceSha256\\s*=\\s*\")" +
                "(?:__SOURCE_SHA256__|[A-F0-9]{64})(\"\\s*;)";
            Require(Regex.Matches(source, sourceStampPattern,
                    RegexOptions.CultureInvariant).Count == 1,
                "TOOL_SOURCE_STAMP_INVALID", 3,
                "source must contain exactly one recognized ExpectedSourceSha256 stamp: " + path);
            return NormalizedStandardManifestSourceHashAt(path);
        }

        private static string NormalizedStandardManifestSourceHashAt(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n")
                .Replace("\r", "\n");
            const string sourceStampPattern =
                "(private\\s+const\\s+string\\s+ExpectedSourceSha256\\s*=\\s*\")" +
                "(?:__SOURCE_SHA256__|[A-F0-9]{64})(\"\\s*;)";
            Require(Regex.Matches(source, sourceStampPattern,
                    RegexOptions.CultureInvariant).Count <= 1,
                "TOOL_SOURCE_STAMP_INVALID", 3,
                "standard tool source contains ambiguous ExpectedSourceSha256 stamps: " + path);
            source = Regex.Replace(source, sourceStampPattern,
                "$1__SOURCE_SHA256__$2", RegexOptions.CultureInvariant);
            const string contractStampPattern =
                "(private\\s+const\\s+string\\s+ExpectedContractSnapshotSha256\\s*=\\s*\")" +
                "(?:__CONTRACT_SHA256__|[A-F0-9]{64})(\"\\s*;)";
            Require(Regex.Matches(source, contractStampPattern,
                    RegexOptions.CultureInvariant).Count <= 1,
                "TOOL_CONTRACT_STAMP_INVALID", 3,
                "standard tool source contains ambiguous contract snapshot stamps: " + path);
            source = Regex.Replace(source, contractStampPattern,
                "$1__CONTRACT_SHA256__$2", RegexOptions.CultureInvariant);
            return Sha256Text(source);
        }

        private static string NormalizedLockSourceHashAt(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            const string sourceStampPattern =
                "(private\\s+const\\s+string\\s+ExpectedSourceSha256\\s*=\\s*\")" +
                "(?:__SOURCE_SHA256__|[A-F0-9]{64})(\"\\s*;)";
            Require(Regex.Matches(source, sourceStampPattern,
                    RegexOptions.CultureInvariant).Count <= 1,
                "TOOL_SOURCE_STAMP_INVALID", 3,
                "lock tool source contains ambiguous ExpectedSourceSha256 stamps: " + path);
            source = Regex.Replace(source, sourceStampPattern,
                "$1__SOURCE_SHA256__$2", RegexOptions.CultureInvariant);
            return Sha256Text(source);
        }

        private static bool ParseUtc(string value, out DateTime output)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out output);
        }

        private static string SafeException(Exception exception)
        {
            return exception == null ? "" : exception.GetType().FullName + ": " +
                exception.Message;
        }

        private static void Require(bool condition, string status, int exitCode, string message)
        {
            if (!condition) throw new StageException(status, exitCode, message);
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

        private static void ReleaseOneComReference(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.ReleaseComObject(value); }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint fileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME creationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME lastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME lastWriteTime;
            public uint volumeSerialNumber;
            public uint fileSizeHigh;
            public uint fileSizeLow;
            public uint numberOfLinks;
            public uint fileIndexHigh;
            public uint fileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(IntPtr handle,
            out ByHandleFileInformation information);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, FileShare share,
            IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,
            [Out] StringBuilder path, uint size, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string newFileName, string existingFileName,
            IntPtr securityAttributes);

        private static int FileLinkCount(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    ByHandleFileInformation information;
                    return GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(),
                        out information) ? checked((int)information.numberOfLinks) : -1;
                }
            }
            catch { return -1; }
        }
    }

    internal sealed class StageException : Exception
    {
        public readonly string Status;
        public readonly int ExitCode;
        public StageException(string status, int exitCode, string message) : base(message)
        {
            Status = status;
            ExitCode = exitCode;
        }
    }

    internal sealed class SelfTestReport
    {
        public string status = "";
        public bool cadStarted;
        public int checksTotal;
        public int checksFailed;
        public Dictionary<string, bool> checks = new Dictionary<string, bool>();
        public string crossLanguageCanonicalJson = "";
        public string crossLanguageCanonicalJsonSha256 = "";
    }

    internal sealed class AuthorizationBinding
    {
        public string authorization_id = "";
        public string issued_at = "";
        public string expires_at = "";
        public string lease_id = "";
        public string lease_expires_at = "";
    }

    internal sealed class FileSnapshot
    {
        public string path = "";
        public string name = "";
        public long size;
        public string sha256 = "";
    }

    internal sealed class TrustedManifestEvidence
    {
        public string id = "";
        public string path = "";
        public string sha256 = "";
    }

    internal sealed class AuthorizationCheckpoint
    {
        public string name = "";
        public string checked_at_utc = "";
        public bool valid;
    }

    internal sealed class ProcessIdentity
    {
        public int pid;
        public string name = "";
        [System.Web.Script.Serialization.ScriptIgnore]
        public long start_utc_ticks_value;
        public string start_utc_ticks
        {
            get { return start_utc_ticks_value.ToString(CultureInfo.InvariantCulture); }
        }
        public string executable_path = "";
        public string executable_sha256 = "";
        public int parent_pid;
        public string command_line = "";
        public string exit_state = "not_checked";
    }

    internal sealed class SessionEvidence
    {
        public string purpose = "";
        public bool created;
        public string revision = "";
        public ProcessIdentity sldworks = new ProcessIdentity();
        public List<ProcessIdentity> monitors = new List<ProcessIdentity>();
        public bool exit_requested;
        public string exit_state = "not_checked";
        public bool monitors_exit_proven;
    }

    internal sealed class ProcessEvidence
    {
        public List<int> baseline_sldworks = new List<int>();
        public List<int> baseline_sldprocmon = new List<int>();
        public List<int> force_stopped_owned_process_ids = new List<int>();
        public List<int> final_sldworks = new List<int>();
        public List<int> final_sldprocmon = new List<int>();
        public List<string> cleanup_errors = new List<string>();
        public bool final_gate;
    }

    internal sealed class HierarchyEvidence
    {
        public bool traversal_complete;
        public int top_level_component_count;
        public int recursive_component_count;
        public int active_component_count;
        public int max_depth;
        public int unresolved_active_component_count;
        public int lightweight_component_count;
        public int internal_id_mismatch_count;
        public int nonlocal_or_missing_path_count;
    }

    internal sealed class DependencyEvidence
    {
        public int raw_count;
        public int closure_count;
        public bool all_target_local;
        public bool exact_inventory_set;
        public int transient_lock_file_count;
        public bool transient_lock_files_valid;
        public List<string> paths = new List<string>();
        public List<string> missing_from_closure = new List<string>();
        public List<string> unexpected_in_closure = new List<string>();
    }

    internal sealed class FeatureIssueEvidence
    {
        public string name = "";
        public string type = "";
        public int error_code;
        public int error_code2;
        public bool warning;
        public int depth;
    }

    internal sealed class FeatureHealthEvidence
    {
        public bool traversal_complete;
        public int feature_count;
        public List<FeatureIssueEvidence> issues = new List<FeatureIssueEvidence>();
    }

    internal sealed class FreshComponentDiagnostic
    {
        public string path = "";
        public bool path_local;
        public bool cache_before_empty;
        public bool opened;
        public int open_errors;
        public int open_warnings;
        public bool read_only;
        public bool rebuilt;
        public bool traversal_complete;
        public int feature_count;
        public bool cache_after_empty;
        public bool session_exit_proven;
        public bool file_unchanged;
        public string fault = "";
        public string summary = "";
    }

    internal sealed class TopCoverLoadOrderDiagnostic
    {
        public string label = "";
        public bool cache_before_empty;
        public bool preload_complete;
        public int preload_count;
        public bool additional_preload_complete;
        public int additional_preload_expected;
        public int additional_preload_count;
        public bool assembly_opened;
        public int assembly_open_errors;
        public int assembly_open_warnings;
        public bool assembly_read_only;
        public bool assembly_rebuilt;
        public bool assembly_component_complete;
        public bool traversal_complete;
        public bool root_opened;
        public int root_open_errors;
        public int root_open_warnings;
        public bool root_read_only;
        public bool root_rebuilt;
        public bool root_hierarchy_gate;
        public bool root_feature_gate;
        public bool cache_after_empty;
        public bool session_exit_proven;
        public bool files_unchanged;
        public string fault = "";
        public string summary = "";
    }

    internal sealed class QualityBoundary
    {
        public string purpose = "";
        public bool engineeringAssistanceReady;
        public bool downstreamStagesRequired;
        public bool structuralEngineerReviewRequired;
    }

    internal sealed class NativeTopCoverComponent
    {
        public string path = "";
        public string name = "";
        public string sha256 = "";
        public string configuration = "";
        public List<double> transform16 = new List<double>();
        public bool originallyFixed;
        public int suppression;
        public bool suppressed;
        public int directChildCount;
    }

    internal sealed class NativeTopCoverTemplate
    {
        public string path = "";
        public int openErrors;
        public int openWarnings;
        public bool exactSix;
        public bool fileUnchanged;
        public bool sessionExitProven;
        public List<NativeTopCoverComponent> components = new List<NativeTopCoverComponent>();
    }

    internal sealed class NativeTopCoverProbeEvidence
    {
        public bool probeOnly;
        public bool consumable;
        public bool probePassed;
        public bool privateEvidenceWritten;
        public string failureStatus = "";
        public int failureExitCode;
        public string failure = "";
        public string completedAtUtc = "";
        public string protectedSourceInventoryDigestBefore = "";
        public string sourceDirectory = "";
        public string workingPack = "";
        public NativeTopCoverTemplate template = new NativeTopCoverTemplate();
        public bool candidateCreated;
        public string candidateSha256 = "";
        public List<string> candidateComponentOpenProfiles = new List<string>();
        public bool candidateSaveAttempted;
        public bool candidateSaveReturned;
        public int candidateSaveErrors;
        public int candidateSaveWarnings;
        public bool candidateFileExistsAfterSave;
        public bool candidateDirtyAfterSave;
        public bool candidateSaveProfileAllowed;
        public int candidateFreshErrors;
        public int candidateFreshWarnings;
        public bool candidateFreshReadOnly;
        public bool candidateFreshRebuilt;
        public FeatureHealthEvidence candidateFeatureHealth = new FeatureHealthEvidence();
        public int candidateFeatureIssueCount;
        public bool candidateRelocationFeatureTraversalComplete;
        public int candidateRelocationFeatureCount;
        public List<string> candidateRelocationFeatureIssues = new List<string>();
        public List<string> candidateLinkFeatures = new List<string>();
        public bool candidateComponentDiagnosticComplete;
        public List<string> candidateComponentDiagnostics = new List<string>();
        public List<string> candidateDiagnosticComponentPaths = new List<string>();
        public string candidateExternalReferenceDiagnostic = "";
        public int candidateClosureCount;
        public List<string> candidateClosurePaths = new List<string>();
        public bool candidateFreshPassed;
        public bool candidateFileUnchanged;
        public bool candidateSessionExitProven;
        public List<NativeTopCoverComponent> candidateComponents = new List<NativeTopCoverComponent>();
        public bool swapCompleted;
        public int initialRootErrors;
        public int initialRootWarnings;
        public HierarchyEvidence initialRootHierarchy = new HierarchyEvidence();
        public DependencyEvidence initialRootDependencies = new DependencyEvidence();
        public List<string> initialMismatchPaths = new List<string>();
        public bool initialRootReadonlyHashesUnchanged;
        public bool rootIdAcceptanceSaveAttempted;
        public bool rootIdAcceptanceSaved;
        public int rootIdAcceptanceSaveErrors;
        public int rootIdAcceptanceSaveWarnings;
        public string rootShaBeforeIdAcceptance = "";
        public string rootShaAfterIdAcceptance = "";
        public int finalRootErrors;
        public int finalRootWarnings;
        public HierarchyEvidence finalRootHierarchy = new HierarchyEvidence();
        public FeatureHealthEvidence finalRootFeature = new FeatureHealthEvidence();
        public DependencyEvidence finalRootDependencies = new DependencyEvidence();
        public string finalRelocationDiagnostic = "";
        public bool finalReadonlyHashesUnchanged;
        public string targetInventoryDigest = "";
        public List<string> changedNames = new List<string>();
        public bool protectedSourceUnchanged;
    }

    internal sealed class NativeTopCoverBodyMeasurement
    {
        public int index;
        public List<double> boxMm = new List<double>();
        public double massKg;
        public double volumeMm3;
        public double surfaceAreaMm2;
        public List<double> massPropertiesSi = new List<double>();
        public int faceCount;
        public int edgeCount;
        public bool complete;
        public string fault = "";
    }

    internal sealed class NativeTopCoverFeatureMeasurement
    {
        public string name = "";
        public string path = "";
        public string type = "";
        public int depth;
        public int errorCode;
        public int errorCode2;
        public bool warning;
        public bool suppressed;
    }

    internal sealed class NativeTopCoverSheetMetalMeasurement
    {
        public string path = "";
        public string type = "";
        public bool readable;
        public bool accessSelections;
        public double thicknessMm;
        public double bendRadiusMm;
        public double kFactor;
        public int bendAllowanceType = -1;
        public bool customBendAllowanceAvailable;
        public int customBendAllowanceType = -1;
        public double customKFactor = double.NaN;
        public double customBendAllowanceMm = double.NaN;
        public double customBendDeductionMm = double.NaN;
        public string customBendTableFile = "";
        public int effectiveBendAllowanceType = -1;
        public string fault = "";
    }

    internal sealed class NativeTopCoverPartMeasurement
    {
        public string name = "";
        public string path = "";
        public bool pathLocal;
        public long sourceSize;
        public string sourceSha256 = "";
        public bool cacheBeforeEmpty;
        public bool opened;
        public int openErrors;
        public int openWarnings;
        public bool readOnly;
        public bool saveFlagBeforeCaptured;
        public bool saveFlagBeforeRebuild;
        public bool saveFlagAfterCaptured;
        public bool saveFlagAfterRebuild;
        public string configuration = "";
        public bool rebuilt;
        public int bodyCount;
        public List<double> partBoxApiRaw = new List<double>();
        public List<double> partBoxMm = new List<double>();
        public bool partBoxDerivedFromBodies;
        public List<NativeTopCoverBodyMeasurement> bodies =
            new List<NativeTopCoverBodyMeasurement>();
        public bool featureTraversalComplete = true;
        public List<NativeTopCoverFeatureMeasurement> features =
            new List<NativeTopCoverFeatureMeasurement>();
        public Dictionary<string, int> typeCounts = new Dictionary<string, int>();
        public List<NativeTopCoverSheetMetalMeasurement> sheetMetalFeatures =
            new List<NativeTopCoverSheetMetalMeasurement>();
        public bool relocationFeatureTraversalComplete;
        public int relocationFeatureCount;
        public List<string> featureIssues = new List<string>();
        public List<string> linkFeatures = new List<string>();
        public int modelExternalReferenceCount = -1;
        public int extensionExternalReferenceCount = -1;
        public string geometrySignatureSha256 = "";
        public string physicalGeometrySignatureSha256 = "";
        public bool cacheAfterEmpty;
        public bool fileUnchanged;
        public string geometryFault = "";
        public string fault = "";
    }

    internal sealed class NativeTopCoverTemplateComponentState
    {
        public string name = "";
        public string path = "";
        public string sha256 = "";
        public string configuration = "";
        public bool hidden;
        public bool hiddenCaptured;
        public int visible = -1;
        public bool visibleCaptured;
        public bool envelope;
        public bool envelopeCaptured;
        public bool excludeFromBom;
        public bool excludeFromBomCaptured;
        public bool suppressed;
        public bool suppressedCaptured;
        public int suppression = -1;
        public bool fixedState;
        public bool fixedCaptured;
        public List<double> transform16 = new List<double>();
        public int directChildCount;
    }

    internal sealed class NativeTopCoverPartMeasurementEvidence
    {
        public string schema = "winnsen.16029.topcover_native_part_measurement_probe.v1";
        public bool probeOnly;
        public bool consumable;
        public string mode = "";
        public bool privateEvidenceWritten;
        public string completedAtUtc = "";
        public string protectedSourceInventoryDigestBefore = "";
        public string sourceDirectory = "";
        public string workingPack = "";
        public List<string> exactSix = new List<string>();
        public bool exactSixSourceBindings;
        public List<NativeTopCoverPartMeasurement> parts =
            new List<NativeTopCoverPartMeasurement>();
        public List<NativeTopCoverTemplateComponentState> templateComponents =
            new List<NativeTopCoverTemplateComponentState>();
        public bool templateCacheBeforeEmpty;
        public int templateOpenErrors;
        public int templateOpenWarnings;
        public bool templateReadOnly;
        public bool templateRebuilt;
        public int templateModelExternalReferenceCount = -1;
        public int templateExtensionExternalReferenceCount = -1;
        public bool templateCacheAfterEmpty;
        public bool templateFileUnchanged;
        public bool templateComponentsComplete;
        public bool sessionExitProven;
        public bool sourceExact75Unchanged;
        public bool workingPackExact75Unchanged;
        public bool allMeasurementsComplete;
        public string failureStatus = "";
        public int failureExitCode;
        public string failure = "";
    }

    internal sealed class NativeTopCoverPartDetachOperation
    {
        public string name = "";
        public string path = "";
        public string sourceHashBefore = "";
        public bool sourceBindingBefore;
        public bool cacheBeforeEmpty;
        public bool opened;
        public int openErrors;
        public int openWarnings;
        public int expectedExternalBefore = -1;
        public int modelExternalBefore = -1;
        public int extensionExternalBefore = -1;
        public bool breakCalled;
        public bool breakSucceeded;
        public bool rebuilt;
        public int modelExternalAfter = -1;
        public int extensionExternalAfter = -1;
        public FeatureHealthEvidence featureHealth = new FeatureHealthEvidence();
        public bool saveSucceeded;
        public int saveErrors;
        public int saveWarnings;
        public bool dirtyAfterSave;
        public bool cacheAfterEmpty;
        public bool sessionExitProven;
        public string targetHashAfter = "";
        public string fault = "";
    }

    internal sealed class NativeTopCoverPartDetachEvidence
    {
        public string schema = "winnsen.16029.topcover_native_part_detach_probe.v1";
        public bool probeOnly;
        public bool consumable;
        public string mode = "";
        public bool privateEvidenceWritten;
        public string completedAtUtc = "";
        public string protectedSourceInventoryDigestBefore = "";
        public string sourceDirectory = "";
        public string workingPack = "";
        public List<string> physicalParts = new List<string>();
        public List<NativeTopCoverPartMeasurement> baseline =
            new List<NativeTopCoverPartMeasurement>();
        public List<NativeTopCoverPartDetachOperation> operations =
            new List<NativeTopCoverPartDetachOperation>();
        public List<NativeTopCoverPartMeasurement> after =
            new List<NativeTopCoverPartMeasurement>();
        public List<string> changedNames = new List<string>();
        public bool afterComplete;
        public bool targetExact75Names;
        public bool changedNamesExact;
        public bool nonPhysicalTargetHashesUnchanged;
        public bool sourceExact75Unchanged;
        public bool pass;
        public string failureStatus = "";
        public int failureExitCode;
        public string failure = "";
    }

    internal sealed class NativeTopCoverInputBinding
    {
        public string role = "";
        public string path = "";
        public string name = "";
        public string expectedDirectory = "";
        public bool exists;
        public bool noReparse;
        public bool singleLink;
        public long sizeBytesBefore;
        public long sizeBytesAfter;
        public string sha256Before = "";
        public string sha256After = "";
        public bool unchanged;
        public string fault = "";
    }

    internal sealed class NativeTopCoverSketchPointCapture
    {
        public List<double> localSi = new List<double>();
        public List<double> modelSi = new List<double>();
    }

    internal sealed class NativeTopCoverSketchSegmentCapture
    {
        public string runtimeType = "";
        public int type = -1;
        public List<int> ids = new List<int>();
        public string name = "";
        public bool construction;
        public int status = -1;
        public double lengthSi = double.NaN;
        public int constraintCount = -1;
        public int relationCount = -1;
        public bool isBendLine;
        public string kind = "";
        public NativeTopCoverSketchPointCapture start =
            new NativeTopCoverSketchPointCapture();
        public NativeTopCoverSketchPointCapture end =
            new NativeTopCoverSketchPointCapture();
        public NativeTopCoverSketchPointCapture center =
            new NativeTopCoverSketchPointCapture();
        public bool circle;
        public double radiusSi = double.NaN;
        public int rotationDirection;
        public List<double> normalSi = new List<double>();
        public string fault = "";
    }

    internal sealed class NativeTopCoverSketchBlockCapture
    {
        public string name = "";
        public double angle = double.NaN;
        public double scale = double.NaN;
        public NativeTopCoverSketchPointCapture insertion =
            new NativeTopCoverSketchPointCapture();
        public List<double> definitionInsertionLocalSi = new List<double>();
        public List<double> blockToSketchTransform16 = new List<double>();
        public string definitionFileName = "";
        public bool linkToFile;
    }

    internal sealed class NativeTopCoverSketchCapture
    {
        public string name = "";
        public string path = "";
        public string featureType = "";
        public bool found;
        public bool is3D;
        public bool isDerived;
        public List<double> modelToSketchTransform16 = new List<double>();
        public List<double> sketchToModelTransform16 = new List<double>();
        public int segmentCount;
        public int relationCount = -1;
        public bool emptyInternal;
        public List<NativeTopCoverSketchSegmentCapture> segments =
            new List<NativeTopCoverSketchSegmentCapture>();
        public List<NativeTopCoverSketchPointCapture> points =
            new List<NativeTopCoverSketchPointCapture>();
        public List<NativeTopCoverSketchBlockCapture> blocks =
            new List<NativeTopCoverSketchBlockCapture>();
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal sealed class NativeTopCoverFaceCapture
    {
        public bool found;
        public List<double> boxSi = new List<double>();
        public List<double> normalSi = new List<double>();
    }

    internal sealed class NativeTopCoverBendCapture
    {
        public string name = "";
        public string path = "";
        public string type = "";
        public bool accessSelections;
        public bool supplementRequired;
        public int bendType = -1;
        public double angle = double.NaN;
        public int direction = -1;
        public bool down;
        public int order = -1;
        public double radius = double.NaN;
        public double kFactor = double.NaN;
        public double allowance = double.NaN;
        public int allowanceType = -1;
        public bool useDefaultRadius;
        public bool useDefaultAllowance;
        public bool useDefaultRelief;
        public bool useAutoRelief;
        public int autoReliefType = -1;
        public double reliefDepth = double.NaN;
        public double reliefWidth = double.NaN;
        public double reliefRatio = double.NaN;
        public bool customAllowanceAvailable;
        public int customAllowanceType = -1;
        public double customKFactor = double.NaN;
        public double customAllowance = double.NaN;
        public double customDeduction = double.NaN;
        public string customTableFile = "";
        public NativeTopCoverFaceCapture fixedFace = new NativeTopCoverFaceCapture();
        public int flatPatternSegmentApiCount = -1;
        public List<NativeTopCoverSketchSegmentCapture> flatPatternSegments =
            new List<NativeTopCoverSketchSegmentCapture>();
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal sealed class NativeTopCoverFlatPatternCapture
    {
        public string name = "";
        public string path = "";
        public bool definitionReadable;
        public bool accessSelections;
        public bool supplementRequired;
        public NativeTopCoverFaceCapture fixedFace = new NativeTopCoverFaceCapture();
        public int excludedFaceCount;
        public bool mergeFace;
        public bool simplifyBends;
        public bool cornerTreatment;
        public double breakCornerRadius = double.NaN;
        public int breakCornerType = -1;
        public double cornerTrimReliefDistance = double.NaN;
        public int cornerTrimReliefType = -1;
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal sealed class NativeTopCoverRebuildPartCapture
    {
        public string purpose = "";
        public string name = "";
        public string path = "";
        public NativeTopCoverInputBinding sourceBinding = new NativeTopCoverInputBinding();
        public bool cacheBeforeEmpty;
        public bool opened;
        public int openErrors;
        public int openWarnings;
        public bool readOnly;
        public bool rebuilt;
        public bool cacheAfterEmpty;
        public bool sessionExitProven;
        public DependencyEvidence preopenDependencies = new DependencyEvidence();
        public bool knownGoldBottomHistoricalDependencyAccepted;
        public NativeTopCoverPartMeasurement measurement =
            new NativeTopCoverPartMeasurement();
        public List<NativeTopCoverSketchCapture> sketches =
            new List<NativeTopCoverSketchCapture>();
        public List<NativeTopCoverBendCapture> oneBends =
            new List<NativeTopCoverBendCapture>();
        public List<NativeTopCoverBendCapture> processBends =
            new List<NativeTopCoverBendCapture>();
        public List<NativeTopCoverFlatPatternCapture> flatPatterns =
            new List<NativeTopCoverFlatPatternCapture>();
        public List<string> unresolved = new List<string>();
        public string fault = "";
        public bool complete;
    }

    internal sealed class NativeTopCoverPhysicalAssemblyMember
    {
        public string name = "";
        public string configuration = "";
        public List<double> transform16 = new List<double>();
        public bool fixedState;
        public bool fixedCaptured;
        public int suppression;
    }

    internal sealed class NativeTopCoverRebuildAssemblyCapture
    {
        public NativeTopCoverInputBinding binding = new NativeTopCoverInputBinding();
        public bool cacheBeforeEmpty;
        public bool opened;
        public int openErrors;
        public int openWarnings;
        public bool readOnly;
        public bool rebuilt;
        public bool cacheAfterEmpty;
        public bool sessionExitProven;
        public DependencyEvidence preopenDependencies = new DependencyEvidence();
        public List<NativeTopCoverTemplateComponentState> components =
            new List<NativeTopCoverTemplateComponentState>();
        public NativeTopCoverTemplateComponentState master;
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal class NativeTopCoverRebuildCaptureEvidence
    {
        public string schema = "winnsen.16029.topcover_native_rebuild_capture_probe.v1";
        public bool probeOnly;
        public bool consumable;
        public string mode = "";
        public bool privateEvidenceWritten;
        public string completedAtUtc = "";
        public string protectedSourceInventoryDigestBefore = "";
        public string v37WorkingPack = "";
        public string goldEngineeringDirectory = "";
        public string goldCapturePack = "";
        public List<NativeTopCoverInputBinding> goldInputs =
            new List<NativeTopCoverInputBinding>();
        public List<NativeTopCoverRebuildPartCapture> v37Parts =
            new List<NativeTopCoverRebuildPartCapture>();
        public List<NativeTopCoverRebuildPartCapture> goldParts =
            new List<NativeTopCoverRebuildPartCapture>();
        public NativeTopCoverRebuildAssemblyCapture goldAssembly =
            new NativeTopCoverRebuildAssemblyCapture();
        public List<NativeTopCoverPhysicalAssemblyMember> physicalIdentityTransforms =
            new List<NativeTopCoverPhysicalAssemblyMember>();
        public bool masterExcludedFromNewAssembly;
        public bool v37SourceExact75Unchanged;
        public bool workingPackExact75Unchanged;
        public bool goldInputsUnchanged;
        public List<string> goldSourceLockFilesBefore = new List<string>();
        public List<string> goldSourceLockFilesAfter = new List<string>();
        public bool goldSourceLockStateUnchanged;
        public bool goldCapturePackComplete;
        public bool datasetComplete;
        public string qualityBoundary =
            "Read-only reconstruction evidence only; no native part or assembly was created.";
        public string failureStatus = "";
        public int failureExitCode;
        public string failure = "";
    }

    internal sealed class NativeTopCoverMasterReferenceCaptureEvidence :
        NativeTopCoverRebuildCaptureEvidence
    {
        public NativeTopCoverMasterReferenceCaptureEvidence()
        {
            schema = "winnsen.16029.topcover_native_master_reference_capture_probe.v1";
            qualityBoundary =
                "Read-only master-reference evidence only; no native CAD was created.";
        }

        public NativeTopCoverRebuildPartCapture v37Master =
            new NativeTopCoverRebuildPartCapture();
        public NativeTopCoverRebuildPartCapture goldMaster =
            new NativeTopCoverRebuildPartCapture();
    }

    internal sealed class NativeTopCoverFrontBrepEdge
    {
        public int loopIndex = -1;
        public int order = -1;
        public int curveType = -1;
        public int curveTag = -1;
        public int sense = -1;
        public double uMin = double.NaN;
        public double uMax = double.NaN;
        public List<double> startPointSi = new List<double>();
        public List<double> endPointSi = new List<double>();
        public double lengthSi = double.NaN;
        public bool isCircle;
        public List<double> circleParamsSi = new List<double>();
        public bool isLine;
        public List<double> lineParamsSi = new List<double>();
        public List<double> startVertexPointSi = new List<double>();
        public List<double> endVertexPointSi = new List<double>();
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal sealed class NativeTopCoverFrontBrepLoop
    {
        public int index = -1;
        public bool isOuter;
        public int edgeCount;
        public int capturedEdgeCount;
        public List<NativeTopCoverFrontBrepEdge> edges = new List<NativeTopCoverFrontBrepEdge>();
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal sealed class NativeTopCoverFrontBrepFace
    {
        public int index = -1;
        public double areaSi = double.NaN;
        public List<double> boxSi = new List<double>();
        public List<double> normalSi = new List<double>();
        public int loopCount = -1;
        public int surfaceIdentity = -1;
        public bool isPlane;
        public bool isCylinder;
        public List<double> planeParamsSi = new List<double>();
        public List<double> cylinderParamsSi = new List<double>();
        public List<NativeTopCoverFrontBrepLoop> loops = new List<NativeTopCoverFrontBrepLoop>();
        public List<string> unresolved = new List<string>();
        public bool complete;
    }

    internal sealed class NativeTopCoverFrontBrepPartCapture
    {
        public string purpose = "";
        public string name = "";
        public string path = "";
        public int expectedFaceCount;
        public int expectedEdgeCount;
        public NativeTopCoverInputBinding sourceBinding = new NativeTopCoverInputBinding();
        public bool cacheBeforeEmpty;
        public bool opened;
        public int openErrors;
        public int openWarnings;
        public bool readOnly;
        public bool rebuilt;
        public bool cacheAfterEmpty;
        public bool sessionExitProven;
        public DependencyEvidence preopenDependencies = new DependencyEvidence();
        public int modelExternalReferenceCount = -1;
        public int extensionExternalReferenceCount = -1;
        public int bodyCount;
        public int faceCount = -1;
        public int edgeCount = -1;
        public int loopEdgeReferenceCount = -1;
        public List<double> bodyBoxSi = new List<double>();
        public List<NativeTopCoverFrontBrepFace> faces = new List<NativeTopCoverFrontBrepFace>();
        public string geometrySignatureSha256 = "";
        public List<string> unresolved = new List<string>();
        public string fault = "";
        public bool complete;
    }

    internal sealed class NativeTopCoverFrontBrepCaptureEvidence :
        NativeTopCoverRebuildCaptureEvidence
    {
        public NativeTopCoverFrontBrepCaptureEvidence()
        {
            schema = "winnsen.16029.topcover_native_front_brep_capture_probe.v1";
            qualityBoundary = "Read-only B-rep evidence only; no native CAD was created.";
        }
        public NativeTopCoverFrontBrepPartCapture v37 = new NativeTopCoverFrontBrepPartCapture();
        public NativeTopCoverFrontBrepPartCapture gold = new NativeTopCoverFrontBrepPartCapture();
    }

    internal sealed class Result
    {
        public string schema = "winnsen.16029.native_seed_pack_result.v1";
        public string phase = "";
        public string recipe_id = "";
        public string recipe_digest = "";
        public string generated_at_utc = "";
        public string completed_at_utc = "";
        public bool success;
        public string status = "NOT_STARTED";
        public string error = "";
        public int exit_code;
        public bool preflight_passed;
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool pair_committed;
        public string repository_root = "";
        public string attempts_root = "";
        public string attempt_directory = "";
        public string task_id = "";
        public int task_revision;
        public string task_digest = "";
        public string request_fingerprint = "";
        public string request_digest = "";
        public string worker_id = "";
        public int attempt;
        public string source_directory = "";
        public string target_directory = "";
        public string source_root_path = "";
        public string target_root_path = "";
        public string output_json = "";
        public string plan_path = "";
        public string plan_sha256 = "";
        public string authorization_path = "";
        public string authorization_sha256 = "";
        public string authorization_json_base64 = "";
        public string authorization_id = "";
        public string authorization_issued_at = "";
        public string authorization_expires_at = "";
        public string authorization_seed_inventory_digest = "";
        public string lease_id = "";
        public string lease_expires_at = "";
        public string tool_id = "";
        public string tool_source_normalized_sha256 = "";
        public string tool_executable_sha256 = "";
        public bool stage_receipt_contracts_validated;
        public bool trusted_toolchain_manifests_validated;
        public List<TrustedManifestEvidence> trusted_toolchain_manifests =
            new List<TrustedManifestEvidence>();
        public string attempt_lock_path = "";
        public bool attempt_lock_acquired;
        public bool target_created_by_this_run;
        public bool direct_copy_completed;
        public List<string> direct_copy_files = new List<string>();
        public string direct_copy_inventory_digest = "";
        public List<FileSnapshot> source_inventory = new List<FileSnapshot>();
        public string source_inventory_digest = "";
        public string target_inventory_digest = "";
        public Dictionary<string, string> source_hashes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        public bool source_unchanged;
        public string root_file_name = "";
        public string root_sha_before = "";
        public string root_sha_after_stable = "";
        public int non_root_file_count;
        public bool non_root_exact_source;
        public int initial_open_errors;
        public int initial_open_warnings;
        public int writable_resolve_status;
        public bool writable_rebuild_succeeded;
        public bool writable_dependency_gate;
        public bool writable_known_root_issue_gate;
        public bool writable_save_succeeded;
        public int stabilize_save_errors;
        public int stabilize_save_warnings;
        public bool save_flag_after_stabilize;
        public int reopen_errors;
        public int reopen_warnings;
        public int readonly_resolve_status;
        public bool readonly_rebuild_succeeded;
        public bool readonly_root_hash_unchanged;
        public HierarchyEvidence writable_hierarchy = new HierarchyEvidence();
        public HierarchyEvidence readonly_hierarchy = new HierarchyEvidence();
        public DependencyEvidence dependencies = new DependencyEvidence();
        public FeatureHealthEvidence root_feature_health = new FeatureHealthEvidence();
        public bool known_root_issue_gate;
        public bool known_relocated_warning64_gate;
        public List<SessionEvidence> sessions = new List<SessionEvidence>();
        public ProcessEvidence processes = new ProcessEvidence();
        public List<AuthorizationCheckpoint> authorization_checkpoints =
            new List<AuthorizationCheckpoint>();
        public bool rollback_attempted;
        public bool rollback_succeeded;
        public string rollback_error = "";
        public int rollback_transient_lock_files_removed;
        [System.Web.Script.Serialization.ScriptIgnore]
        public string topcover_capture_pack = "";
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool topcover_capture_pack_created;
        public bool topcover_capture_pack_rollback_attempted;
        public bool topcover_capture_pack_rollback_succeeded;
        public string topcover_capture_pack_rollback_error = "";
        public int topcover_capture_pack_transient_lock_files_removed;
        [System.Web.Script.Serialization.ScriptIgnore]
        public string topcover_probe_repair_root = "";
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool topcover_probe_repair_root_created;
        public bool topcover_probe_repair_rollback_attempted;
        public bool topcover_probe_repair_rollback_succeeded;
        public string topcover_probe_repair_rollback_error = "";
        public bool topcover_probe_cleanup_evidence_written;
        public string topcover_probe_cleanup_evidence_sha256 = "";
        public string topcover_probe_cleanup_evidence_error = "";
        public string evidence_commitment_sha256 = "";
        public QualityBoundary quality_boundary = new QualityBoundary();

        [System.Web.Script.Serialization.ScriptIgnore]
        public string evidence_path = "";
        [System.Web.Script.Serialization.ScriptIgnore]
        public string receipt_path = "";
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool evidence_created_by_this_run;
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool receipt_created_by_this_run;
        [System.Web.Script.Serialization.ScriptIgnore]
        public Dictionary<string, object> plan_document =
            new Dictionary<string, object>(StringComparer.Ordinal);
        [System.Web.Script.Serialization.ScriptIgnore]
        public Dictionary<string, object> authorization_document =
            new Dictionary<string, object>(StringComparer.Ordinal);
    }
}
