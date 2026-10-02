import { createHash } from 'node:crypto'

export const WIDTH_SOURCE_PATH = 'workers/native_model_requests/parametric_v1/sources/ConfigureWidthV37.cs'

function replaceOnce(source, before, after) {
  if (source.split(before).length !== 2) throw new Error(`width native anchor changed: ${before}`)
  return source.replace(before, after)
}

export function adaptWidthSource(source) {
  const digest = createHash('sha256').update(source).digest('hex').toUpperCase()
  if (digest !== 'C62D8E31BF027146E8ACA42FEC71CA2F48AFE56CCC0AB3370000D469807DB4C6') throw new Error('reviewed native width source hash mismatch')
  let text = source.replace(/\r\n?/g, '\n')
  text = replaceOnce(text, 'if (!suppressed && (error1 > 0 || error2 > 0)) snapshot.error_feature_count++;', 'if (!suppressed && (error1 > 0 || error2 > 0)) { snapshot.error_feature_count++; Console.WriteLine("NATIVE_FEATURE_ERROR " + feature.Name + " type=" + type + " code=" + error1 + "/" + error2); }')
  text = replaceOnce(text, 'public bool rebuilt;\n            public bool relock_attempted;', 'public bool rebuilt;\n            public object rebuild_failure_diagnostics;\n            public bool relock_attempted;')
  text = replaceOnce(text, 'record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);\n                    Require(record.rebuilt, "DERIVED_REBUILD_FAILED", 34,', 'record.rebuilt = Safe(delegate { return model.ForceRebuild3(false); }, false);\n                    if (!record.rebuilt)\n                    {\n                        record.after = CapturePartSnapshot(model);\n                        record.rebuild_failure_diagnostics = CaptureDerivedRebuildFailure(model);\n                    }\n                    Require(record.rebuilt, "DERIVED_REBUILD_FAILED", 34,')
  text = replaceOnce(text, 'string extension = Path.GetExtension(path);\n                    return', 'if (Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal)) return false;\n                    string extension = Path.GetExtension(path);\n                    return')
  text = replaceOnce(text, 'internal static class ConfigureWidthV37', 'internal static partial class NativeParametricWidth')
  text = replaceOnce(text, 'record.links_after_update_none = CaptureLinkSnapshot(model, plan, cadDir);', 'record.links_after_update_none = CaptureLinkSnapshot(model, plan, cadDir);\n                    if (plan.file_name.Equals("底座加强筋.sldprt", StringComparison.OrdinalIgnoreCase))\n                        NativeParametricAssembly.PrepareBaseFootSketch(sw, model, new List<object>());')
  text = replaceOnce(text, 'Require(record.rebuilt && PartHealthGate(record.after, plan.require_sheet_metal),', 'if (record.after.error_feature_count > 0) DescribeMasterCut(model);\n                    Require(record.rebuilt && PartHealthGate(record.after, plan.require_sheet_metal),')
  text = text.replace(/private const string ExpectedCadDirectory =\n\s*@"[^"]+";/, 'private static string ExpectedCadDirectory { get { return ParametricContext.CadDirectory; } }')
  text = replaceOnce(text, 'private const double BaseHoleTargetAbsXMm = 305.0;', 'private static double BaseHoleTargetAbsXMm { get { return (ParametricContext.WidthMm - 150) / 2; } }')
  text = replaceOnce(text, 'private const double BaseHoleTargetSpanMm = 610.0;', 'private static double BaseHoleTargetSpanMm { get { return ParametricContext.WidthMm - 150; } }')
  text = text.replace(/private const string FrozenRightPartitionSha256 =\n\s*"[A-F0-9]+";/, 'private static string FrozenRightPartitionSha256 { get { return ParametricContext.SourceRightPartitionSha256; } }\n        private static double WidthScale { get { return (ParametricContext.WidthMm - 740.0) / 20.0; } }')
  text = replaceOnce(text, 'EnsureParent(outJson);', 'ParametricContext.AssertAuthorized(phase);\n                EnsureParent(outJson);')
  text = replaceOnce(text, 'private static void EnsureParent(string path)\n        {', 'private static void EnsureParent(string path)\n        {\n            ParametricContext.AssertMutation(path);')
  text = replaceOnce(text, 'private static void SaveModel(ModelDoc2 model, SaveRecord record)\n        {', 'private static void SaveModel(ModelDoc2 model, SaveRecord record)\n        {\n            ParametricContext.AssertMutation(model.GetPathName());')
  text = replaceOnce(text, 'Directory.CreateDirectory(result.backup_directory);', 'ParametricContext.AssertMutation(result.backup_directory);\n            Directory.CreateDirectory(result.backup_directory);')
  text = replaceOnce(text, 'File.Copy(path, backup, false);', 'ParametricContext.CheckPath(path, ParametricContext.CadDirectory);\n                ParametricContext.AssertMutation(backup);\n                File.Copy(path, backup, false);')
  text = replaceOnce(text, 'File.Copy(file.backup_path, file.path, true);', 'ParametricContext.CheckPath(file.backup_path, ParametricContext.AttemptDirectory);\n                    ParametricContext.CheckPath(file.path, ParametricContext.CadDirectory);\n                    File.Copy(file.backup_path, file.path, true);')
  text = replaceOnce(text, 'target_width_mm = 760.0,', 'target_width_mm = ParametricContext.WidthMm,')
  text = replaceOnce(text, 'D("D2@草图142", 290, 300), D("D1@草图143", 290, 300))', 'D("D2@草图142", 290, 300), D("D1@草图143", 290, 300), D("D3@草图137", 350, 350))')
  text = replaceOnce(text, 'source_mm = source,\n                target_mm = target,', 'source_mm = source,\n                target_mm = name == "D3@草图137" ? (ParametricContext.WidthMm - 300.0) / 2.0 : source + (target - source) * WidthScale,')
  text = replaceOnce(text, 'name = name,\n                target_mm = target,', 'name = name,\n                target_mm = target == 317 ? ParametricContext.DoorWidthMm : target,')
  text = replaceOnce(text, 'target_xmin_mm = targetXmin, target_xmax_mm = targetXmax', 'target_xmin_mm = sourceXmin + (targetXmin - sourceXmin) * WidthScale, target_xmax_mm = sourceXmax + (targetXmax - sourceXmax) * WidthScale')
  text = replaceOnce(text, 'source_x_mm = source, target_x_mm = target', 'source_x_mm = source, target_x_mm = source + (target - source) * WidthScale')
  text = replaceOnce(text, 'if (Near(row.before_x_mm, target.source_x_mm, TransformToleranceMm))', 'if (Near(row.before_x_mm, target.source_x_mm, TransformToleranceMm) &&\n                !Near(row.before_x_mm, target.target_x_mm, TransformToleranceMm))')
  text = replaceOnce(text, 'return plans;\n        }\n\n        private static AssemblyPlan OuterDoorPlan', 'return plans.Where(plan => !plan.file_name.StartsWith("储物柜门", StringComparison.Ordinal)).ToList();\n        }\n\n        private static AssemblyPlan OuterDoorPlan')
  text = replaceOnce(text, 'Require(plans.Count == 20, "ASSEMBLY_VERIFY_PLAN_COUNT_DRIFT",', 'Require(plans.Count == 8, "ASSEMBLY_VERIFY_PLAN_COUNT_DRIFT",')
  text = replaceOnce(text, '"assembly verification requires exactly twenty plans"', '"cabinet verification requires eight plans; obsolete door assemblies are replaced by native door modules"')
  text = replaceOnce(text, 'Require(result.assemblies.Count == 40 &&', 'Require(result.assemblies.Count == 16 &&')
  text = replaceOnce(text, '"assembly verification requires exactly twenty primary and twenty stabilization records"', '"cabinet verification requires eight primary and eight stabilization records"')
  text = replaceOnce(text, 'result.cleanup_errors.Add("monitor PID " +\n                        pid.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);', 'if (!ProcessIds("sldProcMon").Contains(pid))\n                        AddUnique(result.cleanup_already_exited_process_ids, pid);\n                    else result.cleanup_errors.Add("monitor PID " +\n                        pid.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);')
  return text
}
