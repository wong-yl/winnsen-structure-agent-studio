using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class Normalize16029V43ShelfBands
    {
        private const string LeftShelfText = "\u7bb1\u4f53\u6a2a\u5c42\u677fL\u710a\u63a5";
        private const string RightShelfText = "\u7bb1\u4f53\u6a2a\u5c42\u677fR\u710a\u63a5";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.Error.WriteLine("Usage: Normalize16029V43ShelfBands.exe <assembly.SLDASM> <left-bands-csv> <right-bands-csv> <tolerance-mm> <out-json>");
                return 2;
            }

            var result = new Result
            {
                AssemblyPath = Path.GetFullPath(args[0]),
                LeftExpectedBandsMm = ParseBands(args[1]),
                RightExpectedBandsMm = ParseBands(args[2]),
                ToleranceMm = ParsePositiveDouble(args[3], 30.0),
                OutJson = Path.GetFullPath(args[4]),
            };

            ISldWorks sw = null;
            ModelDoc2 model = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(result.OutJson));
                result.AssemblyExists = File.Exists(result.AssemblyPath);
                if (!result.AssemblyExists || result.LeftExpectedBandsMm.Count == 0 || result.RightExpectedBandsMm.Count == 0)
                {
                    result.Error = "assembly or expected shelf bands missing";
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
                model = TryValue(() => sw.OpenDoc6(
                    result.AssemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2, null);
                result.Opened = model != null;
                result.OpenErrors = errors;
                result.OpenWarnings = warnings;
                var asm = model as AssemblyDoc;
                if (model == null || asm == null)
                {
                    result.Error = "open assembly failed";
                    WriteJson(result.OutJson, result);
                    return 4;
                }

                Try(() => asm.ResolveAllLightWeightComponents(false));
                var targets = new List<Target>();
                foreach (Component2 component in AsComponents(TryValue(() => asm.GetComponents(true), null)))
                {
                    string name = TryValue(() => component.Name2, "");
                    string path = TryValue(() => component.GetPathName(), "");
                    string text = name + " " + path;
                    string side = text.IndexOf(LeftShelfText, StringComparison.OrdinalIgnoreCase) >= 0 ? "L" :
                        text.IndexOf(RightShelfText, StringComparison.OrdinalIgnoreCase) >= 0 ? "R" : "";
                    if (string.IsNullOrEmpty(side)) continue;

                    result.SeenCount++;
                    BoxInfo box = ToBoxInfo(TryValue(() => component.GetBox(false, false), null));
                    var item = new Item { Name = name, Path = path, Side = side, Box = box };
                    if (!box.Valid)
                    {
                        item.Decision = "invalid_bbox_blocked";
                        result.InvalidBoxCount++;
                        result.Items.Add(item);
                        continue;
                    }

                    item.YMidMm = box.YMidMm;
                    List<double> expected = side == "L" ? result.LeftExpectedBandsMm : result.RightExpectedBandsMm;
                    item.NearestBoundaryYmm = expected.OrderBy(value => Math.Abs(value - item.YMidMm)).First();
                    item.DistanceToBoundaryMm = Math.Abs(item.YMidMm - item.NearestBoundaryYmm);
                    item.Remove = item.DistanceToBoundaryMm > result.ToleranceMm;
                    item.Decision = item.Remove ? "remove_historical_shelf_band" : "keep_requested_shelf_band";
                    result.Items.Add(item);
                    if (item.Remove) targets.Add(new Target { Component = component, Item = item });
                    else result.KeptCount++;
                }

                result.RemoveCandidateCount = targets.Count;
                Try(() => model.ClearSelection2(true));
                foreach (Target target in targets)
                {
                    target.Item.Selected = TryValue(() => target.Component.Select4(true, null, false), false);
                    if (target.Item.Selected) result.SelectedCount++;
                }
                if (result.SelectedCount > 0)
                {
                    result.DeleteInvoked = true;
                    Try(() => model.EditDelete());
                    Try(() => model.ClearSelection2(true));
                }

                result.RemainingUnexpectedCount = CountUnexpected(asm, result.LeftExpectedBandsMm, result.RightExpectedBandsMm, result.ToleranceMm);
                result.RemovedCount = Math.Max(0, result.RemoveCandidateCount - result.RemainingUnexpectedCount);
                result.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                int saveErrors = 0;
                int saveWarnings = 0;
                result.Saved = TryValue(() => model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings), false);
                result.SaveErrors = saveErrors;
                result.SaveWarnings = saveWarnings;
                result.Success = result.InvalidBoxCount == 0 &&
                    result.SelectedCount == result.RemoveCandidateCount &&
                    result.RemainingUnexpectedCount == 0 &&
                    result.Rebuilt &&
                    result.Saved;
                if (!result.Success) result.Error = "shelf-band normalization validation failed";
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

        private static int CountUnexpected(AssemblyDoc asm, List<double> leftExpected, List<double> rightExpected, double tolerance)
        {
            int count = 0;
            foreach (Component2 component in AsComponents(TryValue(() => asm.GetComponents(true), null)))
            {
                string text = TryValue(() => component.Name2, "") + " " + TryValue(() => component.GetPathName(), "");
                List<double> expected = text.IndexOf(LeftShelfText, StringComparison.OrdinalIgnoreCase) >= 0 ? leftExpected :
                    text.IndexOf(RightShelfText, StringComparison.OrdinalIgnoreCase) >= 0 ? rightExpected : null;
                if (expected == null) continue;
                BoxInfo box = ToBoxInfo(TryValue(() => component.GetBox(false, false), null));
                if (!box.Valid || expected.All(value => Math.Abs(value - box.YMidMm) > tolerance)) count++;
            }
            return count;
        }

        private static BoxInfo ToBoxInfo(object raw)
        {
            Array values = raw as Array;
            if (values == null || values.Length < 6) return new BoxInfo();
            return new BoxInfo
            {
                Valid = true,
                YMinMm = Convert.ToDouble(values.GetValue(1), CultureInfo.InvariantCulture) * 1000.0,
                YMaxMm = Convert.ToDouble(values.GetValue(4), CultureInfo.InvariantCulture) * 1000.0,
            };
        }

        private static List<double> ParseBands(string value)
        {
            return (value ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => ParsePositiveDouble(item, -1))
                .Where(item => item >= 0)
                .Distinct()
                .OrderBy(item => item)
                .ToList();
        }

        private static double ParsePositiveDouble(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed >= 0 ? parsed : fallback;
        }

        private static IEnumerable<Component2> AsComponents(object raw)
        {
            Array array = raw as Array;
            if (array == null) yield break;
            foreach (object value in array)
            {
                Component2 component = value as Component2;
                if (component != null) yield return component;
            }
        }

        private static ISldWorks GetOrCreateSolidWorks()
        {
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
                try { object active = Marshal.GetActiveObject(progId); if (active != null) return active as ISldWorks; } catch { }
                try { Type type = Type.GetTypeFromProgID(progId); if (type != null) return Activator.CreateInstance(type) as ISldWorks; } catch { }
            }
            return null;
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        private static T TryValue<T>(Func<T> action, T fallback)
        {
            try { T value = action(); return value == null ? fallback : value; } catch { return fallback; }
        }

        private static void WriteJson(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson(result), new UTF8Encoding(false));
        }

        private static string ToJson(Result result)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "schema", "winnsen.locker16029.normalize_v43_shelf_bands.v1").Append(",");
            Field(sb, "assembly_path", result.AssemblyPath).Append(",");
            Field(sb, "assembly_exists", result.AssemblyExists).Append(",");
            Field(sb, "solidworks_process_id", result.SolidWorksProcessId).Append(",");
            Field(sb, "opened", result.Opened).Append(",");
            Field(sb, "open_errors", result.OpenErrors).Append(",");
            Field(sb, "open_warnings", result.OpenWarnings).Append(",");
            NumberArray(sb, "left_expected_bands_mm", result.LeftExpectedBandsMm).Append(",");
            NumberArray(sb, "right_expected_bands_mm", result.RightExpectedBandsMm).Append(",");
            Field(sb, "tolerance_mm", result.ToleranceMm).Append(",");
            Field(sb, "seen_count", result.SeenCount).Append(",");
            Field(sb, "kept_count", result.KeptCount).Append(",");
            Field(sb, "invalid_box_count", result.InvalidBoxCount).Append(",");
            Field(sb, "remove_candidate_count", result.RemoveCandidateCount).Append(",");
            Field(sb, "selected_count", result.SelectedCount).Append(",");
            Field(sb, "delete_invoked", result.DeleteInvoked).Append(",");
            Field(sb, "removed_count", result.RemovedCount).Append(",");
            Field(sb, "remaining_unexpected_count", result.RemainingUnexpectedCount).Append(",");
            Field(sb, "rebuilt", result.Rebuilt).Append(",");
            Field(sb, "saved", result.Saved).Append(",");
            Field(sb, "save_errors", result.SaveErrors).Append(",");
            Field(sb, "save_warnings", result.SaveWarnings).Append(",");
            Field(sb, "success", result.Success).Append(",");
            Field(sb, "error", result.Error).Append(",");
            sb.Append("\"items\":[");
            for (int index = 0; index < result.Items.Count; index++)
            {
                if (index > 0) sb.Append(",");
                ItemJson(sb, result.Items[index]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void ItemJson(StringBuilder sb, Item item)
        {
            sb.Append("{");
            Field(sb, "name", item.Name).Append(",");
            Field(sb, "path", item.Path).Append(",");
            Field(sb, "side", item.Side).Append(",");
            Field(sb, "y_mid_mm", item.YMidMm).Append(",");
            Field(sb, "nearest_boundary_y_mm", item.NearestBoundaryYmm).Append(",");
            Field(sb, "distance_to_boundary_mm", item.DistanceToBoundaryMm).Append(",");
            Field(sb, "decision", item.Decision).Append(",");
            Field(sb, "remove", item.Remove).Append(",");
            Field(sb, "selected", item.Selected);
            sb.Append("}");
        }

        private static StringBuilder NumberArray(StringBuilder sb, string name, IEnumerable<double> values)
        {
            sb.Append("\"").Append(Escape(name)).Append("\":[");
            bool first = true;
            foreach (double value in values)
            {
                if (!first) sb.Append(",");
                sb.Append(Math.Round(value, 6).ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            return sb.Append("]");
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

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class Result
        {
            public string AssemblyPath = "";
            public string OutJson = "";
            public bool AssemblyExists;
            public List<double> LeftExpectedBandsMm = new List<double>();
            public List<double> RightExpectedBandsMm = new List<double>();
            public double ToleranceMm;
            public int SolidWorksProcessId;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public int SeenCount;
            public int KeptCount;
            public int InvalidBoxCount;
            public int RemoveCandidateCount;
            public int SelectedCount;
            public bool DeleteInvoked;
            public int RemovedCount;
            public int RemainingUnexpectedCount;
            public bool Rebuilt;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public bool Success;
            public string Error = "";
            public readonly List<Item> Items = new List<Item>();
        }

        private sealed class Target
        {
            public Component2 Component;
            public Item Item;
        }

        private sealed class Item
        {
            public string Name = "";
            public string Path = "";
            public string Side = "";
            public BoxInfo Box = new BoxInfo();
            public double YMidMm;
            public double NearestBoundaryYmm;
            public double DistanceToBoundaryMm;
            public string Decision = "";
            public bool Remove;
            public bool Selected;
        }

        private sealed class BoxInfo
        {
            public bool Valid;
            public double YMinMm;
            public double YMaxMm;
            public double YMidMm { get { return (YMinMm + YMaxMm) / 2.0; } }
        }
    }
}
