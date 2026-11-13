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
        internal object GetSurface() { return Surface; }
        internal object GetBox() { return Box; }
        internal double GetArea() { return Area; }
        internal object GetLoops() { return Loops; }
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
        internal object GetCurve() { return Curve; }
        internal object GetStartVertex() { return Start; }
        internal object GetEndVertex() { return End; }
    }
    internal sealed class Curve
    {
        internal bool Line;
        internal bool IsLine() { return Line; }
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
            faces.AddRange(Fixture.wideRetained.Select((value, index) => Hole("native-1000-retained-" + index, new[] { value[0] / 1000, value[1] / 1000, value[5] / 1000, 0, 0, -1, value[2] / 1000 }, value.Skip(3).Take(6).ToArray(), value[9])));
            return new PartDoc { Bodies = new[] { new Body2 { Box = new[] { -.5, 1.9222, -.5, .5, 2, -.4388 }, Faces = faces.ToArray() } } };
        }
        private static List<Face2> RetainedFaces(PartDoc part)
        {
            return part.Bodies.SelectMany(body => body.Faces).Where(face => face.Surface.Cylinder && Math.Abs(Math.Abs(face.Surface.Parameters[5]) - 1) < .000001 && Math.Abs(face.Surface.Parameters[6] * 1000 - 2.75) < .001 && face.Box[2] * 1000 > -ParametricContext.DepthMm + 1).ToList();
        }
        private static List<Face2> Skins(PartDoc part) { return part.Bodies.SelectMany(body => body.Faces).Where(face => face.Loops != null).ToList(); }
        private static void RemoveFace(PartDoc part, Face2 face) { part.Bodies[0].Faces = part.Bodies[0].Faces.Where(value => !object.ReferenceEquals(value, face)).ToArray(); }
        private static void AddFace(PartDoc part, Face2 face) { part.Bodies[0].Faces = part.Bodies[0].Faces.Concat(new[] { face }).ToArray(); }
        private static PartDoc Resized820(double width)
        {
            PartDoc part = Native820(); ParametricContext.WidthMm = width;
            part.Bodies[0].Box[0] = -width / 2000; part.Bodies[0].Box[3] = width / 2000;
            foreach (Face2 skin in Skins(part)) {
                skin.Box[0] = -width / 2000; skin.Box[3] = width / 2000;
                skin.Area += (width - 820) * 75.2 / 1000000;
                foreach (Loop2 loop in skin.Loops.Where(loop => loop.Outer)) foreach (Edge edge in loop.Edges) foreach (Vertex vertex in new[] { edge.Start, edge.End }) vertex.Point[0] += Math.Sign(vertex.Point[0]) * (width - 820) / 2000;
            }
            return part;
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
            Run("native_1000_pair_selection_remains_two", delegate {
                PartDoc part = Native1000(); List<Face2> selected = NativeParametricAssembly.SelectForTests(part); GateResult result = NativeParametricAssembly.GateForTests(part, false);
                Check(selected.Count == 2 && selected.All(face => face.Name.StartsWith("native-1000-target-")), "only the native 1000 rear pair is selected");
                Check(!result.Outside && result.Count == 2 && Math.Abs(result.Removed[0] + 450) < .001 && Math.Abs(result.Removed[1] - 450) < .001, "the extracted production count, full-circle and pair gates accept the native pair");
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
            Run("width_894p497_complete_narrow_fixture_accepted", delegate { Check(NativeParametricAssembly.GateForTests(Resized820(894.497), false).Outside, "width immediately below the conservative cutoff still passes full geometry"); });
            foreach (double width in new[] { 894.498, 894.5, 900, 905.5 }) {
                double boundaryWidth = width;
                Run("width_" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_zero_targets_rejected", delegate {
                    PartDoc part = Native1000(); ParametricContext.WidthMm = boundaryWidth; foreach (Face2 face in NativeParametricAssembly.SelectForTests(part)) RemoveFace(part, face);
                    Check(!NativeParametricAssembly.OutsideForTests(part), "cutoff or larger widths do not enter the outside path");
                    Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, "FEEDBACK_REAR_HOLE_COUNT_DRIFT");
                });
            }
            foreach (double width in new[] { 894.5, 900, 905.5 }) {
                double boundaryWidth = width;
                Run("width_" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_partial_pair_rejected", delegate {
                    PartDoc part = Native1000(); ParametricContext.WidthMm = boundaryWidth;
                    List<Face2> selected = NativeParametricAssembly.SelectForTests(part);
                    if (boundaryWidth < 905.5) foreach (Face2 face in selected) { face.Box[0] = Math.Max(face.Box[0], -boundaryWidth / 2000); face.Box[3] = Math.Min(face.Box[3], boundaryWidth / 2000); }
                    else selected[0].Box[0] += .00002;
                    Check(!NativeParametricAssembly.OutsideForTests(part) && NativeParametricAssembly.SelectForTests(part).Count == 2, "partial candidates reach the normal two-hole branch");
                    Reject(delegate { NativeParametricAssembly.GateForTests(part, false); }, "FEEDBACK_REAR_HOLE_PARTIAL_CIRCLE");
                });
            }
            Run("width_905p5_complete_pair_accepted", delegate { PartDoc part = Native1000(); ParametricContext.WidthMm = 905.5; GateResult result = NativeParametricAssembly.GateForTests(part, false); Check(!result.Outside && result.Count == 2, "full circles at the source pair extent pass the normal branch"); });
            RejectPair("wide_partial_circle_area_only", "FEEDBACK_REAR_HOLE_PARTIAL_CIRCLE", (part, selected) => selected[0].Area /= 2);
            RejectPair("wide_partial_circle_Y_bbox_only", "FEEDBACK_REAR_HOLE_PARTIAL_CIRCLE", (part, selected) => selected[0].Box[1] += .00002);
            RejectPair("wide_two_left_holes", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", (part, selected) => MoveX(selected[1], -850));
            RejectPair("wide_asymmetric_pair", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", (part, selected) => MoveX(selected[1], -1));
            RejectPair("wide_one_target_missing", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", (part, selected) => RemoveFace(part, selected[0]));
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
