using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwView = SolidWorks.Interop.sldworks.View;

namespace Winnsen.StructureAgent.Generation
{
    internal static class ProductionOrderDrawings
    {
        public sealed class Entry
        {
            public string templatePath { get; set; }
            public string sourceModelPath { get; set; }
            public string candidateModelPath { get; set; }
            public string configuration { get; set; }
            public string outName { get; set; }
        }
        public sealed class FileRow
        {
            public string path { get; set; }
            public string relativePath { get; set; }
            public string sha256 { get; set; }
        }
        public sealed class RunInput
        {
            public string schema { get; set; }
            public string classification { get; set; }
            public string planPath { get; set; }
            public string planSha256 { get; set; }
            public string attempt { get; set; }
            public string output { get; set; }
            public string packageDirectory { get; set; }
            public string executionPlanPath { get; set; }
            public string executionPlanSha256 { get; set; }
            public string archivePath { get; set; }
            public string archiveSha256 { get; set; }
            public string buildManifestPath { get; set; }
            public string buildManifestSha256 { get; set; }
            public string executablePath { get; set; }
            public string executableSha256 { get; set; }
            public bool productionReleased { get; set; }
            public Entry[] entries { get; set; }
            public FileRow[] protectedFiles { get; set; }
            public FileRow[] packageInventory { get; set; }
            public Dictionary<string, object> productionEvidence { get; set; }
        }
        sealed class DrawingState
        {
            public readonly List<Dictionary<string, object>> views = new List<Dictionary<string, object>>();
            public readonly HashSet<string> dimensions = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<string> dimensionGaps = new List<string>();
            public string[] sheets;
            public int modelDimensionCount, referencedViewCount;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(IntPtr handle, out FileInformation information);

        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 100000000 };
        static readonly Dictionary<string, string> Protected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> PackageBefore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<object> ModelDependencyOrders = new List<object>();
        static readonly List<object> ModelOpenEvents = new List<object>();
        static Dictionary<string, object> Evidence;
        static RunInput Input;
        static ISldWorks Sw;
        static bool Owned, EvidenceReserved, PreferenceCaptured, OriginalReferencesReadonly, PreferenceRestored;
        static Process OwnedProcess;
        static DateTime OwnedStartUtc;
        static string EvidencePath, ExpectedDrawingPath;
        static bool ExpectedDrawingReadOnly;
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static string Full(string path) { Need(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path), "ABSOLUTE_PATH_REQUIRED: " + path); return Path.GetFullPath(path); }
        static bool Same(string left, string right) { return string.Equals(Full(left), Full(right), StringComparison.OrdinalIgnoreCase); }
        static bool IsCad(string path) { return path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase); }
        static bool Inside(string path, string root) { return Same(path, root) || Full(path).StartsWith(Full(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        static string Plain(string path, bool directory)
        {
            string full = Full(path);
            Need(directory ? Directory.Exists(full) : File.Exists(full), "MISSING_" + (directory ? "DIRECTORY" : "FILE") + ": " + full);
            for (string current = full; current != null; current = Path.GetDirectoryName(current))
                Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "LINKED_PATH_REFUSED: " + current);
            if (!directory) using (var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                FileInformation information;
                Need(GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out information) && information.Links == 1, "LINKED_FILE_REFUSED: " + full);
            }
            return full;
        }
        static string Hash(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        static void Protect(string path, string expected = null)
        {
            string full = Plain(path, false), actual = Hash(full), previous;
            Need(expected == null || actual == expected, "PROTECTED_INPUT_HASH_MISMATCH: " + full);
            if (Protected.TryGetValue(full, out previous)) Need(previous == actual, "PROTECTED_INPUT_CHANGED: " + full);
            else Protected.Add(full, actual);
        }
        static void Flush()
        {
            if (!EvidenceReserved || Evidence == null) return;
            byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(Evidence) + "\n");
            string snapshot = EvidencePath + ".snapshot-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Replace(snapshot, EvidencePath, null); break; }
                    catch (IOException ex)
                    {
                        int code = ex.HResult & 0xffff;
                        if (attempt >= 39 || (code != 32 && code != 33 && code != 1224)) throw;
                        Thread.Sleep(50);
                    }
                }
            }
            finally { if (File.Exists(snapshot)) File.Delete(snapshot); }
        }
        static void RequireOwnedExclusive()
        {
            Need(Owned && Sw != null && OwnedProcess != null && !OwnedProcess.HasExited, "OWNED_NATIVE_SESSION_UNAVAILABLE");
            Need(Sw.GetProcessID() == OwnedProcess.Id && OwnedProcess.StartTime.ToUniversalTime() == OwnedStartUtc, "OWNED_NATIVE_PROCESS_IDENTITY_CHANGED");
            Process[] all = Process.GetProcessesByName("SLDWORKS");
            Need(all.Length == 1 && all[0].Id == OwnedProcess.Id, "ANOTHER_NATIVE_SESSION_APPEARED");
        }
        static void BeginReferenceReadOnlyPolicy()
        {
            RequireOwnedExclusive();
            OriginalReferencesReadonly = Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly);
            PreferenceCaptured = true; Evidence["referencesReadonlyPreferenceOriginal"] = OriginalReferencesReadonly;
            Evidence["referencesReadonlyPreferenceRestored"] = false; Flush();
            Sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly, true);
            bool actual = Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly);
            Evidence["referencesReadonlyPreferenceSetCallCompleted"] = true; Evidence["referencesReadonlyPreferenceActual"] = actual; Flush();
            Need(actual, "REFERENCES_READONLY_PREFERENCE_SET_FAILED");
        }
        static void RestoreReferenceReadOnlyPolicy()
        {
            if (!PreferenceCaptured) return;
            RequireOwnedExclusive();
            Sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly, OriginalReferencesReadonly);
            bool actual = Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly);
            PreferenceRestored = actual == OriginalReferencesReadonly;
            Evidence["referencesReadonlyPreferenceAfterRestore"] = actual; Evidence["referencesReadonlyPreferenceRestored"] = PreferenceRestored;
            Need(PreferenceRestored, "REFERENCES_READONLY_PREFERENCE_RESTORE_FAILED"); Flush();
        }
        static object Artifact(string path) { return new { path = path, sha256 = Hash(Plain(path, false)), length = new FileInfo(path).Length }; }
        static object[] ProtectedRows() { return Protected.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => (object)new { path = pair.Key, sha256 = pair.Value }).ToArray(); }
        static Dictionary<string, string> Inventory(string directory)
        {
            var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Action<string> visit = null;
            visit = folder =>
            {
                Plain(folder, true);
                foreach (string child in Directory.GetFileSystemEntries(folder))
                {
                    Need((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0, "LINKED_PACKAGE_ENTRY_REFUSED: " + child);
                    if (Directory.Exists(child)) visit(child);
                    else { Plain(child, false); rows.Add(Full(child), Hash(child)); }
                }
            };
            visit(directory); return rows;
        }
        static bool SameInventory(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            return before.Count == after.Count && before.All(pair => after.ContainsKey(pair.Key) && after[pair.Key] == pair.Value);
        }
        static string[] Dependencies(string path, bool recursive)
        {
            Array raw = Sw.GetDocumentDependencies2(path, recursive, true, false) as Array;
            string[] values = raw == null ? new string[0] : raw.Cast<string>().ToArray();
            Need(values.Length % 2 == 0, "UNREADABLE_NATIVE_DEPENDENCIES: " + path);
            var models = new List<string>();
            for (int i = 1; i < values.Length; i += 2)
                if (IsCad(values[i])) models.Add(Plain(values[i], false));
            return models.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        static List<string> ModelOpenOrder(Dictionary<string, string[]> graph)
        {
            var ordered = new List<string>();
            foreach (var pair in graph)
                foreach (string dependency in pair.Value)
                    Need(graph.ContainsKey(dependency), "UNMAPPED_MODEL_DEPENDENCY: " + dependency);
            foreach (string extension in new[] { ".SLDPRT", ".SLDASM" })
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Action<string> visit = null;
                visit = path =>
                {
                    if (seen.Contains(path)) return;
                    Need(active.Add(path), "CYCLIC_MODEL_DEPENDENCY: " + path);
                    foreach (string dependency in graph[path].Where(value => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) visit(dependency);
                    active.Remove(path); seen.Add(path); ordered.Add(path);
                };
                foreach (string path in graph.Keys.Where(value => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) visit(path);
            }
            return ordered;
        }
        static List<string> ModelClosure(string root, bool candidate)
        {
            var graph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            Action<string> collect = null;
            collect = path =>
            {
                path = Plain(path, false);
                Need(IsCad(path), "NON_NATIVE_MODEL_DEPENDENCY: " + path);
                Need(!candidate || Inside(path, Input.packageDirectory), "CANDIDATE_REFERENCE_OUTSIDE_ACCEPTED_PACKAGE: " + path);
                if (graph.ContainsKey(path)) return;
                Protect(path);
                string[] dependencies = Dependencies(path, false); graph.Add(path, dependencies);
                foreach (string dependency in dependencies) collect(dependency);
            };
            collect(root);
            List<string> ordered = ModelOpenOrder(graph);
            var contexts = graph.Where(pair => pair.Key.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase)).SelectMany(pair => pair.Value.Where(value => value.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)).Select(value => new { part = pair.Key, assembly = value, orderingConstraint = false })).ToArray();
            ModelDependencyOrders.Add(new { root = root, candidate = candidate, paths = ordered.ToArray(), plannedOnly = true, modelOpenMode = "root_only_with_readonly_references", partsBeforeAssemblies = true, assemblyContexts = contexts });
            return ordered;
        }
        static void NeedReadOnlyModels(IEnumerable<string> allowed)
        {
            RequireOwnedExclusive();
            Need(PreferenceCaptured && Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly), "REFERENCES_READONLY_POLICY_NOT_ACTIVE");
            var paths = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
            ModelDoc2[] documents = (Sw.GetDocuments() as Array ?? new object[0]).Cast<ModelDoc2>().ToArray();
            Need(documents.Length > 0, "ACTUAL_LOADED_DOCUMENTS_UNAVAILABLE");
            foreach (ModelDoc2 doc in documents)
            {
                string path = doc.GetPathName();
                if (doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                    Need(ExpectedDrawingPath != null && Same(path, ExpectedDrawingPath) && doc.IsOpenedReadOnly() == ExpectedDrawingReadOnly, "UNMAPPED_DRAWING_OR_UNEXPECTED_ACCESS_MODE: " + path);
                else
                    Need(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && IsCad(path) && paths.Contains(Full(path)) && Protected.ContainsKey(Full(path)) && doc.IsOpenedReadOnly(), "MODEL_NOT_OPENED_READ_ONLY_OR_UNMAPPED: " + path);
            }
        }
        static ModelDoc2 OpenModels(List<string> paths, string root)
        {
            RequireOwnedExclusive();
            Need(PreferenceCaptured && Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly), "REFERENCES_READONLY_POLICY_NOT_ACTIVE");
            Need(paths.Contains(root, StringComparer.OrdinalIgnoreCase), "MODEL_ROOT_OUTSIDE_PROTECTED_CLOSURE: " + root);
            foreach (string path in paths) Protect(path);
            var opened = new Dictionary<string, object> { { "path", root }, { "kind", "model_root" }, { "explicitOpenDoc6Called", false }, { "status", "opening" } };
            ModelOpenEvents.Add(opened); Flush();
            ModelDoc2 doc = Sw.GetOpenDocumentByName(root) as ModelDoc2;
            if (doc == null)
            {
                int error = 0, warning = 0; opened["explicitOpenDoc6Called"] = true; Flush();
                doc = Sw.OpenDoc6(root, root.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref error, ref warning) as ModelDoc2;
                opened["error"] = error; opened["warning"] = warning;
                Need(doc != null && error == 0 && Same(doc.GetPathName(), root) && doc.IsOpenedReadOnly(), "MODEL_READ_ONLY_OPEN_FAILED: " + root + " error=" + error + " warning=" + warning);
            }
            Need(doc.IsOpenedReadOnly() && Same(doc.GetPathName(), root), "MODEL_ROOT_NOT_READ_ONLY: " + root);
            NeedReadOnlyModels(paths); opened["readOnly"] = true; opened["status"] = "read_only_observed"; Flush();
            return doc;
        }
        static ModelDoc2 OpenDrawing(string path, bool readOnly)
        {
            RequireOwnedExclusive();
            Need(PreferenceCaptured && Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefOpenReadOnly), "REFERENCES_READONLY_POLICY_NOT_ACTIVE");
            ExpectedDrawingPath = path; ExpectedDrawingReadOnly = readOnly;
            int error = 0, warning = 0;
            ModelDoc2 doc = Sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocDRAWING,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (readOnly ? (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly : 0), "", ref error, ref warning) as ModelDoc2;
            Need(doc != null && error == 0 && (warning & 128) == 0 && Same(doc.GetPathName(), path) && doc.IsOpenedReadOnly() == readOnly,
                "DRAWING_OPEN_FAILED: " + path + " error=" + error + " warning=" + warning);
            return doc;
        }
        static void CloseDocuments()
        {
            Need(Owned && Sw != null && OwnedProcess != null && !OwnedProcess.HasExited && Sw.GetProcessID() == OwnedProcess.Id && OwnedProcess.StartTime.ToUniversalTime() == OwnedStartUtc, "CLOSE_REFUSED_FOR_UNPROVEN_SESSION");
            Sw.CloseAllDocuments(true);
            Need(Sw.GetFirstDocument() == null, "NATIVE_DOCUMENTS_REMAIN_OPEN; models were never saved by this tool");
            ExpectedDrawingPath = null;
        }
        static void DrawingDependencies(string path, string expected, bool candidate)
        {
            string[] direct = Dependencies(path, false);
            Need(direct.Length == 1 && Same(direct[0], expected), "UNMAPPED_DRAWING_REFERENCE: " + path + " expected=" + expected + " actual=" + string.Join(";", direct));
            if (candidate) foreach (string dependency in Dependencies(path, true))
                Need(Inside(dependency, Input.packageDirectory), "REOPENED_DRAWING_REFERENCE_OUTSIDE_ACCEPTED_PACKAGE: " + dependency);
        }
        static DrawingState Inspect(DrawingDoc drawing, string expectedModel, string configuration)
        {
            var state = new DrawingState();
            state.sheets = ((Array)drawing.GetSheetNames()).Cast<string>().ToArray();
            Need(state.sheets.Length > 0 && state.sheets.Distinct().Count() == state.sheets.Length, "DRAWING_HAS_NO_UNIQUE_SHEETS");
            foreach (string sheet in state.sheets)
            {
                Need(drawing.ActivateSheet(sheet), "CANNOT_ACTIVATE_DRAWING_SHEET: " + sheet);
                for (SwView view = drawing.GetFirstView() as SwView; view != null; view = view.GetNextView() as SwView)
                {
                    string name = view.GetName2(), model = view.GetReferencedModelName();
                    var dimensions = new List<object>();
                    if (!string.IsNullOrEmpty(model))
                    {
                        Need(Same(model, expectedModel), "UNMAPPED_DRAWING_VIEW: " + sheet + "/" + name + " actual=" + model + " expected=" + expectedModel);
                        ModelDoc2 reference = view.ReferencedDocument as ModelDoc2;
                        Need(reference != null && reference.IsOpenedReadOnly() && Same(reference.GetPathName(), expectedModel), "DRAWING_VIEW_MODEL_UNRESOLVED_OR_NOT_READ_ONLY: " + sheet + "/" + name);
                        Need(configuration == null || view.ReferencedConfiguration == configuration, "DRAWING_CONFIGURATION_MISMATCH: " + sheet + "/" + name + " expected=" + configuration + " actual=" + view.ReferencedConfiguration);
                        state.referencedViewCount++;
                    }
                    foreach (DisplayDimension display in view.GetDisplayDimensions() as Array ?? new object[0])
                    {
                        Annotation annotation = display.GetAnnotation() as Annotation;
                        Dimension dimension = display.GetDimension2(0) as Dimension;
                        string annotationName = annotation == null ? null : annotation.GetName();
                        string identity = sheet + "/" + name + "/" + annotationName;
                        bool dangling = annotation == null || dimension == null || annotation.IsDangling();
                        double? value = dimension == null ? (double?)null : dimension.SystemValue;
                        if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value))) { value = null; dangling = true; }
                        if (string.IsNullOrEmpty(annotationName) || !state.dimensions.Add(identity)) state.dimensionGaps.Add("NATIVE_DIMENSION_IDENTITY_UNAVAILABLE_OR_DUPLICATE: " + identity);
                        if (dangling) state.dimensionGaps.Add("DANGLING_NATIVE_DIMENSION: " + identity);
                        if (!string.IsNullOrEmpty(model)) state.modelDimensionCount++;
                        dimensions.Add(new { identity = identity, selectionName = display.GetNameForSelection(), dangling = dangling, valueInSystemUnits = value, position = annotation == null ? null : annotation.GetPosition() });
                    }
                    state.views.Add(new Dictionary<string, object> { { "sheet", sheet }, { "name", name }, { "model", model }, { "configuration", view.ReferencedConfiguration }, { "outlineMetres", view.GetOutline() }, { "dimensions", dimensions } });
                }
            }
            Need(state.referencedViewCount > 0, "DRAWING_HAS_NO_RESOLVED_MODEL_VIEWS");
            return state;
        }
        static void NeedDimensions(DrawingState state, DrawingState template, string context)
        {
            Need(state.dimensionGaps.Count == 0, context + ": " + string.Join("; ", state.dimensionGaps));
            Need(state.modelDimensionCount > 0, "NO_NATIVE_MODEL_DIMENSIONS_SURVIVED: " + context + "; explicit template-to-candidate dimension mapping is required");
            if (template == null) return;
            string[] missing = template.dimensions.Except(state.dimensions).ToArray(), added = state.dimensions.Except(template.dimensions).ToArray();
            string[] templateViews = template.views.Where(view => !string.IsNullOrEmpty((string)view["model"]))
                .Select(view => (string)view["sheet"] + "/" + (string)view["name"]).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] candidateViews = state.views.Where(view => !string.IsNullOrEmpty((string)view["model"]))
                .Select(view => (string)view["sheet"] + "/" + (string)view["name"]).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Need(templateViews.Length == template.referencedViewCount && candidateViews.Length == state.referencedViewCount &&
                templateViews.SequenceEqual(candidateViews, StringComparer.Ordinal) && state.referencedViewCount == template.referencedViewCount,
                "NATIVE_MODEL_VIEW_SET_NOT_PRESERVED: " + context + " missing=" + string.Join(";", templateViews.Except(candidateViews)) + " unexpected=" + string.Join(";", candidateViews.Except(templateViews)));
            Need(missing.Length == 0 && added.Length == 0 && state.modelDimensionCount == template.modelDimensionCount && template.sheets.SequenceEqual(state.sheets),
                "NATIVE_DIMENSIONS_NOT_PRESERVED: " + context + " missing=" + string.Join(";", missing) + " unexpected=" + string.Join(";", added));
        }
        static void Activate(ModelDoc2 doc)
        {
            int error = 0;
            Need(Sw.ActivateDoc3(doc.GetTitle(), false, 0, ref error) != null && error == 0, "CANNOT_ACTIVATE_DRAWING: " + doc.GetPathName());
            doc.Visible = true; doc.GraphicsRedraw2();
        }
        sealed class SignatureNote
        {
            internal Annotation annotation;
            internal Note note;
            internal double[] position;
        }
        static List<object> Signatures(DrawingDoc drawing, bool update)
        {
            var result = new List<object>();
            foreach (string sheet in (Array)drawing.GetSheetNames())
            {
                Need(drawing.ActivateSheet(sheet), "CANNOT_ACTIVATE_SIGNATURE_SHEET: " + sheet);
                drawing.EditSheet();
                SwView view = drawing.GetFirstView() as SwView;
                Need(view != null, "MISSING_TITLE_BLOCK_VIEW: " + sheet);
                var notes = new List<SignatureNote>();
                for (Annotation annotation = view.GetFirstAnnotation3(); annotation != null; annotation = annotation.GetNext3())
                {
                    Note note = annotation.GetSpecificAnnotation() as Note;
                    if (note != null) notes.Add(new SignatureNote { annotation = annotation, note = note, position = annotation.GetPosition() as double[] });
                }
                foreach (string role in new[] { "设计", "绘图" })
                {
                    var labels = notes.Where(n => n.position != null && string.Concat(n.note.GetText().Where(c => !char.IsWhiteSpace(c))) == role).ToArray();
                    Need(labels.Length == 1, "TITLE_BLOCK_ROLE_NOT_UNIQUE: " + sheet + "/" + role);
                    SignatureNote label = labels[0];
                    var values = notes.Where(n => n.position != null && Math.Abs(n.position[1] - label.position[1]) < .0005 && n.position[0] - label.position[0] > .004 && n.position[0] - label.position[0] < .03).ToArray();
                    Need(values.Length == 1, "TITLE_BLOCK_SIGNATURE_CELL_NOT_UNIQUE: " + sheet + "/" + role);
                    SignatureNote value = values[0]; string before = value.note.GetText();
                    if (update)
                    {
                        value.note.PropertyLinkedText = "<FONT name=\"Arial\">Winnsen Bot";
                        TextFormat format = value.annotation.GetTextFormat(0) as TextFormat;
                        Need(format != null, "TITLE_BLOCK_FORMAT_MISSING: " + role);
                        format.TypeFaceName = "Arial"; format.CharHeight = .0021;
                        Need(value.annotation.SetTextFormat(0, false, format), "TITLE_BLOCK_FORMAT_FAILED: " + role);
                        value.note.SetTextJustification(2);
                    }
                    Need(value.note.GetText() == "Winnsen Bot" && !value.note.PropertyLinkedText.Contains("$PRP") && Math.Abs(((TextFormat)value.annotation.GetTextFormat(0)).CharHeight - .0021) < 1e-8, "TITLE_BLOCK_SIGNATURE_NOT_RETAINED: " + role);
                    result.Add(new { sheet = sheet, role = role, previous = before, text = value.note.GetText(), positionMetres = value.annotation.GetPosition(), characterHeightMetres = .0021 });
                }
            }
            return result;
        }
        static void ExportPdf(ModelDoc2 doc, string path, string[] sheets)
        {
            Need(!File.Exists(path), "PRESERVE_PRIOR_PDF: " + path);
            ExportPdfData pdf = Sw.GetExportFileData(1) as ExportPdfData;
            Need(pdf != null, "NATIVE_PDF_EXPORTER_UNAVAILABLE");
            pdf.ViewPdfAfterSaving = false; pdf.ExportAs3D = false;
            Need(pdf.SetSheets(sheets == null ? (int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets : (int)swExportDataSheetsToExport_e.swExportData_ExportSpecifiedSheets, sheets), "CANNOT_SELECT_PDF_SHEETS");
            int error = 0, warning = 0;
            Need(doc.Extension.SaveAs(path, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, pdf, ref error, ref warning) && error == 0 && File.Exists(path) && new FileInfo(path).Length > 100,
                "NATIVE_PDF_EXPORT_FAILED: " + path + " error=" + error + " warning=" + warning);
            using (var stream = File.OpenRead(path))
            {
                var header = new byte[5]; Need(stream.Read(header, 0, header.Length) == header.Length && Encoding.ASCII.GetString(header) == "%PDF-", "INVALID_EXPORTED_PDF: " + path);
            }
        }
        static void Build(Entry entry, List<object> jobs)
        {
            Need(Sw.GetFirstDocument() == null, "DRAWING_JOB_REQUIRES_EMPTY_OWNED_SESSION");
            string drawingPath = Path.Combine(Input.output, "native", entry.outName), stem = Path.GetFileNameWithoutExtension(entry.outName);
            string pdfPath = Path.Combine(Input.output, "pdf", stem + ".pdf");
            var row = new Dictionary<string, object> { { "outName", entry.outName }, { "templatePath", entry.templatePath }, { "sourceModelPath", entry.sourceModelPath }, { "candidateModelPath", entry.candidateModelPath }, { "configuration", entry.configuration }, { "status", "running" } };
            jobs.Add(row); Flush();
            try
            {
                Protect(entry.templatePath); Protect(entry.sourceModelPath); Protect(entry.candidateModelPath);
                DrawingDependencies(entry.templatePath, entry.sourceModelPath, false);
                List<string> sources = ModelClosure(entry.sourceModelPath, false);
                OpenModels(sources, entry.sourceModelPath);
                ModelDoc2 original = OpenDrawing(entry.templatePath, true);
                NeedReadOnlyModels(sources);
                DrawingState template = Inspect((DrawingDoc)original, entry.sourceModelPath, null);
                NeedReadOnlyModels(sources);
                row["templateViews"] = template.views; row["templateNativeDimensionCount"] = template.modelDimensionCount;
                NeedDimensions(template, null, "template " + entry.templatePath);
                CloseDocuments();
                Need(!File.Exists(drawingPath), "PRESERVE_PRIOR_DRAWING: " + drawingPath);
                File.Copy(entry.templatePath, drawingPath, false); File.SetAttributes(drawingPath, FileAttributes.Normal);
                Need(Sw.ReplaceReferencedDocument(drawingPath, entry.sourceModelPath, entry.candidateModelPath), "EXPLICIT_DRAWING_REFERENCE_REPLACEMENT_FAILED: " + entry.outName);
                DrawingDependencies(drawingPath, entry.candidateModelPath, true);
                row["referenceReplacement"] = new { source = entry.sourceModelPath, candidate = entry.candidateModelPath, copyOnly = true };
                List<string> candidates = ModelClosure(entry.candidateModelPath, true);
                ModelDoc2 target = OpenModels(candidates, entry.candidateModelPath);
                Need(target != null && ((Array)target.GetConfigurationNames()).Cast<string>().Contains(entry.configuration), "EXPLICIT_CANDIDATE_CONFIGURATION_MISSING: " + entry.configuration);
                ModelDoc2 doc = OpenDrawing(drawingPath, false);
                NeedReadOnlyModels(candidates); Activate(doc);
                DrawingDoc drawing = (DrawingDoc)doc;
                foreach (string sheet in (Array)drawing.GetSheetNames())
                {
                    Need(drawing.ActivateSheet(sheet), "CANNOT_ACTIVATE_DRAWING_SHEET: " + sheet);
                    for (SwView view = drawing.GetFirstView() as SwView; view != null; view = view.GetNextView() as SwView)
                    {
                        string reference = view.GetReferencedModelName();
                        if (string.IsNullOrEmpty(reference)) continue;
                        Need(Same(reference, entry.candidateModelPath), "UNMAPPED_DRAWING_VIEW_AFTER_REPLACEMENT: " + reference);
                        if (!view.LinkParentConfiguration || view.GetBaseView() == null) view.ReferencedConfiguration = entry.configuration;
                        view.UpdateViewDisplayGeometry();
                    }
                }
                Need(doc.ForceRebuild3(false) && doc.EditRebuild3(), "NATIVE_DRAWING_REBUILD_FAILED: " + entry.outName);
                DrawingState mapped = Inspect(drawing, entry.candidateModelPath, entry.configuration);
                NeedReadOnlyModels(candidates);
                row["mappedViews"] = mapped.views; NeedDimensions(mapped, template, "mapped " + entry.outName);
                row["signaturesApplied"] = Signatures(drawing, true);
                NeedReadOnlyModels(candidates);
                int saveError = 0, saveWarning = 0;
                Need(doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveError, ref saveWarning) && saveError == 0, "NATIVE_DRAWING_SAVE_FAILED: " + entry.outName + " error=" + saveError + " warning=" + saveWarning);
                string savedDrawingHash = Hash(drawingPath); row["saveWarning"] = saveWarning;
                CloseDocuments();
                DrawingDependencies(drawingPath, entry.candidateModelPath, true);
                OpenModels(candidates, entry.candidateModelPath);
                doc = OpenDrawing(drawingPath, true); drawing = (DrawingDoc)doc;
                NeedReadOnlyModels(candidates); Activate(doc);
                DrawingState reopened = Inspect(drawing, entry.candidateModelPath, entry.configuration);
                NeedReadOnlyModels(candidates);
                row["reopenedViews"] = reopened.views; NeedDimensions(reopened, template, "reopened " + entry.outName);
                row["signaturesReopened"] = Signatures(drawing, false);
                row["nativeDimensionsPreserved"] = true; row["candidateNativeDimensionCount"] = reopened.modelDimensionCount;
                row["danglingDimensionCount"] = 0; row["candidateLocalReferences"] = true; row["reopenedReadOnly"] = doc.IsOpenedReadOnly();
                ExportPdf(doc, pdfPath, null);
                var pages = new List<object>(); row["pages"] = pages;
                for (int i = 0; i < reopened.sheets.Length; i++)
                {
                    string sheetName = reopened.sheets[i], suffix = "__page_" + (i + 1).ToString("000");
                    Need(drawing.ActivateSheet(sheetName), "CANNOT_ACTIVATE_PDF_PAGE: " + sheetName);
                    Sheet sheet = drawing.GetCurrentSheet() as Sheet;
                    double width = 0, height = 0; Need(sheet != null, "DRAWING_SHEET_UNAVAILABLE: " + sheetName); int paperSize = sheet.GetSize(ref width, ref height);
                    Need(width > 0 && height > 0, "DRAWING_PAPER_SIZE_UNAVAILABLE: " + sheetName);
                    string pagePdf = Path.Combine(Input.output, "pdf", stem + suffix + ".pdf"), bitmap = Path.Combine(Input.output, "pages", stem + suffix + ".bmp");
                    ExportPdf(doc, pagePdf, new[] { sheetName });
                    doc.ViewZoomtofit2(); doc.GraphicsRedraw2();
                    int imageWidth = width >= height ? 3200 : Math.Max(1, (int)Math.Round(3200 * width / height));
                    int imageHeight = height >= width ? 3200 : Math.Max(1, (int)Math.Round(3200 * height / width));
                    Need(!File.Exists(bitmap) && doc.SaveBMP(bitmap, imageWidth, imageHeight) && File.Exists(bitmap) && new FileInfo(bitmap).Length > 100, "NATIVE_DRAWING_PAGE_CAPTURE_FAILED: " + entry.outName + "/" + sheetName);
                    pages.Add(new { index = i + 1, sheet = sheetName, paperSize = paperSize, paperWidthMetres = width, paperHeightMetres = height, imageWidth = imageWidth, imageHeight = imageHeight, image = Artifact(bitmap), pdf = Artifact(pagePdf), views = reopened.views.Where(view => (string)view["sheet"] == sheetName).ToArray() });
                    Flush();
                }
                Need(Hash(drawingPath) == savedDrawingHash, "REOPEN_OR_PDF_EXPORT_CHANGED_SAVED_DRAWING: " + drawingPath);
                NeedReadOnlyModels(candidates);
                row["drawing"] = Artifact(drawingPath); row["pdf"] = Artifact(pdfPath); row["status"] = "exported_for_engineering_review"; Flush();
            }
            catch (Exception ex) { row["status"] = "failed"; row["error"] = ex.Message; Flush(); throw; }
            finally { CloseDocuments(); row["closedWithoutSavingModels"] = true; Flush(); }
        }
        static void ValidateInput(string invocationPath)
        {
            Need(Input.schema == "winnsen.locker16029.production_drawings_run.v1" && Input.classification == "engineering-review-only" && !Input.productionReleased, "INVALID_DRAWING_RUN_CONTRACT");
            Plain(Input.output, true); Plain(Input.packageDirectory, true);
            Need(Same(Path.GetDirectoryName(invocationPath), Input.output), "DRAWING_INVOCATION_OUTSIDE_OUTPUT");
            Need(!Inside(Input.output, Input.attempt) && !Inside(Input.attempt, Input.output), "DRAWING_OUTPUT_OVERLAPS_ACCEPTED_ATTEMPT");
            Need(Same(Input.packageDirectory, Path.Combine(Input.attempt, "package")), "DRAWING_PACKAGE_OUTSIDE_ACCEPTED_ATTEMPT");
            Need(Input.productionEvidence != null && Input.productionEvidence.ContainsKey("ok") && (bool)Input.productionEvidence["ok"] && (string)Input.productionEvidence["throughPhase"] == "package" && (string)Input.productionEvidence["planSha256"] == Input.executionPlanSha256, "PRODUCTION_PACKAGE_EVIDENCE_BINDING_MISSING");
            Protect(Input.planPath, Input.planSha256); Protect(Input.executionPlanPath, Input.executionPlanSha256); Protect(Input.archivePath, Input.archiveSha256);
            Protect(invocationPath); Protect(Input.buildManifestPath, Input.buildManifestSha256);
            Need(Same(Input.executablePath, Assembly.GetExecutingAssembly().Location), "DRAWING_EXECUTABLE_PATH_MISMATCH"); Protect(Input.executablePath, Input.executableSha256);
            Need(Input.protectedFiles != null && Input.protectedFiles.Length > 0 && Input.packageInventory != null && Input.packageInventory.Length > 0, "DRAWING_PROTECTED_INVENTORY_MISSING");
            foreach (FileRow row in Input.protectedFiles) Protect(row.path, row.sha256);
            foreach (FileRow row in Input.packageInventory) { Need(Inside(row.path, Input.packageDirectory), "PACKAGE_INVENTORY_ESCAPE"); Protect(row.path, row.sha256); PackageBefore.Add(Full(row.path), row.sha256); }
            Need(SameInventory(PackageBefore, Inventory(Input.packageDirectory)), "ACCEPTED_PACKAGE_CHANGED_BEFORE_DRAWING_EXPORT");
            Need(Input.entries != null && Input.entries.Length > 0, "EXPLICIT_DRAWING_ENTRIES_REQUIRED");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry entry in Input.entries)
            {
                Plain(entry.templatePath, false); Plain(entry.sourceModelPath, false); Plain(entry.candidateModelPath, false);
                Need(entry.templatePath.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase) && IsCad(entry.sourceModelPath) && IsCad(entry.candidateModelPath) && !Same(entry.sourceModelPath, entry.candidateModelPath), "INVALID_EXPLICIT_DRAWING_MAPPING");
                Need(!Inside(entry.templatePath, Input.attempt) && !Inside(entry.sourceModelPath, Input.attempt) && Inside(entry.candidateModelPath, Input.packageDirectory), "DRAWING_SOURCE_CANDIDATE_BOUNDARY_INVALID");
                Need(!string.IsNullOrWhiteSpace(entry.configuration) && entry.outName != null && Path.GetFileName(entry.outName) == entry.outName && entry.outName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && entry.outName.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase) && names.Add(entry.outName), "INVALID_OR_DUPLICATE_EXPLICIT_DRAWING_OUTPUT");
                Need(!Inside(Input.output, Path.GetDirectoryName(entry.templatePath)) && !Inside(Input.output, Path.GetDirectoryName(entry.sourceModelPath)), "DRAWING_OUTPUT_OVERLAPS_PROTECTED_SOURCE");
            }
            foreach (string folder in new[] { "native", "pdf", "pages" }) { string path = Plain(Path.Combine(Input.output, folder), true); Need(Directory.GetFileSystemEntries(path).Length == 0, "DRAWING_OUTPUT_NOT_FRESH: " + path); }
        }
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length != 1) { Console.Error.WriteLine("An explicit wrapper-created drawing run.json is required; no template discovery is supported."); return 1; }
            try
            {
                string invocationPath = Plain(args[0], false);
                Input = Json.Deserialize<RunInput>(File.ReadAllText(invocationPath, Encoding.UTF8));
                ValidateInput(invocationPath);
                EvidencePath = Path.Combine(Input.output, "native-drawings.evidence.json");
                Need(!File.Exists(EvidencePath), "PRESERVE_EXISTING_DRAWING_EVIDENCE: " + EvidencePath);
                using (var reservation = new FileStream(EvidencePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
                EvidenceReserved = true;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
            var jobs = new List<object>();
            Evidence = new Dictionary<string, object> { { "schema", "winnsen.locker16029.production_drawings_evidence.v1" }, { "classification", "engineering-review-only" }, { "productionReleased", false }, { "status", "running" }, { "success", false }, { "planPath", Input.planPath }, { "planSha256", Input.planSha256 }, { "executionPlanSha256", Input.executionPlanSha256 }, { "archiveSha256", Input.archiveSha256 }, { "productionEvidence", Input.productionEvidence }, { "executableSha256", Input.executableSha256 }, { "startedAt", DateTimeOffset.Now.ToString("o") }, { "jobs", jobs }, { "documentsClosed", false }, { "sessionExited", false } };
            Evidence["modelDependencyOrders"] = ModelDependencyOrders;
            Evidence["modelOpenMode"] = "root_only_with_readonly_references"; Evidence["modelOpenEvents"] = ModelOpenEvents;
            int exitCode = 1;
            try
            {
                Flush();
                Need(Process.GetProcessesByName("SLDWORKS").Length == 0, "NATIVE_CAD_BUSY; an exclusive new SOLIDWORKS session is required");
                DateTime started = DateTime.UtcNow;
                Sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks;
                Need(Sw != null, "SOLIDWORKS_2020_UNAVAILABLE");
                OwnedProcess = Process.GetProcessById(Sw.GetProcessID());
                OwnedStartUtc = OwnedProcess.StartTime.ToUniversalTime();
                Owned = OwnedStartUtc >= started.AddSeconds(-3) && Process.GetProcessesByName("SLDWORKS").Length == 1 && Sw.GetFirstDocument() == null;
                Need(Owned, "EXCLUSIVE_NEW_NATIVE_SESSION_NOT_PROVEN");
                Need(Sw.RevisionNumber().StartsWith("28."), "SOLIDWORKS_2020_REQUIRED");
                Evidence["processId"] = OwnedProcess.Id; Evidence["processStartUtc"] = OwnedProcess.StartTime.ToUniversalTime().ToString("o"); Evidence["nativeRevision"] = Sw.RevisionNumber(); Evidence["ownedSession"] = Owned;
                Sw.Visible = true; Sw.UserControl = true; Sw.CommandInProgress = false;
                Sw.DocumentVisible(true, (int)swDocumentTypes_e.swDocPART); Sw.DocumentVisible(true, (int)swDocumentTypes_e.swDocASSEMBLY); Sw.DocumentVisible(true, (int)swDocumentTypes_e.swDocDRAWING);
                Flush();
                BeginReferenceReadOnlyPolicy();
                foreach (Entry entry in Input.entries) Build(entry, jobs);
                Evidence["status"] = "exports_completed_pending_cleanup"; exitCode = 0;
            }
            catch (Exception ex) { Evidence["status"] = "failed"; Evidence["error"] = ex.ToString(); Console.Error.WriteLine(ex.ToString()); }
            finally
            {
                var errors = new List<string>();
                if (Sw != null && Owned)
                {
                    try { CloseDocuments(); Evidence["documentsClosed"] = true; Evidence["closeWithoutSavingModels"] = true; }
                    catch (Exception ex) { errors.Add("Close owned documents: " + ex.Message); }
                    try { RestoreReferenceReadOnlyPolicy(); }
                    catch (Exception ex) { Evidence["referencesReadonlyPreferenceRestored"] = false; errors.Add("Restore read-only reference preference: " + ex.Message); }
                    try { Need(!OwnedProcess.HasExited && Sw.GetProcessID() == OwnedProcess.Id && OwnedProcess.StartTime.ToUniversalTime() == OwnedStartUtc, "EXIT_REFUSED_FOR_CHANGED_PROCESS_IDENTITY"); Sw.ExitApp(); Marshal.FinalReleaseComObject(Sw); Sw = null; GC.Collect(); GC.WaitForPendingFinalizers(); Evidence["sessionExited"] = OwnedProcess.WaitForExit(15000); Need((bool)Evidence["sessionExited"], "OWNED_NATIVE_SESSION_DID_NOT_EXIT"); }
                    catch (Exception ex) { errors.Add("Exit owned native session: " + ex.Message); }
                }
                try
                {
                    Evidence["protectedFilesBefore"] = ProtectedRows();
                    string[] changed = Protected.Where(pair => !File.Exists(pair.Key) || Hash(Plain(pair.Key, false)) != pair.Value).Select(pair => pair.Key).ToArray();
                    Evidence["changedProtectedFiles"] = changed; Evidence["modelsUnchanged"] = changed.Length == 0;
                    Need(changed.Length == 0, "PROTECTED_MODEL_TEMPLATE_OR_BINDING_CHANGED: " + string.Join(";", changed));
                    Evidence["packageUnchanged"] = SameInventory(PackageBefore, Inventory(Input.packageDirectory));
                    Need((bool)Evidence["packageUnchanged"], "ACCEPTED_PACKAGE_INVENTORY_CHANGED_DURING_DRAWING_EXPORT");
                    Evidence["protectedFilesAfter"] = ProtectedRows();
                }
                catch (Exception ex) { errors.Add("Verify preserved input bytes: " + ex.Message); }
                if (PreferenceCaptured && !PreferenceRestored) errors.Add("Read-only reference preference restoration was not proven");
                if (errors.Count > 0) exitCode = 1;
                Evidence["success"] = exitCode == 0; Evidence["status"] = exitCode == 0 ? "exported_for_engineering_review" : "failed";
                Evidence["cleanupErrors"] = errors; Evidence["finishedAt"] = DateTimeOffset.Now.ToString("o");
                try { Flush(); }
                catch (Exception ex) { Evidence["success"] = false; exitCode = 1; Console.Error.WriteLine("EVIDENCE_WRITE_FAILED: " + ex); }
            }
            return exitCode;
        }
    }
}
