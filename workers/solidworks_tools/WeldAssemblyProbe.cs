using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class WeldAssemblyProbe
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: WeldAssemblyProbe.exe <assembly-path> <out-json-path> [--close-after]");
                return 2;
            }

            string assemblyPath = Path.GetFullPath(args[0]);
            string outJsonPath = Path.GetFullPath(args[1]);
            bool closeAfter = Array.Exists(args, arg => string.Equals(arg, "--close-after", StringComparison.OrdinalIgnoreCase));
            bool useActive = Array.Exists(args, arg => string.Equals(arg, "--active", StringComparison.OrdinalIgnoreCase));
            var result = new ProbeResult
            {
                AssemblyPath = assemblyPath,
                OutJsonPath = outJsonPath,
                CloseAfter = closeAfter,
            };

            try
            {
                if (!File.Exists(assemblyPath))
                {
                    result.Status = "missing_assembly";
                    WriteJson(outJsonPath, result);
                    return 3;
                }

                dynamic sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Status = "solidworks_unavailable";
                    WriteJson(outJsonPath, result);
                    return 4;
                }

                Safe(() => { sw.Visible = true; });
                dynamic doc = null;
                if (useActive)
                {
                    doc = SafeValue(() => sw.ActiveDoc, null);
                }
                if (doc == null)
                {
                    doc = SafeValue(() => sw.OpenDoc(assemblyPath, 2), null);
                }
                if (doc == null)
                {
                    doc = SafeValue(() => sw.OpenDoc6(assemblyPath, 2, 1, "", 0, 0), null);
                }
                if (doc == null)
                {
                    result.Status = "open_failed";
                    WriteJson(outJsonPath, result);
                    return 5;
                }

                result.Status = "opened";
                result.Title = SafeValue(() => (string)doc.GetTitle(), "");
                result.Path = SafeValue(() => (string)doc.GetPathName(), "");
                result.Type = SafeValue(() => (int)doc.GetType(), 0);
                result.ResolveResult = SafeValue(() => Convert.ToString(doc.ResolveAllLightWeightComponents(false), CultureInfo.InvariantCulture), "");
                result.Rebuilt = SafeValue(() => (bool)doc.ForceRebuild3(false), false);

                ExtractFeatureReferences(doc, result);
                ExtractGetComponents(doc, result);

                if (closeAfter)
                {
                    string title = result.Title;
                    Safe(() => sw.CloseDoc(title));
                }

                WriteJson(outJsonPath, result);
                Console.WriteLine(outJsonPath);
                return 0;
            }
            catch (Exception ex)
            {
                result.Status = "exception";
                result.Error = ex.ToString();
                WriteJson(outJsonPath, result);
                Console.WriteLine(outJsonPath);
                return 9;
            }
        }

        private static dynamic GetOrCreateSolidWorks()
        {
            try { return Marshal.GetActiveObject("SldWorks.Application"); }
            catch { }
            try
            {
                Type t = Type.GetTypeFromProgID("SldWorks.Application");
                return t == null ? null : Activator.CreateInstance(t);
            }
            catch { return null; }
        }

        private static void ExtractFeatureReferences(dynamic doc, ProbeResult result)
        {
            dynamic feat = SafeValue(() => doc.FirstFeature(), null);
            int guard = 0;
            while (feat != null && guard < 5000)
            {
                guard++;
                string type = SafeValue(() => (string)feat.GetTypeName2(), "");
                if (type == "Reference")
                {
                    dynamic comp = SafeValue(() => feat.GetSpecificFeature2(), null);
                    result.References.Add(ReadComponent(comp, SafeValue(() => (string)feat.Name, ""), type));
                }
                feat = SafeValue(() => feat.GetNextFeature(), null);
            }
            result.ReferenceCount = result.References.Count;
        }

        private static void ExtractGetComponents(dynamic doc, ProbeResult result)
        {
            object compsObj = SafeValue(() => doc.GetComponents(false), null);
            foreach (object comp in ToObjectList(compsObj))
            {
                result.Components.Add(ReadComponent(comp, "", "GetComponents(false)"));
            }
            result.ComponentCount = result.Components.Count;
        }

        private static ComponentInfo ReadComponent(dynamic comp, string featureName, string source)
        {
            var info = new ComponentInfo
            {
                FeatureName = featureName,
                Source = source,
            };
            if (comp == null)
            {
                info.Error = "null_component";
                return info;
            }

            info.ComponentName = SafeValue(() => (string)comp.Name2, "");
            info.Path = SafeValue(() => (string)comp.GetPathName(), "");
            info.Suppressed = SafeValue(() => Convert.ToString(comp.IsSuppressed(), CultureInfo.InvariantCulture), "");
            info.Transform = ReadTransform(SafeValue(() => comp.Transform2, null));
            info.TotalTransform = ReadTransform(SafeValue(() => comp.GetTotalTransform(true), null));
            info.Box = ReadBox(comp);
            return info;
        }

        private static List<double> ReadTransform(dynamic transform)
        {
            if (transform == null) return new List<double>();
            object data = SafeValue(() => transform.ArrayData, null);
            var values = ToDoubleList(data);
            if (values.Count == 0)
            {
                data = SafeValue(() => transform.IArrayData, null);
                values = ToDoubleList(data);
            }
            return values;
        }

        private static List<double> ReadBox(dynamic comp)
        {
            object data = SafeValue(() => comp.GetBox(false, false), null);
            var values = ToDoubleList(data);
            if (values.Count == 0)
            {
                data = SafeValue(() => comp.GetBox(false), null);
                values = ToDoubleList(data);
            }
            return values;
        }

        private static List<object> ToObjectList(object value)
        {
            var result = new List<object>();
            if (value == null) return result;
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object item in enumerable) result.Add(item);
            }
            else
            {
                result.Add(value);
            }
            return result;
        }

        private static List<double> ToDoubleList(object value)
        {
            var result = new List<double>();
            if (value == null) return result;
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object item in enumerable)
                {
                    if (item == null) continue;
                    double parsed;
                    if (double.TryParse(Convert.ToString(item, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                    {
                        result.Add(Math.Round(parsed, 9));
                    }
                }
            }
            return result;
        }

        private static void Safe(Action action)
        {
            try { action(); } catch { }
        }

        private static T SafeValue<T>(Func<T> func, T fallback)
        {
            try { return func(); } catch { return fallback; }
        }

        private static void WriteJson(string path, ProbeResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson(result), new UTF8Encoding(false));
        }

        private static string ToJson(ProbeResult r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            WriteProp(sb, "status", r.Status, true);
            WriteProp(sb, "assemblyPath", r.AssemblyPath);
            WriteProp(sb, "outJsonPath", r.OutJsonPath);
            WriteProp(sb, "title", r.Title);
            WriteProp(sb, "path", r.Path);
            WriteProp(sb, "type", r.Type);
            WriteProp(sb, "closeAfter", r.CloseAfter);
            WriteProp(sb, "rebuilt", r.Rebuilt);
            WriteProp(sb, "resolveResult", r.ResolveResult);
            WriteProp(sb, "referenceCount", r.ReferenceCount);
            WriteProp(sb, "componentCount", r.ComponentCount);
            WriteProp(sb, "error", r.Error);
            sb.Append(",\"references\":");
            WriteComponents(sb, r.References);
            sb.Append(",\"components\":");
            WriteComponents(sb, r.Components);
            sb.Append("}");
            return sb.ToString();
        }

        private static void WriteComponents(StringBuilder sb, List<ComponentInfo> items)
        {
            sb.Append("[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var c = items[i];
                sb.Append("{");
                WriteProp(sb, "featureName", c.FeatureName, true);
                WriteProp(sb, "source", c.Source);
                WriteProp(sb, "componentName", c.ComponentName);
                WriteProp(sb, "path", c.Path);
                WriteProp(sb, "suppressed", c.Suppressed);
                WriteProp(sb, "error", c.Error);
                sb.Append(",\"transform\":");
                WriteNumberArray(sb, c.Transform);
                sb.Append(",\"totalTransform\":");
                WriteNumberArray(sb, c.TotalTransform);
                sb.Append(",\"box\":");
                WriteNumberArray(sb, c.Box);
                sb.Append("}");
            }
            sb.Append("]");
        }

        private static void WriteProp(StringBuilder sb, string name, string value, bool first = false)
        {
            if (!first) sb.Append(",");
            sb.Append("\"").Append(JsonEscape(name)).Append("\":\"").Append(JsonEscape(value ?? "")).Append("\"");
        }

        private static void WriteProp(StringBuilder sb, string name, int value)
        {
            sb.Append(",\"").Append(JsonEscape(name)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteProp(StringBuilder sb, string name, bool value)
        {
            sb.Append(",\"").Append(JsonEscape(name)).Append("\":").Append(value ? "true" : "false");
        }

        private static void WriteNumberArray(StringBuilder sb, List<double> values)
        {
            sb.Append("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(values[i].ToString("0.#########", CultureInfo.InvariantCulture));
            }
            sb.Append("]");
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class ProbeResult
        {
            public string Status = "";
            public string AssemblyPath = "";
            public string OutJsonPath = "";
            public string Title = "";
            public string Path = "";
            public int Type;
            public bool CloseAfter;
            public bool Rebuilt;
            public string ResolveResult = "";
            public int ReferenceCount;
            public int ComponentCount;
            public string Error = "";
            public readonly List<ComponentInfo> References = new List<ComponentInfo>();
            public readonly List<ComponentInfo> Components = new List<ComponentInfo>();
        }

        private sealed class ComponentInfo
        {
            public string FeatureName = "";
            public string Source = "";
            public string ComponentName = "";
            public string Path = "";
            public string Suppressed = "";
            public string Error = "";
            public List<double> Transform = new List<double>();
            public List<double> TotalTransform = new List<double>();
            public List<double> Box = new List<double>();
        }
    }
}
