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
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.FinalPack888x14Native
{
    internal static class FinalPack888x14Native
    {
        private const string ToolId = "native_final_pack_888x14_v1";
        private const string ExecutionPhase = "final_pack_and_relocated_reopen";
        private const string Purpose = "structure_engineering_assistance";
        private const string RecipeId = "winnsen-16029-888w-14door-native-v1";
        private const string RecipeDigest =
            "f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8";
        private const string ExpectedSourceSha256 = "0915F7D4880D2B8CE1471D3F3B02CF9FED135BD7ACD8F63E39CB69ADF6D44895";
        private const string ExpectedContractSnapshotSha256 = "AADC2C298E6B62396A0E4047B505F1E3533CA47FB1B149495D322500DA1AB278";
        private const string PlanSchema = "winnsen.native_build_plan.v1";
        private const string AuthorizationSchema = "winnsen.native_execution_authorization.v1";
        private const string ContractSchema = "winnsen.native_16029_assembly_contract.v1";
        private const string ResultSchema = "winnsen.16029.native_final_pack_result.v1";
        private const string ReceiptSchema = "winnsen.16029.native_final_pack_receipt.v1";
        private const string RootReceiptSchema = "winnsen.16029.native_root_assembly_receipt.v1";
        private const string ToolchainManifestSchema = "winnsen.16029.native_toolchain_manifest.v1";
        private const string LockCompositeToolchainManifestSchema =
            "winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1";
        private const string LockToolId = "native_lock_topology_888x14_v1";
        private const string WorkingRootFileName = "标准寄存柜1917×760×550(总装配).SLDASM";
        private const string FinalRootFileName = "标准寄存柜1917×888×550(总装配).SLDASM";
        private const string FinalPackageLeaf = "final_native_package";
        private const string EvidenceRelativePath = "evidence/final_pack_and_relocated_reopen.result.v1.json";
        private const string ReceiptRelativePath = "receipts/final_pack_and_relocated_reopen.json";
        private const string RootReceiptRelativePath = "receipts/root_assembly_888x14.json";
        private const string RootEvidenceRelativePath = "evidence/root_assembly_888x14.result.v1.json";
        private const string AttemptsRootEnvironment = "WINNSEN_NATIVE_16029_ATTEMPTS_ROOT";
        private const string PlanShaEnvironment = "WINNSEN_NATIVE_16029_PLAN_SHA256";
        private const string AuthorizationShaEnvironment = "WINNSEN_NATIVE_EXECUTION_AUTH_SHA256";
        private const string ExpectedSolidWorksExePath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string ExpectedSolidWorksExeSha256 =
            "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string ExpectedSolidWorksVersionPrefix = "28.";
        private const string ExpectedMonitorExePath = @"D:\soildworks2020\SOLIDWORKS\sldProcMon.exe";
        private const string ExpectedMonitorExeSha256 =
            "A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70";

        private static readonly string[] ToolchainKeys =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1",
            "native_door_module_888x14_v1", "native_lock_topology_888x14_v1",
            "native_root_assembly_888x14_v1", ToolId
        };

        private static readonly string[] StageReceiptOrder =
        {
            "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14",
            ExecutionPhase
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
            RootReceiptSchema,
            ReceiptSchema
        };

        private static readonly string[] StageReceiptPaths =
        {
            "receipts/clone_native_seed.json", "evidence/width-dimensions.receipt.json",
            "evidence/width-derived.receipt.json", "evidence/width-base-hole.receipt.json",
            "evidence/width-assemblies.receipt.json", "receipts/door_module_888x14.json",
            "receipts/lock_topology_888x14.json", RootReceiptRelativePath,
            ReceiptRelativePath
        };

        private static readonly string[] StageReceiptTools =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1", "native_width_888_v1",
            "native_width_888_v1", "native_width_888_v1", "native_door_module_888x14_v1",
            "native_lock_topology_888x14_v1", "native_root_assembly_888x14_v1", ToolId
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

        private static readonly string[] CommitmentExcludedKeys =
        {
            "evidence_write_attempted", "evidence_write_succeeded",
            "evidence_finalize_write_succeeded", "evidence_finalize_error",
            "failure_evidence_write_succeeded", "phase_receipt_path",
            "phase_receipt_sha256", "phase_receipt_committed", "authorization_checkpoints",
            "completed_at_utc", "commit_completed_at_utc", "evidence_commitment_sha256",
            "backup_deleted", "backup_delete_error"
        };

        private static FileStream attemptLock;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && (Same(args[0], "--help") || Same(args[0], "-h")))
            {
                PrintUsage();
                return 0;
            }
            if (args.Length == 1 && Same(args[0], "--identity"))
            {
                PrintIdentity();
                return 0;
            }
            if (args.Length == 1 && Same(args[0], "--static-self-test"))
                return RunStaticSelfTests();

            CliOptions options;
            string cliError;
            if (!TryParseCli(args, out options, out cliError))
            {
                Console.Error.WriteLine(cliError);
                PrintUsage();
                return 2;
            }

            RunResult result = NewResult(options);
            int exitCode = 1;
            try
            {
                ValidatePreflight(result, options);
                AcquireAttemptLock(result);
                AssertAuthorizationStillValid(result, "before_transaction");
                CreateCompleteInputBackup(result);
                result.transaction.mutationStarted = true;
                RunDedicatedPackAndGo(result);
                VerifyFinalPackage(result);
                VerifyRelocatedCopy(result);
                CleanupOwnedProcesses(result);
                CaptureFinalProcessGate(result);
                Require(result.processes.finalGate, "FINAL_CAD_PROCESS_GATE_FAILED", 40,
                    "owned SolidWorks 2020 and monitor processes must be gone before commit");
                AuditInputUnchanged(result);
                DeleteBackupBeforeCommit(result);
                AssertAuthorizationStillValid(result, "before_evidence_commit");
                result.completed_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                Require(CompletionWithinAuthorization(result), "AUTHORIZATION_EXPIRED_BEFORE_COMMIT", 41,
                    "completion time exceeds authorization or task lease");
                result.success = true;
                result.status = "STRUCTURE_ENGINEERING_ASSISTANCE_FINAL_PACK_PASS";
                result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                WriteJsonAtomicNew(result.evidencePath, result);
                result.transaction.evidenceCommitted = true;
                result.evidenceSha256 = Sha256(result.evidencePath);
                ValidateCommittedEvidence(result);
                AssertAuthorizationStillValid(result, "before_receipt_commit");
                WriteFinalReceiptAtomic(result);
                result.transaction.receiptCommitted = true;
                AssertAuthorizationStillValid(result, "after_receipt_commit");
                exitCode = 0;
            }
            catch (StageException ex)
            {
                result.status = ex.Status;
                result.errorCode = ex.Status;
                result.error = ex.Message;
                result.exitCode = ex.ExitCode;
            }
            catch (Exception ex)
            {
                result.status = "STRUCTURE_ENGINEERING_ASSISTANCE_FINAL_PACK_FAILED";
                result.errorCode = "UNHANDLED_FAILURE";
                result.error = SafeException(ex);
                result.exitCode = 1;
            }
            finally
            {
                CleanupOwnedProcesses(result);
                CaptureFinalProcessGate(result);
                if (exitCode != 0)
                {
                    result.success = false;
                    if (RollbackOwnsArtifact(result.transaction.receiptCommitted))
                        RemoveOrQuarantineUnpairedArtifact(result, result.receiptPath,
                            "final-receipt");
                    if (RollbackOwnsArtifact(result.transaction.evidenceCommitted))
                        RemoveOrQuarantineUnpairedArtifact(result, result.evidencePath,
                            "final-evidence");
                    if (result.processes.finalGate) RollbackAllWrites(result);
                    else
                    {
                        result.transaction.rollbackBlockedByProcessGate = true;
                        result.transaction.rollbackError =
                            "rollback refused because exact CAD process exit was not proven";
                    }
                    Console.Error.WriteLine(Serialize(new
                    {
                        status = result.status,
                        errorCode = result.errorCode,
                        error = result.error,
                        rollbackCompleted = result.transaction.rollbackCompleted,
                        rollbackError = result.transaction.rollbackError
                    }));
                }
                if (attemptLock != null)
                {
                    attemptLock.Dispose();
                    attemptLock = null;
                }
            }

            if (exitCode == 0)
                Console.WriteLine(Serialize(new
                {
                    status = result.status,
                    purpose = Purpose,
                    evidencePath = result.evidencePath,
                    receiptPath = result.receiptPath,
                    packagePath = result.finalPackagePath,
                    engineerReviewRequired = true
                }));
            return exitCode == 0 ? 0 : (result.exitCode > 0 ? result.exitCode : 1);
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(
                "Usage: FinalPack888x14Native.exe --attempt <attempt-dir> --plan <attempt-dir\\native_build_plan.json> --authorization <attempt-dir\\execution_authorizations\\native_final_pack_888x14_v1.json> --confirm-task <task-id>");
            Console.Error.WriteLine("purpose=structure_engineering_assistance; output is fixed under the attempt; engineer review is required");
        }

        private static void PrintIdentity()
        {
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            string snapshot = Path.Combine(Path.GetDirectoryName(executable),
                "assembly_contract.snapshot.json");
            Console.WriteLine("toolId=" + ToolId);
            Console.WriteLine("phase=" + ExecutionPhase);
            Console.WriteLine("purpose=" + Purpose);
            Console.WriteLine("recipeId=" + RecipeId);
            Console.WriteLine("recipeDigest=" + RecipeDigest);
            Console.WriteLine("sourceNormalizedSha256=" + ExpectedSourceSha256);
            Console.WriteLine("executableSha256=" + Sha256(executable));
            Console.WriteLine("contractSnapshotSha256=" +
                (File.Exists(snapshot) ? Sha256(snapshot) : "MISSING"));
        }

        private static bool TryParseCli(string[] args, out CliOptions options, out string error)
        {
            options = new CliOptions();
            error = "";
            var allowed = new HashSet<string>(new[]
            {
                "--attempt", "--plan", "--authorization", "--confirm-task"
            }, StringComparer.OrdinalIgnoreCase);
            if (args.Length != 8)
            {
                error = args.Any(value => value.StartsWith("--", StringComparison.Ordinal) &&
                    !allowed.Contains(value)) ? "unknown argument" : "exact four CLI bindings are required";
                return false;
            }
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < args.Length; index += 2)
            {
                string key = args[index];
                if (!allowed.Contains(key)) { error = "unknown argument: " + key; return false; }
                if (values.ContainsKey(key)) { error = "duplicate argument: " + key; return false; }
                string value = args[index + 1];
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
                { error = "argument value missing: " + key; return false; }
                values.Add(key, value);
            }
            if (!allowed.All(values.ContainsKey)) { error = "exact four CLI bindings are required"; return false; }
            options.attempt = FullPathOrEmpty(values["--attempt"]);
            options.plan = FullPathOrEmpty(values["--plan"]);
            options.authorization = FullPathOrEmpty(values["--authorization"]);
            options.confirmTask = values["--confirm-task"].Trim();
            if (string.IsNullOrWhiteSpace(options.attempt) || string.IsNullOrWhiteSpace(options.plan) ||
                string.IsNullOrWhiteSpace(options.authorization) ||
                string.IsNullOrWhiteSpace(options.confirmTask))
            { error = "CLI path or task binding is invalid"; return false; }
            return true;
        }

        private static int RunStaticSelfTests()
        {
            var checks = new List<SelfTestRow>();
            Action<string, bool, string> add = (name, passed, detail) => checks.Add(
                new SelfTestRow { name = name, passed = passed, detail = detail });

            var fixture = new Dictionary<string, object>(StringComparer.Ordinal)
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
            add("commitment_fixture_684d", EvidenceCommitmentDigest(fixture) ==
                "684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1",
                EvidenceCommitmentDigest(fixture));

            var numberFixture = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "a", 0.1592142857142857 }, { "b", -1.4150714285714288 },
                { "c", 0.0000001 }, { "d", 0.000001 }, { "e", 1e20 }, { "f", 1e21 },
                { "g", 254.4285714285714 }, { "ticks", 638906112000000000L },
                { "html", "<structure>&engineering" }
            };
            add("commitment_fixture_3bc6", EvidenceCommitmentDigest(numberFixture) ==
                "3BC67213D48D35E839E000955BD2B60A862C13A1EA0DBC0BE4ACBEAD12035C65",
                EvidenceCommitmentDigest(numberFixture));

            var receipt = RuntimeReceiptKeys.ToDictionary(key => key, key => (object)"",
                StringComparer.Ordinal);
            add("exact_runtime_receipt_keys", RuntimeReceiptKeys.Length == 28 &&
                ExactKeys(receipt, RuntimeReceiptKeys), receipt.Count.ToString(CultureInfo.InvariantCulture));
            receipt["unexpected"] = true;
            add("rejects_runtime_receipt_extra_key", !ExactKeys(receipt, RuntimeReceiptKeys),
                receipt.Count.ToString(CultureInfo.InvariantCulture));

            add("exact_nine_stage_contract", StageReceiptOrder.Length == 9 &&
                StageReceiptSchemas.Length == 9 && StageReceiptPaths.Length == 9 &&
                StageReceiptTools.Length == 9 && StageReceiptPredecessors.Length == 9 &&
                StageReceiptOrder[8] == ExecutionPhase && StageReceiptTools[8] == ToolId,
                string.Join("|", StageReceiptOrder));
            add("rejects_forged_final_receipt_path",
                IsCanonicalReceiptRelativePath(ReceiptRelativePath) &&
                !IsCanonicalReceiptRelativePath("receipts/../forged.json") &&
                !IsCanonicalReceiptRelativePath("receipts\\forged.json"), ReceiptRelativePath);

            string fakeAttempt = Path.Combine(@"C:\attempts", "TASK-1", "attempt-0002");
            add("fixed_output_path_is_attempt_derived",
                SamePath(FinalPackagePath(fakeAttempt), Path.Combine(fakeAttempt, FinalPackageLeaf)),
                FinalPackagePath(fakeAttempt));
            add("rejects_nonempty_final_output", !OutputDirectoryAcceptable(new[] { "x" }) &&
                OutputDirectoryAcceptable(new string[0]), "empty-only");
            add("strict_root_warning_32_rejects_96", StrictRootOpen(0, 32) &&
                !StrictRootOpen(0, 96) && !StrictRootOpen(1, 32), "0/32-only");
            add("exact_working_inventory_88_26_62", InventoryCountsExact(88, 26, 62, true),
                "88=26+62");
            add("exact_final_closure_55_14_41", ClosureCountsExact(55, 14, 41, 0, 0),
                "55=14+41");
            add("exact_root_topology_41_294_294_0", TopologyCountsExact(41, 294, 294, 0),
                "41/294/294/0");
            add("exact_module_counts_14_12_12_14", ModuleCountsExact(14, 12, 12, 14),
                "14/12/12/14");

            var noPermission = new Dictionary<string, object>(StringComparer.Ordinal);
            var permission = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "featureHealthPolicy", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "mode", "exact_unique_known_issue_only" }, { "maximumIssueCount", 1 },
                        { "allowedIssue", new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "name", "箱体右侧板焊接-1" }, { "type", "Reference" },
                                { "errorCode", 51 }, { "errorCode2", 51 }, { "warning", true }
                            } }
                    } }
            };
            var issue = new FeatureIssueEvidence { name = "箱体右侧板焊接-1", type = "Reference",
                errorCode = 51, errorCode2 = 51, warning = true };
            Dictionary<string, object> sharedContract = null;
            string sharedContractPath = Path.Combine(Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName), "assembly_contract.snapshot.json");
            try { sharedContract = ReadJsonObject(sharedContractPath); }
            catch { sharedContract = null; }
            Dictionary<string, object> sharedAllowedIssue;
            add("shared_snapshot_feature_policy_exact", sharedContract != null &&
                string.Equals(Sha256(sharedContractPath), ExpectedContractSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactKnownFeatureHealthPolicy(sharedContract, out sharedAllowedIssue),
                sharedContractPath);
            add("requires_valid_feature_health_policy_for_zero_issues",
                !FeatureIssuesAllowed(new FeatureIssueEvidence[0], noPermission) &&
                FeatureIssuesAllowed(new FeatureIssueEvidence[0], sharedContract), "policy-required");
            add("accepts_only_trusted_reference51_tuple",
                FeatureIssuesAllowed(new[] { issue }, sharedContract), "exact trusted tuple");

            var extraTopLevelPolicy = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "featureHealthPolicy", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "mode", "exact_unique_known_issue_only" }, { "maximumIssueCount", 1 },
                        { "allowedIssue", new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "name", "箱体右侧板焊接-1" }, { "type", "Reference" },
                                { "errorCode", 51 }, { "errorCode2", 51 }, { "warning", true }
                            } }, { "unexpected", true }
                    } }
            };
            add("rejects_policy_extra_top_level_key",
                !FeatureIssuesAllowed(new[] { issue }, extraTopLevelPolicy), "policy extra key");

            var extraNestedPolicy = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "featureHealthPolicy", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "mode", "exact_unique_known_issue_only" }, { "maximumIssueCount", 1 },
                        { "allowedIssue", new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { "name", "箱体右侧板焊接-1" }, { "type", "Reference" },
                                { "errorCode", 51 }, { "errorCode2", 51 }, { "warning", true },
                                { "unexpected", true }
                            } }
                    } }
            };
            add("rejects_policy_extra_nested_key",
                !FeatureIssuesAllowed(new[] { issue }, extraNestedPolicy), "allowed issue extra key");

            issue.type = "reference";
            add("rejects_other_reference51_tuple",
                !FeatureIssuesAllowed(new[] { issue }, permission), "case-sensitive tuple mismatch");
            issue.type = "Reference";
            add("rejects_second_feature_issue",
                !FeatureIssuesAllowed(new[] { issue, new FeatureIssueEvidence
                { name = "forged", type = "Reference", errorCode = 51, errorCode2 = 51,
                    warning = true } }, permission), "second issue negative control");
            add("rejects_forbidden_old_reference", HasForbiddenReference(
                new[] { "old-door.SLDPRT" }, new[] { "old-door.SLDPRT" }) &&
                !HasForbiddenReference(new[] { FinalRootFileName }, new[] { "old-door.SLDPRT" }),
                "exact-leaf-match");
            add("all_pack_statuses_must_be_zero", StatusesAllZero(new[] { 0, 0, 0 }) &&
                !StatusesAllZero(new[] { 0, 1, 0 }), "all-zero");
            add("authorization_phase_is_exact",
                StringArrayEquals(new object[] { ExecutionPhase }, new[] { ExecutionPhase }) &&
                !StringArrayEquals(new object[] { ExecutionPhase, "extra" }, new[] { ExecutionPhase }),
                ExecutionPhase);
            add("six_manifest_keys_are_exact", ToolchainKeys.Length == 6 &&
                new HashSet<string>(ToolchainKeys, StringComparer.Ordinal).Count == 6 &&
                ToolchainKeys[5] == ToolId, string.Join("|", ToolchainKeys));

            string repositoryRoot = FindRepositoryRoot(
                Process.GetCurrentProcess().MainModule.FileName);
            string lockManifestPath = Path.Combine(repositoryRoot,
                "workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json"
                    .Replace('/', '\\'));
            try
            {
                Dictionary<string, object> lockDocument = ReadJsonObject(lockManifestPath);
                add("lock_composite_manifest_exact_live_contract",
                    ValidateLockCompositeToolchainManifest(lockDocument, lockManifestPath,
                        repositoryRoot), "exact live lock composite manifest");

                lockDocument["unexpected"] = true;
                add("rejects_lock_composite_manifest_extra_key",
                    !ValidateLockCompositeToolchainManifest(lockDocument, lockManifestPath,
                        repositoryRoot), "extra top-level key");

                lockDocument = ReadJsonObject(lockManifestPath);
                Dictionary<string, object> lockTools = DictionaryValue(lockDocument, "tools");
                Dictionary<string, object> buildTool = DictionaryValue(lockTools, LockToolId);
                buildTool["sourcePath"] = Path.Combine(repositoryRoot, "AGENTS.md");
                add("rejects_lock_composite_manifest_wrong_fixed_path",
                    !ValidateLockCompositeToolchainManifest(lockDocument, lockManifestPath,
                        repositoryRoot), "wrong but repository-local path");

                lockDocument = ReadJsonObject(lockManifestPath);
                DictionaryValue(lockDocument, "validator")["sha256"] =
                    new string('0', 64);
                add("rejects_lock_composite_manifest_hash_drift",
                    !ValidateLockCompositeToolchainManifest(lockDocument, lockManifestPath,
                        repositoryRoot), "validator hash drift");
            }
            catch (Exception ex)
            {
                add("lock_composite_manifest_exact_live_contract", false, ex.Message);
                add("rejects_lock_composite_manifest_extra_key", false, ex.Message);
                add("rejects_lock_composite_manifest_wrong_fixed_path", false, ex.Message);
                add("rejects_lock_composite_manifest_hash_drift", false, ex.Message);
            }

            string identityMaskFixture =
                "private const string ExpectedSourceSha256=\"" + new string('A', 64) +
                "\";\r\nprivate const string ExpectedContractSnapshotSha256 = \"" +
                new string('B', 64) + "\";\r\n";
            string normalizedIdentityMaskFixture = NormalizeIdentitySource(identityMaskFixture);
            add("source_normalization_masks_source_and_contract_constants",
                normalizedIdentityMaskFixture ==
                    "private const string ExpectedSourceSha256=\"__SOURCE_SHA256__\";\n" +
                    "private const string ExpectedContractSnapshotSha256 = " +
                    "\"__CONTRACT_SHA256__\";\n",
                normalizedIdentityMaskFixture);
            add("monitor_parent_argument_is_exact", Regex.IsMatch("--ppid=1234",
                ExactParentPattern(1234), RegexOptions.CultureInvariant) &&
                !Regex.IsMatch("--ppid=12345", ExactParentPattern(1234),
                    RegexOptions.CultureInvariant), ExactParentPattern(1234));
            add("rollback_scope_must_stay_under_attempt",
                IsUnder(Path.Combine(fakeAttempt, FinalPackageLeaf), fakeAttempt) &&
                !IsUnder(Path.GetDirectoryName(fakeAttempt), fakeAttempt), fakeAttempt);
            Dictionary<string, object> evidenceProbe = BuildEvidence(new RunResult());
            add("success_evidence_omits_receipt_identity",
                !evidenceProbe.ContainsKey("phase_receipt_path") &&
                !evidenceProbe.ContainsKey("phase_receipt_sha256") &&
                !evidenceProbe.ContainsKey("phase_receipt_committed") &&
                !evidenceProbe.ContainsKey("receiptPath"), "no-cycle-fields");
            add("partial_backup_cleanup_skips_full_inventory_restore",
                !RollbackBackupRequiresFullInventory(false, false) &&
                RollbackBackupRequiresFullInventory(true, true), "partial-delete-only");
            add("quarantine_artifact_blocks_rollback_completion",
                RollbackArtifactsGone(new bool[] { true, true }) &&
                !RollbackArtifactsGone(new bool[] { true, false }), "all-artifacts-gone");
            add("preexisting_artifact_is_never_rollback_owned",
                !RollbackOwnsArtifact(false) && RollbackOwnsArtifact(true),
                "created-by-current-transaction-only");
            string ownershipProbe = Path.Combine(Path.GetTempPath(),
                "winnsen-final-pack-owned-" + Guid.NewGuid().ToString("N") + ".json");
            bool ownershipMarked = false;
            try
            {
                WriteJsonAtomicNew(ownershipProbe, new { probe = true }, () =>
                {
                    ownershipMarked = true;
                    throw new InvalidOperationException("probe-after-create");
                });
            }
            catch (InvalidOperationException) { }
            bool ownershipFileExists = File.Exists(ownershipProbe);
            if (ownershipFileExists) File.Delete(ownershipProbe);
            add("atomic_create_marks_ownership_before_later_failure",
                ownershipMarked && ownershipFileExists && !File.Exists(ownershipProbe),
                "CreateNew-before-write-verify");

            int failed = checks.Count(row => !row.passed);
            Console.WriteLine(Serialize(new
            {
                status = failed == 0 ? "PASS" : "FAIL",
                checksTotal = checks.Count,
                checksFailed = failed,
                cadStarted = false,
                checks
            }));
            return failed == 0 ? 0 : 1;
        }

        private static bool StrictRootOpen(int openErrors, int openWarnings)
        {
            return openErrors == 0 && openWarnings == 32;
        }

        private static bool InventoryCountsExact(int cadFileCount, int assemblyFileCount,
            int partFileCount, bool flat)
        {
            return flat && cadFileCount == 88 && assemblyFileCount == 26 && partFileCount == 62 &&
                assemblyFileCount + partFileCount == cadFileCount;
        }

        private static bool ClosureCountsExact(int cadFileCount, int assemblyFileCount,
            int partFileCount, int missingFileCount, int externalFileCount)
        {
            return cadFileCount == 55 && assemblyFileCount == 14 && partFileCount == 41 &&
                missingFileCount == 0 && externalFileCount == 0 &&
                assemblyFileCount + partFileCount == cadFileCount;
        }

        private static bool TopologyCountsExact(int topLevelComponentCount,
            int recursiveComponentCount, int activeRecursiveComponentCount,
            int suppressedComponentCount)
        {
            return topLevelComponentCount == 41 && recursiveComponentCount == 294 &&
                activeRecursiveComponentCount == 294 && suppressedComponentCount == 0;
        }

        private static bool ModuleCountsExact(int doorModuleCount, int shelfModuleCount,
            int activeFrameCrossbarCount, int mechanicalTongueCount)
        {
            return doorModuleCount == 14 && shelfModuleCount == 12 &&
                activeFrameCrossbarCount == 12 && mechanicalTongueCount == 14;
        }

        private static bool StatusesAllZero(IEnumerable<int> statuses)
        {
            return statuses != null && statuses.Any() && statuses.All(value => value == 0);
        }

        private static bool OutputDirectoryAcceptable(IEnumerable<string> entries)
        {
            return entries != null && !entries.Any();
        }

        private static string FinalPackagePath(string attemptDirectory)
        {
            return Path.Combine(Path.GetFullPath(attemptDirectory), FinalPackageLeaf);
        }

        private static bool HasForbiddenReference(IEnumerable<string> leaves,
            IEnumerable<string> forbiddenLeaves)
        {
            var forbidden = new HashSet<string>(forbiddenLeaves ?? new string[0],
                StringComparer.OrdinalIgnoreCase);
            return (leaves ?? new string[0]).Any(forbidden.Contains);
        }

        private static bool IsCanonicalReceiptRelativePath(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !value.Contains("\\") &&
                !value.Contains("..") && Regex.IsMatch(value,
                    "^(receipts|evidence)/[A-Za-z0-9_.-]+\\.json$",
                    RegexOptions.CultureInvariant);
        }

        private static bool ExactKeys(Dictionary<string, object> value, IEnumerable<string> expected)
        {
            return value != null && value.Keys.OrderBy(key => key, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(key => key, StringComparer.Ordinal),
                    StringComparer.Ordinal);
        }

        private static RunResult NewResult(CliOptions options)
        {
            string attempt = options.attempt;
            return new RunResult
            {
                schema = ResultSchema,
                purpose = Purpose,
                status = "NOT_STARTED",
                success = false,
                engineerReviewRequired = true,
                attemptDir = attempt,
                planPath = options.plan,
                authorizationPath = options.authorization,
                workingPackPath = Path.Combine(attempt, "native_cad", "working_pack"),
                finalPackagePath = FinalPackagePath(attempt),
                relocatedProbePath = Path.Combine(attempt, ".final-pack-relocated-probe"),
                evidencePath = Path.Combine(attempt, EvidenceRelativePath.Replace('/', '\\')),
                receiptPath = Path.Combine(attempt, ReceiptRelativePath.Replace('/', '\\')),
                rootReceiptPath = Path.Combine(attempt, RootReceiptRelativePath.Replace('/', '\\')),
                rootEvidencePath = Path.Combine(attempt, RootEvidenceRelativePath.Replace('/', '\\')),
                tool = new ToolEvidence
                {
                    id = ToolId,
                    sourceNormalizedSha256 = NormalizedSourceSha256(),
                    executableSha256 = Sha256(Process.GetCurrentProcess().MainModule.FileName)
                },
                quality_boundary = new QualityBoundaryEvidence
                {
                    purpose = Purpose,
                    engineerReviewRequired = true
                }
            };
        }

        private static void ValidatePreflight(RunResult result, CliOptions options)
        {
            Require(Directory.Exists(result.attemptDir), "ATTEMPT_MISSING", 3,
                "attempt directory does not exist");
            string attemptsRoot = FullPathOrEmpty(
                System.Environment.GetEnvironmentVariable(AttemptsRootEnvironment));
            Require(!string.IsNullOrWhiteSpace(attemptsRoot) && Directory.Exists(attemptsRoot),
                "ATTEMPTS_ROOT_MISSING", 3, AttemptsRootEnvironment + " is missing or invalid");
            result.attemptsRoot = attemptsRoot.TrimEnd('\\', '/');
            string relativeAttempt = RelativeUnder(result.attemptDir, result.attemptsRoot,
                "ATTEMPT_OUTSIDE_ATTEMPTS_ROOT");
            string[] segments = relativeAttempt.Split(new[] { '\\', '/' },
                StringSplitOptions.RemoveEmptyEntries);
            Require(segments.Length == 2 && IsSafeId(segments[0]) &&
                Regex.IsMatch(segments[1], "^attempt-[0-9]{4}$", RegexOptions.CultureInvariant),
                "ATTEMPT_SHAPE_INVALID", 3,
                "attempt must be <attempts-root>/<task-id>/attempt-NNNN");
            result.taskId = segments[0];
            result.attempt = int.Parse(segments[1].Substring(8), CultureInfo.InvariantCulture);
            Require(string.Equals(options.confirmTask, result.taskId, StringComparison.Ordinal),
                "TASK_CONFIRMATION_MISMATCH", 3,
                "--confirm-task must exactly match the attempt task id");
            Require(SamePath(result.planPath, Path.Combine(result.attemptDir,
                    "native_build_plan.json")), "PLAN_PATH_INVALID", 3,
                "--plan must be the direct attempt native_build_plan.json");
            Require(SamePath(result.authorizationPath, Path.Combine(result.attemptDir,
                    "execution_authorizations", ToolId + ".json")),
                "AUTHORIZATION_PATH_INVALID", 3,
                "--authorization must be the fixed current-attempt final-pack authorization");
            Require(SamePath(result.workingPackPath, Path.Combine(result.attemptDir,
                    "native_cad", "working_pack")), "WORKING_PACK_PATH_INVALID", 3,
                "working pack must be the fixed attempt path");
            Require(!Directory.Exists(result.finalPackagePath) &&
                !File.Exists(result.finalPackagePath), "FINAL_PACKAGE_MUST_BE_ABSENT", 3,
                "final_native_package must not exist before this immutable transaction");
            Require(!Directory.Exists(result.relocatedProbePath) &&
                !File.Exists(result.relocatedProbePath), "RELOCATED_PROBE_MUST_BE_ABSENT", 3,
                "relocated probe path must not exist before this transaction");
            Require(!File.Exists(result.evidencePath) && !File.Exists(result.receiptPath),
                "FINAL_ARTIFACT_ALREADY_EXISTS", 3,
                "final evidence and receipt are immutable and must not pre-exist");
            Require(File.Exists(result.rootReceiptPath) && File.Exists(result.rootEvidencePath),
                "ROOT_PREDECESSOR_MISSING", 3,
                "current root receipt and evidence are required");

            AssertPathChainNoReparse(result.attemptDir, result.attemptsRoot,
                "ATTEMPT_REPARSE_PATH");
            AssertPathChainNoReparse(result.workingPackPath, result.attemptDir,
                "WORKING_PACK_REPARSE_PATH");
            AssertPathChainNoReparse(result.planPath, result.attemptDir, "PLAN_REPARSE_PATH");
            AssertPathChainNoReparse(result.authorizationPath, result.attemptDir,
                "AUTHORIZATION_REPARSE_PATH");
            AssertPathChainNoReparse(result.rootReceiptPath, result.attemptDir,
                "ROOT_RECEIPT_REPARSE_PATH");

            LoadAndValidateContract(result);
            LoadAndValidatePlan(result);
            CaptureInputInventory(result);
            ValidateRootReceipt(result);
            LoadAndValidateAuthorization(result);
            ValidateOwnToolIdentity(result);

            result.processes.baselineSldworks = ProcessIds("SLDWORKS");
            result.processes.baselineSldprocmon = ProcessIds("sldProcMon");
            Require(result.processes.baselineSldworks.Count == 0 &&
                result.processes.baselineSldprocmon.Count == 0,
                "DIRTY_CAD_PROCESS_BASELINE", 5,
                "SLDWORKS and sldProcMon must be fully closed before final packaging");
            result.preflightPassed = true;
        }

        private static void LoadAndValidateContract(RunResult result)
        {
            string path = Path.Combine(Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName), "assembly_contract.snapshot.json");
            Require(File.Exists(path) && !HasReparsePoint(path) && FileLinkCount(path) == 1,
                "ASSEMBLY_CONTRACT_INVALID", 3,
                "reviewed assembly contract snapshot must be a regular single-link file");
            Require(string.Equals(Sha256(path), ExpectedContractSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase), "ASSEMBLY_CONTRACT_SHA_MISMATCH", 3,
                "assembly contract snapshot changed after compilation");
            Dictionary<string, object> contract = ReadJsonObject(path);
            Dictionary<string, object> recipe = ChildObject(contract, "recipe");
            Dictionary<string, object> expectedWorking = ChildObject(contract,
                "expectedWorkingAssembly");
            Dictionary<string, object> expectedClosure = ChildObject(contract,
                "expectedFinalClosure");
            Dictionary<string, object> execution = ChildObject(contract, "executionBoundary");
            Require(Text(contract, "schema") == ContractSchema &&
                Text(contract, "purpose") == Purpose && Text(recipe, "id") == RecipeId &&
                Number(recipe, "version") == 1.0 &&
                string.Equals(Text(recipe, "digest"), RecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(contract, "workingRootFileName") == WorkingRootFileName &&
                Text(contract, "finalRootFileName") == FinalRootFileName &&
                Number(expectedWorking, "topLevelComponentCount") == 41 &&
                Number(expectedWorking, "recursiveComponentCount") == 294 &&
                Number(expectedWorking, "activeRecursiveComponentCount") == 294 &&
                Number(expectedWorking, "suppressedComponentCount") == 0 &&
                Number(expectedClosure, "cadFileCount") == 55 &&
                Number(expectedClosure, "assemblyFileCount") == 14 &&
                Number(expectedClosure, "partFileCount") == 41 &&
                Bool(execution, "planningOnly") &&
                Bool(execution, "finalPackAndRenameRequired") &&
                Bool(execution, "rebuildSaveFreshReadonlyReopenRequired") &&
                Bool(execution, "relocatedReopenRequired"),
                "ASSEMBLY_CONTRACT_CONTENT_MISMATCH", 3,
                "assembly contract identity or final packaging gates drifted");
            Require(ArrayValue(contract, "doors").Length == 14 &&
                ArrayValue(contract, "shelves").Length == 12 &&
                ArrayValue(contract, "frameCrossbars").Length == 12,
                "ASSEMBLY_CONTRACT_ROW_COUNT_MISMATCH", 3,
                "assembly contract must contain exact door, shelf and crossbar rows");
            result.contractPath = path;
            result.contractSnapshot = contract;
        }

        private static void LoadAndValidatePlan(RunResult result)
        {
            Require(File.Exists(result.planPath) && !HasReparsePoint(result.planPath) &&
                FileLinkCount(result.planPath) == 1, "NATIVE_BUILD_PLAN_INVALID", 3,
                "native build plan must be a regular single-link file");
            string actualSha = Sha256(result.planPath);
            string configuredSha = (System.Environment.GetEnvironmentVariable(
                PlanShaEnvironment) ?? "").Trim();
            Require(IsSha256(configuredSha) && string.Equals(actualSha, configuredSha,
                    StringComparison.OrdinalIgnoreCase), "NATIVE_BUILD_PLAN_SHA_MISMATCH", 3,
                "native build plan differs from the worker-provided SHA-256");
            Dictionary<string, object> plan = ReadJsonObject(result.planPath);
            Dictionary<string, object> task = ChildObject(plan, "task");
            Dictionary<string, object> request = ChildObject(plan, "request");
            Dictionary<string, object> recipe = ChildObject(plan, "recipe");
            Dictionary<string, object> quality = ChildObject(plan, "qualityBoundary");
            Dictionary<string, object> execution = ChildObject(plan, "executionBoundary");
            Require(Text(plan, "schema") == PlanSchema && Text(plan, "purpose") == Purpose &&
                Text(task, "id") == result.taskId && Number(task, "revisionAtPlanning") > 0 &&
                IsSha256(Text(task, "digest")) && IsSha256(Text(request, "digest")) &&
                !string.IsNullOrWhiteSpace(Text(request, "fingerprint")) &&
                Text(recipe, "id") == RecipeId && Number(recipe, "version") == 1 &&
                string.Equals(Text(recipe, "digest"), RecipeDigest,
                    StringComparison.OrdinalIgnoreCase) && Bool(quality, "planningOnly") &&
                !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses") &&
                !Bool(execution, "executorImplemented") && !Bool(execution, "cadStarted") &&
                !Bool(execution, "modelGenerated") && !Bool(execution, "modelReady") &&
                !Bool(execution, "legacyFallbackUsed"),
                "NATIVE_BUILD_PLAN_BOUNDARY_MISMATCH", 3,
                "plan must remain the exact planning-only structure engineering assistance plan");
            object embeddedContract;
            Require(plan.TryGetValue("assemblyContract", out embeddedContract) &&
                StableJson(embeddedContract) == StableJson(result.contractSnapshot),
                "PLAN_ASSEMBLY_CONTRACT_MISMATCH", 3,
                "plan assemblyContract differs from the reviewed snapshot");
            Require(StageContractPresent(plan, "relocate_reopen_and_package", ToolId),
                "PLAN_FINAL_STAGE_MISSING", 3,
                "plan must bind relocate_reopen_and_package to the dedicated final-pack tool");
            Require(StringArrayEquals(ArrayValue(plan, "requiredChecks"),
                ArrayValue(result.contractSnapshot, "requiredChecks").Select(ValueText).ToArray()),
                "PLAN_REQUIRED_CHECKS_MISMATCH", 3,
                "plan requiredChecks differ from the trusted assembly contract");

            result.plan = new PlanEvidence
            {
                path = result.planPath,
                sha256 = actualSha,
                workerId = Text(plan, "workerId"),
                taskRevision = Convert.ToInt32(Number(task, "revisionAtPlanning"),
                    CultureInfo.InvariantCulture),
                taskDigest = Text(task, "digest").ToUpperInvariant(),
                requestFingerprint = Text(request, "fingerprint"),
                requestDigest = Text(request, "digest").ToUpperInvariant(),
                raw = plan
            };
            ValidateStageReceiptContracts(plan);
            ValidateTrustedToolchainManifests(plan, result);
        }

        private static bool StageContractPresent(Dictionary<string, object> plan, string id,
            string toolId)
        {
            return ArrayValue(plan, "stageContracts").OfType<Dictionary<string, object>>()
                .Any(row => Text(row, "id") == id && Text(row, "toolId") == toolId);
        }

        private static void ValidateStageReceiptContracts(Dictionary<string, object> plan)
        {
            Dictionary<string, object> contracts = ChildObject(plan, "stageReceiptContracts");
            RequireExactKeys(contracts, "plan.stageReceiptContracts", StageReceiptOrder);
            for (int index = 0; index < StageReceiptOrder.Length; index++)
            {
                string id = StageReceiptOrder[index];
                Dictionary<string, object> row = contracts[id] as Dictionary<string, object>;
                RequireExactKeys(row, "plan.stageReceiptContracts." + id,
                    "schema", "path", "producerToolId", "predecessor");
                Require(Text(row, "schema") == StageReceiptSchemas[index] &&
                    Text(row, "path") == StageReceiptPaths[index] &&
                    Text(row, "producerToolId") == StageReceiptTools[index] &&
                    Text(row, "predecessor") == StageReceiptPredecessors[index] &&
                    IsCanonicalReceiptRelativePath(Text(row, "path")),
                    "STAGE_RECEIPT_CONTRACT_MISMATCH", 3,
                    "plan stage receipt contract differs from the shared nine-stage contract: " + id);
            }
        }

        private static void ValidateTrustedToolchainManifests(Dictionary<string, object> plan,
            RunResult result)
        {
            Dictionary<string, object> manifests = ChildObject(plan, "trustedToolchainManifests");
            RequireExactKeys(manifests, "plan.trustedToolchainManifests", ToolchainKeys);
            string repositoryRoot = FindRepositoryRoot(
                Process.GetCurrentProcess().MainModule.FileName);
            Require(!string.IsNullOrWhiteSpace(repositoryRoot), "REPOSITORY_ROOT_NOT_FOUND", 3,
                "repository root is required for trusted toolchain validation");
            var paths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ToolchainKeys[0], "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json" },
                { ToolchainKeys[1], "workers/native_model_requests/development/v1/tools/width_888_native_v1/toolchain_manifest.json" },
                { ToolchainKeys[2], "workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/toolchain_manifest.json" },
                { ToolchainKeys[3], "workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json" },
                { ToolchainKeys[4], "workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/toolchain_manifest.json" },
                { ToolchainKeys[5], "workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/toolchain_manifest.json" }
            };
            foreach (string key in ToolchainKeys)
            {
                string expectedSha = ValueText(manifests[key]).ToUpperInvariant();
                string path = Path.GetFullPath(Path.Combine(repositoryRoot,
                    paths[key].Replace('/', '\\')));
                Require(IsSha256(expectedSha) && File.Exists(path) && !HasReparsePoint(path) &&
                    FileLinkCount(path) == 1 && string.Equals(Sha256(path), expectedSha,
                        StringComparison.OrdinalIgnoreCase),
                    "TRUSTED_TOOLCHAIN_MANIFEST_HASH_MISMATCH", 3,
                    "trusted toolchain manifest is missing or changed: " + key);
                Dictionary<string, object> document = ReadJsonObject(path);
                if (string.Equals(key, LockToolId, StringComparison.Ordinal))
                {
                    Require(ValidateLockCompositeToolchainManifest(document, path,
                            repositoryRoot),
                        "TRUSTED_LOCK_COMPOSITE_MANIFEST_CONTENT_INVALID", 3,
                        "lock toolchain manifest must be the exact reviewed three-tool live binding");
                    Dictionary<string, object> lockBuild = DictionaryValue(
                        DictionaryValue(document, "tools"), LockToolId);
                    result.toolchainManifests.Add(new TrustedArtifactEvidence
                    {
                        id = key, path = path, sha256 = expectedSha,
                        sourceNormalizedSha256 = Text(lockBuild, "sourceNormalizedSha256"),
                        executableSha256 = Text(lockBuild, "executableSha256")
                    });
                    continue;
                }
                Dictionary<string, object> tool = ChildObject(document, "tool");
                RequireExactKeys(document, "toolchain manifest " + key,
                    "schema", "generatedBy", "tool");
                RequireExactKeys(tool, "toolchain manifest " + key + ".tool", "id",
                    "sourcePath", "sourceNormalizedSha256", "executablePath",
                    "executableSha256", "verifierPath", "verifierSha256");
                Require(Text(document, "schema") == ToolchainManifestSchema &&
                    Text(tool, "id") == key &&
                    LiveRepoRelativeManifestPath(tool, "sourcePath",
                        "sourceNormalizedSha256", true) &&
                    LiveRepoRelativeManifestPath(tool, "executablePath",
                        "executableSha256", false) &&
                    LiveRepoRelativeManifestPath(tool, "verifierPath",
                        "verifierSha256", false),
                    "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 3,
                    "trusted toolchain manifest content is invalid: " + key);
                result.toolchainManifests.Add(new TrustedArtifactEvidence
                {
                    id = key, path = path, sha256 = expectedSha,
                    sourceNormalizedSha256 = Text(tool, "sourceNormalizedSha256"),
                    executableSha256 = Text(tool, "executableSha256")
                });
            }
            TrustedArtifactEvidence own = result.toolchainManifests.Single(row => row.id == ToolId);
            Require(string.Equals(own.sourceNormalizedSha256,
                    result.tool.sourceNormalizedSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(own.executableSha256, result.tool.executableSha256,
                    StringComparison.OrdinalIgnoreCase),
                "FINAL_TOOLCHAIN_IDENTITY_MISMATCH", 3,
                "final-pack source or executable differs from its frozen manifest");
        }

        private static bool ValidateLockCompositeToolchainManifest(
            Dictionary<string, object> document, string manifestPath, string repositoryRoot)
        {
            if (!ExactKeys(document, new[] { "schema", "generatedBy", "tools", "validator" }) ||
                Text(document, "schema") != LockCompositeToolchainManifestSchema ||
                Text(document, "generatedBy") != "VerifyLockTopology888x14Static.mjs" ||
                !RepositoryFileIsSafe(manifestPath, repositoryRoot)) return false;
            Dictionary<string, object> tools = DictionaryValue(document, "tools");
            Dictionary<string, object> validator = DictionaryValue(document, "validator");
            if (!ExactKeys(tools, new[] { LockToolId, "native_lock_topology_inspector_v1",
                    "native_assembly_tongue_inspector_888x14_v1" }) ||
                !ExactKeys(validator, new[] { "path", "sha256" })) return false;

            string toolsRoot = Path.Combine(repositoryRoot,
                "workers/native_model_requests/development/v1/tools".Replace('/', '\\'));
            return LockCompositeToolEntryMatches(tools, LockToolId,
                    Path.Combine(toolsRoot, "BuildLockTopology888x14.cs"),
                    Path.Combine(toolsRoot, "bin", "BuildLockTopology888x14.exe"),
                    repositoryRoot) &&
                LockCompositeToolEntryMatches(tools, "native_lock_topology_inspector_v1",
                    Path.Combine(toolsRoot, "InspectLockTopology888x14.cs"),
                    Path.Combine(toolsRoot, "bin", "InspectLockTopology888x14.exe"),
                    repositoryRoot) &&
                LockCompositeToolEntryMatches(tools,
                    "native_assembly_tongue_inspector_888x14_v1",
                    Path.Combine(toolsRoot, "InspectAssemblyTongues888x14.cs"),
                    Path.Combine(toolsRoot, "bin", "InspectAssemblyTongues888x14.exe"),
                    repositoryRoot) &&
                ExactRepositoryArtifact(Text(validator, "path"),
                    Path.Combine(toolsRoot, "ValidateLockTopology888x14.mjs"),
                    Text(validator, "sha256"), false, repositoryRoot);
        }

        private static bool LockCompositeToolEntryMatches(
            Dictionary<string, object> tools, string id, string expectedSourcePath,
            string expectedExecutablePath, string repositoryRoot)
        {
            Dictionary<string, object> tool = DictionaryValue(tools, id);
            return ExactKeys(tool, new[] { "sourcePath", "sourceNormalizedSha256",
                    "executablePath", "executableSha256" }) &&
                ExactRepositoryArtifact(Text(tool, "sourcePath"), expectedSourcePath,
                    Text(tool, "sourceNormalizedSha256"), true, repositoryRoot) &&
                ExactRepositoryArtifact(Text(tool, "executablePath"), expectedExecutablePath,
                    Text(tool, "executableSha256"), false, repositoryRoot);
        }

        private static bool ExactRepositoryArtifact(string configuredPath,
            string expectedPath, string expectedSha256, bool normalizedLockSource,
            string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(configuredPath) ||
                !Path.IsPathRooted(configuredPath) || !SamePath(configuredPath, expectedPath) ||
                !IsSha256(expectedSha256) || !RepositoryFileIsSafe(expectedPath, repositoryRoot))
                return false;
            string actualSha256 = normalizedLockSource ?
                NormalizedLockToolSourceHashAt(expectedPath) : Sha256(expectedPath);
            return string.Equals(actualSha256, expectedSha256,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool RepositoryFileIsSafe(string path, string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(repositoryRoot))
                return false;
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(repositoryRoot);
            if (!IsUnder(full, root) || !File.Exists(full) || HasReparsePoint(full) ||
                FileLinkCount(full) != 1) return false;
            string current = full;
            for (int guard = 0; guard < 64; guard++)
            {
                if (HasReparsePoint(current)) return false;
                if (SamePath(current, root)) return true;
                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null || (!SamePath(parent.FullName, root) &&
                    !IsUnder(parent.FullName, root))) return false;
                current = parent.FullName;
            }
            return false;
        }

        private static Dictionary<string, object> DictionaryValue(
            Dictionary<string, object> parent, string key)
        {
            object value;
            return parent != null && parent.TryGetValue(key, out value) ?
                value as Dictionary<string, object> : null;
        }

        private static void CaptureInputInventory(RunResult result)
        {
            List<string> files = CaptureFlatCadTree(result.workingPackPath, 88,
                "WORKING_PACK_INVENTORY_INVALID");
            int assemblies = files.Count(IsAssembly);
            int parts = files.Count(IsPart);
            Require(InventoryCountsExact(files.Count, assemblies, parts, true),
                "WORKING_PACK_INVENTORY_COUNT_MISMATCH", 3,
                "root-stage working pack must be exact flat 88 = 26 SLDASM + 62 SLDPRT");
            string root = Path.Combine(result.workingPackPath, WorkingRootFileName);
            Require(File.Exists(root) && !HasReparsePoint(root) && FileLinkCount(root) == 1,
                "WORKING_ROOT_INVALID", 3,
                "working pack must contain the exact 760-named root from the root stage");
            result.workingRootPath = root;
            result.preInventory = InventoryEvidenceFor(files);
        }

        private static void ValidateRootReceipt(RunResult result)
        {
            Require(File.Exists(result.rootReceiptPath) &&
                !HasReparsePoint(result.rootReceiptPath) &&
                FileLinkCount(result.rootReceiptPath) == 1,
                "ROOT_RECEIPT_FILE_INVALID", 3,
                "root receipt must be a regular single-link current-attempt artifact");
            string receiptShaBefore = Sha256(result.rootReceiptPath);
            Dictionary<string, object> receipt = ReadJsonObject(result.rootReceiptPath);
            RequireExactKeys(receipt, "root receipt", RuntimeReceiptKeys);
            Require(Text(receipt, "schema") == RootReceiptSchema &&
                Text(receipt, "phase") == "root_assembly_888x14" && Bool(receipt, "success") &&
                Text(receipt, "taskId") == result.taskId &&
                Number(receipt, "taskRevision") == result.plan.taskRevision &&
                string.Equals(Text(receipt, "taskDigest"), result.plan.taskDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "requestDigest"), result.plan.requestDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Number(receipt, "attempt") == result.attempt &&
                string.Equals(Text(receipt, "planSha256"), result.plan.sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(receipt, "recipeId") == RecipeId &&
                string.Equals(Text(receipt, "recipeDigest"), RecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(receipt, "toolId") == "native_root_assembly_888x14_v1" &&
                IsSha256(Text(receipt, "preInventoryDigest")) &&
                string.Equals(Text(receipt, "postInventoryDigest"), result.preInventory.digest,
                    StringComparison.OrdinalIgnoreCase) &&
                IsSha256(Text(receipt, "predecessorReceiptSha256")),
                "ROOT_RECEIPT_BINDING_MISMATCH", 3,
                "root receipt does not bind the current plan, task and live working pack");

            TrustedArtifactEvidence rootTool = result.toolchainManifests.Single(row =>
                row.id == "native_root_assembly_888x14_v1");
            Require(string.Equals(Text(receipt, "toolSourceNormalizedSha256"),
                    rootTool.sourceNormalizedSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(receipt, "toolExecutableSha256"),
                    rootTool.executableSha256, StringComparison.OrdinalIgnoreCase),
                "ROOT_RECEIPT_TOOLCHAIN_MISMATCH", 3,
                "root receipt tool identity differs from the frozen manifest");

            string lockReceiptPath = Path.Combine(result.attemptDir,
                "receipts", "lock_topology_888x14.json");
            Require(File.Exists(lockReceiptPath) && !HasReparsePoint(lockReceiptPath) &&
                FileLinkCount(lockReceiptPath) == 1 &&
                string.Equals(Sha256(lockReceiptPath), Text(receipt,
                    "predecessorReceiptSha256"), StringComparison.OrdinalIgnoreCase),
                "ROOT_PREDECESSOR_RECEIPT_SHA_MISMATCH", 3,
                "root receipt predecessor must be the live lock receipt");
            Require(Text(receipt, "evidencePath") == RootEvidenceRelativePath &&
                File.Exists(result.rootEvidencePath) && !HasReparsePoint(result.rootEvidencePath) &&
                FileLinkCount(result.rootEvidencePath) == 1 &&
                string.Equals(Sha256(result.rootEvidencePath), Text(receipt, "evidenceSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "ROOT_EVIDENCE_SHA_MISMATCH", 3,
                "root receipt evidence must match the fixed live root evidence");
            Dictionary<string, object> evidence = ReadJsonObject(result.rootEvidencePath);
            string commitment = FirstNonEmpty(Text(evidence, "evidence_commitment_sha256"),
                Text(evidence, "evidenceCommitmentSha256"));
            string completed = FirstNonEmpty(Text(evidence, "completed_at_utc"),
                Text(evidence, "completedAtUtc"), Text(evidence, "completedAt"));
            Require(IsSha256(commitment) &&
                string.Equals(commitment, Text(receipt, "evidenceCommitmentSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(EvidenceCommitmentDigest(evidence), commitment,
                    StringComparison.OrdinalIgnoreCase) &&
                completed == Text(receipt, "completedAt"),
                "ROOT_EVIDENCE_COMMITMENT_MISMATCH", 3,
                "root evidence semantic commitment or completion time changed");
            ValidateHistoricalRootAuthorization(receipt, result);
            Require(string.Equals(Sha256(result.rootReceiptPath), receiptShaBefore,
                    StringComparison.OrdinalIgnoreCase), "ROOT_RECEIPT_CHANGED_DURING_VALIDATION", 3,
                "root receipt changed while being validated");
            result.predecessorReceiptSha256 = receiptShaBefore;
        }

        private static void ValidateHistoricalRootAuthorization(Dictionary<string, object> receipt,
            RunResult result)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(Text(receipt, "authorizationJsonBase64")); }
            catch { throw new StageException("ROOT_AUTHORIZATION_BASE64_INVALID", 3,
                "root receipt authorizationJsonBase64 is invalid"); }
            Require(string.Equals(Sha256Bytes(bytes), Text(receipt, "authorizationSha256"),
                    StringComparison.OrdinalIgnoreCase), "ROOT_AUTHORIZATION_SHA_MISMATCH", 3,
                "root receipt embedded authorization bytes do not match");
            Dictionary<string, object> authorization;
            try
            {
                authorization = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                    RecursionLimit = 512 }.DeserializeObject(Encoding.UTF8.GetString(bytes))
                    as Dictionary<string, object>;
            }
            catch { authorization = null; }
            Require(authorization != null, "ROOT_AUTHORIZATION_JSON_INVALID", 3,
                "root receipt embedded authorization is not a JSON object");
            ValidateExactAuthorizationKeys(authorization);
            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> request = ChildObject(authorization, "request");
            Dictionary<string, object> plan = ChildObject(authorization, "plan");
            Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> seed = ChildObject(authorization, "seed");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");
            DateTime issued, expires, leaseExpires, completed;
            Require(ParseUtc(Text(authorization, "issuedAt"), out issued) &&
                ParseUtc(Text(authorization, "expiresAt"), out expires) &&
                ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpires) &&
                ParseUtc(Text(receipt, "completedAt"), out completed) &&
                issued <= completed && completed <= expires && completed <= leaseExpires &&
                expires > issued && (expires - issued).TotalMinutes <= 30.0,
                "ROOT_AUTHORIZATION_HISTORICAL_WINDOW_INVALID", 3,
                "root stage did not complete inside its authorization and lease window");
            Require(Text(authorization, "schema") == AuthorizationSchema &&
                Text(authorization, "purpose") == Purpose &&
                Text(authorization, "workerId") == result.plan.workerId &&
                Text(authorization, "authorizationId") ==
                    ExpectedAuthorizationId(authorization) &&
                Text(receipt, "authorizationId") == Text(authorization, "authorizationId") &&
                Text(receipt, "authorizationIssuedAt") == Text(authorization, "issuedAt") &&
                Text(receipt, "authorizationExpiresAt") == Text(authorization, "expiresAt") &&
                Text(receipt, "leaseId") == Text(task, "leaseId") &&
                Text(receipt, "leaseExpiresAt") == Text(task, "leaseExpiresAt") &&
                Text(task, "id") == result.taskId &&
                Number(task, "revision") == result.plan.taskRevision &&
                string.Equals(Text(task, "digest"), result.plan.taskDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(request, "fingerprint") == result.plan.requestFingerprint &&
                string.Equals(Text(request, "digest"), result.plan.requestDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(plan, "sha256"), result.plan.sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(recipe, "id") == RecipeId && Number(recipe, "version") == 1 &&
                string.Equals(Text(recipe, "digest"), RecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(tool, "id") == "native_root_assembly_888x14_v1" &&
                string.Equals(Text(tool, "sourceNormalizedSha256"),
                    Text(receipt, "toolSourceNormalizedSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"),
                    Text(receipt, "toolExecutableSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(seed, "inventoryDigest"),
                    Text(receipt, "preInventoryDigest"), StringComparison.OrdinalIgnoreCase) &&
                Bool(execution, "authorized") && Number(execution, "attempt") == result.attempt &&
                StringArrayEquals(ArrayValue(execution, "phases"),
                    new[] { "root_assembly_888x14" }) &&
                !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses"),
                "ROOT_AUTHORIZATION_BINDING_MISMATCH", 3,
                "root receipt does not exactly bind its historical authorization");
        }

        private static void LoadAndValidateAuthorization(RunResult result)
        {
            Require(File.Exists(result.authorizationPath) &&
                !HasReparsePoint(result.authorizationPath) &&
                FileLinkCount(result.authorizationPath) == 1,
                "EXECUTION_AUTHORIZATION_FILE_INVALID", 3,
                "final-pack authorization must be a regular single-link file");
            string configuredSha = (System.Environment.GetEnvironmentVariable(
                AuthorizationShaEnvironment) ?? "").Trim();
            string actualSha = Sha256(result.authorizationPath);
            Require(IsSha256(configuredSha) && string.Equals(configuredSha, actualSha,
                    StringComparison.OrdinalIgnoreCase),
                "EXECUTION_AUTHORIZATION_SHA_MISMATCH", 3,
                "final-pack authorization differs from the worker-provided SHA-256");
            byte[] rawBytes = File.ReadAllBytes(result.authorizationPath);
            Dictionary<string, object> authorization = ReadJsonObject(result.authorizationPath);
            ValidateExactAuthorizationKeys(authorization);
            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> request = ChildObject(authorization, "request");
            Dictionary<string, object> plan = ChildObject(authorization, "plan");
            Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> seed = ChildObject(authorization, "seed");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");
            DateTime issued, expires, leaseExpires;
            DateTime now = DateTime.UtcNow;
            Require(ParseUtc(Text(authorization, "issuedAt"), out issued) &&
                ParseUtc(Text(authorization, "expiresAt"), out expires) &&
                ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpires) &&
                issued <= now && expires > now && expires > issued && expires <= leaseExpires &&
                (expires - issued).TotalMinutes <= 30.0,
                "EXECUTION_AUTHORIZATION_WINDOW_INVALID", 3,
                "final-pack authorization is expired, future-dated or exceeds the lease");
            Require(Text(authorization, "schema") == AuthorizationSchema &&
                Text(authorization, "purpose") == Purpose &&
                Text(authorization, "workerId") == result.plan.workerId &&
                Text(task, "id") == result.taskId &&
                Number(task, "revision") == result.plan.taskRevision &&
                string.Equals(Text(task, "digest"), result.plan.taskDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(request, "fingerprint") == result.plan.requestFingerprint &&
                string.Equals(Text(request, "digest"), result.plan.requestDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(plan, "sha256"), result.plan.sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(recipe, "id") == RecipeId && Number(recipe, "version") == 1 &&
                string.Equals(Text(recipe, "digest"), RecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(tool, "id") == ToolId &&
                string.Equals(Text(tool, "sourceNormalizedSha256"),
                    result.tool.sourceNormalizedSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"),
                    result.tool.executableSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(seed, "inventoryDigest"), result.preInventory.digest,
                    StringComparison.OrdinalIgnoreCase) &&
                Bool(execution, "authorized") && Number(execution, "attempt") == result.attempt &&
                StringArrayEquals(ArrayValue(execution, "phases"), new[] { ExecutionPhase }) &&
                !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses"),
                "EXECUTION_AUTHORIZATION_BINDING_MISMATCH", 3,
                "authorization does not exactly bind current task, plan, tool, inventory and phase");
            string expectedId = ExpectedAuthorizationId(authorization);
            Require(Text(authorization, "authorizationId") == expectedId &&
                Regex.IsMatch(expectedId, "^native-auth-[a-f0-9]{32}$",
                    RegexOptions.CultureInvariant),
                "EXECUTION_AUTHORIZATION_ID_MISMATCH", 3,
                "authorizationId differs from its stable immutable binding digest");
            result.authorization = new AuthorizationEvidence
            {
                id = expectedId,
                path = result.authorizationPath,
                sha256 = actualSha,
                jsonBase64 = Convert.ToBase64String(rawBytes),
                issuedAt = Text(authorization, "issuedAt"),
                expiresAt = Text(authorization, "expiresAt"),
                leaseId = Text(task, "leaseId"),
                leaseExpiresAt = Text(task, "leaseExpiresAt"),
                raw = authorization
            };
            result.authorization_checkpoints.Add(new AuthorizationCheckpoint
            {
                name = "preflight", checkedAtUtc = now.ToString("o",
                    CultureInfo.InvariantCulture), valid = true
            });
        }

        private static void ValidateExactAuthorizationKeys(Dictionary<string, object> root)
        {
            RequireExactKeys(root, "authorization", "schema", "authorizationId", "issuedAt",
                "expiresAt", "purpose", "workerId", "task", "request", "plan", "recipe",
                "tool", "seed", "execution", "qualityBoundary");
            RequireExactKeys(ChildObject(root, "task"), "authorization.task", "id", "revision",
                "digest", "leaseId", "leaseExpiresAt");
            RequireExactKeys(ChildObject(root, "request"), "authorization.request", "fingerprint",
                "digest");
            RequireExactKeys(ChildObject(root, "plan"), "authorization.plan", "sha256");
            RequireExactKeys(ChildObject(root, "recipe"), "authorization.recipe", "id", "version",
                "digest");
            RequireExactKeys(ChildObject(root, "tool"), "authorization.tool", "id",
                "sourceNormalizedSha256", "executableSha256");
            RequireExactKeys(ChildObject(root, "seed"), "authorization.seed", "inventoryDigest");
            RequireExactKeys(ChildObject(root, "execution"), "authorization.execution", "authorized",
                "attempt", "phases");
            RequireExactKeys(ChildObject(root, "qualityBoundary"), "authorization.qualityBoundary",
                "engineeringAssistanceReady", "readyOnlyAfterEveryRequiredCheckPasses");
        }

        private static string ExpectedAuthorizationId(Dictionary<string, object> authorization)
        {
            var binding = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "workerId", authorization["workerId"] }, { "task", authorization["task"] },
                { "request", authorization["request"] }, { "plan", authorization["plan"] },
                { "recipe", authorization["recipe"] }, { "tool", authorization["tool"] },
                { "seed", authorization["seed"] }, { "execution", authorization["execution"] },
                { "issuedAt", authorization["issuedAt"] },
                { "expiresAt", authorization["expiresAt"] }
            };
            return "native-auth-" + Sha256Text(StableJson(binding)).Substring(0, 32)
                .ToLowerInvariant();
        }

        private static void AssertAuthorizationStillValid(RunResult result, string checkpoint)
        {
            bool valid = result != null && File.Exists(result.authorization.path) &&
                !HasReparsePoint(result.authorization.path) &&
                FileLinkCount(result.authorization.path) == 1 &&
                string.Equals(Sha256(result.authorization.path), result.authorization.sha256,
                    StringComparison.OrdinalIgnoreCase);
            Dictionary<string, object> authorization = valid ?
                ReadJsonObject(result.authorization.path) :
                new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> seed = ChildObject(authorization, "seed");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            DateTime expires, leaseExpires;
            valid = valid && ParseUtc(Text(authorization, "expiresAt"), out expires) &&
                ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpires) &&
                DateTime.UtcNow < expires && DateTime.UtcNow < leaseExpires &&
                Text(authorization, "authorizationId") == result.authorization.id &&
                ExpectedAuthorizationId(authorization) == result.authorization.id &&
                Text(task, "id") == result.taskId &&
                Text(task, "leaseId") == result.authorization.leaseId &&
                Text(tool, "id") == ToolId &&
                string.Equals(Text(tool, "sourceNormalizedSha256"),
                    result.tool.sourceNormalizedSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"),
                    result.tool.executableSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(seed, "inventoryDigest"), result.preInventory.digest,
                    StringComparison.OrdinalIgnoreCase) && Bool(execution, "authorized") &&
                Number(execution, "attempt") == result.attempt &&
                StringArrayEquals(ArrayValue(execution, "phases"), new[] { ExecutionPhase });
            result.authorization_checkpoints.Add(new AuthorizationCheckpoint
            {
                name = checkpoint,
                checkedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                valid = valid
            });
            Require(valid, "EXECUTION_AUTHORIZATION_CHANGED_OR_EXPIRED", 4,
                "authorization changed or expired at checkpoint " + checkpoint);
        }

        private static bool CompletionWithinAuthorization(RunResult result)
        {
            DateTime completed, expires, leaseExpires;
            return ParseUtc(result.completed_at_utc, out completed) &&
                ParseUtc(result.authorization.expiresAt, out expires) &&
                ParseUtc(result.authorization.leaseExpiresAt, out leaseExpires) &&
                completed <= expires && completed <= leaseExpires;
        }

        private static void ValidateOwnToolIdentity(RunResult result)
        {
            Require(string.Equals(result.tool.sourceNormalizedSha256, ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase), "SOURCE_IDENTITY_MISMATCH", 3,
                "final-pack source differs from the compiled normalized identity");
            Require(string.Equals(result.tool.executableSha256,
                    Sha256(Process.GetCurrentProcess().MainModule.FileName),
                    StringComparison.OrdinalIgnoreCase), "EXECUTABLE_IDENTITY_MISMATCH", 3,
                "running executable changed during preflight");
        }

        private static void AcquireAttemptLock(RunResult result)
        {
            string path = Path.Combine(result.attemptDir, ".native-final-pack-888x14.lock");
            Require(IsUnder(path, result.attemptDir) && !HasReparsePoint(result.attemptDir),
                "ATTEMPT_LOCK_SCOPE_INVALID", 8,
                "attempt lock must remain under the non-reparse attempt directory");
            try
            {
                attemptLock = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.WriteThrough);
                byte[] bytes = new UTF8Encoding(false).GetBytes(Serialize(new
                {
                    toolId = ToolId,
                    authorizationId = result.authorization.id,
                    pid = Process.GetCurrentProcess().Id,
                    startedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                }));
                attemptLock.Write(bytes, 0, bytes.Length);
                attemptLock.Flush(true);
                result.transaction.lockPath = path;
                result.transaction.lockAcquired = true;
            }
            catch (IOException ex)
            {
                throw new StageException("ATTEMPT_LOCK_BUSY", 8,
                    "final-pack attempt lock is already held or stale: " + ex.Message);
            }
        }

        private static void CreateCompleteInputBackup(RunResult result)
        {
            string backup = Path.Combine(result.attemptDir,
                ".final-pack-rollback-" + result.authorization.id);
            Require(IsUnder(backup, result.attemptDir) && !Directory.Exists(backup) &&
                !File.Exists(backup), "BACKUP_TARGET_INVALID", 9,
                "dedicated rollback backup path must be absent and inside the attempt");
            Directory.CreateDirectory(backup);
            result.transaction.backupDirectory = backup;
            List<string> sourceFiles = CaptureFlatCadTree(result.workingPackPath, 88,
                "BACKUP_SOURCE_INVALID");
            foreach (string source in sourceFiles)
            {
                string target = Path.Combine(backup, Path.GetFileName(source));
                File.Copy(source, target, false);
                Require(File.Exists(target) && !HasReparsePoint(target) &&
                    FileLinkCount(target) == 1 &&
                    string.Equals(Sha256(source), Sha256(target),
                        StringComparison.OrdinalIgnoreCase),
                    "BACKUP_COPY_MISMATCH", 9,
                    "input backup file does not exactly match: " + Path.GetFileName(source));
            }
            List<string> backupFiles = CaptureFlatCadTree(backup, 88, "BACKUP_TREE_INVALID");
            InventoryEvidence backupInventory = InventoryEvidenceFor(backupFiles);
            Require(backupInventory.fileCount == 88 &&
                string.Equals(backupInventory.digest, result.preInventory.digest,
                    StringComparison.OrdinalIgnoreCase),
                "BACKUP_INVENTORY_MISMATCH", 9,
                "complete 88-file backup digest differs from the input digest");
            result.transaction.backupComplete = true;
            result.transaction.backupFileCount = backupFiles.Count;
            result.transaction.backupDigest = backupInventory.digest;
        }

        private static void RunDedicatedPackAndGo(RunResult result)
        {
            Require(!Directory.Exists(result.finalPackagePath), "FINAL_OUTPUT_NOT_EMPTY", 10,
                "fixed final package path must be absent before PackAndGo");
            Directory.CreateDirectory(result.finalPackagePath);
            result.transaction.finalPackageCreated = true;
            Require(OutputDirectoryAcceptable(Directory.GetFileSystemEntries(
                    result.finalPackagePath)), "FINAL_OUTPUT_NOT_EMPTY", 10,
                "fixed final package path must be exactly empty before PackAndGo");

            OwnedSession session = StartOwnedSession(result, "pack_from_working_root");
            try
            {
                int openErrors = 0;
                int openWarnings = 0;
                ModelDoc2 model = session.sw.OpenDoc6(result.workingRootPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "",
                    ref openErrors, ref openWarnings);
                session.model = model;
                result.packaging.sourceOpenErrors = openErrors;
                result.packaging.sourceOpenWarnings = openWarnings;
                Require(model != null && StrictRootOpen(openErrors, openWarnings),
                    "WORKING_ROOT_OPEN_GATE_FAILED", 11,
                    "working root must open read-only with exact errors=0 warnings=32");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "WORKING_ROOT_NOT_ASSEMBLY", 11,
                    "working root is not an assembly document");
                assembly.ResolveAllLightWeightComponents(false);
                Require(model.ForceRebuild3(false), "WORKING_ROOT_REBUILD_FAILED", 11,
                    "working root ForceRebuild3 returned false");
                result.packaging.sourceSaved = false;
                RootSnapshot sourceSnapshot = CaptureRootSnapshot(session.sw, model,
                    result.workingRootPath, result.workingPackPath, result);
                ValidateRootSnapshot(sourceSnapshot, result, "working_root_before_pack");
                result.packaging.sourceSnapshot = sourceSnapshot;

                ModelDocExtension extension = model.Extension as ModelDocExtension;
                Require(extension != null, "MODEL_EXTENSION_MISSING", 12,
                    "working root ModelDocExtension is unavailable");
                PackAndGo packAndGo = extension.GetPackAndGo() as PackAndGo;
                Require(packAndGo != null, "PACK_AND_GO_CREATE_FAILED", 12,
                    "dedicated final stage GetPackAndGo returned null");
                packAndGo.IncludeDrawings = false;
                packAndGo.IncludeSimulationResults = false;
                packAndGo.IncludeSuppressed = true;
                packAndGo.IncludeToolboxComponents = true;
                packAndGo.FlattenToSingleFolder = true;

                int documentCount = packAndGo.GetDocumentNamesCount();
                result.packaging.documentCount = documentCount;
                Require(documentCount == 55, "PACK_DOCUMENT_COUNT_MISMATCH", 12,
                    "PackAndGo dependency set must contain exact 55 CAD documents");
                object documentNamesRaw = null;
                Require(packAndGo.GetDocumentNames(out documentNamesRaw),
                    "PACK_DOCUMENT_NAMES_FAILED", 12,
                    "PackAndGo GetDocumentNames failed");
                List<string> sourceNames = ToStringList(documentNamesRaw);
                Require(sourceNames.Count == 55 && sourceNames.All(Path.IsPathRooted) &&
                    sourceNames.Select(Path.GetFullPath).Distinct(
                        StringComparer.OrdinalIgnoreCase).Count() == 55,
                    "PACK_DOCUMENT_NAMES_INVALID", 12,
                    "PackAndGo document names must be 55 unique rooted paths");
                result.packaging.sourceDocumentNames = sourceNames;

                Require(packAndGo.SetSaveToName(true, result.finalPackagePath),
                    "PACK_SAVE_ROOT_FAILED", 12,
                    "PackAndGo SetSaveToName failed");
                object proposedRaw = null;
                object proposedStatusesRaw = null;
                Require(packAndGo.GetDocumentSaveToNames(out proposedRaw,
                        out proposedStatusesRaw), "PACK_PROPOSED_NAMES_FAILED", 12,
                    "PackAndGo GetDocumentSaveToNames failed before exact rename");
                List<string> proposed = ToStringList(proposedRaw);
                List<int> proposedStatuses = ToIntList(proposedStatusesRaw);
                Require(proposed.Count == 55 && proposedStatuses.Count == 55 &&
                    StatusesAllZero(proposedStatuses), "PACK_PROPOSED_STATUS_FAILED", 12,
                    "PackAndGo proposed save statuses must be exact 55 all-zero");

                var targetNames = new List<string>();
                var targetLeaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int rootMatches = 0;
                for (int index = 0; index < sourceNames.Count; index++)
                {
                    string sourceLeaf = Path.GetFileName(sourceNames[index]);
                    string targetLeaf = sourceLeaf;
                    if (string.Equals(sourceLeaf, WorkingRootFileName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        targetLeaf = FinalRootFileName;
                        rootMatches++;
                    }
                    string target = Path.Combine(result.finalPackagePath, targetLeaf);
                    Require(IsUnder(target, result.finalPackagePath) &&
                        SamePath(Path.GetDirectoryName(target), result.finalPackagePath) &&
                        targetLeaves.Add(targetLeaf), "PACK_TARGET_NAME_COLLISION", 12,
                        "PackAndGo target names must be unique direct children");
                    targetNames.Add(target);
                }
                Require(rootMatches == 1, "PACK_ROOT_RENAME_SOURCE_MISMATCH", 12,
                    "PackAndGo source set must contain exactly one working root");
                Require(packAndGo.SetDocumentSaveToNames(targetNames.ToArray()),
                    "PACK_EXACT_TARGET_NAMES_FAILED", 12,
                    "PackAndGo rejected the exact final target names");

                object confirmedRaw = null;
                object confirmedStatusesRaw = null;
                Require(packAndGo.GetDocumentSaveToNames(out confirmedRaw,
                        out confirmedStatusesRaw), "PACK_CONFIRMED_NAMES_FAILED", 12,
                    "PackAndGo GetDocumentSaveToNames failed after exact rename");
                List<string> confirmed = ToStringList(confirmedRaw);
                List<int> confirmedStatuses = ToIntList(confirmedStatusesRaw);
                Require(confirmed.Count == 55 && confirmedStatuses.Count == 55 &&
                    StatusesAllZero(confirmedStatuses) &&
                    confirmed.Select(Path.GetFullPath).SequenceEqual(
                        targetNames.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase),
                    "PACK_CONFIRMED_TARGET_MISMATCH", 12,
                    "PackAndGo confirmed names/statuses differ from the exact target map");

                object saveResultRaw = extension.SavePackAndGo(packAndGo);
                List<int> saveStatuses = ToIntList(saveResultRaw);
                result.packaging.saveStatuses = saveStatuses;
                result.packaging.targetDocumentNames = targetNames;
                Require(saveStatuses.Count == 55 && StatusesAllZero(saveStatuses),
                    "PACK_SAVE_STATUS_FAILED", 12,
                    "SavePackAndGo must return exact 55 all-zero statuses");
                result.packaging.packAndGoCompleted = true;
            }
            finally
            {
                CloseOwnedSession(result, session);
            }
            Require(result.sessions.Last().processExited && result.sessions.Last().monitorsExited,
                "PACK_SESSION_EXIT_UNPROVEN", 13,
                "packaging SolidWorks session did not exit with exact ownership proof");
        }

        private static void VerifyFinalPackage(RunResult result)
        {
            List<string> files = CaptureFlatCadTree(result.finalPackagePath, 55,
                "FINAL_PACKAGE_INVENTORY_INVALID");
            InventoryEvidence inventory = InventoryEvidenceFor(files);
            Require(ClosureCountsExact(inventory.fileCount, inventory.assemblyFileCount,
                    inventory.partFileCount, 0, 0),
                "FINAL_PACKAGE_COUNT_MISMATCH", 14,
                "final package must be exact flat 55 = 14 SLDASM + 41 SLDPRT");
            string rootPath = Path.Combine(result.finalPackagePath, FinalRootFileName);
            Require(File.Exists(rootPath) && !File.Exists(Path.Combine(result.finalPackagePath,
                    WorkingRootFileName)), "FINAL_ROOT_NAME_MISMATCH", 14,
                "final package must contain only the exact 888-named root");
            string[] forbidden = ArrayValue(result.contractSnapshot,
                "forbiddenFinalReferenceLeafNames").Select(ValueText).ToArray();
            Require(!HasForbiddenReference(files.Select(Path.GetFileName), forbidden),
                "FINAL_PACKAGE_FORBIDDEN_FILE", 14,
                "final package contains an old referenced door file name");
            result.finalRootPath = rootPath;
            result.postInventory = inventory;

            OwnedSession session = StartOwnedSession(result, "fresh_readonly_final_package");
            try
            {
                int openErrors = 0;
                int openWarnings = 0;
                ModelDoc2 model = session.sw.OpenDoc6(rootPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "",
                    ref openErrors, ref openWarnings);
                session.model = model;
                result.finalVerification.openErrors = openErrors;
                result.finalVerification.openWarnings = openWarnings;
                Require(model != null && openErrors == 0 && openWarnings == 32,
                    "FINAL_ROOT_REOPEN_GATE_FAILED", 15,
                    "final root fresh read-only reopen must be exact errors=0 warnings=32");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "FINAL_ROOT_NOT_ASSEMBLY", 15,
                    "final root is not an assembly document");
                assembly.ResolveAllLightWeightComponents(false);
                Require(model.ForceRebuild3(false), "FINAL_ROOT_REBUILD_FAILED", 15,
                    "final root ForceRebuild3 returned false");
                RootSnapshot snapshot = CaptureRootSnapshot(session.sw, model, rootPath,
                    result.finalPackagePath, result);
                ValidateRootSnapshot(snapshot, result, "final_package_reopen");
                Require(PhysicalInventoryEqualsClosure(files, snapshot.closurePaths),
                    "FINAL_PHYSICAL_REFERENCE_CLOSURE_MISMATCH", 15,
                    "final physical 55-file inventory must equal the root reference closure");
                result.finalVerification.snapshot = snapshot;
                result.finalVerification.passed = true;
            }
            finally
            {
                CloseOwnedSession(result, session);
            }
            Require(result.sessions.Last().processExited && result.sessions.Last().monitorsExited,
                "FINAL_REOPEN_SESSION_EXIT_UNPROVEN", 16,
                "final package verification session did not exit exactly");
            List<string> postReopenFiles = CaptureFlatCadTree(result.finalPackagePath, 55,
                "FINAL_PACKAGE_POST_REOPEN_INVALID");
            Require(string.Equals(InventoryEvidenceFor(postReopenFiles).digest,
                    result.postInventory.digest, StringComparison.OrdinalIgnoreCase),
                "FINAL_PACKAGE_CHANGED_DURING_READONLY_REOPEN", 16,
                "fresh read-only reopen changed final package bytes");
        }

        private static void VerifyRelocatedCopy(RunResult result)
        {
            Require(!Directory.Exists(result.relocatedProbePath),
                "RELOCATED_PROBE_ALREADY_EXISTS", 17,
                "relocated probe path must remain absent until this transaction creates it");
            string relocatedPackage = Path.Combine(result.relocatedProbePath, "package_copy");
            Directory.CreateDirectory(relocatedPackage);
            result.transaction.relocatedProbeCreated = true;
            foreach (string source in CaptureFlatCadTree(result.finalPackagePath, 55,
                "RELOCATED_SOURCE_INVALID"))
            {
                string target = Path.Combine(relocatedPackage, Path.GetFileName(source));
                File.Copy(source, target, false);
                Require(File.Exists(target) && !HasReparsePoint(target) &&
                    FileLinkCount(target) == 1 && string.Equals(Sha256(source), Sha256(target),
                        StringComparison.OrdinalIgnoreCase),
                    "RELOCATED_COPY_HASH_MISMATCH", 17,
                    "relocated package copy differs: " + Path.GetFileName(source));
            }
            List<string> relocatedFiles = CaptureFlatCadTree(relocatedPackage, 55,
                "RELOCATED_INVENTORY_INVALID");
            InventoryEvidence relocatedInventory = InventoryEvidenceFor(relocatedFiles);
            Require(string.Equals(relocatedInventory.digest, result.postInventory.digest,
                    StringComparison.OrdinalIgnoreCase),
                "RELOCATED_INVENTORY_DIGEST_MISMATCH", 17,
                "relocated package inventory differs from final_native_package");
            string relocatedRoot = Path.Combine(relocatedPackage, FinalRootFileName);

            OwnedSession session = StartOwnedSession(result, "second_relocated_readonly_reopen");
            try
            {
                int openErrors = 0;
                int openWarnings = 0;
                ModelDoc2 model = session.sw.OpenDoc6(relocatedRoot,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                        swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "",
                    ref openErrors, ref openWarnings);
                session.model = model;
                result.relocatedVerification.openErrors = openErrors;
                result.relocatedVerification.openWarnings = openWarnings;
                Require(model != null && openErrors == 0 && openWarnings == 32,
                    "RELOCATED_ROOT_REOPEN_GATE_FAILED", 18,
                    "second relocated root reopen must be exact errors=0 warnings=32");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "RELOCATED_ROOT_NOT_ASSEMBLY", 18,
                    "relocated root is not an assembly document");
                assembly.ResolveAllLightWeightComponents(false);
                Require(model.ForceRebuild3(false), "RELOCATED_ROOT_REBUILD_FAILED", 18,
                    "relocated root ForceRebuild3 returned false");
                RootSnapshot snapshot = CaptureRootSnapshot(session.sw, model, relocatedRoot,
                    relocatedPackage, result);
                ValidateRootSnapshot(snapshot, result, "relocated_package_reopen");
                Require(PhysicalInventoryEqualsClosure(relocatedFiles, snapshot.closurePaths),
                    "RELOCATED_PHYSICAL_REFERENCE_CLOSURE_MISMATCH", 18,
                    "relocated physical inventory must equal its local reference closure");
                Require(result.finalVerification.snapshot != null &&
                    snapshot.signature == result.finalVerification.snapshot.signature,
                    "RELOCATED_TOPOLOGY_SIGNATURE_MISMATCH", 18,
                    "relocated topology signature differs from the first final-package reopen");
                result.relocatedVerification.snapshot = snapshot;
                result.relocatedVerification.inventoryDigest = relocatedInventory.digest;
                result.relocatedVerification.passed = true;
            }
            finally
            {
                CloseOwnedSession(result, session);
            }
            Require(result.sessions.Last().processExited && result.sessions.Last().monitorsExited,
                "RELOCATED_SESSION_EXIT_UNPROVEN", 19,
                "relocated verification session did not exit exactly");
            Require(string.Equals(InventoryEvidenceFor(CaptureFlatCadTree(relocatedPackage, 55,
                    "RELOCATED_POST_REOPEN_INVALID")).digest, relocatedInventory.digest,
                    StringComparison.OrdinalIgnoreCase),
                "RELOCATED_PACKAGE_CHANGED_DURING_READONLY_REOPEN", 19,
                "second read-only reopen changed relocated package bytes");
            SafeDeleteOwnedDirectory(result.relocatedProbePath, result.attemptDir,
                ".final-pack-relocated-probe");
            result.transaction.relocatedProbeRemoved = !Directory.Exists(result.relocatedProbePath);
            Require(result.transaction.relocatedProbeRemoved,
                "RELOCATED_PROBE_CLEANUP_FAILED", 19,
                "relocated probe must be removed before immutable evidence commit");
        }

        private static void AuditInputUnchanged(RunResult result)
        {
            List<string> current = CaptureFlatCadTree(result.workingPackPath, 88,
                "INPUT_POST_AUDIT_INVALID");
            InventoryEvidence currentInventory = InventoryEvidenceFor(current);
            Require(string.Equals(currentInventory.digest, result.preInventory.digest,
                    StringComparison.OrdinalIgnoreCase),
                "INPUT_CHANGED_BY_FINAL_PACK", 20,
                "final packaging must not save or mutate the 88-file working pack");
            List<string> backups = CaptureFlatCadTree(result.transaction.backupDirectory, 88,
                "BACKUP_POST_AUDIT_INVALID");
            var backupByName = backups.ToDictionary(Path.GetFileName, Sha256,
                StringComparer.OrdinalIgnoreCase);
            Require(current.All(path => backupByName.ContainsKey(Path.GetFileName(path)) &&
                    string.Equals(backupByName[Path.GetFileName(path)], Sha256(path),
                        StringComparison.OrdinalIgnoreCase)),
                "INPUT_FILE_HASH_AUDIT_FAILED", 20,
                "one or more working-pack files differ from the complete backup");
            result.transaction.inputUnchanged = true;
        }

        private static void DeleteBackupBeforeCommit(RunResult result)
        {
            Require(result.transaction.inputUnchanged && result.processes.finalGate,
                "BACKUP_DELETE_PRECONDITION_FAILED", 21,
                "backup can be removed only after input hash audit and exact process exit");
            SafeDeleteOwnedDirectory(result.transaction.backupDirectory, result.attemptDir,
                ".final-pack-rollback-" + result.authorization.id);
            result.backup_deleted = !Directory.Exists(result.transaction.backupDirectory);
            result.backup_delete_error = result.backup_deleted ? "" : "backup directory remains";
            Require(result.backup_deleted, "BACKUP_DELETE_FAILED", 21,
                "complete backup could not be removed before evidence commit");
        }

        private static RootSnapshot CaptureRootSnapshot(ISldWorks sw, ModelDoc2 model,
            string rootPath, string packageRoot, RunResult result)
        {
            var snapshot = new RootSnapshot { traversalComplete = true };
            List<Component2> direct = DirectComponents(model);
            snapshot.topLevelComponentCount = direct.Count;
            List<Component2> all = AllComponents(model, out snapshot.traversalComplete);
            snapshot.recursiveComponentCount = all.Count;
            snapshot.activeRecursiveComponentCount = all.Count(component =>
                !Safe(() => component.IsSuppressed(), true));
            snapshot.suppressedComponentCount = all.Count -
                snapshot.activeRecursiveComponentCount;

            var doorLeaves = new HashSet<string>(ContractRows(result, "doors")
                .Select(row => Text(row, "sourceAssembly")), StringComparer.OrdinalIgnoreCase);
            var shelfLeaves = new HashSet<string>(ContractRows(result, "shelves")
                .Select(row => Text(row, "source")), StringComparer.OrdinalIgnoreCase);
            var crossbarLeaves = new HashSet<string>(ContractRows(result, "frameCrossbars")
                .Select(row => Text(row, "source")), StringComparer.OrdinalIgnoreCase);

            foreach (Component2 component in direct)
            {
                ComponentEvidence row = ComponentEvidenceFor(component);
                snapshot.topLevelComponents.Add(row);
                string leaf = Path.GetFileName(row.path);
                if (doorLeaves.Contains(leaf)) snapshot.doors.Add(row);
                if (shelfLeaves.Contains(leaf)) snapshot.shelves.Add(row);
            }
            foreach (Component2 component in all)
            {
                ComponentEvidence row = ComponentEvidenceFor(component);
                string leaf = Path.GetFileName(row.path);
                if (crossbarLeaves.Contains(leaf) && row.active)
                    snapshot.frameCrossbars.Add(row);
                if (string.Equals(leaf, "mechanical_lock_tongue.SLDPRT",
                        StringComparison.OrdinalIgnoreCase) && row.active)
                    snapshot.tongues.Add(row);
            }
            snapshot.doorModuleCount = snapshot.doors.Count;
            snapshot.shelfModuleCount = snapshot.shelves.Count;
            snapshot.activeFrameCrossbarCount = snapshot.frameCrossbars.Count;
            snapshot.mechanicalTongueCount = snapshot.tongues.Count;
            snapshot.featureHealth = CaptureFeatureHealth(model);
            snapshot.traversalComplete = snapshot.traversalComplete &&
                snapshot.featureHealth.traversalComplete;

            List<string> dependencies = RootedCadPaths(Safe(() =>
                sw.GetDocumentDependencies2(rootPath, true, true, false), null));
            dependencies.Add(rootPath);
            var closure = new HashSet<string>(dependencies.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            snapshot.cadFileCount = closure.Count;
            snapshot.assemblyFileCount = closure.Count(IsAssembly);
            snapshot.partFileCount = closure.Count(IsPart);
            snapshot.missingFileCount = closure.Count(path => !File.Exists(path));
            snapshot.externalFileCount = closure.Count(path => !IsUnder(path, packageRoot));
            snapshot.dependenciesAllLocal = snapshot.missingFileCount == 0 &&
                snapshot.externalFileCount == 0;
            snapshot.closurePaths = closure.OrderBy(path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase).ToList();
            var forbidden = new HashSet<string>(ArrayValue(result.contractSnapshot,
                "forbiddenFinalReferenceLeafNames").Select(ValueText),
                StringComparer.OrdinalIgnoreCase);
            snapshot.forbiddenReferenceLeaves = closure.Select(Path.GetFileName)
                .Where(forbidden.Contains).OrderBy(value => value,
                    StringComparer.OrdinalIgnoreCase).ToList();
            snapshot.signature = RootSignature(snapshot);
            return snapshot;
        }

        private static void ValidateRootSnapshot(RootSnapshot snapshot, RunResult result,
            string label)
        {
            Require(snapshot != null && snapshot.traversalComplete &&
                snapshot.topLevelComponentCount == 41 &&
                snapshot.recursiveComponentCount == 294 &&
                snapshot.activeRecursiveComponentCount == 294 &&
                snapshot.suppressedComponentCount == 0 &&
                snapshot.doorModuleCount == 14 && snapshot.shelfModuleCount == 12 &&
                snapshot.activeFrameCrossbarCount == 12 &&
                snapshot.mechanicalTongueCount == 14,
                "ROOT_TOPOLOGY_COUNT_MISMATCH", 22,
                label + " must be exact 41/294/294/0 with modules 14/12/12/14");
            Require(snapshot.cadFileCount == 55 && snapshot.assemblyFileCount == 14 &&
                snapshot.partFileCount == 41 && snapshot.missingFileCount == 0 &&
                snapshot.externalFileCount == 0 && snapshot.dependenciesAllLocal &&
                snapshot.forbiddenReferenceLeaves.Count == 0,
                "ROOT_REFERENCE_CLOSURE_MISMATCH", 22,
                label + " must be exact local closure 55 = 14 SLDASM + 41 SLDPRT");
            Require(FeatureIssuesAllowed(snapshot.featureHealth.issues.ToArray(),
                    result.contractSnapshot), "ROOT_FEATURE_HEALTH_NOT_PERMITTED", 22,
                label + " has a feature issue not explicitly permitted by the root contract");

            foreach (Dictionary<string, object> contractRow in ContractRows(result, "doors"))
            {
                string source = Text(contractRow, "sourceAssembly");
                double[] transform = ArrayValue(contractRow, "transform").Select(value =>
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)).ToArray();
                List<ComponentEvidence> matches = snapshot.doors.Where(row => row.active &&
                    !row.fixedState && string.Equals(Path.GetFileName(row.path), source,
                        StringComparison.OrdinalIgnoreCase) &&
                    SameTransform(transform, row.transform.ToArray(), 1e-8)).ToList();
                Require(matches.Count == 1, "ROOT_DOOR_CONTRACT_MISMATCH", 22,
                    label + " door placement mismatch: " + Text(contractRow, "instanceRole"));
                double tongueX = Number(contractRow, "tongueGlobalXmm");
                double tongueY = Number(contractRow, "tongueGlobalYmm");
                double tongueZ = Number(contractRow, "tongueGlobalZmm");
                Require(snapshot.tongues.Count(row => row.active && row.totalTransform.Count >= 12 &&
                    Near(row.totalTransform[9] * 1000.0, tongueX, 0.01) &&
                    Near(row.totalTransform[10] * 1000.0, tongueY, 0.01) &&
                    Near(row.totalTransform[11] * 1000.0, tongueZ, 0.01)) == 1,
                    "ROOT_TONGUE_CONTRACT_MISMATCH", 22,
                    label + " one-door-one-tongue transform mismatch: " +
                    Text(contractRow, "instanceRole"));
            }
            foreach (Dictionary<string, object> contractRow in ContractRows(result, "shelves"))
            {
                string source = Text(contractRow, "source");
                double[] transform = ArrayValue(contractRow, "transform").Select(value =>
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)).ToArray();
                Require(snapshot.shelves.Count(row => row.active && row.fixedState &&
                    string.Equals(Path.GetFileName(row.path), source,
                        StringComparison.OrdinalIgnoreCase) &&
                    SameTransform(transform, row.transform.ToArray(), 1e-8)) == 1,
                    "ROOT_SHELF_CONTRACT_MISMATCH", 22,
                    label + " shelf placement mismatch: " + Text(contractRow, "instanceRole"));
            }
            foreach (Dictionary<string, object> contractRow in ContractRows(result,
                "frameCrossbars"))
            {
                string source = Text(contractRow, "source");
                double[] transform = ArrayValue(contractRow, "transform").Select(value =>
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)).ToArray();
                Require(snapshot.frameCrossbars.Count(row => row.active && !row.fixedState &&
                    string.Equals(Path.GetFileName(row.path), source,
                        StringComparison.OrdinalIgnoreCase) &&
                    SameTransform(transform, row.transform.ToArray(), 1e-8)) == 1,
                    "ROOT_CROSSBAR_CONTRACT_MISMATCH", 22,
                    label + " crossbar placement mismatch: " +
                    Text(contractRow, "instanceRole"));
            }
        }

        private static bool ExactKnownFeatureHealthPolicy(Dictionary<string, object> contract,
            out Dictionary<string, object> allowed)
        {
            allowed = new Dictionary<string, object>(StringComparer.Ordinal);
            if (contract == null) return false;
            Dictionary<string, object> policy = ChildObject(contract, "featureHealthPolicy");
            Dictionary<string, object> candidate = ChildObject(policy, "allowedIssue");
            if (!ExactKeys(policy, new[] { "mode", "maximumIssueCount", "allowedIssue" }) ||
                Text(policy, "mode") != "exact_unique_known_issue_only" ||
                Number(policy, "maximumIssueCount") != 1 ||
                !ExactKeys(candidate, new[] { "name", "type", "errorCode", "errorCode2",
                    "warning" }) ||
                Text(candidate, "name") != "箱体右侧板焊接-1" ||
                Text(candidate, "type") != "Reference" || Number(candidate, "errorCode") != 51 ||
                Number(candidate, "errorCode2") != 51 || !Bool(candidate, "warning"))
                return false;
            allowed = candidate;
            return true;
        }

        private static bool FeatureIssuesAllowed(IEnumerable<FeatureIssueEvidence> issues,
            Dictionary<string, object> contract)
        {
            List<FeatureIssueEvidence> rows = (issues ?? new FeatureIssueEvidence[0]).ToList();
            Dictionary<string, object> allowed;
            if (!ExactKnownFeatureHealthPolicy(contract, out allowed)) return false;
            if (rows.Count == 0) return true;
            if (rows.Count != Convert.ToInt32(Number(
                    ChildObject(contract, "featureHealthPolicy"), "maximumIssueCount"),
                    CultureInfo.InvariantCulture)) return false;
            FeatureIssueEvidence issue = rows[0];
            return string.Equals(issue.name, Text(allowed, "name"), StringComparison.Ordinal) &&
                string.Equals(issue.type, Text(allowed, "type"), StringComparison.Ordinal) &&
                issue.errorCode == Number(allowed, "errorCode") &&
                issue.errorCode2 == Number(allowed, "errorCode2") &&
                issue.warning == Bool(allowed, "warning");
        }

        private static FeatureHealthEvidence CaptureFeatureHealth(ModelDoc2 model)
        {
            var output = new FeatureHealthEvidence { traversalComplete = true };
            Feature feature = null;
            try { feature = model.FirstFeature() as Feature; }
            catch { output.traversalComplete = false; return output; }
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureFeatureRecursive(feature, output, 0);
                if (!output.traversalComplete) { FreeCom(feature); return output; }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { output.traversalComplete = false; }
                FreeCom(feature);
                feature = next;
            }
            if (feature != null) output.traversalComplete = false;
            return output;
        }

        private static void CaptureFeatureRecursive(Feature feature,
            FeatureHealthEvidence output, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || output.featureCount > 20000)
            { output.traversalComplete = false; return; }
            output.featureCount++;
            bool warning;
            int error1;
            int error2;
            bool suppressed;
            string name;
            string type;
            Feature child;
            try
            {
                error1 = feature.GetErrorCode();
                error2 = feature.GetErrorCode2(out warning);
                suppressed = feature.IsSuppressed();
                name = feature.Name;
                type = feature.GetTypeName2();
                child = feature.GetFirstSubFeature() as Feature;
            }
            catch { output.traversalComplete = false; return; }
            if (!suppressed && (error1 > 0 || error2 > 0 || warning))
                output.issues.Add(new FeatureIssueEvidence
                {
                    name = name, type = type, errorCode = error1, errorCode2 = error2,
                    warning = warning, depth = depth
                });
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureRecursive(child, output, depth + 1);
                if (!output.traversalComplete) { FreeCom(child); return; }
                Feature next = null;
                try { next = child.GetNextSubFeature() as Feature; }
                catch { output.traversalComplete = false; }
                FreeCom(child);
                child = next;
            }
            if (child != null) output.traversalComplete = false;
        }

        private static List<Component2> DirectComponents(ModelDoc2 model)
        {
            AssemblyDoc assembly = model as AssemblyDoc;
            Require(assembly != null, "ASSEMBLY_REQUIRED", 22,
                "component traversal requires an assembly");
            object raw = assembly.GetComponents(true);
            Array array = raw as Array;
            Require(array != null, "DIRECT_COMPONENT_TRAVERSAL_FAILED", 22,
                "GetComponents(true) returned no array");
            return array.Cast<object>().OfType<Component2>().ToList();
        }

        private static List<Component2> AllComponents(ModelDoc2 model, out bool complete)
        {
            complete = true;
            AssemblyDoc assembly = model as AssemblyDoc;
            if (assembly == null) { complete = false; return new List<Component2>(); }
            Array array = Safe(() => assembly.GetComponents(false) as Array, null);
            if (array == null) { complete = false; return new List<Component2>(); }
            List<Component2> components = array.Cast<object>().OfType<Component2>().ToList();
            if (components.Count > 1000) complete = false;
            return components;
        }

        private static ComponentEvidence ComponentEvidenceFor(Component2 component)
        {
            double[] transform = TransformData(Safe(() => component.Transform2 as MathTransform,
                null)) ?? new double[0];
            double[] total = TransformData(Safe(() => component.GetTotalTransform(true), null)) ??
                new double[0];
            return new ComponentEvidence
            {
                name = LeafComponentName(Safe(() => component.Name2, "")),
                path = Safe(() => component.GetPathName(), ""),
                active = !Safe(() => component.IsSuppressed(), true),
                fixedState = Safe(() => component.IsFixed(), false),
                transform = transform.ToList(),
                totalTransform = total.ToList()
            };
        }

        private static string RootSignature(RootSnapshot snapshot)
        {
            var rows = snapshot.topLevelComponents.Select(row => "T|" + row.name + "|" +
                Path.GetFileName(row.path) + "|" + row.active + "|" + row.fixedState + "|" +
                string.Join(",", row.transform.Select(Token))).ToList();
            rows.AddRange(snapshot.tongues.Select(row => "L|" + Path.GetFileName(row.path) + "|" +
                string.Join(",", row.totalTransform.Select(Token))));
            rows.Add("C|" + snapshot.topLevelComponentCount + "|" +
                snapshot.recursiveComponentCount + "|" + snapshot.activeRecursiveComponentCount +
                "|" + snapshot.suppressedComponentCount + "|" + snapshot.cadFileCount);
            return Sha256Text(string.Join("\n", rows.OrderBy(value => value,
                StringComparer.Ordinal).ToArray()));
        }

        private static bool PhysicalInventoryEqualsClosure(IEnumerable<string> physical,
            IEnumerable<string> closure)
        {
            var left = new HashSet<string>((physical ?? new string[0]).Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            var right = new HashSet<string>((closure ?? new string[0]).Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            return left.SetEquals(right);
        }

        private static List<Dictionary<string, object>> ContractRows(RunResult result, string name)
        {
            return ArrayValue(result.contractSnapshot, name)
                .OfType<Dictionary<string, object>>().ToList();
        }

        private static double[] TransformData(MathTransform transform)
        {
            Array raw = transform == null ? null : Safe(() => transform.ArrayData as Array, null);
            if (raw == null || raw.Length < 13) return null;
            var output = new double[Math.Max(16, raw.Length)];
            for (int index = 0; index < raw.Length; index++)
                output[index] = Convert.ToDouble(raw.GetValue(index), CultureInfo.InvariantCulture);
            if (output[12] == 0.0) output[12] = 1.0;
            return output;
        }

        private static bool SameTransform(double[] left, double[] right, double tolerance)
        {
            return left != null && right != null && left.Length >= 13 && right.Length >= 13 &&
                Enumerable.Range(0, 13).All(index => Near(left[index], right[index], tolerance));
        }

        private static OwnedSession StartOwnedSession(RunResult result, string purpose)
        {
            var evidence = new SessionEvidence
            {
                purpose = purpose,
                createdAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
            var session = new OwnedSession { evidence = evidence };
            result.sessions.Add(evidence);
            try
            {
                List<int> sldworksBefore = ProcessIds("SLDWORKS");
                List<int> monitorsBefore = ProcessIds("sldProcMon");
                Require(sldworksBefore.Count == 0 && monitorsBefore.Count == 0,
                    "SESSION_PROCESS_BASELINE_DIRTY", 30,
                    "each fresh verification session requires a zero CAD process baseline");
                Type type = Type.GetTypeFromProgID("SldWorks.Application.28", true);
                ISldWorks sw = Activator.CreateInstance(type) as ISldWorks;
                Require(sw != null, "SOLIDWORKS_2020_CREATE_FAILED", 30,
                    "exact SolidWorks 2020 COM activation failed");
                session.sw = sw;
                evidence.created = true;
                SafeAction(() => sw.Visible = false);
                SafeAction(() => sw.UserControl = false);
                int pid = sw.GetProcessID();
                long startTicks = ProcessStartUtcTicks(pid, "SLDWORKS");
                string executable = ProcessExecutablePath(pid, "SLDWORKS");
                string revision = Safe(() => sw.RevisionNumber(), "");
                Require(pid > 0 && !sldworksBefore.Contains(pid) && startTicks > 0 &&
                    SamePath(executable, ExpectedSolidWorksExePath) && File.Exists(executable) &&
                    string.Equals(Sha256(executable), ExpectedSolidWorksExeSha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    revision.StartsWith(ExpectedSolidWorksVersionPrefix,
                        StringComparison.Ordinal),
                    "SOLIDWORKS_2020_IDENTITY_MISMATCH", 30,
                    "SolidWorks session PID/start/executable/hash/version identity is not exact");
                evidence.started = true;
                evidence.sldworksProcessId = pid;
                evidence.sldworksStartUtcTicks = startTicks;
                evidence.solidworksRevision = revision;
                evidence.solidworksExecutablePath = executable;
                evidence.solidworksExecutableSha256 = ExpectedSolidWorksExeSha256;
                evidence.solidworks2020ExactGate = true;
                result.processes.createdSldworks.Add(pid);
                CaptureOwnedMonitors(result, session, monitorsBefore);
                evidence.monitorIdentityGate = ProcessIds("sldProcMon").All(id =>
                    evidence.sldprocmonProcesses.Any(row => row.pid == id && row.ownershipVerified));
                Require(evidence.monitorIdentityGate, "MONITOR_IDENTITY_UNPROVEN", 30,
                    "every sldProcMon process must have exact executable/hash/start/parent proof");
                return session;
            }
            catch
            {
                CloseOwnedSession(result, session);
                throw;
            }
        }

        private static void CloseOwnedSession(RunResult result, OwnedSession session)
        {
            if (session == null || session.evidence == null) return;
            SessionEvidence evidence = session.evidence;
            if (evidence.processExited && evidence.monitorsExited) return;
            try
            {
                if (session.model != null && session.sw != null)
                {
                    string title = Safe(() => session.model.GetTitle(), "");
                    if (!string.IsNullOrWhiteSpace(title)) SafeAction(() => session.sw.CloseDoc(title));
                }
                if (session.sw != null)
                {
                    CaptureOwnedMonitors(result, session, new List<int>());
                    SafeAction(() => session.sw.CloseAllDocuments(true));
                    SafeAction(() => session.sw.ExitApp());
                    evidence.exitRequested = true;
                }
            }
            finally
            {
                FreeCom(session.model);
                session.model = null;
                FreeCom(session.sw);
                session.sw = null;
            }

            evidence.processExited = WaitForExactProcessExit(evidence, 15000);
            if (!evidence.processExited) ForceStopOwnedSldworks(result, evidence);
            CleanupOwnedMonitors(result, evidence);
            evidence.monitorsExited = evidence.sldprocmonProcesses.All(process =>
                ProcessStartUtcTicks(process.pid, "sldProcMon") == 0);
            evidence.completedAtUtc = DateTime.UtcNow.ToString("o",
                CultureInfo.InvariantCulture);
        }

        private static void CleanupOwnedProcesses(RunResult result)
        {
            if (result == null) return;
            foreach (SessionEvidence evidence in result.sessions)
            {
                if (!evidence.processExited)
                    ForceStopOwnedSldworks(result, evidence);
                CleanupOwnedMonitors(result, evidence);
                evidence.processExited = ProcessStartUtcTicks(evidence.sldworksProcessId,
                    "SLDWORKS") == 0;
                evidence.monitorsExited = evidence.sldprocmonProcesses.All(process =>
                    ProcessStartUtcTicks(process.pid, "sldProcMon") == 0);
            }
        }

        private static void CaptureOwnedMonitors(RunResult result, OwnedSession session,
            List<int> monitorsBefore)
        {
            if (session == null || session.evidence == null ||
                session.evidence.sldworksProcessId <= 0) return;
            var before = new HashSet<int>(monitorsBefore ?? new List<int>());
            var assigned = new HashSet<int>(result.sessions.SelectMany(row =>
                row.sldprocmonProcesses.Select(process => process.pid)));
            string pattern = ExactParentPattern(session.evidence.sldworksProcessId);
            for (int retry = 0; retry < 20; retry++)
            {
                foreach (ProcessIdentity identity in ProcessInfoByName("sldProcMon"))
                {
                    if (assigned.Contains(identity.pid)) continue;
                    bool trusted = !before.Contains(identity.pid) &&
                        !result.processes.baselineSldprocmon.Contains(identity.pid) &&
                        identity.startUtcTicks > 0 &&
                        SamePath(identity.executablePath, ExpectedMonitorExePath) &&
                        File.Exists(identity.executablePath) &&
                        string.Equals(Sha256(identity.executablePath),
                            ExpectedMonitorExeSha256, StringComparison.OrdinalIgnoreCase) &&
                        Regex.IsMatch(identity.commandLine ?? "", pattern,
                            RegexOptions.CultureInvariant);
                    if (!trusted) continue;
                    identity.parentSldworksProcessId = session.evidence.sldworksProcessId;
                    identity.ownershipVerified = true;
                    identity.executableSha256 = ExpectedMonitorExeSha256;
                    session.evidence.sldprocmonProcesses.Add(identity);
                    assigned.Add(identity.pid);
                    if (!result.processes.createdSldprocmon.Contains(identity.pid))
                        result.processes.createdSldprocmon.Add(identity.pid);
                }
                List<int> unassigned = ProcessIds("sldProcMon").Where(id =>
                    !before.Contains(id) && !assigned.Contains(id)).ToList();
                if (unassigned.Count == 0) break;
                Thread.Sleep(250);
            }
            List<int> unknown = ProcessIds("sldProcMon").Where(id =>
                !before.Contains(id) && !assigned.Contains(id)).ToList();
            Require(unknown.Count == 0, "UNOWNED_MONITOR_PROCESS", 30,
                "a new sldProcMon process lacks exact parent identity: " +
                string.Join(",", unknown));
        }

        private static string ExactParentPattern(int parentPid)
        {
            return "(?:^|\\s)--ppid=" + parentPid.ToString(CultureInfo.InvariantCulture) +
                "(?:\\s|$)";
        }

        private static void CleanupOwnedMonitors(RunResult result, SessionEvidence session)
        {
            if (session == null) return;
            foreach (ProcessIdentity expected in session.sldprocmonProcesses)
            {
                ProcessIdentity live = ExactOwnedMonitor(expected,
                    session.sldworksProcessId);
                if (live == null)
                {
                    if (ProcessStartUtcTicks(expected.pid, "sldProcMon") == 0)
                        AddUnique(result.processes.alreadyExited, expected.pid);
                    else result.processes.cleanupErrors.Add(
                        "refused unproven or reused sldProcMon PID " + expected.pid);
                    continue;
                }
                try
                {
                    using (Process process = Process.GetProcessById(expected.pid))
                    {
                        process.Kill();
                        process.WaitForExit(10000);
                    }
                    AddUnique(result.processes.forceStopped, expected.pid);
                }
                catch (ArgumentException)
                { AddUnique(result.processes.alreadyExited, expected.pid); }
                catch (Exception ex)
                { result.processes.cleanupErrors.Add("sldProcMon PID " + expected.pid + ": " +
                    ex.Message); }
            }
        }

        private static ProcessIdentity ExactOwnedMonitor(ProcessIdentity expected, int parentPid)
        {
            if (expected == null || !expected.ownershipVerified ||
                expected.parentSldworksProcessId != parentPid) return null;
            string pattern = ExactParentPattern(parentPid);
            return ProcessInfoByName("sldProcMon").FirstOrDefault(current =>
                current.pid == expected.pid && current.startUtcTicks == expected.startUtcTicks &&
                SamePath(current.executablePath, expected.executablePath) &&
                File.Exists(current.executablePath) &&
                string.Equals(Sha256(current.executablePath), expected.executableSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(current.commandLine ?? "", pattern,
                    RegexOptions.CultureInvariant));
        }

        private static void ForceStopOwnedSldworks(RunResult result, SessionEvidence session)
        {
            if (session == null || session.sldworksProcessId <= 0) return;
            int pid = session.sldworksProcessId;
            if (!ExactProcessIdentityExists(pid, "SLDWORKS", session.sldworksStartUtcTicks,
                    session.solidworksExecutablePath, ExpectedSolidWorksExeSha256))
            {
                if (ProcessStartUtcTicks(pid, "SLDWORKS") == 0)
                    AddUnique(result.processes.alreadyExited, pid);
                else result.processes.cleanupErrors.Add(
                    "refused unproven or reused SLDWORKS PID " + pid);
                return;
            }
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
                AddUnique(result.processes.forceStopped, pid);
            }
            catch (ArgumentException)
            { AddUnique(result.processes.alreadyExited, pid); }
            catch (Exception ex)
            { result.processes.cleanupErrors.Add("SLDWORKS PID " + pid + ": " + ex.Message); }
            session.processExited = ProcessStartUtcTicks(pid, "SLDWORKS") == 0;
        }

        private static bool WaitForExactProcessExit(SessionEvidence session, int timeoutMs)
        {
            if (session == null || session.sldworksProcessId <= 0) return true;
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                if (ProcessStartUtcTicks(session.sldworksProcessId, "SLDWORKS") == 0) return true;
                if (!ExactProcessIdentityExists(session.sldworksProcessId, "SLDWORKS",
                        session.sldworksStartUtcTicks, session.solidworksExecutablePath,
                        ExpectedSolidWorksExeSha256)) return false;
                Thread.Sleep(200);
            }
            return ProcessStartUtcTicks(session.sldworksProcessId, "SLDWORKS") == 0;
        }

        private static void CaptureFinalProcessGate(RunResult result)
        {
            if (result == null) return;
            result.processes.finalSldworks = ProcessIds("SLDWORKS");
            result.processes.finalSldprocmon = ProcessIds("sldProcMon");
            result.processes.finalGate = result.processes.finalSldworks.Count == 0 &&
                result.processes.finalSldprocmon.Count == 0 &&
                result.processes.cleanupErrors.Count == 0 &&
                result.sessions.All(session => session.processExited && session.monitorsExited);
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

        private static bool ExactProcessIdentityExists(int pid, string name, long startTicks,
            string executablePath, string executableSha256)
        {
            string livePath = ProcessExecutablePath(pid, name);
            return pid > 0 && startTicks > 0 && ProcessStartUtcTicks(pid, name) == startTicks &&
                SamePath(livePath, executablePath) && File.Exists(livePath) &&
                string.Equals(Sha256(livePath), executableSha256,
                    StringComparison.OrdinalIgnoreCase);
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
                        int pid = Convert.ToInt32(row["ProcessId"],
                            CultureInfo.InvariantCulture);
                        output.Add(new ProcessIdentity
                        {
                            pid = pid,
                            name = Path.GetFileNameWithoutExtension(Convert.ToString(row["Name"],
                                CultureInfo.InvariantCulture) ?? ""),
                            commandLine = Convert.ToString(row["CommandLine"],
                                CultureInfo.InvariantCulture) ?? "",
                            executablePath = Convert.ToString(row["ExecutablePath"],
                                CultureInfo.InvariantCulture) ?? "",
                            startUtcTicks = ProcessStartUtcTicks(pid, name)
                        });
                    }
            }
            catch (Exception ex)
            {
                if (ProcessIds(name).Count > 0)
                    throw new StageException("PROCESS_IDENTITY_QUERY_FAILED", 30,
                        "cannot prove " + name + " process identity: " + ex.Message);
            }
            return output;
        }

        private static void RollbackAllWrites(RunResult result)
        {
            if (result == null || !result.processes.finalGate) return;
            result.transaction.rollbackAttempted = true;
            try
            {
                if (RollbackOwnsArtifact(result.transaction.receiptCommitted))
                    RemoveOrQuarantineUnpairedArtifact(result, result.receiptPath,
                        "final-receipt");
                if (RollbackOwnsArtifact(result.transaction.evidenceCommitted))
                    RemoveOrQuarantineUnpairedArtifact(result, result.evidencePath,
                        "final-evidence");
                if (RollbackOwnsArtifact(result.transaction.relocatedProbeCreated) &&
                    Directory.Exists(result.relocatedProbePath))
                    SafeDeleteOwnedDirectory(result.relocatedProbePath, result.attemptDir,
                        ".final-pack-relocated-probe");
                if (RollbackOwnsArtifact(result.transaction.finalPackageCreated) &&
                    Directory.Exists(result.finalPackagePath))
                    SafeDeleteOwnedDirectory(result.finalPackagePath, result.attemptDir,
                        FinalPackageLeaf);

                if (!string.IsNullOrWhiteSpace(result.transaction.backupDirectory) &&
                    Directory.Exists(result.transaction.backupDirectory))
                {
                    if (RollbackBackupRequiresFullInventory(
                            result.transaction.backupComplete,
                            result.transaction.mutationStarted))
                    {
                        List<string> backups = CaptureFlatCadTree(
                            result.transaction.backupDirectory, 88,
                            "ROLLBACK_BACKUP_INVALID");
                        Require(string.Equals(InventoryEvidenceFor(backups).digest,
                                result.transaction.backupDigest,
                                StringComparison.OrdinalIgnoreCase),
                            "ROLLBACK_BACKUP_DIGEST_MISMATCH", 60,
                            "rollback backup no longer matches its preflight digest");
                        string[] currentDirectories = Directory.Exists(result.workingPackPath) ?
                            Directory.GetDirectories(result.workingPackPath, "*",
                                SearchOption.TopDirectoryOnly) : new string[0];
                        Require(currentDirectories.Length == 0,
                            "ROLLBACK_WORKING_PACK_NOT_FLAT", 60,
                            "rollback refuses a working pack with unexpected subdirectories");
                        foreach (string current in Directory.GetFiles(result.workingPackPath, "*",
                            SearchOption.TopDirectoryOnly))
                        {
                            Require((IsAssembly(current) || IsPart(current)) &&
                                IsUnder(current, result.workingPackPath) &&
                                !HasReparsePoint(current) && FileLinkCount(current) == 1,
                                "ROLLBACK_UNEXPECTED_WORKING_FILE", 60,
                                "rollback refuses an unexpected or linked working-pack file");
                            File.Delete(current);
                        }
                        foreach (string backup in backups)
                            File.Copy(backup, Path.Combine(result.workingPackPath,
                                Path.GetFileName(backup)), false);
                        InventoryEvidence restored = InventoryEvidenceFor(CaptureFlatCadTree(
                            result.workingPackPath, 88, "ROLLBACK_RESTORED_INVALID"));
                        Require(string.Equals(restored.digest, result.preInventory.digest,
                                StringComparison.OrdinalIgnoreCase),
                            "ROLLBACK_RESTORED_DIGEST_MISMATCH", 60,
                            "restored working pack differs from its initial digest");
                        result.transaction.rollbackRestoredDigest = restored.digest;
                    }
                    SafeDeleteOwnedDirectory(result.transaction.backupDirectory,
                        result.attemptDir, Path.GetFileName(result.transaction.backupDirectory));
                }
                for (int index = result.transaction.createdDirectories.Count - 1;
                    index >= 0; index--)
                {
                    string directory = result.transaction.createdDirectories[index];
                    if (Directory.Exists(directory) &&
                        Directory.GetFileSystemEntries(directory).Length == 0 &&
                        IsUnder(directory, result.attemptDir) && !HasReparsePoint(directory))
                        Directory.Delete(directory, false);
                }
                result.transaction.rollbackCompleted =
                    (!result.transaction.finalPackageCreated ||
                        !Directory.Exists(result.finalPackagePath)) &&
                    (!result.transaction.relocatedProbeCreated ||
                        !Directory.Exists(result.relocatedProbePath)) &&
                    (!result.transaction.evidenceCommitted ||
                        !File.Exists(result.evidencePath)) &&
                    (!result.transaction.receiptCommitted ||
                        !File.Exists(result.receiptPath)) &&
                    RollbackArtifactsGone(result.transaction.unpairedArtifactQuarantinePaths
                        .Select(path => !File.Exists(path)));
                Require(result.transaction.rollbackCompleted, "ROLLBACK_INCOMPLETE", 60,
                    "one or more final-stage writes remain after rollback");
            }
            catch (Exception ex)
            {
                result.transaction.rollbackCompleted = false;
                result.transaction.rollbackError = SafeException(ex);
            }
        }

        private static bool RollbackBackupRequiresFullInventory(bool backupComplete,
            bool mutationStarted)
        {
            return backupComplete && mutationStarted;
        }

        private static bool RollbackArtifactsGone(IEnumerable<bool> artifactGone)
        {
            return artifactGone != null && artifactGone.All(value => value);
        }

        private static bool RollbackOwnsArtifact(bool createdByCurrentTransaction)
        {
            return createdByCurrentTransaction;
        }

        private static void SafeDeleteOwnedDirectory(string path, string attemptRoot,
            string exactLeaf)
        {
            string full = Path.GetFullPath(path);
            Require(IsUnder(full, attemptRoot) &&
                string.Equals(Path.GetFileName(full), exactLeaf,
                    StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(full) && !HasReparsePoint(full),
                "OWNED_DIRECTORY_DELETE_SCOPE_INVALID", 60,
                "recursive delete target is not the exact owned attempt directory: " + full);
            AssertPathChainNoReparse(full, attemptRoot, "OWNED_DIRECTORY_REPARSE_PATH");
            Directory.Delete(full, true);
        }

        private static void RemoveOrQuarantineUnpairedArtifact(RunResult result, string path,
            string label)
        {
            if (result == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            if (!IsUnder(path, result.attemptDir) || HasReparsePoint(path) ||
                FileLinkCount(path) != 1)
            {
                result.transaction.rollbackError = FirstNonEmpty(
                    result.transaction.rollbackError,
                    "refused to remove unproven " + label + " path");
                return;
            }
            try { File.Delete(path); }
            catch { }
            if (!File.Exists(path)) return;
            string quarantine = path + ".unpaired-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Move(path, quarantine);
                try { File.Delete(quarantine); }
                catch { }
                if (File.Exists(quarantine))
                    result.transaction.unpairedArtifactQuarantinePaths.Add(quarantine);
            }
            catch (Exception ex)
            {
                result.transaction.rollbackError = FirstNonEmpty(
                    result.transaction.rollbackError,
                    "cannot remove or quarantine " + label + ": " + ex.Message);
            }
        }

        private static Dictionary<string, object> BuildEvidence(RunResult result)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", result.schema }, { "purpose", Purpose },
                { "status", result.status }, { "success", result.success },
                { "engineerReviewRequired", true },
                { "completed_at_utc", result.completed_at_utc },
                { "task", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", result.taskId }, { "revision", result.plan.taskRevision },
                        { "digest", result.plan.taskDigest }, { "attempt", result.attempt },
                        { "requestDigest", result.plan.requestDigest }
                    } },
                { "recipe", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", RecipeId }, { "version", 1 }, { "digest", RecipeDigest }
                    } },
                { "plan", new Dictionary<string, object>(StringComparer.Ordinal)
                    { { "sha256", result.plan.sha256 }, { "workerId", result.plan.workerId } } },
                { "tool", result.tool },
                { "authorization", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "id", result.authorization.id },
                        { "sha256", result.authorization.sha256 },
                        { "issuedAt", result.authorization.issuedAt },
                        { "expiresAt", result.authorization.expiresAt },
                        { "leaseId", result.authorization.leaseId },
                        { "leaseExpiresAt", result.authorization.leaseExpiresAt }
                    } },
                { "predecessorRootReceiptSha256", result.predecessorReceiptSha256 },
                { "paths", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "workingPack", result.workingPackPath },
                        { "finalPackage", result.finalPackagePath },
                        { "finalRoot", result.finalRootPath }
                    } },
                { "preInventory", result.preInventory },
                { "postInventory", result.postInventory },
                { "packaging", result.packaging },
                { "finalVerification", result.finalVerification },
                { "relocatedVerification", result.relocatedVerification },
                { "sessions", result.sessions }, { "processes", result.processes },
                { "transaction", new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "backupComplete", result.transaction.backupComplete },
                        { "backupFileCount", result.transaction.backupFileCount },
                        { "backupDigest", result.transaction.backupDigest },
                        { "inputUnchanged", result.transaction.inputUnchanged },
                        { "finalPackageCreated", result.transaction.finalPackageCreated },
                        { "relocatedProbeCreated", result.transaction.relocatedProbeCreated },
                        { "relocatedProbeRemoved", result.transaction.relocatedProbeRemoved }
                    } },
                { "quality_boundary", result.quality_boundary },
                { "authorization_checkpoints", result.authorization_checkpoints },
                { "backup_deleted", result.backup_deleted },
                { "backup_delete_error", result.backup_delete_error },
                { "evidence_commitment_sha256", result.evidence_commitment_sha256 }
            };
        }

        private static string EvidenceCommitmentDigest(RunResult result)
        {
            return EvidenceCommitmentDigest(BuildEvidence(result));
        }

        private static string EvidenceCommitmentDigest(Dictionary<string, object> evidence)
        {
            Require(evidence != null, "EVIDENCE_COMMITMENT_OBJECT_REQUIRED", 71,
                "evidence commitment requires a JSON object");
            var projection = new Dictionary<string, object>(evidence,
                StringComparer.Ordinal);
            foreach (string key in CommitmentExcludedKeys) projection.Remove(key);
            return Sha256Text(StableJson(projection));
        }

        private static void WriteJsonAtomicNew(string path, RunResult result)
        {
            WriteJsonAtomicNew(path, BuildEvidence(result));
        }

        private static void WriteJsonAtomicNew(string path, object value)
        {
            WriteJsonAtomicNew(path, value, null);
        }

        private static void WriteJsonAtomicNew(string path, object value, Action afterCreate)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            byte[] bytes = new UTF8Encoding(false).GetBytes(Serialize(value) + "\n");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (afterCreate != null) afterCreate();
            Require(File.Exists(path) && !HasReparsePoint(path) && FileLinkCount(path) == 1 &&
                File.ReadAllBytes(path).SequenceEqual(bytes),
                "ATOMIC_CREATE_NEW_VERIFY_FAILED", 72,
                "immutable JSON bytes differ after CreateNew commit: " + path);
        }

        private static void ValidateCommittedEvidence(RunResult result)
        {
            Require(File.Exists(result.evidencePath) && !HasReparsePoint(result.evidencePath) &&
                FileLinkCount(result.evidencePath) == 1 &&
                string.Equals(Sha256(result.evidencePath), result.evidenceSha256,
                    StringComparison.OrdinalIgnoreCase),
                "FINAL_EVIDENCE_FILE_INVALID", 72,
                "committed final evidence is missing, linked or hash-mismatched");
            Dictionary<string, object> evidence = ReadJsonObject(result.evidencePath);
            Require(Text(evidence, "schema") == ResultSchema &&
                Text(evidence, "purpose") == Purpose && Bool(evidence, "success") &&
                !evidence.ContainsKey("phase_receipt_path") &&
                !evidence.ContainsKey("phase_receipt_sha256") &&
                !evidence.ContainsKey("phase_receipt_committed") &&
                IsSha256(Text(evidence, "evidence_commitment_sha256")) &&
                string.Equals(Text(evidence, "evidence_commitment_sha256"),
                    result.evidence_commitment_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(EvidenceCommitmentDigest(evidence),
                    result.evidence_commitment_sha256,
                    StringComparison.OrdinalIgnoreCase),
                "FINAL_EVIDENCE_SEMANTIC_COMMITMENT_INVALID", 72,
                "committed evidence semantic projection differs from the shared commitment");
        }

        private static void WriteFinalReceiptAtomic(RunResult result)
        {
            var receipt = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", ReceiptSchema }, { "phase", ExecutionPhase },
                { "success", true }, { "completedAt", result.completed_at_utc },
                { "taskId", result.taskId }, { "taskRevision", result.plan.taskRevision },
                { "taskDigest", result.plan.taskDigest },
                { "requestDigest", result.plan.requestDigest },
                { "leaseId", result.authorization.leaseId },
                { "leaseExpiresAt", result.authorization.leaseExpiresAt },
                { "attempt", result.attempt }, { "planSha256", result.plan.sha256 },
                { "authorizationId", result.authorization.id },
                { "authorizationSha256", result.authorization.sha256 },
                { "authorizationJsonBase64", result.authorization.jsonBase64 },
                { "authorizationIssuedAt", result.authorization.issuedAt },
                { "authorizationExpiresAt", result.authorization.expiresAt },
                { "recipeId", RecipeId }, { "recipeDigest", RecipeDigest },
                { "toolId", ToolId },
                { "toolSourceNormalizedSha256", result.tool.sourceNormalizedSha256 },
                { "toolExecutableSha256", result.tool.executableSha256 },
                { "evidencePath", EvidenceRelativePath },
                { "evidenceSha256", result.evidenceSha256 },
                { "evidenceCommitmentSha256", result.evidence_commitment_sha256 },
                { "preInventoryDigest", result.preInventory.digest },
                { "postInventoryDigest", result.postInventory.digest },
                { "predecessorReceiptSha256", result.predecessorReceiptSha256 }
            };
            RequireExactKeys(receipt, "final receipt", RuntimeReceiptKeys);
            WriteJsonAtomicNew(result.receiptPath, receipt);
            result.transaction.receiptCommitted = true;
            Dictionary<string, object> committed = ReadJsonObject(result.receiptPath);
            RequireExactKeys(committed, "committed final receipt", RuntimeReceiptKeys);
            Require(Text(committed, "schema") == ReceiptSchema &&
                Text(committed, "phase") == ExecutionPhase && Bool(committed, "success") &&
                Text(committed, "evidencePath") == EvidenceRelativePath &&
                string.Equals(Text(committed, "evidenceSha256"),
                    result.evidenceSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(committed, "evidenceCommitmentSha256"),
                    result.evidence_commitment_sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(committed, "preInventoryDigest"),
                    result.preInventory.digest, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(committed, "postInventoryDigest"),
                    result.postInventory.digest, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(committed, "predecessorReceiptSha256"),
                    result.predecessorReceiptSha256, StringComparison.OrdinalIgnoreCase) &&
                Convert.FromBase64String(Text(committed, "authorizationJsonBase64"))
                    .SequenceEqual(File.ReadAllBytes(result.authorization.path)),
                "FINAL_RECEIPT_COMMIT_VERIFY_FAILED", 73,
                "committed final receipt does not exactly bind evidence, auth and inventories");
        }

        private static List<string> CaptureFlatCadTree(string root, int exactCount,
            string status)
        {
            Require(Directory.Exists(root) && !HasReparsePoint(root), status, 70,
                "CAD directory is missing or reparse: " + root);
            string[] directories = Directory.GetDirectories(root, "*",
                SearchOption.TopDirectoryOnly);
            Require(directories.Length == 0, status, 70,
                "CAD inventory must remain flat with no nested directories");
            string[] allFiles = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly);
            Require(allFiles.Length == exactCount, status, 70,
                "CAD inventory has an unexpected physical file count");
            var cad = new List<string>();
            foreach (string file in allFiles)
            {
                string full = Path.GetFullPath(file);
                Require((IsPart(full) || IsAssembly(full)) && !HasReparsePoint(full) &&
                    FileLinkCount(full) == 1 && SamePath(Path.GetDirectoryName(full), root),
                    status, 70,
                    "CAD inventory may contain only direct non-reparse single-link files: " + full);
                cad.Add(full);
            }
            return cad.OrderBy(path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static InventoryEvidence InventoryEvidenceFor(IEnumerable<string> paths)
        {
            List<string> rows = (paths ?? new string[0]).OrderBy(path =>
                Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToList();
            var evidence = new InventoryEvidence
            {
                fileCount = rows.Count,
                assemblyFileCount = rows.Count(IsAssembly),
                partFileCount = rows.Count(IsPart)
            };
            var digestText = new StringBuilder();
            foreach (string path in rows)
            {
                string sha = Sha256(path).ToUpperInvariant();
                string name = Path.GetFileName(path);
                evidence.files.Add(new InventoryFileEvidence
                {
                    name = name, sha256 = sha, sizeBytes = new FileInfo(path).Length
                });
                digestText.Append(name).Append('|').Append(sha).Append('\n');
            }
            evidence.digest = Sha256Text(digestText.ToString());
            return evidence;
        }

        private static List<string> ToStringList(object raw)
        {
            var output = new List<string>();
            Array array = raw as Array;
            if (array == null)
            {
                if (raw != null) output.Add(Convert.ToString(raw,
                    CultureInfo.InvariantCulture) ?? "");
                return output;
            }
            foreach (object value in array)
                output.Add(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
            return output;
        }

        private static List<int> ToIntList(object raw)
        {
            var output = new List<int>();
            Array array = raw as Array;
            if (array == null)
            {
                int value;
                if (raw != null && int.TryParse(Convert.ToString(raw,
                        CultureInfo.InvariantCulture), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value)) output.Add(value);
                return output;
            }
            foreach (object value in array)
                output.Add(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            return output;
        }

        private static List<string> RootedCadPaths(object raw)
        {
            var output = new List<string>();
            Array array = raw as Array;
            if (array == null) return output;
            foreach (object value in array)
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                if (Path.IsPathRooted(text) && (IsPart(text) || IsAssembly(text)))
                    output.Add(Path.GetFullPath(text));
            }
            return output;
        }

        private static Dictionary<string, object> ReadJsonObject(string path)
        {
            object value = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 }.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
            Dictionary<string, object> output = value as Dictionary<string, object>;
            Require(output != null, "JSON_OBJECT_REQUIRED", 71,
                "JSON artifact must contain an object: " + path);
            return output;
        }

        private static Dictionary<string, object> ChildObject(Dictionary<string, object> parent,
            string name)
        {
            object value;
            Dictionary<string, object> child;
            return parent != null && parent.TryGetValue(name, out value) &&
                (child = value as Dictionary<string, object>) != null ? child :
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static object[] ArrayValue(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return new object[0];
            object[] direct = value as object[];
            if (direct != null) return direct;
            ArrayList list = value as ArrayList;
            return list == null ? new object[0] : list.Cast<object>().ToArray();
        }

        private static string Text(Dictionary<string, object> parent, string name)
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

        private static double Number(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return double.NaN;
            return Safe(() => Convert.ToDouble(value, CultureInfo.InvariantCulture), double.NaN);
        }

        private static bool Bool(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) && value is bool &&
                (bool)value;
        }

        private static bool StringArrayEquals(object[] actual, string[] expected)
        {
            return actual != null && expected != null && actual.Length == expected.Length &&
                actual.Select(ValueText).SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static void RequireExactKeys(Dictionary<string, object> value, string label,
            params string[] expected)
        {
            Require(ExactKeys(value, expected), "EXACT_JSON_KEYS_MISMATCH", 71,
                label + " does not have the exact reviewed keys");
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
            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
                return "{" + string.Join(",", dictionary.Keys.OrderBy(key => key,
                    StringComparer.Ordinal).Select(key => JsonString(key) + ":" +
                    StableJson(dictionary[key])).ToArray()) + "}";
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
                        { builder.Append(character).Append(value[index + 1]); index++; }
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
                if (double.TryParse(current, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out parsed) &&
                    BitConverter.DoubleToInt64Bits(parsed) == bits)
                { candidate = current; break; }
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
                else output = digits.Substring(0, decimalPosition) + "." +
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

        private static bool LiveRepoRelativeManifestPath(Dictionary<string, object> tool,
            string pathKey, string hashKey, bool normalizedSource)
        {
            string relative = Text(tool, pathKey);
            string expected = Text(tool, hashKey);
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("\\") ||
                Path.IsPathRooted(relative) || relative.Split('/').Any(segment =>
                    segment == ".." || segment == ".") || !IsSha256(expected)) return false;
            string repositoryRoot = FindRepositoryRoot(
                Process.GetCurrentProcess().MainModule.FileName);
            if (string.IsNullOrWhiteSpace(repositoryRoot)) return false;
            string full = Path.GetFullPath(Path.Combine(repositoryRoot,
                relative.Replace('/', '\\')));
            if (!IsUnder(full, repositoryRoot) || !File.Exists(full) ||
                HasReparsePoint(full) || FileLinkCount(full) != 1) return false;
            string actual = normalizedSource ? NormalizedSourceHashAt(full) : Sha256(full);
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
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

        private static string NormalizedSourceHashAt(string path)
        {
            return Sha256Text(NormalizeIdentitySource(File.ReadAllText(path, Encoding.UTF8)));
        }

        private static string NormalizeIdentitySource(string source)
        {
            string normalized = source.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = Regex.Replace(normalized,
                "private const string ExpectedSourceSha256\\s*=\\s*\"(?:__SOURCE_SHA256__|[0-9A-F]{64})\";",
                delegate(Match match)
                {
                    return Regex.Replace(match.Value,
                        "(?:__SOURCE_SHA256__|[0-9A-F]{64})", "__SOURCE_SHA256__");
                }, RegexOptions.CultureInvariant);
            return Regex.Replace(normalized,
                "private const string ExpectedContractSnapshotSha256\\s*=\\s*\"(?:__CONTRACT_SHA256__|[0-9A-F]{64})\";",
                delegate(Match match)
                {
                    return Regex.Replace(match.Value,
                        "(?:__CONTRACT_SHA256__|[0-9A-F]{64})", "__CONTRACT_SHA256__");
                }, RegexOptions.CultureInvariant);
        }

        private static string NormalizedLockToolSourceHashAt(string path)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            source = Regex.Replace(source,
                "private const string ExpectedSourceSha256\\s*=\\s*\"(?:__SOURCE_SHA256__|[0-9A-F]{64})\";",
                delegate(Match match)
                {
                    return Regex.Replace(match.Value,
                        "(?:__SOURCE_SHA256__|[0-9A-F]{64})", "__SOURCE_SHA256__");
                }, RegexOptions.CultureInvariant);
            return Sha256Text(source);
        }

        private static string NormalizedSourceSha256()
        {
            string sourcePath = Path.Combine(Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName), "FinalPack888x14Native.cs");
            Require(File.Exists(sourcePath), "TOOL_SOURCE_MISSING", 3,
                "reviewed final-pack source must remain beside the executable");
            return NormalizedSourceHashAt(sourcePath);
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private static string Sha256Text(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(new UTF8Encoding(false)
                    .GetBytes(value))).Replace("-", "");
        }

        private static string Sha256Bytes(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(value)).Replace("-", "");
        }

        private static string Serialize(object value)
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 512 }.Serialize(value);
        }

        private static bool IsSha256(string value)
        {
            return value != null && Regex.IsMatch(value, "^[A-Fa-f0-9]{64}$",
                RegexOptions.CultureInvariant);
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

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string candidate = Path.GetFullPath(path).TrimEnd('\\', '/');
            string parent = Path.GetFullPath(root).TrimEnd('\\', '/');
            return candidate.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            return string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
                Path.GetFullPath(right).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string RelativeUnder(string path, string root, string status)
        {
            Require(IsUnder(path, root), status, 3, path + " is outside " + root);
            return Path.GetFullPath(path).Substring(
                Path.GetFullPath(root).TrimEnd('\\', '/').Length).TrimStart('\\', '/');
        }

        private static void AssertPathChainNoReparse(string path, string stopAt,
            string status)
        {
            Require(IsUnder(path, stopAt) || SamePath(path, stopAt), status, 3,
                "path escaped its trusted root");
            string current = Path.GetFullPath(path);
            string root = Path.GetFullPath(stopAt).TrimEnd('\\', '/');
            int guard = 0;
            while (++guard < 30)
            {
                if (File.Exists(current) || Directory.Exists(current))
                    Require(!HasReparsePoint(current), status, 3,
                        "path chain contains a reparse point: " + current);
                if (SamePath(current, root)) return;
                string parent = Path.GetDirectoryName(current);
                Require(!string.IsNullOrWhiteSpace(parent) && !SamePath(parent, current),
                    status, 3, "path chain did not reach its trusted root");
                current = parent;
            }
            Require(false, status, 3, "path chain traversal exceeded its limit");
        }

        private static bool HasReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
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

        private static int FileLinkCount(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    ByHandleFileInformation information;
                    return GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(),
                        out information) ? checked((int)information.numberOfLinks) : -1;
                }
            }
            catch { return -1; }
        }

        private static bool Near(double left, double right, double tolerance)
        {
            return !double.IsNaN(left) && !double.IsNaN(right) &&
                Math.Abs(left - right) <= tolerance;
        }

        private static string Token(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string LeafComponentName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            int slash = value.LastIndexOf('/');
            return slash < 0 ? value : value.Substring(slash + 1);
        }

        private static string FullPathOrEmpty(string value)
        {
            try { return string.IsNullOrWhiteSpace(value) ? "" :
                Path.GetFullPath(value.Trim()); }
            catch { return ""; }
        }

        private static bool ParseUtc(string value, out DateTime utc)
        {
            DateTime parsed;
            bool ok = DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed);
            utc = ok ? parsed.ToUniversalTime() : DateTime.MinValue;
            return ok;
        }

        private static bool IsSafeId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
                Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]*$",
                    RegexOptions.CultureInvariant);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (!values.Contains(value)) values.Add(value);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values == null ? "" : values.FirstOrDefault(value =>
                !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private static string SafeException(Exception exception)
        {
            return exception == null ? "" : exception.GetType().Name + ": " + exception.Message;
        }

        private static T Safe<T>(Func<T> operation, T fallback)
        {
            try { return operation(); }
            catch { return fallback; }
        }

        private static void SafeAction(Action operation)
        {
            try { operation(); }
            catch { }
        }

        private static void FreeCom(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); }
            catch { }
        }

        private static void Require(bool condition, string status, int exitCode,
            string message)
        {
            if (!condition) throw new StageException(status, exitCode, message);
        }

        private sealed class StageException : Exception
        {
            public readonly string Status;
            public readonly int ExitCode;
            public StageException(string status, int exitCode, string message) : base(message)
            { Status = status; ExitCode = exitCode; }
        }

        private sealed class CliOptions
        {
            public string attempt = "";
            public string plan = "";
            public string authorization = "";
            public string confirmTask = "";
        }

        private sealed class SelfTestRow
        {
            public string name = "";
            public bool passed;
            public string detail = "";
        }

        private sealed class RunResult
        {
            public string schema = "";
            public string purpose = "";
            public string status = "";
            public bool success;
            public bool engineerReviewRequired;
            public string completed_at_utc = "";
            public string evidence_commitment_sha256 = "";
            public bool backup_deleted;
            public string backup_delete_error = "";
            public string errorCode = "";
            public string error = "";
            public int exitCode = 1;
            public bool preflightPassed;
            public string attemptsRoot = "";
            public string attemptDir = "";
            public string taskId = "";
            public int attempt;
            public string planPath = "";
            public string authorizationPath = "";
            public string workingPackPath = "";
            public string workingRootPath = "";
            public string finalPackagePath = "";
            public string finalRootPath = "";
            public string relocatedProbePath = "";
            public string evidencePath = "";
            public string evidenceSha256 = "";
            public string receiptPath = "";
            public string rootReceiptPath = "";
            public string rootEvidencePath = "";
            public string predecessorReceiptSha256 = "";
            public string contractPath = "";
            public Dictionary<string, object> contractSnapshot =
                new Dictionary<string, object>(StringComparer.Ordinal);
            public ToolEvidence tool = new ToolEvidence();
            public PlanEvidence plan = new PlanEvidence();
            public AuthorizationEvidence authorization = new AuthorizationEvidence();
            public InventoryEvidence preInventory = new InventoryEvidence();
            public InventoryEvidence postInventory = new InventoryEvidence();
            public PackagingEvidence packaging = new PackagingEvidence();
            public VerificationEvidence finalVerification = new VerificationEvidence();
            public VerificationEvidence relocatedVerification = new VerificationEvidence();
            public ProcessEvidence processes = new ProcessEvidence();
            public TransactionEvidence transaction = new TransactionEvidence();
            public QualityBoundaryEvidence quality_boundary = new QualityBoundaryEvidence();
            public List<AuthorizationCheckpoint> authorization_checkpoints =
                new List<AuthorizationCheckpoint>();
            public List<TrustedArtifactEvidence> toolchainManifests =
                new List<TrustedArtifactEvidence>();
            public List<SessionEvidence> sessions = new List<SessionEvidence>();
        }

        private sealed class ToolEvidence
        {
            public string id = "";
            public string sourceNormalizedSha256 = "";
            public string executableSha256 = "";
        }

        private sealed class PlanEvidence
        {
            public string path = "";
            public string sha256 = "";
            public string workerId = "";
            public int taskRevision;
            public string taskDigest = "";
            public string requestFingerprint = "";
            public string requestDigest = "";
            public Dictionary<string, object> raw =
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private sealed class AuthorizationEvidence
        {
            public string id = "";
            public string path = "";
            public string sha256 = "";
            public string jsonBase64 = "";
            public string issuedAt = "";
            public string expiresAt = "";
            public string leaseId = "";
            public string leaseExpiresAt = "";
            public Dictionary<string, object> raw =
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private sealed class AuthorizationCheckpoint
        {
            public string name = "";
            public string checkedAtUtc = "";
            public bool valid;
        }

        private sealed class TrustedArtifactEvidence
        {
            public string id = "";
            public string path = "";
            public string sha256 = "";
            public string sourceNormalizedSha256 = "";
            public string executableSha256 = "";
        }

        private sealed class InventoryEvidence
        {
            public string digest = "";
            public int fileCount;
            public int assemblyFileCount;
            public int partFileCount;
            public List<InventoryFileEvidence> files = new List<InventoryFileEvidence>();
        }

        private sealed class InventoryFileEvidence
        {
            public string name = "";
            public string sha256 = "";
            public long sizeBytes;
        }

        private sealed class PackagingEvidence
        {
            public int sourceOpenErrors;
            public int sourceOpenWarnings;
            public bool sourceSaved;
            public int documentCount;
            public bool packAndGoCompleted;
            public List<string> sourceDocumentNames = new List<string>();
            public List<string> targetDocumentNames = new List<string>();
            public List<int> saveStatuses = new List<int>();
            public RootSnapshot sourceSnapshot;
        }

        private sealed class VerificationEvidence
        {
            public int openErrors;
            public int openWarnings;
            public bool passed;
            public string inventoryDigest = "";
            public RootSnapshot snapshot;
        }

        private sealed class RootSnapshot
        {
            public bool traversalComplete;
            public int topLevelComponentCount;
            public int recursiveComponentCount;
            public int activeRecursiveComponentCount;
            public int suppressedComponentCount;
            public int doorModuleCount;
            public int shelfModuleCount;
            public int activeFrameCrossbarCount;
            public int mechanicalTongueCount;
            public int cadFileCount;
            public int assemblyFileCount;
            public int partFileCount;
            public int missingFileCount;
            public int externalFileCount;
            public bool dependenciesAllLocal;
            public string signature = "";
            public List<ComponentEvidence> topLevelComponents = new List<ComponentEvidence>();
            public List<ComponentEvidence> doors = new List<ComponentEvidence>();
            public List<ComponentEvidence> shelves = new List<ComponentEvidence>();
            public List<ComponentEvidence> frameCrossbars = new List<ComponentEvidence>();
            public List<ComponentEvidence> tongues = new List<ComponentEvidence>();
            public List<string> closurePaths = new List<string>();
            public List<string> forbiddenReferenceLeaves = new List<string>();
            public FeatureHealthEvidence featureHealth = new FeatureHealthEvidence();
        }

        private sealed class ComponentEvidence
        {
            public string name = "";
            public string path = "";
            public bool active;
            public bool fixedState;
            public List<double> transform = new List<double>();
            public List<double> totalTransform = new List<double>();
        }

        private sealed class FeatureHealthEvidence
        {
            public bool traversalComplete;
            public int featureCount;
            public List<FeatureIssueEvidence> issues = new List<FeatureIssueEvidence>();
        }

        private sealed class FeatureIssueEvidence
        {
            public string name = "";
            public string type = "";
            public int errorCode;
            public int errorCode2;
            public bool warning;
            public int depth;
        }

        private sealed class SessionEvidence
        {
            public string purpose = "";
            public string createdAtUtc = "";
            public bool created;
            public bool started;
            public int sldworksProcessId;
            public long sldworksStartUtcTicks;
            public string solidworksRevision = "";
            public string solidworksExecutablePath = "";
            public string solidworksExecutableSha256 = "";
            public bool solidworks2020ExactGate;
            public bool monitorIdentityGate;
            public bool exitRequested;
            public bool processExited;
            public bool monitorsExited;
            public string completedAtUtc = "";
            public List<ProcessIdentity> sldprocmonProcesses = new List<ProcessIdentity>();
        }

        private sealed class ProcessIdentity
        {
            public int pid;
            public long startUtcTicks;
            public string name = "";
            public string executablePath = "";
            public string executableSha256 = "";
            public string commandLine = "";
            public int parentSldworksProcessId;
            public bool ownershipVerified;
        }

        private sealed class ProcessEvidence
        {
            public List<int> baselineSldworks = new List<int>();
            public List<int> baselineSldprocmon = new List<int>();
            public List<int> createdSldworks = new List<int>();
            public List<int> createdSldprocmon = new List<int>();
            public List<int> alreadyExited = new List<int>();
            public List<int> forceStopped = new List<int>();
            public List<string> cleanupErrors = new List<string>();
            public List<int> finalSldworks = new List<int>();
            public List<int> finalSldprocmon = new List<int>();
            public bool finalGate;
        }

        private sealed class TransactionEvidence
        {
            public string lockPath = "";
            public bool lockAcquired;
            public string backupDirectory = "";
            public bool backupComplete;
            public int backupFileCount;
            public string backupDigest = "";
            public bool mutationStarted;
            public bool inputUnchanged;
            public bool finalPackageCreated;
            public bool relocatedProbeCreated;
            public bool relocatedProbeRemoved;
            public bool evidenceCommitted;
            public bool receiptCommitted;
            public bool rollbackAttempted;
            public bool rollbackCompleted;
            public bool rollbackBlockedByProcessGate;
            public string rollbackError = "";
            public string rollbackRestoredDigest = "";
            public List<string> createdDirectories = new List<string>();
            public List<string> unpairedArtifactQuarantinePaths = new List<string>();
        }

        private sealed class QualityBoundaryEvidence
        {
            public string purpose = "";
            public bool engineerReviewRequired = true;
        }

        private sealed class OwnedSession
        {
            public ISldWorks sw;
            public ModelDoc2 model;
            public SessionEvidence evidence;
        }
    }
}
