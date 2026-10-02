using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static class ConfigureWidthV37
    {
        private const string ExpectedCadDirectory =
            @"D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v37-760w-six-door-l642-r246-r1\native_cad\candidate_native_hierarchical_pack_and_go";
        private const double DimensionToleranceMm = 0.001;
        private const double TransformToleranceMm = 0.001;
        private const double BoundingBoxToleranceMm = 0.2;
        private const string BaseHolePartFileName = "底座底板.sldprt";
        private const string BaseHoleSketchName = "草图9";
        private const string BaseHoleConsumerFeatureName = "切除-拉伸5";
        private const double BaseHoleSourceAbsXMm = 295.0;
        private const double BaseHoleTargetAbsXMm = 305.0;
        private const double BaseHoleSourceSpanMm = 590.0;
        private const double BaseHoleTargetSpanMm = 610.0;
        private const double BaseHoleDepthSpanMm = 426.0;
        private static readonly string[] BaseHoleBlockNames =
        {
            "块-敲落孔φ45mm-1", "块-敲落孔φ45mm-3",
            "块-敲落孔φ45mm-4", "块-敲落孔φ45mm-5"
        };
        private const string FrozenRightPartitionSha256 =
            "C2B923131D743817E5E38594AF9C5C7AC9A255E77F702642FDE0E1917FE70EA3";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: ConfigureWidthV37.exe <cad-dir> <out-json> <dimensions|derived|base-hole|assemblies>");
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
            int exitCode = 99;

            try
            {
                EnsureParent(outJson);
                Require(string.Equals(cadDir, Path.GetFullPath(ExpectedCadDirectory), StringComparison.OrdinalIgnoreCase),
                    "REFUSED_OUTSIDE_EXACT_V37_CAD_DIRECTORY", 3,
                    "cad-dir is not the exact isolated V37 candidate directory");
                result.cad_directory_allowlist_match = true;
                Require(Directory.Exists(cadDir), "V37_CAD_DIRECTORY_MISSING", 4,
                    "isolated V37 candidate directory does not exist");
                Require(string.Equals(Path.GetExtension(outJson), ".json", StringComparison.OrdinalIgnoreCase),
                    "OUTPUT_MUST_BE_JSON", 3, "out-json must end in .json");
                Require(!IsUnder(outJson, cadDir), "REFUSED_OUTPUT_INSIDE_CAD_DIRECTORY", 3,
                    "out-json must not be written into native_cad");
                Require(phase == "dimensions" || phase == "derived" || phase == "base-hole" ||
                    phase == "assemblies", "UNSUPPORTED_PHASE", 2,
                    "phase must be dimensions, derived, base-hole, or assemblies");

                result.baseline_sldworks_process_ids.AddRange(ProcessIds("SLDWORKS"));
                result.baseline_sldprocmon_process_ids.AddRange(ProcessIds("sldProcMon"));
                Require(result.baseline_sldworks_process_ids.Count == 0 &&
                    result.baseline_sldprocmon_process_ids.Count == 0,
                    "DIRTY_SOLIDWORKS_BASELINE", 5,
                    "SLDWORKS or sldProcMon was already running; refusing non-owned-session mutation");

                List<string> phaseFiles = PhaseFiles(cadDir, phase);
                Require(phaseFiles.Count > 0, "EMPTY_PHASE_PLAN", 6, "phase plan contains no files");
                if (phase == "dimensions")
                    Require(phaseFiles.Count == 5, "DIMENSION_PHASE_FILE_COUNT_DRIFT", 6,
                        "dimensions phase must contain exactly five files");
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
                AssertPackInventoryAndDependencies(editSw, cadDir, result);

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

                result.functional_success = true;
                result.success = true;
                bool assemblyNeedsRegen = phase == "assemblies" &&
                    result.assembly_reopen_needs_regen_files.Count > 0;
                result.status = assemblyNeedsRegen ?
                    "ASSEMBLIES_PHASE_TRANSACTION_PASS_WITH_EXACT_NEEDS_REGEN" :
                    phase.ToUpperInvariant() + "_PHASE_TRANSACTION_PASS";
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
                result.status = "CONFIGURE_WIDTH_V37_EXCEPTION";
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

                if (!result.success) RollBack(result);
                else result.original_restored_on_failure = true;

                result.completed_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                try
                {
                    result.evidence_write_attempted = true;
                    result.evidence_write_succeeded = true;
                    WriteJsonAtomic(outJson, result);
                    DeleteBackupDirectory(result);
                    try
                    {
                        WriteJsonAtomic(outJson, result);
                        result.evidence_finalize_write_succeeded = true;
                    }
                    catch (Exception finalizeError)
                    {
                        result.evidence_finalize_write_succeeded = false;
                        result.evidence_finalize_error = SafeExceptionText(finalizeError);
                        Console.Error.WriteLine(result.evidence_finalize_error);
                    }
                    Console.WriteLine(outJson);
                }
                catch (Exception jsonError)
                {
                    result.evidence_write_succeeded = false;
                    result.evidence_finalize_error = SafeExceptionText(jsonError);
                    Console.Error.WriteLine(result.evidence_finalize_error);
                    if (result.success)
                    {
                        result.success = false;
                        result.functional_success = false;
                        result.status = "EVIDENCE_WRITE_FAILED_ROLLED_BACK";
                        result.error = result.evidence_finalize_error;
                        RollBack(result);
                    }
                    DeleteBackupDirectory(result);
                    try { WriteJsonAtomic(outJson, result); } catch { }
                    exitCode = 91;
                }
            }

            return exitCode;
        }

        private static Result NewResult(string cadDir, string outJson, string phase)
        {
            var result = new Result
            {
                schema = "winnsen.locker16029.configure_width_v37.v1",
                generated_at_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                cad_directory = cadDir,
                out_json = outJson,
                phase = phase,
                target_width_mm = 760.0,
                source_width_mm = 740.0,
                transaction_mode = "phase_scoped_backup_mutate_close_new_readonly_reopen_verify_or_rollback"
            };
            result.external_geometry_pending.AddRange(new[]
            {
                "储物柜门板2╱12_右.SLDPRT: BaseBody; regenerate/mirror from the updated left 317 mm panel",
                "储物柜门板4╱12_右.SLDPRT: BaseBody; regenerate/mirror from the updated left 317 mm panel",
                "储物柜门板6╱12_右.SLDPRT: BaseBody; regenerate/mirror from the updated left 317 mm panel",
                "插销固定板2╱12_左.SLDPRT: BaseBody external geometry pending",
                "插销固定板2╱12_右.SLDPRT: BaseBody external geometry pending",
                "插销固定板4╱12_左.SLDPRT: BaseBody external geometry pending",
                "插销固定板4╱12_右.SLDPRT: BaseBody external geometry pending",
                "插销固定板6╱12_左.SLDPRT: BaseBody external geometry pending",
                "插销固定板6╱12_右.SLDPRT: BaseBody external geometry pending"
            });
            result.derived_exclusions.AddRange(new[]
            {
                "箱体竖隔板L.sldprt: central topology unchanged",
                "箱体竖隔板R.SLDPRT: frozen seven-broken-reference part handled by FreezeRightPartitionV37",
                "门框 竖隔板L.sldprt: central frame-divider geometry unchanged",
                "门框 竖隔板R.SLDPRT: central frame-divider geometry unchanged",
                "箱体竖隔板加强件.sldprt: central reinforcement geometry unchanged",
                "箱体侧板加强筋2.sldprt: audited width-invariant; excluded from derived writes",
                "all door-panel in-context references: excluded from derived refresh"
            });
            return result;
        }

        private static List<string> PhaseFiles(string cadDir, string phase)
        {
            if (phase == "dimensions")
                return PhaseAllowlist(cadDir, phase);
            return CadInventory(cadDir);
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
            return Directory.GetFiles(cadDir, "*", SearchOption.TopDirectoryOnly)
                .Where(delegate(string path)
                {
                    string extension = Path.GetExtension(path);
                    return string.Equals(extension, ".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".SLDASM", StringComparison.OrdinalIgnoreCase);
                })
                .Select(Path.GetFullPath)
                .OrderBy(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AssertPackInventoryAndDependencies(ISldWorks sw, string cadDir, Result result)
        {
            List<string> inventory = CadInventory(cadDir);
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

            string rootPath = ExactFile(cadDir, "标准寄存柜1917×1000×550(总装配).SLDASM");
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
            Require(result.root_dependency_pack_total_count == 75 && result.root_dependencies_all_local,
                "ROOT_DEPENDENCY_LOCAL_INVENTORY_GATE_FAILED", 14,
                "root dependency closure must resolve to the same 75 local CAD files");
        }

        private static void CreateBackups(Result result, List<string> paths)
        {
            string parent = Path.GetDirectoryName(result.out_json) ?? Path.GetTempPath();
            result.backup_directory = Path.Combine(parent,
                ".ConfigureWidthV37-" + result.phase + "-" + Guid.NewGuid().ToString("N"));
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
                    if (anyChanged) SaveModel(model, record);
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
                sketch_name = BaseHoleSketchName,
                consumer_feature_name = BaseHoleConsumerFeatureName,
                source_abs_x_mm = BaseHoleSourceAbsXMm,
                target_abs_x_mm = BaseHoleTargetAbsXMm,
                source_span_mm = BaseHoleSourceSpanMm,
                target_span_mm = BaseHoleTargetSpanMm,
                expected_d2_mm = BaseHoleDepthSpanMm
            };
            result.base_hole = record;
            ModelDoc2 model = null;
            try
            {
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
                record.links_before = CaptureLinkSnapshot(model, sourcePlan, cadDir);
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
                record.links_after = CaptureLinkSnapshot(model, sourcePlan, cadDir);
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
                    "base bottom plate did not rebuild at four blocks X=±305 with D1=610 and preserved topology");
                Require(BaseHoleRelationsEqual(record.relations_before, record.relations_after),
                    "BASE_HOLE_RELATION_TOPOLOGY_CHANGED", 34,
                    "草图9 relation topology changed while moving the four block instances");
                Release(afterSketch);
                Release(afterSketchFeature);

                if (record.changed)
                {
                    SaveModel(model, record);
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
            }
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
            ModelDoc2 model = null;
            try
            {
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
                record.links_reopen = CaptureLinkSnapshot(model, sourcePlan, cadDir);
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
                    "base bottom plate did not persist four blocks X=±305/D1=610 with preserved native topology");
                Release(sketch);
                Release(sketchFeature);
            }
            finally
            {
                CloseDocument(sw, ref model);
            }
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
                string path = ExactFile(cadDir, plan.file_name);
                string sourcePath = ExactFile(cadDir, plan.source_file_name);
                var record = new DerivedPartRecord
                {
                    file_name = plan.file_name,
                    path = path,
                    source_file_name = plan.source_file_name,
                    expected_source_path = sourcePath,
                    link_type = plan.link_type,
                    relock_required = plan.relock_required
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
                        warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen;
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
                        plan.file_name + " X bbox is neither the audited 740W source nor 760W target");
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
                        record.links_after_unlock = CaptureLinkSnapshot(model, plan, cadDir);
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
                    record.links_after_update_none = CaptureLinkSnapshot(model, plan, cadDir);
                    record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);
                    Require(record.rebuilt, "DERIVED_REBUILD_FAILED", 34,
                        "ForceRebuild3 failed for " + plan.file_name);

                    if (plan.relock_required)
                    {
                        record.relock_attempted = true;
                        record.relock_updates = UpdatePrimaryLinksDetailed(model, plan,
                            (int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll,
                            "lock_all");
                        record.relock_succeeded = record.relock_updates.success;
                        Require(record.relock_succeeded, "DOOR_FRAME_BOTTOM_RELOCK_FAILED", 35,
                            "门框 下 SplitBody could not be relocked");
                        record.links_after_relock = CaptureLinkSnapshot(model, plan, cadDir);
                        Require(record.links_after_relock.all_primary_locked,
                            "DOOR_FRAME_BOTTOM_RELOCK_STATUS_GATE_FAILED", 35,
                            "门框 下 did not return to status Locked(1)");
                        record.rebuilt_after_relock = Safe(delegate { return model.ForceRebuild3(false); }, false);
                        Require(record.rebuilt_after_relock, "DOOR_FRAME_BOTTOM_RELOCK_REBUILD_FAILED", 35,
                            "门框 下 failed rebuild after relock");
                    }

                    record.after = CapturePartSnapshot(model);
                    record.links_after = CaptureLinkSnapshot(model, plan, cadDir);
                    record.bbox_after_target = DerivedBboxTargetGate(record.after, plan, record.before);
                    Require(DerivedPartHealthGate(record.after, record.before) &&
                        LinkGate(record.links_after, plan, true) &&
                        record.bbox_after_target,
                        "DERIVED_POST_REBUILD_GATE_FAILED", 36,
                        plan.file_name + " failed exact-source/topology-preserved/error0/target-bbox gate");
                    SaveModel(model, record);
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
                    record.links_reopen = CaptureLinkSnapshot(model, plan, cadDir);
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

        private static void RunAssembliesEditIsolated(string cadDir, Result result,
            string editPass)
        {
            foreach (AssemblyPlan plan in AssemblyPlans(cadDir))
            {
                ISldWorks sw = null;
                var session = new SessionRecord
                {
                    purpose = "assembly_edit:" + editPass + ":" + plan.file_name
                };
                result.sessions.Add(session);
                try
                {
                    sw = StartOwnedSession(result, session);
                    Require(sw != null && session.created, "ASSEMBLY_EDIT_SESSION_UNAVAILABLE", 50,
                        "could not create an owned SolidWorks session for " + plan.file_name);
                    Require(OwnedSessionIsExclusive(session), "ASSEMBLY_EDIT_SESSION_NOT_EXCLUSIVE", 50,
                        "assembly edit session was not the only active SolidWorks process for " + plan.file_name);
                    RunAssemblyEditPlan(sw, cadDir, result, plan, editPass);
                }
                finally
                {
                    if (sw != null) CloseOwnedSession(ref sw, result, session);
                }
                Require(session.process_exited, "ASSEMBLY_EDIT_SESSION_DID_NOT_EXIT", 50,
                    "assembly edit SolidWorks process did not exit cleanly for " + plan.file_name);
            }
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
                    if (saveNeeded) SaveModel(model, record);
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
            Require(plans.Count == 20, "ASSEMBLY_VERIFY_PLAN_COUNT_DRIFT", 56,
                "assembly verification requires exactly twenty plans");
            Require(result.assemblies.Count == 40 &&
                result.assemblies.Count == plans.Count * 2,
                "ASSEMBLY_PASS_RECORD_COUNT_DRIFT", 56,
                "assembly verification requires exactly twenty primary and twenty stabilization records");
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

            for (int index = 0; index < plans.Count; index++)
            {
                AssemblyPlan plan = plans[index];
                AssemblyRecord record = result.assemblies[plans.Count + index];
                ISldWorks sw = null;
                var session = new SessionRecord
                {
                    purpose = "assembly_verify:read_only_reopen:" + plan.file_name
                };
                result.sessions.Add(session);
                try
                {
                    sw = StartOwnedSession(result, session);
                    Require(sw != null && session.created, "ASSEMBLY_VERIFY_SESSION_UNAVAILABLE", 56,
                        "could not create an owned read-only verification session for " + plan.file_name);
                    Require(OwnedSessionIsExclusive(session), "ASSEMBLY_VERIFY_SESSION_NOT_EXCLUSIVE", 56,
                        "assembly verification session was not the only active SolidWorks process for " +
                        plan.file_name);
                    RunAssemblyVerifyPlan(sw, cadDir, result, plan, record);
                }
                finally
                {
                    CloseOwnedSession(ref sw, result, session);
                }
                Require(session.process_exited, "ASSEMBLY_VERIFY_SESSION_DID_NOT_EXIT", 60,
                    "assembly verification SolidWorks process did not exit cleanly for " + plan.file_name);
            }
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
            row.precondition_source_or_target = Near(row.before_x_mm, target.source_x_mm, TransformToleranceMm) ||
                Near(row.before_x_mm, target.target_x_mm, TransformToleranceMm);
            if (!row.precondition_source_or_target) return row;
            row.was_fixed = Safe(delegate { return component.IsFixed(); }, false);
            row.was_suppressed = Safe(delegate { return component.IsSuppressed(); }, false);
            if (Near(row.before_x_mm, target.source_x_mm, TransformToleranceMm))
            {
                double[] data = (double[])before.Clone();
                data[9] = target.target_x_mm / 1000.0;
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
            row.target_readback = after != null && Near(row.after_x_mm, target.target_x_mm, TransformToleranceMm);
            row.rotation_y_z_preserved = SameExceptX(before, after, 1e-10);
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
                    D("D1@草图1", 740, 760), D("D2@草图130", 590, 610),
                    D("D2@草图142", 290, 300), D("D1@草图143", 290, 300))
                    .WithReadbacks(R("D6@草图115", 317, 1), R("D8@草图76", 317, 1)),
                new DimensionPlan("储物柜门板2╱12_W307.SLDPRT", true, D("D2@草图1", 307, 317))
                    .WithReadbacks(R("D1@草图1", 298, 2)),
                new DimensionPlan("储物柜门板4╱12_W307.SLDPRT", true, D("D2@草图1", 307, 317))
                    .WithReadbacks(R("D1@草图1", 603, 2)),
                new DimensionPlan("储物柜门板6╱12_W307.SLDPRT", true, D("D2@草图1", 307, 317))
                    .WithReadbacks(R("D1@草图1", 908, 2)),
                new DimensionPlan("箱体横层板加强筋.SLDPRT", true, D("D2@草图1", 299, 309))
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
                P("底座 模型.sldprt", master, "SplitBody", -370, 370, -380, 380),
                P("底座底板.sldprt", "底座 模型.sldprt", "SplitBody", -349, 349, -359, 359),
                P("底座加强筋.sldprt", "底座 模型.sldprt", "SplitBody", -359.7, 359.7, -369.7, 369.7),
                P("底座外框.sldprt", "底座 模型.sldprt", "SplitBody", -370, 370, -380, 380),
                P("上盖 模型.sldprt", master, "SplitBody", -370, 370, -380, 380),
                P("上盖壳体底板.sldprt", "上盖 模型.sldprt", "SplitBody", -369, 369, -379, 379),
                P("上盖壳体后侧板.sldprt", "上盖 模型.sldprt", "SplitBody", -370, 370, -380, 380),
                P("上盖壳体前侧板.sldprt", "上盖 模型.sldprt", "SplitBody", -370, 370, -380, 380),
                P("上盖壳体左侧板.sldprt", "上盖 模型.sldprt", "SplitBody", -370, -310.8, -380, -320.8),
                P("上盖壳体右侧板.SLDPRT", "上盖壳体左侧板.sldprt", "MirrorStock", 310.8, 370, 320.8, 380),
                P("箱体横层板L.sldprt", master, "SplitBody", -367.7, -64.5, -377.7, -64.5),
                P("箱体横层板R.SLDPRT", "箱体横层板L.sldprt", "MirrorStock", 64.5, 367.7, 64.5, 377.7),
                P("门框 横隔板.sldprt", master, "SplitBody", -351.5, -36.5, -361.5, -36.5),
                P("门框 横隔板R.SLDPRT", "门框 横隔板.sldprt", "MirrorStock", 36.5, 351.5, 36.5, 361.5),
                P("门框 上.sldprt", master, "SplitBody", -368.6, 368.6, -378.6, 378.6),
                P("门框 左.sldprt", master, "SplitBody", -370, -350, -380, -360),
                P("门框 右.sldprt", "门框 左.sldprt", "MirrorStock", 350, 370, 360, 380),
                P("门框 下.sldprt", master, "SplitBody", -368.6, 368.6, -378.6, 378.6, true),
                P("箱体侧板加强筋1.sldprt", master, "SplitBody", -369, -364.2, -379, -374.2),
                P("箱体右侧板.sldprt", master, "SplitBody", 0.5, 370, 0.5, 380),
                P("箱体左侧板.sldprt", master, "SplitBody", -370, 14.7, -380, 14.7)
            };
        }

        private static DerivedPlan P(string file, string source, string type,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax,
            bool relock)
        {
            return new DerivedPlan { file_name = file, source_file_name = source,
                link_type = type, relock_required = relock,
                source_xmin_mm = sourceXmin, source_xmax_mm = sourceXmax,
                target_xmin_mm = targetXmin, target_xmax_mm = targetXmax };
        }

        private static DerivedPlan P(string file, string source, string type,
            double sourceXmin, double sourceXmax, double targetXmin, double targetXmax)
        {
            return P(file, source, type, sourceXmin, sourceXmax, targetXmin, targetXmax, false);
        }

        private static List<AssemblyPlan> AssemblyPlans(string cadDir)
        {
            var plans = new List<AssemblyPlan>();
            foreach (string ratio in new[] { "2", "4", "6" })
            {
                plans.Add(new AssemblyPlan("储物柜门" + ratio + "╱12焊接_左.SLDASM", false, 1,
                    T("插销固定板-1", "插销固定板.SLDPRT", -143.5, -148.5),
                    T("U型锁钩垫板-1", "U型锁钩垫板.SLDPRT", 138.5, 143.5)));
                plans.Add(new AssemblyPlan("储物柜门" + ratio + "╱12焊接_右.SLDASM", false, 1,
                    T("插销固定板-1", "插销固定板.SLDPRT", 143.5, 148.5),
                    T("U型锁钩垫板-1", "U型锁钩垫板.SLDPRT", -138.5, -143.5)));
            }
            foreach (string ratio in new[] { "2", "4", "6" })
            {
                plans.Add(OuterDoorPlan("L" + ratio, true));
                plans.Add(OuterDoorPlan("R" + ratio, false));
            }
            plans.Add(new AssemblyPlan("底座焊接.SLDASM", false,
                T("螺母M12-1", "螺母M12.SLDPRT", -335, -345),
                T("螺母M12-2", "螺母M12.SLDPRT", 335, 345),
                T("螺母M12-3", "螺母M12.SLDPRT", 335, 345),
                T("螺母M12-4", "螺母M12.SLDPRT", -335, -345)));
            plans.Add(new AssemblyPlan("箱体横层板L焊接.SLDASM", false,
                T("箱体横层板加强筋-1", "箱体横层板加强筋.SLDPRT", -216.1, -221.1),
                T("箱体横层板加强筋-2", "箱体横层板加强筋.SLDPRT", -216.1, -221.1)));
            plans.Add(new AssemblyPlan("箱体横层板R焊接.SLDASM", false,
                T("箱体横层板加强筋-1", "箱体横层板加强筋.SLDPRT", 216.1, 221.1),
                T("箱体横层板加强筋-2", "箱体横层板加强筋.SLDPRT", 216.1, 221.1)));
            plans.Add(new AssemblyPlan("箱体竖隔板R焊接.SLDASM", false,
                T("箱体侧板加强筋1-1", "箱体侧板加强筋1.sldprt", 432.2, 442.2),
                T("箱体侧板加强筋1-2", "箱体侧板加强筋1.sldprt", 432.2, 442.2)));
            plans.Add(new AssemblyPlan("箱体竖隔板L焊接.SLDASM", false,
                T("箱体侧板加强筋1-1", "箱体侧板加强筋1.sldprt", -432.2, -442.2),
                T("箱体侧板加强筋1-2", "箱体侧板加强筋1.sldprt", -432.2, -442.2)));
            plans.Add(new AssemblyPlan("箱体左侧板焊接.sldasm", false,
                T("箱体侧板加强筋1-3", "箱体侧板加强筋1.sldprt", -364.6, -369.6)));
            plans.Add(new AssemblyPlan("箱体右侧板焊接.SLDASM", false,
                T("箱体侧板加强筋1-3", "箱体侧板加强筋1.sldprt", 67.6, 72.6)));
            plans.Add(new AssemblyPlan("标准寄存柜1917×1000×550(总装配).SLDASM", true,
                T("储物柜门装配_L2-2", "储物柜门装配_L2.SLDASM", -193.5, -198.5),
                T("储物柜门装配_L4-2", "储物柜门装配_L4.SLDASM", -193.5, -198.5),
                T("储物柜门装配_L6-2", "储物柜门装配_L6.SLDASM", -193.5, -198.5),
                T("储物柜门装配_R2-2", "储物柜门装配_R2.SLDASM", 193.5, 198.5),
                T("储物柜门装配_R4-2", "储物柜门装配_R4.SLDASM", 193.5, 198.5),
                T("储物柜门装配_R6-2", "储物柜门装配_R6.SLDASM", 193.5, 198.5),
                T("调整脚 M12X60(模型)-5", "调整脚 M12X60(模型).SLDPRT", -335, -345),
                T("调整脚 M12X60(模型)-6", "调整脚 M12X60(模型).SLDPRT", 335, 345),
                T("调整脚 M12X60(模型)-7", "调整脚 M12X60(模型).SLDPRT", 335, 345),
                T("调整脚 M12X60(模型)-8", "调整脚 M12X60(模型).SLDPRT", -335, -345)));
            return plans;
        }

        private static AssemblyPlan OuterDoorPlan(string code, bool left)
        {
            double outerSource = left ? -143.5 : 143.5;
            double outerTarget = left ? -148.5 : 148.5;
            double innerSource = left ? 138.5 : -138.5;
            double innerTarget = left ? 143.5 : -143.5;
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

        private static Feature FindFeature(ModelDoc2 model, string name)
        {
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                if (string.Equals(Safe(delegate { return feature.Name; }, ""), name,
                    StringComparison.OrdinalIgnoreCase)) return feature;
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
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
            var snapshot = new PartSnapshot();
            PartDoc part = model as PartDoc;
            Array bodies = part == null ? null : Safe(delegate
            {
                return part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            }, null);
            snapshot.body_count = bodies == null ? 0 : bodies.Length;
            snapshot.part_box_m = DoubleArray(part == null ? null :
                Safe(delegate { return part.GetPartBox(true) as Array; }, null));
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CapturePartFeatureRecursive(feature, snapshot, 0);
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            return snapshot;
        }

        private static void CapturePartFeatureRecursive(Feature feature, PartSnapshot snapshot, int depth)
        {
            if (feature == null || depth > 30 || snapshot.feature_count > 20000) return;
            snapshot.feature_count++;
            string type = Safe(delegate { return feature.GetTypeName2(); }, "");
            if (string.Equals(type, "SheetMetal", StringComparison.OrdinalIgnoreCase)) snapshot.has_sheet_metal = true;
            if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase)) snapshot.has_flat_pattern = true;
            bool warning = false;
            int error2 = FeatureErrorCode2(feature, out warning);
            int error1 = Safe(delegate { return feature.GetErrorCode(); }, 0);
            bool suppressed = Safe(delegate { return feature.IsSuppressed(); }, false);
            if (!suppressed && (error1 > 0 || error2 > 0)) snapshot.error_feature_count++;
            if (!suppressed && warning) snapshot.warning_feature_count++;
            Feature child = Safe(delegate { return feature.GetFirstSubFeature() as Feature; }, null);
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CapturePartFeatureRecursive(child, snapshot, depth + 1);
                Feature next = Safe(delegate { return child.GetNextSubFeature() as Feature; }, null);
                Release(child);
                child = next;
            }
        }

        private static bool PartHealthGate(PartSnapshot snapshot, bool requireSheetMetal)
        {
            if (snapshot == null || snapshot.error_feature_count != 0 ||
                snapshot.warning_feature_count != 0 || snapshot.body_count <= 0) return false;
            if (!requireSheetMetal) return true;
            return snapshot.body_count == 1 && snapshot.has_sheet_metal && snapshot.has_flat_pattern;
        }

        private static bool DerivedPartHealthGate(PartSnapshot snapshot, PartSnapshot baseline)
        {
            if (snapshot == null || baseline == null ||
                snapshot.error_feature_count != 0 || snapshot.warning_feature_count != 0 ||
                baseline.error_feature_count != 0 || baseline.warning_feature_count != 0 ||
                snapshot.body_count <= 0) return false;
            return snapshot.body_count == baseline.body_count &&
                snapshot.feature_count == baseline.feature_count &&
                snapshot.has_sheet_metal == baseline.has_sheet_metal &&
                snapshot.has_flat_pattern == baseline.has_flat_pattern;
        }

        private static LinkSnapshot CaptureLinkSnapshot(ModelDoc2 model, DerivedPlan plan, string cadDir)
        {
            var snapshot = new LinkSnapshot { expected_source_path = ExactFile(cadDir, plan.source_file_name) };
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                string type = Safe(delegate { return feature.GetTypeName2(); }, "");
                if (string.Equals(type, plan.link_type, StringComparison.OrdinalIgnoreCase))
                {
                    PrimaryLinkFeature row = CapturePrimaryFeature(feature, type);
                    if (row.references.Count > 0) snapshot.primary_features.Add(row);
                }
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
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
            snapshot.all_paths_local = snapshot.nonlocal_paths.Count == 0;
            snapshot.all_primary_locked = snapshot.primary_feature_count > 0 && snapshot.nonlocked_count == 0;
            return snapshot;
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
            if (snapshot == null || snapshot.primary_feature_count < 1 ||
                !snapshot.has_exact_expected_source || !snapshot.all_paths_local ||
                snapshot.broken_or_dangling_count != 0) return false;
            if (afterRefresh && plan.relock_required && !snapshot.all_primary_locked) return false;
            return true;
        }

        private static string LinkSnapshotDigest(LinkSnapshot snapshot)
        {
            if (snapshot == null) return "";
            var builder = new StringBuilder();
            builder.Append(Path.GetFullPath(snapshot.expected_source_path ?? "").ToLowerInvariant())
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
            var batch = new LinkUpdateBatch { stage = stage, action = action };
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                string type = Safe(delegate { return feature.GetTypeName2(); }, "");
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
                        row.call_succeeded = TryAction(delegate
                        {
                            feature.UpdateExternalFileReferences(refs.config_option,
                                refs.config_name ?? "", action);
                        });
                        PrimaryLinkFeature after = CapturePrimaryFeature(feature, type);
                        row.statuses_after = after.references.Select(
                            delegate(ReferenceRow reference) { return reference.status; }).ToArray();
                        row.status = row.call_succeeded ? "CALL_SUCCEEDED" : "CALL_FAILED";
                        batch.rows.Add(row);
                    }
                }
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            batch.success = batch.rows.Count > 0 && batch.duplicate_feature_name_count == 0 &&
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
            var snapshot = new AssemblySnapshot { is_root = root };
            snapshot.top_level_component_count = TopLevelComponents(model).Count;
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureAssemblyFeatureRecursive(feature, snapshot, 0);
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            return snapshot;
        }

        private static void CaptureAssemblyFeatureRecursive(Feature feature, AssemblySnapshot snapshot, int depth)
        {
            if (feature == null || depth > 30 || snapshot.feature_count > 20000) return;
            snapshot.feature_count++;
            bool warning = false;
            int error2 = FeatureErrorCode2(feature, out warning);
            int error1 = Safe(delegate { return feature.GetErrorCode(); }, 0);
            bool suppressed = Safe(delegate { return feature.IsSuppressed(); }, false);
            if (!suppressed && (error1 > 0 || error2 > 0 || warning))
                snapshot.issues.Add(new FeatureIssue
                {
                    name = Safe(delegate { return feature.Name; }, ""),
                    type = Safe(delegate { return feature.GetTypeName2(); }, ""),
                    error_code = error1,
                    error_code2 = error2,
                    warning = warning,
                    depth = depth
                });
            Feature child = Safe(delegate { return feature.GetFirstSubFeature() as Feature; }, null);
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureAssemblyFeatureRecursive(child, snapshot, depth + 1);
                Feature next = Safe(delegate { return child.GetNextSubFeature() as Feature; }, null);
                Release(child);
                child = next;
            }
        }

        private static bool AssemblyHealthGate(AssemblySnapshot snapshot, bool root)
        {
            if (snapshot == null || snapshot.top_level_component_count <= 0) return false;
            if (!root) return snapshot.issues.Count == 0;
            if (snapshot.issues.Count != 1) return false;
            FeatureIssue issue = snapshot.issues[0];
            return string.Equals(issue.name, "箱体右侧板焊接-1", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(issue.type, "Reference", StringComparison.OrdinalIgnoreCase) &&
                (issue.error_code == 51 || issue.error_code2 == 51) && issue.warning;
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
                warnings == (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen);
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
                bool fixedState = matches.Count == 1 &&
                    Safe(delegate { return matches[0].IsFixed(); }, false);
                bool suppressedState = matches.Count == 1 &&
                    Safe(delegate { return matches[0].IsSuppressed(); }, false);
                bool target = matches.Count == 1 && values != null &&
                    Near(xMm, row.target_x_mm, TransformToleranceMm);
                bool nonXPreserved = matches.Count == 1 &&
                    SameExceptX(row.before_transform, values, 1e-10);
                bool statesPreserved = matches.Count == 1 &&
                    fixedState == row.was_fixed && suppressedState == row.was_suppressed;
                bool pass = target && nonXPreserved && statesPreserved;
                if (reopen)
                {
                    row.reopen_match_count = matches.Count;
                    row.reopen_transform = values;
                    row.reopen_x_mm = xMm;
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

        private static void SaveModel(ModelDoc2 model, SaveRecord record)
        {
            int errors = 0, warnings = 0;
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
            session.created = session.sldworks_process_id > 0 &&
                session.sldworks_start_utc_ticks > 0 &&
                !result.baseline_sldworks_process_ids.Contains(session.sldworks_process_id);
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
            return running.Count == 1 && running[0] == session.sldworks_process_id;
        }

        private static ISldWorks CreateOwned()
        {
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
                try
                {
                    Type type = Type.GetTypeFromProgID(progId);
                    ISldWorks app = type == null ? null : Activator.CreateInstance(type) as ISldWorks;
                    if (app != null) return app;
                }
                catch { }
            }
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
            foreach (int pid in ProcessIds("sldProcMon"))
                if (!before.Contains(pid) && !assigned.Contains(pid) &&
                    !result.baseline_sldprocmon_process_ids.Contains(pid))
                {
                    session.sldprocmon_process_ids.Add(pid);
                    assigned.Add(pid);
                    if (!result.created_sldprocmon_process_ids.Contains(pid))
                    result.created_sldprocmon_process_ids.Add(pid);
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
            foreach (int pid in session.sldprocmon_process_ids.Distinct().ToList())
            {
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
                        if (!string.Equals(process.ProcessName, "sldProcMon",
                            StringComparison.OrdinalIgnoreCase))
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
                session.sldworks_start_utc_ticks))
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

        private static bool ExactProcessIdentityExists(int pid, string expectedName,
            long expectedStartUtcTicks)
        {
            if (pid <= 0 || expectedStartUtcTicks <= 0) return false;
            return ProcessStartUtcTicks(pid, expectedName) == expectedStartUtcTicks;
        }

        private static bool WaitForExactProcessExit(SessionRecord session, int timeoutMs)
        {
            if (session == null || session.sldworks_process_id <= 0) return true;
            Stopwatch watch = Stopwatch.StartNew();
            int consecutiveAbsentSamples = 0;
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                if (!ExactProcessIdentityExists(session.sldworks_process_id, "SLDWORKS",
                    session.sldworks_start_utc_ticks)) consecutiveAbsentSamples++;
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
            foreach (FileRecord file in result.files)
            {
                try
                {
                    if (!File.Exists(file.backup_path)) { all = false; continue; }
                    File.Copy(file.backup_path, file.path, true);
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

        private static void WriteJsonAtomic(string path, Result result)
        {
            EnsureParent(path);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = new UTF8Encoding(false).GetBytes(
                serializer.Serialize(result) + System.Environment.NewLine);
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
        }

        private sealed class ComponentDigest
        {
            public int count;
            public string sha256 = "";
            public readonly List<ComponentState> states = new List<ComponentState>();
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
            public string cad_directory = "";
            public string out_json = "";
            public string phase = "";
            public double source_width_mm;
            public double target_width_mm;
            public string transaction_mode = "";
            public bool cad_directory_allowlist_match;
            public bool functional_success;
            public bool success;
            public bool release_ready = false;
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
            public bool rollback_attempted;
            public bool rollback_succeeded;
            public bool original_restored_on_failure;
            public bool evidence_write_attempted;
            public bool evidence_write_succeeded;
            public bool evidence_finalize_write_succeeded;
            public string evidence_finalize_error = "";
            public int pack_inventory_count;
            public bool pack_inventory_all_local;
            public string frozen_right_partition_path = "";
            public string frozen_right_partition_sha256 = "";
            public bool frozen_right_partition_sha_match;
            public int root_dependency_raw_count;
            public int root_dependency_pack_total_count;
            public bool root_dependencies_all_local;
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

        public sealed class SessionRecord
        {
            public string purpose = "";
            public bool started;
            public int sldworks_process_id;
            public long sldworks_start_utc_ticks;
            public readonly List<int> sldprocmon_process_ids = new List<int>();
            public bool created;
            public bool exit_requested;
            public bool process_exited;
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

        public sealed class DerivedPartRecord : SaveRecord
        {
            public string file_name = "";
            public string path = "";
            public string source_file_name = "";
            public string expected_source_path = "";
            public string link_type = "";
            public bool relock_required;
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
            public string expected_source_path = "";
            public int primary_feature_count;
            public bool has_exact_expected_source;
            public bool all_paths_local;
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
            public bool rotation_y_z_preserved;
            public bool is_fixed_after;
            public bool is_suppressed_after;
            public bool fixed_state_preserved;
            public bool suppressed_state_preserved;
            public int after_rebuild_match_count;
            public double[] after_rebuild_transform;
            public double after_rebuild_x_mm;
            public bool after_rebuild_is_fixed;
            public bool after_rebuild_is_suppressed;
            public bool after_rebuild_target_readback;
            public bool after_rebuild_rotation_y_z_preserved;
            public bool after_rebuild_fixed_suppressed_preserved;
            public bool after_rebuild_gate;
            public int reopen_match_count;
            public double[] reopen_transform;
            public double reopen_x_mm;
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
