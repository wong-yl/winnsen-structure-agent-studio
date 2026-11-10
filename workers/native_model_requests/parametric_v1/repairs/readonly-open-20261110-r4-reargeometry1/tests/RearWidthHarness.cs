using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

namespace SolidWorks.Interop.sldworks
{
    internal class ModelDoc2 { }
    internal sealed class PartDoc : ModelDoc2
    {
        internal Body2[] Bodies;
        internal object GetBodies2(int bodyType, bool visibleOnly) { return Bodies; }
    }
    internal sealed class Body2
    {
        internal double[] Box;
        internal Face2[] Faces;
        internal object GetFaces() { return Faces; }
        internal bool GetExtremePoint(double x, double y, double z, out double px, out double py, out double pz)
        {
            px = Box[x > 0 ? 3 : 0]; py = Box[y > 0 ? 4 : 1]; pz = Box[z > 0 ? 5 : 2]; return true;
        }
    }
    internal sealed class Face2
    {
        internal string Name;
        internal Surface Surface;
        internal double[] Box;
        internal double Area;
        internal Loop2[] Loops;
        internal Edge[] Edges;
        internal object GetSurface() { return Surface; }
        internal object GetBox() { return Box; }
        internal double GetArea() { return Area; }
        internal object GetLoops() { return Loops; }
        internal object GetEdges() { return Edges; }
    }
    internal sealed class Surface
    {
        internal bool Cylinder, Plane;
        internal double[] Parameters;
        internal bool IsCylinder() { return Cylinder; }
        internal bool IsPlane() { return Plane; }
        internal object CylinderParams { get { return Parameters; } }
    }
    internal sealed class Loop2
    {
        internal bool Outer;
        internal Edge[] Edges;
        internal bool IsOuter() { return Outer; }
        internal object GetEdges() { return Edges; }
    }
    internal sealed class Edge
    {
        internal Curve Curve;
        internal Vertex Start, End;
        internal CurveParamData Parameters;
        internal object GetCurve() { return Curve; }
        internal object GetStartVertex() { return Start; }
        internal object GetEndVertex() { return End; }
        internal CurveParamData GetCurveParams3() { return Parameters; }
    }
    internal sealed class Curve
    {
        internal bool Line, Circle;
        internal double[] Parameters;
        internal double LengthScale = 1;
        internal bool IsLine() { return Line; }
        internal bool IsCircle() { return Circle; }
        internal object CircleParams { get { return Parameters; } }
        internal double GetLength3(double start, double end) { return Math.Abs(end - start) * Parameters[6] * LengthScale; }
    }
    internal sealed class CurveParamData
    {
        internal double UMinValue, UMaxValue;
    }
    internal sealed class Vertex
    {
        internal double[] Point;
        internal object GetPoint() { return Point; }
    }
}

namespace Winnsen.StructureAgent.Generation
{
    public sealed class EdgeFixture { public bool line, circle; public double[] start, end; }
    public sealed class LoopFixture { public bool outer; public EdgeFixture[] edges; }
    public sealed class FaceFixture
    {
        public string name;
        public bool cylinder, plane;
        public double[] parameters, box;
        public double area;
        public LoopFixture[] loops;
    }
    public sealed class PairFixture { public double[] parameters, faceBoxMm; }
    public sealed class FixtureData
    {
        public double[][] bodyBoxes, wideRetained, sourceHoleCenters;
        public double[] sourceHoleRadii;
        public FaceFixture[] faces;
        public PairFixture[] widePair;
    }
    internal sealed class GateResult
    {
        internal bool Outside;
        internal int Count;
        internal double[] Removed;
    }
    internal static partial class NativeParametricAssembly
    {
        /*__METHODS__*/

        internal static bool OutsideForTests(ModelDoc2 model) { return FeedbackRearHolesOutsideWidth(model); }
        internal static List<Face2> SelectForTests(ModelDoc2 model) { return FeedbackRearHoles(model); }
        internal static double[] BoxForTests(ModelDoc2 model) { return BodyBox(model); }
        internal static List<double[]> RetainedForTests(ModelDoc2 model) { return FeedbackRetainedRearHoles(model); }
        internal static bool MatchForTests(List<double[]> before, List<double[]> after) { return FeedbackRetainedHolesMatch(before, after); }
        internal static void PackageMarkerForTests(ModelDoc2 rear, bool outsideWidth, bool repairedMarker)
        {
            /*__PACKAGE_MARKER_GATE__*/
        }
        internal static void PackageActionForTests(bool outsideWidth, string repairAction, int removedCount)
        {
            /*__PACKAGE_ACTION_GATE__*/
        }
        internal static Dictionary<string, object> RepairRecordForTests(bool outsideWidth, bool alreadyRepaired, double[] removedHoleX)
        {
            var records = new List<object>();
            /*__REPAIR_RECORD__*/
            string json = new JavaScriptSerializer().Serialize(records.Single());
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }
        internal static GateResult GateForTests(ModelDoc2 rear, bool alreadyRepaired)
        {
            var holes = FeedbackRearHoles(rear);
            bool outsideWidth = FeedbackRearHolesOutsideWidth(rear);
            /*__PAIR_GATE__*/
            return new GateResult { Outside = outsideWidth, Count = holes.Count, Removed = removedHoleX };
        }
        internal static void PostRepairForTests(ModelDoc2 rear, double[] before, List<double[]> retainedBefore)
        {
            /*__POST_REPAIR_GATES__*/
        }
    }
    internal static class ParametricContext
    {
        internal static double WidthMm, HeightMm, DepthMm;
        /*__REQUIRE__*/
    }
    internal sealed class CaseResult
    {
        public string name, status, expectedFailure, observedFailure, error;
        public readonly List<string> checks = new List<string>();
    }
    internal static class RearWidthTests
    {
        private static FixtureData Fixture;
        private static readonly List<CaseResult> Results = new List<CaseResult>();
        private static CaseResult Current;
        private static int Passed, Failed;
        private static readonly string Pattern = "FEEDBACK_NARROW_REAR_RETAINED_PATTERN_DRIFT";
        private static readonly string Boundary = "FEEDBACK_NARROW_REAR_SKIN_BOUNDARY_DRIFT";
        private static double[] Copy(double[] values) { return values == null ? null : (double[])values.Clone(); }
        private static Face2 Face(FaceFixture value)
        {
            return new Face2 {
                Name = value.name, Box = Copy(value.box), Area = value.area,
                Surface = new Surface { Cylinder = value.cylinder, Plane = value.plane, Parameters = Copy(value.parameters) },
                Loops = value.loops == null ? null : value.loops.Select(loop => new Loop2 {
                    Outer = loop.outer, Edges = loop.edges.Select(edge => new Edge {
                        Curve = new Curve { Line = edge.line }, Start = new Vertex { Point = Copy(edge.start) }, End = new Vertex { Point = Copy(edge.end) }
                    }).ToArray()
                }).ToArray()
            };
        }
        private static PartDoc Native820()
        {
            ParametricContext.WidthMm = 820; ParametricContext.HeightMm = 1980; ParametricContext.DepthMm = 490;
            return new PartDoc { Bodies = new[] { new Body2 { Box = Copy(Fixture.bodyBoxes[0]), Faces = Fixture.faces.Select(Face).ToArray() } } };
        }
        private static Face2 Hole(string name, double[] parameters, double[] boxMm, double areaMm2)
        {
            return new Face2 { Name = name, Surface = new Surface { Cylinder = true, Parameters = Copy(parameters) }, Box = boxMm.Select(value => value / 1000).ToArray(), Area = areaMm2 / 1000000 };
        }
        private static PartDoc Native1000()
        {
            ParametricContext.WidthMm = 1000; ParametricContext.HeightMm = 2000; ParametricContext.DepthMm = 500;
            var faces = Fixture.widePair.Select((value, index) => Hole("native-1000-target-" + index, value.parameters, value.faceBoxMm, 2 * Math.PI * 2.75 * .8)).ToList();
            foreach (Face2 face in faces) TargetEdges(face, 1000, 2000, 500);
            faces.AddRange(Fixture.wideRetained.Select((value, index) => Hole("native-1000-retained-" + index, new[] { value[0] / 1000, value[1] / 1000, value[5] / 1000, 0, 0, -1, value[2] / 1000 }, value.Skip(3).Take(6).ToArray(), value[9])));
            return new PartDoc { Bodies = new[] { new Body2 { Box = new[] { -ParametricContext.WidthMm / 2000, (ParametricContext.HeightMm - 77.8) / 1000, -ParametricContext.DepthMm / 1000, ParametricContext.WidthMm / 2000, ParametricContext.HeightMm / 1000, (-ParametricContext.DepthMm + 61.2) / 1000 }, Faces = faces.ToArray() } } };
        }
        private static List<Face2> RetainedFaces(PartDoc part)
        {
            return part.Bodies.SelectMany(body => body.Faces).Where(face => face.Surface.Cylinder && Math.Abs(Math.Abs(face.Surface.Parameters[5]) - 1) < .000001 && Math.Abs(face.Surface.Parameters[6] * 1000 - 2.75) < .001 && face.Box[2] * 1000 > -ParametricContext.DepthMm + 1).ToList();
        }
        private static List<Face2> Skins(PartDoc part) { return part.Bodies.SelectMany(body => body.Faces).Where(face => face.Loops != null).ToList(); }
        private static void RemoveFace(PartDoc part, Face2 face) { part.Bodies[0].Faces = part.Bodies[0].Faces.Where(value => !object.ReferenceEquals(value, face)).ToArray(); }
        private static void AddFace(PartDoc part, Face2 face) { part.Bodies[0].Faces = part.Bodies[0].Faces.Concat(new[] { face }).ToArray(); }
        private static PartDoc Resized820(double width) { return Resized820(width, 1980, 490); }
        private static PartDoc Resized820(double width, double height, double depth)
        {
            PartDoc part = Native820(); ParametricContext.WidthMm = width; ParametricContext.HeightMm = height; ParametricContext.DepthMm = depth;
            double dy = (height - 1980) / 1000, dz = (490 - depth) / 1000;
            foreach (Face2 face in part.Bodies[0].Faces) {
                face.Box[1] += dy; face.Box[4] += dy; face.Box[2] += dz; face.Box[5] += dz;
                if (face.Surface.Parameters != null) { face.Surface.Parameters[1] += dy; face.Surface.Parameters[2] += dz; }
                if (face.Loops != null) foreach (Loop2 loop in face.Loops) foreach (Edge edge in loop.Edges) foreach (Vertex vertex in new[] { edge.Start, edge.End }) { vertex.Point[1] += dy; vertex.Point[2] += dz; }
            }
            part.Bodies[0].Box[1] += dy; part.Bodies[0].Box[4] += dy; part.Bodies[0].Box[2] += dz; part.Bodies[0].Box[5] += dz;
            part.Bodies[0].Box[0] = -width / 2000; part.Bodies[0].Box[3] = width / 2000;
            foreach (Face2 skin in Skins(part)) {
                skin.Box[0] = -width / 2000; skin.Box[3] = width / 2000;
                skin.Area += (width - 820) * 75.2 / 1000000;
                foreach (Loop2 loop in skin.Loops.Where(loop => loop.Outer)) foreach (Edge edge in loop.Edges) foreach (Vertex vertex in new[] { edge.Start, edge.End }) vertex.Point[0] += Math.Sign(vertex.Point[0]) * (width - 820) / 2000;
            }
            return part;
        }
        private static double ArcHalfAngle(double width)
        {
            // Synthetic fixture construction from the circle's inward-facing angular interval.
            return Math.Acos(Math.Max(-1, Math.Min(1, (450 - width / 2) / 2.75)));
        }
        private static double[] CirclePoint(double x, double y, double z, double angle)
        {
            return new[] { (x + 2.75 * Math.Cos(angle)) / 1000, (y + 2.75 * Math.Sin(angle)) / 1000, z / 1000 };
        }
        private static void TargetEdges(Face2 face, double width, double height, double depth)
        {
            double x = face.Surface.Parameters[0] * 1000, y = height - 70, half = ArcHalfAngle(width);
            double middle = x < 0 ? 0 : Math.PI, start = middle - half, end = middle + half;
            var edges = new List<Edge>();
            foreach (double z in new[] { -depth, -depth + .8 }) edges.Add(new Edge {
                Curve = new Curve { Circle = true, Parameters = new[] { x / 1000, y / 1000, z / 1000, 0, 0, 1, .00275 } },
                Parameters = new CurveParamData { UMinValue = start, UMaxValue = end },
                Start = new Vertex { Point = CirclePoint(x, y, z, start) }, End = new Vertex { Point = CirclePoint(x, y, z, end) }
            });
            if (half < Math.PI) foreach (double angle in new[] { start, end }) edges.Add(new Edge {
                Curve = new Curve { Line = true }, Parameters = new CurveParamData { UMinValue = 0, UMaxValue = .0008 },
                Start = new Vertex { Point = CirclePoint(x, y, -depth, angle) }, End = new Vertex { Point = CirclePoint(x, y, -depth + .8, angle) }
            });
            face.Edges = edges.ToArray();
        }
        private static void SplitTargetArcs(Face2 face)
        {
            var edges = new List<Edge>();
            foreach (Edge edge in face.Edges) {
                if (!edge.Curve.Circle) { edges.Add(edge); continue; }
                double start = edge.Parameters.UMinValue, end = edge.Parameters.UMaxValue, middle = (start + end) / 2;
                foreach (double[] interval in new[] { new[] { start, middle }, new[] { middle, end } }) edges.Add(new Edge {
                    Curve = new Curve { Circle = true, Parameters = Copy(edge.Curve.Parameters) },
                    Parameters = new CurveParamData { UMinValue = interval[0], UMaxValue = interval[1] },
                    Start = new Vertex { Point = CirclePoint(edge.Curve.Parameters[0] * 1000, edge.Curve.Parameters[1] * 1000, edge.Curve.Parameters[2] * 1000, interval[0]) },
                    End = new Vertex { Point = CirclePoint(edge.Curve.Parameters[0] * 1000, edge.Curve.Parameters[1] * 1000, edge.Curve.Parameters[2] * 1000, interval[1]) }
                });
            }
            face.Edges = edges.ToArray();
        }
        private static Face2 SyntheticTarget(double width, double height, double depth, int side)
        {
            double x = side * 450, y = height - 70, half = ArcHalfAngle(width), middle = side < 0 ? 0 : Math.PI;
            var angles = new List<double> { middle - half, middle + half };
            for (int i = -4; i <= 8; i++) { double angle = i * Math.PI / 2; if (angle >= middle - half && angle <= middle + half) angles.Add(angle); }
            var points = angles.Select(angle => CirclePoint(x, y, -depth, angle)).ToList();
            Face2 face = Hole("synthetic-source-target-" + side, new[] { x / 1000, y / 1000, -depth / 1000, 0, 0, -1, .00275 },
                new[] { points.Min(point => point[0]) * 1000, points.Min(point => point[1]) * 1000, -depth, points.Max(point => point[0]) * 1000, points.Max(point => point[1]) * 1000, -depth + .8 }, 2 * half * 2.75 * .8);
            TargetEdges(face, width, height, depth); return face;
        }
        private static PartDoc SyntheticRear(double width, double height, double depth)
        {
            PartDoc part = Resized820(width, height, depth);
            if (width > 894.5) foreach (int side in new[] { -1, 1 }) AddFace(part, SyntheticTarget(width, height, depth, side));
            return part;
        }
        private static void RemoveTargets(PartDoc part)
        {
            foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) RemoveFace(part, face);
        }
        private static void VerifySyntheticRepair(PartDoc part)
        {
            double[] before = NativeParametricAssembly.BoxForTests(part); List<double[]> retainedBefore = NativeParametricAssembly.RetainedForTests(part);
            GateResult result = NativeParametricAssembly.GateForTests(part, false);
            Check(result.Outside == (ParametricContext.WidthMm <= 894.5) && result.Count == (result.Outside ? 0 : 2), "synthetic source intersection enters the expected no-hole or two-target branch");
            if (!result.Outside) Check(result.Removed.SequenceEqual(new[] { -450.0, 450.0 }), "both fixed source centres are exactly -450 and +450 mm");
            RemoveTargets(part); NativeParametricAssembly.PostRepairForTests(part, before, retainedBefore);
            Check(true, "extracted production postconditions accept synthetic deletion with unchanged body, skin boundary and retained holes");
        }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("TEST_ASSERTION_FAILED " + description);
            Current.checks.Add(description);
        }
        private static void Reject(Action action, string expected)
        {
            Current.expectedFailure = expected;
            try { action(); }
            catch (InvalidOperationException error) {
                Current.observedFailure = error.Message;
                Check(error.Message == expected, "production threw " + expected); return;
            }
            throw new InvalidOperationException("TEST_EXPECTED_GATE_NOT_RAISED " + expected);
        }
        private static void Run(string name, Action action)
        {
            Current = new CaseResult { name = name }; Results.Add(Current);
            try { action(); Current.status = "PASS"; Passed++; }
            catch (Exception error) { Current.status = "FAIL"; Current.error = error.ToString(); Failed++; }
        }
        private static void RejectNarrow(string name, string expected, Action<PartDoc> mutation)
        {
            Run(name, delegate { PartDoc part = Native820(); mutation(part); Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, expected); });
        }
        private static void RejectPair(string name, string expected, Action<PartDoc, List<Face2>> mutation)
        {
            Run(name, delegate { PartDoc part = Native1000(); mutation(part, NativeParametricAssembly.SelectForTests(part)); Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, expected); });
        }
        private static void RejectClip(string name, string expected, Action<PartDoc, List<Face2>> mutation)
        {
            Run(name, delegate { PartDoc part = SyntheticRear(900, 2000, 500); mutation(part, NativeParametricAssembly.SelectForTests(part)); Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, expected); });
        }
        private static void RejectAfterRepair(string name, string expected, double width, Action<PartDoc> mutation)
        {
            Run(name, delegate {
                PartDoc part = SyntheticRear(width, 2000, 500); double[] before = NativeParametricAssembly.BoxForTests(part); var retainedBefore = NativeParametricAssembly.RetainedForTests(part);
                NativeParametricAssembly.GateForTests(part, false); RemoveTargets(part); mutation(part);
                Reject(delegate { NativeParametricAssembly.PostRepairForTests(part, before, retainedBefore); }, expected);
            });
        }
        private static void MoveX(Face2 face, double mm)
        {
            face.Surface.Parameters[0] += mm / 1000; face.Box[0] += mm / 1000; face.Box[3] += mm / 1000;
        }
        private static int Main(string[] args)
        {
            Fixture = new JavaScriptSerializer { MaxJsonLength = 4000000 }.Deserialize<FixtureData>(File.ReadAllText(args[0]));
            Run("native_820_all_137_faces_accepted_without_deletion", delegate {
                PartDoc part = Native820(); GateResult result = NativeParametricAssembly.GateForTests(part, false);
                Check(part.Bodies[0].Faces.Length == 137, "complete native 820 face fixture is present");
                Check(result.Outside && result.Count == 0 && result.Removed.Length == 0, "native 820 is proven outside and requires zero target holes");
                Check(Skins(part).Count == 2 && Skins(part).All(face => face.Loops.Length == 5), "both original rear skins use the native five-loop evidence");
            });
            Run("native_820_retained_six_holes_match_complete_coordinates", delegate {
                PartDoc part = Native820(); List<double[]> holes = NativeParametricAssembly.RetainedForTests(part);
                Check(holes.Count == 6 && holes.Select(hole => Math.Round(hole[0], 6)).SequenceEqual(new[] { -305.0, -160, -15, 15, 160, 305 }), "native retained X coordinates are exactly the six expected centres");
                Check(holes.All(hole => Math.Abs(hole[1] - 1910) < .001 && Math.Abs(hole[5] + 475.8) < .001 && Math.Abs(hole[8] + 475) < .001 && Math.Abs(hole[9] - 2 * Math.PI * 2.75 * .8) < .001), "native retained Y/Z/area evidence is preserved");
            });
            Run("native_master_rear_sketch_retains_fixed_X450_centres", delegate {
                Native820(); var x = Fixture.sourceHoleCenters.Select(value => value[0] * 1000).OrderBy(value => value).ToArray();
                Check(x.Length == 8 && Math.Abs(x[0] + 450) < .001 && Math.Abs(x[7] - 450) < .001, "source sketch contains eight circles including fixed -450 and +450 centres");
                Check(Fixture.sourceHoleRadii.All(radius => Math.Abs(radius * 1000 - 2.75) < .001), "native source circles have radius 2.75 mm");
                Check(450 - 2.75 > ParametricContext.WidthMm / 2, "the complete source rear pair lies outside the native 820 body");
            });
            Run("native_1000_pair_with_synthetic_area_and_edges_selection_remains_two", delegate {
                PartDoc part = Native1000(); List<Face2> selected = NativeParametricAssembly.SelectForTests(part); GateResult result = NativeParametricAssembly.GateForTests(part, false);
                Check(selected.Count == 2 && selected.All(face => face.Name.StartsWith("native-1000-target-")), "only the native 1000 rear pair is selected");
                Check(!result.Outside && result.Count == 2 && Math.Abs(result.Removed[0] + 450) < .001 && Math.Abs(result.Removed[1] - 450) < .001, "the extracted production count, source-circle and pair gates accept native centres/boxes with synthetic areas and edges");
            });
            Run("native_1000_eight_retained_holes_excluded_from_target_pair", delegate {
                PartDoc part = Native1000(); List<double[]> holes = NativeParametricAssembly.RetainedForTests(part);
                Check(holes.Count == 8 && NativeParametricAssembly.MatchForTests(Fixture.wideRetained.ToList(), holes), "all eight native retained geometry rows survive independently of the two targets");
            });
            RejectPair("wide_zero_targets_cannot_use_outside_path", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => { foreach (Face2 face in selected) RemoveFace(part, face); Check(!NativeParametricAssembly.OutsideForTests(part), "wide model cannot claim outside-width exemption"); });
            Run("wide_existing_repair_marker_with_zero_targets_accepted", delegate {
                PartDoc part = Native1000(); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) RemoveFace(part, face);
                GateResult result = NativeParametricAssembly.GateForTests(part, true); Check(!result.Outside && result.Count == 0, "wide pre-existing repair still requires zero target holes");
            });
            Run("wide_existing_repair_marker_with_targets_rejected", delegate { PartDoc part = Native1000(); Reject(delegate { NativeParametricAssembly.GateForTests(part, true); }, "FEEDBACK_REAR_HOLE_COUNT_DRIFT"); });
            Run("narrow_existing_repair_marker_rejected", delegate { PartDoc part = Native820(); Reject(delegate { NativeParametricAssembly.GateForTests(part, true); }, "FEEDBACK_NARROW_REAR_UNEXPECTED_REPAIR_MARKER"); });
            Run("production_three_action_records_roundtrip_verification", delegate {
                foreach (bool outside in new[] { true, false }) foreach (bool repaired in new[] { false, true }) {
                    if (outside && repaired) continue;
                    Native820(); double[] removed = outside || repaired ? new double[0] : new[] { -450.0, 450.0 };
                    Dictionary<string, object> record = NativeParametricAssembly.RepairRecordForTests(outside, repaired, removed);
                    string action = Convert.ToString(record["repairAction"]); int count = Convert.ToInt32(record["removedHoleCount"]);
                    string expected = outside ? "absent_outside_source_envelope" : repaired ? "already_repaired_rear_pair" : "deleted_rear_pair";
                    Check(action == expected && count == removed.Length, "production record encodes " + expected + " with count " + count);
                    NativeParametricAssembly.PackageActionForTests(outside, action, count);
                }
            });
            foreach (string action in new[] { "absent_outside_source_envelope", "deleted_rear_pair", "already_repaired_rear_pair", "unexpected" }) foreach (int count in new[] { 0, 1, 2 }) foreach (bool outside in new[] { true, false }) {
                string actionValue = action; int countValue = count; bool outsideValue = outside;
                bool accepted = outside ? action == "absent_outside_source_envelope" && count == 0 : (action == "deleted_rear_pair" && count == 2) || (action == "already_repaired_rear_pair" && count == 0);
                Run("package_tuple_" + (outside ? "narrow_" : "wide_") + action + "_" + count, delegate {
                    if (accepted) { NativeParametricAssembly.PackageActionForTests(outsideValue, actionValue, countValue); Check(true, "valid production action/count tuple accepted"); }
                    else Reject(delegate { NativeParametricAssembly.PackageActionForTests(outsideValue, actionValue, countValue); }, "FEEDBACK_PACKAGE_REAR_REPAIR_EVIDENCE_DRIFT");
                });
            }
            Run("package_narrow_zero_targets_without_marker_accepted", delegate { PartDoc part = Native820(); NativeParametricAssembly.PackageMarkerForTests(part, true, false); Check(true, "outside-width package requires no repair marker and zero targets"); });
            Run("package_narrow_unexpected_marker_rejected", delegate { PartDoc part = Native820(); Reject(delegate { NativeParametricAssembly.PackageMarkerForTests(part, true, true); }, "FEEDBACK_PACKAGE_REAR_HOLES_REMAIN"); });
            Run("package_wide_missing_marker_rejected", delegate { PartDoc part = Native1000(); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) RemoveFace(part, face); Reject(delegate { NativeParametricAssembly.PackageMarkerForTests(part, false, false); }, "FEEDBACK_PACKAGE_REAR_HOLES_REMAIN"); });
            Run("package_wide_marker_with_zero_targets_accepted", delegate { PartDoc part = Native1000(); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) RemoveFace(part, face); NativeParametricAssembly.PackageMarkerForTests(part, false, true); Check(true, "wide package requires marker and zero targets"); });
            Run("package_wide_marker_with_remaining_targets_rejected", delegate { PartDoc part = Native1000(); Reject(delegate { NativeParametricAssembly.PackageMarkerForTests(part, false, true); }, "FEEDBACK_PACKAGE_REAR_HOLES_REMAIN"); });
            RejectNarrow("820_missing_retained_hole", Pattern, part => RemoveFace(part, RetainedFaces(part)[0]));
            RejectNarrow("820_duplicate_retained_hole", Pattern, part => AddFace(part, RetainedFaces(part)[0]));
            RejectNarrow("820_retained_Y_changed", Pattern, part => RetainedFaces(part)[0].Surface.Parameters[1] += .00002);
            RejectNarrow("820_retained_Z_changed", Pattern, part => { Face2 face = RetainedFaces(part)[0]; face.Box[2] += .00002; face.Box[5] += .00002; });
            RejectNarrow("820_retained_radius_changed", Pattern, part => RetainedFaces(part)[0].Surface.Parameters[6] += .00002);
            RejectNarrow("820_retained_X_changed", Pattern, part => MoveX(RetainedFaces(part)[0], .02));
            RejectNarrow("820_retained_area_changed", Pattern, part => RetainedFaces(part)[0].Area -= .00000001);
            RejectNarrow("820_retained_bbox_changed", Pattern, part => RetainedFaces(part)[0].Box[0] += .00002);
            RejectNarrow("820_retained_axis_changed", Pattern, part => { Face2 face = RetainedFaces(part)[0]; face.Surface.Parameters[3] = .1; face.Surface.Parameters[5] = Math.Sqrt(.99); });
            RejectNarrow("820_extra_rear_skin_different_radius_hole", "FEEDBACK_NARROW_REAR_UNEXPECTED_SKIN_HOLE", part => AddFace(part, Hole("unexpected-R4", new[] { 0.0, 1.91, -.49, 0, 0, 1, .004 }, new[] { -4.0, 1906, -490, 4, 1914, -489.2 }, 2 * Math.PI * 4 * .8)));
            RejectNarrow("820_extra_rear_skin_oblique_cylinder", "FEEDBACK_NARROW_REAR_UNEXPECTED_SKIN_HOLE", part => AddFace(part, Hole("unexpected-oblique", new[] { 0.0, 1.91, -.49, Math.Sqrt(.99), 0, .1, .004 }, new[] { -4.0, 1906, -490, 4, 1914, -489.2 }, 10)));
            for (int axis = 0; axis < 6; axis++) {
                int coordinate = axis; RejectNarrow("820_body_bbox_coordinate_" + coordinate + "_changed", "FEEDBACK_NARROW_REAR_ENVELOPE_DRIFT", part => part.Bodies[0].Box[coordinate] += .00002);
            }
            RejectNarrow("820_multiple_bodies_same_outer_envelope", "FEEDBACK_NARROW_REAR_BODY_COUNT_DRIFT", part => part.Bodies = new[] { part.Bodies[0], new Body2 { Box = Copy(part.Bodies[0].Box), Faces = new Face2[0] } });
            Run("width_below_700_rejected", delegate { PartDoc part = Resized820(699.999); Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, "FEEDBACK_NARROW_REAR_WIDTH_UNVERIFIED"); });
            Run("width_700_complete_narrow_fixture_accepted", delegate { Check(NativeParametricAssembly.GateForTests(Resized820(700), false).Outside, "700 mm lower supported width uses the complete narrow guards"); });
            foreach (double width in new[] { 700.0, 820, 894.48, 894.5, 894.52, 899.98, 900, 900.02, 905.48, 905.5, 905.52, 1000, 1200 }) foreach (double[] dimensions in new[] { new[] { 1980.0, 490 }, new[] { 2000.0, 500 }, new[] { 1850.0, 450 }, new[] { 2200.0, 600 } }) {
                double boundaryWidth = width, height = dimensions[0], depth = dimensions[1];
                Run("synthetic_matrix_W" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_H" + height + "_D" + depth, delegate { VerifySyntheticRepair(SyntheticRear(boundaryWidth, height, depth)); });
            }
            foreach (double width in new[] { 894.52, 899.98, 900, 900.02, 905.48, 905.5, 905.52, 1000.0, 1200 }) {
                double boundaryWidth = width;
                Run("synthetic_width_" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_zero_targets_rejected", delegate {
                    PartDoc part = SyntheticRear(boundaryWidth, 2000, 500); RemoveTargets(part);
                    Check(!NativeParametricAssembly.OutsideForTests(part), "intersecting or complete source circles cannot use the outside path");
                    Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, "FEEDBACK_REAR_HOLE_COUNT_DRIFT");
                });
            }
            Run("width_above_1200_rejected", delegate { PartDoc part = SyntheticRear(1200.001, 2000, 500); Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, "FEEDBACK_NARROW_REAR_WIDTH_UNVERIFIED"); });
            Run("synthetic_exact_tangent_has_zero_cylindrical_faces", delegate { PartDoc part = SyntheticRear(894.5, 2000, 500); Check(NativeParametricAssembly.SelectForTests(part).Count == 0 && NativeParametricAssembly.GateForTests(part, false).Outside, "exact 894.5 mm tangency retains no finite-area cylinder"); });
            Run("synthetic_900_is_exact_half_circle_per_side", delegate { PartDoc part = SyntheticRear(900, 2000, 500); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) Check(Math.Abs(face.Area * 1000000 - Math.PI * 2.75 * .8) < .00000001 && Math.Abs(face.Box[3] - face.Box[0] - .00275) < .00000001, "900 mm generates exactly a half circumference and half circle X extent"); NativeParametricAssembly.GateForTests(part, false); });
            Run("synthetic_905p5_is_exact_full_circle_per_side", delegate { PartDoc part = SyntheticRear(905.5, 2000, 500); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) Check(Math.Abs(face.Area * 1000000 - 2 * Math.PI * 2.75 * .8) < .00000001 && Math.Abs(face.Box[3] - face.Box[0] - .0055) < .00000001, "905.5 mm generates complete circumference and diameter"); NativeParametricAssembly.GateForTests(part, false); });
            RejectPair("wide_partial_circle_area_only", "FEEDBACK_REAR_SOURCE_CLIP_GEOMETRY_DRIFT", (part, selected) => selected[0].Area /= 2);
            RejectPair("wide_partial_circle_Y_bbox_only", "FEEDBACK_REAR_SOURCE_CLIP_GEOMETRY_DRIFT", (part, selected) => selected[0].Box[1] += .00002);
            RejectPair("wide_two_left_holes", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", (part, selected) => { Face2 replacement = SyntheticTarget(1000, 2000, 500, -1); RemoveFace(part, selected[1]); AddFace(part, replacement); });
            RejectPair("wide_asymmetric_pair", "FEEDBACK_REAR_SOURCE_CIRCLE_DRIFT", (part, selected) => MoveX(selected[1], -1));
            RejectPair("wide_one_target_missing", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => RemoveFace(part, selected[0]));
            RejectClip("900_left_source_center_minus_450p02_rejected", "FEEDBACK_REAR_SOURCE_CIRCLE_DRIFT", (part, selected) => MoveX(selected[0], -.02));
            RejectClip("900_right_source_center_449p98_rejected", "FEEDBACK_REAR_SOURCE_CIRCLE_DRIFT", (part, selected) => MoveX(selected[1], -.02));
            RejectClip("900_symmetric_wrong_centers_rejected", "FEEDBACK_REAR_SOURCE_CIRCLE_DRIFT", (part, selected) => { MoveX(selected[0], .02); MoveX(selected[1], -.02); });
            RejectClip("900_two_right_holes_rejected", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", (part, selected) => { RemoveFace(part, selected[0]); AddFace(part, SyntheticTarget(900, 2000, 500, 1)); });
            RejectClip("900_left_target_missing", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => RemoveFace(part, selected[0]));
            RejectClip("900_right_target_missing", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => RemoveFace(part, selected[1]));
            RejectClip("900_duplicate_third_target", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => AddFace(part, SyntheticTarget(900, 2000, 500, 1)));
            RejectClip("900_full_area_on_half_geometry_rejected", "FEEDBACK_REAR_SOURCE_CLIP_GEOMETRY_DRIFT", (part, selected) => selected[0].Area *= 2);
            RejectClip("900_half_area_drift_rejected", "FEEDBACK_REAR_SOURCE_CLIP_GEOMETRY_DRIFT", (part, selected) => selected[0].Area += .00000001);
            RejectClip("900_left_right_unequal_area_rejected", "FEEDBACK_REAR_SOURCE_CLIP_GEOMETRY_DRIFT", (part, selected) => selected[1].Area *= .9);
            RejectClip("900_oblique_axis_X_rejected", "FEEDBACK_REAR_SOURCE_CIRCLE_DRIFT", (part, selected) => selected[0].Surface.Parameters[3] = .001);
            RejectClip("900_oblique_axis_Y_rejected", "FEEDBACK_REAR_SOURCE_CIRCLE_DRIFT", (part, selected) => selected[0].Surface.Parameters[4] = .001);
            RejectClip("900_axis_Z_changed_rejected", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => selected[0].Surface.Parameters[5] = -.99);
            RejectClip("900_radius_changed_rejected", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => selected[0].Surface.Parameters[6] += .00002);
            RejectClip("900_hole_Y_changed_rejected", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => selected[0].Surface.Parameters[1] += .00002);
            for (int axis = 0; axis < 6; axis++) {
                int coordinate = axis;
                RejectClip("900_target_bbox_coordinate_" + coordinate + "_changed", axis == 2 || axis == 5 ? "FEEDBACK_REAR_HOLE_COUNT_DRIFT" : "FEEDBACK_REAR_SOURCE_CLIP_GEOMETRY_DRIFT", (part, selected) => selected[0].Box[coordinate] += .00002);
                RejectClip("900_body_bbox_coordinate_" + coordinate + "_changed", "FEEDBACK_NARROW_REAR_ENVELOPE_DRIFT", (part, selected) => part.Bodies[0].Box[coordinate] += .00002);
            }
            RejectClip("900_multiple_bodies_same_outer_envelope", "FEEDBACK_NARROW_REAR_BODY_COUNT_DRIFT", (part, selected) => part.Bodies = new[] { part.Bodies[0], new Body2 { Box = Copy(part.Bodies[0].Box), Faces = new Face2[0] } });
            RejectPair("1000_multiple_bodies_same_outer_envelope", "FEEDBACK_NARROW_REAR_BODY_COUNT_DRIFT", (part, selected) => part.Bodies = new[] { part.Bodies[0], new Body2 { Box = Copy(part.Bodies[0].Box), Faces = new Face2[0] } });
            Run("900_existing_repair_marker_zero_targets_accepted", delegate { PartDoc part = SyntheticRear(900, 2000, 500); RemoveTargets(part); Check(NativeParametricAssembly.GateForTests(part, true).Count == 0, "clipped-width existing repair marker requires zero remaining targets"); });
            Run("900_existing_repair_marker_with_targets_rejected", delegate { PartDoc part = SyntheticRear(900, 2000, 500); Reject(delegate { NativeParametricAssembly.GateForTests(part, true); }, "FEEDBACK_REAR_HOLE_COUNT_DRIFT"); });
            RejectClip("900_missing_edge_curve_rejected", "FEEDBACK_REAR_SOURCE_EDGE_MISSING", (part, selected) => selected[0].Edges[0].Curve = null);
            RejectClip("900_missing_edge_parameter_range_rejected", "FEEDBACK_REAR_SOURCE_EDGE_MISSING", (part, selected) => selected[0].Edges[0].Parameters = null);
            RejectClip("900_unknown_edge_curve_rejected", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => selected[0].Edges[0].Curve.Circle = false);
            foreach (int axis in new[] { 0, 1, 2, 3, 4, 5, 6 }) {
                int coordinate = axis;
                RejectClip("900_arc_parameter_" + axis + "_changed", "FEEDBACK_REAR_SOURCE_ARC_DRIFT", (part, selected) => selected[0].Edges[0].Curve.Parameters[coordinate] += coordinate < 3 || coordinate == 6 ? .00002 : .01);
            }
            RejectClip("900_arc_lower_length_changed", "FEEDBACK_REAR_SOURCE_ARC_LENGTH_DRIFT", (part, selected) => selected[0].Edges[0].Curve.LengthScale = .9);
            RejectClip("900_arc_upper_length_changed", "FEEDBACK_REAR_SOURCE_ARC_LENGTH_DRIFT", (part, selected) => selected[0].Edges[1].Curve.LengthScale = .9);
            RejectClip("900_arc_parameter_span_changed", "FEEDBACK_REAR_SOURCE_ARC_LENGTH_DRIFT", (part, selected) => selected[0].Edges[0].Parameters.UMaxValue -= .01);
            RejectClip("900_arc_missing_lower_layer", "FEEDBACK_REAR_SOURCE_ARC_LENGTH_DRIFT", (part, selected) => selected[0].Edges = selected[0].Edges.Skip(1).ToArray());
            RejectClip("900_arc_missing_upper_layer", "FEEDBACK_REAR_SOURCE_ARC_LENGTH_DRIFT", (part, selected) => selected[0].Edges = selected[0].Edges.Where((edge, index) => index != 1).ToArray());
            RejectClip("900_arc_duplicate_edge", "FEEDBACK_REAR_SOURCE_ARC_LENGTH_DRIFT", (part, selected) => selected[0].Edges = selected[0].Edges.Concat(new[] { selected[0].Edges[0] }).ToArray());
            RejectClip("900_arc_endpoint_wrong_radius", "FEEDBACK_REAR_SOURCE_ARC_ENDPOINT_DRIFT", (part, selected) => selected[0].Edges[0].Start.Point[1] += .00002);
            RejectClip("900_arc_endpoint_outside_plate", "FEEDBACK_REAR_SOURCE_ARC_ENDPOINT_DRIFT", (part, selected) => selected[0].Edges[0].Start.Point = CirclePoint(-450, 1930, -500, Math.PI));
            RejectClip("900_arc_endpoint_wrong_Z", "FEEDBACK_REAR_SOURCE_ARC_ENDPOINT_DRIFT", (part, selected) => selected[0].Edges[0].Start.Point[2] += .00002);
            RejectClip("900_side_edge_wrong_curve", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => selected[0].Edges[2].Curve.Line = false);
            RejectClip("900_side_edge_missing_start_vertex", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => selected[0].Edges[2].Start = null);
            RejectClip("900_side_edge_missing_end_vertex", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => selected[0].Edges[2].End = null);
            RejectClip("900_side_edge_not_axial", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => selected[0].Edges[2].End.Point[0] += .00002);
            RejectClip("900_side_edge_wrong_thickness", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => selected[0].Edges[2].End.Point[2] += .00002);
            RejectClip("900_side_edge_wrong_radius", "FEEDBACK_REAR_SOURCE_SIDE_EDGE_DRIFT", (part, selected) => { selected[0].Edges[2].Start.Point[1] += .00002; selected[0].Edges[2].End.Point[1] += .00002; });
            foreach (double width in new[] { 894.52, 900, 905.48, 905.5, 1200 }) {
                double boundaryWidth = width;
                Run("synthetic_W" + width + "_split_arc_edges_accepted", delegate { PartDoc part = SyntheticRear(boundaryWidth, 2000, 500); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) SplitTargetArcs(face); NativeParametricAssembly.GateForTests(part, false); Check(true, "split circular edges retain total arc length on each thickness layer"); });
            }
            Run("synthetic_full_circles_without_seam_vertices_accepted", delegate { PartDoc part = SyntheticRear(1000, 2000, 500); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) foreach (Edge edge in face.Edges) { edge.Start = null; edge.End = null; } NativeParametricAssembly.GateForTests(part, false); Check(true, "complete circle edges may have no topological end vertices"); });
            Run("synthetic_900_reversed_axial_side_edges_accepted", delegate { PartDoc part = SyntheticRear(900, 2000, 500); foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) foreach (Edge edge in face.Edges.Where(edge => edge.Curve.Line)) { Vertex prior = edge.Start; edge.Start = edge.End; edge.End = prior; } NativeParametricAssembly.GateForTests(part, false); Check(true, "axial side-edge direction does not change geometric acceptance"); });
            foreach (double width in new[] { 700.0, 894.5, 900, 905.5, 1200 }) {
                double boundaryWidth = width;
                RejectAfterRepair("postrepair_W" + width + "_changed_skin_vertex", Boundary, boundaryWidth, part => Skins(part)[0].Loops[0].Edges[0].Start.Point[0] += .00002);
                RejectAfterRepair("postrepair_W" + width + "_changed_skin_area", "FEEDBACK_NARROW_REAR_SKIN_AREA_DRIFT", boundaryWidth, part => Skins(part)[0].Area += .00000001);
                RejectAfterRepair("postrepair_W" + width + "_missing_skin", "FEEDBACK_NARROW_REAR_SKIN_COUNT_DRIFT", boundaryWidth, part => RemoveFace(part, Skins(part)[0]));
                RejectAfterRepair("postrepair_W" + width + "_changed_retained_area", "FEEDBACK_NONTARGET_REAR_HOLES_CHANGED", boundaryWidth, part => RetainedFaces(part)[0].Area += .00000001);
                RejectAfterRepair("postrepair_W" + width + "_missing_retained_hole", "FEEDBACK_NONTARGET_REAR_HOLES_CHANGED", boundaryWidth, part => RemoveFace(part, RetainedFaces(part)[0]));
                RejectAfterRepair("postrepair_W" + width + "_changed_body_envelope", "FEEDBACK_REAR_HOLE_REMOVAL_FAILED", boundaryWidth, part => part.Bodies[0].Box[0] -= .00002);
            }
            Run("postrepair_900_remaining_half_hole_rejected", delegate { PartDoc part = SyntheticRear(900, 2000, 500); double[] before = NativeParametricAssembly.BoxForTests(part); var retained = NativeParametricAssembly.RetainedForTests(part); Reject(delegate { NativeParametricAssembly.PostRepairForTests(part, before, retained); }, "FEEDBACK_REAR_HOLE_REMOVAL_FAILED"); });
            RejectNarrow("820_missing_one_skin_face", "FEEDBACK_NARROW_REAR_SKIN_COUNT_DRIFT", part => RemoveFace(part, Skins(part)[0]));
            RejectNarrow("820_extra_skin_face", "FEEDBACK_NARROW_REAR_SKIN_COUNT_DRIFT", part => AddFace(part, Skins(part)[0]));
            RejectNarrow("820_skin_wrong_Z_datum", "FEEDBACK_NARROW_REAR_SKIN_DATUM_DRIFT", part => { Face2 skin = Skins(part)[0]; skin.Box[2] += .0004; skin.Box[5] += .0004; });
            RejectNarrow("820_skin_extra_square_loop", "FEEDBACK_NARROW_REAR_SKIN_LOOP_COUNT_DRIFT", part => { Face2 skin = Skins(part)[0]; skin.Loops = skin.Loops.Concat(new[] { new Loop2 { Outer = false, Edges = skin.Loops[0].Edges.Take(4).ToArray() } }).ToArray(); });
            RejectNarrow("820_skin_replaced_cutout_has_four_edges", Boundary, part => { Loop2 loop = Skins(part)[0].Loops[0]; loop.Edges = loop.Edges.Take(4).ToArray(); });
            RejectNarrow("820_skin_slot_vertex_changed", Boundary, part => Skins(part)[0].Loops[0].Edges[0].Start.Point[0] += .00002);
            RejectNarrow("820_skin_outer_contour_vertex_changed", Boundary, part => Skins(part)[0].Loops.Single(loop => loop.Outer).Edges[0].End.Point[1] += .00002);
            RejectNarrow("820_skin_edge_Z_changed", Boundary, part => Skins(part)[0].Loops[0].Edges[0].Start.Point[2] += .00002);
            RejectNarrow("820_skin_curved_edge_rejected", Boundary, part => Skins(part)[0].Loops[0].Edges[0].Curve.Line = false);
            RejectNarrow("820_skin_missing_edge_curve_rejected", Boundary, part => Skins(part)[0].Loops[0].Edges[0].Curve = null);
            RejectNarrow("820_skin_missing_edge_vertex_rejected", Boundary, part => Skins(part)[0].Loops[0].Edges[0].Start = null);
            RejectNarrow("820_skin_wrong_outer_flag", Boundary, part => Skins(part)[0].Loops.Single(loop => loop.Outer).Outer = false);
            RejectNarrow("820_skin_area_changed", "FEEDBACK_NARROW_REAR_SKIN_AREA_DRIFT", part => Skins(part)[0].Area += .00000001);
            Run("native_820_unordered_undirected_loop_edges_accepted", delegate {
                PartDoc part = Native820();
                foreach (Face2 skin in Skins(part)) {
                    skin.Loops = skin.Loops.Reverse().ToArray();
                    foreach (Loop2 loop in skin.Loops) { loop.Edges = loop.Edges.Reverse().ToArray(); foreach (Edge edge in loop.Edges) { Vertex prior = edge.Start; edge.Start = edge.End; edge.End = prior; } }
                }
                part.Bodies[0].Faces = part.Bodies[0].Faces.Reverse().ToArray();
                Check(NativeParametricAssembly.GateForTests(part, false).Outside, "native loops match independent of loop/edge order and edge direction");
            });
            Run("retained_match_permutation_and_small_tolerance_accepted", delegate {
                var before = NativeParametricAssembly.RetainedForTests(Native820()); var after = before.AsEnumerable().Reverse().Select(Copy).ToList(); after[0][9] += .0005;
                Check(NativeParametricAssembly.MatchForTests(before, after), "retained comparison is unordered and tolerates 0.0005 mm or mm2 differences");
            });
            Run("retained_match_missing_record_rejected", delegate { var before = NativeParametricAssembly.RetainedForTests(Native820()); Check(!NativeParametricAssembly.MatchForTests(before, before.Skip(1).ToList()), "missing retained hole does not match"); });
            Run("retained_match_duplicate_record_rejected", delegate { var before = NativeParametricAssembly.RetainedForTests(Native820()); var after = before.ToList(); after[0] = after[1]; Check(!NativeParametricAssembly.MatchForTests(before, after), "equal counts with duplicate geometry do not match"); });
            Run("retained_match_changed_area_rejected", delegate { var before = NativeParametricAssembly.RetainedForTests(Native820()); var after = before.Select(Copy).ToList(); after[0][9] += .002; Check(!NativeParametricAssembly.MatchForTests(before, after), "area drift beyond tolerance does not match"); });
            Run("retained_match_changed_record_length_rejected", delegate { var before = NativeParametricAssembly.RetainedForTests(Native820()); var after = before.Select(Copy).ToList(); after[0] = after[0].Take(9).ToArray(); Check(!NativeParametricAssembly.MatchForTests(before, after), "incomplete geometry records do not match"); });
            Console.WriteLine("REAR_WIDTH_TEST_RESULT " + new JavaScriptSerializer { MaxJsonLength = 4000000 }.Serialize(new { passed = Passed, failed = Failed, cases = Results }));
            return Failed == 0 ? 0 : 1;
        }
    }
}
