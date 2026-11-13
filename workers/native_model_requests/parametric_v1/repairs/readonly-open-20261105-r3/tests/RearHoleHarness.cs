using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

namespace SolidWorks.Interop.sldworks
{
    internal class ModelDoc2 { }
    internal sealed class PartDoc : ModelDoc2
    {
        internal Body2[] Bodies;
        internal int Calls, BodyType;
        internal bool VisibleOnly;
        internal object GetBodies2(int bodyType, bool visibleOnly) { Calls++; BodyType = bodyType; VisibleOnly = visibleOnly; return Bodies; }
    }
    internal sealed class Body2
    {
        internal Face2[] Faces;
        internal object GetFaces() { return Faces; }
    }
    internal sealed class Face2
    {
        internal string Name;
        internal Surface Surface;
        internal double[] Box;
        internal object GetSurface() { return Surface; }
        internal object GetBox() { return Box; }
    }
    internal sealed class Surface
    {
        internal bool Cylinder = true;
        internal double[] Parameters;
        internal bool IsCylinder() { return Cylinder; }
        internal object CylinderParams { get { return Parameters; } }
    }
}

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        /*__REAR_HOLES__*/

        internal static List<Face2> SelectForTests(ModelDoc2 model) { return FeedbackRearHoles(model); }
        internal static double[] PairGateForTests(List<Face2> holes, bool alreadyRepaired)
        {
            /*__PAIR_GATE__*/
            return removedHoleX;
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

    internal static class RearHoleTests
    {
        private static readonly List<CaseResult> Results = new List<CaseResult>();
        private static CaseResult Current;
        private static int Passed, Failed;
        private static Face2 Hole(string name, double xMm)
        {
            double yMm = ParametricContext.HeightMm - 70, zMm = -ParametricContext.DepthMm;
            return new Face2 {
                Name = name,
                Surface = new Surface { Parameters = new[] { xMm / 1000, yMm / 1000, zMm / 1000, 0, 0, 1, .00275 } },
                Box = new[] { (xMm - 2.75) / 1000, (yMm - 2.75) / 1000, zMm / 1000, (xMm + 2.75) / 1000, (yMm + 2.75) / 1000, (zMm + .8) / 1000 }
            };
        }
        private static PartDoc Part(params Face2[] faces) { return new PartDoc { Bodies = new[] { new Body2 { Faces = faces } } }; }
        private static List<Face2> Select(params Face2[] faces) { return NativeParametricAssembly.SelectForTests(Part(faces)); }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("TEST_ASSERTION_FAILED " + description);
            Current.checks.Add(description);
        }
        private static void Gate(Action action, string expected)
        {
            Current.expectedFailure = expected;
            try { action(); }
            catch (InvalidOperationException error) {
                Current.observedFailure = error.Message;
                Check(error.Message == expected, "rejected at " + expected);
                return;
            }
            throw new InvalidOperationException("TEST_EXPECTED_GATE_NOT_RAISED " + expected);
        }
        private static void Run(string name, Action action)
        {
            Current = new CaseResult { name = name }; Results.Add(Current);
            ParametricContext.WidthMm = 960; ParametricContext.HeightMm = 2000; ParametricContext.DepthMm = 500;
            try { action(); Current.status = "PASS"; Passed++; }
            catch (Exception error) { Current.status = "FAIL"; Current.error = error.ToString(); Failed++; }
        }
        private static void RejectGeometry(string name, Action<Face2> change)
        {
            Run(name, delegate {
                Face2 target = Hole("valid-rear", -450), wrong = Hole("excluded", 450); change(wrong);
                List<Face2> selected = Select(wrong, target);
                Check(selected.Count == 1 && object.ReferenceEquals(selected[0], target), "only unchanged rear-skin target is selected");
            });
        }
        private static void RejectPair(string name, string expected, params double[] xMm)
        {
            Run(name, delegate {
                List<Face2> selected = Select(xMm.Select((x, i) => Hole("hole-" + i, x)).ToArray());
                Check(selected.Count == xMm.Length, "all supplied target-skin candidates reach the production pair gate");
                Gate(delegate { NativeParametricAssembly.PairGateForTests(selected, false); }, expected);
            });
        }
        private static int Main()
        {
            Run("W960_H2000_D500_actual_X450_pair_accepted", delegate {
                Face2 left = Hole("rear-left", -450), right = Hole("rear-right", 450);
                PartDoc part = Part(right, left);
                List<Face2> selected = NativeParametricAssembly.SelectForTests(part);
                Check(selected.Count == 2 && selected.Contains(left) && selected.Contains(right), "both actual rear holes at X=-450/+450 are selected");
                double oldExpected = ParametricContext.WidthMm / 2 - 50;
                Check(oldExpected == 430 && Math.Abs(450 - oldExpected) == 20, "fixture is 20 mm outside the obsolete +/-430 width formula");
                Check(NativeParametricAssembly.PairGateForTests(selected, false).SequenceEqual(new[] { -450.0, 450.0 }), "extracted count and symmetric pair gates accept sorted actual X values");
                Check(part.Calls == 1 && part.BodyType == 0 && !part.VisibleOnly, "real selector enumerates all requested bodies once");
            });
            Run("same_height_front_skin_eight_holes_excluded", delegate {
                Face2 left = Hole("rear-left", -450), right = Hole("rear-right", 450);
                Face2[] front = Enumerable.Range(0, 8).Select(index => {
                    Face2 face = Hole("retained-front-" + index, -450 + index * 900.0 / 7);
                    face.Box[2] = -.4858; face.Box[5] = -.485; face.Surface.Parameters[2] = -.4858;
                    return face;
                }).ToArray();
                PartDoc part = new PartDoc { Bodies = new[] { new Body2 { Faces = front.Take(4).Concat(new[] { left }).ToArray() }, new Body2 { Faces = front.Skip(4).Concat(new[] { right }).ToArray() } } };
                List<Face2> selected = NativeParametricAssembly.SelectForTests(part);
                Check(selected.Count == 2 && selected.Contains(left) && selected.Contains(right), "only two Z=-500..-499.2 rear-skin holes are selected across bodies");
                Check(front.All(face => !selected.Contains(face)), "all eight same-height Z=-485.8..-485 front-skin holes stay outside selection");
                Check(NativeParametricAssembly.PairGateForTests(selected, false).Length == 2, "excluded front holes do not trigger a false target count");
            });
            Run("negative_Z_axis_direction_accepted", delegate {
                Face2 face = Hole("negative-axis", -450); face.Surface.Parameters[5] = -1;
                Check(Select(face).Count == 1, "absolute Z axis allows reversed cylinder direction");
            });
            RejectGeometry("wrong_radius_too_large_excluded", face => face.Surface.Parameters[6] = .002752);
            RejectGeometry("wrong_radius_too_small_excluded", face => face.Surface.Parameters[6] = .002748);
            RejectGeometry("wrong_X_axis_excluded", face => { face.Surface.Parameters[3] = 1; face.Surface.Parameters[5] = 0; });
            RejectGeometry("wrong_Y_axis_excluded", face => { face.Surface.Parameters[4] = 1; face.Surface.Parameters[5] = 0; });
            RejectGeometry("tilted_Z_axis_excluded", face => { face.Surface.Parameters[3] = Math.Sqrt(1 - .999 * .999); face.Surface.Parameters[5] = .999; });
            RejectGeometry("wrong_Y_above_height_datum_excluded", face => face.Surface.Parameters[1] += .000002);
            RejectGeometry("wrong_Y_below_height_datum_excluded", face => face.Surface.Parameters[1] -= .000002);
            RejectGeometry("wrong_rear_skin_thickness_thin_excluded", face => face.Box[5] -= .0001);
            RejectGeometry("wrong_rear_skin_thickness_thick_excluded", face => face.Box[5] += .0001);
            RejectGeometry("wrong_lower_Z_same_thickness_excluded", face => { face.Box[2] += .0001; face.Box[5] += .0001; });
            RejectGeometry("wrong_lower_Z_only_excluded", face => face.Box[2] -= .0001);
            RejectGeometry("noncylindrical_surface_excluded", face => face.Surface.Cylinder = false);
            RejectGeometry("null_surface_excluded", face => face.Surface = null);
            Run("radius_Y_and_Z_within_existing_tolerance_accepted", delegate {
                Face2 face = Hole("within-tolerances", 450);
                face.Surface.Parameters[6] += .0000005; face.Surface.Parameters[1] += .0000005;
                face.Box[2] += .0000005; face.Box[5] += .0000005;
                Check(Select(face).Count == 1, "0.0005 mm variation remains within the real 0.001 mm tolerances");
            });
            Run("height_and_depth_datums_follow_context", delegate {
                ParametricContext.HeightMm = 2100; ParametricContext.DepthMm = 550;
                Face2 face = Hole("new-datums", 450);
                Check(Select(face).Count == 1, "Y=2030 and Z=-550..-549.2 follow current H/D");
                face.Surface.Parameters[1] = 1.93;
                Check(Select(face).Count == 0, "old height datum no longer matches changed context");
            });
            RejectPair("unrepaired_zero_target_holes_rejected", "FEEDBACK_REAR_HOLE_COUNT_DRIFT");
            RejectPair("unrepaired_one_target_hole_rejected", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", -450);
            RejectPair("unrepaired_three_target_holes_rejected", "FEEDBACK_REAR_HOLE_COUNT_DRIFT", -450, 450, 100);
            RejectPair("two_left_holes_rejected", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", -450, -400);
            RejectPair("two_right_holes_rejected", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", 450, 400);
            RejectPair("asymmetric_signed_pair_rejected", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", -450, 449);
            RejectPair("zero_center_member_rejected", "FEEDBACK_REAR_HOLE_PAIR_DRIFT", 0, 450);
            Run("symmetric_pair_inside_current_tolerance_accepted", delegate {
                List<Face2> selected = Select(Hole("left", -450), Hole("right", 450.0005));
                Check(NativeParametricAssembly.PairGateForTests(selected, false).Length == 2, "symmetric-X sum within 0.001 mm passes the extracted pair gate");
            });
            Run("already_repaired_zero_targets_accepted", delegate {
                Check(NativeParametricAssembly.PairGateForTests(Select(), true).Length == 0, "existing repair marker requires zero targets");
            });
            Run("already_repaired_remaining_targets_rejected", delegate {
                Gate(delegate { NativeParametricAssembly.PairGateForTests(Select(Hole("left", -450), Hole("right", 450)), true); }, "FEEDBACK_REAR_HOLE_COUNT_DRIFT");
            });
            Console.WriteLine("REAR_HOLE_TEST_RESULT " + new JavaScriptSerializer().Serialize(new { passed = Passed, failed = Failed, cases = Results }));
            return Failed == 0 ? 0 : 1;
        }
    }
}
