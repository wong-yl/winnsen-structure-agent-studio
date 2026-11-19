using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

internal static partial class TrialAssembly
{
    const string CraftMarkPrefix = "生产打标_焊接定位_";
    const double CraftTol = 1e-6;
    const double CraftMatTol = 1e-7;
    const double CraftShortLeg = 0.003;
    const double CraftSideEnd = 0.050;
    const double CraftPartitionStandOff = 0.0005;
    const double CraftSideStandOff = 0.0002;

    static List<Feature> CraftFeatures(ModelDoc2 model)
    {
        var result = new List<Feature>();
        var seen = new HashSet<int>();
        Action<Feature, int> visit = null;
        visit = delegate(Feature feature, int depth)
        {
            while (feature != null)
            {
                VariableTrial.RuntimeOwnership();
                Need(depth < 64 && result.Count < 10000, "CRAFT_FEATURE_TRAVERSAL_LIMIT");
                Feature next = depth == 0 ? feature.GetNextFeature() as Feature : feature.GetNextSubFeature() as Feature;
                int id = feature.GetID();
                if (seen.Add(id))
                {
                    result.Add(feature);
                    Feature sub = feature.GetFirstSubFeature() as Feature;
                    if (sub != null) visit(sub, depth + 1);
                }
                else
                {
                    Release(feature);
                }
                feature = next;
            }
        };
        visit(model.FirstFeature() as Feature, 0);
        return result;
    }

    static List<Feature> CraftTopFeatures(ModelDoc2 model)
    {
        var result = new List<Feature>();
        Feature f = model.FirstFeature() as Feature;
        int guard = 0;
        while (f != null && guard++ < 10000)
        {
            result.Add(f);
            f = f.GetNextFeature() as Feature;
        }
        return result;
    }

    static void ReleaseFeatureList(IEnumerable<Feature> features, Feature keep = null)
    {
        if (features == null) return;
        foreach (Feature f in features)
        {
            if (f != null && !ReferenceEquals(f, keep)) Release(f);
        }
    }

    static List<Face2> CraftBodyFaces(ModelDoc2 model)
    {
        var result = new List<Face2>();
        object[] rawBodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
        try
        {
            Need(rawBodies.Length == 1, "CRAFT_REQUIRES_ONE_SOLID " + VariableTrial.ModelGetPathName(model, "CraftBodyFaces"));
            Body2 body = rawBodies[0] as Body2;
            Need(body != null, "CRAFT_SOLID_BODY_REQUIRED");
            object[] rawFaces = Items(body.GetFaces());
            foreach (object item in rawFaces)
            {
                Face2 face = item as Face2;
                if (face != null) result.Add(face);
                else Release(item);
            }
        }
        finally
        {
            foreach (object b in rawBodies) Release(b);
        }
        return result;
    }

    static double CraftSolidVolumeM3(ModelDoc2 model)
    {
        object[] rawBodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
        try
        {
            Need(rawBodies.Length == 1, "CRAFT_VOLUME_ONE_BODY_REQUIRED");
            Body2 body = (Body2)rawBodies[0];
            double[] mass = Numbers(body.GetMassProperties(1));
            Need(mass.Length >= 4 && mass[3] > 0, "CRAFT_MASS_PROPS_INVALID");
            return mass[3];
        }
        finally
        {
            foreach (object b in rawBodies) Release(b);
        }
    }

    static double[] CraftPoint(MathUtility math, double[] point, MathTransform transform)
    {
        MathPoint source = math.CreatePoint(point) as MathPoint;
        MathPoint target = source == null ? null : source.MultiplyTransform(transform) as MathPoint;
        try
        {
            Need(target != null, "CRAFT_POINT_TRANSFORM_FAILED");
            return Numbers(target.ArrayData);
        }
        finally
        {
            Release(target);
            Release(source);
        }
    }

    static double[] CraftFaceAxisLimits(Face2 face, int axis)
    {
        var coords = new List<double>();
        object[] edges = Items(face.GetEdges());
        try
        {
            foreach (object item in edges)
            {
                Edge edge = item as Edge;
                Curve curve = edge == null ? null : edge.GetCurve() as Curve;
                try
                {
                    double[] pars = edge == null ? null : edge.GetCurveParams2() as double[];
                    Need(curve != null && pars != null && pars.Length >= 8, "CRAFT_EXACT_EDGE_UNAVAILABLE");
                    foreach (double parameter in new[] { pars[6], pars[7] })
                    {
                        double[] pt = Numbers(curve.Evaluate2(parameter, 0));
                        Need(pt.Length >= 3, "CRAFT_EXACT_EDGE_POINT_UNAVAILABLE");
                        coords.Add(pt[axis]);
                    }
                }
                finally
                {
                    Release(curve);
                }
            }
        }
        finally
        {
            foreach (object item in edges) Release(item);
        }
        Need(coords.Count > 0, "CRAFT_FACE_AXIS_LIMITS_EMPTY");
        return new[] { coords.Min(), coords.Max() };
    }

    static List<double[]> CraftFaceVertices(Face2 face)
    {
        var points = new List<double[]>();
        object[] edges = Items(face.GetEdges());
        try
        {
            foreach (object item in edges)
            {
                Edge edge = item as Edge;
                if (edge == null) continue;
                Vertex vs = edge.GetStartVertex() as Vertex;
                Vertex ve = edge.GetEndVertex() as Vertex;
                try
                {
                    foreach (Vertex v in new[] { vs, ve })
                    {
                        if (v == null) continue;
                        double[] pt = Numbers(v.GetPoint());
                        if (!points.Any(p => p.Zip(pt, (a, b) => Math.Abs(a - b) < 1e-8).All(x => x))) points.Add(pt);
                    }
                }
                finally
                {
                    Release(ve);
                    Release(vs);
                }
            }
        }
        finally
        {
            foreach (object item in edges) Release(item);
        }
        return points;
    }

    static double[] CraftFaceVertexBounds(Face2 face)
    {
        List<double[]> points = CraftFaceVertices(face);
        Need(points.Count >= 4, "CRAFT_FACE_VERTICES_MISSING");
        return Enumerable.Range(0, 3).Select(i => points.Min(p => p[i]))
            .Concat(Enumerable.Range(0, 3).Select(i => points.Max(p => p[i]))).ToArray();
    }

    static List<Face2> CraftCylinders(ModelDoc2 model, double radiusM, int axis)
    {
        var all = CraftBodyFaces(model);
        var keep = new List<Face2>();
        foreach (Face2 f in all)
        {
            Surface s = f.GetSurface() as Surface;
            try
            {
                if (s != null && s.IsCylinder())
                {
                    double[] c = Numbers(s.CylinderParams);
                    if (c.Length >= 7 && Math.Abs(c[6] - radiusM) < 5e-6 && Math.Abs(Math.Abs(c[axis + 3]) - 1.0) < 1e-6)
                    {
                        keep.Add(f);
                        continue;
                    }
                }
            }
            finally { Release(s); }
            Release(f);
        }
        return keep;
    }

    static List<Face2> CraftLocatorHoles(ModelDoc2 model, int axis)
    {
        List<Face2> cyls = CraftCylinders(model, 0.001, axis);
        var keep = new List<Face2>();
        foreach (Face2 f in cyls)
        {
            double[] range = CraftFaceAxisLimits(f, axis);
            if (Math.Abs((range[1] - range[0]) - 0.0008) < 1e-6 && Math.Abs(f.GetArea() - 2.0 * Math.PI * 0.001 * 0.0008) < 5e-10)
                keep.Add(f);
            else
                Release(f);
        }
        return keep;
    }

    static Face2 CraftFindPlanarFace(ModelDoc2 model, double[] pointM, int normalAxis)
    {
        var all = CraftBodyFaces(model);
        var matches = new List<Face2>();
        foreach (Face2 f in all)
        {
            Surface s = f.GetSurface() as Surface;
            try
            {
                if (s != null && s.IsPlane())
                {
                    double[] n = Numbers(f.Normal);
                    if (n.Length == 3 && Math.Abs(n[normalAxis]) >= 0.999999)
                    {
                        double[] q = Numbers(f.GetClosestPointOn(pointM[0], pointM[1], pointM[2]));
                        if (q.Length >= 3 && Enumerable.Range(0, 3).Sum(i => (q[i] - pointM[i]) * (q[i] - pointM[i])) < 1e-12)
                        {
                            matches.Add(f);
                            continue;
                        }
                    }
                }
            }
            finally { Release(s); }
            Release(f);
        }
        Need(matches.Count == 1, "CRAFT_MATERIAL_FACE_AMBIGUOUS count=" + matches.Count);
        return matches[0];
    }

    static Face2 CraftFindDoorReturnFace(ModelDoc2 model, int sideSign)
    {
        var all = CraftBodyFaces(model);
        var matches = new List<Face2>();
        foreach (Face2 f in all)
        {
            Surface s = f.GetSurface() as Surface;
            try
            {
                if (s != null && s.IsPlane())
                {
                    double[] n = Numbers(f.Normal);
                    if (n.Length == 3 && n[2] < -0.999999 && f.GetArea() > 0.00002)
                    {
                        double[] b = CraftFaceVertexBounds(f);
                        if (b[5] < -0.01 && Math.Sign((b[0] + b[3]) / 2.0) == sideSign && b[1] < 0 && b[4] > 0 && Math.Abs((b[3] - b[0]) - 0.005) < 5e-5)
                        {
                            matches.Add(f);
                            continue;
                        }
                    }
                }
            }
            finally { Release(s); }
            Release(f);
        }
        Need(matches.Count == 1, "CRAFT_DOOR_RETURN_FACE_AMBIGUOUS count=" + matches.Count + " side=" + sideSign);
        return matches[0];
    }

    static void CraftEnsureBeforeFlatPattern(ModelDoc2 model, string featureName)
    {
        List<Feature> top = CraftTopFeatures(model);
        try
        {
            int featIdx = -1, flatIdx = -1;
            string flatName = null;
            for (int i = 0; i < top.Count; i++)
            {
                string n = top[i].Name;
                string t = top[i].GetTypeName2();
                if (n == featureName) featIdx = i;
                if (t == "FlatPattern") { flatIdx = i; flatName = n; }
            }
            if (featIdx >= 0 && flatIdx >= 0 && featIdx > flatIdx && !string.IsNullOrEmpty(flatName))
            {
                bool ok = model.Extension.ReorderFeature(featureName, flatName, (int)swMoveLocation_e.swMoveBefore);
                Need(ok, "CRAFT_REORDER_BEFORE_FLAT_FAILED " + featureName);
            }
        }
        finally
        {
            ReleaseFeatureList(top);
        }
    }

    static void CraftFixSegment(ModelDoc2 model, SketchSegment segment)
    {
        Need(segment != null, "CRAFT_SKETCH_SEGMENT_MISSING");
        VariableTrial.ModelClearSelection2(model, true, "CraftFixSegment");
        Need(segment.Select4(false, null), "CRAFT_SKETCH_SEGMENT_SELECT_FAILED");
        model.SketchAddConstraints("sgFIXED");
    }

    static Feature CraftCreateSketchAndCut(BuildContext c, ModelDoc2 model, Face2 face, string sketchName, string cutName, double depthM, bool bothWays, Action<Sketch, MathUtility> draw)
    {
        ActivatePartForEdit(c, model);
        List<Feature> beforeList = CraftTopFeatures(model);
        var beforeNames = new HashSet<string>(beforeList.Select(f => f.Name), StringComparer.Ordinal);
        ReleaseFeatureList(beforeList);
        Need(!beforeNames.Contains(sketchName) && !beforeNames.Contains(cutName), "CRAFT_FEATURE_ALREADY_EXISTS " + cutName);

        VariableTrial.ModelClearSelection2(model, true, "CraftCreateSketchAndCut.Face");
        Need(((Entity)face).Select4(false, null), "CRAFT_SKETCH_FACE_SELECT_FAILED " + cutName);
        SketchManager mgr = model.SketchManager;
        bool prevDb = mgr.AddToDB, prevDisp = mgr.DisplayWhenAdded;
        MathUtility math = VariableTrial.SwGetMathUtility(c.Sw, "CraftCreateSketchAndCut");
        bool inSketch = false;
        try
        {
            mgr.InsertSketch(true);
            inSketch = true;
            Sketch sk = model.GetActiveSketch2() as Sketch;
            Need(sk != null, "CRAFT_SKETCH_NOT_ACTIVE " + sketchName);
            mgr.AddToDB = true;
            mgr.DisplayWhenAdded = false;
            try
            {
                draw(sk, math);
            }
            finally
            {
                Release(sk);
                mgr.AddToDB = prevDb;
                mgr.DisplayWhenAdded = prevDisp;
                VariableTrial.ModelClearSelection2(model, true, "CraftCreateSketchAndCut.CloseSketch");
                mgr.InsertSketch(true);
                inSketch = false;
            }
        }
        finally
        {
            if (inSketch)
            {
                try { mgr.AddToDB = prevDb; mgr.DisplayWhenAdded = prevDisp; mgr.InsertSketch(true); }
                catch { }
            }
            Release(math);
        }

        List<Feature> afterSkList = CraftTopFeatures(model);
        Feature skFeat = afterSkList.FirstOrDefault(f => !beforeNames.Contains(f.Name) && f.GetSpecificFeature2() is Sketch);
        Need(skFeat != null, "CRAFT_CREATED_SKETCH_NOT_FOUND " + sketchName);
        skFeat.Name = sketchName;
        ReleaseFeatureList(afterSkList, skFeat);

        try
        {
            VariableTrial.ModelClearSelection2(model, true, "CraftCreateSketchAndCut.SelectSk");
            Need(skFeat.Select2(false, 0), "CRAFT_CUT_SKETCH_SELECT_FAILED " + cutName);
            Feature cut = model.FeatureManager.FeatureCut3(!bothWays, false, false, 0, 0, depthM, depthM, false, false, false, false, 0, 0, false, false, false, false, false, true, true, false, false, false, 0, 0, false);
            Need(cut != null, "CRAFT_FEATURE_CUT_FAILED " + cutName);
            cut.Name = cutName;
            Need(cut.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null), "CRAFT_CUT_UNSUPPRESS_FAILED " + cutName);
            VariableTrial.ModelClearSelection2(model, true, "CraftCreateSketchAndCut.AfterCut");
            CraftEnsureBeforeFlatPattern(model, cutName);
            Need(VariableTrial.ModelForceRebuild3(model, false, "CraftCreateSketchAndCut.Rebuild." + cutName), "CRAFT_CUT_REBUILD_FAILED " + cutName);
            return cut;
        }
        finally
        {
            Release(skFeat);
        }
    }

    static object ApplyDoorProductionCraft(BuildContext c, ModelDoc2 model, string side, double doorWidthMm)
    {
        ActivatePartForEdit(c, model);
        List<Face2> holes = CraftCylinders(model, 0.0051, 1)
            .OrderBy(f => CraftFaceAxisLimits(f, 1)[0])
            .ToList();
        Need(holes.Count == 2, "CRAFT_DOOR_PIN_HOLE_COUNT_INVALID actual=" + holes.Count);
        try
        {
            double[][] cyls = new double[2][];
            double[][] ranges = new double[2][];
            var ownerList = new List<Feature>();
            for (int i = 0; i < 2; i++)
            {
                Surface s = holes[i].GetSurface() as Surface;
                try { cyls[i] = Numbers(s.CylinderParams); }
                finally { Release(s); }
                ranges[i] = CraftFaceAxisLimits(holes[i], 1);
                Feature ow = holes[i].GetFeature() as Feature;
                if (ow != null && !ownerList.Any(x => x.Name == ow.Name)) ownerList.Add(ow);
                else Release(ow);
            }

            double nominal = cyls[0][0], z = cyls[0][2];
            Need(Math.Abs(cyls[1][0] - nominal) < 1e-6 && Math.Abs(cyls[1][2] - z) < 1e-6
                && Math.Abs(Math.Abs(nominal) * 1000.0 - (doorWidthMm / 2.0 - 10.0)) < 0.02, "CRAFT_DOOR_PIN_NOMINAL_DRIFT");
            Need(ownerList.Count == 1, "CRAFT_DOOR_PIN_OWNER_AMBIGUOUS count=" + ownerList.Count);
            Feature coupledCut = ownerList[0];
            try
            {
                string owType = coupledCut.GetTypeName2();
                string owUnder = coupledCut.GetTypeName();
                Need(owType == "Cut" || (owType == "ICE" && owUnder == "Cut"), "CRAFT_DOOR_PIN_COUPLED_TYPE_INVALID " + owType + "/" + owUnder);
                Need(coupledCut.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null), "CRAFT_DOOR_PIN_SUPPRESS_FAILED");
                Need(VariableTrial.ModelForceRebuild3(model, false, "ApplyDoorProductionCraft.SuppressPin"), "CRAFT_DOOR_PIN_SUPPRESS_REBUILD_FAILED");
                string[] cfgNames = Items(model.GetConfigurationNames()).Select(Convert.ToString).ToArray();
                foreach (string cfg in cfgNames)
                {
                    object[] st = Items(coupledCut.IsSuppressed2((int)swInConfigurationOpts_e.swSpecifyConfiguration, new[] { cfg }));
                    Need(st.Length == 1 && Convert.ToBoolean(st[0], Invariant), "CRAFT_DOOR_PIN_NOT_SUPPRESSED_IN_CONFIG " + cfg);
                }
            }
            finally
            {
                foreach (Feature ow in ownerList) Release(ow);
            }

            double[] ys = { ranges[0][0], ranges[1][1] };
            for (int index = 0; index < 2; index++)
            {
                bool isUpper = (index == 1);
                double x = nominal - (isUpper ? Math.Sign(nominal) * 0.0005 : 0.0);
                double y = ys[index];
                string cutName = isUpper ? "生产门销_上端预偏0p5" : "生产门销_下端原轴";
                Face2 face = CraftFindPlanarFace(model, new[] { x, y, z }, 1);
                try
                {
                    Feature fCut = CraftCreateSketchAndCut(c, model, face, cutName + "_草图", cutName, 0.000801, false,
                        delegate(Sketch sk, MathUtility math)
                        {
                            double[] pt = CraftPoint(math, new[] { x, y, z }, sk.ModelToSketchTransform);
                            mgrCreateCircle(model, sk, pt[0], pt[1], 0.0051);
                        });
                    Release(fCut);
                }
                finally { Release(face); }
            }

            // Craft 1: Door left/right identification notch on the 5 mm hinge rear return flange
            int sideSign = side == "L" ? -1 : 1;
            Need(Math.Sign(nominal) == sideSign, "CRAFT_DOOR_SIDE_SIGN_MISMATCH side=" + side + " nominal=" + nominal);
            Face2 retFace = CraftFindDoorReturnFace(model, sideSign);
            double edgeMm, midMm, retZMm, removedMm3, expectedMm3;
            string notchName = sideSign < 0 ? "生产识别缺口_L_半圆_R1p5" : "生产识别缺口_R_三角_3x1p5";
            try
            {
                double[] box = CraftFaceVertexBounds(retFace);
                double edge = sideSign < 0 ? box[3] : box[0];
                double middle = (box[1] + box[4]) / 2.0;
                double retZ = box[2];
                double radius = 0.0015;
                double volBefore = CraftSolidVolumeM3(model);
                Feature notchCut = CraftCreateSketchAndCut(c, model, retFace, notchName + "_草图", notchName, 0.001, true,
                    delegate(Sketch sk, MathUtility math)
                    {
                        if (sideSign < 0)
                        {
                            double[] pt = CraftPoint(math, new[] { edge, middle, retZ }, sk.ModelToSketchTransform);
                            mgrCreateCircle(model, sk, pt[0], pt[1], radius);
                        }
                        else
                        {
                            double[][] pts = new[]
                            {
                                new[] { edge - sideSign * 0.0005, middle - 0.002, retZ },
                                new[] { edge + sideSign * 0.0015, middle, retZ },
                                new[] { edge - sideSign * 0.0005, middle + 0.002, retZ }
                            };
                            for (int i = 0; i < 3; i++)
                            {
                                double[] pa = CraftPoint(math, pts[i], sk.ModelToSketchTransform);
                                double[] pb = CraftPoint(math, pts[(i + 1) % 3], sk.ModelToSketchTransform);
                                SketchSegment seg = model.SketchManager.CreateLine(pa[0], pa[1], 0, pb[0], pb[1], 0);
                                CraftFixSegment(model, seg);
                            }
                        }
                    });
                Release(notchCut);
                double volAfter = CraftSolidVolumeM3(model);
                removedMm3 = (volBefore - volAfter) * 1e9;
                expectedMm3 = (sideSign < 0 ? Math.PI * 1.5 * 1.5 / 2.0 : 1.5 * 1.5) * 0.8;
                Need(Math.Abs(removedMm3 - expectedMm3) < 0.02, "CRAFT_NOTCH_VOLUME_MISMATCH actualMm3=" + removedMm3 + " expectedMm3=" + expectedMm3);
                edgeMm = edge * 1000.0;
                midMm = middle * 1000.0;
                retZMm = retZ * 1000.0;
            }
            finally { Release(retFace); }

            // Verify final pin holes & zero locator holes
            List<Face2> finalPins = CraftCylinders(model, 0.0051, 1)
                .OrderBy(f => CraftFaceAxisLimits(f, 1)[0])
                .ToList();
            try
            {
                Need(finalPins.Count == 2, "CRAFT_DOOR_PIN_FINAL_COUNT");
                for (int i = 0; i < 2; i++)
                {
                    Surface s = finalPins[i].GetSurface() as Surface;
                    double[] cp;
                    try { cp = Numbers(s.CylinderParams); }
                    finally { Release(s); }
                    double expectedXmm = sideSign * ((doorWidthMm / 2.0 - 10.0) - (i == 1 ? 0.5 : 0.0));
                    Need(Math.Abs(cp[0] * 1000.0 - expectedXmm) < 0.005, "CRAFT_DOOR_PIN_PREBIAS_VERIFY_FAILED actual=" + (cp[0] * 1000.0) + " expected=" + expectedXmm);
                }
            }
            finally
            {
                foreach (Face2 f in finalPins) Release(f);
            }
            List<Face2> doorLocs = CraftLocatorHoles(model, 2);
            int locRemain = doorLocs.Count;
            foreach (Face2 f in doorLocs) Release(f);
            Need(locRemain == 0, "CRAFT_DOOR_LOCATOR_HOLES_REMAIN count=" + locRemain);

            return new
            {
                inspection = "variable_trial_door_production_craft",
                side = side,
                upperPinPrebiasMm = 0.5,
                pinDiameterMm = 10.2,
                notchFeature = notchName,
                notchKind = sideSign < 0 ? "L_semicircle_R1p5" : "R_triangle_3x1p5",
                freeEdgeXmm = edgeMm,
                midpointYmm = midMm,
                surfaceZmm = retZMm,
                removedVolumeMm3 = removedMm3,
                expectedVolumeMm3 = expectedMm3
            };
        }
        finally
        {
            foreach (Face2 f in holes) Release(f);
        }
    }

    static void mgrCreateCircle(ModelDoc2 model, Sketch sk, double x, double y, double radiusM)
    {
        SketchManager mgr = model.SketchManager;
        bool wasDb = mgr.AddToDB;
        SketchSegment seg = mgr.CreateCircleByRadius(x, y, 0, radiusM);
        CraftFixSegment(model, seg);
        mgr.AddToDB = wasDb;
    }

    static object ApplyShelfLocatorHoleRemoval(BuildContext c, ModelDoc2 model, int expectedIfPresent, bool allowZeroFromMirror)
    {
        ActivatePartForEdit(c, model);
        List<Face2> holes = CraftLocatorHoles(model, 1);
        try
        {
            if (holes.Count == 0 && allowZeroFromMirror)
            {
                return new { inspection = "shelf_locator_hole_removal", strategy = "mirrored_from_source", removedHoleCount = 0, remainingHoleCount = 0, pass = true };
            }
            Need(holes.Count == expectedIfPresent, "CRAFT_SHELF_LOCATOR_COUNT_MISMATCH actual=" + holes.Count + " expected=" + expectedIfPresent);
            double volBefore = CraftSolidVolumeM3(model);
            List<Feature> beforeTop = CraftTopFeatures(model);
            var beforeNames = new HashSet<string>(beforeTop.Select(f => f.Name), StringComparer.Ordinal);
            ReleaseFeatureList(beforeTop);

            VariableTrial.ModelClearSelection2(model, true, "ApplyShelfLocatorHoleRemoval.Pre");
            for (int i = 0; i < holes.Count; i++)
                Need(((Entity)holes[i]).Select4(i > 0, null), "CRAFT_SHELF_LOCATOR_SELECT_FAILED idx=" + i);

            Need(model.InsertDeleteFace2((int)swFaceDeleteOption_e.swFaceDelete_Patch), "CRAFT_SHELF_LOCATOR_PATCH_FAILED");
            List<Feature> afterTop = CraftTopFeatures(model);
            Feature healed = afterTop.FirstOrDefault(f => !beforeNames.Contains(f.Name) && f.GetDefinition() is DeleteFaceFeatureData);
            Need(healed != null, "CRAFT_SHELF_DELETE_FACE_FEATURE_MISSING");
            healed.Name = "生产_取消已验证定位孔";
            ReleaseFeatureList(afterTop, healed);
            try
            {
                Need(healed.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null), "CRAFT_SHELF_HEAL_UNSUPPRESS_FAILED");
                CraftEnsureBeforeFlatPattern(model, "生产_取消已验证定位孔");
                Need(VariableTrial.ModelForceRebuild3(model, false, "ApplyShelfLocatorHoleRemoval.Rebuild"), "CRAFT_SHELF_HEAL_REBUILD_FAILED");
            }
            finally { Release(healed); }

            List<Face2> remain = CraftLocatorHoles(model, 1);
            int remainCount = remain.Count;
            foreach (Face2 f in remain) Release(f);
            Need(remainCount == 0, "CRAFT_SHELF_LOCATORS_REMAIN count=" + remainCount);
            double volAfter = CraftSolidVolumeM3(model);
            double addedMm3 = (volAfter - volBefore) * 1e9;
            double expectedMm3 = holes.Count * Math.PI * 1.0 * 1.0 * 0.8;
            Need(Math.Abs(addedMm3 - expectedMm3) < 0.05, "CRAFT_SHELF_HEAL_VOLUME_MISMATCH addedMm3=" + addedMm3 + " expectedMm3=" + expectedMm3);
            return new { inspection = "shelf_locator_hole_removal", strategy = "direct_patch_heal", removedHoleCount = holes.Count, remainingHoleCount = 0, addedVolumeMm3 = addedMm3, feature = "生产_取消已验证定位孔", pass = true };
        }
        finally
        {
            foreach (Face2 f in holes) Release(f);
        }
    }

    // ==================== Craft 4: Assembly Welding Locator Sketches ====================

    sealed class CraftMarkInstance
    {
        internal Component2 Component;
        internal ModelDoc2 Model;
        internal string Role, Path, Kind, Side;
        internal int Index;
        internal MathTransform Transform, Inverse;
        internal double[] TransformData, InverseData;
        internal List<Body2> Bodies;
    }

    sealed class CraftSegment
    {
        public string kind, basis;
        public int axis;
        public double[] a, b, center, mid;
        public double radius, sweep, length;
    }

    sealed class CraftPlane
    {
        internal int Axis, NormalSign;
        internal double Value;
        internal bool BoundaryRead;
        internal List<Face2> Faces = new List<Face2>();
        internal List<CraftSegment> Boundary = new List<CraftSegment>();
    }

    sealed class CraftContact
    {
        internal CraftMarkInstance Source, Target;
        internal CraftPlane Plane;
        internal double[] ExactBounds;
        internal double ProjectionOffset, SourcePlane;
        internal int SourceNormalSign;
        internal List<CraftSegment> Outline = new List<CraftSegment>();
        internal List<CraftSegment> MaterialOutline = new List<CraftSegment>();
    }

    sealed class CraftMarkPlan
    {
        internal CraftMarkInstance Target;
        internal CraftPlane Plane;
        internal string TargetPath;
        internal int PlaneAxis, PlaneNormalSign;
        internal double PlaneValue;
        internal string Family, FeatureName;
        internal List<CraftSegment> Expected = new List<CraftSegment>();
    }

    static double[] CraftApplyMatrix(double[] p, double[] t)
    {
        Need(p != null && p.Length >= 3 && t != null && t.Length >= 13, "CRAFT_MATRIX_INVALID");
        return new[]
        {
            (p[0] * t[0] + p[1] * t[3] + p[2] * t[6]) * t[12] + t[9],
            (p[0] * t[1] + p[1] * t[4] + p[2] * t[7]) * t[12] + t[10],
            (p[0] * t[2] + p[1] * t[5] + p[2] * t[8]) * t[12] + t[11]
        };
    }

    static double[] CraftApplyVector(double[] p, double[] t)
    {
        double[] o = CraftApplyMatrix(new double[3], t), e = CraftApplyMatrix(p, t);
        return new[] { e[0] - o[0], e[1] - o[1], e[2] - o[2] };
    }

    static double CraftDot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    static double CraftDist(double[] a, double[] b) { return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2])); }
    static bool CraftNearPt(double[] a, double[] b) { return a != null && b != null && CraftDist(a, b) <= CraftTol; }
    static double[] CraftLerp(double[] a, double[] b, double t) { return new[] { a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]), a[2] + t * (b[2] - a[2]) }; }
    static int[] CraftAxes(int normal) { return normal == 0 ? new[] { 1, 2 } : normal == 1 ? new[] { 0, 2 } : new[] { 0, 1 }; }
    static double CraftWrapAngle(double a) { double t = a % (2.0 * Math.PI); return t < 0 ? t + 2.0 * Math.PI : t; }

    static CraftSegment CraftMakeLine(double[] a, double[] b, int axis, string basis)
    {
        double len = CraftDist(a, b);
        Need(len > 1e-7 && Math.Abs(a[axis] - b[axis]) < CraftTol, "CRAFT_DEGENERATE_OR_OFFPLANE_LINE");
        return new CraftSegment { kind = "line", a = a, b = b, axis = axis, length = len, basis = basis };
    }

    static CraftSegment CraftMakeArc(double[] a, double[] b, double[] center, double[] mid, int axis, string basis)
    {
        int[] ax = CraftAxes(axis);
        double r = CraftDist(a, center);
        Need(r > 1e-7 && Math.Abs(CraftDist(b, center) - r) < CraftTol && Math.Abs(CraftDist(mid, center) - r) < CraftTol, "CRAFT_ARC_RADIUS_MISMATCH");
        double s0 = Math.Atan2(a[ax[1]] - center[ax[1]], a[ax[0]] - center[ax[0]]);
        double e0 = Math.Atan2(b[ax[1]] - center[ax[1]], b[ax[0]] - center[ax[0]]);
        double m0 = Math.Atan2(mid[ax[1]] - center[ax[1]], mid[ax[0]] - center[ax[0]]);
        double sweep = CraftNearPt(a, b) ? 2.0 * Math.PI : CraftWrapAngle(e0 - s0);
        if (!CraftNearPt(a, b) && CraftWrapAngle(m0 - s0) > sweep + 1e-6) sweep -= 2.0 * Math.PI;
        Need(Math.Abs(sweep) > 1e-7, "CRAFT_ARC_SWEEP_ZERO");
        return new CraftSegment { kind = "arc", a = a, b = b, center = center, mid = mid, axis = axis, radius = r, sweep = sweep, length = Math.Abs(sweep) * r, basis = basis };
    }

    static double[] CraftEvalSeg(CraftSegment seg, double t)
    {
        if (seg.kind == "line") return CraftLerp(seg.a, seg.b, t);
        int[] ax = CraftAxes(seg.axis);
        double s0 = Math.Atan2(seg.a[ax[1]] - seg.center[ax[1]], seg.a[ax[0]] - seg.center[ax[0]]);
        double[] pt = (double[])seg.center.Clone();
        pt[ax[0]] += seg.radius * Math.Cos(s0 + t * seg.sweep);
        pt[ax[1]] += seg.radius * Math.Sin(s0 + t * seg.sweep);
        return pt;
    }

    static double CraftParamSeg(CraftSegment seg, double[] pt)
    {
        if (seg.kind == "line")
        {
            double[] d = { seg.b[0] - seg.a[0], seg.b[1] - seg.a[1], seg.b[2] - seg.a[2] };
            double[] q = { pt[0] - seg.a[0], pt[1] - seg.a[1], pt[2] - seg.a[2] };
            return CraftDot(q, d) / CraftDot(d, d);
        }
        int[] ax = CraftAxes(seg.axis);
        double s0 = Math.Atan2(seg.a[ax[1]] - seg.center[ax[1]], seg.a[ax[0]] - seg.center[ax[0]]);
        double ang = Math.Atan2(pt[ax[1]] - seg.center[ax[1]], pt[ax[0]] - seg.center[ax[0]]);
        return (seg.sweep > 0 ? CraftWrapAngle(ang - s0) : -CraftWrapAngle(s0 - ang)) / seg.sweep;
    }

    static bool CraftContainsSeg(CraftSegment seg, double[] pt)
    {
        double t = CraftParamSeg(seg, pt);
        return t >= -1e-6 && t <= 1.0 + 1e-6 && CraftDist(CraftEvalSeg(seg, Math.Max(0.0, Math.Min(1.0, t))), pt) <= CraftTol;
    }

    static CraftSegment CraftTrimSeg(CraftSegment seg, double t0, double t1)
    {
        return seg.kind == "line"
            ? CraftMakeLine(CraftEvalSeg(seg, t0), CraftEvalSeg(seg, t1), seg.axis, seg.basis)
            : CraftMakeArc(CraftEvalSeg(seg, t0), CraftEvalSeg(seg, t1), seg.center, CraftEvalSeg(seg, 0.5 * (t0 + t1)), seg.axis, seg.basis);
    }

    static bool CraftSameSeg(CraftSegment a, CraftSegment b)
    {
        if (a.kind != b.kind || Math.Abs(a.length - b.length) > CraftTol) return false;
        bool ends = (CraftNearPt(a.a, b.a) && CraftNearPt(a.b, b.b)) || (CraftNearPt(a.a, b.b) && CraftNearPt(a.b, b.a));
        return ends && (a.kind == "line" || (CraftNearPt(a.center, b.center) && CraftNearPt(CraftEvalSeg(a, 0.5), CraftEvalSeg(b, 0.5)) && Math.Abs(a.radius - b.radius) <= CraftTol));
    }

    static List<CraftSegment> CraftUniqueSegs(IEnumerable<CraftSegment> segs)
    {
        var list = new List<CraftSegment>();
        foreach (CraftSegment s in segs) if (!list.Any(p => CraftSameSeg(p, s))) list.Add(s);
        return list;
    }

    static double[] CraftTransformedBounds(IEnumerable<Body2> bodies, CraftMarkInstance source, CraftMarkInstance target)
    {
        var pts = new List<double[]>();
        foreach (Body2 orig in bodies)
        {
            Body2 copy = orig.Copy() as Body2;
            try
            {
                Need(copy != null, "CRAFT_BODY_COPY_FAILED");
                if (source != null) Need(copy.ApplyTransform(source.Transform), "CRAFT_SOURCE_XFORM_FAILED");
                if (target != null) Need(copy.ApplyTransform(target.Inverse), "CRAFT_TARGET_XFORM_FAILED");
                for (int axis = 0; axis < 3; axis++)
                {
                    foreach (double sign in new[] { -1.0, 1.0 })
                    {
                        double x, y, z;
                        Need(copy.GetExtremePoint(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0, out x, out y, out z), "CRAFT_EXTREME_PT_FAILED");
                        pts.Add(new[] { x, y, z });
                    }
                }
            }
            finally { Release(copy); }
        }
        return Enumerable.Range(0, 6).Select(i => i < 3 ? pts.Min(p => p[i]) : pts.Max(p => p[i - 3])).ToArray();
    }

    static List<CraftSegment> CraftReadLoopSegs(Face2 face, bool outerOnly, int axis, Func<double[], double[]> mapPt)
    {
        var result = new List<CraftSegment>();
        object[] loops = Items(face.GetLoops());
        try
        {
            foreach (object loopObj in loops)
            {
                Loop2 loop = loopObj as Loop2;
                if (loop == null || (outerOnly && !loop.IsOuter())) continue;
                object[] edges = Items(loop.GetEdges());
                try
                {
                    foreach (object edgeObj in edges)
                    {
                        Edge edge = edgeObj as Edge;
                        Curve curve = edge == null ? null : edge.GetCurve() as Curve;
                        try
                        {
                            double[] pars = edge == null ? null : edge.GetCurveParams2() as double[];
                            if (curve == null || pars == null || pars.Length < 8) continue;
                            double[] a = mapPt(new[] { pars[0], pars[1], pars[2] });
                            double[] b = mapPt(new[] { pars[3], pars[4], pars[5] });
                            Need(Math.Abs(a[axis] - b[axis]) <= CraftTol, "CRAFT_LOOP_NOT_PLANAR");
                            if (curve.IsLine())
                            {
                                result.Add(CraftMakeLine(a, b, axis, "contact_outer_line"));
                            }
                            else if (curve.IsCircle())
                            {
                                double[] c = Numbers(curve.CircleParams);
                                double[] m = Numbers(curve.Evaluate2((pars[6] + pars[7]) / 2.0, 0));
                                result.Add(CraftMakeArc(a, b, mapPt(new[] { c[0], c[1], c[2] }), mapPt(new[] { m[0], m[1], m[2] }), axis, "contact_outer_arc"));
                            }
                            else
                            {
                                Need(false, "CRAFT_UNSUPPORTED_CURVE_TYPE " + curve.Identity());
                            }
                        }
                        finally { Release(curve); }
                    }
                }
                finally { foreach (object e in edges) Release(e); }
            }
        }
        finally { foreach (object l in loops) Release(l); }
        return result;
    }

    static List<CraftPlane> CraftPlanarGroups(CraftMarkInstance inst, int axis)
    {
        var result = new List<CraftPlane>();
        foreach (Body2 body in inst.Bodies)
        {
            object[] rawFaces = Items(body.GetFaces());
            foreach (object fObj in rawFaces)
            {
                Face2 f = fObj as Face2;
                Surface s = f == null ? null : f.GetSurface() as Surface;
                try
                {
                    if (s != null && s.IsPlane())
                    {
                        double[] n = Numbers(f.Normal), pl = Numbers(s.PlaneParams);
                        if (n.Length == 3 && pl.Length >= 6 && Math.Abs(n[axis]) >= 1.0 - 1e-7)
                        {
                            int sign = Math.Sign(n[axis]);
                            double val = pl[axis + 3];
                            CraftPlane grp = result.FirstOrDefault(p => p.NormalSign == sign && Math.Abs(p.Value - val) <= CraftMatTol);
                            if (grp == null) { grp = new CraftPlane { Axis = axis, NormalSign = sign, Value = val }; result.Add(grp); }
                            grp.Faces.Add(f);
                        }
                    }
                }
                finally { Release(s); }
            }
        }
        Need(result.Count > 0, "CRAFT_PLANAR_GROUPS_EMPTY " + inst.Role + " axis=" + axis);
        return result;
    }

    static void CraftEnsureBoundary(CraftPlane plane)
    {
        if (plane.BoundaryRead) return;
        foreach (Face2 f in plane.Faces) plane.Boundary.AddRange(CraftReadLoopSegs(f, false, plane.Axis, p => p));
        plane.BoundaryRead = true;
    }

    static double CraftMaterialDist(CraftPlane plane, double[] pt)
    {
        return plane.Faces.Min(f =>
        {
            double[] cl = Numbers(f.GetClosestPointOn(pt[0], pt[1], pt[2]));
            return cl.Length >= 3 ? CraftDist(pt, new[] { cl[0], cl[1], cl[2] }) : 1e9;
        });
    }

    static List<double[]> CraftSegIntersections(CraftSegment first, CraftSegment second)
    {
        var res = new List<double[]>();
        int[] ax = CraftAxes(first.axis);
        if (first.kind == "line" && second.kind == "line")
        {
            double ux = first.b[ax[0]] - first.a[ax[0]], uy = first.b[ax[1]] - first.a[ax[1]];
            double vx = second.b[ax[0]] - second.a[ax[0]], vy = second.b[ax[1]] - second.a[ax[1]];
            double dx = second.a[ax[0]] - first.a[ax[0]], dy = second.a[ax[1]] - first.a[ax[1]];
            double cross = ux * vy - uy * vx;
            if (Math.Abs(cross) > 1e-14) res.Add(CraftEvalSeg(first, (dx * vy - dy * vx) / cross));
            else res.AddRange(new[] { first.a, first.b, second.a, second.b });
        }
        else if (first.kind == "line" || second.kind == "line")
        {
            CraftSegment ln = first.kind == "line" ? first : second, arc = first.kind == "arc" ? first : second;
            double[] d = { ln.b[0] - ln.a[0], ln.b[1] - ln.a[1], ln.b[2] - ln.a[2] };
            double[] o = { ln.a[0] - arc.center[0], ln.a[1] - arc.center[1], ln.a[2] - arc.center[2] };
            double a = CraftDot(d, d), b = 2.0 * CraftDot(d, o), cc = CraftDot(o, o) - arc.radius * arc.radius;
            double disc = b * b - 4.0 * a * cc;
            if (disc >= -1e-18 && a > 1e-18)
                foreach (double sg in new[] { -1.0, 1.0 }) res.Add(CraftEvalSeg(ln, (-b + sg * Math.Sqrt(Math.Max(0.0, disc))) / (2.0 * a)));
        }
        else
        {
            double dist = CraftDist(first.center, second.center);
            if (dist <= 1e-10 && Math.Abs(first.radius - second.radius) <= CraftTol) res.AddRange(new[] { first.a, first.b, second.a, second.b });
            else if (dist > 1e-10 && dist <= first.radius + second.radius + CraftTol && dist >= Math.Abs(first.radius - second.radius) - CraftTol)
            {
                double along = (first.radius * first.radius - second.radius * second.radius + dist * dist) / (2.0 * dist);
                double h = Math.Sqrt(Math.Max(0.0, first.radius * first.radius - along * along));
                double dx = (second.center[ax[0]] - first.center[ax[0]]) / dist, dy = (second.center[ax[1]] - first.center[ax[1]]) / dist;
                foreach (double sg in new[] { -1.0, 1.0 })
                {
                    double[] p = (double[])first.center.Clone();
                    p[ax[0]] += along * dx - sg * h * dy;
                    p[ax[1]] += along * dy + sg * h * dx;
                    res.Add(p);
                }
            }
        }
        return res.Where(p => CraftContainsSeg(first, p) && CraftContainsSeg(second, p)).ToList();
    }

    static List<double> CraftSortedCuts(IEnumerable<double> vals)
    {
        var res = new List<double>();
        foreach (double v in vals.Where(x => x >= -1e-7 && x <= 1.0 + 1e-7).Select(x => Math.Max(0.0, Math.Min(1.0, x))).OrderBy(x => x))
            if (res.Count == 0 || v - res[res.Count - 1] > 1e-7) res.Add(v);
        return res;
    }

    static List<CraftSegment> CraftClipToMaterial(CraftSegment seg, CraftPlane plane)
    {
        CraftEnsureBoundary(plane);
        var cuts = new List<double> { 0.0, 1.0 };
        foreach (CraftSegment b in plane.Boundary)
            cuts.AddRange(CraftSegIntersections(seg, b).Select(p => CraftParamSeg(seg, p)));
        List<double> sorted = CraftSortedCuts(cuts);
        var res = new List<CraftSegment>();
        for (int i = 0; i + 1 < sorted.Count; i++)
        {
            double s = sorted[i], e = sorted[i + 1];
            if ((e - s) * seg.length <= 1e-7) continue;
            if (CraftMaterialDist(plane, CraftEvalSeg(seg, 0.5 * (s + e))) <= 2e-6)
                res.Add(CraftTrimSeg(seg, s, e));
        }
        return res;
    }

    static List<CraftContact> CraftComputeContacts(CraftMarkInstance source, CraftMarkInstance target, List<CraftPlane> planes)
    {
        var result = new List<CraftContact>();
        int axis = planes[0].Axis;
        bool isPartRib = source.Kind == "partition_rib" && target.Kind == "partition";
        bool isSideRib = source.Kind == "side_rib" && (target.Kind == "side" || target.Kind == "partition");
        double standOff = isPartRib ? CraftPartitionStandOff : isSideRib ? CraftSideStandOff : 0.0;
        double[] exactBounds = CraftTransformedBounds(source.Bodies, source, target);
        Func<double[], double[]> intoTarget = p => CraftApplyMatrix(CraftApplyMatrix(p, source.TransformData), target.InverseData);

        foreach (Body2 body in source.Bodies)
        {
            object[] rawFaces = Items(body.GetFaces());
            foreach (object fObj in rawFaces)
            {
                Face2 face = fObj as Face2;
                Surface s = face == null ? null : face.GetSurface() as Surface;
                try
                {
                    if (s == null || !s.IsPlane()) continue;
                    double[] n = Numbers(face.Normal);
                    double[] nInTarget = CraftApplyVector(CraftApplyVector(n, source.TransformData), target.InverseData);
                    if (Math.Abs(nInTarget[axis]) < 1.0 - 1e-7) continue;
                    double[] pl = Numbers(s.PlaneParams);
                    double[] pt = intoTarget(new[] { pl[3], pl[4], pl[5] });
                    CraftPlane plane = planes.FirstOrDefault(p =>
                        Math.Abs((p.Value - pt[axis]) * nInTarget[axis] - standOff) <= 2e-5 &&
                        p.Faces.All(tf => CraftDot(nInTarget, Numbers(tf.Normal)) < -1.0 + 1e-7));
                    if (plane == null) continue;
                    double projOffset = plane.Value - pt[axis];
                    List<CraftSegment> orig = CraftReadLoopSegs(face, true, axis, intoTarget);
                    Func<double[], double[]> projectPt = v => { double[] m = (double[])v.Clone(); m[axis] = plane.Value; return m; };
                    List<CraftSegment> outline = orig.Select(seg => seg.kind == "line"
                        ? CraftMakeLine(projectPt(seg.a), projectPt(seg.b), axis, seg.basis)
                        : CraftMakeArc(projectPt(seg.a), projectPt(seg.b), projectPt(seg.center), projectPt(seg.mid), axis, seg.basis)).ToList();
                    var clipped = outline.SelectMany(seg => CraftClipToMaterial(seg, plane)).ToList();
                    if (clipped.Count == 0) continue;

                    CraftContact ct = result.FirstOrDefault(x => x.Plane == plane);
                    if (ct == null)
                    {
                        ct = new CraftContact { Source = source, Target = target, Plane = plane, ProjectionOffset = projOffset, SourcePlane = pt[axis], SourceNormalSign = Math.Sign(nInTarget[axis]), ExactBounds = exactBounds };
                        result.Add(ct);
                    }
                    ct.Outline.AddRange(outline);
                    ct.MaterialOutline.AddRange(clipped);
                }
                finally { Release(s); }
            }
        }
        foreach (CraftContact ct in result)
        {
            ct.Outline = CraftUniqueSegs(ct.Outline);
            ct.MaterialOutline = CraftUniqueSegs(ct.MaterialOutline);
        }
        return result;
    }

    static List<CraftSegment> CraftCornerLs(CraftContact ct)
    {
        int nAxis = ct.Plane.Axis;
        int[] ax = CraftAxes(nAxis);
        double[] b = ct.ExactBounds;
        var res = new List<CraftSegment>();
        foreach (int f in new[] { 0, 1 })
            foreach (int s in new[] { 0, 1 })
            {
                double[] corner = new double[3];
                corner[nAxis] = ct.Plane.Value;
                corner[ax[0]] = b[ax[0] + 3 * f];
                corner[ax[1]] = b[ax[1] + 3 * s];
                foreach (int idx in new[] { 0, 1 })
                {
                    double[] end = (double[])corner.Clone();
                    end[ax[idx]] += ((idx == 0 ? f : s) == 0 ? -1 : 1) * CraftShortLeg;
                    res.Add(CraftMakeLine((double[])corner.Clone(), end, nAxis, "corner_3mm_L"));
                }
            }
        Need(res.Count == 8, "CRAFT_CORNER_L_COUNT_INVALID");
        return res;
    }

    static List<CraftSegment> CraftShelfEndLines(CraftContact ct)
    {
        int[] ax = CraftAxes(ct.Plane.Axis);
        int longAxis = ax.OrderByDescending(i => ct.ExactBounds[i + 3] - ct.ExactBounds[i]).First();
        int shortAxis = ax.Single(i => i != longAxis);
        var res = new List<CraftSegment>();
        foreach (double sideVal in new[] { ct.ExactBounds[shortAxis], ct.ExactBounds[shortAxis + 3] })
        {
            var outer = ct.Outline.Where(ln => ln.kind == "line" && Math.Abs(ln.a[shortAxis] - sideVal) <= 5e-5 && Math.Abs(ln.b[shortAxis] - sideVal) <= 5e-5 && Math.Abs(ln.a[longAxis] - ln.b[longAxis]) > 2.0 * CraftShortLeg).ToList();
            Need(outer.Count == 1, "CRAFT_SHELF_OUTER_LONG_EDGE_AMBIGUOUS " + ct.Source.Role + " count=" + outer.Count);
            CraftSegment edge = outer[0];
            List<CraftSegment> pieces = CraftClipToMaterial(edge, ct.Plane);
            Need(pieces.Count > 0, "CRAFT_SHELF_EDGE_OUTSIDE_MATERIAL");
            double low = pieces.SelectMany(ln => new[] { ln.a[longAxis], ln.b[longAxis] }).Min();
            double high = pieces.SelectMany(ln => new[] { ln.a[longAxis], ln.b[longAxis] }).Max();
            Need(high - low > 2.0 * CraftShortLeg, "CRAFT_SHELF_EDGE_TOO_SHORT");
            foreach (bool first in new[] { true, false })
            {
                double[] a = (double[])edge.a.Clone(), b = (double[])edge.a.Clone();
                a[longAxis] = first ? low : high - CraftShortLeg;
                b[longAxis] = first ? low + CraftShortLeg : high;
                res.Add(CraftMakeLine(a, b, ct.Plane.Axis, "shelf_outer_end_3mm"));
            }
        }
        Need(res.Count == 4, "CRAFT_SHELF_END_COUNT_INVALID");
        return res;
    }

    static List<CraftSegment> CraftSideEndLines(CraftContact ct)
    {
        int[] ax = CraftAxes(ct.Plane.Axis);
        int longAxis = ax.OrderByDescending(i => ct.ExactBounds[i + 3] - ct.ExactBounds[i]).First();
        double low = ct.ExactBounds[longAxis] + CraftSideEnd, high = ct.ExactBounds[longAxis + 3] - CraftSideEnd;
        Need(high > low + CraftTol, "CRAFT_SIDE_RIB_TOO_SHORT_FOR_50MM");
        var res = new List<CraftSegment>();
        foreach (CraftSegment orig in ct.MaterialOutline)
        {
            var cuts = new List<double> { 0.0, 1.0 };
            foreach (double boundary in new[] { low, high })
            {
                if (orig.kind == "line")
                {
                    double delta = orig.b[longAxis] - orig.a[longAxis];
                    if (Math.Abs(delta) > 1e-10) cuts.Add((boundary - orig.a[longAxis]) / delta);
                }
                else
                {
                    int other = ax.Single(i => i != longAxis);
                    double off = boundary - orig.center[longAxis];
                    if (Math.Abs(off) <= orig.radius + CraftTol)
                        foreach (double sg in new[] { -1.0, 1.0 })
                        {
                            double[] p = (double[])orig.center.Clone(); p[longAxis] = boundary;
                            p[other] += sg * Math.Sqrt(Math.Max(0.0, orig.radius * orig.radius - off * off));
                            if (CraftContainsSeg(orig, p)) cuts.Add(CraftParamSeg(orig, p));
                        }
                }
            }
            List<double> sorted = CraftSortedCuts(cuts);
            for (int i = 0; i + 1 < sorted.Count; i++)
            {
                double s = sorted[i], e = sorted[i + 1];
                if ((e - s) * orig.length <= 1e-7) continue;
                double ord = CraftEvalSeg(orig, 0.5 * (s + e))[longAxis];
                if (ord <= low + 1e-6 || ord >= high - 1e-6) res.Add(CraftTrimSeg(orig, s, e));
            }
        }
        Need(res.Count > 0, "CRAFT_SIDE_END_EMPTY");
        return CraftUniqueSegs(res);
    }

    static CraftMarkPlan CraftSideArrowPlan(CraftContact ct)
    {
        CraftMarkInstance src = ct.Source;
        double[] normalInTarget = new double[3]; normalInTarget[ct.Plane.Axis] = 1.0;
        double[] normal = CraftApplyVector(CraftApplyVector(normalInTarget, ct.Target.TransformData), src.InverseData);
        int axis = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(normal[i])).First();
        double[] contactPt = new double[3]; contactPt[ct.Plane.Axis] = ct.SourcePlane;
        contactPt = CraftApplyMatrix(CraftApplyMatrix(contactPt, ct.Target.TransformData), src.InverseData);
        double[] bounds = CraftTransformedBounds(src.Bodies, null, null);
        bool minimum = Math.Abs(contactPt[axis] - bounds[axis]) <= 2e-5;
        int visibleSign = minimum ? 1 : -1;
        List<CraftPlane> allPlanes = CraftPlanarGroups(src, axis);
        CraftPlane plane = allPlanes
            .Where(cand => cand.NormalSign == visibleSign && (cand.Value - contactPt[axis]) * visibleSign > 1e-5)
            .OrderByDescending(cand => cand.Faces.Sum(f => f.GetArea()))
            .FirstOrDefault();
        Need(plane != null, "CRAFT_ARROW_VISIBLE_FACE_MISSING");
        CraftEnsureBoundary(plane);

        double[] up = CraftApplyVector(new[] { 0.0, 1.0, 0.0 }, src.InverseData);
        int longAxis = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(up[i])).First();
        int shortAxis = CraftAxes(axis).Single(i => i != longAxis);
        double[] outward = new double[3]; outward[axis] = visibleSign;
        double[] right = { up[1] * outward[2] - up[2] * outward[1], up[2] * outward[0] - up[0] * outward[2], up[0] * outward[1] - up[1] * outward[0] };

        var bPts = plane.Boundary.SelectMany(s => new[] { s.a, s.b }).ToList();
        double[] faceBounds = Enumerable.Range(0, 6).Select(i => i < 3 ? bPts.Min(p => p[i]) : bPts.Max(p => p[i - 3])).ToArray();
        foreach (double vf in new[] { 0.75, 0.5, 0.25, 0.9, 0.1 })
        {
            foreach (double hf in new[] { 0.5, 0.25, 0.75 })
            {
                double[] anchor = new double[3]; anchor[axis] = plane.Value;
                anchor[longAxis] = faceBounds[longAxis] + vf * (faceBounds[longAxis + 3] - faceBounds[longAxis]);
                anchor[shortAxis] = faceBounds[shortAxis] + hf * (faceBounds[shortAxis + 3] - faceBounds[shortAxis]);
                Func<double, double, double[]> pt = (x, y) => new[] { anchor[0] + x * right[0] + y * up[0], anchor[1] + x * right[1] + y * up[1], anchor[2] + x * right[2] + y * up[2] };
                var lines = new List<CraftSegment>
                {
                    CraftMakeLine(pt(0, 0), pt(0, 0.012), axis, "arrow_stem"),
                    CraftMakeLine(pt(0, 0.012), pt(-0.003, 0.009), axis, "arrow_left"),
                    CraftMakeLine(pt(0, 0.012), pt(0.003, 0.009), axis, "arrow_right"),
                    CraftMakeLine(pt(0, -0.003), pt(0, -0.011), axis, "glyph_v"),
                    CraftMakeLine(pt(0, -0.007), pt(0.003, -0.007), axis, "glyph_top"),
                    CraftMakeLine(pt(-0.004, -0.011), pt(0.004, -0.011), axis, "glyph_bot")
                };
                if (lines.All(l => CraftClipToMaterial(l, plane).Sum(p => p.length) >= l.length - 1e-6))
                {
                    return new CraftMarkPlan
                    {
                        Target = src,
                        Plane = plane,
                        TargetPath = src.Path,
                        PlaneAxis = plane.Axis,
                        PlaneNormalSign = plane.NormalSign,
                        PlaneValue = plane.Value,
                        Family = "侧加强筋朝上箭头与上",
                        Expected = lines
                    };
                }
            }
        }
        Need(false, "CRAFT_ARROW_NO_SUPPORTED_LOCATION");
        return null;
    }

    static int CraftSideContactAxis(CraftMarkInstance src, CraftMarkInstance tgt)
    {
        var faces = new List<Face2>();
        foreach (Body2 b in src.Bodies)
        {
            foreach (object fObj in Items(b.GetFaces()))
            {
                Face2 f = fObj as Face2;
                Surface s = f == null ? null : f.GetSurface() as Surface;
                if (s != null && s.IsPlane()) faces.Add(f);
                Release(s);
            }
        }
        Face2 primary = faces.OrderByDescending(f => f.GetArea()).First();
        double[] n = CraftApplyVector(CraftApplyVector(Numbers(primary.Normal), src.TransformData), tgt.InverseData);
        return Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(n[i])).First();
    }

    static string CraftClassifyRole(Placement pl)
    {
        switch (pl.Kind)
        {
            case "panel": return "door";
            case "stiffener": return "door_rib";
            case "hinge_latch_bottom":
            case "hinge_latch_top":
            case "hook_pad": return "door_contact";
            case "shelf": return "shelf";
            case "shelf_stiffener": return "shelf_rib";
            case "side_panel": return "side";
            case "side_stiffener1": return "side_rib";
            case "vertical_partition": return "partition";
            case "partition_brace": return "partition_rib";
            default: return "";
        }
    }

    static void CraftAddPlan(List<CraftMarkPlan> plans, CraftContact ct, string family, List<CraftSegment> segs)
    {
        CraftMarkPlan p = plans.FirstOrDefault(x => x.Target == ct.Target && x.Family == family && x.Plane == ct.Plane);
        if (p == null)
        {
            p = new CraftMarkPlan
            {
                Target = ct.Target,
                Plane = ct.Plane,
                TargetPath = ct.Target.Path,
                PlaneAxis = ct.Plane.Axis,
                PlaneNormalSign = ct.Plane.NormalSign,
                PlaneValue = ct.Plane.Value,
                Family = family
            };
            plans.Add(p);
        }
        p.Expected.AddRange(segs);
    }

    static List<Placement> CraftSelectActivePlacements(BuildContext c)
    {
        var selected = new List<Placement>();
        var seenRoles = new HashSet<string>(StringComparer.Ordinal);
        Action<Placement> add = delegate(Placement p)
        {
            if (p != null && seenRoles.Add(p.Role)) selected.Add(p);
        };

        var seenDoorPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Placement door in c.Placements.Where(p => p.Kind == "panel"))
        {
            if (!seenDoorPaths.Add(Path.GetFullPath(door.Path))) continue;
            add(door);
            string prefix = "door_" + door.Side + "_" + door.Index.ToString("D2", Invariant) + "_";
            foreach (Placement s in c.Placements.Where(p => p.Role.StartsWith(prefix, StringComparison.Ordinal) &&
                (p.Kind == "stiffener" || p.Kind == "hinge_latch_bottom" || p.Kind == "hinge_latch_top" || p.Kind == "hook_pad")))
                add(s);
        }

        var seenShelfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Placement shelf in c.Placements.Where(p => p.Kind == "shelf"))
        {
            if (!seenShelfPaths.Add(Path.GetFullPath(shelf.Path))) continue;
            add(shelf);
            string prefix = "shelf_stiffener_" + shelf.Side + "_" + shelf.Index + "_";
            foreach (Placement s in c.Placements.Where(p => p.Kind == "shelf_stiffener" && p.Role.StartsWith(prefix, StringComparison.Ordinal)))
                add(s);
        }

        foreach (Placement sidePl in c.Placements.Where(p => p.Kind == "side_panel"))
        {
            add(sidePl);
            string prefix = sidePl.Role == "side_panel_left" ? "side_stiffener1_L_" : "side_stiffener1_R_";
            foreach (Placement s in c.Placements.Where(p => p.Kind == "side_stiffener1" && p.Role.StartsWith(prefix, StringComparison.Ordinal)))
                add(s);
        }

        foreach (Placement partPl in c.Placements.Where(p => p.Kind == "vertical_partition"))
        {
            add(partPl);
            string sidePrefix = partPl.Role == "partition_left" ? "partition_stiffener1_L_" : "partition_stiffener1_R_";
            foreach (Placement s in c.Placements.Where(p => (p.Kind == "side_stiffener1" && p.Role.StartsWith(sidePrefix, StringComparison.Ordinal)) || p.Kind == "partition_brace"))
                add(s);
        }

        return selected;
    }

    static ModelDoc2 CraftFindOpenDocByPath(ISldWorks sw, string fullPath)
    {
        foreach (object rawDoc in VariableTrial.SwGetDocuments(sw, "ApplyAssemblyProductionMarks.FindOpen", true))
        {
            ModelDoc2 dm = rawDoc as ModelDoc2;
            string p = dm == null ? null : VariableTrial.ModelGetPathName(dm, "ApplyAssemblyProductionMarks.DocPath", true);
            if (!string.IsNullOrEmpty(p) && string.Equals(Path.GetFullPath(p), fullPath, StringComparison.OrdinalIgnoreCase))
                return dm;
            Release(dm);
        }
        return null;
    }

    static string CraftFoldedConfigName(ModelDoc2 model)
    {
        string active = model.ConfigurationManager.ActiveConfiguration.Name;
        if (active.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) < 0) return active;
        string[] cfgs = Items(model.GetConfigurationNames()).Select(Convert.ToString).ToArray();
        string chosen = cfgs.FirstOrDefault(v => String.Equals(v, "Default", StringComparison.OrdinalIgnoreCase))
            ?? cfgs.FirstOrDefault(v => String.Equals(v, "默认", StringComparison.OrdinalIgnoreCase))
            ?? cfgs.FirstOrDefault(v => v.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) < 0);
        Need(!string.IsNullOrEmpty(chosen), "CRAFT_FOLDED_CONFIG_MISSING " + VariableTrial.ModelGetPathName(model, "CraftFoldedConfigName"));
        Need(VariableTrial.ModelShowConfiguration2(model, chosen, "CraftFoldedConfigName"), "CRAFT_SHOW_FOLDED_CONFIG_FAILED " + chosen);
        return chosen;
    }

    static Face2 CraftFindPartSupportFace(List<Face2> faces, int axis, int normalSign, double planeValue, double[] pointM)
    {
        Face2 best = null;
        double bestArea = -1.0;
        foreach (Face2 f in faces)
        {
            Surface s = f.GetSurface() as Surface;
            try
            {
                if (s == null || !s.IsPlane()) continue;
                double[] n = Numbers(f.Normal);
                double[] pl = Numbers(s.PlaneParams);
                if (n.Length != 3 || pl.Length < 6) continue;
                if (Math.Abs(n[axis]) < 1.0 - 1e-7 || Math.Sign(n[axis]) != normalSign) continue;
                if (Math.Abs(pl[axis + 3] - planeValue) > 2e-5) continue;
                double[] cl = Numbers(f.GetClosestPointOn(pointM[0], pointM[1], pointM[2]));
                if (cl.Length >= 3 && CraftDist(pointM, new[] { cl[0], cl[1], cl[2] }) <= 1e-5)
                {
                    double area = f.GetArea();
                    if (area > bestArea) { bestArea = area; best = f; }
                }
            }
            finally { Release(s); }
        }
        return best;
    }

    static object ApplyAssemblyProductionMarks(BuildContext c)
    {
        FullCloseAllDocuments(c);
        Need(VariableTrial.SwGetDocuments(c.Sw, "ApplyAssemblyProductionMarks.EmptyStart").Length == 0, "CRAFT_MARKS_REQUIRE_EMPTY_SESSION");

        List<Placement> activePlacements = CraftSelectActivePlacements(c);
        var bodiesByPath = new Dictionary<string, List<Body2>>(StringComparer.OrdinalIgnoreCase);
        var openedDocs = new List<ModelDoc2>();
        var instances = new List<CraftMarkInstance>();
        var uniquePlans = new List<CraftMarkPlan>();
        MathUtility mathA = VariableTrial.SwGetMathUtility(c.Sw, "ApplyAssemblyProductionMarks.PhaseA");

        // Phase A: Open only the active unique parts invisibly read-only to compute exact weld-mark coordinates in Part space
        VariableTrial.SwDocumentVisible(c.Sw, false, (int)swDocumentTypes_e.swDocPART, "ApplyAssemblyProductionMarks.HidePhaseA");
        try
        {
            foreach (string fullPath in activePlacements.Select(p => Path.GetFullPath(p.Path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                VariableTrial.RuntimeOwnership();
                ModelDoc2 m = CraftFindOpenDocByPath(c.Sw, fullPath) ?? c.Open(fullPath, true);
                openedDocs.Add(m);
                CraftFoldedConfigName(m);
                object[] rawBodies = Items(((PartDoc)m).GetBodies2((int)swBodyType_e.swSolidBody, false));
                var bodies = rawBodies.Cast<Body2>().ToList();
                Need(bodies.Count == 1, "CRAFT_MARK_REQUIRES_ONE_BODY " + fullPath);
                bodiesByPath[fullPath] = bodies;
            }

            foreach (Placement pl in activePlacements)
            {
                string kind = CraftClassifyRole(pl);
                if (string.IsNullOrEmpty(kind)) continue;
                string fullPath = Path.GetFullPath(pl.Path);
                MathTransform xform = mathA.CreateTransform(pl.Transform) as MathTransform;
                MathTransform inv = xform == null ? null : xform.IInverse();
                Need(xform != null && inv != null, "CRAFT_MARK_COMPONENT_XFORM_FAILED " + pl.Role);
                instances.Add(new CraftMarkInstance
                {
                    Component = null,
                    Model = null,
                    Role = pl.Role,
                    Path = fullPath,
                    Kind = kind,
                    Side = pl.Side ?? "",
                    Index = pl.Index,
                    Transform = xform,
                    Inverse = inv,
                    TransformData = Numbers(xform.ArrayData),
                    InverseData = Numbers(inv.ArrayData),
                    Bodies = bodiesByPath[fullPath]
                });
            }

            var plans = new List<CraftMarkPlan>();
            var planeCache = new Dictionary<string, List<CraftPlane>>(StringComparer.Ordinal);
            Func<CraftMarkInstance, int, List<CraftPlane>> getPlanes = delegate(CraftMarkInstance tgt, int axis)
            {
                string key = tgt.Role + "|" + axis;
                List<CraftPlane> plList;
                if (!planeCache.TryGetValue(key, out plList))
                {
                    plList = CraftPlanarGroups(tgt, axis);
                    planeCache[key] = plList;
                }
                return plList;
            };

            // 1. Unique door variants
            var seenDoorPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CraftMarkInstance door in instances.Where(i => i.Kind == "door"))
            {
                if (!seenDoorPaths.Add(door.Path)) continue;
                string prefix = "door_" + door.Side + "_" + door.Index.ToString("D2", Invariant) + "_";
                var srcs = instances.Where(s => s.Role.StartsWith(prefix, StringComparison.Ordinal) && (s.Kind == "door_rib" || s.Kind == "door_contact")).ToList();
                Need(srcs.Count == 4, "CRAFT_DOOR_WELD_SOURCE_COUNT actual=" + srcs.Count + " prefix=" + prefix);
                List<CraftPlane> planes = getPlanes(door, 2);
                foreach (CraftMarkInstance src in srcs)
                {
                    List<CraftContact> cts = CraftComputeContacts(src, door, planes);
                    Need(cts.Count == 1, "CRAFT_DOOR_CONTACT_AMBIGUOUS " + src.Role + " -> " + door.Role + " count=" + cts.Count);
                    if (src.Kind == "door_rib") CraftAddPlan(plans, cts[0], "门加强筋四角L", CraftCornerLs(cts[0]));
                    else CraftAddPlan(plans, cts[0], "锁扣与挂钩真实接触轮廓", cts[0].MaterialOutline);
                }
            }

            // 2. Shelves (L and R if present)
            var seenShelfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CraftMarkInstance shelf in instances.Where(i => i.Kind == "shelf"))
            {
                if (!seenShelfPaths.Add(shelf.Path)) continue;
                string prefix = "shelf_stiffener_" + shelf.Side + "_" + shelf.Index + "_";
                var srcs = instances.Where(s => s.Kind == "shelf_rib" && s.Role.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                Need(srcs.Count == 2, "CRAFT_SHELF_RIB_COUNT actual=" + srcs.Count + " prefix=" + prefix);
                List<CraftPlane> planes = getPlanes(shelf, 1);
                foreach (CraftMarkInstance src in srcs)
                {
                    List<CraftContact> cts = CraftComputeContacts(src, shelf, planes);
                    Need(cts.Count == 1, "CRAFT_SHELF_CONTACT_AMBIGUOUS " + src.Role + " -> " + shelf.Role + " count=" + cts.Count);
                    CraftAddPlan(plans, cts[0], "层板两筋外长边端线", CraftShelfEndLines(cts[0]));
                }
            }

            // 3. Side panels (left and right)
            bool arrowPlanned = false;
            foreach (CraftMarkInstance sideInst in instances.Where(i => i.Kind == "side"))
            {
                string prefix = sideInst.Role == "side_panel_left" ? "side_stiffener1_L_" : "side_stiffener1_R_";
                var srcs = instances.Where(s => s.Kind == "side_rib" && s.Role.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                Need(srcs.Count == 3, "CRAFT_SIDE_RIB_COUNT actual=" + srcs.Count + " prefix=" + prefix);
                foreach (CraftMarkInstance src in srcs)
                {
                    int cAxis = CraftSideContactAxis(src, sideInst);
                    List<CraftPlane> planes = getPlanes(sideInst, cAxis);
                    List<CraftContact> cts = CraftComputeContacts(src, sideInst, planes);
                    Need(cts.Count == 1, "CRAFT_SIDE_CONTACT_AMBIGUOUS " + src.Role + " -> " + sideInst.Role + " count=" + cts.Count);
                    CraftAddPlan(plans, cts[0], cAxis == 0 ? "侧加强筋首尾50mm_侧面X" : "侧加强筋首尾50mm_后折面Z", CraftSideEndLines(cts[0]));
                    if (!arrowPlanned)
                    {
                        CraftMarkPlan arrowPlan = CraftSideArrowPlan(cts[0]);
                        plans.Add(arrowPlan);
                        arrowPlanned = true;
                    }
                }
            }

            // 4. Vertical partitions (L and R)
            foreach (CraftMarkInstance partInst in instances.Where(i => i.Kind == "partition"))
            {
                string sidePrefix = partInst.Role == "partition_left" ? "partition_stiffener1_L_" : "partition_stiffener1_R_";
                var srcs = instances.Where(s => (s.Kind == "side_rib" && s.Role.StartsWith(sidePrefix, StringComparison.Ordinal)) || s.Kind == "partition_rib").ToList();
                Need(srcs.Count == 4, "CRAFT_PARTITION_RIB_COUNT actual=" + srcs.Count + " role=" + partInst.Role);
                List<CraftPlane> planes = getPlanes(partInst, 0);
                foreach (CraftMarkInstance src in srcs)
                {
                    List<CraftContact> cts = CraftComputeContacts(src, partInst, planes);
                    Need(cts.Count == 1, "CRAFT_PARTITION_CONTACT_AMBIGUOUS " + src.Role + " -> " + partInst.Role + " count=" + cts.Count);
                    if (src.Kind == "partition_rib") CraftAddPlan(plans, cts[0], "隔板加强件四角L", CraftCornerLs(cts[0]));
                    else CraftAddPlan(plans, cts[0], "侧加强筋首尾50mm_侧面X", CraftSideEndLines(cts[0]));
                }
            }

            foreach (CraftMarkPlan p in plans.OrderBy(x => x.TargetPath, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Family, StringComparer.Ordinal))
            {
                p.Expected = CraftUniqueSegs(p.Expected);
                Need(p.Expected.Count > 0 && p.Expected.All(s => s.kind == "line" || Math.Abs(s.sweep) < 2.0 * Math.PI - 1e-6), "CRAFT_INVALID_MARK_PLAN " + p.Family);
                p.FeatureName = CraftMarkPrefix + p.Family;
                p.Target = null;
                p.Plane = null;
                uniquePlans.Add(p);
            }
        }
        finally
        {
            foreach (CraftMarkInstance inst in instances)
            {
                Release(inst.Inverse);
                Release(inst.Transform);
            }
            foreach (List<Body2> bl in bodiesByPath.Values)
                foreach (Body2 b in bl) Release(b);
            foreach (ModelDoc2 dm in openedDocs) Release(dm);
            Release(mathA);
            ReclaimComWrappers();
            FullCloseAllDocuments(c);
            VariableTrial.SwDocumentVisible(c.Sw, true, (int)swDocumentTypes_e.swDocPART, "ApplyAssemblyProductionMarks.ShowPhaseB", true);
        }

        // Phase B: Open each target .SLDPRT standalone writable and write native surface sketches using PartDoc-owned faces
        var reportRows = new List<object>();
        foreach (var grp in uniquePlans.GroupBy(p => p.TargetPath, StringComparer.OrdinalIgnoreCase))
        {
            VariableTrial.RuntimeOwnership();
            string targetPath = grp.Key;
            ModelDoc2 doc = c.Open(targetPath, false);
            try
            {
                ActivatePartForEdit(c, doc);
                string origConfig = CraftFoldedConfigName(doc);
                string[] allCfgs = Items(doc.GetConfigurationNames()).Select(Convert.ToString).ToArray();

                int skCreated = 0;
                int segTotal = 0;
                foreach (CraftMarkPlan plan in grp)
                {
                    List<Feature> beforeTop = CraftTopFeatures(doc);
                    var beforeSet = new HashSet<string>(beforeTop.Select(f => f.Name), StringComparer.Ordinal);
                    ReleaseFeatureList(beforeTop);
                    Need(!beforeSet.Contains(plan.FeatureName), "CRAFT_MARK_FEATURE_ALREADY_EXISTS " + plan.FeatureName + " in " + targetPath);
                    Need(doc.GetActiveSketch2() == null, "CRAFT_ANOTHER_SKETCH_ACTIVE " + plan.FeatureName);

                    double[] midPt = CraftEvalSeg(plan.Expected[0], 0.5);
                    List<Face2> docFaces = CraftBodyFaces(doc);
                    try
                    {
                        Face2 supportFace = CraftFindPartSupportFace(docFaces, plan.PlaneAxis, plan.PlaneNormalSign, plan.PlaneValue, midPt);
                        Need(supportFace != null, "CRAFT_MARK_SUPPORT_FACE_MISSING " + plan.FeatureName + " in " + targetPath);

                        VariableTrial.ModelClearSelection2(doc, true, "ApplyAssemblyProductionMarks.SelFace");
                        Need(((Entity)supportFace).Select4(false, null), "CRAFT_MARK_FACE_SELECT_FAILED " + plan.FeatureName);

                        SketchManager mgr = doc.SketchManager;
                        bool prevDb = mgr.AddToDB, prevDisp = mgr.DisplayWhenAdded;
                        bool inSk = false;
                        try
                        {
                            mgr.InsertSketch(true);
                            inSk = true;
                            Sketch sk = mgr.ActiveSketch;
                            Need(sk != null && !sk.Is3D(), "CRAFT_MARK_SKETCH_NOT_ACTIVE " + plan.FeatureName);
                            MathTransform m2s = sk.ModelToSketchTransform;
                            double[] xformData = Numbers(m2s.ArrayData);
                            Release(m2s);
                            mgr.AddToDB = true;
                            mgr.DisplayWhenAdded = false;
                            foreach (CraftSegment seg in plan.Expected)
                            {
                                double[] pa = CraftApplyMatrix(seg.a, xformData);
                                double[] pb = CraftApplyMatrix(seg.b, xformData);
                                SketchSegment added;
                                if (seg.kind == "line")
                                {
                                    added = mgr.CreateLine(pa[0], pa[1], 0, pb[0], pb[1], 0);
                                }
                                else
                                {
                                    double[] pm = CraftApplyMatrix(CraftEvalSeg(seg, 0.5), xformData);
                                    added = mgr.Create3PointArc(pa[0], pa[1], 0, pb[0], pb[1], 0, pm[0], pm[1], 0);
                                }
                                Need(added != null, "CRAFT_MARK_SEGMENT_CREATE_FAILED " + plan.FeatureName);
                                added.ConstructionGeometry = false;
                                mgr.AddToDB = false;
                                CraftFixSegment(doc, added);
                                mgr.AddToDB = true;
                                Release(added);
                            }
                            mgr.AddToDB = prevDb;
                            mgr.DisplayWhenAdded = prevDisp;
                            mgr.InsertSketch(true);
                            inSk = false;
                        }
                        finally
                        {
                            if (inSk)
                            {
                                try { mgr.AddToDB = prevDb; mgr.DisplayWhenAdded = prevDisp; mgr.InsertSketch(true); }
                                catch { }
                            }
                        }
                    }
                    finally
                    {
                        foreach (Face2 f in docFaces) Release(f);
                    }

                    List<Feature> afterTop = CraftTopFeatures(doc);
                    Feature created = null;
                    foreach (Feature f in afterTop)
                    {
                        if (beforeSet.Contains(f.Name)) continue;
                        object spec = f.GetSpecificFeature2();
                        bool isSk = spec is Sketch;
                        Release(spec);
                        if (isSk) { created = f; break; }
                    }
                    Need(created != null, "CRAFT_MARK_SKETCH_CREATED_MISSING " + plan.FeatureName);
                    created.Name = plan.FeatureName;
                    ReleaseFeatureList(afterTop, created);
                    try
                    {
                        Need(created.Select2(false, 0), "CRAFT_MARK_UNBLANK_SEL_FAILED " + plan.FeatureName);
                        doc.UnblankSketch();
                        foreach (string cfgName in allCfgs)
                        {
                            bool wantSuppress = cfgName.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) >= 0;
                            int action = wantSuppress
                                ? (int)swFeatureSuppressionAction_e.swSuppressFeature
                                : (int)swFeatureSuppressionAction_e.swUnSuppressFeature;
                            Need(created.SetSuppression2(action, (int)swInConfigurationOpts_e.swSpecifyConfiguration, new[] { cfgName }),
                                "CRAFT_MARK_CONFIG_SUPPRESSION_FAILED " + plan.FeatureName + " cfg=" + cfgName);
                        }
                        VariableTrial.ModelClearSelection2(doc, true, "ApplyAssemblyProductionMarks.AfterCreate");
                    }
                    finally { Release(created); }

                    skCreated++;
                    segTotal += plan.Expected.Count;
                }

                if (doc.ConfigurationManager.ActiveConfiguration.Name != origConfig)
                    VariableTrial.ModelShowConfiguration2(doc, origConfig, "ApplyAssemblyProductionMarks.RestoreCfg");
                Need(VariableTrial.ModelForceRebuild3(doc, false, "ApplyAssemblyProductionMarks.Rebuild." + Path.GetFileName(targetPath)), "CRAFT_PART_MARK_REBUILD_FAILED " + targetPath);
                CheckFeatureHealth(doc, c, "production_marks_" + Path.GetFileName(targetPath), true);
                c.Save(doc);
                reportRows.Add(new { path = targetPath, file = Path.GetFileName(targetPath), sketchCount = skCreated, segmentCount = segTotal, families = grp.Select(g => g.Family).ToArray() });
            }
            finally
            {
                c.Close(doc);
                FullCloseAllDocuments(c);
            }
        }

        Need(reportRows.Count >= 7 && uniquePlans.Count >= 13, "CRAFT_PRODUCTION_MARKS_INCOMPLETE partFileCount=" + reportRows.Count + " sketchCount=" + uniquePlans.Count);
        c.Event(new
        {
            inspection = "variable_trial_production_marks",
            partFileCount = reportRows.Count,
            totalSketchCount = uniquePlans.Count,
            totalSegmentCount = uniquePlans.Sum(p => p.Expected.Count),
            parts = reportRows.ToArray()
        }, true);

        return new
        {
            inspection = "variable_trial_production_marks",
            partFileCount = reportRows.Count,
            totalSketchCount = uniquePlans.Count,
            totalSegmentCount = uniquePlans.Sum(p => p.Expected.Count),
            parts = reportRows.ToArray()
        };
    }
}
