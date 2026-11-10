// Throwaway closed-file clone relink experiment. This never accepts a CAD model.
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
using Microsoft.Win32.SafeHandles;
using SolidWorks.Interop.sldworks;

internal static class RelinkProbe
{
    private const string AllowedRoot = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_owner_rebind_v2";
    private const string SourcePack = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110111643911-CE439B";
    private const string CandidatePack = AllowedRoot + @"\candidate_attempt";
    private const string RootFile = @"native_cad\working_pack\箱体竖隔板R焊接.SLDASM";
    private const string SourceRootSha256 = "856E3B242EE5BD473788A33BB61A4631B778CB34EBCB3624006C341D88411CD6";
    private const string SourceBindingPath = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_identity_v1\source-binding.json";
    private const string SourceBindingSha = "4445F5F8B60B0867F07D391959AB96670FDE53E6E8C85D802E4BF3C2C8623438";
    private const string FreshPath = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_mirror_fresh_v1\runs\20261110125058267-28268\candidate\lock_right_native.SLDPRT";
    private const string FreshSha = "66F594147670F7BDD6FA835D874D2E8BDE10DC81B7905A6C3B8B9BA5C79B95FF";
    private const string HelpDirectory = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\relink_v6\official-help";
    private const string HelpVb = HelpDirectory + @"\Get_Dependencies_for_Open_and_Unopened_Documents_Example_VB.htm";
    private const string HelpVbSha256 = "F309402E77370F71D0CF2041351C8E60F78E7CBE117D76CAD87F82BC122AA4FF";
    private const string HelpCs = HelpDirectory + @"\Copy_Document_and_Its_Dependencies_Example_CSharp.htm";
    private const string HelpCsSha256 = "96FB3BED52F355780C665FACFF6CF2157469160058111239A3980916D7BAC981";
    private const string EmptyPolicy = "Only bound, byte-identical SLDPRT empty returns may mean api-reported-no-dependencies, after same-representation source cross-check. This API has no error code; empty does not independently exclude read failure. Empty SLDASM/SLDDRW and malformed results block all mutation. Cold CAD acceptance is still required.";
    private const string CadExecutable = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
    private static readonly List<object> Events = new List<object>();
    private static readonly Dictionary<string, string> ExpectedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MutableOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static FileStream Journal;
    private static Manifest Binding;
    private static List<FileState> SourceBefore, CurrentCandidate;
    private static string Mode, Pack, Target, Output, ManifestPath, ManifestSha;
    private static bool LoggingFailed;
    private static int CandidateDependencyCalls, SourceLeafCrossChecks, PlannedReplacements, CompletedReplacements;

    public sealed class FileBinding { public string fileName, sourcePath, path, sha256; public long size; }
    public sealed class PathBinding { public string path, sha256; }
    public sealed class Manifest { public string sourcePack, candidatePack; public FileBinding[] files; public PathBinding sourceReceipt, sourceEvidence; }
    public sealed class ReceiptFile { public string path, relativePath, sha256; public long size; }
    public sealed class Receipt { public string schema, sourceAttempt; public int workingCount, doorsCount, sourcesCount; public ReceiptFile[] inventory; }
    private sealed class FileState { public string path, sha256; public long size, ticks; public int attributes; }
    public sealed class DependencyPair { public int index; public string name, path; }
    public sealed class DependencyResult
    {
        public object raw; public bool rawNull, parseSucceeded; public string rawType, emptyRepresentation;
        public int? rawArrayLength, rawArrayRank, rawArrayLowerBound; public List<DependencyPair> pairs = new List<DependencyPair>();
    }
    public sealed class PlanEdge { public string name, originalPath, sourcePath, candidatePath, initialSha256; public bool replacementRequired; }
    public sealed class OwnerPlan { public string owner, initialSha256, emptyRepresentation, dependencyState; public bool mayChange; public List<PlanEdge> edges = new List<PlanEdge>(); }
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint attributes, creationLow, creationHigh, accessLow, accessHigh, writeLow, writeHigh, volumeSerial, sizeHigh, sizeLow, links, indexHigh, indexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string shortPath, StringBuilder longPath, uint bufferLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

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
        Events.Add(row); byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(D("schema", "obsolete-door-closed-relink-v6", "events", Events)));
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
        if (Same(folder, SourcePack)) return Binding.files.Where(f => Inside(SourcePack, f.sourcePath)).Select(f => Full(f.sourcePath)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => new[] { ".SLDPRT", ".SLDASM", ".SLDDRW" }.Contains(Path.GetExtension(p).ToUpperInvariant())).Select(Full).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        Require(files.Length == 120 && files.All(p => Inside(folder, p)), "EXACT_120_CAD_FILES_REQUIRED " + folder);
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
        if (stage != null) Record(D("inspection", "cad_inventory", "stage", stage, "pack", folder, "count", result.Count, "files", result)); return result;
    }

    private static void SingleLink(string path)
    {
        Plain(path);
        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            ByHandleFileInformation info;
            if (!GetFileInformationByHandle(input.SafeFileHandle, out info)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "FILE_IDENTITY_UNAVAILABLE " + path);
            Require(info.links == 1, "HARDLINK_REJECTED " + path);
        }
    }
    private static void CompareFiles(List<FileState> before, List<FileState> after, HashSet<string> allowed, string stage)
    {
        var rows = after.ToDictionary(r => r.path, StringComparer.OrdinalIgnoreCase);
        var rejected = new List<string>(); var permitted = new List<object>();
        Require(before.Count == after.Count && (before.Count == 119 || before.Count == 120), "INVENTORY_COUNT_DRIFT");
        foreach (FileState a in before)
        {
            FileState b; Require(rows.TryGetValue(a.path, out b), "CAD_FILE_MISSING " + a.path);
            if (a.sha256 == b.sha256 && a.size == b.size && a.ticks == b.ticks && a.attributes == b.attributes) continue;
            if (!allowed.Contains(a.path)) rejected.Add(a.path);
            else permitted.Add(D("path", a.path, "before", a, "after", b));
        }
        Record(D("inspection", "inventory_comparison", "stage", stage, "allowedPaths", allowed.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
            "protectedUnchanged", rejected.Count == 0, "differences", rejected, "allowedChanges", permitted));
        Require(rejected.Count == 0, "PROTECTED_FILE_CHANGED " + string.Join(";", rejected));
    }
    private static void ValidateManifest()
    {
        Plain(ManifestPath); Require(Inside(Path.Combine(AllowedRoot, "runs"), ManifestPath) && !Inside(Pack, ManifestPath), "MANIFEST_PATH_NOT_AUTHORIZED");
        ManifestSha = Hash(ManifestPath); Binding = Json.Deserialize<Manifest>(File.ReadAllText(ManifestPath));
        Require(Binding != null && Same(Binding.sourcePack, SourcePack) && Same(Binding.candidatePack, CandidatePack), "MANIFEST_PACK_BINDING_FAILED");
        Require(Hash(SourceBindingPath) == SourceBindingSha && Hash(FreshPath) == FreshSha && new FileInfo(FreshPath).Length == 1258522, "FIXED_SOURCE_OR_FRESH_CHANGED");
        Receipt source = Json.Deserialize<Receipt>(File.ReadAllText(SourceBindingPath));
        Require(source != null && Same(source.sourceAttempt, SourcePack) && source.inventory.Length == 119 && source.workingCount == 76 && source.doorsCount == 30 && source.sourcesCount == 13, "SOURCE_119_BINDING_INVALID");
        Require(Binding.files != null && Binding.files.Length == 120 && Binding.files.Select(f => Full(f.path)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 120, "MANIFEST_EXACT_120_REQUIRED");
        var rows = source.inventory.ToDictionary(f => Full(f.path), StringComparer.OrdinalIgnoreCase);
        foreach (FileBinding file in Binding.files)
        {
            Require(file != null && ValidSha(file.sha256), "INVALID_FILE_BINDING");
            if (Same(file.sourcePath, FreshPath)) Require(Same(file.path, Path.Combine(Pack, @"native_cad\working_pack\right_rebound_native.SLDPRT")) && file.sha256 == FreshSha && file.size == 1258522, "FRESH_BINDING_INVALID");
            else
            {
                ReceiptFile pin; Require(rows.TryGetValue(Full(file.sourcePath), out pin) && pin.sha256 == file.sha256 && pin.size == file.size, "FILE_NOT_IN_ORIGINAL_119");
                string relative = Full(file.sourcePath).Substring(Full(SourcePack).Length + 1);
                Require(Same(file.path, Path.Combine(Pack, relative)), "RELATIVE_LAYOUT_CHANGED");
            }
            SingleLink(file.sourcePath); SingleLink(file.path);
            Require(Hash(file.sourcePath) == file.sha256 && new FileInfo(file.sourcePath).Length == file.size, "SOURCE_CHECKPOINT_CHANGED");
            Require(Hash(file.path) == file.sha256 && new FileInfo(file.path).Length == file.size, "CANDIDATE_NOT_IDENTICAL_SOURCE_COPY");
            ExpectedHashes.Add(Full(file.path), file.sha256);
        }
        Require(rows.Keys.All(p => Binding.files.Count(f => Same(f.sourcePath, p)) == 1) && Binding.files.Count(f => Same(f.sourcePath, FreshPath)) == 1, "EXACT_119_PLUS_FRESH_REQUIRED");
        Require(Files(Pack).All(p => ExpectedHashes.ContainsKey(p)) && ExpectedHashes[Target] == SourceRootSha256, "CANDIDATE_FILE_SET_OR_ROOT_DIFFERS");
        Require(RuntimeFiles().Length == 107 && Binding.files.Count(f => Inside(Path.Combine(Pack, @"native_cad\working_pack"), f.path)) == 77 && Binding.files.Count(f => Inside(Path.Combine(Pack, "doors"), f.path)) == 30, "EXACT_107_RUNTIME_77_PACK_PLUS_30_DOORS_REQUIRED");
        CheckExcludedDoorSources("manifest");
        Require(Hash(HelpVb) == HelpVbSha256 && Hash(HelpCs) == HelpCsSha256, "OFFICIAL_EMPTY_POLICY_EVIDENCE_CHANGED");
        Record(D("inspection", "manifest_binding", "manifest", ManifestPath, "sha256", ManifestSha, "sourceBindingPath", SourceBindingPath, "sourceBindingSha256", SourceBindingSha, "files", Binding.files));
        Record(D("inspection", "empty_dependency_policy", "policy", EmptyPolicy, "readFailureIndependentlyExcluded", false, "modelAccepted", false));
    }
    private static bool RuntimeFile(FileBinding file)
    {
        return Inside(Path.Combine(Pack, @"native_cad\working_pack"), file.path) || Inside(Path.Combine(Pack, "doors"), file.path);
    }
    private static FileBinding[] RuntimeFiles() { return Binding.files.Where(RuntimeFile).ToArray(); }
    private static void CheckExcludedDoorSources(string stage)
    {
        FileBinding[] excluded = Binding.files.Where(f => !RuntimeFile(f)).ToArray();
        Require(excluded.Length == 13 && excluded.All(f => Inside(Path.Combine(Pack, "door_sources"), f.path) && Inside(Path.Combine(SourcePack, "door_sources"), f.sourcePath)), "EXACT_13_PROTECTED_DOOR_SOURCES_REQUIRED");
        foreach (FileBinding file in excluded)
            Require(Hash(file.path) == file.sha256 && Hash(file.sourcePath) == file.sha256 && new FileInfo(file.path).Length == file.size && new FileInfo(file.sourcePath).Length == file.size, "EXCLUDED_DOOR_SOURCE_CHANGED " + file.path);
        Record(D("inspection", "excluded_protected_door_sources", "stage", stage, "excludedProtectedDoorSources13", 13, "sourceAndCandidateHashesUnchanged", true, "noOpen", true, "noRelink", true, "noSave", true, "files", excluded));
    }
    private static string BindPath(string path)
    {
        string full = Full(path), expected = null;
        Require(Inside(Pack, full) && ExpectedHashes.TryGetValue(full, out expected), "PATH_OUTSIDE_BOUND_CANDIDATE " + full);
        SingleLink(full); Require(Hash(full) == expected, "BOUND_CANDIDATE_FILE_CHANGED " + full); return full;
    }
    private static void FrozenBindings()
    {
        Require(Hash(ManifestPath) == ManifestSha && Hash(SourceBindingPath) == SourceBindingSha && Hash(FreshPath) == FreshSha, "FROZEN_BINDING_CHANGED");
        Require(Hash(Path.Combine(SourcePack, RootFile)) == SourceRootSha256 && Hash(HelpVb) == HelpVbSha256 && Hash(HelpCs) == HelpCsSha256, "SOURCE_ROOT_OR_POLICY_CHANGED");
    }
    private static DependencyResult ParseDependencies(object raw)
    {
        var result = new DependencyResult { raw = raw, rawNull = raw == null, rawType = raw == null ? null : raw.GetType().FullName };
        if (raw == null) { result.parseSucceeded = true; result.emptyRepresentation = "null"; return result; }
        Array array = raw as Array;
        if (array == null) return result;
        result.rawArrayLength = array.Length; result.rawArrayRank = array.Rank;
        if (array.Rank != 1) return result;
        result.rawArrayLowerBound = array.GetLowerBound(0);
        if (array.GetLowerBound(0) != 0 || array.Length % 2 != 0) return result;
        object[] values = ArrayValues(array);
        if (!values.All(value => value is string && !string.IsNullOrWhiteSpace((string)value))) return result;
        result.parseSucceeded = true;
        if (values.Length == 0) result.emptyRepresentation = "zero-length-array:" + result.rawType;
        for (int i = 0; i < values.Length; i += 2) result.pairs.Add(new DependencyPair { index = i / 2, name = (string)values[i], path = (string)values[i + 1] });
        return result;
    }
    private static OwnerPlan PlanOwner(FileBinding owner, object raw, FileBinding[] files)
    {
        DependencyResult parsed = ParseDependencies(raw);
        Require(parsed.parseSucceeded, "MALFORMED_DEPENDENCY_RETURN " + owner.path);
        var plan = new OwnerPlan { owner = Full(owner.path), initialSha256 = owner.sha256, emptyRepresentation = parsed.emptyRepresentation };
        if (parsed.emptyRepresentation != null)
        {
            Require(string.Equals(Path.GetExtension(owner.path), ".SLDPRT", StringComparison.OrdinalIgnoreCase), "EMPTY_NONPART_DEPENDENCIES_UNRESOLVED " + owner.path);
            plan.dependencyState = "api-reported-no-dependencies"; return plan;
        }
        Require(RuntimeFile(owner), "NON_RUNTIME_OWNER_REJECTED");
        FileBinding[] runtimeFiles = files.Where(RuntimeFile).ToArray();
        var sources = runtimeFiles.ToDictionary(f => Full(f.sourcePath), StringComparer.OrdinalIgnoreCase);
        var candidates = runtimeFiles.ToDictionary(f => Full(f.path), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DependencyPair pair in parsed.pairs)
        {
            string original = pair.path;
            Require(Path.IsPathRooted(original) && string.Equals(original, Full(original), StringComparison.OrdinalIgnoreCase), "DEPENDENCY_PATH_NOT_EXACT_ABSOLUTE " + original);
            Require(!Inside(Path.Combine(Pack, "door_sources"), original) && !Inside(Path.Combine(SourcePack, "door_sources"), original), "RUNTIME_DEPENDENCY_TO_EXCLUDED_DOOR_SOURCE " + original);
            FileBinding target; bool source = sources.TryGetValue(original, out target);
            if (!source) Require(candidates.TryGetValue(original, out target), "UNAUTHORIZED_EXTERNAL_DEPENDENCY " + original);
            Require(seen.Add(Full(target.path)), "DUPLICATE_OR_AMBIGUOUS_DEPENDENCY " + original);
            Require(string.Equals(Path.GetExtension(original), Path.GetExtension(target.path), StringComparison.OrdinalIgnoreCase), "DEPENDENCY_TYPE_MISMATCH " + original);
            plan.edges.Add(new PlanEdge { name = pair.name, originalPath = original, sourcePath = Full(target.sourcePath), candidatePath = Full(target.path), initialSha256 = target.sha256, replacementRequired = source });
        }
        plan.mayChange = plan.edges.Any(e => e.replacementRequired); plan.dependencyState = "exact-bound-dependency-pairs"; return plan;
    }
    private static void CheckEmptySource(OwnerPlan candidate, object sourceRaw)
    {
        DependencyResult source = ParseDependencies(sourceRaw);
        Require(source.parseSucceeded && source.emptyRepresentation != null && source.emptyRepresentation == candidate.emptyRepresentation, "EMPTY_SOURCE_CANDIDATE_REPRESENTATION_MISMATCH " + candidate.owner);
    }
    private static void CompareDependencyPlan(OwnerPlan planned, OwnerPlan observed)
    {
        Require(Same(planned.owner, observed.owner) && planned.emptyRepresentation == observed.emptyRepresentation, "POST_DEPENDENCY_OWNER_OR_EMPTY_REPRESENTATION_CHANGED " + planned.owner);
        Require(!observed.edges.Any(e => e.replacementRequired), "SOURCE_DEPENDENCY_REMAINS " + planned.owner);
        string[] expected = planned.edges.Select(e => e.name + "\0" + e.candidatePath).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] actual = observed.edges.Select(e => e.name + "\0" + e.originalPath).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        Require(expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase), "POST_DEPENDENCIES_DIFFER_FROM_FROZEN_PLAN " + planned.owner);
    }
    private static void VerifyOwner(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable)
    {
        Require(process != null && handle != IntPtr.Zero && process.Handle == handle && !process.HasExited, "OWNED_PROCESS_HANDLE_NOT_LIVE");
        Require(process.Id == pid && process.StartTime.ToUniversalTime().Ticks == ticks && Same(process.MainModule.FileName, executable) && sw.GetProcessID() == pid, "OWNED_PROCESS_IDENTITY_CHANGED");
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

    private static DependencyResult ReadDependencies(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable, FileBinding file, bool source, string stage)
    {
        FrozenBindings(); VerifyOwner(sw, process, handle, pid, ticks, executable); EmptyDocuments(sw, "before:" + stage + ":" + file.fileName);
        string path = source ? Full(file.sourcePath) : BindPath(file.path);
        if (source) { SingleLink(path); Require(Hash(path) == file.sha256 && new FileInfo(path).Length == file.size, "SOURCE_DEPENDENCY_INPUT_CHANGED " + path); }
        Record(D("inspection", "dependency_request", "stage", stage, "api", "ISldWorks.GetDocumentDependencies2", "path", path, "sourceLeafCrossCheck", source,
            "sha256", source ? file.sha256 : ExpectedHashes[path], "traverse", false, "search", false, "addReadOnlyInfo", false));
        object raw = sw.GetDocumentDependencies2(path, false, false, false);
        if (source) SourceLeafCrossChecks++; else CandidateDependencyCalls++;
        DependencyResult result = ParseDependencies(raw);
        Record(D("inspection", "closed_dependencies", "stage", stage, "api", "ISldWorks.GetDocumentDependencies2", "path", path, "sourceLeafCrossCheck", source,
            "traverse", false, "search", false, "addReadOnlyInfo", false, "result", result));
        EmptyDocuments(sw, "after:" + stage + ":" + file.fileName); VerifyOwner(sw, process, handle, pid, ticks, executable); FrozenBindings();
        if (source) Require(Hash(path) == file.sha256, "SOURCE_CHANGED_DURING_DEPENDENCY_READ " + path); else BindPath(path);
        return result;
    }
    private static void CrossCheckEmpty(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable, FileBinding file, OwnerPlan plan, string stage)
    {
        if (plan.emptyRepresentation == null) return;
        Require(Hash(BindPath(file.path)) == file.sha256 && Hash(file.sourcePath) == file.sha256, "EMPTY_LEAF_NOT_BYTE_IDENTICAL_TO_SOURCE " + file.path);
        DependencyResult source = ReadDependencies(sw, process, handle, pid, ticks, executable, file, true, stage + "-source-leaf");
        CheckEmptySource(plan, source.raw);
        Require(Hash(BindPath(file.path)) == file.sha256 && Hash(file.sourcePath) == file.sha256, "EMPTY_LEAF_CHANGED_DURING_CROSS_CHECK " + file.path);
        Record(D("inspection", "empty_leaf_cross_check", "stage", stage, "candidatePath", file.path, "sourcePath", file.sourcePath,
            "sameSourceCandidateSha256", file.sha256, "sameEmptyRepresentation", plan.emptyRepresentation, "passed", true, "readFailureIndependentlyExcluded", false));
    }
    private static List<OwnerPlan> Preflight(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable)
    {
        var plans = new List<OwnerPlan>(); var failures = new List<object>(); int attempted = 0;
        foreach (FileBinding file in RuntimeFiles().OrderBy(f => f.path, StringComparer.OrdinalIgnoreCase))
        {
            attempted++;
            try
            {
                DependencyResult raw = ReadDependencies(sw, process, handle, pid, ticks, executable, file, false, "preflight");
                OwnerPlan plan = PlanOwner(file, raw.raw, Binding.files);
                CrossCheckEmpty(sw, process, handle, pid, ticks, executable, file, plan, "preflight");
                plans.Add(plan); Record(D("inspection", "owner_preflight", "plan", plan, "passed", true));
            }
            catch (Exception error)
            {
                var failure = D("owner", file.path, "error", error.ToString()); failures.Add(failure);
                Record(D("inspection", "owner_preflight", "owner", file.path, "passed", false, "error", error.ToString()));
            }
        }
        Record(D("inspection", "preflight_summary", "attemptedCandidateOwners", attempted, "plannedOwners", plans.Count, "failures", failures, "mutationCallsSoFar", CompletedReplacements));
        Require(attempted == 107 && plans.Count == 107 && failures.Count == 0 && CandidateDependencyCalls == 107 && plans.Sum(p => p.edges.Count) == 180, "FULL_107_RUNTIME_180_EDGES_PREFLIGHT_REQUIRED_BEFORE_MUTATION");
        Record(D("inspection", "runtime_dependency_scope", "runtimeOwners", 107, "runtimeEdges", 180, "excludedProtectedDoorSources13", 13, "noRuntimeEdgesToExcluded", true, "isolatedDependencyClosureScope", "107 runtime files only; 13 protected templates excluded", "mutationCallsSoFar", CompletedReplacements));
        CheckExcludedDoorSources("before-first-mutation");
        foreach (OwnerPlan plan in plans.Where(p => p.mayChange))
        {
            Require((File.GetAttributes(plan.owner) & FileAttributes.ReadOnly) == 0, "RELINK_OWNER_READ_ONLY " + plan.owner);
            MutableOwners.Add(plan.owner);
        }
        PlannedReplacements = plans.Sum(p => p.edges.Count(e => e.replacementRequired));
        Record(D("inspection", "relink_plan", "all107RuntimeEnumeratedBeforeMutation", true, "plans", plans, "mutableOwners", MutableOwners.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
            "plannedReplacements", PlannedReplacements, "runtimeFileCount", 107, "runtimeEdgeCount", 180, "excludedProtectedDoorSources13", 13, "noRuntimeEdgesToExcluded", true, "sourceFileCount", 119, "candidateFileCount", 120, "nonOwnerCandidateCount", 120 - MutableOwners.Count, "mutationCallsSoFar", CompletedReplacements));
        CompareFiles(SourceBefore, Inventory(SourcePack, "source-before-first-mutation"), NoPaths, "all119-source-preflight-unchanged");
        CompareFiles(CurrentCandidate, Inventory(Pack, "candidate-before-first-mutation"), NoPaths, "all120-candidate-preflight-unchanged");
        return plans;
    }
    private static void Relink(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable, List<OwnerPlan> plans)
    {
        foreach (OwnerPlan plan in plans)
        foreach (PlanEdge edge in plan.edges.Where(e => e.replacementRequired))
        {
            FrozenBindings(); VerifyOwner(sw, process, handle, pid, ticks, executable); EmptyDocuments(sw, "before-replace:" + Path.GetFileName(plan.owner));
            Require(MutableOwners.Contains(plan.owner) && Same(edge.originalPath, edge.sourcePath), "REPLACEMENT_NOT_IN_FROZEN_SOURCE_PLAN");
            var sourceNow = Inventory(SourcePack, null); var before = Inventory(Pack, null);
            CompareFiles(SourceBefore, sourceNow, NoPaths, "all119-source-before-pair"); CompareFiles(CurrentCandidate, before, NoPaths, "all120-candidate-before-pair");
            BindPath(plan.owner); BindPath(edge.candidatePath); SingleLink(edge.sourcePath);
            Require(Hash(edge.sourcePath) == edge.initialSha256 && File.Exists(edge.candidatePath), "SOURCE_OR_REPLACEMENT_TARGET_CHANGED");
            Record(D("inspection", "replacement_request", "api", "ISldWorks.ReplaceReferencedDocument", "owner", plan.owner, "oldPath", edge.originalPath, "target", edge.candidatePath,
                "ownerBeforeSha256", ExpectedHashes[plan.owner], "initialSourceAndTargetSha256", edge.initialSha256, "currentTargetSha256", ExpectedHashes[edge.candidatePath],
                "initialSourceTargetBytesIdentical", true, "targetChangedOnlyByPreviousPlannedRelink", ExpectedHashes[edge.candidatePath] != edge.initialSha256));
            bool replaced = sw.ReplaceReferencedDocument(plan.owner, edge.originalPath, edge.candidatePath);
            Record(D("inspection", "replacement_result", "owner", plan.owner, "oldPath", edge.originalPath, "target", edge.candidatePath, "returnValue", replaced));
            EmptyDocuments(sw, "after-replace:" + Path.GetFileName(plan.owner)); VerifyOwner(sw, process, handle, pid, ticks, executable); FrozenBindings();
            var after = Inventory(Pack, null);
            CompareFiles(before, after, new HashSet<string>(new[] { plan.owner }, StringComparer.OrdinalIgnoreCase), "only-current-owner-may-change");
            CompareFiles(SourceBefore, Inventory(SourcePack, null), NoPaths, "all119-source-after-pair");
            FileState ownerAfter = after.Single(f => Same(f.path, plan.owner));
            ExpectedHashes[plan.owner] = ownerAfter.sha256; CurrentCandidate = after;
            Record(D("inspection", "replacement_owner_after", "owner", ownerAfter, "nonOwnerFilesUnchanged", true));
            Require(replaced, "REPLACE_REFERENCED_DOCUMENT_RETURNED_FALSE " + plan.owner + " -> " + edge.originalPath);
            CompletedReplacements++;
        }
        Require(CompletedReplacements == PlannedReplacements, "REPLACEMENT_COUNT_DIFFERS_FROM_PLAN");
    }
    private static void VerifyDependencies(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable, List<OwnerPlan> plans)
    {
        var failures = new List<object>(); int checkedOwners = 0, oldSourceEdges = 0;
        foreach (OwnerPlan plan in plans)
        {
            FileBinding file = Binding.files.Single(f => Same(f.path, plan.owner));
            try
            {
                DependencyResult raw = ReadDependencies(sw, process, handle, pid, ticks, executable, file, false, "post-relink");
                OwnerPlan observed = PlanOwner(file, raw.raw, Binding.files); oldSourceEdges += observed.edges.Count(e => e.replacementRequired);
                CrossCheckEmpty(sw, process, handle, pid, ticks, executable, file, observed, "post-relink");
                CompareDependencyPlan(plan, observed); checkedOwners++;
                Record(D("inspection", "owner_dependency_verification", "owner", plan.owner, "observed", observed, "matchesFrozenMappedPlan", true));
            }
            catch (Exception error) { failures.Add(D("owner", plan.owner, "error", error.ToString())); Record(D("inspection", "owner_dependency_verification", "owner", plan.owner, "matchesFrozenMappedPlan", false, "error", error.ToString())); }
        }
        Record(D("inspection", "dependency_verification", "checkedOwners", checkedOwners, "oldSourceEdges", oldSourceEdges, "failures", failures, "plannedReplacements", PlannedReplacements, "completedReplacements", CompletedReplacements));
        Require(checkedOwners == 107 && plans.Sum(p => p.edges.Count) == 180 && oldSourceEdges == 0 && failures.Count == 0 && CandidateDependencyCalls == 214, "POST_DEPENDENCIES_MUST_EQUAL_FROZEN_MAPPED_PLAN");
        CompareFiles(CurrentCandidate, Inventory(Pack, "candidate-after-dependency-verification"), NoPaths, "all120-candidate-post-read-unchanged");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] != "relink") { Console.Error.WriteLine("Usage: RelinkProbe.exe relink <candidate_attempt> <newOutputJson> <manifestJson>"); return 2; }
        ISldWorks sw = null; Process process = null; IntPtr retainedHandle = IntPtr.Zero;
        int pid = 0; long ticks = 0; string executable = null, errorText = "", rootAfterSha = "";
        bool owned = false, closed = false, exited = false, operationChecked = false, filesChecked = false, diagnosticCompleted = false, exitRequested = false;
        bool sourceUnchanged = false, nonOwnersUnchanged = false, trackedCandidatesUnchanged = false;
        List<FileState> packBefore = null;
        try
        {
            Mode = args[0]; Pack = Full(args[1]); Require(Same(Pack, CandidatePack), "CANDIDATE_PATH_NOT_AUTHORIZED"); Plain(Pack);
            Target = Path.Combine(Pack, RootFile); Output = Full(args[2]); ManifestPath = Full(args[3]);
            string runs = Path.Combine(AllowedRoot, "runs");
            Require(Inside(runs, Output) && !Same(Path.GetDirectoryName(Output), runs) && !File.Exists(Output) && !Directory.Exists(Output), "OUTPUT_NOT_NEW_OR_OUTSIDE_RUN");
            Plain(Output); Directory.CreateDirectory(Path.GetDirectoryName(Output)); Plain(Output);
            Journal = new FileStream(Output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            Record(D("inspection", "request", "mode", Mode, "sourcePack", SourcePack, "candidatePack", Pack, "target", Target, "output", Output,
                "traverse", false, "search", false, "addReadOnlyInfo", false, "throwawayExperiment", true, "diagnosticOnly", true,
                "noOpen", true, "modelAccepted", false, "strictColdAcceptancePassed", false, "productionAcceptanceEvaluated", false));
            ValidateManifest(); SourceBefore = Inventory(SourcePack, "source-before"); packBefore = Inventory(Pack, "candidate-before"); CurrentCandidate = packBefore;
            int[] cad = Pids("SLDWORKS"), monitors = Pids("sldProcMon");
            Record(D("inspection", "empty_process_baseline", "cad", cad, "monitor", monitors)); Require(cad.Length == 0 && monitors.Length == 0, "COLD_EXCLUSIVE_CAD_SESSION_REQUIRED");
            long activation = DateTime.UtcNow.Ticks;
            sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks; Require(sw != null, "SW2020_ACTIVATION_FAILED");
            pid = sw.GetProcessID(); process = Process.GetProcessById(pid); retainedHandle = process.Handle; ticks = process.StartTime.ToUniversalTime().Ticks; executable = process.MainModule.FileName;
            string revision = sw.RevisionNumber(); int initialCount = sw.GetDocumentCount();
            owned = retainedHandle != IntPtr.Zero && !process.HasExited && ticks >= activation && Same(CanonicalExecutable(executable), CanonicalExecutable(CadExecutable)) && process.MainModule.FileVersionInfo.FileMajorPart == 28 && revision.StartsWith("28.", StringComparison.Ordinal) && Pids("SLDWORKS").SequenceEqual(new[] { pid }) && initialCount == 0;
            Record(D("inspection", "session_owner", "cadPid", pid, "startTimeUtcTicks", ticks.ToString(), "startTicks", ticks.ToString(), "executable", executable, "exactOwnershipVerified", owned,
                "canonicalExecutable", CanonicalExecutable(executable), "revision", revision, "initialDocumentCount", initialCount, "processHandleRetained", retainedHandle != IntPtr.Zero));
            Require(owned, "EXCLUSIVE_OWNED_EMPTY_SW2020_REQUIRED"); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); EmptyDocuments(sw, "before-preflight");
            List<OwnerPlan> plans = Preflight(sw, process, retainedHandle, pid, ticks, executable);
            Relink(sw, process, retainedHandle, pid, ticks, executable, plans);
            VerifyDependencies(sw, process, retainedHandle, pid, ticks, executable, plans);
            CheckExcludedDoorSources("after-independent-runtime-dependency-verification");
            EmptyDocuments(sw, "final-empty-session"); operationChecked = true;
        }
        catch (Exception error)
        {
            errorText = error.ToString(); if (Journal != null) SafeRecord(D("inspection", "relink_failure", "phase", "closed-file-relink", "exception", errorText)); else Console.Error.WriteLine(errorText);
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
                    if (SourceBefore != null && packBefore != null)
                    {
                        var sourceAfter = Inventory(SourcePack, "source-after"); var packAfter = Inventory(Pack, "candidate-after");
                        CompareFiles(SourceBefore, sourceAfter, NoPaths, "all119-source-unchanged"); sourceUnchanged = true;
                        CompareFiles(packBefore, packAfter, MutableOwners, "all-nonowner-candidate-unchanged"); nonOwnersUnchanged = true;
                        CompareFiles(CurrentCandidate, packAfter, NoPaths, "all120-candidate-match-tracked-pair-results"); trackedCandidatesUnchanged = true;
                        foreach (FileState file in packAfter) Require(file.sha256 == ExpectedHashes[file.path], "FINAL_CANDIDATE_HASH_UNTRACKED " + file.path);
                        FrozenBindings(); rootAfterSha = Hash(Target); filesChecked = true;
                    }
                }
                catch (Exception error) { errorText += "\nFINAL_FILE_BINDING_FAILED " + error; }
                diagnosticCompleted = operationChecked && CandidateDependencyCalls == 214 && CompletedReplacements == PlannedReplacements && owned && closed && exited && filesChecked && !LoggingFailed && errorText == "";
                SafeRecord(D("inspection", "completion", "mode", Mode, "candidatePack", Pack, "operationComplete", false, "diagnosticCompleted", diagnosticCompleted,
                    "modelAccepted", false, "strictColdAcceptancePassed", false, "rootAfterSha256", rootAfterSha, "candidateDependencyCalls", CandidateDependencyCalls, "sourceLeafCrossChecks", SourceLeafCrossChecks,
                    "plannedReplacements", PlannedReplacements, "completedReplacements", CompletedReplacements, "runtimeOwnerCount", 107, "expectedRuntimeEdgeCount", 180, "excludedProtectedDoorSources13", 13, "noRuntimeEdgesToExcluded", operationChecked, "candidateDependencyIsolationScope", "107 runtime files only", "mutableOwners", MutableOwners.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
                    "noOpen", true, "noSave", true, "noRebuild", true, "noDelete", true, "noSelect", true, "noConfigurationSwitch", true,
                    "all119SourceUnchanged", sourceUnchanged, "allNonOwnerCandidateUnchanged", nonOwnersUnchanged, "all120CandidateMatchTrackedPairResults", trackedCandidatesUnchanged,
                    "ownedCadExited", exited, "productionAcceptanceEvaluated", false, "warningCauseProven", false, "noForcedTermination", true, "error", errorText));
                diagnosticCompleted = diagnosticCompleted && !LoggingFailed; Journal.Dispose(); Journal = null;
            }
            if (process != null) process.Dispose();
        }
        return diagnosticCompleted ? 0 : 1;
    }
}
