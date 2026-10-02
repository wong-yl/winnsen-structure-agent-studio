using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SolidWorks.Interop.sldworks;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class StepOpenProbe
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: StepOpenProbe.exe <model-path> <status-json-path> [preview-png-path] [--named-view <isometric|front|back|top|right|left>] [--close-after] [--auto-repair-dialog] [--silent-only]");
                return 2;
            }

            string stepPath = Path.GetFullPath(args[0]);
            string statusPath = Path.GetFullPath(args[1]);
            string previewPath = (args.Length >= 3 && !args[2].StartsWith("--", StringComparison.OrdinalIgnoreCase))
                ? Path.GetFullPath(args[2])
                : "";
            bool closeAfter = Array.Exists(args, arg => string.Equals(arg, "--close-after", StringComparison.OrdinalIgnoreCase));
            bool autoRepairDialog = Array.Exists(args, arg => string.Equals(arg, "--auto-repair-dialog", StringComparison.OrdinalIgnoreCase));
            bool silentOnly = Array.Exists(args, arg => string.Equals(arg, "--silent-only", StringComparison.OrdinalIgnoreCase));
            int waitSeconds = ParseIntArg(args, "--wait-seconds", 0);
            NamedViewSpec previewView = ResolveNamedView(ParseStringArg(args, "--named-view", "*Isometric"));
            var result = new ProbeResult
            {
                StepPath = stepPath,
                StatusPath = statusPath,
                PreviewPath = previewPath,
                PreviewNamedView = previewView.Name,
                PreviewNamedViewId = previewView.Id,
                CloseAfter = closeAfter,
                AutoRepairDialog = autoRepairDialog,
                SilentOnly = silentOnly,
                WaitSeconds = waitSeconds,
            };
            DialogResponder responder = null;

            try
            {
                if (!File.Exists(stepPath))
                {
                    result.Status = "missing_step";
                    result.Message = "Model file does not exist.";
                    WriteResult(statusPath, result);
                    return 3;
                }

                ISldWorks sw = GetOrCreateSolidWorks(result);
                if (sw == null)
                {
                    result.Status = "solidworks_unavailable";
                    result.Message = "Could not create or attach to SolidWorks.Application.";
                    WriteResult(statusPath, result);
                    return 4;
                }

                TrySetVisible(sw, result);
                result.SolidWorksRevision = SafeString(() => sw.RevisionNumber());
                result.SolidWorksExecutable = FindSolidWorksExecutable();
                if (autoRepairDialog)
                {
                    responder = DialogResponder.Start(result, stepPath);
                }
                ModelDoc2 opened = TryOpen(sw, stepPath, result, silentOnly);
                if (opened == null && (autoRepairDialog || waitSeconds > 0))
                {
                    opened = WaitForActiveDocument(sw, result, waitSeconds > 0 ? waitSeconds : 90);
                }
                if (responder != null)
                {
                    responder.Stop();
                    responder = null;
                }
                if (opened == null)
                {
                    result.Status = "open_failed";
                    result.Message = "SolidWorks did not return a model document for this model file.";
                    result.DocumentCount = SafeInt(() => sw.GetDocumentCount());
                    WriteResult(statusPath, result);
                    return 5;
                }

                result.Status = "opened";
                result.Message = "SolidWorks returned and activated a model document for this model file.";
                result.DocumentCount = SafeInt(() => sw.GetDocumentCount());
                ModelDoc2 activeDoc = sw.ActiveDoc as ModelDoc2;
                result.ActiveTitle = SafeString(() => activeDoc == null ? null : activeDoc.GetTitle());
                result.ActivePath = SafeString(() => activeDoc == null ? null : activeDoc.GetPathName());

                if (!string.IsNullOrWhiteSpace(previewPath) && activeDoc != null)
                {
                    result.PreviewSaved = SavePreview(activeDoc, previewPath, previewView, result);
                }

                if (closeAfter && !string.IsNullOrWhiteSpace(result.ActiveTitle))
                {
                    TryAction(() => sw.CloseDoc(result.ActiveTitle), result, "CloseDoc");
                }

                WriteResult(statusPath, result);
                Console.WriteLine(ToJson(result));
                return 0;
            }
            catch (Exception ex)
            {
                if (responder != null)
                {
                    responder.Stop();
                }
                result.Status = "exception";
                result.Message = ex.Message;
                result.Exception = SafeExceptionText(ex);
                WriteResult(statusPath, result);
                Console.WriteLine(ToJson(result));
                return 1;
            }
        }

        private static ISldWorks GetOrCreateSolidWorks(ProbeResult result)
        {
            try
            {
                var existing = Marshal.GetActiveObject("SldWorks.Application") as ISldWorks;
                if (existing != null)
                {
                    result.AttachMode = "active_object";
                    return existing;
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new MethodResult("Marshal.GetActiveObject", false, ex.Message));
            }

            try
            {
                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                {
                    result.Diagnostics.Add(new MethodResult("Type.GetTypeFromProgID", false, "ProgID not found"));
                    return null;
                }
                var created = Activator.CreateInstance(swType) as ISldWorks;
                result.AttachMode = "created_object";
                return created;
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new MethodResult("Activator.CreateInstance", false, ex.Message));
                return null;
            }
        }

        private static void TrySetVisible(ISldWorks sw, ProbeResult result)
        {
            try
            {
                sw.Visible = true;
                result.VisibleSet = true;
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new MethodResult("Visible=true", false, ex.Message));
            }
        }

        private static ModelDoc2 TryOpen(ISldWorks sw, string stepPath, ProbeResult result, bool silentOnly)
        {
            int primaryDocType = DocumentTypeForPath(stepPath);
            string primaryLabel = DocumentTypeLabel(primaryDocType);

            ModelDoc2 doc = TryOpenDoc7(sw, stepPath, primaryDocType, true, result, "OpenDoc7_" + primaryLabel + "_silent");
            if (doc != null) return Activate(sw, doc, result, "OpenDoc7_" + primaryLabel + "_silent");

            if (silentOnly)
            {
                doc = TryOpenDoc6(sw, stepPath, primaryDocType, 1, result, "OpenDoc6_" + primaryLabel + "_silent");
                if (doc != null) return Activate(sw, doc, result, "OpenDoc6_" + primaryLabel + "_silent");

                doc = TryOpenDoc6(sw, stepPath, 0, 1, result, "OpenDoc6_none_silent");
                if (doc != null) return Activate(sw, doc, result, "OpenDoc6_none_silent");

                doc = TryLoadFile4(sw, stepPath, "r", result, "LoadFile4_r");
                if (doc != null) return Activate(sw, doc, result, "LoadFile4_r");

                return null;
            }

            doc = TryOpenDoc7(sw, stepPath, primaryDocType, false, result, "OpenDoc7_" + primaryLabel + "_interactive");
            if (doc != null) return Activate(sw, doc, result, "OpenDoc7_" + primaryLabel + "_interactive");

            doc = TryOpenDoc6(sw, stepPath, primaryDocType, 1, result, "OpenDoc6_" + primaryLabel + "_silent");
            if (doc != null) return Activate(sw, doc, result, "OpenDoc6_" + primaryLabel + "_silent");

            doc = TryOpenDoc6(sw, stepPath, primaryDocType, 0, result, "OpenDoc6_" + primaryLabel + "_interactive");
            if (doc != null) return Activate(sw, doc, result, "OpenDoc6_" + primaryLabel + "_interactive");

            doc = TryOpenDoc6(sw, stepPath, 0, 1, result, "OpenDoc6_none_silent");
            if (doc != null) return Activate(sw, doc, result, "OpenDoc6_none_silent");

            doc = TryLoadFile4(sw, stepPath, "", result, "LoadFile4_empty");
            if (doc != null) return Activate(sw, doc, result, "LoadFile4_empty");

            doc = TryLoadFile4(sw, stepPath, "r", result, "LoadFile4_r");
            if (doc != null) return Activate(sw, doc, result, "LoadFile4_r");

            return null;
        }

        private static ModelDoc2 TryOpenDoc7(ISldWorks sw, string stepPath, int docType, bool silent, ProbeResult result, string name)
        {
            try
            {
                object specObject = sw.GetOpenDocSpec(stepPath);
                var spec = specObject as IDocumentSpecification;
                if (spec == null)
                {
                    result.Methods.Add(new MethodResult(name, false, "GetOpenDocSpec returned null/non-spec"));
                    return null;
                }
                spec.FileName = stepPath;
                spec.DocumentType = docType;
                spec.Silent = silent;
                spec.ReadOnly = false;
                spec.LoadModel = true;
                spec.AutoRepair = true;
                spec.CriticalDataRepair = true;
                ModelDoc2 doc = sw.OpenDoc7(specObject);
                var method = new MethodResult(name, doc != null, doc == null ? "null document" : null)
                {
                    ErrorCode = spec.Error,
                    WarningCode = spec.Warning,
                    Title = SafeString(() => doc == null ? null : doc.GetTitle()),
                    PathName = SafeString(() => doc == null ? null : doc.GetPathName()),
                };
                result.Methods.Add(method);
                return doc;
            }
            catch (Exception ex)
            {
                result.Methods.Add(new MethodResult(name, false, ex.Message));
                return null;
            }
        }

        private static ModelDoc2 TryOpenDoc6(ISldWorks sw, string stepPath, int docType, int options, ProbeResult result, string name)
        {
            try
            {
                int errors = 0;
                int warnings = 0;
                ModelDoc2 doc = sw.OpenDoc6(stepPath, docType, options, "", ref errors, ref warnings);
                result.Methods.Add(
                    new MethodResult(name, doc != null, doc == null ? "null document" : null)
                    {
                        ErrorCode = errors,
                        WarningCode = warnings,
                        Title = SafeString(() => doc == null ? null : doc.GetTitle()),
                        PathName = SafeString(() => doc == null ? null : doc.GetPathName()),
                    }
                );
                return doc;
            }
            catch (Exception ex)
            {
                result.Methods.Add(new MethodResult(name, false, ex.Message));
                return null;
            }
        }

        private static ModelDoc2 TryLoadFile4(ISldWorks sw, string stepPath, string argString, ProbeResult result, string name)
        {
            try
            {
                object importData = sw.GetImportFileData(stepPath);
                if (importData == null)
                {
                    result.Methods.Add(new MethodResult(name, false, "GetImportFileData returned null"));
                    return null;
                }

                int errors = 0;
                ModelDoc2 doc = sw.LoadFile4(stepPath, argString, importData, ref errors);
                result.Methods.Add(
                    new MethodResult(name, doc != null, doc == null ? "null document" : null)
                    {
                        ErrorCode = errors,
                        Title = SafeString(() => doc == null ? null : doc.GetTitle()),
                        PathName = SafeString(() => doc == null ? null : doc.GetPathName()),
                    }
                );
                return doc;
            }
            catch (Exception ex)
            {
                result.Methods.Add(new MethodResult(name, false, ex.Message));
                return null;
            }
        }

        private static ModelDoc2 Activate(ISldWorks sw, ModelDoc2 doc, ProbeResult result, string methodName)
        {
            try
            {
                string title = doc.GetTitle();
                sw.ActivateDoc(title);
                result.SuccessMethod = methodName;
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new MethodResult("ActivateDoc", false, ex.Message));
            }
            return doc;
        }

        private static ModelDoc2 WaitForActiveDocument(ISldWorks sw, ProbeResult result, int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            while (DateTime.Now < deadline)
            {
                try
                {
                    ModelDoc2 doc = sw.ActiveDoc as ModelDoc2;
                    if (doc != null)
                    {
                        result.SuccessMethod = "ActiveDoc_after_repair_dialog";
                        result.Diagnostics.Add(new MethodResult("WaitForActiveDocument", true, "active document found after repair dialog"));
                        return doc;
                    }
                }
                catch (Exception ex)
                {
                    result.Diagnostics.Add(new MethodResult("WaitForActiveDocument", false, ex.Message));
                }
                Thread.Sleep(1000);
            }
            result.Diagnostics.Add(new MethodResult("WaitForActiveDocument", false, "timeout waiting for active document"));
            return null;
        }

        private static int? SafeInt(Func<int> read)
        {
            try { return read(); }
            catch { return null; }
        }

        private static int ParseIntArg(string[] args, string name, int defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    int value;
                    return int.TryParse(args[i + 1], out value) ? value : defaultValue;
                }
            }
            return defaultValue;
        }

        private static string ParseStringArg(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return defaultValue;
        }

        private static int DocumentTypeForPath(string path)
        {
            string extension = Path.GetExtension(path) ?? "";
            extension = extension.TrimStart('.').ToLowerInvariant();
            if (extension == "sldasm") return 2;
            if (extension == "slddrw") return 3;
            return 1;
        }

        private static string DocumentTypeLabel(int docType)
        {
            if (docType == 2) return "assembly";
            if (docType == 3) return "drawing";
            return "part";
        }

        private static NamedViewSpec ResolveNamedView(string requested)
        {
            string value = string.IsNullOrWhiteSpace(requested) ? "*Isometric" : requested.Trim();
            string key = value.TrimStart('*').ToLowerInvariant();
            if (key == "front") return new NamedViewSpec("*Front", 1);
            if (key == "back") return new NamedViewSpec("*Back", 2);
            if (key == "left") return new NamedViewSpec("*Left", 3);
            if (key == "right") return new NamedViewSpec("*Right", 4);
            if (key == "top") return new NamedViewSpec("*Top", 5);
            if (key == "bottom") return new NamedViewSpec("*Bottom", 6);
            return new NamedViewSpec("*Isometric", 7);
        }

        private static string SafeString(Func<string> read)
        {
            try { return read(); }
            catch { return null; }
        }

        private static void TryAction(Action action, ProbeResult result, string label)
        {
            try { action(); }
            catch (Exception ex) { result.Diagnostics.Add(new MethodResult(label, false, ex.Message)); }
        }

        private static bool TryBool(Func<bool> read, ProbeResult result, string label)
        {
            try { return read(); }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new MethodResult(label, false, ex.Message));
                return false;
            }
        }

        private static bool SavePreview(ModelDoc2 activeDoc, string previewPath, NamedViewSpec view, ProbeResult result)
        {
            try
            {
                string previewDir = Path.GetDirectoryName(previewPath);
                if (!string.IsNullOrWhiteSpace(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new MethodResult("CreatePreviewDirectory", false, ex.Message));
                return false;
            }

            TryAction(() => activeDoc.ShowNamedView2(view.Name, view.Id), result, "ShowNamedView2_" + view.Name.TrimStart('*').ToLowerInvariant());
            TryAction(() => activeDoc.ViewZoomtofit2(), result, "ViewZoomtofit2");
            TryAction(() => activeDoc.GraphicsRedraw2(), result, "GraphicsRedraw2");
            Thread.Sleep(1500);

            bool saved = TryBool(() => activeDoc.SaveAs(previewPath), result, "SaveAsPreview");
            if (!saved)
            {
                result.Diagnostics.Add(new MethodResult("SaveAsPreview", false, "SaveAs returned false"));
            }
            return saved && File.Exists(previewPath);
        }

        private static string FindSolidWorksExecutable()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("SLDWORKS"))
                {
                    try
                    {
                        string fileName = process.MainModule == null ? "" : process.MainModule.FileName;
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            return fileName;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        private static void WriteResult(string statusPath, ProbeResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath));
            File.WriteAllText(statusPath, ToJson(result));
        }

        private static string ToJson(ProbeResult result)
        {
            var lines = new List<string>
            {
                "{",
                string.Format("  \"status\": \"{0}\",", Escape(result.Status)),
                string.Format("  \"message\": \"{0}\",", Escape(result.Message)),
                string.Format("  \"step_path\": \"{0}\",", Escape(result.StepPath)),
                string.Format("  \"status_path\": \"{0}\",", Escape(result.StatusPath)),
                string.Format("  \"preview_path\": \"{0}\",", Escape(result.PreviewPath)),
                string.Format("  \"preview_named_view\": \"{0}\",", Escape(result.PreviewNamedView)),
                string.Format("  \"preview_named_view_id\": {0},", result.PreviewNamedViewId),
                string.Format("  \"requested_mainline\": \"{0}\",", Escape(result.RequestedMainline)),
                string.Format("  \"solidworks_revision\": \"{0}\",", Escape(result.SolidWorksRevision)),
                string.Format("  \"solidworks_executable\": \"{0}\",", Escape(result.SolidWorksExecutable)),
                string.Format("  \"opened\": {0},", Bool(string.Equals(result.Status, "opened", StringComparison.OrdinalIgnoreCase))),
                string.Format("  \"preview_saved\": {0},", Bool(result.PreviewSaved)),
                string.Format("  \"attach_mode\": \"{0}\",", Escape(result.AttachMode)),
                string.Format("  \"visible_set\": {0},", Bool(result.VisibleSet)),
                string.Format("  \"close_after\": {0},", Bool(result.CloseAfter)),
                string.Format("  \"auto_repair_dialog\": {0},", Bool(result.AutoRepairDialog)),
                string.Format("  \"silent_only\": {0},", Bool(result.SilentOnly)),
                string.Format("  \"wait_seconds\": {0},", result.WaitSeconds),
                string.Format("  \"dialog_click_count\": {0},", result.DialogClickCount),
                string.Format("  \"success_method\": \"{0}\",", Escape(result.SuccessMethod)),
                string.Format("  \"document_count\": {0},", NullableInt(result.DocumentCount)),
                string.Format("  \"active_title\": \"{0}\",", Escape(result.ActiveTitle)),
                string.Format("  \"active_path\": \"{0}\",", Escape(result.ActivePath)),
                string.Format("  \"exception\": \"{0}\",", Escape(result.Exception)),
                "  \"methods\": [",
            };
            for (int i = 0; i < result.Methods.Count; i++)
            {
                lines.Add("    " + MethodJson(result.Methods[i]) + (i + 1 == result.Methods.Count ? "" : ","));
            }
            lines.Add("  ],");
            lines.Add("  \"dialog_events\": [");
            for (int i = 0; i < result.DialogEvents.Count; i++)
            {
                lines.Add("    \"" + Escape(result.DialogEvents[i]) + "\"" + (i + 1 == result.DialogEvents.Count ? "" : ","));
            }
            lines.Add("  ],");
            lines.Add("  \"diagnostics\": [");
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                lines.Add("    " + MethodJson(result.Diagnostics[i]) + (i + 1 == result.Diagnostics.Count ? "" : ","));
            }
            lines.Add("  ]");
            lines.Add("}");
            return string.Join(System.Environment.NewLine, lines);
        }

        private static string MethodJson(MethodResult method)
        {
            return "{"
                + string.Format("\"name\":\"{0}\",", Escape(method.Name))
                + string.Format("\"ok\":{0},", Bool(method.Ok))
                + string.Format("\"error\":\"{0}\",", Escape(method.Error))
                + string.Format("\"error_code\":{0},", NullableInt(method.ErrorCode))
                + string.Format("\"warning_code\":{0},", NullableInt(method.WarningCode))
                + string.Format("\"title\":\"{0}\",", Escape(method.Title))
                + string.Format("\"path_name\":\"{0}\"", Escape(method.PathName))
                + "}";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string NullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "null";
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string SafeExceptionText(Exception ex)
        {
            if (ex == null) return "";
            try
            {
                return ex.GetType().FullName + ": " + ex.Message;
            }
            catch
            {
                return "Exception text unavailable.";
            }
        }
    }

    internal sealed class ProbeResult
    {
        public string Status = "not_started";
        public string Message = "";
        public string StepPath = "";
        public string StatusPath = "";
        public string PreviewPath = "";
        public string PreviewNamedView = "";
        public int PreviewNamedViewId;
        public string RequestedMainline = "SolidWorks 2020";
        public string SolidWorksRevision = "";
        public string SolidWorksExecutable = "";
        public bool PreviewSaved;
        public string AttachMode = "";
        public bool VisibleSet;
        public bool CloseAfter;
        public bool AutoRepairDialog;
        public bool SilentOnly;
        public int WaitSeconds;
        public int DialogClickCount;
        public string SuccessMethod = "";
        public int? DocumentCount;
        public string ActiveTitle = "";
        public string ActivePath = "";
        public string Exception = "";
        public readonly List<MethodResult> Methods = new List<MethodResult>();
        public readonly List<MethodResult> Diagnostics = new List<MethodResult>();
        public readonly List<string> DialogEvents = new List<string>();
    }

    internal sealed class NamedViewSpec
    {
        public NamedViewSpec(string name, int id)
        {
            Name = name;
            Id = id;
        }

        public string Name;
        public int Id;
    }

    internal sealed class DialogResponder
    {
        private const int BM_CLICK = 0x00F5;
        private readonly ProbeResult result;
        private readonly string stepFileName;
        private readonly Thread thread;
        private volatile bool stopping;

        private DialogResponder(ProbeResult result, string stepPath)
        {
            this.result = result;
            this.stepFileName = Path.GetFileName(stepPath) ?? "";
            this.thread = new Thread(Run);
            this.thread.IsBackground = true;
            this.thread.SetApartmentState(ApartmentState.STA);
        }

        public static DialogResponder Start(ProbeResult result, string stepPath)
        {
            var responder = new DialogResponder(result, stepPath);
            responder.Record("dialog responder started");
            responder.thread.Start();
            return responder;
        }

        public void Stop()
        {
            stopping = true;
            if (!thread.Join(3000))
            {
                Record("dialog responder did not stop within timeout");
            }
        }

        private void Run()
        {
            while (!stopping)
            {
                try
                {
                    TryClickRepairDialog();
                }
                catch (Exception ex)
                {
                    Record("dialog responder error: " + ex.Message);
                }
                Thread.Sleep(400);
            }
            Record("dialog responder stopped");
        }

        private void TryClickRepairDialog()
        {
            var processIds = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName("SLDWORKS"))
            {
                processIds.Add(process.Id);
            }
            if (processIds.Count == 0) return;

            EnumWindows(
                delegate(IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    int pid = WindowProcessId(hWnd);
                    if (!processIds.Contains(pid)) return true;

                    WindowSnapshot snapshot = SnapshotWindow(hWnd);
                    if (!LooksLikeRepairDialog(snapshot)) return true;

                    IntPtr yesButton = FindYesButton(snapshot.Children);
                    if (yesButton != IntPtr.Zero)
                    {
                        SetForegroundWindow(hWnd);
                        SendMessage(yesButton, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                        result.DialogClickCount += 1;
                        Record("clicked repair confirmation: window='" + snapshot.Title + "' text='" + TrimForLog(snapshot.AllText) + "'");
                    }
                    else
                    {
                        Record("repair dialog found but yes button missing: window='" + snapshot.Title + "' text='" + TrimForLog(snapshot.AllText) + "'");
                    }
                    return true;
                },
                IntPtr.Zero
            );
        }

        private bool LooksLikeRepairDialog(WindowSnapshot snapshot)
        {
            string text = (snapshot.Title + " " + snapshot.AllText).ToLowerInvariant();
            bool isSolidWorks = text.Contains("solidworks");
            bool mentionsRepair = text.Contains("repair") || text.Contains("\u4fee\u590d");
            bool mentionsProblem = text.Contains("problem") || text.Contains("\u95ee\u9898");
            return isSolidWorks && mentionsRepair && mentionsProblem;
        }

        private static IntPtr FindYesButton(List<WindowSnapshot> children)
        {
            foreach (WindowSnapshot child in children)
            {
                string text = child.Title.Trim().ToLowerInvariant();
                string className = child.ClassName.Trim().ToLowerInvariant();
                if (className.Contains("button") && (text.Contains("\u662f") || text == "yes" || text.Contains("&y") || text.Contains("(y)")))
                {
                    return child.Handle;
                }
            }
            return IntPtr.Zero;
        }

        private WindowSnapshot SnapshotWindow(IntPtr hWnd)
        {
            var root = new WindowSnapshot
            {
                Handle = hWnd,
                Title = GetText(hWnd),
                ClassName = GetClass(hWnd),
            };
            EnumChildWindows(
                hWnd,
                delegate(IntPtr child, IntPtr lParam)
                {
                    var childSnapshot = new WindowSnapshot
                    {
                        Handle = child,
                        Title = GetText(child),
                        ClassName = GetClass(child),
                    };
                    root.Children.Add(childSnapshot);
                    root.AllText += " " + childSnapshot.Title;
                    return true;
                },
                IntPtr.Zero
            );
            root.AllText = (root.Title + " " + root.AllText).Trim();
            return root;
        }

        private static int WindowProcessId(IntPtr hWnd)
        {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            return (int)pid;
        }

        private static string GetText(IntPtr hWnd)
        {
            var builder = new StringBuilder(512);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string GetClass(IntPtr hWnd)
        {
            var builder = new StringBuilder(256);
            GetClassName(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        private void Record(string message)
        {
            lock (result.DialogEvents)
            {
                result.DialogEvents.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message);
            }
        }

        private static string TrimForLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= 220 ? text : text.Substring(0, 220);
        }

        private sealed class WindowSnapshot
        {
            public IntPtr Handle;
            public string Title = "";
            public string ClassName = "";
            public string AllText = "";
            public readonly List<WindowSnapshot> Children = new List<WindowSnapshot>();
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWnd, EnumWindowsProc enumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class MethodResult
    {
        public MethodResult(string name, bool ok, string error)
        {
            Name = name;
            Ok = ok;
            Error = error ?? "";
        }

        public string Name;
        public bool Ok;
        public string Error;
        public int? ErrorCode;
        public int? WarningCode;
        public string Title = "";
        public string PathName = "";
    }
}
