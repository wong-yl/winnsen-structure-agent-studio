using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class Add16029CenterMaintenanceSheetMetal
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 7)
            {
                Console.Error.WriteLine("Usage: Add16029CenterMaintenanceSheetMetal.exe <assembly.SLDASM> <source.SLDPRT> <role> <tx-mm> <ty-mm> <tz-mm> <out-json>");
                return 2;
            }

            var result = new Result
            {
                AssemblyPath = Path.GetFullPath(args[0]),
                SourcePath = Path.GetFullPath(args[1]),
                Role = args[2],
                TxMm = ParseDouble(args[3]),
                TyMm = ParseDouble(args[4]),
                TzMm = ParseDouble(args[5]),
                OutJson = Path.GetFullPath(args[6]),
            };

            ISldWorks sw = null;
            ModelDoc2 asmModel = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(result.OutJson));
                result.AssemblyExists = File.Exists(result.AssemblyPath);
                result.SourceExists = File.Exists(result.SourcePath);
                if (!result.AssemblyExists || !result.SourceExists)
                {
                    result.Error = "assembly or maintenance sheet-metal source part missing";
                    WriteJson(result.OutJson, result);
                    return 2;
                }

                string packDir = Path.GetDirectoryName(result.AssemblyPath);
                string localSource = Path.Combine(packDir, SafeFileName(result.Role) + ".SLDPRT");
                File.Copy(result.SourcePath, localSource, true);
                result.LocalSourcePath = localSource;
                result.LocalSourceExists = File.Exists(localSource);
                if (!result.LocalSourceExists)
                {
                    result.Error = "failed to copy maintenance sheet-metal source into Pack-and-Go folder";
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

                AssemblyDoc asm = asmModel as AssemblyDoc;
                MathUtility math = TryValue(() => sw.GetMathUtility() as MathUtility, null);
                if (asm == null || math == null)
                {
                    result.Error = "assembly or MathUtility unavailable";
                    WriteJson(result.OutJson, result);
                    return 5;
                }

                Try(() => asm.ResolveAllLightWeightComponents(false));
                Component2 component = FindExisting(asm, result.Role);
                if (component != null)
                {
                    result.Updated = true;
                }
                else
                {
                    ModelDoc2 partDoc = TryValue(() => sw.OpenDoc6(
                        localSource,
                        (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "",
                        ref errors,
                        ref warnings) as ModelDoc2, null);
                    result.PartOpened = partDoc != null;
                    result.PartOpenErrors = errors;
                    result.PartOpenWarnings = warnings;
                    if (partDoc == null)
                    {
                        result.Error = "open maintenance sheet-metal source part failed";
                        WriteJson(result.OutJson, result);
                        return 6;
                    }

                    string partTitle = TryValue(() => partDoc.GetTitle(), "");
                    try
                    {
                        Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
                        component = TryValue(() => asm.AddComponent5(
                            localSource,
                            (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                            "",
                            false,
                            "",
                            result.TxMm / 1000.0,
                            result.TyMm / 1000.0,
                            result.TzMm / 1000.0) as Component2, null);
                        result.Added = component != null;
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
                        result.Error = "AddComponent5 returned null";
                        WriteJson(result.OutJson, result);
                        return 7;
                    }
                    Try(() => { component.Name2 = result.Role; });
                }

                MathTransform transform = TryValue(() => math.CreateTransform(ToTransformData(result.TxMm, result.TyMm, result.TzMm)) as MathTransform, null);
                result.TransformCreated = transform != null;
                if (transform == null)
                {
                    result.Error = "transform create failed";
                    WriteJson(result.OutJson, result);
                    return 8;
                }
                result.TransformApplied = TryValue(() => component.SetTransformAndSolve2(transform), false);
                result.Name = TryValue(() => component.Name2, "");
                result.Path = TryValue(() => component.GetPathName(), "");
                result.Box = ToBoxInfo(TryValue(() => component.GetBox(false, false), null));
                result.Rebuilt = TryValue(() => asmModel.ForceRebuild3(false), false);
                int saveErrors = 0;
                int saveWarnings = 0;
                result.Saved = TryValue(() => asmModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings), false);
                result.SaveErrors = saveErrors;
                result.SaveWarnings = saveWarnings;
                result.SaveFlagAfter = TryValue(() => asmModel.GetSaveFlag(), true);
                result.Success =
                    (result.Added || result.Updated) &&
                    result.TransformCreated &&
                    result.TransformApplied &&
                    result.Rebuilt &&
                    result.Saved &&
                    !result.SaveFlagAfter &&
                    result.Path.StartsWith(packDir, StringComparison.OrdinalIgnoreCase);
                if (!result.Success && string.IsNullOrWhiteSpace(result.Error)) result.Error = "center maintenance sheet-metal add/update validation failed";
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

        private static Component2 FindExisting(AssemblyDoc asm, string role)
        {
            Array arr = TryValue(() => asm.GetComponents(false) as Array, null);
            if (arr == null) return null;
            foreach (object item in arr)
            {
                Component2 component = item as Component2;
                if (component == null) continue;
                string name = TryValue(() => component.Name2, "");
                string path = TryValue(() => component.GetPathName(), "");
                if (name.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("\u5e94\u6025\u7ef4\u62a4\u95e8", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\u5e94\u6025\u7ef4\u62a4\u95e8", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return component;
                }
            }
            return null;
        }

        private static double[] ToTransformData(double txMm, double tyMm, double tzMm)
        {
            return new[]
            {
                1.0, 0.0, 0.0,
                0.0, 1.0, 0.0,
                0.0, 0.0, 1.0,
                txMm / 1000.0, tyMm / 1000.0, tzMm / 1000.0,
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

        private static string SafeFileName(string value)
        {
            string output = value;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                output = output.Replace(c, '_');
            }
            return string.IsNullOrWhiteSpace(output) ? "center_lock_maintenance_sheetmetal_strip" : output;
        }

        private static double ParseDouble(string value)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0.0;
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
            Field(sb, "schema", "winnsen.locker16029.add_center_maintenance_sheetmetal.v1").Append(",");
            Field(sb, "assembly_path", result.AssemblyPath).Append(",");
            Field(sb, "source_path", result.SourcePath).Append(",");
            Field(sb, "local_source_path", result.LocalSourcePath).Append(",");
            Field(sb, "role", result.Role).Append(",");
            Field(sb, "assembly_exists", result.AssemblyExists).Append(",");
            Field(sb, "source_exists", result.SourceExists).Append(",");
            Field(sb, "local_source_exists", result.LocalSourceExists).Append(",");
            Field(sb, "solidworks_process_id", result.SolidWorksProcessId).Append(",");
            Field(sb, "opened", result.Opened).Append(",");
            Field(sb, "open_errors", result.OpenErrors).Append(",");
            Field(sb, "open_warnings", result.OpenWarnings).Append(",");
            Field(sb, "part_opened", result.PartOpened).Append(",");
            Field(sb, "part_open_errors", result.PartOpenErrors).Append(",");
            Field(sb, "part_open_warnings", result.PartOpenWarnings).Append(",");
            Field(sb, "added", result.Added).Append(",");
            Field(sb, "updated", result.Updated).Append(",");
            Field(sb, "transform_created", result.TransformCreated).Append(",");
            Field(sb, "transform_applied", result.TransformApplied).Append(",");
            Field(sb, "tx_mm", result.TxMm).Append(",");
            Field(sb, "ty_mm", result.TyMm).Append(",");
            Field(sb, "tz_mm", result.TzMm).Append(",");
            Field(sb, "name", result.Name).Append(",");
            Field(sb, "path", result.Path).Append(",");
            Field(sb, "box", result.Box).Append(",");
            Field(sb, "rebuilt", result.Rebuilt).Append(",");
            Field(sb, "saved", result.Saved).Append(",");
            Field(sb, "save_errors", result.SaveErrors).Append(",");
            Field(sb, "save_warnings", result.SaveWarnings).Append(",");
            Field(sb, "save_flag_after", result.SaveFlagAfter).Append(",");
            Field(sb, "success", result.Success).Append(",");
            Field(sb, "error", result.Error);
            sb.Append("}");
            return sb.ToString();
        }

        private static StringBuilder Field(StringBuilder sb, string name, string value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":");
            if (value == null) sb.Append("null"); else sb.Append('"').Append(Escape(value)).Append('"');
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, bool value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value ? "true" : "false");
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, int value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, double value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            return sb;
        }

        private static StringBuilder Field(StringBuilder sb, string name, BoxInfo box)
        {
            sb.Append('"').Append(Escape(name)).Append("\":{");
            Field(sb, "valid", box.Valid).Append(",");
            Field(sb, "xmin_mm", box.XMinMm).Append(",");
            Field(sb, "ymin_mm", box.YMinMm).Append(",");
            Field(sb, "zmin_mm", box.ZMinMm).Append(",");
            Field(sb, "xmax_mm", box.XMaxMm).Append(",");
            Field(sb, "ymax_mm", box.YMaxMm).Append(",");
            Field(sb, "zmax_mm", box.ZMaxMm);
            sb.Append("}");
            return sb;
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class Result
        {
            public string AssemblyPath;
            public string SourcePath;
            public string LocalSourcePath;
            public string Role;
            public string OutJson;
            public bool AssemblyExists;
            public bool SourceExists;
            public bool LocalSourceExists;
            public int SolidWorksProcessId;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public bool PartOpened;
            public int PartOpenErrors;
            public int PartOpenWarnings;
            public bool Added;
            public bool Updated;
            public bool TransformCreated;
            public bool TransformApplied;
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public string Name = "";
            public string Path = "";
            public BoxInfo Box = new BoxInfo();
            public bool Rebuilt;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public bool SaveFlagAfter = true;
            public bool Success;
            public string Error = "";
        }

        private struct BoxInfo
        {
            public bool Valid;
            public double XMinMm;
            public double YMinMm;
            public double ZMinMm;
            public double XMaxMm;
            public double YMaxMm;
            public double ZMaxMm;
        }
    }
}
