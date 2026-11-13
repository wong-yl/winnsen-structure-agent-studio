using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwView = SolidWorks.Interop.sldworks.View;

// Independent trial output only. It never saves a part; reloads close only the verified owned output part.
internal static class TrialOutput
{
    const string Root = @"D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112";
    const double Tol = 0.0000001;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static void Need(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    static object[] Items(object value) { return value == null ? new object[0] : ((IEnumerable)value).Cast<object>().ToArray(); }
    static double[] Numbers(object value) { return Items(value).Select(v => Convert.ToDouble(v, Invariant)).ToArray(); }
    static string Hash(string path)
    {
        // Read-only integrity checks also run during cancellation cleanup.
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    static string Local(string path)
    {
        string full = Path.GetFullPath(path);
        Need(full.StartsWith(Root + "\\", StringComparison.OrdinalIgnoreCase), "OUTPUT_OUTSIDE_TRIAL " + full);
        for (string item = full; !String.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
            if (File.Exists(item) || Directory.Exists(item)) Need((File.GetAttributes(item) & FileAttributes.ReparsePoint) == 0, "OUTPUT_REPARSE_PATH " + item);
        return full;
    }
    static string NewPath(string path)
    {
        string full = Local(path);
        Need(!File.Exists(full) && !Directory.Exists(full), "PRESERVE_EXISTING_OUTPUT " + full);
        return full;
    }
    static string InputPath(ModelDoc2 model)
    {
        Need(model != null && model is PartDoc, "SAVED_PART_INPUT_REQUIRED");
        string path = Local(model.GetPathName());
        Need(File.Exists(path) && !model.GetSaveFlag(), "SAVE_TRIAL_PART_BEFORE_OUTPUT " + path);
        return path;
    }
    static string OutputPath(ModelDoc2 model, string path)
    {
        string full = NewPath(path), outputRoot = Path.GetFullPath(VariableTrial.OutputRoot).TrimEnd('\\');
        Need(full.StartsWith(outputRoot + "\\", StringComparison.OrdinalIgnoreCase), "OUTPUT_MUST_BE_IN_OWNED_CAD_OUTPUTS " + full);
        return full;
    }
    static string Configuration(ModelDoc2 model)
    {
        ConfigurationManager manager = model.ConfigurationManager;
        Configuration active = manager.ActiveConfiguration;
        try { return active.Name; } finally { Release(active); Release(manager); }
    }
    static void ActivateConfiguration(ModelDoc2 model, string name, string error, bool newWork = true)
    {
        if (newWork) VariableTrial.RuntimeOwnership();
        if (Configuration(model) != name) Need(model.ShowConfiguration2(name), error);
        Need(Configuration(model) == name, error + "_READBACK");
    }
    static void SameInput(ModelDoc2 model, string path)
    {
        Need(model != null && String.Equals(Local(model.GetPathName()), path, StringComparison.OrdinalIgnoreCase), "CLEANUP_INPUT_IDENTITY_CHANGED");
    }
    sealed class SessionIdentity
    {
        internal int Pid;
        internal long StartTicks;
        internal string Executable;
    }
    static SessionIdentity CaptureSession(ISldWorks sw)
    {
        VariableTrial.RuntimeOwnership(); int pid = sw.GetProcessID();
        using (Process process = Process.GetProcessById(pid))
            return new SessionIdentity { Pid = pid, StartTicks = process.StartTime.ToUniversalTime().Ticks, Executable = Path.GetFullPath(process.MainModule.FileName) };
    }
    static void SameSession(ISldWorks sw, SessionIdentity identity)
    {
        // A cancelled job may restore its own live session, without starting new work.
        Need(sw.GetProcessID() == identity.Pid, "CLEANUP_SESSION_PID_CHANGED");
        using (Process process = Process.GetProcessById(identity.Pid))
            Need(process.StartTime.ToUniversalTime().Ticks == identity.StartTicks && String.Equals(Path.GetFullPath(process.MainModule.FileName), identity.Executable, StringComparison.OrdinalIgnoreCase), "CLEANUP_SESSION_IDENTITY_CHANGED");
    }
    sealed class DxfManifest
    {
        internal string Path, Sha256, SourceSha256, NativeExportMethod;
        internal double[] Alignment;
        internal bool ApiAlignmentSpecified;
        internal NativeCutSnapshot CutGeometry;
    }
    static readonly Dictionary<string, DxfManifest> Exports = new Dictionary<string, DxfManifest>(StringComparer.OrdinalIgnoreCase);

    sealed class Pair
    {
        internal int Code;
        internal string Value;
        internal Pair(int code, string value) { Code = code; Value = value; }
    }
    sealed class DxfEntity
    {
        internal int Start, End;
        internal string Type, Key;
    }
    static List<Pair> ReadDxf(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Need(!Encoding.ASCII.GetString(bytes.Take(22).ToArray()).StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal), "BINARY_DXF_NOT_SUPPORTED " + path);
        string[] lines = Encoding.Default.GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int length = lines.Length;
        while (length > 0 && String.IsNullOrWhiteSpace(lines[length - 1])) length--;
        Need(length % 2 == 0, "MALFORMED_ASCII_DXF_PAIR_COUNT " + path);
        var pairs = new List<Pair>();
        for (int i = 0; i < length; i += 2)
        {
            if ((i & 1023) == 0) VariableTrial.RuntimeOwnership();
            int code;
            Need(Int32.TryParse(lines[i].Trim(), NumberStyles.Integer, Invariant, out code), "MALFORMED_DXF_CODE " + i);
            pairs.Add(new Pair(code, lines[i + 1]));
        }
        Need(pairs.Count > 0 && pairs[pairs.Count - 1].Code == 0 && pairs[pairs.Count - 1].Value.Trim() == "EOF", "DXF_EOF_REQUIRED");
        return pairs;
    }
    static string Text(List<Pair> pairs, DxfEntity entity, int code, string fallback)
    {
        for (int i = entity.Start + 1; i < entity.End; i++) if (pairs[i].Code == code) return pairs[i].Value.Trim();
        return fallback;
    }
    static string Numeric(string value)
    {
        double number;
        Need(Double.TryParse(value, NumberStyles.Float, Invariant, out number) && !Double.IsNaN(number) && !Double.IsInfinity(number), "DXF_NUMBER_INVALID " + value);
        return Math.Round(number, 6, MidpointRounding.AwayFromZero).ToString("0.######", Invariant);
    }
    static string GeometryKey(List<Pair> pairs, DxfEntity entity)
    {
        if (entity.Type == "LINE")
        {
            string a = String.Join(",", new[] { 10, 20, 30 }.Select(code => Numeric(Text(pairs, entity, code, "0"))));
            string b = String.Join(",", new[] { 11, 21, 31 }.Select(code => Numeric(Text(pairs, entity, code, "0"))));
            return "LINE|" + (String.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a);
        }
        var codes = new HashSet<int>(new[] { 0, 10, 20, 30, 11, 21, 31, 40, 41, 42, 50, 51, 70, 90, 210, 220, 230 });
        return entity.Type + "|" + String.Join("|", pairs.Skip(entity.Start + 1).Take(entity.End - entity.Start - 1)
            .Where(p => codes.Contains(p.Code)).Select(p => p.Code + ":" + (p.Code == 0 ? p.Value.Trim() : Numeric(p.Value))));
    }
    static List<DxfEntity> Entities(List<Pair> pairs)
    {
        var rows = new List<DxfEntity>();
        bool inside = false;
        for (int i = 0; i < pairs.Count; i++)
        {
            if ((i & 511) == 0) VariableTrial.RuntimeOwnership();
            if (pairs[i].Code == 0 && pairs[i].Value.Trim() == "SECTION" && i + 1 < pairs.Count)
            { inside = pairs[i + 1].Code == 2 && pairs[i + 1].Value.Trim() == "ENTITIES"; continue; }
            if (pairs[i].Code == 0 && pairs[i].Value.Trim() == "ENDSEC") inside = false;
            if (!inside || pairs[i].Code != 0) continue;
            string type = pairs[i].Value.Trim();
            Need(new[] { "LINE", "ARC", "CIRCLE", "LWPOLYLINE", "POLYLINE" }.Contains(type), "UNSUPPORTED_DXF_ENTITY " + type);
            int end = i + 1;
            while (end < pairs.Count && pairs[end].Code != 0) end++;
            if (type == "POLYLINE")
            {
                while (end < pairs.Count && pairs[end].Code == 0 && pairs[end].Value.Trim() == "VERTEX")
                { end++; while (end < pairs.Count && pairs[end].Code != 0) end++; }
                Need(end < pairs.Count && pairs[end].Value.Trim() == "SEQEND", "DXF_POLYLINE_SEQEND_REQUIRED");
                end++; while (end < pairs.Count && pairs[end].Code != 0) end++;
            }
            var row = new DxfEntity { Start = i, End = end, Type = type };
            row.Key = GeometryKey(pairs, row); rows.Add(row); i = end - 1;
        }
        Need(rows.Count > 0, "DXF_HAS_NO_SUPPORTED_GEOMETRY");
        return rows;
    }
    static int Units(List<Pair> pairs)
    {
        for (int i = 0; i + 1 < pairs.Count; i++)
            if (pairs[i].Code == 9 && pairs[i].Value.Trim() == "$INSUNITS") return Int32.Parse(pairs[i + 1].Value.Trim(), Invariant);
        return -1;
    }
    static void AddLayers(List<Pair> pairs)
    {
        int table = -1, end = -1;
        for (int i = 0; i + 1 < pairs.Count; i++)
            if (pairs[i].Code == 0 && pairs[i].Value.Trim() == "TABLE" && pairs[i + 1].Code == 2 && pairs[i + 1].Value.Trim() == "LAYER") { table = i; break; }
        Need(table >= 0, "NATIVE_DXF_LAYER_TABLE_REQUIRED");
        for (int i = table + 1; i < pairs.Count; i++) if (pairs[i].Code == 0 && pairs[i].Value.Trim() == "ENDTAB") { end = i; break; }
        Need(end > table, "DXF_LAYER_TABLE_NOT_TERMINATED");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prototype = new List<Pair>();
        int layerCount = 0;
        for (int i = table + 1; i < end; i++) if (pairs[i].Code == 0 && pairs[i].Value.Trim() == "LAYER")
        {
            int stop = i + 1; while (stop < end && pairs[stop].Code != 0) stop++;
            string name = pairs.Skip(i + 1).Take(stop - i - 1).Where(p => p.Code == 2).Select(p => p.Value.Trim()).FirstOrDefault();
            if (name != null) names.Add(name); layerCount++;
            if (prototype.Count == 0 || name == "0") prototype = pairs.Skip(i).Take(stop - i).Select(p => new Pair(p.Code, p.Value)).ToList();
            i = stop - 1;
        }
        Need(prototype.Count > 0, "DXF_LAYER_PROTOTYPE_REQUIRED");
        long next = 1;
        foreach (Pair pair in pairs.Where(p => p.Code == 5)) { long value; if (Int64.TryParse(pair.Value.Trim(), NumberStyles.HexNumber, Invariant, out value)) next = Math.Max(next, value + 1); }
        var added = new List<Pair>();
        foreach (string name in new[] { "CUT", "BEND", "MARK" })
        {
            if (names.Contains(name)) continue;
            var copy = prototype.Select(p => new Pair(p.Code, p.Value)).ToList();
            foreach (Pair p in copy)
            {
                if (p.Code == 2) p.Value = name;
                else if (p.Code == 5) p.Value = (next++).ToString("X", Invariant);
                else if (p.Code == 62) p.Value = name == "BEND" ? "3" : name == "MARK" ? "5" : "7";
                else if (p.Code == 6) p.Value = "CONTINUOUS";
                else if (p.Code == 70) p.Value = "0";
            }
            added.AddRange(copy); layerCount++;
        }
        for (int i = table + 2; i < end && pairs[i].Code != 0; i++) if (pairs[i].Code == 70) pairs[i].Value = layerCount.ToString(Invariant);
        pairs.InsertRange(end, added);
        for (int i = 0; i + 1 < pairs.Count; i++) if (pairs[i].Code == 9 && pairs[i].Value.Trim() == "$HANDSEED") pairs[i + 1].Value = next.ToString("X", Invariant);
    }

    sealed class DxfTrace
    {
        readonly string BasePath;
        int Sequence;
        internal readonly List<string> Files = new List<string>(), Errors = new List<string>();
        internal DxfTrace(string basePath)
        {
            BasePath = NewPath(basePath);
            string directory = Path.GetDirectoryName(BasePath), prefix = Path.GetFileName(BasePath) + ".trace-";
            if (!Directory.Exists(directory)) return;
            foreach (string path in Directory.GetFiles(directory, prefix + "*.json"))
            {
                string name = Path.GetFileName(path);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                string suffix = name.Substring(prefix.Length, name.Length - prefix.Length - 5); int number;
                if (Int32.TryParse(suffix, NumberStyles.None, Invariant, out number) && number > 0 && suffix == number.ToString("D3", Invariant)) Sequence = Math.Max(Sequence, number);
            }
        }
        internal void Record(string stage, object details)
        {
            string path = BasePath + ".trace-" + (++Sequence).ToString("D3", Invariant) + ".json";
            try
            {
                NewPath(path);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 }.Serialize(new { inspection = "trial_native_flat_output", atUtc = DateTime.UtcNow.ToString("o"), stage = stage, details = details }));
                Files.Add(path);
            }
            catch (Exception error) { Errors.Add(path + ": " + error); }
        }
    }
    sealed class FlatFeatureState
    {
        public int id, error;
        public int? treeParentId;
        public string name, type;
        public bool suppressed, warning;
        public bool[] configurationSuppression;
        public int[] dependencyParentIds;
    }
    sealed class PreparedFlat
    {
        internal string Configuration;
        internal string[] Configurations;
        internal List<FlatFeatureState> Features;
        internal double Width, Height, Thickness;
    }
    static readonly Dictionary<string, PreparedFlat> Prepared = new Dictionary<string, PreparedFlat>(StringComparer.OrdinalIgnoreCase);
    sealed class NativeCutSnapshot
    {
        public string schema = "native-top-z-cut-v1", modelPath, configuration, modelHand, units = "mm";
        public double topZMm, topToleranceMm = 0.0001;
        public int bodyEdgeCount, boundaryEdgeCount, skippedNonTopEdges, skippedInternalSeams;
        public bool complete;
        public List<Dictionary<string, object>> primitives = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> arcSampling = new List<Dictionary<string, object>>();
        public List<string> errors = new List<string>();
    }
    static readonly Dictionary<string, NativeCutSnapshot> NativeCuts = new Dictionary<string, NativeCutSnapshot>(StringComparer.OrdinalIgnoreCase);
    static string NativeHand(ModelDoc2 model)
    {
        CustomPropertyManager manager = model.Extension.CustomPropertyManager[""];
        try
        {
            string raw, resolved; bool wasResolved, linked;
            manager.Get6("HAND", false, out raw, out resolved, out wasResolved, out linked);
            string value = (String.IsNullOrWhiteSpace(raw) ? resolved : raw) ?? "";
            return value == "L" || value == "R" ? value : null;
        }
        finally { Release(manager); }
    }
    static double[] NativeEdgeSample(Edge edge, double parameter, List<object> samples)
    {
        double[] values = Numbers(edge.Evaluate2(parameter, 0));
        Need(values.Length == 4, "CUT_EDGE_EVALUATION_POINT_AND_PACKED_STATUS_REQUIRED");
        long packed = BitConverter.DoubleToInt64Bits(values[3]); int status = unchecked((int)(packed & 0xffffffffL));
        double[] point = values.Take(3).Select(v => v * 1000).ToArray();
        samples.Add(new { parameter = parameter, pointMm = point, status = status, packedStatusBits = packed.ToString("X16", Invariant) });
        Need(status == 1 && point.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)), "CUT_NATIVE_EDGE_EVALUATION_FAILED");
        return point;
    }
    static bool SameNativePoint(double[] a, double[] b, double toleranceMm)
    { return a.Length == 3 && b.Length == 3 && Math.Sqrt(Enumerable.Range(0, 3).Sum(i => (a[i] - b[i]) * (a[i] - b[i]))) <= toleranceMm; }
    static NativeCutSnapshot NativeCutGeometry(ModelDoc2 model, Body2 body, double topZMm)
    {
        var snapshot = new NativeCutSnapshot { modelPath = Local(model.GetPathName()), configuration = Configuration(model), modelHand = NativeHand(model), topZMm = topZMm };
        object[] edges = Items(body.GetEdges()); snapshot.bodyEdgeCount = edges.Length;
        try
        {
            foreach (Edge edge in edges)
            {
                VariableTrial.RuntimeOwnership(); Curve curve = null; CurveParamData parameters = null;
                try
                {
                    object[] adjacent = Items(edge.GetTwoAdjacentFaces2()); int topFaces = 0;
                    try
                    {
                        Need(adjacent.Length == 2 && adjacent.All(v => v is Face2), "CUT_SOLID_EDGE_ADJACENCY_REQUIRED");
                        foreach (Face2 face in adjacent)
                        {
                            Surface surface = face.GetSurface() as Surface;
                            try
                            {
                                if (surface == null || !surface.IsPlane()) continue;
                                double[] plane = Numbers(surface.PlaneParams); Need(plane.Length == 6, "CUT_FACE_PLANE_PARAMETERS_REQUIRED");
                                double norm = Math.Sqrt(plane.Take(3).Sum(v => v * v));
                                if (norm > 0 && Math.Abs(Math.Abs(plane[2] / norm) - 1) < 0.000001 && Math.Abs(plane[5] * 1000 - topZMm) <= snapshot.topToleranceMm) topFaces++;
                            }
                            finally { Release(surface); }
                        }
                    }
                    finally { foreach (object face in adjacent) Release(face); }
                    if (topFaces == 0) { snapshot.skippedNonTopEdges++; continue; }
                    if (topFaces == 2) { snapshot.skippedInternalSeams++; continue; }
                    snapshot.boundaryEdgeCount++;
                    // GetCurve is required before GetCurveParams3 by the native API.
                    curve = edge.GetCurve() as Curve; Need(curve != null, "CUT_NATIVE_CURVE_REQUIRED"); parameters = edge.GetCurveParams3(); Need(parameters != null, "CUT_NATIVE_CURVE_RANGE_REQUIRED");
                    double[] a = Numbers(parameters.StartPoint).Select(v => v * 1000).ToArray(), b = Numbers(parameters.EndPoint).Select(v => v * 1000).ToArray();
                    Need(a.Length == 3 && b.Length == 3 && a.Concat(b).All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && Math.Abs(a[2] - topZMm) <= snapshot.topToleranceMm && Math.Abs(b[2] - topZMm) <= snapshot.topToleranceMm, "CUT_NATIVE_EDGE_OFF_TOP_Z");
                    double u0 = parameters.UMinValue, u1 = parameters.UMaxValue, length = curve.GetLength3(u0, u1) * 1000;
                    Need(!Double.IsNaN(length) && !Double.IsInfinity(length) && length > 0, "CUT_NATIVE_EDGE_LENGTH_REQUIRED");
                    var row = new Dictionary<string, object> { { "id", "C" + (snapshot.primitives.Count + 1) }, { "startMm", a }, { "endMm", b }, { "lengthMm", length },
                        { "parameterMin", u0 }, { "parameterMax", u1 }, { "curveAndEdgeSameSense", parameters.Sense }, { "curveType", parameters.CurveType } };
                    if (curve.IsLine()) row["type"] = "LINE";
                    else if (curve.IsCircle())
                    {
                        double[] circle = Numbers(curve.CircleParams); Need(circle.Length == 7 && circle[6] > 0, "CUT_NATIVE_CIRCLE_PARAMETERS_REQUIRED");
                        Need(Math.Abs(circle[2] * 1000 - topZMm) <= snapshot.topToleranceMm && Math.Abs(circle[3]) < Tol && Math.Abs(circle[4]) < Tol && Math.Abs(Math.Abs(circle[5]) - 1) < Tol, "CUT_NATIVE_CIRCLE_OFF_TOP_XY_PLANE");
                        bool full = Math.Abs(Math.Abs(u1 - u0) - 2 * Math.PI) < 0.000001;
                        row["type"] = full ? "CIRCLE" : "ARC"; row["centerMm"] = circle.Take(3).Select(v => v * 1000).ToArray(); row["radiusMm"] = circle[6] * 1000;
                        if (!full)
                        {
                            var samples = new List<object>(); var sampling = new Dictionary<string, object> { { "id", row["id"] }, { "evaluationApi", "IEdge.Evaluate2" },
                                { "parameterSource", "IEdge.GetCurveParams3" }, { "edgeParameterRange", new[] { u0, u1 } }, { "samples", samples }, { "endpointsVerified", false } };
                            snapshot.arcSampling.Add(sampling);
                            double curveU0, curveU1; bool closed, periodic;
                            bool curveRangeRead = curve.GetEndParams(out curveU0, out curveU1, out closed, out periodic);
                            Need(curveRangeRead && !Double.IsNaN(curveU0) && !Double.IsInfinity(curveU0) && !Double.IsNaN(curveU1) && !Double.IsInfinity(curveU1), "CUT_CURVE_PARAMETER_DOMAIN_REQUIRED");
                            sampling["curveParameterDomain"] = new { source = "ICurve.GetEndParams", minimum = curveU0, maximum = curveU1, closed = closed, periodic = periodic };
                            // GetCurveParams3 describes the edge domain; use IEdge.Evaluate2, including its packed status.
                            double[] evaluatedA = NativeEdgeSample(edge, u0, samples), middle = NativeEdgeSample(edge, (u0 + u1) / 2, samples), evaluatedB = NativeEdgeSample(edge, u1, samples);
                            Need(SameNativePoint(evaluatedA, a, snapshot.topToleranceMm) && SameNativePoint(evaluatedB, b, snapshot.topToleranceMm)
                                || SameNativePoint(evaluatedA, b, snapshot.topToleranceMm) && SameNativePoint(evaluatedB, a, snapshot.topToleranceMm), "CUT_EDGE_PARAMETER_ENDPOINT_READBACK_FAILED");
                            Need(Math.Abs(middle[2] - topZMm) <= snapshot.topToleranceMm, "CUT_NATIVE_ARC_MIDPOINT_OFF_TOP_Z");
                            sampling["endpointsVerified"] = true; row["midpointMm"] = middle; row["midpointEvaluationApi"] = "IEdge.Evaluate2 with GetCurveParams3 domain and status=1";
                        }
                    }
                    else throw new InvalidOperationException("CUT_NATIVE_CURVE_TYPE_UNSUPPORTED " + parameters.CurveType);
                    snapshot.primitives.Add(row);
                }
                catch (Exception error) { snapshot.errors.Add(error.ToString()); }
                finally { Release(parameters); Release(curve); }
            }
        }
        finally { foreach (object edge in edges) Release(edge); }
        snapshot.complete = snapshot.errors.Count == 0 && snapshot.primitives.Count > 0 && snapshot.primitives.Count == snapshot.boundaryEdgeCount;
        NativeCuts[snapshot.modelPath] = snapshot; return snapshot;
    }
    static void ScanFlatFeatures(Feature feature, bool top, int depth, int? parent, string[] configurations, HashSet<int> seen, List<FlatFeatureState> rows)
    {
        int count = 0;
        while (feature != null)
        {
            Feature next = null;
            try
            {
                VariableTrial.RuntimeOwnership(); Need(++count < 10000 && depth <= 16, "FLAT_FEATURE_TRAVERSAL_LIMIT");
                next = top ? feature.GetNextFeature() as Feature : feature.GetNextSubFeature() as Feature;
                int id = feature.GetID();
                if (seen.Add(id))
                {
                    object[] states = Items(feature.IsSuppressed2((int)swInConfigurationOpts_e.swSpecifyConfiguration, configurations));
                    Need(states.Length == configurations.Length && states.All(v => v is bool), "FLAT_FEATURE_CONFIGURATION_STATE_REQUIRED " + id);
                    object[] parents = Items(feature.GetParents()); int[] parentIds;
                    try { parentIds = parents.Cast<Feature>().Select(v => v.GetID()).OrderBy(v => v).ToArray(); }
                    finally { foreach (object value in parents) Release(value); }
                    bool warning; int error = feature.GetErrorCode2(out warning);
                    rows.Add(new FlatFeatureState { id = id, name = feature.Name, type = feature.GetTypeName2(), treeParentId = parent,
                        suppressed = feature.IsSuppressed(), error = error, warning = warning, configurationSuppression = states.Cast<bool>().ToArray(), dependencyParentIds = parentIds });
                    ScanFlatFeatures(feature.GetFirstSubFeature() as Feature, false, depth + 1, id, configurations, seen, rows);
                }
            }
            catch { Release(next); throw; }
            finally { Release(feature); }
            feature = next;
        }
    }
    static List<FlatFeatureState> FlatFeatures(ModelDoc2 model, string[] configurations)
    {
        Need(configurations.SequenceEqual(Items(model.GetConfigurationNames()).Select(Convert.ToString)), "FLAT_CONFIGURATION_LIST_CHANGED");
        var rows = new List<FlatFeatureState>(); ScanFlatFeatures(model.FirstFeature() as Feature, true, 0, null, configurations, new HashSet<int>(), rows);
        Need(rows.Count > 0, "FLAT_FEATURE_SNAPSHOT_EMPTY"); return rows;
    }
    static Feature FlatFeatureById(Feature feature, bool top, int id, HashSet<int> seen, int depth)
    {
        int count = 0;
        while (feature != null)
        {
            Feature next = null;
            try
            {
                Need(++count < 10000 && depth <= 16, "FLAT_FEATURE_LOOKUP_LIMIT");
                next = top ? feature.GetNextFeature() as Feature : feature.GetNextSubFeature() as Feature;
                int actual = feature.GetID();
                if (actual == id) { Release(next); next = null; Feature result = feature; feature = null; return result; }
                if (seen.Add(actual))
                {
                    Feature child = FlatFeatureById(feature.GetFirstSubFeature() as Feature, false, id, seen, depth + 1);
                    if (child != null) { Release(next); next = null; return child; }
                }
            }
            catch { Release(next); throw; }
            finally { Release(feature); }
            feature = next;
        }
        return null;
    }
    static void CompareFlatStates(List<FlatFeatureState> before, List<FlatFeatureState> after, string[] configurations, string allowedConfiguration, HashSet<int> allowedIds)
    {
        Need(before.Select(r => r.id).OrderBy(v => v).SequenceEqual(after.Select(r => r.id).OrderBy(v => v)), "FLAT_FEATURE_IDENTITIES_CHANGED");
        int column = Array.IndexOf(configurations, allowedConfiguration);
        foreach (FlatFeatureState prior in before)
        {
            FlatFeatureState current = after.Single(r => r.id == prior.id);
            Need(prior.name == current.name && prior.type == current.type && prior.treeParentId == current.treeParentId && prior.dependencyParentIds.SequenceEqual(current.dependencyParentIds), "FLAT_FEATURE_BINDING_CHANGED " + prior.id);
            for (int i = 0; i < configurations.Length; i++)
                Need(allowedIds.Contains(prior.id) && i == column ? !current.configurationSuppression[i] : prior.configurationSuppression[i] == current.configurationSuppression[i], "FLAT_CHANGED_OTHER_FEATURE_OR_CONFIGURATION " + prior.id + "/" + configurations[i]);
        }
    }
    static object FlatBodyProof(ModelDoc2 model, double expectedWidth, double expectedHeight, double expectedThickness, DxfTrace trace)
    {
        object[] bodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
        try
        {
            Need(bodies.Length == 1, "FLAT_ONE_SOLID_BODY_REQUIRED"); Body2 body = (Body2)bodies[0]; var box = new double[6];
            for (int axis = 0; axis < 3; axis++)
            {
                double x, y, z; var direction = new double[3]; direction[axis] = -1;
                Need(body.GetExtremePoint(direction[0], direction[1], direction[2], out x, out y, out z), "FLAT_MIN_EXTREME_FAILED"); box[axis] = new[] { x, y, z }[axis] * 1000;
                direction[axis] = 1;
                Need(body.GetExtremePoint(direction[0], direction[1], direction[2], out x, out y, out z), "FLAT_MAX_EXTREME_FAILED"); box[axis + 3] = new[] { x, y, z }[axis] * 1000;
            }
            double[] spans = Enumerable.Range(0, 3).Select(i => box[i + 3] - box[i]).ToArray(); var offsets = new List<double>(); var planes = new List<object>();
            object[] faces = Items(body.GetFaces());
            try
            {
                foreach (Face2 face in faces)
                {
                    Surface surface = face.GetSurface() as Surface;
                    try
                    {
                        if (surface == null || !surface.IsPlane()) continue;
                        double[] plane = Numbers(surface.PlaneParams); Need(plane.Length == 6, "FLAT_PLANE_PARAMETERS_REQUIRED");
                        double norm = Math.Sqrt(plane.Take(3).Sum(v => v * v)), area = face.GetArea() * 1e6;
                        bool parallel = norm > 0 && Math.Abs(Math.Abs(plane[2] / norm) - 1) < 0.000001 && area > 0;
                        if (parallel) offsets.Add(plane[5] * 1000);
                        planes.Add(new { planeParameters = plane, areaMm2 = area, parallelToModelXY = parallel });
                    }
                    finally { Release(surface); }
                }
            }
            finally { foreach (object face in faces) Release(face); }
            double[] mass = Numbers(body.GetMassProperties(1));
            bool thickness = Math.Abs(spans[2] - expectedThickness) <= 0.01;
            bool parallelFaces = offsets.Count >= 2 && Math.Abs(offsets.Max() - offsets.Min() - expectedThickness) <= 0.01;
            bool width = Double.IsNaN(expectedWidth) || Math.Abs(spans[0] - expectedWidth) <= 0.01;
            bool height = Double.IsNaN(expectedHeight) || Math.Abs(spans[1] - expectedHeight) <= 0.01;
            bool solid = mass.Length > 3 && mass[3] > 0 && body.IsSheetMetal();
            bool pass = solid && box.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && spans[0] > expectedThickness && spans[1] > expectedThickness && thickness && parallelFaces && width && height;
            NativeCutSnapshot nativeCut = NativeCutGeometry(model, body, box[5]);
            object proof = new { body = body.Name, exactExtremaMm = box, widthHeightThicknessMm = spans, bodyCount = bodies.Length, sheetMetalSolid = solid,
                expectedWidthMm = Double.IsNaN(expectedWidth) ? (double?)null : expectedWidth, expectedHeightMm = Double.IsNaN(expectedHeight) ? (double?)null : expectedHeight,
                expectedThicknessMm = expectedThickness, thicknessPass = thickness, parallelFacesPass = parallelFaces, planarFaces = planes, widthPass = width, heightPass = height,
                expectedWidthHeightSpecified = !Double.IsNaN(expectedWidth) && !Double.IsNaN(expectedHeight), flatGeometryVerified = pass, cutGeometry = nativeCut, manufacturingAccepted = false };
            trace.Record("native_flat_body", proof); Need(pass, "ACTUAL_THIN_PLANAR_FLAT_GEOMETRY_REQUIRED"); return proof;
        }
        finally { foreach (object body in bodies) Release(body); }
    }
    public static object PrepareFlat(ModelDoc2 model, double expectedFlatWidthMm, double expectedFlatHeightMm, double expectedThicknessMm)
    {
        VariableTrial.RuntimeOwnership(); Need(model is PartDoc && !model.IsOpenedReadOnly(), "WRITABLE_OUTPUT_PART_REQUIRED_FOR_FLAT_PREPARATION");
        string input = Local(model.GetPathName()), hash = Hash(input), original = Configuration(model);
        Need(input.StartsWith(Path.GetFullPath(VariableTrial.OutputRoot).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase), "FLAT_PREPARATION_OUTPUT_COPY_ONLY");
        Need(original == "Default" && expectedThicknessMm > 0 && !Double.IsNaN(expectedThicknessMm) && !Double.IsInfinity(expectedThicknessMm), "FLAT_DEFAULT_AND_FINITE_THICKNESS_REQUIRED");
        Need((Double.IsNaN(expectedFlatWidthMm) || expectedFlatWidthMm > expectedThicknessMm && !Double.IsInfinity(expectedFlatWidthMm)) && (Double.IsNaN(expectedFlatHeightMm) || expectedFlatHeightMm > expectedThicknessMm && !Double.IsInfinity(expectedFlatHeightMm)), "FLAT_EXPECTED_DIMENSIONS_INVALID");
        string[] configurations = Items(model.GetConfigurationNames()).Select(Convert.ToString).ToArray();
        string[] flatNames = configurations.Where(v => v.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) >= 0).ToArray(); Need(flatNames.Length == 1, "ONE_EXISTING_FLAT_CONFIGURATION_REQUIRED"); string flatName = flatNames[0];
        var trace = new DxfTrace(Path.Combine(Path.GetDirectoryName(input), "flat-prepare")); var operations = new List<object>(); Exception primary = null;
        var originalStates = FlatFeatures(model, configurations); trace.Record("original_default", new { path = input, configuration = original, configurations = configurations, features = originalStates });
        try
        {
            ActivateConfiguration(model, flatName, "FLAT_PREPARE_CONFIGURATION_FAILED"); var before = FlatFeatures(model, configurations);
            CompareFlatStates(originalStates, before, configurations, null, new HashSet<int>());
            FlatFeatureState[] flats = before.Where(r => r.type == "FlatPattern").ToArray(); Need(flats.Length == 1, "ONE_EXISTING_FLAT_FEATURE_REQUIRED"); FlatFeatureState flat = flats[0];
            FlatFeatureState[] bends = before.Where(r => r.type == "UiBend" && r.treeParentId == flat.id).ToArray();
            Need(bends.Length == 12 && bends.All(r => r.dependencyParentIds.Length == 1 && before.Count(p => p.id == r.dependencyParentIds[0] && p.type == "OneBend") == 1), "TWELVE_BOUND_UI_BEND_ONE_BEND_LINKS_REQUIRED");
            var targets = new[] { flat }.Concat(bends).ToArray(); var allowedIds = new HashSet<int>(targets.Select(r => r.id));
            trace.Record("flat_enable_plan", new { configuration = flatName, allowedFeatures = targets, before = before, partSaveAllowed = false, onlyEnableExistingBoundFlatOperations = true });
            foreach (FlatFeatureState target in targets)
            {
                VariableTrial.RuntimeOwnership(); Feature feature = FlatFeatureById(model.FirstFeature() as Feature, true, target.id, new HashSet<int>(), 0);
                try
                {
                    Need(feature != null && feature.Name == target.name && feature.GetTypeName2() == target.type, "FLAT_ENABLE_LIVE_IDENTITY_CHANGED " + target.id);
                    bool prior = feature.IsSuppressed(); bool returned = !prior || feature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    bool actual = feature.IsSuppressed(); object operation = new { id = target.id, name = target.name, type = target.type, wasSuppressed = prior, setterCalled = prior, returned = returned, actualSuppressed = actual, configuration = flatName };
                    operations.Add(operation); trace.Record("flat_enable_operation", operation); Need(returned && !actual, "FLAT_BOUND_OPERATION_ENABLE_FAILED " + target.id);
                }
                finally { Release(feature); }
            }
            Need(model.ForceRebuild3(false), "FLAT_PREPARE_REBUILD_FAILED"); var after = FlatFeatures(model, configurations);
            trace.Record("flat_after_rebuild", new { features = after, configurations = configurations });
            CompareFlatStates(before, after, configurations, flatName, allowedIds);
            Need(after.All(r => r.suppressed || r.error == 0 || r.warning), "FLAT_NATIVE_FEATURE_HEALTH_FAILED");
            object geometry = FlatBodyProof(model, expectedFlatWidthMm, expectedFlatHeightMm, expectedThicknessMm, trace);
            Prepared[input] = new PreparedFlat { Configuration = flatName, Configurations = configurations, Features = after, Width = expectedFlatWidthMm, Height = expectedFlatHeightMm, Thickness = expectedThicknessMm };
            Need(trace.Errors.Count == 0, "FLAT_PREPARE_DIAGNOSTIC_WRITE_FAILED " + String.Join(" | ", trace.Errors));
            return new { success = true, modelPath = input, flatConfiguration = flatName, configurationNames = configurations, allowedFeatureIds = allowedIds.OrderBy(v => v).ToArray(), originalFeatures = originalStates,
                flatFeaturesBefore = before, flatFeaturesAfter = after, operations = operations, geometry = geometry, otherFeatureAndConfigurationSuppressionUnchanged = true,
                partSaved = false, partDiskUnchanged = true, diagnosticFiles = trace.Files, manufacturingAccepted = false };
        }
        catch (Exception error) { primary = error; trace.Record("prepare_primary_failure", new { error = error.ToString(), hResult = "0x" + error.HResult.ToString("X8", Invariant) }); throw; }
        finally
        {
            try
            {
                SameInput(model, input); ActivateConfiguration(model, original, "FLAT_PREPARE_DEFAULT_RESTORE_FAILED", false); Need(model.ForceRebuild3(false), "FLAT_PREPARE_DEFAULT_REBUILD_FAILED");
                PreparedFlat prepared;
                if (primary == null && Prepared.TryGetValue(input, out prepared))
                {
                    var restored = FlatFeatures(model, configurations); CompareFlatStates(prepared.Features, restored, configurations, null, new HashSet<int>());
                    trace.Record("prepare_default_feature_states", new { features = restored, allConfigurationStatesPreservedAfterReturnToDefault = true });
                }
                Need(Hash(input) == hash, "FLAT_PREPARATION_SAVED_PART_FORBIDDEN"); trace.Record("prepare_default_restored", new { configuration = Configuration(model), sourceDiskUnchanged = true });
                Need(trace.Errors.Count == 0, "FLAT_PREPARE_DIAGNOSTIC_WRITE_FAILED " + String.Join(" | ", trace.Errors));
            }
            catch (Exception cleanup) { trace.Record("prepare_restore_failure", new { primaryError = primary == null ? null : primary.ToString(), cleanupError = cleanup.ToString() }); if (primary == null) throw; }
        }
    }

    public static object ReadReopenedFlat(ModelDoc2 model, Dictionary<string, object> prior)
    {
        VariableTrial.RuntimeOwnership(); Need(model is PartDoc && model.IsOpenedReadOnly(), "READONLY_REOPENED_FLAT_PART_REQUIRED");
        string input = Local(model.GetPathName()), beforeHash = Hash(input), original = Configuration(model);
        Need(input.StartsWith(Path.GetFullPath(VariableTrial.OutputRoot).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) && original == "Default", "REOPENED_FLAT_OUTPUT_COPY_DEFAULT_REQUIRED");
        string[] configurations = Items(model.GetConfigurationNames()).Select(Convert.ToString).ToArray();
        Need(prior != null && prior.ContainsKey("success") && Convert.ToBoolean(prior["success"]) && configurations.SequenceEqual(Items(prior["configurationNames"]).Select(Convert.ToString)), "REOPENED_FLAT_PRIOR_CONFIGURATION_PROOF_REQUIRED");
        string flatName = Convert.ToString(prior["flatConfiguration"], Invariant);
        Need(configurations.Count(v => v.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) >= 0) == 1 && configurations.Contains(flatName), "REOPENED_SAVED_FLAT_CONFIGURATION_REQUIRED");
        var priorGeometry = (Dictionary<string, object>)prior["geometry"]; double[] expected = Numbers(priorGeometry["widthHeightThicknessMm"]);
        Need(Convert.ToBoolean(priorGeometry["flatGeometryVerified"]) && expected.Length == 3 && expected.All(v => v > 0 && !Double.IsNaN(v) && !Double.IsInfinity(v)), "REOPENED_FLAT_PRIOR_GEOMETRY_REQUIRED");
        var trace = new DxfTrace(Path.Combine(Path.GetDirectoryName(input), "flat-reopen")); var before = FlatFeatures(model, configurations); object geometry = null; Exception primary = null;
        try
        {
            ActivateConfiguration(model, flatName, "REOPENED_FLAT_CONFIGURATION_FAILED"); Need(model.ForceRebuild3(false), "REOPENED_FLAT_REBUILD_FAILED");
            var after = FlatFeatures(model, configurations); CompareFlatStates(before, after, configurations, null, new HashSet<int>());
            FlatFeatureState[] flats = after.Where(v => v.type == "FlatPattern").ToArray();
            Need(flats.Length == 1 && !flats[0].suppressed && after.Count(v => v.type == "UiBend" && v.treeParentId == flats[0].id && !v.suppressed) == 12 && after.All(v => v.suppressed || v.error == 0 || v.warning), "REOPENED_SAVED_FLAT_FEATURES_UNHEALTHY_OR_SUPPRESSED");
            geometry = FlatBodyProof(model, expected[0], expected[1], expected[2], trace);
            Need(NativeCuts.ContainsKey(input) && NativeCuts[input].complete, "REOPENED_FLAT_NATIVE_CUT_INCOMPLETE");
        }
        catch (Exception error) { primary = error; trace.Record("reopened_flat_primary_failure", new { error = error.ToString() }); throw; }
        finally
        {
            var cleanupErrors = new List<Exception>(); bool restored = false, diskUnchanged = false;
            try
            {
                ActivateConfiguration(model, original, "REOPENED_FLAT_DEFAULT_RESTORE_FAILED", false); Need(model.ForceRebuild3(false), "REOPENED_FLAT_DEFAULT_REBUILD_FAILED");
                CompareFlatStates(before, FlatFeatures(model, configurations), configurations, null, new HashSet<int>()); restored = true;
            }
            catch (Exception error) { cleanupErrors.Add(error); trace.Record("reopened_flat_restore_failure", new { error = error.ToString() }); }
            try { diskUnchanged = Hash(input) == beforeHash; Need(diskUnchanged, "REOPENED_FLAT_READONLY_DISK_CHANGED"); }
            catch (Exception error) { cleanupErrors.Add(error); trace.Record("reopened_flat_hash_failure", new { error = error.ToString() }); }
            trace.Record("reopened_flat_cleanup", new { originalConfiguration = original, originalConfigurationRestored = restored, partDiskUnchanged = diskUnchanged, featureSuppressionUnchanged = restored, partSaveCalled = false });
            if (trace.Errors.Count != 0) cleanupErrors.Add(new InvalidOperationException("REOPENED_FLAT_TRACE_WRITE_FAILED " + String.Join(" | ", trace.Errors)));
            if (cleanupErrors.Count != 0) throw new AggregateException("REOPENED_FLAT_CLEANUP_FAILED", primary == null ? cleanupErrors : new[] { primary }.Concat(cleanupErrors));
        }
        return new { pass = true, modelPath = input, flatConfiguration = flatName, configurationNames = configurations, geometry = geometry, originalConfigurationRestored = true, featureSuppressionUnchanged = true, partDiskUnchanged = true, partSaveCalled = false, diagnosticFiles = trace.Files, manufacturingAccepted = false };
    }

    static ModelDoc2 ActiveInput(ISldWorks sw, SessionIdentity session, string input)
    {
        SameSession(sw, session);
        ModelDoc2 current = sw.ActiveDoc as ModelDoc2;
        try { SameInput(current, input); Need(current is PartDoc, "ACTIVE_TRIAL_PART_REQUIRED"); return current; }
        catch { Release(current); throw; }
    }
    static ModelDoc2 ReopenSavedInput(ISldWorks sw, SessionIdentity session, string input, string sourceHash, string savedConfiguration, DxfTrace trace, string stage)
    {
        VariableTrial.RuntimeOwnership(); SameSession(sw, session);
        trace.Record(stage + "_get_documents_begin", new { input = input });
        object[] documents = Items(sw.GetDocuments()); var rows = new List<object>(); string title = null;
        try
        {
            int count = sw.GetDocumentCount(); trace.Record(stage + "_get_documents_returned", new { nativeDocumentCount = count, arrayLength = documents.Length });
            Need(count == documents.Length, "DXF_REOPEN_DOCUMENT_COUNT_MISMATCH");
            foreach (object value in documents)
            {
                ModelDoc2 document = value as ModelDoc2; Need(document is PartDoc, "DXF_REOPEN_UNKNOWN_DOCUMENT_PRESERVED");
                string path = Local(document.GetPathName());
                rows.Add(new { path = path, exactInput = String.Equals(path, input, StringComparison.OrdinalIgnoreCase) });
                Need(String.Equals(path, input, StringComparison.OrdinalIgnoreCase) && title == null, "DXF_REOPEN_OTHER_DOCUMENT_PRESERVED " + path);
                title = document.GetTitle();
            }
            trace.Record(stage + "_document_paths_verified", new { documents = rows, onlyExactInputOrEmpty = true });
        }
        catch (Exception error) { trace.Record(stage + "_document_inventory_failure", new { error = error.ToString(), observed = rows }); throw; }
        finally { foreach (object value in documents) Release(value); }
        Need(Hash(input) == sourceHash, "DXF_REOPEN_INPUT_DISK_CHANGED");
        if (title != null)
        {
            VariableTrial.RuntimeOwnership(); SameSession(sw, session);
            trace.Record(stage + "_close_exact_input_begin", new { input = input, title = title, saveCalled = false });
            sw.CloseDoc(title);
        }
        object[] remaining = Items(sw.GetDocuments());
        try { Need(sw.GetDocumentCount() == 0 && remaining.Length == 0, "DXF_REOPEN_REQUIRES_CONFIRMED_EMPTY_SESSION"); }
        finally { foreach (object value in remaining) Release(value); }
        VariableTrial.RuntimeOwnership(); SameSession(sw, session); Need(Hash(input) == sourceHash, "DXF_REOPEN_INPUT_DISK_CHANGED");
        int errors = 0, warnings = 0;
        trace.Record(stage + "_open_saved_input_begin", new { input = input, sourceSha256 = sourceHash, options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent, saveCalled = false });
        ModelDoc2 reopened = sw.OpenDoc6(input, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2;
        trace.Record(stage + "_open_saved_input_returned", new { returned = reopened != null, errors = errors, warnings = warnings });
        try
        {
            Need(reopened is PartDoc && errors == 0, "DXF_SAVED_INPUT_REOPEN_FAILED " + errors + "/" + warnings);
            SameInput(reopened, input); Need(!reopened.GetSaveFlag() && Configuration(reopened) == savedConfiguration, "DXF_REOPEN_CLEAN_SAVED_CONFIGURATION_REQUIRED");
            Need(Hash(input) == sourceHash, "DXF_REOPEN_INPUT_DISK_CHANGED"); VariableTrial.RegisterOwned(reopened);
            trace.Record(stage + "_saved_input_verified", new { path = input, configuration = Configuration(reopened), clean = true, sourceDiskUnchanged = true, errors = errors, warnings = warnings });
            return reopened;
        }
        catch { Release(reopened); throw; }
    }
    static bool NativeFlatDxf(ISldWorks sw, SessionIdentity session, string input, PreparedFlat prepared, string output, int options, List<object> attempts, DxfTrace trace)
    {
        VariableTrial.RuntimeOwnership();
        trace.Record("native_export_preflight_begin", new { path = output, input = input, options = options });
        ModelDoc2 current = null;
        var attempt = new Dictionary<string, object> { { "method", "IPartDoc.ExportFlatPatternView direct file" }, { "path", output },
            { "options", options }, { "optionName", Enum.GetName(typeof(swExportFlatPatternViewOptions_e), options) },
            { "returned", null }, { "saveAsCalled", false }, { "apiAlignmentSpecified", false }, { "partSaved", false } };
        attempts.Add(attempt);
        Exception failure = null; bool returned = false;
        try
        {
            current = ActiveInput(sw, session, input);
            ActivateConfiguration(current, prepared.Configuration, "DXF_EXISTING_FLAT_CONFIGURATION_REQUIRED");
            Need(current.ForceRebuild3(false), "DXF_FLAT_CONFIGURATION_REBUILD_FAILED");
            var states = FlatFeatures(current, prepared.Configurations);
            CompareFlatStates(prepared.Features, states, prepared.Configurations, null, new HashSet<int>());
            trace.Record("native_export_feature_states", new { configuration = Configuration(current), features = states, savedPreparedStateUnchanged = true });
            FlatBodyProof(current, prepared.Width, prepared.Height, prepared.Thickness, trace);
            current.ClearSelection2(true);
            attempt["configurationBefore"] = Configuration(current); attempt["modelPathBefore"] = current.GetPathName();
            attempt["saveFlagBefore"] = current.GetSaveFlag(); attempt["readOnlyBefore"] = current.IsOpenedReadOnly();
            VariableTrial.RuntimeOwnership(); NewPath(output);
            trace.Record("native_export_api_begin", attempt);
            try { returned = ((PartDoc)current).ExportFlatPatternView(output, options); attempt["returned"] = returned; }
            catch (Exception error) { failure = error; attempt["exception"] = error.ToString(); attempt["hResult"] = "0x" + error.HResult.ToString("X8", Invariant); }
            // The legacy API can retire a ModelDoc alias. Do not query it after the call.
            attempt["fileExists"] = File.Exists(output); attempt["length"] = File.Exists(output) ? (long?)new FileInfo(output).Length : null;
            attempt["oldModelGetterUsedAfterApi"] = false; trace.Record("native_export_api_return_and_disk_observation", attempt);
        }
        catch (Exception error) { trace.Record("native_export_preflight_failure", new { attempt = attempt, error = error.ToString() }); throw; }
        finally { try { Release(current); } catch (Exception error) { trace.Record("retired_model_release_failure", new { error = error.ToString() }); } }
        VariableTrial.RuntimeOwnership();
        Need(failure == null && returned && File.Exists(output) && new FileInfo(output).Length > 100,
            "NATIVE_FLAT_DXF_EXPORT_FAILED " + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(attempt));
        return returned;
    }

    public static object ExportDxf(ISldWorks sw, ModelDoc2 model, string dxfPath)
    {
        VariableTrial.RuntimeOwnership();
        Need(sw != null, "OWNED_CAD_SESSION_REQUIRED"); SessionIdentity session = CaptureSession(sw);
        string input = InputPath(model), sourceHash = Hash(input), originalConfig = Configuration(model);
        ModelDoc2 active = ActiveInput(sw, session, input);
        try { Need(sw.IsSame(active, model) == (int)swObjectEquality.swObjectSame, "DXF_EXACT_ACTIVE_INPUT_REQUIRED"); }
        finally { Release(active); }
        string[] flatConfigurations = Items(model.GetConfigurationNames()).Select(Convert.ToString).Where(v => v.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        Need(flatConfigurations.Length == 1, "ONE_EXISTING_FLAT_CONFIGURATION_REQUIRED_NO_FEATURE_EDITS");
        string flatConfiguration = flatConfigurations[0];
        PreparedFlat prepared; Need(Prepared.TryGetValue(input, out prepared) && prepared.Configuration == flatConfiguration, "PREPARED_NATIVE_FLAT_EVIDENCE_REQUIRED");
        string output = OutputPath(model, dxfPath);
        Need(Path.GetExtension(output).Equals(".dxf", StringComparison.OrdinalIgnoreCase), "DXF_EXTENSION_REQUIRED");
        string cutPath = NewPath(Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + ".native-cut.dxf"));
        string allPath = NewPath(Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + ".native-bends.dxf"));
        VariableTrial.RuntimeOwnership(); Directory.CreateDirectory(Path.GetDirectoryName(output));
        var trace = new DxfTrace(Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "-export")); Exception primary = null;
        trace.Record("export_begin", new { input = input, sourceSha256 = sourceHash, savedConfiguration = originalConfig, flatConfiguration = flatConfiguration, cadPid = session.Pid, cadStartTicks = session.StartTicks, cadExecutable = session.Executable });
        try
        {
            // The same trial doors previously exported with the legacy API; ExportToDWG2 returned false.
            // Legacy option 0 retains bends and option 1 removes them. No feature suppression is changed here.
            double[] alignment = { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            var attempts = new List<object>();
            bool bends = NativeFlatDxf(sw, session, input, prepared, allPath, (int)swExportFlatPatternViewOptions_e.swExportFlatPatternOption_None, attempts, trace);
            ModelDoc2 fresh = ReopenSavedInput(sw, session, input, sourceHash, originalConfig, trace, "between_native_exports"); Release(fresh);
            bool cut = NativeFlatDxf(sw, session, input, prepared, cutPath, (int)swExportFlatPatternViewOptions_e.swExportFlatPatternOption_RemoveBends, attempts, trace);
            var cutPairs = ReadDxf(cutPath); var allPairs = ReadDxf(allPath);
            var cutEntities = Entities(cutPairs); var allEntities = Entities(allPairs);
            Need(Units(cutPairs) == Units(allPairs), "NATIVE_DXF_UNIT_MISMATCH");
            Need(Units(allPairs) == 4, "NATIVE_DXF_MILLIMETRE_UNITS_REQUIRED");
            var remaining = cutEntities.GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.Count());
            var rows = new List<object>(); int bendCount = 0;
            foreach (DxfEntity entity in allEntities)
            {
                VariableTrial.RuntimeOwnership();
                string sourceLayer = Text(allPairs, entity, 8, "0");
                int count; bool isCut = remaining.TryGetValue(entity.Key, out count) && count > 0;
                string layer = isCut ? "CUT" : "BEND";
                if (isCut) remaining[entity.Key] = count - 1;
                else { Need(entity.Type == "LINE", "NONLINEAR_DIFFERENTIAL_BEND_UNSUPPORTED " + entity.Type); bendCount++; }
                bool mapped = false;
                for (int i = entity.Start + 1; i < entity.End; i++) if (allPairs[i].Code == 8) { allPairs[i].Value = layer; mapped = true; }
                Need(mapped, "NATIVE_ENTITY_LAYER_TAG_REQUIRED");
                rows.Add(new { type = entity.Type, sourceLayer = sourceLayer, targetLayer = layer, evidence = isCut ? "same geometry in legacy RemoveBends option 1" : "additional LINE in legacy None option 0" });
            }
            Need(remaining.Values.All(v => v == 0), "COMBINED_DXF_LOST_CUT_GEOMETRY");
            Need(bendCount > 0, "NATIVE_DXF_HAS_NO_DIFFERENTIAL_BEND_LINES");
            string[] geometryBefore = allEntities.Select(e => e.Key).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            AddLayers(allPairs);
            VariableTrial.RuntimeOwnership(); NewPath(output);
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Encoding.Default))
                for (int i = 0; i < allPairs.Count; i++)
                {
                    if ((i & 511) == 0) VariableTrial.RuntimeOwnership();
                    Pair pair = allPairs[i]; writer.WriteLine(pair.Code.ToString(Invariant)); writer.WriteLine(pair.Value);
                }
            var written = ReadDxf(output);
            Need(geometryBefore.SequenceEqual(Entities(written).Select(e => e.Key).OrderBy(v => v, StringComparer.Ordinal)), "LAYER_MAPPING_CHANGED_DXF_GEOMETRY");
            Need(Hash(input) == sourceHash, "DXF_EXPORT_CHANGED_PART_FILE");
            Need(trace.Errors.Count == 0, "DXF_DIAGNOSTIC_WRITE_FAILED " + String.Join(" | ", trace.Errors));
            NativeCutSnapshot nativeCut; NativeCuts.TryGetValue(input, out nativeCut);
            Exports[input] = new DxfManifest { Path = output, Sha256 = Hash(output), SourceSha256 = sourceHash, Alignment = (double[])alignment.Clone(), NativeExportMethod = "IPartDoc.ExportFlatPatternView direct file", ApiAlignmentSpecified = false, CutGeometry = nativeCut };
            return new
            {
                success = true, exported = true, path = output, sha256 = Hash(output), modelPath = input,
                nativeCutPath = cutPath, nativeCutSha256 = Hash(cutPath), nativeBendsPath = allPath, nativeBendsSha256 = Hash(allPath),
                nativeCutReturned = cut, nativeBendsReturned = bends, nativeErrors = (int?)null, nativeWarnings = (int?)null,
                errors = new string[0], warnings = new[] { "ExportFlatPatternView returns enablement only; success here also requires actual new native DXF files. No native error/warning codes are exposed.", "The legacy API has no alignment argument. Literal model X/Y is only an endpoint-comparison candidate, not an API-guaranteed export basis.", "Bend directions are UNKNOWN. No BEND_UP/DOWN labels were inferred.", "Contour, duplicate, hole and unit acceptance requires dxf_check.py." },
                insunits = Units(written), unitsVerified = Units(written) == 4, cutEntityCount = cutEntities.Count, bendEntityCount = bendCount,
                markEntityCount = 0, declaredLayers = new[] { "CUT", "BEND", "MARK" }, differentialLayerEvidence = rows,
                bendDirection = "UNKNOWN", geometryPreserved = true, partDiskUnchanged = true, partSaved = false,
                systemPreferencesChanged = false, nativeExportMethod = "IPartDoc.ExportFlatPatternView direct file", nativeExportOptions = new[] { 0, 1 },
                nativeCutOption = 1, nativeBendsOption = 0, nativeExportAttempts = attempts, exportConfiguration = flatConfiguration, manufacturingAccepted = false,
                diagnosticFiles = trace.Files,
                nativeCutGeometry = nativeCut, coordinateMappingVerified = false, manufacturingOrientationVerified = false,
                apiAlignmentSpecified = false, alignment = (double[])null, xFlipped = (bool?)null, yFlipped = (bool?)null,
                comparisonAlignment = alignment, comparisonXFlipped = false, comparisonYFlipped = false,
                comparisonCoordinateBasis = "Literal model X/Y in mm; confirmed only if actual DXF endpoints match uniquely, without fitting."
            };
        }
        catch (Exception error) { primary = error; trace.Record("export_primary_failure", new { error = error.ToString(), hResult = "0x" + error.HResult.ToString("X8", Invariant) }); throw; }
        finally
        {
            try
            {
                ModelDoc2 refreshed = ReopenSavedInput(sw, session, input, sourceHash, originalConfig, trace, "export_restore_saved_default");
                try { Need(!refreshed.GetSaveFlag() && Configuration(refreshed) == originalConfig, "DXF_CLEAN_DEFAULT_RESTORE_FAILED"); }
                finally { Release(refreshed); }
                Need(Hash(input) == sourceHash, "DXF_EXPORT_CHANGED_PART_FILE");
                Need(trace.Errors.Count == 0, "DXF_DIAGNOSTIC_WRITE_FAILED " + String.Join(" | ", trace.Errors));
            }
            catch (Exception cleanup) { trace.Record("export_restore_failure", new { primaryError = primary == null ? null : primary.ToString(), cleanupError = cleanup.ToString() }); if (primary == null) throw; }
        }
    }

    sealed class VertexPoint
    {
        internal Vertex Vertex;
        internal double[] Native, Projected;
    }
    static double[] Project(double[] point, SwView view)
    {
        MathTransform transform = view.ModelToViewTransform;
        try
        {
            double[] m = Numbers(transform.ArrayData);
            Need(m.Length == 16 && m[12] > 0, "NATIVE_VIEW_TRANSFORM_REQUIRED");
            return Enumerable.Range(0, 3).Select(i => m[12] * (point[0] * m[i] + point[1] * m[i + 3] + point[2] * m[i + 6]) + m[i + 9]).ToArray();
        }
        finally { Release(transform); }
    }
    static object FontReadback(Annotation annotation, Note note)
    {
        var result = new Dictionary<string, object>(); var formats = new List<object>(); var items = new List<object>();
        try
        {
            result["annotationName"] = annotation.GetName(); result["annotationType"] = annotation.GetType(); int count = annotation.GetTextFormatCount(); result["formatCount"] = count;
            for (int i = 0; i < count; i++)
            {
                TextFormat format = annotation.GetTextFormat(i) as TextFormat;
                try { formats.Add(new { index = i, useDocumentFormat = annotation.GetUseDocTextFormat(i), format = format == null ? null : PdfProbeFormatValues(format) }); }
                finally { Release(format); }
            }
            if (note != null)
            {
                result["text"] = note.GetText(); result["isBendLineNote"] = note.IsBendLineNote; result["hasMultipleFonts"] = note.HasMultipleFonts; int textCount = note.GetTextCount(); result["textCount"] = textCount;
                for (int i = 0; i < textCount; i++) items.Add(new { formatIndexZeroBased = i, textIndexOneBased = i + 1, text = note.GetTextAtIndex(i + 1), font = note.GetTextFontAtIndex(i), heightMetres = note.GetTextHeightAtIndex(i), angleRadians = note.GetTextAngleAtIndex(i) });
            }
        }
        catch (Exception error) { result["readbackError"] = error.ToString(); }
        result["formats"] = formats; result["textItems"] = items; return result;
    }
    static object Font(Annotation annotation, double height, DxfTrace trace = null, Note note = null)
    {
        VariableTrial.RuntimeOwnership();
        if (trace != null) trace.Record("drawing_bend_note_font_before", new { requestedTypeface = "汉仪长仿宋体", requestedHeightMetres = height, readback = FontReadback(annotation, note) });
        if (note != null && note.HasMultipleFonts)
        {
            string originalText = note.GetText(); bool reset = annotation.SetTextFormat(0, true, null);
            if (trace != null) trace.Record("drawing_bend_note_rich_font_reset", new { returned = reset, originalText = originalText, requestedUseDocumentFormat = true, readback = FontReadback(annotation, note) });
            Need(reset && note.GetText() == originalText && !note.HasMultipleFonts, "NATIVE_BEND_NOTE_RICH_FONT_RESET_OR_TEXT_PRESERVATION_FAILED");
        }
        TextFormat format = annotation.GetTextFormat(0) as TextFormat;
        Need(format != null, "ANNOTATION_TEXT_FORMAT_REQUIRED");
        try
        {
            format.TypeFaceName = "汉仪长仿宋体"; format.CharHeight = height;
            if (trace != null) trace.Record("drawing_bend_note_font_request", new { index = 0, useDocumentFormat = false, requestedFormat = PdfProbeFormatValues(format) });
            if (note != null) Need(!format.IsHeightSpecifiedInPts(), "NATIVE_BEND_NOTE_METRE_HEIGHT_REQUEST_REQUIRED");
            bool applied = annotation.SetTextFormat(0, false, format);
            if (trace != null) trace.Record("drawing_bend_note_font_raw_readback", new { returned = applied, requestedTypeface = "汉仪长仿宋体", requestedHeightMetres = height, readback = FontReadback(annotation, note) });
            Need(applied, "ANNOTATION_FONT_SET_FAILED");
        }
        finally { Release(format); }
        format = annotation.GetTextFormat(0) as TextFormat;
        try
        {
            Need(format != null && format.TypeFaceName == "汉仪长仿宋体" && Math.Abs(format.CharHeight - height) < 0.000001, "ANNOTATION_FONT_READBACK_FAILED");
            return new { name = format.TypeFaceName, heightMetres = format.CharHeight };
        }
        finally { Release(format); }
    }
    static void ActivateOutputDrawing(ISldWorks sw, ModelDoc2 drawing)
    {
        VariableTrial.RuntimeOwnership();
        Need(drawing is DrawingDoc, "EXACT_OUTPUT_DRAWING_REQUIRED");
        if (!sw.Visible) sw.Visible = true; if (!drawing.Visible) drawing.Visible = true;
        int error = 0; ModelDoc2 activated = sw.ActivateDoc3(drawing.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref error) as ModelDoc2;
        ModelDoc2 active = sw.ActiveDoc as ModelDoc2;
        try { Need(error == 0 && activated != null && active != null && sw.IsSame(activated, drawing) == 1 && sw.IsSame(active, drawing) == 1, "EXACT_OUTPUT_DRAWING_ACTIVATION_FAILED"); }
        finally { Release(active); Release(activated); }
    }
    static string NoteText(string text) { return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'); }
    static object NoteAt(ISldWorks sw, ModelDoc2 drawing, string text, double x, double y, double size)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing; document.EditSheet();
        Need(document.ActivateView(""), "SHEET_NOTE_CONTEXT_REQUIRED"); drawing.ClearSelection2(true);
        Note note = drawing.InsertNote(text) as Note;
        Need(note != null, "CHINESE_NOTE_INSERT_FAILED");
        Annotation annotation = note.GetAnnotation() as Annotation; SwView sheetView = null; Sheet sheet = null; object[] members = new object[0];
        try
        {
            Need(annotation != null, "NOTE_ANNOTATION_REQUIRED"); object font = Font(annotation, size);
            annotation.Visible = (int)swAnnotationVisibilityState_e.swAnnotationVisible;
            Need(annotation.SetPosition2(x, y, 0) && !annotation.IsDangling() && annotation.Visible == (int)swAnnotationVisibilityState_e.swAnnotationVisible, "VISIBLE_NON_DANGLING_NOTE_POSITION_REQUIRED");
            sheet = document.GetCurrentSheet() as Sheet; sheetView = document.GetFirstView() as SwView;
            Need(sheet != null && sheetView != null, "NOTE_EXACT_DRAWING_SHEET_VIEW_REQUIRED"); members = Items(sheetView.GetNotes());
            Need(members.Count(v => v is Note && sw.IsSame(v, note) == 1) == 1 && NoteText(note.GetText()) == NoteText(text), "NOTE_MUST_BELONG_TO_EXACT_DRAWING_SHEET_WITH_EXACT_TEXT");
            return new Dictionary<string, object> { { "text", text }, { "x", x }, { "y", y }, { "font", font }, { "annotationName", annotation.GetName() },
                { "sheet", sheet.GetName() }, { "sheetView", sheetView.GetName2() }, { "exactDrawingSheetMembershipVerified", true }, { "textReadbackVerified", true }, { "dangling", false }, { "visible", true } };
        }
        finally { foreach (object member in members) Release(member); Release(sheet); Release(sheetView); Release(annotation); Release(note); drawing.ClearSelection2(true); }
    }
    static object DrawingOutputContext(ISldWorks sw, ModelDoc2 drawing, bool requireReady = false)
    {
        ModelDoc2 active = sw.ActiveDoc as ModelDoc2; Sheet sheet = ((DrawingDoc)drawing).GetCurrentSheet() as Sheet; SelectionMgr selection = drawing.SelectionManager as SelectionMgr;
        try
        {
            bool exact = active != null && sw.IsSame(active, drawing) == 1;
            int selectedCount = selection == null ? -1 : selection.GetSelectedObjectCount2(-1);
            bool ready = exact && sw.Visible && drawing.Visible && !drawing.IsOpenedReadOnly() && !drawing.IsOpenedViewOnly() && sheet != null && selectedCount == 0;
            if (requireReady) Need(ready, "VISIBLE_WRITABLE_ACTIVE_DRAWING_WITH_EMPTY_SELECTION_REQUIRED");
            return new { ready = ready, activeDrawingIdentityVerified = exact, title = drawing.GetTitle(), path = drawing.GetPathName(), activeTitle = active == null ? null : active.GetTitle(), activePath = active == null ? null : active.GetPathName(),
                applicationVisible = sw.Visible, drawingVisible = drawing.Visible, readOnly = drawing.IsOpenedReadOnly(), viewOnly = drawing.IsOpenedViewOnly(), selectedCount = selectedCount,
                dirtyInMemory = drawing.GetSaveFlag(), sheet = sheet == null ? null : sheet.GetName(), sheetFormatVisible = sheet == null ? (bool?)null : sheet.SheetFormatVisible, sheetFormatTemplate = sheet == null ? null : sheet.GetTemplateName() };
        }
        finally { Release(selection); Release(sheet); Release(active); }
    }
    static object PrepareDrawingDisplay(ISldWorks sw, ModelDoc2 drawing, SwView[] views, List<object> expectedNotes, bool prepareDisplayMode, DxfTrace trace)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing; document.EditSheet();
        Need(document.ActivateView(""), "OUTPUT_SHEET_CONTEXT_REQUIRED"); drawing.SetPickMode(); drawing.ClearSelection2(true);
        ModelView window = drawing.ActiveView as ModelView; SwView sheetView = null; object[] notes = new object[0]; LayerMgr layers = null;
        try
        {
            Need(window != null, "OUTPUT_DRAWING_GRAPHICS_WINDOW_REQUIRED"); window.EnableGraphicsUpdate = true;
            Need(window.EnableGraphicsUpdate, "OUTPUT_DRAWING_GRAPHICS_UPDATES_REQUIRED");
            foreach (SwView view in views)
                if (prepareDisplayMode) Need(view.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN, false, true), "OUTPUT_PRECISE_HLR_VIEW_MODE_REQUIRED");
            if (prepareDisplayMode) Need(drawing.ForceRebuild3(false), "OUTPUT_DRAWING_DISPLAY_REBUILD_FAILED");
            drawing.ViewZoomtofit2();
            foreach (SwView view in views) view.UpdateViewDisplayGeometry();
            drawing.GraphicsRedraw2(); drawing.WindowRedraw();
            var viewProof = new List<object>();
            foreach (SwView view in views)
            {
                int points = 0, count = view.GetPolyLineCount5((short)1, out points);
                var geometryProof = new { phase = prepareDisplayMode ? "native-save" : "pdf-save", name = view.GetName2(), displayMode = view.GetDisplayMode2(), draftQuality = view.GetFacettedHlrDisplay(), nativePolylineCount = count, nativePolylineDoubleCount = points, displayGeometrySynchronized = true };
                trace.Record("drawing_display_native_polylines", geometryProof);
                Need(view.GetDisplayMode2() == (int)swDisplayMode_e.swHIDDEN && !view.GetFacettedHlrDisplay() && count > 0 && points > 0, "NONEMPTY_PRECISE_NATIVE_DRAWING_DISPLAY_POLYLINES_REQUIRED " + view.GetName2());
                viewProof.Add(geometryProof);
            }
            sheetView = document.GetFirstView() as SwView; Need(sheetView != null, "OUTPUT_DRAWING_SHEET_NOTE_VIEW_REQUIRED"); notes = Items(sheetView.GetNotes());
            var noteProof = new List<object>();
            foreach (Dictionary<string, object> expected in expectedNotes.Cast<Dictionary<string, object>>())
            {
                Note[] matches = notes.OfType<Note>().Where(v => NoteText(v.GetText()) == NoteText(Convert.ToString(expected["text"], Invariant))).ToArray();
                Need(matches.Length == 1, "ONE_EXACT_NATIVE_SHEET_NOTE_TEXT_REQUIRED"); Annotation annotation = matches[0].GetAnnotation() as Annotation;
                try
                {
                    Need(annotation != null && !annotation.IsDangling() && annotation.Visible == (int)swAnnotationVisibilityState_e.swAnnotationVisible, "OUTPUT_SHEET_NOTE_MUST_BE_VISIBLE_AND_NON_DANGLING");
                    noteProof.Add(new { text = matches[0].GetText(), annotationName = annotation.GetName(), exactSheetMembershipVerified = true, dangling = false, visible = true, layer = annotation.Layer });
                }
                finally { Release(annotation); }
            }
            var layerProof = new List<object>(); layers = drawing.GetLayerManager() as LayerMgr;
            if (layers != null)
                foreach (string name in Items(layers.GetLayerList()).Select(Convert.ToString))
                {
                    Layer layer = layers.GetLayer(name) as Layer;
                    try { Need(layer != null, "OUTPUT_DRAWING_LAYER_READBACK_REQUIRED"); layerProof.Add(new { name = name, visible = layer.Visible, printable = layer.Printable }); }
                    finally { Release(layer); }
                }
            return new { context = DrawingOutputContext(sw, drawing, true), graphicsUpdatesEnabled = window.EnableGraphicsUpdate, zoomToFit = true,
                updateViewDisplayGeometryCalled = true, graphicsRedraw2Called = true, windowRedrawCalled = true, views = viewProof, expectedSheetNoteCount = expectedNotes.Count, verifiedSheetNoteCount = noteProof.Count, sheetNotes = noteProof, layers = layerProof };
        }
        finally { Release(layers); foreach (object note in notes) Release(note); Release(sheetView); Release(window); }
    }
    static object VisibleViewEntities(ModelDoc2 model, SwView view, bool includeGeometry)
    {
        object[] components = Items(view.GetVisibleComponents()); var rows = new List<object>();
        try
        {
            foreach (Component2 component in components)
                foreach (int kind in new[] { (int)swViewEntityType_e.swViewEntityType_Vertex, (int)swViewEntityType_e.swViewEntityType_Edge })
                {
                    object[] entities = Items(view.GetVisibleEntities2(component, kind)); var geometry = new List<object>();
                    try
                    {
                        foreach (object entity in includeGeometry ? entities : new object[0])
                        {
                            object native = null; Curve curve = null; CurveParamData parameters = null;
                            try
                            {
                                native = model.Extension.GetCorrespondingEntity2(entity); double[][] points = null;
                                if (native is Vertex) points = new[] { Numbers(((Vertex)native).GetPoint()) };
                                else if (native is Edge)
                                {
                                    curve = ((Edge)native).GetCurve() as Curve; parameters = ((Edge)native).GetCurveParams3();
                                    if (curve != null && parameters != null) points = new[] { Numbers(parameters.StartPoint), Numbers(parameters.EndPoint) };
                                }
                                geometry.Add(new { entityType = entity is Entity ? (int?)((Entity)entity).GetType() : null, supportsVertexInterface = entity is Vertex,
                                    nativeEntityFound = native != null, nativePointsMetres = points, projectedPointsMetres = points == null ? null : points.Select(p => Project(p, view)).ToArray() });
                            }
                            catch (Exception error) { geometry.Add(new { error = error.ToString() }); }
                            finally { Release(parameters); Release(curve); Release(native); }
                        }
                        rows.Add(new { component = component.Name2, viewEntityType = kind, nativeCount = view.GetVisibleEntityCount2(component, kind), arrayCount = entities.Length, entities = geometry });
                    }
                    finally { foreach (object entity in entities) Release(entity); }
                }
            return new { componentCount = components.Length, components = rows };
        }
        finally { foreach (object component in components) Release(component); }
    }
    static Entity SelectExtremeVertex(ISldWorks sw, ModelDoc2 model, ModelDoc2 drawing, SwView view, SelectionMgr selection, SelectData data,
        IEnumerable<VertexPoint> candidates, Entity first, string end, List<object> attempts, out VertexPoint nativePoint)
    {
        nativePoint = null;
        foreach (VertexPoint point in candidates)
        {
            VariableTrial.RuntimeOwnership(); object mapped = null, reverse = null, actual = null, actualNative = null; SwView selectedView = null; bool retained = false;
            var row = new Dictionary<string, object> { { "end", end }, { "nativePointMetres", point.Native }, { "projectedPointMetres", point.Projected } }; attempts.Add(row);
            try
            {
                drawing.ClearSelection2(true);
                if (first != null) Need(first.Select4(false, data) && selection.GetSelectedObjectCount2(-1) == 1, "FIRST_EXACT_VIEW_VERTEX_RESELECTION_FAILED");
                mapped = view.GetCorrespondingEntity(point.Vertex);
                row["mappedFound"] = mapped != null; row["mappedIsEntity"] = mapped is Entity; row["mappedSupportsVertexInterface"] = mapped is Vertex;
                bool selected;
                if (mapped != null)
                {
                    Need(mapped is Entity && ((Entity)mapped).GetType() == (int)swSelectType_e.swSelVERTICES, "MAPPED_ENTITY_MUST_BE_NATIVE_VERTEX");
                    reverse = model.Extension.GetCorrespondingEntity2(mapped); row["mappedReverseNativeIdentity"] = reverse != null && sw.IsSame(reverse, point.Vertex) == 1;
                    row["mappedDirectNativeIdentity"] = sw.IsSame(mapped, point.Vertex) == 1;
                    Need((bool)row["mappedReverseNativeIdentity"] || (bool)row["mappedDirectNativeIdentity"], "MAPPED_VERTEX_REVERSE_NATIVE_IDENTITY_FAILED");
                    row["selectionApi"] = "IEntity.Select4 with exact SelectData.View"; selected = ((Entity)mapped).Select4(first != null, data);
                }
                else
                {
                    // The official view API can select a part entity directly; validate the actual resulting view entity below.
                    row["selectionApi"] = "IView.SelectEntity with exact native vertex"; selected = view.SelectEntity(point.Vertex, first != null);
                }
                int index = first == null ? 1 : 2; row["selectReturned"] = selected; row["selectedCount"] = selection.GetSelectedObjectCount2(-1);
                Need(selected && selection.GetSelectedObjectCount2(-1) == index, "EXACT_VIEW_VERTEX_SELECTION_COUNT_FAILED");
                actual = selection.GetSelectedObject6(index, -1); selectedView = selection.GetSelectedObjectsDrawingView2(index, -1);
                actualNative = actual == null ? null : model.Extension.GetCorrespondingEntity2(actual);
                row["selectedType"] = selection.GetSelectedObjectType3(index, -1);
                row["selectedViewIdentity"] = selectedView != null && sw.IsSame(selectedView, view) == 1;
                row["selectedReverseNativeIdentity"] = actualNative != null && sw.IsSame(actualNative, point.Vertex) == 1;
                row["selectedDirectNativeIdentity"] = actual != null && sw.IsSame(actual, point.Vertex) == 1;
                Need(actual is Entity && ((Entity)actual).GetType() == (int)swSelectType_e.swSelVERTICES
                    && selection.GetSelectedObjectType3(index, -1) == (int)swSelectType_e.swSelVERTICES
                    && (bool)row["selectedViewIdentity"] && ((bool)row["selectedReverseNativeIdentity"] || (bool)row["selectedDirectNativeIdentity"]), "ACTUAL_SELECTED_VERTEX_OR_VIEW_IDENTITY_FAILED");
                nativePoint = point; retained = true; row["verified"] = true; return (Entity)actual;
            }
            catch (Exception error) { row["error"] = error.ToString(); }
            finally { Release(mapped); Release(reverse); Release(actualNative); Release(selectedView); if (!retained) Release(actual); }
        }
        return null;
    }
    static object DimensionProof(ISldWorks sw, ModelDoc2 model, DisplayDimension display, double expected, SwView view, string axis, Vertex nativeA, Vertex nativeB)
    {
        Annotation annotation = display.GetAnnotation() as Annotation;
        Dimension dimension = display.GetDimension2(0) as Dimension;
        try
        {
            Need(annotation != null && dimension != null && !annotation.IsDangling() && !display.GetOverride(), "REAL_NON_DANGLING_DIMENSION_REQUIRED " + axis);
            Need(dimension.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriven && Math.Abs(dimension.SystemValue - expected) < Tol, "ASSOCIATIVE_DIMENSION_VALUE_FAILED " + axis);
            object[] attached = Items(annotation.GetAttachedEntities3());
            try
            {
                Need(attached.Length == 2 && annotation.GetAttachedEntityCount3() == 2 && attached.All(v => v is Entity && ((Entity)v).GetType() == (int)swSelectType_e.swSelVERTICES), "TWO_REAL_VERTEX_ATTACHMENTS_REQUIRED " + axis);
                var identities = new List<int>();
                foreach (object entity in attached)
                {
                    object native = model.Extension.GetCorrespondingEntity2(entity);
                    try
                    {
                        bool a = sw.IsSame(entity, nativeA) == 1 || native != null && sw.IsSame(native, nativeA) == 1,
                            b = sw.IsSame(entity, nativeB) == 1 || native != null && sw.IsSame(native, nativeB) == 1;
                        Need(a != b, "EXACT_NATIVE_EXTREME_VERTEX_ATTACHMENT_REQUIRED " + axis); identities.Add(a ? 0 : 1);
                    }
                    finally { Release(native); }
                }
                Need(identities.Distinct().Count() == 2, "BOTH_DISTINCT_NATIVE_EXTREME_VERTEX_ATTACHMENTS_REQUIRED " + axis);
            }
            finally { foreach (object entity in attached) Release(entity); }
            Need(String.IsNullOrEmpty(display.GetLinkedText()) && display.ShowDimensionValue, "NATIVE_DIMENSION_VALUE_TEXT_REQUIRED");
            object font = Font(annotation, 0.0027);
            return new { axis = axis, view = view.GetName2(), annotation = annotation.GetName(), nativeName = dimension.FullName, actualMm = dimension.SystemValue * 1000, expectedMm = expected * 1000, dangling = false, overrideEnabled = false, attachedVertexCount = 2, exactNativeExtremeAttachmentsVerified = true, driven = true, font = font };
        }
        finally { Release(dimension); Release(annotation); }
    }
    static object Overall(ISldWorks sw, ModelDoc2 model, ModelDoc2 drawing, SwView view, bool horizontal, DxfTrace trace)
    {
        VariableTrial.RuntimeOwnership();
        string configuration = view.ReferencedConfiguration, viewName = view.GetName2(), axisName = horizontal ? "width" : "height";
        trace.Record("drawing_dimension_begin", new { view = viewName, flat = view.IsFlatPatternView(), axis = axisName, configuration = configuration, modelPath = model.GetPathName() });
        int activationError = 0; ModelDoc2 active = sw.ActivateDoc3(drawing.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activationError) as ModelDoc2;
        try { Need(active != null && activationError == 0 && sw.IsSame(active, drawing) == 1, "EXACT_DIMENSION_DRAWING_ACTIVATION_FAILED"); }
        finally { Release(active); }
        Need(((DrawingDoc)drawing).ActivateView(viewName), "DIMENSION_DRAWING_VIEW_ACTIVATION_FAILED");
        ActivateConfiguration(model, configuration, "DIMENSION_SOURCE_CONFIGURATION_FAILED " + configuration);
        Need(model.ForceRebuild3(false), "DIMENSION_SOURCE_REBUILD_FAILED");
        Need(drawing.ForceRebuild3(false), "DIMENSION_DRAWING_VIEW_REBUILD_FAILED");
        Need(Configuration(model) == configuration, "DIMENSION_SOURCE_CONFIGURATION_DRIFT_AFTER_VIEW_REBUILD");
        trace.Record("drawing_dimension_visible_counts", new { view = viewName, flat = view.IsFlatPatternView(), axis = axisName, configuration = configuration, visible = VisibleViewEntities(model, view, false) });
        var vertices = new List<VertexPoint>();
        object[] bodies = Items(((PartDoc)model).GetBodies2(0, false));
        try
        {
            Need(bodies.Length == 1, "ONE_SHEET_METAL_BODY_REQUIRED_FOR_DIMENSION");
            foreach (Vertex vertex in Items(((Body2)bodies[0]).GetVertices()).Cast<Vertex>())
            { VariableTrial.RuntimeOwnership(); double[] point = Numbers(vertex.GetPoint()); vertices.Add(new VertexPoint { Vertex = vertex, Native = point, Projected = Project(point, view) }); }
            Need(vertices.Count > 1, "REAL_BODY_VERTICES_REQUIRED");
            int axis = horizontal ? 0 : 1;
            double min = vertices.Min(v => v.Projected[axis]), max = vertices.Max(v => v.Projected[axis]);
            double expected = (max - min) / view.ScaleDecimal;
            Need(expected > 0.001, "NONZERO_PROJECTED_DIMENSION_REQUIRED");
            trace.Record("drawing_dimension_native_extrema", new { view = viewName, flat = view.IsFlatPatternView(), axis = axisName, scale = view.ScaleDecimal, expectedMm = expected * 1000,
                vertices = vertices.Select(v => new { nativeMetres = v.Native, projectedMetres = v.Projected, minimumCandidate = Math.Abs(v.Projected[axis] - min) < Tol, maximumCandidate = Math.Abs(v.Projected[axis] - max) < Tol }).ToArray() });
            Entity a = null, b = null; VertexPoint nativeA = null, nativeB = null; var attempts = new List<object>();
            try
            {
                SelectionMgr selection = drawing.SelectionManager as SelectionMgr; SelectData data = selection.CreateSelectData();
                try
                {
                    data.View = view;
                    a = SelectExtremeVertex(sw, model, drawing, view, selection, data, vertices.Where(v => Math.Abs(v.Projected[axis] - min) < Tol).OrderByDescending(v => v.Projected[2]), null, "minimum", attempts, out nativeA);
                    if (a != null) b = SelectExtremeVertex(sw, model, drawing, view, selection, data, vertices.Where(v => Math.Abs(v.Projected[axis] - max) < Tol).OrderByDescending(v => v.Projected[2]), a, "maximum", attempts, out nativeB);
                    trace.Record("drawing_dimension_extreme_mapping", new { view = viewName, flat = view.IsFlatPatternView(), axis = axisName, minimumMapped = a != null, maximumMapped = b != null, attempts = attempts });
                    if (a == null || b == null) trace.Record("drawing_dimension_failed_mapping_entities", new { view = viewName, flat = view.IsFlatPatternView(), axis = axisName, visible = VisibleViewEntities(model, view, true) });
                    Need(a != null && b != null && selection.GetSelectedObjectCount2(-1) == 2, "NATIVE_EXTREME_VERTEX_VIEW_MAPPING_FAILED view=" + viewName + " flat=" + view.IsFlatPatternView() + " axis=" + axisName);
                    double[] outline = Numbers(view.GetOutline());
                    VariableTrial.RuntimeOwnership();
                    DisplayDimension display = (horizontal ? drawing.AddHorizontalDimension2((outline[0] + outline[2]) / 2, outline[1] - 0.012, 0) : drawing.AddVerticalDimension2(outline[2] + 0.012, (outline[1] + outline[3]) / 2, 0)) as DisplayDimension;
                    Need(display != null, "NATIVE_ASSOCIATIVE_DIMENSION_CREATION_FAILED");
                    try
                    {
                        object proof = DimensionProof(sw, model, display, expected, view, axisName, nativeA.Vertex, nativeB.Vertex);
                        trace.Record("drawing_dimension_verified", new { view = viewName, flat = view.IsFlatPatternView(), axis = axisName, proof = proof });
                        Annotation annotation = display.GetAnnotation() as Annotation;
                        try { return new { proof = proof, nativeEndpointsMetres = new[] { nativeA.Native, nativeB.Native }, expectedMetres = expected, annotationName = annotation.GetName() }; }
                        finally { Release(annotation); }
                    }
                    finally { Release(display); drawing.ClearSelection2(true); }
                }
                finally { Release(data); Release(selection); }
            }
            finally { Release(a); Release(b); }
        }
        finally { foreach (VertexPoint point in vertices) Release(point.Vertex); foreach (object body in bodies) Release(body); }
    }
    sealed class HolePoint
    {
        internal Edge Edge;
        internal Entity ViewEntity;
        internal double[] Native, Projected;
        internal double Radius;
        internal string Reference;
    }
    static object NativeHoles(ISldWorks sw, ModelDoc2 model, ModelDoc2 drawing, SwView view, List<object> dimensions, List<object> notes, DxfTrace trace)
    {
        VariableTrial.RuntimeOwnership();
        Need(view.IsFlatPatternView(), "HOLE_COORDINATE_TABLE_REQUIRES_NATIVE_FLAT_VIEW");
        Need(((DrawingDoc)drawing).ActivateView(view.GetName2()), "HOLE_SOURCE_DRAWING_VIEW_ACTIVATION_FAILED");
        ActivateConfiguration(model, view.ReferencedConfiguration, "HOLE_SOURCE_CONFIGURATION_FAILED");
        Need(model.ForceRebuild3(false), "HOLE_SOURCE_REBUILD_FAILED");
        Need(drawing.ForceRebuild3(false) && Configuration(model) == view.ReferencedConfiguration, "HOLE_VIEW_REBUILD_OR_CONFIGURATION_FAILED");
        var holes = new List<HolePoint>(); var points = new List<double[]>();
        object[] bodies = Items(((PartDoc)model).GetBodies2(0, false));
        MathTransform transform = view.ModelToViewTransform;
        try
        {
            Need(bodies.Length == 1, "ONE_FLAT_BODY_REQUIRED_FOR_HOLE_TABLE");
            double[] matrix = Numbers(transform.ArrayData); double scale = matrix[12];
            foreach (Vertex vertex in Items(((Body2)bodies[0]).GetVertices()).Cast<Vertex>())
                try { points.Add(Project(Numbers(vertex.GetPoint()), view)); } finally { Release(vertex); }
            Need(points.Count > 2 && scale > 0, "NATIVE_FLAT_EXTREMA_REQUIRED_FOR_HOLE_DATUM");
            double minX = points.Min(v => v[0]), minY = points.Min(v => v[1]);
            var circleCandidates = new List<object>(); int visibleEdgeCount = 0; object[] components = Items(view.GetVisibleComponents());
            try
            {
                Need(components.Length > 0, "HOLE_VISIBLE_COMPONENTS_REQUIRED");
                foreach (Component2 component in components)
                {
                    object[] visibleEdges = Items(view.GetVisibleEntities2(component, (int)swViewEntityType_e.swViewEntityType_Edge));
                    Need(visibleEdges.Length == view.GetVisibleEntityCount2(component, (int)swViewEntityType_e.swViewEntityType_Edge), "HOLE_VISIBLE_EDGE_COUNT_MISMATCH");
                    visibleEdgeCount += visibleEdges.Length;
                    foreach (object visibleEdge in visibleEdges)
                    {
                        VariableTrial.RuntimeOwnership(); bool retained = false; Curve curve = null; CurveParamData parameters = null; object native = null;
                        try
                        {
                            Need(visibleEdge is Entity && ((Entity)visibleEdge).GetType() == (int)swSelectType_e.swSelEDGES, "HOLE_VISIBLE_ENTITY_MUST_BE_EDGE");
                            native = model.Extension.GetCorrespondingEntity2(visibleEdge); Need(native is Edge, "HOLE_VISIBLE_EDGE_NATIVE_IDENTITY_REQUIRED"); Edge edge = (Edge)native;
                            curve = edge.GetCurve() as Curve; Need(curve != null, "HOLE_NATIVE_CURVE_REQUIRED"); if (!curve.IsCircle()) continue;
                            var candidate = new Dictionary<string, object> { { "component", component.Name2 }, { "viewEdgeType", ((Entity)visibleEdge).GetType() }, { "nativeReverseMapping", true }, { "accepted", false } }; circleCandidates.Add(candidate);
                            parameters = edge.GetCurveParams3(); Need(parameters != null, "HOLE_NATIVE_EDGE_PARAMETERS_REQUIRED");
                            double[] circle = Numbers(curve.CircleParams); Need(circle.Length == 7 && circle[6] > 0, "NATIVE_HOLE_CIRCLE_PARAMETERS_REQUIRED");
                            double length = curve.GetLength3(parameters.UMinValue, parameters.UMaxValue), sweep = Math.Abs(parameters.UMaxValue - parameters.UMinValue);
                            double[] axis = Enumerable.Range(0, 3).Select(i => circle[3] * matrix[i] + circle[4] * matrix[i + 3] + circle[5] * matrix[i + 6]).ToArray();
                            candidate["edgeParameterRange"] = new[] { parameters.UMinValue, parameters.UMaxValue }; candidate["curveAndEdgeSameSense"] = parameters.Sense;
                            candidate["circleParametersMetres"] = circle; candidate["nativeLengthMm"] = length * 1000; candidate["projectedAxis"] = axis;
                            bool full = Math.Abs(sweep - 2 * Math.PI) <= 0.000001 && Math.Abs(length - 2 * Math.PI * circle[6]) <= Tol
                                && SameNativePoint(Numbers(parameters.StartPoint).Select(v => v * 1000).ToArray(), Numbers(parameters.EndPoint).Select(v => v * 1000).ToArray(), 0.0001);
                            candidate["completeCircleVerified"] = full;
                            if (!full) { candidate["rejectedReason"] = "Native edge is an ARC, not a complete round hole"; continue; }
                            if (Math.Abs(axis[0]) > Tol || Math.Abs(axis[1]) > Tol || Math.Abs(Math.Abs(axis[2]) - 1) > Tol) { candidate["rejectedReason"] = "Native circle plane is not parallel to the target drawing view"; continue; }
                            object[] faces = Items(edge.GetTwoAdjacentFaces2());
                            try
                            {
                                Need(faces.Length == 2, "HOLE_NATIVE_SOLID_ADJACENCY_REQUIRED");
                                foreach (Face2 face in faces) { Body2 owner = face.GetBody() as Body2; try { Need(owner != null && sw.IsSame(owner, bodies[0]) == 1, "HOLE_NATIVE_EDGE_NOT_IN_EXACT_FLAT_BODY"); } finally { Release(owner); } }
                            }
                            finally { foreach (object face in faces) Release(face); }
                            double[] center = circle.Take(3).ToArray(), projected = Project(center, view);
                            byte[] reference = Items(model.Extension.GetPersistReference3(edge)).Select(Convert.ToByte).ToArray(); Need(reference.Length > 0, "NATIVE_HOLE_EDGE_PERSISTENT_REFERENCE_REQUIRED");
                            holes.Add(new HolePoint { Edge = edge, ViewEntity = (Entity)visibleEdge, Native = center, Projected = projected, Radius = circle[6], Reference = Convert.ToBase64String(reference) });
                            retained = true; candidate["accepted"] = true; candidate["nativeEdgeReference"] = Convert.ToBase64String(reference);
                        }
                        finally { Release(curve); Release(parameters); if (!retained) { Release(native); Release(visibleEdge); } }
                    }
                }
            }
            finally
            {
                trace.Record("drawing_hole_candidates", new { view = view.GetName2(), configuration = view.ReferencedConfiguration, visibleEdgeCount = visibleEdgeCount, acceptedNativeCircles = holes.Count, candidates = circleCandidates });
                foreach (object component in components) Release(component);
            }
            var unique = new List<HolePoint>();
            foreach (HolePoint hole in holes.OrderByDescending(v => v.Projected[2]))
                if (!unique.Any(v => Math.Abs(v.Projected[0] - hole.Projected[0]) < Tol && Math.Abs(v.Projected[1] - hole.Projected[1]) < Tol && Math.Abs(v.Radius - hole.Radius) < Tol)) unique.Add(hole);
            unique = unique.OrderBy(v => v.Projected[1]).ThenBy(v => v.Projected[0]).ToList();
            Need(unique.Count > 0 && unique.Count <= 32, "ONE_TO_32_REAL_FLAT_ROUND_HOLES_REQUIRED_FOR_A3_TABLE");
            var rows = new List<object>(); var text = new StringBuilder("展开圆孔坐标表 / mm\n原点：展开轮廓左下角\n编号     X          Y         直径\n");
            for (int i = 0; i < unique.Count; i++)
            {
                VariableTrial.RuntimeOwnership();
                HolePoint hole = unique[i]; double x = (hole.Projected[0] - minX) / scale * 1000, y = (hole.Projected[1] - minY) / scale * 1000, diameter = hole.Radius * 2000;
                string id = "H" + (i + 1).ToString(Invariant);
                rows.Add(new { id = id, xMm = x, yMm = y, diameterMm = diameter, nativeCenterMetres = hole.Native, projectedCenterMetres = hole.Projected, nativeEdgeReference = hole.Reference, exactNativeViewMapping = true });
                text.Append(id).Append("  ").Append(x.ToString("0.###", Invariant)).Append("  ").Append(y.ToString("0.###", Invariant)).Append("  ").Append(diameter.ToString("0.###", Invariant)).Append("\n");
            }
            // One genuinely associated diameter for each observed diameter group; all centres remain tabulated.
            var annotated = new List<double>();
            foreach (HolePoint hole in unique)
            {
                VariableTrial.RuntimeOwnership();
                if (annotated.Any(v => Math.Abs(v - hole.Radius) < Tol)) continue;
                Need(((DrawingDoc)drawing).ActivateView(view.GetName2()), "HOLE_DIMENSION_VIEW_ACTIVATION_FAILED"); drawing.ClearSelection2(true);
                Entity mapped = hole.ViewEntity;
                SelectionMgr selection = drawing.SelectionManager as SelectionMgr; SelectData data = selection.CreateSelectData(); DisplayDimension display = null;
                try
                {
                    Need(mapped is Entity, "MAPPED_NATIVE_HOLE_EDGE_REQUIRED"); data.View = view;
                    data.X = hole.Projected[0] + hole.Radius * scale; data.Y = hole.Projected[1]; data.Z = 0;
                    Need(((Entity)mapped).Select4(false, data) && selection.GetSelectedObjectCount2(-1) == 1, "ONE_NATIVE_HOLE_EDGE_SELECTION_REQUIRED");
                    object actual = selection.GetSelectedObject6(1, -1), actualNative = actual == null ? null : model.Extension.GetCorrespondingEntity2(actual); SwView actualView = selection.GetSelectedObjectsDrawingView2(1, -1);
                    try { Need(actual != null && sw.IsSame(actual, mapped) == 1 && actualView != null && sw.IsSame(actualView, view) == 1 && actualNative != null && sw.IsSame(actualNative, hole.Edge) == 1, "EXACT_SELECTED_NATIVE_HOLE_EDGE_AND_VIEW_REQUIRED"); }
                    finally { Release(actualNative); Release(actualView); Release(actual); }
                    int error = -1;
                    VariableTrial.RuntimeOwnership();
                    display = drawing.Extension.AddSpecificDimension(hole.Projected[0] + 0.013, hole.Projected[1] + 0.012, 0, (int)swDimensionType_e.swDiameterDimension, ref error) as DisplayDimension;
                    Need(display != null && error == 0 && display.Type2 == (int)swDimensionType_e.swDiameterDimension, "NATIVE_HOLE_DIAMETER_DIMENSION_CREATION_FAILED " + error);
                    Annotation annotation = display.GetAnnotation() as Annotation; Dimension dimension = display.GetDimension2(0) as Dimension;
                    try
                    {
                        Need(annotation != null && dimension != null && !annotation.IsDangling() && !display.GetOverride() && display.ShowDimensionValue && String.IsNullOrEmpty(display.GetLinkedText()), "REAL_ASSOCIATIVE_HOLE_DIMENSION_REQUIRED");
                        Need(dimension.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriven && Math.Abs(dimension.SystemValue - hole.Radius * 2) < Tol, "NATIVE_HOLE_DIAMETER_VALUE_FAILED");
                        object[] attached = Items(annotation.GetAttachedEntities3());
                        try
                        {
                            Need(attached.Length == 1 && attached[0] is Entity && ((Entity)attached[0]).GetType() == (int)swSelectType_e.swSelEDGES, "ONE_REAL_EDGE_HOLE_DIMENSION_ATTACHMENT_REQUIRED");
                            object native = model.Extension.GetCorrespondingEntity2(attached[0]);
                            try { Need(sw.IsSame(attached[0], hole.Edge) == 1 || native != null && sw.IsSame(native, hole.Edge) == 1, "EXACT_NATIVE_HOLE_DIMENSION_ATTACHMENT_REQUIRED"); }
                            finally { Release(native); }
                        }
                        finally { foreach (object entity in attached) Release(entity); }
                        object font = Font(annotation, 0.0027);
                        dimensions.Add(new { kind = "diameter", view = view.GetName2(), annotationName = annotation.GetName(), nativeName = dimension.FullName, expectedMm = hole.Radius * 2000, actualMm = dimension.SystemValue * 1000, nativeEdgeReference = hole.Reference, attachedNativeEdgeCount = 1, exactNativeAttachment = true, dangling = false, overrideEnabled = false, font = font });
                    }
                    finally { Release(dimension); Release(annotation); }
                    annotated.Add(hole.Radius);
                }
                finally { drawing.ClearSelection2(true); Release(display); Release(data); Release(selection); }
            }
            notes.Add(NoteAt(sw, drawing, text.ToString().TrimEnd(), 0.336, 0.247, 0.0023));
            return new { configuration = view.ReferencedConfiguration, view = view.GetName2(), datum = "native flat body projected minimum X/Y", datumProjectedMetres = new[] { minX, minY }, roundHoleCount = rows.Count, roundHoles = rows, nativeDiameterDimensionCount = annotated.Count, coordinateTableAssociative = false, coordinateTableDerivedFromNativeGeometry = true, limitation = "孔坐标表为本次原生圆边快照；后续模型变更须重新生成。仅包含朝向视图的完整圆孔，不把圆弧缺口当圆孔。" };
        }
        finally { foreach (HolePoint hole in holes) { Release(hole.Edge); Release(hole.ViewEntity); } foreach (object body in bodies) Release(body); Release(transform); }
    }
    static void Fit(SwView view, double cx, double cy)
    {
        VariableTrial.RuntimeOwnership();
        view.UseSheetScale = 0; view.ScaleDecimal = 1;
        double[] box = Numbers(view.GetOutline());
        Need(box.Length == 4 && box[2] > box[0] && box[3] > box[1], "NATIVE_VIEW_OUTLINE_REQUIRED");
        view.ScaleDecimal = Math.Min(1.0, Math.Min(0.125 / (box[2] - box[0]), 0.150 / (box[3] - box[1])));
        box = Numbers(view.GetOutline()); double[] position = Numbers(view.Position);
        view.Position = new[] { position[0] + cx - (box[0] + box[2]) / 2, position[1] + cy - (box[1] + box[3]) / 2 };
    }
    static object ViewProof(SwView view, string expectedModel)
    {
        Need(String.Equals(Path.GetFullPath(view.GetReferencedModelName()), expectedModel, StringComparison.OrdinalIgnoreCase), "DRAWING_VIEW_REFERENCES_WRONG_MODEL");
        var dimensions = new List<object>();
        foreach (DisplayDimension display in Items(view.GetDisplayDimensions()).Cast<DisplayDimension>())
        {
            VariableTrial.RuntimeOwnership();
            Annotation annotation = display.GetAnnotation() as Annotation; Dimension dimension = display.GetDimension2(0) as Dimension;
            try { Need(annotation != null && dimension != null && !annotation.IsDangling() && !display.GetOverride(), "SAVED_VIEW_HAS_INVALID_DIMENSION"); dimensions.Add(new { name = annotation.GetName(), actualMm = dimension.SystemValue * 1000, dangling = false, overrideEnabled = false }); }
            finally { Release(annotation); Release(dimension); Release(display); }
        }
        Need(dimensions.Count >= 2, "TWO_ASSOCIATIVE_DIMENSIONS_PER_VIEW_REQUIRED");
        MathTransform transform = view.ModelToViewTransform;
        try { return new { name = view.GetName2(), referencedModel = view.GetReferencedModelName(), configuration = view.ReferencedConfiguration, flatPattern = view.IsFlatPatternView(), useSheetScale = view.UseSheetScale, scale = view.ScaleDecimal, positionMetres = Numbers(view.Position), outlineMetres = Numbers(view.GetOutline()), transform = Numbers(transform.ArrayData), dimensions = dimensions }; }
        finally { Release(transform); }
    }

    static double[] TransformPoint(double[] point, double[] matrix)
    {
        Need(point.Length == 3 && matrix.Length == 16 && matrix[12] > 0, "NATIVE_POINT_TRANSFORM_REQUIRED");
        return Enumerable.Range(0, 3).Select(i => matrix[12] * (point[0] * matrix[i] + point[1] * matrix[i + 3] + point[2] * matrix[i + 6]) + matrix[i + 9]).ToArray();
    }
    static double Gap(double[] a, double[] b)
    {
        return Math.Sqrt(Enumerable.Range(0, a.Length).Sum(i => (a[i] - b[i]) * (a[i] - b[i])));
    }
    static double Number(List<Pair> pairs, DxfEntity entity, int code)
    {
        string text = Text(pairs, entity, code, "0"); double value;
        Need(Double.TryParse(text, NumberStyles.Float, Invariant, out value) && !Double.IsNaN(value) && !Double.IsInfinity(value), "DXF_FINITE_COORDINATE_REQUIRED");
        return value;
    }
    static double[] DxfPoint(List<Pair> pairs, DxfEntity entity, int code)
    {
        Need(Math.Abs(Number(pairs, entity, code + 20)) < 0.000001, "NONPLANAR_DXF_BEND_UNSUPPORTED");
        return new[] { Number(pairs, entity, code), Number(pairs, entity, code + 10) };
    }
    static double[] ExportPoint(double[] modelPoint, double[] alignment)
    {
        Need(alignment.Length == 12, "DXF_COMPARISON_BASIS_REQUIRED");
        return new[] {
            ((modelPoint[0] - alignment[0]) * alignment[3] + (modelPoint[1] - alignment[1]) * alignment[4] + (modelPoint[2] - alignment[2]) * alignment[5]) * 1000,
            ((modelPoint[0] - alignment[0]) * alignment[6] + (modelPoint[1] - alignment[1]) * alignment[7] + (modelPoint[2] - alignment[2]) * alignment[8]) * 1000 };
    }
    static object NativeBends(ISldWorks sw, ModelDoc2 model, SwView view, string input, string hand)
    {
        VariableTrial.RuntimeOwnership(); ActivateConfiguration(model, view.ReferencedConfiguration, "BEND_CONFIGURATION_REQUIRED");
        var rows = new List<Dictionary<string, object>>(); var dxfLines = new List<double[][]>(); var errors = new List<string>();
        DxfManifest manifest; bool linked = Exports.TryGetValue(input, out manifest), units = false;
        string modelHand = NativeHand(model), drawingHand = hand == "left" ? "L" : "R";
        bool sourceIdentity = linked && Hash(input) == manifest.SourceSha256 && manifest.CutGeometry != null
            && String.Equals(manifest.CutGeometry.modelPath, input, StringComparison.OrdinalIgnoreCase)
            && modelHand == drawingHand && manifest.CutGeometry.modelHand == drawingHand;
        if (!sourceIdentity) errors.Add("未验证：原生切割几何的零件路径、文件哈希或 HAND 与工程图不一致。");
        if (linked)
        {
            try
            {
                Need(File.Exists(manifest.Path) && Hash(manifest.Path) == manifest.Sha256, "RECORDED_DXF_CHANGED");
                var pairs = ReadDxf(manifest.Path); units = Units(pairs) == 4;
                foreach (DxfEntity entity in Entities(pairs))
                    if (entity.Type == "LINE" && Text(pairs, entity, 8, "0") == "BEND") dxfLines.Add(new[] { DxfPoint(pairs, entity, 10), DxfPoint(pairs, entity, 11) });
                if (!units) errors.Add("未验证：DXF $INSUNITS 不是毫米。");
            }
            catch (Exception error) { errors.Add("DXF_REFERENCE: " + error.Message); }
        }
        else errors.Add("未验证：缺同一输入原生导出的 DXF 关联记录。");
        var used = new HashSet<int>(); object[] native = Items(view.GetBendLines());
        try
        {
            foreach (object value in native)
            {
                VariableTrial.RuntimeOwnership();
                var row = new Dictionary<string, object> { { "id", "B" + (rows.Count + 1) }, { "direction", "UNKNOWN" }, { "positionVerified", false }, { "directionVerified", false }, { "dxfPositionMatched", false } }; rows.Add(row);
                SketchSegment segment = value as SketchSegment; SketchLine line = value as SketchLine;
                Sketch sketch = null; SketchPoint start = null, end = null; MathTransform modelToSketch = null, sketchToModel = null, modelToView = null;
                try
                {
                    Need(segment != null && line != null && segment.IsBendLine(), "NATIVE_BEND_SKETCH_LINE_REQUIRED");
                    int direction = line.GetBendLineDirection();
                    bool known = direction == (int)swBendLineDirection_e.swUpDirection || direction == (int)swBendLineDirection_e.swDownDirection;
                    row["directionEnum"] = direction; row["directionEnumName"] = Enum.GetName(typeof(swBendLineDirection_e), direction);
                    row["direction"] = direction == (int)swBendLineDirection_e.swUpDirection ? "UP" : direction == (int)swBendLineDirection_e.swDownDirection ? "DOWN" : "UNKNOWN";
                    row["nativeDirectionKnown"] = known; row["segmentId"] = Items(segment.GetID()).Select(Convert.ToInt32).ToArray();
                    start = line.GetStartPoint2() as SketchPoint; end = line.GetEndPoint2() as SketchPoint; sketch = segment.GetSketch();
                    Need(start != null && end != null && sketch != null, "NATIVE_BEND_ENDPOINTS_AND_SKETCH_REQUIRED");
                    double[] a = { start.X, start.Y, start.Z }, b = { end.X, end.Y, end.Z };
                    modelToSketch = sketch.ModelToSketchTransform; Need(modelToSketch != null, "BEND_MODEL_TO_SKETCH_REQUIRED"); sketchToModel = modelToSketch.IInverse();
                    modelToView = view.ModelToViewTransform; Need(sketchToModel != null && modelToView != null, "BEND_TRANSFORMS_REQUIRED");
                    double[] ms = Numbers(modelToSketch.ArrayData), sm = Numbers(sketchToModel.ArrayData), mv = Numbers(modelToView.ArrayData);
                    double[] ma = TransformPoint(a, sm), mb = TransformPoint(b, sm), va = TransformPoint(ma, mv), vb = TransformPoint(mb, mv);
                    double length = segment.GetLength();
                    bool transformsValid = Gap(TransformPoint(ma, ms), a) < Tol && Gap(TransformPoint(mb, ms), b) < Tol && Math.Abs(Gap(ma, mb) - length) < Tol;
                    bool ownedEntity = false; object resolved = null;
                    try
                    {
                        object reference = model.Extension.GetPersistReference3(segment); int referenceError;
                        if (reference != null) resolved = model.Extension.GetObjectByPersistReference3(reference, out referenceError);
                        else referenceError = -1;
                        ownedEntity = referenceError == 0 && resolved != null && sw.IsSame(resolved, segment) == 1;
                    }
                    catch (COMException) { ownedEntity = false; }
                    finally { Release(resolved); }
                    row["startSketchMetres"] = a; row["endSketchMetres"] = b; row["modelToSketchTransform"] = ms; row["sketchToModelTransform"] = sm;
                    row["startModelMetres"] = ma; row["endModelMetres"] = mb; row["modelToViewTransform"] = mv; row["viewScale"] = view.ScaleDecimal;
                    row["startSheetMm"] = va.Select(v => v * 1000).ToArray(); row["endSheetMm"] = vb.Select(v => v * 1000).ToArray(); row["lengthMm"] = length * 1000; row["transformRoundTripVerified"] = transformsValid;
                    row["modelEntityIdentityVerified"] = ownedEntity;
                    if (linked)
                    {
                        double[] da = ExportPoint(ma, manifest.Alignment), db = ExportPoint(mb, manifest.Alignment);
                        row["startDxfMm"] = da; row["endDxfMm"] = db;
                        int[] matches = Enumerable.Range(0, dxfLines.Count).Where(i => Math.Min(Math.Max(Gap(da, dxfLines[i][0]), Gap(db, dxfLines[i][1])), Math.Max(Gap(da, dxfLines[i][1]), Gap(db, dxfLines[i][0]))) <= 0.01).ToArray();
                        bool matched = units && transformsValid && ownedEntity && matches.Length == 1 && used.Add(matches[0]);
                        row["dxfCandidateIndices"] = matches; row["dxfPositionMatched"] = matched; row["positionVerified"] = matched;
                        row["directionVerified"] = matched && known; row["directionBasis"] = "Official native sketch direction; relative to the flat view, not inferred from endpoint order or DXF layer.";
                        if (matched) row["matchedDxfLine"] = new { index = matches[0], startMm = dxfLines[matches[0]][0], endMm = dxfLines[matches[0]][1] };
                        else row["unverifiedReason"] = "未验证：固定导出基准下未得到唯一、同位置的 DXF 折弯线匹配；未搜索平移/旋转/镜像。";
                    }
                }
                catch (Exception error) { row["unverifiedReason"] = error.Message; }
                finally { Release(start); Release(end); Release(sketch); Release(modelToSketch); Release(sketchToModel); Release(modelToView); }
            }
        }
        finally { foreach (object value in native) Release(value); }
        bool literalCandidatesMatched = rows.Count > 0 && rows.All(r => Convert.ToBoolean(r["positionVerified"]) && Convert.ToBoolean(r["directionVerified"])) && used.Count == dxfLines.Count && errors.Count == 0;
        // Legacy origin/orientation is established only by the independent full-CUT comparison.
        bool all = linked && manifest.ApiAlignmentSpecified && literalCandidatesMatched;
        return new { nativeLineCount = rows.Count, dxfLineCount = dxfLines.Count, lines = rows, dxfPath = linked ? manifest.Path : null, dxfSha256 = linked ? manifest.Sha256 : null,
            modelPath = input, modelSha256 = Hash(input), exportModelSha256 = linked ? manifest.SourceSha256 : null,
            modelHand = modelHand, drawingHand = drawingHand, exportModelHand = linked && manifest.CutGeometry != null ? manifest.CutGeometry.modelHand : null,
            sourceIdentityVerified = sourceIdentity, nativeCutGeometry = linked ? manifest.CutGeometry : null,
            exportConfiguration = linked && manifest.CutGeometry != null ? manifest.CutGeometry.configuration : null,
            nativeExportMethod = linked ? manifest.NativeExportMethod : null, apiAlignmentSpecified = linked && manifest.ApiAlignmentSpecified,
            alignment = linked && manifest.ApiAlignmentSpecified ? manifest.Alignment : null, xFlipped = (bool?)null, yFlipped = (bool?)null,
            comparisonAlignment = linked ? manifest.Alignment : null, comparisonXFlipped = false, comparisonYFlipped = false,
            comparisonCoordinateBasis = "Literal model X/Y in mm; no API alignment argument and no fitted registration.", units = "mm", matchingToleranceMm = 0.01,
            positionsAndDirectionsVerified = all, errors = errors, directionConvention = "swBendLineDirection_e in native flat-pattern view; no UP/DOWN layer rewrite",
            literalEndpointCandidatesMatched = literalCandidatesMatched,
            coordinateMappingVerified = false, coordinateMappingRequiresIndependentCutComparison = true, manufacturingOrientationVerified = false,
            limitations = new[] { "Legacy ExportFlatPatternView has no alignment argument. Literal endpoint comparisons above are candidates only; full native top-Z CUT geometry must independently establish a unique coordinate mapping.", "Independent Python endpoint comparison and cold reopen remain required.", "Native UP/DOWN is view-relative; a mirrored coordinate mapping does not establish the manufacturing side." } };
    }

    static void PdfProbeGuard(ISldWorks sw, SessionIdentity session, Dictionary<string, string> protectedFiles)
    {
        VariableTrial.RuntimeOwnership(); SameSession(sw, session);
        foreach (var item in protectedFiles) Need(File.Exists(item.Key) && Hash(item.Key) == item.Value, "PDF_PROBE_PROTECTED_FILE_CHANGED " + item.Key);
    }
    static bool PdfProbeProtectionFailure(Exception error)
    {
        return new[] { "TRIAL_CANCELLATION_REQUESTED", "NATIVE_SHARED_LOCK_LOST", "SHARED_LOCK_PATH_INVALID", "CLEANUP_SESSION_", "PDF_PROBE_PROTECTED_", "PDF_PROBE_NATIVE_INPUT_", "OUTPUT_OUTSIDE_TRIAL", "OUTPUT_REPARSE_PATH", "PRESERVE_EXISTING_OUTPUT", "EXACT_OUTPUT_DRAWING_ACTIVATION_FAILED" }
            .Any(v => error.Message.IndexOf(v, StringComparison.Ordinal) >= 0) || error.InnerException != null && PdfProbeProtectionFailure(error.InnerException);
    }
    static void PdfProbeReceipt(string path, object value)
    {
        using (var stream = new FileStream(NewPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            writer.Write(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(value));
    }
    static object PdfProbeDisplay(ISldWorks sw, ModelDoc2 drawing)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing;
        document.EditSheet(); Need(document.ActivateView(""), "PDF_PROBE_SHEET_CONTEXT_REQUIRED"); drawing.ClearSelection2(true);
        ModelView window = drawing.ActiveView as ModelView; SwView view = null; var views = new List<object>(); var notes = new List<object>();
        try
        {
            Need(window != null, "PDF_PROBE_GRAPHICS_WINDOW_REQUIRED"); window.EnableGraphicsUpdate = true;
            drawing.ViewZoomtofit2(); view = document.GetFirstView() as SwView; int index = 0;
            while (view != null)
            {
                SwView next = null;
                try
                {
                    next = view.GetNextView() as SwView;
                    if (index > 0) view.UpdateViewDisplayGeometry();
                    int points = 0, polylines = view.GetPolyLineCount5((short)1, out points);
                    views.Add(new { index = index, name = view.GetName2(), displayMode = view.GetDisplayMode2(), nativePolylineCount = polylines, nativePolylineDoubleCount = points });
                    object[] values = Items(view.GetNotes());
                    try
                    {
                        foreach (Note note in values.OfType<Note>())
                        {
                            Annotation annotation = note.GetAnnotation() as Annotation; TextFormat format = annotation == null ? null : annotation.GetTextFormat(0) as TextFormat;
                            try { notes.Add(new { viewIndex = index, view = view.GetName2(), text = note.GetText(), annotation = annotation == null ? null : annotation.GetName(), typeface = format == null ? null : format.TypeFaceName,
                                heightMetres = format == null ? (double?)null : format.CharHeight, visible = annotation == null ? (int?)null : annotation.Visible, dangling = annotation == null ? (bool?)null : annotation.IsDangling(), layer = annotation == null ? null : annotation.Layer }); }
                            finally { Release(format); Release(annotation); }
                        }
                    }
                    finally { foreach (object value in values) Release(value); }
                }
                finally { Release(view); view = null; }
                view = next; index++; Need(index < 100, "PDF_PROBE_VIEW_LIMIT");
            }
            drawing.GraphicsRedraw2(); drawing.WindowRedraw();
            return new { context = DrawingOutputContext(sw, drawing), graphicsUpdatesEnabled = window.EnableGraphicsUpdate, graphicsRedraw2Called = true, windowRedrawCalled = true, views = views, notes = notes };
        }
        finally { Release(view); Release(window); }
    }
    static Dictionary<string, object> PdfProbeExport(ISldWorks sw, SessionIdentity session, Dictionary<string, string> protectedFiles,
        ModelDoc2 drawing, string path, string arm, bool explicitSheets, DxfTrace trace, List<object> attempts, object noteRewriteEvidence = null)
    {
        PdfProbeGuard(sw, session, protectedFiles); path = NewPath(path); string receipt = NewPath(Path.ChangeExtension(path, ".json"));
        var row = new Dictionary<string, object> { { "arm", arm }, { "path", path }, { "pathLength", path.Length }, { "explicitExportData", explicitSheets }, { "stageAcceptance", false }, { "contentAcceptance", false } };
        if (noteRewriteEvidence != null) row["noteRewrite"] = noteRewriteEvidence;
        ExportPdfData pdf = null; var sheets = new List<Sheet>(); object[] returnedSheets = new object[0]; DispatchWrapper[] wrappers = null;
        bool? returned = null; int errors = -1, warnings = -1; var elapsed = Stopwatch.StartNew();
        try
        {
            row["before"] = PdfProbeDisplay(sw, drawing);
            if (explicitSheets)
            {
                pdf = sw.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as ExportPdfData;
                Need(pdf != null, "PDF_PROBE_EXPORT_DATA_REQUIRED"); pdf.ViewPdfAfterSaving = false; pdf.ExportAs3D = false;
                DrawingDoc document = (DrawingDoc)drawing; string[] names = Items(document.GetSheetNames()).Select(Convert.ToString).ToArray();
                foreach (string name in names) { Need(document.ActivateSheet(name), "PDF_PROBE_SHEET_ACTIVATION_FAILED"); Sheet sheet = document.GetCurrentSheet() as Sheet; sheets.Add(sheet); Need(sheet != null && sheet.GetName() == name, "PDF_PROBE_EXACT_SHEET_REQUIRED"); }
                wrappers = sheets.Select(v => new DispatchWrapper(v)).ToArray(); bool selected = pdf.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportSpecifiedSheets, wrappers);
                returnedSheets = Items(pdf.GetSheets()); string[] actual = returnedSheets.Select(v => v is Sheet ? ((Sheet)v).GetName() : Convert.ToString(v)).ToArray();
                row["sheetSelection"] = new { returned = selected, which = pdf.GetWhichSheets(), requested = names, readback = actual };
                Need(selected && pdf.GetWhichSheets() == (int)swExportDataSheetsToExport_e.swExportData_ExportSpecifiedSheets && names.OrderBy(v => v, StringComparer.Ordinal).SequenceEqual(actual.OrderBy(v => v, StringComparer.Ordinal)), "PDF_PROBE_EXACT_SHEET_SELECTION_FAILED");
            }
            drawing.ClearSelection2(true); row["saveContext"] = DrawingOutputContext(sw, drawing);
            errors = 0; warnings = 0; returned = drawing.Extension.SaveAs(path, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, pdf, ref errors, ref warnings);
            GC.KeepAlive(wrappers);
            row["after"] = DrawingOutputContext(sw, drawing);
        }
        catch (Exception error)
        {
            PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw; row["exception"] = error.ToString();
        }
        finally
        {
            foreach (object sheet in returnedSheets) Release(sheet); foreach (Sheet sheet in sheets) Release(sheet); Release(pdf);
        }
        PdfProbeGuard(sw, session, protectedFiles); elapsed.Stop();
        bool exists = File.Exists(path); row["returned"] = returned; row["errors"] = errors; row["warnings"] = warnings; row["elapsedMs"] = elapsed.ElapsedMilliseconds;
        row["exists"] = exists; row["length"] = exists ? new FileInfo(path).Length : 0; row["sha256"] = exists ? Hash(path) : null;
        row["nativeApiPass"] = returned == true && errors == 0; row["contentAcceptance"] = false;
        PdfProbeReceipt(receipt, row); trace.Record("pdf_probe_export", row); attempts.Add(row); return row;
    }
    static object PdfProbeNote(ISldWorks sw, ModelDoc2 drawing, string text, string typeface, double x, double y)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing; document.EditSheet(); Need(document.ActivateView(""), "PDF_PROBE_NOTE_SHEET_REQUIRED"); drawing.ClearSelection2(true);
        Note note = drawing.InsertNote(text) as Note; Need(note != null, "PDF_PROBE_NOTE_REQUIRED"); Annotation annotation = note.GetAnnotation() as Annotation; TextFormat format = null; SwView sheetView = null; object[] notes = new object[0];
        try
        {
            Need(annotation != null, "PDF_PROBE_NOTE_ANNOTATION_REQUIRED"); format = annotation.GetTextFormat(0) as TextFormat; Need(format != null, "PDF_PROBE_NOTE_FORMAT_REQUIRED");
            string originalTypeface = format.TypeFaceName;
            if (typeface != null) { format.TypeFaceName = typeface; format.CharHeight = 0.004; Need(annotation.SetTextFormat(0, false, format), "PDF_PROBE_NOTE_FONT_SET_FAILED"); Release(format); format = annotation.GetTextFormat(0) as TextFormat; }
            annotation.Visible = (int)swAnnotationVisibilityState_e.swAnnotationVisible; Need(annotation.SetPosition2(x, y, 0) && !annotation.IsDangling(), "PDF_PROBE_NOTE_POSITION_REQUIRED");
            sheetView = document.GetFirstView() as SwView; Need(sheetView != null, "PDF_PROBE_NOTE_SHEET_VIEW_REQUIRED"); notes = Items(sheetView.GetNotes());
            Need(notes.Count(v => v is Note && sw.IsSame(v, note) == 1) == 1 && NoteText(note.GetText()) == NoteText(text), "PDF_PROBE_NOTE_TEXT_AND_MEMBERSHIP_REQUIRED");
            Need(typeface == null || format != null && (format.TypeFaceName == typeface || typeface == "Microsoft YaHei" && format.TypeFaceName == "微软雅黑"), "PDF_PROBE_NOTE_FONT_READBACK_FAILED");
            return new { text = note.GetText(), requestedTypeface = typeface, originalTypeface = originalTypeface, actualTypeface = format.TypeFaceName, heightMetres = format.CharHeight, x = x, y = y, exactSheetMembership = true, dangling = false };
        }
        finally { foreach (object value in notes) Release(value); Release(sheetView); Release(format); Release(annotation); Release(note); drawing.ClearSelection2(true); }
    }
    sealed class PdfProbeNoteBackup
    {
        internal Note Note; internal string Text, Typeface; internal double Height;
        internal TextFormat Format; internal bool UseDocFormat; internal Dictionary<string, object> FormatValues;
    }
    static Dictionary<string, object> PdfProbeFormatValues(TextFormat format)
    {
        return new Dictionary<string, object> { { "typeface", format.TypeFaceName }, { "heightMetres", format.CharHeight }, { "heightPoints", format.CharHeightInPts }, { "heightSpecifiedInPoints", format.IsHeightSpecifiedInPts() },
            { "italic", format.Italic }, { "underline", format.Underline }, { "strikeout", format.Strikeout }, { "bold", format.Bold }, { "escapement", format.Escapement }, { "lineSpacing", format.LineSpacing },
            { "widthFactor", format.WidthFactor }, { "obliqueAngle", format.ObliqueAngle }, { "lineLength", format.LineLength }, { "vertical", format.Vertical }, { "backwards", format.BackWards }, { "upsideDown", format.UpsideDown }, { "charSpacingFactor", format.CharSpacingFactor } };
    }
    static bool PdfProbeSameFormat(Dictionary<string, object> expected, Dictionary<string, object> actual, string typeface, bool inputHeightOnly = false)
    {
        return expected.All(v =>
        {
            if (inputHeightOnly && v.Key == (Convert.ToBoolean(expected["heightSpecifiedInPoints"]) ? "heightMetres" : "heightPoints")) return true;
            return v.Key == "typeface" ? PdfProbeTypefaceMatches(Convert.ToString(actual[v.Key]), typeface) : v.Value is double
                ? Math.Abs(Convert.ToDouble(v.Value, Invariant) - Convert.ToDouble(actual[v.Key], Invariant)) < 1e-12 : Object.Equals(v.Value, actual[v.Key]);
        });
    }
    static bool PdfProbeTypefaceMatches(string actual, string requested)
    {
        return actual == requested || (actual == "SimSun" || actual == "宋体") && (requested == "SimSun" || requested == "宋体");
    }
    static void PdfProbeRestoreFormat(TextFormat format, Dictionary<string, object> values)
    {
        format.TypeFaceName = Convert.ToString(values["typeface"]); format.Italic = Convert.ToBoolean(values["italic"]); format.Underline = Convert.ToBoolean(values["underline"]);
        format.Strikeout = Convert.ToBoolean(values["strikeout"]); format.Bold = Convert.ToBoolean(values["bold"]); format.Escapement = Convert.ToDouble(values["escapement"], Invariant);
        format.LineSpacing = Convert.ToDouble(values["lineSpacing"], Invariant); format.WidthFactor = Convert.ToDouble(values["widthFactor"], Invariant); format.ObliqueAngle = Convert.ToDouble(values["obliqueAngle"], Invariant);
        format.LineLength = Convert.ToDouble(values["lineLength"], Invariant); format.Vertical = Convert.ToBoolean(values["vertical"]); format.BackWards = Convert.ToBoolean(values["backwards"]);
        format.UpsideDown = Convert.ToBoolean(values["upsideDown"]); format.CharSpacingFactor = Convert.ToDouble(values["charSpacingFactor"], Invariant);
        if (Convert.ToBoolean(values["heightSpecifiedInPoints"])) { format.CharHeight = Convert.ToDouble(values["heightMetres"], Invariant); format.CharHeightInPts = Convert.ToInt32(values["heightPoints"], Invariant); }
        else { format.CharHeightInPts = Convert.ToInt32(values["heightPoints"], Invariant); format.CharHeight = Convert.ToDouble(values["heightMetres"], Invariant); }
    }
    static List<PdfProbeNoteBackup> PdfProbeTrialNotes(ModelDoc2 drawing, bool preserveFormat = false)
    {
        SwView sheetView = ((DrawingDoc)drawing).GetFirstView() as SwView; Need(sheetView != null, "PDF_PROBE_ACTUAL_SHEET_REQUIRED"); var result = new List<PdfProbeNoteBackup>(); object[] notes = Items(sheetView.GetNotes());
        try
        {
            foreach (Note note in notes.OfType<Note>())
            {
                string text = note.GetText(); bool target = text.StartsWith("展开圆孔坐标表", StringComparison.Ordinal) || text.StartsWith("16029 ", StringComparison.Ordinal) && text.Contains("变量化独立试验")
                    || text.StartsWith("折弯态 /", StringComparison.Ordinal) || text.StartsWith("折弯态  1:", StringComparison.Ordinal) || text.StartsWith("真实展开态  1:", StringComparison.Ordinal)
                    || text.StartsWith("图名：16029 ", StringComparison.Ordinal) || text.StartsWith("用途：变量联动与输出验证", StringComparison.Ordinal);
                if (!target) continue;
                Annotation annotation = note.GetAnnotation() as Annotation; TextFormat format = annotation == null ? null : annotation.GetTextFormat(0) as TextFormat;
                try
                {
                    Need(format != null, "PDF_PROBE_ACTUAL_NOTE_FONT_REQUIRED");
                    var backup = new PdfProbeNoteBackup { Note = note, Text = text, Typeface = format.TypeFaceName, Height = format.CharHeight };
                    if (preserveFormat)
                    {
                        Need(annotation.GetTextFormatCount() == 1 && !annotation.GetUseDocTextFormat(0) && (backup.Typeface == "Microsoft YaHei" || backup.Typeface == "微软雅黑"), "PDF_PROBE_SINGLE_EXPLICIT_YAHEI_FORMAT_REQUIRED");
                        backup.UseDocFormat = annotation.GetUseDocTextFormat(0); backup.FormatValues = PdfProbeFormatValues(format); backup.Format = format; format = null;
                    }
                    result.Add(backup);
                }
                finally { Release(format); Release(annotation); }
            }
            Need(result.Count == 7, "PDF_PROBE_SEVEN_ACTUAL_TRIAL_NOTES_REQUIRED"); return result;
        }
        finally { foreach (object note in notes) if (!result.Any(v => Object.ReferenceEquals(v.Note, note))) Release(note); Release(sheetView); }
    }
    static void PdfProbeSetTrialNotes(List<PdfProbeNoteBackup> notes, bool restore, bool arial)
    {
        for (int i = 0; i < notes.Count; i++)
        {
            PdfProbeNoteBackup backup = notes[i]; Need(backup.Note.SetText(restore ? backup.Text : "WINNSEN PDF NOTE " + (i + 1).ToString(Invariant) + " ASCII ABC 123"), "PDF_PROBE_ACTUAL_NOTE_TEXT_SET_FAILED");
            Annotation annotation = backup.Note.GetAnnotation() as Annotation; TextFormat format = annotation.GetTextFormat(0) as TextFormat;
            try
            {
                string requested = arial ? "Arial" : backup.Typeface; format.TypeFaceName = requested; format.CharHeight = backup.Height; Need(annotation.SetTextFormat(0, false, format), "PDF_PROBE_ACTUAL_NOTE_FONT_SET_FAILED");
                Release(format); format = annotation.GetTextFormat(0) as TextFormat;
                Need(format != null && format.TypeFaceName == requested && NoteText(backup.Note.GetText()) == NoteText(restore ? backup.Text : "WINNSEN PDF NOTE " + (i + 1).ToString(Invariant) + " ASCII ABC 123"), "PDF_PROBE_ACTUAL_NOTE_FONT_AND_TEXT_READBACK_FAILED");
            }
            finally { Release(format); Release(annotation); }
        }
    }
    static object PdfProbeRewriteChineseNotes(List<PdfProbeNoteBackup> notes, string arm, bool simSun)
    {
        var rows = new List<object>();
        foreach (PdfProbeNoteBackup backup in notes)
        {
            Need(backup.Format != null, "PDF_PROBE_ORIGINAL_NOTE_FORMAT_REQUIRED"); PdfProbeRestoreFormat(backup.Format, backup.FormatValues);
            Need(PdfProbeSameFormat(backup.FormatValues, PdfProbeFormatValues(backup.Format), backup.Typeface), "PDF_PROBE_ORIGINAL_NOTE_FORMAT_CHANGED");
            Need(backup.Note.SetText(backup.Text), "PDF_PROBE_CHINESE_NOTE_TEXT_RESTORE_FAILED");
            Annotation annotation = backup.Note.GetAnnotation() as Annotation; TextFormat format = null;
            try
            {
                Need(annotation != null && annotation.SetTextFormat(0, backup.UseDocFormat, backup.Format), "PDF_PROBE_CHINESE_NOTE_FORMAT_RESTORE_FAILED");
                format = annotation.GetTextFormat(0) as TextFormat;
                Need(format != null && backup.Note.GetText() == backup.Text && annotation.GetTextFormatCount() == 1 && annotation.GetUseDocTextFormat(0) == backup.UseDocFormat
                    && PdfProbeSameFormat(backup.FormatValues, PdfProbeFormatValues(format), backup.Typeface), "PDF_PROBE_CHINESE_NOTE_ORIGINAL_READBACK_FAILED");
                object restored = PdfProbeFormatValues(format); string requested = simSun ? "SimSun" : backup.Typeface;
                if (simSun)
                {
                    format.TypeFaceName = requested; Need(annotation.SetTextFormat(0, backup.UseDocFormat, format), "PDF_PROBE_CHINESE_NOTE_FONT_SET_FAILED");
                    Release(format); format = null; format = annotation.GetTextFormat(0) as TextFormat;
                }
                Need(format != null && backup.Note.GetText() == backup.Text && annotation.GetTextFormatCount() == 1 && annotation.GetUseDocTextFormat(0) == backup.UseDocFormat
                    && PdfProbeSameFormat(backup.FormatValues, PdfProbeFormatValues(format), requested, simSun), "PDF_PROBE_CHINESE_NOTE_FINAL_READBACK_FAILED");
                rows.Add(new { text = backup.Note.GetText(), originalText = backup.Text, requestedTypeface = requested, typeface = format.TypeFaceName, heightMetres = format.CharHeight,
                    originalFormat = backup.FormatValues, restoredBeforeFontChange = restored, actualFormat = PdfProbeFormatValues(format), useDocumentFormat = annotation.GetUseDocTextFormat(0),
                    localizedTypefaceAliasAccepted = requested != format.TypeFaceName && PdfProbeTypefaceMatches(format.TypeFaceName, requested),
                    heightComparison = simSun ? (Convert.ToBoolean(backup.FormatValues["heightSpecifiedInPoints"]) ? "input-points-strict" : "input-system-metres-strict") : "both-height-readbacks-strict",
                    heightMetresDelta = format.CharHeight - Convert.ToDouble(backup.FormatValues["heightMetres"], Invariant), heightPointsDelta = format.CharHeightInPts - Convert.ToInt32(backup.FormatValues["heightPoints"], Invariant),
                    inputFormatReadbackMatched = true, fullFormatReadbackMatched = PdfProbeSameFormat(backup.FormatValues, PdfProbeFormatValues(format), requested) });
            }
            finally { Release(format); Release(annotation); }
        }
        return new { arm = arm, originalChinesePreserved = true, noteCount = rows.Count, onlyTypefaceChanged = simSun, notes = rows };
    }
    static object PdfProbeChineseFailureReadback(List<PdfProbeNoteBackup> notes, bool simSun)
    {
        var rows = new List<object>();
        foreach (PdfProbeNoteBackup backup in notes)
        {
            string requested = simSun ? "SimSun" : backup.Typeface; var requestedFormat = new Dictionary<string, object>(backup.FormatValues); requestedFormat["typeface"] = requested;
            var row = new Dictionary<string, object> { { "originalText", backup.Text }, { "requestedText", backup.Text }, { "requestedTypeface", requested }, { "originalFormat", backup.FormatValues }, { "requestedFormat", requestedFormat } };
            Annotation annotation = null; TextFormat format = null;
            try
            {
                row["actualText"] = backup.Note.GetText(); row["textMatched"] = backup.Note.GetText() == backup.Text; annotation = backup.Note.GetAnnotation() as Annotation;
                Need(annotation != null, "PDF_PROBE_FAILURE_READBACK_ANNOTATION_REQUIRED"); format = annotation.GetTextFormat(0) as TextFormat; Need(format != null, "PDF_PROBE_FAILURE_READBACK_FORMAT_REQUIRED");
                var actualFormat = PdfProbeFormatValues(format); row["actualFormat"] = actualFormat; row["actualTypeface"] = format.TypeFaceName;
                row["formatCount"] = annotation.GetTextFormatCount(); row["useDocumentFormat"] = annotation.GetUseDocTextFormat(0);
                row["fullFormatMatched"] = PdfProbeSameFormat(backup.FormatValues, actualFormat, requested);
                row["inputFormatMatched"] = PdfProbeSameFormat(backup.FormatValues, actualFormat, requested, simSun);
                row["heightComparison"] = simSun ? (Convert.ToBoolean(backup.FormatValues["heightSpecifiedInPoints"]) ? "input-points-strict" : "input-system-metres-strict") : "both-height-readbacks-strict";
                row["heightMetresDelta"] = format.CharHeight - Convert.ToDouble(backup.FormatValues["heightMetres"], Invariant); row["heightPointsDelta"] = format.CharHeightInPts - Convert.ToInt32(backup.FormatValues["heightPoints"], Invariant);
                row["localizedTypefaceAliasAccepted"] = requested != format.TypeFaceName && PdfProbeTypefaceMatches(format.TypeFaceName, requested);
            }
            catch (Exception error) { row["readbackError"] = error.ToString(); }
            finally { Release(format); Release(annotation); }
            rows.Add(row);
        }
        return rows;
    }
    static string PdfProbeTextSha256(string text)
    {
        if (text == null) return null;
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }
    static string PdfProbeAsciiNoteText(int index) { return "WINNSEN PDF NOTE " + (index + 1).ToString(Invariant) + " ASCII ABC 123"; }
    static Dictionary<string, object> PdfProbeIsolationNoteReadback(PdfProbeNoteBackup backup, int index, string requestedText)
    {
        var row = new Dictionary<string, object> { { "noteIndex", index + 1 }, { "originalText", backup.Text }, { "originalTextSha256", PdfProbeTextSha256(backup.Text) },
            { "requestedText", requestedText }, { "requestedTextSha256", PdfProbeTextSha256(requestedText) }, { "requestedTypeface", backup.Typeface },
            { "originalFormat", backup.FormatValues }, { "requestedFormat", backup.FormatValues }, { "requestedUseDocumentFormat", backup.UseDocFormat }, { "readbackCompleted", false }, { "readbackMatched", false } };
        Annotation annotation = null; TextFormat format = null;
        try
        {
            string text = backup.Note.GetText(); row["actualText"] = text; row["actualTextSha256"] = PdfProbeTextSha256(text); row["textMatched"] = text == requestedText;
            annotation = backup.Note.GetAnnotation() as Annotation; Need(annotation != null, "PDF_PROBE_ISOLATION_READBACK_ANNOTATION_REQUIRED");
            format = annotation.GetTextFormat(0) as TextFormat; Need(format != null, "PDF_PROBE_ISOLATION_READBACK_FORMAT_REQUIRED");
            var actualFormat = PdfProbeFormatValues(format); int formatCount = annotation.GetTextFormatCount(); bool useDocumentFormat = annotation.GetUseDocTextFormat(0);
            bool fullFormatMatched = PdfProbeSameFormat(backup.FormatValues, actualFormat, backup.Typeface);
            row["actualFormat"] = actualFormat; row["actualTypeface"] = format.TypeFaceName; row["formatCount"] = formatCount; row["useDocumentFormat"] = useDocumentFormat;
            row["fullFormatReadbackMatched"] = fullFormatMatched; row["heightComparison"] = "both-height-readbacks-strict";
            row["heightMetresDelta"] = format.CharHeight - Convert.ToDouble(backup.FormatValues["heightMetres"], Invariant);
            row["heightPointsDelta"] = format.CharHeightInPts - Convert.ToInt32(backup.FormatValues["heightPoints"], Invariant);
            row["readbackCompleted"] = true; row["readbackMatched"] = text == requestedText && formatCount == 1 && useDocumentFormat == backup.UseDocFormat && fullFormatMatched;
        }
        catch (Exception error) { row["readbackError"] = error.ToString(); }
        finally { Release(format); Release(annotation); }
        return row;
    }
    static List<Dictionary<string, object>> PdfProbeIsolationReadback(List<PdfProbeNoteBackup> notes, int chineseNoteIndex)
    {
        return notes.Select((note, index) => PdfProbeIsolationNoteReadback(note, index, index == chineseNoteIndex ? note.Text : PdfProbeAsciiNoteText(index))).ToList();
    }
    static void PdfProbeIsolationWrite(PdfProbeNoteBackup backup, int index, string requestedText, string phase, List<object> writes)
    {
        var row = new Dictionary<string, object> { { "noteIndex", index + 1 }, { "phase", phase }, { "requestedText", requestedText }, { "requestedTextSha256", PdfProbeTextSha256(requestedText) },
            { "requestedFormat", backup.FormatValues }, { "completed", false } }; writes.Add(row);
        Annotation annotation = null;
        try
        {
            Need(backup.Format != null, "PDF_PROBE_ISOLATION_ORIGINAL_FORMAT_REQUIRED"); PdfProbeRestoreFormat(backup.Format, backup.FormatValues);
            bool restored = PdfProbeSameFormat(backup.FormatValues, PdfProbeFormatValues(backup.Format), backup.Typeface); row["originalFormatRestored"] = restored;
            Need(restored, "PDF_PROBE_ISOLATION_ORIGINAL_FORMAT_CHANGED"); bool textSet = backup.Note.SetText(requestedText); row["textSetReturned"] = textSet;
            Need(textSet, "PDF_PROBE_ISOLATION_NOTE_TEXT_SET_FAILED"); annotation = backup.Note.GetAnnotation() as Annotation;
            Need(annotation != null, "PDF_PROBE_ISOLATION_NOTE_ANNOTATION_REQUIRED"); bool formatSet = annotation.SetTextFormat(0, backup.UseDocFormat, backup.Format); row["formatSetReturned"] = formatSet;
            Need(formatSet, "PDF_PROBE_ISOLATION_NOTE_FORMAT_SET_FAILED"); var readback = PdfProbeIsolationNoteReadback(backup, index, requestedText); row["readback"] = readback;
            Need(Convert.ToBoolean(readback["readbackMatched"]), "PDF_PROBE_ISOLATION_NOTE_READBACK_FAILED"); row["completed"] = true;
        }
        catch (Exception error) { row["error"] = error.ToString(); throw; }
        finally { Release(annotation); }
    }
    static void PdfProbeRewriteIsolatedChineseNote(List<PdfProbeNoteBackup> notes, int chineseNoteIndex, Dictionary<string, object> evidence)
    {
        Need(notes.Count == 7 && chineseNoteIndex >= 0 && chineseNoteIndex < notes.Count, "PDF_PROBE_ISOLATION_SEVEN_NOTES_AND_INDEX_REQUIRED");
        evidence["phase"] = "ascii-reset"; var resetWrites = new List<object>(); evidence["asciiResetWriteAttempts"] = resetWrites;
        for (int index = 0; index < notes.Count; index++) PdfProbeIsolationWrite(notes[index], index, PdfProbeAsciiNoteText(index), "ascii-reset", resetWrites);
        var reset = PdfProbeIsolationReadback(notes, -1); evidence["asciiResetNotes"] = reset;
        Need(reset.All(v => Convert.ToBoolean(v["readbackMatched"])), "PDF_PROBE_ISOLATION_ALL_ASCII_READBACK_FAILED");
        evidence["everyArmBeginsWithAllAscii"] = true;
        evidence["phase"] = "single-chinese-restore"; var restoreWrites = new List<object>(); evidence["singleChineseWriteAttempts"] = restoreWrites;
        PdfProbeIsolationWrite(notes[chineseNoteIndex], chineseNoteIndex, notes[chineseNoteIndex].Text, "single-chinese-restore", restoreWrites);
        var final = PdfProbeIsolationReadback(notes, chineseNoteIndex); evidence["notes"] = final;
        Need(final.All(v => Convert.ToBoolean(v["readbackMatched"])), "PDF_PROBE_ISOLATION_FINAL_SEVEN_NOTE_READBACK_FAILED");
        evidence["phase"] = "completed"; evidence["completed"] = true; evidence["originalTypefaceAndFullFormatPreserved"] = true;
    }
    static bool PdfProbeLocalizedTypefaceMatches(string actual, string requested)
    {
        return actual == requested || (actual == "Microsoft YaHei" || actual == "微软雅黑") && (requested == "Microsoft YaHei" || requested == "微软雅黑")
            || (actual == "HYSWLongFangSong" || actual == "汉仪长仿宋体") && (requested == "HYSWLongFangSong" || requested == "汉仪长仿宋体");
    }
    static List<Dictionary<string, object>> PdfProbeLocalizedFontReadback(List<PdfProbeNoteBackup> notes, string requestedTypeface)
    {
        var rows = PdfProbeIsolationReadback(notes, 0); PdfProbeNoteBackup selected = notes[0]; var row = rows[0];
        var requestedFormat = new Dictionary<string, object>(selected.FormatValues); requestedFormat["typeface"] = requestedTypeface;
        row["requestedTypeface"] = requestedTypeface; row["requestedFormat"] = requestedFormat; row["readbackMatched"] = false;
        row["heightComparison"] = Convert.ToBoolean(selected.FormatValues["heightSpecifiedInPoints"]) ? "input-points-strict" : "input-system-metres-strict";
        if (Convert.ToBoolean(row["readbackCompleted"]))
        {
            string actualTypeface = Convert.ToString(row["actualTypeface"]); bool nameMatched = PdfProbeLocalizedTypefaceMatches(actualTypeface, requestedTypeface);
            var actualFormat = (Dictionary<string, object>)row["actualFormat"]; var nonTypefaceFormat = new Dictionary<string, object>(actualFormat); nonTypefaceFormat["typeface"] = selected.Typeface;
            bool inputFormatMatched = PdfProbeSameFormat(selected.FormatValues, nonTypefaceFormat, selected.Typeface, true);
            row["exactNameMatched"] = actualTypeface == requestedTypeface; row["verifiedTypefaceNameMatched"] = nameMatched;
            row["localizedTypefaceAliasAccepted"] = actualTypeface != requestedTypeface && nameMatched; row["nonTypefaceInputFormatReadbackMatched"] = inputFormatMatched;
            row["fullFormatReadbackMatched"] = nameMatched && PdfProbeSameFormat(selected.FormatValues, nonTypefaceFormat, selected.Typeface);
            row["inputFormatReadbackMatched"] = nameMatched && inputFormatMatched;
            bool inputPoints = Convert.ToBoolean(selected.FormatValues["heightSpecifiedInPoints"]);
            row["inputHeightUnit"] = inputPoints ? "points" : "metres"; row["derivedHeightUnit"] = inputPoints ? "metres" : "points";
            row["inputHeightDelta"] = row[inputPoints ? "heightPointsDelta" : "heightMetresDelta"]; row["derivedHeightDelta"] = row[inputPoints ? "heightMetresDelta" : "heightPointsDelta"];
            row["readbackMatched"] = Convert.ToBoolean(row["textMatched"]) && Convert.ToInt32(row["formatCount"]) == 1
                && Convert.ToBoolean(row["useDocumentFormat"]) == selected.UseDocFormat && nameMatched && inputFormatMatched;
        }
        return rows;
    }
    static void PdfProbeRewriteLocalizedFont(List<PdfProbeNoteBackup> notes, string requestedTypeface, Dictionary<string, object> evidence)
    {
        var reset = new Dictionary<string, object> { { "phase", "ascii-reset" }, { "everyArmBeginsWithAllAscii", false }, { "completed", false } };
        evidence["phase"] = "reset-and-original-chinese"; evidence["resetAndOriginalChinese"] = reset; PdfProbeRewriteIsolatedChineseNote(notes, 0, reset);
        evidence["phase"] = "selected-typeface"; Annotation annotation = null; TextFormat format = null;
        try
        {
            PdfProbeNoteBackup selected = notes[0]; annotation = selected.Note.GetAnnotation() as Annotation;
            Need(annotation != null, "PDF_PROBE_LOCALIZED_NOTE_ANNOTATION_REQUIRED"); format = annotation.GetTextFormat(0) as TextFormat;
            Need(format != null, "PDF_PROBE_LOCALIZED_NOTE_FORMAT_REQUIRED"); format.TypeFaceName = requestedTypeface;
            bool returned = annotation.SetTextFormat(0, selected.UseDocFormat, format); evidence["fontSetReturned"] = returned;
            Need(returned, "PDF_PROBE_LOCALIZED_NOTE_FONT_SET_FAILED");
        }
        finally { Release(format); Release(annotation); }
        evidence["phase"] = "final-readback"; var final = PdfProbeLocalizedFontReadback(notes, requestedTypeface); evidence["notes"] = final;
        evidence["selectedActualTypeface"] = final[0].ContainsKey("actualTypeface") ? final[0]["actualTypeface"] : null;
        Need(final.All(v => Convert.ToBoolean(v["readbackMatched"])), "PDF_PROBE_LOCALIZED_FINAL_SEVEN_NOTE_READBACK_FAILED");
        evidence["phase"] = "completed"; evidence["completed"] = true;
    }
    public static object PdfProbe(ISldWorks sw, string drawingPath, string outputDir, bool chineseFontOnly = false, bool chineseNoteIsolation = false, bool localizedFontProbe = false)
    {
        VariableTrial.RuntimeOwnership(); Need(!(chineseFontOnly && chineseNoteIsolation || chineseFontOnly && localizedFontProbe || chineseNoteIsolation && localizedFontProbe), "PDF_PROBE_DIAGNOSTIC_MODES_MUTUALLY_EXCLUSIVE"); Need(sw != null, "PDF_PROBE_OWNED_SESSION_REQUIRED"); SessionIdentity session = CaptureSession(sw);
        string cadDirectory = Local(Path.GetDirectoryName(VariableTrial.OutputRoot)); drawingPath = Local(drawingPath); outputDir = Local(outputDir);
        Need(drawingPath.StartsWith(cadDirectory + "\\", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(drawingPath).Equals(".SLDDRW", StringComparison.OrdinalIgnoreCase) && File.Exists(drawingPath), "PDF_PROBE_TRIAL_DRAWING_REQUIRED");
        Need(String.Equals(outputDir, Path.Combine(cadDirectory, "pdf-probe"), StringComparison.OrdinalIgnoreCase), "PDF_PROBE_EXACT_OUTPUT_DIRECTORY_REQUIRED");
        Need(!Directory.Exists(outputDir) || !Directory.EnumerateFileSystemEntries(outputDir).Any(), "PDF_PROBE_FRESH_OUTPUT_DIRECTORY_REQUIRED");
        object[] initialDocuments = Items(sw.GetDocuments()); try { Need(initialDocuments.Length == 0, "PDF_PROBE_EMPTY_OWNED_SESSION_REQUIRED"); } finally { foreach (object document in initialDocuments) Release(document); }
        string template = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
        Need(!String.IsNullOrWhiteSpace(template) && Path.IsPathRooted(template) && !template.StartsWith(@"\\", StringComparison.Ordinal) && new DriveInfo(Path.GetPathRoot(template)).DriveType == DriveType.Fixed && File.Exists(template), "PDF_PROBE_EXISTING_LOCAL_TEMPLATE_REQUIRED");
        var nativeFiles = Directory.GetFiles(cadDirectory, "*", SearchOption.AllDirectories).Where(v => new[] { ".SLDDRW", ".SLDPRT", ".SLDASM" }.Contains(Path.GetExtension(v).ToUpperInvariant())).ToDictionary(v => v, Hash, StringComparer.OrdinalIgnoreCase);
        var protectedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { drawingPath, Hash(drawingPath) }, { template, Hash(template) } };
        bool oldVisible = sw.Visible, oldUserControl = sw.UserControl; Directory.CreateDirectory(outputDir); string shortDirectory = Local(Path.Combine(outputDir, "p")); Directory.CreateDirectory(shortDirectory);
        string longDirectory = Local(Path.Combine(outputDir, "path-comparison", "original-drawing-pdf-export")); Directory.CreateDirectory(longDirectory);
        string templateCopy = NewPath(Path.Combine(outputDir, "probe-template.drwdot")); File.Copy(template, templateCopy, false); protectedFiles.Add(templateCopy, Hash(templateCopy));
        var trace = new DxfTrace(Path.Combine(outputDir, "probe")); var attempts = new List<object>(); var stages = new List<object>(); var cleanupErrors = new List<Exception>();
        ModelDoc2 actual = null, minimal = null; Exception failure = null; bool actualBaselineFailed = false, minimalPassed = false, chineseSequenceCompleted = false, noteIsolationSequenceCompleted = false, localizedFontSequenceCompleted = false;
        string mode = localizedFontProbe ? "localized-font-name-four-arms" : chineseNoteIsolation ? "chinese-note-isolation-seven-arms" : chineseFontOnly ? "chinese-font-A1-B-A2" : "full-matrix";
        int? expectedAttemptCount = localizedFontProbe ? 4 : chineseNoteIsolation ? 7 : chineseFontOnly ? (int?)3 : null;
        trace.Record("pdf_probe_begin", new { drawing = drawingPath, drawingSha256 = protectedFiles[drawingPath], originalTemplate = template, templateSha256 = protectedFiles[template], templateCopy = templateCopy, outputDirectory = outputDir, mode = mode, expectedAttemptCount = expectedAttemptCount, stageAcceptance = false });
        try
        {
            sw.UserControl = true; sw.Visible = true; PdfProbeGuard(sw, session, protectedFiles);
            try
            {
                int errors = 0, warnings = 0; actual = sw.OpenDoc6(drawingPath, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2;
                trace.Record("pdf_probe_cold_open", new { path = drawingPath, returnedDocument = actual != null, errors = errors, warnings = warnings });
                Need(actual is DrawingDoc && errors == 0 && !actual.IsOpenedViewOnly() && String.Equals(actual.GetPathName(), drawingPath, StringComparison.OrdinalIgnoreCase), "PDF_PROBE_EXACT_COLD_DRAWING_OPEN_FAILED");
                if (!chineseFontOnly && !chineseNoteIsolation && !localizedFontProbe) foreach (bool explicitSheets in new[] { true, false })
                    foreach (bool shortPath in new[] { false, true })
                    {
                        string arm = "actual-" + (shortPath ? "short" : "long") + (explicitSheets ? "-explicit" : "-null");
                        var row = PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortPath ? shortDirectory : longDirectory, arm + ".pdf"), arm, explicitSheets, trace, attempts);
                        actualBaselineFailed |= !Convert.ToBoolean(row["nativeApiPass"]);
                    }
                List<PdfProbeNoteBackup> noteBackups = null;
                try
                {
                    noteBackups = PdfProbeTrialNotes(actual, chineseFontOnly || chineseNoteIsolation || localizedFontProbe);
                    if (localizedFontProbe)
                    {
                        string[] typefaces = new[] { "Microsoft YaHei", "汉仪长仿宋体", "HYSWLongFangSong", "微软雅黑" };
                        for (int index = 0; index < typefaces.Length; index++)
                        {
                            PdfProbeGuard(sw, session, protectedFiles); string arm = "actual-localized-font-" + (index + 1).ToString("00", Invariant);
                            var rewritten = new Dictionary<string, object> { { "arm", arm }, { "selectedChineseNoteIndex", 1 }, { "selectedRequestedTypeface", typefaces[index] },
                                { "noteCount", noteBackups.Count }, { "textHashEncoding", "UTF-8" }, { "phase", "reset-and-original-chinese" }, { "completed", false }, { "stageAcceptance", false }, { "contentAcceptance", false } };
                            try { PdfProbeRewriteLocalizedFont(noteBackups, typefaces[index], rewritten); }
                            catch (Exception error)
                            {
                                PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw;
                                bool changingFont = Convert.ToString(rewritten["phase"]) != "reset-and-original-chinese";
                                var reset = rewritten.ContainsKey("resetAndOriginalChinese") ? (Dictionary<string, object>)rewritten["resetAndOriginalChinese"] : null;
                                bool chineseRequested = changingFont || reset != null && Convert.ToString(reset["phase"]) != "ascii-reset";
                                var failedRewrite = new { arm = arm, completed = false, stageAcceptance = false, contentAcceptance = false, error = error.ToString(), noteRewrite = rewritten,
                                    notes = changingFont ? PdfProbeLocalizedFontReadback(noteBackups, typefaces[index]) : PdfProbeIsolationReadback(noteBackups, chineseRequested ? 0 : -1) };
                                PdfProbeReceipt(Path.Combine(shortDirectory, arm + "-note-rewrite-failure.json"), failedRewrite); trace.Record("pdf_probe_localized_font_rewrite_failure", failedRewrite); throw;
                            }
                            stages.Add(rewritten); trace.Record("pdf_probe_localized_font_rewrite", rewritten);
                            PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortDirectory, arm + ".pdf"), arm, true, trace, attempts, rewritten);
                        }
                        localizedFontSequenceCompleted = attempts.Count == 4;
                    }
                    else if (chineseNoteIsolation)
                    {
                        for (int index = 0; index < noteBackups.Count; index++)
                        {
                            PdfProbeGuard(sw, session, protectedFiles); string arm = "actual-cjk-note-" + (index + 1).ToString("00", Invariant) + "-original-font";
                            var rewritten = new Dictionary<string, object> { { "arm", arm }, { "selectedChineseNoteIndex", index + 1 }, { "noteCount", noteBackups.Count }, { "textHashEncoding", "UTF-8" },
                                { "phase", "ascii-reset" }, { "everyArmBeginsWithAllAscii", false }, { "completed", false }, { "stageAcceptance", false }, { "contentAcceptance", false } };
                            try { PdfProbeRewriteIsolatedChineseNote(noteBackups, index, rewritten); }
                            catch (Exception error)
                            {
                                PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw;
                                int requestedChineseIndex = Convert.ToString(rewritten["phase"]) == "ascii-reset" ? -1 : index;
                                var failedRewrite = new { arm = arm, completed = false, stageAcceptance = false, contentAcceptance = false, error = error.ToString(), noteRewrite = rewritten,
                                    requestedChineseNoteIndex = requestedChineseIndex < 0 ? (int?)null : requestedChineseIndex + 1, notes = PdfProbeIsolationReadback(noteBackups, requestedChineseIndex) };
                                PdfProbeReceipt(Path.Combine(shortDirectory, arm + "-note-rewrite-failure.json"), failedRewrite); trace.Record("pdf_probe_note_isolation_rewrite_failure", failedRewrite); throw;
                            }
                            stages.Add(rewritten); trace.Record("pdf_probe_note_isolation_rewrite", rewritten);
                            PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortDirectory, arm + ".pdf"), arm, true, trace, attempts, rewritten);
                        }
                        noteIsolationSequenceCompleted = attempts.Count == 7;
                    }
                    else if (chineseFontOnly)
                    {
                        foreach (string arm in new[] { "actual-cjk-A1-original-font", "actual-cjk-B-simsun", "actual-cjk-A2-original-font" })
                        {
                            PdfProbeGuard(sw, session, protectedFiles); object rewritten;
                            try { rewritten = PdfProbeRewriteChineseNotes(noteBackups, arm, arm == "actual-cjk-B-simsun"); }
                            catch (Exception error)
                            {
                                PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw;
                                var failedRewrite = new { arm = arm, completed = false, stageAcceptance = false, contentAcceptance = false, error = error.ToString(),
                                    notes = PdfProbeChineseFailureReadback(noteBackups, arm == "actual-cjk-B-simsun") };
                                PdfProbeReceipt(Path.Combine(shortDirectory, arm + "-note-rewrite-failure.json"), failedRewrite); trace.Record("pdf_probe_chinese_note_rewrite_failure", failedRewrite); throw;
                            }
                            stages.Add(rewritten); trace.Record("pdf_probe_chinese_note_rewrite", rewritten);
                            PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortDirectory, arm + ".pdf"), arm, true, trace, attempts, rewritten);
                        }
                        chineseSequenceCompleted = true;
                    }
                    else
                    {
                        PdfProbeSetTrialNotes(noteBackups, false, false);
                        PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortDirectory, "actual-ascii-original-font.pdf"), "actual-ascii-original-font", true, trace, attempts);
                        PdfProbeSetTrialNotes(noteBackups, false, true);
                        PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortDirectory, "actual-ascii-arial.pdf"), "actual-ascii-arial", true, trace, attempts);
                    }
                }
                finally
                {
                    if (noteBackups != null)
                    {
                        try { if (chineseFontOnly || chineseNoteIsolation || localizedFontProbe) PdfProbeRewriteChineseNotes(noteBackups, "cleanup-original-font", false); else PdfProbeSetTrialNotes(noteBackups, true, false); }
                        finally { foreach (var note in noteBackups) { Release(note.Format); Release(note.Note); } }
                    }
                }
            }
            catch (Exception error) { PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw; chineseSequenceCompleted = false; noteIsolationSequenceCompleted = false; localizedFontSequenceCompleted = false; stages.Add(new { stage = "actual-drawing", error = error.ToString() }); trace.Record("pdf_probe_stage_failure", stages.Last()); }
            if (!chineseFontOnly && !chineseNoteIsolation && !localizedFontProbe) try
            {
                PdfProbeGuard(sw, session, protectedFiles); minimal = sw.NewDocument(templateCopy, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297) as ModelDoc2; Need(minimal is DrawingDoc, "PDF_PROBE_MINIMAL_DRAWING_REQUIRED");
                ActivateOutputDrawing(sw, minimal); DrawingDoc document = (DrawingDoc)minimal; Sheet sheet = document.GetCurrentSheet() as Sheet;
                try { Need(sheet != null, "PDF_PROBE_MINIMAL_SHEET_REQUIRED"); string previousFormat = sheet.GetTemplateName(); bool setup = document.SetupSheet5(sheet.GetName(), (int)swDwgPaperSizes_e.swDwgPaperA3size, (int)swDwgTemplates_e.swDwgTemplateNone, 1, 1, false, "", 0.420, 0.297, "", true); sheet.SheetFormatVisible = false; trace.Record("pdf_probe_remove_minimal_format", new { returned = setup, previousTemplate = previousFormat, requestedTemplate = "", externalTemplateAccessRequested = false }); Need(setup, "PDF_PROBE_BLANK_SHEET_SETUP_FAILED"); }
                finally { Release(sheet); }
                document.EditSheet(); Need(document.ActivateView(""), "PDF_PROBE_LINE_SHEET_REQUIRED"); document.EditSketch(); SketchManager sketches = minimal.SketchManager; Sketch sketch = sketches.ActiveSketch; SketchSegment line = null;
                try { Need(sketch != null, "PDF_PROBE_ACTIVE_SHEET_SKETCH_REQUIRED"); line = sketches.CreateLine(0.050, 0.150, 0, 0.150, 0.150, 0); Need(line != null && Math.Abs(line.GetLength() - 0.100) < Tol, "PDF_PROBE_NATIVE_100MM_LINE_REQUIRED"); stages.Add(new { stage = "minimal-native-line", startMetres = new[] { 0.050, 0.150, 0.0 }, endMetres = new[] { 0.150, 0.150, 0.0 }, actualLengthMetres = line.GetLength(), layer = line.Layer }); trace.Record("pdf_probe_native_line", stages.Last()); }
                finally { Release(line); Release(sketch); Release(sketches); document.EditSheet(); }
                foreach (string stage in new[] { "line-only", "ascii-default", "yahei-cjk" })
                {
                    try
                    {
                        if (stage == "ascii-default") { object note = PdfProbeNote(sw, minimal, "WINNSEN PDF DEFAULT ASCII ABC 123", null, 0.050, 0.220); stages.Add(note); trace.Record("pdf_probe_minimal_note", note); }
                        if (stage == "yahei-cjk")
                        {
                            foreach (var text in new[] { "WINNSEN YAHEI ASCII ABC 123", "雅黑中文 门板 圆孔 折弯 123" }) { object note = PdfProbeNote(sw, minimal, text, "Microsoft YaHei", 0.050, text.StartsWith("WINNSEN", StringComparison.Ordinal) ? 0.200 : 0.180); stages.Add(note); trace.Record("pdf_probe_minimal_note", note); }
                        }
                    }
                    catch (Exception error) { PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw; stages.Add(new { stage = stage, error = error.ToString() }); trace.Record("pdf_probe_stage_failure", stages.Last()); }
                    foreach (bool explicitSheets in new[] { true, false })
                    {
                        string arm = "minimal-" + stage + (explicitSheets ? "-explicit" : "-null"); var row = PdfProbeExport(sw, session, protectedFiles, minimal, Path.Combine(shortDirectory, arm + ".pdf"), arm, explicitSheets, trace, attempts);
                        minimalPassed |= Convert.ToBoolean(row["nativeApiPass"]);
                    }
                }
            }
            catch (Exception error) { PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw; stages.Add(new { stage = "minimal-drawing", error = error.ToString() }); trace.Record("pdf_probe_stage_failure", stages.Last()); }
            if (!chineseFontOnly && !chineseNoteIsolation && !localizedFontProbe && actual != null && actualBaselineFailed && minimalPassed)
            {
                try
                {
                    PdfProbeGuard(sw, session, protectedFiles); ActivateOutputDrawing(sw, actual); DrawingDoc document = (DrawingDoc)actual; Sheet sheet = document.GetCurrentSheet() as Sheet;
                    try { Need(sheet != null, "PDF_PROBE_FORMAT_ISOLATION_SHEET_REQUIRED"); string previous = sheet.GetTemplateName(); bool setup = document.SetupSheet5(sheet.GetName(), (int)swDwgPaperSizes_e.swDwgPaperA3size, (int)swDwgTemplates_e.swDwgTemplateNone, 1, 1, false, "", 0.420, 0.297, "", false); sheet.SheetFormatVisible = false; trace.Record("pdf_probe_actual_format_isolation", new { returned = setup, previousTemplate = previous, requestedTemplate = "", diskSaveRequested = false }); Need(setup, "PDF_PROBE_FORMAT_ISOLATION_SETUP_FAILED"); }
                    finally { Release(sheet); }
                    foreach (bool explicitSheets in new[] { true, false }) { string arm = "actual-no-format" + (explicitSheets ? "-explicit" : "-null"); PdfProbeExport(sw, session, protectedFiles, actual, Path.Combine(shortDirectory, arm + ".pdf"), arm, explicitSheets, trace, attempts); }
                }
                catch (Exception error) { PdfProbeGuard(sw, session, protectedFiles); if (PdfProbeProtectionFailure(error)) throw; stages.Add(new { stage = "actual-format-isolation", error = error.ToString() }); trace.Record("pdf_probe_stage_failure", stages.Last()); }
            }
            PdfProbeGuard(sw, session, protectedFiles);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try
            {
                SameSession(sw, session); sw.UserControl = true; object[] documents = Items(sw.GetDocuments()); var closeNames = new List<string>();
                try { foreach (ModelDoc2 document in documents.OfType<ModelDoc2>().OrderByDescending(v => v is DrawingDoc)) { string path = document.GetPathName(); Need(String.IsNullOrEmpty(path) && minimal != null && sw.IsSame(document, minimal) == 1 || !String.IsNullOrEmpty(path) && Local(path).StartsWith(cadDirectory + "\\", StringComparison.OrdinalIgnoreCase), "PDF_PROBE_REFUSE_CLOSING_UNOWNED_DOCUMENT"); closeNames.Add(document.GetTitle()); } }
                finally { foreach (object document in documents) Release(document); }
                foreach (string name in closeNames) sw.CloseDoc(name);
                object[] remaining = Items(sw.GetDocuments()); try { Need(remaining.Length == 0, "PDF_PROBE_OWNED_DOCUMENTS_STILL_OPEN"); } finally { foreach (object document in remaining) Release(document); }
                sw.UserControl = oldUserControl; sw.Visible = oldVisible;
                Need(sw.UserControl == oldUserControl && sw.Visible == oldVisible, "PDF_PROBE_APPLICATION_STATE_RESTORE_FAILED");
            }
            catch (Exception error) { cleanupErrors.Add(error); }
            try { foreach (var item in nativeFiles) Need(File.Exists(item.Key) && Hash(item.Key) == item.Value, "PDF_PROBE_NATIVE_INPUT_DISK_CHANGED " + item.Key); foreach (var item in protectedFiles) Need(File.Exists(item.Key) && Hash(item.Key) == item.Value, "PDF_PROBE_PROTECTED_FILE_DISK_CHANGED " + item.Key); }
            catch (Exception error) { cleanupErrors.Add(error); }
            Release(minimal); Release(actual);
        }
        var report = new { schema = "16029-native-pdf-probe/v1", mode = mode, completed = failure == null && cleanupErrors.Count == 0 && (!chineseFontOnly || chineseSequenceCompleted) && (!chineseNoteIsolation || noteIsolationSequenceCompleted) && (!localizedFontProbe || localizedFontSequenceCompleted) && (!expectedAttemptCount.HasValue || attempts.Count == expectedAttemptCount.Value),
            expectedAttemptCount = expectedAttemptCount, actualAttemptCount = attempts.Count, chineseSequenceCompleted = chineseFontOnly ? (bool?)chineseSequenceCompleted : null,
            noteIsolationSequenceCompleted = chineseNoteIsolation ? (bool?)noteIsolationSequenceCompleted : null, localizedFontSequenceCompleted = localizedFontProbe ? (bool?)localizedFontSequenceCompleted : null,
            stageAcceptance = false, contentAcceptance = false, productionReleased = false,
            inputDrawing = drawingPath, inputSha256 = protectedFiles[drawingPath], inputDiskUnchanged = File.Exists(drawingPath) && Hash(drawingPath) == protectedFiles[drawingPath], sourceNativeFilesChecked = nativeFiles.Count,
            originalTemplate = template, templateSha256 = protectedFiles[template], templateUnchanged = Hash(template) == protectedFiles[template], originalVisible = oldVisible, originalUserControl = oldUserControl,
            attempts = attempts, stages = stages, failure = failure == null ? null : failure.ToString(), cleanupErrors = cleanupErrors.Select(v => v.ToString()).ToArray(), limitations = new[] { "Diagnostic only; every PDF still requires independent parsing and visual acceptance.", "No SLDDRW or SLDPRT was saved; sheet-format and text changes existed only in memory." } };
        PdfProbeReceipt(Path.Combine(outputDir, "probe-result.json"), report);
        if (trace.Errors.Count > 0) cleanupErrors.Add(new InvalidOperationException("PDF_PROBE_TRACE_WRITE_FAILED " + String.Join(" | ", trace.Errors)));
        if (failure != null) cleanupErrors.Insert(0, failure);
        if (cleanupErrors.Count > 0) throw new AggregateException("PDF_PROBE_FAILED; partial diagnostic receipts were preserved", cleanupErrors);
        return report;
    }
    static Dictionary<string, object> TrialFrameSketchReadback(ISldWorks sw, Sketch sketch)
    {
        var rows = new List<Dictionary<string, object>>(); var result = new Dictionary<string, object> { { "present", sketch != null }, { "segments", rows }, { "segmentCount", 0 }, { "lineCount", 0 }, { "endpointCount", 0 }, { "blockInstanceCount", 0 }, { "pictureCount", 0 } };
        if (sketch == null) return result;
        object[] segments = new object[0];
        try
        {
            segments = Items(sketch.GetSketchSegments());
            foreach (object value in segments)
            {
                SketchSegment segment = value as SketchSegment; SketchLine line = value as SketchLine; Sketch owner = null; SketchPoint start = null, end = null;
                try
                {
                    Need(segment != null, "TRIAL_FRAME_NATIVE_SKETCH_SEGMENT_REQUIRED"); owner = segment.GetSketch();
                    var row = new Dictionary<string, object> { { "isLine", line != null }, { "construction", segment.ConstructionGeometry }, { "layer", segment.Layer }, { "lengthMetres", segment.GetLength() }, { "exactSketchMembership", owner != null && sw.IsSame(owner, sketch) == 1 } };
                    if (line != null)
                    {
                        start = line.GetStartPoint2() as SketchPoint; end = line.GetEndPoint2() as SketchPoint; Need(start != null && end != null, "TRIAL_FRAME_NATIVE_LINE_ENDPOINTS_REQUIRED");
                        double[] a = { start.X, start.Y, start.Z }, b = { end.X, end.Y, end.Z };
                        row["startMetres"] = a; row["endMetres"] = b; row["startMm"] = a.Select(v => v * 1000).ToArray(); row["endMm"] = b.Select(v => v * 1000).ToArray();
                    }
                    rows.Add(row);
                }
                finally { Release(start); Release(end); Release(owner); }
            }
            result["segmentCount"] = segments.Length; result["lineCount"] = rows.Count(v => Convert.ToBoolean(v["isLine"])); result["endpointCount"] = 2 * Convert.ToInt32(result["lineCount"]);
            result["blockInstanceCount"] = sketch.GetSketchBlockInstanceCount(); result["pictureCount"] = sketch.GetSketchPictureCount(); return result;
        }
        finally { foreach (object value in segments) Release(value); }
    }
    static bool TrialFrameSketchEmpty(Dictionary<string, object> sketch)
    {
        return Convert.ToInt32(sketch["segmentCount"]) == 0 && Convert.ToInt32(sketch["blockInstanceCount"]) == 0 && Convert.ToInt32(sketch["pictureCount"]) == 0;
    }
    static Dictionary<string, object> TrialFrameSheetReadback(ISldWorks sw, ModelDoc2 drawing)
    {
        VariableTrial.RuntimeOwnership(); DrawingDoc document = (DrawingDoc)drawing; Sheet sheet = document.GetCurrentSheet() as Sheet; SwView sheetView = null; Sketch ordinary = null, format = null; object[] notes = new object[0];
        try
        {
            Need(sheet != null, "TRIAL_FRAME_CURRENT_SHEET_REQUIRED"); sheetView = document.GetFirstView() as SwView; Need(sheetView != null, "TRIAL_FRAME_SHEET_VIEW_REQUIRED");
            ordinary = sheetView.GetSketch() as Sketch; format = sheet.GetTemplateSketch(); notes = Items(sheetView.GetNotes()); double width = 0, height = 0; int size = sheet.GetSize(ref width, ref height);
            double[] properties = Numbers(sheet.GetProperties2()); Need(properties.Length == 8, "TRIAL_FRAME_EIGHT_SHEET_PROPERTIES_REQUIRED");
            string path = sheet.GetTemplateName(), formatName = sheet.GetSheetFormatName(); var ordinaryProof = TrialFrameSketchReadback(sw, ordinary); var formatProof = TrialFrameSketchReadback(sw, format);
            bool noFile = String.IsNullOrEmpty(path) || String.Equals(path.Trim(), "*.drt", StringComparison.OrdinalIgnoreCase);
            return new Dictionary<string, object> { { "sheet", sheet.GetName() }, { "sheetCount", document.GetSheetCount() }, { "paperSize", size }, { "widthMetres", width }, { "heightMetres", height }, { "properties", properties },
                { "editSheetMode", document.GetEditSheet() }, { "sheetView", sheetView.GetName2() }, { "templatePath", path }, { "sheetFormatName", formatName }, { "sheetFormatVisible", sheet.SheetFormatVisible },
                { "frameFileReferencesEmpty", noFile && properties[1] == (int)swDwgTemplates_e.swDwgTemplateNone },
                { "ordinarySketch", ordinaryProof }, { "templateSketch", formatProof }, { "templateSketchEmpty", TrialFrameSketchEmpty(formatProof) }, { "ordinaryAndTemplateSketchDistinct", ordinary != null && (format == null || sw.IsSame(ordinary, format) != 1) },
                { "sheetNoteCount", notes.Length }, { "sheetNoteTexts", notes.OfType<Note>().Select(v => v.GetText()).ToArray() } };
        }
        finally { foreach (object note in notes) Release(note); Release(ordinary); Release(format); Release(sheetView); Release(sheet); }
    }
    static Dictionary<string, object> CreateTrialDrawingFrame(ISldWorks sw, ModelDoc2 drawing, DxfTrace trace)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing; document.EditSheet(); Need(document.ActivateView(""), "TRIAL_FRAME_SHEET_CONTEXT_REQUIRED"); drawing.ClearSelection2(true);
        var proof = new Dictionary<string, object> { { "stageAcceptance", false }, { "contentAcceptance", false }, { "frameFileReferencesEmpty", false } };
        proof["beforeFormatRemoval"] = TrialFrameSheetReadback(sw, drawing); trace.Record("drawing_trial_frame_before_format_removal", proof["beforeFormatRemoval"]);
        Sheet sheet = document.GetCurrentSheet() as Sheet;
        try
        {
            Need(sheet != null, "TRIAL_FRAME_SHEET_SETUP_REQUIRED"); bool setup = document.SetupSheet5(sheet.GetName(), (int)swDwgPaperSizes_e.swDwgPaperA3size, (int)swDwgTemplates_e.swDwgTemplateNone, 1, 1, false, "", 0.420, 0.297, "", true);
            trace.Record("drawing_trial_frame_format_setup", new { returned = setup, templateType = (int)swDwgTemplates_e.swDwgTemplateNone, requestedTemplate = "", removeModifiedNotes = true });
            proof["setupSheetReturned"] = setup; Need(setup, "TRIAL_FRAME_REMOVE_OLD_FORMAT_FAILED"); sheet.SheetFormatVisible = false; sheet.SetName("门板变量试验");
        }
        finally { Release(sheet); sheet = null; }
        document.EditSheet(); Need(document.ActivateView(""), "TRIAL_FRAME_CLEARED_SHEET_CONTEXT_REQUIRED"); drawing.ClearSelection2(true);
        var cleared = TrialFrameSheetReadback(sw, drawing); proof["afterFormatRemoval"] = cleared; trace.Record("drawing_trial_frame_after_format_removal", cleared);
        Need(Convert.ToBoolean(cleared["frameFileReferencesEmpty"]) && Convert.ToBoolean(cleared["templateSketchEmpty"]) && Convert.ToInt32(cleared["sheetCount"]) == 1, "TRIAL_FRAME_OLD_FORMAT_OR_REFERENCE_REMAINS");
        double[] clearedProperties = (double[])cleared["properties"];
        Need(Math.Abs(Convert.ToDouble(cleared["widthMetres"], Invariant) - 0.420) < Tol && Math.Abs(Convert.ToDouble(cleared["heightMetres"], Invariant) - 0.297) < Tol
            && Convert.ToInt32(cleared["paperSize"]) == (int)swDwgPaperSizes_e.swDwgPaperA3size && clearedProperties[2] == 1 && clearedProperties[3] == 1, "TRIAL_FRAME_A3_LANDSCAPE_1_TO_1_READBACK_REQUIRED");
        Need(TrialFrameSketchEmpty((Dictionary<string, object>)cleared["ordinarySketch"]) && Convert.ToInt32(cleared["sheetNoteCount"]) == 0, "TRIAL_FRAME_ORDINARY_SHEET_SKETCH_OR_NOTES_NOT_EMPTY");
        double[][][] expected = new[] { new[] { new[] { .020, .010, 0.0 }, new[] { .410, .010, 0.0 } }, new[] { new[] { .410, .010, 0.0 }, new[] { .410, .287, 0.0 } },
            new[] { new[] { .410, .287, 0.0 }, new[] { .020, .287, 0.0 } }, new[] { new[] { .020, .287, 0.0 }, new[] { .020, .010, 0.0 } },
            new[] { new[] { .020, .258, 0.0 }, new[] { .410, .258, 0.0 } }, new[] { new[] { .020, .055, 0.0 }, new[] { .410, .055, 0.0 } },
            new[] { new[] { .020, .025, 0.0 }, new[] { .410, .025, 0.0 } }, new[] { new[] { .205, .025, 0.0 }, new[] { .205, .055, 0.0 } } };
        proof["expectedSegmentsMetres"] = expected; document.EditSketch(); SketchManager sketches = drawing.SketchManager; Sketch active = sketches.ActiveSketch; SwView sheetView = null; Sketch ordinary = null, format = null;
        try
        {
            sheetView = document.GetFirstView() as SwView; Need(sheetView != null, "TRIAL_FRAME_DRAWING_SHEET_VIEW_REQUIRED"); ordinary = sheetView.GetSketch() as Sketch; sheet = document.GetCurrentSheet() as Sheet; format = sheet == null ? null : sheet.GetTemplateSketch();
            bool exact = document.GetEditSheet() && active != null && ordinary != null && sw.IsSame(active, ordinary) == 1 && (format == null || sw.IsSame(active, format) != 1);
            proof["activeOrdinarySheetSketchIdentityVerified"] = exact; trace.Record("drawing_trial_frame_sketch_identity", new { verified = exact, editSheetMode = document.GetEditSheet() });
            Need(exact, "TRIAL_FRAME_REFUSE_NON_SHEET_OR_TEMPLATE_SKETCH"); proof["beforeDrawing"] = TrialFrameSketchReadback(sw, active);
            Need(TrialFrameSketchEmpty((Dictionary<string, object>)proof["beforeDrawing"]), "TRIAL_FRAME_ACTIVE_ORDINARY_SKETCH_NOT_EMPTY");
            foreach (double[][] endpoints in expected)
            {
                VariableTrial.RuntimeOwnership(); SketchSegment line = null;
                try { line = sketches.CreateLine(endpoints[0][0], endpoints[0][1], 0, endpoints[1][0], endpoints[1][1], 0); Need(line != null, "TRIAL_FRAME_NATIVE_LINE_CREATION_FAILED"); line.ConstructionGeometry = false; }
                finally { Release(line); }
            }
            var after = TrialFrameSketchReadback(sw, active); proof["afterDrawing"] = after; trace.Record("drawing_trial_frame_native_geometry", after);
            var actual = (List<Dictionary<string, object>>)after["segments"]; var used = new HashSet<int>();
            foreach (double[][] endpoints in expected)
            {
                int[] matches = Enumerable.Range(0, actual.Count).Where(index => Convert.ToBoolean(actual[index]["isLine"]) && Convert.ToBoolean(actual[index]["exactSketchMembership"]) && !Convert.ToBoolean(actual[index]["construction"])
                    && Math.Min(Math.Max(Gap(endpoints[0], (double[])actual[index]["startMetres"]), Gap(endpoints[1], (double[])actual[index]["endMetres"])), Math.Max(Gap(endpoints[0], (double[])actual[index]["endMetres"]), Gap(endpoints[1], (double[])actual[index]["startMetres"]))) < Tol).ToArray();
                Need(matches.Length == 1 && used.Add(matches[0]), "TRIAL_FRAME_LITERAL_SHEET_ENDPOINT_READBACK_FAILED");
            }
            Need(Convert.ToInt32(after["segmentCount"]) == 8 && Convert.ToInt32(after["lineCount"]) == 8 && Convert.ToInt32(after["endpointCount"]) == 16 && used.Count == 8, "TRIAL_FRAME_EIGHT_LINES_SIXTEEN_ENDPOINTS_REQUIRED"); proof["literalEndpointReadbackVerified"] = true;
        }
        finally { Release(format); Release(ordinary); Release(sheetView); Release(sheet); sheet = null; Release(active); Release(sketches); document.EditSheet(); drawing.ClearSelection2(true); }
        var finished = TrialFrameSheetReadback(sw, drawing); proof["afterDrawingSheet"] = finished;
        Need(Convert.ToBoolean(finished["frameFileReferencesEmpty"]) && Convert.ToBoolean(finished["templateSketchEmpty"]) && Convert.ToBoolean(finished["ordinaryAndTemplateSketchDistinct"])
            && Convert.ToBoolean(finished["editSheetMode"]) && Convert.ToInt32(((Dictionary<string, object>)finished["ordinarySketch"])["segmentCount"]) == 8, "TRIAL_FRAME_FINAL_SHEET_READBACK_FAILED");
        proof["frameFileReferencesEmpty"] = finished["frameFileReferencesEmpty"]; trace.Record("drawing_trial_frame_complete", proof); return proof;
    }
    static void RestoreTrialSheetScale(ISldWorks sw, ModelDoc2 drawing, SwView[] views, DxfTrace trace, string stage)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing; document.EditSheet();
        Need(document.ActivateView(""), "TRIAL_SHEET_SCALE_SHEET_CONTEXT_REQUIRED"); Sheet sheet = document.GetCurrentSheet() as Sheet;
        try
        {
            Need(sheet != null, "TRIAL_SHEET_SCALE_CURRENT_SHEET_REQUIRED"); double[] before = Numbers(sheet.GetProperties2());
            var beforeViews = views.Select(v => new { name = v.GetName2(), useSheetScale = v.UseSheetScale, scale = v.ScaleDecimal, positionMetres = Numbers(v.Position) }).ToArray();
            double[] scales = beforeViews.Select(v => v.scale).ToArray(); double[][] positions = beforeViews.Select(v => v.positionMetres).ToArray();
            trace.Record("drawing_sheet_scale_before_restore", new { phase = stage, properties = before, views = beforeViews });
            foreach (SwView view in views) view.UseSheetScale = 0;
            var independentViews = views.Select(v => new { name = v.GetName2(), useSheetScale = v.UseSheetScale, scale = v.ScaleDecimal, positionMetres = Numbers(v.Position) }).ToArray();
            bool scaleSet = sheet.SetScale(1, 1, false, false), sheetRebuilt = drawing.ForceRebuild3(false);
            double[] afterScale = Numbers(sheet.GetProperties2());
            var afterScaleViews = views.Select(v => new { name = v.GetName2(), useSheetScale = v.UseSheetScale, scale = v.ScaleDecimal, positionMetres = Numbers(v.Position) }).ToArray();
            trace.Record("drawing_sheet_scale_raw_readback", new { phase = stage, beforeProperties = before, afterProperties = afterScale, beforeViews = beforeViews, independentViews = independentViews, afterViews = afterScaleViews, scaleSet = scaleSet, rebuilt = sheetRebuilt });
            Need(scaleSet, "TRIAL_SHEET_SCALE_RESTORE_FAILED"); Need(sheetRebuilt, "TRIAL_SHEET_SCALE_REBUILD_FAILED");
            for (int i = 0; i < views.Length; i++) { views[i].UseSheetScale = 0; views[i].ScaleDecimal = scales[i]; views[i].Position = positions[i]; }
            bool viewsRebuilt = drawing.ForceRebuild3(false); double[] after = Numbers(sheet.GetProperties2());
            var afterViews = views.Select(v => new { name = v.GetName2(), useSheetScale = v.UseSheetScale, scale = v.ScaleDecimal, positionMetres = Numbers(v.Position) }).ToArray();
            trace.Record("drawing_sheet_scale_restored", new { phase = stage, beforeProperties = before, afterSetScaleProperties = afterScale, afterProperties = after, beforeViews = beforeViews, afterSetScaleViews = afterScaleViews,
                scaleAnnotationPosition = false, scaleAnnotationTextHeight = false, viewScaleAndPositionExplicitlyRestored = true, rebuilt = viewsRebuilt, views = afterViews });
            Need(viewsRebuilt, "TRIAL_SHEET_SCALE_VIEW_RESTORE_REBUILD_FAILED");
            Need(after.Length == 8 && after[2] == 1 && after[3] == 1, "TRIAL_SHEET_SCALE_ONE_TO_ONE_READBACK_REQUIRED");
            Need(Enumerable.Range(0, views.Length).All(i => afterViews[i].useSheetScale == 0 && Math.Abs(afterViews[i].scale - scales[i]) < Tol && afterViews[i].positionMetres.Length == positions[i].Length && Gap(afterViews[i].positionMetres, positions[i]) < Tol), "TRIAL_SHEET_SCALE_VIEW_RESTORE_READBACK_FAILED");
        }
        finally { Release(sheet); }
    }
    static object TrialDrawingFrameFinalReadback(ISldWorks sw, ModelDoc2 drawing, SwView[] views, Dictionary<string, object> original, DxfTrace trace, string stage)
    {
        ActivateOutputDrawing(sw, drawing); DrawingDoc document = (DrawingDoc)drawing; document.EditSheet(); Need(document.ActivateView(""), "TRIAL_FRAME_FINAL_SHEET_CONTEXT_REQUIRED"); drawing.ClearSelection2(true);
        var sheet = TrialFrameSheetReadback(sw, drawing); double[] properties = (double[])sheet["properties"];
        Need(properties[2] == 1 && properties[3] == 1 && views.All(v => v.UseSheetScale == 0), "TRIAL_FRAME_FINAL_SHEET_SCALE_ONE_TO_ONE_REQUIRED");
        Need(Convert.ToBoolean(sheet["frameFileReferencesEmpty"]) && Convert.ToBoolean(sheet["templateSketchEmpty"]) && Convert.ToBoolean(sheet["ordinaryAndTemplateSketchDistinct"]) && Convert.ToBoolean(sheet["editSheetMode"]), "TRIAL_FRAME_FINAL_FORMAT_STATE_REQUIRED");
        var sketch = (Dictionary<string, object>)sheet["ordinarySketch"]; var actual = (List<Dictionary<string, object>>)sketch["segments"]; double[][][] expected = (double[][][])original["expectedSegmentsMetres"]; var used = new HashSet<int>();
        foreach (double[][] endpoints in expected)
        {
            int[] matches = Enumerable.Range(0, actual.Count).Where(index => Convert.ToBoolean(actual[index]["isLine"]) && Convert.ToBoolean(actual[index]["exactSketchMembership"]) && !Convert.ToBoolean(actual[index]["construction"])
                && Math.Min(Math.Max(Gap(endpoints[0], (double[])actual[index]["startMetres"]), Gap(endpoints[1], (double[])actual[index]["endMetres"])), Math.Max(Gap(endpoints[0], (double[])actual[index]["endMetres"]), Gap(endpoints[1], (double[])actual[index]["startMetres"]))) < Tol).ToArray();
            Need(matches.Length == 1 && used.Add(matches[0]), "TRIAL_FRAME_FINAL_LITERAL_ENDPOINT_READBACK_FAILED");
        }
        Need(Convert.ToInt32(sketch["segmentCount"]) == 8 && Convert.ToInt32(sketch["lineCount"]) == 8 && Convert.ToInt32(sketch["endpointCount"]) == 16 && Convert.ToInt32(sketch["blockInstanceCount"]) == 0 && Convert.ToInt32(sketch["pictureCount"]) == 0 && used.Count == 8, "TRIAL_FRAME_FINAL_EIGHT_LINES_REQUIRED");
        var proof = new { phase = stage, sheet = sheet, literalEndpointReadbackVerified = true, sheetScaleOneToOne = true, viewsUseIndependentScales = true };
        trace.Record("drawing_trial_frame_final_readback", proof); return proof;
    }
    sealed class NativeBendNoteState
    {
        internal Note Note; internal Annotation Annotation; internal object[] Attachments = new object[0]; internal string Text;
        internal bool Up; internal double BendAngle, Radius; internal double[] Points, OriginalPosition, TargetPosition;
        internal bool OriginalUseDocumentFont; internal Dictionary<string, object> OriginalFont;
    }
    static void ReleaseNativeBendNotes(List<NativeBendNoteState> notes)
    {
        if (notes == null) return;
        foreach (NativeBendNoteState note in notes) { foreach (object entity in note.Attachments) Release(entity); Release(note.Annotation); Release(note.Note); }
    }
    static int[] BendDocumentPreferences(ModelDoc2 drawing)
    {
        ModelDocExtension extension = drawing.Extension;
        try
        {
            int option = (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified;
            return new[] { extension.GetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swSheetMetalBendNotesLeaderDisplay, option),
                extension.GetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swSheetMetalBendNotesLeaderAnchor, option),
                extension.GetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swBendNoteStyle, option) };
        }
        finally { Release(extension); }
    }
    static int[] BendDocumentTargets()
    {
        return new[] { (int)swLeaderStyle_e.swBENT, (int)swLeaderSide_e.swLS_LEFT, (int)swBendNoteStyle_e.swWithLeader };
    }
    static void PrepareBendDocumentLeaders(ModelDoc2 drawing, DxfTrace trace)
    {
        Need(drawing is DrawingDoc && String.IsNullOrEmpty(drawing.GetPathName()) && !drawing.IsOpenedReadOnly(), "NEW_UNSAVED_TRIAL_DRAWING_REQUIRED_FOR_BEND_PREFERENCES");
        int[] preferences = { (int)swUserPreferenceIntegerValue_e.swSheetMetalBendNotesLeaderDisplay, (int)swUserPreferenceIntegerValue_e.swSheetMetalBendNotesLeaderAnchor, (int)swUserPreferenceIntegerValue_e.swBendNoteStyle };
        int[] before = BendDocumentPreferences(drawing), targets = BendDocumentTargets(); int option = (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified;
        trace.Record("drawing_bend_document_preferences_before", new { preferences = preferences, option = option, before = before, targets = targets, scope = "New trial drawing document properties only; no application or template preference setters." });
        ModelDocExtension extension = drawing.Extension;
        try
        {
            foreach (int i in new[] { 2, 1, 0 })
            {
                int[] stepBefore = BendDocumentPreferences(drawing);
                if (stepBefore[i] == targets[i]) { trace.Record("drawing_bend_document_preference_unchanged", new { preference = preferences[i], target = targets[i], actual = stepBefore[i], allPreferences = stepBefore }); continue; }
                VariableTrial.RuntimeOwnership(); bool returned = false;
                try { returned = extension.SetUserPreferenceInteger(preferences[i], option, targets[i]); }
                finally { trace.Record("drawing_bend_document_preference_set", new { preference = preferences[i], original = stepBefore[i], target = targets[i], returned = returned, actual = extension.GetUserPreferenceInteger(preferences[i], option), allBefore = stepBefore, allAfter = BendDocumentPreferences(drawing) }); }
                Need(returned && extension.GetUserPreferenceInteger(preferences[i], option) == targets[i], "BEND_DOCUMENT_PREFERENCE_READBACK_FAILED");
            }
        }
        finally { Release(extension); }
        bool rebuilt = drawing.ForceRebuild3(false); drawing.GraphicsRedraw2(); drawing.WindowRedraw(); int[] after = BendDocumentPreferences(drawing);
        trace.Record("drawing_bend_document_preferences_after", new { rebuilt = rebuilt, preferences = preferences, before = before, targets = targets, after = after });
        Need(rebuilt && after.SequenceEqual(targets), "BEND_DOCUMENT_PREFERENCES_REBUILD_READBACK_FAILED");
    }
    static List<NativeBendNoteState> LayoutNativeBendNotes(ISldWorks sw, ModelDoc2 drawing, SwView view, DxfTrace trace)
    {
        ActivateOutputDrawing(sw, drawing); Need(view.IsFlatPatternView() && view.ShowSheetMetalBendNotes, "NATIVE_BEND_NOTE_FLAT_VIEW_REQUIRED");
        object[] values = Items(view.GetNotes()); var kept = new HashSet<int>(); var notes = new List<NativeBendNoteState>(); bool ready = false;
        try
        {
            for (int i = 0; i < values.Length; i++)
            {
                Note note = values[i] as Note; if (note == null || !note.IsBendLineNote) continue;
                var state = new NativeBendNoteState { Note = note, Text = note.GetText() }; notes.Add(state); kept.Add(i);
                state.Annotation = note.GetAnnotation() as Annotation; Need(state.Annotation != null && !state.Annotation.IsDangling(), "NATIVE_BEND_NOTE_ANNOTATION_REQUIRED");
                object points = null; Need(note.GetBendLineValues2(ref state.Up, ref state.BendAngle, ref state.Radius, ref points), "NATIVE_BEND_NOTE_VALUES_REQUIRED");
                state.Points = Numbers(points); state.OriginalPosition = Numbers(state.Annotation.GetPosition()); state.Attachments = Items(state.Annotation.GetAttachedEntities3());
                Need(state.Points.Length >= 6 && state.Points.Length % 6 == 0 && state.Points.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && state.OriginalPosition.Length == 3, "NATIVE_BEND_NOTE_GEOMETRY_REQUIRED");
                Need(state.Attachments.Length > 0 && state.Attachments.All(v => v != null), "NATIVE_BEND_NOTE_REAL_ATTACHMENTS_REQUIRED");
                TextFormat nativeFont = state.Annotation.GetTextFormat(0) as TextFormat;
                try
                {
                    state.OriginalUseDocumentFont = state.Annotation.GetUseDocTextFormat(0); state.OriginalFont = nativeFont == null ? null : PdfProbeFormatValues(nativeFont);
                    trace.Record("drawing_bend_note_source_font", new { annotationName = state.Annotation.GetName(), sourceUseDocumentFont = state.OriginalUseDocumentFont, fontPolicy = "Preserve each native note's font source and actual 3.5 mm typeface; only its intended layout angle may change.", readback = FontReadback(state.Annotation, note) });
                    Need(nativeFont != null && state.Annotation.GetTextFormatCount() == 1 && !note.HasMultipleFonts && nativeFont.TypeFaceName == "汉仪长仿宋体" && Math.Abs(nativeFont.CharHeight - 0.0035) < Tol, "NATIVE_BEND_NOTE_SOURCE_FONT_REQUIRED");
                }
                finally { Release(nativeFont); }
            }
            Need(notes.Count > 0 && notes.Count == view.GetBendLineCount() && notes.Count <= 12, "ONE_NATIVE_BEND_NOTE_PER_LINE_WITHIN_COLUMN_REQUIRED");
            notes = notes.OrderByDescending(v => v.OriginalPosition[1]).ThenBy(v => v.OriginalPosition[0]).ThenBy(v => v.Annotation.GetName(), StringComparer.Ordinal).ToList();
            trace.Record("drawing_bend_notes_before_layout", new { count = notes.Count, nativeLineCount = view.GetBendLineCount(), notes = notes.Select(v => new { annotationName = v.Annotation.GetName(), text = v.Text, up = v.Up, angleRadians = v.BendAngle, radiusMetres = v.Radius, bendPoints = v.Points, originalPositionMetres = v.OriginalPosition, attachmentCount = v.Attachments.Length }).ToArray() });
            PrepareBendDocumentLeaders(drawing, trace);
            for (int i = 0; i < notes.Count; i++)
            {
                VariableTrial.RuntimeOwnership(); NativeBendNoteState note = notes[i]; double beforeAngle = note.Note.Angle;
                trace.Record("drawing_bend_note_angle_before", new { requestedAngleRadians = 0.0, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                try { note.Note.Angle = 0; }
                finally { trace.Record("drawing_bend_note_angle_raw_readback", new { annotationName = note.Annotation.GetName(), originalAngleRadians = beforeAngle, requestedAngleRadians = 0.0, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) }); }
                Need(Math.Abs(note.Note.Angle) < Tol, "NATIVE_BEND_NOTE_HORIZONTAL_ANGLE_READBACK_FAILED");
                int leaderResult = -1;
                trace.Record("drawing_bend_note_leader_before", new { requestedStyle = (int)swLeaderStyle_e.swBENT, requestedSide = (int)swLeaderSide_e.swLS_LEFT, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                try { leaderResult = note.Annotation.SetLeader3((int)swLeaderStyle_e.swBENT, (int)swLeaderSide_e.swLS_LEFT, true, false, false, false); }
                finally { trace.Record("drawing_bend_note_leader_raw_readback", new { annotationName = note.Annotation.GetName(), returned = leaderResult, requestedStyle = (int)swLeaderStyle_e.swBENT, requestedSide = (int)swLeaderSide_e.swLS_LEFT, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) }); }
                Need(leaderResult == 0, "NATIVE_BEND_NOTE_LEADER_REQUEST_FAILED");
                bool preparedRebuilt = drawing.ForceRebuild3(false); drawing.GraphicsRedraw2(); drawing.WindowRedraw();
                trace.Record("drawing_bend_note_prepared_rebuild_readback", new { returned = preparedRebuilt, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                Need(preparedRebuilt, "NATIVE_BEND_NOTE_PREPARED_REBUILD_FAILED");
                double targetLeft = 0.338, targetTop = 0.220 - i * 0.008;
                double[] currentPosition = Numbers(note.Annotation.GetPosition()), currentBox = VisibleNoteExtent(note.Note);
                Need(currentPosition.Length == 3, "NATIVE_BEND_NOTE_CURRENT_POSITION_REQUIRED");
                note.TargetPosition = new[] { currentPosition[0] + targetLeft - currentBox[0], currentPosition[1] + targetTop - currentBox[4], note.OriginalPosition[2] }; bool positioned = false;
                trace.Record("drawing_bend_note_position_before", new { targetPositionMetres = note.TargetPosition, nativeZPreserved = true, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                try { positioned = note.Annotation.SetPosition2(note.TargetPosition[0], note.TargetPosition[1], note.TargetPosition[2]); }
                finally { trace.Record("drawing_bend_note_position_raw_readback", new { annotationName = note.Annotation.GetName(), returned = positioned, targetPositionMetres = note.TargetPosition, nativeZPreserved = true, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) }); }
                Need(positioned && SameDrawingNumbers(note.Annotation.GetPosition(), note.TargetPosition), "NATIVE_BEND_NOTE_POSITION_FAILED");
                bool movedRebuilt = drawing.ForceRebuild3(false); drawing.GraphicsRedraw2(); drawing.WindowRedraw();
                trace.Record("drawing_bend_note_moved_rebuild_readback", new { returned = movedRebuilt, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                Need(movedRebuilt, "NATIVE_BEND_NOTE_MOVED_REBUILD_FAILED");
                leaderResult = -1;
                trace.Record("drawing_bend_note_moved_leader_before", new { requestedStyle = (int)swLeaderStyle_e.swBENT, requestedSide = (int)swLeaderSide_e.swLS_LEFT, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                try { leaderResult = note.Annotation.SetLeader3((int)swLeaderStyle_e.swBENT, (int)swLeaderSide_e.swLS_LEFT, true, false, false, false); }
                finally { trace.Record("drawing_bend_note_moved_leader_raw_readback", new { annotationName = note.Annotation.GetName(), returned = leaderResult, requestedStyle = (int)swLeaderStyle_e.swBENT, requestedSide = (int)swLeaderSide_e.swLS_LEFT, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) }); }
                Need(leaderResult == 0, "NATIVE_BEND_NOTE_MOVED_LEADER_REQUEST_FAILED");
                bool leaderRebuilt = drawing.ForceRebuild3(false); drawing.GraphicsRedraw2(); drawing.WindowRedraw();
                trace.Record("drawing_bend_note_moved_leader_rebuild_readback", new { returned = leaderRebuilt, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                Need(leaderRebuilt && note.Annotation.GetLeaderStyle() == (int)swLeaderStyle_e.swBENT && note.Annotation.GetLeaderCount() > 0, "NATIVE_BEND_NOTE_BENT_LEADER_FAILED_AFTER_MOVE_AND_REBUILD");
                currentBox = VisibleNoteExtent(note.Note);
                if (Math.Abs(currentBox[0] - targetLeft) >= Tol || Math.Abs(currentBox[4] - targetTop) >= Tol)
                {
                    currentPosition = Numbers(note.Annotation.GetPosition()); Need(currentPosition.Length == 3, "NATIVE_BEND_NOTE_CORRECTION_POSITION_REQUIRED");
                    note.TargetPosition = new[] { currentPosition[0] + targetLeft - currentBox[0], currentPosition[1] + targetTop - currentBox[4], note.OriginalPosition[2] }; positioned = false;
                    try { positioned = note.Annotation.SetPosition2(note.TargetPosition[0], note.TargetPosition[1], note.TargetPosition[2]); }
                    finally { trace.Record("drawing_bend_note_extent_correction_raw_readback", new { targetLeftMetres = targetLeft, targetTopMetres = targetTop, targetPositionMetres = note.TargetPosition, returned = positioned, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) }); }
                    Need(positioned && SameDrawingNumbers(note.Annotation.GetPosition(), note.TargetPosition), "NATIVE_BEND_NOTE_EXTENT_CORRECTION_POSITION_FAILED");
                    bool correctionRebuilt = drawing.ForceRebuild3(false); drawing.GraphicsRedraw2(); drawing.WindowRedraw();
                    trace.Record("drawing_bend_note_extent_correction_rebuild_readback", new { returned = correctionRebuilt, readback = NativeBendNotePlacementReadback(note.Note, note.Annotation) });
                    Need(correctionRebuilt && note.Annotation.GetLeaderStyle() == (int)swLeaderStyle_e.swBENT && note.Annotation.GetLeaderCount() > 0, "NATIVE_BEND_NOTE_EXTENT_CORRECTION_LEADER_FAILED");
                    currentBox = VisibleNoteExtent(note.Note);
                }
                Need(Math.Abs(currentBox[0] - targetLeft) < Tol && Math.Abs(currentBox[4] - targetTop) < Tol, "NATIVE_BEND_NOTE_TEXT_EXTENT_ALIGNMENT_FAILED");
            }
            bool rebuilt = drawing.ForceRebuild3(false); drawing.GraphicsRedraw2(); drawing.WindowRedraw();
            trace.Record("drawing_bend_note_layout_rebuild_raw_readback", new { returned = rebuilt, notes = notes.Select(v => NativeBendNotePlacementReadback(v.Note, v.Annotation)).ToArray() });
            Need(rebuilt, "NATIVE_BEND_NOTE_LAYOUT_REBUILD_FAILED"); ready = true; return notes;
        }
        finally { for (int i = 0; i < values.Length; i++) if (!kept.Contains(i)) Release(values[i]); if (!ready) ReleaseNativeBendNotes(notes); }
    }
    static bool NoteBoxesOverlap(double[] a, double[] b)
    {
        return a[0] < b[3] + 0.001 && a[3] + 0.001 > b[0] && a[1] < b[4] + 0.001 && a[4] + 0.001 > b[1];
    }
    static double[] VisibleNoteExtent(Note note)
    {
        double[] box = Numbers(note.GetExtent());
        Need(box.Length == 6 && box.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && box[3] > box[0] && box[4] > box[1], "VISIBLE_NATIVE_NOTE_EXTENT_REQUIRED"); return box;
    }
    static object NativeBendNotePlacementReadback(Note note, Annotation annotation)
    {
        var result = new Dictionary<string, object>();
        try
        {
            result["annotationName"] = annotation.GetName(); result["text"] = note.GetText(); result["font"] = FontReadback(annotation, note); result["angleRadians"] = note.Angle;
            result["positionMetres"] = Numbers(annotation.GetPosition()); result["extentMetres"] = Numbers(note.GetExtent()); result["visible"] = annotation.Visible; result["dangling"] = annotation.IsDangling(); result["positionLocked"] = note.LockPosition;
            result["attachmentTypes"] = Items(annotation.GetAttachedEntityTypes()).Select(Convert.ToInt32).ToArray(); result["leaderSide"] = annotation.GetLeaderSide();
            result["leaderStyle"] = annotation.GetLeaderStyle(); int count = annotation.GetLeaderCount(); result["leaderCount"] = count;
            result["leaderPointsMetres"] = Enumerable.Range(0, count).Select(i => Numbers(annotation.GetLeaderPointsAtIndex(i))).ToArray();
        }
        catch (Exception error) { result["readbackError"] = error.ToString(); }
        return result;
    }
    static object NativeBendNoteLayoutReadback(ISldWorks sw, ModelDoc2 drawing, SwView view, List<NativeBendNoteState> expected, List<object> expectedSheetNotes, DxfTrace trace, string stage)
    {
        ActivateOutputDrawing(sw, drawing); object[] notes = Items(view.GetNotes()), sheetNotes = new object[0]; SwView sheetView = null; var rows = new List<object>(); var boxes = new List<double[]>();
        try
        {
            Need(expected != null && notes.OfType<Note>().Count(v => v.IsBendLineNote) == expected.Count && expected.Count == view.GetBendLineCount(), "NATIVE_BEND_NOTE_COUNT_CHANGED");
            sheetView = ((DrawingDoc)drawing).GetFirstView() as SwView; Need(sheetView != null, "NATIVE_BEND_NOTE_SHEET_VIEW_REQUIRED"); sheetNotes = Items(sheetView.GetNotes());
            var otherBoxes = new List<double[]>();
            foreach (Dictionary<string, object> item in expectedSheetNotes.Cast<Dictionary<string, object>>())
            {
                Note[] matches = sheetNotes.OfType<Note>().Where(v => NoteText(v.GetText()) == NoteText(Convert.ToString(item["text"], Invariant))).ToArray();
                Need(matches.Length == 1, "NATIVE_BEND_NOTE_OTHER_SHEET_NOTE_REQUIRED"); otherBoxes.Add(VisibleNoteExtent(matches[0]));
            }
            foreach (NativeBendNoteState state in expected)
            {
                VariableTrial.RuntimeOwnership(); Need(notes.Count(v => v is Note && ((Note)v).IsBendLineNote && sw.IsSame(v, state.Note) == 1) == 1, "NATIVE_BEND_NOTE_OBJECT_CHANGED");
                Annotation annotation = state.Note.GetAnnotation() as Annotation; object[] attached = new object[0]; TextFormat format = null;
                try
                {
                    Need(annotation != null && sw.IsSame(annotation, state.Annotation) == 1 && !annotation.IsDangling() && annotation.Visible == (int)swAnnotationVisibilityState_e.swAnnotationVisible, "NATIVE_BEND_NOTE_IDENTITY_OR_VISIBILITY_CHANGED");
                    bool up = false; double angle = 0, radius = 0; object points = null; Need(state.Note.GetBendLineValues2(ref up, ref angle, ref radius, ref points), "NATIVE_BEND_NOTE_VALUES_READBACK_FAILED"); double[] actualPoints = Numbers(points);
                    Need(state.Note.GetText() == state.Text && up == state.Up && Math.Abs(angle - state.BendAngle) < Tol && Math.Abs(radius - state.Radius) < Tol && actualPoints.Length == state.Points.Length && Gap(actualPoints, state.Points) < Tol, "NATIVE_BEND_NOTE_TEXT_DIRECTION_OR_GEOMETRY_CHANGED");
                    attached = Items(annotation.GetAttachedEntities3()); var used = new HashSet<int>();
                    foreach (object entity in state.Attachments)
                    {
                        int[] matches = Enumerable.Range(0, attached.Length).Where(i => attached[i] != null && sw.IsSame(entity, attached[i]) == 1).ToArray();
                        Need(matches.Length == 1 && used.Add(matches[0]), "NATIVE_BEND_NOTE_ATTACHMENT_OBJECT_CHANGED");
                    }
                    Need(attached.Length == state.Attachments.Length && used.Count == attached.Length, "NATIVE_BEND_NOTE_ATTACHMENT_COUNT_CHANGED");
                    format = annotation.GetTextFormat(0) as TextFormat; double[] position = Numbers(annotation.GetPosition());
                    var targetFont = new Dictionary<string, object>(state.OriginalFont); targetFont["escapement"] = 0.0;
                    trace.Record("drawing_bend_note_final_raw_readback", new { phase = stage, sourceFont = state.OriginalFont, sourceUseDocumentFont = state.OriginalUseDocumentFont, targetFont = targetFont, targetPositionMetres = state.TargetPosition, readback = NativeBendNotePlacementReadback(state.Note, annotation) });
                    Need(format != null && format.TypeFaceName == "汉仪长仿宋体" && Math.Abs(format.CharHeight - 0.0035) < Tol && annotation.GetUseDocTextFormat(0) == state.OriginalUseDocumentFont && !state.Note.HasMultipleFonts && SameNativeFontFormat(targetFont, PdfProbeFormatValues(format)) && Math.Abs(state.Note.Angle) < Tol && position.Length == 3 && Gap(position, state.TargetPosition) < Tol, "NATIVE_BEND_NOTE_LAYOUT_OR_SOURCE_FONT_READBACK_FAILED");
                    Need(annotation.GetLeaderStyle() == (int)swLeaderStyle_e.swBENT && annotation.GetLeaderCount() > 0, "NATIVE_BEND_NOTE_VISIBLE_LEADER_REQUIRED");
                    double[][] leaders = Enumerable.Range(0, annotation.GetLeaderCount()).Select(i => Numbers(annotation.GetLeaderPointsAtIndex(i))).ToArray();
                    Need(leaders.All(v => v.Length == 9 && v.All(p => !Double.IsNaN(p) && !Double.IsInfinity(p))), "NATIVE_BEND_NOTE_LEADER_GEOMETRY_REQUIRED");
                    double[] box = VisibleNoteExtent(state.Note);
                    Need(box[0] >= 0.334 && box[3] <= 0.405 && box[1] >= 0.120 && box[4] <= 0.225, "NATIVE_BEND_NOTE_OUTSIDE_RESERVED_PAPER_COLUMN");
                    Need(boxes.All(v => !NoteBoxesOverlap(box, v)) && otherBoxes.All(v => !NoteBoxesOverlap(box, v)), "NATIVE_BEND_NOTE_TEXT_OVERLAP"); boxes.Add(box);
                    object attachmentProof = NativeBendAttachmentReadback(sw, view, annotation, attached);
                    rows.Add(new { annotationName = annotation.GetName(), text = state.Note.GetText(), originalText = state.Text, nativeUp = up, angleRadians = angle, radiusMetres = radius, nativeBendPoints = actualPoints,
                        originalPositionMetres = state.OriginalPosition, targetPositionMetres = state.TargetPosition, actualPositionMetres = position, textExtentMetres = box, font = new { name = format.TypeFaceName, heightMetres = format.CharHeight, useDocumentFormat = annotation.GetUseDocTextFormat(0), sourceUseDocumentFormat = state.OriginalUseDocumentFont, sourceFormat = state.OriginalFont, actualFormat = PdfProbeFormatValues(format), sourceFontPreservedExceptEscapement = true, targetEscapementRadians = 0.0 }, noteAngleRadians = state.Note.Angle,
                        leaderStyle = annotation.GetLeaderStyle(), leaderPointsMetres = leaders, attachmentCount = attached.Length, attachments = attachmentProof, exactObjectPreserved = true, textAndNativeBendValuesPreserved = true, exactAttachmentsPreserved = true });
                }
                finally { Release(format); foreach (object entity in attached) Release(entity); Release(annotation); }
            }
            int[] documentPreferences = BendDocumentPreferences(drawing); Need(documentPreferences.SequenceEqual(BendDocumentTargets()), "BEND_DOCUMENT_PREFERENCES_CHANGED");
            var proof = new { phase = stage, noteCount = rows.Count, nativeLineCount = view.GetBendLineCount(), notes = rows, documentPreferences = documentPreferences, allNativeObjectsAndValuesPreserved = true, noteTextBoxesInsidePaper = true, noteTextBoxesDoNotOverlap = true, minimumTextGapMetres = 0.001,
                reservedColumnMetres = new[] { 0.334, 0.120, 0.405, 0.225 }, limitation = "Native bend direction is retained as supplied by SOLIDWORKS; manufacturing orientation is not independently accepted. Leader routing still requires PDF visual review." };
            trace.Record("drawing_bend_notes_layout_readback", proof); return proof;
        }
        finally { foreach (object note in notes) Release(note); foreach (object note in sheetNotes) Release(note); Release(sheetView); }
    }
    static Dictionary<string, object> DrawingProofMap(object value)
    {
        var result = value as Dictionary<string, object>; Need(result != null, "REOPENED_DRAWING_PRIOR_OBJECT_REQUIRED"); return result;
    }
    static bool SameDrawingNumbers(object a, object b)
    {
        double[] first = Numbers(a), second = Numbers(b);
        return first.Length == second.Length && first.Length > 0 && first.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && second.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && Gap(first, second) < Tol;
    }
    static bool SameNativeFontFormat(Dictionary<string, object> expected, Dictionary<string, object> actual)
    {
        return expected != null && expected.Count == actual.Count && expected.All(v => actual.ContainsKey(v.Key) && (v.Value is string || v.Value is bool
            ? Object.Equals(v.Value, actual[v.Key]) : Math.Abs(Convert.ToDouble(v.Value, Invariant) - Convert.ToDouble(actual[v.Key], Invariant)) < 1e-12));
    }
    static Dictionary<string, object> NativeBendSegmentReadback(object value)
    {
        SketchSegment segment = value as SketchSegment; SketchLine line = value as SketchLine; Sketch sketch = null; SketchPoint start = null, end = null; MathTransform transform = null, inverse = null;
        try
        {
            Need(segment != null && line != null && segment.IsBendLine(), "NATIVE_BEND_ATTACHMENT_SKETCH_LINE_REQUIRED 未验证：缺附着实体与原生折弯草图线的对应证据。");
            sketch = segment.GetSketch(); start = line.GetStartPoint2() as SketchPoint; end = line.GetEndPoint2() as SketchPoint;
            Need(sketch != null && start != null && end != null, "NATIVE_BEND_ATTACHMENT_SKETCH_AND_ENDPOINTS_REQUIRED"); transform = sketch.ModelToSketchTransform;
            Need(transform != null, "NATIVE_BEND_ATTACHMENT_SKETCH_TRANSFORM_REQUIRED"); inverse = transform.IInverse(); Need(inverse != null, "NATIVE_BEND_ATTACHMENT_INVERSE_TRANSFORM_REQUIRED");
            double[] matrix = Numbers(inverse.ArrayData), a = TransformPoint(new[] { start.X, start.Y, start.Z }, matrix), b = TransformPoint(new[] { end.X, end.Y, end.Z }, matrix);
            Need(a.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && b.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && Gap(a, b) > Tol, "NATIVE_BEND_ATTACHMENT_FINITE_GEOMETRY_REQUIRED");
            return new Dictionary<string, object> { { "segmentType", segment.GetType() }, { "directionEnum", line.GetBendLineDirection() }, { "startModelMetres", a }, { "endModelMetres", b }, { "lengthMetres", segment.GetLength() } };
        }
        finally { Release(inverse); Release(transform); Release(start); Release(end); Release(sketch); }
    }
    static bool SameNativeBendSegment(Dictionary<string, object> a, Dictionary<string, object> b)
    {
        return Convert.ToInt32(a["segmentType"]) == Convert.ToInt32(b["segmentType"]) && Convert.ToInt32(a["directionEnum"]) == Convert.ToInt32(b["directionEnum"])
            && Math.Abs(Convert.ToDouble(a["lengthMetres"], Invariant) - Convert.ToDouble(b["lengthMetres"], Invariant)) < Tol
            && (SameDrawingNumbers(a["startModelMetres"], b["startModelMetres"]) && SameDrawingNumbers(a["endModelMetres"], b["endModelMetres"])
                || SameDrawingNumbers(a["startModelMetres"], b["endModelMetres"]) && SameDrawingNumbers(a["endModelMetres"], b["startModelMetres"]));
    }
    static List<Dictionary<string, object>> NativeBendAttachmentReadback(ISldWorks sw, SwView view, Annotation annotation, object[] attached)
    {
        object[] lines = Items(view.GetBendLines()); int[] kinds = Items(annotation.GetAttachedEntityTypes()).Select(Convert.ToInt32).ToArray(); var rows = new List<Dictionary<string, object>>(); var used = new HashSet<int>();
        try
        {
            Need(attached.Length > 0 && kinds.Length == attached.Length && attached.All(v => v != null) && lines.Length == view.GetBendLineCount(), "NATIVE_BEND_ATTACHMENT_ENTITIES_AND_CURRENT_VIEW_LINES_REQUIRED");
            var geometry = lines.Select(NativeBendSegmentReadback).ToArray();
            for (int i = 0; i < attached.Length; i++)
            {
                VariableTrial.RuntimeOwnership(); Need(kinds[i] == (int)swSelectType_e.swSelSKETCHSEGS, "NATIVE_BEND_ATTACHMENT_UNSUPPORTED_KIND 未验证：缺当前附着实体所属视图的唯一原生几何证明。");
                var actual = NativeBendSegmentReadback(attached[i]); int[] matches = Enumerable.Range(0, geometry.Length).Where(index => SameNativeBendSegment(actual, geometry[index])).ToArray();
                Need(matches.Length == 1 && used.Add(matches[0]), "NATIVE_BEND_ATTACHMENT_UNIQUE_CURRENT_VIEW_LINE_REQUIRED");
                rows.Add(new Dictionary<string, object> { { "entityType", kinds[i] }, { "view", view.GetName2() }, { "geometry", actual }, { "matchingNativeBendLineIndex", matches[0] }, { "uniqueCurrentViewGeometryMatch", true }, { "sameCurrentNativeObject", sw.IsSame(attached[i], lines[matches[0]]) == 1 },
                    { "matchingBasis", "Unique native sketch-line geometry among the current IView.GetBendLines result; no cross-process object identity claim." } });
            }
            return rows;
        }
        finally { foreach (object value in lines) Release(value); }
    }
    public static object ReadReopenedDrawing(ISldWorks sw, ModelDoc2 drawing, Dictionary<string, object> priorDrawingProof)
    {
        var report = new Dictionary<string, object> { { "pass", false }, { "productionReleased", false }, { "layoutRepairCalled", false }, { "crossProcessObjectIdentityClaimed", false } };
        var failures = new List<string>(); var currentViews = new List<SwView>(); string path = null, beforeHash = null; SwView sheetView = null; object[] sheetNotes = new object[0];
        try
        {
            VariableTrial.RuntimeOwnership(); Need(sw != null && drawing is DrawingDoc && drawing.IsOpenedReadOnly() && drawing.Visible, "REOPENED_VISIBLE_READONLY_DRAWING_REQUIRED");
            Need(priorDrawingProof != null, "REOPENED_DRAWING_PRIOR_PROOF_REQUIRED"); path = Local(drawing.GetPathName()); beforeHash = Hash(path);
            Need(drawing.ForceRebuild3(false), "REOPENED_DRAWING_REBUILD_FAILED"); DrawingDoc document = (DrawingDoc)drawing; var sheet = TrialFrameSheetReadback(sw, drawing); report["sheet"] = sheet;
            double[] properties = Numbers(sheet["properties"]);
            Need(Convert.ToInt32(sheet["sheetCount"]) == 1 && Convert.ToInt32(sheet["paperSize"]) == (int)swDwgPaperSizes_e.swDwgPaperA3size && Math.Abs(Convert.ToDouble(sheet["widthMetres"], Invariant) - 0.420) < Tol && Math.Abs(Convert.ToDouble(sheet["heightMetres"], Invariant) - 0.297) < Tol && properties[2] == 1 && properties[3] == 1, "REOPENED_A3_SHEET_ONE_TO_ONE_REQUIRED");
            Need(Convert.ToBoolean(sheet["frameFileReferencesEmpty"]) && Convert.ToBoolean(sheet["templateSketchEmpty"]) && Convert.ToBoolean(sheet["ordinaryAndTemplateSketchDistinct"]) && Convert.ToBoolean(sheet["editSheetMode"]), "REOPENED_TRIAL_FRAME_FORMAT_STATE_REQUIRED");
            var frame = DrawingProofMap(priorDrawingProof["trialFrame"]); var sketch = DrawingProofMap(sheet["ordinarySketch"]); var actualSegments = Items(sketch["segments"]).Select(DrawingProofMap).ToArray(); var usedSegments = new HashSet<int>();
            double[][][] expectedSegments = Items(frame["expectedSegmentsMetres"]).Select(v => Items(v).Select(Numbers).ToArray()).ToArray();
            foreach (double[][] pair in expectedSegments)
            {
                Need(pair.Length == 2 && pair.All(v => v.Length == 3), "REOPENED_PRIOR_FRAME_ENDPOINTS_REQUIRED");
                int[] matches = Enumerable.Range(0, actualSegments.Length).Where(i => Convert.ToBoolean(actualSegments[i]["isLine"]) && Convert.ToBoolean(actualSegments[i]["exactSketchMembership"]) && !Convert.ToBoolean(actualSegments[i]["construction"])
                    && (SameDrawingNumbers(pair[0], actualSegments[i]["startMetres"]) && SameDrawingNumbers(pair[1], actualSegments[i]["endMetres"]) || SameDrawingNumbers(pair[0], actualSegments[i]["endMetres"]) && SameDrawingNumbers(pair[1], actualSegments[i]["startMetres"]))).ToArray();
                Need(matches.Length == 1 && usedSegments.Add(matches[0]), "REOPENED_FRAME_LITERAL_ENDPOINT_UNIQUE_MATCH_REQUIRED");
            }
            Need(expectedSegments.Length == 8 && usedSegments.Count == 8 && Convert.ToInt32(sketch["segmentCount"]) == 8 && Convert.ToInt32(sketch["lineCount"]) == 8 && Convert.ToInt32(sketch["endpointCount"]) == 16 && Convert.ToInt32(sketch["blockInstanceCount"]) == 0 && Convert.ToInt32(sketch["pictureCount"]) == 0, "REOPENED_EIGHT_REAL_FRAME_LINES_REQUIRED");
            report["frameLiteralEndpointsMatched"] = true;
            sheetView = document.GetFirstView() as SwView; Need(sheetView != null, "REOPENED_SHEET_VIEW_REQUIRED"); sheetNotes = Items(sheetView.GetNotes());
            SwView current = sheetView.GetNextView() as SwView;
            while (current != null) { currentViews.Add(current); current = current.GetNextView() as SwView; }
            var priorViews = Items(priorDrawingProof["views"]).Select(DrawingProofMap).ToArray(); var viewRows = new List<object>(); var seen = new HashSet<int>();
            Need(currentViews.Count == 2 && priorViews.Length == 2, "REOPENED_TWO_NATIVE_MODEL_VIEWS_REQUIRED"); SwView flat = null;
            string expectedModel = Local(Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(Convert.ToString(priorDrawingProof["modelPath"], Invariant))));
            Need(File.Exists(expectedModel) && Hash(expectedModel) == Convert.ToString(priorDrawingProof["modelSha256"], Invariant), "REOPENED_DRAWING_MODEL_HASH_CHANGED");
            foreach (SwView view in currentViews)
            {
                int[] matches = Enumerable.Range(0, priorViews.Length).Where(i => Convert.ToString(priorViews[i]["name"], Invariant) == view.GetName2()).ToArray();
                Need(matches.Length == 1 && seen.Add(matches[0]), "REOPENED_VIEW_UNIQUE_PRIOR_MATCH_REQUIRED"); var prior = priorViews[matches[0]];
                MathTransform transform = view.ModelToViewTransform;
                try
                {
                    Need(transform != null && view.UseSheetScale == 0 && Convert.ToInt32(prior["useSheetScale"]) == 0 && Math.Abs(view.ScaleDecimal - Convert.ToDouble(prior["scale"], Invariant)) < Tol && SameDrawingNumbers(view.Position, prior["positionMetres"]) && SameDrawingNumbers(view.GetOutline(), prior["outlineMetres"]) && SameDrawingNumbers(transform.ArrayData, prior["transform"]), "REOPENED_VIEW_SCALE_POSITION_OUTLINE_OR_TRANSFORM_CHANGED");
                    Need(view.ReferencedConfiguration == Convert.ToString(prior["configuration"], Invariant) && view.IsFlatPatternView() == Convert.ToBoolean(prior["flatPattern"]), "REOPENED_VIEW_CONFIGURATION_OR_KIND_CHANGED");
                    viewRows.Add(ViewProof(view, expectedModel)); if (view.IsFlatPatternView()) { Need(flat == null, "REOPENED_ONE_FLAT_VIEW_REQUIRED"); flat = view; }
                }
                finally { Release(transform); }
            }
            report["views"] = viewRows; Need(flat != null && seen.Count == 2 && flat.ShowSheetMetalBendNotes, "REOPENED_FLAT_VIEW_WITH_BEND_NOTES_REQUIRED");
            var priorLayout = DrawingProofMap(priorDrawingProof["nativeBendNoteLayout"]); var priorNotes = Items(priorLayout["notes"]).Select(DrawingProofMap).ToArray(); object[] notes = Items(flat.GetNotes()); var noteRows = new List<object>(); var usedNotes = new HashSet<int>(); var boxes = new List<double[]>(); var otherBoxes = new List<double[]>();
            try
            {
                int[] documentPreferences = BendDocumentPreferences(drawing);
                Need(documentPreferences.SequenceEqual(BendDocumentTargets()) && SameDrawingNumbers(documentPreferences.Select(v => (double)v).ToArray(), priorLayout["documentPreferences"]), "REOPENED_BEND_DOCUMENT_PREFERENCES_CHANGED"); report["bendDocumentPreferences"] = documentPreferences;
                Need(flat.GetBendLineCount() == 12 && Convert.ToInt32(priorLayout["nativeLineCount"]) == 12 && priorNotes.Length == 12 && notes.OfType<Note>().Count(v => v.IsBendLineNote) == 12, "REOPENED_TWELVE_NATIVE_BEND_LINES_AND_NOTES_REQUIRED");
                foreach (var priorNote in Items(priorDrawingProof["notes"]).Select(DrawingProofMap))
                {
                    Note[] matches = sheetNotes.OfType<Note>().Where(v => NoteText(v.GetText()) == NoteText(Convert.ToString(priorNote["text"], Invariant))).ToArray(); Need(matches.Length == 1, "REOPENED_SHEET_NOTE_UNIQUE_TEXT_REQUIRED"); otherBoxes.Add(VisibleNoteExtent(matches[0]));
                }
                foreach (Note note in notes.OfType<Note>().Where(v => v.IsBendLineNote))
                {
                    VariableTrial.RuntimeOwnership(); Annotation annotation = note.GetAnnotation() as Annotation; TextFormat format = null; object[] attached = new object[0];
                    try
                    {
                        Need(annotation != null && !annotation.IsDangling() && annotation.Visible == (int)swAnnotationVisibilityState_e.swAnnotationVisible, "REOPENED_BEND_NOTE_VISIBLE_NON_DANGLING_REQUIRED");
                        int[] matches = Enumerable.Range(0, priorNotes.Length).Where(i => Convert.ToString(priorNotes[i]["annotationName"], Invariant) == annotation.GetName()).ToArray(); Need(matches.Length == 1 && usedNotes.Add(matches[0]), "REOPENED_BEND_NOTE_UNIQUE_PRIOR_MATCH_REQUIRED"); var prior = priorNotes[matches[0]];
                        bool up = false; double angle = 0, radius = 0; object points = null; Need(note.GetBendLineValues2(ref up, ref angle, ref radius, ref points), "REOPENED_NATIVE_BEND_VALUES_REQUIRED");
                        Need(note.GetText() == Convert.ToString(prior["text"], Invariant) && up == Convert.ToBoolean(prior["nativeUp"]) && Math.Abs(angle - Convert.ToDouble(prior["angleRadians"], Invariant)) < Tol && Math.Abs(radius - Convert.ToDouble(prior["radiusMetres"], Invariant)) < Tol && SameDrawingNumbers(points, prior["nativeBendPoints"]), "REOPENED_BEND_NOTE_TEXT_DIRECTION_OR_NATIVE_VALUES_CHANGED");
                        format = annotation.GetTextFormat(0) as TextFormat; var priorFont = DrawingProofMap(prior["font"]); double[] position = Numbers(annotation.GetPosition()), box = VisibleNoteExtent(note);
                        var sourceFont = DrawingProofMap(priorFont["sourceFormat"]); var targetFont = new Dictionary<string, object>(sourceFont); targetFont["escapement"] = 0.0; var savedFont = DrawingProofMap(priorFont["actualFormat"]);
                        Need(format != null && format.TypeFaceName == Convert.ToString(priorFont["name"], Invariant) && format.TypeFaceName == "汉仪长仿宋体" && Math.Abs(format.CharHeight - Convert.ToDouble(priorFont["heightMetres"], Invariant)) < Tol && Math.Abs(format.CharHeight - 0.0035) < Tol && Convert.ToBoolean(priorFont["sourceFontPreservedExceptEscapement"]) && Convert.ToDouble(priorFont["targetEscapementRadians"], Invariant) == 0 && Convert.ToBoolean(priorFont["useDocumentFormat"]) == Convert.ToBoolean(priorFont["sourceUseDocumentFormat"]) && annotation.GetUseDocTextFormat(0) == Convert.ToBoolean(priorFont["useDocumentFormat"]) && !note.HasMultipleFonts && SameNativeFontFormat(targetFont, savedFont) && SameNativeFontFormat(savedFont, PdfProbeFormatValues(format)) && Math.Abs(note.Angle - Convert.ToDouble(prior["noteAngleRadians"], Invariant)) < Tol && Math.Abs(note.Angle) < Tol && SameDrawingNumbers(position, prior["actualPositionMetres"]) && SameDrawingNumbers(box, prior["textExtentMetres"]), "REOPENED_BEND_NOTE_SOURCE_FONT_POSITION_ANGLE_OR_EXTENT_CHANGED");
                        Need(annotation.GetLeaderStyle() == Convert.ToInt32(prior["leaderStyle"]) && annotation.GetLeaderStyle() == (int)swLeaderStyle_e.swBENT && annotation.GetLeaderCount() > 0, "REOPENED_BEND_NOTE_BENT_LEADER_REQUIRED");
                        double[][] leaders = Enumerable.Range(0, annotation.GetLeaderCount()).Select(i => Numbers(annotation.GetLeaderPointsAtIndex(i))).ToArray(), priorLeaders = Items(prior["leaderPointsMetres"]).Select(Numbers).ToArray();
                        Need(leaders.Length == priorLeaders.Length && Enumerable.Range(0, leaders.Length).All(i => leaders[i].Length == 9 && SameDrawingNumbers(leaders[i], priorLeaders[i])), "REOPENED_BEND_NOTE_LEADER_GEOMETRY_CHANGED");
                        attached = Items(annotation.GetAttachedEntities3()); var attachmentProof = NativeBendAttachmentReadback(sw, flat, annotation, attached); var actualAttachments = attachmentProof.ToArray();
                        Need(prior.ContainsKey("attachments"), "REOPENED_PRIOR_BEND_ATTACHMENT_PROOF_REQUIRED 未验证：缺保存前折弯注附着的原生几何证明。"); var priorAttachments = Items(prior["attachments"]).Select(DrawingProofMap).ToArray(); var usedAttachments = new HashSet<int>();
                        foreach (var actual in actualAttachments)
                        {
                            int[] attachmentMatches = Enumerable.Range(0, priorAttachments.Length).Where(i => Convert.ToInt32(actual["entityType"]) == Convert.ToInt32(priorAttachments[i]["entityType"]) && Convert.ToString(actual["view"], Invariant) == Convert.ToString(priorAttachments[i]["view"], Invariant) && SameNativeBendSegment(DrawingProofMap(actual["geometry"]), DrawingProofMap(priorAttachments[i]["geometry"]))).ToArray();
                            Need(attachmentMatches.Length == 1 && usedAttachments.Add(attachmentMatches[0]), "REOPENED_BEND_ATTACHMENT_UNIQUE_PRIOR_GEOMETRY_MATCH_REQUIRED");
                        }
                        Need(attached.Length == Convert.ToInt32(prior["attachmentCount"]) && actualAttachments.Length == priorAttachments.Length && usedAttachments.Count == priorAttachments.Length, "REOPENED_BEND_ATTACHMENT_COUNT_CHANGED");
                        Need(box[0] >= 0.334 && box[3] <= 0.405 && box[1] >= 0.120 && box[4] <= 0.225 && boxes.All(v => !NoteBoxesOverlap(box, v)) && otherBoxes.All(v => !NoteBoxesOverlap(box, v)), "REOPENED_BEND_NOTE_OUTSIDE_COLUMN_OR_TEXT_OVERLAP"); boxes.Add(box);
                        noteRows.Add(new { annotationName = annotation.GetName(), text = note.GetText(), nativeUp = up, angleRadians = angle, radiusMetres = radius, nativeBendPoints = Numbers(points), actualPositionMetres = position, textExtentMetres = box, font = new { name = format.TypeFaceName, heightMetres = format.CharHeight, useDocumentFormat = annotation.GetUseDocTextFormat(0), actualFormat = PdfProbeFormatValues(format), savedFormatMatched = true }, noteAngleRadians = note.Angle, leaderStyle = annotation.GetLeaderStyle(), leaderPointsMetres = leaders, attachments = attachmentProof, uniquePriorDataMatch = true, currentViewAttachmentGeometryVerified = true });
                    }
                    finally { foreach (object entity in attached) Release(entity); Release(format); Release(annotation); }
                }
                Need(usedNotes.Count == 12, "REOPENED_ALL_TWELVE_BEND_NOTES_REQUIRED"); report["nativeBendNotes"] = noteRows;
            }
            finally { foreach (object note in notes) Release(note); }
            report["pass"] = true; report["coldReopenVerified"] = true;
        }
        catch (Exception error) { failures.Add(error.ToString()); }
        finally
        {
            foreach (object note in sheetNotes) Release(note); foreach (SwView view in currentViews) Release(view); Release(sheetView);
            if (beforeHash != null) { bool unchanged = File.Exists(path) && Hash(path) == beforeHash; report["drawingDiskUnchanged"] = unchanged; if (!unchanged) { report["pass"] = false; report["coldReopenVerified"] = false; failures.Add("REOPENED_DRAWING_DISK_CHANGED"); } }
        }
        report["errors"] = failures; report["limitations"] = new[] { "Native bend direction and attachment geometry are compared with saved evidence; manufacturing orientation and PDF leader routing still require independent review." }; return report;
    }
    public static object MakeDrawing(ISldWorks sw, ModelDoc2 model, string outputDir, string hand)
    {
        VariableTrial.RuntimeOwnership();
        Need(sw != null && (hand == "left" || hand == "right"), "LEFT_OR_RIGHT_DRAWING_HAND_REQUIRED");
        string input = InputPath(model), inputHash = Hash(input), originalConfig = Configuration(model);
        string[] originalConfigurations = Items(model.GetConfigurationNames()).Select(Convert.ToString).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        string baseConfig = originalConfigurations.Contains("Default") ? "Default" : originalConfigurations.FirstOrDefault(v => v.IndexOf("FLAT", StringComparison.OrdinalIgnoreCase) < 0);
        Need(baseConfig != null && originalConfigurations.Any(v => v.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) >= 0), "SAVED_FOLDED_AND_FLAT_CONFIGURATIONS_REQUIRED_NO_PART_SAVE_ALLOWED");
        string directory = Local(Path.Combine(outputDir, "path-check")); directory = Path.GetDirectoryName(directory);
        string stem = "door_" + hand;
        string drawingPath = OutputPath(model, Path.Combine(directory, stem + ".SLDDRW")), pdfPath = OutputPath(model, Path.Combine(directory, stem + ".pdf"));
        string template = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
        Need(!String.IsNullOrWhiteSpace(template) && File.Exists(template), "EXISTING_DEFAULT_DRAWING_TEMPLATE_REQUIRED");
        string templateHash = Hash(template); VariableTrial.RuntimeOwnership(); Directory.CreateDirectory(directory);
        SessionIdentity session = CaptureSession(sw);
        ModelDoc2 previous = sw.ActiveDoc as ModelDoc2, drawing = null;
        SwView folded = null, flat = null; List<NativeBendNoteState> bendNotes = null;
        bool oldVisible = sw.Visible, oldUserControl = sw.UserControl, oldInput = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate);
        object[] originalDocuments = Items(sw.GetDocuments()); var originalVisibility = new List<KeyValuePair<ModelDoc2, bool>>();
        foreach (object value in originalDocuments) { ModelDoc2 document = value as ModelDoc2; Need(document != null, "DRAWING_ORIGINAL_DOCUMENT_IDENTITY_REQUIRED"); originalVisibility.Add(new KeyValuePair<ModelDoc2, bool>(document, document.Visible)); }
        var trace = new DxfTrace(Path.Combine(directory, stem + "-drawing"));
        trace.Record("drawing_begin", new { modelPath = input, modelSha256 = inputHash, hand = hand, originalConfiguration = originalConfig, applicationVisible = oldVisible,
            userControl = oldUserControl, originalDocuments = originalVisibility.Select(v => new { path = v.Key.GetPathName(), title = v.Key.GetTitle(), visible = v.Value }).ToArray() });
        Exception failure = null; var cleanupErrors = new List<Exception>();
        Dictionary<string, object> report = null;
        try
        {
            sw.Visible = true;
            ActivateConfiguration(model, baseConfig, "FOLDED_CONFIGURATION_ACTIVATION_FAILED");
            VariableTrial.RuntimeOwnership();
            drawing = sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297) as ModelDoc2;
            Need(drawing != null, "NEW_NATIVE_DRAWING_FAILED"); VariableTrial.RegisterOwned(drawing);
            VariableTrial.RuntimeOwnership();
            Need(drawing is DrawingDoc, "NEW_NATIVE_DRAWING_TYPE_REQUIRED"); drawing.Visible = true;
            DrawingDoc document = (DrawingDoc)drawing; Dictionary<string, object> trialFrameProof = CreateTrialDrawingFrame(sw, drawing, trace);
            int activateError = 0; ModelDoc2 activated = sw.ActivateDoc3(drawing.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activateError) as ModelDoc2;
            Need(activated != null && activateError == 0, "NEW_DRAWING_ACTIVATION_FAILED");
            string[] names = Items(model.GetModelViewNames()).Select(Convert.ToString).ToArray();
            string orientation = names.FirstOrDefault(v => v == "*Back" || v == "*后视") ?? names.FirstOrDefault(v => v == "*Front" || v == "*前视");
            Need(orientation != null, "OBSERVED_NATIVE_FRONT_OR_BACK_VIEW_REQUIRED");
            VariableTrial.RuntimeOwnership();
            folded = document.CreateDrawViewFromModelView3(input, orientation, 0.090, 0.168, 0) as SwView;
            Need(folded != null && !folded.IsFlatPatternView() && folded.SetName2("折弯态"), "NATIVE_FOLDED_VIEW_CREATION_FAILED"); folded.ReferencedConfiguration = baseConfig;
            RestoreTrialSheetScale(sw, drawing, new[] { folded }, trace, "after-folded-view-creation");
            Fit(folded, 0.090, 0.168); Need(drawing.ForceRebuild3(false), "FOLDED_VIEW_REBUILD_FAILED");
            var dimensions = new List<object>(); dimensions.Add(Overall(sw, model, drawing, folded, true, trace)); dimensions.Add(Overall(sw, model, drawing, folded, false, trace));
            VariableTrial.RuntimeOwnership();
            flat = document.CreateFlatPatternViewFromModelView3(input, baseConfig, 0.245, 0.168, 0, false, false) as SwView;
            Need(flat != null && flat.IsFlatPatternView() && flat.SetName2("真实展开态"), "NATIVE_FLAT_VIEW_CREATION_FAILED");
            Need(originalConfigurations.Contains(flat.ReferencedConfiguration), "FLAT_VIEW_CREATED_UNSAVED_PART_CONFIGURATION_NO_PART_SAVE_ALLOWED " + flat.ReferencedConfiguration);
            RestoreTrialSheetScale(sw, drawing, new[] { folded, flat }, trace, "after-flat-view-creation");
            Fit(flat, 0.245, 0.168); flat.ShowSheetMetalBendNotes = true; Need(drawing.ForceRebuild3(false), "FLAT_VIEW_REBUILD_FAILED");
            dimensions.Add(Overall(sw, model, drawing, flat, true, trace)); dimensions.Add(Overall(sw, model, drawing, flat, false, trace));
            object bendProof = NativeBends(sw, model, flat, input, hand);
            var notes = new List<object>();
            object holeProof = NativeHoles(sw, model, drawing, flat, dimensions, notes, trace);
            string label = hand == "left" ? "左门板" : "右门板";
            notes.Add(NoteAt(sw, drawing, "16029 " + label + " 变量化独立试验", 0.025, 0.279, 0.004));
            notes.Add(NoteAt(sw, drawing, "折弯态 / 真实展开态；尺寸单位：mm；实体与标注均直接关联原生模型。", 0.025, 0.268, 0.0028));
            notes.Add(NoteAt(sw, drawing, "折弯态  1:" + (1 / folded.ScaleDecimal).ToString("0.###", Invariant), 0.042, 0.065, 0.003));
            notes.Add(NoteAt(sw, drawing, "真实展开态  1:" + (1 / flat.ScaleDecimal).ToString("0.###", Invariant), 0.205, 0.065, 0.003));
            notes.Add(NoteAt(sw, drawing, "图名：16029 " + label + "变量试验\n图号：" + stem + "\n版本：TRIAL   日期：2026-11-12   单位：mm", 0.025, 0.047, 0.0028));
            notes.Add(NoteAt(sw, drawing, "用途：变量联动与输出验证，未作生产放行。\n折弯方向以原生折弯注释为准；未核实方向不标注上/下。\n材料与公差以原始模型及正式工程要求为准。", 0.214, 0.047, 0.0027));
            notes.Add(NoteAt(sw, drawing, "Model: " + Path.GetFileName(input), 0.025, 0.020, 0.002));
            notes.Add(NoteAt(sw, drawing, "SHA256: " + inputHash, 0.025, 0.015, 0.002));
            bendNotes = LayoutNativeBendNotes(sw, drawing, flat, trace);
            Need(drawing.ForceRebuild3(false), "NATIVE_DRAWING_FINAL_REBUILD_FAILED");
            object drawingDisplayProof = PrepareDrawingDisplay(sw, drawing, new[] { folded, flat }, notes, true, trace);
            trace.Record("drawing_native_display_ready", drawingDisplayProof);
            trialFrameProof["beforeNativeSave"] = TrialDrawingFrameFinalReadback(sw, drawing, new[] { folded, flat }, trialFrameProof, trace, "native-save");
            object nativeBendNoteProof = NativeBendNoteLayoutReadback(sw, drawing, flat, bendNotes, notes, trace, "native-save");
            object foldedProof = ViewProof(folded, input), flatProof = ViewProof(flat, input);
            int drawingErrors = 0, drawingWarnings = 0;
            VariableTrial.RuntimeOwnership();
            Need(!File.Exists(drawingPath), "PRESERVE_EXISTING_DRAWING");
            bool saved = drawing.Extension.SaveAs(drawingPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref drawingErrors, ref drawingWarnings);
            trace.Record("drawing_native_save_result", new { returned = saved, errors = drawingErrors, warnings = drawingWarnings, path = drawingPath, exists = File.Exists(drawingPath), context = DrawingOutputContext(sw, drawing) });
            VariableTrial.RuntimeOwnership();
            Need(saved && drawingErrors == 0 && File.Exists(drawingPath) && !drawing.GetSaveFlag(), "NATIVE_SLDDRW_SAVE_FAILED " + drawingErrors + "/" + drawingWarnings);
            foldedProof = ViewProof(folded, input); flatProof = ViewProof(flat, input);
            ExportPdfData pdf = sw.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as ExportPdfData;
            int pdfErrors = 0, pdfWarnings = 0; bool pdfSaved = false; object pdfSheetProof = null, pdfDisplayProof = null, pdfBendNoteProof = null;
            try
            {
                Need(pdf != null, "NATIVE_PDF_EXPORT_DATA_REQUIRED"); pdf.ViewPdfAfterSaving = false; pdf.ExportAs3D = false;
                string[] sheetNames = Items(document.GetSheetNames()).Select(Convert.ToString).ToArray();
                Need(sheetNames.Length == 1 && sheetNames.All(v => !String.IsNullOrWhiteSpace(v)) && sheetNames.Distinct(StringComparer.Ordinal).Count() == sheetNames.Length, "ONE_EXACT_NAMED_PDF_DRAWING_SHEET_REQUIRED");
                var pdfSheets = new List<Sheet>(); object[] returnedSheets = new object[0]; DispatchWrapper[] wrappers = null;
                try
                {
                    foreach (string sheetName in sheetNames)
                    {
                        Need(document.ActivateSheet(sheetName), "PDF_EXACT_SHEET_ACTIVATION_FAILED " + sheetName);
                        Sheet selectedSheet = document.GetCurrentSheet() as Sheet; pdfSheets.Add(selectedSheet);
                        Need(selectedSheet != null && selectedSheet.GetName() == sheetName, "PDF_EXACT_CURRENT_SHEET_IDENTITY_FAILED " + sheetName);
                    }
                    // The official C# PDF example passes Sheet IDispatch wrappers for specified sheets.
                    wrappers = pdfSheets.Select(v => new DispatchWrapper(v)).ToArray();
                    bool selected = pdf.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportSpecifiedSheets, wrappers);
                    int which = pdf.GetWhichSheets(); returnedSheets = Items(pdf.GetSheets());
                    string[] returnedNames = returnedSheets.Select(v => v is Sheet ? ((Sheet)v).GetName() : Convert.ToString(v)).ToArray();
                    pdfSheetProof = new { method = "ExportSpecifiedSheets with official Sheet IDispatch array", returned = selected, which = which, requestedNames = sheetNames, returnedNames = returnedNames };
                    trace.Record("drawing_pdf_sheet_selection", pdfSheetProof);
                    Need(selected && which == (int)swExportDataSheetsToExport_e.swExportData_ExportSpecifiedSheets && returnedNames.Length == sheetNames.Length
                        && returnedNames.OrderBy(v => v, StringComparer.Ordinal).SequenceEqual(sheetNames.OrderBy(v => v, StringComparer.Ordinal)), "NATIVE_PDF_EXACT_SHEETS_SELECTION_OR_READBACK_FAILED");
                    pdfDisplayProof = PrepareDrawingDisplay(sw, drawing, new[] { folded, flat }, notes, false, trace);
                    trace.Record("drawing_pdf_display_ready", pdfDisplayProof);
                    trialFrameProof["beforePdfSave"] = TrialDrawingFrameFinalReadback(sw, drawing, new[] { folded, flat }, trialFrameProof, trace, "pdf-save");
                    pdfBendNoteProof = NativeBendNoteLayoutReadback(sw, drawing, flat, bendNotes, notes, trace, "pdf-save");
                    VariableTrial.RuntimeOwnership(); Need(!File.Exists(pdfPath), "PRESERVE_EXISTING_PDF");
                    trace.Record("drawing_pdf_save_begin", new { path = pdfPath, sheetSelection = pdfSheetProof, context = DrawingOutputContext(sw, drawing, true) });
                    pdfSaved = drawing.Extension.SaveAs(pdfPath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, pdf, ref pdfErrors, ref pdfWarnings);
                    GC.KeepAlive(wrappers);
                    trace.Record("drawing_pdf_save_result", new { returned = pdfSaved, errors = pdfErrors, warnings = pdfWarnings, path = pdfPath, exists = File.Exists(pdfPath), length = File.Exists(pdfPath) ? new FileInfo(pdfPath).Length : 0, context = DrawingOutputContext(sw, drawing) });
                    VariableTrial.RuntimeOwnership();
                }
                finally { foreach (object returnedSheet in returnedSheets) Release(returnedSheet); foreach (Sheet selectedSheet in pdfSheets) Release(selectedSheet); }
            }
            finally { Release(pdf); }
            Need(pdfSaved && pdfErrors == 0 && File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 100, "NATIVE_PDF_SAVE_FAILED " + pdfErrors + "/" + pdfWarnings);
            using (var stream = File.OpenRead(pdfPath)) { byte[] header = new byte[5]; Need(stream.Read(header, 0, 5) == 5 && Encoding.ASCII.GetString(header) == "%PDF-", "NATIVE_PDF_HEADER_INVALID"); }
            Need(Hash(input) == inputHash && Hash(template) == templateHash, "DRAWING_OUTPUT_CHANGED_INPUT_OR_TEMPLATE");
            Need(originalConfigurations.SequenceEqual(Items(model.GetConfigurationNames()).Select(Convert.ToString).OrderBy(v => v, StringComparer.Ordinal)), "DRAWING_CREATED_UNSAVED_PART_CONFIGURATION");
            report = new Dictionary<string, object>
            {
                { "success", true }, { "saved", saved }, { "path", drawingPath }, { "sha256", Hash(drawingPath) }, { "pdfPath", pdfPath }, { "pdfSha256", Hash(pdfPath) },
                { "errors", drawingErrors }, { "warnings", drawingWarnings }, { "pdfSaved", pdfSaved }, { "pdfErrors", pdfErrors }, { "pdfWarnings", pdfWarnings },
                { "pdfSheetSelection", pdfSheetProof },
                { "nativeDrawingDisplay", drawingDisplayProof }, { "pdfDrawingDisplay", pdfDisplayProof },
                { "nativeBendNoteLayout", nativeBendNoteProof }, { "pdfBendNoteLayout", pdfBendNoteProof },
                { "templatePath", template }, { "templateSha256", templateHash }, { "modelPath", input }, { "modelSha256", inputHash }, { "hand", hand },
                { "trialFrame", trialFrameProof }, { "frameFileReferencesEmpty", ((Dictionary<string, object>)trialFrameProof)["frameFileReferencesEmpty"] },
                { "views", new[] { foldedProof, flatProof } }, { "dimensions", dimensions }, { "notes", notes }, { "font", "汉仪长仿宋体" },
                { "holes", holeProof }, { "nativeBends", bendProof }, { "nativeBendLineCount", flat.GetBendLineCount() }, { "partSaved", false }, { "partDiskUnchanged", true }, { "templateUnchanged", true },
                { "diagnosticFiles", trace.Files },
                { "coldReopenVerified", false }, { "productionReleased", false }, { "limitations", new[] { "未验证：缺关闭后只读重开证据。", "未验证：缺 PDF 逐页视觉检查。", "折弯上/下方向未作独立验证；未自行编造方向。" } }
            };
        }
        catch (Exception error) { failure = error; trace.Record("drawing_primary_failure", new { error = error.ToString() }); }
        try { SameSession(sw, session); SameInput(model, input); ActivateConfiguration(model, originalConfig, "DRAWING_MODEL_CONFIGURATION_RESTORE_FAILED", false); }
        catch (Exception error) { cleanupErrors.Add(error); }
        try { SameSession(sw, session); if (previous != null) { int error = 0; Need(sw.ActivateDoc3(previous.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref error) != null && error == 0, "PREVIOUS_DOCUMENT_ACTIVATION_RESTORE_FAILED"); } }
        catch (Exception error) { cleanupErrors.Add(error); }
        bool userControlChangedDuringDrawing = false, applicationVisibilityRestored = false;
        try
        {
            SameSession(sw, session); Need(sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate) == oldInput, "DIMENSION_INPUT_PREFERENCE_CHANGED");
            userControlChangedDuringDrawing = sw.UserControl != oldUserControl;
            trace.Record("drawing_user_control_before_restore", new { original = oldUserControl, actual = sw.UserControl, changedDuringDrawing = userControlChangedDuringDrawing });
            if (userControlChangedDuringDrawing) sw.UserControl = oldUserControl;
            // ISldWorks.Visible can hide the application only after all visible documents are hidden.
            if (drawing != null) { drawing.Visible = false; Need(!drawing.Visible, "NEW_DRAWING_VISIBILITY_RESTORE_FAILED"); }
            foreach (var item in originalVisibility) { item.Key.Visible = item.Value; Need(item.Key.Visible == item.Value, "ORIGINAL_DOCUMENT_VISIBILITY_RESTORE_FAILED " + item.Key.GetTitle()); }
            trace.Record("drawing_visibility_before_application_restore", new { applicationVisible = sw.Visible, expectedApplicationVisible = oldVisible, userControl = sw.UserControl,
                originalDocuments = originalVisibility.Select(v => new { path = v.Key.GetPathName(), title = v.Key.GetTitle(), expectedVisible = v.Value, actualVisible = v.Key.Visible }).ToArray(), newDrawingVisible = drawing == null ? (bool?)null : drawing.Visible });
            bool visibleDocuments = originalVisibility.Any(v => v.Key.Visible) || drawing != null && drawing.Visible;
            if (oldVisible || !sw.UserControl && !visibleDocuments) { sw.Visible = oldVisible; Need(sw.Visible == oldVisible, "APPLICATION_VISIBILITY_RESTORE_FAILED"); applicationVisibilityRestored = true; }
            trace.Record("drawing_visibility_restored", new { applicationVisible = sw.Visible, expectedApplicationVisible = oldVisible, applicationVisibilityRestored = applicationVisibilityRestored,
                userControl = sw.UserControl, expectedUserControl = oldUserControl, userControlRestored = sw.UserControl == oldUserControl,
                deferredToOwnedSessionExit = !applicationVisibilityRestored || sw.UserControl != oldUserControl });
        }
        catch (Exception error) { cleanupErrors.Add(error); trace.Record("drawing_visibility_restore_failure", new { error = error.ToString() }); }
        try { Need(Hash(input) == inputHash && Hash(template) == templateHash, "DRAWING_OUTPUT_CHANGED_INPUT_OR_TEMPLATE"); }
        catch (Exception error) { cleanupErrors.Add(error); }
        ReleaseNativeBendNotes(bendNotes); Release(folded); Release(flat); Release(previous); foreach (object document in originalDocuments) Release(document);
        if (trace.Errors.Count > 0) cleanupErrors.Add(new InvalidOperationException("DRAWING_DIAGNOSTIC_WRITE_FAILED " + String.Join(" | ", trace.Errors)));
        if (failure != null) cleanupErrors.Insert(0, failure);
        if (cleanupErrors.Count > 0) throw new AggregateException("TRIAL_DRAWING_OUTPUT_FAILED; any newly created partial files were preserved for diagnosis", cleanupErrors);
        report["systemPreferencesChanged"] = false; report["originalConfigurationRestored"] = Configuration(model) == originalConfig; report["modelDirtyInMemoryAfter"] = model.GetSaveFlag();
        report["applicationState"] = new { originalVisible = oldVisible, actualVisible = sw.Visible, visibilityRestored = applicationVisibilityRestored,
            originalUserControl = oldUserControl, actualUserControl = sw.UserControl, userControlChangedDuringDrawing = userControlChangedDuringDrawing,
            userControlRestored = sw.UserControl == oldUserControl, deferredToOwnedSessionExit = !applicationVisibilityRestored || sw.UserControl != oldUserControl };
        return report;
    }
}
