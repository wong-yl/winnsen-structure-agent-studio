using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private const string PMPrefix = "生产打标_焊接定位_";
        private const double PMTolerance = 1e-7;
        private const double PMMaterialTolerance = 1e-8;
        private const double PMShortLeg = .003;
        private const double PMSideEnd = .050;
        private const double PMPartitionStandOff = .0005;
        private const double PMSideStandOff = .0002;

        private sealed class PMInstance
        {
            internal Component2 component;
            internal ModelDoc2 model;
            internal string path, configuration, name, parent, kind;
            internal MathTransform transform, inverse;
            internal double[] transformData, inverseData;
            internal List<Body2> bodies;
        }

        private sealed class PMSegment
        {
            public string kind, basis;
            public int axis;
            public double[] a, b, center, mid;
            public double radius, sweep, length;
            public bool construction;
        }

        private sealed class PMPlane
        {
            internal int axis, normalSign;
            internal double value;
            internal bool boundaryRead;
            internal List<Face2> faces = new List<Face2>();
            internal List<PMSegment> boundary = new List<PMSegment>();
        }

        private sealed class PMContact
        {
            internal PMInstance source, target;
            internal PMPlane plane;
            internal double[] exactBounds;
            internal double projectionOffset, sourcePlane;
            internal int sourceNormalSign;
            internal List<PMSegment> outline = new List<PMSegment>();
            internal List<PMSegment> originalOutline = new List<PMSegment>();
            internal List<PMSegment> materialOutline = new List<PMSegment>();
            internal List<object> faces = new List<object>();
        }

        private sealed class PMPlan
        {
            internal PMInstance target;
            internal PMPlane plane;
            internal string family, name;
            internal List<PMSegment> expected = new List<PMSegment>();
            internal List<object> provenance = new List<object>();
            internal List<object> materialChecks;
        }

        private sealed class PMSolid
        {
            public string geometrySha256;
            public double volumeM3;
            public double[] exactBoundsM;
            public int bodies, vertices, edges, faces;
        }

        private static void PMNeed(bool condition, string reason)
        {
            ParametricContext.Require(condition, "PRODUCTION_MARKS_" + reason);
        }

        private static double[] PMPoint(double[] p, double[] t)
        {
            PMNeed(p != null && p.Length >= 3 && t != null && t.Length >= 13, "TRANSFORM_DATA_INVALID");
            return new[] {
                (p[0] * t[0] + p[1] * t[3] + p[2] * t[6]) * t[12] + t[9],
                (p[0] * t[1] + p[1] * t[4] + p[2] * t[7]) * t[12] + t[10],
                (p[0] * t[2] + p[1] * t[5] + p[2] * t[8]) * t[12] + t[11]
            };
        }

        private static double[] PMVector(double[] p, double[] t)
        {
            double[] origin = PMPoint(new double[3], t), end = PMPoint(p, t);
            return end.Select((v, i) => v - origin[i]).ToArray();
        }

        private static double PMDot(double[] a, double[] b) { return a.Select((v, i) => v * b[i]).Sum(); }
        private static double PMDistance(double[] a, double[] b) { return Math.Sqrt(a.Select((v, i) => (v - b[i]) * (v - b[i])).Sum()); }
        private static bool PMNear(double[] a, double[] b) { return a != null && b != null && PMDistance(a, b) <= PMTolerance; }
        private static double[] PMAt(double[] a, double[] b, double t) { return a.Select((v, i) => v + t * (b[i] - v)).ToArray(); }
        private static int[] PMAxes(int normal) { return Enumerable.Range(0, 3).Where(i => i != normal).ToArray(); }
        private static double PMAngle(double angle) { double t = angle % (2 * Math.PI); return t < 0 ? t + 2 * Math.PI : t; }

        private static string PMHash(string value)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToUpperInvariant();
        }

        private static string PMQuant(IEnumerable<double> values)
        {
            return String.Join(",", values.Select(value => Math.Round(value / 1e-9, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)).ToArray());
        }

        private static string PMKind(Component2 component)
        {
            string file = Path.GetFileName(component.GetPathName());
            string name = component.Name2.Split('/').Last();
            if (file.StartsWith("door_panel_", StringComparison.Ordinal)) return "door";
            if (file.StartsWith("door_stiffener_", StringComparison.Ordinal)) return "door_rib";
            if (name.StartsWith("hinge_latch_plate", StringComparison.Ordinal) || file.StartsWith("hinge_latch_plate", StringComparison.Ordinal) || file.StartsWith("top_latch_", StringComparison.Ordinal)) return "door_contact";
            if (name.StartsWith("u_hook_pad", StringComparison.Ordinal) || file.StartsWith("u_hook_pad", StringComparison.Ordinal)) return "door_contact";
            if (file.Equals("箱体横层板L.sldprt", StringComparison.OrdinalIgnoreCase) || file.Equals("箱体横层板R.SLDPRT", StringComparison.OrdinalIgnoreCase)) return "shelf";
            if (file.Equals("箱体横层板加强筋.SLDPRT", StringComparison.OrdinalIgnoreCase)) return "shelf_rib";
            if (file.Equals("箱体左侧板.sldprt", StringComparison.OrdinalIgnoreCase) || file.Equals("箱体右侧板.sldprt", StringComparison.OrdinalIgnoreCase)) return "side";
            if (file.Equals("箱体侧板加强筋1.sldprt", StringComparison.OrdinalIgnoreCase)) return "side_rib";
            if (file.Equals("箱体竖隔板L.sldprt", StringComparison.OrdinalIgnoreCase) || file.Equals("箱体竖隔板R.SLDPRT", StringComparison.OrdinalIgnoreCase)) return "partition";
            if (file.Equals("箱体竖隔板加强件.sldprt", StringComparison.OrdinalIgnoreCase)) return "partition_rib";
            return "";
        }

        private static List<PMInstance> PMInstances(ModelDoc2 root)
        {
            PMNeed(root is AssemblyDoc, "FINAL_ROOT_NOT_ASSEMBLY");
            ParametricContext.CheckPath(root.GetPathName(), ParametricContext.CadDirectory);
            var result = new List<PMInstance>();
            foreach (Component2 component in ((AssemblyDoc)root).GetComponents(false) as Array ?? new object[0])
            {
                if (component.IsSuppressed()) continue;
                string kind = PMKind(component);
                if (kind.Length == 0) continue;
                string path = Path.GetFullPath(component.GetPathName());
                ParametricContext.CheckPath(path, ParametricContext.CadDirectory);
                PMNeed(File.Exists(path) && Path.GetExtension(path).Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase), "NATIVE_PART_UNAVAILABLE " + path);
                ModelDoc2 model = component.GetModelDoc2() as ModelDoc2;
                PMNeed(model is PartDoc && String.Equals(Path.GetFullPath(model.GetPathName()), path, StringComparison.OrdinalIgnoreCase), "UNRESOLVED_COMPONENT " + component.Name2);
                MathTransform transform = component.GetTotalTransform(false);
                PMNeed(transform != null, "COMPONENT_TRANSFORM_UNAVAILABLE " + component.Name2);
                MathTransform inverse = transform.IInverse();
                double[] data = transform.ArrayData as double[], inverseData = inverse.ArrayData as double[];
                PMNeed(data != null && data.Length >= 13 && data.All(v => !Double.IsNaN(v) && !Double.IsInfinity(v)) && Math.Abs(data[12] - 1) < 1e-10, "NONRIGID_TRANSFORM " + component.Name2);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        PMNeed(Math.Abs(Enumerable.Range(0, 3).Sum(k => data[i * 3 + k] * data[j * 3 + k]) - (i == j ? 1 : 0)) < 1e-9, "TRANSFORM_NOT_ORTHONORMAL " + component.Name2);
                object info;
                Array raw = component.GetBodies3((int)swBodyType_e.swSolidBody, out info) as Array;
                PMNeed(raw != null && raw.Length == 1, "EXPECTED_SINGLE_SOLID " + component.Name2);
                var bodies = raw.Cast<Body2>().ToList();
                PMNeed(bodies.All(body => body.Check2() == 0), "INVALID_SOLID " + component.Name2);
                Component2 parent = component.GetParent();
                result.Add(new PMInstance { component = component, model = model, path = path, name = component.Name2,
                    configuration = component.ReferencedConfiguration, parent = parent == null ? "" : parent.Name2,
                    kind = kind, transform = transform, inverse = inverse, transformData = data, inverseData = inverseData, bodies = bodies });
            }
            PMNeed(result.Count > 0 && result.Any(p => p.kind == "door") && result.Any(p => p.kind == "shelf") && result.Any(p => p.kind == "side") && result.Any(p => p.kind == "partition"), "REQUIRED_PART_FAMILY_MISSING");
            return result;
        }

        private static double[] PMBounds(IEnumerable<Body2> bodies, PMInstance source, PMInstance target)
        {
            var points = new List<double[]>();
            foreach (Body2 original in bodies)
            {
                Body2 body = original.Copy() as Body2;
                try
                {
                    PMNeed(body != null, "BODY_COPY_FAILED");
                    if (source != null) PMNeed(body.ApplyTransform(source.transform), "SOURCE_TRANSFORM_FAILED " + source.name);
                    if (target != null) PMNeed(body.ApplyTransform(target.inverse), "TARGET_TRANSFORM_FAILED " + target.name);
                    for (int axis = 0; axis < 3; axis++) foreach (double sign in new[] { -1.0, 1.0 })
                    {
                        double x, y, z;
                        PMNeed(body.GetExtremePoint(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0, out x, out y, out z), "EXACT_EXTREME_POINT_FAILED");
                        points.Add(new[] { x, y, z });
                    }
                }
                finally { if (body != null) Marshal.ReleaseComObject(body); }
            }
            PMNeed(points.Count > 0, "NO_SOLID_EXTREMA");
            return Enumerable.Range(0, 6).Select(i => i < 3 ? points.Min(p => p[i]) : points.Max(p => p[i - 3])).ToArray();
        }

        private static PMSegment PMLine(double[] a, double[] b, int axis, string basis)
        {
            PMNeed(PMDistance(a, b) > 1e-9 && Math.Abs(a[axis] - b[axis]) < PMTolerance, "DEGENERATE_LINE");
            return new PMSegment { kind = "line", a = a, b = b, axis = axis, length = PMDistance(a, b), basis = basis };
        }

        private static PMSegment PMArc(double[] a, double[] b, double[] center, double[] mid, int axis, string basis)
        {
            int[] axes = PMAxes(axis);
            double radius = PMDistance(a, center);
            PMNeed(radius > 1e-9 && Math.Abs(PMDistance(b, center) - radius) < PMTolerance && Math.Abs(PMDistance(mid, center) - radius) < PMTolerance, "ARC_RADIUS_MISMATCH");
            double start = Math.Atan2(a[axes[1]] - center[axes[1]], a[axes[0]] - center[axes[0]]);
            double end = Math.Atan2(b[axes[1]] - center[axes[1]], b[axes[0]] - center[axes[0]]);
            double middle = Math.Atan2(mid[axes[1]] - center[axes[1]], mid[axes[0]] - center[axes[0]]);
            double sweep = PMNear(a, b) ? 2 * Math.PI : PMAngle(end - start);
            if (!PMNear(a, b) && PMAngle(middle - start) > sweep + 1e-9) sweep -= 2 * Math.PI;
            PMNeed(Math.Abs(sweep) > 1e-9, "ARC_SWEEP_UNAVAILABLE");
            return new PMSegment { kind = "arc", a = a, b = b, center = center, mid = mid, axis = axis, radius = radius, sweep = sweep, length = Math.Abs(sweep) * radius, basis = basis };
        }

        private static double[] PMOn(PMSegment segment, double t)
        {
            if (segment.kind == "line") return PMAt(segment.a, segment.b, t);
            int[] axes = PMAxes(segment.axis);
            double start = Math.Atan2(segment.a[axes[1]] - segment.center[axes[1]], segment.a[axes[0]] - segment.center[axes[0]]);
            double[] point = (double[])segment.center.Clone();
            point[axes[0]] += segment.radius * Math.Cos(start + t * segment.sweep);
            point[axes[1]] += segment.radius * Math.Sin(start + t * segment.sweep);
            return point;
        }

        private static double PMParameter(PMSegment segment, double[] point)
        {
            if (segment.kind == "line")
            {
                double[] delta = segment.b.Select((v, i) => v - segment.a[i]).ToArray();
                return PMDot(point.Select((v, i) => v - segment.a[i]).ToArray(), delta) / PMDot(delta, delta);
            }
            int[] axes = PMAxes(segment.axis);
            double start = Math.Atan2(segment.a[axes[1]] - segment.center[axes[1]], segment.a[axes[0]] - segment.center[axes[0]]);
            double angle = Math.Atan2(point[axes[1]] - segment.center[axes[1]], point[axes[0]] - segment.center[axes[0]]);
            return (segment.sweep > 0 ? PMAngle(angle - start) : -PMAngle(start - angle)) / segment.sweep;
        }

        private static bool PMContains(PMSegment segment, double[] point)
        {
            double t = PMParameter(segment, point);
            return t >= -1e-8 && t <= 1 + 1e-8 && PMDistance(PMOn(segment, Math.Max(0, Math.Min(1, t))), point) <= PMTolerance;
        }

        private static PMSegment PMTrim(PMSegment original, double start, double end)
        {
            PMSegment result = original.kind == "line" ? PMLine(PMOn(original, start), PMOn(original, end), original.axis, original.basis)
                : PMArc(PMOn(original, start), PMOn(original, end), original.center, PMOn(original, (start + end) / 2), original.axis, original.basis);
            result.construction = original.construction;
            return result;
        }

        private static List<PMSegment> PMReadLoop(Face2 face, bool outerOnly, int axis, Func<double[], double[]> point, string context)
        {
            var result = new List<PMSegment>();
            Array loops = face.GetLoops() as Array;
            PMNeed(loops != null && loops.Length > 0, "FACE_LOOP_UNAVAILABLE");
            foreach (Loop2 loop in loops)
            {
                if (outerOnly && !loop.IsOuter()) continue;
                Array edges = loop.GetEdges() as Array;
                PMNeed(edges != null && edges.Length > 0, "LOOP_EDGE_UNAVAILABLE");
                foreach (Edge edge in edges)
                {
                    Curve curve = edge.GetCurve() as Curve;
                    double[] parameters = edge.GetCurveParams2() as double[];
                    PMNeed(curve != null && parameters != null && parameters.Length >= 8, "EDGE_PARAMETERS_UNAVAILABLE");
                    double[] a = point(parameters.Take(3).ToArray()), b = point(parameters.Skip(3).Take(3).ToArray());
                    PMNeed(Math.Abs(a[axis] - b[axis]) <= PMTolerance, "OUTLINE_NOT_PLANAR");
                    if (curve.IsLine()) result.Add(PMLine(a, b, axis, "actual planar contact outer-loop edge"));
                    else
                    {
                        if (!curve.IsCircle())
                        {
                            Feature owner = face.GetFeature() as Feature;
                            var samples = Enumerable.Range(0, 9).Select(index => {
                                double parameter = parameters[6] + (parameters[7] - parameters[6]) * index / 8;
                                return new { parameter = parameter, evaluated = curve.Evaluate2(parameter, 2) };
                            }).ToArray();
                            PMNeed(false, "UNSUPPORTED_CONTACT_CURVE " + ParametricContext.Json.Serialize(new {
                                context = context, axis = axis, outer = loop.IsOuter(), owner = owner == null ? "" : owner.Name,
                                curveType = curve.Identity(), parameters = parameters, samples = samples,
                                lengthM = curve.GetLength3(parameters[6], parameters[7]), faceBoxM = face.GetBox(), faceNormal = face.Normal
                            }));
                        }
                        double[] circle = curve.CircleParams as double[], evaluated = curve.Evaluate2((parameters[6] + parameters[7]) / 2, 0) as double[];
                        PMNeed(circle != null && circle.Length >= 7 && evaluated != null && evaluated.Length >= 3, "CONTACT_ARC_UNAVAILABLE");
                        result.Add(PMArc(a, b, point(circle.Take(3).ToArray()), point(evaluated.Take(3).ToArray()), axis, "actual planar contact outer-loop arc"));
                    }
                }
            }
            PMNeed(result.Count > 0, "NO_OUTER_LOOP_GEOMETRY");
            return result;
        }

        private static List<PMPlane> PMPlanes(PMInstance part, int axis)
        {
            var result = new List<PMPlane>();
            foreach (Face2 face in part.bodies.SelectMany(body => (body.GetFaces() as Array ?? new object[0]).Cast<Face2>()))
            {
                Surface surface = face.GetSurface() as Surface;
                if (surface == null || !surface.IsPlane()) continue;
                double[] normal = face.Normal as double[], plane = surface.PlaneParams as double[];
                PMNeed(normal != null && normal.Length == 3 && plane != null && plane.Length >= 6, "PLANE_DATA_UNAVAILABLE");
                if (Math.Abs(normal[axis]) < 1 - 1e-9) continue;
                int sign = Math.Sign(normal[axis]);
                PMPlane group = result.FirstOrDefault(p => p.normalSign == sign && Math.Abs(p.value - plane[axis + 3]) <= PMMaterialTolerance);
                if (group == null) { group = new PMPlane { axis = axis, normalSign = sign, value = plane[axis + 3] }; result.Add(group); }
                group.faces.Add(face);
            }
            PMNeed(result.Count > 0, "SUBSTRATE_PLANAR_FACE_UNAVAILABLE " + part.name);
            return result;
        }

        private static void PMReadPlaneBoundary(PMPlane plane, string context)
        {
            if (plane.boundaryRead) return;
            foreach (Face2 face in plane.faces)
                plane.boundary.AddRange(PMReadLoop(face, false, plane.axis, p => p, context));
            plane.boundaryRead = true;
        }

        private static double PMMaterialDistance(PMPlane plane, double[] point)
        {
            return plane.faces.Min(face => {
                double[] closest = face.GetClosestPointOn(point[0], point[1], point[2]) as double[];
                PMNeed(closest != null && closest.Length >= 3, "CLOSEST_MATERIAL_POINT_UNAVAILABLE");
                return PMDistance(point, closest.Take(3).ToArray());
            });
        }

        private static List<double[]> PMIntersections(PMSegment first, PMSegment second)
        {
            var result = new List<double[]>();
            int[] axes = PMAxes(first.axis);
            if (first.kind == "line" && second.kind == "line")
            {
                double ux = first.b[axes[0]] - first.a[axes[0]], uy = first.b[axes[1]] - first.a[axes[1]];
                double vx = second.b[axes[0]] - second.a[axes[0]], vy = second.b[axes[1]] - second.a[axes[1]];
                double dx = second.a[axes[0]] - first.a[axes[0]], dy = second.a[axes[1]] - first.a[axes[1]];
                double cross = ux * vy - uy * vx;
                if (Math.Abs(cross) > 1e-16) result.Add(PMOn(first, (dx * vy - dy * vx) / cross));
                else result.AddRange(new[] { first.a, first.b, second.a, second.b });
            }
            else if (first.kind == "line" || second.kind == "line")
            {
                PMSegment line = first.kind == "line" ? first : second, arc = first.kind == "arc" ? first : second;
                double[] direction = line.b.Select((v, i) => v - line.a[i]).ToArray(), offset = line.a.Select((v, i) => v - arc.center[i]).ToArray();
                double a = PMDot(direction, direction), b = 2 * PMDot(direction, offset), c = PMDot(offset, offset) - arc.radius * arc.radius;
                double discriminant = b * b - 4 * a * c;
                if (discriminant >= -1e-20)
                    foreach (double sign in new[] { -1.0, 1.0 }) result.Add(PMOn(line, (-b + sign * Math.Sqrt(Math.Max(0, discriminant))) / (2 * a)));
            }
            else
            {
                double distance = PMDistance(first.center, second.center);
                if (distance <= 1e-12 && Math.Abs(first.radius - second.radius) <= PMTolerance) result.AddRange(new[] { first.a, first.b, second.a, second.b });
                else if (distance > 1e-12 && distance <= first.radius + second.radius + PMTolerance && distance >= Math.Abs(first.radius - second.radius) - PMTolerance)
                {
                    double along = (first.radius * first.radius - second.radius * second.radius + distance * distance) / (2 * distance);
                    double height = Math.Sqrt(Math.Max(0, first.radius * first.radius - along * along));
                    double dx = (second.center[axes[0]] - first.center[axes[0]]) / distance, dy = (second.center[axes[1]] - first.center[axes[1]]) / distance;
                    foreach (double sign in new[] { -1.0, 1.0 })
                    {
                        double[] p = (double[])first.center.Clone();
                        p[axes[0]] += along * dx - sign * height * dy;
                        p[axes[1]] += along * dy + sign * height * dx;
                        result.Add(p);
                    }
                }
            }
            return result.Where(p => PMContains(first, p) && PMContains(second, p)).ToList();
        }

        private static List<double> PMCuts(IEnumerable<double> values)
        {
            var result = new List<double>();
            foreach (double value in values.Where(v => v >= -1e-9 && v <= 1 + 1e-9).Select(v => Math.Max(0, Math.Min(1, v))).OrderBy(v => v))
                if (result.Count == 0 || value - result.Last() > 1e-9) result.Add(value);
            return result;
        }

        private static List<PMSegment> PMClipMaterial(PMSegment segment, PMPlane plane)
        {
            var cuts = new List<double> { 0, 1 };
            foreach (PMSegment boundary in plane.boundary)
                cuts.AddRange(PMIntersections(segment, boundary).Select(p => PMParameter(segment, p)));
            List<double> sorted = PMCuts(cuts);
            var result = new List<PMSegment>();
            for (int i = 0; i + 1 < sorted.Count; i++)
            {
                double start = sorted[i], end = sorted[i + 1];
                if ((end - start) * segment.length <= 1e-9) continue;
                if (PMMaterialDistance(plane, PMOn(segment, (start + end) / 2)) <= PMMaterialTolerance)
                    result.Add(PMTrim(segment, start, end));
            }
            return result;
        }

        private static bool PMSame(PMSegment a, PMSegment b)
        {
            if (a.kind != b.kind || a.construction != b.construction || Math.Abs(a.length - b.length) > PMTolerance) return false;
            bool ends = (PMNear(a.a, b.a) && PMNear(a.b, b.b)) || (PMNear(a.a, b.b) && PMNear(a.b, b.a));
            return ends && (a.kind == "line" || (PMNear(a.center, b.center) && PMNear(PMOn(a, .5), PMOn(b, .5)) && Math.Abs(a.radius - b.radius) <= PMTolerance));
        }

        private static List<PMSegment> PMUnique(IEnumerable<PMSegment> segments)
        {
            var result = new List<PMSegment>();
            foreach (PMSegment segment in segments) if (!result.Any(prior => PMSame(prior, segment))) result.Add(segment);
            return result;
        }

        private static void PMSameGeometry(List<PMSegment> expected, List<PMSegment> actual, string context)
        {
            PMNeed(expected.Count == actual.Count, "SEGMENT_COUNT_MISMATCH " + context);
            var remaining = new List<PMSegment>(actual);
            foreach (PMSegment segment in expected)
            {
                int index = remaining.FindIndex(candidate => PMSame(segment, candidate));
                PMNeed(index >= 0, "SEGMENT_GEOMETRY_MISMATCH " + context);
                remaining.RemoveAt(index);
            }
        }

        private static List<PMContact> PMContacts(PMInstance source, PMInstance target, List<PMPlane> planes)
        {
            var result = new List<PMContact>();
            int axis = planes[0].axis;
            bool projectedPartition = source.kind == "partition_rib" && target.kind == "partition";
            bool projectedSide = source.kind == "side_rib" && (target.kind == "side" || target.kind == "partition");
            double standOff = projectedPartition ? PMPartitionStandOff : projectedSide ? PMSideStandOff : 0;
            PMNeed(!projectedPartition || axis == 0, "PARTITION_PROJECTION_AXIS_INVALID");
            PMNeed(!projectedSide || (axis == 0 || axis == 2) && (target.kind != "partition" || axis == 0), "SIDE_PROJECTION_AXIS_INVALID");
            double[] exactBounds = PMBounds(source.bodies, source, target);
            Func<double[], double[]> intoTarget = p => PMPoint(PMPoint(p, source.transformData), target.inverseData);
            foreach (Face2 face in source.bodies.SelectMany(body => (body.GetFaces() as Array ?? new object[0]).Cast<Face2>()))
            {
                Surface surface = face.GetSurface() as Surface;
                if (surface == null || !surface.IsPlane()) continue;
                double[] normal = PMVector(PMVector(face.Normal as double[], source.transformData), target.inverseData);
                if (Math.Abs(normal[axis]) < 1 - 1e-9) continue;
                double[] parameters = surface.PlaneParams as double[];
                PMNeed(parameters != null && parameters.Length >= 6, "SOURCE_PLANE_UNAVAILABLE");
                double[] point = intoTarget(parameters.Skip(3).Take(3).ToArray());
                PMPlane plane = planes.SingleOrDefault(p =>
                    Math.Abs((p.value - point[axis]) * normal[axis] - standOff) <= PMTolerance &&
                    p.faces.All(f => PMDot(normal, f.Normal as double[]) < -1 + 1e-9));
                if (plane == null) continue;
                PMNeed(standOff == 0 || Math.Abs(point[axis] - exactBounds[axis + (normal[axis] > 0 ? 3 : 0)]) <= PMTolerance,
                    "PROJECTION_SOURCE_NOT_OUTER_EXTREME " + source.name);
                double projectionOffset = plane.value - point[axis];
                PMReadPlaneBoundary(plane, target.name + " | " + target.path);
                List<PMSegment> original = PMReadLoop(face, true, axis, intoTarget, source.name + " -> " + target.name);
                PMNeed(original.All(segment => Math.Abs(segment.a[axis] - point[axis]) <= PMTolerance && Math.Abs(segment.b[axis] - point[axis]) <= PMTolerance), "SOURCE_PLANE_MISMATCH");
                Func<double[], double[]> projectedPoint = value => { double[] moved = (double[])value.Clone(); moved[axis] = plane.value; return moved; };
                List<PMSegment> outline = original.Select(segment => segment.kind == "line" ?
                    PMLine(projectedPoint(segment.a), projectedPoint(segment.b), axis, segment.basis) :
                    PMArc(projectedPoint(segment.a), projectedPoint(segment.b), projectedPoint(segment.center), projectedPoint(segment.mid), axis, segment.basis)).ToList();
                var clipped = outline.SelectMany(segment => PMClipMaterial(segment, plane)).ToList();
                if (clipped.Count == 0) continue;
                object witness = PMContactWitness(face, source, target, plane, outline, projectionOffset);
                PMNeed(witness != null, "CONTACT_HAS_NO_POSITIVE_MATERIAL_AREA " + source.name + " -> " + target.name);
                PMContact contact = result.FirstOrDefault(c => c.plane == plane);
                if (contact == null)
                {
                    contact = new PMContact { source = source, target = target, plane = plane, projectionOffset = projectionOffset,
                        sourcePlane = point[axis], sourceNormalSign = Math.Sign(normal[axis]), exactBounds = exactBounds };
                    result.Add(contact);
                }
                PMNeed(Math.Abs(contact.projectionOffset - projectionOffset) <= PMTolerance, "MIXED_CONTACT_PROJECTION_OFFSETS");
                PMNeed(contact.sourceNormalSign == Math.Sign(normal[axis]), "MIXED_SOURCE_NORMAL_SIGNS");
                contact.originalOutline.AddRange(original); contact.outline.AddRange(outline); contact.materialOutline.AddRange(clipped);
                contact.faces.Add(new { sourceAreaM2 = face.GetArea(), sourceNormalInTarget = normal, sourcePlanePointInTargetM = point,
                    sourceOuterLoop = original, projectedOuterLoop = outline, normalProjectionOffsetM = projectionOffset,
                    relation = projectedPartition ? "approved_0p5mm_partition_standoff_projection" : projectedSide ? "approved_0p2mm_vertical_rib_standoff_projection" : "exact_planar_contact",
                    actualContact = standOff == 0, nominalStandOffMm = standOff * 1000,
                    signedNormalGapM = projectionOffset * normal[axis], targetNormalSign = plane.normalSign,
                    overlapAreaWitness = witness });
            }
            foreach (PMContact contact in result)
            {
                contact.outline = PMUnique(contact.outline);
                contact.originalOutline = PMUnique(contact.originalOutline);
                contact.materialOutline = PMUnique(contact.materialOutline);
            }
            return result;
        }

        private static double PMDistanceToSegment(PMSegment segment, double[] point)
        {
            double t = PMParameter(segment, point);
            if (segment.kind == "line") return PMDistance(point, PMOn(segment, Math.Max(0, Math.Min(1, t))));
            if (t >= 0 && t <= 1) return PMDistance(point, PMOn(segment, t));
            return Math.Min(PMDistance(point, segment.a), PMDistance(point, segment.b));
        }

        private static object PMContactWitness(Face2 sourceFace, PMInstance source, PMInstance target, PMPlane plane, List<PMSegment> outline, double projectionOffset)
        {
            int[] axes = PMAxes(plane.axis);
            foreach (PMSegment edge in outline) foreach (double fraction in new[] { .5, .25, .75 })
            {
                double[] middle = PMOn(edge, fraction);
                double[] before = PMOn(edge, Math.Max(0, fraction - .0001)), after = PMOn(edge, Math.Min(1, fraction + .0001));
                double dx = after[axes[0]] - before[axes[0]], dy = after[axes[1]] - before[axes[1]];
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 1e-12) continue;
                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    double offset = Math.Min(.0001, edge.length / 8);
                    double[] p = (double[])middle.Clone();
                    p[axes[0]] -= sign * dy / length * offset; p[axes[1]] += sign * dx / length * offset;
                    if (plane.boundary.Any(boundary => PMDistanceToSegment(boundary, p) <= 10 * PMTolerance)) continue;
                    double[] sourcePointInTarget = (double[])p.Clone(); sourcePointInTarget[plane.axis] -= projectionOffset;
                    double[] local = PMPoint(PMPoint(sourcePointInTarget, target.transformData), source.inverseData);
                    double[] closest = sourceFace.GetClosestPointOn(local[0], local[1], local[2]) as double[];
                    PMNeed(closest != null && closest.Length >= 3, "SOURCE_CONTACT_WITNESS_UNAVAILABLE");
                    double sourceError = PMDistance(local, closest.Take(3).ToArray()), targetError = PMMaterialDistance(plane, p);
                    if (sourceError <= PMMaterialTolerance && targetError <= PMMaterialTolerance && outline.All(boundary => PMDistanceToSegment(boundary, p) > 10 * PMTolerance))
                        return new { targetPointM = p, sourcePointM = local, sourcePointInTargetM = sourcePointInTarget,
                            normalProjectionOffsetM = projectionOffset, sourceMaterialErrorM = sourceError, targetMaterialErrorM = targetError,
                            boundaryClearanceM = plane.boundary.Concat(outline).Min(boundary => PMDistanceToSegment(boundary, p)) };
                }
            }
            return null;
        }

        private static object PMContactEvidence(PMContact contact)
        {
            bool partition = contact.source.kind == "partition_rib" && contact.target.kind == "partition";
            bool side = contact.source.kind == "side_rib" && (contact.target.kind == "side" || contact.target.kind == "partition");
            return new { sourceComponent = contact.source.name, sourcePath = contact.source.path,
                sourceConfiguration = contact.source.configuration, sourceSha256 = ParametricContext.Hash(contact.source.path),
                sourceToRoot = contact.source.transformData, targetComponent = contact.target.name, targetPath = contact.target.path,
                targetConfiguration = contact.target.configuration, rootToTarget = contact.target.inverseData,
                exactSourceBoundsInTargetM = contact.exactBounds, normalAxis = contact.plane.axis, contactPlaneM = contact.plane.value,
                sourcePlaneM = contact.sourcePlane, sourceNormalSign = contact.sourceNormalSign, targetNormalSign = contact.plane.normalSign,
                relation = partition ? "approved_0p5mm_partition_standoff_projection" : side ? "approved_0p2mm_vertical_rib_standoff_projection" : "exact_planar_contact",
                actualContact = !(partition || side), nominalStandOffMm = partition ? .5 : side ? .2 : 0,
                signedNormalGapM = contact.projectionOffset * contact.sourceNormalSign,
                normalProjectionOffsetM = contact.projectionOffset, measuredStandOffMm = Math.Abs(contact.projectionOffset) * 1000,
                contactFaces = contact.faces, originalContactOuterLoop = contact.originalOutline,
                projectedContactOuterLoop = contact.outline, materialClippedContactOuterLoop = contact.materialOutline };
        }

        private static List<PMSegment> PMCornerLs(PMContact contact)
        {
            int normal = contact.plane.axis;
            int[] axes = PMAxes(normal);
            double[] bounds = contact.exactBounds;
            PMNeed(axes.All(axis => bounds[axis + 3] - bounds[axis] > 2 * PMShortLeg), "CORNER_FOOTPRINT_TOO_SMALL");
            var result = new List<PMSegment>();
            foreach (int first in new[] { 0, 1 }) foreach (int second in new[] { 0, 1 })
            {
                double[] corner = new double[3]; corner[normal] = contact.plane.value;
                corner[axes[0]] = bounds[axes[0] + 3 * first];
                corner[axes[1]] = bounds[axes[1] + 3 * second];
                foreach (int index in new[] { 0, 1 })
                {
                    double[] end = (double[])corner.Clone();
                    end[axes[index]] += ((index == 0 ? first : second) == 0 ? -1 : 1) * PMShortLeg;
                    result.Add(PMLine((double[])corner.Clone(), end, normal, "exact projected solid outer corner; outward 3 mm L leg; R4 review candidate"));
                }
            }
            PMNeed(result.Count == 8, "CORNER_L_COUNT_INVALID");
            return result;
        }

        private static List<PMSegment> PMShelfEnds(PMContact contact)
        {
            int[] axes = PMAxes(contact.plane.axis);
            int longAxis = axes.OrderByDescending(i => contact.exactBounds[i + 3] - contact.exactBounds[i]).First();
            int shortAxis = axes.Single(i => i != longAxis);
            PMNeed(contact.exactBounds[longAxis + 3] - contact.exactBounds[longAxis] > contact.exactBounds[shortAxis + 3] - contact.exactBounds[shortAxis] + PMTolerance, "SHELF_LONG_AXIS_AMBIGUOUS");
            var result = new List<PMSegment>();
            foreach (double side in new[] { contact.exactBounds[shortAxis], contact.exactBounds[shortAxis + 3] })
            {
                var outer = contact.outline.Where(line => line.kind == "line" && Math.Abs(line.a[shortAxis] - side) <= PMTolerance && Math.Abs(line.b[shortAxis] - side) <= PMTolerance && Math.Abs(line.a[longAxis] - line.b[longAxis]) > 2 * PMShortLeg).ToList();
                PMNeed(outer.Count == 1, "SHELF_OUTER_LONG_EDGE_AMBIGUOUS " + contact.source.name);
                PMSegment edge = outer[0];
                var pieces = PMClipMaterial(edge, contact.plane);
                PMNeed(pieces.Count > 0, "SHELF_OUTER_LONG_EDGE_OUTSIDE_MATERIAL");
                double low = pieces.SelectMany(line => new[] { line.a[longAxis], line.b[longAxis] }).Min();
                double high = pieces.SelectMany(line => new[] { line.a[longAxis], line.b[longAxis] }).Max();
                PMNeed(high - low > 2 * PMShortLeg, "SHELF_MATERIAL_EDGE_TOO_SHORT");
                foreach (bool first in new[] { true, false })
                {
                    double[] a = (double[])edge.a.Clone(), b = (double[])edge.a.Clone();
                    a[longAxis] = first ? low : high - PMShortLeg;
                    b[longAxis] = first ? low + PMShortLeg : high;
                    result.Add(PMLine(a, b, contact.plane.axis, "first/last 3 mm of actual material-clipped outer long edge; omit inner edges and end crossbars"));
                }
            }
            PMNeed(result.Count == 4, "SHELF_RIB_END_LINE_COUNT_INVALID");
            return result;
        }

        private static List<PMSegment> PMSideEnds(PMContact contact)
        {
            int[] axes = PMAxes(contact.plane.axis);
            int longAxis = axes.OrderByDescending(i => contact.exactBounds[i + 3] - contact.exactBounds[i]).First();
            double[] up = PMVector(new[] { 0.0, 1.0, 0.0 }, contact.target.inverseData);
            PMNeed(Math.Abs(up[longAxis]) > 1 - 1e-9, "SIDE_LONG_AXIS_NOT_CABINET_UP");
            double low = contact.exactBounds[longAxis] + PMSideEnd, high = contact.exactBounds[longAxis + 3] - PMSideEnd;
            PMNeed(high > low + PMTolerance, "SIDE_RIB_TOO_SHORT_FOR_50MM_ENDS");
            var result = new List<PMSegment>();
            foreach (PMSegment original in contact.materialOutline)
            {
                var cuts = new List<double> { 0, 1 };
                foreach (double boundary in new[] { low, high })
                {
                    if (original.kind == "line")
                    {
                        double delta = original.b[longAxis] - original.a[longAxis];
                        if (Math.Abs(delta) > 1e-12) cuts.Add((boundary - original.a[longAxis]) / delta);
                    }
                    else
                    {
                        int otherAxis = axes.Single(i => i != longAxis);
                        double offset = boundary - original.center[longAxis];
                        if (Math.Abs(offset) <= original.radius + PMTolerance)
                            foreach (double sign in new[] { -1.0, 1.0 })
                            {
                                double[] p = (double[])original.center.Clone(); p[longAxis] = boundary;
                                p[otherAxis] += sign * Math.Sqrt(Math.Max(0, original.radius * original.radius - offset * offset));
                                if (PMContains(original, p)) cuts.Add(PMParameter(original, p));
                            }
                    }
                }
                List<double> sorted = PMCuts(cuts);
                for (int i = 0; i + 1 < sorted.Count; i++)
                {
                    double start = sorted[i], end = sorted[i + 1];
                    if ((end - start) * original.length <= 1e-9) continue;
                    double ordinate = PMOn(original, (start + end) / 2)[longAxis];
                    if (ordinate <= low + 1e-10 || ordinate >= high - 1e-10)
                    {
                        PMSegment kept = PMTrim(original, start, end);
                        kept.basis = "subset of exact original contact outline; first/last 50 mm from solid extrema; original end crosslines retained; no added closure";
                        PMNeed(PMContains(original, kept.a) && PMContains(original, kept.b), "SIDE_CLIP_ADDED_GEOMETRY");
                        result.Add(kept);
                    }
                }
            }
            PMNeed(result.Count > 0, "SIDE_END_OUTLINE_EMPTY");
            foreach (bool first in new[] { true, false })
            {
                double end = contact.exactBounds[longAxis + (first ? 0 : 3)];
                var originals = contact.materialOutline.Where(s => s.kind == "line" && Math.Abs(s.a[longAxis] - end) <= PMTolerance && Math.Abs(s.b[longAxis] - end) <= PMTolerance).ToList();
                PMNeed(originals.Count > 0, "SIDE_ORIGINAL_END_CROSSLINE_UNAVAILABLE");
                PMNeed(originals.All(line => result.Any(kept => PMSame(line, kept))), "SIDE_ORIGINAL_END_CROSSLINE_LOST");
            }
            return PMUnique(result);
        }

        private static bool PMSupported(PMSegment segment, PMPlane plane)
        {
            var pieces = PMClipMaterial(segment, plane);
            return Math.Abs(pieces.Sum(piece => piece.length) - segment.length) <= PMMaterialTolerance &&
                new[] { 0.0, .25, .5, .75, 1.0 }.All(t => PMMaterialDistance(plane, PMOn(segment, t)) <= PMMaterialTolerance);
        }

        private static List<object> PMMaterialChecks(List<PMSegment> segments, PMPlane plane)
        {
            var result = new List<object>();
            for (int index = 0; index < segments.Count; index++)
            {
                PMSegment segment = segments[index];
                PMNeed(PMSupported(segment, plane), "MARK_OUTSIDE_ACTUAL_MATERIAL segment=" + index.ToString(CultureInfo.InvariantCulture));
                foreach (double t in new[] { 0.0, .25, .5, .75, 1.0 })
                {
                    double[] point = PMOn(segment, t);
                    result.Add(new { segment = index, parameter = t, pointModelM = point, distanceToMaterialM = PMMaterialDistance(plane, point), exactBoundaryClippingCoveredEntireSegment = true });
                }
            }
            return result;
        }

        private static double[] PMOutlineBounds(List<PMSegment> segments)
        {
            var points = new List<double[]>();
            foreach (PMSegment segment in segments)
            {
                points.Add(segment.a); points.Add(segment.b);
                if (segment.kind != "arc") continue;
                int[] axes = PMAxes(segment.axis);
                foreach (int axis in axes) foreach (double sign in new[] { -1.0, 1.0 })
                {
                    double[] p = (double[])segment.center.Clone(); p[axis] += sign * segment.radius;
                    if (PMContains(segment, p)) points.Add(p);
                }
            }
            PMNeed(points.Count > 0, "PLANAR_FACE_BOUNDS_UNAVAILABLE");
            return Enumerable.Range(0, 6).Select(i => i < 3 ? points.Min(p => p[i]) : points.Max(p => p[i - 3])).ToArray();
        }

        private static PMPlan PMArrow(PMContact contact)
        {
            PMInstance source = contact.source;
            double[] normalInTarget = new double[3]; normalInTarget[contact.plane.axis] = 1;
            double[] normal = PMVector(PMVector(normalInTarget, contact.target.transformData), source.inverseData);
            int axis = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(normal[i])).First();
            PMNeed(Math.Abs(normal[axis]) > 1 - 1e-9, "ARROW_SOURCE_PLANE_NOT_ALIGNED");
            double[] contactPoint = new double[3]; contactPoint[contact.plane.axis] = contact.sourcePlane;
            contactPoint = PMPoint(PMPoint(contactPoint, contact.target.transformData), source.inverseData);
            double[] bounds = PMBounds(source.bodies, null, null);
            bool minimum = Math.Abs(contactPoint[axis] - bounds[axis]) <= PMTolerance;
            bool maximum = Math.Abs(contactPoint[axis] - bounds[axis + 3]) <= PMTolerance;
            PMNeed(minimum != maximum, "ARROW_CONTACT_NOT_EXACT_SOLID_EXTREME");
            int visibleNormalSign = minimum ? 1 : -1;
            var visiblePlanes = PMPlanes(source, axis).Where(candidate => candidate.normalSign == visibleNormalSign &&
                (candidate.value - contactPoint[axis]) * visibleNormalSign > PMMaterialTolerance).ToList();
            PMNeed(visiblePlanes.Count > 0, "ARROW_VISIBLE_PLANAR_FACE_UNAVAILABLE");
            double largestArea = visiblePlanes.Max(candidate => candidate.faces.Sum(face => face.GetArea()));
            PMPlane plane = visiblePlanes.SingleOrDefault(candidate => Math.Abs(candidate.faces.Sum(face => face.GetArea()) - largestArea) <= 1e-12);
            PMNeed(plane != null, "ARROW_VISIBLE_PLANAR_FACE_UNAVAILABLE");
            PMReadPlaneBoundary(plane, source.name + " visible marking face");
            double[] up = PMVector(new[] { 0.0, 1.0, 0.0 }, source.inverseData);
            int longAxis = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(up[i])).First();
            PMNeed(longAxis != axis && Math.Abs(up[longAxis]) > 1 - 1e-9, "ARROW_UP_DIRECTION_AMBIGUOUS");
            double[] outward = new double[3]; outward[axis] = minimum ? 1 : -1;
            double[] right = new[] { up[1] * outward[2] - up[2] * outward[1], up[2] * outward[0] - up[0] * outward[2], up[0] * outward[1] - up[1] * outward[0] };
            int shortAxis = PMAxes(axis).Single(i => i != longAxis);
            double[] faceBounds = PMOutlineBounds(plane.boundary);
            PMPlan plan = null;
            foreach (double verticalFraction in new[] { .75, .5, .25, .9, .1 })
            {
                foreach (double horizontalFraction in new[] { .5, .25, .75 })
                {
                    double[] anchor = new double[3]; anchor[axis] = plane.value;
                    anchor[longAxis] = faceBounds[longAxis] + verticalFraction * (faceBounds[longAxis + 3] - faceBounds[longAxis]);
                    anchor[shortAxis] = faceBounds[shortAxis] + horizontalFraction * (faceBounds[shortAxis + 3] - faceBounds[shortAxis]);
                    Func<double, double, double[]> p = (x, y) => anchor.Select((v, i) => v + x * right[i] + y * up[i]).ToArray();
                    var lines = new List<PMSegment> {
                        PMLine(p(0, 0), p(0, .012), axis, "cabinet world +Y installation arrow"),
                        PMLine(p(0, .012), p(-.003, .009), axis, "installation arrow left head"),
                        PMLine(p(0, .012), p(.003, .009), axis, "installation arrow right head"),
                        PMLine(p(0, -.003), p(0, -.011), axis, "上 glyph vertical stroke"),
                        PMLine(p(0, -.007), p(.003, -.007), axis, "上 glyph upper horizontal stroke"),
                        PMLine(p(-.004, -.011), p(.004, -.011), axis, "上 glyph lower horizontal stroke")
                    };
                    if (!lines.All(line => PMSupported(line, plane))) continue;
                    plan = new PMPlan { target = source, plane = plane, family = "侧加强筋朝上箭头与上", expected = lines };
                    plan.provenance.Add(new { sourceContact = PMContactEvidence(contact), worldUp = new[] { 0, 1, 0 }, upInPart = up, rightInPart = right,
                        exactSourceBoundsM = bounds, markingFaceBoundsM = faceBounds, markingPlaneM = plane.value, anchorM = anchor, glyph = "上", glyphRepresentation = "three native sketch line strokes" });
                    break;
                }
                if (plan != null) break;
            }
            PMNeed(plan != null, "ARROW_AND_GLYPH_HAVE_NO_SUPPORTED_FACE_LOCATION " + source.name);
            return plan;
        }

        private static void PMAddContactPlan(List<PMPlan> plans, PMContact contact, string family, List<PMSegment> expected)
        {
            PMPlan plan = plans.FirstOrDefault(p => p.target == contact.target && p.family == family && p.plane == contact.plane);
            if (plan == null) { plan = new PMPlan { target = contact.target, plane = contact.plane, family = family }; plans.Add(plan); }
            plan.expected.AddRange(expected);
            plan.provenance.Add(PMContactEvidence(contact));
        }

        private static object PMContactDiagnostic(PMInstance target, List<PMInstance> sources, List<PMPlane> planes)
        {
            return new {
                target = target.name, path = target.path, configuration = target.configuration, transform = target.transformData, normalAxis = planes[0].axis,
                targetPlanes = planes.Select(plane => new { axis = plane.axis, normalSign = plane.normalSign, value = plane.value,
                    faces = plane.faces.Select(face => new { normal = face.Normal, box = face.GetBox(), areaM2 = face.GetArea() }).ToArray() }).ToArray(),
                sources = sources.Select(source => new {
                    name = source.name, path = source.path, configuration = source.configuration, transform = source.transformData,
                    exactBoundsInTargetM = PMBounds(source.bodies, source, target),
                    planesInTarget = source.bodies.SelectMany(body => (body.GetFaces() as Array ?? new object[0]).Cast<Face2>())
                        .Where(face => { Surface surface = face.GetSurface() as Surface; return surface != null && surface.IsPlane(); })
                        .Select(face => new {
                            normalModel = face.Normal, planeModel = ((Surface)face.GetSurface()).PlaneParams,
                            normalInTarget = PMVector(PMVector(face.Normal as double[], source.transformData), target.inverseData),
                            pointInTargetM = PMPoint(PMPoint((((Surface)face.GetSurface()).PlaneParams as double[]).Skip(3).Take(3).ToArray(), source.transformData), target.inverseData),
                            areaM2 = face.GetArea()
                        }).ToArray()
                }).ToArray()
            };
        }

        private static void PMRecordAssemblyGeometry(ModelDoc2 root, List<PMInstance> instances, List<object> rows)
        {
            rows.Add(new {
                inspection = "production_marks_assembly_preflight",
                components = (((AssemblyDoc)root).GetComponents(false) as Array ?? new object[0]).Cast<Component2>()
                    .Where(component => !component.IsSuppressed()).Select(component => {
                        Component2 parent = component.GetParent();
                        return new { name = component.Name2, path = component.GetPathName(), configuration = component.ReferencedConfiguration,
                            parent = parent == null ? "" : parent.Name2, kind = PMKind(component) };
                    }).OrderBy(component => component.name, StringComparer.Ordinal).ToArray(),
                geometry = instances.Where(part => part.kind == "side" || part.kind == "partition")
                    .OrderBy(part => part.name, StringComparer.Ordinal).SelectMany(target => Enumerable.Range(0, 3).Select(axis => PMContactDiagnostic(target,
                        instances.Where(source => source.kind == "partition_rib" && target.kind == "partition" || source.kind == "side_rib" && source.parent == target.parent).ToList(),
                        PMPlanes(target, axis)))).ToArray()
            });
        }

        private static int PMSideContactAxis(PMInstance source, PMInstance target)
        {
            var faces = source.bodies.SelectMany(body => (body.GetFaces() as Array ?? new object[0]).Cast<Face2>())
                .Where(face => { Surface surface = face.GetSurface() as Surface; return surface != null && surface.IsPlane(); }).ToList();
            PMNeed(faces.Count > 0, "SIDE_PRIMARY_PLANE_UNAVAILABLE");
            Face2 primary = faces.OrderByDescending(face => face.GetArea()).First();
            double[] normal = PMVector(PMVector(primary.Normal as double[], source.transformData), target.inverseData);
            int axis = Enumerable.Range(0, 3).OrderByDescending(index => Math.Abs(normal[index])).First();
            PMNeed((axis == 0 || axis == 2) && Math.Abs(normal[axis]) > 1 - 1e-9 &&
                faces.Any(face => face.GetArea() > primary.GetArea() / 2 && PMDot(face.Normal as double[], primary.Normal as double[]) < -1 + 1e-9),
                "SIDE_PRIMARY_PLANE_AXIS_AMBIGUOUS " + source.name);
            PMNeed(target.kind != "partition" || axis == 0, "PARTITION_SIDE_RIB_AXIS_INVALID " + source.name);
            return axis;
        }

        private static List<PMPlan> PMBuildPlans(List<PMInstance> instances)
        {
            var plans = new List<PMPlan>();
            var used = new HashSet<PMInstance>();
            var partitionProjections = new List<PMContact>();
            var sideProjections = new List<PMContact>();
            foreach (PMInstance target in instances.Where(p => p.kind == "door" || p.kind == "shelf" || p.kind == "side" || p.kind == "partition"))
            {
                int axis = target.kind == "door" ? 2 : target.kind == "shelf" ? 1 : 0;
                List<PMPlane> planes = PMPlanes(target, axis);
                string sourceKind = target.kind == "door" ? "door_rib" : target.kind == "shelf" ? "shelf_rib" : target.kind == "side" ? "side_rib" : "partition_rib";
                var sources = instances.Where(p => p.kind == sourceKind && (target.kind == "partition" || p.parent == target.parent)).ToList();
                if (target.kind == "partition")
                {
                    sources.AddRange(instances.Where(p => p.kind == "side_rib" && p.parent == target.parent));
                    PMNeed(sources.Count(p => p.kind == "partition_rib") == 2 && sources.Count(p => p.kind == "side_rib") == 2, "PARTITION_WELD_COMPONENT_COUNT_INVALID " + target.name);
                }
                if (target.kind == "door") sources.AddRange(instances.Where(p => p.kind == "door_contact" && p.parent == target.parent));
                if (target.kind == "door") PMNeed(sources.Count(p => p.kind == "door_rib") == 1 && sources.Count(p => p.kind == "door_contact") == 3, "DOOR_WELD_COMPONENT_COUNT_INVALID " + target.name);
                else if (target.kind == "shelf") PMNeed(sources.Count == 2, "WELD_REINFORCEMENT_COUNT_INVALID " + target.name);
                else if (target.kind == "side") PMNeed(sources.Count == 3, "SIDE_WELD_REINFORCEMENT_COUNT_INVALID " + target.name);
                else PMNeed(sources.Count > 0, "PARTITION_REINFORCEMENT_MISSING");
                int contacting = 0;
                foreach (PMInstance source in sources)
                {
                    int contactAxis = source.kind == "side_rib" ? PMSideContactAxis(source, target) : axis;
                    List<PMPlane> contactPlanes = contactAxis == axis ? planes : PMPlanes(target, contactAxis);
                    List<PMContact> contacts = PMContacts(source, target, contactPlanes);
                    PMNeed(contacts.Count == 1, "CONTACT_PLANE_MISSING_OR_AMBIGUOUS " + source.name + " -> " + target.name +
                        (contacts.Count == 1 ? "" : " " + ParametricContext.Json.Serialize(PMContactDiagnostic(target, new List<PMInstance> { source }, contactPlanes))));
                    PMContact contact = contacts[0]; used.Add(source); contacting++;
                    if (source.kind == "partition_rib") partitionProjections.Add(contact);
                    if (source.kind == "side_rib") sideProjections.Add(contact);
                    if (source.kind == "door_rib") PMAddContactPlan(plans, contact, "门加强筋四角L", PMCornerLs(contact));
                    else if (source.kind == "door_contact") PMAddContactPlan(plans, contact, "锁扣与挂钩真实接触轮廓", contact.materialOutline);
                    else if (source.kind == "shelf_rib") PMAddContactPlan(plans, contact, "层板两筋外长边端线", PMShelfEnds(contact));
                    else if (source.kind == "partition_rib") PMAddContactPlan(plans, contact, "隔板加强件四角L", PMCornerLs(contact));
                    else { PMAddContactPlan(plans, contact, contactAxis == 0 ? "侧加强筋首尾50mm_侧面X" : "侧加强筋首尾50mm_后折面Z", PMSideEnds(contact)); plans.Add(PMArrow(contact)); }
                }
                PMNeed(contacting > 0, "SUBSTRATE_HAS_NO_EXACT_CONTACT " + ParametricContext.Json.Serialize(PMContactDiagnostic(target, sources, planes)));
                if (target.kind == "shelf") PMNeed(plans.Where(p => p.target == target).Sum(p => p.expected.Count) == 8, "SHELF_REQUIRES_EXACTLY_EIGHT_OUTER_END_LINES");
            }
            var centralSources = instances.Where(p => p.kind == "partition_rib").ToList();
            var partitions = instances.Where(p => p.kind == "partition").ToList();
            PMNeed(centralSources.Count == 2 && partitions.Count == 2 && partitionProjections.Count == 4 &&
                centralSources.All(source => partitions.All(target => partitionProjections.Count(contact => contact.source == source && contact.target == target) == 1)),
                "PARTITION_PROJECTION_PAIR_COVERAGE_INVALID");
            var sideSources = instances.Where(part => part.kind == "side_rib").ToList();
            PMNeed(sideSources.Count == 10 && sideProjections.Count == 10 && sideSources.All(source => sideProjections.Count(contact => contact.source == source) == 1) &&
                instances.Where(part => part.kind == "side").All(target => sideProjections.Count(contact => contact.target == target && contact.plane.axis == 0) == 2 &&
                    sideProjections.Count(contact => contact.target == target && contact.plane.axis == 2) == 1) &&
                partitions.All(target => sideProjections.Count(contact => contact.target == target && contact.plane.axis == 0) == 2),
                "SIDE_PROJECTION_PAIR_COVERAGE_INVALID");
            PMNeed(instances.Where(p => p.kind.EndsWith("_rib", StringComparison.Ordinal) || p.kind == "door_contact").All(used.Contains), "UNASSIGNED_WELD_COMPONENT");
            var unique = new List<PMPlan>();
            foreach (PMPlan plan in plans.OrderBy(p => p.target.path, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.family, StringComparer.Ordinal))
            {
                plan.expected = PMUnique(plan.expected);
                PMNeed(plan.expected.Count > 0 && plan.expected.All(s => s.kind == "line" || Math.Abs(s.sweep) < 2 * Math.PI - 1e-9), "UNSUPPORTED_FULL_CIRCLE_MARK");
                plan.materialChecks = PMMaterialChecks(plan.expected, plan.plane);
                PMPlan prior = unique.FirstOrDefault(p => String.Equals(p.target.path, plan.target.path, StringComparison.OrdinalIgnoreCase) && p.target.configuration == plan.target.configuration && p.family == plan.family);
                if (prior != null)
                {
                    PMNeed(prior.plane.axis == plan.plane.axis && Math.Abs(prior.plane.value - plan.plane.value) <= PMMaterialTolerance, "SHARED_PART_PLANE_CONFLICT " + plan.target.path);
                    PMSameGeometry(prior.expected, plan.expected, "shared component instances " + plan.target.path);
                    prior.provenance.AddRange(plan.provenance);
                    continue;
                }
                plan.name = PMPrefix + plan.family + "_" + PMHash(plan.target.configuration).Substring(0, 12);
                unique.Add(plan);
            }
            return unique;
        }

        private static List<Feature> PMFeatures(ModelDoc2 model)
        {
            var result = new List<Feature>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Action<Feature, int> visit = null;
            visit = delegate(Feature feature, int depth) {
                PMNeed(depth < 64 && result.Count < 10000, "FEATURE_TRAVERSAL_INCOMPLETE");
                if (!seen.Add(feature.Name)) return;
                result.Add(feature);
                for (Feature child = feature.GetFirstSubFeature() as Feature; child != null; child = child.GetNextSubFeature() as Feature) visit(child, depth + 1);
            };
            for (Feature feature = model.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature) visit(feature, 0);
            return result;
        }

        private static bool PMSuppressed(Feature feature, string configuration)
        {
            bool[] states = feature.IsSuppressed2((int)swInConfigurationOpts_e.swSpecifyConfiguration, new[] { configuration }) as bool[];
            PMNeed(states != null && states.Length == 1, "FEATURE_CONFIGURATION_STATE_UNAVAILABLE " + feature.Name);
            return states[0];
        }

        private static List<string> PMConfigurationOrder(ModelDoc2 model)
        {
            Array raw = model.GetConfigurationNames() as Array;
            PMNeed(raw != null && raw.Length > 0, "CONFIGURATION_TREE_UNAVAILABLE");
            var names = raw.Cast<string>().ToList();
            PMNeed(names.Distinct(StringComparer.Ordinal).Count() == names.Count, "CONFIGURATION_TREE_DUPLICATED");
            var parents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                Configuration configuration = model.GetConfigurationByName(name) as Configuration;
                PMNeed(configuration != null && configuration.Name == name, "CONFIGURATION_TREE_NODE_UNAVAILABLE " + name);
                Configuration parent = configuration.GetParent() as Configuration;
                PMNeed(parent == null || names.Contains(parent.Name), "CONFIGURATION_TREE_PARENT_UNAVAILABLE " + name);
                parents.Add(name, parent == null ? null : parent.Name);
            }
            var depths = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal); string current = name;
                while (current != null) { PMNeed(seen.Add(current), "CONFIGURATION_TREE_CYCLE " + name); current = parents[current]; }
                depths.Add(name, seen.Count - 1);
            }
            return names.OrderBy(name => depths[name]).ThenBy(name => name, StringComparer.Ordinal).ToList();
        }

        private static object[] PMConfigurationStates(PMPlan plan, Feature feature)
        {
            return PMConfigurationOrder(plan.target.model).Select(name => {
                bool expected = name != plan.target.configuration, actual = PMSuppressed(feature, name);
                PMNeed(actual == expected, "MARK_WRONG_CONFIGURATION_SUPPRESSION " + ParametricContext.Json.Serialize(new {
                    path = plan.target.path, feature = plan.name, configuration = name, expectedSuppressed = expected,
                    actualSuppressed = actual, activeConfiguration = plan.target.model.ConfigurationManager.ActiveConfiguration.Name
                }));
                return (object)new { configuration = name, suppressed = actual, expectedSuppressed = expected };
            }).ToArray();
        }

        private static void PMUseConfiguration(ModelDoc2 model, string configuration)
        {
            Array names = model.GetConfigurationNames() as Array;
            PMNeed(names != null && names.Cast<object>().Any(value => String.Equals(Convert.ToString(value), configuration, StringComparison.Ordinal)), "REFERENCED_CONFIGURATION_MISSING " + model.GetPathName() + "/" + configuration);
            if (model.ConfigurationManager.ActiveConfiguration.Name != configuration)
                PMNeed(model.ShowConfiguration2(configuration), "CONFIGURATION_ACTIVATION_FAILED " + configuration);
            PMNeed(model.ConfigurationManager.ActiveConfiguration.Name == configuration, "CONFIGURATION_READBACK_FAILED");
        }

        private static void PMActivate(ISldWorks sw, ModelDoc2 model)
        {
            int error = 0;
            ModelDoc2 active = sw.ActivateDoc3(model.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref error) as ModelDoc2;
            PMNeed(active != null && error == 0 && String.Equals(active.GetPathName(), model.GetPathName(), StringComparison.OrdinalIgnoreCase), "DOCUMENT_ACTIVATION_FAILED " + model.GetPathName());
        }

        private static PMPlane PMCurrentPlane(PMPlan plan)
        {
            Array bodies = ((PartDoc)plan.target.model).GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            PMNeed(bodies != null && bodies.Length == 1, "CURRENT_SUBSTRATE_BODY_UNAVAILABLE");
            var current = new PMInstance { name = plan.target.name, bodies = bodies.Cast<Body2>().ToList() };
            PMPlane plane = PMPlanes(current, plan.plane.axis).SingleOrDefault(p => p.normalSign == plan.plane.normalSign && Math.Abs(p.value - plan.plane.value) <= PMMaterialTolerance);
            PMNeed(plane != null, "CURRENT_SUBSTRATE_PLANE_CHANGED");
            PMReadPlaneBoundary(plane, plan.target.name + " current marking face");
            return plane;
        }

        private static PMSolid PMSolidState(ModelDoc2 model)
        {
            Array raw = ((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            PMNeed(raw != null && raw.Length == 1, "MARKING_MUST_PRESERVE_SINGLE_SOLID " + model.GetPathName());
            var bodies = raw.Cast<Body2>().ToList();
            var rows = new List<string>();
            var result = new PMSolid { bodies = bodies.Count, exactBoundsM = PMBounds(bodies, null, null) };
            foreach (Body2 body in bodies)
            {
                PMNeed(body.Check2() == 0 && body.IsSheetMetal(), "MARKING_TARGET_NOT_VALID_SHEET_METAL " + model.GetPathName());
                double[] mass = body.GetMassProperties(1) as double[];
                PMNeed(mass != null && mass.Length >= 4 && mass[3] > 0, "SOLID_VOLUME_UNAVAILABLE");
                result.volumeM3 += mass[3];
                result.vertices += body.GetVertexCount(); result.edges += body.GetEdgeCount(); result.faces += body.GetFaceCount();
                foreach (Vertex vertex in body.GetVertices() as Array ?? new object[0]) rows.Add("V:" + PMQuant(vertex.GetPoint() as double[]));
                foreach (Edge edge in body.GetEdges() as Array ?? new object[0])
                {
                    Curve curve = edge.GetCurve() as Curve;
                    double[] parameters = edge.GetCurveParams2() as double[];
                    PMNeed(curve != null && parameters != null && parameters.Length >= 8, "PROTECTED_EDGE_UNAVAILABLE");
                    var samples = new List<string>();
                    foreach (double t in new[] { 0.0, .25, .5, .75, 1.0 })
                    {
                        double[] p = curve.Evaluate2(parameters[6] + t * (parameters[7] - parameters[6]), 0) as double[];
                        PMNeed(p != null && p.Length >= 3, "PROTECTED_EDGE_SAMPLE_UNAVAILABLE");
                        samples.Add(PMQuant(p.Take(3)));
                    }
                    samples.Sort(StringComparer.Ordinal);
                    rows.Add("E:" + curve.Identity().ToString(CultureInfo.InvariantCulture) + ":" + String.Join(";", samples.ToArray()));
                }
                foreach (Face2 face in body.GetFaces() as Array ?? new object[0])
                {
                    Surface surface = face.GetSurface() as Surface;
                    PMNeed(surface != null, "PROTECTED_FACE_UNAVAILABLE");
                    rows.Add("F:" + surface.Identity().ToString(CultureInfo.InvariantCulture) + ":" + Math.Round(face.GetArea() / 1e-12).ToString("0", CultureInfo.InvariantCulture) +
                        (surface.IsPlane() ? ":" + PMQuant(surface.PlaneParams as double[]) : surface.IsCylinder() ? ":" + PMQuant(surface.CylinderParams as double[]) : ""));
                }
            }
            rows.Sort(StringComparer.Ordinal);
            result.geometrySha256 = PMHash(String.Join("\n", rows.ToArray()));
            return result;
        }

        private static string PMProtectedFeatures(ModelDoc2 model)
        {
            return ParametricContext.Json.Serialize(PMFeatures(model).Where(f => !f.Name.StartsWith(PMPrefix, StringComparison.Ordinal))
                .Select(f => new { name = f.Name, type = f.GetTypeName2(), suppressed = f.IsSuppressed(), errorCode = f.GetErrorCode() }).OrderBy(f => f.name, StringComparer.Ordinal).ToArray());
        }

        private static void PMSameSolid(PMSolid before, PMSolid after)
        {
            PMNeed(before.geometrySha256 == after.geometrySha256 && before.bodies == after.bodies && before.vertices == after.vertices && before.edges == after.edges && before.faces == after.faces &&
                before.exactBoundsM.Zip(after.exactBoundsM, (a, b) => Math.Abs(a - b) <= PMMaterialTolerance).All(v => v), "SOLID_GEOMETRY_CHANGED_BY_MARKING");
            PMNeed(Math.Abs(before.volumeM3 - after.volumeM3) <= Math.Max(1e-12, before.volumeM3 * 1e-9), "SOLID_VOLUME_CHANGED_BY_MARKING");
        }

        private static PMSegment PMNativeArc(SketchSegment native, SketchArc arc, int axis, double[] inverse)
        {
            SketchPoint a = arc.GetStartPoint2() as SketchPoint, b = arc.GetEndPoint2() as SketchPoint, center = arc.GetCenterPoint2() as SketchPoint;
            PMNeed(a != null && b != null && center != null, "NATIVE_ARC_POINTS_UNAVAILABLE");
            double[] asketch = new[] { a.X, a.Y, a.Z }, bsketch = new[] { b.X, b.Y, b.Z };
            double[] am = PMPoint(asketch, inverse), bm = PMPoint(bsketch, inverse), cm = PMPoint(new[] { center.X, center.Y, center.Z }, inverse);
            double radius = arc.GetRadius(), length = native.GetLength();
            PMNeed(radius > 0 && length > 0 && length / radius < 2 * Math.PI - 1e-9, "NATIVE_ARC_LENGTH_INVALID");
            int[] axes = PMAxes(axis);
            double startAngle = Math.Atan2(am[axes[1]] - cm[axes[1]], am[axes[0]] - cm[axes[0]]);
            var candidates = new List<PMSegment>();
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                double sweep = sign * length / radius;
                double[] end = (double[])cm.Clone(), middle = (double[])cm.Clone();
                end[axes[0]] += radius * Math.Cos(startAngle + sweep); end[axes[1]] += radius * Math.Sin(startAngle + sweep);
                middle[axes[0]] += radius * Math.Cos(startAngle + sweep / 2); middle[axes[1]] += radius * Math.Sin(startAngle + sweep / 2);
                if (!PMNear(end, bm)) continue;
                PMSegment candidate = PMArc(am, bm, cm, middle, axis, "independently read native arc endpoints, radius and arc length");
                if (Math.Abs(candidate.length - length) <= PMTolerance) candidates.Add(candidate);
            }
            candidates = PMUnique(candidates);
            if (candidates.Count == 1) return candidates[0];
            // A semicircle has two equal-length sweeps. Resolve it from the native
            // trimmed curve, accepting either API coordinate convention only after endpoint readback.
            Curve curve = native.GetCurve() as Curve;
            double start = 0, endParameter = 0; bool closed = false, periodic = false;
            PMNeed(curve != null && curve.GetEndParams(out start, out endParameter, out closed, out periodic) && !closed, "NATIVE_SEMICIRCLE_SWEEP_UNAVAILABLE");
            double[] ca = curve.Evaluate2(start, 0) as double[], cb = curve.Evaluate2(endParameter, 0) as double[], midpoint = curve.Evaluate2((start + endParameter) / 2, 0) as double[];
            PMNeed(ca != null && cb != null && midpoint != null && ca.Length >= 3 && cb.Length >= 3 && midpoint.Length >= 3, "NATIVE_ARC_CURVE_SAMPLE_UNAVAILABLE");
            var middleCandidates = new List<double[]>();
            if ((PMNear(ca.Take(3).ToArray(), am) && PMNear(cb.Take(3).ToArray(), bm)) || (PMNear(ca.Take(3).ToArray(), bm) && PMNear(cb.Take(3).ToArray(), am))) middleCandidates.Add(midpoint.Take(3).ToArray());
            if ((PMNear(ca.Take(3).ToArray(), asketch) && PMNear(cb.Take(3).ToArray(), bsketch)) || (PMNear(ca.Take(3).ToArray(), bsketch) && PMNear(cb.Take(3).ToArray(), asketch))) middleCandidates.Add(PMPoint(midpoint.Take(3).ToArray(), inverse));
            var resolved = candidates.Where(candidate => middleCandidates.Any(p => PMNear(PMOn(candidate, .5), p))).ToList();
            PMNeed(resolved.Count == 1, "NATIVE_ARC_SWEEP_AMBIGUOUS");
            return resolved[0];
        }

        private static List<PMSegment> PMReadSketch(PMPlan plan, Feature feature)
        {
            Sketch sketch = feature.GetSpecificFeature2() as Sketch;
            PMNeed(sketch != null && !sketch.Is3D() && feature.GetTypeName2() == "ProfileFeature", "MARK_NOT_NATIVE_SURFACE_SKETCH " + feature.Name);
            PMNeed(!PMSuppressed(feature, plan.target.configuration) && feature.Visible == (int)swVisibilityState_e.swVisibilityStateShown, "MARK_SUPPRESSED_OR_HIDDEN " + feature.Name);
            PMNeed(feature.GetErrorCode() == 0, "MARK_FEATURE_ERROR " + feature.Name);
            double[] inverse = sketch.ModelToSketchTransform.IInverse().ArrayData as double[];
            var result = new List<PMSegment>();
            foreach (SketchSegment native in sketch.GetSketchSegments() as Array ?? new object[0])
            {
                SketchLine line = native as SketchLine;
                SketchArc arc = native as SketchArc;
                PMSegment segment;
                if (line != null)
                {
                    SketchPoint a = line.GetStartPoint2() as SketchPoint, b = line.GetEndPoint2() as SketchPoint;
                    PMNeed(a != null && b != null, "NATIVE_LINE_ENDPOINT_UNAVAILABLE");
                    segment = PMLine(PMPoint(new[] { a.X, a.Y, a.Z }, inverse), PMPoint(new[] { b.X, b.Y, b.Z }, inverse), plan.plane.axis, "independently read native sketch line");
                }
                else
                {
                    PMNeed(arc != null && arc.IsCircle() != 1, "UNSUPPORTED_NATIVE_MARK_SEGMENT");
                    segment = PMNativeArc(native, arc, plan.plane.axis, inverse);
                    PMNeed(Math.Abs(segment.radius - arc.GetRadius()) <= PMTolerance, "NATIVE_ARC_RADIUS_MISMATCH");
                }
                segment.construction = native.ConstructionGeometry;
                PMNeed(Math.Abs(segment.length - native.GetLength()) <= PMTolerance, "NATIVE_MARK_LENGTH_MISMATCH");
                PMNeed(Math.Abs(segment.a[plan.plane.axis] - plan.plane.value) <= PMMaterialTolerance && Math.Abs(segment.b[plan.plane.axis] - plan.plane.value) <= PMMaterialTolerance, "NATIVE_MARK_LEFT_MATERIAL_PLANE");
                result.Add(segment);
            }
            PMNeed(result.Count > 0, "NATIVE_MARK_EMPTY");
            return result;
        }

        private static Feature PMCreateSketch(ISldWorks sw, PMPlan plan)
        {
            ModelDoc2 model = plan.target.model;
            PMActivate(sw, model);
            PMUseConfiguration(model, plan.target.configuration);
            PMNeed(model.GetActiveSketch2() == null, "ANOTHER_SKETCH_IS_BEING_EDITED");
            PMPlane material = PMCurrentPlane(plan);
            double[] point = PMOn(plan.expected[0], .5);
            Face2 face = material.faces.FirstOrDefault(candidate => {
                double[] closest = candidate.GetClosestPointOn(point[0], point[1], point[2]) as double[];
                return closest != null && PMDistance(point, closest.Take(3).ToArray()) <= PMMaterialTolerance;
            });
            PMNeed(face != null, "SKETCH_SUPPORT_FACE_UNAVAILABLE");
            var beforeNames = new HashSet<string>(PMFeatures(model).Select(f => f.Name), StringComparer.Ordinal);
            model.ClearSelection2(true);
            PMNeed(((Entity)face).Select4(false, null), "SKETCH_SUPPORT_FACE_SELECTION_FAILED");
            SketchManager manager = model.SketchManager;
            bool previousAdd = manager.AddToDB, previousDisplay = manager.DisplayWhenAdded;
            bool editing = false;
            try
            {
                manager.InsertSketch(true); editing = true;
                Sketch sketch = manager.ActiveSketch;
                PMNeed(sketch != null && !sketch.Is3D(), "NATIVE_MARK_SKETCH_CREATION_FAILED");
                double[] transform = sketch.ModelToSketchTransform.ArrayData as double[];
                manager.AddToDB = true; manager.DisplayWhenAdded = false;
                foreach (PMSegment segment in plan.expected)
                {
                    double[] a = PMPoint(segment.a, transform), b = PMPoint(segment.b, transform);
                    PMNeed(Math.Abs(a[2]) <= PMMaterialTolerance && Math.Abs(b[2]) <= PMMaterialTolerance, "MARK_LEFT_NATIVE_SKETCH_PLANE");
                    SketchSegment added;
                    if (segment.kind == "line") added = manager.CreateLine(a[0], a[1], 0, b[0], b[1], 0);
                    else
                    {
                        double[] mid = PMPoint(PMOn(segment, .5), transform);
                        PMNeed(Math.Abs(mid[2]) <= PMMaterialTolerance, "ARC_LEFT_NATIVE_SKETCH_PLANE");
                        added = manager.Create3PointArc(a[0], a[1], 0, b[0], b[1], 0, mid[0], mid[1], 0);
                    }
                    PMNeed(added != null, "NATIVE_SEGMENT_CREATION_FAILED");
                    added.ConstructionGeometry = false;
                    manager.AddToDB = false;
                    model.ClearSelection2(true);
                    PMNeed(added.Select4(false, null), "NATIVE_SEGMENT_FIX_SELECTION_FAILED");
                    model.SketchAddConstraints("sgFIXED");
                    manager.AddToDB = true;
                }
                manager.AddToDB = previousAdd; manager.DisplayWhenAdded = previousDisplay;
                manager.InsertSketch(true); editing = false;
            }
            finally
            {
                manager.AddToDB = previousAdd; manager.DisplayWhenAdded = previousDisplay;
                if (editing && manager.ActiveSketch != null) manager.InsertSketch(true);
                model.ClearSelection2(true);
            }
            var created = PMFeatures(model).Where(f => !beforeNames.Contains(f.Name) && f.GetSpecificFeature2() is Sketch).ToList();
            PMNeed(created.Count == 1, "NEW_MARK_FEATURE_IDENTITY_AMBIGUOUS");
            Feature feature = created[0]; feature.Name = plan.name;
            PMNeed(feature.Name == plan.name, "MARK_FEATURE_NAME_READBACK_FAILED");
            PMNeed(feature.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null), "MARK_CONFIGURATION_ISOLATION_FAILED");
            foreach (string name in PMConfigurationOrder(model))
            {
                int action = name == plan.target.configuration ? (int)swFeatureSuppressionAction_e.swUnSuppressFeature : (int)swFeatureSuppressionAction_e.swSuppressFeature;
                PMNeed(feature.SetSuppression2(action, (int)swInConfigurationOpts_e.swSpecifyConfiguration, new[] { name }), "MARK_CONFIGURATION_SET_FAILED " + name);
            }
            PMNeed(feature.Select2(false, 0), "MARK_VISIBILITY_SELECTION_FAILED");
            model.UnblankSketch(); model.ClearSelection2(true);
            PMConfigurationStates(plan, feature);
            return feature;
        }

        private static object PMVerifyPlan(PMPlan plan)
        {
            PMUseConfiguration(plan.target.model, plan.target.configuration);
            var matches = PMFeatures(plan.target.model).Where(f => f.Name == plan.name).ToList();
            PMNeed(matches.Count == 1, "MARK_FEATURE_MISSING_OR_DUPLICATED " + plan.name);
            object[] configurationStates = PMConfigurationStates(plan, matches[0]);
            List<PMSegment> actual = PMReadSketch(plan, matches[0]);
            PMSameGeometry(plan.expected, actual, plan.target.path + "/" + plan.name);
            return new { sketchName = plan.name, family = plan.family, path = plan.target.path, configuration = plan.target.configuration,
                component = plan.target.name, normalAxis = plan.plane.axis, planeM = plan.plane.value,
                expectedSegments = plan.expected, actualSegments = actual, exactSegmentComparison = "PASS",
                materialChecks = PMMaterialChecks(actual, PMCurrentPlane(plan)), sourceContactsAndTransforms = plan.provenance,
                visible = true, noncuttingNativeSketch = true, configurationStates = configurationStates, shortLegMm = PMShortLeg * 1000, sideEndMm = PMSideEnd * 1000,
                shortLegEngineeringStatus = "R4 3 mm review candidate; production release not asserted" };
        }

        private static void PMValidateFeatureInventory(List<PMPlan> plans, bool requirePresent)
        {
            foreach (var group in plans.GroupBy(plan => plan.target.path, StringComparer.OrdinalIgnoreCase))
            {
                ModelDoc2 model = group.First().target.model;
                var expected = new HashSet<string>(group.Select(plan => plan.name), StringComparer.Ordinal);
                List<Feature> features = PMFeatures(model);
                var present = features.Where(f => f.Name.StartsWith(PMPrefix, StringComparison.Ordinal)).ToList();
                PMNeed(present.All(f => expected.Contains(f.Name)), "UNEXPECTED_OWNED_MARK_FEATURE " + model.GetPathName());
                PMNeed(!features.Any(f => f.Name == "生产打标_焊接零件实际接触轮廓" || f.Name == "生产打标_端部短定位线"), "LEGACY_CONTACT_MARK_REQUIRES_EXPLICIT_RECONCILIATION " + model.GetPathName());
                PMNeed(requirePresent ? present.Count == expected.Count : present.Count == 0 || present.Count == expected.Count, "PARTIAL_OR_MISSING_MARK_SET " + model.GetPathName());
                if (present.Count == 0) continue;
                foreach (PMPlan plan in group)
                {
                    Feature feature = present.Single(f => f.Name == plan.name);
                    PMConfigurationStates(plan, feature);
                }
            }
        }

        private static void PMReleaseInstances(List<PMInstance> instances)
        {
            if (instances == null) return;
            foreach (PMInstance instance in instances)
            {
                foreach (Body2 body in instance.bodies) Marshal.ReleaseComObject(body);
                Marshal.ReleaseComObject(instance.inverse); Marshal.ReleaseComObject(instance.transform);
            }
        }

        internal static void ApplyProductionMarks(ISldWorks sw, ModelDoc2 finalRoot, List<object> rows)
        {
            PMNeed(sw != null && finalRoot != null && rows != null && !finalRoot.IsOpenedReadOnly(), "APPLY_REQUIRES_LOADED_WRITABLE_FINAL_ROOT");
            List<PMInstance> instances = null;
            var configurations = new Dictionary<ModelDoc2, string>();
            try
            {
                instances = PMInstances(finalRoot);
                PMRecordAssemblyGeometry(finalRoot, instances, rows);
                List<PMPlan> plans = PMBuildPlans(instances);
                foreach (PMPlan plan in plans)
                {
                    PMNeed(!plan.target.model.IsOpenedReadOnly(), "APPLY_TARGET_IS_READ_ONLY " + plan.target.path);
                    ParametricContext.AssertMutation(plan.target.path);
                    if (!configurations.ContainsKey(plan.target.model)) configurations.Add(plan.target.model, plan.target.model.ConfigurationManager.ActiveConfiguration.Name);
                }
                PMValidateFeatureInventory(plans, false);
                // Every geometry, contact, shared-instance and material preflight above precedes the first edit.
                foreach (var group in plans.GroupBy(plan => plan.target.path + "\n" + plan.target.configuration, StringComparer.OrdinalIgnoreCase))
                {
                    PMPlan first = group.First(); ModelDoc2 model = first.target.model;
                    PMUseConfiguration(model, first.target.configuration);
                    PMSolid before = PMSolidState(model);
                    string protectedFeatures = PMProtectedFeatures(model), beforeHash = ParametricContext.Hash(first.target.path);
                    bool reused = PMFeatures(model).Any(f => f.Name == first.name);
                    if (!reused) foreach (PMPlan plan in group) PMCreateSketch(sw, plan);
                    var marks = group.Select(PMVerifyPlan).ToArray();
                    PMSolid after = PMSolidState(model);
                    PMSameSolid(before, after);
                    PMNeed(protectedFeatures == PMProtectedFeatures(model), "PROTECTED_FEATURE_STATE_CHANGED");
                    PMNeed(beforeHash == ParametricContext.Hash(first.target.path), "MARK_HELPER_WROTE_NATIVE_FILE");
                    rows.Add(new { inspection = "production_marks_apply", path = first.target.path, configuration = first.target.configuration,
                        sourceFileSha256BeforeCallerSave = beforeHash, markSolidBefore = before, markSolidAfter = after,
                        solidUnchanged = true, protectedFeatureStateSha256 = PMHash(protectedFeatures), reused = reused,
                        changed = !reused, callerMustSave = !reused, marks = marks, documentSavePerformed = false });
                }
                PMValidateFeatureInventory(plans, true);
            }
            finally
            {
                foreach (var entry in configurations) PMUseConfiguration(entry.Key, entry.Value);
                PMActivate(sw, finalRoot);
                PMReleaseInstances(instances);
            }
        }

        internal static void VerifyProductionMarks(ISldWorks sw, ModelDoc2 finalRoot, List<object> rows)
        {
            PMNeed(sw != null && finalRoot != null && rows != null && finalRoot.IsOpenedReadOnly(), "VERIFY_REQUIRES_LOADED_READ_ONLY_FINAL_ROOT");
            List<PMInstance> instances = null;
            var configurations = new Dictionary<ModelDoc2, string>();
            var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                instances = PMInstances(finalRoot);
                foreach (PMInstance instance in instances)
                {
                    PMNeed(instance.model.IsOpenedReadOnly(), "VERIFY_PART_NOT_READ_ONLY " + instance.path);
                    if (!fileHashes.ContainsKey(instance.path)) fileHashes.Add(instance.path, ParametricContext.Hash(instance.path));
                    if (!configurations.ContainsKey(instance.model)) configurations.Add(instance.model, instance.model.ConfigurationManager.ActiveConfiguration.Name);
                }
                fileHashes.Add(finalRoot.GetPathName(), ParametricContext.Hash(finalRoot.GetPathName()));
                PMRecordAssemblyGeometry(finalRoot, instances, rows);
                // This plan is recomputed from the reopened assembly; apply rows and saved line coordinates are not inputs.
                List<PMPlan> plans = PMBuildPlans(instances);
                PMValidateFeatureInventory(plans, true);
                foreach (var group in plans.GroupBy(plan => plan.target.path + "\n" + plan.target.configuration, StringComparer.OrdinalIgnoreCase))
                {
                    PMPlan first = group.First(); ModelDoc2 model = first.target.model;
                    PMUseConfiguration(model, first.target.configuration);
                    PMSolid before = PMSolidState(model); string protectedFeatures = PMProtectedFeatures(model);
                    var marks = group.Select(PMVerifyPlan).ToArray();
                    PMSolid after = PMSolidState(model); PMSameSolid(before, after);
                    PMNeed(protectedFeatures == PMProtectedFeatures(model), "READ_ONLY_VERIFY_CHANGED_FEATURE_STATE");
                    rows.Add(new { inspection = "production_marks_verify", path = first.target.path, configuration = first.target.configuration,
                        sha256 = fileHashes[first.target.path], readOnly = model.IsOpenedReadOnly(), independentlyRecomputed = true,
                        markSolidCurrent = after, verificationSolidBefore = before, verificationSolidAfter = after,
                        protectedFeatureStateSha256 = PMHash(protectedFeatures), marks = marks,
                        applyBaselineComparisonRequiredByCaller = true, saved = false });
                }
            }
            finally
            {
                foreach (var entry in configurations) PMUseConfiguration(entry.Key, entry.Value);
                PMReleaseInstances(instances);
                foreach (var entry in fileHashes) PMNeed(entry.Value == ParametricContext.Hash(entry.Key), "READ_ONLY_VERIFY_CHANGED_FILE " + entry.Key);
            }
        }
    }
}
