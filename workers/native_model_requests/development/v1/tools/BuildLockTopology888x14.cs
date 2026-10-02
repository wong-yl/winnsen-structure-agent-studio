using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Native888
{
    internal static class BuildLockTopology888x14
    {
        private const string AttemptRootEnvironmentVariable = "WINNSEN_NATIVE_16029_ATTEMPTS_ROOT";
        private const string PlanShaEnvironmentVariable = "WINNSEN_NATIVE_16029_PLAN_SHA256";
        private const string AuthorizationPathEnvironmentVariable = "WINNSEN_NATIVE_EXECUTION_AUTH_PATH";
        private const string AuthorizationShaEnvironmentVariable = "WINNSEN_NATIVE_EXECUTION_AUTH_SHA256";
        private const string ExpectedAuthorizationSchema = "winnsen.native_execution_authorization.v1";
        private const string ExpectedToolId = "native_lock_topology_888x14_v1";
        private const string ExpectedToolchainManifestSchema =
            "winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1";
        private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";
        private const string ExpectedRecipeDigest = "F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8";
        private const string ExpectedSourceSeedInventoryDigest =
            "9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B";
        private const string ExpectedSolidWorksExePath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string ExpectedSolidWorksExeSha256 = "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string CadDirectoryRelativePath = @"native_cad\working_pack";
        private const string LeftPartName = "箱体竖隔板L.sldprt";
        private const string RightPartName = "箱体竖隔板R.SLDPRT";
        private const string EvidenceRelativePath = @"evidence\lock_topology_888x14.v2.json";
        private const string ReceiptRelativePath = @"receipts\lock_topology_888x14.json";
        private const string ReceiptSchema = "winnsen.16029.native_lock_topology_receipt.v1";
        private const string BackupRelativePath = @"transaction_backup\lock_topology_888x14_original";
        private const string TrustedRecipeId = "winnsen-16029-888w-14door-native-v1";
        private const string TrustedRecipeRegistryPath = @"D:\Winnsen_Structure_Agent_Studio\tools\locker_16029_native_recipe_registry.mjs";
        private const string TrustedRecipeRegistrySha256 = "F7A5F1C53873CFBF6B9EA5C469D9FDEF919B07FD3442162D3E334180F7A15DBF";
        private const string LayoutRuleEvidenceId = "16029-1000w-14door-rule-evidence-20260519";
        private const string LayoutRuleEvidencePath = @"D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\CHECK_HANDOFF_READY.status.txt";
        private const string LayoutRuleEvidenceSha256 = "A1C15076110AF9954039B66599C1558F0B6F4C2F0FA04F3048656B06F46E5D21";
        private const string MechanicalTonguePartName = "锁舌.SLDPRT";
        private const string MechanicalTongueSha256 = "26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719";
        private const string SourcePatternName = "阵列(线性)2";
        private const string SlotSketchName = "草图6";
        private const string CircleSketchName = "草图11";
        private const string NativePatternName = "NATIVE_888_14_LOCK_ROWS";
        private const double DoorHeightMm = (12.0 / 7.0) * 152.5 - 7.0;
        private const double DoorPitchMm = DoorHeightMm + 7.0;
        private const double DoorBottomMm = 32.0;
        private const double RowToleranceMm = 0.01;
        private const double CoordinateToleranceMm = 0.01;
        private const double LengthToleranceMm = 0.0005;
        private const double CircleLengthMm = Math.PI * 5.0;
        private const double SeedSlotRowMm = 1706.0;
        private const double SeedCircleRowMm = 1646.0;
        private const string ReferenceNormalizedFingerprintSha256 = "a705d0f241e9c7a2f79b3c26f9c061f1f7dcefe6933dcc6a1c65686d647dcacc";
        private static readonly double[] ExpectedSlots = Enumerable.Range(0, 7)
            .Select(index => DoorBottomMm + DoorHeightMm / 2.0 + index * DoorPitchMm).ToArray();
        private static readonly double[] ExpectedCircles = ExpectedSlots.Select(value => value - 60.0).ToArray();
        private static readonly string[] LegacyPatternNames =
        {
            "阵列(线性)2",
            "V34_锁孔补充_Y1248.5",
            "V34_锁孔补充_Y638.5",
            "V35_L_锁孔_Y486"
        };

        private static readonly string[] TrustedToolchainManifestKeys =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1",
            "native_door_module_888x14_v1", ExpectedToolId,
            "native_root_assembly_888x14_v1", "native_final_pack_888x14_v1"
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
            "winnsen.16029.native_door_module_receipt.v1",
            "winnsen.16029.native_lock_topology_receipt.v1",
            "winnsen.16029.native_root_assembly_receipt.v1",
            "winnsen.16029.native_final_pack_receipt.v1"
        };

        private static readonly string[] StageReceiptPaths =
        {
            "receipts/clone_native_seed.json", "evidence/width-dimensions.receipt.json",
            "evidence/width-derived.receipt.json", "evidence/width-base-hole.receipt.json",
            "evidence/width-assemblies.receipt.json", "receipts/door_module_888x14.json",
            "receipts/lock_topology_888x14.json", "receipts/root_assembly_888x14.json",
            "receipts/final_pack_and_relocated_reopen.json"
        };

        private static readonly string[] StageReceiptTools =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1", "native_width_888_v1",
            "native_width_888_v1", "native_width_888_v1", "native_door_module_888x14_v1",
            ExpectedToolId, "native_root_assembly_888x14_v1", "native_final_pack_888x14_v1"
        };

        private static readonly string[] StageReceiptPredecessors =
        {
            "", "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14"
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

        private static readonly string[] EvidenceCommitmentExcludedKeys =
        {
            "evidence_write_attempted", "evidence_write_succeeded",
            "evidence_finalize_write_succeeded", "evidence_finalize_error",
            "failure_evidence_write_succeeded", "phase_receipt_path", "phase_receipt_sha256",
            "phase_receipt_committed", "authorization_checkpoints", "completed_at_utc",
            "commit_completed_at_utc", "evidence_commitment_sha256", "backup_deleted",
            "backup_delete_error"
        };

        private static readonly SlotTemplate[] SlotTemplates =
        {
            T("E01", 0.788322, 61.7, -5.708124, -19.3, 62.2, -5.639994, -19.8),
            T("E02", 0.788322, 62.2, 5.639994, -19.8, 61.7, 5.708124, -19.3),
            T("E03", 0.8, 61.7, -5.708124, -19.3, 61.7, -5.708124, -18.5),
            T("E04", 0.8, 62.2, -5.639994, -19.8, 63, -5.639994, -19.8),
            T("E05", 0.8, 62.2, -4.497259, -41.604672, 63, -4.497259, -41.604672),
            T("E06", 0.8, 62.2, -2.5, -43.5, 63, -2.5, -43.5),
            T("E07", 0.8, 62.2, 2.5, -43.5, 63, 2.5, -43.5),
            T("E08", 0.8, 62.2, 4.497259, -41.604672, 63, 4.497259, -41.604672),
            T("E09", 0.8, 62.2, 5.639994, -19.8, 63, 5.639994, -19.8),
            T("E10", 0.8, 61.7, 5.708124, -19.3, 61.7, 5.708124, -18.5),
            T("E11", 2.043171, 61.7, -5.708124, -18.5, 63, -5.639994, -19.8),
            T("E12", 2.043171, 63, 5.639994, -19.8, 61.7, 5.708124, -18.5),
            T("E13", 3.036873, 62.2, -2.5, -43.5, 62.2, -4.497259, -41.604672),
            T("E14", 3.036873, 63, -2.5, -43.5, 63, -4.497259, -41.604672),
            T("E15", 3.036873, 62.2, 4.497259, -41.604672, 62.2, 2.5, -43.5),
            T("E16", 3.036873, 63, 4.497259, -41.604672, 63, 2.5, -43.5),
            T("E17", 5, 63, 2.5, -43.5, 63, -2.5, -43.5),
            T("E18", 5, 62.2, 2.5, -43.5, 62.2, -2.5, -43.5),
            T("E19", 21.834595, 63, -4.497259, -41.604672, 63, -5.639994, -19.8),
            T("E20", 21.834595, 62.2, -4.497259, -41.604672, 62.2, -5.639994, -19.8),
            T("E21", 21.834595, 62.2, 5.639994, -19.8, 62.2, 4.497259, -41.604672),
            T("E22", 21.834595, 63, 5.639994, -19.8, 63, 4.497259, -41.604672)
        };

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && string.Equals(args[0], "--identity", StringComparison.Ordinal))
            {
                Console.WriteLine("sourceNormalizedSha256=" + ExpectedSourceSha256);
                Console.WriteLine("executableSha256=" + Sha256(System.Reflection.Assembly.GetExecutingAssembly().Location));
                return ExpectedSourceSha256.Length == 64 ? 0 : 1;
            }
            if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
                return SelfTest();
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: BuildLockTopology888x14.exe <exact-native-task-attempt-root>");
                Console.Error.WriteLine("       BuildLockTopology888x14.exe --self-test");
                return 2;
            }

            string attemptRoot = Path.GetFullPath(args[0]).TrimEnd('\\', '/');
            string cadDirectory = Path.Combine(attemptRoot, CadDirectoryRelativePath);
            string leftPath = Path.Combine(cadDirectory, LeftPartName);
            string rightPath = Path.Combine(cadDirectory, RightPartName);
            string temporaryRightPath = Path.Combine(cadDirectory,
                ".native-lock-right-" + Guid.NewGuid().ToString("N") + ".SLDPRT");
            string outputPath = Path.Combine(attemptRoot, EvidenceRelativePath);
            string receiptPath = Path.Combine(attemptRoot, ReceiptRelativePath);
            string backupDirectory = Path.Combine(attemptRoot, BackupRelativePath);
            var result = new Result
            {
                schema = "winnsen.locker16029.native_888x14_lock_topology.v2",
                generated_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                attempt_root = attemptRoot,
                cad_directory = cadDirectory,
                left_part_path = leftPath,
                right_part_path = rightPath,
                temporary_right_path = temporaryRightPath,
                output_path = outputPath,
                backup_directory = backupDirectory,
                trusted_recipe_id = TrustedRecipeId,
                trusted_recipe_registry_path = TrustedRecipeRegistryPath,
                trusted_recipe_registry_sha256_expected = TrustedRecipeRegistrySha256,
                layout_rule_evidence_id = LayoutRuleEvidenceId,
                layout_rule_evidence_path = LayoutRuleEvidencePath,
                layout_rule_evidence_sha256_expected = LayoutRuleEvidenceSha256,
                expected_slot_rows_mm = ExpectedSlots,
                expected_circle_rows_mm = ExpectedCircles,
                native_slot_edge_count_per_opening = SlotTemplates.Length,
                reference_normalized_slot_fingerprint_sha256 = ReferenceNormalizedFingerprintSha256,
                paired_circle_diameter_mm = 5.0,
                paired_circle_offset_from_slot_mm = -60.0,
                door_count = 14,
                lock_tongue_validation = new TongueContract
                {
                    required = true,
                    deferred_to_assembly_stage = true,
                    evidence_relative_path = @"evidence\assembly_lock_tongues_888x14.json",
                    expected_total = 14,
                    expected_per_side = 7,
                    expected_left_x_mm = -55.0,
                    expected_right_x_mm = 55.0,
                    expected_z_mm = -11.3,
                    trusted_source_basename = MechanicalTonguePartName,
                    trusted_source_sha256 = MechanicalTongueSha256,
                    forbidden_name_token = "electric",
                    expected_y_rows_mm = ExpectedSlots
                }
            };
            bool stagesComplete = false;
            int exitCode = 1;

            try
            {
                string attemptsRoot = (System.Environment.GetEnvironmentVariable(AttemptRootEnvironmentVariable) ?? "").Trim();
                Require(Path.IsPathRooted(attemptsRoot) && Directory.Exists(attemptsRoot),
                    "TRUSTED_ATTEMPTS_ROOT_MISSING", 3,
                    AttemptRootEnvironmentVariable + " must identify the worker-owned attempts root");
                attemptsRoot = Path.GetFullPath(attemptsRoot).TrimEnd('\\', '/');
                Require(IsAllowedAttemptRoot(attemptRoot, attemptsRoot),
                    "REFUSED_OUTSIDE_EXACT_NATIVE_ATTEMPT_ROOT", 3,
                    "attempt root must be <trusted-attempts-root>\\<safe-task-id>\\attempt-####");
                BindImmutablePlanAndAuthorization(attemptRoot, attemptsRoot, result);
                Require(File.Exists(TrustedRecipeRegistryPath), "TRUSTED_RECIPE_REGISTRY_MISSING", 3,
                    TrustedRecipeRegistryPath);
                result.trusted_recipe_registry_sha256_actual = Sha256(TrustedRecipeRegistryPath);
                Require(string.Equals(result.trusted_recipe_registry_sha256_actual,
                    TrustedRecipeRegistrySha256, StringComparison.OrdinalIgnoreCase),
                    "TRUSTED_RECIPE_REGISTRY_HASH_MISMATCH", 3,
                    "the embedded 888x14 row contract is not bound to the reviewed registry revision");
                Require(File.Exists(LayoutRuleEvidencePath), "LAYOUT_RULE_EVIDENCE_MISSING", 3,
                    LayoutRuleEvidencePath);
                result.layout_rule_evidence_sha256_actual = Sha256(LayoutRuleEvidencePath);
                Require(string.Equals(result.layout_rule_evidence_sha256_actual,
                    LayoutRuleEvidenceSha256, StringComparison.OrdinalIgnoreCase),
                    "LAYOUT_RULE_EVIDENCE_HASH_MISMATCH", 3,
                    "the seven-row formula must stay bound to the verified 1000W 14-door rules-only evidence");
                result.trusted_layout_rule_gate = true;
                Require(SamePath(outputPath, Path.Combine(attemptRoot, EvidenceRelativePath)),
                    "OUTPUT_PATH_MISMATCH", 3, "evidence path is not exact");
                Require(NoReparsePoints(attemptsRoot, Path.GetDirectoryName(outputPath)),
                    "EVIDENCE_DIRECTORY_REPARSE_POINT_REFUSED", 3,
                    "evidence directory path contains a reparse point");
                Require(!File.Exists(outputPath), "EVIDENCE_ALREADY_EXISTS", 3,
                    "single evidence commit refuses to overwrite an earlier stage result");
                Require(!File.Exists(receiptPath), "LOCK_RECEIPT_ALREADY_EXISTS", 3,
                    "lock receipt commit refuses to overwrite an earlier stage receipt");
                result.path_allowlist_match = true;
                Require(Directory.Exists(cadDirectory), "ISOLATED_CAD_DIRECTORY_MISSING", 4, cadDirectory);
                Require(File.Exists(leftPath) && File.Exists(rightPath), "PARTITION_PAIR_MISSING", 4,
                    "working_pack must contain the exact L/R partition pair");
                Require(NoReparsePoints(attemptsRoot, cadDirectory) && !TreeHasReparsePoint(cadDirectory),
                    "WORKING_PACK_REPARSE_POINT_REFUSED", 4,
                    "working_pack and every ancestor/file must be ordinary filesystem entries");
                Require(!TreeHasHardLinks(cadDirectory), "WORKING_PACK_HARDLINK_REFUSED", 4,
                    "working_pack CAD files must not share hard-linked storage");
                Require(!ContainsStepOrFreeCad(cadDirectory), "LEGACY_GEOMETRY_ARTIFACT_PRESENT", 4,
                    "working_pack contains STEP, STP, FCStd, or FreeCAD artifacts");

                result.original_inventory = Inventory(cadDirectory);
                result.part_count = result.original_inventory.Count(row =>
                    string.Equals(Path.GetExtension(row.relative_path), ".SLDPRT", StringComparison.OrdinalIgnoreCase));
                result.assembly_count = result.original_inventory.Count(row =>
                    string.Equals(Path.GetExtension(row.relative_path), ".SLDASM", StringComparison.OrdinalIgnoreCase));
                Require(result.original_inventory.Count == 75 && result.part_count == 53 &&
                    result.assembly_count == 22 && result.original_inventory.All(IsCadInventoryRow),
                    "WORKING_PACK_INVENTORY_NOT_EXACT_75", 4,
                    "working_pack must contain exactly 75 top-level CAD files: 53 SLDPRT and 22 SLDASM");
                result.initial_inventory_digest = InventoryDigest(result.original_inventory);
                Require(string.Equals(result.authorization_seed_inventory_digest,
                    result.initial_inventory_digest, StringComparison.OrdinalIgnoreCase),
                    "AUTHORIZATION_SEED_INVENTORY_MISMATCH", 4,
                    "execution authorization is not bound to the live 75-file working_pack inventory");
                ValidateLivePredecessorReceiptChain(attemptRoot, attemptsRoot, result);

                result.baseline_sldworks_process_ids.AddRange(ProcessIds("SLDWORKS"));
                result.baseline_sldprocmon_process_ids.AddRange(ProcessIds("sldProcMon"));
                Require(result.baseline_sldworks_process_ids.Count == 0 &&
                    result.baseline_sldprocmon_process_ids.Count == 0,
                    "DIRTY_SOLIDWORKS_BASELINE", 5,
                    "zero SLDWORKS and sldProcMon processes are required");

                Require(!Directory.Exists(backupDirectory), "TRANSACTION_BACKUP_ALREADY_EXISTS", 6,
                    "a prior transaction backup exists; recover or start a new attempt");
                Require(NoReparsePoints(attemptsRoot, Path.GetDirectoryName(backupDirectory)),
                    "BACKUP_PARENT_REPARSE_POINT_REFUSED", 6,
                    "transaction backup parent contains a reparse point");
                CopyDirectoryExact(cadDirectory, backupDirectory);
                result.backup_inventory = Inventory(backupDirectory);
                result.backup_created = InventoriesEqual(result.original_inventory, result.backup_inventory);
                Require(result.backup_created, "FULL_PACK_BACKUP_FAILED", 6,
                    "full isolated CAD backup inventory/hash mismatch");

                result.left_sha_before = Sha256(leftPath);
                result.right_sha_before = Sha256(rightPath);
                EditLeft(result);
                CreateNativeRightMirror(result);
                result.temporary_right_reopen = VerifyPart(result, temporaryRightPath, "R",
                    "temporary_right_reopen", true);
                Require(RelatedProcessIds().Count == 0, "DIRTY_BASELINE_BEFORE_RIGHT_COMMIT", 26,
                    "temporary right cannot replace the old right while a CAD process exists");
                File.Replace(temporaryRightPath, rightPath, null, true);
                result.right_replaced_transactionally = !File.Exists(temporaryRightPath) && File.Exists(rightPath);
                Require(result.right_replaced_transactionally, "RIGHT_TRANSACTION_REPLACE_FAILED", 26,
                    "the validated temporary native right did not transactionally replace the old V37 right");
                result.right_sha_after_commit = Sha256(rightPath);
                Require(!string.Equals(result.right_sha_before, result.right_sha_after_commit,
                    StringComparison.OrdinalIgnoreCase), "RIGHT_COMMITTED_HASH_DID_NOT_CHANGE", 26,
                    "the committed native right still hashes as the old V37 right");
                result.left_reopen = VerifyPart(result, leftPath, "L", "fresh_readonly_reopen_left", false);
                result.right_reopen = VerifyPart(result, rightPath, "R", "committed_right_reopen", true);
                Require(GeometryGate(result.left_reopen, "L") && GeometryGate(result.right_reopen, "R"),
                    "FRESH_SESSION_EDGE_GATE_FAILED", 30,
                    "fresh read-only sessions did not prove seven native 22-edge slots and seven paired diameter-5 holes per side");
                Require(result.sessions.Select(row => row.sldworks_process_id).Distinct().Count() == 5,
                    "SESSION_IDENTITY_NOT_DISTINCT", 30,
                    "left edit, MirrorPart2, temporary-right reopen, left reopen, and committed-right reopen must use five distinct owned sessions");
                result.post_inventory = Inventory(cadDirectory);
                AuditPostInventory(result);
                stagesComplete = true;
            }
            catch (GateException ex)
            {
                result.status = ex.Status;
                result.error = ex.Message;
                exitCode = ex.ExitCode;
            }
            catch (Exception ex)
            {
                result.status = "LOCK_TOPOLOGY_888X14_EXCEPTION";
                result.error = SafeException(ex);
                exitCode = 90;
            }
            finally
            {
                CleanupOwned(result);
                result.final_sldworks_process_ids.AddRange(ProcessIds("SLDWORKS"));
                result.final_sldprocmon_process_ids.AddRange(ProcessIds("sldProcMon"));
                result.final_process_cleanup_gate = result.final_sldworks_process_ids.Count == 0 &&
                    result.final_sldprocmon_process_ids.Count == 0 && result.cleanup_errors.Count == 0 &&
                    result.sessions.All(row => row.process_exited && row.monitors_exited &&
                        row.sldprocmon_identity_gate);

                if (!stagesComplete || !result.final_process_cleanup_gate)
                {
                    if (result.backup_created && result.final_process_cleanup_gate)
                    {
                        result.rollback_attempted = true;
                        try
                        {
                            Require(SamePath(cadDirectory, Path.Combine(attemptRoot, CadDirectoryRelativePath)),
                                "ROLLBACK_TARGET_PATH_DRIFT", 91, cadDirectory);
                            RestoreDirectoryExact(backupDirectory, cadDirectory);
                            result.rollback_inventory = Inventory(cadDirectory);
                            result.rollback_succeeded = InventoriesEqual(result.original_inventory,
                                result.rollback_inventory) && !TreeHasReparsePoint(cadDirectory) &&
                                !TreeHasHardLinks(cadDirectory);
                            if (!result.rollback_succeeded)
                                result.rollback_error = "restored inventory/hash does not match the original full pack";
                        }
                        catch (Exception rollbackError)
                        {
                            result.rollback_error = SafeException(rollbackError);
                        }
                    }
                    else if (result.backup_created)
                    {
                        result.rollback_blocked_by_process_gate = true;
                        if (string.IsNullOrWhiteSpace(result.error))
                            result.error = "rollback refused because the final CAD process gate is not clean";
                    }
                    result.success = false;
                    if (stagesComplete && !result.final_process_cleanup_gate)
                    {
                        result.status = "FINAL_PROCESS_CLEANUP_GATE_FAILED";
                        exitCode = 31;
                    }
                }
                else result.status = "LOCK_TOPOLOGY_888X14_CAD_STAGES_COMPLETE_PENDING_EVIDENCE";
                result.backup_retained = Directory.Exists(backupDirectory);

                if (CanPrepareSuccessEvidence(stagesComplete, result))
                {
                    try
                    {
                        string attemptsRoot = Path.GetFullPath(System.Environment.GetEnvironmentVariable(
                            AttemptRootEnvironmentVariable) ?? "").TrimEnd('\\', '/');
                        Require(NoReparsePoints(attemptsRoot, Path.GetDirectoryName(outputPath)),
                            "EVIDENCE_DIRECTORY_REPARSE_POINT_REFUSED_AT_COMMIT", 92,
                            "evidence directory path changed before commit");
                        Require(AuthorizationStillValidAtCommit(result), "EXECUTION_AUTHORIZATION_EXPIRED_AT_COMMIT", 92,
                            "authorization and task lease must both remain valid through evidence commit");
                        result.completed_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                        result.success = true;
                        result.status = "LOCK_TOPOLOGY_888X14_COMPLETE";
                        result.single_evidence_commit = true;
                        result.evidence_commit_verified = true;
                        result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                        WriteJsonAtomicNew(outputPath, result);
                        Require(File.Exists(outputPath) && BoolValue(ReadJsonObject(outputPath), "evidence_commit_verified") &&
                            string.Equals(EvidenceCommitmentDigest(ReadJsonObject(outputPath)),
                                result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase),
                            "EVIDENCE_COMMIT_REREAD_FAILED", 92, "committed evidence could not be read back");
                        Dictionary<string, object> receipt = PrepareLockReceipt(result,
                            Sha256(outputPath));
                        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
                        Require(NoReparsePoints(attemptsRoot, Path.GetDirectoryName(receiptPath)),
                            "LOCK_RECEIPT_DIRECTORY_REPARSE_POINT_REFUSED_AT_COMMIT", 92,
                            "receipt directory path changed before commit");
                        WriteJsonObjectAtomicNew(receiptPath, receipt);
                        Require(ValidateCommittedLockReceipt(receiptPath, outputPath, result),
                            "LOCK_RECEIPT_COMMIT_REREAD_FAILED", 92,
                            "committed lock receipt/evidence pair could not be verified");
                        Require(AuthorizationStillValidAtCommit(result),
                            "EXECUTION_AUTHORIZATION_EXPIRED_AFTER_RECEIPT_COMMIT", 92,
                            "authorization and task lease must remain valid through receipt commit");
                        exitCode = 0;
                    }
                    catch (Exception writeError)
                    {
                        result.success = false;
                        result.evidence_commit_verified = false;
                        bool evidenceContained = ContainUnpairedSuccessArtifact(outputPath,
                            attemptRoot, "lock-evidence");
                        bool receiptContained = ContainUnpairedSuccessArtifact(receiptPath,
                            attemptRoot, "lock-receipt");
                        if (result.backup_created && result.final_process_cleanup_gate)
                        {
                            result.rollback_attempted = true;
                            try
                            {
                                RestoreDirectoryExact(backupDirectory, cadDirectory);
                                result.rollback_inventory = Inventory(cadDirectory);
                                result.rollback_succeeded = InventoriesEqual(result.original_inventory, result.rollback_inventory) &&
                                    !TreeHasReparsePoint(cadDirectory) && !TreeHasHardLinks(cadDirectory);
                            }
                            catch (Exception rollbackError) { result.rollback_error = SafeException(rollbackError); }
                        }
                        result.status = result.rollback_succeeded ? "EVIDENCE_COMMIT_FAILED_ROLLBACK_COMPLETE" :
                            "EVIDENCE_COMMIT_FAILED_ROLLBACK_INCOMPLETE";
                        if (!evidenceContained || !receiptContained)
                            result.status = "UNPAIRED_SUCCESS_ARTIFACT_CONTAINMENT_FAILED";
                        Console.Error.WriteLine(SafeException(writeError));
                        exitCode = 92;
                    }
                }
            }

            if (exitCode == 0 && result.success && result.evidence_commit_verified && File.Exists(outputPath))
                Console.WriteLine(outputPath);
            return exitCode;
        }

        private static int SelfTest()
        {
            bool formulas = ExpectedSlots.Length == 7 && ExpectedCircles.Length == 7 &&
                Close(ExpectedSlots[0], 159.21428571428572, 1e-9) &&
                Close(ExpectedSlots[3], 943.5, 1e-9) &&
                Close(ExpectedSlots[6], 1727.7857142857142, 1e-9) &&
                ExpectedSlots.Zip(ExpectedCircles, (slot, circle) => Close(slot - circle, 60.0, 1e-9)).All(value => value);
            bool templates = SlotTemplates.Length == 22 &&
                SlotTemplates.Count(row => Close(row.length, 0.8, 1e-9)) == 8 &&
                SlotTemplates.Count(row => Close(row.length, 0.788322, 1e-9)) == 2 &&
                SlotTemplates.Count(row => Close(row.length, 2.043171, 1e-9)) == 2 &&
                SlotTemplates.Count(row => Close(row.length, 3.036873, 1e-9)) == 4 &&
                SlotTemplates.Count(row => Close(row.length, 5.0, 1e-9)) == 2 &&
                SlotTemplates.Count(row => Close(row.length, 21.834595, 1e-9)) == 4;
            string fixtureRoot = @"C:\fixture\attempts";
            string allowedFixture = Path.Combine(fixtureRoot, "self-test-task", "attempt-0001");
            bool paths = IsAllowedAttemptRoot(allowedFixture, fixtureRoot) &&
                !IsAllowedAttemptRoot(Path.Combine(fixtureRoot, "self-test-task"), fixtureRoot) &&
                !IsAllowedAttemptRoot(Path.Combine(fixtureRoot, "..", "escaped", "attempt-0001"), fixtureRoot);
            bool evidenceContract = TrustedRecipeId == "winnsen-16029-888w-14door-native-v1" &&
                TrustedRecipeRegistrySha256.Length == 64 && LayoutRuleEvidenceSha256.Length == 64 &&
                MechanicalTonguePartName == "锁舌.SLDPRT" && MechanicalTongueSha256.Length == 64;
            bool mirrorContract = CadDirectoryRelativePath == @"native_cad\working_pack" &&
                ExpectedAuthorizationSchema == "winnsen.native_execution_authorization.v1" &&
                ExpectedToolId == "native_lock_topology_888x14_v1";
            var successFixture = new Result
            {
                authorization_gate = true,
                trusted_toolchain_manifest_gate = true,
                stage_receipt_contract_gate = true,
                predecessor_receipt_chain_gate = true,
                trusted_layout_rule_gate = true,
                path_allowlist_match = true,
                backup_created = true,
                right_replaced_transactionally = true,
                post_inventory_gate = true,
                final_process_cleanup_gate = true
            };
            bool successCommitPredicate = CanPrepareSuccessEvidence(true, successFixture) &&
                !CanPrepareSuccessEvidence(false, successFixture);
            successFixture.error = "forced failure";
            bool exceptionCannotCommit = !CanPrepareSuccessEvidence(true, successFixture);
            successFixture.error = "";
            successFixture.rollback_attempted = true;
            bool rollbackCannotCommit = !CanPrepareSuccessEvidence(true, successFixture);
            var commitmentFixture = new Dictionary<string, object>(StringComparer.Ordinal)
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
            bool commitmentContract = string.Equals(EvidenceCommitmentDigest(commitmentFixture),
                "684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1",
                StringComparison.Ordinal);
            var numericCommitmentFixture = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "a", 0.1592142857142857 }, { "b", -1.4150714285714288 },
                { "c", 0.0000001 }, { "d", 0.000001 }, { "e", 1e20 }, { "f", 1e21 },
                { "g", 254.4285714285714 }, { "ticks", 638906112000000000L },
                { "html", "<structure>&engineering" }
            };
            bool numericCommitmentContract = string.Equals(
                EvidenceCommitmentDigest(numericCommitmentFixture),
                "3BC67213D48D35E839E000955BD2B60A862C13A1EA0DBC0BE4ACBEAD12035C65",
                StringComparison.Ordinal);
            var inventoryDigestFixture = new List<InventoryRow>
            {
                new InventoryRow { relative_path = "B.SLDPRT", sha256 = new string('B', 64), size = 17 },
                new InventoryRow { relative_path = "a.SLDASM", sha256 = new string('A', 64), size = 999 }
            };
            bool canonicalInventoryDigestContract = string.Equals(
                InventoryDigest(inventoryDigestFixture),
                "EB0A5DAF0EA1214BDBB9C581A0FD853E29C445AE16C5945A15C92ECC0D8E38A5",
                StringComparison.Ordinal);
            string standardSourceFixture =
                "private const string ExpectedSourceSha256=\"" + new string('A', 64) +
                "\";\r\nprivate const string ExpectedContractSnapshotSha256 = \"" +
                new string('B', 64) + "\";\r\ninternal class Fixture {}\r\n";
            bool standardToolchainNormalizerContract = NormalizeStandardToolSource(
                    standardSourceFixture).Contains("ExpectedSourceSha256=\"__SOURCE_SHA256__\"") &&
                NormalizeStandardToolSource(standardSourceFixture).Contains(
                    "ExpectedContractSnapshotSha256 = \"__CONTRACT_SHA256__\"") &&
                !NormalizeStandardToolSource(standardSourceFixture).Contains("\r");
            bool passed = formulas && templates && paths && evidenceContract && mirrorContract &&
                successCommitPredicate && exceptionCannotCommit && rollbackCannotCommit &&
                commitmentContract && numericCommitmentContract && canonicalInventoryDigestContract &&
                standardToolchainNormalizerContract;
            Console.WriteLine("{\"selfTest\":\"" + (passed ? "PASS" : "FAIL") +
                "\",\"solidWorksStarted\":false,\"slotRows\":" + Json(ExpectedSlots) +
                ",\"circleRows\":" + Json(ExpectedCircles) +
                ",\"slotTemplateCount\":" + SlotTemplates.Length.ToString(CultureInfo.InvariantCulture) +
                ",\"successCommitPredicate\":" + successCommitPredicate.ToString().ToLowerInvariant() +
                ",\"exceptionCannotCommit\":" + exceptionCannotCommit.ToString().ToLowerInvariant() +
                ",\"rollbackCannotCommit\":" + rollbackCannotCommit.ToString().ToLowerInvariant() +
                ",\"sharedCommitmentContract\":" + commitmentContract.ToString().ToLowerInvariant() +
                ",\"sharedNumericCommitmentContract\":" +
                    numericCommitmentContract.ToString().ToLowerInvariant() +
                ",\"canonicalInventoryDigestContract\":" +
                    canonicalInventoryDigestContract.ToString().ToLowerInvariant() +
                ",\"standardToolchainNormalizerContract\":" +
                    standardToolchainNormalizerContract.ToString().ToLowerInvariant() + "}");
            return passed ? 0 : 1;
        }

        private static bool CanPrepareSuccessEvidence(bool stagesComplete, Result result)
        {
            return stagesComplete && result != null && result.final_process_cleanup_gate &&
                result.authorization_gate && result.trusted_toolchain_manifest_gate &&
                result.stage_receipt_contract_gate && result.predecessor_receipt_chain_gate &&
                result.trusted_layout_rule_gate && result.path_allowlist_match &&
                result.backup_created && result.right_replaced_transactionally &&
                result.post_inventory_gate && !result.rollback_attempted &&
                !result.rollback_succeeded && !result.rollback_blocked_by_process_gate &&
                string.IsNullOrWhiteSpace(result.rollback_error) &&
                string.IsNullOrWhiteSpace(result.error);
        }

        private static void EditLeft(Result result)
        {
            Require(RelatedProcessIds().Count == 0, "DIRTY_BASELINE_BEFORE_LEFT_EDIT", 10,
                "CAD processes appeared before the left edit session");
            ISldWorks sw = null;
            ModelDoc2 model = null;
            SessionRecord session = new SessionRecord { purpose = "edit_left_lock_seed_and_pattern" };
            result.sessions.Add(session);
            try
            {
                sw = StartOwned(result, session);
                Require(sw != null && IsExclusive(session), "LEFT_SESSION_OWNERSHIP_GATE_FAILED", 10,
                    "left edit session is not a new exclusive owned SolidWorks process");
                int errors = 0, warnings = 0;
                model = OpenPart(sw, result.left_part_path, false, ref errors, ref warnings);
                session.open_errors = errors;
                session.open_warnings = warnings;
                Require(model != null && errors == 0 && warnings == 0, "LEFT_OPEN_FAILED", 11,
                    "left partition requires open errors=0 and warnings=0");
                model.ShowFeatureErrorDialog = false;
                Require(model.ForceRebuild3(false), "LEFT_INITIAL_REBUILD_FAILED", 11, "ForceRebuild3 returned false");
                result.left_before = CaptureGeometry(model, "L");
                Require(BasicPartGate(result.left_before) &&
                    RowsEqual(result.left_before.slot_rows_mm, new[] { 486.0, 1248.5, 1706.0 }, RowToleranceMm) &&
                    RowsEqual(result.left_before.circle_rows_mm, new[] { 426.0, 1188.5, 1646.0 }, RowToleranceMm),
                    "LEFT_VERIFIED_SEED_TOPOLOGY_MISMATCH", 12,
                    "left seed must be the verified V37/V35 native three-row topology before editing");
                Require(FindFeature(model, NativePatternName) == null, "NATIVE_PATTERN_ALREADY_PRESENT", 12,
                    "start a new attempt instead of editing an already modified part");
                foreach (string name in LegacyPatternNames)
                {
                    Feature feature = FindFeature(model, name);
                    Require(feature != null && SetSuppression(feature, true), "LEGACY_PATTERN_SUPPRESSION_FAILED", 13, name);
                }
                Require(model.ForceRebuild3(false), "LEFT_SEED_ONLY_REBUILD_FAILED", 13, "ForceRebuild3 returned false");
                result.left_seed_only_before_move = CaptureGeometry(model, "L");
                Require(BasicPartGate(result.left_seed_only_before_move) &&
                    RowsEqual(result.left_seed_only_before_move.slot_rows_mm, new[] { SeedSlotRowMm }, RowToleranceMm) &&
                    RowsEqual(result.left_seed_only_before_move.circle_rows_mm, new[] { SeedCircleRowMm }, RowToleranceMm),
                    "LEFT_SEED_ISOLATION_GATE_FAILED", 13,
                    "legacy suppression must leave exactly the native slot seed at Y1706 and diameter-5 seed at Y1646");

                double targetTopSlot = ExpectedSlots[ExpectedSlots.Length - 1];
                double deltaMm = targetTopSlot - result.left_seed_only_before_move.slot_rows_mm[0];
                result.seed_translation_mm = deltaMm;
                MoveSlotBlock(sw, model, result, deltaMm);
                MoveCircleDatumLine(model, result, deltaMm);
                Require(model.ForceRebuild3(false), "LEFT_MOVED_SEED_REBUILD_FAILED", 14, "ForceRebuild3 returned false");
                result.left_seed_only_after_move = CaptureGeometry(model, "L");
                Require(BasicPartGate(result.left_seed_only_after_move) &&
                    RowsEqual(result.left_seed_only_after_move.slot_rows_mm, new[] { targetTopSlot }, RowToleranceMm) &&
                    RowsEqual(result.left_seed_only_after_move.circle_rows_mm, new[] { targetTopSlot - 60.0 }, RowToleranceMm),
                    "MOVED_SEED_EDGE_READBACK_GATE_FAILED", 14,
                    "the moved seed must be proven from real edges, not sketch or pattern parameters");

                Feature pattern = CreateSevenRowPattern(model, result);
                Require(pattern != null, "NATIVE_SEVEN_ROW_PATTERN_CREATE_FAILED", 15,
                    "could not create the native seven-instance linear pattern");
                Require(model.ForceRebuild3(false), "LEFT_PATTERN_REBUILD_FAILED", 15, "ForceRebuild3 returned false");
                result.left_after_pattern = CaptureGeometry(model, "L");
                Require(GeometryGate(result.left_after_pattern, "L"), "LEFT_REAL_EDGE_TOPOLOGY_GATE_FAILED", 16,
                    "left part does not contain exactly seven complete 22-edge slots and seven paired diameter-5 openings");

                int saveErrors = 0, saveWarnings = 0;
                result.left_save_attempted = true;
                result.left_saved = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref saveErrors, ref saveWarnings);
                result.left_save_errors = saveErrors;
                result.left_save_warnings = saveWarnings;
                Require(result.left_saved && saveErrors == 0 && !model.GetSaveFlag(), "LEFT_SAVE_FAILED", 17,
                    "left part was not cleanly saved");
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseOwned(ref sw, result, session);
            }
            result.left_sha_after_save = Sha256(result.left_part_path);
            Require(!string.Equals(result.left_sha_before, result.left_sha_after_save,
                StringComparison.OrdinalIgnoreCase), "LEFT_HASH_DID_NOT_CHANGE", 17,
                "saved left part hash is unchanged");
        }

        private static void CreateNativeRightMirror(Result result)
        {
            Require(RelatedProcessIds().Count == 0, "DIRTY_BASELINE_BEFORE_RIGHT_MIRROR", 20,
                "CAD processes appeared before the native right mirror session");
            Require(!File.Exists(result.temporary_right_path), "MIRROR_TEMP_RIGHT_ALREADY_EXISTS", 20,
                "the transaction-specific temporary right path must be absent");
            ISldWorks sw = null;
            ModelDoc2 left = null;
            ModelDoc2 temporaryRight = null;
            Feature mirrorFeature = null;
            MirrorPartFeatureData mirrorData = null;
            SessionRecord session = new SessionRecord { purpose = "mirror_native_right_partition" };
            result.sessions.Add(session);
            try
            {
                sw = StartOwned(result, session);
                Require(sw != null && IsExclusive(session), "RIGHT_MIRROR_SESSION_OWNERSHIP_GATE_FAILED", 20,
                    "right mirror session is not a new exclusive owned SolidWorks 2020 process");
                int errors = 0, warnings = 0;
                left = OpenPart(sw, result.left_part_path, true, ref errors, ref warnings);
                session.open_errors = errors;
                session.open_warnings = warnings;
                Require(left != null && errors == 0 && warnings == 0 && left.ForceRebuild3(false),
                    "MIRROR_LEFT_OPEN_FAILED", 21,
                    "the edited native left partition could not be opened read-only");
                GeometrySnapshot leftHealth = CaptureGeometry(left, "L");
                Require(GeometryGate(leftHealth, "L") && ExternalReferenceCount(left) == 0,
                    "MIRROR_LEFT_HEALTH_GATE_FAILED", 21,
                    "MirrorPart2 input must be one healthy link-free native sheet-metal body");
                left.ClearSelection2(true);
                Require(SelectRightPlane(left), "MIRROR_RIGHT_PLANE_SELECTION_FAILED", 21,
                    "could not select the native Right plane");
                PartDoc leftPart = left as PartDoc;
                Require(leftPart != null, "MIRROR_LEFT_NOT_PARTDOC", 21, "left partition is not a PartDoc");
                int options = (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSolids |
                    (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSMInfo |
                    (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportIndProps |
                    (int)swMirrorPartOptions_e.swMirrorPartOptions_ImportCutListProperties;
                mirrorFeature = leftPart.MirrorPart2(true, options, out temporaryRight);
                Require(mirrorFeature != null && temporaryRight != null, "MIRRORPART2_CREATE_FAILED", 22,
                    "IPartDoc.MirrorPart2 did not create a temporary native right partition");
                mirrorData = mirrorFeature.GetDefinition() as MirrorPartFeatureData;
                Require(mirrorData != null, "MIRROR_FEATURE_DATA_MISSING", 22,
                    "MirrorPart2 did not expose MirrorPartFeatureData");
                bool accessed = mirrorData.AccessSelections(temporaryRight, null);
                bool importedSheetMetal = accessed && mirrorData.SheetMetalInformation;
                if (accessed) mirrorData.ReleaseSelectionAccess();
                Require(importedSheetMetal, "MIRROR_SHEET_METAL_INFO_NOT_IMPORTED", 22,
                    "MirrorPart2 must preserve sheet-metal information");
                Require(temporaryRight.ForceRebuild3(false), "MIRROR_TEMP_RIGHT_REBUILD_FAILED", 22,
                    "temporary right failed ForceRebuild3");
                result.right_after_mirror = CaptureGeometry(temporaryRight, "R");
                Require(MirrorGeometryGate(result.right_after_mirror, "R"),
                    "MIRROR_TEMP_RIGHT_HEALTH_GATE_FAILED", 23,
                    "temporary right must have one body, MirrorStock/MirrorPart, SheetMetal, FlatPattern, no BaseBody/Imported, and exact real edges");
                result.temporary_right_external_reference_count = ExternalReferenceCount(temporaryRight);
                Require(result.temporary_right_external_reference_count == 0,
                    "MIRROR_TEMP_RIGHT_EXTERNAL_REFERENCE_GATE_FAILED", 23,
                    "temporary right must contain zero external references");
                int saveErrors = 0, saveWarnings = 0;
                result.right_save_attempted = true;
                result.right_saved = temporaryRight.Extension.SaveAs(result.temporary_right_path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null,
                    ref saveErrors, ref saveWarnings);
                result.right_save_errors = saveErrors;
                result.right_save_warnings = saveWarnings;
                Require(result.right_saved && saveErrors == 0 && saveWarnings == 0 &&
                    File.Exists(result.temporary_right_path) && !temporaryRight.GetSaveFlag(),
                    "MIRROR_TEMP_RIGHT_SAVE_FAILED", 24,
                    "temporary native right did not save cleanly");
                result.mirror_api = "IPartDoc.MirrorPart2";
                result.mirror_options = options;
                result.mirror_break_link = true;
            }
            finally
            {
                Release(mirrorData);
                Release(mirrorFeature);
                CloseDocument(sw, ref temporaryRight);
                CloseDocument(sw, ref left);
                CloseOwned(ref sw, result, session);
            }
            result.right_sha_after_save = Sha256(result.temporary_right_path);
        }

        private static GeometrySnapshot VerifyPart(Result result, string path, string side,
            string purpose, bool requireNativeMirror)
        {
            Require(RelatedProcessIds().Count == 0, "DIRTY_BASELINE_BEFORE_" + side + "_REOPEN", 27,
                "CAD processes appeared before fresh read-only reopen");
            ISldWorks sw = null;
            ModelDoc2 model = null;
            SessionRecord session = new SessionRecord { purpose = purpose };
            result.sessions.Add(session);
            string shaBefore = Sha256(path);
            GeometrySnapshot snapshot = null;
            try
            {
                sw = StartOwned(result, session);
                Require(sw != null && IsExclusive(session), side + "_REOPEN_OWNERSHIP_GATE_FAILED", 27,
                    "read-only reopen is not a new exclusive owned SolidWorks process");
                int errors = 0, warnings = 0;
                model = OpenPart(sw, path, true, ref errors, ref warnings);
                session.open_errors = errors;
                session.open_warnings = warnings;
                Require(model != null && errors == 0 && warnings == 0, side + "_REOPEN_FAILED", 28,
                    "read-only reopen requires errors=0 and warnings=0");
                Require(model.ForceRebuild3(false), side + "_REOPEN_REBUILD_FAILED", 28,
                    "ForceRebuild3 returned false");
                snapshot = CaptureGeometry(model, side);
                Require((requireNativeMirror ? MirrorGeometryGate(snapshot, side) : GeometryGate(snapshot, side)),
                    side + "_REOPEN_REAL_EDGE_GATE_FAILED", 28,
                    "fresh reopen did not prove the exact real-edge topology");
                if (requireNativeMirror)
                    Require(ExternalReferenceCount(model) == 0,
                        "RIGHT_REOPEN_EXTERNAL_REFERENCE_GATE_FAILED", 29,
                        "fresh reopened native right contains an external reference");
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseOwned(ref sw, result, session);
            }
            string shaAfter = Sha256(path);
            Require(string.Equals(shaBefore, shaAfter, StringComparison.OrdinalIgnoreCase),
                side + "_HASH_CHANGED_DURING_READONLY_REOPEN", 29,
                "read-only verification changed the part hash");
            snapshot.source_sha256 = shaAfter;
            snapshot.read_only_reopen = true;
            snapshot.open_errors = session.open_errors;
            snapshot.open_warnings = session.open_warnings;
            snapshot.session_process_id = session.sldworks_process_id;
            return snapshot;
        }

        private static void MoveSlotBlock(ISldWorks sw, ModelDoc2 model, Result result,
            double deltaMm)
        {
            Feature sketchFeature = FindFeature(model, SlotSketchName);
            Require(sketchFeature != null, "SLOT_SKETCH_MISSING", 14, SlotSketchName);
            Sketch sketch = Safe(delegate { return sketchFeature.GetSpecificFeature2() as Sketch; }, null);
            Require(sketch != null && sketch.GetSketchBlockInstanceCount() == 1,
                "SLOT_BLOCK_INSTANCE_COUNT_MISMATCH", 14,
                "slot sketch must contain exactly one native block instance");
            Array instances = Safe(delegate { return sketch.GetSketchBlockInstances() as Array; }, null);
            SketchBlockInstance instance = instances == null || instances.Length != 1 ? null :
                instances.GetValue(0) as SketchBlockInstance;
            Require(instance != null, "SLOT_BLOCK_INSTANCE_UNAVAILABLE", 14, SlotSketchName);
            MathPoint oldPoint = Safe(delegate { return instance.InstancePosition; }, null);
            double[] before = ToNumbers(oldPoint == null ? null : oldPoint.ArrayData);
            Require(before.Length >= 3, "SLOT_BLOCK_POSITION_UNAVAILABLE", 14, SlotSketchName);
            Require(before.Take(3).All(value => !double.IsNaN(value) && !double.IsInfinity(value)),
                "SLOT_BLOCK_POSITION_INVALID", 14,
                "InstancePosition must be read from the live sketch before applying the edge-derived delta");
            result.slot_block_position_before_mm = before.Select(value => value * 1000.0).ToArray();
            double[] after = (double[])before.Clone();
            after[1] += deltaMm / 1000.0;
            MathUtility math = Safe(delegate { return sw.GetMathUtility() as MathUtility; }, null);
            MathPoint newPoint = math == null ? null : Safe(delegate
            {
                return math.CreatePoint(after) as MathPoint;
            }, null);
            Require(newPoint != null, "SLOT_BLOCK_TARGET_POINT_CREATE_FAILED", 14, SlotSketchName);
            instance.InstancePosition = newPoint;
            MathPoint readbackPoint = Safe(delegate { return instance.InstancePosition; }, null);
            double[] readback = ToNumbers(readbackPoint == null ? null : readbackPoint.ArrayData);
            result.slot_block_position_after_mm = readback.Select(value => value * 1000.0).ToArray();
            Require(readback.Length >= 3 && Close(readback[0], after[0], 1e-9) &&
                Close(readback[1], after[1], 1e-9) && Close(readback[2], after[2], 1e-9),
                "SLOT_BLOCK_POSITION_READBACK_FAILED", 14,
                "InstancePosition setter did not persist the exact delta-derived point");
            Release(readbackPoint);
            Release(newPoint);
            Release(math);
            Release(oldPoint);
            Release(instance);
            Release(sketch);
        }

        private static void MoveCircleDatumLine(ModelDoc2 model, Result result, double deltaMm)
        {
            Feature sketchFeature = FindFeature(model, CircleSketchName);
            Require(sketchFeature != null, "CIRCLE_SKETCH_MISSING", 14, CircleSketchName);
            Sketch sketch = Safe(delegate { return sketchFeature.GetSpecificFeature2() as Sketch; }, null);
            Require(sketch != null, "CIRCLE_SKETCH_UNAVAILABLE", 14, CircleSketchName);
            Array segments = Safe(delegate { return sketch.GetSketchSegments() as Array; }, null);
            Require(segments != null && segments.Length == 2, "CIRCLE_SKETCH_SEGMENT_COUNT_MISMATCH", 14,
                "circle sketch must contain one circle and one construction datum line");
            SketchLine datumLine = null;
            SketchArc circle = null;
            foreach (object item in segments)
            {
                SketchLine line = item as SketchLine;
                if (line != null && Safe(delegate { return ((SketchSegment)line).ConstructionGeometry; }, false))
                    datumLine = line;
                SketchArc arc = item as SketchArc;
                if (arc != null && Safe(delegate { return arc.IsCircle(); }, 0) != 0) circle = arc;
            }
            Require(datumLine != null && circle != null, "CIRCLE_DATUM_TOPOLOGY_UNPROVEN", 14,
                "could not identify the unique construction line and circle");
            SketchPoint start = Safe(delegate { return datumLine.IGetStartPoint2(); }, null);
            SketchPoint end = Safe(delegate { return datumLine.IGetEndPoint2(); }, null);
            SketchPoint center = Safe(delegate { return circle.IGetCenterPoint2(); }, null);
            double[] a = PointCoordinates(start);
            double[] b = PointCoordinates(end);
            double[] c = PointCoordinates(center);
            Require(a.Length == 3 && b.Length == 3 && c.Length == 3 &&
                Close(a[1] * 1000.0, SeedSlotRowMm, 0.02) &&
                Close(b[1] * 1000.0, SeedSlotRowMm, 0.02) &&
                Close(c[1] * 1000.0, SeedCircleRowMm, 0.02) &&
                Close(a[1] * 1000.0 - c[1] * 1000.0, 60.0, 0.02),
                "CIRCLE_DATUM_MAPPING_UNPROVEN", 14,
                "actual sketch points must prove a Y1706 datum line and a Y1646 circle center");
            result.circle_datum_before_mm = new[] { a[0] * 1000.0, a[1] * 1000.0,
                b[0] * 1000.0, b[1] * 1000.0, c[0] * 1000.0, c[1] * 1000.0 };
            bool startMoved = start.SetCoords(a[0], a[1] + deltaMm / 1000.0, a[2]);
            bool endMoved = end.SetCoords(b[0], b[1] + deltaMm / 1000.0, b[2]);
            Require(startMoved && endMoved, "CIRCLE_DATUM_SETCOORDS_FAILED", 14,
                "construction line endpoints rejected the delta-derived translation");
            result.circle_datum_after_request_mm = new[] { a[0] * 1000.0,
                a[1] * 1000.0 + deltaMm, b[0] * 1000.0, b[1] * 1000.0 + deltaMm };
            Release(center);
            Release(end);
            Release(start);
            Release(circle);
            Release(datumLine);
            Release(sketch);
        }

        private static Feature CreateSevenRowPattern(ModelDoc2 model, Result result)
        {
            Feature source = FindFeature(model, SourcePatternName);
            Require(source != null, "SOURCE_PATTERN_MISSING", 15, SourcePatternName);
            LinearPatternFeatureData sourceData = Safe(delegate
            {
                return source.GetDefinition() as LinearPatternFeatureData;
            }, null);
            Require(sourceData != null && sourceData.AccessSelections(model, null),
                "SOURCE_PATTERN_DEFINITION_UNAVAILABLE", 15, SourcePatternName);
            try
            {
                object axis = Safe(delegate { return sourceData.D1Axis; }, null);
                Array seeds = Safe(delegate { return sourceData.PatternFeatureArray as Array; }, null);
                List<string> seedNames = new List<string>();
                if (seeds != null)
                {
                    foreach (object item in seeds)
                    {
                        Feature seed = item as Feature;
                        if (seed != null) seedNames.Add(Safe(delegate { return seed.Name; }, ""));
                    }
                }
                result.pattern_seed_names = seedNames.ToArray();
                Require(axis != null && seeds != null && seeds.Length == 2 &&
                    seedNames.Contains("切除-拉伸3") && seedNames.Contains("切除-拉伸5"),
                    "SOURCE_PATTERN_SEED_CONTRACT_MISMATCH", 15,
                    "source pattern must expose the native slot and diameter-5 cut seeds");
                model.ClearSelection2(true);
                SelectionMgr selection = model.SelectionManager as SelectionMgr;
                SelectData axisData = selection == null ? null : selection.CreateSelectData() as SelectData;
                Entity axisEntity = axis as Entity;
                if (axisData != null) axisData.Mark = 1;
                bool axisSelected = axisEntity != null && axisData != null && axisEntity.Select4(false, axisData);
                int seedsSelected = 0;
                foreach (object item in seeds)
                {
                    Feature seed = item as Feature;
                    if (seed != null && seed.Select2(true, 4)) seedsSelected++;
                }
                result.pattern_axis_selected = axisSelected;
                result.pattern_seed_selected_count = seedsSelected;
                Require(axisSelected && seedsSelected == 2, "NATIVE_PATTERN_SELECTION_FAILED", 15,
                    "axis mark=1 and both seed features mark=4 are required");
                FeatureManager manager = model.FeatureManager;
                LinearPatternFeatureData data = manager.CreateDefinition(
                    (int)swFeatureNameID_e.swFmLPattern) as LinearPatternFeatureData;
                Require(data != null, "NATIVE_PATTERN_DEFINITION_CREATE_FAILED", 15, NativePatternName);
                data.BodyPattern = false;
                data.D1EndCondition = (int)swPatternEndCondition_e.swPatternEndCondition_SpacingAndInstances;
                data.D1Spacing = DoorPitchMm / 1000.0;
                data.D1TotalInstances = 7;
                data.D1ReverseDirection = false;
                data.D2EndCondition = (int)swPatternEndCondition_e.swPatternEndCondition_SpacingAndInstances;
                data.D2Spacing = Math.Max(sourceData.D2Spacing, 0.001);
                data.D2TotalInstances = 1;
                data.D2ReverseDirection = sourceData.D2ReverseDirection;
                data.D2PatternSeedOnly = sourceData.D2PatternSeedOnly;
                data.GeometryPattern = sourceData.GeometryPattern;
                data.VarySketch = sourceData.VarySketch;
                data.PropagateVisualProperty = sourceData.PropagateVisualProperty;
                Feature created = manager.CreateFeature(data) as Feature;
                if (created != null) created.Name = NativePatternName;
                model.ClearSelection2(true);
                result.pattern_spacing_mm = DoorPitchMm;
                result.pattern_instance_count = 7;
                result.pattern_reverse_direction = false;
                Release(data);
                Release(axisData);
                Release(selection);
                Release(manager);
                Release(axis);
                return created;
            }
            finally
            {
                TryAction(delegate { sourceData.ReleaseSelectionAccess(); });
                Release(sourceData);
            }
        }

        private static GeometrySnapshot CaptureGeometry(ModelDoc2 model, string side)
        {
            var snapshot = new GeometrySnapshot { side = side };
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureFeatureHealth(feature, snapshot, 0);
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            if (feature != null)
            {
                snapshot.traversal_complete = false;
                Release(feature);
            }

            PartDoc part = model as PartDoc;
            Array bodies = part == null ? null : Safe(delegate
            {
                return part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            }, null);
            if (bodies == null) return snapshot;
            snapshot.body_count = bodies.Length;
            foreach (object bodyItem in bodies)
            {
                Body2 body = bodyItem as Body2;
                Array edges = body == null ? null : Safe(delegate { return body.GetEdges() as Array; }, null);
                if (edges != null)
                {
                    foreach (object edgeItem in edges)
                    {
                        Edge edge = edgeItem as Edge;
                        if (edge == null) continue;
                        var row = new EdgeRow { index = snapshot.edges.Count + 1 };
                        Curve curve = Safe(delegate { return edge.GetCurve() as Curve; }, null);
                        CurveParamData parameter = Safe(delegate
                        {
                            return edge.GetCurveParams3() as CurveParamData;
                        }, null);
                        if (parameter != null)
                        {
                            row.length_mm = curve == null ? 0.0 : Safe(delegate
                            {
                                return curve.GetLength3(parameter.UMinValue, parameter.UMaxValue);
                            }, 0.0) * 1000.0;
                            row.param_start_mm = ToPointMm(Safe(delegate { return parameter.StartPoint; }, null));
                        }
                        row.vertex_start_mm = VertexMm(Safe(delegate { return edge.GetStartVertex() as Vertex; }, null));
                        row.vertex_end_mm = VertexMm(Safe(delegate { return edge.GetEndVertex() as Vertex; }, null));
                        snapshot.edges.Add(row);
                        Release(parameter);
                        Release(curve);
                        Release(edge);
                    }
                }
                Release(body);
            }
            snapshot.edge_count = snapshot.edges.Count;
            AnalyzeGeometry(snapshot);
            return snapshot;
        }

        private static void CaptureFeatureHealth(Feature feature, GeometrySnapshot snapshot, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || snapshot.feature_count > 20000)
            {
                snapshot.traversal_complete = false;
                return;
            }
            string type = Safe(delegate { return feature.GetTypeName2(); }, "");
            string name = Safe(delegate { return feature.Name; }, "");
            snapshot.feature_types.Add(type);
            if (string.Equals(type, "SheetMetal", StringComparison.OrdinalIgnoreCase)) snapshot.has_sheet_metal = true;
            if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase)) snapshot.has_flat_pattern = true;
            if (string.Equals(type, "MirrorStock", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "MirrorPart", StringComparison.OrdinalIgnoreCase))
                snapshot.mirror_feature_count++;
            if (string.Equals(type, "BaseBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Imported", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ImportedBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ForeignBody", StringComparison.OrdinalIgnoreCase))
                snapshot.forbidden_import_feature_count++;
            bool warning = false;
            int error2 = -1;
            try { error2 = feature.GetErrorCode2(out warning); } catch { }
            int error = Safe(delegate { return feature.GetErrorCode(); }, -1);
            bool suppressed = Safe(delegate { return feature.IsSuppressed(); }, false);
            snapshot.feature_count++;
            if (!suppressed && (error > 0 || error2 > 0)) snapshot.error_feature_count++;
            if (!suppressed && warning) snapshot.warning_feature_count++;
            Feature child = Safe(delegate { return feature.GetFirstSubFeature() as Feature; }, null);
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureHealth(child, snapshot, depth + 1);
                Feature next = Safe(delegate { return child.GetNextSubFeature() as Feature; }, null);
                Release(child);
                child = next;
            }
            if (child != null)
            {
                snapshot.traversal_complete = false;
                Release(child);
            }
        }

        private static void AnalyzeGeometry(GeometrySnapshot snapshot)
        {
            List<double> candidates = new List<double>();
            foreach (EdgeRow edge in snapshot.edges)
            {
                foreach (SlotTemplate template in SlotTemplates.Where(row => !Close(row.length, 0.8, 1e-9)))
                    candidates.AddRange(DerivedRows(edge, template));
            }
            foreach (double row in DistinctRows(candidates))
            {
                SlotAnalysis analysis = AnalyzeSlot(snapshot.edges, row, snapshot.side);
                if (analysis.matched_edge_count > 0) snapshot.slot_analyses.Add(analysis);
            }
            snapshot.slot_rows_mm = snapshot.slot_analyses.Where(row => row.full_fingerprint)
                .Select(row => row.row_mm).OrderBy(value => value).ToArray();
            snapshot.slot_fragment_count = snapshot.slot_analyses.Count(row => !row.full_fingerprint);

            List<double> circleCandidates = new List<double>();
            foreach (EdgeRow edge in snapshot.edges)
            {
                if (!Close(edge.length_mm, CircleLengthMm, LengthToleranceMm) || edge.param_start_mm.Length < 3) continue;
                double x = Math.Abs(edge.param_start_mm[0]);
                double z = edge.param_start_mm[2];
                if ((Close(x, 42.5, CoordinateToleranceMm) && Close(z, -19.3, CoordinateToleranceMm)) ||
                    (Close(x, 47.5, CoordinateToleranceMm) && Close(z, -18.5, CoordinateToleranceMm)))
                    circleCandidates.Add(edge.param_start_mm[1]);
            }
            foreach (double row in DistinctRows(circleCandidates))
            {
                CircleAnalysis analysis = AnalyzeCircle(snapshot.edges, row, snapshot.side);
                if (analysis.matched_edge_count > 0) snapshot.circle_analyses.Add(analysis);
            }
            snapshot.circle_rows_mm = snapshot.circle_analyses.Where(row => row.full_diameter5_pair)
                .Select(row => row.row_mm).OrderBy(value => value).ToArray();
            snapshot.circle_fragment_count = snapshot.circle_analyses.Count(row => !row.full_diameter5_pair);
            snapshot.unique_slot_profile_signature_count = snapshot.slot_analyses
                .Where(row => row.full_fingerprint).Select(row => row.normalized_signature_sha256)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        private static SlotAnalysis AnalyzeSlot(List<EdgeRow> edges, double row, string side)
        {
            var analysis = new SlotAnalysis { row_mm = Round(row, 9) };
            var used = new HashSet<int>();
            var signature = new List<string>();
            int expectedSign = side == "L" ? -1 : 1;
            foreach (SlotTemplate template in SlotTemplates)
            {
                List<EdgeRow> matches = edges.Where(candidate => EdgeMatches(candidate, template, row)).ToList();
                if (matches.Count != 1)
                {
                    analysis.missing_or_duplicate_template_ids.Add(template.id);
                    continue;
                }
                EdgeRow edge = matches[0];
                if (!used.Add(edge.index)) analysis.ambiguous_edge_indexes.Add(edge.index);
                analysis.matched_edge_indexes.Add(edge.index);
                bool orientation = edge.vertex_start_mm.Length >= 3 && edge.vertex_end_mm.Length >= 3 &&
                    Math.Sign(edge.vertex_start_mm[0]) == expectedSign &&
                    Math.Sign(edge.vertex_end_mm[0]) == expectedSign;
                if (!orientation) analysis.side_orientation_exact = false;
                signature.Add(NormalizedEdgeSignature(template.id, edge, row));
            }
            analysis.matched_edge_count = used.Count;
            analysis.full_fingerprint = used.Count == 22 &&
                analysis.missing_or_duplicate_template_ids.Count == 0 &&
                analysis.ambiguous_edge_indexes.Count == 0 && analysis.side_orientation_exact;
            if (analysis.full_fingerprint)
                analysis.normalized_signature_sha256 = Sha256Text(string.Join("\n", signature));
            return analysis;
        }

        private static CircleAnalysis AnalyzeCircle(List<EdgeRow> edges, double row, string side)
        {
            var analysis = new CircleAnalysis { row_mm = Round(row, 9) };
            int sign = side == "L" ? -1 : 1;
            EdgeRow c1 = edges.SingleOrDefault(edge => CircleMatches(edge, row, 42.5, -19.3));
            EdgeRow c2 = edges.SingleOrDefault(edge => CircleMatches(edge, row, 47.5, -18.5));
            foreach (EdgeRow edge in new[] { c1, c2 }.Where(value => value != null))
            {
                analysis.matched_edge_indexes.Add(edge.index);
                if (edge.param_start_mm.Length < 3 || Math.Sign(edge.param_start_mm[0]) != sign)
                    analysis.side_orientation_exact = false;
            }
            analysis.matched_edge_count = analysis.matched_edge_indexes.Distinct().Count();
            analysis.full_diameter5_pair = c1 != null && c2 != null &&
                analysis.matched_edge_count == 2 && analysis.side_orientation_exact;
            return analysis;
        }

        private static bool GeometryGate(GeometrySnapshot value, string side)
        {
            return BasicPartGate(value) && value.side == side &&
                RowsEqual(value.slot_rows_mm, ExpectedSlots, RowToleranceMm) &&
                RowsEqual(value.circle_rows_mm, ExpectedCircles, RowToleranceMm) &&
                value.slot_fragment_count == 0 && value.circle_fragment_count == 0 &&
                value.slot_analyses.Count(row => row.full_fingerprint) == 7 &&
                value.circle_analyses.Count(row => row.full_diameter5_pair) == 7 &&
                value.slot_analyses.Where(row => row.full_fingerprint).All(row => row.side_orientation_exact) &&
                value.circle_analyses.Where(row => row.full_diameter5_pair).All(row => row.side_orientation_exact) &&
                value.unique_slot_profile_signature_count == 1 &&
                RowsEqual(value.slot_rows_mm.Select(row => row - 60.0).ToArray(),
                    value.circle_rows_mm, RowToleranceMm);
        }

        private static bool BasicPartGate(GeometrySnapshot value)
        {
            return value != null && value.body_count == 1 && value.feature_count > 0 &&
                value.error_feature_count == 0 && value.warning_feature_count == 0 &&
                value.has_sheet_metal && value.has_flat_pattern && value.edge_count == value.edges.Count &&
                value.edge_count > 0 && value.traversal_complete &&
                value.forbidden_import_feature_count == 0;
        }

        private static bool MirrorGeometryGate(GeometrySnapshot value, string side)
        {
            return GeometryGate(value, side) && value.mirror_feature_count >= 1 &&
                value.forbidden_import_feature_count == 0;
        }

        private static bool SameGeometry(GeometrySnapshot left, GeometrySnapshot right)
        {
            return left != null && right != null && left.body_count == right.body_count &&
                left.edge_count == right.edge_count && RowsEqual(left.slot_rows_mm, right.slot_rows_mm, RowToleranceMm) &&
                RowsEqual(left.circle_rows_mm, right.circle_rows_mm, RowToleranceMm) &&
                left.slot_fragment_count == right.slot_fragment_count &&
                left.circle_fragment_count == right.circle_fragment_count;
        }

        private static bool EdgeMatches(EdgeRow edge, SlotTemplate template, double row)
        {
            if (!Close(edge.length_mm, template.length, LengthToleranceMm) ||
                edge.vertex_start_mm.Length < 3 || edge.vertex_end_mm.Length < 3) return false;
            double[] start = NormalizeVertex(edge.vertex_start_mm, row);
            double[] end = NormalizeVertex(edge.vertex_end_mm, row);
            return (PointMatches(start, template.a) && PointMatches(end, template.b)) ||
                (PointMatches(start, template.b) && PointMatches(end, template.a));
        }

        private static IEnumerable<double> DerivedRows(EdgeRow edge, SlotTemplate template)
        {
            var output = new List<double>();
            if (!Close(edge.length_mm, template.length, LengthToleranceMm) ||
                edge.vertex_start_mm.Length < 3 || edge.vertex_end_mm.Length < 3) return output;
            double[] start = NormalizeVertex(edge.vertex_start_mm, 0);
            double[] end = NormalizeVertex(edge.vertex_end_mm, 0);
            foreach (var pair in new[] { new[] { template.a, template.b }, new[] { template.b, template.a } })
            {
                if (!Close(start[0], pair[0][0], CoordinateToleranceMm) ||
                    !Close(start[2], pair[0][2], CoordinateToleranceMm) ||
                    !Close(end[0], pair[1][0], CoordinateToleranceMm) ||
                    !Close(end[2], pair[1][2], CoordinateToleranceMm)) continue;
                double r1 = start[1] - pair[0][1];
                double r2 = end[1] - pair[1][1];
                if (Close(r1, r2, CoordinateToleranceMm)) output.Add((r1 + r2) / 2.0);
            }
            return output;
        }

        private static bool CircleMatches(EdgeRow edge, double row, double absX, double z)
        {
            return Close(edge.length_mm, CircleLengthMm, LengthToleranceMm) &&
                edge.param_start_mm.Length >= 3 &&
                Close(Math.Abs(edge.param_start_mm[0]), absX, CoordinateToleranceMm) &&
                Close(edge.param_start_mm[1], row, CoordinateToleranceMm) &&
                Close(edge.param_start_mm[2], z, CoordinateToleranceMm);
        }

        private static string NormalizedEdgeSignature(string id, EdgeRow edge, double row)
        {
            string start = PointSignature(NormalizeVertex(edge.vertex_start_mm, row));
            string end = PointSignature(NormalizeVertex(edge.vertex_end_mm, row));
            if (string.CompareOrdinal(start, end) > 0) { string temp = start; start = end; end = temp; }
            return id + "|" + Round(edge.length_mm, 6).ToString("0.######", CultureInfo.InvariantCulture) +
                "|" + start + "|" + end;
        }

        private static string PointSignature(double[] point)
        {
            return string.Join(",", point.Select(value => Round(value, 6)
                .ToString("0.######", CultureInfo.InvariantCulture)));
        }

        private static double[] NormalizeVertex(double[] point, double row)
        {
            return new[] { Math.Abs(point[0]), point[1] - row, point[2] };
        }

        private static bool PointMatches(double[] actual, double[] expected)
        {
            return actual.Length >= 3 && expected.Length >= 3 &&
                Close(actual[0], expected[0], CoordinateToleranceMm) &&
                Close(actual[1], expected[1], CoordinateToleranceMm) &&
                Close(actual[2], expected[2], CoordinateToleranceMm);
        }

        private static List<double> DistinctRows(IEnumerable<double> values)
        {
            var output = new List<double>();
            foreach (double value in values.Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .OrderBy(value => value))
                if (output.Count == 0 || !Close(output[output.Count - 1], value, RowToleranceMm))
                    output.Add(Round(value, 9));
            return output;
        }

        private static ISldWorks StartOwned(Result result, SessionRecord session)
        {
            ISldWorks sw = null;
            try
            {
                Type type = Type.GetTypeFromProgID("SldWorks.Application.28", true);
                sw = Activator.CreateInstance(type) as ISldWorks;
            }
            catch { }
            if (sw == null) return null;
            TryAction(delegate { sw.Visible = false; });
            TryAction(delegate { sw.UserControl = false; });
            TryAction(delegate { sw.CommandInProgress = true; });
            session.solidworks_revision = Safe(delegate { return sw.RevisionNumber(); }, "");
            session.sldworks_process_id = Safe(delegate { return sw.GetProcessID(); }, 0);
            session.sldworks_start_ticks_utc = ProcessStartTicks(session.sldworks_process_id, "SLDWORKS");
            session.solidworks_executable_path = ProcessExecutablePath(session.sldworks_process_id);
            session.solidworks_executable_sha256 = File.Exists(session.solidworks_executable_path)
                ? Sha256(session.solidworks_executable_path) : "";
            session.solidworks_2020_exact_gate = session.solidworks_revision.StartsWith("28.",
                    StringComparison.Ordinal) &&
                SamePath(session.solidworks_executable_path, ExpectedSolidWorksExePath) &&
                string.Equals(session.solidworks_executable_sha256, ExpectedSolidWorksExeSha256,
                    StringComparison.OrdinalIgnoreCase);
            session.created = session.sldworks_process_id > 0 && session.sldworks_start_ticks_utc > 0 &&
                !result.baseline_sldworks_process_ids.Contains(session.sldworks_process_id);
            Thread.Sleep(750);
            CaptureMonitors(result, session);
            if (!session.created || !session.solidworks_2020_exact_gate)
            {
                TryAction(delegate { sw.ExitApp(); });
                Release(sw);
                return null;
            }
            return sw;
        }

        private static string ProcessExecutablePath(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    return Path.GetFullPath(process.MainModule.FileName);
            }
            catch { return ""; }
        }

        private static ModelDoc2 OpenPart(ISldWorks sw, string path, bool readOnly,
            ref int errors, ref int warnings)
        {
            int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
            if (readOnly) options |= (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;
            try
            {
                return sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, options, "",
                    ref errors, ref warnings) as ModelDoc2;
            }
            catch { return null; }
        }

        private static void CloseDocument(ISldWorks sw, ref ModelDoc2 model)
        {
            if (model == null) return;
            ModelDoc2 current = model;
            string title = Safe(delegate { return current.GetTitle(); }, "");
            Release(current);
            model = null;
            if (sw != null && !string.IsNullOrWhiteSpace(title))
                TryAction(delegate { sw.CloseDoc(title); });
        }

        private static void CloseOwned(ref ISldWorks sw, Result result, SessionRecord session)
        {
            if (session == null) return;
            CaptureMonitors(result, session);
            if (sw != null)
            {
                ISldWorks current = sw;
                TryAction(delegate { current.CloseAllDocuments(true); });
                TryAction(delegate { current.CommandInProgress = false; });
                session.exit_requested = TryAction(delegate { current.ExitApp(); });
                Release(current);
                sw = null;
            }
            session.process_exited = WaitForOwnedSolidWorksExit(session, 30000);
            if (!session.process_exited) ForceStopOwnedSolidWorks(result, session);
            CaptureMonitors(result, session);
            if (!WaitForOwnedMonitorsExit(session, 10000)) ForceStopOwnedMonitors(result, session);
            session.monitors_exited = WaitForOwnedMonitorsExit(session, 5000);
            if (!session.monitors_exited) result.cleanup_errors.Add("owned sldProcMon remained after cleanup");
        }

        private static void CleanupOwned(Result result)
        {
            foreach (SessionRecord session in result.sessions)
            {
                if (!session.process_exited) ForceStopOwnedSolidWorks(result, session);
                if (!session.monitors_exited) ForceStopOwnedMonitors(result, session);
                session.monitors_exited = WaitForOwnedMonitorsExit(session, 1000);
            }
        }

        private static void CaptureMonitors(Result result, SessionRecord session)
        {
            if (session == null || session.sldworks_process_id <= 0) return;
            string exactParent = ExactParentPattern(session.sldworks_process_id);
            foreach (ProcessIdentity identity in ProcessInfoByName("sldProcMon"))
            {
                if (!Regex.IsMatch(identity.command_line ?? "", exactParent, RegexOptions.CultureInvariant)) continue;
                identity.parent_sldworks_process_id = session.sldworks_process_id;
                identity.ownership_verified = identity.pid > 0 && identity.start_ticks_utc > 0 &&
                    string.Equals(identity.name, "sldProcMon", StringComparison.OrdinalIgnoreCase);
                if (!identity.ownership_verified) session.sldprocmon_identity_gate = false;
                else if (!session.sldprocmon_processes.Any(row => row.pid == identity.pid &&
                    row.start_ticks_utc == identity.start_ticks_utc))
                    session.sldprocmon_processes.Add(identity);
            }
        }

        private static void ForceStopOwnedSolidWorks(Result result, SessionRecord session)
        {
            if (session == null || !session.created) return;
            if (!ExactProcessAlive(session.sldworks_process_id, "SLDWORKS", session.sldworks_start_ticks_utc))
            {
                session.process_exited = true;
                return;
            }
            try
            {
                using (Process process = Process.GetProcessById(session.sldworks_process_id))
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
            }
            catch (Exception ex) { result.cleanup_errors.Add(SafeException(ex)); }
            session.process_exited = !ExactProcessAlive(session.sldworks_process_id,
                "SLDWORKS", session.sldworks_start_ticks_utc);
        }

        private static void ForceStopOwnedMonitors(Result result, SessionRecord session)
        {
            if (session == null || !session.process_exited) return;
            foreach (ProcessIdentity identity in session.sldprocmon_processes)
            {
                ProcessIdentity current = ExactOwnedMonitor(identity, session.sldworks_process_id);
                if (current == null) continue;
                try
                {
                    using (Process process = Process.GetProcessById(identity.pid))
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch (ArgumentException) { }
                catch (Exception ex) { result.cleanup_errors.Add(SafeException(ex)); }
            }
        }

        private static bool WaitForOwnedSolidWorksExit(SessionRecord session, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            int clear = 0;
            while (DateTime.UtcNow < deadline)
            {
                if (!ExactProcessAlive(session.sldworks_process_id, "SLDWORKS", session.sldworks_start_ticks_utc))
                {
                    if (++clear >= 3) return true;
                }
                else clear = 0;
                Thread.Sleep(250);
            }
            return false;
        }

        private static bool WaitForOwnedMonitorsExit(SessionRecord session, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            int clear = 0;
            while (DateTime.UtcNow < deadline)
            {
                bool alive = session.sldprocmon_processes.Any(identity =>
                    ExactOwnedMonitor(identity, session.sldworks_process_id) != null);
                if (!alive) { if (++clear >= 3) return true; }
                else clear = 0;
                Thread.Sleep(250);
            }
            return false;
        }

        private static bool IsExclusive(SessionRecord session)
        {
            List<int> ids = ProcessIds("SLDWORKS");
            return session != null && session.created && ids.Count == 1 &&
                ids[0] == session.sldworks_process_id &&
                session.solidworks_2020_exact_gate &&
                ExactProcessAlive(session.sldworks_process_id, "SLDWORKS", session.sldworks_start_ticks_utc);
        }

        private static ProcessIdentity ExactOwnedMonitor(ProcessIdentity identity, int parentPid)
        {
            if (identity == null || !identity.ownership_verified ||
                identity.parent_sldworks_process_id != parentPid) return null;
            string pattern = ExactParentPattern(parentPid);
            return ProcessInfoByName("sldProcMon").FirstOrDefault(current =>
                current.pid == identity.pid && current.start_ticks_utc == identity.start_ticks_utc &&
                Regex.IsMatch(current.command_line ?? "", pattern, RegexOptions.CultureInvariant));
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
                    "SELECT ProcessId,Name,CommandLine FROM Win32_Process WHERE Name='" +
                    name.Replace("'", "''") + ".exe'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        int pid = Convert.ToInt32(row["ProcessId"], CultureInfo.InvariantCulture);
                        output.Add(new ProcessIdentity
                        {
                            pid = pid,
                            name = Path.GetFileNameWithoutExtension(Convert.ToString(row["Name"],
                                CultureInfo.InvariantCulture) ?? ""),
                            command_line = Convert.ToString(row["CommandLine"], CultureInfo.InvariantCulture) ?? "",
                            start_ticks_utc = ProcessStartTicks(pid, name)
                        });
                    }
                }
            }
            catch { }
            return output;
        }

        private static long ProcessStartTicks(int pid, string expectedName)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    return !process.HasExited && string.Equals(process.ProcessName, expectedName,
                        StringComparison.OrdinalIgnoreCase) ? process.StartTime.ToUniversalTime().Ticks : 0;
            }
            catch { return 0; }
        }

        private static bool ExactProcessAlive(int pid, string name, long startTicks)
        {
            return pid > 0 && startTicks > 0 && ProcessStartTicks(pid, name) == startTicks;
        }

        private static List<int> ProcessIds(string name)
        {
            return Process.GetProcessesByName(name).Select(process =>
            {
                try { return process.Id; }
                finally { process.Dispose(); }
            }).OrderBy(value => value).ToList();
        }

        private static List<int> RelatedProcessIds()
        {
            var ids = ProcessIds("SLDWORKS");
            ids.AddRange(ProcessIds("sldProcMon"));
            ids.Sort();
            return ids;
        }

        private static bool IsAllowedAttemptRoot(string path, string attemptsRoot)
        {
            if (!IsUnder(path, attemptsRoot)) return false;
            string relative = Path.GetFullPath(path).Substring(
                Path.GetFullPath(attemptsRoot).TrimEnd('\\').Length).TrimStart('\\');
            string[] parts = relative.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && Regex.IsMatch(parts[0], "^[A-Za-z0-9_.-]{1,160}$") &&
                Regex.IsMatch(parts[1], "^attempt-[0-9]{4}$");
        }

        private static void ValidateTrustedPlanSecurityContracts(Dictionary<string, object> plan,
            Result result)
        {
            Dictionary<string, object> manifests = ChildObject(plan, "trustedToolchainManifests");
            Require(ExactKeys(manifests, TrustedToolchainManifestKeys) &&
                TrustedToolchainManifestKeys.All(delegate(string key)
                {
                    return IsSha256(TextValue(manifests, key));
                }), "IMMUTABLE_PLAN_TOOLCHAIN_MANIFEST_MAP_MISMATCH", 3,
                "trustedToolchainManifests must contain the exact six frozen tool manifest hashes");

            Dictionary<string, object> contracts = ChildObject(plan, "stageReceiptContracts");
            Require(StageReceiptContractsMatch(contracts),
                "IMMUTABLE_PLAN_STAGE_RECEIPT_CONTRACT_MISMATCH", 3,
                "stageReceiptContracts must exactly match the shared nine-stage contract");
            result.stage_receipt_contract_gate = true;

            string executablePath = Path.GetFullPath(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            string toolsDirectory = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(executablePath), ".."));
            string manifestPath = Path.Combine(toolsDirectory, "lock_toolchain_manifest.json");
            string expectedManifestSha = NormalizeHash(TextValue(manifests, ExpectedToolId));
            Require(File.Exists(manifestPath) && NoReparsePoints(toolsDirectory, manifestPath) &&
                string.Equals(Sha256(manifestPath), expectedManifestSha,
                    StringComparison.OrdinalIgnoreCase),
                "FROZEN_LOCK_TOOLCHAIN_MANIFEST_HASH_MISMATCH", 3,
                "the live lock toolchain manifest must match the SHA frozen in the immutable plan");
            Dictionary<string, object> manifest = ReadJsonObject(manifestPath);
            Dictionary<string, object> tools = ChildObject(manifest, "tools");
            Dictionary<string, object> validator = ChildObject(manifest, "validator");
            Require(ExactKeys(manifest, "schema", "generatedBy", "tools", "validator") &&
                string.Equals(TextValue(manifest, "schema"), ExpectedToolchainManifestSchema,
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(manifest, "generatedBy"),
                    "VerifyLockTopology888x14Static.mjs", StringComparison.Ordinal) &&
                ExactKeys(tools, ExpectedToolId, "native_lock_topology_inspector_v1",
                    "native_assembly_tongue_inspector_888x14_v1") &&
                ExactKeys(validator, "path", "sha256"),
                "LOCK_TOOLCHAIN_MANIFEST_SCHEMA_MISMATCH", 3,
                "the frozen lock manifest must have the exact reviewed three-tool and validator schema");

            Require(ToolManifestEntryMatches(ChildObject(tools, ExpectedToolId),
                    Path.Combine(toolsDirectory, "BuildLockTopology888x14.cs"), executablePath,
                    ExpectedSourceSha256) &&
                ToolManifestEntryMatches(ChildObject(tools, "native_lock_topology_inspector_v1"),
                    Path.Combine(toolsDirectory, "InspectLockTopology888x14.cs"),
                    Path.Combine(toolsDirectory, "bin", "InspectLockTopology888x14.exe"), "") &&
                ToolManifestEntryMatches(ChildObject(tools,
                    "native_assembly_tongue_inspector_888x14_v1"),
                    Path.Combine(toolsDirectory, "InspectAssemblyTongues888x14.cs"),
                    Path.Combine(toolsDirectory, "bin", "InspectAssemblyTongues888x14.exe"), ""),
                "LOCK_TOOLCHAIN_MANIFEST_TOOL_IDENTITY_MISMATCH", 3,
                "the frozen lock manifest does not match all three live reviewed tool identities");
            string validatorPath = Path.Combine(toolsDirectory, "ValidateLockTopology888x14.mjs");
            Require(SamePath(TextValue(validator, "path"), validatorPath) &&
                IsSha256(TextValue(validator, "sha256")) && File.Exists(validatorPath) &&
                string.Equals(Sha256(validatorPath), TextValue(validator, "sha256"),
                    StringComparison.OrdinalIgnoreCase),
                "LOCK_TOOLCHAIN_MANIFEST_VALIDATOR_IDENTITY_MISMATCH", 3,
                "the frozen lock manifest does not match the live validator");
            result.toolchain_manifest_path = manifestPath;
            result.toolchain_manifest_sha256 = expectedManifestSha;
            result.trusted_toolchain_manifest_gate = true;
        }

        private static bool ToolManifestEntryMatches(Dictionary<string, object> entry,
            string expectedSourcePath, string expectedExecutablePath, string expectedSourceSha)
        {
            if (!ExactKeys(entry, "sourcePath", "sourceNormalizedSha256", "executablePath",
                    "executableSha256") || !SamePath(TextValue(entry, "sourcePath"),
                    expectedSourcePath) || !SamePath(TextValue(entry, "executablePath"),
                    expectedExecutablePath) || !File.Exists(expectedSourcePath) ||
                !File.Exists(expectedExecutablePath) ||
                !IsSha256(TextValue(entry, "sourceNormalizedSha256")) ||
                !IsSha256(TextValue(entry, "executableSha256"))) return false;
            string liveSourceSha = NormalizedToolSourceSha256(expectedSourcePath);
            return string.Equals(liveSourceSha, TextValue(entry, "sourceNormalizedSha256"),
                       StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrEmpty(expectedSourceSha) ||
                    string.Equals(liveSourceSha, expectedSourceSha,
                        StringComparison.OrdinalIgnoreCase)) &&
                   string.Equals(Sha256(expectedExecutablePath),
                       TextValue(entry, "executableSha256"), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizedToolSourceSha256(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            string normalized = Regex.Replace(source,
                "private const string ExpectedSourceSha256\\s*=\\s*\"(?:__SOURCE_SHA256__|[0-9A-F]{64})\";",
                delegate(Match match)
                {
                    return Regex.Replace(match.Value, "(?:__SOURCE_SHA256__|[0-9A-F]{64})",
                        "__SOURCE_SHA256__");
                }, RegexOptions.CultureInvariant);
            return Sha256Text(normalized);
        }

        private static string NormalizedStandardToolSourceSha256(string path)
        {
            return Sha256Text(NormalizeStandardToolSource(File.ReadAllText(path, Encoding.UTF8)));
        }

        private static string NormalizeStandardToolSource(string source)
        {
            string normalized = (source ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = Regex.Replace(normalized,
                "(private const string ExpectedSourceSha256\\s*=\\s*\")" +
                "(?:__SOURCE_SHA256__|[0-9A-F]{64})(\";)",
                delegate(Match match)
                {
                    return match.Groups[1].Value + "__SOURCE_SHA256__" + match.Groups[2].Value;
                }, RegexOptions.CultureInvariant);
            return Regex.Replace(normalized,
                "(private const string ExpectedContractSnapshotSha256\\s*=\\s*\")" +
                "(?:__CONTRACT_SHA256__|[0-9A-F]{64})(\";)",
                delegate(Match match)
                {
                    return match.Groups[1].Value + "__CONTRACT_SHA256__" + match.Groups[2].Value;
                }, RegexOptions.CultureInvariant);
        }

        private static bool StageReceiptContractsMatch(Dictionary<string, object> contracts)
        {
            if (!ExactKeys(contracts, StageReceiptOrder)) return false;
            for (int index = 0; index < StageReceiptOrder.Length; index++)
            {
                Dictionary<string, object> row = ChildObject(contracts, StageReceiptOrder[index]);
                if (!ExactKeys(row, "schema", "path", "producerToolId", "predecessor") ||
                    !string.Equals(TextValue(row, "schema"), StageReceiptSchemas[index],
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "path"), StageReceiptPaths[index],
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "producerToolId"), StageReceiptTools[index],
                        StringComparison.Ordinal) ||
                    !string.Equals(TextValue(row, "predecessor"), StageReceiptPredecessors[index],
                        StringComparison.Ordinal) ||
                    !Regex.IsMatch(TextValue(row, "path"),
                        "^(receipts|evidence)/[A-Za-z0-9_.-]+\\.json$",
                        RegexOptions.CultureInvariant) ||
                    TextValue(row, "path").IndexOf('\\') >= 0 ||
                    TextValue(row, "path").Contains("..")) return false;
            }
            return true;
        }

        private static void ValidateLivePredecessorReceiptChain(string attemptRoot,
            string attemptsRoot, Result result)
        {
            Dictionary<string, object> plan = ReadJsonObject(result.plan_path);
            string predecessorReceiptSha = "";
            string predecessorInventoryDigest = "";
            for (int index = 0; index < 6; index++)
            {
                string phase = StageReceiptOrder[index];
                string relativeReceiptPath = StageReceiptPaths[index];
                string receiptPath = Path.Combine(attemptRoot,
                    relativeReceiptPath.Replace('/', Path.DirectorySeparatorChar));
                Require(SamePath(receiptPath, Path.Combine(attemptRoot,
                        relativeReceiptPath.Replace('/', Path.DirectorySeparatorChar))) &&
                    File.Exists(receiptPath) && NoReparsePoints(attemptsRoot, receiptPath) &&
                    FileLinkCount(receiptPath) == 1,
                    "PREDECESSOR_RECEIPT_FILE_INVALID", 3,
                    "missing, linked, or path-drifted predecessor receipt: " + phase);
                string receiptShaBefore = Sha256(receiptPath);
                Dictionary<string, object> receipt = ReadJsonObject(receiptPath);
                bool seed = index == 0;
                Require(ExactKeys(receipt, seed ? SeedReceiptKeys : RuntimeReceiptKeys) &&
                    string.Equals(TextValue(receipt, "schema"), StageReceiptSchemas[index],
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "phase"), phase, StringComparison.Ordinal) &&
                    BoolValue(receipt, "success") &&
                    string.Equals(TextValue(receipt, "predecessorReceiptSha256"),
                        predecessorReceiptSha, StringComparison.OrdinalIgnoreCase),
                    "PREDECESSOR_RECEIPT_SCHEMA_OR_CHAIN_MISMATCH", 3,
                    "predecessor receipt exact schema or hash chain is invalid: " + phase);

                string inputDigest = TextValue(receipt,
                    seed ? "sourceInventoryDigest" : "preInventoryDigest");
                string outputDigest = TextValue(receipt,
                    seed ? "targetInventoryDigest" : "postInventoryDigest");
                Require(IsSha256(inputDigest) && IsSha256(outputDigest) &&
                    (index == 0 || string.Equals(inputDigest, predecessorInventoryDigest,
                        StringComparison.OrdinalIgnoreCase)),
                    "PREDECESSOR_RECEIPT_INVENTORY_CHAIN_MISMATCH", 3,
                    "pre/post inventory digest chain is invalid: " + phase);
                if (seed)
                {
                    Require(string.Equals(inputDigest, ExpectedSourceSeedInventoryDigest,
                            StringComparison.OrdinalIgnoreCase) &&
                        NumberValue(receipt, "nonRootFileCount") == 74 &&
                        BoolValue(receipt, "nonRootExactSource") &&
                        IsSha256(TextValue(receipt, "rootShaBefore")) &&
                        IsSha256(TextValue(receipt, "rootShaAfterStable")) &&
                        NumberValue(receipt, "initialOpenErrors") == 0 &&
                        NumberValue(receipt, "initialOpenWarnings") == 0 &&
                        NumberValue(receipt, "stabilizeSaveErrors") == 0 &&
                        NumberValue(receipt, "stabilizeSaveWarnings") == 0 &&
                        NumberValue(receipt, "reopenErrors") == 0 &&
                        NumberValue(receipt, "reopenWarnings") == 0 &&
                        NumberValue(receipt, "dependencyClosureCount") == 75 &&
                        BoolValue(receipt, "dependenciesAllTargetLocal") &&
                        BoolValue(receipt, "knownRootIssueGate") &&
                        !string.IsNullOrWhiteSpace(TextValue(receipt, "rootFileName")),
                        "SEED_RECEIPT_SEMANTIC_CONTRACT_MISMATCH", 3,
                        "seed receipt does not prove the exact 75-file stabilized native seed pack");
                }

                ValidateHistoricalReceiptAuthorization(receipt, phase, StageReceiptTools[index],
                    inputDigest, plan, result);
                ValidateProducerToolchainIdentity(plan, StageReceiptTools[index],
                    TextValue(receipt, "toolSourceNormalizedSha256"),
                    TextValue(receipt, "toolExecutableSha256"));
                string evidenceRelativePath = TextValue(receipt, "evidencePath");
                Require(ReceiptEvidencePathMatches(phase, evidenceRelativePath),
                    "PREDECESSOR_RECEIPT_EVIDENCE_PATH_MISMATCH", 3,
                    "receipt evidencePath is not the fixed stage result: " + phase);
                string evidencePath = Path.Combine(attemptRoot,
                    evidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Require(IsUnder(evidencePath, attemptRoot) && File.Exists(evidencePath) &&
                    NoReparsePoints(attemptsRoot, evidencePath) && FileLinkCount(evidencePath) == 1,
                    "PREDECESSOR_EVIDENCE_FILE_INVALID", 3,
                    "receipt evidence is missing, linked, or outside the attempt: " + phase);
                string evidenceShaBefore = Sha256(evidencePath);
                Dictionary<string, object> evidence = ReadJsonObject(evidencePath);
                Require(string.Equals(evidenceShaBefore, TextValue(receipt, "evidenceSha256"),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(EvidenceCommitmentDigest(evidence),
                        TextValue(receipt, "evidenceCommitmentSha256"),
                        StringComparison.OrdinalIgnoreCase) && BoolValue(evidence, "success") &&
                    !evidence.ContainsKey("phase_receipt_path") &&
                    !evidence.ContainsKey("phase_receipt_sha256") &&
                    !evidence.ContainsKey("phase_receipt_committed") &&
                    string.Equals(evidenceShaBefore, Sha256(evidencePath),
                        StringComparison.OrdinalIgnoreCase),
                    "PREDECESSOR_EVIDENCE_COMMITMENT_MISMATCH", 3,
                    "receipt does not bind the final live success evidence bytes and semantic projection: " + phase);
                Require(string.Equals(receiptShaBefore, Sha256(receiptPath),
                        StringComparison.OrdinalIgnoreCase),
                    "PREDECESSOR_RECEIPT_CHANGED_DURING_VALIDATION", 3,
                    "predecessor receipt changed while it was being validated: " + phase);

                result.predecessor_receipts.Add(new StageReceiptEvidence
                {
                    phase = phase,
                    path = receiptPath,
                    sha256 = receiptShaBefore,
                    evidence_path = evidencePath,
                    evidence_sha256 = evidenceShaBefore,
                    evidence_commitment_sha256 = TextValue(receipt,
                        "evidenceCommitmentSha256"),
                    input_inventory_digest = inputDigest,
                    output_inventory_digest = outputDigest,
                    predecessor_receipt_sha256 = predecessorReceiptSha,
                    authorization_id = TextValue(receipt, "authorizationId"),
                    authorization_sha256 = TextValue(receipt, "authorizationSha256"),
                    tool_id = StageReceiptTools[index],
                    validated = true
                });
                predecessorReceiptSha = receiptShaBefore;
                predecessorInventoryDigest = outputDigest;
            }
            Require(string.Equals(predecessorInventoryDigest, result.initial_inventory_digest,
                    StringComparison.OrdinalIgnoreCase),
                "DOOR_RECEIPT_POST_INVENTORY_NOT_CURRENT_LOCK_INPUT", 3,
                "the door-stage post inventory must equal the live lock-stage pre inventory");
            result.predecessor_receipt_sha256 = predecessorReceiptSha;
            result.predecessor_receipt_chain_gate = true;
        }

        private static bool ReceiptEvidencePathMatches(string phase, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\\') >= 0 ||
                value.Contains("..") || !Regex.IsMatch(value,
                    "^evidence/[A-Za-z0-9_.-]+\\.json$", RegexOptions.CultureInvariant))
                return false;
            if (phase == "clone_native_seed")
                return value == "evidence/seed_pack_888_native_v1.json";
            if (phase == "dimensions" || phase == "derived" || phase == "base-hole" ||
                phase == "assemblies") return value == "evidence/width-" + phase + ".json";
            return phase == "door_module_888x14" &&
                value == "evidence/door_module_888x14.result.v1.json";
        }

        private static void ValidateHistoricalReceiptAuthorization(
            Dictionary<string, object> receipt, string phase, string producerToolId,
            string inputInventoryDigest, Dictionary<string, object> currentPlan, Result result)
        {
            byte[] bytes;
            Dictionary<string, object> authorization;
            try
            {
                bytes = Convert.FromBase64String(TextValue(receipt, "authorizationJsonBase64"));
                authorization = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                    RecursionLimit = 256 }.DeserializeObject(
                        new UTF8Encoding(false).GetString(bytes)) as Dictionary<string, object>;
            }
            catch (Exception error)
            {
                throw new GateException("PREDECESSOR_AUTHORIZATION_SNAPSHOT_INVALID", 3,
                    "historical authorization snapshot cannot be decoded: " + SafeException(error));
            }
            Require(authorization != null &&
                ExactKeys(authorization, "schema", "authorizationId", "issuedAt", "expiresAt",
                    "purpose", "workerId", "task", "request", "plan", "recipe", "tool",
                    "seed", "execution", "qualityBoundary"),
                "PREDECESSOR_AUTHORIZATION_TOP_LEVEL_SCHEMA_MISMATCH", 3,
                "historical authorization must exactly match the shared authorization schema");
            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> request = ChildObject(authorization, "request");
            Dictionary<string, object> plan = ChildObject(authorization, "plan");
            Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> seed = ChildObject(authorization, "seed");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");
            DateTime issued = DateTime.MinValue, expires = DateTime.MinValue;
            DateTime leaseExpires = DateTime.MinValue, completed = DateTime.MinValue;
            bool times = ParseUtc(TextValue(authorization, "issuedAt"), out issued) &&
                ParseUtc(TextValue(authorization, "expiresAt"), out expires) &&
                ParseUtc(TextValue(task, "leaseExpiresAt"), out leaseExpires) &&
                ParseUtc(TextValue(receipt, "completedAt"), out completed);
            string[] expectedPhases = producerToolId == "native_width_888_v1"
                ? new[] { "dimensions", "derived", "base-hole", "assemblies" }
                : new[] { phase };
            Require(ExactKeys(task, "id", "revision", "digest", "leaseId", "leaseExpiresAt") &&
                ExactKeys(request, "fingerprint", "digest") && ExactKeys(plan, "sha256") &&
                ExactKeys(recipe, "id", "version", "digest") &&
                ExactKeys(tool, "id", "sourceNormalizedSha256", "executableSha256") &&
                ExactKeys(seed, "inventoryDigest") &&
                ExactKeys(execution, "authorized", "attempt", "phases") &&
                ExactKeys(quality, "engineeringAssistanceReady",
                    "readyOnlyAfterEveryRequiredCheckPasses") &&
                string.Equals(TextValue(authorization, "schema"), ExpectedAuthorizationSchema,
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(authorization, "purpose"),
                    "structure_engineering_assistance", StringComparison.Ordinal) &&
                string.Equals(TextValue(authorization, "workerId"), result.worker_id,
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(task, "id"), result.task_id, StringComparison.Ordinal) &&
                NumberValue(task, "revision") == result.task_revision &&
                string.Equals(TextValue(task, "digest"), result.task_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(request, "fingerprint"), result.request_fingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(request, "digest"), result.request_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(plan, "sha256"), TextValue(receipt, "planSha256"),
                    StringComparison.OrdinalIgnoreCase) && IsSha256(TextValue(plan, "sha256")) &&
                string.Equals(TextValue(recipe, "id"), TrustedRecipeId, StringComparison.Ordinal) &&
                NumberValue(recipe, "version") == 1 &&
                string.Equals(TextValue(recipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(tool, "id"), producerToolId, StringComparison.Ordinal) &&
                string.Equals(TextValue(tool, "sourceNormalizedSha256"),
                    TextValue(receipt, "toolSourceNormalizedSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(tool, "executableSha256"),
                    TextValue(receipt, "toolExecutableSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(seed, "inventoryDigest"), inputInventoryDigest,
                    StringComparison.OrdinalIgnoreCase) && BoolValue(execution, "authorized") &&
                NumberValue(execution, "attempt") == result.attempt_number &&
                StringArrayExactly(ArrayValue(execution, "phases"), expectedPhases) &&
                !BoolValue(quality, "engineeringAssistanceReady") &&
                BoolValue(quality, "readyOnlyAfterEveryRequiredCheckPasses") &&
                times && issued <= completed && completed <= expires && expires <= leaseExpires &&
                expires > issued && expires - issued <= TimeSpan.FromMinutes(30) &&
                string.Equals(TextValue(receipt, "authorizationId"),
                    TextValue(authorization, "authorizationId"), StringComparison.Ordinal) &&
                string.Equals(TextValue(receipt, "authorizationSha256"), Sha256Bytes(bytes),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "authorizationIssuedAt"),
                    TextValue(authorization, "issuedAt"), StringComparison.Ordinal) &&
                string.Equals(TextValue(receipt, "authorizationExpiresAt"),
                    TextValue(authorization, "expiresAt"), StringComparison.Ordinal) &&
                string.Equals(TextValue(receipt, "leaseId"), TextValue(task, "leaseId"),
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(receipt, "leaseExpiresAt"),
                    TextValue(task, "leaseExpiresAt"), StringComparison.Ordinal) &&
                string.Equals(TextValue(receipt, "taskId"), result.task_id,
                    StringComparison.Ordinal) &&
                NumberValue(receipt, "taskRevision") == result.task_revision &&
                string.Equals(TextValue(receipt, "taskDigest"), result.task_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "requestDigest"), result.request_digest,
                    StringComparison.OrdinalIgnoreCase) &&
                NumberValue(receipt, "attempt") == result.attempt_number &&
                string.Equals(TextValue(receipt, "recipeId"), TrustedRecipeId,
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(receipt, "recipeDigest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(receipt, "toolId"), producerToolId,
                    StringComparison.Ordinal) && AuthorizationIdentityMatches(authorization),
                "PREDECESSOR_AUTHORIZATION_BINDING_MISMATCH", 3,
                "historical authorization does not bind receipt task/request/recipe/tool/input/attempt/time: " + phase);
        }

        private static void ValidateProducerToolchainIdentity(Dictionary<string, object> plan,
            string toolId, string sourceSha, string executableSha)
        {
            string manifestRelativePath = ProducerManifestRelativePath(toolId);
            string repositoryRoot = Directory.GetParent(
                Path.GetDirectoryName(TrustedRecipeRegistryPath)).FullName;
            string manifestPath = Path.GetFullPath(Path.Combine(repositoryRoot,
                manifestRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string expectedManifestSha = TextValue(ChildObject(plan,
                "trustedToolchainManifests"), toolId);
            Require(IsUnder(manifestPath, repositoryRoot) && File.Exists(manifestPath) &&
                NoReparsePoints(repositoryRoot, manifestPath) && FileLinkCount(manifestPath) == 1 &&
                string.Equals(Sha256(manifestPath), expectedManifestSha,
                    StringComparison.OrdinalIgnoreCase),
                "PREDECESSOR_TOOLCHAIN_MANIFEST_HASH_MISMATCH", 3,
                "producer manifest is missing or differs from the immutable plan: " + toolId);
            Dictionary<string, object> manifest = ReadJsonObject(manifestPath);
            Dictionary<string, object> tool = ChildObject(manifest, "tool");
            Require(ExactKeys(manifest, "schema", "generatedBy", "tool") &&
                string.Equals(TextValue(manifest, "schema"),
                    "winnsen.16029.native_toolchain_manifest.v1", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(TextValue(manifest, "generatedBy")) &&
                ExactKeys(tool, "id", "sourcePath", "sourceNormalizedSha256", "executablePath",
                    "executableSha256", "verifierPath", "verifierSha256") &&
                string.Equals(TextValue(tool, "id"), toolId, StringComparison.Ordinal) &&
                string.Equals(TextValue(tool, "sourceNormalizedSha256"), sourceSha,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(tool, "executableSha256"), executableSha,
                    StringComparison.OrdinalIgnoreCase) &&
                ManifestArtifactMatches(repositoryRoot, tool, "sourcePath",
                    "sourceNormalizedSha256", true) &&
                ManifestArtifactMatches(repositoryRoot, tool, "executablePath",
                    "executableSha256", false) &&
                ManifestArtifactMatches(repositoryRoot, tool, "verifierPath", "verifierSha256",
                    false),
                "PREDECESSOR_TOOLCHAIN_MANIFEST_IDENTITY_MISMATCH", 3,
                "producer manifest does not bind the live reviewed source/executable/verifier: " + toolId);
        }

        private static string ProducerManifestRelativePath(string toolId)
        {
            if (toolId == "native_seed_pack_888x14_v1")
                return "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json";
            if (toolId == "native_width_888_v1")
                return "workers/native_model_requests/development/v1/tools/width_888_native_v1/toolchain_manifest.json";
            if (toolId == "native_door_module_888x14_v1")
                return "workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/toolchain_manifest.json";
            throw new GateException("UNEXPECTED_PREDECESSOR_TOOL_ID", 3, toolId);
        }

        private static bool ManifestArtifactMatches(string repositoryRoot,
            Dictionary<string, object> tool, string pathKey, string hashKey, bool normalizedSource)
        {
            string relative = TextValue(tool, pathKey);
            if (string.IsNullOrWhiteSpace(relative) || relative.IndexOf('\\') >= 0 ||
                Path.IsPathRooted(relative) || relative.Split('/').Any(segment =>
                    segment == "." || segment == ".." || segment.Length == 0)) return false;
            string path = Path.GetFullPath(Path.Combine(repositoryRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            return IsUnder(path, repositoryRoot) && File.Exists(path) &&
                NoReparsePoints(repositoryRoot, path) && FileLinkCount(path) == 1 &&
                IsSha256(TextValue(tool, hashKey)) &&
                string.Equals(normalizedSource ? NormalizedStandardToolSourceSha256(path) : Sha256(path),
                    TextValue(tool, hashKey), StringComparison.OrdinalIgnoreCase);
        }

        private static string EvidenceCommitmentDigest(Dictionary<string, object> evidence)
        {
            if (evidence == null) return "";
            var projection = new Dictionary<string, object>(evidence, StringComparer.Ordinal);
            foreach (string key in EvidenceCommitmentExcludedKeys) projection.Remove(key);
            return Sha256Text(StableJson(projection));
        }

        private static string EvidenceCommitmentDigest(Result result)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 };
            Dictionary<string, object> row = serializer.DeserializeObject(
                serializer.Serialize(result)) as Dictionary<string, object>;
            return EvidenceCommitmentDigest(row);
        }

        private static Dictionary<string, object> PrepareLockReceipt(Result result,
            string evidenceSha256)
        {
            var receipt = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", ReceiptSchema }, { "phase", "lock_topology_888x14" },
                { "success", true }, { "completedAt", result.completed_at_utc },
                { "taskId", result.task_id }, { "taskRevision", result.task_revision },
                { "taskDigest", result.task_digest }, { "requestDigest", result.request_digest },
                { "leaseId", result.lease_id }, { "leaseExpiresAt", result.lease_expires_at_utc },
                { "attempt", result.attempt_number }, { "planSha256", result.plan_sha256 },
                { "authorizationId", result.authorization_id },
                { "authorizationSha256", result.authorization_sha256 },
                { "authorizationJsonBase64", result.authorization_json_base64 },
                { "authorizationIssuedAt", result.authorization_issued_at_utc },
                { "authorizationExpiresAt", result.authorization_expires_at_utc },
                { "recipeId", TrustedRecipeId }, { "recipeDigest", ExpectedRecipeDigest },
                { "toolId", ExpectedToolId },
                { "toolSourceNormalizedSha256", result.tool_source_normalized_sha256 },
                { "toolExecutableSha256", result.tool_executable_sha256 },
                { "evidencePath", EvidenceRelativePath.Replace('\\', '/') },
                { "evidenceSha256", evidenceSha256 },
                { "evidenceCommitmentSha256", result.evidence_commitment_sha256 },
                { "preInventoryDigest", result.initial_inventory_digest },
                { "postInventoryDigest", result.post_inventory_digest },
                { "predecessorReceiptSha256", result.predecessor_receipt_sha256 }
            };
            Require(ExactKeys(receipt, RuntimeReceiptKeys), "LOCK_RECEIPT_SCHEMA_DRIFT", 92,
                "prepared lock receipt does not have the exact shared runtime receipt keys");
            return receipt;
        }

        private static bool ValidateCommittedLockReceipt(string receiptPath, string evidencePath,
            Result result)
        {
            try
            {
                if (!File.Exists(receiptPath) || FileLinkCount(receiptPath) != 1 ||
                    !File.Exists(evidencePath) || FileLinkCount(evidencePath) != 1) return false;
                string receiptShaBefore = Sha256(receiptPath);
                string evidenceShaBefore = Sha256(evidencePath);
                Dictionary<string, object> receipt = ReadJsonObject(receiptPath);
                Dictionary<string, object> evidence = ReadJsonObject(evidencePath);
                string doorReceiptPath = Path.Combine(result.attempt_root,
                    StageReceiptPaths[5].Replace('/', Path.DirectorySeparatorChar));
                return ExactKeys(receipt, RuntimeReceiptKeys) &&
                    string.Equals(TextValue(receipt, "schema"), ReceiptSchema,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "phase"), "lock_topology_888x14",
                        StringComparison.Ordinal) && BoolValue(receipt, "success") &&
                    string.Equals(TextValue(receipt, "completedAt"), result.completed_at_utc,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "taskId"), result.task_id,
                        StringComparison.Ordinal) &&
                    NumberValue(receipt, "taskRevision") == result.task_revision &&
                    string.Equals(TextValue(receipt, "taskDigest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "requestDigest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "leaseId"), result.lease_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "leaseExpiresAt"),
                        result.lease_expires_at_utc, StringComparison.Ordinal) &&
                    NumberValue(receipt, "attempt") == result.attempt_number &&
                    string.Equals(TextValue(receipt, "planSha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "authorizationId"),
                        result.authorization_id, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationSha256"),
                        result.authorization_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "authorizationJsonBase64"),
                        result.authorization_json_base64, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationIssuedAt"),
                        result.authorization_issued_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "authorizationExpiresAt"),
                        result.authorization_expires_at_utc, StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "recipeId"), TrustedRecipeId,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "recipeDigest"), ExpectedRecipeDigest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "toolId"), ExpectedToolId,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "toolSourceNormalizedSha256"),
                        result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "toolExecutableSha256"),
                        result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "evidencePath"),
                        EvidenceRelativePath.Replace('\\', '/'), StringComparison.Ordinal) &&
                    string.Equals(TextValue(receipt, "evidenceSha256"), evidenceShaBefore,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "evidenceCommitmentSha256"),
                        EvidenceCommitmentDigest(evidence), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "preInventoryDigest"),
                        result.initial_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "postInventoryDigest"),
                        result.post_inventory_digest, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(receipt, "predecessorReceiptSha256"),
                        result.predecessor_receipt_sha256, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(doorReceiptPath) &&
                    string.Equals(Sha256(doorReceiptPath), result.predecessor_receipt_sha256,
                        StringComparison.OrdinalIgnoreCase) && BoolValue(evidence, "success") &&
                    string.Equals(TextValue(evidence, "status"),
                        "LOCK_TOPOLOGY_888X14_COMPLETE", StringComparison.Ordinal) &&
                    !evidence.ContainsKey("phase_receipt_path") &&
                    !evidence.ContainsKey("phase_receipt_sha256") &&
                    !evidence.ContainsKey("phase_receipt_committed") &&
                    string.Equals(receiptShaBefore, Sha256(receiptPath),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evidenceShaBefore, Sha256(evidencePath),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool ContainUnpairedSuccessArtifact(string path, string attemptRoot,
            string label)
        {
            try
            {
                if (!File.Exists(path)) return true;
                try { File.Delete(path); } catch { }
                if (!File.Exists(path)) return true;
                string quarantine = Path.Combine(attemptRoot, "failed_evidence_quarantine");
                Directory.CreateDirectory(quarantine);
                if (!NoReparsePoints(attemptRoot, quarantine)) return false;
                string target = Path.Combine(quarantine, label + "-" +
                    Guid.NewGuid().ToString("N") + ".unpaired.json");
                File.Move(path, target);
                return !File.Exists(path) && File.Exists(target);
            }
            catch { return false; }
        }

        private static bool ParseUtc(string value, out DateTime result)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out result);
        }

        private static void BindImmutablePlanAndAuthorization(string attemptRoot, string attemptsRoot,
            Result result)
        {
            Require(NoReparsePoints(attemptsRoot, attemptRoot), "ATTEMPT_PATH_HAS_REPARSE_POINT", 3,
                "worker-owned attempt path contains a reparse point");
            string planPath = Path.Combine(attemptRoot, "native_build_plan.json");
            string expectedPlanSha = NormalizeHash(System.Environment.GetEnvironmentVariable(PlanShaEnvironmentVariable));
            Require(File.Exists(planPath) && IsSha256(expectedPlanSha), "IMMUTABLE_PLAN_MISSING", 3,
                PlanShaEnvironmentVariable + " must bind native_build_plan.json");
            string actualPlanSha = Sha256(planPath);
            Require(string.Equals(actualPlanSha, expectedPlanSha, StringComparison.OrdinalIgnoreCase),
                "IMMUTABLE_PLAN_HASH_MISMATCH", 3, "native_build_plan.json hash does not match the worker binding");
            Dictionary<string, object> plan = ReadJsonObject(planPath);
            Dictionary<string, object> planTask = ChildObject(plan, "task");
            Dictionary<string, object> planRequest = ChildObject(plan, "request");
            Dictionary<string, object> recipe = ChildObject(plan, "recipe");
            Dictionary<string, object> geometry = ChildObject(plan, "geometry");
            Dictionary<string, object> quality = ChildObject(plan, "qualityBoundary");
            Dictionary<string, object> execution = ChildObject(plan, "executionBoundary");
            string taskId = new DirectoryInfo(Path.GetDirectoryName(attemptRoot)).Name;
            int attemptNumber = int.Parse(new DirectoryInfo(attemptRoot).Name.Substring(8), CultureInfo.InvariantCulture);
            Require(string.Equals(TextValue(plan, "schema"), "winnsen.native_build_plan.v1", StringComparison.Ordinal) &&
                string.Equals(TextValue(plan, "purpose"), "structure_engineering_assistance", StringComparison.Ordinal) &&
                string.Equals(TextValue(planTask, "id"), taskId, StringComparison.Ordinal) &&
                NumberValue(planTask, "revisionAtPlanning") > 0 && IsSha256(TextValue(planTask, "digest")) &&
                IsSha256(TextValue(planRequest, "fingerprint")) && IsSha256(TextValue(planRequest, "digest")) &&
                string.Equals(TextValue(recipe, "id"), TrustedRecipeId, StringComparison.Ordinal) &&
                NumberValue(recipe, "version") == 1 &&
                string.Equals(TextValue(recipe, "digest"), ExpectedRecipeDigest, StringComparison.OrdinalIgnoreCase) &&
                Near(NumberValue(geometry, "cabinetWidthMm"), 888.0, 1e-9) &&
                Near(NumberValue(geometry, "cabinetHeightMm"), 1917.0, 1e-9) &&
                Near(NumberValue(geometry, "cabinetDepthMm"), 550.0, 1e-9) &&
                NumberValue(geometry, "columns") == 2 && NumberValue(geometry, "doorCount") == 14 &&
                NumberArrayEquals(ArrayValue(geometry, "columnDoorCounts"), new[] { 7.0, 7.0 }) &&
                string.Equals(TextValue(geometry, "rowSequence"), "L1111111-R1111111", StringComparison.Ordinal) &&
                Near(NumberValue(geometry, "doorPanelWidthMm"), 381.0, 1e-9) &&
                BoolValue(quality, "planningOnly") && !BoolValue(execution, "executorImplemented") &&
                !BoolValue(execution, "legacyFallbackUsed") &&
                StringArrayContains(ArrayValue(plan, "requiredChecks"), "one_door_one_lock",
                    "native_lock_slot_edge_profile", "paired_diameter_5_circle_per_lock_opening",
                    "mechanical_lock_tongue_alignment", "rebuild_save_reopen", "relocated_reopen"),
                "IMMUTABLE_PLAN_CONTRACT_MISMATCH", 3,
                "plan must remain planning-only and bind the exact reviewed 888x14 recipe/checks");
            ValidateTrustedPlanSecurityContracts(plan, result);

            string authorizationPath = Path.GetFullPath((System.Environment.GetEnvironmentVariable(
                AuthorizationPathEnvironmentVariable) ?? "").Trim());
            string exactAuthorizationPath = Path.Combine(attemptRoot,
                @"execution_authorizations\native_lock_topology_888x14_v1.json");
            Require(SamePath(authorizationPath, exactAuthorizationPath) && File.Exists(authorizationPath),
                "EXECUTION_AUTHORIZATION_PATH_MISMATCH", 3,
                AuthorizationPathEnvironmentVariable + " must identify the exact stage-specific authorization");
            Require(NoReparsePoints(attemptsRoot, authorizationPath), "EXECUTION_AUTHORIZATION_REPARSE_POINT", 3,
                "authorization path contains a reparse point");
            string expectedAuthorizationSha = NormalizeHash(System.Environment.GetEnvironmentVariable(
                AuthorizationShaEnvironmentVariable));
            Require(IsSha256(expectedAuthorizationSha), "EXECUTION_AUTHORIZATION_SHA_MISSING", 3,
                AuthorizationShaEnvironmentVariable + " must contain the immutable authorization SHA-256");
            string actualAuthorizationSha = Sha256(authorizationPath);
            Require(string.Equals(actualAuthorizationSha, expectedAuthorizationSha,
                StringComparison.OrdinalIgnoreCase), "EXECUTION_AUTHORIZATION_SHA_MISMATCH", 3,
                "stage authorization hash does not match the worker binding");
            Dictionary<string, object> authorization = ReadJsonObject(authorizationPath);
            Dictionary<string, object> authTask = ChildObject(authorization, "task");
            Dictionary<string, object> authRequest = ChildObject(authorization, "request");
            Dictionary<string, object> authPlan = ChildObject(authorization, "plan");
            Dictionary<string, object> authRecipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> authTool = ChildObject(authorization, "tool");
            Dictionary<string, object> authSeed = ChildObject(authorization, "seed");
            Dictionary<string, object> authExecution = ChildObject(authorization, "execution");
            Dictionary<string, object> authQuality = ChildObject(authorization, "qualityBoundary");
            string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executablePath), "..",
                "BuildLockTopology888x14.cs"));
            Require(File.Exists(sourcePath) && string.Equals(Sha256(sourcePath), ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase), "TOOL_SOURCE_LIVE_HASH_MISMATCH", 3,
                "live lock-topology source does not match the normalized source identity compiled into the executable");
            Require(ExactKeys(authorization, "schema", "authorizationId", "issuedAt", "expiresAt", "purpose",
                    "workerId", "task", "request", "plan", "recipe", "tool", "seed", "execution", "qualityBoundary"),
                "AUTHORIZATION_TOP_LEVEL_KEYS_MISMATCH", 3, "authorization top-level keys must exactly match the shared schema");
            Require(string.Equals(TextValue(authorization, "schema"), ExpectedAuthorizationSchema,
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(authorization, "purpose"), "structure_engineering_assistance", StringComparison.Ordinal) &&
                string.Equals(TextValue(authorization, "workerId"), TextValue(plan, "workerId"), StringComparison.Ordinal) &&
                ExactKeys(authTask, "id", "revision", "digest", "leaseId", "leaseExpiresAt") &&
                ExactKeys(authRequest, "fingerprint", "digest") && ExactKeys(authPlan, "sha256") &&
                ExactKeys(authRecipe, "id", "version", "digest") &&
                ExactKeys(authTool, "id", "sourceNormalizedSha256", "executableSha256") &&
                ExactKeys(authSeed, "inventoryDigest") && ExactKeys(authExecution, "authorized", "attempt", "phases") &&
                ExactKeys(authQuality, "engineeringAssistanceReady", "readyOnlyAfterEveryRequiredCheckPasses") &&
                !BoolValue(authQuality, "engineeringAssistanceReady") &&
                BoolValue(authQuality, "readyOnlyAfterEveryRequiredCheckPasses") &&
                AuthorizationTimeWindowIsValid(authorization) &&
                string.Equals(TextValue(authTask, "id"), taskId, StringComparison.Ordinal) &&
                NumberValue(authTask, "revision") == NumberValue(planTask, "revisionAtPlanning") &&
                string.Equals(TextValue(authTask, "digest"), TextValue(planTask, "digest"),
                    StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(TextValue(authTask, "leaseId")) &&
                string.Equals(TextValue(authRequest, "fingerprint"), TextValue(planRequest, "fingerprint"),
                    StringComparison.Ordinal) &&
                string.Equals(TextValue(authRequest, "digest"), TextValue(planRequest, "digest"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(authPlan, "sha256"), actualPlanSha, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(authRecipe, "id"), TrustedRecipeId, StringComparison.Ordinal) &&
                NumberValue(authRecipe, "version") == 1 &&
                string.Equals(TextValue(authRecipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(authTool, "id"), ExpectedToolId, StringComparison.Ordinal) &&
                string.Equals(TextValue(authTool, "sourceNormalizedSha256"), ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TextValue(authTool, "executableSha256"), Sha256(executablePath),
                    StringComparison.OrdinalIgnoreCase) && IsSha256(TextValue(authSeed, "inventoryDigest")) &&
                BoolValue(authExecution, "authorized") &&
                NumberValue(authExecution, "attempt") == attemptNumber &&
                StringArrayExactly(ArrayValue(authExecution, "phases"), "lock_topology_888x14") &&
                AuthorizationIdentityMatches(authorization),
                "EXECUTION_AUTHORIZATION_BINDING_MISMATCH", 3,
                "authorization must bind task lease/revision/digest, request, plan, recipe, tool, seed, attempt, phase, and time window");
            result.plan_path = planPath;
            result.plan_sha256 = actualPlanSha;
            result.authorization_path = authorizationPath;
            result.authorization_sha256 = actualAuthorizationSha;
            result.authorization_id = TextValue(authorization, "authorizationId");
            result.authorization_json_base64 = Convert.ToBase64String(File.ReadAllBytes(authorizationPath));
            result.authorization_issued_at_utc = TextValue(authorization, "issuedAt");
            result.authorization_seed_inventory_digest = TextValue(authSeed, "inventoryDigest");
            result.authorization_gate = true;
            result.tool_source_normalized_sha256 = ExpectedSourceSha256;
            result.tool_source_path = sourcePath;
            result.tool_executable_path = Path.GetFullPath(executablePath);
            result.tool_executable_sha256 = Sha256(executablePath);
            result.task_id = taskId;
            result.worker_id = TextValue(plan, "workerId");
            result.task_revision = Convert.ToInt32(NumberValue(planTask, "revisionAtPlanning"), CultureInfo.InvariantCulture);
            result.task_digest = TextValue(planTask, "digest");
            result.request_fingerprint = TextValue(planRequest, "fingerprint");
            result.request_digest = TextValue(planRequest, "digest");
            result.lease_id = TextValue(authTask, "leaseId");
            result.authorization_expires_at_utc = TextValue(authorization, "expiresAt");
            result.lease_expires_at_utc = TextValue(authTask, "leaseExpiresAt");
            result.attempt_number = attemptNumber;
        }

        private static Dictionary<string, object> ReadJsonObject(string path)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            return serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object> ??
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static Dictionary<string, object> ChildObject(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value is Dictionary<string, object>
                ? (Dictionary<string, object>)value : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static string TextValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        }

        private static double NumberValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value)
                ? Safe(delegate { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }, double.NaN)
                : double.NaN;
        }

        private static bool BoolValue(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value is bool && (bool)value;
        }

        private static object[] ArrayValue(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null) return new object[0];
            object[] array = value as object[];
            if (array != null) return array;
            System.Collections.ArrayList list = value as System.Collections.ArrayList;
            return list == null ? new object[0] : list.ToArray();
        }

        private static bool NumberArrayEquals(object[] actual, double[] expected)
        {
            return actual.Length == expected.Length && actual.Select(value =>
                Safe(delegate { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }, double.NaN))
                .Zip(expected, (left, right) => Near(left, right, 0.0)).All(value => value);
        }

        private static bool StringArrayContains(object[] actual, params string[] expected)
        {
            var values = new HashSet<string>(actual.Select(value =>
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""), StringComparer.Ordinal);
            return expected.All(values.Contains);
        }

        private static bool AuthorizationTimeWindowIsValid(Dictionary<string, object> authorization)
        {
            DateTime issued, expires;
            if (!DateTime.TryParse(TextValue(authorization, "issuedAt"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out issued) ||
                !DateTime.TryParse(TextValue(authorization, "expiresAt"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out expires)) return false;
            DateTime leaseExpires;
            Dictionary<string, object> task = ChildObject(authorization, "task");
            if (!DateTime.TryParse(TextValue(task, "leaseExpiresAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out leaseExpires)) return false;
            DateTime now = DateTime.UtcNow;
            return issued <= now.AddMinutes(1) && expires > now && expires > issued &&
                expires <= leaseExpires && expires - issued <= TimeSpan.FromMinutes(15);
        }

        private static bool AuthorizationStillValidAtCommit(Result result)
        {
            try
            {
                if (result == null || !result.authorization_gate ||
                    !File.Exists(result.authorization_path) ||
                    !string.Equals(Sha256(result.authorization_path), result.authorization_sha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Convert.ToBase64String(File.ReadAllBytes(result.authorization_path)),
                        result.authorization_json_base64, StringComparison.Ordinal)) return false;
                Dictionary<string, object> authorization = ReadJsonObject(result.authorization_path);
                if (!ExactKeys(authorization, "schema", "authorizationId", "issuedAt", "expiresAt",
                        "purpose", "workerId", "task", "request", "plan", "recipe", "tool",
                        "seed", "execution", "qualityBoundary") ||
                    !AuthorizationIdentityMatches(authorization)) return false;
                Dictionary<string, object> task = ChildObject(authorization, "task");
                Dictionary<string, object> request = ChildObject(authorization, "request");
                Dictionary<string, object> plan = ChildObject(authorization, "plan");
                Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
                Dictionary<string, object> tool = ChildObject(authorization, "tool");
                Dictionary<string, object> seed = ChildObject(authorization, "seed");
                Dictionary<string, object> execution = ChildObject(authorization, "execution");
                DateTime authorizationExpires, leaseExpires;
                return ParseUtc(TextValue(authorization, "expiresAt"), out authorizationExpires) &&
                    ParseUtc(TextValue(task, "leaseExpiresAt"), out leaseExpires) &&
                    DateTime.UtcNow < authorizationExpires && DateTime.UtcNow < leaseExpires &&
                    string.Equals(TextValue(authorization, "authorizationId"),
                        result.authorization_id, StringComparison.Ordinal) &&
                    string.Equals(TextValue(authorization, "workerId"), result.worker_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(task, "id"), result.task_id, StringComparison.Ordinal) &&
                    NumberValue(task, "revision") == result.task_revision &&
                    string.Equals(TextValue(task, "digest"), result.task_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(task, "leaseId"), result.lease_id,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(request, "fingerprint"), result.request_fingerprint,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(request, "digest"), result.request_digest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(plan, "sha256"), result.plan_sha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(recipe, "id"), TrustedRecipeId,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(recipe, "digest"), ExpectedRecipeDigest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(tool, "id"), ExpectedToolId,
                        StringComparison.Ordinal) &&
                    string.Equals(TextValue(tool, "sourceNormalizedSha256"),
                        result.tool_source_normalized_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(tool, "executableSha256"),
                        result.tool_executable_sha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(TextValue(seed, "inventoryDigest"), result.initial_inventory_digest,
                        StringComparison.OrdinalIgnoreCase) && BoolValue(execution, "authorized") &&
                    NumberValue(execution, "attempt") == result.attempt_number &&
                    StringArrayExactly(ArrayValue(execution, "phases"), "lock_topology_888x14");
            }
            catch { return false; }
        }

        private static bool ExactKeys(Dictionary<string, object> value, params string[] expected)
        {
            return value != null && new HashSet<string>(value.Keys, StringComparer.Ordinal)
                .SetEquals(expected) && value.Count == expected.Length;
        }

        private static bool StringArrayExactly(object[] values, params string[] expected)
        {
            return values.Length == expected.Length && values.Select((value, index) =>
                string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), expected[index], StringComparison.Ordinal)).All(v => v);
        }

        private static bool AuthorizationIdentityMatches(Dictionary<string, object> authorization)
        {
            var binding = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "workerId", authorization["workerId"] }, { "task", authorization["task"] },
                { "request", authorization["request"] }, { "plan", authorization["plan"] },
                { "recipe", authorization["recipe"] }, { "tool", authorization["tool"] },
                { "seed", authorization["seed"] }, { "execution", authorization["execution"] },
                { "issuedAt", authorization["issuedAt"] }, { "expiresAt", authorization["expiresAt"] }
            };
            string expected = "native-auth-" + Sha256Text(StableJson(binding)).Substring(0, 32).ToLowerInvariant();
            return string.Equals(TextValue(authorization, "authorizationId"), expected, StringComparison.Ordinal);
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
            Dictionary<string, object> map = value as Dictionary<string, object>;
            if (map != null) return "{" + string.Join(",", map.Keys.OrderBy(k => k,
                StringComparer.Ordinal).Select(k => JsonString(k) + ":" + StableJson(map[k]))) + "}";
            System.Collections.IEnumerable sequence = value as System.Collections.IEnumerable;
            if (sequence != null)
            {
                var rows = new List<string>();
                foreach (object row in sequence) rows.Add(StableJson(row));
                return "[" + string.Join(",", rows) + "]";
            }
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 };
            return StableJson(serializer.DeserializeObject(serializer.Serialize(value)));
        }

        private static string JsonString(string value)
        {
            var builder = new StringBuilder((value ?? "").Length + 2);
            builder.Append('"');
            for (int index = 0; index < (value ?? "").Length; index++)
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

        private static string NormalizeHash(string value)
        {
            return (value ?? "").Trim().ToUpperInvariant();
        }

        private static bool IsSha256(string value)
        {
            return Regex.IsMatch(NormalizeHash(value), "^[0-9A-F]{64}$");
        }

        private static bool Near(double left, double right, double tolerance)
        {
            return !double.IsNaN(left) && !double.IsInfinity(left) &&
                !double.IsNaN(right) && !double.IsInfinity(right) && Math.Abs(left - right) <= tolerance;
        }

        private static bool NoReparsePoints(string root, string candidate)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd('\\');
            string fullCandidate = Path.GetFullPath(candidate).TrimEnd('\\');
            if ((File.Exists(fullCandidate) || Directory.Exists(fullCandidate)) &&
                (File.GetAttributes(fullCandidate) & FileAttributes.ReparsePoint) != 0) return false;
            string current = File.Exists(fullCandidate) ? Path.GetDirectoryName(fullCandidate) : fullCandidate;
            if (!IsUnder(current, fullRoot)) return false;
            while (!string.IsNullOrWhiteSpace(current) && IsUnder(current, fullRoot))
            {
                if (File.Exists(current) || Directory.Exists(current))
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
                if (SamePath(current, fullRoot)) return true;
                current = Path.GetDirectoryName(current);
            }
            return false;
        }

        private static bool TreeHasReparsePoint(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return true;
            return Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                .Any(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
        }

        private static bool TreeHasHardLinks(string directory)
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Any(path => FileLinkCount(path) != 1);
        }

        private static uint FileLinkCount(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    ByHandleFileInformation information;
                    return GetFileInformationByHandle(stream.SafeFileHandle, out information)
                        ? information.NumberOfLinks : uint.MaxValue;
                }
            }
            catch { return uint.MaxValue; }
        }

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            Microsoft.Win32.SafeHandles.SafeFileHandle handle, out ByHandleFileInformation information);

        private static bool ContainsStepOrFreeCad(string directory)
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any(path =>
            {
                string extension = Path.GetExtension(path);
                return string.Equals(extension, ".step", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".stp", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".fcstd", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).IndexOf("freecad", StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private static void CopyDirectoryExact(string source, string target)
        {
            Require(Directory.Exists(source), "COPY_SOURCE_MISSING", 91, source);
            Require(!Directory.Exists(target), "COPY_TARGET_ALREADY_EXISTS", 91, target);
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("reparse point refused: " + directory);
            Directory.CreateDirectory(target);
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, RelativePath(source, directory)));
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("reparse point refused: " + file);
                string destination = Path.Combine(target, RelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, false);
            }
        }

        private static void RestoreDirectoryExact(string backup, string target)
        {
            Require(Directory.Exists(backup), "ROLLBACK_BACKUP_MISSING", 91, backup);
            string backupAttempt = Path.GetDirectoryName(Path.GetDirectoryName(backup));
            string targetAttempt = Path.GetDirectoryName(Path.GetDirectoryName(target));
            Require(SamePath(backupAttempt, targetAttempt) &&
                string.Equals(Path.GetFileName(Path.GetDirectoryName(target)), "native_cad",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(target), "working_pack", StringComparison.OrdinalIgnoreCase),
                "ROLLBACK_PATH_REFUSED", 91, "rollback must remain inside the same exact attempt working_pack");
            Require(!TreeHasReparsePoint(backup) && !TreeHasHardLinks(backup),
                "ROLLBACK_BACKUP_LINK_REFUSED", 91,
                "backup tree contains a reparse point or hard link");
            if (Directory.Exists(target))
                Require(!TreeHasReparsePoint(target), "ROLLBACK_TARGET_REPARSE_POINT_REFUSED", 91,
                    "refusing recursive delete of a working_pack containing a reparse point");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            CopyDirectoryExact(backup, target);
        }

        private static List<InventoryRow> Inventory(string directory)
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => new InventoryRow
                {
                    relative_path = RelativePath(directory, path).Replace('\\', '/'),
                    size = new FileInfo(path).Length,
                    sha256 = Sha256(path)
                }).OrderBy(row => row.relative_path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool InventoriesEqual(List<InventoryRow> left, List<InventoryRow> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
                if (!string.Equals(left[index].relative_path, right[index].relative_path,
                        StringComparison.OrdinalIgnoreCase) || left[index].size != right[index].size ||
                    !string.Equals(left[index].sha256, right[index].sha256,
                        StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static bool IsCadInventoryRow(InventoryRow row)
        {
            if (row == null || row.relative_path.IndexOf('/') >= 0 || row.relative_path.IndexOf('\\') >= 0)
                return false;
            string extension = Path.GetExtension(row.relative_path);
            return string.Equals(extension, ".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".SLDASM", StringComparison.OrdinalIgnoreCase);
        }

        private static string InventoryDigest(List<InventoryRow> rows)
        {
            var builder = new StringBuilder();
            foreach (InventoryRow row in rows.OrderBy(value => value.relative_path,
                StringComparer.OrdinalIgnoreCase))
                builder.Append(Path.GetFileName(row.relative_path)).Append('|')
                    .Append(row.sha256.ToUpperInvariant()).Append('\n');
            return Sha256Text(builder.ToString());
        }

        private static void AuditPostInventory(Result result)
        {
            result.added_files.Clear();
            result.deleted_files.Clear();
            result.changed_files.Clear();
            var before = result.original_inventory.ToDictionary(row => row.relative_path,
                StringComparer.OrdinalIgnoreCase);
            var after = result.post_inventory.ToDictionary(row => row.relative_path,
                StringComparer.OrdinalIgnoreCase);
            result.added_files.AddRange(after.Keys.Where(name => !before.ContainsKey(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            result.deleted_files.AddRange(before.Keys.Where(name => !after.ContainsKey(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            result.changed_files.AddRange(before.Keys.Where(name => after.ContainsKey(name) &&
                (!string.Equals(before[name].sha256, after[name].sha256, StringComparison.OrdinalIgnoreCase) ||
                    before[name].size != after[name].size)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            result.post_part_count = result.post_inventory.Count(row =>
                string.Equals(Path.GetExtension(row.relative_path), ".SLDPRT", StringComparison.OrdinalIgnoreCase));
            result.post_assembly_count = result.post_inventory.Count(row =>
                string.Equals(Path.GetExtension(row.relative_path), ".SLDASM", StringComparison.OrdinalIgnoreCase));
            result.post_inventory_digest = InventoryDigest(result.post_inventory);
            var expectedChanged = new HashSet<string>(new[] { LeftPartName, RightPartName },
                StringComparer.OrdinalIgnoreCase);
            result.post_inventory_gate = result.post_inventory.Count == 75 && result.post_part_count == 53 &&
                result.post_assembly_count == 22 && result.post_inventory.All(IsCadInventoryRow) &&
                result.added_files.Count == 0 && result.deleted_files.Count == 0 &&
                new HashSet<string>(result.changed_files, StringComparer.OrdinalIgnoreCase).SetEquals(expectedChanged) &&
                !File.Exists(result.temporary_right_path) && !TreeHasReparsePoint(result.cad_directory) &&
                !TreeHasHardLinks(result.cad_directory);
            Require(result.post_inventory_gate, "POST_PHASE_INVENTORY_OR_LINK_GATE_FAILED", 30,
                "post phase must remain the exact 75 CAD names with only L/R changed and no temporary, reparse, or hard-linked file");
        }

        private static string RelativePath(string root, string path)
        {
            Uri rootUri = new Uri(Path.GetFullPath(root).TrimEnd('\\') + "\\");
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', '\\');
        }

        private static Feature FindFeature(ModelDoc2 model, string name)
        {
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                if (string.Equals(Safe(delegate { return feature.Name; }, ""), name,
                    StringComparison.OrdinalIgnoreCase)) return feature;
                feature = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
            }
            return null;
        }

        private static bool SelectRightPlane(ModelDoc2 model)
        {
            foreach (string name in new[] { "右视", "Right Plane", "Right" })
            {
                bool selected = Safe(delegate
                {
                    return model.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0);
                }, false);
                if (selected) return true;
            }
            return false;
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

        private static bool SetSuppression(Feature feature, bool suppress)
        {
            if (feature == null) return false;
            if (Safe(delegate { return feature.IsSuppressed(); }, false) == suppress) return true;
            bool changed = Safe(delegate
            {
                return feature.SetSuppression2(
                    suppress ? (int)swFeatureSuppressionAction_e.swSuppressFeature :
                        (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                    (int)swInConfigurationOpts_e.swThisConfiguration, null);
            }, false);
            return changed && Safe(delegate { return feature.IsSuppressed(); }, !suppress) == suppress;
        }

        private static string NormalizeReferencePath(string value, string directory)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            try { return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(directory, value)); }
            catch { return value; }
        }

        private static int MaxLength(params Array[] arrays)
        {
            return arrays.Where(value => value != null).Select(value => value.Length).DefaultIfEmpty(0).Max();
        }

        private static string ArrayString(Array array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return "";
            return Convert.ToString(array.GetValue(index), CultureInfo.InvariantCulture) ?? "";
        }

        private static int ArrayInt(Array array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return int.MinValue;
            return Convert.ToInt32(array.GetValue(index), CultureInfo.InvariantCulture);
        }

        private static double[] PointCoordinates(SketchPoint point)
        {
            if (point == null) return new double[0];
            return new[]
            {
                Safe(delegate { return point.X; }, double.NaN),
                Safe(delegate { return point.Y; }, double.NaN),
                Safe(delegate { return point.Z; }, double.NaN)
            };
        }

        private static double[] VertexMm(Vertex vertex)
        {
            try { return ToPointMm(vertex == null ? null : Safe(delegate { return vertex.GetPoint(); }, null)); }
            finally { Release(vertex); }
        }

        private static double[] ToPointMm(object raw)
        {
            double[] values = ToNumbers(raw);
            for (int index = 0; index < Math.Min(3, values.Length); index++) values[index] *= 1000.0;
            return values;
        }

        private static double[] ToNumbers(object raw)
        {
            double[] direct = raw as double[];
            if (direct != null) return (double[])direct.Clone();
            Array array = raw as Array;
            if (array == null) return new double[0];
            var values = new double[array.Length];
            for (int index = 0; index < array.Length; index++)
                values[index] = Convert.ToDouble(array.GetValue(index), CultureInfo.InvariantCulture);
            return values;
        }

        private static bool RowsEqual(double[] actual, double[] expected, double tolerance)
        {
            return actual != null && expected != null && actual.Length == expected.Length &&
                actual.Zip(expected, (left, right) => Close(left, right, tolerance)).All(value => value);
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

        private static bool IsUnder(string path, string root)
        {
            try
            {
                string fullPath = Path.GetFullPath(path).TrimEnd('\\') + "\\";
                string fullRoot = Path.GetFullPath(root).TrimEnd('\\') + "\\";
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static SlotTemplate T(string id, double length,
            double ax, double ay, double az, double bx, double by, double bz)
        {
            return new SlotTemplate { id = id, length = length,
                a = new[] { ax, ay, az }, b = new[] { bx, by, bz } };
        }

        private static double Round(double value, int digits)
        {
            return Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        private static bool Close(double left, double right, double tolerance)
        {
            return Math.Abs(left - right) <= tolerance;
        }

        private static string Sha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 hash = SHA256.Create())
                return BytesToHex(hash.ComputeHash(stream));
        }

        private static string Sha256Text(string value)
        {
            using (SHA256 hash = SHA256.Create())
                return BytesToHex(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }

        private static string Sha256Bytes(byte[] value)
        {
            using (SHA256 hash = SHA256.Create())
                return BytesToHex(hash.ComputeHash(value ?? new byte[0]));
        }

        private static string BytesToHex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        private static string Json(double[] values)
        {
            return "[" + string.Join(",", values.Select(value => value.ToString("R",
                CultureInfo.InvariantCulture))) + "]";
        }

        private static void WriteJsonAtomicNew(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Require(!File.Exists(path), "EVIDENCE_ALREADY_EXISTS", 92,
                "single evidence commit refuses overwrite");
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
            string content = serializer.Serialize(result);
            string temp = path + ".tmp-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) +
                "-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content + System.Environment.NewLine);
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temp, path);
                byte[] committed = File.ReadAllBytes(path);
                byte[] expected = new UTF8Encoding(false).GetBytes(content + System.Environment.NewLine);
                Require(committed.SequenceEqual(expected), "EVIDENCE_COMMIT_BYTES_MISMATCH", 92,
                    "committed evidence bytes differ from the prepared final object");
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void WriteJsonObjectAtomicNew(string path,
            Dictionary<string, object> value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Require(!File.Exists(path), "JSON_OBJECT_ALREADY_EXISTS", 92,
                "atomic object commit refuses overwrite");
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 };
            string content = serializer.Serialize(value);
            string temporary = path + ".tmp-" +
                Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + "-" +
                Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content +
                    System.Environment.NewLine);
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, path);
                Require(FileLinkCount(path) == 1 && File.ReadAllBytes(path).SequenceEqual(bytes),
                    "JSON_OBJECT_COMMIT_BYTES_MISMATCH", 92,
                    "committed object bytes differ from the prepared final object");
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void Require(bool condition, string status, int exitCode, string message)
        {
            if (!condition) throw new GateException(status, exitCode, message);
        }

        private static bool TryAction(Action action)
        {
            try { action(); return true; }
            catch { return false; }
        }

        private static T Safe<T>(Func<T> action, T fallback)
        {
            try { return action(); }
            catch { return fallback; }
        }

        private static string SafeException(Exception error)
        {
            return (error.GetType().FullName + ": " + error.Message + "\n" + error.StackTrace)
                .Replace("\r\n", "\n");
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                try { Marshal.FinalReleaseComObject(value); } catch { }
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

        public sealed class SlotTemplate
        {
            public string id = "";
            public double length;
            public double[] a = new double[0];
            public double[] b = new double[0];
        }

        public sealed class EdgeRow
        {
            public int index;
            public double length_mm;
            public double[] vertex_start_mm = new double[0];
            public double[] vertex_end_mm = new double[0];
            public double[] param_start_mm = new double[0];
        }

        public sealed class SlotAnalysis
        {
            public double row_mm;
            public int matched_edge_count;
            public bool side_orientation_exact = true;
            public bool full_fingerprint;
            public string normalized_signature_sha256 = "";
            public readonly List<int> matched_edge_indexes = new List<int>();
            public readonly List<int> ambiguous_edge_indexes = new List<int>();
            public readonly List<string> missing_or_duplicate_template_ids = new List<string>();
        }

        public sealed class CircleAnalysis
        {
            public double row_mm;
            public int matched_edge_count;
            public bool side_orientation_exact = true;
            public bool full_diameter5_pair;
            public readonly List<int> matched_edge_indexes = new List<int>();
        }

        public sealed class GeometrySnapshot
        {
            public string side = "";
            public int body_count;
            public int feature_count;
            public int error_feature_count;
            public int warning_feature_count;
            public bool has_sheet_metal;
            public bool has_flat_pattern;
            public bool traversal_complete = true;
            public int mirror_feature_count;
            public int forbidden_import_feature_count;
            public int edge_count;
            public double[] slot_rows_mm = new double[0];
            public double[] circle_rows_mm = new double[0];
            public int slot_fragment_count;
            public int circle_fragment_count;
            public int unique_slot_profile_signature_count;
            public bool read_only_reopen;
            public int open_errors;
            public int open_warnings;
            public int session_process_id;
            public string source_sha256 = "";
            public readonly List<string> feature_types = new List<string>();
            public readonly List<EdgeRow> edges = new List<EdgeRow>();
            public readonly List<SlotAnalysis> slot_analyses = new List<SlotAnalysis>();
            public readonly List<CircleAnalysis> circle_analyses = new List<CircleAnalysis>();
        }

        public sealed class ProcessIdentity
        {
            public int pid;
            public string name = "";
            public string command_line = "";
            public long start_ticks_utc;
            public int parent_sldworks_process_id;
            public bool ownership_verified;
        }

        public sealed class SessionRecord
        {
            public string purpose = "";
            public int sldworks_process_id;
            public long sldworks_start_ticks_utc;
            public string solidworks_revision = "";
            public string solidworks_executable_path = "";
            public string solidworks_executable_sha256 = "";
            public bool solidworks_2020_exact_gate;
            public bool created;
            public bool exit_requested;
            public bool process_exited;
            public bool monitors_exited;
            public bool sldprocmon_identity_gate = true;
            public int open_errors;
            public int open_warnings;
            public readonly List<ProcessIdentity> sldprocmon_processes = new List<ProcessIdentity>();
        }

        public sealed class InventoryRow
        {
            public string relative_path = "";
            public long size;
            public string sha256 = "";
        }

        public sealed class TongueContract
        {
            public bool required;
            public bool deferred_to_assembly_stage;
            public string evidence_relative_path = "";
            public int expected_total;
            public int expected_per_side;
            public double expected_left_x_mm;
            public double expected_right_x_mm;
            public double expected_z_mm;
            public string trusted_source_basename = "";
            public string trusted_source_sha256 = "";
            public string forbidden_name_token = "";
            public double[] expected_y_rows_mm = new double[0];
        }

        public sealed class StageReceiptEvidence
        {
            public string phase = "";
            public string path = "";
            public string sha256 = "";
            public string evidence_path = "";
            public string evidence_sha256 = "";
            public string evidence_commitment_sha256 = "";
            public string input_inventory_digest = "";
            public string output_inventory_digest = "";
            public string predecessor_receipt_sha256 = "";
            public string authorization_id = "";
            public string authorization_sha256 = "";
            public string tool_id = "";
            public bool validated;
        }

        public sealed class Result
        {
            public string schema = "";
            public string status = "";
            public bool success;
            public string error = "";
            public string generated_at_utc = "";
            public string completed_at_utc = "";
            public string attempt_root = "";
            public string cad_directory = "";
            public string left_part_path = "";
            public string right_part_path = "";
            public string temporary_right_path = "";
            public string output_path = "";
            public string backup_directory = "";
            public string trusted_recipe_id = "";
            public string trusted_recipe_registry_path = "";
            public string trusted_recipe_registry_sha256_expected = "";
            public string trusted_recipe_registry_sha256_actual = "";
            public string layout_rule_evidence_id = "";
            public string layout_rule_evidence_path = "";
            public string layout_rule_evidence_sha256_expected = "";
            public string layout_rule_evidence_sha256_actual = "";
            public bool trusted_layout_rule_gate;
            public bool path_allowlist_match;
            public string plan_path = "";
            public string plan_sha256 = "";
            public string authorization_path = "";
            public string authorization_sha256 = "";
            public string authorization_id = "";
            public string authorization_json_base64 = "";
            public string authorization_issued_at_utc = "";
            public string authorization_seed_inventory_digest = "";
            public bool authorization_gate;
            public string task_id = "";
            public string worker_id = "";
            public int task_revision;
            public string task_digest = "";
            public string request_fingerprint = "";
            public string request_digest = "";
            public string lease_id = "";
            public string authorization_expires_at_utc = "";
            public string lease_expires_at_utc = "";
            public int attempt_number;
            public string tool_source_normalized_sha256 = "";
            public string tool_source_path = "";
            public string tool_executable_path = "";
            public string tool_executable_sha256 = "";
            public string toolchain_manifest_path = "";
            public string toolchain_manifest_sha256 = "";
            public bool trusted_toolchain_manifest_gate;
            public bool stage_receipt_contract_gate;
            public bool predecessor_receipt_chain_gate;
            public string predecessor_receipt_sha256 = "";
            public readonly List<StageReceiptEvidence> predecessor_receipts =
                new List<StageReceiptEvidence>();
            public bool backup_created;
            public bool backup_retained;
            public bool rollback_attempted;
            public bool rollback_succeeded;
            public bool rollback_blocked_by_process_gate;
            public string rollback_error = "";
            public int door_count;
            public double[] expected_slot_rows_mm = new double[0];
            public double[] expected_circle_rows_mm = new double[0];
            public int native_slot_edge_count_per_opening;
            public string reference_normalized_slot_fingerprint_sha256 = "";
            public double paired_circle_diameter_mm;
            public double paired_circle_offset_from_slot_mm;
            public TongueContract lock_tongue_validation = new TongueContract();
            public List<InventoryRow> original_inventory = new List<InventoryRow>();
            public List<InventoryRow> backup_inventory = new List<InventoryRow>();
            public List<InventoryRow> rollback_inventory = new List<InventoryRow>();
            public List<InventoryRow> post_inventory = new List<InventoryRow>();
            public int part_count;
            public int assembly_count;
            public int post_part_count;
            public int post_assembly_count;
            public string initial_inventory_digest = "";
            public string post_inventory_digest = "";
            public readonly List<string> added_files = new List<string>();
            public readonly List<string> deleted_files = new List<string>();
            public readonly List<string> changed_files = new List<string>();
            public bool post_inventory_gate;
            public string left_sha_before = "";
            public string right_sha_before = "";
            public string left_sha_after_save = "";
            public string right_sha_after_save = "";
            public string right_sha_after_commit = "";
            public bool right_replaced_transactionally;
            public string mirror_api = "";
            public int mirror_options;
            public bool mirror_break_link;
            public int temporary_right_external_reference_count = int.MaxValue;
            public double seed_translation_mm;
            public double[] slot_block_position_before_mm = new double[0];
            public double[] slot_block_position_after_mm = new double[0];
            public double[] circle_datum_before_mm = new double[0];
            public double[] circle_datum_after_request_mm = new double[0];
            public string[] pattern_seed_names = new string[0];
            public bool pattern_axis_selected;
            public int pattern_seed_selected_count;
            public double pattern_spacing_mm;
            public int pattern_instance_count;
            public bool pattern_reverse_direction;
            public GeometrySnapshot left_before = new GeometrySnapshot();
            public GeometrySnapshot left_seed_only_before_move = new GeometrySnapshot();
            public GeometrySnapshot left_seed_only_after_move = new GeometrySnapshot();
            public GeometrySnapshot left_after_pattern = new GeometrySnapshot();
            public GeometrySnapshot right_after_mirror = new GeometrySnapshot();
            public GeometrySnapshot temporary_right_reopen = new GeometrySnapshot();
            public GeometrySnapshot left_reopen = new GeometrySnapshot();
            public GeometrySnapshot right_reopen = new GeometrySnapshot();
            public bool left_save_attempted;
            public bool left_saved;
            public int left_save_errors;
            public int left_save_warnings;
            public bool right_save_attempted;
            public bool right_saved;
            public int right_save_errors;
            public int right_save_warnings;
            public readonly List<int> baseline_sldworks_process_ids = new List<int>();
            public readonly List<int> baseline_sldprocmon_process_ids = new List<int>();
            public readonly List<int> final_sldworks_process_ids = new List<int>();
            public readonly List<int> final_sldprocmon_process_ids = new List<int>();
            public readonly List<string> cleanup_errors = new List<string>();
            public readonly List<SessionRecord> sessions = new List<SessionRecord>();
            public bool final_process_cleanup_gate;
            public bool single_evidence_commit;
            public bool evidence_commit_verified;
            public string evidence_commitment_sha256 = "";
        }
    }
}
