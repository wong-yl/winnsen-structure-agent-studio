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

                sw.Visible = true;
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
                object saveResult = ext.SavePackAndGo(packAndGo);
                result.SavePackAndGoReturned = saveResult != null;
                result.SavePackAndGoResultType = saveResult == null ? "" : saveResult.GetType().FullName;
                result.SavePackAndGoResultText = DescribeObject(saveResult);
                result.Inventory = Inventory(outDir);
                result.Closed = Close(sw, model);

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Opened && result.PackAndGoCreated && result.Inventory.FileCount > 0 ? 0 : 6;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                result.Inventory = Inventory(outDir);
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
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
            if (array == null) return Convert.ToString(value, CultureInfo.InvariantCulture);
            var parts = new List<string>();
            foreach (object item in array)
            {
                parts.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
            }
            return string.Join(",", parts.ToArray());
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
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public bool PackAndGoCreated;
            public int DocumentCount;
            public bool SetSaveToName;
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
                sb.Append("\"opened\":").Append(Opened ? "true" : "false").Append(",");
                sb.Append("\"open_errors\":").Append(OpenErrors).Append(",");
                sb.Append("\"open_warnings\":").Append(OpenWarnings).Append(",");
                sb.Append("\"pack_and_go_created\":").Append(PackAndGoCreated ? "true" : "false").Append(",");
                sb.Append("\"document_count\":").Append(DocumentCount).Append(",");
                sb.Append("\"set_save_to_name\":").Append(SetSaveToName ? "true" : "false").Append(",");
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
