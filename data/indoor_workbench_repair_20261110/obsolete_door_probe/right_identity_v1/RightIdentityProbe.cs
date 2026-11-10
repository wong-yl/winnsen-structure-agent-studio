// Throwaway read-only identity diagnostic. Raw warnings never accept a model.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

internal static class RightIdentityProbe
{
    private const string OwnedRoot = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_identity_v1";
    private const string Attempt = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110111643911-CE439B";
    private const string CadExecutable = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
    private const string LocksSha = "578300923B8435204695EC9A4512D90068C4E1320EB2AAA6D83390FAFEFE4409";
    private static readonly string[] Arms = { "right-only", "parameter-source-only", "left-close-right" };
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
    private static readonly List<object> Events = new List<object>();
    private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static FileStream Journal;
    private static DateTime Deadline;
    private static string ManifestPath, ManifestSha;
    private static Binding Input;
    private static bool LoggingFailed;
    public sealed class FilePin { public string path, sha256; public long size; }
    public sealed class Binding { public string schema, sourceAttempt; public FilePin locksEvidence, right, parameterSource, left; public FilePin[] inventory; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string shortPath, StringBuilder longPath, uint bufferLength);
    private static Dictionary<string, object> D(params object[] values) { var row = new Dictionary<string, object>(); for (int i = 0; i < values.Length; i += 2) row[(string)values[i]] = values[i + 1]; return row; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string Full(string path) { Require(!string.IsNullOrWhiteSpace(path), "EMPTY_PATH"); return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
    private static bool Same(string a, string b) { return string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase); }
    private static bool Inside(string root, string path) { return Full(path).StartsWith(Full(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
    private static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
    private static void Plain(string path) { for (string current = Full(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) if (File.Exists(current) || Directory.Exists(current)) Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "REPARSE_PATH_REJECTED " + current); }
    private static string CanonicalExecutable(string path) { var buffer = new StringBuilder(32768); uint length = GetLongPathName(Full(path), buffer, (uint)buffer.Capacity); Require(length > 0 && length < buffer.Capacity, "CANONICAL_EXECUTABLE_FAILED"); return Full(buffer.ToString()); }
    private static int[] Pids(string name) { Process[] rows = Process.GetProcessesByName(name); try { return rows.Select(p => p.Id).OrderBy(p => p).ToArray(); } finally { foreach (Process row in rows) row.Dispose(); } }
    private static void CheckTime() { Require(DateTime.UtcNow < Deadline, "DIAGNOSTIC_DEADLINE_REACHED"); }
    private static void Record(object row) { Events.Add(row); byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(D("schema", "right-identity-diagnostic-v1", "events", Events))); Journal.Position = 0; Journal.Write(bytes, 0, bytes.Length); Journal.SetLength(bytes.Length); Journal.Flush(true); }
    private static void SafeRecord(object row) { try { Record(row); } catch (Exception error) { LoggingFailed = true; Console.Error.WriteLine(error); } }
    private static object Read(Func<object> action) { try { return action(); } catch (Exception error) { return D("readError", error.ToString()); } }
    private static object[] Values(object raw) { return raw is Array || raw is IList ? ((IEnumerable)raw).Cast<object>().ToArray() : new object[0]; }
    private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    // Hold each distinct IUnknown until the entire traversal ends, so its address cannot be reused.
    private sealed class ComIdentityScope : IDisposable
    {
        private readonly HashSet<long> keys = new HashSet<long>();
        private readonly List<IntPtr> held = new List<IntPtr>();
        internal bool First(object value) { if (value == null) return false; IntPtr pointer = Marshal.GetIUnknownForObject(value); if (!keys.Add(pointer.ToInt64())) { Marshal.Release(pointer); return false; } held.Add(pointer); return true; }
        public void Dispose() { foreach (IntPtr pointer in held) Marshal.Release(pointer); held.Clear(); keys.Clear(); }
    }
    private static bool Bound(string path) { try { return Known.ContainsKey(Full(path)); } catch { return false; } }
    private static object Identity(ModelDoc2 model)
    {
        if (model == null) return null;
        object path = Read(delegate { return model.GetPathName(); });
        return D("actualPath", path, "title", Read(delegate { return model.GetTitle(); }), "readOnly", Read(delegate { return model.IsOpenedReadOnly(); }), "saveFlag", Read(delegate { return model.GetSaveFlag(); }), "insideFixedInventory", path is string && Bound((string)path));
    }
    private static void Documents(ISldWorks sw, string stage)
    {
        var rows = new List<object>(); object raw = sw.GetDocuments();
        using (var identities = new ComIdentityScope()) foreach (object value in Values(raw)) if (identities.First(value)) rows.Add(Identity(value as ModelDoc2));
        Record(D("inspection", "loaded_documents", "stage", stage, "documentCount", sw.GetDocumentCount(), "rawArrayLength", Values(raw).Length, "documents", rows, "unknownPathsAccepted", false));
    }
    private static object References(ModelDoc2 model)
    {
        object files, parts, features, types, statuses, entities, components;
        model.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
        return D("files", files, "parts", parts, "features", features, "types", types, "statuses", statuses, "entities", entities, "components", components, "fileWithinFixedInventory", Values(files).Select(value => Bound(Convert.ToString(value))).ToArray(), "unknownPathsAccepted", false);
    }
    private static void FeatureReferences(Feature first, bool sub, int depth, ComIdentityScope identities, List<object> rows)
    {
        Require(depth <= 32 && rows.Count < 10000, "FEATURE_TRAVERSAL_BOUND_EXCEEDED");
        for (Feature feature = first; feature != null; feature = (sub ? feature.GetNextSubFeature() : feature.GetNextFeature()) as Feature)
        {
            CheckTime(); if (!identities.First(feature)) break;
            string name = feature.Name;
            object refs = Read(delegate { object files, parts, features, types, statuses, entities, components; int configOption; string configName; feature.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components, out configOption, out configName); return D("files", files, "parts", parts, "features", features, "types", types, "statuses", statuses, "entities", entities, "components", components, "configOption", configOption, "configName", configName, "fileWithinFixedInventory", Values(files).Select(value => Bound(Convert.ToString(value))).ToArray(), "unknownPathsAccepted", false); });
            rows.Add(D("name", name, "type", feature.GetTypeName2(), "depth", depth, "references", refs));
            FeatureReferences(feature.GetFirstSubFeature() as Feature, true, depth + 1, identities, rows);
        }
    }
    private static Dictionary<string, object> ParseDependencies(object raw)
    {
        bool array = raw is Array || raw is IList; object[] values = array ? Values(raw) : null;
        bool parsed = raw == null || (array && values.Length % 2 == 0 && values.All(value => value is string));
        var pairs = new List<object>(); if (parsed && values != null) for (int i = 0; i < values.Length; i += 2) pairs.Add(D("name", values[i], "path", values[i + 1], "insideFixedInventory", Bound((string)values[i + 1])));
        return D("raw", raw, "rawNull", raw == null, "rawType", raw == null ? null : raw.GetType().FullName, "namePathPairParseSucceeded", parsed, "pairs", pairs, "returnedPathsLoaded", false, "returnedPathsWhitelisted", false, "modelAccepted", false);
    }
    private static void Inspect(ISldWorks sw, ModelDoc2 model, string path, string stage)
    {
        Record(D("inspection", "file_external_references", "stage", stage, "requestedPath", path, "actualDocument", Identity(model), "raw", Read(delegate { return References(model); })));
        var rows = new List<object>(); using (var identities = new ComIdentityScope()) { try { FeatureReferences(model.FirstFeature() as Feature, false, 0, identities, rows); } catch (Exception error) { rows.Add(D("traversalError", error.ToString())); } }
        Record(D("inspection", "feature_external_references", "stage", stage, "requestedPath", path, "rows", rows, "unknownPathsAccepted", false));
    }
    private static void Dependencies(ISldWorks sw, string path)
    {
        foreach (bool traverse in new[] { false, true }) { CheckTime(); Record(D("inspection", "document_dependencies", "requestedPath", path, "traverse", traverse, "search", false, "addReadOnlyInfo", false, "result", Read(delegate { return ParseDependencies(sw.GetDocumentDependencies2(path, traverse, false, false)); }))); }
    }
    private static ModelDoc2 Open(ISldWorks sw, string path, string stage)
    {
        CheckTime(); CheckBindings(); Documents(sw, stage + ":before-open");
        object loaded = Read(delegate { return Identity(sw.GetOpenDocumentByName(path) as ModelDoc2); });
        Record(D("inspection", "open_request", "stage", stage, "path", path, "documentType", 1, "options", 3, "alreadyLoaded", loaded));
        int errors = 0, warnings = 0; ModelDoc2 model = null; string exception = null;
        try { model = sw.OpenDoc6(path, 1, 3, "", ref errors, ref warnings) as ModelDoc2; } catch (Exception error) { exception = error.ToString(); }
        Record(D("inspection", "open_result", "stage", stage, "requestedPath", path, "errors", errors, "warnings", warnings, "idMismatch", (warnings & 1) != 0, "needsRegen", (warnings & 32) != 0, "actual", Identity(model), "exception", exception, "warning33IsDiagnosticOnly", true, "modelAccepted", false));
        Documents(sw, stage + ":after-open");
        Require(model != null && errors == 0 && model.IsOpenedReadOnly(), "READ_ONLY_OPEN_FAILED " + stage);
        Inspect(sw, model, path, stage); return model;
    }
    private static string[] ArmSequence(string arm) { Require(Arms.Contains(arm), "UNKNOWN_ARM"); return arm == "left-close-right" ? new[] { "left", "right" } : new[] { arm == "right-only" ? "right" : "parameterSource" }; }
    private static void CheckPin(FilePin pin, string expectedPath, string expectedSha) { Require(pin != null && Same(pin.path, expectedPath) && pin.sha256 == expectedSha && Hash(pin.path) == pin.sha256, "FIXED_SOURCE_PIN_CHANGED " + expectedPath); Plain(pin.path); }
    private static void CheckBindings()
    {
        Require(Hash(ManifestPath) == ManifestSha, "BINDING_MANIFEST_CHANGED");
        foreach (FilePin pin in Input.inventory) Require(Hash(pin.path) == pin.sha256 && new FileInfo(pin.path).Length == pin.size, "SOURCE_INVENTORY_CHANGED " + pin.path);
        Require(Hash(Input.locksEvidence.path) == LocksSha, "LOCKS_EVIDENCE_CHANGED");
    }
    private static void LoadBindings(string path, string expected)
    {
        ManifestPath = Full(path); ManifestSha = expected; Require(Same(ManifestPath, Path.Combine(OwnedRoot, "source-binding.json")) && Hash(ManifestPath) == expected, "BINDING_PATH_OR_HASH_MISMATCH"); Plain(ManifestPath);
        Input = Json.Deserialize<Binding>(File.ReadAllText(ManifestPath, Encoding.UTF8)); Require(Input.schema == "right-identity-source-binding-v1" && Same(Input.sourceAttempt, Attempt) && Input.inventory.Length == 119, "SOURCE_INVENTORY_SCOPE_CHANGED");
        CheckPin(Input.locksEvidence, Path.Combine(Attempt, "evidence", "locks.json"), LocksSha);
        string working = Path.Combine(Attempt, "native_cad", "working_pack");
        CheckPin(Input.right, Path.Combine(working, "箱体竖隔板R.SLDPRT"), "1F3C0BBF20E2E4E1811663BBD403D75C8AA9EC00BEF7281BFC94B2A6CCAF95E8");
        CheckPin(Input.parameterSource, Path.Combine(working, "箱体竖隔板R参数源.SLDPRT"), "061C20B4E618F820B63A0D8F1F9F81E22BA21F925DD05D5D0D2CF330056BA428");
        CheckPin(Input.left, Path.Combine(working, "箱体竖隔板L.sldprt"), "7D55237D49B654EC3DC19715DC0681FF3688CDBEC8E1517D1D567A4932508772");
        foreach (FilePin pin in Input.inventory) { Plain(pin.path); Require(Inside(Attempt, pin.path), "INVENTORY_OUTSIDE_ATTEMPT"); Known.Add(Full(pin.path), pin.sha256); }
        CheckBindings();
    }
    private static void VerifyOwner(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable)
    {
        Require(process != null && handle != IntPtr.Zero && process.Handle == handle && !process.HasExited, "OWNED_PROCESS_HANDLE_NOT_LIVE");
        Require(process.Id == pid && process.StartTime.ToUniversalTime().Ticks == ticks && Same(process.MainModule.FileName, executable) && sw.GetProcessID() == pid, "OWNED_PROCESS_IDENTITY_CHANGED");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4 || !Arms.Contains(args[0])) { Console.Error.WriteLine("Usage: RightIdentityProbe.exe <right-only|parameter-source-only|left-close-right> <newOutput> <source-binding.json> <bindingSha>"); return 2; }
        ISldWorks sw = null; Process process = null; IntPtr handle = IntPtr.Zero; int pid = 0; long ticks = 0; string executable = null, errorText = "";
        bool owned = false, exited = false, exitRequested = false, collected = false, filesChecked = false, closed = false;
        try
        {
            string output = Full(args[1]); Require(Inside(Path.Combine(OwnedRoot, "runs"), output) && !File.Exists(output) && Directory.Exists(Path.GetDirectoryName(output)), "OUTPUT_NOT_NEW_OWNED_RUN"); Plain(output);
            Journal = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read); Deadline = DateTime.UtcNow.AddSeconds(180);
            Record(D("inspection", "request", "arm", args[0], "options", 3, "partType", 1, "maximumDiagnosticSeconds", 180, "modelAccepted", false, "operationComplete", false));
            LoadBindings(args[2], args[3]); Require(Pids("SLDWORKS").Length == 0 && Pids("sldProcMon").Length == 0, "EMPTY_CAD_BASELINE_REQUIRED");
            long activation = DateTime.UtcNow.Ticks; sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks; Require(sw != null, "SW2020_ACTIVATION_FAILED");
            pid = sw.GetProcessID(); process = Process.GetProcessById(pid); handle = process.Handle; ticks = process.StartTime.ToUniversalTime().Ticks; executable = process.MainModule.FileName;
            string canonical = CanonicalExecutable(executable), revision = sw.RevisionNumber(); int count = sw.GetDocumentCount();
            owned = handle != IntPtr.Zero && !process.HasExited && ticks >= activation && Same(canonical, CanonicalExecutable(CadExecutable)) && process.MainModule.FileVersionInfo.FileMajorPart == 28 && revision.StartsWith("28.", StringComparison.Ordinal) && Pids("SLDWORKS").SequenceEqual(new[] { pid }) && count == 0;
            Record(D("inspection", "session_owner", "arm", args[0], "cadPid", pid, "startTimeUtcTicks", ticks.ToString(), "startTicks", ticks.ToString(), "executable", executable, "canonicalExecutable", canonical, "revision", revision, "initialDocumentCount", count, "exactOwnershipVerified", owned, "processHandleRetained", handle != IntPtr.Zero));
            Require(owned, "EXCLUSIVE_OWNED_EMPTY_SW2020_REQUIRED"); VerifyOwner(sw, process, handle, pid, ticks, executable);
            Require(sw.SetCurrentWorkingDirectory(Path.Combine(Attempt, "native_cad", "working_pack")), "SESSION_WORKING_DIRECTORY_FAILED");
            if (args[0] == "left-close-right")
            {
                ModelDoc2 left = Open(sw, Input.left.path, "left"); string title = left.GetTitle(); sw.CloseDoc(title);
                Release(left); Record(D("inspection", "left_close", "title", title, "api", "CloseDoc", "closeAllUsed", false)); Documents(sw, "after-left-close-before-right");
                ModelDoc2 right = Open(sw, Input.right.path, "right"); Release(right); Dependencies(sw, Input.left.path); Dependencies(sw, Input.right.path);
            }
            else { string path = args[0] == "right-only" ? Input.right.path : Input.parameterSource.path; ModelDoc2 model = Open(sw, path, args[0]); Release(model); Dependencies(sw, path); }
            Documents(sw, "after-diagnostics"); collected = true;
        }
        catch (Exception error) { errorText = error.ToString(); if (Journal != null) SafeRecord(D("inspection", "read_failure", "exception", errorText)); else Console.Error.WriteLine(errorText); }
        finally
        {
            if (sw != null && owned) try { VerifyOwner(sw, process, handle, pid, ticks, executable); bool result = sw.CloseAllDocuments(true); closed = result && sw.GetDocumentCount() == 0; SafeRecord(D("inspection", "close_all", "discardUnsaved", true, "closed", closed)); VerifyOwner(sw, process, handle, pid, ticks, executable); sw.ExitApp(); exitRequested = true; } catch (Exception error) { errorText += "\nNATURAL_SHUTDOWN_FAILED " + error; }
            try { Release(sw); } catch (Exception error) { errorText += "\nAPPLICATION_RELEASE_FAILED " + error; } sw = null;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GC.WaitForPendingFinalizers();
            if (owned && process != null) try { var timer = Stopwatch.StartNew(); while (timer.ElapsedMilliseconds < 45000) if (process.HasExited || process.WaitForExit(500)) { exited = true; break; } } catch (Exception error) { errorText += "\nNATURAL_EXIT_OBSERVATION_FAILED " + error; }
            if (Journal != null)
            {
                SafeRecord(D("inspection", "natural_exit", "cadPid", pid, "ownedCadExited", exited, "exitRequested", exitRequested, "forcedTermination", false, "remainingCad", Pids("SLDWORKS"), "remainingMonitor", Pids("sldProcMon"), "monitorCleanupOwner", "external observer"));
                try { if (Input != null) { CheckBindings(); filesChecked = true; } } catch (Exception error) { errorText += "\nFINAL_FILE_BINDING_FAILED " + error; }
                bool complete = collected && owned && exited && closed && filesChecked && !LoggingFailed && errorText == "";
                SafeRecord(D("inspection", "completion", "arm", args[0], "diagnosticCompleted", complete, "operationComplete", false, "modelAccepted", false, "strictColdAcceptancePassed", false, "ownedCadExited", exited, "allInputsUnchanged", filesChecked, "noSave", true, "noRebuild", true, "noReplace", true, "unknownPathsAccepted", false, "warningCauseProven", false, "error", errorText));
                Journal.Dispose(); Journal = null; collected = complete && !LoggingFailed;
            }
            if (process != null) process.Dispose();
        }
        return collected ? 0 : 1;
    }
}
