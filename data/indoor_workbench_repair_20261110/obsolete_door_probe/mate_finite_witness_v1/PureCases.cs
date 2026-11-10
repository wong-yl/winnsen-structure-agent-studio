// Synthetic analytic fixtures only. No COM objects or SOLIDWORKS process are created.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Winnsen.MateFiniteWitnessV1;

internal static class PureCases
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16777216 };
    private static readonly List<object> Results = new List<object>();
    private static int failures;
    private static T Copy<T>(T value) { return Json.Deserialize<T>(Json.Serialize(value)); }
    private static double[] Transform() { return new[] { 1d, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 }; }
    private static CylinderWitness Fixture(string feature)
    {
        const string root = @"D:\synthetic-only\标准寄存柜1917×1000×550(总装配).SLDASM";
        const string owner = @"D:\synthetic-only\箱体竖隔板L焊接.SLDASM";
        const string part = @"D:\synthetic-only\箱体竖隔板L.sldprt";
        double y = 1.8214, z = -0.1485, radius = 0.001, start = 0.372, height = 0.001;
        var w = new CylinderWitness
        {
            CaptureStage = "before", CaptureComplete = true, ProfileSupported = true,
            ActiveRootDocumentPath = root, ActiveRootConfiguration = "Default",
            OwnerDocumentPath = owner, OwnerConfiguration = "Default", FeatureName = feature,
            FeatureType = "MateConcentric", FeatureId = 29 + Array.IndexOf(FiniteWitness.TargetNames, feature),
            FeaturePersistentReference = "synthetic-feature-" + feature, MateType = 1, MateAlignment = 1,
            MateFlipped = false, MateCanBeFlipped = false, EntityIndex = 0, EntityCount = 2, EntityReferenceType = 4,
            MateParameterCoordinateSystem = FiniteWitness.MateFrame,
            MateParameters = new[] { 0.4528284271247463, y, z, -1d, 0, 0, radius, 0 },
            OwnerFacePersistentReference = "synthetic-owner-face-" + feature,
            RoundTripOwnerFacePersistentReference = "synthetic-owner-face-" + feature,
            ComponentName = "箱体竖隔板L-1", ComponentDocumentPath = part, ComponentConfiguration = "Default",
            ComponentPersistentReference = "synthetic-component", ComponentTransformCoordinateSystem = FiniteWitness.TransformFrame,
            ComponentTransform = Transform(), ComponentTotalTransformWithoutPresentation = Transform(),
            LocalDocumentPath = part, LocalConfiguration = "Default", LocalMappingRoundTripVerified = true,
            Face = new FaceWitness
            {
                CoordinateSystem = FiniteWitness.LocalFrame, LocalPersistentReference = "synthetic-local-face-" + feature,
                FaceInSurfaceSense = false, SurfaceIdentity = 4002, IsCylinder = true,
                CylinderParameters = new[] { 0.4528284271247463, y, z, -1d, 0, 0, radius },
                Area = 2 * Math.PI * radius * height, ReportedLoopCount = 2, ReportedEdgeCount = 2
            }
        };
        for (int i = 0; i < 2; i++)
        {
            string id = "synthetic-edge-" + i, vertexId = "synthetic-vertex-" + i;
            double x = start + i * height; double[] point = { x, y + radius, z };
            var edge = new EdgeWitness
            {
                Id = id, StartVertexId = vertexId, EndVertexId = vertexId,
                Curve = new CurveWitness { Kind = "circle", Identity = 3002, AnalyticParameters = new[] { x, y, z, -1d, 0, 0, radius }, EndConditionsAvailable = true, UnderlyingStart = 0, UnderlyingEnd = 2 * Math.PI, UnderlyingClosed = true, UnderlyingPeriodic = true },
                TrimStartPoint = (double[])point.Clone(), TrimEndPoint = (double[])point.Clone(), UMin = 0, UMax = 2 * Math.PI,
                CurveAndEdgeSameSense = true, TrimCurveType = 3002, TrimCurveTag = 100 + i, TrimLength = 2 * Math.PI * radius
            };
            w.Face.Edges.Add(edge); w.Face.Vertices.Add(new VertexWitness { Id = vertexId, Point = (double[])point.Clone() });
            w.Face.Loops.Add(new LoopWitness
            {
                IsOuter = i == 0, IsSingular = false, ReportedCoEdgeCount = 1, ReportedEdgeCount = 1, ReportedVertexCount = 1,
                EdgeIds = new[] { id }, VertexIds = new[] { vertexId },
                CoEdges = new List<CoEdgeWitness> { new CoEdgeWitness { EdgeId = id, SameSenseAsEdge = i == 0, UsedCurveParameters = point.Concat(point).Concat(new[] { 0d, 2 * Math.PI }).ToArray(), HasIndependentCurve = false } }
            });
        }
        return w;
    }

    private static void Case(string name, bool expectedEquivalent, CylinderWitness a, CylinderWitness b)
    {
        string aBefore = Json.Serialize(a), bBefore = Json.Serialize(b);
        ComparisonResult result = FiniteWitness.Compare(a, b);
        bool unchanged = aBefore == Json.Serialize(a) && bBefore == Json.Serialize(b);
        bool pass = result.Equivalent == expectedEquivalent && unchanged && (expectedEquivalent || result.Reasons.Count > 0);
        if (!pass) failures++;
        Results.Add(new { name, expectedEquivalent, equivalent = result.Equivalent, inputObjectsUnchanged = unchanged, pass, reasons = result.Reasons });
    }
    private static void Mutated(string name, Action<CylinderWitness> mutate)
    {
        CylinderWitness a = Fixture("同心1"), b = Copy(a); b.CaptureStage = "independent-cold"; mutate(b); Case(name, false, a, b);
    }
    private static void UnsupportedPair(string name, Action<CylinderWitness> mutate)
    {
        CylinderWitness a = Fixture("同心1"); mutate(a); CylinderWitness b = Copy(a); b.CaptureStage = "independent-cold"; Case(name, false, a, b);
    }
    private static void MoveBoundary(CylinderWitness w, int edgeIndex, double dx)
    {
        EdgeWitness e = w.Face.Edges[edgeIndex]; e.Curve.AnalyticParameters[0] += dx;
        e.TrimStartPoint[0] += dx; e.TrimEndPoint[0] += dx;
        w.Face.Vertices.Single(v => v.Id == e.StartVertexId).Point[0] += dx;
        CoEdgeWitness c = w.Face.Loops.Single(l => l.EdgeIds[0] == e.Id).CoEdges[0]; c.UsedCurveParameters[0] += dx; c.UsedCurveParameters[3] += dx;
    }
    private static void BatchCase(string name, bool expectedEquivalent, IList<CylinderWitness> a, IList<CylinderWitness> b)
    {
        string aBefore = Json.Serialize(a), bBefore = Json.Serialize(b); ComparisonResult result = FiniteWitness.CompareFour(a, b);
        bool unchanged = aBefore == Json.Serialize(a) && bBefore == Json.Serialize(b); bool pass = result.Equivalent == expectedEquivalent && unchanged;
        if (!pass) failures++;
        Results.Add(new { name, expectedEquivalent, equivalent = result.Equivalent, inputObjectsUnchanged = unchanged, pass, reasons = result.Reasons });
    }
    public static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathRooted(args[0])) throw new InvalidOperationException("ABSOLUTE_NEW_REPORT_PATH_REQUIRED");
        if (File.Exists(args[0])) throw new InvalidOperationException("REPORT_EXISTS_DO_NOT_OVERWRITE");
        CylinderWitness baseline = Fixture("同心1"), exact = Copy(baseline); exact.CaptureStage = "edit";
        Case("identical_complete_finite_band", true, baseline, exact);
        CylinderWitness shiftedRepresentative = Copy(baseline); shiftedRepresentative.CaptureStage = "independent-cold";
        shiftedRepresentative.MateParameters[0] -= 0.08; shiftedRepresentative.Face.CylinderParameters[0] -= 0.08;
        Case("only_mate_and_surface_axis_representative_points_shift_80mm", true, baseline, shiftedRepresentative);
        CylinderWitness shiftedMateOnly = Copy(baseline); shiftedMateOnly.MateParameters[0] -= 0.08;
        Case("only_mate_axis_representative_point_shifts_80mm", true, baseline, shiftedMateOnly);
        CylinderWitness vertexless = Copy(baseline); vertexless.Face.Vertices.Clear();
        foreach (EdgeWitness e in vertexless.Face.Edges) { e.StartVertexId = null; e.EndVertexId = null; }
        foreach (LoopWitness l in vertexless.Face.Loops) { l.VertexIds = new string[0]; l.ReportedVertexCount = 0; }
        Case("complete_periodic_circles_without_topological_vertices", true, vertexless, Copy(vertexless));
        Mutated("finite_face_translates_axially_80mm_same_radius_area_and_transforms", w => { MoveBoundary(w, 0, 0.08); MoveBoundary(w, 1, 0.08); });
        Mutated("finite_face_truncates_with_consistently_updated_area", w => { MoveBoundary(w, 1, -0.0002); w.Face.Area *= 0.8; });
        Mutated("one_finite_boundary_moves_same_aggregate_area_claim", w => MoveBoundary(w, 1, 0.0002));
        Mutated("component_translates_axially", w => { w.ComponentTransform[9] += 0.08; w.ComponentTotalTransformWithoutPresentation[9] += 0.08; });
        Mutated("mate_axis_point_moves_perpendicular", w => w.MateParameters[1] += 0.001);
        Mutated("surface_axis_point_moves_perpendicular", w => w.Face.CylinderParameters[1] += 0.001);
        Mutated("mate_radius_changes", w => w.MateParameters[6] += 0.0001);
        Mutated("surface_radius_changes", w => w.Face.CylinderParameters[6] += 0.0001);
        Mutated("axis_direction_reverses", w => w.MateParameters[3] = 1);
        Mutated("mate_remaining_parameter_changes", w => w.MateParameters[7] = 0.0001);
        Mutated("persistent_face_identity_changes", w => { w.OwnerFacePersistentReference += "-changed"; w.RoundTripOwnerFacePersistentReference = w.OwnerFacePersistentReference; });
        Mutated("finite_vertex_moves", w => w.Face.Vertices[0].Point[0] += 0.001);
        Mutated("coedge_orientation_changes", w => w.Face.Loops[0].CoEdges[0].SameSenseAsEdge = !w.Face.Loops[0].CoEdges[0].SameSenseAsEdge);
        Mutated("face_orientation_changes", w => w.Face.FaceInSurfaceSense = !w.Face.FaceInSurfaceSense);
        Mutated("active_root_coordinate_context_changes", w => w.ActiveRootDocumentPath += "-changed");
        Mutated("owner_configuration_changes", w => w.OwnerConfiguration = "Other");
        Mutated("edge_curve_trim_tag_changes_conservatively_rejected", w => w.Face.Edges[0].TrimCurveTag++);
        UnsupportedPair("both_snapshots_lack_finite_loops", w => { w.Face.Loops.Clear(); w.Face.ReportedLoopCount = 0; });
        UnsupportedPair("both_snapshots_use_line_boundary_diagnostic_only", w => { w.Face.Edges[0].Curve.Kind = "line-diagnostic-only"; w.Face.Edges[0].Curve.AnalyticParameters = new[] { 0d, 0, 0, 1, 0, 0 }; });
        UnsupportedPair("both_snapshots_use_unknown_spline_boundary", w => { w.Face.Edges[0].Curve.Kind = "unsupported"; w.Face.Edges[0].Curve.Identity = 3005; w.Face.Edges[0].Curve.AnalyticParameters = new double[0]; });
        UnsupportedPair("closed_underlying_curve_but_half_circle_edge_trim", w => { w.Face.Edges[0].UMax = Math.PI; w.Face.Edges[0].TrimLength /= 2; });
        UnsupportedPair("extra_loop_even_if_identical_pair", w => { w.Face.Loops.Add(Copy(w.Face.Loops[0])); w.Face.ReportedLoopCount++; });
        UnsupportedPair("imprecisely_sewn_independent_coedge_curve", w => { w.Face.Loops[0].CoEdges[0].HasIndependentCurve = true; w.Face.Loops[0].CoEdges[0].IndependentCurve = Copy(w.Face.Edges[0].Curve); });
        UnsupportedPair("capture_incomplete_even_if_identical_pair", w => w.CaptureComplete = false);
        UnsupportedPair("missing_local_mapping_even_if_identical_pair", w => w.LocalMappingRoundTripVerified = false);
        UnsupportedPair("missing_coordinate_label_even_if_identical_pair", w => w.Face.CoordinateSystem = null);
        UnsupportedPair("nonfinite_edge_input_even_if_identical_pair", w => w.Face.Edges[0].UMax = double.PositiveInfinity);
        var fourBefore = FiniteWitness.TargetNames.Select(Fixture).ToList(); var fourAfter = Copy(fourBefore);
        foreach (CylinderWitness w in fourAfter) { w.CaptureStage = "independent-cold"; w.MateParameters[0] -= 0.08; w.Face.CylinderParameters[0] -= 0.08; }
        BatchCase("exact_four_targets_with_only_axis_representative_changes", true, fourBefore, fourAfter);
        BatchCase("missing_one_of_four_targets", false, fourBefore.Take(3).ToList(), fourAfter.Take(3).ToList());
        var duplicate = Copy(fourAfter); duplicate[3] = Copy(duplicate[2]); BatchCase("duplicate_target_cannot_replace_fourth", false, fourBefore, duplicate);
        var oneTranslated = Copy(fourAfter); MoveBoundary(oneTranslated[2], 0, 0.08); MoveBoundary(oneTranslated[2], 1, 0.08);
        BatchCase("one_of_four_faces_translates_axially", false, fourBefore, oneTranslated);
        var report = new { schema = "mate-finite-witness-pure-behavior-v1", syntheticFixturesOnly = true, nativeCadExecuted = false, realFourFaceCaptureVerified = false, overallModelAccepted = false, scope = FiniteWitness.Scope, caseCount = Results.Count, passed = Results.Count - failures, failed = failures, cases = Results };
        File.WriteAllText(args[0], Json.Serialize(report));
        Console.WriteLine(Json.Serialize(new { caseCount = Results.Count, passed = Results.Count - failures, failed = failures, nativeCadExecuted = false, report = args[0] }));
        return failures == 0 ? 0 : 1;
    }
}
