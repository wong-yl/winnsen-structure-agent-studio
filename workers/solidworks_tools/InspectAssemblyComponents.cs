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
    internal static class InspectAssemblyComponents
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: InspectAssemblyComponents.exe <assembly.SLDASM> <out-json> [--keep-open]");
                return 2;
            }

            string asmPath = Path.GetFullPath(args[0]);
            string outJson = Path.GetFullPath(args[1]);
            bool keepOpen = Array.Exists(args, arg => string.Equals(arg, "--keep-open", StringComparison.OrdinalIgnoreCase));
            var result = new InspectResult { AssemblyPath = asmPath, Exists = File.Exists(asmPath), KeepOpen = keepOpen };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));
                if (!result.Exists)
                {
                    result.Error = "input does not exist";
                    WriteJson(outJson, result);
                    return 2;
                }

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

                result.Title = TryValue(() => model.GetTitle(), "");
                result.ModelPath = TryValue(() => model.GetPathName(), "");

                AssemblyDoc asm = model as AssemblyDoc;
                if (asm != null)
                {
                    Try(() => asm.ResolveAllLightWeightComponents(false));

                    Configuration cfg = TryValue(() => model.GetActiveConfiguration() as Configuration, null);
                    Component2 root = cfg == null ? null : TryValue(() => cfg.GetRootComponent3(true), null);
                    if (root != null)
                    {
                        foreach (Component2 c in AsComponents(TryValue(() => root.GetChildren(), null)))
                        {
                            AddComponentRecursive(result, c, 0, "");
                        }
                        if (result.Components.Count > 0)
                        {
                            result.EnumerationMode = "root_children_recursive";
                        }
                    }

                    if (result.Components.Count == 0)
                    {
                        object raw = TryValue(() => asm.GetComponents(false), null);
                        foreach (Component2 c in AsComponents(raw))
                        {
                            AddComponent(result, c, 0, "");
                        }
                        if (result.Components.Count > 0)
                        {
                            result.EnumerationMode = "assembly_get_components_flat";
                        }
                    }
                }

                result.ComponentCount = result.Components.Count;
                if (result.ComponentCount == 0)
                {
                    result.Error = "component enumeration returned zero";
                    if (!keepOpen)
                    {
                        Try(() => sw.CloseDoc(model.GetTitle()));
                    }
                    WriteJson(outJson, result);
                    Console.WriteLine(outJson);
                    return 6;
                }

                if (!keepOpen)
                {
                    Try(() => sw.CloseDoc(model.GetTitle()));
                }
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 0;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 1;
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
                Component2 c = item as Component2;
                if (c != null) yield return c;
            }
        }

        private static void AddComponentRecursive(InspectResult result, Component2 c, int depth, string parent)
        {
            AddComponent(result, c, depth, parent);
            string name = result.Components[result.Components.Count - 1].Name;
            foreach (Component2 child in AsComponents(TryValue(() => c.GetChildren(), null)))
            {
                AddComponentRecursive(result, child, depth + 1, name);
            }
        }

        private static void AddComponent(InspectResult result, Component2 c, int depth, string parent)
        {
            MathTransform transform = TryValue(() => c.Transform2, null);
            MathTransform totalTransform = TryValue(() => c.GetTotalTransform(true), null);
            BoxInfo box = ToBoxInfo(TryValue(() => c.GetBox(false, false), null));
            result.Components.Add(new ComponentInfo
            {
                Index = result.Components.Count,
                Depth = depth,
                Parent = parent ?? "",
                Name = TryValue(() => c.Name2, ""),
                Path = TryValue(() => c.GetPathName(), ""),
                ReferencedConfiguration = TryValue(() => c.ReferencedConfiguration, ""),
                Suppression = TryValue(() => c.GetSuppression(), -1),
                IsSuppressed = TryValue(() => c.IsSuppressed(), false),
                IsHidden = TryValue(() => c.IsHidden(false), false),
                Transform = ToTransformInfo(transform),
                TotalTransform = ToTransformInfo(totalTransform),
                Box = box,
            });
        }

        private static TransformInfo ToTransformInfo(MathTransform transform)
        {
            if (transform == null) return null;
            object raw = TryValue(() => transform.ArrayData, null);
            Array array = raw as Array;
            if (array == null) return null;
            var values = new List<double>();
            foreach (object item in array)
            {
                values.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
            }
            return new TransformInfo
            {
                Array = values,
                TxMm = values.Count > 9 ? values[9] * 1000.0 : (double?)null,
                TyMm = values.Count > 10 ? values[10] * 1000.0 : (double?)null,
                TzMm = values.Count > 11 ? values[11] * 1000.0 : (double?)null,
            };
        }

        private static BoxInfo ToBoxInfo(object raw)
        {
            Array array = raw as Array;
            if (array == null || array.Length < 6) return null;
            var values = new List<double>();
            foreach (object item in array)
            {
                values.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture) * 1000.0);
            }
            if (values.Count < 6) return null;
            return new BoxInfo
            {
                XMinMm = values[0],
                YMinMm = values[1],
                ZMinMm = values[2],
                XMaxMm = values[3],
                YMaxMm = values[4],
                ZMaxMm = values[5],
            };
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

        private static void WriteJson(string path, InspectResult result)
        {
            File.WriteAllText(path, Json(result), new UTF8Encoding(false));
        }

        private static string Json(InspectResult r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "assembly_path", r.AssemblyPath).Append(",");
            Field(sb, "exists", r.Exists).Append(",");
            Field(sb, "opened", r.Opened).Append(",");
            Field(sb, "keep_open", r.KeepOpen).Append(",");
            Field(sb, "open_errors", r.OpenErrors).Append(",");
            Field(sb, "open_warnings", r.OpenWarnings).Append(",");
            Field(sb, "title", r.Title).Append(",");
            Field(sb, "model_path", r.ModelPath).Append(",");
            Field(sb, "enumeration_mode", r.EnumerationMode).Append(",");
            Field(sb, "component_count", r.ComponentCount).Append(",");
            Field(sb, "error", r.Error).Append(",");
            sb.Append("\"components\":[");
            for (int i = 0; i < r.Components.Count; i++)
            {
                if (i > 0) sb.Append(",");
                ComponentJson(sb, r.Components[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void ComponentJson(StringBuilder sb, ComponentInfo c)
        {
            sb.Append("{");
            Field(sb, "index", c.Index).Append(",");
            Field(sb, "depth", c.Depth).Append(",");
            Field(sb, "parent", c.Parent).Append(",");
            Field(sb, "name", c.Name).Append(",");
            Field(sb, "path", c.Path).Append(",");
            Field(sb, "referenced_configuration", c.ReferencedConfiguration).Append(",");
            Field(sb, "suppression", c.Suppression).Append(",");
            Field(sb, "is_suppressed", c.IsSuppressed).Append(",");
            Field(sb, "is_hidden", c.IsHidden).Append(",");
            sb.Append("\"transform\":");
            TransformJson(sb, c.Transform);
            sb.Append(",\"total_transform\":");
            TransformJson(sb, c.TotalTransform);
            sb.Append(",\"box\":");
            BoxJson(sb, c.Box);
            sb.Append("}");
        }

        private static void TransformJson(StringBuilder sb, TransformInfo t)
        {
            if (t == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append("{\"array\":[");
            for (int i = 0; i < t.Array.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(t.Array[i].ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append("],");
            Field(sb, "tx_mm", t.TxMm).Append(",");
            Field(sb, "ty_mm", t.TyMm).Append(",");
            Field(sb, "tz_mm", t.TzMm);
            sb.Append("}");
        }

        private static void BoxJson(StringBuilder sb, BoxInfo b)
        {
            if (b == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append("{");
            Field(sb, "xmin_mm", b.XMinMm).Append(",");
            Field(sb, "ymin_mm", b.YMinMm).Append(",");
            Field(sb, "zmin_mm", b.ZMinMm).Append(",");
            Field(sb, "xmax_mm", b.XMaxMm).Append(",");
            Field(sb, "ymax_mm", b.YMaxMm).Append(",");
            Field(sb, "zmax_mm", b.ZMaxMm).Append(",");
            Field(sb, "xlen_mm", b.XMaxMm - b.XMinMm).Append(",");
            Field(sb, "ylen_mm", b.YMaxMm - b.YMinMm).Append(",");
            Field(sb, "zlen_mm", b.ZMaxMm - b.ZMinMm);
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

        private static StringBuilder Field(StringBuilder sb, string name, double? value)
        {
            sb.Append("\"").Append(Escape(name)).Append("\":");
            return value.HasValue ? sb.Append(value.Value.ToString("R", CultureInfo.InvariantCulture)) : sb.Append("null");
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class InspectResult
        {
            public string AssemblyPath = "";
            public bool Exists;
            public bool Opened;
            public bool KeepOpen;
            public int OpenErrors;
            public int OpenWarnings;
            public string Title = "";
            public string ModelPath = "";
            public string EnumerationMode = "";
            public int ComponentCount;
            public string Error = "";
            public readonly List<ComponentInfo> Components = new List<ComponentInfo>();
        }

        private sealed class ComponentInfo
        {
            public int Index;
            public int Depth;
            public string Parent = "";
            public string Name = "";
            public string Path = "";
            public string ReferencedConfiguration = "";
            public int Suppression;
            public bool IsSuppressed;
            public bool IsHidden;
            public TransformInfo Transform;
            public TransformInfo TotalTransform;
            public BoxInfo Box;
        }

        private sealed class TransformInfo
        {
            public List<double> Array = new List<double>();
            public double? TxMm;
            public double? TyMm;
            public double? TzMm;
        }

        private sealed class BoxInfo
        {
            public double XMinMm;
            public double YMinMm;
            public double ZMinMm;
            public double XMaxMm;
            public double YMaxMm;
            public double ZMaxMm;
        }
    }
}
