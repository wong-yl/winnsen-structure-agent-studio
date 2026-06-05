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
    internal static class RemoveAssemblyComponentsByPattern
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: RemoveAssemblyComponentsByPattern.exe <assembly.SLDASM> <regex> <out-json>");
                return 2;
            }

            string asmPath = Path.GetFullPath(args[0]);
            string pattern = args[1];
            string outJson = Path.GetFullPath(args[2]);
            var result = new RemoveResult
            {
                AssemblyPath = asmPath,
                Pattern = pattern,
                Exists = File.Exists(asmPath),
            };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));
                if (!result.Exists)
                {
                    result.Error = "assembly does not exist";
                    WriteJson(outJson, result);
                    return 2;
                }

                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
                Try(() => model.ShowFeatureErrorDialog = false);

                AssemblyDoc asm = model as AssemblyDoc;
                if (asm == null)
                {
                    result.Error = "opened document is not an assembly";
                    WriteJson(outJson, result);
                    Close(sw, model);
                    return 5;
                }

                Try(() => asm.ResolveAllLightWeightComponents(false));
                var targets = new List<RemoveTarget>();
                foreach (Component2 component in EnumerateComponents(model, asm))
                {
                    if (component == null) continue;
                    string name = TryValue(() => component.Name2, "");
                    string path = TryValue(() => component.GetPathName(), "");
                    if (!regex.IsMatch(name ?? "") && !regex.IsMatch(path ?? "")) continue;

                    var item = new RemoveItem
                    {
                        Name = name,
                        Path = path,
                        WasSuppressed = TryValue(() => component.IsSuppressed(), false),
                        WasHidden = TryValue(() => component.IsHidden(false), false),
                    };
                    result.Candidates.Add(item);
                    targets.Add(new RemoveTarget { Component = component, Item = item });
                }

                result.SeenCount = result.Candidates.Count;
                if (result.Candidates.Count > 0)
                {
                    Try(() => model.ClearSelection2(true));
                    foreach (RemoveTarget target in targets)
                    {
                        bool selected = SelectComponent(model, target.Component, target.Item.Name);
                        target.Item.Selected = selected;
                        if (selected)
                        {
                            result.SelectedCount++;
                        }
                    }

                    if (result.SelectedCount > 0)
                    {
                        result.DeleteInvoked = true;
                        Try(() => model.EditDelete());
                        Try(() => model.ClearSelection2(true));
                    }
                }

                result.RemainingCount = CountMatches(model, asm, regex);
                result.RemovedCount = Math.Max(0, result.SeenCount - result.RemainingCount);
                result.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                if (!result.Rebuilt)
                {
                    result.Error = "assembly rebuild failed after component cleanup";
                }
                int saveErrors = 0;
                int saveWarnings = 0;
                result.Saved = TryValue(
                    () => model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings),
                    false);
                result.SaveErrors = saveErrors;
                result.SaveWarnings = saveWarnings;
                result.Closed = Close(sw, model);

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Opened && result.Rebuilt && result.Saved && result.RemainingCount == 0 ? 0 : 6;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 1;
            }
        }

        private static IEnumerable<Component2> EnumerateComponents(ModelDoc2 model, AssemblyDoc asm)
        {
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Configuration cfg = TryValue(() => model.GetActiveConfiguration() as Configuration, null);
            Component2 root = cfg == null ? null : TryValue(() => cfg.GetRootComponent3(true), null);
            if (root != null)
            {
                foreach (Component2 child in AsComponents(TryValue(() => root.GetChildren(), null)))
                {
                    foreach (Component2 nested in EnumerateRecursive(child, emitted))
                    {
                        yield return nested;
                    }
                }
            }

            foreach (Component2 component in AsComponents(TryValue(() => asm.GetComponents(false), null)))
            {
                string key = ComponentKey(component);
                if (emitted.Add(key))
                {
                    yield return component;
                }
            }
        }

        private static IEnumerable<Component2> EnumerateRecursive(Component2 component, HashSet<string> emitted)
        {
            if (component == null) yield break;
            string key = ComponentKey(component);
            if (emitted.Add(key))
            {
                yield return component;
            }

            foreach (Component2 child in AsComponents(TryValue(() => component.GetChildren(), null)))
            {
                foreach (Component2 nested in EnumerateRecursive(child, emitted))
                {
                    yield return nested;
                }
            }
        }

        private static string ComponentKey(Component2 component)
        {
            string name = TryValue(() => component.Name2, "");
            string path = TryValue(() => component.GetPathName(), "");
            return name + "|" + path;
        }

        private static bool SelectComponent(ModelDoc2 model, Component2 component, string name)
        {
            if (component != null && TryValue(() => component.Select4(true, null, false), false))
            {
                return true;
            }

            ModelDocExtension ext = model == null ? null : model.Extension as ModelDocExtension;
            if (ext == null || string.IsNullOrWhiteSpace(name)) return false;

            if (TryValue(() => ext.SelectByID2(name, "COMPONENT", 0, 0, 0, true, 0, null, 0), false))
            {
                return true;
            }

            string leaf = name;
            int slash = leaf.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < leaf.Length)
            {
                leaf = leaf.Substring(slash + 1);
            }
            return !string.Equals(leaf, name, StringComparison.Ordinal) &&
                   TryValue(() => ext.SelectByID2(leaf, "COMPONENT", 0, 0, 0, true, 0, null, 0), false);
        }

        private static int CountMatches(ModelDoc2 model, AssemblyDoc asm, Regex regex)
        {
            int count = 0;
            foreach (Component2 component in EnumerateComponents(model, asm))
            {
                string name = TryValue(() => component.Name2, "");
                string path = TryValue(() => component.GetPathName(), "");
                if (regex.IsMatch(name ?? "") || regex.IsMatch(path ?? ""))
                {
                    count++;
                }
            }
            return count;
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

        private static void WriteJson(string path, RemoveResult result)
        {
            File.WriteAllText(path, Json(result), new UTF8Encoding(false));
        }

        private static string Json(RemoveResult result)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Field(sb, "assembly_path", result.AssemblyPath).Append(",");
            Field(sb, "pattern", result.Pattern).Append(",");
            Field(sb, "exists", result.Exists).Append(",");
            Field(sb, "opened", result.Opened).Append(",");
            Field(sb, "open_errors", result.OpenErrors).Append(",");
            Field(sb, "open_warnings", result.OpenWarnings).Append(",");
            Field(sb, "seen_count", result.SeenCount).Append(",");
            Field(sb, "selected_count", result.SelectedCount).Append(",");
            Field(sb, "delete_invoked", result.DeleteInvoked).Append(",");
            Field(sb, "remaining_count", result.RemainingCount).Append(",");
            Field(sb, "removed_count", result.RemovedCount).Append(",");
            Field(sb, "rebuilt", result.Rebuilt).Append(",");
            Field(sb, "saved", result.Saved).Append(",");
            Field(sb, "save_errors", result.SaveErrors).Append(",");
            Field(sb, "save_warnings", result.SaveWarnings).Append(",");
            Field(sb, "closed", result.Closed).Append(",");
            Field(sb, "error", result.Error).Append(",");
            sb.Append("\"candidates\":[");
            for (int i = 0; i < result.Candidates.Count; i++)
            {
                if (i > 0) sb.Append(",");
                RemoveItemJson(sb, result.Candidates[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void RemoveItemJson(StringBuilder sb, RemoveItem item)
        {
            sb.Append("{");
            Field(sb, "name", item.Name).Append(",");
            Field(sb, "path", item.Path).Append(",");
            Field(sb, "was_suppressed", item.WasSuppressed).Append(",");
            Field(sb, "was_hidden", item.WasHidden).Append(",");
            Field(sb, "selected", item.Selected);
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

        private sealed class RemoveResult
        {
            public string AssemblyPath = "";
            public string Pattern = "";
            public bool Exists;
            public bool Opened;
            public int OpenErrors;
            public int OpenWarnings;
            public int SeenCount;
            public int SelectedCount;
            public bool DeleteInvoked;
            public int RemainingCount;
            public int RemovedCount;
            public bool Rebuilt;
            public bool Saved;
            public int SaveErrors;
            public int SaveWarnings;
            public bool Closed;
            public string Error = "";
            public readonly List<RemoveItem> Candidates = new List<RemoveItem>();
        }

        private sealed class RemoveItem
        {
            public string Name = "";
            public string Path = "";
            public bool WasSuppressed;
            public bool WasHidden;
            public bool Selected;
        }

        private sealed class RemoveTarget
        {
            public Component2 Component;
            public RemoveItem Item;
        }
    }
}
