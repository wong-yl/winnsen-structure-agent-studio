using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class RestoreDoorLockTongues
    {
        private const string LockTongueName = "\u9501\u820c";
        private const string LockTongueFileName = "\u9501\u820c.SLDPRT";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: RestoreDoorLockTongues.exe <pack-dir> <source-lock-tongue.SLDPRT> <out-json> [door-width-mm]");
                return 2;
            }

            string packDir = Path.GetFullPath(args[0]);
            string sourcePart = Path.GetFullPath(args[1]);
            string outJson = Path.GetFullPath(args[2]);
            double doorWidthMm = args.Length >= 4
                ? ParseDouble(args[3], 307.0)
                : 307.0;

            var result = new RestoreResult
            {
                PackDir = packDir,
                SourcePart = sourcePart,
                DoorWidthMm = doorWidthMm,
                LockTonguePart = Path.Combine(packDir, LockTongueFileName),
                PackDirExists = Directory.Exists(packDir),
                SourcePartExists = File.Exists(sourcePart),
            };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));
                if (!result.PackDirExists)
                {
                    result.Error = "pack dir does not exist";
                    WriteJson(outJson, result);
                    return 2;
                }
                if (!result.SourcePartExists)
                {
                    result.Error = "source lock tongue part does not exist";
                    WriteJson(outJson, result);
                    return 2;
                }

                File.Copy(sourcePart, result.LockTonguePart, true);
                result.LockTongueCopied = File.Exists(result.LockTonguePart);
                if (!result.LockTongueCopied)
                {
                    result.Error = "lock tongue part copy failed";
                    WriteJson(outJson, result);
                    return 3;
                }

                ISldWorks sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 4;
                }

                sw.Visible = true;
                MathUtility math = TryValue(() => sw.GetMathUtility() as MathUtility, null);
                if (math == null)
                {
                    result.Error = "SolidWorks MathUtility unavailable";
                    WriteJson(outJson, result);
                    return 5;
                }

                foreach (DoorAssemblySpec spec in FindDoorAssemblies(packDir))
                {
                    RestoreItem item = RestoreOne(sw, math, spec, result.LockTonguePart, doorWidthMm);
                    result.Items.Add(item);
                }

                result.AssemblyCount = result.Items.Count;
                foreach (RestoreItem item in result.Items)
                {
                    if (item.Added) result.AddedCount++;
                    if (item.SkippedExisting) result.SkippedExistingCount++;
                    if (!string.IsNullOrWhiteSpace(item.Error) || !item.Rebuilt || !item.Saved) result.FailedCount++;
                }

                result.Success = result.AssemblyCount > 0 && result.FailedCount == 0 && (result.AddedCount + result.SkippedExistingCount) == result.AssemblyCount;
                result.Status = result.Success ? "restored" : "failed";
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Success ? 0 : 6;
            }
            catch (Exception ex)
            {
                result.Error = SafeExceptionText(ex);
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 1;
            }
        }

        private static IEnumerable<DoorAssemblySpec> FindDoorAssemblies(string packDir)
        {
            var asciiRegex = new Regex(@"_(?<side>[LR])(?<unit>\d+(?:p\d+)?|\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var chineseDoorRegex = new Regex(@"\u50a8\u7269\u67dc\u95e8(?<unit>\d+(?:[p\.]\d+)?)\u2571?12\u88c5\u914d(?:_(?<sideZh>\u5de6|\u53f3))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var source14DoorRegex = new Regex(@"\u50a8\u7269\u67dc\u95e814\u95e8\u6e90\u94a3\u91d1\u88c5\u914d(?:_(?<sideZh>\u5de6|\u53f3))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (string path in Directory.GetFiles(packDir, "*.SLDASM"))
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                Match match = asciiRegex.Match(stem);
                if (match.Success)
                {
                    yield return new DoorAssemblySpec
                    {
                        Path = path,
                        Side = match.Groups["side"].Value.ToUpperInvariant(),
                        Unit = match.Groups["unit"].Value.Replace('p', '.'),
                    };
                    continue;
                }

                match = source14DoorRegex.Match(stem);
                if (match.Success)
                {
                    string source14SideZh = match.Groups["sideZh"].Value;
                    yield return new DoorAssemblySpec
                    {
                        Path = path,
                        Side = source14SideZh == "\u53f3" ? "R" : "L",
                        Unit = "14door_source_sheetmetal"
                    };
                    continue;
                }

                match = chineseDoorRegex.Match(stem);
                if (!match.Success) continue;
                string sideZh = match.Groups["sideZh"].Value;
                yield return new DoorAssemblySpec
                {
                    Path = path,
                    Side = sideZh == "\u53f3" ? "R" : "L",
                    Unit = match.Groups["unit"].Value.Replace('p', '.'),
                };
            }
        }

        private static RestoreItem RestoreOne(ISldWorks sw, MathUtility math, DoorAssemblySpec spec, string lockTonguePart, double doorWidthMm)
        {
            var item = new RestoreItem
            {
                AssemblyPath = spec.Path,
                Side = spec.Side,
                Unit = spec.Unit,
                LockTonguePart = lockTonguePart,
            };

            int errors = 0;
            int warnings = 0;
            ModelDoc2 model = sw.OpenDoc6(
                spec.Path,
                (int)swDocumentTypes_e.swDocASSEMBLY,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as ModelDoc2;
            if (model == null)
            {
                model = TryValue(() => sw.OpenDoc(spec.Path, (int)swDocumentTypes_e.swDocASSEMBLY) as ModelDoc2, null);
            }

            item.Opened = model != null;
            item.OpenErrors = errors;
            item.OpenWarnings = warnings;
            if (model == null)
            {
                item.Error = "failed to open door assembly";
                return item;
            }
            Try(() => model.ShowFeatureErrorDialog = false);

            AssemblyDoc asm = model as AssemblyDoc;
            if (asm == null)
            {
                item.Error = "opened document is not an assembly";
                Close(sw, model);
                return item;
            }

            Try(() => asm.ResolveAllLightWeightComponents(false));
            item.ExistingCount = CountExistingLockTongues(model, asm);
            if (item.ExistingCount > 0)
            {
                item.SkippedExisting = true;
                item.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                if (!item.Rebuilt) item.Error = "door assembly rebuild failed";
                item.Saved = Save(model, item);
                item.Closed = Close(sw, model);
                return item;
            }

            ModelDoc2 partDoc = OpenPart(sw, lockTonguePart, item);
            if (partDoc == null)
            {
                item.Error = "failed to open lock tongue part";
                Close(sw, model);
                return item;
            }

            Try(() => sw.ActivateDoc2(model.GetTitle(), false, ref errors));
            double side = string.Equals(spec.Side, "L", StringComparison.OrdinalIgnoreCase) ? 1.0 : -1.0;
            double doorHalfWidth = doorWidthMm / 2.0;
            item.TxMm = (doorHalfWidth - 15.0) * side;
            item.TyMm = 0.0;
            item.TzMm = -11.3;

            Component2 comp = asm.AddComponent5(
                lockTonguePart,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "",
                false,
                "",
                item.TxMm / 1000.0,
                item.TyMm / 1000.0,
                item.TzMm / 1000.0) as Component2;
            item.Added = comp != null;
            item.PartClosed = Close(sw, partDoc);
            if (comp == null)
            {
                item.Error = "AddComponent5 returned null";
                Close(sw, model);
                return item;
            }

            Try(() => { comp.Name2 = LockTongueName; });
            MathTransform xf = math.CreateTransform(ToTransformData(FlipXZ(), item.TxMm, item.TyMm, item.TzMm)) as MathTransform;
            item.TransformCreated = xf != null;
            if (xf == null)
            {
                item.Error = "transform create failed";
                Close(sw, model);
                return item;
            }
            item.TransformApplied = TryValue(() => comp.SetTransformAndSolve2(xf), false);
            if (!item.TransformApplied)
            {
                item.Error = "transform apply failed";
                Close(sw, model);
                return item;
            }

            item.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
            if (!item.Rebuilt) item.Error = "door assembly rebuild failed";
            item.Saved = Save(model, item);
            item.Closed = Close(sw, model);
            return item;
        }

        private static ModelDoc2 OpenPart(ISldWorks sw, string path, RestoreItem item)
        {
            int errors = 0;
            int warnings = 0;
            ModelDoc2 partDoc = sw.OpenDoc6(
                path,
                (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as ModelDoc2;
            item.PartOpenErrors = errors;
            item.PartOpenWarnings = warnings;
            return partDoc ?? TryValue(() => sw.OpenDoc(path, (int)swDocumentTypes_e.swDocPART) as ModelDoc2, null);
        }

        private static int CountExistingLockTongues(ModelDoc2 model, AssemblyDoc asm)
        {
            int count = 0;
            var regex = new Regex(@"\u9501\u820c|lock[_ -]?tongue", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (Component2 component in EnumerateComponents(model, asm))
            {
                string path = TryValue(() => component.GetPathName(), "") ?? "";
                string text = (TryValue(() => component.Name2, "") ?? "") + " " + Path.GetFileName(path);
                if (regex.IsMatch(text)) count++;
            }
            return count;
        }

        private static IEnumerable<Component2> EnumerateComponents(ModelDoc2 model, AssemblyDoc asm)
        {
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Configuration cfg = TryValue(() => model.GetActiveConfiguration() as Configuration, null);
            Component2 root = cfg == null ? null : TryValue(() => cfg.GetRootComponent3(true), null);
            if (root != null)
            {
                foreach (Component2 child in AsComponents(TryValue(() => root.GetChildren(), null)))
                {
                    foreach (Component2 nested in EnumerateRecursive(child, emitted))
                    {
                        yield return nested;
                    }
                }
            }

            foreach (Component2 component in AsComponents(TryValue(() => asm.GetComponents(false), null)))
            {
                string key = ComponentKey(component);
                if (emitted.Add(key)) yield return component;
            }
        }

        private static IEnumerable<Component2> EnumerateRecursive(Component2 component, HashSet<string> emitted)
        {
            if (component == null) yield break;
            string key = ComponentKey(component);
            if (emitted.Add(key)) yield return component;

            foreach (Component2 child in AsComponents(TryValue(() => component.GetChildren(), null)))
            {
                foreach (Component2 nested in EnumerateRecursive(child, emitted))
                {
                    yield return nested;
                }
            }
        }

        private static IEnumerable<Component2> AsComponents(object raw)
        {
            if (raw == null) yield break;
            Array array = raw as Array;
            if (array == null)
            {
                Component2 single = raw as Component2;
                if (single != null) yield return single;
                yield break;
            }

            foreach (object item in array)
            {
                Component2 component = item as Component2;
                if (component != null) yield return component;
            }
        }

        private static string ComponentKey(Component2 component)
        {
            string name = TryValue(() => component.Name2, "");
            string path = TryValue(() => component.GetPathName(), "");
            return name + "|" + path;
        }

        private static bool Save(ModelDoc2 model, RestoreItem item)
        {
            int saveErrors = 0;
            int saveWarnings = 0;
            bool saved = TryValue(
                () => model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings),
                false);
            item.SaveErrors = saveErrors;
            item.SaveWarnings = saveWarnings;
            return saved;
        }

        private static object ToTransformData(double[] r, double txMm, double tyMm, double tzMm)
        {
            return new double[]
            {
                r[0], r[1], r[2],
                r[3], r[4], r[5],
                r[6], r[7], r[8],
                txMm / 1000.0, tyMm / 1000.0, tzMm / 1000.0,
                1.0, 0.0, 0.0, 0.0
            };
        }

        private static double[] FlipXZ()
        {
            return new double[] { -1, 0, 0, 0, 1, 0, 0, 0, -1 };
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
                    Type t = Type.GetTypeFromProgID(progId);
                    if (t != null) return Activator.CreateInstance(t) as ISldWorks;
                }
                catch { }
            }
            return null;
        }

        private static bool Close(ISldWorks sw, ModelDoc2 model)
        {
            string title = TryValue(() => model.GetTitle(), "");
            if (string.IsNullOrEmpty(title)) return false;
            return TryValue(() =>
            {
                sw.CloseDoc(title);
                return true;
            }, false);
        }

        private static double ParseDouble(string text, double fallback)
        {
            double value;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
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

        private static string SafeExceptionText(Exception ex)
        {
            return ex == null ? "" : ex.ToString().Replace("\r\n", "\n");
        }

        private static void WriteJson(string path, RestoreResult result)
        {
            File.WriteAllText(path, Json(result), new UTF8Encoding(false));
        }

        private static string Json(RestoreResult result)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "schema", "winnsen.locker16029.restore_door_lock_tongues.v1").Append(",");
            Field(sb, "status", result.Status).Append(",");
            Field(sb, "success", result.Success).Append(",");
            Field(sb, "pack_dir", result.PackDir).Append(",");
            Field(sb, "pack_dir_exists", result.PackDirExists).Append(",");
            Field(sb, "source_part", result.SourcePart).Append(",");
            Field(sb, "source_part_exists", result.SourcePartExists).Append(",");
            Field(sb, "lock_tongue_part", result.LockTonguePart).Append(",");
            Field(sb, "lock_tongue_copied", result.LockTongueCopied).Append(",");
            Field(sb, "door_width_mm", result.DoorWidthMm).Append(",");
            Field(sb, "assembly_count", result.AssemblyCount).Append(",");
            Field(sb, "added_count", result.AddedCount).Append(",");
            Field(sb, "skipped_existing_count", result.SkippedExistingCount).Append(",");
            Field(sb, "failed_count", result.FailedCount).Append(",");
            Field(sb, "error", result.Error).Append(",");
            sb.Append("\"items\":[");
            for (int i = 0; i < result.Items.Count; i++)
            {
                if (i > 0) sb.Append(",");
                ItemJson(sb, result.Items[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void ItemJson(StringBuilder sb, RestoreItem item)
        {
            sb.Append("{");
            Field(sb, "assembly_path", item.AssemblyPath).Append(",");
            Field(sb, "side", item.Side).Append(",");
            Field(sb, "unit", item.Unit).Append(",");
            Field(sb, "lock_tongue_part", item.LockTonguePart).Append(",");
            Field(sb, "opened", item.Opened).Append(",");
            Field(sb, "open_errors", item.OpenErrors).Append(",");
            Field(sb, "open_warnings", item.OpenWarnings).Append(",");
            Field(sb, "part_open_errors", item.PartOpenErrors).Append(",");
            Field(sb, "part_open_warnings", item.PartOpenWarnings).Append(",");
            Field(sb, "part_closed", item.PartClosed).Append(",");
            Field(sb, "existing_count", item.ExistingCount).Append(",");
            Field(sb, "skipped_existing", item.SkippedExisting).Append(",");
            Field(sb, "added", item.Added).Append(",");
            Field(sb, "transform_created", item.TransformCreated).Append(",");
            Field(sb, "transform_applied", item.TransformApplied).Append(",");
            Field(sb, "tx_mm", item.TxMm).Append(",");
            Field(sb, "ty_mm", item.TyMm).Append(",");
            Field(sb, "tz_mm", item.TzMm).Append(",");
            Field(sb, "rebuilt", item.Rebuilt).Append(",");
            Field(sb, "saved", item.Saved).Append(",");
            Field(sb, "save_errors", item.SaveErrors).Append(",");
            Field(sb, "save_warnings", item.SaveWarnings).Append(",");
            Field(sb, "closed", item.Closed).Append(",");
            Field(sb, "error", item.Error);
            sb.Append("}");
        }

        private static StringBuilder Field(StringBuilder sb, string name, string value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":");
            if (value == null)
            {
                sb.Append("null");
            }
            else
            {
                sb.Append('"').Append(Escape(value)).Append('"');
            }
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, bool value)
        {
            return FieldRaw(sb, name, value ? "true" : "false");
        }

        private static StringBuilder Field(StringBuilder sb, string name, int value)
        {
            return FieldRaw(sb, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static StringBuilder Field(StringBuilder sb, string name, double value)
        {
            return FieldRaw(sb, name, value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static StringBuilder FieldRaw(StringBuilder sb, string name, string raw)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(raw);
            return sb;
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private sealed class DoorAssemblySpec
        {
            public string Path = "";
            public string Side = "";
            public string Unit = "";
        }

        private sealed class RestoreResult
        {
            public string Status = "not_run";
            public bool Success;
            public string PackDir = "";
            public bool PackDirExists;
            public string SourcePart = "";
            public bool SourcePartExists;
            public string LockTonguePart = "";
            public bool LockTongueCopied;
            public double DoorWidthMm;
            public int AssemblyCount;
            public int AddedCount;
            public int SkippedExistingCount;
            public int FailedCount;
            public string Error = "";
            public readonly List<RestoreItem> Items = new List<RestoreItem>();
        }

        private sealed class RestoreItem
        {
            public string AssemblyPath = "";
            public string Side = "";
            public string Unit = "";
            public string LockTonguePart = "";
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public int PartOpenErrors;
            public int PartOpenWarnings;
            public bool PartClosed;
            public int ExistingCount;
            public bool SkippedExisting;
            public bool Added;
            public bool TransformCreated;
            public bool TransformApplied;
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public bool Rebuilt;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public bool Closed;
            public string Error = "";
        }
    }
}
