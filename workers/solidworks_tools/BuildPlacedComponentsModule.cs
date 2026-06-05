using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                Console.Error.WriteLine("Usage: BuildPlacedComponentsModule.exe <placements-tsv> <out-asm> <out-json> [--isolated-session]");
                return 2;
            }

            string placementPath = Path.GetFullPath(args[0]);
            string outAsm = Path.GetFullPath(args[1]);
            string outJson = Path.GetFullPath(args[2]);
            bool isolatedSession = HasArgument(args, "--isolated-session");

            var result = new BuildResult
            {
                PlacementPath = placementPath,
                OutAsmPath = outAsm,
                BuildStatus = "starting",
                SolidWorksSessionMode = isolatedSession ? "isolated_new_instance" : "reuse_or_create",
            };
            ISldWorks sw = null;
            bool ownsSolidWorksSession = false;

            try
            {
                if (!File.Exists(placementPath))
                {
                    result.BuildStatus = "failed";
                    result.Error = "placements file not found";
                    WriteJson(outJson, result);
                    return 2;
                }

                List<Placement> placements = ReadPlacements(placementPath);
                result.PlacementCount = placements.Count;
                if (placements.Count == 0)
                {
                    result.BuildStatus = "failed";
                    result.Error = "placements file has no rows";
                    WriteJson(outJson, result);
                    return 2;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outAsm));
                if (isolatedSession && Process.GetProcessesByName("SLDWORKS").Length > 0)
                {
                    result.BuildStatus = "failed";
                    result.Error = "isolated session requires no running SolidWorks process";
                    WriteJson(outJson, result);
                    return 3;
                }
                sw = isolatedSession ? CreateSolidWorks() : GetOrCreateSolidWorks();
                ownsSolidWorksSession = isolatedSession && sw != null;
                if (sw == null)
                {
                    result.BuildStatus = "failed";
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 3;
                }
                sw.Visible = true;
                result.SolidWorksProcessId = TryValue(() => sw.GetProcessID(), 0);
                result.SolidWorksSessionOwned = ownsSolidWorksSession;
                result.BuildStatus = "solidworks_session_started";
                WriteJson(outJson, result);
                Try(() => sw.CloseAllDocuments(true));

                ModelDoc2 model = NewAssemblyDocument(sw);
                AssemblyDoc asm = model as AssemblyDoc;
                if (model == null || asm == null)
                {
                    result.BuildStatus = "failed";
                    result.Error = "NewAssembly failed";
                    WriteJson(outJson, result);
                    return 4;
                }
                result.NewAssembly = true;

                MathUtility math = sw.GetMathUtility() as MathUtility;
                if (math == null)
                {
                    result.BuildStatus = "failed";
                    result.Error = "GetMathUtility failed";
                    WriteJson(outJson, result);
                    return 5;
                }

                foreach (Placement p in placements)
                {
                    result.Components.Add(Add(sw, model, asm, math, p));
                }
                if (HasComponentErrors(result.Components))
                {
                    result.BuildStatus = "failed";
                    result.Error = "component placement validation failed; see components[].error";
                    WriteJson(outJson, result);
                    Console.WriteLine(outJson);
                    Try(() => sw.CloseDoc(model.GetTitle()));
                    return 7;
                }

                result.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                result.Saved = SaveModelWithRetry(model, outAsm, result);
                result.ReferenceCount = CountReferenceFeatures(model);
                Try(() => sw.CloseDoc(model.GetTitle()));

                result.BuildStatus = result.Saved ? "completed" : "failed";
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Saved ? 0 : 6;
            }
            catch (Exception ex)
            {
                result.BuildStatus = "failed";
                result.Error = SafeExceptionText(ex);
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
            finally
            {
                if (ownsSolidWorksSession && sw != null)
                {
                    Try(() => sw.CloseAllDocuments(true));
                    Try(() => sw.ExitApp());
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            for (int i = 3; i < args.Length; i++)
            {
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static List<Placement> ReadPlacements(string path)
        {
            var rows = new List<Placement>();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return rows;

            string[] headers = SplitTsv(lines[0]);
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                index[headers[i].Trim()] = i;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] values = SplitTsv(lines[i]);
                string role = Get(values, index, "role");
                string componentPath = Get(values, index, "path");
                if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(componentPath)) continue;
                double tx = ParseDouble(Get(values, index, "tx_mm"));
                double ty = ParseDouble(Get(values, index, "ty_mm"));
                double tz = ParseDouble(Get(values, index, "tz_mm"));
                double[] rotation = ReadRotation(values, index);
                string resolvedPath = Path.IsPathRooted(componentPath)
                    ? Path.GetFullPath(componentPath)
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? "", componentPath));
                rows.Add(new Placement(role, resolvedPath, rotation, tx, ty, tz));
            }
            return rows;
        }

        private static ModelDoc2 NewAssemblyDocument(ISldWorks sw)
        {
            ModelDoc2 model = TryValue(() => sw.NewAssembly() as ModelDoc2, null);
            if (model != null) return model;

            string template = TryValue(
                () => sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly),
                "");
            if (!string.IsNullOrWhiteSpace(template) && File.Exists(template))
            {
                model = TryValue(() => sw.NewDocument(template, 0, 0, 0) as ModelDoc2, null);
                if (model != null) return model;
            }

            foreach (string candidate in new[]
            {
                @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2020\templates\gb_assembly.asmdot",
                @"D:\soildworks2020\SOLIDWORKS\lang\chinese-simplified\Tutorial\assem.asmdot",
                @"D:\soildworks2020\SOLIDWORKS\lang\english\Tutorial\assem.asmdot",
            })
            {
                if (!File.Exists(candidate)) continue;
                model = TryValue(() => sw.NewDocument(candidate, 0, 0, 0) as ModelDoc2, null);
                if (model != null) return model;
            }

            return null;
        }

        private static bool HasComponentErrors(List<PlacementResult> components)
        {
            foreach (PlacementResult p in components)
            {
                if (!string.IsNullOrWhiteSpace(p.Error)) return true;
                if (!p.Exists || !p.Opened || !p.Added || !p.TransformCreated || !p.TransformApplied) return true;
            }
            return false;
        }

        private static string[] SplitTsv(string line)
        {
            return line.Split(new[] { '\t' });
        }

        private static string Get(string[] values, Dictionary<string, int> index, string name)
        {
            int i;
            if (!index.TryGetValue(name, out i) || i < 0 || i >= values.Length) return "";
            return values[i].Trim();
        }

        private static double[] ReadRotation(string[] values, Dictionary<string, int> index)
        {
            string packed = Get(values, index, "rotation");
            if (!string.IsNullOrWhiteSpace(packed))
            {
                string[] parts = packed.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 9)
                {
                    var r = new double[9];
                    for (int i = 0; i < 9; i++) r[i] = ParseDouble(parts[i]);
                    return r;
                }
            }

            string[] names = { "r11", "r12", "r13", "r21", "r22", "r23", "r31", "r32", "r33" };
            bool hasMatrix = true;
            for (int i = 0; i < names.Length; i++)
            {
                if (!index.ContainsKey(names[i]))
                {
                    hasMatrix = false;
                    break;
                }
            }
            if (hasMatrix)
            {
                var r = new double[9];
                for (int i = 0; i < names.Length; i++) r[i] = ParseDouble(Get(values, index, names[i]));
                return r;
            }

            return Identity();
        }

        private static double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0.0;
            return double.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }

        private static PlacementResult Add(ISldWorks sw, ModelDoc2 asmModel, AssemblyDoc asm, MathUtility math, Placement p)
        {
            var row = new PlacementResult
            {
                Role = p.Role,
                Path = p.Path,
                Exists = File.Exists(p.Path),
                Rotation = p.Rotation,
                TxMm = p.TxMm,
                TyMm = p.TyMm,
                TzMm = p.TzMm,
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
            ModelDoc2 partDoc = TryValue(
                () => sw.OpenDoc6(p.Path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2,
                null);
            if (partDoc == null)
            {
                partDoc = TryValue(() => sw.OpenDoc(p.Path, docType) as ModelDoc2, null);
            }
            row.Opened = partDoc != null;
            row.OpenErrors = errors;
            row.OpenWarnings = warnings;
            if (partDoc == null)
            {
                row.Error = "open failed";
                return row;
            }

            string sourceTitle = TryValue(() => partDoc.GetTitle(), "");
            try
            {
                Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
                Component2 comp = TryValue(
                    () => asm.AddComponent5(
                        p.Path,
                        (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                        "",
                        false,
                        "",
                        p.TxMm / 1000.0,
                        p.TyMm / 1000.0,
                        p.TzMm / 1000.0) as Component2,
                    null);
                row.Added = comp != null;
                if (comp == null)
                {
                    row.Error = "add failed";
                    return row;
                }
                Try(() => { comp.Name2 = p.Role; });

                MathTransform xf = math.CreateTransform(ToTransformData(p.Rotation, p.TxMm, p.TyMm, p.TzMm)) as MathTransform;
                row.TransformCreated = xf != null;
                if (xf == null)
                {
                    row.Error = "transform create failed";
                    return row;
                }
                row.TransformApplied = TryValue(() => comp.SetTransformAndSolve2(xf), false);
                if (!row.TransformApplied)
                {
                    row.Error = "transform apply failed";
                }
                return row;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(sourceTitle) && !string.Equals(sourceTitle, asmModel.GetTitle(), StringComparison.OrdinalIgnoreCase))
                {
                    Try(() => sw.CloseDoc(sourceTitle));
                }
                Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
            }
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

        private static int CountReferenceFeatures(ModelDoc2 model)
        {
            int count = 0;
            Feature feat = TryValue(() => model.FirstFeature() as Feature, null);
            int guard = 0;
            while (feat != null && guard < 5000)
            {
                guard++;
                string type = TryValue(() => feat.GetTypeName2(), "");
                if (type == "Reference") count++;
                feat = TryValue(() => feat.GetNextFeature() as Feature, null);
            }
            return count;
        }

        private static bool SaveModelWithRetry(ModelDoc2 model, string outAsm, BuildResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outAsm));
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                result.SaveAttempts = attempt;
                Try(() => model.ForceRebuild3(false));
                bool saved = TryValue(() => model.SaveAs(outAsm), false);
                if (saved && File.Exists(outAsm)) return true;

                int errors = 0;
                int warnings = 0;
                ModelDocExtension extension = TryValue(() => model.Extension, null);
                if (extension != null)
                {
                    saved = TryValue(
                        () => extension.SaveAs(
                            outAsm,
                            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                            null,
                            ref errors,
                            ref warnings),
                        false);
                    result.SaveErrors = errors;
                    result.SaveWarnings = warnings;
                    if (saved && File.Exists(outAsm)) return true;
                }

                System.Threading.Thread.Sleep(750);
            }
            return File.Exists(outAsm);
        }

        private static double[] Identity()
        {
            return new[] { 1.0, 0, 0, 0, 1.0, 0, 0, 0, 1.0 };
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
            }
            return CreateSolidWorks();
        }

        private static ISldWorks CreateSolidWorks()
        {
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
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

        private static T TryValue<T>(Func<T> func, T fallback)
        {
            try { return func(); } catch { return fallback; }
        }

        private static string SafeExceptionText(Exception ex)
        {
            try { return ex.ToString(); }
            catch
            {
                try { return ex.GetType().FullName + ": " + ex.Message; }
                catch { return "unknown exception"; }
            }
        }

        private static void WriteJson(string path, BuildResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson(result), new UTF8Encoding(false));
        }

        private static string ToJson(BuildResult r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Prop(sb, "placement_path", r.PlacementPath, true);
            Prop(sb, "out_asm_path", r.OutAsmPath);
            Prop(sb, "build_status", r.BuildStatus);
            Prop(sb, "solidworks_session_mode", r.SolidWorksSessionMode);
            Prop(sb, "solidworks_session_owned", r.SolidWorksSessionOwned);
            Prop(sb, "solidworks_process_id", r.SolidWorksProcessId);
            Prop(sb, "placement_count", r.PlacementCount);
            Prop(sb, "new_assembly", r.NewAssembly);
            Prop(sb, "rebuilt", r.Rebuilt);
            Prop(sb, "saved", r.Saved);
            Prop(sb, "save_attempts", r.SaveAttempts);
            Prop(sb, "save_errors", r.SaveErrors);
            Prop(sb, "save_warnings", r.SaveWarnings);
            Prop(sb, "reference_count", r.ReferenceCount);
            Prop(sb, "error", r.Error);
            sb.Append(",\"components\":[");
            for (int i = 0; i < r.Components.Count; i++)
            {
                if (i > 0) sb.Append(",");
                PlacementResult p = r.Components[i];
                sb.Append("{");
                Prop(sb, "role", p.Role, true);
                Prop(sb, "path", p.Path);
                Prop(sb, "exists", p.Exists);
                Prop(sb, "opened", p.Opened);
                Prop(sb, "added", p.Added);
                Prop(sb, "transform_created", p.TransformCreated);
                Prop(sb, "transform_applied", p.TransformApplied);
                ArrayProp(sb, "rotation", p.Rotation);
                Prop(sb, "tx_mm", p.TxMm);
                Prop(sb, "ty_mm", p.TyMm);
                Prop(sb, "tz_mm", p.TzMm);
                Prop(sb, "open_errors", p.OpenErrors);
                Prop(sb, "open_warnings", p.OpenWarnings);
                Prop(sb, "error", p.Error);
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void Prop(StringBuilder sb, string name, string value, bool first = false)
        {
            if (!first) sb.Append(",");
            sb.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? "")).Append("\"");
        }

        private static void Prop(StringBuilder sb, string name, bool value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value ? "true" : "false");
        }

        private static void Prop(StringBuilder sb, string name, int value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value);
        }

        private static void Prop(StringBuilder sb, string name, double value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value.ToString("0.#########", CultureInfo.InvariantCulture));
        }

        private static void ArrayProp(StringBuilder sb, string name, double[] values)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":[");
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(values[i].ToString("0.#########", CultureInfo.InvariantCulture));
                }
            }
            sb.Append("]");
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class Placement
        {
            public readonly string Role;
            public readonly string Path;
            public readonly double[] Rotation;
            public readonly double TxMm;
            public readonly double TyMm;
            public readonly double TzMm;

            public Placement(string role, string path, double[] rotation, double txMm, double tyMm, double tzMm)
            {
                Role = role;
                Path = path;
                Rotation = rotation;
                TxMm = txMm;
                TyMm = tyMm;
                TzMm = tzMm;
            }
        }

        private sealed class BuildResult
        {
            public string PlacementPath = "";
            public string OutAsmPath = "";
            public string BuildStatus = "";
            public string SolidWorksSessionMode = "";
            public bool SolidWorksSessionOwned;
            public int SolidWorksProcessId;
            public int PlacementCount;
            public bool NewAssembly;
            public bool Rebuilt;
            public bool Saved;
            public int SaveAttempts;
            public int SaveErrors;
            public int SaveWarnings;
            public int ReferenceCount;
            public string Error = "";
            public readonly List<PlacementResult> Components = new List<PlacementResult>();
        }

        private sealed class PlacementResult
        {
            public string Role = "";
            public string Path = "";
            public bool Exists;
            public double[] Rotation = new double[0];
            public bool Opened;
            public bool Added;
            public bool TransformCreated;
            public bool TransformApplied;
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public int OpenErrors;
            public int OpenWarnings;
            public string Error = "";
        }
    }
}
