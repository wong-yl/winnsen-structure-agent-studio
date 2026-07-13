using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class Add16029V43LockHoleDatums
    {
        private const string LockTongueText = "\u9501\u820c";
        private const string LockHoleDatumText = "\u9501\u5b54\u57fa\u51c6";
        private const double DatumOffsetXAbsMm = 19.8;
        private const double DatumOffsetYMm = -4.9;
        private const double DatumOffsetZMm = -12.7;

        private static readonly Vec LeftSourceCenterMm = new Vec(-74.8, 1701.1, -38.5);
        private static readonly Vec RightSourceCenterMm = new Vec(74.8, 1701.1, -38.5);
        private static readonly Regex DoorSpecRegex = new Regex(@"(?<side>[LR])(?<unit>[246])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: Add16029V43LockHoleDatums.exe <assembly.SLDASM> <left-datum.SLDPRT> <right-datum.SLDPRT> <out-json>");
                return 2;
            }

            var result = new Result
            {
                AssemblyPath = Path.GetFullPath(args[0]),
                LeftDatumPath = Path.GetFullPath(args[1]),
                RightDatumPath = Path.GetFullPath(args[2]),
                OutJson = Path.GetFullPath(args[3]),
            };

            ISldWorks sw = null;
            ModelDoc2 asmModel = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(result.OutJson));
                result.AssemblyExists = File.Exists(result.AssemblyPath);
                result.LeftDatumExists = File.Exists(result.LeftDatumPath);
                result.RightDatumExists = File.Exists(result.RightDatumPath);
                if (!result.AssemblyExists || !result.LeftDatumExists || !result.RightDatumExists)
                {
                    result.Error = "assembly or datum source part missing";
                    WriteJson(result.OutJson, result);
                    return 2;
                }

                sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(result.OutJson, result);
                    return 3;
                }
                sw.Visible = true;
                result.SolidWorksProcessId = TryValue(() => sw.GetProcessID(), 0);

                int errors = 0;
                int warnings = 0;
                asmModel = TryValue(() => sw.OpenDoc6(
                    result.AssemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2, null);
                result.Opened = asmModel != null;
                result.OpenErrors = errors;
                result.OpenWarnings = warnings;
                if (asmModel == null)
                {
                    result.Error = "open assembly failed";
                    WriteJson(result.OutJson, result);
                    return 4;
                }

                var asm = asmModel as AssemblyDoc;
                var math = TryValue(() => sw.GetMathUtility() as MathUtility, null);
                if (asm == null || math == null)
                {
                    result.Error = "assembly or MathUtility unavailable";
                    WriteJson(result.OutJson, result);
                    return 5;
                }

                Try(() => asm.ResolveAllLightWeightComponents(false));
                var components = AsComponents(TryValue(() => asm.GetComponents(false), null)).ToList();
                var existingDatumComponents = components.Where(IsTopLevelLockHoleDatum).ToList();
                result.ExistingDatumCountBefore = existingDatumComponents.Count;
                result.ExistingDatumSelectedCount = RemoveExistingDatums(asmModel, existingDatumComponents);
                components = AsComponents(TryValue(() => asm.GetComponents(false), null)).ToList();
                result.ExistingDatumCountAfterCleanup = components.Count(IsTopLevelLockHoleDatum);
                result.ExistingDatumRemovedCount = Math.Max(0, result.ExistingDatumCountBefore - result.ExistingDatumCountAfterCleanup);
                if (result.ExistingDatumCountAfterCleanup != 0)
                {
                    result.Error = "existing lock-hole datum cleanup failed";
                    WriteJson(result.OutJson, result);
                    return 6;
                }
                var existing = ExistingByRole(components);
                var tongues = components
                    .Where(IsLockTongue)
                    .Select(ToTongue)
                    .Where(item => item != null)
                    .OrderBy(item => item.Side)
                    .ThenBy(item => item.Center.Y)
                    .ToList();

                result.LockTongueCount = tongues.Count;
                foreach (Tongue tongue in tongues)
                {
                    result.Items.Add(AddOrUpdate(sw, asmModel, asm, math, existing, result, tongue));
                }

                result.AddedCount = Count(result.Items, item => item.Added);
                result.UpdatedCount = Count(result.Items, item => item.Updated);
                result.TransformAppliedCount = Count(result.Items, item => item.TransformApplied);
                result.MatchedCount = Count(result.Items, item => item.DistanceMm <= 1.0);
                result.Rebuilt = TryValue(() => asmModel.ForceRebuild3(false), false);
                var finalComponents = AsComponents(TryValue(() => asm.GetComponents(false), null)).ToList();
                result.FinalDatumCount = finalComponents.Count(IsTopLevelLockHoleDatum);
                int saveErrors = 0;
                int saveWarnings = 0;
                result.Saved = TryValue(() => asmModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings), false);
                result.SaveErrors = saveErrors;
                result.SaveWarnings = saveWarnings;
                result.SaveFlagAfter = TryValue(() => asmModel.GetSaveFlag(), true);
                result.Success =
                    result.LockTongueCount == 6 &&
                    result.Items.Count == 6 &&
                    result.TransformAppliedCount == 6 &&
                    result.MatchedCount == 6 &&
                    result.ExistingDatumCountAfterCleanup == 0 &&
                    result.FinalDatumCount == 6 &&
                    result.Rebuilt &&
                    result.Saved &&
                    !result.SaveFlagAfter;
                if (!result.Success && string.IsNullOrWhiteSpace(result.Error)) result.Error = "lock-hole datum placement validation failed";
                WriteJson(result.OutJson, result);
                Console.WriteLine(result.OutJson);
                return result.Success ? 0 : 10;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString().Replace("\r\n", "\n");
                WriteJson(result.OutJson, result);
                Console.WriteLine(result.OutJson);
                return 1;
            }
        }

        private static bool IsLockTongue(Component2 component)
        {
            string name = TryValue(() => component.Name2, "");
            string path = TryValue(() => component.GetPathName(), "");
            return name.IndexOf(LockTongueText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(LockTongueText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("lock_tongue", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTopLevelLockHoleDatum(Component2 component)
        {
            string name = TryValue(() => component.Name2, "");
            string path = TryValue(() => component.GetPathName(), "");
            if (name.IndexOf("/", StringComparison.Ordinal) >= 0) return false;
            return name.IndexOf(LockHoleDatumText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(LockHoleDatumText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("lock_hole_datum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("lock_mounting_hole_datum", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int RemoveExistingDatums(ModelDoc2 model, IEnumerable<Component2> components)
        {
            int selected = 0;
            Try(() => model.ClearSelection2(true));
            foreach (Component2 component in components)
            {
                if (component != null && TryValue(() => component.Select4(true, null, false), false)) selected++;
            }
            if (selected > 0)
            {
                Try(() => model.EditDelete());
                Try(() => model.ClearSelection2(true));
                Try(() => model.ForceRebuild3(false));
            }
            return selected;
        }

        private static Tongue ToTongue(Component2 component)
        {
            var box = ToBoxInfo(TryValue(() => component.GetBox(false, false), null));
            if (!box.Valid) return null;
            string name = TryValue(() => component.Name2, "");
            string side = box.Center.X < 0 ? "L" : "R";
            string unit = "";
            Match match = DoorSpecRegex.Match(name);
            if (match.Success)
            {
                side = match.Groups["side"].Value.ToUpperInvariant();
                unit = match.Groups["unit"].Value;
            }
            return new Tongue
            {
                Name = name,
                Path = TryValue(() => component.GetPathName(), ""),
                Side = side,
                Unit = unit,
                Center = box.Center,
                Box = box,
            };
        }

        private static Dictionary<string, Component2> ExistingByRole(IEnumerable<Component2> components)
        {
            var map = new Dictionary<string, Component2>(StringComparer.OrdinalIgnoreCase);
            foreach (Component2 component in components)
            {
                string name = TryValue(() => component.Name2, "");
                if (name.IndexOf(LockHoleDatumText, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string role = RoleFromName(name);
                if (!string.IsNullOrWhiteSpace(role) && !map.ContainsKey(role)) map.Add(role, component);
            }
            return map;
        }

        private static string RoleFromName(string name)
        {
            int index = name.IndexOf("-1", StringComparison.OrdinalIgnoreCase);
            return index > 0 ? name.Substring(0, index) : name;
        }

        private static Item AddOrUpdate(ISldWorks sw, ModelDoc2 asmModel, AssemblyDoc asm, MathUtility math, Dictionary<string, Component2> existing, Result result, Tongue tongue)
        {
            string side = string.Equals(tongue.Side, "R", StringComparison.OrdinalIgnoreCase) ? "R" : "L";
            string role = string.Format(
                CultureInfo.InvariantCulture,
                "{0}_{1}_row_{2:0000}_aligned_to_lock_tongue",
                LockHoleDatumText,
                side,
                Math.Round(tongue.Center.Y));
            string sourcePath = side == "R" ? result.RightDatumPath : result.LeftDatumPath;
            Vec sourceCenter = side == "R" ? RightSourceCenterMm : LeftSourceCenterMm;
            double xOffset = side == "R" ? DatumOffsetXAbsMm : -DatumOffsetXAbsMm;
            Vec targetCenter = new Vec(tongue.Center.X + xOffset, tongue.Center.Y + DatumOffsetYMm, tongue.Center.Z + DatumOffsetZMm);
            Vec transformMm = targetCenter - sourceCenter;

            var item = new Item
            {
                Role = role,
                SourcePath = sourcePath,
                TongueName = tongue.Name,
                TongueCenter = tongue.Center,
                TargetCenter = targetCenter,
                TxMm = transformMm.X,
                TyMm = transformMm.Y,
                TzMm = transformMm.Z,
            };

            Component2 component = null;
            if (existing.TryGetValue(role, out component) && component != null)
            {
                item.Updated = true;
            }
            else
            {
                int errors = 0;
                int warnings = 0;
                ModelDoc2 partDoc = TryValue(() => sw.OpenDoc6(
                    sourcePath,
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                        (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2, null);
                item.PartOpened = partDoc != null;
                item.PartOpenErrors = errors;
                item.PartOpenWarnings = warnings;
                if (partDoc == null)
                {
                    item.Error = "open lock-hole datum part failed";
                    return item;
                }

                string partTitle = TryValue(() => partDoc.GetTitle(), "");
                try
                {
                    Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
                    component = TryValue(() => asm.AddComponent5(
                        sourcePath,
                        (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                        "",
                        false,
                        "",
                        transformMm.X / 1000.0,
                        transformMm.Y / 1000.0,
                        transformMm.Z / 1000.0) as Component2, null);
                    item.Added = component != null;
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(partTitle) && !string.Equals(partTitle, asmModel.GetTitle(), StringComparison.OrdinalIgnoreCase))
                    {
                        Try(() => sw.CloseDoc(partTitle));
                    }
                    Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
                }
                if (component == null)
                {
                    item.Error = "AddComponent5 returned null";
                    return item;
                }
                Try(() => { component.Name2 = role; });
                existing[role] = component;
            }

            MathTransform transform = TryValue(() => math.CreateTransform(ToTransformData(transformMm)) as MathTransform, null);
            item.TransformCreated = transform != null;
            if (transform == null)
            {
                item.Error = "transform create failed";
                return item;
            }
            item.TransformApplied = TryValue(() => component.SetTransformAndSolve2(transform), false);
            if (!item.TransformApplied)
            {
                item.Error = "SetTransformAndSolve2 failed";
                return item;
            }
            item.Name = TryValue(() => component.Name2, "");
            item.Path = TryValue(() => component.GetPathName(), "");
            item.Box = ToBoxInfo(TryValue(() => component.GetBox(false, false), null));
            item.ActualCenter = item.Box.Center;
            item.DistanceMm = Distance(item.ActualCenter, item.TargetCenter);
            if (item.DistanceMm > 1.0) item.Error = "placed datum center is outside 1mm tolerance";
            return item;
        }

        private static double[] ToTransformData(Vec mm)
        {
            return new[]
            {
                1.0, 0.0, 0.0,
                0.0, 1.0, 0.0,
                0.0, 0.0, 1.0,
                mm.X / 1000.0, mm.Y / 1000.0, mm.Z / 1000.0,
                1.0, 0.0, 0.0, 0.0
            };
        }

        private static BoxInfo ToBoxInfo(object raw)
        {
            Array arr = raw as Array;
            if (arr == null || arr.Length < 6) return new BoxInfo();
            return new BoxInfo
            {
                Valid = true,
                XMinMm = Convert.ToDouble(arr.GetValue(0), CultureInfo.InvariantCulture) * 1000.0,
                YMinMm = Convert.ToDouble(arr.GetValue(1), CultureInfo.InvariantCulture) * 1000.0,
                ZMinMm = Convert.ToDouble(arr.GetValue(2), CultureInfo.InvariantCulture) * 1000.0,
                XMaxMm = Convert.ToDouble(arr.GetValue(3), CultureInfo.InvariantCulture) * 1000.0,
                YMaxMm = Convert.ToDouble(arr.GetValue(4), CultureInfo.InvariantCulture) * 1000.0,
                ZMaxMm = Convert.ToDouble(arr.GetValue(5), CultureInfo.InvariantCulture) * 1000.0,
            };
        }

        private static IEnumerable<Component2> AsComponents(object raw)
        {
            Array arr = raw as Array;
            if (arr == null) yield break;
            foreach (object item in arr)
            {
                Component2 component = item as Component2;
                if (component != null) yield return component;
            }
        }

        private static double Distance(Vec a, Vec b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static int Count<T>(IEnumerable<T> items, Func<T, bool> predicate)
        {
            int count = 0;
            foreach (T item in items)
            {
                if (predicate(item)) count++;
            }
            return count;
        }

        private static ISldWorks GetOrCreateSolidWorks()
        {
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
                try { object active = Marshal.GetActiveObject(progId); if (active != null) return active as ISldWorks; } catch { }
                try { Type t = Type.GetTypeFromProgID(progId); if (t != null) return Activator.CreateInstance(t) as ISldWorks; } catch { }
            }
            return null;
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        private static T TryValue<T>(Func<T> fn, T fallback)
        {
            try { T value = fn(); return value == null ? fallback : value; } catch { return fallback; }
        }

        private static void WriteJson(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Json(result), new UTF8Encoding(false));
        }

        private static string Json(Result result)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "schema", "winnsen.locker16029.add_v43_lock_hole_datums.v1").Append(",");
            Field(sb, "assembly_path", result.AssemblyPath).Append(",");
            Field(sb, "left_datum_path", result.LeftDatumPath).Append(",");
            Field(sb, "right_datum_path", result.RightDatumPath).Append(",");
            Field(sb, "assembly_exists", result.AssemblyExists).Append(",");
            Field(sb, "left_datum_exists", result.LeftDatumExists).Append(",");
            Field(sb, "right_datum_exists", result.RightDatumExists).Append(",");
            Field(sb, "solidworks_process_id", result.SolidWorksProcessId).Append(",");
            Field(sb, "opened", result.Opened).Append(",");
            Field(sb, "open_errors", result.OpenErrors).Append(",");
            Field(sb, "open_warnings", result.OpenWarnings).Append(",");
            Field(sb, "lock_tongue_count", result.LockTongueCount).Append(",");
            Field(sb, "existing_datum_count_before", result.ExistingDatumCountBefore).Append(",");
            Field(sb, "existing_datum_selected_count", result.ExistingDatumSelectedCount).Append(",");
            Field(sb, "existing_datum_removed_count", result.ExistingDatumRemovedCount).Append(",");
            Field(sb, "existing_datum_count_after_cleanup", result.ExistingDatumCountAfterCleanup).Append(",");
            Field(sb, "final_datum_count", result.FinalDatumCount).Append(",");
            Field(sb, "added_count", result.AddedCount).Append(",");
            Field(sb, "updated_count", result.UpdatedCount).Append(",");
            Field(sb, "transform_applied_count", result.TransformAppliedCount).Append(",");
            Field(sb, "matched_count", result.MatchedCount).Append(",");
            Field(sb, "rebuilt", result.Rebuilt).Append(",");
            Field(sb, "saved", result.Saved).Append(",");
            Field(sb, "save_errors", result.SaveErrors).Append(",");
            Field(sb, "save_warnings", result.SaveWarnings).Append(",");
            Field(sb, "save_flag_after", result.SaveFlagAfter).Append(",");
            Field(sb, "success", result.Success).Append(",");
            Field(sb, "error", result.Error).Append(",");
            sb.Append("\"items\":[");
            for (int i = 0; i < result.Items.Count; i++)
            {
                if (i > 0) sb.Append(",");
                Json(sb, result.Items[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void Json(StringBuilder sb, Item item)
        {
            sb.Append("{");
            Field(sb, "role", item.Role).Append(",");
            Field(sb, "name", item.Name).Append(",");
            Field(sb, "path", item.Path).Append(",");
            Field(sb, "source_path", item.SourcePath).Append(",");
            Field(sb, "tongue_name", item.TongueName).Append(",");
            Field(sb, "tx_mm", item.TxMm).Append(",");
            Field(sb, "ty_mm", item.TyMm).Append(",");
            Field(sb, "tz_mm", item.TzMm).Append(",");
            Field(sb, "part_opened", item.PartOpened).Append(",");
            Field(sb, "part_open_errors", item.PartOpenErrors).Append(",");
            Field(sb, "part_open_warnings", item.PartOpenWarnings).Append(",");
            Field(sb, "added", item.Added).Append(",");
            Field(sb, "updated", item.Updated).Append(",");
            Field(sb, "transform_created", item.TransformCreated).Append(",");
            Field(sb, "transform_applied", item.TransformApplied).Append(",");
            Field(sb, "distance_mm", item.DistanceMm).Append(",");
            sb.Append("\"tongue_center\":");
            Json(sb, item.TongueCenter).Append(",");
            sb.Append("\"target_center\":");
            Json(sb, item.TargetCenter).Append(",");
            sb.Append("\"actual_center\":");
            Json(sb, item.ActualCenter).Append(",");
            sb.Append("\"box\":");
            Json(sb, item.Box).Append(",");
            Field(sb, "error", item.Error);
            sb.Append("}");
        }

        private static StringBuilder Json(StringBuilder sb, Vec value)
        {
            sb.Append("{");
            Field(sb, "x_mm", value.X).Append(",");
            Field(sb, "y_mm", value.Y).Append(",");
            Field(sb, "z_mm", value.Z);
            sb.Append("}");
            return sb;
        }

        private static StringBuilder Json(StringBuilder sb, BoxInfo box)
        {
            sb.Append("{");
            Field(sb, "xmin_mm", box.XMinMm).Append(",");
            Field(sb, "xmax_mm", box.XMaxMm).Append(",");
            Field(sb, "ymin_mm", box.YMinMm).Append(",");
            Field(sb, "ymax_mm", box.YMaxMm).Append(",");
            Field(sb, "zmin_mm", box.ZMinMm).Append(",");
            Field(sb, "zmax_mm", box.ZMaxMm);
            sb.Append("}");
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, string value)
        {
            return sb.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? "")).Append("\"");
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
            return sb.Append("\"").Append(Escape(name)).Append("\":").Append(Math.Round(value, 6).ToString(CultureInfo.InvariantCulture));
        }

        private static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private struct Vec
        {
            public readonly double X;
            public readonly double Y;
            public readonly double Z;

            public Vec(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static Vec operator -(Vec a, Vec b)
            {
                return new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            }
        }

        private sealed class Tongue
        {
            public string Name = "";
            public string Path = "";
            public string Side = "";
            public string Unit = "";
            public Vec Center;
            public BoxInfo Box = new BoxInfo();
        }

        private sealed class Result
        {
            public string AssemblyPath = "";
            public string LeftDatumPath = "";
            public string RightDatumPath = "";
            public string OutJson = "";
            public bool AssemblyExists;
            public bool LeftDatumExists;
            public bool RightDatumExists;
            public int SolidWorksProcessId;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public int LockTongueCount;
            public int ExistingDatumCountBefore;
            public int ExistingDatumSelectedCount;
            public int ExistingDatumRemovedCount;
            public int ExistingDatumCountAfterCleanup;
            public int FinalDatumCount;
            public int AddedCount;
            public int UpdatedCount;
            public int TransformAppliedCount;
            public int MatchedCount;
            public bool Rebuilt;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public bool SaveFlagAfter = true;
            public bool Success;
            public string Error = "";
            public readonly List<Item> Items = new List<Item>();
        }

        private sealed class Item
        {
            public string Role = "";
            public string Name = "";
            public string Path = "";
            public string SourcePath = "";
            public string TongueName = "";
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public Vec TongueCenter;
            public Vec TargetCenter;
            public Vec ActualCenter;
            public double DistanceMm;
            public bool PartOpened;
            public int PartOpenErrors;
            public int PartOpenWarnings;
            public bool Added;
            public bool Updated;
            public bool TransformCreated;
            public bool TransformApplied;
            public BoxInfo Box = new BoxInfo();
            public string Error = "";
        }

        private sealed class BoxInfo
        {
            public bool Valid;
            public double XMinMm;
            public double XMaxMm;
            public double YMinMm;
            public double YMaxMm;
            public double ZMinMm;
            public double ZMaxMm;
            public Vec Center { get { return new Vec((XMinMm + XMaxMm) / 2.0, (YMinMm + YMaxMm) / 2.0, (ZMinMm + ZMaxMm) / 2.0); } }
        }
    }
}
