using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class RepairVerifiedV43DoorPanelFeature
    {
        private const string TargetPartFileName = "\u50A8\u7269\u67DC\u95E8\u677F2\u257112_W307.SLDPRT";
        private const string TargetFeatureName = "\u5207\u9664-\u62C9\u4F386";
        private const double GeometryToleranceMm = 0.01;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: RepairVerifiedV43DoorPanelFeature.exe <generated-part-copy.SLDPRT> <generated-root> <out-json>");
                return 2;
            }

            string partPath = Path.GetFullPath(args[0]);
            string allowedRoot = Path.GetFullPath(args[1]);
            string outJson = Path.GetFullPath(args[2]);
            var result = new RepairResult
            {
                PartPath = partPath,
                AllowedRoot = allowedRoot,
                TargetPartFileName = TargetPartFileName,
                TargetFeatureName = TargetFeatureName,
                Exists = File.Exists(partPath),
                AllowedRootExists = Directory.Exists(allowedRoot),
                PartNameMatched = string.Equals(Path.GetFileName(partPath), TargetPartFileName, StringComparison.Ordinal),
                PathWithinAllowedRoot = IsWithinRoot(partPath, allowedRoot),
            };

            ISldWorks sw = null;
            ModelDoc2 model = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));
                if (!result.Exists || !result.AllowedRootExists || !result.PartNameMatched || !result.PathWithinAllowedRoot)
                {
                    result.Error = "refusing to modify a file that is not the known verified-v43 generated door-panel copy";
                    WriteJson(outJson, result);
                    return 3;
                }

                sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 4;
                }

                sw.Visible = true;
                result.SolidWorksProcessId = TryValue(() => sw.GetProcessID(), 0);
                int errors = 0;
                int warnings = 0;
                model = sw.OpenDoc6(
                    partPath,
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2;
                if (model == null)
                {
                    model = TryValue(() => sw.OpenDoc(partPath, (int)swDocumentTypes_e.swDocPART) as ModelDoc2, null);
                }

                result.Opened = model != null;
                result.OpenErrors = errors;
                result.OpenWarnings = warnings;
                if (model == null)
                {
                    result.Error = "failed to open generated part copy";
                    WriteJson(outJson, result);
                    return 5;
                }

                Try(() => model.ShowFeatureErrorDialog = false);
                PartDoc part = model as PartDoc;
                if (part == null)
                {
                    result.Error = "opened document is not a part";
                    WriteJson(outJson, result);
                    return 6;
                }

                result.Before = CaptureGeometry(part);
                result.InitialRebuilt = TryValue(() => model.ForceRebuild3(false), false);

                Feature target = FindFeature(model, TargetFeatureName);
                result.FeatureFound = target != null;
                if (target == null)
                {
                    result.Error = "known failed feature was not found";
                    WriteJson(outJson, result);
                    return 7;
                }

                result.FeatureType = TryValue(() => target.GetTypeName2(), "");
                result.FeatureWasSuppressed = TryValue(() => target.IsSuppressed(), false);
                result.FeatureErrorCode = TryValue(() => target.GetErrorCode(), -1);
                bool isWarning = false;
                result.FeatureErrorCode2 = GetErrorCode2(target, out isWarning);
                result.FeatureWarning = isWarning;

                if (result.FeatureWasSuppressed)
                {
                    result.SuppressionSucceeded = true;
                }
                else
                {
                    if (result.InitialRebuilt)
                    {
                        result.Error = "target feature is not causing a rebuild failure; refusing to suppress it";
                        WriteJson(outJson, result);
                        return 8;
                    }
                    if (result.FeatureErrorCode <= 0 && result.FeatureErrorCode2 <= 0)
                    {
                        result.Error = "target feature does not report an error; refusing to suppress it";
                        WriteJson(outJson, result);
                        return 9;
                    }

                    result.SuppressionInvoked = true;
                    result.SuppressionSucceeded = TryValue(
                        () => target.SetSuppression2(
                            (int)swFeatureSuppressionAction_e.swSuppressFeature,
                            (int)swInConfigurationOpts_e.swThisConfiguration,
                            null),
                        false);
                    if (!result.SuppressionSucceeded)
                    {
                        Try(() => model.ClearSelection2(true));
                        bool selected = TryValue(() => target.Select2(false, 0), false);
                        result.SuppressionSucceeded = selected && TryValue(() => model.EditSuppress2(), false);
                    }
                    result.SuppressionSucceeded = result.SuppressionSucceeded && TryValue(() => target.IsSuppressed(), false);
                }

                result.FinalRebuilt = result.SuppressionSucceeded && TryValue(() => model.ForceRebuild3(false), false);
                result.After = CaptureGeometry(part);
                result.GeometryMaxDeltaMm = MaxDelta(result.Before, result.After);
                result.GeometryUnchanged =
                    result.Before != null &&
                    result.After != null &&
                    result.Before.BodyCount == result.After.BodyCount &&
                    result.GeometryMaxDeltaMm <= GeometryToleranceMm;

                if (!result.FinalRebuilt)
                {
                    result.Error = "part still fails to rebuild after suppressing the known failed feature";
                    WriteJson(outJson, result);
                    return 10;
                }
                if (!result.GeometryUnchanged)
                {
                    result.Error = "body count or bounding box changed; refusing to save the generated copy";
                    WriteJson(outJson, result);
                    return 11;
                }

                int saveErrors = 0;
                int saveWarnings = 0;
                result.Saved = TryValue(
                    () => model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings),
                    false);
                result.SaveErrors = saveErrors;
                result.SaveWarnings = saveWarnings;
                result.Success = result.Saved;
                result.Status = result.Success ? "known_failed_feature_suppressed_geometry_unchanged" : "save_failed";
                if (!result.Success)
                {
                    result.Error = "failed to save repaired generated part copy";
                }

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Success ? 0 : 12;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 1;
            }
            finally
            {
                if (sw != null && model != null)
                {
                    string title = TryValue(() => model.GetTitle(), "");
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        Try(() => sw.CloseDoc(title));
                    }
                }
            }
        }

        private static Feature FindFeature(ModelDoc2 model, string featureName)
        {
            Feature feature = TryValue(() => model.FirstFeature() as Feature, null);
            int guard = 0;
            while (feature != null && guard < 10000)
            {
                guard++;
                string name = TryValue(() => feature.Name, "");
                if (string.Equals(name, featureName, StringComparison.Ordinal))
                {
                    return feature;
                }
                feature = TryValue(() => feature.GetNextFeature() as Feature, null);
            }
            return null;
        }

        private static int GetErrorCode2(Feature feature, out bool isWarning)
        {
            isWarning = false;
            try
            {
                return feature.GetErrorCode2(out isWarning);
            }
            catch
            {
                return -1;
            }
        }

        private static GeometryInfo CaptureGeometry(PartDoc part)
        {
            object raw = TryValue(() => part.GetBodies2((int)swBodyType_e.swSolidBody, false), null);
            Array bodies = raw as Array;
            var boxes = new List<double[]>();
            if (bodies != null)
            {
                foreach (object item in bodies)
                {
                    Body2 body = item as Body2;
                    if (body == null) continue;
                    object rawBox = TryValue(() => body.GetBodyBox(), null);
                    double[] box = rawBox as double[];
                    if (box != null && box.Length >= 6)
                    {
                        boxes.Add(box);
                    }
                }
            }

            if (boxes.Count == 0)
            {
                return new GeometryInfo { BodyCount = 0 };
            }

            var result = new GeometryInfo
            {
                BodyCount = boxes.Count,
                XMinMm = Mm(boxes[0][0]),
                YMinMm = Mm(boxes[0][1]),
                ZMinMm = Mm(boxes[0][2]),
                XMaxMm = Mm(boxes[0][3]),
                YMaxMm = Mm(boxes[0][4]),
                ZMaxMm = Mm(boxes[0][5]),
            };
            for (int i = 1; i < boxes.Count; i++)
            {
                result.XMinMm = Math.Min(result.XMinMm, Mm(boxes[i][0]));
                result.YMinMm = Math.Min(result.YMinMm, Mm(boxes[i][1]));
                result.ZMinMm = Math.Min(result.ZMinMm, Mm(boxes[i][2]));
                result.XMaxMm = Math.Max(result.XMaxMm, Mm(boxes[i][3]));
                result.YMaxMm = Math.Max(result.YMaxMm, Mm(boxes[i][4]));
                result.ZMaxMm = Math.Max(result.ZMaxMm, Mm(boxes[i][5]));
            }
            return result;
        }

        private static double MaxDelta(GeometryInfo before, GeometryInfo after)
        {
            if (before == null || after == null) return double.MaxValue;
            return Math.Max(
                Math.Max(Math.Abs(before.XMinMm - after.XMinMm), Math.Abs(before.YMinMm - after.YMinMm)),
                Math.Max(
                    Math.Max(Math.Abs(before.ZMinMm - after.ZMinMm), Math.Abs(before.XMaxMm - after.XMaxMm)),
                    Math.Max(Math.Abs(before.YMaxMm - after.YMaxMm), Math.Abs(before.ZMaxMm - after.ZMaxMm))));
        }

        private static double Mm(double meters)
        {
            return Math.Round(meters * 1000.0, 6);
        }

        private static bool IsWithinRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static ISldWorks GetOrCreateSolidWorks()
        {
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
                try
                {
                    object active = Marshal.GetActiveObject(progId);
                    if (active != null) return active as ISldWorks;
                }
                catch { }

                try
                {
                    Type type = Type.GetTypeFromProgID(progId);
                    if (type != null) return Activator.CreateInstance(type) as ISldWorks;
                }
                catch { }
            }
            return null;
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        private static T TryValue<T>(Func<T> fn, T fallback)
        {
            try
            {
                T value = fn();
                return value == null ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        private static void WriteJson(string path, RepairResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, Json(result), new UTF8Encoding(false));
        }

        private static string Json(RepairResult result)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "schema", "winnsen.locker16029.repair_verified_v43_door_panel_feature.v1").Append(",");
            Field(sb, "status", result.Status).Append(",");
            Field(sb, "success", result.Success).Append(",");
            Field(sb, "part_path", result.PartPath).Append(",");
            Field(sb, "allowed_root", result.AllowedRoot).Append(",");
            Field(sb, "target_part_file_name", result.TargetPartFileName).Append(",");
            Field(sb, "target_feature_name", result.TargetFeatureName).Append(",");
            Field(sb, "exists", result.Exists).Append(",");
            Field(sb, "allowed_root_exists", result.AllowedRootExists).Append(",");
            Field(sb, "part_name_matched", result.PartNameMatched).Append(",");
            Field(sb, "path_within_allowed_root", result.PathWithinAllowedRoot).Append(",");
            Field(sb, "solidworks_process_id", result.SolidWorksProcessId).Append(",");
            Field(sb, "opened", result.Opened).Append(",");
            Field(sb, "open_errors", result.OpenErrors).Append(",");
            Field(sb, "open_warnings", result.OpenWarnings).Append(",");
            Field(sb, "initial_rebuilt", result.InitialRebuilt).Append(",");
            Field(sb, "feature_found", result.FeatureFound).Append(",");
            Field(sb, "feature_type", result.FeatureType).Append(",");
            Field(sb, "feature_was_suppressed", result.FeatureWasSuppressed).Append(",");
            Field(sb, "feature_error_code", result.FeatureErrorCode).Append(",");
            Field(sb, "feature_error_code2", result.FeatureErrorCode2).Append(",");
            Field(sb, "feature_warning", result.FeatureWarning).Append(",");
            Field(sb, "suppression_invoked", result.SuppressionInvoked).Append(",");
            Field(sb, "suppression_succeeded", result.SuppressionSucceeded).Append(",");
            Field(sb, "final_rebuilt", result.FinalRebuilt).Append(",");
            Field(sb, "geometry_max_delta_mm", result.GeometryMaxDeltaMm).Append(",");
            Field(sb, "geometry_unchanged", result.GeometryUnchanged).Append(",");
            Field(sb, "saved", result.Saved).Append(",");
            Field(sb, "save_errors", result.SaveErrors).Append(",");
            Field(sb, "save_warnings", result.SaveWarnings).Append(",");
            Field(sb, "error", result.Error).Append(",");
            sb.Append("\"before\":");
            GeometryJson(sb, result.Before);
            sb.Append(",\"after\":");
            GeometryJson(sb, result.After);
            sb.Append("}");
            return sb.ToString();
        }

        private static void GeometryJson(StringBuilder sb, GeometryInfo geometry)
        {
            if (geometry == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append("{");
            Field(sb, "body_count", geometry.BodyCount).Append(",");
            Field(sb, "xmin_mm", geometry.XMinMm).Append(",");
            Field(sb, "ymin_mm", geometry.YMinMm).Append(",");
            Field(sb, "zmin_mm", geometry.ZMinMm).Append(",");
            Field(sb, "xmax_mm", geometry.XMaxMm).Append(",");
            Field(sb, "ymax_mm", geometry.YMaxMm).Append(",");
            Field(sb, "zmax_mm", geometry.ZMaxMm);
            sb.Append("}");
        }

        private static StringBuilder Field(StringBuilder sb, string name, string value)
        {
            sb.Append("\"").Append(Escape(name)).Append("\":");
            if (value == null) sb.Append("null");
            else sb.Append("\"").Append(Escape(value)).Append("\"");
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, bool value)
        {
            return sb.Append("\"").Append(Escape(name)).Append("\":").Append(value ? "true" : "false");
        }

        private static StringBuilder Field(StringBuilder sb, string name, int value)
        {
            return sb.Append("\"").Append(Escape(name)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static StringBuilder Field(StringBuilder sb, string name, double value)
        {
            string raw = double.IsInfinity(value) || double.IsNaN(value)
                ? "null"
                : value.ToString("0.######", CultureInfo.InvariantCulture);
            return sb.Append("\"").Append(Escape(name)).Append("\":").Append(raw);
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class RepairResult
        {
            public string Status = "not_run";
            public bool Success;
            public string PartPath = "";
            public string AllowedRoot = "";
            public string TargetPartFileName = "";
            public string TargetFeatureName = "";
            public bool Exists;
            public bool AllowedRootExists;
            public bool PartNameMatched;
            public bool PathWithinAllowedRoot;
            public int SolidWorksProcessId;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public bool InitialRebuilt;
            public bool FeatureFound;
            public string FeatureType = "";
            public bool FeatureWasSuppressed;
            public int FeatureErrorCode;
            public int FeatureErrorCode2;
            public bool FeatureWarning;
            public bool SuppressionInvoked;
            public bool SuppressionSucceeded;
            public bool FinalRebuilt;
            public GeometryInfo Before;
            public GeometryInfo After;
            public double GeometryMaxDeltaMm;
            public bool GeometryUnchanged;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public string Error = "";
        }

        private sealed class GeometryInfo
        {
            public int BodyCount;
            public double XMinMm;
            public double YMinMm;
            public double ZMinMm;
            public double XMaxMm;
            public double YMaxMm;
            public double ZMaxMm;
        }
    }
}
