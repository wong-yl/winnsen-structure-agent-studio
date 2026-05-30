using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class ImportStepSaveNative
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: ImportStepSaveNative.exe <source-step> <preferred-native-path> <exported-step> <out-json>");
                return 2;
            }

            string sourceStep = Path.GetFullPath(args[0]);
            string preferredNative = Path.GetFullPath(args[1]);
            string exportedStep = Path.GetFullPath(args[2]);
            string outJson = Path.GetFullPath(args[3]);
            string importStrategy = args.Length >= 5
                ? args[4]
                : System.Environment.GetEnvironmentVariable("STUDIO_SW_STEP_IMPORT_STRATEGY") ?? "auto";
            var result = new Result
            {
                SourceStepPath = sourceStep,
                PreferredNativePath = preferredNative,
                ExportedStepPath = exportedStep,
                ImportStrategy = importStrategy,
            };

            try
            {
                if (!File.Exists(sourceStep))
                {
                    result.Error = "source STEP missing";
                    WriteJson(outJson, result);
                    return 2;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(preferredNative));
                Directory.CreateDirectory(Path.GetDirectoryName(exportedStep));
                Directory.CreateDirectory(Path.GetDirectoryName(outJson));

                ISldWorks sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 3;
                }
                sw.Visible = true;
                Try(() => sw.CloseAllDocuments(true));

                OpenResult openResult = OpenStep(sw, sourceStep, importStrategy);
                ModelDoc2 imported = openResult.Document;
                result.LoadFileErrors = openResult.Errors;
                result.NativeOpenWarnings = openResult.Warnings;
                result.OpenMethod = openResult.Method;
                result.OpenedSource = imported != null;
                if (imported == null)
                {
                    result.Error = "LoadFile4 source STEP failed";
                    WriteJson(outJson, result);
                    return 4;
                }

                result.SourceTitle = TryValue(() => imported.GetTitle(), "");
                result.DocumentType = TryValue(() => imported.GetType(), 0);
                string nativePath = NativePathForType(preferredNative, result.DocumentType);
                result.NativePath = nativePath;

                // Imported STEP assemblies can contain transient component geometry.
                // Export the audited STEP while that import document is still live, then
                // save/reopen native only as the SolidWorks 2020 audit checkpoint.
                result.SavedStep = TryValue(() => imported.SaveAs(exportedStep), false);
                result.ExportedStepBytes = File.Exists(exportedStep) ? new FileInfo(exportedStep).Length : 0;

                result.SavedNative = TryValue(() => imported.SaveAs(nativePath), false);
                Try(() => sw.CloseDoc(imported.GetTitle()));
                if (!result.SavedNative)
                {
                    result.Error = "save native failed";
                    WriteJson(outJson, result);
                    return 5;
                }

                int openErrors = 0;
                int openWarnings = 0;
                int docType = nativePath.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)
                    ? (int)swDocumentTypes_e.swDocASSEMBLY
                    : (int)swDocumentTypes_e.swDocPART;
                ModelDoc2 reopened = TryValue(
                    () => sw.OpenDoc6(nativePath, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref openErrors, ref openWarnings) as ModelDoc2,
                    null);
                result.NativeOpenErrors = openErrors;
                result.NativeOpenWarnings = openWarnings;
                result.ReopenedNative = reopened != null;
                if (reopened == null)
                {
                    result.Error = "reopen native failed";
                    WriteJson(outJson, result);
                    return 6;
                }

                result.NativeTitle = TryValue(() => reopened.GetTitle(), "");
                if (!result.SavedStep || result.ExportedStepBytes < 4096)
                {
                    result.SavedStep = TryValue(() => reopened.SaveAs(exportedStep), false);
                    result.ExportedStepBytes = File.Exists(exportedStep) ? new FileInfo(exportedStep).Length : 0;
                }
                Try(() => sw.CloseDoc(reopened.GetTitle()));
                if (!result.SavedStep || result.ExportedStepBytes < 4096)
                {
                    result.Error = "export STEP failed or produced no geometry";
                    WriteJson(outJson, result);
                    return 7;
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
                return 9;
            }
        }

        private static string NativePathForType(string preferredNative, int documentType)
        {
            if (documentType == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                return Path.ChangeExtension(preferredNative, ".SLDASM");
            }
            return Path.ChangeExtension(preferredNative, ".SLDPRT");
        }

        private static OpenResult OpenStep(ISldWorks sw, string sourceStep, string importStrategy)
        {
            foreach (bool interconnectEnabled in new[] { false, true })
            {
                Try(() => sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swMultiCAD_Enable3DInterconnect, interconnectEnabled));
                foreach (string arg in new[] { "", "r" })
                {
                    string candidate = "LoadFile4:" + (arg == "" ? "empty" : arg) + ":3DI=" + interconnectEnabled;
                    if (!ShouldTryStrategy(importStrategy, candidate)) continue;
                    int errors = 0;
                    object importData = TryValue(() => sw.GetImportFileData(sourceStep), null);
                    ModelDoc2 doc = TryValue(() => sw.LoadFile4(sourceStep, arg, importData, ref errors), null) as ModelDoc2;
                    if (doc != null)
                    {
                        return new OpenResult { Document = doc, Errors = errors, Warnings = 0, Method = candidate };
                    }
                }
                foreach (int docType in new[] { (int)swDocumentTypes_e.swDocPART, (int)swDocumentTypes_e.swDocASSEMBLY })
                {
                    string candidate = "OpenDoc6:" + docType + ":3DI=" + interconnectEnabled;
                    if (!ShouldTryStrategy(importStrategy, candidate)) continue;
                    int errors = 0;
                    int warnings = 0;
                    ModelDoc2 doc = TryValue(
                        () => sw.OpenDoc6(sourceStep, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2,
                        null);
                    if (doc != null)
                    {
                        return new OpenResult { Document = doc, Errors = errors, Warnings = warnings, Method = candidate };
                    }
                }
                foreach (int docType in new[] { (int)swDocumentTypes_e.swDocPART, (int)swDocumentTypes_e.swDocASSEMBLY })
                {
                    string candidate = "OpenDoc7:" + docType + ":3DI=" + interconnectEnabled;
                    if (!ShouldTryStrategy(importStrategy, candidate)) continue;
                    IDocumentSpecification spec = TryValue(() => sw.GetOpenDocSpec(sourceStep) as IDocumentSpecification, null);
                    if (spec == null) continue;
                    Try(() => { spec.FileName = sourceStep; });
                    Try(() => { spec.DocumentType = docType; });
                    Try(() => { spec.Silent = true; });
                    Try(() => { spec.ReadOnly = false; });
                    Try(() => { spec.LoadModel = true; });
                    ModelDoc2 doc = TryValue(() => sw.OpenDoc7(spec), null) as ModelDoc2;
                    if (doc != null)
                    {
                        return new OpenResult { Document = doc, Errors = spec.Error, Warnings = spec.Warning, Method = candidate };
                    }
                }
            }
            return new OpenResult { Document = null, Errors = 1, Warnings = 0, Method = "none" };
        }

        private static bool ShouldTryStrategy(string requested, string candidate)
        {
            if (string.IsNullOrWhiteSpace(requested) || requested.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return NormalizeStrategy(requested).Equals(NormalizeStrategy(candidate), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStrategy(string value)
        {
            return (value ?? "")
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "")
                .Replace(":", "")
                .Replace("=", "")
                .ToLowerInvariant();
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

        private static T TryValue<T>(Func<T> func, T fallback)
        {
            try { return func(); } catch { return fallback; }
        }

        private static void WriteJson(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson(result), new UTF8Encoding(false));
        }

        private static string ToJson(Result r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Prop(sb, "sourceStepPath", r.SourceStepPath, true);
            Prop(sb, "preferredNativePath", r.PreferredNativePath);
            Prop(sb, "nativePath", r.NativePath);
            Prop(sb, "exportedStepPath", r.ExportedStepPath);
            Prop(sb, "importStrategy", r.ImportStrategy);
            Prop(sb, "openedSource", r.OpenedSource);
            Prop(sb, "savedNative", r.SavedNative);
            Prop(sb, "reopenedNative", r.ReopenedNative);
            Prop(sb, "savedStep", r.SavedStep);
            Prop(sb, "exportedStepBytes", r.ExportedStepBytes);
            Prop(sb, "documentType", r.DocumentType);
            Prop(sb, "loadFileErrors", r.LoadFileErrors);
            Prop(sb, "nativeOpenErrors", r.NativeOpenErrors);
            Prop(sb, "nativeOpenWarnings", r.NativeOpenWarnings);
            Prop(sb, "openMethod", r.OpenMethod);
            Prop(sb, "sourceTitle", r.SourceTitle);
            Prop(sb, "nativeTitle", r.NativeTitle);
            Prop(sb, "error", r.Error);
            sb.Append("}");
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

        private static void Prop(StringBuilder sb, string name, long value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value);
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class Result
        {
            public string SourceStepPath = "";
            public string PreferredNativePath = "";
            public string NativePath = "";
            public string ExportedStepPath = "";
            public string ImportStrategy = "auto";
            public bool OpenedSource;
            public bool SavedNative;
            public bool ReopenedNative;
            public bool SavedStep;
            public long ExportedStepBytes;
            public int DocumentType;
            public int LoadFileErrors;
            public int NativeOpenErrors;
            public int NativeOpenWarnings;
            public string OpenMethod = "";
            public string SourceTitle = "";
            public string NativeTitle = "";
            public string Error = "";
        }

        private sealed class OpenResult
        {
            public ModelDoc2 Document;
            public int Errors;
            public int Warnings;
            public string Method = "";
        }
    }
}
