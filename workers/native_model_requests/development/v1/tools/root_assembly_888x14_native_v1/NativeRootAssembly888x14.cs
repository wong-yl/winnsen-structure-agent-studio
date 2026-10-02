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

namespace Winnsen.StructureAgent.NativeRootAssembly888x14
{
    internal static class NativeRootAssembly888x14
    {
        private const string ToolId = "native_root_assembly_888x14_v1";
        private const string ExecutionPhase = "root_assembly_888x14";
        private const string Purpose = "structure_engineering_assistance";
        private const string ExpectedRecipeId = "winnsen-16029-888w-14door-native-v1";
        private const string ExpectedRecipeDigest =
            "f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8";
        private const string ExpectedSourceSha256 = "E9C9970173579AFB313137A75EF98CDCD0828367625A6598C0B21BE483C19BF6";
        private const string ExpectedContractSnapshotSha256 = "AADC2C298E6B62396A0E4047B505F1E3533CA47FB1B149495D322500DA1AB278";
        private const string PlanSchema = "winnsen.native_build_plan.v1";
        private const string ContractSchema = "winnsen.native_16029_assembly_contract.v1";
        private const string AuthorizationSchema = "winnsen.native_execution_authorization.v1";
        private const string LockCompositeToolchainManifestSchema =
            "winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1";
        private const string LockToolId = "native_lock_topology_888x14_v1";
        private const string DoorManifestSchema =
            "winnsen.16029.native_door_module_flat_import_manifest.v1";
        private const string ResultSchema = "winnsen.16029.native_root_assembly_result.v1";
        private const string ManifestSchema =
            "winnsen.locker16029.native_888x14_root_assembly_manifest.v1";
        private const string WorkingPackLeaf = "working_pack";
        private const string WorkingRootFileName = "标准寄存柜1917×760×550(总装配).SLDASM";
        private const string FrameFileName = "门框焊接.sldasm";
        private const string DoorImportManifestFileName = "door_module_flat_import_manifest.json";
        private const string ResultFileName = "root_assembly_888x14.result.v1.json";
        private const string RootManifestFileName = "root_assembly_888x14.v1.json";
        private const string RootReceiptRelativePath = "receipts/root_assembly_888x14.json";
        private const string RootReceiptSchema =
            "winnsen.16029.native_root_assembly_receipt.v1";
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
        private const string RootAssemblerBoundary =
            "ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK";
        private const string TrustedV37SourceInventoryDigest =
            "9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B";
        private const double TransformTolerance = 1e-8;
        private static FileStream attemptLock;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Any(value => string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)))
            {
                PrintUsage();
                return 0;
            }
            if (args.Any(value => string.Equals(value, "--identity", StringComparison.OrdinalIgnoreCase)))
            {
                PrintIdentity();
                return 0;
            }
            if (args.Any(value => string.Equals(value, "--static-self-test",
                StringComparison.OrdinalIgnoreCase)))
                return RunStaticSelfTests();

            string workingPack = FullPathOrEmpty(Argument(args, "--working-pack"));
            string doorModule = FullPathOrEmpty(Argument(args, "--door-module"));
            string outPath = FullPathOrEmpty(Argument(args, "--out"));
            string confirmTask = Argument(args, "--confirm-task") ?? "";
            if (string.IsNullOrWhiteSpace(workingPack) || string.IsNullOrWhiteSpace(doorModule) ||
                string.IsNullOrWhiteSpace(outPath) || string.IsNullOrWhiteSpace(confirmTask))
            {
                PrintUsage();
                return 2;
            }

            var result = NewResult(workingPack, doorModule, outPath);
            int exitCode = 1;
            try
            {
                ValidatePreflight(result, confirmTask);
                AcquireAttemptLock(result);
                AssertAuthorizationStillValid(result, "before_transaction");
                CreateCompleteBackup(result);
                result.transaction.mutationStarted = true;
                ImportDoorModule(result);

                RelinkImportedDoorReferences(result);
                ConfigureFrame(result);
                VerifyFrameFreshReadonly(result);
                ConfigureRoot(result);
                VerifyRootFreshReadonly(result);

                CleanupOwnedProcesses(result);
                CaptureFinalProcessGate(result);
                Require(result.processes.finalGate, "FINAL_CAD_PROCESS_GATE_FAILED", 40,
                    "all owned SolidWorks 2020 and sldProcMon processes must be gone before validation commit");
                AuditPostState(result);
                AssertAuthorizationStillValid(result, "before_evidence_commit");

                result.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                Require(CompletionWithinAuthorization(result), "AUTHORIZATION_EXPIRED_BEFORE_COMMIT", 41,
                    "completion time must be no later than both authorization and task lease expiry");
                result.success = true;
                result.committed = true;
                result.status = "NATIVE_ROOT_ASSEMBLY_888X14_PASS";
                result.qualityBoundary = QualityBoundary();
                result.evidence.written = true;
                result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                WriteJsonAtomicNew(result.evidence.path, result);
                result.evidence.sha256 = Sha256(result.evidence.path);
                ValidateCommittedRootEvidence(result);
                AssertAuthorizationStillValid(result, "before_receipt_commit");
                WriteRootReceiptAtomic(result);
                result.transaction.receiptCommitted = true;
                AssertAuthorizationStillValid(result, "before_manifest_commit");
                WriteRootManifestAtomic(result);
                result.transaction.manifestCommitted = true;
                AssertAuthorizationStillValid(result, "after_manifest_commit");
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
                result.status = "NATIVE_ROOT_ASSEMBLY_888X14_FAILED";
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
                    result.committed = false;
                    RemoveOrQuarantineUnpairedArtifact(result, result.rootManifestPath,
                        "root-manifest");
                    RemoveOrQuarantineUnpairedArtifact(result, result.rootReceiptPath,
                        "root-receipt");
                    RemoveOrQuarantineUnpairedArtifact(result, result.evidence.path,
                        "root-evidence");
                    if (result.processes.finalGate && result.transaction.mutationStarted)
                        RollBack(result);
                    else if (result.processes.finalGate)
                        result.transaction.rollbackCompleted = true;
                    else
                    {
                        result.transaction.rollbackBlockedByProcessGate = true;
                        result.transaction.rollbackError =
                            "rollback refused because exact CAD process exit could not be proven";
                    }
                    result.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    result.qualityBoundary = QualityBoundary();
                    TryWriteFailureEvidence(result);
                }
                ReleaseAttemptLock(result);
            }

            Console.WriteLine(result.evidence.path);
            return exitCode;
        }

        private static RunResult NewResult(string workingPack, string doorModule, string outPath)
        {
            return new RunResult
            {
                schema = ResultSchema,
                generatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                status = "NOT_RUN",
                workingPack = workingPack,
                doorModuleOutput = doorModule,
                evidence = new EvidenceRecord { path = outPath, schema = ResultSchema },
                plan = new PlanEvidence(),
                authorization = new AuthorizationEvidence(),
                tool = new ToolEvidence
                {
                    id = ToolId,
                    sourceNormalizedSha256 = NormalizedSourceSha256(),
                    executableSha256 = Sha256(Process.GetCurrentProcess().MainModule.FileName),
                    contractSnapshotSha256 = ExpectedContractSnapshotSha256
                },
                execution = new ExecutionEvidence
                {
                    phases = new List<string> { ExecutionPhase }
                },
                inventory = new InventoryEvidence(),
                doorModule = new DoorImportEvidence(),
                frame = new FrameEvidence(),
                root = new RootEvidence(),
                processes = new ProcessEvidence(),
                transaction = new TransactionEvidence(),
                sessions = new List<SessionEvidence>(),
                authorizationCheckpoints = new List<AuthorizationCheckpoint>(),
                qualityBoundary = QualityBoundary()
            };
        }

        private static QualityBoundaryEvidence QualityBoundary()
        {
            return new QualityBoundaryEvidence
            {
                purpose = Purpose,
                engineeringAssistanceReady = false,
                finalPackAndRelocatedReopenRequired = true,
                structuralEngineerReviewRequired = true
            };
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: NativeRootAssembly888x14.exe --working-pack <attempt\\native_cad\\working_pack> --door-module <same-attempt-door-output> --out <attempt\\evidence\\root-stage-evidence.json> --confirm-task <task-id>");
        }

        private static void PrintIdentity()
        {
            Console.WriteLine("toolId=" + ToolId);
            Console.WriteLine("phase=" + ExecutionPhase);
            Console.WriteLine("recipeDigest=" + ExpectedRecipeDigest);
            Console.WriteLine("sourceNormalizedSha256=" + NormalizedSourceSha256());
            Console.WriteLine("executableSha256=" + Sha256(Process.GetCurrentProcess().MainModule.FileName));
            Console.WriteLine("contractSnapshotSha256=" + ExpectedContractSnapshotSha256);
        }

        private static int RunStaticSelfTests()
        {
            var checks = new List<StaticCheck>();
            Action<string, bool, string> add = (name, passed, detail) => checks.Add(
                new StaticCheck { name = name, passed = passed, detail = detail });
            var fixture = new Dictionary<string, object>(StringComparer.Ordinal)
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
            string commitment = EvidenceCommitmentDigest(fixture);
            add("shared_commitment_fixture", commitment ==
                "684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1",
                commitment);
            fixture["completed_at_utc"] = "excluded-change";
            add("commitment_excludes_receipt_cycle_fields",
                EvidenceCommitmentDigest(fixture) == commitment, "excluded field changed");
            fixture["z"] = 8;
            add("commitment_rejects_semantic_change",
                EvidenceCommitmentDigest(fixture) != commitment, "semantic field changed");
            var numericFixture = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "a", 0.1592142857142857 }, { "b", -1.4150714285714288 },
                { "c", 0.0000001 }, { "d", 0.000001 }, { "e", 1e20 }, { "f", 1e21 },
                { "g", 254.4285714285714 }, { "ticks", 638906112000000000L },
                { "html", "<structure>&engineering" }
            };
            add("shared_commitment_numeric_and_string_fixture",
                EvidenceCommitmentDigest(numericFixture) ==
                    "3BC67213D48D35E839E000955BD2B60A862C13A1EA0DBC0BE4ACBEAD12035C65",
                StableJson(numericFixture) + "|" + EvidenceCommitmentDigest(numericFixture));

            var contracts = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < StageReceiptOrder.Length; index++)
                contracts[StageReceiptOrder[index]] = new Dictionary<string, object>(
                    StringComparer.Ordinal)
                {
                    { "schema", StageReceiptSchemas[index] },
                    { "path", StageReceiptPaths[index] },
                    { "producerToolId", StageReceiptTools[index] },
                    { "predecessor", StageReceiptPredecessors[index] }
                };
            var plan = new Dictionary<string, object>(StringComparer.Ordinal)
                { { "stageReceiptContracts", contracts } };
            var probe = new RunResult();
            bool exactAccepted = TryAction(() => ValidateStageReceiptContracts(plan, probe));
            add("accepts_exact_nine_stage_contract", exactAccepted &&
                probe.stageReceiptContractsValidated, "exact contract");
            Dictionary<string, object> rootContract = contracts["root_assembly_888x14"] as
                Dictionary<string, object>;
            rootContract["path"] = "evidence/forged.json";
            add("rejects_forged_root_receipt_path",
                !TryAction(() => ValidateStageReceiptContracts(plan, new RunResult())),
                "root path tampered");
            rootContract["path"] = RootReceiptRelativePath;

            add("rejects_receipt_path_traversal",
                !IsCanonicalReceiptRelativePath("receipts/../forged.json") &&
                !IsCanonicalReceiptRelativePath("receipts\\forged.json") &&
                IsCanonicalReceiptRelativePath(RootReceiptRelativePath), "canonical path gate");
            var receipt = RuntimeReceiptKeys.ToDictionary(key => key, key => (object)"",
                StringComparer.Ordinal);
            bool exactRuntimeKeys = new HashSet<string>(receipt.Keys, StringComparer.Ordinal)
                .SetEquals(RuntimeReceiptKeys) && receipt.Count == RuntimeReceiptKeys.Length;
            receipt["unexpected"] = true;
            add("rejects_runtime_receipt_extra_key", exactRuntimeKeys &&
                !(new HashSet<string>(receipt.Keys, StringComparer.Ordinal)
                    .SetEquals(RuntimeReceiptKeys) && receipt.Count == RuntimeReceiptKeys.Length),
                "extra key negative control");

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

            Dictionary<string, object> sharedContract = null;
            string sharedContractPath = Path.Combine(Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName),
                "assembly_contract.snapshot.json");
            try { sharedContract = ReadJsonObject(sharedContractPath); }
            catch { sharedContract = null; }
            Dictionary<string, object> sharedAllowedIssue;
            add("shared_snapshot_feature_policy_exact", sharedContract != null &&
                string.Equals(Sha256(sharedContractPath), ExpectedContractSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                ExactKnownFeatureHealthPolicy(sharedContract, out sharedAllowedIssue),
                sharedContractPath);

            var healthy = new FeatureHealthEvidence { traversalComplete = true };
            add("accepts_zero_root_feature_issues",
                RootFeatureHealthAllowed(healthy, sharedContract), "zero issue baseline");
            healthy.issues.Add(new FeatureIssueEvidence
            {
                name = "箱体右侧板焊接-1", type = "Reference", errorCode = 51,
                errorCode2 = 51, warning = true
            });
            add("accepts_only_trusted_reference51_tuple",
                RootFeatureHealthAllowed(healthy, sharedContract), "trusted tuple");

            Dictionary<string, object> extraKeyContract = null;
            try { extraKeyContract = ReadJsonObject(sharedContractPath); }
            catch { extraKeyContract = new Dictionary<string, object>(StringComparer.Ordinal); }
            ChildObject(extraKeyContract, "featureHealthPolicy")["unexpected"] = true;
            add("rejects_policy_extra_key",
                !RootFeatureHealthAllowed(healthy, extraKeyContract), "policy extra key");

            healthy.issues[0].name = "箱体右侧板焊接-2";
            add("rejects_other_reference51_tuple",
                !RootFeatureHealthAllowed(healthy, sharedContract), "different feature name");
            healthy.issues[0].name = "箱体右侧板焊接-1";
            healthy.issues.Add(new FeatureIssueEvidence
                { name = "forged", type = "Reference", errorCode = 51,
                    errorCode2 = 51, warning = true });
            add("rejects_additional_root_feature_issue",
                !RootFeatureHealthAllowed(healthy, sharedContract),
                "second issue negative control");

            string temp = Path.Combine(Path.GetTempPath(), "native-root-static-" +
                Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                string a = Path.Combine(temp, "A.SLDPRT");
                string b = Path.Combine(temp, "b.SLDASM");
                File.WriteAllText(b, "B", new UTF8Encoding(false));
                File.WriteAllText(a, "A", new UTF8Encoding(false));
                string left = InventoryDigest(new[] { b, a });
                string right = InventoryDigest(new[] { a, b });
                add("inventory_digest_ordinal_ignore_case_and_lf", left == right &&
                    IsSha256(left), left);
            }
            catch (Exception ex)
            {
                add("inventory_digest_ordinal_ignore_case_and_lf", false, ex.Message);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }

            int failed = checks.Count(check => !check.passed);
            Console.WriteLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                RecursionLimit = 128 }.Serialize(new StaticSelfTestReport
                {
                    status = failed == 0 ? "PASS" : "FAIL",
                    checksTotal = checks.Count,
                    checksFailed = failed,
                    checks = checks
                }));
            return failed == 0 ? 0 : 1;
        }

        private static void ValidatePreflight(RunResult result, string confirmTask)
        {
            Require(Directory.Exists(result.workingPack), "WORKING_PACK_MISSING", 3,
                "working pack does not exist");
            Require(Directory.Exists(result.doorModuleOutput), "DOOR_MODULE_OUTPUT_MISSING", 3,
                "door module committed output does not exist");
            string attemptsRoot = FullPathOrEmpty(System.Environment.GetEnvironmentVariable(AttemptsRootEnvironment));
            Require(!string.IsNullOrWhiteSpace(attemptsRoot) && Directory.Exists(attemptsRoot),
                "ATTEMPTS_ROOT_MISSING", 3, AttemptsRootEnvironment + " is missing or invalid");
            result.attemptsRoot = attemptsRoot.TrimEnd('\\', '/');

            string relative = RelativeUnder(result.workingPack, result.attemptsRoot,
                "WORKING_PACK_OUTSIDE_ATTEMPTS_ROOT");
            string[] segments = relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            Require(segments.Length == 4 && IsSafeId(segments[0]) &&
                Regex.IsMatch(segments[1], "^attempt-[0-9]{4}$", RegexOptions.CultureInvariant) &&
                string.Equals(segments[2], "native_cad", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[3], WorkingPackLeaf, StringComparison.OrdinalIgnoreCase),
                "WORKING_PACK_SHAPE_INVALID", 3,
                "working pack must be <attempts-root>/<task-id>/attempt-NNNN/native_cad/working_pack");
            result.taskId = segments[0];
            result.execution.attempt = int.Parse(segments[1].Substring(8), CultureInfo.InvariantCulture);
            Require(string.Equals(confirmTask, result.taskId, StringComparison.Ordinal),
                "TASK_CONFIRMATION_MISMATCH", 3, "--confirm-task must exactly match the attempt task id");
            result.attemptDir = Path.Combine(result.attemptsRoot, segments[0], segments[1]);
            Require(SamePath(result.attemptDir,
                Directory.GetParent(Directory.GetParent(result.workingPack).FullName).FullName),
                "ATTEMPT_PATH_MISMATCH", 3, "working pack attempt path is ambiguous");
            Require(IsUnder(result.doorModuleOutput, result.attemptDir) &&
                !IsUnder(result.doorModuleOutput, result.workingPack) &&
                !SamePath(result.doorModuleOutput, result.attemptDir),
                "DOOR_MODULE_SCOPE_INVALID", 3,
                "door module output must be a separate committed directory in the same attempt");

            string evidenceDir = Path.Combine(result.attemptDir, "evidence");
            string expectedEvidencePath = Path.Combine(evidenceDir, ResultFileName);
            Require(SamePath(result.evidence.path, expectedEvidencePath),
                "EVIDENCE_PATH_INVALID", 3,
                "--out must be the fixed current-attempt root assembly evidence path");
            result.rootManifestPath = Path.Combine(evidenceDir, RootManifestFileName);
            result.rootReceiptPath = Path.Combine(result.attemptDir,
                RootReceiptRelativePath.Replace('/', '\\'));
            string finalReceiptPath = Path.Combine(result.attemptDir,
                StageReceiptPaths[8].Replace('/', '\\'));
            Require(!File.Exists(result.evidence.path) && !File.Exists(result.rootManifestPath) &&
                !File.Exists(result.rootReceiptPath) && !File.Exists(finalReceiptPath),
                "EVIDENCE_ALREADY_EXISTS", 3,
                "root evidence, manifest and current/later receipts are immutable outputs");
            Directory.CreateDirectory(evidenceDir);
            Directory.CreateDirectory(Path.GetDirectoryName(result.rootReceiptPath));

            AssertPathChainNoReparse(result.workingPack, result.attemptsRoot,
                "WORKING_PACK_REPARSE_PATH");
            AssertPathChainNoReparse(result.doorModuleOutput, result.attemptDir,
                "DOOR_MODULE_REPARSE_PATH");
            AssertPathChainNoReparse(evidenceDir, result.attemptDir, "EVIDENCE_REPARSE_PATH");
            AssertPathChainNoReparse(Path.GetDirectoryName(result.rootReceiptPath),
                result.attemptDir, "RECEIPT_DIRECTORY_REPARSE_PATH");

            LoadAndValidateContract(result);
            LoadAndValidatePlan(result);
            CaptureInitialInventory(result);
            ValidateLivePredecessorReceiptChain(result);
            LoadAndValidateAuthorization(result);
            LoadAndValidateDoorManifest(result);

            result.processes.baselineSldworks = ProcessIds("SLDWORKS");
            result.processes.baselineSldprocmon = ProcessIds("sldProcMon");
            Require(result.processes.baselineSldworks.Count == 0 &&
                result.processes.baselineSldprocmon.Count == 0,
                "DIRTY_CAD_PROCESS_BASELINE", 5,
                "SLDWORKS and sldProcMon must be fully closed before the root transaction");
            result.preflightPassed = true;
        }

        private static void LoadAndValidateContract(RunResult result)
        {
            string path = Path.Combine(Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule.FileName), "assembly_contract.snapshot.json");
            Require(File.Exists(path) && !HasReparsePoint(path) && FileLinkCount(path) == 1,
                "ASSEMBLY_CONTRACT_SNAPSHOT_INVALID", 3,
                "reviewed assembly contract snapshot must be a regular single-link file beside the executable");
            string actualSha = Sha256(path);
            Require(string.Equals(actualSha, ExpectedContractSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase),
                "ASSEMBLY_CONTRACT_SNAPSHOT_SHA_MISMATCH", 3,
                "assembly contract snapshot no longer matches the reviewed executable");
            Dictionary<string, object> contract = ReadJsonObject(path);
            Dictionary<string, object> recipe = ChildObject(contract, "recipe");
            Dictionary<string, object> mutation = ChildObject(contract, "mutationPolicy");
            Dictionary<string, object> execution = ChildObject(contract, "executionBoundary");
            Dictionary<string, object> allowedFeatureIssue;
            Require(Text(contract, "schema") == ContractSchema && Text(contract, "purpose") == Purpose &&
                Text(recipe, "id") == ExpectedRecipeId && Number(recipe, "version") == 1.0 &&
                string.Equals(Text(recipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(contract, "workingRootFileName") == WorkingRootFileName &&
                Text(contract, "finalRootFileName") == "标准寄存柜1917×888×550(总装配).SLDASM" &&
                Bool(mutation, "directChildrenOnly") && Bool(mutation, "exactInstanceNamesOnly") &&
                Bool(mutation, "recursivePatternDeleteForbidden") &&
                Bool(execution, "planningOnly") && !Bool(execution, "engineeringAssistanceReady") &&
                Bool(execution, "rebuildSaveFreshReadonlyReopenRequired") &&
                ExactKnownFeatureHealthPolicy(contract, out allowedFeatureIssue),
                "ASSEMBLY_CONTRACT_CONTENT_MISMATCH", 3,
                "assembly contract identity, mutation policy or quality boundary drifted");
            Require(ArrayValue(contract, "doors").Length == 14 &&
                ArrayValue(contract, "shelves").Length == 12 &&
                ArrayValue(contract, "frameCrossbars").Length == 12,
                "ASSEMBLY_CONTRACT_ROW_COUNT_MISMATCH", 3,
                "assembly contract must contain exact 14 door, 12 shelf and 12 frame rows");
            result.contractPath = path;
            result.contractSnapshot = contract;
        }

        private static void LoadAndValidatePlan(RunResult result)
        {
            string path = Path.Combine(result.attemptDir, "native_build_plan.json");
            Require(File.Exists(path) && !HasReparsePoint(path) && FileLinkCount(path) == 1,
                "NATIVE_BUILD_PLAN_INVALID", 3,
                "native_build_plan.json must be the single-link direct attempt artifact");
            string actualSha = Sha256(path);
            string configuredSha = (System.Environment.GetEnvironmentVariable(PlanShaEnvironment) ?? "").Trim();
            Require(IsSha256(configuredSha) && string.Equals(actualSha, configuredSha,
                    StringComparison.OrdinalIgnoreCase),
                "NATIVE_BUILD_PLAN_SHA_MISMATCH", 3,
                "native build plan does not match the worker-provided SHA-256");
            Dictionary<string, object> plan = ReadJsonObject(path);
            Dictionary<string, object> task = ChildObject(plan, "task");
            Dictionary<string, object> recipe = ChildObject(plan, "recipe");
            Dictionary<string, object> quality = ChildObject(plan, "qualityBoundary");
            Dictionary<string, object> execution = ChildObject(plan, "executionBoundary");
            Require(Text(plan, "schema") == PlanSchema && Text(plan, "purpose") == Purpose &&
                Text(task, "id") == result.taskId && Number(task, "revisionAtPlanning") > 0 &&
                IsSha256(Text(task, "digest")) && Text(recipe, "id") == ExpectedRecipeId &&
                Number(recipe, "version") == 1.0 && string.Equals(Text(recipe, "digest"),
                    ExpectedRecipeDigest, StringComparison.OrdinalIgnoreCase) &&
                Bool(quality, "planningOnly") && !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses") &&
                !Bool(execution, "executorImplemented") && !Bool(execution, "cadStarted") &&
                !Bool(execution, "modelGenerated") && !Bool(execution, "modelReady") &&
                !Bool(execution, "legacyFallbackUsed"),
                "NATIVE_BUILD_PLAN_BOUNDARY_MISMATCH", 3,
                "plan must remain the exact planning-only trusted 888x14 recipe");
            object embeddedContract;
            Require(plan.TryGetValue("assemblyContract", out embeddedContract) &&
                StableJson(embeddedContract) == StableJson(result.contractSnapshot),
                "PLAN_ASSEMBLY_CONTRACT_MISMATCH", 3,
                "plan assemblyContract must exactly equal the reviewed live contract snapshot");
            Require(StageContractPresent(plan, "assemble_native_model", ToolId),
                "PLAN_ROOT_STAGE_MISSING", 3,
                "plan must bind assemble_native_model to the new native root assembler");
            Require(StringArrayEquals(ArrayValue(plan, "requiredChecks"),
                ArrayValue(result.contractSnapshot, "requiredChecks").Select(ValueText).ToArray()),
                "PLAN_REQUIRED_CHECKS_MISMATCH", 3,
                "plan required checks must exactly equal the trusted recipe contract");
            result.plan.path = path;
            result.plan.sha256 = actualSha;
            result.plan.workerId = Text(plan, "workerId");
            result.plan.taskRevision = Convert.ToInt32(Number(task, "revisionAtPlanning"),
                CultureInfo.InvariantCulture);
            result.plan.taskDigest = Text(task, "digest");
            result.plan.requestFingerprint = Text(ChildObject(plan, "request"), "fingerprint");
            result.plan.requestDigest = Text(ChildObject(plan, "request"), "digest");
            result.plan.recipeDigest = Text(recipe, "digest");
            result.plan.raw = plan;
            ValidateTrustedToolchainManifests(plan, result);
            ValidateStageReceiptContracts(plan, result);
        }

        private static void ValidateTrustedToolchainManifests(Dictionary<string, object> plan,
            RunResult result)
        {
            Dictionary<string, object> manifests = ChildObject(plan, "trustedToolchainManifests");
            string[] exactKeys =
            {
                "native_seed_pack_888x14_v1", "native_width_888_v1",
                "native_door_module_888x14_v1", "native_lock_topology_888x14_v1",
                "native_root_assembly_888x14_v1", "native_final_pack_888x14_v1"
            };
            RequireExactKeys(manifests, "plan.trustedToolchainManifests", exactKeys);
            string repositoryRoot = FindRepositoryRoot(
                Process.GetCurrentProcess().MainModule.FileName);
            Require(!string.IsNullOrWhiteSpace(repositoryRoot),
                "REPOSITORY_ROOT_NOT_FOUND", 3,
                "repository root is required for plan-frozen toolchain manifests");
            var paths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { exactKeys[0], Path.Combine(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json".Replace('/', '\\')) },
                { exactKeys[1], Path.Combine(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/width_888_native_v1/toolchain_manifest.json".Replace('/', '\\')) },
                { exactKeys[2], Path.Combine(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/toolchain_manifest.json".Replace('/', '\\')) },
                { exactKeys[3], Path.Combine(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json".Replace('/', '\\')) },
                { exactKeys[4], Path.Combine(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/toolchain_manifest.json".Replace('/', '\\')) },
                { exactKeys[5], Path.Combine(repositoryRoot,
                    "workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/toolchain_manifest.json".Replace('/', '\\')) }
            };
            foreach (string key in exactKeys)
            {
                string expectedSha = ValueText(manifests[key]).ToUpperInvariant();
                string manifestPath = Path.GetFullPath(paths[key]);
                Require(IsSha256(expectedSha) && File.Exists(manifestPath) &&
                    !HasReparsePoint(manifestPath) && FileLinkCount(manifestPath) == 1 &&
                    string.Equals(Sha256(manifestPath), expectedSha,
                        StringComparison.OrdinalIgnoreCase),
                    "TRUSTED_TOOLCHAIN_MANIFEST_LIVE_HASH_MISMATCH", 3,
                    "trusted toolchain manifest is missing or no longer matches plan: " + key);
                Dictionary<string, object> document = ReadJsonObject(manifestPath);
                if (string.Equals(key, LockToolId, StringComparison.Ordinal))
                {
                    Require(ValidateLockCompositeToolchainManifest(document, manifestPath,
                            repositoryRoot),
                        "TRUSTED_LOCK_COMPOSITE_MANIFEST_CONTENT_INVALID", 3,
                        "lock toolchain manifest must be the exact reviewed three-tool live binding");
                    result.toolchainManifests.Add(new TrustedArtifactEvidence
                    {
                        id = key,
                        schema = Text(document, "schema"),
                        path = manifestPath,
                        sha256 = expectedSha
                    });
                    continue;
                }
                Dictionary<string, object> tool = ChildObject(document, "tool");
                RequireExactKeys(document, "toolchain manifest " + key,
                    "schema", "generatedBy", "tool");
                RequireExactKeys(tool, "toolchain manifest " + key + ".tool", "id",
                    "sourcePath", "sourceNormalizedSha256", "executablePath",
                    "executableSha256", "verifierPath", "verifierSha256");
                Require(Text(document, "schema") ==
                        "winnsen.16029.native_toolchain_manifest.v1" &&
                    !string.IsNullOrWhiteSpace(Text(document, "generatedBy")) &&
                    Text(tool, "id") == key &&
                    LiveRepoRelativeManifestPath(tool, "sourcePath",
                        "sourceNormalizedSha256", true) &&
                    LiveRepoRelativeManifestPath(tool, "executablePath",
                        "executableSha256", false) &&
                    LiveRepoRelativeManifestPath(tool, "verifierPath",
                        "verifierSha256", false),
                    "TRUSTED_TOOLCHAIN_MANIFEST_CONTENT_INVALID", 3,
                    "toolchain manifest is not the exact standard live binding: " + key);
                result.toolchainManifests.Add(new TrustedArtifactEvidence
                {
                    id = key,
                    schema = Text(document, "schema"),
                    path = manifestPath,
                    sha256 = expectedSha
                });
            }
            TrustedArtifactEvidence own = result.toolchainManifests.Single(row => row.id == ToolId);
            Require(OwnToolchainManifestMatches(own.path, result),
                "ROOT_TOOLCHAIN_MANIFEST_IDENTITY_MISMATCH", 3,
                "root toolchain manifest does not bind this reviewed source, executable and contract");
        }

        private static bool ValidateLockCompositeToolchainManifest(
            Dictionary<string, object> document, string manifestPath, string repositoryRoot)
        {
            if (!HasExactKeys(document, "schema", "generatedBy", "tools", "validator") ||
                Text(document, "schema") != LockCompositeToolchainManifestSchema ||
                Text(document, "generatedBy") != "VerifyLockTopology888x14Static.mjs" ||
                !RepositoryFileIsSafe(manifestPath, repositoryRoot)) return false;
            Dictionary<string, object> tools = DictionaryValue(document, "tools");
            Dictionary<string, object> validator = DictionaryValue(document, "validator");
            if (!HasExactKeys(tools, LockToolId, "native_lock_topology_inspector_v1",
                    "native_assembly_tongue_inspector_888x14_v1") ||
                !HasExactKeys(validator, "path", "sha256")) return false;

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
            return HasExactKeys(tool, "sourcePath", "sourceNormalizedSha256",
                    "executablePath", "executableSha256") &&
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

        private static bool HasExactKeys(Dictionary<string, object> value,
            params string[] expected)
        {
            return value != null && value.Count == expected.Length &&
                new HashSet<string>(value.Keys, StringComparer.Ordinal).SetEquals(expected);
        }

        private static bool OwnToolchainManifestMatches(string path, RunResult result)
        {
            Dictionary<string, object> doc = ReadJsonObject(path);
            RequireExactKeys(doc, "root toolchain manifest", "schema", "generatedBy", "tool");
            Dictionary<string, object> tool = ChildObject(doc, "tool");
            RequireExactKeys(tool, "root toolchain manifest.tool", "id", "sourcePath",
                "sourceNormalizedSha256", "executablePath", "executableSha256", "verifierPath",
                "verifierSha256");
            return Text(doc, "schema") == "winnsen.16029.native_toolchain_manifest.v1" &&
                Text(tool, "id") == ToolId &&
                Text(tool, "sourceNormalizedSha256").Equals(result.tool.sourceNormalizedSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(tool, "executableSha256").Equals(result.tool.executableSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                LiveRepoRelativeManifestPath(tool, "sourcePath", "sourceNormalizedSha256", true) &&
                LiveRepoRelativeManifestPath(tool, "executablePath", "executableSha256", false) &&
                LiveRepoRelativeManifestPath(tool, "verifierPath", "verifierSha256", false);
        }

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
            RootReceiptSchema,
            "winnsen.16029.native_final_pack_receipt.v1"
        };

        private static readonly string[] StageReceiptPaths =
        {
            "receipts/clone_native_seed.json", "evidence/width-dimensions.receipt.json",
            "evidence/width-derived.receipt.json", "evidence/width-base-hole.receipt.json",
            "evidence/width-assemblies.receipt.json", "receipts/door_module_888x14.json",
            "receipts/lock_topology_888x14.json", RootReceiptRelativePath,
            "receipts/final_pack_and_relocated_reopen.json"
        };

        private static readonly string[] StageReceiptTools =
        {
            "native_seed_pack_888x14_v1", "native_width_888_v1", "native_width_888_v1",
            "native_width_888_v1", "native_width_888_v1", "native_door_module_888x14_v1",
            "native_lock_topology_888x14_v1", ToolId, "native_final_pack_888x14_v1"
        };

        private static readonly string[] StageReceiptPredecessors =
        {
            "", "clone_native_seed", "dimensions", "derived", "base-hole", "assemblies",
            "door_module_888x14", "lock_topology_888x14", "root_assembly_888x14"
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

        private static void ValidateStageReceiptContracts(Dictionary<string, object> plan,
            RunResult result)
        {
            Dictionary<string, object> contracts = ChildObject(plan, "stageReceiptContracts");
            RequireExactKeys(contracts, "plan.stageReceiptContracts", StageReceiptOrder);
            for (int index = 0; index < StageReceiptOrder.Length; index++)
            {
                string id = StageReceiptOrder[index];
                Dictionary<string, object> row = ChildObject(contracts, id);
                RequireExactKeys(row, "plan.stageReceiptContracts." + id,
                    "schema", "path", "producerToolId", "predecessor");
                Require(Text(row, "schema") == StageReceiptSchemas[index] &&
                    Text(row, "path") == StageReceiptPaths[index] &&
                    Text(row, "producerToolId") == StageReceiptTools[index] &&
                    Text(row, "predecessor") == StageReceiptPredecessors[index] &&
                    IsCanonicalReceiptRelativePath(Text(row, "path")),
                    "NATIVE_STAGE_RECEIPT_CONTRACT_MISMATCH", 3,
                    "stageReceiptContracts differs from the shared nine-stage contract: " + id);
            }
            result.stageReceiptContractsValidated = true;
        }

        private static bool IsCanonicalReceiptRelativePath(string value)
        {
            return value != null && value.IndexOf('\\') < 0 &&
                Regex.IsMatch(value, "^(receipts|evidence)/[A-Za-z0-9_.-]+\\.json$",
                    RegexOptions.CultureInvariant) && value.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static void ValidateLivePredecessorReceiptChain(RunResult result)
        {
            string predecessorSha = "";
            string predecessorPostDigest = "";
            for (int index = 0; index <= 6; index++)
            {
                string id = StageReceiptOrder[index];
                string path = Path.GetFullPath(Path.Combine(result.attemptDir,
                    StageReceiptPaths[index].Replace('/', '\\')));
                AssertPathChainNoReparse(path, result.attemptDir,
                    "LIVE_STAGE_RECEIPT_REPARSE_PATH");
                Require(IsUnder(path, result.attemptDir) && File.Exists(path) &&
                    !HasReparsePoint(path) && FileLinkCount(path) == 1,
                    "LIVE_STAGE_RECEIPT_FILE_INVALID", 3,
                    "predecessor receipt must be a fixed non-reparse single-link file: " + id);
                string hashBefore = Sha256(path);
                Dictionary<string, object> receipt = ReadJsonObject(path);
                RequireExactKeys(receipt, "runtime receipt " + id,
                    index == 0 ? SeedReceiptKeys : RuntimeReceiptKeys);
                string preDigest = index == 0 ? Text(receipt, "sourceInventoryDigest") :
                    Text(receipt, "preInventoryDigest");
                string postDigest = index == 0 ? Text(receipt, "targetInventoryDigest") :
                    Text(receipt, "postInventoryDigest");
                Require(Text(receipt, "schema") == StageReceiptSchemas[index] &&
                    Text(receipt, "phase") == id && Bool(receipt, "success") &&
                    Text(receipt, "taskId") == result.taskId &&
                    Number(receipt, "taskRevision") == result.plan.taskRevision &&
                    string.Equals(Text(receipt, "taskDigest"), result.plan.taskDigest,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Text(receipt, "requestDigest"), result.plan.requestDigest,
                        StringComparison.OrdinalIgnoreCase) &&
                    Number(receipt, "attempt") == result.execution.attempt &&
                    Text(receipt, "recipeId") == ExpectedRecipeId &&
                    string.Equals(Text(receipt, "recipeDigest"), ExpectedRecipeDigest,
                        StringComparison.OrdinalIgnoreCase) &&
                    Text(receipt, "toolId") == StageReceiptTools[index] &&
                    IsSha256(Text(receipt, "toolSourceNormalizedSha256")) &&
                    IsSha256(Text(receipt, "toolExecutableSha256")) &&
                    string.Equals(Text(receipt, "predecessorReceiptSha256"), predecessorSha,
                        StringComparison.OrdinalIgnoreCase) && IsSha256(postDigest) &&
                    (index == 0 ? string.Equals(preDigest, TrustedV37SourceInventoryDigest,
                        StringComparison.OrdinalIgnoreCase) : IsSha256(preDigest)) &&
                    (index == 0 || string.Equals(preDigest, predecessorPostDigest,
                        StringComparison.OrdinalIgnoreCase)),
                    "LIVE_STAGE_RECEIPT_CHAIN_BINDING_INVALID", 3,
                    "predecessor receipt identity or inventory chain is invalid: " + id);
                ValidateReceiptToolchainBinding(receipt, result, id);
                ValidateHistoricalReceiptAuthorization(receipt, preDigest, result, id);
                ValidateReceiptEvidence(receipt, result, id, index);
                if (index == 0)
                {
                    Require(Number(receipt, "nonRootFileCount") == 74 &&
                        Bool(receipt, "nonRootExactSource") &&
                        Text(receipt, "rootFileName") == WorkingRootFileName &&
                        IsSha256(Text(receipt, "rootShaBefore")) &&
                        IsSha256(Text(receipt, "rootShaAfterStable")) &&
                        Number(receipt, "initialOpenErrors") == 0 &&
                        Number(receipt, "initialOpenWarnings") == 32 &&
                        Number(receipt, "stabilizeSaveErrors") == 0 &&
                        Number(receipt, "stabilizeSaveWarnings") == 0 &&
                        Number(receipt, "reopenErrors") == 0 &&
                        Number(receipt, "reopenWarnings") == 32 &&
                        Number(receipt, "dependencyClosureCount") == 75 &&
                        Bool(receipt, "dependenciesAllTargetLocal") &&
                        Bool(receipt, "knownRootIssueGate"),
                        "SEED_RECEIPT_STABILIZATION_PROOF_INVALID", 3,
                        "seed receipt does not prove the exact stable V37 75-file handoff");
                }
                Require(string.Equals(Sha256(path), hashBefore,
                        StringComparison.OrdinalIgnoreCase),
                    "LIVE_STAGE_RECEIPT_CHANGED_DURING_READ", 3,
                    "predecessor receipt changed while it was being validated: " + id);
                result.stageReceipts.Add(new TrustedArtifactEvidence
                {
                    id = id,
                    schema = Text(receipt, "schema"),
                    path = path,
                    sha256 = hashBefore
                });
                predecessorSha = hashBefore;
                predecessorPostDigest = postDigest;
            }
            Require(string.Equals(predecessorPostDigest, result.inventory.preDigest,
                    StringComparison.OrdinalIgnoreCase),
                "LOCK_RECEIPT_POST_TO_ROOT_PRE_INVENTORY_MISMATCH", 3,
                "lock topology receipt post-inventory must equal the live root prestate inventory");
            result.predecessorReceiptSha256 = predecessorSha;
        }

        private static void ValidateReceiptToolchainBinding(Dictionary<string, object> receipt,
            RunResult result, string id)
        {
            string toolId = Text(receipt, "toolId");
            TrustedArtifactEvidence artifact = result.toolchainManifests.SingleOrDefault(row =>
                row.id == toolId);
            Require(artifact != null, "RECEIPT_TOOLCHAIN_MANIFEST_MISSING", 3,
                "receipt producer has no plan-frozen toolchain manifest: " + id);
            Dictionary<string, object> manifest = ReadJsonObject(artifact.path);
            Dictionary<string, object> tool = ChildObject(manifest, "tool");
            Require(string.Equals(Text(tool, "sourceNormalizedSha256"),
                    Text(receipt, "toolSourceNormalizedSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"),
                    Text(receipt, "toolExecutableSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "RECEIPT_TOOLCHAIN_IDENTITY_MISMATCH", 3,
                "receipt producer source/executable differs from the plan-frozen manifest: " + id);
        }

        private static void ValidateHistoricalReceiptAuthorization(
            Dictionary<string, object> receipt, string preDigest, RunResult result, string id)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(Text(receipt, "authorizationJsonBase64")); }
            catch { throw new StageException("RECEIPT_AUTHORIZATION_BASE64_INVALID", 3,
                "embedded historical authorization is not valid base64: " + id); }
            string bytesSha = Sha256Bytes(bytes);
            Require(string.Equals(bytesSha, Text(receipt, "authorizationSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "RECEIPT_AUTHORIZATION_BYTES_HASH_MISMATCH", 3,
                "embedded historical authorization bytes do not match receipt: " + id);
            Dictionary<string, object> authorization;
            try
            {
                authorization = new JavaScriptSerializer { MaxJsonLength = int.MaxValue,
                    RecursionLimit = 128 }.DeserializeObject(
                        new UTF8Encoding(false).GetString(bytes)) as Dictionary<string, object>;
            }
            catch { authorization = null; }
            Require(authorization != null, "RECEIPT_AUTHORIZATION_JSON_INVALID", 3,
                "embedded historical authorization is not a JSON object: " + id);
            ValidateExactAuthorizationKeys(authorization);
            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> request = ChildObject(authorization, "request");
            Dictionary<string, object> plan = ChildObject(authorization, "plan");
            Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> seed = ChildObject(authorization, "seed");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");
            string[] expectedPhases = Text(receipt, "toolId") == "native_width_888_v1" ?
                new[] { "dimensions", "derived", "base-hole", "assemblies" } : new[] { id };
            DateTime issuedAt, expiresAt, leaseExpiresAt, completedAt;
            Require(ParseUtc(Text(authorization, "issuedAt"), out issuedAt) &&
                ParseUtc(Text(authorization, "expiresAt"), out expiresAt) &&
                ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpiresAt) &&
                ParseUtc(Text(receipt, "completedAt"), out completedAt) &&
                completedAt >= issuedAt && completedAt <= expiresAt &&
                completedAt <= leaseExpiresAt && expiresAt > issuedAt &&
                expiresAt <= leaseExpiresAt && expiresAt - issuedAt <= TimeSpan.FromMinutes(30),
                "RECEIPT_HISTORICAL_AUTHORIZATION_TIME_INVALID", 3,
                "historical completion is outside its authorization/task lease: " + id);
            Require(Text(authorization, "schema") == AuthorizationSchema &&
                Text(authorization, "purpose") == Purpose &&
                Text(authorization, "workerId") == result.plan.workerId &&
                Text(authorization, "authorizationId") == ExpectedAuthorizationId(authorization) &&
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
                string.Equals(Text(plan, "sha256"), Text(receipt, "planSha256"),
                    StringComparison.OrdinalIgnoreCase) && IsSha256(Text(plan, "sha256")) &&
                Text(recipe, "id") == ExpectedRecipeId && Number(recipe, "version") == 1 &&
                string.Equals(Text(recipe, "digest"), ExpectedRecipeDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                Text(tool, "id") == Text(receipt, "toolId") &&
                string.Equals(Text(tool, "sourceNormalizedSha256"),
                    Text(receipt, "toolSourceNormalizedSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"),
                    Text(receipt, "toolExecutableSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(seed, "inventoryDigest"), preDigest,
                    StringComparison.OrdinalIgnoreCase) && Bool(execution, "authorized") &&
                Number(execution, "attempt") == result.execution.attempt &&
                StringArrayEquals(ArrayValue(execution, "phases"), expectedPhases) &&
                !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses"),
                "RECEIPT_HISTORICAL_AUTHORIZATION_BINDING_INVALID", 3,
                "receipt does not exactly bind its embedded historical authorization: " + id);
        }

        private static void ValidateReceiptEvidence(Dictionary<string, object> receipt,
            RunResult result, string id, int index)
        {
            string relative = Text(receipt, "evidencePath");
            Require(IsCanonicalReceiptRelativePath(relative),
                "RECEIPT_EVIDENCE_RELATIVE_PATH_INVALID", 3,
                "receipt evidencePath must be canonical attempt-relative JSON: " + id);
            if (index == 0)
                Require(relative == "evidence/seed_pack_888_native_v1.json",
                    "SEED_RECEIPT_EVIDENCE_FIXED_PATH_MISMATCH", 3,
                    "seed receipt evidencePath differs from the fixed contract");
            if (index >= 1 && index <= 4)
                Require(relative == "evidence/width-" + id + ".json",
                    "WIDTH_RECEIPT_EVIDENCE_FIXED_PATH_MISMATCH", 3,
                    "width receipt evidencePath differs from the fixed phase path: " + id);
            if (index == 5)
                Require(relative == "evidence/door_module_888x14.result.v1.json",
                    "DOOR_RECEIPT_EVIDENCE_FIXED_PATH_MISMATCH", 3,
                    "door receipt evidencePath differs from its fixed producer contract");
            if (index == 6)
                Require(relative == "evidence/lock_topology_888x14.v2.json",
                    "LOCK_RECEIPT_EVIDENCE_FIXED_PATH_MISMATCH", 3,
                    "lock receipt evidencePath differs from its fixed producer contract");
            string path = Path.GetFullPath(Path.Combine(result.attemptDir,
                relative.Replace('/', '\\')));
            AssertPathChainNoReparse(path, result.attemptDir,
                "RECEIPT_EVIDENCE_REPARSE_PATH");
            string expectedSha = Text(receipt, "evidenceSha256");
            Require(IsUnder(path, result.attemptDir) && File.Exists(path) &&
                !HasReparsePoint(path) && FileLinkCount(path) == 1 && IsSha256(expectedSha) &&
                string.Equals(Sha256(path), expectedSha, StringComparison.OrdinalIgnoreCase),
                "RECEIPT_EVIDENCE_LIVE_HASH_MISMATCH", 3,
                "receipt evidence is missing, linked or hash-mismatched: " + id);
            Dictionary<string, object> evidence = ReadJsonObject(path);
            string commitment = FirstNonEmpty(Text(evidence, "evidenceCommitmentSha256"),
                Text(evidence, "evidence_commitment_sha256"));
            string evidenceCompletedAt = FirstNonEmpty(Text(evidence, "completedAtUtc"),
                Text(evidence, "completed_at_utc"), Text(evidence, "completedAt"),
                Text(evidence, "evidence_commit_completed_at_utc"));
            Require(Bool(evidence, "success") &&
                Text(receipt, "completedAt") == evidenceCompletedAt &&
                IsSha256(Text(receipt, "evidenceCommitmentSha256")) &&
                string.Equals(commitment, Text(receipt, "evidenceCommitmentSha256"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(EvidenceCommitmentDigest(evidence),
                    Text(receipt, "evidenceCommitmentSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "RECEIPT_EVIDENCE_COMMITMENT_MISMATCH", 3,
                "receipt commitment differs from its live evidence: " + id);
            if (index == 6)
                Require(Bool(evidence, "success") &&
                    SamePath(Text(evidence, "cad_directory"), result.workingPack),
                    "LOCK_RECEIPT_WORKING_PACK_BINDING_INVALID", 3,
                    "lock evidence must bind the exact live root working pack");
        }

        private static bool StageContractPresent(Dictionary<string, object> plan, string id, string tool)
        {
            object[] stages = ArrayValue(plan, "stageContracts");
            return stages.Count(value =>
            {
                var row = value as Dictionary<string, object>;
                return row != null && Text(row, "id") == id && Text(row, "toolId") == tool;
            }) == 1;
        }

        private static void CaptureInitialInventory(RunResult result)
        {
            List<string> files = CaptureFlatCadTree(result.workingPack, 75,
                "INITIAL_WORKING_PACK_FILE_SYSTEM_INVALID");
            result.inventory.preCount = files.Count;
            result.inventory.preAssemblyCount = files.Count(IsAssembly);
            result.inventory.prePartCount = files.Count(IsPart);
            Require(result.inventory.preCount == 75 && result.inventory.preAssemblyCount == 22 &&
                result.inventory.prePartCount == 53,
                "INITIAL_WORKING_PACK_COUNT_INVALID", 6,
                "working pack must begin with exactly 75 CAD files: 22 SLDASM and 53 SLDPRT");
            result.inventory.preDigest = InventoryDigest(files);
            foreach (string path in files)
                result.initialHashes[Path.GetFileName(path)] = Sha256(path);
            Require(result.initialHashes.ContainsKey(WorkingRootFileName) &&
                result.initialHashes.ContainsKey(FrameFileName),
                "INITIAL_WORKING_PACK_CORE_FILES_MISSING", 6,
                "working root and frame assemblies are missing from the exact 75-file pack");
        }

        private static void LoadAndValidateAuthorization(RunResult result)
        {
            string configuredPath = FullPathOrEmpty(System.Environment.GetEnvironmentVariable(
                AuthorizationPathEnvironment));
            string expectedPath = Path.Combine(result.attemptDir, "execution_authorizations",
                ToolId + ".json");
            Require(SamePath(configuredPath, expectedPath) && File.Exists(configuredPath) &&
                !HasReparsePoint(Path.GetDirectoryName(configuredPath)) &&
                !HasReparsePoint(configuredPath) && FileLinkCount(configuredPath) == 1,
                "EXECUTION_AUTHORIZATION_PATH_INVALID", 3,
                "authorization must be the exact single-link worker artifact for this root tool");
            string configuredSha = (System.Environment.GetEnvironmentVariable(
                AuthorizationShaEnvironment) ?? "").Trim();
            string actualSha = Sha256(configuredPath);
            Require(IsSha256(configuredSha) && string.Equals(configuredSha, actualSha,
                    StringComparison.OrdinalIgnoreCase),
                "EXECUTION_AUTHORIZATION_SHA_MISMATCH", 3,
                "execution authorization does not match the worker-provided SHA-256");
            Dictionary<string, object> authorization = ReadJsonObject(configuredPath);
            ValidateExactAuthorizationKeys(authorization);

            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> request = ChildObject(authorization, "request");
            Dictionary<string, object> plan = ChildObject(authorization, "plan");
            Dictionary<string, object> recipe = ChildObject(authorization, "recipe");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> seed = ChildObject(authorization, "seed");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            Dictionary<string, object> quality = ChildObject(authorization, "qualityBoundary");

            DateTime issuedAt = DateTime.MinValue, expiresAt = DateTime.MinValue,
                leaseExpiresAt = DateTime.MinValue;
            bool timesParsed = ParseUtc(Text(authorization, "issuedAt"), out issuedAt) &&
                ParseUtc(Text(authorization, "expiresAt"), out expiresAt) &&
                ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpiresAt);
            DateTime now = DateTime.UtcNow;
            Require(timesParsed && issuedAt <= now && expiresAt > now && expiresAt > issuedAt &&
                expiresAt - issuedAt <= TimeSpan.FromMinutes(30) &&
                expiresAt <= leaseExpiresAt && leaseExpiresAt > now,
                "EXECUTION_AUTHORIZATION_TIME_INVALID", 3,
                "authorization must be active, no longer than 30 minutes and no later than the task lease");
            Require(Text(authorization, "schema") == AuthorizationSchema &&
                Text(authorization, "purpose") == Purpose &&
                Text(authorization, "workerId") == result.plan.workerId &&
                Text(task, "id") == result.taskId && Number(task, "revision") == result.plan.taskRevision &&
                string.Equals(Text(task, "digest"), result.plan.taskDigest,
                    StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Text(task, "leaseId")) &&
                Text(request, "fingerprint") == result.plan.requestFingerprint &&
                string.Equals(Text(request, "digest"), result.plan.requestDigest,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(plan, "sha256"), result.plan.sha256,
                    StringComparison.OrdinalIgnoreCase) && Text(recipe, "id") == ExpectedRecipeId &&
                Number(recipe, "version") == 1.0 && string.Equals(Text(recipe, "digest"),
                    ExpectedRecipeDigest, StringComparison.OrdinalIgnoreCase) &&
                Text(tool, "id") == ToolId && string.Equals(Text(tool, "sourceNormalizedSha256"),
                    result.tool.sourceNormalizedSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"), result.tool.executableSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(seed, "inventoryDigest"), result.inventory.preDigest,
                    StringComparison.OrdinalIgnoreCase) && Bool(execution, "authorized") &&
                Number(execution, "attempt") == result.execution.attempt &&
                StringArrayEquals(ArrayValue(execution, "phases"), new[] { ExecutionPhase }) &&
                !Bool(quality, "engineeringAssistanceReady") &&
                Bool(quality, "readyOnlyAfterEveryRequiredCheckPasses"),
                "EXECUTION_AUTHORIZATION_BINDING_MISMATCH", 3,
                "authorization does not exactly bind task, plan, tool, seed inventory and root phase");

            string expectedId = ExpectedAuthorizationId(authorization);
            Require(Regex.IsMatch(Text(authorization, "authorizationId"),
                    "^native-auth-[a-f0-9]{32}$", RegexOptions.CultureInvariant) &&
                Text(authorization, "authorizationId") == expectedId,
                "EXECUTION_AUTHORIZATION_ID_MISMATCH", 3,
                "authorizationId does not match the stable immutable binding digest");

            result.authorization.path = configuredPath;
            result.authorization.sha256 = actualSha;
            result.authorization.authorizationId = expectedId;
            result.authorization.issuedAt = Text(authorization, "issuedAt");
            result.authorization.expiresAt = Text(authorization, "expiresAt");
            result.authorization.leaseId = Text(task, "leaseId");
            result.authorization.leaseExpiresAt = Text(task, "leaseExpiresAt");
            result.authorization.workerId = Text(authorization, "workerId");
            result.authorization.raw = authorization;
            result.execution.authorized = true;
            result.authorizationCheckpoints.Add(new AuthorizationCheckpoint
            {
                name = "initial_binding",
                checkedAtUtc = now.ToString("o", CultureInfo.InvariantCulture),
                valid = true
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

        private static void AssertAuthorizationStillValid(RunResult result, string checkpoint)
        {
            bool valid = result != null && File.Exists(result.authorization.path) &&
                !HasReparsePoint(result.authorization.path) && FileLinkCount(result.authorization.path) == 1 &&
                string.Equals(Sha256(result.authorization.path), result.authorization.sha256,
                    StringComparison.OrdinalIgnoreCase);
            Dictionary<string, object> authorization = valid ? ReadJsonObject(result.authorization.path) :
                new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> task = ChildObject(authorization, "task");
            Dictionary<string, object> tool = ChildObject(authorization, "tool");
            Dictionary<string, object> execution = ChildObject(authorization, "execution");
            DateTime expires, leaseExpires;
            valid = valid && ParseUtc(Text(authorization, "expiresAt"), out expires) &&
                ParseUtc(Text(task, "leaseExpiresAt"), out leaseExpires) &&
                DateTime.UtcNow < expires && DateTime.UtcNow < leaseExpires && expires <= leaseExpires &&
                Text(authorization, "authorizationId") == result.authorization.authorizationId &&
                ExpectedAuthorizationId(authorization) == result.authorization.authorizationId &&
                Text(authorization, "workerId") == result.authorization.workerId &&
                Text(task, "id") == result.taskId && Text(task, "leaseId") == result.authorization.leaseId &&
                Text(task, "leaseExpiresAt") == result.authorization.leaseExpiresAt &&
                Text(tool, "id") == ToolId && string.Equals(Text(tool, "sourceNormalizedSha256"),
                    result.tool.sourceNormalizedSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(tool, "executableSha256"), result.tool.executableSha256,
                    StringComparison.OrdinalIgnoreCase) && Bool(execution, "authorized") &&
                Number(execution, "attempt") == result.execution.attempt &&
                StringArrayEquals(ArrayValue(execution, "phases"), new[] { ExecutionPhase }) &&
                string.Equals(Sha256(result.authorization.path), result.authorization.sha256,
                    StringComparison.OrdinalIgnoreCase);
            result.authorizationCheckpoints.Add(new AuthorizationCheckpoint
            {
                name = checkpoint,
                checkedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                valid = valid
            });
            Require(valid, "EXECUTION_AUTHORIZATION_EXPIRED_OR_CHANGED", 3,
                "worker authorization expired or changed at checkpoint " + checkpoint);
        }

        private static bool CompletionWithinAuthorization(RunResult result)
        {
            DateTime completed, expires, leaseExpires;
            return ParseUtc(result.completedAtUtc, out completed) &&
                ParseUtc(result.authorization.expiresAt, out expires) &&
                ParseUtc(result.authorization.leaseExpiresAt, out leaseExpires) &&
                completed <= expires && completed <= leaseExpires;
        }

        private static void LoadAndValidateDoorManifest(RunResult result)
        {
            string manifestPath = Path.Combine(result.doorModuleOutput, DoorImportManifestFileName);
            Require(File.Exists(manifestPath) && !HasReparsePoint(manifestPath) &&
                FileLinkCount(manifestPath) == 1,
                "DOOR_IMPORT_MANIFEST_INVALID", 7,
                "door module flat import manifest must be a single-link file in the committed output root");
            Dictionary<string, object> manifest = ReadJsonObject(manifestPath);
            RequireExactKeys(manifest, "door manifest", "schema", "immutable", "cadFileCount",
                "v37WorkingPackFileCount", "v37WorkingPackInventoryDigest", "workingPackImportState",
                "rootAssemblerBoundary", "entries");
            Require(Text(manifest, "schema") == DoorManifestSchema && Bool(manifest, "immutable") &&
                Number(manifest, "cadFileCount") == 13 &&
                Number(manifest, "v37WorkingPackFileCount") == 75 &&
                string.Equals(Text(manifest, "v37WorkingPackInventoryDigest"),
                    TrustedV37SourceInventoryDigest, StringComparison.OrdinalIgnoreCase) &&
                Text(manifest, "workingPackImportState") == "NOT_IMPORTED" &&
                Text(manifest, "rootAssemblerBoundary") == RootAssemblerBoundary,
                "DOOR_IMPORT_MANIFEST_CONTRACT_MISMATCH", 7,
                "door module manifest is not the exact immutable NOT_IMPORTED handoff");
            object[] entries = ArrayValue(manifest, "entries");
            Require(entries.Length == 13, "DOOR_IMPORT_MANIFEST_COUNT_INVALID", 7,
                "door import manifest must contain exactly 13 entries");

            var expectedLeaves = new HashSet<string>(ArrayValue(
                ChildObject(result.contractSnapshot, "doorModuleInventory"), "relativeLeafNames")
                .Select(ValueText), StringComparer.OrdinalIgnoreCase);
            var seenLeaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenRelative = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object value in entries)
            {
                var entry = value as Dictionary<string, object>;
                RequireExactKeys(entry, "door manifest entry", "canonicalRelativePath", "flatTargetName",
                    "sizeBytes", "sha256", "category");
                string relative = Text(entry, "canonicalRelativePath").Replace('/', '\\');
                string leaf = Text(entry, "flatTargetName");
                string category = Text(entry, "category");
                Require(!Path.IsPathRooted(relative) && relative.Split('\\').Length == 2 &&
                    (relative.StartsWith("parts\\", StringComparison.Ordinal) ||
                     relative.StartsWith("assemblies\\", StringComparison.Ordinal)) &&
                    Path.GetFileName(relative) == leaf && expectedLeaves.Contains(leaf) &&
                    seenLeaves.Add(leaf) && seenRelative.Add(relative) && IsSha256(Text(entry, "sha256")) &&
                    ((category == "part" && IsPart(leaf)) ||
                     (category == "assembly" && IsAssembly(leaf))),
                    "DOOR_IMPORT_MANIFEST_ENTRY_INVALID", 7,
                    "door import manifest entry path, leaf, category or hash is invalid");
                string source = Path.GetFullPath(Path.Combine(result.doorModuleOutput, relative));
                Require(IsUnder(source, result.doorModuleOutput) && File.Exists(source) &&
                    !HasReparsePoint(source) && FileLinkCount(source) == 1 &&
                    new FileInfo(source).Length == Convert.ToInt64(Number(entry, "sizeBytes"),
                        CultureInfo.InvariantCulture) && string.Equals(Sha256(source), Text(entry, "sha256"),
                        StringComparison.OrdinalIgnoreCase),
                    "DOOR_IMPORT_SOURCE_FILE_INVALID", 7,
                    "door module source file does not match its immutable manifest: " + relative);
                string target = Path.Combine(result.workingPack, leaf);
                Require(!File.Exists(target) && !Directory.Exists(target),
                    "DOOR_IMPORT_TARGET_COLLISION", 7,
                    "door module target collides with the 75-file working pack: " + leaf);
                result.doorModule.entries.Add(new DoorImportEntryEvidence
                {
                    canonicalRelativePath = relative.Replace('\\', '/'),
                    flatTargetName = leaf,
                    sourcePath = source,
                    sourceSha256 = Text(entry, "sha256").ToUpperInvariant(),
                    sizeBytes = new FileInfo(source).Length,
                    category = category,
                    targetPath = target
                });
            }
            Require(seenLeaves.SetEquals(expectedLeaves), "DOOR_IMPORT_CONTRACT_LEAVES_MISMATCH", 7,
                "manifest flat target leaves differ from the live assembly contract");
            ValidateDoorOutputTree(result, manifestPath);
            result.doorModule.manifestPath = manifestPath;
            result.doorModule.manifestSha256 = Sha256(manifestPath);
            result.doorModule.sourceOutputDigest = InventoryDigest(
                result.doorModule.entries.Select(row => row.sourcePath));
            result.doorModule.workingPackImportStateBefore = "NOT_IMPORTED";
            ValidateDoorReceiptOutputBinding(result);
        }

        private static void ValidateDoorReceiptOutputBinding(RunResult result)
        {
            TrustedArtifactEvidence artifact = result.stageReceipts.Single(row =>
                row.id == "door_module_888x14");
            Dictionary<string, object> receipt = ReadJsonObject(artifact.path);
            string evidencePath = Path.GetFullPath(Path.Combine(result.attemptDir,
                Text(receipt, "evidencePath").Replace('/', '\\')));
            Dictionary<string, object> evidence = ReadJsonObject(evidencePath);
            Require(Bool(evidence, "completed") && Bool(evidence, "committed") &&
                !Bool(evidence, "working_pack_imported") &&
                SamePath(Text(evidence, "committed_output_dir"), result.doorModuleOutput) &&
                SamePath(Text(evidence, "flat_import_manifest_path"),
                    result.doorModule.manifestPath) &&
                string.Equals(Text(evidence, "flat_import_manifest_sha256"),
                    result.doorModule.manifestSha256, StringComparison.OrdinalIgnoreCase) &&
                Text(evidence, "root_assembler_boundary") == RootAssemblerBoundary,
                "DOOR_RECEIPT_OUTPUT_BINDING_INVALID", 7,
                "door receipt evidence must bind this exact committed output and NOT_IMPORTED manifest");
        }

        private static void ValidateDoorOutputTree(RunResult result, string manifestPath)
        {
            var allowedFiles = new HashSet<string>(result.doorModule.entries.Select(row => row.sourcePath),
                StringComparer.OrdinalIgnoreCase) { manifestPath };
            var allowedDirectories = new HashSet<string>(new[]
            {
                Path.Combine(result.doorModuleOutput, "parts"),
                Path.Combine(result.doorModuleOutput, "assemblies")
            }, StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            pending.Enqueue(result.doorModuleOutput);
            var actualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actualDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int guard = 0;
            while (pending.Count > 0)
            {
                Require(++guard <= 100, "DOOR_OUTPUT_TRAVERSAL_LIMIT", 7,
                    "door output directory traversal exceeded the fail-closed limit");
                string directory = pending.Dequeue();
                Require(!HasReparsePoint(directory), "DOOR_OUTPUT_REPARSE_DIRECTORY", 7,
                    "door output contains a reparse directory: " + directory);
                foreach (string child in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string full = Path.GetFullPath(child);
                    Require(allowedDirectories.Contains(full) && actualDirectories.Add(full),
                        "DOOR_OUTPUT_UNEXPECTED_DIRECTORY", 7,
                        "door output may contain only parts and assemblies directories");
                    pending.Enqueue(full);
                }
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string full = Path.GetFullPath(file);
                    Require(!HasReparsePoint(full) && FileLinkCount(full) == 1 && actualFiles.Add(full),
                        "DOOR_OUTPUT_FILE_IDENTITY_INVALID", 7,
                        "door output file must be non-reparse and single-link: " + full);
                }
            }
            Require(actualDirectories.SetEquals(allowedDirectories) && actualFiles.SetEquals(allowedFiles),
                "DOOR_OUTPUT_EXACT_TREE_MISMATCH", 7,
                "door output must contain only the exact 13 CAD files plus immutable manifest");
        }

        private static void AcquireAttemptLock(RunResult result)
        {
            string path = Path.Combine(result.attemptDir, ".native-root-assembly-888x14.lock");
            Require(!HasReparsePoint(result.attemptDir), "ATTEMPT_REPARSE_PATH", 8,
                "attempt directory is a reparse point");
            try
            {
                attemptLock = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.WriteThrough);
                byte[] content = new UTF8Encoding(false).GetBytes(
                    Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + "\n");
                attemptLock.Write(content, 0, content.Length);
                attemptLock.Flush(true);
                result.transaction.lockPath = path;
                result.transaction.lockAcquired = true;
            }
            catch (IOException)
            {
                Require(false, "ROOT_ASSEMBLY_TRANSACTION_ALREADY_ACTIVE", 8,
                    "another root assembly transaction lock already exists");
            }
        }

        private static void ReleaseAttemptLock(RunResult result)
        {
            if (attemptLock != null)
            {
                try { attemptLock.Dispose(); } catch { }
                attemptLock = null;
            }
            if (!string.IsNullOrWhiteSpace(result.transaction.lockPath))
                TryDelete(result.transaction.lockPath);
        }

        private static void CreateCompleteBackup(RunResult result)
        {
            string evidenceDir = Path.GetDirectoryName(result.evidence.path);
            result.transaction.backupDirectory = Path.Combine(evidenceDir,
                ".native-root-assembly-backup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(result.transaction.backupDirectory);
            Require(!HasReparsePoint(result.transaction.backupDirectory), "BACKUP_REPARSE_PATH", 9,
                "backup directory is a reparse point");
            foreach (KeyValuePair<string, string> row in result.initialHashes.OrderBy(value => value.Key,
                StringComparer.OrdinalIgnoreCase))
            {
                string source = Path.Combine(result.workingPack, row.Key);
                string target = Path.Combine(result.transaction.backupDirectory, row.Key);
                File.Copy(source, target, false);
                Require(!HasReparsePoint(target) && FileLinkCount(target) == 1 &&
                    string.Equals(Sha256(target), row.Value, StringComparison.OrdinalIgnoreCase),
                    "BACKUP_HASH_VERIFICATION_FAILED", 9,
                    "complete 75-file backup verification failed: " + row.Key);
            }
            List<string> backups = CaptureFlatCadTree(result.transaction.backupDirectory, 75,
                "BACKUP_TREE_INVALID");
            Require(InventoryDigest(backups) == result.inventory.preDigest,
                "BACKUP_INVENTORY_DIGEST_MISMATCH", 9,
                "complete backup digest differs from the 75-file prestate");
            result.transaction.backupComplete = true;
            result.transaction.backupFileCount = backups.Count;
        }

        private static void ImportDoorModule(RunResult result)
        {
            Require(result.transaction.backupComplete, "BACKUP_REQUIRED_BEFORE_IMPORT", 10,
                "door import cannot start before the complete 75-file backup is verified");
            foreach (DoorImportEntryEvidence row in result.doorModule.entries.OrderBy(
                value => value.flatTargetName, StringComparer.OrdinalIgnoreCase))
            {
                Require(!File.Exists(row.targetPath), "DOOR_IMPORT_TARGET_COLLISION", 10,
                    "door import target appeared after preflight: " + row.flatTargetName);
                File.Copy(row.sourcePath, row.targetPath, false);
                row.importedSha256 = Sha256(row.targetPath);
                row.copyHashMatched = !HasReparsePoint(row.targetPath) && FileLinkCount(row.targetPath) == 1 &&
                    string.Equals(row.importedSha256, row.sourceSha256,
                        StringComparison.OrdinalIgnoreCase);
                Require(row.copyHashMatched, "DOOR_IMPORT_COPY_HASH_MISMATCH", 10,
                    "transactional flat copy does not match manifest: " + row.flatTargetName);
                result.transaction.importedPaths.Add(row.targetPath);
            }
            List<string> postImport = CaptureFlatCadTree(result.workingPack, 88,
                "POST_IMPORT_WORKING_PACK_INVALID");
            Require(postImport.Count == 88 && postImport.Count(IsAssembly) == 26 &&
                postImport.Count(IsPart) == 62,
                "POST_IMPORT_WORKING_PACK_COUNT_INVALID", 10,
                "flat working pack must contain exactly 88 CAD files after the exact 13-file import");
            result.inventory.importedCount = 13;
            result.inventory.afterImportCount = 88;
            result.inventory.afterImportDigest = InventoryDigest(postImport);
            result.doorModule.imported = true;
            result.doorModule.workingPackImportStateAfter = "IMPORTED_BY_ROOT_ASSEMBLER_TRANSACTION";
        }

        private static void RelinkImportedDoorReferences(RunResult result)
        {
            AssertAuthorizationStillValid(result, "before_door_reference_relink");
            var session = new SessionEvidence { purpose = "door_reference_relink" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && OwnedSessionIsExclusive(session),
                    "RELINK_SOLIDWORKS_SESSION_INVALID", 12,
                    "exclusive trusted SolidWorks 2020 session could not be proven");
                var importedLeaves = new HashSet<string>(result.doorModule.entries.Select(
                    row => row.flatTargetName), StringComparer.OrdinalIgnoreCase);
                foreach (DoorImportEntryEvidence assemblyRow in result.doorModule.entries.Where(
                    row => row.category == "assembly").OrderBy(row => row.flatTargetName,
                        StringComparer.OrdinalIgnoreCase))
                {
                    object raw = Safe(() => sw.GetDocumentDependencies2(assemblyRow.targetPath,
                        true, true, false), null);
                    List<string> dependencies = RootedCadPaths(raw);
                    Require(dependencies.Count > 0, "DOOR_ASSEMBLY_DEPENDENCIES_MISSING", 12,
                        "imported door assembly has no readable native dependencies: " +
                        assemblyRow.flatTargetName);
                    foreach (string oldReference in dependencies.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        string leaf = Path.GetFileName(oldReference);
                        Require(importedLeaves.Contains(leaf), "DOOR_ASSEMBLY_EXTERNAL_DEPENDENCY", 12,
                            "imported door assembly references a file outside the approved 13: " + oldReference);
                        string target = Path.Combine(result.workingPack, leaf);
                        if (!SamePath(oldReference, target))
                        {
                            bool replaced = Safe(() => sw.ReplaceReferencedDocument(
                                assemblyRow.targetPath, oldReference, target), false);
                            Require(replaced, "DOOR_REFERENCE_RELINK_FAILED", 12,
                                "ReplaceReferencedDocument failed for " + assemblyRow.flatTargetName +
                                " -> " + leaf);
                            result.doorModule.relinkedReferences.Add(new ReferenceRelinkEvidence
                            {
                                parentAssembly = assemblyRow.targetPath,
                                oldReference = oldReference,
                                flatReference = target,
                                replaced = true
                            });
                        }
                    }
                }
            }
            finally
            {
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.processExited, "RELINK_SESSION_DID_NOT_EXIT", 12,
                "door reference relink SolidWorks session did not exit cleanly");
            VerifyImportedDoorClosure(result);
        }

        private static void VerifyImportedDoorClosure(RunResult result)
        {
            var session = new SessionEvidence { purpose = "door_reference_relink_verify" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && OwnedSessionIsExclusive(session),
                    "RELINK_VERIFY_SESSION_INVALID", 12,
                    "exclusive SolidWorks session unavailable for door reference closure verification");
                var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DoorImportEntryEvidence assemblyRow in result.doorModule.entries.Where(
                    row => row.category == "assembly"))
                {
                    closure.Add(assemblyRow.targetPath);
                    foreach (string path in RootedCadPaths(Safe(() => sw.GetDocumentDependencies2(
                        assemblyRow.targetPath, true, true, false), null))) closure.Add(path);
                }
                var expected = new HashSet<string>(result.doorModule.entries.Select(row => row.targetPath),
                    StringComparer.OrdinalIgnoreCase);
                Require(closure.SetEquals(expected) && closure.All(path => IsUnder(path,
                        result.workingPack) && File.Exists(path)),
                    "IMPORTED_DOOR_CLOSURE_INVALID", 12,
                    "relinked door module closure must be the exact 13 files in the flat working pack");
                result.doorModule.relinkedClosureCount = closure.Count;
            }
            finally
            {
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.processExited, "RELINK_VERIFY_SESSION_DID_NOT_EXIT", 12,
                "door reference verification SolidWorks session did not exit cleanly");
        }

        private static void ConfigureFrame(RunResult result)
        {
            AssertAuthorizationStillValid(result, "before_frame_edit");
            var session = new SessionEvidence { purpose = "frame_edit" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            ModelDoc2 model = null;
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && OwnedSessionIsExclusive(session), "FRAME_SESSION_INVALID", 14,
                    "exclusive trusted SolidWorks 2020 session unavailable for frame edit");
                int errors = 0, warnings = 0;
                string framePath = Path.Combine(result.workingPack, FrameFileName);
                model = OpenDocument(sw, framePath, false, ref errors, ref warnings);
                result.frame.path = framePath;
                result.frame.writableOpenErrors = errors;
                result.frame.writableOpenWarnings = warnings;
                Require(model != null && errors == 0 && warnings == 0, "FRAME_WRITABLE_OPEN_FAILED", 14,
                    "frame writable open must return exact errors=0 and warnings=0");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "FRAME_NOT_ASSEMBLY", 14,
                    "frame document is not a native assembly");
                Safe(() => assembly.ResolveAllLightWeightComponents(false), -1);
                DeleteExactDirectComponents(model, ContractRemoveNames(result, "frameCrossbarInstances"),
                    result.frame.deletedInstances, "FRAME", 12);
                Require(DirectComponents(model).Count == 6, "FRAME_POST_DELETE_COUNT_INVALID", 14,
                    "frame must retain exactly six non-crossbar direct components before rebuilding rows");
                foreach (Dictionary<string, object> row in ContractRows(result, "frameCrossbars"))
                    AddContractComponent(sw, model, assembly, result,
                        Path.Combine(result.workingPack, Text(row, "source")), row, false,
                        result.frame.placements);
                Require(Safe(() => model.ForceRebuild3(false), false), "FRAME_REBUILD_FAILED", 14,
                    "frame rebuild failed after exact 12-row replacement");
                FrameSnapshot snapshot = CaptureFrameSnapshot(model, result);
                ValidateFrameSnapshot(snapshot, result);
                result.frame.afterEdit = snapshot;
                SaveExact(model, result, "frame_save");
                result.frame.sha256 = Sha256(framePath);
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.processExited, "FRAME_SESSION_DID_NOT_EXIT", 14,
                "frame edit SolidWorks session did not exit cleanly");
        }

        private static void VerifyFrameFreshReadonly(RunResult result)
        {
            AssertAuthorizationStillValid(result, "before_frame_readonly_reopen");
            var session = new SessionEvidence { purpose = "frame_fresh_readonly_reopen" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            ModelDoc2 model = null;
            string hashBefore = Sha256(result.frame.path);
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && OwnedSessionIsExclusive(session),
                    "FRAME_REOPEN_SESSION_INVALID", 15,
                    "exclusive SolidWorks session unavailable for frame read-only reopen");
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, result.frame.path, true, ref errors, ref warnings);
                result.frame.readonlyOpenErrors = errors;
                result.frame.readonlyOpenWarnings = warnings;
                Require(model != null && errors == 0 && warnings == 0,
                    "FRAME_READONLY_REOPEN_FAILED", 15,
                    "fresh read-only frame reopen must return exact errors=0 and warnings=0");
                Require(Safe(() => model.ForceRebuild3(false), false),
                    "FRAME_READONLY_REBUILD_FAILED", 15,
                    "fresh read-only frame rebuild failed");
                FrameSnapshot snapshot = CaptureFrameSnapshot(model, result);
                ValidateFrameSnapshot(snapshot, result);
                Require(snapshot.signature == result.frame.afterEdit.signature,
                    "FRAME_FRESH_REOPEN_TOPOLOGY_DRIFT", 15,
                    "frame component topology changed after fresh read-only reopen");
                result.frame.afterReadonlyReopen = snapshot;
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.processExited && string.Equals(hashBefore, Sha256(result.frame.path),
                    StringComparison.OrdinalIgnoreCase),
                "FRAME_READONLY_REOPEN_MUTATED_FILE", 15,
                "fresh read-only frame reopen must exit and preserve the saved file hash");
            result.frame.freshReadonlyReopenPassed = true;
        }

        private static void ConfigureRoot(RunResult result)
        {
            AssertAuthorizationStillValid(result, "before_root_edit");
            var session = new SessionEvidence { purpose = "root_edit" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            ModelDoc2 model = null;
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && OwnedSessionIsExclusive(session), "ROOT_SESSION_INVALID", 20,
                    "exclusive trusted SolidWorks 2020 session unavailable for root edit");
                int errors = 0, warnings = 0;
                string rootPath = Path.Combine(result.workingPack, WorkingRootFileName);
                model = OpenDocument(sw, rootPath, false, ref errors, ref warnings);
                result.root.path = rootPath;
                result.root.writableOpenErrors = errors;
                result.root.writableOpenWarnings = warnings;
                Require(model != null && errors == 0 && warnings == 32,
                    "ROOT_WRITABLE_OPEN_WARNING_MISMATCH", 20,
                    "trusted V37 root writable open must return exact errors=0 and warnings=32");
                AssemblyDoc assembly = model as AssemblyDoc;
                Require(assembly != null, "ROOT_NOT_ASSEMBLY", 20,
                    "working root document is not a native assembly");
                Safe(() => assembly.ResolveAllLightWeightComponents(false), -1);
                List<string> removeNames = ContractRemoveNames(result, "rootDoors")
                    .Concat(ContractRemoveNames(result, "rootShelfInstances")).ToList();
                DeleteExactDirectComponents(model, removeNames, result.root.deletedInstances,
                    "ROOT", 10);
                Require(DirectComponents(model).Count == 15, "ROOT_POST_DELETE_COUNT_INVALID", 20,
                    "root must retain exactly 15 direct components after deleting six old doors and four shelves");

                foreach (Dictionary<string, object> row in ContractRows(result, "doors"))
                    AddContractComponent(sw, model, assembly, result,
                        Path.Combine(result.workingPack, Text(row, "sourceAssembly")), row, false,
                        result.root.doorPlacements);
                foreach (Dictionary<string, object> row in ContractRows(result, "shelves"))
                    AddContractComponent(sw, model, assembly, result,
                        Path.Combine(result.workingPack, Text(row, "source")), row, true,
                        result.root.shelfPlacements);
                Require(Safe(() => model.ForceRebuild3(false), false), "ROOT_REBUILD_FAILED", 20,
                    "root rebuild failed after exact door and shelf replacement");
                RootSnapshot snapshot = CaptureRootSnapshot(sw, model, result);
                ValidateRootSnapshot(snapshot, result);
                result.root.afterEdit = snapshot;
                SaveExact(model, result, "root_save");
                result.root.sha256 = Sha256(rootPath);
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.processExited, "ROOT_SESSION_DID_NOT_EXIT", 20,
                "root edit SolidWorks session did not exit cleanly");
        }

        private static void VerifyRootFreshReadonly(RunResult result)
        {
            AssertAuthorizationStillValid(result, "before_root_readonly_reopen");
            var session = new SessionEvidence { purpose = "root_fresh_readonly_reopen" };
            result.sessions.Add(session);
            ISldWorks sw = null;
            ModelDoc2 model = null;
            string hashBefore = Sha256(result.root.path);
            try
            {
                sw = StartOwnedSession(result, session);
                Require(sw != null && OwnedSessionIsExclusive(session),
                    "ROOT_REOPEN_SESSION_INVALID", 21,
                    "exclusive SolidWorks session unavailable for root read-only reopen");
                int errors = 0, warnings = 0;
                model = OpenDocument(sw, result.root.path, true, ref errors, ref warnings);
                result.root.readonlyOpenErrors = errors;
                result.root.readonlyOpenWarnings = warnings;
                Require(model != null && errors == 0 && warnings == 32,
                    "ROOT_READONLY_REOPEN_WARNING_MISMATCH", 21,
                    "trusted root fresh read-only reopen must return exact errors=0 and warnings=32");
                Require(Safe(() => model.ForceRebuild3(false), false),
                    "ROOT_READONLY_REBUILD_FAILED", 21,
                    "fresh read-only root rebuild failed");
                RootSnapshot snapshot = CaptureRootSnapshot(sw, model, result);
                ValidateRootSnapshot(snapshot, result);
                Require(snapshot.signature == result.root.afterEdit.signature,
                    "ROOT_FRESH_REOPEN_TOPOLOGY_DRIFT", 21,
                    "root component topology changed after fresh read-only reopen");
                result.root.afterReadonlyReopen = snapshot;
            }
            finally
            {
                CloseDocument(sw, ref model);
                CloseOwnedSession(ref sw, result, session);
            }
            Require(session.processExited && string.Equals(hashBefore, Sha256(result.root.path),
                    StringComparison.OrdinalIgnoreCase),
                "ROOT_READONLY_REOPEN_MUTATED_FILE", 21,
                "fresh read-only root reopen must exit and preserve the saved file hash");
            result.root.freshReadonlyReopenPassed = true;
        }

        private static List<Dictionary<string, object>> ContractRows(RunResult result, string name)
        {
            return ArrayValue(result.contractSnapshot, name).Select(value =>
                value as Dictionary<string, object>).Where(value => value != null).ToList();
        }

        private static List<string> ContractRemoveNames(RunResult result, string name)
        {
            return ArrayValue(ChildObject(result.contractSnapshot, "remove"), name)
                .Select(ValueText).ToList();
        }

        private static void DeleteExactDirectComponents(ModelDoc2 model, List<string> exactNames,
            List<string> evidence, string stage, int expectedCount)
        {
            Require(exactNames.Count == expectedCount && exactNames.Distinct(
                StringComparer.Ordinal).Count() == expectedCount,
                stage + "_DELETE_CONTRACT_INVALID", 20,
                stage + " delete contract must contain exact unique direct instance names");
            List<Component2> direct = DirectComponents(model);
            var targets = new List<Component2>();
            foreach (string exact in exactNames)
            {
                List<Component2> matches = direct.Where(component => string.Equals(
                    LeafComponentName(Safe(() => component.Name2, "")), exact,
                    StringComparison.Ordinal)).ToList();
                Require(matches.Count == 1, stage + "_DELETE_EXACT_INSTANCE_MISMATCH", 20,
                    stage + " exact direct instance must occur once: " + exact);
                targets.Add(matches[0]);
            }
            model.ClearSelection2(true);
            foreach (Component2 target in targets)
            {
                if (Safe(() => target.IsSuppressed(), false))
                    Require(Safe(() => target.SetSuppression2(
                        (int)swComponentSuppressionState_e.swComponentResolved), -1) >= 0,
                        stage + "_DELETE_TARGET_RESOLVE_FAILED", 20,
                        "failed to resolve exact delete target: " + LeafComponentName(target.Name2));
                Require(Safe(() => target.Select4(true, null, false), false),
                    stage + "_DELETE_SELECTION_FAILED", 20,
                    "failed to select exact direct delete target: " + LeafComponentName(target.Name2));
                evidence.Add(LeafComponentName(target.Name2));
            }
            Require(evidence.Count == expectedCount, stage + "_DELETE_SELECTION_COUNT_INVALID", 20,
                "selected delete target count differs from exact contract");
            model.EditDelete();
            model.ClearSelection2(true);
            var remaining = new HashSet<string>(DirectComponents(model).Select(component =>
                LeafComponentName(Safe(() => component.Name2, ""))), StringComparer.Ordinal);
            Require(exactNames.All(name => !remaining.Contains(name)),
                stage + "_DELETE_DID_NOT_REMOVE_EXACT_INSTANCES", 20,
                "one or more exact direct instances remained after delete");
        }

        private static void AddContractComponent(ISldWorks sw, ModelDoc2 model, AssemblyDoc assembly,
            RunResult result, string sourcePath, Dictionary<string, object> row, bool fixedRequired,
            List<PlacementEvidence> evidence)
        {
            Require(File.Exists(sourcePath) && IsUnder(sourcePath, result.workingPack) &&
                !HasReparsePoint(sourcePath) && FileLinkCount(sourcePath) == 1,
                "PLACEMENT_SOURCE_INVALID", 22,
                "placement source must be a local single-link CAD file: " + sourcePath);
            double[] target = ArrayValue(row, "transform").Select(value => Convert.ToDouble(value,
                CultureInfo.InvariantCulture)).ToArray();
            Require(target.Length == 16 && IdentityRotation(target) && Near(target[12], 1.0,
                    TransformTolerance),
                "PLACEMENT_TRANSFORM_CONTRACT_INVALID", 22,
                "placement transform must be a 16-value identity rotation transform");
            string role = Text(row, "instanceRole");
            Component2 component = Safe(() => assembly.AddComponent5(sourcePath,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "", false, "", target[9], target[10], target[11]) as Component2, null);
            Require(component != null, "ADD_COMPONENT_FAILED", 22,
                "AddComponent5 failed for contract role " + role);
            MathUtility math = sw.GetMathUtility() as MathUtility;
            MathTransform transform = math == null ? null : math.CreateTransform(target) as MathTransform;
            Require(transform != null && Safe(() => component.SetTransformAndSolve2(transform), false),
                "SET_COMPONENT_TRANSFORM_FAILED", 22,
                "failed to apply exact contract transform for " + role);
            model.ClearSelection2(true);
            Require(Safe(() => component.Select4(false, null, false), false),
                "COMPONENT_FIXED_STATE_SELECTION_FAILED", 22,
                "failed to select newly added component for fixed-state normalization: " + role);
            bool beforeFixed = Safe(() => component.IsFixed(), false);
            if (fixedRequired && !beforeFixed) assembly.FixComponent();
            if (!fixedRequired && beforeFixed) assembly.UnfixComponent();
            model.ClearSelection2(true);
            double[] actual = TransformData(Safe(() => component.Transform2 as MathTransform, null));
            bool actualFixed = Safe(() => component.IsFixed(), false);
            Require(SameTransform(target, actual, TransformTolerance) &&
                actualFixed == fixedRequired && !Safe(() => component.IsSuppressed(), true) &&
                SamePath(Safe(() => component.GetPathName(), ""), sourcePath),
                "PLACEMENT_READBACK_MISMATCH", 22,
                "component transform/fixed/path readback failed for " + role);
            evidence.Add(new PlacementEvidence
            {
                role = role,
                sourcePath = sourcePath,
                componentName = LeafComponentName(Safe(() => component.Name2, "")),
                transform = actual.ToList(),
                fixedState = actualFixed,
                active = true
            });
        }

        private static void SaveExact(ModelDoc2 model, RunResult result, string checkpoint)
        {
            AssertAuthorizationStillValid(result, "before_" + checkpoint);
            int errors = 0, warnings = 0;
            bool saved = Safe(() => model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                ref errors, ref warnings), false);
            Require(saved && errors == 0 && warnings == 0,
                checkpoint.ToUpperInvariant() + "_FAILED", 23,
                checkpoint + " must return saved=true, errors=0 and warnings=0");
            if (checkpoint == "frame_save")
            {
                result.frame.saveErrors = errors;
                result.frame.saveWarnings = warnings;
            }
            else
            {
                result.root.saveErrors = errors;
                result.root.saveWarnings = warnings;
            }
        }

        private static FrameSnapshot CaptureFrameSnapshot(ModelDoc2 model, RunResult result)
        {
            var snapshot = new FrameSnapshot { traversalComplete = true };
            List<Component2> direct = DirectComponents(model);
            snapshot.directComponentCount = direct.Count;
            foreach (Component2 component in direct)
            {
                ComponentEvidence row = ComponentEvidenceFor(component, false);
                snapshot.components.Add(row);
                if (string.Equals(Path.GetFileName(row.path), "门框 横隔板.sldprt",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(row.path), "门框 横隔板R.SLDPRT",
                        StringComparison.OrdinalIgnoreCase)) snapshot.crossbars.Add(row);
            }
            snapshot.featureHealth = CaptureFeatureHealth(model);
            snapshot.traversalComplete = snapshot.featureHealth.traversalComplete;
            snapshot.signature = ComponentSignature(snapshot.components);
            return snapshot;
        }

        private static void ValidateFrameSnapshot(FrameSnapshot snapshot, RunResult result)
        {
            Require(snapshot != null && snapshot.traversalComplete &&
                snapshot.directComponentCount == 18 && snapshot.components.Count == 18 &&
                snapshot.components.All(row => row.active) && snapshot.crossbars.Count == 12 &&
                snapshot.crossbars.All(row => !row.fixedState) &&
                snapshot.featureHealth.issues.Count == 0,
                "FRAME_SNAPSHOT_COUNT_OR_HEALTH_INVALID", 24,
                "frame must have 18 active direct components, 12 unfixed crossbars and zero feature issues");
            foreach (Dictionary<string, object> contractRow in ContractRows(result, "frameCrossbars"))
            {
                string source = Text(contractRow, "source");
                double[] expected = ArrayValue(contractRow, "transform").Select(value =>
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)).ToArray();
                List<ComponentEvidence> matches = snapshot.crossbars.Where(row =>
                    string.Equals(Path.GetFileName(row.path), source, StringComparison.OrdinalIgnoreCase) &&
                    SameTransform(expected, row.transform.ToArray(), TransformTolerance)).ToList();
                Require(matches.Count == 1, "FRAME_CONTRACT_PLACEMENT_MISMATCH", 24,
                    "frame contract row does not have exactly one active direct placement: " +
                    Text(contractRow, "instanceRole"));
            }
        }

        private static RootSnapshot CaptureRootSnapshot(ISldWorks sw, ModelDoc2 model, RunResult result)
        {
            var snapshot = new RootSnapshot { traversalComplete = true };
            List<Component2> direct = DirectComponents(model);
            snapshot.topLevelComponentCount = direct.Count;
            List<Component2> all = AllComponents(model, out snapshot.traversalComplete);
            snapshot.recursiveComponentCount = all.Count;
            snapshot.activeRecursiveComponentCount = all.Count(component =>
                !Safe(() => component.IsSuppressed(), true));
            snapshot.suppressedComponentCount = all.Count - snapshot.activeRecursiveComponentCount;
            snapshot.featureHealth = CaptureFeatureHealth(model);
            snapshot.traversalComplete = snapshot.traversalComplete &&
                snapshot.featureHealth.traversalComplete;
            foreach (Component2 component in direct)
            {
                ComponentEvidence row = ComponentEvidenceFor(component, false);
                snapshot.topLevelComponents.Add(row);
                string leaf = Path.GetFileName(row.path);
                if (string.Equals(leaf, "ordinary_door_left_W381_H254p428571.SLDASM",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(leaf, "ordinary_door_right_W381_H254p428571.SLDASM",
                        StringComparison.OrdinalIgnoreCase)) snapshot.doors.Add(row);
                else if (string.Equals(leaf, "箱体横层板L焊接.SLDASM",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(leaf, "箱体横层板R焊接.SLDASM",
                        StringComparison.OrdinalIgnoreCase)) snapshot.shelves.Add(row);
                else if (string.Equals(leaf, FrameFileName, StringComparison.OrdinalIgnoreCase))
                    snapshot.frameAssemblyCount++;
            }
            foreach (Component2 component in all)
            {
                string path = Safe(() => component.GetPathName(), "");
                string leaf = Path.GetFileName(path);
                if (string.Equals(leaf, "mechanical_lock_tongue.SLDPRT",
                        StringComparison.OrdinalIgnoreCase))
                    snapshot.tongues.Add(ComponentEvidenceFor(component, true));
                if ((string.Equals(leaf, "门框 横隔板.sldprt", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(leaf, "门框 横隔板R.SLDPRT", StringComparison.OrdinalIgnoreCase)) &&
                    !Safe(() => component.IsSuppressed(), true)) snapshot.activeFrameCrossbars++;
            }
            CaptureRootDependencyClosure(sw, snapshot, result);
            snapshot.signature = RootSignature(snapshot);
            return snapshot;
        }

        private static void CaptureRootDependencyClosure(ISldWorks sw, RootSnapshot snapshot,
            RunResult result)
        {
            List<string> dependencies = RootedCadPaths(Safe(() => sw.GetDocumentDependencies2(
                result.root.path, true, true, false), null));
            dependencies.Add(result.root.path);
            var closure = new HashSet<string>(dependencies.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            snapshot.closureCadFileCount = closure.Count;
            snapshot.closureAssemblyFileCount = closure.Count(IsAssembly);
            snapshot.closurePartFileCount = closure.Count(IsPart);
            snapshot.missingFileCount = closure.Count(path => !File.Exists(path));
            snapshot.externalFileCount = closure.Count(path => !IsUnder(path, result.workingPack));
            snapshot.closurePaths = closure.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            var forbidden = new HashSet<string>(ArrayValue(result.contractSnapshot,
                "forbiddenFinalReferenceLeafNames").Select(ValueText), StringComparer.OrdinalIgnoreCase);
            snapshot.forbiddenReferenceLeaves = closure.Select(Path.GetFileName)
                .Where(forbidden.Contains).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ValidateRootSnapshot(RootSnapshot snapshot, RunResult result)
        {
            Dictionary<string, object> expected = ChildObject(result.contractSnapshot,
                "expectedWorkingAssembly");
            Dictionary<string, object> closureExpected = ChildObject(result.contractSnapshot,
                "expectedFinalClosure");
            Require(snapshot != null && snapshot.traversalComplete &&
                snapshot.topLevelComponentCount == Convert.ToInt32(Number(expected,
                    "topLevelComponentCount"), CultureInfo.InvariantCulture) &&
                snapshot.recursiveComponentCount == Convert.ToInt32(Number(expected,
                    "recursiveComponentCount"), CultureInfo.InvariantCulture) &&
                snapshot.activeRecursiveComponentCount == Convert.ToInt32(Number(expected,
                    "activeRecursiveComponentCount"), CultureInfo.InvariantCulture) &&
                snapshot.suppressedComponentCount == Convert.ToInt32(Number(expected,
                    "suppressedComponentCount"), CultureInfo.InvariantCulture) &&
                snapshot.doors.Count == 14 && snapshot.shelves.Count == 12 &&
                snapshot.tongues.Count == 14 && snapshot.activeFrameCrossbars == 12 &&
                snapshot.frameAssemblyCount == 1,
                "ROOT_COMPONENT_COUNT_MISMATCH", 25,
                "root must match exact 41/294/294/0 topology with 14 doors, 12 shelves, 12 frame crossbars and 14 tongues");
            Require(snapshot.closureCadFileCount == Convert.ToInt32(Number(closureExpected,
                    "cadFileCount"), CultureInfo.InvariantCulture) &&
                snapshot.closureAssemblyFileCount == Convert.ToInt32(Number(closureExpected,
                    "assemblyFileCount"), CultureInfo.InvariantCulture) &&
                snapshot.closurePartFileCount == Convert.ToInt32(Number(closureExpected,
                    "partFileCount"), CultureInfo.InvariantCulture) &&
                snapshot.missingFileCount == 0 && snapshot.externalFileCount == 0 &&
                snapshot.forbiddenReferenceLeaves.Count == 0,
                "ROOT_REFERENCE_CLOSURE_MISMATCH", 25,
                "root closure must be exact 55 = 14 SLDASM + 41 SLDPRT with no missing, external or old-door references");
            Require(RootFeatureHealthAllowed(snapshot.featureHealth, result.contractSnapshot),
                "ROOT_FEATURE_HEALTH_MISMATCH", 25,
                "root may contain at most only the trusted right-side-panel Reference 51 warning tuple");

            foreach (Dictionary<string, object> contractRow in ContractRows(result, "doors"))
            {
                string source = Text(contractRow, "sourceAssembly");
                double[] transform = ArrayValue(contractRow, "transform").Select(value =>
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)).ToArray();
                List<ComponentEvidence> matches = snapshot.doors.Where(row =>
                    string.Equals(Path.GetFileName(row.path), source, StringComparison.OrdinalIgnoreCase) &&
                    !row.fixedState && row.active && SameTransform(transform, row.transform.ToArray(),
                        TransformTolerance)).ToList();
                Require(matches.Count == 1, "ROOT_DOOR_PLACEMENT_MISMATCH", 25,
                    "root door contract row does not have exactly one unfixed placement: " +
                    Text(contractRow, "instanceRole"));
                double expectedTongueX = Number(contractRow, "tongueGlobalXmm");
                double expectedTongueY = Number(contractRow, "tongueGlobalYmm");
                double expectedTongueZ = Number(contractRow, "tongueGlobalZmm");
                Require(snapshot.tongues.Count(row => row.active &&
                    Near(row.totalTransform[9] * 1000.0, expectedTongueX, 0.01) &&
                    Near(row.totalTransform[10] * 1000.0, expectedTongueY, 0.01) &&
                    Near(row.totalTransform[11] * 1000.0, expectedTongueZ, 0.01)) == 1,
                    "ROOT_TONGUE_PLACEMENT_MISMATCH", 25,
                    "one-door-one-mechanical-tongue transform failed for " +
                    Text(contractRow, "instanceRole"));
            }
            foreach (Dictionary<string, object> contractRow in ContractRows(result, "shelves"))
            {
                string source = Text(contractRow, "source");
                double[] transform = ArrayValue(contractRow, "transform").Select(value =>
                    Convert.ToDouble(value, CultureInfo.InvariantCulture)).ToArray();
                Require(snapshot.shelves.Count(row => row.active && row.fixedState &&
                    string.Equals(Path.GetFileName(row.path), source, StringComparison.OrdinalIgnoreCase) &&
                    SameTransform(transform, row.transform.ToArray(), TransformTolerance)) == 1,
                    "ROOT_SHELF_PLACEMENT_MISMATCH", 25,
                    "root shelf contract row does not have exactly one fixed placement: " +
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
            if (!HasExactKeys(policy, "mode", "maximumIssueCount", "allowedIssue") ||
                Text(policy, "mode") != "exact_unique_known_issue_only" ||
                Number(policy, "maximumIssueCount") != 1 ||
                !HasExactKeys(candidate, "name", "type", "errorCode", "errorCode2",
                    "warning") ||
                Text(candidate, "name") != "箱体右侧板焊接-1" ||
                Text(candidate, "type") != "Reference" || Number(candidate, "errorCode") != 51 ||
                Number(candidate, "errorCode2") != 51 || !Bool(candidate, "warning"))
                return false;
            allowed = candidate;
            return true;
        }

        private static bool RootFeatureHealthAllowed(FeatureHealthEvidence health,
            Dictionary<string, object> contract)
        {
            if (health == null || !health.traversalComplete) return false;
            Dictionary<string, object> allowed;
            if (!ExactKnownFeatureHealthPolicy(contract, out allowed)) return false;
            if (health.issues.Count == 0) return true;
            if (health.issues.Count != Convert.ToInt32(Number(
                    ChildObject(contract, "featureHealthPolicy"), "maximumIssueCount"),
                    CultureInfo.InvariantCulture)) return false;
            FeatureIssueEvidence issue = health.issues[0];
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
                if (!output.traversalComplete) { Release(feature); return output; }
                Feature next = null;
                try { next = feature.GetNextFeature() as Feature; }
                catch { output.traversalComplete = false; }
                Release(feature);
                feature = next;
            }
            if (feature != null) output.traversalComplete = false;
            return output;
        }

        private static void CaptureFeatureRecursive(Feature feature, FeatureHealthEvidence output,
            int depth)
        {
            if (feature == null) return;
            if (depth > 30 || output.featureCount > 20000)
            {
                output.traversalComplete = false;
                return;
            }
            output.featureCount++;
            bool warning = false;
            int error1, error2;
            bool suppressed;
            string name, type;
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
            catch
            {
                output.traversalComplete = false;
                return;
            }
            if (!suppressed && (error1 > 0 || error2 > 0 || warning))
                output.issues.Add(new FeatureIssueEvidence
                {
                    name = name,
                    type = type,
                    errorCode = error1,
                    errorCode2 = error2,
                    warning = warning,
                    depth = depth
                });
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureRecursive(child, output, depth + 1);
                if (!output.traversalComplete) { Release(child); return; }
                Feature next = null;
                try { next = child.GetNextSubFeature() as Feature; }
                catch { output.traversalComplete = false; }
                Release(child);
                child = next;
            }
            if (child != null) output.traversalComplete = false;
        }

        private static ComponentEvidence ComponentEvidenceFor(Component2 component, bool total)
        {
            double[] transform = TransformData(Safe(() => component.Transform2 as MathTransform, null)) ??
                new double[0];
            double[] totalTransform = TransformData(Safe(() => component.GetTotalTransform(true), null)) ??
                new double[0];
            return new ComponentEvidence
            {
                name = LeafComponentName(Safe(() => component.Name2, "")),
                path = Safe(() => component.GetPathName(), ""),
                configuration = Safe(() => component.ReferencedConfiguration, ""),
                active = !Safe(() => component.IsSuppressed(), true),
                fixedState = Safe(() => component.IsFixed(), false),
                transform = transform.ToList(),
                totalTransform = totalTransform.ToList()
            };
        }

        private static string ComponentSignature(IEnumerable<ComponentEvidence> rows)
        {
            return Sha256Text(string.Join("\n", rows.Select(row => row.name + "|" + row.path + "|" +
                row.configuration + "|" + row.active + "|" + row.fixedState + "|" +
                string.Join(",", row.transform.Select(Token))).OrderBy(value => value,
                    StringComparer.Ordinal).ToArray()));
        }

        private static string RootSignature(RootSnapshot snapshot)
        {
            var rows = snapshot.topLevelComponents.Select(row => "T|" + row.name + "|" + row.path +
                "|" + row.active + "|" + row.fixedState + "|" +
                string.Join(",", row.transform.Select(Token))).ToList();
            rows.AddRange(snapshot.tongues.Select(row => "L|" + row.path + "|" +
                string.Join(",", row.totalTransform.Select(Token))));
            rows.Add("C|" + snapshot.topLevelComponentCount + "|" + snapshot.recursiveComponentCount +
                "|" + snapshot.activeRecursiveComponentCount + "|" + snapshot.suppressedComponentCount +
                "|" + snapshot.closureCadFileCount);
            return Sha256Text(string.Join("\n", rows.OrderBy(value => value,
                StringComparer.Ordinal).ToArray()));
        }

        private static void AuditPostState(RunResult result)
        {
            List<string> files = CaptureFlatCadTree(result.workingPack, 88,
                "FINAL_WORKING_PACK_FILE_SYSTEM_INVALID");
            result.inventory.postCount = files.Count;
            result.inventory.postAssemblyCount = files.Count(IsAssembly);
            result.inventory.postPartCount = files.Count(IsPart);
            result.inventory.postDigest = InventoryDigest(files);
            Require(result.inventory.postCount == 88 && result.inventory.postAssemblyCount == 26 &&
                result.inventory.postPartCount == 62,
                "FINAL_WORKING_PACK_COUNT_INVALID", 30,
                "poststate working pack must remain flat 88 = 26 SLDASM + 62 SLDPRT");
            Require(string.Equals(Sha256(result.root.path), result.root.sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256(result.frame.path), result.frame.sha256,
                    StringComparison.OrdinalIgnoreCase),
                "FRESH_REOPEN_CORE_HASH_CHANGED_BEFORE_COMMIT", 30,
                "root or frame changed after fresh read-only reopen and before evidence commit");
            var allowChangedOriginals = new HashSet<string>(new[]
            {
                WorkingRootFileName, FrameFileName
            }, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> row in result.initialHashes)
            {
                string path = Path.Combine(result.workingPack, row.Key);
                string current = Sha256(path);
                if (!string.Equals(current, row.Value, StringComparison.OrdinalIgnoreCase))
                    result.inventory.changedOriginalFiles.Add(row.Key);
            }
            Require(result.inventory.changedOriginalFiles.Count == 2 &&
                new HashSet<string>(result.inventory.changedOriginalFiles,
                    StringComparer.OrdinalIgnoreCase).SetEquals(allowChangedOriginals),
                "ORIGINAL_CAD_MUTATION_ALLOWLIST_VIOLATION", 30,
                "among the original 75 files, only root and frame assemblies may change");
            foreach (DoorImportEntryEvidence row in result.doorModule.entries)
            {
                Require(File.Exists(row.sourcePath) && string.Equals(Sha256(row.sourcePath),
                        row.sourceSha256, StringComparison.OrdinalIgnoreCase),
                    "DOOR_MODULE_SOURCE_MUTATED", 30,
                    "root assembler must not mutate committed door-module sources: " +
                    row.flatTargetName);
                row.postSha256 = Sha256(row.targetPath);
            }
            Require(string.Equals(Sha256(result.doorModule.manifestPath),
                    result.doorModule.manifestSha256, StringComparison.OrdinalIgnoreCase),
                "DOOR_MODULE_MANIFEST_MUTATED", 30,
                "root assembler must not mutate the immutable source manifest");
            ValidateDoorOutputTree(result, result.doorModule.manifestPath);
            var forbidden = new HashSet<string>(ArrayValue(result.contractSnapshot,
                "forbiddenFinalReferenceLeafNames").Select(ValueText), StringComparer.OrdinalIgnoreCase);
            Require(forbidden.All(leaf => File.Exists(Path.Combine(result.workingPack, leaf))),
                "OLD_PHYSICAL_DOOR_FILES_MISSING_PREMATURELY", 30,
                "all 33 old door files must remain physical until the separate final-pack stage");
            Require(result.root.afterReadonlyReopen != null &&
                result.root.afterReadonlyReopen.forbiddenReferenceLeaves.Count == 0,
                "OLD_DOOR_REFERENCE_REINTRODUCED", 30,
                "old door files may remain physical but must not be referenced by the root closure");
            result.inventory.changedCadAllowlistPassed = true;
            result.doorModule.sourcesUnchanged = true;
        }

        private static void RollBack(RunResult result)
        {
            result.transaction.rollbackAttempted = true;
            try
            {
                Require(result.processes.finalGate, "ROLLBACK_PROCESS_GATE_FAILED", 50,
                    "rollback requires exact CAD process baseline restoration");
                if (result.transaction.backupComplete &&
                    Directory.Exists(result.transaction.backupDirectory))
                {
                    foreach (KeyValuePair<string, string> row in result.initialHashes)
                    {
                        string backup = Path.Combine(result.transaction.backupDirectory, row.Key);
                        string target = Path.Combine(result.workingPack, row.Key);
                        Require(File.Exists(backup) && string.Equals(Sha256(backup), row.Value,
                                StringComparison.OrdinalIgnoreCase),
                            "ROLLBACK_BACKUP_INVALID", 50,
                            "rollback backup hash mismatch: " + row.Key);
                        File.Copy(backup, target, true);
                    }
                }
                foreach (DoorImportEntryEvidence row in result.doorModule.entries)
                    if (File.Exists(row.targetPath)) File.Delete(row.targetPath);
                List<string> restored = CaptureFlatCadTree(result.workingPack, 75,
                    "ROLLBACK_WORKING_PACK_INVALID");
                Require(restored.Count == 75 && restored.All(path =>
                {
                    string expected;
                    return result.initialHashes.TryGetValue(Path.GetFileName(path), out expected) &&
                        string.Equals(Sha256(path), expected, StringComparison.OrdinalIgnoreCase);
                }) && InventoryDigest(restored) == result.inventory.preDigest,
                    "ROLLBACK_HASH_AUDIT_FAILED", 50,
                    "rollback did not restore exact 75-file prestate and remove exact imported 13");
                result.transaction.rollbackCompleted = true;
                result.transaction.rollbackRestoredDigest = InventoryDigest(restored);
            }
            catch (Exception ex)
            {
                result.transaction.rollbackCompleted = false;
                result.transaction.rollbackError = SafeException(ex);
            }
        }

        private static void TryWriteFailureEvidence(RunResult result)
        {
            try
            {
                if (File.Exists(result.evidence.path)) File.Delete(result.evidence.path);
                result.evidence.written = true;
                result.evidence.sha256 = "";
                result.evidence_commitment_sha256 = EvidenceCommitmentDigest(result);
                WriteJsonAtomicNew(result.evidence.path, result);
                result.evidence.sha256 = Sha256(result.evidence.path);
            }
            catch (Exception ex)
            {
                result.evidence.writeError = SafeException(ex);
            }
        }

        private static void RemoveOrQuarantineUnpairedArtifact(RunResult result, string path,
            string label)
        {
            if (result == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try { File.Delete(path); }
            catch { }
            if (!File.Exists(path)) return;
            string quarantine = path + ".unpaired-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Move(path, quarantine);
                result.transaction.unpairedArtifactQuarantinePaths.Add(quarantine);
            }
            catch (Exception ex)
            {
                result.transaction.rollbackError = FirstNonEmpty(result.transaction.rollbackError,
                    "unpaired " + label + " could not be removed or quarantined: " + ex.Message);
            }
        }

        private static void ValidateCommittedRootEvidence(RunResult result)
        {
            Require(File.Exists(result.evidence.path) && !HasReparsePoint(result.evidence.path) &&
                FileLinkCount(result.evidence.path) == 1,
                "ROOT_EVIDENCE_COMMIT_FILE_INVALID", 41,
                "root evidence must be a committed non-reparse single-link file");
            string hashBefore = Sha256(result.evidence.path);
            Dictionary<string, object> evidence = ReadJsonObject(result.evidence.path);
            Require(Text(evidence, "schema") == ResultSchema && Bool(evidence, "success") &&
                Bool(evidence, "committed") &&
                string.Equals(Text(evidence, "evidence_commitment_sha256"),
                    EvidenceCommitmentDigest(evidence), StringComparison.OrdinalIgnoreCase) &&
                !evidence.ContainsKey("phase_receipt_path") &&
                !evidence.ContainsKey("phase_receipt_sha256") &&
                !evidence.ContainsKey("phase_receipt_committed") &&
                !evidence.ContainsKey("rootReceiptPath") &&
                !evidence.ContainsKey("rootReceiptSha256") &&
                string.Equals(hashBefore, result.evidence.sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256(result.evidence.path), hashBefore,
                    StringComparison.OrdinalIgnoreCase),
                "ROOT_EVIDENCE_COMMIT_REREAD_INVALID", 41,
                "root evidence failed semantic commitment or immutable re-read validation");
        }

        private static void WriteRootReceiptAtomic(RunResult result)
        {
            Require(result.success && result.committed && result.evidence.written &&
                File.Exists(result.evidence.path) && IsSha256(result.evidence.sha256) &&
                IsSha256(result.evidence_commitment_sha256) &&
                IsSha256(result.predecessorReceiptSha256) &&
                !File.Exists(result.rootReceiptPath),
                "ROOT_RECEIPT_PRECONDITION_FAILED", 42,
                "root receipt requires committed evidence and the exact live predecessor chain");
            byte[] authorizationBytes = File.ReadAllBytes(result.authorization.path);
            Require(string.Equals(Sha256Bytes(authorizationBytes), result.authorization.sha256,
                    StringComparison.OrdinalIgnoreCase),
                "ROOT_RECEIPT_AUTHORIZATION_BYTES_CHANGED", 42,
                "authorization bytes changed before root receipt commit");
            var receipt = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "schema", RootReceiptSchema },
                { "phase", ExecutionPhase },
                { "success", true },
                { "completedAt", result.completedAtUtc },
                { "taskId", result.taskId },
                { "taskRevision", result.plan.taskRevision },
                { "taskDigest", result.plan.taskDigest },
                { "requestDigest", result.plan.requestDigest },
                { "leaseId", result.authorization.leaseId },
                { "leaseExpiresAt", result.authorization.leaseExpiresAt },
                { "attempt", result.execution.attempt },
                { "planSha256", result.plan.sha256 },
                { "authorizationId", result.authorization.authorizationId },
                { "authorizationSha256", result.authorization.sha256 },
                { "authorizationJsonBase64", Convert.ToBase64String(authorizationBytes) },
                { "authorizationIssuedAt", result.authorization.issuedAt },
                { "authorizationExpiresAt", result.authorization.expiresAt },
                { "recipeId", ExpectedRecipeId },
                { "recipeDigest", ExpectedRecipeDigest.ToUpperInvariant() },
                { "toolId", ToolId },
                { "toolSourceNormalizedSha256", result.tool.sourceNormalizedSha256 },
                { "toolExecutableSha256", result.tool.executableSha256 },
                { "evidencePath", "evidence/" + ResultFileName },
                { "evidenceSha256", result.evidence.sha256 },
                { "evidenceCommitmentSha256", result.evidence_commitment_sha256 },
                { "preInventoryDigest", result.inventory.preDigest },
                { "postInventoryDigest", result.inventory.postDigest },
                { "predecessorReceiptSha256", result.predecessorReceiptSha256 }
            };
            RequireExactKeys(receipt, "root runtime receipt", RuntimeReceiptKeys);
            WriteJsonAtomicNew(result.rootReceiptPath, receipt);
            string receiptSha = Sha256(result.rootReceiptPath);
            Dictionary<string, object> reread = ReadJsonObject(result.rootReceiptPath);
            RequireExactKeys(reread, "committed root runtime receipt", RuntimeReceiptKeys);
            Require(string.Equals(Text(reread, "evidenceSha256"), result.evidence.sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(reread, "evidenceCommitmentSha256"),
                    result.evidence_commitment_sha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Text(reread, "predecessorReceiptSha256"),
                    result.predecessorReceiptSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Sha256(result.rootReceiptPath), receiptSha,
                    StringComparison.OrdinalIgnoreCase),
                "ROOT_RECEIPT_COMMIT_REREAD_INVALID", 42,
                "root receipt failed immutable re-read validation");
            result.rootReceiptSha256 = receiptSha;
        }

        private static void WriteRootManifestAtomic(RunResult result)
        {
            Require(result.success && result.committed && result.evidence.written &&
                File.Exists(result.evidence.path) && IsSha256(result.evidence.sha256) &&
                result.transaction.receiptCommitted && File.Exists(result.rootReceiptPath) &&
                string.Equals(Sha256(result.rootReceiptPath), result.rootReceiptSha256,
                    StringComparison.OrdinalIgnoreCase),
                "ROOT_MANIFEST_PRECONDITION_FAILED", 42,
                "successful immutable evidence and receipt must exist before root manifest commit");
            var manifest = new RootStageManifest
            {
                schema = ManifestSchema,
                task = new ManifestTaskBinding
                {
                    id = result.taskId,
                    revision = result.plan.taskRevision,
                    digest = result.plan.taskDigest
                },
                recipe = new ManifestRecipeBinding
                {
                    id = ExpectedRecipeId,
                    version = 1,
                    digest = ExpectedRecipeDigest.ToUpperInvariant()
                },
                plan = new ManifestPlanBinding
                {
                    sha256 = result.plan.sha256
                },
                tool = new ManifestToolBinding
                {
                    id = ToolId,
                    sourceNormalizedSha256 = result.tool.sourceNormalizedSha256,
                    executableSha256 = result.tool.executableSha256
                },
                evidence = new ManifestEvidenceBinding
                {
                    path = result.evidence.path,
                    sha256 = result.evidence.sha256
                },
                rootAssembly = new ManifestRootAssemblyBinding
                {
                    path = result.root.path,
                    sha256 = result.root.sha256
                },
                generatedAtUtc = result.completedAtUtc
            };
            WriteJsonAtomicNew(result.rootManifestPath, manifest);
            result.rootManifestSha256 = Sha256(result.rootManifestPath);
        }

        private static void WriteJsonAtomicNew(string path, object value)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            Require(!HasReparsePoint(directory), "EVIDENCE_DIRECTORY_REPARSE_PATH", 60,
                "evidence directory is a reparse point");
            string temp = Path.Combine(directory, "." + Path.GetFileName(path) + ".tmp-" +
                Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + "-" +
                Guid.NewGuid().ToString("N"));
            byte[] bytes = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 512
            }.Serialize(value) + "\n");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 65536, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                Require(!File.Exists(path), "IMMUTABLE_EVIDENCE_ALREADY_EXISTS", 60,
                    "refusing to replace immutable evidence: " + path);
                File.Move(temp, path);
                Require(!HasReparsePoint(path) && FileLinkCount(path) == 1,
                    "IMMUTABLE_EVIDENCE_FILE_IDENTITY_INVALID", 60,
                    "committed evidence must be a non-reparse single-link file");
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static ModelDoc2 OpenDocument(ISldWorks sw, string path, bool readOnly,
            ref int errors, ref int warnings)
        {
            int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
            if (readOnly) options |= (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;
            try
            {
                return sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocASSEMBLY,
                    options, "", ref errors, ref warnings) as ModelDoc2;
            }
            catch { return null; }
        }

        private static void CloseDocument(ISldWorks sw, ref ModelDoc2 model)
        {
            if (model == null) return;
            string title = "";
            try { title = model.GetTitle(); }
            catch { }
            Release(model);
            model = null;
            if (sw != null && !string.IsNullOrWhiteSpace(title)) Try(() => sw.CloseDoc(title));
        }

        private static ISldWorks StartOwnedSession(RunResult result, SessionEvidence session)
        {
            List<int> monitorsBefore = ProcessIds("sldProcMon");
            ISldWorks sw = null;
            try
            {
                Type type = Type.GetTypeFromProgID("SldWorks.Application.28", true);
                sw = type == null ? null : Activator.CreateInstance(type) as ISldWorks;
            }
            catch { }
            if (sw == null) return null;
            Try(() => sw.Visible = false);
            Try(() => sw.UserControl = false);
            Try(() => sw.CommandInProgress = true);
            session.sldworksProcessId = Safe(() => sw.GetProcessID(), 0);
            session.sldworksStartUtcTicks = ProcessStartUtcTicks(session.sldworksProcessId,
                "SLDWORKS");
            session.solidworksRevision = Safe(() => sw.RevisionNumber(), "");
            session.solidworksExecutablePath = ProcessExecutablePath(session.sldworksProcessId,
                "SLDWORKS");
            session.solidworksExecutableSha256 = File.Exists(session.solidworksExecutablePath) ?
                Sha256(session.solidworksExecutablePath) : "";
            session.created = session.sldworksProcessId > 0 &&
                session.sldworksStartUtcTicks > 0 &&
                !result.processes.baselineSldworks.Contains(session.sldworksProcessId) &&
                session.solidworksRevision.StartsWith(ExpectedSolidWorksVersionPrefix,
                    StringComparison.Ordinal) &&
                SamePath(session.solidworksExecutablePath, ExpectedSolidWorksExePath) &&
                string.Equals(session.solidworksExecutableSha256, ExpectedSolidWorksExeSha256,
                    StringComparison.OrdinalIgnoreCase);
            session.solidworks2020ExactGate = session.created;
            if (session.created && !result.processes.createdSldworks.Contains(
                    session.sldworksProcessId))
                result.processes.createdSldworks.Add(session.sldworksProcessId);
            Thread.Sleep(500);
            CaptureOwnedMonitors(result, session, monitorsBefore);
            session.monitorIdentityGate = session.sldprocmonProcesses.Count > 0 &&
                session.sldprocmonProcesses.All(process => process.ownershipVerified &&
                    process.parentSldworksProcessId == session.sldworksProcessId);
            session.started = true;
            return sw;
        }

        private static bool OwnedSessionIsExclusive(SessionEvidence session)
        {
            if (session == null || !session.created) return false;
            List<int> running = ProcessIds("SLDWORKS");
            return running.Count == 1 && running[0] == session.sldworksProcessId &&
                session.solidworks2020ExactGate && session.monitorIdentityGate &&
                ExactProcessIdentityExists(session.sldworksProcessId, "SLDWORKS",
                    session.sldworksStartUtcTicks, session.solidworksExecutablePath,
                    ExpectedSolidWorksExeSha256);
        }

        private static void CloseOwnedSession(ref ISldWorks sw, RunResult result,
            SessionEvidence session)
        {
            if (session == null) return;
            if (sw != null)
            {
                CaptureOwnedMonitors(result, session, null);
                ISldWorks current = sw;
                Try(() => current.CloseAllDocuments(true));
                Try(() => current.CommandInProgress = false);
                session.exitRequested = TryAction(() => current.ExitApp());
                Release(current);
                sw = null;
            }
            session.processExited = WaitForExactProcessExit(session, 10000);
            if (!session.processExited) ForceStopOwnedSldworks(result, session);
            CleanupOwnedMonitors(result, session);
            session.monitorsExited = session.sldprocmonProcesses.Count > 0 &&
                session.sldprocmonProcesses.All(process =>
                    ProcessStartUtcTicks(process.pid, "sldProcMon") == 0);
            session.completedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        private static void CleanupOwnedProcesses(RunResult result)
        {
            foreach (SessionEvidence session in result.sessions)
            {
                if (!session.processExited) ForceStopOwnedSldworks(result, session);
                CleanupOwnedMonitors(result, session);
            }
        }

        private static void CaptureOwnedMonitors(RunResult result, SessionEvidence session,
            List<int> monitorsBefore)
        {
            if (session == null || !session.created ||
                !ExactProcessIdentityExists(session.sldworksProcessId, "SLDWORKS",
                    session.sldworksStartUtcTicks, session.solidworksExecutablePath,
                    ExpectedSolidWorksExeSha256)) return;
            var before = new HashSet<int>(monitorsBefore ?? new List<int>());
            var assigned = new HashSet<int>(result.sessions.SelectMany(row =>
                row.sldprocmonProcesses.Select(process => process.pid)));
            string pattern = ExactParentPattern(session.sldworksProcessId);
            foreach (ProcessIdentity identity in ProcessInfoByName("sldProcMon"))
            {
                bool trusted = !before.Contains(identity.pid) && !assigned.Contains(identity.pid) &&
                    !result.processes.baselineSldprocmon.Contains(identity.pid) &&
                    identity.startUtcTicks > 0 && SamePath(identity.executablePath,
                        ExpectedMonitorExePath) && File.Exists(identity.executablePath) &&
                    string.Equals(Sha256(identity.executablePath), ExpectedMonitorExeSha256,
                        StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(identity.commandLine ?? "",
                        pattern, RegexOptions.CultureInvariant);
                if (!trusted) continue;
                identity.parentSldworksProcessId = session.sldworksProcessId;
                identity.ownershipVerified = true;
                identity.executableSha256 = ExpectedMonitorExeSha256;
                session.sldprocmonProcesses.Add(identity);
                assigned.Add(identity.pid);
                if (!result.processes.createdSldprocmon.Contains(identity.pid))
                    result.processes.createdSldprocmon.Add(identity.pid);
            }
        }

        private static void CleanupOwnedMonitors(RunResult result, SessionEvidence session)
        {
            if (session == null) return;
            foreach (ProcessIdentity expected in session.sldprocmonProcesses)
            {
                if (result.processes.baselineSldprocmon.Contains(expected.pid))
                {
                    AddUnique(result.processes.cleanupRefusedBaseline, expected.pid);
                    continue;
                }
                ProcessIdentity live = ExactOwnedMonitor(expected, session.sldworksProcessId);
                if (live == null)
                {
                    if (ProcessStartUtcTicks(expected.pid, "sldProcMon") == 0)
                        AddUnique(result.processes.alreadyExited, expected.pid);
                    else
                        result.processes.cleanupErrors.Add("refused unproven/reused sldProcMon PID " +
                            expected.pid.ToString(CultureInfo.InvariantCulture));
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
                {
                    AddUnique(result.processes.alreadyExited, expected.pid);
                }
                catch (Exception ex)
                {
                    result.processes.cleanupErrors.Add("sldProcMon PID " + expected.pid + ": " +
                        ex.Message);
                }
            }
        }

        private static void ForceStopOwnedSldworks(RunResult result, SessionEvidence session)
        {
            if (session == null || session.sldworksProcessId <= 0) return;
            int pid = session.sldworksProcessId;
            if (result.processes.baselineSldworks.Contains(pid))
            {
                AddUnique(result.processes.cleanupRefusedBaseline, pid);
                return;
            }
            if (!ExactProcessIdentityExists(pid, "SLDWORKS", session.sldworksStartUtcTicks,
                    session.solidworksExecutablePath, ExpectedSolidWorksExeSha256))
            {
                if (ProcessStartUtcTicks(pid, "SLDWORKS") == 0)
                {
                    session.processExited = true;
                    AddUnique(result.processes.alreadyExited, pid);
                }
                else result.processes.cleanupErrors.Add("refused unproven/reused SLDWORKS PID " + pid);
                return;
            }
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
                session.processExited = WaitForExactProcessExit(session, 2000);
                if (session.processExited) AddUnique(result.processes.forceStopped, pid);
                else result.processes.cleanupErrors.Add("owned SLDWORKS PID did not exit: " + pid);
            }
            catch (ArgumentException)
            {
                session.processExited = true;
                AddUnique(result.processes.alreadyExited, pid);
            }
            catch (Exception ex)
            {
                result.processes.cleanupErrors.Add("SLDWORKS PID " + pid + ": " + ex.Message);
            }
        }

        private static void CaptureFinalProcessGate(RunResult result)
        {
            result.processes.finalSldworks = ProcessIds("SLDWORKS");
            result.processes.finalSldprocmon = ProcessIds("sldProcMon");
            result.processes.finalGate = result.processes.finalSldworks.Count == 0 &&
                result.processes.finalSldprocmon.Count == 0 &&
                result.processes.cleanupErrors.Count == 0 &&
                result.processes.cleanupRefusedBaseline.Count == 0 &&
                result.sessions.All(session => session.processExited &&
                    session.sldprocmonProcesses.All(process =>
                        ProcessStartUtcTicks(process.pid, "sldProcMon") == 0));
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

        private static string ExactParentPattern(int parentPid)
        {
            return "(?:^|\\s)--ppid=" + parentPid.ToString(CultureInfo.InvariantCulture) +
                "(?:\\s|$)";
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
                            commandLine = Convert.ToString(row["CommandLine"],
                                CultureInfo.InvariantCulture) ?? "",
                            executablePath = Convert.ToString(row["ExecutablePath"],
                                CultureInfo.InvariantCulture) ?? "",
                            startUtcTicks = ProcessStartUtcTicks(pid, name)
                        });
                    }
            }
            catch { }
            return output;
        }

        private static ProcessIdentity ExactOwnedMonitor(ProcessIdentity expected, int parentPid)
        {
            if (expected == null || !expected.ownershipVerified ||
                expected.parentSldworksProcessId != parentPid) return null;
            string pattern = ExactParentPattern(parentPid);
            return ProcessInfoByName("sldProcMon").FirstOrDefault(current =>
                current.pid == expected.pid && current.startUtcTicks == expected.startUtcTicks &&
                SamePath(current.executablePath, expected.executablePath) &&
                string.Equals(Sha256(current.executablePath), expected.executableSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(current.commandLine ?? "", pattern, RegexOptions.CultureInvariant));
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

        private static bool WaitForExactProcessExit(SessionEvidence session, int timeoutMs)
        {
            if (session == null || session.sldworksProcessId <= 0) return true;
            Stopwatch watch = Stopwatch.StartNew();
            int absent = 0;
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                if (!ExactProcessIdentityExists(session.sldworksProcessId, "SLDWORKS",
                    session.sldworksStartUtcTicks, session.solidworksExecutablePath,
                    ExpectedSolidWorksExeSha256)) absent++;
                else absent = 0;
                if (absent >= 3) return true;
                Thread.Sleep(250);
            }
            return false;
        }

        private static List<Component2> DirectComponents(ModelDoc2 model)
        {
            var output = new List<Component2>();
            Configuration configuration = Safe(() => model.GetActiveConfiguration() as Configuration,
                null);
            Component2 root = configuration == null ? null : Safe(() =>
                configuration.GetRootComponent3(true), null);
            Array children = root == null ? null : Safe(() => root.GetChildren() as Array, null);
            if (children != null)
                foreach (object value in children)
                {
                    Component2 component = value as Component2;
                    if (component != null) output.Add(component);
                }
            return output;
        }

        private static List<Component2> AllComponents(ModelDoc2 model, out bool traversalComplete)
        {
            traversalComplete = true;
            var output = new List<Component2>();
            var pending = new Stack<Component2>(DirectComponents(model).Reverse<Component2>());
            int guard = 0;
            while (pending.Count > 0)
            {
                if (++guard > 10000)
                {
                    traversalComplete = false;
                    break;
                }
                Component2 component = pending.Pop();
                output.Add(component);
                Array children = null;
                try { children = component.GetChildren() as Array; }
                catch { traversalComplete = false; break; }
                if (children == null) continue;
                var rows = new List<Component2>();
                foreach (object value in children)
                {
                    Component2 child = value as Component2;
                    if (child != null) rows.Add(child);
                }
                for (int index = rows.Count - 1; index >= 0; index--) pending.Push(rows[index]);
            }
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
                if (!Path.IsPathRooted(text)) continue;
                if (!IsPart(text) && !IsAssembly(text)) continue;
                output.Add(Path.GetFullPath(text));
            }
            return output;
        }

        private static List<string> CaptureFlatCadTree(string root, int traversalLimit,
            string status)
        {
            Require(Directory.Exists(root) && !HasReparsePoint(root), status, 70,
                "CAD root is missing or reparse: " + root);
            string[] directories = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly);
            Require(directories.Length == 0, status, 70,
                "CAD working pack/backup must remain flat; nested directories are forbidden");
            string[] allFiles = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly);
            var cad = new List<string>();
            int guard = 0;
            foreach (string file in allFiles)
            {
                Require(++guard <= Math.Max(traversalLimit + 20, 120), status, 70,
                    "flat CAD inventory exceeded fail-closed traversal limit");
                string full = Path.GetFullPath(file);
                Require((IsPart(full) || IsAssembly(full)) && !HasReparsePoint(full) &&
                    FileLinkCount(full) == 1, status, 70,
                    "working pack may contain only non-reparse single-link CAD files: " + full);
                cad.Add(full);
            }
            return cad.OrderBy(path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string InventoryDigest(IEnumerable<string> paths)
        {
            var builder = new StringBuilder();
            foreach (string path in paths.OrderBy(value => Path.GetFileName(value),
                StringComparer.OrdinalIgnoreCase))
                builder.Append(Path.GetFileName(path)).Append('|').Append(Sha256(path)).Append('\n');
            return Sha256Text(builder.ToString());
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

        private static bool IdentityRotation(double[] values)
        {
            double[] identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            return values != null && values.Length >= 9 && Enumerable.Range(0, 9)
                .All(index => Near(values[index], identity[index], TransformTolerance));
        }

        private static bool SameTransform(double[] left, double[] right, double tolerance)
        {
            return left != null && right != null && left.Length >= 13 && right.Length >= 13 &&
                Enumerable.Range(0, 13).All(index => Near(left[index], right[index], tolerance));
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
            return actual.Length == expected.Length && actual.Select(ValueText)
                .SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static void RequireExactKeys(Dictionary<string, object> value, string label,
            params string[] expected)
        {
            bool valid = value != null && value.Keys.OrderBy(key => key, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(key => key, StringComparer.Ordinal),
                    StringComparer.Ordinal);
            Require(valid, "EXACT_JSON_KEYS_MISMATCH", 71,
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
                    StringComparer.Ordinal).Select(key => JsonString(key) +
                    ":" + StableJson(dictionary[key])).ToArray()) + "}";
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

        private static bool LiveRepoRelativeManifestPath(Dictionary<string, object> tool,
            string pathKey, string hashKey, bool normalizedSource)
        {
            string relative = Text(tool, pathKey);
            string expected = Text(tool, hashKey);
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("\\") ||
                Path.IsPathRooted(relative) || relative.Split('/').Any(segment =>
                    segment == ".." || segment == ".") || !IsSha256(expected)) return false;
            string repoRoot = FindRepositoryRoot(Process.GetCurrentProcess().MainModule.FileName);
            if (string.IsNullOrWhiteSpace(repoRoot)) return false;
            string full = Path.GetFullPath(Path.Combine(repoRoot, relative.Replace('/', '\\')));
            if (!IsUnder(full, repoRoot) || !File.Exists(full) || HasReparsePoint(full) ||
                FileLinkCount(full) != 1) return false;
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
                    File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
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
                Process.GetCurrentProcess().MainModule.FileName), "NativeRootAssembly888x14.cs");
            Require(File.Exists(sourcePath), "TOOL_SOURCE_MISSING", 3,
                "reviewed root assembler source must remain beside the executable");
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
            Require(evidence != null, "EVIDENCE_COMMITMENT_OBJECT_REQUIRED", 71,
                "evidence commitment requires a JSON object");
            var projection = new Dictionary<string, object>(evidence, StringComparer.Ordinal);
            foreach (string key in new[]
            {
                "evidence_write_attempted", "evidence_write_succeeded",
                "evidence_finalize_write_succeeded", "evidence_finalize_error",
                "failure_evidence_write_succeeded", "phase_receipt_path",
                "phase_receipt_sha256", "phase_receipt_committed", "authorization_checkpoints",
                "completed_at_utc", "commit_completed_at_utc", "evidence_commitment_sha256",
                "backup_deleted", "backup_delete_error"
            }) projection.Remove(key);
            return Sha256Text(StableJson(projection));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values == null ? "" : values.FirstOrDefault(value =>
                !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private static bool IsSha256(string value)
        {
            return value != null && Regex.IsMatch(value, "^[A-Fa-f0-9]{64}$",
                RegexOptions.CultureInvariant);
        }

        private static bool IsPart(string path)
        {
            return string.Equals(Path.GetExtension(path), ".SLDPRT",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAssembly(string path)
        {
            return string.Equals(Path.GetExtension(path), ".SLDASM",
                StringComparison.OrdinalIgnoreCase);
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

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            return string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
                Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static void AssertPathChainNoReparse(string path, string stopAt, string status)
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
                Require(!string.IsNullOrWhiteSpace(parent) && !SamePath(parent, current), status, 3,
                    "path chain did not reach trusted root");
                current = parent;
            }
            Require(false, status, 3, "path chain traversal exceeded limit");
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

        private static string Argument(string[] args, string name)
        {
            for (int index = 0; index < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return index + 1 < args.Length ? args[index + 1] : null;
            return null;
        }

        private static string FullPathOrEmpty(string value)
        {
            try { return string.IsNullOrWhiteSpace(value) ? "" : Path.GetFullPath(value.Trim()); }
            catch { return ""; }
        }

        private static bool IsSafeId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 96 &&
                Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]*$",
                    RegexOptions.CultureInvariant);
        }

        private static bool ParseUtc(string value, out DateTime output)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out output);
        }

        private static string SafeException(Exception exception)
        {
            if (exception == null) return "";
            return exception.GetType().FullName + ": " + exception.Message;
        }

        private static void Require(bool condition, string status, int exitCode, string message)
        {
            if (!condition) throw new StageException(status, exitCode, message);
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); }
            catch { }
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

        private static T Safe<T>(Func<T> function, T fallback)
        {
            try { return function(); }
            catch { return fallback; }
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (value > 0 && !values.Contains(value)) values.Add(value);
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

    internal sealed class StaticCheck
    {
        public string name = "";
        public bool passed;
        public string detail = "";
    }

    internal sealed class StaticSelfTestReport
    {
        public string status = "";
        public int checksTotal;
        public int checksFailed;
        public List<StaticCheck> checks = new List<StaticCheck>();
    }

    internal sealed class RunResult
    {
        public string schema = "";
        public string generatedAtUtc = "";
        public string completedAtUtc = "";
        public string status = "";
        public bool success;
        public bool committed;
        public bool preflightPassed;
        public bool stageReceiptContractsValidated;
        public string errorCode = "";
        public string error = "";
        public int exitCode;
        public string taskId = "";
        public string attemptsRoot = "";
        public string attemptDir = "";
        public string workingPack = "";
        public string doorModuleOutput = "";
        public string contractPath = "";
        public Dictionary<string, object> contractSnapshot = new Dictionary<string, object>();
        public PlanEvidence plan = new PlanEvidence();
        public AuthorizationEvidence authorization = new AuthorizationEvidence();
        public ToolEvidence tool = new ToolEvidence();
        public ExecutionEvidence execution = new ExecutionEvidence();
        public InventoryEvidence inventory = new InventoryEvidence();
        public DoorImportEvidence doorModule = new DoorImportEvidence();
        public FrameEvidence frame = new FrameEvidence();
        public RootEvidence root = new RootEvidence();
        public ProcessEvidence processes = new ProcessEvidence();
        public TransactionEvidence transaction = new TransactionEvidence();
        public EvidenceRecord evidence = new EvidenceRecord();
        public QualityBoundaryEvidence qualityBoundary = new QualityBoundaryEvidence();
        public List<SessionEvidence> sessions = new List<SessionEvidence>();
        public List<AuthorizationCheckpoint> authorizationCheckpoints =
            new List<AuthorizationCheckpoint>();
        public List<TrustedArtifactEvidence> toolchainManifests =
            new List<TrustedArtifactEvidence>();
        public List<TrustedArtifactEvidence> stageReceipts =
            new List<TrustedArtifactEvidence>();
        public Dictionary<string, string> initialHashes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        public string rootManifestPath = "";
        public string rootManifestSha256 = "";
        [ScriptIgnore]
        public string rootReceiptPath = "";
        [ScriptIgnore]
        public string rootReceiptSha256 = "";
        public string predecessorReceiptSha256 = "";
        public string evidence_commitment_sha256 = "";
    }

    internal sealed class PlanEvidence
    {
        public string path = "";
        public string sha256 = "";
        public string workerId = "";
        public int taskRevision;
        public string taskDigest = "";
        public string requestFingerprint = "";
        public string requestDigest = "";
        public string recipeDigest = "";
        public Dictionary<string, object> raw = new Dictionary<string, object>();
    }

    internal sealed class AuthorizationEvidence
    {
        public string path = "";
        public string sha256 = "";
        public string authorizationId = "";
        public string issuedAt = "";
        public string expiresAt = "";
        public string leaseId = "";
        public string leaseExpiresAt = "";
        public string workerId = "";
        public Dictionary<string, object> raw = new Dictionary<string, object>();
    }

    internal sealed class ToolEvidence
    {
        public string id = "";
        public string sourceNormalizedSha256 = "";
        public string executableSha256 = "";
        public string contractSnapshotSha256 = "";
    }

    internal sealed class ExecutionEvidence
    {
        public bool authorized;
        public int attempt;
        public List<string> phases = new List<string>();
    }

    internal sealed class EvidenceRecord
    {
        public string schema = "";
        public string path = "";
        public string sha256 = "";
        public bool written;
        public string writeError = "";
    }

    internal sealed class QualityBoundaryEvidence
    {
        public string purpose = "";
        public bool engineeringAssistanceReady;
        public bool finalPackAndRelocatedReopenRequired;
        public bool structuralEngineerReviewRequired;
    }

    internal sealed class InventoryEvidence
    {
        public int preCount;
        public int preAssemblyCount;
        public int prePartCount;
        public string preDigest = "";
        public int importedCount;
        public int afterImportCount;
        public string afterImportDigest = "";
        public int postCount;
        public int postAssemblyCount;
        public int postPartCount;
        public string postDigest = "";
        public List<string> changedOriginalFiles = new List<string>();
        public bool changedCadAllowlistPassed;
    }

    internal sealed class DoorImportEvidence
    {
        public string manifestPath = "";
        public string manifestSha256 = "";
        public string sourceOutputDigest = "";
        public string workingPackImportStateBefore = "";
        public string workingPackImportStateAfter = "";
        public bool imported;
        public bool sourcesUnchanged;
        public int relinkedClosureCount;
        public List<DoorImportEntryEvidence> entries = new List<DoorImportEntryEvidence>();
        public List<ReferenceRelinkEvidence> relinkedReferences =
            new List<ReferenceRelinkEvidence>();
    }

    internal sealed class DoorImportEntryEvidence
    {
        public string canonicalRelativePath = "";
        public string flatTargetName = "";
        public string sourcePath = "";
        public string sourceSha256 = "";
        public long sizeBytes;
        public string category = "";
        public string targetPath = "";
        public string importedSha256 = "";
        public bool copyHashMatched;
        public string postSha256 = "";
    }

    internal sealed class ReferenceRelinkEvidence
    {
        public string parentAssembly = "";
        public string oldReference = "";
        public string flatReference = "";
        public bool replaced;
    }

    internal sealed class FrameEvidence
    {
        public string path = "";
        public int writableOpenErrors;
        public int writableOpenWarnings;
        public int saveErrors;
        public int saveWarnings;
        public int readonlyOpenErrors;
        public int readonlyOpenWarnings;
        public string sha256 = "";
        public bool freshReadonlyReopenPassed;
        public List<string> deletedInstances = new List<string>();
        public List<PlacementEvidence> placements = new List<PlacementEvidence>();
        public FrameSnapshot afterEdit;
        public FrameSnapshot afterReadonlyReopen;
    }

    internal sealed class RootEvidence
    {
        public string path = "";
        public int writableOpenErrors;
        public int writableOpenWarnings;
        public int saveErrors;
        public int saveWarnings;
        public int readonlyOpenErrors;
        public int readonlyOpenWarnings;
        public string sha256 = "";
        public bool freshReadonlyReopenPassed;
        public List<string> deletedInstances = new List<string>();
        public List<PlacementEvidence> doorPlacements = new List<PlacementEvidence>();
        public List<PlacementEvidence> shelfPlacements = new List<PlacementEvidence>();
        public RootSnapshot afterEdit;
        public RootSnapshot afterReadonlyReopen;
    }

    internal sealed class PlacementEvidence
    {
        public string role = "";
        public string sourcePath = "";
        public string componentName = "";
        public List<double> transform = new List<double>();
        public bool fixedState;
        public bool active;
    }

    internal sealed class FrameSnapshot
    {
        public bool traversalComplete;
        public int directComponentCount;
        public List<ComponentEvidence> components = new List<ComponentEvidence>();
        public List<ComponentEvidence> crossbars = new List<ComponentEvidence>();
        public FeatureHealthEvidence featureHealth = new FeatureHealthEvidence();
        public string signature = "";
    }

    internal sealed class RootSnapshot
    {
        public bool traversalComplete;
        public int topLevelComponentCount;
        public int recursiveComponentCount;
        public int activeRecursiveComponentCount;
        public int suppressedComponentCount;
        public int frameAssemblyCount;
        public int activeFrameCrossbars;
        public int closureCadFileCount;
        public int closureAssemblyFileCount;
        public int closurePartFileCount;
        public int missingFileCount;
        public int externalFileCount;
        public List<ComponentEvidence> topLevelComponents = new List<ComponentEvidence>();
        public List<ComponentEvidence> doors = new List<ComponentEvidence>();
        public List<ComponentEvidence> shelves = new List<ComponentEvidence>();
        public List<ComponentEvidence> tongues = new List<ComponentEvidence>();
        public List<string> closurePaths = new List<string>();
        public List<string> forbiddenReferenceLeaves = new List<string>();
        public FeatureHealthEvidence featureHealth = new FeatureHealthEvidence();
        public string signature = "";
    }

    internal sealed class ComponentEvidence
    {
        public string name = "";
        public string path = "";
        public string configuration = "";
        public bool active;
        public bool fixedState;
        public List<double> transform = new List<double>();
        public List<double> totalTransform = new List<double>();
    }

    internal sealed class FeatureHealthEvidence
    {
        public bool traversalComplete;
        public int featureCount;
        public List<FeatureIssueEvidence> issues = new List<FeatureIssueEvidence>();
    }

    internal sealed class FeatureIssueEvidence
    {
        public string name = "";
        public string type = "";
        public int errorCode;
        public int errorCode2;
        public bool warning;
        public int depth;
    }

    internal sealed class ProcessEvidence
    {
        public List<int> baselineSldworks = new List<int>();
        public List<int> baselineSldprocmon = new List<int>();
        public List<int> createdSldworks = new List<int>();
        public List<int> createdSldprocmon = new List<int>();
        public List<int> alreadyExited = new List<int>();
        public List<int> forceStopped = new List<int>();
        public List<int> cleanupRefusedBaseline = new List<int>();
        public List<string> cleanupErrors = new List<string>();
        public List<int> finalSldworks = new List<int>();
        public List<int> finalSldprocmon = new List<int>();
        public bool finalGate;
    }

    internal sealed class SessionEvidence
    {
        public string purpose = "";
        public bool created;
        public bool started;
        public int sldworksProcessId;
        public long sldworksStartUtcTicks;
        public string solidworksRevision = "";
        public string solidworksExecutablePath = "";
        public string solidworksExecutableSha256 = "";
        public bool solidworks2020ExactGate;
        public bool exitRequested;
        public bool processExited;
        public bool monitorsExited;
        public bool monitorIdentityGate;
        public string completedAtUtc = "";
        public List<ProcessIdentity> sldprocmonProcesses = new List<ProcessIdentity>();
    }

    internal sealed class ProcessIdentity
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

    internal sealed class TransactionEvidence
    {
        public string lockPath = "";
        public bool lockAcquired;
        public string backupDirectory = "";
        public bool backupComplete;
        public int backupFileCount;
        public bool mutationStarted;
        public List<string> importedPaths = new List<string>();
        public bool manifestCommitted;
        public bool receiptCommitted;
        public bool rollbackAttempted;
        public bool rollbackCompleted;
        public bool rollbackBlockedByProcessGate;
        public string rollbackError = "";
        public string rollbackRestoredDigest = "";
        public List<string> unpairedArtifactQuarantinePaths = new List<string>();
    }

    internal sealed class AuthorizationCheckpoint
    {
        public string name = "";
        public string checkedAtUtc = "";
        public bool valid;
    }

    internal sealed class TrustedArtifactEvidence
    {
        public string id = "";
        public string schema = "";
        public string path = "";
        public string sha256 = "";
    }

    internal sealed class RootStageManifest
    {
        public string schema = "";
        public ManifestTaskBinding task = new ManifestTaskBinding();
        public ManifestRecipeBinding recipe = new ManifestRecipeBinding();
        public ManifestPlanBinding plan = new ManifestPlanBinding();
        public ManifestToolBinding tool = new ManifestToolBinding();
        public ManifestEvidenceBinding evidence = new ManifestEvidenceBinding();
        public ManifestRootAssemblyBinding rootAssembly = new ManifestRootAssemblyBinding();
        public string generatedAtUtc = "";
    }

    internal sealed class ManifestTaskBinding
    { public string id = ""; public int revision; public string digest = ""; }
    internal sealed class ManifestRecipeBinding
    { public string id = ""; public int version; public string digest = ""; }
    internal sealed class ManifestPlanBinding
    { public string sha256 = ""; }
    internal sealed class ManifestToolBinding
    { public string id = ""; public string sourceNormalizedSha256 = "";
        public string executableSha256 = ""; }
    internal sealed class ManifestEvidenceBinding
    { public string path = ""; public string sha256 = ""; }
    internal sealed class ManifestRootAssemblyBinding
    { public string path = ""; public string sha256 = ""; }
}
