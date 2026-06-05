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
    internal static class PackAndGoAssembly
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: PackAndGoAssembly.exe <assembly.SLDASM> <out-dir> <out-json>");
                return 2;
            }

            string assemblyPath = Path.GetFullPath(args[0]);
            string outDir = Path.GetFullPath(args[1]);
            string outJson = Path.GetFullPath(args[2]);
            var result = new PackResult
            {
                AssemblyPath = assemblyPath,
                OutDir = outDir,
                InputExists = File.Exists(assemblyPath),
            };
            bool restoreUpdateComponentNames = false;
            bool previousUpdateComponentNames = false;

            try
            {
                Directory.CreateDirectory(outDir);
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));

                ISldWorks sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 3;
                }

                result.SolidWorksProcessId = TryValue(() => sw.GetProcessID(), 0);
                sw.Visible = true;
                try
                {
                    previousUpdateComponentNames = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames);
                    restoreUpdateComponentNames = true;
                    sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, true);
                }
                catch { }
                int errors = 0;
                int warnings = 0;
                ModelDoc2 model = sw.OpenDoc6(
                    assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2;
                if (model == null)
                {
                    model = sw.OpenDoc(assemblyPath, (int)swDocumentTypes_e.swDocASSEMBLY) as ModelDoc2;
                }
                result.Opened = model != null;
                result.OpenErrors = errors;
                result.OpenWarnings = warnings;
                if (model == null)
                {
                    result.Error = "OpenDoc failed";
                    if (restoreUpdateComponentNames)
                    {
                        Try(() => sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, previousUpdateComponentNames));
                    }
                    WriteJson(outJson, result);
                    return 4;
                }

                ModelDocExtension ext = model.Extension as ModelDocExtension;
                PackAndGo packAndGo = ext == null ? null : ext.GetPackAndGo() as PackAndGo;
                result.PackAndGoCreated = packAndGo != null;
                if (packAndGo == null)
                {
                    result.Error = "GetPackAndGo returned null";
                    Close(sw, model);
                    if (restoreUpdateComponentNames)
                    {
                        Try(() => sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, previousUpdateComponentNames));
                    }
                    WriteJson(outJson, result);
                    return 5;
                }

                Try(() => { packAndGo.IncludeDrawings = false; });
                Try(() => { packAndGo.IncludeSimulationResults = false; });
                Try(() => { packAndGo.IncludeSuppressed = true; });
                Try(() => { packAndGo.IncludeToolboxComponents = true; });
                Try(() => { packAndGo.FlattenToSingleFolder = true; });

                result.DocumentCount = TryValue(() => packAndGo.GetDocumentNamesCount(), -1);
                result.SetSaveToName = TryValue(() => packAndGo.SetSaveToName(true, outDir), false);
                if (result.SetSaveToName)
                {
                    ApplyChineseSaveNames(packAndGo, outDir, result);
                }
                object saveResult = ext.SavePackAndGo(packAndGo);
                result.SavePackAndGoReturned = saveResult != null;
                result.SavePackAndGoResultType = saveResult == null ? "" : saveResult.GetType().FullName;
                result.SavePackAndGoResultText = DescribeObject(saveResult);
                result.Inventory = Inventory(outDir);
                result.Closed = CloseAll(sw);
                if (restoreUpdateComponentNames)
                {
                    Try(() => sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, previousUpdateComponentNames));
                }

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Opened && result.PackAndGoCreated && result.Inventory.FileCount > 0 ? 0 : 6;
            }
            catch (Exception ex)
            {
                try
                {
                    ISldWorks active = GetOrCreateSolidWorks();
                    if (active != null && restoreUpdateComponentNames)
                    {
                        Try(() => active.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, previousUpdateComponentNames));
                    }
                }
                catch { }
                result.Error = SafeExceptionText(ex);
                result.Inventory = Inventory(outDir);
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
        }

        private static void ApplyChineseSaveNames(PackAndGo packAndGo, string outDir, PackResult result)
        {
            object saveToNamesObject = null;
            object documentStatusObject = null;
            result.GotDocumentSaveToNames = TryValue(
                () => packAndGo.GetDocumentSaveToNames(out saveToNamesObject, out documentStatusObject),
                false);
            List<string> saveToNames = ToStringList(saveToNamesObject);
            if (!result.GotDocumentSaveToNames || saveToNames.Count == 0) return;

            var finalNames = new string[saveToNames.Count];
            var desiredNames = new string[saveToNames.Count];
            var changed = new bool[saveToNames.Count];
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < saveToNames.Count; i++)
            {
                string current = saveToNames[i] ?? "";
                string directory = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(directory)) directory = outDir;
                string desiredLeaf = ChineseLeafName(Path.GetFileName(current));
                if (string.IsNullOrWhiteSpace(desiredLeaf)) desiredLeaf = Path.GetFileName(current);
                desiredNames[i] = Path.Combine(directory, desiredLeaf);
                changed[i] = !string.Equals(Path.GetFileName(current), desiredLeaf, StringComparison.OrdinalIgnoreCase);
            }

            for (int i = 0; i < saveToNames.Count; i++)
            {
                if (changed[i]) continue;
                finalNames[i] = saveToNames[i];
                if (!string.IsNullOrWhiteSpace(finalNames[i])) used.Add(finalNames[i]);
            }

            for (int i = 0; i < saveToNames.Count; i++)
            {
                if (!changed[i]) continue;
                string finalName = UniquePath(desiredNames[i], used);
                finalNames[i] = finalName;
                used.Add(finalName);
                result.ChineseSaveNameMap.Add(new SaveNameMapItem
                {
                    From = saveToNames[i],
                    To = finalName,
                });
            }

            result.ChineseSaveNameMapCount = result.ChineseSaveNameMap.Count;
            if (result.ChineseSaveNameMapCount > 0)
            {
                result.SetDocumentSaveToNames = TryValue(() => packAndGo.SetDocumentSaveToNames(finalNames), false);
            }
        }

        private static List<string> ToStringList(object raw)
        {
            var result = new List<string>();
            if (raw == null) return result;
            Array array = raw as Array;
            if (array == null)
            {
                result.Add(Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "");
                return result;
            }
            foreach (object item in array)
            {
                result.Add(Convert.ToString(item, CultureInfo.InvariantCulture) ?? "");
            }
            return result;
        }

        private static string UniquePath(string preferred, HashSet<string> used)
        {
            if (!used.Contains(preferred)) return preferred;
            string directory = Path.GetDirectoryName(preferred);
            string leaf = Path.GetFileNameWithoutExtension(preferred);
            string extension = Path.GetExtension(preferred);
            for (int index = 2; index < 1000; index++)
            {
                string candidate = Path.Combine(directory ?? "", leaf + "_" + index.ToString(CultureInfo.InvariantCulture) + extension);
                if (!used.Contains(candidate)) return candidate;
            }
            return preferred;
        }

        private static string ChineseLeafName(string leaf)
        {
            string name = Path.GetFileNameWithoutExtension(leaf ?? "");
            string extension = Path.GetExtension(leaf ?? "");
            if (string.IsNullOrWhiteSpace(name)) return leaf ?? "";

            Match ordinaryDoor = Regex.Match(name, @"^gold_ordinary_door_([0-9]+(?:p[0-9]+)?)_12_W\d+_(left|right)", RegexOptions.IgnoreCase);
            if (ordinaryDoor.Success)
            {
                return "储物柜门" + RatioLabel(ordinaryDoor.Groups[1].Value) + "╱12装配_" + SideLabel(ordinaryDoor.Groups[2].Value) + extension;
            }

            Match doorWeld = Regex.Match(name, @"^gold_door_weld_([0-9]+(?:p[0-9]+)?)_12_W\d+_(left|right)", RegexOptions.IgnoreCase);
            if (doorWeld.Success)
            {
                return "储物柜门" + RatioLabel(doorWeld.Groups[1].Value) + "╱12焊接_" + SideLabel(doorWeld.Groups[2].Value) + extension;
            }

            Match source14Door = Regex.Match(name, @"^review_14door_source_ordinary_door_W\d+_H[0-9]+(?:p[0-9]+)?_(left|right)", RegexOptions.IgnoreCase);
            if (source14Door.Success)
            {
                return "储物柜门14门源钣金装配_" + SideLabel(source14Door.Groups[1].Value) + extension;
            }

            Match source14Weld = Regex.Match(name, @"^review_14door_source_door_weld_W\d+_H[0-9]+(?:p[0-9]+)?_(left|right)", RegexOptions.IgnoreCase);
            if (source14Weld.Success)
            {
                return "储物柜门14门源钣金焊接_" + SideLabel(source14Weld.Groups[1].Value) + extension;
            }

            Match generatedDoor = Regex.Match(name, @"^review_single_ordinary_door_W\d+_H([0-9]+(?:p[0-9]+)?)_(left|right)", RegexOptions.IgnoreCase);
            if (generatedDoor.Success)
            {
                return "储物柜门" + DoorRatioTextFromHeightToken(generatedDoor.Groups[1].Value) + "装配_" + SideLabel(generatedDoor.Groups[2].Value) + extension;
            }

            Match generatedWeld = Regex.Match(name, @"^review_single_door_weld_W\d+_H([0-9]+(?:p[0-9]+)?)_(left|right)", RegexOptions.IgnoreCase);
            if (generatedWeld.Success)
            {
                return "储物柜门" + DoorRatioTextFromHeightToken(generatedWeld.Groups[1].Value) + "焊接_" + SideLabel(generatedWeld.Groups[2].Value) + extension;
            }

            Match generatedPanel = Regex.Match(name, @"^review_single_door_panel_W\d+_H([0-9]+(?:p[0-9]+)?)_sheetmetal", RegexOptions.IgnoreCase);
            if (generatedPanel.Success)
            {
                return "储物柜门板" + DoorRatioTextFromHeightToken(generatedPanel.Groups[1].Value) + extension;
            }

            Match generated14DoorPanel = Regex.Match(name, @"^review_14door_source_panel_W\d+_H[0-9]+(?:p[0-9]+)?_sheetmetal", RegexOptions.IgnoreCase);
            if (generated14DoorPanel.Success)
            {
                return "储物柜门板14门源钣金" + extension;
            }

            Match generatedStiffener = Regex.Match(name, @"^review_single_door_stiffener_L([0-9]+(?:p[0-9]+)?)_sheetmetal", RegexOptions.IgnoreCase);
            if (generatedStiffener.Success)
            {
                return "柜门加强筋" + DoorRatioTextFromStiffenerToken(generatedStiffener.Groups[1].Value) + extension;
            }

            Match generated14DoorStiffener = Regex.Match(name, @"^review_14door_source_stiffener_L[0-9]+(?:p[0-9]+)?_sheetmetal", RegexOptions.IgnoreCase);
            if (generated14DoorStiffener.Success)
            {
                return "柜门加强筋14门源钣金" + extension;
            }

            Match rightPanel = Regex.Match(name, @"^right_mirror_ordinary_panel_([0-9]+(?:p[0-9]+)?)_12_W\d+", RegexOptions.IgnoreCase);
            if (rightPanel.Success)
            {
                return "储物柜门板" + RatioLabel(rightPanel.Groups[1].Value) + "╱12_右" + extension;
            }

            Match rib = Regex.Match(name, @"^rib_([0-9]+(?:p[0-9]+)?)_12$", RegexOptions.IgnoreCase);
            if (rib.Success)
            {
                return "柜门加强筋" + RatioLabel(rib.Groups[1].Value) + "╱12" + extension;
            }

            Match topLatch = Regex.Match(name, @"^top_latch_from_bottom_mirror_([0-9]+(?:p[0-9]+)?)_12_W\d+_(left|right)", RegexOptions.IgnoreCase);
            if (topLatch.Success)
            {
                return "插销固定板" + RatioLabel(topLatch.Groups[1].Value) + "╱12_" + SideLabel(topLatch.Groups[2].Value) + extension;
            }

            Match generatedTopLatch = Regex.Match(name, @"^top_latch_from_bottom_mirror_W\d+_H([0-9]+(?:p[0-9]+)?)_(left|right)", RegexOptions.IgnoreCase);
            if (generatedTopLatch.Success)
            {
                return "插销固定板" + DoorRatioTextFromHeightToken(generatedTopLatch.Groups[1].Value) + "_" + SideLabel(generatedTopLatch.Groups[2].Value) + extension;
            }

            if (Regex.IsMatch(name, @"^candidate_16029_740W_gold_electronics_module", RegexOptions.IgnoreCase)) return "电控模块" + extension;
            if (Regex.IsMatch(name, @"^electric_lock_body_zja_s500", RegexOptions.IgnoreCase)) return "电控锁体ZJA-S500" + extension;
            if (Regex.IsMatch(name, @"^electric_lock_hook_zja_s500", RegexOptions.IgnoreCase)) return "电控U型锁钩ZJA-S500" + extension;
            if (Regex.IsMatch(name, @"^插销固定板_SW2020_from_gold_step", RegexOptions.IgnoreCase)) return "插销固定板" + extension;
            if (Regex.IsMatch(name, @"^U型锁钩垫板_SW2020_from_gold_step", RegexOptions.IgnoreCase)) return "U型锁钩垫板" + extension;
            if (Regex.IsMatch(name, @"^开口挡圈5_SW2020_from_gold_assembly_step", RegexOptions.IgnoreCase)) return "开口挡圈5" + extension;

            return leaf ?? "";
        }

        private static string RatioLabel(string value)
        {
            return (value ?? "").Replace("p", ".");
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

        private static string SideLabel(string value)
        {
            return string.Equals(value, "right", StringComparison.OrdinalIgnoreCase) ? "右" : "左";
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

        private static bool CloseAll(ISldWorks sw)
        {
            return TryValue(() =>
            {
                sw.CloseAllDocuments(true);
                return true;
            }, false);
        }

        private static InventoryResult Inventory(string outDir)
        {
            var result = new InventoryResult();
            if (!Directory.Exists(outDir)) return result;
            foreach (string path in Directory.GetFiles(outDir, "*", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(path);
                string ext = info.Extension.ToLowerInvariant();
                double sizeMb = Math.Round(info.Length / 1048576.0, 3);
                if (ext == ".sldasm") result.SldasmCount++;
                else if (ext == ".sldprt") result.SldprtCount++;
                else
                {
                    result.OtherCount++;
                    continue;
                }
                result.FileCount++;
                result.TotalMb = Math.Round(result.TotalMb + sizeMb, 3);
                result.Files.Add(new FileInventory { Path = path, SizeMb = sizeMb });
            }
            return result;
        }

        private static string DescribeObject(object value)
        {
            if (value == null) return "";
            Array array = value as Array;
            if (array == null) return TryValue(() => Convert.ToString(value, CultureInfo.InvariantCulture), value.GetType().FullName);
            var parts = new List<string>();
            foreach (object item in array)
            {
                parts.Add(TryValue(() => Convert.ToString(item, CultureInfo.InvariantCulture), item == null ? "" : item.GetType().FullName));
            }
            return string.Join(",", parts.ToArray());
        }

        private static string SafeExceptionText(Exception ex)
        {
            if (ex == null) return "";
            try { return ex.ToString(); }
            catch
            {
                try
                {
                    return ex.GetType().FullName + ": " + (ex.Message ?? "");
                }
                catch { return "unprintable exception"; }
            }
        }

        private static ISldWorks GetOrCreateSolidWorks()
        {
            try { return Marshal.GetActiveObject("SldWorks.Application") as ISldWorks; }
            catch { }
            try
            {
                Type t = Type.GetTypeFromProgID("SldWorks.Application");
                return t == null ? null : Activator.CreateInstance(t) as ISldWorks;
            }
            catch { return null; }
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        private static T TryValue<T>(Func<T> action, T fallback)
        {
            try { return action(); } catch { return fallback; }
        }

        private static void WriteJson(string path, PackResult result)
        {
            File.WriteAllText(path, result.ToJson(), Encoding.UTF8);
        }

        private static string Json(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        private sealed class PackResult
        {
            public string AssemblyPath = "";
            public string OutDir = "";
            public bool InputExists;
            public int SolidWorksProcessId;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public bool PackAndGoCreated;
            public int DocumentCount;
            public bool SetSaveToName;
            public bool GotDocumentSaveToNames;
            public bool SetDocumentSaveToNames;
            public int ChineseSaveNameMapCount;
            public readonly List<SaveNameMapItem> ChineseSaveNameMap = new List<SaveNameMapItem>();
            public bool SavePackAndGoReturned;
            public string SavePackAndGoResultType = "";
            public string SavePackAndGoResultText = "";
            public InventoryResult Inventory = new InventoryResult();
            public bool Closed;
            public string Error = "";

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"assembly_path\":").Append(Json(AssemblyPath)).Append(",");
                sb.Append("\"out_dir\":").Append(Json(OutDir)).Append(",");
                sb.Append("\"input_exists\":").Append(InputExists ? "true" : "false").Append(",");
                sb.Append("\"solidworks_process_id\":").Append(SolidWorksProcessId).Append(",");
                sb.Append("\"opened\":").Append(Opened ? "true" : "false").Append(",");
                sb.Append("\"open_errors\":").Append(OpenErrors).Append(",");
                sb.Append("\"open_warnings\":").Append(OpenWarnings).Append(",");
                sb.Append("\"pack_and_go_created\":").Append(PackAndGoCreated ? "true" : "false").Append(",");
                sb.Append("\"document_count\":").Append(DocumentCount).Append(",");
                sb.Append("\"set_save_to_name\":").Append(SetSaveToName ? "true" : "false").Append(",");
                sb.Append("\"got_document_save_to_names\":").Append(GotDocumentSaveToNames ? "true" : "false").Append(",");
                sb.Append("\"set_document_save_to_names\":").Append(SetDocumentSaveToNames ? "true" : "false").Append(",");
                sb.Append("\"chinese_save_name_map_count\":").Append(ChineseSaveNameMapCount).Append(",");
                sb.Append("\"chinese_save_name_map\":[");
                for (int i = 0; i < ChineseSaveNameMap.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(ChineseSaveNameMap[i].ToJson());
                }
                sb.Append("],");
                sb.Append("\"save_pack_and_go_returned\":").Append(SavePackAndGoReturned ? "true" : "false").Append(",");
                sb.Append("\"save_pack_and_go_result_type\":").Append(Json(SavePackAndGoResultType)).Append(",");
                sb.Append("\"save_pack_and_go_result_text\":").Append(Json(SavePackAndGoResultText)).Append(",");
                sb.Append("\"inventory\":").Append(Inventory.ToJson()).Append(",");
                sb.Append("\"closed\":").Append(Closed ? "true" : "false").Append(",");
                sb.Append("\"error\":").Append(Json(Error));
                sb.Append("}");
                return sb.ToString();
            }
        }

        private sealed class SaveNameMapItem
        {
            public string From = "";
            public string To = "";

            public string ToJson()
            {
                return "{\"from\":" + Json(From) + ",\"to\":" + Json(To) + "}";
            }
        }

        private sealed class InventoryResult
        {
            public int FileCount;
            public int SldasmCount;
            public int SldprtCount;
            public int OtherCount;
            public double TotalMb;
            public readonly List<FileInventory> Files = new List<FileInventory>();

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"file_count\":").Append(FileCount).Append(",");
                sb.Append("\"sldasm_count\":").Append(SldasmCount).Append(",");
                sb.Append("\"sldprt_count\":").Append(SldprtCount).Append(",");
                sb.Append("\"other_count\":").Append(OtherCount).Append(",");
                sb.Append("\"total_mb\":").Append(TotalMb.ToString(CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"files\":[");
                for (int i = 0; i < Files.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Files[i].ToJson());
                }
                sb.Append("]}");
                return sb.ToString();
            }
        }

        private sealed class FileInventory
        {
            public string Path = "";
            public double SizeMb;

            public string ToJson()
            {
                return "{\"path\":" + Json(Path) + ",\"size_mb\":" + SizeMb.ToString(CultureInfo.InvariantCulture) + "}";
            }
        }
    }
}
