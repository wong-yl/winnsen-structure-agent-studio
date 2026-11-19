using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwView = SolidWorks.Interop.sldworks.View;

internal static partial class TrialOutput
{
    const string ProdMarkPrefix = "生产打标_焊接定位_";

    internal sealed class ProdHole2D
    {
        internal double U, V, RadiusMm;
        internal int Axis;
        internal double[] Center3Mm;
    }

    internal sealed class ProdSeg2D
    {
        internal string Kind; // "LINE" or "ARC"
        internal string SourceSketchName;
        internal double[] A;  // [u, v] in mm
        internal double[] B;  // [u, v] in mm
        internal double[] Center; // [cu, cv] in mm (ARC only)
        internal double[] Mid;    // [mu, mv] in mm (ARC only)
        internal double RadiusMm;
    }

    internal sealed class ProdSketchGroup
    {
        internal int PlaneAxis;
        internal double PlaneValMm;
        internal string AxisLabel;
        internal List<string> SketchNames = new List<string>();
        internal List<ProdSeg2D> Segments = new List<ProdSeg2D>();
        internal List<ProdHole2D> FaceHoles = new List<ProdHole2D>();
        internal List<ProdSeg2D> FaceEdges = new List<ProdSeg2D>();
        internal double[] FaceBoundsUV; // [uMin, vMin, uMax, vMax]
    }

    internal sealed class ProdMarkPartCapture
    {
        internal string RelPath;
        internal bool IsDoorPanel;
        internal double[] FoldedBoundsMm; // [minX, minY, minZ, maxX, maxY, maxZ]
        internal List<ProdHole2D> PinHoles = new List<ProdHole2D>(); // R = 5.1 mm, axis = Y
        internal List<ProdHole2D> AllHoles = new List<ProdHole2D>();
        internal List<ProdSketchGroup> Groups = new List<ProdSketchGroup>();
    }

    static int[] ProdAxes(int normal)
    {
        return normal == 0 ? new[] { 1, 2 } : normal == 1 ? new[] { 0, 2 } : new[] { 0, 1 };
    }

    static double[] ProdProjectUV(double[] ptMm, int normalAxis)
    {
        int[] ax = ProdAxes(normalAxis);
        return new[] { ptMm[ax[0]], ptMm[ax[1]] };
    }

    static double[] ProdApplyXform(MathUtility math, double[] ptM, double[] m)
    {
        return new[]
        {
            (ptM[0] * m[0] + ptM[1] * m[3] + ptM[2] * m[6]) * m[12] + m[9],
            (ptM[0] * m[1] + ptM[1] * m[4] + ptM[2] * m[7]) * m[12] + m[10],
            (ptM[0] * m[2] + ptM[1] * m[5] + ptM[2] * m[8]) * m[12] + m[11]
        };
    }

    static double ProdWrap2Pi(double a)
    {
        double t = a % (2.0 * Math.PI);
        return t < 0 ? t + 2.0 * Math.PI : t;
    }

    static double ProdDist2(double[] a, double[] b)
    {
        return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
    }

    static ProdSeg2D ProdReadNativeSketchArc(SketchSegment native, SketchArc arc, int axis, double[] s2m)
    {
        SketchPoint pa = arc.GetStartPoint2() as SketchPoint;
        SketchPoint pb = arc.GetEndPoint2() as SketchPoint;
        SketchPoint pc = arc.GetCenterPoint2() as SketchPoint;
        try
        {
            Need(pa != null && pb != null && pc != null, "PROD_MARK_ARC_POINTS_MISSING");
            double[] am = ProdApplyXform(null, new[] { pa.X, pa.Y, pa.Z }, s2m).Select(v => v * 1000.0).ToArray();
            double[] bm = ProdApplyXform(null, new[] { pb.X, pb.Y, pb.Z }, s2m).Select(v => v * 1000.0).ToArray();
            double[] cm = ProdApplyXform(null, new[] { pc.X, pc.Y, pc.Z }, s2m).Select(v => v * 1000.0).ToArray();
            double radius = arc.GetRadius() * 1000.0;
            double length = native.GetLength() * 1000.0;
            Need(radius > 0 && length > 0 && length / radius < 2.0 * Math.PI - 1e-5, "PROD_MARK_ARC_LENGTH_INVALID");

            double[] a2 = ProdProjectUV(am, axis);
            double[] b2 = ProdProjectUV(bm, axis);
            double[] c2 = ProdProjectUV(cm, axis);
            double startAng = Math.Atan2(a2[1] - c2[1], a2[0] - c2[0]);

            var midCandidates = new List<double[]>();
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                double sweep = sign * length / radius;
                double[] endTest = { c2[0] + radius * Math.Cos(startAng + sweep), c2[1] + radius * Math.Sin(startAng + sweep) };
                if (ProdDist2(endTest, b2) <= 0.05)
                {
                    midCandidates.Add(new[] { c2[0] + radius * Math.Cos(startAng + 0.5 * sweep), c2[1] + radius * Math.Sin(startAng + 0.5 * sweep) });
                }
            }
            Need(midCandidates.Count >= 1, "PROD_MARK_ARC_SWEEP_NOT_RESOLVED");
            double[] chosenMid = midCandidates[0];
            if (midCandidates.Count > 1)
            {
                Curve curve = native.GetCurve() as Curve;
                try
                {
                    double u0 = 0, u1 = 0; bool closed = false, periodic = false;
                    if (curve != null && curve.GetEndParams(out u0, out u1, out closed, out periodic) && !closed)
                    {
                        double[] ev = Numbers(curve.Evaluate2(0.5 * (u0 + u1), 0));
                        if (ev.Length >= 3)
                        {
                            double[] evModelMm = ProdProjectUV(ev.Take(3).Select(v => v * 1000.0).ToArray(), axis);
                            double[] evMappedMm = ProdProjectUV(ProdApplyXform(null, ev.Take(3).ToArray(), s2m).Select(v => v * 1000.0).ToArray(), axis);
                            double bestD = double.MaxValue;
                            foreach (double[] mc in midCandidates)
                            {
                                double d = Math.Min(ProdDist2(mc, evModelMm), ProdDist2(mc, evMappedMm));
                                if (d < bestD) { bestD = d; chosenMid = mc; }
                            }
                        }
                    }
                }
                finally { Release(curve); }
            }
            return new ProdSeg2D { Kind = "ARC", A = a2, B = b2, Center = c2, Mid = chosenMid, RadiusMm = radius };
        }
        finally
        {
            Release(pc);
            Release(pb);
            Release(pa);
        }
    }

    internal static void SuppressProductionMarkSketchesInMemory(ModelDoc2 model, string context)
    {
        Feature cur = model.FirstFeature() as Feature;
        int guard = 0;
        while (cur != null && guard++ < 10000)
        {
            Feature next = cur.GetNextFeature() as Feature;
            try
            {
                string fname = cur.Name ?? "";
                if (fname.StartsWith(ProdMarkPrefix, StringComparison.Ordinal) && !cur.IsSuppressed())
                {
                    bool ok = cur.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null);
                    Need(ok && cur.IsSuppressed(), context + ".SUPPRESS_MARK_FAILED " + fname);
                }
            }
            finally { Release(cur); }
            cur = next;
        }
    }

    internal static ProdMarkPartCapture CaptureProductionMarksFromFoldedPart(ISldWorks sw, ModelDoc2 model, string relPath)
    {
        var sketchFeats = new List<Feature>();
        Feature cur = model.FirstFeature() as Feature;
        int guard = 0;
        while (cur != null && guard++ < 10000)
        {
            Feature next = cur.GetNextFeature() as Feature;
            try
            {
                string fname = cur.Name ?? "";
                string ftype = cur.GetTypeName2() ?? "";
                if (fname.StartsWith(ProdMarkPrefix, StringComparison.Ordinal) && ftype == "ProfileFeature" && !cur.IsSuppressed())
                {
                    sketchFeats.Add(cur);
                    cur = null;
                }
            }
            finally { Release(cur); }
            cur = next;
        }
        if (sketchFeats.Count == 0) return null;

        var cap = new ProdMarkPartCapture { RelPath = relPath };
        try
        {
            // 1. Read sketch segments in 3D model space (mm) and group by host plane
            foreach (Feature skFeat in sketchFeats)
            {
                Sketch sk = skFeat.GetSpecificFeature2() as Sketch;
                Need(sk != null && !sk.Is3D(), "PROD_MARK_EXPECTED_2D_SKETCH " + skFeat.Name);
                MathTransform m2s = sk.ModelToSketchTransform;
                MathTransform s2m = m2s == null ? null : m2s.IInverse();
                Need(s2m != null, "PROD_MARK_SKETCH_INVERSE_MISSING " + skFeat.Name);
                double[] s2mArr = Numbers(s2m.ArrayData);
                object[] rawSegs = Items(sk.GetSketchSegments());
                var rawLines = new List<Tuple<double[], double[]>>();
                var rawArcs = new List<Tuple<SketchSegment, SketchArc>>();
                try
                {
                    foreach (object obj in rawSegs)
                    {
                        SketchSegment seg = obj as SketchSegment;
                        if (seg == null || seg.ConstructionGeometry) continue;
                        SketchLine ln = seg as SketchLine;
                        if (ln != null)
                        {
                            SketchPoint sa = ln.GetStartPoint2() as SketchPoint;
                            SketchPoint sb = ln.GetEndPoint2() as SketchPoint;
                            try
                            {
                                Need(sa != null && sb != null, "PROD_MARK_LINE_POINTS_MISSING");
                                double[] am = ProdApplyXform(null, new[] { sa.X, sa.Y, sa.Z }, s2mArr).Select(v => v * 1000.0).ToArray();
                                double[] bm = ProdApplyXform(null, new[] { sb.X, sb.Y, sb.Z }, s2mArr).Select(v => v * 1000.0).ToArray();
                                rawLines.Add(Tuple.Create(am, bm));
                            }
                            finally { Release(sb); Release(sa); }
                        }
                        else
                        {
                            SketchArc arc = seg as SketchArc;
                            Need(arc != null && arc.IsCircle() != 1, "PROD_MARK_UNSUPPORTED_SEGMENT " + skFeat.Name);
                            rawArcs.Add(Tuple.Create(seg, arc));
                        }
                    }

                    // Determine constant axis across all endpoints of this sketch
                    var all3 = new List<double[]>();
                    foreach (var l in rawLines) { all3.Add(l.Item1); all3.Add(l.Item2); }
                    if (all3.Count == 0 && rawArcs.Count > 0)
                    {
                        SketchPoint pa = rawArcs[0].Item2.GetStartPoint2() as SketchPoint;
                        SketchPoint pb = rawArcs[0].Item2.GetEndPoint2() as SketchPoint;
                        try
                        {
                            all3.Add(ProdApplyXform(null, new[] { pa.X, pa.Y, pa.Z }, s2mArr).Select(v => v * 1000.0).ToArray());
                            all3.Add(ProdApplyXform(null, new[] { pb.X, pb.Y, pb.Z }, s2mArr).Select(v => v * 1000.0).ToArray());
                        }
                        finally { Release(pb); Release(pa); }
                    }
                    Need(all3.Count > 0, "PROD_MARK_SKETCH_EMPTY " + skFeat.Name);
                    int planeAxis = -1;
                    double bestSpan = double.MaxValue;
                    double planeValMm = 0.0;
                    for (int a = 0; a < 3; a++)
                    {
                        double span = all3.Max(p => p[a]) - all3.Min(p => p[a]);
                        if (span < bestSpan) { bestSpan = span; planeAxis = a; planeValMm = 0.5 * (all3.Max(p => p[a]) + all3.Min(p => p[a])); }
                    }
                    Need(planeAxis >= 0 && bestSpan < 0.05, "PROD_MARK_SKETCH_NOT_AXIS_ALIGNED " + skFeat.Name + " span=" + bestSpan);

                    ProdSketchGroup grp = cap.Groups.FirstOrDefault(g => g.PlaneAxis == planeAxis && Math.Abs(g.PlaneValMm - planeValMm) <= 0.1);
                    if (grp == null)
                    {
                        grp = new ProdSketchGroup { PlaneAxis = planeAxis, PlaneValMm = planeValMm, AxisLabel = planeAxis == 0 ? "X" : planeAxis == 1 ? "Y" : "Z" };
                        cap.Groups.Add(grp);
                    }
                    grp.SketchNames.Add(skFeat.Name);
                    foreach (var l in rawLines)
                        grp.Segments.Add(new ProdSeg2D { Kind = "LINE", SourceSketchName = skFeat.Name, A = ProdProjectUV(l.Item1, planeAxis), B = ProdProjectUV(l.Item2, planeAxis) });
                    foreach (var ar in rawArcs)
                    {
                        ProdSeg2D arcSeg = ProdReadNativeSketchArc(ar.Item1, ar.Item2, planeAxis, s2mArr);
                        arcSeg.SourceSketchName = skFeat.Name;
                        grp.Segments.Add(arcSeg);
                    }
                }
                finally
                {
                    foreach (object obj in rawSegs) Release(obj);
                    Release(s2m);
                    Release(m2s);
                    Release(sk);
                }
            }

            // 2. Capture folded body bounds, pin holes, all holes, and face edges for each plane group
            object[] bodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
            try
            {
                Need(bodies.Length == 1, "PROD_MARK_FOLDED_ONE_BODY_REQUIRED");
                Body2 body = (Body2)bodies[0];
                var box = new double[6];
                for (int ax = 0; ax < 3; ax++)
                {
                    double x, y, z;
                    var dir = new double[3]; dir[ax] = -1;
                    body.GetExtremePoint(dir[0], dir[1], dir[2], out x, out y, out z);
                    box[ax] = new[] { x, y, z }[ax] * 1000.0;
                    dir[ax] = 1;
                    body.GetExtremePoint(dir[0], dir[1], dir[2], out x, out y, out z);
                    box[ax + 3] = new[] { x, y, z }[ax] * 1000.0;
                }
                cap.FoldedBoundsMm = box;

                object[] faces = Items(body.GetFaces());
                try
                {
                    foreach (object fObj in faces)
                    {
                        Face2 face = fObj as Face2;
                        Surface surf = face == null ? null : face.GetSurface() as Surface;
                        try
                        {
                            if (surf == null) continue;
                            if (surf.IsCylinder())
                            {
                                double[] cp = Numbers(surf.CylinderParams);
                                if (cp.Length >= 7)
                                {
                                    double rMm = cp[6] * 1000.0;
                                    double[] centerMm = { cp[0] * 1000.0, cp[1] * 1000.0, cp[2] * 1000.0 };
                                    double[] axVec = { cp[3], cp[4], cp[5] };
                                    int hAxis = Math.Abs(axVec[0]) > 0.999 ? 0 : Math.Abs(axVec[1]) > 0.999 ? 1 : Math.Abs(axVec[2]) > 0.999 ? 2 : -1;
                                    if (hAxis >= 0 && rMm >= 0.4 && rMm <= 25.0)
                                    {
                                        // Only keep complete 360-degree sheet-metal holes (exclude 90-degree bend fillets and 180-degree notches)
                                        var vPts = new List<double>();
                                        object[] hEdges = Items(face.GetEdges());
                                        try
                                        {
                                            foreach (object he in hEdges)
                                            {
                                                Edge edge = he as Edge;
                                                double[] ep = edge == null ? null : edge.GetCurveParams2() as double[];
                                                if (ep != null && ep.Length >= 6)
                                                {
                                                    vPts.Add(ep[hAxis] * 1000.0);
                                                    vPts.Add(ep[hAxis + 3] * 1000.0);
                                                }
                                            }
                                        }
                                        finally { foreach (object he in hEdges) Release(he); }
                                        double axialSpanMm = vPts.Count > 0 ? (vPts.Max() - vPts.Min()) : 0.0;
                                        double fullCylAreaMm2 = 2.0 * Math.PI * rMm * axialSpanMm;
                                        double faceAreaMm2 = face.GetArea() * 1e6;
                                        if (axialSpanMm >= 0.2 && axialSpanMm <= 3.5 && Math.Abs(faceAreaMm2 - fullCylAreaMm2) <= 0.05 * fullCylAreaMm2)
                                        {
                                            centerMm[hAxis] = 0.5 * (vPts.Min() + vPts.Max());
                                            double[] uv = ProdProjectUV(centerMm, hAxis);
                                            var hole = new ProdHole2D { U = uv[0], V = uv[1], RadiusMm = rMm, Axis = hAxis, Center3Mm = centerMm };
                                            cap.AllHoles.Add(hole);
                                            if (hAxis == 1 && Math.Abs(rMm - 5.1) <= 0.05) cap.PinHoles.Add(hole);
                                        }
                                    }
                                }
                            }
                            else if (surf.IsPlane())
                            {
                                double[] n = Numbers(face.Normal);
                                double[] pl = Numbers(surf.PlaneParams);
                                if (n.Length == 3 && pl.Length >= 6)
                                {
                                    foreach (ProdSketchGroup grp in cap.Groups)
                                    {
                                        if (Math.Abs(n[grp.PlaneAxis]) >= 0.999 && Math.Abs(pl[grp.PlaneAxis + 3] * 1000.0 - grp.PlaneValMm) <= 1.2)
                                        {
                                            // Collect vertices/edges only on the exact side or opposite skin of the web
                                            object[] edges = Items(face.GetEdges());
                                            try
                                            {
                                                foreach (object eObj in edges)
                                                {
                                                    Edge edge = eObj as Edge;
                                                    Curve ec = edge == null ? null : edge.GetCurve() as Curve;
                                                    try
                                                    {
                                                        double[] ep = edge == null ? null : edge.GetCurveParams2() as double[];
                                                        if (ec == null || ep == null || ep.Length < 8) continue;
                                                        double[] a3 = { ep[0] * 1000.0, ep[1] * 1000.0, ep[2] * 1000.0 };
                                                        double[] b3 = { ep[3] * 1000.0, ep[4] * 1000.0, ep[5] * 1000.0 };
                                                        double[] a2 = ProdProjectUV(a3, grp.PlaneAxis);
                                                        double[] b2 = ProdProjectUV(b3, grp.PlaneAxis);
                                                        if (ec.IsLine() && ProdDist2(a2, b2) > 0.05)
                                                        {
                                                            grp.FaceEdges.Add(new ProdSeg2D { Kind = "LINE", A = a2, B = b2 });
                                                        }
                                                    }
                                                    finally { Release(ec); }
                                                }
                                            }
                                            finally { foreach (object e in edges) Release(e); }
                                        }
                                    }
                                }
                            }
                        }
                        finally { Release(surf); }
                    }
                }
                finally { foreach (object f in faces) Release(f); }

                cap.IsDoorPanel = cap.PinHoles.Count == 2 && Math.Abs((box[5] - box[2]) - 15.0) <= 0.3 && relPath.IndexOf("_panel", StringComparison.OrdinalIgnoreCase) >= 0;

                foreach (ProdSketchGroup grp in cap.Groups)
                {
                    // Populate FaceHoles belonging to this group's PlaneAxis & within the group's UV face bounds
                    var uvPts = grp.FaceEdges.SelectMany(e => new[] { e.A, e.B }).ToList();
                    if (uvPts.Count > 0)
                    {
                        double uMin = uvPts.Min(p => p[0]), uMax = uvPts.Max(p => p[0]);
                        double vMin = uvPts.Min(p => p[1]), vMax = uvPts.Max(p => p[1]);
                        grp.FaceBoundsUV = new[] { uMin, vMin, uMax, vMax };
                        foreach (ProdHole2D h in cap.AllHoles)
                        {
                            if (h.Axis == grp.PlaneAxis
                                && Math.Abs(h.Center3Mm[grp.PlaneAxis] - grp.PlaneValMm) <= 1.2
                                && h.U >= uMin - 0.1 && h.U <= uMax + 0.1
                                && h.V >= vMin - 0.1 && h.V <= vMax + 0.1)
                            {
                                if (!grp.FaceHoles.Any(x => Math.Abs(x.U - h.U) <= 0.05 && Math.Abs(x.V - h.V) <= 0.05 && Math.Abs(x.RadiusMm - h.RadiusMm) <= 0.02))
                                    grp.FaceHoles.Add(h);
                            }
                        }
                    }
                }
            }
            finally { foreach (object b in bodies) Release(b); }
            return cap;
        }
        finally { foreach (Feature f in sketchFeats) Release(f); }
    }

    sealed class Dxf2DCircle
    {
        internal double Cx, Cy, R;
    }
    sealed class Dxf2DLine
    {
        internal double Ax, Ay, Bx, By;
        internal double Len { get { return Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay)); } }
    }
    sealed class Rigid2D
    {
        internal bool Swap;
        internal int Su, Sv;
        internal double Tx, Ty;
        internal bool IsReflection { get { return Swap ? (Su * Sv > 0) : (Su * Sv < 0); } }
        internal double[] Map(double[] uv)
        {
            double u = uv[0], v = uv[1];
            return Swap ? new[] { Su * v + Tx, Sv * u + Ty } : new[] { Su * u + Tx, Sv * v + Ty };
        }
    }

    static void ExtractDxfFeatures(List<Pair> pairs, List<DxfEntity> entities, out List<Dxf2DLine> cutLines, out List<Dxf2DLine> bendLines, out List<Dxf2DCircle> cutCircles)
    {
        cutLines = new List<Dxf2DLine>();
        bendLines = new List<Dxf2DLine>();
        cutCircles = new List<Dxf2DCircle>();
        var cutArcs = new List<Tuple<double, double, double, double, double>>();
        foreach (DxfEntity e in entities)
        {
            string layer = Text(pairs, e, 8, "0");
            if (e.Type == "LINE")
            {
                var ln = new Dxf2DLine { Ax = Number(pairs, e, 10), Ay = Number(pairs, e, 20), Bx = Number(pairs, e, 11), By = Number(pairs, e, 21) };
                if (String.Equals(layer, "BEND", StringComparison.OrdinalIgnoreCase)) bendLines.Add(ln);
                else if (String.Equals(layer, "CUT", StringComparison.OrdinalIgnoreCase)) cutLines.Add(ln);
            }
            else if (e.Type == "CIRCLE" && String.Equals(layer, "CUT", StringComparison.OrdinalIgnoreCase))
            {
                cutCircles.Add(new Dxf2DCircle { Cx = Number(pairs, e, 10), Cy = Number(pairs, e, 20), R = Number(pairs, e, 40) });
            }
            else if (e.Type == "ARC" && String.Equals(layer, "CUT", StringComparison.OrdinalIgnoreCase))
            {
                double cx = Number(pairs, e, 10), cy = Number(pairs, e, 20), r = Number(pairs, e, 40);
                double a0 = Number(pairs, e, 50), a1 = Number(pairs, e, 51);
                double sweep = ((a1 - a0) % 360.0 + 360.0) % 360.0;
                cutArcs.Add(Tuple.Create(cx, cy, r, sweep, 0.0));
            }
        }
        // Reconstruct closed full-circle holes split into semicircular ARCs
        foreach (var grp in cutArcs.GroupBy(a => String.Format(Invariant, "{0:F2}|{1:F2}|{2:F2}", a.Item1, a.Item2, a.Item3)))
        {
            double totalSweep = grp.Sum(x => x.Item4);
            if (Math.Abs(totalSweep - 360.0) <= 0.5)
            {
                var first = grp.First();
                if (!cutCircles.Any(c => Math.Sqrt((c.Cx - first.Item1) * (c.Cx - first.Item1) + (c.Cy - first.Item2) * (c.Cy - first.Item2)) <= 0.05))
                    cutCircles.Add(new Dxf2DCircle { Cx = first.Item1, Cy = first.Item2, R = first.Item3 });
            }
        }
    }

    static IEnumerable<Rigid2D> All8Orientations()
    {
        foreach (bool swap in new[] { false, true })
            foreach (int su in new[] { -1, 1 })
                foreach (int sv in new[] { -1, 1 })
                    yield return new Rigid2D { Swap = swap, Su = su, Sv = sv };
    }

    static bool LineMatchesAnyDxfLine(double[] a, double[] b, List<Dxf2DLine> lines, double tol)
    {
        double len = ProdDist2(a, b);
        if (len < 0.05) return false;
        foreach (Dxf2DLine ln in lines)
        {
            if (Math.Abs(ln.Len - len) > tol) continue;
            double[] la = { ln.Ax, ln.Ay }, lb = { ln.Bx, ln.By };
            if ((ProdDist2(a, la) <= tol && ProdDist2(b, lb) <= tol) || (ProdDist2(a, lb) <= tol && ProdDist2(b, la) <= tol)) return true;
        }
        return false;
    }

    static Rigid2D SolveProdMarkTransform(ProdMarkPartCapture cap, ProdSketchGroup grp, List<Dxf2DLine> cutLines, List<Dxf2DLine> bendLines, List<Dxf2DCircle> cutCircles)
    {
        Need(grp.FaceBoundsUV != null && grp.FaceBoundsUV.Length == 4, "PROD_MARK_FACE_BOUNDS_MISSING " + cap.RelPath);
        double fuMin = grp.FaceBoundsUV[0], fvMin = grp.FaceBoundsUV[1], fuMax = grp.FaceBoundsUV[2], fvMax = grp.FaceBoundsUV[3];
        double fCx = 0.5 * (fuMin + fuMax), fCy = 0.5 * (fvMin + fvMax);

        // Path 1: Door panel via 4 central BEND rectangle + 0.5mm upper pin pre-bias disambiguation
        if (cap.IsDoorPanel && grp.PlaneAxis == 2)
        {
            double dMinW = Math.Min(fuMax - fuMin, fvMax - fvMin);
            var longBends = bendLines.Where(b => b.Len > 0.4 * dMinW).ToList();
            var vBends = longBends.Where(b => Math.Abs(b.Ax - b.Bx) < 0.1).ToList();
            var hBends = longBends.Where(b => Math.Abs(b.Ay - b.By) < 0.1).ToList();
            Need(vBends.Count >= 2 && hBends.Count >= 2, "PROD_MARK_DOOR_BEND_RECT_MISSING " + cap.RelPath);
            double dCx = 0.5 * (vBends.Min(b => b.Ax) + vBends.Max(b => b.Ax));
            double dCy = 0.5 * (hBends.Min(b => b.Ay) + hBends.Max(b => b.Ay));
            // Take the innermost pair of long BEND lines (forming the door web boundary) so return flanges do not inflate span.
            double vInnerLeft = vBends.Where(b => b.Ax < dCx).Max(b => b.Ax);
            double vInnerRight = vBends.Where(b => b.Ax > dCx).Min(b => b.Ax);
            double hInnerBot = hBends.Where(b => b.Ay < dCy).Max(b => b.Ay);
            double hInnerTop = hBends.Where(b => b.Ay > dCy).Min(b => b.Ay);
            double bSpanX = vInnerRight - vInnerLeft;
            double bSpanY = hInnerTop - hInnerBot;

            var dxfPins = cutCircles.Where(c => Math.Abs(c.R - 5.1) <= 0.1).ToList();
            Need(dxfPins.Count == 2 && cap.PinHoles.Count == 2, "PROD_MARK_DOOR_PIN_HOLES_REQUIRED " + cap.RelPath);
            ProdHole2D fBot = cap.PinHoles.OrderBy(p => p.Center3Mm[1]).First();
            ProdHole2D fTop = cap.PinHoles.OrderByDescending(p => p.Center3Mm[1]).First();
            double[] fPinBot = { fBot.Center3Mm[0], fBot.Center3Mm[1] };
            double[] fPinTop = { fTop.Center3Mm[0], fTop.Center3Mm[1] };

            var valid = new List<Tuple<double, Rigid2D>>();
            foreach (Rigid2D r in All8Orientations())
            {
                double[] mc = r.Map(new[] { fCx, fCy });
                r.Tx = dCx - mc[0]; r.Ty = dCy - mc[1];
                double spanU = r.Swap ? (fvMax - fvMin) : (fuMax - fuMin);
                double spanV = r.Swap ? (fuMax - fuMin) : (fvMax - fvMin);
                if (Math.Abs(spanU - bSpanX) > 4.0 || Math.Abs(spanV - bSpanY) > 4.0) continue;

                double[] mpTop = r.Map(fPinTop), mpBot = r.Map(fPinBot);
                // Which axis is the parallel hinge axis (the axis where X_folded maps)?
                int hingeDxfAxis = r.Swap ? 1 : 0;
                int heightDxfAxis = 1 - hingeDxfAxis;
                Func<Dxf2DCircle, double[]> dPt = c => new[] { c.Cx, c.Cy };
                // Pair dxfPins with mpTop/mpBot by height side
                Dxf2DCircle dp0 = dxfPins[0], dp1 = dxfPins[1];
                double[] p0 = dPt(dp0), p1 = dPt(dp1);
                bool p0IsTop = (mpTop[heightDxfAxis] - (heightDxfAxis == 0 ? dCx : dCy)) * (p0[heightDxfAxis] - (heightDxfAxis == 0 ? dCx : dCy)) > 0;
                Dxf2DCircle dpTop = p0IsTop ? dp0 : dp1, dpBot = p0IsTop ? dp1 : dp0;
                if ((mpBot[heightDxfAxis] - (heightDxfAxis == 0 ? dCx : dCy)) * (dPt(dpBot)[heightDxfAxis] - (heightDxfAxis == 0 ? dCx : dCy)) <= 0) continue;

                double errTop = Math.Abs(mpTop[hingeDxfAxis] - dPt(dpTop)[hingeDxfAxis]);
                double errBot = Math.Abs(mpBot[hingeDxfAxis] - dPt(dpBot)[hingeDxfAxis]);
                double totalErr = errTop + errBot;
                if (totalErr <= 0.15) valid.Add(Tuple.Create(totalErr, r));
            }
            Need(valid.Count == 1, "PROD_MARK_DOOR_TRANSFORM_NOT_UNIQUE matches=" + valid.Count + " " + cap.RelPath);
            return valid[0].Item2;
        }

        // Path 2: Parts with >= 2 in-plane round holes (side X, side Z, partition, side_rib)
        if (grp.FaceHoles.Count >= 2)
        {
            ProdHole2D hAnchor = grp.FaceHoles
                .OrderBy(h => grp.FaceHoles.Count(x => Math.Abs(x.RadiusMm - h.RadiusMm) <= 0.05))
                .First();
            var candidates = new List<Tuple<int, double, Rigid2D>>();
            foreach (Rigid2D orient in All8Orientations())
            {
                foreach (Dxf2DCircle dc in cutCircles.Where(c => Math.Abs(c.R - hAnchor.RadiusMm) <= 0.08))
                {
                    var cand = new Rigid2D { Swap = orient.Swap, Su = orient.Su, Sv = orient.Sv };
                    double[] mp0 = cand.Map(new[] { hAnchor.U, hAnchor.V });
                    cand.Tx = dc.Cx - mp0[0];
                    cand.Ty = dc.Cy - mp0[1];

                    int holeMatches = 0;
                    double errSum = 0.0;
                    foreach (ProdHole2D fh in grp.FaceHoles)
                    {
                        double[] mp = cand.Map(new[] { fh.U, fh.V });
                        Dxf2DCircle best = cutCircles
                            .Where(c => Math.Abs(c.R - fh.RadiusMm) <= 0.08)
                            .OrderBy(c => Math.Sqrt((c.Cx - mp[0]) * (c.Cx - mp[0]) + (c.Cy - mp[1]) * (c.Cy - mp[1])))
                            .FirstOrDefault();
                        if (best != null)
                        {
                            double d = Math.Sqrt((best.Cx - mp[0]) * (best.Cx - mp[0]) + (best.Cy - mp[1]) * (best.Cy - mp[1]));
                            if (d <= 0.15) { holeMatches++; errSum += d; }
                        }
                    }
                    if (holeMatches == grp.FaceHoles.Count)
                    {
                        bool dup = candidates.Any(x =>
                            x.Item3.Swap == cand.Swap && x.Item3.Su == cand.Su && x.Item3.Sv == cand.Sv
                            && Math.Abs(x.Item3.Tx - cand.Tx) <= 0.05 && Math.Abs(x.Item3.Ty - cand.Ty) <= 0.05);
                        if (!dup) candidates.Add(Tuple.Create(holeMatches, errSum, cand));
                    }
                }
            }
            if (candidates.Count > 1)
            {
                var proper = candidates.Where(x => !x.Item3.IsReflection).ToList();
                if (proper.Count == 1) return proper[0].Item3;
            }
            Need(candidates.Count == 1, "PROD_MARK_HOLES_TRANSFORM_NOT_UNIQUE matches=" + candidates.Count + " " + cap.RelPath + " axis=" + grp.AxisLabel);
            return candidates[0].Item3;
        }

        // Path 3: Shelf web face (axis=1) via outer bend box + flange Ø3.5 holes + non-round cutout edges
        {
            Need(bendLines.Count >= 4, "PROD_MARK_SHELF_BENDS_MISSING " + cap.RelPath);
            double bMinX = bendLines.Min(b => Math.Min(b.Ax, b.Bx)), bMaxX = bendLines.Max(b => Math.Max(b.Ax, b.Bx));
            double bMinY = bendLines.Min(b => Math.Min(b.Ay, b.By)), bMaxY = bendLines.Max(b => Math.Max(b.Ay, b.By));
            double dCx = 0.5 * (bMinX + bMaxX), dCy = 0.5 * (bMinY + bMaxY);
            double bSpanX = bMaxX - bMinX, bSpanY = bMaxY - bMinY;

            var fFlangeHoles = cap.AllHoles.Where(h => Math.Abs(h.RadiusMm - 1.75) <= 0.05).ToList();
            var dFlangeHoles = cutCircles.Where(c => Math.Abs(c.R - 1.75) <= 0.08).ToList();

            var scored = new List<Tuple<int, double, Rigid2D>>();
            foreach (Rigid2D r in All8Orientations())
            {
                double[] mc = r.Map(new[] { fCx, fCy });
                r.Tx = dCx - mc[0]; r.Ty = dCy - mc[1];
                double spanU = r.Swap ? (fvMax - fvMin) : (fuMax - fuMin);
                double spanV = r.Swap ? (fuMax - fuMin) : (fvMax - fvMin);
                if (Math.Abs(spanU - bSpanX) > 6.0 || Math.Abs(spanV - bSpanY) > 6.0) continue;

                int edgeHits = 0;
                foreach (ProdSeg2D fe in grp.FaceEdges)
                {
                    if (fe.Kind != "LINE") continue;
                    double[] ma = r.Map(fe.A), mb = r.Map(fe.B);
                    if (LineMatchesAnyDxfLine(ma, mb, cutLines, 0.25)) edgeHits++;
                }

                // Compare 3x Ø3.5 flange holes: exact position along bend axis (<= 2.5mm), unfolded outward along bend-crossing axis (<= 35mm)
                double flangeErr = 0.0;
                int flangeOk = 0;
                if (fFlangeHoles.Count > 0 && dFlangeHoles.Count == fFlangeHoles.Count)
                {
                    int flangeOutAxis = (dFlangeHoles.Max(c => c.Cx) - dFlangeHoles.Min(c => c.Cx) < dFlangeHoles.Max(c => c.Cy) - dFlangeHoles.Min(c => c.Cy)) ? 0 : 1;
                    int flangeParAxis = 1 - flangeOutAxis;
                    double[] dCenter = { dCx, dCy };
                    foreach (ProdHole2D fh in fFlangeHoles)
                    {
                        double[] fhUV = ProdProjectUV(fh.Center3Mm, grp.PlaneAxis);
                        double[] mp = r.Map(fhUV);
                        Dxf2DCircle best = dFlangeHoles
                            .Where(c =>
                            {
                                double[] cp = { c.Cx, c.Cy };
                                return Math.Sign(cp[flangeOutAxis] - dCenter[flangeOutAxis]) == Math.Sign(mp[flangeOutAxis] - dCenter[flangeOutAxis])
                                    && Math.Abs(cp[flangeParAxis] - mp[flangeParAxis]) <= 2.5
                                    && Math.Abs(cp[flangeOutAxis] - mp[flangeOutAxis]) <= 35.0;
                            })
                            .OrderBy(c =>
                            {
                                double[] cp = { c.Cx, c.Cy };
                                return Math.Abs(cp[flangeParAxis] - mp[flangeParAxis]) + 0.1 * Math.Abs(cp[flangeOutAxis] - mp[flangeOutAxis]);
                            })
                            .FirstOrDefault();
                        if (best != null)
                        {
                            double[] cp = { best.Cx, best.Cy };
                            flangeOk++;
                            flangeErr += Math.Abs(cp[flangeParAxis] - mp[flangeParAxis]);
                        }
                    }
                }
                if (flangeOk == fFlangeHoles.Count)
                    scored.Add(Tuple.Create(edgeHits * 10 + flangeOk, flangeErr, r));
            }
            Need(scored.Count > 0, "PROD_MARK_SHELF_NO_TRANSFORM_CANDIDATE " + cap.RelPath);
            int maxScore = scored.Max(x => x.Item1);
            var top = scored.Where(x => x.Item1 == maxScore).OrderBy(x => x.Item2).ToList();
            Need(top.Count == 1 || top[0].Item2 + 0.5 < top[1].Item2, "PROD_MARK_SHELF_TRANSFORM_AMBIGUOUS " + cap.RelPath);
            return top[0].Item3;
        }
    }

    internal static int AppendProductionMarksToDxf(List<Pair> allPairs, List<DxfEntity> allEntities, ProdMarkPartCapture capture)
    {
        if (capture == null || capture.Groups.Count == 0) return 0;

        List<Dxf2DLine> cutLines, bendLines;
        List<Dxf2DCircle> cutCircles;
        ExtractDxfFeatures(allPairs, allEntities, out cutLines, out bendLines, out cutCircles);

        // Find ENTITIES section ENDSEC index and owner handle (330) + next free handle (5)
        int entitiesEndSec = -1;
        bool inEntities = false;
        string ownerHandle = "1F";
        for (int i = 0; i + 1 < allPairs.Count; i++)
        {
            if (allPairs[i].Code == 0 && allPairs[i].Value.Trim() == "SECTION" && allPairs[i + 1].Code == 2 && allPairs[i + 1].Value.Trim() == "ENTITIES")
            { inEntities = true; continue; }
            if (inEntities && allPairs[i].Code == 0 && allPairs[i].Value.Trim() == "ENDSEC")
            { entitiesEndSec = i; break; }
            if (inEntities && allPairs[i].Code == 330 && !String.IsNullOrWhiteSpace(allPairs[i].Value))
                ownerHandle = allPairs[i].Value.Trim();
        }
        Need(entitiesEndSec > 0, "PROD_MARK_ENTITIES_ENDSEC_MISSING");

        long nextHandle = 1;
        foreach (Pair p in allPairs)
        {
            if (p.Code == 5)
            {
                long val;
                if (Int64.TryParse(p.Value.Trim(), NumberStyles.HexNumber, Invariant, out val))
                    nextHandle = Math.Max(nextHandle, val + 1);
            }
        }

        // Collect existing entity keys so we never emit duplicate geometry on MARK
        var existingKeys = new HashSet<string>(allEntities.Select(e => e.Key), StringComparer.Ordinal);
        var insertPairs = new List<Pair>();
        int appendedCount = 0;

        foreach (ProdSketchGroup grp in capture.Groups)
        {
            Rigid2D xform = SolveProdMarkTransform(capture, grp, cutLines, bendLines, cutCircles);
            foreach (ProdSeg2D seg in grp.Segments)
            {
                double[] a = xform.Map(seg.A);
                double[] b = xform.Map(seg.B);
                if (seg.Kind == "LINE")
                {
                    if (ProdDist2(a, b) <= 1e-4) continue;
                    string ka = String.Format(Invariant, "{0},{1},0", Math.Round(a[0], 6), Math.Round(a[1], 6));
                    string kb = String.Format(Invariant, "{0},{1},0", Math.Round(b[0], 6), Math.Round(b[1], 6));
                    string key = "LINE|" + (String.CompareOrdinal(ka, kb) <= 0 ? ka + "|" + kb : kb + "|" + ka);
                    if (!existingKeys.Add(key)) continue;

                    insertPairs.Add(new Pair(0, "LINE"));
                    insertPairs.Add(new Pair(5, (nextHandle++).ToString("X", Invariant)));
                    insertPairs.Add(new Pair(330, ownerHandle));
                    insertPairs.Add(new Pair(100, "AcDbEntity"));
                    insertPairs.Add(new Pair(8, "MARK"));
                    insertPairs.Add(new Pair(6, "Continuous"));
                    insertPairs.Add(new Pair(62, "     5"));
                    insertPairs.Add(new Pair(100, "AcDbLine"));
                    insertPairs.Add(new Pair(10, Math.Round(a[0], 6).ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(20, Math.Round(a[1], 6).ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(30, "0.0"));
                    insertPairs.Add(new Pair(11, Math.Round(b[0], 6).ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(21, Math.Round(b[1], 6).ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(31, "0.0"));
                    appendedCount++;
                }
                else
                {
                    double[] c = xform.Map(seg.Center);
                    double[] m = xform.Map(seg.Mid);
                    double angA = ProdWrap2Pi(Math.Atan2(a[1] - c[1], a[0] - c[0]));
                    double angB = ProdWrap2Pi(Math.Atan2(b[1] - c[1], b[0] - c[0]));
                    double angM = ProdWrap2Pi(Math.Atan2(m[1] - c[1], m[0] - c[0]));
                    double sweepAB = ProdWrap2Pi(angB - angA);
                    bool ccwFromA = ProdWrap2Pi(angM - angA) <= sweepAB + 1e-4;
                    double startDeg = (ccwFromA ? angA : angB) * 180.0 / Math.PI;
                    double endDeg = (ccwFromA ? angB : angA) * 180.0 / Math.PI;
                    double r = Math.Round(seg.RadiusMm, 6);
                    string sDegStr = Math.Round(startDeg, 6).ToString("0.######", Invariant);
                    string eDegStr = Math.Round(endDeg, 6).ToString("0.######", Invariant);
                    string key = String.Format(Invariant, "ARC|10:{0}|20:{1}|30:0|40:{2}|50:{3}|51:{4}",
                        Math.Round(c[0], 6), Math.Round(c[1], 6), r, sDegStr, eDegStr);
                    if (!existingKeys.Add(key)) continue;

                    insertPairs.Add(new Pair(0, "ARC"));
                    insertPairs.Add(new Pair(5, (nextHandle++).ToString("X", Invariant)));
                    insertPairs.Add(new Pair(330, ownerHandle));
                    insertPairs.Add(new Pair(100, "AcDbEntity"));
                    insertPairs.Add(new Pair(8, "MARK"));
                    insertPairs.Add(new Pair(6, "Continuous"));
                    insertPairs.Add(new Pair(62, "     5"));
                    insertPairs.Add(new Pair(100, "AcDbCircle"));
                    insertPairs.Add(new Pair(10, Math.Round(c[0], 6).ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(20, Math.Round(c[1], 6).ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(30, "0.0"));
                    insertPairs.Add(new Pair(40, r.ToString("0.######", Invariant)));
                    insertPairs.Add(new Pair(100, "AcDbArc"));
                    insertPairs.Add(new Pair(50, sDegStr));
                    insertPairs.Add(new Pair(51, eDegStr));
                    appendedCount++;
                }
            }
        }

        if (insertPairs.Count > 0)
            allPairs.InsertRange(entitiesEndSec, insertPairs);
        return appendedCount;
    }

    static string FormatMmShort(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", Invariant);
    }

    static string PickModelViewName(ModelDoc2 model, params string[] preferredNames)
    {
        string[] available = Items(model.GetModelViewNames()).Select(Convert.ToString).ToArray();
        foreach (string candidate in preferredNames)
        {
            string match = available.FirstOrDefault(v => String.Equals(v, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return null;
    }

    static void SetProductionMarkSketchesThisConfig(ModelDoc2 model, bool suppress, string context)
    {
        int action = suppress
            ? (int)swFeatureSuppressionAction_e.swSuppressFeature
            : (int)swFeatureSuppressionAction_e.swUnSuppressFeature;
        Feature cur = model.FirstFeature() as Feature;
        int guard = 0;
        while (cur != null && guard++ < 10000)
        {
            Feature next = cur.GetNextFeature() as Feature;
            try
            {
                string fname = cur.Name ?? "";
                if (fname.StartsWith(ProdMarkPrefix, StringComparison.Ordinal) && cur.IsSuppressed() != suppress)
                {
                    bool ok = cur.SetSuppression2(action, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    Need(ok && cur.IsSuppressed() == suppress, context + ".MARK_SUPPRESSION_FAILED " + fname);
                }
            }
            finally { Release(cur); }
            cur = next;
        }
    }

    static double[] FitViewInBox(SwView view, double cx, double cy, double maxW, double maxH, double? forcedScale = null)
    {
        VariableTrial.RuntimeOwnership();
        view.UseSheetScale = 0;
        view.ScaleDecimal = 1.0;
        double[] box = Numbers(view.GetOutline());
        Need(box.Length == 4 && box[2] > box[0] && box[3] > box[1], "NATIVE_VIEW_OUTLINE_REQUIRED");
        double targetScale = forcedScale.HasValue
            ? forcedScale.Value
            : Math.Min(1.0, Math.Min(maxW / (box[2] - box[0]), maxH / (box[3] - box[1])));
        Need(targetScale > 1e-6, "INVALID_VIEW_FIT_SCALE");
        view.ScaleDecimal = targetScale;
        box = Numbers(view.GetOutline());
        double[] position = Numbers(view.Position);
        view.Position = new[]
        {
            position[0] + cx - 0.5 * (box[0] + box[2]),
            position[1] + cy - 0.5 * (box[1] + box[3])
        };
        return Numbers(view.GetOutline());
    }

    static void ResetSheetScaleOneToOnePreservingViews(ISldWorks sw, ModelDoc2 drawing, SwView[] views)
    {
        ActivateOutputDrawing(sw, drawing);
        DrawingDoc document = (DrawingDoc)drawing;
        document.EditSheet();
        Need(document.ActivateView(""), "SHEET_SCALE_RESET_CONTEXT_REQUIRED");
        double[] scales = views.Select(v => v.ScaleDecimal).ToArray();
        double[][] positions = views.Select(v => Numbers(v.Position)).ToArray();
        foreach (SwView v in views) v.UseSheetScale = 0;
        Sheet sheet = document.GetCurrentSheet() as Sheet;
        try
        {
            Need(sheet != null, "SHEET_SCALE_RESET_SHEET_REQUIRED");
            Need(sheet.SetScale(1, 1, false, false), "SHEET_SCALE_ONE_TO_ONE_FAILED");
        }
        finally { Release(sheet); }
        Need(VariableTrial.ModelForceRebuild3(drawing, false, "ResetSheetScaleOneToOne.Sheet"), "SHEET_SCALE_REBUILD_FAILED");
        for (int i = 0; i < views.Length; i++)
        {
            views[i].UseSheetScale = 0;
            views[i].ScaleDecimal = scales[i];
            views[i].Position = positions[i];
        }
        Need(VariableTrial.ModelForceRebuild3(drawing, false, "ResetSheetScaleOneToOne.Views"), "SHEET_VIEW_RESTORE_REBUILD_FAILED");
    }

    static double[] GetSheetToSketchInverseMatrix(DrawingDoc document)
    {
        SwView sheetView = document.GetFirstView() as SwView;
        MathTransform xform = null, inv = null;
        try
        {
            Need(sheetView != null, "SHEET_FIRST_VIEW_REQUIRED");
            xform = sheetView.ModelToViewTransform;
            Need(xform != null, "SHEET_MODEL_TO_VIEW_XFORM_REQUIRED");
            inv = xform.IInverse();
            Need(inv != null, "SHEET_INVERSE_XFORM_REQUIRED");
            return Numbers(inv.ArrayData);
        }
        finally
        {
            Release(inv);
            Release(xform);
            Release(sheetView);
        }
    }

    static void CreateSheetSketchLine(ModelDoc2 drawing, double[] invMatrix, double x1, double y1, double x2, double y2)
    {
        if (Math.Abs(x2 - x1) + Math.Abs(y2 - y1) <= 1e-8) return;
        double[] a = TransformPoint(new[] { x1, y1, 0.0 }, invMatrix);
        double[] b = TransformPoint(new[] { x2, y2, 0.0 }, invMatrix);
        SketchSegment seg = drawing.SketchManager.CreateLine(a[0], a[1], a[2], b[0], b[1], b[2]) as SketchSegment;
        Need(seg != null, "SHEET_SKETCH_LINE_CREATE_FAILED");
        seg.ConstructionGeometry = false;
        Release(seg);
    }

    static void CreateSheetSketchArc(ModelDoc2 drawing, double[] invMatrix, double[] c, double[] a, double[] b, double[] mid)
    {
        double[] cc = TransformPoint(new[] { c[0], c[1], 0.0 }, invMatrix);
        double[] aa = TransformPoint(new[] { a[0], a[1], 0.0 }, invMatrix);
        double[] bb = TransformPoint(new[] { b[0], b[1], 0.0 }, invMatrix);
        double[] mm = TransformPoint(new[] { mid[0], mid[1], 0.0 }, invMatrix);
        double start = Math.Atan2(aa[1] - cc[1], aa[0] - cc[0]);
        double end = Math.Atan2(bb[1] - cc[1], bb[0] - cc[0]);
        double midA = Math.Atan2(mm[1] - cc[1], mm[0] - cc[0]);
        double ccw = ProdWrap2Pi(end - start);
        double sweep = ProdWrap2Pi(midA - start) <= ccw + 1e-6 ? ccw : ccw - 2.0 * Math.PI;
        SketchSegment seg = drawing.SketchManager.CreateArc(cc[0], cc[1], cc[2], aa[0], aa[1], aa[2], bb[0], bb[1], bb[2], (short)(sweep > 0 ? 1 : -1)) as SketchSegment;
        Need(seg != null, "SHEET_SKETCH_ARC_CREATE_FAILED");
        seg.ConstructionGeometry = false;
        Release(seg);
    }

    static void DrawCleanA3FrameAtOneToOne(ISldWorks sw, ModelDoc2 drawing, double verticalDividerX, DxfTrace trace)
    {
        ActivateOutputDrawing(sw, drawing);
        DrawingDoc document = (DrawingDoc)drawing;
        document.EditSheet();
        Need(document.ActivateView(""), "CLEAN_FRAME_SHEET_CONTEXT_REQUIRED");
        VariableTrial.ModelClearSelection2(drawing, true, "DrawCleanA3Frame");
        double[] invMatrix = GetSheetToSketchInverseMatrix(document);
        SketchManager mgr = drawing.SketchManager;
        bool prevDb = mgr.AddToDB;
        bool prevDisp = mgr.DisplayWhenAdded;
        document.EditSketch();
        try
        {
            mgr.AddToDB = true;
            mgr.DisplayWhenAdded = false;
            double[][][] segments = new[]
            {
                // Outer A3 border (390 x 277 mm)
                new[] { new[] { 0.020, 0.010 }, new[] { 0.410, 0.010 } },
                new[] { new[] { 0.410, 0.010 }, new[] { 0.410, 0.287 } },
                new[] { new[] { 0.410, 0.287 }, new[] { 0.020, 0.287 } },
                new[] { new[] { 0.020, 0.287 }, new[] { 0.020, 0.010 } },
                // Header banner rule
                new[] { new[] { 0.020, 0.260 }, new[] { 0.410, 0.260 } },
                // Bottom title block grid
                new[] { new[] { 0.020, 0.042 }, new[] { 0.410, 0.042 } },
                new[] { new[] { 0.020, 0.024 }, new[] { 0.410, 0.024 } },
                new[] { new[] { 0.145, 0.010 }, new[] { 0.145, 0.042 } },
                new[] { new[] { 0.275, 0.010 }, new[] { 0.275, 0.042 } },
                // Right-hand specification divider
                new[] { new[] { verticalDividerX, 0.042 }, new[] { verticalDividerX, 0.260 } }
            };
            foreach (double[][] pair in segments)
            {
                VariableTrial.RuntimeOwnership();
                CreateSheetSketchLine(drawing, invMatrix, pair[0][0], pair[0][1], pair[1][0], pair[1][1]);
            }
        }
        finally
        {
            mgr.AddToDB = prevDb;
            mgr.DisplayWhenAdded = prevDisp;
            document.EditSheet();
            VariableTrial.ModelClearSelection2(drawing, true, "DrawCleanA3Frame.Done");
            Release(mgr);
        }
        trace.Record("clean_a3_frame_drawn", new { verticalDividerX = verticalDividerX });
    }

    static string EnsureDrawingLayer(ModelDoc2 drawing, string name, string desc, int colorRef, int style, int weight)
    {
        LayerMgr layers = drawing.GetLayerManager() as LayerMgr;
        if (layers == null) return "";
        try
        {
            Layer existing = layers.GetLayer(name) as Layer;
            if (existing == null)
            {
                layers.AddLayer(name, desc, colorRef, style, weight);
            }
            else
            {
                Release(existing);
            }
            string prev = layers.GetCurrentLayer() ?? "";
            layers.SetCurrentLayer(name);
            return prev;
        }
        finally { Release(layers); }
    }

    static void RestoreDrawingLayer(ModelDoc2 drawing, string previousLayer)
    {
        LayerMgr layers = drawing.GetLayerManager() as LayerMgr;
        if (layers == null) return;
        try { layers.SetCurrentLayer(previousLayer ?? ""); }
        finally { Release(layers); }
    }

    static void DrawTableNotes(ISldWorks sw, ModelDoc2 drawing, List<object> notes, double x, double y, string title, string[] headers, List<string[]> rows, double[] colWidths, double fontSize = 0.0020)
    {
        notes.Add(NoteAt(sw, drawing, title, x, y, 0.0024));
        double cx = x;
        for (int c = 0; c < headers.Length; c++)
        {
            var colLines = new List<string> { headers[c] };
            foreach (string[] r in rows)
            {
                colLines.Add(c < r.Length && !String.IsNullOrEmpty(r[c]) ? r[c] : "-");
            }
            string colText = String.Join("\n", colLines);
            // Guarantee unique note text even if two columns coincidentally match
            if (notes.OfType<Dictionary<string, object>>().Any(n => NoteText(Convert.ToString(n["text"], Invariant)) == NoteText(colText)))
                colText = colText + " ";
            notes.Add(NoteAt(sw, drawing, colText, cx, y - 0.0052, fontSize));
            cx += colWidths[c];
        }
    }

    sealed class FlatHoleRecord
    {
        internal int Index;
        internal Edge Edge;
        internal Entity ViewEntity;
        internal double XMm, YMm, DMm, RadiusM;
        internal double[] Projected;
    }

    sealed class FlatPatternExtractResult
    {
        internal double FlatWidthMm, FlatHeightMm;
        internal double FlatMinX, FlatMinY, FlatScale;
        internal List<FlatHoleRecord> Holes = new List<FlatHoleRecord>();
        internal List<Dxf2DLine> BendLinesMm = new List<Dxf2DLine>();
        internal double[] NotchFlatMm; // [xMm, yMm]
        internal string NotchLabel;
        internal string NotchDescription;
    }

    static FlatPatternExtractResult ExtractFlatPatternDetails(ISldWorks sw, ModelDoc2 model, ModelDoc2 drawing, SwView flatView, string relPath, List<object> dimensions)
    {
        VariableTrial.RuntimeOwnership();
        ActivateOutputDrawing(sw, drawing);
        Need(((DrawingDoc)drawing).ActivateView(flatView.GetName2()), "FLAT_EXTRACT_VIEW_ACTIVATION_FAILED");
        ActivateConfiguration(model, flatView.ReferencedConfiguration, "FLAT_EXTRACT_CFG_FAILED");
        Need(VariableTrial.ModelForceRebuild3(model, false, "ExtractFlat.Model"), "FLAT_EXTRACT_MODEL_REBUILD_FAILED");
        Need(VariableTrial.ModelForceRebuild3(drawing, false, "ExtractFlat.Drawing"), "FLAT_EXTRACT_DRAWING_REBUILD_FAILED");

        var res = new FlatPatternExtractResult { FlatScale = flatView.ScaleDecimal };
        object[] bodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
        try
        {
            Need(bodies.Length == 1, "ONE_FLAT_BODY_REQUIRED_FOR_DRAWING");
            Body2 body = (Body2)bodies[0];
            var vProj = new List<double[]>();
            foreach (Vertex v in Items(body.GetVertices()).Cast<Vertex>())
            {
                try { vProj.Add(Project(Numbers(v.GetPoint()), flatView)); }
                finally { Release(v); }
            }
            Need(vProj.Count > 2, "FLAT_VERTICES_REQUIRED");
            double minX = vProj.Min(p => p[0]), maxX = vProj.Max(p => p[0]);
            double minY = vProj.Min(p => p[1]), maxY = vProj.Max(p => p[1]);
            res.FlatMinX = minX;
            res.FlatMinY = minY;
            res.FlatWidthMm = (maxX - minX) / flatView.ScaleDecimal * 1000.0;
            res.FlatHeightMm = (maxY - minY) / flatView.ScaleDecimal * 1000.0;
            double viewMidX = 0.5 * (minX + maxX), viewMidY = 0.5 * (minY + maxY);

            // 1. Complete round holes visible in flat view
            var candidates = new List<FlatHoleRecord>();
            MathTransform viewXform = flatView.ModelToViewTransform;
            object[] components = Items(flatView.GetVisibleComponents());
            try
            {
                double[] matrix = Numbers(viewXform.ArrayData);
                foreach (Component2 comp in components)
                {
                    object[] visEdges = Items(flatView.GetVisibleEntities2(comp, (int)swViewEntityType_e.swViewEntityType_Edge));
                    foreach (object ve in visEdges)
                    {
                        bool keep = false;
                        object nativeObj = null;
                        Curve curve = null;
                        CurveParamData cpd = null;
                        try
                        {
                            if (!(ve is Entity)) continue;
                            nativeObj = model.Extension.GetCorrespondingEntity2(ve);
                            Edge edge = nativeObj as Edge;
                            if (edge == null) continue;
                            curve = edge.GetCurve() as Curve;
                            if (curve == null || !curve.IsCircle()) continue;
                            cpd = edge.GetCurveParams3();
                            if (cpd == null) continue;
                            double[] cp = Numbers(curve.CircleParams);
                            if (cp.Length < 7 || cp[6] <= 0) continue;
                            double len = curve.GetLength3(cpd.UMinValue, cpd.UMaxValue);
                            double sweep = Math.Abs(cpd.UMaxValue - cpd.UMinValue);
                            bool fullCircle = Math.Abs(sweep - 2.0 * Math.PI) <= 1e-4
                                && Math.Abs(len - 2.0 * Math.PI * cp[6]) <= 0.001;
                            if (!fullCircle) continue;
                            double[] axis = Enumerable.Range(0, 3).Select(i => cp[3] * matrix[i] + cp[4] * matrix[i + 3] + cp[5] * matrix[i + 6]).ToArray();
                            if (Math.Abs(axis[0]) > 0.01 || Math.Abs(axis[1]) > 0.01 || Math.Abs(Math.Abs(axis[2]) - 1.0) > 0.01) continue;
                            double[] centerM = cp.Take(3).ToArray();
                            double[] proj = Project(centerM, flatView);
                            double xMm = (proj[0] - minX) / flatView.ScaleDecimal * 1000.0;
                            double yMm = (proj[1] - minY) / flatView.ScaleDecimal * 1000.0;
                            if (!candidates.Any(h => Math.Abs(h.XMm - xMm) < 0.05 && Math.Abs(h.YMm - yMm) < 0.05 && Math.Abs(h.RadiusM - cp[6]) < 1e-4))
                            {
                                candidates.Add(new FlatHoleRecord
                                {
                                    Edge = edge,
                                    ViewEntity = (Entity)ve,
                                    XMm = xMm,
                                    YMm = yMm,
                                    DMm = cp[6] * 2000.0,
                                    RadiusM = cp[6],
                                    Projected = proj
                                });
                                keep = true;
                            }
                        }
                        finally
                        {
                            Release(cpd);
                            Release(curve);
                            if (!keep) { Release(nativeObj); Release(ve); }
                        }
                    }
                }
            }
            finally
            {
                foreach (object c in components) Release(c);
                Release(viewXform);
            }

            res.Holes = candidates.OrderBy(h => h.YMm).ThenBy(h => h.XMm).ToList();
            for (int i = 0; i < res.Holes.Count; i++) res.Holes[i].Index = i + 1;

            // Add one associative diameter dimension per unique hole diameter
            var annotatedRadii = new List<double>();
            foreach (FlatHoleRecord hole in res.Holes)
            {
                if (annotatedRadii.Any(r => Math.Abs(r - hole.RadiusM) < 1e-4)) continue;
                Need(((DrawingDoc)drawing).ActivateView(flatView.GetName2()), "HOLE_DIM_VIEW_ACTIVATE_FAILED");
                VariableTrial.ModelClearSelection2(drawing, true, "HoleDim.Clear");
                SelectionMgr sel = drawing.SelectionManager as SelectionMgr;
                SelectData sd = sel.CreateSelectData();
                DisplayDimension disp = null;
                try
                {
                    sd.View = flatView;
                    sd.X = hole.Projected[0] + hole.RadiusM * flatView.ScaleDecimal;
                    sd.Y = hole.Projected[1];
                    sd.Z = 0;
                    if (hole.ViewEntity.Select4(false, sd) && sel.GetSelectedObjectCount2(-1) == 1)
                    {
                        int dimErr = -1;
                        double tx = hole.Projected[0] + (hole.Projected[0] <= viewMidX ? 0.011 : -0.011);
                        double ty = hole.Projected[1] + (hole.Projected[1] <= viewMidY ? 0.010 : -0.010);
                        disp = drawing.Extension.AddSpecificDimension(tx, ty, 0, (int)swDimensionType_e.swDiameterDimension, ref dimErr) as DisplayDimension;
                        if (disp != null && dimErr == 0)
                        {
                            Annotation ann = disp.GetAnnotation() as Annotation;
                            Dimension dObj = disp.GetDimension2(0) as Dimension;
                            try
                            {
                                if (ann != null && dObj != null && !ann.IsDangling())
                                {
                                    Font(ann, 0.0024);
                                    dimensions.Add(new { kind = "diameter", view = flatView.GetName2(), diameterMm = dObj.SystemValue * 1000.0 });
                                    annotatedRadii.Add(hole.RadiusM);
                                }
                            }
                            finally { Release(dObj); Release(ann); }
                        }
                    }
                }
                finally
                {
                    VariableTrial.ModelClearSelection2(drawing, true, "HoleDim.Done");
                    Release(disp);
                    Release(sd);
                    Release(sel);
                }
            }

            // 2. Identify Craft 1 Notch on flatBody edges
            bool isLeftDoor = Path.GetFileName(relPath).StartsWith("L_", StringComparison.OrdinalIgnoreCase);
            var semiNotchPts = new List<double[]>();
            var triMidPts = new List<double[]>();
            object[] bodyEdges = Items(body.GetEdges());
            try
            {
                foreach (object eObj in bodyEdges)
                {
                    Edge edge = eObj as Edge;
                    Curve c = edge == null ? null : edge.GetCurve() as Curve;
                    try
                    {
                        if (c == null) continue;
                        if (isLeftDoor && c.IsCircle())
                        {
                            double[] cp = Numbers(c.CircleParams);
                            double rMm = cp.Length >= 7 ? cp[6] * 1000.0 : 0.0;
                            if (Math.Abs(rMm - 1.5) <= 0.08)
                            {
                                double[] p = Project(cp.Take(3).ToArray(), flatView);
                                double xMm = (p[0] - minX) / flatView.ScaleDecimal * 1000.0;
                                double yMm = (p[1] - minY) / flatView.ScaleDecimal * 1000.0;
                                if (Math.Abs(yMm - 0.5 * res.FlatHeightMm) < 60.0)
                                    semiNotchPts.Add(new[] { xMm, yMm });
                            }
                        }
                        else if (!isLeftDoor && c.IsLine())
                        {
                            Vertex va = edge.GetStartVertex() as Vertex;
                            Vertex vb = edge.GetEndVertex() as Vertex;
                            try
                            {
                                if (va == null || vb == null) continue;
                                double[] pa = Numbers(va.GetPoint()), pb = Numbers(vb.GetPoint());
                                double[] spa = Project(pa, flatView), spb = Project(pb, flatView);
                                double ax = (spa[0] - minX) / flatView.ScaleDecimal * 1000.0;
                                double ay = (spa[1] - minY) / flatView.ScaleDecimal * 1000.0;
                                double bx = (spb[0] - minX) / flatView.ScaleDecimal * 1000.0;
                                double by = (spb[1] - minY) / flatView.ScaleDecimal * 1000.0;
                                double len2D = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                                if (Math.Abs(len2D - 1.5 * Math.Sqrt(2.0)) <= 0.15)
                                {
                                    double mx = 0.5 * (ax + bx), my = 0.5 * (ay + by);
                                    if (Math.Abs(my - 0.5 * res.FlatHeightMm) < 60.0)
                                        triMidPts.Add(new[] { mx, my });
                                }
                            }
                            finally { Release(vb); Release(va); }
                        }
                    }
                    finally { Release(c); }
                }
            }
            finally { foreach (object e in bodyEdges) Release(e); }

            if (isLeftDoor && semiNotchPts.Count > 0)
            {
                res.NotchFlatMm = semiNotchPts.OrderBy(p => Math.Abs(p[1] - 0.5 * res.FlatHeightMm)).First();
                res.NotchLabel = "C1 半圆缺口R1.5";
                res.NotchDescription = "左门防呆中心";
            }
            else if (!isLeftDoor && triMidPts.Count >= 2)
            {
                var midCluster = triMidPts.OrderBy(p => Math.Abs(p[1] - 0.5 * res.FlatHeightMm)).Take(2).ToArray();
                res.NotchFlatMm = new[] { 0.5 * (midCluster[0][0] + midCluster[1][0]), 0.5 * (midCluster[0][1] + midCluster[1][1]) };
                res.NotchLabel = "C1 三角缺口3x1.5";
                res.NotchDescription = "右门防呆中心";
            }

            // 3. Extract native bend lines from flatView in flat-pattern mm
            Sketch vSketch = flatView.GetSketch() as Sketch;
            MathTransform sM2s = vSketch == null ? null : vSketch.ModelToSketchTransform;
            MathTransform sInv = sM2s == null ? null : sM2s.IInverse();
            object[] rawBends = Items(flatView.GetBendLines());
            try
            {
                if (sInv != null)
                {
                    double[] mArr = Numbers(sInv.ArrayData);
                    foreach (object rb in rawBends)
                    {
                        SketchSegment seg = rb as SketchSegment;
                        SketchLine ln = rb as SketchLine;
                        if (seg == null || ln == null) continue;
                        SketchPoint sa = ln.GetStartPoint2() as SketchPoint;
                        SketchPoint sb = ln.GetEndPoint2() as SketchPoint;
                        try
                        {
                            if (sa == null || sb == null) continue;
                            double[] pa = TransformPoint(new[] { sa.X, sa.Y, sa.Z }, mArr);
                            double[] pb = TransformPoint(new[] { sb.X, sb.Y, sb.Z }, mArr);
                            res.BendLinesMm.Add(new Dxf2DLine
                            {
                                Ax = (pa[0] - minX) / flatView.ScaleDecimal * 1000.0,
                                Ay = (pa[1] - minY) / flatView.ScaleDecimal * 1000.0,
                                Bx = (pb[0] - minX) / flatView.ScaleDecimal * 1000.0,
                                By = (pb[1] - minY) / flatView.ScaleDecimal * 1000.0
                            });
                        }
                        finally { Release(sb); Release(sa); }
                    }
                }
            }
            finally
            {
                foreach (object rb in rawBends) Release(rb);
                Release(sInv);
                Release(sM2s);
                Release(vSketch);
            }

            return res;
        }
        finally
        {
            foreach (object b in bodies) Release(b);
        }
    }

    static void ProjectAndDrawProductionMarksOnFlatView(ISldWorks sw, ModelDoc2 drawing, SwView flatView, ProdMarkPartCapture capture, FlatPatternExtractResult flatRes, out string lBoundsText, out string hookBoundsText, out int markSegCount, out double[] flatUpperPinMm, out double[] flatLowerPinMm)
    {
        lBoundsText = "-";
        hookBoundsText = "-";
        markSegCount = 0;
        flatUpperPinMm = null;
        flatLowerPinMm = null;
        if (capture == null || capture.Groups.Count == 0 || flatRes.BendLinesMm.Count < 4) return;

        var pinCircles = flatRes.Holes
            .Where(h => Math.Abs(h.DMm - 10.2) <= 0.25)
            .Select(h => new Dxf2DCircle { Cx = h.XMm, Cy = h.YMm, R = 0.5 * h.DMm })
            .ToList();
        if (pinCircles.Count != 2) return;

        DrawingDoc document = (DrawingDoc)drawing;
        document.EditSheet();
        Need(document.ActivateView(""), "MARK_PROJECT_SHEET_CONTEXT_REQUIRED");
        double[] invSheet = GetSheetToSketchInverseMatrix(document);
        string prevLayer = EnsureDrawingLayer(drawing, "MARK", "激光打标与焊接定位", 16711680, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_THIN);

        var lPtsMm = new List<double[]>();
        var hookPtsMm = new List<double[]>();

        SketchManager mgr = drawing.SketchManager;
        bool prevDb = mgr.AddToDB;
        bool prevDisp = mgr.DisplayWhenAdded;
        document.EditSketch();
        try
        {
            mgr.AddToDB = true;
            mgr.DisplayWhenAdded = false;
            foreach (ProdSketchGroup grp in capture.Groups)
            {
                Rigid2D xform = SolveProdMarkTransform(capture, grp, new List<Dxf2DLine>(), flatRes.BendLinesMm, pinCircles);
                if (grp.PlaneAxis == 2 && capture.PinHoles.Count == 2 && flatUpperPinMm == null)
                {
                    ProdHole2D fTop = capture.PinHoles.OrderByDescending(p => p.Center3Mm[1]).First();
                    ProdHole2D fBot = capture.PinHoles.OrderBy(p => p.Center3Mm[1]).First();
                    flatUpperPinMm = xform.Map(new[] { fTop.Center3Mm[0], fTop.Center3Mm[1] });
                    flatLowerPinMm = xform.Map(new[] { fBot.Center3Mm[0], fBot.Center3Mm[1] });
                }
                foreach (ProdSeg2D seg in grp.Segments)
                {
                    VariableTrial.RuntimeOwnership();
                    double[] aMm = xform.Map(seg.A);
                    double[] bMm = xform.Map(seg.B);
                    bool isLMark = (seg.SourceSketchName ?? "").IndexOf("加强筋", StringComparison.Ordinal) >= 0;
                    if (isLMark) { lPtsMm.Add(aMm); lPtsMm.Add(bMm); }
                    else { hookPtsMm.Add(aMm); hookPtsMm.Add(bMm); }

                    double[] aSheet = { flatRes.FlatMinX + aMm[0] * 0.001 * flatRes.FlatScale, flatRes.FlatMinY + aMm[1] * 0.001 * flatRes.FlatScale };
                    double[] bSheet = { flatRes.FlatMinX + bMm[0] * 0.001 * flatRes.FlatScale, flatRes.FlatMinY + bMm[1] * 0.001 * flatRes.FlatScale };

                    if (seg.Kind == "LINE")
                    {
                        CreateSheetSketchLine(drawing, invSheet, aSheet[0], aSheet[1], bSheet[0], bSheet[1]);
                    }
                    else
                    {
                        double[] cMm = xform.Map(seg.Center);
                        double[] mMm = xform.Map(seg.Mid);
                        double[] cSheet = { flatRes.FlatMinX + cMm[0] * 0.001 * flatRes.FlatScale, flatRes.FlatMinY + cMm[1] * 0.001 * flatRes.FlatScale };
                        double[] mSheet = { flatRes.FlatMinX + mMm[0] * 0.001 * flatRes.FlatScale, flatRes.FlatMinY + mMm[1] * 0.001 * flatRes.FlatScale };
                        CreateSheetSketchArc(drawing, invSheet, cSheet, aSheet, bSheet, mSheet);
                    }
                    markSegCount++;
                }
            }
        }
        finally
        {
            mgr.AddToDB = prevDb;
            mgr.DisplayWhenAdded = prevDisp;
            document.EditSheet();
            RestoreDrawingLayer(drawing, prevLayer);
            VariableTrial.ModelClearSelection2(drawing, true, "DrawMarks.Done");
            Release(mgr);
        }

        if (lPtsMm.Count > 0)
        {
            lBoundsText = String.Format(Invariant, "{0}~{1}|{2}~{3}",
                FormatMmShort(lPtsMm.Min(p => p[0])), FormatMmShort(lPtsMm.Max(p => p[0])),
                FormatMmShort(lPtsMm.Min(p => p[1])), FormatMmShort(lPtsMm.Max(p => p[1])));
        }
        if (hookPtsMm.Count > 0)
        {
            hookBoundsText = String.Format(Invariant, "{0}~{1}|{2}~{3}",
                FormatMmShort(hookPtsMm.Min(p => p[0])), FormatMmShort(hookPtsMm.Max(p => p[0])),
                FormatMmShort(hookPtsMm.Min(p => p[1])), FormatMmShort(hookPtsMm.Max(p => p[1])));
        }
    }

    static void SaveDrawingAndExportPdf(ISldWorks sw, ModelDoc2 drawing, SwView[] views, List<object> expectedNotes, string drawingPath, string pdfPath, DxfTrace trace, int saveTimeoutMs)
    {
        ActivateOutputDrawing(sw, drawing);
        object displayProof = PrepareDrawingDisplay(sw, drawing, views, expectedNotes, true, trace);
        trace.Record("drawing_save_display_ready", displayProof);

        int drawingErrors = 0, drawingWarnings = 0;
        VariableTrial.RuntimeOwnership();
        bool saved = VariableTrial.ModelSaveAs(drawing, drawingPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, out drawingErrors, out drawingWarnings, "SaveDrawing.Slddrw", saveTimeoutMs);
        Need(saved && drawingErrors == 0 && File.Exists(drawingPath), "NATIVE_SLDDRW_SAVE_FAILED " + drawingPath + " err=" + drawingErrors + "/" + drawingWarnings);

        ExportPdfData pdf = sw.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as ExportPdfData;
        int pdfErrors = 0, pdfWarnings = 0;
        bool pdfSaved = false;
        try
        {
            Need(pdf != null, "NATIVE_PDF_EXPORT_DATA_REQUIRED");
            pdf.ViewPdfAfterSaving = false;
            pdf.ExportAs3D = false;
            DrawingDoc document = (DrawingDoc)drawing;
            string[] sheetNames = Items(document.GetSheetNames()).Select(Convert.ToString).ToArray();
            Need(sheetNames.Length == 1, "ONE_DRAWING_SHEET_REQUIRED");
            var pdfSheets = new List<Sheet>();
            DispatchWrapper[] wrappers = null;
            try
            {
                Need(document.ActivateSheet(sheetNames[0]), "PDF_SHEET_ACTIVATE_FAILED");
                Sheet activeSheet = document.GetCurrentSheet() as Sheet;
                Need(activeSheet != null, "PDF_CURRENT_SHEET_REQUIRED");
                pdfSheets.Add(activeSheet);
                wrappers = pdfSheets.Select(s => new DispatchWrapper(s)).ToArray();
                Need(pdf.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportSpecifiedSheets, wrappers), "PDF_SET_SHEETS_FAILED");
                PrepareDrawingDisplay(sw, drawing, views, expectedNotes, false, trace);
                VariableTrial.ModelClearSelection2(drawing, true, "SaveDrawing.PdfClear");
                pdfSaved = VariableTrial.ModelSaveAs(drawing, pdfPath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, pdf, out pdfErrors, out pdfWarnings, "SaveDrawing.Pdf", saveTimeoutMs);
                GC.KeepAlive(wrappers);
            }
            finally
            {
                foreach (Sheet s in pdfSheets) Release(s);
            }
        }
        finally { Release(pdf); }

        Need(pdfSaved && pdfErrors == 0 && File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 100, "NATIVE_PDF_EXPORT_FAILED " + pdfPath + " err=" + pdfErrors + "/" + pdfWarnings);
        using (var stream = File.OpenRead(pdfPath))
        {
            byte[] header = new byte[5];
            Need(stream.Read(header, 0, 5) == 5 && Encoding.ASCII.GetString(header) == "%PDF-", "NATIVE_PDF_HEADER_INVALID " + pdfPath);
        }
    }

    static int CaptureMarkSegCountByKeyword(ProdMarkPartCapture cap, string keyword)
    {
        if (cap == null) return 0;
        int count = 0;
        foreach (ProdSketchGroup grp in cap.Groups)
            foreach (ProdSeg2D seg in grp.Segments)
                if ((seg.SourceSketchName ?? "").IndexOf(keyword, StringComparison.Ordinal) >= 0) count++;
        return count;
    }

    static double[] GetSolidBodyBoundsMm(ModelDoc2 model)
    {
        object[] bodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
        try
        {
            Need(bodies.Length == 1, "ONE_SOLID_BODY_REQUIRED_FOR_BOUNDS");
            Body2 body = (Body2)bodies[0];
            double[] box = new double[6];
            for (int a = 0; a < 3; a++)
            {
                double x, y, z;
                double[] v = new double[3];
                v[a] = -1;
                Need(body.GetExtremePoint(v[0], v[1], v[2], out x, out y, out z), "BODY_EXTREME_MIN_FAILED");
                box[a] = new[] { x, y, z }[a] * 1000.0;
                v[a] = 1;
                Need(body.GetExtremePoint(v[0], v[1], v[2], out x, out y, out z), "BODY_EXTREME_MAX_FAILED");
                box[a + 3] = new[] { x, y, z }[a] * 1000.0;
            }
            return box;
        }
        finally
        {
            foreach (object b in bodies) Release(b);
        }
    }

    static object BuildDoorPanelEngineeringDrawing(
        ISldWorks sw,
        string stagedDoorPath,
        string relPath,
        int installedQuantity,
        string cabinetSpecLabel,
        string stageDrawingDir,
        string outRoot,
        string diagDir,
        string templatePath,
        int doorOrdinal)
    {
        VariableTrial.RuntimeOwnership();
        string fileStem = Path.GetFileNameWithoutExtension(relPath);
        bool isLeft = Path.GetFileName(relPath).StartsWith("L_", StringComparison.OrdinalIgnoreCase);
        string sideCn = isLeft ? "左门板" : "右门板";
        string outputStem = String.Format(Invariant, "{0:00}_{1}_{2}", doorOrdinal, sideCn, fileStem);
        string stageSlddrw = Local(Path.Combine(stageDrawingDir, outputStem + ".SLDDRW"));
        string stagePdf = Local(Path.Combine(stageDrawingDir, outputStem + ".pdf"));
        var trace = new DxfTrace(Path.Combine(diagDir, outputStem + "-trace"));

        int errs = 0, warns = 0;
        ModelDoc2 model = VariableTrial.SwOpenDoc6(sw, stagedDoorPath, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", out errs, out warns, "DoorDrw.OpenPart");
        ModelDoc2 drawing = null;
        SwView folded = null, flat = null;
        FlatPatternExtractResult flatExtract = null;
        try
        {
            Need(model != null && model is PartDoc, "DOOR_DRAWING_PART_OPEN_FAILED " + relPath);
            string baseConfig;
            List<string> deletedConfigs;
            DeleteDerivedFlatConfigsAndPrepare(model, "DoorDrw.Prep." + fileStem, out baseConfig, out deletedConfigs);
            SetProductionMarkSketchesThisConfig(model, false, "DoorDrw.EnsureFoldedMarks." + fileStem);
            Need(VariableTrial.ModelForceRebuild3(model, false, "DoorDrw.FoldedRebuild." + fileStem), "DOOR_FOLDED_REBUILD_FAILED " + relPath);

            ProdMarkPartCapture markCapture = CaptureProductionMarksFromFoldedPart(sw, model, relPath);
            double[] foldedBounds = markCapture != null && markCapture.FoldedBoundsMm != null && markCapture.FoldedBoundsMm.Length == 6
                ? markCapture.FoldedBoundsMm
                : GetSolidBodyBoundsMm(model);
            double foldedW = foldedBounds[3] - foldedBounds[0];
            double foldedH = foldedBounds[4] - foldedBounds[1];

            drawing = VariableTrial.SwNewDocument(sw, templatePath, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297, "DoorDrw.NewDoc");
            Need(drawing != null && drawing is DrawingDoc, "DOOR_NEW_DRAWING_FAILED " + fileStem);
            VariableTrial.RegisterOwned(drawing);
            VariableTrial.ModelSetVisible(drawing, true, "DoorDrw.ShowDrawing");
            ActivateOutputDrawing(sw, drawing);
            DrawingDoc document = (DrawingDoc)drawing;
            document.EditSheet();
            Sheet sheet = document.GetCurrentSheet() as Sheet;
            try
            {
                Need(sheet != null, "DOOR_SHEET_REQUIRED");
                Need(document.SetupSheet5(sheet.GetName(), (int)swDwgPaperSizes_e.swDwgPaperA3size, (int)swDwgTemplates_e.swDwgTemplateNone, 1, 1, false, "", 0.420, 0.297, "", true), "DOOR_SHEET_SETUP_FAILED");
                sheet.SheetFormatVisible = false;
                sheet.SetName(sideCn + "_" + fileStem);
            }
            finally { Release(sheet); }

            // Create Folded View (left zone)
            string foldedOrientation = PickModelViewName(model, "*Back", "*后视", "*Front", "*前视");
            Need(foldedOrientation != null, "DOOR_FOLDED_ORIENTATION_MISSING " + fileStem);
            folded = document.CreateDrawViewFromModelView3(stagedDoorPath, foldedOrientation, 0.072, 0.162, 0) as SwView;
            Need(folded != null && !folded.IsFlatPatternView() && folded.SetName2("折弯态"), "DOOR_FOLDED_VIEW_CREATE_FAILED " + fileStem);
            folded.ReferencedConfiguration = baseConfig;
            ResetSheetScaleOneToOnePreservingViews(sw, drawing, new[] { folded });
            FitViewInBox(folded, 0.072, 0.162, 0.078, 0.132);
            Need(VariableTrial.ModelForceRebuild3(drawing, false, "DoorDrw.FitFolded." + fileStem), "DOOR_FOLDED_VIEW_REBUILD_FAILED");

            var dimensions = new List<object>();
            dimensions.Add(Overall(sw, model, drawing, folded, true, trace));
            dimensions.Add(Overall(sw, model, drawing, folded, false, trace));

            // Create True Flat-Pattern View (middle zone)
            flat = document.CreateFlatPatternViewFromModelView3(stagedDoorPath, baseConfig, 0.194, 0.162, 0, false, false) as SwView;
            Need(flat != null && flat.IsFlatPatternView() && flat.SetName2("真实展开态"), "DOOR_FLAT_VIEW_CREATE_FAILED " + fileStem);
            string flatConfig = flat.ReferencedConfiguration;
            ActivateConfiguration(model, flatConfig, "DoorDrw.ActivateFlat." + fileStem);
            SetProductionMarkSketchesThisConfig(model, true, "DoorDrw.SuppressFlatMarks." + fileStem);
            var features = new List<SimpleFeatureInfo>();
            WalkSimpleFeatures(model.FirstFeature() as Feature, true, 0, new HashSet<int>(), features);
            var flatIds = features.Where(f => f.Type == "FlatPattern" || f.Type == "UiBend").OrderBy(f => f.Type == "FlatPattern" ? 0 : 1).Select(f => f.Id).ToList();
            SetFeaturesSuppressionByIds(model, flatIds, false, "DoorDrw.UnsuppressFlat." + fileStem);
            Need(VariableTrial.ModelForceRebuild3(model, false, "DoorDrw.RebuildFlatModel." + fileStem), "DOOR_FLAT_MODEL_REBUILD_FAILED");

            ResetSheetScaleOneToOnePreservingViews(sw, drawing, new[] { folded, flat });
            FitViewInBox(flat, 0.194, 0.162, 0.092, 0.132);
            flat.ShowSheetMetalBendNotes = true;
            Need(VariableTrial.ModelForceRebuild3(drawing, false, "DoorDrw.FitFlat." + fileStem), "DOOR_FLAT_VIEW_REBUILD_FAILED");

            // Make native bend notes smaller so they fit cleanly on scaled views
            foreach (object bnObj in Items(flat.GetNotes()))
            {
                Note bn = bnObj as Note;
                if (bn == null || !bn.IsBendLineNote) { Release(bnObj); continue; }
                Annotation bAnn = bn.GetAnnotation() as Annotation;
                TextFormat bFmt = bAnn == null ? null : bAnn.GetTextFormat(0) as TextFormat;
                try
                {
                    if (bFmt != null && bAnn != null)
                    {
                        bFmt.CharHeight = 0.0020;
                        bAnn.SetTextFormat(0, false, bFmt);
                    }
                }
                finally { Release(bFmt); Release(bAnn); Release(bnObj); }
            }

            dimensions.Add(Overall(sw, model, drawing, flat, true, trace));
            dimensions.Add(Overall(sw, model, drawing, flat, false, trace));

            flatExtract = ExtractFlatPatternDetails(sw, model, drawing, flat, relPath, dimensions);

            // Switch model back to folded config, keep marks visible in folded config, and save staged part with both configs intact
            ActivateConfiguration(model, baseConfig, "DoorDrw.RestoreFolded." + fileStem);
            SetProductionMarkSketchesThisConfig(model, false, "DoorDrw.RestoreFoldedMarks." + fileStem);
            Need(VariableTrial.ModelForceRebuild3(model, false, "DoorDrw.FinalModelRebuild." + fileStem), "DOOR_FINAL_MODEL_REBUILD_FAILED");
            int saveErrs = 0, saveWarns = 0;
            Need(VariableTrial.ModelSave3(model, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, out saveErrs, out saveWarns, "DoorDrw.SavePart." + fileStem) && saveErrs == 0, "DOOR_STAGE_PART_SAVE_FAILED " + fileStem);

            // Draw A3 frame & right divider at locked 1:1 sheet scale
            DrawCleanA3FrameAtOneToOne(sw, drawing, 0.265, trace);

            // Project & draw Craft 4 MARK contours onto the flat pattern view
            string lBoundsText, hookBoundsText;
            int markSegCount;
            double[] flatUpperPinMm, flatLowerPinMm;
            ProjectAndDrawProductionMarksOnFlatView(sw, drawing, flat, markCapture, flatExtract, out lBoundsText, out hookBoundsText, out markSegCount, out flatUpperPinMm, out flatLowerPinMm);

            // Annotate header, view labels, Craft/Hole table, and title block
            var notes = new List<object>();
            notes.Add(NoteAt(sw, drawing, String.Format(Invariant, "Winnsen 16029 全参数门板工程图 | {0} W{1} x H{2} mm ({3})", sideCn, FormatMmShort(foldedW), FormatMmShort(foldedH), fileStem), 0.025, 0.281, 0.0038));
            notes.Add(NoteAt(sw, drawing, String.Format(Invariant, "视图: 折弯态 + 真实展开态(折弯/打标) | 单台用量: {0}件 | 料厚: T0.8mm | 单位: mm | Q1-Q4 + C1-C4 已集成", installedQuantity), 0.025, 0.269, 0.0024));
            notes.Add(NoteAt(sw, drawing, "折弯态(内面)  比例 1:" + (1.0 / folded.ScaleDecimal).ToString("0.##", Invariant), 0.038, 0.056, 0.0026));
            notes.Add(NoteAt(sw, drawing, "真实展开态(激光打标MARK)  比例 1:" + (1.0 / flat.ScaleDecimal).ToString("0.##", Invariant), 0.150, 0.056, 0.0026));

            var tableRows = new List<string[]>();
            tableRows.Add(new[] { "成形外廓", FormatMmShort(foldedW), FormatMmShort(foldedH), "折弯成品 WxH" });
            tableRows.Add(new[] { "展开外形", FormatMmShort(flatExtract.FlatWidthMm), FormatMmShort(flatExtract.FlatHeightMm), "料厚 T0.8 冷轧板" });
            tableRows.Add(new[] { "单台用量", installedQuantity.ToString(Invariant) + " 件", flat.GetBendLineCount().ToString(Invariant) + " 折弯", "全部 90° R0.2" });

            if (flatExtract.NotchFlatMm != null)
            {
                tableRows.Add(new[] { flatExtract.NotchLabel, FormatMmShort(flatExtract.NotchFlatMm[0]), FormatMmShort(flatExtract.NotchFlatMm[1]), flatExtract.NotchDescription });
            }
            var pinHoles = flatExtract.Holes.Where(h => Math.Abs(h.DMm - 10.2) <= 0.25).ToArray();
            if (pinHoles.Length == 2)
            {
                FlatHoleRecord lowerPin = flatLowerPinMm != null
                    ? pinHoles.OrderBy(h => Math.Abs(h.XMm - flatLowerPinMm[0]) + Math.Abs(h.YMm - flatLowerPinMm[1])).First()
                    : pinHoles.OrderBy(h => h.YMm).First();
                FlatHoleRecord upperPin = pinHoles.FirstOrDefault(h => h != lowerPin) ?? pinHoles.OrderByDescending(h => h.YMm).First();
                double dxPin = Math.Abs(upperPin.XMm - lowerPin.XMm);
                tableRows.Add(new[] { "C2 3D下销孔", FormatMmShort(lowerPin.XMm), FormatMmShort(lowerPin.YMm), "Φ10.2 名义基准" });
                tableRows.Add(new[] { "C2 3D上销孔", FormatMmShort(upperPin.XMm), FormatMmShort(upperPin.YMm), "内偏0.5(实dx=" + dxPin.ToString("0.00", Invariant) + ")" });
            }
            string[] lXY = lBoundsText.Split('|');
            string[] hXY = hookBoundsText.Split('|');
            int lSegCount = CaptureMarkSegCountByKeyword(markCapture, "加强筋");
            int hSegCount = Math.Max(0, markSegCount - lSegCount);
            tableRows.Add(new[] { "C4 加强筋L标", lXY[0], lXY.Length > 1 ? lXY[1] : "-", "4角 3x3 L (" + lSegCount + "线)" });
            tableRows.Add(new[] { "C4 锁钩轮廓", hXY[0], hXY.Length > 1 ? hXY[1] : "-", "含4-R3钩 (" + hSegCount + "图元)" });

            foreach (FlatHoleRecord h in flatExtract.Holes)
            {
                tableRows.Add(new[] { "圆孔 H" + h.Index, FormatMmShort(h.XMm), FormatMmShort(h.YMm), "Φ" + FormatMmShort(h.DMm) + " 通孔" });
            }

            DrawTableNotes(sw, drawing, notes, 0.270, 0.253,
                "展开工艺与孔位坐标表 (mm) | 原点:展开左下角",
                new[] { "项目", "X(mm)", "Y(mm)", "说明/工艺" },
                tableRows,
                new[] { 0.036, 0.034, 0.034, 0.032 },
                0.0019);

            double craftSummaryY = Math.Max(0.068, 0.238 - (tableRows.Count + 2) * 0.0044);
            string doorCraftSummary = String.Join("\n", new[]
            {
                "门板工艺与检验说明 (" + sideCn + "):",
                "1. 防呆缺口 (C1): 5mm 铰链回折边自由边中点激光切除",
                "   " + (isLeft ? "左门 R1.5mm 半圆缺口" : "右门 3.0 x 1.5mm 三角缺口") + ", 防左右装错。",
                "2. 上销预偏 (C2): 上端 Φ10.2 销孔相对下销轴向内侧预偏 0.50mm",
                "   抵消折弯与焊装热变形, 免现场扩孔校形。",
                "3. 激光打标 (C4): 展开图中蓝线为门内面非切割 MARK 图元,",
                "   包含门加强筋四角 3mm L 定位标及锁扣/4-R3 U型挂钩轮廓。",
                "4. 展开检验: 折弯扣除 1.4mm, 内角 R0.2mm, 板厚 0.8mm,",
                "   展开数据与 DXF_展开图/parts/" + Path.GetFileNameWithoutExtension(relPath) + ".flat.dxf 同步。"
            });
            notes.Add(NoteAt(sw, drawing, doorCraftSummary, 0.270, craftSummaryY, 0.0020));

            // Title Block
            notes.Add(NoteAt(sw, drawing, "图名: 16029 " + sideCn + "工程图 (" + cabinetSpecLabel + ")", 0.024, 0.037, 0.0024));
            notes.Add(NoteAt(sw, drawing, "图号: " + outputStem + "   用量: " + installedQuantity + " 件", 0.024, 0.019, 0.0023));
            notes.Add(NoteAt(sw, drawing, "材料: 冷轧钢板 SPCC/Q235   T0.8mm", 0.149, 0.037, 0.0023));
            notes.Add(NoteAt(sw, drawing, "基准: " + Path.GetFileName(relPath), 0.149, 0.019, 0.0021));
            notes.Add(NoteAt(sw, drawing, "状态: 工程审图版 (production_release=false)", 0.279, 0.037, 0.0022));
            notes.Add(NoteAt(sw, drawing, "日期: " + DateTime.UtcNow.ToString("yyyy-MM-dd", Invariant) + "   单位: mm   比例: 独立", 0.279, 0.019, 0.0021));

            SaveDrawingAndExportPdf(sw, drawing, new[] { folded, flat }, notes, stageSlddrw, stagePdf, trace, VariableTrial.IoRebuildComTimeoutMs);

            // Export final drawings & flat-config door part to output directory
            string outSlddrw = Local(Path.Combine(outRoot, outputStem + ".SLDDRW"));
            string outPdf = Local(Path.Combine(outRoot, outputStem + ".pdf"));
            Directory.CreateDirectory(Path.GetDirectoryName(outSlddrw));
            File.Copy(stageSlddrw, outSlddrw, true);
            File.Copy(stagePdf, outPdf, true);

            string savedPartOut = Local(Path.Combine(outRoot, ".cad_flat_config_parts", relPath));
            Directory.CreateDirectory(Path.GetDirectoryName(savedPartOut));
            File.Copy(stagedDoorPath, savedPartOut, true);

            return new
            {
                kind = "door_panel",
                side = isLeft ? "L" : "R",
                relPath = relPath,
                fileStem = outputStem,
                installedQuantity = installedQuantity,
                foldedWidthMm = Math.Round(foldedW, 3),
                foldedHeightMm = Math.Round(foldedH, 3),
                flatWidthMm = Math.Round(flatExtract.FlatWidthMm, 3),
                flatHeightMm = Math.Round(flatExtract.FlatHeightMm, 3),
                roundHoleCount = flatExtract.Holes.Count,
                bendLineCount = flat.GetBendLineCount(),
                projectedMarkSegments = markSegCount,
                slddrwPath = outSlddrw,
                pdfPath = outPdf,
                slddrwSha256 = Hash(outSlddrw),
                pdfSha256 = Hash(outPdf)
            };
        }
        finally
        {
            Release(flat);
            Release(folded);
            Release(drawing);
            Release(model);
            VariableTrial.SwCloseAllDocuments(sw, true, "DoorDrw.FinallyClose." + fileStem);
        }
    }

    static double[] ProjectShellBoundsInView(SwView view, double[] shellBoundsMm)
    {
        double x0 = shellBoundsMm[0] * 0.001, y0 = shellBoundsMm[1] * 0.001, z0 = shellBoundsMm[2] * 0.001;
        double x1 = shellBoundsMm[3] * 0.001, y1 = shellBoundsMm[4] * 0.001, z1 = shellBoundsMm[5] * 0.001;
        var pts = new List<double[]>();
        foreach (double x in new[] { x0, x1 })
            foreach (double y in new[] { y0, y1 })
                foreach (double z in new[] { z0, z1 })
                    pts.Add(Project(new[] { x, y, z }, view));
        return new[] { pts.Min(p => p[0]), pts.Min(p => p[1]), pts.Max(p => p[0]), pts.Max(p => p[1]) };
    }

    static void DrawHorizontalSpanCallout(ModelDoc2 drawing, double[] invMatrix, double xLeft, double xRight, double yAnchor, double yDim, bool tickDown = true)
    {
        double extGap = tickDown ? -0.0015 : 0.0015;
        double extOver = tickDown ? -0.0020 : 0.0020;
        CreateSheetSketchLine(drawing, invMatrix, xLeft, yAnchor + extGap, xLeft, yDim + extOver);
        CreateSheetSketchLine(drawing, invMatrix, xRight, yAnchor + extGap, xRight, yDim + extOver);
        CreateSheetSketchLine(drawing, invMatrix, xLeft, yDim, xRight, yDim);
        // Clean arrows at ends
        CreateSheetSketchLine(drawing, invMatrix, xLeft, yDim, xLeft + 0.0022, yDim + 0.0008);
        CreateSheetSketchLine(drawing, invMatrix, xLeft, yDim, xLeft + 0.0022, yDim - 0.0008);
        CreateSheetSketchLine(drawing, invMatrix, xRight, yDim, xRight - 0.0022, yDim + 0.0008);
        CreateSheetSketchLine(drawing, invMatrix, xRight, yDim, xRight - 0.0022, yDim - 0.0008);
    }

    static void DrawVerticalSpanCallout(ModelDoc2 drawing, double[] invMatrix, double xAnchor, double xDim, double yBot, double yTop, bool tickLeft = true)
    {
        double extGap = tickLeft ? -0.0015 : 0.0015;
        double extOver = tickLeft ? -0.0020 : 0.0020;
        CreateSheetSketchLine(drawing, invMatrix, xAnchor + extGap, yBot, xDim + extOver, yBot);
        CreateSheetSketchLine(drawing, invMatrix, xAnchor + extGap, yTop, xDim + extOver, yTop);
        CreateSheetSketchLine(drawing, invMatrix, xDim, yBot, xDim, yTop);
        CreateSheetSketchLine(drawing, invMatrix, xDim, yBot, xDim - 0.0008, yBot + 0.0022);
        CreateSheetSketchLine(drawing, invMatrix, xDim, yBot, xDim + 0.0008, yBot + 0.0022);
        CreateSheetSketchLine(drawing, invMatrix, xDim, yTop, xDim - 0.0008, yTop - 0.0022);
        CreateSheetSketchLine(drawing, invMatrix, xDim, yTop, xDim + 0.0008, yTop - 0.0022);
    }

    static object BuildCabinetAssemblyDrawing(
        ISldWorks sw,
        string stagedAsmPath,
        string stageDrawingDir,
        string outRoot,
        string diagDir,
        string templatePath,
        double cabW,
        double cabH,
        double cabD,
        double leafW,
        double[] shellBoundsMm,
        int leftDoorCount,
        int rightDoorCount,
        string leftHeightsText,
        string rightHeightsText,
        int uniquePartCount,
        int totalInstanceCount,
        int sheetMetalUniqueCount,
        int prebiasCount,
        int markPartCount,
        int markSketchCount,
        int markSegmentCount)
    {
        VariableTrial.RuntimeOwnership();
        string outputStem = "00_总装图_16029_variable_full_cabinet";
        string stageSlddrw = Local(Path.Combine(stageDrawingDir, outputStem + ".SLDDRW"));
        string stagePdf = Local(Path.Combine(stageDrawingDir, outputStem + ".pdf"));
        var trace = new DxfTrace(Path.Combine(diagDir, outputStem + "-trace"));

        int errs = 0, warns = 0;
        ModelDoc2 asmModel = VariableTrial.SwOpenDoc6(sw, stagedAsmPath, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", out errs, out warns, "AsmDrw.OpenAsm", VariableTrial.HeavyAssemblyComTimeoutMs);
        ModelDoc2 drawing = null;
        SwView front = null, top = null, right = null, iso = null;
        try
        {
            Need(asmModel != null && asmModel is AssemblyDoc, "ASSEMBLY_DRAWING_OPEN_FAILED");
            VariableTrial.AssemblyResolveAllLightWeightComponents((AssemblyDoc)asmModel, false, "AsmDrw.ResolveAll");

            string vFront = PickModelViewName(asmModel, "*Front", "*前视", "Front", "前视");
            string vRight = PickModelViewName(asmModel, "*Right", "*右视", "Right", "右视");
            string vTop = PickModelViewName(asmModel, "*Top", "*上视", "Top", "上视");
            string vIso = PickModelViewName(asmModel, "*Isometric", "*等轴测", "Isometric", "等轴测");
            Need(vFront != null && vRight != null && vTop != null && vIso != null, "ASSEMBLY_STANDARD_VIEWS_MISSING");

            drawing = VariableTrial.SwNewDocument(sw, templatePath, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297, "AsmDrw.NewDoc");
            Need(drawing != null && drawing is DrawingDoc, "ASSEMBLY_NEW_DRAWING_FAILED");
            VariableTrial.RegisterOwned(drawing);
            VariableTrial.ModelSetVisible(drawing, true, "AsmDrw.ShowDrawing");
            ActivateOutputDrawing(sw, drawing);
            DrawingDoc document = (DrawingDoc)drawing;
            document.EditSheet();
            Sheet sheet = document.GetCurrentSheet() as Sheet;
            try
            {
                Need(sheet != null, "ASSEMBLY_SHEET_REQUIRED");
                Need(document.SetupSheet5(sheet.GetName(), (int)swDwgPaperSizes_e.swDwgPaperA3size, (int)swDwgTemplates_e.swDwgTemplateNone, 1, 1, false, "", 0.420, 0.297, "", true), "ASSEMBLY_SHEET_SETUP_FAILED");
                sheet.SheetFormatVisible = false;
                sheet.SetName("总装配图");
            }
            finally { Release(sheet); }

            front = document.CreateDrawViewFromModelView3(stagedAsmPath, vFront, 0.086, 0.126, 0) as SwView;
            right = document.CreateDrawViewFromModelView3(stagedAsmPath, vRight, 0.160, 0.126, 0) as SwView;
            top = document.CreateDrawViewFromModelView3(stagedAsmPath, vTop, 0.086, 0.222, 0) as SwView;
            iso = document.CreateDrawViewFromModelView3(stagedAsmPath, vIso, 0.228, 0.164, 0) as SwView;
            Need(front != null && right != null && top != null && iso != null, "ASSEMBLY_DRAWING_VIEWS_FAILED");
            front.SetName2("主视图");
            right.SetName2("右视图");
            top.SetName2("俯视图");
            iso.SetName2("等轴测");

            SwView[] views = new[] { front, right, top, iso };
            ResetSheetScaleOneToOnePreservingViews(sw, drawing, views);

            // Compute shared orthographic scale so Front, Top, and Right match 1:N proportion and fit cleanly
            double orthoScale = Math.Min(1.0, Math.Min(0.080 / (cabW * 0.001), Math.Min(0.106 / ((cabH + 70.0) * 0.001), Math.Min(0.044 / (cabD * 0.001), 0.044 / (cabD * 0.001)))));
            FitViewInBox(front, 0.086, 0.126, 0.082, 0.112, orthoScale);
            FitViewInBox(right, 0.160, 0.126, 0.044, 0.112, orthoScale);
            FitViewInBox(top, 0.086, 0.222, 0.082, 0.044, orthoScale);
            FitViewInBox(iso, 0.228, 0.164, 0.076, 0.125, null);
            Need(VariableTrial.ModelForceRebuild3(drawing, false, "AsmDrw.ViewsFitted"), "ASSEMBLY_VIEWS_REBUILD_FAILED");

            // Draw clean A3 border & divider at 1:1
            DrawCleanA3FrameAtOneToOne(sw, drawing, 0.272, trace);

            // Project exact shell envelope into Front, Right, Top for visual dimension callouts
            double[] fShell = ProjectShellBoundsInView(front, shellBoundsMm);
            double[] rShell = ProjectShellBoundsInView(right, shellBoundsMm);
            double[] tShell = ProjectShellBoundsInView(top, shellBoundsMm);
            double[] fOutline = Numbers(front.GetOutline());
            double[] rOutline = Numbers(right.GetOutline());
            double[] tOutline = Numbers(top.GetOutline());
            double[] isoOutline = Numbers(iso.GetOutline());

            double wDimY = Math.Min(fOutline[1], fShell[1]) - 0.006;
            double hDimX = Math.Min(fOutline[0], fShell[0]) - 0.005;
            double dDimY = Math.Min(rOutline[1], rShell[1]) - 0.006;
            double tDimX = Math.Max(tOutline[2], tShell[2]) + 0.005;

            document.EditSheet();
            Need(document.ActivateView(""), "ASM_DIM_SHEET_CTX");
            double[] invSheet = GetSheetToSketchInverseMatrix(document);
            SketchManager mgr = drawing.SketchManager;
            bool prevDb = mgr.AddToDB;
            bool prevDisp = mgr.DisplayWhenAdded;
            document.EditSketch();
            try
            {
                mgr.AddToDB = true;
                mgr.DisplayWhenAdded = false;
                DrawHorizontalSpanCallout(drawing, invSheet, fShell[0], fShell[2], fShell[1], wDimY, true);
                DrawVerticalSpanCallout(drawing, invSheet, fShell[0], hDimX, fShell[1], fShell[3], true);
                DrawHorizontalSpanCallout(drawing, invSheet, rShell[0], rShell[2], rShell[1], dDimY, true);
                DrawVerticalSpanCallout(drawing, invSheet, tShell[2], tDimX, tShell[1], tShell[3], false);
            }
            finally
            {
                mgr.AddToDB = prevDb;
                mgr.DisplayWhenAdded = prevDisp;
                document.EditSheet();
                Release(mgr);
            }

            var notes = new List<object>();
            string orthoRatio = "1:" + (1.0 / orthoScale).ToString("0.#", Invariant);
            string isoRatio = "1:" + (1.0 / iso.ScaleDecimal).ToString("0.#", Invariant);
            notes.Add(NoteAt(sw, drawing, String.Format(Invariant, "Winnsen 16029 全参数智能柜总装配体工程图 | {0}W x {1}H x {2}D mm", FormatMmShort(cabW), FormatMmShort(cabH), FormatMmShort(cabD)), 0.025, 0.281, 0.0038));
            notes.Add(NoteAt(sw, drawing, String.Format(Invariant, "实测外廓: {0}x{1}x{2} mm | 门叶宽: {3} mm | 门格: L{4}/R{5} (共{6}门) | 装机: {7}种/{8}件 | 单位: mm",
                FormatMmShort(cabW), FormatMmShort(cabH), FormatMmShort(cabD), FormatMmShort(leafW), leftDoorCount, rightDoorCount, leftDoorCount + rightDoorCount, uniquePartCount, totalInstanceCount), 0.025, 0.269, 0.0023));

            // Dimension labels + View labels
            notes.Add(NoteAt(sw, drawing, "W=" + FormatMmShort(cabW), 0.5 * (fShell[0] + fShell[2]) - 0.008, wDimY + 0.004, 0.0023));
            notes.Add(NoteAt(sw, drawing, "H=" + FormatMmShort(cabH), Math.Max(0.022, hDimX - 0.013), 0.5 * (fShell[1] + fShell[3]) + 0.003, 0.0023));
            notes.Add(NoteAt(sw, drawing, "D=" + FormatMmShort(cabD), 0.5 * (rShell[0] + rShell[2]) - 0.008, dDimY + 0.004, 0.0023));
            notes.Add(NoteAt(sw, drawing, "D=" + FormatMmShort(cabD) + " ", tDimX + 0.002, 0.5 * (tShell[1] + tShell[3]) + 0.002, 0.0023));

            notes.Add(NoteAt(sw, drawing, "主视图  " + orthoRatio, 0.066, Math.Min(wDimY - 0.005, 0.056), 0.0025));
            notes.Add(NoteAt(sw, drawing, "右视图  " + orthoRatio, 0.142, Math.Min(dDimY - 0.005, 0.056), 0.0025));
            notes.Add(NoteAt(sw, drawing, "俯视图  " + orthoRatio, 0.066, Math.Min(0.254, tOutline[3] + 0.006), 0.0025));
            notes.Add(NoteAt(sw, drawing, "等轴测  " + isoRatio, 0.208, isoOutline[1] - 0.006, 0.0025));

            // Right Specification / Craft Summary Column (0.277..0.405)
            notes.Add(NoteAt(sw, drawing, "整柜规格、门格配置与工艺门禁汇总", 0.277, 0.253, 0.0026));
            string specText = String.Join("\n", new[]
            {
                "[1] 外廓与门格参数 (mm)",
                "  整柜包络: W=" + FormatMmShort(cabW) + " x H=" + FormatMmShort(cabH) + " x D=" + FormatMmShort(cabD),
                "  门叶单宽: " + FormatMmShort(leafW) + " (对称左右双列)",
                "  左列 L" + leftDoorCount + "门: " + leftHeightsText,
                "  右列 R" + rightDoorCount + "门: " + rightHeightsText,
                "  底脚高度: 32mm (安装可调) | 门缝: 7mm",
                "",
                "[2] 装机 BOM 与质量审计",
                "  实际装机: " + uniquePartCount + " 种零件 / " + totalInstanceCount + " 件实例",
                "  钣金下料: 全部装机钣金展开通过 dxf_check",
                "  结构干涉: structuralCollisionCount = 0",
                "  材质校核: missingMaterialCount = 0",
                "",
                "[3] 4 项生产工艺与 Q1-Q4 集成",
                "  C1 防呆缺口: 左门5mm边R1.5半圆 / 右门3x1.5三角",
                "  C2 门销预偏: 上销孔Φ10.2向内预偏0.50mm (n=" + prebiasCount + ")",
                "  C3 层板修补: 横层板L/R取消8处Φ2.0定位孔",
                "  C4 焊接打标: " + markPartCount + "零件/" + markSketchCount + "草图/" + markSegmentCount + "线段3D+DXF",
                "  Q3/Q4 过滤: 成形模具->MARK, 压铆同心内孔已清理",
                "",
                "[4] 交付包结构说明",
                "  3D_CAD/: SolidWorks 2020 全参数装配与零件",
                "  DXF_展开图/: 装机钣金 CUT/BEND/MARK 展开 DXF",
                "  2D_工程图/: 本总装图及各唯一门板 SLDDRW+PDF",
                "  BOM清单: BOM_零件与下料清单.csv / .md"
            });
            notes.Add(NoteAt(sw, drawing, specText, 0.277, 0.244, 0.0020));

            // Title Block
            notes.Add(NoteAt(sw, drawing, "图名: 16029 全参数智能柜总装配图", 0.024, 0.037, 0.0024));
            notes.Add(NoteAt(sw, drawing, "规格: " + FormatMmShort(cabW) + "W_" + FormatMmShort(cabH) + "H_" + FormatMmShort(cabD) + "D_L" + leftDoorCount + "-R" + rightDoorCount, 0.024, 0.019, 0.0022));
            notes.Add(NoteAt(sw, drawing, "材料: 冷轧钢板 + 标准五金件", 0.149, 0.037, 0.0023));
            notes.Add(NoteAt(sw, drawing, "总装: 16029_variable_full_cabinet.SLDASM", 0.149, 0.019, 0.0020));
            notes.Add(NoteAt(sw, drawing, "状态: 工程审图版 (production_release=false)", 0.279, 0.037, 0.0022));
            notes.Add(NoteAt(sw, drawing, "日期: " + DateTime.UtcNow.ToString("yyyy-MM-dd", Invariant) + "   单位: mm   正交比例: " + orthoRatio, 0.279, 0.019, 0.0021));

            SaveDrawingAndExportPdf(sw, drawing, views, notes, stageSlddrw, stagePdf, trace, VariableTrial.HeavyAssemblyComTimeoutMs);

            string outSlddrw = Local(Path.Combine(outRoot, outputStem + ".SLDDRW"));
            string outPdf = Local(Path.Combine(outRoot, outputStem + ".pdf"));
            Directory.CreateDirectory(Path.GetDirectoryName(outSlddrw));
            File.Copy(stageSlddrw, outSlddrw, true);
            File.Copy(stagePdf, outPdf, true);

            return new
            {
                kind = "cabinet_assembly",
                fileStem = outputStem,
                widthMm = Math.Round(cabW, 3),
                heightMm = Math.Round(cabH, 3),
                depthMm = Math.Round(cabD, 3),
                doorLeafWidthMm = Math.Round(leafW, 3),
                orthoScaleRatio = orthoRatio,
                slddrwPath = outSlddrw,
                pdfPath = outPdf,
                slddrwSha256 = Hash(outSlddrw),
                pdfSha256 = Hash(outPdf)
            };
        }
        finally
        {
            Release(iso);
            Release(top);
            Release(right);
            Release(front);
            Release(drawing);
            Release(asmModel);
            VariableTrial.SwCloseAllDocuments(sw, true, "AsmDrw.FinallyClose");
        }
    }

    static string SummarizeDoorHeights(List<double> heights)
    {
        if (heights == null || heights.Count == 0) return "-";
        var distinct = heights.Select(h => Math.Round(h, 2)).Distinct().ToList();
        if (distinct.Count == 1) return FormatMmShort(distinct[0]) + "mm x" + heights.Count;
        return String.Join(", ", heights.Select(FormatMmShort)) + "mm";
    }

    public static object ExportAssemblyEngineeringDrawings(ISldWorks sw, string cadAssemblyDir, string stageDir, string diagDir, string outputDrawingDir)
    {
        VariableTrial.RuntimeOwnership();
        Need(sw != null, "OWNED_CAD_SESSION_REQUIRED");
        string cadDir = Local(cadAssemblyDir);
        Need(Directory.Exists(cadDir), "ASSEMBLY_DRAWING_CAD_DIR_MISSING " + cadDir);
        string runRoot = Path.GetFullPath(VariableTrial.RunDirectory).TrimEnd('\\') + "\\";
        string stage = Local(stageDir);
        string diag = Local(diagDir);
        string outRoot = Local(outputDrawingDir);
        Need(stage.StartsWith(runRoot, StringComparison.OrdinalIgnoreCase) && !stage.StartsWith(Path.GetFullPath(cadDir).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase), "STAGE_MUST_BE_IN_RUN_DIR");
        Need(diag.StartsWith(runRoot, StringComparison.OrdinalIgnoreCase), "DIAG_MUST_BE_IN_RUN_DIR");
        Need(outRoot.StartsWith(runRoot, StringComparison.OrdinalIgnoreCase), "OUTPUT_DRAWING_MUST_BE_IN_RUN_DIR");

        string stageCadDir = Local(Path.Combine(stage, "3D_CAD"));
        string stageDrawingDir = Local(Path.Combine(stage, "2D_工程图"));
        Directory.CreateDirectory(stageDrawingDir);
        Directory.CreateDirectory(diag);
        Directory.CreateDirectory(outRoot);

        string template = VariableTrial.SwGetUserPreferenceStringValue(sw, (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing, "ExportAssemblyEngineeringDrawings");
        Need(!String.IsNullOrWhiteSpace(template) && File.Exists(template), "EXISTING_DEFAULT_DRAWING_TEMPLATE_REQUIRED");

        var beforeHashes = DirectorySha256Map(cadDir);
        CopyDirectoryNormal(cadDir, stageCadDir);
        VariableTrial.RewireCopy(stageCadDir);
        VariableTrial.SwCloseAllDocuments(sw, true, "ExportAssemblyEngineeringDrawings.PreClean");

        bool oldVisible = sw.Visible;
        bool oldUserControl = sw.UserControl;
        bool oldInputDimVal = VariableTrial.SwGetUserPreferenceToggle(sw, (int)swUserPreferenceToggle_e.swInputDimValOnCreate, "ExportDrawings.Init");

        string evidencePath = Path.Combine(cadDir, "assembly-evidence.json");
        Need(File.Exists(evidencePath), "ASSEMBLY_EVIDENCE_REQUIRED " + evidencePath);
        var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 120 };
        var evRoot = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(evidencePath, Encoding.UTF8));

        var measEnv = evRoot["measuredEnvelopeMm"] as IDictionary<string, object>;
        double cabW = Convert.ToDouble(measEnv["widthMm"], Invariant);
        double cabH = Convert.ToDouble(measEnv["heightMm"], Invariant);
        double cabD = Convert.ToDouble(measEnv["depthMm"], Invariant);
        double[] shellBounds = Items(evRoot["cabinetShellBoundsMm"]).Select(v => Convert.ToDouble(v, Invariant)).ToArray();
        Need(shellBounds.Length == 6, "CABINET_SHELL_BOUNDS_REQUIRED");

        var layoutMap = evRoot["layout"] as IDictionary<string, object>;
        double leafW = Convert.ToDouble(layoutMap["doorLeafWidthMm"], Invariant);
        var rowsByCol = layoutMap["rowsByColumn"] as IDictionary<string, object>;
        var leftHeights = Items(rowsByCol["L"]).Cast<IDictionary<string, object>>().Select(r => Convert.ToDouble(r["doorHeightMm"], Invariant)).ToList();
        var rightHeights = Items(rowsByCol["R"]).Cast<IDictionary<string, object>>().Select(r => Convert.ToDouble(r["doorHeightMm"], Invariant)).ToList();

        var ic = evRoot["interferenceClassification"] as IDictionary<string, object>;
        int prebiasCount = ic != null && ic.ContainsKey("approvedUpperPinPrebiasCount") ? Convert.ToInt32(ic["approvedUpperPinPrebiasCount"], Invariant) : 0;

        var pm = evRoot.ContainsKey("productionMarks") ? evRoot["productionMarks"] as IDictionary<string, object> : null;
        int markPartCount = pm != null && pm.ContainsKey("partFileCount") ? Convert.ToInt32(pm["partFileCount"], Invariant) : 0;
        int markSketchCount = pm != null && pm.ContainsKey("totalSketchCount") ? Convert.ToInt32(pm["totalSketchCount"], Invariant) : 0;
        int markSegmentCount = pm != null && pm.ContainsKey("totalSegmentCount") ? Convert.ToInt32(pm["totalSegmentCount"], Invariant) : 0;

        string stagePrefix = Path.GetFullPath(stageCadDir).TrimEnd('\\') + "\\";
        string[] stagedParts = Directory.GetFiles(stagePrefix, "*.sldprt", SearchOption.AllDirectories)
            .OrderBy(v => v.Substring(stagePrefix.Length), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stagedByRel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pf in stagedParts) stagedByRel[pf.Substring(stagePrefix.Length)] = pf.Substring(stagePrefix.Length);

        var installedQty = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalInstanceCount = 0;
        foreach (object entryObj in Items(evRoot["expectedManifest"]))
        {
            var dict = entryObj as IDictionary<string, object>;
            if (dict == null || !dict.ContainsKey("path")) continue;
            totalInstanceCount++;
            string rel = ResolveInstalledRelPath(Convert.ToString(dict["path"], Invariant), stagedByRel);
            if (!String.IsNullOrEmpty(rel))
                installedQty[rel] = installedQty.ContainsKey(rel) ? installedQty[rel] + 1 : 1;
        }

        string[] doorRels = installedQty.Keys
            .Where(r => r.StartsWith("parts\\", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileNameWithoutExtension(r).EndsWith("_panel", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => Path.GetFileName(r).StartsWith("L_", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Need(doorRels.Length > 0, "NO_INSTALLED_DOOR_PANELS_FOUND_FOR_DRAWING");

        string specLabel = String.Format(Invariant, "{0}W_{1}H_{2}D_L{3}-R{4}", FormatMmShort(cabW), FormatMmShort(cabH), FormatMmShort(cabD), leftHeights.Count, rightHeights.Count);
        var drawingReports = new List<object>();

        try
        {
            VariableTrial.SwSetUserPreferenceToggle(sw, (int)swUserPreferenceToggle_e.swInputDimValOnCreate, false, "ExportDrawings.DisableDimPrompt");
            VariableTrial.SwSetVisible(sw, true, "ExportDrawings.ShowSw");

            // 1. Generate each unique Door Panel engineering drawing first (which also saves 默认SM-FLAT-PATTERN into staged door parts)
            int ordinal = 1;
            foreach (string doorRel in doorRels)
            {
                string doorFull = Local(Path.Combine(stageCadDir, doorRel));
                object doorRep = BuildDoorPanelEngineeringDrawing(
                    sw,
                    doorFull,
                    doorRel,
                    installedQty[doorRel],
                    specLabel,
                    stageDrawingDir,
                    outRoot,
                    diag,
                    template,
                    ordinal++);
                drawingReports.Add(doorRep);
            }

            // 2. Generate Full Cabinet Assembly engineering drawing
            string stagedAsmPath = Local(Path.Combine(stageCadDir, "16029_variable_full_cabinet.SLDASM"));
            Need(File.Exists(stagedAsmPath), "STAGED_FULL_CABINET_ASM_MISSING");
            object asmRep = BuildCabinetAssemblyDrawing(
                sw,
                stagedAsmPath,
                stageDrawingDir,
                outRoot,
                diag,
                template,
                cabW, cabH, cabD, leafW,
                shellBounds,
                leftHeights.Count, rightHeights.Count,
                SummarizeDoorHeights(leftHeights),
                SummarizeDoorHeights(rightHeights),
                installedQty.Count,
                totalInstanceCount,
                0,
                prebiasCount,
                markPartCount,
                markSketchCount,
                markSegmentCount);
            drawingReports.Insert(0, asmRep);
        }
        finally
        {
            try
            {
                VariableTrial.SwCloseAllDocuments(sw, true, "ExportDrawings.FinallyClose");
                VariableTrial.SwSetUserPreferenceToggle(sw, (int)swUserPreferenceToggle_e.swInputDimValOnCreate, oldInputDimVal, "ExportDrawings.RestoreDimPrompt", true);
                VariableTrial.SwSetUserControl(sw, oldUserControl, "ExportDrawings.RestoreUC");
                VariableTrial.SwSetVisible(sw, oldVisible, "ExportDrawings.RestoreVis");
            }
            catch { }
            VariableTrial.ClearOwned();
        }

        Need(VariableTrial.SwGetDocumentCount(sw, "ExportDrawings.VerifyEmpty") == 0, "DRAWING_EXPORT_DOCUMENTS_REMAIN_OPEN");
        var afterHashes = DirectorySha256Map(cadDir);
        bool hashesUnchanged = beforeHashes.Count == afterHashes.Count && beforeHashes.All(kv => afterHashes.ContainsKey(kv.Key) && afterHashes[kv.Key] == kv.Value);
        Need(hashesUnchanged, "INPUT_CAD_CHANGED_DURING_ASSEMBLY_DRAWING_EXPORT");

        string[] slddrwFiles = Directory.GetFiles(outRoot, "*.SLDDRW", SearchOption.TopDirectoryOnly).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] pdfFiles = Directory.GetFiles(outRoot, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
        Need(slddrwFiles.Length == drawingReports.Count && pdfFiles.Length == drawingReports.Count, "DRAWING_OUTPUT_COUNT_MISMATCH");

        return new
        {
            cadAssemblyDir = cadDir,
            stageDir = stage,
            diagnosticDir = diag,
            drawingOutputDir = outRoot,
            drawingCount = drawingReports.Count,
            slddrwFileCount = slddrwFiles.Length,
            pdfFileCount = pdfFiles.Length,
            inputHashesUnchanged = true,
            drawings = drawingReports
        };
    }
}
