// Throwaway owner-reference experiment. No model or engineering acceptance is claimed.
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
using SolidWorks.Interop.swconst;
internal static class RightOwnerRebindProbe
{
    private const string OwnedRoot = @"D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_owner_rebind_v2";
    private const string Attempt = @"D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110111643911-CE439B";
    private const string CandidateRoot = OwnedRoot + @"\candidate_attempt";
    private const string Pack = CandidateRoot + @"\native_cad\working_pack";
    private const string CadExecutable = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
    private static readonly List<object> Events = new List<object>();
    private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] Arms = { "repair", "cold-owner", "cold-total", "cold-right" };
    private static readonly HashSet<string> MateFeatureTypes = new HashSet<string>(new[] { "MateCamTangent", "MateCoincident", "MateConcentric", "MateDistanceDim", "MateGearDim", "MateHinge", "MateInPlace", "MateLinearCoupler", "MateLock", "MateParallel", "MatePerpendicular", "MatePlanarAngleDim", "MateProfileCenter", "MateRackPinionDim", "MateScrew", "MateSlot", "MateSymmetric", "MateTangent", "MateUniversalJoint", "MateWidth" }, StringComparer.Ordinal);
    private static FileStream Journal;
    private static DateTime Deadline;
    private static string ManifestPath, ManifestSha, Arm;
    private static Manifest Input;
    private static bool LoggingFailed, OwnerSaved, ReplaceAttempted, RebuildAttempted, SaveAttempted;
    private static int LastOpenWarnings;
    public sealed class Pin { public string path, sha256; public long size; }
    public sealed class Manifest { public string schema, candidateRoot, owner, right, rebound, total, left, parameterSource, activeRight; public Pin[] candidatePins, sourcePins; public double[] rightRows; }
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
    private static void Record(object row) { Events.Add(row); byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Serialize(D("schema", "right-owner-rebind-diagnostic-v1", "events", Events))); Journal.Position = 0; Journal.Write(bytes, 0, bytes.Length); Journal.SetLength(bytes.Length); Journal.Flush(true); }
    private static void SafeRecord(object row) { try { Record(row); } catch (Exception error) { LoggingFailed = true; Console.Error.WriteLine(error); } }
    private static object Read(Func<object> action) { try { return action(); } catch (Exception error) { return D("readError", error.ToString()); } }
    private static object[] Values(object raw) { return raw is Array || raw is IList ? ((IEnumerable)raw).Cast<object>().ToArray() : new object[0]; }
    private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private static bool SameComObject(object a, object b) { if (a == null || b == null) return false; IntPtr pa = Marshal.GetIUnknownForObject(a), pb = IntPtr.Zero; try { pb = Marshal.GetIUnknownForObject(b); return pa == pb; } finally { if (pb != IntPtr.Zero) Marshal.Release(pb); Marshal.Release(pa); } }
    // Hold each distinct IUnknown until the entire traversal ends, so its address cannot be reused.
    private sealed class ComIdentityScope : IDisposable
    {
        private readonly HashSet<long> keys = new HashSet<long>();
        private readonly List<IntPtr> held = new List<IntPtr>();
        internal bool First(object value) { if (value == null) return false; IntPtr pointer = Marshal.GetIUnknownForObject(value); if (!keys.Add(pointer.ToInt64())) { Marshal.Release(pointer); return false; } held.Add(pointer); return true; }
        public void Dispose() { foreach (IntPtr pointer in held) Marshal.Release(pointer); held.Clear(); keys.Clear(); }
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


    private static bool Bound(string path) { try { return Known.ContainsKey(Full(path)); } catch { return false; } }
    private static double[] Numbers(object raw, int minimum, string context)
    {
        Require(raw is Array, "NUMERIC_ARRAY_UNAVAILABLE " + context); double[] row = Values(raw).Select(Convert.ToDouble).ToArray();
        Require(row.Length >= minimum && row.All(n => !double.IsNaN(n) && !double.IsInfinity(n)), "NUMERIC_ARRAY_INVALID " + context); return row;
    }
    private static string Config(ModelDoc2 model) { Configuration c = model.ConfigurationManager.ActiveConfiguration; try { Require(c != null, "ACTIVE_CONFIGURATION_NULL"); return c.Name; } finally { Release(c); } }
    private static object Persistent(ModelDocExtension extension, object value)
    {
        object raw = extension.GetPersistReference3(value); Require(raw is Array && Values(raw).Length > 0, "PERSISTENT_REFERENCE_UNAVAILABLE"); return Convert.ToBase64String(Values(raw).Select(Convert.ToByte).ToArray());
    }
    private static void CheckBindings()
    {
        Require(Hash(ManifestPath) == ManifestSha, "MANIFEST_CHANGED");
        foreach (Pin row in Input.sourcePins) Require(Hash(row.path) == row.sha256 && new FileInfo(row.path).Length == row.size, "ORIGINAL_SOURCE_CHANGED " + row.path);
        foreach (Pin row in Input.candidatePins) if (!(Arm == "repair" && Same(row.path, Input.owner) && OwnerSaved)) Require(Hash(row.path) == row.sha256, "NON_TARGET_CANDIDATE_CHANGED " + row.path);
    }
    private static void LoadBindings(string path, string sha)
    {
        ManifestPath = Full(path); ManifestSha = sha; Require(Inside(Path.Combine(OwnedRoot, "runs"), path) && Hash(path) == sha, "MANIFEST_PATH_OR_HASH_INVALID");
        Input = Json.Deserialize<Manifest>(File.ReadAllText(path, Encoding.UTF8));
        Require(Input.schema == "right-owner-native-manifest-v1" && Same(Input.candidateRoot, CandidateRoot) && Input.candidatePins.Length == 120 && Input.sourcePins.Length >= 120 && Input.rightRows.Length == 9, "MANIFEST_SCOPE_INVALID");
        Require(Same(Input.owner, Path.Combine(Pack, "箱体竖隔板R焊接.SLDASM")) && Same(Input.right, Path.Combine(Pack, "箱体竖隔板R.SLDPRT")) && Same(Input.rebound, Path.Combine(Pack, "right_rebound_native.SLDPRT")) && Same(Input.total, Path.Combine(Pack, "标准寄存柜1917×1000×550(总装配).SLDASM")), "FIXED_TARGET_PATH_CHANGED");
        foreach (Pin row in Input.candidatePins) { Plain(row.path); Require(Inside(CandidateRoot, row.path), "CANDIDATE_OUTSIDE_ISOLATION"); Known.Add(Full(row.path), row.sha256); }
        foreach (string expected in new[] { Input.owner, Input.right, Input.rebound, Input.total, Input.left, Input.parameterSource }) Require(Bound(expected), "REQUIRED_CANDIDATE_PIN_MISSING");
        Require(Directory.GetFiles(CandidateRoot, "*", SearchOption.AllDirectories).Count(p => new[] { ".SLDASM", ".SLDPRT" }.Contains(Path.GetExtension(p).ToUpperInvariant())) == 120, "CANDIDATE_CAD_COUNT_CHANGED"); CheckBindings();
    }
    private static void Documents(ISldWorks sw, string stage)
    {
        var rows = new List<object>();
        foreach (object raw in Values(sw.GetDocuments())) { ModelDoc2 doc = raw as ModelDoc2; Require(doc != null && Bound(doc.GetPathName()), "LOADED_DOCUMENT_OUTSIDE_CANDIDATE"); rows.Add(D("path", doc.GetPathName(), "readOnly", doc.IsOpenedReadOnly(), "saveFlag", doc.GetSaveFlag())); }
        Record(D("inspection", "loaded_documents", "stage", stage, "count", sw.GetDocumentCount(), "rows", rows));
    }
    private static ModelDoc2 Open(ISldWorks sw, string path, bool writable, bool repairEntry)
    {
        CheckTime(); CheckBindings(); Require(sw.GetOpenDocumentByName(path) == null, "TARGET_ALREADY_LOADED");
        int errors = 0, warnings = 0; int type = Path.GetExtension(path).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        ModelDoc2 model = sw.OpenDoc6(path, type, writable ? 1 : 3, "", ref errors, ref warnings) as ModelDoc2;
        Record(D("inspection", "open_result", "arm", Arm, "requestedPath", path, "errors", errors, "warnings", warnings, "idMismatch", (warnings & 1) != 0, "needsRegen", (warnings & 32) != 0, "actualPath", model == null ? null : model.GetPathName(), "actualReadOnly", model == null ? (object)null : model.IsOpenedReadOnly(), "repairEntryAllowsExact33", repairEntry, "productionOpenGateChanged", false));
        Require(model != null && errors == 0 && Same(model.GetPathName(), path) && model.IsOpenedReadOnly() == !writable, "EXACT_OPEN_FAILED");
        LastOpenWarnings = warnings;
        if (repairEntry) Require(warnings == 33 || warnings == 0 || warnings == 32, "REPAIR_ENTRY_UNEXPECTED_WARNING_NO_MUTATION");
        Documents(sw, "after-open-" + Arm); return model;
    }
    private static Dictionary<string, object> MateState(Mate2 mate, ModelDocExtension extension, string owner)
    {
        Require(mate != null, "MATE_INTERFACE_UNKNOWN " + owner); int count = mate.GetMateEntityCount(); Require(count > 0 && count <= 32, "MATE_ENTITY_COUNT_UNKNOWN"); var entities = new List<object>();
        for (int i = 0; i < count; i++)
        {
            MateEntity2 entity = null; Component2 component = null; object reference = null;
            try
            {
                entity = mate.MateEntity(i); Require(entity != null, "MATE_ENTITY_NULL"); component = entity.ReferenceComponent; reference = entity.Reference; Require(reference != null, "MATE_REFERENCE_NULL");
                string path = component == null ? Full(owner) : Full(component.GetPathName()); Require(Bound(path), "MATE_REFERENCE_OUTSIDE_CANDIDATE");
                object parameters = entity.EntityParams;
                entities.Add(D("index", i, "componentName", component == null ? null : component.Name2, "componentPath", path, "configuration", component == null ? "owner-active-configuration-in-snapshot" : component.ReferencedConfiguration, "ownerDocumentReference", component == null, "referenceType", entity.ReferenceType, "persistentReference", Persistent(extension, reference), "parameters", parameters == null ? null : (object)Numbers(parameters, 1, "mate-parameters")));
            }
            finally { Release(reference); Release(component); Release(entity); }
        }
        return D("type", mate.Type, "alignment", mate.Alignment, "flipped", mate.Flipped, "canBeFlipped", mate.CanBeFlipped, "entityCount", count, "entities", entities);
    }
    private static void Features(Feature first, bool sub, int depth, string owner, ModelDocExtension extension, ComIdentityScope identities, List<object> rows)
    {
        Require(depth <= 64 && rows.Count < 20000, "FEATURE_TRAVERSAL_LIMIT");
        for (Feature f = first; f != null; f = (sub ? f.GetNextSubFeature() : f.GetNextFeature()) as Feature)
        {
            CheckTime(); if (!identities.First(f)) break; string type = f.GetTypeName2(); bool warning; int error2 = f.GetErrorCode2(out warning);
            var row = D("name", f.Name, "type", type, "suppressed", f.IsSuppressed(), "error", f.GetErrorCode(), "error2", error2, "warning", warning, "depth", depth);
            bool isMate = MateFeatureTypes.Contains(type); Require(!type.StartsWith("Mate", StringComparison.Ordinal) || isMate || type == "MateGroup" || type == "MateReference" || type == "MateReferenceGroupFolder", "UNKNOWN_MATE_TYPE " + type);
            if (isMate) { Mate2 mate = f.GetSpecificFeature2() as Mate2; try { row["mate"] = MateState(mate, extension, owner); row["persistentReference"] = Persistent(extension, f); } finally { Release(mate); } }
            rows.Add(row); Features(f.GetFirstSubFeature() as Feature, true, depth + 1, owner, extension, identities, rows);
        }
    }
    private static object EdgeShape(Winnsen.StructureAgent.Native888.NativeParametricLocks.GeometrySnapshot g)
    {
        Func<double[], string> key = values => string.Join(",", values.Select(v => Math.Round(v, 6).ToString("F6", CultureInfo.InvariantCulture)));
        return g.edges.Select(e => { string a = key(e.vertex_start_mm), b = key(e.vertex_end_mm); return D("length", e.length_mm, "start", string.CompareOrdinal(a, b) <= 0 ? e.vertex_start_mm : e.vertex_end_mm, "end", string.CompareOrdinal(a, b) <= 0 ? e.vertex_end_mm : e.vertex_start_mm, "parameterStart", e.param_start_mm); }).OrderBy(e => key((double[])e["start"]) + "|" + key((double[])e["end"]) + "|" + Math.Round((double)e["length"], 6).ToString("F6", CultureInfo.InvariantCulture), StringComparer.Ordinal).ToArray();
    }
    private static object Geometry(ModelDoc2 doc, bool right)
    {
        var g = Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchCapture(doc, right ? "R" : "other", Input.rightRows);
        if (right) Require(Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchGeometryGate(g, "R", Input.rightRows) && Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchMirrorGeometryGate(g, Input.rightRows), "ORIGINAL_RIGHT_GEOMETRY_GATE_FAILED");
        var vertices = new List<double[]>(); foreach (Body2 body in (Array)((PartDoc)doc).GetBodies2(0, false)) foreach (Vertex vertex in (Array)body.GetVertices()) vertices.Add((double[])vertex.GetPoint());
        if (right) Require(vertices.Count == 568, "RIGHT_EXPECTED_568_VERTICES_REQUIRED");
        Record(D("inspection", "part_geometry", "path", doc.GetPathName(), "originalGeometry", g, "vertexCount", vertices.Count, "originalRightGatePassed", right, "vertices", vertices));
        return D("errors", g.error_feature_count, "warnings", g.warning_feature_count, "traversalComplete", g.traversal_complete, "bodies", g.body_count, "edgeCount", g.edge_count, "edges", EdgeShape(g), "vertexCount", vertices.Count, "vertices", vertices.OrderBy(v => string.Join(",", v.Select(n => Math.Round(n, 9).ToString("F9", CultureInfo.InvariantCulture))), StringComparer.Ordinal).ToArray());
    }
    private static Dictionary<string, object> Snapshot(ModelDoc2 owner, string rightPath, string stage)
    {
        var components = new List<object>(); var features = new List<object>(); ModelDocExtension ext = owner.Extension;
        object[] raw = Values(((AssemblyDoc)owner).GetComponents(true)); Require(raw.Length > 0 && raw.All(c => c is Component2), "TOP_COMPONENTS_UNKNOWN");
        Require(raw.Cast<Component2>().Count(c => Same(c.GetPathName(), rightPath)) == 1, "UNIQUE_TOP_RIGHT_REQUIRED");
        foreach (Component2 c in raw)
        {
            Require(Bound(c.GetPathName()) && !c.IsSuppressed(), "TOP_COMPONENT_UNBOUND_OR_SUPPRESSED"); ModelDoc2 doc = c.GetModelDoc2() as ModelDoc2; Require(doc != null && Same(doc.GetPathName(), c.GetPathName()), "COMPONENT_MODEL_UNKNOWN");
            MathTransform t = c.Transform2, total = c.GetTotalTransform(false); Require(t != null && total != null, "TRANSFORM_UNKNOWN");
            components.Add(D("name", c.Name2, "path", Full(c.GetPathName()), "configuration", c.ReferencedConfiguration, "activeConfiguration", Config(doc), "transform", Numbers(t.ArrayData, 13, "Transform2"), "totalTransform", Numbers(total.ArrayData, 13, "TotalTransform"), "fixed", c.IsFixed(), "suppression", c.GetSuppression(), "suppression2", c.GetSuppression2(), "geometry", ((IModelDoc2)doc).GetType() == 1 ? Geometry(doc, Same(c.GetPathName(), rightPath)) : (object)null));
        }
        using (var ids = new ComIdentityScope()) Features(owner.FirstFeature() as Feature, false, 0, owner.GetPathName(), ext, ids, features);
        Record(D("inspection", "owner_mate_count", "stage", stage, "count", features.Cast<Dictionary<string, object>>().Count(f => f.ContainsKey("mate")), "zeroMateCountIsObservedNotRejected", true));
        var state = D("owner", Full(owner.GetPathName()), "configuration", Config(owner), "componentCount", raw.Length, "components", components.Cast<Dictionary<string, object>>().OrderBy(c => Same(Convert.ToString(c["path"]), rightPath) ? "0-RIGHT" : "1-" + Convert.ToString(c["path"]) + "/" + Convert.ToString(c["name"]), StringComparer.Ordinal).ToArray(), "features", features);
        Record(D("inspection", "owner_snapshot", "stage", stage, "state", state, "finiteMateBoundaryWitnessComplete", false)); return state;
    }
    private static object Normalize(object value, string oldName, string newName, string currentPath)
    {
        var d = value as IDictionary<string, object>;
        if (d != null)
        {
            string componentPath = d.ContainsKey("componentPath") ? Convert.ToString(d["componentPath"]) : null;
            bool replacedEntity = componentPath != null && Same(componentPath, currentPath); var output = new Dictionary<string, object>();
            foreach (var item in d) output[item.Key] = item.Key == "persistentReference" && replacedEntity ? "EXPLICIT_REPLACED_RIGHT_ENTITY_REFERENCE" : Normalize(item.Value, oldName, newName, currentPath);
            return output;
        }
        if (value is string)
        {
            string text = (string)value;
            if (string.Equals(text, currentPath, StringComparison.OrdinalIgnoreCase)) return "EXPLICIT_RIGHT_COMPONENT_PATH";
            if (text == oldName || text == newName) return "EXPLICIT_RIGHT_COMPONENT_NAME";
            return text;
        }
        if (value is IEnumerable) return ((IEnumerable)value).Cast<object>().Select(v => Normalize(v, oldName, newName, currentPath)).ToArray(); return value;
    }
    private static void AllowHealthImprovement(object before, object after, string context)
    {
        var a = before as IDictionary<string, object>; var b = after as IDictionary<string, object>;
        if (a != null && b != null)
        {
            if (a.ContainsKey("error") && a.ContainsKey("error2") && a.ContainsKey("warning") && b.ContainsKey("error") && b.ContainsKey("error2") && b.ContainsKey("warning"))
            {
                foreach (string key in new[] { "error", "error2" }) { int x = Convert.ToInt32(a[key]), y = Convert.ToInt32(b[key]); Require(y == x || y == 0, "NEW_OR_CHANGED_FEATURE_ERROR " + context + "/" + key); if (x != y) Record(D("inspection", "explicit_health_improvement", "context", context, "field", key, "before", x, "after", y)); a[key] = b[key]; }
                bool xw = Convert.ToBoolean(a["warning"]), yw = Convert.ToBoolean(b["warning"]); Require(xw || !yw, "NEW_FEATURE_WARNING " + context); if (xw != yw) Record(D("inspection", "explicit_health_improvement", "context", context, "field", "warning", "before", xw, "after", yw)); a["warning"] = b["warning"];
            }
            foreach (string key in a.Keys.ToArray()) if (b.ContainsKey(key)) AllowHealthImprovement(a[key], b[key], context + "/" + key);
        }
        else if (!(before is string) && before is IEnumerable && !(after is string) && after is IEnumerable)
        {
            object[] aa = ((IEnumerable)before).Cast<object>().ToArray(), bb = ((IEnumerable)after).Cast<object>().ToArray();
            for (int i = 0; i < Math.Min(aa.Length, bb.Length); i++) AllowHealthImprovement(aa[i], bb[i], context + "[" + i + "]");
        }
    }
    private static void Compare(Dictionary<string, object> before, Dictionary<string, object> after, string oldName, string newName, string stage)
    {
        bool rawEqual = false; string rawFailure = null;
        try { Equal(before, after, "raw-owner"); rawEqual = true; } catch (Exception e) { rawFailure = e.ToString(); }
        Record(D("inspection", "raw_equal", "stage", stage, "passed", rawEqual, "error", rawFailure, "finiteMateBoundaryWitnessComplete", false));
        object a = Normalize(before, oldName, newName, Input.right), b = Normalize(after, oldName, newName, Input.rebound);
        // Raw states remain preserved; this copy permits only explicitly logged feature-health improvement.
        AllowHealthImprovement(a, b, stage);
        Equal(a, b, "explicit-single-component-reference-normalization");
        Record(D("inspection", "research_state_comparison", "stage", stage, "passed", true, "allowedPathFrom", Input.right, "allowedPathTo", Input.rebound, "allowedNameFrom", oldName, "allowedNameTo", newName, "allowedPersistentReferenceDifferenceScope", "mate entities whose ReferenceComponent is the replaced right instance only", "finiteMateBoundaryWitnessComplete", false, "engineeringAccepted", false));
    }
    private static Component2 OneRight(ModelDoc2 owner, string path) { Component2[] rows = Values(((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().Where(c => Same(c.GetPathName(), path)).ToArray(); Require(rows.Length == 1, "EXACT_ONE_TOP_RIGHT_REQUIRED"); return rows[0]; }
    private static void Repair(ISldWorks sw)
    {
        ModelDoc2 owner = Open(sw, Input.owner, true, true);
        if (LastOpenWarnings != 33) { Record(D("inspection", "no_rebind_needed", "warnings", LastOpenWarnings, "idMismatch", false, "rebindPerformed", false, "activeRight", Input.right, "noSave", true, "noRebuild", true, "modelAccepted", false)); return; }
        int activateError = 0; ModelDoc2 active = sw.ActivateDoc3(owner.GetTitle(), false, 0, ref activateError) as ModelDoc2;
        Require(active != null && activateError == 0 && Same(active.GetPathName(), Input.owner), "OWNER_ACTIVATION_FAILED");
        Component2 old = OneRight(owner, Input.right); string oldName = old.Name2, config = old.ReferencedConfiguration; Require(!string.IsNullOrEmpty(config), "ORIGINAL_CONFIGURATION_EMPTY");
        var before = Snapshot(owner, Input.right, "before-replace"); CheckBindings(); owner.ClearSelection2(true); Require(old.Select4(false, null, false), "EXACT_RIGHT_SELECTION_FAILED");
        SelectionMgr selection = owner.SelectionManager as SelectionMgr; Require(selection != null && selection.GetSelectedObjectCount2(-1) == 1 && SameComObject(selection.GetSelectedObject6(1, -1), old), "SELECTION_NOT_UNIQUE_EXACT_RIGHT");
        Record(D("inspection", "replace_request", "owner", Input.owner, "selectedInstance", oldName, "newPath", Input.rebound, "configuration", config, "replaceAllInstance", false, "configurationOption", 0, "configurationOptionName", "swReplaceComponentsConfiguration_MatchName", "reAttachMates", true, "reAttachMatesIsAcceptanceEvidence", false));
        ReplaceAttempted = true; bool replaced = ((AssemblyDoc)owner).ReplaceComponents2(Input.rebound, config, false, 0, true); Record(D("inspection", "replace_result", "returnValue", replaced)); Require(replaced, "REPLACE_COMPONENTS2_FALSE");
        Component2 rebound = OneRight(owner, Input.rebound); string newName = rebound.Name2; Require(rebound.ReferencedConfiguration == config && Values(((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().All(c => !Same(c.GetPathName(), Input.right)), "REPLACEMENT_PATH_OR_CONFIGURATION_INVALID");
        var after = Snapshot(owner, Input.rebound, "after-replace-before-rebuild"); Compare(before, after, oldName, newName, "before-rebuild"); CheckBindings();
        // Original strict Save gate, applied only to the isolated right owner.
        RebuildAttempted = true; bool rebuilt = owner.ForceRebuild3(false); Record(D("inspection", "owner_rebuild", "returnValue", rebuilt, "path", owner.GetPathName())); Require(rebuilt && !owner.IsOpenedReadOnly() && Same(owner.GetPathName(), Input.owner), "STRICT_OWNER_REBUILD_FAILED");
        Compare(before, Snapshot(owner, Input.rebound, "after-rebuild"), oldName, newName, "after-rebuild"); CheckBindings();
        SaveAttempted = true; int errors = 0, warnings = 0; bool saved = owner.Save3(1, ref errors, ref warnings); OwnerSaved = true;
        Record(D("inspection", "owner_save", "saved", saved, "errors", errors, "warnings", warnings, "path", owner.GetPathName(), "saveFlag", owner.GetSaveFlag(), "experimentalSaveOnly", true));
        Require(saved && errors == 0 && warnings == 0 && !owner.GetSaveFlag(), "STRICT_OWNER_SAVE_FAILED");
        Compare(before, Snapshot(owner, Input.rebound, "after-save"), oldName, newName, "after-save"); CheckBindings();
        Record(D("inspection", "owner_saved_sha256", "path", Input.owner, "sha256", Hash(Input.owner), "finiteMateBoundaryWitnessComplete", false, "modelAccepted", false, "engineeringAccepted", false)); Documents(sw, "after-owner-save");
    }
    private static void Cold(ISldWorks sw)
    {
        string activeRight = Input.activeRight; Require(Same(activeRight, Input.right) || Same(activeRight, Input.rebound), "COLD_ACTIVE_RIGHT_NOT_EXPLICITLY_BOUND");
        string path = Arm == "cold-owner" ? Input.owner : Arm == "cold-total" ? Input.total : activeRight;
        ModelDoc2 model = Open(sw, path, false, false);
        if (Arm == "cold-owner") Snapshot(model, activeRight, Arm);
        if (Arm == "cold-right")
        {
            Geometry(model, true); ModelDoc2 source = sw.GetOpenDocumentByName(Input.parameterSource) as ModelDoc2;
            if (source == null) source = Open(sw, Input.parameterSource, false, false);
            Require(source != null && Same(source.GetPathName(), Input.parameterSource), "REFLECTION_SOURCE_MISSING");
            Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.VerifyReflectedVertices(source, model);
            Record(D("inspection", "original_reflected_vertices", "source", Input.parameterSource, "right", activeRight, "vertexCount", 568, "passed", true));
        }
        CheckBindings(); Documents(sw, "after-cold");
    }
    private static void VerifyOwner(ISldWorks sw, Process process, IntPtr handle, int pid, long ticks, string executable)
    {
        Require(process != null && handle != IntPtr.Zero && process.Handle == handle && !process.HasExited, "OWNED_PROCESS_HANDLE_NOT_LIVE");
        Require(process.Id == pid && process.StartTime.ToUniversalTime().Ticks == ticks && Same(process.MainModule.FileName, executable) && sw.GetProcessID() == pid, "OWNED_PROCESS_IDENTITY_CHANGED");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4 || !Arms.Contains(args[0])) { Console.Error.WriteLine("Usage: RightOwnerRebindProbe.exe <repair|cold-owner|cold-total|cold-right> <newOutput> <source-binding.json> <bindingSha>"); return 2; }
        ISldWorks sw = null; Process process = null; IntPtr handle = IntPtr.Zero; int pid = 0; long ticks = 0; string executable = null, errorText = "";
        bool owned = false, exited = false, exitRequested = false, collected = false, filesChecked = false, closed = false;
        try
        {
            string output = Full(args[1]); Require(Inside(Path.Combine(OwnedRoot, "runs"), output) && !File.Exists(output) && Directory.Exists(Path.GetDirectoryName(output)), "OUTPUT_NOT_NEW_OWNED_RUN"); Plain(output);
            Journal = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read); Deadline = DateTime.UtcNow.AddSeconds(600); Arm = args[0];
            Record(D("inspection", "request", "arm", args[0], "options", 3, "partType", 1, "maximumDiagnosticSeconds", 600, "modelAccepted", false, "operationComplete", false));
            LoadBindings(args[2], args[3]); Require(Pids("SLDWORKS").Length == 0 && Pids("sldProcMon").Length == 0, "EMPTY_CAD_BASELINE_REQUIRED");
            long activation = DateTime.UtcNow.Ticks; sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks; Require(sw != null, "SW2020_ACTIVATION_FAILED");
            pid = sw.GetProcessID(); process = Process.GetProcessById(pid); handle = process.Handle; ticks = process.StartTime.ToUniversalTime().Ticks; executable = process.MainModule.FileName;
            string canonical = CanonicalExecutable(executable), revision = sw.RevisionNumber(); int count = sw.GetDocumentCount();
            owned = handle != IntPtr.Zero && !process.HasExited && ticks >= activation && Same(canonical, CanonicalExecutable(CadExecutable)) && process.MainModule.FileVersionInfo.FileMajorPart == 28 && revision.StartsWith("28.", StringComparison.Ordinal) && Pids("SLDWORKS").SequenceEqual(new[] { pid }) && count == 0;
            Record(D("inspection", "session_owner", "arm", args[0], "cadPid", pid, "startTimeUtcTicks", ticks.ToString(), "startTicks", ticks.ToString(), "executable", executable, "canonicalExecutable", canonical, "revision", revision, "initialDocumentCount", count, "exactOwnershipVerified", owned, "processHandleRetained", handle != IntPtr.Zero));
            Require(owned, "EXCLUSIVE_OWNED_EMPTY_SW2020_REQUIRED"); VerifyOwner(sw, process, handle, pid, ticks, executable);
            Require(sw.SetCurrentWorkingDirectory(Pack), "SESSION_WORKING_DIRECTORY_FAILED");
            if (Arm == "repair") Repair(sw); else Cold(sw);
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
                SafeRecord(D("inspection", "completion", "arm", args[0], "diagnosticCompleted", complete, "operationComplete", false, "modelAccepted", false, "strictColdAcceptancePassed", false, "ownedCadExited", exited, "allInputsUnchanged", filesChecked, "noSave", !SaveAttempted, "noRebuild", !RebuildAttempted, "noReplace", !ReplaceAttempted, "finiteMateBoundaryWitnessComplete", false, "engineeringAccepted", false, "unknownPathsAccepted", false, "warningCauseProven", false, "error", errorText));
                Journal.Dispose(); Journal = null; collected = complete && !LoggingFailed;
            }
            if (process != null) process.Dispose();
        }
        return collected ? 0 : 1;
    }
}
