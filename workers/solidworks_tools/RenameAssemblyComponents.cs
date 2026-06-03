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
    internal static class RenameAssemblyComponents
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: RenameAssemblyComponents.exe <assembly.SLDASM> <out-json>");
                return 2;
            }

            string asmPath = Path.GetFullPath(args[0]);
            string outJson = Path.GetFullPath(args[1]);
            var result = new RenameResult { AssemblyPath = asmPath, Exists = File.Exists(asmPath) };
            ISldWorks sw = null;
            bool restoreUpdateComponentNames = false;
            bool previousUpdateComponentNames = false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));
                if (!result.Exists)
                {
                    result.Error = "assembly does not exist";
                    WriteJson(outJson, result);
                    return 2;
                }

                sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 3;
                }

                sw.Visible = true;
                try
                {
                    previousUpdateComponentNames = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames);
                    restoreUpdateComponentNames = true;
                    sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, false);
                }
                catch { }
                int errors = 0;
                int warnings = 0;
                ModelDoc2 model = sw.OpenDoc6(
                    asmPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2;
                if (model == null)
                {
                    model = sw.OpenDoc(asmPath, (int)swDocumentTypes_e.swDocASSEMBLY) as ModelDoc2;
                }

                result.Opened = model != null;
                result.OpenErrors = errors;
                result.OpenWarnings = warnings;
                if (model == null)
                {
                    result.Error = "failed to open assembly";
                    WriteJson(outJson, result);
                    return 4;
                }

                AssemblyDoc asm = model as AssemblyDoc;
                if (asm == null)
                {
                    result.Error = "opened document is not an assembly";
                    WriteJson(outJson, result);
                    return 5;
                }

                Try(() => asm.ResolveAllLightWeightComponents(false));
                Configuration cfg = TryValue(() => model.GetActiveConfiguration() as Configuration, null);
                Component2 root = cfg == null ? null : TryValue(() => cfg.GetRootComponent3(true), null);
                if (root != null)
                {
                    foreach (Component2 child in AsComponents(TryValue(() => root.GetChildren(), null)))
                    {
                        RenameRecursive(result, child);
                    }
                }
                if (result.SeenCount == 0)
                {
                    foreach (Component2 child in AsComponents(TryValue(() => asm.GetComponents(false), null)))
                    {
                        RenameOne(result, child);
                    }
                }

                result.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                int saveErrors = 0;
                int saveWarnings = 0;
                result.Saved = TryValue(
                    () => model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings),
                    false);
                result.SaveErrors = saveErrors;
                result.SaveWarnings = saveWarnings;
                if (restoreUpdateComponentNames)
                {
                    Try(() => sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, previousUpdateComponentNames));
                }
                Try(() => sw.CloseDoc(model.GetTitle()));

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Opened && result.Saved ? 0 : 6;
            }
            catch (Exception ex)
            {
                if (sw != null && restoreUpdateComponentNames)
                {
                    Try(() => sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, previousUpdateComponentNames));
                }
                result.Error = ex.ToString();
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 1;
            }
        }

        private static void RenameRecursive(RenameResult result, Component2 component)
        {
            RenameOne(result, component);
            foreach (Component2 child in AsComponents(TryValue(() => component.GetChildren(), null)))
            {
                RenameRecursive(result, child);
            }
        }

        private static void RenameOne(RenameResult result, Component2 component)
        {
            if (component == null) return;
            result.SeenCount++;
            string current = TryValue(() => component.Name2, "");
            string path = TryValue(() => component.GetPathName(), "");
            string desired = DesiredName(current, path);
            if (string.IsNullOrWhiteSpace(desired)) return;
            if (string.Equals(StripInstance(LeafComponentName(current)), desired, StringComparison.Ordinal)) return;

            var item = new RenameItem
            {
                OldName = current,
                Path = path,
                NewName = desired,
            };
            result.Renames.Add(item);
            try
            {
                component.Name2 = desired;
                item.Assigned = true;
                result.AssignedCount++;
                item.AfterName = TryValue(() => component.Name2, "");
                if (string.Equals(StripInstance(LeafComponentName(item.AfterName)), desired, StringComparison.Ordinal))
                {
                    item.Applied = true;
                    item.Verified = true;
                    result.VerifiedCount++;
                    result.RenamedCount++;
                }
                else
                {
                    item.Error = "Name2 assignment did not read back as the requested component name";
                }
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
            }
        }

        private static string DesiredName(string componentName, string path)
        {
            string leafName = LeafComponentName(componentName);
            string text = leafName + "\n" + Path.GetFileNameWithoutExtension(path ?? "");

            Match door = Regex.Match(text, @"gold_ordinary_door_(\d+)_12_W\d+_(left|right)", RegexOptions.IgnoreCase);
            if (door.Success) return "储物柜门" + door.Groups[1].Value + "╱12装配_" + SideLabel(door.Groups[2].Value);

            Match weld = Regex.Match(text, @"gold_door_weld_(\d+)_12_W\d+_(left|right)", RegexOptions.IgnoreCase);
            if (weld.Success) return "储物柜门" + weld.Groups[1].Value + "╱12焊接_" + SideLabel(weld.Groups[2].Value);

            Match generatedDoor = Regex.Match(text, @"review_single_ordinary_door_W\d+_H([0-9]+(?:p[0-9]+)?)_(left|right)", RegexOptions.IgnoreCase);
            if (generatedDoor.Success) return "储物柜门" + DoorRatioTextFromHeightToken(generatedDoor.Groups[1].Value) + "装配_" + SideLabel(generatedDoor.Groups[2].Value);

            Match generatedWeld = Regex.Match(text, @"review_single_door_weld_W\d+_H([0-9]+(?:p[0-9]+)?)_(left|right)", RegexOptions.IgnoreCase);
            if (generatedWeld.Success) return "储物柜门" + DoorRatioTextFromHeightToken(generatedWeld.Groups[1].Value) + "焊接_" + SideLabel(generatedWeld.Groups[2].Value);

            Match generatedPanel = Regex.Match(text, @"review_single_door_panel_W\d+_H([0-9]+(?:p[0-9]+)?)_sheetmetal", RegexOptions.IgnoreCase);
            if (generatedPanel.Success) return "储物柜门板" + DoorRatioTextFromHeightToken(generatedPanel.Groups[1].Value);

            Match generatedStiffener = Regex.Match(text, @"review_single_door_stiffener_L([0-9]+(?:p[0-9]+)?)_sheetmetal", RegexOptions.IgnoreCase);
            if (generatedStiffener.Success) return "柜门加强筋" + DoorRatioTextFromStiffenerToken(generatedStiffener.Groups[1].Value);

            Match generatedTopLatch = Regex.Match(text, @"top_latch_from_bottom_mirror_W\d+_H([0-9]+(?:p[0-9]+)?)_(left|right)", RegexOptions.IgnoreCase);
            if (generatedTopLatch.Success) return "插销固定板" + DoorRatioTextFromHeightToken(generatedTopLatch.Groups[1].Value) + "_" + SideLabel(generatedTopLatch.Groups[2].Value);

            Match rightPanel = Regex.Match(text, @"right_mirror_ordinary_panel_(\d+)_12_W\d+", RegexOptions.IgnoreCase);
            if (rightPanel.Success) return "储物柜门板" + rightPanel.Groups[1].Value + "╱12_右";

            Match panel = Regex.Match(text, @"储物柜门板(\d+)╱12_W\d+", RegexOptions.IgnoreCase);
            if (panel.Success) return "储物柜门板" + panel.Groups[1].Value + "╱12";

            Match rib = Regex.Match(text, @"rib_(\d+)_12", RegexOptions.IgnoreCase);
            if (rib.Success) return "柜门加强筋" + rib.Groups[1].Value + "╱12";

            if (Regex.IsMatch(text, @"candidate_16029_740W_gold_shell_frame_shelf_only", RegexOptions.IgnoreCase)) return "箱体焊接待拆分";
            if (Regex.IsMatch(text, @"candidate_16029_740W_gold_electronics_module", RegexOptions.IgnoreCase)) return "电控模块";
            if (Regex.IsMatch(text, @"electric_lock_body_zja_s500", RegexOptions.IgnoreCase)) return "电控锁体ZJA-S500";
            if (Regex.IsMatch(text, @"electric_lock_hook_zja_s500", RegexOptions.IgnoreCase)) return "电控U型锁钩ZJA-S500";
            if (Regex.IsMatch(text, @"top_latch_from_bottom_mirror|插销固定板_SW2020", RegexOptions.IgnoreCase)) return "插销固定板";
            if (Regex.IsMatch(text, @"U型锁钩垫板_SW2020", RegexOptions.IgnoreCase)) return "U型锁钩垫板";
            if (Regex.IsMatch(text, @"开口挡圈5_SW2020", RegexOptions.IgnoreCase)) return "开口挡圈 5";

            return "";
        }

        private static string LeafComponentName(string name)
        {
            string value = name ?? "";
            int slash = value.LastIndexOf('/');
            return slash >= 0 && slash + 1 < value.Length ? value.Substring(slash + 1) : value;
        }

        private static string StripInstance(string name)
        {
            return Regex.Replace(name ?? "", @"-\d+$", "");
        }

        private static string SideLabel(string side)
        {
            return string.Equals(side, "right", StringComparison.OrdinalIgnoreCase) ? "右" : "左";
        }

        private static string DoorRatioTextFromHeightToken(string token)
        {
            double heightMm;
            if (!TryParseToken(token, out heightMm)) return "非标";
            return DoorRatioText((heightMm + 7.0) / 152.5);
        }

        private static string DoorRatioTextFromStiffenerToken(string token)
        {
            double lengthMm;
            if (!TryParseToken(token, out lengthMm)) return "非标";
            return DoorRatioText((lengthMm + 17.5) / 152.5);
        }

        private static string DoorRatioText(double units)
        {
            double rounded = Math.Round(units, 3);
            double integer = Math.Round(rounded);
            if (Math.Abs(rounded - integer) < 0.01)
            {
                return integer.ToString("0", CultureInfo.InvariantCulture) + "╱12";
            }
            return rounded.ToString("0.###", CultureInfo.InvariantCulture) + "╱12";
        }

        private static bool TryParseToken(string token, out double value)
        {
            return double.TryParse((token ?? "").Replace("p", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

        private static void WriteJson(string path, RenameResult result)
        {
            File.WriteAllText(path, Json(result), new UTF8Encoding(false));
        }

        private static string Json(RenameResult result)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "assembly_path", result.AssemblyPath).Append(",");
            Field(sb, "exists", result.Exists).Append(",");
            Field(sb, "opened", result.Opened).Append(",");
            Field(sb, "open_errors", result.OpenErrors).Append(",");
            Field(sb, "open_warnings", result.OpenWarnings).Append(",");
            Field(sb, "seen_count", result.SeenCount).Append(",");
            Field(sb, "attempted_count", result.Renames.Count).Append(",");
            Field(sb, "assigned_count", result.AssignedCount).Append(",");
            Field(sb, "verified_count", result.VerifiedCount).Append(",");
            Field(sb, "renamed_count", result.RenamedCount).Append(",");
            Field(sb, "rebuilt", result.Rebuilt).Append(",");
            Field(sb, "saved", result.Saved).Append(",");
            Field(sb, "save_errors", result.SaveErrors).Append(",");
            Field(sb, "save_warnings", result.SaveWarnings).Append(",");
            Field(sb, "error", result.Error).Append(",");
            sb.Append("\"renames\":[");
            for (int i = 0; i < result.Renames.Count; i++)
            {
                if (i > 0) sb.Append(",");
                RenameJson(sb, result.Renames[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void RenameJson(StringBuilder sb, RenameItem item)
        {
            sb.Append("{");
            Field(sb, "old_name", item.OldName).Append(",");
            Field(sb, "new_name", item.NewName).Append(",");
            Field(sb, "after_name", item.AfterName).Append(",");
            Field(sb, "path", item.Path).Append(",");
            Field(sb, "assigned", item.Assigned).Append(",");
            Field(sb, "verified", item.Verified).Append(",");
            Field(sb, "applied", item.Applied).Append(",");
            Field(sb, "error", item.Error);
            sb.Append("}");
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

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class RenameResult
        {
            public string AssemblyPath = "";
            public bool Exists;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public int SeenCount;
            public int AssignedCount;
            public int VerifiedCount;
            public int RenamedCount;
            public bool Rebuilt;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public string Error = "";
            public readonly List<RenameItem> Renames = new List<RenameItem>();
        }

        private sealed class RenameItem
        {
            public string OldName = "";
            public string NewName = "";
            public string AfterName = "";
            public string Path = "";
            public bool Assigned;
            public bool Verified;
            public bool Applied;
            public string Error = "";
        }
    }
}
