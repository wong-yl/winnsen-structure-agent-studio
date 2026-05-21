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
    internal static class BuildPlacedComponentsModule
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: BuildPlacedComponentsModule.exe <placements-tsv> <out-asm> <out-json>");
                return 2;
            }

            string placementPath = Path.GetFullPath(args[0]);
            string outAsm = Path.GetFullPath(args[1]);
            string outJson = Path.GetFullPath(args[2]);
            var result = new BuildResult
            {
                PlacementPath = placementPath,
                OutAsmPath = outAsm,
            };

            try
            {
                var placements = ReadPlacements(placementPath);
                result.PlacementCount = placements.Count;
                Directory.CreateDirectory(Path.GetDirectoryName(outAsm));

                ISldWorks sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 3;
                }

                sw.Visible = true;
                Try(() => sw.CloseAllDocuments(true));

                ModelDoc2 model = sw.NewAssembly() as ModelDoc2;
                AssemblyDoc asm = model as AssemblyDoc;
                if (model == null || asm == null)
                {
                    result.Error = "NewAssembly failed";
                    WriteJson(outJson, result);
                    return 4;
                }
                result.NewAssembly = true;

                MathUtility math = sw.GetMathUtility() as MathUtility;
                if (math == null)
                {
                    result.Error = "GetMathUtility failed";
                    WriteJson(outJson, result);
                    return 5;
                }

                foreach (Placement p in placements)
                {
                    result.Components.Add(Add(sw, model, asm, math, p));
                }

                result.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                result.Saved = TryValue(() => model.SaveAs(outAsm), false);
                result.ReferenceCount = CountReferenceFeatures(model);
                Try(() => sw.CloseAllDocuments(true));

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Saved ? 0 : 6;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
        }

        private static List<Placement> ReadPlacements(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("placements tsv not found", path);
            }

            var rows = new List<Placement>();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cols = line.Split('\t');
                if (cols.Length < 5)
                {
                    throw new InvalidDataException("Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " needs role, path, tx_mm, ty_mm, tz_mm");
                }
                if (i == 0 && string.Equals(cols[0], "role", StringComparison.OrdinalIgnoreCase)) continue;

                rows.Add(new Placement
                {
                    Role = cols[0],
                    Path = Path.GetFullPath(cols[1]),
                    TxMm = Parse(cols[2]),
                    TyMm = Parse(cols[3]),
                    TzMm = Parse(cols[4]),
                });
            }
            return rows;
        }

        private static ComponentResult Add(ISldWorks sw, ModelDoc2 asmModel, AssemblyDoc asm, MathUtility math, Placement p)
        {
            var row = new ComponentResult
            {
                Role = p.Role,
                Path = p.Path,
                TxMm = p.TxMm,
                TyMm = p.TyMm,
                TzMm = p.TzMm,
                Exists = File.Exists(p.Path),
            };
            if (!row.Exists)
            {
                row.Error = "missing source";
                return row;
            }

            int errors = 0;
            int warnings = 0;
            int docType = p.Path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART;
            ModelDoc2 source = sw.OpenDoc6(p.Path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2;
            row.Opened = source != null;
            row.OpenErrors = errors;
            row.OpenWarnings = warnings;
            if (source == null)
            {
                row.Error = "open failed";
                return row;
            }

            Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
            Component2 comp = asm.AddComponent5(
                p.Path,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "",
                false,
                "",
                p.TxMm / 1000.0,
                p.TyMm / 1000.0,
                p.TzMm / 1000.0) as Component2;
            row.Added = comp != null;
            if (comp == null)
            {
                row.Error = "add failed";
                return row;
            }

            Try(() => { comp.Name2 = p.Role; });
            MathTransform xf = math.CreateTransform(new double[]
            {
                1.0, 0.0, 0.0,
                0.0, 1.0, 0.0,
                0.0, 0.0, 1.0,
                p.TxMm / 1000.0, p.TyMm / 1000.0, p.TzMm / 1000.0,
                1.0, 0.0, 0.0, 0.0
            }) as MathTransform;
            row.TransformCreated = xf != null;
            row.TransformApplied = xf != null && TryValue(() => comp.SetTransformAndSolve2(xf), false);
            if (!row.TransformApplied)
            {
                row.Error = "transform apply failed";
            }
            return row;
        }

        private static int CountReferenceFeatures(ModelDoc2 model)
        {
            int count = 0;
            Feature feat = TryValue(() => model.FirstFeature() as Feature, null);
            int guard = 0;
            while (feat != null && guard < 5000)
            {
                guard++;
                if (TryValue(() => feat.GetTypeName2(), "") == "Reference") count++;
                feat = TryValue(() => feat.GetNextFeature() as Feature, null);
            }
            return count;
        }

        private static double Parse(string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
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

        private static void WriteJson(string path, BuildResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, result.ToJson(), Encoding.UTF8);
        }

        private sealed class Placement
        {
            public string Role;
            public string Path;
            public double TxMm;
            public double TyMm;
            public double TzMm;
        }

        private sealed class BuildResult
        {
            public string PlacementPath;
            public string OutAsmPath;
            public int PlacementCount;
            public bool NewAssembly;
            public bool Rebuilt;
            public bool Saved;
            public int ReferenceCount;
            public string Error;
            public readonly List<ComponentResult> Components = new List<ComponentResult>();

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                JsonProp(sb, "placement_path", PlacementPath, true);
                JsonProp(sb, "out_asm_path", OutAsmPath, true);
                JsonProp(sb, "placement_count", PlacementCount, true);
                JsonProp(sb, "new_assembly", NewAssembly, true);
                JsonProp(sb, "rebuilt", Rebuilt, true);
                JsonProp(sb, "saved", Saved, true);
                JsonProp(sb, "reference_count", ReferenceCount, true);
                JsonProp(sb, "error", Error, true);
                sb.AppendLine("  \"components\": [");
                for (int i = 0; i < Components.Count; i++)
                {
                    sb.Append(Components[i].ToJson("    "));
                    sb.AppendLine(i == Components.Count - 1 ? "" : ",");
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                return sb.ToString();
            }
        }

        private sealed class ComponentResult
        {
            public string Role;
            public string Path;
            public bool Exists;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public bool Added;
            public bool TransformCreated;
            public bool TransformApplied;
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public string Error;

            public string ToJson(string indent)
            {
                var sb = new StringBuilder();
                sb.AppendLine(indent + "{");
                JsonProp(sb, "role", Role, true, indent + "  ");
                JsonProp(sb, "path", Path, true, indent + "  ");
                JsonProp(sb, "exists", Exists, true, indent + "  ");
                JsonProp(sb, "opened", Opened, true, indent + "  ");
                JsonProp(sb, "open_errors", OpenErrors, true, indent + "  ");
                JsonProp(sb, "open_warnings", OpenWarnings, true, indent + "  ");
                JsonProp(sb, "added", Added, true, indent + "  ");
                JsonProp(sb, "transform_created", TransformCreated, true, indent + "  ");
                JsonProp(sb, "transform_applied", TransformApplied, true, indent + "  ");
                JsonProp(sb, "tx_mm", TxMm, true, indent + "  ");
                JsonProp(sb, "ty_mm", TyMm, true, indent + "  ");
                JsonProp(sb, "tz_mm", TzMm, true, indent + "  ");
                JsonProp(sb, "error", Error, false, indent + "  ");
                sb.Append(indent + "}");
                return sb.ToString();
            }
        }

        private static void JsonProp(StringBuilder sb, string key, string value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ");
            if (value == null) sb.Append("null");
            else sb.Append('"').Append(Escape(value)).Append('"');
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static void JsonProp(StringBuilder sb, string key, bool value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ").Append(value ? "true" : "false");
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static void JsonProp(StringBuilder sb, string key, int value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static void JsonProp(StringBuilder sb, string key, double value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ").Append(value.ToString("0.######", CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static string Escape(string value)
        {
            return value == null ? "" : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
