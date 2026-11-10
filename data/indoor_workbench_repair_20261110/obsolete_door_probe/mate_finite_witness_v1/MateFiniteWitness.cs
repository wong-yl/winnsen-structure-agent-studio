// Preparation module only. It attaches to caller-owned interfaces; it never starts CAD.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace Winnsen.MateFiniteWitnessV1
{
    public sealed class TargetBinding
    {
        public string ActiveRootDocumentPath { get; set; }
        public string OwnerDocumentPath { get; set; }
        public string OwnerConfiguration { get; set; }
        public string ComponentDocumentPath { get; set; }
        public string ComponentConfiguration { get; set; }
        public string FeatureName { get; set; }
        public string FeaturePersistentReference { get; set; }
        public string OwnerFacePersistentReference { get; set; }
    }

    public sealed class VertexWitness
    {
        public string Id { get; set; }
        public double[] Point { get; set; }
    }

    public sealed class CurveWitness
    {
        public string Kind { get; set; }
        public int Identity { get; set; }
        public double[] AnalyticParameters { get; set; }
        public bool EndConditionsAvailable { get; set; }
        public double UnderlyingStart { get; set; }
        public double UnderlyingEnd { get; set; }
        public bool UnderlyingClosed { get; set; }
        public bool UnderlyingPeriodic { get; set; }
    }

    public sealed class EdgeWitness
    {
        public string Id { get; set; }
        public string StartVertexId { get; set; }
        public string EndVertexId { get; set; }
        public CurveWitness Curve { get; set; }
        public double[] TrimStartPoint { get; set; }
        public double[] TrimEndPoint { get; set; }
        public double UMin { get; set; }
        public double UMax { get; set; }
        public bool CurveAndEdgeSameSense { get; set; }
        public int TrimCurveType { get; set; }
        public int TrimCurveTag { get; set; }
        public double TrimLength { get; set; }
    }

    public sealed class CoEdgeWitness
    {
        public string EdgeId { get; set; }
        public bool SameSenseAsEdge { get; set; }
        // GetCurveParams slots 0..7 only. Slots 8/9 are documented as unused.
        public double[] UsedCurveParameters { get; set; }
        public bool HasIndependentCurve { get; set; }
        public CurveWitness IndependentCurve { get; set; }
    }

    public sealed class LoopWitness
    {
        public bool IsOuter { get; set; }
        public bool IsSingular { get; set; }
        public int ReportedCoEdgeCount { get; set; }
        public int ReportedEdgeCount { get; set; }
        public int ReportedVertexCount { get; set; }
        public string[] EdgeIds { get; set; }
        public string[] VertexIds { get; set; }
        // Traversed from GetFirstCoEdge/GetNext, including verification of cycle closure.
        public List<CoEdgeWitness> CoEdges { get; set; }
    }

    public sealed class FaceWitness
    {
        public string CoordinateSystem { get; set; }
        public string LocalPersistentReference { get; set; }
        public bool FaceInSurfaceSense { get; set; }
        public int SurfaceIdentity { get; set; }
        public bool IsCylinder { get; set; }
        public double[] CylinderParameters { get; set; }
        public double Area { get; set; }
        public int ReportedLoopCount { get; set; }
        public int ReportedEdgeCount { get; set; }
        public List<LoopWitness> Loops { get; set; }
        public List<EdgeWitness> Edges { get; set; }
        public List<VertexWitness> Vertices { get; set; }
        public FaceWitness()
        {
            Loops = new List<LoopWitness>(); Edges = new List<EdgeWitness>(); Vertices = new List<VertexWitness>();
        }
    }

    public sealed class CylinderWitness
    {
        public string Schema { get; set; }
        public string CaptureStage { get; set; }
        public bool CaptureComplete { get; set; }
        public List<string> CaptureIssues { get; set; }
        public bool ProfileSupported { get; set; }
        public List<string> SupportIssues { get; set; }
        public string ActiveRootDocumentPath { get; set; }
        public string ActiveRootConfiguration { get; set; }
        public string OwnerDocumentPath { get; set; }
        public string OwnerConfiguration { get; set; }
        public string FeatureName { get; set; }
        public string FeatureType { get; set; }
        public int FeatureId { get; set; }
        public string FeaturePersistentReference { get; set; }
        public int MateType { get; set; }
        public int MateAlignment { get; set; }
        public bool MateFlipped { get; set; }
        public bool MateCanBeFlipped { get; set; }
        public int EntityIndex { get; set; }
        public int EntityCount { get; set; }
        public int EntityReferenceType { get; set; }
        public string MateParameterCoordinateSystem { get; set; }
        public double[] MateParameters { get; set; }
        public string OwnerFacePersistentReference { get; set; }
        public string RoundTripOwnerFacePersistentReference { get; set; }
        public string ComponentName { get; set; }
        public string ComponentDocumentPath { get; set; }
        public string ComponentConfiguration { get; set; }
        public string ComponentPersistentReference { get; set; }
        public string ComponentTransformCoordinateSystem { get; set; }
        public double[] ComponentTransform { get; set; }
        public double[] ComponentTotalTransformWithoutPresentation { get; set; }
        public string LocalDocumentPath { get; set; }
        public string LocalConfiguration { get; set; }
        public bool LocalMappingRoundTripVerified { get; set; }
        public FaceWitness Face { get; set; }
        public CylinderWitness()
        {
            Schema = FiniteWitness.Schema; CaptureIssues = new List<string>(); SupportIssues = new List<string>();
        }
    }

    public sealed class ComparisonResult
    {
        public bool Equivalent { get; set; }
        public List<string> Reasons { get; set; }
        public string Scope { get; set; }
        public ComparisonResult() { Reasons = new List<string>(); Scope = FiniteWitness.Scope; }
    }

    public static class FiniteWitness
    {
        public const string Schema = "mate-finite-cylinder-band-witness-v1";
        public const string Scope = "restricted two-full-circle finite cylinder band only; not complete BRep or overall model acceptance";
        public const string LocalFrame = "underlying component document local; GetCorrespondingEntity2";
        public const string MateFrame = "owning assembly document; IMateEntity2.EntityParams";
        public const string TransformFrame = "root component of active assembly document; IComponent2.Transform2/GetTotalTransform(false)";
        public static readonly string[] TargetNames = { "同心1", "同心2", "同心3", "同心4" };
        private const string ComponentName = "箱体竖隔板L-1";
        private const double Absolute = 1e-12;
        private const double Relative = 1e-10;

        private static void Need(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static bool Near(double x, double y) { return Finite(x) && Finite(y) && Math.Abs(x - y) <= Absolute + Math.Max(Math.Abs(x), Math.Abs(y)) * Relative; }
        private static bool Numbers(double[] a, int count) { return a != null && a.Length == count && a.All(Finite); }
        private static bool Text(string x) { return !string.IsNullOrWhiteSpace(x); }
        private static string Full(string p) { Need(Text(p), "PATH_MISSING"); return Path.GetFullPath(p); }
        private static bool SamePath(string a, string b) { return string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase); }
        private static object[] Values(object raw) { return raw == null ? new object[0] : ((IEnumerable)raw).Cast<object>().ToArray(); }
        private static double[] ReadNumbers(object raw, int count, string context)
        {
            var a = Values(raw).Select(Convert.ToDouble).ToArray(); Need(Numbers(a, count), "NUMERIC_INPUT_INVALID " + context); return a;
        }
        private static string Persistent(ModelDocExtension extension, object value)
        {
            Need(extension != null && value != null, "PERSISTENT_INPUT_NULL");
            byte[] bytes = Values(extension.GetPersistReference3(value)).Select(Convert.ToByte).ToArray();
            Need(bytes.Length > 0, "PERSISTENT_REFERENCE_UNAVAILABLE"); return Convert.ToBase64String(bytes);
        }
        private static long Identity(object value)
        {
            Need(value != null && Marshal.IsComObject(value), "COM_IDENTITY_INPUT_INVALID");
            IntPtr p = Marshal.GetIUnknownForObject(value); try { return p.ToInt64(); } finally { Marshal.Release(p); }
        }
        private static string ConfigurationName(ModelDoc2 model)
        {
            Configuration c = model.GetActiveConfiguration() as Configuration;
            Need(c != null && Text(c.Name), "ACTIVE_CONFIGURATION_MISSING"); return c.Name;
        }

        // All arguments are existing caller-owned interfaces. No activation/open/rebuild/save calls.
        // No RCW is manually released: shared wrappers remain under the caller/runtime lifetime.
        public static CylinderWitness CaptureTarget(SldWorks app, ModelDoc2 owner, Feature feature, TargetBinding expected, string stage)
        {
            var w = new CylinderWitness { CaptureStage = stage };
            var held = new List<object>();
            try
            {
                Need(app != null && owner != null && feature != null && expected != null, "CAPTURE_INPUT_NULL");
                Need(Text(stage) && TargetNames.Contains(expected.FeatureName, StringComparer.Ordinal), "TARGET_OR_STAGE_UNBOUND");
                Need(Path.GetFileName(expected.OwnerDocumentPath).Equals("箱体竖隔板L焊接.SLDASM", StringComparison.OrdinalIgnoreCase), "OWNER_OUT_OF_SCOPE");
                Need(Path.GetFileName(expected.ComponentDocumentPath).Equals("箱体竖隔板L.sldprt", StringComparison.OrdinalIgnoreCase), "COMPONENT_OUT_OF_SCOPE");
                Need(Text(expected.OwnerConfiguration) && Text(expected.ComponentConfiguration) && Text(expected.FeaturePersistentReference) && Text(expected.OwnerFacePersistentReference), "EXPECTED_BINDING_MISSING");
                ModelDoc2 active = app.ActiveDoc as ModelDoc2; held.Add(active);
                Need(active != null && ((IModelDoc2)active).GetType() == 2 && SamePath(active.GetPathName(), expected.ActiveRootDocumentPath), "ACTIVE_ROOT_CONTEXT_MISMATCH");
                w.ActiveRootDocumentPath = Full(active.GetPathName()); w.ActiveRootConfiguration = ConfigurationName(active);
                Need(((IModelDoc2)owner).GetType() == 2 && SamePath(owner.GetPathName(), expected.OwnerDocumentPath), "OWNER_PATH_OR_TYPE_MISMATCH");
                w.OwnerDocumentPath = Full(owner.GetPathName()); w.OwnerConfiguration = ConfigurationName(owner);
                Need(w.OwnerConfiguration == expected.OwnerConfiguration, "OWNER_CONFIGURATION_MISMATCH");
                ModelDocExtension ownerExt = owner.Extension; held.Add(ownerExt);
                w.FeatureName = feature.Name; w.FeatureType = feature.GetTypeName2(); w.FeatureId = feature.GetID();
                w.FeaturePersistentReference = Persistent(ownerExt, feature);
                Need(w.FeatureName == expected.FeatureName && w.FeatureType == "MateConcentric" && !feature.IsSuppressed() && w.FeaturePersistentReference == expected.FeaturePersistentReference, "FEATURE_BINDING_MISMATCH");
                Mate2 mate = feature.GetSpecificFeature2() as Mate2; held.Add(mate); Need(mate != null, "MATE_NULL");
                w.MateType = mate.Type; w.MateAlignment = mate.Alignment; w.MateFlipped = mate.Flipped; w.MateCanBeFlipped = mate.CanBeFlipped;
                w.EntityIndex = 0; w.EntityCount = mate.GetMateEntityCount(); Need(w.MateType == 1 && w.EntityCount == 2, "MATE_TYPE_OR_COUNT_MISMATCH");
                MateEntity2 entity = mate.MateEntity(0); held.Add(entity); Need(entity != null, "MATE_ENTITY_NULL");
                w.EntityReferenceType = entity.ReferenceType; Need(w.EntityReferenceType == 4, "MATE_ENTITY_NOT_CYLINDER");
                w.MateParameterCoordinateSystem = MateFrame; w.MateParameters = ReadNumbers(entity.EntityParams, 8, "mate");
                Component2 component = entity.ReferenceComponent; held.Add(component); Need(component != null && !component.IsVirtual && !component.IsSuppressed(), "REFERENCE_COMPONENT_UNAVAILABLE");
                w.ComponentName = component.Name2; w.ComponentDocumentPath = Full(component.GetPathName()); w.ComponentConfiguration = component.ReferencedConfiguration;
                Need(w.ComponentName == ComponentName && SamePath(w.ComponentDocumentPath, expected.ComponentDocumentPath) && w.ComponentConfiguration == expected.ComponentConfiguration, "COMPONENT_BINDING_MISMATCH");
                w.ComponentPersistentReference = Persistent(ownerExt, component);
                object raw = entity.Reference; held.Add(raw); Face2 ownerFace = raw as Face2; Need(ownerFace != null, "MATE_REFERENCE_NOT_FACE2");
                w.OwnerFacePersistentReference = Persistent(ownerExt, ownerFace); Need(w.OwnerFacePersistentReference == expected.OwnerFacePersistentReference, "OWNER_FACE_BINDING_MISMATCH");
                Component2 faceComponent = ((Entity)ownerFace).GetComponent() as Component2; held.Add(faceComponent);
                Need(faceComponent != null && Identity(faceComponent) == Identity(component), "FACE_REFERENCE_COMPONENT_MISMATCH");
                w.ComponentTransformCoordinateSystem = TransformFrame;
                MathTransform transform = component.Transform2, total = component.GetTotalTransform(false); held.Add(transform); held.Add(total);
                Need(transform != null && total != null, "COMPONENT_TRANSFORM_NULL");
                w.ComponentTransform = ReadNumbers(transform.ArrayData, 16, "Transform2");
                w.ComponentTotalTransformWithoutPresentation = ReadNumbers(total.ArrayData, 16, "GetTotalTransform(false)");
                ModelDoc2 local = component.GetModelDoc2() as ModelDoc2; held.Add(local);
                Need(local != null && ((IModelDoc2)local).GetType() == 1 && SamePath(local.GetPathName(), w.ComponentDocumentPath), "LOCAL_PART_DOCUMENT_MISMATCH");
                w.LocalDocumentPath = Full(local.GetPathName()); w.LocalConfiguration = ConfigurationName(local);
                Need(w.LocalConfiguration == w.ComponentConfiguration, "LOCAL_CONFIGURATION_MISMATCH");
                ModelDocExtension localExt = local.Extension; held.Add(localExt);
                Face2 localFace = localExt.GetCorrespondingEntity2(ownerFace) as Face2; held.Add(localFace); Need(localFace != null, "LOCAL_FACE_MAPPING_NULL");
                object unexpectedComponent = ((Entity)localFace).GetComponent(); held.Add(unexpectedComponent); Need(unexpectedComponent == null, "LOCAL_FACE_STILL_IN_ASSEMBLY_CONTEXT");
                object back = component.GetCorrespondingEntity(localFace); held.Add(back); Need(back is Face2, "OWNER_FACE_ROUNDTRIP_NULL");
                w.RoundTripOwnerFacePersistentReference = Persistent(ownerExt, back);
                Need(w.RoundTripOwnerFacePersistentReference == w.OwnerFacePersistentReference, "OWNER_FACE_ROUNDTRIP_ID_MISMATCH");
                w.LocalMappingRoundTripVerified = true;
                w.Face = new FaceWitness { CoordinateSystem = LocalFrame, LocalPersistentReference = Persistent(localExt, localFace) };
                CaptureFace(localFace, localExt, w.Face, held);
                w.CaptureComplete = true;
            }
            catch (Exception ex) { w.CaptureIssues.Add("CAPTURE_FAILED " + ex.GetType().FullName + ": " + ex.Message); }
            finally { GC.KeepAlive(held); }
            w.SupportIssues = SupportIssues(w); w.ProfileSupported = w.SupportIssues.Count == 0;
            return w;
        }

        private static CurveWitness ReadCurve(Curve curve)
        {
            Need(curve != null, "EDGE_CURVE_NULL");
            var c = new CurveWitness { Identity = curve.Identity(), Kind = "unsupported", AnalyticParameters = new double[0] };
            if (curve.IsCircle()) { c.Kind = "circle"; c.AnalyticParameters = ReadNumbers(curve.CircleParams, 7, "CircleParams"); }
            else if (curve.IsLine()) { c.Kind = "line-diagnostic-only"; c.AnalyticParameters = ReadNumbers(curve.LineParams, 6, "LineParams"); }
            double start, end; bool closed, periodic;
            c.EndConditionsAvailable = curve.GetEndParams(out start, out end, out closed, out periodic);
            c.UnderlyingStart = start; c.UnderlyingEnd = end; c.UnderlyingClosed = closed; c.UnderlyingPeriodic = periodic;
            return c;
        }

        private static string AddVertex(Vertex vertex, ModelDocExtension extension, FaceWitness f, List<object> held)
        {
            if (vertex == null) return null; held.Add(vertex);
            string id = Persistent(extension, vertex); double[] point = ReadNumbers(vertex.GetPoint(), 3, "Vertex.GetPoint");
            VertexWitness prior = f.Vertices.SingleOrDefault(v => v.Id == id);
            if (prior == null) f.Vertices.Add(new VertexWitness { Id = id, Point = point });
            else Need(prior.Point.Zip(point, Near).All(x => x), "VERTEX_ID_POSITION_CONFLICT");
            return id;
        }

        private static void CaptureFace(Face2 face, ModelDocExtension extension, FaceWitness f, List<object> held)
        {
            Surface surface = face.GetSurface() as Surface; held.Add(surface); Need(surface != null, "SURFACE_NULL");
            f.SurfaceIdentity = surface.Identity(); f.IsCylinder = surface.IsCylinder();
            f.CylinderParameters = f.IsCylinder ? ReadNumbers(surface.CylinderParams, 7, "CylinderParams") : new double[0];
            f.FaceInSurfaceSense = face.FaceInSurfaceSense(); f.Area = face.GetArea();
            f.ReportedLoopCount = face.GetLoopCount(); f.ReportedEdgeCount = face.GetEdgeCount();
            object[] edges = Values(face.GetEdges()); Need(edges.Length == f.ReportedEdgeCount && edges.Length > 0 && edges.Length <= 256, "FACE_EDGE_COUNT_INVALID");
            var edgeIdentities = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (object value in edges)
            {
                held.Add(value); Edge edge = value as Edge; Need(edge != null, "EDGE_INTERFACE_NULL");
                var e = new EdgeWitness { Id = Persistent(extension, edge) };
                Need(!edgeIdentities.ContainsKey(e.Id), "DUPLICATE_FACE_EDGE_ID"); edgeIdentities[e.Id] = Identity(edge);
                f.Edges.Add(e);
                Curve curve = edge.GetCurve() as Curve; held.Add(curve); e.Curve = ReadCurve(curve);
                CurveParamData p = edge.GetCurveParams3(); held.Add(p); Need(p != null, "EDGE_TRIM_PARAMETERS_NULL");
                e.TrimStartPoint = ReadNumbers(p.StartPoint, 3, "CurveParamData.StartPoint"); e.TrimEndPoint = ReadNumbers(p.EndPoint, 3, "CurveParamData.EndPoint");
                e.UMin = p.UMinValue; e.UMax = p.UMaxValue; e.CurveAndEdgeSameSense = p.Sense; e.TrimCurveType = p.CurveType; e.TrimCurveTag = p.CurveTag;
                e.TrimLength = curve.GetLength3(e.UMin, e.UMax);
                e.StartVertexId = AddVertex(edge.GetStartVertex() as Vertex, extension, f, held);
                e.EndVertexId = AddVertex(edge.GetEndVertex() as Vertex, extension, f, held);
            }
            object[] loops = Values(face.GetLoops()); Need(loops.Length == f.ReportedLoopCount && loops.Length > 0 && loops.Length <= 256, "FACE_LOOP_COUNT_INVALID");
            var seenLoops = new HashSet<long>();
            foreach (object value in loops)
            {
                held.Add(value); Loop2 loop = value as Loop2; Need(loop != null && seenLoops.Add(Identity(loop)), "LOOP_INTERFACE_OR_ID_INVALID");
                var l = new LoopWitness { IsOuter = loop.IsOuter(), IsSingular = loop.IsSingular(), ReportedCoEdgeCount = loop.GetCoEdgeCount(), ReportedEdgeCount = loop.GetEdgeCount(), ReportedVertexCount = loop.GetVertexCount(), CoEdges = new List<CoEdgeWitness>() };
                f.Loops.Add(l);
                object[] loopEdges = Values(loop.GetEdges()); Need(loopEdges.Length == l.ReportedEdgeCount, "LOOP_EDGE_COUNT_MISMATCH");
                var ids = new List<string>();
                foreach (object e in loopEdges)
                {
                    held.Add(e); string id = Persistent(extension, e); long identity;
                    Need(edgeIdentities.TryGetValue(id, out identity) && identity == Identity(e), "LOOP_EDGE_NOT_IN_FACE"); ids.Add(id);
                }
                l.EdgeIds = ids.ToArray();
                object[] vertices = Values(loop.GetVertices()); Need(vertices.Length == l.ReportedVertexCount, "LOOP_VERTEX_COUNT_MISMATCH");
                l.VertexIds = vertices.Select(v => AddVertex(v as Vertex, extension, f, held)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                object[] listedCoEdges = Values(loop.GetCoEdges()); Need(listedCoEdges.Length == l.ReportedCoEdgeCount && listedCoEdges.Length <= 512, "LOOP_COEDGE_COUNT_INVALID");
                var listed = new HashSet<long>(); foreach (object c in listedCoEdges) { held.Add(c); Need(listed.Add(Identity(c)), "DUPLICATE_LISTED_COEDGE"); }
                CoEdge current = loop.GetFirstCoEdge() as CoEdge; held.Add(current);
                if (listed.Count == 0) { Need(current == null && l.IsSingular, "EMPTY_NONSINGULAR_LOOP"); continue; }
                Need(current != null, "FIRST_COEDGE_NULL"); long first = Identity(current); var visited = new HashSet<long>();
                while (true)
                {
                    long id = Identity(current); Need(listed.Contains(id) && visited.Add(id), "COEDGE_CYCLE_INVALID");
                    Edge edge = current.GetEdge() as Edge; held.Add(edge); Need(edge != null, "COEDGE_EDGE_NULL"); string edgeId = Persistent(extension, edge); long edgeIdentity;
                    Need(edgeIdentities.TryGetValue(edgeId, out edgeIdentity) && edgeIdentity == Identity(edge), "COEDGE_EDGE_NOT_IN_FACE");
                    var c = new CoEdgeWitness { EdgeId = edgeId, SameSenseAsEdge = current.GetSense() }; l.CoEdges.Add(c);
                    object[] raw = Values(current.GetCurveParams()); Need(raw.Length == 10, "COEDGE_PARAMETER_COUNT_INVALID");
                    c.UsedCurveParameters = ReadNumbers(raw.Take(8).ToArray(), 8, "CoEdge.GetCurveParams used slots");
                    Curve independent = current.GetCurve() as Curve; held.Add(independent); c.HasIndependentCurve = independent != null;
                    if (independent != null) c.IndependentCurve = ReadCurve(independent);
                    current = current.GetNext() as CoEdge; held.Add(current); Need(current != null, "NEXT_COEDGE_NULL");
                    if (Identity(current) == first) break;
                    Need(visited.Count < listed.Count, "COEDGE_CYCLE_DID_NOT_CLOSE");
                }
                Need(visited.SetEquals(listed), "COEDGE_ENUMERATION_TRAVERSAL_MISMATCH");
            }
            f.Edges = f.Edges.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
            f.Vertices = f.Vertices.OrderBy(v => v.Id, StringComparer.Ordinal).ToList();
            f.Loops = f.Loops.OrderBy(l => string.Join("|", l.CoEdges.Select(c => c.EdgeId)), StringComparer.Ordinal).ToList();
        }

        private static double Dot(double[] a, double[] b) { return a.Zip(b, (x, y) => x * y).Sum(); }
        private static double[] Sub(double[] a, double[] b) { return a.Zip(b, (x, y) => x - y).ToArray(); }
        private static double[] Point(double[] a) { return a.Take(3).ToArray(); }
        private static double[] Axis(double[] a) { return a.Skip(3).Take(3).ToArray(); }
        private static bool Unit(double[] a) { return Numbers(a, 3) && Near(Dot(a, a), 1); }
        private static bool SamePoint(double[] a, double[] b) { return Numbers(a, 3) && Numbers(b, 3) && a.Zip(b, Near).All(x => x); }
        private static bool OnAxis(double[] point, double[] origin, double[] unit)
        {
            double[] delta = Sub(point, origin); double axial = Dot(delta, unit);
            return delta.Zip(unit, (d, u) => Near(d - axial * u, 0)).All(x => x);
        }
        private static bool OnCircle(double[] point, double[] circle)
        {
            if (!Numbers(point, 3) || !Numbers(circle, 7) || !Unit(Axis(circle))) return false;
            double[] delta = Sub(point, Point(circle));
            return Near(Dot(delta, Axis(circle)), 0) && Near(Math.Sqrt(Dot(delta, delta)), circle[6]);
        }
        private static void Check(List<string> issues, bool valid, string code) { if (!valid) issues.Add(code); }

        // Validation recomputes support; ProfileSupported is never trusted as evidence.
        public static List<string> SupportIssues(CylinderWitness w)
        {
            var issues = new List<string>();
            if (w == null) { issues.Add("WITNESS_NULL"); return issues; }
            Check(issues, w.Schema == Schema && Text(w.CaptureStage) && w.CaptureComplete && w.CaptureIssues != null && w.CaptureIssues.Count == 0, "CAPTURE_INCOMPLETE_OR_SCHEMA_INVALID");
            Check(issues, Text(w.ActiveRootDocumentPath) && Text(w.ActiveRootConfiguration) && Text(w.OwnerDocumentPath) && Text(w.OwnerConfiguration) && Text(w.LocalDocumentPath) && Text(w.LocalConfiguration), "DOCUMENT_CONTEXT_MISSING");
            Check(issues, TargetNames.Contains(w.FeatureName, StringComparer.Ordinal) && w.FeatureType == "MateConcentric" && w.MateType == 1 && w.EntityIndex == 0 && w.EntityCount == 2 && w.EntityReferenceType == 4, "TARGET_BINDING_INVALID");
            Check(issues, w.ComponentName == ComponentName && Text(w.ComponentDocumentPath) && Text(w.ComponentConfiguration) && w.LocalDocumentPath == w.ComponentDocumentPath && w.LocalConfiguration == w.ComponentConfiguration, "LOCAL_COMPONENT_BINDING_INVALID");
            Check(issues, Text(w.FeaturePersistentReference) && Text(w.ComponentPersistentReference) && Text(w.OwnerFacePersistentReference) && w.OwnerFacePersistentReference == w.RoundTripOwnerFacePersistentReference && w.LocalMappingRoundTripVerified, "PERSISTENT_MAPPING_INCOMPLETE");
            Check(issues, w.MateParameterCoordinateSystem == MateFrame && w.ComponentTransformCoordinateSystem == TransformFrame && Numbers(w.ComponentTransform, 16) && Numbers(w.ComponentTotalTransformWithoutPresentation, 16), "COORDINATE_OR_TRANSFORM_INPUT_INVALID");
            Check(issues, Numbers(w.MateParameters, 8) && Unit(Axis(w.MateParameters ?? new double[0])) && w.MateParameters[6] > 0, "MATE_PARAMETERS_INVALID");
            FaceWitness f = w.Face;
            if (f == null) { issues.Add("FACE_INPUT_MISSING"); return issues; }
            Check(issues, f.CoordinateSystem == LocalFrame && Text(f.LocalPersistentReference) && f.IsCylinder && Numbers(f.CylinderParameters, 7) && Unit(Axis(f.CylinderParameters ?? new double[0])) && f.CylinderParameters[6] > 0 && Finite(f.Area) && f.Area > 0, "LOCAL_CYLINDER_INPUT_INVALID");
            if (f.Loops == null || f.Edges == null || f.Vertices == null) { issues.Add("FINITE_BOUNDARY_INPUT_MISSING"); return issues; }
            Check(issues, f.ReportedLoopCount == 2 && f.Loops.Count == 2 && f.ReportedEdgeCount == 2 && f.Edges.Count == 2, "UNSUPPORTED_TOPOLOGY_REQUIRES_TWO_CIRCULAR_LOOPS");
            if (!Numbers(f.CylinderParameters, 7) || !Unit(Axis(f.CylinderParameters))) return issues;
            Check(issues, f.Edges.All(e => e != null && Text(e.Id)) && f.Edges.Select(e => e == null ? null : e.Id).Distinct(StringComparer.Ordinal).Count() == f.Edges.Count, "EDGE_ID_SET_INVALID");
            Check(issues, f.Vertices.All(v => v != null && Text(v.Id) && Numbers(v.Point, 3)) && f.Vertices.Select(v => v == null ? null : v.Id).Distinct(StringComparer.Ordinal).Count() == f.Vertices.Count, "VERTEX_ID_SET_INVALID");
            var usedEdges = new List<string>(); var usedVertices = new HashSet<string>(StringComparer.Ordinal);
            foreach (LoopWitness l in f.Loops)
            {
                if (l == null || l.CoEdges == null || l.EdgeIds == null || l.VertexIds == null) { issues.Add("LOOP_INPUT_INCOMPLETE"); continue; }
                Check(issues, !l.IsSingular && l.ReportedCoEdgeCount == 1 && l.CoEdges.Count == 1 && l.ReportedEdgeCount == 1 && l.EdgeIds.Length == 1 && l.ReportedVertexCount == l.VertexIds.Length, "UNSUPPORTED_LOOP_PROFILE");
                foreach (string id in l.VertexIds) { Check(issues, Text(id), "NULL_LOOP_VERTEX"); if (id != null) usedVertices.Add(id); }
                if (l.CoEdges.Count != 1 || l.EdgeIds.Length != 1 || l.CoEdges[0] == null) continue;
                CoEdgeWitness c = l.CoEdges[0]; usedEdges.Add(c.EdgeId);
                Check(issues, c.EdgeId == l.EdgeIds[0] && !c.HasIndependentCurve && c.IndependentCurve == null, "UNSUPPORTED_INDEPENDENT_COEDGE_OR_TOPOLOGY");
                Check(issues, Numbers(c.UsedCurveParameters, 8), "COEDGE_TRIM_INPUT_INVALID");
                EdgeWitness e = f.Edges.FirstOrDefault(x => x != null && x.Id == c.EdgeId);
                if (e == null) { issues.Add("COEDGE_EDGE_MISSING"); continue; }
                CurveWitness curve = e.Curve;
                if (curve == null || curve.Kind != "circle" || !Numbers(curve.AnalyticParameters, 7)) { issues.Add("UNSUPPORTED_CURVE_REQUIRES_CIRCLE"); continue; }
                double[] cp = curve.AnalyticParameters; double[] cylinder = f.CylinderParameters;
                Check(issues, e.TrimCurveType == curve.Identity, "EDGE_CURVE_TYPE_MISMATCH");
                Check(issues, Unit(Axis(cp)) && Near(Math.Abs(Dot(Axis(cp), Axis(cylinder))), 1) && cp[6] > 0 && Near(cp[6], cylinder[6]) && OnAxis(Point(cp), Point(cylinder), Axis(cylinder)), "CIRCLE_NOT_CYLINDER_BOUNDARY");
                Check(issues, curve.EndConditionsAvailable && curve.UnderlyingClosed && curve.UnderlyingPeriodic && Finite(curve.UnderlyingStart) && Finite(curve.UnderlyingEnd) && Near(curve.UnderlyingEnd - curve.UnderlyingStart, 2 * Math.PI), "UNDERLYING_CIRCLE_PERIOD_INVALID");
                Check(issues, Finite(e.UMin) && Finite(e.UMax) && Near(e.UMax - e.UMin, 2 * Math.PI) && Finite(e.TrimLength) && Near(e.TrimLength, 2 * Math.PI * cp[6]), "UNSUPPORTED_ARC_OR_TRIM_LENGTH");
                Check(issues, OnCircle(e.TrimStartPoint, cp) && OnCircle(e.TrimEndPoint, cp) && SamePoint(e.TrimStartPoint, e.TrimEndPoint), "FULL_CIRCLE_TRIM_POINTS_INVALID");
                if (Numbers(c.UsedCurveParameters, 8))
                {
                    double[] a = c.UsedCurveParameters.Take(3).ToArray(), b = c.UsedCurveParameters.Skip(3).Take(3).ToArray();
                    Check(issues, SamePoint(a, e.TrimStartPoint) && SamePoint(b, e.TrimEndPoint) && Near(Math.Abs(c.UsedCurveParameters[7] - c.UsedCurveParameters[6]), 2 * Math.PI), "COEDGE_CIRCLE_TRIM_MISMATCH");
                }
                Check(issues, (e.StartVertexId == null && e.EndVertexId == null) || (Text(e.StartVertexId) && e.StartVertexId == e.EndVertexId), "FULL_CIRCLE_VERTEX_TOPOLOGY_INVALID");
                string[] endpointIds = new[] { e.StartVertexId, e.EndVertexId }.Where(Text).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                Check(issues, endpointIds.SequenceEqual(l.VertexIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)), "LOOP_EDGE_VERTEX_SET_MISMATCH");
                foreach (string id in endpointIds)
                {
                    usedVertices.Add(id); VertexWitness v = f.Vertices.FirstOrDefault(x => x != null && x.Id == id);
                    Check(issues, v != null && SamePoint(v.Point, e.TrimStartPoint), "VERTEX_ENDPOINT_MISMATCH");
                }
            }
            Check(issues, usedEdges.Count == 2 && usedEdges.Distinct(StringComparer.Ordinal).Count() == 2 && new HashSet<string>(usedEdges, StringComparer.Ordinal).SetEquals(f.Edges.Where(e => e != null).Select(e => e.Id)), "FACE_LOOP_EDGE_SET_MISMATCH");
            Check(issues, usedVertices.SetEquals(f.Vertices.Where(v => v != null).Select(v => v.Id)), "FACE_VERTEX_SET_MISMATCH");
            if (f.Edges.Count == 2 && f.Edges.All(e => e != null && e.Curve != null && e.Curve.Kind == "circle" && Numbers(e.Curve.AnalyticParameters, 7)))
            {
                double height = Math.Abs(Dot(Sub(Point(f.Edges[1].Curve.AnalyticParameters), Point(f.Edges[0].Curve.AnalyticParameters)), Axis(f.CylinderParameters)));
                Check(issues, Finite(height) && height > Absolute && Near(f.Area, 2 * Math.PI * f.CylinderParameters[6] * height), "FINITE_BAND_HEIGHT_OR_AREA_INCONSISTENT");
            }
            return issues.Distinct(StringComparer.Ordinal).ToList();
        }

        private static void DeepCompare(object a, object b, string path, List<string> reasons)
        {
            if (path == "CaptureStage") return;
            if (a == null || b == null) { if (a != null || b != null) reasons.Add("NULL_DRIFT " + path); return; }
            if (a.GetType() != b.GetType()) { reasons.Add("TYPE_DRIFT " + path); return; }
            if (a is double) { if (!Near((double)a, (double)b)) reasons.Add("NUMERIC_DRIFT " + path); return; }
            if (a is string || a.GetType().IsPrimitive) { if (!a.Equals(b)) reasons.Add("VALUE_DRIFT " + path); return; }
            if (a is IEnumerable)
            {
                object[] x = ((IEnumerable)a).Cast<object>().ToArray(), y = ((IEnumerable)b).Cast<object>().ToArray();
                if (x.Length != y.Length) { reasons.Add("COUNT_DRIFT " + path); return; }
                for (int i = 0; i < x.Length; i++)
                {
                    if ((path == "MateParameters" || path == "Face.CylinderParameters") && i < 3) continue;
                    DeepCompare(x[i], y[i], path + "[" + i + "]", reasons);
                }
                return;
            }
            foreach (PropertyInfo p in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
                DeepCompare(p.GetValue(a, null), p.GetValue(b, null), Text(path) ? path + "." + p.Name : p.Name, reasons);
        }

        public static ComparisonResult Compare(CylinderWitness before, CylinderWitness after)
        {
            var result = new ComparisonResult();
            result.Reasons.AddRange(SupportIssues(before).Select(x => "BEFORE_UNVERIFIED " + x));
            result.Reasons.AddRange(SupportIssues(after).Select(x => "AFTER_UNVERIFIED " + x));
            if (result.Reasons.Count != 0) return result;
            // Only the two documented points-on-axis may vary; all finite boundaries remain compared.
            Check(result.Reasons, OnAxis(Point(after.MateParameters), Point(before.MateParameters), Axis(before.MateParameters)), "MATE_AXIS_LINE_DRIFT");
            Check(result.Reasons, OnAxis(Point(after.Face.CylinderParameters), Point(before.Face.CylinderParameters), Axis(before.Face.CylinderParameters)), "SURFACE_AXIS_LINE_DRIFT");
            DeepCompare(before, after, "", result.Reasons);
            result.Equivalent = result.Reasons.Count == 0; return result;
        }

        public static ComparisonResult CompareFour(IList<CylinderWitness> before, IList<CylinderWitness> after)
        {
            var result = new ComparisonResult();
            bool bound = before != null && after != null && before.Count == 4 && after.Count == 4 &&
                before.All(w => w != null) && after.All(w => w != null) &&
                before.Select(w => w.FeatureName).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(TargetNames.OrderBy(n => n, StringComparer.Ordinal)) &&
                after.Select(w => w.FeatureName).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(TargetNames.OrderBy(n => n, StringComparer.Ordinal));
            Check(result.Reasons, bound, "EXACT_FOUR_TARGET_WITNESSES_REQUIRED");
            if (!bound) return result;
            foreach (string name in TargetNames)
                result.Reasons.AddRange(Compare(before.Single(w => w.FeatureName == name), after.Single(w => w.FeatureName == name)).Reasons.Select(r => name + "/" + r));
            Check(result.Reasons, before.Select(w => w.OwnerDocumentPath).Distinct(StringComparer.Ordinal).Count() == 1 && after.Select(w => w.OwnerDocumentPath).Distinct(StringComparer.Ordinal).Count() == 1, "FOUR_TARGET_OWNER_CONTEXT_MISMATCH");
            result.Equivalent = result.Reasons.Count == 0; return result;
        }
    }
}
