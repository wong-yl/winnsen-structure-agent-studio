// Throwaway closed-file dependency diagnostic. This never opens a CAD document or accepts a model.
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
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

internal static class DependencyProbe
{
    private const string AllowedRoot = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe";
    private const string SourcePack = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110035409405-0C0A4D\native_cad\working_pack";
    private const string CandidatePack = AllowedRoot + @"\candidate_pack_v5";
    private const string RootFile = "标准寄存柜1917×1000×550(总装配).SLDASM";
    private const string ReceiptPath = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110035409405-0C0A4D\receipts\assemblies.json";
    private const string ReceiptSha256 = "1F89B12B1F63D8134ACFB04BA53B24571A0CB57035C6B16FA44F8F4107EDB4BD";
    private const string EvidencePath = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110035409405-0C0A4D\evidence\assemblies.json";
    private const string EvidenceSha256 = "2417CD3A7578A85EA4047D10E7EB6C932C89AD7976C46B8951EBB49D497C6AC2";
    private const string SuccessfulEditEvidencePath = AllowedRoot + @"\runs\20261110065558794-edit-33604\probe.json";
    private const string SuccessfulEditEvidenceSha256 = "FD3965620A9DE4B8F35C9DA3FA9741C556079B044039EB5D32EAE961DFAEF2D9";
    private const string SavedRootSha256 = "1A14C86D381267E95ACDA7BDD825E8BA028261E1F19BB3C6A8D88BA77E5A5DB1";
    private const string CadExecutable = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
    private static readonly List<object> Events = new List<object>();
    private static readonly HashSet<string> RemovedExclusivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ExpectedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static FileStream Journal;
    private static Manifest Binding;
    private static string Mode, Pack, Target, Output, ManifestPath, ManifestSha;
    private static bool LoggingFailed;

    public sealed class FileBinding { public string fileName, sourcePath, path, sha256; public long size; }
    public sealed class PathBinding { public string path, sha256; }
    public sealed class Manifest { public string sourcePack, candidatePack, expectedRootSha256; public FileBinding[] files; public PathBinding sourceReceipt, editEvidence; }
    public sealed class ReceiptFile { public string path, relativePath, sha256; }
    public sealed class Receipt { public string phase, evidenceSha256; public ReceiptFile[] after; }
    private sealed class FileState { public string path, sha256; public long size, ticks; public int attributes; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string shortPath, StringBuilder longPath, uint bufferLength);

    private static Dictionary<string, object> D(params object[] values)
    {
        var row = new Dictionary<string, object>();
        for (int i = 0; i < values.Length; i += 2) row[(string)values[i]] = values[i + 1];
        return row;
    }
    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    private static string Full(string path) { Require(!string.IsNullOrWhiteSpace(path), "EMPTY_PATH"); return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
    private static bool Same(string a, string b) { return string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase); }
    private static bool Inside(string root, string path) { return Full(path).StartsWith(Full(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
    private static void Plain(string path)
    {
        for (string current = Full(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current)) Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "REPARSE_PATH_REJECTED " + current);
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
    }
    private static bool ValidSha(string value) { return value != null && Regex.IsMatch(value, "^[0-9A-F]{64}$"); }
    private static string CanonicalExecutable(string path)
    {
        var buffer = new StringBuilder(32768); uint length = GetLongPathName(Full(path), buffer, (uint)buffer.Capacity);
        if (length == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "GET_LONG_EXECUTABLE_PATH_FAILED");
        Require(length < buffer.Capacity, "LONG_EXECUTABLE_PATH_TOO_LONG"); return Full(buffer.ToString());
    }
    private static void Record(object row)
    {
        Events.Add(row); byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(D("schema", "obsolete-door-closed-dependency-diagnostic-v1", "events", Events)));
        Journal.Position = 0; Journal.Write(bytes, 0, bytes.Length); Journal.SetLength(bytes.Length); Journal.Flush(true);
    }
    private static void SafeRecord(object row) { try { Record(row); } catch (Exception error) { LoggingFailed = true; Console.Error.WriteLine(error); } }
    private static object[] ArrayValues(object value)
    {
        if (value == null) return new object[0]; Require(value is Array || value is IList, "EXPECTED_VARIANT_ARRAY " + value.GetType().FullName);
        return ((IEnumerable)value).Cast<object>().ToArray();
    }
    private static void Release(object value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
    private static int[] Pids(string name)
    {
        Process[] rows = Process.GetProcessesByName(name);
        try { return rows.Select(p => p.Id).OrderBy(id => id).ToArray(); } finally { foreach (Process p in rows) p.Dispose(); }
    }
    private static string[] Files(string folder)
    {
        Plain(folder); Require(Directory.Exists(folder), "PACK_MISSING " + folder);
        string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => new[] { ".SLDPRT", ".SLDASM", ".SLDDRW" }.Contains(Path.GetExtension(p).ToUpperInvariant())).Select(Full).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        Require(files.Length == 75 && files.All(p => Same(Path.GetDirectoryName(p), folder)), "EXACT_75_FLAT_CAD_FILES_REQUIRED " + folder);
        return files;
    }
    private static List<FileState> Inventory(string folder, string stage)
    {
        var result = new List<FileState>();
        foreach (string path in Files(folder))
        {
            Plain(path); var first = new FileInfo(path); long size = first.Length, ticks = first.LastWriteTimeUtc.Ticks; int attributes = (int)first.Attributes;
            string sha = Hash(path); var last = new FileInfo(path);
            Require(size == last.Length && ticks == last.LastWriteTimeUtc.Ticks && attributes == (int)last.Attributes, "FILE_CHANGED_DURING_HASH " + path);
            result.Add(new FileState { path = path, sha256 = sha, size = size, ticks = ticks, attributes = attributes });
        }
        Record(D("inspection", "cad_inventory", "stage", stage, "pack", folder, "count", result.Count, "files", result)); return result;
    }
    private static void CompareFiles(List<FileState> before, List<FileState> after, bool rootMayChange, string stage)
    {
        var rows = after.ToDictionary(r => r.path, StringComparer.OrdinalIgnoreCase); var differences = new List<string>();
        Require(before.Count == 75 && after.Count == 75, "INVENTORY_COUNT_DRIFT");
        foreach (FileState a in before)
        {
            FileState b; Require(rows.TryGetValue(a.path, out b), "CAD_FILE_MISSING " + a.path);
            if (rootMayChange && Same(a.path, Target)) continue;
            if (a.sha256 != b.sha256 || a.size != b.size || a.ticks != b.ticks || a.attributes != b.attributes) differences.Add(a.path);
        }
        Record(D("inspection", "inventory_comparison", "stage", stage, "rootMayChange", rootMayChange, "unchanged", differences.Count == 0, "differences", differences));
        Require(differences.Count == 0, "PROTECTED_FILE_CHANGED " + string.Join(";", differences));
    }
    private static Dictionary<string, object> EventFrom(string path, string inspection, string stage)
    {
        var log = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        var rows = ArrayValues(log["events"]).Cast<Dictionary<string, object>>().Where(r => Convert.ToString(r["inspection"]) == inspection && (stage == null || (r.ContainsKey("stage") && Convert.ToString(r["stage"]) == stage))).ToArray();
        Require(rows.Length == 1, "EVIDENCE_EVENT_NOT_UNIQUE " + inspection + "/" + stage); return rows[0];
    }
    private static void ValidateManifest()
    {
        Plain(ManifestPath); Require(Inside(Path.Combine(AllowedRoot, "runs"), ManifestPath) && !Inside(Pack, ManifestPath), "MANIFEST_PATH_NOT_AUTHORIZED");
        ManifestSha = Hash(ManifestPath); Binding = Json.Deserialize<Manifest>(File.ReadAllText(ManifestPath));
        Require(Binding != null && Same(Binding.sourcePack, SourcePack) && Same(Binding.candidatePack, CandidatePack), "MANIFEST_PACK_BINDING_FAILED");
        Require(Binding.sourceReceipt != null && Same(Binding.sourceReceipt.path, ReceiptPath) && Binding.sourceReceipt.sha256 == ReceiptSha256, "RECEIPT_BINDING_FAILED");
        Plain(ReceiptPath); Plain(EvidencePath); Require(Hash(ReceiptPath) == ReceiptSha256 && Hash(EvidencePath) == EvidenceSha256, "CHECKPOINT_RECEIPT_OR_EVIDENCE_CHANGED");
        Receipt receipt = Json.Deserialize<Receipt>(File.ReadAllText(ReceiptPath));
        Require(receipt.phase == "assemblies" && receipt.evidenceSha256 == EvidenceSha256 && receipt.after != null && receipt.after.Length == 75, "ASSEMBLIES_CHECKPOINT_INVALID");
        Require(Binding.files != null && Binding.files.Length == 75 && Binding.files.Select(f => f.fileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 75, "MANIFEST_EXACT_75_REQUIRED");
        var recorded = receipt.after.ToDictionary(r => Full(r.path), StringComparer.OrdinalIgnoreCase);
        foreach (FileBinding file in Binding.files)
        {
            Require(!string.IsNullOrWhiteSpace(file.fileName) && Path.GetFileName(file.fileName) == file.fileName && ValidSha(file.sha256), "INVALID_FILE_BINDING");
            Require(Same(file.sourcePath, Path.Combine(SourcePack, file.fileName)) && Same(file.path, Path.Combine(Pack, file.fileName)), "FILE_PATH_BINDING_FAILED " + file.fileName);
            ReceiptFile row; Require(recorded.TryGetValue(Full(file.sourcePath), out row) && row.sha256 == file.sha256, "FILE_NOT_BOUND_TO_RECEIPT " + file.fileName);
            Plain(file.sourcePath); Plain(file.path); Require(Hash(file.sourcePath) == file.sha256 && new FileInfo(file.sourcePath).Length == file.size, "SOURCE_CHECKPOINT_CHANGED " + file.fileName);
            ExpectedHashes.Add(Full(file.path), file.sha256);
        }
        Require(ExpectedHashes.ContainsKey(Target), "ROOT_MISSING_FROM_MANIFEST");
        Require(Binding.expectedRootSha256 == SavedRootSha256 && Binding.editEvidence != null &&
            Same(Binding.editEvidence.path, SuccessfulEditEvidencePath) && Binding.editEvidence.sha256 == SuccessfulEditEvidenceSha256, "DIAGNOSTIC_SUCCESSFUL_EDIT_PIN_REQUIRED");
        Plain(Binding.editEvidence.path); Require(Inside(Path.Combine(AllowedRoot, "runs"), Binding.editEvidence.path) && Hash(Binding.editEvidence.path) == Binding.editEvidence.sha256 && !Same(Binding.editEvidence.path, Output), "EDIT_EVIDENCE_BINDING_FAILED");
        var completed = EventFrom(Binding.editEvidence.path, "completion", null);
        Require(object.Equals(completed["operationComplete"], true) && Convert.ToString(completed["mode"]) == "edit" && Same(Convert.ToString(completed["candidatePack"]), Pack) && Convert.ToString(completed["rootAfterSha256"]) == Binding.expectedRootSha256, "EDIT_DID_NOT_COMPLETE_FOR_THIS_ROOT");
        var plan = EventFrom(Binding.editEvidence.path, "migration_plan", null);
        foreach (object path in ArrayValues(plan["removedExclusivePaths"])) { string full = Full(Convert.ToString(path)); Require(ExpectedHashes.ContainsKey(full) && !Same(full, Target), "INVALID_REMOVED_PATH_BINDING"); RemovedExclusivePaths.Add(full); }
        Require(RemovedExclusivePaths.Count > 0, "REMOVED_REFERENCE_SET_MISSING"); ExpectedHashes[Target] = Binding.expectedRootSha256;
        Require(Files(Pack).All(p => ExpectedHashes.ContainsKey(p)), "CANDIDATE_FILE_SET_DIFFERS");
        foreach (var row in ExpectedHashes) Require(Hash(row.Key) == row.Value, "CANDIDATE_HASH_BINDING_FAILED " + row.Key);
        Record(D("inspection", "manifest_binding", "manifest", ManifestPath, "sha256", ManifestSha, "sourceReceipt", Binding.sourceReceipt, "files", Binding.files, "expectedRootSha256", Binding.expectedRootSha256, "editEvidence", Binding.editEvidence));
    }
    private static string BindPath(string path)
    {
        string full = Full(path), expected = null; Require(Inside(Pack, full) && ExpectedHashes.TryGetValue(full, out expected), "PATH_OUTSIDE_BOUND_CANDIDATE " + full);
        Plain(full); Require(Hash(full) == expected, "BOUND_CANDIDATE_FILE_CHANGED " + full); return full;
    }
    private static void FrozenBindings()
    {
        Require(Hash(ManifestPath) == ManifestSha && Hash(ReceiptPath) == ReceiptSha256 && Hash(EvidencePath) == EvidenceSha256, "FROZEN_BINDING_CHANGED");
        Require(Hash(Binding.editEvidence.path) == SuccessfulEditEvidenceSha256 && Hash(Target) == SavedRootSha256, "DIAGNOSTIC_EDIT_OR_ROOT_CHANGED");
    }
    private static void VerifyOwner(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable)
    {
        Require(process != null && handle != IntPtr.Zero && process.Handle == handle && !process.HasExited, "OWNED_PROCESS_HANDLE_NOT_LIVE");
        Require(process.Id == pid && process.StartTime.ToUniversalTime().Ticks == ticks && Same(process.MainModule.FileName, executable) && sw.GetProcessID() == pid, "OWNED_PROCESS_IDENTITY_CHANGED");
    }
    private static readonly string[] DependencyTargets = { "上盖壳体底板.sldprt", "上盖焊接.SLDASM", "箱体竖隔板L焊接.SLDASM", RootFile };
    private static Dictionary<string, object> ParseDependencies(object raw)
    {
        bool array = raw is Array || raw is IList;
        object[] values = array ? ArrayValues(raw) : null;
        bool parsed = raw == null || (array && values.Length % 2 == 0 && values.All(value => value is string));
        var pairs = new List<object>();
        if (parsed && values != null)
            for (int i = 0; i < values.Length; i += 2) pairs.Add(D("index", i / 2, "name", values[i], "path", values[i + 1]));
        return D("raw", raw, "rawNull", raw == null, "rawType", raw == null ? null : raw.GetType().FullName,
            "rawArrayLength", values == null ? null : (object)values.Length, "namePathPairParseSucceeded", parsed, "pairs", pairs,
            "returnedPathsLoaded", false, "returnedPathsWhitelisted", false, "modelAccepted", false);
    }
    private static void EmptyDocuments(ISldWorks sw, string stage)
    {
        int count = sw.GetDocumentCount(); object raw = sw.GetDocuments(); object[] documents = ArrayValues(raw);
        try
        {
            Record(D("inspection", "empty_documents", "stage", stage, "documentCount", count, "documentArrayCount", documents.Length, "documentsRawNull", raw == null));
            Require(count == 0 && documents.Length == 0, "CLOSED_DEPENDENCY_SESSION_DOCUMENTS_NOT_EMPTY " + stage);
        }
        finally { foreach (object document in documents) Release(document); }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] != "diagnostic35") { Console.Error.WriteLine("Usage: DependencyProbe.exe diagnostic35 <candidate_pack_v5> <newOutputJson> <manifestJson>"); return 2; }
        ISldWorks sw = null; Process process = null; IntPtr retainedHandle = IntPtr.Zero;
        int pid = 0, completedCalls = 0; long ticks = 0; string executable = null, errorText = "", rootAfterSha = "";
        bool owned = false, closed = false, exited = false, operationChecked = false, filesChecked = false, diagnosticCompleted = false, exitRequested = false;
        List<FileState> sourceBefore = null, packBefore = null;
        try
        {
            Mode = args[0]; Pack = Full(args[1]); Require(Same(Pack, CandidatePack), "CANDIDATE_PATH_NOT_AUTHORIZED"); Plain(Pack);
            Target = Path.Combine(Pack, RootFile); Output = Full(args[2]); ManifestPath = Full(args[3]);
            string runs = Path.Combine(AllowedRoot, "runs");
            Require(Inside(runs, Output) && !Same(Path.GetDirectoryName(Output), runs) && !File.Exists(Output) && !Directory.Exists(Output), "OUTPUT_NOT_NEW_OR_OUTSIDE_RUN");
            Plain(Output); Directory.CreateDirectory(Path.GetDirectoryName(Output)); Plain(Output);
            Journal = new FileStream(Output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            Record(D("inspection", "request", "mode", Mode, "sourcePack", SourcePack, "candidatePack", Pack, "target", Target, "output", Output,
                "dependencyTargets", DependencyTargets, "traverseFlags", new[] { false, true }, "search", false, "addReadOnlyInfo", false,
                "throwawayExperiment", true, "diagnosticOnly", true, "noOpen", true, "modelAccepted", false, "strictColdAcceptancePassed", false, "productionAcceptanceEvaluated", false));
            ValidateManifest(); sourceBefore = Inventory(SourcePack, "source-before"); packBefore = Inventory(Pack, "candidate-before");
            int[] cad = Pids("SLDWORKS"), monitors = Pids("sldProcMon");
            Record(D("inspection", "empty_process_baseline", "cad", cad, "monitor", monitors)); Require(cad.Length == 0 && monitors.Length == 0, "COLD_EXCLUSIVE_CAD_SESSION_REQUIRED");
            long activation = DateTime.UtcNow.Ticks;
            sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks; Require(sw != null, "SW2020_ACTIVATION_FAILED");
            pid = sw.GetProcessID(); process = Process.GetProcessById(pid); retainedHandle = process.Handle; ticks = process.StartTime.ToUniversalTime().Ticks; executable = process.MainModule.FileName;
            string revision = sw.RevisionNumber(); int initialCount = sw.GetDocumentCount();
            owned = retainedHandle != IntPtr.Zero && !process.HasExited && ticks >= activation && Same(CanonicalExecutable(executable), CanonicalExecutable(CadExecutable)) && process.MainModule.FileVersionInfo.FileMajorPart == 28 && revision.StartsWith("28.", StringComparison.Ordinal) && Pids("SLDWORKS").SequenceEqual(new[] { pid }) && initialCount == 0;
            Record(D("inspection", "session_owner", "cadPid", pid, "startTimeUtcTicks", ticks.ToString(), "startTicks", ticks.ToString(), "executable", executable, "exactOwnershipVerified", owned,
                "canonicalExecutable", CanonicalExecutable(executable), "revision", revision, "initialDocumentCount", initialCount, "processHandleRetained", retainedHandle != IntPtr.Zero));
            Require(owned, "EXCLUSIVE_OWNED_EMPTY_SW2020_REQUIRED"); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); EmptyDocuments(sw, "before-dependencies");
            foreach (string fileName in DependencyTargets)
            {
                string path = BindPath(Path.Combine(Pack, fileName));
                foreach (bool traverse in new[] { false, true })
                {
                    FrozenBindings(); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); EmptyDocuments(sw, "before:" + fileName + ":" + traverse);
                    Require(Same(BindPath(path), path), "DEPENDENCY_REQUEST_PATH_CHANGED");
                    Record(D("inspection", "dependency_request", "api", "ISldWorks.GetDocumentDependencies2", "path", path, "sha256", ExpectedHashes[path],
                        "traverse", traverse, "search", false, "addReadOnlyInfo", false));
                    object raw = sw.GetDocumentDependencies2(path, traverse, false, false);
                    var result = ParseDependencies(raw);
                    Record(D("inspection", "closed_dependencies", "api", "ISldWorks.GetDocumentDependencies2", "path", path, "sha256", ExpectedHashes[path],
                        "traverse", traverse, "search", false, "addReadOnlyInfo", false, "result", result));
                    EmptyDocuments(sw, "after:" + fileName + ":" + traverse); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); FrozenBindings();
                    Require(object.Equals(result["namePathPairParseSucceeded"], true), "DEPENDENCY_NAME_PATH_PAIR_PARSE_FAILED " + fileName); completedCalls++;
                }
            }
            Require(completedCalls == 8, "DEPENDENCY_EIGHT_CALL_CONTRACT_FAILED"); EmptyDocuments(sw, "final-empty-session"); operationChecked = true;
        }
        catch (Exception error)
        {
            errorText = error.ToString(); if (Journal != null) SafeRecord(D("inspection", "read_failure", "phase", "closed-file-dependencies", "exception", errorText)); else Console.Error.WriteLine(errorText);
        }
        finally
        {
            if (sw != null && owned)
            {
                try
                {
                    VerifyOwner(sw, process, retainedHandle, pid, ticks, executable);
                    try { EmptyDocuments(sw, "before-natural-exit"); } catch (Exception error) { errorText += "\nEMPTY_BEFORE_SHUTDOWN_FAILED " + error; }
                    bool closedResult = sw.CloseAllDocuments(true); int count = sw.GetDocumentCount(); closed = closedResult && count == 0;
                    SafeRecord(D("inspection", "close_all", "discardUnsaved", true, "returnValue", closedResult, "remainingDocumentCount", count, "closed", closed));
                    VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); sw.ExitApp(); exitRequested = true;
                }
                catch (Exception error) { errorText += "\nNATURAL_SHUTDOWN_FAILED " + error; }
            }
            try { Release(sw); } catch (Exception error) { errorText += "\nAPPLICATION_RELEASE_FAILED " + error; } sw = null;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GC.WaitForPendingFinalizers();
            if (owned && process != null)
            {
                try { var timer = Stopwatch.StartNew(); while (timer.ElapsedMilliseconds < 45000) if (process.HasExited || process.WaitForExit(500)) { exited = true; break; } }
                catch (Exception error) { errorText += "\nNATURAL_EXIT_OBSERVATION_FAILED " + error; }
            }
            if (Journal != null)
            {
                SafeRecord(D("inspection", "natural_exit", "cadPid", pid, "ownedCadExited", exited, "exitRequested", exitRequested, "forcedTermination", false,
                    "remainingCad", Pids("SLDWORKS"), "remainingMonitor", Pids("sldProcMon"), "monitorCleanupOwner", "external observer"));
                try
                {
                    if (sourceBefore != null && packBefore != null)
                    {
                        var sourceAfter = Inventory(SourcePack, "source-after"); var packAfter = Inventory(Pack, "candidate-after");
                        CompareFiles(sourceBefore, sourceAfter, false, "all75-source-unchanged"); CompareFiles(packBefore, packAfter, false, "all75-candidate-unchanged");
                        FrozenBindings(); rootAfterSha = Hash(Target); filesChecked = true;
                    }
                }
                catch (Exception error) { errorText += "\nFINAL_FILE_BINDING_FAILED " + error; }
                diagnosticCompleted = operationChecked && completedCalls == 8 && owned && closed && exited && filesChecked && !LoggingFailed && errorText == "";
                SafeRecord(D("inspection", "completion", "mode", Mode, "candidatePack", Pack, "operationComplete", false, "diagnosticCompleted", diagnosticCompleted,
                    "modelAccepted", false, "strictColdAcceptancePassed", false, "rootAfterSha256", rootAfterSha, "completedDependencyCalls", completedCalls,
                    "noOpen", true, "noSnapshot", true, "noSave", true, "noRebuild", true, "noDelete", true, "noSelect", true, "noConfigurationSwitch", true,
                    "returnedPathsLoaded", false, "returnedPathsWhitelisted", false, "all75SourceUnchanged", filesChecked, "all75CandidateUnchanged", filesChecked,
                    "ownedCadExited", exited, "productionAcceptanceEvaluated", false, "warningCauseProven", false, "noForcedTermination", true, "error", errorText));
                diagnosticCompleted = diagnosticCompleted && !LoggingFailed; Journal.Dispose(); Journal = null;
            }
            if (process != null) process.Dispose();
        }
        return diagnosticCompleted ? 0 : 1;
    }
}
