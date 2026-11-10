// Throwaway, single-checkpoint migration experiment. This is not a production acceptance gate.
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

internal static class IdentityProbe
{
    private const string AllowedRoot = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe";
    private const string SourcePack = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110035409405-0C0A4D\native_cad\working_pack";
    private const string CandidatePack = AllowedRoot + @"\candidate_pack_v6";
    private const string RootFile = "标准寄存柜1917×1000×550(总装配).SLDASM";
    private const string ReceiptPath = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110035409405-0C0A4D\receipts\assemblies.json";
    private const string ReceiptSha256 = "1F89B12B1F63D8134ACFB04BA53B24571A0CB57035C6B16FA44F8F4107EDB4BD";
    private const string EvidencePath = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110035409405-0C0A4D\evidence\assemblies.json";
    private const string EvidenceSha256 = "2417CD3A7578A85EA4047D10E7EB6C932C89AD7976C46B8951EBB49D497C6AC2";
    private const string ReferenceBaselinePath = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\identity_probe\config35\runs\20261110052310959-32984\probe.json";
    private const string ReferenceBaselineSha256 = "86661305C2ACD047D431DB12CE800D2449472B6AFC0786EE6C395C3D6F10F7BE";
    private const string CadExecutable = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
    private static readonly string[] OldNames = { "储物柜门装配_L2-2", "储物柜门装配_L4-2", "储物柜门装配_L6-2", "储物柜门装配_R2-2", "储物柜门装配_R4-2", "储物柜门装配_R6-2" };
    private static readonly string[] RootMateNames = { "重合1", "重合2", "同心39" };
    private static readonly HashSet<string> MateFeatureTypes = new HashSet<string>(new[] {
        "MateCamTangent", "MateCoincident", "MateConcentric", "MateDistanceDim", "MateGearDim", "MateHinge", "MateInPlace",
        "MateLinearCoupler", "MateLock", "MateParallel", "MatePerpendicular", "MatePlanarAngleDim", "MateProfileCenter",
        "MateRackPinionDim", "MateScrew", "MateSlot", "MateSymmetric", "MateTangent", "MateUniversalJoint", "MateWidth"
    }, StringComparer.Ordinal);
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
    private static readonly List<object> Events = new List<object>();
    private static readonly HashSet<string> RemovedExclusivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ExpectedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FileBinding> ReferenceSourceBindings = new Dictionary<string, FileBinding>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> FrozenProofManifestHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static List<FileState> CandidateInitialStates;
    private static object IndependentBaselineState;
    private static FileStream Journal;
    private static Manifest Binding;
    private static string Mode, Pack, Target, Output, ManifestPath, ManifestSha;
    private static bool LoggingFailed;

    public sealed class FileBinding { public string fileName, sourcePath, path, sha256, candidateSha256; public long size, candidateSize; }
    public sealed class PathBinding { public string path, sha256; }
    public sealed class Manifest { public string sourcePack, candidatePack, expectedRootSha256; public FileBinding[] files; public PathBinding sourceReceipt, sourceEvidence, relinkEvidence, baselineEvidence, editEvidence; }
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
        Events.Add(row); byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(D("schema", "obsolete-door-isolated-migration-v1", "events", Events)));
        Journal.Position = 0; Journal.Write(bytes, 0, bytes.Length); Journal.SetLength(bytes.Length); Journal.Flush(true);
    }
    private static void SafeRecord(object row) { try { Record(row); } catch (Exception error) { LoggingFailed = true; Console.Error.WriteLine(error); } }
    private static object[] ArrayValues(object value)
    {
        if (value == null) return new object[0]; Require(value is Array || value is IList, "EXPECTED_VARIANT_ARRAY " + value.GetType().FullName);
        return ((IEnumerable)value).Cast<object>().ToArray();
    }
    private static double[] Numbers(object value, int minimum, string context)
    {
        Require(value is Array, "NUMERIC_ARRAY_UNAVAILABLE " + context);
        var result = ArrayValues(value).Select(Convert.ToDouble).ToArray();
        Require(result.Length >= minimum && result.All(n => !double.IsNaN(n) && !double.IsInfinity(n)), "INVALID_NUMERIC_ARRAY " + context); return result;
    }
    private static void Release(object value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
    private sealed class IdentityScope : IDisposable
    {
        private readonly Dictionary<long, IntPtr> Held = new Dictionary<long, IntPtr>();
        internal long Acquire(object value)
        {
            IntPtr pointer = Marshal.GetIUnknownForObject(value); long key = pointer.ToInt64();
            if (Held.ContainsKey(key)) Marshal.Release(pointer); else Held.Add(key, pointer); return key;
        }
        public void Dispose() { foreach (IntPtr pointer in Held.Values) Marshal.Release(pointer); Held.Clear(); }
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
    private static void EvidenceBinding(PathBinding binding, string context)
    {
        Require(binding != null && ValidSha(binding.sha256), context + "_BINDING_REQUIRED");
        Plain(binding.path); Require(Inside(Path.Combine(AllowedRoot, "runs"), binding.path) && !Inside(Pack, binding.path) && !Same(binding.path, Output) && Hash(binding.path) == binding.sha256, context + "_EVIDENCE_BINDING_FAILED");
    }
    private static bool SameBinding(PathBinding a, PathBinding b)
    {
        return a != null && b != null && Same(a.path, b.path) && a.sha256 == b.sha256;
    }
    private static void ProofManifest(string evidence, string mode)
    {
        var row = EventFrom(evidence, "manifest_binding", null);
        string path = Full(Convert.ToString(row["manifest"])), sha = Convert.ToString(row["sha256"]);
        Plain(path); Require(Inside(Path.Combine(AllowedRoot, "runs"), path) && !Inside(Pack, path) && !Same(path, Output) && ValidSha(sha) && Hash(path) == sha, "PROOF_MANIFEST_BINDING_FAILED " + mode);
        string priorSha; Require(!FrozenProofManifestHashes.TryGetValue(path, out priorSha) || priorSha == sha, "PROOF_MANIFEST_CONFLICT " + mode); FrozenProofManifestHashes[path] = sha;
        Manifest prior = Json.Deserialize<Manifest>(File.ReadAllText(path));
        Require(prior != null && Same(prior.sourcePack, SourcePack) && Same(prior.candidatePack, Pack) && SameBinding(prior.sourceReceipt, Binding.sourceReceipt) && SameBinding(prior.sourceEvidence, Binding.sourceEvidence), "PROOF_SOURCE_CHAIN_MISMATCH " + mode);
        Require(prior.files != null && prior.files.Length == 75 && prior.files.Select(f => f.fileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 75, "PROOF_FILE_BINDINGS_INVALID " + mode);
        var priorFiles = prior.files.ToDictionary(f => f.fileName, StringComparer.OrdinalIgnoreCase);
        foreach (FileBinding file in Binding.files)
        {
            FileBinding before; Require(priorFiles.TryGetValue(file.fileName, out before) && before.fileName == file.fileName && Same(before.sourcePath, file.sourcePath) && Same(before.path, file.path) && before.sha256 == file.sha256 && before.size == file.size, "PROOF_SOURCE_FILE_BINDING_MISMATCH " + mode + "/" + file.fileName);
            if (mode != "relink") Require(before.candidateSha256 == file.candidateSha256 && before.candidateSize == file.candidateSize, "PROOF_CANDIDATE_FILE_BINDING_MISMATCH " + mode + "/" + file.fileName);
        }
        if (mode != "relink") Require(SameBinding(prior.relinkEvidence, Binding.relinkEvidence), "PROOF_RELINK_CHAIN_MISMATCH " + mode);
        if (mode == "edit") Require(SameBinding(prior.baselineEvidence, Binding.baselineEvidence), "PROOF_BASELINE_CHAIN_MISMATCH");
    }
    private static List<FileState> ProofCandidateInventory(string evidence, string stage, string editedRootSha)
    {
        var inventory = EventFrom(evidence, "cad_inventory", stage);
        Require(Same(Convert.ToString(inventory["pack"]), Pack) && Convert.ToInt32(inventory["count"]) == 75, "PROOF_INVENTORY_PACK_OR_COUNT_MISMATCH " + stage);
        var rows = ArrayValues(inventory["files"]).Cast<Dictionary<string, object>>().Select(row => new FileState {
            path = Full(Convert.ToString(row["path"])), sha256 = Convert.ToString(row["sha256"]), size = Convert.ToInt64(row["size"]), ticks = Convert.ToInt64(row["ticks"]), attributes = Convert.ToInt32(row["attributes"])
        }).ToList();
        Require(rows.Count == 75 && rows.Select(row => row.path).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 75, "PROOF_INVENTORY_EXACT_75_REQUIRED " + stage);
        var byPath = rows.ToDictionary(row => row.path, StringComparer.OrdinalIgnoreCase);
        foreach (FileBinding file in Binding.files)
        {
            FileState row; Require(byPath.TryGetValue(Full(file.path), out row), "PROOF_INVENTORY_FILE_MISSING " + file.fileName);
            bool editedRoot = editedRootSha != null && Same(row.path, Target);
            Require(row.sha256 == (editedRoot ? editedRootSha : file.candidateSha256) && row.size > 0 && (editedRoot || row.size == file.candidateSize) && row.ticks > 0 && (row.attributes & (int)(FileAttributes.ReparsePoint | FileAttributes.Directory)) == 0, "PROOF_INVENTORY_FILE_BINDING_FAILED " + file.fileName);
        }
        return rows;
    }
    private static void BaselineProof()
    {
        EvidenceBinding(Binding.baselineEvidence, "BASELINE"); ProofManifest(Binding.baselineEvidence.path, "baseline");
        var completed = EventFrom(Binding.baselineEvidence.path, "completion", null);
        Require(Convert.ToString(completed["mode"]) == "baseline" && Same(Convert.ToString(completed["candidatePack"]), Pack) && object.Equals(completed["diagnosticCompleted"], true) && object.Equals(completed["operationComplete"], false) && object.Equals(completed["modelAccepted"], false) && object.Equals(completed["strictColdAcceptancePassed"], false) && object.Equals(completed["all75SourceUnchanged"], true) && object.Equals(completed["all75CandidateUnchanged"], true) && object.Equals(completed["ownedCadExited"], true), "BASELINE_DID_NOT_COMPLETE_FOR_THIS_PACK");
        var raw = EventFrom(Binding.baselineEvidence.path, "raw_open", null);
        Require(Convert.ToString(raw["mode"]) == "baseline" && Convert.ToInt32(raw["options"]) == 33 && Convert.ToInt32(raw["errors"]) == 0 && Convert.ToInt32(raw["warnings"]) == 32769 && Same(Convert.ToString(raw["actualPath"]), Target) && object.Equals(raw["actualReadOnly"], false), "BASELINE_EXACT_MIGRATION_INPUT_REQUIRED");
        CompareFiles(CandidateInitialStates, ProofCandidateInventory(Binding.baselineEvidence.path, "candidate-before", null), false, "baseline-start-matches-relink");
        CompareFiles(CandidateInitialStates, ProofCandidateInventory(Binding.baselineEvidence.path, "candidate-after", null), false, "baseline-end-matches-relink");
        IndependentBaselineState = EventFrom(Binding.baselineEvidence.path, "snapshot", "before-delete-baseline")["state"];
    }
    private static void ValidateManifest()
    {
        Plain(ManifestPath); Require(Inside(Path.Combine(AllowedRoot, "runs"), ManifestPath) && !Inside(Pack, ManifestPath), "MANIFEST_PATH_NOT_AUTHORIZED");
        ManifestSha = Hash(ManifestPath); Binding = Json.Deserialize<Manifest>(File.ReadAllText(ManifestPath));
        Require(Binding != null && Same(Binding.sourcePack, SourcePack) && Same(Binding.candidatePack, CandidatePack), "MANIFEST_PACK_BINDING_FAILED");
        Require(Binding.sourceReceipt != null && Same(Binding.sourceReceipt.path, ReceiptPath) && Binding.sourceReceipt.sha256 == ReceiptSha256, "RECEIPT_BINDING_FAILED");
        Require(Binding.sourceEvidence != null && Same(Binding.sourceEvidence.path, EvidencePath) && Binding.sourceEvidence.sha256 == EvidenceSha256, "SOURCE_EVIDENCE_BINDING_FAILED");
        Plain(ReceiptPath); Plain(EvidencePath); Require(Hash(ReceiptPath) == ReceiptSha256 && Hash(EvidencePath) == EvidenceSha256, "CHECKPOINT_RECEIPT_OR_EVIDENCE_CHANGED");
        Receipt receipt = Json.Deserialize<Receipt>(File.ReadAllText(ReceiptPath));
        Require(receipt.phase == "assemblies" && receipt.evidenceSha256 == EvidenceSha256 && receipt.after != null && receipt.after.Length == 75, "ASSEMBLIES_CHECKPOINT_INVALID");
        Require(Binding.files != null && Binding.files.Length == 75 && Binding.files.Select(f => f.fileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 75, "MANIFEST_EXACT_75_REQUIRED");
        var recorded = receipt.after.ToDictionary(r => Full(r.path), StringComparer.OrdinalIgnoreCase);
        foreach (FileBinding file in Binding.files)
        {
            Require(!string.IsNullOrWhiteSpace(file.fileName) && Path.GetFileName(file.fileName) == file.fileName && ValidSha(file.sha256) && ValidSha(file.candidateSha256) && file.size > 0 && file.candidateSize > 0, "INVALID_FILE_BINDING");
            Require(Same(file.sourcePath, Path.Combine(SourcePack, file.fileName)) && Same(file.path, Path.Combine(Pack, file.fileName)), "FILE_PATH_BINDING_FAILED " + file.fileName);
            ReceiptFile row; Require(recorded.TryGetValue(Full(file.sourcePath), out row) && row.sha256 == file.sha256, "FILE_NOT_BOUND_TO_RECEIPT " + file.fileName);
            Plain(file.sourcePath); Plain(file.path); Require(Hash(file.sourcePath) == file.sha256 && new FileInfo(file.sourcePath).Length == file.size, "SOURCE_CHECKPOINT_CHANGED " + file.fileName);
            ExpectedHashes.Add(Full(file.path), file.candidateSha256);
        }
        Require(ExpectedHashes.ContainsKey(Target), "ROOT_MISSING_FROM_MANIFEST");
        EvidenceBinding(Binding.relinkEvidence, "RELINK"); ProofManifest(Binding.relinkEvidence.path, "relink");
        var relink = EventFrom(Binding.relinkEvidence.path, "completion", null);
        Require(Convert.ToString(relink["mode"]) == "relink" && Same(Convert.ToString(relink["candidatePack"]), Pack) && object.Equals(relink["diagnosticCompleted"], true) && object.Equals(relink["operationComplete"], false) && object.Equals(relink["modelAccepted"], false) && object.Equals(relink["strictColdAcceptancePassed"], false), "RELINK_DID_NOT_COMPLETE_FOR_THIS_PACK");
        CandidateInitialStates = ProofCandidateInventory(Binding.relinkEvidence.path, "candidate-after", null);
        if (Mode == "baseline") Require(Binding.baselineEvidence == null, "BASELINE_MUST_BE_INDEPENDENT"); else BaselineProof();
        if (Mode == "verify" || Mode == "diagnostic33")
        {
            Require(ValidSha(Binding.expectedRootSha256), "VERIFY_EDIT_ROOT_BINDING_REQUIRED");
            EvidenceBinding(Binding.editEvidence, "EDIT"); ProofManifest(Binding.editEvidence.path, "edit");
            var completed = EventFrom(Binding.editEvidence.path, "completion", null);
            Require(object.Equals(completed["operationComplete"], true) && Convert.ToString(completed["mode"]) == "edit" && Same(Convert.ToString(completed["candidatePack"]), Pack) && Convert.ToString(completed["rootAfterSha256"]) == Binding.expectedRootSha256, "EDIT_DID_NOT_COMPLETE_FOR_THIS_ROOT");
            var plan = EventFrom(Binding.editEvidence.path, "migration_plan", null);
            foreach (object path in ArrayValues(plan["removedExclusivePaths"])) { string full = Full(Convert.ToString(path)); Require(ExpectedHashes.ContainsKey(full) && !Same(full, Target), "INVALID_REMOVED_PATH_BINDING"); RemovedExclusivePaths.Add(full); }
            Require(RemovedExclusivePaths.Count > 0, "REMOVED_REFERENCE_SET_MISSING"); ExpectedHashes[Target] = Binding.expectedRootSha256;
            var edited = ProofCandidateInventory(Binding.editEvidence.path, "candidate-after", Binding.expectedRootSha256);
            CompareFiles(CandidateInitialStates, edited, true, "edit-proof-other74-match-relink"); CandidateInitialStates = edited;
        }
        else Require(Binding.editEvidence == null && string.IsNullOrEmpty(Binding.expectedRootSha256), "EDIT_MUST_START_FROM_UNMODIFIED_RELINK_CHECKPOINT");
        Require(Files(Pack).All(p => ExpectedHashes.ContainsKey(p)), "CANDIDATE_FILE_SET_DIFFERS");
        foreach (var row in ExpectedHashes) Require(Hash(row.Key) == row.Value, "CANDIDATE_HASH_BINDING_FAILED " + row.Key);
        LoadReferenceSourceWhitelist();
        Record(D("inspection", "manifest_binding", "manifest", ManifestPath, "sha256", ManifestSha, "sourcePack", SourcePack, "candidatePack", Pack, "sourceReceipt", Binding.sourceReceipt,
            "sourceEvidence", Binding.sourceEvidence, "relinkEvidence", Binding.relinkEvidence, "baselineEvidence", Binding.baselineEvidence, "files", Binding.files, "expectedRootSha256", Binding.expectedRootSha256, "editEvidence", Binding.editEvidence));
        FrozenBindings();
    }
    private static void LoadReferenceSourceWhitelist()
    {
        Plain(ReferenceBaselinePath); Require(Hash(ReferenceBaselinePath) == ReferenceBaselineSha256, "REFERENCE_BASELINE_CHANGED");
        var completed = EventFrom(ReferenceBaselinePath, "completion", null);
        Require(object.Equals(completed["operationComplete"], true), "REFERENCE_BASELINE_NOT_COMPLETE");
        var baseline = EventFrom(ReferenceBaselinePath, "components", null);
        Require(Same(Convert.ToString(baseline["path"]), Path.Combine(SourcePack, RootFile)), "REFERENCE_BASELINE_ROOT_MISMATCH");
        var retained = ArrayValues(baseline["tree"]).Cast<Dictionary<string, object>>().Where(row => Convert.ToInt32(row["depth"]) > 0 &&
            Convert.ToString(row["name"]).IndexOf("储物柜门装配_", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
        Require(retained.Length == 80, "REFERENCE_BASELINE_RETAINED_80_REQUIRED");
        ReferenceSourceBindings.Clear();
        foreach (var component in retained)
        {
            var references = component["externalReferences"] as Dictionary<string, object>;
            Require(references != null && object.Equals(references["readSucceeded"], true) && object.Equals(references["parallelArrayLengthsMatch"], true), "REFERENCE_BASELINE_ARRAYS_UNKNOWN");
            foreach (string key in new[] { "modelPathNames", "componentPathNames" }) foreach (object raw in ArrayValues(references[key]))
            {
                string value = Convert.ToString(raw); if (string.IsNullOrWhiteSpace(value)) continue;
                string full = Full(value); FileBinding[] matches = Binding.files.Where(file => Same(file.sourcePath, full)).ToArray();
                Require(Inside(SourcePack, full) && matches.Length == 1, "REFERENCE_BASELINE_NOT_BOUND_TO_MANIFEST " + full);
                ReferenceSourceBindings[full] = matches[0];
            }
        }
        Require(ReferenceSourceBindings.Count == 15, "REFERENCE_BASELINE_EXACT_15_PATHS_REQUIRED");
        Record(D("inspection", "reference_source_whitelist", "baseline", ReferenceBaselinePath, "baselineSha256", ReferenceBaselineSha256,
            "retainedInstanceCount", retained.Length, "sourcePathCount", ReferenceSourceBindings.Count,
            "bindings", ReferenceSourceBindings.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase).Select(row => D("sourcePath", row.Key,
                "sourceSha256", row.Value.sha256, "sourceSize", row.Value.size, "candidatePath", Full(row.Value.path))).ToArray()));
    }
    private static string BindPath(string path)
    {
        string full = Full(path), expected = null; Require(Inside(Pack, full) && ExpectedHashes.TryGetValue(full, out expected), "PATH_OUTSIDE_BOUND_CANDIDATE " + full);
        Plain(full); Require(Hash(full) == expected, "BOUND_CANDIDATE_FILE_CHANGED " + full); return full;
    }
    private static Dictionary<string, object> BindReferencePath(string rawPath)
    {
        string full = Full(rawPath);
        if (Inside(Pack, full)) return D("rawPath", rawPath, "canonicalPath", full, "bindingKind", "candidate", "candidatePath", BindPath(full));
        FileBinding file; Require(ReferenceSourceBindings.TryGetValue(full, out file), "REFERENCE_SOURCE_NOT_IN_BASELINE_WHITELIST " + full);
        Plain(full); Require(File.Exists(full) && (File.GetAttributes(full) & FileAttributes.Directory) == 0, "REFERENCE_SOURCE_NOT_REGULAR_FILE " + full);
        Require(new FileInfo(full).Length == file.size && Hash(full) == file.sha256, "REFERENCE_SOURCE_CHECKPOINT_CHANGED " + full);
        string counterpart = BindPath(file.path);
        return D("rawPath", rawPath, "canonicalPath", full, "bindingKind", "serialized-source-checkpoint", "sourcePath", Full(file.sourcePath),
            "sourceSha256", file.sha256, "sourceSize", file.size, "candidatePath", counterpart);
    }
    private static object Persistent(ModelDocExtension extension, object value, string context)
    {
        Require(extension != null && value != null, "PERSISTENT_REFERENCE_INPUT_NULL " + context);
        object raw = extension.GetPersistReference3(value); object[] bytes = ArrayValues(raw);
        Require(bytes.Length > 0, "PERSISTENT_REFERENCE_UNAVAILABLE " + context); return Convert.ToBase64String(bytes.Select(Convert.ToByte).ToArray());
    }
    private static bool NumberType(object value) { return value is double || value is float || value is decimal; }
    private static void Equal(object before, object after, string context)
    {
        if (before == null || after == null) { Require(before == null && after == null, "STATE_DRIFT " + context); return; }
        var a = before as IDictionary<string, object>; var b = after as IDictionary<string, object>;
        if (a != null || b != null)
        {
            Require(a != null && b != null && a.Count == b.Count && a.Keys.All(b.ContainsKey), "OBJECT_FIELDS_DRIFT " + context);
            foreach (string key in a.Keys) Equal(a[key], b[key], context + "/" + key); return;
        }
        if (!(before is string) && before is IEnumerable && !(after is string) && after is IEnumerable)
        {
            object[] aa = ((IEnumerable)before).Cast<object>().ToArray(), bb = ((IEnumerable)after).Cast<object>().ToArray();
            Require(aa.Length == bb.Length, "ARRAY_COUNT_DRIFT " + context); for (int i = 0; i < aa.Length; i++) Equal(aa[i], bb[i], context + "[" + i + "]"); return;
        }
        if (NumberType(before) || NumberType(after))
        {
            double x = Convert.ToDouble(before), y = Convert.ToDouble(after);
            Require(!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y) && Math.Abs(x - y) <= 1e-12 + Math.Max(Math.Abs(x), Math.Abs(y)) * 1e-10, "NUMERIC_STATE_DRIFT " + context); return;
        }
        Require(string.Equals(Convert.ToString(before, CultureInfo.InvariantCulture), Convert.ToString(after, CultureInfo.InvariantCulture), StringComparison.Ordinal), "STATE_DRIFT " + context);
    }

    private static bool ObsoleteName(string text)
    {
        return !string.IsNullOrEmpty(text) && OldNames.Any(name => text.IndexOf(name.Substring(0, name.Length - 2), StringComparison.OrdinalIgnoreCase) >= 0);
    }
    private static Dictionary<string, object> References(object files, object parts, object features, object types, object statuses, object entities, object components, string context, bool audit)
    {
        string[] keys = { "modelPathNames", "componentPathNames", "features", "dataTypes", "statuses", "referencedEntities", "featureComponents" };
        object[][] arrays = new[] { files, parts, features, types, statuses, entities, components }.Select(ArrayValues).ToArray();
        if (arrays.Any(array => array.Length > 0))
        {
            var rawRows = new List<object>();
            for (int i = 0; i < arrays.Max(array => array.Length); i++)
            {
                var rawRow = new Dictionary<string, object>(); for (int n = 0; n < keys.Length; n++) rawRow[keys[n]] = i < arrays[n].Length ? arrays[n][i] : null;
                rawRows.Add(rawRow);
            }
            Record(D("inspection", "external_reference_raw", "owner", context, "auditPreservedStructure", audit,
                "arrayLengths", arrays.Select(array => array.Length).ToArray(), "rows", rawRows));
        }
        Require(arrays.All(a => a.Length == arrays[0].Length), "EXTERNAL_REFERENCE_ARRAY_LENGTH_MISMATCH " + context);
        var rows = new List<object>();
        for (int i = 0; i < arrays[0].Length; i++)
        {
            var row = new Dictionary<string, object>(); for (int n = 0; n < keys.Length; n++) row[keys[n]] = arrays[n][i];
            if (audit)
            {
                for (int n = 0; n < keys.Length; n++)
                {
                    Require(arrays[n][i] != null, "EXTERNAL_REFERENCE_VALUE_UNKNOWN " + context + "/" + keys[n]);
                    string value = Convert.ToString(arrays[n][i]);
                    Require(!ObsoleteName(value), "PRESERVED_REFERENCE_POINTS_TO_OBSOLETE_DOOR " + context + "/" + keys[n] + "=" + value);
                    if (n < 2 && !string.IsNullOrWhiteSpace(value))
                    {
                        var binding = BindReferencePath(value); row[keys[n] + "Binding"] = binding;
                        string path = Convert.ToString(binding["candidatePath"]); Require(!RemovedExclusivePaths.Contains(path), "PRESERVED_REFERENCE_POINTS_TO_REMOVED_SUBTREE " + context + "=" + path);
                    }
                }
                Require(!string.IsNullOrWhiteSpace(Convert.ToString(arrays[0][i])) &&
                    (!string.IsNullOrWhiteSpace(Convert.ToString(arrays[5][i])) || !string.IsNullOrWhiteSpace(Convert.ToString(arrays[6][i]))), "EXTERNAL_REFERENCE_TARGET_UNKNOWN " + context);
            }
            rows.Add(row);
        }
        return D("count", rows.Count, "auditPreservedStructure", audit, "rows", rows);
    }
    private static Dictionary<string, object> DocumentReferences(ModelDoc2 model, string path, bool audit)
    {
        object files, parts, features, types, statuses, entities, components;
        model.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
        return References(files, parts, features, types, statuses, entities, components, path, audit);
    }
    private static Dictionary<string, object> FeatureReferences(Feature feature, string path)
    {
        object files, parts, features, types, statuses, entities, components; int configOption; string configName;
        feature.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components, out configOption, out configName);
        var row = References(files, parts, features, types, statuses, entities, components, path + "/" + feature.Name, true);
        row["configurationOption"] = configOption; row["configurationName"] = configName; return row;
    }
    private static Dictionary<string, object> ComponentReferences(Component2 component)
    {
        object files, parts, features, types, statuses, entities, components; int configOption; string configName;
        component.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components, out configOption, out configName);
        var row = References(files, parts, features, types, statuses, entities, components, component.Name2, true);
        row["configurationOption"] = configOption; row["configurationName"] = configName; return row;
    }
    private static Dictionary<string, object> MateState(Mate2 mate, ModelDocExtension extension, string owner)
    {
        Require(mate != null, "MATE_INTERFACE_UNKNOWN " + owner); int count = mate.GetMateEntityCount();
        Require(count > 0 && count <= 32, "MATE_ENTITY_COUNT_UNKNOWN " + owner); var entities = new List<object>();
        for (int i = 0; i < count; i++)
        {
            MateEntity2 entity = null; Component2 component = null; object reference = null;
            try
            {
                entity = mate.MateEntity(i); Require(entity != null, "MATE_ENTITY_NULL " + owner);
                component = entity.ReferenceComponent; Require(component != null, "MATE_REFERENCE_COMPONENT_UNKNOWN " + owner);
                reference = entity.Reference; Require(reference != null, "MATE_REFERENCE_NULL " + owner);
                string path = BindPath(component.GetPathName()); string name = component.Name2;
                Require(!ObsoleteName(name) && !RemovedExclusivePaths.Contains(path), "MATE_DEPENDS_ON_OBSOLETE_DOOR " + owner + "/" + name);
                int type = entity.ReferenceType; Require(type >= 0, "MATE_REFERENCE_TYPE_UNKNOWN " + owner);
                object parameters = entity.EntityParams;
                entities.Add(D("index", i, "componentName", name, "componentPath", path, "ownerDocumentReference", false,
                    "referenceType", type, "persistentReference", Persistent(extension, reference, owner + "/mate-entity-" + i),
                    "parameters", parameters == null ? null : (object)Numbers(parameters, 1, owner + "/mate-entity-parameters")));
            }
            finally { Release(reference); Release(component); Release(entity); }
        }
        return D("ownerDocumentPath", owner, "type", mate.Type, "alignment", mate.Alignment, "flipped", mate.Flipped, "canBeFlipped", mate.CanBeFlipped, "entityCount", count, "entities", entities);
    }
    private static void Features(Feature first, bool subfeatures, int depth, string owner, ModelDocExtension extension, bool root, List<object> rows, HashSet<long> visited, IdentityScope identities)
    {
        Feature current = first; int guard = 0;
        while (current != null)
        {
            Feature next = null;
            try
            {
                Require(depth <= 128 && guard++ < 20000 && rows.Count < 100000, "FEATURE_TRAVERSAL_LIMIT " + owner);
                next = (subfeatures ? current.GetNextSubFeature() : current.GetNextFeature()) as Feature;
                long id = identities.Acquire(current);
                if (!visited.Add(id)) continue;
                string name = current.Name, type = current.GetTypeName2();
                if (type != null && type.StartsWith("Mate", StringComparison.Ordinal)) Record(D("inspection", "feature_identity", "document", owner, "name", name, "type", type, "depth", depth));
                Require(name != null && type != null, "FEATURE_IDENTITY_UNKNOWN " + owner);
                if (root && type == "Reference" && OldNames.Contains(name, StringComparer.Ordinal)) continue;
                bool warning; int error2 = current.GetErrorCode2(out warning); int error = current.GetErrorCode();
                var row = D("name", name, "type", type, "id", current.GetID(), "depth", depth, "suppressed", current.IsSuppressed(),
                    "errorCode", error, "errorCode2", error2, "warning", warning, "externalReferences", FeatureReferences(current, owner));
                bool isMate = MateFeatureTypes.Contains(type);
                Require(!type.StartsWith("Mate", StringComparison.Ordinal) || isMate || type == "MateGroup" || type == "MateReference" ||
                    type == "MateReferenceGroupFolder" || type == "MaterialFolder", "MATE_FEATURE_TYPE_UNKNOWN " + owner + "/" + name + "=" + type);
                if (isMate)
                {
                    Mate2 mate = current.GetSpecificFeature2() as Mate2;
                    try { row["mate"] = MateState(mate, extension, owner); row["persistentReference"] = Persistent(extension, current, owner + "/" + name); }
                    finally { Release(mate); }
                }
                rows.Add(row);
                Features(current.GetFirstSubFeature() as Feature, true, depth + 1, owner, extension, root, rows, visited, identities);
            }
            finally { Release(current); current = next; }
        }
    }
    private static List<object> FeatureSnapshot(ModelDoc2 model, bool root)
    {
        string path = BindPath(model.GetPathName()); ModelDocExtension extension = model.Extension; var rows = new List<object>();
        try
        {
            Require(extension != null, "MODEL_EXTENSION_NULL " + path);
            using (var identities = new IdentityScope()) Features(model.FirstFeature() as Feature, false, 0, path, extension, root, rows, new HashSet<long>(), identities);
            Require(rows.Count > 0, "FEATURE_TREE_EMPTY " + path);
            if (root)
            {
                var mates = rows.Cast<Dictionary<string, object>>().Where(r => r.ContainsKey("mate")).ToArray();
                Require(mates.Length == 3 && mates.Select(r => (string)r["name"]).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(RootMateNames.OrderBy(n => n, StringComparer.Ordinal)), "ROOT_THREE_MATES_CHANGED");
            }
            return rows;
        }
        finally { Release(extension); }
    }
    private sealed class ComponentHandle { public Component2 value; public string name, path; public int depth; public bool obsolete; }
    private sealed class ComponentSet : IDisposable
    {
        internal readonly List<ComponentHandle> rows = new List<ComponentHandle>();
        private readonly IdentityScope identities = new IdentityScope();
        private readonly HashSet<long> visited = new HashSet<long>();
        internal void Add(Component2 component, int depth, bool obsolete)
        {
            Require(component != null && depth <= 128 && rows.Count < 10000, "COMPONENT_TRAVERSAL_UNKNOWN");
            Require(visited.Add(identities.Acquire(component)), "COMPONENT_CYCLE_OR_DUPLICATE");
            string name = component.Name2, path = BindPath(component.GetPathName());
            Require(!string.IsNullOrWhiteSpace(name) && rows.All(r => r.name != name), "COMPONENT_NAME_NOT_UNIQUE " + name);
            if (depth == 1 && OldNames.Contains(name, StringComparer.Ordinal))
            {
                Require(Same(path, Path.Combine(Pack, name.Substring(0, name.Length - 2) + ".SLDASM")), "OBSOLETE_DOOR_NAME_PATH_MISMATCH " + name); obsolete = true;
            }
            rows.Add(new ComponentHandle { value = component, name = name, path = path, depth = depth, obsolete = obsolete });
            foreach (object child in ArrayValues(component.GetChildren())) { Require(child is Component2, "COMPONENT_CHILD_INTERFACE_UNKNOWN"); Add((Component2)child, depth + 1, obsolete); }
        }
        public void Dispose() { foreach (ComponentHandle row in rows) Release(row.value); identities.Dispose(); }
    }
    private static ComponentSet Tree(ModelDoc2 model)
    {
        var set = new ComponentSet();
        try { foreach (object component in ArrayValues(((AssemblyDoc)model).GetComponents(true))) { Require(component is Component2, "DIRECT_COMPONENT_INTERFACE_UNKNOWN"); set.Add((Component2)component, 1, false); } return set; }
        catch { set.Dispose(); throw; }
    }
    private static object Bodies(Component2 component, ModelDoc2 model)
    {
        object information; object[] bodies = ArrayValues(component.GetBodies3(0, out information)); var rows = new List<object>();
        object[] info = ArrayValues(information); Require(info.Length == 0 || info.Length == bodies.Length, "BODY_INFORMATION_COUNT_UNKNOWN " + component.Name2);
        object[] documentBodies = ArrayValues(((PartDoc)model).GetBodies2(0, false));
        try { Require(documentBodies.Length == bodies.Length, "DOCUMENT_COMPONENT_SOLID_COUNT_DISAGREES " + component.Name2); }
        finally { foreach (object body in documentBodies) Release(body); }
        try
        {
            foreach (object value in bodies)
            {
                Body2 body = value as Body2; Require(body != null, "BODY_INTERFACE_UNKNOWN " + component.Name2);
                double[] box = Numbers(body.GetBodyBox(), 6, component.Name2 + "/bounds"), mass = Numbers(body.GetMassProperties(1.0), 12, component.Name2 + "/mass-properties");
                Require(box.Length == 6 && box[0] <= box[3] && box[1] <= box[4] && box[2] <= box[5] && ((IBody2)body).GetType() == 0 && mass[3] > 0 && mass[4] > 0, "REAL_SOLID_BODY_STATE_INVALID " + component.Name2);
                FaultEntity faults = body.Check3;
                try
                {
                    Require(faults != null, "BODY_CHECK3_RESULT_UNKNOWN " + component.Name2); int faultCount = faults.Count;
                    Require(faultCount == 0, "BODY_CHECK3_FAULTS_PRESENT " + component.Name2 + "/" + faultCount);
                    rows.Add(D("name", body.Name, "bodyType", ((IBody2)body).GetType(), "healthCheckApi", "IBody2.Check3", "faultCount", faultCount, "visible", body.Visible, "sheetMetal", body.IsSheetMetal(),
                        "faceCount", body.GetFaceCount(), "edgeCount", body.GetEdgeCount(), "volumeM3", mass[3], "areaM2", mass[4], "boundsM", box, "massPropertiesDensity1", mass));
                }
                finally { Release(faults); }
            }
        }
        finally { foreach (object body in bodies) Release(body); }
        return D("solidCount", rows.Count, "documentSolidCount", documentBodies.Length, "bodyInformation", info,
            "zeroSolidStateCorroboratedByPartDoc", rows.Count == 0, "bodies", rows.OrderBy(r => Json.Serialize(r), StringComparer.Ordinal).ToArray());
    }
    private static string ActiveConfigurationName(ModelDoc2 model)
    {
        Configuration active = model.GetActiveConfiguration() as Configuration;
        try { Require(active != null && !string.IsNullOrWhiteSpace(active.Name), "LOADED_DOCUMENT_CONFIGURATION_UNKNOWN " + model.GetPathName()); return active.Name; }
        finally { Release(active); }
    }
    private static ComponentHandle ResolvedWitness(IEnumerable<ComponentHandle> handles, string configuration)
    {
        foreach (ComponentHandle candidate in handles.OrderBy(r => r.name, StringComparer.Ordinal))
        {
            if (candidate.value.GetSuppression2() != 2 || candidate.value.IsSuppressed() || candidate.value.ReferencedConfiguration != configuration) continue;
            ModelDoc2 document = candidate.value.GetModelDoc2() as ModelDoc2;
            try
            {
                if (document == null) continue;
                Require(Same(BindPath(document.GetPathName()), candidate.path), "WITNESS_DOCUMENT_PATH_MISMATCH " + candidate.name);
                Require(ActiveConfigurationName(document) == configuration, "WITNESS_ACTIVE_CONFIGURATION_MISMATCH " + candidate.name); return candidate;
            }
            finally { Release(document); }
        }
        throw new InvalidOperationException("RESOLVED_SAME_PATH_CONFIGURATION_WITNESS_MISSING " + configuration);
    }
    private static object ComponentSnapshot(ComponentHandle handle, ComponentHandle witness)
    {
        Component2 component = handle.value; ModelDoc2 model = null; MathTransform transform = null, totalTransform = null;
        try
        {
            Require(!component.IsVirtual, "VIRTUAL_COMPONENT_REJECTED " + handle.name);
            string config = component.ReferencedConfiguration; Require(!string.IsNullOrWhiteSpace(config), "PRESERVED_CONFIGURATION_UNKNOWN " + handle.name);
            int suppression = component.GetSuppression2(); bool suppressed = component.IsSuppressed();
            Require((suppression == 0 && suppressed) || (suppression == 2 && !suppressed), "PRESERVED_SUPPRESSION_OR_IDENTITY_UNKNOWN " + handle.name);
            Require(witness != null && Same(witness.path, handle.path) && witness.value.ReferencedConfiguration == config && witness.value.GetSuppression2() == 2 && !witness.value.IsSuppressed(), "PRESERVED_SAME_PATH_CONFIGURATION_WITNESS_INVALID " + handle.name);
            model = component.GetModelDoc2() as ModelDoc2; bool instanceModelLoaded = model != null;
            Require(instanceModelLoaded || suppressed, "UNSUPPRESSED_MODEL_NOT_LOADED " + handle.name);
            if (!instanceModelLoaded) model = witness.value.GetModelDoc2() as ModelDoc2;
            Require(model != null && Same(BindPath(model.GetPathName()), handle.path), "PRESERVED_WITNESS_MODEL_PATH_MISMATCH " + handle.name);
            Require(ActiveConfigurationName(model) == config, "PRESERVED_WITNESS_CONFIGURATION_MISMATCH " + handle.name);
            Require(ArrayValues(model.GetConfigurationNames()).Select(Convert.ToString).Contains(config, StringComparer.Ordinal), "PRESERVED_CONFIGURATION_MISSING " + handle.name);
            transform = component.Transform2; Require(transform != null, "COMPONENT_TRANSFORM_UNKNOWN " + handle.name);
            totalTransform = component.GetTotalTransform(false); Require(totalTransform != null, "COMPONENT_TOTAL_TRANSFORM_UNKNOWN " + handle.name);
            int docType = ((IModelDoc2)model).GetType(); Require(docType == 1 || docType == 2, "PRESERVED_DOCUMENT_TYPE_UNKNOWN " + handle.name);
            return D("name", handle.name, "path", handle.path, "fileSha256", ExpectedHashes[handle.path], "depth", handle.depth, "configuration", config,
                "transform", Numbers(transform.ArrayData, 13, handle.name + "/Transform2"), "totalTransform", Numbers(totalTransform.ArrayData, 13, handle.name + "/GetTotalTransform(false)"), "fixed", component.IsFixed(), "suppressed", suppressed,
                "suppression", component.GetSuppression(), "suppression2", suppression, "documentType", docType,
                "externalReferences", ComponentReferences(component), "mateEvidence", "owning-document-feature-snapshot", "instanceModelLoaded", instanceModelLoaded,
                "documentEvidenceWitness", suppressed ? witness.name : handle.name,
                "solidBodies", suppressed ? D("instanceGeometryState", "notLoaded-suppressed", "geometryReadPerformed", false, "resolvedSamePathConfigurationWitness", witness.name, "witnessDocumentPath", witness.path, "witnessConfiguration", config) :
                    (docType == 1 ? Bodies(component, model) : D("container", true)));
        }
        finally { Release(totalTransform); Release(transform); Release(model); }
    }
    private static Dictionary<string, object> Snapshot(ISldWorks sw, ModelDoc2 model, string stage, bool beforeDelete)
    {
        using (ComponentSet set = Tree(model))
        {
            var obsolete = set.rows.Where(r => r.depth == 1 && r.obsolete).ToArray();
            Require(set.rows.Count(r => r.depth == 1) == (beforeDelete ? 25 : 19) && obsolete.Length == (beforeDelete ? 6 : 0), "DIRECT_INSTANCE_CONTRACT_FAILED " + stage);
            if (beforeDelete)
            {
                Require(obsolete.Select(r => r.name).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(OldNames.OrderBy(n => n, StringComparer.Ordinal)), "SIX_OBSOLETE_NAMES_REQUIRED");
                var keepPaths = new HashSet<string>(set.rows.Where(r => !r.obsolete).Select(r => r.path), StringComparer.OrdinalIgnoreCase);
                foreach (string path in set.rows.Where(r => r.obsolete).Select(r => r.path).Distinct(StringComparer.OrdinalIgnoreCase)) if (!keepPaths.Contains(path)) RemovedExclusivePaths.Add(path);
                Record(D("inspection", "migration_plan", "obsoleteDirect", obsolete.Select(r => D("name", r.name, "path", r.path, "sha256", ExpectedHashes[r.path])).ToArray(),
                    "obsoleteSubtreeInstanceCount", set.rows.Count(r => r.obsolete), "retainedInstanceCount", set.rows.Count(r => !r.obsolete), "removedExclusivePaths", RemovedExclusivePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray()));
            }
            Require(set.rows.Count(r => !r.obsolete) == 80, "RETAINED_80_INSTANCE_CONTRACT_FAILED " + stage);
            ModelDocExtension extension = model.Extension; var components = new List<object>(); var docs = new List<object>();
            try
            {
                foreach (ComponentHandle handle in set.rows.Where(r => !r.obsolete).OrderBy(r => r.name, StringComparer.Ordinal))
                {
                    ComponentHandle witness = handle.value.GetSuppression2() == 2 && !handle.value.IsSuppressed() ? handle :
                        ResolvedWitness(set.rows.Where(r => !r.obsolete && Same(r.path, handle.path)), handle.value.ReferencedConfiguration);
                    components.Add(ComponentSnapshot(handle, witness));
                }
                foreach (var group in set.rows.Where(r => !r.obsolete).GroupBy(r => r.path, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ComponentHandle witness = ResolvedWitness(group, group.First().value.ReferencedConfiguration);
                    ModelDoc2 document = witness.value.GetModelDoc2() as ModelDoc2;
                    try
                    {
                        Require(document != null, "PRESERVED_DOCUMENT_NULL " + group.Key);
                        docs.Add(D("path", BindPath(document.GetPathName()), "fileSha256", ExpectedHashes[group.Key], "resolvedWitness", witness.name, "activeConfiguration", ActiveConfigurationName(document), "externalReferences", DocumentReferences(document, group.Key, true), "features", FeatureSnapshot(document, false)));
                    }
                    finally { Release(document); }
                }
                Require(docs.Count == 41, "RETAINED_41_DOCUMENT_CONTRACT_FAILED " + stage);
                var snapshot = D("directRetainedCount", 19, "retainedInstanceCount", components.Count, "retainedDocumentCount", docs.Count,
                    "components", components, "documents", docs, "rootFeatures", FeatureSnapshot(model, true));
                Record(D("inspection", "root_aggregate_references", "stage", stage, "interpretation", "aggregate only; removed-document rows are not root-feature dependencies", "references", DocumentReferences(model, Target, false)));
                Record(D("inspection", "snapshot", "stage", stage, "state", snapshot)); Loaded(sw, stage); return snapshot;
            }
            finally { Release(extension); }
        }
    }

    private static void Loaded(ISldWorks sw, string stage)
    {
        object[] documents = ArrayValues(sw.GetDocuments()); var rows = new List<object>();
        try
        {
            foreach (object value in documents)
            {
                ModelDoc2 model = value as ModelDoc2; Require(model != null, "LOADED_DOCUMENT_INTERFACE_UNKNOWN");
                string path = BindPath(model.GetPathName());
                rows.Add(D("path", path, "sha256", ExpectedHashes[path], "readOnly", model.IsOpenedReadOnly(), "saveFlag", model.GetSaveFlag()));
            }
            Require(documents.Length == sw.GetDocumentCount(), "LOADED_DOCUMENT_COUNT_DISAGREES");
            Record(D("inspection", "loaded_documents", "stage", stage, "count", documents.Length, "allPathsBoundToCandidate", true, "documents", rows));
        }
        finally { foreach (object value in documents) Release(value); }
    }
    private static void DeleteOldDoors(ModelDoc2 model)
    {
        Require(Mode == "edit" && Same(model.GetPathName(), Target) && !model.IsOpenedReadOnly() && !LoggingFailed, "MUTATION_NOT_AUTHORIZED");
        using (ComponentSet set = Tree(model))
        {
            var targets = set.rows.Where(r => r.depth == 1 && OldNames.Contains(r.name, StringComparer.Ordinal)).ToArray();
            Require(targets.Length == 6 && set.rows.Count(r => r.depth == 1) == 25, "DELETE_EXACT_SIX_PRECONDITION_FAILED");
            foreach (ComponentHandle row in targets)
                Require(row.value.GetSuppression2() == 2 && !row.value.IsSuppressed(), "OBSOLETE_DOOR_MUST_ALREADY_BE_RESOLVED " + row.name);
            model.ClearSelection2(true);
            foreach (ComponentHandle row in targets) Require(row.value.Select4(true, null, false), "OBSOLETE_DOOR_SELECT_FAILED " + row.name);
            SelectionMgr selection = model.SelectionManager as SelectionMgr;
            try
            {
                Require(selection != null && selection.GetSelectedObjectCount2(-1) == 6, "DELETE_SELECTION_COUNT_NOT_SIX");
                for (int i = 1; i <= 6; i++)
                {
                    Component2 component = selection.GetSelectedObject6(i, -1) as Component2;
                    try { Require(component != null && targets.Any(r => r.name == component.Name2 && Same(r.path, component.GetPathName())), "DELETE_SELECTED_OBJECT_NOT_EXACT_OLD_DOOR"); }
                    finally { Release(component); }
                }
            }
            finally { Release(selection); }
            Require(!LoggingFailed && Same(model.GetPathName(), Target), "DELETE_LOG_OR_IDENTITY_GUARD_FAILED");
            bool deleted = model.Extension.DeleteSelection2(0); model.ClearSelection2(true);
            object[] remaining = ArrayValues(((AssemblyDoc)model).GetComponents(true));
            try
            {
                Require(deleted && remaining.Length == 19 && remaining.Cast<Component2>().All(c => !OldNames.Contains(c.Name2, StringComparer.Ordinal)), "OBSOLETE_DOOR_DELETE_FAILED");
                Record(D("inspection", "delete_direct", "assembly", Target, "requested", targets.Select(r => D("name", r.name, "path", r.path)).ToArray(), "selected", 6, "deleteSelectionOptions", 0, "deleted", deleted,
                    "remainingDirectNames", remaining.Cast<Component2>().Select(c => c.Name2).OrderBy(n => n, StringComparer.Ordinal).ToArray()));
            }
            finally { foreach (object value in remaining) Release(value); }
        }
    }
    private static void RootHealthy(Dictionary<string, object> snapshot)
    {
        foreach (var feature in ((IEnumerable)snapshot["rootFeatures"]).Cast<Dictionary<string, object>>())
            Require(Convert.ToInt32(feature["errorCode"]) == 0 && Convert.ToInt32(feature["errorCode2"]) == 0, "ROOT_FEATURE_ERROR_NONZERO " + feature["name"]);
    }
    private static void FrozenBindings()
    {
        Require(Hash(ManifestPath) == ManifestSha && Hash(ReceiptPath) == ReceiptSha256 && Hash(EvidencePath) == EvidenceSha256 && Hash(ReferenceBaselinePath) == ReferenceBaselineSha256, "FROZEN_BINDING_CHANGED");
        Require(Hash(Binding.relinkEvidence.path) == Binding.relinkEvidence.sha256, "RELINK_EVIDENCE_CHANGED");
        if (Mode != "baseline") Require(Hash(Binding.baselineEvidence.path) == Binding.baselineEvidence.sha256, "BASELINE_EVIDENCE_CHANGED");
        if (Mode == "verify" || Mode == "diagnostic33") Require(Hash(Binding.editEvidence.path) == Binding.editEvidence.sha256, "EDIT_EVIDENCE_CHANGED");
        foreach (var proof in FrozenProofManifestHashes) Require(Hash(proof.Key) == proof.Value, "PROOF_MANIFEST_CHANGED " + proof.Key);
        Record(D("inspection", "frozen_bindings", "manifest", ManifestPath, "manifestSha256", ManifestSha, "sourceReceipt", Binding.sourceReceipt, "sourceEvidence", Binding.sourceEvidence,
            "relinkEvidence", Binding.relinkEvidence, "baselineEvidence", Binding.baselineEvidence, "editEvidence", Binding.editEvidence, "proofManifestHashes", FrozenProofManifestHashes,
            "referenceBaseline", ReferenceBaselinePath, "referenceBaselineSha256", ReferenceBaselineSha256));
    }
    private static void VerifyOwner(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable)
    {
        Require(process != null && handle != IntPtr.Zero && process.Handle == handle && !process.HasExited, "OWNED_PROCESS_HANDLE_NOT_LIVE");
        Require(process.Id == pid && process.StartTime.ToUniversalTime().Ticks == ticks && Same(process.MainModule.FileName, executable) && sw.GetProcessID() == pid, "OWNED_PROCESS_IDENTITY_CHANGED");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4 || (args[0] != "baseline" && args[0] != "edit" && args[0] != "diagnostic33" && args[0] != "verify")) { Console.Error.WriteLine("Usage: IdentityProbe.exe <baseline|edit|diagnostic33|verify> <candidate_pack_v6> <newOutputJson> <manifestJson>"); return 2; }
        ISldWorks sw = null; ModelDoc2 model = null; Process process = null; IntPtr retainedHandle = IntPtr.Zero;
        int pid = 0; long ticks = 0; string executable = null, errorText = "", rootAfterSha = "";
        bool owned = false, closed = false, exited = false, operationChecked = false, filesChecked = false, passed = false, exitRequested = false, savedRoot = false;
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
                "openOptions", Mode == "verify" ? 35 : 33, "throwawayExperiment", true, "productionAcceptanceEvaluated", false, "warningCauseProven", false));
            ValidateManifest(); sourceBefore = Inventory(SourcePack, "source-before"); packBefore = Inventory(Pack, "candidate-before");
            CompareFiles(CandidateInitialStates, packBefore, false, "candidate-start-matches-frozen-proof"); FrozenBindings();
            int[] cad = Pids("SLDWORKS"), monitors = Pids("sldProcMon");
            Record(D("inspection", "empty_process_baseline", "cad", cad, "monitor", monitors)); Require(cad.Length == 0 && monitors.Length == 0, "COLD_EXCLUSIVE_CAD_SESSION_REQUIRED");
            long activation = DateTime.UtcNow.Ticks;
            sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks; Require(sw != null, "SW2020_ACTIVATION_FAILED");
            pid = sw.GetProcessID(); process = Process.GetProcessById(pid); retainedHandle = process.Handle; ticks = process.StartTime.ToUniversalTime().Ticks; executable = process.MainModule.FileName;
            string revision = sw.RevisionNumber(); int initialCount = sw.GetDocumentCount();
            owned = retainedHandle != IntPtr.Zero && !process.HasExited && ticks >= activation && Same(CanonicalExecutable(executable), CanonicalExecutable(CadExecutable)) && process.MainModule.FileVersionInfo.FileMajorPart == 28 && revision.StartsWith("28.", StringComparison.Ordinal) && Pids("SLDWORKS").SequenceEqual(new[] { pid }) && initialCount == 0;
            Record(D("inspection", "session_owner", "cadPid", pid, "startTimeUtcTicks", ticks.ToString(), "startTicks", ticks.ToString(), "executable", executable, "exactOwnershipVerified", owned,
                "canonicalExecutable", CanonicalExecutable(executable), "revision", revision, "initialDocumentCount", initialCount, "processHandleRetained", retainedHandle != IntPtr.Zero));
            Require(owned, "EXCLUSIVE_OWNED_EMPTY_SW2020_REQUIRED"); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); Loaded(sw, "before-open");
            int errors = 0, warnings = 0, options = Mode == "verify" ? 35 : 33;
            model = sw.OpenDoc6(Target, 2, options, "", ref errors, ref warnings) as ModelDoc2;
            Record(D("inspection", "raw_open", "mode", Mode, "options", options, "errors", errors, "warnings", warnings,
                "actualPath", model == null ? null : model.GetPathName(), "actualReadOnly", model == null ? null : (object)model.IsOpenedReadOnly(),
                "migrationInputKnownWarnings", Mode == "edit" && errors == 0 && warnings == 32769, "inputOpenAcceptedAsModel", false, "strictOpenPassed", Mode == "verify" && errors == 0 && warnings == 0));
            Require(model != null && errors == 0 && (Mode == "verify" ? warnings == 0 : Mode == "edit" ? warnings == 32769 : true), "EXACT_RAW_OPEN_GATE_FAILED");
            Require(Same(model.GetPathName(), Target) && ((IModelDoc2)model).GetType() == 2 && model.IsOpenedReadOnly() == (Mode == "verify"), "RETURNED_ROOT_IDENTITY_OR_MODE_MISMATCH");
            Loaded(sw, "after-open"); FrozenBindings();
            if (Mode == "baseline")
            {
                var baseline = Snapshot(sw, model, "before-delete-baseline", true); RootHealthy(baseline); FrozenBindings();
                Record(D("inspection", "baseline_collection", "stage", "before-delete-baseline", "readOnlyCollection", true, "actualDocumentReadOnly", false, "saved", false, "rawWarningsDiagnosticOnly", true));
            }
            else if (Mode == "edit")
            {
                var before = Snapshot(sw, model, "before-delete", true); Equal(IndependentBaselineState, before, "independent-cold-baseline-to-before-delete"); FrozenBindings();
                Record(D("inspection", "preservation_comparison", "stage", "independent-cold-baseline-to-before-delete", "equal", true, "rawStateCompared", true));
                Require(!LoggingFailed, "PREMUTATION_LOGGING_FAILED"); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable);
                DeleteOldDoors(model);
                var afterDelete = Snapshot(sw, model, "after-delete-before-rebuild", false); Equal(before, afterDelete, "retained-before-after-delete-before-rebuild"); RootHealthy(afterDelete);
                Record(D("inspection", "preservation_comparison", "stage", "after-delete-before-rebuild", "equal", true, "numericTolerance", "absolute 1e-12 plus relative 1e-10"));
                FrozenBindings();
                bool rebuilt = model.ForceRebuild3(true); Record(D("inspection", "rebuild", "topOnly", true, "result", rebuilt)); Require(rebuilt, "ROOT_REBUILD_FAILED");
                var after = Snapshot(sw, model, "after-rebuild", false); Equal(before, after, "retained-before-after-rebuild"); RootHealthy(after);
                Record(D("inspection", "preservation_comparison", "stage", "after-rebuild", "equal", true, "numericTolerance", "absolute 1e-12 plus relative 1e-10"));
                FrozenBindings(); VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); Loaded(sw, "before-save"); Require(!LoggingFailed && Same(model.GetPathName(), Target), "ROOT_ONLY_SAVE_GUARD_FAILED");
                int saveErrors = 0, saveWarnings = 0; savedRoot = model.Save3(1, ref saveErrors, ref saveWarnings);
                bool saveFlag = model.GetSaveFlag(); Record(D("inspection", "save_root_only", "path", Target, "options", 1, "saved", savedRoot, "errors", saveErrors, "warnings", saveWarnings, "saveFlag", saveFlag));
                Require(savedRoot && saveErrors == 0 && saveWarnings == 0 && !saveFlag, "ROOT_SAVE_GATE_FAILED");
                ExpectedHashes[Target] = Hash(Target); rootAfterSha = ExpectedHashes[Target]; Require(rootAfterSha != Binding.files.Single(f => f.fileName == RootFile).candidateSha256, "ROOT_FILE_DID_NOT_CHANGE");
                var afterSave = Snapshot(sw, model, "after-save", false); Equal(before, afterSave, "retained-before-after-save"); RootHealthy(afterSave);
                Require(!model.GetSaveFlag(), "ROOT_DIRTY_AFTER_FINAL_AUDIT"); Record(D("inspection", "preservation_comparison", "stage", "after-save", "equal", true));
            }
            else if (Mode == "diagnostic33")
            {
                var actual = Snapshot(sw, model, "diagnostic33-reopen", false); var prior = EventFrom(Binding.editEvidence.path, "snapshot", "after-save");
                Equal(prior["state"], actual, "edit-to-new-cold-writable-diagnostic-session"); RootHealthy(actual); rootAfterSha = Hash(Target); FrozenBindings();
                Record(D("inspection", "preservation_comparison", "stage", "diagnostic33-reopen", "equal", true, "rawWarningsDiagnosticOnly", true, "saved", false));
            }
            else
            {
                Require(!model.GetSaveFlag(), "READONLY_ROOT_SAVE_FLAG_SET");
                var actual = Snapshot(sw, model, "readonly-reopen", false); var prior = EventFrom(Binding.editEvidence.path, "snapshot", "after-save");
                Equal(prior["state"], actual, "edit-to-new-cold-readonly-session"); RootHealthy(actual);
                Require(!model.GetSaveFlag(), "READONLY_INSPECTION_DIRTIED_ROOT"); rootAfterSha = Hash(Target);
                Record(D("inspection", "preservation_comparison", "stage", "readonly-reopen", "equal", true, "strictWarningsZero", true, "saved", false));
            }
            FrozenBindings(); Loaded(sw, "final-loaded-documents"); operationChecked = true;
        }
        catch (Exception error)
        {
            errorText = error.ToString(); if (Journal != null) SafeRecord(D("inspection", "read_failure", "phase", "migration-or-verification", "exception", errorText)); else Console.Error.WriteLine(errorText);
        }
        finally
        {
            try { Release(model); } catch (Exception error) { errorText += "\nROOT_RELEASE_FAILED " + error; } model = null;
            if (sw != null && owned)
            {
                try
                {
                    VerifyOwner(sw, process, retainedHandle, pid, ticks, executable); bool closedResult = sw.CloseAllDocuments(true); int count = sw.GetDocumentCount(); closed = closedResult && count == 0;
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
                        CompareFiles(sourceBefore, sourceAfter, false, "all75-source-unchanged"); CompareFiles(packBefore, packAfter, Mode == "edit", Mode == "edit" ? "other74-candidate-unchanged" : "all75-candidate-unchanged");
                        FrozenBindings(); rootAfterSha = Hash(Target); filesChecked = true;
                    }
                }
                catch (Exception error) { errorText += "\nFINAL_FILE_BINDING_FAILED " + error; }
                passed = operationChecked && owned && closed && exited && filesChecked && !LoggingFailed && errorText == "";
                SafeRecord(D("inspection", "completion", "mode", Mode, "candidatePack", Pack, "operationComplete", (Mode == "edit" || Mode == "verify") && passed, "diagnosticCompleted", (Mode == "baseline" || Mode == "diagnostic33") && passed,
                    "modelAccepted", false, "strictColdAcceptancePassed", false, "rootAfterSha256", rootAfterSha,
                    "savedRootOnly", Mode == "edit" && savedRoot, "noSave", Mode != "edit", "all75SourceUnchanged", filesChecked, "other74CandidateUnchanged", filesChecked,
                    "all75CandidateUnchanged", Mode != "edit" && filesChecked, "ownedCadExited", exited, "productionAcceptanceEvaluated", false, "warningCauseProven", false,
                    "knownWarningMigrationInputOnly", Mode == "edit", "strictVerifyRequiresZeroWarnings", true, "noForcedTermination", true, "error", errorText));
                passed = passed && !LoggingFailed; Journal.Dispose(); Journal = null;
            }
            if (process != null) process.Dispose();
        }
        return passed ? 0 : 1;
    }
}
